using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Nomina;

public static class NominaSupport
{
    private static readonly HashSet<string> EmployeeContractCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIJO",
        "TEMPORAL",
        "INDETERMINADO",
        "INDETERMINADA",
    };

    private static readonly HashSet<string> ServiceContractCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SERVICIOS",
        "PROFESIONAL_PERSONA_NATURAL",
        "SERVICIO_GENERAL",
    };

    public static string GetOperatorUser(HttpRequest request)
    {
        var usuario = request.Headers["X-Operator-User"].ToString().Trim();
        return string.IsNullOrWhiteSpace(usuario) ? "sistema.local" : usuario;
    }

    public static string TranslateSqlMessage(string message, string defaultMessage)
    {
        var text = message.ToLowerInvariant();

        if (text.Contains("ya fue generada") || text.Contains("periodo de nomina no existe"))
        {
            return message;
        }

        if (text.Contains("duplicate") || text.Contains("unique") || text.Contains("cannot insert duplicate"))
        {
            return "Ya existe un registro con esos datos.";
        }

        if (text.Contains("datetime") || text.Contains("out-of-range") || text.Contains("fuera de intervalo"))
        {
            return "Hay una fecha fuera del rango permitido. Usa una fecha igual o mayor a 01/01/1753.";
        }

        return defaultMessage;
    }

    public static void RegisterBitacora(
        SqlConnection connection,
        SqlTransaction? transaction,
        HttpContext httpContext,
        string proceso,
        string tipoEvento,
        long idReferencia,
        string referenciaTexto,
        string descripcion,
        object resumen,
        string? usuarioRegistro = null)
    {
        using var command = transaction is null
            ? new SqlCommand("operacion.usp_registrar_bitacora_operativa", connection)
            : new SqlCommand("operacion.usp_registrar_bitacora_operativa", connection, transaction);

        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@modulo", SqlDbType.NVarChar, 50).Value = "NOMINA";
        command.Parameters.Add("@proceso", SqlDbType.NVarChar, 100).Value = proceso;
        command.Parameters.Add("@tipo_evento", SqlDbType.NVarChar, 50).Value = tipoEvento;
        command.Parameters.Add("@id_referencia", SqlDbType.BigInt).Value = idReferencia;
        command.Parameters.Add("@referencia_texto", SqlDbType.NVarChar, 100).Value = referenciaTexto;
        command.Parameters.Add("@descripcion_evento", SqlDbType.NVarChar, 1000).Value = descripcion;
        command.Parameters.Add("@datos_resumen", SqlDbType.NVarChar).Value = JsonSerializer.Serialize(resumen);
        command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value =
            usuarioRegistro ?? GetOperatorUser(httpContext.Request);
        command.Parameters.Add("@equipo", SqlDbType.NVarChar, 100).Value = Environment.MachineName;
        command.Parameters.Add("@ip_equipo", SqlDbType.NVarChar, 50).Value =
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "LOCAL";
        command.ExecuteNonQuery();
    }

    public static void EnsurePayslipRecord(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idNominaDetalle,
        string usuario)
    {
        const string existsSql = """
            SELECT TOP (1) id_esquela_pago
            FROM nomina.esquela_pago
            WHERE id_nomina_detalle = @id_nomina_detalle
            ORDER BY id_esquela_pago DESC;
            """;

        using (var existsCommand = transaction is null
            ? new SqlCommand(existsSql, connection)
            : new SqlCommand(existsSql, connection, transaction))
        {
            existsCommand.Parameters.Add("@id_nomina_detalle", SqlDbType.BigInt).Value = idNominaDetalle;
            var existingId = existsCommand.ExecuteScalar();
            if (existingId is not null && existingId != DBNull.Value)
            {
                return;
            }
        }

        using var command = transaction is null
            ? new SqlCommand("nomina.usp_generar_esquela_pago", connection)
            : new SqlCommand("nomina.usp_generar_esquela_pago", connection, transaction);

        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@id_nomina_detalle", SqlDbType.BigInt).Value = idNominaDetalle;
        command.Parameters.Add("@nombre_archivo", SqlDbType.NVarChar, 255).Value = $"esquela-{idNominaDetalle}.html";
        command.Parameters.Add("@ruta_archivo", SqlDbType.NVarChar, 500).Value = DBNull.Value;
        command.Parameters.Add("@contenido_base64", SqlDbType.NVarChar).Value = DBNull.Value;
        command.Parameters.Add("@hash_documento", SqlDbType.NVarChar, 200).Value = DBNull.Value;
        command.Parameters.Add("@observacion", SqlDbType.NVarChar, 500).Value =
            "Vista previa HTML generada desde el modulo de nomina.";
        command.Parameters.Add("@usuario_generacion", SqlDbType.NVarChar, 100).Value = usuario;
        command.ExecuteScalar();
    }

    public static void EnsurePayslipRecordsForPayroll(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idNomina,
        string usuario)
    {
        const string sql = """
            SELECT nd.id_nomina_detalle
            FROM nomina.nomina_detalle nd
            WHERE nd.id_nomina = @id_nomina
            ORDER BY nd.id_nomina_detalle;
            """;

        var detailIds = new List<long>();
        using (var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@id_nomina", SqlDbType.BigInt).Value = idNomina;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                detailIds.Add(reader.GetInt64(0));
            }
        }

        foreach (var detailId in detailIds)
        {
            EnsurePayslipRecord(connection, transaction, detailId, usuario);
        }
    }

    public static void EnsurePayslipRecordsForEmployee(
        SqlConnection connection,
        SqlTransaction? transaction,
        long idEmpleado,
        string usuario)
    {
        const string sql = """
            SELECT nd.id_nomina_detalle
            FROM nomina.nomina_detalle nd
            WHERE nd.id_empleado = @id_empleado
            ORDER BY nd.id_nomina_detalle;
            """;

        var detailIds = new List<long>();
        using (var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("@id_empleado", SqlDbType.BigInt).Value = idEmpleado;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                detailIds.Add(reader.GetInt64(0));
            }
        }

        foreach (var detailId in detailIds)
        {
            EnsurePayslipRecord(connection, transaction, detailId, usuario);
        }
    }

    public static void EnsureNominaSetup(SqlConnection connection)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'INDETERMINADO')
            BEGIN
                INSERT INTO rrhh.tipo_contrato (codigo_tipo_contrato, nombre_tipo_contrato, descripcion, activo)
                VALUES (N'INDETERMINADO', N'Contrato indeterminado', N'Contrato con acumulacion de vacaciones y tratamiento de nomina.', 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'PROFESIONAL_PERSONA_NATURAL')
            BEGIN
                INSERT INTO rrhh.tipo_contrato (codigo_tipo_contrato, nombre_tipo_contrato, descripcion, activo)
                VALUES (N'PROFESIONAL_PERSONA_NATURAL', N'Servicio profesional persona natural', N'Pago a tercero con retencion IR del 10%.', 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato = N'SERVICIO_GENERAL')
            BEGIN
                INSERT INTO rrhh.tipo_contrato (codigo_tipo_contrato, nombre_tipo_contrato, descripcion, activo)
                VALUES (N'SERVICIO_GENERAL', N'Servicio general', N'Pago a tercero con retencion IR del 2%.', 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_nomina WHERE codigo_parametro = N'REGIMEN_INSS_EMPRESA')
            BEGIN
                INSERT INTO nomina.parametro_nomina (codigo_parametro, valor_decimal, valor_texto, descripcion, activo, fecha_registro)
                VALUES (N'REGIMEN_INSS_EMPRESA', NULL, N'INTEGRAL', N'Regimen INSS aplicado a la empresa.', 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_nomina WHERE codigo_parametro = N'CANTIDAD_TRABAJADORES_EMPRESA')
            BEGIN
                INSERT INTO nomina.parametro_nomina (codigo_parametro, valor_decimal, valor_texto, descripcion, activo, fecha_registro)
                VALUES (N'CANTIDAD_TRABAJADORES_EMPRESA', 35, NULL, N'Cantidad de trabajadores utilizada para el aporte patronal.', 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_nomina WHERE codigo_parametro = N'MODO_PASANTIA_POR_DEFECTO')
            BEGIN
                INSERT INTO nomina.parametro_nomina (codigo_parametro, valor_decimal, valor_texto, descripcion, activo, fecha_registro)
                VALUES (N'MODO_PASANTIA_POR_DEFECTO', NULL, N'NO_NOMINA', N'Tratamiento por defecto para pasantias.', 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'INSS_LABORAL_INTEGRAL' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'INSS_LABORAL_INTEGRAL', N'INSS laboral integral', N'LABORAL', '2024-01-01', NULL, 7.0, NULL, 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'INSS_LABORAL_IVM_RP' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'INSS_LABORAL_IVM_RP', N'INSS laboral IVM-RP', N'LABORAL', '2024-01-01', NULL, 5.0, NULL, 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'INSS_PATRONAL_INTEGRAL_LT50' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'INSS_PATRONAL_INTEGRAL_LT50', N'INSS patronal integral menor a 50', N'PATRONAL', '2024-01-01', NULL, 21.5, NULL, 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'INSS_PATRONAL_INTEGRAL_GE50' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'INSS_PATRONAL_INTEGRAL_GE50', N'INSS patronal integral 50 o mas', N'PATRONAL', '2024-01-01', NULL, 22.5, NULL, 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'INSS_PATRONAL_IVM_RP_LT50' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'INSS_PATRONAL_IVM_RP_LT50', N'INSS patronal IVM-RP menor a 50', N'PATRONAL', '2024-01-01', NULL, 15.5, NULL, 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'INSS_PATRONAL_IVM_RP_GE50' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'INSS_PATRONAL_IVM_RP_GE50', N'INSS patronal IVM-RP 50 o mas', N'PATRONAL', '2024-01-01', NULL, 16.5, NULL, 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'RETENCION_SERVICIO_PROF_NATURAL' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'RETENCION_SERVICIO_PROF_NATURAL', N'Retencion servicio profesional persona natural', N'RETENCION', '2024-01-01', NULL, 10.0, NULL, 1, SYSDATETIME());
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.parametro_contribucion WHERE codigo_contribucion = N'RETENCION_SERVICIO_GENERAL' AND vigencia_desde = '2024-01-01')
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'RETENCION_SERVICIO_GENERAL', N'Retencion servicio general', N'RETENCION', '2024-01-01', NULL, 2.0, NULL, 1, SYSDATETIME());
            END;

            DECLARE @tipo_ingreso BIGINT = (SELECT TOP (1) id_tipo_concepto_nomina FROM nomina.tipo_concepto_nomina WHERE codigo_tipo_concepto = N'INGRESO');
            DECLARE @tipo_deduccion BIGINT = (SELECT TOP (1) id_tipo_concepto_nomina FROM nomina.tipo_concepto_nomina WHERE codigo_tipo_concepto = N'DEDUCCION');

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'AYUDA_PASANTIA')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_ingreso, N'AYUDA_PASANTIA', N'Ayuda economica pasantia', 0, 0, 1, 8, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'SERVICIO_PROFESIONAL')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_ingreso, N'SERVICIO_PROFESIONAL', N'Servicio profesional', 0, 0, 1, 9, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'FONDO_AHORRO')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_deduccion, N'FONDO_AHORRO', N'Fondo de ahorro', 0, 0, 1, 54, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'RETENCION_SERVICIO')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_deduccion, N'RETENCION_SERVICIO', N'Retencion IR servicios', 0, 0, 1, 55, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'COMISION_COLOCACION')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_ingreso, N'COMISION_COLOCACION', N'Comision por colocacion de creditos', 1, 1, 1, 20, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'EMBARGO_JUDICIAL')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_deduccion, N'EMBARGO_JUDICIAL', N'Embargo judicial', 0, 0, 1, 60, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'PENSION_ALIMENTICIA')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_deduccion, N'PENSION_ALIMENTICIA', N'Pension alimenticia', 0, 0, 1, 61, 1);
            END;

            EXEC('
            CREATE OR ALTER FUNCTION nomina.fn_obtener_parametro_texto
            (
                @codigo_parametro NVARCHAR(100)
            )
            RETURNS NVARCHAR(400)
            AS
            BEGIN
                DECLARE @valor NVARCHAR(400);

                SELECT TOP 1 @valor = valor_texto
                FROM nomina.parametro_nomina
                WHERE codigo_parametro = @codigo_parametro
                  AND activo = 1
                ORDER BY id_parametro_nomina DESC;

                RETURN @valor;
            END;
            ');

            EXEC('
            CREATE OR ALTER PROCEDURE nomina.usp_generar_nomina
                @id_periodo_nomina BIGINT,
                @usuario_generacion NVARCHAR(100)
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE
                    @fecha_desde DATE,
                    @fecha_hasta DATE,
                    @fecha_pago DATE,
                    @tipo_periodo NVARCHAR(60),
                    @observacion_periodo NVARCHAR(600),
                    @fecha_corte_hora_extra DATE,
                    @id_estado_generada BIGINT,
                    @id_nomina BIGINT,
                    @regimen_inss_empresa NVARCHAR(50),
                    @modo_pasante NVARCHAR(50),
                    @cantidad_trabajadores_empresa INT,
                    @dias_mes_nomina DECIMAL(18,6),
                    @dias_periodo INT,
                    @factor_periodo DECIMAL(18,6),
                    @moneda_base_empresa NVARCHAR(20),
                    @tipo_cambio_oficial_pago DECIMAL(18,6),
                    @snapshot_configuracion NVARCHAR(MAX),
                    @concepto_salario_base BIGINT,
                    @concepto_horas_extra BIGINT,
                    @concepto_vacaciones BIGINT,
                    @concepto_ayuda_pasantia BIGINT,
                    @concepto_servicio BIGINT,
                    @concepto_inss_laboral BIGINT,
                    @concepto_ir_laboral BIGINT,
                    @concepto_prestamo BIGINT,
                    @concepto_descuento_fijo BIGINT,
                    @concepto_inss_patronal BIGINT,
                    @concepto_fondo_ahorro BIGINT,
                    @concepto_retencion_servicio BIGINT;

                SELECT
                    @fecha_desde = fecha_desde,
                    @fecha_hasta = fecha_hasta,
                    @fecha_pago = fecha_pago,
                    @tipo_periodo = tipo_periodo,
                    @observacion_periodo = observacion
                FROM nomina.periodo_nomina
                WHERE id_periodo_nomina = @id_periodo_nomina;

                IF @fecha_desde IS NULL
                    THROW 62001, ''El periodo de nomina no existe.'', 1;

                IF EXISTS (SELECT 1 FROM nomina.nomina WHERE id_periodo_nomina = @id_periodo_nomina)
                    THROW 62002, ''La nomina de este periodo ya fue generada.'', 1;

                IF ISJSON(@observacion_periodo) = 1
                BEGIN
                    SET @fecha_corte_hora_extra = TRY_CONVERT(DATE, JSON_VALUE(@observacion_periodo, ''$.fechaCorteHoraExtra''));
                END;

                IF @fecha_corte_hora_extra IS NULL OR @fecha_corte_hora_extra < @fecha_desde OR @fecha_corte_hora_extra > @fecha_hasta
                BEGIN
                    SET @fecha_corte_hora_extra = @fecha_hasta;
                END;

                SELECT @id_estado_generada = id_estado_nomina
                FROM nomina.estado_nomina
                WHERE codigo_estado_nomina = N''GENERADA'';

                SET @regimen_inss_empresa = UPPER(COALESCE(nomina.fn_obtener_parametro_texto(N''REGIMEN_INSS_EMPRESA''), N''INTEGRAL''));
                SET @modo_pasante = UPPER(COALESCE(nomina.fn_obtener_parametro_texto(N''MODO_PASANTIA_POR_DEFECTO''), N''NO_NOMINA''));
                SET @cantidad_trabajadores_empresa = TRY_CAST(nomina.fn_obtener_parametro_decimal(N''CANTIDAD_TRABAJADORES_EMPRESA'') AS INT);
                SET @dias_mes_nomina = NULLIF(nomina.fn_obtener_parametro_decimal(N''DIAS_MES_NOMINA''), 0);
                SET @moneda_base_empresa =
                (
                    SELECT TOP (1) UPPER(COALESCE(NULLIF(moneda_base, N''''), N''NIO''))
                    FROM empresa.empresa
                    ORDER BY id_empresa
                );

                IF @cantidad_trabajadores_empresa IS NULL OR @cantidad_trabajadores_empresa < 1
                    SET @cantidad_trabajadores_empresa = 1;

                IF @dias_mes_nomina IS NULL
                    SET @dias_mes_nomina = 30;

                IF @moneda_base_empresa IS NULL OR LEN(@moneda_base_empresa) = 0
                    SET @moneda_base_empresa = N''NIO'';

                SET @dias_periodo = DATEDIFF(DAY, @fecha_desde, @fecha_hasta) + 1;
                IF @dias_periodo < 1
                    SET @dias_periodo = 1;

                SET @factor_periodo = CAST(@dias_periodo AS DECIMAL(18,6)) / @dias_mes_nomina;
                IF @factor_periodo <= 0
                    SET @factor_periodo = 1;

                IF @moneda_base_empresa = N''NIO''
                BEGIN
                    SELECT TOP (1) @tipo_cambio_oficial_pago = valor_tipo_cambio
                    FROM parametros.tipo_cambio_oficial
                    WHERE moneda_origen = N''USD''
                      AND moneda_destino = N''NIO''
                      AND fecha_tipo_cambio <= @fecha_pago
                    ORDER BY fecha_tipo_cambio DESC, id_tipo_cambio_oficial DESC;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM rrhh.contrato c
                        INNER JOIN rrhh.empleado e
                            ON e.id_empleado = c.id_empleado
                        INNER JOIN rrhh.estado_empleado ee
                            ON ee.id_estado_empleado = e.id_estado_empleado
                           AND ee.codigo_estado_empleado = N''ACTIVO''
                        WHERE e.activo = 1
                          AND c.es_contrato_vigente = 1
                          AND c.fecha_inicio <= @fecha_hasta
                          AND (c.fecha_fin IS NULL OR c.fecha_fin >= @fecha_desde)
                          AND UPPER(LTRIM(RTRIM(COALESCE(c.moneda, N''NIO'')))) IN (N''USD'', N''US$'', N''DOLAR'', N''DOLARES'', N''DÓLAR'', N''DÓLARES'')
                    )
                    AND (@tipo_cambio_oficial_pago IS NULL OR @tipo_cambio_oficial_pago <= 0)
                    BEGIN
                        THROW 62003, ''No hay tipo de cambio oficial BCN disponible para la fecha de pago de la nomina.'', 1;
                    END;
                END;

                IF @tipo_cambio_oficial_pago IS NULL OR @tipo_cambio_oficial_pago <= 0
                    SET @tipo_cambio_oficial_pago = 1;

                SET @snapshot_configuracion =
                (
                    SELECT
                        @regimen_inss_empresa AS regimenInssEmpresa,
                        @cantidad_trabajadores_empresa AS cantidadTrabajadoresEmpresa,
                        @modo_pasante AS modoPasantiaPorDefecto,
                        @moneda_base_empresa AS monedaBaseEmpresa,
                        @tipo_cambio_oficial_pago AS tipoCambioOficialUsdBase,
                        CONVERT(NVARCHAR(10), @fecha_corte_hora_extra, 23) AS fechaCorteHoraExtra,
                        @tipo_periodo AS tipoPeriodo,
                        @dias_periodo AS diasPeriodo,
                        @dias_mes_nomina AS diasMesNomina,
                        CONVERT(NVARCHAR(19), SYSDATETIME(), 126) AS fechaCalculo
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                );

                SELECT @concepto_salario_base = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''SALARIO_BASE'';
                SELECT @concepto_horas_extra = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''HORAS_EXTRA'';
                SELECT @concepto_vacaciones = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''VACACIONES'';
                SELECT @concepto_ayuda_pasantia = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''AYUDA_PASANTIA'';
                SELECT @concepto_servicio = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''SERVICIO_PROFESIONAL'';
                SELECT @concepto_inss_laboral = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''INSS_LABORAL'';
                SELECT @concepto_ir_laboral = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''IR_LABORAL'';
                SELECT @concepto_prestamo = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''PRESTAMO'';
                SELECT @concepto_descuento_fijo = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''DEDUCCION_FIJA'';
                SELECT @concepto_inss_patronal = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''INSS_PATRONAL'';
                SELECT @concepto_fondo_ahorro = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''FONDO_AHORRO'';
                SELECT @concepto_retencion_servicio = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''RETENCION_SERVICIO'';

                BEGIN TRAN;

                INSERT INTO nomina.nomina
                (
                    id_periodo_nomina,
                    id_estado_nomina,
                    fecha_generacion,
                    usuario_generacion,
                    observacion
                )
                VALUES
                (
                    @id_periodo_nomina,
                    @id_estado_generada,
                    SYSDATETIME(),
                    @usuario_generacion,
                    @snapshot_configuracion
                );

                SET @id_nomina = SCOPE_IDENTITY();

                DECLARE cur_empleados CURSOR LOCAL FAST_FORWARD FOR
                SELECT
                    e.id_empleado,
                    c.id_contrato,
                    c.salario_base_mensual,
                    UPPER(LTRIM(RTRIM(COALESCE(c.moneda, N''NIO'')))) AS moneda_contrato,
                    UPPER(tc.codigo_tipo_contrato) AS codigo_tipo_contrato
                FROM rrhh.empleado e
                INNER JOIN rrhh.contrato c
                    ON c.id_empleado = e.id_empleado
                   AND c.es_contrato_vigente = 1
                INNER JOIN rrhh.tipo_contrato tc
                    ON tc.id_tipo_contrato = c.id_tipo_contrato
                INNER JOIN rrhh.estado_empleado ee
                    ON ee.id_estado_empleado = e.id_estado_empleado
                   AND ee.codigo_estado_empleado = N''ACTIVO''
                WHERE e.activo = 1
                  AND c.fecha_inicio <= @fecha_hasta
                  AND (c.fecha_fin IS NULL OR c.fecha_fin >= @fecha_desde);

                DECLARE
                    @id_empleado BIGINT,
                    @id_contrato BIGINT,
                    @salario_base_mensual DECIMAL(18,2),
                    @moneda_contrato NVARCHAR(20),
                    @codigo_tipo_contrato NVARCHAR(60),
                    @tipo_pago NVARCHAR(40),
                    @salario_base_periodo DECIMAL(18,2),
                    @horas_extra DECIMAL(18,2),
                    @vacaciones DECIMAL(18,2),
                    @movimientos_ingreso_total DECIMAL(18,2),
                    @movimientos_ingreso_gravado DECIMAL(18,2),
                    @movimientos_ingreso_base_inss DECIMAL(18,2),
                    @devengados_ingreso_total DECIMAL(18,2),
                    @devengados_ingreso_gravado DECIMAL(18,2),
                    @devengados_ingreso_base_inss DECIMAL(18,2),
                    @movimientos_deduccion_total DECIMAL(18,2),
                    @fondo_ahorro_variable DECIMAL(18,2),
                    @descuento_fijo_total DECIMAL(18,2),
                    @fondo_ahorro_fijo DECIMAL(18,2),
                    @descuento_fijo DECIMAL(18,2),
                    @prestamo DECIMAL(18,2),
                    @total_ingresos DECIMAL(18,2),
                    @ingresos_gravables_periodo DECIMAL(18,2),
                    @base_inss DECIMAL(18,2),
                    @inss_laboral DECIMAL(18,2),
                    @inss_patronal DECIMAL(18,2),
                    @base_ir_periodo DECIMAL(18,2),
                    @base_ir_mensual_equivalente DECIMAL(18,2),
                    @ir_retenido DECIMAL(18,2),
                    @total_deducciones DECIMAL(18,2),
                    @total_aportes DECIMAL(18,2),
                    @neto DECIMAL(18,2),
                    @id_nomina_detalle BIGINT,
                    @codigo_inss_laboral NVARCHAR(80),
                    @codigo_inss_patronal NVARCHAR(80),
                    @codigo_retencion_servicio NVARCHAR(80);

                OPEN cur_empleados;
                FETCH NEXT FROM cur_empleados INTO @id_empleado, @id_contrato, @salario_base_mensual, @moneda_contrato, @codigo_tipo_contrato;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF @moneda_base_empresa = N''NIO''
                       AND @moneda_contrato IN (N''USD'', N''US$'', N''DOLAR'', N''DOLARES'', N''DÓLAR'', N''DÓLARES'')
                    BEGIN
                        SET @salario_base_mensual = ROUND(@salario_base_mensual * @tipo_cambio_oficial_pago, 2);
                    END;

                    SET @salario_base_periodo = ROUND((@salario_base_mensual / @dias_mes_nomina) * @dias_periodo, 2);
                    SET @horas_extra = 0;
                    SET @vacaciones = 0;
                    SET @movimientos_ingreso_total = 0;
                    SET @movimientos_ingreso_gravado = 0;
                    SET @movimientos_ingreso_base_inss = 0;
                    SET @devengados_ingreso_total = 0;
                    SET @devengados_ingreso_gravado = 0;
                    SET @devengados_ingreso_base_inss = 0;
                    SET @movimientos_deduccion_total = 0;
                    SET @fondo_ahorro_variable = 0;
                    SET @descuento_fijo_total = 0;
                    SET @fondo_ahorro_fijo = 0;
                    SET @descuento_fijo = 0;
                    SET @prestamo = 0;
                    SET @total_ingresos = 0;
                    SET @ingresos_gravables_periodo = 0;
                    SET @base_inss = 0;
                    SET @inss_laboral = 0;
                    SET @inss_patronal = 0;
                    SET @base_ir_periodo = 0;
                    SET @base_ir_mensual_equivalente = 0;
                    SET @ir_retenido = 0;
                    SET @total_deducciones = 0;
                    SET @total_aportes = 0;
                    SET @neto = 0;
                    SET @codigo_inss_laboral = NULL;
                    SET @codigo_inss_patronal = NULL;
                    SET @codigo_retencion_servicio = NULL;

                    IF @codigo_tipo_contrato IN (N''SERVICIOS'', N''PROFESIONAL_PERSONA_NATURAL'', N''SERVICIO_GENERAL'')
                        SET @tipo_pago = N''SERVICIO_PROFESIONAL'';
                    ELSE IF @codigo_tipo_contrato = N''PASANTIA'' AND @modo_pasante <> N''COMO_EMPLEADO''
                        SET @tipo_pago = N''PASANTE_AYUDA'';
                    ELSE
                        SET @tipo_pago = N''EMPLEADO_NOMINA'';

                    SELECT
                        @movimientos_ingreso_total = ISNULL(SUM(CASE WHEN tcn.codigo_tipo_concepto = N''INGRESO'' THEN mv.monto ELSE 0 END), 0),
                        @movimientos_ingreso_gravado = ISNULL(SUM(CASE WHEN tcn.codigo_tipo_concepto = N''INGRESO'' AND cn.afecta_ingresos_gravables = 1 THEN mv.monto ELSE 0 END), 0),
                        @movimientos_ingreso_base_inss = ISNULL(SUM(CASE WHEN tcn.codigo_tipo_concepto = N''INGRESO'' AND cn.afecta_base_inss = 1 THEN mv.monto ELSE 0 END), 0),
                        @movimientos_deduccion_total = ISNULL(SUM(CASE WHEN tcn.codigo_tipo_concepto = N''DEDUCCION'' THEN mv.monto ELSE 0 END), 0),
                        @fondo_ahorro_variable = ISNULL(SUM(CASE WHEN cn.codigo_concepto = N''FONDO_AHORRO'' THEN mv.monto ELSE 0 END), 0)
                    FROM nomina.movimiento_variable_empleado mv
                    INNER JOIN nomina.concepto_nomina cn
                        ON cn.id_concepto_nomina = mv.id_concepto_nomina
                    INNER JOIN nomina.tipo_concepto_nomina tcn
                        ON tcn.id_tipo_concepto_nomina = cn.id_tipo_concepto_nomina
                    WHERE mv.id_empleado = @id_empleado
                      AND mv.aplicado_en_nomina = 0
                      AND mv.activo = 1
                      AND mv.fecha_movimiento BETWEEN @fecha_desde AND @fecha_hasta;

                    SELECT
                        @devengados_ingreso_total = ISNULL(SUM(CASE WHEN tcn.codigo_tipo_concepto = N''INGRESO'' THEN dv.monto_devengado ELSE 0 END), 0),
                        @devengados_ingreso_gravado = ISNULL(SUM(CASE WHEN tcn.codigo_tipo_concepto = N''INGRESO'' AND cn.afecta_ingresos_gravables = 1 THEN dv.monto_devengado ELSE 0 END), 0),
                        @devengados_ingreso_base_inss = ISNULL(SUM(CASE WHEN tcn.codigo_tipo_concepto = N''INGRESO'' AND cn.afecta_base_inss = 1 THEN dv.monto_devengado ELSE 0 END), 0)
                    FROM nomina.devengado_variable_periodo dv
                    INNER JOIN nomina.concepto_nomina cn
                        ON cn.id_concepto_nomina = dv.id_concepto_nomina
                    INNER JOIN nomina.tipo_concepto_nomina tcn
                        ON tcn.id_tipo_concepto_nomina = cn.id_tipo_concepto_nomina
                    WHERE dv.id_empleado = @id_empleado
                      AND dv.aplicado_en_nomina = 0
                      AND dv.estado_devengado = N''APROBADO''
                      AND dv.fecha_desde <= @fecha_hasta
                      AND dv.fecha_hasta >= @fecha_desde;

                    SELECT
                        @descuento_fijo_total = ISNULL(SUM(monto_mensual), 0),
                        @fondo_ahorro_fijo = ISNULL(SUM(CASE WHEN UPPER(REPLACE(descripcion_descuento, N'' '', N'''')) LIKE N''%FONDOAHORRO%'' THEN monto_mensual ELSE 0 END), 0)
                    FROM nomina.descuento_fijo_empleado
                    WHERE id_empleado = @id_empleado
                      AND activo = 1
                      AND vigencia_desde <= @fecha_hasta
                      AND (vigencia_hasta IS NULL OR vigencia_hasta >= @fecha_desde);

                    SET @descuento_fijo = @descuento_fijo_total - @fondo_ahorro_fijo;

                    SELECT @prestamo = ISNULL(SUM(CASE WHEN saldo_pendiente < cuota_mensual THEN saldo_pendiente ELSE cuota_mensual END), 0)
                    FROM nomina.prestamo_empleado
                    WHERE id_empleado = @id_empleado
                      AND activo = 1
                      AND saldo_pendiente > 0;

                    IF @tipo_pago = N''EMPLEADO_NOMINA''
                    BEGIN
                        SELECT @horas_extra = ISNULL(SUM(ROUND(((@salario_base_mensual / NULLIF(nomina.fn_obtener_parametro_decimal(N''HORAS_MES_BASE''), 0)) * he.cantidad_horas) * thex.factor_pago, 2)), 0)
                        FROM rrhh.hora_extra he
                        INNER JOIN rrhh.tipo_hora_extra thex
                            ON thex.id_tipo_hora_extra = he.id_tipo_hora_extra
                        WHERE he.id_empleado = @id_empleado
                          AND he.estado_hora_extra = N''APROBADA''
                          AND he.pagada_en_nomina = 0
                          AND he.fecha_hora_extra BETWEEN @fecha_desde AND @fecha_corte_hora_extra;

                        SELECT @vacaciones = ISNULL(SUM(ROUND((@salario_base_mensual / @dias_mes_nomina) * ISNULL(v.dias_aprobados, 0), 2)), 0)
                        FROM rrhh.vacacion v
                        WHERE v.id_empleado = @id_empleado
                          AND v.estado_vacacion = N''APROBADA''
                          AND v.pagada_en_nomina = 0
                          AND v.fecha_inicio BETWEEN @fecha_desde AND @fecha_hasta;

                        SET @total_ingresos =
                            ISNULL(@salario_base_periodo, 0)
                          + ISNULL(@horas_extra, 0)
                          + ISNULL(@vacaciones, 0)
                          + ISNULL(@movimientos_ingreso_total, 0)
                          + ISNULL(@devengados_ingreso_total, 0);

                        SET @ingresos_gravables_periodo =
                            ISNULL(@salario_base_periodo, 0)
                          + ISNULL(@horas_extra, 0)
                          + ISNULL(@vacaciones, 0)
                          + ISNULL(@movimientos_ingreso_gravado, 0)
                          + ISNULL(@devengados_ingreso_gravado, 0);

                        SET @base_inss =
                            ISNULL(@salario_base_periodo, 0)
                          + ISNULL(@horas_extra, 0)
                          + ISNULL(@vacaciones, 0)
                          + ISNULL(@movimientos_ingreso_base_inss, 0)
                          + ISNULL(@devengados_ingreso_base_inss, 0);

                        IF @regimen_inss_empresa = N''IVM_RP''
                        BEGIN
                            SET @codigo_inss_laboral = N''INSS_LABORAL_IVM_RP'';
                            SET @codigo_inss_patronal =
                                CASE WHEN @cantidad_trabajadores_empresa < 50
                                    THEN N''INSS_PATRONAL_IVM_RP_LT50''
                                    ELSE N''INSS_PATRONAL_IVM_RP_GE50''
                                END;
                        END
                        ELSE
                        BEGIN
                            SET @codigo_inss_laboral = N''INSS_LABORAL_INTEGRAL'';
                            SET @codigo_inss_patronal =
                                CASE WHEN @cantidad_trabajadores_empresa < 50
                                    THEN N''INSS_PATRONAL_INTEGRAL_LT50''
                                    ELSE N''INSS_PATRONAL_INTEGRAL_GE50''
                                END;
                        END;

                        SET @inss_laboral = nomina.fn_calcular_contribucion(@codigo_inss_laboral, @fecha_pago, @base_inss);
                        SET @inss_patronal = nomina.fn_calcular_contribucion(@codigo_inss_patronal, @fecha_pago, @base_inss);

                        SET @base_ir_periodo =
                            CASE
                                WHEN @ingresos_gravables_periodo - @inss_laboral - @fondo_ahorro_variable - @fondo_ahorro_fijo < 0
                                    THEN 0
                                ELSE @ingresos_gravables_periodo - @inss_laboral - @fondo_ahorro_variable - @fondo_ahorro_fijo
                            END;

                        SET @base_ir_mensual_equivalente =
                            CASE
                                WHEN @factor_periodo <= 0 THEN @base_ir_periodo
                                ELSE ROUND(@base_ir_periodo / @factor_periodo, 2)
                            END;

                        SET @ir_retenido = ROUND(nomina.fn_calcular_ir_laboral_mensual(@fecha_pago, @base_ir_mensual_equivalente) * @factor_periodo, 2);

                        SET @total_deducciones =
                            ISNULL(@inss_laboral, 0)
                          + ISNULL(@ir_retenido, 0)
                          + ISNULL(@movimientos_deduccion_total, 0)
                          + ISNULL(@descuento_fijo_total, 0)
                          + ISNULL(@prestamo, 0);

                        SET @total_aportes = ISNULL(@inss_patronal, 0);
                    END
                    ELSE IF @tipo_pago = N''PASANTE_AYUDA''
                    BEGIN
                        SET @total_ingresos = ISNULL(@salario_base_periodo, 0);
                        SET @neto = @total_ingresos;
                    END
                    ELSE
                    BEGIN
                        SET @total_ingresos = ISNULL(@salario_base_periodo, 0);

                        IF @codigo_tipo_contrato = N''SERVICIO_GENERAL''
                            SET @codigo_retencion_servicio = N''RETENCION_SERVICIO_GENERAL'';
                        ELSE
                            SET @codigo_retencion_servicio = N''RETENCION_SERVICIO_PROF_NATURAL'';

                        SET @ir_retenido = nomina.fn_calcular_contribucion(@codigo_retencion_servicio, @fecha_pago, @total_ingresos);
                        SET @total_deducciones = ISNULL(@ir_retenido, 0);
                        SET @total_aportes = 0;
                    END;

                    IF @tipo_pago <> N''PASANTE_AYUDA''
                    BEGIN
                        SET @neto = @total_ingresos - @total_deducciones;
                    END;

                    INSERT INTO nomina.nomina_detalle
                    (
                        id_nomina,
                        id_empleado,
                        id_contrato,
                        salario_base_periodo,
                        total_ingresos,
                        total_deducciones,
                        total_aportes_patronales,
                        neto_pagar,
                        inss_laboral,
                        inss_patronal,
                        ir_laboral,
                        ir_patronal
                    )
                    VALUES
                    (
                        @id_nomina,
                        @id_empleado,
                        @id_contrato,
                        @salario_base_periodo,
                        @total_ingresos,
                        @total_deducciones,
                        @total_aportes,
                        @neto,
                        @inss_laboral,
                        @inss_patronal,
                        @ir_retenido,
                        0
                    );

                    SET @id_nomina_detalle = SCOPE_IDENTITY();

                    IF @tipo_pago = N''EMPLEADO_NOMINA''
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        VALUES (@id_nomina_detalle, @concepto_salario_base, @salario_base_periodo, N''Salario base del periodo'');

                        IF ISNULL(@horas_extra, 0) > 0
                            INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                            VALUES (@id_nomina_detalle, @concepto_horas_extra, @horas_extra, N''Horas extra aprobadas no pagadas'');

                        IF ISNULL(@vacaciones, 0) > 0
                            INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                            VALUES (@id_nomina_detalle, @concepto_vacaciones, @vacaciones, N''Vacaciones aprobadas no pagadas'');
                    END
                    ELSE IF @tipo_pago = N''PASANTE_AYUDA''
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        VALUES (@id_nomina_detalle, @concepto_ayuda_pasantia, @salario_base_periodo, N''Ayuda economica de pasantia'');
                    END
                    ELSE
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        VALUES (@id_nomina_detalle, @concepto_servicio, @salario_base_periodo, N''Servicio profesional del periodo'');
                    END;

                    INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                    SELECT
                        @id_nomina_detalle,
                        mv.id_concepto_nomina,
                        mv.monto,
                        COALESCE(NULLIF(mv.observacion, N''''), N''Movimiento variable'')
                    FROM nomina.movimiento_variable_empleado mv
                    INNER JOIN nomina.concepto_nomina cn
                        ON cn.id_concepto_nomina = mv.id_concepto_nomina
                    WHERE mv.id_empleado = @id_empleado
                      AND mv.aplicado_en_nomina = 0
                      AND mv.activo = 1
                      AND mv.fecha_movimiento BETWEEN @fecha_desde AND @fecha_hasta
                      AND (
                            @tipo_pago = N''EMPLEADO_NOMINA''
                         OR (@tipo_pago = N''PASANTE_AYUDA'' AND cn.codigo_concepto <> N''FONDO_AHORRO'')
                         OR (@tipo_pago = N''SERVICIO_PROFESIONAL'' AND cn.codigo_concepto <> N''FONDO_AHORRO'')
                      );

                    INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                    SELECT
                        @id_nomina_detalle,
                        dv.id_concepto_nomina,
                        dv.monto_devengado,
                        COALESCE(NULLIF(dv.referencia_operativa, N''''), N''Devengado variable'')
                    FROM nomina.devengado_variable_periodo dv
                    INNER JOIN nomina.concepto_nomina cn
                        ON cn.id_concepto_nomina = dv.id_concepto_nomina
                    INNER JOIN nomina.tipo_concepto_nomina tcn
                        ON tcn.id_tipo_concepto_nomina = cn.id_tipo_concepto_nomina
                    WHERE dv.id_empleado = @id_empleado
                      AND dv.aplicado_en_nomina = 0
                      AND dv.estado_devengado = N''APROBADO''
                      AND dv.fecha_desde <= @fecha_hasta
                      AND dv.fecha_hasta >= @fecha_desde
                      AND tcn.codigo_tipo_concepto = N''INGRESO''
                      AND @tipo_pago = N''EMPLEADO_NOMINA'';

                    IF ISNULL(@fondo_ahorro_fijo, 0) > 0
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        SELECT
                            @id_nomina_detalle,
                            @concepto_fondo_ahorro,
                            d.monto_mensual,
                            d.descripcion_descuento
                        FROM nomina.descuento_fijo_empleado d
                        WHERE d.id_empleado = @id_empleado
                          AND d.activo = 1
                          AND d.vigencia_desde <= @fecha_hasta
                          AND (d.vigencia_hasta IS NULL OR d.vigencia_hasta >= @fecha_desde)
                          AND UPPER(REPLACE(d.descripcion_descuento, N'' '', N'''')) LIKE N''%FONDOAHORRO%'';
                    END;

                    IF ISNULL(@descuento_fijo, 0) > 0
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        SELECT
                            @id_nomina_detalle,
                            @concepto_descuento_fijo,
                            d.monto_mensual,
                            d.descripcion_descuento
                        FROM nomina.descuento_fijo_empleado d
                        WHERE d.id_empleado = @id_empleado
                          AND d.activo = 1
                          AND d.vigencia_desde <= @fecha_hasta
                          AND (d.vigencia_hasta IS NULL OR d.vigencia_hasta >= @fecha_desde)
                          AND UPPER(REPLACE(d.descripcion_descuento, N'' '', N'''')) NOT LIKE N''%FONDOAHORRO%'';
                    END;

                    IF ISNULL(@prestamo, 0) > 0
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        SELECT
                            @id_nomina_detalle,
                            @concepto_prestamo,
                            CASE WHEN p.saldo_pendiente < p.cuota_mensual THEN p.saldo_pendiente ELSE p.cuota_mensual END,
                            p.descripcion_prestamo
                        FROM nomina.prestamo_empleado p
                        WHERE p.id_empleado = @id_empleado
                          AND p.activo = 1
                          AND p.saldo_pendiente > 0;
                    END;

                    IF ISNULL(@inss_laboral, 0) > 0
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        VALUES (@id_nomina_detalle, @concepto_inss_laboral, @inss_laboral, N''Retencion INSS laboral'');
                    END;

                    IF ISNULL(@ir_retenido, 0) > 0
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        VALUES
                        (
                            @id_nomina_detalle,
                            CASE WHEN @tipo_pago = N''SERVICIO_PROFESIONAL'' THEN @concepto_retencion_servicio ELSE @concepto_ir_laboral END,
                            @ir_retenido,
                            CASE WHEN @tipo_pago = N''SERVICIO_PROFESIONAL'' THEN N''Retencion de servicio profesional'' ELSE N''Retencion IR laboral'' END
                        );
                    END;

                    IF ISNULL(@inss_patronal, 0) > 0
                    BEGIN
                        INSERT INTO nomina.nomina_detalle_concepto (id_nomina_detalle, id_concepto_nomina, monto, referencia)
                        VALUES (@id_nomina_detalle, @concepto_inss_patronal, @inss_patronal, N''Aporte patronal INSS'');
                    END;

                    IF @tipo_pago = N''EMPLEADO_NOMINA''
                    BEGIN
                        UPDATE rrhh.hora_extra
                        SET pagada_en_nomina = 1
                        WHERE id_empleado = @id_empleado
                          AND estado_hora_extra = N''APROBADA''
                          AND pagada_en_nomina = 0
                          AND fecha_hora_extra BETWEEN @fecha_desde AND @fecha_corte_hora_extra;

                        UPDATE rrhh.vacacion
                        SET pagada_en_nomina = 1
                        WHERE id_empleado = @id_empleado
                          AND estado_vacacion = N''APROBADA''
                          AND pagada_en_nomina = 0
                          AND fecha_inicio BETWEEN @fecha_desde AND @fecha_hasta;
                    END;

                    UPDATE nomina.movimiento_variable_empleado
                    SET aplicado_en_nomina = 1
                    WHERE id_empleado = @id_empleado
                      AND aplicado_en_nomina = 0
                      AND activo = 1
                      AND fecha_movimiento BETWEEN @fecha_desde AND @fecha_hasta
                      AND (
                            @tipo_pago = N''EMPLEADO_NOMINA''
                         OR (@tipo_pago = N''PASANTE_AYUDA'' AND id_concepto_nomina <> @concepto_fondo_ahorro)
                         OR (@tipo_pago = N''SERVICIO_PROFESIONAL'' AND id_concepto_nomina <> @concepto_fondo_ahorro)
                      );

                    IF @tipo_pago = N''EMPLEADO_NOMINA''
                    BEGIN
                        UPDATE nomina.devengado_variable_periodo
                        SET aplicado_en_nomina = 1,
                            estado_devengado = N''APLICADO'',
                            id_periodo_nomina = @id_periodo_nomina
                        WHERE id_empleado = @id_empleado
                          AND aplicado_en_nomina = 0
                          AND estado_devengado = N''APROBADO''
                          AND fecha_desde <= @fecha_hasta
                          AND fecha_hasta >= @fecha_desde;

                        UPDATE nomina.prestamo_empleado
                        SET saldo_pendiente =
                            CASE
                                WHEN saldo_pendiente <= cuota_mensual THEN 0
                                ELSE saldo_pendiente - cuota_mensual
                            END
                        WHERE id_empleado = @id_empleado
                          AND activo = 1
                          AND saldo_pendiente > 0;
                    END;

                    FETCH NEXT FROM cur_empleados INTO @id_empleado, @id_contrato, @salario_base_mensual, @moneda_contrato, @codigo_tipo_contrato;
                END;

                CLOSE cur_empleados;
                DEALLOCATE cur_empleados;

                UPDATE nomina.periodo_nomina
                SET id_estado_periodo_nomina =
                    (
                        SELECT id_estado_periodo_nomina
                        FROM nomina.estado_periodo_nomina
                        WHERE codigo_estado_periodo = N''GENERADO''
                    )
                WHERE id_periodo_nomina = @id_periodo_nomina;

                COMMIT TRAN;

                SELECT @id_nomina AS id_nomina_generada;
            END;
            ');
            """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
        EnsureLiquidationSetup(connection);
    }

    private static void EnsureLiquidationSetup(SqlConnection connection)
    {
        const string sql = """
            DECLARE @tipo_ingreso BIGINT =
            (
                SELECT id_tipo_concepto_nomina
                FROM nomina.tipo_concepto_nomina
                WHERE codigo_tipo_concepto = N'INGRESO'
            );

            DECLARE @tipo_deduccion BIGINT =
            (
                SELECT id_tipo_concepto_nomina
                FROM nomina.tipo_concepto_nomina
                WHERE codigo_tipo_concepto = N'DEDUCCION'
            );

            DECLARE @tipo_aporte BIGINT =
            (
                SELECT id_tipo_concepto_nomina
                FROM nomina.tipo_concepto_nomina
                WHERE codigo_tipo_concepto = N'APORTE_PATRONAL'
            );

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'LIQ_SALARIO_PENDIENTE')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_ingreso, N'LIQ_SALARIO_PENDIENTE', N'Salario pendiente', 1, 1, 1, 61, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'LIQ_VACACIONES_POR_PAGAR')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_ingreso, N'LIQ_VACACIONES_POR_PAGAR', N'Vacaciones por pagar', 1, 1, 1, 62, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'LIQ_AGUINALDO_PROPORCIONAL')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_ingreso, N'LIQ_AGUINALDO_PROPORCIONAL', N'Aguinaldo proporcional', 0, 0, 1, 63, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'LIQ_INDEMNIZACION_ART45')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_ingreso, N'LIQ_INDEMNIZACION_ART45', N'Indemnizacion art. 45', 0, 0, 1, 64, 1);
            END;

            IF NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'INATEC_PATRONAL')
            BEGIN
                INSERT INTO nomina.concepto_nomina
                (
                    id_tipo_concepto_nomina,
                    codigo_concepto,
                    nombre_concepto,
                    afecta_ingresos_gravables,
                    afecta_base_inss,
                    afecta_neto,
                    orden_visual,
                    activo
                )
                VALUES (@tipo_aporte, N'INATEC_PATRONAL', N'INATEC patronal', 0, 0, 0, 82, 1);
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM nomina.parametro_contribucion
                WHERE codigo_contribucion = N'INATEC_PATRONAL'
                  AND vigencia_desde = '2024-01-01'
            )
            BEGIN
                INSERT INTO nomina.parametro_contribucion
                (
                    codigo_contribucion,
                    nombre_contribucion,
                    tipo_contribucion,
                    vigencia_desde,
                    vigencia_hasta,
                    porcentaje,
                    techo_mensual,
                    activo,
                    fecha_registro
                )
                VALUES (N'INATEC_PATRONAL', N'INATEC patronal', N'PATRONAL', '2024-01-01', NULL, 2.0, NULL, 1, SYSDATETIME());
            END;

            EXEC(N'
            CREATE OR ALTER PROCEDURE nomina.usp_generar_liquidacion
                @id_empleado BIGINT,
                @fecha_liquidacion DATE,
                @fecha_baja DATE,
                @motivo_liquidacion NVARCHAR(200),
                @usuario_registro NVARCHAR(100),
                @causal_codigo NVARCHAR(50) = N''RENUNCIA_ART44'',
                @dias_salario_pendiente DECIMAL(18,2) = 0,
                @monto_salario_pendiente DECIMAL(18,2) = 0,
                @dias_vacaciones DECIMAL(18,2) = 0,
                @monto_vacaciones DECIMAL(18,2) = 0,
                @dias_aguinaldo DECIMAL(18,2) = 0,
                @monto_aguinaldo DECIMAL(18,2) = 0,
                @dias_indemnizacion DECIMAL(18,2) = 0,
                @monto_indemnizacion DECIMAL(18,2) = 0,
                @inss_laboral DECIMAL(18,2) = 0,
                @ir_laboral DECIMAL(18,2) = 0,
                @inss_patronal DECIMAL(18,2) = 0,
                @inatec_patronal DECIMAL(18,2) = 0
            AS
            BEGIN
                SET NOCOUNT ON;

                DECLARE @id_contrato BIGINT;
                DECLARE @salario_base DECIMAL(18,2);
                DECLARE @id_liquidacion BIGINT;
                DECLARE @id_estado_retirado BIGINT;
                DECLARE @concepto_salario_pendiente BIGINT;
                DECLARE @concepto_vacaciones BIGINT;
                DECLARE @concepto_aguinaldo BIGINT;
                DECLARE @concepto_indemnizacion BIGINT;
                DECLARE @concepto_inss BIGINT;
                DECLARE @concepto_ir BIGINT;
                DECLARE @concepto_inss_patronal BIGINT;
                DECLARE @concepto_inatec BIGINT;
                DECLARE @total_ingresos DECIMAL(18,2);
                DECLARE @total_deducciones DECIMAL(18,2);
                DECLARE @neto_liquidacion DECIMAL(18,2);

                IF @fecha_baja < @fecha_liquidacion
                BEGIN
                    SET @fecha_liquidacion = @fecha_baja;
                END;

                SELECT TOP (1)
                    @id_contrato = c.id_contrato,
                    @salario_base = c.salario_base_mensual
                FROM rrhh.contrato c
                WHERE c.id_empleado = @id_empleado
                  AND c.es_contrato_vigente = 1
                ORDER BY c.id_contrato DESC;

                IF @id_contrato IS NULL
                    THROW 62010, ''El empleado no tiene contrato vigente.'', 1;

                IF EXISTS (SELECT 1 FROM nomina.liquidacion WHERE id_contrato = @id_contrato)
                    THROW 62011, ''Ya existe una liquidacion registrada para el contrato vigente del empleado.'', 1;

                SELECT @id_estado_retirado = id_estado_empleado
                FROM rrhh.estado_empleado
                WHERE codigo_estado_empleado = N''RETIRADO'';

                SELECT @concepto_salario_pendiente = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''LIQ_SALARIO_PENDIENTE'';
                SELECT @concepto_vacaciones = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''LIQ_VACACIONES_POR_PAGAR'';
                SELECT @concepto_aguinaldo = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''LIQ_AGUINALDO_PROPORCIONAL'';
                SELECT @concepto_indemnizacion = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''LIQ_INDEMNIZACION_ART45'';
                SELECT @concepto_inss = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''INSS_LABORAL'';
                SELECT @concepto_ir = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''IR_LABORAL'';
                SELECT @concepto_inss_patronal = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''INSS_PATRONAL'';
                SELECT @concepto_inatec = id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto = N''INATEC_PATRONAL'';

                SET @dias_salario_pendiente = ROUND(ISNULL(@dias_salario_pendiente, 0), 2);
                SET @monto_salario_pendiente = ROUND(ISNULL(@monto_salario_pendiente, 0), 2);
                SET @dias_vacaciones = ROUND(ISNULL(@dias_vacaciones, 0), 2);
                SET @monto_vacaciones = ROUND(ISNULL(@monto_vacaciones, 0), 2);
                SET @dias_aguinaldo = ROUND(ISNULL(@dias_aguinaldo, 0), 2);
                SET @monto_aguinaldo = ROUND(ISNULL(@monto_aguinaldo, 0), 2);
                SET @dias_indemnizacion = ROUND(ISNULL(@dias_indemnizacion, 0), 2);
                SET @monto_indemnizacion = ROUND(ISNULL(@monto_indemnizacion, 0), 2);
                SET @inss_laboral = ROUND(ISNULL(@inss_laboral, 0), 2);
                SET @ir_laboral = ROUND(ISNULL(@ir_laboral, 0), 2);
                SET @inss_patronal = ROUND(ISNULL(@inss_patronal, 0), 2);
                SET @inatec_patronal = ROUND(ISNULL(@inatec_patronal, 0), 2);

                SET @total_ingresos =
                    @monto_salario_pendiente
                  + @monto_vacaciones
                  + @monto_aguinaldo
                  + @monto_indemnizacion;

                SET @total_deducciones = @inss_laboral + @ir_laboral;
                SET @neto_liquidacion = ROUND(@total_ingresos - @total_deducciones, 2);

                INSERT INTO nomina.liquidacion
                (
                    id_empleado,
                    id_contrato,
                    fecha_liquidacion,
                    fecha_baja,
                    motivo_liquidacion,
                    salario_base_referencia,
                    total_ingresos,
                    total_deducciones,
                    neto_liquidacion,
                    usuario_registro
                )
                VALUES
                (
                    @id_empleado,
                    @id_contrato,
                    @fecha_liquidacion,
                    @fecha_baja,
                    @motivo_liquidacion,
                    @salario_base,
                    @total_ingresos,
                    @total_deducciones,
                    @neto_liquidacion,
                    @usuario_registro
                );

                SET @id_liquidacion = SCOPE_IDENTITY();

                IF @monto_salario_pendiente > 0 AND @concepto_salario_pendiente IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_salario_pendiente, @monto_salario_pendiente, CONCAT(CONVERT(NVARCHAR(30), @dias_salario_pendiente), N'' dia(s) de salario pendiente''));
                END;

                IF @monto_vacaciones > 0 AND @concepto_vacaciones IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_vacaciones, @monto_vacaciones, CONCAT(CONVERT(NVARCHAR(30), @dias_vacaciones), N'' dia(s) de vacaciones por pagar''));
                END;

                IF @monto_aguinaldo > 0 AND @concepto_aguinaldo IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_aguinaldo, @monto_aguinaldo, CONCAT(CONVERT(NVARCHAR(30), @dias_aguinaldo), N'' dia(s) equivalentes de aguinaldo proporcional''));
                END;

                IF @monto_indemnizacion > 0 AND @concepto_indemnizacion IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_indemnizacion, @monto_indemnizacion, CONCAT(@causal_codigo, N'' | '', CONVERT(NVARCHAR(30), @dias_indemnizacion), N'' dia(s) equivalentes''));
                END;

                IF @inss_laboral > 0 AND @concepto_inss IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_inss, @inss_laboral, N''Retencion INSS laboral sobre prestaciones gravables'');
                END;

                IF @ir_laboral > 0 AND @concepto_ir IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_ir, @ir_laboral, N''Retencion IR laboral sobre prestaciones gravables'');
                END;

                IF @inss_patronal > 0 AND @concepto_inss_patronal IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_inss_patronal, @inss_patronal, N''Aporte patronal INSS de liquidacion'');
                END;

                IF @inatec_patronal > 0 AND @concepto_inatec IS NOT NULL
                BEGIN
                    INSERT INTO nomina.liquidacion_detalle (id_liquidacion, id_concepto_nomina, monto, referencia)
                    VALUES (@id_liquidacion, @concepto_inatec, @inatec_patronal, N''Aporte patronal INATEC de liquidacion'');
                END;

                UPDATE rrhh.contrato
                SET
                    es_contrato_vigente = 0,
                    fecha_fin = @fecha_baja
                WHERE id_contrato = @id_contrato;

                UPDATE rrhh.empleado
                SET
                    fecha_baja = @fecha_baja,
                    motivo_baja = @motivo_liquidacion,
                    activo = 0,
                    id_estado_empleado = @id_estado_retirado
                WHERE id_empleado = @id_empleado;

                SELECT
                    @id_liquidacion AS id_liquidacion_generada,
                    @total_ingresos AS total_ingresos,
                    @total_deducciones AS total_deducciones,
                    @neto_liquidacion AS neto_liquidacion;
            END;
            ');
            """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    public static string BuildPeriodObservation(string? note, DateTime? cutoffDate)
    {
        var payload = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(note))
        {
            payload["note"] = note.Trim();
        }

        if (cutoffDate.HasValue)
        {
            payload["fechaCorteHoraExtra"] = cutoffDate.Value.ToString("yyyy-MM-dd");
        }

        return payload.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(payload);
    }

    public static PayrollPeriodObservation ParsePeriodObservation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new PayrollPeriodObservation();
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            DateTime? cutoffDate = null;
            if (root.TryGetProperty("fechaCorteHoraExtra", out var cutoffNode) &&
                DateTime.TryParse(cutoffNode.GetString(), out var parsedCutoff))
            {
                cutoffDate = parsedCutoff.Date;
            }

            return new PayrollPeriodObservation
            {
                Note = root.TryGetProperty("note", out var noteNode) ? noteNode.GetString() : null,
                CutoffDate = cutoffDate,
                Raw = raw,
            };
        }
        catch
        {
            return new PayrollPeriodObservation
            {
                Note = raw.Trim(),
                Raw = raw,
            };
        }
    }

    public static PayrollType ResolvePayrollType(string? contractCode, decimal netTax, decimal inssLaboral, decimal inssPatronal)
    {
        var code = (contractCode ?? string.Empty).Trim().ToUpperInvariant();

        if (ServiceContractCodes.Contains(code))
        {
            return PayrollType.ServicioProfesional;
        }

        if (string.Equals(code, "PASANTIA", StringComparison.OrdinalIgnoreCase))
        {
            return inssLaboral > 0 || inssPatronal > 0
                ? PayrollType.EmpleadoNomina
                : PayrollType.PasanteAyuda;
        }

        return EmployeeContractCodes.Contains(code)
            ? PayrollType.EmpleadoNomina
            : (netTax > 0 && inssLaboral == 0 && inssPatronal == 0
                ? PayrollType.ServicioProfesional
                : PayrollType.EmpleadoNomina);
    }

    public static ReportBrandingDto GetReportBranding(SqlConnection connection)
    {
        const string sql = """
            SELECT TOP (1)
                e.razon_social,
                e.nombre_comercial,
                e.ruc,
                e.telefono,
                e.correo,
                e.direccion,
                COALESCE(cg.logo_sidebar_url, cg.logo_login_url, e.logo_url) AS logo_url,
                cg.texto_footer,
                cg.nombre_gerente_rrhh
            FROM empresa.empresa e
            LEFT JOIN empresa.configuracion_general cg
                ON cg.id_empresa = e.id_empresa
               AND cg.activo = 1
            WHERE e.activo = 1
            ORDER BY e.id_empresa;
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return new ReportBrandingDto
            {
                LegalName = "SIFNIC",
                CompanyName = "SIFNIC",
                FooterText = "Documento generado por SIFNIC.",
                LogoPending = true,
            };
        }

        var legalName = reader.IsDBNull(0) ? "SIFNIC" : reader.GetString(0);
        var companyName = reader.IsDBNull(1) ? legalName : reader.GetString(1);
        var phone = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
        var email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
        var address = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
        var logoUrl = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
        var footerText = reader.IsDBNull(7) ? "Documento generado por SIFNIC." : reader.GetString(7);
        var hrManagerName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);

        return new ReportBrandingDto
        {
            LegalName = legalName,
            CompanyName = companyName,
            Ruc = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Phone = phone,
            Email = email,
            Address = address,
            LogoUrl = logoUrl,
            LogoPending = string.IsNullOrWhiteSpace(logoUrl),
            FooterText = footerText,
            HrManagerName = hrManagerName,
        };
    }
}

public sealed class PayrollPeriodObservation
{
    public string? Note { get; set; }
    public DateTime? CutoffDate { get; set; }
    public string? Raw { get; set; }
}

public sealed class ReportBrandingDto
{
    public string LegalName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public bool LogoPending { get; set; }
    public string FooterText { get; set; } = string.Empty;
    public string HrManagerName { get; set; } = string.Empty;
}

public enum PayrollType
{
    EmpleadoNomina,
    PasanteAyuda,
    ServicioProfesional,
}
