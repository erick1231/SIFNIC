using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Rrhh;

public static class FormalOrganizationStructureSupport
{
    private static readonly string[] AllowedNodeTypes =
    [
        "ASAMBLEA",
        "JUNTA_DIRECTIVA",
        "GERENCIA_GENERAL",
        "VICEGERENCIA",
        "GERENCIA",
        "JEFATURA",
        "COORDINACION",
        "UNIDAD",
        "PUESTO",
        "APOYO",
        "VACANTE",
    ];

    private static readonly HashSet<string> AllowedNodeTypeSet = new(AllowedNodeTypes, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetAllowedNodeTypes() => AllowedNodeTypes;

    public static void EnsureSchema(SqlConnection connection)
    {
        const string sql = """
            IF OBJECT_ID(N'rrhh.estructura_organizativa_nodo', N'U') IS NULL
            BEGIN
                CREATE TABLE rrhh.estructura_organizativa_nodo
                (
                    id_nodo_estructura BIGINT IDENTITY(1,1) NOT NULL
                        CONSTRAINT PK_rrhh_estructura_organizativa_nodo PRIMARY KEY,
                    codigo_nodo NVARCHAR(50) NOT NULL,
                    nombre_nodo NVARCHAR(200) NOT NULL,
                    tipo_nodo NVARCHAR(40) NOT NULL,
                    id_nodo_padre BIGINT NULL,
                    id_empleado_titular BIGINT NULL,
                    id_departamento BIGINT NULL,
                    id_cargo BIGINT NULL,
                    orden_visual INT NOT NULL
                        CONSTRAINT DF_rrhh_estructura_organizativa_nodo_orden DEFAULT (0),
                    activo BIT NOT NULL
                        CONSTRAINT DF_rrhh_estructura_organizativa_nodo_activo DEFAULT (1),
                    observacion NVARCHAR(500) NULL,
                    fecha_registro DATETIME2 NOT NULL
                        CONSTRAINT DF_rrhh_estructura_organizativa_nodo_fecha_registro DEFAULT SYSDATETIME(),
                    fecha_actualizacion DATETIME2 NULL,
                    usuario_registro NVARCHAR(100) NULL,
                    usuario_actualizacion NVARCHAR(100) NULL,
                    CONSTRAINT FK_rrhh_estructura_organizativa_nodo_padre
                        FOREIGN KEY (id_nodo_padre) REFERENCES rrhh.estructura_organizativa_nodo(id_nodo_estructura),
                    CONSTRAINT FK_rrhh_estructura_organizativa_nodo_empleado
                        FOREIGN KEY (id_empleado_titular) REFERENCES rrhh.empleado(id_empleado),
                    CONSTRAINT FK_rrhh_estructura_organizativa_nodo_departamento
                        FOREIGN KEY (id_departamento) REFERENCES rrhh.departamento(id_departamento),
                    CONSTRAINT FK_rrhh_estructura_organizativa_nodo_cargo
                        FOREIGN KEY (id_cargo) REFERENCES rrhh.cargo(id_cargo),
                    CONSTRAINT CK_rrhh_estructura_organizativa_nodo_tipo
                        CHECK (tipo_nodo IN (
                            N'ASAMBLEA',
                            N'JUNTA_DIRECTIVA',
                            N'GERENCIA_GENERAL',
                            N'VICEGERENCIA',
                            N'GERENCIA',
                            N'JEFATURA',
                            N'COORDINACION',
                            N'UNIDAD',
                            N'PUESTO',
                            N'APOYO',
                            N'VACANTE'
                        ))
                );

                CREATE UNIQUE INDEX UX_rrhh_estructura_organizativa_nodo_codigo
                    ON rrhh.estructura_organizativa_nodo(codigo_nodo);

                CREATE INDEX IX_rrhh_estructura_organizativa_nodo_padre
                    ON rrhh.estructura_organizativa_nodo(id_nodo_padre, orden_visual, activo);

                CREATE INDEX IX_rrhh_estructura_organizativa_nodo_empleado
                    ON rrhh.estructura_organizativa_nodo(id_empleado_titular, activo);
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static FormalStructureCatalogsDto GetCatalogs(SqlConnection connection)
    {
        EnsureSchema(connection);
        RrhhSupport.EnsureEmployeeProfileSchema(connection);

        var nodes = ListFlatNodes(connection, new FormalStructureListOptions
        {
            IncludeInactive = true,
        });

        var departments = new List<FormalStructureOptionDto>();
        var positions = new List<FormalStructureOptionDto>();
        var employees = new List<FormalStructureOptionDto>();

        const string sql = """
            SELECT id_departamento, codigo_departamento, nombre_departamento
            FROM rrhh.departamento
            WHERE activo = 1
            ORDER BY nombre_departamento;

            SELECT c.id_cargo, c.codigo_cargo, c.nombre_cargo, d.nombre_departamento
            FROM rrhh.cargo c
            LEFT JOIN rrhh.departamento d
                ON d.id_departamento = c.id_departamento
            WHERE c.activo = 1
            ORDER BY d.nombre_departamento, c.nombre_cargo;

            SELECT
                e.id_empleado,
                e.codigo_empleado,
                COALESCE(NULLIF(e.nombre_completo, N''), CONCAT(e.nombres, N' ', e.apellidos)) AS nombre_empleado,
                d.nombre_departamento,
                c.nombre_cargo
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            LEFT JOIN rrhh.departamento d
                ON d.id_departamento = e.id_departamento
            LEFT JOIN rrhh.cargo c
                ON c.id_cargo = e.id_cargo
            WHERE e.activo = 1
              AND e.fecha_baja IS NULL
              AND UPPER(COALESCE(ee.codigo_estado_empleado, N'')) <> N'RETIRADO'
            ORDER BY nombre_empleado, e.codigo_empleado;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            departments.Add(new FormalStructureOptionDto
            {
                Id = reader.GetInt64(0),
                Code = reader.IsDBNull(1) ? null : reader.GetString(1),
                Label = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            });
        }

        reader.NextResult();
        while (reader.Read())
        {
            positions.Add(new FormalStructureOptionDto
            {
                Id = reader.GetInt64(0),
                Code = reader.IsDBNull(1) ? null : reader.GetString(1),
                Label = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Meta = reader.IsDBNull(3) ? null : reader.GetString(3),
            });
        }

        reader.NextResult();
        while (reader.Read())
        {
            var name = reader.IsDBNull(2) ? "Empleado" : reader.GetString(2);
            var code = reader.IsDBNull(1) ? null : reader.GetString(1);
            var department = reader.IsDBNull(3) ? null : reader.GetString(3);
            var position = reader.IsDBNull(4) ? null : reader.GetString(4);
            var metaParts = new[] { department, position }.Where(value => !string.IsNullOrWhiteSpace(value));

            employees.Add(new FormalStructureOptionDto
            {
                Id = reader.GetInt64(0),
                Code = code,
                Label = code is null ? name : $"{name} ({code})",
                Meta = string.Join(" · ", metaParts),
            });
        }

        return new FormalStructureCatalogsDto
        {
            NodeTypes = AllowedNodeTypes
                .Select(type => new FormalStructureTypeOptionDto
                {
                    Value = type,
                    Label = FormatNodeTypeLabel(type),
                })
                .ToList(),
            Departments = departments,
            Positions = positions,
            Employees = employees,
            ParentNodes = nodes
                .Select(node => new FormalStructureParentOptionDto
                {
                    Id = node.IdNodoEstructura,
                    Code = node.CodigoNodo,
                    Label = node.NombreNodo,
                    Meta = $"{FormatNodeTypeLabel(node.TipoNodo)}{(node.Activo ? string.Empty : " · Inactivo")}",
                    Activo = node.Activo,
                })
                .ToList(),
        };
    }

    public static List<FormalStructureFlatNodeDto> ListFlatNodes(
        SqlConnection connection,
        FormalStructureListOptions? options = null)
    {
        EnsureSchema(connection);
        RrhhSupport.EnsureEmployeeProfileSchema(connection);

        var normalized = options ?? new FormalStructureListOptions();
        var rows = LoadRows(connection);

        return rows
            .Where(row => normalized.IncludeInactive || row.Activo)
            .Where(row => MatchesFlatFilters(row, normalized))
            .OrderBy(row => GetTypeRank(row.TipoNodo))
            .ThenBy(row => row.OrdenVisual)
            .ThenBy(row => row.NombreNodo)
            .ThenBy(row => row.CodigoNodo)
            .Select(MapFlatNode)
            .ToList();
    }

    public static FormalStructureTreeResponseDto GetTree(
        SqlConnection connection,
        FormalStructureTreeOptions? options = null)
    {
        EnsureSchema(connection);
        RrhhSupport.EnsureEmployeeProfileSchema(connection);

        var normalized = options ?? new FormalStructureTreeOptions();
        var allRows = LoadRows(connection);
        var workingRows = allRows
            .Where(row => normalized.IncludeInactive || row.Activo)
            .ToList();

        var fullTree = BuildTree(workingRows);
        var filteredTree = ApplyFilters(fullTree, normalized);
        var filteredRows = Flatten(filteredTree).ToList();

        return new FormalStructureTreeResponseDto
        {
            Summary = BuildSummary(filteredRows),
            Branches = BuildBranches(workingRows),
            Tree = filteredTree,
            GeneralManagementName = FindLeadNodeName(workingRows),
        };
    }

    public static FormalStructureDetailDto? GetNode(SqlConnection connection, long idNodoEstructura)
    {
        EnsureSchema(connection);
        RrhhSupport.EnsureEmployeeProfileSchema(connection);

        var rows = LoadRows(connection);
        var row = rows.FirstOrDefault(item => item.IdNodoEstructura == idNodoEstructura);
        if (row is null)
        {
            return null;
        }

        var tree = BuildTree(rows.Where(item => item.Activo).ToList());
        var treeLookup = Flatten(tree).ToDictionary(item => item.IdNodoEstructura);
        var activeTreeNode = treeLookup.GetValueOrDefault(idNodoEstructura);
        var breadcrumb = BuildBreadcrumb(rows, row.IdNodoEstructura);

        return new FormalStructureDetailDto
        {
            IdNodoEstructura = row.IdNodoEstructura,
            CodigoNodo = row.CodigoNodo,
            NombreNodo = row.NombreNodo,
            TipoNodo = row.TipoNodo,
            TipoNodoLabel = FormatNodeTypeLabel(row.TipoNodo),
            IdNodoPadre = row.IdNodoPadre,
            NombreNodoPadre = row.NombreNodoPadre,
            TipoNodoPadre = row.TipoNodoPadre,
            TipoNodoPadreLabel = FormatNodeTypeLabel(row.TipoNodoPadre),
            IdEmpleadoTitular = row.IdEmpleadoTitular,
            CodigoEmpleadoTitular = row.CodigoEmpleadoTitular,
            NombreEmpleadoTitular = row.NombreEmpleadoTitular,
            FotoPerfilUrl = row.FotoPerfilUrl,
            IdDepartamento = row.DisplayDepartmentId,
            NombreDepartamento = row.DisplayDepartmentName,
            IdCargo = row.DisplayPositionId,
            NombreCargo = row.DisplayPositionName,
            OrdenVisual = row.OrdenVisual,
            Activo = row.Activo,
            Observacion = row.Observacion,
            FechaRegistro = row.FechaRegistro.ToString("yyyy-MM-ddTHH:mm:ss"),
            FechaActualizacion = row.FechaActualizacion?.ToString("yyyy-MM-ddTHH:mm:ss"),
            UsuarioRegistro = row.UsuarioRegistro,
            UsuarioActualizacion = row.UsuarioActualizacion,
            DirectChildCount = activeTreeNode?.DirectChildCount ?? rows.Count(item => item.IdNodoPadre == row.IdNodoEstructura && item.Activo),
            TotalBranchCount = activeTreeNode is null ? 1 : 1 + activeTreeNode.TotalDescendantCount,
            Breadcrumb = breadcrumb,
        };
    }

    public static long CreateNode(
        SqlConnection connection,
        SqlTransaction transaction,
        FormalStructureSaveModel model,
        string user)
    {
        ValidateSaveModel(connection, transaction, null, model);

        const string sql = """
            INSERT INTO rrhh.estructura_organizativa_nodo
            (
                codigo_nodo,
                nombre_nodo,
                tipo_nodo,
                id_nodo_padre,
                id_empleado_titular,
                id_departamento,
                id_cargo,
                orden_visual,
                activo,
                observacion,
                usuario_registro
            )
            OUTPUT INSERTED.id_nodo_estructura
            VALUES
            (
                @codigo_nodo,
                @nombre_nodo,
                @tipo_nodo,
                @id_nodo_padre,
                @id_empleado_titular,
                @id_departamento,
                @id_cargo,
                @orden_visual,
                @activo,
                @observacion,
                @usuario_registro
            );
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        AssignSaveParameters(command, model, user, isUpdate: false);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public static void UpdateNode(
        SqlConnection connection,
        SqlTransaction transaction,
        long idNodoEstructura,
        FormalStructureSaveModel model,
        string user)
    {
        ValidateSaveModel(connection, transaction, idNodoEstructura, model);

        const string sql = """
            UPDATE rrhh.estructura_organizativa_nodo
            SET
                codigo_nodo = @codigo_nodo,
                nombre_nodo = @nombre_nodo,
                tipo_nodo = @tipo_nodo,
                id_nodo_padre = @id_nodo_padre,
                id_empleado_titular = @id_empleado_titular,
                id_departamento = @id_departamento,
                id_cargo = @id_cargo,
                orden_visual = @orden_visual,
                activo = @activo,
                observacion = @observacion,
                fecha_actualizacion = SYSDATETIME(),
                usuario_actualizacion = @usuario_actualizacion
            WHERE id_nodo_estructura = @id_nodo_estructura;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        AssignSaveParameters(command, model, user, isUpdate: true);
        command.Parameters.Add("@id_nodo_estructura", SqlDbType.BigInt).Value = idNodoEstructura;
        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows == 0)
        {
            throw new InvalidOperationException("El nodo seleccionado ya no existe.");
        }
    }

    public static void DeleteNode(SqlConnection connection, SqlTransaction transaction, long idNodoEstructura)
    {
        if (!NodeExists(connection, transaction, idNodoEstructura))
        {
            throw new InvalidOperationException("El nodo seleccionado ya no existe.");
        }

        const string childSql = """
            SELECT COUNT(1)
            FROM rrhh.estructura_organizativa_nodo
            WHERE id_nodo_padre = @id_nodo_estructura;
            """;

        using (var childCommand = new SqlCommand(childSql, connection, transaction))
        {
            childCommand.Parameters.Add("@id_nodo_estructura", SqlDbType.BigInt).Value = idNodoEstructura;
            var childCount = Convert.ToInt32(childCommand.ExecuteScalar() ?? 0);
            if (childCount > 0)
            {
                throw new InvalidOperationException("El nodo tiene subordinados o unidades hijas asociadas. Elimina o reasigna primero esos nodos.");
            }
        }

        using var command = new SqlCommand(
            "DELETE FROM rrhh.estructura_organizativa_nodo WHERE id_nodo_estructura = @id_nodo_estructura;",
            connection,
            transaction);
        command.Parameters.Add("@id_nodo_estructura", SqlDbType.BigInt).Value = idNodoEstructura;
        command.ExecuteNonQuery();
    }

    public static FormalStructureEmployeeContextDto? GetEmployeeContext(SqlConnection connection, long idEmpleado)
    {
        EnsureSchema(connection);
        RrhhSupport.EnsureEmployeeProfileSchema(connection);

        var rows = LoadRows(connection)
            .Where(row => row.Activo && row.IdEmpleadoTitular == idEmpleado)
            .ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        var allRows = LoadRows(connection);
        var lookup = allRows.ToDictionary(item => item.IdNodoEstructura);
        var selected = rows
            .OrderByDescending(row => GetDepth(row, lookup))
            .ThenByDescending(row => GetTypeRank(row.TipoNodo))
            .ThenBy(row => row.OrdenVisual)
            .First();

        lookup.TryGetValue(selected.IdNodoPadre ?? 0, out var parent);
        var breadcrumb = BuildBreadcrumb(allRows, selected.IdNodoEstructura);

        return new FormalStructureEmployeeContextDto
        {
            IdNodoEstructura = selected.IdNodoEstructura,
            CodigoNodo = selected.CodigoNodo,
            NombreNodo = selected.NombreNodo,
            TipoNodo = selected.TipoNodo,
            TipoNodoLabel = FormatNodeTypeLabel(selected.TipoNodo),
            NombreDepartamento = selected.DisplayDepartmentName,
            NombreCargo = selected.DisplayPositionName,
            RutaOrganizativa = breadcrumb.Count == 0 ? null : string.Join(" > ", breadcrumb.Select(item => item.Label)),
            ReportaFormalmenteA = parent is null ? null : BuildFormalParentLabel(parent),
            NombreNodoPadre = parent?.NombreNodo,
            TipoNodoPadre = parent?.TipoNodo,
            TipoNodoPadreLabel = FormatNodeTypeLabel(parent?.TipoNodo),
            TitularNodoPadre = parent?.NombreEmpleadoTitular,
        };
    }

    public static DemoSeedResult SeedBaseStructure(SqlConnection connection, SqlTransaction transaction, string user)
    {
        EnsureSchema(connection);

        using var countCommand = new SqlCommand(
            "SELECT COUNT(1) FROM rrhh.estructura_organizativa_nodo;",
            connection,
            transaction);
        var existingCount = Convert.ToInt32(countCommand.ExecuteScalar() ?? 0);
        if (existingCount > 0)
        {
            return new DemoSeedResult
            {
                InsertedCount = 0,
                Skipped = true,
                Message = "La estructura formal ya contiene nodos. La carga base se omite para no sobrescribir informacion existente.",
            };
        }

        var seedNodes = BuildSeedNodes();
        var employeeIds = LoadCodeIdMap(connection, transaction, "rrhh.empleado", "codigo_empleado", "id_empleado");
        var departmentIds = LoadCodeIdMap(connection, transaction, "rrhh.departamento", "codigo_departamento", "id_departamento");
        var positionIds = LoadCodeIdMap(connection, transaction, "rrhh.cargo", "codigo_cargo", "id_cargo");
        var idsByCode = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var insertedCount = 0;

        foreach (var seedNode in seedNodes)
        {
            var model = new FormalStructureSaveModel
            {
                CodigoNodo = seedNode.Code,
                NombreNodo = seedNode.Name,
                TipoNodo = seedNode.Type,
                IdNodoPadre = string.IsNullOrWhiteSpace(seedNode.ParentCode)
                    ? null
                    : idsByCode.GetValueOrDefault(seedNode.ParentCode!),
                IdEmpleadoTitular = ResolveSeedId(employeeIds, seedNode.EmployeeCode),
                IdDepartamento = ResolveSeedId(departmentIds, seedNode.DepartmentCode),
                IdCargo = ResolveSeedId(positionIds, seedNode.PositionCode),
                OrdenVisual = seedNode.VisualOrder,
                Activo = true,
                Observacion = seedNode.Note,
            };

            var id = CreateNode(connection, transaction, model, user);
            idsByCode[seedNode.Code] = id;
            insertedCount += 1;
        }

        return new DemoSeedResult
        {
            InsertedCount = insertedCount,
            Skipped = false,
            Message = $"Se cargaron {insertedCount} nodos base de referencia en la estructura organizativa formal.",
        };
    }

    private static Dictionary<string, long> LoadCodeIdMap(
        SqlConnection connection,
        SqlTransaction transaction,
        string tableName,
        string codeColumn,
        string idColumn)
    {
        using var command = new SqlCommand(
            $"""
            SELECT {codeColumn}, {idColumn}
            FROM {tableName};
            """,
            connection,
            transaction);

        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                continue;
            }

            var code = reader.GetString(0).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            result[code] = reader.GetInt64(1);
        }

        return result;
    }

    private static long? ResolveSeedId(Dictionary<string, long> idsByCode, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return idsByCode.TryGetValue(code.Trim(), out var id) ? id : null;
    }

    private static List<FormalStructureNodeDto> BuildTree(List<FormalStructureRow> rows)
    {
        var childrenLookup = rows.ToLookup(row => row.IdNodoPadre ?? 0);

        List<FormalStructureNodeDto> BuildChildren(long? parentId, List<string> parentTrail)
        {
            var items = childrenLookup[parentId ?? 0]
                .OrderBy(row => row.OrdenVisual)
                .ThenBy(row => GetTypeRank(row.TipoNodo))
                .ThenBy(row => row.NombreNodo)
                .ThenBy(row => row.CodigoNodo)
                .ToList();

            if (items.Count == 0)
            {
                return [];
            }

            var result = new List<FormalStructureNodeDto>();
            foreach (var row in items)
            {
                var breadcrumb = new List<string>(parentTrail) { row.NombreNodo };
                var children = BuildChildren(row.IdNodoEstructura, breadcrumb);
                var node = MapTreeNode(row);
                node.Breadcrumb = breadcrumb;
                node.Children = children;
                node.DirectChildCount = children.Count;
                node.TotalDescendantCount = children.Sum(item => 1 + item.TotalDescendantCount);
                result.Add(node);
            }

            return result;
        }

        return BuildChildren(null, []);
    }

    private static List<FormalStructureNodeDto> ApplyFilters(
        List<FormalStructureNodeDto> tree,
        FormalStructureTreeOptions options)
    {
        var activeTree = tree;

        if (options.BranchNodeId.HasValue)
        {
            var branchNode = Flatten(activeTree)
                .FirstOrDefault(item => item.IdNodoEstructura == options.BranchNodeId.Value);
            activeTree = branchNode is null ? [] : [branchNode];
        }

        var hasSearch = !string.IsNullOrWhiteSpace(options.Search);
        var hasDepartment = options.IdDepartamento.HasValue && options.IdDepartamento.Value > 0;
        if (!hasSearch && !hasDepartment)
        {
            return activeTree;
        }

        var filtered = new List<FormalStructureNodeDto>();
        foreach (var node in activeTree)
        {
            var pruned = PruneNode(node, options);
            if (pruned is not null)
            {
                filtered.Add(pruned);
            }
        }

        return filtered;
    }

    private static FormalStructureNodeDto? PruneNode(
        FormalStructureNodeDto node,
        FormalStructureTreeOptions options)
    {
        var children = new List<FormalStructureNodeDto>();
        foreach (var child in node.Children)
        {
            var prunedChild = PruneNode(child, options);
            if (prunedChild is not null)
            {
                children.Add(prunedChild);
            }
        }

        var matchesSearch = MatchesSearch(node, options.Search);
        var matchesDepartment = !options.IdDepartamento.HasValue || options.IdDepartamento.Value <= 0
            ? true
            : node.IdDepartamento == options.IdDepartamento.Value;
        var keep = (matchesSearch && matchesDepartment) || children.Count > 0;

        if (!keep)
        {
            return null;
        }

        return new FormalStructureNodeDto
        {
            IdNodoEstructura = node.IdNodoEstructura,
            CodigoNodo = node.CodigoNodo,
            NombreNodo = node.NombreNodo,
            TipoNodo = node.TipoNodo,
            TipoNodoLabel = node.TipoNodoLabel,
            IdNodoPadre = node.IdNodoPadre,
            NombreNodoPadre = node.NombreNodoPadre,
            IdEmpleadoTitular = node.IdEmpleadoTitular,
            CodigoEmpleadoTitular = node.CodigoEmpleadoTitular,
            NombreEmpleadoTitular = node.NombreEmpleadoTitular,
            FotoPerfilUrl = node.FotoPerfilUrl,
            IdDepartamento = node.IdDepartamento,
            NombreDepartamento = node.NombreDepartamento,
            IdCargo = node.IdCargo,
            NombreCargo = node.NombreCargo,
            OrdenVisual = node.OrdenVisual,
            Activo = node.Activo,
            Observacion = node.Observacion,
            Breadcrumb = [.. node.Breadcrumb],
            Children = children,
            DirectChildCount = children.Count,
            TotalDescendantCount = children.Sum(item => 1 + item.TotalDescendantCount),
        };
    }

    private static bool MatchesFlatFilters(FormalStructureRow row, FormalStructureListOptions options)
    {
        if (options.IdDepartamento.HasValue && options.IdDepartamento.Value > 0 && row.DisplayDepartmentId != options.IdDepartamento.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.TipoNodo) &&
            !string.Equals(row.TipoNodo, options.TipoNodo.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.Search))
        {
            return true;
        }

        var search = options.Search.Trim();
        return ContainsText(row.CodigoNodo, search) ||
            ContainsText(row.NombreNodo, search) ||
            ContainsText(row.TipoNodo, search) ||
            ContainsText(row.NombreEmpleadoTitular, search) ||
            ContainsText(row.CodigoEmpleadoTitular, search) ||
            ContainsText(row.DisplayDepartmentName, search) ||
            ContainsText(row.DisplayPositionName, search);
    }

    private static bool MatchesSearch(FormalStructureNodeDto node, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var normalized = search.Trim();
        return ContainsText(node.CodigoNodo, normalized) ||
            ContainsText(node.NombreNodo, normalized) ||
            ContainsText(node.TipoNodoLabel, normalized) ||
            ContainsText(node.NombreEmpleadoTitular, normalized) ||
            ContainsText(node.CodigoEmpleadoTitular, normalized) ||
            ContainsText(node.NombreDepartamento, normalized) ||
            ContainsText(node.NombreCargo, normalized);
    }

    private static bool ContainsText(string? source, string search) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static FormalStructureSummaryDto BuildSummary(IReadOnlyCollection<FormalStructureNodeDto> nodes)
    {
        return new FormalStructureSummaryDto
        {
            TotalNodes = nodes.Count,
            NodesWithTitular = nodes.Count(node => node.IdEmpleadoTitular.HasValue),
            VacantNodes = nodes.Count(node => string.Equals(node.TipoNodo, "VACANTE", StringComparison.OrdinalIgnoreCase) || !node.IdEmpleadoTitular.HasValue),
            ManagementCount = nodes.Count(node => string.Equals(node.TipoNodo, "GERENCIA", StringComparison.OrdinalIgnoreCase)),
            HeadquartersCount = nodes.Count(node => string.Equals(node.TipoNodo, "JEFATURA", StringComparison.OrdinalIgnoreCase)),
            CoordinationCount = nodes.Count(node => string.Equals(node.TipoNodo, "COORDINACION", StringComparison.OrdinalIgnoreCase)),
            PositionCount = nodes.Count(node => string.Equals(node.TipoNodo, "PUESTO", StringComparison.OrdinalIgnoreCase)),
            UnitCount = nodes.Count(node => string.Equals(node.TipoNodo, "UNIDAD", StringComparison.OrdinalIgnoreCase)),
            ActiveNodes = nodes.Count(node => node.Activo),
        };
    }

    private static List<FormalStructureBranchDto> BuildBranches(List<FormalStructureRow> rows)
    {
        var roots = rows
            .Where(row => row.Activo)
            .Where(row =>
                string.Equals(row.TipoNodo, "GERENCIA_GENERAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.TipoNodo, "VICEGERENCIA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.TipoNodo, "GERENCIA", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => GetTypeRank(row.TipoNodo))
            .ThenBy(row => row.OrdenVisual)
            .ThenBy(row => row.NombreNodo)
            .ToList();

        return roots
            .Select(row => new FormalStructureBranchDto
            {
                Key = $"NODE-{row.IdNodoEstructura}",
                NodeId = row.IdNodoEstructura,
                Label = row.NombreNodo,
                Subtitle = BuildBranchSubtitle(row),
                EmployeeCount = rows.Count(item => item.Activo && IsWithinBranch(item.IdNodoEstructura, row.IdNodoEstructura, rows)),
            })
            .ToList();
    }

    private static bool IsWithinBranch(long nodeId, long branchRootId, IReadOnlyList<FormalStructureRow> rows)
    {
        var lookup = rows.ToDictionary(item => item.IdNodoEstructura);
        var current = lookup.GetValueOrDefault(nodeId);

        while (current is not null)
        {
            if (current.IdNodoEstructura == branchRootId)
            {
                return true;
            }

            if (!current.IdNodoPadre.HasValue)
            {
                return false;
            }

            current = lookup.GetValueOrDefault(current.IdNodoPadre.Value);
        }

        return false;
    }

    private static string? FindLeadNodeName(List<FormalStructureRow> rows)
    {
        var lead = rows.FirstOrDefault(row => row.Activo &&
            string.Equals(row.TipoNodo, "GERENCIA_GENERAL", StringComparison.OrdinalIgnoreCase));
        return lead?.NombreNodo;
    }

    private static string BuildBranchSubtitle(FormalStructureRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.NombreEmpleadoTitular))
        {
            return row.NombreEmpleadoTitular!;
        }

        return row.DisplayDepartmentName ??
            row.DisplayPositionName ??
            FormatNodeTypeLabel(row.TipoNodo);
    }

    private static FormalStructureFlatNodeDto MapFlatNode(FormalStructureRow row)
    {
        return new FormalStructureFlatNodeDto
        {
            IdNodoEstructura = row.IdNodoEstructura,
            CodigoNodo = row.CodigoNodo,
            NombreNodo = row.NombreNodo,
            TipoNodo = row.TipoNodo,
            TipoNodoLabel = FormatNodeTypeLabel(row.TipoNodo),
            IdNodoPadre = row.IdNodoPadre,
            NombreNodoPadre = row.NombreNodoPadre,
            IdEmpleadoTitular = row.IdEmpleadoTitular,
            CodigoEmpleadoTitular = row.CodigoEmpleadoTitular,
            NombreEmpleadoTitular = row.NombreEmpleadoTitular,
            FotoPerfilUrl = row.FotoPerfilUrl,
            IdDepartamento = row.DisplayDepartmentId,
            NombreDepartamento = row.DisplayDepartmentName,
            IdCargo = row.DisplayPositionId,
            NombreCargo = row.DisplayPositionName,
            OrdenVisual = row.OrdenVisual,
            Activo = row.Activo,
            Observacion = row.Observacion,
        };
    }

    private static FormalStructureNodeDto MapTreeNode(FormalStructureRow row)
    {
        return new FormalStructureNodeDto
        {
            IdNodoEstructura = row.IdNodoEstructura,
            CodigoNodo = row.CodigoNodo,
            NombreNodo = row.NombreNodo,
            TipoNodo = row.TipoNodo,
            TipoNodoLabel = FormatNodeTypeLabel(row.TipoNodo),
            IdNodoPadre = row.IdNodoPadre,
            NombreNodoPadre = row.NombreNodoPadre,
            IdEmpleadoTitular = row.IdEmpleadoTitular,
            CodigoEmpleadoTitular = row.CodigoEmpleadoTitular,
            NombreEmpleadoTitular = row.NombreEmpleadoTitular,
            FotoPerfilUrl = row.FotoPerfilUrl,
            IdDepartamento = row.DisplayDepartmentId,
            NombreDepartamento = row.DisplayDepartmentName,
            IdCargo = row.DisplayPositionId,
            NombreCargo = row.DisplayPositionName,
            OrdenVisual = row.OrdenVisual,
            Activo = row.Activo,
            Observacion = row.Observacion,
        };
    }

    private static IEnumerable<FormalStructureNodeDto> Flatten(IEnumerable<FormalStructureNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private static List<FormalStructureBreadcrumbDto> BuildBreadcrumb(IReadOnlyList<FormalStructureRow> rows, long idNodoEstructura)
    {
        var lookup = rows.ToDictionary(item => item.IdNodoEstructura);
        var breadcrumb = new List<FormalStructureBreadcrumbDto>();
        var visited = new HashSet<long>();
        var current = lookup.GetValueOrDefault(idNodoEstructura);

        while (current is not null && visited.Add(current.IdNodoEstructura))
        {
            breadcrumb.Add(new FormalStructureBreadcrumbDto
            {
                Id = current.IdNodoEstructura,
                Label = current.NombreNodo,
                Type = current.TipoNodo,
                TypeLabel = FormatNodeTypeLabel(current.TipoNodo),
            });

            current = current.IdNodoPadre.HasValue
                ? lookup.GetValueOrDefault(current.IdNodoPadre.Value)
                : null;
        }

        breadcrumb.Reverse();
        return breadcrumb;
    }

    private static int GetDepth(FormalStructureRow row, IReadOnlyDictionary<long, FormalStructureRow> lookup)
    {
        var depth = 0;
        var visited = new HashSet<long>();
        var current = row;

        while (current.IdNodoPadre.HasValue && lookup.TryGetValue(current.IdNodoPadre.Value, out var parent))
        {
            if (!visited.Add(parent.IdNodoEstructura))
            {
                break;
            }

            depth += 1;
            current = parent;
        }

        return depth;
    }

    private static string BuildFormalParentLabel(FormalStructureRow row)
    {
        return string.IsNullOrWhiteSpace(row.NombreEmpleadoTitular)
            ? row.NombreNodo
            : $"{row.NombreNodo} · {row.NombreEmpleadoTitular}";
    }

    private static int GetTypeRank(string? type) => (type ?? string.Empty).ToUpperInvariant() switch
    {
        "ASAMBLEA" => 1,
        "JUNTA_DIRECTIVA" => 2,
        "GERENCIA_GENERAL" => 3,
        "VICEGERENCIA" => 4,
        "GERENCIA" => 5,
        "JEFATURA" => 6,
        "COORDINACION" => 7,
        "UNIDAD" => 8,
        "PUESTO" => 9,
        "APOYO" => 10,
        "VACANTE" => 11,
        _ => 99,
    };

    public static string FormatNodeTypeLabel(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "Sin tipo";
        }

        return type
            .Trim()
            .Replace("_", " ", StringComparison.Ordinal)
            .ToLowerInvariant() switch
        {
            "asamblea" => "Asamblea",
            "junta directiva" => "Junta Directiva",
            "gerencia general" => "Gerencia General",
            "vicegerencia" => "Vicegerencia",
            "gerencia" => "Gerencia",
            "jefatura" => "Jefatura",
            "coordinacion" => "Coordinacion",
            "unidad" => "Unidad",
            "puesto" => "Puesto",
            "apoyo" => "Apoyo",
            "vacante" => "Vacante",
            var value => value,
        };
    }

    private static void ValidateSaveModel(
        SqlConnection connection,
        SqlTransaction transaction,
        long? currentNodeId,
        FormalStructureSaveModel model)
    {
        if (string.IsNullOrWhiteSpace(model.CodigoNodo))
        {
            throw new InvalidOperationException("Ingresa el codigo del nodo.");
        }

        var codigoNodo = model.CodigoNodo.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigoNodo, "^[A-Z0-9_-]{2,50}$"))
        {
            throw new InvalidOperationException("El codigo del nodo solo puede usar letras, numeros, guion y guion bajo.");
        }

        if (string.IsNullOrWhiteSpace(model.NombreNodo))
        {
            throw new InvalidOperationException("Ingresa el nombre del nodo.");
        }

        var nombreNodo = model.NombreNodo.Trim();
        if (nombreNodo.Length < 3 || nombreNodo.Length > 200)
        {
            throw new InvalidOperationException("El nombre del nodo debe tener entre 3 y 200 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(model.TipoNodo) || !AllowedNodeTypeSet.Contains(model.TipoNodo.Trim()))
        {
            throw new InvalidOperationException("Selecciona un tipo de nodo valido.");
        }

        if (model.IdNodoPadre.HasValue && currentNodeId.HasValue && model.IdNodoPadre.Value == currentNodeId.Value)
        {
            throw new InvalidOperationException("El nodo padre no puede ser el mismo nodo.");
        }

        if (model.OrdenVisual < 0)
        {
            throw new InvalidOperationException("El orden visual no puede ser negativo.");
        }

        if (!string.IsNullOrWhiteSpace(model.Observacion) && model.Observacion.Trim().Length > 500)
        {
            throw new InvalidOperationException("La observacion supera el limite permitido.");
        }

        if (model.IdNodoPadre.HasValue && !NodeExists(connection, transaction, model.IdNodoPadre.Value))
        {
            throw new InvalidOperationException("Selecciona un nodo padre valido.");
        }

        if (CodeExists(connection, transaction, codigoNodo, currentNodeId))
        {
            throw new InvalidOperationException("El codigo del nodo ya existe.");
        }

        if (string.Equals(model.TipoNodo?.Trim(), "VACANTE", StringComparison.OrdinalIgnoreCase) &&
            model.IdEmpleadoTitular.HasValue)
        {
            throw new InvalidOperationException("Un nodo VACANTE no puede tener empleado titular asignado.");
        }

        if (model.IdEmpleadoTitular.HasValue && !EmployeeExists(connection, transaction, model.IdEmpleadoTitular.Value))
        {
            throw new InvalidOperationException("Selecciona un empleado titular activo y vigente.");
        }

        if (model.IdDepartamento.HasValue && !CatalogExists(connection, transaction, "rrhh.departamento", "id_departamento", model.IdDepartamento.Value))
        {
            throw new InvalidOperationException("Selecciona un departamento valido.");
        }

        if (model.IdCargo.HasValue && !CatalogExists(connection, transaction, "rrhh.cargo", "id_cargo", model.IdCargo.Value))
        {
            throw new InvalidOperationException("Selecciona un cargo valido.");
        }

        if (currentNodeId.HasValue && model.IdNodoPadre.HasValue && WouldCreateCycle(connection, transaction, currentNodeId.Value, model.IdNodoPadre.Value))
        {
            throw new InvalidOperationException("La asignacion del nodo padre crea un ciclo en la estructura organizativa.");
        }

        if (model.IdCargo.HasValue && model.IdDepartamento.HasValue &&
            !CargoBelongsToDepartment(connection, transaction, model.IdCargo.Value, model.IdDepartamento.Value))
        {
            throw new InvalidOperationException("El cargo indicado no pertenece al departamento seleccionado.");
        }

        if (model.IdEmpleadoTitular.HasValue)
        {
            var employee = GetEmployeeAssignmentSnapshot(connection, transaction, model.IdEmpleadoTitular.Value);
            if (employee is null)
            {
                throw new InvalidOperationException("Selecciona un empleado titular activo y vigente.");
            }

            if (model.IdDepartamento.HasValue && employee.IdDepartamento != model.IdDepartamento.Value)
            {
                throw new InvalidOperationException("El departamento indicado no coincide con el departamento actual del titular.");
            }

            if (model.IdCargo.HasValue && employee.IdCargo != model.IdCargo.Value)
            {
                throw new InvalidOperationException("El cargo indicado no coincide con el cargo actual del titular.");
            }
        }
    }

    private static void AssignSaveParameters(SqlCommand command, FormalStructureSaveModel model, string user, bool isUpdate)
    {
        command.Parameters.Add("@codigo_nodo", SqlDbType.NVarChar, 50).Value = model.CodigoNodo.Trim().ToUpperInvariant();
        command.Parameters.Add("@nombre_nodo", SqlDbType.NVarChar, 200).Value = model.NombreNodo.Trim();
        command.Parameters.Add("@tipo_nodo", SqlDbType.NVarChar, 40).Value = model.TipoNodo.Trim().ToUpperInvariant();
        command.Parameters.Add("@id_nodo_padre", SqlDbType.BigInt).Value = (object?)model.IdNodoPadre ?? DBNull.Value;
        command.Parameters.Add("@id_empleado_titular", SqlDbType.BigInt).Value = (object?)model.IdEmpleadoTitular ?? DBNull.Value;
        command.Parameters.Add("@id_departamento", SqlDbType.BigInt).Value = (object?)model.IdDepartamento ?? DBNull.Value;
        command.Parameters.Add("@id_cargo", SqlDbType.BigInt).Value = (object?)model.IdCargo ?? DBNull.Value;
        command.Parameters.Add("@orden_visual", SqlDbType.Int).Value = model.OrdenVisual;
        command.Parameters.Add("@activo", SqlDbType.Bit).Value = model.Activo;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value = RrhhSupport.ToDbValue(model.Observacion);

        if (isUpdate)
        {
            command.Parameters.Add("@usuario_actualizacion", SqlDbType.NVarChar, 100).Value = user;
            return;
        }

        command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value = user;
    }

    private static bool EmployeeExists(SqlConnection connection, SqlTransaction transaction, long idEmpleado)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.id_empleado = @id
              AND e.activo = 1
              AND e.fecha_baja IS NULL
              AND UPPER(COALESCE(ee.codigo_estado_empleado, N'')) <> N'RETIRADO';
            """,
            connection,
            transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = idEmpleado;
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    private static bool CatalogExists(SqlConnection connection, SqlTransaction transaction, string tableName, string idField, long idValue)
    {
        using var command = new SqlCommand(
            $"SELECT COUNT(1) FROM {tableName} WHERE {idField} = @id AND activo = 1;",
            connection,
            transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = idValue;
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    private static bool NodeExists(SqlConnection connection, SqlTransaction transaction, long idNodoEstructura)
    {
        using var command = new SqlCommand(
            "SELECT COUNT(1) FROM rrhh.estructura_organizativa_nodo WHERE id_nodo_estructura = @id;",
            connection,
            transaction);
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = idNodoEstructura;
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    private static bool CodeExists(
        SqlConnection connection,
        SqlTransaction transaction,
        string codigoNodo,
        long? exceptNodeId)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.estructura_organizativa_nodo
            WHERE codigo_nodo = @codigo_nodo
              AND (@except_id IS NULL OR id_nodo_estructura <> @except_id);
            """,
            connection,
            transaction);
        command.Parameters.Add("@codigo_nodo", SqlDbType.NVarChar, 50).Value = codigoNodo;
        command.Parameters.Add("@except_id", SqlDbType.BigInt).Value = (object?)exceptNodeId ?? DBNull.Value;
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    private static bool CargoBelongsToDepartment(
        SqlConnection connection,
        SqlTransaction transaction,
        long idCargo,
        long idDepartamento)
    {
        using var command = new SqlCommand(
            """
            SELECT COUNT(1)
            FROM rrhh.cargo
            WHERE id_cargo = @id_cargo
              AND id_departamento = @id_departamento
              AND activo = 1;
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_cargo", SqlDbType.BigInt).Value = idCargo;
        command.Parameters.Add("@id_departamento", SqlDbType.BigInt).Value = idDepartamento;
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    private static EmployeeAssignmentSnapshot? GetEmployeeAssignmentSnapshot(
        SqlConnection connection,
        SqlTransaction transaction,
        long idEmpleado)
    {
        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                e.id_departamento,
                e.id_cargo
            FROM rrhh.empleado e
            LEFT JOIN rrhh.estado_empleado ee
                ON ee.id_estado_empleado = e.id_estado_empleado
            WHERE e.id_empleado = @id_empleado
              AND e.activo = 1
              AND e.fecha_baja IS NULL
              AND UPPER(COALESCE(ee.codigo_estado_empleado, N'')) <> N'RETIRADO';
            """,
            connection,
            transaction);
        command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new EmployeeAssignmentSnapshot
        {
            IdDepartamento = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0),
            IdCargo = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1),
        };
    }

    private static bool WouldCreateCycle(
        SqlConnection connection,
        SqlTransaction transaction,
        long currentNodeId,
        long proposedParentId)
    {
        const string sql = """
            SELECT id_nodo_estructura, id_nodo_padre
            FROM rrhh.estructura_organizativa_nodo;
            """;

        using var command = new SqlCommand(sql, connection, transaction);
        using var reader = command.ExecuteReader();
        var parents = new Dictionary<long, long?>();

        while (reader.Read())
        {
            parents[reader.GetInt64(0)] = reader.IsDBNull(1) ? null : reader.GetInt64(1);
        }

        var visited = new HashSet<long>();
        long? current = proposedParentId;
        while (current.HasValue && visited.Add(current.Value))
        {
            if (current.Value == currentNodeId)
            {
                return true;
            }

            current = parents.GetValueOrDefault(current.Value);
        }

        return false;
    }

    private static List<FormalStructureRow> LoadRows(SqlConnection connection)
    {
        const string sql = """
            SELECT
                n.id_nodo_estructura,
                n.codigo_nodo,
                n.nombre_nodo,
                n.tipo_nodo,
                n.id_nodo_padre,
                padre.nombre_nodo AS nombre_nodo_padre,
                padre.tipo_nodo AS tipo_nodo_padre,
                n.id_empleado_titular,
                emp.codigo_empleado,
                COALESCE(NULLIF(emp.nombre_completo, N''), CONCAT(emp.nombres, N' ', emp.apellidos)) AS nombre_empleado_titular,
                emp.foto_perfil_url,
                n.id_departamento,
                dep.nombre_departamento,
                n.id_cargo,
                cargo.nombre_cargo,
                emp.id_departamento AS id_departamento_empleado,
                dep_emp.nombre_departamento AS nombre_departamento_empleado,
                emp.id_cargo AS id_cargo_empleado,
                cargo_emp.nombre_cargo AS nombre_cargo_empleado,
                n.orden_visual,
                n.activo,
                n.observacion,
                n.fecha_registro,
                n.fecha_actualizacion,
                n.usuario_registro,
                n.usuario_actualizacion
            FROM rrhh.estructura_organizativa_nodo n
            LEFT JOIN rrhh.estructura_organizativa_nodo padre
                ON padre.id_nodo_estructura = n.id_nodo_padre
            LEFT JOIN rrhh.empleado emp
                ON emp.id_empleado = n.id_empleado_titular
            LEFT JOIN rrhh.departamento dep
                ON dep.id_departamento = n.id_departamento
            LEFT JOIN rrhh.cargo cargo
                ON cargo.id_cargo = n.id_cargo
            LEFT JOIN rrhh.departamento dep_emp
                ON dep_emp.id_departamento = emp.id_departamento
            LEFT JOIN rrhh.cargo cargo_emp
                ON cargo_emp.id_cargo = emp.id_cargo
            ORDER BY
                CASE WHEN n.id_nodo_padre IS NULL THEN 0 ELSE 1 END,
                n.orden_visual,
                n.nombre_nodo,
                n.id_nodo_estructura;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var rows = new List<FormalStructureRow>();

        while (reader.Read())
        {
            var idDepartamentoNodo = reader.IsDBNull(11) ? (long?)null : reader.GetInt64(11);
            var nombreDepartamentoNodo = reader.IsDBNull(12) ? null : reader.GetString(12);
            var idCargoNodo = reader.IsDBNull(13) ? (long?)null : reader.GetInt64(13);
            var nombreCargoNodo = reader.IsDBNull(14) ? null : reader.GetString(14);
            var idDepartamentoEmpleado = reader.IsDBNull(15) ? (long?)null : reader.GetInt64(15);
            var nombreDepartamentoEmpleado = reader.IsDBNull(16) ? null : reader.GetString(16);
            var idCargoEmpleado = reader.IsDBNull(17) ? (long?)null : reader.GetInt64(17);
            var nombreCargoEmpleado = reader.IsDBNull(18) ? null : reader.GetString(18);

            rows.Add(new FormalStructureRow
            {
                IdNodoEstructura = reader.GetInt64(0),
                CodigoNodo = reader.GetString(1),
                NombreNodo = reader.GetString(2),
                TipoNodo = reader.GetString(3),
                IdNodoPadre = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4),
                NombreNodoPadre = reader.IsDBNull(5) ? null : reader.GetString(5),
                TipoNodoPadre = reader.IsDBNull(6) ? null : reader.GetString(6),
                IdEmpleadoTitular = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7),
                CodigoEmpleadoTitular = reader.IsDBNull(8) ? null : reader.GetString(8),
                NombreEmpleadoTitular = reader.IsDBNull(9) ? null : reader.GetString(9),
                FotoPerfilUrl = reader.IsDBNull(10) ? null : reader.GetString(10),
                IdDepartamento = idDepartamentoNodo,
                NombreDepartamento = nombreDepartamentoNodo,
                IdCargo = idCargoNodo,
                NombreCargo = nombreCargoNodo,
                DisplayDepartmentId = idDepartamentoNodo ?? idDepartamentoEmpleado,
                DisplayDepartmentName = nombreDepartamentoNodo ?? nombreDepartamentoEmpleado,
                DisplayPositionId = idCargoNodo ?? idCargoEmpleado,
                DisplayPositionName = nombreCargoNodo ?? nombreCargoEmpleado,
                OrdenVisual = reader.GetInt32(19),
                Activo = reader.GetBoolean(20),
                Observacion = reader.IsDBNull(21) ? null : reader.GetString(21),
                FechaRegistro = reader.GetDateTime(22),
                FechaActualizacion = reader.IsDBNull(23) ? (DateTime?)null : reader.GetDateTime(23),
                UsuarioRegistro = reader.IsDBNull(24) ? null : reader.GetString(24),
                UsuarioActualizacion = reader.IsDBNull(25) ? null : reader.GetString(25),
            });
        }

        return rows;
    }

    private static List<SeedNodeDefinition> BuildSeedNodes() =>
    [
        new("A", "Asamblea General de Accionistas", "ASAMBLEA", null, 10, "Nodo base institucional"),
        new("B", "Junta Directiva", "JUNTA_DIRECTIVA", "A", 20, "Nodo base institucional"),
        new("C", "Gerencia General", "GERENCIA_GENERAL", "B", 30, "Nodo base institucional", "BAT011", "ADM", "GER_GNR"),
        new("D", "Vicegerencia General", "VICEGERENCIA", "C", 40, "Nodo base institucional"),

        new("GF", "Gerencia Financiera", "GERENCIA", "D", 100, "Nodo base institucional", "BAT001", "FIN", "GER_FIN"),
        new("GO", "Gerencia de Operaciones", "GERENCIA", "D", 110, "Nodo base institucional"),
        new("GT", "Gerencia de Tecnologia", "GERENCIA", "D", 120, "Nodo base institucional", null, "TEC", null),
        new("GRH", "Gerencia de Recursos Humanos", "GERENCIA", "D", 130, "Nodo base institucional", null, "RRHH", null),
        new("GC", "Gerencia de Credito", "GERENCIA", "D", 140, "Nodo base institucional", null, "CRE", null),
        new("GN", "Gerencia de Negocios", "GERENCIA", "D", 150, "Nodo base institucional"),

        new("JF1", "Jefatura de Contabilidad", "JEFATURA", "GF", 200, "Nodo base institucional", null, "CON", null),
        new("JF2", "Jefatura de Tesoreria", "JEFATURA", "GF", 210, "Nodo base institucional"),
        new("CF1", "Coordinacion de Finanzas", "COORDINACION", "GF", 220, "Nodo base institucional"),

        new("JO1", "Jefatura de Operaciones", "JEFATURA", "GO", 230, "Nodo base institucional"),
        new("CO1", "Coordinacion de Sucursales", "COORDINACION", "GO", 240, "Nodo base institucional"),

        new("JT1", "Jefatura de Sistemas", "JEFATURA", "GT", 250, "Nodo base institucional", null, "TEC", null),
        new("JT2", "Jefatura de Seguridad de Informacion", "JEFATURA", "GT", 260, "Nodo base institucional"),
        new("CT1", "Coordinacion de Soporte", "COORDINACION", "GT", 270, "Nodo base institucional"),

        new("JRH1", "Jefatura de Administracion de Personal", "JEFATURA", "GRH", 280, "Nodo base institucional"),
        new("CRH1", "Coordinacion de Nomina y Beneficios", "COORDINACION", "GRH", 290, "Nodo base institucional"),

        new("JC1", "Jefatura de Analisis de Credito", "JEFATURA", "GC", 300, "Nodo base institucional"),
        new("CC1", "Coordinacion de Cartera", "COORDINACION", "GC", 310, "Nodo base institucional", "BAT003", "CRE", "COORD_CRED"),

        new("JN1", "Jefatura Comercial", "JEFATURA", "GN", 320, "Nodo base institucional"),
        new("CN1", "Coordinacion de Ventas", "COORDINACION", "GN", 330, "Nodo base institucional"),

        new("P1", "Contador General", "PUESTO", "JF1", 400, "Nodo base institucional", "BAT010", "CON", "CONTADOR"),
        new("P2", "Auxiliar Contable", "PUESTO", "JF1", 410, "Nodo base institucional"),
        new("P3", "Analista de Sistemas", "PUESTO", "JT1", 420, "Nodo base institucional", "BAT008", "TEC", "ANL_SIS"),
        new("P4", "Soporte Tecnico", "PUESTO", "CT1", 430, "Nodo base institucional"),
        new("P5", "Analista de Nomina", "PUESTO", "CRH1", 440, "Nodo base institucional"),
        new("P6", "Oficial de Credito I", "PUESTO", "CC1", 450, "Nodo base institucional", "BAT004", "CRE", "OFI_CRED"),
        new("P7", "Oficial de Credito II", "PUESTO", "CC1", 460, "Nodo base institucional", "BAT005", "CRE", "OFI_CRED"),
        new("P8", "Gestor de Cobranza", "PUESTO", "CC1", 470, "Nodo base institucional", "BAT009", "COB", "GEST_COB"),
    ];

    public sealed class FormalStructureListOptions
    {
        public string? Search { get; set; }
        public long? IdDepartamento { get; set; }
        public string? TipoNodo { get; set; }
        public bool IncludeInactive { get; set; }
    }

    public sealed class FormalStructureTreeOptions
    {
        public string? Search { get; set; }
        public long? IdDepartamento { get; set; }
        public long? BranchNodeId { get; set; }
        public bool IncludeInactive { get; set; }
    }

    public sealed class FormalStructureSaveModel
    {
        public string CodigoNodo { get; set; } = string.Empty;
        public string NombreNodo { get; set; } = string.Empty;
        public string TipoNodo { get; set; } = string.Empty;
        public long? IdNodoPadre { get; set; }
        public long? IdEmpleadoTitular { get; set; }
        public long? IdDepartamento { get; set; }
        public long? IdCargo { get; set; }
        public int OrdenVisual { get; set; }
        public bool Activo { get; set; } = true;
        public string? Observacion { get; set; }
    }

    public sealed class FormalStructureCatalogsDto
    {
        public List<FormalStructureTypeOptionDto> NodeTypes { get; set; } = [];
        public List<FormalStructureOptionDto> Departments { get; set; } = [];
        public List<FormalStructureOptionDto> Positions { get; set; } = [];
        public List<FormalStructureOptionDto> Employees { get; set; } = [];
        public List<FormalStructureParentOptionDto> ParentNodes { get; set; } = [];
    }

    public sealed class FormalStructureTypeOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class FormalStructureOptionDto
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Meta { get; set; }
    }

    public sealed class FormalStructureParentOptionDto : FormalStructureOptionDto
    {
        public bool Activo { get; set; }
    }

    public sealed class FormalStructureTreeResponseDto
    {
        public FormalStructureSummaryDto Summary { get; set; } = new();
        public List<FormalStructureBranchDto> Branches { get; set; } = [];
        public List<FormalStructureNodeDto> Tree { get; set; } = [];
        public string? GeneralManagementName { get; set; }
    }

    public sealed class FormalStructureSummaryDto
    {
        public int TotalNodes { get; set; }
        public int NodesWithTitular { get; set; }
        public int VacantNodes { get; set; }
        public int ManagementCount { get; set; }
        public int HeadquartersCount { get; set; }
        public int CoordinationCount { get; set; }
        public int PositionCount { get; set; }
        public int UnitCount { get; set; }
        public int ActiveNodes { get; set; }
    }

    public sealed class FormalStructureBranchDto
    {
        public string Key { get; set; } = string.Empty;
        public long NodeId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public int EmployeeCount { get; set; }
    }

    public class FormalStructureFlatNodeDto
    {
        public long IdNodoEstructura { get; set; }
        public string CodigoNodo { get; set; } = string.Empty;
        public string NombreNodo { get; set; } = string.Empty;
        public string TipoNodo { get; set; } = string.Empty;
        public string TipoNodoLabel { get; set; } = string.Empty;
        public long? IdNodoPadre { get; set; }
        public string? NombreNodoPadre { get; set; }
        public long? IdEmpleadoTitular { get; set; }
        public string? CodigoEmpleadoTitular { get; set; }
        public string? NombreEmpleadoTitular { get; set; }
        public string? FotoPerfilUrl { get; set; }
        public long? IdDepartamento { get; set; }
        public string? NombreDepartamento { get; set; }
        public long? IdCargo { get; set; }
        public string? NombreCargo { get; set; }
        public int OrdenVisual { get; set; }
        public bool Activo { get; set; }
        public string? Observacion { get; set; }
    }

    public sealed class FormalStructureNodeDto : FormalStructureFlatNodeDto
    {
        public List<string> Breadcrumb { get; set; } = [];
        public int DirectChildCount { get; set; }
        public int TotalDescendantCount { get; set; }
        public List<FormalStructureNodeDto> Children { get; set; } = [];
    }

    public sealed class FormalStructureDetailDto : FormalStructureFlatNodeDto
    {
        public string? TipoNodoPadre { get; set; }
        public string? TipoNodoPadreLabel { get; set; }
        public string? FechaRegistro { get; set; }
        public string? FechaActualizacion { get; set; }
        public string? UsuarioRegistro { get; set; }
        public string? UsuarioActualizacion { get; set; }
        public int DirectChildCount { get; set; }
        public int TotalBranchCount { get; set; }
        public List<FormalStructureBreadcrumbDto> Breadcrumb { get; set; } = [];
    }

    public sealed class FormalStructureBreadcrumbDto
    {
        public long Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
    }

    public sealed class FormalStructureEmployeeContextDto
    {
        public long IdNodoEstructura { get; set; }
        public string CodigoNodo { get; set; } = string.Empty;
        public string NombreNodo { get; set; } = string.Empty;
        public string TipoNodo { get; set; } = string.Empty;
        public string TipoNodoLabel { get; set; } = string.Empty;
        public string? NombreDepartamento { get; set; }
        public string? NombreCargo { get; set; }
        public string? RutaOrganizativa { get; set; }
        public string? ReportaFormalmenteA { get; set; }
        public string? NombreNodoPadre { get; set; }
        public string? TipoNodoPadre { get; set; }
        public string? TipoNodoPadreLabel { get; set; }
        public string? TitularNodoPadre { get; set; }
    }

    public sealed class DemoSeedResult
    {
        public int InsertedCount { get; set; }
        public bool Skipped { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    private sealed class FormalStructureRow
    {
        public long IdNodoEstructura { get; set; }
        public string CodigoNodo { get; set; } = string.Empty;
        public string NombreNodo { get; set; } = string.Empty;
        public string TipoNodo { get; set; } = string.Empty;
        public long? IdNodoPadre { get; set; }
        public string? NombreNodoPadre { get; set; }
        public string? TipoNodoPadre { get; set; }
        public long? IdEmpleadoTitular { get; set; }
        public string? CodigoEmpleadoTitular { get; set; }
        public string? NombreEmpleadoTitular { get; set; }
        public string? FotoPerfilUrl { get; set; }
        public long? IdDepartamento { get; set; }
        public string? NombreDepartamento { get; set; }
        public long? IdCargo { get; set; }
        public string? NombreCargo { get; set; }
        public long? DisplayDepartmentId { get; set; }
        public string? DisplayDepartmentName { get; set; }
        public long? DisplayPositionId { get; set; }
        public string? DisplayPositionName { get; set; }
        public int OrdenVisual { get; set; }
        public bool Activo { get; set; }
        public string? Observacion { get; set; }
        public DateTime FechaRegistro { get; set; }
        public DateTime? FechaActualizacion { get; set; }
        public string? UsuarioRegistro { get; set; }
        public string? UsuarioActualizacion { get; set; }
    }

    private sealed class EmployeeAssignmentSnapshot
    {
        public long? IdDepartamento { get; set; }
        public long? IdCargo { get; set; }
    }

    private sealed record SeedNodeDefinition(
        string Code,
        string Name,
        string Type,
        string? ParentCode,
        int VisualOrder,
        string? Note,
        string? EmployeeCode = null,
        string? DepartmentCode = null,
        string? PositionCode = null);
}
