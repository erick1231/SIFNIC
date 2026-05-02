using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Security;

namespace Sifnic.Api.Rrhh;

public static class RrhhSupport
{
    private const decimal VacationDaysPerMonth = 2.5m;
    private static readonly HashSet<string> VacationAccrualContractCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIJO",
        "TEMPORAL",
        "INDETERMINADO",
        "INDETERMINADA",
    };

    public static string GetOperatorUser(HttpRequest request)
    {
        var usuario = request.Headers["X-Operator-User"].ToString().Trim();
        return string.IsNullOrWhiteSpace(usuario) ? "sistema.local" : usuario;
    }

    public static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    public static object ToDateDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : DateTime.Parse(value);
    }

    public static string TranslateSqlMessage(string message, string defaultMessage)
    {
        var texto = message.ToLowerInvariant();

        if (texto.Contains("datetime") || texto.Contains("fuera de intervalo") || texto.Contains("out-of-range"))
        {
            return "Hay una fecha fuera del rango permitido. Usa una fecha igual o mayor a 01/01/1753.";
        }

        if (texto.Contains("duplicate") || texto.Contains("unique") || texto.Contains("cannot insert duplicate"))
        {
            return "Ya existe un registro con esos datos.";
        }

        return defaultMessage;
    }

    public static AdminValidationResult ValidateAdministrator(
        SqlConnection connection,
        string username,
        string password)
    {
        const string sql = """
            SELECT TOP (1)
                u.usuario,
                u.hash_clave
            FROM seguridad.usuario u
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = u.id_usuario
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
            WHERE
                u.usuario = @usuario
                AND u.activo = 1
                AND u.bloqueado = 0
                AND r.codigo_rol = N'ADMINISTRADOR';
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = username.Trim();

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new AdminValidationResult
            {
                Ok = false,
                Message = "El usuario no tiene permisos de administrador.",
            };
        }

        var adminUser = reader.GetString(0);
        var hash = reader.GetString(1);
        reader.Close();

        if (!SecuritySupport.VerifyPassword(password, hash))
        {
            return new AdminValidationResult
            {
                Ok = false,
                Message = "La contrasena del administrador es incorrecta.",
            };
        }

        return new AdminValidationResult
        {
            Ok = true,
            UsuarioAdministrador = adminUser,
        };
    }

    public static void RegisterBitacora(
        SqlConnection connection,
        SqlTransaction? transaction,
        HttpContext httpContext,
        string proceso,
        string tipoEvento,
        long idReferencia,
        string referenciaTexto,
        string descripcion,
        object resumen,
        string? usuarioRegistro = null)
    {
        using var command = transaction is null
            ? new SqlCommand("operacion.usp_registrar_bitacora_operativa", connection)
            : new SqlCommand("operacion.usp_registrar_bitacora_operativa", connection, transaction);

        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@modulo", SqlDbType.NVarChar, 50).Value = "RRHH";
        command.Parameters.Add("@proceso", SqlDbType.NVarChar, 100).Value = proceso;
        command.Parameters.Add("@tipo_evento", SqlDbType.NVarChar, 50).Value = tipoEvento;
        command.Parameters.Add("@id_referencia", SqlDbType.BigInt).Value = idReferencia;
        command.Parameters.Add("@referencia_texto", SqlDbType.NVarChar, 100).Value = referenciaTexto;
        command.Parameters.Add("@descripcion_evento", SqlDbType.NVarChar, 1000).Value = descripcion;
        command.Parameters.Add("@datos_resumen", SqlDbType.NVarChar).Value = JsonSerializer.Serialize(resumen);
        command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value =
            usuarioRegistro ?? GetOperatorUser(httpContext.Request);
        command.Parameters.Add("@equipo", SqlDbType.NVarChar, 100).Value = Environment.MachineName;
        command.Parameters.Add("@ip_equipo", SqlDbType.NVarChar, 50).Value =
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "LOCAL";
        command.ExecuteNonQuery();
    }

    public static void EnsureClockSchema(SqlConnection connection)
    {
        const string sql = """
            IF OBJECT_ID(N'rrhh.marcacion_reloj', N'U') IS NULL
            BEGIN
                CREATE TABLE rrhh.marcacion_reloj
                (
                    id_marcacion_reloj BIGINT IDENTITY(1,1) NOT NULL
                        CONSTRAINT PK_rrhh_marcacion_reloj PRIMARY KEY,
                    id_empleado BIGINT NOT NULL,
                    id_usuario BIGINT NULL,
                    fecha_operacion DATE NOT NULL,
                    fecha_hora_marcacion DATETIME2 NOT NULL
                        CONSTRAINT DF_rrhh_marcacion_reloj_fecha_hora DEFAULT SYSDATETIME(),
                    tipo_marcacion NVARCHAR(20) NOT NULL,
                    origen NVARCHAR(50) NOT NULL
                        CONSTRAINT DF_rrhh_marcacion_reloj_origen DEFAULT N'WEB',
                    observacion NVARCHAR(300) NULL,
                    fecha_registro DATETIME2 NOT NULL
                        CONSTRAINT DF_rrhh_marcacion_reloj_fecha_registro DEFAULT SYSDATETIME(),
                    CONSTRAINT FK_rrhh_marcacion_reloj_empleado
                        FOREIGN KEY (id_empleado) REFERENCES rrhh.empleado(id_empleado),
                    CONSTRAINT FK_rrhh_marcacion_reloj_usuario
                        FOREIGN KEY (id_usuario) REFERENCES seguridad.usuario(id_usuario),
                    CONSTRAINT CK_rrhh_marcacion_reloj_tipo
                        CHECK (tipo_marcacion IN (N'ENTRADA', N'SALIDA'))
                );

                CREATE INDEX IX_rrhh_marcacion_reloj_empleado_fecha
                    ON rrhh.marcacion_reloj (id_empleado, fecha_operacion, fecha_hora_marcacion DESC);

                CREATE INDEX IX_rrhh_marcacion_reloj_usuario_fecha
                    ON rrhh.marcacion_reloj (id_usuario, fecha_operacion, fecha_hora_marcacion DESC);
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static void EnsureEmployeeSupervisorSchema(SqlConnection connection)
    {
        const string sql = """
            IF OBJECT_ID(N'rrhh.empleado_supervision', N'U') IS NULL
            BEGIN
                CREATE TABLE rrhh.empleado_supervision
                (
                    id_empleado_supervision BIGINT IDENTITY(1,1) NOT NULL
                        CONSTRAINT PK_rrhh_empleado_supervision PRIMARY KEY,
                    id_empleado BIGINT NOT NULL,
                    id_supervisor_empleado BIGINT NOT NULL,
                    fecha_asignacion DATE NOT NULL
                        CONSTRAINT DF_rrhh_empleado_supervision_fecha_asignacion DEFAULT CAST(GETDATE() AS DATE),
                    activo BIT NOT NULL
                        CONSTRAINT DF_rrhh_empleado_supervision_activo DEFAULT 1,
                    fecha_registro DATETIME2 NOT NULL
                        CONSTRAINT DF_rrhh_empleado_supervision_fecha_registro DEFAULT SYSDATETIME(),
                    fecha_actualizacion DATETIME2 NULL,
                    usuario_registro NVARCHAR(100) NULL,
                    usuario_actualizacion NVARCHAR(100) NULL,
                    CONSTRAINT FK_rrhh_empleado_supervision_empleado
                        FOREIGN KEY (id_empleado) REFERENCES rrhh.empleado(id_empleado),
                    CONSTRAINT FK_rrhh_empleado_supervision_supervisor
                        FOREIGN KEY (id_supervisor_empleado) REFERENCES rrhh.empleado(id_empleado),
                    CONSTRAINT CK_rrhh_empleado_supervision_distinto
                        CHECK (id_empleado <> id_supervisor_empleado)
                );

                CREATE UNIQUE INDEX UX_rrhh_empleado_supervision_activa
                    ON rrhh.empleado_supervision (id_empleado)
                    WHERE activo = 1;

                CREATE INDEX IX_rrhh_empleado_supervision_supervisor_activo
                    ON rrhh.empleado_supervision (id_supervisor_empleado, activo, id_empleado);
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static void EnsureEmployeeProfileSchema(SqlConnection connection)
    {
        const string sql = """
            IF COL_LENGTH(N'rrhh.empleado', N'foto_perfil_url') IS NULL
            BEGIN
                ALTER TABLE rrhh.empleado
                ADD foto_perfil_url NVARCHAR(1000) NULL;
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static VacationBalanceSnapshot CalculateVacationBalance(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado,
        DateTime? cutoffDate = null)
    {
        var cutoff = (cutoffDate ?? DateTime.Today).Date;
        var employment = GetEmployeeEmploymentSnapshot(connection, transaction, idEmpleado);
        var fechaIngreso = employment?.FechaIngreso;

        if (!fechaIngreso.HasValue)
        {
            return new VacationBalanceSnapshot
            {
                IdEmpleado = idEmpleado,
                FechaCorte = cutoff,
            };
        }

        var cutoffForAccrual = employment?.FechaBaja.HasValue == true && employment.FechaBaja.Value.Date < cutoff
            ? employment.FechaBaja.Value.Date
            : cutoff;
        var retiredAsOfCutoff = employment?.FechaBaja.HasValue == true && employment.FechaBaja.Value.Date <= cutoff;

        var contracts = LoadVacationContracts(connection, transaction, idEmpleado, cutoffForAccrual, fechaIngreso.Value.Date);
        var accruedDays = CalculateAccruedVacationDays(fechaIngreso.Value, cutoffForAccrual, contracts);
        var currentContract = contracts
            .Where(contract => contract.StartDate <= cutoffForAccrual && (!contract.EndDate.HasValue || contract.EndDate.Value >= cutoffForAccrual))
            .OrderByDescending(contract => contract.StartDate)
            .ThenByDescending(contract => contract.IdContrato)
            .FirstOrDefault();
        var hasEligibleHistory = contracts.Any(contract => contract.AccruesVacations);

        const string sql = """
            SELECT
                COALESCE((
                    SELECT SUM(COALESCE(v.dias_aprobados, v.dias_solicitados))
                    FROM rrhh.vacacion v
                    WHERE v.id_empleado = @id_empleado
                      AND v.estado_vacacion = N'APROBADA'
                      AND v.fecha_inicio <= @fecha_corte
                ), 0) AS vacaciones_aprobadas,
                COALESCE((
                    SELECT SUM(v.dias_solicitados)
                    FROM rrhh.vacacion v
                    WHERE v.id_empleado = @id_empleado
                      AND v.estado_vacacion = N'SOLICITADA'
                      AND v.fecha_inicio <= @fecha_corte
                ), 0) AS vacaciones_pendientes;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoffForAccrual;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new VacationBalanceSnapshot
            {
                IdEmpleado = idEmpleado,
                FechaIngreso = fechaIngreso.Value.Date,
                FechaCorte = cutoff,
                DiasAcumulados = accruedDays,
            };
        }

        var usedByVacations = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
        var pendingVacations = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
        var availableDays = RoundDays(accruedDays - usedByVacations);

        return new VacationBalanceSnapshot
        {
            IdEmpleado = idEmpleado,
            FechaIngreso = fechaIngreso.Value.Date,
            FechaCorte = cutoff,
            DiasAcumulados = accruedDays,
            DiasTomadosVacacion = usedByVacations,
            DiasDescontadosPermiso = 0m,
            DiasPendientesVacacion = pendingVacations,
            DiasPendientesPermiso = 0m,
            DiasDisponibles = availableDays,
            TieneContratoVigente = currentContract is not null,
            CodigoTipoContratoVigente = currentContract?.Code,
            NombreTipoContratoVigente = currentContract?.Name,
            AcumulaVacaciones = !retiredAsOfCutoff && (currentContract?.AccruesVacations ?? false),
            TieneHistorialElegible = hasEligibleHistory,
            MotivoNoAcumulacion = BuildNoAccrualReason(currentContract, hasEligibleHistory, employment, retiredAsOfCutoff),
        };
    }

    public static decimal CalculateAccruedVacationDays(DateTime joinDate, DateTime cutoffDate)
    {
        var start = joinDate.Date;
        var cutoff = cutoffDate.Date;

        if (cutoff <= start)
        {
            return 0m;
        }

        var fullMonths = ((cutoff.Year - start.Year) * 12) + cutoff.Month - start.Month;
        var currentAnchor = start.AddMonths(fullMonths);

        if (currentAnchor > cutoff)
        {
            fullMonths -= 1;
            currentAnchor = start.AddMonths(fullMonths);
        }

        var accrued = fullMonths * VacationDaysPerMonth;
        var nextAnchor = currentAnchor.AddMonths(1);
        var cycleDays = (decimal)(nextAnchor - currentAnchor).TotalDays;

        if (cycleDays > 0m && cutoff > currentAnchor)
        {
            var partialDays = (decimal)(cutoff - currentAnchor).TotalDays;
            accrued += (partialDays / cycleDays) * VacationDaysPerMonth;
        }

        return RoundDays(accrued);
    }

    public static decimal CalculateAccruedVacationDays(
        DateTime joinDate,
        DateTime cutoffDate,
        IReadOnlyList<VacationContractWindow> contracts)
    {
        var start = joinDate.Date;
        var cutoff = cutoffDate.Date;

        if (cutoff <= start || contracts.Count == 0)
        {
            return 0m;
        }

        var cycleStart = start;
        var accrued = 0m;

        while (cycleStart < cutoff)
        {
            var cycleEnd = cycleStart.AddMonths(1);
            var elapsedCycleEnd = cycleEnd < cutoff ? cycleEnd : cutoff;
            var cycleDays = (decimal)(cycleEnd - cycleStart).TotalDays;

            if (cycleDays <= 0m)
            {
                break;
            }

            var eligibleDays = GetEligibleDaysWithinWindow(contracts, cycleStart, elapsedCycleEnd);
            if (eligibleDays > 0m)
            {
                accrued += (eligibleDays / cycleDays) * VacationDaysPerMonth;
            }

            cycleStart = cycleEnd;
        }

        return RoundDays(accrued);
    }

    public static decimal RoundDays(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static string BuildVacationAvailabilityMessage(
        VacationBalanceSnapshot snapshot,
        string defaultPrefix = "El colaborador no tiene saldo suficiente de vacaciones.")
    {
        if (!snapshot.TieneHistorialElegible && !string.IsNullOrWhiteSpace(snapshot.MotivoNoAcumulacion))
        {
            return snapshot.MotivoNoAcumulacion!;
        }

        return $"{defaultPrefix} Disponible: {snapshot.DiasDisponibles:0.##} dia(s).";
    }

    private static EmployeeEmploymentSnapshot? GetEmployeeEmploymentSnapshot(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado)
    {
        const string sql = """
            SELECT TOP (1)
                e.fecha_ingreso,
                e.fecha_baja,
                ee.codigo_estado_empleado,
                ee.nombre_estado_empleado
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.id_empleado = @id_empleado;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
        {
            return null;
        }

        return new EmployeeEmploymentSnapshot
        {
            FechaIngreso = reader.GetDateTime(0).Date,
            FechaBaja = reader.IsDBNull(1) ? null : reader.GetDateTime(1).Date,
            CodigoEstado = reader.IsDBNull(2) ? null : reader.GetString(2),
            NombreEstado = reader.IsDBNull(3) ? null : reader.GetString(3),
        };
    }

    private static List<VacationContractWindow> LoadVacationContracts(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado,
        DateTime cutoffDate,
        DateTime joinDate)
    {
        const string sql = """
            SELECT
                c.id_contrato,
                c.fecha_inicio,
                c.fecha_fin,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato
            FROM rrhh.contrato c
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            WHERE c.id_empleado = @id_empleado
              AND c.fecha_inicio <= @fecha_corte
            ORDER BY c.fecha_inicio, c.id_contrato;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoffDate;

        using var reader = command.ExecuteReader();
        var items = new List<VacationContractWindow>();

        while (reader.Read())
        {
            var startDate = reader.GetDateTime(1).Date;
            var endDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2).Date;
            var code = reader.IsDBNull(3) ? null : reader.GetString(3);
            var name = reader.IsDBNull(4) ? null : reader.GetString(4);

            items.Add(new VacationContractWindow
            {
                IdContrato = reader.GetInt64(0),
                StartDate = startDate < joinDate ? joinDate : startDate,
                EndDate = endDate,
                Code = code,
                Name = name,
                AccruesVacations = ContractTypeAccruesVacations(code, name),
            });
        }

        return items;
    }

    private static decimal GetEligibleDaysWithinWindow(
        IReadOnlyList<VacationContractWindow> contracts,
        DateTime windowStart,
        DateTime windowEnd)
    {
        if (windowEnd <= windowStart)
        {
            return 0m;
        }

        var ranges = contracts
            .Where(contract => contract.AccruesVacations)
            .Select(contract => new
            {
                Start = contract.StartDate > windowStart ? contract.StartDate : windowStart,
                End = (contract.EndDate.HasValue && contract.EndDate.Value < windowEnd) ? contract.EndDate.Value : windowEnd,
            })
            .Where(range => range.End > range.Start)
            .OrderBy(range => range.Start)
            .ThenBy(range => range.End)
            .ToList();

        if (ranges.Count == 0)
        {
            return 0m;
        }

        var totalDays = 0m;
        var currentStart = ranges[0].Start;
        var currentEnd = ranges[0].End;

        for (var index = 1; index < ranges.Count; index += 1)
        {
            var range = ranges[index];
            if (range.Start <= currentEnd)
            {
                if (range.End > currentEnd)
                {
                    currentEnd = range.End;
                }

                continue;
            }

            totalDays += (decimal)(currentEnd - currentStart).TotalDays;
            currentStart = range.Start;
            currentEnd = range.End;
        }

        totalDays += (decimal)(currentEnd - currentStart).TotalDays;
        return totalDays;
    }

    private static bool ContractTypeAccruesVacations(string? code, string? name)
    {
        if (!string.IsNullOrWhiteSpace(code) && VacationAccrualContractCodes.Contains(code.Trim()))
        {
            return true;
        }

        var normalizedName = NormalizeText(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        if (normalizedName.Contains("SERVICIO") || normalizedName.Contains("PASANTIA"))
        {
            return false;
        }

        return normalizedName.Contains("FIJO")
            || normalizedName.Contains("TEMPORAL")
            || normalizedName.Contains("INDETERMINAD");
    }

    private static string? BuildNoAccrualReason(
        VacationContractWindow? currentContract,
        bool hasEligibleHistory,
        EmployeeEmploymentSnapshot? employment,
        bool retiredAsOfCutoff)
    {
        if (retiredAsOfCutoff && employment?.FechaBaja.HasValue == true)
        {
            return $"El colaborador fue retirado el {employment.FechaBaja.Value:dd/MM/yyyy}; desde esa fecha ya no acumula vacaciones.";
        }

        if (currentContract is not null && !currentContract.AccruesVacations && !string.IsNullOrWhiteSpace(currentContract.Name))
        {
            return $"El contrato vigente \"{currentContract.Name}\" no acumula vacaciones.";
        }

        if (!hasEligibleHistory)
        {
            return "El colaborador no tiene un contrato elegible para acumular vacaciones.";
        }

        return null;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static SupervisorAssignment? GetActiveSupervisor(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado)
    {
        const string sql = """
            SELECT TOP (1)
                rel.id_empleado_supervision,
                rel.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                rel.id_supervisor_empleado,
                s.codigo_empleado,
                COALESCE(NULLIF(s.nombre_completo, N''), CONCAT(s.nombres, N' ', s.apellidos)) AS nombre_supervisor,
                ds.nombre_departamento,
                cs.nombre_cargo,
                usuario_supervisor.usuario,
                rel.fecha_asignacion,
                rel.activo
            FROM rrhh.empleado_supervision rel
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = rel.id_empleado
            INNER JOIN rrhh.empleado s
                ON s.id_empleado = rel.id_supervisor_empleado
            LEFT JOIN rrhh.departamento ds
                ON ds.id_departamento = s.id_departamento
            LEFT JOIN rrhh.cargo cs
                ON cs.id_cargo = s.id_cargo
            OUTER APPLY
            (
                SELECT TOP (1) u.usuario
                FROM seguridad.usuario u
                WHERE u.activo = 1
                  AND
                  (
                    (
                        u.correo IS NOT NULL
                        AND LTRIM(RTRIM(u.correo)) <> N''
                        AND s.correo IS NOT NULL
                        AND LOWER(LTRIM(RTRIM(u.correo))) = LOWER(LTRIM(RTRIM(s.correo)))
                    )
                    OR
                    (
                        LOWER(LTRIM(RTRIM(u.nombres))) = LOWER(LTRIM(RTRIM(s.nombres)))
                        AND LOWER(LTRIM(RTRIM(u.apellidos))) = LOWER(LTRIM(RTRIM(s.apellidos)))
                    )
                  )
                ORDER BY u.id_usuario DESC
            ) usuario_supervisor
            WHERE rel.id_empleado = @id_empleado
              AND rel.activo = 1
            ORDER BY rel.fecha_asignacion DESC, rel.id_empleado_supervision DESC;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SupervisorAssignment
        {
            IdEmpleadoSupervision = reader.GetInt64(0),
            IdEmpleado = reader.GetInt64(1),
            CodigoEmpleado = reader.GetString(2),
            NombreEmpleado = reader.GetString(3),
            IdSupervisorEmpleado = reader.GetInt64(4),
            CodigoSupervisorEmpleado = reader.GetString(5),
            NombreSupervisorEmpleado = reader.GetString(6),
            NombreDepartamentoSupervisor = reader.IsDBNull(7) ? null : reader.GetString(7),
            NombreCargoSupervisor = reader.IsDBNull(8) ? null : reader.GetString(8),
            UsuarioSupervisor = reader.IsDBNull(9) ? null : reader.GetString(9),
            FechaAsignacion = reader.GetDateTime(10),
            Activo = reader.GetBoolean(11),
        };
    }

    public static List<SupervisorCandidate> ListSupervisorCandidates(
        SqlConnection connection,
        SqlTransaction? transaction,
        long? excludeEmployeeId = null)
    {
        const string sql = """
            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                usuario_relacionado.usuario
            FROM rrhh.empleado e
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            OUTER APPLY
            (
                SELECT TOP (1) u.usuario
                FROM seguridad.usuario u
                WHERE u.activo = 1
                  AND
                  (
                    (
                        u.correo IS NOT NULL
                        AND LTRIM(RTRIM(u.correo)) <> N''
                        AND e.correo IS NOT NULL
                        AND LOWER(LTRIM(RTRIM(u.correo))) = LOWER(LTRIM(RTRIM(e.correo)))
                    )
                    OR
                    (
                        LOWER(LTRIM(RTRIM(u.nombres))) = LOWER(LTRIM(RTRIM(e.nombres)))
                        AND LOWER(LTRIM(RTRIM(u.apellidos))) = LOWER(LTRIM(RTRIM(e.apellidos)))
                    )
                  )
                ORDER BY u.id_usuario DESC
            ) usuario_relacionado
            WHERE e.activo = 1
              AND (@exclude_id IS NULL OR e.id_empleado <> @exclude_id)
            ORDER BY nombre_empleado, e.codigo_empleado;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@exclude_id", SqlDbType.BigInt).Value =
            excludeEmployeeId.HasValue ? excludeEmployeeId.Value : DBNull.Value;

        using var reader = command.ExecuteReader();
        var items = new List<SupervisorCandidate>();
        while (reader.Read())
        {
            items.Add(new SupervisorCandidate
            {
                IdEmpleado = reader.GetInt64(0),
                CodigoEmpleado = reader.GetString(1),
                NombreEmpleado = reader.GetString(2),
                NombreDepartamento = reader.GetString(3),
                NombreCargo = reader.GetString(4),
                UsuarioSistema = reader.IsDBNull(5) ? null : reader.GetString(5),
            });
        }

        return items;
    }

    public static List<SupervisorCandidate> ListSubordinates(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idSupervisorEmpleado)
    {
        const string sql = """
            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                usuario_relacionado.usuario
            FROM rrhh.empleado_supervision rel
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = rel.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            OUTER APPLY
            (
                SELECT TOP (1) u.usuario
                FROM seguridad.usuario u
                WHERE u.activo = 1
                  AND
                  (
                    (
                        u.correo IS NOT NULL
                        AND LTRIM(RTRIM(u.correo)) <> N''
                        AND e.correo IS NOT NULL
                        AND LOWER(LTRIM(RTRIM(u.correo))) = LOWER(LTRIM(RTRIM(e.correo)))
                    )
                    OR
                    (
                        LOWER(LTRIM(RTRIM(u.nombres))) = LOWER(LTRIM(RTRIM(e.nombres)))
                        AND LOWER(LTRIM(RTRIM(u.apellidos))) = LOWER(LTRIM(RTRIM(e.apellidos)))
                    )
                  )
                ORDER BY u.id_usuario DESC
            ) usuario_relacionado
            WHERE rel.id_supervisor_empleado = @id_supervisor_empleado
              AND rel.activo = 1
              AND e.activo = 1
            ORDER BY nombre_empleado, e.codigo_empleado;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;

        using var reader = command.ExecuteReader();
        var items = new List<SupervisorCandidate>();
        while (reader.Read())
        {
            items.Add(new SupervisorCandidate
            {
                IdEmpleado = reader.GetInt64(0),
                CodigoEmpleado = reader.GetString(1),
                NombreEmpleado = reader.GetString(2),
                NombreDepartamento = reader.GetString(3),
                NombreCargo = reader.GetString(4),
                UsuarioSistema = reader.IsDBNull(5) ? null : reader.GetString(5),
            });
        }

        return items;
    }

    public static int CountSubordinates(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idSupervisorEmpleado)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM rrhh.empleado_supervision rel
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = rel.id_empleado
            WHERE rel.id_supervisor_empleado = @id_supervisor_empleado
              AND rel.activo = 1
              AND e.activo = 1;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public static bool WouldCreateSupervisorCycle(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado,
        long idSupervisorEmpleado)
    {
        if (idEmpleado <= 0 || idSupervisorEmpleado <= 0)
        {
            return false;
        }

        if (idEmpleado == idSupervisorEmpleado)
        {
            return true;
        }

        var currentSupervisorId = idSupervisorEmpleado;
        var visited = new HashSet<long>();

        while (currentSupervisorId > 0 && visited.Add(currentSupervisorId))
        {
            if (currentSupervisorId == idEmpleado)
            {
                return true;
            }

            var assignment = GetActiveSupervisor(connection, transaction, currentSupervisorId);
            if (assignment is null)
            {
                break;
            }

            currentSupervisorId = assignment.IdSupervisorEmpleado;
        }

        return false;
    }

    public static void ReplaceSupervisorAssignment(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado,
        long? idSupervisorEmpleado,
        string? usuarioRegistro = null)
    {
        var current = GetActiveSupervisor(connection, transaction, idEmpleado);
        if (current is not null && current.IdSupervisorEmpleado == idSupervisorEmpleado)
        {
            return;
        }

        if (current is not null)
        {
            using var closeCommand = transaction is null
                ? new SqlCommand(
                    """
                    UPDATE rrhh.empleado_supervision
                    SET
                        activo = 0,
                        fecha_actualizacion = SYSDATETIME(),
                        usuario_actualizacion = @usuario_actualizacion
                    WHERE id_empleado_supervision = @id_empleado_supervision;
                    """,
                    connection)
                : new SqlCommand(
                    """
                    UPDATE rrhh.empleado_supervision
                    SET
                        activo = 0,
                        fecha_actualizacion = SYSDATETIME(),
                        usuario_actualizacion = @usuario_actualizacion
                    WHERE id_empleado_supervision = @id_empleado_supervision;
                    """,
                    connection,
                    transaction);

            closeCommand.Parameters.Add("@usuario_actualizacion", SqlDbType.NVarChar, 100).Value =
                ToDbValue(usuarioRegistro);
            closeCommand.Parameters.Add("@id_empleado_supervision", SqlDbType.BigInt).Value =
                current.IdEmpleadoSupervision;
            closeCommand.ExecuteNonQuery();
        }

        if (!idSupervisorEmpleado.HasValue || idSupervisorEmpleado.Value <= 0)
        {
            return;
        }

        using var insertCommand = transaction is null
            ? new SqlCommand(
                """
                INSERT INTO rrhh.empleado_supervision
                (
                    id_empleado,
                    id_supervisor_empleado,
                    fecha_asignacion,
                    activo,
                    fecha_registro,
                    usuario_registro
                )
                VALUES
                (
                    @id_empleado,
                    @id_supervisor_empleado,
                    CAST(GETDATE() AS DATE),
                    1,
                    SYSDATETIME(),
                    @usuario_registro
                );
                """,
                connection)
            : new SqlCommand(
                """
                INSERT INTO rrhh.empleado_supervision
                (
                    id_empleado,
                    id_supervisor_empleado,
                    fecha_asignacion,
                    activo,
                    fecha_registro,
                    usuario_registro
                )
                VALUES
                (
                    @id_empleado,
                    @id_supervisor_empleado,
                    CAST(GETDATE() AS DATE),
                    1,
                    SYSDATETIME(),
                    @usuario_registro
                );
                """,
                connection,
                transaction);

        insertCommand.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        insertCommand.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado.Value;
        insertCommand.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value =
            ToDbValue(usuarioRegistro);
        insertCommand.ExecuteNonQuery();
    }

    public static EmployeeLink? FindEmployeeByUsername(SqlConnection connection, string username)
    {
        const string sql = """
            SELECT TOP (1)
                u.id_usuario,
                u.usuario,
                u.nombres,
                u.apellidos,
                u.correo,
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.id_estado_empleado,
                ee.codigo_estado_empleado,
                e.fecha_ingreso,
                d.nombre_departamento,
                c.nombre_cargo
            FROM seguridad.usuario u
            LEFT JOIN rrhh.empleado e
                ON (
                    u.correo IS NOT NULL
                    AND LTRIM(RTRIM(u.correo)) <> N''
                    AND e.correo IS NOT NULL
                    AND LOWER(LTRIM(RTRIM(u.correo))) = LOWER(LTRIM(RTRIM(e.correo)))
                )
                OR (
                    LOWER(LTRIM(RTRIM(u.nombres))) = LOWER(LTRIM(RTRIM(e.nombres)))
                    AND LOWER(LTRIM(RTRIM(u.apellidos))) = LOWER(LTRIM(RTRIM(e.apellidos)))
                )
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            LEFT JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            LEFT JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            WHERE u.usuario = @usuario
              AND u.activo = 1
            ORDER BY
                CASE
                    WHEN e.id_empleado IS NULL THEN 1
                    ELSE 0
                END,
                e.id_empleado DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = username.Trim();

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new EmployeeLink
        {
            IdUsuario = reader.GetInt64(0),
            Usuario = reader.GetString(1),
            NombresUsuario = reader.GetString(2),
            ApellidosUsuario = reader.GetString(3),
            CorreoUsuario = reader.IsDBNull(4) ? null : reader.GetString(4),
            IdEmpleado = reader.IsDBNull(5) ? null : reader.GetInt64(5),
            CodigoEmpleado = reader.IsDBNull(6) ? null : reader.GetString(6),
            NombreEmpleado = reader.IsDBNull(7) ? null : reader.GetString(7),
            IdEstadoEmpleado = reader.IsDBNull(8) ? null : reader.GetInt64(8),
            CodigoEstadoEmpleado = reader.IsDBNull(9) ? null : reader.GetString(9),
            FechaIngreso = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            NombreDepartamento = reader.IsDBNull(11) ? null : reader.GetString(11),
            NombreCargo = reader.IsDBNull(12) ? null : reader.GetString(12),
        };
    }

    public sealed class EmployeeLink
    {
        public long IdUsuario { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string NombresUsuario { get; set; } = string.Empty;
        public string ApellidosUsuario { get; set; } = string.Empty;
        public string? CorreoUsuario { get; set; }
        public long? IdEmpleado { get; set; }
        public string? CodigoEmpleado { get; set; }
        public string? NombreEmpleado { get; set; }
        public long? IdEstadoEmpleado { get; set; }
        public string? CodigoEstadoEmpleado { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public string? NombreDepartamento { get; set; }
        public string? NombreCargo { get; set; }
    }

    public sealed class SupervisorAssignment
    {
        public long IdEmpleadoSupervision { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public long IdSupervisorEmpleado { get; set; }
        public string CodigoSupervisorEmpleado { get; set; } = string.Empty;
        public string NombreSupervisorEmpleado { get; set; } = string.Empty;
        public string? NombreDepartamentoSupervisor { get; set; }
        public string? NombreCargoSupervisor { get; set; }
        public string? UsuarioSupervisor { get; set; }
        public DateTime FechaAsignacion { get; set; }
        public bool Activo { get; set; }
    }

    public sealed class SupervisorCandidate
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string? UsuarioSistema { get; set; }
    }

    public sealed class AdminValidationResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = string.Empty;
        public string UsuarioAdministrador { get; set; } = string.Empty;
    }

    public sealed class VacationBalanceSnapshot
    {
        public long IdEmpleado { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime FechaCorte { get; set; }
        public decimal DiasAcumulados { get; set; }
        public decimal DiasTomadosVacacion { get; set; }
        public decimal DiasDescontadosPermiso { get; set; }
        public decimal DiasPendientesVacacion { get; set; }
        public decimal DiasPendientesPermiso { get; set; }
        public decimal DiasDisponibles { get; set; }
        public bool TieneContratoVigente { get; set; }
        public string? CodigoTipoContratoVigente { get; set; }
        public string? NombreTipoContratoVigente { get; set; }
        public bool AcumulaVacaciones { get; set; }
        public bool TieneHistorialElegible { get; set; }
        public string? MotivoNoAcumulacion { get; set; }
    }

    private sealed class EmployeeEmploymentSnapshot
    {
        public DateTime FechaIngreso { get; set; }
        public DateTime? FechaBaja { get; set; }
        public string? CodigoEstado { get; set; }
        public string? NombreEstado { get; set; }
    }

    public sealed class VacationContractWindow
    {
        public long IdContrato { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public bool AccruesVacations { get; set; }
    }
}
