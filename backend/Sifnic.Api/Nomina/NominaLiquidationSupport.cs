using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Controllers;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Nomina;

public static class NominaLiquidationSupport
{
    private static readonly HashSet<string> IndefiniteContractCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIJO",
        "INDETERMINADO",
        "INDETERMINADA",
    };

    private static readonly List<LiquidationCauseDescriptor> Causes =
    [
        new()
        {
            Code = "RENUNCIA_ART44",
            Label = "Renuncia con preaviso",
            Reference = "Arts. 43, 44 y 45 CT",
            Description = "Liquida prestaciones y reconoce antiguedad cuando existe preaviso.",
            GrantsIndemnization = true,
            RequiresIndefiniteContract = true,
        },
        new()
        {
            Code = "RENUNCIA_SIN_PREAVISO",
            Label = "Renuncia sin preaviso",
            Reference = "Art. 44 CT",
            Description = "Liquida prestaciones causadas, sin aplicar indemnizacion por antiguedad en esta parametrizacion.",
            GrantsIndemnization = false,
            RequiresIndefiniteContract = false,
        },
        new()
        {
            Code = "MUTUO_ACUERDO_ART43",
            Label = "Mutuo acuerdo",
            Reference = "Arts. 43 y 45 CT",
            Description = "Se liquida la terminacion por mutuo acuerdo con antiguedad cuando corresponde.",
            GrantsIndemnization = true,
            RequiresIndefiniteContract = true,
        },
        new()
        {
            Code = "DESPIDO_INJUSTIFICADO_ART45",
            Label = "Despido sin causa justificada",
            Reference = "Art. 45 CT",
            Description = "Aplica indemnizacion por antiguedad, mas vacaciones, aguinaldo y salario pendiente.",
            GrantsIndemnization = true,
            RequiresIndefiniteContract = true,
        },
        new()
        {
            Code = "DESPIDO_CON_CAUSA_ART48",
            Label = "Despido con causa",
            Reference = "Art. 48 CT",
            Description = "No reconoce indemnizacion art. 45; solo prestaciones legales causadas.",
            GrantsIndemnization = false,
            RequiresIndefiniteContract = false,
        },
        new()
        {
            Code = "FIN_CONTRATO_TEMPORAL",
            Label = "Fin de contrato temporal",
            Reference = "Terminacion por vencimiento de plazo",
            Description = "Liquida prestaciones causadas sin indemnizacion art. 45.",
            GrantsIndemnization = false,
            RequiresIndefiniteContract = false,
        },
    ];

    public static IReadOnlyList<object> BuildLiquidationCauses()
    {
        return Causes
            .Select(cause => new
            {
                code = cause.Code,
                label = cause.Label,
                reference = cause.Reference,
                description = cause.Description,
            })
            .Cast<object>()
            .ToList();
    }

    public static string GetLiquidationCauseLabel(string? code)
    {
        return ResolveCause(code).Label;
    }

    public static LiquidationReasonData ParseLiquidationReason(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new LiquidationReasonData
            {
                Code = "RENUNCIA_ART44",
                Note = string.Empty,
            };
        }

        var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && Causes.Any(cause => cause.Code.Equals(parts[0], StringComparison.OrdinalIgnoreCase)))
        {
            return new LiquidationReasonData
            {
                Code = parts[0].Trim().ToUpperInvariant(),
                Note = parts[1].Trim(),
            };
        }

        return new LiquidationReasonData
        {
            Code = "RENUNCIA_ART44",
            Note = raw.Trim(),
        };
    }

    public static string ComposeLiquidationReason(string? causeCode, string? note)
    {
        var normalizedCode = ResolveCause(causeCode).Code;
        var trimmedNote = (note ?? string.Empty).Trim();
        var composed = string.IsNullOrWhiteSpace(trimmedNote)
            ? normalizedCode
            : $"{normalizedCode}|{trimmedNote}";

        return composed.Length <= 200 ? composed : composed[..200];
    }

    public static LiquidationPreviewDto? BuildPreview(
        SqlConnection connection,
        SqlTransaction? transaction,
        LiquidationPreviewRequest model)
    {
        if (!DateTime.TryParse(model.FechaLiquidacion, out var fechaLiquidacion) ||
            !DateTime.TryParse(model.FechaBaja, out var fechaBaja))
        {
            return null;
        }

        var employee = GetEmployeeLiquidationBase(connection, transaction, model.IdEmpleado);
        if (employee is null)
        {
            return null;
        }

        if (fechaBaja.Date < employee.FechaIngreso.Date)
        {
            throw new InvalidOperationException("La fecha de baja no puede ser menor a la fecha de ingreso del colaborador.");
        }

        var cause = ResolveCause(model.CausalCodigo);
        var companyConfig = GetCompanyConfig(connection, transaction, fechaBaja.Date);
        var salaryDaily = RoundMoney(employee.SalarioMensual / companyConfig.DiasMesNomina);
        var pendingSalaryDays = model.DiasSalarioPendiente.HasValue
            ? RrhhSupport.RoundDays(model.DiasSalarioPendiente.Value)
            : GetDefaultPendingSalaryDays(connection, transaction, employee.IdEmpleado, employee.FechaIngreso, fechaBaja.Date, companyConfig.DiasMesNomina);
        var pendingSalaryAmount = RoundMoney(salaryDaily * pendingSalaryDays);

        var vacationSnapshot = RrhhSupport.CalculateVacationBalance(connection, transaction, employee.IdEmpleado, fechaBaja.Date);
        var vacationDays = vacationSnapshot.AcumulaVacaciones
            ? RrhhSupport.RoundDays(Math.Max(vacationSnapshot.DiasDisponibles, 0m))
            : 0m;
        var vacationAmount = RoundMoney(salaryDaily * vacationDays);

        var aguinaldoCycleStart = GetAguinaldoCycleStart(fechaBaja.Date);
        var aguinaldoStart = employee.FechaIngreso.Date > aguinaldoCycleStart ? employee.FechaIngreso.Date : aguinaldoCycleStart;
        var aguinaldoBaseDays = fechaBaja.Date >= aguinaldoStart
            ? CalculateCommercialDaysInclusive(aguinaldoStart, fechaBaja.Date)
            : 0m;
        var aguinaldoDays = RrhhSupport.RoundDays(aguinaldoBaseDays / 12m);
        var aguinaldoAmount = RoundMoney(salaryDaily * aguinaldoDays);

        var serviceCommercialDays = CalculateCommercialDaysInclusive(employee.FechaIngreso.Date, fechaBaja.Date);
        var indemnizationDays = CalculateIndemnizationDays(cause, employee.CodigoTipoContrato, serviceCommercialDays);
        var indemnizationAmount = RoundMoney(salaryDaily * indemnizationDays);

        var taxableIncome = RoundMoney(pendingSalaryAmount + vacationAmount);
        var nonTaxableIncome = RoundMoney(aguinaldoAmount + indemnizationAmount);
        var inssLaboral = companyConfig.CodigoInssLaboral is null
            ? 0m
            : ExecuteContribution(connection, transaction, companyConfig.CodigoInssLaboral, fechaBaja.Date, taxableIncome);
        var inssPatronal = companyConfig.CodigoInssPatronal is null
            ? 0m
            : ExecuteContribution(connection, transaction, companyConfig.CodigoInssPatronal, fechaBaja.Date, taxableIncome);
        var inatecPatronal = ExecuteContribution(connection, transaction, "INATEC_PATRONAL", fechaBaja.Date, taxableIncome, allowMissing: true);

        var irBase = Math.Max(taxableIncome - inssLaboral, 0m);
        var irLaboral = ExecuteLaborIr(connection, transaction, fechaBaja.Date, irBase);
        var totalIngresos = RoundMoney(taxableIncome + nonTaxableIncome);
        var totalDeducciones = RoundMoney(inssLaboral + irLaboral);
        var neto = RoundMoney(totalIngresos - totalDeducciones);

        var notes = new List<string>
        {
            $"Causal aplicada: {cause.Label} ({cause.Reference}).",
            "Prestaciones gravables: salario pendiente y vacaciones por pagar.",
            "Prestaciones no gravables: aguinaldo proporcional e indemnizacion art. 45 cuando corresponda.",
        };

        if (model.DiasSalarioPendiente.HasValue)
        {
            notes.Add("Los dias de salario pendiente fueron ingresados manualmente en la revision.");
        }
        else
        {
            notes.Add("Los dias de salario pendiente se estimaron desde el ultimo periodo de nomina pagado del colaborador.");
        }

        if (!vacationSnapshot.AcumulaVacaciones && !string.IsNullOrWhiteSpace(vacationSnapshot.MotivoNoAcumulacion))
        {
            notes.Add(vacationSnapshot.MotivoNoAcumulacion!);
        }

        if (cause.GrantsIndemnization && cause.RequiresIndefiniteContract && !IndefiniteContractCodes.Contains(employee.CodigoTipoContrato))
        {
            notes.Add("La causal permite antiguedad, pero el contrato vigente no es indeterminado/fijo; no se calculo indemnizacion art. 45.");
        }

        var lines = new List<LiquidationLineDto>();
        AddLine(lines, "PRESTACIONES_GRAVABLES", "Prestaciones gravables", "LIQ_SALARIO_PENDIENTE", "Salario pendiente", pendingSalaryDays, pendingSalaryAmount, $"{pendingSalaryDays:0.##} dia(s) pendientes", "INGRESO", 10);
        AddLine(lines, "PRESTACIONES_GRAVABLES", "Prestaciones gravables", "LIQ_VACACIONES_POR_PAGAR", "Vacaciones por pagar", vacationDays, vacationAmount, $"{vacationDays:0.##} dia(s) disponibles", "INGRESO", 20);
        AddLine(lines, "PRESTACIONES_NO_GRAVABLES", "Prestaciones no gravables", "LIQ_AGUINALDO_PROPORCIONAL", "Aguinaldo proporcional", aguinaldoDays, aguinaldoAmount, $"Base desde {aguinaldoStart:dd/MM/yyyy} hasta {fechaBaja:dd/MM/yyyy}", "INGRESO", 30);
        AddLine(
            lines,
            "PRESTACIONES_NO_GRAVABLES",
            "Prestaciones no gravables",
            "LIQ_INDEMNIZACION_ART45",
            "Indemnizacion art. 45",
            indemnizationDays,
            indemnizationAmount,
            indemnizationDays > 0m
                ? $"{cause.Reference} | {indemnizationDays:0.##} dia(s) equivalentes"
                : cause.Reference,
            "INGRESO",
            40);
        AddLine(lines, "DEDUCCIONES", "Deducciones", "INSS_LABORAL", "INSS laboral", 0m, inssLaboral, "Sobre prestaciones gravables", "DEDUCCION", 50);
        AddLine(lines, "DEDUCCIONES", "Deducciones", "IR_LABORAL", "IR laboral", 0m, irLaboral, "Calculado con tabla IR laboral vigente", "DEDUCCION", 60);
        AddLine(lines, "APORTES_PATRONALES", "Aportes patronales", "INSS_PATRONAL", "INSS patronal", 0m, inssPatronal, companyConfig.CodigoInssPatronal ?? "Sin codigo patronal", "APORTE_PATRONAL", 70);
        AddLine(lines, "APORTES_PATRONALES", "Aportes patronales", "INATEC_PATRONAL", "INATEC (2%)", 0m, inatecPatronal, "Parametro patronal vigente", "APORTE_PATRONAL", 80);

        return new LiquidationPreviewDto
        {
            Persisted = false,
            Header = new LiquidationHeaderDto
            {
                IdEmpleado = employee.IdEmpleado,
                IdContrato = employee.IdContrato,
                CodigoEmpleado = employee.CodigoEmpleado,
                NombreEmpleado = employee.NombreEmpleado,
                Nombres = employee.Nombres,
                Apellidos = employee.Apellidos,
                Correo = employee.Correo,
                Cedula = employee.Cedula,
                Inss = employee.Inss,
                Departamento = employee.Departamento,
                Cargo = employee.Cargo,
                CodigoTipoContrato = employee.CodigoTipoContrato,
                NombreTipoContrato = employee.NombreTipoContrato,
                Moneda = employee.Moneda,
                FechaIngreso = employee.FechaIngreso.Date,
                FechaBaja = fechaBaja.Date,
                FechaLiquidacion = fechaLiquidacion.Date,
                TiempoLaborado = FormatServiceDuration(employee.FechaIngreso.Date, fechaBaja.Date),
                SalarioMensual = employee.SalarioMensual,
                SalarioDiario = salaryDaily,
                SalarioPromedio = employee.SalarioMensual,
                MotivoRetiro = (model.MotivoLiquidacion ?? string.Empty).Trim(),
            },
            Cause = new LiquidationCauseSummaryDto
            {
                Code = cause.Code,
                Label = cause.Label,
                Reference = cause.Reference,
                Description = cause.Description,
            },
            TaxableSection = new LiquidationTaxableSectionDto
            {
                PendingSalaryDays = pendingSalaryDays,
                PendingSalaryAmount = pendingSalaryAmount,
                VacationDays = vacationDays,
                VacationAmount = vacationAmount,
                TaxableSubtotal = taxableIncome,
            },
            NonTaxableSection = new LiquidationNonTaxableSectionDto
            {
                AguinaldoDays = aguinaldoDays,
                AguinaldoAmount = aguinaldoAmount,
                IndemnizationDays = indemnizationDays,
                IndemnizationAmount = indemnizationAmount,
                NonTaxableSubtotal = nonTaxableIncome,
            },
            Deductions = new LiquidationDeductionSectionDto
            {
                InssLaboral = inssLaboral,
                IrLaboral = irLaboral,
                TotalDeductions = totalDeducciones,
            },
            EmployerContributions = new LiquidationEmployerContributionSectionDto
            {
                InssPatronal = inssPatronal,
                InatecPatronal = inatecPatronal,
                TotalEmployerContributions = RoundMoney(inssPatronal + inatecPatronal),
            },
            Totals = new LiquidationTotalsDto
            {
                TotalIngresos = totalIngresos,
                TotalDeducciones = totalDeducciones,
                NetoLiquidacion = neto,
            },
            Lines = lines.Where(line => line.Amount > 0m).OrderBy(line => line.SortOrder).ToList(),
            Notes = notes,
        };
    }

    public static LiquidationPreviewDto? BuildDetail(SqlConnection connection, long idLiquidacion)
    {
        const string sql = """
            SELECT
                l.id_liquidacion,
                l.id_empleado,
                l.id_contrato,
                l.fecha_liquidacion,
                l.fecha_baja,
                l.motivo_liquidacion,
                l.salario_base_referencia,
                l.total_ingresos,
                l.total_deducciones,
                l.neto_liquidacion,
                l.usuario_registro,
                l.fecha_registro,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.nombres,
                e.apellidos,
                e.correo,
                e.cedula,
                e.inss,
                e.fecha_ingreso,
                d.nombre_departamento,
                cg.nombre_cargo,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato,
                c.moneda
            FROM nomina.liquidacion l
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = l.id_empleado
            INNER JOIN rrhh.contrato c
                ON c.id_contrato = l.id_contrato
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo cg
                ON cg.id_cargo = e.id_cargo
            WHERE l.id_liquidacion = @id_liquidacion;

            SELECT
                cn.codigo_concepto,
                cn.nombre_concepto,
                tcn.codigo_tipo_concepto,
                ld.monto,
                ld.referencia,
                cn.orden_visual
            FROM nomina.liquidacion_detalle ld
            INNER JOIN nomina.concepto_nomina cn
                ON cn.id_concepto_nomina = ld.id_concepto_nomina
            INNER JOIN nomina.tipo_concepto_nomina tcn
                ON tcn.id_tipo_concepto_nomina = cn.id_tipo_concepto_nomina
            WHERE ld.id_liquidacion = @id_liquidacion
            ORDER BY cn.orden_visual, ld.id_liquidacion_detalle;

            SELECT
                COALESCE(NULLIF(nomina.fn_obtener_parametro_decimal(N'DIAS_MES_NOMINA'), 0), 30) AS dias_mes_nomina;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_liquidacion", SqlDbType.BigInt).Value = idLiquidacion;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var reason = ParseLiquidationReason(reader.IsDBNull(5) ? null : reader.GetString(5));
        var cause = ResolveCause(reason.Code);
        var payload = new LiquidationPreviewDto
        {
            Persisted = true,
            IdLiquidacion = reader.GetInt64(0),
            Header = new LiquidationHeaderDto
            {
                IdLiquidacion = reader.GetInt64(0),
                IdEmpleado = reader.GetInt64(1),
                IdContrato = reader.GetInt64(2),
                FechaLiquidacion = reader.GetDateTime(3),
                FechaBaja = reader.GetDateTime(4),
                MotivoRetiro = reason.Note,
                SalarioMensual = reader.GetDecimal(6),
                SalarioPromedio = reader.GetDecimal(6),
                CodigoEmpleado = reader.GetString(12),
                NombreEmpleado = reader.GetString(13),
                Nombres = reader.GetString(14),
                Apellidos = reader.GetString(15),
                Correo = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                Cedula = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                Inss = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                FechaIngreso = reader.GetDateTime(19),
                Departamento = reader.GetString(20),
                Cargo = reader.GetString(21),
                CodigoTipoContrato = reader.GetString(22),
                NombreTipoContrato = reader.GetString(23),
                Moneda = reader.IsDBNull(24) ? "NIO" : reader.GetString(24),
            },
            Cause = new LiquidationCauseSummaryDto
            {
                Code = cause.Code,
                Label = cause.Label,
                Reference = cause.Reference,
                Description = cause.Description,
            },
            Totals = new LiquidationTotalsDto
            {
                TotalIngresos = reader.GetDecimal(7),
                TotalDeducciones = reader.GetDecimal(8),
                NetoLiquidacion = reader.GetDecimal(9),
            },
            Notes =
            [
                $"Liquidacion registrada por {reader.GetString(10)} el {reader.GetDateTime(11):dd/MM/yyyy HH:mm}.",
                $"Causal aplicada: {cause.Label} ({cause.Reference}).",
            ],
        };

        reader.NextResult();
        while (reader.Read())
        {
            payload.Lines.Add(new LiquidationLineDto
            {
                GroupCode = MapGroupCode(reader.GetString(0)),
                GroupLabel = MapGroupLabel(reader.GetString(0)),
                ConceptCode = reader.GetString(0),
                ConceptName = reader.GetString(1),
                ConceptType = reader.GetString(2),
                Amount = reader.GetDecimal(3),
                Reference = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Days = ExtractDays(reader.IsDBNull(4) ? null : reader.GetString(4)),
                SortOrder = reader.GetInt32(5),
            });
        }

        reader.NextResult();
        var diasMesNomina = 30m;
        if (reader.Read())
        {
            diasMesNomina = reader.IsDBNull(0) ? 30m : reader.GetDecimal(0);
        }

        payload.Header.TiempoLaborado = FormatServiceDuration(payload.Header.FechaIngreso, payload.Header.FechaBaja);
        payload.Header.SalarioDiario = diasMesNomina <= 0m
            ? payload.Header.SalarioMensual
            : RoundMoney(payload.Header.SalarioMensual / diasMesNomina);

        payload.TaxableSection = new LiquidationTaxableSectionDto
        {
            PendingSalaryDays = GetLineDays(payload, "LIQ_SALARIO_PENDIENTE"),
            PendingSalaryAmount = GetLineAmount(payload, "LIQ_SALARIO_PENDIENTE"),
            VacationDays = GetLineDays(payload, "LIQ_VACACIONES_POR_PAGAR"),
            VacationAmount = GetLineAmount(payload, "LIQ_VACACIONES_POR_PAGAR"),
        };
        payload.TaxableSection.TaxableSubtotal = RoundMoney(payload.TaxableSection.PendingSalaryAmount + payload.TaxableSection.VacationAmount);

        payload.NonTaxableSection = new LiquidationNonTaxableSectionDto
        {
            AguinaldoDays = GetLineDays(payload, "LIQ_AGUINALDO_PROPORCIONAL"),
            AguinaldoAmount = GetLineAmount(payload, "LIQ_AGUINALDO_PROPORCIONAL"),
            IndemnizationDays = GetLineDays(payload, "LIQ_INDEMNIZACION_ART45"),
            IndemnizationAmount = GetLineAmount(payload, "LIQ_INDEMNIZACION_ART45"),
        };
        payload.NonTaxableSection.NonTaxableSubtotal = RoundMoney(payload.NonTaxableSection.AguinaldoAmount + payload.NonTaxableSection.IndemnizationAmount);

        payload.Deductions = new LiquidationDeductionSectionDto
        {
            InssLaboral = GetLineAmount(payload, "INSS_LABORAL"),
            IrLaboral = GetLineAmount(payload, "IR_LABORAL"),
        };
        payload.Deductions.TotalDeductions = RoundMoney(payload.Deductions.InssLaboral + payload.Deductions.IrLaboral);

        payload.EmployerContributions = new LiquidationEmployerContributionSectionDto
        {
            InssPatronal = GetLineAmount(payload, "INSS_PATRONAL"),
            InatecPatronal = GetLineAmount(payload, "INATEC_PATRONAL"),
        };
        payload.EmployerContributions.TotalEmployerContributions =
            RoundMoney(payload.EmployerContributions.InssPatronal + payload.EmployerContributions.InatecPatronal);

        return payload;
    }

    public static void DeactivateRelatedSecurityUser(
        SqlConnection connection,
        SqlTransaction? transaction,
        LiquidationHeaderDto header)
    {
        const string sql = """
            SELECT TOP (1) id_usuario
            FROM seguridad.usuario
            WHERE
                (@correo <> N'' AND correo = @correo)
                OR (nombres = @nombres AND apellidos = @apellidos)
            ORDER BY
                CASE
                    WHEN @correo <> N'' AND correo = @correo THEN 0
                    ELSE 1
                END,
                id_usuario DESC;
            """;

        using var findCommand = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        findCommand.Parameters.Add("@correo", SqlDbType.NVarChar, 300).Value = header.Correo?.Trim() ?? string.Empty;
        findCommand.Parameters.Add("@nombres", SqlDbType.NVarChar, 300).Value = header.Nombres.Trim();
        findCommand.Parameters.Add("@apellidos", SqlDbType.NVarChar, 300).Value = header.Apellidos.Trim();

        var result = findCommand.ExecuteScalar();
        if (result is null || result == DBNull.Value)
        {
            return;
        }

        using var updateCommand = transaction is null
            ? new SqlCommand(
                """
                UPDATE seguridad.usuario
                SET
                    activo = 0,
                    bloqueado = 1,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_usuario = @id_usuario;
                """,
                connection)
            : new SqlCommand(
                """
                UPDATE seguridad.usuario
                SET
                    activo = 0,
                    bloqueado = 1,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_usuario = @id_usuario;
                """,
                connection,
                transaction);

        updateCommand.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = Convert.ToInt64(result, CultureInfo.InvariantCulture);
        updateCommand.ExecuteNonQuery();
    }

    public static string BuildLiquidationHtml(LiquidationPreviewDto payload, ReportBrandingDto branding)
    {
        var exportFileName = $"Liquidacion-{SanitizeFileNamePart(payload.Header.CodigoEmpleado)}-{payload.IdLiquidacion ?? 0}";
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Liquidacion final</title>");
        builder.AppendLine("""
            <style>
              @page { size: letter portrait; margin: 8mm 10mm; }
              :root { color-scheme: light; }
              * { box-sizing: border-box; }
              html, body { margin: 0; padding: 0; }
              body { font-family: Arial, Helvetica, sans-serif; background: #101822; color: #16202b; }
              .page { width: 100%; min-height: 100vh; padding: 18px; background: linear-gradient(180deg, #121b27, #0d141e); }
              .screen-actions { max-width: 980px; margin: 0 auto 12px; display: flex; justify-content: flex-end; gap: 10px; flex-wrap: wrap; }
              .screen-note { max-width: 980px; margin: 0 auto 10px; color: #aec0d5; font-size: 11px; text-align: right; }
              .action-button { min-height: 40px; padding: 0 16px; border-radius: 999px; border: 1px solid rgba(255,255,255,.14); background: rgba(255,255,255,.05); color: #f5f7fa; font: inherit; font-weight: 700; cursor: pointer; }
              .action-button.is-primary { border: 0; background: linear-gradient(135deg, #18c5b7 0%, #f4be63 100%); color: #041018; }
              .sheet { max-width: 980px; margin: 0 auto; background: #fff; border: 1px solid #c8d0d9; padding: 12mm 10mm 10mm; }
              .document-header { text-align: center; position: relative; padding-bottom: 10px; border-bottom: 1px solid #aeb8c3; min-height: 88px; }
              .document-header .logo-box { width: 70px; height: 56px; margin: 0 auto 5px; display: grid; place-items: center; overflow: hidden; }
              .document-header .logo-box img { width: 100%; height: 100%; object-fit: contain; }
              .logo-fallback { font-size: 24px; font-weight: 800; color: #5b6773; }
              .document-header h1 { margin: 0; font-size: 15px; letter-spacing: .02em; text-transform: uppercase; color: #26313c; }
              .document-header h2 { margin: 3px 0 0; font-size: 20px; line-height: 1.1; text-transform: uppercase; color: #111820; }
              .document-header .run-meta { position: absolute; right: 0; top: 2px; min-width: 180px; text-align: right; font-size: 10.5px; color: #314050; }
              .document-header .run-meta strong { font-size: 12px; color: #101820; }
              .document-header .run-meta .line { margin-top: 4px; }
              .info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-top: 10px; }
              .info-box { border: 1px solid #b7c0cb; }
              .box-head { padding: 5px 8px; background: #d6d9dd; color: #1e252d; font-weight: 700; font-size: 10.5px; text-transform: uppercase; border-bottom: 1px solid #b7c0cb; }
              .info-table { width: 100%; border-collapse: collapse; }
              .info-table td { padding: 4px 6px; font-size: 10.5px; border-bottom: 1px solid #e7ebef; vertical-align: top; line-height: 1.2; }
              .info-table tr:last-child td { border-bottom: none; }
              .info-table td.label { width: 38%; font-weight: 700; color: #465362; }
              .salary-strip { margin-top: 8px; border: 1px dashed #9ca9b5; width: 100%; border-collapse: collapse; }
              .salary-strip td { padding: 5px 6px; font-size: 10.5px; }
              .salary-strip .label { width: 28%; font-weight: 700; color: #3c4d5d; text-transform: uppercase; }
              .salary-strip .value { font-weight: 700; text-align: right; }
              .section-table { width: 100%; border-collapse: collapse; margin-top: 10px; page-break-inside: avoid; break-inside: avoid; }
              .section-table thead th { background: #757575; color: #fff; padding: 5px 6px; font-size: 10.5px; text-transform: uppercase; border: 1px solid #666; }
              .section-table td { border: 1px solid #cdd5dd; padding: 4px 6px; font-size: 10.5px; line-height: 1.2; }
              .section-table td.number, .section-table th.number { text-align: right; }
              .section-table .subtotal td { background: #f0f2f5; font-weight: 700; }
              .grand-total { display: grid; grid-template-columns: 1fr 220px; gap: 10px; align-items: start; margin-top: 10px; page-break-inside: avoid; break-inside: avoid; }
              .notes { border: 1px solid #c9d1da; padding: 8px 10px; min-height: 0; }
              .notes h3 { margin: 0 0 6px; font-size: 10.5px; text-transform: uppercase; color: #243344; }
              .notes ul { margin: 0; padding-left: 16px; font-size: 10.5px; line-height: 1.3; color: #334352; }
              .receipt-box { border: 2px solid #222b34; padding: 8px 10px; page-break-inside: avoid; break-inside: avoid; }
              .receipt-box .label { display: block; font-size: 10.5px; text-transform: uppercase; color: #4d5d6f; }
              .receipt-box .amount { display: block; margin-top: 6px; font-size: 22px; font-weight: 800; color: #0d1823; }
              .receipt-box .meta { display: block; margin-top: 4px; font-size: 10.5px; color: #4d5d6f; }
              .signature-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; margin-top: 18px; page-break-inside: avoid; break-inside: avoid; }
              .signature-box { min-height: 64px; display: flex; align-items: end; justify-content: center; padding-top: 12px; border-top: 1px solid #9099a3; text-align: center; font-size: 10.5px; color: #3b4c5d; }
              .footer { margin-top: 8px; text-align: center; font-size: 10px; color: #687786; }
              @media print {
                body { background: #fff; }
                .page { padding: 0; background: #fff; }
                .screen-actions, .screen-note { display: none !important; }
                .sheet { max-width: none; border: none; padding: 0; }
                .notes { display: none; }
                .document-header { min-height: auto; padding-bottom: 6px; }
                .document-header .logo-box { width: 54px; height: 42px; margin-bottom: 4px; }
                .document-header h1 { font-size: 13px; }
                .document-header h2 { font-size: 17px; }
                .document-header .run-meta { min-width: 150px; font-size: 9px; }
                .info-grid { gap: 8px; margin-top: 8px; }
                .box-head { padding: 4px 6px; font-size: 9.3px; }
                .info-table td,
                .salary-strip td,
                .section-table thead th,
                .section-table td,
                .receipt-box .label,
                .receipt-box .meta,
                .signature-box,
                .footer { font-size: 9.3px; }
                .salary-strip { margin-top: 6px; }
                .section-table { margin-top: 8px; }
                .grand-total { margin-top: 8px; grid-template-columns: 1fr; justify-items: end; page-break-inside: auto; break-inside: auto; }
                .receipt-box { width: 76mm; }
                .receipt-box .amount { font-size: 18px; }
                .signature-grid { margin-top: 12px; gap: 10px; }
                .signature-box { min-height: 46px; padding-top: 8px; }
                .footer { display: none; }
                .document-header, .info-grid, .salary-strip, .section-table, .grand-total, .signature-grid {
                  page-break-inside: avoid;
                  break-inside: avoid;
                }
              }
            </style>
            """);
        builder.AppendLine($"""
            </head><body>
            <div class="page">
              <div class="screen-actions">
                <button class="action-button" type="button" onclick="window.print()">Imprimir</button>
                <button class="action-button" type="button" onclick="exportExcel()">Generar Excel</button>
                <button class="action-button is-primary" type="button" onclick="exportPdf()">Generar PDF</button>
              </div>
              <div class="screen-note">Liquidacion final lista para impresion, PDF o exportacion a Excel despues de revisar calculos.</div>
              <section class="sheet">
            """);

        builder.AppendLine("<header class=\"document-header\">");
        builder.Append("<div class=\"logo-box\">");
        if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
        {
            builder.Append($"<img src=\"{WebUtility.HtmlEncode(branding.LogoUrl)}\" alt=\"Logo empresa\" />");
        }
        else
        {
            builder.Append($"<div class=\"logo-fallback\">{WebUtility.HtmlEncode((branding.CompanyName.Length >= 2 ? branding.CompanyName[..2] : "SI").ToUpperInvariant())}</div>");
        }

        builder.AppendLine("</div>");
        builder.AppendLine($"<h1>{WebUtility.HtmlEncode(branding.LegalName)}</h1>");
        builder.AppendLine("<h2>Gerencia de Recursos Humanos<br />Liquidacion final</h2>");
        builder.AppendLine($"""
            <div class="run-meta">
              <div><strong>Liquidacion: {WebUtility.HtmlEncode((payload.IdLiquidacion?.ToString() ?? "Previo").PadLeft(2, '0'))}</strong></div>
              <div class="line">Fecha elaboracion: {payload.Header.FechaLiquidacion:dd-MMM-yy}</div>
              <div class="line">Fecha egreso: {payload.Header.FechaBaja:dd-MMM-yy}</div>
            </div>
            """);
        builder.AppendLine("</header>");

        builder.AppendLine("<section class=\"info-grid\">");
        builder.AppendLine("<article class=\"info-box\">");
        builder.AppendLine("<div class=\"box-head\">Datos del empleado</div>");
        builder.AppendLine("<table class=\"info-table\">");
        AppendInfoRow(builder, "Nombre y apellidos", payload.Header.NombreEmpleado);
        AppendInfoRow(builder, "Cargo", payload.Header.Cargo);
        AppendInfoRow(builder, "Fecha de ingreso", payload.Header.FechaIngreso.ToString("dd/MM/yyyy"));
        AppendInfoRow(builder, "Fecha de egreso", payload.Header.FechaBaja.ToString("dd/MM/yyyy"));
        AppendInfoRow(builder, "Motivo del retiro", payload.Cause.Label);
        AppendInfoRow(builder, "Detalle", payload.Header.MotivoRetiro);
        AppendInfoRow(builder, "Tipo de contrato", payload.Header.NombreTipoContrato);
        AppendInfoRow(builder, "Salario basico", FormatCurrency(payload.Header.SalarioMensual, payload.Header.Moneda));
        AppendInfoRow(builder, "Salario promedio", FormatCurrency(payload.Header.SalarioPromedio, payload.Header.Moneda));
        builder.AppendLine("</table>");
        builder.AppendLine("</article>");

        builder.AppendLine("<article class=\"info-box\">");
        builder.AppendLine("<div class=\"box-head\">Datos laborales</div>");
        builder.AppendLine("<table class=\"info-table\">");
        AppendInfoRow(builder, "Tiempo laborado", payload.Header.TiempoLaborado);
        AppendInfoRow(builder, "Unidad administrativa", payload.Header.Departamento);
        AppendInfoRow(builder, "Codigo empleado", payload.Header.CodigoEmpleado);
        AppendInfoRow(builder, "No. INSS empleado", payload.Header.Inss);
        AppendInfoRow(builder, "No. cedula", payload.Header.Cedula);
        builder.AppendLine("</table>");
        builder.AppendLine("<table class=\"salary-strip\">");
        builder.AppendLine($"<tr><td class=\"label\">Salario mensual</td><td class=\"value\">{WebUtility.HtmlEncode(FormatCurrency(payload.Header.SalarioMensual, payload.Header.Moneda))}</td></tr>");
        builder.AppendLine($"<tr><td class=\"label\">Salario diario</td><td class=\"value\">{WebUtility.HtmlEncode(FormatCurrency(payload.Header.SalarioDiario, payload.Header.Moneda))}</td></tr>");
        builder.AppendLine("</table>");
        builder.AppendLine("</article>");
        builder.AppendLine("</section>");

        BuildSectionTable(
            builder,
            "Prestaciones (ingresos gravables de renta)",
            payload,
            ["LIQ_SALARIO_PENDIENTE", "LIQ_VACACIONES_POR_PAGAR"],
            payload.TaxableSection.TaxableSubtotal,
            payload.Header.Moneda);

        BuildSectionTable(
            builder,
            "Otras prestaciones (ingresos no gravables de renta)",
            payload,
            ["LIQ_AGUINALDO_PROPORCIONAL", "LIQ_INDEMNIZACION_ART45"],
            payload.NonTaxableSection.NonTaxableSubtotal,
            payload.Header.Moneda);

        builder.AppendLine("<table class=\"section-table\">");
        builder.AppendLine("<thead><tr><th colspan=\"3\">Total prestaciones</th><th class=\"number\">Monto</th></tr></thead>");
        builder.AppendLine("<tbody>");
        builder.AppendLine($"<tr class=\"subtotal\"><td colspan=\"3\">Total prestaciones</td><td class=\"number\">{WebUtility.HtmlEncode(FormatCurrency(payload.Totals.TotalIngresos, payload.Header.Moneda))}</td></tr>");
        builder.AppendLine("</tbody></table>");

        BuildSectionTable(
            builder,
            "Deducciones",
            payload,
            ["INSS_LABORAL", "IR_LABORAL"],
            payload.Deductions.TotalDeductions,
            payload.Header.Moneda);

        builder.AppendLine("<section class=\"grand-total\">");
        builder.AppendLine("<article class=\"notes\">");
        builder.AppendLine("<h3>Notas de calculo</h3><ul>");
        foreach (var note in payload.Notes)
        {
            builder.AppendLine($"<li>{WebUtility.HtmlEncode(note)}</li>");
        }

        if (branding.LogoPending)
        {
            builder.AppendLine("<li>El logo corporativo del reporte queda pendiente de configuracion.</li>");
        }

        builder.AppendLine("</ul></article>");
        builder.AppendLine("<article class=\"receipt-box\">");
        builder.AppendLine("<span class=\"label\">Total deducciones</span>");
        builder.AppendLine($"<span class=\"meta\">{WebUtility.HtmlEncode(FormatCurrency(payload.Totals.TotalDeducciones, payload.Header.Moneda))}</span>");
        builder.AppendLine("<span class=\"label\" style=\"margin-top:10px;\">Total a recibir</span>");
        builder.AppendLine($"<span class=\"amount\">{WebUtility.HtmlEncode(FormatCurrency(payload.Totals.NetoLiquidacion, payload.Header.Moneda))}</span>");
        builder.AppendLine("</article>");
        builder.AppendLine("</section>");

        BuildSectionTable(
            builder,
            "Aportes patronales",
            payload,
            ["INSS_PATRONAL", "INATEC_PATRONAL"],
            payload.EmployerContributions.TotalEmployerContributions,
            payload.Header.Moneda);

        builder.AppendLine("<section class=\"signature-grid\">");
        builder.AppendLine("<div class=\"signature-box\">Elaborada por<br />Recursos Humanos</div>");
        builder.AppendLine("<div class=\"signature-box\">Revisada por<br />Contabilidad</div>");
        builder.AppendLine("<div class=\"signature-box\">Autorizada por<br />________________________</div>");
        builder.AppendLine("</section>");
        builder.AppendLine($"<div class=\"footer\">{WebUtility.HtmlEncode(branding.FooterText)}</div>");
        builder.AppendLine("</section></div>");
        builder.AppendLine("<script>");
        builder.AppendLine("const originalTitle = document.title;");
        builder.AppendLine("function exportExcel() {");
        builder.AppendLine("  const excelUrl = `${window.location.pathname.replace('LiquidacionHtml', 'LiquidacionExcel')}${window.location.search}`;");
        builder.AppendLine("  window.location.href = excelUrl;");
        builder.AppendLine("}");
        builder.AppendLine("function exportPdf() {");
        builder.AppendLine($"  document.title = \"{WebUtility.HtmlEncode(exportFileName)}\";");
        builder.AppendLine("  window.print();");
        builder.AppendLine("  window.setTimeout(() => { document.title = originalTitle; }, 400);");
        builder.AppendLine("}");
        builder.AppendLine("</script>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string BuildLiquidationExcel(LiquidationPreviewDto payload, ReportBrandingDto branding)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
        builder.AppendLine("""
            <!--[if gte mso 9]>
            <xml>
              <x:ExcelWorkbook>
                <x:ExcelWorksheets>
                  <x:ExcelWorksheet>
                    <x:Name>Liquidacion</x:Name>
                    <x:WorksheetOptions>
                      <x:DisplayGridlines/>
                    </x:WorksheetOptions>
                  </x:ExcelWorksheet>
                </x:ExcelWorksheets>
              </x:ExcelWorkbook>
            </xml>
            <![endif]-->
            """);
        builder.AppendLine("""
            <style>
              body { font-family: Arial, sans-serif; margin: 18px; color: #15212d; }
              table { border-collapse: collapse; width: 100%; }
              td, th { border: 1px solid #b7c4d3; padding: 7px 9px; font-size: 12px; vertical-align: top; }
              .no-border td { border: none; padding: 2px 0; }
              .title { font-size: 18px; font-weight: 700; text-transform: uppercase; color: #1b2a38; }
              .subtitle { font-size: 13px; color: #4e6378; }
              .box-head { background: #d7dbdf; color: #1f2730; font-weight: 700; text-transform: uppercase; }
              .section-head { background: #757575; color: #fff; font-weight: 700; text-transform: uppercase; }
              .subtotal { background: #f0f2f5; font-weight: 700; }
              .right { text-align: right; }
              .center { text-align: center; }
              .label { font-weight: 700; color: #415263; width: 28%; }
              .section-gap { height: 10px; }
            </style>
            """);
        builder.AppendLine("</head><body>");

        builder.AppendLine("<table class=\"no-border\">");
        builder.AppendLine($"<tr><td class=\"title\">{WebUtility.HtmlEncode(branding.LegalName)}</td><td class=\"right subtitle\">Liquidacion: {WebUtility.HtmlEncode((payload.IdLiquidacion?.ToString() ?? "Previo").PadLeft(2, '0'))}</td></tr>");
        builder.AppendLine("<tr><td class=\"title\">Gerencia de Recursos Humanos - Liquidacion final</td>");
        builder.AppendLine($"<td class=\"right subtitle\">Fecha elaboracion: {payload.Header.FechaLiquidacion:dd/MM/yyyy}</td></tr>");
        builder.AppendLine("</table>");

        builder.AppendLine("<div class=\"section-gap\"></div>");
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><td class=\"box-head\" colspan=\"2\">Datos del empleado</td><td class=\"box-head\" colspan=\"2\">Datos laborales</td></tr>");
        AppendExcelInfoRow(builder, "Nombre y apellidos", payload.Header.NombreEmpleado, "Tiempo laborado", payload.Header.TiempoLaborado);
        AppendExcelInfoRow(builder, "Cargo", payload.Header.Cargo, "Unidad administrativa", payload.Header.Departamento);
        AppendExcelInfoRow(builder, "Fecha de ingreso", payload.Header.FechaIngreso.ToString("dd/MM/yyyy"), "Codigo empleado", payload.Header.CodigoEmpleado);
        AppendExcelInfoRow(builder, "Fecha de egreso", payload.Header.FechaBaja.ToString("dd/MM/yyyy"), "No. INSS empleado", payload.Header.Inss);
        AppendExcelInfoRow(builder, "Motivo del retiro", payload.Cause.Label, "No. cedula", payload.Header.Cedula);
        AppendExcelInfoRow(builder, "Tipo de contrato", payload.Header.NombreTipoContrato, "Moneda", payload.Header.Moneda);
        AppendExcelInfoRow(builder, "Salario basico", FormatCurrency(payload.Header.SalarioMensual, payload.Header.Moneda), "Salario diario", FormatCurrency(payload.Header.SalarioDiario, payload.Header.Moneda));
        AppendExcelInfoRow(builder, "Salario promedio", FormatCurrency(payload.Header.SalarioPromedio, payload.Header.Moneda), "Detalle", payload.Header.MotivoRetiro);
        builder.AppendLine("</table>");

        AppendLiquidationExcelSection(
            builder,
            "Prestaciones (ingresos gravables de renta)",
            payload,
            ["LIQ_SALARIO_PENDIENTE", "LIQ_VACACIONES_POR_PAGAR"],
            payload.TaxableSection.TaxableSubtotal,
            payload.Header.Moneda);

        AppendLiquidationExcelSection(
            builder,
            "Otras prestaciones (ingresos no gravables de renta)",
            payload,
            ["LIQ_AGUINALDO_PROPORCIONAL", "LIQ_INDEMNIZACION_ART45"],
            payload.NonTaxableSection.NonTaxableSubtotal,
            payload.Header.Moneda);

        builder.AppendLine("<div class=\"section-gap\"></div>");
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><td class=\"section-head\" colspan=\"3\">Total prestaciones</td><td class=\"section-head right\">Monto</td></tr>");
        builder.AppendLine($"<tr class=\"subtotal\"><td colspan=\"3\">Total prestaciones</td><td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency(payload.Totals.TotalIngresos, payload.Header.Moneda))}</td></tr>");
        builder.AppendLine("</table>");

        AppendLiquidationExcelSection(
            builder,
            "Deducciones",
            payload,
            ["INSS_LABORAL", "IR_LABORAL"],
            payload.Deductions.TotalDeductions,
            payload.Header.Moneda);

        AppendLiquidationExcelSection(
            builder,
            "Aportes patronales",
            payload,
            ["INSS_PATRONAL", "INATEC_PATRONAL"],
            payload.EmployerContributions.TotalEmployerContributions,
            payload.Header.Moneda);

        builder.AppendLine("<div class=\"section-gap\"></div>");
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><td class=\"section-head\" colspan=\"3\">Total deducciones</td><td class=\"section-head right\">Monto</td></tr>");
        builder.AppendLine($"<tr class=\"subtotal\"><td colspan=\"3\">Total deducciones</td><td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency(payload.Totals.TotalDeducciones, payload.Header.Moneda))}</td></tr>");
        builder.AppendLine("<tr><td class=\"section-head\" colspan=\"3\">Total a recibir</td><td class=\"section-head right\">Monto</td></tr>");
        builder.AppendLine($"<tr class=\"subtotal\"><td colspan=\"3\">Total a recibir</td><td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency(payload.Totals.NetoLiquidacion, payload.Header.Moneda))}</td></tr>");
        builder.AppendLine("</table>");

        builder.AppendLine("<div class=\"section-gap\"></div>");
        builder.AppendLine("<table>");
        builder.AppendLine("<tr><td class=\"box-head\">Notas</td></tr>");
        builder.AppendLine("<tr><td>");
        builder.AppendLine("<ul>");
        foreach (var note in payload.Notes)
        {
            builder.AppendLine($"<li>{WebUtility.HtmlEncode(note)}</li>");
        }
        if (branding.LogoPending)
        {
            builder.AppendLine("<li>El logo corporativo del reporte queda pendiente de configuracion.</li>");
        }
        builder.AppendLine("</ul>");
        builder.AppendLine("</td></tr>");
        builder.AppendLine("</table>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    public static string BuildRecommendationLetterHtml(LiquidationPreviewDto payload, ReportBrandingDto branding)
    {
        var exportFileName = $"Carta-Recomendacion-{SanitizeFileNamePart(payload.Header.CodigoEmpleado)}-{payload.IdLiquidacion ?? 0}";
        var issuanceDate = payload.Header.FechaLiquidacion == default ? DateTime.Today : payload.Header.FechaLiquidacion;
        var employeeName = string.IsNullOrWhiteSpace(payload.Header.NombreEmpleado)
            ? $"{payload.Header.Nombres} {payload.Header.Apellidos}".Trim()
            : payload.Header.NombreEmpleado.Trim();
        var companyName = string.IsNullOrWhiteSpace(branding.LegalName) ? "la empresa" : branding.LegalName.Trim();
        var managerName = string.IsNullOrWhiteSpace(branding.HrManagerName)
            ? "________________________________"
            : branding.HrManagerName.Trim();
        var roleText = string.IsNullOrWhiteSpace(payload.Header.Cargo) ? "un cargo dentro de la empresa" : payload.Header.Cargo.Trim();
        var departmentText = string.IsNullOrWhiteSpace(payload.Header.Departamento)
            ? string.Empty
            : $" en el area de {payload.Header.Departamento.Trim()}";
        var culture = new CultureInfo("es-NI");
        var issueDateText = issuanceDate.ToString("dd 'de' MMMM 'de' yyyy", culture);
        var startDateText = payload.Header.FechaIngreso.ToString("dd 'de' MMMM 'del' yyyy", culture);
        var endDateText = payload.Header.FechaBaja.ToString("dd 'de' MMMM 'del' yyyy", culture);
        var issueLongText = issuanceDate.ToString("dd 'dias del mes de' MMMM 'del ano' yyyy", culture);

        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        builder.AppendLine("<title>Carta de recomendacion</title>");
        builder.AppendLine("""
            <style>
              :root { color-scheme: light; }
              * { box-sizing: border-box; }
              body { margin: 0; font-family: "Segoe UI", Arial, sans-serif; background: #eff4f8; color: #122331; }
              .page { min-height: 100vh; padding: 24px; }
              .screen-shell { max-width: 980px; margin: 0 auto; }
              .screen-actions { display: flex; justify-content: flex-end; gap: 12px; margin-bottom: 12px; }
              .action-button { border: 1px solid #cfdae5; background: #fff; color: #17324a; border-radius: 999px; padding: 12px 22px; font-size: 15px; font-weight: 700; cursor: pointer; }
              .action-button.is-primary { background: linear-gradient(135deg, #31c7b2, #f3cc63); border-color: transparent; color: #0f2031; }
              .screen-note { text-align: right; color: #66798d; font-size: 13px; margin-bottom: 18px; }
              .sheet { background: #fff; border: 1px solid #d4dee8; box-shadow: 0 18px 42px rgba(15, 32, 49, .08); padding: 34px 56px 48px; min-height: 1080px; }
              .brand-row { display: flex; justify-content: space-between; align-items: flex-start; gap: 22px; }
              .brand { display: flex; align-items: center; gap: 18px; }
              .logo-shell { width: 108px; height: 72px; display: inline-flex; align-items: center; justify-content: center; overflow: hidden; }
              .logo-shell img { max-width: 100%; max-height: 100%; object-fit: contain; }
              .logo-fallback { width: 72px; height: 72px; border: 1px solid #d7e1ea; display: inline-flex; align-items: center; justify-content: center; font-size: 28px; font-weight: 800; color: #183650; }
              .brand-copy strong { display: block; font-size: 24px; line-height: 1.05; color: #12263a; }
              .brand-copy small { display: block; margin-top: 8px; color: #5e7184; font-size: 13px; }
              .issue-meta { text-align: right; color: #20394f; font-size: 14px; line-height: 1.65; }
              .title { margin: 92px 0 84px; text-align: center; }
              .title strong { font-size: 26px; letter-spacing: .04em; text-transform: uppercase; text-decoration: underline; }
              .letter-body { font-size: 20px; line-height: 2; text-align: justify; }
              .letter-body p { margin: 0 0 34px; }
              .signature-area { margin-top: 110px; }
              .signature-line { width: 360px; border-top: 1px solid #23394d; margin-bottom: 12px; }
              .signature-name { font-size: 18px; font-weight: 700; color: #132a3f; }
              .signature-role { font-size: 15px; color: #5c6f82; margin-top: 4px; }
              .footer { margin-top: 26px; padding-top: 10px; border-top: 1px solid #d7e1ea; color: #6b7d8f; font-size: 13px; text-align: center; }
              @page { size: A4; margin: 18mm 18mm; }
              @media print {
                body { background: #fff; }
                .page { padding: 0; }
                .screen-actions, .screen-note { display: none !important; }
                .screen-shell { max-width: none; }
                .sheet { min-height: auto; border: none; box-shadow: none; padding: 0; }
              }
            </style>
            """);
        builder.AppendLine("</head><body>");
        builder.AppendLine("<div class=\"page\"><div class=\"screen-shell\">");
        builder.AppendLine("<div class=\"screen-actions\">");
        builder.AppendLine("<button class=\"action-button\" type=\"button\" onclick=\"window.print()\">Imprimir</button>");
        builder.AppendLine("<button class=\"action-button is-primary\" type=\"button\" onclick=\"exportPdf()\">Generar PDF</button>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"screen-note\">Machote listo para firma y sello de Recursos Humanos.</div>");
        builder.AppendLine("<section class=\"sheet\">");
        builder.AppendLine("<div class=\"brand-row\">");
        builder.AppendLine("<div class=\"brand\">");
        builder.AppendLine("<div class=\"logo-shell\">");
        if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
        {
            builder.Append($"<img src=\"{WebUtility.HtmlEncode(branding.LogoUrl)}\" alt=\"Logo empresa\" />");
        }
        else
        {
            builder.Append($"<div class=\"logo-fallback\">{WebUtility.HtmlEncode((branding.CompanyName.Length >= 2 ? branding.CompanyName[..2] : "SI").ToUpperInvariant())}</div>");
        }
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"brand-copy\">");
        builder.AppendLine($"<strong>{WebUtility.HtmlEncode(companyName)}</strong>");
        if (!string.IsNullOrWhiteSpace(branding.CompanyName) && !string.Equals(branding.CompanyName, companyName, StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"<small>{WebUtility.HtmlEncode(branding.CompanyName)}</small>");
        }
        builder.AppendLine("</div></div>");
        builder.AppendLine($"<div class=\"issue-meta\">Managua, Nicaragua<br />{WebUtility.HtmlEncode(issueDateText)}</div>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"title\"><strong>A quien interese</strong></div>");
        builder.AppendLine("<div class=\"letter-body\">");
        builder.AppendLine($"<p>Por este medio hago constar que el senor(a) <strong>{WebUtility.HtmlEncode(employeeName)}</strong> laboro para <strong>{WebUtility.HtmlEncode(companyName)}</strong>, del {WebUtility.HtmlEncode(startDateText)} al {WebUtility.HtmlEncode(endDateText)}, durante dicho periodo se desempeno como <strong>{WebUtility.HtmlEncode(roleText)}</strong>{WebUtility.HtmlEncode(departmentText)}.</p>");
        builder.AppendLine("<p>Durante su permanencia en la institucion mantuvo relacion laboral con nuestra empresa, dejando constancia de su tiempo de servicio para los fines que estime convenientes.</p>");
        builder.AppendLine($"<p>Y para los fines que se estimen convenientes, se extiende la presente a los {WebUtility.HtmlEncode(issueLongText)}.</p>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"signature-area\">");
        builder.AppendLine("<div class=\"signature-line\"></div>");
        builder.AppendLine($"<div class=\"signature-name\">{WebUtility.HtmlEncode(managerName)}</div>");
        builder.AppendLine("<div class=\"signature-role\">Gerente de Recursos Humanos</div>");
        builder.AppendLine("</div>");
        builder.AppendLine($"<div class=\"footer\">{WebUtility.HtmlEncode(branding.FooterText)}{(branding.LogoPending ? " Logo corporativo pendiente de configuracion." : string.Empty)}</div>");
        builder.AppendLine("</section></div></div>");
        builder.AppendLine("<script>");
        builder.AppendLine("const originalTitle = document.title;");
        builder.AppendLine("function exportPdf() {");
        builder.AppendLine($"  document.title = \"{WebUtility.HtmlEncode(exportFileName)}\";");
        builder.AppendLine("  window.print();");
        builder.AppendLine("  window.setTimeout(() => { document.title = originalTitle; }, 400);");
        builder.AppendLine("}");
        builder.AppendLine("</script>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static void AppendExcelInfoRow(
        StringBuilder builder,
        string leftLabel,
        string leftValue,
        string rightLabel,
        string rightValue)
    {
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td class=\"label\">{WebUtility.HtmlEncode(leftLabel)}</td>");
        builder.AppendLine($"<td>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(leftValue) ? "-" : leftValue)}</td>");
        builder.AppendLine($"<td class=\"label\">{WebUtility.HtmlEncode(rightLabel)}</td>");
        builder.AppendLine($"<td>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(rightValue) ? "-" : rightValue)}</td>");
        builder.AppendLine("</tr>");
    }

    private static void AppendLiquidationExcelSection(
        StringBuilder builder,
        string title,
        LiquidationPreviewDto payload,
        IReadOnlyCollection<string> codes,
        decimal subtotal,
        string currency)
    {
        builder.AppendLine("<div class=\"section-gap\"></div>");
        builder.AppendLine("<table>");
        builder.AppendLine($"<tr><td class=\"section-head\">{WebUtility.HtmlEncode(title)}</td><td class=\"section-head\">Referencia</td><td class=\"section-head center\">Dias</td><td class=\"section-head right\">Monto</td></tr>");

        foreach (var line in payload.Lines.Where(line => codes.Contains(line.ConceptCode)).OrderBy(line => line.SortOrder))
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{WebUtility.HtmlEncode(line.ConceptName)}</td>");
            builder.AppendLine($"<td>{WebUtility.HtmlEncode(line.Reference)}</td>");
            builder.AppendLine($"<td class=\"center\">{(line.Days > 0m ? WebUtility.HtmlEncode(line.Days.ToString("0.##", CultureInfo.InvariantCulture)) : string.Empty)}</td>");
            builder.AppendLine($"<td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency(line.Amount, currency))}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine($"<tr class=\"subtotal\"><td colspan=\"3\">Subtotal</td><td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency(subtotal, currency))}</td></tr>");
        builder.AppendLine("</table>");
    }

    private static LiquidationCauseDescriptor ResolveCause(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return Causes.FirstOrDefault(cause => cause.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?? Causes[0];
    }

    private static EmployeeLiquidationBase? GetEmployeeLiquidationBase(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado)
    {
        const string sql = """
            SELECT TOP (1)
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.nombres,
                e.apellidos,
                e.correo,
                e.cedula,
                e.inss,
                e.fecha_ingreso,
                d.nombre_departamento,
                cg.nombre_cargo,
                c.id_contrato,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato,
                c.salario_base_mensual,
                c.moneda
            FROM rrhh.empleado e
            INNER JOIN rrhh.contrato c
                ON c.id_empleado = e.id_empleado
               AND c.es_contrato_vigente = 1
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo cg
                ON cg.id_cargo = e.id_cargo
            WHERE e.id_empleado = @id_empleado
            ORDER BY c.id_contrato DESC;
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

        return new EmployeeLiquidationBase
        {
            IdEmpleado = reader.GetInt64(0),
            CodigoEmpleado = reader.GetString(1),
            NombreEmpleado = reader.GetString(2),
            Nombres = reader.GetString(3),
            Apellidos = reader.GetString(4),
            Correo = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            Cedula = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            Inss = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            FechaIngreso = reader.GetDateTime(8),
            Departamento = reader.GetString(9),
            Cargo = reader.GetString(10),
            IdContrato = reader.GetInt64(11),
            CodigoTipoContrato = reader.GetString(12),
            NombreTipoContrato = reader.GetString(13),
            SalarioMensual = reader.GetDecimal(14),
            Moneda = reader.IsDBNull(15) ? "NIO" : reader.GetString(15),
        };
    }

    private static LiquidationCompanyConfig GetCompanyConfig(
        SqlConnection connection,
        SqlTransaction? transaction,
        DateTime referenceDate)
    {
        const string sql = """
            SELECT
                UPPER(COALESCE(nomina.fn_obtener_parametro_texto(N'REGIMEN_INSS_EMPRESA'), N'INTEGRAL')) AS regimen_inss_empresa,
                COALESCE(TRY_CAST(nomina.fn_obtener_parametro_decimal(N'CANTIDAD_TRABAJADORES_EMPRESA') AS INT), 1) AS cantidad_trabajadores_empresa,
                COALESCE(NULLIF(nomina.fn_obtener_parametro_decimal(N'DIAS_MES_NOMINA'), 0), 30) AS dias_mes_nomina;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new LiquidationCompanyConfig
            {
                RegimenInssEmpresa = "INTEGRAL",
                CantidadTrabajadoresEmpresa = 1,
                DiasMesNomina = 30m,
                CodigoInssLaboral = "INSS_LABORAL_INTEGRAL",
                CodigoInssPatronal = "INSS_PATRONAL_INTEGRAL_LT50",
                ReferenceDate = referenceDate.Date,
            };
        }

        var regimen = reader.IsDBNull(0) ? "INTEGRAL" : reader.GetString(0).Trim().ToUpperInvariant();
        var cantidad = reader.IsDBNull(1) ? 1 : reader.GetInt32(1);
        var diasMes = reader.IsDBNull(2) ? 30m : reader.GetDecimal(2);

        return new LiquidationCompanyConfig
        {
            RegimenInssEmpresa = regimen,
            CantidadTrabajadoresEmpresa = cantidad < 1 ? 1 : cantidad,
            DiasMesNomina = diasMes <= 0m ? 30m : diasMes,
            CodigoInssLaboral = regimen == "IVM_RP" ? "INSS_LABORAL_IVM_RP" : "INSS_LABORAL_INTEGRAL",
            CodigoInssPatronal = regimen == "IVM_RP"
                ? (cantidad < 50 ? "INSS_PATRONAL_IVM_RP_LT50" : "INSS_PATRONAL_IVM_RP_GE50")
                : (cantidad < 50 ? "INSS_PATRONAL_INTEGRAL_LT50" : "INSS_PATRONAL_INTEGRAL_GE50"),
            ReferenceDate = referenceDate.Date,
        };
    }

    private static decimal ExecuteContribution(
        SqlConnection connection,
        SqlTransaction? transaction,
        string contributionCode,
        DateTime referenceDate,
        decimal baseAmount,
        bool allowMissing = false)
    {
        const string sql = """
            SELECT COALESCE(nomina.fn_calcular_contribucion(@codigo, @fecha, @base), 0);
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@codigo", SqlDbType.NVarChar, 80).Value = contributionCode;
        command.Parameters.Add("@fecha", SqlDbType.Date).Value = referenceDate.Date;
        command.Parameters.Add("@base", SqlDbType.Decimal).Value = baseAmount;
        command.Parameters["@base"].Precision = 18;
        command.Parameters["@base"].Scale = 2;

        try
        {
            var result = command.ExecuteScalar();
            return result is null || result == DBNull.Value
                ? 0m
                : RoundMoney(Convert.ToDecimal(result, CultureInfo.InvariantCulture));
        }
        catch
        {
            if (allowMissing)
            {
                return 0m;
            }

            throw;
        }
    }

    private static decimal ExecuteLaborIr(
        SqlConnection connection,
        SqlTransaction? transaction,
        DateTime referenceDate,
        decimal baseAmount)
    {
        const string sql = """
            SELECT COALESCE(nomina.fn_calcular_ir_laboral_mensual(@fecha, @base), 0);
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@fecha", SqlDbType.Date).Value = referenceDate.Date;
        command.Parameters.Add("@base", SqlDbType.Decimal).Value = baseAmount;
        command.Parameters["@base"].Precision = 18;
        command.Parameters["@base"].Scale = 2;
        var result = command.ExecuteScalar();
        return result is null || result == DBNull.Value
            ? 0m
            : RoundMoney(Convert.ToDecimal(result, CultureInfo.InvariantCulture));
    }

    private static decimal GetDefaultPendingSalaryDays(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado,
        DateTime joinDate,
        DateTime terminationDate,
        decimal diasMesNomina)
    {
        const string sql = """
            SELECT MAX(p.fecha_hasta)
            FROM nomina.nomina_detalle nd
            INNER JOIN nomina.nomina n
                ON n.id_nomina = nd.id_nomina
            INNER JOIN nomina.periodo_nomina p
                ON p.id_periodo_nomina = n.id_periodo_nomina
            WHERE nd.id_empleado = @id_empleado
              AND p.fecha_hasta <= @fecha_baja;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        command.Parameters.Add("@fecha_baja", SqlDbType.Date).Value = terminationDate.Date;
        var result = command.ExecuteScalar();

        var referenceStart = result is null || result == DBNull.Value
            ? new DateTime(terminationDate.Year, terminationDate.Month, 1)
            : Convert.ToDateTime(result, CultureInfo.InvariantCulture).Date.AddDays(1);

        if (referenceStart < joinDate.Date)
        {
            referenceStart = joinDate.Date;
        }

        if (referenceStart > terminationDate.Date)
        {
            return 0m;
        }

        var days = (decimal)(terminationDate.Date - referenceStart).TotalDays + 1m;
        if (diasMesNomina > 0m && days > diasMesNomina)
        {
            days = diasMesNomina;
        }

        return RrhhSupport.RoundDays(days);
    }

    private static DateTime GetAguinaldoCycleStart(DateTime terminationDate)
    {
        return terminationDate.Month == 12
            ? new DateTime(terminationDate.Year, 12, 1)
            : new DateTime(terminationDate.Year - 1, 12, 1);
    }

    private static decimal CalculateCommercialDaysInclusive(DateTime startDate, DateTime endDate)
    {
        if (endDate.Date < startDate.Date)
        {
            return 0m;
        }

        var d1 = Math.Min(startDate.Day, 30);
        var d2 = Math.Min(endDate.Day, 30);
        var months = (endDate.Year - startDate.Year) * 12 + (endDate.Month - startDate.Month);
        var total = months * 30 + (d2 - d1) + 1;
        return total < 0 ? 0m : total;
    }

    private static decimal CalculateIndemnizationDays(
        LiquidationCauseDescriptor cause,
        string contractCode,
        decimal commercialServiceDays)
    {
        if (!cause.GrantsIndemnization || commercialServiceDays <= 0m)
        {
            return 0m;
        }

        if (cause.RequiresIndefiniteContract && !IndefiniteContractCodes.Contains(contractCode))
        {
            return 0m;
        }

        decimal days;
        if (commercialServiceDays <= 1080m)
        {
            days = commercialServiceDays / 12m;
        }
        else
        {
            days = 90m + ((commercialServiceDays - 1080m) / 18m);
        }

        days = Math.Min(150m, Math.Max(30m, days));
        return RrhhSupport.RoundDays(days);
    }

    private static string FormatServiceDuration(DateTime startDate, DateTime endDate)
    {
        if (endDate.Date < startDate.Date)
        {
            return "0 aÃ±os, 0 meses, 0 dÃ­as";
        }

        var years = 0;
        var months = 0;
        var cursor = startDate.Date;

        while (cursor.AddYears(1) <= endDate.Date)
        {
            cursor = cursor.AddYears(1);
            years += 1;
        }

        while (cursor.AddMonths(1) <= endDate.Date)
        {
            cursor = cursor.AddMonths(1);
            months += 1;
        }

        var days = (endDate.Date - cursor).Days;
        return $"{years} aÃ±os, {months} meses, {days} dÃ­as";
    }

    private static decimal RoundMoney(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static void AddLine(
        List<LiquidationLineDto> lines,
        string groupCode,
        string groupLabel,
        string conceptCode,
        string conceptName,
        decimal days,
        decimal amount,
        string reference,
        string conceptType,
        int sortOrder)
    {
        if (amount <= 0m)
        {
            return;
        }

        lines.Add(new LiquidationLineDto
        {
            GroupCode = groupCode,
            GroupLabel = groupLabel,
            ConceptCode = conceptCode,
            ConceptName = conceptName,
            Days = days,
            Amount = amount,
            Reference = reference,
            ConceptType = conceptType,
            SortOrder = sortOrder,
        });
    }

    private static string MapGroupCode(string conceptCode) => conceptCode switch
    {
        "LIQ_SALARIO_PENDIENTE" or "LIQ_VACACIONES_POR_PAGAR" => "PRESTACIONES_GRAVABLES",
        "LIQ_AGUINALDO_PROPORCIONAL" or "LIQ_INDEMNIZACION_ART45" => "PRESTACIONES_NO_GRAVABLES",
        "INSS_LABORAL" or "IR_LABORAL" => "DEDUCCIONES",
        "INSS_PATRONAL" or "INATEC_PATRONAL" => "APORTES_PATRONALES",
        _ => "OTROS",
    };

    private static string MapGroupLabel(string conceptCode) => MapGroupCode(conceptCode) switch
    {
        "PRESTACIONES_GRAVABLES" => "Prestaciones gravables",
        "PRESTACIONES_NO_GRAVABLES" => "Prestaciones no gravables",
        "DEDUCCIONES" => "Deducciones",
        "APORTES_PATRONALES" => "Aportes patronales",
        _ => "Otros",
    };

    private static decimal GetLineAmount(LiquidationPreviewDto payload, string conceptCode)
    {
        return payload.Lines
            .Where(line => line.ConceptCode.Equals(conceptCode, StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Amount)
            .DefaultIfEmpty(0m)
            .First();
    }

    private static decimal GetLineDays(LiquidationPreviewDto payload, string conceptCode)
    {
        return payload.Lines
            .Where(line => line.ConceptCode.Equals(conceptCode, StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Days)
            .DefaultIfEmpty(0m)
            .First();
    }

    private static decimal ExtractDays(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return 0m;
        }

        var dayMatch = Regex.Match(reference, @"(\d+(?:[.,]\d+)?)\s*dia", RegexOptions.IgnoreCase);
        if (!dayMatch.Success)
        {
            return 0m;
        }

        return decimal.TryParse(dayMatch.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? RrhhSupport.RoundDays(value)
            : 0m;
    }

    private static void AppendInfoRow(StringBuilder builder, string label, string value)
    {
        builder.AppendLine(
            $"<tr><td class=\"label\">{WebUtility.HtmlEncode(label)}</td><td>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value)}</td></tr>");
    }

    private static void BuildSectionTable(
        StringBuilder builder,
        string title,
        LiquidationPreviewDto payload,
        IReadOnlyCollection<string> codes,
        decimal subtotal,
        string currency)
    {
        builder.AppendLine("<table class=\"section-table\">");
        builder.AppendLine($"<thead><tr><th colspan=\"2\">{WebUtility.HtmlEncode(title)}</th><th class=\"number\">DÃ­as</th><th class=\"number\">Monto</th></tr></thead><tbody>");

        foreach (var line in payload.Lines.Where(line => codes.Contains(line.ConceptCode)).OrderBy(line => line.SortOrder))
        {
            builder.AppendLine(
                $"<tr><td>{WebUtility.HtmlEncode(line.ConceptName)}</td><td>{WebUtility.HtmlEncode(line.Reference)}</td><td class=\"number\">{(line.Days > 0m ? line.Days.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty)}</td><td class=\"number\">{WebUtility.HtmlEncode(FormatCurrency(line.Amount, currency))}</td></tr>");
        }

        builder.AppendLine($"<tr class=\"subtotal\"><td colspan=\"3\">Subtotal</td><td class=\"number\">{WebUtility.HtmlEncode(FormatCurrency(subtotal, currency))}</td></tr>");
        builder.AppendLine("</tbody></table>");
    }

    private static string FormatCurrency(decimal amount, string currencyCode)
    {
        return string.Format(CultureInfo.GetCultureInfo("es-NI"), "{0} {1:N2}", currencyCode, amount);
    }

    private static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "archivo";
        }

        var sanitized = Regex.Replace(value.Trim(), @"[^A-Za-z0-9_-]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "archivo" : sanitized;
    }
}

public sealed class LiquidationCauseDescriptor
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool GrantsIndemnization { get; set; }
    public bool RequiresIndefiniteContract { get; set; }
}

public sealed class LiquidationReasonData
{
    public string Code { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public sealed class LiquidationCompanyConfig
{
    public string RegimenInssEmpresa { get; set; } = "INTEGRAL";
    public int CantidadTrabajadoresEmpresa { get; set; } = 1;
    public decimal DiasMesNomina { get; set; } = 30m;
    public string? CodigoInssLaboral { get; set; }
    public string? CodigoInssPatronal { get; set; }
    public DateTime ReferenceDate { get; set; }
}

public sealed class EmployeeLiquidationBase
{
    public long IdEmpleado { get; set; }
    public string CodigoEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Inss { get; set; } = string.Empty;
    public DateTime FechaIngreso { get; set; }
    public string Departamento { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public long IdContrato { get; set; }
    public string CodigoTipoContrato { get; set; } = string.Empty;
    public string NombreTipoContrato { get; set; } = string.Empty;
    public decimal SalarioMensual { get; set; }
    public string Moneda { get; set; } = "NIO";
}

public sealed class LiquidationPreviewDto
{
    public bool Persisted { get; set; }
    public long? IdLiquidacion { get; set; }
    public LiquidationHeaderDto Header { get; set; } = new();
    public LiquidationCauseSummaryDto Cause { get; set; } = new();
    public LiquidationTaxableSectionDto TaxableSection { get; set; } = new();
    public LiquidationNonTaxableSectionDto NonTaxableSection { get; set; } = new();
    public LiquidationDeductionSectionDto Deductions { get; set; } = new();
    public LiquidationEmployerContributionSectionDto EmployerContributions { get; set; } = new();
    public LiquidationTotalsDto Totals { get; set; } = new();
    public List<LiquidationLineDto> Lines { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public sealed class LiquidationHeaderDto
{
    public long? IdLiquidacion { get; set; }
    public long IdEmpleado { get; set; }
    public long IdContrato { get; set; }
    public string CodigoEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Inss { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string CodigoTipoContrato { get; set; } = string.Empty;
    public string NombreTipoContrato { get; set; } = string.Empty;
    public string Moneda { get; set; } = "NIO";
    public DateTime FechaIngreso { get; set; }
    public DateTime FechaBaja { get; set; }
    public DateTime FechaLiquidacion { get; set; }
    public string TiempoLaborado { get; set; } = string.Empty;
    public decimal SalarioMensual { get; set; }
    public decimal SalarioDiario { get; set; }
    public decimal SalarioPromedio { get; set; }
    public string MotivoRetiro { get; set; } = string.Empty;
}

public sealed class LiquidationCauseSummaryDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class LiquidationTaxableSectionDto
{
    public decimal PendingSalaryDays { get; set; }
    public decimal PendingSalaryAmount { get; set; }
    public decimal VacationDays { get; set; }
    public decimal VacationAmount { get; set; }
    public decimal TaxableSubtotal { get; set; }
}

public sealed class LiquidationNonTaxableSectionDto
{
    public decimal AguinaldoDays { get; set; }
    public decimal AguinaldoAmount { get; set; }
    public decimal IndemnizationDays { get; set; }
    public decimal IndemnizationAmount { get; set; }
    public decimal NonTaxableSubtotal { get; set; }
}

public sealed class LiquidationDeductionSectionDto
{
    public decimal InssLaboral { get; set; }
    public decimal IrLaboral { get; set; }
    public decimal TotalDeductions { get; set; }
}

public sealed class LiquidationEmployerContributionSectionDto
{
    public decimal InssPatronal { get; set; }
    public decimal InatecPatronal { get; set; }
    public decimal TotalEmployerContributions { get; set; }
}

public sealed class LiquidationTotalsDto
{
    public decimal TotalIngresos { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal NetoLiquidacion { get; set; }
}

public sealed class LiquidationLineDto
{
    public string GroupCode { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public string ConceptCode { get; set; } = string.Empty;
    public string ConceptName { get; set; } = string.Empty;
    public decimal Days { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
