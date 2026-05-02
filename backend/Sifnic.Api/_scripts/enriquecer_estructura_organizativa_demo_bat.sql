/*
  Enriquece la estructura base institucional ya cargada en rrhh.estructura_organizativa_nodo
  con departamentos, cargos y titulares BAT* para pruebas integrales.

  Uso:
  - Ejecutar despues de la demo base cuando ya existan los nodos A..P8
    o parte de ellos.
  - Inserta los puestos faltantes P6, P7 y P8 si no existen.
*/

IF OBJECT_ID(N'rrhh.estructura_organizativa_nodo', N'U') IS NULL
BEGIN
    RAISERROR(N'La tabla rrhh.estructura_organizativa_nodo no existe.', 16, 1);
    RETURN;
END;

DECLARE @cc1 BIGINT =
(
    SELECT TOP (1) id_nodo_estructura
    FROM rrhh.estructura_organizativa_nodo
    WHERE codigo_nodo = N'CC1'
);

IF @cc1 IS NULL
BEGIN
    RAISERROR(N'No existe el nodo CC1. Ejecuta primero la estructura base institucional.', 16, 1);
    RETURN;
END;

DECLARE @usuario NVARCHAR(100) = N'seed.demo.enriquecido';

INSERT INTO rrhh.estructura_organizativa_nodo
(
    codigo_nodo,
    nombre_nodo,
    tipo_nodo,
    id_nodo_padre,
    orden_visual,
    activo,
    observacion,
    usuario_registro
)
SELECT N'P6', N'Oficial de Credito I', N'PUESTO', @cc1, 450, 1, N'Nodo base institucional enlazado', @usuario
WHERE NOT EXISTS
(
    SELECT 1
    FROM rrhh.estructura_organizativa_nodo
    WHERE codigo_nodo = N'P6'
);

INSERT INTO rrhh.estructura_organizativa_nodo
(
    codigo_nodo,
    nombre_nodo,
    tipo_nodo,
    id_nodo_padre,
    orden_visual,
    activo,
    observacion,
    usuario_registro
)
SELECT N'P7', N'Oficial de Credito II', N'PUESTO', @cc1, 460, 1, N'Nodo base institucional enlazado', @usuario
WHERE NOT EXISTS
(
    SELECT 1
    FROM rrhh.estructura_organizativa_nodo
    WHERE codigo_nodo = N'P7'
);

INSERT INTO rrhh.estructura_organizativa_nodo
(
    codigo_nodo,
    nombre_nodo,
    tipo_nodo,
    id_nodo_padre,
    orden_visual,
    activo,
    observacion,
    usuario_registro
)
SELECT N'P8', N'Gestor de Cobranza', N'PUESTO', @cc1, 470, 1, N'Nodo base institucional enlazado', @usuario
WHERE NOT EXISTS
(
    SELECT 1
    FROM rrhh.estructura_organizativa_nodo
    WHERE codigo_nodo = N'P8'
);

DECLARE @asignaciones TABLE
(
    codigo_nodo NVARCHAR(50) NOT NULL PRIMARY KEY,
    codigo_empleado NVARCHAR(50) NULL,
    codigo_departamento NVARCHAR(50) NULL,
    codigo_cargo NVARCHAR(50) NULL
);

INSERT INTO @asignaciones(codigo_nodo, codigo_empleado, codigo_departamento, codigo_cargo)
VALUES
    (N'C', N'BAT011', N'ADM', N'GER_GNR'),
    (N'GF', N'BAT001', N'FIN', N'GER_FIN'),
    (N'GT', NULL, N'TEC', NULL),
    (N'GRH', NULL, N'RRHH', NULL),
    (N'GC', NULL, N'CRE', NULL),
    (N'JF1', NULL, N'CON', NULL),
    (N'JT1', NULL, N'TEC', NULL),
    (N'CC1', N'BAT003', N'CRE', N'COORD_CRED'),
    (N'P1', N'BAT010', N'CON', N'CONTADOR'),
    (N'P3', N'BAT008', N'TEC', N'ANL_SIS'),
    (N'P6', N'BAT004', N'CRE', N'OFI_CRED'),
    (N'P7', N'BAT005', N'CRE', N'OFI_CRED'),
    (N'P8', N'BAT009', N'COB', N'GEST_COB');

UPDATE nodo
SET
    nodo.id_empleado_titular = emp.id_empleado,
    nodo.id_departamento = dep.id_departamento,
    nodo.id_cargo = cargo.id_cargo,
    nodo.fecha_actualizacion = SYSDATETIME(),
    nodo.usuario_actualizacion = @usuario
FROM rrhh.estructura_organizativa_nodo nodo
INNER JOIN @asignaciones asignacion
    ON asignacion.codigo_nodo = nodo.codigo_nodo
LEFT JOIN rrhh.empleado emp
    ON emp.codigo_empleado = asignacion.codigo_empleado
LEFT JOIN rrhh.departamento dep
    ON dep.codigo_departamento = asignacion.codigo_departamento
LEFT JOIN rrhh.cargo cargo
    ON cargo.codigo_cargo = asignacion.codigo_cargo;

PRINT N'Estructura base institucional enlazada con titulares, departamentos y cargos BAT*.';
