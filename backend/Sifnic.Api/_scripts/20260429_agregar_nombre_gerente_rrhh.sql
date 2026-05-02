IF COL_LENGTH('empresa.configuracion_general', 'nombre_gerente_rrhh') IS NULL
BEGIN
    ALTER TABLE empresa.configuracion_general
    ADD nombre_gerente_rrhh NVARCHAR(300) NULL;
END;
