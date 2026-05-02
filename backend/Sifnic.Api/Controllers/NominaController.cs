using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Nomina;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class NominaController : Controller
{
    private static readonly string[] PayrollManagerRoles = ["ADMINISTRADOR", "ADMINISTRACION"];

    [HttpGet]
    public IActionResult Contexto()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso al modulo de nomina.",
                });
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var context = BuildContext(connection, branding);

            return Json(new
            {
                ok = true,
                data = context,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el contexto de nomina.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult GuardarConfiguracionEmpresa([FromBody] SaveNominaConfigRequest model)
    {
        if (model is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "No se recibio la configuracion.",
            });
        }

        var regimen = (model.RegimenInssEmpresa ?? string.Empty).Trim().ToUpperInvariant();
        if (regimen is not ("INTEGRAL" or "IVM_RP"))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona un regimen INSS valido.",
            });
        }

        if (model.CantidadTrabajadoresEmpresa < 1)
        {
            return BadRequest(new
            {
                ok = false,
                message = "La cantidad de trabajadores debe ser mayor a cero.",
            });
        }

        var modoPasantia = (model.ModoPasantiaPorDefecto ?? string.Empty).Trim().ToUpperInvariant();
        if (modoPasantia is not ("NO_NOMINA" or "COMO_EMPLEADO"))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona un modo de pasantia valido.",
            });
        }

        if (!(model.DiasMesNomina > 0) || !(model.HorasMesBase > 0))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Los parametros base de dias y horas deben ser mayores a cero.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para actualizar la configuracion de nomina.",
                });
            }

            using var transaction = connection.BeginTransaction();

            UpsertNominaParameter(connection, transaction, "REGIMEN_INSS_EMPRESA", null, regimen, "Regimen INSS aplicado a la empresa.");
            UpsertNominaParameter(connection, transaction, "CANTIDAD_TRABAJADORES_EMPRESA", model.CantidadTrabajadoresEmpresa, null, "Cantidad de trabajadores utilizada para el aporte patronal.");
            UpsertNominaParameter(connection, transaction, "MODO_PASANTIA_POR_DEFECTO", null, modoPasantia, "Tratamiento por defecto para pasantias.");
            UpsertNominaParameter(connection, transaction, "DIAS_MES_NOMINA", model.DiasMesNomina, null, "Dias base mensuales.");
            UpsertNominaParameter(connection, transaction, "HORAS_MES_BASE", model.HorasMesBase, null, "Horas base mensuales.");

            NominaSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CONFIGURACION",
                "ACTUALIZACION",
                0,
                "PARAMETROS_EMPRESA",
                "Se actualizaron los parametros principales de nomina.",
                new
                {
                    regimen,
                    model.CantidadTrabajadoresEmpresa,
                    modoPasantia,
                    model.DiasMesNomina,
                    model.HorasMesBase,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Configuracion guardada correctamente.",
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = NominaSupport.TranslateSqlMessage(ex.Message, "No se pudo guardar la configuracion de nomina."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo guardar la configuracion de nomina.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult AbrirPeriodo([FromBody] OpenPayrollPeriodRequest model)
    {
        if (model is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "No se recibieron los datos del periodo.",
            });
        }

        if (!DateTime.TryParse(model.FechaDesde, out var fechaDesde) ||
            !DateTime.TryParse(model.FechaHasta, out var fechaHasta) ||
            !DateTime.TryParse(model.FechaPago, out var fechaPago))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa fechas validas para el periodo.",
            });
        }

        if (fechaHasta.Date < fechaDesde.Date)
        {
            return BadRequest(new
            {
                ok = false,
                message = "La fecha fin debe ser igual o mayor a la fecha inicio.",
            });
        }

        DateTime? fechaCorteHoraExtra = null;
        if (!string.IsNullOrWhiteSpace(model.FechaCorteHoraExtra))
        {
            if (!DateTime.TryParse(model.FechaCorteHoraExtra, out var fechaCorte))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "La fecha de corte de horas extra no es valida.",
                });
            }

            if (fechaCorte.Date < fechaDesde.Date || fechaCorte.Date > fechaHasta.Date)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "La fecha de corte de horas extra debe estar dentro del periodo.",
                });
            }

            fechaCorteHoraExtra = fechaCorte.Date;
        }

        var tipoPeriodo = string.IsNullOrWhiteSpace(model.TipoPeriodo)
            ? "MENSUAL"
            : model.TipoPeriodo.Trim().ToUpperInvariant();

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para abrir periodos de nomina.",
                });
            }

            using var transaction = connection.BeginTransaction();
            using var command = new SqlCommand("nomina.usp_abrir_periodo_nomina", connection, transaction);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@codigo_periodo", SqlDbType.NVarChar, 30).Value = model.CodigoPeriodo.Trim();
            command.Parameters.Add("@fecha_desde", SqlDbType.Date).Value = fechaDesde.Date;
            command.Parameters.Add("@fecha_hasta", SqlDbType.Date).Value = fechaHasta.Date;
            command.Parameters.Add("@fecha_pago", SqlDbType.Date).Value = fechaPago.Date;
            command.Parameters.Add("@tipo_periodo", SqlDbType.NVarChar, 30).Value = tipoPeriodo;
            command.Parameters.Add("@observacion", SqlDbType.NVarChar, 300).Value =
                string.IsNullOrWhiteSpace(model.Observacion) && !fechaCorteHoraExtra.HasValue
                    ? DBNull.Value
                    : NominaSupport.BuildPeriodObservation(model.Observacion, fechaCorteHoraExtra);

            var idPeriodo = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);

            NominaSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PERIODO_NOMINA",
                "APERTURA",
                idPeriodo,
                model.CodigoPeriodo.Trim(),
                "Se abrio un nuevo periodo de nomina.",
                new
                {
                    model.CodigoPeriodo,
                    fechaDesde = fechaDesde.ToString("yyyy-MM-dd"),
                    fechaHasta = fechaHasta.ToString("yyyy-MM-dd"),
                    fechaPago = fechaPago.ToString("yyyy-MM-dd"),
                    tipoPeriodo,
                    fechaCorteHoraExtra = fechaCorteHoraExtra?.ToString("yyyy-MM-dd"),
                    model.Observacion,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Periodo abierto correctamente.",
                data = new
                {
                    idPeriodoNomina = idPeriodo,
                },
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = NominaSupport.TranslateSqlMessage(ex.Message, "No se pudo abrir el periodo de nomina."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo abrir el periodo de nomina.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Generar([FromBody] GeneratePayrollRequest model)
    {
        if (model is null || model.IdPeriodoNomina <= 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona el periodo que deseas procesar.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para generar la nomina.",
                });
            }

            var usuario = NominaSupport.GetOperatorUser(Request);
            long idNomina;

            using (var command = new SqlCommand("nomina.usp_generar_nomina", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@id_periodo_nomina", SqlDbType.BigInt).Value = model.IdPeriodoNomina;
                command.Parameters.Add("@usuario_generacion", SqlDbType.NVarChar, 100).Value = usuario;
                idNomina = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using var transaction = connection.BeginTransaction();
            NominaSupport.EnsurePayslipRecordsForPayroll(connection, transaction, idNomina, usuario);
            NominaSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "NOMINA",
                "GENERACION",
                idNomina,
                $"NOM-{idNomina}",
                "Se genero una nomina para el periodo solicitado.",
                new
                {
                    model.IdPeriodoNomina,
                    idNomina,
                    usuario,
                },
                usuario);
            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Nomina generada correctamente.",
                data = new
                {
                    idNomina,
                },
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = NominaSupport.TranslateSqlMessage(ex.Message, "No se pudo generar la nomina."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo generar la nomina.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Cerrar([FromBody] ClosePayrollRequest model)
    {
        if (model is null || model.IdNomina <= 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona la nomina que deseas cerrar.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para cerrar la nomina.",
                });
            }

            var usuario = NominaSupport.GetOperatorUser(Request);

            using (var command = new SqlCommand("nomina.usp_cerrar_nomina", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@id_nomina", SqlDbType.BigInt).Value = model.IdNomina;
                command.Parameters.Add("@usuario_cierre", SqlDbType.NVarChar, 100).Value = usuario;
                command.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction();
            NominaSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "NOMINA",
                "CIERRE",
                model.IdNomina,
                $"NOM-{model.IdNomina}",
                "Se cerro la nomina seleccionada.",
                new
                {
                    model.IdNomina,
                    usuario,
                },
                usuario);
            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Nomina cerrada correctamente.",
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = NominaSupport.TranslateSqlMessage(ex.Message, "No se pudo cerrar la nomina."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cerrar la nomina.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult PrevisualizarLiquidacion([FromBody] LiquidationPreviewRequest model)
    {
        var validation = ValidateLiquidationRequest(model);
        if (validation is not null)
        {
            return BadRequest(new
            {
                ok = false,
                message = validation,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para previsualizar liquidaciones.",
                });
            }

            var preview = NominaLiquidationSupport.BuildPreview(connection, null, model);
            if (preview is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontro el colaborador o no tiene contrato vigente.",
                });
            }

            return Json(new
            {
                ok = true,
                data = preview,
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = NominaSupport.TranslateSqlMessage(ex.Message, "No se pudo calcular la liquidacion."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo calcular la liquidacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult GenerarLiquidacion([FromBody] LiquidationPreviewRequest model)
    {
        var validation = ValidateLiquidationRequest(model);
        if (validation is not null)
        {
            return BadRequest(new
            {
                ok = false,
                message = validation,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para generar liquidaciones.",
                });
            }

            var usuario = NominaSupport.GetOperatorUser(Request);
            using var transaction = connection.BeginTransaction();
            var preview = NominaLiquidationSupport.BuildPreview(connection, transaction, model);
            if (preview is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontro el colaborador o no tiene contrato vigente.",
                });
            }

            var storedReason = NominaLiquidationSupport.ComposeLiquidationReason(model.CausalCodigo, model.MotivoLiquidacion);
            long idLiquidacion;

            using (var command = new SqlCommand("nomina.usp_generar_liquidacion", connection, transaction))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = model.IdEmpleado;
                command.Parameters.Add("@fecha_liquidacion", SqlDbType.Date).Value = preview.Header.FechaLiquidacion;
                command.Parameters.Add("@fecha_baja", SqlDbType.Date).Value = preview.Header.FechaBaja;
                command.Parameters.Add("@motivo_liquidacion", SqlDbType.NVarChar, 200).Value = storedReason;
                command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value = usuario;
                command.Parameters.Add("@causal_codigo", SqlDbType.NVarChar, 50).Value = preview.Cause.Code;
                command.Parameters.Add("@dias_salario_pendiente", SqlDbType.Decimal).Value = preview.TaxableSection.PendingSalaryDays;
                command.Parameters["@dias_salario_pendiente"].Precision = 18;
                command.Parameters["@dias_salario_pendiente"].Scale = 2;
                command.Parameters.Add("@monto_salario_pendiente", SqlDbType.Decimal).Value = preview.TaxableSection.PendingSalaryAmount;
                command.Parameters["@monto_salario_pendiente"].Precision = 18;
                command.Parameters["@monto_salario_pendiente"].Scale = 2;
                command.Parameters.Add("@dias_vacaciones", SqlDbType.Decimal).Value = preview.TaxableSection.VacationDays;
                command.Parameters["@dias_vacaciones"].Precision = 18;
                command.Parameters["@dias_vacaciones"].Scale = 2;
                command.Parameters.Add("@monto_vacaciones", SqlDbType.Decimal).Value = preview.TaxableSection.VacationAmount;
                command.Parameters["@monto_vacaciones"].Precision = 18;
                command.Parameters["@monto_vacaciones"].Scale = 2;
                command.Parameters.Add("@dias_aguinaldo", SqlDbType.Decimal).Value = preview.NonTaxableSection.AguinaldoDays;
                command.Parameters["@dias_aguinaldo"].Precision = 18;
                command.Parameters["@dias_aguinaldo"].Scale = 2;
                command.Parameters.Add("@monto_aguinaldo", SqlDbType.Decimal).Value = preview.NonTaxableSection.AguinaldoAmount;
                command.Parameters["@monto_aguinaldo"].Precision = 18;
                command.Parameters["@monto_aguinaldo"].Scale = 2;
                command.Parameters.Add("@dias_indemnizacion", SqlDbType.Decimal).Value = preview.NonTaxableSection.IndemnizationDays;
                command.Parameters["@dias_indemnizacion"].Precision = 18;
                command.Parameters["@dias_indemnizacion"].Scale = 2;
                command.Parameters.Add("@monto_indemnizacion", SqlDbType.Decimal).Value = preview.NonTaxableSection.IndemnizationAmount;
                command.Parameters["@monto_indemnizacion"].Precision = 18;
                command.Parameters["@monto_indemnizacion"].Scale = 2;
                command.Parameters.Add("@inss_laboral", SqlDbType.Decimal).Value = preview.Deductions.InssLaboral;
                command.Parameters["@inss_laboral"].Precision = 18;
                command.Parameters["@inss_laboral"].Scale = 2;
                command.Parameters.Add("@ir_laboral", SqlDbType.Decimal).Value = preview.Deductions.IrLaboral;
                command.Parameters["@ir_laboral"].Precision = 18;
                command.Parameters["@ir_laboral"].Scale = 2;
                command.Parameters.Add("@inss_patronal", SqlDbType.Decimal).Value = preview.EmployerContributions.InssPatronal;
                command.Parameters["@inss_patronal"].Precision = 18;
                command.Parameters["@inss_patronal"].Scale = 2;
                command.Parameters.Add("@inatec_patronal", SqlDbType.Decimal).Value = preview.EmployerContributions.InatecPatronal;
                command.Parameters["@inatec_patronal"].Precision = 18;
                command.Parameters["@inatec_patronal"].Scale = 2;

                using var reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    throw new InvalidOperationException("El procedimiento de liquidacion no devolvio el identificador generado.");
                }

                idLiquidacion = reader.GetInt64(0);
            }

            NominaLiquidationSupport.DeactivateRelatedSecurityUser(connection, transaction, preview.Header);

            NominaSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "LIQUIDACION",
                "GENERACION",
                idLiquidacion,
                $"LIQ-{idLiquidacion}",
                $"Se genero la liquidacion final del empleado {preview.Header.CodigoEmpleado}.",
                new
                {
                    idLiquidacion,
                    preview.Header.IdEmpleado,
                    preview.Header.CodigoEmpleado,
                    preview.Cause.Code,
                    preview.Cause.Label,
                    preview.Header.FechaBaja,
                    preview.Totals.TotalIngresos,
                    preview.Totals.TotalDeducciones,
                    preview.Totals.NetoLiquidacion,
                },
                usuario);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Liquidacion generada y empleado retirado correctamente.",
                data = new
                {
                    idLiquidacion,
                    preview.Totals.NetoLiquidacion,
                },
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = NominaSupport.TranslateSqlMessage(ex.Message, "No se pudo generar la liquidacion."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo generar la liquidacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult ObtenerLiquidacion(long idLiquidacion)
    {
        if (idLiquidacion <= 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona una liquidacion valida.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para consultar liquidaciones.",
                });
            }

            var result = NominaLiquidationSupport.BuildDetail(connection, idLiquidacion);
            if (result is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "La liquidacion solicitada no existe.",
                });
            }

            return Json(new
            {
                ok = true,
                data = result,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el detalle de la liquidacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult LiquidacionHtml(long idLiquidacion)
    {
        if (idLiquidacion <= 0)
        {
            return BadRequest("Liquidacion invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, "La sesion no es valida o ya vencio.");
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, "No tienes acceso para ver esta liquidacion.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var payload = NominaLiquidationSupport.BuildDetail(connection, idLiquidacion);
            if (payload is null)
            {
                return NotFound("No se encontro la liquidacion solicitada.");
            }

            var html = NominaLiquidationSupport.BuildLiquidationHtml(payload, branding);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar la liquidacion: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult LiquidacionExcel(long idLiquidacion)
    {
        if (idLiquidacion <= 0)
        {
            return BadRequest("Liquidacion invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, "La sesion no es valida o ya vencio.");
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, "No tienes acceso para exportar esta liquidacion.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var payload = NominaLiquidationSupport.BuildDetail(connection, idLiquidacion);
            if (payload is null)
            {
                return NotFound("No se encontro la liquidacion solicitada.");
            }

            var workbookHtml = NominaLiquidationSupport.BuildLiquidationExcel(payload, branding);
            var fileName = $"Liquidacion-{SanitizeFileNamePart(payload.Header.CodigoEmpleado)}-{payload.IdLiquidacion ?? idLiquidacion}.xls";
            var bodyBytes = Encoding.UTF8.GetBytes(workbookHtml);
            var preamble = Encoding.UTF8.GetPreamble();
            var bytes = new byte[preamble.Length + bodyBytes.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(bodyBytes, 0, bytes, preamble.Length, bodyBytes.Length);
            return File(bytes, "application/vnd.ms-excel", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar el Excel de la liquidacion: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult CartaRecomendacionHtml(long idLiquidacion)
    {
        if (idLiquidacion <= 0)
        {
            return BadRequest("Liquidacion invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, "La sesion no es valida o ya vencio.");
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, "No tienes acceso para emitir esta carta.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var payload = NominaLiquidationSupport.BuildDetail(connection, idLiquidacion);
            if (payload is null)
            {
                return NotFound("No se encontro la liquidacion solicitada.");
            }

            var html = NominaLiquidationSupport.BuildRecommendationLetterHtml(payload, branding);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar la carta de recomendacion: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult ObtenerNomina(long idNomina)
    {
        if (idNomina <= 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona una nomina valida.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes acceso para consultar esta nomina.",
                });
            }

            var result = BuildPayrollDetail(connection, idNomina);
            if (result is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "La nomina solicitada no existe.",
                });
            }

            return Json(new
            {
                ok = true,
                data = result,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el detalle de la nomina.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult EsquelaHtml(long idNominaDetalle)
    {
        if (idNominaDetalle <= 0)
        {
            return BadRequest("Detalle de nomina invalido.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, "La sesion no es valida o ya vencio.");
            }

            if (!PuedeVerEsquela(connection, session, idNominaDetalle))
            {
                return StatusCode(403, "No tienes permiso para ver esta esquela de pago.");
            }

            NominaSupport.EnsurePayslipRecord(connection, null, idNominaDetalle, session.Username);
            var branding = NominaSupport.GetReportBranding(connection);
            var payload = BuildPayslipPayload(connection, idNominaDetalle);
            if (payload is null)
            {
                return NotFound("No se encontro la esquela solicitada.");
            }

            var html = BuildPayslipHtmlV2(payload, branding);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar la esquela: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult ReporteGeneralHtml(long idNomina)
    {
        if (idNomina <= 0)
        {
            return BadRequest("Nomina invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, "La sesion no es valida o ya vencio.");
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, "No tienes acceso para ver el reporte general de nomina.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            dynamic detail = BuildPayrollDetail(connection, idNomina)!;
            if (detail is null)
            {
                return NotFound("La nomina solicitada no existe.");
            }

            var html = BuildGeneralReportHtmlV2(detail, branding);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar el reporte general: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult ReporteGeneralExcel(long idNomina)
    {
        if (idNomina <= 0)
        {
            return BadRequest("Nomina invalida.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            NominaSupport.EnsureNominaSetup(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, "La sesion no es valida o ya vencio.");
            }

            if (!TieneRol(session, PayrollManagerRoles))
            {
                return StatusCode(403, "No tienes acceso al reporte general de nomina.");
            }

            var branding = NominaSupport.GetReportBranding(connection);
            var detailPayload = BuildPayrollDetail(connection, idNomina);
            if (detailPayload is null)
            {
                return NotFound("La nomina solicitada no existe.");
            }

            dynamic detail = detailPayload;
            var workbookHtml = BuildGeneralReportExcel(detail, branding);
            var fileName = $"Reporte-Nomina-{SanitizeFileNamePart((string)detail.run.periodCode)}.xls";
            var bodyBytes = Encoding.UTF8.GetBytes(workbookHtml);
            var preamble = Encoding.UTF8.GetPreamble();
            var bytes = new byte[preamble.Length + bodyBytes.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(bodyBytes, 0, bytes, preamble.Length, bodyBytes.Length);
            return File(bytes, "application/vnd.ms-excel", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo generar el Excel del reporte general: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    private static string? ValidateLiquidationRequest(LiquidationPreviewRequest? model)
    {
        if (model is null || model.IdEmpleado <= 0)
        {
            return "Selecciona un colaborador valido para la liquidacion.";
        }

        if (!DateTime.TryParse(model.FechaLiquidacion, out var fechaLiquidacion) ||
            !DateTime.TryParse(model.FechaBaja, out var fechaBaja))
        {
            return "Ingresa una fecha valida para la liquidacion y el retiro.";
        }

        if (fechaBaja.Date < fechaLiquidacion.Date)
        {
            return "La fecha de baja no puede ser menor que la fecha de liquidacion.";
        }

        if (model.DiasSalarioPendiente.HasValue && model.DiasSalarioPendiente.Value < 0m)
        {
            return "Los dias de salario pendiente no pueden ser negativos.";
        }

        if (string.IsNullOrWhiteSpace(model.CausalCodigo))
        {
            return "Selecciona la causal de retiro.";
        }

        if (string.IsNullOrWhiteSpace(model.MotivoLiquidacion))
        {
            return "Describe el motivo o detalle del retiro.";
        }

        return null;
    }

    private static object BuildContext(SqlConnection connection, ReportBrandingDto branding)
    {
        const string sql = """
            ;WITH parametros AS
            (
                SELECT
                    codigo_parametro,
                    valor_decimal,
                    valor_texto,
                    descripcion,
                    ROW_NUMBER() OVER (PARTITION BY codigo_parametro ORDER BY id_parametro_nomina DESC) AS rn
                FROM nomina.parametro_nomina
                WHERE activo = 1
            )
            SELECT
                MAX(CASE WHEN codigo_parametro = N'REGIMEN_INSS_EMPRESA' THEN valor_texto END) AS regimen_inss_empresa,
                MAX(CASE WHEN codigo_parametro = N'CANTIDAD_TRABAJADORES_EMPRESA' THEN valor_decimal END) AS cantidad_trabajadores_empresa,
                MAX(CASE WHEN codigo_parametro = N'MODO_PASANTIA_POR_DEFECTO' THEN valor_texto END) AS modo_pasantia_por_defecto,
                MAX(CASE WHEN codigo_parametro = N'DIAS_MES_NOMINA' THEN valor_decimal END) AS dias_mes_nomina,
                MAX(CASE WHEN codigo_parametro = N'HORAS_MES_BASE' THEN valor_decimal END) AS horas_mes_base
            FROM parametros
            WHERE rn = 1;

            SELECT
                codigo_contribucion,
                nombre_contribucion,
                tipo_contribucion,
                vigencia_desde,
                vigencia_hasta,
                porcentaje,
                techo_mensual
            FROM nomina.parametro_contribucion
            WHERE activo = 1
            ORDER BY codigo_contribucion, vigencia_desde DESC;

            SELECT
                id_tabla_ir_laboral,
                vigencia_desde,
                vigencia_hasta,
                tramo_desde_anual,
                tramo_hasta_anual,
                base_impuesto_anual,
                porcentaje_exceso
            FROM nomina.tabla_ir_laboral
            WHERE activo = 1
            ORDER BY vigencia_desde DESC, tramo_desde_anual;

            SELECT
                p.id_periodo_nomina,
                p.codigo_periodo,
                p.fecha_desde,
                p.fecha_hasta,
                p.fecha_pago,
                p.tipo_periodo,
                p.observacion,
                ep.codigo_estado_periodo,
                ep.nombre_estado_periodo
            FROM nomina.periodo_nomina p
            INNER JOIN nomina.estado_periodo_nomina ep
                ON ep.id_estado_periodo_nomina = p.id_estado_periodo_nomina
            ORDER BY p.id_periodo_nomina DESC;

            SELECT
                n.id_nomina,
                n.id_periodo_nomina,
                p.codigo_periodo,
                p.fecha_desde,
                p.fecha_hasta,
                p.fecha_pago,
                p.tipo_periodo,
                en.codigo_estado_nomina,
                en.nombre_estado_nomina,
                n.fecha_generacion,
                n.usuario_generacion,
                n.fecha_cierre,
                n.usuario_cierre,
                COUNT(nd.id_nomina_detalle) AS colaboradores,
                COALESCE(SUM(nd.total_ingresos), 0) AS total_ingresos,
                COALESCE(SUM(nd.total_deducciones), 0) AS total_deducciones,
                COALESCE(SUM(nd.total_aportes_patronales), 0) AS total_aportes_patronales,
                COALESCE(SUM(nd.neto_pagar), 0) AS total_neto
            FROM nomina.nomina n
            INNER JOIN nomina.periodo_nomina p
                ON p.id_periodo_nomina = n.id_periodo_nomina
            INNER JOIN nomina.estado_nomina en
                ON en.id_estado_nomina = n.id_estado_nomina
            LEFT JOIN nomina.nomina_detalle nd
                ON nd.id_nomina = n.id_nomina
            GROUP BY
                n.id_nomina,
                n.id_periodo_nomina,
                p.codigo_periodo,
                p.fecha_desde,
                p.fecha_hasta,
                p.fecha_pago,
                p.tipo_periodo,
                en.codigo_estado_nomina,
                en.nombre_estado_nomina,
                n.fecha_generacion,
                n.usuario_generacion,
                n.fecha_cierre,
                n.usuario_cierre
            ORDER BY n.id_nomina DESC;

            SELECT
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato,
                COUNT(1) AS total
            FROM rrhh.contrato c
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = c.id_empleado
               AND e.activo = 1
            INNER JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
               AND ee.codigo_estado_empleado = N'ACTIVO'
            WHERE c.es_contrato_vigente = 1
            GROUP BY tc.codigo_tipo_contrato, tc.nombre_tipo_contrato
            ORDER BY tc.nombre_tipo_contrato;

            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.fecha_ingreso,
                e.cedula,
                e.inss,
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
            INNER JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
               AND ee.codigo_estado_empleado = N'ACTIVO'
            WHERE e.activo = 1
            ORDER BY nombre_empleado;

            SELECT
                l.id_liquidacion,
                l.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                cg.nombre_cargo,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato,
                c.moneda,
                l.fecha_liquidacion,
                l.fecha_baja,
                l.motivo_liquidacion,
                l.salario_base_referencia,
                l.total_ingresos,
                l.total_deducciones,
                l.neto_liquidacion,
                l.usuario_registro,
                l.fecha_registro
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
            ORDER BY l.id_liquidacion DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        object config = new
        {
            regimenInssEmpresa = "INTEGRAL",
            cantidadTrabajadoresEmpresa = 1,
            modoPasantiaPorDefecto = "NO_NOMINA",
            diasMesNomina = 30m,
            horasMesBase = 240m,
        };

        if (reader.Read())
        {
            config = new
            {
                regimenInssEmpresa = reader.IsDBNull(0) ? "INTEGRAL" : reader.GetString(0).Trim().ToUpperInvariant(),
                cantidadTrabajadoresEmpresa = reader.IsDBNull(1) ? 1m : reader.GetDecimal(1),
                modoPasantiaPorDefecto = reader.IsDBNull(2) ? "NO_NOMINA" : reader.GetString(2).Trim().ToUpperInvariant(),
                diasMesNomina = reader.IsDBNull(3) ? 30m : reader.GetDecimal(3),
                horasMesBase = reader.IsDBNull(4) ? 240m : reader.GetDecimal(4),
            };
        }

        reader.NextResult();
        var contributions = new List<object>();
        while (reader.Read())
        {
            contributions.Add(new
            {
                code = reader.GetString(0),
                name = reader.GetString(1),
                type = reader.GetString(2),
                startDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                endDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4).ToString("yyyy-MM-dd"),
                percent = reader.GetDecimal(5),
                monthlyCap = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
            });
        }

        reader.NextResult();
        var irTable = new List<object>();
        while (reader.Read())
        {
            irTable.Add(new
            {
                id = reader.GetInt64(0),
                startDate = reader.GetDateTime(1).ToString("yyyy-MM-dd"),
                endDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2).ToString("yyyy-MM-dd"),
                annualFrom = reader.GetDecimal(3),
                annualTo = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4),
                annualBaseTax = reader.GetDecimal(5),
                excessPercent = reader.GetDecimal(6),
            });
        }

        reader.NextResult();
        var periods = new List<object>();
        while (reader.Read())
        {
            var observation = NominaSupport.ParsePeriodObservation(reader.IsDBNull(6) ? null : reader.GetString(6));
            periods.Add(new
            {
                id = reader.GetInt64(0),
                code = reader.GetString(1),
                startDate = reader.GetDateTime(2).ToString("yyyy-MM-dd"),
                endDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                payDate = reader.GetDateTime(4).ToString("yyyy-MM-dd"),
                periodType = reader.GetString(5),
                observation = observation.Note ?? string.Empty,
                overtimeCutoffDate = observation.CutoffDate?.ToString("yyyy-MM-dd"),
                status = reader.GetString(7),
                statusLabel = reader.GetString(8),
            });
        }

        reader.NextResult();
        var payrolls = new List<object>();
        while (reader.Read())
        {
            payrolls.Add(new
            {
                id = reader.GetInt64(0),
                periodId = reader.GetInt64(1),
                periodCode = reader.GetString(2),
                startDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                endDate = reader.GetDateTime(4).ToString("yyyy-MM-dd"),
                payDate = reader.GetDateTime(5).ToString("yyyy-MM-dd"),
                periodType = reader.GetString(6),
                status = reader.GetString(7),
                statusLabel = reader.GetString(8),
                generatedAt = reader.GetDateTime(9).ToString("yyyy-MM-ddTHH:mm:ss"),
                generatedBy = reader.GetString(10),
                closedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11).ToString("yyyy-MM-ddTHH:mm:ss"),
                closedBy = reader.IsDBNull(12) ? null : reader.GetString(12),
                employees = reader.GetInt32(13),
                totalIncome = reader.GetDecimal(14),
                totalDeductions = reader.GetDecimal(15),
                totalEmployerCost = reader.GetDecimal(14) + reader.GetDecimal(16),
                totalEmployerContribution = reader.GetDecimal(16),
                totalNet = reader.GetDecimal(17),
            });
        }

        reader.NextResult();
        var contractPopulation = new List<object>();
        while (reader.Read())
        {
            contractPopulation.Add(new
            {
                code = reader.GetString(0),
                name = reader.GetString(1),
                total = reader.GetInt32(2),
            });
        }

        reader.NextResult();
        var liquidationCandidates = new List<object>();
        while (reader.Read())
        {
            liquidationCandidates.Add(new
            {
                id = reader.GetInt64(0),
                code = reader.GetString(1),
                name = reader.GetString(2),
                joinDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                cedula = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                inss = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                department = reader.GetString(6),
                cargo = reader.GetString(7),
                contractId = reader.GetInt64(8),
                contractCode = reader.GetString(9),
                contractName = reader.GetString(10),
                salaryBase = reader.GetDecimal(11),
                currency = reader.IsDBNull(12) ? "NIO" : reader.GetString(12),
            });
        }

        reader.NextResult();
        var liquidations = new List<object>();
        while (reader.Read())
        {
            var reason = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
            var parsedReason = NominaLiquidationSupport.ParseLiquidationReason(reason);

            liquidations.Add(new
            {
                id = reader.GetInt64(0),
                employeeId = reader.GetInt64(1),
                employeeCode = reader.GetString(2),
                employeeName = reader.GetString(3),
                department = reader.GetString(4),
                cargo = reader.GetString(5),
                contractCode = reader.GetString(6),
                contractName = reader.GetString(7),
                currency = reader.IsDBNull(8) ? "NIO" : reader.GetString(8),
                liquidationDate = reader.GetDateTime(9).ToString("yyyy-MM-dd"),
                terminationDate = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
                reason,
                causeCode = parsedReason.Code,
                causeLabel = NominaLiquidationSupport.GetLiquidationCauseLabel(parsedReason.Code),
                reasonNote = parsedReason.Note,
                salaryBase = reader.GetDecimal(12),
                totalIncome = reader.GetDecimal(13),
                totalDeductions = reader.GetDecimal(14),
                netAmount = reader.GetDecimal(15),
                registeredBy = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                registeredAt = reader.GetDateTime(17).ToString("yyyy-MM-ddTHH:mm:ss"),
            });
        }

        return new
        {
            branding,
            config,
            contributions,
            irTable,
            periods,
            payrolls,
            contractPopulation,
            liquidationCandidates,
            liquidations,
            liquidationCauses = NominaLiquidationSupport.BuildLiquidationCauses(),
        };
    }

    private static object? BuildPayrollDetail(SqlConnection connection, long idNomina)
    {
        const string sql = """
            SELECT
                n.id_nomina,
                p.id_periodo_nomina,
                p.codigo_periodo,
                p.fecha_desde,
                p.fecha_hasta,
                p.fecha_pago,
                p.tipo_periodo,
                n.observacion,
                en.codigo_estado_nomina,
                en.nombre_estado_nomina,
                n.fecha_generacion,
                n.usuario_generacion,
                n.fecha_cierre,
                n.usuario_cierre
            FROM nomina.nomina n
            INNER JOIN nomina.periodo_nomina p
                ON p.id_periodo_nomina = n.id_periodo_nomina
            INNER JOIN nomina.estado_nomina en
                ON en.id_estado_nomina = n.id_estado_nomina
            WHERE n.id_nomina = @id_nomina;

            SELECT
                nd.id_nomina_detalle,
                nd.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.cedula,
                e.inss,
                e.correo,
                e.numero_cuenta_bancaria,
                d.nombre_departamento,
                cg.nombre_cargo,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato,
                COALESCE((SELECT TOP (1) COALESCE(NULLIF(moneda_base, N''), N'NIO') FROM empresa.empresa ORDER BY id_empresa), N'NIO') AS moneda_pago,
                nd.salario_base_periodo,
                nd.total_ingresos,
                nd.total_deducciones,
                nd.total_aportes_patronales,
                nd.neto_pagar,
                nd.inss_laboral,
                nd.inss_patronal,
                nd.ir_laboral,
                nd.ir_patronal
            FROM nomina.nomina_detalle nd
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = nd.id_empleado
            INNER JOIN rrhh.contrato c
                ON c.id_contrato = nd.id_contrato
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo cg
                ON cg.id_cargo = e.id_cargo
            WHERE nd.id_nomina = @id_nomina
            ORDER BY nombre_empleado;

            SELECT
                ndc.id_nomina_detalle_concepto,
                ndc.id_nomina_detalle,
                cn.codigo_concepto,
                cn.nombre_concepto,
                tcn.codigo_tipo_concepto,
                ndc.monto,
                ndc.referencia,
                cn.orden_visual
            FROM nomina.nomina_detalle_concepto ndc
            INNER JOIN nomina.concepto_nomina cn
                ON cn.id_concepto_nomina = ndc.id_concepto_nomina
            INNER JOIN nomina.tipo_concepto_nomina tcn
                ON tcn.id_tipo_concepto_nomina = cn.id_tipo_concepto_nomina
            INNER JOIN nomina.nomina_detalle nd
                ON nd.id_nomina_detalle = ndc.id_nomina_detalle
            WHERE nd.id_nomina = @id_nomina
            ORDER BY ndc.id_nomina_detalle, cn.orden_visual, ndc.id_nomina_detalle_concepto;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_nomina", SqlDbType.BigInt).Value = idNomina;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var observation = reader.IsDBNull(7) ? null : reader.GetString(7);
        var snapshot = ParseJsonObject(observation);

        var run = new
        {
            id = reader.GetInt64(0),
            periodId = reader.GetInt64(1),
            periodCode = reader.GetString(2),
            startDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
            endDate = reader.GetDateTime(4).ToString("yyyy-MM-dd"),
            payDate = reader.GetDateTime(5).ToString("yyyy-MM-dd"),
            periodType = reader.GetString(6),
            configSnapshot = snapshot,
            status = reader.GetString(8),
            statusLabel = reader.GetString(9),
            generatedAt = reader.GetDateTime(10).ToString("yyyy-MM-ddTHH:mm:ss"),
            generatedBy = reader.GetString(11),
            closedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12).ToString("yyyy-MM-ddTHH:mm:ss"),
            closedBy = reader.IsDBNull(13) ? null : reader.GetString(13),
        };

        reader.NextResult();
        var details = new List<PayrollDetailRowDto>();
        while (reader.Read())
        {
            var type = NominaSupport.ResolvePayrollType(
                reader.GetString(10),
                reader.GetDecimal(20),
                reader.GetDecimal(18),
                reader.GetDecimal(19));

            details.Add(new PayrollDetailRowDto
            {
                IdNominaDetalle = reader.GetInt64(0),
                IdEmpleado = reader.GetInt64(1),
                CodigoEmpleado = reader.GetString(2),
                NombreEmpleado = reader.GetString(3),
                Cedula = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Inss = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Correo = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                CuentaBancaria = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Departamento = reader.GetString(8),
                Cargo = reader.GetString(9),
                CodigoTipoContrato = reader.GetString(10),
                NombreTipoContrato = reader.GetString(11),
                Moneda = reader.GetString(12),
                SalarioBasePeriodo = reader.GetDecimal(13),
                TotalIngresos = reader.GetDecimal(14),
                TotalDeducciones = reader.GetDecimal(15),
                TotalAportesPatronales = reader.GetDecimal(16),
                NetoPagar = reader.GetDecimal(17),
                InssLaboral = reader.GetDecimal(18),
                InssPatronal = reader.GetDecimal(19),
                IrRetenido = reader.GetDecimal(20),
                IrPatronal = reader.GetDecimal(21),
                TipoPago = type.ToString().ToUpperInvariant(),
            });
        }

        reader.NextResult();
        var concepts = new List<object>();
        while (reader.Read())
        {
            concepts.Add(new
            {
                id = reader.GetInt64(0),
                detailId = reader.GetInt64(1),
                code = reader.GetString(2),
                name = reader.GetString(3),
                conceptType = reader.GetString(4),
                amount = reader.GetDecimal(5),
                reference = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                visualOrder = reader.GetInt32(7),
            });
        }

        var summary = new
        {
            totalRecords = details.Count,
            totalBrutoNomina = details.Where(detail => detail.TipoPago == "EMPLEADONOMINA").Sum(detail => detail.TotalIngresos),
            totalPasantes = details.Where(detail => detail.TipoPago == "PASANTEAYUDA").Sum(detail => detail.TotalIngresos),
            totalServicios = details.Where(detail => detail.TipoPago == "SERVICIOPROFESIONAL").Sum(detail => detail.TotalIngresos),
            totalInssLaboral = details.Sum(detail => detail.InssLaboral),
            totalInssPatronal = details.Sum(detail => detail.InssPatronal),
            totalIrTrabajadores = details.Where(detail => detail.TipoPago == "EMPLEADONOMINA").Sum(detail => detail.IrRetenido),
            totalRetencionesServicios = details.Where(detail => detail.TipoPago == "SERVICIOPROFESIONAL").Sum(detail => detail.IrRetenido),
            totalNeto = details.Sum(detail => detail.NetoPagar),
            totalCostoEmpresa = details.Sum(detail => detail.TotalIngresos + detail.TotalAportesPatronales),
        };

        return new
        {
            run,
            details,
            concepts,
            summary,
        };
    }

    private static PayslipPayload? BuildPayslipPayload(SqlConnection connection, long idNominaDetalle)
    {
        const string sql = """
            SELECT
                nd.id_nomina_detalle,
                nd.id_nomina,
                nd.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.cedula,
                e.inss,
                e.numero_cuenta_bancaria,
                d.nombre_departamento,
                cg.nombre_cargo,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato,
                COALESCE((SELECT TOP (1) COALESCE(NULLIF(moneda_base, N''), N'NIO') FROM empresa.empresa ORDER BY id_empresa), N'NIO') AS moneda_pago,
                nd.salario_base_periodo,
                nd.total_ingresos,
                nd.total_deducciones,
                nd.total_aportes_patronales,
                nd.neto_pagar,
                nd.inss_laboral,
                nd.inss_patronal,
                nd.ir_laboral,
                p.codigo_periodo,
                p.fecha_desde,
                p.fecha_hasta,
                p.fecha_pago
            FROM nomina.nomina_detalle nd
            INNER JOIN nomina.nomina n
                ON n.id_nomina = nd.id_nomina
            INNER JOIN nomina.periodo_nomina p
                ON p.id_periodo_nomina = n.id_periodo_nomina
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = nd.id_empleado
            INNER JOIN rrhh.contrato c
                ON c.id_contrato = nd.id_contrato
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo cg
                ON cg.id_cargo = e.id_cargo
            WHERE nd.id_nomina_detalle = @id_nomina_detalle;

            SELECT
                cn.nombre_concepto,
                tcn.codigo_tipo_concepto,
                ndc.monto,
                ndc.referencia,
                cn.orden_visual
            FROM nomina.nomina_detalle_concepto ndc
            INNER JOIN nomina.concepto_nomina cn
                ON cn.id_concepto_nomina = ndc.id_concepto_nomina
            INNER JOIN nomina.tipo_concepto_nomina tcn
                ON tcn.id_tipo_concepto_nomina = cn.id_tipo_concepto_nomina
            WHERE ndc.id_nomina_detalle = @id_nomina_detalle
            ORDER BY cn.orden_visual, ndc.id_nomina_detalle_concepto;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_nomina_detalle", SqlDbType.BigInt).Value = idNominaDetalle;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var payload = new PayslipPayload
        {
            IdNominaDetalle = reader.GetInt64(0),
            IdNomina = reader.GetInt64(1),
            IdEmpleado = reader.GetInt64(2),
            CodigoEmpleado = reader.GetString(3),
            NombreEmpleado = reader.GetString(4),
            Cedula = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            Inss = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            CuentaBancaria = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            Departamento = reader.GetString(8),
            Cargo = reader.GetString(9),
            CodigoTipoContrato = reader.GetString(10),
            NombreTipoContrato = reader.GetString(11),
            Moneda = reader.GetString(12),
            SalarioBasePeriodo = reader.GetDecimal(13),
            TotalIngresos = reader.GetDecimal(14),
            TotalDeducciones = reader.GetDecimal(15),
            TotalAportesPatronales = reader.GetDecimal(16),
            NetoPagar = reader.GetDecimal(17),
            InssLaboral = reader.GetDecimal(18),
            InssPatronal = reader.GetDecimal(19),
            IrRetenido = reader.GetDecimal(20),
            CodigoPeriodo = reader.GetString(21),
            FechaDesde = reader.GetDateTime(22),
            FechaHasta = reader.GetDateTime(23),
            FechaPago = reader.GetDateTime(24),
        };

        reader.NextResult();
        while (reader.Read())
        {
            payload.Concepts.Add(new PayslipConcept
            {
                Name = reader.GetString(0),
                ConceptType = reader.GetString(1),
                Amount = reader.GetDecimal(2),
                Reference = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                VisualOrder = reader.GetInt32(4),
            });
        }

        return payload;
    }

    private static void EnsurePayslipRecord(SqlConnection connection, long idNominaDetalle, string usuario)
    {
        const string existsSql = """
            SELECT TOP (1) id_esquela_pago
            FROM nomina.esquela_pago
            WHERE id_nomina_detalle = @id_nomina_detalle
            ORDER BY id_esquela_pago DESC;
            """;

        using (var existsCommand = new SqlCommand(existsSql, connection))
        {
            existsCommand.Parameters.Add("@id_nomina_detalle", SqlDbType.BigInt).Value = idNominaDetalle;
            var existingId = existsCommand.ExecuteScalar();
            if (existingId is not null && existingId != DBNull.Value)
            {
                return;
            }
        }

        using var command = new SqlCommand("nomina.usp_generar_esquela_pago", connection);
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@id_nomina_detalle", SqlDbType.BigInt).Value = idNominaDetalle;
        command.Parameters.Add("@nombre_archivo", SqlDbType.NVarChar, 255).Value = $"esquela-{idNominaDetalle}.html";
        command.Parameters.Add("@ruta_archivo", SqlDbType.NVarChar, 500).Value = DBNull.Value;
        command.Parameters.Add("@contenido_base64", SqlDbType.NVarChar).Value = DBNull.Value;
        command.Parameters.Add("@hash_documento", SqlDbType.NVarChar, 200).Value = DBNull.Value;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value = "Vista previa HTML generada desde el modulo de nomina.";
        command.Parameters.Add("@usuario_generacion", SqlDbType.NVarChar, 100).Value = usuario;
        command.ExecuteScalar();
    }

    private static void UpsertNominaParameter(
        SqlConnection connection,
        SqlTransaction transaction,
        string code,
        decimal? decimalValue,
        string? textValue,
        string description)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM nomina.parametro_nomina WHERE codigo_parametro = @codigo_parametro)
            BEGIN
                UPDATE nomina.parametro_nomina
                SET
                    valor_decimal = @valor_decimal,
                    valor_texto = @valor_texto,
                    descripcion = @descripcion,
                    activo = 1
                WHERE codigo_parametro = @codigo_parametro;
            END
            ELSE
            BEGIN
                INSERT INTO nomina.parametro_nomina
                (
                    codigo_parametro,
                    valor_decimal,
                    valor_texto,
                    descripcion,
                    activo,
                    fecha_registro
                )
                VALUES
                (
                    @codigo_parametro,
                    @valor_decimal,
                    @valor_texto,
                    @descripcion,
                    1,
                    SYSDATETIME()
                );
            END;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@codigo_parametro", SqlDbType.NVarChar, 100).Value = code;
        command.Parameters.Add("@valor_decimal", SqlDbType.Decimal).Value = decimalValue.HasValue ? decimalValue.Value : DBNull.Value;
        command.Parameters["@valor_decimal"].Precision = 18;
        command.Parameters["@valor_decimal"].Scale = 6;
        command.Parameters.Add("@valor_texto", SqlDbType.NVarChar, 400).Value = string.IsNullOrWhiteSpace(textValue) ? DBNull.Value : textValue.Trim();
        command.Parameters.Add("@descripcion", SqlDbType.NVarChar, 600).Value = description;
        command.ExecuteNonQuery();
    }

    private static Dictionary<string, object?> ParseJsonObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.TryGetDecimal(out var number) ? number : property.Value.GetRawText(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => property.Value.GetRawText(),
                };
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, object?>
            {
                ["raw"] = raw,
            };
        }
    }

    private static string BuildPayslipHtml(PayslipPayload payload, ReportBrandingDto branding)
    {
        var incomeRows = payload.Concepts
            .Where(concept => string.Equals(concept.ConceptType, "INGRESO", StringComparison.OrdinalIgnoreCase))
            .OrderBy(concept => concept.VisualOrder)
            .ToList();
        var deductionRows = payload.Concepts
            .Where(concept => string.Equals(concept.ConceptType, "DEDUCCION", StringComparison.OrdinalIgnoreCase))
            .OrderBy(concept => concept.VisualOrder)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Esquela de pago</title>");
        builder.AppendLine("""
            <style>
              :root { color-scheme: light; }
              * { box-sizing: border-box; }
              body { margin: 0; font-family: Arial, sans-serif; background: #0e141d; color: #f5f7fa; }
              .page { width: 100%; min-height: 100vh; padding: 28px; background: linear-gradient(180deg, #121b27, #0d141e); }
              .sheet { max-width: 1120px; margin: 0 auto; background: #151d28; border: 1px solid rgba(255,255,255,.1); padding: 28px; }
              .top { display: grid; grid-template-columns: auto 1fr auto; gap: 20px; align-items: center; }
              .logo-box { width: 84px; height: 84px; border-radius: 18px; border: 1px solid rgba(255,255,255,.12); display: grid; place-items: center; background: rgba(255,255,255,.04); color: #45d2bc; font-size: 28px; font-weight: 700; overflow: hidden; }
              .logo-box img { width: 100%; height: 100%; object-fit: contain; }
              .company h1 { margin: 0; font-size: 30px; }
              .company small { display: block; margin-top: 6px; color: #b8c2d0; }
              .pill { display: inline-flex; align-items: center; min-height: 34px; padding: 0 14px; border-radius: 999px; background: rgba(69,210,188,.12); color: #8ce7d9; font-weight: 700; }
              .grid { display: grid; grid-template-columns: repeat(4, minmax(0,1fr)); gap: 12px; margin-top: 22px; }
              .card { border: 1px solid rgba(255,255,255,.08); background: rgba(255,255,255,.03); padding: 14px 16px; min-height: 88px; }
              .card span { display: block; color: #99a8bb; font-size: 12px; text-transform: uppercase; letter-spacing: .08em; margin-bottom: 8px; }
              .card strong { font-size: 18px; }
              table { width: 100%; border-collapse: collapse; margin-top: 24px; }
              th { background: #1270d2; color: #fff; font-size: 13px; padding: 10px; text-align: left; }
              td { padding: 10px; border-bottom: 1px solid rgba(255,255,255,.08); font-size: 14px; vertical-align: top; }
              td.number, th.number { text-align: right; }
              .totals td { font-weight: 700; }
              .footer { margin-top: 22px; text-align: center; color: #95a3b7; font-size: 12px; }
              .meta { color: #95a3b7; font-size: 13px; }
              @@media print {
                body { background: #fff; color: #000; }
                .page { padding: 0; background: #fff; }
                .sheet { max-width: none; border: none; padding: 0; color: #000; }
                .card { background: #fff; }
                .footer, .meta { color: #444; }
              }
            </style>
            """);
        builder.AppendLine("</head><body><div class=\"page\"><section class=\"sheet\">");
        builder.AppendLine("<header class=\"top\">");
        builder.Append("<div class=\"logo-box\">");
        if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
        {
            builder.Append($"<img src=\"{WebUtility.HtmlEncode(branding.LogoUrl)}\" alt=\"Logo empresa\" />");
        }
        else
        {
            builder.Append(WebUtility.HtmlEncode((branding.CompanyName.Length >= 2 ? branding.CompanyName[..2] : branding.CompanyName).ToUpperInvariant()));
        }
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"company\">");
        builder.AppendLine($"<h1>{WebUtility.HtmlEncode(branding.LegalName)}</h1>");
        builder.AppendLine($"<small>{WebUtility.HtmlEncode(branding.Address)} {(string.IsNullOrWhiteSpace(branding.Email) ? string.Empty : $"· {WebUtility.HtmlEncode(branding.Email)}")} {(string.IsNullOrWhiteSpace(branding.Phone) ? string.Empty : $"· {WebUtility.HtmlEncode(branding.Phone)}")}</small>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"pill\">Esquela de pago</div>");
        builder.AppendLine("</header>");
        builder.AppendLine("<section class=\"grid\">");
        builder.AppendLine(BuildInfoCard("Empleado", payload.NombreEmpleado));
        builder.AppendLine(BuildInfoCard("Codigo", payload.CodigoEmpleado));
        builder.AppendLine(BuildInfoCard("Cedula", payload.Cedula));
        builder.AppendLine(BuildInfoCard("No. INSS", payload.Inss));
        builder.AppendLine(BuildInfoCard("Cargo", payload.Cargo));
        builder.AppendLine(BuildInfoCard("Departamento", payload.Departamento));
        builder.AppendLine(BuildInfoCard("Tipo contrato", payload.NombreTipoContrato));
        builder.AppendLine(BuildInfoCard("Cuenta", payload.CuentaBancaria));
        builder.AppendLine(BuildInfoCard("Periodo", $"{payload.FechaDesde:dd/MM/yyyy} - {payload.FechaHasta:dd/MM/yyyy}"));
        builder.AppendLine(BuildInfoCard("Fecha pago", payload.FechaPago.ToString("dd/MM/yyyy")));
        builder.AppendLine(BuildInfoCard("Moneda", payload.Moneda));
        builder.AppendLine(BuildInfoCard("Codigo periodo", payload.CodigoPeriodo));
        builder.AppendLine("</section>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th style=\"width:56px;\">No.</th><th>Concepto</th><th class=\"number\">Devengado</th><th class=\"number\">Deducciones</th></tr></thead><tbody>");

        var rowIndex = 1;
        foreach (var concept in incomeRows)
        {
            builder.AppendLine($"<tr><td>{rowIndex++}</td><td>{WebUtility.HtmlEncode(concept.Name)}</td><td class=\"number\">{FormatCurrency(concept.Amount, payload.Moneda)}</td><td class=\"number\"></td></tr>");
        }

        foreach (var concept in deductionRows)
        {
            builder.AppendLine($"<tr><td>{rowIndex++}</td><td>{WebUtility.HtmlEncode(concept.Name)}</td><td class=\"number\"></td><td class=\"number\">{FormatCurrency(concept.Amount, payload.Moneda)}</td></tr>");
        }

        builder.AppendLine($"<tr class=\"totals\"><td colspan=\"2\">Totales</td><td class=\"number\">{FormatCurrency(payload.TotalIngresos, payload.Moneda)}</td><td class=\"number\">{FormatCurrency(payload.TotalDeducciones, payload.Moneda)}</td></tr>");
        builder.AppendLine($"<tr class=\"totals\"><td colspan=\"3\">Neto a recibir</td><td class=\"number\">{FormatCurrency(payload.NetoPagar, payload.Moneda)}</td></tr>");
        builder.AppendLine("</tbody></table>");
        builder.AppendLine($"<div class=\"meta\">INSS patronal empresa: {FormatCurrency(payload.InssPatronal, payload.Moneda)} · Retencion IR: {FormatCurrency(payload.IrRetenido, payload.Moneda)}</div>");
        builder.AppendLine($"<div class=\"footer\">{WebUtility.HtmlEncode(branding.FooterText)}{(branding.LogoPending ? " Logo corporativo pendiente de configuracion." : string.Empty)}</div>");
        builder.AppendLine("</section></div></body></html>");
        return builder.ToString();
    }

    private static string BuildGeneralReportHtml(dynamic detail, ReportBrandingDto branding)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Reporte general de nomina</title>");
        builder.AppendLine("""
            <style>
              body { margin: 0; font-family: Arial, sans-serif; background: #0f1621; color: #f4f6fa; }
              .page { padding: 28px; }
              .sheet { max-width: 1240px; margin: 0 auto; background: #131c28; border: 1px solid rgba(255,255,255,.1); padding: 28px; }
              h1,h2,h3 { margin: 0; }
              .head { display: flex; justify-content: space-between; gap: 16px; align-items: center; margin-bottom: 20px; }
              .logo { width: 70px; height: 70px; border-radius: 16px; border: 1px solid rgba(255,255,255,.1); display: grid; place-items: center; overflow: hidden; }
              .logo img { width: 100%; height: 100%; object-fit: contain; }
              .meta { color: #a6b2c4; margin-top: 6px; }
              .summary { display: grid; grid-template-columns: repeat(4, minmax(0,1fr)); gap: 12px; margin: 18px 0 24px; }
              .summary article { background: rgba(255,255,255,.03); border: 1px solid rgba(255,255,255,.08); padding: 14px 16px; }
              .summary span { display: block; color: #9db0c6; font-size: 12px; margin-bottom: 6px; text-transform: uppercase; letter-spacing: .08em; }
              .summary strong { font-size: 22px; }
              table { width: 100%; border-collapse: collapse; margin-top: 18px; }
              th { background: #1270d2; color: #fff; padding: 10px; font-size: 13px; text-align: left; }
              td { padding: 10px; border-bottom: 1px solid rgba(255,255,255,.08); font-size: 14px; }
              .number { text-align: right; }
              .footer { margin-top: 20px; font-size: 12px; color: #97a8bf; text-align: center; }
              @@media print {
                body { background: #fff; color: #000; }
                .sheet { max-width: none; border: none; color: #000; padding: 0; }
                .summary article { background: #fff; }
                .footer, .meta { color: #444; }
              }
            </style>
            """);
        builder.AppendLine("</head><body><div class=\"page\"><section class=\"sheet\">");
        builder.AppendLine("<div class=\"head\">");
        builder.Append("<div style=\"display:flex;gap:16px;align-items:center;\">");
        builder.Append("<div class=\"logo\">");
        if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
        {
            builder.Append($"<img src=\"{WebUtility.HtmlEncode(branding.LogoUrl)}\" alt=\"Logo empresa\" />");
        }
        else
        {
            builder.Append(WebUtility.HtmlEncode((branding.CompanyName.Length >= 2 ? branding.CompanyName[..2] : branding.CompanyName).ToUpperInvariant()));
        }
        builder.Append("</div>");
        builder.Append("<div>");
        builder.Append($"<h1>{WebUtility.HtmlEncode(branding.LegalName)}</h1>");
        builder.Append($"<div class=\"meta\">Reporte general de nomina - periodo {WebUtility.HtmlEncode((string)detail.run.periodCode)}</div>");
        builder.Append("</div></div>");
        builder.Append($"<div class=\"meta\">Pago: {((string)detail.run.payDate).Replace("-", "/")}</div>");
        builder.AppendLine("</div>");

        builder.AppendLine("<section class=\"summary\">");
        builder.AppendLine(BuildSummaryCard("Bruto empleados", FormatCurrency((decimal)detail.summary.totalBrutoNomina, "NIO")));
        builder.AppendLine(BuildSummaryCard("Pasantes", FormatCurrency((decimal)detail.summary.totalPasantes, "NIO")));
        builder.AppendLine(BuildSummaryCard("Servicios", FormatCurrency((decimal)detail.summary.totalServicios, "NIO")));
        builder.AppendLine(BuildSummaryCard("Neto total", FormatCurrency((decimal)detail.summary.totalNeto, "NIO")));
        builder.AppendLine(BuildSummaryCard("INSS laboral", FormatCurrency((decimal)detail.summary.totalInssLaboral, "NIO")));
        builder.AppendLine(BuildSummaryCard("INSS patronal", FormatCurrency((decimal)detail.summary.totalInssPatronal, "NIO")));
        builder.AppendLine(BuildSummaryCard("IR trabajadores", FormatCurrency((decimal)detail.summary.totalIrTrabajadores, "NIO")));
        builder.AppendLine(BuildSummaryCard("Retenciones servicios", FormatCurrency((decimal)detail.summary.totalRetencionesServicios, "NIO")));
        builder.AppendLine("</section>");

        builder.AppendLine("<table><thead><tr><th>Empleado</th><th>Tipo</th><th class=\"number\">Bruto</th><th class=\"number\">INSS lab.</th><th class=\"number\">IR/Ret.</th><th class=\"number\">Neto</th><th class=\"number\">Costo empresa</th></tr></thead><tbody>");
        foreach (var row in detail.details)
        {
            builder.AppendLine($"""
                <tr>
                  <td>{WebUtility.HtmlEncode((string)row.NombreEmpleado)}<br /><span class="meta">{WebUtility.HtmlEncode((string)row.CodigoEmpleado)} · {WebUtility.HtmlEncode((string)row.Cargo)}</span></td>
                  <td>{WebUtility.HtmlEncode((string)row.NombreTipoContrato)}</td>
                  <td class="number">{FormatCurrency((decimal)row.TotalIngresos, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.InssLaboral, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.IrRetenido, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.NetoPagar, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.TotalIngresos + (decimal)row.TotalAportesPatronales, (string)row.Moneda)}</td>
                </tr>
                """);
        }
        builder.AppendLine("</tbody></table>");
        builder.AppendLine($"<div class=\"footer\">{WebUtility.HtmlEncode(branding.FooterText)}{(branding.LogoPending ? " Logo corporativo pendiente de configuracion." : string.Empty)}</div>");
        builder.AppendLine("</section></div></body></html>");
        return builder.ToString();
    }

    private static string BuildPayslipHtmlV2(PayslipPayload payload, ReportBrandingDto branding)
    {
        var incomeRows = payload.Concepts
            .Where(concept => string.Equals(concept.ConceptType, "INGRESO", StringComparison.OrdinalIgnoreCase))
            .OrderBy(concept => concept.VisualOrder)
            .ToList();
        var deductionRows = payload.Concepts
            .Where(concept => string.Equals(concept.ConceptType, "DEDUCCION", StringComparison.OrdinalIgnoreCase))
            .OrderBy(concept => concept.VisualOrder)
            .ToList();
        var exportFileName = $"Esquela-{SanitizeFileNamePart(payload.CodigoEmpleado)}-{SanitizeFileNamePart(payload.CodigoPeriodo)}";
        var brandingMeta = BuildBrandingMeta(branding);

        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Esquela de pago</title>");
        builder.AppendLine("""
            <style>
              @page { size: A4 portrait; margin: 10mm 12mm; }
              :root { color-scheme: light; }
              * { box-sizing: border-box; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
              html, body { margin: 0; padding: 0; }
              body { font-family: Arial, Helvetica, sans-serif; background: #edf2f6; color: #1f2b38; }
              .page { width: 100%; min-height: 100vh; padding: 18px; background: linear-gradient(180deg, #eff3f7 0%, #e8edf3 100%); }
              .screen-shell { max-width: 210mm; margin: 0 auto; }
              .screen-actions { display: flex; justify-content: flex-end; gap: 8px; flex-wrap: wrap; margin-bottom: 8px; }
              .screen-note { margin-bottom: 10px; color: #69798a; font-size: 11px; text-align: right; }
              .action-button { min-height: 38px; padding: 0 15px; border-radius: 999px; border: 1px solid #cad3dc; background: #ffffff; color: #1c2b3b; font: inherit; font-size: 13px; font-weight: 700; cursor: pointer; }
              .action-button.is-primary { border-color: #0d6f8a; background: linear-gradient(135deg, #18c5b7 0%, #f2c56c 100%); color: #08141f; }
              .sheet { background: #fff; border: 1px solid #d4dde6; box-shadow: 0 20px 45px rgba(22, 37, 52, .12); padding: 10mm 12mm 11mm; }
              .top { display: grid; grid-template-columns: 64px 1fr 180px; gap: 14px; align-items: center; padding-bottom: 8px; border-bottom: 2px solid #1a4f80; }
              .logo-box { width: 64px; height: 52px; border-radius: 10px; border: 1px solid #d0d8e1; display: grid; place-items: center; background: #fff; color: #1e6e93; font-size: 21px; font-weight: 700; overflow: hidden; }
              .logo-box img { width: 100%; height: 100%; object-fit: contain; }
              .company h1 { margin: 0; font-size: 24px; line-height: 1.05; color: #1a2e44; }
              .company small { display: block; margin-top: 5px; color: #647487; font-size: 11px; }
              .pill { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; text-align: right; }
              .pill .eyebrow { font-size: 10px; font-weight: 700; letter-spacing: .18em; text-transform: uppercase; color: #51677f; }
              .pill strong { font-size: 19px; line-height: 1.05; color: #0f7d80; }
              .grid { display: grid; grid-template-columns: repeat(4, minmax(0,1fr)); gap: 8px; margin-top: 12px; }
              .card { border: 1px solid #d9e1e9; background: #f8fafc; padding: 8px 9px; min-height: 0; }
              .card span { display: block; color: #586b7e; font-size: 9.5px; text-transform: uppercase; letter-spacing: .12em; margin-bottom: 4px; font-weight: 700; }
              .card strong { display: block; font-size: 13px; line-height: 1.24; color: #1d2d3d; word-break: break-word; }
              table { width: 100%; border-collapse: collapse; margin-top: 12px; border: 1px solid #cfdae5; }
              th { background: #1f67b4; color: #fff; font-size: 11px; padding: 7px 8px; text-align: left; text-transform: uppercase; letter-spacing: .06em; }
              td { padding: 7px 8px; border-bottom: 1px solid #dee6ee; font-size: 11px; vertical-align: top; color: #1f2b38; }
              td.number, th.number { text-align: right; }
              tbody tr:nth-child(even) td { background: #fbfcfd; }
              .totals td { font-weight: 700; background: #f1f5f8; }
              .summary-strip { display: grid; grid-template-columns: 1fr 76mm; gap: 10px; margin-top: 10px; align-items: stretch; }
              .meta-box { border: 1px solid #d9e1e9; background: #f8fafc; padding: 9px 10px; font-size: 11px; line-height: 1.45; color: #506172; }
              .meta-box strong { color: #1f2b38; }
              .receipt-box { border: 2px solid #1e3144; padding: 9px 11px; text-align: right; }
              .receipt-box .label { display: block; font-size: 10px; text-transform: uppercase; letter-spacing: .12em; color: #51667c; }
              .receipt-box .value { display: block; margin-top: 5px; font-size: 24px; font-weight: 800; color: #112131; }
              .footer { margin-top: 10px; padding-top: 6px; border-top: 1px solid #dfe6ee; text-align: center; color: #6f8091; font-size: 10px; }
              @media (max-width: 1080px) {
                .top { grid-template-columns: 56px 1fr; }
                .pill { grid-column: 1 / -1; align-items: flex-start; text-align: left; }
                .grid { grid-template-columns: repeat(2, minmax(0,1fr)); }
                .summary-strip { grid-template-columns: 1fr; }
                .receipt-box { text-align: left; }
              }
              @media print {
                body { background: #fff; color: #000; }
                .screen-actions, .screen-note { display: none !important; }
                .page { padding: 0; background: #fff; }
                .screen-shell { max-width: none; }
                .sheet { border: none; box-shadow: none; padding: 0; }
                .card, .meta-box, .receipt-box { background: #fff; }
                .company h1 { font-size: 21px; }
                .pill strong { font-size: 17px; }
                .card strong { font-size: 12px; }
                td, th, .meta-box, .footer { font-size: 10px; }
                .receipt-box .value { font-size: 21px; }
              }
            </style>
            """);
        builder.AppendLine("</head><body>");
        builder.AppendLine("<div class=\"page\"><div class=\"screen-shell\">");
        builder.AppendLine("<div class=\"screen-actions\">");
        builder.AppendLine("<button class=\"action-button\" type=\"button\" onclick=\"window.print()\">Imprimir</button>");
        builder.AppendLine("<button class=\"action-button is-primary\" type=\"button\" onclick=\"exportPdf()\">Generar PDF</button>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"screen-note\">Vista lista para imprimir o guardar en PDF sin perder el formato del documento.</div>");
        builder.AppendLine("<section class=\"sheet\">");
        builder.AppendLine("<header class=\"top\">");
        builder.Append("<div class=\"logo-box\">");
        if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
        {
            builder.Append($"<img src=\"{WebUtility.HtmlEncode(branding.LogoUrl)}\" alt=\"Logo empresa\" />");
        }
        else
        {
            builder.Append(WebUtility.HtmlEncode((branding.CompanyName.Length >= 2 ? branding.CompanyName[..2] : branding.CompanyName).ToUpperInvariant()));
        }

        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"company\">");
        builder.AppendLine($"<h1>{WebUtility.HtmlEncode(branding.LegalName)}</h1>");
        builder.AppendLine($"<small>{WebUtility.HtmlEncode(brandingMeta)}</small>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"pill\"><span class=\"eyebrow\">Reporte individual</span><strong>Esquela de pago</strong></div>");
        builder.AppendLine("</header>");
        builder.AppendLine("<section class=\"grid\">");
        builder.AppendLine(BuildInfoCard("Empleado", payload.NombreEmpleado));
        builder.AppendLine(BuildInfoCard("Codigo", payload.CodigoEmpleado));
        builder.AppendLine(BuildInfoCard("Cedula", payload.Cedula));
        builder.AppendLine(BuildInfoCard("No. INSS", payload.Inss));
        builder.AppendLine(BuildInfoCard("Cargo", payload.Cargo));
        builder.AppendLine(BuildInfoCard("Departamento", payload.Departamento));
        builder.AppendLine(BuildInfoCard("Tipo contrato", payload.NombreTipoContrato));
        builder.AppendLine(BuildInfoCard("Cuenta", payload.CuentaBancaria));
        builder.AppendLine(BuildInfoCard("Periodo", $"{payload.FechaDesde:dd/MM/yyyy} - {payload.FechaHasta:dd/MM/yyyy}"));
        builder.AppendLine(BuildInfoCard("Fecha pago", payload.FechaPago.ToString("dd/MM/yyyy")));
        builder.AppendLine(BuildInfoCard("Moneda", payload.Moneda));
        builder.AppendLine(BuildInfoCard("Codigo periodo", payload.CodigoPeriodo));
        builder.AppendLine("</section>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th style=\"width:56px;\">No.</th><th>Concepto</th><th class=\"number\">Devengado</th><th class=\"number\">Deducciones</th></tr></thead><tbody>");

        var rowIndex = 1;
        foreach (var concept in incomeRows)
        {
            builder.AppendLine($"<tr><td>{rowIndex++}</td><td>{WebUtility.HtmlEncode(concept.Name)}</td><td class=\"number\">{FormatCurrency(concept.Amount, payload.Moneda)}</td><td class=\"number\"></td></tr>");
        }

        foreach (var concept in deductionRows)
        {
            builder.AppendLine($"<tr><td>{rowIndex++}</td><td>{WebUtility.HtmlEncode(concept.Name)}</td><td class=\"number\"></td><td class=\"number\">{FormatCurrency(concept.Amount, payload.Moneda)}</td></tr>");
        }

        builder.AppendLine($"<tr class=\"totals\"><td colspan=\"2\">Totales</td><td class=\"number\">{FormatCurrency(payload.TotalIngresos, payload.Moneda)}</td><td class=\"number\">{FormatCurrency(payload.TotalDeducciones, payload.Moneda)}</td></tr>");
        builder.AppendLine("</tbody></table>");
        builder.AppendLine("<section class=\"summary-strip\">");
        builder.AppendLine($"<div class=\"meta-box\"><strong>INSS patronal empresa:</strong> {WebUtility.HtmlEncode(FormatCurrency(payload.InssPatronal, payload.Moneda))}<br /><strong>Retencion IR:</strong> {WebUtility.HtmlEncode(FormatCurrency(payload.IrRetenido, payload.Moneda))}</div>");
        builder.AppendLine($"<div class=\"receipt-box\"><span class=\"label\">Neto a recibir</span><span class=\"value\">{WebUtility.HtmlEncode(FormatCurrency(payload.NetoPagar, payload.Moneda))}</span></div>");
        builder.AppendLine("</section>");
        builder.AppendLine($"<div class=\"footer\">{WebUtility.HtmlEncode(branding.FooterText)}{(branding.LogoPending ? " Logo corporativo pendiente de configuracion." : string.Empty)}</div>");
        builder.AppendLine("</section>");
        builder.AppendLine("</div></div>");
        builder.AppendLine("<script>");
        builder.AppendLine("const originalTitle = document.title;");
        builder.AppendLine("function exportPdf() {");
        builder.AppendLine($"  document.title = \"{WebUtility.HtmlEncode(exportFileName)}\";");
        builder.AppendLine("  window.print();");
        builder.AppendLine("  window.setTimeout(() => {");
        builder.AppendLine("    document.title = originalTitle;");
        builder.AppendLine("  }, 400);");
        builder.AppendLine("}");
        builder.AppendLine("</script>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static string BuildGeneralReportHtmlV2(dynamic detail, ReportBrandingDto branding)
    {
        var exportFileName = $"Reporte-Nomina-{SanitizeFileNamePart((string)detail.run.periodCode)}";
        var brandingMeta = BuildBrandingMeta(branding);
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Reporte general de nomina</title>");
        builder.AppendLine("""
            <style>
              @page { size: A4 landscape; margin: 10mm 12mm; }
              * { box-sizing: border-box; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
              html, body { margin: 0; padding: 0; }
              body { font-family: Arial, Helvetica, sans-serif; background: #edf2f6; color: #1e2b38; }
              .page { padding: 18px; background: linear-gradient(180deg, #eff3f7 0%, #e8edf3 100%); }
              .screen-shell { max-width: 297mm; margin: 0 auto; }
              .screen-actions { display: flex; justify-content: flex-end; gap: 8px; flex-wrap: wrap; margin-bottom: 8px; }
              .screen-note { margin-bottom: 10px; color: #69798a; font-size: 11px; text-align: right; }
              .action-button { min-height: 38px; padding: 0 15px; border-radius: 999px; border: 1px solid #cad3dc; background: #ffffff; color: #1c2b3b; font: inherit; font-size: 13px; font-weight: 700; cursor: pointer; }
              .action-button.is-primary { border-color: #0d6f8a; background: linear-gradient(135deg, #18c5b7 0%, #f2c56c 100%); color: #08141f; }
              .sheet { background: #fff; border: 1px solid #d4dde6; box-shadow: 0 20px 45px rgba(22, 37, 52, .12); padding: 10mm 10mm 11mm; }
              h1,h2,h3 { margin: 0; }
              .head { display: grid; grid-template-columns: auto 1fr auto; gap: 14px; align-items: center; margin-bottom: 12px; padding-bottom: 8px; border-bottom: 2px solid #1a4f80; }
              .logo { width: 62px; height: 52px; border-radius: 10px; border: 1px solid #d0d8e1; display: grid; place-items: center; overflow: hidden; background: #fff; }
              .logo img { width: 100%; height: 100%; object-fit: contain; }
              .company h1 { font-size: 22px; line-height: 1.08; color: #1a2e44; }
              .company .meta { margin-top: 5px; color: #647487; font-size: 11px; }
              .doc-tag { text-align: right; }
              .doc-tag .eyebrow { display: block; font-size: 10px; letter-spacing: .16em; text-transform: uppercase; color: #50667d; font-weight: 700; }
              .doc-tag strong { display: block; margin-top: 4px; font-size: 19px; color: #0f7d80; }
              .summary { display: grid; grid-template-columns: repeat(4, minmax(0,1fr)); gap: 8px; margin: 12px 0 14px; }
              .summary article { background: #f8fafc; border: 1px solid #d9e1e9; padding: 8px 10px; }
              .summary span { display: block; color: #586b7e; font-size: 9.5px; margin-bottom: 4px; text-transform: uppercase; letter-spacing: .12em; font-weight: 700; }
              .summary strong { font-size: 16px; color: #1d2d3d; line-height: 1.18; }
              table { width: 100%; border-collapse: collapse; margin-top: 10px; border: 1px solid #cfdae5; }
              th { background: #1f67b4; color: #fff; padding: 7px 8px; font-size: 10.5px; text-align: left; text-transform: uppercase; letter-spacing: .06em; }
              td { padding: 7px 8px; border-bottom: 1px solid #dee6ee; font-size: 10.5px; vertical-align: top; color: #1f2b38; }
              .number { text-align: right; }
              tbody tr:nth-child(even) td { background: #fbfcfd; }
              .footer { margin-top: 10px; padding-top: 6px; border-top: 1px solid #dfe6ee; font-size: 10px; color: #6f8091; text-align: center; }
              @media (max-width: 1280px) {
                .head { grid-template-columns: auto 1fr; }
                .doc-tag { grid-column: 1 / -1; text-align: left; }
                .summary { grid-template-columns: repeat(2, minmax(0,1fr)); }
              }
              @media print {
                body { background: #fff; color: #000; }
                .screen-actions, .screen-note { display: none !important; }
                .page { padding: 0; background: #fff; }
                .screen-shell { max-width: none; }
                .sheet { border: none; box-shadow: none; padding: 0; }
                .summary article { background: #fff; }
                .footer, .company .meta { color: #444; }
              }
            </style>
            """);
        builder.AppendLine("</head><body>");
        builder.AppendLine("<div class=\"page\"><div class=\"screen-shell\">");
        builder.AppendLine("<div class=\"screen-actions\">");
        builder.AppendLine("<button class=\"action-button\" type=\"button\" onclick=\"window.print()\">Imprimir</button>");
        builder.AppendLine("<button class=\"action-button\" type=\"button\" onclick=\"exportExcel()\">Generar Excel</button>");
        builder.AppendLine("<button class=\"action-button is-primary\" type=\"button\" onclick=\"exportPdf()\">Generar PDF</button>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"screen-note\">Documento compacto para impresion, PDF o exportacion a Excel.</div>");
        builder.AppendLine("<section class=\"sheet\">");
        builder.AppendLine("<div class=\"head\">");
        builder.Append("<div class=\"logo\">");
        if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
        {
            builder.Append($"<img src=\"{WebUtility.HtmlEncode(branding.LogoUrl)}\" alt=\"Logo empresa\" />");
        }
        else
        {
            builder.Append(WebUtility.HtmlEncode((branding.CompanyName.Length >= 2 ? branding.CompanyName[..2] : branding.CompanyName).ToUpperInvariant()));
        }
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"company\">");
        builder.AppendLine($"<h1>{WebUtility.HtmlEncode(branding.LegalName)}</h1>");
        builder.AppendLine($"<div class=\"meta\">Reporte general de nomina | Periodo {WebUtility.HtmlEncode((string)detail.run.periodCode)}<br />{WebUtility.HtmlEncode(brandingMeta)}</div>");
        builder.AppendLine("</div>");
        builder.AppendLine($"<div class=\"doc-tag\"><span class=\"eyebrow\">Reporte consolidado</span><strong>Pago: {WebUtility.HtmlEncode(((string)detail.run.payDate).Replace("-", "/"))}</strong></div>");
        builder.AppendLine("</div>");

        builder.AppendLine("<section class=\"summary\">");
        builder.AppendLine(BuildSummaryCard("Bruto empleados", FormatCurrency((decimal)detail.summary.totalBrutoNomina, "NIO")));
        builder.AppendLine(BuildSummaryCard("Pasantes", FormatCurrency((decimal)detail.summary.totalPasantes, "NIO")));
        builder.AppendLine(BuildSummaryCard("Servicios", FormatCurrency((decimal)detail.summary.totalServicios, "NIO")));
        builder.AppendLine(BuildSummaryCard("Neto total", FormatCurrency((decimal)detail.summary.totalNeto, "NIO")));
        builder.AppendLine(BuildSummaryCard("INSS laboral", FormatCurrency((decimal)detail.summary.totalInssLaboral, "NIO")));
        builder.AppendLine(BuildSummaryCard("INSS patronal", FormatCurrency((decimal)detail.summary.totalInssPatronal, "NIO")));
        builder.AppendLine(BuildSummaryCard("IR trabajadores", FormatCurrency((decimal)detail.summary.totalIrTrabajadores, "NIO")));
        builder.AppendLine(BuildSummaryCard("Retenciones servicios", FormatCurrency((decimal)detail.summary.totalRetencionesServicios, "NIO")));
        builder.AppendLine("</section>");

        builder.AppendLine("<table><thead><tr><th>Empleado</th><th>Tipo</th><th class=\"number\">Bruto</th><th class=\"number\">INSS lab.</th><th class=\"number\">IR/Ret.</th><th class=\"number\">Neto</th><th class=\"number\">Costo empresa</th></tr></thead><tbody>");
        foreach (var row in detail.details)
        {
            builder.AppendLine($"""
                <tr>
                  <td>{WebUtility.HtmlEncode((string)row.NombreEmpleado)}<br /><span style="color:#5c6d7f;font-size:10px;">{WebUtility.HtmlEncode((string)row.CodigoEmpleado)} | {WebUtility.HtmlEncode((string)row.Cargo)}</span></td>
                  <td>{WebUtility.HtmlEncode((string)row.NombreTipoContrato)}</td>
                  <td class="number">{FormatCurrency((decimal)row.TotalIngresos, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.InssLaboral, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.IrRetenido, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.NetoPagar, (string)row.Moneda)}</td>
                  <td class="number">{FormatCurrency((decimal)row.TotalIngresos + (decimal)row.TotalAportesPatronales, (string)row.Moneda)}</td>
                </tr>
                """);
        }

        builder.AppendLine("</tbody></table>");
        builder.AppendLine($"<div class=\"footer\">{WebUtility.HtmlEncode(branding.FooterText)}{(branding.LogoPending ? " Logo corporativo pendiente de configuracion." : string.Empty)}</div>");
        builder.AppendLine("</section>");
        builder.AppendLine("</div></div>");
        builder.AppendLine("<script>");
        builder.AppendLine("const originalTitle = document.title;");
        builder.AppendLine("function exportExcel() {");
        builder.AppendLine("  const excelUrl = `${window.location.pathname.replace('ReporteGeneralHtml', 'ReporteGeneralExcel')}${window.location.search}`;");
        builder.AppendLine("  window.location.href = excelUrl;");
        builder.AppendLine("}");
        builder.AppendLine("function exportPdf() {");
        builder.AppendLine($"  document.title = \"{WebUtility.HtmlEncode(exportFileName)}\";");
        builder.AppendLine("  window.print();");
        builder.AppendLine("  window.setTimeout(() => {");
        builder.AppendLine("    document.title = originalTitle;");
        builder.AppendLine("  }, 400);");
        builder.AppendLine("}");
        builder.AppendLine("</script>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static string BuildGeneralReportExcel(dynamic detail, ReportBrandingDto branding)
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
                    <x:Name>Reporte general</x:Name>
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
              body { font-family: Arial, sans-serif; margin: 18px; color: #14202a; }
              table { border-collapse: collapse; width: 100%; }
              td, th { border: 1px solid #b7c4d3; padding: 8px 10px; font-size: 12px; vertical-align: middle; }
              .no-border td { border: none; padding: 4px 0; }
              .title { font-size: 22px; font-weight: 700; color: #102233; }
              .subtitle { font-size: 13px; color: #52687a; }
              .right { text-align: right; }
              .center { text-align: center; }
              .section-gap { height: 10px; }
              .summary-label { background: #dce7f4; color: #224865; font-weight: 700; text-transform: uppercase; letter-spacing: .04em; }
              .summary-value { background: #edf3f9; font-size: 15px; font-weight: 700; }
              .head { background: #1270d2; color: #fff; font-weight: 700; }
              .text-soft { color: #5f7385; }
            </style>
            """);
        builder.AppendLine("</head><body>");
        builder.AppendLine("<table class=\"no-border\">");
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td class=\"title\">{WebUtility.HtmlEncode(branding.LegalName)}</td>");
        builder.AppendLine($"<td class=\"right subtitle\">Pago: {WebUtility.HtmlEncode(((string)detail.run.payDate).Replace("-", "/"))}</td>");
        builder.AppendLine("</tr>");
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td class=\"subtitle\">Reporte general de nomina - periodo {WebUtility.HtmlEncode((string)detail.run.periodCode)}</td>");
        builder.AppendLine($"<td class=\"right subtitle\">{WebUtility.HtmlEncode(branding.FooterText)}{(branding.LogoPending ? " | Logo corporativo pendiente." : string.Empty)}</td>");
        builder.AppendLine("</tr>");
        builder.AppendLine("</table>");

        builder.AppendLine("<div class=\"section-gap\"></div>");
        builder.AppendLine("<table>");
        AppendExcelSummaryRow(
            builder,
            ("Bruto empleados", FormatCurrency((decimal)detail.summary.totalBrutoNomina, "NIO")),
            ("Pasantes", FormatCurrency((decimal)detail.summary.totalPasantes, "NIO")),
            ("Servicios", FormatCurrency((decimal)detail.summary.totalServicios, "NIO")),
            ("Neto total", FormatCurrency((decimal)detail.summary.totalNeto, "NIO")));
        AppendExcelSummaryRow(
            builder,
            ("INSS laboral", FormatCurrency((decimal)detail.summary.totalInssLaboral, "NIO")),
            ("INSS patronal", FormatCurrency((decimal)detail.summary.totalInssPatronal, "NIO")),
            ("IR trabajadores", FormatCurrency((decimal)detail.summary.totalIrTrabajadores, "NIO")),
            ("Retenciones servicios", FormatCurrency((decimal)detail.summary.totalRetencionesServicios, "NIO")));
        builder.AppendLine("</table>");

        builder.AppendLine("<div class=\"section-gap\"></div>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr>");
        builder.AppendLine("<th class=\"head\">Empleado</th>");
        builder.AppendLine("<th class=\"head\">Tipo</th>");
        builder.AppendLine("<th class=\"head right\">Bruto</th>");
        builder.AppendLine("<th class=\"head right\">INSS lab.</th>");
        builder.AppendLine("<th class=\"head right\">IR/Ret.</th>");
        builder.AppendLine("<th class=\"head right\">Neto</th>");
        builder.AppendLine("<th class=\"head right\">Costo empresa</th>");
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var row in detail.details)
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{WebUtility.HtmlEncode((string)row.NombreEmpleado)}<br /><span class=\"text-soft\">{WebUtility.HtmlEncode((string)row.CodigoEmpleado)} | {WebUtility.HtmlEncode((string)row.Cargo)}</span></td>");
            builder.AppendLine($"<td>{WebUtility.HtmlEncode((string)row.NombreTipoContrato)}</td>");
            builder.AppendLine($"<td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency((decimal)row.TotalIngresos, (string)row.Moneda))}</td>");
            builder.AppendLine($"<td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency((decimal)row.InssLaboral, (string)row.Moneda))}</td>");
            builder.AppendLine($"<td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency((decimal)row.IrRetenido, (string)row.Moneda))}</td>");
            builder.AppendLine($"<td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency((decimal)row.NetoPagar, (string)row.Moneda))}</td>");
            builder.AppendLine($"<td class=\"right\">{WebUtility.HtmlEncode(FormatCurrency((decimal)row.TotalIngresos + (decimal)row.TotalAportesPatronales, (string)row.Moneda))}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static void AppendExcelSummaryRow(
        StringBuilder builder,
        (string Label, string Value) first,
        (string Label, string Value) second,
        (string Label, string Value) third,
        (string Label, string Value) fourth)
    {
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td class=\"summary-label\">{WebUtility.HtmlEncode(first.Label)}</td>");
        builder.AppendLine($"<td class=\"summary-value\">{WebUtility.HtmlEncode(first.Value)}</td>");
        builder.AppendLine($"<td class=\"summary-label\">{WebUtility.HtmlEncode(second.Label)}</td>");
        builder.AppendLine($"<td class=\"summary-value\">{WebUtility.HtmlEncode(second.Value)}</td>");
        builder.AppendLine($"<td class=\"summary-label\">{WebUtility.HtmlEncode(third.Label)}</td>");
        builder.AppendLine($"<td class=\"summary-value\">{WebUtility.HtmlEncode(third.Value)}</td>");
        builder.AppendLine($"<td class=\"summary-label\">{WebUtility.HtmlEncode(fourth.Label)}</td>");
        builder.AppendLine($"<td class=\"summary-value\">{WebUtility.HtmlEncode(fourth.Value)}</td>");
        builder.AppendLine("</tr>");
    }

    private static string BuildBrandingMeta(ReportBrandingDto branding)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(branding.Address))
        {
            parts.Add(branding.Address.Trim());
        }

        if (!string.IsNullOrWhiteSpace(branding.Email))
        {
            parts.Add(branding.Email.Trim());
        }

        if (!string.IsNullOrWhiteSpace(branding.Phone))
        {
            parts.Add(branding.Phone.Trim());
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : branding.CompanyName;
    }

    private static string SanitizeFileNamePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "archivo";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || character is '/' or '\\')
            {
                builder.Append('-');
            }
        }

        var sanitized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "archivo" : sanitized;
    }

    private static string BuildInfoCard(string label, string value)
    {
        return $"<article class=\"card\"><span>{WebUtility.HtmlEncode(label)}</span><strong>{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value)}</strong></article>";
    }

    private static string BuildSummaryCard(string label, string value)
    {
        return $"<article><span>{WebUtility.HtmlEncode(label)}</span><strong>{WebUtility.HtmlEncode(value)}</strong></article>";
    }

    private NominaSessionContext? ObtenerSesion(SqlConnection connection)
    {
        var tokenText = Request.Headers["X-Session-Token"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(tokenText))
        {
            tokenText = Request.Query["sessionToken"].ToString().Trim();
        }

        if (!Guid.TryParse(tokenText, out var token))
        {
            return null;
        }

        const string sql = """
            SELECT
                s.id_sesion_usuario,
                s.id_usuario,
                u.usuario,
                u.nombres,
                u.apellidos
            FROM seguridad.sesion_usuario s
            INNER JOIN seguridad.usuario u
                ON u.id_usuario = s.id_usuario
            WHERE s.token_sesion = @token_sesion
              AND s.activa = 1
              AND s.fecha_cierre IS NULL
              AND u.activo = 1;

            SELECT r.codigo_rol
            FROM seguridad.sesion_usuario s
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = s.id_usuario
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
            WHERE s.token_sesion = @token_sesion
              AND s.activa = 1
              AND s.fecha_cierre IS NULL
              AND ur.activo = 1
              AND r.activo = 1;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = token;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var context = new NominaSessionContext
        {
            SessionId = reader.GetInt64(0),
            UserId = reader.GetInt64(1),
            Username = reader.GetString(2),
            DisplayName = $"{reader.GetString(3)} {reader.GetString(4)}".Trim(),
        };

        if (reader.NextResult())
        {
            while (reader.Read())
            {
                context.Roles.Add(reader.GetString(0));
            }
        }

        return context;
    }

    private static bool TieneRol(NominaSessionContext session, IEnumerable<string> allowedRoles)
    {
        var roleSet = session.Roles
            .Select(role => role.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return allowedRoles.Any(role => roleSet.Contains(role));
    }

    private static bool PuedeVerEsquela(SqlConnection connection, NominaSessionContext session, long idNominaDetalle)
    {
        if (TieneRol(session, PayrollManagerRoles))
        {
            return true;
        }

        var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
        if (employee?.IdEmpleado is null)
        {
            return false;
        }

        const string sql = """
            SELECT TOP (1) id_empleado
            FROM nomina.nomina_detalle
            WHERE id_nomina_detalle = @id_nomina_detalle;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_nomina_detalle", SqlDbType.BigInt).Value = idNominaDetalle;
        var owner = command.ExecuteScalar();

        if (owner is null || owner == DBNull.Value)
        {
            return false;
        }

        return Convert.ToInt64(owner, CultureInfo.InvariantCulture) == employee.IdEmpleado.Value;
    }

    private static string FormatCurrency(decimal amount, string currencyCode)
    {
        try
        {
            return string.Format(
                CultureInfo.GetCultureInfo("es-NI"),
                "{0} {1:N2}",
                currencyCode,
                amount);
        }
        catch
        {
            return $"{currencyCode} {amount:N2}";
        }
    }
}

public sealed class SaveNominaConfigRequest
{
    public string RegimenInssEmpresa { get; set; } = "INTEGRAL";
    public decimal CantidadTrabajadoresEmpresa { get; set; }
    public string ModoPasantiaPorDefecto { get; set; } = "NO_NOMINA";
    public decimal DiasMesNomina { get; set; } = 30;
    public decimal HorasMesBase { get; set; } = 240;
}

public sealed class OpenPayrollPeriodRequest
{
    public string CodigoPeriodo { get; set; } = string.Empty;
    public string FechaDesde { get; set; } = string.Empty;
    public string FechaHasta { get; set; } = string.Empty;
    public string FechaPago { get; set; } = string.Empty;
    public string TipoPeriodo { get; set; } = "MENSUAL";
    public string? Observacion { get; set; }
    public string? FechaCorteHoraExtra { get; set; }
}

public sealed class GeneratePayrollRequest
{
    public long IdPeriodoNomina { get; set; }
}

public sealed class NominaSessionContext
{
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; } = [];
}

public sealed class ClosePayrollRequest
{
    public long IdNomina { get; set; }
}

public sealed class LiquidationPreviewRequest
{
    public long IdEmpleado { get; set; }
    public string FechaLiquidacion { get; set; } = string.Empty;
    public string FechaBaja { get; set; } = string.Empty;
    public string CausalCodigo { get; set; } = "RENUNCIA_ART44";
    public string MotivoLiquidacion { get; set; } = string.Empty;
    public decimal? DiasSalarioPendiente { get; set; }
}

public sealed class PayrollDetailRowDto
{
    public long IdNominaDetalle { get; set; }
    public long IdEmpleado { get; set; }
    public string CodigoEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Inss { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string CuentaBancaria { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string CodigoTipoContrato { get; set; } = string.Empty;
    public string NombreTipoContrato { get; set; } = string.Empty;
    public string Moneda { get; set; } = "NIO";
    public decimal SalarioBasePeriodo { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalAportesPatronales { get; set; }
    public decimal NetoPagar { get; set; }
    public decimal InssLaboral { get; set; }
    public decimal InssPatronal { get; set; }
    public decimal IrRetenido { get; set; }
    public decimal IrPatronal { get; set; }
    public string TipoPago { get; set; } = string.Empty;
}

public sealed class PayslipPayload
{
    public long IdNominaDetalle { get; set; }
    public long IdNomina { get; set; }
    public long IdEmpleado { get; set; }
    public string CodigoEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Inss { get; set; } = string.Empty;
    public string CuentaBancaria { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string CodigoTipoContrato { get; set; } = string.Empty;
    public string NombreTipoContrato { get; set; } = string.Empty;
    public string Moneda { get; set; } = "NIO";
    public decimal SalarioBasePeriodo { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalAportesPatronales { get; set; }
    public decimal NetoPagar { get; set; }
    public decimal InssLaboral { get; set; }
    public decimal InssPatronal { get; set; }
    public decimal IrRetenido { get; set; }
    public string CodigoPeriodo { get; set; } = string.Empty;
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
    public DateTime FechaPago { get; set; }
    public List<PayslipConcept> Concepts { get; } = [];
}

public sealed class PayslipConcept
{
    public string Name { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int VisualOrder { get; set; }
}
