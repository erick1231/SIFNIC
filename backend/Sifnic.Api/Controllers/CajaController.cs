using System.Data;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Creditos;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class CajaController : Controller
{
    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            var cashSession = LoadCurrentCashSession(connection, session.Username);
            var summary = cashSession is null ? null : LoadCashSummary(connection, cashSession.Id);
            var branchContext = LoadBranchContext(connection, session);
            var exchangeRates = LoadInstitutionalExchangeRates(connection, DateTime.Today);
            MicrofinanceCoreSupport.EnsureSchema(connection);
            return Json(new
            {
                ok = true,
                data = new
                {
                    currencies = new[] { "NIO", "USD" },
                    methods = new[] { "EFECTIVO", "TRANSFERENCIA", "CHEQUE", "POS" },
                    branches = branchContext.Branches.Select(branch => new
                    {
                        value = branch.Name,
                        label = $"{branch.Code} - {branch.Name}",
                        branch.Code,
                        branch.Id,
                    }),
                    assignedBranch = branchContext.AssignedBranch is null ? null : new
                    {
                        value = branchContext.AssignedBranch.Name,
                        label = $"{branchContext.AssignedBranch.Code} - {branchContext.AssignedBranch.Name}",
                        branchContext.AssignedBranch.Code,
                        branchContext.AssignedBranch.Id,
                    },
                    branchLocked = branchContext.Locked,
                    exchangeRate = exchangeRates.Buy,
                    exchangeRateType = "INSTITUCIONAL",
                    exchangeRates = new
                    {
                        buy = exchangeRates.Buy,
                        sell = exchangeRates.Sell,
                        reference = exchangeRates.Reference,
                        date = exchangeRates.Date,
                    },
                    denominations = new
                    {
                        NIO = new[] { 1000m, 500m, 200m, 100m, 50m, 20m, 10m, 5m, 1m, 0.50m, 0.25m, 0.10m, 0.05m },
                        USD = new[] { 100m, 50m, 20m, 10m, 5m, 1m, 0.25m, 0.10m, 0.05m, 0.01m },
                    },
                    session = cashSession,
                    summary,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar Caja.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult BuscarCreditos(string? search)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            var canSeeAll = session.HasAnyRole("ADMINISTRADOR", "ADMINISTRACION", "CAJA", "CAJERO", "JEFE_CREDITO", "GERENTE_CREDITO");
            using var command = new SqlCommand(
                """
                SELECT TOP (30)
                    cr.id_credito,
                    cr.numero_credito,
                    cr.cedula_id_cliente,
                    cr.nom_cliente,
                    cr.moneda,
                    cr.saldo_capital,
                    cr.estado_operativo,
                    nextDue.numero_cuota,
                    nextDue.fecha_cuota,
                    nextDue.pendiente_capital,
                    nextDue.pendiente_interes,
                    nextDue.pendiente_comision,
                    nextDue.pendiente_mora,
                    nextDue.pendiente_deslizamiento,
                    nextDue.pendiente_cuota,
                    dueInfo.total_vencido,
                    dueInfo.total_hoy,
                    dueInfo.cuotas_vencidas,
                    dueInfo.dias_mora,
                    dueInfo.mora_total,
                    dueInfo.interes_total,
                    dueInfo.comision_total,
                    dueInfo.capital_total,
                    dueInfo.proxima_fecha
                FROM creditos.credito cr
                OUTER APPLY
                (
                    SELECT TOP (1)
                        pp.numero_cuota,
                        pp.fecha_cuota,
                        CASE WHEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) > 0 THEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) ELSE 0 END AS pendiente_capital,
                        CASE WHEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) > 0 THEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) ELSE 0 END AS pendiente_interes,
                        CASE WHEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) > 0 THEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) ELSE 0 END AS pendiente_comision,
                        CASE WHEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) > 0 THEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) ELSE 0 END AS pendiente_mora,
                        ISNULL(pp.deslizamiento_programado, 0) AS pendiente_deslizamiento,
                        CASE WHEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) > 0 THEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) ELSE 0 END +
                        CASE WHEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) > 0 THEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) ELSE 0 END +
                        CASE WHEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) > 0 THEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) ELSE 0 END +
                        CASE WHEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) > 0 THEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) ELSE 0 END +
                        ISNULL(pp.deslizamiento_programado, 0) AS pendiente_cuota
                    FROM creditos.plan_pago_credito pp
                    WHERE pp.id_credito = cr.id_credito
                      AND pp.pagada = 0
                    ORDER BY pp.fecha_cuota, pp.numero_cuota
                ) nextDue
                OUTER APPLY
                (
                    SELECT
                        ISNULL(SUM(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_operacion THEN
                            CASE WHEN
                                ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                ISNULL(pp.deslizamiento_programado, 0) -
                                ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0) > 0
                            THEN
                                ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                ISNULL(pp.deslizamiento_programado, 0) -
                                ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                            ELSE 0 END ELSE 0 END), 0) AS total_vencido,
                        ISNULL(SUM(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota <= @fecha_operacion THEN
                            CASE WHEN
                                ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                ISNULL(pp.deslizamiento_programado, 0) -
                                ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0) > 0
                            THEN
                                ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                ISNULL(pp.deslizamiento_programado, 0) -
                                ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                            ELSE 0 END ELSE 0 END), 0) AS total_hoy,
                        ISNULL(SUM(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_operacion THEN 1 ELSE 0 END), 0) AS cuotas_vencidas,
                        ISNULL(MAX(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_operacion THEN DATEDIFF(DAY, pp.fecha_cuota, @fecha_operacion) ELSE 0 END), 0) AS dias_mora,
                        ISNULL(SUM(CASE WHEN pp.pagada = 0 THEN CASE WHEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) > 0 THEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) ELSE 0 END ELSE 0 END), 0) AS mora_total,
                        ISNULL(SUM(CASE WHEN pp.pagada = 0 THEN CASE WHEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) > 0 THEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) ELSE 0 END ELSE 0 END), 0) AS interes_total,
                        ISNULL(SUM(CASE WHEN pp.pagada = 0 THEN CASE WHEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) > 0 THEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) ELSE 0 END + ISNULL(pp.deslizamiento_programado, 0) ELSE 0 END), 0) AS comision_total,
                        ISNULL(SUM(CASE WHEN pp.pagada = 0 THEN CASE WHEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) > 0 THEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) ELSE 0 END ELSE 0 END), 0) AS capital_total,
                        MIN(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota > @fecha_operacion THEN pp.fecha_cuota ELSE NULL END) AS proxima_fecha
                    FROM creditos.plan_pago_credito pp
                    WHERE pp.id_credito = cr.id_credito
                ) dueInfo
                OUTER APPLY
                (
                    SELECT TOP (1) ao.id_usuario_oficial
                    FROM creditos.asignacion_oficial_credito ao
                    WHERE ao.id_credito = cr.id_credito
                      AND ao.activo = 1
                      AND ao.fecha_fin IS NULL
                    ORDER BY ao.fecha_asignacion DESC, ao.id_asignacion_oficial_credito DESC
                ) oficial
                WHERE cr.activo = 1
                  AND cr.saldo_capital > 0
                  AND cr.fecha_desembolso IS NOT NULL
                  AND cr.estado_operativo NOT IN (N'APROBADO', N'PENDIENTE_DESEMBOLSO')
                  AND (@puede_ver_todo = 1 OR oficial.id_usuario_oficial = @id_usuario)
                  AND (
                    @buscar = N''
                    OR CONVERT(NVARCHAR(30), cr.id_credito) = @buscar
                    OR ISNULL(cr.numero_credito, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(cr.cedula_id_cliente, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(cr.nom_cliente, N'') LIKE N'%' + @buscar + N'%'
                  )
                ORDER BY cr.id_credito DESC;
                """,
                connection);
            command.Parameters.Add("@buscar", SqlDbType.NVarChar, 160).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@puede_ver_todo", SqlDbType.Bit).Value = canSeeAll;
            command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = session.UserId;
            command.Parameters.Add("@fecha_operacion", SqlDbType.Date).Value = DateTime.Today;

            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(new
                {
                    creditId = ReadInt64(reader, "id_credito"),
                    creditNumber = ReadString(reader, "numero_credito"),
                    clientIdentification = ReadString(reader, "cedula_id_cliente"),
                    clientName = ReadString(reader, "nom_cliente"),
                    currency = ReadString(reader, "moneda", "NIO"),
                    capitalBalance = ReadDecimal(reader, "saldo_capital"),
                    status = ReadString(reader, "estado_operativo"),
                    nextInstallment = ReadInt32Nullable(reader, "numero_cuota"),
                    nextDueDate = ReadDateTimeNullable(reader, "fecha_cuota"),
                    nextCapital = ReadDecimal(reader, "pendiente_capital"),
                    nextInterest = ReadDecimal(reader, "pendiente_interes"),
                    nextCommission = ReadDecimal(reader, "pendiente_comision"),
                    nextMora = ReadDecimal(reader, "pendiente_mora"),
                    nextSlide = ReadDecimal(reader, "pendiente_deslizamiento"),
                    nextAmount = ReadDecimal(reader, "pendiente_cuota"),
                    overdueAmount = ReadDecimal(reader, "total_vencido"),
                    dueTodayAmount = ReadDecimal(reader, "total_hoy"),
                    overdueInstallments = ReadInt32(reader, "cuotas_vencidas"),
                    overdueDays = ReadInt32(reader, "dias_mora"),
                    totalMora = ReadDecimal(reader, "mora_total"),
                    totalInterest = ReadDecimal(reader, "interes_total"),
                    totalCommission = ReadDecimal(reader, "comision_total"),
                    totalCapitalDue = ReadDecimal(reader, "capital_total"),
                    followingDueDate = ReadDateTimeNullable(reader, "proxima_fecha"),
                });
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron buscar creditos.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult BuscarDesembolsos(string? search)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            using var command = new SqlCommand(
                """
                SELECT TOP (40)
                    cr.id_credito,
                    COALESCE(NULLIF(cr.numero_credito, N''), cr.cedula_id_cliente_ofic_ciclo, N'') AS numero_credito,
                    cr.cedula_id_cliente,
                    cr.nom_cliente,
                    cr.moneda,
                    cr.monto_aprobado,
                    cr.plazo_meses,
                    cr.tasa_interes_anual,
                    cr.estado_operativo,
                    s.numero_solicitud,
                    s.destino_credito,
                    s.producto_credito,
                    s.promotor_credito,
                    s.sucursal_credito
                FROM creditos.credito cr
                INNER JOIN creditos.solicitud_credito s
                    ON s.id_solicitud_credito = cr.id_solicitud_credito
                WHERE cr.activo = 1
                  AND s.estado_solicitud = N'APROBADA'
                  AND (cr.fecha_desembolso IS NULL OR cr.estado_operativo IN (N'APROBADO', N'PENDIENTE_DESEMBOLSO'))
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM creditos.desembolso_credito d
                      WHERE d.id_credito = cr.id_credito
                  )
                  AND (
                    @buscar = N''
                    OR CONVERT(NVARCHAR(30), cr.id_credito) = @buscar
                    OR ISNULL(cr.numero_credito, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(s.numero_solicitud, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(cr.cedula_id_cliente, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(cr.nom_cliente, N'') LIKE N'%' + @buscar + N'%'
                  )
                ORDER BY cr.id_credito DESC;
                """,
                connection);
            command.Parameters.Add("@buscar", SqlDbType.NVarChar, 160).Value = (search ?? string.Empty).Trim();

            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(new
                {
                    creditId = ReadInt64(reader, "id_credito"),
                    creditNumber = ReadString(reader, "numero_credito"),
                    requestNumber = ReadString(reader, "numero_solicitud"),
                    clientIdentification = ReadString(reader, "cedula_id_cliente"),
                    clientName = ReadString(reader, "nom_cliente"),
                    currency = ReadString(reader, "moneda", "NIO"),
                    approvedAmount = ReadDecimal(reader, "monto_aprobado"),
                    termMonths = ReadInt32(reader, "plazo_meses"),
                    annualRate = ReadDecimal(reader, "tasa_interes_anual"),
                    status = ReadString(reader, "estado_operativo"),
                    destination = ReadString(reader, "destino_credito"),
                    product = ReadString(reader, "producto_credito"),
                    promoter = ReadString(reader, "promotor_credito"),
                    branch = ReadString(reader, "sucursal_credito"),
                });
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar desembolsos pendientes.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AbrirSesion([FromBody] CashOpenModel model)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (LoadCurrentCashSession(connection, session.Username) is not null)
            {
                return BadRequest(new { ok = false, message = "Ya existe una sesion de caja abierta para este usuario." });
            }

            var openingErrors = ValidateCashAmounts(model.OpeningNio, model.OpeningUsd, model.Breakdown, "apertura");
            if (openingErrors.Count > 0)
            {
                return BadRequest(new { ok = false, message = "La apertura de caja tiene valores invalidos.", errors = openingErrors });
            }

            var branchContext = LoadBranchContext(connection, session);
            var cashBranch = ResolveCashBranch(model.Branch, branchContext);
            var openingNio = ResolveCashAmount(model.OpeningNio, model.Breakdown, "NIO");
            var openingUsd = ResolveCashAmount(model.OpeningUsd, model.Breakdown, "USD");
            using var transaction = connection.BeginTransaction();
            long sessionId;
            using (var command = new SqlCommand(
                """
                INSERT INTO caja.sesion_caja
                (
                    fecha_operacion,
                    sucursal,
                    usuario_cajero,
                    monto_apertura_nio,
                    monto_apertura_usd,
                    fecha_apertura,
                    estado_sesion,
                    observacion_apertura,
                    total_ingresos_nio,
                    total_ingresos_usd,
                    total_egresos_nio,
                    total_egresos_usd,
                    saldo_teorico_nio,
                    saldo_teorico_usd
                )
                OUTPUT INSERTED.id_sesion_caja
                VALUES
                (
                    CONVERT(date, SYSDATETIME()),
                    @sucursal,
                    @usuario_cajero,
                    @monto_apertura_nio,
                    @monto_apertura_usd,
                    SYSDATETIME(),
                    N'ABIERTA',
                    @observacion,
                    0,
                    0,
                    0,
                    0,
                    @monto_apertura_nio,
                    @monto_apertura_usd
                );
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@sucursal", SqlDbType.NVarChar, 100).Value = cashBranch;
                command.Parameters.Add("@usuario_cajero", SqlDbType.NVarChar, 200).Value = session.Username;
                command.Parameters.Add("@monto_apertura_nio", SqlDbType.Decimal).Value = openingNio;
                command.Parameters.Add("@monto_apertura_usd", SqlDbType.Decimal).Value = openingUsd;
                command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(model.Observation);
                sessionId = Convert.ToInt64(command.ExecuteScalar());
            }

            SaveBreakdown(connection, transaction, sessionId, "APERTURA", model.Breakdown);
            transaction.Commit();

            return Json(new { ok = true, message = "Sesion de caja abierta.", data = new { sessionId } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo abrir caja.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult CerrarSesion([FromBody] CashCloseModel model)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            var cashSession = LoadCurrentCashSession(connection, session.Username);
            if (cashSession is null)
            {
                return BadRequest(new { ok = false, message = "No hay sesion de caja abierta." });
            }

            var closeErrors = ValidateCashAmounts(model.PhysicalNio, model.PhysicalUsd, model.Breakdown, "cierre");
            if (closeErrors.Count > 0)
            {
                return BadRequest(new { ok = false, message = "El cierre de caja tiene valores invalidos.", errors = closeErrors });
            }

            var physicalNio = ResolveCashAmount(model.PhysicalNio, model.Breakdown, "NIO");
            var physicalUsd = ResolveCashAmount(model.PhysicalUsd, model.Breakdown, "USD");
            using var transaction = connection.BeginTransaction();
            using (var command = new SqlCommand(
                """
                UPDATE caja.sesion_caja
                SET
                    fecha_cierre = SYSDATETIME(),
                    estado_sesion = N'CERRADA',
                    observacion_cierre = @observacion,
                    saldo_fisico_nio = @saldo_fisico_nio,
                    saldo_fisico_usd = @saldo_fisico_usd,
                    diferencia_caja_nio = @saldo_fisico_nio - saldo_teorico_nio,
                    diferencia_caja_usd = @saldo_fisico_usd - saldo_teorico_usd
                WHERE id_sesion_caja = @id_sesion_caja
                  AND estado_sesion = N'ABIERTA';
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSession.Id;
                command.Parameters.Add("@saldo_fisico_nio", SqlDbType.Decimal).Value = physicalNio;
                command.Parameters.Add("@saldo_fisico_usd", SqlDbType.Decimal).Value = physicalUsd;
                command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(model.Observation);
                command.ExecuteNonQuery();
            }

            SaveBreakdown(connection, transaction, cashSession.Id, "CIERRE", model.Breakdown);
            var differenceNio = SignedDecimal(physicalNio - cashSession.TheoreticalNio);
            var differenceUsd = SignedDecimal(physicalUsd - cashSession.TheoreticalUsd);
            var countId = InsertCashCount(
                connection,
                transaction,
                cashSession.Id,
                "CIERRE",
                cashSession.TheoreticalNio,
                cashSession.TheoreticalUsd,
                physicalNio,
                physicalUsd,
                differenceNio,
                differenceUsd,
                model.Observation,
                session.Username);
            transaction.Commit();
            return Json(new
            {
                ok = true,
                message = DifferenceMessage(differenceNio, differenceUsd, "Sesion de caja cerrada."),
                data = new { cashSession.Id, countId, physicalNio, physicalUsd, differenceNio, differenceUsd },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cerrar caja.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult GenerarArqueo([FromBody] CashCloseModel model)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            var cashSession = LoadCurrentCashSession(connection, session.Username);
            if (cashSession is null)
            {
                return BadRequest(new { ok = false, message = "Debe abrir caja antes de iniciar arqueo." });
            }

            var countErrors = ValidateCashAmounts(model.PhysicalNio, model.PhysicalUsd, model.Breakdown, "arqueo");
            if (countErrors.Count > 0)
            {
                return BadRequest(new { ok = false, message = "El arqueo tiene valores invalidos.", errors = countErrors });
            }

            var physicalNio = ResolveCashAmount(model.PhysicalNio, model.Breakdown, "NIO");
            var physicalUsd = ResolveCashAmount(model.PhysicalUsd, model.Breakdown, "USD");
            var differenceNio = SignedDecimal(physicalNio - cashSession.TheoreticalNio);
            var differenceUsd = SignedDecimal(physicalUsd - cashSession.TheoreticalUsd);

            using var transaction = connection.BeginTransaction();
            var countId = InsertCashCount(
                connection,
                transaction,
                cashSession.Id,
                "ARQUEO",
                cashSession.TheoreticalNio,
                cashSession.TheoreticalUsd,
                physicalNio,
                physicalUsd,
                differenceNio,
                differenceUsd,
                model.Observation,
                session.Username);
            SaveBreakdown(connection, transaction, cashSession.Id, "ARQUEO", model.Breakdown);

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CAJA",
                "ARQUEO",
                "GENERACION",
                countId,
                $"ARQ-{countId}",
                $"Arqueo generado para caja {cashSession.Id}.",
                new { cashSession.Id, countId, cashSession.TheoreticalNio, cashSession.TheoreticalUsd, physicalNio, physicalUsd, differenceNio, differenceUsd });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = DifferenceMessage(differenceNio, differenceUsd, "Arqueo generado sin diferencias."),
                data = new
                {
                    countId,
                    cashSessionId = cashSession.Id,
                    theoreticalNio = cashSession.TheoreticalNio,
                    theoreticalUsd = cashSession.TheoreticalUsd,
                    physicalNio,
                    physicalUsd,
                    differenceNio,
                    differenceUsd,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo generar el arqueo.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Resumen()
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            var cashSession = LoadCurrentCashSession(connection, session.Username);
            return Json(new { ok = true, data = cashSession is null ? null : LoadCashSummary(connection, cashSession.Id) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el arqueo.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ListarRecibos(string? search, DateTime? dateFrom, DateTime? dateTo)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            using var command = new SqlCommand(
                """
                SELECT TOP (80)
                    p.id_pago_credito,
                    p.numero_recibo,
                    p.fecha_pago,
                    p.monto_pagado,
                    p.moneda_pago,
                    p.forma_pago,
                    p.estado_pago,
                    p.nombre_abonante,
                    cr.numero_credito,
                    cr.cedula_id_cliente,
                    cr.nom_cliente,
                    ro.numero_recibo_oficial
                FROM creditos.pago_credito p
                INNER JOIN creditos.credito cr
                    ON cr.id_credito = p.id_credito
                LEFT JOIN caja.movimiento_caja mc
                    ON mc.id_pago_credito = p.id_pago_credito
                   AND ISNULL(mc.anulado, 0) = 0
                LEFT JOIN caja.recibo_oficial_caja ro
                    ON ro.id_movimiento_caja = mc.id_movimiento_caja
                   AND ISNULL(ro.anulado, 0) = 0
                WHERE ISNULL(p.anulado, 0) = 0
                  AND (@buscar = N''
                       OR p.numero_recibo LIKE N'%' + @buscar + N'%'
                       OR ISNULL(ro.numero_recibo_oficial, N'') LIKE N'%' + @buscar + N'%'
                       OR ISNULL(cr.numero_credito, N'') LIKE N'%' + @buscar + N'%'
                       OR ISNULL(cr.cedula_id_cliente, N'') LIKE N'%' + @buscar + N'%'
                       OR ISNULL(cr.nom_cliente, N'') LIKE N'%' + @buscar + N'%')
                  AND (@desde IS NULL OR CONVERT(date, p.fecha_pago) >= @desde)
                  AND (@hasta IS NULL OR CONVERT(date, p.fecha_pago) <= @hasta)
                ORDER BY p.fecha_pago DESC, p.id_pago_credito DESC;
                """,
                connection);
            command.Parameters.Add("@buscar", SqlDbType.NVarChar, 160).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@desde", SqlDbType.Date).Value = dateFrom.HasValue ? dateFrom.Value.Date : DBNull.Value;
            command.Parameters.Add("@hasta", SqlDbType.Date).Value = dateTo.HasValue ? dateTo.Value.Date : DBNull.Value;

            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(new
                {
                    paymentId = ReadInt64(reader, "id_pago_credito"),
                    voucherNumber = ReadString(reader, "numero_recibo"),
                    officialReceiptNumber = ReadString(reader, "numero_recibo_oficial"),
                    date = ReadDateTime(reader, "fecha_pago"),
                    amount = ReadDecimal(reader, "monto_pagado"),
                    currency = ReadString(reader, "moneda_pago", "NIO"),
                    method = ReadString(reader, "forma_pago"),
                    status = ReadString(reader, "estado_pago"),
                    payer = ReadString(reader, "nombre_abonante"),
                    creditNumber = ReadString(reader, "numero_credito"),
                    clientIdentification = ReadString(reader, "cedula_id_cliente"),
                    clientName = ReadString(reader, "nom_cliente"),
                    printUrl = $"/Caja/VoucherPagoHtml?id={ReadInt64(reader, "id_pago_credito")}",
                });
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar los recibos.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult BuscarAbonante(string? cedula)
    {
        var normalized = NormalizeIdentification(cedula);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest(new { ok = false, message = "Digita la cedula del abonante." });
        }

        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            using (var clientCommand = new SqlCommand(
                """
                SELECT TOP (1)
                    c.cedula,
                    LTRIM(RTRIM(CONCAT(c.nombres, N' ', c.apellidos))) AS nombre_completo,
                    COALESCE(NULLIF(c.celular, N''), NULLIF(c.telefono, N''), NULLIF(c.telefono_secundario, N''), N'') AS telefono
                FROM clientes.cliente c
                WHERE c.activo = 1
                  AND UPPER(REPLACE(REPLACE(REPLACE(c.cedula, N'-', N''), N' ', N''), N'.', N'')) = @cedula
                ORDER BY c.id_cliente DESC;
                """,
                connection))
            {
                clientCommand.Parameters.Add("@cedula", SqlDbType.NVarChar, 100).Value = normalized;
                using var reader = clientCommand.ExecuteReader();
                if (reader.Read())
                {
                    return Json(new
                    {
                        ok = true,
                        data = new
                        {
                            found = true,
                            source = "CLIENTE",
                            identification = ReadString(reader, "cedula"),
                            name = ReadString(reader, "nombre_completo"),
                            phone = ReadString(reader, "telefono"),
                        },
                    });
                }
            }

            using (var payerCommand = new SqlCommand(
                """
                SELECT TOP (1)
                    p.cedula_abonante,
                    p.nombre_abonante,
                    p.telefono_abonante
                FROM creditos.pago_credito p
                WHERE p.cedula_abonante IS NOT NULL
                  AND UPPER(REPLACE(REPLACE(REPLACE(p.cedula_abonante, N'-', N''), N' ', N''), N'.', N'')) = @cedula
                  AND NULLIF(p.nombre_abonante, N'') IS NOT NULL
                ORDER BY p.fecha_pago DESC, p.id_pago_credito DESC;
                """,
                connection))
            {
                payerCommand.Parameters.Add("@cedula", SqlDbType.NVarChar, 100).Value = normalized;
                using var reader = payerCommand.ExecuteReader();
                if (reader.Read())
                {
                    return Json(new
                    {
                        ok = true,
                        data = new
                        {
                            found = true,
                            source = "ABONANTE",
                            identification = ReadString(reader, "cedula_abonante"),
                            name = ReadString(reader, "nombre_abonante"),
                            phone = ReadString(reader, "telefono_abonante"),
                        },
                    });
                }
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    found = false,
                    identification = normalized,
                    name = "",
                    phone = "",
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo buscar el abonante.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AplicarPago([FromBody] PaymentApplyModel model)
    {
        var errors = ValidatePayment(model);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "Corrige los datos del pago.", errors });
        }

        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            using var transaction = connection.BeginTransaction();
            var cashSession = LoadCurrentCashSession(connection, transaction, session.Username);
            if (cashSession is null)
            {
                return BadRequest(new { ok = false, message = "Debe abrir caja antes de aplicar pagos." });
            }

            var loan = LoadLoanForPayment(connection, transaction, model.CreditId);
            if (loan is null)
            {
                return NotFound(new { ok = false, message = "Prestamo no encontrado." });
            }

            var voucherNumber = CreditOperationsSupport.NextCode(
                connection,
                "creditos.recibo_pago_credito",
                "numero_recibo",
                $"VCH-{DateTime.Today:yyyy}-",
                transaction);
            var officialReceipt = CreditOperationsSupport.NextCode(
                connection,
                "caja.recibo_oficial_caja",
                "numero_recibo_oficial",
                $"RCJ-{DateTime.Today:yyyy}-",
                transaction);
            var amount = CreditOperationsSupport.SafeDecimal(model.Amount);
            var currency = NormalizeCurrency(model.Currency, loan.Currency);
            var exchangeRate = ResolveExchangeRate(connection, transaction, model.ExchangeRate, DateTime.Today, currency, loan.Currency);
            var appliedAmount = ConvertToCreditCurrency(amount, currency, loan.Currency, exchangeRate);
            var method = NormalizePaymentMethod(model.Method);
            var payerName = string.IsNullOrWhiteSpace(model.PayerName) ? loan.ClientName : model.PayerName.Trim();
            var payerId = string.IsNullOrWhiteSpace(model.PayerIdentification) ? loan.ClientIdentification : model.PayerIdentification.Trim();

            var paymentId = InsertPayment(connection, transaction, loan, model, amount, appliedAmount, currency, exchangeRate, method, voucherNumber, payerName, payerId);
            var allocation = ApplyPaymentToSchedule(connection, transaction, loan.CreditId, paymentId, appliedAmount);
            UpdateLoanBalances(connection, transaction, loan.CreditId, allocation);
            var cashMovementId = InsertCashMovement(connection, transaction, loan, paymentId, amount, currency, exchangeRate, appliedAmount, method, session.Username, cashSession.Id);
            InsertCreditReceipt(connection, transaction, paymentId, voucherNumber, amount, currency, model.Observation);
            InsertOfficialReceipt(connection, transaction, cashMovementId, officialReceipt, loan, amount, currency, model.Observation);
            AccountingAutomationSupport.RegisterCreditPaymentEntry(
                connection,
                transaction,
                paymentId,
                loan.CreditId,
                loan.CreditNumber,
                loan.ClientIdentification,
                loan.ClientName,
                loan.Currency,
                exchangeRate,
                appliedAmount,
                allocation.Capital,
                allocation.Interest,
                allocation.Commission,
                allocation.Mora,
                session.Username);
            if (AffectsCash(method))
            {
                UpdateCashSessionTotals(connection, transaction, cashSession.Id, amount, currency);
            }

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CAJA",
                "PAGO_CREDITO",
                "APLICACION",
                paymentId,
                voucherNumber,
                $"Pago aplicado al credito {loan.CreditNumber}.",
                new
                {
                    creditId = loan.CreditId,
                    creditNumber = loan.CreditNumber,
                    receivedAmount = amount,
                    receivedCurrency = currency,
                    creditCurrency = loan.Currency,
                    exchangeRate,
                    appliedAmount,
                    voucherNumber,
                    officialReceipt,
                    allocation,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Pago aplicado y voucher generado.",
                data = new
                {
                    paymentId,
                    voucherNumber,
                    officialReceiptNumber = officialReceipt,
                    printUrl = $"/Caja/VoucherPagoHtml?id={paymentId}",
                    receivedAmount = amount,
                    receivedCurrency = currency,
                    creditCurrency = loan.Currency,
                    exchangeRate,
                    appliedAmount,
                    allocation,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo aplicar el pago.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult DesembolsarCredito([FromBody] CreditDisbursementModel model)
    {
        if (model.CreditId <= 0)
        {
            return BadRequest(new { ok = false, message = "Selecciona un credito aprobado para desembolsar." });
        }

        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            using var transaction = connection.BeginTransaction();
            var cashSession = LoadCurrentCashSession(connection, transaction, session.Username);
            if (cashSession is null)
            {
                return BadRequest(new { ok = false, message = "Debe abrir caja antes de desembolsar creditos." });
            }

            var loan = LoadLoanForDisbursement(connection, transaction, model.CreditId);
            if (loan is null)
            {
                return NotFound(new { ok = false, message = "Credito no encontrado, no aprobado o ya desembolsado." });
            }

            var method = NormalizePaymentMethod(model.Method);
            var currency = NormalizeCurrency(model.Currency, loan.Currency);
            var amount = CreditOperationsSupport.SafeDecimal(model.Amount <= 0 ? loan.ApprovedAmount : model.Amount);
            if (amount <= 0)
            {
                return BadRequest(new { ok = false, message = "El monto de desembolso debe ser mayor que cero." });
            }

            if (amount > loan.ApprovedAmount)
            {
                return BadRequest(new { ok = false, message = "El desembolso no puede superar el monto aprobado." });
            }

            var exchangeRate = ResolveExchangeRate(connection, transaction, model.ExchangeRate, DateTime.Today, loan.Currency, currency);
            var movementAmount = ConvertFromCreditCurrency(amount, loan.Currency, currency, exchangeRate);
            var officialReceipt = CreditOperationsSupport.NextCode(
                connection,
                "caja.recibo_oficial_caja",
                "numero_recibo_oficial",
                $"DCK-{DateTime.Today:yyyy}-",
                transaction);

            var disbursementId = InsertCreditDisbursement(connection, transaction, loan.CreditId, amount, loan.Currency, method, exchangeRate, model.Observation);
            var movementId = InsertDisbursementMovement(connection, transaction, loan, movementAmount, currency, exchangeRate, method, session.Username, cashSession.Id, model.Observation);
            InsertOfficialReceipt(connection, transaction, movementId, officialReceipt, loan, movementAmount, currency, model.Observation, "Desembolso de credito");
            AccountingAutomationSupport.RegisterCreditDisbursementEntry(
                connection,
                transaction,
                disbursementId,
                loan.CreditId,
                loan.CreditNumber,
                loan.ClientIdentification,
                loan.ClientName,
                loan.Currency,
                exchangeRate,
                amount,
                amount,
                session.Username);
            ActivateDisbursedLoan(connection, transaction, loan.CreditId, amount);
            if (AffectsCash(method))
            {
                UpdateCashSessionOutflow(connection, transaction, cashSession.Id, movementAmount, currency);
            }

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CAJA",
                "DESEMBOLSO_CREDITO",
                "APLICACION",
                disbursementId,
                officialReceipt,
                $"Desembolso aplicado al credito {loan.CreditNumber}.",
                new
                {
                    creditId = loan.CreditId,
                    creditNumber = loan.CreditNumber,
                    creditAmount = amount,
                    creditCurrency = loan.Currency,
                    movementAmount,
                    movementCurrency = currency,
                    method,
                    exchangeRate,
                    officialReceipt,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Credito desembolsado y voucher generado.",
                data = new
                {
                    disbursementId,
                    movementId,
                    officialReceiptNumber = officialReceipt,
                    printUrl = $"/Caja/VoucherMovimientoHtml?id={movementId}",
                    creditAmount = amount,
                    creditCurrency = loan.Currency,
                    paidAmount = movementAmount,
                    paidCurrency = currency,
                    method,
                    exchangeRate,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo desembolsar el credito.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AnularPago([FromBody] PaymentVoidModel model)
    {
        if (model.PaymentId <= 0 || string.IsNullOrWhiteSpace(model.Reason) || model.Reason.Trim().Length < 8)
        {
            return BadRequest(new { ok = false, message = "Indica el voucher y un motivo de anulacion claro." });
        }

        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            using var transaction = connection.BeginTransaction();
            var cashSession = LoadCurrentCashSession(connection, transaction, session.Username);
            var payment = LoadPaymentForVoid(connection, transaction, model.PaymentId);
            if (payment is null)
            {
                return NotFound(new { ok = false, message = "Voucher no encontrado." });
            }

            if (payment.IsVoided)
            {
                return BadRequest(new { ok = false, message = "El voucher ya esta anulado." });
            }

            var targetCashSessionId = cashSession?.Id ?? payment.CashSessionId;
            var isSameOpenCashSession = cashSession is not null && payment.CashSessionId == cashSession.Id;
            var canReverseClosedCash = cashSession is null && payment.CashSessionId > 0 && CanReverseClosedCash(session);
            if (!isSameOpenCashSession && !canReverseClosedCash)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Solo se pueden anular vouchers de la sesion abierta. Para una caja cerrada se requiere reversa autorizada por administrador, jefe o gerente.",
                });
            }

            ReverseScheduleApplications(connection, transaction, payment.PaymentId);
            ReverseLoanBalances(connection, transaction, payment.CreditId, payment.CapitalApplied, payment.InterestApplied, payment.CommissionApplied);
            MarkPaymentVoided(connection, transaction, payment.PaymentId, session.Username, model.Reason.Trim());
            MarkCashMovementVoided(connection, transaction, payment.MovementId, session.Username, model.Reason.Trim());
            AccountingAutomationSupport.RegisterCreditPaymentVoidEntry(
                connection,
                transaction,
                payment.PaymentId,
                payment.VoucherNumber,
                payment.CreditNumber,
                string.Empty,
                payment.CreditCurrency,
                1,
                payment.CapitalApplied + payment.InterestApplied + payment.CommissionApplied + payment.MoraApplied,
                payment.CapitalApplied,
                payment.InterestApplied,
                payment.CommissionApplied,
                payment.MoraApplied,
                session.Username);
            if (AffectsCash(payment.Method))
            {
                UpdateCashSessionTotals(connection, transaction, targetCashSessionId, -payment.Amount, payment.Currency);
                RefreshCashSessionDifference(connection, transaction, targetCashSessionId);
            }

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CAJA",
                "PAGO_CREDITO",
                "ANULACION",
                payment.PaymentId,
                payment.VoucherNumber,
                $"Voucher {payment.VoucherNumber} anulado.",
                new
                {
                    payment.PaymentId,
                    payment.VoucherNumber,
                    payment.CreditId,
                    payment.CreditNumber,
                    payment.Amount,
                    payment.Currency,
                    payment.CashSessionId,
                    reversalMode = isSameOpenCashSession ? "CAJA_ABIERTA" : "REVERSA_AUTORIZADA_CAJA_CERRADA",
                    model.Reason,
                });

            transaction.Commit();
            return Json(new
            {
                ok = true,
                message = isSameOpenCashSession
                    ? "Voucher anulado y saldos reversados."
                    : "Voucher anulado con reversa autorizada sobre caja cerrada.",
                data = new { payment.PaymentId, payment.VoucherNumber, payment.CashSessionId },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo anular el voucher.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult VoucherPagoHtml(long id, bool reprint = false)
    {
        if (id <= 0)
        {
            return BadRequest("Pago invalido.");
        }

        try
        {
            using var connection = OpenConnection();
            var voucher = LoadVoucher(connection, id);
            if (voucher is null)
            {
                return NotFound("Voucher no encontrado.");
            }

            return Content(BuildVoucherHtml(voucher, reprint), "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo imprimir el voucher: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult VoucherMovimientoHtml(long id, bool reprint = false)
    {
        if (id <= 0)
        {
            return BadRequest("Movimiento invalido.");
        }

        try
        {
            using var connection = OpenConnection();
            var voucher = LoadMovementVoucher(connection, id);
            if (voucher is null)
            {
                return NotFound("Voucher no encontrado.");
            }

            return Content(BuildMovementVoucherHtml(voucher, reprint), "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo imprimir el voucher: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpGet]
    public IActionResult HojaArqueoHtml(long? id = null)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveCashSession(connection);
            if (session is null)
            {
                return Unauthorized("Sesion invalida o expirada.");
            }

            var cashSession = id.HasValue
                ? LoadCashSessionById(connection, id.Value)
                : LoadCurrentCashSession(connection, session.Username);
            if (cashSession is null)
            {
                return NotFound("No hay sesion de caja para imprimir.");
            }

            var report = LoadCashReport(connection, cashSession.Id);
            return Content(BuildCashCountHtml(cashSession, report), "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo imprimir hoja de arqueo: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    private static SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(ConexionDb.Cadena);
        connection.Open();
        CreditOperationsSupport.EnsureSchema(connection);
        CreditPortfolioSecuritySupport.EnsureSchema(connection);
        EnsureCashSchema(connection);
        return connection;
    }

    private CreditPortfolioSession? ResolveCashSession(SqlConnection connection)
    {
        var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
        if (session is null)
        {
            return null;
        }

        return session.HasAnyRole("ADMINISTRADOR", "ADMINISTRACION", "CAJA", "CAJERO", "CREDITO", "OFICIAL_CREDITO", "JEFE_CREDITO", "GERENTE_CREDITO")
            ? session
            : null;
    }

    private static BranchContextDto LoadBranchContext(SqlConnection connection, CreditPortfolioSession session)
    {
        var branches = new List<BranchOptionDto>();
        BranchOptionDto? assignedBranch = null;

        using (var command = new SqlCommand(
            """
            IF OBJECT_ID(N'empresa.sucursal', N'U') IS NULL
            BEGIN
                SELECT CAST(1 AS BIGINT) AS id_sucursal, N'CASA' AS codigo_sucursal, N'Casa Matriz' AS nombre_sucursal, CAST(1 AS BIT) AS asignada
            END
            ELSE
            BEGIN
                SELECT
                    s.id_sucursal,
                    COALESCE(NULLIF(s.codigo_sucursal, N''), CONVERT(NVARCHAR(20), s.id_sucursal)) AS codigo_sucursal,
                    COALESCE(NULLIF(s.nombre_sucursal, N''), NULLIF(s.codigo_sucursal, N''), N'Casa Matriz') AS nombre_sucursal,
                    CASE WHEN u.id_sucursal = s.id_sucursal THEN 1 ELSE 0 END AS asignada
                FROM empresa.sucursal s
                LEFT JOIN seguridad.usuario u
                    ON u.id_usuario = @id_usuario
                WHERE ISNULL(s.activo, 1) = 1
                ORDER BY CASE WHEN u.id_sucursal = s.id_sucursal THEN 0 ELSE 1 END, s.nombre_sucursal;
            END;
            """,
            connection))
        {
            command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = session.UserId;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var branch = new BranchOptionDto
                {
                    Id = ReadInt64(reader, "id_sucursal"),
                    Code = ReadString(reader, "codigo_sucursal", "CASA"),
                    Name = ReadString(reader, "nombre_sucursal", "Casa Matriz"),
                };
                branches.Add(branch);
                if (ReadBool(reader, "asignada"))
                {
                    assignedBranch = branch;
                }
            }
        }

        if (branches.Count == 0)
        {
            branches.Add(new BranchOptionDto { Id = 1, Code = "CASA", Name = "Casa Matriz" });
        }

        assignedBranch ??= branches[0];
        var locked = session.HasAnyRole("CAJA", "CAJERO")
            && !session.HasAnyRole("ADMINISTRADOR", "ADMINISTRACION", "GERENTE_CREDITO");

        return new BranchContextDto
        {
            Branches = branches,
            AssignedBranch = assignedBranch,
            Locked = locked,
        };
    }

    private static string ResolveCashBranch(string? requestedBranch, BranchContextDto branchContext)
    {
        if (branchContext.Locked)
        {
            return branchContext.AssignedBranch?.Name ?? branchContext.Branches.FirstOrDefault()?.Name ?? "Casa Matriz";
        }

        var normalized = requestedBranch?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var match = branchContext.Branches.FirstOrDefault(branch =>
                string.Equals(branch.Name, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(branch.Code, normalized, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.Name;
            }

            return normalized;
        }

        return branchContext.AssignedBranch?.Name ?? branchContext.Branches.FirstOrDefault()?.Name ?? "Casa Matriz";
    }

    private static void EnsureCashSchema(SqlConnection connection)
    {
        const string sql = """
            IF SCHEMA_ID(N'caja') IS NULL EXEC(N'CREATE SCHEMA caja');
            IF OBJECT_ID(N'caja.movimiento_caja', N'U') IS NULL
            BEGIN
                CREATE TABLE caja.movimiento_caja
                (
                    id_movimiento_caja BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_caja_movimiento_caja PRIMARY KEY,
                    fecha_movimiento DATETIME2 NOT NULL CONSTRAINT DF_caja_movimiento_fecha DEFAULT (SYSDATETIME()),
                    tipo_movimiento NVARCHAR(60) NOT NULL,
                    origen_movimiento NVARCHAR(60) NOT NULL,
                    id_pago_credito BIGINT NULL,
                    id_credito BIGINT NULL,
                    monto_movimiento DECIMAL(18,2) NOT NULL,
                    moneda NVARCHAR(20) NOT NULL,
                    tipo_cambio_aplicado DECIMAL(18,6) NULL,
                    descripcion NVARCHAR(1000) NULL,
                    estado_movimiento NVARCHAR(40) NOT NULL CONSTRAINT DF_caja_movimiento_estado DEFAULT (N'APLICADO'),
                    usuario_registro NVARCHAR(200) NULL,
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_caja_movimiento_creacion DEFAULT (SYSDATETIME()),
                    id_sesion_caja BIGINT NULL,
                    anulado BIT NOT NULL CONSTRAINT DF_caja_movimiento_anulado DEFAULT (0),
                    fecha_anulacion DATETIME2 NULL,
                    usuario_anulacion NVARCHAR(200) NULL
                );
            END;
            IF COL_LENGTH(N'caja.movimiento_caja', N'forma_pago') IS NULL
                ALTER TABLE caja.movimiento_caja ADD forma_pago NVARCHAR(60) NULL;
            IF COL_LENGTH(N'caja.movimiento_caja', N'tipo_cambio_aplicado') IS NULL
                ALTER TABLE caja.movimiento_caja ADD tipo_cambio_aplicado DECIMAL(18,6) NULL;

            IF OBJECT_ID(N'creditos.pago_credito', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'creditos.pago_credito', N'monto_recibido_nio') IS NULL
                    ALTER TABLE creditos.pago_credito ADD monto_recibido_nio DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_pago_recibido_nio DEFAULT (0);
                IF COL_LENGTH(N'creditos.pago_credito', N'monto_recibido_usd') IS NULL
                    ALTER TABLE creditos.pago_credito ADD monto_recibido_usd DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_pago_recibido_usd DEFAULT (0);
                IF COL_LENGTH(N'creditos.pago_credito', N'monto_aplicado_moneda_credito') IS NULL
                    ALTER TABLE creditos.pago_credito ADD monto_aplicado_moneda_credito DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_pago_aplicado_moneda_credito DEFAULT (0);
                IF COL_LENGTH(N'creditos.pago_credito', N'detalle_tipo_cambio') IS NULL
                    ALTER TABLE creditos.pago_credito ADD detalle_tipo_cambio NVARCHAR(100) NULL;
                IF COL_LENGTH(N'creditos.pago_credito', N'anulado') IS NULL
                    ALTER TABLE creditos.pago_credito ADD anulado BIT NOT NULL CONSTRAINT DF_creditos_pago_anulado DEFAULT (0);
                IF COL_LENGTH(N'creditos.pago_credito', N'fecha_anulacion') IS NULL
                    ALTER TABLE creditos.pago_credito ADD fecha_anulacion DATETIME2 NULL;
                IF COL_LENGTH(N'creditos.pago_credito', N'usuario_anulacion') IS NULL
                    ALTER TABLE creditos.pago_credito ADD usuario_anulacion NVARCHAR(200) NULL;
                IF COL_LENGTH(N'creditos.pago_credito', N'motivo_anulacion') IS NULL
                    ALTER TABLE creditos.pago_credito ADD motivo_anulacion NVARCHAR(1000) NULL;
            END;

            IF OBJECT_ID(N'caja.recibo_oficial_caja', N'U') IS NULL
            BEGIN
                CREATE TABLE caja.recibo_oficial_caja
                (
                    id_recibo_oficial_caja BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_caja_recibo_oficial PRIMARY KEY,
                    id_movimiento_caja BIGINT NOT NULL,
                    numero_recibo_oficial NVARCHAR(100) NOT NULL,
                    fecha_recibo DATETIME2 NOT NULL CONSTRAINT DF_caja_recibo_fecha DEFAULT (SYSDATETIME()),
                    nombre_cliente NVARCHAR(500) NULL,
                    cedula_cliente NVARCHAR(100) NULL,
                    numero_credito NVARCHAR(100) NULL,
                    concepto NVARCHAR(500) NOT NULL,
                    monto_total DECIMAL(18,2) NOT NULL,
                    moneda NVARCHAR(20) NOT NULL,
                    observacion NVARCHAR(1000) NULL,
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_caja_recibo_creacion DEFAULT (SYSDATETIME()),
                    anulado BIT NOT NULL CONSTRAINT DF_caja_recibo_anulado DEFAULT (0),
                    fecha_anulacion DATETIME2 NULL
                );
            END;

            IF OBJECT_ID(N'creditos.recibo_pago_credito', N'U') IS NULL
            BEGIN
                CREATE TABLE creditos.recibo_pago_credito
                (
                    id_recibo_pago_credito BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_creditos_recibo_pago PRIMARY KEY,
                    id_pago_credito BIGINT NOT NULL,
                    numero_recibo NVARCHAR(100) NOT NULL,
                    fecha_recibo DATETIME2 NOT NULL CONSTRAINT DF_creditos_recibo_pago_fecha DEFAULT (SYSDATETIME()),
                    monto_total DECIMAL(18,2) NOT NULL,
                    moneda NVARCHAR(20) NOT NULL,
                    observacion NVARCHAR(1000) NULL,
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_creditos_recibo_pago_creacion DEFAULT (SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'caja.desglose_arqueo_caja', N'U') IS NULL
            BEGIN
                CREATE TABLE caja.desglose_arqueo_caja
                (
                    id_desglose_arqueo_caja BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_caja_desglose_arqueo PRIMARY KEY,
                    id_sesion_caja BIGINT NOT NULL,
                    tipo_registro NVARCHAR(30) NOT NULL,
                    moneda NVARCHAR(20) NOT NULL,
                    denominacion DECIMAL(18,2) NOT NULL,
                    cantidad INT NOT NULL,
                    monto_total DECIMAL(18,2) NOT NULL,
                    fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_caja_desglose_arqueo_fecha DEFAULT (SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'caja.arqueo_caja', N'U') IS NULL
            BEGIN
                CREATE TABLE caja.arqueo_caja
                (
                    id_arqueo_caja BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_caja_arqueo_caja PRIMARY KEY,
                    id_sesion_caja BIGINT NOT NULL,
                    fecha_arqueo DATETIME2 NOT NULL CONSTRAINT DF_caja_arqueo_fecha DEFAULT (SYSDATETIME()),
                    tipo_arqueo NVARCHAR(30) NOT NULL,
                    monto_teorico_nio DECIMAL(18,2) NOT NULL,
                    monto_teorico_usd DECIMAL(18,2) NOT NULL,
                    monto_fisico_nio DECIMAL(18,2) NOT NULL,
                    monto_fisico_usd DECIMAL(18,2) NOT NULL,
                    diferencia_nio DECIMAL(18,2) NOT NULL,
                    diferencia_usd DECIMAL(18,2) NOT NULL,
                    observacion NVARCHAR(1000) NULL,
                    usuario_registro NVARCHAR(200) NOT NULL,
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_caja_arqueo_creacion DEFAULT (SYSDATETIME())
                );
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
    }

    private static CashSessionDto? LoadCurrentCashSession(SqlConnection connection, string username)
    {
        using var command = new SqlCommand(CurrentCashSessionSql, connection);
        command.Parameters.Add("@usuario_cajero", SqlDbType.NVarChar, 200).Value = username;
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapCashSession(reader) : null;
    }

    private static CashSessionDto? LoadCashSessionById(SqlConnection connection, long id)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                id_sesion_caja,
                fecha_operacion,
                sucursal,
                usuario_cajero,
                monto_apertura_nio,
                monto_apertura_usd,
                fecha_apertura,
                fecha_cierre,
                estado_sesion,
                observacion_apertura,
                observacion_cierre,
                total_ingresos_nio,
                total_ingresos_usd,
                total_egresos_nio,
                total_egresos_usd,
                saldo_teorico_nio,
                saldo_teorico_usd,
                saldo_fisico_nio,
                saldo_fisico_usd,
                diferencia_caja_nio,
                diferencia_caja_usd
            FROM caja.sesion_caja
            WHERE id_sesion_caja = @id_sesion_caja;
            """,
            connection);
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = id;
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapCashSession(reader) : null;
    }

    private static CashSessionDto? LoadCurrentCashSession(SqlConnection connection, SqlTransaction transaction, string username)
    {
        using var command = new SqlCommand(CurrentCashSessionSql, connection, transaction);
        command.Parameters.Add("@usuario_cajero", SqlDbType.NVarChar, 200).Value = username;
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapCashSession(reader) : null;
    }

    private const string CurrentCashSessionSql = """
        SELECT TOP (1)
            id_sesion_caja,
            fecha_operacion,
            sucursal,
            usuario_cajero,
            monto_apertura_nio,
            monto_apertura_usd,
            fecha_apertura,
            fecha_cierre,
            estado_sesion,
            observacion_apertura,
            observacion_cierre,
            total_ingresos_nio,
            total_ingresos_usd,
            total_egresos_nio,
            total_egresos_usd,
            saldo_teorico_nio,
            saldo_teorico_usd,
            saldo_fisico_nio,
            saldo_fisico_usd,
            diferencia_caja_nio,
            diferencia_caja_usd
        FROM caja.sesion_caja
        WHERE usuario_cajero = @usuario_cajero
          AND estado_sesion = N'ABIERTA'
        ORDER BY fecha_apertura DESC, id_sesion_caja DESC;
        """;

    private static CashSessionDto MapCashSession(SqlDataReader reader)
    {
        return new CashSessionDto
        {
            Id = ReadInt64(reader, "id_sesion_caja"),
            OperationDate = ReadDateTime(reader, "fecha_operacion"),
            Branch = ReadString(reader, "sucursal"),
            CashierUser = ReadString(reader, "usuario_cajero"),
            OpeningNio = ReadDecimal(reader, "monto_apertura_nio"),
            OpeningUsd = ReadDecimal(reader, "monto_apertura_usd"),
            OpenedAt = ReadDateTime(reader, "fecha_apertura"),
            ClosedAt = ReadDateTimeNullable(reader, "fecha_cierre"),
            Status = ReadString(reader, "estado_sesion"),
            OpeningNote = ReadString(reader, "observacion_apertura"),
            ClosingNote = ReadString(reader, "observacion_cierre"),
            IncomeNio = ReadDecimal(reader, "total_ingresos_nio"),
            IncomeUsd = ReadDecimal(reader, "total_ingresos_usd"),
            ExpenseNio = ReadDecimal(reader, "total_egresos_nio"),
            ExpenseUsd = ReadDecimal(reader, "total_egresos_usd"),
            TheoreticalNio = ReadDecimal(reader, "saldo_teorico_nio"),
            TheoreticalUsd = ReadDecimal(reader, "saldo_teorico_usd"),
            PhysicalNio = ReadDecimal(reader, "saldo_fisico_nio"),
            PhysicalUsd = ReadDecimal(reader, "saldo_fisico_usd"),
            DifferenceNio = ReadDecimal(reader, "diferencia_caja_nio"),
            DifferenceUsd = ReadDecimal(reader, "diferencia_caja_usd"),
        };
    }

    private static object LoadCashSummary(SqlConnection connection, long cashSessionId)
    {
        using var command = new SqlCommand(
            """
            SELECT
                moneda,
                forma_pago,
                COUNT(1) AS cantidad,
                SUM(monto_movimiento) AS total
            FROM caja.movimiento_caja
            WHERE id_sesion_caja = @id_sesion_caja
              AND estado_movimiento = N'APLICADO'
              AND ISNULL(anulado, 0) = 0
            GROUP BY moneda, forma_pago
            ORDER BY moneda, forma_pago;

            SELECT TOP (30)
                mc.fecha_movimiento,
                mc.tipo_movimiento,
                mc.origen_movimiento,
                mc.monto_movimiento,
                mc.moneda,
                mc.descripcion,
                cr.numero_credito,
                cr.nom_cliente,
                p.numero_recibo
            FROM caja.movimiento_caja mc
            LEFT JOIN creditos.credito cr
                ON cr.id_credito = mc.id_credito
            LEFT JOIN creditos.pago_credito p
                ON p.id_pago_credito = mc.id_pago_credito
            WHERE mc.id_sesion_caja = @id_sesion_caja
              AND ISNULL(mc.anulado, 0) = 0
            ORDER BY mc.fecha_movimiento DESC, mc.id_movimiento_caja DESC;
            """,
            connection);
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;

        using var reader = command.ExecuteReader();
        var byMethod = new List<object>();
        while (reader.Read())
        {
            byMethod.Add(new
            {
                currency = ReadString(reader, "moneda"),
                method = ReadString(reader, "forma_pago"),
                count = ReadInt32(reader, "cantidad"),
                total = ReadDecimal(reader, "total"),
            });
        }

        var movements = new List<object>();
        reader.NextResult();
        while (reader.Read())
        {
            movements.Add(new
            {
                date = ReadDateTime(reader, "fecha_movimiento"),
                type = ReadString(reader, "tipo_movimiento"),
                origin = ReadString(reader, "origen_movimiento"),
                amount = ReadDecimal(reader, "monto_movimiento"),
                currency = ReadString(reader, "moneda"),
                description = ReadString(reader, "descripcion"),
                creditNumber = ReadString(reader, "numero_credito"),
                clientName = ReadString(reader, "nom_cliente"),
                voucherNumber = ReadString(reader, "numero_recibo"),
            });
        }

        return new { byMethod, movements };
    }

    private static CashReportDto LoadCashReport(SqlConnection connection, long cashSessionId)
    {
        using var command = new SqlCommand(
            """
            SELECT
                moneda,
                forma_pago,
                COUNT(1) AS cantidad,
                SUM(monto_movimiento) AS total
            FROM caja.movimiento_caja
            WHERE id_sesion_caja = @id_sesion_caja
              AND estado_movimiento = N'APLICADO'
              AND ISNULL(anulado, 0) = 0
            GROUP BY moneda, forma_pago
            ORDER BY moneda, forma_pago;

            SELECT
                moneda,
                denominacion,
                cantidad,
                monto_total,
                tipo_registro
            FROM caja.desglose_arqueo_caja
            WHERE id_sesion_caja = @id_sesion_caja
            ORDER BY tipo_registro, moneda, denominacion DESC;

            SELECT
                mc.fecha_movimiento,
                mc.tipo_movimiento,
                mc.origen_movimiento,
                mc.monto_movimiento,
                mc.moneda,
                ISNULL(mc.forma_pago, N'') AS forma_pago,
                mc.descripcion,
                cr.numero_credito,
                cr.nom_cliente,
                p.numero_recibo
            FROM caja.movimiento_caja mc
            LEFT JOIN creditos.credito cr
                ON cr.id_credito = mc.id_credito
            LEFT JOIN creditos.pago_credito p
                ON p.id_pago_credito = mc.id_pago_credito
            WHERE mc.id_sesion_caja = @id_sesion_caja
              AND ISNULL(mc.anulado, 0) = 0
            ORDER BY mc.fecha_movimiento;
            """,
            connection);
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
        using var reader = command.ExecuteReader();

        var report = new CashReportDto();
        while (reader.Read())
        {
            report.ByMethod.Add(new CashReportMethodDto
            {
                Currency = ReadString(reader, "moneda"),
                Method = ReadString(reader, "forma_pago"),
                Count = ReadInt32(reader, "cantidad"),
                Total = ReadDecimal(reader, "total"),
            });
        }

        reader.NextResult();
        while (reader.Read())
        {
            report.Breakdown.Add(new CashReportBreakdownDto
            {
                Currency = ReadString(reader, "moneda"),
                Denomination = ReadDecimal(reader, "denominacion"),
                Quantity = ReadInt32(reader, "cantidad"),
                Total = ReadDecimal(reader, "monto_total"),
                Type = ReadString(reader, "tipo_registro"),
            });
        }

        reader.NextResult();
        while (reader.Read())
        {
            report.Movements.Add(new CashReportMovementDto
            {
                Date = ReadDateTime(reader, "fecha_movimiento"),
                Type = ReadString(reader, "tipo_movimiento"),
                Origin = ReadString(reader, "origen_movimiento"),
                Amount = ReadDecimal(reader, "monto_movimiento"),
                Currency = ReadString(reader, "moneda"),
                Method = ReadString(reader, "forma_pago"),
                Description = ReadString(reader, "descripcion"),
                CreditNumber = ReadString(reader, "numero_credito"),
                ClientName = ReadString(reader, "nom_cliente"),
                VoucherNumber = ReadString(reader, "numero_recibo"),
            });
        }

        return report;
    }

    private static void SaveBreakdown(SqlConnection connection, SqlTransaction transaction, long cashSessionId, string type, IReadOnlyList<CashBreakdownLineModel>? lines)
    {
        using (var delete = new SqlCommand(
            """
            DELETE FROM caja.desglose_arqueo_caja
            WHERE id_sesion_caja = @id_sesion_caja
              AND tipo_registro = @tipo_registro;
            """,
            connection,
            transaction))
        {
            delete.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
            delete.Parameters.Add("@tipo_registro", SqlDbType.NVarChar, 30).Value = type;
            delete.ExecuteNonQuery();
        }

        foreach (var line in lines ?? [])
        {
            var quantity = Math.Max(0, line.Quantity);
            var denomination = CreditOperationsSupport.SafeDecimal(line.Denomination);
            if (quantity <= 0 || denomination <= 0)
            {
                continue;
            }

            using var insert = new SqlCommand(
                """
                INSERT INTO caja.desglose_arqueo_caja
                (
                    id_sesion_caja,
                    tipo_registro,
                    moneda,
                    denominacion,
                    cantidad,
                    monto_total
                )
                VALUES
                (
                    @id_sesion_caja,
                    @tipo_registro,
                    @moneda,
                    @denominacion,
                    @cantidad,
                    @monto_total
                );
                """,
                connection,
                transaction);
            insert.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
            insert.Parameters.Add("@tipo_registro", SqlDbType.NVarChar, 30).Value = type;
            insert.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = NormalizeCurrency(line.Currency, "NIO");
            insert.Parameters.Add("@denominacion", SqlDbType.Decimal).Value = denomination;
            insert.Parameters.Add("@cantidad", SqlDbType.Int).Value = quantity;
            insert.Parameters.Add("@monto_total", SqlDbType.Decimal).Value = denomination * quantity;
            insert.ExecuteNonQuery();
        }
    }

    private static long InsertCashCount(
        SqlConnection connection,
        SqlTransaction transaction,
        long cashSessionId,
        string type,
        decimal theoreticalNio,
        decimal theoreticalUsd,
        decimal physicalNio,
        decimal physicalUsd,
        decimal differenceNio,
        decimal differenceUsd,
        string? observation,
        string username)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO caja.arqueo_caja
            (
                id_sesion_caja,
                fecha_arqueo,
                tipo_arqueo,
                monto_teorico_nio,
                monto_teorico_usd,
                monto_fisico_nio,
                monto_fisico_usd,
                diferencia_nio,
                diferencia_usd,
                observacion,
                usuario_registro
            )
            OUTPUT INSERTED.id_arqueo_caja
            VALUES
            (
                @id_sesion_caja,
                SYSDATETIME(),
                @tipo_arqueo,
                @monto_teorico_nio,
                @monto_teorico_usd,
                @monto_fisico_nio,
                @monto_fisico_usd,
                @diferencia_nio,
                @diferencia_usd,
                @observacion,
                @usuario_registro
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
        command.Parameters.Add("@tipo_arqueo", SqlDbType.NVarChar, 30).Value = type;
        command.Parameters.Add("@monto_teorico_nio", SqlDbType.Decimal).Value = theoreticalNio;
        command.Parameters.Add("@monto_teorico_usd", SqlDbType.Decimal).Value = theoreticalUsd;
        command.Parameters.Add("@monto_fisico_nio", SqlDbType.Decimal).Value = physicalNio;
        command.Parameters.Add("@monto_fisico_usd", SqlDbType.Decimal).Value = physicalUsd;
        command.Parameters.Add("@diferencia_nio", SqlDbType.Decimal).Value = differenceNio;
        command.Parameters.Add("@diferencia_usd", SqlDbType.Decimal).Value = differenceUsd;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(observation);
        command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 200).Value = username;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static decimal SumBreakdown(IReadOnlyList<CashBreakdownLineModel>? lines, string currency)
    {
        return CreditOperationsSupport.SafeDecimal((lines ?? [])
            .Where(line => string.Equals(NormalizeCurrency(line.Currency, currency), currency, StringComparison.OrdinalIgnoreCase))
            .Sum(line => Math.Max(0, line.Quantity) * Math.Max(0, line.Denomination)));
    }

    private static decimal SignedDecimal(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal ResolveCashAmount(decimal manualAmount, IReadOnlyList<CashBreakdownLineModel>? lines, string currency)
    {
        if (manualAmount < 0)
        {
            return manualAmount;
        }

        var breakdownAmount = SumBreakdown(lines, currency);
        return breakdownAmount > 0 ? breakdownAmount : CreditOperationsSupport.SafeDecimal(manualAmount);
    }

    private static IReadOnlyDictionary<string, string> ValidateCashAmounts(decimal nioAmount, decimal usdAmount, IReadOnlyList<CashBreakdownLineModel>? lines, string operation)
    {
        var errors = new Dictionary<string, string>();
        if (nioAmount < 0)
        {
            errors["nioAmount"] = $"El monto NIO de {operation} no puede ser negativo.";
        }

        if (usdAmount < 0)
        {
            errors["usdAmount"] = $"El monto USD de {operation} no puede ser negativo.";
        }

        var index = 0;
        foreach (var line in lines ?? [])
        {
            if (line.Quantity < 0)
            {
                errors[$"breakdown[{index}].quantity"] = "La cantidad de billetes o monedas no puede ser negativa.";
            }

            if (line.Denomination <= 0 && line.Quantity > 0)
            {
                errors[$"breakdown[{index}].denomination"] = "La denominacion debe ser mayor que cero.";
            }

            index++;
        }

        return errors;
    }

    private static string DifferenceMessage(decimal differenceNio, decimal differenceUsd, string noDifferenceMessage)
    {
        if (Math.Abs(differenceNio) <= 0.005m && Math.Abs(differenceUsd) <= 0.005m)
        {
            return noDifferenceMessage;
        }

        static string Label(decimal value) => value < 0 ? "faltante" : "sobrante";
        return $"Arqueo con diferencia: NIO {differenceNio:N2} ({Label(differenceNio)}), USD {differenceUsd:N2} ({Label(differenceUsd)}).";
    }

    private static decimal ResolveExchangeRate(SqlConnection connection, SqlTransaction transaction, decimal? modelRate, DateTime date, string receivedCurrency, string creditCurrency)
    {
        var rates = LoadInstitutionalExchangeRates(connection, transaction, date);
        receivedCurrency = NormalizeCurrency(receivedCurrency, "NIO");
        creditCurrency = NormalizeCurrency(creditCurrency, "NIO");
        if (receivedCurrency == "USD" && creditCurrency == "NIO")
        {
            return rates.Buy;
        }

        if (receivedCurrency == "NIO" && creditCurrency == "USD")
        {
            return rates.Sell;
        }

        return rates.Buy;
    }

    private static InstitutionalExchangeRateDto LoadInstitutionalExchangeRates(SqlConnection connection, DateTime date)
    {
        return LoadInstitutionalExchangeRates(connection, null, date);
    }

    private static InstitutionalExchangeRateDto LoadInstitutionalExchangeRates(SqlConnection connection, SqlTransaction? transaction, DateTime date)
    {
        using var command = transaction is null
            ? new SqlCommand(ExchangeRateSql, connection)
            : new SqlCommand(ExchangeRateSql, connection, transaction);
        command.Parameters.Add("@fecha", SqlDbType.Date).Value = date.Date;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("No hay tipo de cambio institucional USD/NIO disponible para la fecha de pago.");
        }

        return new InstitutionalExchangeRateDto
        {
            Date = ReadDateTime(reader, "fecha_tipo_cambio"),
            Buy = Math.Round(ReadDecimal(reader, "valor_compra"), 6, MidpointRounding.AwayFromZero),
            Sell = Math.Round(ReadDecimal(reader, "valor_venta"), 6, MidpointRounding.AwayFromZero),
            Reference = Math.Round(ReadDecimal(reader, "valor_referencia"), 6, MidpointRounding.AwayFromZero),
        };
    }

    private const string ExchangeRateSql = """
        SELECT TOP (1)
            fecha_tipo_cambio,
            valor_compra,
            valor_venta,
            valor_referencia
        FROM parametros.tipo_cambio_institucional
        WHERE moneda_origen = N'USD'
          AND moneda_destino = N'NIO'
          AND fecha_tipo_cambio <= @fecha
        ORDER BY fecha_tipo_cambio DESC, id_tipo_cambio_institucional DESC;
        """;

    private static decimal ConvertToCreditCurrency(decimal receivedAmount, string receivedCurrency, string creditCurrency, decimal exchangeRate)
    {
        receivedCurrency = NormalizeCurrency(receivedCurrency, "NIO");
        creditCurrency = NormalizeCurrency(creditCurrency, "NIO");
        if (receivedCurrency == creditCurrency)
        {
            return CreditOperationsSupport.SafeDecimal(receivedAmount);
        }

        if (receivedCurrency == "USD" && creditCurrency == "NIO")
        {
            return CreditOperationsSupport.SafeDecimal(receivedAmount * exchangeRate);
        }

        if (receivedCurrency == "NIO" && creditCurrency == "USD")
        {
            return CreditOperationsSupport.SafeDecimal(receivedAmount / exchangeRate);
        }

        return CreditOperationsSupport.SafeDecimal(receivedAmount);
    }

    private static decimal ConvertFromCreditCurrency(decimal creditAmount, string creditCurrency, string paidCurrency, decimal exchangeRate)
    {
        creditCurrency = NormalizeCurrency(creditCurrency, "NIO");
        paidCurrency = NormalizeCurrency(paidCurrency, creditCurrency);
        if (creditCurrency == paidCurrency)
        {
            return CreditOperationsSupport.SafeDecimal(creditAmount);
        }

        if (creditCurrency == "USD" && paidCurrency == "NIO")
        {
            return CreditOperationsSupport.SafeDecimal(creditAmount * exchangeRate);
        }

        if (creditCurrency == "NIO" && paidCurrency == "USD")
        {
            return exchangeRate > 0 ? CreditOperationsSupport.SafeDecimal(creditAmount / exchangeRate) : 0;
        }

        return CreditOperationsSupport.SafeDecimal(creditAmount);
    }

    private static PaymentLoanDto? LoadLoanForPayment(SqlConnection connection, SqlTransaction transaction, long creditId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                cr.id_credito,
                COALESCE(NULLIF(cr.numero_credito, N''), cr.cedula_id_cliente_ofic_ciclo, N'') AS numero_credito,
                cr.id_cliente,
                cr.cedula_id_cliente,
                cr.nom_cliente,
                COALESCE(cr.moneda, N'NIO') AS moneda,
                COALESCE(cr.saldo_capital, 0) AS saldo_capital
            FROM creditos.credito cr WITH (UPDLOCK, ROWLOCK)
            WHERE cr.id_credito = @id_credito
              AND cr.activo = 1
              AND cr.fecha_desembolso IS NOT NULL
              AND cr.estado_operativo NOT IN (N'APROBADO', N'PENDIENTE_DESEMBOLSO');
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PaymentLoanDto
        {
            CreditId = ReadInt64(reader, "id_credito"),
            CreditNumber = ReadString(reader, "numero_credito"),
            ClientId = ReadInt64Nullable(reader, "id_cliente"),
            ClientIdentification = ReadString(reader, "cedula_id_cliente"),
            ClientName = ReadString(reader, "nom_cliente"),
            Currency = ReadString(reader, "moneda", "NIO"),
            PrincipalBalance = ReadDecimal(reader, "saldo_capital"),
        };
    }

    private static PaymentLoanDto? LoadLoanForDisbursement(SqlConnection connection, SqlTransaction transaction, long creditId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                cr.id_credito,
                COALESCE(NULLIF(cr.numero_credito, N''), cr.cedula_id_cliente_ofic_ciclo, N'') AS numero_credito,
                cr.id_cliente,
                cr.cedula_id_cliente,
                cr.nom_cliente,
                COALESCE(cr.moneda, N'NIO') AS moneda,
                COALESCE(cr.saldo_capital, 0) AS saldo_capital,
                COALESCE(cr.monto_aprobado, 0) AS monto_aprobado
            FROM creditos.credito cr WITH (UPDLOCK, ROWLOCK)
            INNER JOIN creditos.solicitud_credito s
                ON s.id_solicitud_credito = cr.id_solicitud_credito
            WHERE cr.id_credito = @id_credito
              AND cr.activo = 1
              AND s.estado_solicitud = N'APROBADA'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM creditos.desembolso_credito d
                  WHERE d.id_credito = cr.id_credito
              );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PaymentLoanDto
        {
            CreditId = ReadInt64(reader, "id_credito"),
            CreditNumber = ReadString(reader, "numero_credito"),
            ClientId = ReadInt64Nullable(reader, "id_cliente"),
            ClientIdentification = ReadString(reader, "cedula_id_cliente"),
            ClientName = ReadString(reader, "nom_cliente"),
            Currency = ReadString(reader, "moneda", "NIO"),
            PrincipalBalance = ReadDecimal(reader, "saldo_capital"),
            ApprovedAmount = ReadDecimal(reader, "monto_aprobado"),
        };
    }

    private static long InsertPayment(
        SqlConnection connection,
        SqlTransaction transaction,
        PaymentLoanDto loan,
        PaymentApplyModel model,
        decimal amount,
        decimal appliedAmount,
        string currency,
        decimal exchangeRate,
        string method,
        string voucherNumber,
        string payerName,
        string payerId)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO creditos.pago_credito
            (
                id_credito,
                fecha_pago,
                monto_pagado,
                moneda_pago,
                tipo_cambio_aplicado,
                forma_pago,
                numero_recibo,
                observacion,
                es_mismo_abonante,
                nombre_abonante,
                cedula_abonante,
                telefono_abonante,
                recibo_manual,
                monto_recibido_nio,
                monto_recibido_usd,
                monto_aplicado_moneda_credito,
                detalle_tipo_cambio,
                estado_pago,
                anulado
            )
            OUTPUT INSERTED.id_pago_credito
            VALUES
            (
                @id_credito,
                SYSDATETIME(),
                @monto_pagado,
                @moneda_pago,
                @tipo_cambio,
                @forma_pago,
                @numero_recibo,
                @observacion,
                @es_mismo_abonante,
                @nombre_abonante,
                @cedula_abonante,
                @telefono_abonante,
                @recibo_manual,
                @monto_recibido_nio,
                @monto_recibido_usd,
                @monto_aplicado_moneda_credito,
                @detalle_tipo_cambio,
                N'APLICADO',
                0
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = loan.CreditId;
        command.Parameters.Add("@monto_pagado", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda_pago", SqlDbType.NVarChar, 20).Value = currency;
        command.Parameters.Add("@tipo_cambio", SqlDbType.Decimal).Value = exchangeRate;
        command.Parameters.Add("@forma_pago", SqlDbType.NVarChar, 60).Value = method;
        command.Parameters.Add("@numero_recibo", SqlDbType.NVarChar, 100).Value = voucherNumber;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(model.Observation);
        command.Parameters.Add("@es_mismo_abonante", SqlDbType.Bit).Value = string.Equals(payerId, loan.ClientIdentification, StringComparison.OrdinalIgnoreCase);
        command.Parameters.Add("@nombre_abonante", SqlDbType.NVarChar, 500).Value = payerName;
        command.Parameters.Add("@cedula_abonante", SqlDbType.NVarChar, 100).Value = payerId;
        command.Parameters.Add("@telefono_abonante", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.TextOrDbNull(model.PayerPhone);
        command.Parameters.Add("@recibo_manual", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.TextOrDbNull(model.ManualReceipt);
        command.Parameters.Add("@monto_recibido_nio", SqlDbType.Decimal).Value = currency == "NIO" ? amount : 0;
        command.Parameters.Add("@monto_recibido_usd", SqlDbType.Decimal).Value = currency == "USD" ? amount : 0;
        command.Parameters.Add("@monto_aplicado_moneda_credito", SqlDbType.Decimal).Value = appliedAmount;
        command.Parameters.Add("@detalle_tipo_cambio", SqlDbType.NVarChar, 100).Value = currency == loan.Currency
            ? $"Sin conversion ({currency})"
            : $"{currency}->{loan.Currency} TC institucional {exchangeRate:N6}";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static PaymentAllocationDto ApplyPaymentToSchedule(SqlConnection connection, SqlTransaction transaction, long creditId, long paymentId, decimal amount)
    {
        var remaining = amount;
        var result = new PaymentAllocationDto();
        var order = 1;

        using var command = new SqlCommand(
            """
            SELECT
                id_plan_pago_credito,
                capital_programado,
                interes_programado,
                comision_programada,
                mora_programada,
                capital_pagado_cuota,
                interes_pagado_cuota,
                comision_pagada_cuota,
                mora_pagada_cuota
            FROM creditos.plan_pago_credito WITH (UPDLOCK, ROWLOCK)
            WHERE id_credito = @id_credito
              AND pagada = 0
            ORDER BY fecha_cuota, numero_cuota;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;

        var rows = new List<PaymentPlanPendingDto>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add(new PaymentPlanPendingDto
                {
                    PlanId = ReadInt64(reader, "id_plan_pago_credito"),
                    PendingCapital = Math.Max(0, ReadDecimal(reader, "capital_programado") - ReadDecimal(reader, "capital_pagado_cuota")),
                    PendingInterest = Math.Max(0, ReadDecimal(reader, "interes_programado") - ReadDecimal(reader, "interes_pagado_cuota")),
                    PendingCommission = Math.Max(0, ReadDecimal(reader, "comision_programada") - ReadDecimal(reader, "comision_pagada_cuota")),
                    PendingMora = Math.Max(0, ReadDecimal(reader, "mora_programada") - ReadDecimal(reader, "mora_pagada_cuota")),
                });
            }
        }

        foreach (var row in rows)
        {
            ApplyRubric(connection, transaction, paymentId, row.PlanId, "INTERES", row.PendingInterest, ref remaining, ref order, result);
            ApplyRubric(connection, transaction, paymentId, row.PlanId, "MORA", row.PendingMora, ref remaining, ref order, result);
            ApplyRubric(connection, transaction, paymentId, row.PlanId, "COMISION", row.PendingCommission, ref remaining, ref order, result);
            ApplyRubric(connection, transaction, paymentId, row.PlanId, "CAPITAL", row.PendingCapital, ref remaining, ref order, result);
            MarkInstallmentIfPaid(connection, transaction, row.PlanId);
            if (remaining <= 0.005m)
            {
                break;
            }
        }

        if (remaining > 0.005m)
        {
            InsertGlobalApplication(connection, transaction, paymentId, "CAPITAL_ANTICIPADO", remaining, order++);
            result.Capital += remaining;
            result.TotalApplied += remaining;
            remaining = 0;
        }

        return result;
    }

    private static void ApplyRubric(
        SqlConnection connection,
        SqlTransaction transaction,
        long paymentId,
        long planId,
        string rubric,
        decimal pending,
        ref decimal remaining,
        ref int order,
        PaymentAllocationDto result)
    {
        if (remaining <= 0.005m || pending <= 0.005m)
        {
            return;
        }

        var applied = CreditOperationsSupport.SafeDecimal(Math.Min(remaining, pending));
        var column = rubric switch
        {
            "MORA" => "mora_pagada_cuota",
            "INTERES" => "interes_pagado_cuota",
            "COMISION" => "comision_pagada_cuota",
            _ => "capital_pagado_cuota",
        };

        using (var update = new SqlCommand(
            $"""
            UPDATE creditos.plan_pago_credito
            SET {column} = ISNULL({column}, 0) + @monto
            WHERE id_plan_pago_credito = @id_plan_pago_credito;
            """,
            connection,
            transaction))
        {
            update.Parameters.Add("@monto", SqlDbType.Decimal).Value = applied;
            update.Parameters.Add("@id_plan_pago_credito", SqlDbType.BigInt).Value = planId;
            update.ExecuteNonQuery();
        }

        InsertCuotaApplication(connection, transaction, paymentId, planId, rubric, applied, order);
        InsertGlobalApplication(connection, transaction, paymentId, rubric, applied, order);
        order++;
        remaining -= applied;
        result.TotalApplied += applied;
        if (rubric == "MORA") result.Mora += applied;
        else if (rubric == "INTERES") result.Interest += applied;
        else if (rubric == "COMISION") result.Commission += applied;
        else result.Capital += applied;
    }

    private static void MarkInstallmentIfPaid(SqlConnection connection, SqlTransaction transaction, long planId)
    {
        using var command = new SqlCommand(
            """
            UPDATE creditos.plan_pago_credito
            SET
                pagada = CASE
                    WHEN capital_pagado_cuota >= capital_programado
                     AND interes_pagado_cuota >= interes_programado
                     AND comision_pagada_cuota >= comision_programada
                     AND mora_pagada_cuota >= mora_programada THEN 1
                    ELSE pagada
                END,
                estado_cuota = CASE
                    WHEN capital_pagado_cuota >= capital_programado
                     AND interes_pagado_cuota >= interes_programado
                     AND comision_pagada_cuota >= comision_programada
                     AND mora_pagada_cuota >= mora_programada THEN N'PAGADA'
                    ELSE estado_cuota
                END
            WHERE id_plan_pago_credito = @id_plan_pago_credito;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_plan_pago_credito", SqlDbType.BigInt).Value = planId;
        command.ExecuteNonQuery();
    }

    private static void InsertCuotaApplication(SqlConnection connection, SqlTransaction transaction, long paymentId, long planId, string rubric, decimal amount, int order)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO creditos.aplicacion_pago_cuota
            (
                id_pago_credito,
                id_plan_pago_credito,
                rubro,
                monto_aplicado,
                orden_aplicacion
            )
            VALUES
            (
                @id_pago_credito,
                @id_plan_pago_credito,
                @rubro,
                @monto_aplicado,
                @orden_aplicacion
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;
        command.Parameters.Add("@id_plan_pago_credito", SqlDbType.BigInt).Value = planId;
        command.Parameters.Add("@rubro", SqlDbType.NVarChar, 60).Value = rubric;
        command.Parameters.Add("@monto_aplicado", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@orden_aplicacion", SqlDbType.Int).Value = order;
        command.ExecuteNonQuery();
    }

    private static void InsertGlobalApplication(SqlConnection connection, SqlTransaction transaction, long paymentId, string rubric, decimal amount, int order)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO creditos.aplicacion_pago_credito
            (
                id_pago_credito,
                rubro,
                monto_aplicado,
                orden_aplicacion
            )
            VALUES
            (
                @id_pago_credito,
                @rubro,
                @monto_aplicado,
                @orden_aplicacion
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;
        command.Parameters.Add("@rubro", SqlDbType.NVarChar, 60).Value = rubric;
        command.Parameters.Add("@monto_aplicado", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@orden_aplicacion", SqlDbType.Int).Value = order;
        command.ExecuteNonQuery();
    }

    private static void UpdateLoanBalances(SqlConnection connection, SqlTransaction transaction, long creditId, PaymentAllocationDto allocation)
    {
        using var command = new SqlCommand(
            """
            UPDATE creditos.credito
            SET
                saldo_capital = CASE WHEN ISNULL(saldo_capital, 0) - @capital < 0 THEN 0 ELSE ISNULL(saldo_capital, 0) - @capital END,
                interes_pagado = ISNULL(interes_pagado, 0) + @interes,
                comision_pagada = ISNULL(comision_pagada, 0) + @comision,
                fecha_cancelacion = CASE WHEN ISNULL(saldo_capital, 0) - @capital <= 0 THEN CONVERT(date, SYSDATETIME()) ELSE fecha_cancelacion END,
                estado_operativo = CASE WHEN ISNULL(saldo_capital, 0) - @capital <= 0 THEN N'CA' ELSE estado_operativo END
            WHERE id_credito = @id_credito;
            """,
            connection,
            transaction);
        command.Parameters.Add("@capital", SqlDbType.Decimal).Value = allocation.Capital;
        command.Parameters.Add("@interes", SqlDbType.Decimal).Value = allocation.Interest;
        command.Parameters.Add("@comision", SqlDbType.Decimal).Value = allocation.Commission;
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
        command.ExecuteNonQuery();
    }

    private static long InsertCashMovement(SqlConnection connection, SqlTransaction transaction, PaymentLoanDto loan, long paymentId, decimal amount, string currency, decimal exchangeRate, decimal appliedAmount, string method, string username, long cashSessionId)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO caja.movimiento_caja
            (
                fecha_movimiento,
                tipo_movimiento,
                origen_movimiento,
                id_pago_credito,
                id_credito,
                monto_movimiento,
                moneda,
                tipo_cambio_aplicado,
                forma_pago,
                descripcion,
                estado_movimiento,
                usuario_registro,
                id_sesion_caja,
                anulado
            )
            OUTPUT INSERTED.id_movimiento_caja
            VALUES
            (
                SYSDATETIME(),
                N'INGRESO',
                N'PAGO_CREDITO',
                @id_pago_credito,
                @id_credito,
                @monto_movimiento,
                @moneda,
                @tipo_cambio_aplicado,
                @forma_pago,
                @descripcion,
                N'APLICADO',
                @usuario_registro,
                @id_sesion_caja,
                0
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = loan.CreditId;
        command.Parameters.Add("@monto_movimiento", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = currency;
        command.Parameters.Add("@tipo_cambio_aplicado", SqlDbType.Decimal).Value = exchangeRate;
        command.Parameters.Add("@forma_pago", SqlDbType.NVarChar, 60).Value = method;
        command.Parameters.Add("@descripcion", SqlDbType.NVarChar, 1000).Value = currency == loan.Currency
            ? $"Pago {method} credito {loan.CreditNumber} recibido en {currency}."
            : $"Pago {method} credito {loan.CreditNumber}: recibido {currency} {amount:N2}, aplicado {loan.Currency} {appliedAmount:N2}, TC {exchangeRate:N6}.";
        command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 200).Value = username;
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static long InsertCreditDisbursement(
        SqlConnection connection,
        SqlTransaction transaction,
        long creditId,
        decimal amount,
        string currency,
        string method,
        decimal exchangeRate,
        string? observation)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO creditos.desembolso_credito
            (
                id_credito,
                fecha_desembolso,
                monto_desembolsado,
                moneda,
                tipo_cambio_institucion,
                forma_desembolso,
                observacion
            )
            OUTPUT INSERTED.id_desembolso_credito
            VALUES
            (
                @id_credito,
                CONVERT(date, SYSDATETIME()),
                @monto_desembolsado,
                @moneda,
                @tipo_cambio_institucion,
                @forma_desembolso,
                @observacion
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
        command.Parameters.Add("@monto_desembolsado", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = currency;
        command.Parameters.Add("@tipo_cambio_institucion", SqlDbType.Decimal).Value = exchangeRate;
        command.Parameters.Add("@forma_desembolso", SqlDbType.NVarChar, 80).Value = method;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(observation);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static long InsertDisbursementMovement(SqlConnection connection, SqlTransaction transaction, PaymentLoanDto loan, decimal amount, string currency, decimal exchangeRate, string method, string username, long cashSessionId, string? observation)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO caja.movimiento_caja
            (
                fecha_movimiento,
                tipo_movimiento,
                origen_movimiento,
                id_credito,
                monto_movimiento,
                moneda,
                tipo_cambio_aplicado,
                forma_pago,
                descripcion,
                estado_movimiento,
                usuario_registro,
                id_sesion_caja,
                anulado
            )
            OUTPUT INSERTED.id_movimiento_caja
            VALUES
            (
                SYSDATETIME(),
                N'EGRESO',
                N'DESEMBOLSO_CREDITO',
                @id_credito,
                @monto_movimiento,
                @moneda,
                @tipo_cambio_aplicado,
                @forma_pago,
                @descripcion,
                N'APLICADO',
                @usuario_registro,
                @id_sesion_caja,
                0
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = loan.CreditId;
        command.Parameters.Add("@monto_movimiento", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = currency;
        command.Parameters.Add("@tipo_cambio_aplicado", SqlDbType.Decimal).Value = exchangeRate;
        command.Parameters.Add("@forma_pago", SqlDbType.NVarChar, 60).Value = method;
        command.Parameters.Add("@descripcion", SqlDbType.NVarChar, 1000).Value =
            CreditOperationsSupport.TextOrDbNull(string.IsNullOrWhiteSpace(observation)
                ? $"Desembolso {method} credito {loan.CreditNumber}."
                : $"Desembolso {method} credito {loan.CreditNumber}. {observation.Trim()}");
        command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 200).Value = username;
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void ActivateDisbursedLoan(SqlConnection connection, SqlTransaction transaction, long creditId, decimal amount)
    {
        using var command = new SqlCommand(
            """
            UPDATE creditos.credito
            SET
                fecha_desembolso = CONVERT(date, SYSDATETIME()),
                estado_operativo = N'VI',
                saldo_capital = @monto_desembolsado,
                fecha_vencimiento = COALESCE(fecha_vencimiento, DATEADD(MONTH, plazo_meses, CONVERT(date, SYSDATETIME())))
            WHERE id_credito = @id_credito;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
        command.Parameters.Add("@monto_desembolsado", SqlDbType.Decimal).Value = amount;
        command.ExecuteNonQuery();
    }

    private static void UpdateCashSessionTotals(SqlConnection connection, SqlTransaction transaction, long cashSessionId, decimal amount, string currency)
    {
        using var command = new SqlCommand(
            """
            UPDATE caja.sesion_caja
            SET
                total_ingresos_nio = total_ingresos_nio + CASE WHEN @moneda = N'NIO' THEN @monto ELSE 0 END,
                total_ingresos_usd = total_ingresos_usd + CASE WHEN @moneda = N'USD' THEN @monto ELSE 0 END,
                saldo_teorico_nio = saldo_teorico_nio + CASE WHEN @moneda = N'NIO' THEN @monto ELSE 0 END,
                saldo_teorico_usd = saldo_teorico_usd + CASE WHEN @moneda = N'USD' THEN @monto ELSE 0 END
            WHERE id_sesion_caja = @id_sesion_caja;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
        command.Parameters.Add("@monto", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = currency;
        command.ExecuteNonQuery();
    }

    private static void RefreshCashSessionDifference(SqlConnection connection, SqlTransaction transaction, long cashSessionId)
    {
        using var command = new SqlCommand(
            """
            UPDATE caja.sesion_caja
            SET
                diferencia_caja_nio = ISNULL(saldo_fisico_nio, 0) - ISNULL(saldo_teorico_nio, 0),
                diferencia_caja_usd = ISNULL(saldo_fisico_usd, 0) - ISNULL(saldo_teorico_usd, 0)
            WHERE id_sesion_caja = @id_sesion_caja
              AND estado_sesion = N'CERRADA';
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
        command.ExecuteNonQuery();
    }

    private static void UpdateCashSessionOutflow(SqlConnection connection, SqlTransaction transaction, long cashSessionId, decimal amount, string currency)
    {
        using var command = new SqlCommand(
            """
            UPDATE caja.sesion_caja
            SET
                total_egresos_nio = total_egresos_nio + CASE WHEN @moneda = N'NIO' THEN @monto ELSE 0 END,
                total_egresos_usd = total_egresos_usd + CASE WHEN @moneda = N'USD' THEN @monto ELSE 0 END,
                saldo_teorico_nio = saldo_teorico_nio - CASE WHEN @moneda = N'NIO' THEN @monto ELSE 0 END,
                saldo_teorico_usd = saldo_teorico_usd - CASE WHEN @moneda = N'USD' THEN @monto ELSE 0 END
            WHERE id_sesion_caja = @id_sesion_caja;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_sesion_caja", SqlDbType.BigInt).Value = cashSessionId;
        command.Parameters.Add("@monto", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = currency;
        command.ExecuteNonQuery();
    }

    private static PaymentVoidDto? LoadPaymentForVoid(SqlConnection connection, SqlTransaction transaction, long paymentId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                p.id_pago_credito,
                p.id_credito,
                p.numero_recibo,
                p.monto_pagado,
                p.moneda_pago,
                p.forma_pago,
                ISNULL(p.anulado, 0) AS anulado,
                cr.numero_credito,
                cr.moneda AS moneda_credito,
                ISNULL(mc.id_movimiento_caja, 0) AS id_movimiento_caja,
                ISNULL(mc.id_sesion_caja, 0) AS id_sesion_caja,
                ISNULL(mc.anulado, 0) AS movimiento_anulado,
                ISNULL(SUM(CASE WHEN ap.rubro IN (N'CAPITAL', N'CAPITAL_ANTICIPADO') THEN ap.monto_aplicado ELSE 0 END), 0) AS capital_aplicado,
                ISNULL(SUM(CASE WHEN ap.rubro = N'INTERES' THEN ap.monto_aplicado ELSE 0 END), 0) AS interes_aplicado,
                ISNULL(SUM(CASE WHEN ap.rubro = N'COMISION' THEN ap.monto_aplicado ELSE 0 END), 0) AS comision_aplicada,
                ISNULL(SUM(CASE WHEN ap.rubro = N'MORA' THEN ap.monto_aplicado ELSE 0 END), 0) AS mora_aplicada
            FROM creditos.pago_credito p WITH (UPDLOCK, ROWLOCK)
            INNER JOIN creditos.credito cr
                ON cr.id_credito = p.id_credito
            LEFT JOIN caja.movimiento_caja mc WITH (UPDLOCK, ROWLOCK)
                ON mc.id_pago_credito = p.id_pago_credito
            LEFT JOIN creditos.aplicacion_pago_credito ap
                ON ap.id_pago_credito = p.id_pago_credito
            WHERE p.id_pago_credito = @id_pago_credito
            GROUP BY
                p.id_pago_credito,
                p.id_credito,
                p.numero_recibo,
                p.monto_pagado,
                p.moneda_pago,
                p.forma_pago,
                p.anulado,
                cr.numero_credito,
                cr.moneda,
                mc.id_movimiento_caja,
                mc.id_sesion_caja,
                mc.anulado;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PaymentVoidDto
        {
            PaymentId = ReadInt64(reader, "id_pago_credito"),
            CreditId = ReadInt64(reader, "id_credito"),
            VoucherNumber = ReadString(reader, "numero_recibo"),
            Amount = ReadDecimal(reader, "monto_pagado"),
            Currency = ReadString(reader, "moneda_pago", "NIO"),
            CreditCurrency = ReadString(reader, "moneda_credito", ReadString(reader, "moneda_pago", "NIO")),
            Method = ReadString(reader, "forma_pago", "EFECTIVO"),
            IsVoided = ReadBool(reader, "anulado") || ReadBool(reader, "movimiento_anulado"),
            CreditNumber = ReadString(reader, "numero_credito"),
            MovementId = ReadInt64(reader, "id_movimiento_caja"),
            CashSessionId = ReadInt64(reader, "id_sesion_caja"),
            CapitalApplied = ReadDecimal(reader, "capital_aplicado"),
            InterestApplied = ReadDecimal(reader, "interes_aplicado"),
            CommissionApplied = ReadDecimal(reader, "comision_aplicada"),
            MoraApplied = ReadDecimal(reader, "mora_aplicada"),
        };
    }

    private static void ReverseScheduleApplications(SqlConnection connection, SqlTransaction transaction, long paymentId)
    {
        using var command = new SqlCommand(
            """
            UPDATE pp
            SET
                capital_pagado_cuota = CASE WHEN ISNULL(pp.capital_pagado_cuota, 0) - x.capital < 0 THEN 0 ELSE ISNULL(pp.capital_pagado_cuota, 0) - x.capital END,
                interes_pagado_cuota = CASE WHEN ISNULL(pp.interes_pagado_cuota, 0) - x.interes < 0 THEN 0 ELSE ISNULL(pp.interes_pagado_cuota, 0) - x.interes END,
                comision_pagada_cuota = CASE WHEN ISNULL(pp.comision_pagada_cuota, 0) - x.comision < 0 THEN 0 ELSE ISNULL(pp.comision_pagada_cuota, 0) - x.comision END,
                mora_pagada_cuota = CASE WHEN ISNULL(pp.mora_pagada_cuota, 0) - x.mora < 0 THEN 0 ELSE ISNULL(pp.mora_pagada_cuota, 0) - x.mora END,
                pagada = CASE
                    WHEN CASE WHEN ISNULL(pp.capital_pagado_cuota, 0) - x.capital < 0 THEN 0 ELSE ISNULL(pp.capital_pagado_cuota, 0) - x.capital END >= pp.capital_programado
                     AND CASE WHEN ISNULL(pp.interes_pagado_cuota, 0) - x.interes < 0 THEN 0 ELSE ISNULL(pp.interes_pagado_cuota, 0) - x.interes END >= pp.interes_programado
                     AND CASE WHEN ISNULL(pp.comision_pagada_cuota, 0) - x.comision < 0 THEN 0 ELSE ISNULL(pp.comision_pagada_cuota, 0) - x.comision END >= pp.comision_programada
                     AND CASE WHEN ISNULL(pp.mora_pagada_cuota, 0) - x.mora < 0 THEN 0 ELSE ISNULL(pp.mora_pagada_cuota, 0) - x.mora END >= pp.mora_programada THEN 1
                    ELSE 0
                END,
                estado_cuota = CASE
                    WHEN CASE WHEN ISNULL(pp.capital_pagado_cuota, 0) - x.capital < 0 THEN 0 ELSE ISNULL(pp.capital_pagado_cuota, 0) - x.capital END >= pp.capital_programado
                     AND CASE WHEN ISNULL(pp.interes_pagado_cuota, 0) - x.interes < 0 THEN 0 ELSE ISNULL(pp.interes_pagado_cuota, 0) - x.interes END >= pp.interes_programado
                     AND CASE WHEN ISNULL(pp.comision_pagada_cuota, 0) - x.comision < 0 THEN 0 ELSE ISNULL(pp.comision_pagada_cuota, 0) - x.comision END >= pp.comision_programada
                     AND CASE WHEN ISNULL(pp.mora_pagada_cuota, 0) - x.mora < 0 THEN 0 ELSE ISNULL(pp.mora_pagada_cuota, 0) - x.mora END >= pp.mora_programada THEN N'PAGADA'
                    WHEN pp.fecha_cuota < CONVERT(date, SYSDATETIME()) THEN N'VENCIDA'
                    ELSE N'PENDIENTE'
                END
            FROM creditos.plan_pago_credito pp
            INNER JOIN
            (
                SELECT
                    id_plan_pago_credito,
                    SUM(CASE WHEN rubro = N'CAPITAL' THEN monto_aplicado ELSE 0 END) AS capital,
                    SUM(CASE WHEN rubro = N'INTERES' THEN monto_aplicado ELSE 0 END) AS interes,
                    SUM(CASE WHEN rubro = N'COMISION' THEN monto_aplicado ELSE 0 END) AS comision,
                    SUM(CASE WHEN rubro = N'MORA' THEN monto_aplicado ELSE 0 END) AS mora
                FROM creditos.aplicacion_pago_cuota
                WHERE id_pago_credito = @id_pago_credito
                GROUP BY id_plan_pago_credito
            ) x
                ON x.id_plan_pago_credito = pp.id_plan_pago_credito;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;
        command.ExecuteNonQuery();
    }

    private static void ReverseLoanBalances(SqlConnection connection, SqlTransaction transaction, long creditId, decimal capital, decimal interest, decimal commission)
    {
        using var command = new SqlCommand(
            """
            UPDATE creditos.credito
            SET
                saldo_capital = ISNULL(saldo_capital, 0) + @capital,
                interes_pagado = CASE WHEN ISNULL(interes_pagado, 0) - @interes < 0 THEN 0 ELSE ISNULL(interes_pagado, 0) - @interes END,
                comision_pagada = CASE WHEN ISNULL(comision_pagada, 0) - @comision < 0 THEN 0 ELSE ISNULL(comision_pagada, 0) - @comision END,
                fecha_cancelacion = CASE WHEN ISNULL(saldo_capital, 0) + @capital > 0 THEN NULL ELSE fecha_cancelacion END,
                estado_operativo = CASE WHEN ISNULL(saldo_capital, 0) + @capital > 0 AND estado_operativo = N'CA' THEN N'VI' ELSE estado_operativo END
            WHERE id_credito = @id_credito;
            """,
            connection,
            transaction);
        command.Parameters.Add("@capital", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(capital);
        command.Parameters.Add("@interes", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(interest);
        command.Parameters.Add("@comision", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(commission);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = creditId;
        command.ExecuteNonQuery();
    }

    private static void MarkPaymentVoided(SqlConnection connection, SqlTransaction transaction, long paymentId, string username, string reason)
    {
        using var command = new SqlCommand(
            """
            UPDATE creditos.pago_credito
            SET
                anulado = 1,
                estado_pago = N'ANULADO',
                fecha_anulacion = SYSDATETIME(),
                usuario_anulacion = @usuario,
                motivo_anulacion = @motivo
            WHERE id_pago_credito = @id_pago_credito
              AND ISNULL(anulado, 0) = 0;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;
        command.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = username;
        command.Parameters.Add("@motivo", SqlDbType.NVarChar, 1000).Value = reason;
        command.ExecuteNonQuery();
    }

    private static void MarkCashMovementVoided(SqlConnection connection, SqlTransaction transaction, long movementId, string username, string reason)
    {
        using (var command = new SqlCommand(
            """
            UPDATE caja.movimiento_caja
            SET
                anulado = 1,
                estado_movimiento = N'ANULADO',
                fecha_anulacion = SYSDATETIME(),
                usuario_anulacion = @usuario,
                descripcion = CONCAT(ISNULL(descripcion, N''), N' ANULADO: ', @motivo)
            WHERE id_movimiento_caja = @id_movimiento_caja
              AND ISNULL(anulado, 0) = 0;
            """,
            connection,
            transaction))
        {
            command.Parameters.Add("@id_movimiento_caja", SqlDbType.BigInt).Value = movementId;
            command.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = username;
            command.Parameters.Add("@motivo", SqlDbType.NVarChar, 1000).Value = reason;
            command.ExecuteNonQuery();
        }

        using (var receipt = new SqlCommand(
            """
            UPDATE caja.recibo_oficial_caja
            SET
                anulado = 1,
                fecha_anulacion = SYSDATETIME(),
                observacion = CONCAT(ISNULL(observacion, N''), N' ANULADO: ', @motivo)
            WHERE id_movimiento_caja = @id_movimiento_caja
              AND ISNULL(anulado, 0) = 0;
            """,
            connection,
            transaction))
        {
            receipt.Parameters.Add("@id_movimiento_caja", SqlDbType.BigInt).Value = movementId;
            receipt.Parameters.Add("@motivo", SqlDbType.NVarChar, 1000).Value = reason;
            receipt.ExecuteNonQuery();
        }
    }

    private static void InsertCreditReceipt(SqlConnection connection, SqlTransaction transaction, long paymentId, string voucherNumber, decimal amount, string currency, string? observation)
    {
        using var command = new SqlCommand(
            """
            INSERT INTO creditos.recibo_pago_credito
            (
                id_pago_credito,
                numero_recibo,
                fecha_recibo,
                monto_total,
                moneda,
                observacion
            )
            VALUES
            (
                @id_pago_credito,
                @numero_recibo,
                SYSDATETIME(),
                @monto_total,
                @moneda,
                @observacion
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;
        command.Parameters.Add("@numero_recibo", SqlDbType.NVarChar, 100).Value = voucherNumber;
        command.Parameters.Add("@monto_total", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = currency;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(observation);
        command.ExecuteNonQuery();
    }

    private static void InsertOfficialReceipt(SqlConnection connection, SqlTransaction transaction, long movementId, string officialReceipt, PaymentLoanDto loan, decimal amount, string currency, string? observation, string concept = "Pago de credito")
    {
        using var command = new SqlCommand(
            """
            INSERT INTO caja.recibo_oficial_caja
            (
                id_movimiento_caja,
                numero_recibo_oficial,
                fecha_recibo,
                nombre_cliente,
                cedula_cliente,
                numero_credito,
                concepto,
                monto_total,
                moneda,
                observacion,
                anulado
            )
            VALUES
            (
                @id_movimiento_caja,
                @numero_recibo_oficial,
                SYSDATETIME(),
                @nombre_cliente,
                @cedula_cliente,
                @numero_credito,
                @concepto,
                @monto_total,
                @moneda,
                @observacion,
                0
            );
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_movimiento_caja", SqlDbType.BigInt).Value = movementId;
        command.Parameters.Add("@numero_recibo_oficial", SqlDbType.NVarChar, 100).Value = officialReceipt;
        command.Parameters.Add("@nombre_cliente", SqlDbType.NVarChar, 500).Value = loan.ClientName;
        command.Parameters.Add("@cedula_cliente", SqlDbType.NVarChar, 100).Value = loan.ClientIdentification;
        command.Parameters.Add("@numero_credito", SqlDbType.NVarChar, 100).Value = loan.CreditNumber;
        command.Parameters.Add("@concepto", SqlDbType.NVarChar, 500).Value = concept;
        command.Parameters.Add("@monto_total", SqlDbType.Decimal).Value = amount;
        command.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value = currency;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(observation);
        command.ExecuteNonQuery();
    }

    private static VoucherDto? LoadVoucher(SqlConnection connection, long paymentId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                p.id_pago_credito,
                p.numero_recibo,
                p.fecha_pago,
                p.monto_pagado,
                p.monto_aplicado_moneda_credito,
                p.detalle_tipo_cambio,
                p.moneda_pago,
                p.forma_pago,
                p.estado_pago,
                p.nombre_abonante,
                p.cedula_abonante,
                p.telefono_abonante,
                p.observacion,
                cr.numero_credito,
                cr.moneda AS moneda_credito,
                cr.cedula_id_cliente,
                cr.nom_cliente,
                cr.saldo_capital,
                ro.numero_recibo_oficial,
                ro.fecha_recibo AS fecha_recibo_oficial
            FROM creditos.pago_credito p
            INNER JOIN creditos.credito cr
                ON cr.id_credito = p.id_credito
            LEFT JOIN caja.movimiento_caja mc
                ON mc.id_pago_credito = p.id_pago_credito
               AND ISNULL(mc.anulado, 0) = 0
            LEFT JOIN caja.recibo_oficial_caja ro
                ON ro.id_movimiento_caja = mc.id_movimiento_caja
               AND ISNULL(ro.anulado, 0) = 0
            WHERE p.id_pago_credito = @id_pago_credito;

            SELECT rubro, SUM(monto_aplicado) AS monto
            FROM creditos.aplicacion_pago_credito
            WHERE id_pago_credito = @id_pago_credito
            GROUP BY rubro
            ORDER BY MIN(orden_aplicacion);
            """,
            connection);
        command.Parameters.Add("@id_pago_credito", SqlDbType.BigInt).Value = paymentId;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var voucher = new VoucherDto
        {
            PaymentId = ReadInt64(reader, "id_pago_credito"),
            VoucherNumber = ReadString(reader, "numero_recibo"),
            OfficialReceiptNumber = ReadString(reader, "numero_recibo_oficial"),
            PaymentDate = ReadDateTime(reader, "fecha_pago"),
            OfficialReceiptDate = ReadDateTimeNullable(reader, "fecha_recibo_oficial"),
            Amount = ReadDecimal(reader, "monto_pagado"),
            AppliedAmount = ReadDecimal(reader, "monto_aplicado_moneda_credito"),
            ExchangeDetail = ReadString(reader, "detalle_tipo_cambio"),
            Currency = ReadString(reader, "moneda_pago", "NIO"),
            Method = ReadString(reader, "forma_pago"),
            Status = ReadString(reader, "estado_pago"),
            PayerName = ReadString(reader, "nombre_abonante"),
            PayerIdentification = ReadString(reader, "cedula_abonante"),
            PayerPhone = ReadString(reader, "telefono_abonante"),
            Observation = ReadString(reader, "observacion"),
            CreditNumber = ReadString(reader, "numero_credito"),
            CreditCurrency = ReadString(reader, "moneda_credito", ReadString(reader, "moneda_pago", "NIO")),
            ClientIdentification = ReadString(reader, "cedula_id_cliente"),
            ClientName = ReadString(reader, "nom_cliente"),
            PrincipalBalance = ReadDecimal(reader, "saldo_capital"),
        };

        reader.NextResult();
        while (reader.Read())
        {
            voucher.Applications.Add(new VoucherApplicationDto
            {
                Rubric = ReadString(reader, "rubro"),
                Amount = ReadDecimal(reader, "monto"),
            });
        }

        return voucher;
    }

    private static MovementVoucherDto? LoadMovementVoucher(SqlConnection connection, long movementId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                mc.id_movimiento_caja,
                mc.fecha_movimiento,
                mc.tipo_movimiento,
                mc.origen_movimiento,
                mc.monto_movimiento,
                mc.moneda,
                ISNULL(mc.forma_pago, N'') AS forma_pago,
                ISNULL(mc.tipo_cambio_aplicado, 0) AS tipo_cambio_aplicado,
                mc.estado_movimiento,
                mc.descripcion,
                cr.numero_credito,
                cr.moneda AS moneda_credito,
                cr.cedula_id_cliente,
                cr.nom_cliente,
                cr.saldo_capital,
                ro.numero_recibo_oficial,
                ro.fecha_recibo,
                ro.concepto,
                ro.observacion
            FROM caja.movimiento_caja mc
            LEFT JOIN creditos.credito cr
                ON cr.id_credito = mc.id_credito
            LEFT JOIN caja.recibo_oficial_caja ro
                ON ro.id_movimiento_caja = mc.id_movimiento_caja
               AND ISNULL(ro.anulado, 0) = 0
            WHERE mc.id_movimiento_caja = @id_movimiento_caja;
            """,
            connection);
        command.Parameters.Add("@id_movimiento_caja", SqlDbType.BigInt).Value = movementId;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new MovementVoucherDto
        {
            MovementId = ReadInt64(reader, "id_movimiento_caja"),
            ReceiptNumber = ReadString(reader, "numero_recibo_oficial"),
            MovementDate = ReadDateTime(reader, "fecha_movimiento"),
            ReceiptDate = ReadDateTimeNullable(reader, "fecha_recibo"),
            MovementType = ReadString(reader, "tipo_movimiento"),
            Origin = ReadString(reader, "origen_movimiento"),
            Concept = ReadString(reader, "concepto", ReadString(reader, "origen_movimiento")),
            Amount = ReadDecimal(reader, "monto_movimiento"),
            Currency = ReadString(reader, "moneda", "NIO"),
            Method = ReadString(reader, "forma_pago", "EFECTIVO"),
            ExchangeRate = ReadDecimal(reader, "tipo_cambio_aplicado"),
            Status = ReadString(reader, "estado_movimiento"),
            Description = ReadString(reader, "descripcion"),
            Observation = ReadString(reader, "observacion"),
            CreditNumber = ReadString(reader, "numero_credito"),
            CreditCurrency = ReadString(reader, "moneda_credito", ReadString(reader, "moneda", "NIO")),
            ClientIdentification = ReadString(reader, "cedula_id_cliente"),
            ClientName = ReadString(reader, "nom_cliente"),
            PrincipalBalance = ReadDecimal(reader, "saldo_capital"),
        };
    }

    private static string BuildMovementVoucherHtml(MovementVoucherDto voucher, bool reprint)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine($"<title>Voucher de caja - {Html(voucher.ReceiptNumber)}</title>");
        builder.AppendLine("""
        <style>
          @page{size:80mm auto;margin:2mm}
          *{box-sizing:border-box}
          body{margin:0;background:#f2f2f2;color:#000;font-family:"Courier New",Consolas,monospace;font-size:11px;line-height:1.22}
          .actions{position:fixed;top:10px;right:10px;font-family:Arial,sans-serif;font-size:14px;padding:8px 12px}
          .voucher{width:76mm;margin:10px auto;background:#fff;padding:4mm 3mm;border:1px solid #ddd}
          .center{text-align:center}.right{text-align:right}.bold{font-weight:700}.small{font-size:10px}.big{font-size:14px}
          .rule{border-top:1px dashed #000;margin:5px 0}.double{border-top:2px solid #000;margin:5px 0}
          .row{display:flex;justify-content:space-between;gap:6px;align-items:flex-start}
          .row span:first-child{max-width:44mm}.row span:last-child{text-align:right;max-width:30mm;overflow-wrap:anywhere}
          .reprint{border:1px solid #000;text-align:center;font-weight:700;padding:2px;margin:4px 0;letter-spacing:1px}
          .copy-label{text-align:center;font-size:10px;font-weight:700;margin-bottom:3px}.cut-line{border-top:1px dashed #000;margin:7px 0;text-align:center;font-size:9px;padding-top:2px}
          .sign{margin-top:14px;text-align:center}.sign::before{content:"";display:block;border-top:1px solid #000;margin:0 9mm 4px}
          @media print{html,body{width:80mm;background:#fff}.actions{display:none}.voucher{width:76mm;margin:0 auto;border:0;padding:2mm 2mm}}
        </style></head><body>
        <button class="actions" onclick="window.print()">Imprimir</button>
        <section class="voucher">
        """);
        for (var copy = 1; copy <= 2; copy++)
        {
            builder.AppendLine($"<div class=\"copy-label\">{(copy == 1 ? "COPIA CAJA" : "COPIA CLIENTE")}</div>");
            if (reprint)
            {
                builder.AppendLine("<div class=\"reprint\">*** REIMPRESION ***</div>");
            }

            builder.AppendLine("<div class=\"center bold big\">SIFNIC</div>");
            builder.AppendLine("<div class=\"center bold\">VOUCHER DE CAJA</div>");
            builder.AppendLine($"<div class=\"center small\">{Html(voucher.Concept.ToUpperInvariant())}</div>");
            builder.AppendLine("<div class=\"double\"></div>");
            builder.AppendLine(ReceiptRow("Voucher", string.IsNullOrWhiteSpace(voucher.ReceiptNumber) ? $"MOV-{voucher.MovementId:000000}" : voucher.ReceiptNumber));
            builder.AppendLine(ReceiptRow("Fecha", voucher.MovementDate.ToString("dd/MM/yyyy HH:mm")));
            builder.AppendLine(ReceiptRow("Estado", voucher.Status));
            builder.AppendLine(ReceiptRow("Operacion", $"{voucher.MovementType} / {voucher.Origin}"));
            builder.AppendLine("<div class=\"rule\"></div>");
            builder.AppendLine(ReceiptRow("Prestamo", voucher.CreditNumber));
            builder.AppendLine(ReceiptRow("Cliente", voucher.ClientName));
            builder.AppendLine(ReceiptRow("Cedula", voucher.ClientIdentification));
            builder.AppendLine("<div class=\"rule\"></div>");
            builder.AppendLine(ReceiptRow("Forma", voucher.Method));
            builder.AppendLine(ReceiptRow("Monto", $"{voucher.Currency} {voucher.Amount:N2}"));
            if (voucher.ExchangeRate > 0)
            {
                builder.AppendLine(ReceiptRow("TC institucional", voucher.ExchangeRate.ToString("N4")));
            }
            builder.AppendLine(ReceiptRow("Saldo capital", $"{voucher.CreditCurrency} {voucher.PrincipalBalance:N2}"));
            if (!string.IsNullOrWhiteSpace(voucher.Observation) || !string.IsNullOrWhiteSpace(voucher.Description))
            {
                builder.AppendLine("<div class=\"rule\"></div>");
                builder.AppendLine("<div class=\"bold\">OBSERVACION</div>");
                builder.AppendLine($"<div>{Html(string.IsNullOrWhiteSpace(voucher.Observation) ? voucher.Description : voucher.Observation)}</div>");
            }
            builder.AppendLine("<div class=\"rule\"></div>");
            builder.AppendLine("<div class=\"sign\">CAJERO</div>");
            builder.AppendLine("<div class=\"sign\">CLIENTE</div>");
            builder.AppendLine("<div class=\"center small\">Conserve este voucher para cualquier reclamo.</div>");
            if (copy == 1)
            {
                builder.AppendLine("<div class=\"cut-line\">CORTE / SEGUNDA COPIA</div>");
            }
        }

        builder.AppendLine("</section></body></html>");
        return builder.ToString();
    }

    private static string BuildVoucherHtml(VoucherDto voucher, bool reprint)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine($"<title>Voucher de pago - {Html(voucher.VoucherNumber)}</title>");
        builder.AppendLine("""
        <style>
          @page{size:80mm auto;margin:2mm}
          *{box-sizing:border-box}
          body{margin:0;background:#f2f2f2;color:#000;font-family:"Courier New",Consolas,monospace;font-size:11px;line-height:1.22}
          .actions{position:fixed;top:10px;right:10px;font-family:Arial,sans-serif;font-size:14px;padding:8px 12px}
          .voucher{width:76mm;margin:10px auto;background:#fff;padding:4mm 3mm;border:1px solid #ddd}
          .center{text-align:center}.right{text-align:right}.bold{font-weight:700}.small{font-size:10px}.big{font-size:14px}
          .rule{border-top:1px dashed #000;margin:5px 0}.double{border-top:2px solid #000;margin:5px 0}
          .row{display:flex;justify-content:space-between;gap:6px;align-items:flex-start}
          .row span:first-child{max-width:44mm}.row span:last-child{text-align:right;max-width:30mm;overflow-wrap:anywhere}
          .block{margin:4px 0}.reprint{border:1px solid #000;text-align:center;font-weight:700;padding:2px;margin:4px 0;letter-spacing:1px}
          table{width:100%;border-collapse:collapse;margin-top:4px}
          th,td{padding:2px 0;border:0;font-size:11px;vertical-align:top}
          th{border-bottom:1px dashed #000;text-align:left}
          td:last-child,th:last-child{text-align:right}
          .copy-label{text-align:center;font-size:10px;font-weight:700;margin-bottom:3px}
          .cut-line{border-top:1px dashed #000;margin:7px 0;text-align:center;font-size:9px;padding-top:2px}
          .sign{margin-top:14px;text-align:center}.sign::before{content:"";display:block;border-top:1px solid #000;margin:0 9mm 4px}
          @media print{
            html,body{width:80mm;background:#fff}
            body{margin:0}
            .actions{display:none}
            .voucher{width:76mm;margin:0 auto;border:0;padding:2mm 2mm}
          }
        </style></head><body>
        <button class="actions" onclick="window.print()">Imprimir</button>
        <section class="voucher">
        """);
        for (var copy = 1; copy <= 2; copy++)
        {
            builder.AppendLine($"<div class=\"copy-label\">{(copy == 1 ? "COPIA CAJA" : "COPIA CLIENTE")}</div>");
        if (reprint)
        {
            builder.AppendLine("<div class=\"reprint\">*** REIMPRESION ***</div>");
        }

        builder.AppendLine("<div class=\"center bold big\">SIFNIC</div>");
        builder.AppendLine("<div class=\"center bold\">VOUCHER DE PAGO</div>");
        builder.AppendLine("<div class=\"center small\">RECIBO DE CREDITO</div>");
        builder.AppendLine("<div class=\"double\"></div>");
        builder.AppendLine(ReceiptRow("Voucher", voucher.VoucherNumber));
        builder.AppendLine(ReceiptRow("Recibo caja", string.IsNullOrWhiteSpace(voucher.OfficialReceiptNumber) ? "-" : voucher.OfficialReceiptNumber));
        builder.AppendLine(ReceiptRow("Fecha", voucher.PaymentDate.ToString("dd/MM/yyyy HH:mm")));
        builder.AppendLine(ReceiptRow("Estado", voucher.Status));
        builder.AppendLine("<div class=\"rule\"></div>");
        builder.AppendLine(ReceiptRow("Prestamo", voucher.CreditNumber));
        builder.AppendLine(ReceiptRow("Cliente", voucher.ClientName));
        builder.AppendLine(ReceiptRow("Cedula", voucher.ClientIdentification));
        builder.AppendLine(ReceiptRow("Abonante", voucher.PayerName));
        builder.AppendLine(ReceiptRow("Ced abonante", voucher.PayerIdentification));
        if (!string.IsNullOrWhiteSpace(voucher.PayerPhone))
        {
            builder.AppendLine(ReceiptRow("Telefono", voucher.PayerPhone));
        }
        var totalApplied = voucher.Applications.Sum(row => row.Amount);
        var displayedAppliedAmount = voucher.AppliedAmount > 0 ? voucher.AppliedAmount : totalApplied;
        builder.AppendLine("<div class=\"rule\"></div>");
        builder.AppendLine(ReceiptRow("Forma pago", voucher.Method));
        builder.AppendLine(ReceiptRow("Monto recibido", $"{voucher.Currency} {voucher.Amount:N2}"));
        builder.AppendLine(ReceiptRow("Aplicado credito", $"{voucher.CreditCurrency} {displayedAppliedAmount:N2}"));
        if (!string.IsNullOrWhiteSpace(voucher.ExchangeDetail))
        {
            builder.AppendLine(ReceiptRow("Tipo cambio", voucher.ExchangeDetail));
        }
        builder.AppendLine(ReceiptRow("Saldo capital", $"{voucher.CreditCurrency} {voucher.PrincipalBalance:N2}"));
        builder.AppendLine("<div class=\"rule\"></div>");
        builder.AppendLine("<div class=\"bold\">DESGLOSE DEL PAGO</div>");
        builder.AppendLine("<table><thead><tr><th>Rubro</th><th>Monto</th></tr></thead><tbody>");
        foreach (var row in VoucherApplicationRows(voucher))
        {
            builder.AppendLine($"<tr><td>{Html(row.Label)}</td><td class=\"right\">{voucher.CreditCurrency} {row.Amount:N2}</td></tr>");
        }
        builder.AppendLine($"<tr><th>Total</th><th class=\"right\">{voucher.CreditCurrency} {totalApplied:N2}</th></tr>");
        builder.AppendLine("</tbody></table>");
        if (!string.IsNullOrWhiteSpace(voucher.Observation))
        {
            builder.AppendLine("<div class=\"rule\"></div>");
            builder.AppendLine("<div class=\"bold\">OBSERVACION</div>");
            builder.AppendLine($"<div>{Html(voucher.Observation)}</div>");
        }
        builder.AppendLine("<div class=\"rule\"></div>");
        builder.AppendLine("<div class=\"sign\">CAJERO</div>");
        builder.AppendLine("<div class=\"sign\">CLIENTE / ABONANTE</div>");
        builder.AppendLine("<div class=\"center small block\">Conserve este voucher para cualquier reclamo.</div>");
            if (copy == 1)
            {
                builder.AppendLine("<div class=\"cut-line\">CORTE / SEGUNDA COPIA</div>");
            }
        }
        builder.AppendLine("</section></body></html>");
        return builder.ToString();
    }

    private static string BuildCashCountHtml(CashSessionDto session, CashReportDto report)
    {
        var closingNio = session.Status == "CERRADA" ? session.PhysicalNio : 0;
        var closingUsd = session.Status == "CERRADA" ? session.PhysicalUsd : 0;
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine($"<title>Hoja de arqueo - Caja {session.Id}</title>");
        builder.AppendLine("""
        <style>
          @page{size:letter;margin:14mm}
          body{font-family:Arial,sans-serif;color:#142b34;background:#fff;margin:0;font-size:12px}
          .actions{position:fixed;top:12px;right:16px}.sheet{max-width:980px;margin:auto}
          h1{margin:0;font-size:24px}.kicker{color:#006b8d;text-transform:uppercase;font-weight:700;font-size:12px}
          .head{display:flex;justify-content:space-between;gap:18px;border-bottom:3px solid #163846;padding-bottom:10px;margin-bottom:12px}
          .grid{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin:10px 0}.card{border:1px solid #d5e3e9;border-radius:7px;padding:8px;background:#fbfdfe}
          .card span{display:block;color:#5f747c;font-size:10px;text-transform:uppercase;font-weight:700}.card strong{font-size:14px}
          table{width:100%;border-collapse:collapse;margin:10px 0 16px}th{background:#e9f5f8;color:#006b8d;text-transform:uppercase;font-size:10px}
          th,td{border:1px solid #d8e4ea;padding:6px;text-align:left}td.num,th.num{text-align:right}.section-title{margin:18px 0 6px;color:#163846;font-size:15px}
          .signatures{display:grid;grid-template-columns:1fr 1fr 1fr;gap:28px;margin-top:42px}.line{border-top:1px solid #142b34;text-align:center;padding-top:8px;color:#5f747c}
          .danger{color:#a5372d;font-weight:700}.ok{color:#0b6d42;font-weight:700}
          @media print{.actions{display:none}.sheet{max-width:none}}
        </style></head><body>
        <button class="actions" onclick="window.print()">Imprimir</button>
        <main class="sheet">
        """);
        builder.AppendLine("<header class=\"head\"><div><div class=\"kicker\">SIFNIC - Caja</div><h1>Hoja de arqueo</h1></div>");
        builder.AppendLine($"<div><strong>Sesion #{session.Id}</strong><br>{Html(session.Status)}</div></header>");
        builder.AppendLine("<section class=\"grid\">");
        builder.AppendLine(Card("Fecha operacion", session.OperationDate.ToString("dd/MM/yyyy")));
        builder.AppendLine(Card("Sucursal", session.Branch));
        builder.AppendLine(Card("Cajero", session.CashierUser));
        builder.AppendLine(Card("Apertura", session.OpenedAt.ToString("dd/MM/yyyy HH:mm")));
        builder.AppendLine(Card("Cierre", session.ClosedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Caja abierta"));
        builder.AppendLine(Card("Apertura NIO", session.OpeningNio.ToString("N2")));
        builder.AppendLine(Card("Apertura USD", session.OpeningUsd.ToString("N2")));
        builder.AppendLine(Card("Movimientos", report.Movements.Count.ToString()));
        builder.AppendLine("</section>");

        builder.AppendLine("<h2 class=\"section-title\">Resumen de saldos</h2>");
        builder.AppendLine("<table><thead><tr><th>Moneda</th><th class=\"num\">Apertura</th><th class=\"num\">Ingresos</th><th class=\"num\">Egresos</th><th class=\"num\">Teorico</th><th class=\"num\">Fisico</th><th class=\"num\">Diferencia</th></tr></thead><tbody>");
        builder.AppendLine(CashBalanceRow("NIO", session.OpeningNio, session.IncomeNio, session.ExpenseNio, session.TheoreticalNio, closingNio, session.DifferenceNio));
        builder.AppendLine(CashBalanceRow("USD", session.OpeningUsd, session.IncomeUsd, session.ExpenseUsd, session.TheoreticalUsd, closingUsd, session.DifferenceUsd));
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h2 class=\"section-title\">Ingresos por forma de pago</h2>");
        builder.AppendLine("<table><thead><tr><th>Moneda</th><th>Forma</th><th class=\"num\">Cantidad</th><th class=\"num\">Total</th></tr></thead><tbody>");
        foreach (var row in report.ByMethod)
        {
            builder.AppendLine($"<tr><td>{Html(row.Currency)}</td><td>{Html(row.Method)}</td><td class=\"num\">{row.Count}</td><td class=\"num\">{row.Total:N2}</td></tr>");
        }
        if (report.ByMethod.Count == 0)
        {
            builder.AppendLine("<tr><td colspan=\"4\">Sin ingresos.</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        AppendBreakdownTable(builder, "Desglose de apertura", report.Breakdown.Where(row => row.Type == "APERTURA"));
        AppendBreakdownTable(builder, "Desglose de cierre", report.Breakdown.Where(row => row.Type == "CIERRE"));

        builder.AppendLine("<h2 class=\"section-title\">Movimientos</h2>");
        builder.AppendLine("<table><thead><tr><th>Fecha</th><th>Voucher</th><th>Cliente</th><th>Credito</th><th>Forma</th><th class=\"num\">Monto</th></tr></thead><tbody>");
        foreach (var row in report.Movements)
        {
            builder.AppendLine($"<tr><td>{row.Date:dd/MM/yyyy HH:mm}</td><td>{Html(row.VoucherNumber)}</td><td>{Html(row.ClientName)}</td><td>{Html(row.CreditNumber)}</td><td>{Html(row.Method)}</td><td class=\"num\">{Html(row.Currency)} {row.Amount:N2}</td></tr>");
        }
        if (report.Movements.Count == 0)
        {
            builder.AppendLine("<tr><td colspan=\"6\">Sin movimientos.</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        if (!string.IsNullOrWhiteSpace(session.OpeningNote) || !string.IsNullOrWhiteSpace(session.ClosingNote))
        {
            builder.AppendLine("<h2 class=\"section-title\">Observaciones</h2>");
            builder.AppendLine($"<p><strong>Apertura:</strong> {Html(session.OpeningNote)}</p>");
            builder.AppendLine($"<p><strong>Cierre:</strong> {Html(session.ClosingNote)}</p>");
        }

        builder.AppendLine("<section class=\"signatures\"><div class=\"line\">Cajero</div><div class=\"line\">Supervisor</div><div class=\"line\">Contabilidad</div></section>");
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static string CashBalanceRow(string currency, decimal opening, decimal income, decimal expense, decimal theoretical, decimal physical, decimal difference)
    {
        var css = difference == 0 ? "ok" : "danger";
        return $"<tr><td>{Html(currency)}</td><td class=\"num\">{opening:N2}</td><td class=\"num\">{income:N2}</td><td class=\"num\">{expense:N2}</td><td class=\"num\">{theoretical:N2}</td><td class=\"num\">{physical:N2}</td><td class=\"num {css}\">{difference:N2}</td></tr>";
    }

    private static void AppendBreakdownTable(StringBuilder builder, string title, IEnumerable<CashReportBreakdownDto> rows)
    {
        var items = rows.ToList();
        builder.AppendLine($"<h2 class=\"section-title\">{Html(title)}</h2>");
        builder.AppendLine("<table><thead><tr><th>Moneda</th><th class=\"num\">Denominacion</th><th class=\"num\">Cantidad</th><th class=\"num\">Total</th></tr></thead><tbody>");
        foreach (var row in items)
        {
            builder.AppendLine($"<tr><td>{Html(row.Currency)}</td><td class=\"num\">{row.Denomination:N2}</td><td class=\"num\">{row.Quantity}</td><td class=\"num\">{row.Total:N2}</td></tr>");
        }
        if (items.Count == 0)
        {
            builder.AppendLine("<tr><td colspan=\"4\">Sin desglose registrado.</td></tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

    private static string ReceiptRow(string label, string value) =>
        $"<div class=\"row\"><span>{Html(label)}</span><span>{Html(value)}</span></div>";

    private static string Card(string label, string value) => $"<article class=\"card\"><span>{Html(label)}</span><strong>{Html(value)}</strong></article>";
    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static IReadOnlyList<(string Label, decimal Amount)> VoucherApplicationRows(VoucherDto voucher)
    {
        decimal Sum(params string[] rubrics) => voucher.Applications
            .Where(row => rubrics.Contains(row.Rubric, StringComparer.OrdinalIgnoreCase))
            .Sum(row => row.Amount);

        return
        [
            ("Abono capital", Sum("CAPITAL", "CAPITAL_ANTICIPADO")),
            ("Abono intereses", Sum("INTERES")),
            ("Abono mora", Sum("MORA")),
            ("Comision / otros", Sum("COMISION", "DESLIZAMIENTO", "OTROS")),
        ];
    }

    private static IReadOnlyDictionary<string, string> ValidatePayment(PaymentApplyModel model)
    {
        var errors = new Dictionary<string, string>();
        if (model.CreditId <= 0) errors["creditId"] = "Selecciona un prestamo.";
        if (model.Amount <= 0) errors["amount"] = "El monto debe ser mayor que cero.";
        var method = NormalizePaymentMethod(model.Method);
        if (!string.Equals(method, "EFECTIVO", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(model.ManualReceipt))
        {
            errors["manualReceipt"] = "La referencia es obligatoria para pagos que no son en efectivo.";
        }
        return errors;
    }

    private static string NormalizeCurrency(string? value, string fallback)
    {
        var currency = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
        return currency is "USD" or "DOLAR" ? "USD" : "NIO";
    }

    private static string NormalizeIdentification(string? value)
    {
        return string.Concat((value ?? string.Empty).Trim().ToUpperInvariant().Where(char.IsLetterOrDigit));
    }

    private static string NormalizePaymentMethod(string? value)
    {
        var method = string.IsNullOrWhiteSpace(value) ? "EFECTIVO" : value.Trim().ToUpperInvariant();
        return method is "EFECTIVO" or "TRANSFERENCIA" or "CHEQUE" or "POS" ? method : "EFECTIVO";
    }

    private static bool AffectsCash(string? method) =>
        string.Equals(NormalizePaymentMethod(method), "EFECTIVO", StringComparison.OrdinalIgnoreCase);

    private static bool CanReverseClosedCash(CreditPortfolioSession session) =>
        session.HasAnyRole("ADMINISTRADOR", "ADMINISTRACION", "JEFE_CREDITO", "GERENTE_CREDITO", "CAJA_SUPERVISOR");

    private static string ReadString(SqlDataReader reader, string name, string fallback = "")
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? fallback : Convert.ToString(reader.GetValue(ordinal))?.Trim() ?? fallback;
    }

    private static long ReadInt64(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static long? ReadInt64Nullable(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static int ReadInt32(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadInt32Nullable(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal ReadDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool ReadBool(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static DateTime ReadDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
    }

    private static DateTime? ReadDateTimeNullable(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public sealed class PaymentApplyModel
    {
        public long CreditId { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public string? Method { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? PayerName { get; set; }
        public string? PayerIdentification { get; set; }
        public string? PayerPhone { get; set; }
        public string? ManualReceipt { get; set; }
        public string? Observation { get; set; }
    }

    public sealed class PaymentVoidModel
    {
        public long PaymentId { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class CreditDisbursementModel
    {
        public long CreditId { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public string? Method { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? Observation { get; set; }
    }

    public sealed class CashOpenModel
    {
        public string? Branch { get; set; }
        public decimal OpeningNio { get; set; }
        public decimal OpeningUsd { get; set; }
        public string? Observation { get; set; }
        public List<CashBreakdownLineModel> Breakdown { get; set; } = [];
    }

    public sealed class CashCloseModel
    {
        public decimal PhysicalNio { get; set; }
        public decimal PhysicalUsd { get; set; }
        public string? Observation { get; set; }
        public List<CashBreakdownLineModel> Breakdown { get; set; } = [];
    }

    public sealed class CashBreakdownLineModel
    {
        public string? Currency { get; set; }
        public decimal Denomination { get; set; }
        public int Quantity { get; set; }
    }

    private sealed class CashSessionDto
    {
        public long Id { get; init; }
        public DateTime OperationDate { get; init; }
        public string Branch { get; init; } = string.Empty;
        public string CashierUser { get; init; } = string.Empty;
        public decimal OpeningNio { get; init; }
        public decimal OpeningUsd { get; init; }
        public DateTime OpenedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public string Status { get; init; } = string.Empty;
        public string OpeningNote { get; init; } = string.Empty;
        public string ClosingNote { get; init; } = string.Empty;
        public decimal IncomeNio { get; init; }
        public decimal IncomeUsd { get; init; }
        public decimal ExpenseNio { get; init; }
        public decimal ExpenseUsd { get; init; }
        public decimal TheoreticalNio { get; init; }
        public decimal TheoreticalUsd { get; init; }
        public decimal PhysicalNio { get; init; }
        public decimal PhysicalUsd { get; init; }
        public decimal DifferenceNio { get; init; }
        public decimal DifferenceUsd { get; init; }
    }

    private sealed class BranchContextDto
    {
        public List<BranchOptionDto> Branches { get; init; } = [];
        public BranchOptionDto? AssignedBranch { get; init; }
        public bool Locked { get; init; }
    }

    private sealed class BranchOptionDto
    {
        public long Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    private sealed class InstitutionalExchangeRateDto
    {
        public DateTime Date { get; init; }
        public decimal Buy { get; init; }
        public decimal Sell { get; init; }
        public decimal Reference { get; init; }
    }

    private sealed class CashReportDto
    {
        public List<CashReportMethodDto> ByMethod { get; } = [];
        public List<CashReportBreakdownDto> Breakdown { get; } = [];
        public List<CashReportMovementDto> Movements { get; } = [];
    }

    private sealed class CashReportMethodDto
    {
        public string Currency { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public int Count { get; init; }
        public decimal Total { get; init; }
    }

    private sealed class CashReportBreakdownDto
    {
        public string Currency { get; init; } = string.Empty;
        public decimal Denomination { get; init; }
        public int Quantity { get; init; }
        public decimal Total { get; init; }
        public string Type { get; init; } = string.Empty;
    }

    private sealed class CashReportMovementDto
    {
        public DateTime Date { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Origin { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string CreditNumber { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string VoucherNumber { get; init; } = string.Empty;
    }

    private sealed class PaymentLoanDto
    {
        public long CreditId { get; init; }
        public string CreditNumber { get; init; } = string.Empty;
        public long? ClientId { get; init; }
        public string ClientIdentification { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public string Currency { get; init; } = "NIO";
        public string CreditCurrency { get; init; } = "NIO";
        public decimal PrincipalBalance { get; init; }
        public decimal ApprovedAmount { get; init; }
    }

    private sealed class PaymentPlanPendingDto
    {
        public long PlanId { get; init; }
        public decimal PendingCapital { get; init; }
        public decimal PendingInterest { get; init; }
        public decimal PendingCommission { get; init; }
        public decimal PendingMora { get; init; }
    }

    private sealed class PaymentAllocationDto
    {
        public decimal Capital { get; set; }
        public decimal Interest { get; set; }
        public decimal Commission { get; set; }
        public decimal Mora { get; set; }
        public decimal TotalApplied { get; set; }
    }

    private sealed class PaymentVoidDto
    {
        public long PaymentId { get; init; }
        public long CreditId { get; init; }
        public string VoucherNumber { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "NIO";
        public string CreditCurrency { get; init; } = "NIO";
        public string Method { get; init; } = "EFECTIVO";
        public bool IsVoided { get; init; }
        public string CreditNumber { get; init; } = string.Empty;
        public long MovementId { get; init; }
        public long CashSessionId { get; init; }
        public decimal CapitalApplied { get; init; }
        public decimal InterestApplied { get; init; }
        public decimal CommissionApplied { get; init; }
        public decimal MoraApplied { get; init; }
    }

    private sealed class VoucherDto
    {
        public long PaymentId { get; init; }
        public string VoucherNumber { get; init; } = string.Empty;
        public string OfficialReceiptNumber { get; init; } = string.Empty;
        public DateTime PaymentDate { get; init; }
        public DateTime? OfficialReceiptDate { get; init; }
        public decimal Amount { get; init; }
        public decimal AppliedAmount { get; init; }
        public string Currency { get; init; } = "NIO";
        public string CreditCurrency { get; init; } = "NIO";
        public string ExchangeDetail { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string PayerName { get; init; } = string.Empty;
        public string PayerIdentification { get; init; } = string.Empty;
        public string PayerPhone { get; init; } = string.Empty;
        public string Observation { get; init; } = string.Empty;
        public string CreditNumber { get; init; } = string.Empty;
        public string ClientIdentification { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public decimal PrincipalBalance { get; init; }
        public List<VoucherApplicationDto> Applications { get; } = [];
    }

    private sealed class MovementVoucherDto
    {
        public long MovementId { get; init; }
        public string ReceiptNumber { get; init; } = string.Empty;
        public DateTime MovementDate { get; init; }
        public DateTime? ReceiptDate { get; init; }
        public string MovementType { get; init; } = string.Empty;
        public string Origin { get; init; } = string.Empty;
        public string Concept { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "NIO";
        public string CreditCurrency { get; init; } = "NIO";
        public string Method { get; init; } = string.Empty;
        public decimal ExchangeRate { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Observation { get; init; } = string.Empty;
        public string CreditNumber { get; init; } = string.Empty;
        public string ClientIdentification { get; init; } = string.Empty;
        public string ClientName { get; init; } = string.Empty;
        public decimal PrincipalBalance { get; init; }
    }

    private sealed class VoucherApplicationDto
    {
        public string Rubric { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }
}
