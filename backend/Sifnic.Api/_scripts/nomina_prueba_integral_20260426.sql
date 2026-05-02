/*
  Seed de prueba integral para nomina Nicaragua.

  Casos sembrados:
  - NOMT001: empleado nomina, salario 25,000.00
  - NOMT002: pasante ayuda, 5,000.00
  - NOMT003: servicio profesional persona natural, 10,000.00
  - NOMT004: servicio general, 10,000.00
  - NOMT005: empleado con variables, ahorro, prestamo, vacaciones y horas extra
*/

SET NOCOUNT ON;

DECLARE @idEstadoActivo BIGINT = (SELECT TOP (1) id_estado_empleado FROM rrhh.estado_empleado WHERE codigo_estado_empleado = N'ACTIVO');
DECLARE @idBancoBase BIGINT = (SELECT TOP (1) id_banco FROM rrhh.banco ORDER BY id_banco);
DECLARE @idHorarioBase BIGINT = (SELECT TOP (1) id_horario_laboral FROM rrhh.horario_laboral WHERE codigo_horario = N'ADM_LV');
DECLARE @idDepartamentoBase BIGINT =
(
    SELECT COALESCE
    (
        (SELECT TOP (1) id_departamento FROM rrhh.departamento WHERE nombre_departamento COLLATE Latin1_General_100_CI_AI = N'Tecnologia'),
        (SELECT TOP (1) id_departamento FROM rrhh.departamento ORDER BY id_departamento)
    )
);
DECLARE @idCargoBase BIGINT =
(
    SELECT COALESCE
    (
        (SELECT TOP (1) id_cargo FROM rrhh.cargo WHERE nombre_cargo = N'Analista de Sistemas'),
        (SELECT TOP (1) id_cargo FROM rrhh.cargo ORDER BY id_cargo)
    )
);

DECLARE @idTipoContratoFijo BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'FIJO');
DECLARE @idTipoContratoPasantia BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'PASANTIA');
DECLARE @idTipoContratoProfNat BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'PROFESIONAL_PERSONA_NATURAL');
DECLARE @idTipoContratoServGeneral BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'SERVICIO_GENERAL');
DECLARE @idTipoContratoTemporal BIGINT = (SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'TEMPORAL');

DECLARE @idTipoHoraExtraDiurna BIGINT = (SELECT TOP (1) id_tipo_hora_extra FROM rrhh.tipo_hora_extra WHERE codigo_tipo_hora_extra = N'HE_DIURNA');

DECLARE @idConceptoOtroDevengado BIGINT = (SELECT TOP (1) id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N'OTRO_DEVENGADO');
DECLARE @idConceptoFondoAhorro BIGINT = (SELECT TOP (1) id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N'FONDO_AHORRO');

IF @idEstadoActivo IS NULL OR @idBancoBase IS NULL OR @idHorarioBase IS NULL
   OR @idDepartamentoBase IS NULL OR @idCargoBase IS NULL
   OR @idTipoContratoFijo IS NULL OR @idTipoContratoPasantia IS NULL
   OR @idTipoContratoProfNat IS NULL OR @idTipoContratoServGeneral IS NULL
   OR @idTipoContratoTemporal IS NULL OR @idTipoHoraExtraDiurna IS NULL
   OR @idConceptoOtroDevengado IS NULL OR @idConceptoFondoAhorro IS NULL
BEGIN
    THROW 51000, 'No se encontraron todos los catalogos necesarios para el seed de nomina.', 1;
END;

DECLARE @idNomt001 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'NOMT001');
DECLARE @idNomt002 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'NOMT002');
DECLARE @idNomt003 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'NOMT003');
DECLARE @idNomt004 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'NOMT004');
DECLARE @idNomt005 BIGINT = (SELECT TOP (1) id_empleado FROM rrhh.empleado WHERE codigo_empleado = N'NOMT005');

IF @idNomt001 IS NULL
BEGIN
    INSERT INTO rrhh.empleado
    (
        codigo_empleado, id_departamento, id_cargo, id_estado_empleado, cedula, inss,
        nombres, apellidos, fecha_nacimiento, sexo, estado_civil,
        telefono, correo, direccion, fecha_ingreso, id_banco, numero_cuenta_bancaria, activo
    )
    VALUES
    (
        N'NOMT001', @idDepartamentoBase, @idCargoBase, @idEstadoActivo, N'901-010190-0001A', N'9010101900001',
        N'ANA', N'PRUEBA LEY', '1990-01-01', N'F', N'SOLTERA',
        N'8888-9001', N'nomt001@sifnic.local', N'Managua, Nicaragua', '2026-01-01', @idBancoBase, N'9001000100010001', 1
    );
    SET @idNomt001 = SCOPE_IDENTITY();
END;

IF @idNomt002 IS NULL
BEGIN
    INSERT INTO rrhh.empleado
    (
        codigo_empleado, id_departamento, id_cargo, id_estado_empleado, cedula, inss,
        nombres, apellidos, fecha_nacimiento, sexo, estado_civil,
        telefono, correo, direccion, fecha_ingreso, id_banco, numero_cuenta_bancaria, activo
    )
    VALUES
    (
        N'NOMT002', @idDepartamentoBase, @idCargoBase, @idEstadoActivo, N'902-020290-0002B', N'9020202900002',
        N'PABLO', N'PASANTE TEST', '1998-02-02', N'M', N'SOLTERO',
        N'8888-9002', N'nomt002@sifnic.local', N'Managua, Nicaragua', '2026-01-01', @idBancoBase, N'9002000200020002', 1
    );
    SET @idNomt002 = SCOPE_IDENTITY();
END;

IF @idNomt003 IS NULL
BEGIN
    INSERT INTO rrhh.empleado
    (
        codigo_empleado, id_departamento, id_cargo, id_estado_empleado, cedula, inss,
        nombres, apellidos, fecha_nacimiento, sexo, estado_civil,
        telefono, correo, direccion, fecha_ingreso, id_banco, numero_cuenta_bancaria, activo
    )
    VALUES
    (
        N'NOMT003', @idDepartamentoBase, @idCargoBase, @idEstadoActivo, N'903-030390-0003C', NULL,
        N'NORA', N'PROFESIONAL TEST', '1993-03-03', N'F', N'SOLTERA',
        N'8888-9003', N'nomt003@sifnic.local', N'Managua, Nicaragua', '2026-01-01', @idBancoBase, N'9003000300030003', 1
    );
    SET @idNomt003 = SCOPE_IDENTITY();
END;

IF @idNomt004 IS NULL
BEGIN
    INSERT INTO rrhh.empleado
    (
        codigo_empleado, id_departamento, id_cargo, id_estado_empleado, cedula, inss,
        nombres, apellidos, fecha_nacimiento, sexo, estado_civil,
        telefono, correo, direccion, fecha_ingreso, id_banco, numero_cuenta_bancaria, activo
    )
    VALUES
    (
        N'NOMT004', @idDepartamentoBase, @idCargoBase, @idEstadoActivo, N'904-040490-0004D', NULL,
        N'GINA', N'SERVICIO GENERAL', '1994-04-04', N'F', N'SOLTERA',
        N'8888-9004', N'nomt004@sifnic.local', N'Managua, Nicaragua', '2026-01-01', @idBancoBase, N'9004000400040004', 1
    );
    SET @idNomt004 = SCOPE_IDENTITY();
END;

IF @idNomt005 IS NULL
BEGIN
    INSERT INTO rrhh.empleado
    (
        codigo_empleado, id_departamento, id_cargo, id_estado_empleado, cedula, inss,
        nombres, apellidos, fecha_nacimiento, sexo, estado_civil,
        telefono, correo, direccion, fecha_ingreso, id_banco, numero_cuenta_bancaria, activo
    )
    VALUES
    (
        N'NOMT005', @idDepartamentoBase, @idCargoBase, @idEstadoActivo, N'905-050590-0005E', N'9050505900005',
        N'HUGO', N'VARIABLE TEST', '1995-05-05', N'M', N'CASADO',
        N'8888-9005', N'nomt005@sifnic.local', N'Managua, Nicaragua', '2026-01-01', @idBancoBase, N'9005000500050005', 1
    );
    SET @idNomt005 = SCOPE_IDENTITY();
END;

IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-NOMT001')
BEGIN
    INSERT INTO rrhh.contrato
    (
        id_empleado, id_tipo_contrato, id_horario_laboral, numero_contrato, fecha_inicio,
        fecha_fin, salario_base_mensual, moneda, es_contrato_vigente, observacion
    )
    VALUES
    (
        @idNomt001, @idTipoContratoFijo, @idHorarioBase, N'CTR-NOMT001', '2026-01-01',
        NULL, 25000.00, N'NIO', 1, N'Caso legal empleado nomina 25,000.00'
    );
END;

IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-NOMT002')
BEGIN
    INSERT INTO rrhh.contrato
    (
        id_empleado, id_tipo_contrato, id_horario_laboral, numero_contrato, fecha_inicio,
        fecha_fin, salario_base_mensual, moneda, es_contrato_vigente, observacion
    )
    VALUES
    (
        @idNomt002, @idTipoContratoPasantia, @idHorarioBase, N'CTR-NOMT002', '2026-01-01',
        NULL, 5000.00, N'NIO', 1, N'Caso legal pasante ayuda 5,000.00'
    );
END;

IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-NOMT003')
BEGIN
    INSERT INTO rrhh.contrato
    (
        id_empleado, id_tipo_contrato, id_horario_laboral, numero_contrato, fecha_inicio,
        fecha_fin, salario_base_mensual, moneda, es_contrato_vigente, observacion
    )
    VALUES
    (
        @idNomt003, @idTipoContratoProfNat, @idHorarioBase, N'CTR-NOMT003', '2026-01-01',
        NULL, 10000.00, N'NIO', 1, N'Caso legal profesional persona natural 10,000.00'
    );
END;

IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-NOMT004')
BEGIN
    INSERT INTO rrhh.contrato
    (
        id_empleado, id_tipo_contrato, id_horario_laboral, numero_contrato, fecha_inicio,
        fecha_fin, salario_base_mensual, moneda, es_contrato_vigente, observacion
    )
    VALUES
    (
        @idNomt004, @idTipoContratoServGeneral, @idHorarioBase, N'CTR-NOMT004', '2026-01-01',
        NULL, 10000.00, N'NIO', 1, N'Caso legal servicio general 10,000.00'
    );
END;

IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE numero_contrato = N'CTR-NOMT005')
BEGIN
    INSERT INTO rrhh.contrato
    (
        id_empleado, id_tipo_contrato, id_horario_laboral, numero_contrato, fecha_inicio,
        fecha_fin, salario_base_mensual, moneda, es_contrato_vigente, observacion
    )
    VALUES
    (
        @idNomt005, @idTipoContratoTemporal, @idHorarioBase, N'CTR-NOMT005', '2026-01-01',
        NULL, 18000.00, N'NIO', 1, N'Caso integral con variables, vacaciones y horas extra.'
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM nomina.movimiento_variable_empleado
    WHERE id_empleado = @idNomt005
      AND id_concepto_nomina = @idConceptoOtroDevengado
      AND fecha_movimiento = '2026-04-12'
      AND monto = 1200.00
)
BEGIN
    INSERT INTO nomina.movimiento_variable_empleado
    (
        id_empleado, id_concepto_nomina, fecha_movimiento, monto, observacion, aplicado_en_nomina, activo
    )
    VALUES
    (
        @idNomt005, @idConceptoOtroDevengado, '2026-04-12', 1200.00, N'Otro ingreso gravado para prueba integral.', 0, 1
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM nomina.movimiento_variable_empleado
    WHERE id_empleado = @idNomt005
      AND id_concepto_nomina = @idConceptoFondoAhorro
      AND fecha_movimiento = '2026-04-12'
      AND monto = 300.00
)
BEGIN
    INSERT INTO nomina.movimiento_variable_empleado
    (
        id_empleado, id_concepto_nomina, fecha_movimiento, monto, observacion, aplicado_en_nomina, activo
    )
    VALUES
    (
        @idNomt005, @idConceptoFondoAhorro, '2026-04-12', 300.00, N'Fondo de ahorro variable para prueba integral.', 0, 1
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM nomina.descuento_fijo_empleado
    WHERE id_empleado = @idNomt005
      AND descripcion_descuento = N'Seguro medico colectivo'
      AND monto_mensual = 200.00
)
BEGIN
    INSERT INTO nomina.descuento_fijo_empleado
    (
        id_empleado, descripcion_descuento, monto_mensual, vigencia_desde, vigencia_hasta, activo
    )
    VALUES
    (
        @idNomt005, N'Seguro medico colectivo', 200.00, '2026-01-01', NULL, 1
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM nomina.descuento_fijo_empleado
    WHERE id_empleado = @idNomt005
      AND descripcion_descuento = N'Fondo ahorro voluntario'
      AND monto_mensual = 150.00
)
BEGIN
    INSERT INTO nomina.descuento_fijo_empleado
    (
        id_empleado, descripcion_descuento, monto_mensual, vigencia_desde, vigencia_hasta, activo
    )
    VALUES
    (
        @idNomt005, N'Fondo ahorro voluntario', 150.00, '2026-01-01', NULL, 1
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM nomina.prestamo_empleado
    WHERE id_empleado = @idNomt005
      AND descripcion_prestamo = N'Prestamo equipo de trabajo'
      AND monto_original = 2000.00
)
BEGIN
    INSERT INTO nomina.prestamo_empleado
    (
        id_empleado, descripcion_prestamo, monto_original, saldo_pendiente, fecha_otorgamiento, cuota_mensual, activo
    )
    VALUES
    (
        @idNomt005, N'Prestamo equipo de trabajo', 2000.00, 1000.00, '2026-02-01', 250.00, 1
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM rrhh.hora_extra
    WHERE id_empleado = @idNomt005
      AND fecha_hora_extra = '2026-04-20'
      AND cantidad_horas = 2.00
      AND estado_hora_extra = N'APROBADA'
)
BEGIN
    INSERT INTO rrhh.hora_extra
    (
        id_empleado, id_tipo_hora_extra, fecha_hora_extra, cantidad_horas, estado_hora_extra,
        observacion, usuario_registra, usuario_aprueba, fecha_aprobacion, pagada_en_nomina
    )
    VALUES
    (
        @idNomt005, @idTipoHoraExtraDiurna, '2026-04-20', 2.00, N'APROBADA',
        N'Horas extra aprobadas para prueba integral.', N'qa.nomina', N'qa.supervisor', '2026-04-21T09:00:00', 0
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM rrhh.hora_extra
    WHERE id_empleado = @idNomt005
      AND fecha_hora_extra = '2026-04-21'
      AND cantidad_horas = 3.00
      AND estado_hora_extra = N'REGISTRADA'
)
BEGIN
    INSERT INTO rrhh.hora_extra
    (
        id_empleado, id_tipo_hora_extra, fecha_hora_extra, cantidad_horas, estado_hora_extra,
        observacion, usuario_registra, usuario_aprueba, fecha_aprobacion, pagada_en_nomina
    )
    VALUES
    (
        @idNomt005, @idTipoHoraExtraDiurna, '2026-04-21', 3.00, N'REGISTRADA',
        N'Horas extra no aprobadas para validar exclusion.', N'qa.nomina', NULL, NULL, 0
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM rrhh.vacacion
    WHERE id_empleado = @idNomt005
      AND fecha_inicio = '2026-04-18'
      AND fecha_fin = '2026-04-18'
      AND dias_solicitados = 1.00
)
BEGIN
    INSERT INTO rrhh.vacacion
    (
        id_empleado, fecha_solicitud, fecha_inicio, fecha_fin, dias_solicitados, dias_aprobados,
        estado_vacacion, observacion_solicitud, observacion_aprobacion, usuario_solicita, usuario_aprueba,
        fecha_aprobacion, pagada_en_nomina
    )
    VALUES
    (
        @idNomt005, '2026-04-15', '2026-04-18', '2026-04-18', 1.00, 1.00,
        N'APROBADA', N'Vacacion aprobada para prueba integral.', N'Aprobada para entrar a nomina.',
        N'qa.nomina', N'qa.supervisor', '2026-04-16T10:00:00', 0
    );
END;

SELECT e.codigo_empleado, e.nombre_completo, tc.codigo_tipo_contrato, c.salario_base_mensual
FROM rrhh.empleado e
INNER JOIN rrhh.contrato c
    ON c.id_empleado = e.id_empleado
   AND c.es_contrato_vigente = 1
INNER JOIN rrhh.tipo_contrato tc
    ON tc.id_tipo_contrato = c.id_tipo_contrato
WHERE e.codigo_empleado IN (N'NOMT001', N'NOMT002', N'NOMT003', N'NOMT004', N'NOMT005')
ORDER BY e.codigo_empleado;
