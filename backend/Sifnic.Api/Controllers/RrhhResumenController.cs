using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class RrhhResumenController : Controller
{
    [HttpGet]
    public IActionResult Overview()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var overview = BuildOverview(connection);

            return Json(new
            {
                ok = true,
                data = overview,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el tablero de RRHH.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Bitacora(string? search = null, string? process = null, string? dateFrom = null, string? dateTo = null)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var result = BuildAuditLog(
                connection,
                search,
                process,
                ParseDate(dateFrom),
                ParseDate(dateTo));

            return Json(new
            {
                ok = true,
                data = result,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la bitacora de RRHH.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult EstructuraEmpresa()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            var result = FormalOrganizationStructureSupport.GetTree(connection);

            return Json(new
            {
                ok = true,
                data = result,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la estructura general de la empresa.",
                detail = ex.Message,
            });
        }
    }

    private static OverviewDto BuildOverview(SqlConnection connection)
    {
        const string sql = """
            DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
            DECLARE @limite DATE = DATEADD(DAY, 30, @hoy);
            DECLARE @ultimo30 DATE = DATEADD(DAY, -30, @hoy);

            SELECT
                COUNT(1) AS total_empleados,
                SUM(CASE WHEN e.activo = 1 THEN 1 ELSE 0 END) AS empleados_activos,
                SUM(CASE WHEN e.activo = 0 THEN 1 ELSE 0 END) AS empleados_inactivos,
                SUM(CASE WHEN ee.codigo_estado_empleado = N'VACACIONES' THEN 1 ELSE 0 END) AS en_vacaciones,
                SUM(CASE WHEN ee.codigo_estado_empleado = N'SUSPENDIDO' THEN 1 ELSE 0 END) AS suspendidos,
                (
                    SELECT COUNT(1)
                    FROM rrhh.empleado e2
                    WHERE e2.activo = 1
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM rrhh.contrato c
                          WHERE c.id_empleado = e2.id_empleado
                            AND c.es_contrato_vigente = 1
                            AND (c.fecha_fin IS NULL OR c.fecha_fin >= @hoy)
                      )
                ) AS sin_contrato_vigente
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado;

            SELECT
                COUNT(1) AS total_contratos,
                SUM(CASE WHEN c.es_contrato_vigente = 1 THEN 1 ELSE 0 END) AS contratos_vigentes,
                SUM(
                    CASE
                        WHEN c.es_contrato_vigente = 1
                             AND c.fecha_fin IS NOT NULL
                             AND c.fecha_fin BETWEEN @hoy AND @limite
                        THEN 1
                        ELSE 0
                    END
                ) AS contratos_por_vencer
            FROM rrhh.contrato c;

            SELECT
                COUNT(1) AS total_documentos,
                SUM(
                    CASE
                        WHEN d.ruta_archivo IS NULL OR LTRIM(RTRIM(d.ruta_archivo)) = N''
                        THEN 1
                        ELSE 0
                    END
                ) AS sin_archivo,
                SUM(
                    CASE
                        WHEN d.fecha_vencimiento IS NOT NULL AND d.fecha_vencimiento < @hoy
                        THEN 1
                        ELSE 0
                    END
                ) AS vencidos,
                SUM(
                    CASE
                        WHEN d.fecha_vencimiento IS NOT NULL AND d.fecha_vencimiento BETWEEN @hoy AND @limite
                        THEN 1
                        ELSE 0
                    END
                ) AS por_vencer
            FROM rrhh.expediente_documento d;

            SELECT
                0 AS permisos_pendientes,
                (
                    SELECT COUNT(1)
                    FROM rrhh.vacacion
                    WHERE estado_vacacion = N'SOLICITADA'
                ) AS vacaciones_pendientes,
                (SELECT COUNT(1) FROM rrhh.hora_extra WHERE estado_hora_extra = N'REGISTRADA') AS horas_extra_pendientes;

            SELECT
                COUNT(1) AS total_acciones,
                SUM(CASE WHEN a.fecha_accion >= @ultimo30 THEN 1 ELSE 0 END) AS acciones_ultimo_30
            FROM rrhh.accion_personal a;

            ;WITH ultimas AS
            (
                SELECT
                    m.id_empleado,
                    m.tipo_marcacion,
                    ROW_NUMBER() OVER (
                        PARTITION BY m.id_empleado, m.fecha_operacion
                        ORDER BY m.fecha_hora_marcacion DESC, m.id_marcacion_reloj DESC
                    ) AS rn
                FROM rrhh.marcacion_reloj m
                WHERE m.fecha_operacion = @hoy
            )
            SELECT
                (SELECT COUNT(1) FROM rrhh.marcacion_reloj WHERE fecha_operacion = @hoy) AS marcaciones_hoy,
                (SELECT COUNT(1) FROM ultimas WHERE rn = 1) AS empleados_marcados_hoy,
                (SELECT COUNT(1) FROM ultimas WHERE rn = 1 AND tipo_marcacion = N'ENTRADA') AS jornadas_abiertas_hoy;

            SELECT N'tipo_contrato' AS module_id, COUNT(1) AS total FROM rrhh.tipo_contrato WHERE activo = 1
            UNION ALL
            SELECT N'estado_empleado', COUNT(1) FROM rrhh.estado_empleado
            UNION ALL
            SELECT N'departamento', COUNT(1) FROM rrhh.departamento WHERE activo = 1
            UNION ALL
            SELECT N'cargo', COUNT(1) FROM rrhh.cargo WHERE activo = 1
            UNION ALL
            SELECT N'horario_laboral', COUNT(1) FROM rrhh.horario_laboral WHERE activo = 1
            UNION ALL
            SELECT N'banco', COUNT(1) FROM rrhh.banco WHERE activo = 1
            UNION ALL
            SELECT N'tipo_permiso', COUNT(1) FROM rrhh.tipo_permiso WHERE activo = 1
            UNION ALL
            SELECT N'tipo_hora_extra', COUNT(1) FROM rrhh.tipo_hora_extra WHERE activo = 1;

            SELECT TOP (8)
                proceso,
                tipo_evento,
                referencia_texto,
                descripcion_evento,
                usuario_registro,
                fecha_evento
            FROM operacion.bitacora_operativa
            WHERE modulo = N'RRHH'
            ORDER BY fecha_evento DESC, id_bitacora_operativa DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var summary = new SummaryDto();
        if (reader.Read())
        {
            summary.TotalEmployees = GetInt32(reader, 0);
            summary.ActiveEmployees = GetInt32(reader, 1);
            summary.InactiveEmployees = GetInt32(reader, 2);
            summary.EmployeesOnVacation = GetInt32(reader, 3);
            summary.SuspendedEmployees = GetInt32(reader, 4);
            summary.EmployeesWithoutCurrentContract = GetInt32(reader, 5);
        }

        reader.NextResult();
        if (reader.Read())
        {
            summary.TotalContracts = GetInt32(reader, 0);
            summary.CurrentContracts = GetInt32(reader, 1);
            summary.ExpiringContracts = GetInt32(reader, 2);
        }

        reader.NextResult();
        if (reader.Read())
        {
            summary.TotalDocuments = GetInt32(reader, 0);
            summary.DocumentsWithoutFile = GetInt32(reader, 1);
            summary.ExpiredDocuments = GetInt32(reader, 2);
            summary.ExpiringDocuments = GetInt32(reader, 3);
        }

        reader.NextResult();
        if (reader.Read())
        {
            summary.PendingPermissions = GetInt32(reader, 0);
            summary.PendingVacations = GetInt32(reader, 1);
            summary.PendingOvertime = GetInt32(reader, 2);
        }

        reader.NextResult();
        var totalActions = 0;
        var actionsLast30 = 0;
        if (reader.Read())
        {
            totalActions = GetInt32(reader, 0);
            actionsLast30 = GetInt32(reader, 1);
        }

        reader.NextResult();
        if (reader.Read())
        {
            summary.TodayClockMarks = GetInt32(reader, 0);
            summary.EmployeesMarkedToday = GetInt32(reader, 1);
            summary.OpenClockShiftsToday = GetInt32(reader, 2);
        }

        summary.PendingApprovals =
            summary.PendingVacations +
            summary.PendingOvertime;

        reader.NextResult();
        var catalogCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            catalogCounts[reader.GetString(0)] = GetInt32(reader, 1);
        }

        reader.NextResult();
        var recentActivity = new List<ActivityDto>();
        while (reader.Read())
        {
            recentActivity.Add(new ActivityDto
            {
                Process = reader.GetString(0),
                EventType = reader.GetString(1),
                Reference = reader.GetString(2),
                Description = reader.GetString(3),
                User = reader.GetString(4),
                OccurredAt = reader.GetDateTime(5).ToString("yyyy-MM-ddTHH:mm:ss"),
            });
        }

        var modules = BuildModules(summary, totalActions, actionsLast30, catalogCounts);
        var alerts = BuildAlerts(summary);

        return new OverviewDto
        {
            Summary = summary,
            Modules = modules,
            Alerts = alerts,
            RecentActivity = recentActivity,
        };
    }

    private static AuditLogResultDto BuildAuditLog(
        SqlConnection connection,
        string? search,
        string? process,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var normalizedProcess = string.IsNullOrWhiteSpace(process) ? null : process.Trim();
        var normalizedDateFrom = dateFrom?.Date ?? DateTime.Today.AddDays(-14);
        var normalizedDateTo = dateTo?.Date ?? DateTime.Today;

        if (normalizedDateTo < normalizedDateFrom)
        {
            normalizedDateTo = normalizedDateFrom;
        }

        const string sql = """
            SELECT TOP (250)
                proceso,
                tipo_evento,
                referencia_texto,
                descripcion_evento,
                usuario_registro,
                fecha_evento
            FROM operacion.bitacora_operativa
            WHERE modulo = N'RRHH'
              AND CAST(fecha_evento AS DATE) BETWEEN @fecha_desde AND @fecha_hasta
              AND (@proceso IS NULL OR proceso = @proceso)
              AND (
                    @busqueda IS NULL
                    OR proceso LIKE N'%' + @busqueda + N'%'
                    OR tipo_evento LIKE N'%' + @busqueda + N'%'
                    OR referencia_texto LIKE N'%' + @busqueda + N'%'
                    OR descripcion_evento LIKE N'%' + @busqueda + N'%'
                    OR usuario_registro LIKE N'%' + @busqueda + N'%'
                  )
            ORDER BY fecha_evento DESC, id_bitacora_operativa DESC;

            SELECT DISTINCT proceso
            FROM operacion.bitacora_operativa
            WHERE modulo = N'RRHH'
            ORDER BY proceso;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@fecha_desde", normalizedDateFrom);
        command.Parameters.AddWithValue("@fecha_hasta", normalizedDateTo);
        command.Parameters.Add("@proceso", SqlDbType.NVarChar, 120).Value = (object?)normalizedProcess ?? DBNull.Value;
        command.Parameters.Add("@busqueda", SqlDbType.NVarChar, 250).Value = (object?)normalizedSearch ?? DBNull.Value;

        using var reader = command.ExecuteReader();

        var rows = new List<ActivityDto>();
        while (reader.Read())
        {
            rows.Add(new ActivityDto
            {
                Process = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                EventType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Reference = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                User = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                OccurredAt = reader.IsDBNull(5) ? string.Empty : reader.GetDateTime(5).ToString("yyyy-MM-ddTHH:mm:ss"),
            });
        }

        reader.NextResult();
        var processes = new List<string>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                processes.Add(reader.GetString(0));
            }
        }

        return new AuditLogResultDto
        {
            Rows = rows,
            Processes = processes,
        };
    }

    private static OrganizationStructureDto BuildOrganizationStructure(SqlConnection connection)
    {
        const string sql = """
            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.id_departamento,
                d.codigo_departamento,
                d.nombre_departamento,
                c.id_cargo,
                c.codigo_cargo,
                c.nombre_cargo,
                COALESCE(c.nivel_jerarquico, 0) AS nivel_jerarquico,
                ee.codigo_estado_empleado,
                ee.nombre_estado_empleado,
                e.fecha_ingreso,
                supervisor.id_supervisor_empleado,
                supervisor.codigo_supervisor,
                supervisor.nombre_supervisor,
                supervisor.cargo_supervisor,
                contrato.numero_contrato,
                contrato.nombre_tipo_contrato,
                contrato.fecha_fin
            FROM rrhh.empleado e
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            OUTER APPLY
            (
                SELECT TOP (1)
                    rel.id_supervisor_empleado,
                    sup.codigo_empleado AS codigo_supervisor,
                    COALESCE(NULLIF(sup.nombre_completo, N''), CONCAT(sup.nombres, N' ', sup.apellidos)) AS nombre_supervisor,
                    cargo_sup.nombre_cargo AS cargo_supervisor
                FROM rrhh.empleado_supervision rel
                INNER JOIN rrhh.empleado sup
                    ON sup.id_empleado = rel.id_supervisor_empleado
                LEFT JOIN rrhh.cargo cargo_sup
                    ON cargo_sup.id_cargo = sup.id_cargo
                WHERE rel.id_empleado = e.id_empleado
                  AND rel.activo = 1
                ORDER BY rel.fecha_asignacion DESC, rel.id_empleado_supervision DESC
            ) supervisor
            OUTER APPLY
            (
                SELECT TOP (1)
                    co.numero_contrato,
                    tc.nombre_tipo_contrato,
                    co.fecha_fin
                FROM rrhh.contrato co
                INNER JOIN rrhh.tipo_contrato tc
                    ON tc.id_tipo_contrato = co.id_tipo_contrato
                WHERE co.id_empleado = e.id_empleado
                  AND co.es_contrato_vigente = 1
                ORDER BY co.fecha_inicio DESC, co.id_contrato DESC
            ) contrato
            WHERE e.activo = 1
            ORDER BY COALESCE(c.nivel_jerarquico, 0) DESC, nombre_empleado, e.codigo_empleado;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var rows = new List<OrganizationEmployeeRow>();
        while (reader.Read())
        {
            rows.Add(new OrganizationEmployeeRow
            {
                IdEmpleado = reader.GetInt64(0),
                CodigoEmpleado = reader.GetString(1),
                NombreEmpleado = reader.GetString(2),
                IdDepartamento = reader.GetInt64(3),
                CodigoDepartamento = reader.GetString(4),
                NombreDepartamento = reader.GetString(5),
                IdCargo = reader.GetInt64(6),
                CodigoCargo = reader.GetString(7),
                NombreCargo = reader.GetString(8),
                NivelJerarquico = reader.GetInt32(9),
                CodigoEstadoEmpleado = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                NombreEstadoEmpleado = reader.IsDBNull(11) ? "Sin estado" : reader.GetString(11),
                FechaIngreso = reader.GetDateTime(12).ToString("yyyy-MM-dd"),
                IdSupervisorEmpleado = reader.IsDBNull(13) ? (long?)null : reader.GetInt64(13),
                CodigoSupervisor = reader.IsDBNull(14) ? null : reader.GetString(14),
                NombreSupervisor = reader.IsDBNull(15) ? null : reader.GetString(15),
                CargoSupervisor = reader.IsDBNull(16) ? null : reader.GetString(16),
                NumeroContrato = reader.IsDBNull(17) ? null : reader.GetString(17),
                NombreTipoContrato = reader.IsDBNull(18) ? null : reader.GetString(18),
                FechaFinContrato = reader.IsDBNull(19) ? null : reader.GetDateTime(19).ToString("yyyy-MM-dd"),
            });
        }

        var lookup = rows.ToDictionary(item => item.IdEmpleado);
        var childrenLookup = rows
            .Where(item => item.IdSupervisorEmpleado.HasValue && lookup.ContainsKey(item.IdSupervisorEmpleado.Value))
            .GroupBy(item => item.IdSupervisorEmpleado!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.NivelJerarquico)
                    .ThenBy(item => item.NombreDepartamento)
                    .ThenBy(item => item.NombreEmpleado)
                    .ToList());

        var roots = rows
            .Where(item => !item.IdSupervisorEmpleado.HasValue || !lookup.ContainsKey(item.IdSupervisorEmpleado.Value))
            .OrderByDescending(item => item.NivelJerarquico)
            .ThenBy(item => item.NombreDepartamento)
            .ThenBy(item => item.NombreEmpleado)
            .ToList();

        var generalRoot = roots.FirstOrDefault(item => string.Equals(GetHierarchyCode(item), "GERENCIA_GENERAL", StringComparison.OrdinalIgnoreCase));
        var branchRoots = generalRoot is not null && childrenLookup.TryGetValue(generalRoot.IdEmpleado, out var generalBranches)
            ? generalBranches
            : roots;

        var branchSummaries = branchRoots
            .Select(item => new OrganizationBranchDto
            {
                Key = $"BRANCH-{item.IdEmpleado}",
                Label = BuildBranchLabel(item),
                Subtitle = item.NombreCargo,
                LeaderName = item.NombreEmpleado,
                EmployeeCount = CountTreeNodes(item.IdEmpleado, childrenLookup),
            })
            .ToList();

        var summary = new OrganizationStructureSummaryDto
        {
            TotalEmployees = rows.Count,
            GeneralManagementCount = rows.Count(item => string.Equals(GetHierarchyCode(item), "GERENCIA_GENERAL", StringComparison.OrdinalIgnoreCase)),
            ManagementCount = rows.Count(item => string.Equals(GetHierarchyCode(item), "GERENCIA", StringComparison.OrdinalIgnoreCase)),
            HeadquartersCount = rows.Count(item => string.Equals(GetHierarchyCode(item), "JEFATURA", StringComparison.OrdinalIgnoreCase)),
            CoordinationCount = rows.Count(item => string.Equals(GetHierarchyCode(item), "COORDINACION", StringComparison.OrdinalIgnoreCase)),
            DirectReportCount = rows.Count(item => string.Equals(GetHierarchyCode(item), "SUBORDINADO", StringComparison.OrdinalIgnoreCase)),
        };

        var tree = roots
            .Select(root => BuildOrganizationNode(root, childrenLookup))
            .ToList();

        return new OrganizationStructureDto
        {
            Summary = summary,
            Branches = branchSummaries,
            Tree = tree,
            GeneralManagementName = generalRoot?.NombreEmpleado,
        };
    }

    private static OrganizationNodeDto BuildOrganizationNode(
        OrganizationEmployeeRow row,
        IReadOnlyDictionary<long, List<OrganizationEmployeeRow>> childrenLookup)
    {
        var children = childrenLookup.TryGetValue(row.IdEmpleado, out var items)
            ? items.Select(item => BuildOrganizationNode(item, childrenLookup)).ToList()
            : [];

        return new OrganizationNodeDto
        {
            IdEmpleado = row.IdEmpleado,
            CodigoEmpleado = row.CodigoEmpleado,
            NombreEmpleado = row.NombreEmpleado,
            IdDepartamento = row.IdDepartamento,
            CodigoDepartamento = row.CodigoDepartamento,
            NombreDepartamento = row.NombreDepartamento,
            IdCargo = row.IdCargo,
            CodigoCargo = row.CodigoCargo,
            NombreCargo = row.NombreCargo,
            NivelJerarquico = row.NivelJerarquico,
            HierarchyCode = GetHierarchyCode(row),
            HierarchyLabel = GetHierarchyLabel(row),
            CodigoEstadoEmpleado = row.CodigoEstadoEmpleado,
            NombreEstadoEmpleado = row.NombreEstadoEmpleado,
            FechaIngreso = row.FechaIngreso,
            IdSupervisorEmpleado = row.IdSupervisorEmpleado,
            CodigoSupervisor = row.CodigoSupervisor,
            NombreSupervisor = row.NombreSupervisor,
            CargoSupervisor = row.CargoSupervisor,
            NumeroContrato = row.NumeroContrato,
            NombreTipoContrato = row.NombreTipoContrato,
            FechaFinContrato = row.FechaFinContrato,
            DirectReportCount = children.Count,
            TotalReportCount = children.Sum(item => 1 + item.TotalReportCount),
            Children = children,
        };
    }

    private static int CountTreeNodes(long idEmpleado, IReadOnlyDictionary<long, List<OrganizationEmployeeRow>> childrenLookup)
    {
        if (!childrenLookup.TryGetValue(idEmpleado, out var children) || children.Count == 0)
        {
            return 1;
        }

        return 1 + children.Sum(item => CountTreeNodes(item.IdEmpleado, childrenLookup));
    }

    private static string BuildBranchLabel(OrganizationEmployeeRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.NombreDepartamento))
        {
            return row.NombreDepartamento;
        }

        return row.NombreCargo;
    }

    private static string GetHierarchyCode(OrganizationEmployeeRow row)
    {
        var cargo = row.NombreCargo?.ToUpperInvariant() ?? string.Empty;

        if (row.NivelJerarquico >= 10 || cargo.Contains("GERENTE GENERAL"))
        {
            return "GERENCIA_GENERAL";
        }

        if (row.NivelJerarquico >= 9 || cargo.Contains("GERENTE"))
        {
            return "GERENCIA";
        }

        if (row.NivelJerarquico >= 8 || cargo.Contains("JEFE"))
        {
            return "JEFATURA";
        }

        if (row.NivelJerarquico >= 7 || cargo.Contains("COORDIN"))
        {
            return "COORDINACION";
        }

        return "SUBORDINADO";
    }

    private static string GetHierarchyLabel(OrganizationEmployeeRow row)
    {
        return GetHierarchyCode(row) switch
        {
            "GERENCIA_GENERAL" => "Gerente general",
            "GERENCIA" => "Gerencia",
            "JEFATURA" => "Jefatura",
            "COORDINACION" => "Coordinacion",
            _ => "Subordinado",
        };
    }

    private static Dictionary<string, ModuleMetricDto> BuildModules(
        SummaryDto summary,
        int totalActions,
        int actionsLast30,
        IReadOnlyDictionary<string, int> catalogCounts)
    {
        var modules = new Dictionary<string, ModuleMetricDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["empleado"] = new()
            {
                Value = summary.ActiveEmployees,
                Caption = "activos",
                Detail = $"{summary.InactiveEmployees} inactivos / {summary.EmployeesWithoutCurrentContract} sin contrato",
                Tone = summary.EmployeesWithoutCurrentContract > 0 ? "warning" : "success",
            },
            ["accion_personal"] = new()
            {
                Value = actionsLast30,
                Caption = "mov. 30 dias",
                Detail = $"{totalActions} registros acumulados",
                Tone = actionsLast30 > 0 ? "accent" : "neutral",
            },
            ["contrato"] = new()
            {
                Value = summary.CurrentContracts,
                Caption = "vigentes",
                Detail = $"{summary.ExpiringContracts} por vencer",
                Tone = summary.ExpiringContracts > 0 ? "warning" : "success",
            },
            ["estructura_empresa"] = new()
            {
                Value = summary.ActiveEmployees,
                Caption = "activos",
                Detail = "Organigrama por jefatura inmediata",
                Tone = "accent",
            },
            ["expediente_documento"] = new()
            {
                Value = summary.TotalDocuments,
                Caption = "expedientes",
                Detail = $"{summary.ExpiredDocuments} vencidos / {summary.DocumentsWithoutFile} sin archivo",
                Tone = summary.ExpiredDocuments > 0 ? "danger" : summary.ExpiringDocuments > 0 ? "warning" : "success",
            },
            ["reloj"] = new()
            {
                Value = summary.TodayClockMarks,
                Caption = "marcaciones hoy",
                Detail = $"{summary.OpenClockShiftsToday} jornadas abiertas",
                Tone = summary.OpenClockShiftsToday > 0 ? "warning" : "accent",
            },
            ["vacacion"] = new()
            {
                Value = summary.PendingVacations,
                Caption = "pendientes",
                Detail = "Vacaciones por revisar",
                Tone = summary.PendingVacations > 0 ? "warning" : "success",
            },
            ["hora_extra"] = new()
            {
                Value = summary.PendingOvertime,
                Caption = "pendientes",
                Detail = "Horas extra por aprobar",
                Tone = summary.PendingOvertime > 0 ? "warning" : "success",
            },
        };

        AddCatalogMetric(modules, "tipo_contrato", GetCount(catalogCounts, "tipo_contrato"), "tipos activos");
        AddCatalogMetric(modules, "estado_empleado", GetCount(catalogCounts, "estado_empleado"), "estados");
        AddCatalogMetric(modules, "departamento", GetCount(catalogCounts, "departamento"), "departamentos");
        AddCatalogMetric(modules, "cargo", GetCount(catalogCounts, "cargo"), "cargos activos");
        AddCatalogMetric(modules, "horario_laboral", GetCount(catalogCounts, "horario_laboral"), "horarios activos");
        AddCatalogMetric(modules, "banco", GetCount(catalogCounts, "banco"), "bancos");
        AddCatalogMetric(modules, "tipo_permiso", GetCount(catalogCounts, "tipo_permiso"), "tipos activos");
        AddCatalogMetric(modules, "tipo_hora_extra", GetCount(catalogCounts, "tipo_hora_extra"), "tipos activos");

        return modules;
    }

    private static List<AlertDto> BuildAlerts(SummaryDto summary)
    {
        var alerts = new List<AlertDto>();

        if (summary.EmployeesWithoutCurrentContract > 0)
        {
            alerts.Add(new AlertDto
            {
                ModuleId = "contrato",
                Tone = "warning",
                Title = $"{summary.EmployeesWithoutCurrentContract} empleado(s) sin contrato vigente",
                Detail = "Revisa las altas recientes o contratos vencidos pendientes de renovar.",
            });
        }

        if (summary.ExpiringContracts > 0)
        {
            alerts.Add(new AlertDto
            {
                ModuleId = "contrato",
                Tone = "warning",
                Title = $"{summary.ExpiringContracts} contrato(s) por vencer",
                Detail = "Requieren renovacion o cierre dentro de los proximos 30 dias.",
            });
        }

        if (summary.ExpiredDocuments > 0 || summary.ExpiringDocuments > 0)
        {
            alerts.Add(new AlertDto
            {
                ModuleId = "expediente_documento",
                Tone = summary.ExpiredDocuments > 0 ? "danger" : "warning",
                Title = $"{summary.ExpiredDocuments} vencidos / {summary.ExpiringDocuments} por vencer",
                Detail = "Hay expedientes documentales que necesitan atencion de RRHH.",
            });
        }

        if (summary.PendingApprovals > 0)
        {
            alerts.Add(new AlertDto
            {
                ModuleId = "vacacion",
                Tone = "warning",
                Title = $"{summary.PendingApprovals} novedad(es) pendientes",
                Detail = $"{summary.PendingVacations} vacaciones y {summary.PendingOvertime} horas extra en revision.",
            });
        }

        if (summary.OpenClockShiftsToday > 0)
        {
            alerts.Add(new AlertDto
            {
                ModuleId = "reloj",
                Tone = "warning",
                Title = $"{summary.OpenClockShiftsToday} jornada(s) abierta(s) hoy",
                Detail = "Hay marcaciones de entrada sin salida registrada.",
            });
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new AlertDto
            {
                ModuleId = "empleado",
                Tone = "success",
                Title = "Sin alertas operativas fuertes",
                Detail = "Los indicadores principales de RRHH estan al dia.",
            });
        }

        return alerts;
    }

    private static void AddCatalogMetric(
        IDictionary<string, ModuleMetricDto> modules,
        string moduleId,
        int value,
        string caption)
    {
        modules[moduleId] = new ModuleMetricDto
        {
            Value = value,
            Caption = caption,
            Detail = value > 0 ? "Configuracion disponible" : "Sin registros activos",
            Tone = value > 0 ? "neutral" : "warning",
        };
    }

    private static int GetCount(IReadOnlyDictionary<string, int> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : 0;
    }

    private static DateTime? ParseDate(string? value)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed.Date : null;
    }

    private static int GetInt32(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    public sealed class OverviewDto
    {
        public SummaryDto Summary { get; set; } = new();
        public Dictionary<string, ModuleMetricDto> Modules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<AlertDto> Alerts { get; set; } = [];
        public List<ActivityDto> RecentActivity { get; set; } = [];
    }

    public sealed class SummaryDto
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }
        public int EmployeesOnVacation { get; set; }
        public int SuspendedEmployees { get; set; }
        public int EmployeesWithoutCurrentContract { get; set; }
        public int TotalContracts { get; set; }
        public int CurrentContracts { get; set; }
        public int ExpiringContracts { get; set; }
        public int TotalDocuments { get; set; }
        public int DocumentsWithoutFile { get; set; }
        public int ExpiredDocuments { get; set; }
        public int ExpiringDocuments { get; set; }
        public int PendingPermissions { get; set; }
        public int PendingVacations { get; set; }
        public int PendingOvertime { get; set; }
        public int PendingApprovals { get; set; }
        public int TodayClockMarks { get; set; }
        public int EmployeesMarkedToday { get; set; }
        public int OpenClockShiftsToday { get; set; }
    }

    public sealed class ModuleMetricDto
    {
        public int Value { get; set; }
        public string Caption { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Tone { get; set; } = "neutral";
    }

    public sealed class AlertDto
    {
        public string ModuleId { get; set; } = string.Empty;
        public string Tone { get; set; } = "neutral";
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
    }

    public sealed class ActivityDto
    {
        public string Process { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string OccurredAt { get; set; } = string.Empty;
    }

    public sealed class OrganizationStructureDto
    {
        public OrganizationStructureSummaryDto Summary { get; set; } = new();
        public List<OrganizationBranchDto> Branches { get; set; } = [];
        public List<OrganizationNodeDto> Tree { get; set; } = [];
        public string? GeneralManagementName { get; set; }
    }

    public sealed class OrganizationStructureSummaryDto
    {
        public int TotalEmployees { get; set; }
        public int GeneralManagementCount { get; set; }
        public int ManagementCount { get; set; }
        public int HeadquartersCount { get; set; }
        public int CoordinationCount { get; set; }
        public int DirectReportCount { get; set; }
    }

    public sealed class OrganizationBranchDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string LeaderName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
    }

    public sealed class OrganizationNodeDto
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public long IdDepartamento { get; set; }
        public string CodigoDepartamento { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public long IdCargo { get; set; }
        public string CodigoCargo { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public int NivelJerarquico { get; set; }
        public string HierarchyCode { get; set; } = string.Empty;
        public string HierarchyLabel { get; set; } = string.Empty;
        public string CodigoEstadoEmpleado { get; set; } = string.Empty;
        public string NombreEstadoEmpleado { get; set; } = string.Empty;
        public string FechaIngreso { get; set; } = string.Empty;
        public long? IdSupervisorEmpleado { get; set; }
        public string? CodigoSupervisor { get; set; }
        public string? NombreSupervisor { get; set; }
        public string? CargoSupervisor { get; set; }
        public string? NumeroContrato { get; set; }
        public string? NombreTipoContrato { get; set; }
        public string? FechaFinContrato { get; set; }
        public int DirectReportCount { get; set; }
        public int TotalReportCount { get; set; }
        public List<OrganizationNodeDto> Children { get; set; } = [];
    }

    private sealed class OrganizationEmployeeRow
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public long IdDepartamento { get; set; }
        public string CodigoDepartamento { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public long IdCargo { get; set; }
        public string CodigoCargo { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public int NivelJerarquico { get; set; }
        public string CodigoEstadoEmpleado { get; set; } = string.Empty;
        public string NombreEstadoEmpleado { get; set; } = string.Empty;
        public string FechaIngreso { get; set; } = string.Empty;
        public long? IdSupervisorEmpleado { get; set; }
        public string? CodigoSupervisor { get; set; }
        public string? NombreSupervisor { get; set; }
        public string? CargoSupervisor { get; set; }
        public string? NumeroContrato { get; set; }
        public string? NombreTipoContrato { get; set; }
        public string? FechaFinContrato { get; set; }
    }

    public sealed class AuditLogResultDto
    {
        public List<ActivityDto> Rows { get; set; } = [];
        public List<string> Processes { get; set; } = [];
    }
}
