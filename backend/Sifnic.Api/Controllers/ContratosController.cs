using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Security;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class ContratosController : Controller
{
    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            const string sql = """
                DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
                DECLARE @limite DATE = DATEADD(DAY, 30, @hoy);

                SELECT
                    e.id_empleado,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_completo,
                    d.nombre_departamento,
                    c.nombre_cargo,
                    e.fecha_ingreso,
                    contrato_actual.id_contrato,
                    contrato_actual.numero_contrato,
                    contrato_actual.fecha_fin,
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
                OUTER APPLY
                (
                    SELECT TOP (1)
                        cc.id_contrato,
                        cc.numero_contrato,
                        cc.id_tipo_contrato,
                        cc.fecha_fin
                    FROM rrhh.contrato cc
                    WHERE cc.id_empleado = e.id_empleado
                      AND cc.es_contrato_vigente = 1
                    ORDER BY cc.fecha_inicio DESC, cc.id_contrato DESC
                ) contrato_actual
                LEFT JOIN rrhh.tipo_contrato tc
                    ON tc.id_tipo_contrato = contrato_actual.id_tipo_contrato
                WHERE
                    e.activo = 1
                    AND
                    (
                        contrato_actual.id_contrato IS NULL
                        OR
                        (
                            contrato_actual.fecha_fin IS NOT NULL
                            AND contrato_actual.fecha_fin BETWEEN @hoy AND @limite
                        )
                    )
                ORDER BY
                    CASE WHEN contrato_actual.id_contrato IS NULL THEN 0 ELSE 1 END,
                    nombre_completo;

                SELECT
                    id_tipo_contrato,
                    codigo_tipo_contrato,
                    nombre_tipo_contrato
                FROM rrhh.tipo_contrato
                WHERE activo = 1
                ORDER BY nombre_tipo_contrato;

                SELECT
                    id_horario_laboral,
                    codigo_horario,
                    nombre_horario,
                    horas_semanales,
                    horas_diarias
                FROM rrhh.horario_laboral
                WHERE activo = 1
                ORDER BY nombre_horario;

                SELECT TOP (1) moneda_base
                FROM empresa.empresa
                WHERE activo = 1
                ORDER BY id_empresa;
                """;

            using var comando = new SqlCommand(sql, conexion);
            using var reader = comando.ExecuteReader();

            var empleados = new List<object>();
            while (reader.Read())
            {
                empleados.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                    department = reader.GetString(3),
                    position = reader.GetString(4),
                    startDate = reader.GetDateTime(5).ToString("yyyy-MM-dd"),
                    currentContractId = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6),
                    currentContractNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                    currentContractEndDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToString("yyyy-MM-dd"),
                    currentContractTypeCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                    currentContractTypeName = reader.IsDBNull(10) ? null : reader.GetString(10),
                    contractAlertCode = reader.IsDBNull(11) ? null : reader.GetString(11),
                    contractAlertLabel = BuildEmployeeContractAlertLabel(
                        reader.IsDBNull(11) ? null : reader.GetString(11),
                        reader.IsDBNull(8) ? null : reader.GetDateTime(8)),
                });
            }

            reader.NextResult();

            var tiposContrato = new List<object>();
            while (reader.Read())
            {
                tiposContrato.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                });
            }

            reader.NextResult();

            var horarios = new List<object>();
            while (reader.Read())
            {
                horarios.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                    weeklyHours = reader.GetDecimal(3),
                    dailyHours = reader.GetDecimal(4),
                });
            }

            reader.NextResult();

            var monedaBase = "NIO";
            if (reader.Read() && !reader.IsDBNull(0))
            {
                monedaBase = reader.GetString(0).Trim().ToUpperInvariant();
            }

            var monedas = new[]
            {
                monedaBase,
                "USD",
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => new
            {
                value,
                label = value == "NIO" ? "Cordoba (NIO)" : value == "USD" ? "Dolar (USD)" : value,
            });

            return Json(new
            {
                ok = true,
                data = new
                {
                    employees = empleados,
                    contractTypes = tiposContrato,
                    schedules = horarios,
                    currencies = monedas,
                    defaultCurrency = monedaBase,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos de contratos.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult SugerirNumero(long idEmpleado, long? ignorarIdContrato = null)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var codigoEmpleado = ObtenerCodigoEmpleado(conexion, idEmpleado);
            if (string.IsNullOrWhiteSpace(codigoEmpleado))
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Empleado no encontrado para generar el numero de contrato.",
                });
            }

            var numero = SugerirNumeroContrato(conexion, codigoEmpleado, ignorarIdContrato);

            return Json(new
            {
                ok = true,
                data = new
                {
                    numeroContrato = numero,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo sugerir el numero de contrato.",
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

            const string sql = """
                DECLARE @hoy DATE = CAST(GETDATE() AS DATE);
                DECLARE @limite DATE = DATEADD(DAY, 30, @hoy);

                SELECT
                    c.id_contrato,
                    c.id_empleado,
                    e.codigo_empleado,
                    COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                    e.cedula,
                    d.nombre_departamento,
                    cg.nombre_cargo,
                    e.fecha_ingreso,
                    c.id_tipo_contrato,
                    tc.codigo_tipo_contrato,
                    tc.nombre_tipo_contrato,
                    c.id_horario_laboral,
                    h.codigo_horario,
                    h.nombre_horario,
                    h.horas_semanales,
                    h.horas_diarias,
                    c.numero_contrato,
                    c.fecha_inicio,
                    c.fecha_fin,
                    c.salario_base_mensual,
                    c.moneda,
                    c.es_contrato_vigente,
                    c.observacion,
                    c.fecha_registro,
                    CASE
                        WHEN UPPER(COALESCE(tc.codigo_tipo_contrato, N'')) LIKE N'%TEMPORAL%'
                             OR UPPER(COALESCE(tc.nombre_tipo_contrato, N'')) LIKE N'%TEMPORAL%'
                             OR UPPER(COALESCE(tc.nombre_tipo_contrato, N'')) LIKE N'%PLAZO%' THEN CAST(1 AS bit)
                        ELSE CAST(0 AS bit)
                    END AS es_temporal,
                    CASE
                        WHEN c.es_contrato_vigente = 1
                             AND c.fecha_fin IS NOT NULL
                             AND c.fecha_fin BETWEEN @hoy AND @limite THEN DATEDIFF(DAY, @hoy, c.fecha_fin)
                        ELSE NULL
                    END AS dias_para_vencer
                FROM rrhh.contrato c
                INNER JOIN rrhh.empleado e
                    ON e.id_empleado = c.id_empleado
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = e.id_departamento
                INNER JOIN rrhh.cargo cg
                    ON cg.id_cargo = e.id_cargo
                INNER JOIN rrhh.tipo_contrato tc
                    ON tc.id_tipo_contrato = c.id_tipo_contrato
                INNER JOIN rrhh.horario_laboral h
                    ON h.id_horario_laboral = c.id_horario_laboral
                WHERE
                    (
                        @search = N''
                        OR c.numero_contrato LIKE N'%' + @search + N'%'
                        OR e.codigo_empleado LIKE N'%' + @search + N'%'
                        OR COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) LIKE N'%' + @search + N'%'
                        OR tc.nombre_tipo_contrato LIKE N'%' + @search + N'%'
                        OR h.nombre_horario LIKE N'%' + @search + N'%'
                    )
                ORDER BY c.id_contrato DESC;
                """;

            using var comando = new SqlCommand(sql, conexion);
            comando.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value =
                (search ?? string.Empty).Trim();
            var normalizedStatus = NormalizarEstadoContrato(status);

            using var reader = comando.ExecuteReader();
            var items = new List<ContratoDto>();
            while (reader.Read())
            {
                items.Add(MapearContrato(reader));
            }

            items = items
                .Where(item => CumpleEstadoContrato(item, normalizedStatus))
                .ToList();

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
                message = "No se pudo cargar el listado de contratos.",
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

            var contrato = ObtenerContratoInterno(conexion, id);
            if (contrato is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Contrato no encontrado.",
                });
            }

            return Json(new
            {
                ok = true,
                data = contrato,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo obtener el contrato.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] ContratoGuardarModel model)
    {
        var errores = ValidarContrato(model);
        if (errores.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del contrato.",
                errors = errores,
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            using var transaccion = conexion.BeginTransaction();

            ValidarDependenciasContrato(conexion, transaccion, model, errores, null);
            if (errores.Count > 0)
            {
                transaccion.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos del contrato.",
                    errors = errores,
                });
            }

            if (NumeroContratoExiste(conexion, transaccion, model.NumeroContrato, null))
            {
                transaccion.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos del contrato.",
                    errors = new Dictionary<string, string>
                    {
                        ["numeroContrato"] = "El numero de contrato ya existe.",
                    },
                });
            }

            if (model.EsContratoVigente)
            {
                ActualizarVigenciaOtrosContratos(conexion, transaccion, model.IdEmpleado, null);
            }

            long idContrato;
            using (var comando = new SqlCommand(
                """
                INSERT INTO rrhh.contrato
                (
                    id_empleado,
                    id_tipo_contrato,
                    id_horario_laboral,
                    numero_contrato,
                    fecha_inicio,
                    fecha_fin,
                    salario_base_mensual,
                    moneda,
                    es_contrato_vigente,
                    observacion
                )
                OUTPUT INSERTED.id_contrato
                VALUES
                (
                    @id_empleado,
                    @id_tipo_contrato,
                    @id_horario_laboral,
                    @numero_contrato,
                    @fecha_inicio,
                    @fecha_fin,
                    @salario_base_mensual,
                    @moneda,
                    @es_contrato_vigente,
                    @observacion
                );
                """,
                conexion,
                transaccion))
            {
                AsignarParametrosContrato(comando, model);
                idContrato = Convert.ToInt64(comando.ExecuteScalar());
            }

            var contrato = ObtenerContratoInterno(conexion, idContrato, transaccion)!;

            RegistrarBitacora(
                conexion,
                transaccion,
                "INSERCION",
                contrato.IdContrato,
                contrato.NumeroContrato,
                $"Se registro el contrato {contrato.NumeroContrato} para el empleado {contrato.CodigoEmpleado}.",
                new
                {
                    contrato.IdContrato,
                    contrato.NumeroContrato,
                    contrato.CodigoEmpleado,
                    contrato.NombreEmpleado,
                    contrato.NombreTipoContrato,
                    contrato.FechaInicio,
                    contrato.FechaFin,
                    contrato.SalarioBaseMensual,
                    contrato.Moneda,
                    contrato.EsContratoVigente,
                });

            transaccion.Commit();

            return Json(new
            {
                ok = true,
                message = "Contrato registrado correctamente.",
                data = contrato,
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
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
                message = "No se pudo registrar el contrato.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult Actualizar(long id, [FromBody] ContratoGuardarModel model)
    {
        var errores = ValidarContrato(model);
        if (errores.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del contrato.",
                errors = errores,
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();
            using var transaccion = conexion.BeginTransaction();

            var actual = ObtenerContratoInterno(conexion, id, transaccion);
            if (actual is null)
            {
                transaccion.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = "Contrato no encontrado.",
                });
            }

            ValidarDependenciasContrato(conexion, transaccion, model, errores, id);
            if (errores.Count > 0)
            {
                transaccion.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos del contrato.",
                    errors = errores,
                });
            }

            if (NumeroContratoExiste(conexion, transaccion, model.NumeroContrato, id))
            {
                transaccion.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = "Corrige los datos del contrato.",
                    errors = new Dictionary<string, string>
                    {
                        ["numeroContrato"] = "El numero de contrato ya existe.",
                    },
                });
            }

            if (model.EsContratoVigente)
            {
                ActualizarVigenciaOtrosContratos(conexion, transaccion, model.IdEmpleado, id);
            }

            using (var comando = new SqlCommand(
                """
                UPDATE rrhh.contrato
                SET
                    id_empleado = @id_empleado,
                    id_tipo_contrato = @id_tipo_contrato,
                    id_horario_laboral = @id_horario_laboral,
                    numero_contrato = @numero_contrato,
                    fecha_inicio = @fecha_inicio,
                    fecha_fin = @fecha_fin,
                    salario_base_mensual = @salario_base_mensual,
                    moneda = @moneda,
                    es_contrato_vigente = @es_contrato_vigente,
                    observacion = @observacion
                WHERE id_contrato = @id_contrato;
                """,
                conexion,
                transaccion))
            {
                AsignarParametrosContrato(comando, model);
                comando.Parameters.Add("@id_contrato", SqlDbType.BigInt).Value = id;
                comando.ExecuteNonQuery();
            }

            var actualizado = ObtenerContratoInterno(conexion, id, transaccion)!;

            RegistrarBitacora(
                conexion,
                transaccion,
                "MODIFICACION",
                actualizado.IdContrato,
                actualizado.NumeroContrato,
                $"Se actualizo el contrato {actualizado.NumeroContrato} del empleado {actualizado.CodigoEmpleado}.",
                new
                {
                    antes = new
                    {
                        actual.IdEmpleado,
                        actual.IdTipoContrato,
                        actual.IdHorarioLaboral,
                        actual.NumeroContrato,
                        actual.FechaInicio,
                        actual.FechaFin,
                        actual.SalarioBaseMensual,
                        actual.Moneda,
                        actual.EsContratoVigente,
                    },
                    despues = new
                    {
                        actualizado.IdEmpleado,
                        actualizado.IdTipoContrato,
                        actualizado.IdHorarioLaboral,
                        actualizado.NumeroContrato,
                        actualizado.FechaInicio,
                        actualizado.FechaFin,
                        actualizado.SalarioBaseMensual,
                        actualizado.Moneda,
                        actualizado.EsContratoVigente,
                    },
                });

            transaccion.Commit();

            return Json(new
            {
                ok = true,
                message = "Contrato actualizado correctamente.",
                data = actualizado,
            });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
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
                message = "No se pudo actualizar el contrato.",
                detail = ex.Message,
            });
        }
    }

    [HttpDelete("{id:long}")]
    public IActionResult Eliminar(long id, [FromBody] ContratoEliminarModel model)
    {
        if (string.IsNullOrWhiteSpace(model.AdminUsuario) || string.IsNullOrWhiteSpace(model.AdminPassword))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa usuario y contrasena de administrador.",
            });
        }

        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contrato = ObtenerContratoInterno(conexion, id);
            if (contrato is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Contrato no encontrado.",
                });
            }

            var admin = ValidarAdministrador(conexion, model.AdminUsuario, model.AdminPassword);
            if (!admin.Ok)
            {
                return StatusCode(403, new
                {
                    ok = false,
                    message = admin.Message,
                });
            }

            var referencias = ObtenerReferenciasContrato(conexion, id);
            if (referencias.Count > 0)
            {
                return StatusCode(409, new
                {
                    ok = false,
                    message = "No se puede eliminar el contrato porque tiene registros relacionados.",
                    data = referencias,
                });
            }

            using var transaccion = conexion.BeginTransaction();

            using (var comando = new SqlCommand(
                """
                DELETE FROM rrhh.contrato
                WHERE id_contrato = @id_contrato;
                """,
                conexion,
                transaccion))
            {
                comando.Parameters.Add("@id_contrato", SqlDbType.BigInt).Value = id;
                comando.ExecuteNonQuery();
            }

            RegistrarBitacora(
                conexion,
                transaccion,
                "ELIMINACION",
                contrato.IdContrato,
                contrato.NumeroContrato,
                $"Se elimino el contrato {contrato.NumeroContrato} del empleado {contrato.CodigoEmpleado}.",
                new
                {
                    contrato.IdContrato,
                    contrato.NumeroContrato,
                    contrato.CodigoEmpleado,
                    contrato.NombreEmpleado,
                    contrato.NombreTipoContrato,
                    usuarioAdministrador = admin.UsuarioAdministrador,
                },
                admin.UsuarioAdministrador);

            transaccion.Commit();

            return Json(new
            {
                ok = true,
                message = "Contrato eliminado correctamente.",
                data = new
                {
                    contrato.IdContrato,
                    contrato.NumeroContrato,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo eliminar el contrato.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet("{id:long}")]
    public IActionResult Documento(long id)
    {
        try
        {
            using var conexion = new SqlConnection(ConexionDb.Cadena);
            conexion.Open();

            var contrato = ObtenerContratoInterno(conexion, id);
            if (contrato is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Contrato no encontrado.",
                });
            }

            var empresa = ObtenerContextoEmpresa(conexion);

            RegistrarBitacora(
                conexion,
                null,
                "IMPRESION",
                contrato.IdContrato,
                contrato.NumeroContrato,
                $"Se genero la impresion del contrato {contrato.NumeroContrato}.",
                new
                {
                    contrato.IdContrato,
                    contrato.NumeroContrato,
                    contrato.CodigoEmpleado,
                    contrato.NombreEmpleado,
                });

            return Json(new
            {
                ok = true,
                data = new
                {
                    company = empresa,
                    contract = contrato,
                    generatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo preparar el documento del contrato.",
                detail = ex.Message,
            });
        }
    }

    private static Dictionary<string, string> ValidarContrato(ContratoGuardarModel model)
    {
        var errores = new Dictionary<string, string>();
        var fechaMinimaBase = new DateTime(1753, 1, 1);

        if (model.IdEmpleado <= 0)
        {
            errores["idEmpleado"] = "Selecciona el empleado.";
        }

        if (model.IdTipoContrato <= 0)
        {
            errores["idTipoContrato"] = "Selecciona el tipo de contrato.";
        }

        if (model.IdHorarioLaboral <= 0)
        {
            errores["idHorarioLaboral"] = "Selecciona el horario laboral.";
        }

        if (string.IsNullOrWhiteSpace(model.NumeroContrato) ||
            !Regex.IsMatch(model.NumeroContrato.Trim().ToUpperInvariant(), "^[A-Z0-9-]{4,100}$"))
        {
            errores["numeroContrato"] = "Numero de contrato invalido.";
        }

        if (!DateTime.TryParse(model.FechaInicio, out var fechaInicio))
        {
            errores["fechaInicio"] = "Ingresa la fecha de inicio.";
        }
        else if (fechaInicio.Date < fechaMinimaBase)
        {
            errores["fechaInicio"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }

        DateTime fechaFin;
        if (!string.IsNullOrWhiteSpace(model.FechaFin))
        {
            if (!DateTime.TryParse(model.FechaFin, out fechaFin))
            {
                errores["fechaFin"] = "Fecha fin invalida.";
            }
            else if (fechaFin.Date < fechaMinimaBase)
            {
                errores["fechaFin"] = "Ingresa una fecha igual o mayor a 01/01/1753.";
            }
            else if (DateTime.TryParse(model.FechaInicio, out var inicioValido) &&
                     fechaFin.Date < inicioValido.Date)
            {
                errores["fechaFin"] = "La fecha fin debe ser igual o mayor a la fecha de inicio.";
            }
        }
        else if (!model.EsContratoVigente)
        {
            errores["fechaFin"] = "Ingresa la fecha fin si el contrato no esta vigente.";
        }

        if (model.SalarioBaseMensual <= 0)
        {
            errores["salarioBaseMensual"] = "Ingresa un salario base valido.";
        }

        if (string.IsNullOrWhiteSpace(model.Moneda) ||
            !Regex.IsMatch(model.Moneda.Trim().ToUpperInvariant(), "^[A-Z]{3,20}$"))
        {
            errores["moneda"] = "Moneda invalida.";
        }

        if (!string.IsNullOrWhiteSpace(model.Observacion) && model.Observacion.Trim().Length > 1000)
        {
            errores["observacion"] = "La observacion supera el limite permitido.";
        }

        return errores;
    }

    private void ValidarDependenciasContrato(
        SqlConnection conexion,
        SqlTransaction transaccion,
        ContratoGuardarModel model,
        Dictionary<string, string> errores,
        long? idContratoActual)
    {
        var empleado = ObtenerEmpleadoResumen(conexion, transaccion, model.IdEmpleado);
        if (empleado is null)
        {
            errores["idEmpleado"] = "El empleado seleccionado no existe, esta inactivo o ya fue retirado.";
        }
        else if (DateTime.TryParse(model.FechaInicio, out var fechaInicio) &&
                 fechaInicio.Date < empleado.FechaIngreso.Date)
        {
            errores["fechaInicio"] = $"La fecha de inicio no puede ser menor al ingreso del empleado ({empleado.FechaIngreso:dd/MM/yyyy}).";
        }

        if (empleado is not null &&
            DateTime.TryParse(model.FechaInicio, out var inicioContrato) &&
            DateTime.TryParse(model.FechaFin, out var finContrato) &&
            ExisteSolapamientoContrato(conexion, transaccion, model.IdEmpleado, inicioContrato.Date, finContrato.Date, idContratoActual))
        {
            errores["fechaInicio"] = "El empleado ya tiene otro contrato en ese rango de fechas.";
        }
        else if (empleado is not null &&
                 DateTime.TryParse(model.FechaInicio, out inicioContrato) &&
                 string.IsNullOrWhiteSpace(model.FechaFin) &&
                 ExisteSolapamientoContrato(conexion, transaccion, model.IdEmpleado, inicioContrato.Date, null, idContratoActual))
        {
            errores["fechaInicio"] = "El empleado ya tiene otro contrato que se cruza con esa fecha de inicio.";
        }

        if (!ExisteCatalogo(conexion, transaccion, "rrhh.tipo_contrato", "id_tipo_contrato", model.IdTipoContrato))
        {
            errores["idTipoContrato"] = "El tipo de contrato seleccionado no existe.";
        }

        if (!ExisteCatalogo(conexion, transaccion, "rrhh.horario_laboral", "id_horario_laboral", model.IdHorarioLaboral))
        {
            errores["idHorarioLaboral"] = "El horario laboral seleccionado no existe.";
        }

    }

    private static bool ExisteCatalogo(
        SqlConnection conexion,
        SqlTransaction transaccion,
        string tabla,
        string columnaId,
        long id)
    {
        using var comando = new SqlCommand(
            $"SELECT COUNT(1) FROM {tabla} WHERE {columnaId} = @id AND activo = 1;",
            conexion,
            transaccion);
        comando.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        return Convert.ToInt32(comando.ExecuteScalar()) > 0;
    }

    private static void ActualizarVigenciaOtrosContratos(
        SqlConnection conexion,
        SqlTransaction transaccion,
        long idEmpleado,
        long? excluirIdContrato)
    {
        using var comando = new SqlCommand(
            """
            UPDATE rrhh.contrato
            SET es_contrato_vigente = 0
            WHERE id_empleado = @id_empleado
              AND (@id_contrato IS NULL OR id_contrato <> @id_contrato)
              AND es_contrato_vigente = 1;
            """,
            conexion,
            transaccion);
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        comando.Parameters.Add("@id_contrato", SqlDbType.BigInt).Value =
            excluirIdContrato.HasValue ? excluirIdContrato.Value : DBNull.Value;
        comando.ExecuteNonQuery();
    }

    private ContratoDto? ObtenerContratoInterno(SqlConnection conexion, long id, SqlTransaction? transaccion = null)
    {
        const string sql = """
            SELECT
                c.id_contrato,
                c.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                e.cedula,
                d.nombre_departamento,
                cg.nombre_cargo,
                e.fecha_ingreso,
                c.id_tipo_contrato,
                tc.codigo_tipo_contrato,
                tc.nombre_tipo_contrato,
                c.id_horario_laboral,
                h.codigo_horario,
                h.nombre_horario,
                h.horas_semanales,
                h.horas_diarias,
                c.numero_contrato,
                c.fecha_inicio,
                c.fecha_fin,
                c.salario_base_mensual,
                c.moneda,
                c.es_contrato_vigente,
                c.observacion,
                c.fecha_registro,
                CASE
                    WHEN UPPER(COALESCE(tc.codigo_tipo_contrato, N'')) LIKE N'%TEMPORAL%'
                         OR UPPER(COALESCE(tc.nombre_tipo_contrato, N'')) LIKE N'%TEMPORAL%'
                         OR UPPER(COALESCE(tc.nombre_tipo_contrato, N'')) LIKE N'%PLAZO%' THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS es_temporal,
                CASE
                    WHEN c.es_contrato_vigente = 1
                         AND c.fecha_fin IS NOT NULL
                         AND c.fecha_fin BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 30, CAST(GETDATE() AS DATE))
                        THEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), c.fecha_fin)
                    ELSE NULL
                END AS dias_para_vencer
            FROM rrhh.contrato c
            INNER JOIN rrhh.empleado e
                ON e.id_empleado = c.id_empleado
            INNER JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            INNER JOIN rrhh.cargo cg
                ON cg.id_cargo = e.id_cargo
            INNER JOIN rrhh.tipo_contrato tc
                ON tc.id_tipo_contrato = c.id_tipo_contrato
            INNER JOIN rrhh.horario_laboral h
                ON h.id_horario_laboral = c.id_horario_laboral
            WHERE c.id_contrato = @id_contrato;
            """;

        using var comando = transaccion is null
            ? new SqlCommand(sql, conexion)
            : new SqlCommand(sql, conexion, transaccion);
        comando.Parameters.Add("@id_contrato", SqlDbType.BigInt).Value = id;

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapearContrato(reader);
    }

    private static ContratoDto MapearContrato(SqlDataReader reader)
    {
        return new ContratoDto
        {
            IdContrato = reader.GetInt64(reader.GetOrdinal("id_contrato")),
            IdEmpleado = reader.GetInt64(reader.GetOrdinal("id_empleado")),
            CodigoEmpleado = reader.GetString(reader.GetOrdinal("codigo_empleado")),
            NombreEmpleado = reader.GetString(reader.GetOrdinal("nombre_empleado")),
            CedulaEmpleado = reader.GetString(reader.GetOrdinal("cedula")),
            NombreDepartamento = reader.GetString(reader.GetOrdinal("nombre_departamento")),
            NombreCargo = reader.GetString(reader.GetOrdinal("nombre_cargo")),
            FechaIngresoEmpleado = reader.GetDateTime(reader.GetOrdinal("fecha_ingreso")).ToString("yyyy-MM-dd"),
            IdTipoContrato = reader.GetInt64(reader.GetOrdinal("id_tipo_contrato")),
            CodigoTipoContrato = reader.GetString(reader.GetOrdinal("codigo_tipo_contrato")),
            NombreTipoContrato = reader.GetString(reader.GetOrdinal("nombre_tipo_contrato")),
            IdHorarioLaboral = reader.GetInt64(reader.GetOrdinal("id_horario_laboral")),
            CodigoHorario = reader.GetString(reader.GetOrdinal("codigo_horario")),
            NombreHorario = reader.GetString(reader.GetOrdinal("nombre_horario")),
            HorasSemanales = reader.GetDecimal(reader.GetOrdinal("horas_semanales")),
            HorasDiarias = reader.GetDecimal(reader.GetOrdinal("horas_diarias")),
            NumeroContrato = reader.GetString(reader.GetOrdinal("numero_contrato")),
            FechaInicio = reader.GetDateTime(reader.GetOrdinal("fecha_inicio")).ToString("yyyy-MM-dd"),
            FechaFin = reader.IsDBNull(reader.GetOrdinal("fecha_fin"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("fecha_fin")).ToString("yyyy-MM-dd"),
            SalarioBaseMensual = reader.GetDecimal(reader.GetOrdinal("salario_base_mensual")),
            Moneda = reader.GetString(reader.GetOrdinal("moneda")),
            EsContratoVigente = reader.GetBoolean(reader.GetOrdinal("es_contrato_vigente")),
            EsTemporal = reader.GetBoolean(reader.GetOrdinal("es_temporal")),
            DiasParaVencer = reader.IsDBNull(reader.GetOrdinal("dias_para_vencer"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("dias_para_vencer")),
            EstaPorVencer = !reader.IsDBNull(reader.GetOrdinal("dias_para_vencer")),
            Observacion = reader.IsDBNull(reader.GetOrdinal("observacion"))
                ? null
                : reader.GetString(reader.GetOrdinal("observacion")),
            FechaRegistro = reader.GetDateTime(reader.GetOrdinal("fecha_registro")).ToString("yyyy-MM-dd HH:mm:ss"),
            EtiquetaAlerta = BuildContractAlertLabel(
                reader.IsDBNull(reader.GetOrdinal("dias_para_vencer"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("dias_para_vencer")),
                reader.IsDBNull(reader.GetOrdinal("fecha_fin"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("fecha_fin")),
                reader.GetBoolean(reader.GetOrdinal("es_temporal"))),
        };
    }

    private static void AsignarParametrosContrato(SqlCommand comando, ContratoGuardarModel model)
    {
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = model.IdEmpleado;
        comando.Parameters.Add("@id_tipo_contrato", SqlDbType.BigInt).Value = model.IdTipoContrato;
        comando.Parameters.Add("@id_horario_laboral", SqlDbType.BigInt).Value = model.IdHorarioLaboral;
        comando.Parameters.Add("@numero_contrato", SqlDbType.NVarChar, 100).Value =
            model.NumeroContrato.Trim().ToUpperInvariant();
        comando.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = DateTime.Parse(model.FechaInicio);
        comando.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = ToDateDbValue(model.FechaFin);
        comando.Parameters.Add("@salario_base_mensual", SqlDbType.Decimal).Value = model.SalarioBaseMensual;
        comando.Parameters["@salario_base_mensual"].Precision = 18;
        comando.Parameters["@salario_base_mensual"].Scale = 2;
        comando.Parameters.Add("@moneda", SqlDbType.NVarChar, 20).Value =
            model.Moneda.Trim().ToUpperInvariant();
        comando.Parameters.Add("@es_contrato_vigente", SqlDbType.Bit).Value = model.EsContratoVigente;
        comando.Parameters.Add("@observacion", SqlDbType.NVarChar, 1000).Value = ToDbValue(model.Observacion);
    }

    private string? ObtenerCodigoEmpleado(SqlConnection conexion, long idEmpleado)
    {
        using var comando = new SqlCommand(
            """
            SELECT codigo_empleado
            FROM rrhh.empleado
            WHERE id_empleado = @id_empleado;
            """,
            conexion);
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        return comando.ExecuteScalar()?.ToString();
    }

    private string SugerirNumeroContrato(SqlConnection conexion, string codigoEmpleado, long? ignorarIdContrato = null)
    {
        var baseNumero = $"CTR-{codigoEmpleado.Trim().ToUpperInvariant()}";
        if (!NumeroContratoExiste(conexion, null, baseNumero, ignorarIdContrato))
        {
            return baseNumero;
        }

        for (var indice = 2; indice < 1000; indice += 1)
        {
            var candidato = $"{baseNumero}-{indice:00}";
            if (!NumeroContratoExiste(conexion, null, candidato, ignorarIdContrato))
            {
                return candidato;
            }
        }

        return $"{baseNumero}-{DateTime.Now:HHmmss}";
    }

    private static bool NumeroContratoExiste(
        SqlConnection conexion,
        SqlTransaction? transaccion,
        string numeroContrato,
        long? ignorarIdContrato)
    {
        using var comando = transaccion is null
            ? new SqlCommand(
                """
                SELECT COUNT(1)
                FROM rrhh.contrato
                WHERE numero_contrato = @numero_contrato
                  AND (@ignorar_id IS NULL OR id_contrato <> @ignorar_id);
                """,
                conexion)
            : new SqlCommand(
                """
                SELECT COUNT(1)
                FROM rrhh.contrato
                WHERE numero_contrato = @numero_contrato
                  AND (@ignorar_id IS NULL OR id_contrato <> @ignorar_id);
                """,
                conexion,
                transaccion);

        comando.Parameters.Add("@numero_contrato", SqlDbType.NVarChar, 100).Value =
            numeroContrato.Trim().ToUpperInvariant();
        comando.Parameters.Add("@ignorar_id", SqlDbType.BigInt).Value =
            ignorarIdContrato.HasValue ? ignorarIdContrato.Value : DBNull.Value;
        return Convert.ToInt32(comando.ExecuteScalar()) > 0;
    }

    private EmpleadoResumenDto? ObtenerEmpleadoResumen(
        SqlConnection conexion,
        SqlTransaction transaccion,
        long idEmpleado)
    {
        using var comando = new SqlCommand(
            """
            SELECT
                id_empleado,
                codigo_empleado,
                COALESCE(NULLIF(nombre_completo, N''), CONCAT(nombres, N' ', apellidos)) AS nombre_empleado,
                fecha_ingreso
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.id_empleado = @id_empleado
              AND e.activo = 1
              AND e.fecha_baja IS NULL
              AND ISNULL(ee.codigo_estado_empleado, N'') <> N'RETIRADO';
            """,
            conexion,
            transaccion);
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

        using var reader = comando.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new EmpleadoResumenDto
        {
            IdEmpleado = reader.GetInt64(0),
            CodigoEmpleado = reader.GetString(1),
            NombreEmpleado = reader.GetString(2),
            FechaIngreso = reader.GetDateTime(3),
        };
    }

    private static bool ExisteSolapamientoContrato(
        SqlConnection conexion,
        SqlTransaction transaccion,
        long idEmpleado,
        DateTime fechaInicio,
        DateTime? fechaFin,
        long? idContratoActual)
    {
        using var comando = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.contrato
            WHERE id_empleado = @id_empleado
              AND (@id_contrato_actual IS NULL OR id_contrato <> @id_contrato_actual)
              AND fecha_inicio <= COALESCE(@fecha_fin, '9999-12-31')
              AND COALESCE(fecha_fin, '9999-12-31') >= @fecha_inicio;
            """,
            conexion,
            transaccion);
        comando.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
        comando.Parameters.Add("@fecha_inicio", SqlDbType.Date).Value = fechaInicio;
        comando.Parameters.Add("@fecha_fin", SqlDbType.Date).Value = fechaFin.HasValue ? fechaFin.Value : DBNull.Value;
        comando.Parameters.Add("@id_contrato_actual", SqlDbType.BigInt).Value = idContratoActual.HasValue ? idContratoActual.Value : DBNull.Value;
        return Convert.ToInt32(comando.ExecuteScalar()) > 0;
    }

    private EmpresaContratoDto ObtenerContextoEmpresa(SqlConnection conexion)
    {
        const string sql = """
            SELECT TOP (1)
                e.razon_social,
                e.nombre_comercial,
                e.ruc,
                e.telefono,
                e.correo,
                e.direccion,
                e.moneda_base,
                s.nombre_sucursal,
                s.codigo_sucursal,
                s.telefono AS telefono_sucursal,
                s.correo AS correo_sucursal,
                s.direccion AS direccion_sucursal
            FROM empresa.empresa e
            LEFT JOIN empresa.sucursal s
                ON s.id_empresa = e.id_empresa
               AND s.activo = 1
               AND s.es_principal = 1
            WHERE e.activo = 1
            ORDER BY e.id_empresa;
            """;

        using var comando = new SqlCommand(sql, conexion);
        using var reader = comando.ExecuteReader();

        if (!reader.Read())
        {
            return new EmpresaContratoDto
            {
                RazonSocial = "Sistema Informacion Financiera Nicaragua, S.A.",
                NombreComercial = "SIFNIC",
                Ruc = string.Empty,
                Telefono = string.Empty,
                Correo = string.Empty,
                Direccion = "Managua, Nicaragua",
                MonedaBase = "NIO",
                NombreSucursal = "Casa Matriz",
                CodigoSucursal = "CASA",
                DireccionSucursal = "Managua, Nicaragua",
            };
        }

        return new EmpresaContratoDto
        {
            RazonSocial = reader.GetString(0),
            NombreComercial = reader.GetString(1),
            Ruc = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Telefono = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Correo = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            Direccion = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            MonedaBase = reader.IsDBNull(6) ? "NIO" : reader.GetString(6),
            NombreSucursal = reader.IsDBNull(7) ? "Casa Matriz" : reader.GetString(7),
            CodigoSucursal = reader.IsDBNull(8) ? "CASA" : reader.GetString(8),
            TelefonoSucursal = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            CorreoSucursal = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
            DireccionSucursal = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
        };
    }

    private List<ReferenciaRelacionadaDto> ObtenerReferenciasContrato(SqlConnection conexion, long idContrato)
    {
        const string sql = """
            SELECT
                QUOTENAME(OBJECT_SCHEMA_NAME(fkc.parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(fkc.parent_object_id)) AS tabla,
                pc.name AS columna
            FROM sys.foreign_key_columns fkc
            INNER JOIN sys.columns pc
                ON pc.object_id = fkc.parent_object_id
               AND pc.column_id = fkc.parent_column_id
            WHERE fkc.referenced_object_id = OBJECT_ID('rrhh.contrato');
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
                $"SELECT COUNT(1) FROM {validacion.Tabla} WHERE {validacion.Columna} = @id_contrato;",
                conexion);
            verificador.Parameters.Add("@id_contrato", SqlDbType.BigInt).Value = idContrato;

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

    private void RegistrarBitacora(
        SqlConnection conexion,
        SqlTransaction? transaccion,
        string tipoEvento,
        long idReferencia,
        string referenciaTexto,
        string descripcion,
        object resumen,
        string? usuarioRegistro = null)
    {
        using var comando = transaccion is null
            ? new SqlCommand("operacion.usp_registrar_bitacora_operativa", conexion)
            : new SqlCommand("operacion.usp_registrar_bitacora_operativa", conexion, transaccion);
        comando.CommandType = CommandType.StoredProcedure;
        comando.Parameters.Add("@modulo", SqlDbType.NVarChar, 50).Value = "RRHH";
        comando.Parameters.Add("@proceso", SqlDbType.NVarChar, 100).Value = "CONTRATOS";
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

        if (texto.Contains("numero_contrato"))
        {
            return "El numero de contrato ya existe.";
        }

        if (texto.Contains("out-of-range") || texto.Contains("fuera de intervalo") || texto.Contains("datetime"))
        {
            return "Hay una fecha fuera del rango permitido. Usa una fecha igual o mayor a 01/01/1753.";
        }

        return "La base de datos rechazo la operacion.";
    }

    private static string NormalizarEstadoContrato(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return "TODOS";
        }

        var valor = estado.Trim().ToUpperInvariant();
        return valor switch
        {
            "VIGENTES" => "VIGENTES",
            "POR_VENCER" => "POR_VENCER",
            "TEMPORALES" => "TEMPORALES",
            "TEMPORALES_POR_VENCER" => "TEMPORALES_POR_VENCER",
            "HISTORICOS" => "HISTORICOS",
            _ => "TODOS",
        };
    }

    private static bool CumpleEstadoContrato(ContratoDto contrato, string status) =>
        status switch
        {
            "VIGENTES" => contrato.EsContratoVigente,
            "POR_VENCER" => contrato.EsContratoVigente && contrato.EstaPorVencer,
            "TEMPORALES" => contrato.EsTemporal,
            "TEMPORALES_POR_VENCER" => contrato.EsTemporal && contrato.EstaPorVencer,
            "HISTORICOS" => !contrato.EsContratoVigente,
            _ => true,
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

    private static string? BuildContractAlertLabel(int? daysToExpire, DateTime? endDate, bool isTemporary)
    {
        if (daysToExpire is > -1)
        {
            var prefix = isTemporary ? "Temporal por vencer" : "Por vencer";
            return endDate.HasValue
                ? $"{prefix} · {endDate.Value:dd/MM/yyyy}"
                : prefix;
        }

        if (isTemporary)
        {
            return "Temporal";
        }

        return null;
    }

    public sealed class ContratoGuardarModel
    {
        public long IdEmpleado { get; set; }
        public long IdTipoContrato { get; set; }
        public long IdHorarioLaboral { get; set; }
        public string NumeroContrato { get; set; } = string.Empty;
        public string FechaInicio { get; set; } = string.Empty;
        public string? FechaFin { get; set; }
        public decimal SalarioBaseMensual { get; set; }
        public string Moneda { get; set; } = "NIO";
        public bool EsContratoVigente { get; set; } = true;
        public string? Observacion { get; set; }
    }

    public sealed class ContratoEliminarModel
    {
        public string AdminUsuario { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    public sealed class ContratoDto
    {
        public long IdContrato { get; set; }
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public string CedulaEmpleado { get; set; } = string.Empty;
        public string NombreDepartamento { get; set; } = string.Empty;
        public string NombreCargo { get; set; } = string.Empty;
        public string FechaIngresoEmpleado { get; set; } = string.Empty;
        public long IdTipoContrato { get; set; }
        public string CodigoTipoContrato { get; set; } = string.Empty;
        public string NombreTipoContrato { get; set; } = string.Empty;
        public long IdHorarioLaboral { get; set; }
        public string CodigoHorario { get; set; } = string.Empty;
        public string NombreHorario { get; set; } = string.Empty;
        public decimal HorasSemanales { get; set; }
        public decimal HorasDiarias { get; set; }
        public string NumeroContrato { get; set; } = string.Empty;
        public string FechaInicio { get; set; } = string.Empty;
        public string? FechaFin { get; set; }
        public decimal SalarioBaseMensual { get; set; }
        public string Moneda { get; set; } = string.Empty;
        public bool EsContratoVigente { get; set; }
        public bool EsTemporal { get; set; }
        public bool EstaPorVencer { get; set; }
        public int? DiasParaVencer { get; set; }
        public string? EtiquetaAlerta { get; set; }
        public string? Observacion { get; set; }
        public string FechaRegistro { get; set; } = string.Empty;
    }

    public sealed class EmpresaContratoDto
    {
        public string RazonSocial { get; set; } = string.Empty;
        public string NombreComercial { get; set; } = string.Empty;
        public string Ruc { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string MonedaBase { get; set; } = "NIO";
        public string NombreSucursal { get; set; } = string.Empty;
        public string CodigoSucursal { get; set; } = string.Empty;
        public string TelefonoSucursal { get; set; } = string.Empty;
        public string CorreoSucursal { get; set; } = string.Empty;
        public string DireccionSucursal { get; set; } = string.Empty;
    }

    public sealed class ReferenciaRelacionadaDto
    {
        public string Table { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    private sealed class EmpleadoResumenDto
    {
        public long IdEmpleado { get; set; }
        public string CodigoEmpleado { get; set; } = string.Empty;
        public string NombreEmpleado { get; set; } = string.Empty;
        public DateTime FechaIngreso { get; set; }
    }
}
