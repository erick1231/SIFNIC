using System.Data;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Creditos;

public static class RegulatoryPortfolioSupport
{
    public static void EnsureSchema(SqlConnection connection)
    {
        const string sql = """
            IF SCHEMA_ID(N'regulatorio') IS NULL EXEC(N'CREATE SCHEMA regulatorio');
            IF SCHEMA_ID(N'reportes') IS NULL EXEC(N'CREATE SCHEMA reportes');

            IF OBJECT_ID(N'regulatorio.version_regla', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.version_regla
                (
                    id_version_regla INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_version_regla PRIMARY KEY,
                    codigo_version NVARCHAR(50) NOT NULL CONSTRAINT UQ_regulatorio_version_codigo UNIQUE,
                    descripcion NVARCHAR(250) NOT NULL,
                    fecha_vigencia_desde DATE NOT NULL,
                    fecha_vigencia_hasta DATE NULL,
                    es_activa BIT NOT NULL CONSTRAINT DF_regulatorio_version_activa DEFAULT (1),
                    creada_por NVARCHAR(100) NOT NULL CONSTRAINT DF_regulatorio_version_creada DEFAULT (SUSER_SNAME()),
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_regulatorio_version_fecha DEFAULT (SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'regulatorio.regla_clasificacion', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.regla_clasificacion
                (
                    id_regla_clasificacion BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_regla_clasificacion PRIMARY KEY,
                    id_version_regla INT NOT NULL,
                    tipo_agrupacion TINYINT NULL,
                    dias_mora_desde INT NOT NULL,
                    dias_mora_hasta INT NULL,
                    categoria NVARCHAR(5) NOT NULL,
                    clasificacion_credito NVARCHAR(20) NOT NULL,
                    reconocimiento_ingreso BIT NOT NULL CONSTRAINT DF_regulatorio_regla_clasificacion_ingreso DEFAULT (1),
                    sanea_intereses BIT NOT NULL CONSTRAINT DF_regulatorio_regla_clasificacion_sanea DEFAULT (0),
                    pasa_a_vencido BIT NOT NULL CONSTRAINT DF_regulatorio_regla_clasificacion_ve DEFAULT (0),
                    pasa_a_cobro_judicial BIT NOT NULL CONSTRAINT DF_regulatorio_regla_clasificacion_cj DEFAULT (0),
                    observacion NVARCHAR(250) NULL
                );
            END;

            IF OBJECT_ID(N'regulatorio.regla_provision', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.regla_provision
                (
                    id_regla_provision BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_regla_provision PRIMARY KEY,
                    id_version_regla INT NOT NULL,
                    tipo_agrupacion TINYINT NULL,
                    categoria NVARCHAR(5) NOT NULL,
                    estado_operativo NVARCHAR(5) NULL,
                    porcentaje_provision DECIMAL(12,6) NOT NULL,
                    permite_reversion BIT NOT NULL CONSTRAINT DF_regulatorio_regla_provision_reversion DEFAULT (1),
                    observacion NVARCHAR(250) NULL
                );
            END;

            IF OBJECT_ID(N'regulatorio.lote_cierre', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.lote_cierre
                (
                    id_lote_cierre BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_lote_cierre PRIMARY KEY,
                    fecha_cierre DATE NOT NULL,
                    tipo_cierre NVARCHAR(20) NOT NULL CONSTRAINT DF_regulatorio_lote_tipo DEFAULT (N'4_MENSUAL'),
                    id_version_regla INT NOT NULL,
                    estado_lote NVARCHAR(20) NOT NULL CONSTRAINT DF_regulatorio_lote_estado DEFAULT (N'PENDIENTE'),
                    total_creditos INT NOT NULL CONSTRAINT DF_regulatorio_lote_creditos DEFAULT (0),
                    total_saldo DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_lote_saldo DEFAULT (0),
                    total_provision DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_lote_provision DEFAULT (0),
                    usuario_ejecucion NVARCHAR(100) NOT NULL CONSTRAINT DF_regulatorio_lote_usuario DEFAULT (SUSER_SNAME()),
                    fecha_inicio DATETIME2 NOT NULL CONSTRAINT DF_regulatorio_lote_inicio DEFAULT (SYSDATETIME()),
                    fecha_fin DATETIME2 NULL,
                    mensaje_error NVARCHAR(MAX) NULL
                );
            END;

            IF OBJECT_ID(N'regulatorio.cierre_credito', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.cierre_credito
                (
                    id_cierre_credito BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_cierre_credito PRIMARY KEY,
                    id_lote_cierre BIGINT NOT NULL,
                    fecha_cierre DATE NOT NULL,
                    tipo_cierre NVARCHAR(20) NOT NULL,
                    cedula_id_cliente_ofic_ciclo NVARCHAR(100) NOT NULL,
                    cedula_id_cliente NVARCHAR(50) NULL,
                    nom_cliente NVARCHAR(250) NULL,
                    tipo_agrupacion TINYINT NOT NULL,
                    garantia NVARCHAR(50) NULL,
                    oficina NVARCHAR(20) NULL,
                    fecha_desembolso DATE NULL,
                    fecha_vencimiento DATE NULL,
                    fecha_saneamiento DATE NULL,
                    estado_operativo_origen NVARCHAR(5) NULL,
                    estado_operativo_final NVARCHAR(5) NULL,
                    categoria NVARCHAR(5) NULL,
                    clasificacion_credito NVARCHAR(20) NULL,
                    porcentaje_provision DECIMAL(12,6) NOT NULL CONSTRAINT DF_regulatorio_cierre_pct DEFAULT (0),
                    monto_provision DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_prov DEFAULT (0),
                    monto_provision_anterior DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_prov_ant DEFAULT (0),
                    saldo_capital DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_capital DEFAULT (0),
                    saldo_total DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_total DEFAULT (0),
                    monto_en_mora DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_mora DEFAULT (0),
                    cuotas_vencidas INT NOT NULL CONSTRAINT DF_regulatorio_cierre_cuotas DEFAULT (0),
                    dias_mora INT NOT NULL CONSTRAINT DF_regulatorio_cierre_dias DEFAULT (0),
                    reconocimiento_ingreso BIT NOT NULL CONSTRAINT DF_regulatorio_cierre_ingreso DEFAULT (1),
                    sanea_intereses BIT NOT NULL CONSTRAINT DF_regulatorio_cierre_sanea DEFAULT (0),
                    interes_por_cobrar DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_interes DEFAULT (0),
                    comision_por_cobrar DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_comision DEFAULT (0),
                    mto_por_vencer_030 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_pv030 DEFAULT (0),
                    mto_por_vencer_060 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_pv060 DEFAULT (0),
                    mto_por_vencer_090 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_pv090 DEFAULT (0),
                    mto_por_vencer_120 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_pv120 DEFAULT (0),
                    mto_por_vencer_180 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_pv180 DEFAULT (0),
                    mto_por_vencer_360 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_pv360 DEFAULT (0),
                    mto_por_vencer_mas_360 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_pv361 DEFAULT (0),
                    mto_vencido_015 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v015 DEFAULT (0),
                    mto_vencido_030 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v030 DEFAULT (0),
                    mto_vencido_060 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v060 DEFAULT (0),
                    mto_vencido_090 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v090 DEFAULT (0),
                    mto_vencido_120 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v120 DEFAULT (0),
                    mto_vencido_180 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v180 DEFAULT (0),
                    mto_vencido_360 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v360 DEFAULT (0),
                    mto_vencido_mas_360 DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_cierre_v361 DEFAULT (0),
                    cta_capital NVARCHAR(20) NULL,
                    cta_interes NVARCHAR(20) NULL,
                    cta_provision NVARCHAR(20) NULL,
                    cta_ingreso_financiero NVARCHAR(20) NULL,
                    cta_gasto_provision NVARCHAR(20) NULL,
                    cta_ingreso_reversion NVARCHAR(20) NULL,
                    cta_gasto_saneamiento NVARCHAR(20) NULL,
                    cta_orden_saneado NVARCHAR(20) NULL,
                    cta_orden_saneado_contra NVARCHAR(20) NULL,
                    cta_documentado NVARCHAR(20) NULL,
                    cta_documentado_contra NVARCHAR(20) NULL,
                    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_regulatorio_cierre_fecha DEFAULT (SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'regulatorio.historial_clasificacion', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.historial_clasificacion
                (
                    id_historial BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_historial_clasificacion PRIMARY KEY,
                    cedula_id_cliente_ofic_ciclo NVARCHAR(100) NOT NULL,
                    fecha_cierre DATE NOT NULL,
                    estado_operativo NVARCHAR(5) NULL,
                    categoria NVARCHAR(5) NULL,
                    clasificacion_credito NVARCHAR(20) NULL,
                    dias_mora INT NOT NULL CONSTRAINT DF_regulatorio_hist_clas_dias DEFAULT (0),
                    cuotas_vencidas INT NOT NULL CONSTRAINT DF_regulatorio_hist_clas_cuotas DEFAULT (0),
                    id_lote_cierre BIGINT NOT NULL
                );
            END;

            IF OBJECT_ID(N'regulatorio.historial_provision', N'U') IS NULL
            BEGIN
                CREATE TABLE regulatorio.historial_provision
                (
                    id_historial_prov BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_regulatorio_historial_provision PRIMARY KEY,
                    cedula_id_cliente_ofic_ciclo NVARCHAR(100) NOT NULL,
                    fecha_cierre DATE NOT NULL,
                    categoria NVARCHAR(5) NULL,
                    porcentaje_provision DECIMAL(12,6) NOT NULL CONSTRAINT DF_regulatorio_hist_prov_pct DEFAULT (0),
                    saldo_capital DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_hist_prov_capital DEFAULT (0),
                    monto_provision DECIMAL(18,2) NOT NULL CONSTRAINT DF_regulatorio_hist_prov_monto DEFAULT (0),
                    id_lote_cierre BIGINT NOT NULL
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM regulatorio.version_regla WHERE codigo_version = N'MUC_CONAMI_V09')
            BEGIN
                INSERT INTO regulatorio.version_regla(codigo_version, descripcion, fecha_vigencia_desde, es_activa)
                VALUES(N'MUC_CONAMI_V09', N'Version parametrizada cartera CONAMI SIFNIC', '2024-01-01', 1);
            END;

            DECLARE @id_version INT;
            SELECT @id_version = id_version_regla
            FROM regulatorio.version_regla
            WHERE codigo_version = N'MUC_CONAMI_V09';

            ;WITH d AS
            (
                SELECT *,
                    ROW_NUMBER() OVER (
                        PARTITION BY id_version_regla, ISNULL(tipo_agrupacion, 0), dias_mora_desde, ISNULL(dias_mora_hasta, -1), categoria
                        ORDER BY id_regla_clasificacion
                    ) AS rn
                FROM regulatorio.regla_clasificacion
                WHERE id_version_regla = @id_version
            )
            DELETE FROM d WHERE rn > 1;

            ;WITH d AS
            (
                SELECT *,
                    ROW_NUMBER() OVER (
                        PARTITION BY id_version_regla, ISNULL(tipo_agrupacion, 0), categoria, ISNULL(estado_operativo, N'')
                        ORDER BY id_regla_provision
                    ) AS rn
                FROM regulatorio.regla_provision
                WHERE id_version_regla = @id_version
            )
            DELETE FROM d WHERE rn > 1;

            DECLARE @clasificacion TABLE(
                dias_desde INT NOT NULL,
                dias_hasta INT NULL,
                categoria NVARCHAR(5) NOT NULL,
                clasificacion NVARCHAR(20) NOT NULL,
                reconocimiento BIT NOT NULL,
                sanea BIT NOT NULL,
                vencido BIT NOT NULL,
                judicial BIT NOT NULL,
                observacion NVARCHAR(250) NOT NULL
            );

            INSERT INTO @clasificacion(dias_desde,dias_hasta,categoria,clasificacion,reconocimiento,sanea,vencido,judicial,observacion)
            VALUES
            (0,0,N'A',N'NORMAL',1,0,0,0,N'Al dia'),
            (1,30,N'B',N'RIESGO_BAJO',1,0,1,0,N'Mora inicial'),
            (31,60,N'C',N'RIESGO_MEDIO',1,0,1,0,N'Mora media'),
            (61,90,N'D',N'RIESGO_ALTO',0,1,1,0,N'Mora alta'),
            (91,NULL,N'E',N'IRRECUPERABLE',0,1,1,1,N'Irrecuperable o cobro judicial');

            INSERT INTO regulatorio.regla_clasificacion(
                id_version_regla,tipo_agrupacion,dias_mora_desde,dias_mora_hasta,categoria,clasificacion_credito,
                reconocimiento_ingreso,sanea_intereses,pasa_a_vencido,pasa_a_cobro_judicial,observacion
            )
            SELECT @id_version,NULL,c.dias_desde,c.dias_hasta,c.categoria,c.clasificacion,c.reconocimiento,c.sanea,c.vencido,c.judicial,c.observacion
            FROM @clasificacion c
            WHERE NOT EXISTS (
                SELECT 1 FROM regulatorio.regla_clasificacion r
                WHERE r.id_version_regla = @id_version
                  AND r.tipo_agrupacion IS NULL
                  AND r.dias_mora_desde = c.dias_desde
                  AND ISNULL(r.dias_mora_hasta, -1) = ISNULL(c.dias_hasta, -1)
            );

            DECLARE @provision TABLE(categoria NVARCHAR(5) NOT NULL, porcentaje DECIMAL(12,6) NOT NULL);
            INSERT INTO @provision(categoria,porcentaje)
            VALUES(N'A',0.010000),(N'B',0.050000),(N'C',0.200000),(N'D',0.500000),(N'E',1.000000);

            INSERT INTO regulatorio.regla_provision(id_version_regla,tipo_agrupacion,categoria,estado_operativo,porcentaje_provision,permite_reversion,observacion)
            SELECT @id_version,NULL,p.categoria,NULL,p.porcentaje,1,N'Provision base CONAMI parametrizada'
            FROM @provision p
            WHERE NOT EXISTS (
                SELECT 1 FROM regulatorio.regla_provision r
                WHERE r.id_version_regla = @id_version
                  AND r.tipo_agrupacion IS NULL
                  AND r.categoria = p.categoria
                  AND r.estado_operativo IS NULL
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'regulatorio.cierre_credito') AND name = N'IX_regulatorio_cierre_credito_lote')
                CREATE INDEX IX_regulatorio_cierre_credito_lote ON regulatorio.cierre_credito(id_lote_cierre, categoria, estado_operativo_final);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'regulatorio.lote_cierre') AND name = N'IX_regulatorio_lote_cierre_fecha')
                CREATE INDEX IX_regulatorio_lote_cierre_fecha ON regulatorio.lote_cierre(fecha_cierre, tipo_cierre, id_version_regla);
            """;

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 180;
        command.ExecuteNonQuery();

        CreateProceduresAndViews(connection);
    }

    private static void CreateProceduresAndViews(SqlConnection connection)
    {
        const string sql = """
            CREATE OR ALTER PROCEDURE regulatorio.usp_calcular_cartera_conami_sifnic
                @fecha_corte DATE,
                @codigo_version NVARCHAR(50) = N'MUC_CONAMI_V09',
                @tipo_cierre NVARCHAR(20) = N'DIARIO',
                @persistir BIT = 0,
                @reprocesar BIT = 0,
                @usuario_ejecucion NVARCHAR(100) = NULL
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @id_version INT, @id_lote BIGINT = NULL;

                SELECT TOP (1) @id_version = id_version_regla
                FROM regulatorio.version_regla
                WHERE codigo_version = @codigo_version
                  AND es_activa = 1
                  AND @fecha_corte >= fecha_vigencia_desde
                  AND (@fecha_corte <= ISNULL(fecha_vigencia_hasta, '9999-12-31'))
                ORDER BY fecha_vigencia_desde DESC;

                IF @id_version IS NULL
                    THROW 58001, N'No existe version activa de reglas CONAMI para la fecha indicada.', 1;

                IF OBJECT_ID('tempdb..#plan') IS NOT NULL DROP TABLE #plan;
                IF OBJECT_ID('tempdb..#base') IS NOT NULL DROP TABLE #base;
                IF OBJECT_ID('tempdb..#clasif') IS NOT NULL DROP TABLE #clasif;

                SELECT
                    COALESCE(pp.id_credito, cr.id_credito) AS id_credito,
                    pp.cedula_id_cliente_ofic_ciclo,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) BETWEEN 1 AND 15 THEN pendiente_capital ELSE 0 END) AS mto_vencido_015,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) BETWEEN 16 AND 30 THEN pendiente_capital ELSE 0 END) AS mto_vencido_030,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) BETWEEN 31 AND 60 THEN pendiente_capital ELSE 0 END) AS mto_vencido_060,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) BETWEEN 61 AND 90 THEN pendiente_capital ELSE 0 END) AS mto_vencido_090,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) BETWEEN 91 AND 120 THEN pendiente_capital ELSE 0 END) AS mto_vencido_120,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) BETWEEN 121 AND 180 THEN pendiente_capital ELSE 0 END) AS mto_vencido_180,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) BETWEEN 181 AND 360 THEN pendiente_capital ELSE 0 END) AS mto_vencido_360,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, pp.fecha_cuota, @fecha_corte) >= 361 THEN pendiente_capital ELSE 0 END) AS mto_vencido_mas_360,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, @fecha_corte, pp.fecha_cuota) BETWEEN 1 AND 30 THEN pendiente_capital ELSE 0 END) AS mto_por_vencer_030,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, @fecha_corte, pp.fecha_cuota) BETWEEN 31 AND 60 THEN pendiente_capital ELSE 0 END) AS mto_por_vencer_060,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, @fecha_corte, pp.fecha_cuota) BETWEEN 61 AND 90 THEN pendiente_capital ELSE 0 END) AS mto_por_vencer_090,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, @fecha_corte, pp.fecha_cuota) BETWEEN 91 AND 120 THEN pendiente_capital ELSE 0 END) AS mto_por_vencer_120,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, @fecha_corte, pp.fecha_cuota) BETWEEN 121 AND 180 THEN pendiente_capital ELSE 0 END) AS mto_por_vencer_180,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, @fecha_corte, pp.fecha_cuota) BETWEEN 181 AND 360 THEN pendiente_capital ELSE 0 END) AS mto_por_vencer_360,
                    SUM(CASE WHEN pendiente_capital > 0 AND DATEDIFF(DAY, @fecha_corte, pp.fecha_cuota) >= 361 THEN pendiente_capital ELSE 0 END) AS mto_por_vencer_mas_360,
                    MIN(CASE WHEN pendiente_total > 0 AND pp.fecha_cuota < @fecha_corte THEN pp.fecha_cuota END) AS fec_cuota_mas_antigua,
                    SUM(CASE WHEN pendiente_total > 0 AND pp.fecha_cuota < @fecha_corte THEN 1 ELSE 0 END) AS cuotas_vencidas,
                    SUM(CASE WHEN pendiente_total > 0 AND pp.fecha_cuota < @fecha_corte THEN pendiente_total ELSE 0 END) AS monto_en_mora
                INTO #plan
                FROM creditos.plan_pago_credito pp
                LEFT JOIN creditos.credito cr
                    ON cr.cedula_id_cliente_ofic_ciclo = pp.cedula_id_cliente_ofic_ciclo
                CROSS APPLY
                (
                    SELECT
                        CASE
                            WHEN ISNULL(pp.capital_programado, 0) > 0 THEN
                                CASE WHEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.capital_dispensado_cuota, 0) > 0
                                     THEN ISNULL(pp.capital_programado, 0) - ISNULL(pp.capital_pagado_cuota, 0) - ISNULL(pp.capital_dispensado_cuota, 0) ELSE 0 END
                            ELSE CASE WHEN ISNULL(pp.saldo_capital_cuota, 0) > 0 THEN ISNULL(pp.saldo_capital_cuota, 0) ELSE 0 END
                        END AS pendiente_capital,
                        CASE WHEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) - ISNULL(pp.interes_dispensado_cuota, 0) > 0
                             THEN ISNULL(pp.interes_programado, 0) - ISNULL(pp.interes_pagado_cuota, 0) - ISNULL(pp.interes_dispensado_cuota, 0)
                             ELSE ISNULL(pp.saldo_interes_cuota, 0) END AS pendiente_interes,
                        CASE WHEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.comision_dispensada_cuota, 0) > 0
                             THEN ISNULL(pp.comision_programada, 0) - ISNULL(pp.comision_pagada_cuota, 0) - ISNULL(pp.comision_dispensada_cuota, 0)
                             ELSE ISNULL(pp.saldo_comision_cuota, 0) END AS pendiente_comision,
                        CASE WHEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) - ISNULL(pp.mora_dispensada_cuota, 0) > 0
                             THEN ISNULL(pp.mora_programada, 0) - ISNULL(pp.mora_pagada_cuota, 0) - ISNULL(pp.mora_dispensada_cuota, 0)
                             ELSE ISNULL(pp.saldo_mora_cuota, 0) END AS pendiente_mora
                ) p
                CROSS APPLY
                (
                    SELECT p.pendiente_capital + p.pendiente_interes + p.pendiente_comision + p.pendiente_mora + ISNULL(pp.deslizamiento_programado, 0) AS pendiente_total
                ) t
                WHERE ISNULL(pp.pagada, 0) = 0
                GROUP BY COALESCE(pp.id_credito, cr.id_credito), pp.cedula_id_cliente_ofic_ciclo;

                SELECT
                    cr.id_credito,
                    COALESCE(NULLIF(cr.cedula_id_cliente_ofic_ciclo, N''), cr.numero_credito, CONVERT(NVARCHAR(100), cr.id_credito)) AS cedula_id_cliente_ofic_ciclo,
                    cr.cedula_id_cliente,
                    cr.nom_cliente,
                    TRY_CAST(ISNULL(cr.tipo_agrupacion, 1) AS TINYINT) AS tipo_agrupacion,
                    cr.garantia,
                    cr.oficina,
                    CAST(cr.fecha_desembolso AS DATE) AS fecha_desembolso,
                    CAST(cr.fecha_vencimiento AS DATE) AS fecha_vencimiento,
                    CAST(cr.fecha_saneamiento AS DATE) AS fecha_saneamiento,
                    ISNULL(cr.estado_operativo, N'VI') AS estado_operativo_origen,
                    CAST(ISNULL(cr.saldo_capital, 0) AS DECIMAL(18,2)) AS saldo_capital,
                    CAST(ISNULL(cr.saldo_capital,0) + ISNULL(cr.interes_acumulado,0) + ISNULL(cr.mora_acumulada,0) + ISNULL(cr.cargos_acumulados,0) + ISNULL(cr.comision_acumulada,0) AS DECIMAL(18,2)) AS saldo_total,
                    CAST(CASE WHEN ISNULL(cr.interes_acumulado,0) - ISNULL(cr.interes_pagado,0) > 0 THEN ISNULL(cr.interes_acumulado,0) - ISNULL(cr.interes_pagado,0) ELSE 0 END AS DECIMAL(18,2)) AS interes_por_cobrar,
                    CAST(CASE WHEN ISNULL(cr.comision_acumulada,0) - ISNULL(cr.comision_pagada,0) > 0 THEN ISNULL(cr.comision_acumulada,0) - ISNULL(cr.comision_pagada,0) ELSE 0 END AS DECIMAL(18,2)) AS comision_por_cobrar,
                    ISNULL(p.cuotas_vencidas, 0) AS cuotas_vencidas,
                    CASE WHEN p.fec_cuota_mas_antigua IS NULL THEN 0 ELSE DATEDIFF(DAY, p.fec_cuota_mas_antigua, @fecha_corte) END AS dias_mora,
                    CAST(ISNULL(p.mto_vencido_015,0) AS DECIMAL(18,2)) AS mto_vencido_015,
                    CAST(ISNULL(p.mto_vencido_030,0) AS DECIMAL(18,2)) AS mto_vencido_030,
                    CAST(ISNULL(p.mto_vencido_060,0) AS DECIMAL(18,2)) AS mto_vencido_060,
                    CAST(ISNULL(p.mto_vencido_090,0) AS DECIMAL(18,2)) AS mto_vencido_090,
                    CAST(ISNULL(p.mto_vencido_120,0) AS DECIMAL(18,2)) AS mto_vencido_120,
                    CAST(ISNULL(p.mto_vencido_180,0) AS DECIMAL(18,2)) AS mto_vencido_180,
                    CAST(ISNULL(p.mto_vencido_360,0) AS DECIMAL(18,2)) AS mto_vencido_360,
                    CAST(ISNULL(p.mto_vencido_mas_360,0) AS DECIMAL(18,2)) AS mto_vencido_mas_360,
                    CAST(ISNULL(p.mto_por_vencer_030,0) AS DECIMAL(18,2)) AS mto_por_vencer_030,
                    CAST(ISNULL(p.mto_por_vencer_060,0) AS DECIMAL(18,2)) AS mto_por_vencer_060,
                    CAST(ISNULL(p.mto_por_vencer_090,0) AS DECIMAL(18,2)) AS mto_por_vencer_090,
                    CAST(ISNULL(p.mto_por_vencer_120,0) AS DECIMAL(18,2)) AS mto_por_vencer_120,
                    CAST(ISNULL(p.mto_por_vencer_180,0) AS DECIMAL(18,2)) AS mto_por_vencer_180,
                    CAST(ISNULL(p.mto_por_vencer_360,0) AS DECIMAL(18,2)) AS mto_por_vencer_360,
                    CAST(ISNULL(p.mto_por_vencer_mas_360,0) AS DECIMAL(18,2)) AS mto_por_vencer_mas_360,
                    CAST(ISNULL(p.monto_en_mora,0) AS DECIMAL(18,2)) AS monto_en_mora
                INTO #base
                FROM creditos.credito cr
                LEFT JOIN #plan p
                    ON p.id_credito = cr.id_credito
                    OR (p.id_credito IS NULL AND p.cedula_id_cliente_ofic_ciclo = cr.cedula_id_cliente_ofic_ciclo)
                WHERE cr.activo = 1
                  AND (cr.fecha_desembolso IS NULL OR CONVERT(date, cr.fecha_desembolso) <= @fecha_corte)
                  AND ISNULL(cr.estado_operativo, N'') NOT IN (N'AN', N'ANULADO');

                SELECT
                    b.*,
                    rc.categoria,
                    rc.clasificacion_credito,
                    rc.reconocimiento_ingreso,
                    rc.sanea_intereses,
                    CASE
                        WHEN b.saldo_capital <= 0.50 THEN N'CA'
                        WHEN b.estado_operativo_origen = N'SA' THEN N'SA'
                        WHEN rc.pasa_a_cobro_judicial = 1 THEN N'CJ'
                        WHEN rc.pasa_a_vencido = 1 THEN N'VE'
                        ELSE b.estado_operativo_origen
                    END AS estado_operativo_final,
                    rp.porcentaje_provision
                INTO #clasif
                FROM #base b
                OUTER APPLY
                (
                    SELECT TOP (1) *
                    FROM regulatorio.regla_clasificacion rc
                    WHERE rc.id_version_regla = @id_version
                      AND (rc.tipo_agrupacion IS NULL OR rc.tipo_agrupacion = b.tipo_agrupacion)
                      AND b.dias_mora >= rc.dias_mora_desde
                      AND (rc.dias_mora_hasta IS NULL OR b.dias_mora <= rc.dias_mora_hasta)
                    ORDER BY CASE WHEN rc.tipo_agrupacion = b.tipo_agrupacion THEN 0 ELSE 1 END, rc.dias_mora_desde DESC
                ) rc
                OUTER APPLY
                (
                    SELECT TOP (1) *
                    FROM regulatorio.regla_provision rp
                    WHERE rp.id_version_regla = @id_version
                      AND rp.categoria = rc.categoria
                      AND (rp.tipo_agrupacion IS NULL OR rp.tipo_agrupacion = b.tipo_agrupacion)
                      AND (rp.estado_operativo IS NULL OR rp.estado_operativo = CASE
                            WHEN b.saldo_capital <= 0.50 THEN N'CA'
                            WHEN b.estado_operativo_origen = N'SA' THEN N'SA'
                            WHEN rc.pasa_a_cobro_judicial = 1 THEN N'CJ'
                            WHEN rc.pasa_a_vencido = 1 THEN N'VE'
                            ELSE b.estado_operativo_origen END)
                    ORDER BY CASE WHEN rp.tipo_agrupacion = b.tipo_agrupacion THEN 0 ELSE 1 END,
                             CASE WHEN rp.estado_operativo IS NOT NULL THEN 0 ELSE 1 END
                ) rp;

                IF EXISTS (SELECT 1 FROM #clasif WHERE categoria IS NULL)
                    THROW 58002, N'Faltan reglas de clasificacion para una o mas combinaciones de mora.', 1;

                IF EXISTS (SELECT 1 FROM #clasif WHERE porcentaje_provision IS NULL)
                    THROW 58003, N'Faltan reglas de provision para una o mas combinaciones de categoria/estado.', 1;

                IF @persistir = 1
                BEGIN
                    BEGIN TRAN;

                    IF @reprocesar = 1
                    BEGIN
                        DELETE cc
                        FROM regulatorio.cierre_credito cc
                        INNER JOIN regulatorio.lote_cierre lc ON lc.id_lote_cierre = cc.id_lote_cierre
                        WHERE lc.fecha_cierre = @fecha_corte
                          AND lc.tipo_cierre = @tipo_cierre
                          AND lc.id_version_regla = @id_version;

                        DELETE FROM regulatorio.lote_cierre
                        WHERE fecha_cierre = @fecha_corte
                          AND tipo_cierre = @tipo_cierre
                          AND id_version_regla = @id_version;
                    END;

                    INSERT INTO regulatorio.lote_cierre(fecha_cierre,tipo_cierre,id_version_regla,estado_lote,usuario_ejecucion)
                    VALUES(@fecha_corte,@tipo_cierre,@id_version,N'PROCESANDO',ISNULL(NULLIF(@usuario_ejecucion, N''), SUSER_SNAME()));

                    SET @id_lote = SCOPE_IDENTITY();

                    INSERT INTO regulatorio.cierre_credito(
                        id_lote_cierre,fecha_cierre,tipo_cierre,cedula_id_cliente_ofic_ciclo,cedula_id_cliente,nom_cliente,
                        tipo_agrupacion,garantia,oficina,fecha_desembolso,fecha_vencimiento,fecha_saneamiento,
                        estado_operativo_origen,estado_operativo_final,categoria,clasificacion_credito,
                        porcentaje_provision,monto_provision,monto_provision_anterior,saldo_capital,saldo_total,monto_en_mora,
                        cuotas_vencidas,dias_mora,reconocimiento_ingreso,sanea_intereses,interes_por_cobrar,comision_por_cobrar,
                        mto_por_vencer_030,mto_por_vencer_060,mto_por_vencer_090,mto_por_vencer_120,mto_por_vencer_180,mto_por_vencer_360,mto_por_vencer_mas_360,
                        mto_vencido_015,mto_vencido_030,mto_vencido_060,mto_vencido_090,mto_vencido_120,mto_vencido_180,mto_vencido_360,mto_vencido_mas_360,
                        cta_capital,cta_interes,cta_provision,cta_ingreso_financiero,cta_gasto_provision,cta_ingreso_reversion,cta_gasto_saneamiento,
                        cta_orden_saneado,cta_orden_saneado_contra,cta_documentado,cta_documentado_contra
                    )
                    SELECT
                        @id_lote,@fecha_corte,@tipo_cierre,c.cedula_id_cliente_ofic_ciclo,c.cedula_id_cliente,c.nom_cliente,
                        c.tipo_agrupacion,c.garantia,c.oficina,c.fecha_desembolso,c.fecha_vencimiento,c.fecha_saneamiento,
                        c.estado_operativo_origen,c.estado_operativo_final,c.categoria,c.clasificacion_credito,
                        c.porcentaje_provision,
                        ROUND(c.saldo_capital * c.porcentaje_provision, 2) AS monto_provision,
                        ISNULL(prev.monto_provision, 0) AS monto_provision_anterior,
                        c.saldo_capital,c.saldo_total,c.monto_en_mora,
                        c.cuotas_vencidas,c.dias_mora,c.reconocimiento_ingreso,c.sanea_intereses,c.interes_por_cobrar,c.comision_por_cobrar,
                        c.mto_por_vencer_030,c.mto_por_vencer_060,c.mto_por_vencer_090,c.mto_por_vencer_120,c.mto_por_vencer_180,c.mto_por_vencer_360,c.mto_por_vencer_mas_360,
                        c.mto_vencido_015,c.mto_vencido_030,c.mto_vencido_060,c.mto_vencido_090,c.mto_vencido_120,c.mto_vencido_180,c.mto_vencido_360,c.mto_vencido_mas_360,
                        m.cta_capital,m.cta_interes,m.cta_provision,m.cta_ingreso_financiero,m.cta_gasto_provision,m.cta_ingreso_reversion,m.cta_gasto_saneamiento,
                        m.cta_orden_saneado,m.cta_orden_saneado_contra,m.cta_documentado,m.cta_documentado_contra
                    FROM #clasif c
                    OUTER APPLY
                    (
                        SELECT TOP (1) cc.monto_provision
                        FROM regulatorio.cierre_credito cc
                        INNER JOIN regulatorio.lote_cierre lc ON lc.id_lote_cierre = cc.id_lote_cierre
                        WHERE cc.cedula_id_cliente_ofic_ciclo = c.cedula_id_cliente_ofic_ciclo
                          AND lc.fecha_cierre < @fecha_corte
                        ORDER BY lc.fecha_cierre DESC, cc.id_cierre_credito DESC
                    ) prev
                    LEFT JOIN contabilidad.mapeo_estado_muc m
                      ON m.id_version_regla = @id_version
                     AND m.estado_operativo = c.estado_operativo_final
                     AND m.tipo_agrupacion = c.tipo_agrupacion;

                    INSERT INTO regulatorio.historial_clasificacion(cedula_id_cliente_ofic_ciclo,fecha_cierre,estado_operativo,categoria,clasificacion_credito,dias_mora,cuotas_vencidas,id_lote_cierre)
                    SELECT cedula_id_cliente_ofic_ciclo,fecha_cierre,estado_operativo_final,categoria,clasificacion_credito,dias_mora,cuotas_vencidas,id_lote_cierre
                    FROM regulatorio.cierre_credito
                    WHERE id_lote_cierre = @id_lote;

                    INSERT INTO regulatorio.historial_provision(cedula_id_cliente_ofic_ciclo,fecha_cierre,categoria,porcentaje_provision,saldo_capital,monto_provision,id_lote_cierre)
                    SELECT cedula_id_cliente_ofic_ciclo,fecha_cierre,categoria,porcentaje_provision,saldo_capital,monto_provision,id_lote_cierre
                    FROM regulatorio.cierre_credito
                    WHERE id_lote_cierre = @id_lote;

                    UPDATE lc
                       SET total_creditos = x.total_creditos,
                           total_saldo = x.total_saldo,
                           total_provision = x.total_provision,
                           estado_lote = N'SNAPSHOT_OK',
                           fecha_fin = SYSDATETIME()
                    FROM regulatorio.lote_cierre lc
                    CROSS APPLY
                    (
                        SELECT COUNT(1) AS total_creditos,
                               ISNULL(SUM(saldo_capital), 0) AS total_saldo,
                               ISNULL(SUM(monto_provision), 0) AS total_provision
                        FROM regulatorio.cierre_credito
                        WHERE id_lote_cierre = @id_lote
                    ) x
                    WHERE lc.id_lote_cierre = @id_lote;

                    COMMIT TRAN;
                END;

                SELECT
                    c.cedula_id_cliente_ofic_ciclo,
                    c.cedula_id_cliente,
                    c.nom_cliente,
                    c.estado_operativo_origen,
                    c.estado_operativo_final,
                    c.dias_mora,
                    c.cuotas_vencidas,
                    c.categoria,
                    c.clasificacion_credito,
                    c.porcentaje_provision,
                    ROUND(c.saldo_capital * c.porcentaje_provision, 2) AS monto_provision,
                    c.saldo_capital,
                    c.saldo_total,
                    c.monto_en_mora,
                    c.tipo_agrupacion,
                    c.garantia,
                    c.oficina,
                    c.fecha_desembolso,
                    c.fecha_vencimiento,
                    c.reconocimiento_ingreso,
                    c.sanea_intereses,
                    c.interes_por_cobrar,
                    c.comision_por_cobrar,
                    @id_lote AS id_lote_cierre
                FROM #clasif c
                ORDER BY c.dias_mora DESC, c.cedula_id_cliente_ofic_ciclo;
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 180;
        command.ExecuteNonQuery();

        const string views = """
            CREATE OR ALTER VIEW reportes.vw_cartera_regulatoria_ultimo_cierre
            AS
            SELECT
                cc.*,
                lc.estado_lote,
                lc.total_creditos,
                lc.total_saldo,
                lc.total_provision,
                lc.usuario_ejecucion,
                lc.fecha_inicio,
                lc.fecha_fin
            FROM regulatorio.cierre_credito cc
            INNER JOIN regulatorio.lote_cierre lc
                ON lc.id_lote_cierre = cc.id_lote_cierre
            WHERE lc.id_lote_cierre =
            (
                SELECT TOP (1) id_lote_cierre
                FROM regulatorio.lote_cierre
                WHERE estado_lote = N'SNAPSHOT_OK'
                ORDER BY fecha_cierre DESC, id_lote_cierre DESC
            );
            """;

        using var viewCommand = new SqlCommand(views, connection);
        viewCommand.CommandTimeout = 120;
        viewCommand.ExecuteNonQuery();
    }
}
