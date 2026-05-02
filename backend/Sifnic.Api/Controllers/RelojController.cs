using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class RelojController : Controller
{
    [HttpGet]
    public IActionResult Estado(string? cedula)
    {
        var cleanedCedula = CleanCedula(cedula);
        if (string.IsNullOrWhiteSpace(cleanedCedula))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa el numero de cedula.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureClockSchema(connection);

            var employee = ObtenerEmpleadoPorCedula(connection, cleanedCedula);
            if (employee is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontro un empleado activo con esa cedula.",
                });
            }

            var todayMarks = ObtenerMarcacionesEmpleado(connection, employee.IdEmpleado, DateTime.Today.AddDays(-1), DateTime.Today);
            var lastMark = ObtenerUltimaMarcacion(connection, employee.IdEmpleado);
            var nextAction = DeterminarSiguienteAccion(lastMark);

            return Json(new
            {
                ok = true,
                data = new ClockStatusDto
                {
                    Employee = employee,
                    NextAction = nextAction,
                    CurrentStatus = nextAction == "SALIDA" ? "DENTRO" : "FUERA",
                    LastMark = lastMark,
                    TodayMarks = todayMarks,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo obtener el estado del reloj.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Marcar([FromBody] ClockMarkRequest model)
    {
        var cleanedCedula = CleanCedula(model.Cedula);
        var requestedAction = string.IsNullOrWhiteSpace(model.TipoMarcacion)
            ? null
            : model.TipoMarcacion.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(cleanedCedula))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa el numero de cedula.",
            });
        }

        if (requestedAction is not null && requestedAction is not ("ENTRADA" or "SALIDA"))
        {
            return BadRequest(new
            {
                ok = false,
                message = "La accion de marcacion no es valida.",
            });
        }

        if (!string.IsNullOrWhiteSpace(model.Observacion) && model.Observacion.Trim().Length > 300)
        {
            return BadRequest(new
            {
                ok = false,
                message = "La observacion supera el limite permitido.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureClockSchema(connection);
            using var transaction = connection.BeginTransaction();

            var employee = ObtenerEmpleadoPorCedula(connection, cleanedCedula, transaction);
            if (employee is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontro un empleado activo con esa cedula.",
                });
            }

            var lastMark = ObtenerUltimaMarcacion(connection, employee.IdEmpleado, transaction);
            var nextAction = DeterminarSiguienteAccion(lastMark);
            var actionToRegister = requestedAction ?? nextAction;
            var now = DateTime.Now;
            var fechaOperacion = actionToRegister == "SALIDA" && lastMark is not null
                ? DateTime.Parse(lastMark.FechaOperacion)
                : now.Date;

            if (!string.Equals(actionToRegister, nextAction, StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = $"La siguiente marcacion esperada es {nextAction.ToLowerInvariant()}.",
                });
            }

            long idMarcacion;
            using (var command = new SqlCommand(
                """
                INSERT INTO rrhh.marcacion_reloj
                (
                    id_empleado,
                    id_usuario,
                    fecha_operacion,
                    fecha_hora_marcacion,
                    tipo_marcacion,
                    origen,
                    observacion
                )
                OUTPUT INSERTED.id_marcacion_reloj
                VALUES
                (
                    @id_empleado,
                    NULL,
                    @fecha_operacion,
                    @fecha_hora_marcacion,
                    @tipo_marcacion,
                    N'RELOJ',
                    @observacion
                );
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = employee.IdEmpleado;
                command.Parameters.Add("@fecha_operacion", SqlDbType.Date).Value = fechaOperacion;
                command.Parameters.Add("@fecha_hora_marcacion", SqlDbType.DateTime2).Value = now;
                command.Parameters.Add("@tipo_marcacion", SqlDbType.NVarChar, 20).Value = actionToRegister;
                command.Parameters.Add("@observacion", SqlDbType.NVarChar, 300).Value =
                    RrhhSupport.ToDbValue(model.Observacion);
                idMarcacion = Convert.ToInt64(command.ExecuteScalar());
            }

            var inserted = ObtenerMarcacionPorId(connection, idMarcacion, transaction)!;
            var summary = ConstruirResumenMarcaciones(
                ObtenerMarcacionesRango(connection, fechaOperacion, fechaOperacion, employee.IdEmpleado, transaction));
            var daySummary = summary.FirstOrDefault();

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "RELOJ",
                actionToRegister == "ENTRADA" ? "MARCACION_ENTRADA" : "MARCACION_SALIDA",
                inserted.IdMarcacionReloj,
                $"{employee.CodigoEmpleado}-{inserted.TipoMarcacion}",
                actionToRegister == "ENTRADA"
                    ? $"El empleado {employee.CodigoEmpleado} marco entrada en reloj."
                    : $"El empleado {employee.CodigoEmpleado} marco salida en reloj.",
                new
                {
                    employee.IdEmpleado,
                    employee.CodigoEmpleado,
                    employee.NombreEmpleado,
                    inserted.TipoMarcacion,
                    inserted.FechaOperacion,
                    inserted.FechaHoraMarcacion,
                },
                employee.CodigoEmpleado);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = actionToRegister == "ENTRADA"
                    ? $"Entrada registrada para {employee.NombreEmpleado}."
                    : $"Salida registrada para {employee.NombreEmpleado}.",
                data = new
                {
                    employee,
                    mark = inserted,
                    nextAction = DeteminarAccionContraria(actionToRegister),
                    summary = daySummary,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo registrar la marcacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureClockSchema(connection);

            const string sql = """
                SELECT
                    e.id_empleado,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    d.nombre_departamento,
                    c.nombre_cargo
                FROM rrhh.empleado e
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                WHERE e.activo = 1
                ORDER BY nombre_empleado;
                """;

            using var command = new SqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            var employees = new List<object>();
            while (reader.Read())
            {
                employees.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                    department = reader.GetString(3),
                    position = reader.GetString(4),
                });
            }

            reader.Close();

            return Json(new
            {
                ok = true,
                data = new
                {
                    employees,
                    branding = ObtenerBrandingReporte(connection),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos del reloj.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Resumen(string? search, string? dateFrom, string? dateTo, long? idEmpleado = null)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureClockSchema(connection);

            var desde = ParseDateOrDefault(dateFrom, DateTime.Today.AddDays(-6));
            var hasta = ParseDateOrDefault(dateTo, DateTime.Today);
            if (hasta.Date < desde.Date)
            {
                (desde, hasta) = (hasta, desde);
            }

            var rows = ObtenerMarcacionesRango(connection, desde, hasta, idEmpleado, null, search);
            var summary = ConstruirResumenMarcaciones(rows);

            RrhhSupport.RegisterBitacora(
                connection,
                null,
                HttpContext,
                "RELOJ",
                "CONSULTA",
                0,
                "REPORTE_RELOJ",
                $"Se consulto el reporte de reloj desde {desde:yyyy-MM-dd} hasta {hasta:yyyy-MM-dd}.",
                new
                {
                    desde = desde.ToString("yyyy-MM-dd"),
                    hasta = hasta.ToString("yyyy-MM-dd"),
                    idEmpleado,
                    search = search?.Trim(),
                    total = summary.Count,
                });

            return Json(new
            {
                ok = true,
                data = new
                {
                    rows = summary,
                    branding = ObtenerBrandingReporte(connection),
                    filters = new
                    {
                        search = search?.Trim() ?? string.Empty,
                        dateFrom = desde.ToString("yyyy-MM-dd"),
                        dateTo = hasta.ToString("yyyy-MM-dd"),
                        idEmpleado,
                    },
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo generar el resumen del reloj.",
                detail = ex.Message,
            });
        }
    }

    private ClockEmployeeDto? ObtenerEmpleadoPorCedula(
        SqlConnection connection,
        string cedula,
        SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT TOP (1)
                e.id_empleado,
                e.codigo_empleado,
                e.cedula,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                ee.nombre_estado_empleado
            FROM rrhh.empleado e
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.cedula = @cedula
              AND e.activo = 1;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@cedula", SqlDbType.NVarChar, 20).Value = cedula;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ClockEmployeeDto
        {
            IdEmpleado = reader.GetInt64(0),
            CodigoEmpleado = reader.GetString(1),
            Cedula = reader.GetString(2),
            NombreEmpleado = reader.GetString(3),
            NombreDepartamento = reader.GetString(4),
            NombreCargo = reader.GetString(5),
            EstadoEmpleado = reader.GetString(6),
        };
    }

    private ClockMarkDto? ObtenerUltimaMarcacion(
        SqlConnection connection,
        long idEmpleado,
        SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT TOP (1)
                id_marcacion_reloj,
                id_empleado,
                fecha_operacion,
                fecha_hora_marcacion,
                tipo_marcacion,
                origen,
                observacion
            FROM rrhh.marcacion_reloj
            WHERE id_empleado = @id_empleado
            ORDER BY fecha_hora_marcacion DESC, id_marcacion_reloj DESC;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapearMarcacion(reader) : null;
    }

    private ClockMarkDto? ObtenerMarcacionPorId(
        SqlConnection connection,
        long idMarcacion,
        SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT
                id_marcacion_reloj,
                id_empleado,
                fecha_operacion,
                fecha_hora_marcacion,
                tipo_marcacion,
                origen,
                observacion
            FROM rrhh.marcacion_reloj
            WHERE id_marcacion_reloj = @id_marcacion;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_marcacion", SqlDbType.BigInt).Value = idMarcacion;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapearMarcacion(reader) : null;
    }

    private List<ClockMarkDto> ObtenerMarcacionesEmpleado(
        SqlConnection connection,
        long idEmpleado,
        DateTime desde,
        DateTime hasta)
    {
        return ObtenerMarcacionesRango(connection, desde, hasta, idEmpleado)
            .OrderBy(item => item.FechaHoraMarcacion)
            .Select(item => new ClockMarkDto
            {
                IdMarcacionReloj = item.IdMarcacionReloj,
                IdEmpleado = item.IdEmpleado,
                FechaOperacion = item.FechaOperacion,
                FechaHoraMarcacion = item.FechaHoraMarcacion,
                TipoMarcacion = item.TipoMarcacion,
                Origen = item.Origen,
                Observacion = item.Observacion,
            })
            .ToList();
    }

    private List<ClockMarkRowDto> ObtenerMarcacionesRango(
        SqlConnection connection,
        DateTime desde,
        DateTime hasta,
        long? idEmpleado = null,
        SqlTransaction? transaction = null,
        string? search = null)
    {
        const string sql = """
            SELECT
                m.id_marcacion_reloj,
                m.id_empleado,
                e.codigo_empleado,
                e.cedula,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                horario_actual.nombre_horario,
                horario_actual.horas_semanales,
                horario_actual.horas_diarias,
                m.fecha_operacion,
                m.fecha_hora_marcacion,
                m.tipo_marcacion,
                m.origen,
                m.observacion
            FROM rrhh.marcacion_reloj m
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = m.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            OUTER APPLY
            (
                SELECT TOP (1)
                    h.nombre_horario,
                    h.horas_semanales,
                    h.horas_diarias
                FROM rrhh.contrato ct
                INNER JOIN rrhh.horario_laboral h
                    ON h.id_horario_laboral = ct.id_horario_laboral
                WHERE ct.id_empleado = e.id_empleado
                  AND ct.es_contrato_vigente = 1
                ORDER BY ct.fecha_inicio DESC, ct.id_contrato DESC
            ) horario_actual
            WHERE m.fecha_operacion BETWEEN @desde AND @hasta
              AND (@id_empleado IS NULL OR m.id_empleado = @id_empleado)
              AND (
                    @search = N''
                    OR e.codigo_empleado LIKE N'%' + @search + N'%'
                    OR e.cedula LIKE N'%' + @search + N'%'
                    OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                  )
            ORDER BY nombre_empleado, m.fecha_operacion DESC, m.fecha_hora_marcacion ASC;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@desde", SqlDbType.Date).Value = desde.Date;
        command.Parameters.Add("@hasta", SqlDbType.Date).Value = hasta.Date;
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado.HasValue ? idEmpleado.Value : DBNull.Value;
        command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();

        using var reader = command.ExecuteReader();
        var rows = new List<ClockMarkRowDto>();
        while (reader.Read())
        {
            rows.Add(new ClockMarkRowDto
            {
                IdMarcacionReloj = reader.GetInt64(0),
                IdEmpleado = reader.GetInt64(1),
                CodigoEmpleado = reader.GetString(2),
                Cedula = reader.GetString(3),
                NombreEmpleado = reader.GetString(4),
                NombreDepartamento = reader.GetString(5),
                NombreCargo = reader.GetString(6),
                NombreHorario = reader.IsDBNull(7) ? null : reader.GetString(7),
                HorasSemanales = reader.IsDBNull(8) ? null : Convert.ToDecimal(reader.GetValue(8)),
                HorasDiarias = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetValue(9)),
                FechaOperacion = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
                FechaHoraMarcacion = reader.GetDateTime(11).ToString("yyyy-MM-dd HH:mm:ss"),
                TipoMarcacion = reader.GetString(12),
                Origen = reader.GetString(13),
                Observacion = reader.IsDBNull(14) ? null : reader.GetString(14),
            });
        }

        return rows;
    }

    private static List<ClockSummaryDto> ConstruirResumenMarcaciones(List<ClockMarkRowDto> rows)
    {
        var groups = rows
            .GroupBy(item => new { item.IdEmpleado, item.FechaOperacion })
            .OrderByDescending(group => group.Key.FechaOperacion)
            .ThenBy(group => group.First().NombreEmpleado);

        var result = new List<ClockSummaryDto>();

        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(item => DateTime.Parse(item.FechaHoraMarcacion))
                .ToList();
            var visibleMarks = ConsolidarMarcasVisibles(ordered);

            DateTime? openEntry = null;
            TimeSpan totalWorked = TimeSpan.Zero;

            foreach (var row in ordered)
            {
                var markTime = DateTime.Parse(row.FechaHoraMarcacion);

                if (row.TipoMarcacion == "ENTRADA")
                {
                    openEntry = markTime;
                    continue;
                }

                if (row.TipoMarcacion == "SALIDA" && openEntry.HasValue && markTime >= openEntry.Value)
                {
                    totalWorked += markTime - openEntry.Value;
                    openEntry = null;
                }
            }

            var firstEntry = ordered.FirstOrDefault(item => item.TipoMarcacion == "ENTRADA");
            var lastExit = ordered.LastOrDefault(item => item.TipoMarcacion == "SALIDA");
            var lastMark = ordered.LastOrDefault();
            var expectedDailyHours = ordered
                .Select(item => item.HorasDiarias)
                .FirstOrDefault(value => value.HasValue && value.Value > 0) ?? 8m;
            var expectedWeeklyHours = ordered
                .Select(item => item.HorasSemanales)
                .FirstOrDefault(value => value.HasValue && value.Value > 0);
            var workedHours = Math.Round(totalWorked.TotalHours, 2);
            var isClosed = !openEntry.HasValue && firstEntry is not null && lastExit is not null;

            result.Add(new ClockSummaryDto
            {
                IdEmpleado = group.Key.IdEmpleado,
                CodigoEmpleado = ordered[0].CodigoEmpleado,
                Cedula = ordered[0].Cedula,
                NombreEmpleado = ordered[0].NombreEmpleado,
                NombreDepartamento = ordered[0].NombreDepartamento,
                NombreCargo = ordered[0].NombreCargo,
                NombreHorario = ordered[0].NombreHorario,
                HorasSemanales = expectedWeeklyHours,
                HorasDiarias = expectedDailyHours,
                FechaOperacion = group.Key.FechaOperacion,
                HoraEntrada = firstEntry is null
                    ? null
                    : DateTime.Parse(firstEntry.FechaHoraMarcacion).ToString("HH:mm:ss"),
                HoraSalida = lastExit is null
                    ? null
                    : DateTime.Parse(lastExit.FechaHoraMarcacion).ToString("HH:mm:ss"),
                HorasTrabajadas = workedHours,
                HorasExtraMenos = isClosed ? Math.Round(workedHours - (double)expectedDailyHours, 2) : 0,
                EstadoJornada = openEntry.HasValue ? "ABIERTA" : "CERRADA",
                UltimaAccion = lastMark?.TipoMarcacion ?? "SIN_MARCAS",
                TotalMarcaciones = visibleMarks.Count,
                Marcas = visibleMarks,
            });
        }

        return result;
    }

    private static List<ClockMarkRowDto> ConsolidarMarcasVisibles(List<ClockMarkRowDto> ordered)
    {
        var result = new List<ClockMarkRowDto>();
        var firstEntry = ordered.FirstOrDefault(item =>
            string.Equals(item.TipoMarcacion, "ENTRADA", StringComparison.OrdinalIgnoreCase));
        var lastExit = ordered.LastOrDefault(item =>
            string.Equals(item.TipoMarcacion, "SALIDA", StringComparison.OrdinalIgnoreCase));

        if (firstEntry is not null)
        {
            result.Add(firstEntry);
        }

        if (lastExit is not null &&
            (firstEntry is null || lastExit.IdMarcacionReloj != firstEntry.IdMarcacionReloj))
        {
            result.Add(lastExit);
        }

        if (result.Count > 0)
        {
            return result;
        }

        var lastMark = ordered.LastOrDefault();
        if (lastMark is not null)
        {
            result.Add(lastMark);
        }

        return result;
    }

    private ReportBrandingDto ObtenerBrandingReporte(SqlConnection connection)
    {
        const string sql = """
            SELECT TOP (1)
                e.razon_social,
                e.nombre_comercial,
                e.ruc,
                e.telefono,
                e.correo,
                e.direccion,
                cg.logo_sidebar_url,
                cg.logo_login_url,
                cg.nombre_sistema,
                cg.texto_footer
            FROM empresa.empresa e
            LEFT JOIN empresa.configuracion_general cg
                ON cg.id_empresa = e.id_empresa
               AND cg.activo = 1
            WHERE e.activo = 1
            ORDER BY e.id_empresa;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return new ReportBrandingDto
            {
                CompanyName = "SISFNIC",
                LegalName = "Sistema Informacion Financiera Nicaragua, S.A.",
                LogoUrl = null,
                LogoPending = true,
                FooterText = "Logo pendiente de configuracion.",
            };
        }

        var logoSidebar = reader.IsDBNull(6) ? null : reader.GetString(6);
        var logoLogin = reader.IsDBNull(7) ? null : reader.GetString(7);
        var logoUrl = !string.IsNullOrWhiteSpace(logoSidebar) ? logoSidebar : logoLogin;

        return new ReportBrandingDto
        {
            CompanyName = reader.IsDBNull(1) ? "SISFNIC" : reader.GetString(1),
            LegalName = reader.IsDBNull(0) ? "Sistema Informacion Financiera Nicaragua, S.A." : reader.GetString(0),
            Ruc = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Phone = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            Address = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            LogoUrl = logoUrl,
            LogoPending = string.IsNullOrWhiteSpace(logoUrl),
            SystemName = reader.IsDBNull(8) ? "SISFNIC" : reader.GetString(8),
            FooterText = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
        };
    }

    private static ClockMarkDto MapearMarcacion(SqlDataReader reader) => new()
    {
        IdMarcacionReloj = reader.GetInt64(0),
        IdEmpleado = reader.GetInt64(1),
        FechaOperacion = reader.GetDateTime(2).ToString("yyyy-MM-dd"),
        FechaHoraMarcacion = reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss"),
        TipoMarcacion = reader.GetString(4),
        Origen = reader.GetString(5),
        Observacion = reader.IsDBNull(6) ? null : reader.GetString(6),
    };

    private static string DeterminarSiguienteAccion(ClockMarkDto? lastMark)
    {
        if (lastMark is null || string.Equals(lastMark.TipoMarcacion, "SALIDA", StringComparison.OrdinalIgnoreCase))
        {
            return "ENTRADA";
        }

        return "SALIDA";
    }

    private static string DeteminarAccionContraria(string action)
    {
        return string.Equals(action, "ENTRADA", StringComparison.OrdinalIgnoreCase)
            ? "SALIDA"
            : "ENTRADA";
    }

    private static DateTime ParseDateOrDefault(string? value, DateTime defaultValue)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed.Date : defaultValue.Date;
    }

    private static string CleanCedula(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    public sealed class ClockMarkRequest
    {
        public string Cedula { get; set; } = string.Empty;
        public string? TipoMarcacion { get; set; }
        public string? Observacion { get; set; }
    }

    public sealed class ClockEmployeeDto
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string EstadoEmpleado { get; set; } = string.Empty;
    }

    public sealed class ClockMarkDto
    {
        public long IdMarcacionReloj { get; set; }
        public long IdEmpleado { get; set; }
        public string FechaOperacion { get; set; } = string.Empty;
        public string FechaHoraMarcacion { get; set; } = string.Empty;
        public string TipoMarcacion { get; set; } = string.Empty;
        public string Origen { get; set; } = string.Empty;
        public string? Observacion { get; set; }
    }

    public sealed class ClockMarkRowDto
    {
        public long IdMarcacionReloj { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string? NombreHorario { get; set; }
        public decimal? HorasSemanales { get; set; }
        public decimal? HorasDiarias { get; set; }
        public string FechaOperacion { get; set; } = string.Empty;
        public string FechaHoraMarcacion { get; set; } = string.Empty;
        public string TipoMarcacion { get; set; } = string.Empty;
        public string Origen { get; set; } = string.Empty;
        public string? Observacion { get; set; }
    }

    public sealed class ClockSummaryDto
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string? NombreHorario { get; set; }
        public decimal? HorasSemanales { get; set; }
        public decimal? HorasDiarias { get; set; }
        public string FechaOperacion { get; set; } = string.Empty;
        public string? HoraEntrada { get; set; }
        public string? HoraSalida { get; set; }
        public double HorasTrabajadas { get; set; }
        public double HorasExtraMenos { get; set; }
        public string EstadoJornada { get; set; } = string.Empty;
        public string UltimaAccion { get; set; } = string.Empty;
        public int TotalMarcaciones { get; set; }
        public List<ClockMarkRowDto> Marcas { get; set; } = [];
    }

    public sealed class ClockStatusDto
    {
        public ClockEmployeeDto Employee { get; set; } = new();
        public string NextAction { get; set; } = "ENTRADA";
        public string CurrentStatus { get; set; } = "FUERA";
        public ClockMarkDto? LastMark { get; set; }
        public List<ClockMarkDto> TodayMarks { get; set; } = [];
    }

    public sealed class ReportBrandingDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string Ruc { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public bool LogoPending { get; set; }
        public string SystemName { get; set; } = string.Empty;
        public string FooterText { get; set; } = string.Empty;
    }
}
