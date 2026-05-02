using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class CatalogosRrhhController : Controller
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
                    id_departamento,
                    codigo_departamento,
                    nombre_departamento,
                    activo
                FROM rrhh.departamento
                ORDER BY
                    CASE WHEN activo = 1 THEN 0 ELSE 1 END,
                    nombre_departamento;
                """;

            using var command = new SqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            var departments = new List<object>();
            while (reader.Read())
            {
                departments.Add(new
                {
                    id = reader.GetInt64(0),
                    code = reader.GetString(1),
                    name = reader.GetString(2),
                    active = reader.GetBoolean(3),
                });
            }

            return Json(new
            {
                ok = true,
                data = new
                {
                    departments,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos base de RRHH.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Listar(string moduleId, string? search, string? status)
    {
        var definition = ResolveDefinition(moduleId);
        if (definition is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Catalogo no soportado.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            using var command = new SqlCommand(BuildListSql(definition.ModuleId), connection);
            command.Parameters.Add("@search", SqlDbType.NVarChar, 150).Value = (search ?? string.Empty).Trim();
            command.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = NormalizeCatalogStatus(status);

            using var reader = command.ExecuteReader();
            var items = new List<CatalogRecordDto>();
            while (reader.Read())
            {
                items.Add(MapCatalogRecord(reader, definition.ModuleId));
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
                message = $"No se pudo cargar {definition.PluralLabel.ToLowerInvariant()}.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Obtener(string moduleId, long id)
    {
        var definition = ResolveDefinition(moduleId);
        if (definition is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Catalogo no soportado.",
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var record = GetCatalogRecord(connection, definition.ModuleId, id);
            if (record is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = $"{definition.SingularLabel} no encontrado.",
                });
            }

            return Json(new
            {
                ok = true,
                data = record,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = $"No se pudo obtener {definition.SingularLabel.ToLowerInvariant()}.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] CatalogSaveModel model)
    {
        var definition = ResolveDefinition(model.ModuleId);
        if (definition is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Catalogo no soportado.",
            });
        }

        var errors = ValidateCatalog(model, definition.ModuleId);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = $"Corrige los datos de {definition.SingularLabel.ToLowerInvariant()}.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            long id;
            using (var command = new SqlCommand(BuildInsertSql(definition.ModuleId), connection, transaction))
            {
                ConfigureWriteCommand(command, definition.ModuleId, model, includeId: false, id: null);
                id = Convert.ToInt64(command.ExecuteScalar());
            }

            var record = GetCatalogRecord(connection, definition.ModuleId, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                definition.Process,
                "INSERCION",
                record.IdCatalogo,
                record.Codigo,
                $"Se creo {definition.SingularLabel.ToLowerInvariant()} {record.Codigo}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    registro = record,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = $"{definition.SingularLabel} creado correctamente.",
                data = record,
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                ok = false,
                message = TranslateCatalogSqlError(ex.Message, definition.SingularLabel),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = $"No se pudo crear {definition.SingularLabel.ToLowerInvariant()}.",
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult Actualizar(long id, [FromBody] CatalogSaveModel model)
    {
        var definition = ResolveDefinition(model.ModuleId);
        if (definition is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Catalogo no soportado.",
            });
        }

        var errors = ValidateCatalog(model, definition.ModuleId);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = $"Corrige los datos de {definition.SingularLabel.ToLowerInvariant()}.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var previous = GetCatalogRecord(connection, definition.ModuleId, id, transaction);
            if (previous is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = $"{definition.SingularLabel} no encontrado.",
                });
            }

            using (var command = new SqlCommand(BuildUpdateSql(definition.ModuleId), connection, transaction))
            {
                ConfigureWriteCommand(command, definition.ModuleId, model, includeId: true, id);
                command.ExecuteNonQuery();
            }

            var record = GetCatalogRecord(connection, definition.ModuleId, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                definition.Process,
                "MODIFICACION",
                record.IdCatalogo,
                record.Codigo,
                $"Se modifico {definition.SingularLabel.ToLowerInvariant()} {record.Codigo}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    anterior = previous,
                    actual = record,
                });

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = $"{definition.SingularLabel} actualizado correctamente.",
                data = record,
            });
        }
        catch (SqlException ex)
        {
            return BadRequest(new
            {
                ok = false,
                message = TranslateCatalogSqlError(ex.Message, definition.SingularLabel),
                detail = ex.Message,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = $"No se pudo actualizar {definition.SingularLabel.ToLowerInvariant()}.",
                detail = ex.Message,
            });
        }
    }

    [HttpDelete("{id:long}")]
    public IActionResult Eliminar(long id, [FromBody] CatalogDeleteModel model)
    {
        var definition = ResolveDefinition(model.ModuleId);
        if (definition is null)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Catalogo no soportado.",
            });
        }

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

            var record = GetCatalogRecord(connection, definition.ModuleId, id, transaction);
            if (record is null)
            {
                transaction.Rollback();
                return NotFound(new
                {
                    ok = false,
                    message = $"{definition.SingularLabel} no encontrado.",
                });
            }

            if (!record.Activo)
            {
                transaction.Rollback();
                return BadRequest(new
                {
                    ok = false,
                    message = $"{definition.SingularLabel} ya estaba inactivo.",
                });
            }

            using (var command = new SqlCommand(BuildDeactivateSql(definition.ModuleId), connection, transaction))
            {
                command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
                command.ExecuteNonQuery();
            }

            var updated = GetCatalogRecord(connection, definition.ModuleId, id, transaction)!;

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                definition.Process,
                "ELIMINACION",
                updated.IdCatalogo,
                updated.Codigo,
                $"Se desactivo {definition.SingularLabel.ToLowerInvariant()} {updated.Codigo}.",
                new
                {
                    operador = RrhhSupport.GetOperatorUser(Request),
                    administrador = authorization.UsuarioAdministrador,
                    registro = updated,
                },
                authorization.UsuarioAdministrador);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = $"{definition.SingularLabel} desactivado correctamente.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = $"No se pudo desactivar {definition.SingularLabel.ToLowerInvariant()}.",
                detail = ex.Message,
            });
        }
    }

    private static CatalogDefinition? ResolveDefinition(string? moduleId)
    {
        return NormalizeModuleId(moduleId) switch
        {
            "tipo_contrato" => new CatalogDefinition("tipo_contrato", "CATALOGO_TIPO_CONTRATO", "Tipo de contrato", "tipos de contrato"),
            "estado_empleado" => new CatalogDefinition("estado_empleado", "CATALOGO_ESTADO_EMPLEADO", "Estado de empleado", "estados de empleado"),
            "departamento" => new CatalogDefinition("departamento", "CATALOGO_DEPARTAMENTO", "Departamento", "departamentos"),
            "cargo" => new CatalogDefinition("cargo", "CATALOGO_CARGO", "Cargo", "cargos"),
            "horario_laboral" => new CatalogDefinition("horario_laboral", "CATALOGO_HORARIO", "Horario laboral", "horarios laborales"),
            "banco" => new CatalogDefinition("banco", "CATALOGO_BANCO", "Banco", "bancos"),
            "tipo_permiso" => new CatalogDefinition("tipo_permiso", "CATALOGO_TIPO_PERMISO", "Tipo de permiso", "tipos de permiso"),
            "tipo_hora_extra" => new CatalogDefinition("tipo_hora_extra", "CATALOGO_TIPO_HORA_EXTRA", "Tipo de hora extra", "tipos de hora extra"),
            _ => null,
        };
    }

    private CatalogRecordDto? GetCatalogRecord(
        SqlConnection connection,
        string moduleId,
        long id,
        SqlTransaction? transaction = null)
    {
        using var command = transaction is null
            ? new SqlCommand(BuildGetSql(moduleId), connection)
            : new SqlCommand(BuildGetSql(moduleId), connection, transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = id;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapCatalogRecord(reader, moduleId) : null;
    }

    private static CatalogRecordDto MapCatalogRecord(SqlDataReader reader, string moduleId) => new()
    {
        ModuleId = moduleId,
        IdCatalogo = reader.GetInt64(0),
        Codigo = reader.GetString(1),
        Nombre = reader.GetString(2),
        Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
        Activo = reader.GetBoolean(4),
        RelatedId = reader.IsDBNull(5) ? null : reader.GetInt64(5),
        RelatedName = reader.IsDBNull(6) ? null : reader.GetString(6),
        NumberValue1 = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
        NumberValue2 = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
        IntegerValue1 = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        FlagValue1 = reader.IsDBNull(10) ? null : reader.GetBoolean(10),
        FechaRegistro = reader.IsDBNull(11) ? null : reader.GetDateTime(11).ToString("yyyy-MM-dd HH:mm:ss"),
    };

    private static Dictionary<string, string> ValidateCatalog(CatalogSaveModel model, string moduleId)
    {
        var errors = new Dictionary<string, string>();
        var code = (model.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var name = (model.Nombre ?? string.Empty).Trim();
        var description = model.Descripcion?.Trim();

        if (!Regex.IsMatch(code, "^[A-Z0-9_-]{2,30}$"))
        {
            errors["codigo"] = "Codigo invalido.";
        }

        if (name.Length < 2 || name.Length > 150)
        {
            errors["nombre"] = "Nombre invalido.";
        }

        if (!string.IsNullOrWhiteSpace(description) && description.Length > 300)
        {
            errors["descripcion"] = "La descripcion supera el limite permitido.";
        }

        switch (moduleId)
        {
            case "cargo":
                if (!(model.RelatedId > 0))
                {
                    errors["relatedId"] = "Selecciona un departamento.";
                }

                if (!(model.IntegerValue1 >= 1 && model.IntegerValue1 <= 99))
                {
                    errors["integerValue1"] = "Ingresa un nivel jerarquico valido.";
                }
                break;

            case "horario_laboral":
                if (!(model.NumberValue1 > 0) || model.NumberValue1 > 168)
                {
                    errors["numberValue1"] = "Ingresa las horas semanales validas.";
                }

                if (!(model.NumberValue2 > 0) || model.NumberValue2 > 24)
                {
                    errors["numberValue2"] = "Ingresa las horas diarias validas.";
                }
                else if (model.NumberValue1 > 0 && model.NumberValue2 > model.NumberValue1)
                {
                    errors["numberValue2"] = "Las horas diarias no pueden superar las horas semanales.";
                }
                break;

            case "tipo_permiso":
                break;

            case "tipo_hora_extra":
                if (!(model.NumberValue1 > 0) || model.NumberValue1 > 10)
                {
                    errors["numberValue1"] = "Ingresa un factor de pago valido.";
                }
                break;
        }

        return errors;
    }

    private static void ConfigureWriteCommand(
        SqlCommand command,
        string moduleId,
        CatalogSaveModel model,
        bool includeId,
        long? id)
    {
        if (includeId && id.HasValue)
        {
            command.Parameters.Add("@id", SqlDbType.BigInt).Value = id.Value;
        }

        command.Parameters.Add("@codigo", SqlDbType.NVarChar, 30).Value =
            (model.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        command.Parameters.Add("@nombre", SqlDbType.NVarChar, 150).Value =
            (model.Nombre ?? string.Empty).Trim();
        command.Parameters.Add("@descripcion", SqlDbType.NVarChar, 300).Value =
            RrhhSupport.ToDbValue(model.Descripcion);
        command.Parameters.Add("@activo", SqlDbType.Bit).Value = model.Activo;

        switch (moduleId)
        {
            case "cargo":
                command.Parameters.Add("@related_id", SqlDbType.BigInt).Value = model.RelatedId ?? 0;
                command.Parameters.Add("@integer_value_1", SqlDbType.Int).Value = model.IntegerValue1 ?? 0;
                break;

            case "horario_laboral":
                command.Parameters.Add("@number_value_1", SqlDbType.Decimal).Value = model.NumberValue1 ?? 0;
                command.Parameters["@number_value_1"].Precision = 18;
                command.Parameters["@number_value_1"].Scale = 2;
                command.Parameters.Add("@number_value_2", SqlDbType.Decimal).Value = model.NumberValue2 ?? 0;
                command.Parameters["@number_value_2"].Precision = 18;
                command.Parameters["@number_value_2"].Scale = 2;
                break;

            case "tipo_permiso":
                command.Parameters.Add("@flag_value_1", SqlDbType.Bit).Value = model.FlagValue1 ?? false;
                break;

            case "tipo_hora_extra":
                command.Parameters.Add("@number_value_1", SqlDbType.Decimal).Value = model.NumberValue1 ?? 0;
                command.Parameters["@number_value_1"].Precision = 18;
                command.Parameters["@number_value_1"].Scale = 2;
                break;
        }
    }

    private static string BuildListSql(string moduleId)
    {
        return moduleId switch
        {
            "tipo_contrato" => """
                SELECT
                    t.id_tipo_contrato,
                    t.codigo_tipo_contrato,
                    t.nombre_tipo_contrato,
                    t.descripcion,
                    t.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.tipo_contrato t
                WHERE
                    (
                        @search = N''
                        OR t.codigo_tipo_contrato LIKE N'%' + @search + N'%'
                        OR t.nombre_tipo_contrato LIKE N'%' + @search + N'%'
                        OR COALESCE(t.descripcion, N'') LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND t.activo = 1)
                        OR (@status = N'INACTIVOS' AND t.activo = 0)
                    )
                ORDER BY
                    CASE WHEN t.activo = 1 THEN 0 ELSE 1 END,
                    t.nombre_tipo_contrato;
                """,
            "estado_empleado" => """
                SELECT
                    e.id_estado_empleado,
                    e.codigo_estado_empleado,
                    e.nombre_estado_empleado,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    e.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.estado_empleado e
                WHERE
                    (
                        @search = N''
                        OR e.codigo_estado_empleado LIKE N'%' + @search + N'%'
                        OR e.nombre_estado_empleado LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND e.activo = 1)
                        OR (@status = N'INACTIVOS' AND e.activo = 0)
                    )
                ORDER BY
                    CASE WHEN e.activo = 1 THEN 0 ELSE 1 END,
                    e.nombre_estado_empleado;
                """,
            "departamento" => """
                SELECT
                    d.id_departamento,
                    d.codigo_departamento,
                    d.nombre_departamento,
                    d.descripcion,
                    d.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    d.fecha_registro
                FROM rrhh.departamento d
                WHERE
                    (
                        @search = N''
                        OR d.codigo_departamento LIKE N'%' + @search + N'%'
                        OR d.nombre_departamento LIKE N'%' + @search + N'%'
                        OR COALESCE(d.descripcion, N'') LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND d.activo = 1)
                        OR (@status = N'INACTIVOS' AND d.activo = 0)
                    )
                ORDER BY
                    CASE WHEN d.activo = 1 THEN 0 ELSE 1 END,
                    d.nombre_departamento;
                """,
            "cargo" => """
                SELECT
                    c.id_cargo,
                    c.codigo_cargo,
                    c.nombre_cargo,
                    c.descripcion,
                    c.activo,
                    d.id_departamento,
                    d.nombre_departamento,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    c.nivel_jerarquico,
                    CAST(NULL AS BIT) AS flag_value_1,
                    c.fecha_registro
                FROM rrhh.cargo c
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = c.id_departamento
                WHERE
                    (
                        @search = N''
                        OR c.codigo_cargo LIKE N'%' + @search + N'%'
                        OR c.nombre_cargo LIKE N'%' + @search + N'%'
                        OR d.nombre_departamento LIKE N'%' + @search + N'%'
                        OR COALESCE(c.descripcion, N'') LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND c.activo = 1)
                        OR (@status = N'INACTIVOS' AND c.activo = 0)
                    )
                ORDER BY
                    CASE WHEN c.activo = 1 THEN 0 ELSE 1 END,
                    c.nombre_cargo;
                """,
            "horario_laboral" => """
                SELECT
                    h.id_horario_laboral,
                    h.codigo_horario,
                    h.nombre_horario,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    h.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    h.horas_semanales,
                    h.horas_diarias,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.horario_laboral h
                WHERE
                    (
                        @search = N''
                        OR h.codigo_horario LIKE N'%' + @search + N'%'
                        OR h.nombre_horario LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND h.activo = 1)
                        OR (@status = N'INACTIVOS' AND h.activo = 0)
                    )
                ORDER BY
                    CASE WHEN h.activo = 1 THEN 0 ELSE 1 END,
                    h.nombre_horario;
                """,
            "banco" => """
                SELECT
                    b.id_banco,
                    b.codigo_banco,
                    b.nombre_banco,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    b.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.banco b
                WHERE
                    (
                        @search = N''
                        OR b.codigo_banco LIKE N'%' + @search + N'%'
                        OR b.nombre_banco LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND b.activo = 1)
                        OR (@status = N'INACTIVOS' AND b.activo = 0)
                    )
                ORDER BY
                    CASE WHEN b.activo = 1 THEN 0 ELSE 1 END,
                    b.nombre_banco;
                """,
            "tipo_permiso" => """
                SELECT
                    t.id_tipo_permiso,
                    t.codigo_tipo_permiso,
                    t.nombre_tipo_permiso,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    t.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    t.afecta_salario,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.tipo_permiso t
                WHERE
                    (
                        @search = N''
                        OR t.codigo_tipo_permiso LIKE N'%' + @search + N'%'
                        OR t.nombre_tipo_permiso LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND t.activo = 1)
                        OR (@status = N'INACTIVOS' AND t.activo = 0)
                    )
                ORDER BY
                    CASE WHEN t.activo = 1 THEN 0 ELSE 1 END,
                    t.nombre_tipo_permiso;
                """,
            "tipo_hora_extra" => """
                SELECT
                    t.id_tipo_hora_extra,
                    t.codigo_tipo_hora_extra,
                    t.nombre_tipo_hora_extra,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    t.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    t.factor_pago,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.tipo_hora_extra t
                WHERE
                    (
                        @search = N''
                        OR t.codigo_tipo_hora_extra LIKE N'%' + @search + N'%'
                        OR t.nombre_tipo_hora_extra LIKE N'%' + @search + N'%'
                    )
                    AND
                    (
                        @status = N'TODOS'
                        OR (@status = N'ACTIVOS' AND t.activo = 1)
                        OR (@status = N'INACTIVOS' AND t.activo = 0)
                    )
                ORDER BY
                    CASE WHEN t.activo = 1 THEN 0 ELSE 1 END,
                    t.nombre_tipo_hora_extra;
                """,
            _ => throw new InvalidOperationException("Catalogo no soportado."),
        };
    }

    private static string BuildGetSql(string moduleId)
    {
        return moduleId switch
        {
            "tipo_contrato" => """
                SELECT
                    t.id_tipo_contrato,
                    t.codigo_tipo_contrato,
                    t.nombre_tipo_contrato,
                    t.descripcion,
                    t.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.tipo_contrato t
                WHERE t.id_tipo_contrato = @id;
                """,
            "estado_empleado" => """
                SELECT
                    e.id_estado_empleado,
                    e.codigo_estado_empleado,
                    e.nombre_estado_empleado,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    e.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.estado_empleado e
                WHERE e.id_estado_empleado = @id;
                """,
            "departamento" => """
                SELECT
                    d.id_departamento,
                    d.codigo_departamento,
                    d.nombre_departamento,
                    d.descripcion,
                    d.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    d.fecha_registro
                FROM rrhh.departamento d
                WHERE d.id_departamento = @id;
                """,
            "cargo" => """
                SELECT
                    c.id_cargo,
                    c.codigo_cargo,
                    c.nombre_cargo,
                    c.descripcion,
                    c.activo,
                    d.id_departamento,
                    d.nombre_departamento,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    c.nivel_jerarquico,
                    CAST(NULL AS BIT) AS flag_value_1,
                    c.fecha_registro
                FROM rrhh.cargo c
                INNER JOIN rrhh.departamento d
                    ON d.id_departamento = c.id_departamento
                WHERE c.id_cargo = @id;
                """,
            "horario_laboral" => """
                SELECT
                    h.id_horario_laboral,
                    h.codigo_horario,
                    h.nombre_horario,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    h.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    h.horas_semanales,
                    h.horas_diarias,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.horario_laboral h
                WHERE h.id_horario_laboral = @id;
                """,
            "banco" => """
                SELECT
                    b.id_banco,
                    b.codigo_banco,
                    b.nombre_banco,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    b.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.banco b
                WHERE b.id_banco = @id;
                """,
            "tipo_permiso" => """
                SELECT
                    t.id_tipo_permiso,
                    t.codigo_tipo_permiso,
                    t.nombre_tipo_permiso,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    t.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_1,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    t.afecta_salario,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.tipo_permiso t
                WHERE t.id_tipo_permiso = @id;
                """,
            "tipo_hora_extra" => """
                SELECT
                    t.id_tipo_hora_extra,
                    t.codigo_tipo_hora_extra,
                    t.nombre_tipo_hora_extra,
                    CAST(NULL AS NVARCHAR(300)) AS descripcion,
                    t.activo,
                    CAST(NULL AS BIGINT) AS related_id,
                    CAST(NULL AS NVARCHAR(150)) AS related_name,
                    t.factor_pago,
                    CAST(NULL AS DECIMAL(18,2)) AS number_value_2,
                    CAST(NULL AS INT) AS integer_value_1,
                    CAST(NULL AS BIT) AS flag_value_1,
                    CAST(NULL AS DATETIME2) AS fecha_registro
                FROM rrhh.tipo_hora_extra t
                WHERE t.id_tipo_hora_extra = @id;
                """,
            _ => throw new InvalidOperationException("Catalogo no soportado."),
        };
    }

    private static string BuildInsertSql(string moduleId)
    {
        return moduleId switch
        {
            "tipo_contrato" => """
                INSERT INTO rrhh.tipo_contrato
                (
                    codigo_tipo_contrato,
                    nombre_tipo_contrato,
                    descripcion,
                    activo
                )
                OUTPUT INSERTED.id_tipo_contrato
                VALUES
                (
                    @codigo,
                    @nombre,
                    @descripcion,
                    @activo
                );
                """,
            "estado_empleado" => """
                INSERT INTO rrhh.estado_empleado
                (
                    codigo_estado_empleado,
                    nombre_estado_empleado,
                    activo
                )
                OUTPUT INSERTED.id_estado_empleado
                VALUES
                (
                    @codigo,
                    @nombre,
                    @activo
                );
                """,
            "departamento" => """
                INSERT INTO rrhh.departamento
                (
                    codigo_departamento,
                    nombre_departamento,
                    descripcion,
                    activo,
                    fecha_registro
                )
                OUTPUT INSERTED.id_departamento
                VALUES
                (
                    @codigo,
                    @nombre,
                    @descripcion,
                    @activo,
                    SYSDATETIME()
                );
                """,
            "cargo" => """
                INSERT INTO rrhh.cargo
                (
                    id_departamento,
                    codigo_cargo,
                    nombre_cargo,
                    descripcion,
                    nivel_jerarquico,
                    activo,
                    fecha_registro
                )
                OUTPUT INSERTED.id_cargo
                VALUES
                (
                    @related_id,
                    @codigo,
                    @nombre,
                    @descripcion,
                    @integer_value_1,
                    @activo,
                    SYSDATETIME()
                );
                """,
            "horario_laboral" => """
                INSERT INTO rrhh.horario_laboral
                (
                    codigo_horario,
                    nombre_horario,
                    horas_semanales,
                    horas_diarias,
                    activo
                )
                OUTPUT INSERTED.id_horario_laboral
                VALUES
                (
                    @codigo,
                    @nombre,
                    @number_value_1,
                    @number_value_2,
                    @activo
                );
                """,
            "banco" => """
                INSERT INTO rrhh.banco
                (
                    codigo_banco,
                    nombre_banco,
                    activo
                )
                OUTPUT INSERTED.id_banco
                VALUES
                (
                    @codigo,
                    @nombre,
                    @activo
                );
                """,
            "tipo_permiso" => """
                INSERT INTO rrhh.tipo_permiso
                (
                    codigo_tipo_permiso,
                    nombre_tipo_permiso,
                    afecta_salario,
                    activo
                )
                OUTPUT INSERTED.id_tipo_permiso
                VALUES
                (
                    @codigo,
                    @nombre,
                    @flag_value_1,
                    @activo
                );
                """,
            "tipo_hora_extra" => """
                INSERT INTO rrhh.tipo_hora_extra
                (
                    codigo_tipo_hora_extra,
                    nombre_tipo_hora_extra,
                    factor_pago,
                    activo
                )
                OUTPUT INSERTED.id_tipo_hora_extra
                VALUES
                (
                    @codigo,
                    @nombre,
                    @number_value_1,
                    @activo
                );
                """,
            _ => throw new InvalidOperationException("Catalogo no soportado."),
        };
    }

    private static string BuildUpdateSql(string moduleId)
    {
        return moduleId switch
        {
            "tipo_contrato" => """
                UPDATE rrhh.tipo_contrato
                SET
                    codigo_tipo_contrato = @codigo,
                    nombre_tipo_contrato = @nombre,
                    descripcion = @descripcion,
                    activo = @activo
                WHERE id_tipo_contrato = @id;
                """,
            "estado_empleado" => """
                UPDATE rrhh.estado_empleado
                SET
                    codigo_estado_empleado = @codigo,
                    nombre_estado_empleado = @nombre,
                    activo = @activo
                WHERE id_estado_empleado = @id;
                """,
            "departamento" => """
                UPDATE rrhh.departamento
                SET
                    codigo_departamento = @codigo,
                    nombre_departamento = @nombre,
                    descripcion = @descripcion,
                    activo = @activo
                WHERE id_departamento = @id;
                """,
            "cargo" => """
                UPDATE rrhh.cargo
                SET
                    id_departamento = @related_id,
                    codigo_cargo = @codigo,
                    nombre_cargo = @nombre,
                    descripcion = @descripcion,
                    nivel_jerarquico = @integer_value_1,
                    activo = @activo
                WHERE id_cargo = @id;
                """,
            "horario_laboral" => """
                UPDATE rrhh.horario_laboral
                SET
                    codigo_horario = @codigo,
                    nombre_horario = @nombre,
                    horas_semanales = @number_value_1,
                    horas_diarias = @number_value_2,
                    activo = @activo
                WHERE id_horario_laboral = @id;
                """,
            "banco" => """
                UPDATE rrhh.banco
                SET
                    codigo_banco = @codigo,
                    nombre_banco = @nombre,
                    activo = @activo
                WHERE id_banco = @id;
                """,
            "tipo_permiso" => """
                UPDATE rrhh.tipo_permiso
                SET
                    codigo_tipo_permiso = @codigo,
                    nombre_tipo_permiso = @nombre,
                    afecta_salario = @flag_value_1,
                    activo = @activo
                WHERE id_tipo_permiso = @id;
                """,
            "tipo_hora_extra" => """
                UPDATE rrhh.tipo_hora_extra
                SET
                    codigo_tipo_hora_extra = @codigo,
                    nombre_tipo_hora_extra = @nombre,
                    factor_pago = @number_value_1,
                    activo = @activo
                WHERE id_tipo_hora_extra = @id;
                """,
            _ => throw new InvalidOperationException("Catalogo no soportado."),
        };
    }

    private static string BuildDeactivateSql(string moduleId)
    {
        var table = moduleId switch
        {
            "tipo_contrato" => "rrhh.tipo_contrato",
            "estado_empleado" => "rrhh.estado_empleado",
            "departamento" => "rrhh.departamento",
            "cargo" => "rrhh.cargo",
            "horario_laboral" => "rrhh.horario_laboral",
            "banco" => "rrhh.banco",
            "tipo_permiso" => "rrhh.tipo_permiso",
            "tipo_hora_extra" => "rrhh.tipo_hora_extra",
            _ => throw new InvalidOperationException("Catalogo no soportado."),
        };

        return $"UPDATE {table} SET activo = 0 WHERE {GetPlainIdColumn(moduleId)} = @id;";
    }

    private static string GetPlainIdColumn(string moduleId)
    {
        return moduleId switch
        {
            "tipo_contrato" => "id_tipo_contrato",
            "estado_empleado" => "id_estado_empleado",
            "departamento" => "id_departamento",
            "cargo" => "id_cargo",
            "horario_laboral" => "id_horario_laboral",
            "banco" => "id_banco",
            "tipo_permiso" => "id_tipo_permiso",
            "tipo_hora_extra" => "id_tipo_hora_extra",
            _ => throw new InvalidOperationException("Catalogo no soportado."),
        };
    }

    private static string NormalizeCatalogStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? "ACTIVOS"
            : status.Trim().ToUpperInvariant() switch
            {
                "TODOS" => "TODOS",
                "INACTIVOS" => "INACTIVOS",
                _ => "ACTIVOS",
            };
    }

    private static string NormalizeModuleId(string? moduleId) =>
        string.IsNullOrWhiteSpace(moduleId) ? string.Empty : moduleId.Trim().ToLowerInvariant();

    private static string TranslateCatalogSqlError(string message, string label)
    {
        var text = message.ToLowerInvariant();

        if (text.Contains("codigo_"))
        {
            return $"El codigo de {label.ToLowerInvariant()} ya existe.";
        }

        if (text.Contains("nombre_"))
        {
            return $"El nombre de {label.ToLowerInvariant()} ya existe.";
        }

        if (text.Contains("foreign key") || text.Contains("constraint"))
        {
            return $"Hay una relacion invalida en {label.ToLowerInvariant()}.";
        }

        return RrhhSupport.TranslateSqlMessage(message, $"No se pudo guardar {label.ToLowerInvariant()}.");
    }

    private sealed record CatalogDefinition(
        string ModuleId,
        string Process,
        string SingularLabel,
        string PluralLabel);

    public sealed class CatalogSaveModel
    {
        public string ModuleId { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public long? RelatedId { get; set; }
        public decimal? NumberValue1 { get; set; }
        public decimal? NumberValue2 { get; set; }
        public int? IntegerValue1 { get; set; }
        public bool? FlagValue1 { get; set; }
        public bool Activo { get; set; } = true;
    }

    public sealed class CatalogDeleteModel
    {
        public string ModuleId { get; set; } = string.Empty;
        public string AdminUsuario { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    public sealed class CatalogRecordDto
    {
        public string ModuleId { get; set; } = string.Empty;
        public long IdCatalogo { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public bool Activo { get; set; }
        public long? RelatedId { get; set; }
        public string? RelatedName { get; set; }
        public decimal? NumberValue1 { get; set; }
        public decimal? NumberValue2 { get; set; }
        public int? IntegerValue1 { get; set; }
        public bool? FlagValue1 { get; set; }
        public string? FechaRegistro { get; set; }
    }
}
