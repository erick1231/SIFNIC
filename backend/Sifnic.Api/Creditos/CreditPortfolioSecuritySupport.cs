using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Security;

namespace Sifnic.Api.Creditos;

public sealed class CreditPortfolioSession
{
    public long UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public List<string> Roles { get; } = [];

    public bool CanSeeFullPortfolio =>
        HasAnyRole("ADMINISTRADOR", "ADMINISTRACION", "JEFE_CREDITO", "GERENTE_CREDITO");

    public bool CanSeeAssignedPortfolio =>
        CanSeeFullPortfolio || HasAnyRole("OFICIAL_CREDITO", "CREDITO");

    public bool HasAnyRole(params string[] roles)
    {
        var current = new HashSet<string>(Roles.Select(role => role.ToUpperInvariant()));
        return roles.Any(role => current.Contains(role.ToUpperInvariant()));
    }
}

public static class CreditPortfolioSecuritySupport
{
    public static void EnsureSchema(SqlConnection connection)
    {
        const string sql = """
            IF SCHEMA_ID(N'creditos') IS NULL EXEC(N'CREATE SCHEMA creditos');

            IF OBJECT_ID(N'creditos.asignacion_oficial_credito', N'U') IS NULL
            BEGIN
                CREATE TABLE creditos.asignacion_oficial_credito
                (
                    id_asignacion_oficial_credito BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_creditos_asignacion_oficial_credito PRIMARY KEY,
                    id_credito BIGINT NOT NULL,
                    id_usuario_oficial BIGINT NOT NULL,
                    id_usuario_asigna BIGINT NULL,
                    fecha_asignacion DATETIME2(6) NOT NULL CONSTRAINT DF_creditos_asignacion_oficial_fecha DEFAULT (SYSDATETIME()),
                    fecha_fin DATETIME2(6) NULL,
                    motivo NVARCHAR(600) NULL,
                    observacion NVARCHAR(1000) NULL,
                    activo BIT NOT NULL CONSTRAINT DF_creditos_asignacion_oficial_activo DEFAULT (1),
                    fecha_registro DATETIME2(6) NOT NULL CONSTRAINT DF_creditos_asignacion_oficial_registro DEFAULT (SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'creditos.historial_asignacion_oficial_credito', N'U') IS NULL
            BEGIN
                CREATE TABLE creditos.historial_asignacion_oficial_credito
                (
                    id_historial_asignacion_oficial_credito BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_creditos_historial_asignacion_oficial PRIMARY KEY,
                    id_credito BIGINT NOT NULL,
                    id_usuario_oficial_anterior BIGINT NULL,
                    id_usuario_oficial_nuevo BIGINT NULL,
                    id_usuario_accion BIGINT NULL,
                    fecha_accion DATETIME2(6) NOT NULL CONSTRAINT DF_creditos_historial_asignacion_fecha DEFAULT (SYSDATETIME()),
                    tipo_accion NVARCHAR(50) NOT NULL CONSTRAINT DF_creditos_historial_asignacion_tipo DEFAULT (N'ASIGNACION'),
                    motivo NVARCHAR(600) NULL,
                    observacion NVARCHAR(1000) NULL
                );
            END;

            IF COL_LENGTH(N'creditos.historial_asignacion_oficial_credito', N'id_usuario_oficial_nuevo') IS NOT NULL
               AND EXISTS
               (
                   SELECT 1
                   FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'creditos.historial_asignacion_oficial_credito')
                     AND name = N'id_usuario_oficial_nuevo'
                     AND is_nullable = 0
               )
            BEGIN
                ALTER TABLE creditos.historial_asignacion_oficial_credito
                ALTER COLUMN id_usuario_oficial_nuevo BIGINT NULL;
            END;

            IF NOT EXISTS (SELECT 1 FROM seguridad.rol WHERE codigo_rol = N'GERENTE_CREDITO')
            BEGIN
                INSERT INTO seguridad.rol
                (
                    codigo_rol,
                    nombre_rol,
                    descripcion,
                    activo,
                    fecha_registro
                )
                VALUES
                (
                    N'GERENTE_CREDITO',
                    N'Gerente de credito',
                    N'Puede consultar y administrar toda la cartera de credito.',
                    1,
                    SYSDATETIME()
                );
            END;

            INSERT INTO creditos.asignacion_oficial_credito
            (
                id_credito,
                id_usuario_oficial,
                id_usuario_asigna,
                motivo,
                observacion
            )
            SELECT
                cr.id_credito,
                u.id_usuario,
                NULL,
                N'ASIGNACION_INICIAL',
                N'Asignacion generada desde promotor de solicitud aprobada.'
            FROM creditos.credito cr
            INNER JOIN creditos.solicitud_credito s
                ON s.id_solicitud_credito = cr.id_solicitud_credito
            INNER JOIN seguridad.usuario u
                ON u.activo = 1
               AND u.bloqueado = 0
               AND (
                    UPPER(LTRIM(RTRIM(u.usuario))) = UPPER(LTRIM(RTRIM(ISNULL(s.promotor_credito, N''))))
                    OR UPPER(LTRIM(RTRIM(CONCAT(u.nombres, N' ', u.apellidos)))) = UPPER(LTRIM(RTRIM(ISNULL(s.promotor_credito, N''))))
               )
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = u.id_usuario
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
               AND r.codigo_rol = N'OFICIAL_CREDITO'
            WHERE cr.activo = 1
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM creditos.asignacion_oficial_credito ao
                  WHERE ao.id_credito = cr.id_credito
                    AND ao.activo = 1
                    AND ao.fecha_fin IS NULL
              );
            """;

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
    }

    public static CreditPortfolioSession? ResolveSession(HttpRequest request, SqlConnection connection, bool touchSession = true)
    {
        var tokenText = request.Headers["X-Session-Token"].ToString().Trim();
        if (!Guid.TryParse(tokenText, out var token))
        {
            return null;
        }

        var expirationMinutes = GetExpirationMinutes(connection);
        const string sql = """
            SELECT
                s.id_usuario,
                s.fecha_inicio,
                s.fecha_ultimo_movimiento,
                u.usuario,
                u.nombres,
                u.apellidos,
                u.activo,
                u.bloqueado
            FROM seguridad.sesion_usuario s
            INNER JOIN seguridad.usuario u
                ON u.id_usuario = s.id_usuario
            WHERE s.token_sesion = @token_sesion
              AND s.activa = 1;

            SELECT r.codigo_rol
            FROM seguridad.sesion_usuario s
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = s.id_usuario
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
            WHERE s.token_sesion = @token_sesion
            ORDER BY r.codigo_rol;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = token;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var startDate = reader.GetDateTime(reader.GetOrdinal("fecha_inicio"));
        var lastActivity = reader.IsDBNull(reader.GetOrdinal("fecha_ultimo_movimiento"))
            ? startDate
            : reader.GetDateTime(reader.GetOrdinal("fecha_ultimo_movimiento"));
        if (lastActivity.AddMinutes(expirationMinutes) < DateTime.Now)
        {
            return null;
        }

        var active = reader.GetBoolean(reader.GetOrdinal("activo"));
        var blocked = reader.GetBoolean(reader.GetOrdinal("bloqueado"));
        if (!active || blocked)
        {
            return null;
        }

        var context = new CreditPortfolioSession
        {
            UserId = reader.GetInt64(reader.GetOrdinal("id_usuario")),
            Username = reader.GetString(reader.GetOrdinal("usuario")),
            DisplayName = SecuritySupport.BuildDisplayName(
                reader.GetString(reader.GetOrdinal("nombres")),
                reader.GetString(reader.GetOrdinal("apellidos"))),
        };

        reader.NextResult();
        while (reader.Read())
        {
            context.Roles.Add(reader.GetString(0));
        }

        reader.Close();

        if (touchSession)
        {
            using var update = new SqlCommand(
                """
                UPDATE seguridad.sesion_usuario
                SET fecha_ultimo_movimiento = SYSDATETIME()
                WHERE token_sesion = @token_sesion;
                """,
                connection);
            update.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = token;
            update.ExecuteNonQuery();
        }

        return context;
    }

    public static long? ResolveOfficerUserId(
        SqlConnection connection,
        SqlTransaction? transaction,
        string? promoter,
        HttpRequest request)
    {
        var normalizedPromoter = promoter?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedPromoter))
        {
            using var command = new SqlCommand(
                """
                SELECT TOP (1) u.id_usuario
                FROM seguridad.usuario u
                INNER JOIN seguridad.usuario_rol ur
                    ON ur.id_usuario = u.id_usuario
                   AND ur.activo = 1
                INNER JOIN seguridad.rol r
                    ON r.id_rol = ur.id_rol
                   AND r.activo = 1
                   AND r.codigo_rol = N'OFICIAL_CREDITO'
                WHERE u.activo = 1
                  AND u.bloqueado = 0
                  AND (
                      UPPER(LTRIM(RTRIM(u.usuario))) = UPPER(LTRIM(RTRIM(@promotor)))
                      OR UPPER(LTRIM(RTRIM(CONCAT(u.nombres, N' ', u.apellidos)))) = UPPER(LTRIM(RTRIM(@promotor)))
                  )
                ORDER BY CASE WHEN UPPER(LTRIM(RTRIM(u.usuario))) = UPPER(LTRIM(RTRIM(@promotor))) THEN 0 ELSE 1 END;
                """,
                connection,
                transaction);
            command.Parameters.Add("@promotor", SqlDbType.NVarChar, 200).Value = normalizedPromoter;
            var match = command.ExecuteScalar();
            if (match is not null && match != DBNull.Value)
            {
                return Convert.ToInt64(match);
            }
        }

        var tokenText = request.Headers["X-Session-Token"].ToString().Trim();
        if (!Guid.TryParse(tokenText, out var token))
        {
            return null;
        }

        using var currentCommand = new SqlCommand(
            """
            SELECT TOP (1) s.id_usuario
            FROM seguridad.sesion_usuario s
            INNER JOIN seguridad.usuario u
                ON u.id_usuario = s.id_usuario
               AND u.activo = 1
               AND u.bloqueado = 0
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = s.id_usuario
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
               AND r.codigo_rol = N'OFICIAL_CREDITO'
            WHERE s.token_sesion = @token_sesion
              AND s.activa = 1;
            """,
            connection,
            transaction);
        currentCommand.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = token;
        var current = currentCommand.ExecuteScalar();
        return current is null || current == DBNull.Value ? null : Convert.ToInt64(current);
    }

    public static long? ResolveCurrentUserId(SqlConnection connection, SqlTransaction? transaction, HttpRequest request)
    {
        var tokenText = request.Headers["X-Session-Token"].ToString().Trim();
        if (!Guid.TryParse(tokenText, out var token))
        {
            return null;
        }

        using var command = new SqlCommand(
            """
            SELECT TOP (1) id_usuario
            FROM seguridad.sesion_usuario
            WHERE token_sesion = @token_sesion
              AND activa = 1;
            """,
            connection,
            transaction);
        command.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = token;
        var value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static int GetExpirationMinutes(SqlConnection connection)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1) TRY_CONVERT(INT, valor_parametro)
            FROM seguridad.parametro_seguridad
            WHERE codigo_parametro = N'MINUTOS_EXPIRACION_SESION'
              AND activo = 1;
            """,
            connection);
        var value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? 30 : Math.Max(5, Convert.ToInt32(value));
    }
}
