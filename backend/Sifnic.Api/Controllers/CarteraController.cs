using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Creditos;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class CarteraController : Controller
{
    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeAssignedPortfolio)
            {
                return Forbid();
            }

            var statuses = LoadStatuses(connection);
            var officers = session.CanSeeFullPortfolio ? LoadOfficers(connection) : [];

            return Json(new
            {
                ok = true,
                data = new
                {
                    canSeeFullPortfolio = session.CanSeeFullPortfolio,
                    currentUserId = session.UserId,
                    currentUser = session.Username,
                    roles = session.Roles,
                    statuses,
                    officers,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar la configuracion de cartera.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Resumen(string? search, string? status, long? officerId, DateTime? cutoffDate)
    {
        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeAssignedPortfolio)
            {
                return Forbid();
            }

            var normalizedOfficerId = session.CanSeeFullPortfolio ? Math.Max(0, officerId ?? 0) : 0;
            var cutoff = cutoffDate?.Date ?? DateTime.Today;

            using var command = new SqlCommand(
                """
                SELECT
                    COUNT(1) AS total_creditos,
                    ISNULL(SUM(ISNULL(cr.monto_aprobado, 0)), 0) AS monto_colocado,
                    ISNULL(SUM(ISNULL(cr.saldo_capital, 0)), 0) AS saldo_capital,
                    ISNULL(SUM(ISNULL(cr.interes_acumulado, 0) + ISNULL(cr.mora_acumulada, 0) + ISNULL(cr.cargos_acumulados, 0) + ISNULL(cr.comision_acumulada, 0)), 0) AS saldo_accesorios,
                    ISNULL(SUM(ISNULL(planInfo.saldo_vencido, 0)), 0) AS saldo_vencido,
                    ISNULL(SUM(ISNULL(planInfo.cuotas_vencidas, 0)), 0) AS cuotas_vencidas,
                    SUM(CASE WHEN ISNULL(planInfo.saldo_vencido, 0) > 0 THEN 1 ELSE 0 END) AS creditos_en_mora,
                    SUM(CASE WHEN oficial.id_usuario_oficial IS NULL THEN 1 ELSE 0 END) AS creditos_sin_oficial
                FROM creditos.credito cr
                LEFT JOIN clientes.cliente c
                    ON c.id_cliente = cr.id_cliente
                LEFT JOIN creditos.solicitud_credito s
                    ON s.id_solicitud_credito = cr.id_solicitud_credito
                OUTER APPLY
                (
                    SELECT TOP (1)
                        ao.id_usuario_oficial,
                        u.usuario AS usuario_oficial,
                        CONCAT(u.nombres, N' ', u.apellidos) AS nombre_oficial
                    FROM creditos.asignacion_oficial_credito ao
                    LEFT JOIN seguridad.usuario u
                        ON u.id_usuario = ao.id_usuario_oficial
                    WHERE ao.id_credito = cr.id_credito
                      AND ao.activo = 1
                      AND ao.fecha_fin IS NULL
                    ORDER BY ao.fecha_asignacion DESC, ao.id_asignacion_oficial_credito DESC
                ) oficial
                OUTER APPLY
                (
                    SELECT
                        SUM(CASE
                            WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN
                                CASE
                                    WHEN (
                                        ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                        ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                        ISNULL(pp.deslizamiento_programado, 0) -
                                        ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                        ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                                    ) > 0 THEN
                                        ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                        ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                        ISNULL(pp.deslizamiento_programado, 0) -
                                        ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                        ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                                    ELSE 0
                                END
                            ELSE 0
                        END) AS saldo_vencido,
                        SUM(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN 1 ELSE 0 END) AS cuotas_vencidas
                    FROM creditos.plan_pago_credito pp
                    WHERE pp.id_credito = cr.id_credito
                ) planInfo
                WHERE cr.activo = 1
                  AND (@puede_ver_todo = 1 OR oficial.id_usuario_oficial = @id_usuario)
                  AND (@estado = N'TODOS' OR cr.estado_operativo = @estado)
                  AND (@id_oficial = 0 OR oficial.id_usuario_oficial = @id_oficial)
                  AND (
                      @buscar = N''
                      OR ISNULL(cr.numero_credito, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(cr.cedula_id_cliente, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(cr.nom_cliente, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(c.cedula, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(c.nombres + N' ' + c.apellidos, N'') LIKE N'%' + @buscar + N'%'
                  );
                """,
                connection);
            AddPortfolioParameters(command, session, search, status, normalizedOfficerId, cutoff);

            using var reader = command.ExecuteReader();
            reader.Read();
            return Json(new
            {
                ok = true,
                data = new
                {
                    totalCredits = ReadInt32(reader, "total_creditos"),
                    placedAmount = ReadDecimal(reader, "monto_colocado"),
                    capitalBalance = ReadDecimal(reader, "saldo_capital"),
                    accessoryBalance = ReadDecimal(reader, "saldo_accesorios"),
                    overdueBalance = ReadDecimal(reader, "saldo_vencido"),
                    overdueInstallments = ReadInt32(reader, "cuotas_vencidas"),
                    overdueCredits = ReadInt32(reader, "creditos_en_mora"),
                    unassignedCredits = ReadInt32(reader, "creditos_sin_oficial"),
                    cutoffDate = cutoff,
                    scope = session.CanSeeFullPortfolio ? "GLOBAL" : "ASIGNADA",
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el resumen de cartera.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Listar(string? search, string? status, long? officerId, DateTime? cutoffDate)
    {
        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeAssignedPortfolio)
            {
                return Forbid();
            }

            var normalizedOfficerId = session.CanSeeFullPortfolio ? Math.Max(0, officerId ?? 0) : 0;
            var cutoff = cutoffDate?.Date ?? DateTime.Today;

            using var command = new SqlCommand(
                """
                SELECT
                    cr.id_credito,
                    cr.numero_credito,
                    cr.id_cliente,
                    cr.cedula_id_cliente,
                    cr.nom_cliente,
                    cr.moneda,
                    cr.monto_aprobado,
                    cr.plazo_meses,
                    cr.tasa_interes_anual,
                    cr.fecha_desembolso,
                    cr.fecha_vencimiento,
                    cr.estado_operativo,
                    cr.saldo_capital,
                    ISNULL(cr.interes_acumulado, 0) + ISNULL(cr.mora_acumulada, 0) + ISNULL(cr.cargos_acumulados, 0) + ISNULL(cr.comision_acumulada, 0) AS saldo_accesorios,
                    s.producto_credito,
                    s.frecuencia_pago,
                    s.destino_credito,
                    s.nivel_riesgo,
                    s.clasificacion_conami,
                    oficial.id_usuario_oficial,
                    oficial.usuario_oficial,
                    oficial.nombre_oficial,
                    ISNULL(planInfo.saldo_vencido, 0) AS saldo_vencido,
                    ISNULL(planInfo.cuotas_vencidas, 0) AS cuotas_vencidas,
                    ISNULL(planInfo.dias_mora, 0) AS dias_mora,
                    planInfo.proxima_fecha,
                    planInfo.proxima_cuota,
                    pago.fecha_pago AS ultimo_pago_fecha,
                    pago.monto_pagado AS ultimo_pago_monto,
                    pago.numero_recibo AS ultimo_pago_recibo
                FROM creditos.credito cr
                LEFT JOIN clientes.cliente c
                    ON c.id_cliente = cr.id_cliente
                LEFT JOIN creditos.solicitud_credito s
                    ON s.id_solicitud_credito = cr.id_solicitud_credito
                OUTER APPLY
                (
                    SELECT TOP (1)
                        ao.id_usuario_oficial,
                        u.usuario AS usuario_oficial,
                        CONCAT(u.nombres, N' ', u.apellidos) AS nombre_oficial
                    FROM creditos.asignacion_oficial_credito ao
                    LEFT JOIN seguridad.usuario u
                        ON u.id_usuario = ao.id_usuario_oficial
                    WHERE ao.id_credito = cr.id_credito
                      AND ao.activo = 1
                      AND ao.fecha_fin IS NULL
                    ORDER BY ao.fecha_asignacion DESC, ao.id_asignacion_oficial_credito DESC
                ) oficial
                OUTER APPLY
                (
                    SELECT
                        SUM(CASE
                            WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN
                                CASE
                                    WHEN (
                                        ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                        ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                        ISNULL(pp.deslizamiento_programado, 0) -
                                        ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                        ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                                    ) > 0 THEN
                                        ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                        ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                        ISNULL(pp.deslizamiento_programado, 0) -
                                        ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                        ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                                    ELSE 0
                                END
                            ELSE 0
                        END) AS saldo_vencido,
                        SUM(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN 1 ELSE 0 END) AS cuotas_vencidas,
                        MAX(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) ELSE 0 END) AS dias_mora,
                        MIN(CASE WHEN pp.pagada = 0 THEN pp.fecha_cuota ELSE NULL END) AS proxima_fecha,
                        MIN(CASE WHEN pp.pagada = 0 THEN pp.numero_cuota ELSE NULL END) AS proxima_cuota
                    FROM creditos.plan_pago_credito pp
                    WHERE pp.id_credito = cr.id_credito
                ) planInfo
                OUTER APPLY
                (
                    SELECT TOP (1)
                        p.fecha_pago,
                        p.monto_pagado,
                        p.numero_recibo
                    FROM creditos.pago_credito p
                    WHERE p.id_credito = cr.id_credito
                      AND ISNULL(p.anulado, 0) = 0
                      AND ISNULL(p.estado_pago, N'') = N'APLICADO'
                    ORDER BY p.fecha_pago DESC, p.id_pago_credito DESC
                ) pago
                WHERE cr.activo = 1
                  AND (@puede_ver_todo = 1 OR oficial.id_usuario_oficial = @id_usuario)
                  AND (@estado = N'TODOS' OR cr.estado_operativo = @estado)
                  AND (@id_oficial = 0 OR oficial.id_usuario_oficial = @id_oficial)
                  AND (
                      @buscar = N''
                      OR ISNULL(cr.numero_credito, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(cr.cedula_id_cliente, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(cr.nom_cliente, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(c.cedula, N'') LIKE N'%' + @buscar + N'%'
                      OR ISNULL(c.nombres + N' ' + c.apellidos, N'') LIKE N'%' + @buscar + N'%'
                  )
                ORDER BY
                    CASE WHEN ISNULL(planInfo.saldo_vencido, 0) > 0 THEN 0 ELSE 1 END,
                    ISNULL(planInfo.dias_mora, 0) DESC,
                    cr.id_credito DESC;
                """,
                connection);
            AddPortfolioParameters(command, session, search, status, normalizedOfficerId, cutoff);

            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(new
                {
                    id = ReadInt64(reader, "id_credito"),
                    number = ReadString(reader, "numero_credito"),
                    clientId = ReadInt64Nullable(reader, "id_cliente"),
                    clientIdentification = ReadString(reader, "cedula_id_cliente"),
                    clientName = ReadString(reader, "nom_cliente"),
                    currency = ReadString(reader, "moneda", "NIO"),
                    approvedAmount = ReadDecimal(reader, "monto_aprobado"),
                    termMonths = ReadInt32(reader, "plazo_meses"),
                    annualRate = ReadDecimal(reader, "tasa_interes_anual"),
                    disbursementDate = ReadDateNullable(reader, "fecha_desembolso"),
                    dueDate = ReadDateNullable(reader, "fecha_vencimiento"),
                    status = ReadString(reader, "estado_operativo"),
                    capitalBalance = ReadDecimal(reader, "saldo_capital"),
                    accessoryBalance = ReadDecimal(reader, "saldo_accesorios"),
                    product = ReadString(reader, "producto_credito", "MICROCREDITO"),
                    frequency = ReadString(reader, "frecuencia_pago", "MENSUAL"),
                    destination = ReadString(reader, "destino_credito"),
                    riskLevel = ReadString(reader, "nivel_riesgo", "MEDIO"),
                    conamiClassification = ReadString(reader, "clasificacion_conami", "A"),
                    officerId = ReadInt64Nullable(reader, "id_usuario_oficial"),
                    officerUser = ReadString(reader, "usuario_oficial"),
                    officerName = ReadString(reader, "nombre_oficial"),
                    overdueBalance = ReadDecimal(reader, "saldo_vencido"),
                    overdueInstallments = ReadInt32(reader, "cuotas_vencidas"),
                    overdueDays = ReadInt32(reader, "dias_mora"),
                    nextDueDate = ReadDateNullable(reader, "proxima_fecha"),
                    nextInstallment = ReadInt32Nullable(reader, "proxima_cuota"),
                    lastPaymentDate = ReadDateNullable(reader, "ultimo_pago_fecha"),
                    lastPaymentAmount = ReadDecimal(reader, "ultimo_pago_monto"),
                    lastPaymentReceipt = ReadString(reader, "ultimo_pago_recibo"),
                });
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar la cartera.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Obtener(long id, DateTime? cutoffDate)
    {
        if (id <= 0)
        {
            return BadRequest(new { ok = false, message = "Prestamo invalido." });
        }

        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeAssignedPortfolio)
            {
                return Forbid();
            }

            var cutoff = cutoffDate?.Date ?? DateTime.Today;
            using var command = new SqlCommand(
                """
                SELECT
                    cr.id_credito,
                    cr.numero_credito,
                    cr.id_cliente,
                    cr.cedula_id_cliente,
                    cr.nom_cliente,
                    cr.moneda,
                    cr.monto_aprobado,
                    cr.plazo_meses,
                    cr.tasa_interes_anual,
                    cr.fecha_desembolso,
                    cr.fecha_vencimiento,
                    cr.estado_operativo,
                    cr.saldo_capital,
                    ISNULL(cr.interes_acumulado, 0) AS interes_acumulado,
                    ISNULL(cr.mora_acumulada, 0) AS mora_acumulada,
                    ISNULL(cr.cargos_acumulados, 0) AS cargos_acumulados,
                    ISNULL(cr.comision_acumulada, 0) AS comision_acumulada,
                    s.producto_credito,
                    s.frecuencia_pago,
                    s.destino_credito,
                    s.nivel_riesgo,
                    s.clasificacion_conami,
                    oficial.id_usuario_oficial,
                    oficial.usuario_oficial,
                    oficial.nombre_oficial,
                    oficial.fecha_asignacion,
                    planInfo.saldo_vencido,
                    planInfo.cuotas_vencidas,
                    planInfo.dias_mora,
                    planInfo.proxima_fecha,
                    planInfo.proxima_cuota
                FROM creditos.credito cr
                LEFT JOIN creditos.solicitud_credito s
                    ON s.id_solicitud_credito = cr.id_solicitud_credito
                OUTER APPLY
                (
                    SELECT TOP (1)
                        ao.id_usuario_oficial,
                        u.usuario AS usuario_oficial,
                        CONCAT(u.nombres, N' ', u.apellidos) AS nombre_oficial,
                        ao.fecha_asignacion
                    FROM creditos.asignacion_oficial_credito ao
                    LEFT JOIN seguridad.usuario u
                        ON u.id_usuario = ao.id_usuario_oficial
                    WHERE ao.id_credito = cr.id_credito
                      AND ao.activo = 1
                      AND ao.fecha_fin IS NULL
                    ORDER BY ao.fecha_asignacion DESC, ao.id_asignacion_oficial_credito DESC
                ) oficial
                OUTER APPLY
                (
                    SELECT
                        SUM(CASE
                            WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN
                                CASE
                                    WHEN (
                                        ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                        ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                        ISNULL(pp.deslizamiento_programado, 0) -
                                        ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                        ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                                    ) > 0 THEN
                                        ISNULL(pp.capital_programado, 0) + ISNULL(pp.interes_programado, 0) +
                                        ISNULL(pp.comision_programada, 0) + ISNULL(pp.mora_programada, 0) +
                                        ISNULL(pp.deslizamiento_programado, 0) -
                                        ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.interes_pagado_cuota, 0) -
                                        ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.mora_pagada_cuota, 0)
                                    ELSE 0
                                END
                            ELSE 0
                        END) AS saldo_vencido,
                        SUM(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN 1 ELSE 0 END) AS cuotas_vencidas,
                        MAX(CASE WHEN pp.pagada = 0 AND pp.fecha_cuota < @fecha_corte THEN DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) ELSE 0 END) AS dias_mora,
                        MIN(CASE WHEN pp.pagada = 0 THEN pp.fecha_cuota ELSE NULL END) AS proxima_fecha,
                        MIN(CASE WHEN pp.pagada = 0 THEN pp.numero_cuota ELSE NULL END) AS proxima_cuota
                    FROM creditos.plan_pago_credito pp
                    WHERE pp.id_credito = cr.id_credito
                ) planInfo
                WHERE cr.id_credito = @id_credito
                  AND cr.activo = 1
                  AND (@puede_ver_todo = 1 OR oficial.id_usuario_oficial = @id_usuario);

                SELECT
                    pp.id_plan_pago_credito,
                    pp.numero_cuota,
                    pp.fecha_cuota,
                    pp.saldo_capital_cuota,
                    pp.capital_programado,
                    pp.interes_programado,
                    pp.comision_programada,
                    pp.mora_programada,
                    pp.deslizamiento_programado,
                    pp.capital_pagado_cuota,
                    pp.interes_pagado_cuota,
                    pp.comision_pagada_cuota,
                    pp.mora_pagada_cuota,
                    pp.dias_mora,
                    pp.estado_cuota,
                    pp.pagada
                FROM creditos.plan_pago_credito pp
                WHERE pp.id_credito = @id_credito
                ORDER BY pp.numero_cuota;

                SELECT TOP (25)
                    p.id_pago_credito,
                    p.fecha_pago,
                    p.monto_pagado,
                    p.moneda_pago,
                    p.forma_pago,
                    p.numero_recibo,
                    p.estado_pago,
                    p.nombre_abonante,
                    p.cedula_abonante,
                    p.observacion
                FROM creditos.pago_credito p
                WHERE p.id_credito = @id_credito
                  AND ISNULL(p.anulado, 0) = 0
                ORDER BY p.fecha_pago DESC, p.id_pago_credito DESC;

                SELECT TOP (25)
                    r.id_recibo_pago_credito,
                    r.numero_recibo,
                    r.fecha_recibo,
                    r.monto_total,
                    r.moneda,
                    r.observacion
                FROM creditos.recibo_pago_credito r
                INNER JOIN creditos.pago_credito p
                    ON p.id_pago_credito = r.id_pago_credito
                WHERE p.id_credito = @id_credito
                ORDER BY r.fecha_recibo DESC, r.id_recibo_pago_credito DESC;

                SELECT
                    tv.fecha_tasa,
                    tv.tasa_interes_anual,
                    tv.observacion,
                    tv.usuario_registro
                FROM creditos.tasa_variable_credito tv
                WHERE tv.id_credito = @id_credito
                ORDER BY tv.fecha_tasa DESC, tv.id_tasa_variable DESC;
                """,
                connection);
            command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = id;
            command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoff;
            command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = session.UserId;
            command.Parameters.Add("@puede_ver_todo", SqlDbType.Bit).Value = session.CanSeeFullPortfolio;

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { ok = false, message = "Prestamo no encontrado o fuera de tu cartera asignada." });
            }

            var loan = new
            {
                id = ReadInt64(reader, "id_credito"),
                number = ReadString(reader, "numero_credito"),
                clientId = ReadInt64Nullable(reader, "id_cliente"),
                clientIdentification = ReadString(reader, "cedula_id_cliente"),
                clientName = ReadString(reader, "nom_cliente"),
                currency = ReadString(reader, "moneda", "NIO"),
                approvedAmount = ReadDecimal(reader, "monto_aprobado"),
                termMonths = ReadInt32(reader, "plazo_meses"),
                annualRate = ReadDecimal(reader, "tasa_interes_anual"),
                disbursementDate = ReadDateNullable(reader, "fecha_desembolso"),
                dueDate = ReadDateNullable(reader, "fecha_vencimiento"),
                status = ReadString(reader, "estado_operativo"),
                capitalBalance = ReadDecimal(reader, "saldo_capital"),
                interestBalance = ReadDecimal(reader, "interes_acumulado"),
                moraBalance = ReadDecimal(reader, "mora_acumulada"),
                chargeBalance = ReadDecimal(reader, "cargos_acumulados"),
                commissionBalance = ReadDecimal(reader, "comision_acumulada"),
                product = ReadString(reader, "producto_credito", "MICROCREDITO"),
                frequency = ReadString(reader, "frecuencia_pago", "MENSUAL"),
                destination = ReadString(reader, "destino_credito"),
                riskLevel = ReadString(reader, "nivel_riesgo", "MEDIO"),
                conamiClassification = ReadString(reader, "clasificacion_conami", "A"),
                officerId = ReadInt64Nullable(reader, "id_usuario_oficial"),
                officerUser = ReadString(reader, "usuario_oficial"),
                officerName = ReadString(reader, "nombre_oficial"),
                officerAssignedAt = ReadDateTimeNullable(reader, "fecha_asignacion"),
                overdueBalance = ReadDecimal(reader, "saldo_vencido"),
                overdueInstallments = ReadInt32(reader, "cuotas_vencidas"),
                overdueDays = ReadInt32(reader, "dias_mora"),
                nextDueDate = ReadDateNullable(reader, "proxima_fecha"),
                nextInstallment = ReadInt32Nullable(reader, "proxima_cuota"),
            };

            var plan = new List<object>();
            reader.NextResult();
            while (reader.Read())
            {
                var scheduled = ReadDecimal(reader, "capital_programado") + ReadDecimal(reader, "interes_programado") +
                    ReadDecimal(reader, "comision_programada") + ReadDecimal(reader, "mora_programada") +
                    ReadDecimal(reader, "deslizamiento_programado");
                var paid = ReadDecimal(reader, "capital_pagado_cuota") + ReadDecimal(reader, "interes_pagado_cuota") +
                    ReadDecimal(reader, "comision_pagada_cuota") + ReadDecimal(reader, "mora_pagada_cuota");
                plan.Add(new
                {
                    id = ReadInt64(reader, "id_plan_pago_credito"),
                    number = ReadInt32(reader, "numero_cuota"),
                    dueDate = ReadDateNullable(reader, "fecha_cuota"),
                    balance = ReadDecimal(reader, "saldo_capital_cuota"),
                    capital = ReadDecimal(reader, "capital_programado"),
                    interest = ReadDecimal(reader, "interes_programado"),
                    commission = ReadDecimal(reader, "comision_programada"),
                    mora = ReadDecimal(reader, "mora_programada"),
                    sliding = ReadDecimal(reader, "deslizamiento_programado"),
                    paidCapital = ReadDecimal(reader, "capital_pagado_cuota"),
                    paidInterest = ReadDecimal(reader, "interes_pagado_cuota"),
                    paidCommission = ReadDecimal(reader, "comision_pagada_cuota"),
                    paidMora = ReadDecimal(reader, "mora_pagada_cuota"),
                    scheduledTotal = scheduled,
                    paidTotal = paid,
                    pendingTotal = Math.Max(0, scheduled - paid),
                    overdueDays = ReadInt32(reader, "dias_mora"),
                    status = ReadString(reader, "estado_cuota"),
                    paid = ReadBoolean(reader, "pagada"),
                });
            }

            var payments = new List<object>();
            reader.NextResult();
            while (reader.Read())
            {
                payments.Add(new
                {
                    id = ReadInt64(reader, "id_pago_credito"),
                    date = ReadDateNullable(reader, "fecha_pago"),
                    amount = ReadDecimal(reader, "monto_pagado"),
                    currency = ReadString(reader, "moneda_pago", "NIO"),
                    method = ReadString(reader, "forma_pago"),
                    receipt = ReadString(reader, "numero_recibo"),
                    status = ReadString(reader, "estado_pago"),
                    payer = ReadString(reader, "nombre_abonante"),
                    payerIdentification = ReadString(reader, "cedula_abonante"),
                    note = ReadString(reader, "observacion"),
                });
            }

            var receipts = new List<object>();
            reader.NextResult();
            while (reader.Read())
            {
                receipts.Add(new
                {
                    id = ReadInt64(reader, "id_recibo_pago_credito"),
                    number = ReadString(reader, "numero_recibo"),
                    date = ReadDateTimeNullable(reader, "fecha_recibo"),
                    amount = ReadDecimal(reader, "monto_total"),
                    currency = ReadString(reader, "moneda", "NIO"),
                    note = ReadString(reader, "observacion"),
                });
            }

            var rates = new List<object>();
            reader.NextResult();
            while (reader.Read())
            {
                rates.Add(new
                {
                    date = ReadDateNullable(reader, "fecha_tasa"),
                    annualRate = ReadDecimal(reader, "tasa_interes_anual"),
                    note = ReadString(reader, "observacion"),
                    user = ReadString(reader, "usuario_registro"),
                });
            }

            return Json(new { ok = true, data = new { loan, plan, payments, receipts, rates, cutoffDate = cutoff } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el detalle de cartera.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult RecuperacionDiaria(DateTime? fecha)
    {
        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeAssignedPortfolio)
            {
                return Forbid();
            }

            var reportDate = (fecha ?? DateTime.Today).Date;
            using var command = new SqlCommand(
                """
                SELECT
                    COALESCE(oficial.id_usuario_oficial, 0) AS id_usuario_oficial,
                    COALESCE(oficial.usuario_oficial, N'SIN_ASIGNAR') AS usuario_oficial,
                    COALESCE(NULLIF(oficial.nombre_oficial, N''), N'Sin asignar') AS nombre_oficial,
                    COUNT(DISTINCT cr.id_credito) AS creditos,
                    ISNULL(SUM(cuota.monto_programado), 0) AS monto_a_recuperar,
                    ISNULL(SUM(pago.monto_recuperado), 0) AS monto_recuperado,
                    ISNULL(SUM(cuota.monto_programado), 0) - ISNULL(SUM(pago.monto_recuperado), 0) AS pendiente
                FROM creditos.credito cr
                OUTER APPLY
                (
                    SELECT TOP (1)
                        ao.id_usuario_oficial,
                        u.usuario AS usuario_oficial,
                        CONCAT(u.nombres, N' ', u.apellidos) AS nombre_oficial
                    FROM creditos.asignacion_oficial_credito ao
                    LEFT JOIN seguridad.usuario u
                        ON u.id_usuario = ao.id_usuario_oficial
                    WHERE ao.id_credito = cr.id_credito
                      AND ao.activo = 1
                      AND ao.fecha_fin IS NULL
                    ORDER BY ao.fecha_asignacion DESC, ao.id_asignacion_oficial_credito DESC
                ) oficial
                INNER JOIN
                (
                    SELECT
                        id_credito,
                        SUM(
                            CASE WHEN ISNULL(capital_programado, 0) - ISNULL(capital_pagado_cuota, 0) > 0 THEN ISNULL(capital_programado, 0) - ISNULL(capital_pagado_cuota, 0) ELSE 0 END +
                            CASE WHEN ISNULL(interes_programado, 0) - ISNULL(interes_pagado_cuota, 0) > 0 THEN ISNULL(interes_programado, 0) - ISNULL(interes_pagado_cuota, 0) ELSE 0 END +
                            CASE WHEN ISNULL(comision_programada, 0) - ISNULL(comision_pagada_cuota, 0) > 0 THEN ISNULL(comision_programada, 0) - ISNULL(comision_pagada_cuota, 0) ELSE 0 END +
                            CASE WHEN ISNULL(mora_programada, 0) - ISNULL(mora_pagada_cuota, 0) > 0 THEN ISNULL(mora_programada, 0) - ISNULL(mora_pagada_cuota, 0) ELSE 0 END +
                            ISNULL(deslizamiento_programado, 0)
                        ) AS monto_programado
                    FROM creditos.plan_pago_credito
                    WHERE fecha_cuota = @fecha
                    GROUP BY id_credito
                ) cuota
                    ON cuota.id_credito = cr.id_credito
                OUTER APPLY
                (
                    SELECT SUM(ISNULL(p.monto_aplicado_moneda_credito, p.monto_pagado)) AS monto_recuperado
                    FROM creditos.pago_credito p
                    WHERE p.id_credito = cr.id_credito
                      AND CONVERT(date, p.fecha_pago) = @fecha
                      AND ISNULL(p.anulado, 0) = 0
                      AND p.estado_pago = N'APLICADO'
                ) pago
                WHERE cr.activo = 1
                  AND cr.fecha_desembolso IS NOT NULL
                  AND (@puede_ver_todo = 1 OR oficial.id_usuario_oficial = @id_usuario)
                GROUP BY
                    COALESCE(oficial.id_usuario_oficial, 0),
                    COALESCE(oficial.usuario_oficial, N'SIN_ASIGNAR'),
                    COALESCE(NULLIF(oficial.nombre_oficial, N''), N'Sin asignar')
                ORDER BY nombre_oficial;
                """,
                connection);
            command.Parameters.Add("@fecha", SqlDbType.Date).Value = reportDate;
            command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = session.UserId;
            command.Parameters.Add("@puede_ver_todo", SqlDbType.Bit).Value = session.CanSeeFullPortfolio;

            using var reader = command.ExecuteReader();
            var rows = new List<object>();
            decimal expected = 0;
            decimal recovered = 0;
            while (reader.Read())
            {
                var rowExpected = ReadDecimal(reader, "monto_a_recuperar");
                var rowRecovered = ReadDecimal(reader, "monto_recuperado");
                expected += rowExpected;
                recovered += rowRecovered;
                rows.Add(new
                {
                    officerId = ReadInt64(reader, "id_usuario_oficial"),
                    officerUser = ReadString(reader, "usuario_oficial"),
                    officerName = ReadString(reader, "nombre_oficial"),
                    credits = ReadInt32(reader, "creditos"),
                    expected = rowExpected,
                    recovered = rowRecovered,
                    pending = Math.Max(0, rowExpected - rowRecovered),
                    progress = rowExpected <= 0 ? 0 : Math.Round(rowRecovered / rowExpected * 100m, 2),
                });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    date = reportDate,
                    expected,
                    recovered,
                    pending = Math.Max(0, expected - recovered),
                    progress = expected <= 0 ? 0 : Math.Round(recovered / expected * 100m, 2),
                    rows,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar recuperacion diaria.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ClasificacionRegulatoria(DateTime? fechaCorte, bool persistir = false, bool reprocesar = false, string? tipoCierre = null)
    {
        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeAssignedPortfolio)
            {
                return Forbid();
            }

            if (persistir && !session.CanSeeFullPortfolio)
            {
                return Forbid();
            }

            var cutoff = (fechaCorte ?? DateTime.Today).Date;
            using var command = new SqlCommand("regulatorio.usp_calcular_cartera_conami_sifnic", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 180,
            };
            command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoff;
            command.Parameters.Add("@codigo_version", SqlDbType.NVarChar, 50).Value = "MUC_CONAMI_V09";
            command.Parameters.Add("@tipo_cierre", SqlDbType.NVarChar, 20).Value = string.IsNullOrWhiteSpace(tipoCierre) ? "DIARIO" : tipoCierre.Trim().ToUpperInvariant();
            command.Parameters.Add("@persistir", SqlDbType.Bit).Value = persistir;
            command.Parameters.Add("@reprocesar", SqlDbType.Bit).Value = reprocesar;
            command.Parameters.Add("@usuario_ejecucion", SqlDbType.NVarChar, 100).Value = session.Username;

            using var reader = command.ExecuteReader();
            var rows = new List<RegulatoryPortfolioRow>();
            while (reader.Read())
            {
                rows.Add(new RegulatoryPortfolioRow
                {
                    Cycle = ReadString(reader, "cedula_id_cliente_ofic_ciclo"),
                    ClientIdentification = ReadString(reader, "cedula_id_cliente"),
                    ClientName = ReadString(reader, "nom_cliente"),
                    SourceStatus = ReadString(reader, "estado_operativo_origen"),
                    FinalStatus = ReadString(reader, "estado_operativo_final"),
                    OverdueDays = ReadInt32(reader, "dias_mora"),
                    OverdueInstallments = ReadInt32(reader, "cuotas_vencidas"),
                    Category = ReadString(reader, "categoria"),
                    Classification = ReadString(reader, "clasificacion_credito"),
                    ProvisionRate = ReadDecimal(reader, "porcentaje_provision"),
                    ProvisionAmount = ReadDecimal(reader, "monto_provision"),
                    CapitalBalance = ReadDecimal(reader, "saldo_capital"),
                    TotalBalance = ReadDecimal(reader, "saldo_total"),
                    OverdueAmount = ReadDecimal(reader, "monto_en_mora"),
                    GroupType = ReadInt32(reader, "tipo_agrupacion"),
                    Guarantee = ReadString(reader, "garantia"),
                    Branch = ReadString(reader, "oficina"),
                    DisbursementDate = ReadDateNullable(reader, "fecha_desembolso"),
                    DueDate = ReadDateNullable(reader, "fecha_vencimiento"),
                    RecognizesIncome = ReadBoolean(reader, "reconocimiento_ingreso"),
                    WritesOffInterest = ReadBoolean(reader, "sanea_intereses"),
                    InterestReceivable = ReadDecimal(reader, "interes_por_cobrar"),
                    CommissionReceivable = ReadDecimal(reader, "comision_por_cobrar"),
                    ClosureBatchId = ReadInt64Nullable(reader, "id_lote_cierre"),
                });
            }

            var summary = rows
                .GroupBy(row => new { row.Category, row.Classification, row.FinalStatus })
                .OrderBy(group => group.Key.Category)
                .ThenBy(group => group.Key.FinalStatus)
                .Select(group => new
                {
                    group.Key.Category,
                    group.Key.Classification,
                    group.Key.FinalStatus,
                    Credits = group.Count(),
                    CapitalBalance = group.Sum(row => row.CapitalBalance),
                    OverdueAmount = group.Sum(row => row.OverdueAmount),
                    ProvisionAmount = group.Sum(row => row.ProvisionAmount),
                })
                .ToArray();

            return Json(new
            {
                ok = true,
                data = new
                {
                    cutoffDate = cutoff,
                    persisted = persistir,
                    batchId = rows.Select(row => row.ClosureBatchId).FirstOrDefault(id => id.HasValue),
                    totals = new
                    {
                        credits = rows.Count,
                        capitalBalance = rows.Sum(row => row.CapitalBalance),
                        overdueAmount = rows.Sum(row => row.OverdueAmount),
                        provisionAmount = rows.Sum(row => row.ProvisionAmount),
                    },
                    summary,
                    rows,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo calcular la clasificacion regulatoria de cartera.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult UltimoCierreRegulatorio()
    {
        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeAssignedPortfolio)
            {
                return Forbid();
            }

            using var command = new SqlCommand(
                """
                SELECT TOP (300)
                    fecha_cierre,
                    id_lote_cierre,
                    cedula_id_cliente_ofic_ciclo,
                    cedula_id_cliente,
                    nom_cliente,
                    estado_operativo_origen,
                    estado_operativo_final,
                    dias_mora,
                    cuotas_vencidas,
                    categoria,
                    clasificacion_credito,
                    porcentaje_provision,
                    monto_provision,
                    saldo_capital,
                    saldo_total,
                    monto_en_mora,
                    estado_lote
                FROM reportes.vw_cartera_regulatoria_ultimo_cierre
                ORDER BY dias_mora DESC, cedula_id_cliente_ofic_ciclo;
                """,
                connection);
            using var reader = command.ExecuteReader();
            var rows = new List<object>();
            while (reader.Read())
            {
                rows.Add(new
                {
                    cutoffDate = ReadDateNullable(reader, "fecha_cierre"),
                    batchId = ReadInt64(reader, "id_lote_cierre"),
                    cycle = ReadString(reader, "cedula_id_cliente_ofic_ciclo"),
                    clientIdentification = ReadString(reader, "cedula_id_cliente"),
                    clientName = ReadString(reader, "nom_cliente"),
                    sourceStatus = ReadString(reader, "estado_operativo_origen"),
                    finalStatus = ReadString(reader, "estado_operativo_final"),
                    overdueDays = ReadInt32(reader, "dias_mora"),
                    overdueInstallments = ReadInt32(reader, "cuotas_vencidas"),
                    category = ReadString(reader, "categoria"),
                    classification = ReadString(reader, "clasificacion_credito"),
                    provisionRate = ReadDecimal(reader, "porcentaje_provision"),
                    provisionAmount = ReadDecimal(reader, "monto_provision"),
                    capitalBalance = ReadDecimal(reader, "saldo_capital"),
                    totalBalance = ReadDecimal(reader, "saldo_total"),
                    overdueAmount = ReadDecimal(reader, "monto_en_mora"),
                    batchStatus = ReadString(reader, "estado_lote"),
                });
            }

            return Json(new { ok = true, data = rows });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo consultar el ultimo cierre regulatorio.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Reasignar([FromBody] PortfolioAssignmentModel model)
    {
        return ChangeAssignment(model, "REASIGNACION");
    }

    [HttpPost]
    public IActionResult Desasignar([FromBody] PortfolioAssignmentModel model)
    {
        return ChangeAssignment(model, "DESASIGNACION");
    }

    private static SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(ConexionDb.Cadena);
        connection.Open();
        CreditOperationsSupport.EnsureSchema(connection);
        CreditPortfolioSecuritySupport.EnsureSchema(connection);
        RegulatoryPortfolioSupport.EnsureSchema(connection);
        return connection;
    }

    private IActionResult ChangeAssignment(PortfolioAssignmentModel model, string action)
    {
        if (model.CreditId <= 0)
        {
            return BadRequest(new { ok = false, message = "Selecciona el credito." });
        }

        if (action == "REASIGNACION" && model.OfficerId <= 0)
        {
            return BadRequest(new { ok = false, message = "Selecciona el nuevo oficial." });
        }

        if (string.IsNullOrWhiteSpace(model.Reason) || model.Reason.Trim().Length < 8)
        {
            return BadRequest(new { ok = false, message = "Indica un motivo claro para la bitacora." });
        }

        try
        {
            using var connection = OpenConnection();
            var session = CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!session.CanSeeFullPortfolio)
            {
                return Forbid();
            }

            using var transaction = connection.BeginTransaction();
            long? previousOfficer = null;
            using (var current = new SqlCommand(
                """
                SELECT TOP (1) id_usuario_oficial
                FROM creditos.asignacion_oficial_credito WITH (UPDLOCK, ROWLOCK)
                WHERE id_credito = @id_credito
                  AND activo = 1
                  AND fecha_fin IS NULL
                ORDER BY fecha_asignacion DESC, id_asignacion_oficial_credito DESC;
                """,
                connection,
                transaction))
            {
                current.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = model.CreditId;
                var value = current.ExecuteScalar();
                previousOfficer = value is null || value == DBNull.Value ? null : Convert.ToInt64(value);
            }

            using (var closeCurrent = new SqlCommand(
                """
                UPDATE creditos.asignacion_oficial_credito
                SET activo = 0,
                    fecha_fin = SYSDATETIME(),
                    motivo = @motivo,
                    observacion = @observacion
                WHERE id_credito = @id_credito
                  AND activo = 1
                  AND fecha_fin IS NULL;
                """,
                connection,
                transaction))
            {
                closeCurrent.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = model.CreditId;
                closeCurrent.Parameters.Add("@motivo", SqlDbType.NVarChar, 600).Value = model.Reason.Trim();
                closeCurrent.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(model.Observation);
                closeCurrent.ExecuteNonQuery();
            }

            if (action == "REASIGNACION")
            {
                using var insert = new SqlCommand(
                    """
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
                        @motivo,
                        @observacion
                    );
                    """,
                    connection,
                    transaction);
                insert.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = model.CreditId;
                insert.Parameters.Add("@id_usuario_oficial", SqlDbType.BigInt).Value = model.OfficerId;
                insert.Parameters.Add("@id_usuario_asigna", SqlDbType.BigInt).Value = session.UserId;
                insert.Parameters.Add("@motivo", SqlDbType.NVarChar, 600).Value = model.Reason.Trim();
                insert.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(model.Observation);
                insert.ExecuteNonQuery();
            }

            using (var history = new SqlCommand(
                """
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
                    @id_usuario_oficial_anterior,
                    @id_usuario_oficial_nuevo,
                    @id_usuario_accion,
                    @tipo_accion,
                    @motivo,
                    @observacion
                );
                """,
                connection,
                transaction))
            {
                history.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = model.CreditId;
                history.Parameters.Add("@id_usuario_oficial_anterior", SqlDbType.BigInt).Value = previousOfficer.HasValue ? previousOfficer.Value : DBNull.Value;
                history.Parameters.Add("@id_usuario_oficial_nuevo", SqlDbType.BigInt).Value = action == "REASIGNACION" ? model.OfficerId : DBNull.Value;
                history.Parameters.Add("@id_usuario_accion", SqlDbType.BigInt).Value = session.UserId;
                history.Parameters.Add("@tipo_accion", SqlDbType.NVarChar, 50).Value = action;
                history.Parameters.Add("@motivo", SqlDbType.NVarChar, 600).Value = model.Reason.Trim();
                history.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(model.Observation);
                history.ExecuteNonQuery();
            }

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CREDITOS",
                "CARTERA_ASIGNACION",
                action,
                model.CreditId,
                model.CreditId.ToString(),
                action == "REASIGNACION" ? "Cartera reasignada a otro oficial." : "Cartera desasignada.",
                new { model.CreditId, previousOfficer, model.OfficerId, model.Reason, model.Observation });

            transaction.Commit();
            return Json(new { ok = true, message = action == "REASIGNACION" ? "Cartera reasignada." : "Cartera desasignada." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cambiar la asignacion de cartera.", detail = ex.Message });
        }
    }

    private static void AddPortfolioParameters(
        SqlCommand command,
        CreditPortfolioSession session,
        string? search,
        string? status,
        long officerId,
        DateTime cutoffDate)
    {
        command.Parameters.Add("@buscar", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
        command.Parameters.Add("@estado", SqlDbType.NVarChar, 30).Value = NormalizeStatus(status);
        command.Parameters.Add("@id_oficial", SqlDbType.BigInt).Value = officerId;
        command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoffDate.Date;
        command.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = session.UserId;
        command.Parameters.Add("@puede_ver_todo", SqlDbType.Bit).Value = session.CanSeeFullPortfolio;
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "TODOS" : status.Trim().ToUpperInvariant();
        return normalized == "TODOS" ? "TODOS" : normalized;
    }

    private static IReadOnlyList<string> LoadStatuses(SqlConnection connection)
    {
        using var command = new SqlCommand(
            """
            SELECT DISTINCT estado_operativo
            FROM creditos.credito
            WHERE estado_operativo IS NOT NULL
            ORDER BY estado_operativo;
            """,
            connection);
        using var reader = command.ExecuteReader();
        var statuses = new List<string>();
        while (reader.Read())
        {
            statuses.Add(reader.GetString(0));
        }

        if (!statuses.Contains("VI", StringComparer.OrdinalIgnoreCase))
        {
            statuses.Insert(0, "VI");
        }

        return statuses;
    }

    private static IReadOnlyList<object> LoadOfficers(SqlConnection connection)
    {
        using var command = new SqlCommand(
            """
            SELECT DISTINCT
                u.id_usuario,
                u.usuario,
                CONCAT(u.nombres, N' ', u.apellidos) AS nombre
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
            ORDER BY nombre, u.usuario;
            """,
            connection);
        using var reader = command.ExecuteReader();
        var officers = new List<object>();
        while (reader.Read())
        {
            officers.Add(new
            {
                id = ReadInt64(reader, "id_usuario"),
                user = ReadString(reader, "usuario"),
                name = ReadString(reader, "nombre"),
            });
        }

        return officers;
    }

    private static string ReadString(SqlDataReader reader, string name, string fallback = "")
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal).Trim();
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

    private static DateTime? ReadDateNullable(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal).Date;
    }

    private static DateTime? ReadDateTimeNullable(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static bool ReadBoolean(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }

    public sealed class PortfolioAssignmentModel
    {
        public long CreditId { get; set; }
        public long OfficerId { get; set; }
        public string? Reason { get; set; }
        public string? Observation { get; set; }
    }

    private sealed class RegulatoryPortfolioRow
    {
        public string Cycle { get; set; } = string.Empty;
        public string ClientIdentification { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string SourceStatus { get; set; } = string.Empty;
        public string FinalStatus { get; set; } = string.Empty;
        public int OverdueDays { get; set; }
        public int OverdueInstallments { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public decimal ProvisionRate { get; set; }
        public decimal ProvisionAmount { get; set; }
        public decimal CapitalBalance { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal OverdueAmount { get; set; }
        public int GroupType { get; set; }
        public string Guarantee { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public DateTime? DisbursementDate { get; set; }
        public DateTime? DueDate { get; set; }
        public bool RecognizesIncome { get; set; }
        public bool WritesOffInterest { get; set; }
        public decimal InterestReceivable { get; set; }
        public decimal CommissionReceivable { get; set; }
        public long? ClosureBatchId { get; set; }
    }
}

