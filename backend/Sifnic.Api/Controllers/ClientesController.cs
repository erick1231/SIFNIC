using System.Data;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Creditos;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class ClientesController : Controller
{
    private readonly IWebHostEnvironment _environment;

    public ClientesController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    private static readonly Regex NicaraguanIdPattern = new(@"^\d{13}[A-Z0-9]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);
            var conamiRules = ConamiRulesSupport.LoadActiveRuleMap(connection);

            return Json(new
            {
                ok = true,
                data = new
                {
                    identificationTypes = new[] { "CEDULA", "RUC", "PASAPORTE", "RESIDENCIA" },
                    clientTypes = new[] { "INDIVIDUAL", "GRUPO_SOLIDARIO", "JURIDICO" },
                    statuses = CreditOperationsSupport.ClientStatuses,
                    relations = new[] { "NUEVO", "RECURRENTE", "GRUPO", "FIADOR", "PROSPECTO" },
                    genders = new[] { "MASCULINO", "FEMENINO", "NO_APLICA" },
                    civilStatuses = new[] { "SOLTERO", "CASADO", "UNION_HECHO", "DIVORCIADO", "VIUDO", "NO_APLICA" },
                    riskLevels = CreditOperationsSupport.RiskLevels,
                    expedienteStatuses = new[] { "INCOMPLETO", "COMPLETO", "VENCIDO", "EN_REVISION" },
                    branches = new[] { "CASA MATRIZ", "MANAGUA", "MASAYA", "GRANADA", "LEON" },
                    conamiRules,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar los catalogos de clientes.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Listar(string? search, string? status, string? type)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            const string sql = """
                SELECT
                    c.id_cliente,
                    c.cedula,
                    c.nombres,
                    c.apellidos,
                    c.fecha_nacimiento,
                    c.telefono,
                    c.correo,
                    c.direccion,
                    c.tipo_cliente,
                    c.activo,
                    c.fecha_creacion,
                    c.tipo_identificacion,
                    c.sucursal,
                    c.relacion_cliente,
                    c.estado_cliente,
                    c.fecha_ingreso,
                    c.genero,
                    c.estado_civil,
                    c.nombre_conyuge,
                    c.telefono_secundario,
                    c.celular,
                    c.geografia_casa,
                    c.ocupacion,
                    c.actividad_economica,
                    c.nombre_negocio,
                    c.direccion_negocio,
                    c.geografia_negocio,
                    c.antiguedad_negocio_meses,
                    c.ingresos_mensuales,
                    c.ingresos_conyuge,
                    c.remesas,
                    c.alquileres,
                    c.otros_ingresos,
                    c.egresos_mensuales,
                    c.origen_fondos,
                    c.proposito_relacion,
                    c.pep,
                    c.nivel_riesgo,
                    c.puntaje_riesgo,
                    c.estado_expediente,
                    c.observaciones,
                    c.usuario_registro,
                    c.fecha_actualizacion,
                    (SELECT COUNT(1) FROM creditos.solicitud_credito s WHERE s.id_cliente = c.id_cliente) AS total_solicitudes,
                    (SELECT COUNT(1) FROM creditos.credito cr WHERE cr.id_cliente = c.id_cliente) AS total_creditos,
                    (SELECT ISNULL(SUM(cr.saldo_capital), 0) FROM creditos.credito cr WHERE cr.id_cliente = c.id_cliente AND cr.activo = 1) AS saldo_capital
                FROM clientes.cliente c
                WHERE
                    (
                        @search = N''
                        OR c.cedula LIKE N'%' + @search + N'%'
                        OR c.nombres LIKE N'%' + @search + N'%'
                        OR c.apellidos LIKE N'%' + @search + N'%'
                        OR (c.nombres + N' ' + c.apellidos) LIKE N'%' + @search + N'%'
                        OR ISNULL(c.telefono, N'') LIKE N'%' + @search + N'%'
                        OR ISNULL(c.celular, N'') LIKE N'%' + @search + N'%'
                    )
                    AND (@status = N'TODOS' OR c.estado_cliente = @status)
                    AND (@type = N'TODOS' OR c.tipo_cliente = @type)
                ORDER BY c.id_cliente DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeStatus(status);
            command.Parameters.Add("@type", SqlDbType.NVarChar, 30).Value = NormalizeType(type);

            using var reader = command.ExecuteReader();
            var items = new List<ClientDto>();
            while (reader.Read())
            {
                items.Add(MapClient(reader));
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los clientes.",
                detail = ex.Message,
            });
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

            var client = GetClient(connection, id);
            if (client is null)
            {
                return NotFound(new { ok = false, message = "Cliente no encontrado." });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    client,
                    applications = GetClientApplications(connection, id),
                    loans = GetClientLoans(connection, id),
                    deletionRequests = GetClientDeletionRequests(connection, id),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo obtener la informacion del cliente.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult PrestamoDetalle(long id)
    {
        if (id <= 0)
        {
            return BadRequest(new { ok = false, message = "Prestamo invalido." });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var detail = LoadLoanDetail(connection, id);
            if (detail is null)
            {
                return NotFound(new { ok = false, message = "Prestamo no encontrado." });
            }

            return Json(new { ok = true, data = detail });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudo cargar el detalle del prestamo.", detail = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult EstadoCuentaPrestamoHtml(long id)
    {
        return LoanPrintable(id, "estado");
    }

    [HttpGet]
    public IActionResult PlanPagoPrestamoHtml(long id)
    {
        return LoanPrintable(id, "plan");
    }

    private IActionResult LoanPrintable(long id, string mode)
    {
        if (id <= 0)
        {
            return BadRequest("Prestamo invalido.");
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var detail = LoadLoanDetail(connection, id);
            if (detail is null)
            {
                return NotFound("Prestamo no encontrado.");
            }

            return Content(BuildLoanPrintableHtml(detail, mode), "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"No se pudo imprimir el prestamo: {WebUtility.HtmlEncode(ex.Message)}");
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] ClientSaveModel model)
    {
        var errors = ValidateClient(model, isUpdate: false);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "Corrige los datos del cliente.", errors });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var duplicate = FindDuplicateId(connection, model.Cedula, null);
            if (duplicate.HasValue)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Ya existe un cliente con esa identificacion.",
                    errors = new { cedula = "La identificacion ya esta registrada." },
                });
            }

            using var transaction = connection.BeginTransaction();
            long id;

            using (var command = new SqlCommand(BuildInsertSql(), connection, transaction))
            {
                AddClientParameters(command, model);
                command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value =
                    CreditOperationsSupport.GetOperatorUser(Request);
                id = Convert.ToInt64(command.ExecuteScalar());
            }

            var client = GetClient(connection, id, transaction)!;
            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CLIENTES",
                "CLIENTE",
                "CREACION",
                id,
                client.Cedula,
                $"Se creo el cliente {client.FullName}.",
                client);

            transaction.Commit();

            return Json(new { ok = true, message = "Cliente creado correctamente.", data = client });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo crear el cliente."),
                detail = ex.Message,
            });
        }
    }

    /// <summary>
    /// Sube una foto u otro archivo del expediente del cliente (app móvil). Persiste en disco y en <c>clientes.archivo_movil</c>.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public IActionResult SubirArchivoMovil([FromForm] long idCliente, [FromForm] string? tipoDocumento, IFormFile? archivo)
    {
        var tipo = string.IsNullOrWhiteSpace(tipoDocumento) ? "DOCUMENTO" : tipoDocumento.Trim();
        if (idCliente <= 0)
        {
            return BadRequest(new { ok = false, message = "Cliente invalido." });
        }

        if (archivo is null || archivo.Length == 0)
        {
            return BadRequest(new { ok = false, message = "Selecciona un archivo." });
        }

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf"))
        {
            return BadRequest(new { ok = false, message = "Formato no permitido. Usa foto JPG/PNG/WebP o PDF." });
        }

        if (archivo.Length > 12 * 1024 * 1024)
        {
            return BadRequest(new { ok = false, message = "El archivo supera 12 MB." });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);
            CreditOperationsSupport.EnsureClienteArchivoMovilSchema(connection);

            using var existsCmd = new SqlCommand(
                """
                SELECT COUNT(1)
                FROM clientes.cliente
                WHERE id_cliente = @id AND activo = 1;
                """,
                connection);
            existsCmd.Parameters.Add("@id", SqlDbType.BigInt).Value = idCliente;
            if (Convert.ToInt32(existsCmd.ExecuteScalar()) == 0)
            {
                return NotFound(new { ok = false, message = "Cliente no encontrado." });
            }

            var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                : _environment.WebRootPath;

            var folderRel = Path.Combine("uploads", "clientes", "movil", idCliente.ToString(CultureInfo.InvariantCulture));
            var folderAbs = Path.Combine(webRoot, folderRel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folderAbs);

            var storedName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(folderAbs, storedName);
            using (var stream = System.IO.File.Create(physicalPath))
            {
                archivo.CopyTo(stream);
            }

            var relativePath = "/" + Path.Combine(folderRel, storedName).Replace("\\", "/");
            var operatorUser = CreditOperationsSupport.GetOperatorUser(Request);

            using var insert = new SqlCommand(
                """
                INSERT INTO clientes.archivo_movil
                    (id_cliente, tipo_documento, nombre_archivo, ruta_relativa, usuario_registro)
                OUTPUT INSERTED.id_archivo_movil
                VALUES
                    (@id_cliente, @tipo_documento, @nombre_archivo, @ruta_relativa, @usuario_registro);
                """,
                connection);
            insert.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = idCliente;
            insert.Parameters.Add("@tipo_documento", SqlDbType.NVarChar, 80).Value = tipo.Length > 80 ? tipo[..80] : tipo;
            insert.Parameters.Add("@nombre_archivo", SqlDbType.NVarChar, 255).Value = Path.GetFileName(archivo.FileName);
            insert.Parameters.Add("@ruta_relativa", SqlDbType.NVarChar, 500).Value = relativePath;
            insert.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 200).Value = operatorUser;

            var newId = Convert.ToInt64(insert.ExecuteScalar());

            using var updateExp = new SqlCommand(
                """
                UPDATE clientes.cliente
                SET estado_expediente = CASE
                        WHEN estado_expediente IN (N'INCOMPLETO', N'VENCIDO') THEN N'EN_REVISION'
                        ELSE estado_expediente
                    END,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_cliente = @id;
                """,
                connection);
            updateExp.Parameters.Add("@id", SqlDbType.BigInt).Value = idCliente;
            updateExp.ExecuteNonQuery();

            CreditOperationsSupport.RegisterBitacora(
                connection,
                null,
                HttpContext,
                "CLIENTES",
                "EXPEDIENTE_MOVIL",
                "CARGA",
                newId,
                tipo,
                $"Archivo movil cargado para cliente {idCliente}.",
                new { idCliente, tipo, relativePath });

            return Json(new
            {
                ok = true,
                message = "Archivo registrado correctamente.",
                data = new { id = newId, relativePath, tipoDocumento = tipo },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo guardar el archivo.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Actualizar(long id, [FromBody] ClientSaveModel model)
    {
        var errors = ValidateClient(model, isUpdate: true);
        if (errors.Count > 0)
        {
            return BadRequest(new { ok = false, message = "Corrige los datos del cliente.", errors });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var current = GetClient(connection, id);
            if (current is null)
            {
                return NotFound(new { ok = false, message = "Cliente no encontrado." });
            }

            var duplicate = FindDuplicateId(connection, model.Cedula, id);
            if (duplicate.HasValue)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Ya existe otro cliente con esa identificacion.",
                    errors = new { cedula = "La identificacion ya esta registrada en otro cliente." },
                });
            }

            using var transaction = connection.BeginTransaction();
            using (var command = new SqlCommand(BuildUpdateSql(), connection, transaction))
            {
                command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;
                AddClientParameters(command, model);
                command.ExecuteNonQuery();
            }

            var updated = GetClient(connection, id, transaction)!;
            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CLIENTES",
                "CLIENTE",
                "ACTUALIZACION",
                id,
                updated.Cedula,
                $"Se actualizo el cliente {updated.FullName}.",
                new { antes = current, despues = updated });

            transaction.Commit();
            return Json(new { ok = true, message = "Cliente actualizado correctamente.", data = updated });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo actualizar el cliente."),
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult SolicitarEliminacion(long id, [FromBody] ClientDeleteRequestModel model)
    {
        model ??= new ClientDeleteRequestModel();

        if (string.IsNullOrWhiteSpace(model.Reason) || model.Reason.Trim().Length < 12)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Indica un motivo de al menos 12 caracteres.",
                errors = new { reason = "El motivo es obligatorio." },
            });
        }

        if (string.IsNullOrWhiteSpace(model.AdminUser) || string.IsNullOrWhiteSpace(model.AdminPassword))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Para eliminar debes ingresar usuario y clave de administrador.",
                errors = new
                {
                    adminUser = "El usuario administrador es obligatorio.",
                    adminPassword = "La clave administrador es obligatoria.",
                },
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            var client = GetClient(connection, id);
            if (client is null)
            {
                return NotFound(new { ok = false, message = "Cliente no encontrado." });
            }

            var hasLoans = ClientHasActiveLoans(connection, id);
            var operatorUser = CreditOperationsSupport.GetOperatorUser(Request);
            var validation = RrhhSupport.ValidateAdministrator(
                connection,
                model.AdminUser,
                model.AdminPassword);

            if (!validation.Ok)
            {
                return Unauthorized(new { ok = false, message = validation.Message });
            }

            var adminUser = validation.UsuarioAdministrador ?? model.AdminUser.Trim();
            const bool authorized = true;

            using var transaction = connection.BeginTransaction();
            long requestId;
            const string state = "AUTORIZADA";

            using (var command = new SqlCommand(
                """
                INSERT INTO clientes.solicitud_eliminacion_cliente
                (
                    id_cliente,
                    motivo,
                    estado,
                    usuario_solicita,
                    fecha_solicitud,
                    usuario_autoriza,
                    fecha_autorizacion,
                    observacion_autorizacion
                )
                OUTPUT INSERTED.id_solicitud_eliminacion
                VALUES
                (
                    @id_cliente,
                    @motivo,
                    @estado,
                    @usuario_solicita,
                    SYSDATETIME(),
                    @usuario_autoriza,
                    CASE WHEN @estado = N'AUTORIZADA' THEN SYSDATETIME() ELSE NULL END,
                    @observacion_autorizacion
                );
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;
                command.Parameters.Add("@motivo", SqlDbType.NVarChar, 500).Value = model.Reason.Trim();
                command.Parameters.Add("@estado", SqlDbType.NVarChar, 30).Value = state;
                command.Parameters.Add("@usuario_solicita", SqlDbType.NVarChar, 100).Value = operatorUser;
                command.Parameters.Add("@usuario_autoriza", SqlDbType.NVarChar, 100).Value = adminUser;
                command.Parameters.Add("@observacion_autorizacion", SqlDbType.NVarChar, 500).Value =
                    "Autorizacion directa desde clientes.";
                requestId = Convert.ToInt64(command.ExecuteScalar());
            }

            if (authorized && !hasLoans)
            {
                using var deactivateCommand = new SqlCommand(
                    """
                    UPDATE clientes.cliente
                    SET
                        activo = 0,
                        estado_cliente = N'INACTIVO',
                        fecha_actualizacion = SYSDATETIME()
                    WHERE id_cliente = @id_cliente;
                    """,
                    connection,
                    transaction);
                deactivateCommand.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;
                deactivateCommand.ExecuteNonQuery();
            }

            CreditOperationsSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "CLIENTES",
                "ELIMINACION_CLIENTE",
                "AUTORIZACION",
                id,
                client.Cedula,
                authorized && !hasLoans
                    ? $"Se autorizo y desactivo el cliente {client.FullName}."
                    : $"Se autorizo solicitud de eliminacion para {client.FullName}; conserva prestamos activos.",
                new { requestId, model.Reason, authorized, hasLoans });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = authorized && !hasLoans
                    ? "El cliente fue desactivado con autorizacion."
                    : "La autorizacion fue registrada, pero el cliente conserva prestamos activos y no se desactivo.",
                data = new { requestId, state, hasLoans },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo procesar la eliminacion del cliente.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult EliminacionesPendientes()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            CreditOperationsSupport.EnsureSchema(connection);

            const string sql = """
                SELECT TOP (80)
                    se.id_solicitud_eliminacion,
                    se.id_cliente,
                    c.cedula,
                    c.nombres + N' ' + c.apellidos AS nombre_cliente,
                    se.motivo,
                    se.estado,
                    se.usuario_solicita,
                    se.fecha_solicitud,
                    se.usuario_autoriza,
                    se.fecha_autorizacion,
                    se.observacion_autorizacion
                FROM clientes.solicitud_eliminacion_cliente se
                INNER JOIN clientes.cliente c ON c.id_cliente = se.id_cliente
                ORDER BY se.id_solicitud_eliminacion DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            using var reader = command.ExecuteReader();
            var items = new List<object>();
            while (reader.Read())
            {
                items.Add(new
                {
                    id = reader.GetInt64(0),
                    clientId = reader.GetInt64(1),
                    identification = reader.GetString(2),
                    clientName = reader.GetString(3),
                    reason = reader.GetString(4),
                    state = reader.GetString(5),
                    requestedBy = reader.GetString(6),
                    requestedAt = reader.GetDateTime(7),
                    authorizedBy = reader.IsDBNull(8) ? null : reader.GetString(8),
                    authorizedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                    authorizationNote = reader.IsDBNull(10) ? null : reader.GetString(10),
                });
            }

            return Json(new { ok = true, data = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, message = "No se pudieron cargar las solicitudes de eliminacion.", detail = ex.Message });
        }
    }

    private static IReadOnlyDictionary<string, string> ValidateClient(ClientSaveModel model, bool isUpdate)
    {
        var errors = new Dictionary<string, string>();
        var idType = CreditOperationsSupport.NormalizeCode(model.IdentificationType, "CEDULA");
        var cedula = NormalizeIdentification(model.Cedula);

        if (string.IsNullOrWhiteSpace(model.Names))
        {
            errors["names"] = "Ingresa los nombres.";
        }

        if (string.IsNullOrWhiteSpace(model.LastNames))
        {
            errors["lastNames"] = "Ingresa los apellidos.";
        }

        if (string.IsNullOrWhiteSpace(cedula))
        {
            errors["cedula"] = "Ingresa la identificacion.";
        }
        else if (idType == "CEDULA" && !NicaraguanIdPattern.IsMatch(cedula))
        {
            errors["cedula"] = "La cedula debe tener 13 digitos y una letra o digito final.";
        }
        else if (idType != "CEDULA" && cedula.Length < 6)
        {
            errors["cedula"] = "La identificacion debe tener al menos 6 caracteres.";
        }

        if (model.BirthDate.HasValue && model.BirthDate.Value.Date > DateTime.Today.AddYears(-18))
        {
            errors["birthDate"] = "El cliente debe ser mayor de edad.";
        }
        else if (!model.BirthDate.HasValue &&
                 idType == "CEDULA" &&
                 InferBirthDateFromIdentification(cedula) is { } inferredBirthDate &&
                 inferredBirthDate > DateTime.Today.AddYears(-18))
        {
            errors["birthDate"] = "La fecha inferida desde la cedula indica que el cliente no es mayor de edad.";
        }

        var status = NormalizeStatus(model.Status);
        if (status != "TODOS" && !CreditOperationsSupport.ClientStatuses.Contains(status))
        {
            errors["status"] = "Selecciona un estado valido.";
        }

        if (model.Email?.Contains('@') == false)
        {
            errors["email"] = "Ingresa un correo valido.";
        }

        if (model.MonthlyIncome < 0 || model.MonthlyExpenses < 0 || model.OtherIncome < 0 ||
            model.SpouseIncome < 0 || model.Remittances < 0 || model.RentIncome < 0)
        {
            errors["financials"] = "Los montos no pueden ser negativos.";
        }

        if (model.RiskScore is < 0 or > 100)
        {
            errors["riskScore"] = "El puntaje de riesgo debe estar entre 0 y 100.";
        }

        if (model.BusinessAgeMonths < 0)
        {
            errors["businessAgeMonths"] = "La antiguedad no puede ser negativa.";
        }

        return errors;
    }

    private static string BuildInsertSql()
    {
        return """
            INSERT INTO clientes.cliente
            (
                cedula, nombres, apellidos, fecha_nacimiento, telefono, correo, direccion, tipo_cliente, activo,
                tipo_identificacion, sucursal, relacion_cliente, estado_cliente, fecha_ingreso, genero,
                estado_civil, nombre_conyuge, telefono_secundario, celular, geografia_casa, ocupacion,
                actividad_economica, nombre_negocio, direccion_negocio, geografia_negocio,
                antiguedad_negocio_meses, ingresos_mensuales, ingresos_conyuge, remesas, alquileres,
                otros_ingresos, egresos_mensuales, origen_fondos, proposito_relacion, pep, nivel_riesgo,
                puntaje_riesgo, estado_expediente, observaciones, usuario_registro
            )
            OUTPUT INSERTED.id_cliente
            VALUES
            (
                @cedula, @nombres, @apellidos, @fecha_nacimiento, @telefono, @correo, @direccion, @tipo_cliente, @activo,
                @tipo_identificacion, @sucursal, @relacion_cliente, @estado_cliente, @fecha_ingreso, @genero,
                @estado_civil, @nombre_conyuge, @telefono_secundario, @celular, @geografia_casa, @ocupacion,
                @actividad_economica, @nombre_negocio, @direccion_negocio, @geografia_negocio,
                @antiguedad_negocio_meses, @ingresos_mensuales, @ingresos_conyuge, @remesas, @alquileres,
                @otros_ingresos, @egresos_mensuales, @origen_fondos, @proposito_relacion, @pep, @nivel_riesgo,
                @puntaje_riesgo, @estado_expediente, @observaciones, @usuario_registro
            );
            """;
    }

    private static string BuildUpdateSql()
    {
        return """
            UPDATE clientes.cliente
            SET
                cedula = @cedula,
                nombres = @nombres,
                apellidos = @apellidos,
                fecha_nacimiento = @fecha_nacimiento,
                telefono = @telefono,
                correo = @correo,
                direccion = @direccion,
                tipo_cliente = @tipo_cliente,
                activo = @activo,
                tipo_identificacion = @tipo_identificacion,
                sucursal = @sucursal,
                relacion_cliente = @relacion_cliente,
                estado_cliente = @estado_cliente,
                fecha_ingreso = @fecha_ingreso,
                genero = @genero,
                estado_civil = @estado_civil,
                nombre_conyuge = @nombre_conyuge,
                telefono_secundario = @telefono_secundario,
                celular = @celular,
                geografia_casa = @geografia_casa,
                ocupacion = @ocupacion,
                actividad_economica = @actividad_economica,
                nombre_negocio = @nombre_negocio,
                direccion_negocio = @direccion_negocio,
                geografia_negocio = @geografia_negocio,
                antiguedad_negocio_meses = @antiguedad_negocio_meses,
                ingresos_mensuales = @ingresos_mensuales,
                ingresos_conyuge = @ingresos_conyuge,
                remesas = @remesas,
                alquileres = @alquileres,
                otros_ingresos = @otros_ingresos,
                egresos_mensuales = @egresos_mensuales,
                origen_fondos = @origen_fondos,
                proposito_relacion = @proposito_relacion,
                pep = @pep,
                nivel_riesgo = @nivel_riesgo,
                puntaje_riesgo = @puntaje_riesgo,
                estado_expediente = @estado_expediente,
                observaciones = @observaciones,
                fecha_actualizacion = SYSDATETIME()
            WHERE id_cliente = @id_cliente;
            """;
    }

    private static void AddClientParameters(SqlCommand command, ClientSaveModel model)
    {
        command.Parameters.Add("@cedula", SqlDbType.NVarChar, 50).Value = NormalizeIdentification(model.Cedula);
        command.Parameters.Add("@nombres", SqlDbType.NVarChar, 150).Value = model.Names.Trim();
        command.Parameters.Add("@apellidos", SqlDbType.NVarChar, 150).Value = model.LastNames.Trim();
        var status = NormalizeStatus(model.Status) == "TODOS" ? "ACTIVO" : NormalizeStatus(model.Status);
        var birthDate = model.BirthDate ?? InferBirthDateFromIdentification(NormalizeIdentification(model.Cedula));

        command.Parameters.Add("@fecha_nacimiento", SqlDbType.Date).Value = CreditOperationsSupport.DateOrDbNull(birthDate);
        command.Parameters.Add("@telefono", SqlDbType.NVarChar, 50).Value = CreditOperationsSupport.TextOrDbNull(model.Phone);
        command.Parameters.Add("@correo", SqlDbType.NVarChar, 150).Value = CreditOperationsSupport.TextOrDbNull(model.Email);
        command.Parameters.Add("@direccion", SqlDbType.NVarChar, 300).Value = CreditOperationsSupport.TextOrDbNull(model.Address);
        command.Parameters.Add("@tipo_cliente", SqlDbType.NVarChar, 30).Value = NormalizeType(model.ClientType) == "TODOS" ? "INDIVIDUAL" : NormalizeType(model.ClientType);
        command.Parameters.Add("@activo", SqlDbType.Bit).Value = status is "ACTIVO" or "PROSPECTO";
        command.Parameters.Add("@tipo_identificacion", SqlDbType.NVarChar, 30).Value = CreditOperationsSupport.NormalizeCode(model.IdentificationType, "CEDULA");
        command.Parameters.Add("@sucursal", SqlDbType.NVarChar, 100).Value = CreditOperationsSupport.TextOrDbNull(model.Branch);
        command.Parameters.Add("@relacion_cliente", SqlDbType.NVarChar, 50).Value = CreditOperationsSupport.TextOrDbNull(model.Relationship);
        command.Parameters.Add("@estado_cliente", SqlDbType.NVarChar, 30).Value = status;
        command.Parameters.Add("@fecha_ingreso", SqlDbType.Date).Value = model.EntryDate?.Date ?? DateTime.Today;
        command.Parameters.Add("@genero", SqlDbType.NVarChar, 20).Value = CreditOperationsSupport.TextOrDbNull(model.Gender);
        command.Parameters.Add("@estado_civil", SqlDbType.NVarChar, 30).Value = CreditOperationsSupport.TextOrDbNull(model.CivilStatus);
        command.Parameters.Add("@nombre_conyuge", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.SpouseName);
        command.Parameters.Add("@telefono_secundario", SqlDbType.NVarChar, 50).Value = CreditOperationsSupport.TextOrDbNull(model.SecondaryPhone);
        command.Parameters.Add("@celular", SqlDbType.NVarChar, 50).Value = CreditOperationsSupport.TextOrDbNull(model.Mobile);
        command.Parameters.Add("@geografia_casa", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.HomeGeography);
        command.Parameters.Add("@ocupacion", SqlDbType.NVarChar, 150).Value = CreditOperationsSupport.TextOrDbNull(model.Occupation);
        command.Parameters.Add("@actividad_economica", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.EconomicActivity);
        command.Parameters.Add("@nombre_negocio", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.BusinessName);
        command.Parameters.Add("@direccion_negocio", SqlDbType.NVarChar, 300).Value = CreditOperationsSupport.TextOrDbNull(model.BusinessAddress);
        command.Parameters.Add("@geografia_negocio", SqlDbType.NVarChar, 200).Value = CreditOperationsSupport.TextOrDbNull(model.BusinessGeography);
        command.Parameters.Add("@antiguedad_negocio_meses", SqlDbType.Int).Value = Math.Max(model.BusinessAgeMonths, 0);
        command.Parameters.Add("@ingresos_mensuales", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.MonthlyIncome);
        command.Parameters.Add("@ingresos_conyuge", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.SpouseIncome);
        command.Parameters.Add("@remesas", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.Remittances);
        command.Parameters.Add("@alquileres", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.RentIncome);
        command.Parameters.Add("@otros_ingresos", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.OtherIncome);
        command.Parameters.Add("@egresos_mensuales", SqlDbType.Decimal).Value = CreditOperationsSupport.SafeDecimal(model.MonthlyExpenses);
        command.Parameters.Add("@origen_fondos", SqlDbType.NVarChar, 250).Value = CreditOperationsSupport.TextOrDbNull(model.SourceOfFunds);
        command.Parameters.Add("@proposito_relacion", SqlDbType.NVarChar, 250).Value = CreditOperationsSupport.TextOrDbNull(model.RelationshipPurpose);
        command.Parameters.Add("@pep", SqlDbType.Bit).Value = model.IsPep;
        command.Parameters.Add("@nivel_riesgo", SqlDbType.NVarChar, 20).Value = NormalizeRisk(model.RiskLevel);
        command.Parameters.Add("@puntaje_riesgo", SqlDbType.Int).Value = Math.Clamp(model.RiskScore, 0, 100);
        command.Parameters.Add("@estado_expediente", SqlDbType.NVarChar, 30).Value = CreditOperationsSupport.NormalizeCode(model.FileStatus, "INCOMPLETO");
        command.Parameters.Add("@observaciones", SqlDbType.NVarChar, 1000).Value = CreditOperationsSupport.TextOrDbNull(model.Notes);
    }

    private static ClientDto? GetClient(SqlConnection connection, long id, SqlTransaction? transaction = null)
    {
        using var command = new SqlCommand(
            """
            SELECT
                c.id_cliente, c.cedula, c.nombres, c.apellidos, c.fecha_nacimiento, c.telefono,
                c.correo, c.direccion, c.tipo_cliente, c.activo, c.fecha_creacion, c.tipo_identificacion,
                c.sucursal, c.relacion_cliente, c.estado_cliente, c.fecha_ingreso, c.genero, c.estado_civil,
                c.nombre_conyuge, c.telefono_secundario, c.celular, c.geografia_casa, c.ocupacion,
                c.actividad_economica, c.nombre_negocio, c.direccion_negocio, c.geografia_negocio,
                c.antiguedad_negocio_meses, c.ingresos_mensuales, c.ingresos_conyuge, c.remesas, c.alquileres,
                c.otros_ingresos, c.egresos_mensuales, c.origen_fondos, c.proposito_relacion, c.pep,
                c.nivel_riesgo, c.puntaje_riesgo, c.estado_expediente, c.observaciones, c.usuario_registro,
                c.fecha_actualizacion,
                (SELECT COUNT(1) FROM creditos.solicitud_credito s WHERE s.id_cliente = c.id_cliente) AS total_solicitudes,
                (SELECT COUNT(1) FROM creditos.credito cr WHERE cr.id_cliente = c.id_cliente) AS total_creditos,
                (SELECT ISNULL(SUM(cr.saldo_capital), 0) FROM creditos.credito cr WHERE cr.id_cliente = c.id_cliente AND cr.activo = 1) AS saldo_capital
            FROM clientes.cliente c
            WHERE c.id_cliente = @id_cliente;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapClient(reader) : null;
    }

    private static List<object> GetClientApplications(SqlConnection connection, long id)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (50)
                id_solicitud_credito,
                numero_solicitud,
                fecha_solicitud,
                monto_solicitado,
                plazo_meses,
                tasa_interes_anual,
                moneda,
                destino_credito,
                estado_solicitud,
                producto_credito,
                frecuencia_pago,
                cuota_estimada,
                nivel_riesgo,
                clasificacion_conami
            FROM creditos.solicitud_credito
            WHERE id_cliente = @id_cliente
            ORDER BY id_solicitud_credito DESC;
            """,
            connection);
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                number = reader.GetString(1),
                requestDate = reader.GetDateTime(2),
                amount = reader.GetDecimal(3),
                termMonths = reader.GetInt32(4),
                annualRate = reader.GetDecimal(5),
                currency = reader.GetString(6),
                destination = reader.IsDBNull(7) ? null : reader.GetString(7),
                status = reader.GetString(8),
                product = reader.IsDBNull(9) ? null : reader.GetString(9),
                frequency = reader.GetString(10),
                estimatedInstallment = reader.GetDecimal(11),
                riskLevel = reader.GetString(12),
                conamiClassification = reader.GetString(13),
            });
        }

        return items;
    }

    private static List<object> GetClientLoans(SqlConnection connection, long id)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (50)
                id_credito,
                ISNULL(numero_credito, cedula_id_cliente_ofic_ciclo) AS numero_credito,
                estado_operativo,
                moneda,
                monto_aprobado,
                saldo_capital,
                fecha_desembolso,
                fecha_vencimiento,
                tasa_interes_anual
            FROM creditos.credito
            WHERE id_cliente = @id_cliente
            ORDER BY id_credito DESC;
            """,
            connection);
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                number = reader.GetString(1),
                status = reader.GetString(2),
                currency = reader.GetString(3),
                approvedAmount = reader.GetDecimal(4),
                principalBalance = reader.GetDecimal(5),
                disbursementDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                dueDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                annualRate = reader.GetDecimal(8),
            });
        }

        return items;
    }

    private static ClientLoanDetailDto? LoadLoanDetail(SqlConnection connection, long id)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                cr.id_credito,
                COALESCE(NULLIF(cr.numero_credito, N''), cr.cedula_id_cliente_ofic_ciclo, N'') AS numero_credito,
                cr.estado_operativo,
                cr.moneda,
                COALESCE(cr.monto_aprobado, cr.saldo_capital, 0) AS monto_aprobado,
                COALESCE(cr.saldo_capital, 0) AS saldo_capital,
                cr.fecha_desembolso,
                cr.fecha_vencimiento,
                COALESCE(cr.tasa_interes_anual, s.tasa_interes_anual, 0) AS tasa_interes_anual,
                cr.id_cliente,
                c.cedula,
                c.nombres + N' ' + c.apellidos AS cliente,
                COALESCE(c.tipo_cliente, N'INDIVIDUAL') AS tipo_cliente,
                COALESCE(c.sucursal, N'CENTRAL') AS sucursal,
                COALESCE(c.nombre_negocio, N'') AS alias_cliente,
                COALESCE(s.producto_credito, N'MICROCREDITO') AS producto_credito,
                COALESCE(s.destino_credito, N'') AS destino_credito,
                COALESCE(s.plazo_meses, cr.plazo_meses, 0) AS plazo_meses,
                COALESCE(s.cuota_estimada, 0) AS cuota_estimada,
                COALESCE(s.tasa_comision_ascc, 0) AS tasa_comision_ascc,
                COALESCE(s.tasa_mora_anual, 0) AS tasa_mora_anual,
                COALESCE(s.tasa_deslizamiento_anual, 0) AS tasa_deslizamiento_anual
            FROM creditos.credito cr
            INNER JOIN clientes.cliente c ON c.id_cliente = cr.id_cliente
            LEFT JOIN creditos.solicitud_credito s ON s.id_solicitud_credito = cr.id_solicitud_credito
            WHERE cr.id_credito = @id_credito;
            """,
            connection);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var detail = new ClientLoanDetailDto
        {
            Id = reader.GetInt64(0),
            Number = reader.GetString(1),
            Status = reader.GetString(2),
            Currency = reader.GetString(3),
            ApprovedAmount = reader.GetDecimal(4),
            PrincipalBalance = reader.GetDecimal(5),
            DisbursementDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            DueDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            AnnualRate = reader.GetDecimal(8),
            ClientId = reader.GetInt64(9),
            ClientIdentification = reader.GetString(10),
            ClientName = reader.GetString(11),
            ClientType = reader.GetString(12),
            Branch = reader.GetString(13),
            Alias = reader.GetString(14),
            Product = reader.GetString(15),
            Destination = reader.GetString(16),
            TermMonths = reader.GetInt32(17),
            EstimatedInstallment = reader.GetDecimal(18),
            CommissionRate = reader.GetDecimal(19),
            MoraRate = reader.GetDecimal(20),
            SlidingRate = reader.GetDecimal(21),
        };
        reader.Close();

        detail.Plan = LoadLoanPlan(connection, detail);
        detail.Statement = BuildLoanStatement(detail.Plan);
        detail.Rates = LoadLoanRates(connection, detail);
        ApplyLoanSummary(detail);
        return detail;
    }

    private static List<ClientLoanPlanRowDto> LoadLoanPlan(SqlConnection connection, ClientLoanDetailDto loan)
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
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = loan.Id;

        using var reader = command.ExecuteReader();
        var rows = new List<ClientLoanPlanRowDto>();
        var runningBalance = loan.ApprovedAmount;
        while (reader.Read())
        {
            var capital = reader.GetDecimal(3);
            runningBalance = CreditOperationsSupport.SafeDecimal(runningBalance - capital);
            var balance = runningBalance;
            var state = reader.GetString(10);
            rows.Add(new ClientLoanPlanRowDto
            {
                Number = reader.GetInt32(0),
                DueDate = reader.GetDateTime(1),
                InterestDays = reader.GetInt32(2),
                Capital = capital,
                Interest = reader.GetDecimal(4),
                Other = reader.GetDecimal(5) + reader.GetDecimal(6),
                Commission = reader.GetDecimal(5),
                Sliding = reader.GetDecimal(6),
                Mora = reader.GetDecimal(7),
                Total = reader.GetDecimal(8),
                Balance = balance,
                Status = state,
                Paid = IsPaidInstallment(state),
            });
        }

        return rows;
    }

    private static List<ClientLoanStatementRowDto> BuildLoanStatement(IReadOnlyList<ClientLoanPlanRowDto> plan)
    {
        return plan.Select(row =>
        {
            var paid = row.Paid;
            return new ClientLoanStatementRowDto
            {
                PaymentDate = row.DueDate,
                LateDays = paid ? 0 : Math.Max(0, (DateTime.Today - row.DueDate.Date).Days),
                PaidAmount = paid ? row.Total : 0,
                CurrentInterest = row.Interest,
                MoraInterest = row.Mora,
                Other = row.Other,
                CapitalPayment = paid ? row.Capital : 0,
                PrincipalBalance = row.Balance,
                Status = row.Status,
            };
        }).ToList();
    }

    private static List<ClientLoanRateRowDto> LoadLoanRates(SqlConnection connection, ClientLoanDetailDto loan)
    {
        using var command = new SqlCommand(
            """
            SELECT fecha_tasa, tasa_interes_anual, COALESCE(observacion, N'') AS observacion
            FROM creditos.tasa_variable_credito
            WHERE id_credito = @id_credito
            ORDER BY fecha_tasa;
            """,
            connection);
        command.Parameters.Add("@id_credito", SqlDbType.BigInt).Value = loan.Id;

        using var reader = command.ExecuteReader();
        var rates = new List<ClientLoanRateRowDto>();
        while (reader.Read())
        {
            rates.Add(new ClientLoanRateRowDto
            {
                Date = reader.GetDateTime(0),
                AnnualRate = reader.GetDecimal(1),
                Note = reader.GetString(2),
            });
        }

        if (rates.Count == 0)
        {
            rates.Add(new ClientLoanRateRowDto
            {
                Date = loan.DisbursementDate ?? DateTime.Today,
                AnnualRate = loan.AnnualRate,
                Note = "Tasa actual del prestamo.",
            });
        }

        return rates;
    }

    private static void ApplyLoanSummary(ClientLoanDetailDto loan)
    {
        var next = loan.Plan.FirstOrDefault(row => !row.Paid && row.DueDate.Date >= DateTime.Today)
            ?? loan.Plan.FirstOrDefault(row => !row.Paid)
            ?? loan.Plan.LastOrDefault();

        loan.NextPayment = next is null
            ? null
            : new ClientLoanNextPaymentDto
            {
                DueDate = next.DueDate,
                Capital = next.Capital,
                CurrentInterest = next.Interest,
                Other = next.Other,
                Mora = next.Mora,
                Total = next.Total,
            };

        foreach (var row in loan.Plan)
        {
            row.CalendarStatus = row.Paid
                ? "CANCELADO"
                : next is not null && row.Number == next.Number ? "VIGENTE" : "NO VIGENTE";
        }

        var paidCount = loan.Plan.Count(row => row.Paid);
        loan.InstallmentProgress = $"{Math.Min(paidCount + 1, Math.Max(loan.Plan.Count, 1))}/{loan.Plan.Count}";
        loan.TotalCommission = loan.Plan.Sum(row => row.Commission);
        loan.CurrentInterest = loan.Plan.Where(row => !row.Paid && row.DueDate.Date <= DateTime.Today).Sum(row => row.Interest);
        if (loan.CurrentInterest <= 0 && next is not null)
        {
            loan.CurrentInterest = next.Interest;
        }

        loan.OtherBalance = next?.Other ?? 0;
        loan.TotalOwed = CreditOperationsSupport.SafeDecimal(loan.PrincipalBalance + loan.CurrentInterest + loan.OtherBalance);
    }

    private static bool IsPaidInstallment(string? status)
    {
        var value = CreditOperationsSupport.NormalizeCode(status, string.Empty);
        return value is "PAGADA" or "CANCELADA" or "CANCELADO";
    }

    private static string BuildLoanPrintableHtml(ClientLoanDetailDto detail, string mode)
    {
        var title = mode == "plan" ? "Plan de pago" : "Estado de cuenta";
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" />");
        builder.AppendLine($"<title>{Html(title)} - {Html(detail.Number)}</title>");
        builder.AppendLine("""
        <style>
          body{font-family:Arial,sans-serif;margin:28px;color:#102a37;background:#fff}
          h1{margin:0 0 6px;font-size:24px} h2{font-size:16px;margin:22px 0 10px;color:#12617a}
          .muted{color:#60727d}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;margin:18px 0}
          .card{border:1px solid #d8e4ea;border-radius:8px;padding:10px}.card span{display:block;color:#60727d;font-size:12px}.card strong{font-size:15px}
          table{width:100%;border-collapse:collapse;font-size:12px}th{background:#e8f4f8;color:#006b8d;text-transform:uppercase;font-size:11px}
          th,td{border:1px solid #d8e4ea;padding:7px;text-align:right}td:first-child,th:first-child{text-align:left}.left{text-align:left}.total{font-weight:bold;background:#f3f7f9}
          .actions{position:fixed;top:14px;right:18px}@media print{.actions{display:none}body{margin:12px}}
        </style></head><body>
        <button class="actions" onclick="window.print()">Imprimir</button>
        """);
        builder.AppendLine($"<h1>{Html(title)}</h1><div class=\"muted\">Prestamo {Html(detail.Number)} - {Html(detail.ClientName)} - emitido {DateTime.Now:dd/MM/yyyy HH:mm}</div>");
        builder.AppendLine("<section class=\"grid\">");
        builder.AppendLine(PrintCard("Cliente", detail.ClientIdentification));
        builder.AppendLine(PrintCard("Producto", detail.Product));
        builder.AppendLine(PrintCard("Monto", $"{detail.Currency} {detail.ApprovedAmount:N2}"));
        builder.AppendLine(PrintCard("Saldo capital", $"{detail.Currency} {detail.PrincipalBalance:N2}"));
        builder.AppendLine(PrintCard("Cuota/plazo", detail.InstallmentProgress));
        builder.AppendLine(PrintCard("Tasa variable actual", $"{detail.AnnualRate:N2}%"));
        builder.AppendLine(PrintCard("Estado", detail.Status));
        builder.AppendLine(PrintCard("Total adeudado", $"{detail.Currency} {detail.TotalOwed:N2}"));
        builder.AppendLine("</section>");

        if (mode == "plan")
        {
            AppendPlanPrintableRows(builder, detail);
            AppendRatePrintableRows(builder, detail);
        }
        else
        {
            AppendStatementPrintableRows(builder, detail);
        }

        builder.AppendLine("</body></html>");
        return builder.ToString();
    }

    private static void AppendStatementPrintableRows(StringBuilder builder, ClientLoanDetailDto detail)
    {
        builder.AppendLine("<h2>Estado de cuenta</h2><table><thead><tr><th>Fecha pago</th><th>Dias atraso</th><th>Monto pagado</th><th>Interes corriente</th><th>Interes moratorio</th><th>Otros</th><th>Abono capital</th><th>Saldo capital</th><th>Estado</th></tr></thead><tbody>");
        foreach (var row in detail.Statement)
        {
            builder.AppendLine($"<tr><td class=\"left\">{row.PaymentDate:dd/MM/yyyy}</td><td>{row.LateDays}</td><td>{row.PaidAmount:N2}</td><td>{row.CurrentInterest:N2}</td><td>{row.MoraInterest:N2}</td><td>{row.Other:N2}</td><td>{row.CapitalPayment:N2}</td><td>{row.PrincipalBalance:N2}</td><td class=\"left\">{Html(row.Status)}</td></tr>");
        }
        builder.AppendLine($"<tr class=\"total\"><td class=\"left\">Totales</td><td></td><td>{detail.Statement.Sum(row => row.PaidAmount):N2}</td><td>{detail.Statement.Sum(row => row.CurrentInterest):N2}</td><td>{detail.Statement.Sum(row => row.MoraInterest):N2}</td><td>{detail.Statement.Sum(row => row.Other):N2}</td><td>{detail.Statement.Sum(row => row.CapitalPayment):N2}</td><td>{detail.PrincipalBalance:N2}</td><td></td></tr>");
        builder.AppendLine("</tbody></table>");
    }

    private static void AppendPlanPrintableRows(StringBuilder builder, ClientLoanDetailDto detail)
    {
        builder.AppendLine("<h2>Calendario de pagos</h2><table><thead><tr><th>Cuota</th><th>Fecha</th><th>Saldo capital</th><th>Capital</th><th>Interes</th><th>Otros</th><th>Valor paga</th><th>Valor cuota</th><th>Estado</th></tr></thead><tbody>");
        foreach (var row in detail.Plan)
        {
            builder.AppendLine($"<tr><td class=\"left\">{row.Number}</td><td class=\"left\">{row.DueDate:dd/MM/yyyy}</td><td>{row.Balance:N2}</td><td>{row.Capital:N2}</td><td>{row.Interest:N2}</td><td>{row.Other:N2}</td><td>{(row.Paid ? row.Total : 0):N2}</td><td>{row.Total:N2}</td><td class=\"left\">{Html(row.CalendarStatus)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

    private static void AppendRatePrintableRows(StringBuilder builder, ClientLoanDetailDto detail)
    {
        builder.AppendLine("<h2>Tasas variables</h2><table><thead><tr><th>Fecha</th><th>Tasa</th><th>Observacion</th></tr></thead><tbody>");
        foreach (var row in detail.Rates)
        {
            builder.AppendLine($"<tr><td class=\"left\">{row.Date:dd/MM/yyyy}</td><td>{row.AnnualRate:N2}%</td><td class=\"left\">{Html(row.Note)}</td></tr>");
        }
        builder.AppendLine("</tbody></table>");
    }

    private static string PrintCard(string label, string value) => $"<article class=\"card\"><span>{Html(label)}</span><strong>{Html(value)}</strong></article>";

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static List<object> GetClientDeletionRequests(SqlConnection connection, long id)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (20)
                id_solicitud_eliminacion,
                motivo,
                estado,
                usuario_solicita,
                fecha_solicitud,
                usuario_autoriza,
                fecha_autorizacion,
                observacion_autorizacion
            FROM clientes.solicitud_eliminacion_cliente
            WHERE id_cliente = @id_cliente
            ORDER BY id_solicitud_eliminacion DESC;
            """,
            connection);
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                reason = reader.GetString(1),
                state = reader.GetString(2),
                requestedBy = reader.GetString(3),
                requestedAt = reader.GetDateTime(4),
                authorizedBy = reader.IsDBNull(5) ? null : reader.GetString(5),
                authorizedAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                authorizationNote = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }

        return items;
    }

    private static long? FindDuplicateId(SqlConnection connection, string? cedula, long? exceptId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1) id_cliente
            FROM clientes.cliente
            WHERE cedula = @cedula
              AND (@except_id IS NULL OR id_cliente <> @except_id);
            """,
            connection);
        command.Parameters.Add("@cedula", SqlDbType.NVarChar, 50).Value = NormalizeIdentification(cedula);
        command.Parameters.Add("@except_id", SqlDbType.BigInt).Value = exceptId.HasValue ? exceptId.Value : DBNull.Value;
        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    private static bool ClientHasActiveLoans(SqlConnection connection, long id)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM creditos.credito
            WHERE id_cliente = @id_cliente
              AND activo = 1
              AND saldo_capital > 0;
            """,
            connection);
        command.Parameters.Add("@id_cliente", SqlDbType.BigInt).Value = id;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static ClientDto MapClient(SqlDataReader reader)
    {
        var dto = new ClientDto
        {
            Id = reader.GetInt64(0),
            Cedula = reader.GetString(1),
            Names = reader.GetString(2),
            LastNames = reader.GetString(3),
            BirthDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
            Phone = reader.IsDBNull(5) ? null : reader.GetString(5),
            Email = reader.IsDBNull(6) ? null : reader.GetString(6),
            Address = reader.IsDBNull(7) ? null : reader.GetString(7),
            ClientType = reader.GetString(8),
            Active = reader.GetBoolean(9),
            CreatedAt = reader.GetDateTime(10),
            IdentificationType = reader.IsDBNull(11) ? "CEDULA" : reader.GetString(11),
            Branch = reader.IsDBNull(12) ? null : reader.GetString(12),
            Relationship = reader.IsDBNull(13) ? null : reader.GetString(13),
            Status = reader.GetString(14),
            EntryDate = reader.GetDateTime(15),
            Gender = reader.IsDBNull(16) ? null : reader.GetString(16),
            CivilStatus = reader.IsDBNull(17) ? null : reader.GetString(17),
            SpouseName = reader.IsDBNull(18) ? null : reader.GetString(18),
            SecondaryPhone = reader.IsDBNull(19) ? null : reader.GetString(19),
            Mobile = reader.IsDBNull(20) ? null : reader.GetString(20),
            HomeGeography = reader.IsDBNull(21) ? null : reader.GetString(21),
            Occupation = reader.IsDBNull(22) ? null : reader.GetString(22),
            EconomicActivity = reader.IsDBNull(23) ? null : reader.GetString(23),
            BusinessName = reader.IsDBNull(24) ? null : reader.GetString(24),
            BusinessAddress = reader.IsDBNull(25) ? null : reader.GetString(25),
            BusinessGeography = reader.IsDBNull(26) ? null : reader.GetString(26),
            BusinessAgeMonths = reader.GetInt32(27),
            MonthlyIncome = reader.GetDecimal(28),
            SpouseIncome = reader.GetDecimal(29),
            Remittances = reader.GetDecimal(30),
            RentIncome = reader.GetDecimal(31),
            OtherIncome = reader.GetDecimal(32),
            MonthlyExpenses = reader.GetDecimal(33),
            SourceOfFunds = reader.IsDBNull(34) ? null : reader.GetString(34),
            RelationshipPurpose = reader.IsDBNull(35) ? null : reader.GetString(35),
            IsPep = reader.GetBoolean(36),
            RiskLevel = reader.GetString(37),
            RiskScore = reader.GetInt32(38),
            FileStatus = reader.GetString(39),
            Notes = reader.IsDBNull(40) ? null : reader.GetString(40),
            RegisteredBy = reader.IsDBNull(41) ? null : reader.GetString(41),
            UpdatedAt = reader.IsDBNull(42) ? null : reader.GetDateTime(42),
            TotalApplications = reader.GetInt32(43),
            TotalLoans = reader.GetInt32(44),
            PrincipalBalance = reader.GetDecimal(45),
        };

        dto.FullName = $"{dto.Names} {dto.LastNames}".Trim();
        dto.TotalIncome = dto.MonthlyIncome + dto.SpouseIncome + dto.Remittances + dto.RentIncome + dto.OtherIncome;
        dto.PaymentCapacity = Math.Max(0, dto.TotalIncome - dto.MonthlyExpenses);
        return dto;
    }

    private static string NormalizeIdentification(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
    }

    private static string NormalizeStatus(string? value)
    {
        var status = CreditOperationsSupport.NormalizeCode(value, "TODOS");
        return status == string.Empty ? "TODOS" : status;
    }

    private static string NormalizeType(string? value)
    {
        var type = CreditOperationsSupport.NormalizeCode(value, "TODOS");
        return type == string.Empty ? "TODOS" : type;
    }

    private static string NormalizeRisk(string? value)
    {
        var risk = CreditOperationsSupport.NormalizeCode(value, "MEDIO");
        return CreditOperationsSupport.RiskLevels.Contains(risk) ? risk : "MEDIO";
    }

    private static DateTime? InferBirthDateFromIdentification(string? normalizedIdentification)
    {
        var value = NormalizeIdentification(normalizedIdentification);
        if (!NicaraguanIdPattern.IsMatch(value))
        {
            return null;
        }

        if (!int.TryParse(value.Substring(3, 2), out var day) ||
            !int.TryParse(value.Substring(5, 2), out var month) ||
            !int.TryParse(value.Substring(7, 2), out var year2))
        {
            return null;
        }

        var currentYear2 = DateTime.Today.Year % 100;
        var century = year2 > currentYear2 ? 1900 : 2000;
        var year = century + year2;

        try
        {
            var date = new DateTime(year, month, day);
            if (date > DateTime.Today)
            {
                date = date.AddYears(-100);
            }

            return date;
        }
        catch
        {
            return null;
        }
    }

    public sealed class ClientSaveModel
    {
        public string? IdentificationType { get; set; }
        public string? Cedula { get; set; }
        public string Names { get; set; } = string.Empty;
        public string LastNames { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string? Phone { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? ClientType { get; set; }
        public string? Branch { get; set; }
        public string? Relationship { get; set; }
        public string? Status { get; set; }
        public DateTime? EntryDate { get; set; }
        public string? Gender { get; set; }
        public string? CivilStatus { get; set; }
        public string? SpouseName { get; set; }
        public string? HomeGeography { get; set; }
        public string? Occupation { get; set; }
        public string? EconomicActivity { get; set; }
        public string? BusinessName { get; set; }
        public string? BusinessAddress { get; set; }
        public string? BusinessGeography { get; set; }
        public int BusinessAgeMonths { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal SpouseIncome { get; set; }
        public decimal Remittances { get; set; }
        public decimal RentIncome { get; set; }
        public decimal OtherIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public string? SourceOfFunds { get; set; }
        public string? RelationshipPurpose { get; set; }
        public bool IsPep { get; set; }
        public string? RiskLevel { get; set; }
        public int RiskScore { get; set; } = 50;
        public string? FileStatus { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class ClientDeleteRequestModel
    {
        public string? Reason { get; set; }
        public string? AdminUser { get; set; }
        public string? AdminPassword { get; set; }
    }

    public sealed class ClientLoanDetailDto
    {
        public long Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal ApprovedAmount { get; set; }
        public decimal PrincipalBalance { get; set; }
        public DateTime? DisbursementDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal AnnualRate { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal MoraRate { get; set; }
        public decimal SlidingRate { get; set; }
        public long ClientId { get; set; }
        public string ClientIdentification { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientType { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public int TermMonths { get; set; }
        public decimal EstimatedInstallment { get; set; }
        public string InstallmentProgress { get; set; } = string.Empty;
        public decimal TotalCommission { get; set; }
        public decimal CurrentInterest { get; set; }
        public decimal OtherBalance { get; set; }
        public decimal TotalOwed { get; set; }
        public ClientLoanNextPaymentDto? NextPayment { get; set; }
        public List<ClientLoanStatementRowDto> Statement { get; set; } = new();
        public List<ClientLoanPlanRowDto> Plan { get; set; } = new();
        public List<ClientLoanRateRowDto> Rates { get; set; } = new();
    }

    public sealed class ClientLoanNextPaymentDto
    {
        public DateTime DueDate { get; set; }
        public decimal Capital { get; set; }
        public decimal CurrentInterest { get; set; }
        public decimal Other { get; set; }
        public decimal Mora { get; set; }
        public decimal Total { get; set; }
    }

    public sealed class ClientLoanStatementRowDto
    {
        public DateTime PaymentDate { get; set; }
        public int LateDays { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal CurrentInterest { get; set; }
        public decimal MoraInterest { get; set; }
        public decimal Other { get; set; }
        public decimal CapitalPayment { get; set; }
        public decimal PrincipalBalance { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class ClientLoanPlanRowDto
    {
        public int Number { get; set; }
        public DateTime DueDate { get; set; }
        public int InterestDays { get; set; }
        public decimal Capital { get; set; }
        public decimal Interest { get; set; }
        public decimal Other { get; set; }
        public decimal Commission { get; set; }
        public decimal Sliding { get; set; }
        public decimal Mora { get; set; }
        public decimal Total { get; set; }
        public decimal Balance { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool Paid { get; set; }
        public string CalendarStatus { get; set; } = string.Empty;
    }

    public sealed class ClientLoanRateRowDto
    {
        public DateTime Date { get; set; }
        public decimal AnnualRate { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public sealed class ClientDto
    {
        public long Id { get; set; }
        public string Cedula { get; set; } = string.Empty;
        public string Names { get; set; } = string.Empty;
        public string LastNames { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string? Phone { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string ClientType { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public string IdentificationType { get; set; } = string.Empty;
        public string? Branch { get; set; }
        public string? Relationship { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string? Gender { get; set; }
        public string? CivilStatus { get; set; }
        public string? SpouseName { get; set; }
        public string? HomeGeography { get; set; }
        public string? Occupation { get; set; }
        public string? EconomicActivity { get; set; }
        public string? BusinessName { get; set; }
        public string? BusinessAddress { get; set; }
        public string? BusinessGeography { get; set; }
        public int BusinessAgeMonths { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal SpouseIncome { get; set; }
        public decimal Remittances { get; set; }
        public decimal RentIncome { get; set; }
        public decimal OtherIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal PaymentCapacity { get; set; }
        public string? SourceOfFunds { get; set; }
        public string? RelationshipPurpose { get; set; }
        public bool IsPep { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public int RiskScore { get; set; }
        public string FileStatus { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? RegisteredBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int TotalApplications { get; set; }
        public int TotalLoans { get; set; }
        public decimal PrincipalBalance { get; set; }
    }
}
