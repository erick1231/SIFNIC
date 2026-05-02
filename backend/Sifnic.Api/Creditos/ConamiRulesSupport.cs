using System.Data;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Creditos;

public static class ConamiRulesSupport
{
    private const string RegulationPageUrl = "https://www.conami.gob.ni/fomento-y-regulacion/regulacion/";
    private const string CreditRiskNormUrl = "https://www.leybook.com/doc/31821";
    private const string PlaFtNormUrl = "https://www.conami.gob.ni/media/filer_public/32/3e/323e3da9-0eae-46e4-85d6-464f6bdf6f6e/norma_prevencion_lavado_activos_1.pdf";

    public static void EnsureSchema(SqlConnection connection)
    {
        const string sql = """
            IF SCHEMA_ID(N'regulatorio') IS NULL EXEC(N'CREATE SCHEMA regulatorio');

            IF OBJECT_ID(N'regulatorio.conami_norma', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.conami_norma
                (
                    id_norma BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_conami_norma PRIMARY KEY,
                    codigo_norma NVARCHAR(80) NOT NULL CONSTRAINT UQ_regulatorio_conami_norma_codigo UNIQUE,
                    nombre_norma NVARCHAR(250) NOT NULL,
                    categoria NVARCHAR(60) NOT NULL,
                    fuente_url NVARCHAR(1000) NULL,
                    vigente BIT NOT NULL CONSTRAINT DF_regulatorio_conami_norma_vigente DEFAULT (1),
                    fecha_vigencia DATE NULL,
                    descripcion NVARCHAR(1000) NULL,
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_regulatorio_conami_norma_creacion DEFAULT (SYSDATETIME()),
                    fecha_actualizacion DATETIME2 NULL
                );
            END;

            IF OBJECT_ID(N'regulatorio.conami_regla', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.conami_regla
                (
                    id_regla BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_conami_regla PRIMARY KEY,
                    codigo_regla NVARCHAR(100) NOT NULL CONSTRAINT UQ_regulatorio_conami_regla_codigo UNIQUE,
                    id_norma BIGINT NOT NULL,
                    modulo NVARCHAR(60) NOT NULL,
                    categoria NVARCHAR(60) NOT NULL,
                    nombre_regla NVARCHAR(200) NOT NULL,
                    descripcion NVARCHAR(1000) NOT NULL,
                    tipo_dato NVARCHAR(30) NOT NULL,
                    valor_texto NVARCHAR(500) NULL,
                    valor_decimal DECIMAL(18,6) NULL,
                    valor_entero INT NULL,
                    valor_booleano BIT NULL,
                    severidad NVARCHAR(20) NOT NULL CONSTRAINT DF_regulatorio_conami_regla_severidad DEFAULT (N'MEDIA'),
                    activo BIT NOT NULL CONSTRAINT DF_regulatorio_conami_regla_activo DEFAULT (1),
                    orden INT NOT NULL CONSTRAINT DF_regulatorio_conami_regla_orden DEFAULT (100),
                    editable BIT NOT NULL CONSTRAINT DF_regulatorio_conami_regla_editable DEFAULT (1),
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_regulatorio_conami_regla_creacion DEFAULT (SYSDATETIME()),
                    fecha_actualizacion DATETIME2 NULL,
                    usuario_actualizacion NVARCHAR(150) NULL,
                    CONSTRAINT FK_regulatorio_conami_regla_norma
                        FOREIGN KEY (id_norma) REFERENCES regulatorio.conami_norma(id_norma)
                );
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
    }

    public static void SeedDefaults(SqlConnection connection)
    {
        EnsureSchema(connection);

        var norms = new[]
        {
            new ConamiNormSeed("CONAMI-REG", "Biblioteca de regulacion CONAMI", "GENERAL", RegulationPageUrl, "Pagina oficial de normas y disposiciones CONAMI para microfinancieras."),
            new ConamiNormSeed("CONAMI-RIESGO-CREDITICIO", "Norma sobre gestion de riesgo crediticio", "RIESGO_CREDITICIO", CreditRiskNormUrl, "Base para evaluacion, clasificacion, manual de riesgo, garantias, seguimiento y provisiones."),
            new ConamiNormSeed("CONAMI-PLAFT", "Norma PLA/FT aplicable a IFIM supervisadas por CONAMI", "PLAFT", PlaFtNormUrl, "Base para debida diligencia, enfoque de riesgo, PEP, origen de fondos y monitoreo."),
        };

        foreach (var norm in norms)
        {
            InsertNormIfMissing(connection, norm);
        }

        var rules = new[]
        {
            new ConamiRuleSeed("CLIENTE_BASE_SCORE", "CONAMI-PLAFT", "CLIENTES", "riesgo-cliente", "Puntaje base de cliente", "Punto inicial del calculo automatico de riesgo del cliente.", "ENTERO", null, null, 30, null, "MEDIA", 10),
            new ConamiRuleSeed("CLIENTE_PEP_ALTO", "CONAMI-PLAFT", "CLIENTES", "riesgo-cliente", "PEP eleva riesgo", "Si el cliente es PEP, el sistema aplica mayor debida diligencia y eleva puntaje.", "BOOLEANO", null, null, null, true, "ALTA", 20),
            new ConamiRuleSeed("CLIENTE_PEP_SCORE_ADD", "CONAMI-PLAFT", "CLIENTES", "riesgo-cliente", "Puntos por PEP", "Puntos agregados al puntaje automatico cuando el cliente es PEP.", "ENTERO", null, null, 40, null, "ALTA", 30),
            new ConamiRuleSeed("CLIENTE_DATOS_MINIMOS_SCORE_ADD", "CONAMI-PLAFT", "CLIENTES", "riesgo-cliente", "Puntos por datos minimos pendientes", "Puntos agregados cuando faltan direccion, telefono o actividad economica.", "ENTERO", null, null, 15, null, "MEDIA", 40),
            new ConamiRuleSeed("CLIENTE_DDC_SCORE_ADD", "CONAMI-PLAFT", "CLIENTES", "riesgo-cliente", "Puntos por debida diligencia pendiente", "Puntos agregados cuando falta origen de fondos o proposito de la relacion.", "ENTERO", null, null, 10, null, "MEDIA", 50),
            new ConamiRuleSeed("CLIENTE_ENDEUDAMIENTO_SCORE_ADD", "CONAMI-RIESGO-CREDITICIO", "CLIENTES", "riesgo-cliente", "Puntos por presion de egresos", "Puntos agregados cuando los egresos exceden el umbral de ingresos configurado.", "ENTERO", null, null, 15, null, "MEDIA", 60),
            new ConamiRuleSeed("CLIENTE_NEGOCIO_NUEVO_SCORE_ADD", "CONAMI-RIESGO-CREDITICIO", "CLIENTES", "riesgo-cliente", "Puntos por negocio nuevo", "Puntos agregados cuando la antiguedad del negocio es menor al minimo observado.", "ENTERO", null, null, 10, null, "MEDIA", 70),
            new ConamiRuleSeed("CLIENTE_NEGOCIO_MIN_MESES_OBSERVADO", "CONAMI-RIESGO-CREDITICIO", "CLIENTES", "riesgo-cliente", "Antiguedad minima observada", "Meses minimos de antiguedad del negocio antes de quitar el factor de negocio nuevo.", "ENTERO", null, null, 6, null, "MEDIA", 80),
            new ConamiRuleSeed("CLIENTE_ENDEUDAMIENTO_MEDIO_PCT", "CONAMI-RIESGO-CREDITICIO", "CLIENTES", "riesgo-cliente", "Umbral egresos sobre ingresos", "Porcentaje de ingresos a partir del cual se agrega presion de egresos al riesgo.", "DECIMAL", null, 70m, null, null, "MEDIA", 90),
            new ConamiRuleSeed("CLIENTE_SCORE_MEDIO_MIN", "CONAMI-RIESGO-CREDITICIO", "CLIENTES", "riesgo-cliente", "Puntaje minimo riesgo medio", "Desde este puntaje el cliente queda en riesgo MEDIO.", "ENTERO", null, null, 45, null, "MEDIA", 100),
            new ConamiRuleSeed("CLIENTE_SCORE_ALTO_MIN", "CONAMI-RIESGO-CREDITICIO", "CLIENTES", "riesgo-cliente", "Puntaje minimo riesgo alto", "Desde este puntaje el cliente queda en riesgo ALTO.", "ENTERO", null, null, 70, null, "ALTA", 110),
            new ConamiRuleSeed("CLIENTE_DDC_ORIGEN_FONDOS", "CONAMI-PLAFT", "CLIENTES", "expediente", "Origen de fondos obligatorio", "El expediente debe documentar el origen de fondos para relacion comercial y credito.", "BOOLEANO", null, null, null, true, "ALTA", 120),
            new ConamiRuleSeed("CLIENTE_DDC_PROPOSITO_RELACION", "CONAMI-PLAFT", "CLIENTES", "expediente", "Proposito de relacion obligatorio", "El expediente debe documentar el proposito de la relacion comercial.", "BOOLEANO", null, null, null, true, "ALTA", 130),
            new ConamiRuleSeed("CLIENTE_EXPEDIENTE_DATOS_MINIMOS", "CONAMI-PLAFT", "CLIENTES", "expediente", "Datos minimos de expediente", "Campos minimos usados para considerar completo el expediente del cliente.", "TEXTO", "direccion,telefono,actividad_economica,origen_fondos,proposito_relacion", null, null, null, "MEDIA", 140),
            new ConamiRuleSeed("SOL_APROBACION_IDENTIFICACION", "CONAMI-PLAFT", "SOLICITUDES", "credito", "Identificacion validada", "No aprobar credito si la identificacion del cliente no fue validada.", "BOOLEANO", null, null, null, true, "ALTA", 210),
            new ConamiRuleSeed("SOL_APROBACION_EXPEDIENTE", "CONAMI-PLAFT", "SOLICITUDES", "credito", "Expediente completo", "No aprobar credito si el expediente documental no esta completo.", "BOOLEANO", null, null, null, true, "ALTA", 220),
            new ConamiRuleSeed("SOL_APROBACION_VISITA", "CONAMI-RIESGO-CREDITICIO", "SOLICITUDES", "credito", "Visita casa/negocio", "No aprobar credito si no se marco visita de casa o negocio cuando corresponde al expediente.", "BOOLEANO", null, null, null, true, "MEDIA", 230),
            new ConamiRuleSeed("SOL_APROBACION_CAPACIDAD", "CONAMI-RIESGO-CREDITICIO", "SOLICITUDES", "credito", "Capacidad de pago validada", "No aprobar credito si no se valido capacidad de pago.", "BOOLEANO", null, null, null, true, "ALTA", 240),
            new ConamiRuleSeed("SOL_APROBACION_REVISION_CONAMI", "CONAMI-RIESGO-CREDITICIO", "SOLICITUDES", "credito", "Revision CONAMI", "No aprobar credito si no se marco la revision normativa CONAMI del expediente.", "BOOLEANO", null, null, null, true, "ALTA", 250),
            new ConamiRuleSeed("SOL_COMITE_MONTO_MIN", "CONAMI-RIESGO-CREDITICIO", "SOLICITUDES", "credito", "Monto minimo a comite", "Monto solicitado desde el cual la solicitud se marca automaticamente para comite.", "DECIMAL", null, 50000m, null, null, "MEDIA", 260),
            new ConamiRuleSeed("SOL_COMITE_CUOTA_CAPACIDAD_PCT", "CONAMI-RIESGO-CREDITICIO", "SOLICITUDES", "credito", "Cuota sobre capacidad para comite", "Porcentaje de capacidad de pago desde el cual la cuota estimada envia a comite.", "DECIMAL", null, 50m, null, null, "MEDIA", 270),
            new ConamiRuleSeed("SOL_MONTO_CAPACIDAD_VECES_COMITE", "CONAMI-RIESGO-CREDITICIO", "SOLICITUDES", "credito", "Monto sobre capacidad para comite", "Veces de capacidad de pago mensual que marca comite durante precalculo en pantalla.", "DECIMAL", null, 2m, null, null, "MEDIA", 280),
            new ConamiRuleSeed("CARTERA_CLASIFICACIONES_CONAMI", "CONAMI-RIESGO-CREDITICIO", "CREDITOS", "mora", "Clasificaciones de cartera", "Catalogo operativo para clasificacion CONAMI de cartera.", "TEXTO", "A,B,C,D,E", null, null, null, "MEDIA", 310),
            new ConamiRuleSeed("REPORTE_CONAMI_PDF_EXCEL", "CONAMI-RIESGO-CREDITICIO", "REPORTES", "reportes", "Exportacion PDF y Excel", "Los reportes CONAMI deben poder generarse en PDF y Excel desde Solicitudes/Creditos.", "BOOLEANO", null, null, null, true, "MEDIA", 410),
            new ConamiRuleSeed("PLAN_PAGO_OBLIGATORIO", "CONAMI-RIESGO-CREDITICIO", "SOLICITUDES", "reportes", "Plan de pago obligatorio", "Toda solicitud evaluada debe generar plan de pago y expediente de credito.", "BOOLEANO", null, null, null, true, "ALTA", 420),
        };

        foreach (var rule in rules)
        {
            InsertRuleIfMissing(connection, rule);
        }
    }

    public static ConamiRulesConfigurationDto LoadConfiguration(SqlConnection connection)
    {
        SeedDefaults(connection);

        var norms = new List<ConamiNormDto>();
        using (var normsCommand = new SqlCommand(
            """
            SELECT id_norma, codigo_norma, nombre_norma, categoria, fuente_url, vigente, fecha_vigencia, descripcion
            FROM regulatorio.conami_norma
            ORDER BY categoria, nombre_norma;
            """,
            connection))
        using (var reader = normsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                norms.Add(new ConamiNormDto
                {
                    Id = reader.GetInt64(0),
                    Code = reader.GetString(1),
                    Name = reader.GetString(2),
                    Category = reader.GetString(3),
                    SourceUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Active = reader.GetBoolean(5),
                    EffectiveDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                });
            }
        }

        var rules = new List<ConamiRuleDto>();
        using (var rulesCommand = new SqlCommand(
            """
            SELECT
                r.id_regla,
                r.codigo_regla,
                r.modulo,
                r.categoria,
                r.nombre_regla,
                r.descripcion,
                r.tipo_dato,
                r.valor_texto,
                r.valor_decimal,
                r.valor_entero,
                r.valor_booleano,
                r.severidad,
                r.activo,
                r.orden,
                r.editable,
                r.fecha_actualizacion,
                r.usuario_actualizacion,
                n.codigo_norma,
                n.nombre_norma,
                n.fuente_url
            FROM regulatorio.conami_regla r
            INNER JOIN regulatorio.conami_norma n ON n.id_norma = r.id_norma
            ORDER BY r.categoria, r.orden, r.codigo_regla;
            """,
            connection))
        using (var reader = rulesCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                rules.Add(MapRule(reader));
            }
        }

        return new ConamiRulesConfigurationDto
        {
            Norms = norms,
            Rules = rules,
            Categories = rules.Select(rule => rule.Category).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        };
    }

    public static IReadOnlyDictionary<string, ConamiRuleValue> LoadActiveRuleMap(SqlConnection connection)
    {
        SeedDefaults(connection);

        using var command = new SqlCommand(
            """
            SELECT codigo_regla, tipo_dato, valor_texto, valor_decimal, valor_entero, valor_booleano
            FROM regulatorio.conami_regla
            WHERE activo = 1;
            """,
            connection);
        using var reader = command.ExecuteReader();
        var rules = new Dictionary<string, ConamiRuleValue>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            rules[reader.GetString(0)] = new ConamiRuleValue
            {
                Type = reader.GetString(1),
                TextValue = reader.IsDBNull(2) ? null : reader.GetString(2),
                DecimalValue = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                IntegerValue = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                BooleanValue = reader.IsDBNull(5) ? null : reader.GetBoolean(5),
            };
        }

        return rules;
    }

    public static void SaveRules(SqlConnection connection, IEnumerable<ConamiRuleSaveModel> rules, string? username)
    {
        SeedDefaults(connection);

        using var transaction = connection.BeginTransaction();
        foreach (var rule in rules)
        {
            var code = (rule.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var currentType = GetRuleType(connection, transaction, code);
            if (currentType is null)
            {
                continue;
            }

            var normalized = NormalizeValue(currentType, rule.Value);
            using var command = new SqlCommand(
                """
                UPDATE regulatorio.conami_regla
                SET
                    valor_texto = @valor_texto,
                    valor_decimal = @valor_decimal,
                    valor_entero = @valor_entero,
                    valor_booleano = @valor_booleano,
                    activo = @activo,
                    fecha_actualizacion = SYSDATETIME(),
                    usuario_actualizacion = @usuario
                WHERE codigo_regla = @codigo_regla
                  AND editable = 1;
                """,
                connection,
                transaction);
            command.Parameters.Add("@codigo_regla", SqlDbType.NVarChar, 100).Value = code;
            command.Parameters.Add("@valor_texto", SqlDbType.NVarChar, 500).Value = normalized.TextValue is null ? DBNull.Value : normalized.TextValue;
            command.Parameters.Add("@valor_decimal", SqlDbType.Decimal).Value = normalized.DecimalValue.HasValue ? normalized.DecimalValue.Value : DBNull.Value;
            command.Parameters["@valor_decimal"].Precision = 18;
            command.Parameters["@valor_decimal"].Scale = 6;
            command.Parameters.Add("@valor_entero", SqlDbType.Int).Value = normalized.IntegerValue.HasValue ? normalized.IntegerValue.Value : DBNull.Value;
            command.Parameters.Add("@valor_booleano", SqlDbType.Bit).Value = normalized.BooleanValue.HasValue ? normalized.BooleanValue.Value : DBNull.Value;
            command.Parameters.Add("@activo", SqlDbType.Bit).Value = rule.Active;
            command.Parameters.Add("@usuario", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(username) ? "sistema.local" : username.Trim();
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static int GetInt(IReadOnlyDictionary<string, ConamiRuleValue> rules, string code, int fallback)
        => rules.TryGetValue(code, out var rule) ? rule.AsInt(fallback) : fallback;

    public static decimal GetDecimal(IReadOnlyDictionary<string, ConamiRuleValue> rules, string code, decimal fallback)
        => rules.TryGetValue(code, out var rule) ? rule.AsDecimal(fallback) : fallback;

    public static bool GetBool(IReadOnlyDictionary<string, ConamiRuleValue> rules, string code, bool fallback)
        => rules.TryGetValue(code, out var rule) ? rule.AsBool(fallback) : fallback;

    public static string GetText(IReadOnlyDictionary<string, ConamiRuleValue> rules, string code, string fallback)
        => rules.TryGetValue(code, out var rule) ? rule.AsText(fallback) : fallback;

    public static string[] GetList(IReadOnlyDictionary<string, ConamiRuleValue> rules, string code, string[] fallback)
    {
        var text = GetText(rules, code, string.Join(",", fallback));
        var items = text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return items.Length > 0 ? items : fallback;
    }

    private static void InsertNormIfMissing(SqlConnection connection, ConamiNormSeed norm)
    {
        using var command = new SqlCommand(
            """
            IF NOT EXISTS (SELECT 1 FROM regulatorio.conami_norma WHERE codigo_norma = @codigo_norma)
            BEGIN
                INSERT INTO regulatorio.conami_norma
                (
                    codigo_norma, nombre_norma, categoria, fuente_url, vigente, descripcion
                )
                VALUES
                (
                    @codigo_norma, @nombre_norma, @categoria, @fuente_url, 1, @descripcion
                );
            END;
            ELSE
            BEGIN
                UPDATE regulatorio.conami_norma
                SET
                    nombre_norma = @nombre_norma,
                    categoria = @categoria,
                    fuente_url = @fuente_url,
                    descripcion = @descripcion,
                    vigente = 1,
                    fecha_actualizacion = SYSDATETIME()
                WHERE codigo_norma = @codigo_norma;
            END;
            """,
            connection);
        command.Parameters.Add("@codigo_norma", SqlDbType.NVarChar, 80).Value = norm.Code;
        command.Parameters.Add("@nombre_norma", SqlDbType.NVarChar, 250).Value = norm.Name;
        command.Parameters.Add("@categoria", SqlDbType.NVarChar, 60).Value = norm.Category;
        command.Parameters.Add("@fuente_url", SqlDbType.NVarChar, 1000).Value = norm.SourceUrl;
        command.Parameters.Add("@descripcion", SqlDbType.NVarChar, 1000).Value = norm.Description;
        command.ExecuteNonQuery();
    }

    private static void InsertRuleIfMissing(SqlConnection connection, ConamiRuleSeed rule)
    {
        using var command = new SqlCommand(
            """
            DECLARE @id_norma BIGINT =
            (
                SELECT TOP (1) id_norma
                FROM regulatorio.conami_norma
                WHERE codigo_norma = @codigo_norma
            );

            IF @id_norma IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM regulatorio.conami_regla WHERE codigo_regla = @codigo_regla)
            BEGIN
                INSERT INTO regulatorio.conami_regla
                (
                    codigo_regla, id_norma, modulo, categoria, nombre_regla, descripcion,
                    tipo_dato, valor_texto, valor_decimal, valor_entero, valor_booleano,
                    severidad, activo, orden, editable
                )
                VALUES
                (
                    @codigo_regla, @id_norma, @modulo, @categoria, @nombre_regla, @descripcion,
                    @tipo_dato, @valor_texto, @valor_decimal, @valor_entero, @valor_booleano,
                    @severidad, 1, @orden, 1
                );
            END;
            """,
            connection);
        command.Parameters.Add("@codigo_norma", SqlDbType.NVarChar, 80).Value = rule.NormCode;
        command.Parameters.Add("@codigo_regla", SqlDbType.NVarChar, 100).Value = rule.Code;
        command.Parameters.Add("@modulo", SqlDbType.NVarChar, 60).Value = rule.Module;
        command.Parameters.Add("@categoria", SqlDbType.NVarChar, 60).Value = rule.Category;
        command.Parameters.Add("@nombre_regla", SqlDbType.NVarChar, 200).Value = rule.Name;
        command.Parameters.Add("@descripcion", SqlDbType.NVarChar, 1000).Value = rule.Description;
        command.Parameters.Add("@tipo_dato", SqlDbType.NVarChar, 30).Value = rule.Type;
        command.Parameters.Add("@valor_texto", SqlDbType.NVarChar, 500).Value = rule.TextValue is null ? DBNull.Value : rule.TextValue;
        command.Parameters.Add("@valor_decimal", SqlDbType.Decimal).Value = rule.DecimalValue.HasValue ? rule.DecimalValue.Value : DBNull.Value;
        command.Parameters["@valor_decimal"].Precision = 18;
        command.Parameters["@valor_decimal"].Scale = 6;
        command.Parameters.Add("@valor_entero", SqlDbType.Int).Value = rule.IntegerValue.HasValue ? rule.IntegerValue.Value : DBNull.Value;
        command.Parameters.Add("@valor_booleano", SqlDbType.Bit).Value = rule.BooleanValue.HasValue ? rule.BooleanValue.Value : DBNull.Value;
        command.Parameters.Add("@severidad", SqlDbType.NVarChar, 20).Value = rule.Severity;
        command.Parameters.Add("@orden", SqlDbType.Int).Value = rule.Order;
        command.ExecuteNonQuery();
    }

    private static string? GetRuleType(SqlConnection connection, SqlTransaction transaction, string code)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1) tipo_dato
            FROM regulatorio.conami_regla
            WHERE codigo_regla = @codigo_regla
              AND editable = 1;
            """,
            connection,
            transaction);
        command.Parameters.Add("@codigo_regla", SqlDbType.NVarChar, 100).Value = code;
        return command.ExecuteScalar() as string;
    }

    private static ConamiRuleValue NormalizeValue(string type, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return type.ToUpperInvariant() switch
        {
            "BOOLEANO" => new ConamiRuleValue { Type = type, BooleanValue = text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("1", StringComparison.OrdinalIgnoreCase) || text.Equals("SI", StringComparison.OrdinalIgnoreCase) },
            "DECIMAL" => new ConamiRuleValue { Type = type, DecimalValue = decimal.TryParse(text, out var number) ? number : 0m },
            "ENTERO" => new ConamiRuleValue { Type = type, IntegerValue = int.TryParse(text, out var number) ? number : 0 },
            _ => new ConamiRuleValue { Type = type, TextValue = text },
        };
    }

    private static ConamiRuleDto MapRule(SqlDataReader reader)
    {
        var rule = new ConamiRuleDto
        {
            Id = reader.GetInt64(0),
            Code = reader.GetString(1),
            Module = reader.GetString(2),
            Category = reader.GetString(3),
            Name = reader.GetString(4),
            Description = reader.GetString(5),
            Type = reader.GetString(6),
            TextValue = reader.IsDBNull(7) ? null : reader.GetString(7),
            DecimalValue = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            IntegerValue = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            BooleanValue = reader.IsDBNull(10) ? null : reader.GetBoolean(10),
            Severity = reader.GetString(11),
            Active = reader.GetBoolean(12),
            Order = reader.GetInt32(13),
            Editable = reader.GetBoolean(14),
            UpdatedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
            UpdatedBy = reader.IsDBNull(16) ? null : reader.GetString(16),
            NormCode = reader.GetString(17),
            NormName = reader.GetString(18),
            SourceUrl = reader.IsDBNull(19) ? null : reader.GetString(19),
        };

        rule.Value = rule.Type.ToUpperInvariant() switch
        {
            "BOOLEANO" => rule.BooleanValue == true ? "true" : "false",
            "DECIMAL" => (rule.DecimalValue ?? 0m).ToString("0.######"),
            "ENTERO" => (rule.IntegerValue ?? 0).ToString(),
            _ => rule.TextValue ?? string.Empty,
        };

        return rule;
    }

    private sealed record ConamiNormSeed(string Code, string Name, string Category, string SourceUrl, string Description);

    private sealed record ConamiRuleSeed(
        string Code,
        string NormCode,
        string Module,
        string Category,
        string Name,
        string Description,
        string Type,
        string? TextValue,
        decimal? DecimalValue,
        int? IntegerValue,
        bool? BooleanValue,
        string Severity,
        int Order);
}

public sealed class ConamiRulesConfigurationDto
{
    public IReadOnlyList<ConamiNormDto> Norms { get; set; } = [];
    public IReadOnlyList<ConamiRuleDto> Rules { get; set; } = [];
    public IReadOnlyList<string> Categories { get; set; } = [];
}

public sealed class ConamiNormDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public bool Active { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? Description { get; set; }
}

public sealed class ConamiRuleDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? TextValue { get; set; }
    public decimal? DecimalValue { get; set; }
    public int? IntegerValue { get; set; }
    public bool? BooleanValue { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool Active { get; set; }
    public int Order { get; set; }
    public bool Editable { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string NormCode { get; set; } = string.Empty;
    public string NormName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
}

public sealed class ConamiRuleSaveModel
{
    public string? Code { get; set; }
    public string? Value { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class SaveConamiRulesRequest
{
    public List<ConamiRuleSaveModel> Rules { get; set; } = [];
}

public sealed class ConamiRuleValue
{
    public string Type { get; set; } = string.Empty;
    public string? TextValue { get; set; }
    public decimal? DecimalValue { get; set; }
    public int? IntegerValue { get; set; }
    public bool? BooleanValue { get; set; }

    public int AsInt(int fallback)
        => IntegerValue ?? (DecimalValue.HasValue ? (int)DecimalValue.Value : int.TryParse(TextValue, out var parsed) ? parsed : fallback);

    public decimal AsDecimal(decimal fallback)
        => DecimalValue ?? (IntegerValue.HasValue ? IntegerValue.Value : decimal.TryParse(TextValue, out var parsed) ? parsed : fallback);

    public bool AsBool(bool fallback)
        => BooleanValue ?? (bool.TryParse(TextValue, out var parsed) ? parsed : fallback);

    public string AsText(string fallback)
        => TextValue ?? DecimalValue?.ToString("0.######") ?? IntegerValue?.ToString() ?? BooleanValue?.ToString().ToLowerInvariant() ?? fallback;
}
