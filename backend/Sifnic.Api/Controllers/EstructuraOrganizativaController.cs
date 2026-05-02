using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Sifnic.Api.Rrhh;

namespace Sifnic.Api.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class EstructuraOrganizativaController : Controller
{
    [HttpGet]
    public IActionResult Catalogos()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var catalogs = FormalOrganizationStructureSupport.GetCatalogs(connection);
            return Json(new
            {
                ok = true,
                data = catalogs,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudieron cargar los catalogos de estructura organizativa.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Listar(
        string? search = null,
        long? idDepartamento = null,
        string? tipoNodo = null,
        bool includeInactive = false)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var rows = FormalOrganizationStructureSupport.ListFlatNodes(connection, new FormalOrganizationStructureSupport.FormalStructureListOptions
            {
                Search = search,
                IdDepartamento = idDepartamento,
                TipoNodo = tipoNodo,
                IncludeInactive = includeInactive,
            });

            return Json(new
            {
                ok = true,
                data = new
                {
                    items = rows,
                },
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo listar la estructura organizativa formal.",
                detail = ex.Message,
            });
        }
    }

    [HttpGet]
    public IActionResult Arbol(
        string? search = null,
        long? idDepartamento = null,
        long? idNodoGerencia = null,
        bool includeInactive = false)
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var tree = FormalOrganizationStructureSupport.GetTree(connection, new FormalOrganizationStructureSupport.FormalStructureTreeOptions
            {
                Search = search,
                IdDepartamento = idDepartamento,
                BranchNodeId = idNodoGerencia,
                IncludeInactive = includeInactive,
            });

            return Json(new
            {
                ok = true,
                data = tree,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el organigrama formal.",
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

            var node = FormalOrganizationStructureSupport.GetNode(connection, id);
            if (node is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "El nodo solicitado no existe.",
                });
            }

            return Json(new
            {
                ok = true,
                data = node,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                ok = false,
                message = "No se pudo cargar el nodo de estructura.",
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] EstructuraOrganizativaSaveRequest? model)
    {
        var errors = ValidateSaveRequest(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del nodo.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var operatorUser = RrhhSupport.GetOperatorUser(Request);
            var entity = MapSaveModel(model!);
            var id = FormalOrganizationStructureSupport.CreateNode(connection, transaction, entity, operatorUser);

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "ESTRUCTURA_ORGANIZATIVA_FORMAL",
                "CREAR",
                id,
                entity.CodigoNodo,
                $"Se creo el nodo formal {entity.NombreNodo}.",
                new
                {
                    entity.CodigoNodo,
                    entity.NombreNodo,
                    entity.TipoNodo,
                    entity.IdNodoPadre,
                    entity.IdEmpleadoTitular,
                    entity.IdDepartamento,
                    entity.IdCargo,
                    entity.OrdenVisual,
                },
                operatorUser);

            transaction.Commit();

            var saved = FormalOrganizationStructureSupport.GetNode(connection, id);
            return Json(new
            {
                ok = true,
                message = "Nodo de estructura creado correctamente.",
                data = saved,
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
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo crear el nodo de estructura."),
                detail = ex.Message,
            });
        }
    }

    [HttpPut("{id:long}")]
    public IActionResult Actualizar(long id, [FromBody] EstructuraOrganizativaSaveRequest? model)
    {
        var errors = ValidateSaveRequest(model);
        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                ok = false,
                message = "Corrige los datos del nodo.",
                errors,
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var previous = FormalOrganizationStructureSupport.GetNode(connection, id);
            if (previous is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "El nodo seleccionado no existe.",
                });
            }

            using var transaction = connection.BeginTransaction();
            var operatorUser = RrhhSupport.GetOperatorUser(Request);
            var entity = MapSaveModel(model!);
            FormalOrganizationStructureSupport.UpdateNode(connection, transaction, id, entity, operatorUser);

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "ESTRUCTURA_ORGANIZATIVA_FORMAL",
                "ACTUALIZAR",
                id,
                entity.CodigoNodo,
                $"Se actualizo el nodo formal {entity.NombreNodo}.",
                new
                {
                    anterior = previous,
                    actual = entity,
                },
                operatorUser);

            transaction.Commit();

            var saved = FormalOrganizationStructureSupport.GetNode(connection, id);
            return Json(new
            {
                ok = true,
                message = "Nodo de estructura actualizado correctamente.",
                data = saved,
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
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo actualizar el nodo de estructura."),
                detail = ex.Message,
            });
        }
    }

    [HttpDelete("{id:long}")]
    public IActionResult Eliminar(long id, [FromBody] EstructuraOrganizativaDeleteRequest? model)
    {
        if (model is null ||
            string.IsNullOrWhiteSpace(model.AdminUsuario) ||
            string.IsNullOrWhiteSpace(model.AdminPassword))
        {
            return BadRequest(new
            {
                ok = false,
                message = "Ingresa la autorizacion del administrador para eliminar el nodo.",
                errors = new Dictionary<string, string>
                {
                    ["adminUsuario"] = "Ingresa el usuario administrador.",
                    ["adminPassword"] = "Ingresa la contrasena del administrador.",
                },
            });
        }

        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();

            var node = FormalOrganizationStructureSupport.GetNode(connection, id);
            if (node is null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "El nodo seleccionado no existe.",
                });
            }

            var authorization = RrhhSupport.ValidateAdministrator(connection, model.AdminUsuario, model.AdminPassword);
            if (!authorization.Ok)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = authorization.Message,
                    errors = new Dictionary<string, string>
                    {
                        ["adminPassword"] = authorization.Message,
                    },
                });
            }

            using var transaction = connection.BeginTransaction();
            FormalOrganizationStructureSupport.DeleteNode(connection, transaction, id);

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "ESTRUCTURA_ORGANIZATIVA_FORMAL",
                "ELIMINAR",
                id,
                node.CodigoNodo,
                $"Se elimino el nodo formal {node.NombreNodo}.",
                node,
                authorization.UsuarioAdministrador);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = "Nodo de estructura eliminado correctamente.",
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
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo eliminar el nodo de estructura."),
                detail = ex.Message,
            });
        }
    }

    [HttpPost]
    public IActionResult CargarEstructuraBase()
    {
        try
        {
            using var connection = new SqlConnection(ConexionDb.Cadena);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var operatorUser = RrhhSupport.GetOperatorUser(Request);
            var result = FormalOrganizationStructureSupport.SeedBaseStructure(connection, transaction, operatorUser);

            if (result.Skipped)
            {
                transaction.Rollback();
                return Conflict(new
                {
                    ok = false,
                    message = result.Message,
                });
            }

            RrhhSupport.RegisterBitacora(
                connection,
                transaction,
                HttpContext,
                "ESTRUCTURA_ORGANIZATIVA_FORMAL",
                "SEED_DEMO",
                0,
                "ESTRUCTURA-BASE",
                result.Message,
                new
                {
                    result.InsertedCount,
                    fuente = "MERMAID_REFERENCIA",
                },
                operatorUser);

            transaction.Commit();

            return Json(new
            {
                ok = true,
                message = result.Message,
                data = new
                {
                    result.InsertedCount,
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
                message = RrhhSupport.TranslateSqlMessage(ex.Message, "No se pudo cargar la estructura base institucional."),
                detail = ex.Message,
            });
        }
    }

    public IActionResult CargarDemoFdl() => CargarEstructuraBase();

    private static Dictionary<string, string> ValidateSaveRequest(EstructuraOrganizativaSaveRequest? model)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (model is null)
        {
            errors["form"] = "No se recibieron datos del nodo.";
            return errors;
        }

        if (string.IsNullOrWhiteSpace(model.CodigoNodo))
        {
            errors["codigoNodo"] = "Ingresa el codigo del nodo.";
        }

        if (string.IsNullOrWhiteSpace(model.NombreNodo))
        {
            errors["nombreNodo"] = "Ingresa el nombre del nodo.";
        }

        if (string.IsNullOrWhiteSpace(model.TipoNodo))
        {
            errors["tipoNodo"] = "Selecciona un tipo de nodo.";
        }

        if (model.OrdenVisual < 0)
        {
            errors["ordenVisual"] = "El orden visual no puede ser negativo.";
        }

        return errors;
    }

    private static FormalOrganizationStructureSupport.FormalStructureSaveModel MapSaveModel(EstructuraOrganizativaSaveRequest model)
    {
        return new FormalOrganizationStructureSupport.FormalStructureSaveModel
        {
            CodigoNodo = model.CodigoNodo?.Trim() ?? string.Empty,
            NombreNodo = model.NombreNodo?.Trim() ?? string.Empty,
            TipoNodo = model.TipoNodo?.Trim() ?? string.Empty,
            IdNodoPadre = NormalizeOptionalId(model.IdNodoPadre),
            IdEmpleadoTitular = NormalizeOptionalId(model.IdEmpleadoTitular),
            IdDepartamento = NormalizeOptionalId(model.IdDepartamento),
            IdCargo = NormalizeOptionalId(model.IdCargo),
            OrdenVisual = model.OrdenVisual,
            Activo = model.Activo,
            Observacion = string.IsNullOrWhiteSpace(model.Observacion) ? null : model.Observacion.Trim(),
        };
    }

    private static long? NormalizeOptionalId(long? value) =>
        value.HasValue && value.Value > 0 ? value.Value : null;

    public sealed class EstructuraOrganizativaSaveRequest
    {
        public string? CodigoNodo { get; set; }
        public string? NombreNodo { get; set; }
        public string? TipoNodo { get; set; }
        public long? IdNodoPadre { get; set; }
        public long? IdEmpleadoTitular { get; set; }
        public long? IdDepartamento { get; set; }
        public long? IdCargo { get; set; }
        public int OrdenVisual { get; set; }
        public bool Activo { get; set; } = true;
        public string? Observacion { get; set; }
    }

    public sealed class EstructuraOrganizativaDeleteRequest
    {
        public string AdminUsuario { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }
}
