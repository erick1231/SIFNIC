using System.Data;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Security;

public static class ModuleAccessSupport
{
    private static readonly string[] AdministrativeRoles = ["ADMINISTRADOR", "ADMINISTRACION"];
    private static readonly AppModuleDefinition[] AppModules =
    [
        new("rrhh", "RRHH", "Recursos Humanos", "Gestion de personal, contratos y novedades", "Core"),
        new("nomina", "NOMINA", "Nomina", "Planilla, periodos, esquelas y obligaciones", "Core"),
        new("mi-portal", "MI_PORTAL", "Mi Portal", "Mi ficha, vacaciones y horas extra", "Portal"),
        new("bandeja-supervisor", "BANDEJA_SUPERVISOR", "Bandeja Supervisor", "Aprobaciones de tu equipo", "Portal"),
        new("configuracion", "CONFIGURACION", "Configuracion", "Usuarios, claves y bitacoras", "Administracion"),
        new("clientes", "CLIENTES", "Clientes", "Relacion comercial y prospectos", "Operativo"),
        new("creditos", "CREDITOS", "Creditos", "Colocacion, pagos y expedientes", "Operativo"),
        new("simulador-credito", "CREDITOS", "Simulador de Credito", "Cuota, plan y nivel de endeudamiento", "Operativo"),
        new("cobranza", "COBRANZA", "Cobranza", "Seguimiento y recuperacion", "Operativo"),
        new("caja", "CAJA", "Caja", "Sesiones, movimientos y arqueos", "Operativo"),
        new("bancos", "BANCOS", "Bancos", "Cuentas, movimientos y conciliacion", "Operativo"),
        new("contabilidad", "CONTABILIDAD", "Contabilidad", "Asientos y control contable", "Operativo"),
        new("cxc", "CXC", "Cuentas por Cobrar", "Cobros, anticipos y documentos", "Finanzas"),
        new("cxp", "CXP", "Cuentas por Pagar", "Pagos y obligaciones", "Finanzas"),
        new("inventario", "INVENTARIO", "Inventario", "Productos, categorias y bodegas", "Operativo"),
        new("captaciones", "CAPTACIONES", "Captaciones", "Ahorros, depositos y movimientos", "Operativo"),
        new("cumplimiento", "CUMPLIMIENTO", "Cumplimiento", "Monitoreo regulatorio y KYC", "Operativo"),
        new("regulatorio", "REGULATORIO", "Regulatorio", "Cierre, provision y clasificacion", "Operativo"),
    ];

    public static IReadOnlyList<AppModuleDefinition> GetAppModules() => AppModules;

    public static void EnsureUserModuleAccessSchema(SqlConnection connection)
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.schemas s
                INNER JOIN sys.tables t
                    ON t.schema_id = s.schema_id
                WHERE s.name = N'seguridad'
                  AND t.name = N'usuario_modulo'
            )
            BEGIN
                CREATE TABLE seguridad.usuario_modulo
                (
                    id_usuario_modulo BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    id_usuario BIGINT NOT NULL,
                    codigo_modulo NVARCHAR(100) NOT NULL,
                    activo BIT NOT NULL CONSTRAINT DF_seguridad_usuario_modulo_activo DEFAULT (1),
                    usuario_registro NVARCHAR(200) NULL,
                    fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_seguridad_usuario_modulo_fecha_registro DEFAULT (SYSDATETIME()),
                    fecha_actualizacion DATETIME2 NULL,
                    CONSTRAINT FK_seguridad_usuario_modulo_usuario
                        FOREIGN KEY (id_usuario) REFERENCES seguridad.usuario(id_usuario),
                    CONSTRAINT UQ_seguridad_usuario_modulo UNIQUE (id_usuario, codigo_modulo)
                );
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static bool HasCustomConfiguration(SqlConnection connection, long idUsuario)
    {
        EnsureUserModuleAccessSchema(connection);

        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM seguridad.usuario_modulo
            WHERE id_usuario = @id_usuario
              AND activo = 1;
            """,
            connection);
        command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public static HashSet<string> GetConfiguredModuleKeys(SqlConnection connection, long idUsuario)
    {
        EnsureUserModuleAccessSchema(connection);

        using var command = new SqlCommand(
            """
            SELECT codigo_modulo
            FROM seguridad.usuario_modulo
            WHERE id_usuario = @id_usuario
              AND activo = 1
            ORDER BY codigo_modulo;
            """,
            connection);
        command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;

        using var reader = command.ExecuteReader();
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(NormalizeModuleKey(reader.GetString(0)));
            }
        }

        return values;
    }

    public static IReadOnlyList<UserModuleAccessDto> BuildUserModuleAccess(
        SqlConnection connection,
        long idUsuario,
        IEnumerable<string> roleCodes,
        string username)
    {
        EnsureUserModuleAccessSchema(connection);

        var hasCustomConfiguration = HasCustomConfiguration(connection, idUsuario);
        var selectedKeys = hasCustomConfiguration
            ? GetConfiguredModuleKeys(connection, idUsuario)
            : BuildDefaultModuleKeys(connection, roleCodes, username);

        return AppModules
            .Select(module => new UserModuleAccessDto
            {
                Key = module.Key,
                Code = module.Code,
                Name = module.Name,
                Description = module.Description,
                Group = module.Group,
                Selected = selectedKeys.Contains(module.Key),
            })
            .ToArray();
    }

    public static HashSet<string> GetEffectiveModuleKeys(
        SqlConnection connection,
        long idUsuario,
        IEnumerable<string> roleCodes,
        string username)
    {
        EnsureUserModuleAccessSchema(connection);

        if (HasCustomConfiguration(connection, idUsuario))
        {
            return GetConfiguredModuleKeys(connection, idUsuario);
        }

        return BuildDefaultModuleKeys(connection, roleCodes, username);
    }

    public static void SaveUserModuleConfiguration(
        SqlConnection connection,
        long idUsuario,
        IEnumerable<string> moduleKeys,
        string username)
    {
        EnsureUserModuleAccessSchema(connection);

        var normalizedKeys = moduleKeys
            .Select(NormalizeModuleKey)
            .Where(key => AppModules.Any(module => module.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedKeys.Length == 0)
        {
            ClearUserModuleConfiguration(connection, idUsuario);
            return;
        }

        using var transaction = connection.BeginTransaction();

        using (var deactivateCommand = new SqlCommand(
            """
            UPDATE seguridad.usuario_modulo
            SET
                activo = 0,
                usuario_registro = @usuario_registro,
                fecha_actualizacion = SYSDATETIME()
            WHERE id_usuario = @id_usuario;
            """,
            connection,
            transaction))
        {
            deactivateCommand.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
            deactivateCommand.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 200).Value = username;
            deactivateCommand.ExecuteNonQuery();
        }

        foreach (var key in normalizedKeys)
        {
            using var command = new SqlCommand(
                """
                IF EXISTS (
                    SELECT 1
                    FROM seguridad.usuario_modulo
                    WHERE id_usuario = @id_usuario
                      AND codigo_modulo = @codigo_modulo
                )
                BEGIN
                    UPDATE seguridad.usuario_modulo
                    SET
                        activo = 1,
                        usuario_registro = @usuario_registro,
                        fecha_actualizacion = SYSDATETIME()
                    WHERE id_usuario = @id_usuario
                      AND codigo_modulo = @codigo_modulo;
                END
                ELSE
                BEGIN
                    INSERT INTO seguridad.usuario_modulo
                    (
                        id_usuario,
                        codigo_modulo,
                        activo,
                        usuario_registro,
                        fecha_registro
                    )
                    VALUES
                    (
                        @id_usuario,
                        @codigo_modulo,
                        1,
                        @usuario_registro,
                        SYSDATETIME()
                    );
                END;
                """,
                connection,
                transaction);

            command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
            command.Parameters.Add("@codigo_modulo", SqlDbType.NVarChar, 100).Value = key;
            command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 200).Value = username;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static void ClearUserModuleConfiguration(SqlConnection connection, long idUsuario)
    {
        EnsureUserModuleAccessSchema(connection);

        using var command = new SqlCommand(
            """
            DELETE FROM seguridad.usuario_modulo
            WHERE id_usuario = @id_usuario;
            """,
            connection);
        command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
        command.ExecuteNonQuery();
    }

    public static string NormalizeModuleKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static HashSet<string> BuildDefaultModuleKeys(
        SqlConnection connection,
        IEnumerable<string> roleCodes,
        string username)
    {
        var roles = roleCodes
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Overlaps(AdministrativeRoles))
        {
            return AppModules
                .Select(module => module.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var employee = RrhhSupport.FindEmployeeByUsername(connection, username);

        foreach (var module in AppModules)
        {
            if (roles.Contains(module.Code))
            {
                keys.Add(module.Key);
            }
        }

        if (employee?.IdEmpleado is not null)
        {
            keys.Add("mi-portal");

            if (RrhhSupport.CountSubordinates(connection, null, employee.IdEmpleado.Value) > 0)
            {
                keys.Add("bandeja-supervisor");
            }
        }

        if (roles.Contains("CREDITO") || roles.Contains("OFICIAL_CREDITO") || roles.Contains("JEFE_CREDITO"))
        {
            keys.Add("creditos");
        }

        if (roles.Contains("CAJERO"))
        {
            keys.Add("caja");
        }

        if (roles.Contains("INVENTARIO"))
        {
            keys.Add("inventario");
        }

        if (roles.Contains("PROVEEDORES") || roles.Contains("CXP"))
        {
            keys.Add("cxp");
        }

        if (roles.Contains("CXC"))
        {
            keys.Add("cxc");
        }

        if (roles.Contains("CUMPLIMIENTO"))
        {
            keys.Add("cumplimiento");
        }

        return keys;
    }

    public sealed record AppModuleDefinition(
        string Key,
        string Code,
        string Name,
        string Description,
        string Group);

    public sealed class UserModuleAccessDto
    {
        public string Key { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}
