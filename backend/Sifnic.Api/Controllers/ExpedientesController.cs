using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class ExpedientesController : Controller
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".doc",
        ".docx",
    };

    private readonly IWebHostEnvironment _environment;

    public ExpedientesController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            const string sql = """
                SELECT
                    e.id_empleado,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    d.nombre_departamento,
                    c.nombre_cargo,
                    ee.codigo_estado_empleado
                FROM rrhh.empleado e
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                INNER JOIN rrhh.estado_empleado ee
                    ON ee.id_estado_empleado = e.id_estado_empleado
                WHERE e.activo = 1
                ORDER BY nombre_empleado;
                """;

            using var command = new SqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            var employees = new List<object>();
            while (reader.Read())
            {
                employees.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                    department = reader.GetString(3),
                    position = reader.GetString(4),
                    status = reader.GetString(5),
                });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    employees,
                    documentTypes = GetDocumentTypeOptions(),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos de expedientes.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Listar(string? search, string? status)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            const string sql = """
                DECLARE @hoy DATE = CAST(GETDATE() AS date);
                DECLARE @proximo DATE = DATEADD(day, 30, @hoy);

                SELECT
                    x.id_expediente_documento,
                    x.id_empleado,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    e.cedula,
                    d.nombre_departamento,
                    c.nombre_cargo,
                    x.tipo_documento,
                    x.nombre_archivo,
                    x.ruta_archivo,
                    x.fecha_documento,
                    x.fecha_vencimiento,
                    x.observacion,
                    x.fecha_registro
                FROM rrhh.expediente_documento x
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = x.id_empleado
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                WHERE
                    (
                        @search = N''
                        OR e.codigo_empleado LIKE N'%' + @search + N'%'
                        OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                        OR x.tipo_documento LIKE N'%' + @search + N'%'
                        OR COALESCE(x.nombre_archivo, N'') LIKE N'%' + @search + N'%'
                        OR COALESCE(x.observacion, N'') LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'VIGENTES' AND (x.fecha_vencimiento IS NULL OR x.fecha_vencimiento >= @hoy))
                        OR (@status = N'POR_VENCER' AND x.fecha_vencimiento BETWEEN @hoy AND @proximo)
                        OR (@status = N'VENCIDOS' AND x.fecha_vencimiento < @hoy)
                        OR (@status = N'SIN_ARCHIVO' AND (x.ruta_archivo IS NULL OR LTRIM(RTRIM(x.ruta_archivo)) = N''))
                    )
                ORDER BY x.id_expediente_documento DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeStatus(status);

            using var reader = command.ExecuteReader();
            var items = new List<ExpedienteDto>();
            while (reader.Read())
            {
                items.Add(MapExpediente(reader));
            }

            return Json(new
            {
                ok = true,
                data = items,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el listado de expedientes.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{id:long}")]
    public IActionResult Obtener(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var item = GetExpediente(connection, id);
            if (item is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Documento de expediente no encontrado.",
                });
            }

            return Json(new
            {
                ok = true,
                data = item,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo obtener el expediente.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    [RequestSizeLimit(10485760)]
    public IActionResult Crear([FromForm] ExpedienteSaveModel model)
    {
        var errors = ValidateDocument(model, requireExistingFile: false);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del expediente.",
                errors,
            });
        }

        string? newPhysicalPath = null;

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            if (!EmployeeExists(connection, model.IdEmpleado, transaction))
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "El empleado seleccionado no existe.",
                    errors = new Dictionary<string, string>
                    {
                        ["idEmpleado"] = "Selecciona un empleado valido.",
                    },
                });
            }

            var fileData = SaveIncomingFile(model.Archivo);
            newPhysicalPath = fileData?.PhysicalPath;

            long id;
            using (var command = new SqlCommand(
                """
                INSERT INTO rrhh.expediente_documento
                (
                    id_empleado,
                    tipo_documento,
                    nombre_archivo,
                    ruta_archivo,
                    fecha_documento,
                    fecha_vencimiento,
                    observacion,
                    fecha_registro
                )
                OUTPUT INSERTED.id_expediente_documento
                VALUES
                (
                    @id_empleado,
                    @tipo_documento,
                    @nombre_archivo,
                    @ruta_archivo,
                    @fecha_documento,
                    @fecha_vencimiento,
                    @observacion,
                    SYSDATETIME()
                );
                """,
                connection,
                transaction))
            {
                ConfigureWriteCommand(command, model, fileData, removeFile: false);
                id = Convert.ToInt64(command.ExecuteScalar());
            }

            var created = GetExpediente(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "EXPEDIENTE_DOCUMENTO",
                "INSERCION",
                created.IdExpedienteDocumento,
                created.CodigoEmpleado,
                $"Se registro el documento {created.TipoDocumento} para {created.CodigoEmpleado}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    expediente = created,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Documento de expediente creado correctamente.",
                data = created,
            });
        }
        catch (SqlException ex)
        {
            TryDeleteFile(newPhysicalPath);
            return BadRequest(new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo crear el expediente."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            TryDeleteFile(newPhysicalPath);
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo crear el expediente.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    [RequestSizeLimit(10485760)]
    public IActionResult Actualizar(long id, [FromForm] ExpedienteSaveModel model)
    {
        var errors = ValidateDocument(model, requireExistingFile: false);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del expediente.",
                errors,
            });
        }

        string? newPhysicalPath = null;
        string? oldPhysicalPath = null;

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var previous = GetExpediente(connection, id, transaction);
            if (previous is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Documento de expediente no encontrado.",
                });
            }

            if (!EmployeeExists(connection, model.IdEmpleado, transaction))
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "El empleado seleccionado no existe.",
                    errors = new Dictionary<string, string>
                    {
                        ["idEmpleado"] = "Selecciona un empleado valido.",
                    },
                });
            }

            var fileData = SaveIncomingFile(model.Archivo);
            newPhysicalPath = fileData?.PhysicalPath;
            oldPhysicalPath = ResolvePhysicalPath(previous.RutaArchivo);

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.expediente_documento
                SET
                    id_empleado = @id_empleado,
                    tipo_documento = @tipo_documento,
                    nombre_archivo = @nombre_archivo,
                    ruta_archivo = @ruta_archivo,
                    fecha_documento = @fecha_documento,
                    fecha_vencimiento = @fecha_vencimiento,
                    observacion = @observacion
                WHERE id_expediente_documento = @id_expediente_documento;
                """,
                connection,
                transaction))
            {
                ConfigureWriteCommand(command, model, fileData, model.RemoverArchivo);
                command.Parameters.Add("@id_expediente_documento", SqlDbType.BigInt).Value = id;

                if (fileData is null && !model.RemoverArchivo)
                {
                    command.Parameters["@nombre_archivo"].Value = RrhhSupport.ToDbValue(previous.NombreArchivo);
                    command.Parameters["@ruta_archivo"].Value = RrhhSupport.ToDbValue(previous.RutaArchivo);
                }

                command.ExecuteNonQuery();
            }

            var updated = GetExpediente(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "EXPEDIENTE_DOCUMENTO",
                "MODIFICACION",
                updated.IdExpedienteDocumento,
                updated.CodigoEmpleado,
                $"Se modifico el documento {updated.TipoDocumento} para {updated.CodigoEmpleado}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    anterior = previous,
                    actual = updated,
                });

            transaction.Commit();

            if ((fileData is not null || model.RemoverArchivo) && !string.IsNullOrWhiteSpace(oldPhysicalPath))
            {
                TryDeleteFile(oldPhysicalPath);
            }

            return Json(new
            {
                ok = true,
                message = "Documento de expediente actualizado correctamente.",
                data = updated,
            });
        }
        catch (SqlException ex)
        {
            TryDeleteFile(newPhysicalPath);
            return BadRequest(new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo actualizar el expediente."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            TryDeleteFile(newPhysicalPath);
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar el expediente.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{id:long}")]
    public IActionResult Descargar(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var record = GetExpediente(connection, id);
            if (record is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Documento de expediente no encontrado.",
                });
            }

            if (!record.TieneArchivo || string.IsNullOrWhiteSpace(record.RutaArchivo))
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Este expediente no tiene archivo adjunto.",
                });
            }

            var physicalPath = ResolvePhysicalPath(record.RutaArchivo);
            if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
            {
                return NotFound(new
                {
                    ok = false,
                    message = "No se encontro el archivo del expediente.",
                });
            }

            RrhhSupport.RegisterBitacora(
                connection,
                null,
                HttpContext,
                "EXPEDIENTE_DOCUMENTO",
                "DESCARGA",
                record.IdExpedienteDocumento,
                record.CodigoEmpleado,
                $"Se descargo el documento {record.TipoDocumento} de {record.CodigoEmpleado}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    expediente = record,
                });

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(record.NombreArchivo ?? physicalPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(physicalPath, contentType, record.NombreArchivo ?? Path.GetFileName(physicalPath));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo descargar el expediente.",
                detail = ex.Message,
            });
        }
    }

    [HttpDelete("{id:long}")]
    public IActionResult Eliminar(long id, [FromBody] DeleteRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.AdminUsuario) || string.IsNullOrWhiteSpace(model.AdminPassword))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Debes ingresar usuario y contrasena de administrador.",
            });
        }

        string? oldPhysicalPath = null;

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var authorization = RrhhSupport.ValidateAdministrator(connection, model.AdminUsuario, model.AdminPassword);
            if (!authorization.Ok)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = authorization.Message,
                });
            }

            using var transaction = connection.BeginTransaction();
            var record = GetExpediente(connection, id, transaction);
            if (record is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Documento de expediente no encontrado.",
                });
            }

            oldPhysicalPath = ResolvePhysicalPath(record.RutaArchivo);

            using (var command = new SqlCommand(
                "DELETE FROM rrhh.expediente_documento WHERE id_expediente_documento = @id_expediente_documento;",
                connection,
                transaction))
            {
                command.Parameters.Add("@id_expediente_documento", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "EXPEDIENTE_DOCUMENTO",
                "ELIMINACION",
                record.IdExpedienteDocumento,
                record.CodigoEmpleado,
                $"Se elimino el documento {record.TipoDocumento} de {record.CodigoEmpleado}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    administrador = authorization.UsuarioAdministrador,
                    expediente = record,
                },
                authorization.UsuarioAdministrador);

            transaction.Commit();

            TryDeleteFile(oldPhysicalPath);

            return Json(new
            {
                ok = true,
                message = "Documento de expediente eliminado correctamente.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo eliminar el expediente.",
                detail = ex.Message,
            });
        }
    }

    private ExpedienteDto? GetExpediente(SqlConnection connection, long id, SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT
                x.id_expediente_documento,
                x.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.cedula,
                d.nombre_departamento,
                c.nombre_cargo,
                x.tipo_documento,
                x.nombre_archivo,
                x.ruta_archivo,
                x.fecha_documento,
                x.fecha_vencimiento,
                x.observacion,
                x.fecha_registro
            FROM rrhh.expediente_documento x
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = x.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            WHERE x.id_expediente_documento = @id_expediente_documento;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_expediente_documento", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapExpediente(reader) : null;
    }

    private ExpedienteDto MapExpediente(SqlDataReader reader)
    {
        var fechaVencimiento = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11);
        var tieneArchivo = !reader.IsDBNull(9) && !string.IsNullOrWhiteSpace(reader.GetString(9));
        var today = DateTime.Today;

        var estadoDocumento = "VIGENTE";
        if (!tieneArchivo)
        {
            estadoDocumento = "SIN_ARCHIVO";
        }
        else if (fechaVencimiento.HasValue && fechaVencimiento.Value.Date < today)
        {
            estadoDocumento = "VENCIDO";
        }
        else if (fechaVencimiento.HasValue && fechaVencimiento.Value.Date <= today.AddDays(30))
        {
            estadoDocumento = "POR_VENCER";
        }

        return new ExpedienteDto
        {
            IdExpedienteDocumento = reader.GetInt64(0),
            IdEmpleado = reader.GetInt64(1),
            CodigoEmpleado = reader.GetString(2),
            NombreEmpleado = reader.GetString(3),
            Cedula = reader.GetString(4),
            NombreDepartamento = reader.GetString(5),
            NombreCargo = reader.GetString(6),
            TipoDocumento = reader.GetString(7),
            NombreArchivo = reader.IsDBNull(8) ? null : reader.GetString(8),
            RutaArchivo = reader.IsDBNull(9) ? null : reader.GetString(9),
            FechaDocumento = reader.IsDBNull(10) ? null : reader.GetDateTime(10).ToString("yyyy-MM-dd"),
            FechaVencimiento = fechaVencimiento?.ToString("yyyy-MM-dd"),
            Observacion = reader.IsDBNull(12) ? null : reader.GetString(12),
            FechaRegistro = reader.GetDateTime(13).ToString("yyyy-MM-dd HH:mm:ss"),
            TieneArchivo = tieneArchivo,
            EstadoDocumento = estadoDocumento,
            DownloadUrl = $"/Expedientes/Descargar/{reader.GetInt64(0)}",
        };
    }

    private static Dictionary<string, string> ValidateDocument(ExpedienteSaveModel model, bool requireExistingFile)
    {
        var errors = new Dictionary<string, string>();
        var today = DateTime.Today;
        var minDate = new DateTime(1753, 1, 1);

        if (model.IdEmpleado <= 0)
        {
            errors["idEmpleado"] = "Selecciona el empleado.";
        }

        if (string.IsNullOrWhiteSpace(model.TipoDocumento) ||
            model.TipoDocumento.Trim().Length < 3 ||
            model.TipoDocumento.Trim().Length > 100 ||
            !Regex.IsMatch(model.TipoDocumento.Trim(), "^[A-Za-z0-9ÁÉÍÓÚáéíóúÑñ /_-]+$"))
        {
            errors["tipoDocumento"] = "Ingresa un tipo de documento valido.";
        }

        DateTime? fechaDocumento = null;
        if (!string.IsNullOrWhiteSpace(model.FechaDocumento))
        {
            if (!DateTime.TryParse(model.FechaDocumento, out var parsedFechaDocumento) || parsedFechaDocumento < minDate)
            {
                errors["fechaDocumento"] = "Ingresa una fecha de documento valida.";
            }
            else if (parsedFechaDocumento.Date > today)
            {
                errors["fechaDocumento"] = "La fecha del documento no puede ser futura.";
            }
            else
            {
                fechaDocumento = parsedFechaDocumento.Date;
            }
        }

        if (!string.IsNullOrWhiteSpace(model.FechaVencimiento))
        {
            if (!DateTime.TryParse(model.FechaVencimiento, out var parsedFechaVencimiento) || parsedFechaVencimiento < minDate)
            {
                errors["fechaVencimiento"] = "Ingresa una fecha de vencimiento valida.";
            }
            else if (fechaDocumento.HasValue && parsedFechaVencimiento.Date < fechaDocumento.Value)
            {
                errors["fechaVencimiento"] = "La fecha de vencimiento debe ser igual o mayor a la fecha del documento.";
            }
        }

        if (!string.IsNullOrWhiteSpace(model.Observacion) && model.Observacion.Trim().Length > 500)
        {
            errors["observacion"] = "La observacion supera el limite permitido.";
        }

        if (model.Archivo is not null)
        {
            var extension = Path.GetExtension(model.Archivo.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                errors["archivo"] = "Adjunta un archivo PDF, Word o imagen valido.";
            }

            if (model.Archivo.Length <= 0 || model.Archivo.Length > 10 * 1024 * 1024)
            {
                errors["archivo"] = "El archivo debe pesar entre 1 byte y 10 MB.";
            }
        }
        else if (requireExistingFile && model.RemoverArchivo)
        {
            errors["archivo"] = "No hay archivo para remover.";
        }

        return errors;
    }

    private static bool EmployeeExists(SqlConnection connection, long idEmpleado, SqlTransaction? transaction = null)
    {
        using var command = transaction is null
            ? new SqlCommand("SELECT COUNT(1) FROM rrhh.empleado WHERE id_empleado = @id_empleado;", connection)
            : new SqlCommand("SELECT COUNT(1) FROM rrhh.empleado WHERE id_empleado = @id_empleado;", connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private void ConfigureWriteCommand(
        SqlCommand command,
        ExpedienteSaveModel model,
        SavedFileInfo? fileData,
        bool removeFile)
    {
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = model.IdEmpleado;
        command.Parameters.Add("@tipo_documento", SqlDbType.NVarChar, 100).Value = model.TipoDocumento.Trim();
        command.Parameters.Add("@nombre_archivo", SqlDbType.NVarChar, 255).Value =
            removeFile ? DBNull.Value : RrhhSupport.ToDbValue(fileData?.OriginalName);
        command.Parameters.Add("@ruta_archivo", SqlDbType.NVarChar, 500).Value =
            removeFile ? DBNull.Value : RrhhSupport.ToDbValue(fileData?.RelativePath);
        command.Parameters.Add("@fecha_documento", SqlDbType.Date).Value = RrhhSupport.ToDateDbValue(model.FechaDocumento);
        command.Parameters.Add("@fecha_vencimiento", SqlDbType.Date).Value = RrhhSupport.ToDateDbValue(model.FechaVencimiento);
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value =
            RrhhSupport.ToDbValue(model.Observacion);
    }

    private SavedFileInfo? SaveIncomingFile(IFormFile? file)
    {
        if (file is null)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var folderPath = EnsureStorageFolder();
        var physicalPath = Path.Combine(folderPath, storedName);
        var relativePath = $"/uploads/rrhh/expedientes/{storedName}";

        using var stream = System.IO.File.Create(physicalPath);
        file.CopyTo(stream);

        return new SavedFileInfo
        {
            OriginalName = Path.GetFileName(file.FileName),
            RelativePath = relativePath,
            PhysicalPath = physicalPath,
        };
    }

    private string EnsureStorageFolder()
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var folderPath = Path.Combine(webRoot, "uploads", "rrhh", "expedientes");
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private string? ResolvePhysicalPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var normalized = relativePath.Trim().TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(webRoot, normalized);
    }

    private static void TryDeleteFile(string? physicalPath)
    {
        if (string.IsNullOrWhiteSpace(physicalPath))
        {
            return;
        }

        try
        {
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }
        catch
        {
            // If cleanup fails, the database change remains valid and the file can be removed manually.
        }
    }

    private static object[] GetDocumentTypeOptions() =>
        new object[]
        {
            new { value = "CEDULA", label = "Cedula" },
            new { value = "CONTRATO", label = "Contrato laboral" },
            new { value = "INSS", label = "Documento INSS" },
            new { value = "CURRICULUM", label = "Curriculum" },
            new { value = "TITULO", label = "Titulo o diploma" },
            new { value = "LICENCIA", label = "Licencia o permiso" },
            new { value = "CONSTANCIA", label = "Constancia" },
            new { value = "REFERENCIA", label = "Referencia" },
            new { value = "OTRO", label = "Otro" },
        };

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "TODOS"
            : status.Trim().ToUpperInvariant() switch
            {
                "VIGENTES" => "VIGENTES",
                "POR_VENCER" => "POR_VENCER",
                "VENCIDOS" => "VENCIDOS",
                "SIN_ARCHIVO" => "SIN_ARCHIVO",
                _ => "TODOS",
            };
    }

    public sealed class ExpedienteSaveModel
    {
        public long IdEmpleado { get; set; }
        public string TipoDocumento { get; set; } = string.Empty;
        public string? FechaDocumento { get; set; }
        public string? FechaVencimiento { get; set; }
        public string? Observacion { get; set; }
        public bool RemoverArchivo { get; set; }
        public IFormFile? Archivo { get; set; }
    }

    public sealed class DeleteRequest
    {
        public string AdminUsuario { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    private sealed class SavedFileInfo
    {
        public string OriginalName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string PhysicalPath { get; set; } = string.Empty;
    }

    public sealed class ExpedienteDto
    {
        public long IdExpedienteDocumento { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty;
        public string? NombreArchivo { get; set; }
        public string? RutaArchivo { get; set; }
        public string? FechaDocumento { get; set; }
        public string? FechaVencimiento { get; set; }
        public string? Observacion { get; set; }
        public string FechaRegistro { get; set; } = string.Empty;
        public bool TieneArchivo { get; set; }
        public string EstadoDocumento { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
