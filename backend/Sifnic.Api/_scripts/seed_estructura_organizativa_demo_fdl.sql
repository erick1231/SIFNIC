/*
  Estructura base institucional para rrhh.estructura_organizativa_nodo
  Basado en el organigrama mermaid compartido por RRHH.

  Notas:
  - Inserta solo si la tabla formal esta vacia.
  - La demo queda enlazada con departamentos, cargos y titulares BAT*
    cuando esos catalogos existen para facilitar pruebas de portal,
    filtros y organigrama formal.
*/

IF OBJECT_ID(N'rrhh.estructura_organizativa_nodo', N'U') IS NULL
BEGIN
    RAISERROR(N'La tabla rrhh.estructura_organizativa_nodo no existe.', 16, 1);
    RETURN;
END;

IF EXISTS (SELECT 1 FROM rrhh.estructura_organizativa_nodo)
BEGIN
    PRINT N'La estructura formal ya contiene datos. No se inserta la estructura base.';
    RETURN;
END;

DECLARE @ids TABLE
(
    codigo_nodo NVARCHAR(50) NOT NULL PRIMARY KEY,
    id_nodo_estructura BIGINT NOT NULL
);

DECLARE @usuario NVARCHAR(100) = N'seed.demo';

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
OUTPUT inserted.codigo_nodo, inserted.id_nodo_estructura INTO @ids(codigo_nodo, id_nodo_estructura)
VALUES
    (N'A', N'Asamblea General de Accionistas', N'ASAMBLEA', NULL, NULL, NULL, NULL, 10, 1, N'Nodo base institucional', @usuario),
    (N'B', N'Junta Directiva', N'JUNTA_DIRECTIVA', NULL, NULL, NULL, NULL, 20, 1, N'Nodo base institucional', @usuario),
    (N'C', N'Gerencia General', N'GERENCIA_GENERAL', NULL, NULL, NULL, NULL, 30, 1, N'Nodo base institucional', @usuario),
    (N'D', N'Vicegerencia General', N'VICEGERENCIA', NULL, NULL, NULL, NULL, 40, 1, N'Nodo base institucional', @usuario);

UPDATE n
SET n.id_nodo_padre = padre.id_nodo_estructura
FROM rrhh.estructura_organizativa_nodo n
INNER JOIN @ids actual ON actual.id_nodo_estructura = n.id_nodo_estructura
INNER JOIN @ids padre ON padre.codigo_nodo =
    CASE actual.codigo_nodo
        WHEN N'B' THEN N'A'
        WHEN N'C' THEN N'B'
        WHEN N'D' THEN N'C'
    END
WHERE actual.codigo_nodo IN (N'B', N'C', N'D');

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
OUTPUT inserted.codigo_nodo, inserted.id_nodo_estructura INTO @ids(codigo_nodo, id_nodo_estructura)
SELECT demo.codigo_nodo, demo.nombre_nodo, demo.tipo_nodo, padre.id_nodo_estructura, demo.orden_visual, 1, N'Nodo base institucional', @usuario
FROM
(
    VALUES
        (N'GF', N'Gerencia Financiera', N'GERENCIA', N'D', 100),
        (N'GO', N'Gerencia de Operaciones', N'GERENCIA', N'D', 110),
        (N'GT', N'Gerencia de Tecnologia', N'GERENCIA', N'D', 120),
        (N'GRH', N'Gerencia de Recursos Humanos', N'GERENCIA', N'D', 130),
        (N'GC', N'Gerencia de Credito', N'GERENCIA', N'D', 140),
        (N'GN', N'Gerencia de Negocios', N'GERENCIA', N'D', 150)
) demo(codigo_nodo, nombre_nodo, tipo_nodo, codigo_padre, orden_visual)
INNER JOIN @ids padre
    ON padre.codigo_nodo = demo.codigo_padre;

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
OUTPUT inserted.codigo_nodo, inserted.id_nodo_estructura INTO @ids(codigo_nodo, id_nodo_estructura)
SELECT demo.codigo_nodo, demo.nombre_nodo, demo.tipo_nodo, padre.id_nodo_estructura, demo.orden_visual, 1, N'Nodo base institucional', @usuario
FROM
(
    VALUES
        (N'JF1', N'Jefatura de Contabilidad', N'JEFATURA', N'GF', 200),
        (N'JF2', N'Jefatura de Tesoreria', N'JEFATURA', N'GF', 210),
        (N'CF1', N'Coordinacion de Finanzas', N'COORDINACION', N'GF', 220),

        (N'JO1', N'Jefatura de Operaciones', N'JEFATURA', N'GO', 230),
        (N'CO1', N'Coordinacion de Sucursales', N'COORDINACION', N'GO', 240),

        (N'JT1', N'Jefatura de Sistemas', N'JEFATURA', N'GT', 250),
        (N'JT2', N'Jefatura de Seguridad de Informacion', N'JEFATURA', N'GT', 260),
        (N'CT1', N'Coordinacion de Soporte', N'COORDINACION', N'GT', 270),

        (N'JRH1', N'Jefatura de Administracion de Personal', N'JEFATURA', N'GRH', 280),
        (N'CRH1', N'Coordinacion de Nomina y Beneficios', N'COORDINACION', N'GRH', 290),

        (N'JC1', N'Jefatura de Analisis de Credito', N'JEFATURA', N'GC', 300),
        (N'CC1', N'Coordinacion de Cartera', N'COORDINACION', N'GC', 310),

        (N'JN1', N'Jefatura Comercial', N'JEFATURA', N'GN', 320),
        (N'CN1', N'Coordinacion de Ventas', N'COORDINACION', N'GN', 330)
) demo(codigo_nodo, nombre_nodo, tipo_nodo, codigo_padre, orden_visual)
INNER JOIN @ids padre
    ON padre.codigo_nodo = demo.codigo_padre;

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
OUTPUT inserted.codigo_nodo, inserted.id_nodo_estructura INTO @ids(codigo_nodo, id_nodo_estructura)
SELECT demo.codigo_nodo, demo.nombre_nodo, demo.tipo_nodo, padre.id_nodo_estructura, demo.orden_visual, 1, N'Nodo base institucional', @usuario
FROM
(
    VALUES
        (N'P1', N'Contador General', N'PUESTO', N'JF1', 400),
        (N'P2', N'Auxiliar Contable', N'PUESTO', N'JF1', 410),
        (N'P3', N'Analista de Sistemas', N'PUESTO', N'JT1', 420),
        (N'P4', N'Soporte Tecnico', N'PUESTO', N'CT1', 430),
        (N'P5', N'Analista de Nomina', N'PUESTO', N'CRH1', 440),
        (N'P6', N'Oficial de Credito I', N'PUESTO', N'CC1', 450),
        (N'P7', N'Oficial de Credito II', N'PUESTO', N'CC1', 460),
        (N'P8', N'Gestor de Cobranza', N'PUESTO', N'CC1', 470)
) demo(codigo_nodo, nombre_nodo, tipo_nodo, codigo_padre, orden_visual)
INNER JOIN @ids padre
    ON padre.codigo_nodo = demo.codigo_padre;

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
    nodo.id_cargo = cargo.id_cargo
FROM rrhh.estructura_organizativa_nodo nodo
INNER JOIN @asignaciones asignacion
    ON asignacion.codigo_nodo = nodo.codigo_nodo
LEFT JOIN rrhh.empleado emp
    ON emp.codigo_empleado = asignacion.codigo_empleado
LEFT JOIN rrhh.departamento dep
    ON dep.codigo_departamento = asignacion.codigo_departamento
LEFT JOIN rrhh.cargo cargo
    ON cargo.codigo_cargo = asignacion.codigo_cargo;

PRINT N'Estructura base institucional insertada correctamente en rrhh.estructura_organizativa_nodo.';
