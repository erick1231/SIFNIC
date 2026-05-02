/*
  Seed de prueba RRHH / autoservicio sin tocar nomina.

  Usuarios de prueba creados por este script:
  - eespinoza / eespinoza
  - mruiz / mruiz
  - lmartinez / lmartinez

  Nota:
  - La base actual no tiene un campo formal de supervisor o jefe inmediato en rrhh.empleado.
  - Este script deja supervisor y colaborador de prueba, pero la relacion jerarquica queda pendiente
    hasta que exista una columna o tabla de relacion.
*/

SET NOCOUNT ON;

DECLARE @idEstadoActivo BIGINT = 1;
DECLARE @idBancoBase BIGINT = 1;
DECLARE @idHorario BIGINT = 1;
DECLARE @idTipoContratoFijo BIGINT = 1;
DECLARE @idTipoContratoTemporal BIGINT = 2;
DECLARE @idTipoHoraExtraDiurna BIGINT = 1;
DECLARE @idTipoPermisoConGoce BIGINT = 1;
DECLARE @idRolSupervisor BIGINT = (SELECT TOP (1) id_rol FROM seguridad.rol WHERE codigo_rol = N'SUPERVISOR');
DECLARE @idRolVentas BIGINT = (SELECT TOP (1) id_rol FROM seguridad.rol WHERE codigo_rol = N'VENTAS');

DECLARE @idEmpleadoErick BIGINT = (
    SELECT TOP (1) id_empleado
    FROM rrhh.empleado
    WHERE codigo_empleado = N'EMP0003'
);

DECLARE @idEmpleadoMarta BIGINT = (
    SELECT TOP (1) id_empleado
    FROM rrhh.empleado
    WHERE codigo_empleado = N'EMP0004'
);

IF @idEmpleadoMarta IS NULL
BEGIN
    INSERT INTO rrhh.empleado
    (
        codigo_empleado,
        id_departamento,
        id_cargo,
        id_estado_empleado,
        cedula,
        inss,
        nombres,
        apellidos,
        fecha_nacimiento,
        sexo,
        estado_civil,
        telefono,
        correo,
        direccion,
        fecha_ingreso,
        id_banco,
        numero_cuenta_bancaria,
        activo
    )
    VALUES
    (
        N'EMP0004',
        4,
        4,
        @idEstadoActivo,
        N'001-120390-0001A',
        N'1203900001',
        N'MARTA',
        N'RUIZ LOPEZ',
        '1990-03-12',
        N'F',
        N'CASADA',
        N'8888-4101',
        N'marta.ruiz@sisfnic.local',
        N'Managua, Nicaragua',
        '2024-03-01',
        @idBancoBase,
        N'0004000400040004',
        1
    );

    SET @idEmpleadoMarta = SCOPE_IDENTITY();
END;

DECLARE @idEmpleadoLucas BIGINT = (
    SELECT TOP (1) id_empleado
    FROM rrhh.empleado
    WHERE codigo_empleado = N'EMP0005'
);

IF @idEmpleadoLucas IS NULL
BEGIN
    INSERT INTO rrhh.empleado
    (
        codigo_empleado,
        id_departamento,
        id_cargo,
        id_estado_empleado,
        cedula,
        inss,
        nombres,
        apellidos,
        fecha_nacimiento,
        sexo,
        estado_civil,
        telefono,
        correo,
        direccion,
        fecha_ingreso,
        id_banco,
        numero_cuenta_bancaria,
        activo
    )
    VALUES
    (
        N'EMP0005',
        5,
        10,
        @idEstadoActivo,
        N'001-150798-0002B',
        N'1507980002',
        N'LUCAS',
        N'MARTINEZ DIAZ',
        '1998-07-15',
        N'M',
        N'SOLTERO',
        N'8888-4102',
        N'lucas.martinez@sisfnic.local',
        N'Masaya, Nicaragua',
        '2025-08-01',
        @idBancoBase,
        N'0005000500050005',
        1
    );

    SET @idEmpleadoLucas = SCOPE_IDENTITY();
END;

IF @idEmpleadoErick IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'eespinoza')
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
    SELECT
        NULL,
        N'eespinoza',
        e.nombres,
        e.apellidos,
        e.correo,
        e.telefono,
        N'PBKDF2SHA1|100000|sJV7sWBg3LLBmWUdgU0ZTA==|0KNgGIAaX4T30s45I9FGO8AB6vwhPfRwsbCZMSiUflk=',
        0,
        0,
        1,
        0
    FROM rrhh.empleado e
    WHERE e.id_empleado = @idEmpleadoErick;
END;

IF @idEmpleadoMarta IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'mruiz')
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
    SELECT
        NULL,
        N'mruiz',
        e.nombres,
        e.apellidos,
        e.correo,
        e.telefono,
        N'PBKDF2SHA1|100000|z9TDdKiqnhMeFpFox7BlGQ==|FDNRJobPlT8Kio3QZBE6INSF59mQeE7WK3wYaR+XgoI=',
        0,
        0,
        1,
        0
    FROM rrhh.empleado e
    WHERE e.id_empleado = @idEmpleadoMarta;
END;

IF @idEmpleadoLucas IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM seguridad.usuario WHERE usuario = N'lmartinez')
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
    SELECT
        NULL,
        N'lmartinez',
        e.nombres,
        e.apellidos,
        e.correo,
        e.telefono,
        N'PBKDF2SHA1|100000|14bgnwP00t8v7s/yFr+low==|HVSNqpbyfe6C55jz169sdRT/cGtSSPcL7jR5+f/MYl8=',
        0,
        0,
        1,
        0
    FROM rrhh.empleado e
    WHERE e.id_empleado = @idEmpleadoLucas;
END;

DECLARE @idUsuarioMarta BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'mruiz');
DECLARE @idUsuarioLucas BIGINT = (SELECT TOP (1) id_usuario FROM seguridad.usuario WHERE usuario = N'lmartinez');

IF @idUsuarioMarta IS NOT NULL
   AND @idRolSupervisor IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM seguridad.usuario_rol
       WHERE id_usuario = @idUsuarioMarta
         AND id_rol = @idRolSupervisor
   )
BEGIN
    INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo)
    VALUES (@idUsuarioMarta, @idRolSupervisor, 1);
END;

IF @idUsuarioLucas IS NOT NULL
   AND @idRolVentas IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM seguridad.usuario_rol
       WHERE id_usuario = @idUsuarioLucas
         AND id_rol = @idRolVentas
   )
BEGIN
    INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo)
    VALUES (@idUsuarioLucas, @idRolVentas, 1);
END;

IF @idEmpleadoMarta IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-EMP0004')
BEGIN
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
    VALUES
    (
        @idEmpleadoMarta,
        @idTipoContratoFijo,
        @idHorario,
        N'CTR-EMP0004',
        '2024-03-01',
        NULL,
        28500.00,
        N'NIO',
        1,
        N'Contrato fijo de prueba para flujo de supervisor.'
    );
END;

IF @idEmpleadoLucas IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-EMP0005')
BEGIN
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
    VALUES
    (
        @idEmpleadoLucas,
        @idTipoContratoTemporal,
        @idHorario,
        N'CTR-EMP0005',
        '2025-08-01',
        '2026-08-01',
        16500.00,
        N'NIO',
        1,
        N'Contrato temporal de prueba para autoservicio.'
    );
END;

IF @idEmpleadoLucas IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM rrhh.accion_personal
       WHERE id_empleado = @idEmpleadoLucas
         AND tipo_accion = N'PROMOCION'
         AND fecha_accion = '2026-03-01'
   )
BEGIN
    INSERT INTO rrhh.accion_personal
    (
        id_empleado,
        tipo_accion,
        fecha_accion,
        descripcion_accion,
        usuario_registro
    )
    VALUES
    (
        @idEmpleadoLucas,
        N'PROMOCION',
        '2026-03-01',
        N'Promocion de prueba a Oficial de Credito Senior por resultados del trimestre.',
        N'admin.sisfnic'
    );
END;

IF @idEmpleadoErick IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM rrhh.hora_extra
       WHERE id_empleado = @idEmpleadoErick
         AND fecha_hora_extra = '2026-04-24'
         AND cantidad_horas = 3.50
   )
BEGIN
    INSERT INTO rrhh.hora_extra
    (
        id_empleado,
        id_tipo_hora_extra,
        fecha_hora_extra,
        cantidad_horas,
        estado_hora_extra,
        observacion,
        usuario_registra,
        usuario_aprueba,
        fecha_aprobacion,
        pagada_en_nomina
    )
    VALUES
    (
        @idEmpleadoErick,
        @idTipoHoraExtraDiurna,
        '2026-04-24',
        3.50,
        N'REGISTRADA',
        N'Soporte extendido y cierre operativo mensual.',
        N'eespinoza',
        NULL,
        NULL,
        0
    );
END;

IF @idEmpleadoLucas IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM rrhh.hora_extra
       WHERE id_empleado = @idEmpleadoLucas
         AND fecha_hora_extra = '2026-04-22'
         AND cantidad_horas = 2.00
   )
BEGIN
    INSERT INTO rrhh.hora_extra
    (
        id_empleado,
        id_tipo_hora_extra,
        fecha_hora_extra,
        cantidad_horas,
        estado_hora_extra,
        observacion,
        usuario_registra,
        usuario_aprueba,
        fecha_aprobacion,
        pagada_en_nomina
    )
    VALUES
    (
        @idEmpleadoLucas,
        @idTipoHoraExtraDiurna,
        '2026-04-22',
        2.00,
        N'APROBADA',
        N'Apoyo en visita de campo y cierre diario.',
        N'lmartinez',
        N'mruiz',
        '2026-04-23T09:10:00',
        0
    );
END;

IF @idEmpleadoErick IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM rrhh.vacacion
       WHERE id_empleado = @idEmpleadoErick
         AND fecha_inicio = '2026-05-12'
         AND fecha_fin = '2026-05-13'
   )
BEGIN
    INSERT INTO rrhh.vacacion
    (
        id_empleado,
        fecha_solicitud,
        fecha_inicio,
        fecha_fin,
        dias_solicitados,
        dias_aprobados,
        estado_vacacion,
        observacion_solicitud,
        observacion_aprobacion,
        usuario_solicita,
        usuario_aprueba,
        fecha_aprobacion,
        pagada_en_nomina
    )
    VALUES
    (
        @idEmpleadoErick,
        '2026-04-26',
        '2026-05-12',
        '2026-05-13',
        2.00,
        NULL,
        N'SOLICITADA',
        N'Solicitud de descanso familiar de prueba.',
        NULL,
        N'eespinoza',
        NULL,
        NULL,
        0
    );
END;

IF @idEmpleadoLucas IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM rrhh.vacacion
       WHERE id_empleado = @idEmpleadoLucas
         AND fecha_inicio = '2026-04-28'
         AND fecha_fin = '2026-04-28'
   )
BEGIN
    INSERT INTO rrhh.vacacion
    (
        id_empleado,
        fecha_solicitud,
        fecha_inicio,
        fecha_fin,
        dias_solicitados,
        dias_aprobados,
        estado_vacacion,
        observacion_solicitud,
        observacion_aprobacion,
        usuario_solicita,
        usuario_aprueba,
        fecha_aprobacion,
        pagada_en_nomina
    )
    VALUES
    (
        @idEmpleadoLucas,
        '2026-04-20',
        '2026-04-28',
        '2026-04-28',
        1.00,
        1.00,
        N'APROBADA',
        N'Vacacion de prueba aprobada.',
        N'Aprobada por supervisor de prueba.',
        N'lmartinez',
        N'mruiz',
        '2026-04-21T08:30:00',
        0
    );
END;

IF @idEmpleadoLucas IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM rrhh.solicitud_permiso
       WHERE id_empleado = @idEmpleadoLucas
         AND fecha_inicio = '2026-04-29'
         AND fecha_fin = '2026-04-29'
   )
BEGIN
    INSERT INTO rrhh.solicitud_permiso
    (
        id_empleado,
        id_tipo_permiso,
        fecha_solicitud,
        fecha_inicio,
        fecha_fin,
        cantidad_dias,
        estado_permiso,
        observacion,
        usuario_solicita,
        usuario_aprueba,
        fecha_aprobacion
    )
    VALUES
    (
        @idEmpleadoLucas,
        @idTipoPermisoConGoce,
        '2026-04-26',
        '2026-04-29',
        '2026-04-29',
        0.50,
        N'SOLICITADO',
        N'{\"textoSolicitud\":\"Permiso de prueba por diligencia medica.\",\"textoResolucion\":null,\"esMedioDia\":true,\"jornadaMedioDia\":\"MANANA\"}',
        N'lmartinez',
        NULL,
        NULL
    );
END;

SELECT
    e.codigo_empleado,
    e.nombres,
    e.apellidos,
    u.usuario,
    STRING_AGG(r.codigo_rol, ', ') WITHIN GROUP (ORDER BY r.codigo_rol) AS roles
FROM rrhh.empleado e
LEFT JOIN seguridad.usuario u
    ON (u.correo IS NOT NULL AND u.correo <> N'' AND u.correo = e.correo)
    OR (u.nombres = e.nombres AND u.apellidos = e.apellidos)
LEFT JOIN seguridad.usuario_rol ur
    ON ur.id_usuario = u.id_usuario
   AND ur.activo = 1
LEFT JOIN seguridad.rol r
    ON r.id_rol = ur.id_rol
   AND r.activo = 1
WHERE e.codigo_empleado IN (N'EMP0003', N'EMP0004', N'EMP0005')
GROUP BY e.codigo_empleado, e.nombres, e.apellidos, u.usuario
ORDER BY e.codigo_empleado;
