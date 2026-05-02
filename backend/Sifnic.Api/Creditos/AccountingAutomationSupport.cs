using System.Data;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Creditos;

public static class AccountingAutomationSupport
{
    private const string CashNio = "11010101201";
    private const string CashUsd = "11010101301";
    private const string PortfolioNio = "14010101201";
    private const string PortfolioUsd = "14010101301";
    private const string InterestIncomeNio = "41060101201";
    private const string InterestIncomeUsd = "41060101301";
    private const string CommissionIncomeNio = "41060101211";
    private const string CommissionIncomeUsd = "41060101311";
    private const string MoraIncome = "41080401212";

    public static long RegisterCreditPaymentEntry(
        SqlConnection connection,
        SqlTransaction transaction,
        long paymentId,
        long creditId,
        string creditNumber,
        string clientCycleCode,
        string clientName,
        string currency,
        decimal exchangeRate,
        decimal receivedAmount,
        decimal capital,
        decimal interest,
        decimal commission,
        decimal mora,
        string username)
    {
        MicrofinanceCoreSupport.EnsureSchema(connection, transaction);
        if (ExistsEntry(connection, transaction, "CAJA", "PAGO_CREDITO", paymentId))
        {
            return 0;
        }

        var detail = new List<AccountLine>
        {
            new(AccountForCash(currency), "D", receivedAmount, $"Caja recibe pago {creditNumber}")
        };

        AddCredit(detail, AccountForPortfolio(currency), capital, $"Abono a capital {creditNumber}");
        AddCredit(detail, AccountForInterest(currency), interest, $"Ingreso por intereses {creditNumber}");
        AddCredit(detail, AccountForCommission(currency), commission, $"Ingreso por comision {creditNumber}");
        AddCredit(detail, MoraIncome, mora, $"Ingreso por mora {creditNumber}");

        return InsertBalancedEntry(
            connection,
            transaction,
            "PAGO_CREDITO",
            $"PAGO-{paymentId}",
            $"Cobro de credito {creditNumber} - {clientName}",
            "CAJA",
            "PAGO_CREDITO",
            paymentId,
            currency,
            exchangeRate,
            username,
            clientCycleCode,
            detail);
    }

    public static long RegisterCreditDisbursementEntry(
        SqlConnection connection,
        SqlTransaction transaction,
        long disbursementId,
        long creditId,
        string creditNumber,
        string clientCycleCode,
        string clientName,
        string currency,
        decimal exchangeRate,
        decimal creditAmount,
        decimal cashAmount,
        string username)
    {
        MicrofinanceCoreSupport.EnsureSchema(connection, transaction);
        if (ExistsEntry(connection, transaction, "CAJA", "DESEMBOLSO_CREDITO", disbursementId))
        {
            return 0;
        }

        var detail = new List<AccountLine>
        {
            new(AccountForPortfolio(currency), "D", creditAmount, $"Desembolso a cartera {creditNumber}"),
            new(AccountForCash(currency), "A", cashAmount, $"Salida de caja por desembolso {creditNumber}")
        };

        return InsertBalancedEntry(
            connection,
            transaction,
            "DESEMBOLSO_CREDITO",
            $"DESEMB-{disbursementId}",
            $"Desembolso de credito {creditNumber} - {clientName}",
            "CAJA",
            "DESEMBOLSO_CREDITO",
            disbursementId,
            currency,
            exchangeRate,
            username,
            clientCycleCode,
            detail);
    }

    public static long RegisterCreditPaymentVoidEntry(
        SqlConnection connection,
        SqlTransaction transaction,
        long paymentId,
        string voucherNumber,
        string creditNumber,
        string clientCycleCode,
        string currency,
        decimal exchangeRate,
        decimal amount,
        decimal capital,
        decimal interest,
        decimal commission,
        decimal mora,
        string username)
    {
        MicrofinanceCoreSupport.EnsureSchema(connection, transaction);
        if (ExistsEntry(connection, transaction, "CAJA", "ANULACION_PAGO_CREDITO", paymentId))
        {
            return 0;
        }

        var detail = new List<AccountLine>();
        AddDebit(detail, AccountForPortfolio(currency), capital, $"Reversa capital {creditNumber}");
        AddDebit(detail, AccountForInterest(currency), interest, $"Reversa interes {creditNumber}");
        AddDebit(detail, AccountForCommission(currency), commission, $"Reversa comision {creditNumber}");
        AddDebit(detail, MoraIncome, mora, $"Reversa mora {creditNumber}");
        detail.Add(new(AccountForCash(currency), "A", amount, $"Salida por anulacion {voucherNumber}"));

        return InsertBalancedEntry(
            connection,
            transaction,
            "ANULACION_PAGO_CREDITO",
            $"ANUL-{paymentId}",
            $"Anulacion de pago {voucherNumber} del credito {creditNumber}",
            "CAJA",
            "ANULACION_PAGO_CREDITO",
            paymentId,
            currency,
            exchangeRate,
            username,
            clientCycleCode,
            detail);
    }

    private static long InsertBalancedEntry(
        SqlConnection connection,
        SqlTransaction transaction,
        string entryType,
        string reference,
        string description,
        string originModule,
        string documentType,
        long documentId,
        string currency,
        decimal exchangeRate,
        string username,
        string clientCycleCode,
        List<AccountLine> lines)
    {
        lines.RemoveAll(line => line.Amount <= 0.005m);
        if (lines.Count < 2)
        {
            return 0;
        }

        var debits = lines.Where(line => line.Nature == "D").Sum(line => line.Amount);
        var credits = lines.Where(line => line.Nature == "A").Sum(line => line.Amount);
        var difference = Math.Round(debits - credits, 2);
        if (difference != 0)
        {
            var side = difference > 0 ? "A" : "D";
            lines.Add(new(AccountForCommission(currency), side, Math.Abs(difference), "Ajuste automatico por redondeo"));
        }

        using var header = new SqlCommand(
            """
            INSERT INTO contabilidad.asiento
            (
                id_lote_cierre,
                fecha_asiento,
                tipo_asiento,
                referencia,
                descripcion,
                estado_asiento,
                id_empresa,
                origen_modulo,
                tipo_documento_origen,
                id_documento_origen,
                codigo_moneda,
                tipo_cambio,
                usuario_registro
            )
            OUTPUT INSERTED.id_asiento
            VALUES
            (
                0,
                CONVERT(date, SYSDATETIME()),
                @tipo_asiento,
                @referencia,
                @descripcion,
                N'GENERADO',
                (SELECT TOP (1) id_empresa FROM empresa.empresa ORDER BY id_empresa),
                @origen_modulo,
                @tipo_documento_origen,
                @id_documento_origen,
                @codigo_moneda,
                @tipo_cambio,
                @usuario_registro
            );
            """,
            connection,
            transaction);
        header.Parameters.Add("@tipo_asiento", SqlDbType.NVarChar, 80).Value = entryType;
        header.Parameters.Add("@referencia", SqlDbType.NVarChar, 120).Value = reference;
        header.Parameters.Add("@descripcion", SqlDbType.NVarChar, 500).Value = description;
        header.Parameters.Add("@origen_modulo", SqlDbType.NVarChar, 80).Value = originModule;
        header.Parameters.Add("@tipo_documento_origen", SqlDbType.NVarChar, 80).Value = documentType;
        header.Parameters.Add("@id_documento_origen", SqlDbType.BigInt).Value = documentId;
        header.Parameters.Add("@codigo_moneda", SqlDbType.NVarChar, 10).Value = NormalizeCurrency(currency);
        header.Parameters.Add("@tipo_cambio", SqlDbType.Decimal).Value = exchangeRate <= 0 ? 1 : exchangeRate;
        header.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 120).Value = string.IsNullOrWhiteSpace(username) ? "sistema" : username.Trim();
        var entryId = Convert.ToInt64(header.ExecuteScalar());

        foreach (var line in lines)
        {
            using var detail = new SqlCommand(
                """
                INSERT INTO contabilidad.asiento_detalle
                (
                    id_asiento,
                    codigo_cuenta,
                    naturaleza,
                    monto,
                    cedula_id_cliente_ofic_ciclo,
                    descripcion_linea
                )
                VALUES
                (
                    @id_asiento,
                    @codigo_cuenta,
                    @naturaleza,
                    @monto,
                    @cedula_id_cliente_ofic_ciclo,
                    @descripcion_linea
                );
                """,
                connection,
                transaction);
            detail.Parameters.Add("@id_asiento", SqlDbType.BigInt).Value = entryId;
            detail.Parameters.Add("@codigo_cuenta", SqlDbType.NVarChar, 30).Value = line.Account;
            detail.Parameters.Add("@naturaleza", SqlDbType.Char, 1).Value = line.Nature;
            detail.Parameters.Add("@monto", SqlDbType.Decimal).Value = Math.Round(line.Amount, 2);
            detail.Parameters.Add("@cedula_id_cliente_ofic_ciclo", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(clientCycleCode) ? DBNull.Value : clientCycleCode;
            detail.Parameters.Add("@descripcion_linea", SqlDbType.NVarChar, 300).Value = line.Description;
            detail.ExecuteNonQuery();
        }

        return entryId;
    }

    private static bool ExistsEntry(SqlConnection connection, SqlTransaction transaction, string originModule, string documentType, long documentId)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM contabilidad.asiento
            WHERE origen_modulo = @origen_modulo
              AND tipo_documento_origen = @tipo_documento_origen
              AND id_documento_origen = @id_documento_origen
              AND anulado = 0;
            """,
            connection,
            transaction);
        command.Parameters.Add("@origen_modulo", SqlDbType.NVarChar, 80).Value = originModule;
        command.Parameters.Add("@tipo_documento_origen", SqlDbType.NVarChar, 80).Value = documentType;
        command.Parameters.Add("@id_documento_origen", SqlDbType.BigInt).Value = documentId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void AddCredit(List<AccountLine> lines, string account, decimal amount, string description)
    {
        if (amount > 0.005m)
        {
            lines.Add(new(account, "A", amount, description));
        }
    }

    private static void AddDebit(List<AccountLine> lines, string account, decimal amount, string description)
    {
        if (amount > 0.005m)
        {
            lines.Add(new(account, "D", amount, description));
        }
    }

    private static string AccountForCash(string currency)
    {
        return NormalizeCurrency(currency) == "USD" ? CashUsd : CashNio;
    }

    private static string AccountForPortfolio(string currency)
    {
        return NormalizeCurrency(currency) == "USD" ? PortfolioUsd : PortfolioNio;
    }

    private static string AccountForInterest(string currency)
    {
        return NormalizeCurrency(currency) == "USD" ? InterestIncomeUsd : InterestIncomeNio;
    }

    private static string AccountForCommission(string currency)
    {
        return NormalizeCurrency(currency) == "USD" ? CommissionIncomeUsd : CommissionIncomeNio;
    }

    private static string NormalizeCurrency(string? currency)
    {
        return string.Equals(currency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase) ? "USD" : "NIO";
    }

    private sealed record AccountLine(string Account, string Nature, decimal Amount, string Description);
}
