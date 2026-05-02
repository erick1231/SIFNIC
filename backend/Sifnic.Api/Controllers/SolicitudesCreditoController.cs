using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Creditos;
using Sifnic.Api.Nomina;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class SolicitudesCreditoController : Controller
{
    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);
            CreditPortfolioSecuritySupport.EnsureSchema(connection);
            MicrofinanceCoreSupport.EnsureSchema(connection);
            var conamiRules = ConamiRulesSupport.LoadActiveRuleMap(connection);
            var conamiClassifications = ConamiRulesSupport.GetList(
                conamiRules,
                "CARTERA_CLASIFICACIONES_CONAMI",
                ["A", "B", "C", "D", "E"]);

            using var command = new SqlCommand(
                """
                SELECT TOP (300)
                    id_cliente,
                    cedula,
                    nombres + N' ' + apellidos AS nombre_cliente,
                    tipo_cliente,
                    estado_cliente,
                    ingresos_mensuales + ingresos_conyuge + remesas + alquileres + otros_ingresos AS ingresos_totales,
                    egresos_mensuales,
                    ingresos_mensuales + ingresos_conyuge + remesas + alquileres + otros_ingresos - egresos_mensuales AS capacidad_pago,
                    nivel_riesgo,
                    estado_expediente
                FROM clientes.cliente
                WHERE activo = 1
                  AND estado_cliente IN (N'ACTIVO', N'PROSPECTO')
                ORDER BY nombres, apellidos;
                """,
                connection);

            using var reader = command.ExecuteReader();
            var clients = new List<object>();
            while (reader.Read())
            {
                clients.Add(new
                {
                    id = reader.GetInt64(0),
                    identification = reader.GetString(1),
                    name = reader.GetString(2),
                    clientType = reader.GetString(3),
                    status = reader.GetString(4),
                    totalIncome = reader.GetDecimal(5),
                    monthlyExpenses = reader.GetDecimal(6),
                    paymentCapacity = reader.GetDecimal(7),
                    riskLevel = reader.GetString(8),
                    fileStatus = reader.GetString(9),
                });
            }
            reader.Close();

            return Json(new
            {
                ok = true,
                data = new
                {
                    clients,
                    products = MicrofinanceCoreSupport.LoadProducts(connection),
                    currencies = new[] { "NIO", "USD" },
                    frequencies = CreditOperationsSupport.Frequencies,
                    installmentTypes = new[] { "NIVELADA", "SOLO_CAPITAL", "INTERES_FLAT" },
                    guaranteeTypes = MicrofinanceCoreSupport.LoadCatalog(connection, "TIPO_GARANTIA").Select(item => item.GetType().GetProperty("code")?.GetValue(item)?.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray(),
                    economicActivities = MicrofinanceCoreSupport.LoadCatalog(connection, "ACTIVIDAD_ECONOMICA"),
                    baseCatalogs = new
                    {
                        activities = MicrofinanceCoreSupport.LoadCatalog(connection, "ACTIVIDAD_ECONOMICA"),
                        departments = MicrofinanceCoreSupport.LoadCatalog(connection, "DEPARTAMENTO"),
                        municipalities = MicrofinanceCoreSupport.LoadCatalog(connection, "MUNICIPIO"),
                        administrativeStatuses = MicrofinanceCoreSupport.LoadCatalog(connection, "ESTADO_ADMINISTRATIVO"),
                        primDictionary = MicrofinanceCoreSupport.LoadCatalog(connection, "ICC_PRIM"),
                    },
                    uafAlerts = MicrofinanceCoreSupport.LoadUafAlerts(connection),
                    statuses = CreditOperationsSupport.CreditRequestStatuses,
                    prospectionStages = CreditOperationsSupport.ProspectionStages,
                    visitResults = CreditOperationsSupport.VisitResults,
                    creditBureauResults = CreditOperationsSupport.CreditBureauResults,
                    riskLevels = CreditOperationsSupport.RiskLevels,
                    conamiClassifications,
                    conamiRules,
                    requiredChecklist = new[]
                    {
                        "identificacion",
                        "expediente",
                        "visita_casa_negocio",
                        "capacidad_pago",
                        "revision_conami",
                    },
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar los catalogos de solicitudes.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult BuscarCliente(string? cedula)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            using var command = new SqlCommand(
                """
                SELECT TOP (1)
                    id_cliente,
                    cedula,
                    nombres + N' ' + apellidos AS nombre_cliente,
                    tipo_cliente,
                    estado_cliente,
                    ingresos_mensuales + ingresos_conyuge + remesas + alquileres + otros_ingresos AS ingresos_totales,
                    egresos_mensuales,
                    ingresos_mensuales + ingresos_conyuge + remesas + alquileres + otros_ingresos - egresos_mensuales AS capacidad_pago,
                    nivel_riesgo,
                    estado_expediente,
                    actividad_economica
                FROM clientes.cliente
                WHERE cedula = @cedula
                  AND activo = 1
                  AND estado_cliente IN (N'ACTIVO', N'PROSPECTO');
                """,
                connection);
            command.Parameters.Add("@cedula", SqlDbType.NVarChar, 50).Value = NormalizeIdentification(cedula);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return Json(new { ok = true, data = new { found = false } });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    found = true,
                    client = new
                    {
                        id = reader.GetInt64(0),
                        identification = reader.GetString(1),
                        name = reader.GetString(2),
                        clientType = reader.GetString(3),
                        status = reader.GetString(4),
                        totalIncome = reader.GetDecimal(5),
                        monthlyExpenses = reader.GetDecimal(6),
                        paymentCapacity = reader.GetDecimal(7),
                        riskLevel = reader.GetString(8),
                        fileStatus = reader.GetString(9),
                        economicActivity = reader.IsDBNull(10) ? null : reader.GetString(10),
                    },
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo buscar el cliente.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Listar(string? search, string? status, long? clientId)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            const string sql = """
                SELECT
                    s.id_solicitud_credito,
                    s.id_cliente,
                    c.cedula,
                    c.nombres + N' ' + c.apellidos AS nombre_cliente,
                    s.numero_solicitud,
                    s.fecha_solicitud,
                    s.monto_solicitado,
                    s.plazo_meses,
                    s.tasa_interes_anual,
                    s.moneda,
                    s.destino_credito,
                    s.estado_solicitud,
                    s.observacion,
                    s.producto_credito,
                    s.frecuencia_pago,
                    s.tipo_cuota,
                    s.cuota_estimada,
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
                    s.fecha_creacion,
                    s.usuario_registro,
                    s.fecha_actualizacion,
                    s.usuario_resolucion,
                    s.fecha_resolucion,
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
                    s.tasa_mora_anual,
                    cr.id_credito,
                    cr.numero_credito
                FROM creditos.solicitud_credito s
                INNER JOIN clientes.cliente c ON c.id_cliente = s.id_cliente
                LEFT JOIN creditos.credito cr ON cr.id_solicitud_credito = s.id_solicitud_credito
                WHERE
                    (@client_id IS NULL OR s.id_cliente = @client_id)
                    AND (@status = N'TODOS' OR s.estado_solicitud = @status)
                    AND (
                        @search = N''
                        OR s.numero_solicitud LIKE N'%' + @search + N'%'
                        OR c.cedula LIKE N'%' + @search + N'%'
                        OR c.nombres + N' ' + c.apellidos LIKE N'%' + @search + N'%'
                        OR ISNULL(s.destino_credito, N'') LIKE N'%' + @search + N'%'
                    )
                ORDER BY s.id_solicitud_credito DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeStatus(status);
            command.Parameters.Add("@client_id", SqlDbType.BigInt).Value = clientId.HasValue && clientId.Value > 0 ? clientId.Value : DBNull.Value;

            using var reader = command.ExecuteReader();
            var items = new List<CreditRequestDto>();
            while (reader.Read())
            {
                items.Add(MapRequest(reader));
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar las solicitudes.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Obtener(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);
            var conamiRules = ConamiRulesSupport.LoadActiveRuleMap(connection);

            var request = GetRequest(connection, id);
            if (request is null)
            {
                return NotFound(new { ok = false, message = "Solicitud no encontrada." });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    request,
                    paymentPlan = GetStoredOrGeneratedPlan(connection, request),
                    approvals = GetApprovals(connection, id),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo obtener la solicitud.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ReporteConamiHtml(DateTime? fechaCorte, string? tipo)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var report = CreditReportingSupport.LoadConamiPortfolioReport(connection, fechaCorte?.Date ?? DateTime.Today, tipo);
            var branding = NominaSupport.GetReportBranding(connection);
            var html = CreditReportingSupport.BuildConamiReportHtml(report, branding);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar el reporte CONAMI: {System.Net.WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult ReporteConamiExcel(DateTime? fechaCorte, string? tipo)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var report = CreditReportingSupport.LoadConamiPortfolioReport(connection, fechaCorte?.Date ?? DateTime.Today, tipo);
            var branding = NominaSupport.GetReportBranding(connection);
            var workbookHtml = CreditReportingSupport.BuildConamiReportExcel(report, branding);
            var fileName = $"Reporte-CONAMI-{CreditReportingSupport.SanitizeFileNamePart(report.ReportType)}-{report.CutoffDate:yyyyMMdd}.xls";
            return ExcelFile(workbookHtml, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo exportar el reporte CONAMI: {System.Net.WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult ExpedienteHtml(long id)
    {
        if (id <= 0)
        {
            return BadRequest("Solicitud invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var file = CreditReportingSupport.LoadCreditFile(connection, id);
            if (file is null)
            {
                return NotFound("No se encontro la solicitud.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var html = CreditReportingSupport.BuildCreditFileHtml(file, branding);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar el expediente: {System.Net.WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult ExpedienteExcel(long id)
    {
        if (id <= 0)
        {
            return BadRequest("Solicitud invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var file = CreditReportingSupport.LoadCreditFile(connection, id);
            if (file is null)
            {
                return NotFound("No se encontro la solicitud.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var workbookHtml = CreditReportingSupport.BuildCreditFileExcel(file, branding);
            var fileName = $"Expediente-{CreditReportingSupport.SanitizeFileNamePart(file.Request.Number)}.xls";
            return ExcelFile(workbookHtml, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo exportar el expediente: {System.Net.WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult PlanPagoHtml(long id)
    {
        if (id <= 0)
        {
            return BadRequest("Solicitud invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var file = CreditReportingSupport.LoadCreditFile(connection, id);
            if (file is null)
            {
                return NotFound("No se encontro la solicitud.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var html = CreditReportingSupport.BuildPaymentPlanHtml(file, branding);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar el plan de pago: {System.Net.WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult PlanPagoExcel(long id)
    {
        if (id <= 0)
        {
            return BadRequest("Solicitud invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var file = CreditReportingSupport.LoadCreditFile(connection, id);
            if (file is null)
            {
                return NotFound("No se encontro la solicitud.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var workbookHtml = CreditReportingSupport.BuildPaymentPlanExcel(file, branding);
            var fileName = $"Plan-Pago-{CreditReportingSupport.SanitizeFileNamePart(file.Request.Number)}.xls";
            return ExcelFile(workbookHtml, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo exportar el plan de pago: {System.Net.WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] CreditRequestSaveModel model)
    {
        return SaveRequest(null, model);
    }

    [HttpPost]
    public IActionResult Actualizar(long id, [FromBody] CreditRequestSaveModel model)
    {
        return SaveRequest(id, model);
    }

    [HttpPost]
    public IActionResult GenerarPlan([FromBody] PaymentPlanRequestModel model)
    {
        CreditProductDto? product;
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            product = MicrofinanceCoreSupport.FindProduct(connection, model.Product);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar la parametrizacion del tipo de credito.", detail = ex.Message });
        }

        if (product is null)
        {
            return BadRequest(new { ok = false, message = "Selecciona un tipo de credito activo.", errors = new { product = "Tipo de credito no configurado." } });
        }

        ApplyProductPolicy(model, product);
        var errors = ValidatePaymentPlan(model.Amount, model.TermMonths, product.AnnualRate);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "No se pudo generar el plan de pago.", errors });
        }

        var startDate = model.StartDate?.Date ?? DateTime.Today;
        var plan = CreditOperationsSupport.GeneratePaymentPlan(
            model.Amount,
            model.AnnualRate,
            model.TermMonths,
            model.Frequency,
            startDate,
            model.CommissionRate,
            model.SlidingRate,
            model.MoraRate,
            model.CommissionMode);

        return Json(new
        {
            ok = true,
            data = new
            {
                product,
                paymentPlan = plan,
                summary = BuildPlanSummary(plan, model.Amount, model.CommissionRate, model.OtherCharges, startDate, model.CommissionMode),
            },
        });
    }

    [HttpGet]
    public IActionResult ProductosCredito()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            var products = MicrofinanceCoreSupport.LoadProducts(connection);
            return Json(new { ok = true, data = products });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar los tipos de credito.", detail = ex.Message });
        }
    }

    [HttpPut]
    public IActionResult GuardarProductoCredito([FromBody] CreditProductDto model)
    {
        if (model is null)
        {
            return BadRequest(new { ok = false, message = "No se recibio el tipo de credito." });
        }

        var errors = ValidateCreditProduct(model);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "Corrige la parametrizacion del tipo de credito.", errors });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var saved = MicrofinanceCoreSupport.UpsertProduct(
                connection,
                model,
                CreditOperationsSupport.GetOperatorUser(Request),
                transaction);

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CONFIGURACION",
                "TIPO_CREDITO",
                "ACTUALIZACION",
                0,
                saved.Code,
                $"Se actualizo el tipo de credito {saved.Code}.",
                saved);

            transaction.Commit();
            return Json(new { ok = true, message = "Tipo de credito actualizado correctamente.", data = saved });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo guardar el tipo de credito.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult ConsultarCentral([FromBody] CreditBureauQueryModel model)
    {
        if (model.ClientId <= 0)
        {
            return BadRequest(new { ok = false, message = "Selecciona un cliente para consultar central de riesgo." });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);
            var snapshot = BuildSinRiesgoRegistrationDraft(connection, model);
            return Json(new { ok = true, data = snapshot });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo consultar la central de riesgo.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Resolver(long id, [FromBody] CreditRequestResolutionModel model)
    {
        model ??= new CreditRequestResolutionModel();
        var action = CreditOperationsSupport.NormalizeCode(model.Action, string.Empty);
        if (action is not ("APROBAR" or "RECHAZAR" or "MEJORA" or "COMITE" or "ANULAR" or "PRECALIFICAR"))
        {
            return BadRequest(new { ok = false, message = "Accion no soportada." });
        }

        if ((action is "RECHAZAR" or "ANULAR" or "MEJORA") && string.IsNullOrWhiteSpace(model.Observation))
        {
            return BadRequest(new { ok = false, message = "Indica el motivo de la resolucion.", errors = new { observation = "El motivo es obligatorio." } });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);
            var conamiRules = ConamiRulesSupport.LoadActiveRuleMap(connection);

            var request = GetRequest(connection, id);
            if (request is null)
            {
                return NotFound(new { ok = false, message = "Solicitud no encontrada." });
            }

            if (action == "APROBAR")
            {
                if (request.ProspectionStage != "SOLICITUD_FORMAL")
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "La solicitud debe estar en etapa SOLICITUD FORMAL antes de aprobar.",
                        errors = new { prospectionStage = "Formaliza la prospeccion antes de aprobar." },
                    });
                }

                var prospectionErrors = ValidateApprovalProspection(request);
                if (prospectionErrors.Count > 0)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "La prospeccion no cumple los controles para aprobar.",
                        errors = prospectionErrors,
                    });
                }

                var bureauErrors = ValidateCreditBureau(request.CreditBureau, requireClearResult: true);
                if (bureauErrors.Count > 0)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "La consulta de central de riesgo no cumple los controles para aprobar.",
                        errors = bureauErrors,
                    });
                }

                var checklistErrors = ValidateApprovalChecklist(request.Checklist, conamiRules);
                if (checklistErrors.Count > 0)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "El expediente no cumple las validaciones para aprobar.",
                        errors = checklistErrors,
                    });
                }
            }

            var approvedBaseAmount = request.Amount;
            var approvedTermMonths = request.TermMonths;
            var approvedAnnualRate = request.AnnualRate;
            var commissionAmount = 0m;
            var financedAmount = request.Amount;

            if (action == "APROBAR")
            {
                approvedBaseAmount = CreditOperationsSupport.SafeDecimal(model.ApprovedAmount.GetValueOrDefault(request.Amount));
                approvedTermMonths = model.ApprovedTermMonths.GetValueOrDefault(request.TermMonths);
                approvedAnnualRate = CreditOperationsSupport.SafeDecimal(model.ApprovedAnnualRate.GetValueOrDefault(request.AnnualRate));

                if (approvedBaseAmount <= 0)
                {
                    return BadRequest(new { ok = false, message = "El monto a aprobar debe ser mayor que cero.", errors = new { approvedAmount = "Monto invalido." } });
                }

                foreach (var item in ValidatePaymentPlan(approvedBaseAmount, approvedTermMonths, approvedAnnualRate))
                {
                    return BadRequest(new { ok = false, message = item.Value, errors = new Dictionary<string, string> { [item.Key] = item.Value } });
                }

                commissionAmount = CreditOperationsSupport.SafeDecimal(approvedBaseAmount * Math.Max(request.CommissionRate, 0m) / 100m);
                financedAmount = CreditOperationsSupport.SafeDecimal(approvedBaseAmount + commissionAmount);
            }

            using var transaction = connection.BeginTransaction();
            var targetStatus = action switch
            {
                "APROBAR" => "APROBADA",
                "RECHAZAR" => "RECHAZADA",
                "MEJORA" => "MEJORA",
                "ANULAR" => "ANULADA",
                "COMITE" => "COMITE",
                _ => "PRECALIFICADA",
            };

            using (var command = new SqlCommand(
                """
                UPDATE creditos.solicitud_credito
                SET
                    estado_solicitud = @estado_solicitud,
                    usuario_resolucion = @usuario_resolucion,
                    fecha_resolucion = SYSDATETIME(),
                    observacion = COALESCE(NULLIF(@observacion, N''), observacion),
                    motivo_descarte_rechazo = CASE WHEN @motivo_descarte_rechazo <> N'' THEN @motivo_descarte_rechazo ELSE motivo_descarte_rechazo END,
                    etapa_prospeccion = @etapa_prospeccion,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_solicitud_credito = @id_solicitud_credito;
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@estado_solicitud", SqlDbType.NVarChar, 30).Value = targetStatus;
                command.Parameters.Add("@usuario_resolucion", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.GetOperatorUser(Request);
                command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value = model.Observation?.Trim() ?? string.Empty;
                command.Parameters.Add("@motivo_descarte_rechazo", SqlDbType.NVarChar, 500).Value = model.Observation?.Trim() ?? string.Empty;
                command.Parameters.Add("@etapa_prospeccion", SqlDbType.NVarChar, 30).Value = action switch
                {
                    "RECHAZAR" => "DESCARTADO",
                    "PRECALIFICAR" => "PRECALIFICADO",
                    "MEJORA" => "PRECALIFICADO",
                    _ => request.ProspectionStage,
                };
                command.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            long? creditId = null;
            string? creditNumber = null;

            if (action == "APROBAR")
            {
                using (var approvalCommand = new SqlCommand(
                    """
                    INSERT INTO creditos.aprobacion_solicitud_credito
                    (
                        id_solicitud_credito,
                        fecha_aprobacion,
                        monto_aprobado,
                        plazo_meses,
                        tasa_interes_anual,
                        moneda,
                        usuario_aprobador,
                        resolucion,
                        observacion
                    )
                    VALUES
                    (
                        @id_solicitud_credito,
                        SYSDATETIME(),
                        @monto_aprobado,
                        @plazo_meses,
                        @tasa_interes_anual,
                        @moneda,
                        @usuario_aprobador,
                        N'APROBADA',
                        @observacion
                    );
                    """,
                    connection,
                    transaction))
                {
                    approvalCommand.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = id;
                    approvalCommand.Parameters.Add("@monto_aprobado", SqlDbType.Decimal).Value = financedAmount;
                    approvalCommand.Parameters.Add("@plazo_meses", SqlDbType.Int).Value = approvedTermMonths;
                    approvalCommand.Parameters.Add("@tasa_interes_anual", SqlDbType.Decimal).Value = approvedAnnualRate;
                    approvalCommand.Parameters.Add("@moneda", SqlDbType.NVarChar, 10).Value = request.Currency;
                    approvalCommand.Parameters.Add("@usuario_aprobador", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.GetOperatorUser(Request);
                    approvalCommand.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value = CreditOperationsSupport.TextOrDbNull(BuildApprovalObservation(model.Observation, approvedBaseAmount, commissionAmount, financedAmount));
                    approvalCommand.ExecuteNonQuery();
                }

                (creditId, creditNumber) = CreateLoanFromRequest(
                    connection,
                    transaction,
                    request,
                    approvedBaseAmount,
                    financedAmount,
                    approvedTermMonths,
                    approvedAnnualRate);
            }

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CREDITOS",
                "SOLICITUD_CREDITO",
                targetStatus,
                id,
                request.Number,
                $"Solicitud {request.Number} marcada como {targetStatus}.",
                new { action, targetStatus, creditId, creditNumber, model.Observation, approvedBaseAmount, commissionAmount, financedAmount });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = action == "APROBAR"
                    ? "Solicitud aprobada y enviada a caja para desembolso."
                    : "Solicitud actualizada correctamente.",
                data = new { status = targetStatus, creditId, creditNumber, approvedBaseAmount, commissionAmount, financedAmount, disbursementUrl = creditId.HasValue ? $"/App/Caja?credito={creditId}" : null },
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SolicitudesCredito.Resolver] {ex}");
            return StatusCode(500, new { ok = false, message = "No se pudo resolver la solicitud." });
        }
    }

    private IActionResult SaveRequest(long? id, CreditRequestSaveModel model)
    {
        var errors = ValidateRequest(model);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "Corrige los datos de la solicitud.", errors });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);
            var conamiRules = ConamiRulesSupport.LoadActiveRuleMap(connection);

            var existing = id.HasValue ? GetRequest(connection, id.Value) : null;
            if (id.HasValue && existing is null)
            {
                return NotFound(new { ok = false, message = "Solicitud no encontrada." });
            }

            if (!ClientExists(connection, model.ClientId))
            {
                return BadRequest(new { ok = false, message = "Selecciona un cliente activo.", errors = new { clientId = "Cliente no valido." } });
            }

            var product = MicrofinanceCoreSupport.FindProduct(connection, model.Product);
            if (product is null)
            {
                return BadRequest(new { ok = false, message = "Selecciona un tipo de credito activo.", errors = new { product = "Tipo de credito no configurado." } });
            }

            ApplyProductPolicy(model, product);

            var plan = CreditOperationsSupport.GeneratePaymentPlan(
                model.Amount,
                model.AnnualRate,
                model.TermMonths,
                model.Frequency,
                model.RequestDate?.Date ?? DateTime.Today,
                model.CommissionRate,
                model.SlidingRate,
                model.MoraRate,
                "DESCONTADA");
            var summary = BuildPlanSummary(plan, model.Amount, model.CommissionRate, 0, model.RequestDate?.Date ?? DateTime.Today, "DESCONTADA");
            var capacity = CreditOperationsSupport.SafeDecimal(model.DeclaredIncome - model.DeclaredExpenses);
            var committeeAmount = ConamiRulesSupport.GetDecimal(conamiRules, "SOL_COMITE_MONTO_MIN", 50000m);
            var committeeCapacityFactor = ConamiRulesSupport.GetDecimal(conamiRules, "SOL_COMITE_CUOTA_CAPACIDAD_PCT", 50m) / 100m;
            var conamiClassifications = ConamiRulesSupport.GetList(conamiRules, "CARTERA_CLASIFICACIONES_CONAMI", ["A", "B", "C", "D", "E"]);
            var requiresCommittee =
                model.RequiresCommittee ||
                summary.EstimatedInstallment > capacity * committeeCapacityFactor ||
                model.Amount >= committeeAmount ||
                NormalizeRisk(model.RiskLevel) == "ALTO";
            var checklistJson = JsonSerializer.Serialize(model.Checklist ?? new CreditChecklistModel());
            var referencesJson = JsonSerializer.Serialize(model.References ?? new ProspectionReferencesModel());
            var visitsJson = JsonSerializer.Serialize(model.Visits ?? new ProspectionVisitsModel());
            var creditBureau = NormalizeCreditBureau(model.CreditBureau);
            var creditBureauJson = JsonSerializer.Serialize(creditBureau);
            var planJson = JsonSerializer.Serialize(new { paymentPlan = plan, summary });
            var status = NormalizeStatus(model.Status) == "TODOS" ? "TRAMITE" : NormalizeStatus(model.Status);

            using var transaction = connection.BeginTransaction();
            long requestId;
            string number;

            if (id.HasValue)
            {
                number = existing!.Number;
                using var update = new SqlCommand(BuildUpdateSql(), connection, transaction);
                update.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = id.Value;
                AddRequestParameters(update, model, number, summary.EstimatedInstallment, capacity, requiresCommittee, checklistJson, referencesJson, visitsJson, creditBureauJson, creditBureau, planJson, status, conamiClassifications);
                update.ExecuteNonQuery();
                requestId = id.Value;
            }
            else
            {
                number = CreditOperationsSupport.NextCode(
                    connection,
                    "creditos.solicitud_credito",
                    "numero_solicitud",
                    $"SOL-{DateTime.Today:yyyy}-",
                    transaction);
                using var insert = new SqlCommand(BuildInsertSql(), connection, transaction);
                AddRequestParameters(insert, model, number, summary.EstimatedInstallment, capacity, requiresCommittee, checklistJson, referencesJson, visitsJson, creditBureauJson, creditBureau, planJson, status, conamiClassifications);
                insert.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.GetOperatorUser(Request);
                requestId = Convert.ToInt64(insert.ExecuteScalar());
            }

            var saved = GetRequest(connection, requestId, transaction)!;
            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CREDITOS",
                "SOLICITUD_CREDITO",
                id.HasValue ? "ACTUALIZACION" : "CREACION",
                requestId,
                saved.Number,
                id.HasValue ? $"Se actualizo la solicitud {saved.Number}." : $"Se creo la solicitud {saved.Number}.",
                saved);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = id.HasValue ? "Solicitud actualizada correctamente." : "Solicitud creada correctamente.",
                data = new { request = saved, paymentPlan = plan, summary },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo guardar la solicitud."),
                detail = ex.Message,
            });
        }
    }

    private static string BuildInsertSql()
    {
        return """
            INSERT INTO creditos.solicitud_credito
            (
                id_cliente, numero_solicitud, fecha_solicitud, monto_solicitado, plazo_meses,
                tasa_interes_anual, moneda, destino_credito, estado_solicitud, observacion,
                producto_credito, frecuencia_pago, tipo_cuota, cuota_estimada, ingresos_declarados,
                egresos_declarados, capacidad_pago, fuente_ingreso, actividad_financiada,
                tipo_garantia, descripcion_garantia, valor_garantia, nombre_fiador, cedula_fiador,
                telefono_fiador, requiere_comite, nivel_riesgo, clasificacion_conami,
                checklist_json, etapa_prospeccion, motivo_descarte_rechazo, promotor_credito,
                sucursal_credito, oficina_credito, fecha_sistema_prospeccion,
                referencias_prospeccion_json, visitas_prospeccion_json,
                fecha_consulta_central, central_riesgo_json,
                tasa_comision_ascc, tasa_deslizamiento_anual, tasa_mora_anual,
                plan_generado_json, usuario_registro
            )
            OUTPUT INSERTED.id_solicitud_credito
            VALUES
            (
                @id_cliente, @numero_solicitud, @fecha_solicitud, @monto_solicitado, @plazo_meses,
                @tasa_interes_anual, @moneda, @destino_credito, @estado_solicitud, @observacion,
                @producto_credito, @frecuencia_pago, @tipo_cuota, @cuota_estimada, @ingresos_declarados,
                @egresos_declarados, @capacidad_pago, @fuente_ingreso, @actividad_financiada,
                @tipo_garantia, @descripcion_garantia, @valor_garantia, @nombre_fiador, @cedula_fiador,
                @telefono_fiador, @requiere_comite, @nivel_riesgo, @clasificacion_conami,
                @checklist_json, @etapa_prospeccion, @motivo_descarte_rechazo, @promotor_credito,
                @sucursal_credito, @oficina_credito, @fecha_sistema_prospeccion,
                @referencias_prospeccion_json, @visitas_prospeccion_json,
                @fecha_consulta_central, @central_riesgo_json,
                @tasa_comision_ascc, @tasa_deslizamiento_anual, @tasa_mora_anual,
                @plan_generado_json, @usuario_registro
            );
            """;
    }

    private static string BuildUpdateSql()
    {
        return """
            UPDATE creditos.solicitud_credito
            SET
                id_cliente = @id_cliente,
                fecha_solicitud = @fecha_solicitud,
                monto_solicitado = @monto_solicitado,
                plazo_meses = @plazo_meses,
                tasa_interes_anual = @tasa_interes_anual,
                moneda = @moneda,
                destino_credito = @destino_credito,
                estado_solicitud = @estado_solicitud,
                observacion = @observacion,
                producto_credito = @producto_credito,
                frecuencia_pago = @frecuencia_pago,
                tipo_cuota = @tipo_cuota,
                cuota_estimada = @cuota_estimada,
                ingresos_declarados = @ingresos_declarados,
                egresos_declarados = @egresos_declarados,
                capacidad_pago = @capacidad_pago,
                fuente_ingreso = @fuente_ingreso,
                actividad_financiada = @actividad_financiada,
                tipo_garantia = @tipo_garantia,
                descripcion_garantia = @descripcion_garantia,
                valor_garantia = @valor_garantia,
                nombre_fiador = @nombre_fiador,
                cedula_fiador = @cedula_fiador,
                telefono_fiador = @telefono_fiador,
                requiere_comite = @requiere_comite,
                nivel_riesgo = @nivel_riesgo,
                clasificacion_conami = @clasificacion_conami,
                checklist_json = @checklist_json,
                etapa_prospeccion = @etapa_prospeccion,
                motivo_descarte_rechazo = @motivo_descarte_rechazo,
                promotor_credito = @promotor_credito,
                sucursal_credito = @sucursal_credito,
                oficina_credito = @oficina_credito,
                fecha_sistema_prospeccion = @fecha_sistema_prospeccion,
                referencias_prospeccion_json = @referencias_prospeccion_json,
                visitas_prospeccion_json = @visitas_prospeccion_json,
                fecha_consulta_central = @fecha_consulta_central,
                central_riesgo_json = @central_riesgo_json,
                tasa_comision_ascc = @tasa_comision_ascc,
                tasa_deslizamiento_anual = @tasa_deslizamiento_anual,
                tasa_mora_anual = @tasa_mora_anual,
                plan_generado_json = @plan_generado_json,
                fecha_actualizacion = SYSDATETIME()
            WHERE id_solicitud_credito = @id_solicitud_credito;
            """;
    }

    private static void AddRequestParameters(
        SqlCommand command,
        CreditRequestSaveModel model,
        string number,
        decimal estimatedInstallment,
        decimal capacity,
        bool requiresCommittee,
        string checklistJson,
        string referencesJson,
        string visitsJson,
        string creditBureauJson,
        CreditBureauSnapshotModel creditBureau,
        string planJson,
        string status,
        IReadOnlyCollection<string> conamiClassifications)
    {
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = model.ClientId;
        command.Parameters.Add("@numero_solicitud", SqlDbType.NVarChar, 50).Value = number;
        command.Parameters.Add("@fecha_solicitud", SqlDbType.Date).Value = model.RequestDate?.Date ?? DateTime.Today;
        command.Parameters.Add("@monto_solicitado", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.Amount);
        command.Parameters.Add("@plazo_meses", SqlDbType.Int).Value = model.TermMonths;
        command.Parameters.Add("@tasa_interes_anual", SqlDbType.Decimal).Value = Math.Round(model.AnnualRate, 6);
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 10).Value = CreditOperationsSupport.NormalizeCode(model.Currency, "NIO");
        command.Parameters.Add("@destino_credito", SqlDbType.NVarChar, 250).Value = CreditOperationsSupport.TextOrDbNull(model.Destination);
        command.Parameters.Add("@estado_solicitud", SqlDbType.NVarChar, 30).Value = status;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value = CreditOperationsSupport.TextOrDbNull(model.Notes);
        command.Parameters.Add("@producto_credito", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.TextOrDbNull(model.Product);
        command.Parameters.Add("@frecuencia_pago", SqlDbType.NVarChar, 30).Value = NormalizeFrequency(model.Frequency);
        command.Parameters.Add("@tipo_cuota", SqlDbType.NVarChar, 30).Value = CreditOperationsSupport.NormalizeCode(model.InstallmentType, "NIVELADA");
        command.Parameters.Add("@cuota_estimada", SqlDbType.Decimal).Value = estimatedInstallment;
        command.Parameters.Add("@ingresos_declarados", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.DeclaredIncome);
        command.Parameters.Add("@egresos_declarados", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.DeclaredExpenses);
        command.Parameters.Add("@capacidad_pago", SqlDbType.Decimal).Value = capacity;
        command.Parameters.Add("@fuente_ingreso", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.IncomeSource);
        command.Parameters.Add("@actividad_financiada", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.FinancedActivity);
        command.Parameters.Add("@tipo_garantia", SqlDbType.NVarChar, 80).Value = CreditOperationsSupport.TextOrDbNull(model.GuaranteeType);
        command.Parameters.Add("@descripcion_garantia", SqlDbType.NVarChar, 500).Value = CreditOperationsSupport.TextOrDbNull(model.GuaranteeDescription);
        command.Parameters.Add("@valor_garantia", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.GuaranteeValue);
        command.Parameters.Add("@nombre_fiador", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.GuarantorName);
        command.Parameters.Add("@cedula_fiador", SqlDbType.NVarChar, 50).Value = CreditOperationsSupport.TextOrDbNull(model.GuarantorIdentification);
        command.Parameters.Add("@telefono_fiador", SqlDbType.NVarChar, 50).Value = CreditOperationsSupport.TextOrDbNull(model.GuarantorPhone);
        command.Parameters.Add("@requiere_comite", SqlDbType.Bit).Value = requiresCommittee;
        command.Parameters.Add("@nivel_riesgo", SqlDbType.NVarChar, 20).Value = NormalizeRisk(model.RiskLevel);
        command.Parameters.Add("@clasificacion_conami", SqlDbType.NVarChar, 10).Value = NormalizeConamiClass(model.ConamiClassification, conamiClassifications);
        command.Parameters.Add("@checklist_json", SqlDbType.NVarChar).Value = checklistJson;
        command.Parameters.Add("@etapa_prospeccion", SqlDbType.NVarChar, 30).Value = NormalizeProspectionStage(model.ProspectionStage);
        command.Parameters.Add("@motivo_descarte_rechazo", SqlDbType.NVarChar, 500).Value = CreditOperationsSupport.TextOrDbNull(model.DiscardRejectReason);
        command.Parameters.Add("@promotor_credito", SqlDbType.NVarChar, 150).Value = CreditOperationsSupport.TextOrDbNull(model.Promoter);
        command.Parameters.Add("@sucursal_credito", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.TextOrDbNull(model.Branch);
        command.Parameters.Add("@oficina_credito", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.TextOrDbNull(model.Office);
        command.Parameters.Add("@fecha_sistema_prospeccion", SqlDbType.Date).Value = model.SystemDate?.Date ?? DateTime.Today;
        command.Parameters.Add("@referencias_prospeccion_json", SqlDbType.NVarChar).Value = referencesJson;
        command.Parameters.Add("@visitas_prospeccion_json", SqlDbType.NVarChar).Value = visitsJson;
        command.Parameters.Add("@fecha_consulta_central", SqlDbType.Date).Value = creditBureau.Consulted && creditBureau.ConsultationDate.HasValue ? creditBureau.ConsultationDate.Value.Date : (object)DBNull.Value;
        command.Parameters.Add("@central_riesgo_json", SqlDbType.NVarChar).Value = creditBureauJson;
        command.Parameters.Add("@tasa_comision_ascc", SqlDbType.Decimal).Value = Math.Round(Math.Max(0, model.CommissionRate), 6);
        command.Parameters.Add("@tasa_deslizamiento_anual", SqlDbType.Decimal).Value = Math.Round(Math.Max(0, model.SlidingRate), 6);
        command.Parameters.Add("@tasa_mora_anual", SqlDbType.Decimal).Value = Math.Round(Math.Max(0, model.MoraRate), 6);
        command.Parameters.Add("@plan_generado_json", SqlDbType.NVarChar).Value = planJson;
    }

    private static string BuildApprovalObservation(string? observation, decimal approvedBaseAmount, decimal commissionAmount, decimal financedAmount)
    {
        var operatorNote = string.IsNullOrWhiteSpace(observation) ? "Aprobacion directa desde expediente." : observation.Trim();
        var note = $"{operatorNote} Monto a desembolsar: {approvedBaseAmount:N2}. Comision financiada: {commissionAmount:N2}. Capital aprobado: {financedAmount:N2}.";
        return note.Length <= 500 ? note : note[..500];
    }

    private (long CreditId, string CreditNumber) CreateLoanFromRequest(
        SqlConnection connection,
        SqlTransaction transaction,
        CreditRequestDto request,
        decimal approvedBaseAmount,
        decimal financedAmount,
        int approvedTermMonths,
        decimal approvedAnnualRate)
    {
        using (var existingCommand = new SqlCommand(
            """
            SELECT TOP (1) id_credito, numero_credito
            FROM creditos.credito
            WHERE id_solicitud_credito = @id_solicitud_credito;
            """,
            connection,
            transaction))
        {
            existingCommand.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = request.Id;
            using var existingReader = existingCommand.ExecuteReader();
            if (existingReader.Read())
            {
                return (existingReader.GetInt64(0), existingReader.IsDBNull(1) ? request.Number : existingReader.GetString(1));
            }
        }

        // La comision de desembolso queda financiada dentro del capital aprobado;
        // por eso el plan se genera sobre el monto financiado y no vuelve a programar comision.
        var plan = CreditOperationsSupport.GeneratePaymentPlan(
            financedAmount,
            approvedAnnualRate,
            approvedTermMonths,
            request.Frequency,
            DateTime.Today,
            0,
            request.SlidingRate,
            request.MoraRate,
            "DESCONTADA");
        var creditNumber = CreditOperationsSupport.NextCode(
            connection,
            "creditos.credito",
            "numero_credito",
            $"CRD-{DateTime.Today:yyyy}-",
            transaction);
        var dueDate = plan.Last().DueDate;

        long creditId;
        using (var command = new SqlCommand(
            """
            INSERT INTO creditos.credito
            (
                cedula_id_cliente_ofic_ciclo,
                cedula_id_cliente,
                nom_cliente,
                tipo_agrupacion,
                garantia,
                oficina,
                fecha_desembolso,
                fecha_vencimiento,
                estado_operativo,
                saldo_capital,
                id_cliente,
                id_solicitud_credito,
                numero_credito,
                moneda,
                monto_aprobado,
                plazo_meses,
                tasa_interes_anual,
                fecha_aprobacion
            )
            OUTPUT INSERTED.id_credito
            VALUES
            (
                @numero_credito,
                @cedula,
                @nombre_cliente,
                @tipo_agrupacion,
                @garantia,
                @oficina,
                NULL,
                @fecha_vencimiento,
                N'APROBADO',
                0,
                @id_cliente,
                @id_solicitud_credito,
                @numero_credito,
                @moneda,
                @monto_aprobado,
                @plazo_meses,
                @tasa_interes_anual,
                CONVERT(date, SYSDATETIME())
            );
            """,
            connection,
            transaction))
        {
            command.Parameters.Add("@numero_credito", SqlDbType.NVarChar, 100).Value = creditNumber;
            command.Parameters.Add("@cedula", SqlDbType.NVarChar, 50).Value = request.ClientIdentification;
            command.Parameters.Add("@nombre_cliente", SqlDbType.NVarChar, 250).Value = request.ClientName;
            command.Parameters.Add("@tipo_agrupacion", SqlDbType.TinyInt).Value = request.ClientType == "GRUPO_SOLIDARIO" ? 4 : 1;
            command.Parameters.Add("@garantia", SqlDbType.NVarChar, 50).Value = CreditOperationsSupport.TextOrDbNull(request.GuaranteeType);
            command.Parameters.Add("@oficina", SqlDbType.NVarChar, 20).Value = "CASA";
            command.Parameters.Add("@fecha_vencimiento", SqlDbType.Date).Value = dueDate;
            command.Parameters.Add("@saldo_capital", SqlDbType.Decimal).Value = financedAmount;
            command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = request.ClientId;
            command.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = request.Id;
            command.Parameters.Add("@moneda", SqlDbType.NVarChar, 10).Value = request.Currency;
            command.Parameters.Add("@monto_aprobado", SqlDbType.Decimal).Value = financedAmount;
            command.Parameters.Add("@plazo_meses", SqlDbType.Int).Value = approvedTermMonths;
            command.Parameters.Add("@tasa_interes_anual", SqlDbType.Decimal).Value = approvedAnnualRate;
            creditId = Convert.ToInt64(command.ExecuteScalar());
        }

        foreach (var item in plan)
        {
            using var planCommand = new SqlCommand(
                """
                INSERT INTO creditos.plan_pago_credito
                (
                    cedula_id_cliente_ofic_ciclo,
                    numero_cuota,
                    fecha_cuota,
                    saldo_capital_cuota,
                    saldo_interes_cuota,
                    saldo_comision_cuota,
                    saldo_mora_cuota,
                    capital_programado,
                    interes_programado,
                    comision_programada,
                    mora_programada,
                    dias_interes,
                    deslizamiento_programado,
                    estado_cuota,
                    id_credito
                )
                VALUES
                (
                    @numero_credito,
                    @numero_cuota,
                    @fecha_cuota,
                    @saldo_capital,
                    @interes,
                    @comision,
                    0,
                    @capital,
                    @interes,
                    @comision,
                    0,
                    @dias_interes,
                    @deslizamiento,
                    N'PENDIENTE',
                    @id_credito
                );
                """,
                connection,
                transaction);
            planCommand.Parameters.Add("@numero_credito", SqlDbType.NVarChar, 100).Value = creditNumber;
            planCommand.Parameters.Add("@numero_cuota", SqlDbType.Int).Value = item.Number;
            planCommand.Parameters.Add("@fecha_cuota", SqlDbType.Date).Value = item.DueDate.Date;
            planCommand.Parameters.Add("@capital", SqlDbType.Decimal).Value = item.Capital;
            planCommand.Parameters.Add("@saldo_capital", SqlDbType.Decimal).Value = item.Balance;
            planCommand.Parameters.Add("@interes", SqlDbType.Decimal).Value = item.Interest;
            planCommand.Parameters.Add("@comision", SqlDbType.Decimal).Value = item.Commission;
            planCommand.Parameters.Add("@dias_interes", SqlDbType.Int).Value = item.InterestDays;
            planCommand.Parameters.Add("@deslizamiento", SqlDbType.Decimal).Value = item.Sliding;
            planCommand.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
            planCommand.ExecuteNonQuery();
        }

        using (var rateCommand = new SqlCommand(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM creditos.tasa_variable_credito
                WHERE id_credito = @id_credito
                  AND fecha_tasa = CONVERT(date, SYSDATETIME())
            )
            BEGIN
                INSERT INTO creditos.tasa_variable_credito
                (
                    id_credito,
                    fecha_tasa,
                    tasa_interes_anual,
                    observacion,
                    usuario_registro
                )
                VALUES
                (
                    @id_credito,
                    CONVERT(date, SYSDATETIME()),
                    @tasa_interes_anual,
                    N'Tasa inicial al aprobar credito.',
                    @usuario_registro
                );
            END;
            """,
            connection,
            transaction))
        {
            rateCommand.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
            rateCommand.Parameters.Add("@tasa_interes_anual", SqlDbType.Decimal).Value = approvedAnnualRate;
            rateCommand.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.GetOperatorUser(Request);
            rateCommand.ExecuteNonQuery();
        }

        AssignLoanToOfficer(connection, transaction, creditId, request.Promoter);

        return (creditId, creditNumber);
    }

    private void AssignLoanToOfficer(
        SqlConnection connection,
        SqlTransaction transaction,
        long creditId,
        string? promoter)
    {
        var officerId = CreditPortfolioSecuritySupport.ResolveOfficerUserId(connection, transaction, promoter, Request);
        if (!officerId.HasValue)
        {
            return;
        }

        var assignerId = CreditPortfolioSecuritySupport.ResolveCurrentUserId(connection, transaction, Request);
        using var command = new SqlCommand(
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM creditos.asignacion_oficial_credito
                WHERE id_credito = @id_credito
                  AND activo = 1
                  AND fecha_fin IS NULL
            )
            BEGIN
                INSERT INTO creditos.asignacion_oficial_credito
                (
                    id_credito,
                    id_usuario_oficial,
                    id_usuario_asigna,
                    motivo,
                    observacion
                )
                VALUES
                (
                    @id_credito,
                    @id_usuario_oficial,
                    @id_usuario_asigna,
                    N'APROBACION_SOLICITUD',
                    N'Asignacion automatica al generar prestamo desde solicitud aprobada.'
                );

                INSERT INTO creditos.historial_asignacion_oficial_credito
                (
                    id_credito,
                    id_usuario_oficial_anterior,
                    id_usuario_oficial_nuevo,
                    id_usuario_accion,
                    tipo_accion,
                    motivo,
                    observacion
                )
                VALUES
                (
                    @id_credito,
                    NULL,
                    @id_usuario_oficial,
                    @id_usuario_asigna,
                    N'ASIGNACION',
                    N'APROBACION_SOLICITUD',
                    N'Asignacion automatica inicial.'
                );
            END;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
        command.Parameters.Add("@id_usuario_oficial", SqlDbType.BigInt).Value = officerId.Value;
        command.Parameters.Add("@id_usuario_asigna", SqlDbType.BigInt).Value = assignerId.HasValue ? assignerId.Value : DBNull.Value;
        command.ExecuteNonQuery();
    }

    private static IReadOnlyDictionary<string, string> ValidateRequest(CreditRequestSaveModel model)
    {
        var errors = new Dictionary<string, string>();

        if (model.ClientId <= 0)
        {
            errors["clientId"] = "Selecciona el cliente.";
        }

        foreach (var item in ValidatePaymentPlan(model.Amount, model.TermMonths, model.AnnualRate))
        {
            errors[item.Key] = item.Value;
        }

        if (string.IsNullOrWhiteSpace(model.Destination))
        {
            errors["destination"] = "Indica el destino del credito.";
        }

        if (model.DeclaredIncome < 0 || model.DeclaredExpenses < 0 || model.GuaranteeValue < 0)
        {
            errors["amounts"] = "Los montos declarados no pueden ser negativos.";
        }

        if (model.DeclaredIncome > 0 && model.DeclaredExpenses >= model.DeclaredIncome)
        {
            errors["declaredExpenses"] = "Los egresos no deben superar o igualar los ingresos declarados.";
        }

        var status = NormalizeStatus(model.Status);
        if (status != "TODOS" && !CreditOperationsSupport.CreditRequestStatuses.Contains(status))
        {
            errors["status"] = "Estado de solicitud no valido.";
        }

        var stage = NormalizeProspectionStage(model.ProspectionStage);
        if (stage is "DESCARTADO" || status is "RECHAZADA")
        {
            if (string.IsNullOrWhiteSpace(model.DiscardRejectReason))
            {
                errors["discardRejectReason"] = "Indica el motivo de descarte o rechazo.";
            }
        }

        if (stage is "PRECALIFICADO" or "SOLICITUD_FORMAL" || status is "PRECALIFICADA" or "COMITE" or "APROBADA")
        {
            if (string.IsNullOrWhiteSpace(model.Promoter)) errors["promoter"] = "Indica el promotor responsable.";
            if (string.IsNullOrWhiteSpace(model.Branch)) errors["branch"] = "Indica la sucursal.";
            if (string.IsNullOrWhiteSpace(model.Office)) errors["office"] = "Indica la oficina de credito.";
        }

            if (stage == "SOLICITUD_FORMAL" || status is "COMITE" or "APROBADA")
            {
                var references = model.References ?? new ProspectionReferencesModel();
                var visits = model.Visits ?? new ProspectionVisitsModel();
                foreach (var item in ValidateCreditBureau(NormalizeCreditBureau(model.CreditBureau), requireClearResult: status is "COMITE" or "APROBADA"))
                {
                    errors[item.Key] = item.Value;
                }

                if (string.IsNullOrWhiteSpace(references.Personal.Name))
                {
                    errors["personalReferenceName"] = "Registra al menos una referencia personal.";
            }

            if (string.IsNullOrWhiteSpace(references.Commercial.Name) && string.IsNullOrWhiteSpace(references.Financial.Name))
            {
                errors["commercialReferenceName"] = "Registra una referencia comercial o financiera.";
            }

            if (NormalizeVisitResult(visits.Home.Result) != "REALIZADA")
            {
                errors["homeVisitResult"] = "La visita domiciliar debe quedar realizada.";
            }

            if (NormalizeVisitResult(visits.Business.Result) != "REALIZADA")
            {
                errors["businessVisitResult"] = "La visita al negocio debe quedar realizada.";
            }
        }

        return errors;
    }

    private static IReadOnlyDictionary<string, string> ValidateCreditProduct(CreditProductDto model)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            errors["code"] = "Indica el codigo del tipo de credito.";
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            errors["name"] = "Indica el nombre del tipo de credito.";
        }

        if (model.AnnualRate is < 0 or > 200)
        {
            errors["annualRate"] = "La tasa anual debe estar entre 0 y 200.";
        }

        if (model.CommissionRate is < 0 or > 100)
        {
            errors["commissionRate"] = "La comision por desembolso debe estar entre 0 y 100.";
        }

        if (model.MoraRate is < 0 or > 200)
        {
            errors["moraRate"] = "La mora anual debe estar entre 0 y 200.";
        }

        if (model.SlidingRate is < 0 or > 100)
        {
            errors["slidingRate"] = "El deslizamiento debe estar entre 0 y 100.";
        }

        if (model.MinTermMonths < 1 || model.MaxTermMonths < model.MinTermMonths)
        {
            errors["termMonths"] = "El plazo maximo debe ser igual o mayor al plazo minimo.";
        }

        if (model.MaxAmount > 0 && model.MaxAmount < model.MinAmount)
        {
            errors["amount"] = "El monto maximo debe ser igual o mayor al monto minimo.";
        }

        return errors;
    }

    private static IReadOnlyDictionary<string, string> ValidatePaymentPlan(decimal amount, int termMonths, decimal annualRate)
    {
        var errors = new Dictionary<string, string>();
        if (amount <= 0)
        {
            errors["amount"] = "El monto debe ser mayor que cero.";
        }

        if (termMonths is < 1 or > 120)
        {
            errors["termMonths"] = "El plazo debe estar entre 1 y 120 meses.";
        }

        if (annualRate is < 0 or > 200)
        {
            errors["annualRate"] = "La tasa anual debe estar entre 0 y 200.";
        }

        return errors;
    }

    private static void ApplyProductPolicy(PaymentPlanRequestModel model, CreditProductDto product)
    {
        model.Product = product.Code;
        model.Currency = product.Currency;
        model.AnnualRate = product.AnnualRate;
        model.CommissionRate = product.CommissionRate;
        model.SlidingRate = product.SlidingRate;
        model.MoraRate = product.MoraRate;
        model.Frequency = product.Frequency;
        model.CommissionMode = "DESCONTADA";
    }

    private static void ApplyProductPolicy(CreditRequestSaveModel model, CreditProductDto product)
    {
        model.Product = product.Code;
        model.Currency = product.Currency;
        model.AnnualRate = product.AnnualRate;
        model.CommissionRate = product.CommissionRate;
        model.SlidingRate = product.SlidingRate;
        model.MoraRate = product.MoraRate;
        model.Frequency = product.Frequency;
        model.InstallmentType = product.InstallmentType;
        model.RequiresCommittee = model.RequiresCommittee || (product.CommitteeFrom > 0 && model.Amount >= product.CommitteeFrom);

        if (product.RequiresGuarantee && string.IsNullOrWhiteSpace(model.GuaranteeType))
        {
            model.GuaranteeType = "HIPOTECARIA";
        }
    }

    private static IReadOnlyDictionary<string, string> ValidateApprovalChecklist(
        CreditChecklistModel checklist,
        IReadOnlyDictionary<string, ConamiRuleValue> rules)
    {
        var errors = new Dictionary<string, string>();

        if (ConamiRulesSupport.GetBool(rules, "SOL_APROBACION_IDENTIFICACION", true) && !checklist.Identification) errors["identification"] = "Falta identificacion validada.";
        if (ConamiRulesSupport.GetBool(rules, "SOL_APROBACION_EXPEDIENTE", true) && !checklist.FileCompleted) errors["fileCompleted"] = "Falta expediente completo.";
        if (ConamiRulesSupport.GetBool(rules, "SOL_APROBACION_VISITA", true) && !checklist.HomeBusinessVisit) errors["homeBusinessVisit"] = "Falta visita casa/negocio.";
        if (ConamiRulesSupport.GetBool(rules, "SOL_APROBACION_CAPACIDAD", true) && !checklist.PaymentCapacity) errors["paymentCapacity"] = "Falta validacion de capacidad de pago.";
        if (ConamiRulesSupport.GetBool(rules, "SOL_APROBACION_REVISION_CONAMI", true) && !checklist.ConamiReview) errors["conamiReview"] = "Falta revision CONAMI.";

        return errors;
    }

    private static IReadOnlyDictionary<string, string> ValidateApprovalProspection(CreditRequestDto request)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request.Promoter)) errors["promoter"] = "Falta promotor responsable.";
        if (string.IsNullOrWhiteSpace(request.Branch)) errors["branch"] = "Falta sucursal.";
        if (string.IsNullOrWhiteSpace(request.Office)) errors["office"] = "Falta oficina de credito.";
        if (string.IsNullOrWhiteSpace(request.References.Personal.Name)) errors["personalReference"] = "Falta referencia personal.";
        if (string.IsNullOrWhiteSpace(request.References.Commercial.Name) && string.IsNullOrWhiteSpace(request.References.Financial.Name))
        {
            errors["commercialFinancialReference"] = "Falta referencia comercial o financiera.";
        }

        if (NormalizeVisitResult(request.Visits.Home.Result) != "REALIZADA") errors["homeVisit"] = "Falta visita domiciliar realizada.";
        if (NormalizeVisitResult(request.Visits.Business.Result) != "REALIZADA") errors["businessVisit"] = "Falta visita al negocio realizada.";
        return errors;
    }

    private static IReadOnlyDictionary<string, string> ValidateCreditBureau(CreditBureauSnapshotModel bureau, bool requireClearResult)
    {
        var errors = new Dictionary<string, string>();
        if (!bureau.Consulted)
        {
            errors["sinRiesgoConsulted"] = "Falta registrar la consulta oficial de SIN RIESGO.";
        }

        if (!string.Equals(bureau.BureauName, "SIN_RIESGO", StringComparison.OrdinalIgnoreCase))
        {
            errors["sinRiesgoSource"] = "La fuente del historial debe ser SIN RIESGO.";
        }

        if (string.IsNullOrWhiteSpace(bureau.ReportNumber))
        {
            errors["sinRiesgoReportNumber"] = "Indica el numero de reporte SIN RIESGO.";
        }

        if (!bureau.ConsultationDate.HasValue)
        {
            errors["sinRiesgoConsultationDate"] = "Indica la fecha de consulta SIN RIESGO.";
        }

        if (bureau.Result == "SIN_CONSULTA")
        {
            errors["sinRiesgoResult"] = "Registra el resultado de SIN RIESGO.";
        }

        if (requireClearResult && bureau.Result == "BLOQUEADO")
        {
            errors["sinRiesgoBlocked"] = "SIN RIESGO bloqueado no permite aprobacion.";
        }

        return errors;
    }

    private static bool ClientExists(SqlConnection connection, long clientId)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM clientes.cliente
            WHERE id_cliente = @id_cliente
              AND activo = 1;
            """,
            connection);
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = clientId;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static string NormalizeIdentification(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static CreditRequestDto? GetRequest(SqlConnection connection, long id, SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(
            """
            SELECT
                s.id_solicitud_credito, s.id_cliente, c.cedula, c.nombres + N' ' + c.apellidos AS nombre_cliente,
                s.numero_solicitud, s.fecha_solicitud, s.monto_solicitado, s.plazo_meses,
                s.tasa_interes_anual, s.moneda, s.destino_credito, s.estado_solicitud, s.observacion,
                s.producto_credito, s.frecuencia_pago, s.tipo_cuota, s.cuota_estimada,
                s.ingresos_declarados, s.egresos_declarados, s.capacidad_pago, s.fuente_ingreso,
                s.actividad_financiada, s.tipo_garantia, s.descripcion_garantia, s.valor_garantia,
                s.nombre_fiador, s.cedula_fiador, s.telefono_fiador, s.requiere_comite,
                s.nivel_riesgo, s.clasificacion_conami, s.checklist_json, s.fecha_creacion,
                s.usuario_registro, s.fecha_actualizacion, s.usuario_resolucion, s.fecha_resolucion,
                s.etapa_prospeccion, s.motivo_descarte_rechazo, s.promotor_credito, s.sucursal_credito,
                s.oficina_credito, s.fecha_sistema_prospeccion, s.referencias_prospeccion_json, s.visitas_prospeccion_json,
                s.fecha_consulta_central, s.central_riesgo_json,
                s.tasa_comision_ascc, s.tasa_deslizamiento_anual, s.tasa_mora_anual,
                cr.id_credito, cr.numero_credito, c.tipo_cliente
            FROM creditos.solicitud_credito s
            INNER JOIN clientes.cliente c ON c.id_cliente = s.id_cliente
            LEFT JOIN creditos.credito cr ON cr.id_solicitud_credito = s.id_solicitud_credito
            WHERE s.id_solicitud_credito = @id_solicitud_credito;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = id;
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapRequest(reader) : null;
    }

    private static List<object> GetApprovals(SqlConnection connection, long requestId)
    {
        using var command = new SqlCommand(
            """
            SELECT
                id_aprobacion_solicitud_credito,
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
            """,
            connection);
        command.Parameters.Add("@id_solicitud_credito", SqlDbType.BigInt).Value = requestId;
        using var reader = command.ExecuteReader();
        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                approvalDate = reader.GetDateTime(1),
                approvedAmount = reader.GetDecimal(2),
                termMonths = reader.GetInt32(3),
                annualRate = reader.GetDecimal(4),
                currency = reader.GetString(5),
                approvedBy = reader.GetString(6),
                resolution = reader.GetString(7),
                note = reader.IsDBNull(8) ? null : reader.GetString(8),
            });
        }

        return items;
    }

    private static object GetStoredOrGeneratedPlan(SqlConnection connection, CreditRequestDto request)
    {
        if (request.CreditId.HasValue)
        {
            using var command = new SqlCommand(
                """
                SELECT
                    numero_cuota,
                    fecha_cuota,
                    dias_interes,
                    capital_programado,
                    interes_programado,
                    comision_programada,
                    deslizamiento_programado,
                    mora_programada,
                    capital_programado + interes_programado + comision_programada + deslizamiento_programado + mora_programada AS total_cuota,
                    saldo_capital_cuota,
                    estado_cuota
                FROM creditos.plan_pago_credito
                WHERE id_credito = @id_credito
                ORDER BY numero_cuota;
                """,
                connection);
            command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = request.CreditId.Value;
            using var reader = command.ExecuteReader();
            var stored = new List<object>();
            while (reader.Read())
            {
                stored.Add(new
                {
                    number = reader.GetInt32(0),
                    dueDate = reader.GetDateTime(1),
                    interestDays = reader.GetInt32(2),
                    capital = reader.GetDecimal(3),
                    interest = reader.GetDecimal(4),
                    commission = reader.GetDecimal(5),
                    sliding = reader.GetDecimal(6),
                    mora = reader.GetDecimal(7),
                    total = reader.GetDecimal(8),
                    balance = reader.GetDecimal(9),
                    status = reader.GetString(10),
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
            request.MoraRate);
    }

    private static CreditRequestDto MapRequest(SqlDataReader reader)
    {
        var checklist = new CreditChecklistModel();
        var checklistOrdinal = reader.GetOrdinal("checklist_json");
        if (!reader.IsDBNull(checklistOrdinal))
        {
            try
            {
                checklist = JsonSerializer.Deserialize<CreditChecklistModel>(reader.GetString(checklistOrdinal)) ?? new CreditChecklistModel();
            }
            catch
            {
                checklist = new CreditChecklistModel();
            }
        }

        var dto = new CreditRequestDto
        {
            Id = ReadInt64(reader, "id_solicitud_credito"),
            ClientId = ReadInt64(reader, "id_cliente"),
            ClientIdentification = ReadString(reader, "cedula") ?? string.Empty,
            ClientName = ReadString(reader, "nombre_cliente") ?? string.Empty,
            Number = ReadString(reader, "numero_solicitud") ?? string.Empty,
            RequestDate = ReadDateTime(reader, "fecha_solicitud") ?? DateTime.Today,
            Amount = ReadDecimal(reader, "monto_solicitado"),
            TermMonths = ReadInt32(reader, "plazo_meses"),
            AnnualRate = ReadDecimal(reader, "tasa_interes_anual"),
            Currency = ReadString(reader, "moneda") ?? string.Empty,
            Destination = ReadString(reader, "destino_credito"),
            Status = ReadString(reader, "estado_solicitud") ?? string.Empty,
            Notes = ReadString(reader, "observacion"),
            Product = ReadString(reader, "producto_credito"),
            Frequency = ReadString(reader, "frecuencia_pago") ?? string.Empty,
            InstallmentType = ReadString(reader, "tipo_cuota") ?? string.Empty,
            EstimatedInstallment = ReadDecimal(reader, "cuota_estimada"),
            CommissionRate = ReadDecimal(reader, "tasa_comision_ascc"),
            SlidingRate = ReadDecimal(reader, "tasa_deslizamiento_anual"),
            MoraRate = ReadDecimal(reader, "tasa_mora_anual"),
            DeclaredIncome = ReadDecimal(reader, "ingresos_declarados"),
            DeclaredExpenses = ReadDecimal(reader, "egresos_declarados"),
            PaymentCapacity = ReadDecimal(reader, "capacidad_pago"),
            IncomeSource = ReadString(reader, "fuente_ingreso"),
            FinancedActivity = ReadString(reader, "actividad_financiada"),
            GuaranteeType = ReadString(reader, "tipo_garantia"),
            GuaranteeDescription = ReadString(reader, "descripcion_garantia"),
            GuaranteeValue = ReadDecimal(reader, "valor_garantia"),
            GuarantorName = ReadString(reader, "nombre_fiador"),
            GuarantorIdentification = ReadString(reader, "cedula_fiador"),
            GuarantorPhone = ReadString(reader, "telefono_fiador"),
            RequiresCommittee = ReadBoolean(reader, "requiere_comite"),
            RiskLevel = ReadString(reader, "nivel_riesgo") ?? string.Empty,
            ConamiClassification = ReadString(reader, "clasificacion_conami") ?? string.Empty,
            Checklist = checklist,
            CreatedAt = ReadDateTime(reader, "fecha_creacion") ?? DateTime.MinValue,
            RegisteredBy = ReadString(reader, "usuario_registro"),
            UpdatedAt = ReadDateTime(reader, "fecha_actualizacion"),
            ResolutionUser = ReadString(reader, "usuario_resolucion"),
            ResolutionDate = ReadDateTime(reader, "fecha_resolucion"),
            ProspectionStage = ReadString(reader, "etapa_prospeccion") ?? "PROSPECTO",
            DiscardRejectReason = ReadString(reader, "motivo_descarte_rechazo"),
            Promoter = ReadString(reader, "promotor_credito"),
            Branch = ReadString(reader, "sucursal_credito"),
            Office = ReadString(reader, "oficina_credito"),
            SystemDate = ReadDateTime(reader, "fecha_sistema_prospeccion") ?? DateTime.Today,
            References = DeserializeJson(reader, "referencias_prospeccion_json", new ProspectionReferencesModel()),
            Visits = DeserializeJson(reader, "visitas_prospeccion_json", new ProspectionVisitsModel()),
            CreditBureau = DeserializeJson(reader, "central_riesgo_json", new CreditBureauSnapshotModel()),
            CreditId = ReadInt64Nullable(reader, "id_credito"),
            CreditNumber = ReadString(reader, "numero_credito"),
            ClientType = ReadString(reader, "tipo_cliente") ?? string.Empty,
        };

        return dto;
    }

    private static bool HasColumn(SqlDataReader reader, string name)
    {
        for (var index = 0; index < reader.FieldCount; index += 1)
        {
            if (string.Equals(reader.GetName(index), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        if (!HasColumn(reader, name))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadDateTime(SqlDataReader reader, string name)
    {
        if (!HasColumn(reader, name))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long ReadInt64(SqlDataReader reader, string name) => ReadInt64Nullable(reader, name) ?? 0;

    private static long? ReadInt64Nullable(SqlDataReader reader, string name)
    {
        if (!HasColumn(reader, name))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int ReadInt32(SqlDataReader reader, string name)
    {
        if (!HasColumn(reader, name))
        {
            return 0;
        }

        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(SqlDataReader reader, string name)
    {
        if (!HasColumn(reader, name))
        {
            return 0;
        }

        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool ReadBoolean(SqlDataReader reader, string name)
    {
        if (!HasColumn(reader, name))
        {
            return false;
        }

        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static T DeserializeJson<T>(SqlDataReader reader, string name, T fallback)
    {
        var value = ReadString(reader, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static PlanSummaryDto BuildPlanSummary(
        IReadOnlyList<PlanPaymentDto> plan,
        decimal amount = 0,
        decimal commissionRate = 0,
        decimal otherCharges = 0,
        DateTime? startDate = null,
        string? commissionMode = null)
    {
        amount = CreditOperationsSupport.SafeDecimal(amount);
        otherCharges = CreditOperationsSupport.SafeDecimal(otherCharges);
        var totalCommission = CreditOperationsSupport.SafeDecimal(amount * (Math.Max(commissionRate, 0) / 100m));
        var upfrontCommission = CreditOperationsSupport.NormalizeCode(commissionMode, "PRORRATEADA") == "DESCONTADA"
            ? totalCommission
            : 0;
        var netDisbursed = CreditOperationsSupport.SafeDecimal(amount - upfrontCommission - otherCharges);
        var effectiveAnnualCostRate = CreditOperationsSupport.CalculateEffectiveAnnualCostRate(
            netDisbursed,
            startDate?.Date ?? DateTime.Today,
            plan);

        return new PlanSummaryDto
        {
            Installments = plan.Count,
            EstimatedInstallment = plan.Count == 0 ? 0 : plan[0].Total,
            TotalCapital = plan.Sum(item => item.Capital),
            TotalInterest = plan.Sum(item => item.Interest),
            TotalCommission = plan.Sum(item => item.Commission),
            UpfrontCommission = upfrontCommission,
            OtherCharges = otherCharges,
            NetDisbursed = netDisbursed,
            TotalSliding = plan.Sum(item => item.Sliding),
            TotalToPay = plan.Sum(item => item.Total),
            AverageInstallment = plan.Count == 0 ? 0 : CreditOperationsSupport.SafeDecimal(plan.Sum(item => item.Total) / plan.Count),
            EffectiveAnnualCostRate = effectiveAnnualCostRate,
            LastDueDate = plan.Count == 0 ? null : plan[^1].DueDate,
        };
    }

    private static string NormalizeStatus(string? value)
    {
        var status = CreditOperationsSupport.NormalizeCode(value, "TODOS");
        return status == string.Empty ? "TODOS" : status;
    }

    private static string NormalizeFrequency(string? value)
    {
        var frequency = CreditOperationsSupport.NormalizeCode(value, "MENSUAL");
        return CreditOperationsSupport.Frequencies.Contains(frequency) ? frequency : "MENSUAL";
    }

    private static string NormalizeRisk(string? value)
    {
        var risk = CreditOperationsSupport.NormalizeCode(value, "MEDIO");
        return CreditOperationsSupport.RiskLevels.Contains(risk) ? risk : "MEDIO";
    }

    private static string NormalizeProspectionStage(string? value)
    {
        var stage = CreditOperationsSupport.NormalizeCode(value, "PROSPECTO");
        return CreditOperationsSupport.ProspectionStages.Contains(stage) ? stage : "PROSPECTO";
    }

    private static string NormalizeVisitResult(string? value)
    {
        var result = CreditOperationsSupport.NormalizeCode(value, "PENDIENTE");
        return CreditOperationsSupport.VisitResults.Contains(result) ? result : "PENDIENTE";
    }

    private static CreditBureauSnapshotModel NormalizeCreditBureau(CreditBureauSnapshotModel? value)
    {
        var bureau = value ?? new CreditBureauSnapshotModel();
        bureau.BureauName = "SIN_RIESGO";
        bureau.Result = NormalizeCreditBureauResult(bureau.Result);
        bureau.ReportNumber = bureau.ReportNumber?.Trim() ?? string.Empty;
        bureau.ExternalDebt = CreditOperationsSupport.SafeDecimal(bureau.ExternalDebt);
        bureau.ExternalInstallment = CreditOperationsSupport.SafeDecimal(bureau.ExternalInstallment);
        bureau.InternalDebt = CreditOperationsSupport.SafeDecimal(bureau.InternalDebt);
        bureau.InternalInstallment = CreditOperationsSupport.SafeDecimal(bureau.InternalInstallment);
        bureau.RequestedAmount = CreditOperationsSupport.SafeDecimal(bureau.RequestedAmount);
        bureau.RequestedInstallment = CreditOperationsSupport.SafeDecimal(bureau.RequestedInstallment);
        bureau.TotalDebt = CreditOperationsSupport.SafeDecimal(bureau.ExternalDebt + bureau.InternalDebt + bureau.RequestedAmount);
        bureau.TotalInstallment = CreditOperationsSupport.SafeDecimal(bureau.ExternalInstallment + bureau.InternalInstallment + bureau.RequestedInstallment);
        bureau.PaymentCapacity = CreditOperationsSupport.SafeDecimal(bureau.PaymentCapacity);
        bureau.DebtCapacityRatio = bureau.PaymentCapacity <= 0 ? 0 : CreditOperationsSupport.SafeDecimal((bureau.TotalInstallment / bureau.PaymentCapacity) * 100m);
        bureau.Score = Math.Clamp(bureau.Score, 0, 999);
        bureau.Alerts ??= [];
        bureau.Notes = bureau.Notes?.Trim() ?? string.Empty;

        if (bureau.DebtCapacityRatio > 50 && !bureau.Alerts.Any(alert => alert.Contains("endeudamiento", StringComparison.OrdinalIgnoreCase)))
        {
            bureau.Alerts.Add($"Endeudamiento proyectado {bureau.DebtCapacityRatio:N2}% sobre capacidad.");
        }

        return bureau;
    }

    private static string NormalizeCreditBureauResult(string? value)
    {
        var result = CreditOperationsSupport.NormalizeCode(value, "SIN_CONSULTA");
        return CreditOperationsSupport.CreditBureauResults.Contains(result) ? result : "SIN_CONSULTA";
    }

    private static CreditBureauSnapshotModel BuildSinRiesgoRegistrationDraft(SqlConnection connection, CreditBureauQueryModel model)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                c.nivel_riesgo,
                c.estado_expediente,
                c.ingresos_mensuales + c.ingresos_conyuge + c.remesas + c.alquileres + c.otros_ingresos - c.egresos_mensuales AS capacidad_pago
            FROM clientes.cliente c
            WHERE c.id_cliente = @id_cliente
              AND c.activo = 1;
            """,
            connection);
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = model.ClientId;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Cliente no encontrado o inactivo.");
        }

        var risk = reader.GetString(0);
        var fileStatus = reader.GetString(1);
        var capacity = reader.GetDecimal(2);
        reader.Close();

        using var debtCommand = new SqlCommand(
            """
            SELECT
                COALESCE(SUM(COALESCE(cr.saldo_capital, cr.monto_aprobado, 0)), 0) AS saldo_interno,
                COALESCE(SUM(COALESCE(s.cuota_estimada, cuota.total_cuota, 0)), 0) AS cuota_interna,
                COUNT(cr.id_credito) AS creditos_activos,
                COALESCE(SUM(CASE WHEN mora.cuotas_vencidas > 0 THEN mora.cuotas_vencidas ELSE 0 END), 0) AS cuotas_vencidas
            FROM creditos.credito cr
            LEFT JOIN creditos.solicitud_credito s ON s.id_solicitud_credito = cr.id_solicitud_credito
            OUTER APPLY
            (
                SELECT TOP (1)
                    pp.capital_programado + pp.interes_programado + pp.comision_programada + pp.mora_programada AS total_cuota
                FROM creditos.plan_pago_credito pp
                WHERE pp.id_credito = cr.id_credito
                ORDER BY pp.numero_cuota
            ) cuota
            OUTER APPLY
            (
                SELECT COUNT(1) AS cuotas_vencidas
                FROM creditos.plan_pago_credito pp
                WHERE pp.id_credito = cr.id_credito
                  AND pp.estado_cuota <> N'PAGADA'
                  AND pp.fecha_cuota < CONVERT(date, SYSDATETIME())
            ) mora
            WHERE cr.id_cliente = @id_cliente
              AND COALESCE(cr.saldo_capital, cr.monto_aprobado, 0) > 0;
            """,
            connection);
        debtCommand.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = model.ClientId;
        using var debtReader = debtCommand.ExecuteReader();
        debtReader.Read();
        var internalDebt = debtReader.GetDecimal(0);
        var internalInstallment = debtReader.GetDecimal(1);
        var activeLoans = debtReader.GetInt32(2);
        var overdueInstallments = debtReader.GetInt32(3);

        var alerts = new List<string>();
        if (activeLoans > 0) alerts.Add($"Cliente tiene {activeLoans} credito(s) interno(s) activo(s).");
        if (overdueInstallments > 0) alerts.Add($"Cliente tiene {overdueInstallments} cuota(s) vencida(s) internas.");
        if (string.Equals(risk, "ALTO", StringComparison.OrdinalIgnoreCase)) alerts.Add("Cliente marcado como riesgo alto en SIFNIC.");
        if (!string.Equals(fileStatus, "COMPLETO", StringComparison.OrdinalIgnoreCase)) alerts.Add("Expediente de cliente incompleto.");

        return NormalizeCreditBureau(new CreditBureauSnapshotModel
        {
            Consulted = false,
            BureauName = "SIN_RIESGO",
            ConsultationDate = DateTime.Today,
            Result = "SIN_CONSULTA",
            InternalDebt = internalDebt,
            InternalInstallment = internalInstallment,
            RequestedAmount = model.Amount,
            RequestedInstallment = model.EstimatedInstallment,
            PaymentCapacity = capacity,
            Alerts = alerts,
            Notes = "Registrar aqui el reporte oficial emitido por SIN RIESGO y las deudas/cuotas externas encontradas.",
        });
    }

    private static string NormalizeConamiClass(string? value, IReadOnlyCollection<string>? allowed = null)
    {
        var text = CreditOperationsSupport.NormalizeCode(value, "A");
        var valid = allowed is { Count: > 0 } ? allowed : ["A", "B", "C", "D", "E"];
        return valid.Contains(text, StringComparer.OrdinalIgnoreCase) ? text : valid.First();
    }

    private FileContentResult ExcelFile(string workbookHtml, string fileName)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(workbookHtml);
        var preamble = Encoding.UTF8.GetPreamble();
        var bytes = new byte[preamble.Length + bodyBytes.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(bodyBytes, 0, bytes, preamble.Length, bodyBytes.Length);
        return File(bytes, "application/vnd.ms-excel; charset=utf-8", fileName);
    }

    public sealed class CreditRequestSaveModel
    {
        public long ClientId { get; set; }
        public DateTime? RequestDate { get; set; }
        public decimal Amount { get; set; }
        public int TermMonths { get; set; }
        public decimal AnnualRate { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal SlidingRate { get; set; }
        public decimal MoraRate { get; set; }
        public string? Currency { get; set; }
        public string? Destination { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public string? Product { get; set; }
        public string? Frequency { get; set; }
        public string? InstallmentType { get; set; }
        public decimal DeclaredIncome { get; set; }
        public decimal DeclaredExpenses { get; set; }
        public string? IncomeSource { get; set; }
        public string? FinancedActivity { get; set; }
        public string? GuaranteeType { get; set; }
        public string? GuaranteeDescription { get; set; }
        public decimal GuaranteeValue { get; set; }
        public string? GuarantorName { get; set; }
        public string? GuarantorIdentification { get; set; }
        public string? GuarantorPhone { get; set; }
        public bool RequiresCommittee { get; set; }
        public string? RiskLevel { get; set; }
        public string? ConamiClassification { get; set; }
        public CreditChecklistModel? Checklist { get; set; }
        public string? ProspectionStage { get; set; }
        public string? DiscardRejectReason { get; set; }
        public string? Promoter { get; set; }
        public string? Branch { get; set; }
        public string? Office { get; set; }
        public DateTime? SystemDate { get; set; }
        public ProspectionReferencesModel? References { get; set; }
        public ProspectionVisitsModel? Visits { get; set; }
        public CreditBureauSnapshotModel? CreditBureau { get; set; }
    }

    public sealed class PaymentPlanRequestModel
    {
        public string? Product { get; set; }
        public string? Currency { get; set; }
        public decimal Amount { get; set; }
        public int TermMonths { get; set; }
        public decimal AnnualRate { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal SlidingRate { get; set; }
        public decimal MoraRate { get; set; }
        public decimal OtherCharges { get; set; }
        public string? CommissionMode { get; set; }
        public string? Frequency { get; set; }
        public DateTime? StartDate { get; set; }
    }

    public sealed class CreditBureauQueryModel
    {
        public long ClientId { get; set; }
        public decimal Amount { get; set; }
        public decimal EstimatedInstallment { get; set; }
    }

    public sealed class CreditRequestResolutionModel
    {
        public string? Action { get; set; }
        public string? Observation { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public int? ApprovedTermMonths { get; set; }
        public decimal? ApprovedAnnualRate { get; set; }
    }

    public sealed class CreditChecklistModel
    {
        public bool Identification { get; set; }
        public bool FileCompleted { get; set; }
        public bool HomeBusinessVisit { get; set; }
        public bool PaymentCapacity { get; set; }
        public bool ConamiReview { get; set; }
        public bool ListCheck { get; set; }
        public bool GuaranteeReview { get; set; }
    }

    public sealed class ProspectionReferenceModel
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Result { get; set; }
    }

    public sealed class ProspectionReferencesModel
    {
        public ProspectionReferenceModel Personal { get; set; } = new();
        public ProspectionReferenceModel Commercial { get; set; } = new();
        public ProspectionReferenceModel Financial { get; set; } = new();
    }

    public sealed class ProspectionVisitModel
    {
        public DateTime? Date { get; set; }
        public string? Result { get; set; }
        public string? Observation { get; set; }
        public string? Evidence { get; set; }
    }

    public sealed class ProspectionVisitsModel
    {
        public ProspectionVisitModel Home { get; set; } = new();
        public ProspectionVisitModel Business { get; set; } = new();
    }

    public sealed class CreditBureauSnapshotModel
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
        public List<string> Alerts { get; set; } = [];
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class CreditRequestDto
    {
        public long Id { get; set; }
        public long ClientId { get; set; }
        public string ClientIdentification { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientType { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public decimal Amount { get; set; }
        public int TermMonths { get; set; }
        public decimal AnnualRate { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal SlidingRate { get; set; }
        public decimal MoraRate { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string? Destination { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? Product { get; set; }
        public string Frequency { get; set; } = string.Empty;
        public string InstallmentType { get; set; } = string.Empty;
        public decimal EstimatedInstallment { get; set; }
        public decimal DeclaredIncome { get; set; }
        public decimal DeclaredExpenses { get; set; }
        public decimal PaymentCapacity { get; set; }
        public string? IncomeSource { get; set; }
        public string? FinancedActivity { get; set; }
        public string? GuaranteeType { get; set; }
        public string? GuaranteeDescription { get; set; }
        public decimal GuaranteeValue { get; set; }
        public string? GuarantorName { get; set; }
        public string? GuarantorIdentification { get; set; }
        public string? GuarantorPhone { get; set; }
        public bool RequiresCommittee { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string ConamiClassification { get; set; } = string.Empty;
        public CreditChecklistModel Checklist { get; set; } = new();
        public string ProspectionStage { get; set; } = "PROSPECTO";
        public string? DiscardRejectReason { get; set; }
        public string? Promoter { get; set; }
        public string? Branch { get; set; }
        public string? Office { get; set; }
        public DateTime SystemDate { get; set; }
        public ProspectionReferencesModel References { get; set; } = new();
        public ProspectionVisitsModel Visits { get; set; } = new();
        public CreditBureauSnapshotModel CreditBureau { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string? RegisteredBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? ResolutionUser { get; set; }
        public DateTime? ResolutionDate { get; set; }
        public long? CreditId { get; set; }
        public string? CreditNumber { get; set; }
    }

    public sealed class PlanSummaryDto
    {
        public int Installments { get; set; }
        public decimal EstimatedInstallment { get; set; }
        public decimal TotalCapital { get; set; }
        public decimal TotalInterest { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal UpfrontCommission { get; set; }
        public decimal OtherCharges { get; set; }
        public decimal NetDisbursed { get; set; }
        public decimal TotalSliding { get; set; }
        public decimal TotalToPay { get; set; }
        public decimal AverageInstallment { get; set; }
        public decimal? EffectiveAnnualCostRate { get; set; }
        public DateTime? LastDueDate { get; set; }
    }
}
