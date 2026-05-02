using System.Data;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Sifnic.Api.Creditos;
using Sifnic.Api.Parameters;
using Sifnic.Api.Security;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class SeguridadController : Controller
{
    private static readonly string[] RolesAdministrativos = ["ADMINISTRADOR", "ADMINISTRACION"];
    private readonly IWebHostEnvironment _environment;

    public SeguridadController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost]
    public IActionResult Login([FromBody] LoginRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa tu usuario y tu contraseña.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var usuario = ObtenerUsuarioPorLogin(conexion, model.Username);
            if (usuario is null)
            {
                RegistrarBitacoraAcceso(
                    conexion,
                    null,
                    model.Username.Trim(),
                    "DENEGADO",
                    "El usuario no existe o no está activo.");

                return Unauthorized(new
                {
                    ok = false,
                    message = "Usuario o contraseña incorrectos.",
                });
            }

            if (usuario.Bloqueado)
            {
                RegistrarBitacoraAcceso(
                    conexion,
                    usuario.IdUsuario,
                    usuario.Usuario,
                    "BLOQUEADO",
                    "La cuenta está bloqueada.");

                return StatusCode(423, new
                {
                    ok = false,
                    message = "Tu usuario está bloqueado. Solicita un restablecimiento de contraseña.",
                });
            }

            if (!SecuritySupport.VerifyPassword(model.Password, usuario.HashClave))
            {
                var intentosMaximos = ObtenerParametroEntero(conexion, "INTENTOS_MAXIMOS", 6);
                var intentosActualizados = usuario.IntentosFallidos + 1;
                var bloquear = intentosActualizados >= intentosMaximos;

                using (var comando = new SqlCommand(
                    """
                    UPDATE seguridad.usuario
                    SET
                        intentos_fallidos = @intentos_fallidos,
                        bloqueado = @bloqueado,
                        fecha_actualizacion = SYSDATETIME()
                    WHERE id_usuario = @id_usuario;
                    """,
                    conexion))
                {
                    comando.Parameters.Add("@intentos_fallidos", SqlDbType.Int).Value = intentosActualizados;
                    comando.Parameters.Add("@bloqueado", SqlDbType.Bit).Value = bloquear;
                    comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = usuario.IdUsuario;
                    comando.ExecuteNonQuery();
                }

                RegistrarBitacoraAcceso(
                    conexion,
                    usuario.IdUsuario,
                    usuario.Usuario,
                    bloquear ? "BLOQUEADO" : "DENEGADO",
                    bloquear
                        ? "Se bloqueó la cuenta por intentos fallidos."
                        : "La contraseña ingresada es incorrecta.");

                return Unauthorized(new
                {
                    ok = false,
                    message = bloquear
                        ? "Tu usuario quedó bloqueado por varios intentos fallidos. Solicita un restablecimiento."
                        : "Usuario o contraseña incorrectos.",
                });
            }

            ReiniciarIntentos(conexion, usuario.IdUsuario);

            if (usuario.CambiarClaveEnProximoInicio)
            {
                RegistrarBitacoraAcceso(
                    conexion,
                    usuario.IdUsuario,
                    usuario.Usuario,
                    "CAMBIO_CLAVE_REQUERIDO",
                    "El usuario debe cambiar la contraseña antes de continuar.");

                return Json(new
                {
                    ok = true,
                    data = new
                    {
                        requirePasswordChange = true,
                        username = usuario.Usuario,
                        user = usuario.NombreCompleto,
                        displayName = usuario.NombreCompleto,
                        roles = usuario.Roles,
                        rolesLabel = string.Join(", ", usuario.Roles),
                    },
                });
            }

            var sesion = CrearSesion(conexion, usuario);

            RegistrarBitacoraAcceso(
                conexion,
                usuario.IdUsuario,
                usuario.Usuario,
                "AUTORIZADO",
                "Inicio de sesión correcto.");

            return Json(new
            {
                ok = true,
                data = ConstruirSesionRespuesta(usuario, sesion),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo iniciar sesión.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult CambiarClave([FromBody] ChangePasswordRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Username) ||
            string.IsNullOrWhiteSpace(model.CurrentPassword) ||
            string.IsNullOrWhiteSpace(model.NewPassword))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Completa los datos para cambiar la contraseña.",
            });
        }

        if (model.NewPassword.Trim().Length < 6)
        {
            return BadRequest(new
            {
                ok = false,
                message = "La nueva contraseña debe tener al menos 6 caracteres.",
            });
        }

        if (string.Equals(model.CurrentPassword, model.NewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                ok = false,
                message = "La nueva contraseña debe ser diferente a la temporal.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var usuario = ObtenerUsuarioPorLogin(conexion, model.Username);
            if (usuario is null || !SecuritySupport.VerifyPassword(model.CurrentPassword, usuario.HashClave))
            {
                return Unauthorized(new
                {
                    ok = false,
                    message = "No se pudo validar la contraseña actual.",
                });
            }

            using (var comando = new SqlCommand(
                """
                UPDATE seguridad.usuario
                SET
                    hash_clave = @hash_clave,
                    cambiar_clave_en_proximo_inicio = 0,
                    intentos_fallidos = 0,
                    bloqueado = 0,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_usuario = @id_usuario;
                """,
                conexion))
            {
                comando.Parameters.Add("@hash_clave", SqlDbType.NVarChar, 1000).Value =
                    SecuritySupport.HashPassword(model.NewPassword.Trim());
                comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = usuario.IdUsuario;
                comando.ExecuteNonQuery();
            }

            usuario = ObtenerUsuarioPorId(conexion, usuario.IdUsuario)!;
            var sesion = CrearSesion(conexion, usuario);

            RegistrarBitacoraAcceso(
                conexion,
                usuario.IdUsuario,
                usuario.Usuario,
                "AUTORIZADO",
                "Acceso autorizado después de cambio de contraseña.");

            RegistrarBitacoraOperativa(
                conexion,
                "SEGURIDAD",
                "USUARIOS",
                "CAMBIO_CLAVE",
                usuario.IdUsuario,
                usuario.Usuario,
                $"El usuario {usuario.Usuario} actualizó su contraseña.",
                usuario.Usuario);

            return Json(new
            {
                ok = true,
                message = "Contraseña actualizada correctamente.",
                data = ConstruirSesionRespuesta(usuario, sesion),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cambiar la contraseña.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Logout()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion, touchSession: false);
            if (contexto is not null)
            {
                CerrarSesion(conexion, contexto.TokenSesion, "LOGOUT");
                RegistrarBitacoraOperativa(
                    conexion,
                    "SEGURIDAD",
                    "SESION",
                    "CIERRE",
                    contexto.IdUsuario,
                    contexto.Username,
                    $"El usuario {contexto.Username} cerró sesión.",
                    contexto.Username);
            }

            return Json(new
            {
                ok = true,
                message = "Sesión cerrada.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cerrar la sesión.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Usuarios()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            ModuleAccessSupport.EnsureUserModuleAccessSchema(conexion);

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesión no es válida o ya expiró.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar usuarios.",
                });
            }

            const string sql = """
                SELECT
                    u.id_usuario,
                    u.usuario,
                    u.nombres,
                    u.apellidos,
                    u.correo,
                    u.telefono,
                    u.cambiar_clave_en_proximo_inicio,
                    u.bloqueado,
                    u.activo,
                    u.fecha_ultimo_acceso,
                    roles.roles,
                    empleado.id_empleado,
                    empleado.nombre_completo,
                    empleado.nombre_cargo,
                    modulos.tiene_configuracion_modulos
                FROM seguridad.usuario u
                OUTER APPLY
                (
                    SELECT STRING_AGG(r.codigo_rol, ', ') WITHIN GROUP (ORDER BY r.codigo_rol) AS roles
                    FROM seguridad.usuario_rol ur
                    INNER JOIN seguridad.rol r
                        ON r.id_rol = ur.id_rol
                       AND r.activo = 1
                    WHERE ur.id_usuario = u.id_usuario
                      AND ur.activo = 1
                ) roles
                OUTER APPLY
                (
                    SELECT TOP (1)
                        e.id_empleado,
                        COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_completo,
                        c.nombre_cargo
                    FROM rrhh.empleado e
                    LEFT JOIN rrhh.cargo c
                        ON c.id_cargo = e.id_cargo
                    WHERE
                        (u.correo IS NOT NULL AND u.correo <> N'' AND e.correo = u.correo)
                        OR (e.nombres = u.nombres AND e.apellidos = u.apellidos)
                    ORDER BY
                        CASE
                            WHEN u.correo IS NOT NULL AND u.correo <> N'' AND e.correo = u.correo THEN 0
                            ELSE 1
                        END,
                        e.id_empleado DESC
                ) empleado
                OUTER APPLY
                (
                    SELECT
                        CASE
                            WHEN EXISTS (
                                SELECT 1
                                FROM seguridad.usuario_modulo um
                                WHERE um.id_usuario = u.id_usuario
                                  AND um.activo = 1
                            )
                            THEN CAST(1 AS bit)
                            ELSE CAST(0 AS bit)
                        END AS tiene_configuracion_modulos
                ) modulos
                ORDER BY u.usuario;
                """;

            using var comando = new SqlCommand(sql, conexion);
            using var reader = comando.ExecuteReader();

            var items = new List<object>();
            while (reader.Read())
            {
                var nombreUsuario = SecuritySupport.BuildDisplayName(
                    reader.GetString(reader.GetOrdinal("nombres")),
                    reader.GetString(reader.GetOrdinal("apellidos")));

                items.Add(new
                {
                    idUsuario = reader.GetInt64(reader.GetOrdinal("id_usuario")),
                    usuario = reader.GetString(reader.GetOrdinal("usuario")),
                    nombreCompleto = reader.IsDBNull(reader.GetOrdinal("nombre_completo"))
                        ? nombreUsuario
                        : reader.GetString(reader.GetOrdinal("nombre_completo")),
                    cargo = reader.IsDBNull(reader.GetOrdinal("nombre_cargo"))
                        ? "Sin cargo"
                        : reader.GetString(reader.GetOrdinal("nombre_cargo")),
                    roles = reader.IsDBNull(reader.GetOrdinal("roles"))
                        ? "Sin rol"
                        : reader.GetString(reader.GetOrdinal("roles")),
                    correo = reader.IsDBNull(reader.GetOrdinal("correo"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("correo")),
                    telefono = reader.IsDBNull(reader.GetOrdinal("telefono"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("telefono")),
                    requiereCambioClave = reader.GetBoolean(reader.GetOrdinal("cambiar_clave_en_proximo_inicio")),
                    bloqueado = reader.GetBoolean(reader.GetOrdinal("bloqueado")),
                    activo = reader.GetBoolean(reader.GetOrdinal("activo")),
                    fechaUltimoAcceso = reader.IsDBNull(reader.GetOrdinal("fecha_ultimo_acceso"))
                        ? null
                        : reader.GetDateTime(reader.GetOrdinal("fecha_ultimo_acceso")).ToString("yyyy-MM-ddTHH:mm:ss"),
                    tieneConfiguracionModulos = reader.GetBoolean(reader.GetOrdinal("tiene_configuracion_modulos")),
                });
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
                message = "No se pudo cargar el listado de usuarios.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{idUsuario:long}")]
    public IActionResult ModulosUsuario(long idUsuario)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            ModuleAccessSupport.EnsureUserModuleAccessSchema(conexion);

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar modulos de usuario.",
                });
            }

            var usuario = ObtenerUsuarioPorId(conexion, idUsuario);
            if (usuario is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Usuario no encontrado.",
                });
            }

            var modules = ModuleAccessSupport.BuildUserModuleAccess(
                conexion,
                usuario.IdUsuario,
                usuario.Roles,
                usuario.Usuario);

            return Json(new
            {
                ok = true,
                data = new
                {
                    idUsuario = usuario.IdUsuario,
                    usuario = usuario.Usuario,
                    nombreCompleto = usuario.NombreCompleto,
                    roles = usuario.Roles,
                    hasCustomConfiguration = ModuleAccessSupport.HasCustomConfiguration(conexion, usuario.IdUsuario),
                    modules,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la configuracion de modulos del usuario.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{idUsuario:long}")]
    public IActionResult GuardarModulosUsuario(long idUsuario, [FromBody] SaveUserModulesRequest model)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            ModuleAccessSupport.EnsureUserModuleAccessSchema(conexion);

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar modulos de usuario.",
                });
            }

            var usuario = ObtenerUsuarioPorId(conexion, idUsuario);
            if (usuario is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Usuario no encontrado.",
                });
            }

            var requestedKeys = (model.ModuleKeys ?? [])
                .Select(ModuleAccessSupport.NormalizeModuleKey)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (model.UseAutomatic)
            {
                ModuleAccessSupport.ClearUserModuleConfiguration(conexion, usuario.IdUsuario);
            }
            else
            {
                ModuleAccessSupport.SaveUserModuleConfiguration(
                    conexion,
                    usuario.IdUsuario,
                    requestedKeys,
                    contexto.Username);
            }

            RegistrarBitacoraOperativa(
                conexion,
                "SEGURIDAD",
                "USUARIOS",
                "MODULOS_USUARIO",
                usuario.IdUsuario,
                usuario.Usuario,
                $"Se actualizo la configuracion de modulos del usuario {usuario.Usuario}.",
                contexto.Username);

            var modules = ModuleAccessSupport.BuildUserModuleAccess(
                conexion,
                usuario.IdUsuario,
                usuario.Roles,
                usuario.Usuario);

            return Json(new
            {
                ok = true,
                message = model.UseAutomatic
                    ? "Se restauro la configuracion automatica del usuario."
                    : "Permisos por modulo actualizados correctamente.",
                data = new
                {
                    idUsuario = usuario.IdUsuario,
                    usuario = usuario.Usuario,
                    hasCustomConfiguration = ModuleAccessSupport.HasCustomConfiguration(conexion, usuario.IdUsuario),
                    modules,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron guardar los modulos del usuario.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult MisModulosDashboard()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            ModuleAccessSupport.EnsureUserModuleAccessSchema(conexion);

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            var keys = ModuleAccessSupport.GetEffectiveModuleKeys(
                conexion,
                contexto.IdUsuario,
                contexto.Roles,
                contexto.Username);

            return Json(new
            {
                ok = true,
                data = new
                {
                    modules = keys.OrderBy(value => value).ToArray(),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los modulos del dashboard.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult ConfiguracionGeneral()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar la configuracion general.",
                });
            }

            var configuracion = CargarConfiguracionGeneral(conexion);

            return Json(new
            {
                ok = true,
                data = configuracion,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la configuracion general.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut]
    public IActionResult GuardarConfiguracionGeneral([FromBody] SaveSystemConfigurationRequest model)
    {
        if (model is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "No se recibio la configuracion a guardar.",
            });
        }

        if (string.IsNullOrWhiteSpace(model.NombreSistema) ||
            string.IsNullOrWhiteSpace(model.RazonSocial) ||
            string.IsNullOrWhiteSpace(model.NombreComercial))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Completa el nombre del sistema, la razon social y el nombre comercial.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para guardar la configuracion general.",
                });
            }

            var configuracionActual = CargarConfiguracionGeneral(conexion);
            var idEmpresa = model.IdEmpresa > 0 ? model.IdEmpresa : configuracionActual.IdEmpresa;
            var idConfiguracionGeneral = model.IdConfiguracionGeneral > 0
                ? model.IdConfiguracionGeneral
                : configuracionActual.IdConfiguracionGeneral;

            using var transaccion = conexion.BeginTransaction();

            using (var companyCommand = new SqlCommand(
                """
                UPDATE empresa.empresa
                SET
                    razon_social = @razon_social,
                    nombre_comercial = @nombre_comercial,
                    ruc = @ruc,
                    telefono = @telefono,
                    correo = @correo,
                    direccion = @direccion,
                    logo_url = @logo_url,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_empresa = @id_empresa;
                """,
                conexion,
                transaccion))
            {
                companyCommand.Parameters.Add("@id_empresa", SqlDbType.BigInt).Value = idEmpresa;
                companyCommand.Parameters.Add("@razon_social", SqlDbType.NVarChar, 300).Value = model.RazonSocial.Trim();
                companyCommand.Parameters.Add("@nombre_comercial", SqlDbType.NVarChar, 300).Value = model.NombreComercial.Trim();
                companyCommand.Parameters.Add("@ruc", SqlDbType.NVarChar, 100).Value = ToDbValue(model.Ruc);
                companyCommand.Parameters.Add("@telefono", SqlDbType.NVarChar, 100).Value = ToDbValue(model.TelefonoEmpresa);
                companyCommand.Parameters.Add("@correo", SqlDbType.NVarChar, 300).Value = ToDbValue(model.CorreoEmpresa);
                companyCommand.Parameters.Add("@direccion", SqlDbType.NVarChar, 1000).Value = ToDbValue(model.DireccionEmpresa);
                companyCommand.Parameters.Add("@logo_url", SqlDbType.NVarChar, 1000).Value = ToDbValue(model.LogoEmpresaUrl);
                companyCommand.ExecuteNonQuery();
            }

            if (idConfiguracionGeneral > 0)
            {
                using var configUpdate = new SqlCommand(
                    """
                    UPDATE empresa.configuracion_general
                    SET
                        nombre_sistema = @nombre_sistema,
                        tema_color = @tema_color,
                        logo_login_url = @logo_login_url,
                        logo_sidebar_url = @logo_sidebar_url,
                        nombre_gerente_rrhh = @nombre_gerente_rrhh,
                        texto_footer = @texto_footer,
                        correo_soporte = @correo_soporte,
                        telefono_soporte = @telefono_soporte,
                        mostrar_logo_login = @mostrar_logo_login,
                        activo = 1,
                        fecha_actualizacion = SYSDATETIME()
                    WHERE id_configuracion_general = @id_configuracion_general;
                    """,
                    conexion,
                    transaccion);
                configUpdate.Parameters.Add("@id_configuracion_general", SqlDbType.BigInt).Value = idConfiguracionGeneral;
                configUpdate.Parameters.Add("@nombre_sistema", SqlDbType.NVarChar, 300).Value = model.NombreSistema.Trim();
                configUpdate.Parameters.Add("@tema_color", SqlDbType.NVarChar, 120).Value = ToDbValue(model.TemaColor);
                configUpdate.Parameters.Add("@logo_login_url", SqlDbType.NVarChar, 1000).Value = ToDbValue(
                    string.IsNullOrWhiteSpace(model.LogoLoginUrl) ? model.LogoEmpresaUrl : model.LogoLoginUrl);
                configUpdate.Parameters.Add("@logo_sidebar_url", SqlDbType.NVarChar, 1000).Value = ToDbValue(
                    string.IsNullOrWhiteSpace(model.LogoSidebarUrl) ? model.LogoEmpresaUrl : model.LogoSidebarUrl);
                configUpdate.Parameters.Add("@nombre_gerente_rrhh", SqlDbType.NVarChar, 300).Value = ToDbValue(model.NombreGerenteRrhh);
                configUpdate.Parameters.Add("@texto_footer", SqlDbType.NVarChar, 1000).Value = ToDbValue(model.TextoFooter);
                configUpdate.Parameters.Add("@correo_soporte", SqlDbType.NVarChar, 300).Value = ToDbValue(model.CorreoSoporte);
                configUpdate.Parameters.Add("@telefono_soporte", SqlDbType.NVarChar, 100).Value = ToDbValue(model.TelefonoSoporte);
                configUpdate.Parameters.Add("@mostrar_logo_login", SqlDbType.Bit).Value = model.MostrarLogoLogin;
                configUpdate.ExecuteNonQuery();
            }
            else
            {
                using var configInsert = new SqlCommand(
                    """
                    INSERT INTO empresa.configuracion_general
                    (
                        id_empresa,
                        nombre_sistema,
                        tema_color,
                        logo_login_url,
                        logo_sidebar_url,
                        nombre_gerente_rrhh,
                        texto_footer,
                        correo_soporte,
                        telefono_soporte,
                        mostrar_logo_login,
                        activo,
                        fecha_registro,
                        fecha_actualizacion
                    )
                    VALUES
                    (
                        @id_empresa,
                        @nombre_sistema,
                        @tema_color,
                        @logo_login_url,
                        @logo_sidebar_url,
                        @nombre_gerente_rrhh,
                        @texto_footer,
                        @correo_soporte,
                        @telefono_soporte,
                        @mostrar_logo_login,
                        1,
                        SYSDATETIME(),
                        SYSDATETIME()
                    );
                    """,
                    conexion,
                    transaccion);
                configInsert.Parameters.Add("@id_empresa", SqlDbType.BigInt).Value = idEmpresa;
                configInsert.Parameters.Add("@nombre_sistema", SqlDbType.NVarChar, 300).Value = model.NombreSistema.Trim();
                configInsert.Parameters.Add("@tema_color", SqlDbType.NVarChar, 120).Value = ToDbValue(model.TemaColor);
                configInsert.Parameters.Add("@logo_login_url", SqlDbType.NVarChar, 1000).Value = ToDbValue(
                    string.IsNullOrWhiteSpace(model.LogoLoginUrl) ? model.LogoEmpresaUrl : model.LogoLoginUrl);
                configInsert.Parameters.Add("@logo_sidebar_url", SqlDbType.NVarChar, 1000).Value = ToDbValue(
                    string.IsNullOrWhiteSpace(model.LogoSidebarUrl) ? model.LogoEmpresaUrl : model.LogoSidebarUrl);
                configInsert.Parameters.Add("@nombre_gerente_rrhh", SqlDbType.NVarChar, 300).Value = ToDbValue(model.NombreGerenteRrhh);
                configInsert.Parameters.Add("@texto_footer", SqlDbType.NVarChar, 1000).Value = ToDbValue(model.TextoFooter);
                configInsert.Parameters.Add("@correo_soporte", SqlDbType.NVarChar, 300).Value = ToDbValue(model.CorreoSoporte);
                configInsert.Parameters.Add("@telefono_soporte", SqlDbType.NVarChar, 100).Value = ToDbValue(model.TelefonoSoporte);
                configInsert.Parameters.Add("@mostrar_logo_login", SqlDbType.Bit).Value = model.MostrarLogoLogin;
                configInsert.ExecuteNonQuery();
            }

            transaccion.Commit();

            RegistrarBitacoraOperativa(
                conexion,
                "SEGURIDAD",
                "CONFIGURACION",
                "ACTUALIZACION_GENERAL",
                idEmpresa,
                model.NombreComercial.Trim(),
                $"Se actualizo la configuracion general del sistema {model.NombreSistema.Trim()}.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = "Configuracion general actualizada correctamente.",
                data = CargarConfiguracionGeneral(conexion),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo guardar la configuracion general.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult ReglasConami()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar reglas CONAMI.",
                });
            }

            var configuracion = ConamiRulesSupport.LoadConfiguration(conexion);

            return Json(new
            {
                ok = true,
                data = configuracion,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar las reglas CONAMI.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult CoreMicrofinanciero()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar el core microfinanciero.",
                });
            }

            MicrofinanceCoreSupport.EnsureSchema(conexion);
            return Json(new
            {
                ok = true,
                data = new
                {
                    products = MicrofinanceCoreSupport.LoadProducts(conexion),
                    catalogs = new
                    {
                        activities = MicrofinanceCoreSupport.LoadCatalog(conexion, "ACTIVIDAD_ECONOMICA"),
                        departments = MicrofinanceCoreSupport.LoadCatalog(conexion, "DEPARTAMENTO"),
                        municipalities = MicrofinanceCoreSupport.LoadCatalog(conexion, "MUNICIPIO"),
                        guaranteeTypes = MicrofinanceCoreSupport.LoadCatalog(conexion, "TIPO_GARANTIA"),
                        administrativeStatuses = MicrofinanceCoreSupport.LoadCatalog(conexion, "ESTADO_ADMINISTRATIVO"),
                        primDictionary = MicrofinanceCoreSupport.LoadCatalog(conexion, "ICC_PRIM"),
                        cashOperations = MicrofinanceCoreSupport.LoadCatalog(conexion, "OPERACION_CAJA"),
                    },
                    uafAlerts = MicrofinanceCoreSupport.LoadUafAlerts(conexion),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la parametrizacion microfinanciera.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut]
    public IActionResult GuardarReglasConami([FromBody] SaveConamiRulesRequest model)
    {
        if (model is null || model.Rules.Count == 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "No se recibieron reglas CONAMI para guardar.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para guardar reglas CONAMI.",
                });
            }

            ConamiRulesSupport.SaveRules(conexion, model.Rules, contexto.Username);
            RegistrarBitacoraOperativa(
                conexion,
                "CONFIGURACION",
                "CONAMI",
                "ACTUALIZACION_REGLAS",
                null,
                "Reglas CONAMI",
                $"Se actualizaron {model.Rules.Count} reglas parametrizables CONAMI.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = "Reglas CONAMI actualizadas correctamente.",
                data = ConamiRulesSupport.LoadConfiguration(conexion),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron guardar las reglas CONAMI.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult ParametrosSeguridad()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar parametros de seguridad.",
                });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    intentosMaximos = ObtenerParametroEntero(conexion, "INTENTOS_MAXIMOS", 6),
                    minutosExpiracionSesion = ObtenerParametroEntero(conexion, "MINUTOS_EXPIRACION_SESION", 30),
                    horasExpiracionRecuperacion = ObtenerParametroEntero(conexion, "HORAS_EXPIRACION_RECUPERACION", 24),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los parametros de seguridad.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut]
    public IActionResult GuardarParametrosSeguridad([FromBody] SaveSecurityParametersRequest model)
    {
        if (model is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "No se recibieron parametros de seguridad.",
            });
        }

        if (model.IntentosMaximos < 1 || model.MinutosExpiracionSesion < 1 || model.HorasExpiracionRecuperacion < 1)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Todos los parametros de seguridad deben ser mayores a cero.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para guardar parametros de seguridad.",
                });
            }

            using var transaccion = conexion.BeginTransaction();
            UpsertParametroSeguridad(
                conexion,
                transaccion,
                "INTENTOS_MAXIMOS",
                model.IntentosMaximos.ToString(),
                "Cantidad maxima de intentos fallidos");
            UpsertParametroSeguridad(
                conexion,
                transaccion,
                "MINUTOS_EXPIRACION_SESION",
                model.MinutosExpiracionSesion.ToString(),
                "Minutos de expiracion de sesion");
            UpsertParametroSeguridad(
                conexion,
                transaccion,
                "HORAS_EXPIRACION_RECUPERACION",
                model.HorasExpiracionRecuperacion.ToString(),
                "Horas de expiracion de recuperacion de clave");

            transaccion.Commit();

            RegistrarBitacoraOperativa(
                conexion,
                "SEGURIDAD",
                "PARAMETROS",
                "ACTUALIZACION",
                null,
                "PARAMETROS_SEGURIDAD",
                $"Se actualizaron intentos maximos={model.IntentosMaximos}, minutos de sesion={model.MinutosExpiracionSesion} y horas de recuperacion={model.HorasExpiracionRecuperacion}.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = "Parametros de seguridad actualizados correctamente.",
                data = new
                {
                    intentosMaximos = model.IntentosMaximos,
                    minutosExpiracionSesion = model.MinutosExpiracionSesion,
                    horasExpiracionRecuperacion = model.HorasExpiracionRecuperacion,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron guardar los parametros de seguridad.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult TiposCambioConfiguracion()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para administrar tipos de cambio.",
                });
            }

            return Json(new
            {
                ok = true,
                data = ExchangeRateSupport.LoadConfiguration(conexion),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la configuracion de tipos de cambio.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult ImportarTipoCambioOficial([FromForm] List<IFormFile>? archivos)
    {
        if (archivos is null || archivos.Count == 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona al menos un archivo del BCN para importar.",
            });
        }

        var validFiles = archivos
            .Where(file => file is not null && file.Length > 0)
            .ToList();

        if (validFiles.Count == 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Los archivos seleccionados estan vacios.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para importar tipo de cambio oficial.",
                });
            }

            var summaries = new List<object>();
            var totalRows = 0;

            using var transaccion = conexion.BeginTransaction();

            foreach (var archivo in validFiles)
            {
                using var reader = new StreamReader(archivo.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
                var rawContent = reader.ReadToEnd();
                var rows = ExchangeRateSupport.ParseOfficialRateFile(rawContent);
                var loteId = ExchangeRateSupport.CreateImportBatch(
                    conexion,
                    transaccion,
                    Path.GetFileName(archivo.FileName),
                    contexto.Username);

                foreach (var row in rows)
                {
                    ExchangeRateSupport.RegisterOfficialRate(
                        conexion,
                        transaccion,
                        row.FechaTipoCambio,
                        row.ValorTipoCambio,
                        loteId);
                }

                var observation =
                    $"Archivo {Path.GetFileName(archivo.FileName)} importado con {rows.Count} fechas, desde {rows[0].FechaTipoCambio:yyyy-MM-dd} hasta {rows[^1].FechaTipoCambio:yyyy-MM-dd}.";

                ExchangeRateSupport.CompleteImportBatch(
                    conexion,
                    transaccion,
                    loteId,
                    "PROCESADO",
                    observation);

                summaries.Add(new
                {
                    idLoteImportacion = loteId,
                    archivo = Path.GetFileName(archivo.FileName),
                    registros = rows.Count,
                    fechaDesde = rows[0].FechaTipoCambio.ToString("yyyy-MM-dd"),
                    fechaHasta = rows[^1].FechaTipoCambio.ToString("yyyy-MM-dd"),
                });

                totalRows += rows.Count;
            }

            transaccion.Commit();

            RegistrarBitacoraOperativa(
                conexion,
                "CONFIGURACION",
                "TIPO_CAMBIO",
                "IMPORTACION_BCN",
                0,
                "BCN",
                $"Se importaron {totalRows} registros oficiales desde {validFiles.Count} archivo(s) BCN.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = validFiles.Count == 1
                    ? $"Tipo de cambio oficial importado correctamente con {totalRows} fechas."
                    : $"Tipos de cambio oficiales importados correctamente con {totalRows} fechas en total.",
                data = new
                {
                    totalArchivos = validFiles.Count,
                    totalRegistros = totalRows,
                    archivos = summaries,
                    contexto = ExchangeRateSupport.LoadConfiguration(conexion),
                },
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                ok = false,
                message = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo importar el tipo de cambio oficial del BCN.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult GuardarTipoCambioInstitucional([FromBody] SaveInstitutionalExchangeRateRequest model)
    {
        if (model is null || string.IsNullOrWhiteSpace(model.FechaTipoCambio))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa la fecha del tipo de cambio institucional.",
            });
        }

        if (!DateTime.TryParse(model.FechaTipoCambio, out var fechaTipoCambio))
        {
            return BadRequest(new
            {
                ok = false,
                message = "La fecha del tipo de cambio institucional no es valida.",
            });
        }

        if (!model.ValorCompra.HasValue && !model.ValorVenta.HasValue && !model.ValorReferencia.HasValue)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa al menos compra, venta o referencia para guardar el tipo de cambio institucional.",
            });
        }

        var valorCompra = model.ValorCompra.HasValue ? Math.Round(model.ValorCompra.Value, 6) : (decimal?)null;
        var valorVenta = model.ValorVenta.HasValue ? Math.Round(model.ValorVenta.Value, 6) : (decimal?)null;
        var valorReferencia = model.ValorReferencia.HasValue
            ? Math.Round(model.ValorReferencia.Value, 6)
            : (valorCompra.HasValue && valorVenta.HasValue
                ? Math.Round((valorCompra.Value + valorVenta.Value) / 2m, 6)
                : valorCompra ?? valorVenta);

        if ((valorCompra.HasValue && valorCompra.Value <= 0) ||
            (valorVenta.HasValue && valorVenta.Value <= 0) ||
            (valorReferencia.HasValue && valorReferencia.Value <= 0))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Los valores de tipo de cambio deben ser mayores a cero.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para guardar tipo de cambio institucional.",
                });
            }

            using var transaccion = conexion.BeginTransaction();

            ExchangeRateSupport.RegisterInstitutionalRate(
                conexion,
                transaccion,
                fechaTipoCambio.Date,
                valorCompra,
                valorVenta,
                valorReferencia,
                model.Observacion,
                contexto.Username);

            transaccion.Commit();

            RegistrarBitacoraOperativa(
                conexion,
                "CONFIGURACION",
                "TIPO_CAMBIO",
                "GUARDAR_INSTITUCIONAL",
                0,
                $"USD-NIO-{fechaTipoCambio:yyyyMMdd}",
                $"Se guardo el tipo de cambio institucional USD/NIO del {fechaTipoCambio:yyyy-MM-dd}.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = "Tipo de cambio institucional guardado correctamente.",
                data = ExchangeRateSupport.LoadConfiguration(conexion),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo guardar el tipo de cambio institucional.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult SubirLogoSistema([FromForm] IFormFile? archivo, [FromForm] string? destino)
    {
        if (archivo is null || archivo.Length <= 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona una imagen para subir.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesion no es valida o ya expiro.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para subir logos del sistema.",
                });
            }

            var extension = Path.GetExtension(archivo.FileName)?.Trim().ToLowerInvariant();
            var extensionesPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png",
                ".jpg",
                ".jpeg",
                ".webp",
                ".svg",
            };

            if (string.IsNullOrWhiteSpace(extension) || !extensionesPermitidas.Contains(extension))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Usa una imagen PNG, JPG, WEBP o SVG.",
                });
            }

            var nombreSeguro = Path.GetFileNameWithoutExtension(archivo.FileName);
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                nombreSeguro = nombreSeguro.Replace(invalidChar, '-');
            }

            nombreSeguro = string.IsNullOrWhiteSpace(nombreSeguro) ? "logo" : nombreSeguro.Trim();
            var sello = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var nombreArchivo = $"{nombreSeguro}-{sello}{extension}";
            var carpetaRelativa = Path.Combine("uploads", "branding");
            var carpetaFisica = Path.Combine(_environment.WebRootPath, carpetaRelativa);
            Directory.CreateDirectory(carpetaFisica);
            var rutaFisica = Path.Combine(carpetaFisica, nombreArchivo);

            using (var stream = System.IO.File.Create(rutaFisica))
            {
                archivo.CopyTo(stream);
            }

            var rutaUrl = "/" + Path.Combine(carpetaRelativa, nombreArchivo).Replace("\\", "/");

            RegistrarBitacoraOperativa(
                conexion,
                "SEGURIDAD",
                "CONFIGURACION",
                "CARGA_LOGO",
                null,
                nombreArchivo,
                $"Se cargo un logo del sistema para {destino ?? "general"}.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = "Logo cargado correctamente.",
                data = new
                {
                    url = rutaUrl,
                    fileName = nombreArchivo,
                    destino = string.IsNullOrWhiteSpace(destino) ? "general" : destino.Trim(),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la imagen del logo.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost("{idUsuario:long}")]
    public IActionResult DesbloquearUsuario(long idUsuario)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesiÃ³n no es vÃ¡lida o ya expirÃ³.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para desbloquear usuarios.",
                });
            }

            var usuario = ObtenerUsuarioPorId(conexion, idUsuario);
            if (usuario is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Usuario no encontrado.",
                });
            }

            using (var comando = new SqlCommand(
                """
                UPDATE seguridad.usuario
                SET
                    intentos_fallidos = 0,
                    bloqueado = 0,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_usuario = @id_usuario;
                """,
                conexion))
            {
                comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = usuario.IdUsuario;
                comando.ExecuteNonQuery();
            }

            RegistrarBitacoraOperativa(
                conexion,
                "SEGURIDAD",
                "USUARIOS",
                "DESBLOQUEO",
                usuario.IdUsuario,
                usuario.Usuario,
                $"Se desbloqueo el usuario {usuario.Usuario} y se reiniciaron sus intentos fallidos.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = $"Usuario {usuario.Usuario} desbloqueado correctamente.",
                data = new
                {
                    usuario = usuario.Usuario,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo desbloquear el usuario.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost("{idUsuario:long}")]
    public IActionResult RestablecerClaveTemporal(long idUsuario)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesión no es válida o ya expiró.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para restablecer contraseñas.",
                });
            }

            var usuario = ObtenerUsuarioPorId(conexion, idUsuario);
            if (usuario is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Usuario no encontrado.",
                });
            }

            using (var comando = new SqlCommand(
                """
                UPDATE seguridad.usuario
                SET
                    hash_clave = @hash_clave,
                    cambiar_clave_en_proximo_inicio = 1,
                    intentos_fallidos = 0,
                    bloqueado = 0,
                    fecha_actualizacion = SYSDATETIME()
                WHERE id_usuario = @id_usuario;
                """,
                conexion))
            {
                comando.Parameters.Add("@hash_clave", SqlDbType.NVarChar, 1000).Value =
                    SecuritySupport.HashPassword(usuario.Usuario);
                comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = usuario.IdUsuario;
                comando.ExecuteNonQuery();
            }

            RegistrarBitacoraOperativa(
                conexion,
                "SEGURIDAD",
                "USUARIOS",
                "RESET_CLAVE",
                usuario.IdUsuario,
                usuario.Usuario,
                $"Se restableció la contraseña temporal del usuario {usuario.Usuario}.",
                contexto.Username);

            return Json(new
            {
                ok = true,
                message = $"Contraseña temporal restablecida para {usuario.Usuario}. La clave temporal es su mismo usuario y deberá cambiarla al ingresar.",
                data = new
                {
                    usuario = usuario.Usuario,
                    claveTemporal = usuario.Usuario,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo restablecer la contraseña temporal.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult BitacoraAcceso(int take = 120)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesión no es válida o ya expiró.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para ver la bitácora de accesos.",
                });
            }

            using var comando = new SqlCommand(
                """
                SELECT TOP (@take)
                    b.id_bitacora_acceso,
                    b.usuario_digitado,
                    b.resultado_acceso,
                    b.detalle_resultado,
                    b.ip_origen,
                    b.equipo_origen,
                    b.navegador,
                    b.fecha_evento,
                    u.nombres,
                    u.apellidos,
                    u.usuario
                FROM seguridad.bitacora_acceso b
                LEFT JOIN seguridad.usuario u
                    ON u.id_usuario = b.id_usuario
                ORDER BY b.id_bitacora_acceso DESC;
                """,
                conexion);
            comando.Parameters.Add("@take", SqlDbType.Int).Value = Math.Clamp(take, 20, 300);

            using var reader = comando.ExecuteReader();
            var items = new List<object>();

            while (reader.Read())
            {
                var nombre = reader.IsDBNull(reader.GetOrdinal("usuario"))
                    ? null
                    : SecuritySupport.BuildDisplayName(
                        reader.IsDBNull(reader.GetOrdinal("nombres")) ? null : reader.GetString(reader.GetOrdinal("nombres")),
                        reader.IsDBNull(reader.GetOrdinal("apellidos")) ? null : reader.GetString(reader.GetOrdinal("apellidos")));

                items.Add(new
                {
                    id = reader.GetInt64(reader.GetOrdinal("id_bitacora_acceso")),
                    usuario = reader.IsDBNull(reader.GetOrdinal("usuario"))
                        ? reader.IsDBNull(reader.GetOrdinal("usuario_digitado"))
                            ? "Sin usuario"
                            : reader.GetString(reader.GetOrdinal("usuario_digitado"))
                        : reader.GetString(reader.GetOrdinal("usuario")),
                    nombreCompleto = string.IsNullOrWhiteSpace(nombre) ? null : nombre,
                    resultado = reader.GetString(reader.GetOrdinal("resultado_acceso")),
                    detalle = reader.IsDBNull(reader.GetOrdinal("detalle_resultado"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("detalle_resultado")),
                    ip = reader.IsDBNull(reader.GetOrdinal("ip_origen"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("ip_origen")),
                    equipo = reader.IsDBNull(reader.GetOrdinal("equipo_origen"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("equipo_origen")),
                    navegador = reader.IsDBNull(reader.GetOrdinal("navegador"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("navegador")),
                    fechaEvento = reader.GetDateTime(reader.GetOrdinal("fecha_evento")).ToString("yyyy-MM-ddTHH:mm:ss"),
                });
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
                message = "No se pudo cargar la bitácora de accesos.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult BitacoraMovimientos(int take = 120)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contexto = ObtenerContextoSesion(conexion);
            if (contexto is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "Tu sesión no es válida o ya expiró.",
                });
            }

            if (!TieneRol(contexto, RolesAdministrativos))
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "No tienes permisos para ver la bitácora operativa.",
                });
            }

            using var comando = new SqlCommand(
                """
                SELECT TOP (@take)
                    id_bitacora_operativa,
                    modulo,
                    proceso,
                    tipo_evento,
                    referencia_texto,
                    descripcion_evento,
                    usuario_registro,
                    fecha_evento,
                    equipo,
                    ip_equipo
                FROM operacion.bitacora_operativa
                ORDER BY id_bitacora_operativa DESC;
                """,
                conexion);
            comando.Parameters.Add("@take", SqlDbType.Int).Value = Math.Clamp(take, 20, 300);

            using var reader = comando.ExecuteReader();
            var items = new List<object>();

            while (reader.Read())
            {
                items.Add(new
                {
                    id = reader.GetInt64(reader.GetOrdinal("id_bitacora_operativa")),
                    modulo = reader.GetString(reader.GetOrdinal("modulo")),
                    proceso = reader.GetString(reader.GetOrdinal("proceso")),
                    tipoEvento = reader.GetString(reader.GetOrdinal("tipo_evento")),
                    referencia = reader.IsDBNull(reader.GetOrdinal("referencia_texto"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("referencia_texto")),
                    descripcion = reader.GetString(reader.GetOrdinal("descripcion_evento")),
                    usuario = reader.GetString(reader.GetOrdinal("usuario_registro")),
                    fechaEvento = reader.GetDateTime(reader.GetOrdinal("fecha_evento")).ToString("yyyy-MM-ddTHH:mm:ss"),
                    equipo = reader.IsDBNull(reader.GetOrdinal("equipo")) ? null : reader.GetString(reader.GetOrdinal("equipo")),
                    ip = reader.IsDBNull(reader.GetOrdinal("ip_equipo")) ? null : reader.GetString(reader.GetOrdinal("ip_equipo")),
                });
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
                message = "No se pudo cargar la bitácora operativa.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult SugerirUsuario(string? nombres, string? apellidos)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var sugerido = SecuritySupport.GenerateUniqueUsername(
                nombres,
                apellidos,
                candidate => UsuarioExiste(conexion, candidate));

            return Json(new
            {
                ok = true,
                data = new
                {
                    usuario = sugerido,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo generar el usuario sugerido.",
                detail = ex.Message,
            });
        }
    }

    private object ConstruirSesionRespuesta(SecurityUser usuario, SessionInsertResult sesion)
    {
        using var conexion = new SqlConnection(ConexionDb.Cadena);
        conexion.Open();
        ModuleAccessSupport.EnsureUserModuleAccessSchema(conexion);

        var modules = ModuleAccessSupport.GetEffectiveModuleKeys(
            conexion,
            usuario.IdUsuario,
            usuario.Roles,
            usuario.Usuario)
            .OrderBy(value => value)
            .ToArray();

        return new
        {
            active = true,
            user = usuario.NombreCompleto,
            username = usuario.Usuario,
            displayName = usuario.NombreCompleto,
            roles = usuario.Roles,
            rolesLabel = string.Join(", ", usuario.Roles),
            modules,
            sessionToken = sesion.TokenSesion.ToString(),
            loginAt = sesion.FechaInicio.ToString("yyyy-MM-ddTHH:mm:ss"),
            requirePasswordChange = false,
        };
    }

    private SecurityUser? ObtenerUsuarioPorLogin(SqlConnection conexion, string username)
    {
        const string sql = """
            SELECT
                u.id_usuario,
                u.usuario,
                u.nombres,
                u.apellidos,
                u.correo,
                u.telefono,
                u.hash_clave,
                u.cambiar_clave_en_proximo_inicio,
                u.bloqueado,
                u.activo,
                u.intentos_fallidos
            FROM seguridad.usuario u
            WHERE u.usuario = @usuario
              AND u.activo = 1;

            SELECT r.codigo_rol
            FROM seguridad.usuario u
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = u.id_usuario
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
            WHERE u.usuario = @usuario
            ORDER BY r.codigo_rol;
            """;

        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = username.Trim();

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var user = new SecurityUser
        {
            IdUsuario = reader.GetInt64(reader.GetOrdinal("id_usuario")),
            Usuario = reader.GetString(reader.GetOrdinal("usuario")),
            Nombres = reader.GetString(reader.GetOrdinal("nombres")),
            Apellidos = reader.GetString(reader.GetOrdinal("apellidos")),
            Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString(reader.GetOrdinal("correo")),
            Telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString(reader.GetOrdinal("telefono")),
            HashClave = reader.GetString(reader.GetOrdinal("hash_clave")),
            CambiarClaveEnProximoInicio = reader.GetBoolean(reader.GetOrdinal("cambiar_clave_en_proximo_inicio")),
            Bloqueado = reader.GetBoolean(reader.GetOrdinal("bloqueado")),
            Activo = reader.GetBoolean(reader.GetOrdinal("activo")),
            IntentosFallidos = reader.GetInt32(reader.GetOrdinal("intentos_fallidos")),
        };

        reader.NextResult();
        while (reader.Read())
        {
            user.Roles.Add(reader.GetString(0));
        }

        return user;
    }

    private SecurityUser? ObtenerUsuarioPorId(SqlConnection conexion, long idUsuario)
    {
        const string sql = """
            SELECT
                u.id_usuario,
                u.usuario,
                u.nombres,
                u.apellidos,
                u.correo,
                u.telefono,
                u.hash_clave,
                u.cambiar_clave_en_proximo_inicio,
                u.bloqueado,
                u.activo,
                u.intentos_fallidos
            FROM seguridad.usuario u
            WHERE u.id_usuario = @id_usuario;

            SELECT r.codigo_rol
            FROM seguridad.usuario_rol ur
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
            WHERE ur.id_usuario = @id_usuario
              AND ur.activo = 1
            ORDER BY r.codigo_rol;
            """;

        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var user = new SecurityUser
        {
            IdUsuario = reader.GetInt64(reader.GetOrdinal("id_usuario")),
            Usuario = reader.GetString(reader.GetOrdinal("usuario")),
            Nombres = reader.GetString(reader.GetOrdinal("nombres")),
            Apellidos = reader.GetString(reader.GetOrdinal("apellidos")),
            Correo = reader.IsDBNull(reader.GetOrdinal("correo")) ? null : reader.GetString(reader.GetOrdinal("correo")),
            Telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString(reader.GetOrdinal("telefono")),
            HashClave = reader.GetString(reader.GetOrdinal("hash_clave")),
            CambiarClaveEnProximoInicio = reader.GetBoolean(reader.GetOrdinal("cambiar_clave_en_proximo_inicio")),
            Bloqueado = reader.GetBoolean(reader.GetOrdinal("bloqueado")),
            Activo = reader.GetBoolean(reader.GetOrdinal("activo")),
            IntentosFallidos = reader.GetInt32(reader.GetOrdinal("intentos_fallidos")),
        };

        reader.NextResult();
        while (reader.Read())
        {
            user.Roles.Add(reader.GetString(0));
        }

        return user;
    }

    private SessionContext? ObtenerContextoSesion(SqlConnection conexion, bool touchSession = true)
    {
        var tokenText = Request.Headers["X-Session-Token"].ToString().Trim();
        if (!Guid.TryParse(tokenText, out var tokenSesion))
        {
            return null;
        }

        var minutosExpiracion = ObtenerParametroEntero(conexion, "MINUTOS_EXPIRACION_SESION", 30);

        const string sql = """
            SELECT
                s.id_sesion_usuario,
                s.id_usuario,
                s.token_sesion,
                s.fecha_inicio,
                s.fecha_ultimo_movimiento,
                u.usuario,
                u.nombres,
                u.apellidos,
                u.activo,
                u.bloqueado
            FROM seguridad.sesion_usuario s
            INNER JOIN seguridad.usuario u
                ON u.id_usuario = s.id_usuario
            WHERE s.token_sesion = @token_sesion
              AND s.activa = 1;

            SELECT r.codigo_rol
            FROM seguridad.sesion_usuario s
            INNER JOIN seguridad.usuario_rol ur
                ON ur.id_usuario = s.id_usuario
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
            WHERE s.token_sesion = @token_sesion
            ORDER BY r.codigo_rol;
            """;

        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = tokenSesion;

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var fechaInicio = reader.GetDateTime(reader.GetOrdinal("fecha_inicio"));
        var fechaUltimoMovimiento = reader.IsDBNull(reader.GetOrdinal("fecha_ultimo_movimiento"))
            ? fechaInicio
            : reader.GetDateTime(reader.GetOrdinal("fecha_ultimo_movimiento"));

        if (fechaUltimoMovimiento.AddMinutes(minutosExpiracion) < DateTime.Now)
        {
            reader.Close();
            CerrarSesion(conexion, tokenSesion, "EXPIRADA");
            return null;
        }

        var context = new SessionContext
        {
            IdSesionUsuario = reader.GetInt64(reader.GetOrdinal("id_sesion_usuario")),
            IdUsuario = reader.GetInt64(reader.GetOrdinal("id_usuario")),
            TokenSesion = tokenSesion,
            Username = reader.GetString(reader.GetOrdinal("usuario")),
            DisplayName = SecuritySupport.BuildDisplayName(
                reader.GetString(reader.GetOrdinal("nombres")),
                reader.GetString(reader.GetOrdinal("apellidos"))),
        };

        reader.NextResult();
        while (reader.Read())
        {
            context.Roles.Add(reader.GetString(0));
        }

        reader.Close();

        if (touchSession)
        {
            using var updateCommand = new SqlCommand(
                """
                UPDATE seguridad.sesion_usuario
                SET fecha_ultimo_movimiento = SYSDATETIME()
                WHERE token_sesion = @token_sesion;
                """,
                conexion);
            updateCommand.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = tokenSesion;
            updateCommand.ExecuteNonQuery();
        }

        return context;
    }

    private SessionInsertResult CrearSesion(SqlConnection conexion, SecurityUser usuario)
    {
        var token = Guid.NewGuid();
        var now = DateTime.Now;

        using (var comando = new SqlCommand(
            """
            INSERT INTO seguridad.sesion_usuario
            (
                id_usuario,
                token_sesion,
                ip_origen,
                equipo_origen,
                navegador,
                fecha_inicio,
                fecha_ultimo_movimiento,
                activa
            )
            VALUES
            (
                @id_usuario,
                @token_sesion,
                @ip_origen,
                @equipo_origen,
                @navegador,
                @fecha_inicio,
                @fecha_ultimo_movimiento,
                1
            );
            """,
            conexion))
        {
            comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = usuario.IdUsuario;
            comando.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = token;
            comando.Parameters.Add("@ip_origen", SqlDbType.NVarChar, 100).Value = ToDbValue(GetClientIp());
            comando.Parameters.Add("@equipo_origen", SqlDbType.NVarChar, 300).Value = ToDbValue(Environment.MachineName);
            comando.Parameters.Add("@navegador", SqlDbType.NVarChar, 500).Value = ToDbValue(GetClientBrowser());
            comando.Parameters.Add("@fecha_inicio", SqlDbType.DateTime2).Value = now;
            comando.Parameters.Add("@fecha_ultimo_movimiento", SqlDbType.DateTime2).Value = now;
            comando.ExecuteNonQuery();
        }

        using (var comando = new SqlCommand(
            """
            UPDATE seguridad.usuario
            SET
                intentos_fallidos = 0,
                fecha_ultimo_acceso = SYSDATETIME(),
                fecha_actualizacion = SYSDATETIME()
            WHERE id_usuario = @id_usuario;
            """,
            conexion))
        {
            comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = usuario.IdUsuario;
            comando.ExecuteNonQuery();
        }

        return new SessionInsertResult
        {
            TokenSesion = token,
            FechaInicio = now,
        };
    }

    private void CerrarSesion(SqlConnection conexion, Guid tokenSesion, string motivo)
    {
        using var comando = new SqlCommand(
            """
            UPDATE seguridad.sesion_usuario
            SET
                activa = 0,
                fecha_cierre = SYSDATETIME(),
                motivo_cierre = @motivo_cierre
            WHERE token_sesion = @token_sesion
              AND activa = 1;
            """,
            conexion);
        comando.Parameters.Add("@motivo_cierre", SqlDbType.NVarChar, 300).Value = motivo;
        comando.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = tokenSesion;
        comando.ExecuteNonQuery();
    }

    private void ReiniciarIntentos(SqlConnection conexion, long idUsuario)
    {
        using var comando = new SqlCommand(
            """
            UPDATE seguridad.usuario
            SET
                intentos_fallidos = 0,
                fecha_actualizacion = SYSDATETIME()
            WHERE id_usuario = @id_usuario;
            """,
            conexion);
        comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value = idUsuario;
        comando.ExecuteNonQuery();
    }

    private int ObtenerParametroEntero(SqlConnection conexion, string codigoParametro, int valorPorDefecto)
    {
        using var comando = new SqlCommand(
            """
            SELECT TOP (1) valor_parametro
            FROM seguridad.parametro_seguridad
            WHERE codigo_parametro = @codigo_parametro
              AND activo = 1;
            """,
            conexion);
        comando.Parameters.Add("@codigo_parametro", SqlDbType.NVarChar, 200).Value = codigoParametro;

        var valor = comando.ExecuteScalar()?.ToString();
        return int.TryParse(valor, out var parsed) ? parsed : valorPorDefecto;
    }

    private SystemConfigurationDto CargarConfiguracionGeneral(SqlConnection conexion)
    {
        const string sql = """
            SELECT TOP (1)
                e.id_empresa,
                e.razon_social,
                e.nombre_comercial,
                e.ruc,
                e.telefono AS telefono_empresa,
                e.correo AS correo_empresa,
                e.direccion AS direccion_empresa,
                e.logo_url AS logo_empresa_url,
                cg.id_configuracion_general,
                cg.nombre_sistema,
                cg.tema_color,
                cg.logo_login_url,
                cg.logo_sidebar_url,
                cg.nombre_gerente_rrhh,
                cg.texto_footer,
                cg.correo_soporte,
                cg.telefono_soporte,
                cg.mostrar_logo_login
            FROM empresa.empresa e
            LEFT JOIN empresa.configuracion_general cg
                ON cg.id_empresa = e.id_empresa
               AND cg.activo = 1
            WHERE e.activo = 1
            ORDER BY e.id_empresa;
            """;

        using var comando = new SqlCommand(sql, conexion);
        using var reader = comando.ExecuteReader();

        if (!reader.Read())
        {
            return new SystemConfigurationDto();
        }

        return new SystemConfigurationDto
        {
            IdEmpresa = reader.GetInt64(reader.GetOrdinal("id_empresa")),
            IdConfiguracionGeneral = reader.IsDBNull(reader.GetOrdinal("id_configuracion_general"))
                ? 0
                : reader.GetInt64(reader.GetOrdinal("id_configuracion_general")),
            RazonSocial = reader.IsDBNull(reader.GetOrdinal("razon_social"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("razon_social")),
            NombreComercial = reader.IsDBNull(reader.GetOrdinal("nombre_comercial"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("nombre_comercial")),
            Ruc = reader.IsDBNull(reader.GetOrdinal("ruc"))
                ? null
                : reader.GetString(reader.GetOrdinal("ruc")),
            TelefonoEmpresa = reader.IsDBNull(reader.GetOrdinal("telefono_empresa"))
                ? null
                : reader.GetString(reader.GetOrdinal("telefono_empresa")),
            CorreoEmpresa = reader.IsDBNull(reader.GetOrdinal("correo_empresa"))
                ? null
                : reader.GetString(reader.GetOrdinal("correo_empresa")),
            DireccionEmpresa = reader.IsDBNull(reader.GetOrdinal("direccion_empresa"))
                ? null
                : reader.GetString(reader.GetOrdinal("direccion_empresa")),
            LogoEmpresaUrl = reader.IsDBNull(reader.GetOrdinal("logo_empresa_url"))
                ? null
                : reader.GetString(reader.GetOrdinal("logo_empresa_url")),
            NombreSistema = reader.IsDBNull(reader.GetOrdinal("nombre_sistema"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("nombre_sistema")),
            TemaColor = reader.IsDBNull(reader.GetOrdinal("tema_color"))
                ? null
                : reader.GetString(reader.GetOrdinal("tema_color")),
            LogoLoginUrl = reader.IsDBNull(reader.GetOrdinal("logo_login_url"))
                ? (reader.IsDBNull(reader.GetOrdinal("logo_empresa_url"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("logo_empresa_url")))
                : reader.GetString(reader.GetOrdinal("logo_login_url")),
            LogoSidebarUrl = reader.IsDBNull(reader.GetOrdinal("logo_sidebar_url"))
                ? (reader.IsDBNull(reader.GetOrdinal("logo_empresa_url"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("logo_empresa_url")))
                : reader.GetString(reader.GetOrdinal("logo_sidebar_url")),
            NombreGerenteRrhh = reader.IsDBNull(reader.GetOrdinal("nombre_gerente_rrhh"))
                ? null
                : reader.GetString(reader.GetOrdinal("nombre_gerente_rrhh")),
            TextoFooter = reader.IsDBNull(reader.GetOrdinal("texto_footer"))
                ? null
                : reader.GetString(reader.GetOrdinal("texto_footer")),
            CorreoSoporte = reader.IsDBNull(reader.GetOrdinal("correo_soporte"))
                ? null
                : reader.GetString(reader.GetOrdinal("correo_soporte")),
            TelefonoSoporte = reader.IsDBNull(reader.GetOrdinal("telefono_soporte"))
                ? null
                : reader.GetString(reader.GetOrdinal("telefono_soporte")),
            MostrarLogoLogin = !reader.IsDBNull(reader.GetOrdinal("mostrar_logo_login")) &&
                reader.GetBoolean(reader.GetOrdinal("mostrar_logo_login")),
        };
    }

    private void UpsertParametroSeguridad(
        SqlConnection conexion,
        SqlTransaction transaccion,
        string codigoParametro,
        string valor,
        string descripcion)
    {
        using var comando = new SqlCommand(
            """
            IF EXISTS
            (
                SELECT 1
                FROM seguridad.parametro_seguridad
                WHERE codigo_parametro = @codigo_parametro
            )
            BEGIN
                UPDATE seguridad.parametro_seguridad
                SET
                    valor_parametro = @valor_parametro,
                    descripcion = @descripcion,
                    activo = 1
                WHERE codigo_parametro = @codigo_parametro;
            END
            ELSE
            BEGIN
                INSERT INTO seguridad.parametro_seguridad
                (
                    codigo_parametro,
                    valor_parametro,
                    descripcion,
                    activo,
                    fecha_registro
                )
                VALUES
                (
                    @codigo_parametro,
                    @valor_parametro,
                    @descripcion,
                    1,
                    SYSDATETIME()
                );
            END
            """,
            conexion,
            transaccion);
        comando.Parameters.Add("@codigo_parametro", SqlDbType.NVarChar, 200).Value = codigoParametro;
        comando.Parameters.Add("@valor_parametro", SqlDbType.NVarChar, 200).Value = valor;
        comando.Parameters.Add("@descripcion", SqlDbType.NVarChar, 500).Value = descripcion;
        comando.ExecuteNonQuery();
    }

    private bool UsuarioExiste(SqlConnection conexion, string username)
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

    private void RegistrarBitacoraAcceso(
        SqlConnection conexion,
        long? idUsuario,
        string usuarioDigitado,
        string resultado,
        string detalle)
    {
        using var comando = new SqlCommand(
            """
            INSERT INTO seguridad.bitacora_acceso
            (
                id_usuario,
                usuario_digitado,
                resultado_acceso,
                detalle_resultado,
                ip_origen,
                equipo_origen,
                navegador,
                fecha_evento
            )
            VALUES
            (
                @id_usuario,
                @usuario_digitado,
                @resultado_acceso,
                @detalle_resultado,
                @ip_origen,
                @equipo_origen,
                @navegador,
                SYSDATETIME()
            );
            """,
            conexion);

        comando.Parameters.Add("@id_usuario", SqlDbType.BigInt).Value =
            idUsuario.HasValue ? idUsuario.Value : DBNull.Value;
        comando.Parameters.Add("@usuario_digitado", SqlDbType.NVarChar, 200).Value = ToDbValue(usuarioDigitado);
        comando.Parameters.Add("@resultado_acceso", SqlDbType.NVarChar, 60).Value = resultado;
        comando.Parameters.Add("@detalle_resultado", SqlDbType.NVarChar, 500).Value = ToDbValue(detalle);
        comando.Parameters.Add("@ip_origen", SqlDbType.NVarChar, 100).Value = ToDbValue(GetClientIp());
        comando.Parameters.Add("@equipo_origen", SqlDbType.NVarChar, 300).Value = ToDbValue(Environment.MachineName);
        comando.Parameters.Add("@navegador", SqlDbType.NVarChar, 500).Value = ToDbValue(GetClientBrowser());
        comando.ExecuteNonQuery();
    }

    private void RegistrarBitacoraOperativa(
        SqlConnection conexion,
        string modulo,
        string proceso,
        string tipoEvento,
        long? idReferencia,
        string? referenciaTexto,
        string descripcion,
        string usuarioRegistro)
    {
        using var comando = new SqlCommand(
            """
            INSERT INTO operacion.bitacora_operativa
            (
                modulo,
                proceso,
                tipo_evento,
                id_referencia,
                referencia_texto,
                descripcion_evento,
                datos_resumen,
                usuario_registro,
                fecha_evento,
                equipo,
                ip_equipo
            )
            VALUES
            (
                @modulo,
                @proceso,
                @tipo_evento,
                @id_referencia,
                @referencia_texto,
                @descripcion_evento,
                NULL,
                @usuario_registro,
                SYSDATETIME(),
                @equipo,
                @ip_equipo
            );
            """,
            conexion);

        comando.Parameters.Add("@modulo", SqlDbType.NVarChar, 100).Value = modulo;
        comando.Parameters.Add("@proceso", SqlDbType.NVarChar, 200).Value = proceso;
        comando.Parameters.Add("@tipo_evento", SqlDbType.NVarChar, 100).Value = tipoEvento;
        comando.Parameters.Add("@id_referencia", SqlDbType.BigInt).Value =
            idReferencia.HasValue ? idReferencia.Value : DBNull.Value;
        comando.Parameters.Add("@referencia_texto", SqlDbType.NVarChar, 200).Value = ToDbValue(referenciaTexto);
        comando.Parameters.Add("@descripcion_evento", SqlDbType.NVarChar, 2000).Value = descripcion;
        comando.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 200).Value = usuarioRegistro;
        comando.Parameters.Add("@equipo", SqlDbType.NVarChar, 200).Value = ToDbValue(Environment.MachineName);
        comando.Parameters.Add("@ip_equipo", SqlDbType.NVarChar, 100).Value = ToDbValue(GetClientIp());
        comando.ExecuteNonQuery();
    }

    private static bool TieneRol(SessionContext contexto, IEnumerable<string> rolesPermitidos)
    {
        var roles = contexto.Roles.Select(role => role.ToUpperInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rolesPermitidos.Any(role => roles.Contains(role));
    }

    private string GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "LOCAL";
    }

    private string GetClientBrowser()
    {
        return Request.Headers.UserAgent.ToString();
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    public sealed class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class ChangePasswordRequest
    {
        public string Username { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public sealed class SaveUserModulesRequest
    {
        public string[] ModuleKeys { get; set; } = [];
        public bool UseAutomatic { get; set; }
    }

    public sealed class SaveSystemConfigurationRequest
    {
        public long IdEmpresa { get; set; }
        public long IdConfiguracionGeneral { get; set; }
        public string NombreSistema { get; set; } = string.Empty;
        public string? TemaColor { get; set; }
        public string? LogoLoginUrl { get; set; }
        public string? LogoSidebarUrl { get; set; }
        public string? NombreGerenteRrhh { get; set; }
        public string? TextoFooter { get; set; }
        public string? CorreoSoporte { get; set; }
        public string? TelefonoSoporte { get; set; }
        public bool MostrarLogoLogin { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string NombreComercial { get; set; } = string.Empty;
        public string? Ruc { get; set; }
        public string? TelefonoEmpresa { get; set; }
        public string? CorreoEmpresa { get; set; }
        public string? DireccionEmpresa { get; set; }
        public string? LogoEmpresaUrl { get; set; }
    }

    public sealed class SaveSecurityParametersRequest
    {
        public int IntentosMaximos { get; set; } = 6;
        public int MinutosExpiracionSesion { get; set; } = 30;
        public int HorasExpiracionRecuperacion { get; set; } = 24;
    }

    public sealed class SaveInstitutionalExchangeRateRequest
    {
        public string FechaTipoCambio { get; set; } = string.Empty;
        public decimal? ValorCompra { get; set; }
        public decimal? ValorVenta { get; set; }
        public decimal? ValorReferencia { get; set; }
        public string? Observacion { get; set; }
    }

    private sealed class SystemConfigurationDto
    {
        public long IdEmpresa { get; set; }
        public long IdConfiguracionGeneral { get; set; }
        public string NombreSistema { get; set; } = string.Empty;
        public string? TemaColor { get; set; }
        public string? LogoLoginUrl { get; set; }
        public string? LogoSidebarUrl { get; set; }
        public string? NombreGerenteRrhh { get; set; }
        public string? TextoFooter { get; set; }
        public string? CorreoSoporte { get; set; }
        public string? TelefonoSoporte { get; set; }
        public bool MostrarLogoLogin { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string NombreComercial { get; set; } = string.Empty;
        public string? Ruc { get; set; }
        public string? TelefonoEmpresa { get; set; }
        public string? CorreoEmpresa { get; set; }
        public string? DireccionEmpresa { get; set; }
        public string? LogoEmpresaUrl { get; set; }
    }

    private sealed class SecurityUser
    {
        public long IdUsuario { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string? Correo { get; set; }
        public string? Telefono { get; set; }
        public string HashClave { get; set; } = string.Empty;
        public bool CambiarClaveEnProximoInicio { get; set; }
        public bool Bloqueado { get; set; }
        public bool Activo { get; set; }
        public int IntentosFallidos { get; set; }
        public List<string> Roles { get; } = [];
        public string NombreCompleto => SecuritySupport.BuildDisplayName(Nombres, Apellidos);
    }

    private sealed class SessionContext
    {
        public long IdSesionUsuario { get; set; }
        public long IdUsuario { get; set; }
        public Guid TokenSesion { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> Roles { get; } = [];
    }

    private sealed class SessionInsertResult
    {
        public Guid TokenSesion { get; set; }
        public DateTime FechaInicio { get; set; }
    }
}
