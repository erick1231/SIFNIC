using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class AccionesPersonalController : Controller
{
    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            const string sql = """
                DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
                DECLARE @limite DATE = DATEADD(DAY, 30, @hoy);

                SELECT
                    e.id_empleado,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    d.nombre_departamento,
                    c.nombre_cargo,
                    ee.codigo_estado_empleado,
                    c.id_cargo,
                    c.nivel_jerarquico,
                    contrato_actual.id_contrato,
                    contrato_actual.numero_contrato,
                    contrato_actual.fecha_fin,
                    contrato_actual.salario_base_mensual,
                    contrato_actual.moneda,
                    tc.codigo_tipo_contrato,
                    tc.nombre_tipo_contrato,
                    CASE
                        WHEN contrato_actual.id_contrato IS NULL THEN N'SIN_CONTRATO'
                        WHEN contrato_actual.fecha_fin IS NOT NULL
                             AND contrato_actual.fecha_fin BETWEEN @hoy AND @limite THEN N'POR_VENCER'
                        ELSE N''
                    END AS alerta_contrato
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
                        cc.id_contrato,
                        cc.numero_contrato,
                        cc.id_tipo_contrato,
                        cc.fecha_fin,
                        cc.salario_base_mensual,
                        cc.moneda
                    FROM rrhh.contrato cc
                    WHERE cc.id_empleado = e.id_empleado
                      AND cc.es_contrato_vigente = 1
                    ORDER BY cc.fecha_inicio DESC, cc.id_contrato DESC
                ) contrato_actual
                LEFT JOIN rrhh.tipo_contrato tc
                    ON tc.id_tipo_contrato = contrato_actual.id_tipo_contrato
                WHERE e.activo = 1
                ORDER BY nombre_empleado;

                SELECT
                    c.id_cargo,
                    c.codigo_cargo,
                    c.nombre_cargo,
                    d.id_departamento,
                    d.nombre_departamento,
                    c.nivel_jerarquico
                FROM rrhh.cargo c
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = c.id_departamento
                WHERE c.activo = 1
                ORDER BY c.nivel_jerarquico DESC, c.nombre_cargo;
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
                    positionId = reader.GetInt64(6),
                    hierarchyLevel = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    hierarchyLabel = GetHierarchyLabel(reader.IsDBNull(7) ? 0 : reader.GetInt32(7)),
                    currentContractId = reader.IsDBNull(8) ? (long?)null : reader.GetInt64(8),
                    currentContractNumber = reader.IsDBNull(9) ? null : reader.GetString(9),
                    currentContractEndDate = reader.IsDBNull(10) ? null : reader.GetDateTime(10).ToString("yyyy-MM-dd"),
                    currentSalary = reader.IsDBNull(11) ? (decimal?)null : reader.GetDecimal(11),
                    currentCurrency = reader.IsDBNull(12) ? null : reader.GetString(12),
                    currentContractTypeCode = reader.IsDBNull(13) ? null : reader.GetString(13),
                    currentContractTypeName = reader.IsDBNull(14) ? null : reader.GetString(14),
                    contractAlertCode = reader.IsDBNull(15) ? null : reader.GetString(15),
                    contractAlertLabel = BuildEmployeeContractAlertLabel(
                        reader.IsDBNull(15) ? null : reader.GetString(15),
                        reader.IsDBNull(10) ? null : reader.GetDateTime(10)),
                });
            }

            reader.NextResult();

            var positions = new List<object>();
            while (reader.Read())
            {
                var hierarchyLevel = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                positions.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    name = reader.GetString(2),
                    departmentId = reader.GetInt64(3),
                    department = reader.GetString(4),
                    hierarchyLevel,
                    hierarchyLabel = GetHierarchyLabel(hierarchyLevel),
                });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    employees,
                    positions,
                    actionTypes = GetActionTypeOptions(),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos de acciones de personal.",
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
                SELECT
                    a.id_accion_personal,
                    a.id_empleado,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    e.cedula,
                    d.nombre_departamento,
                    c.nombre_cargo,
                    c.id_cargo,
                    c.nivel_jerarquico,
                    a.tipo_accion,
                    a.fecha_accion,
                    a.descripcion_accion,
                    a.usuario_registro,
                    a.fecha_registro,
                    contrato_actual.id_contrato,
                    contrato_actual.numero_contrato,
                    contrato_actual.fecha_fin,
                    contrato_actual.salario_base_mensual,
                    contrato_actual.moneda
                FROM rrhh.accion_personal a
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = a.id_empleado
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo c
                    ON c.id_cargo = e.id_cargo
                OUTER APPLY
                (
                    SELECT TOP (1)
                        cc.id_contrato,
                        cc.numero_contrato,
                        cc.fecha_fin,
                        cc.salario_base_mensual,
                        cc.moneda
                    FROM rrhh.contrato cc
                    WHERE cc.id_empleado = e.id_empleado
                      AND cc.es_contrato_vigente = 1
                    ORDER BY cc.fecha_inicio DESC, cc.id_contrato DESC
                ) contrato_actual
                WHERE
                    a.tipo_accion <> N'RETIRO'
                    AND
                    (
                        @search = N''
                        OR e.codigo_empleado LIKE N'%' + @search + N'%'
                        OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                        OR a.tipo_accion LIKE N'%' + @search + N'%'
                        OR a.descripcion_accion LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'HOY' AND a.fecha_accion = CAST(GETDATE() AS date))
                        OR (@status = N'30DIAS' AND a.fecha_accion >= DATEADD(day, -30, CAST(GETDATE() AS date)))
                        OR (@status = N'90DIAS' AND a.fecha_accion >= DATEADD(day, -90, CAST(GETDATE() AS date)))
                    )
                ORDER BY a.id_accion_personal DESC;
                """;

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeListStatus(status);

            using var reader = command.ExecuteReader();
            var items = new List<AccionPersonalDto>();
            while (reader.Read())
            {
                items.Add(MapAction(reader));
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
                message = "No se pudo cargar el listado de acciones de personal.",
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

            var item = GetAction(connection, id);
            if (item is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Accion de personal no encontrada.",
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
                message = "No se pudo obtener la accion de personal.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] AccionPersonalSaveModel model)
    {
        var errors = ValidateAction(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos de la accion de personal.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var employee = GetEmployeeSnapshot(connection, model.IdEmpleado, transaction);
            if (employee is null)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "El empleado seleccionado no existe, esta inactivo o ya fue retirado.",
                    errors = new Dictionary<string, string>
                    {
                        ["idEmpleado"] = "Selecciona un empleado activo y vigente.",
                    },
                });
            }

            var newPosition = model.IdCargoNuevo.HasValue
                ? GetPositionSnapshot(connection, model.IdCargoNuevo.Value, transaction)
                : null;

            var operationalErrors = ValidateOperationalRequirements(model, employee, newPosition);
            if (operationalErrors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la accion de personal.",
                    errors = operationalErrors,
                });
            }

            var actionData = BuildActionRecord(model, employee, newPosition);

            long id;
            using (var command = new SqlCommand(
                """
                INSERT INTO rrhh.accion_personal
                (
                    id_empleado,
                    tipo_accion,
                    fecha_accion,
                    descripcion_accion,
                    usuario_registro,
                    fecha_registro
                )
                OUTPUT INSERTED.id_accion_personal
                VALUES
                (
                    @id_empleado,
                    @tipo_accion,
                    @fecha_accion,
                    @descripcion_accion,
                    @usuario_registro,
                    SYSDATETIME()
                );
                """,
                connection,
                transaction))
            {
                ConfigureWriteCommand(command, model, actionData.StoredDescription);
                command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value =
                    RrhhSupport.GetOperatorUser(Request);
                id = Convert.ToInt64(command.ExecuteScalar());
            }

            if (model.AplicarCambioOperativo)
            {
                ApplyOperationalChanges(connection, transaction, model, employee, newPosition);
            }

            var created = GetAction(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "ACCION_PERSONAL",
                "INSERCION",
                created.IdAccionPersonal,
                created.CodigoEmpleado,
                $"Se registro la accion de personal {created.TipoAccion} para {created.CodigoEmpleado}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    accion = created,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Accion de personal creada correctamente.",
                data = created,
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo crear la accion de personal."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo crear la accion de personal.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult Actualizar(long id, [FromBody] AccionPersonalSaveModel model)
    {
        var errors = ValidateAction(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos de la accion de personal.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var previous = GetAction(connection, id, transaction);
            if (previous is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Accion de personal no encontrada.",
                });
            }

            var employee = GetEmployeeSnapshot(connection, model.IdEmpleado, transaction);
            if (employee is null)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "El empleado seleccionado no existe, esta inactivo o ya fue retirado.",
                    errors = new Dictionary<string, string>
                    {
                        ["idEmpleado"] = "Selecciona un empleado activo y vigente.",
                    },
                });
            }

            var newPosition = model.IdCargoNuevo.HasValue
                ? GetPositionSnapshot(connection, model.IdCargoNuevo.Value, transaction)
                : null;

            var operationalErrors = ValidateOperationalRequirements(model, employee, newPosition);
            if (operationalErrors.Count > 0)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos de la accion de personal.",
                    errors = operationalErrors,
                });
            }

            var actionData = BuildActionRecord(model, employee, newPosition);

            using (var command = new SqlCommand(
                """
                UPDATE rrhh.accion_personal
                SET
                    id_empleado = @id_empleado,
                    tipo_accion = @tipo_accion,
                    fecha_accion = @fecha_accion,
                    descripcion_accion = @descripcion_accion
                WHERE id_accion_personal = @id_accion_personal;
                """,
                connection,
                transaction))
            {
                ConfigureWriteCommand(command, model, actionData.StoredDescription);
                command.Parameters.Add("@id_accion_personal", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            if (model.AplicarCambioOperativo)
            {
                ApplyOperationalChanges(connection, transaction, model, employee, newPosition);
            }

            var updated = GetAction(connection, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "ACCION_PERSONAL",
                "MODIFICACION",
                updated.IdAccionPersonal,
                updated.CodigoEmpleado,
                $"Se modifico la accion de personal {updated.TipoAccion} para {updated.CodigoEmpleado}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    anterior = previous,
                    actual = updated,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Accion de personal actualizada correctamente.",
                data = updated,
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                ok = false,
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo actualizar la accion de personal."),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo actualizar la accion de personal.",
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
            var record = GetAction(connection, id, transaction);
            if (record is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Accion de personal no encontrada.",
                });
            }

            using (var command = new SqlCommand(
                "DELETE FROM rrhh.accion_personal WHERE id_accion_personal = @id_accion_personal;",
                connection,
                transaction))
            {
                command.Parameters.Add("@id_accion_personal", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "ACCION_PERSONAL",
                "ELIMINACION",
                record.IdAccionPersonal,
                record.CodigoEmpleado,
                $"Se elimino la accion de personal {record.TipoAccion} para {record.CodigoEmpleado}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    administrador = authorization.UsuarioAdministrador,
                    accion = record,
                },
                authorization.UsuarioAdministrador);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Accion de personal eliminada correctamente.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo eliminar la accion de personal.",
                detail = ex.Message,
            });
        }
    }

    private AccionPersonalDto? GetAction(SqlConnection connection, long id, SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT
                a.id_accion_personal,
                a.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.cedula,
                d.nombre_departamento,
                c.nombre_cargo,
                c.id_cargo,
                c.nivel_jerarquico,
                a.tipo_accion,
                a.fecha_accion,
                a.descripcion_accion,
                a.usuario_registro,
                a.fecha_registro,
                contrato_actual.id_contrato,
                contrato_actual.numero_contrato,
                contrato_actual.fecha_fin,
                contrato_actual.salario_base_mensual,
                contrato_actual.moneda
            FROM rrhh.accion_personal a
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = a.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            OUTER APPLY
            (
                SELECT TOP (1)
                    cc.id_contrato,
                    cc.numero_contrato,
                    cc.fecha_fin,
                    cc.salario_base_mensual,
                    cc.moneda
                FROM rrhh.contrato cc
                WHERE cc.id_empleado = e.id_empleado
                  AND cc.es_contrato_vigente = 1
                ORDER BY cc.fecha_inicio DESC, cc.id_contrato DESC
            ) contrato_actual
            WHERE a.id_accion_personal = @id_accion_personal;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_accion_personal", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAction(reader) : null;
    }

    private static AccionPersonalDto MapAction(SqlDataReader reader)
    {
        var originalDescription = reader.GetString(11);
        var parsed = ParseStoredDescription(originalDescription);
        var metadata = parsed.Metadata;

        var currentHierarchyLevel = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
        var currentContractEndDate = reader.IsDBNull(16)
            ? null
            : reader.GetDateTime(16).ToString("yyyy-MM-dd");
        var currentSalary = reader.IsDBNull(17) ? (decimal?)null : reader.GetDecimal(17);
        var currentCurrency = reader.IsDBNull(18) ? null : reader.GetString(18);

        var dto = new AccionPersonalDto
        {
            IdAccionPersonal = reader.GetInt64(0),
            IdEmpleado = reader.GetInt64(1),
            CodigoEmpleado = reader.GetString(2),
            NombreEmpleado = reader.GetString(3),
            Cedula = reader.GetString(4),
            NombreDepartamento = metadata?.Da ?? reader.GetString(5),
            NombreCargo = metadata?.Ca ?? reader.GetString(6),
            IdCargoActual = reader.GetInt64(7),
            NivelJerarquicoActual = currentHierarchyLevel,
            JerarquiaActual = metadata?.Ha ?? GetHierarchyLabel(currentHierarchyLevel),
            TipoAccion = reader.GetString(9),
            FechaAccion = reader.GetDateTime(10).ToString("yyyy-MM-dd"),
            DescripcionAccion = parsed.Summary,
            DescripcionAccionOriginal = originalDescription,
            UsuarioRegistro = reader.GetString(12),
            FechaRegistro = reader.GetDateTime(13).ToString("yyyy-MM-dd HH:mm:ss"),
            CurrentContractId = reader.IsDBNull(14) ? null : reader.GetInt64(14),
            CurrentContractNumber = reader.IsDBNull(15) ? null : reader.GetString(15),
            FechaFinContratoActual = metadata?.Fa ?? currentContractEndDate,
            SalarioActual = metadata?.Sa ?? currentSalary,
            MonedaSalario = metadata?.Mo ?? currentCurrency ?? "NIO",
            IdCargoNuevo = metadata?.Nc,
            NombreCargoNuevo = metadata?.Cn,
            NombreDepartamentoNuevo = metadata?.Dn,
            JerarquiaNueva = metadata?.Hn,
            NuevoSalarioBaseMensual = metadata?.Sn,
            NuevaFechaFinContrato = metadata?.Fn,
            AplicarCambioOperativo = metadata?.Ap ?? false,
        };

        dto.MemorandumTexto = BuildMemorandumText(dto);
        return dto;
    }

    private static void ConfigureWriteCommand(SqlCommand command, AccionPersonalSaveModel model, string storedDescription)
    {
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = model.IdEmpleado;
        command.Parameters.Add("@tipo_accion", SqlDbType.NVarChar, 50).Value = model.TipoAccion.Trim().ToUpperInvariant();
        command.Parameters.Add("@fecha_accion", SqlDbType.Date).Value = DateTime.Parse(model.FechaAccion);
        command.Parameters.Add("@descripcion_accion", SqlDbType.NVarChar, 500).Value = storedDescription;
    }

    private static Dictionary<string, string> ValidateAction(AccionPersonalSaveModel model)
    {
        var errors = new Dictionary<string, string>();
        var minDate = new DateTime(1753, 1, 1);
        var allowedActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PROMOCION",
            "TRASLADO",
            "CAMBIO SALARIAL",
            "PRORROGA CONTRATO",
            "CAMBIO HORARIO",
        };

        if (model.IdEmpleado <= 0)
        {
            errors["idEmpleado"] = "Selecciona el empleado.";
        }

        if (string.IsNullOrWhiteSpace(model.TipoAccion) ||
            model.TipoAccion.Trim().Length < 3 ||
            model.TipoAccion.Trim().Length > 50 ||
            !Regex.IsMatch(model.TipoAccion.Trim(), "^[A-Za-z0-9ÁÉÍÓÚáéíóúÑñ /_-]+$"))
        {
            errors["tipoAccion"] = "Ingresa un tipo de accion valido.";
        }

        if (!DateTime.TryParse(model.FechaAccion, out var fechaAccion) || fechaAccion < minDate)
        {
            errors["fechaAccion"] = "Ingresa una fecha valida.";
        }

        var tipoAccion = (model.TipoAccion ?? string.Empty).Trim().ToUpperInvariant();
        if (!allowedActionTypes.Contains(tipoAccion))
        {
            errors["tipoAccion"] = "Esta accion no se administra en este modulo. Usa promociones o cambios internos.";
        }

        if ((tipoAccion == "PROMOCION" || tipoAccion == "TRASLADO") && (!model.IdCargoNuevo.HasValue || model.IdCargoNuevo <= 0))
        {
            errors["idCargoNuevo"] = "Selecciona el nuevo cargo.";
        }

        if ((tipoAccion == "PROMOCION" || tipoAccion == "CAMBIO SALARIAL") &&
            (!model.NuevoSalarioBaseMensual.HasValue || model.NuevoSalarioBaseMensual <= 0))
        {
            errors["nuevoSalarioBaseMensual"] = "Ingresa el nuevo salario base.";
        }

        if (tipoAccion == "PRORROGA CONTRATO")
        {
            if (string.IsNullOrWhiteSpace(model.NuevaFechaFinContrato) ||
                !DateTime.TryParse(model.NuevaFechaFinContrato, out var nuevaFechaFin) ||
                nuevaFechaFin < minDate)
            {
                errors["nuevaFechaFinContrato"] = "Ingresa la nueva fecha fin.";
            }
            else if (DateTime.TryParse(model.FechaAccion, out fechaAccion) && nuevaFechaFin < fechaAccion)
            {
                errors["nuevaFechaFinContrato"] = "Debe ser igual o mayor a la fecha de la accion.";
            }
        }

        if (string.IsNullOrWhiteSpace(model.DescripcionAccion) ||
            model.DescripcionAccion.Trim().Length < 5 ||
            model.DescripcionAccion.Trim().Length > 500)
        {
            errors["descripcionAccion"] = "Ingresa una descripcion valida.";
        }

        return errors;
    }

    private static Dictionary<string, string> ValidateOperationalRequirements(
        AccionPersonalSaveModel model,
        EmployeeSnapshot employee,
        PositionSnapshot? newPosition)
    {
        var errors = new Dictionary<string, string>();
        var actionType = (model.TipoAccion ?? string.Empty).Trim().ToUpperInvariant();

        if (DateTime.TryParse(model.FechaAccion, out var actionDate) && actionDate.Date < employee.FechaIngreso.Date)
        {
            errors["fechaAccion"] = $"La fecha de la accion no puede ser menor al ingreso del empleado ({employee.FechaIngreso:dd/MM/yyyy}).";
        }

        if ((actionType == "PROMOCION" || actionType == "TRASLADO") && model.IdCargoNuevo.HasValue && newPosition is null)
        {
            errors["idCargoNuevo"] = "Selecciona un cargo valido.";
        }

        if ((actionType == "PROMOCION" || actionType == "TRASLADO") &&
            model.IdCargoNuevo.HasValue &&
            newPosition is not null &&
            newPosition.IdCargo == employee.IdCargo)
        {
            errors["idCargoNuevo"] = "Selecciona un cargo diferente al actual.";
        }

        if ((actionType == "PROMOCION" || actionType == "CAMBIO SALARIAL" || actionType == "PRORROGA CONTRATO") &&
            employee.CurrentContractId is null)
        {
            var field = actionType == "PRORROGA CONTRATO" ? "nuevaFechaFinContrato" : "nuevoSalarioBaseMensual";
            errors[field] = "El empleado no tiene contrato vigente para aplicar este cambio.";
        }

        if (actionType == "PRORROGA CONTRATO" &&
            employee.CurrentContractEndDate.HasValue &&
            DateTime.TryParse(model.NuevaFechaFinContrato, out var newEndDate) &&
            newEndDate <= employee.CurrentContractEndDate.Value)
        {
            errors["nuevaFechaFinContrato"] = "La nueva fecha fin debe ser mayor a la fecha fin actual del contrato.";
        }

        if (actionType == "PRORROGA CONTRATO" && employee.CurrentContractId.HasValue)
        {
            var typeCode = (employee.CurrentContractTypeCode ?? string.Empty).Trim().ToUpperInvariant();
            var typeName = (employee.CurrentContractTypeName ?? string.Empty).Trim().ToUpperInvariant();
            var isTemporaryContract = typeCode.Contains("TEMP") || typeName.Contains("TEMPORAL");

            if (!isTemporaryContract)
            {
                errors["nuevaFechaFinContrato"] = "La prorroga solo aplica a contratos temporales vigentes.";
            }
        }

        if ((actionType == "PROMOCION" || actionType == "CAMBIO SALARIAL") &&
            model.NuevoSalarioBaseMensual.HasValue &&
            employee.CurrentSalary.HasValue &&
            model.NuevoSalarioBaseMensual.Value == employee.CurrentSalary.Value)
        {
            errors["nuevoSalarioBaseMensual"] = "El nuevo salario debe ser diferente al salario actual.";
        }

        return errors;
    }

    private static bool EmployeeExists(SqlConnection connection, long idEmpleado, SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.id_empleado = @id_empleado
              AND e.activo = 1
              AND e.fecha_baja IS NULL
              AND UPPER(ISNULL(ee.codigo_estado_empleado, N'')) <> N'RETIRADO';
            """;
        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static string NormalizeListStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "TODOS"
            : status.Trim().ToUpperInvariant() switch
            {
                "HOY" => "HOY",
                "30DIAS" => "30DIAS",
                "90DIAS" => "90DIAS",
                _ => "TODOS",
            };
    }

    private static object[] GetActionTypeOptions() =>
        new object[]
        {
            new { value = "PROMOCION", label = "Promocion" },
            new { value = "TRASLADO", label = "Traslado" },
            new { value = "CAMBIO SALARIAL", label = "Cambio salarial" },
            new { value = "PRORROGA CONTRATO", label = "Prorroga de contrato" },
            new { value = "CAMBIO HORARIO", label = "Cambio de horario" },
        };

    private static EmployeeSnapshot? GetEmployeeSnapshot(SqlConnection connection, long idEmpleado, SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.cedula,
                e.fecha_ingreso,
                d.id_departamento,
                d.nombre_departamento,
                c.id_cargo,
                c.nombre_cargo,
                c.nivel_jerarquico,
                contrato_actual.id_contrato,
                contrato_actual.numero_contrato,
                contrato_actual.fecha_fin,
                contrato_actual.salario_base_mensual,
                contrato_actual.moneda,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            OUTER APPLY
            (
                SELECT TOP (1)
                    cc.id_contrato,
                    cc.numero_contrato,
                    cc.id_tipo_contrato,
                    cc.fecha_fin,
                    cc.salario_base_mensual,
                    cc.moneda
                FROM rrhh.contrato cc
                WHERE cc.id_empleado = e.id_empleado
                  AND cc.es_contrato_vigente = 1
                ORDER BY cc.fecha_inicio DESC, cc.id_contrato DESC
            ) contrato_actual
            LEFT JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = contrato_actual.id_tipo_contrato
            WHERE e.id_empleado = @id_empleado
              AND e.activo = 1
              AND e.fecha_baja IS NULL
              AND UPPER(ISNULL(ee.codigo_estado_empleado, N'')) <> N'RETIRADO';
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var hierarchyLevel = reader.IsDBNull(9) ? 0 : reader.GetInt32(9);
        return new EmployeeSnapshot
        {
            IdEmpleado = reader.GetInt64(0),
            CodigoEmpleado = reader.GetString(1),
            NombreEmpleado = reader.GetString(2),
            Cedula = reader.GetString(3),
            FechaIngreso = reader.GetDateTime(4),
            IdDepartamento = reader.GetInt64(5),
            NombreDepartamento = reader.GetString(6),
            IdCargo = reader.GetInt64(7),
            NombreCargo = reader.GetString(8),
            HierarchyLevel = hierarchyLevel,
            HierarchyLabel = GetHierarchyLabel(hierarchyLevel),
            CurrentContractId = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            CurrentContractNumber = reader.IsDBNull(11) ? null : reader.GetString(11),
            CurrentContractEndDate = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            CurrentSalary = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
            CurrentCurrency = reader.IsDBNull(14) ? null : reader.GetString(14),
            CurrentContractTypeCode = reader.IsDBNull(15) ? null : reader.GetString(15),
            CurrentContractTypeName = reader.IsDBNull(16) ? null : reader.GetString(16),
        };
    }

    private static PositionSnapshot? GetPositionSnapshot(SqlConnection connection, long idCargo, SqlTransaction? transaction = null)
    {
        const string sql = """
            SELECT
                c.id_cargo,
                c.nombre_cargo,
                c.nivel_jerarquico,
                d.id_departamento,
                d.nombre_departamento
            FROM rrhh.cargo c
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = c.id_departamento
            WHERE c.id_cargo = @id_cargo
              AND c.activo = 1;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@id_cargo", SqlDbType.BigInt).Value = idCargo;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var hierarchyLevel = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
        return new PositionSnapshot
        {
            IdCargo = reader.GetInt64(0),
            NombreCargo = reader.GetString(1),
            HierarchyLevel = hierarchyLevel,
            HierarchyLabel = GetHierarchyLabel(hierarchyLevel),
            IdDepartamento = reader.GetInt64(3),
            NombreDepartamento = reader.GetString(4),
        };
    }

    private static ActionRecordData BuildActionRecord(
        AccionPersonalSaveModel model,
        EmployeeSnapshot employee,
        PositionSnapshot? newPosition)
    {
        var metadata = new ActionMetadata
        {
            Ca = employee.NombreCargo,
            Da = employee.NombreDepartamento,
            Ha = employee.HierarchyLabel,
            Sa = employee.CurrentSalary,
            Mo = employee.CurrentCurrency,
            Fa = employee.CurrentContractEndDate?.ToString("yyyy-MM-dd"),
            Nc = model.IdCargoNuevo,
            Cn = newPosition?.NombreCargo,
            Dn = newPosition?.NombreDepartamento,
            Hn = newPosition?.HierarchyLabel,
            Sn = model.NuevoSalarioBaseMensual,
            Fn = string.IsNullOrWhiteSpace(model.NuevaFechaFinContrato) ? null : DateTime.Parse(model.NuevaFechaFinContrato).ToString("yyyy-MM-dd"),
            Ap = model.AplicarCambioOperativo,
            Ob = model.DescripcionAccion.Trim(),
        };

        var summary = BuildActionSummary(model, employee, newPosition);
        var storedDescription = BuildStoredDescription(summary, metadata);

        return new ActionRecordData
        {
            Summary = summary,
            Metadata = metadata,
            StoredDescription = storedDescription,
        };
    }

    private static string BuildActionSummary(AccionPersonalSaveModel model, EmployeeSnapshot employee, PositionSnapshot? newPosition)
    {
        var actionType = (model.TipoAccion ?? string.Empty).Trim().ToUpperInvariant();
        var observation = model.DescripcionAccion.Trim();

        return (actionType switch
        {
            "PROMOCION" => $"Promocion a {newPosition?.NombreCargo ?? "nuevo cargo"} con salario {FormatMoneyInline(model.NuevoSalarioBaseMensual ?? employee.CurrentSalary ?? 0m, employee.CurrentCurrency)}. {observation}",
            "TRASLADO" => $"Traslado a {newPosition?.NombreDepartamento ?? employee.NombreDepartamento} / {newPosition?.NombreCargo ?? employee.NombreCargo}. {observation}",
            "CAMBIO SALARIAL" => $"Cambio salarial a {FormatMoneyInline(model.NuevoSalarioBaseMensual ?? employee.CurrentSalary ?? 0m, employee.CurrentCurrency)}. {observation}",
            "PRORROGA CONTRATO" => $"Prorroga de contrato hasta {FormatDateInline(model.NuevaFechaFinContrato)}. {observation}",
            _ => observation,
        }).Trim();
    }

    private static string BuildStoredDescription(string summary, ActionMetadata metadata)
    {
        metadata.Ob = string.IsNullOrWhiteSpace(metadata.Ob) ? null : metadata.Ob.Trim();
        if (metadata.Ob is { Length: > 160 })
        {
            metadata.Ob = metadata.Ob[..160];
        }

        var metaJson = JsonSerializer.Serialize(metadata);
        var maxSummaryLength = Math.Max(0, 500 - " ||META:".Length - metaJson.Length);
        var normalizedSummary = summary.Trim();
        if (normalizedSummary.Length > maxSummaryLength)
        {
            normalizedSummary = normalizedSummary[..maxSummaryLength].TrimEnd();
        }

        return $"{normalizedSummary} ||META:{metaJson}";
    }

    private static ParsedDescription ParseStoredDescription(string? description)
    {
        var raw = description ?? string.Empty;
        var marker = "||META:";
        var markerIndex = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return new ParsedDescription { Summary = raw.Trim(), Metadata = null };
        }

        var summary = raw[..markerIndex].Trim();
        var metaJson = raw[(markerIndex + marker.Length)..].Trim();
        try
        {
            var metadata = JsonSerializer.Deserialize<ActionMetadata>(metaJson);
            return new ParsedDescription { Summary = summary, Metadata = metadata };
        }
        catch
        {
            return new ParsedDescription { Summary = raw.Trim(), Metadata = null };
        }
    }

    private static void ApplyOperationalChanges(
        SqlConnection connection,
        SqlTransaction transaction,
        AccionPersonalSaveModel model,
        EmployeeSnapshot employee,
        PositionSnapshot? newPosition)
    {
        var actionType = (model.TipoAccion ?? string.Empty).Trim().ToUpperInvariant();

        if ((actionType == "PROMOCION" || actionType == "TRASLADO") && newPosition is not null)
        {
            using var command = new SqlCommand(
                """
                UPDATE rrhh.empleado
                SET
                    id_cargo = @id_cargo,
                    id_departamento = @id_departamento
                WHERE id_empleado = @id_empleado;
                """,
                connection,
                transaction);
            command.Parameters.Add("@id_cargo", SqlDbType.BigInt).Value = newPosition.IdCargo;
            command.Parameters.Add("@id_departamento", SqlDbType.BigInt).Value = newPosition.IdDepartamento;
            command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = employee.IdEmpleado;
            command.ExecuteNonQuery();
        }

        if ((actionType == "PROMOCION" || actionType == "CAMBIO SALARIAL") &&
            employee.CurrentContractId.HasValue &&
            model.NuevoSalarioBaseMensual.HasValue)
        {
            using var command = new SqlCommand(
                """
                UPDATE rrhh.contrato
                SET salario_base_mensual = @salario_base_mensual
                WHERE id_contrato = @id_contrato;
                """,
                connection,
                transaction);
            command.Parameters.Add("@salario_base_mensual", SqlDbType.Decimal).Value = model.NuevoSalarioBaseMensual.Value;
            command.Parameters["@salario_base_mensual"].Precision = 18;
            command.Parameters["@salario_base_mensual"].Scale = 2;
            command.Parameters.Add("@id_contrato", SqlDbType.BigInt).Value = employee.CurrentContractId.Value;
            command.ExecuteNonQuery();
        }

        if (actionType == "PRORROGA CONTRATO" &&
            employee.CurrentContractId.HasValue &&
            !string.IsNullOrWhiteSpace(model.NuevaFechaFinContrato))
        {
            using var command = new SqlCommand(
                """
                UPDATE rrhh.contrato
                SET
                    fecha_fin = @fecha_fin,
                    es_contrato_vigente = 1
                WHERE id_contrato = @id_contrato;
                """,
                connection,
                transaction);
            command.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = DateTime.Parse(model.NuevaFechaFinContrato);
            command.Parameters.Add("@id_contrato", SqlDbType.BigInt).Value = employee.CurrentContractId.Value;
            command.ExecuteNonQuery();
        }
    }

    private static string GetHierarchyLabel(int level) =>
        level switch
        {
            >= 9 => "Gerencia",
            >= 8 => "Jefatura",
            >= 7 => "Coordinacion",
            >= 6 => "Especialista / Analista",
            >= 5 => "Operacion",
            > 0 => "Apoyo",
            _ => "Sin jerarquia definida",
        };

    private static string BuildEmployeeContractAlertLabel(string? alertCode, DateTime? endDate)
    {
        var normalized = (alertCode ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "SIN_CONTRATO" => "Sin contrato vigente",
            "POR_VENCER" when endDate.HasValue => $"Por vencer {endDate.Value:dd/MM/yyyy}",
            "POR_VENCER" => "Por vencer",
            _ => string.Empty,
        };
    }

    private static string BuildMemorandumText(AccionPersonalDto record)
    {
        var actionType = (record.TipoAccion ?? string.Empty).Trim().ToUpperInvariant();
        var lines = new List<string>
        {
            $"Por medio del presente se notifica a {record.NombreEmpleado} ({record.CodigoEmpleado}) la accion de personal registrada con fecha {FormatDateInline(record.FechaAccion)}."
        };

        if (!string.IsNullOrWhiteSpace(record.NombreCargoNuevo))
        {
            lines.Add($"Nuevo puesto: {record.NombreCargoNuevo}.");
        }

        if (!string.IsNullOrWhiteSpace(record.JerarquiaNueva))
        {
            lines.Add($"Jerarquia asignada: {record.JerarquiaNueva}.");
        }

        if (record.NuevoSalarioBaseMensual.HasValue)
        {
            lines.Add($"Nuevo salario base mensual: {FormatMoneyInline(record.NuevoSalarioBaseMensual.Value, record.MonedaSalario)}.");
        }

        if (!string.IsNullOrWhiteSpace(record.NuevaFechaFinContrato))
        {
            lines.Add($"Vigencia actualizada del contrato hasta {FormatDateInline(record.NuevaFechaFinContrato)}.");
        }

        lines.Add(actionType switch
        {
            "PROMOCION" => "Esta notificacion sirve como memorandum interno de promocion.",
            "TRASLADO" => "Esta notificacion sirve como memorandum interno de traslado.",
            "CAMBIO SALARIAL" => "Esta notificacion sirve como memorandum interno de cambio salarial.",
            "PRORROGA CONTRATO" => "Esta notificacion sirve como memorandum interno de prorroga contractual.",
            _ => "Esta notificacion sirve como memorandum interno del movimiento registrado.",
        });

        if (!string.IsNullOrWhiteSpace(record.DescripcionAccion))
        {
            lines.Add($"Observacion: {record.DescripcionAccion}");
        }

        return string.Join(" ", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string FormatMoneyInline(decimal amount, string? currency)
    {
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? "NIO" : currency.Trim().ToUpperInvariant();
        return string.Create(CultureInfo.InvariantCulture, $"{normalizedCurrency} {amount:0.00}");
    }

    private static string FormatDateInline(string? isoDate)
    {
        if (!DateTime.TryParse(isoDate, out var date))
        {
            return isoDate ?? "-";
        }

        return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    public sealed class AccionPersonalSaveModel
    {
        public long IdEmpleado { get; set; }
        public string TipoAccion { get; set; } = string.Empty;
        public string FechaAccion { get; set; } = string.Empty;
        public long? IdCargoNuevo { get; set; }
        public decimal? NuevoSalarioBaseMensual { get; set; }
        public string? NuevaFechaFinContrato { get; set; }
        public bool AplicarCambioOperativo { get; set; } = true;
        public string DescripcionAccion { get; set; } = string.Empty;
    }

    public sealed class DeleteRequest
    {
        public string AdminUsuario { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    public sealed class AccionPersonalDto
    {
        public long IdAccionPersonal { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public long IdCargoActual { get; set; }
        public int NivelJerarquicoActual { get; set; }
        public string JerarquiaActual { get; set; } = string.Empty;
        public string TipoAccion { get; set; } = string.Empty;
        public string FechaAccion { get; set; } = string.Empty;
        public string DescripcionAccion { get; set; } = string.Empty;
        public string DescripcionAccionOriginal { get; set; } = string.Empty;
        public string UsuarioRegistro { get; set; } = string.Empty;
        public string FechaRegistro { get; set; } = string.Empty;
        public long? CurrentContractId { get; set; }
        public string? CurrentContractNumber { get; set; }
        public decimal? SalarioActual { get; set; }
        public string MonedaSalario { get; set; } = "NIO";
        public string? FechaFinContratoActual { get; set; }
        public long? IdCargoNuevo { get; set; }
        public string? NombreCargoNuevo { get; set; }
        public string? NombreDepartamentoNuevo { get; set; }
        public string? JerarquiaNueva { get; set; }
        public decimal? NuevoSalarioBaseMensual { get; set; }
        public string? NuevaFechaFinContrato { get; set; }
        public bool AplicarCambioOperativo { get; set; }
        public string MemorandumTexto { get; set; } = string.Empty;
    }

    private sealed class EmployeeSnapshot
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string Cedula { get; set; } = string.Empty;
        public DateTime FechaIngreso { get; set; }
        public long IdDepartamento { get; set; }
        public string NombreDepartamento { get; set; } = string.Empty;
        public long IdCargo { get; set; }
        public string NombreCargo { get; set; } = string.Empty;
        public int HierarchyLevel { get; set; }
        public string HierarchyLabel { get; set; } = string.Empty;
        public long? CurrentContractId { get; set; }
        public string? CurrentContractNumber { get; set; }
        public DateTime? CurrentContractEndDate { get; set; }
        public decimal? CurrentSalary { get; set; }
        public string? CurrentCurrency { get; set; }
        public string? CurrentContractTypeCode { get; set; }
        public string? CurrentContractTypeName { get; set; }
    }

    private sealed class PositionSnapshot
    {
        public long IdCargo { get; set; }
        public string NombreCargo { get; set; } = string.Empty;
        public int HierarchyLevel { get; set; }
        public string HierarchyLabel { get; set; } = string.Empty;
        public long IdDepartamento { get; set; }
        public string NombreDepartamento { get; set; } = string.Empty;
    }

    private sealed class ActionRecordData
    {
        public string Summary { get; set; } = string.Empty;
        public string StoredDescription { get; set; } = string.Empty;
        public ActionMetadata Metadata { get; set; } = new();
    }

    private sealed class ParsedDescription
    {
        public string Summary { get; set; } = string.Empty;
        public ActionMetadata? Metadata { get; set; }
    }

    private sealed class ActionMetadata
    {
        public string? Ca { get; set; }
        public string? Da { get; set; }
        public string? Ha { get; set; }
        public decimal? Sa { get; set; }
        public string? Mo { get; set; }
        public string? Fa { get; set; }
        public long? Nc { get; set; }
        public string? Cn { get; set; }
        public string? Dn { get; set; }
        public string? Hn { get; set; }
        public decimal? Sn { get; set; }
        public string? Fn { get; set; }
        public bool Ap { get; set; }
        public string? Ob { get; set; }
    }
}
