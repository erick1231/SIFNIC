/*
  Script sugerido para una siguiente fase.
  No se aplica automaticamente desde la app.

  Motivo:
  La BD actual solo tiene rrhh.empleado_supervision, que sirve para linea
  de reporte inmediata, pero no para un organigrama formal con nodos no-empleado
  como Asamblea, Junta Directiva, plazas vacantes o unidades sin titular.
*/

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
