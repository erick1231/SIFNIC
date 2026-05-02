using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;
using Sifnic.Api.Security;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class EmpleadosController : Controller
{
    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
    };

    private readonly IWebHostEnvironment _environment;

    public EmpleadosController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(conexion);
            RrhhSupport.EnsureEmployeeProfileSchema(conexion);

            const string sql = """
                SELECT id_departamento, nombre_departamento
                FROM rrhh.departamento
                WHERE activo = 1
                ORDER BY nombre_departamento;

                SELECT id_cargo, nombre_cargo
                FROM rrhh.cargo
                WHERE activo = 1
                ORDER BY nombre_cargo;

                SELECT id_banco, nombre_banco
                FROM rrhh.banco
                WHERE activo = 1
                ORDER BY nombre_banco;

                SELECT TOP (1) codigo_empleado
                FROM rrhh.empleado
                WHERE codigo_empleado LIKE 'EMP%'
                ORDER BY TRY_CONVERT(INT, RIGHT(codigo_empleado, 6)) DESC, codigo_empleado DESC;
                """;

            using var comando = new SqlCommand(sql, conexion);
            using var reader = comando.ExecuteReader();

            var departamentos = new List<object>();
            while (reader.Read())
            {
                departamentos.Add(new
                {
                    id = reader.GetInt64(0),
                    name = reader.GetString(1),
                });
            }

            reader.NextResult();

            var cargos = new List<object>();
            while (reader.Read())
            {
                cargos.Add(new
                {
                    id = reader.GetInt64(0),
                    name = reader.GetString(1),
                });
            }

            reader.NextResult();

            var bancos = new List<object>();
            while (reader.Read())
            {
                bancos.Add(new
                {
                    id = reader.GetInt64(0),
                    name = reader.GetString(1),
                });
            }

            reader.NextResult();

            var ultimoCodigo = "EMP0000";
            if (reader.Read() && !reader.IsDBNull(0))
            {
                ultimoCodigo = reader.GetString(0);
            }

            reader.Close();
            var supervisores = RrhhSupport.ListSupervisorCandidates(conexion, null);

            return Json(new
            {
                ok = true,
                data = new
                {
                    departments = departamentos,
                    positions = cargos,
                    banks = bancos,
                    supervisors = supervisores.Select(supervisor => new
                    {
                        id = supervisor.IdEmpleado,
                        code = supervisor.CodigoEmpleado,
                        name = supervisor.NombreEmpleado,
                        department = supervisor.NombreDepartamento,
                        position = supervisor.NombreCargo,
                        username = supervisor.UsuarioSistema,
                    }),
                    suggestedCode = SiguienteCodigo(ultimoCodigo),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Listar(string? search, string? status)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(conexion);
            RrhhSupport.EnsureEmployeeProfileSchema(conexion);

            const string sql = """
                SELECT
                    e.id_empleado,
                    e.codigo_empleado,
                    e.cedula,
                    e.nombres,
                    e.apellidos,
                    e.nombre_completo,
                    e.foto_perfil_url,
                    d.id_departamento,
                    d.nombre_departamento,
                    c.id_cargo,
                    c.nombre_cargo,
                    ee.id_estado_empleado,
                    ee.codigo_estado_empleado,
                    ee.nombre_estado_empleado,
                    e.fecha_ingreso,
                    e.fecha_nacimiento,
                    e.telefono,
                    e.correo,
                    e.sexo,
                    e.estado_civil,
                    e.direccion,
                    e.id_banco,
                    b.nombre_banco,
                    e.numero_cuenta_bancaria,
                    e.inss,
                    e.activo,
                    e.fecha_baja,
                    e.motivo_baja,
                    e.fecha_registro,
                    e.fecha_actualizacion
                FROM rrhh.empleado e
                INNER JOIN rrhh.departamento d ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c ON c.id_cargo = e.id_cargo
                INNER JOIN rrhh.estado_empleado ee ON ee.id_estado_empleado = e.id_estado_empleado
                LEFT JOIN rrhh.banco b ON b.id_banco = e.id_banco
                WHERE
                    (
                        @search = N''
                        OR e.codigo_empleado LIKE N'%' + @search + N'%'
                        OR e.cedula LIKE N'%' + @search + N'%'
                        OR e.nombres LIKE N'%' + @search + N'%'
                        OR e.apellidos LIKE N'%' + @search + N'%'
                        OR e.nombre_completo LIKE N'%' + @search + N'%'
                        OR ISNULL(e.telefono, N'') LIKE N'%' + @search + N'%'
                        OR ISNULL(e.correo, N'') LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR ee.codigo_estado_empleado = @status
                    )
                ORDER BY e.id_empleado DESC;
                """;

            using var comando = new SqlCommand(sql, conexion);
            comando.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value =
                (search ?? string.Empty).Trim();
            comando.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value =
                NormalizarEstado(status);

            using var reader = comando.ExecuteReader();
            var items = new List<EmpleadoDto>();

            while (reader.Read())
            {
                items.Add(MapearEmpleado(reader));
            }

            reader.Close();

            foreach (var empleado in items)
            {
                CompletarUsuarioSistema(conexion, empleado);
                CompletarSupervisorAsignado(conexion, empleado);
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
                message = "No se pudo cargar el listado de empleados.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{id:long}")]
    public IActionResult Obtener(long id)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(conexion);
            RrhhSupport.EnsureEmployeeProfileSchema(conexion);

            var empleado = ObtenerEmpleadoInterno(conexion, id);
            if (empleado is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado.",
                });
            }

            CompletarUsuarioSistema(conexion, empleado);
            CompletarSupervisorAsignado(conexion, empleado);
            CompletarResumenLaboral(conexion, empleado);

            return Json(new
            {
                ok = true,
                data = empleado,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo obtener el empleado.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost("{id:long}")]
    [HttpPost("{id:long}/FotoPerfil")]
    [RequestSizeLimit(5242880)]
    public IActionResult SubirFotoPerfil(long id, [FromForm] IFormFile? archivo)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(conexion);
            RrhhSupport.EnsureEmployeeProfileSchema(conexion);

            var empleado = ObtenerEmpleadoInterno(conexion, id);
            if (empleado is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado.",
                });
            }

            var validationError = EmployeePhotoSupport.ValidateUpload(archivo);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = validationError,
                });
            }

            var currentPhotoUrl = EmployeePhotoSupport.GetPhotoUrl(conexion, null, id);
            var photoUrl = EmployeePhotoSupport.SavePhotoFile(
                _environment,
                archivo!,
                empleado.CodigoEmpleado ?? $"EMP-{id}");

            EmployeePhotoSupport.UpdatePhotoUrl(conexion, null, id, photoUrl);
            EmployeePhotoSupport.DeleteManagedPhoto(_environment, currentPhotoUrl, photoUrl);

            var actualizado = ObtenerEmpleadoInterno(conexion, id);
            if (actualizado is not null)
            {
                CompletarUsuarioSistema(conexion, actualizado);
                CompletarSupervisorAsignado(conexion, actualizado);
            }

            RegistrarBitacora(
                conexion,
                "FOTO_PERFIL",
                id,
                empleado.CodigoEmpleado ?? $"EMP-{id}",
                $"Se actualizo la foto del empleado {empleado.CodigoEmpleado ?? $"EMP-{id}"}.",
                new
                {
                    operador = ObtenerUsuarioOperador(),
                    empleado = empleado.CodigoEmpleado,
                    fotoPerfilUrl = photoUrl,
                });

            return Json(new
            {
                ok = true,
                message = "Foto de perfil actualizada correctamente.",
                data = (object)(actualizado is null
                    ? new { fotoPerfilUrl = photoUrl }
                    : actualizado),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar la foto del empleado.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] EmpleadoGuardarModel model)
    {
        try
        {
            var errores = ValidarEmpleado(model);
            if (errores.Count > 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Revisa los datos del formulario.",
                    errors = errores,
                });
            }

            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(conexion);
            RrhhSupport.EnsureEmployeeProfileSchema(conexion);

            ValidarDependenciasEmpleado(conexion, model, errores, null);
            ValidarSupervisorRelacionado(conexion, 0, model.IdSupervisorEmpleado, errores);
            if (errores.Count > 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Revisa los datos del formulario.",
                    errors = errores,
                });
            }

            using var comando = new SqlCommand("rrhh.usp_crear_empleado", conexion);
            comando.CommandType = CommandType.StoredProcedure;
            AsignarParametrosEmpleado(comando, model);

            var resultado = comando.ExecuteScalar();
            if (resultado is null)
            {
                throw new InvalidOperationException("La base de datos no devolvio el id del empleado.");
            }

            var empleadoId = Convert.ToInt64(resultado);
            var empleado = ObtenerEmpleadoInterno(conexion, empleadoId);
            if (empleado is null)
            {
                throw new InvalidOperationException("No se pudo leer el empleado creado.");
            }

            empleado.UsuarioSistema = CrearUsuarioSeguridadEmpleado(conexion, empleado, model.UsuarioSistema);
            RrhhSupport.ReplaceSupervisorAssignment(
                conexion,
                null,
                empleadoId,
                model.IdSupervisorEmpleado,
                ObtenerUsuarioOperador());
            CompletarSupervisorAsignado(conexion, empleado);

            if (model.IdSupervisorEmpleado.HasValue && model.IdSupervisorEmpleado.Value > 0)
            {
                AsegurarRolSupervisorPorEmpleado(conexion, model.IdSupervisorEmpleado.Value);
            }

            RegistrarBitacora(
                conexion,
                "INSERCION",
                empleado.IdEmpleado,
                empleado.CodigoEmpleado,
                $"Se creo el empleado {empleado.CodigoEmpleado}.",
                new
                {
                    operador = ObtenerUsuarioOperador(),
                    empleado,
                });

            return Json(new
            {
                ok = true,
                message = "Empleado creado correctamente.",
                data = empleado,
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                ok = false,
                message = TraducirErrorSql(ex.Message),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo crear el empleado.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult Actualizar(long id, [FromBody] EmpleadoGuardarModel model)
    {
        try
        {
            var errores = ValidarEmpleado(model);
            if (errores.Count > 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Revisa los datos del formulario.",
                    errors = errores,
                });
            }

            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(conexion);
            RrhhSupport.EnsureEmployeeProfileSchema(conexion);

            ValidarDependenciasEmpleado(conexion, model, errores, id);
            ValidarSupervisorRelacionado(conexion, id, model.IdSupervisorEmpleado, errores);
            if (errores.Count > 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Revisa los datos del formulario.",
                    errors = errores,
                });
            }

            var empleadoAnterior = ObtenerEmpleadoInterno(conexion, id);
            if (empleadoAnterior is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado.",
                });
            }

            CompletarUsuarioSistema(conexion, empleadoAnterior, model.UsuarioSistema);

            const string sql = """
                UPDATE rrhh.empleado
                SET
                    codigo_empleado = @codigo_empleado,
                    id_departamento = @id_departamento,
                    id_cargo = @id_cargo,
                    cedula = @cedula,
                    inss = @inss,
                    nombres = @nombres,
                    apellidos = @apellidos,
                    fecha_nacimiento = @fecha_nacimiento,
                    sexo = @sexo,
                    estado_civil = @estado_civil,
                    telefono = @telefono,
                    correo = @correo,
                    direccion = @direccion,
                    fecha_ingreso = @fecha_ingreso,
                    id_banco = @id_banco,
                    numero_cuenta_bancaria = @numero_cuenta_bancaria,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_empleado = @id_empleado;
                """;

            using var comando = new SqlCommand(sql, conexion);
            AsignarParametrosEmpleado(comando, model);
            comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = id;

            var filas = comando.ExecuteNonQuery();
            if (filas == 0)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado.",
                });
            }

            var empleado = ObtenerEmpleadoInterno(conexion, id);
            if (empleado is null)
            {
                throw new InvalidOperationException("No se pudo leer el empleado actualizado.");
            }

            SincronizarUsuarioSeguridadEmpleado(conexion, empleadoAnterior, empleado, model.UsuarioSistema);
            CompletarUsuarioSistema(conexion, empleado, model.UsuarioSistema);
            RrhhSupport.ReplaceSupervisorAssignment(
                conexion,
                null,
                id,
                model.IdSupervisorEmpleado,
                ObtenerUsuarioOperador());
            CompletarSupervisorAsignado(conexion, empleado);

            if (model.IdSupervisorEmpleado.HasValue && model.IdSupervisorEmpleado.Value > 0)
            {
                AsegurarRolSupervisorPorEmpleado(conexion, model.IdSupervisorEmpleado.Value);
            }

            RegistrarBitacora(
                conexion,
                "MODIFICACION",
                empleado.IdEmpleado,
                empleado.CodigoEmpleado,
                $"Se modifico el empleado {empleado.CodigoEmpleado}.",
                new
                {
                    operador = ObtenerUsuarioOperador(),
                    empleado,
                });

            return Json(new
            {
                ok = true,
                message = "Empleado actualizado correctamente.",
                data = empleado,
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                ok = false,
                message = TraducirErrorSql(ex.Message),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar el empleado.",
                detail = ex.Message,
            });
        }
    }

    [HttpDelete("{id:long}")]
    public IActionResult Eliminar(long id, [FromBody] EmpleadoEliminarModel model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.AdminUsuario) || string.IsNullOrWhiteSpace(model.AdminPassword))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Debes ingresar usuario y contrasena de administrador.",
                });
            }

            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(conexion);

            var autorizacion = ValidarAdministrador(conexion, model.AdminUsuario, model.AdminPassword);
            if (!autorizacion.Ok)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = autorizacion.Message,
                });
            }

            var empleado = ObtenerEmpleadoInterno(conexion, id);
            if (empleado is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado.",
                });
            }

            var referencias = ObtenerReferenciasEmpleado(conexion, id);
            if (referencias.Count > 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "No se puede eliminar porque el empleado tiene registros relacionados.",
                    data = referencias,
                });
            }

            using var comando = new SqlCommand("DELETE FROM rrhh.empleado WHERE id_empleado = @id_empleado;", conexion);
            comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = id;

            var filas = comando.ExecuteNonQuery();
            if (filas == 0)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado.",
                });
            }

            RegistrarBitacora(
                conexion,
                "ELIMINACION",
                empleado.IdEmpleado,
                empleado.CodigoEmpleado,
                $"Se elimino el empleado {empleado.CodigoEmpleado}.",
                new
                {
                    operador = ObtenerUsuarioOperador(),
                    administrador = autorizacion.UsuarioAdministrador,
                    empleado,
                },
                autorizacion.UsuarioAdministrador);

            return Json(new
            {
                ok = true,
                message = "Empleado eliminado correctamente.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo eliminar el empleado.",
                detail = ex.Message,
            });
        }
    }

    private EmpleadoDto? ObtenerEmpleadoInterno(SqlConnection conexion, long id)
    {
        const string sql = """
            SELECT
                e.id_empleado,
                e.codigo_empleado,
                e.cedula,
                e.nombres,
                e.apellidos,
                e.nombre_completo,
                e.foto_perfil_url,
                d.id_departamento,
                d.nombre_departamento,
                c.id_cargo,
                c.nombre_cargo,
                ee.id_estado_empleado,
                ee.codigo_estado_empleado,
                ee.nombre_estado_empleado,
                e.fecha_ingreso,
                e.fecha_nacimiento,
                e.telefono,
                e.correo,
                e.sexo,
                e.estado_civil,
                e.direccion,
                e.id_banco,
                b.nombre_banco,
                e.numero_cuenta_bancaria,
                e.inss,
                e.activo,
                e.fecha_baja,
                e.motivo_baja,
                e.fecha_registro,
                e.fecha_actualizacion
            FROM rrhh.empleado e
            INNER JOIN rrhh.departamento d ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.estado_empleado ee ON ee.id_estado_empleado = e.id_estado_empleado
            LEFT JOIN rrhh.banco b ON b.id_banco = e.id_banco
            WHERE e.id_empleado = @id_empleado;
            """;

        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = id;

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapearEmpleado(reader);
    }

    private void CompletarUsuarioSistema(SqlConnection conexion, EmpleadoDto empleado, string? preferredUsername = null)
    {
        var usuario = BuscarUsuarioSeguridadRelacionado(conexion, empleado, preferredUsername);
        empleado.UsuarioSistema = usuario?.Usuario;
    }

    private void CompletarSupervisorAsignado(SqlConnection conexion, EmpleadoDto empleado)
    {
        var supervisor = RrhhSupport.GetActiveSupervisor(conexion, null, empleado.IdEmpleado);
        if (supervisor is null)
        {
            empleado.IdSupervisorEmpleado = null;
            empleado.CodigoSupervisorEmpleado = null;
            empleado.NombreSupervisorEmpleado = null;
            empleado.UsuarioSupervisor = null;
            return;
        }

        empleado.IdSupervisorEmpleado = supervisor.IdSupervisorEmpleado;
        empleado.CodigoSupervisorEmpleado = supervisor.CodigoSupervisorEmpleado;
        empleado.NombreSupervisorEmpleado = supervisor.NombreSupervisorEmpleado;
        empleado.UsuarioSupervisor = supervisor.UsuarioSupervisor;
    }

    private string CrearUsuarioSeguridadEmpleado(SqlConnection conexion, EmpleadoDto empleado, string? preferredUsername = null)
    {
        var usuarioRelacionado = BuscarUsuarioSeguridadRelacionado(conexion, empleado, preferredUsername);
        if (usuarioRelacionado is not null)
        {
            ActualizarDatosUsuarioRelacionado(conexion, usuarioRelacionado.IdUsuario, empleado);
            AsegurarRolRelacionado(conexion, usuarioRelacionado.IdUsuario, empleado);
            return usuarioRelacionado.Usuario;
        }

        var username = PrepararUsernameEmpleado(conexion, empleado, preferredUsername);

        using (var comando = new SqlCommand(
            """
            INSERT INTO seguridad.usuario
            (
                id_sucursal,
                usuario,
                nombres,
                apellidos,
                correo,
                telefono,
                hash_clave,
                cambiar_clave_en_proximo_inicio,
                bloqueado,
                activo,
                intentos_fallidos,
                fecha_registro
            )
            OUTPUT INSERTED.id_usuario
            VALUES
            (
                NULL,
                @usuario,
                @nombres,
                @apellidos,
                @correo,
                @telefono,
                @hash_clave,
                1,
                0,
                1,
                0,
                SYSDATETIME()
            );
            """,
            conexion))
        {
            comando.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = username;
            comando.Parameters.Add("@nombres", SqlDbType.NVarChar, 300).Value = empleado.Nombres;
            comando.Parameters.Add("@apellidos", SqlDbType.NVarChar, 300).Value = empleado.Apellidos;
            comando.Parameters.Add("@correo", SqlDbType.NVarChar, 300).Value = ToDbValue(empleado.Correo);
            comando.Parameters.Add("@telefono", SqlDbType.NVarChar, 100).Value = ToDbValue(empleado.Telefono);
            comando.Parameters.Add("@hash_clave", SqlDbType.NVarChar, 1000).Value =
                SecuritySupport.HashPassword(username);

            var insertedId = comando.ExecuteScalar();
            if (insertedId is not null)
            {
                AsegurarRolRelacionado(conexion, Convert.ToInt64(insertedId), empleado);
            }
        }

        return username;
    }

    private void SincronizarUsuarioSeguridadEmpleado(
        SqlConnection conexion,
        EmpleadoDto empleadoAnterior,
        EmpleadoDto empleadoActual,
        string? preferredUsername = null)
    {
        var usuarioRelacionado =
            BuscarUsuarioSeguridadRelacionado(conexion, empleadoAnterior, preferredUsername) ??
            BuscarUsuarioSeguridadRelacionado(conexion, empleadoActual, preferredUsername);

        if (usuarioRelacionado is null)
        {
            empleadoActual.UsuarioSistema = CrearUsuarioSeguridadEmpleado(conexion, empleadoActual, preferredUsername);
            return;
        }

        ActualizarDatosUsuarioRelacionado(conexion, usuarioRelacionado.IdUsuario, empleadoActual);
        AsegurarRolRelacionado(conexion, usuarioRelacionado.IdUsuario, empleadoActual);
        empleadoActual.UsuarioSistema = usuarioRelacionado.Usuario;
    }

    private SecurityUserLink? BuscarUsuarioSeguridadRelacionado(
        SqlConnection conexion,
        EmpleadoDto empleado,
        string? preferredUsername = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredUsername))
        {
            using var comandoPorUsuario = new SqlCommand(
                """
                SELECT TOP (1) id_usuario, usuario
                FROM seguridad.usuario
                WHERE usuario = @usuario;
                """,
                conexion);
            comandoPorUsuario.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = preferredUsername.Trim();

            using var readerPorUsuario = comandoPorUsuario.ExecuteReader();
            if (readerPorUsuario.Read())
            {
                return new SecurityUserLink
                {
                    IdUsuario = readerPorUsuario.GetInt64(0),
                    Usuario = readerPorUsuario.GetString(1),
                };
            }
        }

        if (string.IsNullOrWhiteSpace(empleado.Correo))
        {
            return null;
        }

        using var comando = new SqlCommand(
            """
            SELECT TOP (1) id_usuario, usuario
            FROM seguridad.usuario
            WHERE correo = @correo
            ORDER BY id_usuario DESC;
            """,
            conexion);

        comando.Parameters.Add("@correo", SqlDbType.NVarChar, 300).Value = empleado.Correo?.Trim() ?? string.Empty;

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SecurityUserLink
        {
            IdUsuario = reader.GetInt64(0),
            Usuario = reader.GetString(1),
        };
    }

    private void ActualizarDatosUsuarioRelacionado(SqlConnection conexion, long idUsuario, EmpleadoDto empleado)
    {
        using var comando = new SqlCommand(
            """
            UPDATE seguridad.usuario
            SET
                nombres = @nombres,
                apellidos = @apellidos,
                correo = @correo,
                telefono = @telefono,
                fecha_actualizacion = SYSDATETIME()
            WHERE id_usuario = @id_usuario;
            """,
            conexion);

        comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
        comando.Parameters.Add("@nombres", SqlDbType.NVarChar, 300).Value = empleado.Nombres.Trim();
        comando.Parameters.Add("@apellidos", SqlDbType.NVarChar, 300).Value = empleado.Apellidos.Trim();
        comando.Parameters.Add("@correo", SqlDbType.NVarChar, 300).Value = ToDbValue(empleado.Correo);
        comando.Parameters.Add("@telefono", SqlDbType.NVarChar, 100).Value = ToDbValue(empleado.Telefono);
        comando.ExecuteNonQuery();
    }

    private string PrepararUsernameEmpleado(SqlConnection conexion, EmpleadoDto empleado, string? preferredUsername = null)
    {
        var usernamePreferido = SecuritySupport.NormalizeUsername(preferredUsername);
        if (!string.IsNullOrWhiteSpace(usernamePreferido) && !UsuarioSeguridadExiste(conexion, usernamePreferido))
        {
            return usernamePreferido;
        }

        return SecuritySupport.GenerateUniqueUsername(
            empleado.Nombres,
            empleado.Apellidos,
            candidate => UsuarioSeguridadExiste(conexion, candidate));
    }

    private bool UsuarioSeguridadExiste(SqlConnection conexion, string username)
    {
        using var comando = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM seguridad.usuario
            WHERE usuario = @usuario;
            """,
            conexion);
        comando.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = username;
        return Convert.ToInt32(comando.ExecuteScalar()) > 0;
    }

    private void AsegurarRolRelacionado(SqlConnection conexion, long idUsuario, EmpleadoDto empleado)
    {
        var rol = ResolverRolRelacionado(conexion, empleado);
        if (rol is null)
        {
            return;
        }

        AsegurarRolEspecifico(conexion, idUsuario, rol.IdRol);
    }

    private void AsegurarRolSupervisorPorEmpleado(SqlConnection conexion, long idEmpleadoSupervisor)
    {
        var supervisor = ObtenerEmpleadoInterno(conexion, idEmpleadoSupervisor);
        if (supervisor is null)
        {
            return;
        }

        var usuarioRelacionado = BuscarUsuarioSeguridadRelacionado(conexion, supervisor);
        if (usuarioRelacionado is null)
        {
            return;
        }

        AsegurarRolEspecifico(conexion, usuarioRelacionado.IdUsuario, "SUPERVISOR");
    }

    private void AsegurarRolEspecifico(SqlConnection conexion, long idUsuario, string codigoRol)
    {
        using var buscarRol = new SqlCommand(
            """
            SELECT TOP (1) id_rol
            FROM seguridad.rol
            WHERE activo = 1
              AND codigo_rol = @codigo_rol;
            """,
            conexion);
        buscarRol.Parameters.Add("@codigo_rol", SqlDbType.NVarChar, 50).Value = codigoRol;

        var roleId = buscarRol.ExecuteScalar();
        if (roleId is null || roleId == DBNull.Value)
        {
            return;
        }

        AsegurarRolEspecifico(conexion, idUsuario, Convert.ToInt64(roleId));
    }

    private void AsegurarRolEspecifico(SqlConnection conexion, long idUsuario, long idRol)
    {
        using var verificador = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM seguridad.usuario_rol
            WHERE id_usuario = @id_usuario
              AND id_rol = @id_rol
              AND activo = 1;
            """,
            conexion);
        verificador.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
        verificador.Parameters.Add("@id_rol", SqlDbType.BigInt).Value = idRol;

        if (Convert.ToInt32(verificador.ExecuteScalar()) > 0)
        {
            return;
        }

        using var comando = new SqlCommand(
            """
            INSERT INTO seguridad.usuario_rol
            (
                id_usuario,
                id_rol,
                activo,
                fecha_registro
            )
            VALUES
            (
                @id_usuario,
                @id_rol,
                1,
                SYSDATETIME()
            );
            """,
            conexion);

        comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
        comando.Parameters.Add("@id_rol", SqlDbType.BigInt).Value = idRol;
        comando.ExecuteNonQuery();
    }

    private RoleMatch? ResolverRolRelacionado(SqlConnection conexion, EmpleadoDto empleado)
    {
        var candidatos = new[]
        {
            SecuritySupport.NormalizeUsername(empleado.NombreCargo),
            SecuritySupport.NormalizeUsername(empleado.NombreDepartamento),
        }
        .Where(valor => !string.IsNullOrWhiteSpace(valor))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        if (candidatos.Length == 0)
        {
            return null;
        }

        using var comando = new SqlCommand(
            """
            SELECT id_rol, codigo_rol, nombre_rol
            FROM seguridad.rol
            WHERE activo = 1;
            """,
            conexion);

        using var reader = comando.ExecuteReader();
        while (reader.Read())
        {
            var match = new RoleMatch
            {
                IdRol = reader.GetInt64(0),
                CodigoRol = reader.GetString(1),
                NombreRol = reader.GetString(2),
            };

            var codigoNormalizado = SecuritySupport.NormalizeUsername(match.CodigoRol);
            var nombreNormalizado = SecuritySupport.NormalizeUsername(match.NombreRol);

            if (candidatos.Any(candidato =>
                    string.Equals(candidato, codigoNormalizado, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidato, nombreNormalizado, StringComparison.OrdinalIgnoreCase)))
            {
                return match;
            }
        }

        return null;
    }

    private void RegistrarBitacora(
        SqlConnection conexion,
        string tipoEvento,
        long idReferencia,
        string referenciaTexto,
        string descripcion,
        object resumen,
        string? usuarioRegistro = null)
    {
        using var comando = new SqlCommand("operacion.usp_registrar_bitacora_operativa", conexion);
        comando.CommandType = CommandType.StoredProcedure;
        comando.Parameters.Add("@modulo", SqlDbType.NVarChar, 50).Value = "RRHH";
        comando.Parameters.Add("@proceso", SqlDbType.NVarChar, 100).Value = "EMPLEADOS";
        comando.Parameters.Add("@tipo_evento", SqlDbType.NVarChar, 50).Value = tipoEvento;
        comando.Parameters.Add("@id_referencia", SqlDbType.BigInt).Value = idReferencia;
        comando.Parameters.Add("@referencia_texto", SqlDbType.NVarChar, 100).Value = referenciaTexto;
        comando.Parameters.Add("@descripcion_evento", SqlDbType.NVarChar, 1000).Value = descripcion;
        comando.Parameters.Add("@datos_resumen", SqlDbType.NVarChar).Value = JsonSerializer.Serialize(resumen);
        comando.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value =
            usuarioRegistro ?? ObtenerUsuarioOperador();
        comando.Parameters.Add("@equipo", SqlDbType.NVarChar, 100).Value = Environment.MachineName;
        comando.Parameters.Add("@ip_equipo", SqlDbType.NVarChar, 50).Value =
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "LOCAL";
        comando.ExecuteNonQuery();
    }

    private void ValidarSupervisorRelacionado(
        SqlConnection conexion,
        long idEmpleado,
        long? idSupervisorEmpleado,
        Dictionary<string, string> errores)
    {
        if (!idSupervisorEmpleado.HasValue || idSupervisorEmpleado.Value <= 0)
        {
            return;
        }

        if (idEmpleado > 0 && idSupervisorEmpleado.Value == idEmpleado)
        {
            errores["idSupervisorEmpleado"] = "El jefe inmediato no puede ser el mismo colaborador.";
            return;
        }

        using var comando = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.id_empleado = @id_empleado
              AND e.activo = 1
              AND e.fecha_baja IS NULL
              AND ISNULL(ee.codigo_estado_empleado, N'') <> N'RETIRADO';
            """,
            conexion);
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado.Value;

        if (Convert.ToInt32(comando.ExecuteScalar()) == 0)
        {
            errores["idSupervisorEmpleado"] = "Selecciona un jefe inmediato valido y activo.";
            return;
        }

        if (idEmpleado > 0 &&
            RrhhSupport.WouldCreateSupervisorCycle(conexion, null, idEmpleado, idSupervisorEmpleado.Value))
        {
            errores["idSupervisorEmpleado"] = "La asignacion crea un ciclo de supervision. Revisa la jefatura seleccionada.";
        }
    }

    private void ValidarDependenciasEmpleado(
        SqlConnection conexion,
        EmpleadoGuardarModel model,
        Dictionary<string, string> errores,
        long? currentId)
    {
        if (!ExisteCatalogoActivo(conexion, "rrhh.departamento", "id_departamento", model.IdDepartamento))
        {
            errores["idDepartamento"] = "Selecciona un departamento valido.";
        }

        if (!ExisteCatalogoActivo(conexion, "rrhh.cargo", "id_cargo", model.IdCargo))
        {
            errores["idCargo"] = "Selecciona un cargo valido.";
        }

        if (!ExisteCatalogoActivo(conexion, "rrhh.banco", "id_banco", model.IdBanco))
        {
            errores["idBanco"] = "Selecciona un banco valido.";
        }

        if (!errores.ContainsKey("idDepartamento") &&
            !errores.ContainsKey("idCargo") &&
            !CargoPerteneceADepartamento(conexion, model.IdCargo, model.IdDepartamento))
        {
            errores["idCargo"] = "El cargo seleccionado no pertenece al departamento indicado.";
        }

        if (!errores.ContainsKey("codigoEmpleado") &&
            ValorEmpleadoDuplicado(conexion, "codigo_empleado", model.CodigoEmpleado.Trim().ToUpperInvariant(), currentId))
        {
            errores["codigoEmpleado"] = "El codigo de empleado ya existe.";
        }

        if (!errores.ContainsKey("cedula") &&
            ValorEmpleadoDuplicado(conexion, "cedula", model.Cedula.Trim().ToUpperInvariant(), currentId))
        {
            errores["cedula"] = "La cedula ya existe.";
        }

        if (!errores.ContainsKey("inss") &&
            ValorEmpleadoDuplicado(conexion, "inss", model.Inss?.Trim().ToUpperInvariant(), currentId))
        {
            errores["inss"] = "El INSS ya existe.";
        }

        if (!errores.ContainsKey("correo") &&
            ValorEmpleadoDuplicado(conexion, "correo", model.Correo?.Trim().ToLowerInvariant(), currentId))
        {
            errores["correo"] = "El correo ya esta asignado a otro colaborador.";
        }

        if (!errores.ContainsKey("numeroCuentaBancaria") &&
            ValorEmpleadoDuplicado(conexion, "numero_cuenta_bancaria", model.NumeroCuentaBancaria?.Trim(), currentId))
        {
            errores["numeroCuentaBancaria"] = "La cuenta bancaria ya esta registrada en otro colaborador.";
        }
    }

    private static bool ExisteCatalogoActivo(SqlConnection conexion, string tabla, string columnaId, long? id)
    {
        if (!id.HasValue || id.Value <= 0)
        {
            return false;
        }

        using var comando = new SqlCommand(
            $"SELECT COUNT(1) FROM {tabla} WHERE {columnaId} = @id AND activo = 1;",
            conexion);
        comando.Parameters.Add("@id", SqlDbType.BigInt).Value = id.Value;
        return Convert.ToInt32(comando.ExecuteScalar()) > 0;
    }

    private static bool CargoPerteneceADepartamento(SqlConnection conexion, long idCargo, long idDepartamento)
    {
        using var comando = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.cargo
            WHERE id_cargo = @id_cargo
              AND activo = 1
              AND (id_departamento IS NULL OR id_departamento = @id_departamento);
            """,
            conexion);
        comando.Parameters.Add("@id_cargo", SqlDbType.BigInt).Value = idCargo;
        comando.Parameters.Add("@id_departamento", SqlDbType.BigInt).Value = idDepartamento;
        return Convert.ToInt32(comando.ExecuteScalar()) > 0;
    }

    private static bool ValorEmpleadoDuplicado(SqlConnection conexion, string columna, string? valor, long? currentId)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        using var comando = new SqlCommand(
            $"""
            SELECT COUNT(1)
            FROM rrhh.empleado
            WHERE {columna} = @valor
              AND (@id_actual IS NULL OR id_empleado <> @id_actual);
            """,
            conexion);
        comando.Parameters.Add("@valor", SqlDbType.NVarChar, 300).Value = valor.Trim();
        comando.Parameters.Add("@id_actual", SqlDbType.BigInt).Value = currentId.HasValue ? currentId.Value : DBNull.Value;
        return Convert.ToInt32(comando.ExecuteScalar()) > 0;
    }

    private List<ReferenciaRelacionadaDto> ObtenerReferenciasEmpleado(SqlConnection conexion, long idEmpleado)
    {
        const string sql = """
            SELECT
                QUOTENAME(OBJECT_SCHEMA_NAME(fkc.parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(fkc.parent_object_id)) AS tabla,
                pc.name AS columna
            FROM sys.foreign_key_columns fkc
            INNER JOIN sys.columns pc
                ON pc.object_id = fkc.parent_object_id
               AND pc.column_id = fkc.parent_column_id
            WHERE fkc.referenced_object_id = OBJECT_ID('rrhh.empleado');
            """;

        var referencias = new List<ReferenciaRelacionadaDto>();

        using var comando = new SqlCommand(sql, conexion);
        using var reader = comando.ExecuteReader();

        var validaciones = new List<(string Tabla, string Columna)>();
        while (reader.Read())
        {
            validaciones.Add((reader.GetString(0), reader.GetString(1)));
        }

        reader.Close();

        foreach (var validacion in validaciones)
        {
            using var verificador = new SqlCommand(
                $"SELECT COUNT(1) FROM {validacion.Tabla} WHERE {validacion.Columna} = @id_empleado;",
                conexion);
            verificador.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

            var total = Convert.ToInt32(verificador.ExecuteScalar());
            if (total > 0)
            {
                referencias.Add(new ReferenciaRelacionadaDto
                {
                    Table = validacion.Tabla,
                    Total = total,
                });
            }
        }

        return referencias;
    }

    private void CompletarResumenLaboral(SqlConnection conexion, EmpleadoDto empleado)
    {
        const string sql = """
            DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
            DECLARE @limite DATE = DATEADD(DAY, 30, @hoy);

            SELECT TOP (1)
                c.numero_contrato,
                tc.nombre_tipo_contrato,
                c.fecha_inicio,
                c.fecha_fin,
                c.salario_base_mensual,
                c.moneda,
                c.es_contrato_vigente
            FROM rrhh.contrato c
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            WHERE c.id_empleado = @id_empleado
            ORDER BY
                CASE
                    WHEN c.es_contrato_vigente = 1 THEN 0
                    ELSE 1
                END,
                c.fecha_inicio DESC,
                c.id_contrato DESC;

            SELECT
                (SELECT COUNT(1) FROM rrhh.contrato WHERE id_empleado = @id_empleado) AS total_contratos,
                (SELECT COUNT(1) FROM rrhh.accion_personal WHERE id_empleado = @id_empleado) AS total_acciones,
                (SELECT COUNT(1) FROM rrhh.expediente_documento WHERE id_empleado = @id_empleado) AS total_expedientes,
                (
                    SELECT COUNT(1)
                    FROM rrhh.empleado_supervision rel
                    INNER JOIN rrhh.empleado sub
                        ON sub.id_empleado = rel.id_empleado
                    WHERE rel.id_supervisor_empleado = @id_empleado
                      AND rel.activo = 1
                      AND sub.activo = 1
                ) AS total_subordinados,
                (
                    SELECT COUNT(1)
                    FROM rrhh.expediente_documento
                    WHERE id_empleado = @id_empleado
                      AND fecha_vencimiento IS NOT NULL
                      AND fecha_vencimiento < @hoy
                ) AS expedientes_vencidos,
                (
                    SELECT COUNT(1)
                    FROM rrhh.expediente_documento
                    WHERE id_empleado = @id_empleado
                      AND fecha_vencimiento IS NOT NULL
                      AND fecha_vencimiento BETWEEN @hoy AND @limite
                ) AS expedientes_por_vencer,
                (
                    SELECT 0
                ) AS permisos_pendientes,
                (
                    SELECT COUNT(1)
                    FROM rrhh.vacacion
                    WHERE id_empleado = @id_empleado
                      AND estado_vacacion = N'SOLICITADA'
                ) AS vacaciones_pendientes,
                (
                    SELECT COUNT(1)
                    FROM rrhh.hora_extra
                    WHERE id_empleado = @id_empleado
                      AND estado_hora_extra = N'REGISTRADA'
                ) AS horas_extra_pendientes,
                (
                    SELECT COUNT(1)
                    FROM rrhh.marcacion_reloj
                    WHERE id_empleado = @id_empleado
                      AND fecha_operacion = @hoy
                ) AS marcaciones_hoy;

            SELECT TOP (1)
                m.fecha_hora_marcacion,
                m.tipo_marcacion
            FROM rrhh.marcacion_reloj m
            WHERE m.id_empleado = @id_empleado
            ORDER BY m.fecha_hora_marcacion DESC, m.id_marcacion_reloj DESC;
            """;

        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = empleado.IdEmpleado;

        using var reader = comando.ExecuteReader();

        var resumen = new EmpleadoResumenDto();

        if (reader.Read())
        {
            resumen.ContratoVigenteNumero = reader.IsDBNull(0) ? null : reader.GetString(0);
            resumen.ContratoVigenteTipo = reader.IsDBNull(1) ? null : reader.GetString(1);
            resumen.ContratoVigenteInicio = reader.IsDBNull(2) ? null : reader.GetDateTime(2).ToString("yyyy-MM-dd");
            resumen.ContratoVigenteHasta = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy-MM-dd");
            resumen.SalarioBaseMensual = reader.IsDBNull(4) ? null : reader.GetDecimal(4);
            resumen.MonedaContrato = reader.IsDBNull(5) ? null : reader.GetString(5);
            resumen.TieneContratoVigente = !reader.IsDBNull(6) && reader.GetBoolean(6);
        }

        reader.NextResult();
        if (reader.Read())
        {
            resumen.TotalContratos = GetSafeInt32(reader, 0);
            resumen.TotalAcciones = GetSafeInt32(reader, 1);
            resumen.TotalExpedientes = GetSafeInt32(reader, 2);
            resumen.TotalSubordinados = GetSafeInt32(reader, 3);
            resumen.ExpedientesVencidos = GetSafeInt32(reader, 4);
            resumen.ExpedientesPorVencer = GetSafeInt32(reader, 5);
            resumen.PermisosPendientes = GetSafeInt32(reader, 6);
            resumen.VacacionesPendientes = GetSafeInt32(reader, 7);
            resumen.HorasExtraPendientes = GetSafeInt32(reader, 8);
            resumen.MarcacionesHoy = GetSafeInt32(reader, 9);
        }

        reader.NextResult();
        if (reader.Read())
        {
            resumen.UltimaMarcacionFechaHora = reader.IsDBNull(0)
                ? null
                : reader.GetDateTime(0).ToString("yyyy-MM-ddTHH:mm:ss");
            resumen.UltimaMarcacionTipo = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        reader.Close();

        var vacaciones = RrhhSupport.CalculateVacationBalance(conexion, null, empleado.IdEmpleado, DateTime.Today);
        resumen.DiasVacacionesAcumulados = vacaciones.DiasAcumulados;
        resumen.DiasVacacionesTomados = vacaciones.DiasTomadosVacacion;
        resumen.DiasPermisosDescontados = 0;
        resumen.DiasVacacionesDisponibles = vacaciones.DiasDisponibles;
        resumen.DiasVacacionesPendientesDias = vacaciones.DiasPendientesVacacion;
        resumen.DiasPermisosPendientesDias = 0;

        empleado.ResumenLaboral = resumen;
    }

    private static int GetSafeInt32(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private (bool Ok, string Message, string UsuarioAdministrador) ValidarAdministrador(
        SqlConnection conexion,
        string usuario,
        string password)
    {
        const string sql = """
            SELECT TOP (1)
                u.usuario,
                u.hash_clave
            FROM seguridad.usuario u
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = u.id_usuario
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
            WHERE
                u.usuario = @usuario
                AND u.activo = 1
                AND u.bloqueado = 0
                AND r.codigo_rol = 'ADMINISTRADOR';
            """;

        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = usuario.Trim();

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return (false, "El usuario no tiene permisos de administrador.", string.Empty);
        }

        var usuarioAdministrador = reader.GetString(0);
        var hash = reader.GetString(1);
        reader.Close();

        if (!SecuritySupport.VerifyPassword(password, hash))
        {
            return (false, "La contrasena del administrador es incorrecta.", string.Empty);
        }

        return (true, string.Empty, usuarioAdministrador);
    }

    private string ObtenerUsuarioOperador()
    {
        var usuario = Request.Headers["X-Operator-User"].ToString().Trim();
        return string.IsNullOrWhiteSpace(usuario) ? "sistema.local" : usuario;
    }

    private static Dictionary<string, string> ValidarEmpleado(EmpleadoGuardarModel model)
    {
        var errores = new Dictionary<string, string>();
        var estadosCivilesValidos = new[] { "SOLTERO", "CASADO", "UNION DE HECHO", "DIVORCIADO", "VIUDO" };
        var fechaMinimaBase = new DateTime(1753, 1, 1);

        if (string.IsNullOrWhiteSpace(model.CodigoEmpleado) ||
            !Regex.IsMatch(model.CodigoEmpleado.Trim().ToUpperInvariant(), "^[A-Z0-9-]{4,30}$"))
        {
            errores["codigoEmpleado"] = "Codigo invalido.";
        }

        if (model.IdDepartamento <= 0)
        {
            errores["idDepartamento"] = "Selecciona un departamento.";
        }

        if (model.IdCargo <= 0)
        {
            errores["idCargo"] = "Selecciona un cargo.";
        }

        if (string.IsNullOrWhiteSpace(model.Cedula) ||
            !Regex.IsMatch(model.Cedula.Trim().ToUpperInvariant(), "^\\d{3}-\\d{6}-\\d{4}[A-Z]$"))
        {
            errores["cedula"] = "Cedula invalida.";
        }

        if (string.IsNullOrWhiteSpace(model.Nombres) ||
            !Regex.IsMatch(model.Nombres.Trim(), "^[A-Za-zÀ-ÿ ]{2,150}$"))
        {
            errores["nombres"] = "Nombres invalidos.";
        }

        if (string.IsNullOrWhiteSpace(model.Apellidos) ||
            !Regex.IsMatch(model.Apellidos.Trim(), "^[A-Za-zÀ-ÿ ]{2,150}$"))
        {
            errores["apellidos"] = "Apellidos invalidos.";
        }

        if (!DateTime.TryParse(model.FechaIngreso, out var fechaIngreso))
        {
            errores["fechaIngreso"] = "La fecha de ingreso es obligatoria.";
        }
        else if (fechaIngreso.Date < fechaMinimaBase)
        {
            errores["fechaIngreso"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }
        else if (fechaIngreso.Date > DateTime.Today)
        {
            errores["fechaIngreso"] = "La fecha de ingreso no puede ser futura.";
        }

        if (string.IsNullOrWhiteSpace(model.FechaNacimiento))
        {
            errores["fechaNacimiento"] = "La fecha de nacimiento es obligatoria.";
        }
        else
        {
            if (!DateTime.TryParse(model.FechaNacimiento, out var fechaNacimiento))
            {
                errores["fechaNacimiento"] = "Fecha de nacimiento invalida.";
            }
            else
            {
                if (fechaNacimiento.Date < fechaMinimaBase)
                {
                    errores["fechaNacimiento"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
                }
                else if (fechaNacimiento.Date > DateTime.Today)
                {
                    errores["fechaNacimiento"] = "La fecha de nacimiento no puede ser futura.";
                }
                else if (DateTime.TryParse(model.FechaIngreso, out var ingresoValido) &&
                         fechaNacimiento.Date >= ingresoValido.Date)
                {
                    errores["fechaNacimiento"] = "La fecha de nacimiento debe ser menor a la fecha de ingreso.";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(model.Telefono))
        {
            errores["telefono"] = "El telefono es obligatorio.";
        }
        else if (!Regex.IsMatch(model.Telefono.Trim(), "^\\d{4}-\\d{4}$"))
        {
            errores["telefono"] = "Telefono invalido.";
        }

        if (string.IsNullOrWhiteSpace(model.Inss))
        {
            errores["inss"] = "El INSS es obligatorio.";
        }
        else if (!Regex.IsMatch(model.Inss.Trim().ToUpperInvariant(), "^[A-Z0-9-]{4,20}$"))
        {
            errores["inss"] = "INSS invalido.";
        }

        if (!model.IdBanco.HasValue || model.IdBanco.Value <= 0)
        {
            errores["idBanco"] = "Selecciona el banco.";
        }

        if (string.IsNullOrWhiteSpace(model.NumeroCuentaBancaria))
        {
            errores["numeroCuentaBancaria"] = "Ingresa la cuenta bancaria.";
        }
        else if (!Regex.IsMatch(model.NumeroCuentaBancaria.Trim(), "^\\d{6,30}$"))
        {
            errores["numeroCuentaBancaria"] = "Cuenta bancaria invalida.";
        }

        if (string.IsNullOrWhiteSpace(model.Correo))
        {
            errores["correo"] = "El correo es obligatorio.";
        }
        else if (!Regex.IsMatch(model.Correo.Trim(), "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"))
        {
            errores["correo"] = "Correo invalido.";
        }

        if (string.IsNullOrWhiteSpace(model.Sexo))
        {
            errores["sexo"] = "Selecciona el sexo.";
        }
        else
        {
            var sexo = model.Sexo.Trim().ToUpperInvariant();
            if (sexo != "F" && sexo != "M" && sexo != "FEMENINO" && sexo != "MASCULINO")
            {
                errores["sexo"] = "Sexo invalido.";
            }
        }

        if (string.IsNullOrWhiteSpace(model.EstadoCivil))
        {
            errores["estadoCivil"] = "Selecciona el estado civil.";
        }
        else
        {
            var estadoCivil = model.EstadoCivil.Trim().ToUpperInvariant();
            if (Array.IndexOf(estadosCivilesValidos, estadoCivil) < 0)
            {
                errores["estadoCivil"] = "Estado civil invalido.";
            }
        }

        if (string.IsNullOrWhiteSpace(model.Direccion))
        {
            errores["direccion"] = "La direccion es obligatoria.";
        }
        else if (model.Direccion.Trim().Length > 300)
        {
            errores["direccion"] = "La direccion supera el limite permitido.";
        }

        return errores;
    }

    private static EmpleadoDto MapearEmpleado(SqlDataReader reader)
    {
        var nombres = reader.GetString(reader.GetOrdinal("nombres"));
        var apellidos = reader.GetString(reader.GetOrdinal("apellidos"));

        return new EmpleadoDto
        {
            IdEmpleado = reader.GetInt64(reader.GetOrdinal("id_empleado")),
            CodigoEmpleado = reader.GetString(reader.GetOrdinal("codigo_empleado")),
            FotoPerfilUrl = reader.IsDBNull(reader.GetOrdinal("foto_perfil_url"))
                ? null
                : reader.GetString(reader.GetOrdinal("foto_perfil_url")),
            Cedula = reader.GetString(reader.GetOrdinal("cedula")),
            Nombres = nombres,
            Apellidos = apellidos,
            NombreCompleto = reader.IsDBNull(reader.GetOrdinal("nombre_completo"))
                ? $"{nombres} {apellidos}".Trim()
                : reader.GetString(reader.GetOrdinal("nombre_completo")),
            IdDepartamento = reader.GetInt64(reader.GetOrdinal("id_departamento")),
            NombreDepartamento = reader.GetString(reader.GetOrdinal("nombre_departamento")),
            IdCargo = reader.GetInt64(reader.GetOrdinal("id_cargo")),
            NombreCargo = reader.GetString(reader.GetOrdinal("nombre_cargo")),
            IdEstadoEmpleado = reader.GetInt64(reader.GetOrdinal("id_estado_empleado")),
            CodigoEstadoEmpleado = reader.GetString(reader.GetOrdinal("codigo_estado_empleado")),
            NombreEstadoEmpleado = reader.GetString(reader.GetOrdinal("nombre_estado_empleado")),
            FechaIngreso = reader.GetDateTime(reader.GetOrdinal("fecha_ingreso")).ToString("yyyy-MM-dd"),
            FechaNacimiento = reader.IsDBNull(reader.GetOrdinal("fecha_nacimiento"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("fecha_nacimiento")).ToString("yyyy-MM-dd"),
            Telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString(reader.GetOrdinal("telefono")),
            Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString(reader.GetOrdinal("correo")),
            Sexo = reader.IsDBNull(reader.GetOrdinal("sexo")) ? null : reader.GetString(reader.GetOrdinal("sexo")),
            EstadoCivil = reader.IsDBNull(reader.GetOrdinal("estado_civil")) ? null : reader.GetString(reader.GetOrdinal("estado_civil")),
            Direccion = reader.IsDBNull(reader.GetOrdinal("direccion")) ? null : reader.GetString(reader.GetOrdinal("direccion")),
            IdBanco = reader.IsDBNull(reader.GetOrdinal("id_banco")) ? null : reader.GetInt64(reader.GetOrdinal("id_banco")),
            NombreBanco = reader.IsDBNull(reader.GetOrdinal("nombre_banco")) ? null : reader.GetString(reader.GetOrdinal("nombre_banco")),
            NumeroCuentaBancaria = reader.IsDBNull(reader.GetOrdinal("numero_cuenta_bancaria"))
                ? null
                : reader.GetString(reader.GetOrdinal("numero_cuenta_bancaria")),
            Inss = reader.IsDBNull(reader.GetOrdinal("inss")) ? null : reader.GetString(reader.GetOrdinal("inss")),
            Activo = reader.GetBoolean(reader.GetOrdinal("activo")),
            FechaBaja = reader.IsDBNull(reader.GetOrdinal("fecha_baja"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("fecha_baja")).ToString("yyyy-MM-dd"),
            MotivoBaja = reader.IsDBNull(reader.GetOrdinal("motivo_baja")) ? null : reader.GetString(reader.GetOrdinal("motivo_baja")),
            FechaRegistro = reader.GetDateTime(reader.GetOrdinal("fecha_registro")).ToString("yyyy-MM-dd HH:mm:ss"),
            FechaActualizacion = reader.IsDBNull(reader.GetOrdinal("fecha_actualizacion"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("fecha_actualizacion")).ToString("yyyy-MM-dd HH:mm:ss"),
        };
    }

    private static void AsignarParametrosEmpleado(SqlCommand comando, EmpleadoGuardarModel model)
    {
        comando.Parameters.Add("@codigo_empleado", SqlDbType.NVarChar, 30).Value =
            model.CodigoEmpleado.Trim().ToUpperInvariant();
        comando.Parameters.Add("@id_departamento", SqlDbType.BigInt).Value = model.IdDepartamento;
        comando.Parameters.Add("@id_cargo", SqlDbType.BigInt).Value = model.IdCargo;
        comando.Parameters.Add("@cedula", SqlDbType.NVarChar, 50).Value =
            model.Cedula.Trim().ToUpperInvariant();
        comando.Parameters.Add("@inss", SqlDbType.NVarChar, 50).Value = ToDbValue(model.Inss);
        comando.Parameters.Add("@nombres", SqlDbType.NVarChar, 150).Value = model.Nombres.Trim();
        comando.Parameters.Add("@apellidos", SqlDbType.NVarChar, 150).Value = model.Apellidos.Trim();
        comando.Parameters.Add("@fecha_nacimiento", SqlDbType.Date).Value = ToDateDbValue(model.FechaNacimiento);
        comando.Parameters.Add("@sexo", SqlDbType.NVarChar, 20).Value =
            ToDbValue(model.Sexo?.Trim().ToUpperInvariant());
        comando.Parameters.Add("@estado_civil", SqlDbType.NVarChar, 30).Value =
            ToDbValue(model.EstadoCivil?.Trim().ToUpperInvariant());
        comando.Parameters.Add("@telefono", SqlDbType.NVarChar, 50).Value = ToDbValue(model.Telefono);
        comando.Parameters.Add("@correo", SqlDbType.NVarChar, 150).Value =
            ToDbValue(model.Correo?.Trim().ToLowerInvariant());
        comando.Parameters.Add("@direccion", SqlDbType.NVarChar, 300).Value = ToDbValue(model.Direccion);
        comando.Parameters.Add("@fecha_ingreso", SqlDbType.Date).Value = DateTime.Parse(model.FechaIngreso);
        comando.Parameters.Add("@id_banco", SqlDbType.BigInt).Value =
            model.IdBanco.HasValue ? model.IdBanco.Value : DBNull.Value;
        comando.Parameters.Add("@numero_cuenta_bancaria", SqlDbType.NVarChar, 100).Value =
            ToDbValue(model.NumeroCuentaBancaria);
    }

    private static object ToDbValue(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
    }

    private static object ToDateDbValue(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? DBNull.Value : DateTime.Parse(valor);
    }

    private static string TraducirErrorSql(string message)
    {
        var texto = message.ToLowerInvariant();

        if (texto.Contains("codigo_empleado"))
        {
            return "El codigo de empleado ya existe.";
        }

        if (texto.Contains("cedula"))
        {
            return "La cedula ya existe.";
        }

        if (texto.Contains("out-of-range") || texto.Contains("fuera de intervalo") || texto.Contains("datetime"))
        {
            return "Hay una fecha fuera del rango permitido. Usa una fecha igual o mayor a 01/01/1753.";
        }

        return "La base de datos rechazo la operacion.";
    }

    private static string SiguienteCodigo(string? ultimoCodigo)
    {
        var match = Regex.Match(ultimoCodigo ?? "EMP0000", "(\\d+)$");
        if (!match.Success)
        {
            return "EMP0001";
        }

        var siguiente = int.Parse(match.Groups[1].Value) + 1;
        return $"EMP{siguiente:0000}";
    }

    private static string NormalizarEstado(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return "TODOS";
        }

        var valor = estado.Trim().ToUpperInvariant();
        return valor switch
        {
            "ACTIVO" => "ACTIVO",
            "SUSPENDIDO" => "SUSPENDIDO",
            "RETIRADO" => "RETIRADO",
            "VACACIONES" => "VACACIONES",
            _ => "TODOS",
        };
    }

    public sealed class EmpleadoGuardarModel
    {
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string? UsuarioSistema { get; set; }
        public long? IdSupervisorEmpleado { get; set; }
        public long IdDepartamento { get; set; }
        public long IdCargo { get; set; }
        public string Cedula { get; set; } = string.Empty;
        public string? Inss { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string? FechaNacimiento { get; set; }
        public string? Sexo { get; set; }
        public string? EstadoCivil { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public string FechaIngreso { get; set; } = string.Empty;
        public long? IdBanco { get; set; }
        public string? NumeroCuentaBancaria { get; set; }
    }

    public sealed class EmpleadoEliminarModel
    {
        public string AdminUsuario { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    public sealed class EmpleadoDto
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string? FotoPerfilUrl { get; set; }
        public string? UsuarioSistema { get; set; }
        public long? IdSupervisorEmpleado { get; set; }
        public string? CodigoSupervisorEmpleado { get; set; }
        public string? NombreSupervisorEmpleado { get; set; }
        public string? UsuarioSupervisor { get; set; }
        public string Cedula { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public long IdDepartamento { get; set; }
        public string NombreDepartamento { get; set; } = string.Empty;
        public long IdCargo { get; set; }
        public string NombreCargo { get; set; } = string.Empty;
        public long IdEstadoEmpleado { get; set; }
        public string CodigoEstadoEmpleado { get; set; } = string.Empty;
        public string NombreEstadoEmpleado { get; set; } = string.Empty;
        public string FechaIngreso { get; set; } = string.Empty;
        public string? FechaNacimiento { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Sexo { get; set; }
        public string? EstadoCivil { get; set; }
        public string? Direccion { get; set; }
        public long? IdBanco { get; set; }
        public string? NombreBanco { get; set; }
        public string? NumeroCuentaBancaria { get; set; }
        public string? Inss { get; set; }
        public bool Activo { get; set; }
        public string? FechaBaja { get; set; }
        public string? MotivoBaja { get; set; }
        public string FechaRegistro { get; set; } = string.Empty;
        public string? FechaActualizacion { get; set; }
        public EmpleadoResumenDto? ResumenLaboral { get; set; }
    }

    public sealed class EmpleadoResumenDto
    {
        public int TotalContratos { get; set; }
        public int TotalAcciones { get; set; }
        public int TotalExpedientes { get; set; }
        public int TotalSubordinados { get; set; }
        public int ExpedientesVencidos { get; set; }
        public int ExpedientesPorVencer { get; set; }
        public int PermisosPendientes { get; set; }
        public int VacacionesPendientes { get; set; }
        public int HorasExtraPendientes { get; set; }
        public int MarcacionesHoy { get; set; }
        public bool TieneContratoVigente { get; set; }
        public string? ContratoVigenteNumero { get; set; }
        public string? ContratoVigenteTipo { get; set; }
        public string? ContratoVigenteInicio { get; set; }
        public string? ContratoVigenteHasta { get; set; }
        public decimal? SalarioBaseMensual { get; set; }
        public string? MonedaContrato { get; set; }
        public string? UltimaMarcacionFechaHora { get; set; }
        public string? UltimaMarcacionTipo { get; set; }
        public decimal DiasVacacionesAcumulados { get; set; }
        public decimal DiasVacacionesTomados { get; set; }
        public decimal DiasPermisosDescontados { get; set; }
        public decimal DiasVacacionesDisponibles { get; set; }
        public decimal DiasVacacionesPendientesDias { get; set; }
        public decimal DiasPermisosPendientesDias { get; set; }
    }

    public sealed class ReferenciaRelacionadaDto
    {
        public string Table { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    private sealed class SecurityUserLink
    {
        public long IdUsuario { get; set; }
        public string Usuario { get; set; } = string.Empty;
    }

    private sealed class RoleMatch
    {
        public long IdRol { get; set; }
        public string CodigoRol { get; set; } = string.Empty;
        public string NombreRol { get; set; } = string.Empty;
    }
}
