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
            EnsureFiscalDgiSchema(connection);
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
                    fiscalBooks = new[]
                    {
                        new { value = "COMPRAS_IVA", label = "Compras / credito fiscal IVA" },
                        new { value = "INGRESOS", label = "Ingresos mensuales" },
                        new { value = "RETENCIONES", label = "Retenciones en la fuente" },
                    },
                    fiscalDocumentTypes = new[]
                    {
                        new { value = "FACTURA", label = "Factura" },
                        new { value = "RECIBO", label = "Recibo" },
                        new { value = "NOTA_CREDITO", label = "Nota credito" },
                        new { value = "COMPROBANTE", label = "Comprobante" },
                        new { value = "OTRO", label = "Otro" },
                    },
                    fiscalStatuses = new[]
                    {
                        new { value = "BORRADOR", label = "Borrador" },
                        new { value = "VALIDADO", label = "Validado" },
                        new { value = "REPORTADO_DGI", label = "Reportado DGI" },
                    },
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
            EnsureFiscalDgiSchema(connection);
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
                    (SELECT COUNT(1) FROM contabilidad.documento_fiscal_dgi WHERE anulado = 0 AND periodo = FORMAT(@fecha, N'yyyy-MM')) AS documentos_dgi,
                    (SELECT ISNULL(SUM(monto_iva_trasladado), 0) FROM contabilidad.documento_fiscal_dgi WHERE anulado = 0 AND periodo = FORMAT(@fecha, N'yyyy-MM')) AS iva_dgi,
                    (SELECT ISNULL(SUM(valor_retenido), 0) FROM contabilidad.documento_fiscal_dgi WHERE anulado = 0 AND periodo = FORMAT(@fecha, N'yyyy-MM')) AS retenciones_dgi,
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
                    fiscalDocuments = ReadInt32(reader, "documentos_dgi"),
                    fiscalIva = ReadDecimal(reader, "iva_dgi"),
                    fiscalRetentions = ReadDecimal(reader, "retenciones_dgi"),
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

    [HttpGet]
    public IActionResult ListarDocumentosFiscales(string? periodo, string? libro, string? search)
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
            EnsureFiscalDgiSchema(connection);
            var normalizedPeriod = NormalizePeriod(periodo);
            var normalizedBook = NormalizeFiscalBook(libro);
            using var command = new SqlCommand(
                """
                SELECT TOP (300)
                    id_documento_fiscal,
                    periodo,
                    libro_fiscal,
                    tipo_documento,
                    numero_documento,
                    fecha_documento,
                    ruc,
                    razon_social,
                    descripcion_pago,
                    ingreso_sin_iva,
                    monto_iva_trasladado,
                    codigo_renglon,
                    ingresos_gravados_15,
                    ingresos_gravados_7,
                    ingresos_exentos,
                    ingresos_exonerados,
                    ingresos_brutos_mensuales,
                    valor_cotizacion_inss,
                    valor_fondo_pension_ahorro,
                    base_imponible,
                    valor_retenido,
                    alicuota_retencion,
                    codigo_retencion,
                    codigo_cuenta,
                    estado_documento,
                    fecha_registro,
                    usuario_registro
                FROM contabilidad.documento_fiscal_dgi
                WHERE anulado = 0
                  AND periodo = @periodo
                  AND (@libro = N'TODOS' OR libro_fiscal = @libro)
                  AND (@buscar = N''
                    OR ISNULL(numero_documento, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(ruc, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(razon_social, N'') LIKE N'%' + @buscar + N'%'
                    OR ISNULL(descripcion_pago, N'') LIKE N'%' + @buscar + N'%')
                ORDER BY fecha_documento DESC, id_documento_fiscal DESC;
                """,
                connection);
            command.Parameters.Add("@periodo", SqlDbType.Char, 7).Value = normalizedPeriod;
            command.Parameters.Add("@libro", SqlDbType.NVarChar, 30).Value = normalizedBook;
            command.Parameters.Add("@buscar", SqlDbType.NVarChar, 180).Value = (search ?? string.Empty).Trim();

            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(MapFiscalDocument(reader));
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar los documentos fiscales DGI.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult GuardarDocumentoFiscal([FromBody] FiscalDocumentSaveRequest request)
    {
        var errors = ValidateFiscalDocument(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "Revisa la fila fiscal antes de guardarla.", errors });
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
            EnsureFiscalDgiSchema(connection);
            using var command = new SqlCommand(
                """
                MERGE contabilidad.documento_fiscal_dgi AS target
                USING (SELECT @id_documento_fiscal AS id_documento_fiscal) AS source
                ON target.id_documento_fiscal = source.id_documento_fiscal AND @id_documento_fiscal > 0
                WHEN MATCHED THEN
                    UPDATE SET
                        periodo = @periodo,
                        libro_fiscal = @libro_fiscal,
                        tipo_documento = @tipo_documento,
                        numero_documento = @numero_documento,
                        fecha_documento = @fecha_documento,
                        ruc = @ruc,
                        razon_social = @razon_social,
                        descripcion_pago = @descripcion_pago,
                        ingreso_sin_iva = @ingreso_sin_iva,
                        monto_iva_trasladado = @monto_iva_trasladado,
                        codigo_renglon = @codigo_renglon,
                        ingresos_gravados_15 = @ingresos_gravados_15,
                        ingresos_gravados_7 = @ingresos_gravados_7,
                        ingresos_exentos = @ingresos_exentos,
                        ingresos_exonerados = @ingresos_exonerados,
                        ingresos_brutos_mensuales = @ingresos_brutos_mensuales,
                        valor_cotizacion_inss = @valor_cotizacion_inss,
                        valor_fondo_pension_ahorro = @valor_fondo_pension_ahorro,
                        base_imponible = @base_imponible,
                        valor_retenido = @valor_retenido,
                        alicuota_retencion = @alicuota_retencion,
                        codigo_retencion = @codigo_retencion,
                        codigo_cuenta = @codigo_cuenta,
                        estado_documento = @estado_documento,
                        usuario_modificacion = @usuario,
                        fecha_modificacion = SYSDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT
                    (
                        periodo, libro_fiscal, tipo_documento, numero_documento, fecha_documento, ruc, razon_social,
                        descripcion_pago, ingreso_sin_iva, monto_iva_trasladado, codigo_renglon,
                        ingresos_gravados_15, ingresos_gravados_7, ingresos_exentos, ingresos_exonerados,
                        ingresos_brutos_mensuales, valor_cotizacion_inss, valor_fondo_pension_ahorro,
                        base_imponible, valor_retenido, alicuota_retencion, codigo_retencion, codigo_cuenta,
                        estado_documento, usuario_registro
                    )
                    VALUES
                    (
                        @periodo, @libro_fiscal, @tipo_documento, @numero_documento, @fecha_documento, @ruc, @razon_social,
                        @descripcion_pago, @ingreso_sin_iva, @monto_iva_trasladado, @codigo_renglon,
                        @ingresos_gravados_15, @ingresos_gravados_7, @ingresos_exentos, @ingresos_exonerados,
                        @ingresos_brutos_mensuales, @valor_cotizacion_inss, @valor_fondo_pension_ahorro,
                        @base_imponible, @valor_retenido, @alicuota_retencion, @codigo_retencion, @codigo_cuenta,
                        @estado_documento, @usuario
                    )
                OUTPUT INSERTED.id_documento_fiscal;
                """,
                connection);
            AddFiscalDocumentParameters(command, request, session.Username);
            var id = Convert.ToInt64(command.ExecuteScalar());
            return Json(new { ok = true, message = "Fila fiscal DGI guardada.", data = new { id } });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo guardar la fila fiscal DGI.", detail = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult AnularDocumentoFiscal([FromBody] FiscalDocumentStatusRequest request)
    {
        if (request.Id <= 0)
        {
            return BadRequest(new { ok = false, message = "Selecciona una fila fiscal." });
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
            EnsureFiscalDgiSchema(connection);
            using var command = new SqlCommand(
                """
                UPDATE contabilidad.documento_fiscal_dgi
                SET anulado = 1,
                    estado_documento = N'ANULADO',
                    usuario_modificacion = @usuario,
                    fecha_modificacion = SYSDATETIME()
                WHERE id_documento_fiscal = @id_documento_fiscal;
                """,
                connection);
            command.Parameters.Add("@id_documento_fiscal", SqlDbType.BigInt).Value = request.Id;
            command.Parameters.Add("@usuario", SqlDbType.NVarChar, 120).Value = session.Username;
            var affected = command.ExecuteNonQuery();
            return affected == 0
                ? NotFound(new { ok = false, message = "Fila fiscal no encontrada." })
                : Json(new { ok = true, message = "Fila fiscal anulada." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo anular la fila fiscal.", detail = ex.Message });
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

    private static object MapFiscalDocument(SqlDataReader reader)
    {
        return new
        {
            id = ReadInt64(reader, "id_documento_fiscal"),
            period = ReadString(reader, "periodo"),
            book = ReadString(reader, "libro_fiscal"),
            documentType = ReadString(reader, "tipo_documento"),
            documentNumber = ReadString(reader, "numero_documento"),
            documentDate = ReadDateTimeNullable(reader, "fecha_documento"),
            ruc = ReadString(reader, "ruc"),
            name = ReadString(reader, "razon_social"),
            description = ReadString(reader, "descripcion_pago"),
            incomeWithoutIva = ReadDecimal(reader, "ingreso_sin_iva"),
            ivaAmount = ReadDecimal(reader, "monto_iva_trasladado"),
            rowCode = ReadString(reader, "codigo_renglon"),
            taxable15 = ReadDecimal(reader, "ingresos_gravados_15"),
            taxable7 = ReadDecimal(reader, "ingresos_gravados_7"),
            exempt = ReadDecimal(reader, "ingresos_exentos"),
            exonerated = ReadDecimal(reader, "ingresos_exonerados"),
            monthlyGrossIncome = ReadDecimal(reader, "ingresos_brutos_mensuales"),
            inssContribution = ReadDecimal(reader, "valor_cotizacion_inss"),
            pensionFund = ReadDecimal(reader, "valor_fondo_pension_ahorro"),
            taxableBase = ReadDecimal(reader, "base_imponible"),
            retainedAmount = ReadDecimal(reader, "valor_retenido"),
            retentionRate = ReadDecimal(reader, "alicuota_retencion"),
            retentionCode = ReadString(reader, "codigo_retencion"),
            accountCode = ReadString(reader, "codigo_cuenta"),
            status = ReadString(reader, "estado_documento"),
            registeredAt = ReadDateTimeNullable(reader, "fecha_registro"),
            registeredBy = ReadString(reader, "usuario_registro"),
        };
    }

    private static Dictionary<string, string> ValidateFiscalDocument(FiscalDocumentSaveRequest request)
    {
        var errors = new Dictionary<string, string>();
        if (!IsValidPeriod(request.Period))
        {
            errors["period"] = "El periodo debe tener formato AAAA-MM.";
        }

        if (NormalizeFiscalBook(request.Book) == "TODOS")
        {
            errors["book"] = "Selecciona libro fiscal: compras IVA, ingresos o retenciones.";
        }

        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            errors["documentNumber"] = "El numero de documento es obligatorio.";
        }

        if (request.DocumentDate is null)
        {
            errors["documentDate"] = "La fecha del documento es obligatoria.";
        }

        if (string.IsNullOrWhiteSpace(request.Ruc))
        {
            errors["ruc"] = "El RUC es obligatorio para planillas DGI.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = "El nombre o razon social es obligatorio.";
        }

        return errors;
    }

    private static void AddFiscalDocumentParameters(SqlCommand command, FiscalDocumentSaveRequest request, string username)
    {
        command.Parameters.Add("@id_documento_fiscal", SqlDbType.BigInt).Value = request.Id.GetValueOrDefault();
        command.Parameters.Add("@periodo", SqlDbType.Char, 7).Value = NormalizePeriod(request.Period);
        command.Parameters.Add("@libro_fiscal", SqlDbType.NVarChar, 30).Value = NormalizeFiscalBook(request.Book);
        command.Parameters.Add("@tipo_documento", SqlDbType.NVarChar, 40).Value = NormalizeDocumentType(request.DocumentType);
        command.Parameters.Add("@numero_documento", SqlDbType.NVarChar, 60).Value = CleanText(request.DocumentNumber, 60);
        command.Parameters.Add("@fecha_documento", SqlDbType.Date).Value = request.DocumentDate?.Date ?? DateTime.Today;
        command.Parameters.Add("@ruc", SqlDbType.NVarChar, 30).Value = CleanText(request.Ruc, 30).ToUpperInvariant();
        command.Parameters.Add("@razon_social", SqlDbType.NVarChar, 250).Value = CleanText(request.Name, 250).ToUpperInvariant();
        command.Parameters.Add("@descripcion_pago", SqlDbType.NVarChar, 500).Value = CleanText(request.Description, 500);
        command.Parameters.Add("@ingreso_sin_iva", SqlDbType.Decimal).Value = MoneyValue(request.IncomeWithoutIva);
        command.Parameters.Add("@monto_iva_trasladado", SqlDbType.Decimal).Value = MoneyValue(request.IvaAmount);
        command.Parameters.Add("@codigo_renglon", SqlDbType.NVarChar, 20).Value = CleanText(request.RowCode, 20);
        command.Parameters.Add("@ingresos_gravados_15", SqlDbType.Decimal).Value = MoneyValue(request.Taxable15);
        command.Parameters.Add("@ingresos_gravados_7", SqlDbType.Decimal).Value = MoneyValue(request.Taxable7);
        command.Parameters.Add("@ingresos_exentos", SqlDbType.Decimal).Value = MoneyValue(request.Exempt);
        command.Parameters.Add("@ingresos_exonerados", SqlDbType.Decimal).Value = MoneyValue(request.Exonerated);
        command.Parameters.Add("@ingresos_brutos_mensuales", SqlDbType.Decimal).Value = MoneyValue(request.MonthlyGrossIncome);
        command.Parameters.Add("@valor_cotizacion_inss", SqlDbType.Decimal).Value = MoneyValue(request.InssContribution);
        command.Parameters.Add("@valor_fondo_pension_ahorro", SqlDbType.Decimal).Value = MoneyValue(request.PensionFund);
        command.Parameters.Add("@base_imponible", SqlDbType.Decimal).Value = MoneyValue(request.TaxableBase);
        command.Parameters.Add("@valor_retenido", SqlDbType.Decimal).Value = MoneyValue(request.RetainedAmount);
        command.Parameters.Add("@alicuota_retencion", SqlDbType.Decimal).Value = MoneyValue(request.RetentionRate);
        command.Parameters.Add("@codigo_retencion", SqlDbType.NVarChar, 20).Value = CleanText(request.RetentionCode, 20);
        command.Parameters.Add("@codigo_cuenta", SqlDbType.NVarChar, 30).Value = NormalizeCode(request.AccountCode);
        command.Parameters.Add("@estado_documento", SqlDbType.NVarChar, 30).Value = NormalizeFiscalStatus(request.Status);
        command.Parameters.Add("@usuario", SqlDbType.NVarChar, 120).Value = string.IsNullOrWhiteSpace(username) ? "sistema" : username.Trim();
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

    private static void EnsureFiscalDgiSchema(SqlConnection connection)
    {
        const string sql = """
            IF OBJECT_ID(N'contabilidad.documento_fiscal_dgi', N'U') IS NULL
            BEGIN
                CREATE TABLE contabilidad.documento_fiscal_dgi
                (
                    id_documento_fiscal BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_documento_fiscal_dgi PRIMARY KEY,
                    periodo CHAR(7) NOT NULL,
                    libro_fiscal NVARCHAR(30) NOT NULL,
                    tipo_documento NVARCHAR(40) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_tipo DEFAULT N'FACTURA',
                    numero_documento NVARCHAR(60) NOT NULL,
                    fecha_documento DATE NOT NULL,
                    ruc NVARCHAR(30) NOT NULL,
                    razon_social NVARCHAR(250) NOT NULL,
                    descripcion_pago NVARCHAR(500) NULL,
                    ingreso_sin_iva DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_ingreso_sin_iva DEFAULT 0,
                    monto_iva_trasladado DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_iva DEFAULT 0,
                    codigo_renglon NVARCHAR(20) NULL,
                    ingresos_gravados_15 DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_grav15 DEFAULT 0,
                    ingresos_gravados_7 DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_grav7 DEFAULT 0,
                    ingresos_exentos DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_exento DEFAULT 0,
                    ingresos_exonerados DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_exonerado DEFAULT 0,
                    ingresos_brutos_mensuales DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_bruto DEFAULT 0,
                    valor_cotizacion_inss DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_inss DEFAULT 0,
                    valor_fondo_pension_ahorro DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_pension DEFAULT 0,
                    base_imponible DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_base DEFAULT 0,
                    valor_retenido DECIMAL(18,2) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_retenido DEFAULT 0,
                    alicuota_retencion DECIMAL(9,4) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_alicuota DEFAULT 0,
                    codigo_retencion NVARCHAR(20) NULL,
                    codigo_cuenta NVARCHAR(30) NULL,
                    estado_documento NVARCHAR(30) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_estado DEFAULT N'BORRADOR',
                    anulado BIT NOT NULL CONSTRAINT DF_documento_fiscal_dgi_anulado DEFAULT 0,
                    usuario_registro NVARCHAR(120) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_usuario DEFAULT N'sistema',
                    fecha_registro DATETIME2(0) NOT NULL CONSTRAINT DF_documento_fiscal_dgi_fecha DEFAULT SYSDATETIME(),
                    usuario_modificacion NVARCHAR(120) NULL,
                    fecha_modificacion DATETIME2(0) NULL,
                    CONSTRAINT CK_documento_fiscal_dgi_libro CHECK (libro_fiscal IN (N'COMPRAS_IVA', N'INGRESOS', N'RETENCIONES')),
                    CONSTRAINT CK_documento_fiscal_dgi_estado CHECK (estado_documento IN (N'BORRADOR', N'VALIDADO', N'REPORTADO_DGI', N'ANULADO'))
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_documento_fiscal_dgi_periodo_libro' AND object_id = OBJECT_ID(N'contabilidad.documento_fiscal_dgi'))
            BEGIN
                CREATE INDEX IX_documento_fiscal_dgi_periodo_libro
                ON contabilidad.documento_fiscal_dgi(periodo, libro_fiscal, anulado, fecha_documento)
                INCLUDE(numero_documento, ruc, razon_social, monto_iva_trasladado, valor_retenido);
            END;

            IF NOT EXISTS (SELECT 1 FROM contabilidad.documento_fiscal_dgi WHERE numero_documento = N'DGI-DEMO-IVA-001' AND periodo = FORMAT(SYSDATETIME(), N'yyyy-MM'))
            BEGIN
                INSERT INTO contabilidad.documento_fiscal_dgi
                (
                    periodo, libro_fiscal, tipo_documento, numero_documento, fecha_documento, ruc, razon_social,
                    descripcion_pago, ingreso_sin_iva, monto_iva_trasladado, codigo_renglon, codigo_cuenta,
                    estado_documento, usuario_registro
                )
                VALUES
                (
                    FORMAT(SYSDATETIME(), N'yyyy-MM'), N'COMPRAS_IVA', N'FACTURA', N'DGI-DEMO-IVA-001', CONVERT(date, SYSDATETIME()),
                    N'J0310000001812', N'PROVEEDOR DEMO DGI', N'Compra operativa con credito fiscal IVA para validar la planilla 124.',
                    10000, 1500, N'124', N'51010101', N'BORRADOR', N'sistema'
                );
            END;
        """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static bool CanMaintainAccounting(CreditPortfolioSession session)
    {
        return session.HasAnyRole("ADMINISTRADOR", "ADMINISTRACION", "CONTABILIDAD");
    }

    private static string NormalizePeriod(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (IsValidPeriod(text))
        {
            return text;
        }

        return DateTime.Today.ToString("yyyy-MM");
    }

    private static bool IsValidPeriod(string? value)
    {
        return Regex.IsMatch((value ?? string.Empty).Trim(), @"^\d{4}-(0[1-9]|1[0-2])$");
    }

    private static string NormalizeFiscalBook(string? value)
    {
        var normalized = (value ?? "TODOS").Trim().ToUpperInvariant();
        return normalized is "COMPRAS_IVA" or "INGRESOS" or "RETENCIONES" ? normalized : "TODOS";
    }

    private static string NormalizeDocumentType(string? value)
    {
        var normalized = (value ?? "FACTURA").Trim().ToUpperInvariant();
        return normalized is "FACTURA" or "RECIBO" or "NOTA_CREDITO" or "COMPROBANTE" or "OTRO" ? normalized : "FACTURA";
    }

    private static string NormalizeFiscalStatus(string? value)
    {
        var normalized = (value ?? "BORRADOR").Trim().ToUpperInvariant();
        return normalized is "VALIDADO" or "REPORTADO_DGI" ? normalized : "BORRADOR";
    }

    private static decimal MoneyValue(decimal? value)
    {
        return Math.Round(Math.Max(value.GetValueOrDefault(), 0), 2);
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

public sealed class FiscalDocumentSaveRequest
{
    public long? Id { get; set; }
    public string? Period { get; set; }
    public string? Book { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? Ruc { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? IncomeWithoutIva { get; set; }
    public decimal? IvaAmount { get; set; }
    public string? RowCode { get; set; }
    public decimal? Taxable15 { get; set; }
    public decimal? Taxable7 { get; set; }
    public decimal? Exempt { get; set; }
    public decimal? Exonerated { get; set; }
    public decimal? MonthlyGrossIncome { get; set; }
    public decimal? InssContribution { get; set; }
    public decimal? PensionFund { get; set; }
    public decimal? TaxableBase { get; set; }
    public decimal? RetainedAmount { get; set; }
    public decimal? RetentionRate { get; set; }
    public string? RetentionCode { get; set; }
    public string? AccountCode { get; set; }
    public string? Status { get; set; }
}

public sealed class FiscalDocumentStatusRequest
{
    public long Id { get; set; }
}
