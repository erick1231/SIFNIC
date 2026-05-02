using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Nomina;

namespace Sifnic.Api.Creditos;

public static class CreditReportingSupport
{
    private const decimal FallbackExchangeRate = 36.50m;

    public static ConamiPortfolioReportDto LoadConamiPortfolioReport(
        SqlConnection connection,
        DateTime cutoffDate,
        string? reportType)
    {
        var exchangeRate = LoadReferenceExchangeRate(connection, cutoffDate);
        var type = CreditOperationsSupport.NormalizeCode(reportType, "CARTERA");
        var report = new ConamiPortfolioReportDto
        {
            CutoffDate = cutoffDate.Date,
            GeneratedAt = DateTime.Now,
            ExchangeRate = exchangeRate,
            ReportType = type is "MORA" or "ICC" or "DESEMBOLSOS" ? type : "CARTERA",
        };

        const string sql = """
            SELECT
                c.id_cliente,
                c.cedula,
                c.nombres + N' ' + c.apellidos AS cliente,
                COALESCE(c.genero, N'') AS sexo,
                COALESCE(c.estado_civil, N'') AS estado_civil,
                COALESCE(NULLIF(c.celular, N''), c.telefono, N'') AS celular,
                COALESCE(NULLIF(c.actividad_economica, N''), c.ocupacion, N'') AS actividad,
                COALESCE(NULLIF(s.tipo_garantia, N''), cr.garantia, N'NINGUNA') AS tipo_garantia,
                cr.id_credito,
                COALESCE(NULLIF(cr.numero_credito, N''), cr.cedula_id_cliente_ofic_ciclo, N'') AS numero_credito,
                COALESCE(cr.monto_aprobado, cr.saldo_capital, 0) AS monto_aprobado,
                COALESCE(cr.tasa_interes_anual, s.tasa_interes_anual, 0) AS tasa_interes,
                COALESCE(cr.plazo_meses, s.plazo_meses, 0) AS plazo_meses,
                COALESCE(s.cuota_estimada, cuota.total_cuota, 0) AS cuota,
                COALESCE(cr.saldo_capital, 0) AS saldo_capital,
                COALESCE(cr.fecha_desembolso, cr.fecha_aprobacion, s.fecha_solicitud) AS fecha_desembolso,
                cr.fecha_vencimiento,
                COALESCE(cr.estado_operativo, N'') AS estado_credito,
                COALESCE(s.producto_credito, N'MICROCREDITO') AS producto_credito,
                COALESCE(s.destino_credito, N'') AS destino_credito,
                COALESCE(s.clasificacion_conami, N'A') AS clasificacion_origen,
                COALESCE(cr.moneda, s.moneda, N'NIO') AS moneda,
                COALESCE(c.sucursal, N'CENTRAL') AS oficina,
                COALESCE(s.id_solicitud_credito, 0) AS id_solicitud_credito,
                COALESCE(s.numero_solicitud, N'') AS numero_solicitud,
                COALESCE(c.tipo_cliente, N'INDIVIDUAL') AS tipo_cliente,
                COALESCE(s.valor_garantia, 0) AS valor_garantia,
                COALESCE(c.direccion, N'') AS direccion,
                COALESCE(c.geografia_casa, N'') AS zona,
                mora.fecha_primera_vencida,
                COALESCE(mora.cuotas_vencidas, 0) AS cuotas_vencidas,
                COALESCE(cuotas_plan.cuotas_total, s.plazo_meses, 0) AS cuotas_total
            FROM creditos.credito cr
            INNER JOIN clientes.cliente c ON c.id_cliente = cr.id_cliente
            LEFT JOIN creditos.solicitud_credito s ON s.id_solicitud_credito = cr.id_solicitud_credito
            OUTER APPLY
            (
                SELECT TOP (1)
                    pp.capital_programado + pp.interes_programado + pp.comision_programada + pp.deslizamiento_programado + pp.mora_programada AS total_cuota
                FROM creditos.plan_pago_credito pp
                WHERE pp.id_credito = cr.id_credito
                ORDER BY pp.numero_cuota
            ) cuota
            OUTER APPLY
            (
                SELECT
                    MIN(pp.fecha_cuota) AS fecha_primera_vencida,
                    COUNT(1) AS cuotas_vencidas
                FROM creditos.plan_pago_credito pp
                WHERE pp.id_credito = cr.id_credito
                  AND pp.estado_cuota <> N'PAGADA'
                  AND pp.fecha_cuota < @fecha_corte
            ) mora
            OUTER APPLY
            (
                SELECT COUNT(1) AS cuotas_total
                FROM creditos.plan_pago_credito pp
                WHERE pp.id_credito = cr.id_credito
            ) cuotas_plan
            WHERE COALESCE(cr.fecha_desembolso, cr.fecha_aprobacion, s.fecha_solicitud) <= @fecha_corte
              AND COALESCE(cr.saldo_capital, 0) > 0
            ORDER BY c.id_cliente, cr.id_credito;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoffDate.Date;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var amount = reader.GetDecimal(10);
            var balance = reader.GetDecimal(14);
            var firstOverdue = reader.IsDBNull(29) ? (DateTime?)null : reader.GetDateTime(29);
            var daysPastDue = firstOverdue.HasValue && balance > 0
                ? Math.Max(0, (cutoffDate.Date - firstOverdue.Value.Date).Days)
                : 0;
            var classification = BuildConamiClassification(daysPastDue);
            var provisionRate = BuildProvisionRate(daysPastDue);
            var usdAmount = exchangeRate > 0 ? amount / exchangeRate : amount;

            report.Rows.Add(new ConamiPortfolioRowDto
            {
                ClientId = reader.GetInt64(0),
                Identification = reader.GetString(1),
                ClientName = reader.GetString(2),
                Sex = reader.GetString(3),
                MaritalStatus = reader.GetString(4),
                Mobile = reader.GetString(5),
                EconomicActivity = reader.GetString(6),
                GuaranteeType = reader.GetString(7),
                CreditId = reader.GetInt64(8),
                CreditNumber = reader.GetString(9),
                ApprovedAmount = amount,
                AnnualRate = reader.GetDecimal(11),
                TermMonths = reader.GetInt32(12),
                Installment = reader.GetDecimal(13),
                PrincipalBalance = balance,
                DisbursementDate = reader.GetDateTime(15),
                DueDate = reader.GetDateTime(16),
                OperationalStatus = reader.GetString(17),
                Product = reader.GetString(18),
                Destination = reader.GetString(19),
                SourceClassification = reader.GetString(20),
                Currency = reader.GetString(21),
                Office = reader.GetString(22),
                RequestId = reader.GetInt64(23),
                RequestNumber = reader.GetString(24),
                ClientType = reader.GetString(25),
                GuaranteeValue = reader.GetDecimal(26),
                Address = reader.GetString(27),
                Zone = reader.GetString(28),
                FirstOverdueDate = firstOverdue,
                PastDueInstallments = reader.GetInt32(30),
                TotalInstallments = reader.GetInt32(31),
                DaysPastDue = daysPastDue,
                Situation = daysPastDue > 0 ? "VENCIDO" : "VIGENTE",
                PastDueRange = BuildPastDueRange(daysPastDue),
                DisbursementUsdRange = BuildUsdDisbursementRange(usdAmount),
                ConamiClassification = classification,
                ClassificationId = BuildConamiClassificationId(daysPastDue),
                ProvisionRate = provisionRate,
                ProvisionAmount = CreditOperationsSupport.SafeDecimal(balance * provisionRate),
            });
        }

        return report;
    }

    public static CreditFileDto? LoadCreditFile(SqlConnection connection, long requestId)
    {
        const string sql = """
            SELECT
                s.id_solicitud_credito,
                s.numero_solicitud,
                s.fecha_solicitud,
                s.estado_solicitud,
                s.producto_credito,
                s.moneda,
                s.monto_solicitado,
                s.plazo_meses,
                s.tasa_interes_anual,
                s.frecuencia_pago,
                s.tipo_cuota,
                s.cuota_estimada,
                s.destino_credito,
                s.ingresos_declarados,
                s.egresos_declarados,
                s.capacidad_pago,
                s.fuente_ingreso,
                s.actividad_financiada,
                s.tipo_garantia,
                s.descripcion_garantia,
                s.valor_garantia,
                s.nombre_fiador,
                s.cedula_fiador,
                s.telefono_fiador,
                s.requiere_comite,
                s.nivel_riesgo,
                s.clasificacion_conami,
                s.checklist_json,
                s.observacion,
                s.usuario_registro,
                s.fecha_creacion,
                s.usuario_resolucion,
                s.fecha_resolucion,
                c.id_cliente,
                c.cedula,
                c.nombres + N' ' + c.apellidos AS cliente,
                c.tipo_cliente,
                c.estado_cliente,
                COALESCE(c.telefono, N'') AS telefono,
                COALESCE(c.celular, N'') AS celular,
                COALESCE(c.correo, N'') AS correo,
                COALESCE(c.direccion, N'') AS direccion,
                COALESCE(c.geografia_casa, N'') AS geografia_casa,
                COALESCE(c.ocupacion, N'') AS ocupacion,
                COALESCE(c.actividad_economica, N'') AS actividad_economica,
                COALESCE(c.nombre_negocio, N'') AS nombre_negocio,
                COALESCE(c.direccion_negocio, N'') AS direccion_negocio,
                COALESCE(c.ingresos_mensuales + c.ingresos_conyuge + c.remesas + c.alquileres + c.otros_ingresos, 0) AS ingresos_cliente,
                COALESCE(c.egresos_mensuales, 0) AS egresos_cliente,
                c.nivel_riesgo,
                c.puntaje_riesgo,
                c.estado_expediente,
                COALESCE(c.origen_fondos, N'') AS origen_fondos,
                COALESCE(c.proposito_relacion, N'') AS proposito_relacion,
                c.pep,
                cr.id_credito,
                COALESCE(NULLIF(cr.numero_credito, N''), cr.cedula_id_cliente_ofic_ciclo, N'') AS numero_credito,
                cr.fecha_desembolso,
                cr.fecha_vencimiento,
                cr.saldo_capital,
                cr.estado_operativo,
                s.etapa_prospeccion,
                s.motivo_descarte_rechazo,
                s.promotor_credito,
                s.sucursal_credito,
                s.oficina_credito,
                s.fecha_sistema_prospeccion,
                s.referencias_prospeccion_json,
                s.visitas_prospeccion_json,
                s.fecha_consulta_central,
                s.central_riesgo_json,
                s.tasa_comision_ascc,
                s.tasa_deslizamiento_anual,
                s.tasa_mora_anual
            FROM creditos.solicitud_credito s
            INNER JOIN clientes.cliente c ON c.id_cliente = s.id_cliente
            LEFT JOIN creditos.credito cr ON cr.id_solicitud_credito = s.id_solicitud_credito
            WHERE s.id_solicitud_credito = @id_solicitud_credito;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = requestId;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var file = new CreditFileDto
        {
            GeneratedAt = DateTime.Now,
            Request = new CreditFileRequestDto
            {
                Id = reader.GetInt64(0),
                Number = reader.GetString(1),
                RequestDate = reader.GetDateTime(2),
                Status = reader.GetString(3),
                Product = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Currency = reader.GetString(5),
                Amount = reader.GetDecimal(6),
                TermMonths = reader.GetInt32(7),
                AnnualRate = reader.GetDecimal(8),
                Frequency = reader.GetString(9),
                InstallmentType = reader.GetString(10),
                EstimatedInstallment = reader.GetDecimal(11),
                Destination = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                DeclaredIncome = reader.GetDecimal(13),
                DeclaredExpenses = reader.GetDecimal(14),
                PaymentCapacity = reader.GetDecimal(15),
                IncomeSource = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                FinancedActivity = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                GuaranteeType = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                GuaranteeDescription = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),
                GuaranteeValue = reader.GetDecimal(20),
                GuarantorName = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                GuarantorIdentification = reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                GuarantorPhone = reader.IsDBNull(23) ? string.Empty : reader.GetString(23),
                RequiresCommittee = reader.GetBoolean(24),
                RiskLevel = reader.GetString(25),
                ConamiClassification = reader.GetString(26),
                Notes = reader.IsDBNull(28) ? string.Empty : reader.GetString(28),
                RegisteredBy = reader.IsDBNull(29) ? string.Empty : reader.GetString(29),
                CreatedAt = reader.GetDateTime(30),
                ResolutionUser = reader.IsDBNull(31) ? string.Empty : reader.GetString(31),
                ResolutionDate = reader.IsDBNull(32) ? null : reader.GetDateTime(32),
                CreditId = reader.IsDBNull(55) ? null : reader.GetInt64(55),
                CreditNumber = reader.IsDBNull(56) ? string.Empty : reader.GetString(56),
                DisbursementDate = reader.IsDBNull(57) ? null : reader.GetDateTime(57),
                DueDate = reader.IsDBNull(58) ? null : reader.GetDateTime(58),
                PrincipalBalance = reader.IsDBNull(59) ? null : reader.GetDecimal(59),
                OperationalStatus = reader.IsDBNull(60) ? string.Empty : reader.GetString(60),
                ProspectionStage = reader.IsDBNull(61) ? "PROSPECTO" : reader.GetString(61),
                DiscardRejectReason = reader.IsDBNull(62) ? string.Empty : reader.GetString(62),
                Promoter = reader.IsDBNull(63) ? string.Empty : reader.GetString(63),
                Branch = reader.IsDBNull(64) ? string.Empty : reader.GetString(64),
                Office = reader.IsDBNull(65) ? string.Empty : reader.GetString(65),
                SystemDate = reader.IsDBNull(66) ? null : reader.GetDateTime(66),
                CommissionRate = reader.IsDBNull(71) ? 0 : reader.GetDecimal(71),
                SlidingRate = reader.IsDBNull(72) ? 0 : reader.GetDecimal(72),
                MoraRate = reader.IsDBNull(73) ? 0 : reader.GetDecimal(73),
            },
            Client = new CreditFileClientDto
            {
                Id = reader.GetInt64(33),
                Identification = reader.GetString(34),
                Name = reader.GetString(35),
                ClientType = reader.GetString(36),
                Status = reader.GetString(37),
                Phone = reader.GetString(38),
                Mobile = reader.GetString(39),
                Email = reader.GetString(40),
                Address = reader.GetString(41),
                HomeGeography = reader.GetString(42),
                Occupation = reader.GetString(43),
                EconomicActivity = reader.GetString(44),
                BusinessName = reader.GetString(45),
                BusinessAddress = reader.GetString(46),
                TotalIncome = reader.GetDecimal(47),
                MonthlyExpenses = reader.GetDecimal(48),
                RiskLevel = reader.GetString(49),
                RiskScore = reader.GetInt32(50),
                FileStatus = reader.GetString(51),
                SourceOfFunds = reader.GetString(52),
                RelationshipPurpose = reader.GetString(53),
                IsPep = reader.GetBoolean(54),
            },
        };

        file.Checklist = DeserializeChecklist(reader.IsDBNull(27) ? null : reader.GetString(27));
        file.References = DeserializeProspectionReferences(reader.IsDBNull(67) ? null : reader.GetString(67));
        file.Visits = DeserializeProspectionVisits(reader.IsDBNull(68) ? null : reader.GetString(68));
        file.CreditBureau = DeserializeCreditBureau(reader.IsDBNull(70) ? null : reader.GetString(70));
        reader.Close();

        file.Approvals = LoadApprovals(connection, requestId);
        file.PaymentPlan = LoadPaymentPlan(connection, file.Request);
        file.Summary = BuildCreditFileSummary(file);

        return file;
    }

    public static string BuildConamiReportHtml(ConamiPortfolioReportDto report, ReportBrandingDto branding)
    {
        var title = $"Reporte CONAMI {report.ReportType}";
        var builder = new StringBuilder();
        AppendHtmlHead(builder, title);
        builder.AppendLine("<body><main class=\"sheet landscape\">");
        AppendScreenActions(builder, $"Reporte-CONAMI-{report.ReportType}-{report.CutoffDate:yyyyMMdd}");
        AppendReportHeader(builder, branding, title, $"Fecha corte: {report.CutoffDate:dd/MM/yyyy} | Tipo cambio: {report.ExchangeRate:N4}");
        AppendConamiSummary(builder, report);
        AppendConamiTable(builder, report.Rows);
        builder.AppendLine("</main>");
        AppendPrintScript(builder, $"Reporte-CONAMI-{report.ReportType}-{report.CutoffDate:yyyyMMdd}");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string BuildConamiReportExcel(ConamiPortfolioReportDto report, ReportBrandingDto branding)
    {
        var builder = BuildExcelStart("CONAMI");
        builder.AppendLine($"<tr><td colspan=\"18\" class=\"title\">{H(branding.LegalName)}</td></tr>");
        builder.AppendLine($"<tr><td colspan=\"18\" class=\"subtitle\">Reporte CONAMI {H(report.ReportType)} - Corte {report.CutoffDate:dd/MM/yyyy}</td></tr>");
        builder.AppendLine($"<tr><td colspan=\"18\">Tipo cambio referencia: {report.ExchangeRate:N4}</td></tr>");
        AppendConamiExcelRows(builder, report.Rows);
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string BuildCreditFileHtml(CreditFileDto file, ReportBrandingDto branding)
    {
        var title = $"Expediente de credito {file.Request.Number}";
        var builder = new StringBuilder();
        AppendHtmlHead(builder, title);
        builder.AppendLine("<body><main class=\"sheet\">");
        AppendScreenActions(builder, $"Expediente-{SanitizeFileNamePart(file.Request.Number)}");
        AppendReportHeader(builder, branding, title, $"Cliente: {file.Client.Name} | Cedula: {file.Client.Identification}");
        AppendCreditFileSections(builder, file, includePlan: true);
        builder.AppendLine("</main>");
        AppendPrintScript(builder, $"Expediente-{SanitizeFileNamePart(file.Request.Number)}");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string BuildCreditFileExcel(CreditFileDto file, ReportBrandingDto branding)
    {
        var builder = BuildExcelStart("Expediente");
        builder.AppendLine($"<tr><td colspan=\"8\" class=\"title\">{H(branding.LegalName)}</td></tr>");
        builder.AppendLine($"<tr><td colspan=\"8\" class=\"subtitle\">Expediente de credito {H(file.Request.Number)}</td></tr>");
        AppendCreditFileExcelSections(builder, file, includePlan: true);
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string BuildPaymentPlanHtml(CreditFileDto file, ReportBrandingDto branding)
    {
        var title = $"Plan de pago {file.Request.Number}";
        var builder = new StringBuilder();
        AppendHtmlHead(builder, title);
        builder.AppendLine("<body><main class=\"sheet\">");
        AppendScreenActions(builder, $"Plan-{SanitizeFileNamePart(file.Request.Number)}");
        AppendReportHeader(builder, branding, title, $"Cliente: {file.Client.Name} | Monto: {file.Request.Currency} {file.Request.Amount:N2}");
        AppendPlanTable(builder, file.PaymentPlan, file.Request.Currency);
        builder.AppendLine("</main>");
        AppendPrintScript(builder, $"Plan-{SanitizeFileNamePart(file.Request.Number)}");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string BuildPaymentPlanExcel(CreditFileDto file, ReportBrandingDto branding)
    {
        var builder = BuildExcelStart("PlanPago");
        builder.AppendLine($"<tr><td colspan=\"8\" class=\"title\">{H(branding.LegalName)}</td></tr>");
        builder.AppendLine($"<tr><td colspan=\"8\" class=\"subtitle\">Plan de pago {H(file.Request.Number)}</td></tr>");
        AppendPlanExcelRows(builder, file.PaymentPlan, file.Request.Currency);
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string SanitizeFileNamePart(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "reporte" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        return new string(text.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray());
    }

    private static decimal LoadReferenceExchangeRate(SqlConnection connection, DateTime cutoffDate)
    {
        const string sql = """
            IF OBJECT_ID(N'parametros.tipo_cambio_oficial', N'U') IS NOT NULL
            BEGIN
                SELECT TOP (1) valor_tipo_cambio
                FROM parametros.tipo_cambio_oficial
                WHERE moneda_origen = N'USD'
                  AND moneda_destino = N'NIO'
                  AND fecha_tipo_cambio <= @fecha_corte
                ORDER BY fecha_tipo_cambio DESC, id_tipo_cambio_oficial DESC;
            END
            ELSE
            BEGIN
                SELECT CAST(NULL AS DECIMAL(18,6));
            END
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoffDate.Date;
        var value = command.ExecuteScalar();
        return value == null || value == DBNull.Value ? FallbackExchangeRate : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static List<CreditFileApprovalDto> LoadApprovals(SqlConnection connection, long requestId)
    {
        const string sql = """
            SELECT
                fecha_aprobacion,
                monto_aprobado,
                plazo_meses,
                tasa_interes_anual,
                moneda,
                usuario_aprobador,
                resolucion,
                observacion
            FROM creditos.aprobacion_solicitud_credito
            WHERE id_solicitud_credito = @id_solicitud_credito
            ORDER BY id_aprobacion_solicitud_credito DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = requestId;
        using var reader = command.ExecuteReader();
        var items = new List<CreditFileApprovalDto>();
        while (reader.Read())
        {
            items.Add(new CreditFileApprovalDto
            {
                ApprovalDate = reader.GetDateTime(0),
                ApprovedAmount = reader.GetDecimal(1),
                TermMonths = reader.GetInt32(2),
                AnnualRate = reader.GetDecimal(3),
                Currency = reader.GetString(4),
                ApprovedBy = reader.GetString(5),
                Resolution = reader.GetString(6),
                Note = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            });
        }

        return items;
    }

    private static List<CreditFilePlanRowDto> LoadPaymentPlan(SqlConnection connection, CreditFileRequestDto request)
    {
        if (request.CreditId.HasValue)
        {
            const string sql = """
                SELECT
                    numero_cuota,
                    fecha_cuota,
                    dias_interes,
                    capital_programado,
                    interes_programado,
                    comision_programada,
                    mora_programada,
                    deslizamiento_programado,
                    capital_programado + interes_programado + comision_programada + deslizamiento_programado + mora_programada AS total_cuota,
                    saldo_capital_cuota,
                    estado_cuota
                FROM creditos.plan_pago_credito
                WHERE id_credito = @id_credito
                ORDER BY numero_cuota;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = request.CreditId.Value;
            using var reader = command.ExecuteReader();
            var stored = new List<CreditFilePlanRowDto>();
            while (reader.Read())
            {
                stored.Add(new CreditFilePlanRowDto
                {
                    Number = reader.GetInt32(0),
                    DueDate = reader.GetDateTime(1),
                    InterestDays = reader.GetInt32(2),
                    Capital = reader.GetDecimal(3),
                    Interest = reader.GetDecimal(4),
                    Commission = reader.GetDecimal(5),
                    Mora = reader.GetDecimal(6),
                    Sliding = reader.GetDecimal(7),
                    Total = reader.GetDecimal(8),
                    Balance = reader.GetDecimal(9),
                    Status = reader.GetString(10),
                });
            }

            if (stored.Count > 0)
            {
                return stored;
            }
        }

        return CreditOperationsSupport.GeneratePaymentPlan(
                request.Amount,
                request.AnnualRate,
                request.TermMonths,
                request.Frequency,
                request.RequestDate,
                request.CommissionRate,
                request.SlidingRate,
                request.MoraRate)
            .Select(row => new CreditFilePlanRowDto
            {
                Number = row.Number,
                DueDate = row.DueDate,
                InterestDays = row.InterestDays,
                Capital = row.Capital,
                Interest = row.Interest,
                Commission = row.Commission,
                Sliding = row.Sliding,
                Mora = row.Mora,
                Total = row.Total,
                Balance = row.Balance,
                Status = "SIMULADO",
            })
            .ToList();
    }

    private static CreditChecklistSnapshotDto DeserializeChecklist(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new CreditChecklistSnapshotDto();
        }

        try
        {
            return JsonSerializer.Deserialize<CreditChecklistSnapshotDto>(raw) ?? new CreditChecklistSnapshotDto();
        }
        catch
        {
            return new CreditChecklistSnapshotDto();
        }
    }

    private static CreditFileReferencesDto DeserializeProspectionReferences(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new CreditFileReferencesDto();
        }

        try
        {
            return JsonSerializer.Deserialize<CreditFileReferencesDto>(raw) ?? new CreditFileReferencesDto();
        }
        catch
        {
            return new CreditFileReferencesDto();
        }
    }

    private static CreditFileVisitsDto DeserializeProspectionVisits(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new CreditFileVisitsDto();
        }

        try
        {
            return JsonSerializer.Deserialize<CreditFileVisitsDto>(raw) ?? new CreditFileVisitsDto();
        }
        catch
        {
            return new CreditFileVisitsDto();
        }
    }

    private static CreditFileBureauDto DeserializeCreditBureau(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new CreditFileBureauDto();
        }

        try
        {
            return JsonSerializer.Deserialize<CreditFileBureauDto>(raw) ?? new CreditFileBureauDto();
        }
        catch
        {
            return new CreditFileBureauDto();
        }
    }

    private static CreditFileSummaryDto BuildCreditFileSummary(CreditFileDto file)
    {
        var planTotal = file.PaymentPlan.Sum(row => row.Total);
        var planInterest = file.PaymentPlan.Sum(row => row.Interest);
        var firstInstallment = file.PaymentPlan.FirstOrDefault()?.Total ?? 0;
        var paymentCapacityRatio = file.Request.PaymentCapacity <= 0 ? 0 : firstInstallment / file.Request.PaymentCapacity;
        var guaranteeCoverage = file.Request.Amount <= 0 ? 0 : file.Request.GuaranteeValue / file.Request.Amount;
        var missing = new List<string>();

        if (!file.Checklist.Identification) missing.Add("Identificacion");
        if (!file.Checklist.FileCompleted) missing.Add("Expediente");
        if (!file.Checklist.HomeBusinessVisit) missing.Add("Visita");
        if (!file.Checklist.PaymentCapacity) missing.Add("Capacidad");
        if (!file.Checklist.ConamiReview) missing.Add("CONAMI");
        if (!file.Checklist.ListCheck) missing.Add("Listas");
        if (!file.Checklist.GuaranteeReview) missing.Add("Garantia");
        if (file.Request.ProspectionStage != "SOLICITUD_FORMAL") missing.Add("Solicitud formal");
        if (string.IsNullOrWhiteSpace(file.References.Personal.Name)) missing.Add("Referencia personal");
        if (string.IsNullOrWhiteSpace(file.References.Commercial.Name) && string.IsNullOrWhiteSpace(file.References.Financial.Name)) missing.Add("Referencia comercial/financiera");
        if (!string.Equals(file.Visits.Home.Result, "REALIZADA", StringComparison.OrdinalIgnoreCase)) missing.Add("Visita domiciliar");
        if (!string.Equals(file.Visits.Business.Result, "REALIZADA", StringComparison.OrdinalIgnoreCase)) missing.Add("Visita negocio");
        if (!file.CreditBureau.Consulted) missing.Add("SIN RIESGO");
        if (string.IsNullOrWhiteSpace(file.CreditBureau.ReportNumber)) missing.Add("Reporte SIN RIESGO");

        return new CreditFileSummaryDto
        {
            PlanTotal = CreditOperationsSupport.SafeDecimal(planTotal),
            PlanInterest = CreditOperationsSupport.SafeDecimal(planInterest),
            FirstInstallment = CreditOperationsSupport.SafeDecimal(firstInstallment),
            PaymentCapacityRatio = CreditOperationsSupport.SafeDecimal(paymentCapacityRatio * 100),
            GuaranteeCoverage = CreditOperationsSupport.SafeDecimal(guaranteeCoverage * 100),
            MissingChecklistItems = missing,
        };
    }

    private static string BuildPastDueRange(int days)
    {
        if (days <= 0) return "a. Al Dia";
        if (days <= 15) return "b. Mora 1 a 15 Dias";
        if (days <= 30) return "c. Mora 16 a 30 Dias";
        if (days <= 60) return "d. Mora 31 a 60 Dias";
        if (days <= 90) return "e. Mora 61 a 90 Dias";
        if (days <= 120) return "f. Mora 91 a 120 Dias";
        if (days <= 180) return "g. Mora 121 a 180 Dias";
        if (days <= 240) return "h. Mora 181 a 240 Dias";
        if (days <= 360) return "i. Mora 241 a 360 Dias";
        return "j. Mora 360 dias a mas";
    }

    private static string BuildUsdDisbursementRange(decimal amountUsd)
    {
        if (amountUsd <= 300) return "1_Menor_$300";
        if (amountUsd <= 500) return "2_De_$300_$500";
        if (amountUsd <= 1000) return "3_De_$501_$1000";
        if (amountUsd <= 3000) return "4_De_$1000.01_$3000";
        if (amountUsd <= 5000) return "5_De_$3000.01_$5000";
        if (amountUsd <= 10000) return "6_De_$5000.01_$10000";
        if (amountUsd <= 15000) return "7_De_$10000.01_$15000";
        if (amountUsd <= 201000) return "8_De_$15000.01_$201000";
        return "9_Mayor_$201000";
    }

    private static string BuildConamiClassification(int days)
    {
        if (days <= 15) return "A";
        if (days <= 30) return "B";
        if (days <= 60) return "C";
        if (days <= 90) return "D";
        return "E";
    }

    private static int BuildConamiClassificationId(int days)
    {
        if (days <= 15) return 1;
        if (days <= 30) return 2;
        if (days <= 60) return 3;
        if (days <= 90) return 4;
        return 5;
    }

    private static decimal BuildProvisionRate(int days)
    {
        if (days <= 0) return 0m;
        if (days <= 15) return 0.01m;
        if (days <= 30) return 0.05m;
        if (days <= 60) return 0.20m;
        if (days <= 90) return 0.50m;
        return 1.00m;
    }

    private static void AppendHtmlHead(StringBuilder builder, string title)
    {
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine($"<title>{H(title)}</title>");
        builder.AppendLine("""
            <style>
              :root { color-scheme: light; font-family: "Segoe UI", Arial, sans-serif; color: #17262f; }
              body { margin: 0; background: #e8eef0; }
              .sheet { width: min(1120px, calc(100% - 28px)); margin: 14px auto; background: #fff; padding: 26px; box-shadow: 0 12px 34px rgba(0,0,0,.14); }
              .sheet.landscape { width: min(1520px, calc(100% - 28px)); }
              .screen-actions { display:flex; gap:8px; justify-content:flex-end; margin-bottom:14px; }
              button { min-height:38px; border:1px solid #c8d4d8; border-radius:7px; padding:0 12px; background:#f6f9fa; font-weight:700; cursor:pointer; }
              button.is-primary { background:#168f82; color:white; border-color:#168f82; }
              .report-head { border-bottom: 3px solid #168f82; padding-bottom: 14px; margin-bottom: 16px; display:flex; justify-content:space-between; gap:18px; }
              h1, h2, h3 { margin:0; letter-spacing:0; }
              h1 { font-size: 1.35rem; }
              h2 { font-size: 1.05rem; color:#45616b; margin-top:4px; }
              h3 { margin: 22px 0 8px; font-size: .96rem; text-transform: uppercase; color:#168f82; }
              .muted { color:#58717a; font-size:.9rem; }
              .grid { display:grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap:8px; }
              .box { border:1px solid #d7e1e4; border-radius:7px; padding:9px; background:#fbfdfd; }
              .box span { display:block; color:#667d85; font-size:.73rem; text-transform:uppercase; font-weight:700; }
              .box strong { display:block; margin-top:4px; overflow-wrap:anywhere; }
              table { width:100%; border-collapse:collapse; font-size:.82rem; }
              th, td { border:1px solid #d5e0e3; padding:7px 8px; text-align:left; vertical-align:top; }
              th { background:#eaf4f3; color:#17343a; text-transform:uppercase; font-size:.72rem; }
              .right { text-align:right; }
              .badge { display:inline-block; border-radius:999px; padding:4px 8px; background:#e9f6f3; font-weight:700; }
              .danger { color:#9c2f25; }
              .ok { color:#166b36; }
              @media print {
                body { background:white; }
                .sheet, .sheet.landscape { width:auto; margin:0; padding:12mm; box-shadow:none; }
                .screen-actions { display:none; }
                table { font-size:9px; }
                .page-break { break-before: page; }
              }
            </style>
            """);
        builder.AppendLine("</head>");
    }

    private static void AppendScreenActions(StringBuilder builder, string fileName)
    {
        builder.AppendLine("<div class=\"screen-actions\">");
        builder.AppendLine("<button type=\"button\" onclick=\"window.print()\">Imprimir</button>");
        builder.AppendLine($"<button class=\"is-primary\" type=\"button\" onclick=\"exportPdf('{H(fileName)}')\">Generar PDF</button>");
        builder.AppendLine("<button type=\"button\" onclick=\"exportExcel()\">Generar Excel</button>");
        builder.AppendLine("</div>");
    }

    private static void AppendPrintScript(StringBuilder builder, string fileName)
    {
        builder.AppendLine("<script>");
        builder.AppendLine("const originalTitle = document.title;");
        builder.AppendLine("function exportExcel() { window.location.href = `${window.location.pathname.replace('Html', 'Excel')}${window.location.search}`; }");
        builder.AppendLine($"function exportPdf() {{ document.title = \"{H(fileName)}\"; window.print(); setTimeout(() => document.title = originalTitle, 400); }}");
        builder.AppendLine("</script>");
    }

    private static void AppendReportHeader(StringBuilder builder, ReportBrandingDto branding, string title, string subtitle)
    {
        builder.AppendLine("<header class=\"report-head\">");
        builder.AppendLine("<div>");
        builder.AppendLine($"<h1>{H(branding.LegalName)}</h1>");
        builder.AppendLine($"<h2>{H(title)}</h2>");
        builder.AppendLine($"<div class=\"muted\">{H(subtitle)}</div>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"muted right\">");
        builder.AppendLine($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}<br />");
        builder.AppendLine(H(branding.FooterText));
        builder.AppendLine("</div></header>");
    }

    private static void AppendConamiSummary(StringBuilder builder, ConamiPortfolioReportDto report)
    {
        var totalBalance = report.Rows.Sum(row => row.PrincipalBalance);
        var totalProvision = report.Rows.Sum(row => row.ProvisionAmount);
        var overdue = report.Rows.Count(row => row.DaysPastDue > 0);
        builder.AppendLine("<section class=\"grid\">");
        AppendBox(builder, "Creditos", report.Rows.Count.ToString("N0", CultureInfo.CurrentCulture));
        AppendBox(builder, "Saldo capital", totalBalance.ToString("N2", CultureInfo.CurrentCulture));
        AppendBox(builder, "Provision estimada", totalProvision.ToString("N2", CultureInfo.CurrentCulture));
        AppendBox(builder, "Creditos vencidos", overdue.ToString("N0", CultureInfo.CurrentCulture));
        builder.AppendLine("</section>");
    }

    private static void AppendConamiTable(StringBuilder builder, IReadOnlyList<ConamiPortfolioRowDto> rows)
    {
        builder.AppendLine("<h3>Cartera clasificada</h3>");
        builder.AppendLine("<table><thead><tr>");
        foreach (var header in new[] { "Cliente", "Cedula", "Credito", "Producto", "Monto", "Saldo", "Cuota", "Mora", "Rango", "Clase", "Provision", "Garantia", "Vence", "Actividad" })
        {
            builder.AppendLine($"<th>{H(header)}</th>");
        }
        builder.AppendLine("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{H(row.ClientName)}</td><td>{H(row.Identification)}</td><td>{H(row.CreditNumber)}</td><td>{H(row.Product)}</td>");
            builder.AppendLine($"<td class=\"right\">{row.Currency} {row.ApprovedAmount:N2}</td><td class=\"right\">{row.PrincipalBalance:N2}</td><td class=\"right\">{row.Installment:N2}</td>");
            builder.AppendLine($"<td class=\"right\">{row.DaysPastDue}</td><td>{H(row.PastDueRange)}</td><td><span class=\"badge\">{H(row.ConamiClassification)}</span></td>");
            builder.AppendLine($"<td class=\"right\">{row.ProvisionAmount:N2}</td><td>{H(row.GuaranteeType)}</td><td>{row.DueDate:dd/MM/yyyy}</td><td>{H(row.EconomicActivity)}</td>");
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

    private static void AppendConamiExcelRows(StringBuilder builder, IReadOnlyList<ConamiPortfolioRowDto> rows)
    {
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><th>IdCliente</th><th>Cliente</th><th>Cedula</th><th>Actividad</th><th>TipoGarantia</th><th>IdCredito</th><th>MontoAprobado</th><th>Interes</th><th>Plazo</th><th>Cuota</th><th>EstadoCredito</th><th>CuotasVencidas</th><th>SaldoCapital</th><th>FechaVencimiento</th><th>Situacion_Conami</th><th>Rango_Atraso_Conami</th><th>Rango_Desembolso_Dolarizado</th><th>Clasificacion</th><th>Provision</th></tr>");
        foreach (var row in rows)
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{row.ClientId}</td><td>{H(row.ClientName)}</td><td>{H(row.Identification)}</td><td>{H(row.EconomicActivity)}</td><td>{H(row.GuaranteeType)}</td><td>{row.CreditId}</td>");
            builder.AppendLine($"<td>{row.ApprovedAmount:N2}</td><td>{row.AnnualRate:N6}</td><td>{row.TermMonths}</td><td>{row.Installment:N2}</td><td>{H(row.OperationalStatus)}</td><td>{row.PastDueInstallments}</td>");
            builder.AppendLine($"<td>{row.PrincipalBalance:N2}</td><td>{row.DueDate:dd/MM/yyyy}</td><td>{H(row.Situation)}</td><td>{H(row.PastDueRange)}</td><td>{H(row.DisbursementUsdRange)}</td><td>{H(row.ConamiClassification)}</td><td>{row.ProvisionAmount:N2}</td>");
            builder.AppendLine("</tr>");
        }
        builder.AppendLine("</table>");
    }

    private static void AppendCreditFileSections(StringBuilder builder, CreditFileDto file, bool includePlan)
    {
        builder.AppendLine("<section class=\"grid\">");
        AppendBox(builder, "Solicitud", file.Request.Number);
        AppendBox(builder, "Estado", file.Request.Status);
        AppendBox(builder, "Monto", $"{file.Request.Currency} {file.Request.Amount:N2}");
        AppendBox(builder, "Cuota estimada", $"{file.Request.EstimatedInstallment:N2}");
        AppendBox(builder, "Capacidad cuota", $"{file.Summary.PaymentCapacityRatio:N2}%");
        AppendBox(builder, "Cobertura garantia", $"{file.Summary.GuaranteeCoverage:N2}%");
        AppendBox(builder, "Riesgo", $"{file.Request.RiskLevel} / {file.Request.ConamiClassification}");
        AppendBox(builder, "Comite", file.Request.RequiresCommittee ? "SI" : "NO");
        builder.AppendLine("</section>");

        builder.AppendLine("<h3>Prospeccion formal</h3>");
        builder.AppendLine("<section class=\"grid\">");
        AppendBox(builder, "Etapa", file.Request.ProspectionStage);
        AppendBox(builder, "Promotor", file.Request.Promoter);
        AppendBox(builder, "Sucursal", file.Request.Branch);
        AppendBox(builder, "Oficina", file.Request.Office);
        AppendBox(builder, "Fecha sistema", file.Request.SystemDate?.ToString("dd/MM/yyyy") ?? "-");
        AppendBox(builder, "Motivo descarte/rechazo", file.Request.DiscardRejectReason);
        builder.AppendLine("</section>");

        builder.AppendLine("<table><thead><tr><th>Referencia</th><th>Nombre</th><th>Telefono</th><th>Resultado</th></tr></thead><tbody>");
        AppendReferenceHtml(builder, "Personal", file.References.Personal);
        AppendReferenceHtml(builder, "Comercial", file.References.Commercial);
        AppendReferenceHtml(builder, "Financiera", file.References.Financial);
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<table><thead><tr><th>Visita</th><th>Fecha</th><th>Resultado</th><th>Observacion</th><th>Evidencia</th></tr></thead><tbody>");
        AppendVisitHtml(builder, "Domiciliar", file.Visits.Home);
        AppendVisitHtml(builder, "Negocio", file.Visits.Business);
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h3>SIN RIESGO y endeudamiento</h3>");
        builder.AppendLine("<section class=\"grid\">");
        AppendBox(builder, "Fuente", file.CreditBureau.BureauName);
        AppendBox(builder, "Reporte", file.CreditBureau.ReportNumber);
        AppendBox(builder, "Fecha consulta", file.CreditBureau.ConsultationDate?.ToString("dd/MM/yyyy") ?? "-");
        AppendBox(builder, "Resultado", file.CreditBureau.Result);
        AppendBox(builder, "Deuda externa", file.CreditBureau.ExternalDebt.ToString("N2", CultureInfo.CurrentCulture));
        AppendBox(builder, "Cuota externa", file.CreditBureau.ExternalInstallment.ToString("N2", CultureInfo.CurrentCulture));
        AppendBox(builder, "Cuota nueva", file.CreditBureau.RequestedInstallment.ToString("N2", CultureInfo.CurrentCulture));
        AppendBox(builder, "Endeudamiento", $"{file.CreditBureau.DebtCapacityRatio:N2}%");
        builder.AppendLine("</section>");
        if (file.CreditBureau.Alerts.Count > 0)
        {
            builder.AppendLine($"<p><strong>Alertas:</strong> {H(string.Join(" | ", file.CreditBureau.Alerts))}</p>");
        }

        builder.AppendLine("<h3>Cliente</h3>");
        builder.AppendLine("<section class=\"grid\">");
        AppendBox(builder, "Cedula", file.Client.Identification);
        AppendBox(builder, "Nombre", file.Client.Name);
        AppendBox(builder, "Tipo", file.Client.ClientType);
        AppendBox(builder, "Expediente", file.Client.FileStatus);
        AppendBox(builder, "Actividad", file.Client.EconomicActivity);
        AppendBox(builder, "Negocio", file.Client.BusinessName);
        AppendBox(builder, "Ingresos", file.Client.TotalIncome.ToString("N2", CultureInfo.CurrentCulture));
        AppendBox(builder, "Egresos", file.Client.MonthlyExpenses.ToString("N2", CultureInfo.CurrentCulture));
        builder.AppendLine("</section>");

        builder.AppendLine("<h3>Evaluacion y garantia</h3>");
        builder.AppendLine("<section class=\"grid\">");
        AppendBox(builder, "Destino", file.Request.Destination);
        AppendBox(builder, "Fuente ingreso", file.Request.IncomeSource);
        AppendBox(builder, "Actividad financiada", file.Request.FinancedActivity);
        AppendBox(builder, "Garantia", file.Request.GuaranteeType);
        AppendBox(builder, "Valor garantia", file.Request.GuaranteeValue.ToString("N2", CultureInfo.CurrentCulture));
        AppendBox(builder, "Fiador", file.Request.GuarantorName);
        AppendBox(builder, "Credito generado", string.IsNullOrWhiteSpace(file.Request.CreditNumber) ? "No generado" : file.Request.CreditNumber);
        AppendBox(builder, "Vence", file.Request.DueDate?.ToString("dd/MM/yyyy") ?? "-");
        builder.AppendLine("</section>");

        builder.AppendLine("<h3>Check de expediente</h3>");
        builder.AppendLine("<table><thead><tr><th>Control</th><th>Estado</th></tr></thead><tbody>");
        AppendChecklistHtml(builder, "Identificacion", file.Checklist.Identification);
        AppendChecklistHtml(builder, "Expediente completo", file.Checklist.FileCompleted);
        AppendChecklistHtml(builder, "Visita casa/negocio", file.Checklist.HomeBusinessVisit);
        AppendChecklistHtml(builder, "Capacidad de pago", file.Checklist.PaymentCapacity);
        AppendChecklistHtml(builder, "Revision CONAMI", file.Checklist.ConamiReview);
        AppendChecklistHtml(builder, "Listas restrictivas", file.Checklist.ListCheck);
        AppendChecklistHtml(builder, "Revision garantia", file.Checklist.GuaranteeReview);
        builder.AppendLine("</tbody></table>");

        if (file.Summary.MissingChecklistItems.Count > 0)
        {
            builder.AppendLine($"<p class=\"danger\"><strong>Pendiente:</strong> {H(string.Join(", ", file.Summary.MissingChecklistItems))}</p>");
        }

        if (file.Approvals.Count > 0)
        {
            builder.AppendLine("<h3>Resoluciones</h3><table><thead><tr><th>Fecha</th><th>Resolucion</th><th>Monto</th><th>Usuario</th><th>Nota</th></tr></thead><tbody>");
            foreach (var approval in file.Approvals)
            {
                builder.AppendLine($"<tr><td>{approval.ApprovalDate:dd/MM/yyyy HH:mm}</td><td>{H(approval.Resolution)}</td><td class=\"right\">{approval.Currency} {approval.ApprovedAmount:N2}</td><td>{H(approval.ApprovedBy)}</td><td>{H(approval.Note)}</td></tr>");
            }
            builder.AppendLine("</tbody></table>");
        }

        if (includePlan)
        {
            builder.AppendLine("<div class=\"page-break\"></div>");
            AppendPlanTable(builder, file.PaymentPlan, file.Request.Currency);
        }
    }

    private static void AppendCreditFileExcelSections(StringBuilder builder, CreditFileDto file, bool includePlan)
    {
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><th colspan=\"4\">Datos de solicitud</th><th colspan=\"4\">Datos de cliente</th></tr>");
        AppendExcelInfoRow(builder, "Solicitud", file.Request.Number, "Estado", file.Request.Status, "Cliente", file.Client.Name, "Cedula", file.Client.Identification);
        AppendExcelInfoRow(builder, "Monto", $"{file.Request.Currency} {file.Request.Amount:N2}", "Plazo", $"{file.Request.TermMonths}", "Actividad", file.Client.EconomicActivity, "Expediente", file.Client.FileStatus);
        AppendExcelInfoRow(builder, "Destino", file.Request.Destination, "Cuota", $"{file.Request.EstimatedInstallment:N2}", "Ingresos", $"{file.Client.TotalIncome:N2}", "Egresos", $"{file.Client.MonthlyExpenses:N2}");
        AppendExcelInfoRow(builder, "Garantia", file.Request.GuaranteeType, "Valor garantia", $"{file.Request.GuaranteeValue:N2}", "Riesgo", file.Request.RiskLevel, "CONAMI", file.Request.ConamiClassification);
        builder.AppendLine("</table>");

        builder.AppendLine("<br /><table>");
        builder.AppendLine("<tr><th colspan=\"8\">Prospeccion formal</th></tr>");
        AppendExcelInfoRow(builder, "Etapa", file.Request.ProspectionStage, "Promotor", file.Request.Promoter, "Sucursal", file.Request.Branch, "Oficina", file.Request.Office);
        AppendExcelInfoRow(builder, "Fecha sistema", file.Request.SystemDate?.ToString("dd/MM/yyyy") ?? "-", "Motivo", file.Request.DiscardRejectReason, "Casa", file.Visits.Home.Result, "Negocio", file.Visits.Business.Result);
        builder.AppendLine("<tr><th>Referencia</th><th>Nombre</th><th>Telefono</th><th colspan=\"5\">Resultado</th></tr>");
        AppendReferenceExcel(builder, "Personal", file.References.Personal);
        AppendReferenceExcel(builder, "Comercial", file.References.Commercial);
        AppendReferenceExcel(builder, "Financiera", file.References.Financial);
        builder.AppendLine("</table>");

        builder.AppendLine("<br /><table>");
        builder.AppendLine("<tr><th colspan=\"8\">SIN RIESGO y endeudamiento</th></tr>");
        AppendExcelInfoRow(builder, "Fuente", file.CreditBureau.BureauName, "Reporte", file.CreditBureau.ReportNumber, "Resultado", file.CreditBureau.Result, "Score", $"{file.CreditBureau.Score}");
        AppendExcelInfoRow(builder, "Deuda externa", $"{file.CreditBureau.ExternalDebt:N2}", "Cuota externa", $"{file.CreditBureau.ExternalInstallment:N2}", "Cuota nueva", $"{file.CreditBureau.RequestedInstallment:N2}", "Endeudamiento", $"{file.CreditBureau.DebtCapacityRatio:N2}%");
        builder.AppendLine("</table>");

        builder.AppendLine("<br /><table><tr><th>Check</th><th>Estado</th></tr>");
        AppendChecklistExcel(builder, "Identificacion", file.Checklist.Identification);
        AppendChecklistExcel(builder, "Expediente completo", file.Checklist.FileCompleted);
        AppendChecklistExcel(builder, "Visita casa/negocio", file.Checklist.HomeBusinessVisit);
        AppendChecklistExcel(builder, "Capacidad de pago", file.Checklist.PaymentCapacity);
        AppendChecklistExcel(builder, "Revision CONAMI", file.Checklist.ConamiReview);
        AppendChecklistExcel(builder, "Listas restrictivas", file.Checklist.ListCheck);
        AppendChecklistExcel(builder, "Revision garantia", file.Checklist.GuaranteeReview);
        builder.AppendLine("</table>");

        if (includePlan)
        {
            builder.AppendLine("<br />");
            AppendPlanExcelRows(builder, file.PaymentPlan, file.Request.Currency);
        }
    }

    private static void AppendPlanTable(StringBuilder builder, IReadOnlyList<CreditFilePlanRowDto> plan, string currency)
    {
        builder.AppendLine("<h3>Plan de pago</h3>");
        builder.AppendLine("<table><thead><tr><th>No.</th><th>Fecha</th><th>Dias interes</th><th>Capital</th><th>Interes</th><th>Comision</th><th>Deslizamiento</th><th>Mora</th><th>Total</th><th>Saldo</th><th>Estado</th></tr></thead><tbody>");
        foreach (var row in plan)
        {
            builder.AppendLine($"<tr><td>{row.Number}</td><td>{row.DueDate:dd/MM/yyyy}</td><td class=\"right\">{row.InterestDays}</td><td class=\"right\">{currency} {row.Capital:N2}</td><td class=\"right\">{row.Interest:N2}</td><td class=\"right\">{row.Commission:N2}</td><td class=\"right\">{row.Sliding:N2}</td><td class=\"right\">{row.Mora:N2}</td><td class=\"right\">{row.Total:N2}</td><td class=\"right\">{row.Balance:N2}</td><td>{H(row.Status)}</td></tr>");
        }
        builder.AppendLine($"<tr><th colspan=\"3\">Totales</th><th class=\"right\">{currency} {plan.Sum(row => row.Capital):N2}</th><th class=\"right\">{plan.Sum(row => row.Interest):N2}</th><th class=\"right\">{plan.Sum(row => row.Commission):N2}</th><th class=\"right\">{plan.Sum(row => row.Sliding):N2}</th><th class=\"right\">{plan.Sum(row => row.Mora):N2}</th><th class=\"right\">{plan.Sum(row => row.Total):N2}</th><th></th><th></th></tr>");
        builder.AppendLine("</tbody></table>");
    }

    private static void AppendPlanExcelRows(StringBuilder builder, IReadOnlyList<CreditFilePlanRowDto> plan, string currency)
    {
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><th>No.</th><th>Fecha</th><th>Dias interes</th><th>Capital</th><th>Interes</th><th>Comision</th><th>Deslizamiento</th><th>Mora</th><th>Total</th><th>Saldo</th><th>Estado</th></tr>");
        foreach (var row in plan)
        {
            builder.AppendLine($"<tr><td>{row.Number}</td><td>{row.DueDate:dd/MM/yyyy}</td><td>{row.InterestDays}</td><td>{row.Capital:N2}</td><td>{row.Interest:N2}</td><td>{row.Commission:N2}</td><td>{row.Sliding:N2}</td><td>{row.Mora:N2}</td><td>{row.Total:N2}</td><td>{row.Balance:N2}</td><td>{H(row.Status)}</td></tr>");
        }
        builder.AppendLine($"<tr><th colspan=\"3\">Totales {H(currency)}</th><th>{plan.Sum(row => row.Capital):N2}</th><th>{plan.Sum(row => row.Interest):N2}</th><th>{plan.Sum(row => row.Commission):N2}</th><th>{plan.Sum(row => row.Sliding):N2}</th><th>{plan.Sum(row => row.Mora):N2}</th><th>{plan.Sum(row => row.Total):N2}</th><th></th><th></th></tr>");
        builder.AppendLine("</table>");
    }

    private static StringBuilder BuildExcelStart(string sheetName)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">");
        builder.AppendLine("<head><meta charset=\"utf-8\" /><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
        builder.AppendLine($"""
            <!--[if gte mso 9]>
            <xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet><x:Name>{H(sheetName)}</x:Name><x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions></x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml>
            <![endif]-->
            """);
        builder.AppendLine("""
            <style>
              body, table { font-family: Arial, sans-serif; font-size: 10pt; }
              table { border-collapse: collapse; }
              th, td { border: 1px solid #b8c7cc; padding: 6px; vertical-align: top; }
              th { background: #dcefed; font-weight: bold; }
              .title { font-size: 16pt; font-weight: bold; }
              .subtitle { font-size: 12pt; font-weight: bold; color: #28505a; }
            </style>
            """);
        builder.AppendLine("</head><body>");
        return builder;
    }

    private static void AppendBox(StringBuilder builder, string label, string? value)
    {
        builder.AppendLine($"<article class=\"box\"><span>{H(label)}</span><strong>{H(value)}</strong></article>");
    }

    private static void AppendChecklistHtml(StringBuilder builder, string label, bool ok)
    {
        builder.AppendLine($"<tr><td>{H(label)}</td><td><span class=\"{(ok ? "ok" : "danger")}\">{(ok ? "Completo" : "Pendiente")}</span></td></tr>");
    }

    private static void AppendReferenceHtml(StringBuilder builder, string label, CreditFileReferenceDto reference)
    {
        builder.AppendLine($"<tr><td>{H(label)}</td><td>{H(reference.Name)}</td><td>{H(reference.Phone)}</td><td>{H(reference.Result)}</td></tr>");
    }

    private static void AppendVisitHtml(StringBuilder builder, string label, CreditFileVisitDto visit)
    {
        builder.AppendLine($"<tr><td>{H(label)}</td><td>{visit.Date?.ToString("dd/MM/yyyy") ?? "-"}</td><td>{H(visit.Result)}</td><td>{H(visit.Observation)}</td><td>{H(visit.Evidence)}</td></tr>");
    }

    private static void AppendChecklistExcel(StringBuilder builder, string label, bool ok)
    {
        builder.AppendLine($"<tr><td>{H(label)}</td><td>{(ok ? "Completo" : "Pendiente")}</td></tr>");
    }

    private static void AppendReferenceExcel(StringBuilder builder, string label, CreditFileReferenceDto reference)
    {
        builder.AppendLine($"<tr><td>{H(label)}</td><td>{H(reference.Name)}</td><td>{H(reference.Phone)}</td><td colspan=\"5\">{H(reference.Result)}</td></tr>");
    }

    private static void AppendExcelInfoRow(
        StringBuilder builder,
        string label1,
        string value1,
        string label2,
        string value2,
        string label3,
        string value3,
        string label4,
        string value4)
    {
        builder.AppendLine($"<tr><td><strong>{H(label1)}</strong></td><td>{H(value1)}</td><td><strong>{H(label2)}</strong></td><td>{H(value2)}</td><td><strong>{H(label3)}</strong></td><td>{H(value3)}</td><td><strong>{H(label4)}</strong></td><td>{H(value4)}</td></tr>");
    }

    private static string H(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}

public sealed class ConamiPortfolioReportDto
{
    public DateTime CutoffDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public decimal ExchangeRate { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public List<ConamiPortfolioRowDto> Rows { get; } = new();
}

public sealed class ConamiPortfolioRowDto
{
    public long ClientId { get; set; }
    public string Identification { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public string MaritalStatus { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string EconomicActivity { get; set; } = string.Empty;
    public string GuaranteeType { get; set; } = string.Empty;
    public long CreditId { get; set; }
    public string CreditNumber { get; set; } = string.Empty;
    public decimal ApprovedAmount { get; set; }
    public decimal AnnualRate { get; set; }
    public int TermMonths { get; set; }
    public decimal Installment { get; set; }
    public decimal PrincipalBalance { get; set; }
    public DateTime DisbursementDate { get; set; }
    public DateTime DueDate { get; set; }
    public string OperationalStatus { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string SourceClassification { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public long RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string ClientType { get; set; } = string.Empty;
    public decimal GuaranteeValue { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public DateTime? FirstOverdueDate { get; set; }
    public int PastDueInstallments { get; set; }
    public int TotalInstallments { get; set; }
    public int DaysPastDue { get; set; }
    public string Situation { get; set; } = string.Empty;
    public string PastDueRange { get; set; } = string.Empty;
    public string DisbursementUsdRange { get; set; } = string.Empty;
    public string ConamiClassification { get; set; } = string.Empty;
    public int ClassificationId { get; set; }
    public decimal ProvisionRate { get; set; }
    public decimal ProvisionAmount { get; set; }
}

public sealed class CreditFileDto
{
    public DateTime GeneratedAt { get; set; }
    public CreditFileRequestDto Request { get; set; } = new();
    public CreditFileClientDto Client { get; set; } = new();
    public CreditChecklistSnapshotDto Checklist { get; set; } = new();
    public CreditFileReferencesDto References { get; set; } = new();
    public CreditFileVisitsDto Visits { get; set; } = new();
    public CreditFileBureauDto CreditBureau { get; set; } = new();
    public List<CreditFileApprovalDto> Approvals { get; set; } = new();
    public List<CreditFilePlanRowDto> PaymentPlan { get; set; } = new();
    public CreditFileSummaryDto Summary { get; set; } = new();
}

public sealed class CreditFileRequestDto
{
    public long Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TermMonths { get; set; }
    public decimal AnnualRate { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal SlidingRate { get; set; }
    public decimal MoraRate { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public string InstallmentType { get; set; } = string.Empty;
    public decimal EstimatedInstallment { get; set; }
    public string Destination { get; set; } = string.Empty;
    public decimal DeclaredIncome { get; set; }
    public decimal DeclaredExpenses { get; set; }
    public decimal PaymentCapacity { get; set; }
    public string IncomeSource { get; set; } = string.Empty;
    public string FinancedActivity { get; set; } = string.Empty;
    public string GuaranteeType { get; set; } = string.Empty;
    public string GuaranteeDescription { get; set; } = string.Empty;
    public decimal GuaranteeValue { get; set; }
    public string GuarantorName { get; set; } = string.Empty;
    public string GuarantorIdentification { get; set; } = string.Empty;
    public string GuarantorPhone { get; set; } = string.Empty;
    public bool RequiresCommittee { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string ConamiClassification { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RegisteredBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string ResolutionUser { get; set; } = string.Empty;
    public DateTime? ResolutionDate { get; set; }
    public long? CreditId { get; set; }
    public string CreditNumber { get; set; } = string.Empty;
    public DateTime? DisbursementDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? PrincipalBalance { get; set; }
    public string OperationalStatus { get; set; } = string.Empty;
    public string ProspectionStage { get; set; } = string.Empty;
    public string DiscardRejectReason { get; set; } = string.Empty;
    public string Promoter { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public DateTime? SystemDate { get; set; }
}

public sealed class CreditFileClientDto
{
    public long Id { get; set; }
    public string Identification { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ClientType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string HomeGeography { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string EconomicActivity { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public string FileStatus { get; set; } = string.Empty;
    public string SourceOfFunds { get; set; } = string.Empty;
    public string RelationshipPurpose { get; set; } = string.Empty;
    public bool IsPep { get; set; }
}

public sealed class CreditChecklistSnapshotDto
{
    public bool Identification { get; set; }
    public bool FileCompleted { get; set; }
    public bool HomeBusinessVisit { get; set; }
    public bool PaymentCapacity { get; set; }
    public bool ConamiReview { get; set; }
    public bool ListCheck { get; set; }
    public bool GuaranteeReview { get; set; }
}

public sealed class CreditFileReferenceDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}

public sealed class CreditFileReferencesDto
{
    public CreditFileReferenceDto Personal { get; set; } = new();
    public CreditFileReferenceDto Commercial { get; set; } = new();
    public CreditFileReferenceDto Financial { get; set; } = new();
}

public sealed class CreditFileVisitDto
{
    public DateTime? Date { get; set; }
    public string Result { get; set; } = string.Empty;
    public string Observation { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
}

public sealed class CreditFileVisitsDto
{
    public CreditFileVisitDto Home { get; set; } = new();
    public CreditFileVisitDto Business { get; set; } = new();
}

public sealed class CreditFileBureauDto
{
    public bool Consulted { get; set; }
    public string BureauName { get; set; } = "SIN_RIESGO";
    public DateTime? ConsultationDate { get; set; }
    public string ReportNumber { get; set; } = string.Empty;
    public string Result { get; set; } = "SIN_CONSULTA";
    public int Score { get; set; }
    public string Classification { get; set; } = string.Empty;
    public decimal ExternalDebt { get; set; }
    public decimal ExternalInstallment { get; set; }
    public decimal InternalDebt { get; set; }
    public decimal InternalInstallment { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal RequestedInstallment { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal TotalInstallment { get; set; }
    public decimal PaymentCapacity { get; set; }
    public decimal DebtCapacityRatio { get; set; }
    public List<string> Alerts { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

public sealed class CreditFileApprovalDto
{
    public DateTime ApprovalDate { get; set; }
    public decimal ApprovedAmount { get; set; }
    public int TermMonths { get; set; }
    public decimal AnnualRate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public sealed class CreditFilePlanRowDto
{
    public int Number { get; set; }
    public DateTime DueDate { get; set; }
    public int InterestDays { get; set; }
    public decimal Capital { get; set; }
    public decimal Interest { get; set; }
    public decimal Commission { get; set; }
    public decimal Sliding { get; set; }
    public decimal Mora { get; set; }
    public decimal Total { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CreditFileSummaryDto
{
    public decimal PlanTotal { get; set; }
    public decimal PlanInterest { get; set; }
    public decimal FirstInstallment { get; set; }
    public decimal PaymentCapacityRatio { get; set; }
    public decimal GuaranteeCoverage { get; set; }
    public List<string> MissingChecklistItems { get; set; } = new();
}
