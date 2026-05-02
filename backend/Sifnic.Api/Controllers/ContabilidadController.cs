using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Creditos;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class ContabilidadController : Controller
{
    private static readonly Regex AccountCodePattern = new(@"^\d{1,20}$", RegexOptions.Compiled);

    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            MicrofinanceCoreSupport.EnsureSchema(connection);
            return Json(new
            {
                ok = true,
                data = new
                {
                    session = new
                    {
                        session.UserId,
                        session.Username,
                        session.DisplayName,
                        session.Roles,
                    },
                    classes = new[]
                    {
                        new { value = 1, label = "1 - Activo" },
                        new { value = 2, label = "2 - Pasivo" },
                        new { value = 3, label = "3 - Patrimonio" },
                        new { value = 4, label = "4 - Ingresos" },
                        new { value = 5, label = "5 - Gastos" },
                        new { value = 6, label = "6 - Cuentas de orden" },
                        new { value = 7, label = "7 - Cuentas de orden" },
                        new { value = 8, label = "8 - Cuentas de orden deudoras" },
                        new { value = 9, label = "9 - Cuentas de orden acreedoras" },
                    },
                    natures = new[]
                    {
                        new { value = "D", label = "Deudora" },
                        new { value = "A", label = "Acreedora" },
                    },
                    reportTypes = new[] { "BALANCE_GENERAL", "ESTADO_RESULTADOS", "BALANCE_COMPROBACION", "CARTERA_CONTABLE", "PRIM_BASE" },
                    microfinanceCore = new
                    {
                        products = MicrofinanceCoreSupport.LoadProducts(connection),
                        uafAlerts = MicrofinanceCoreSupport.LoadUafAlerts(connection),
                        cashOperations = MicrofinanceCoreSupport.LoadCatalog(connection, "OPERACION_CAJA"),
                        primDictionary = MicrofinanceCoreSupport.LoadCatalog(connection, "ICC_PRIM"),
                    },
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar Contabilidad.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult CoreMicrofinanciero()
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            MicrofinanceCoreSupport.EnsureSchema(connection);
            return Json(new
            {
                ok = true,
                data = new
                {
                    products = MicrofinanceCoreSupport.LoadProducts(connection),
                    catalogs = new
                    {
                        activities = MicrofinanceCoreSupport.LoadCatalog(connection, "ACTIVIDAD_ECONOMICA"),
                        departments = MicrofinanceCoreSupport.LoadCatalog(connection, "DEPARTAMENTO"),
                        municipalities = MicrofinanceCoreSupport.LoadCatalog(connection, "MUNICIPIO"),
                        guaranteeTypes = MicrofinanceCoreSupport.LoadCatalog(connection, "TIPO_GARANTIA"),
                        administrativeStatuses = MicrofinanceCoreSupport.LoadCatalog(connection, "ESTADO_ADMINISTRATIVO"),
                        primDictionary = MicrofinanceCoreSupport.LoadCatalog(connection, "ICC_PRIM"),
                        cashOperations = MicrofinanceCoreSupport.LoadCatalog(connection, "OPERACION_CAJA"),
                    },
                    uafAlerts = MicrofinanceCoreSupport.LoadUafAlerts(connection),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el core microfinanciero.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Resumen(DateTime? fecha)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            var cutoff = (fecha ?? DateTime.Today).Date;
            using var command = new SqlCommand(
                """
                SELECT
                    (SELECT COUNT(1) FROM contabilidad.catalogo_cuenta_muc WHERE activa = 1) AS cuentas_activas,
                    (SELECT COUNT(1) FROM contabilidad.catalogo_cuenta_muc WHERE activa = 0) AS cuentas_inactivas,
                    (SELECT COUNT(1) FROM contabilidad.catalogo_cuenta_muc WHERE ISNULL(nivel, 0) >= 8 AND activa = 1) AS cuentas_movimiento,
                    (SELECT COUNT(1) FROM contabilidad.asiento WHERE anulado = 0) AS asientos_activos,
                    (SELECT COUNT(1) FROM contabilidad.asiento WHERE anulado = 1) AS asientos_anulados,
                    (SELECT COUNT(1) FROM contabilidad.periodo_contable WHERE activo = 1 AND estado_periodo = N'ABIERTO') AS periodos_abiertos,
                    (SELECT ISNULL(SUM(total_debito), 0) FROM reportes.vw_contabilidad_asientos WHERE anulado = 0 AND fecha_asiento <= @fecha) AS debitos,
                    (SELECT ISNULL(SUM(total_credito), 0) FROM reportes.vw_contabilidad_asientos WHERE anulado = 0 AND fecha_asiento <= @fecha) AS creditos,
                    (SELECT MAX(fecha_asiento) FROM contabilidad.asiento WHERE anulado = 0) AS ultimo_asiento;
                """,
                connection);
            command.Parameters.Add("@fecha", SqlDbType.Date).Value = cutoff;
            using var reader = command.ExecuteReader();
            reader.Read();

            return Json(new
            {
                ok = true,
                data = new
                {
                    activeAccounts = ReadInt32(reader, "cuentas_activas"),
                    inactiveAccounts = ReadInt32(reader, "cuentas_inactivas"),
                    movementAccounts = ReadInt32(reader, "cuentas_movimiento"),
                    activeEntries = ReadInt32(reader, "asientos_activos"),
                    voidEntries = ReadInt32(reader, "asientos_anulados"),
                    openPeriods = ReadInt32(reader, "periodos_abiertos"),
                    debits = ReadDecimal(reader, "debitos"),
                    credits = ReadDecimal(reader, "creditos"),
                    difference = ReadDecimal(reader, "debitos") - ReadDecimal(reader, "creditos"),
                    lastEntryDate = ReadDateTimeNullable(reader, "ultimo_asiento"),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el resumen contable.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ListarCuentas(string? search, int? accountClass, string? status, bool movementOnly = false)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(
                """
                SELECT TOP (500)
                    codigo_cuenta,
                    codigo_cuenta_normalizado,
                    nombre_cuenta,
                    clase,
                    grupo,
                    nivel,
                    naturaleza,
                    activa,
                    nivel_1,
                    nivel_2,
                    nivel_3,
                    permite_movimiento
                FROM reportes.vw_contabilidad_catalogo_muc
                WHERE
                    (@buscar = N''
                        OR codigo_cuenta LIKE N'%' + @buscar + N'%'
                        OR codigo_cuenta_normalizado LIKE N'%' + @buscar + N'%'
                        OR nombre_cuenta LIKE N'%' + @buscar + N'%')
                    AND (@clase = 0 OR clase = @clase)
                    AND (@estado = N'TODOS' OR (@estado = N'ACTIVA' AND activa = 1) OR (@estado = N'INACTIVA' AND activa = 0))
                    AND (@solo_movimiento = 0 OR permite_movimiento = 1)
                ORDER BY codigo_cuenta_normalizado, codigo_cuenta;
                """,
                connection);
            command.Parameters.Add("@buscar", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@clase", SqlDbType.Int).Value = accountClass.GetValueOrDefault();
            command.Parameters.Add("@estado", SqlDbType.NVarChar, 20).Value = NormalizeStatus(status);
            command.Parameters.Add("@solo_movimiento", SqlDbType.Bit).Value = movementOnly;

            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(MapAccount(reader));
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el catalogo MUC.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ObtenerCuenta(string codigo)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(
                """
                SELECT TOP (1)
                    codigo_cuenta,
                    codigo_cuenta_normalizado,
                    nombre_cuenta,
                    clase,
                    grupo,
                    nivel,
                    naturaleza,
                    activa,
                    nivel_1,
                    nivel_2,
                    nivel_3,
                    permite_movimiento
                FROM reportes.vw_contabilidad_catalogo_muc
                WHERE codigo_cuenta = @codigo OR codigo_cuenta_normalizado = @codigo;
                """,
                connection);
            command.Parameters.Add("@codigo", SqlDbType.NVarChar, 20).Value = NormalizeCode(codigo);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return NotFound(new { ok = false, message = "Cuenta contable no encontrada." });
            }

            return Json(new { ok = true, data = MapAccount(reader) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo obtener la cuenta contable.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult GuardarCuenta([FromBody] AccountSaveRequest request)
    {
        var errors = ValidateAccount(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "Revisa los datos de la cuenta contable.", errors });
        }

        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!CanMaintainAccounting(session))
            {
                return Forbid();
            }

            EnsureAccountingSchema(connection);
            using var transaction = connection.BeginTransaction();
            try
            {
                var code = NormalizeCode(request.Code);
                var name = CleanText(request.Name, 250);
                var classCode = request.ClassCode ?? InferClass(code);
                var group = request.Group ?? InferGroup(code);
                var level = request.Level ?? code.Length;
                var nature = NormalizeNature(request.Nature, classCode);
                var active = request.Active ?? true;
                var movement = request.MovementAllowed ?? level >= 8;
                var level1 = CleanText(request.Level1 ?? ClassName(classCode), 50);
                var level2 = CleanText(request.Level2 ?? $"GRUPO {group:00}", 50);
                var level3 = CleanText(request.Level3 ?? name, 50);

                using var save = new SqlCommand(
                    """
                    MERGE contabilidad.catalogo_cuenta_muc AS target
                    USING (SELECT @codigo AS codigo_cuenta) AS source
                    ON target.codigo_cuenta = source.codigo_cuenta
                    WHEN MATCHED THEN
                        UPDATE SET
                            nombre_cuenta = @nombre,
                            clase = @clase,
                            grupo = @grupo,
                            naturaleza = @naturaleza,
                            nivel = @nivel,
                            activa = @activa
                    WHEN NOT MATCHED THEN
                        INSERT(codigo_cuenta,nombre_cuenta,clase,grupo,naturaleza,nivel,activa)
                        VALUES(@codigo,@nombre,@clase,@grupo,@naturaleza,@nivel,@activa);

                    IF OBJECT_ID(N'contabilidad.catalogo_muc_detallado', N'U') IS NOT NULL
                    BEGIN
                        MERGE contabilidad.catalogo_muc_detallado AS target
                        USING (SELECT @codigo AS codigo_cuenta) AS source
                        ON target.codigo_cuenta = source.codigo_cuenta
                        WHEN MATCHED THEN
                            UPDATE SET
                                nombre_cuenta = @nombre,
                                nivel_1 = @nivel_1,
                                nivel_2 = @nivel_2,
                                nivel_3 = @nivel_3,
                                naturaleza = @naturaleza,
                                permite_movimiento = @permite_movimiento,
                                activa = @activa
                        WHEN NOT MATCHED THEN
                            INSERT(codigo_cuenta,nombre_cuenta,nivel_1,nivel_2,nivel_3,naturaleza,permite_movimiento,activa)
                            VALUES(@codigo,@nombre,@nivel_1,@nivel_2,@nivel_3,@naturaleza,@permite_movimiento,@activa);
                    END;
                    """,
                    connection,
                    transaction);
                save.Parameters.Add("@codigo", SqlDbType.NVarChar, 20).Value = code;
                save.Parameters.Add("@nombre", SqlDbType.NVarChar, 250).Value = name;
                save.Parameters.Add("@clase", SqlDbType.Int).Value = classCode;
                save.Parameters.Add("@grupo", SqlDbType.Int).Value = group;
                save.Parameters.Add("@naturaleza", SqlDbType.Char, 1).Value = nature;
                save.Parameters.Add("@nivel", SqlDbType.Int).Value = level;
                save.Parameters.Add("@activa", SqlDbType.Bit).Value = active;
                save.Parameters.Add("@nivel_1", SqlDbType.NVarChar, 50).Value = level1;
                save.Parameters.Add("@nivel_2", SqlDbType.NVarChar, 50).Value = level2;
                save.Parameters.Add("@nivel_3", SqlDbType.NVarChar, 50).Value = level3;
                save.Parameters.Add("@permite_movimiento", SqlDbType.Bit).Value = movement;
                save.ExecuteNonQuery();

                transaction.Commit();
                return Json(new { ok = true, message = "Cuenta contable guardada.", data = new { code } });
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo guardar la cuenta contable.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult CambiarEstadoCuenta([FromBody] AccountStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { ok = false, message = "Selecciona una cuenta contable." });
        }

        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            if (!CanMaintainAccounting(session))
            {
                return Forbid();
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(
                """
                UPDATE contabilidad.catalogo_cuenta_muc
                SET activa = @activa
                WHERE codigo_cuenta = @codigo;

                IF OBJECT_ID(N'contabilidad.catalogo_muc_detallado', N'U') IS NOT NULL
                BEGIN
                    UPDATE contabilidad.catalogo_muc_detallado
                    SET activa = @activa
                    WHERE codigo_cuenta = @codigo;
                END;
                """,
                connection);
            command.Parameters.Add("@codigo", SqlDbType.NVarChar, 20).Value = NormalizeCode(request.Code);
            command.Parameters.Add("@activa", SqlDbType.Bit).Value = request.Active;
            var affected = command.ExecuteNonQuery();
            return affected == 0
                ? NotFound(new { ok = false, message = "Cuenta contable no encontrada." })
                : Json(new { ok = true, message = request.Active ? "Cuenta activada." : "Cuenta inactivada." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cambiar el estado de la cuenta.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ListarAsientos(DateTime? desde, DateTime? hasta, string? search, string? origin)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(
                """
                SELECT TOP (200)
                    id_asiento,
                    fecha_asiento,
                    tipo_asiento,
                    origen_modulo,
                    referencia,
                    descripcion,
                    estado_asiento,
                    codigo_moneda,
                    tipo_cambio,
                    anulado,
                    total_lineas,
                    total_debito,
                    total_credito,
                    puede_revertirse
                FROM reportes.vw_contabilidad_asientos
                WHERE
                    fecha_asiento BETWEEN @desde AND @hasta
                    AND (@origen = N'TODOS' OR origen_modulo = @origen)
                    AND (@buscar = N''
                        OR ISNULL(referencia, N'') LIKE N'%' + @buscar + N'%'
                        OR ISNULL(descripcion, N'') LIKE N'%' + @buscar + N'%'
                        OR ISNULL(tipo_asiento, N'') LIKE N'%' + @buscar + N'%')
                ORDER BY fecha_asiento DESC, id_asiento DESC;
                """,
                connection);
            command.Parameters.Add("@desde", SqlDbType.Date).Value = (desde ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            command.Parameters.Add("@hasta", SqlDbType.Date).Value = (hasta ?? DateTime.Today).Date;
            command.Parameters.Add("@buscar", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@origen", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(origin) ? "TODOS" : origin.Trim().ToUpperInvariant();

            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(new
                {
                    entryId = ReadInt64(reader, "id_asiento"),
                    entryDate = ReadDateTime(reader, "fecha_asiento"),
                    type = ReadString(reader, "tipo_asiento"),
                    origin = ReadString(reader, "origen_modulo"),
                    reference = ReadString(reader, "referencia"),
                    description = ReadString(reader, "descripcion"),
                    status = ReadString(reader, "estado_asiento"),
                    currency = ReadString(reader, "codigo_moneda"),
                    exchangeRate = ReadDecimal(reader, "tipo_cambio"),
                    voided = ReadBool(reader, "anulado"),
                    lines = ReadInt32(reader, "total_lineas"),
                    debit = ReadDecimal(reader, "total_debito"),
                    credit = ReadDecimal(reader, "total_credito"),
                    balanced = ReadDecimal(reader, "total_debito") == ReadDecimal(reader, "total_credito"),
                    canReverse = ReadBool(reader, "puede_revertirse"),
                });
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar los asientos.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult DetalleAsiento(long id)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(
                """
                SELECT
                    id_asiento,
                    fecha_asiento,
                    tipo_asiento,
                    origen_modulo,
                    referencia,
                    descripcion_asiento,
                    estado_asiento,
                    codigo_moneda,
                    codigo_cuenta,
                    nombre_cuenta,
                    descripcion_linea,
                    naturaleza_movimiento,
                    debito,
                    credito,
                    nombre_centro_costo,
                    nombre_cliente
                FROM reportes.vw_contabilidad_diario_general
                WHERE id_asiento = @id
                ORDER BY id_asiento_detalle;
                """,
                connection);
            command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
            using var reader = command.ExecuteReader();
            var lines = new List<object>();
            object? header = null;
            while (reader.Read())
            {
                header ??= new
                {
                    entryId = ReadInt64(reader, "id_asiento"),
                    entryDate = ReadDateTime(reader, "fecha_asiento"),
                    type = ReadString(reader, "tipo_asiento"),
                    origin = ReadString(reader, "origen_modulo"),
                    reference = ReadString(reader, "referencia"),
                    description = ReadString(reader, "descripcion_asiento"),
                    status = ReadString(reader, "estado_asiento"),
                    currency = ReadString(reader, "codigo_moneda"),
                };

                lines.Add(new
                {
                    accountCode = ReadString(reader, "codigo_cuenta"),
                    accountName = ReadString(reader, "nombre_cuenta"),
                    description = ReadString(reader, "descripcion_linea"),
                    nature = ReadString(reader, "naturaleza_movimiento"),
                    debit = ReadDecimal(reader, "debito"),
                    credit = ReadDecimal(reader, "credito"),
                    costCenter = ReadString(reader, "nombre_centro_costo"),
                    client = ReadString(reader, "nombre_cliente"),
                });
            }

            if (header is null)
            {
                return NotFound(new { ok = false, message = "Asiento no encontrado." });
            }

            return Json(new { ok = true, data = new { header, lines } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el detalle del asiento.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult BalanceComprobacion(DateTime? desde, DateTime? hasta)
    {
        return ReportFromSql(
            """
            SELECT
                codigo_cuenta,
                nombre_cuenta,
                nivel_1,
                nivel_2,
                SUM(debito) AS debito,
                SUM(credito) AS credito,
                SUM(debito - credito) AS saldo_deudor,
                SUM(credito - debito) AS saldo_acreedor
            FROM reportes.vw_contabilidad_diario_general
            WHERE anulado = 0 AND fecha_asiento BETWEEN @desde AND @hasta
            GROUP BY codigo_cuenta,nombre_cuenta,nivel_1,nivel_2
            HAVING SUM(debito) <> 0 OR SUM(credito) <> 0
            ORDER BY codigo_cuenta;
            """,
            desde,
            hasta,
            "No se pudo generar el balance de comprobacion.");
    }

    [HttpGet]
    public IActionResult BalanceGeneral(DateTime? hasta)
    {
        return ReportFromSql(
            """
            SELECT
                CASE clase WHEN 1 THEN N'ACTIVO' WHEN 2 THEN N'PASIVO' WHEN 3 THEN N'PATRIMONIO' ELSE ISNULL(nivel_1, N'OTROS') END AS rubro,
                codigo_cuenta,
                nombre_cuenta,
                clase,
                SUM(CASE WHEN clase = 1 THEN debito - credito ELSE credito - debito END) AS saldo
            FROM reportes.vw_contabilidad_diario_general
            WHERE anulado = 0
              AND fecha_asiento <= @hasta
              AND clase IN (1,2,3)
            GROUP BY clase,nivel_1,codigo_cuenta,nombre_cuenta
            HAVING SUM(CASE WHEN clase = 1 THEN debito - credito ELSE credito - debito END) <> 0
            ORDER BY clase,codigo_cuenta;
            """,
            null,
            hasta,
            "No se pudo generar el balance general.",
            singleDateMode: true);
    }

    [HttpGet]
    public IActionResult EstadoResultados(DateTime? desde, DateTime? hasta)
    {
        return ReportFromSql(
            """
            SELECT
                CASE clase WHEN 4 THEN N'INGRESOS' WHEN 5 THEN N'GASTOS' ELSE ISNULL(nivel_1, N'OTROS') END AS rubro,
                codigo_cuenta,
                nombre_cuenta,
                clase,
                SUM(CASE WHEN clase = 4 THEN credito - debito ELSE debito - credito END) AS saldo
            FROM reportes.vw_contabilidad_diario_general
            WHERE anulado = 0
              AND fecha_asiento BETWEEN @desde AND @hasta
              AND clase IN (4,5)
            GROUP BY clase,nivel_1,codigo_cuenta,nombre_cuenta
            HAVING SUM(CASE WHEN clase = 4 THEN credito - debito ELSE debito - credito END) <> 0
            ORDER BY clase,codigo_cuenta;
            """,
            desde,
            hasta,
            "No se pudo generar el estado de resultados.");
    }

    [HttpGet]
    public IActionResult CarteraContable(DateTime? hasta)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(
                """
                SELECT
                    ISNULL(cr.moneda, N'NIO') AS moneda,
                    ISNULL(cr.estado_operativo, N'SIN_ESTADO') AS estado_operativo,
                    COUNT(1) AS creditos,
                    ISNULL(SUM(ISNULL(cr.monto_aprobado, 0)), 0) AS monto_colocado,
                    ISNULL(SUM(ISNULL(cr.saldo_capital, 0)), 0) AS saldo_capital,
                    ISNULL(SUM(ISNULL(cr.interes_acumulado, 0)), 0) AS interes_acumulado,
                    ISNULL(SUM(ISNULL(cr.mora_acumulada, 0)), 0) AS mora_acumulada,
                    ISNULL(SUM(ISNULL(cr.comision_acumulada, 0)), 0) AS comision_acumulada
                FROM creditos.credito cr
                WHERE cr.activo = 1
                  AND (@hasta IS NULL OR CONVERT(date, ISNULL(cr.fecha_desembolso, cr.fecha_creacion)) <= @hasta)
                GROUP BY ISNULL(cr.moneda, N'NIO'), ISNULL(cr.estado_operativo, N'SIN_ESTADO')
                ORDER BY moneda, estado_operativo;
                """,
                connection);
            command.Parameters.Add("@hasta", SqlDbType.Date).Value = (object?)hasta?.Date ?? DBNull.Value;
            return Json(new { ok = true, data = ReadReportRows(command) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo generar cartera contable.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult PrimBase()
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(
                """
                SELECT TOP (300)
                    codigo_cuenta,
                    nombre_cuenta,
                    clase,
                    grupo,
                    naturaleza,
                    nivel_1,
                    nivel_2,
                    nivel_3,
                    activa,
                    permite_movimiento,
                    CASE
                        WHEN codigo_cuenta LIKE N'14%' THEN N'CARTERA_CREDITOS'
                        WHEN codigo_cuenta LIKE N'41%' THEN N'INGRESOS_FINANCIEROS'
                        WHEN codigo_cuenta LIKE N'52%' THEN N'GASTOS_PROVISION'
                        WHEN codigo_cuenta LIKE N'82%' OR codigo_cuenta LIKE N'86%' THEN N'CUENTAS_ORDEN_CARTERA'
                        ELSE ISNULL(nivel_1, N'MUC')
                    END AS bloque_prim
                FROM reportes.vw_contabilidad_catalogo_muc
                WHERE activa = 1
                  AND (codigo_cuenta LIKE N'14%' OR codigo_cuenta LIKE N'41%' OR codigo_cuenta LIKE N'52%' OR codigo_cuenta LIKE N'82%' OR codigo_cuenta LIKE N'86%')
                ORDER BY codigo_cuenta;
                """,
                connection);
            return Json(new { ok = true, data = ReadReportRows(command) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo generar la base PRIM.", detail = ex.Message });
        }
    }

    private IActionResult ReportFromSql(string sql, DateTime? desde, DateTime? hasta, string errorMessage, bool singleDateMode = false)
    {
        try
        {
            using var connection = OpenConnection();
            var session = ResolveAccountingSession(connection);
            if (session is null)
            {
                return Unauthorized(new { ok = false, message = "Sesion invalida o expirada." });
            }

            EnsureAccountingSchema(connection);
            using var command = new SqlCommand(sql, connection);
            if (singleDateMode)
            {
                command.Parameters.Add("@hasta", SqlDbType.Date).Value = (hasta ?? DateTime.Today).Date;
            }
            else
            {
                command.Parameters.Add("@desde", SqlDbType.Date).Value = (desde ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
                command.Parameters.Add("@hasta", SqlDbType.Date).Value = (hasta ?? DateTime.Today).Date;
            }

            return Json(new { ok = true, data = ReadReportRows(command) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = errorMessage, detail = ex.Message });
        }
    }

    private static List<Dictionary<string, object?>> ReadReportRows(SqlCommand command)
    {
        using var reader = command.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, string> ValidateAccount(AccountSaveRequest request)
    {
        var errors = new Dictionary<string, string>();
        var code = NormalizeCode(request.Code);
        if (!AccountCodePattern.IsMatch(code))
        {
            errors["code"] = "El codigo debe contener solo numeros y maximo 20 digitos.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = "El nombre de la cuenta es obligatorio.";
        }

        if (!string.IsNullOrWhiteSpace(request.Nature) && !new[] { "D", "A" }.Contains(request.Nature.Trim().ToUpperInvariant()))
        {
            errors["nature"] = "La naturaleza debe ser D o A.";
        }

        if (request.ClassCode is < 1 or > 9)
        {
            errors["classCode"] = "La clase debe estar entre 1 y 9.";
        }

        return errors;
    }

    private static object MapAccount(SqlDataReader reader)
    {
        return new
        {
            code = ReadString(reader, "codigo_cuenta"),
            normalizedCode = ReadString(reader, "codigo_cuenta_normalizado"),
            name = ReadString(reader, "nombre_cuenta"),
            classCode = ReadInt32Nullable(reader, "clase"),
            className = ClassName(ReadInt32Nullable(reader, "clase") ?? 0),
            group = ReadInt32Nullable(reader, "grupo"),
            level = ReadInt32Nullable(reader, "nivel"),
            nature = ReadString(reader, "naturaleza"),
            active = ReadBool(reader, "activa"),
            level1 = ReadString(reader, "nivel_1"),
            level2 = ReadString(reader, "nivel_2"),
            level3 = ReadString(reader, "nivel_3"),
            movementAllowed = ReadBool(reader, "permite_movimiento"),
        };
    }

    private static SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(ConexionDb.Cadena);
        connection.Open();
        return connection;
    }

    private CreditPortfolioSession? ResolveAccountingSession(SqlConnection connection)
    {
        return CreditPortfolioSecuritySupport.ResolveSession(Request, connection);
    }

    private static void EnsureAccountingSchema(SqlConnection connection)
    {
        const string sql = """
            IF SCHEMA_ID(N'contabilidad') IS NULL EXEC(N'CREATE SCHEMA contabilidad');
            IF SCHEMA_ID(N'reportes') IS NULL EXEC(N'CREATE SCHEMA reportes');

            IF NOT EXISTS (SELECT 1 FROM seguridad.rol WHERE codigo_rol = N'CONTABILIDAD')
            BEGIN
                INSERT INTO seguridad.rol(codigo_rol,nombre_rol,descripcion,activo,fecha_registro)
                VALUES(N'CONTABILIDAD', N'Contabilidad', N'Administra catalogo contable, asientos y reporteria financiera.', 1, SYSDATETIME());
            END;
        """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static bool CanMaintainAccounting(CreditPortfolioSession session)
    {
        return session.HasAnyRole("ADMINISTRADOR", "ADMINISTRACION", "CONTABILIDAD");
    }

    private static string NormalizeCode(string? value)
    {
        return Regex.Replace((value ?? string.Empty).Trim(), @"\D+", string.Empty);
    }

    private static string NormalizeStatus(string? value)
    {
        var normalized = (value ?? "TODOS").Trim().ToUpperInvariant();
        return normalized is "ACTIVA" or "INACTIVA" ? normalized : "TODOS";
    }

    private static int InferClass(string code)
    {
        return int.TryParse(code[..Math.Min(1, code.Length)], out var value) ? value : 1;
    }

    private static int InferGroup(string code)
    {
        return int.TryParse(code[..Math.Min(2, code.Length)], out var value) ? value : InferClass(code);
    }

    private static string NormalizeNature(string? value, int classCode)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized is "D" or "A")
        {
            return normalized;
        }

        return classCode is 1 or 5 or 8 ? "D" : "A";
    }

    private static string CleanText(string? value, int maxLength)
    {
        var clean = Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ");
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private static string ClassName(int classCode)
    {
        return classCode switch
        {
            1 => "ACTIVO",
            2 => "PASIVO",
            3 => "PATRIMONIO",
            4 => "INGRESOS",
            5 => "GASTOS",
            6 => "CUENTAS DE ORDEN",
            7 => "CUENTAS DE ORDEN",
            8 => "CUENTAS DE ORDEN DEUDORAS",
            9 => "CUENTAS DE ORDEN ACREEDORAS",
            _ => "OTRAS",
        };
    }

    private static string ReadString(SqlDataReader reader, string name, string fallback = "")
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? fallback : Convert.ToString(reader.GetValue(ordinal)) ?? fallback;
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

    private static long ReadInt64(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
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
        return reader.IsDBNull(ordinal) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeNullable(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

public sealed class AccountSaveRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int? ClassCode { get; set; }
    public int? Group { get; set; }
    public int? Level { get; set; }
    public string? Nature { get; set; }
    public string? Level1 { get; set; }
    public string? Level2 { get; set; }
    public string? Level3 { get; set; }
    public bool? MovementAllowed { get; set; }
    public bool? Active { get; set; }
}

public sealed class AccountStatusRequest
{
    public string? Code { get; set; }
    public bool Active { get; set; }
}
