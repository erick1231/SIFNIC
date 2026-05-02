using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Nomina;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class PortalController : Controller
{
    private readonly IWebHostEnvironment _environment;

    public PortalController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    public IActionResult MiContexto()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);
            RrhhSupport.EnsureEmployeeProfileSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
            if (employee?.IdEmpleado is null)
            {
                return Json(new
                {
                    ok = true,
                    data = new
                    {
                        session = new
                        {
                            session.Username,
                            session.DisplayName,
                            session.Roles,
                        },
                        hasEmployee = false,
                        message = "Tu usuario no esta vinculado todavia a una ficha de empleado.",
                    },
                });
            }

            var employeeId = employee.IdEmpleado.Value;
            var profile = BuildEmployeeProfile(connection, employeeId);
            var vacation = RrhhSupport.CalculateVacationBalance(connection, null, employeeId, DateTime.Today);
            var summary = BuildPortalSummary(connection, employeeId);
            var overtimeTypes = LoadOvertimeTypes(connection);

            return Json(new
            {
                ok = true,
                data = new
                {
                    session = new
                    {
                        session.Username,
                        session.DisplayName,
                        session.Roles,
                        rolesLabel = string.Join(", ", session.Roles),
                    },
                    hasEmployee = true,
                    employee = profile,
                    vacationBalance = new
                    {
                        vacation.DiasAcumulados,
                        vacation.DiasTomadosVacacion,
                        diasConsumidos = vacation.DiasTomadosVacacion,
                        diasPendientes = vacation.DiasPendientesVacacion,
                        vacation.DiasDisponibles,
                        vacation.TieneContratoVigente,
                        vacation.CodigoTipoContratoVigente,
                        vacation.NombreTipoContratoVigente,
                        vacation.AcumulaVacaciones,
                        vacation.TieneHistorialElegible,
                        vacation.MotivoNoAcumulacion,
                    },
                    summary,
                    overtimeTypes,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar tu portal.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    [RequestSizeLimit(5242880)]
    public IActionResult SubirMiFotoPerfil([FromForm] IFormFile? archivo)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);
            RrhhSupport.EnsureEmployeeProfileSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
            if (employee?.IdEmpleado is not long employeeId)
            {
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Tu usuario no esta vinculado a una ficha de empleado.",
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

            var currentPhotoUrl = EmployeePhotoSupport.GetPhotoUrl(connection, null, employeeId);
            var photoUrl = EmployeePhotoSupport.SavePhotoFile(
                _environment,
                archivo!,
                employee.CodigoEmpleado ?? $"EMP-{employeeId}");
            EmployeePhotoSupport.UpdatePhotoUrl(connection, null, employeeId, photoUrl);
            EmployeePhotoSupport.DeleteManagedPhoto(_environment, currentPhotoUrl, photoUrl);

            RrhhSupport.RegisterBitacora(
                connection,
                null,
                HttpContext,
                "PORTAL_EMPLEADO",
                "FOTO_PERFIL",
                employeeId,
                employee.CodigoEmpleado ?? employeeId.ToString(),
                $"El colaborador actualizo su foto de perfil ({employee.CodigoEmpleado}).",
                new
                {
                    empleado = employee.CodigoEmpleado,
                    fotoPerfilUrl = photoUrl,
                });

            return Json(new
            {
                ok = true,
                message = "Foto de perfil actualizada correctamente.",
                data = new
                {
                    fotoPerfilUrl = photoUrl,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar tu foto de perfil.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ActualizarMiVacacion(long id, [FromBody] PortalVacationEditModel model)
    {
        var errors = ValidatePortalVacationEdit(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos de la vacacion.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
            if (employee?.IdEmpleado is not long employeeId)
            {
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Tu usuario no esta vinculado a una ficha de empleado.",
                });
            }

            using var transaction = connection.BeginTransaction();
            var current = ObtenerVacacionInterna(connection, transaction, id);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Vacacion no encontrada.",
                });
            }

            if (current.IdEmpleado != employeeId)
            {
                transaction.Rollback();
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes editar tus propias vacaciones.",
                });
            }

            if (!string.Equals(current.EstadoVacacion, "SOLICITADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo puedes editar vacaciones pendientes de aprobacion.",
                });
            }

            var validationErrors = ValidatePortalVacationBusinessRules(connection, transaction, employeeId, model, id);
            if (validationErrors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la vacacion.",
                    errors = validationErrors,
                });
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.vacacion
                SET
                    fecha_inicio = @fecha_inicio,
                    fecha_fin = @fecha_fin,
                    dias_solicitados = @dias_solicitados,
                    observacion_solicitud = @observacion_solicitud
                WHERE id_vacacion = @id_vacacion;
                """,
                connection,
                transaction))
            {
                AssignPortalVacationParameters(command, model);
                command.Parameters.Add("@id_vacacion", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerVacacionInterna(connection, transaction, id)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PORTAL_EMPLEADO",
                "VACACION_MODIFICACION",
                updated.IdVacacion,
                $"VAC-{updated.IdVacacion}",
                $"El colaborador actualizo su solicitud de vacacion {updated.IdVacacion}.",
                new
                {
                    antes = current,
                    despues = updated,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Vacacion actualizada correctamente.",
                data = updated,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar tu vacacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpDelete("{id:long}")]
    public IActionResult EliminarMiVacacion(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
            if (employee?.IdEmpleado is not long employeeId)
            {
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Tu usuario no esta vinculado a una ficha de empleado.",
                });
            }

            using var transaction = connection.BeginTransaction();
            var current = ObtenerVacacionInterna(connection, transaction, id);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Vacacion no encontrada.",
                });
            }

            if (current.IdEmpleado != employeeId)
            {
                transaction.Rollback();
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes retirar tus propias vacaciones.",
                });
            }

            if (!string.Equals(current.EstadoVacacion, "SOLICITADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo puedes retirar vacaciones pendientes de aprobacion.",
                });
            }

            using (var command = new SqlCommand(
                "DELETE FROM rrhh.vacacion WHERE id_vacacion = @id_vacacion;",
                connection,
                transaction))
            {
                command.Parameters.Add("@id_vacacion", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PORTAL_EMPLEADO",
                "VACACION_RETIRO",
                current.IdVacacion,
                $"VAC-{current.IdVacacion}",
                $"El colaborador retiro su solicitud de vacacion {current.IdVacacion}.",
                current);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Vacacion retirada correctamente.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo retirar tu vacacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ActualizarMiHoraExtra(long id, [FromBody] PortalOvertimeEditModel model)
    {
        var errors = ValidatePortalOvertimeEdit(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos de la hora extra.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
            if (employee?.IdEmpleado is not long employeeId)
            {
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Tu usuario no esta vinculado a una ficha de empleado.",
                });
            }

            using var transaction = connection.BeginTransaction();
            var current = ObtenerHoraExtraInterna(connection, transaction, id);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Hora extra no encontrada.",
                });
            }

            if (current.IdEmpleado != employeeId)
            {
                transaction.Rollback();
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes editar tus propias horas extra.",
                });
            }

            if (!string.Equals(current.EstadoHoraExtra, "REGISTRADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo puedes editar horas extra pendientes de aprobacion.",
                });
            }

            var validationErrors = ValidatePortalOvertimeBusinessRules(connection, transaction, employeeId, model, id);
            if (validationErrors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la hora extra.",
                    errors = validationErrors,
                });
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.hora_extra
                SET
                    id_tipo_hora_extra = @id_tipo_hora_extra,
                    fecha_hora_extra = @fecha_hora_extra,
                    cantidad_horas = @cantidad_horas,
                    observacion = @observacion
                WHERE id_hora_extra = @id_hora_extra;
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@id_tipo_hora_extra", SqlDbType.BigInt).Value = model.IdTipoHoraExtra;
                command.Parameters.Add("@fecha_hora_extra", SqlDbType.Date).Value = DateTime.Parse(model.FechaHoraExtra!);
                command.Parameters.Add("@cantidad_horas", SqlDbType.Decimal).Value = model.CantidadHoras;
                command.Parameters["@cantidad_horas"].Precision = 10;
                command.Parameters["@cantidad_horas"].Scale = 2;
                command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value =
                    RrhhSupport.ToDbValue(string.IsNullOrWhiteSpace(model.Observacion) ? null : model.Observacion.Trim());
                command.Parameters.Add("@id_hora_extra", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerHoraExtraInterna(connection, transaction, id)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PORTAL_EMPLEADO",
                "HORA_EXTRA_MODIFICACION",
                updated.IdHoraExtra,
                $"HEX-{updated.IdHoraExtra}",
                $"El colaborador actualizo su hora extra {updated.IdHoraExtra}.",
                new
                {
                    antes = current,
                    despues = updated,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Hora extra actualizada correctamente.",
                data = updated,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar tu hora extra.",
                detail = ex.Message,
            });
        }
    }

    [HttpDelete("{id:long}")]
    public IActionResult EliminarMiHoraExtra(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
            if (employee?.IdEmpleado is not long employeeId)
            {
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Tu usuario no esta vinculado a una ficha de empleado.",
                });
            }

            using var transaction = connection.BeginTransaction();
            var current = ObtenerHoraExtraInterna(connection, transaction, id);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Hora extra no encontrada.",
                });
            }

            if (current.IdEmpleado != employeeId)
            {
                transaction.Rollback();
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes retirar tus propias horas extra.",
                });
            }

            if (!string.Equals(current.EstadoHoraExtra, "REGISTRADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo puedes retirar horas extra pendientes de aprobacion.",
                });
            }

            using (var command = new SqlCommand(
                "DELETE FROM rrhh.hora_extra WHERE id_hora_extra = @id_hora_extra;",
                connection,
                transaction))
            {
                command.Parameters.Add("@id_hora_extra", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PORTAL_EMPLEADO",
                "HORA_EXTRA_RETIRO",
                current.IdHoraExtra,
                $"HEX-{current.IdHoraExtra}",
                $"El colaborador retiro su hora extra {current.IdHoraExtra}.",
                current);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Hora extra retirada correctamente.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo retirar tu hora extra.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult MisEsquelas()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);
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

            var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
            if (employee?.IdEmpleado is null)
            {
                return Json(new
                {
                    ok = true,
                    data = Array.Empty<object>(),
                });
            }

            NominaSupport.EnsurePayslipRecordsForEmployee(
                connection,
                null,
                employee.IdEmpleado.Value,
                session.Username);

            var payslips = LoadPortalPayslips(connection, employee.IdEmpleado.Value);
            return Json(new
            {
                ok = true,
                data = payslips,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar tus esquelas de pago.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult SupervisorContexto()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var scope = ResolveSupervisorAccess(connection, session);
            if (!scope.HasAccess || scope.Employee?.IdEmpleado is not long supervisorEmployeeId)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Tu usuario no tiene subordinados directos para usar esta bandeja.",
                });
            }

            var subordinates = RrhhSupport.ListSubordinates(connection, null, supervisorEmployeeId);
            var counts = BuildSupervisorCounts(connection, supervisorEmployeeId);

            return Json(new
            {
                ok = true,
                data = new
                {
                    session = new
                    {
                        session.Username,
                        session.DisplayName,
                        session.Roles,
                        rolesLabel = string.Join(", ", session.Roles),
                    },
                    employee = new
                    {
                        scope.Employee.IdEmpleado,
                        scope.Employee.CodigoEmpleado,
                        scope.Employee.NombreEmpleado,
                        scope.Employee.NombreDepartamento,
                        scope.Employee.NombreCargo,
                    },
                    scope = "DIRECT_REPORTS",
                    note = BuildSupervisorScopeNote(true, subordinates.Count),
                    counts = new
                    {
                        counts.PendingVacations,
                        pendingPermissions = 0,
                        counts.PendingOvertime,
                    },
                    subordinateCount = subordinates.Count,
                    subordinates = subordinates.Select(item => new
                    {
                        item.IdEmpleado,
                        item.CodigoEmpleado,
                        item.NombreEmpleado,
                        item.NombreDepartamento,
                        item.NombreCargo,
                        item.UsuarioSistema,
                    }),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la bandeja del supervisor.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult SupervisorNotificaciones()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var scope = ResolveSupervisorAccess(connection, session);
            if (!scope.HasAccess || scope.Employee?.IdEmpleado is not long supervisorEmployeeId)
            {
                return Json(new
                {
                    ok = true,
                    data = new
                    {
                        available = false,
                        totalPending = 0,
                        subordinateCount = 0,
                        note = scope.Employee?.IdEmpleado is null
                            ? "Tu usuario no esta vinculado a una ficha de empleado."
                            : "No tienes subordinados directos pendientes de aprobacion.",
                        counts = new
                        {
                            pendingVacations = 0,
                            pendingPermissions = 0,
                            pendingOvertime = 0,
                        },
                        items = Array.Empty<object>(),
                    },
                });
            }

            var counts = BuildSupervisorCounts(connection, supervisorEmployeeId);
            var items = LoadSupervisorNotificationItems(connection, supervisorEmployeeId);

            return Json(new
            {
                ok = true,
                data = new
                {
                    available = true,
                    totalPending = counts.PendingVacations + counts.PendingOvertime,
                    subordinateCount = scope.DirectReportCount,
                    note = BuildSupervisorScopeNote(true, scope.DirectReportCount),
                    counts = new
                    {
                        pendingVacations = counts.PendingVacations,
                        pendingPermissions = 0,
                        pendingOvertime = counts.PendingOvertime,
                    },
                    items,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar las notificaciones del supervisor.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult SupervisorPendientes()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var scope = ResolveSupervisorAccess(connection, session);
            if (!scope.HasAccess || scope.Employee?.IdEmpleado is not long supervisorEmployeeId)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Tu usuario no tiene subordinados directos para usar esta bandeja.",
                });
            }

            var vacations = LoadSupervisorVacations(connection, supervisorEmployeeId);
            var overtime = LoadSupervisorOvertime(connection, supervisorEmployeeId);

            return Json(new
            {
                ok = true,
                data = new
                {
                    vacations,
                    permissions = Array.Empty<object>(),
                    overtime,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar la bandeja de aprobaciones.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ResolverSupervisorVacacion(long id, [FromBody] PortalVacationResolutionModel model)
    {
        var action = NormalizeResolutionAction(model.Action);
        if (action is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona una accion valida para resolver la vacacion.",
            });
        }

        if (action == "RECHAZAR" && string.IsNullOrWhiteSpace(model.Observation))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Explica el motivo del rechazo.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var scope = ResolveSupervisorAccess(connection, session);
            if (!scope.HasAccess || scope.Employee?.IdEmpleado is not long supervisorEmployeeId)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes resolver solicitudes de tus subordinados directos.",
                });
            }

            using var transaction = connection.BeginTransaction();

            var current = ObtenerVacacionInterna(connection, transaction, id);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Vacacion no encontrada.",
                });
            }

            if (!EsSubordinadoDirecto(connection, transaction, supervisorEmployeeId, current.IdEmpleado))
            {
                transaction.Rollback();
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes resolver solicitudes de tus subordinados directos.",
                });
            }

            if (!string.Equals(current.EstadoVacacion, "SOLICITADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo se pueden resolver vacaciones solicitadas.",
                });
            }

            if (action == "APROBAR" && (!(model.ApprovedDays > 0) || model.ApprovedDays > current.DiasSolicitados))
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Ingresa una cantidad de dias aprobados valida.",
                });
            }

            if (action == "APROBAR")
            {
                var snapshot = RrhhSupport.CalculateVacationBalance(
                    connection,
                    transaction,
                    current.IdEmpleado,
                    DateTime.Parse(current.FechaFin));

                if (snapshot.DiasDisponibles < model.ApprovedDays)
                {
                    transaction.Rollback();
                    return BadRequest(new
                    {
                        ok = false,
                        message = RrhhSupport.BuildVacationAvailabilityMessage(snapshot),
                    });
                }
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.vacacion
                SET
                    dias_aprobados = @dias_aprobados,
                    estado_vacacion = @estado_vacacion,
                    usuario_aprueba = @usuario_aprueba,
                    fecha_aprobacion = SYSDATETIME(),
                    observacion_aprobacion = @observacion_aprobacion
                WHERE id_vacacion = @id_vacacion;
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@dias_aprobados", SqlDbType.Decimal).Value =
                    action == "APROBAR" ? model.ApprovedDays : DBNull.Value;
                command.Parameters["@dias_aprobados"].Precision = 10;
                command.Parameters["@dias_aprobados"].Scale = 2;
                command.Parameters.Add("@estado_vacacion", SqlDbType.NVarChar, 30).Value =
                    action == "APROBAR" ? "APROBADA" : "RECHAZADA";
                command.Parameters.Add("@usuario_aprueba", SqlDbType.NVarChar, 100).Value =
                    RrhhSupport.GetOperatorUser(Request);
                command.Parameters.Add("@observacion_aprobacion", SqlDbType.NVarChar, 500).Value =
                    RrhhSupport.ToDbValue(model.Observation);
                command.Parameters.Add("@id_vacacion", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerVacacionInterna(connection, transaction, id)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "VACACIONES",
                action == "APROBAR" ? "APROBACION_SUPERVISOR" : "RECHAZO_SUPERVISOR",
                updated.IdVacacion,
                $"VAC-{updated.IdVacacion}",
                action == "APROBAR"
                    ? $"El supervisor aprobo la solicitud de vacacion {updated.IdVacacion}."
                    : $"El supervisor rechazo la solicitud de vacacion {updated.IdVacacion}.",
                updated);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = action == "APROBAR"
                    ? "Vacacion aprobada correctamente."
                    : "Vacacion rechazada correctamente.",
                data = updated,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo resolver la vacacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ResolverSupervisorPermiso(long id, [FromBody] PortalResolutionModel model)
    {
        var action = NormalizeResolutionAction(model.Action);
        if (action is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona una accion valida para resolver el permiso.",
            });
        }

        if (action == "RECHAZAR" && string.IsNullOrWhiteSpace(model.Observation))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Explica el motivo del rechazo.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var scope = ResolveSupervisorAccess(connection, session);
            if (!scope.HasAccess || scope.Employee?.IdEmpleado is not long supervisorEmployeeId)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes resolver solicitudes de tus subordinados directos.",
                });
            }

            using var transaction = connection.BeginTransaction();

            var current = ObtenerPermisoInterno(connection, transaction, id);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Permiso no encontrado.",
                });
            }

            if (!EsSubordinadoDirecto(connection, transaction, supervisorEmployeeId, current.IdEmpleado))
            {
                transaction.Rollback();
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes resolver solicitudes de tus subordinados directos.",
                });
            }

            if (!string.Equals(current.EstadoPermiso, "SOLICITADO", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo se pueden resolver permisos solicitados.",
                });
            }

            if (action == "APROBAR")
            {
                var snapshot = RrhhSupport.CalculateVacationBalance(
                    connection,
                    transaction,
                    current.IdEmpleado,
                    DateTime.Parse(current.FechaFin));

                if (snapshot.DiasDisponibles < current.CantidadDias)
                {
                    transaction.Rollback();
                    return BadRequest(new
                    {
                        ok = false,
                        message = RrhhSupport.BuildVacationAvailabilityMessage(snapshot),
                    });
                }
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.solicitud_permiso
                SET
                    estado_permiso = @estado_permiso,
                    usuario_aprueba = @usuario_aprueba,
                    fecha_aprobacion = SYSDATETIME(),
                    observacion = @observacion
                WHERE id_solicitud_permiso = @id_solicitud_permiso;
                """,
                connection,
                transaction))
            {
                var rawObservation = BuildPermissionObservationPayload(
                    current.ObservacionRaw,
                    current.EsMedioDia,
                    current.JornadaMedioDia,
                    model.Observation);

                if (!string.IsNullOrWhiteSpace(rawObservation) && rawObservation.Length > 500)
                {
                    transaction.Rollback();
                    return BadRequest(new
                    {
                        ok = false,
                        message = "La observacion del permiso supera el espacio disponible.",
                    });
                }

                command.Parameters.Add("@estado_permiso", SqlDbType.NVarChar, 30).Value =
                    action == "APROBAR" ? "APROBADO" : "RECHAZADO";
                command.Parameters.Add("@usuario_aprueba", SqlDbType.NVarChar, 100).Value =
                    RrhhSupport.GetOperatorUser(Request);
                command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value =
                    RrhhSupport.ToDbValue(rawObservation);
                command.Parameters.Add("@id_solicitud_permiso", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerPermisoInterno(connection, transaction, id)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PERMISOS",
                action == "APROBAR" ? "APROBACION_SUPERVISOR" : "RECHAZO_SUPERVISOR",
                updated.IdSolicitudPermiso,
                $"PERM-{updated.IdSolicitudPermiso}",
                action == "APROBAR"
                    ? $"El supervisor aprobo la solicitud de permiso {updated.IdSolicitudPermiso}."
                    : $"El supervisor rechazo la solicitud de permiso {updated.IdSolicitudPermiso}.",
                updated);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = action == "APROBAR"
                    ? "Permiso aprobado correctamente."
                    : "Permiso rechazado correctamente.",
                data = updated,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo resolver el permiso.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ResolverSupervisorHoraExtra(long id, [FromBody] PortalResolutionModel model)
    {
        var action = NormalizeResolutionAction(model.Action);
        if (action is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona una accion valida para resolver la hora extra.",
            });
        }

        if (action == "RECHAZAR" && string.IsNullOrWhiteSpace(model.Observation))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Explica el motivo del rechazo.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            RrhhSupport.EnsureEmployeeSupervisorSchema(connection);

            var session = ObtenerSesion(connection);
            if (session is null)
            {
                return StatusCode(401, new
                {
                    ok = false,
                    message = "La sesion no es valida o ya vencio.",
                });
            }

            var scope = ResolveSupervisorAccess(connection, session);
            if (!scope.HasAccess || scope.Employee?.IdEmpleado is not long supervisorEmployeeId)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes resolver solicitudes de tus subordinados directos.",
                });
            }

            using var transaction = connection.BeginTransaction();

            var current = ObtenerHoraExtraInterna(connection, transaction, id);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Hora extra no encontrada.",
                });
            }

            if (!EsSubordinadoDirecto(connection, transaction, supervisorEmployeeId, current.IdEmpleado))
            {
                transaction.Rollback();
                return StatusCode(403, new
                {
                    ok = false,
                    message = "Solo puedes resolver solicitudes de tus subordinados directos.",
                });
            }

            if (!string.Equals(current.EstadoHoraExtra, "REGISTRADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo se pueden resolver horas extra registradas.",
                });
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.hora_extra
                SET
                    estado_hora_extra = @estado_hora_extra,
                    usuario_aprueba = @usuario_aprueba,
                    fecha_aprobacion = SYSDATETIME(),
                    observacion = @observacion
                WHERE id_hora_extra = @id_hora_extra;
                """,
                connection,
                transaction))
            {
                command.Parameters.Add("@estado_hora_extra", SqlDbType.NVarChar, 30).Value =
                    action == "APROBAR" ? "APROBADA" : "RECHAZADA";
                command.Parameters.Add("@usuario_aprueba", SqlDbType.NVarChar, 100).Value =
                    RrhhSupport.GetOperatorUser(Request);
                command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value =
                    RrhhSupport.ToDbValue(model.Observation);
                command.Parameters.Add("@id_hora_extra", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerHoraExtraInterna(connection, transaction, id)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "HORAS_EXTRA",
                action == "APROBAR" ? "APROBACION_SUPERVISOR" : "RECHAZO_SUPERVISOR",
                updated.IdHoraExtra,
                $"HEX-{updated.IdHoraExtra}",
                action == "APROBAR"
                    ? $"El supervisor aprobo la hora extra {updated.IdHoraExtra}."
                    : $"El supervisor rechazo la hora extra {updated.IdHoraExtra}.",
                updated);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = action == "APROBAR"
                    ? "Hora extra aprobada correctamente."
                    : "Hora extra rechazada correctamente.",
                data = updated,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo resolver la hora extra.",
                detail = ex.Message,
            });
        }
    }

    private SessionContext? ObtenerSesion(SqlConnection connection)
    {
        var tokenText = Request.Headers["X-Session-Token"].ToString().Trim();
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
               AND ur.activo = 1
            INNER JOIN seguridad.rol r
                ON r.id_rol = ur.id_rol
               AND r.activo = 1
            WHERE s.token_sesion = @token_sesion
            ORDER BY r.codigo_rol;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@token_sesion", SqlDbType.UniqueIdentifier).Value = token;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var context = new SessionContext
        {
            IdSesionUsuario = reader.GetInt64(0),
            IdUsuario = reader.GetInt64(1),
            Username = reader.GetString(2),
            DisplayName = $"{reader.GetString(3)} {reader.GetString(4)}".Trim(),
        };

        reader.NextResult();
        while (reader.Read())
        {
            context.Roles.Add(reader.GetString(0));
        }

        reader.Close();

        using var updateCommand = new SqlCommand(
            """
            UPDATE seguridad.sesion_usuario
            SET fecha_ultimo_movimiento = SYSDATETIME()
            WHERE id_sesion_usuario = @id_sesion_usuario;
            """,
            connection);
        updateCommand.Parameters.Add("@id_sesion_usuario", SqlDbType.BigInt).Value = context.IdSesionUsuario;
        updateCommand.ExecuteNonQuery();

        return context;
    }

    private static SupervisorAccessScope ResolveSupervisorAccess(SqlConnection connection, SessionContext session)
    {
        var employee = RrhhSupport.FindEmployeeByUsername(connection, session.Username);
        if (employee?.IdEmpleado is not long employeeId)
        {
            return new SupervisorAccessScope
            {
                Employee = employee,
                DirectReportCount = 0,
                HasAccess = false,
            };
        }

        var directReportCount = RrhhSupport.CountSubordinates(connection, null, employeeId);
        return new SupervisorAccessScope
        {
            Employee = employee,
            DirectReportCount = directReportCount,
            HasAccess = directReportCount > 0,
        };
    }

    private static object BuildEmployeeProfile(SqlConnection connection, long idEmpleado)
    {
        const string sql = """
            SELECT TOP (1)
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.cedula,
                e.correo,
                e.telefono,
                e.foto_perfil_url,
                e.fecha_ingreso,
                e.fecha_nacimiento,
                d.nombre_departamento,
                c.nombre_cargo,
                ee.nombre_estado_empleado,
                contrato.numero_contrato,
                contrato.nombre_tipo_contrato,
                contrato.salario_base_mensual,
                contrato.moneda,
                supervisor.id_supervisor_empleado,
                supervisor.codigo_supervisor,
                supervisor.nombre_supervisor,
                supervisor.cargo_supervisor
            FROM rrhh.empleado e
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            OUTER APPLY
            (
                SELECT TOP (1)
                    co.numero_contrato,
                    tc.nombre_tipo_contrato,
                    co.salario_base_mensual,
                    co.moneda
                FROM rrhh.contrato co
                INNER JOIN rrhh.tipo_contrato tc
                    ON tc.id_tipo_contrato = co.id_tipo_contrato
                WHERE co.id_empleado = e.id_empleado
                  AND co.es_contrato_vigente = 1
                ORDER BY co.fecha_inicio DESC, co.id_contrato DESC
            ) contrato
            OUTER APPLY
            (
                SELECT TOP (1)
                    rel.id_supervisor_empleado,
                    sup.codigo_empleado AS codigo_supervisor,
                    COALESCE(NULLIF(sup.nombre_completo, N''), CONCAT(sup.nombres, N' ', sup.apellidos)) AS nombre_supervisor,
                    cargo_sup.nombre_cargo AS cargo_supervisor
                FROM rrhh.empleado_supervision rel
                INNER JOIN rrhh.empleado sup
                    ON sup.id_empleado = rel.id_supervisor_empleado
                LEFT JOIN rrhh.cargo cargo_sup
                    ON cargo_sup.id_cargo = sup.id_cargo
                WHERE rel.id_empleado = e.id_empleado
                  AND rel.activo = 1
                ORDER BY rel.fecha_asignacion DESC, rel.id_empleado_supervision DESC
            ) supervisor
            WHERE e.id_empleado = @id_empleado;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return new { };
        }

        var profile = new
        {
            idEmpleado = reader.GetInt64(0),
            codigoEmpleado = reader.GetString(1),
            nombreEmpleado = reader.GetString(2),
            cedula = reader.GetString(3),
            correo = reader.IsDBNull(4) ? null : reader.GetString(4),
            telefono = reader.IsDBNull(5) ? null : reader.GetString(5),
            fotoPerfilUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
            fechaIngreso = reader.GetDateTime(7).ToString("yyyy-MM-dd"),
            fechaNacimiento = reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToString("yyyy-MM-dd"),
            departamento = reader.GetString(9),
            cargo = reader.GetString(10),
            estado = reader.GetString(11),
            contratoNumero = reader.IsDBNull(12) ? null : reader.GetString(12),
            contratoTipo = reader.IsDBNull(13) ? null : reader.GetString(13),
            salarioBase = reader.IsDBNull(14) ? 0m : reader.GetDecimal(14),
            moneda = reader.IsDBNull(15) ? "NIO" : reader.GetString(15),
            idSupervisorEmpleado = reader.IsDBNull(16) ? (long?)null : reader.GetInt64(16),
            codigoSupervisor = reader.IsDBNull(17) ? null : reader.GetString(17),
            jefeInmediato = reader.IsDBNull(18) ? null : reader.GetString(18),
            cargoSupervisor = reader.IsDBNull(19) ? null : reader.GetString(19),
        };

        reader.Close();
        var structure = FormalOrganizationStructureSupport.GetEmployeeContext(connection, idEmpleado);

        return new
        {
            profile.idEmpleado,
            profile.codigoEmpleado,
            profile.nombreEmpleado,
            profile.cedula,
            profile.correo,
            profile.telefono,
            profile.fotoPerfilUrl,
            profile.fechaIngreso,
            profile.fechaNacimiento,
            profile.departamento,
            profile.cargo,
            profile.estado,
            profile.contratoNumero,
            profile.contratoTipo,
            profile.salarioBase,
            profile.moneda,
            profile.idSupervisorEmpleado,
            profile.codigoSupervisor,
            profile.jefeInmediato,
            profile.cargoSupervisor,
            idNodoEstructura = structure?.IdNodoEstructura,
            codigoNodoEstructura = structure?.CodigoNodo,
            nombreNodoEstructura = structure?.NombreNodo,
            tipoNodoEstructura = structure?.TipoNodo,
            tipoNodoEstructuraLabel = structure?.TipoNodoLabel,
            departamentoFormal = structure?.NombreDepartamento,
            cargoFormal = structure?.NombreCargo,
            rutaOrganizativa = structure?.RutaOrganizativa,
            reportaFormalmenteA = structure?.ReportaFormalmenteA,
            nombreNodoPadre = structure?.NombreNodoPadre,
            tipoNodoPadre = structure?.TipoNodoPadre,
            tipoNodoPadreLabel = structure?.TipoNodoPadreLabel,
            titularNodoPadre = structure?.TitularNodoPadre,
            estructuraFormal = structure,
        };
    }

    private static object BuildPortalSummary(SqlConnection connection, long idEmpleado)
    {
        const string sql = """
            DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
            DECLARE @inicio_mes DATE = DATEFROMPARTS(YEAR(@hoy), MONTH(@hoy), 1);
            DECLARE @inicio_semana DATE = DATEADD(DAY, 1 - DATEPART(WEEKDAY, @hoy), @hoy);

            SELECT
                COALESCE((
                    SELECT SUM(h.cantidad_horas)
                    FROM rrhh.hora_extra h
                    WHERE h.id_empleado = @id_empleado
                      AND h.fecha_hora_extra >= @inicio_semana
                      AND h.estado_hora_extra <> N'RECHAZADA'
                ), 0) AS horas_semana,
                COALESCE((
                    SELECT SUM(h.cantidad_horas)
                    FROM rrhh.hora_extra h
                    WHERE h.id_empleado = @id_empleado
                      AND h.fecha_hora_extra >= @inicio_mes
                      AND h.estado_hora_extra <> N'RECHAZADA'
                ), 0) AS horas_mes,
                (SELECT COUNT(1) FROM rrhh.hora_extra h WHERE h.id_empleado = @id_empleado) AS total_horas_extra,
                (SELECT COUNT(1) FROM rrhh.vacacion v WHERE v.id_empleado = @id_empleado AND v.estado_vacacion = N'SOLICITADA') AS vacaciones_pendientes,
                0 AS permisos_pendientes,
                (SELECT COUNT(1) FROM rrhh.hora_extra h WHERE h.id_empleado = @id_empleado AND h.estado_hora_extra = N'REGISTRADA') AS horas_extra_pendientes,
                (
                    SELECT COUNT(1)
                    FROM rrhh.empleado_supervision rel
                    INNER JOIN rrhh.empleado sub
                        ON sub.id_empleado = rel.id_empleado
                    WHERE rel.id_supervisor_empleado = @id_empleado
                      AND rel.activo = 1
                      AND sub.activo = 1
                ) AS subordinados_directos;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        using var reader = command.ExecuteReader();
        reader.Read();

        return new
        {
            horasSemana = reader.GetDecimal(0),
            horasMes = reader.GetDecimal(1),
            totalHorasExtra = reader.GetInt32(2),
            vacacionesPendientes = reader.GetInt32(3),
            permisosPendientes = 0,
            horasExtraPendientes = reader.GetInt32(5),
            subordinadosDirectos = reader.GetInt32(6),
        };
    }

    private static List<object> LoadOvertimeTypes(SqlConnection connection)
    {
        const string sql = """
            SELECT id_tipo_hora_extra, codigo_tipo_hora_extra, nombre_tipo_hora_extra
            FROM rrhh.tipo_hora_extra
            WHERE activo = 1
            ORDER BY nombre_tipo_hora_extra;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                code = reader.GetString(1),
                name = reader.GetString(2),
            });
        }

        return items;
    }

    private static List<object> LoadPermissionTypes(SqlConnection connection)
    {
        const string sql = """
            SELECT id_tipo_permiso, codigo_tipo_permiso, nombre_tipo_permiso
            FROM rrhh.tipo_permiso
            WHERE activo = 1
            ORDER BY nombre_tipo_permiso;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                code = reader.GetString(1),
                name = reader.GetString(2),
            });
        }

        return items;
    }

    private static SupervisorCounts BuildSupervisorCounts(SqlConnection connection, long idSupervisorEmpleado)
    {
        const string sql = """
            SELECT
                (
                    SELECT COUNT(1)
                    FROM rrhh.vacacion v
                    INNER JOIN rrhh.empleado_supervision rel
                        ON rel.id_empleado = v.id_empleado
                       AND rel.id_supervisor_empleado = @id_supervisor_empleado
                       AND rel.activo = 1
                    WHERE v.estado_vacacion = N'SOLICITADA'
                ) AS vacaciones,
                0 AS permisos,
                (
                    SELECT COUNT(1)
                    FROM rrhh.hora_extra h
                    INNER JOIN rrhh.empleado_supervision rel
                        ON rel.id_empleado = h.id_empleado
                       AND rel.id_supervisor_empleado = @id_supervisor_empleado
                       AND rel.activo = 1
                    WHERE h.estado_hora_extra = N'REGISTRADA'
                ) AS horas_extra;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;
        using var reader = command.ExecuteReader();
        reader.Read();

        return new SupervisorCounts
        {
            PendingVacations = reader.GetInt32(0),
            PendingPermissions = reader.GetInt32(1),
            PendingOvertime = reader.GetInt32(2),
        };
    }

    private static List<object> LoadSupervisorNotificationItems(SqlConnection connection, long idSupervisorEmpleado)
    {
        const string sql = """
            SELECT TOP (6)
                items.tipo,
                items.tipo_label,
                items.id_solicitud,
                items.codigo_empleado,
                items.nombre_empleado,
                items.nombre_cargo,
                items.fecha_evento,
                items.resumen
            FROM
            (
                SELECT
                    N'VACACION' AS tipo,
                    N'Vacacion' AS tipo_label,
                    v.id_vacacion AS id_solicitud,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    c.nombre_cargo,
                    COALESCE(v.fecha_registro, v.fecha_solicitud) AS fecha_evento,
                    CONCAT(
                        FORMAT(v.dias_solicitados, '0.##'),
                        N' dia(s) del ',
                        CONVERT(NVARCHAR(10), v.fecha_inicio, 103),
                        N' al ',
                        CONVERT(NVARCHAR(10), v.fecha_fin, 103)
                    ) AS resumen
                FROM rrhh.vacacion v
                INNER JOIN rrhh.empleado_supervision rel
                    ON rel.id_empleado = v.id_empleado
                   AND rel.id_supervisor_empleado = @id_supervisor_empleado
                   AND rel.activo = 1
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = v.id_empleado
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                WHERE v.estado_vacacion = N'SOLICITADA'

                UNION ALL

                SELECT
                    N'HORA_EXTRA' AS tipo,
                    N'Hora extra' AS tipo_label,
                    h.id_hora_extra AS id_solicitud,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    c.nombre_cargo,
                    COALESCE(h.fecha_registro, CAST(h.fecha_hora_extra AS DATETIME2)) AS fecha_evento,
                    CONCAT(
                        FORMAT(h.cantidad_horas, '0.##'),
                        N' hora(s) - ',
                        th.nombre_tipo_hora_extra
                    ) AS resumen
                FROM rrhh.hora_extra h
                INNER JOIN rrhh.empleado_supervision rel
                    ON rel.id_empleado = h.id_empleado
                   AND rel.id_supervisor_empleado = @id_supervisor_empleado
                   AND rel.activo = 1
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = h.id_empleado
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                INNER JOIN rrhh.tipo_hora_extra th
                    ON th.id_tipo_hora_extra = h.id_tipo_hora_extra
                WHERE h.estado_hora_extra = N'REGISTRADA'
            ) AS items
            ORDER BY items.fecha_evento DESC, items.tipo, items.codigo_empleado;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;
        using var reader = command.ExecuteReader();

        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                type = reader.GetString(0),
                typeLabel = reader.GetString(1),
                requestId = reader.GetInt64(2),
                employeeCode = reader.GetString(3),
                employeeName = reader.GetString(4),
                positionName = reader.GetString(5),
                requestDate = reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm:ss"),
                summary = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            });
        }

        return items;
    }

    private static string BuildSupervisorScopeNote(bool hasEmployeeLink, int subordinateCount)
    {
        if (!hasEmployeeLink)
        {
            return "Tu usuario todavia no esta enlazado a una ficha de empleado, por eso no se puede calcular una bandeja propia.";
        }

        if (subordinateCount <= 0)
        {
            return "Aun no tienes subordinados directos asignados como jefe inmediato.";
        }

        return $"Solo ves solicitudes de {subordinateCount} subordinado(s) directos bajo tu jefatura inmediata.";
    }

    private static bool EsSubordinadoDirecto(
        SqlConnection connection,
        SqlTransaction transaction,
        long idSupervisorEmpleado,
        long idEmpleado)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM rrhh.empleado_supervision
            WHERE id_supervisor_empleado = @id_supervisor_empleado
              AND id_empleado = @id_empleado
              AND activo = 1;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static List<object> LoadSupervisorVacations(SqlConnection connection, long idSupervisorEmpleado)
    {
        const string sql = """
            SELECT
                v.id_vacacion,
                v.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                v.fecha_solicitud,
                v.fecha_inicio,
                v.fecha_fin,
                v.dias_solicitados,
                v.dias_aprobados,
                v.estado_vacacion,
                v.observacion_solicitud,
                v.observacion_aprobacion,
                v.usuario_solicita,
                v.usuario_aprueba,
                v.fecha_aprobacion,
                v.pagada_en_nomina,
                v.fecha_registro
            FROM rrhh.vacacion v
            INNER JOIN rrhh.empleado_supervision rel
                ON rel.id_empleado = v.id_empleado
               AND rel.id_supervisor_empleado = @id_supervisor_empleado
               AND rel.activo = 1
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = v.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            WHERE v.estado_vacacion = N'SOLICITADA'
            ORDER BY v.fecha_solicitud DESC, v.id_vacacion DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;
        using var reader = command.ExecuteReader();

        var items = new List<PortalVacationRow>();
        while (reader.Read())
        {
            items.Add(new PortalVacationRow
            {
                IdVacacion = reader.GetInt64(0),
                IdEmpleado = reader.GetInt64(1),
                CodigoEmpleado = reader.GetString(2),
                NombreEmpleado = reader.GetString(3),
                NombreDepartamento = reader.GetString(4),
                NombreCargo = reader.GetString(5),
                FechaSolicitud = reader.GetDateTime(6).ToString("yyyy-MM-dd"),
                FechaInicio = reader.GetDateTime(7).ToString("yyyy-MM-dd"),
                FechaFin = reader.GetDateTime(8).ToString("yyyy-MM-dd"),
                DiasSolicitados = reader.GetDecimal(9),
                DiasAprobados = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                EstadoVacacion = reader.GetString(11),
                ObservacionSolicitud = reader.IsDBNull(12) ? null : reader.GetString(12),
                ObservacionAprobacion = reader.IsDBNull(13) ? null : reader.GetString(13),
                UsuarioSolicita = reader.GetString(14),
                UsuarioAprueba = reader.IsDBNull(15) ? null : reader.GetString(15),
                FechaAprobacion = reader.IsDBNull(16) ? null : reader.GetDateTime(16).ToString("yyyy-MM-dd HH:mm:ss"),
                PagadaEnNomina = !reader.IsDBNull(17) && reader.GetBoolean(17),
                FechaRegistro = reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }

        reader.Close();

        foreach (var item in items)
        {
            var snapshot = RrhhSupport.CalculateVacationBalance(
                connection,
                null,
                item.IdEmpleado,
                DateTime.Parse(item.FechaFin));
            item.DiasVacacionesDisponibles = snapshot.DiasDisponibles;
        }

        return items.Cast<object>().ToList();
    }

    private static List<object> LoadSupervisorPermissions(SqlConnection connection, long idSupervisorEmpleado)
    {
        const string sql = """
            SELECT
                p.id_solicitud_permiso,
                p.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                p.id_tipo_permiso,
                tp.codigo_tipo_permiso,
                tp.nombre_tipo_permiso,
                tp.afecta_salario,
                p.fecha_solicitud,
                p.fecha_inicio,
                p.fecha_fin,
                p.cantidad_dias,
                p.estado_permiso,
                p.observacion,
                p.usuario_solicita,
                p.usuario_aprueba,
                p.fecha_aprobacion,
                p.fecha_registro
            FROM rrhh.solicitud_permiso p
            INNER JOIN rrhh.empleado_supervision rel
                ON rel.id_empleado = p.id_empleado
               AND rel.id_supervisor_empleado = @id_supervisor_empleado
               AND rel.activo = 1
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = p.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.tipo_permiso tp
                ON tp.id_tipo_permiso = p.id_tipo_permiso
            WHERE p.estado_permiso = N'SOLICITADO'
            ORDER BY p.fecha_solicitud DESC, p.id_solicitud_permiso DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;
        using var reader = command.ExecuteReader();

        var items = new List<PortalPermissionRow>();
        while (reader.Read())
        {
            var rawObservation = reader.IsDBNull(15) ? null : reader.GetString(15);
            var envelope = ParsePermissionObservation(rawObservation);

            items.Add(new PortalPermissionRow
            {
                IdSolicitudPermiso = reader.GetInt64(0),
                IdEmpleado = reader.GetInt64(1),
                CodigoEmpleado = reader.GetString(2),
                NombreEmpleado = reader.GetString(3),
                NombreDepartamento = reader.GetString(4),
                NombreCargo = reader.GetString(5),
                IdTipoPermiso = reader.GetInt64(6),
                CodigoTipoPermiso = reader.GetString(7),
                NombreTipoPermiso = reader.GetString(8),
                AfectaSalario = reader.GetBoolean(9),
                FechaSolicitud = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
                FechaInicio = reader.GetDateTime(11).ToString("yyyy-MM-dd"),
                FechaFin = reader.GetDateTime(12).ToString("yyyy-MM-dd"),
                CantidadDias = reader.GetDecimal(13),
                EstadoPermiso = reader.GetString(14),
                Observacion = envelope.TextoSolicitud,
                ObservacionResolucion = envelope.TextoResolucion,
                EsMedioDia = envelope.EsMedioDia,
                JornadaMedioDia = envelope.JornadaMedioDia,
                UsuarioSolicita = reader.GetString(16),
                UsuarioAprueba = reader.IsDBNull(17) ? null : reader.GetString(17),
                FechaAprobacion = reader.IsDBNull(18) ? null : reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
                FechaRegistro = reader.GetDateTime(19).ToString("yyyy-MM-dd HH:mm:ss"),
                ObservacionRaw = rawObservation,
            });
        }

        return items.Cast<object>().ToList();
    }

    private static List<object> LoadSupervisorOvertime(SqlConnection connection, long idSupervisorEmpleado)
    {
        const string sql = """
            SELECT
                h.id_hora_extra,
                h.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                h.id_tipo_hora_extra,
                th.codigo_tipo_hora_extra,
                th.nombre_tipo_hora_extra,
                th.factor_pago,
                h.fecha_hora_extra,
                h.cantidad_horas,
                h.estado_hora_extra,
                h.observacion,
                h.usuario_registra,
                h.usuario_aprueba,
                h.fecha_aprobacion,
                h.pagada_en_nomina,
                h.fecha_registro
            FROM rrhh.hora_extra h
            INNER JOIN rrhh.empleado_supervision rel
                ON rel.id_empleado = h.id_empleado
               AND rel.id_supervisor_empleado = @id_supervisor_empleado
               AND rel.activo = 1
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = h.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.tipo_hora_extra th
                ON th.id_tipo_hora_extra = h.id_tipo_hora_extra
            WHERE h.estado_hora_extra = N'REGISTRADA'
            ORDER BY h.fecha_registro DESC, h.id_hora_extra DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_supervisor_empleado", SqlDbType.BigInt).Value = idSupervisorEmpleado;
        using var reader = command.ExecuteReader();

        var items = new List<PortalOvertimeRow>();
        while (reader.Read())
        {
            items.Add(new PortalOvertimeRow
            {
                IdHoraExtra = reader.GetInt64(0),
                IdEmpleado = reader.GetInt64(1),
                CodigoEmpleado = reader.GetString(2),
                NombreEmpleado = reader.GetString(3),
                NombreDepartamento = reader.GetString(4),
                NombreCargo = reader.GetString(5),
                IdTipoHoraExtra = reader.GetInt64(6),
                CodigoTipoHoraExtra = reader.GetString(7),
                NombreTipoHoraExtra = reader.GetString(8),
                FactorPago = reader.GetDecimal(9),
                FechaHoraExtra = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
                CantidadHoras = reader.GetDecimal(11),
                EstadoHoraExtra = reader.GetString(12),
                Observacion = reader.IsDBNull(13) ? null : reader.GetString(13),
                UsuarioRegistra = reader.GetString(14),
                UsuarioAprueba = reader.IsDBNull(15) ? null : reader.GetString(15),
                FechaAprobacion = reader.IsDBNull(16) ? null : reader.GetDateTime(16).ToString("yyyy-MM-dd HH:mm:ss"),
                PagadaEnNomina = !reader.IsDBNull(17) && reader.GetBoolean(17),
                FechaRegistro = reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }

        return items.Cast<object>().ToList();
    }

    private static PortalVacationRow? ObtenerVacacionInterna(SqlConnection connection, SqlTransaction transaction, long id)
    {
        const string sql = """
            SELECT TOP (1)
                v.id_vacacion,
                v.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                v.fecha_solicitud,
                v.fecha_inicio,
                v.fecha_fin,
                v.dias_solicitados,
                v.dias_aprobados,
                v.estado_vacacion,
                v.observacion_solicitud,
                v.observacion_aprobacion,
                v.usuario_solicita,
                v.usuario_aprueba,
                v.fecha_aprobacion,
                v.pagada_en_nomina,
                v.fecha_registro
            FROM rrhh.vacacion v
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = v.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            WHERE v.id_vacacion = @id_vacacion;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_vacacion", SqlDbType.BigInt).Value = id;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PortalVacationRow
        {
            IdVacacion = reader.GetInt64(0),
            IdEmpleado = reader.GetInt64(1),
            CodigoEmpleado = reader.GetString(2),
            NombreEmpleado = reader.GetString(3),
            NombreDepartamento = reader.GetString(4),
            NombreCargo = reader.GetString(5),
            FechaSolicitud = reader.GetDateTime(6).ToString("yyyy-MM-dd"),
            FechaInicio = reader.GetDateTime(7).ToString("yyyy-MM-dd"),
            FechaFin = reader.GetDateTime(8).ToString("yyyy-MM-dd"),
            DiasSolicitados = reader.GetDecimal(9),
            DiasAprobados = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
            EstadoVacacion = reader.GetString(11),
            ObservacionSolicitud = reader.IsDBNull(12) ? null : reader.GetString(12),
            ObservacionAprobacion = reader.IsDBNull(13) ? null : reader.GetString(13),
            UsuarioSolicita = reader.GetString(14),
            UsuarioAprueba = reader.IsDBNull(15) ? null : reader.GetString(15),
            FechaAprobacion = reader.IsDBNull(16) ? null : reader.GetDateTime(16).ToString("yyyy-MM-dd HH:mm:ss"),
            PagadaEnNomina = !reader.IsDBNull(17) && reader.GetBoolean(17),
            FechaRegistro = reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
        };
    }

    private static PortalPermissionRow? ObtenerPermisoInterno(SqlConnection connection, SqlTransaction transaction, long id)
    {
        const string sql = """
            SELECT TOP (1)
                p.id_solicitud_permiso,
                p.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                p.id_tipo_permiso,
                tp.codigo_tipo_permiso,
                tp.nombre_tipo_permiso,
                tp.afecta_salario,
                p.fecha_solicitud,
                p.fecha_inicio,
                p.fecha_fin,
                p.cantidad_dias,
                p.estado_permiso,
                p.observacion,
                p.usuario_solicita,
                p.usuario_aprueba,
                p.fecha_aprobacion,
                p.fecha_registro
            FROM rrhh.solicitud_permiso p
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = p.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.tipo_permiso tp
                ON tp.id_tipo_permiso = p.id_tipo_permiso
            WHERE p.id_solicitud_permiso = @id_solicitud_permiso;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_solicitud_permiso", SqlDbType.BigInt).Value = id;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var rawObservation = reader.IsDBNull(15) ? null : reader.GetString(15);
        var envelope = ParsePermissionObservation(rawObservation);

        return new PortalPermissionRow
        {
            IdSolicitudPermiso = reader.GetInt64(0),
            IdEmpleado = reader.GetInt64(1),
            CodigoEmpleado = reader.GetString(2),
            NombreEmpleado = reader.GetString(3),
            NombreDepartamento = reader.GetString(4),
            NombreCargo = reader.GetString(5),
            IdTipoPermiso = reader.GetInt64(6),
            CodigoTipoPermiso = reader.GetString(7),
            NombreTipoPermiso = reader.GetString(8),
            AfectaSalario = reader.GetBoolean(9),
            FechaSolicitud = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
            FechaInicio = reader.GetDateTime(11).ToString("yyyy-MM-dd"),
            FechaFin = reader.GetDateTime(12).ToString("yyyy-MM-dd"),
            CantidadDias = reader.GetDecimal(13),
            EstadoPermiso = reader.GetString(14),
            Observacion = envelope.TextoSolicitud,
            ObservacionResolucion = envelope.TextoResolucion,
            EsMedioDia = envelope.EsMedioDia,
            JornadaMedioDia = envelope.JornadaMedioDia,
            UsuarioSolicita = reader.GetString(16),
            UsuarioAprueba = reader.IsDBNull(17) ? null : reader.GetString(17),
            FechaAprobacion = reader.IsDBNull(18) ? null : reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
            FechaRegistro = reader.GetDateTime(19).ToString("yyyy-MM-dd HH:mm:ss"),
            ObservacionRaw = rawObservation,
        };
    }

    private static PortalOvertimeRow? ObtenerHoraExtraInterna(SqlConnection connection, SqlTransaction transaction, long id)
    {
        const string sql = """
            SELECT TOP (1)
                h.id_hora_extra,
                h.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo,
                h.id_tipo_hora_extra,
                th.codigo_tipo_hora_extra,
                th.nombre_tipo_hora_extra,
                th.factor_pago,
                h.fecha_hora_extra,
                h.cantidad_horas,
                h.estado_hora_extra,
                h.observacion,
                h.usuario_registra,
                h.usuario_aprueba,
                h.fecha_aprobacion,
                h.pagada_en_nomina,
                h.fecha_registro
            FROM rrhh.hora_extra h
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = h.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.tipo_hora_extra th
                ON th.id_tipo_hora_extra = h.id_tipo_hora_extra
            WHERE h.id_hora_extra = @id_hora_extra;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_hora_extra", SqlDbType.BigInt).Value = id;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PortalOvertimeRow
        {
            IdHoraExtra = reader.GetInt64(0),
            IdEmpleado = reader.GetInt64(1),
            CodigoEmpleado = reader.GetString(2),
            NombreEmpleado = reader.GetString(3),
            NombreDepartamento = reader.GetString(4),
            NombreCargo = reader.GetString(5),
            IdTipoHoraExtra = reader.GetInt64(6),
            CodigoTipoHoraExtra = reader.GetString(7),
            NombreTipoHoraExtra = reader.GetString(8),
            FactorPago = reader.GetDecimal(9),
            FechaHoraExtra = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
            CantidadHoras = reader.GetDecimal(11),
            EstadoHoraExtra = reader.GetString(12),
            Observacion = reader.IsDBNull(13) ? null : reader.GetString(13),
            UsuarioRegistra = reader.GetString(14),
            UsuarioAprueba = reader.IsDBNull(15) ? null : reader.GetString(15),
            FechaAprobacion = reader.IsDBNull(16) ? null : reader.GetDateTime(16).ToString("yyyy-MM-dd HH:mm:ss"),
            PagadaEnNomina = !reader.IsDBNull(17) && reader.GetBoolean(17),
            FechaRegistro = reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
        };
    }

    private static string? NormalizeResolutionAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        return action.Trim().ToUpperInvariant() switch
        {
            "APROBAR" => "APROBAR",
            "RECHAZAR" => "RECHAZAR",
            _ => null,
        };
    }

    private static string? NormalizeHalfDayShiftDuplicate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "MANANA" => "MANANA",
            "MAÑANA" => "MANANA",
            "MAÃ‘ANA" => "MANANA",
            "TARDE" => "TARDE",
            _ => null,
        };
    }

    private static PermissionObservationEnvelope ParsePermissionObservation(string? rawObservation)
    {
        if (string.IsNullOrWhiteSpace(rawObservation))
        {
            return new PermissionObservationEnvelope();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PermissionObservationEnvelope>(rawObservation);
            if (parsed is not null && parsed.IsEnvelope)
            {
                parsed.TextoSolicitud = string.IsNullOrWhiteSpace(parsed.TextoSolicitud)
                    ? null
                    : parsed.TextoSolicitud.Trim();
                parsed.TextoResolucion = string.IsNullOrWhiteSpace(parsed.TextoResolucion)
                    ? null
                    : parsed.TextoResolucion.Trim();
                parsed.JornadaMedioDia = NormalizeHalfDayShift(parsed.JornadaMedioDia);
                return parsed;
            }
        }
        catch
        {
            // Se conserva compatibilidad con texto plano.
        }

        return new PermissionObservationEnvelope
        {
            TextoSolicitud = rawObservation.Trim(),
        };
    }

    private static string? BuildPermissionObservationPayload(
        string? requestObservation,
        bool isHalfDay,
        string? halfDayShift,
        string? resolutionObservation)
    {
        var requestText = string.IsNullOrWhiteSpace(requestObservation)
            ? null
            : requestObservation.Trim();
        var resolutionText = string.IsNullOrWhiteSpace(resolutionObservation)
            ? null
            : resolutionObservation.Trim();
        var normalizedShift = NormalizeHalfDayShift(halfDayShift);

        if (!isHalfDay && normalizedShift is null && resolutionText is null)
        {
            return requestText;
        }

        var envelope = new PermissionObservationEnvelope
        {
            TextoSolicitud = requestText,
            TextoResolucion = resolutionText,
            EsMedioDia = isHalfDay,
            JornadaMedioDia = normalizedShift,
        };

        var payload = JsonSerializer.Serialize(envelope);
        return string.IsNullOrWhiteSpace(payload) ? null : payload;
    }

    private static List<object> LoadPortalPayslips(SqlConnection connection, long idEmpleado)
    {
        const string sql = """
            ;WITH ultima_esquela AS
            (
                SELECT
                    ep.id_esquela_pago,
                    ep.id_nomina_detalle,
                    ep.fecha_generacion,
                    ep.fecha_publicacion,
                    ep.nombre_archivo,
                    ROW_NUMBER() OVER (PARTITION BY ep.id_nomina_detalle ORDER BY ep.id_esquela_pago DESC) AS rn
                FROM nomina.esquela_pago ep
                INNER JOIN nomina.nomina_detalle nd
                    ON nd.id_nomina_detalle = ep.id_nomina_detalle
                WHERE nd.id_empleado = @id_empleado
            )
            SELECT
                ue.id_esquela_pago,
                ue.id_nomina_detalle,
                nd.id_nomina,
                p.codigo_periodo,
                p.fecha_desde,
                p.fecha_hasta,
                p.fecha_pago,
                nd.total_ingresos,
                nd.total_deducciones,
                nd.neto_pagar,
                nd.inss_laboral,
                nd.ir_laboral,
                ue.fecha_generacion,
                ue.fecha_publicacion,
                ue.nombre_archivo
            FROM ultima_esquela ue
            INNER JOIN nomina.nomina_detalle nd
                ON nd.id_nomina_detalle = ue.id_nomina_detalle
            INNER JOIN nomina.nomina n
                ON n.id_nomina = nd.id_nomina
            INNER JOIN nomina.periodo_nomina p
                ON p.id_periodo_nomina = n.id_periodo_nomina
            WHERE ue.rn = 1
            ORDER BY p.fecha_pago DESC, ue.id_esquela_pago DESC;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        using var reader = command.ExecuteReader();

        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                idEsquelaPago = reader.GetInt64(0),
                idNominaDetalle = reader.GetInt64(1),
                idNomina = reader.GetInt64(2),
                codigoPeriodo = reader.GetString(3),
                fechaDesde = reader.GetDateTime(4).ToString("yyyy-MM-dd"),
                fechaHasta = reader.GetDateTime(5).ToString("yyyy-MM-dd"),
                fechaPago = reader.GetDateTime(6).ToString("yyyy-MM-dd"),
                totalIngresos = reader.GetDecimal(7),
                totalDeducciones = reader.GetDecimal(8),
                netoPagar = reader.GetDecimal(9),
                inssLaboral = reader.GetDecimal(10),
                irRetenido = reader.GetDecimal(11),
                fechaGeneracion = reader.GetDateTime(12).ToString("yyyy-MM-ddTHH:mm:ss"),
                fechaPublicacion = reader.IsDBNull(13)
                    ? null
                    : reader.GetDateTime(13).ToString("yyyy-MM-ddTHH:mm:ss"),
                nombreArchivo = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
            });
        }

        return items;
    }

    private static Dictionary<string, string> ValidatePortalVacationEdit(PortalVacationEditModel model)
    {
        var errors = new Dictionary<string, string>();
        var minDate = new DateTime(1753, 1, 1);

        if (!DateTime.TryParse(model.FechaInicio, out var startDate))
        {
            errors["fechaInicio"] = "Ingresa la fecha de inicio.";
        }
        else if (startDate.Date < minDate)
        {
            errors["fechaInicio"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }

        if (!DateTime.TryParse(model.FechaFin, out var endDate))
        {
            errors["fechaFin"] = "Ingresa la fecha fin.";
        }
        else if (endDate.Date < minDate)
        {
            errors["fechaFin"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }
        else if (DateTime.TryParse(model.FechaInicio, out startDate) && endDate.Date < startDate.Date)
        {
            errors["fechaFin"] = "La fecha fin debe ser igual o mayor a la fecha de inicio.";
        }

        if (model.EsMedioDia)
        {
            if (DateTime.TryParse(model.FechaInicio, out var halfStart) &&
                DateTime.TryParse(model.FechaFin, out var halfEnd) &&
                halfStart.Date != halfEnd.Date)
            {
                errors["fechaFin"] = "Si la vacacion es de medio dia, la fecha fin debe ser igual a la fecha de inicio.";
            }

            if (NormalizeHalfDayShift(model.JornadaMedioDia) is null)
            {
                errors["jornadaMedioDia"] = "Selecciona si corresponde a manana o tarde.";
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ObservacionSolicitud) &&
            model.ObservacionSolicitud.Trim().Length > 500)
        {
            errors["observacionSolicitud"] = "La observacion supera el limite permitido.";
        }

        if (DateTime.TryParse(model.FechaInicio, out startDate) &&
            DateTime.TryParse(model.FechaFin, out endDate) &&
            GetRequestedVacationDays(model) <= 0)
        {
            errors["fechaFin"] = "La cantidad de dias de vacaciones no es valida.";
        }

        return errors;
    }

    private static Dictionary<string, string> ValidatePortalVacationBusinessRules(
        SqlConnection connection,
        SqlTransaction transaction,
        long employeeId,
        PortalVacationEditModel model,
        long currentId)
    {
        var errors = new Dictionary<string, string>();

        if (DateTime.TryParse(model.FechaInicio, out var startDate) &&
            DateTime.TryParse(model.FechaFin, out var endDate) &&
            ExistsVacationOverlap(connection, transaction, employeeId, startDate, endDate, currentId))
        {
            errors["fechaInicio"] = "Ya tienes otra vacacion en ese rango de fechas.";
        }

        if (errors.Count == 0 && DateTime.TryParse(model.FechaFin, out var cutoff))
        {
            var snapshot = RrhhSupport.CalculateVacationBalance(connection, transaction, employeeId, cutoff);
            var requestedDays = GetRequestedVacationDays(model);

            if (snapshot.DiasDisponibles < requestedDays)
            {
                errors["fechaFin"] = RrhhSupport.BuildVacationAvailabilityMessage(snapshot);
            }
        }

        return errors;
    }

    private static Dictionary<string, string> ValidatePortalOvertimeEdit(PortalOvertimeEditModel model)
    {
        var errors = new Dictionary<string, string>();
        var today = DateTime.Today;
        var minDate = new DateTime(1753, 1, 1);

        if (model.IdTipoHoraExtra <= 0)
        {
            errors["idTipoHoraExtra"] = "Selecciona el tipo de hora extra.";
        }

        if (!DateTime.TryParse(model.FechaHoraExtra, out var date))
        {
            errors["fechaHoraExtra"] = "Ingresa la fecha de la hora extra.";
        }
        else if (date.Date < minDate)
        {
            errors["fechaHoraExtra"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }
        else if (date.Date > today)
        {
            errors["fechaHoraExtra"] = "La fecha de la hora extra no puede ser futura.";
        }

        if (model.CantidadHoras <= 0 || model.CantidadHoras > 16)
        {
            errors["cantidadHoras"] = "Ingresa una cantidad de horas valida.";
        }

        if (!string.IsNullOrWhiteSpace(model.Observacion) && model.Observacion.Trim().Length > 500)
        {
            errors["observacion"] = "La observacion supera el limite permitido.";
        }

        return errors;
    }

    private static Dictionary<string, string> ValidatePortalOvertimeBusinessRules(
        SqlConnection connection,
        SqlTransaction transaction,
        long employeeId,
        PortalOvertimeEditModel model,
        long currentId)
    {
        var errors = new Dictionary<string, string>();

        if (!CatalogExists(connection, transaction, "rrhh.tipo_hora_extra", "id_tipo_hora_extra", model.IdTipoHoraExtra))
        {
            errors["idTipoHoraExtra"] = "El tipo de hora extra seleccionado no existe.";
            return errors;
        }

        if (DateTime.TryParse(model.FechaHoraExtra, out var date) &&
            ExistsOvertimeDuplicate(connection, transaction, employeeId, model.IdTipoHoraExtra, date, currentId))
        {
            errors["fechaHoraExtra"] = "Ya existe una hora extra similar para esa fecha.";
        }

        return errors;
    }

    private static void AssignPortalVacationParameters(SqlCommand command, PortalVacationEditModel model)
    {
        var rawObservation = BuildVacationObservationPayload(
            model.ObservacionSolicitud,
            model.EsMedioDia,
            model.JornadaMedioDia);

        command.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = DateTime.Parse(model.FechaInicio!);
        command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = DateTime.Parse(model.FechaFin!);
        command.Parameters.Add("@dias_solicitados", SqlDbType.Decimal).Value = GetRequestedVacationDays(model);
        command.Parameters["@dias_solicitados"].Precision = 10;
        command.Parameters["@dias_solicitados"].Scale = 2;
        command.Parameters.Add("@observacion_solicitud", SqlDbType.NVarChar, 500).Value = RrhhSupport.ToDbValue(rawObservation);
    }

    private static decimal GetRequestedVacationDays(PortalVacationEditModel model)
    {
        if (model.EsMedioDia)
        {
            return 0.5m;
        }

        return CalculateDaysInclusive(DateTime.Parse(model.FechaInicio!), DateTime.Parse(model.FechaFin!));
    }

    private static decimal CalculateDaysInclusive(DateTime start, DateTime end) =>
        Convert.ToDecimal((end.Date - start.Date).TotalDays + 1d);

    private static string? NormalizeHalfDayShift(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        return text switch
        {
            "MANANA" => "MANANA",
            "MAÑANA" => "MANANA",
            "TARDE" => "TARDE",
            _ => null,
        };
    }

    private static string? BuildVacationObservationPayload(
        string? requestObservation,
        bool isHalfDay,
        string? halfDayShift)
    {
        var requestText = string.IsNullOrWhiteSpace(requestObservation)
            ? null
            : requestObservation.Trim();
        var normalizedShift = NormalizeHalfDayShift(halfDayShift);

        if (!isHalfDay && normalizedShift is null)
        {
            return requestText;
        }

        var envelope = new
        {
            TextoSolicitud = requestText,
            EsMedioDia = isHalfDay,
            JornadaMedioDia = normalizedShift,
        };

        var payload = JsonSerializer.Serialize(envelope);
        return string.IsNullOrWhiteSpace(payload) ? null : payload;
    }

    private static bool ExistsVacationOverlap(
        SqlConnection connection,
        SqlTransaction transaction,
        long employeeId,
        DateTime startDate,
        DateTime endDate,
        long currentId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1) 1
            FROM rrhh.vacacion
            WHERE id_empleado = @id_empleado
              AND estado_vacacion <> N'RECHAZADA'
              AND id_vacacion <> @id_actual
              AND fecha_inicio <= @fecha_fin
              AND fecha_fin >= @fecha_inicio;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = employeeId;
        command.Parameters.Add("@id_actual", SqlDbType.BigInt).Value = currentId;
        command.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = startDate.Date;
        command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = endDate.Date;
        return command.ExecuteScalar() is not null;
    }

    private static bool ExistsOvertimeDuplicate(
        SqlConnection connection,
        SqlTransaction transaction,
        long employeeId,
        long overtimeTypeId,
        DateTime date,
        long currentId)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1) 1
            FROM rrhh.hora_extra
            WHERE id_empleado = @id_empleado
              AND id_tipo_hora_extra = @id_tipo_hora_extra
              AND id_hora_extra <> @id_actual
              AND fecha_hora_extra = @fecha_hora_extra;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = employeeId;
        command.Parameters.Add("@id_tipo_hora_extra", SqlDbType.BigInt).Value = overtimeTypeId;
        command.Parameters.Add("@id_actual", SqlDbType.BigInt).Value = currentId;
        command.Parameters.Add("@fecha_hora_extra", SqlDbType.Date).Value = date.Date;
        return command.ExecuteScalar() is not null;
    }

    private static bool CatalogExists(
        SqlConnection connection,
        SqlTransaction transaction,
        string tableName,
        string keyColumn,
        long value)
    {
        var sql = $"""
            SELECT TOP (1) 1
            FROM {tableName}
            WHERE {keyColumn} = @value
              AND activo = 1;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@value", SqlDbType.BigInt).Value = value;
        return command.ExecuteScalar() is not null;
    }

    private sealed class SessionContext
    {
        public long IdSesionUsuario { get; set; }
        public long IdUsuario { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> Roles { get; } = [];
    }

    private sealed class SupervisorCounts
    {
        public int PendingVacations { get; set; }
        public int PendingPermissions { get; set; }
        public int PendingOvertime { get; set; }
    }

    private sealed class SupervisorAccessScope
    {
        public RrhhSupport.EmployeeLink? Employee { get; set; }
        public int DirectReportCount { get; set; }
        public bool HasAccess { get; set; }
    }

    public class PortalResolutionModel
    {
        public string? Action { get; set; }
        public string? Observation { get; set; }
    }

    public sealed class PortalVacationResolutionModel : PortalResolutionModel
    {
        public decimal ApprovedDays { get; set; }
    }

    public sealed class PortalVacationEditModel
    {
        public string? FechaInicio { get; set; }
        public string? FechaFin { get; set; }
        public string? ObservacionSolicitud { get; set; }
        public bool EsMedioDia { get; set; }
        public string? JornadaMedioDia { get; set; }
    }

    public sealed class PortalOvertimeEditModel
    {
        public long IdTipoHoraExtra { get; set; }
        public string? FechaHoraExtra { get; set; }
        public decimal CantidadHoras { get; set; }
        public string? Observacion { get; set; }
    }

    private sealed class PermissionObservationEnvelope
    {
        public string? TextoSolicitud { get; set; }
        public string? TextoResolucion { get; set; }
        public bool EsMedioDia { get; set; }
        public string? JornadaMedioDia { get; set; }

        public bool IsEnvelope =>
            TextoSolicitud is not null ||
            TextoResolucion is not null ||
            EsMedioDia ||
            !string.IsNullOrWhiteSpace(JornadaMedioDia);
    }

    private sealed class PortalVacationRow
    {
        public long IdVacacion { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string FechaSolicitud { get; set; } = string.Empty;
        public string FechaInicio { get; set; } = string.Empty;
        public string FechaFin { get; set; } = string.Empty;
        public decimal DiasSolicitados { get; set; }
        public decimal? DiasAprobados { get; set; }
        public string EstadoVacacion { get; set; } = string.Empty;
        public string? ObservacionSolicitud { get; set; }
        public string? ObservacionAprobacion { get; set; }
        public string UsuarioSolicita { get; set; } = string.Empty;
        public string? UsuarioAprueba { get; set; }
        public string? FechaAprobacion { get; set; }
        public bool PagadaEnNomina { get; set; }
        public string FechaRegistro { get; set; } = string.Empty;
        public decimal DiasVacacionesDisponibles { get; set; }
    }

    private sealed class PortalPermissionRow
    {
        public long IdSolicitudPermiso { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public long IdTipoPermiso { get; set; }
        public string CodigoTipoPermiso { get; set; } = string.Empty;
        public string NombreTipoPermiso { get; set; } = string.Empty;
        public bool AfectaSalario { get; set; }
        public string FechaSolicitud { get; set; } = string.Empty;
        public string FechaInicio { get; set; } = string.Empty;
        public string FechaFin { get; set; } = string.Empty;
        public decimal CantidadDias { get; set; }
        public string EstadoPermiso { get; set; } = string.Empty;
        public string? Observacion { get; set; }
        public string? ObservacionResolucion { get; set; }
        public bool EsMedioDia { get; set; }
        public string? JornadaMedioDia { get; set; }
        public string UsuarioSolicita { get; set; } = string.Empty;
        public string? UsuarioAprueba { get; set; }
        public string? FechaAprobacion { get; set; }
        public string FechaRegistro { get; set; } = string.Empty;
        public string? ObservacionRaw { get; set; }
    }

    private sealed class PortalOvertimeRow
    {
        public long IdHoraExtra { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public long IdTipoHoraExtra { get; set; }
        public string CodigoTipoHoraExtra { get; set; } = string.Empty;
        public string NombreTipoHoraExtra { get; set; } = string.Empty;
        public decimal FactorPago { get; set; }
        public string FechaHoraExtra { get; set; } = string.Empty;
        public decimal CantidadHoras { get; set; }
        public string EstadoHoraExtra { get; set; } = string.Empty;
        public string? Observacion { get; set; }
        public string UsuarioRegistra { get; set; } = string.Empty;
        public string? UsuarioAprueba { get; set; }
        public string? FechaAprobacion { get; set; }
        public bool PagadaEnNomina { get; set; }
        public string FechaRegistro { get; set; } = string.Empty;
    }
}
