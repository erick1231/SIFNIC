using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class NovedadesController : Controller
{
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
                  AND ISNULL(ee.codigo_estado_empleado, N'') <> N'RETIRADO'
                  AND e.fecha_baja IS NULL
                ORDER BY nombre_empleado;

                SELECT
                    id_tipo_permiso,
                    codigo_tipo_permiso,
                    nombre_tipo_permiso,
                    afecta_salario
                FROM rrhh.tipo_permiso
                WHERE activo = 1
                ORDER BY nombre_tipo_permiso;

                SELECT
                    id_tipo_hora_extra,
                    codigo_tipo_hora_extra,
                    nombre_tipo_hora_extra,
                    factor_pago
                FROM rrhh.tipo_hora_extra
                WHERE activo = 1
                ORDER BY nombre_tipo_hora_extra;
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

            reader.NextResult();

            var permissionTypes = new List<object>();
            while (reader.Read())
            {
                permissionTypes.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                    affectsSalary = reader.GetBoolean(3),
                });
            }

            reader.NextResult();

            var overtimeTypes = new List<object>();
            while (reader.Read())
            {
                overtimeTypes.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                    factor = reader.GetDecimal(3),
                });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    employees,
                    permissionTypes,
                    overtimeTypes,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos de novedades.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult ObtenerSaldoVacaciones(long idEmpleado, string? fechaCorte)
    {
        if (idEmpleado <= 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Selecciona el empleado.",
            });
        }

        var cutoff = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(fechaCorte) && !DateTime.TryParse(fechaCorte, out cutoff))
        {
            return BadRequest(new
            {
                ok = false,
                message = "La fecha de corte es invalida.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var snapshot = RrhhSupport.CalculateVacationBalance(connection, null, idEmpleado, cutoff);
            if (!snapshot.FechaIngreso.HasValue)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado.",
                });
            }

            return Json(new
            {
                ok = true,
                data = BuildVacationBalanceDto(snapshot),
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo calcular el saldo de vacaciones.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult ReporteVacacionesDisponibles(string? search, long? idDepartamento, string? status, string? fechaCorte)
    {
        var cutoff = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(fechaCorte) && !DateTime.TryParse(fechaCorte, out cutoff))
        {
            return BadRequest(new
            {
                ok = false,
                message = "La fecha de corte es invalida.",
            });
        }

        var normalizedStatus = NormalizeVacationReportEmployeeStatus(status);

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var baseRows = LoadVacationAvailabilityBaseRows(
                connection,
                search,
                idDepartamento,
                normalizedStatus,
                cutoff.Date);

            var rows = baseRows
                .Select(row =>
                {
                    var snapshot = RrhhSupport.CalculateVacationBalance(connection, null, row.IdEmpleado, cutoff.Date);
                    var consumedDays = snapshot.DiasTomadosVacacion + snapshot.DiasDescontadosPermiso;
                    var pendingDays = snapshot.DiasPendientesVacacion + snapshot.DiasPendientesPermiso;

                    return new
                    {
                        idEmpleado = row.IdEmpleado,
                        codigoEmpleado = row.CodigoEmpleado,
                        nombreEmpleado = row.NombreEmpleado,
                        fechaIngreso = snapshot.FechaIngreso?.ToString("yyyy-MM-dd") ?? row.FechaIngreso?.ToString("yyyy-MM-dd"),
                        nombreDepartamento = row.NombreDepartamento,
                        nombreCargo = row.NombreCargo,
                        nombreEstadoEmpleado = row.NombreEstadoEmpleado,
                        activo = row.Activo,
                        fechaCorte = cutoff.Date.ToString("yyyy-MM-dd"),
                        nombreTipoContratoVigente = snapshot.NombreTipoContratoVigente ?? row.NombreTipoContratoVigente,
                        codigoTipoContratoVigente = snapshot.CodigoTipoContratoVigente ?? row.CodigoTipoContratoVigente,
                        diasAcumulados = snapshot.DiasAcumulados,
                        diasTomadosVacacion = snapshot.DiasTomadosVacacion,
                        diasConsumidos = consumedDays,
                        diasPendientes = pendingDays,
                        diasDisponibles = snapshot.DiasDisponibles,
                        tieneContratoVigente = snapshot.TieneContratoVigente,
                        acumulaVacaciones = snapshot.AcumulaVacaciones,
                        tieneHistorialElegible = snapshot.TieneHistorialElegible,
                        motivoNoAcumulacion = snapshot.MotivoNoAcumulacion,
                    };
                })
                .ToList();

            RrhhSupport.RegisterBitacora(
                connection,
                null,
                HttpContext,
                "VACACIONES",
                "CONSULTA_REPORTE",
                0,
                $"SALDO-VACACIONES-{cutoff:yyyyMMdd}",
                "Se consulto el reporte de vacaciones disponibles por empleado.",
                new
                {
                    search = search?.Trim() ?? string.Empty,
                    idDepartamento,
                    status = normalizedStatus,
                    fechaCorte = cutoff.Date.ToString("yyyy-MM-dd"),
                    totalRegistros = rows.Count,
                });

            return Json(new
            {
                ok = true,
                data = new
                {
                    rows,
                    departments = LoadVacationAvailabilityDepartments(connection),
                    branding = ObtenerBrandingReporte(connection),
                    fechaCorte = cutoff.Date.ToString("yyyy-MM-dd"),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el reporte de vacaciones disponibles.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult AplicarAjusteVacacionesMasivo([FromBody] VacacionAjusteMasivoModel model)
    {
        var errors = ValidarAjusteVacacionesMasivo(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del ajuste masivo.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var operatorUser = RrhhSupport.GetOperatorUser(Request);
            var adjustmentDate = DateTime.Parse(model.FechaAjuste).Date;
            var employees = LoadActiveEmployees(connection, transaction);
            var applied = new List<object>();
            var skipped = new List<object>();

            foreach (var employee in employees)
            {
                var snapshot = RrhhSupport.CalculateVacationBalance(
                    connection,
                    transaction,
                    employee.IdEmpleado,
                    adjustmentDate);

                if (!snapshot.FechaIngreso.HasValue)
                {
                    skipped.Add(new
                    {
                        employee.IdEmpleado,
                        employee.CodigoEmpleado,
                        employee.NombreEmpleado,
                        reason = "No se encontro la fecha de ingreso del colaborador.",
                    });
                    continue;
                }

                if (snapshot.DiasDisponibles < model.CantidadDias)
                {
                    skipped.Add(new
                    {
                        employee.IdEmpleado,
                        employee.CodigoEmpleado,
                        employee.NombreEmpleado,
                        reason = RrhhSupport.BuildVacationAvailabilityMessage(
                            snapshot,
                            "Saldo insuficiente."),
                    });
                    continue;
                }

                long idVacacion;
                using (var command = new SqlCommand(
                    """
                    INSERT INTO rrhh.vacacion
                    (
                        id_empleado,
                        fecha_solicitud,
                        fecha_inicio,
                        fecha_fin,
                        dias_solicitados,
                        dias_aprobados,
                        estado_vacacion,
                        observacion_solicitud,
                        observacion_aprobacion,
                        usuario_solicita,
                        usuario_aprueba,
                        fecha_aprobacion,
                        pagada_en_nomina
                    )
                    OUTPUT INSERTED.id_vacacion
                    VALUES
                    (
                        @id_empleado,
                        @fecha_solicitud,
                        @fecha_inicio,
                        @fecha_fin,
                        @dias_solicitados,
                        @dias_aprobados,
                        N'APROBADA',
                        @observacion_solicitud,
                        @observacion_aprobacion,
                        @usuario_solicita,
                        @usuario_aprueba,
                        SYSDATETIME(),
                        0
                    );
                    """,
                    connection,
                    transaction))
                {
                    command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = employee.IdEmpleado;
                    command.Parameters.Add("@fecha_solicitud", SqlDbType.Date).Value = adjustmentDate;
                    command.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = adjustmentDate;
                    command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = adjustmentDate;
                    command.Parameters.Add("@dias_solicitados", SqlDbType.Decimal).Value = model.CantidadDias;
                    command.Parameters["@dias_solicitados"].Precision = 10;
                    command.Parameters["@dias_solicitados"].Scale = 2;
                    command.Parameters.Add("@dias_aprobados", SqlDbType.Decimal).Value = model.CantidadDias;
                    command.Parameters["@dias_aprobados"].Precision = 10;
                    command.Parameters["@dias_aprobados"].Scale = 2;
                    command.Parameters.Add("@observacion_solicitud", SqlDbType.NVarChar, 500).Value =
                        $"Ajuste masivo de vacaciones. {model.Observacion?.Trim()}".Trim();
                    command.Parameters.Add("@observacion_aprobacion", SqlDbType.NVarChar, 500).Value =
                        "Ajuste aplicado automaticamente desde RRHH.";
                    command.Parameters.Add("@usuario_solicita", SqlDbType.NVarChar, 100).Value = operatorUser;
                    command.Parameters.Add("@usuario_aprueba", SqlDbType.NVarChar, 100).Value = operatorUser;
                    idVacacion = Convert.ToInt64(command.ExecuteScalar());
                }

                var created = ObtenerVacacionInterna(connection, idVacacion, transaction)!;
                RrhhSupport.RegisterBitacora(
                    connection,
                    transaction,
                    HttpContext,
                    "VACACIONES",
                    "AJUSTE_MASIVO",
                    created.IdVacacion,
                    $"VAC-{created.IdVacacion}",
                    $"Se desconto {model.CantidadDias:0.##} dia(s) de vacaciones al empleado {created.CodigoEmpleado}.",
                    created,
                    operatorUser);

                applied.Add(new
                {
                    created.IdVacacion,
                    created.CodigoEmpleado,
                    created.NombreEmpleado,
                    created.DiasAprobados,
                });
            }

            if (applied.Count == 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Ningun colaborador tenia saldo suficiente para aplicar el ajuste.",
                    data = new
                    {
                        applied,
                        skipped,
                    },
                });
            }

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "VACACIONES",
                "AJUSTE_MASIVO_RESUMEN",
                0,
                $"VAC-BULK-{adjustmentDate:yyyyMMdd}",
                $"Se aplico un ajuste masivo de {model.CantidadDias:0.##} dia(s) a {applied.Count} colaborador(es).",
                new
                {
                    fecha = adjustmentDate.ToString("yyyy-MM-dd"),
                    cantidadDias = model.CantidadDias,
                    applied,
                    skipped,
                },
                operatorUser);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = $"Ajuste aplicado a {applied.Count} colaborador(es).",
                data = new
                {
                    applied,
                    skipped,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo aplicar el ajuste masivo de vacaciones.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult ListarPermisos(string? search, string? status)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

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
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = p.id_empleado
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                INNER JOIN rrhh.tipo_permiso tp
                    ON tp.id_tipo_permiso = p.id_tipo_permiso
                WHERE
                    (
                        @search = N''
                        OR e.codigo_empleado LIKE N'%' + @search + N'%'
                        OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                        OR tp.nombre_tipo_permiso LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'PENDIENTES' AND p.estado_permiso = N'SOLICITADO')
                        OR (@status = N'APROBADOS' AND p.estado_permiso = N'APROBADO')
                        OR (@status = N'RECHAZADOS' AND p.estado_permiso = N'RECHAZADO')
                    )
                ORDER BY p.id_solicitud_permiso DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeWorkflowStatus(status);

            using var reader = command.ExecuteReader();
            var items = new List<PermisoDto>();
            while (reader.Read())
            {
                items.Add(MapearPermiso(reader));
            }
            reader.Close();

            foreach (var item in items)
            {
                PopulateVacationBalance(connection, null, item, item.FechaFin);
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
                message = "No se pudo cargar el listado de permisos.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{id:long}")]
    public IActionResult ObtenerPermiso(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var item = ObtenerPermisoInterno(connection, id);
            if (item is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Permiso no encontrado.",
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
                message = "No se pudo obtener el permiso.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult CrearPermiso([FromBody] PermisoGuardarModel model)
    {
        var errors = ValidarPermiso(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del permiso.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            CompletarDatosPermiso(connection, transaction, model, errors, null);
            if (errors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos del permiso.",
                    errors,
                });
            }

            long idPermiso;
            using (var command = new SqlCommand(
                """
                INSERT INTO rrhh.solicitud_permiso
                (
                    id_empleado,
                    id_tipo_permiso,
                    fecha_solicitud,
                    fecha_inicio,
                    fecha_fin,
                    cantidad_dias,
                    estado_permiso,
                    observacion,
                    usuario_solicita
                )
                OUTPUT INSERTED.id_solicitud_permiso
                VALUES
                (
                    @id_empleado,
                    @id_tipo_permiso,
                    @fecha_solicitud,
                    @fecha_inicio,
                    @fecha_fin,
                    @cantidad_dias,
                    N'SOLICITADO',
                    @observacion,
                    @usuario_solicita
                );
                """,
                connection,
                transaction))
            {
                AsignarParametrosPermiso(command, model);
                command.Parameters.Add("@usuario_solicita", SqlDbType.NVarChar, 100).Value =
                    RrhhSupport.GetOperatorUser(Request);
                idPermiso = Convert.ToInt64(command.ExecuteScalar());
            }

            var created = ObtenerPermisoInterno(connection, idPermiso, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PERMISOS",
                "INSERCION",
                created.IdSolicitudPermiso,
                $"PERM-{created.IdSolicitudPermiso}",
                $"Se registro la solicitud de permiso del empleado {created.CodigoEmpleado}.",
                created);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Permiso registrado correctamente.",
                data = created,
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "La base de datos rechazo la operacion."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo registrar el permiso.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ActualizarPermiso(long id, [FromBody] PermisoGuardarModel model)
    {
        var errors = ValidarPermiso(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del permiso.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var current = ObtenerPermisoInterno(connection, id, transaction);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Permiso no encontrado.",
                });
            }

            if (!string.Equals(current.EstadoPermiso, "SOLICITADO", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo se pueden editar permisos en estado solicitado.",
                });
            }

            CompletarDatosPermiso(connection, transaction, model, errors, id);
            if (errors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos del permiso.",
                    errors,
                });
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.solicitud_permiso
                SET
                    id_empleado = @id_empleado,
                    id_tipo_permiso = @id_tipo_permiso,
                    fecha_solicitud = @fecha_solicitud,
                    fecha_inicio = @fecha_inicio,
                    fecha_fin = @fecha_fin,
                    cantidad_dias = @cantidad_dias,
                    observacion = @observacion
                WHERE id_solicitud_permiso = @id_solicitud_permiso;
                """,
                connection,
                transaction))
            {
                AsignarParametrosPermiso(command, model);
                command.Parameters.Add("@id_solicitud_permiso", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerPermisoInterno(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PERMISOS",
                "MODIFICACION",
                updated.IdSolicitudPermiso,
                $"PERM-{updated.IdSolicitudPermiso}",
                $"Se actualizo la solicitud de permiso del empleado {updated.CodigoEmpleado}.",
                new
                {
                    antes = current,
                    despues = updated,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Permiso actualizado correctamente.",
                data = updated,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar el permiso.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ResolverPermiso(long id, [FromBody] WorkflowResolutionModel model)
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
            using var transaction = connection.BeginTransaction();

            var current = ObtenerPermisoInterno(connection, id, transaction);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Permiso no encontrado.",
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

            var updated = ObtenerPermisoInterno(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "PERMISOS",
                action == "APROBAR" ? "APROBACION" : "RECHAZO",
                updated.IdSolicitudPermiso,
                $"PERM-{updated.IdSolicitudPermiso}",
                action == "APROBAR"
                    ? $"Se aprobo la solicitud de permiso {updated.IdSolicitudPermiso}."
                    : $"Se rechazo la solicitud de permiso {updated.IdSolicitudPermiso}.",
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

    [HttpGet]
    public IActionResult ListarVacaciones(string? search, string? status)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

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
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = v.id_empleado
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                WHERE
                    (
                        @search = N''
                        OR e.codigo_empleado LIKE N'%' + @search + N'%'
                        OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'PENDIENTES' AND v.estado_vacacion = N'SOLICITADA')
                        OR (@status = N'APROBADOS' AND v.estado_vacacion = N'APROBADA')
                        OR (@status = N'RECHAZADOS' AND v.estado_vacacion = N'RECHAZADA')
                    )
                ORDER BY v.id_vacacion DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeWorkflowStatus(status);

            using var reader = command.ExecuteReader();
            var items = new List<VacacionDto>();
            while (reader.Read())
            {
                items.Add(MapearVacacion(reader));
            }
            reader.Close();

            foreach (var item in items)
            {
                PopulateVacationBalance(connection, null, item, item.FechaFin);
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
                message = "No se pudo cargar el listado de vacaciones.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{id:long}")]
    public IActionResult ObtenerVacacion(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var item = ObtenerVacacionInterna(connection, id);
            if (item is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Vacacion no encontrada.",
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
                message = "No se pudo obtener la vacacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult CrearVacacion([FromBody] VacacionGuardarModel model)
    {
        var errors = ValidarVacacion(model);
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
            using var transaction = connection.BeginTransaction();

            CompletarDatosVacacion(connection, transaction, model, errors, null);
            if (errors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la vacacion.",
                    errors,
                });
            }

            long idVacacion;
            using (var command = new SqlCommand(
                """
                INSERT INTO rrhh.vacacion
                (
                    id_empleado,
                    fecha_solicitud,
                    fecha_inicio,
                    fecha_fin,
                    dias_solicitados,
                    estado_vacacion,
                    observacion_solicitud,
                    usuario_solicita,
                    pagada_en_nomina
                )
                OUTPUT INSERTED.id_vacacion
                VALUES
                (
                    @id_empleado,
                    @fecha_solicitud,
                    @fecha_inicio,
                    @fecha_fin,
                    @dias_solicitados,
                    N'SOLICITADA',
                    @observacion_solicitud,
                    @usuario_solicita,
                    0
                );
                """,
                connection,
                transaction))
            {
                AsignarParametrosVacacion(command, model);
                command.Parameters.Add("@usuario_solicita", SqlDbType.NVarChar, 100).Value =
                    RrhhSupport.GetOperatorUser(Request);
                idVacacion = Convert.ToInt64(command.ExecuteScalar());
            }

            var created = ObtenerVacacionInterna(connection, idVacacion, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "VACACIONES",
                "INSERCION",
                created.IdVacacion,
                $"VAC-{created.IdVacacion}",
                $"Se registro la solicitud de vacacion del empleado {created.CodigoEmpleado}.",
                created);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Vacacion registrada correctamente.",
                data = created,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo registrar la vacacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ActualizarVacacion(long id, [FromBody] VacacionGuardarModel model)
    {
        var errors = ValidarVacacion(model);
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
            using var transaction = connection.BeginTransaction();

            var current = ObtenerVacacionInterna(connection, id, transaction);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Vacacion no encontrada.",
                });
            }

            if (!string.Equals(current.EstadoVacacion, "SOLICITADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo se pueden editar vacaciones solicitadas.",
                });
            }

            CompletarDatosVacacion(connection, transaction, model, errors, id);
            if (errors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la vacacion.",
                    errors,
                });
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.vacacion
                SET
                    id_empleado = @id_empleado,
                    fecha_solicitud = @fecha_solicitud,
                    fecha_inicio = @fecha_inicio,
                    fecha_fin = @fecha_fin,
                    dias_solicitados = @dias_solicitados,
                    observacion_solicitud = @observacion_solicitud
                WHERE id_vacacion = @id_vacacion;
                """,
                connection,
                transaction))
            {
                AsignarParametrosVacacion(command, model);
                command.Parameters.Add("@id_vacacion", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerVacacionInterna(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "VACACIONES",
                "MODIFICACION",
                updated.IdVacacion,
                $"VAC-{updated.IdVacacion}",
                $"Se actualizo la solicitud de vacacion del empleado {updated.CodigoEmpleado}.",
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
                message = "No se pudo actualizar la vacacion.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ResolverVacacion(long id, [FromBody] VacacionResolutionModel model)
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
            using var transaction = connection.BeginTransaction();

            var current = ObtenerVacacionInterna(connection, id, transaction);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Vacacion no encontrada.",
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

            var updated = ObtenerVacacionInterna(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "VACACIONES",
                action == "APROBAR" ? "APROBACION" : "RECHAZO",
                updated.IdVacacion,
                $"VAC-{updated.IdVacacion}",
                action == "APROBAR"
                    ? $"Se aprobo la solicitud de vacacion {updated.IdVacacion}."
                    : $"Se rechazo la solicitud de vacacion {updated.IdVacacion}.",
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

    [HttpGet]
    public IActionResult ListarHorasExtra(string? search, string? status)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

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
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = h.id_empleado
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                INNER JOIN rrhh.tipo_hora_extra th
                    ON th.id_tipo_hora_extra = h.id_tipo_hora_extra
                WHERE
                    (
                        @search = N''
                        OR e.codigo_empleado LIKE N'%' + @search + N'%'
                        OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                        OR th.nombre_tipo_hora_extra LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'PENDIENTES' AND h.estado_hora_extra = N'REGISTRADA')
                        OR (@status = N'APROBADOS' AND h.estado_hora_extra = N'APROBADA')
                        OR (@status = N'RECHAZADOS' AND h.estado_hora_extra = N'RECHAZADA')
                    )
                ORDER BY h.id_hora_extra DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeWorkflowStatus(status);

            using var reader = command.ExecuteReader();
            var items = new List<HoraExtraDto>();
            while (reader.Read())
            {
                items.Add(MapearHoraExtra(reader));
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
                message = "No se pudo cargar el listado de horas extra.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{id:long}")]
    public IActionResult ObtenerHoraExtra(long id)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var item = ObtenerHoraExtraInterna(connection, id);
            if (item is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Hora extra no encontrada.",
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
                message = "No se pudo obtener la hora extra.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult CrearHoraExtra([FromBody] HoraExtraGuardarModel model)
    {
        var errors = ValidarHoraExtra(model);
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
            using var transaction = connection.BeginTransaction();

            CompletarDatosHoraExtra(connection, transaction, model, errors, null);
            if (errors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la hora extra.",
                    errors,
                });
            }

            long idHoraExtra;
            using (var command = new SqlCommand(
                """
                INSERT INTO rrhh.hora_extra
                (
                    id_empleado,
                    id_tipo_hora_extra,
                    fecha_hora_extra,
                    cantidad_horas,
                    estado_hora_extra,
                    observacion,
                    usuario_registra,
                    pagada_en_nomina
                )
                OUTPUT INSERTED.id_hora_extra
                VALUES
                (
                    @id_empleado,
                    @id_tipo_hora_extra,
                    @fecha_hora_extra,
                    @cantidad_horas,
                    N'REGISTRADA',
                    @observacion,
                    @usuario_registra,
                    0
                );
                """,
                connection,
                transaction))
            {
                AsignarParametrosHoraExtra(command, model);
                command.Parameters.Add("@usuario_registra", SqlDbType.NVarChar, 100).Value =
                    RrhhSupport.GetOperatorUser(Request);
                idHoraExtra = Convert.ToInt64(command.ExecuteScalar());
            }

            var created = ObtenerHoraExtraInterna(connection, idHoraExtra, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "HORAS_EXTRA",
                "INSERCION",
                created.IdHoraExtra,
                $"HEX-{created.IdHoraExtra}",
                $"Se registro hora extra para el empleado {created.CodigoEmpleado}.",
                created);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Hora extra registrada correctamente.",
                data = created,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo registrar la hora extra.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ActualizarHoraExtra(long id, [FromBody] HoraExtraGuardarModel model)
    {
        var errors = ValidarHoraExtra(model);
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
            using var transaction = connection.BeginTransaction();

            var current = ObtenerHoraExtraInterna(connection, id, transaction);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Hora extra no encontrada.",
                });
            }

            if (!string.Equals(current.EstadoHoraExtra, "REGISTRADA", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Rollback();
                return StatusCode(409, new
                {
                    ok = false,
                    message = "Solo se pueden editar horas extra registradas.",
                });
            }

            CompletarDatosHoraExtra(connection, transaction, model, errors, id);
            if (errors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la hora extra.",
                    errors,
                });
            }

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.hora_extra
                SET
                    id_empleado = @id_empleado,
                    id_tipo_hora_extra = @id_tipo_hora_extra,
                    fecha_hora_extra = @fecha_hora_extra,
                    cantidad_horas = @cantidad_horas,
                    observacion = @observacion
                WHERE id_hora_extra = @id_hora_extra;
                """,
                connection,
                transaction))
            {
                AsignarParametrosHoraExtra(command, model);
                command.Parameters.Add("@id_hora_extra", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = ObtenerHoraExtraInterna(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "HORAS_EXTRA",
                "MODIFICACION",
                updated.IdHoraExtra,
                $"HEX-{updated.IdHoraExtra}",
                $"Se actualizo la hora extra del empleado {updated.CodigoEmpleado}.",
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
                message = "No se pudo actualizar la hora extra.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult ResolverHoraExtra(long id, [FromBody] WorkflowResolutionModel model)
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
            using var transaction = connection.BeginTransaction();

            var current = ObtenerHoraExtraInterna(connection, id, transaction);
            if (current is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Hora extra no encontrada.",
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

            var updated = ObtenerHoraExtraInterna(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "HORAS_EXTRA",
                action == "APROBAR" ? "APROBACION" : "RECHAZO",
                updated.IdHoraExtra,
                $"HEX-{updated.IdHoraExtra}",
                action == "APROBAR"
                    ? $"Se aprobo la hora extra {updated.IdHoraExtra}."
                    : $"Se rechazo la hora extra {updated.IdHoraExtra}.",
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

    private static Dictionary<string, string> ValidarPermiso(PermisoGuardarModel model)
    {
        var errors = new Dictionary<string, string>();
        var minDate = new DateTime(1753, 1, 1);

        if (model.IdEmpleado <= 0)
        {
            errors["idEmpleado"] = "Selecciona el empleado.";
        }

        if (model.IdTipoPermiso <= 0)
        {
            errors["idTipoPermiso"] = "Selecciona el tipo de permiso.";
        }

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
        else if (DateTime.TryParse(model.FechaInicio, out var validStart) && endDate.Date < validStart.Date)
        {
            errors["fechaFin"] = "La fecha fin debe ser igual o mayor a la fecha de inicio.";
        }

        if (model.EsMedioDia)
        {
            if (DateTime.TryParse(model.FechaInicio, out var halfStart) &&
                DateTime.TryParse(model.FechaFin, out var halfEnd) &&
                halfStart.Date != halfEnd.Date)
            {
                errors["fechaFin"] = "Si el permiso es de medio dia, la fecha fin debe ser igual a la fecha de inicio.";
            }

            if (NormalizeHalfDayShift(model.JornadaMedioDia) is null)
            {
                errors["jornadaMedioDia"] = "Selecciona si corresponde a manana o tarde.";
            }
        }

        var requestedDays = model.EsMedioDia
            ? 0.5m
            : DateTime.TryParse(model.FechaInicio, out var startForDays) &&
              DateTime.TryParse(model.FechaFin, out var endForDays)
                ? CalculateDaysInclusive(startForDays, endForDays)
                : 0m;

        if (requestedDays <= 0)
        {
            errors["fechaFin"] = "La cantidad de dias del permiso no es valida.";
        }

        if (!string.IsNullOrWhiteSpace(model.Observacion) && model.Observacion.Trim().Length > 320)
        {
            errors["observacion"] = "La observacion del permiso supera el limite permitido.";
        }

        return errors;
    }

    private static Dictionary<string, string> ValidarVacacion(VacacionGuardarModel model)
    {
        var errors = new Dictionary<string, string>();
        var minDate = new DateTime(1753, 1, 1);

        if (model.IdEmpleado <= 0)
        {
            errors["idEmpleado"] = "Selecciona el empleado.";
        }

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
        else if (DateTime.TryParse(model.FechaInicio, out var validStart) && endDate.Date < validStart.Date)
        {
            errors["fechaFin"] = "La fecha fin debe ser igual o mayor a la fecha de inicio.";
        }

        if (model.EsMedioDia)
        {
            if (DateTime.TryParse(model.FechaInicio, out var halfDayStart) &&
                DateTime.TryParse(model.FechaFin, out var halfDayEnd) &&
                halfDayStart.Date != halfDayEnd.Date)
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

    private static Dictionary<string, string> ValidarHoraExtra(HoraExtraGuardarModel model)
    {
        var errors = new Dictionary<string, string>();
        var today = DateTime.Today;
        var minDate = new DateTime(1753, 1, 1);

        if (model.IdEmpleado <= 0)
        {
            errors["idEmpleado"] = "Selecciona el empleado.";
        }

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

    private void CompletarDatosPermiso(
        SqlConnection connection,
        SqlTransaction transaction,
        PermisoGuardarModel model,
        Dictionary<string, string> errors,
        long? currentId)
    {
        if (!EmpleadoActivoExiste(connection, transaction, model.IdEmpleado, out var ingreso))
        {
            errors["idEmpleado"] = "El empleado seleccionado no existe, esta inactivo o ya fue retirado.";
        }

        if (!CatalogoActivoExiste(connection, transaction, "rrhh.tipo_permiso", "id_tipo_permiso", model.IdTipoPermiso))
        {
            errors["idTipoPermiso"] = "El tipo de permiso seleccionado no existe.";
        }

        if (DateTime.TryParse(model.FechaInicio, out var startDate) &&
            ingreso.HasValue &&
            startDate.Date < ingreso.Value.Date)
        {
            errors["fechaInicio"] = $"La fecha de inicio no puede ser menor al ingreso del empleado ({ingreso.Value:dd/MM/yyyy}).";
        }

        if (DateTime.TryParse(model.FechaInicio, out startDate) &&
            DateTime.TryParse(model.FechaFin, out var endDate) &&
            ExisteSolapamientoPermiso(connection, transaction, model.IdEmpleado, startDate, endDate, currentId))
        {
            errors["fechaInicio"] = "El empleado ya tiene otro permiso en ese rango de fechas.";
        }

        if (errors.Count == 0 &&
            DateTime.TryParse(model.FechaFin, out var cutoff))
        {
            var snapshot = RrhhSupport.CalculateVacationBalance(connection, transaction, model.IdEmpleado, cutoff);
            var requestedDays = GetRequestedPermissionDays(model);

            if (snapshot.DiasDisponibles < requestedDays)
            {
                errors["fechaFin"] =
                    RrhhSupport.BuildVacationAvailabilityMessage(snapshot);
            }
        }
    }

    private void CompletarDatosVacacion(
        SqlConnection connection,
        SqlTransaction transaction,
        VacacionGuardarModel model,
        Dictionary<string, string> errors,
        long? currentId)
    {
        if (!EmpleadoActivoExiste(connection, transaction, model.IdEmpleado, out var ingreso))
        {
            errors["idEmpleado"] = "El empleado seleccionado no existe, esta inactivo o ya fue retirado.";
        }

        if (DateTime.TryParse(model.FechaInicio, out var startDate) &&
            ingreso.HasValue &&
            startDate.Date < ingreso.Value.Date)
        {
            errors["fechaInicio"] = $"La fecha de inicio no puede ser menor al ingreso del empleado ({ingreso.Value:dd/MM/yyyy}).";
        }

        if (DateTime.TryParse(model.FechaInicio, out startDate) &&
            DateTime.TryParse(model.FechaFin, out var endDate) &&
            ExisteSolapamientoVacacion(connection, transaction, model.IdEmpleado, startDate, endDate, currentId))
        {
            errors["fechaInicio"] = "El empleado ya tiene otra vacacion en ese rango de fechas.";
        }

        if (errors.Count == 0 &&
            DateTime.TryParse(model.FechaFin, out var cutoff))
        {
            var snapshot = RrhhSupport.CalculateVacationBalance(connection, transaction, model.IdEmpleado, cutoff);
            var requestedDays = GetRequestedVacationDays(model);

            if (snapshot.DiasDisponibles < requestedDays)
            {
                errors["fechaFin"] =
                    RrhhSupport.BuildVacationAvailabilityMessage(snapshot);
            }
        }
    }

    private void CompletarDatosHoraExtra(
        SqlConnection connection,
        SqlTransaction transaction,
        HoraExtraGuardarModel model,
        Dictionary<string, string> errors,
        long? currentId)
    {
        if (!EmpleadoActivoExiste(connection, transaction, model.IdEmpleado, out var ingreso))
        {
            errors["idEmpleado"] = "El empleado seleccionado no existe, esta inactivo o ya fue retirado.";
        }

        if (!CatalogoActivoExiste(connection, transaction, "rrhh.tipo_hora_extra", "id_tipo_hora_extra", model.IdTipoHoraExtra))
        {
            errors["idTipoHoraExtra"] = "El tipo de hora extra seleccionado no existe.";
        }

        if (DateTime.TryParse(model.FechaHoraExtra, out var date) &&
            ingreso.HasValue &&
            date.Date < ingreso.Value.Date)
        {
            errors["fechaHoraExtra"] = $"La fecha no puede ser menor al ingreso del empleado ({ingreso.Value:dd/MM/yyyy}).";
        }

        if (DateTime.TryParse(model.FechaHoraExtra, out date) &&
            ExisteDuplicadoHoraExtra(connection, transaction, model.IdEmpleado, model.IdTipoHoraExtra, date, currentId))
        {
            errors["fechaHoraExtra"] = "Ya existe una hora extra similar para ese empleado y fecha.";
        }
    }

    private static void AsignarParametrosPermiso(SqlCommand command, PermisoGuardarModel model)
    {
        var rawObservation = BuildPermissionObservationPayload(
            model.Observacion,
            model.EsMedioDia,
            model.JornadaMedioDia,
            null);

        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = model.IdEmpleado;
        command.Parameters.Add("@id_tipo_permiso", SqlDbType.BigInt).Value = model.IdTipoPermiso;
        command.Parameters.Add("@fecha_solicitud", SqlDbType.Date).Value =
            string.IsNullOrWhiteSpace(model.FechaSolicitud)
                ? DateTime.Today
                : DateTime.Parse(model.FechaSolicitud);
        command.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = DateTime.Parse(model.FechaInicio);
        command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = DateTime.Parse(model.FechaFin);
        command.Parameters.Add("@cantidad_dias", SqlDbType.Decimal).Value =
            GetRequestedPermissionDays(model);
        command.Parameters["@cantidad_dias"].Precision = 10;
        command.Parameters["@cantidad_dias"].Scale = 2;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value = RrhhSupport.ToDbValue(rawObservation);
    }

    private static void AsignarParametrosVacacion(SqlCommand command, VacacionGuardarModel model)
    {
        var rawObservation = BuildVacationObservationPayload(
            model.ObservacionSolicitud,
            model.EsMedioDia,
            model.JornadaMedioDia);

        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = model.IdEmpleado;
        command.Parameters.Add("@fecha_solicitud", SqlDbType.Date).Value =
            string.IsNullOrWhiteSpace(model.FechaSolicitud)
                ? DateTime.Today
                : DateTime.Parse(model.FechaSolicitud);
        command.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = DateTime.Parse(model.FechaInicio);
        command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = DateTime.Parse(model.FechaFin);
        command.Parameters.Add("@dias_solicitados", SqlDbType.Decimal).Value =
            GetRequestedVacationDays(model);
        command.Parameters["@dias_solicitados"].Precision = 10;
        command.Parameters["@dias_solicitados"].Scale = 2;
        command.Parameters.Add("@observacion_solicitud", SqlDbType.NVarChar, 500).Value =
            RrhhSupport.ToDbValue(rawObservation);
    }

    private static void AsignarParametrosHoraExtra(SqlCommand command, HoraExtraGuardarModel model)
    {
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = model.IdEmpleado;
        command.Parameters.Add("@id_tipo_hora_extra", SqlDbType.BigInt).Value = model.IdTipoHoraExtra;
        command.Parameters.Add("@fecha_hora_extra", SqlDbType.Date).Value = DateTime.Parse(model.FechaHoraExtra);
        command.Parameters.Add("@cantidad_horas", SqlDbType.Decimal).Value = model.CantidadHoras;
        command.Parameters["@cantidad_horas"].Precision = 10;
        command.Parameters["@cantidad_horas"].Scale = 2;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value =
            RrhhSupport.ToDbValue(model.Observacion);
    }

    private static decimal GetRequestedPermissionDays(PermisoGuardarModel model)
    {
        if (model.EsMedioDia)
        {
            return 0.5m;
        }

        return CalculateDaysInclusive(DateTime.Parse(model.FechaInicio), DateTime.Parse(model.FechaFin));
    }

    private static decimal GetRequestedVacationDays(VacacionGuardarModel model)
    {
        if (model.EsMedioDia)
        {
            return 0.5m;
        }

        return CalculateDaysInclusive(DateTime.Parse(model.FechaInicio), DateTime.Parse(model.FechaFin));
    }

    private static string? NormalizeHalfDayShift(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "MANANA" => "MANANA",
            "MAÑANA" => "MANANA",
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
            // Se trata como texto plano heredado.
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

    private static VacationObservationEnvelope ParseVacationObservation(string? rawObservation)
    {
        if (string.IsNullOrWhiteSpace(rawObservation))
        {
            return new VacationObservationEnvelope();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<VacationObservationEnvelope>(rawObservation);
            if (parsed is not null && parsed.IsEnvelope)
            {
                parsed.TextoSolicitud = string.IsNullOrWhiteSpace(parsed.TextoSolicitud)
                    ? null
                    : parsed.TextoSolicitud.Trim();
                parsed.JornadaMedioDia = NormalizeHalfDayShift(parsed.JornadaMedioDia);
                return parsed;
            }
        }
        catch
        {
            // Se trata como texto plano heredado.
        }

        return new VacationObservationEnvelope
        {
            TextoSolicitud = rawObservation.Trim(),
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

        var envelope = new VacationObservationEnvelope
        {
            TextoSolicitud = requestText,
            EsMedioDia = isHalfDay,
            JornadaMedioDia = normalizedShift,
        };

        var payload = JsonSerializer.Serialize(envelope);
        return string.IsNullOrWhiteSpace(payload) ? null : payload;
    }

    private static object BuildVacationBalanceDto(RrhhSupport.VacationBalanceSnapshot snapshot) => new
    {
        idEmpleado = snapshot.IdEmpleado,
        fechaIngreso = snapshot.FechaIngreso?.ToString("yyyy-MM-dd"),
        fechaCorte = snapshot.FechaCorte.ToString("yyyy-MM-dd"),
        diasAcumulados = snapshot.DiasAcumulados,
        diasTomadosVacacion = snapshot.DiasTomadosVacacion,
        diasConsumidos = snapshot.DiasTomadosVacacion + snapshot.DiasDescontadosPermiso,
        diasPendientes = snapshot.DiasPendientesVacacion + snapshot.DiasPendientesPermiso,
        diasDisponibles = snapshot.DiasDisponibles,
        tieneContratoVigente = snapshot.TieneContratoVigente,
        codigoTipoContratoVigente = snapshot.CodigoTipoContratoVigente,
        nombreTipoContratoVigente = snapshot.NombreTipoContratoVigente,
        acumulaVacaciones = snapshot.AcumulaVacaciones,
        tieneHistorialElegible = snapshot.TieneHistorialElegible,
        motivoNoAcumulacion = snapshot.MotivoNoAcumulacion,
    };

    private static string NormalizeVacationReportEmployeeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "TODOS";
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "ACTIVOS" => "ACTIVOS",
            "INACTIVOS" => "INACTIVOS",
            _ => "TODOS",
        };
    }

    private static List<VacationAvailabilityBaseRow> LoadVacationAvailabilityBaseRows(
        SqlConnection connection,
        string? search,
        long? idDepartamento,
        string status,
        DateTime cutoffDate)
    {
        const string sql = """
            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.fecha_ingreso,
                d.nombre_departamento,
                c.nombre_cargo,
                ee.nombre_estado_empleado,
                e.activo,
                contrato_actual.codigo_tipo_contrato,
                contrato_actual.nombre_tipo_contrato
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
                    tc.codigo_tipo_contrato,
                    tc.nombre_tipo_contrato
                FROM rrhh.contrato co
                INNER JOIN rrhh.tipo_contrato tc
                    ON tc.id_tipo_contrato = co.id_tipo_contrato
                WHERE co.id_empleado = e.id_empleado
                  AND co.fecha_inicio <= @fecha_corte
                  AND (co.fecha_fin IS NULL OR co.fecha_fin >= @fecha_corte)
                ORDER BY co.es_contrato_vigente DESC, co.fecha_inicio DESC, co.id_contrato DESC
            ) contrato_actual
            WHERE
                (
                    @search = N''
                    OR e.codigo_empleado LIKE N'%' + @search + N'%'
                    OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                    OR d.nombre_departamento LIKE N'%' + @search + N'%'
                    OR c.nombre_cargo LIKE N'%' + @search + N'%'
                )
                AND
                (
                    @id_departamento IS NULL
                    OR e.id_departamento = @id_departamento
                )
                AND
                (
                    @status = N'TODOS'
                    OR (@status = N'ACTIVOS' AND e.activo = 1)
                    OR (@status = N'INACTIVOS' AND e.activo = 0)
                )
            ORDER BY nombre_empleado;
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@search", SqlDbType.NVarChar, 200).Value = search?.Trim() ?? string.Empty;
        command.Parameters.Add("@id_departamento", SqlDbType.BigInt).Value = idDepartamento.HasValue && idDepartamento.Value > 0
            ? idDepartamento.Value
            : DBNull.Value;
        command.Parameters.Add("@status", SqlDbType.NVarChar, 20).Value = status;
        command.Parameters.Add("@fecha_corte", SqlDbType.Date).Value = cutoffDate.Date;

        using var reader = command.ExecuteReader();
        var items = new List<VacationAvailabilityBaseRow>();

        while (reader.Read())
        {
            items.Add(new VacationAvailabilityBaseRow
            {
                IdEmpleado = reader.GetInt64(0),
                CodigoEmpleado = reader.GetString(1),
                NombreEmpleado = reader.GetString(2),
                FechaIngreso = reader.IsDBNull(3) ? null : reader.GetDateTime(3).Date,
                NombreDepartamento = reader.GetString(4),
                NombreCargo = reader.GetString(5),
                NombreEstadoEmpleado = reader.GetString(6),
                Activo = reader.GetBoolean(7),
                CodigoTipoContratoVigente = reader.IsDBNull(8) ? null : reader.GetString(8),
                NombreTipoContratoVigente = reader.IsDBNull(9) ? null : reader.GetString(9),
            });
        }

        return items;
    }

    private static List<object> LoadVacationAvailabilityDepartments(SqlConnection connection)
    {
        const string sql = """
            SELECT
                id_departamento,
                nombre_departamento
            FROM rrhh.departamento
            WHERE activo = 1
            ORDER BY nombre_departamento;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var items = new List<object>();

        while (reader.Read())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                name = reader.GetString(1),
            });
        }

        return items;
    }

    private ReportBrandingDto ObtenerBrandingReporte(SqlConnection connection)
    {
        const string sql = """
            SELECT TOP (1)
                e.razon_social,
                e.nombre_comercial,
                e.ruc,
                e.telefono,
                e.correo,
                e.direccion,
                cg.logo_sidebar_url,
                cg.logo_login_url,
                cg.nombre_sistema,
                cg.texto_footer
            FROM empresa.empresa e
            LEFT JOIN empresa.configuracion_general cg
                ON cg.id_empresa = e.id_empresa
               AND cg.activo = 1
            WHERE e.activo = 1
            ORDER BY e.id_empresa;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return new ReportBrandingDto
            {
                CompanyName = "SISFNIC",
                LegalName = "Sistema Informacion Financiera Nicaragua, S.A.",
                LogoUrl = null,
                LogoPending = true,
                FooterText = "Logo pendiente de configuracion.",
            };
        }

        var logoSidebar = reader.IsDBNull(6) ? null : reader.GetString(6);
        var logoLogin = reader.IsDBNull(7) ? null : reader.GetString(7);
        var logoUrl = !string.IsNullOrWhiteSpace(logoSidebar) ? logoSidebar : logoLogin;

        return new ReportBrandingDto
        {
            CompanyName = reader.IsDBNull(1) ? "SISFNIC" : reader.GetString(1),
            LegalName = reader.IsDBNull(0) ? "Sistema Informacion Financiera Nicaragua, S.A." : reader.GetString(0),
            Ruc = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Phone = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            Address = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            LogoUrl = logoUrl,
            LogoPending = string.IsNullOrWhiteSpace(logoUrl),
            SystemName = reader.IsDBNull(8) ? "SISFNIC" : reader.GetString(8),
            FooterText = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
        };
    }

    private static void PopulateVacationBalance(
        SqlConnection connection,
        SqlTransaction? transaction,
        PermisoDto item,
        string? cutoffDate)
    {
        var snapshot = RrhhSupport.CalculateVacationBalance(
            connection,
            transaction,
            item.IdEmpleado,
            DateTime.TryParse(cutoffDate, out var cutoff) ? cutoff : DateTime.Today);

        item.DiasVacacionesAcumulados = snapshot.DiasAcumulados;
        item.DiasVacacionesTomados = snapshot.DiasTomadosVacacion + snapshot.DiasDescontadosPermiso;
        item.DiasVacacionesDisponibles = snapshot.DiasDisponibles;
        item.DiasVacacionesPendientes = snapshot.DiasPendientesVacacion + snapshot.DiasPendientesPermiso;
        item.DiasPermisosPendientes = 0;
    }

    private static void PopulateVacationBalance(
        SqlConnection connection,
        SqlTransaction? transaction,
        VacacionDto item,
        string? cutoffDate)
    {
        var snapshot = RrhhSupport.CalculateVacationBalance(
            connection,
            transaction,
            item.IdEmpleado,
            DateTime.TryParse(cutoffDate, out var cutoff) ? cutoff : DateTime.Today);

        item.DiasVacacionesAcumulados = snapshot.DiasAcumulados;
        item.DiasVacacionesTomados = snapshot.DiasTomadosVacacion + snapshot.DiasDescontadosPermiso;
        item.DiasVacacionesDisponibles = snapshot.DiasDisponibles;
        item.DiasVacacionesPendientes = snapshot.DiasPendientesVacacion + snapshot.DiasPendientesPermiso;
        item.DiasPermisosPendientes = 0;
    }

    private static List<ActiveEmployeeRow> LoadActiveEmployees(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        const string sql = """
            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado
            FROM rrhh.empleado e
            WHERE e.activo = 1
            ORDER BY nombre_empleado;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        using var reader = command.ExecuteReader();
        var items = new List<ActiveEmployeeRow>();

        while (reader.Read())
        {
            items.Add(new ActiveEmployeeRow
            {
                IdEmpleado = reader.GetInt64(0),
                CodigoEmpleado = reader.GetString(1),
                NombreEmpleado = reader.GetString(2),
            });
        }

        return items;
    }

    private static Dictionary<string, string> ValidarAjusteVacacionesMasivo(VacacionAjusteMasivoModel model)
    {
        var errors = new Dictionary<string, string>();

        if (!DateTime.TryParse(model.FechaAjuste, out var adjustmentDate))
        {
            errors["fechaAjuste"] = "Ingresa la fecha del ajuste.";
        }
        else if (adjustmentDate.Date < new DateTime(1753, 1, 1))
        {
            errors["fechaAjuste"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }

        if (model.CantidadDias != 0.5m && model.CantidadDias != 1m)
        {
            errors["cantidadDias"] = "Selecciona medio dia o un dia completo.";
        }

        if (!string.IsNullOrWhiteSpace(model.Observacion) && model.Observacion.Trim().Length > 250)
        {
            errors["observacion"] = "La observacion supera el limite permitido.";
        }

        return errors;
    }

    private PermisoDto? ObtenerPermisoInterno(SqlConnection connection, long id, SqlTransaction? transaction = null)
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
            INNER JOIN rrhh.empleado e ON e.id_empleado = p.id_empleado
            INNER JOIN rrhh.departamento d ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.tipo_permiso tp ON tp.id_tipo_permiso = p.id_tipo_permiso
            WHERE p.id_solicitud_permiso = @id;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var item = MapearPermiso(reader);
        reader.Close();
        PopulateVacationBalance(connection, transaction, item, item.FechaFin);
        return item;
    }

    private VacacionDto? ObtenerVacacionInterna(SqlConnection connection, long id, SqlTransaction? transaction = null)
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
            INNER JOIN rrhh.empleado e ON e.id_empleado = v.id_empleado
            INNER JOIN rrhh.departamento d ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c ON c.id_cargo = e.id_cargo
            WHERE v.id_vacacion = @id;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var item = MapearVacacion(reader);
        reader.Close();
        PopulateVacationBalance(connection, transaction, item, item.FechaFin);
        return item;
    }

    private HoraExtraDto? ObtenerHoraExtraInterna(SqlConnection connection, long id, SqlTransaction? transaction = null)
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
            INNER JOIN rrhh.empleado e ON e.id_empleado = h.id_empleado
            INNER JOIN rrhh.departamento d ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c ON c.id_cargo = e.id_cargo
            INNER JOIN rrhh.tipo_hora_extra th ON th.id_tipo_hora_extra = h.id_tipo_hora_extra
            WHERE h.id_hora_extra = @id;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapearHoraExtra(reader) : null;
    }

    private static PermisoDto MapearPermiso(SqlDataReader reader)
    {
        var rawObservation = reader.IsDBNull(15) ? null : reader.GetString(15);
        var envelope = ParsePermissionObservation(rawObservation);

        return new PermisoDto
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

    private static VacacionDto MapearVacacion(SqlDataReader reader)
    {
        var rawObservation = reader.IsDBNull(12) ? null : reader.GetString(12);
        var envelope = ParseVacationObservation(rawObservation);

        return new VacacionDto
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
            ObservacionSolicitud = envelope.TextoSolicitud,
            EsMedioDia = envelope.EsMedioDia,
            JornadaMedioDia = envelope.JornadaMedioDia,
            ObservacionAprobacion = reader.IsDBNull(13) ? null : reader.GetString(13),
            UsuarioSolicita = reader.GetString(14),
            UsuarioAprueba = reader.IsDBNull(15) ? null : reader.GetString(15),
            FechaAprobacion = reader.IsDBNull(16) ? null : reader.GetDateTime(16).ToString("yyyy-MM-dd HH:mm:ss"),
            PagadaEnNomina = reader.GetBoolean(17),
            FechaRegistro = reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
            ObservacionSolicitudRaw = rawObservation,
        };
    }

    private static HoraExtraDto MapearHoraExtra(SqlDataReader reader) => new()
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
        PagadaEnNomina = reader.GetBoolean(17),
        FechaRegistro = reader.GetDateTime(18).ToString("yyyy-MM-dd HH:mm:ss"),
    };

    private static bool CatalogoActivoExiste(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        string idColumn,
        long id)
    {
        using var command = new SqlCommand(
            $"SELECT COUNT(1) FROM {table} WHERE {idColumn} = @id AND activo = 1;",
            connection,
            transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool EmpleadoActivoExiste(
        SqlConnection connection,
        SqlTransaction transaction,
        long idEmpleado,
        out DateTime? fechaIngreso)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1) e.fecha_ingreso
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.id_empleado = @id_empleado
              AND e.activo = 1
              AND ISNULL(ee.codigo_estado_empleado, N'') <> N'RETIRADO'
              AND e.fecha_baja IS NULL;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

        var result = command.ExecuteScalar();
        if (result is null || result == DBNull.Value)
        {
            fechaIngreso = null;
            return false;
        }

        fechaIngreso = Convert.ToDateTime(result);
        return true;
    }

    private static bool ExisteSolapamientoPermiso(
        SqlConnection connection,
        SqlTransaction transaction,
        long idEmpleado,
        DateTime fechaInicio,
        DateTime fechaFin,
        long? currentId)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.solicitud_permiso
            WHERE id_empleado = @id_empleado
              AND estado_permiso IN (N'SOLICITADO', N'APROBADO')
              AND (@id_actual IS NULL OR id_solicitud_permiso <> @id_actual)
              AND fecha_inicio <= @fecha_fin
              AND fecha_fin >= @fecha_inicio;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        command.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = fechaInicio.Date;
        command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = fechaFin.Date;
        command.Parameters.Add("@id_actual", SqlDbType.BigInt).Value =
            currentId.HasValue ? currentId.Value : DBNull.Value;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ExisteSolapamientoVacacion(
        SqlConnection connection,
        SqlTransaction transaction,
        long idEmpleado,
        DateTime fechaInicio,
        DateTime fechaFin,
        long? currentId)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.vacacion
            WHERE id_empleado = @id_empleado
              AND estado_vacacion IN (N'SOLICITADA', N'APROBADA')
              AND (@id_actual IS NULL OR id_vacacion <> @id_actual)
              AND fecha_inicio <= @fecha_fin
              AND fecha_fin >= @fecha_inicio;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        command.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = fechaInicio.Date;
        command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = fechaFin.Date;
        command.Parameters.Add("@id_actual", SqlDbType.BigInt).Value =
            currentId.HasValue ? currentId.Value : DBNull.Value;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ExisteDuplicadoHoraExtra(
        SqlConnection connection,
        SqlTransaction transaction,
        long idEmpleado,
        long idTipoHoraExtra,
        DateTime fechaHoraExtra,
        long? currentId)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.hora_extra
            WHERE id_empleado = @id_empleado
              AND id_tipo_hora_extra = @id_tipo_hora_extra
              AND fecha_hora_extra = @fecha_hora_extra
              AND estado_hora_extra IN (N'REGISTRADA', N'APROBADA')
              AND (@id_actual IS NULL OR id_hora_extra <> @id_actual);
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        command.Parameters.Add("@id_tipo_hora_extra", SqlDbType.BigInt).Value = idTipoHoraExtra;
        command.Parameters.Add("@fecha_hora_extra", SqlDbType.Date).Value = fechaHoraExtra.Date;
        command.Parameters.Add("@id_actual", SqlDbType.BigInt).Value =
            currentId.HasValue ? currentId.Value : DBNull.Value;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static decimal CalculateDaysInclusive(DateTime startDate, DateTime endDate)
    {
        return decimal.Round((decimal)(endDate.Date - startDate.Date).TotalDays + 1m, 2);
    }

    private static string NormalizeWorkflowStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "TODOS";
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "PENDIENTES" => "PENDIENTES",
            "APROBADOS" => "APROBADOS",
            "RECHAZADOS" => "RECHAZADOS",
            _ => "TODOS",
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

    public sealed class PermisoGuardarModel
    {
        public long IdEmpleado { get; set; }
        public long IdTipoPermiso { get; set; }
        public string? FechaSolicitud { get; set; }
        public string FechaInicio { get; set; } = string.Empty;
        public string FechaFin { get; set; } = string.Empty;
        public string? Observacion { get; set; }
        public bool EsMedioDia { get; set; }
        public string? JornadaMedioDia { get; set; }
    }

    public sealed class VacacionGuardarModel
    {
        public long IdEmpleado { get; set; }
        public string? FechaSolicitud { get; set; }
        public string FechaInicio { get; set; } = string.Empty;
        public string FechaFin { get; set; } = string.Empty;
        public string? ObservacionSolicitud { get; set; }
        public bool EsMedioDia { get; set; }
        public string? JornadaMedioDia { get; set; }
    }

    public sealed class VacacionAjusteMasivoModel
    {
        public string FechaAjuste { get; set; } = string.Empty;
        public decimal CantidadDias { get; set; }
        public string? Observacion { get; set; }
    }

    public sealed class HoraExtraGuardarModel
    {
        public long IdEmpleado { get; set; }
        public long IdTipoHoraExtra { get; set; }
        public string FechaHoraExtra { get; set; } = string.Empty;
        public decimal CantidadHoras { get; set; }
        public string? Observacion { get; set; }
    }

    public class WorkflowResolutionModel
    {
        public string Action { get; set; } = string.Empty;
        public string? Observation { get; set; }
    }

    public sealed class VacacionResolutionModel : WorkflowResolutionModel
    {
        public decimal ApprovedDays { get; set; }
    }

    public sealed class PermisoDto
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
        public decimal DiasVacacionesAcumulados { get; set; }
        public decimal DiasVacacionesTomados { get; set; }
        public decimal DiasVacacionesDisponibles { get; set; }
        public decimal DiasVacacionesPendientes { get; set; }
        public decimal DiasPermisosPendientes { get; set; }
        internal string? ObservacionRaw { get; set; }
    }

    public sealed class VacacionDto
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
        public bool EsMedioDia { get; set; }
        public string? JornadaMedioDia { get; set; }
        public string? ObservacionAprobacion { get; set; }
        public string UsuarioSolicita { get; set; } = string.Empty;
        public string? UsuarioAprueba { get; set; }
        public string? FechaAprobacion { get; set; }
        public bool PagadaEnNomina { get; set; }
        public string FechaRegistro { get; set; } = string.Empty;
        public decimal DiasVacacionesAcumulados { get; set; }
        public decimal DiasVacacionesTomados { get; set; }
        public decimal DiasVacacionesDisponibles { get; set; }
        public decimal DiasVacacionesPendientes { get; set; }
        public decimal DiasPermisosPendientes { get; set; }
        internal string? ObservacionSolicitudRaw { get; set; }
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

    private sealed class VacationObservationEnvelope
    {
        public string? TextoSolicitud { get; set; }
        public bool EsMedioDia { get; set; }
        public string? JornadaMedioDia { get; set; }

        public bool IsEnvelope =>
            TextoSolicitud is not null ||
            EsMedioDia ||
            !string.IsNullOrWhiteSpace(JornadaMedioDia);
    }

    private sealed class ActiveEmployeeRow
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
    }

    private sealed class VacationAvailabilityBaseRow
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public DateTime? FechaIngreso { get; set; }
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string NombreEstadoEmpleado { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public string? CodigoTipoContratoVigente { get; set; }
        public string? NombreTipoContratoVigente { get; set; }
    }

    private sealed class ReportBrandingDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string Ruc { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public bool LogoPending { get; set; }
        public string SystemName { get; set; } = string.Empty;
        public string FooterText { get; set; } = string.Empty;
    }

    public sealed class HoraExtraDto
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
