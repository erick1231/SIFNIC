/*
  Extension de data de prueba para organigrama y notificaciones por jefatura inmediata.
  Requiere que ya exista la bateria BAT001..BAT010.

  Credencial adicional:
  - batgg / Prueba123!

  Objetivo visual:
  - Gerente General
  - Gerencias
  - Jefaturas
  - Coordinaciones
  - Subordinados

  Pendientes de prueba:
  - BAT011 (gerente general) ve pendiente de BAT001
  - BAT001 (gerencia) ve pendiente de BAT007
  - BAT002 (jefatura) ve pendiente de BAT009
  - BAT003 (coordinacion) ya conserva pendiente de BAT004
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @usuarioSeed NVARCHAR(100) = N'SEED_ESTRUCTURA_20260428';
    DECLARE @claveHashComun NVARCHAR(500) = N'PBKDF2SHA1|100000|iPvPZVWViXfkqTHXk33Ksw==|vvf/mCPglsemeUfsYum3tsFWYpaeCzIkYkf8KDu1YPY=';

    DECLARE @idDeptAdmin BIGINT = (
        SELECT TOP (1) id_departamento
        FROM rrhh.departamento
        WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Administracion'
    );
    DECLARE @idDeptCredito BIGINT = (
        SELECT TOP (1) id_departamento
        FROM rrhh.departamento
        WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Credito'
    );
    DECLARE @idBancoBase BIGINT = (
        SELECT TOP (1) id_banco
        FROM rrhh.banco
        WHERE nombre_banco = N'BAC'
    );
    IF @idBancoBase IS NULL
        SET @idBancoBase = (SELECT TOP (1) id_banco FROM rrhh.banco ORDER BY id_banco);

    DECLARE @idHorarioAdm BIGINT = (
        SELECT TOP (1) id_horario_laboral
        FROM rrhh.horario_laboral
        WHERE codigo_horario = N'ADM_LV'
    );
    DECLARE @idTipoIndeterminado BIGINT = (
        SELECT TOP (1) id_tipo_contrato
        FROM rrhh.tipo_contrato
        WHERE codigo_tipo_contrato = N'INDETERMINADO'
    );
    DECLARE @idTipoPermisoConGoce BIGINT = (
        SELECT TOP (1) id_tipo_permiso
        FROM rrhh.tipo_permiso
        WHERE codigo_tipo_permiso = N'CON_GOCE'
    );
    DECLARE @idTipoHoraExtraDiurna BIGINT = (
        SELECT TOP (1) id_tipo_hora_extra
        FROM rrhh.tipo_hora_extra
        WHERE codigo_tipo_hora_extra = N'HE_DIURNA'
    );
    DECLARE @idRolSupervisor BIGINT = (
        SELECT TOP (1) id_rol
        FROM seguridad.rol
        WHERE codigo_rol = N'SUPERVISOR'
    );

    DECLARE @idCargoGerenteGeneral BIGINT = (
        SELECT TOP (1) id_cargo
        FROM rrhh.cargo
        WHERE nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Gerente General'
    );
    DECLARE @idCargoCoordinadorCredito BIGINT = (
        SELECT TOP (1) id_cargo
        FROM rrhh.cargo
        WHERE codigo_cargo = N'COORD_CRED'
           OR nombre_cargo COLLATE Latin1_General_100_CI_AI = N'Coordinador de Credito'
    );

    IF @idCargoCoordinadorCredito IS NULL
    BEGIN
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
        VALUES
        (
            @idDeptCredito,
            N'COORD_CRED',
            N'Coordinador de Credito',
            N'Cargo de coordinacion para pruebas de jerarquia y aprobacion.',
            7,
            1,
            SYSDATETIME()
        );

        SET @idCargoCoordinadorCredito = SCOPE_IDENTITY();
    END;

    IF @idDeptAdmin IS NULL OR @idDeptCredito IS NULL OR @idBancoBase IS NULL
       OR @idHorarioAdm IS NULL OR @idTipoIndeterminado IS NULL
       OR @idTipoPermisoConGoce IS NULL OR @idTipoHoraExtraDiurna IS NULL
       OR @idCargoGerenteGeneral IS NULL OR @idCargoCoordinadorCredito IS NULL
    BEGIN
        THROW 51020, 'No se encontraron catalogos base para la extension de estructura de prueba.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM rrhh.empleado WHERE codigo_empleado = N'BAT011')
    BEGIN
        EXEC rrhh.usp_crear_empleado
            @codigo_empleado = N'BAT011',
            @id_departamento = @idDeptAdmin,
            @id_cargo = @idCargoGerenteGeneral,
            @cedula = N'111-111100-0011K',
            @inss = N'1111111000011',
            @nombres = N'JORGE',
            @apellidos = N'AVILES CENTENO',
            @fecha_nacimiento = '1984-11-11',
            @sexo = N'M',
            @estado_civil = N'CASADO',
            @telefono = N'8888-5011',
            @correo = N'bat011@sifnic.local',
            @direccion = N'Managua, Nicaragua',
            @fecha_ingreso = '2020-02-03',
            @id_banco = @idBancoBase,
            @numero_cuenta_bancaria = N'5100000000000011';
    END;

    DECLARE @idBat001 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT001');
    DECLARE @idBat002 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT002');
    DECLARE @idBat003 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT003');
    DECLARE @idBat004 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT004');
    DECLARE @idBat007 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT007');
    DECLARE @idBat009 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT009');
    DECLARE @idBat011 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'BAT011');

    IF @idBat001 IS NULL OR @idBat002 IS NULL OR @idBat003 IS NULL OR @idBat004 IS NULL
       OR @idBat007 IS NULL OR @idBat009 IS NULL OR @idBat011 IS NULL
    BEGIN
        THROW 51021, 'No existe la bateria base BAT001..BAT010 requerida para completar la estructura.', 1;
    END;

    UPDATE rrhh.empleado
    SET id_cargo = @idCargoCoordinadorCredito,
        fecha_actualizacion = SYSDATETIME()
    WHERE id_empleado = @idBat003
      AND id_cargo <> @idCargoCoordinadorCredito;

    IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-BAT011')
    BEGIN
        EXEC rrhh.usp_registrar_contrato
            @id_empleado = @idBat011,
            @id_tipo_contrato = @idTipoIndeterminado,
            @id_horario_laboral = @idHorarioAdm,
            @numero_contrato = N'CTR-BAT011',
            @fecha_inicio = '2020-02-03',
            @fecha_fin = NULL,
            @salario_base_mensual = 52000.00,
            @moneda = N'NIO',
            @observacion = N'Seed estructura: gerencia general corporativa.';
    END;

    IF NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'batgg')
    BEGIN
        INSERT INTO seguridad.usuario
        (
            id_sucursal,
            usuario,
            nombres,
            apellidos,
            correo,
            telefono,
            hash_clave,
            cambiar_clave_en_proximo_inicio,
            bloqueado,
            activo,
            intentos_fallidos
        )
        VALUES
        (
            NULL,
            N'batgg',
            N'JORGE',
            N'AVILES CENTENO',
            N'bat011@sifnic.local',
            N'8888-5011',
            @claveHashComun,
            0,
            0,
            1,
            0
        );
    END
    ELSE
    BEGIN
        UPDATE seguridad.usuario
        SET nombres = N'JORGE',
            apellidos = N'AVILES CENTENO',
            correo = N'bat011@sifnic.local',
            telefono = N'8888-5011',
            hash_clave = @claveHashComun,
            cambiar_clave_en_proximo_inicio = 0,
            bloqueado = 0,
            activo = 1,
            intentos_fallidos = 0,
            fecha_actualizacion = SYSDATETIME()
        WHERE usuario = N'batgg';
    END;

    DECLARE @idUsuarioBatGg BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'batgg');
    IF @idRolSupervisor IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario = @idUsuarioBatGg AND id_rol = @idRolSupervisor)
    BEGIN
        INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo)
        VALUES (@idUsuarioBatGg, @idRolSupervisor, 1);
    END;

    UPDATE rrhh.empleado_supervision
    SET activo = 0,
        fecha_actualizacion = SYSDATETIME(),
        usuario_actualizacion = @usuarioSeed
    WHERE activo = 1
      AND id_empleado = @idBat001
      AND id_supervisor_empleado <> @idBat011;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.empleado_supervision
        WHERE id_empleado = @idBat001
          AND id_supervisor_empleado = @idBat011
          AND activo = 1
    )
    BEGIN
        INSERT INTO rrhh.empleado_supervision
        (
            id_empleado,
            id_supervisor_empleado,
            usuario_registro
        )
        VALUES
        (
            @idBat001,
            @idBat011,
            @usuarioSeed
        );
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.vacacion
        WHERE id_empleado = @idBat001
          AND fecha_inicio = '2026-06-15'
          AND fecha_fin = '2026-06-16'
    )
    BEGIN
        EXEC rrhh.usp_solicitar_vacacion
            @id_empleado = @idBat001,
            @fecha_solicitud = '2026-06-01',
            @fecha_inicio = '2026-06-15',
            @fecha_fin = '2026-06-16',
            @dias_solicitados = 2.00,
            @observacion_solicitud = N'Pendiente de prueba para gerente general.',
            @usuario_solicita = N'batadmin';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.hora_extra
        WHERE id_empleado = @idBat007
          AND fecha_hora_extra = '2026-05-26'
          AND cantidad_horas = 2.25
    )
    BEGIN
        EXEC rrhh.usp_registrar_hora_extra
            @id_empleado = @idBat007,
            @id_tipo_hora_extra = @idTipoHoraExtraDiurna,
            @fecha_hora_extra = '2026-05-26',
            @cantidad_horas = 2.25,
            @observacion = N'Pendiente de prueba para gerencia financiera.',
            @usuario_registra = N'batserv';
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM rrhh.solicitud_permiso
        WHERE id_empleado = @idBat009
          AND fecha_inicio = '2026-05-18'
          AND cantidad_dias = 1.00
    )
    BEGIN
        EXEC rrhh.usp_solicitar_permiso
            @id_empleado = @idBat009,
            @id_tipo_permiso = @idTipoPermisoConGoce,
            @fecha_solicitud = '2026-05-16',
            @fecha_inicio = '2026-05-18',
            @fecha_fin = '2026-05-18',
            @cantidad_dias = 1.00,
            @observacion = N'Pendiente de prueba para jefatura de credito.',
            @usuario_solicita = N'battemp';
    END;

    COMMIT TRAN;

    SELECT
        e.codigo_empleado,
        e.nombres + N' ' + e.apellidos AS nombre_empleado,
        c.nombre_cargo,
        s.codigo_empleado AS codigo_supervisor,
        s.nombres + N' ' + s.apellidos AS supervisor
    FROM rrhh.empleado e
    LEFT JOIN rrhh.cargo c
        ON c.id_cargo = e.id_cargo
    LEFT JOIN rrhh.empleado_supervision es
        ON es.id_empleado = e.id_empleado
       AND es.activo = 1
    LEFT JOIN rrhh.empleado s
        ON s.id_empleado = es.id_supervisor_empleado
    WHERE e.codigo_empleado IN (N'BAT001',N'BAT002',N'BAT003',N'BAT004',N'BAT007',N'BAT009',N'BAT011')
    ORDER BY
        CASE e.codigo_empleado
            WHEN N'BAT011' THEN 1
            WHEN N'BAT001' THEN 2
            WHEN N'BAT002' THEN 3
            WHEN N'BAT003' THEN 4
            ELSE 5
        END,
        e.codigo_empleado;

    SELECT usuario, activo, bloqueado
    FROM seguridad.usuario
    WHERE usuario = N'batgg';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    THROW;
END CATCH;
