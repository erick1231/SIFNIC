/*
  Bateria integral de datos de prueba para SIFNIC.
  Objetivo:
  - RRHH operativo
  - Mi Portal
  - Bandeja Supervisor
  - Nomina
  - Contratos por tipo
  - Horas extra, vacaciones y permisos
  - Variables, deducciones y prestamos
  - Periodos de nomina listos para procesar

  Credenciales de prueba:
  - usuarios: batadmin, batjefe, batcoord, batof1, batof2, batpas, batserv, batprof, battemp, batliq
  - clave comun: Prueba123!

  Nota:
  - Respeta tablas y procedimientos existentes.
  - Es idempotente: puede ejecutarse varias veces sin duplicar el set.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @usuarioSeed NVARCHAR(100) = N'SEED_BATERIA_20260428';
    DECLARE @claveHashComun NVARCHAR(500) = N'PBKDF2SHA1|100000|iPvPZVWViXfkqTHXk33Ksw==|vvf/mCPglsemeUfsYum3tsFWYpaeCzIkYkf8KDu1YPY=';

    IF OBJECT_ID(N'rrhh.empleado_supervision', N'U') IS NULL
    BEGIN
        CREATE TABLE rrhh.empleado_supervision
        (
            id_empleado_supervision BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_rrhh_empleado_supervision PRIMARY KEY,
            id_empleado BIGINT NOT NULL,
            id_supervisor_empleado BIGINT NOT NULL,
            fecha_asignacion DATE NOT NULL
                CONSTRAINT DF_rrhh_empleado_supervision_fecha_asignacion DEFAULT CAST(GETDATE() AS DATE),
            activo BIT NOT NULL
                CONSTRAINT DF_rrhh_empleado_supervision_activo DEFAULT 1,
            fecha_registro DATETIME2 NOT NULL
                CONSTRAINT DF_rrhh_empleado_supervision_fecha_registro DEFAULT SYSDATETIME(),
            fecha_actualizacion DATETIME2 NULL,
            usuario_registro NVARCHAR(100) NULL,
            usuario_actualizacion NVARCHAR(100) NULL,
            CONSTRAINT FK_rrhh_empleado_supervision_empleado
                FOREIGN KEY (id_empleado) REFERENCES rrhh.empleado(id_empleado),
            CONSTRAINT FK_rrhh_empleado_supervision_supervisor
                FOREIGN KEY (id_supervisor_empleado) REFERENCES rrhh.empleado(id_empleado),
            CONSTRAINT CK_rrhh_empleado_supervision_distinto
                CHECK (id_empleado <> id_supervisor_empleado)
        );

        CREATE UNIQUE INDEX UX_rrhh_empleado_supervision_activa
            ON rrhh.empleado_supervision (id_empleado)
            WHERE activo = 1;

        CREATE INDEX IX_rrhh_empleado_supervision_supervisor_activo
            ON rrhh.empleado_supervision (id_supervisor_empleado, activo, id_empleado);
    END;

    DECLARE @idDeptAdmin BIGINT = (SELECT TOP (1) id_departamento FROM rrhh.departamento WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Administracion');
    DECLARE @idDeptRrhh BIGINT = (SELECT TOP (1) id_departamento FROM rrhh.departamento WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Recursos Humanos');
    DECLARE @idDeptCredito BIGINT = (SELECT TOP (1) id_departamento FROM rrhh.departamento WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Credito');
    DECLARE @idDeptCobranza BIGINT = (SELECT TOP (1) id_departamento FROM rrhh.departamento WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Cobranza');
    DECLARE @idDeptTecnologia BIGINT = (SELECT TOP (1) id_departamento FROM rrhh.departamento WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Tecnologia');
    DECLARE @idDeptContabilidad BIGINT = (SELECT TOP (1) id_departamento FROM rrhh.departamento WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Contabilidad');

    DECLARE @idCargoGerenteFin BIGINT = (SELECT TOP (1) id_cargo FROM rrhh.cargo WHERE nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Gerente Financiero');
    DECLARE @idCargoJefeCredito BIGINT = (SELECT TOP (1) id_cargo FROM rrhh.cargo WHERE nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Jefe de Credito');
    DECLARE @idCargoOficialCredito BIGINT = (SELECT TOP (1) id_cargo FROM rrhh.cargo WHERE nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Oficial de Credito');
    DECLARE @idCargoGestorCobranza BIGINT = (SELECT TOP (1) id_cargo FROM rrhh.cargo WHERE nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Gestor de Cobranza');
    DECLARE @idCargoAnalistaSistemas BIGINT = (SELECT TOP (1) id_cargo FROM rrhh.cargo WHERE nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Analista de Sistemas');
    DECLARE @idCargoContador BIGINT = (SELECT TOP (1) id_cargo FROM rrhh.cargo WHERE nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Contador General');

    DECLARE @idBancoBase BIGINT = (SELECT TOP (1) id_banco FROM rrhh.banco WHERE nombre_banco = N'BAC');
    IF @idBancoBase IS NULL
        SET @idBancoBase = (SELECT TOP (1) id_banco FROM rrhh.banco ORDER BY id_banco);

    DECLARE @idHorarioAdm BIGINT = (SELECT TOP (1) id_horario_laboral FROM rrhh.horario_laboral WHERE codigo_horario = N'ADM_LV');
    DECLARE @idHorarioOperativo BIGINT = (SELECT TOP (1) id_horario_laboral FROM rrhh.horario_laboral WHERE codigo_horario = N'OP_LS');

    DECLARE @idTipoFijo BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'FIJO');
    DECLARE @idTipoTemporal BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'TEMPORAL');
    DECLARE @idTipoServicios BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'SERVICIOS');
    DECLARE @idTipoPasantia BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'PASANTIA');
    DECLARE @idTipoIndeterminado BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'INDETERMINADO');
    DECLARE @idTipoProfNat BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'PROFESIONAL_PERSONA_NATURAL');

    DECLARE @idTipoPermisoConGoce BIGINT = (SELECT TOP (1) id_tipo_permiso FROM rrhh.tipo_permiso WHERE codigo_tipo_permiso = N'CON_GOCE');
    DECLARE @idTipoPermisoSinGoce BIGINT = (SELECT TOP (1) id_tipo_permiso FROM rrhh.tipo_permiso WHERE codigo_tipo_permiso = N'SIN_GOCE');
    DECLARE @idTipoHoraExtraDiurna BIGINT = (SELECT TOP (1) id_tipo_hora_extra FROM rrhh.tipo_hora_extra WHERE codigo_tipo_hora_extra = N'HE_DIURNA');
    DECLARE @idTipoHoraExtraNocturna BIGINT = (SELECT TOP (1) id_tipo_hora_extra FROM rrhh.tipo_hora_extra WHERE codigo_tipo_hora_extra = N'HE_NOCTURNA');

    DECLARE @idRolAdministracion BIGINT = (SELECT TOP (1) id_rol FROM seguridad.rol WHERE codigo_rol = N'ADMINISTRACION');
    DECLARE @idRolSupervisor BIGINT = (SELECT TOP (1) id_rol FROM seguridad.rol WHERE codigo_rol = N'SUPERVISOR');
    DECLARE @idRolCredito BIGINT = (SELECT TOP (1) id_rol FROM seguridad.rol WHERE codigo_rol = N'CREDITO');
    DECLARE @idRolJefeCredito BIGINT = (SELECT TOP (1) id_rol FROM seguridad.rol WHERE codigo_rol = N'JEFE_CREDITO');
    DECLARE @idRolOficialCredito BIGINT = (SELECT TOP (1) id_rol FROM seguridad.rol WHERE codigo_rol = N'OFICIAL_CREDITO');

    DECLARE @idConceptoOtroDevengado BIGINT = (SELECT TOP (1) id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N'OTRO_DEVENGADO');
    DECLARE @idConceptoFondoAhorro BIGINT = (SELECT TOP (1) id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N'FONDO_AHORRO');

    IF @idDeptAdmin IS NULL OR @idDeptRrhh IS NULL OR @idDeptCredito IS NULL OR @idDeptCobranza IS NULL
       OR @idDeptTecnologia IS NULL OR @idDeptContabilidad IS NULL
       OR @idCargoGerenteFin IS NULL OR @idCargoJefeCredito IS NULL OR @idCargoOficialCredito IS NULL
       OR @idCargoGestorCobranza IS NULL OR @idCargoAnalistaSistemas IS NULL OR @idCargoContador IS NULL
       OR @idBancoBase IS NULL OR @idHorarioAdm IS NULL OR @idHorarioOperativo IS NULL
       OR @idTipoFijo IS NULL OR @idTipoTemporal IS NULL OR @idTipoServicios IS NULL OR @idTipoPasantia IS NULL
       OR @idTipoIndeterminado IS NULL OR @idTipoProfNat IS NULL
       OR @idTipoPermisoConGoce IS NULL OR @idTipoPermisoSinGoce IS NULL
       OR @idTipoHoraExtraDiurna IS NULL OR @idTipoHoraExtraNocturna IS NULL
       OR @idConceptoOtroDevengado IS NULL OR @idConceptoFondoAhorro IS NULL
    BEGIN
        THROW 51010, 'No se encontraron todos los catalogos requeridos para la bateria de pruebas.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT001')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT001',
            @id_departamento = @idDeptAdmin,
            @id_cargo = @idCargoGerenteFin,
            @cedula = N'101-010190-0001A',
            @inss = N'1010101900001',
            @nombres = N'PAOLA',
            @apellidos = N'HERRERA MOLINA',
            @fecha_nacimiento = '1990-01-01',
            @sexo = N'F',
            @estado_civil = N'CASADA',
            @telefono = N'8888-5001',
            @correo = N'bat001@sifnic.local',
            @direccion = N'Managua, Nicaragua',
            @fecha_ingreso = '2022-01-10',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000001';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT002')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT002',
            @id_departamento = @idDeptCredito,
            @id_cargo = @idCargoJefeCredito,
            @cedula = N'102-020291-0002B',
            @inss = N'1020202910002',
            @nombres = N'CARLOS',
            @apellidos = N'MENA DUARTE',
            @fecha_nacimiento = '1991-02-02',
            @sexo = N'M',
            @estado_civil = N'CASADO',
            @telefono = N'8888-5002',
            @correo = N'bat002@sifnic.local',
            @direccion = N'Matagalpa, Nicaragua',
            @fecha_ingreso = '2022-04-18',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000002';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT003')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT003',
            @id_departamento = @idDeptCredito,
            @id_cargo = @idCargoOficialCredito,
            @cedula = N'103-030392-0003C',
            @inss = N'1030303920003',
            @nombres = N'ELENA',
            @apellidos = N'ROCHA CASTILLO',
            @fecha_nacimiento = '1992-03-03',
            @sexo = N'F',
            @estado_civil = N'SOLTERA',
            @telefono = N'8888-5003',
            @correo = N'bat003@sifnic.local',
            @direccion = N'Leon, Nicaragua',
            @fecha_ingreso = '2023-01-15',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000003';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT004')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT004',
            @id_departamento = @idDeptCredito,
            @id_cargo = @idCargoOficialCredito,
            @cedula = N'104-040493-0004D',
            @inss = N'1040404930004',
            @nombres = N'IRIS',
            @apellidos = N'FLORES PEREZ',
            @fecha_nacimiento = '1993-04-04',
            @sexo = N'F',
            @estado_civil = N'SOLTERA',
            @telefono = N'8888-5004',
            @correo = N'bat004@sifnic.local',
            @direccion = N'Esteli, Nicaragua',
            @fecha_ingreso = '2024-02-01',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000004';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT005')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT005',
            @id_departamento = @idDeptCredito,
            @id_cargo = @idCargoOficialCredito,
            @cedula = N'105-050594-0005E',
            @inss = N'1050505940005',
            @nombres = N'MATEO',
            @apellidos = N'CRUZ SALGADO',
            @fecha_nacimiento = '1994-05-05',
            @sexo = N'M',
            @estado_civil = N'CASADO',
            @telefono = N'8888-5005',
            @correo = N'bat005@sifnic.local',
            @direccion = N'Jinotega, Nicaragua',
            @fecha_ingreso = '2023-07-10',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000005';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT006')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT006',
            @id_departamento = @idDeptRrhh,
            @id_cargo = @idCargoAnalistaSistemas,
            @cedula = N'106-060695-0006F',
            @inss = N'1060606950006',
            @nombres = N'NAOMI',
            @apellidos = N'LOPEZ VEGA',
            @fecha_nacimiento = '1995-06-06',
            @sexo = N'F',
            @estado_civil = N'SOLTERA',
            @telefono = N'8888-5006',
            @correo = N'bat006@sifnic.local',
            @direccion = N'Chinandega, Nicaragua',
            @fecha_ingreso = '2026-02-03',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000006';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT007')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT007',
            @id_departamento = @idDeptTecnologia,
            @id_cargo = @idCargoAnalistaSistemas,
            @cedula = N'107-070796-0007G',
            @inss = N'1070707960007',
            @nombres = N'OSCAR',
            @apellidos = N'TELLEZ RUIZ',
            @fecha_nacimiento = '1996-07-07',
            @sexo = N'M',
            @estado_civil = N'SOLTERO',
            @telefono = N'8888-5007',
            @correo = N'bat007@sifnic.local',
            @direccion = N'Managua, Nicaragua',
            @fecha_ingreso = '2026-01-20',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000007';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT008')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT008',
            @id_departamento = @idDeptTecnologia,
            @id_cargo = @idCargoAnalistaSistemas,
            @cedula = N'108-080897-0008H',
            @inss = N'1080808970008',
            @nombres = N'NORA',
            @apellidos = N'PINEDA GOMEZ',
            @fecha_nacimiento = '1997-08-08',
            @sexo = N'F',
            @estado_civil = N'SOLTERA',
            @telefono = N'8888-5008',
            @correo = N'bat008@sifnic.local',
            @direccion = N'Masaya, Nicaragua',
            @fecha_ingreso = '2026-01-20',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000008';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT009')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT009',
            @id_departamento = @idDeptCobranza,
            @id_cargo = @idCargoGestorCobranza,
            @cedula = N'109-090998-0009I',
            @inss = N'1090909980009',
            @nombres = N'SILVIA',
            @apellidos = N'TORRES RAMOS',
            @fecha_nacimiento = '1998-09-09',
            @sexo = N'F',
            @estado_civil = N'CASADA',
            @telefono = N'8888-5009',
            @correo = N'bat009@sifnic.local',
            @direccion = N'Carazo, Nicaragua',
            @fecha_ingreso = '2024-10-01',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000009';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT010')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT010',
            @id_departamento = @idDeptContabilidad,
            @id_cargo = @idCargoContador,
            @cedula = N'110-101099-0010J',
            @inss = N'1101010990010',
            @nombres = N'HECTOR',
            @apellidos = N'SALINAS MORA',
            @fecha_nacimiento = '1989-10-10',
            @sexo = N'M',
            @estado_civil = N'CASADO',
            @telefono = N'8888-5010',
            @correo = N'bat010@sifnic.local',
            @direccion = N'Granada, Nicaragua',
            @fecha_ingreso = '2021-06-14',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000010';
    END;

    DECLARE @idBat001 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT001');
    DECLARE @idBat002 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT002');
    DECLARE @idBat003 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT003');
    DECLARE @idBat004 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT004');
    DECLARE @idBat005 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT005');
    DECLARE @idBat006 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT006');
    DECLARE @idBat007 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT007');
    DECLARE @idBat008 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT008');
    DECLARE @idBat009 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT009');
    DECLARE @idBat010 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT010');

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT001')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat001,
            @id_tipo_contrato = @idTipoIndeterminado,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT001',
            @fecha_inicio = '2022-01-10',
            @fecha_fin = NULL,
            @salario_base_mensual = 42000.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: administracion.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT002')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat002,
            @id_tipo_contrato = @idTipoIndeterminado,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT002',
            @fecha_inicio = '2022-04-18',
            @fecha_fin = NULL,
            @salario_base_mensual = 28000.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: jefe de credito.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT003')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat003,
            @id_tipo_contrato = @idTipoFijo,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT003',
            @fecha_inicio = '2023-01-15',
            @fecha_fin = NULL,
            @salario_base_mensual = 22000.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: coordinacion operativa.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT004')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat004,
            @id_tipo_contrato = @idTipoTemporal,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT004',
            @fecha_inicio = '2024-02-01',
            @fecha_fin = '2026-12-31',
            @salario_base_mensual = 16000.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: oficial temporal.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT005')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat005,
            @id_tipo_contrato = @idTipoFijo,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT005',
            @fecha_inicio = '2023-07-10',
            @fecha_fin = NULL,
            @salario_base_mensual = 15500.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: oficial fijo.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT006')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat006,
            @id_tipo_contrato = @idTipoPasantia,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT006',
            @fecha_inicio = '2026-02-03',
            @fecha_fin = '2026-08-31',
            @salario_base_mensual = 6500.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: pasantia no acumula vacaciones.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT007')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat007,
            @id_tipo_contrato = @idTipoServicios,
            @id_horario_laboral = @idHorarioOperativo,
            @numero_contrato = N'CTR-BAT007',
            @fecha_inicio = '2026-01-20',
            @fecha_fin = '2026-12-31',
            @salario_base_mensual = 18000.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: servicios profesionales no acumula vacaciones.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT008')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat008,
            @id_tipo_contrato = @idTipoProfNat,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT008',
            @fecha_inicio = '2026-01-20',
            @fecha_fin = '2026-12-31',
            @salario_base_mensual = 24000.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: profesional persona natural.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT009')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat009,
            @id_tipo_contrato = @idTipoTemporal,
            @id_horario_laboral = @idHorarioOperativo,
            @numero_contrato = N'CTR-BAT009',
            @fecha_inicio = '2024-10-01',
            @fecha_fin = '2026-10-01',
            @salario_base_mensual = 13500.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: cobranza temporal.';
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT010')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat010,
            @id_tipo_contrato = @idTipoIndeterminado,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT010',
            @fecha_inicio = '2021-06-14',
            @fecha_fin = NULL,
            @salario_base_mensual = 18500.00,
            @moneda = N'NIO',
            @observacion = N'Seed bateria integral: candidato para liquidacion.';
    END;

    UPDATE seguridad.usuario
    SET
        nombres = CASE usuario
            WHEN N'batadmin' THEN N'PAOLA'
            WHEN N'batjefe' THEN N'CARLOS'
            WHEN N'batcoord' THEN N'ELENA'
            WHEN N'batof1' THEN N'IRIS'
            WHEN N'batof2' THEN N'MATEO'
            WHEN N'batpas' THEN N'NAOMI'
            WHEN N'batserv' THEN N'OSCAR'
            WHEN N'batprof' THEN N'NORA'
            WHEN N'battemp' THEN N'SILVIA'
            WHEN N'batliq' THEN N'HECTOR'
            ELSE nombres
        END,
        apellidos = CASE usuario
            WHEN N'batadmin' THEN N'HERRERA MOLINA'
            WHEN N'batjefe' THEN N'MENA DUARTE'
            WHEN N'batcoord' THEN N'ROCHA CASTILLO'
            WHEN N'batof1' THEN N'FLORES PEREZ'
            WHEN N'batof2' THEN N'CRUZ SALGADO'
            WHEN N'batpas' THEN N'LOPEZ VEGA'
            WHEN N'batserv' THEN N'TELLEZ RUIZ'
            WHEN N'batprof' THEN N'PINEDA GOMEZ'
            WHEN N'battemp' THEN N'TORRES RAMOS'
            WHEN N'batliq' THEN N'SALINAS MORA'
            ELSE apellidos
        END,
        hash_clave = @claveHashComun,
        cambiar_clave_en_proximo_inicio = 0,
        bloqueado = 0,
        activo = 1,
        intentos_fallidos = 0,
        fecha_actualizacion = SYSDATETIME()
    WHERE usuario IN (N'batadmin',N'batjefe',N'batcoord',N'batof1',N'batof2',N'batpas',N'batserv',N'batprof',N'battemp',N'batliq');

    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batadmin')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batadmin', N'PAOLA', N'HERRERA MOLINA', N'bat001@sifnic.local', N'8888-5001', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batjefe')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batjefe', N'CARLOS', N'MENA DUARTE', N'bat002@sifnic.local', N'8888-5002', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batcoord')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batcoord', N'ELENA', N'ROCHA CASTILLO', N'bat003@sifnic.local', N'8888-5003', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batof1')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batof1', N'IRIS', N'FLORES PEREZ', N'bat004@sifnic.local', N'8888-5004', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batof2')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batof2', N'MATEO', N'CRUZ SALGADO', N'bat005@sifnic.local', N'8888-5005', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batpas')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batpas', N'NAOMI', N'LOPEZ VEGA', N'bat006@sifnic.local', N'8888-5006', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batserv')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batserv', N'OSCAR', N'TELLEZ RUIZ', N'bat007@sifnic.local', N'8888-5007', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batprof')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batprof', N'NORA', N'PINEDA GOMEZ', N'bat008@sifnic.local', N'8888-5008', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'battemp')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'battemp', N'SILVIA', N'TORRES RAMOS', N'bat009@sifnic.local', N'8888-5009', @claveHashComun, 0, 0, 1, 0);
    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batliq')
        INSERT INTO seguridad.usuario (id_sucursal, usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos)
        VALUES (NULL, N'batliq', N'HECTOR', N'SALINAS MORA', N'bat010@sifnic.local', N'8888-5010', @claveHashComun, 0, 0, 1, 0);

    DECLARE @idUsuarioBatAdmin BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batadmin');
    DECLARE @idUsuarioBatJefe BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batjefe');
    DECLARE @idUsuarioBatCoord BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batcoord');
    DECLARE @idUsuarioBatOf1 BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batof1');
    DECLARE @idUsuarioBatOf2 BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batof2');
    DECLARE @idUsuarioBatPas BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batpas');
    DECLARE @idUsuarioBatServ BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batserv');
    DECLARE @idUsuarioBatProf BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batprof');
    DECLARE @idUsuarioBatTemp BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'battemp');
    DECLARE @idUsuarioBatLiq BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batliq');

    IF @idRolAdministracion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatAdmin AND id_rol = @idRolAdministracion)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatAdmin, @idRolAdministracion, 1);

    IF @idRolSupervisor IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatJefe AND id_rol = @idRolSupervisor)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatJefe, @idRolSupervisor, 1);
    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatJefe AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatJefe, @idRolCredito, 1);
    IF @idRolJefeCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatJefe AND id_rol = @idRolJefeCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatJefe, @idRolJefeCredito, 1);

    IF @idRolSupervisor IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatCoord AND id_rol = @idRolSupervisor)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatCoord, @idRolSupervisor, 1);
    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatCoord AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatCoord, @idRolCredito, 1);

    IF @idRolOficialCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatOf1 AND id_rol = @idRolOficialCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatOf1, @idRolOficialCredito, 1);
    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatOf1 AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatOf1, @idRolCredito, 1);

    IF @idRolOficialCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatOf2 AND id_rol = @idRolOficialCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatOf2, @idRolOficialCredito, 1);
    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatOf2 AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatOf2, @idRolCredito, 1);

    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatPas AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatPas, @idRolCredito, 1);
    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatServ AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatServ, @idRolCredito, 1);
    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatProf AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatProf, @idRolCredito, 1);
    IF @idRolCredito IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatTemp AND id_rol = @idRolCredito)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatTemp, @idRolCredito, 1);
    IF @idRolAdministracion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatLiq AND id_rol = @idRolAdministracion)
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo) VALUES (@idUsuarioBatLiq, @idRolAdministracion, 1);

    UPDATE rrhh.empleado_supervision
    SET activo = 0,
        fecha_actualizacion = SYSDATETIME(),
        usuario_actualizacion = @usuarioSeed
    WHERE activo = 1
      AND id_empleado IN (@idBat002, @idBat003, @idBat004, @idBat005, @idBat006, @idBat007, @idBat008, @idBat009, @idBat010)
      AND (
            (id_empleado = @idBat002 AND id_supervisor_empleado <> @idBat001) OR
            (id_empleado = @idBat003 AND id_supervisor_empleado <> @idBat002) OR
            (id_empleado = @idBat004 AND id_supervisor_empleado <> @idBat003) OR
            (id_empleado = @idBat005 AND id_supervisor_empleado <> @idBat003) OR
            (id_empleado = @idBat006 AND id_supervisor_empleado <> @idBat003) OR
            (id_empleado = @idBat007 AND id_supervisor_empleado <> @idBat001) OR
            (id_empleado = @idBat008 AND id_supervisor_empleado <> @idBat001) OR
            (id_empleado = @idBat009 AND id_supervisor_empleado <> @idBat002) OR
            (id_empleado = @idBat010 AND id_supervisor_empleado <> @idBat001)
          );

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat002 AND id_supervisor_empleado = @idBat001 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat002, @idBat001, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat003 AND id_supervisor_empleado = @idBat002 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat003, @idBat002, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat004 AND id_supervisor_empleado = @idBat003 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat004, @idBat003, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat005 AND id_supervisor_empleado = @idBat003 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat005, @idBat003, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat006 AND id_supervisor_empleado = @idBat003 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat006, @idBat003, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat007 AND id_supervisor_empleado = @idBat001 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat007, @idBat001, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat008 AND id_supervisor_empleado = @idBat001 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat008, @idBat001, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat009 AND id_supervisor_empleado = @idBat002 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat009, @idBat002, @usuarioSeed);
    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado_supervision WHERE id_empleado = @idBat010 AND id_supervisor_empleado = @idBat001 AND activo = 1)
        INSERT INTO rrhh.empleado_supervision (id_empleado, id_supervisor_empleado, usuario_registro) VALUES (@idBat010, @idBat001, @usuarioSeed);

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.vacacion
        WHERE id_empleado = @idBat004
          AND fecha_inicio = '2026-05-19'
          AND fecha_fin = '2026-05-21'
    )
    BEGIN
        EXEC rrhh.usp_solicitar_vacacion
            @id_empleado = @idBat004,
            @fecha_solicitud = '2026-05-05',
            @fecha_inicio = '2026-05-19',
            @fecha_fin = '2026-05-21',
            @dias_solicitados = 3.00,
            @observacion_solicitud = N'Vacacion pendiente para prueba de bandeja supervisor.',
            @usuario_solicita = N'batof1';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.vacacion
        WHERE id_empleado = @idBat005
          AND fecha_inicio = '2026-05-12'
          AND fecha_fin = '2026-05-13'
    )
    BEGIN
        EXEC rrhh.usp_solicitar_vacacion
            @id_empleado = @idBat005,
            @fecha_solicitud = '2026-05-02',
            @fecha_inicio = '2026-05-12',
            @fecha_fin = '2026-05-13',
            @dias_solicitados = 2.00,
            @observacion_solicitud = N'Vacacion aprobada para incluir en nomina de prueba.',
            @usuario_solicita = N'batof2';
    END;

    DECLARE @idVacBat005 BIGINT = (
        SELECT TOP (1) id_vacacion
        FROM rrhh.vacacion
        WHERE id_empleado = @idBat005
          AND fecha_inicio = '2026-05-12'
          AND fecha_fin = '2026-05-13'
        ORDER BY id_vacacion DESC
    );

    IF @idVacBat005 IS NOT NULL
       AND EXISTS (SELECT 1 FROM rrhh.vacacion WHERE id_vacacion = @idVacBat005 AND estado_vacacion <> N'APROBADA')
    BEGIN
        EXEC rrhh.usp_aprobar_vacacion
            @id_vacacion = @idVacBat005,
            @dias_aprobados = 2.00,
            @usuario_aprueba = N'batcoord',
            @observacion_aprobacion = N'Aprobada como parte del set de prueba.';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.vacacion
        WHERE id_empleado = @idBat009
          AND fecha_inicio = '2026-06-03'
          AND fecha_fin = '2026-06-05'
    )
    BEGIN
        EXEC rrhh.usp_solicitar_vacacion
            @id_empleado = @idBat009,
            @fecha_solicitud = '2026-05-25',
            @fecha_inicio = '2026-06-03',
            @fecha_fin = '2026-06-05',
            @dias_solicitados = 3.00,
            @observacion_solicitud = N'Vacacion futura pendiente para jefe inmediato.',
            @usuario_solicita = N'battemp';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.solicitud_permiso
        WHERE id_empleado = @idBat004
          AND fecha_inicio = '2026-05-08'
          AND cantidad_dias = 0.50
    )
    BEGIN
        EXEC rrhh.usp_solicitar_permiso
            @id_empleado = @idBat004,
            @id_tipo_permiso = @idTipoPermisoConGoce,
            @fecha_solicitud = '2026-05-07',
            @fecha_inicio = '2026-05-08',
            @fecha_fin = '2026-05-08',
            @cantidad_dias = 0.50,
            @observacion = N'Permiso medio dia maniana para prueba de portal.',
            @usuario_solicita = N'batof1';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.solicitud_permiso
        WHERE id_empleado = @idBat005
          AND fecha_inicio = '2026-05-09'
          AND cantidad_dias = 1.00
    )
    BEGIN
        EXEC rrhh.usp_solicitar_permiso
            @id_empleado = @idBat005,
            @id_tipo_permiso = @idTipoPermisoSinGoce,
            @fecha_solicitud = '2026-05-08',
            @fecha_inicio = '2026-05-09',
            @fecha_fin = '2026-05-09',
            @cantidad_dias = 1.00,
            @observacion = N'Permiso aprobado para historial.',
            @usuario_solicita = N'batof2';
    END;

    DECLARE @idPermBat005 BIGINT = (
        SELECT TOP (1) id_solicitud_permiso
        FROM rrhh.solicitud_permiso
        WHERE id_empleado = @idBat005
          AND fecha_inicio = '2026-05-09'
          AND cantidad_dias = 1.00
        ORDER BY id_solicitud_permiso DESC
    );

    IF @idPermBat005 IS NOT NULL
       AND EXISTS (SELECT 1 FROM rrhh.solicitud_permiso WHERE id_solicitud_permiso = @idPermBat005 AND estado_permiso <> N'APROBADO')
    BEGIN
        EXEC rrhh.usp_aprobar_permiso
            @id_solicitud_permiso = @idPermBat005,
            @usuario_aprueba = N'batcoord';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.hora_extra
        WHERE id_empleado = @idBat004
          AND fecha_hora_extra = '2026-05-14'
          AND cantidad_horas = 2.50
    )
    BEGIN
        EXEC rrhh.usp_registrar_hora_extra
            @id_empleado = @idBat004,
            @id_tipo_hora_extra = @idTipoHoraExtraDiurna,
            @fecha_hora_extra = '2026-05-14',
            @cantidad_horas = 2.50,
            @observacion = N'Cierre de cartera y apoyo operativo de prueba.',
            @usuario_registra = N'batof1';
    END;

    DECLARE @idHexBat004 BIGINT = (
        SELECT TOP (1) id_hora_extra
        FROM rrhh.hora_extra
        WHERE id_empleado = @idBat004
          AND fecha_hora_extra = '2026-05-14'
          AND cantidad_horas = 2.50
        ORDER BY id_hora_extra DESC
    );

    IF @idHexBat004 IS NOT NULL
       AND EXISTS (SELECT 1 FROM rrhh.hora_extra WHERE id_hora_extra = @idHexBat004 AND estado_hora_extra <> N'APROBADA')
    BEGIN
        EXEC rrhh.usp_aprobar_hora_extra
            @id_hora_extra = @idHexBat004,
            @usuario_aprueba = N'batcoord';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.hora_extra
        WHERE id_empleado = @idBat005
          AND fecha_hora_extra = '2026-05-14'
          AND cantidad_horas = 1.75
    )
    BEGIN
        EXEC rrhh.usp_registrar_hora_extra
            @id_empleado = @idBat005,
            @id_tipo_hora_extra = @idTipoHoraExtraNocturna,
            @fecha_hora_extra = '2026-05-14',
            @cantidad_horas = 1.75,
            @observacion = N'Cierre de dia de prueba pendiente de aprobacion.',
            @usuario_registra = N'batof2';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.hora_extra
        WHERE id_empleado = @idBat009
          AND fecha_hora_extra = '2026-05-10'
          AND cantidad_horas = 3.00
    )
    BEGIN
        EXEC rrhh.usp_registrar_hora_extra
            @id_empleado = @idBat009,
            @id_tipo_hora_extra = @idTipoHoraExtraDiurna,
            @fecha_hora_extra = '2026-05-10',
            @cantidad_horas = 3.00,
            @observacion = N'Apoyo de cobranza sabatino para nomina de prueba.',
            @usuario_registra = N'battemp';
    END;

    DECLARE @idHexBat009 BIGINT = (
        SELECT TOP (1) id_hora_extra
        FROM rrhh.hora_extra
        WHERE id_empleado = @idBat009
          AND fecha_hora_extra = '2026-05-10'
          AND cantidad_horas = 3.00
        ORDER BY id_hora_extra DESC
    );

    IF @idHexBat009 IS NOT NULL
       AND EXISTS (SELECT 1 FROM rrhh.hora_extra WHERE id_hora_extra = @idHexBat009 AND estado_hora_extra <> N'APROBADA')
    BEGIN
        EXEC rrhh.usp_aprobar_hora_extra
            @id_hora_extra = @idHexBat009,
            @usuario_aprueba = N'batjefe';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM nomina.movimiento_variable_empleado
        WHERE id_empleado = @idBat004
          AND id_concepto_nomina = @idConceptoOtroDevengado
          AND fecha_movimiento = '2026-05-11'
          AND monto = 950.00
    )
    BEGIN
        INSERT INTO nomina.movimiento_variable_empleado
        (
            id_empleado, id_concepto_nomina, fecha_movimiento, monto, observacion, aplicado_en_nomina, activo
        )
        VALUES
        (
            @idBat004, @idConceptoOtroDevengado, '2026-05-11', 950.00, N'Bono operativo de prueba.', 0, 1
        );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM nomina.movimiento_variable_empleado
        WHERE id_empleado = @idBat004
          AND id_concepto_nomina = @idConceptoFondoAhorro
          AND fecha_movimiento = '2026-05-11'
          AND monto = 200.00
    )
    BEGIN
        INSERT INTO nomina.movimiento_variable_empleado
        (
            id_empleado, id_concepto_nomina, fecha_movimiento, monto, observacion, aplicado_en_nomina, activo
        )
        VALUES
        (
            @idBat004, @idConceptoFondoAhorro, '2026-05-11', 200.00, N'Fondo de ahorro de prueba.', 0, 1
        );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM nomina.movimiento_variable_empleado
        WHERE id_empleado = @idBat009
          AND id_concepto_nomina = @idConceptoOtroDevengado
          AND fecha_movimiento = '2026-05-13'
          AND monto = 450.00
    )
    BEGIN
        INSERT INTO nomina.movimiento_variable_empleado
        (
            id_empleado, id_concepto_nomina, fecha_movimiento, monto, observacion, aplicado_en_nomina, activo
        )
        VALUES
        (
            @idBat009, @idConceptoOtroDevengado, '2026-05-13', 450.00, N'Incentivo de recuperacion de prueba.', 0, 1
        );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM nomina.descuento_fijo_empleado
        WHERE id_empleado = @idBat004
          AND descripcion_descuento = N'Seguro medico BAT004'
    )
    BEGIN
        INSERT INTO nomina.descuento_fijo_empleado
        (
            id_empleado, descripcion_descuento, monto_mensual, vigencia_desde, vigencia_hasta, activo
        )
        VALUES
        (
            @idBat004, N'Seguro medico BAT004', 125.00, '2026-01-01', NULL, 1
        );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM nomina.prestamo_empleado
        WHERE id_empleado = @idBat004
          AND descripcion_prestamo = N'Prestamo BAT004 equipo'
    )
    BEGIN
        INSERT INTO nomina.prestamo_empleado
        (
            id_empleado, descripcion_prestamo, monto_original, saldo_pendiente, fecha_otorgamiento, cuota_mensual, activo
        )
        VALUES
        (
            @idBat004, N'Prestamo BAT004 equipo', 1200.00, 600.00, '2026-03-01', 100.00, 1
        );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM nomina.descuento_fijo_empleado
        WHERE id_empleado = @idBat009
          AND descripcion_descuento = N'Adelanto recurrente BAT009'
    )
    BEGIN
        INSERT INTO nomina.descuento_fijo_empleado
        (
            id_empleado, descripcion_descuento, monto_mensual, vigencia_desde, vigencia_hasta, activo
        )
        VALUES
        (
            @idBat009, N'Adelanto recurrente BAT009', 80.00, '2026-02-01', NULL, 1
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM nomina.periodo_nomina WHERE codigo_periodo = N'BAT-MAY26-Q1')
    BEGIN
        EXEC nomina.usp_abrir_periodo_nomina
            @codigo_periodo = N'BAT-MAY26-Q1',
            @fecha_desde = '2026-05-01',
            @fecha_hasta = '2026-05-15',
            @fecha_pago = '2026-05-15',
            @tipo_periodo = N'QUINCENAL',
            @observacion = N'{"tipoSeed":"BATERIA_20260428","fechaCorteHoraExtra":"2026-05-14","nota":"Primera quincena de mayo para pruebas integrales."}';
    END;

    IF NOT EXISTS (SELECT 1 FROM nomina.periodo_nomina WHERE codigo_periodo = N'BAT-MAY26-Q2')
    BEGIN
        EXEC nomina.usp_abrir_periodo_nomina
            @codigo_periodo = N'BAT-MAY26-Q2',
            @fecha_desde = '2026-05-16',
            @fecha_hasta = '2026-05-31',
            @fecha_pago = '2026-05-31',
            @tipo_periodo = N'QUINCENAL',
            @observacion = N'{"tipoSeed":"BATERIA_20260428","fechaCorteHoraExtra":"2026-05-30","nota":"Segunda quincena de mayo para pruebas integrales."}';
    END;

    COMMIT;

    SELECT codigo_empleado, nombres + N' ' + apellidos AS nombre_completo, correo
    FROM rrhh.empleado
    WHERE codigo_empleado LIKE N'BAT%'
    ORDER BY codigo_empleado;

    SELECT usuario, activo, bloqueado, cambiar_clave_en_proximo_inicio
    FROM seguridad.usuario
    WHERE usuario IN (N'batadmin',N'batjefe',N'batcoord',N'batof1',N'batof2',N'batpas',N'batserv',N'batprof',N'battemp',N'batliq')
    ORDER BY usuario;

    SELECT codigo_periodo, fecha_desde, fecha_hasta, fecha_pago, estado = observacion
    FROM nomina.periodo_nomina
    WHERE codigo_periodo IN (N'BAT-MAY26-Q1', N'BAT-MAY26-Q2')
    ORDER BY fecha_desde;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;

    THROW;
END CATCH;
