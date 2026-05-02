using System.Data;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Parameters;

public static class ExchangeRateSupport
{
    private static readonly string[] SupportedDateFormats =
    [
        "dd-MM-yyyy",
        "d-M-yyyy",
        "dd-MMMM-yy",
        "d-MMMM-yy",
        "dd-MMM-yy",
        "d-MMM-yy",
    ];

    private static readonly CultureInfo[] SupportedCultures =
    [
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("en-US"),
        CultureInfo.GetCultureInfo("es-NI"),
    ];

    public static ExchangeRateConfigurationDto LoadConfiguration(SqlConnection connection)
    {
        const string sql = """
            SELECT TOP (1)
                COALESCE(NULLIF(moneda_base, N''), N'NIO') AS moneda_base,
                razon_social,
                nombre_comercial
            FROM empresa.empresa
            ORDER BY id_empresa;

            SELECT TOP (1)
                fecha_tipo_cambio,
                moneda_origen,
                moneda_destino,
                valor_tipo_cambio,
                fuente,
                id_lote_importacion_tipo_cambio,
                cargado_manual,
                fecha_creacion
            FROM parametros.tipo_cambio_oficial
            WHERE moneda_origen = N'USD'
              AND moneda_destino = N'NIO'
            ORDER BY fecha_tipo_cambio DESC, id_tipo_cambio_oficial DESC;

            SELECT TOP (1)
                l.id_lote_importacion_tipo_cambio,
                l.tipo_fuente,
                l.nombre_archivo,
                l.fecha_importacion,
                l.usuario_importacion,
                l.estado_lote,
                l.observacion
            FROM parametros.lote_importacion_tipo_cambio l
            ORDER BY l.id_lote_importacion_tipo_cambio DESC;

            SELECT TOP (1)
                fecha_tipo_cambio,
                moneda_origen,
                moneda_destino,
                valor_compra,
                valor_venta,
                valor_referencia,
                observacion,
                usuario_registro,
                fecha_creacion
            FROM parametros.tipo_cambio_institucional
            WHERE moneda_origen = N'USD'
              AND moneda_destino = N'NIO'
            ORDER BY fecha_tipo_cambio DESC, id_tipo_cambio_institucional DESC;

            SELECT TOP (40)
                fecha_tipo_cambio,
                valor_tipo_cambio,
                fuente,
                nombre_archivo = COALESCE(l.nombre_archivo, N''),
                fecha_importacion = l.fecha_importacion
            FROM parametros.tipo_cambio_oficial o
            LEFT JOIN parametros.lote_importacion_tipo_cambio l
                ON l.id_lote_importacion_tipo_cambio = o.id_lote_importacion_tipo_cambio
            WHERE o.moneda_origen = N'USD'
              AND o.moneda_destino = N'NIO'
            ORDER BY o.fecha_tipo_cambio DESC, o.id_tipo_cambio_oficial DESC;

            SELECT TOP (40)
                fecha_tipo_cambio,
                valor_compra,
                valor_venta,
                valor_referencia,
                observacion,
                usuario_registro,
                fecha_creacion
            FROM parametros.tipo_cambio_institucional
            WHERE moneda_origen = N'USD'
              AND moneda_destino = N'NIO'
            ORDER BY fecha_tipo_cambio DESC, id_tipo_cambio_institucional DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var dto = new ExchangeRateConfigurationDto();

        if (reader.Read())
        {
            dto.MonedaBaseEmpresa = reader.IsDBNull(0) ? "NIO" : reader.GetString(0).Trim().ToUpperInvariant();
            dto.RazonSocial = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            dto.NombreComercial = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        }

        if (reader.NextResult() && reader.Read())
        {
            dto.OficialActual = new OfficialExchangeRateDto
            {
                FechaTipoCambio = reader.GetDateTime(0).ToString("yyyy-MM-dd"),
                MonedaOrigen = reader.GetString(1),
                MonedaDestino = reader.GetString(2),
                ValorTipoCambio = reader.GetDecimal(3),
                Fuente = reader.IsDBNull(4) ? "BCN" : reader.GetString(4),
                IdLoteImportacion = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                CargadoManual = !reader.IsDBNull(6) && reader.GetBoolean(6),
                FechaCreacion = reader.GetDateTime(7).ToString("yyyy-MM-ddTHH:mm:ss"),
            };
        }

        if (reader.NextResult() && reader.Read())
        {
            dto.UltimoLoteOficial = new ExchangeRateImportBatchDto
            {
                IdLoteImportacion = reader.GetInt64(0),
                TipoFuente = reader.GetString(1),
                NombreArchivo = reader.GetString(2),
                FechaImportacion = reader.GetDateTime(3).ToString("yyyy-MM-ddTHH:mm:ss"),
                UsuarioImportacion = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                EstadoLote = reader.GetString(5),
                Observacion = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            };
        }

        if (reader.NextResult() && reader.Read())
        {
            dto.InstitucionalActual = new InstitutionalExchangeRateDto
            {
                FechaTipoCambio = reader.GetDateTime(0).ToString("yyyy-MM-dd"),
                MonedaOrigen = reader.GetString(1),
                MonedaDestino = reader.GetString(2),
                ValorCompra = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                ValorVenta = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                ValorReferencia = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                Observacion = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                UsuarioRegistro = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                FechaCreacion = reader.GetDateTime(8).ToString("yyyy-MM-ddTHH:mm:ss"),
            };
        }

        if (reader.NextResult())
        {
            while (reader.Read())
            {
                dto.HistorialOficial.Add(new OfficialExchangeRateHistoryDto
                {
                    FechaTipoCambio = reader.GetDateTime(0).ToString("yyyy-MM-dd"),
                    ValorTipoCambio = reader.GetDecimal(1),
                    Fuente = reader.IsDBNull(2) ? "BCN" : reader.GetString(2),
                    NombreArchivo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    FechaImportacion = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy-MM-ddTHH:mm:ss"),
                });
            }
        }

        if (reader.NextResult())
        {
            while (reader.Read())
            {
                dto.HistorialInstitucional.Add(new InstitutionalExchangeRateHistoryDto
                {
                    FechaTipoCambio = reader.GetDateTime(0).ToString("yyyy-MM-dd"),
                    ValorCompra = reader.IsDBNull(1) ? null : reader.GetDecimal(1),
                    ValorVenta = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                    ValorReferencia = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                    Observacion = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    UsuarioRegistro = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    FechaCreacion = reader.IsDBNull(6) ? null : reader.GetDateTime(6).ToString("yyyy-MM-ddTHH:mm:ss"),
                });
            }
        }

        return dto;
    }

    public static List<ParsedOfficialExchangeRateRow> ParseOfficialRateFile(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new InvalidOperationException("El archivo de tipo de cambio esta vacio.");
        }

        if (!rawContent.Contains("<table", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La plantilla no tiene el formato esperado del BCN. Sube el archivo mensual o anual descargado del banco central.");
        }

        var rows = new Dictionary<DateOnly, decimal>();
        var rowMatches = Regex.Matches(rawContent, "<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match rowMatch in rowMatches)
        {
            var cellMatches = Regex.Matches(
                rowMatch.Groups[1].Value,
                "<t[dh][^>]*>(.*?)</t[dh]>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (cellMatches.Count < 2)
            {
                continue;
            }

            var dateText = ExtractPlainText(cellMatches[0].Groups[1].Value);
            var valueText = ExtractPlainText(cellMatches[1].Groups[1].Value);

            if (!TryParseExchangeDate(dateText, out var exchangeDate))
            {
                continue;
            }

            if (!TryParseExchangeValue(valueText, out var exchangeValue))
            {
                continue;
            }

            rows[DateOnly.FromDateTime(exchangeDate)] = exchangeValue;
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                "No se encontraron fechas y valores validos en la plantilla BCN.");
        }

        return rows
            .OrderBy(item => item.Key)
            .Select(item => new ParsedOfficialExchangeRateRow
            {
                FechaTipoCambio = item.Key.ToDateTime(TimeOnly.MinValue),
                ValorTipoCambio = item.Value,
            })
            .ToList();
    }

    public static long CreateImportBatch(
        SqlConnection connection,
        SqlTransaction transaction,
        string fileName,
        string usuarioImportacion)
    {
        const string sql = """
            INSERT INTO parametros.lote_importacion_tipo_cambio
            (
                tipo_fuente,
                nombre_archivo,
                fecha_importacion,
                usuario_importacion,
                estado_lote,
                observacion
            )
            VALUES
            (
                N'BCN',
                @nombre_archivo,
                SYSDATETIME(),
                @usuario_importacion,
                N'EN_PROCESO',
                N'Importacion iniciada desde configuracion del sistema.'
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@nombre_archivo", SqlDbType.NVarChar, 255).Value = fileName;
        command.Parameters.Add("@usuario_importacion", SqlDbType.NVarChar, 100).Value = usuarioImportacion;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static void CompleteImportBatch(
        SqlConnection connection,
        SqlTransaction transaction,
        long batchId,
        string status,
        string observation)
    {
        const string sql = """
            UPDATE parametros.lote_importacion_tipo_cambio
            SET
                estado_lote = @estado_lote,
                observacion = @observacion
            WHERE id_lote_importacion_tipo_cambio = @id_lote_importacion_tipo_cambio;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@estado_lote", SqlDbType.NVarChar, 40).Value = status;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = observation;
        command.Parameters.Add("@id_lote_importacion_tipo_cambio", SqlDbType.BigInt).Value = batchId;
        command.ExecuteNonQuery();
    }

    public static void RegisterOfficialRate(
        SqlConnection connection,
        SqlTransaction transaction,
        DateTime date,
        decimal value,
        long batchId)
    {
        using (var command = new SqlCommand("parametros.usp_registrar_tipo_cambio_oficial", connection, transaction))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@fecha_tipo_cambio", SqlDbType.Date).Value = date.Date;
            command.Parameters.Add("@moneda_origen", SqlDbType.NVarChar, 10).Value = "USD";
            command.Parameters.Add("@moneda_destino", SqlDbType.NVarChar, 10).Value = "NIO";
            command.Parameters.Add("@valor_tipo_cambio", SqlDbType.Decimal).Value = value;
            command.Parameters["@valor_tipo_cambio"].Precision = 18;
            command.Parameters["@valor_tipo_cambio"].Scale = 6;
            command.Parameters.Add("@fuente", SqlDbType.NVarChar, 50).Value = "BCN";
            command.ExecuteNonQuery();
        }

        using (var command = new SqlCommand(
            """
            UPDATE parametros.tipo_cambio_oficial
            SET
                id_lote_importacion_tipo_cambio = @id_lote_importacion_tipo_cambio,
                cargado_manual = 0,
                fuente = N'BCN'
            WHERE fecha_tipo_cambio = @fecha_tipo_cambio
              AND moneda_origen = N'USD'
              AND moneda_destino = N'NIO';
            """,
            connection,
            transaction))
        {
            command.Parameters.Add("@id_lote_importacion_tipo_cambio", SqlDbType.BigInt).Value = batchId;
            command.Parameters.Add("@fecha_tipo_cambio", SqlDbType.Date).Value = date.Date;
            command.ExecuteNonQuery();
        }

        UpsertUnifiedExchangeRate(
            connection,
            transaction,
            date.Date,
            "USD",
            "NIO",
            false,
            "Sincronizado desde importacion BCN.");
    }

    public static void RegisterInstitutionalRate(
        SqlConnection connection,
        SqlTransaction transaction,
        DateTime date,
        decimal? buyValue,
        decimal? saleValue,
        decimal? referenceValue,
        string? observation,
        string? usuarioRegistro)
    {
        using (var command = new SqlCommand("parametros.usp_registrar_tipo_cambio_institucional", connection, transaction))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@fecha_tipo_cambio", SqlDbType.Date).Value = date.Date;
            command.Parameters.Add("@moneda_origen", SqlDbType.NVarChar, 10).Value = "USD";
            command.Parameters.Add("@moneda_destino", SqlDbType.NVarChar, 10).Value = "NIO";
            command.Parameters.Add("@valor_compra", SqlDbType.Decimal).Value = buyValue.HasValue ? buyValue.Value : DBNull.Value;
            command.Parameters["@valor_compra"].Precision = 18;
            command.Parameters["@valor_compra"].Scale = 6;
            command.Parameters.Add("@valor_venta", SqlDbType.Decimal).Value = saleValue.HasValue ? saleValue.Value : DBNull.Value;
            command.Parameters["@valor_venta"].Precision = 18;
            command.Parameters["@valor_venta"].Scale = 6;
            command.Parameters.Add("@valor_referencia", SqlDbType.Decimal).Value = referenceValue.HasValue ? referenceValue.Value : DBNull.Value;
            command.Parameters["@valor_referencia"].Precision = 18;
            command.Parameters["@valor_referencia"].Scale = 6;
            command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value =
                string.IsNullOrWhiteSpace(observation) ? DBNull.Value : observation.Trim();
            command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value =
                string.IsNullOrWhiteSpace(usuarioRegistro) ? DBNull.Value : usuarioRegistro.Trim();
            command.ExecuteNonQuery();
        }

        UpsertUnifiedExchangeRate(
            connection,
            transaction,
            date.Date,
            "USD",
            "NIO",
            true,
            string.IsNullOrWhiteSpace(observation)
                ? "Tipo de cambio institucional actualizado desde configuracion."
                : observation.Trim());
    }

    public static void UpsertUnifiedExchangeRate(
        SqlConnection connection,
        SqlTransaction transaction,
        DateTime date,
        string sourceCurrency,
        string targetCurrency,
        bool loadedManually,
        string observation)
    {
        const string sql = """
            DECLARE @valor_oficial DECIMAL(18,6) =
            (
                SELECT TOP (1) valor_tipo_cambio
                FROM parametros.tipo_cambio_oficial
                WHERE fecha_tipo_cambio = @fecha_tipo_cambio
                  AND moneda_origen = @moneda_origen
                  AND moneda_destino = @moneda_destino
                ORDER BY id_tipo_cambio_oficial DESC
            );

            DECLARE @valor_institucional DECIMAL(18,6) =
            (
                SELECT TOP (1) COALESCE(valor_referencia, valor_venta, valor_compra)
                FROM parametros.tipo_cambio_institucional
                WHERE fecha_tipo_cambio = @fecha_tipo_cambio
                  AND moneda_origen = @moneda_origen
                  AND moneda_destino = @moneda_destino
                ORDER BY id_tipo_cambio_institucional DESC
            );

            IF @valor_oficial IS NULL AND @valor_institucional IS NULL
                RETURN;

            MERGE parametros.tipo_cambio AS destino
            USING
            (
                SELECT
                    @fecha_tipo_cambio AS fecha_tipo_cambio,
                    @moneda_origen AS moneda_origen,
                    @moneda_destino AS moneda_destino
            ) AS origen
            ON destino.fecha_tipo_cambio = origen.fecha_tipo_cambio
            AND destino.moneda_origen = origen.moneda_origen
            AND destino.moneda_destino = origen.moneda_destino
            WHEN MATCHED THEN
                UPDATE SET
                    tipo_cambio_oficial = COALESCE(@valor_oficial, destino.tipo_cambio_oficial),
                    tipo_cambio_institucion = @valor_institucional,
                    cargado_manual = @cargado_manual,
                    observacion = @observacion
            WHEN NOT MATCHED THEN
                INSERT
                (
                    fecha_tipo_cambio,
                    moneda_origen,
                    moneda_destino,
                    tipo_cambio_oficial,
                    tipo_cambio_institucion,
                    cargado_manual,
                    observacion,
                    fecha_creacion
                )
                VALUES
                (
                    @fecha_tipo_cambio,
                    @moneda_origen,
                    @moneda_destino,
                    COALESCE(@valor_oficial, 0),
                    @valor_institucional,
                    @cargado_manual,
                    @observacion,
                    SYSDATETIME()
                );
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@fecha_tipo_cambio", SqlDbType.Date).Value = date.Date;
        command.Parameters.Add("@moneda_origen", SqlDbType.NVarChar, 20).Value = sourceCurrency;
        command.Parameters.Add("@moneda_destino", SqlDbType.NVarChar, 20).Value = targetCurrency;
        command.Parameters.Add("@cargado_manual", SqlDbType.Bit).Value = loadedManually;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 600).Value = observation;
        command.ExecuteNonQuery();
    }

    private static string ExtractPlainText(string rawHtml)
    {
        var normalized = Regex.Replace(rawHtml ?? string.Empty, "<[^>]+>", " ");
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim();
    }

    private static bool TryParseExchangeDate(string rawValue, out DateTime date)
    {
        var match = Regex.Match(rawValue ?? string.Empty, @"\d{1,2}-\d{1,2}-\d{4}|\d{1,2}-[A-Za-z]+-\d{2}", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            date = default;
            return false;
        }

        var candidate = match.Value.Trim();
        foreach (var culture in SupportedCultures)
        {
            if (DateTime.TryParseExact(
                candidate,
                SupportedDateFormats,
                culture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
            {
                return true;
            }
        }

        date = default;
        return false;
    }

    private static bool TryParseExchangeValue(string rawValue, out decimal value)
    {
        var match = Regex.Match(rawValue ?? string.Empty, @"-?\d+(?:[.,]\d+)?");
        if (!match.Success)
        {
            value = default;
            return false;
        }

        var normalized = match.Value.Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}

public sealed class ExchangeRateConfigurationDto
{
    public string MonedaBaseEmpresa { get; set; } = "NIO";
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public OfficialExchangeRateDto? OficialActual { get; set; }
    public ExchangeRateImportBatchDto? UltimoLoteOficial { get; set; }
    public InstitutionalExchangeRateDto? InstitucionalActual { get; set; }
    public List<OfficialExchangeRateHistoryDto> HistorialOficial { get; } = [];
    public List<InstitutionalExchangeRateHistoryDto> HistorialInstitucional { get; } = [];
}

public sealed class OfficialExchangeRateDto
{
    public string FechaTipoCambio { get; set; } = string.Empty;
    public string MonedaOrigen { get; set; } = "USD";
    public string MonedaDestino { get; set; } = "NIO";
    public decimal ValorTipoCambio { get; set; }
    public string Fuente { get; set; } = "BCN";
    public long? IdLoteImportacion { get; set; }
    public bool CargadoManual { get; set; }
    public string FechaCreacion { get; set; } = string.Empty;
}

public sealed class ExchangeRateImportBatchDto
{
    public long IdLoteImportacion { get; set; }
    public string TipoFuente { get; set; } = "BCN";
    public string NombreArchivo { get; set; } = string.Empty;
    public string FechaImportacion { get; set; } = string.Empty;
    public string UsuarioImportacion { get; set; } = string.Empty;
    public string EstadoLote { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
}

public sealed class InstitutionalExchangeRateDto
{
    public string FechaTipoCambio { get; set; } = string.Empty;
    public string MonedaOrigen { get; set; } = "USD";
    public string MonedaDestino { get; set; } = "NIO";
    public decimal? ValorCompra { get; set; }
    public decimal? ValorVenta { get; set; }
    public decimal? ValorReferencia { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public string UsuarioRegistro { get; set; } = string.Empty;
    public string FechaCreacion { get; set; } = string.Empty;
}

public sealed class OfficialExchangeRateHistoryDto
{
    public string FechaTipoCambio { get; set; } = string.Empty;
    public decimal ValorTipoCambio { get; set; }
    public string Fuente { get; set; } = "BCN";
    public string NombreArchivo { get; set; } = string.Empty;
    public string? FechaImportacion { get; set; }
}

public sealed class InstitutionalExchangeRateHistoryDto
{
    public string FechaTipoCambio { get; set; } = string.Empty;
    public decimal? ValorCompra { get; set; }
    public decimal? ValorVenta { get; set; }
    public decimal? ValorReferencia { get; set; }
    public string Observacion { get; set; } = string.Empty;
    public string UsuarioRegistro { get; set; } = string.Empty;
    public string? FechaCreacion { get; set; }
}

public sealed class ParsedOfficialExchangeRateRow
{
    public DateTime FechaTipoCambio { get; set; }
    public decimal ValorTipoCambio { get; set; }
}
