using System.Data;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Creditos;

public static class MicrofinanceCoreSupport
{
    public static void EnsureSchema(SqlConnection connection, SqlTransaction? transaction = null)
    {
        const string sql = """
            IF SCHEMA_ID(N'creditos') IS NULL EXEC(N'CREATE SCHEMA creditos');
            IF SCHEMA_ID(N'configuracion') IS NULL EXEC(N'CREATE SCHEMA configuracion');
            IF SCHEMA_ID(N'cumplimiento') IS NULL EXEC(N'CREATE SCHEMA cumplimiento');
            IF SCHEMA_ID(N'contabilidad') IS NULL EXEC(N'CREATE SCHEMA contabilidad');

            IF OBJECT_ID(N'creditos.producto_crediticio', N'U') IS NULL
            BEGIN
                CREATE TABLE creditos.producto_crediticio
                (
                    id_producto_crediticio BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_producto_crediticio PRIMARY KEY,
                    codigo_producto NVARCHAR(40) NOT NULL CONSTRAINT UQ_producto_crediticio_codigo UNIQUE,
                    nombre_producto NVARCHAR(150) NOT NULL,
                    descripcion NVARCHAR(500) NULL,
                    moneda NVARCHAR(10) NOT NULL CONSTRAINT DF_producto_crediticio_moneda DEFAULT N'NIO',
                    monto_minimo DECIMAL(18,2) NOT NULL CONSTRAINT DF_producto_crediticio_min DEFAULT 0,
                    monto_maximo DECIMAL(18,2) NOT NULL CONSTRAINT DF_producto_crediticio_max DEFAULT 0,
                    plazo_minimo_meses INT NOT NULL CONSTRAINT DF_producto_crediticio_plazo_min DEFAULT 1,
                    plazo_maximo_meses INT NOT NULL CONSTRAINT DF_producto_crediticio_plazo_max DEFAULT 12,
                    tasa_interes_anual DECIMAL(18,6) NOT NULL CONSTRAINT DF_producto_crediticio_tasa DEFAULT 0,
                    tasa_mora_anual DECIMAL(18,6) NOT NULL CONSTRAINT DF_producto_crediticio_mora DEFAULT 0,
                    tasa_comision DECIMAL(18,6) NOT NULL CONSTRAINT DF_producto_crediticio_comision DEFAULT 0,
                    tasa_deslizamiento_anual DECIMAL(18,6) NOT NULL CONSTRAINT DF_producto_crediticio_desliz DEFAULT 0,
                    frecuencia_pago NVARCHAR(30) NOT NULL CONSTRAINT DF_producto_crediticio_freq DEFAULT N'MENSUAL',
                    tipo_cuota NVARCHAR(30) NOT NULL CONSTRAINT DF_producto_crediticio_cuota DEFAULT N'NIVELADA',
                    requiere_garantia BIT NOT NULL CONSTRAINT DF_producto_crediticio_garantia DEFAULT 0,
                    requiere_fiador BIT NOT NULL CONSTRAINT DF_producto_crediticio_fiador DEFAULT 0,
                    requiere_visita BIT NOT NULL CONSTRAINT DF_producto_crediticio_visita DEFAULT 1,
                    requiere_comite_desde DECIMAL(18,2) NOT NULL CONSTRAINT DF_producto_crediticio_comite DEFAULT 0,
                    reglas_json NVARCHAR(MAX) NULL,
                    activo BIT NOT NULL CONSTRAINT DF_producto_crediticio_activo DEFAULT 1,
                    usuario_registro NVARCHAR(120) NOT NULL CONSTRAINT DF_producto_crediticio_usuario DEFAULT N'sistema',
                    fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_producto_crediticio_fecha DEFAULT SYSDATETIME(),
                    fecha_modificacion DATETIME2 NULL
                );
            END;

            IF OBJECT_ID(N'configuracion.catalogo_operativo', N'U') IS NULL
            BEGIN
                CREATE TABLE configuracion.catalogo_operativo
                (
                    id_catalogo_operativo BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_catalogo_operativo PRIMARY KEY,
                    tipo_catalogo NVARCHAR(80) NOT NULL,
                    codigo NVARCHAR(80) NOT NULL,
                    nombre NVARCHAR(200) NOT NULL,
                    descripcion NVARCHAR(500) NULL,
                    valor_texto NVARCHAR(500) NULL,
                    valor_decimal DECIMAL(18,6) NULL,
                    orden INT NOT NULL CONSTRAINT DF_catalogo_operativo_orden DEFAULT 0,
                    activo BIT NOT NULL CONSTRAINT DF_catalogo_operativo_activo DEFAULT 1,
                    usuario_registro NVARCHAR(120) NOT NULL CONSTRAINT DF_catalogo_operativo_usuario DEFAULT N'sistema',
                    fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_catalogo_operativo_fecha DEFAULT SYSDATETIME(),
                    CONSTRAINT UQ_catalogo_operativo UNIQUE(tipo_catalogo, codigo)
                );
            END;

            IF OBJECT_ID(N'contabilidad.configuracion_asiento_transaccion', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM contabilidad.tipo_transaccion_contable WHERE codigo_tipo_transaccion_contable = N'CREDITO_PAGO')
                    INSERT INTO contabilidad.tipo_transaccion_contable(codigo_tipo_transaccion_contable,nombre_transaccion,origen_modulo,tipo_asiento_predeterminado,requiere_documento_origen,permite_reversion,activo,usuario_creacion,fecha_creacion)
                    VALUES(N'CREDITO_PAGO', N'Cobro de cuota de credito', N'CAJA', N'PAGO_CREDITO', 1, 1, 1, N'sistema', SYSDATETIME());
                IF NOT EXISTS (SELECT 1 FROM contabilidad.tipo_transaccion_contable WHERE codigo_tipo_transaccion_contable = N'CREDITO_DESEMBOLSO')
                    INSERT INTO contabilidad.tipo_transaccion_contable(codigo_tipo_transaccion_contable,nombre_transaccion,origen_modulo,tipo_asiento_predeterminado,requiere_documento_origen,permite_reversion,activo,usuario_creacion,fecha_creacion)
                    VALUES(N'CREDITO_DESEMBOLSO', N'Desembolso de credito por caja', N'CAJA', N'DESEMBOLSO_CREDITO', 1, 1, 1, N'sistema', SYSDATETIME());
                IF NOT EXISTS (SELECT 1 FROM contabilidad.tipo_transaccion_contable WHERE codigo_tipo_transaccion_contable = N'CREDITO_PAGO_ANULACION')
                    INSERT INTO contabilidad.tipo_transaccion_contable(codigo_tipo_transaccion_contable,nombre_transaccion,origen_modulo,tipo_asiento_predeterminado,requiere_documento_origen,permite_reversion,activo,usuario_creacion,fecha_creacion)
                    VALUES(N'CREDITO_PAGO_ANULACION', N'Anulacion/reversa de pago de credito', N'CAJA', N'ANULACION_PAGO_CREDITO', 1, 1, 1, N'sistema', SYSDATETIME());
                IF NOT EXISTS (SELECT 1 FROM contabilidad.tipo_transaccion_contable WHERE codigo_tipo_transaccion_contable = N'CAJA_APERTURA')
                    INSERT INTO contabilidad.tipo_transaccion_contable(codigo_tipo_transaccion_contable,nombre_transaccion,origen_modulo,tipo_asiento_predeterminado,requiere_documento_origen,permite_reversion,activo,usuario_creacion,fecha_creacion)
                    VALUES(N'CAJA_APERTURA', N'Apertura de caja', N'CAJA', N'APERTURA_CAJA', 1, 0, 1, N'sistema', SYSDATETIME());
                IF NOT EXISTS (SELECT 1 FROM contabilidad.tipo_transaccion_contable WHERE codigo_tipo_transaccion_contable = N'CAJA_ARQUEO_DIFERENCIA')
                    INSERT INTO contabilidad.tipo_transaccion_contable(codigo_tipo_transaccion_contable,nombre_transaccion,origen_modulo,tipo_asiento_predeterminado,requiere_documento_origen,permite_reversion,activo,usuario_creacion,fecha_creacion)
                    VALUES(N'CAJA_ARQUEO_DIFERENCIA', N'Sobrante o faltante de arqueo', N'CAJA', N'DIFERENCIA_ARQUEO', 1, 1, 1, N'sistema', SYSDATETIME());
            END;

            EXEC sys.sp_executesql N'
            MERGE creditos.producto_crediticio AS target
            USING (VALUES
                (N''MICROCREDITO'', N''Microcredito individual'', N''Credito para comercio, servicio o produccion de microempresa.'', N''NIO'', 3000.00, 80000.00, 3, 18, 36.000000, 36.000000, 2.000000, 0.000000, N''MENSUAL'', N''NIVELADA'', 0, 0, 1, 30000.00),
                (N''CAPITAL_TRABAJO'', N''Capital de trabajo'', N''Financia inventario, materia prima y operacion diaria.'', N''NIO'', 5000.00, 150000.00, 3, 24, 34.000000, 36.000000, 2.000000, 0.000000, N''MENSUAL'', N''NIVELADA'', 1, 0, 1, 50000.00),
                (N''CONSUMO_PERSONAL'', N''Credito personal'', N''Credito personal con validacion de capacidad y referencias.'', N''NIO'', 3000.00, 60000.00, 3, 18, 69.000000, 36.000000, 18.000000, 0.000000, N''MENSUAL'', N''NIVELADA'', 0, 1, 1, 25000.00),
                (N''HIPOTECARIO'', N''Credito hipotecario'', N''Credito respaldado por garantia hipotecaria.'', N''NIO'', 50000.00, 1500000.00, 12, 180, 24.000000, 36.000000, 10.000000, 0.000000, N''MENSUAL'', N''NIVELADA'', 1, 0, 1, 250000.00),
                (N''MEJORA_VIVIENDA'', N''Mejora de vivienda'', N''Credito para reparacion, ampliacion o mejora habitacional.'', N''NIO'', 5000.00, 120000.00, 6, 36, 30.000000, 36.000000, 1.500000, 0.000000, N''MENSUAL'', N''NIVELADA'', 1, 0, 1, 60000.00),
                (N''GRUPO_SOLIDARIO'', N''Grupo solidario'', N''Credito con responsabilidad solidaria entre miembros del grupo.'', N''NIO'', 2000.00, 50000.00, 3, 12, 38.000000, 36.000000, 2.000000, 0.000000, N''MENSUAL'', N''NIVELADA'', 0, 0, 1, 20000.00),
                (N''PRESTAMO_USD_VARIABLE'', N''Prestamo USD tasa variable'', N''Credito en dolares con historial de tasas y estado de cuenta.'', N''USD'', 100.00, 10000.00, 6, 60, 23.000000, 24.000000, 1.000000, 0.000000, N''MENSUAL'', N''NIVELADA'', 1, 0, 1, 3000.00)
            ) AS source(codigo_producto,nombre_producto,descripcion,moneda,monto_minimo,monto_maximo,plazo_minimo_meses,plazo_maximo_meses,tasa_interes_anual,tasa_mora_anual,tasa_comision,tasa_deslizamiento_anual,frecuencia_pago,tipo_cuota,requiere_garantia,requiere_fiador,requiere_visita,requiere_comite_desde)
            ON target.codigo_producto = source.codigo_producto
            WHEN MATCHED AND target.usuario_registro = N''sistema'' THEN UPDATE SET
                nombre_producto = source.nombre_producto,
                descripcion = source.descripcion,
                moneda = source.moneda,
                monto_minimo = source.monto_minimo,
                monto_maximo = source.monto_maximo,
                plazo_minimo_meses = source.plazo_minimo_meses,
                plazo_maximo_meses = source.plazo_maximo_meses,
                tasa_interes_anual = source.tasa_interes_anual,
                tasa_mora_anual = source.tasa_mora_anual,
                tasa_comision = source.tasa_comision,
                tasa_deslizamiento_anual = source.tasa_deslizamiento_anual,
                frecuencia_pago = source.frecuencia_pago,
                tipo_cuota = source.tipo_cuota,
                requiere_garantia = source.requiere_garantia,
                requiere_fiador = source.requiere_fiador,
                requiere_visita = source.requiere_visita,
                requiere_comite_desde = source.requiere_comite_desde,
                activo = 1,
                fecha_modificacion = SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT(codigo_producto,nombre_producto,descripcion,moneda,monto_minimo,monto_maximo,plazo_minimo_meses,plazo_maximo_meses,tasa_interes_anual,tasa_mora_anual,tasa_comision,tasa_deslizamiento_anual,frecuencia_pago,tipo_cuota,requiere_garantia,requiere_fiador,requiere_visita,requiere_comite_desde,activo)
                VALUES(source.codigo_producto,source.nombre_producto,source.descripcion,source.moneda,source.monto_minimo,source.monto_maximo,source.plazo_minimo_meses,source.plazo_maximo_meses,source.tasa_interes_anual,source.tasa_mora_anual,source.tasa_comision,source.tasa_deslizamiento_anual,source.frecuencia_pago,source.tipo_cuota,source.requiere_garantia,source.requiere_fiador,source.requiere_visita,source.requiere_comite_desde,1);';

            EXEC sys.sp_executesql N'
            MERGE configuracion.catalogo_operativo AS target
            USING (VALUES
                (N''ACTIVIDAD_ECONOMICA'', N''PULPERIA'', N''Pulperia'', N''Comercio minorista de alimentos y articulos basicos'', 10),
                (N''ACTIVIDAD_ECONOMICA'', N''VENTA_ROPA'', N''Venta de ropa'', N''Comercio minorista textil'', 20),
                (N''ACTIVIDAD_ECONOMICA'', N''SERVICIO_TRANSPORTE'', N''Servicio de transporte'', N''Taxi, moto, acarreo o transporte local'', 30),
                (N''ACTIVIDAD_ECONOMICA'', N''AGRICULTURA'', N''Agricultura'', N''Produccion agricola familiar o comercial'', 40),
                (N''ACTIVIDAD_ECONOMICA'', N''RIESGO_ALTO_EFECTIVO'', N''Negocio intensivo en efectivo'', N''Actividad con mayor exposicion AML; requiere DDC reforzada'', 90),
                (N''DEPARTAMENTO'', N''MANAGUA'', N''Managua'', N''Departamento'', 10),
                (N''DEPARTAMENTO'', N''MASAYA'', N''Masaya'', N''Departamento'', 20),
                (N''DEPARTAMENTO'', N''GRANADA'', N''Granada'', N''Departamento'', 30),
                (N''DEPARTAMENTO'', N''LEON'', N''Leon'', N''Departamento'', 40),
                (N''MUNICIPIO'', N''MANAGUA'', N''Managua'', N''Municipio Managua'', 10),
                (N''MUNICIPIO'', N''MASAYA'', N''Masaya'', N''Municipio Masaya'', 20),
                (N''TIPO_GARANTIA'', N''FIDUCIARIA'', N''Fiduciaria'', N''Fiador o codeudor solidario'', 10),
                (N''TIPO_GARANTIA'', N''PRENDARIA'', N''Prendaria'', N''Bien mueble en garantia'', 20),
                (N''TIPO_GARANTIA'', N''HIPOTECARIA'', N''Hipotecaria'', N''Bien inmueble en garantia'', 30),
                (N''TIPO_GARANTIA'', N''NINGUNA'', N''Ninguna'', N''Sin garantia real'', 99),
                (N''ESTADO_ADMINISTRATIVO'', N''VI'', N''Vigente'', N''Credito activo al dia'', 10),
                (N''ESTADO_ADMINISTRATIVO'', N''VE'', N''Vencido'', N''Credito con mora operativa'', 20),
                (N''ESTADO_ADMINISTRATIVO'', N''CJ'', N''Cobro judicial'', N''Credito en recuperacion legal'', 30),
                (N''ESTADO_ADMINISTRATIVO'', N''CA'', N''Cancelado'', N''Credito cancelado'', 40),
                (N''ICC_PRIM'', N''CARTERA_MICROCREDITO'', N''Cartera microcredito'', N''Diccionario base para PRIM/ICC'', 10),
                (N''ICC_PRIM'', N''PROVISION_CARTERA'', N''Provision cartera'', N''Diccionario base para PRIM/ICC'', 20),
                (N''OPERACION_CAJA'', N''COBRO_CREDITO'', N''Cobro de credito'', N''Entrada por pago de cuota'', 10),
                (N''OPERACION_CAJA'', N''DESEMBOLSO_CREDITO'', N''Desembolso de credito'', N''Salida por credito aprobado'', 20),
                (N''OPERACION_CAJA'', N''ANULACION'', N''Anulacion/reversa'', N''Operacion controlada con auditoria'', 30),
                (N''OPERACION_CAJA'', N''ARQUEO'', N''Arqueo'', N''Conteo fisico y diferencia'', 40)
            ) AS source(tipo_catalogo,codigo,nombre,descripcion,orden)
            ON target.tipo_catalogo = source.tipo_catalogo AND target.codigo = source.codigo
            WHEN MATCHED THEN UPDATE SET nombre = source.nombre, descripcion = source.descripcion, orden = source.orden, activo = 1
            WHEN NOT MATCHED THEN
                INSERT(tipo_catalogo,codigo,nombre,descripcion,orden,activo)
                VALUES(source.tipo_catalogo,source.codigo,source.nombre,source.descripcion,source.orden,1);';

            IF OBJECT_ID(N'cumplimiento.matriz_alerta_temprana', N'U') IS NOT NULL
            BEGIN
                MERGE cumplimiento.matriz_alerta_temprana AS target
                USING (VALUES
                    (N'UAF001', N'Cliente PEP o relacionado', N'Cliente declara o coincide como PEP, familiar o asociado cercano.', N'ALTO'),
                    (N'UAF002', N'Origen de fondos no documentado', N'No existe soporte razonable del origen de fondos o ingresos declarados.', N'ALTO'),
                    (N'UAF003', N'Actividad economica sensible', N'Actividad intensiva en efectivo o de mayor exposicion AML.', N'MEDIO'),
                    (N'UAF004', N'Expediente incompleto', N'Faltan documentos minimos de identificacion, referencias, visita o garantia.', N'MEDIO'),
                    (N'UAF005', N'Endeudamiento elevado', N'El nivel de endeudamiento excede los parametros internos definidos.', N'ALTO'),
                    (N'UAF006', N'Referencias inconsistentes', N'Referencias personales, comerciales o financieras no verificadas o inconsistentes.', N'MEDIO'),
                    (N'UAF007', N'Pago anticipado inusual', N'Pago anticipado, de tercero o en moneda distinta sin justificacion.', N'MEDIO'),
                    (N'UAF008', N'Operacion fraccionada o recurrente inusual', N'Operacion repetitiva o fraccionada que requiere revision.', N'ALTO')
                ) AS source(codigo_alerta,nombre_alerta,descripcion_alerta,nivel_riesgo)
                ON target.codigo_alerta = source.codigo_alerta
                WHEN MATCHED THEN UPDATE SET nombre_alerta = source.nombre_alerta, descripcion_alerta = source.descripcion_alerta, nivel_riesgo = source.nivel_riesgo, activa = 1
                WHEN NOT MATCHED THEN
                    INSERT(codigo_alerta,nombre_alerta,descripcion_alerta,nivel_riesgo,activa,fecha_creacion)
                    VALUES(source.codigo_alerta,source.nombre_alerta,source.descripcion_alerta,source.nivel_riesgo,1,SYSDATETIME());
            END;

            IF OBJECT_ID(N'cumplimiento.parametro_alerta_aml', N'U') IS NOT NULL
            BEGIN
                MERGE cumplimiento.parametro_alerta_aml AS target
                USING (VALUES
                    (N'DDC_ENDEUDAMIENTO_MEDIO_PCT', N'Porcentaje de cuota sobre ingreso para alerta media', 35.000000, NULL, N'PORC'),
                    (N'DDC_ENDEUDAMIENTO_ALTO_PCT', N'Porcentaje de cuota sobre ingreso para alerta alta', 50.000000, NULL, N'PORC'),
                    (N'DDC_MONTO_TERCERO_REVISION', N'Monto recibido por tercero que dispara revision', 1000.000000, NULL, N'USD'),
                    (N'DDC_EXPEDIENTE_MINIMO', N'Checklist minimo de expediente para credito', NULL, N'IDENTIFICACION,REFERENCIAS,VISITA,CAPACIDAD,SIN_RIESGO,CONAMI', NULL)
                ) AS source(codigo_parametro,descripcion,valor_numerico,valor_texto,moneda)
                ON target.codigo_parametro = source.codigo_parametro
                WHEN MATCHED THEN UPDATE SET descripcion = source.descripcion, valor_numerico = source.valor_numerico, valor_texto = source.valor_texto, moneda = source.moneda, activo = 1
                WHEN NOT MATCHED THEN
                    INSERT(codigo_parametro,descripcion,valor_numerico,valor_texto,moneda,fecha_vigencia_desde,activo,fecha_registro)
                    VALUES(source.codigo_parametro,source.descripcion,source.valor_numerico,source.valor_texto,source.moneda,CONVERT(date, SYSDATETIME()),1,SYSDATETIME());
            END;

            IF OBJECT_ID(N'nomina.concepto_nomina', N'U') IS NOT NULL
               AND OBJECT_ID(N'nomina.tipo_concepto_nomina', N'U') IS NOT NULL
            BEGIN
                DECLARE @tipo_ingreso BIGINT = (SELECT TOP (1) id_tipo_concepto_nomina FROM nomina.tipo_concepto_nomina WHERE codigo_tipo_concepto = N'INGRESO');
                DECLARE @tipo_deduccion BIGINT = (SELECT TOP (1) id_tipo_concepto_nomina FROM nomina.tipo_concepto_nomina WHERE codigo_tipo_concepto = N'DEDUCCION');

                IF @tipo_ingreso IS NOT NULL AND NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'COMISION_COLOCACION')
                    INSERT INTO nomina.concepto_nomina(id_tipo_concepto_nomina,codigo_concepto,nombre_concepto,afecta_ingresos_gravables,afecta_base_inss,afecta_neto,orden_visual,activo)
                    VALUES(@tipo_ingreso, N'COMISION_COLOCACION', N'Comision por colocacion de creditos', 1, 1, 1, 20, 1);

                IF @tipo_deduccion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'EMBARGO_JUDICIAL')
                    INSERT INTO nomina.concepto_nomina(id_tipo_concepto_nomina,codigo_concepto,nombre_concepto,afecta_ingresos_gravables,afecta_base_inss,afecta_neto,orden_visual,activo)
                    VALUES(@tipo_deduccion, N'EMBARGO_JUDICIAL', N'Embargo judicial', 0, 0, 1, 60, 1);

                IF @tipo_deduccion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM nomina.concepto_nomina WHERE codigo_concepto = N'PENSION_ALIMENTICIA')
                    INSERT INTO nomina.concepto_nomina(id_tipo_concepto_nomina,codigo_concepto,nombre_concepto,afecta_ingresos_gravables,afecta_base_inss,afecta_neto,orden_visual,activo)
                    VALUES(@tipo_deduccion, N'PENSION_ALIMENTICIA', N'Pension alimenticia', 0, 0, 1, 61, 1);
            END;
        """;

        using var command = new SqlCommand(sql, connection, transaction);
        command.CommandTimeout = 90;
        command.ExecuteNonQuery();
    }

    public static IReadOnlyList<CreditProductDto> LoadProducts(SqlConnection connection)
    {
        EnsureSchema(connection);
        using var command = new SqlCommand(
            """
            SELECT codigo_producto,nombre_producto,descripcion,moneda,monto_minimo,monto_maximo,
                   plazo_minimo_meses,plazo_maximo_meses,tasa_interes_anual,tasa_mora_anual,
                   tasa_comision,tasa_deslizamiento_anual,frecuencia_pago,tipo_cuota,
                   requiere_garantia,requiere_fiador,requiere_visita,requiere_comite_desde
            FROM creditos.producto_crediticio
            WHERE activo = 1
            ORDER BY nombre_producto;
            """,
            connection);

        using var reader = command.ExecuteReader();
        var products = new List<CreditProductDto>();
        while (reader.Read())
        {
            products.Add(MapProduct(reader));
        }

        return products;
    }

    public static CreditProductDto? FindProduct(SqlConnection connection, string? codeOrName, SqlTransaction? transaction = null)
    {
        EnsureSchema(connection, transaction);
        var normalized = NormalizeProductCode(codeOrName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "CONSUMO_PERSONAL";
        }

        using var command = new SqlCommand(
            """
            SELECT TOP (1)
                   codigo_producto,nombre_producto,descripcion,moneda,monto_minimo,monto_maximo,
                   plazo_minimo_meses,plazo_maximo_meses,tasa_interes_anual,tasa_mora_anual,
                   tasa_comision,tasa_deslizamiento_anual,frecuencia_pago,tipo_cuota,
                   requiere_garantia,requiere_fiador,requiere_visita,requiere_comite_desde
            FROM creditos.producto_crediticio
            WHERE activo = 1
              AND (
                    codigo_producto = @codigo
                    OR REPLACE(UPPER(nombre_producto), N' ', N'_') = @codigo
                  )
            ORDER BY codigo_producto;
            """,
            connection,
            transaction);
        command.Parameters.Add("@codigo", SqlDbType.NVarChar, 40).Value = normalized;

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapProduct(reader) : null;
    }

    public static CreditProductDto UpsertProduct(SqlConnection connection, CreditProductDto model, string user, SqlTransaction? transaction = null)
    {
        EnsureSchema(connection, transaction);
        var code = NormalizeProductCode(model.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("El codigo del tipo de credito es obligatorio.");
        }

        using (var command = new SqlCommand(
            """
            MERGE creditos.producto_crediticio AS target
            USING (SELECT @codigo_producto AS codigo_producto) AS source
            ON target.codigo_producto = source.codigo_producto
            WHEN MATCHED THEN UPDATE SET
                nombre_producto = @nombre_producto,
                descripcion = @descripcion,
                moneda = @moneda,
                monto_minimo = @monto_minimo,
                monto_maximo = @monto_maximo,
                plazo_minimo_meses = @plazo_minimo_meses,
                plazo_maximo_meses = @plazo_maximo_meses,
                tasa_interes_anual = @tasa_interes_anual,
                tasa_mora_anual = @tasa_mora_anual,
                tasa_comision = @tasa_comision,
                tasa_deslizamiento_anual = @tasa_deslizamiento_anual,
                frecuencia_pago = @frecuencia_pago,
                tipo_cuota = @tipo_cuota,
                requiere_garantia = @requiere_garantia,
                requiere_fiador = @requiere_fiador,
                requiere_visita = @requiere_visita,
                requiere_comite_desde = @requiere_comite_desde,
                activo = @activo,
                usuario_registro = @usuario_registro,
                fecha_modificacion = SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT
                (
                    codigo_producto,nombre_producto,descripcion,moneda,monto_minimo,monto_maximo,
                    plazo_minimo_meses,plazo_maximo_meses,tasa_interes_anual,tasa_mora_anual,
                    tasa_comision,tasa_deslizamiento_anual,frecuencia_pago,tipo_cuota,
                    requiere_garantia,requiere_fiador,requiere_visita,requiere_comite_desde,
                    activo,usuario_registro
                )
                VALUES
                (
                    @codigo_producto,@nombre_producto,@descripcion,@moneda,@monto_minimo,@monto_maximo,
                    @plazo_minimo_meses,@plazo_maximo_meses,@tasa_interes_anual,@tasa_mora_anual,
                    @tasa_comision,@tasa_deslizamiento_anual,@frecuencia_pago,@tipo_cuota,
                    @requiere_garantia,@requiere_fiador,@requiere_visita,@requiere_comite_desde,
                    @activo,@usuario_registro
                );
            """,
            connection,
            transaction))
        {
            command.Parameters.Add("@codigo_producto", SqlDbType.NVarChar, 40).Value = code;
            command.Parameters.Add("@nombre_producto", SqlDbType.NVarChar, 150).Value = string.IsNullOrWhiteSpace(model.Name) ? code.Replace("_", " ") : model.Name.Trim();
            command.Parameters.Add("@descripcion", SqlDbType.NVarChar, 500).Value = string.IsNullOrWhiteSpace(model.Description) ? DBNull.Value : model.Description.Trim();
            command.Parameters.Add("@moneda", SqlDbType.NVarChar, 10).Value = string.IsNullOrWhiteSpace(model.Currency) ? "NIO" : model.Currency.Trim().ToUpperInvariant();
            command.Parameters.Add("@monto_minimo", SqlDbType.Decimal).Value = Math.Max(0, model.MinAmount);
            command.Parameters.Add("@monto_maximo", SqlDbType.Decimal).Value = Math.Max(0, model.MaxAmount);
            command.Parameters.Add("@plazo_minimo_meses", SqlDbType.Int).Value = Math.Max(1, model.MinTermMonths);
            command.Parameters.Add("@plazo_maximo_meses", SqlDbType.Int).Value = Math.Max(Math.Max(1, model.MinTermMonths), model.MaxTermMonths);
            command.Parameters.Add("@tasa_interes_anual", SqlDbType.Decimal).Value = Math.Round(Math.Max(0, model.AnnualRate), 6);
            command.Parameters.Add("@tasa_mora_anual", SqlDbType.Decimal).Value = Math.Round(Math.Max(0, model.MoraRate), 6);
            command.Parameters.Add("@tasa_comision", SqlDbType.Decimal).Value = Math.Round(Math.Max(0, model.CommissionRate), 6);
            command.Parameters.Add("@tasa_deslizamiento_anual", SqlDbType.Decimal).Value = Math.Round(Math.Max(0, model.SlidingRate), 6);
            command.Parameters.Add("@frecuencia_pago", SqlDbType.NVarChar, 30).Value = string.IsNullOrWhiteSpace(model.Frequency) ? "MENSUAL" : model.Frequency.Trim().ToUpperInvariant();
            command.Parameters.Add("@tipo_cuota", SqlDbType.NVarChar, 30).Value = string.IsNullOrWhiteSpace(model.InstallmentType) ? "NIVELADA" : model.InstallmentType.Trim().ToUpperInvariant();
            command.Parameters.Add("@requiere_garantia", SqlDbType.Bit).Value = model.RequiresGuarantee;
            command.Parameters.Add("@requiere_fiador", SqlDbType.Bit).Value = model.RequiresGuarantor;
            command.Parameters.Add("@requiere_visita", SqlDbType.Bit).Value = model.RequiresVisit;
            command.Parameters.Add("@requiere_comite_desde", SqlDbType.Decimal).Value = Math.Max(0, model.CommitteeFrom);
            command.Parameters.Add("@activo", SqlDbType.Bit).Value = model.Active;
            command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 120).Value = string.IsNullOrWhiteSpace(user) ? "sistema.local" : user.Trim();
            command.ExecuteNonQuery();
        }

        return FindProduct(connection, code, transaction) ?? throw new InvalidOperationException("No se pudo cargar el tipo de credito guardado.");
    }

    public static string NormalizeProductCode(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join("_", text.Split([' ', '-', '/'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static CreditProductDto MapProduct(SqlDataReader reader)
    {
        return new CreditProductDto
            {
            Code = ReadString(reader, "codigo_producto"),
            Name = ReadString(reader, "nombre_producto"),
            Description = ReadString(reader, "descripcion"),
            Currency = ReadString(reader, "moneda", "NIO"),
            MinAmount = ReadDecimal(reader, "monto_minimo"),
            MaxAmount = ReadDecimal(reader, "monto_maximo"),
            MinTermMonths = ReadInt32(reader, "plazo_minimo_meses"),
            MaxTermMonths = ReadInt32(reader, "plazo_maximo_meses"),
            AnnualRate = ReadDecimal(reader, "tasa_interes_anual"),
            MoraRate = ReadDecimal(reader, "tasa_mora_anual"),
            CommissionRate = ReadDecimal(reader, "tasa_comision"),
            SlidingRate = ReadDecimal(reader, "tasa_deslizamiento_anual"),
            Frequency = ReadString(reader, "frecuencia_pago", "MENSUAL"),
            InstallmentType = ReadString(reader, "tipo_cuota", "NIVELADA"),
            RequiresGuarantee = ReadBool(reader, "requiere_garantia"),
            RequiresGuarantor = ReadBool(reader, "requiere_fiador"),
            RequiresVisit = ReadBool(reader, "requiere_visita"),
            CommitteeFrom = ReadDecimal(reader, "requiere_comite_desde"),
            Active = true,
        };
    }

    public static IReadOnlyList<object> LoadCatalog(SqlConnection connection, string catalogType)
    {
        EnsureSchema(connection);
        using var command = new SqlCommand(
            """
            SELECT tipo_catalogo,codigo,nombre,descripcion,valor_texto,valor_decimal,orden
            FROM configuracion.catalogo_operativo
            WHERE tipo_catalogo = @tipo AND activo = 1
            ORDER BY orden,nombre;
            """,
            connection);
        command.Parameters.Add("@tipo", SqlDbType.NVarChar, 80).Value = catalogType.Trim().ToUpperInvariant();

        using var reader = command.ExecuteReader();
        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                type = ReadString(reader, "tipo_catalogo"),
                code = ReadString(reader, "codigo"),
                name = ReadString(reader, "nombre"),
                description = ReadString(reader, "descripcion"),
                textValue = ReadString(reader, "valor_texto"),
                decimalValue = ReadDecimalNullable(reader, "valor_decimal"),
                order = ReadInt32(reader, "orden"),
            });
        }

        return items;
    }

    public static IReadOnlyList<object> LoadUafAlerts(SqlConnection connection)
    {
        EnsureSchema(connection);
        using var command = new SqlCommand(
            """
            SELECT codigo_alerta,nombre_alerta,descripcion_alerta,nivel_riesgo
            FROM cumplimiento.matriz_alerta_temprana
            WHERE activa = 1 AND codigo_alerta LIKE N'UAF%'
            ORDER BY codigo_alerta;
            """,
            connection);

        using var reader = command.ExecuteReader();
        var items = new List<object>();
        while (reader.Read())
        {
            items.Add(new
            {
                code = ReadString(reader, "codigo_alerta"),
                name = ReadString(reader, "nombre_alerta"),
                description = ReadString(reader, "descripcion_alerta"),
                risk = ReadString(reader, "nivel_riesgo"),
            });
        }

        return items;
    }

    private static string ReadString(SqlDataReader reader, string name, string fallback = "")
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? fallback : Convert.ToString(reader.GetValue(ordinal)) ?? fallback;
    }

    private static int ReadInt32(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal ReadDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimalNullable(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool ReadBool(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
    }
}

public sealed class CreditProductDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "NIO";
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermMonths { get; set; }
    public int MaxTermMonths { get; set; }
    public decimal AnnualRate { get; set; }
    public decimal MoraRate { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal SlidingRate { get; set; }
    public string Frequency { get; set; } = "MENSUAL";
    public string InstallmentType { get; set; } = "NIVELADA";
    public bool RequiresGuarantee { get; set; }
    public bool RequiresGuarantor { get; set; }
    public bool RequiresVisit { get; set; } = true;
    public decimal CommitteeFrom { get; set; }
    public bool Active { get; set; } = true;
}
