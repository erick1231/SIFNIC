using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Sifnic.Api.Creditos;

public static class CreditOperationsSupport
{
    public static readonly string[] ClientStatuses = ["ACTIVO", "INACTIVO", "BLOQUEADO", "PROSPECTO"];
    public static readonly string[] CreditRequestStatuses = ["TRAMITE", "PRECALIFICADA", "COMITE", "MEJORA", "APROBADA", "RECHAZADA", "ANULADA"];
    public static readonly string[] ProspectionStages = ["PROSPECTO", "PRECALIFICADO", "DESCARTADO", "SOLICITUD_FORMAL"];
    public static readonly string[] VisitResults = ["PENDIENTE", "REALIZADA", "NO_UBICADO", "RECHAZADA", "NO_APLICA"];
    public static readonly string[] CreditBureauResults = ["SIN_CONSULTA", "ACEPTABLE", "OBSERVADO", "BLOQUEADO"];
    public static readonly string[] Frequencies = ["MENSUAL", "QUINCENAL", "SEMANAL", "DIARIO"];
    public static readonly string[] RiskLevels = ["BAJO", "MEDIO", "ALTO"];

    public static void EnsureSchema(SqlConnection connection)
    {
        const string sql = """
            IF SCHEMA_ID(N'clientes') IS NULL EXEC(N'CREATE SCHEMA clientes');
            IF SCHEMA_ID(N'creditos') IS NULL EXEC(N'CREATE SCHEMA creditos');
            IF SCHEMA_ID(N'operacion') IS NULL EXEC(N'CREATE SCHEMA operacion');

            IF COL_LENGTH(N'creditos.credito', N'estado_operativo') IS NOT NULL
               AND EXISTS (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON t.object_id = c.object_id
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                    WHERE s.name = N'creditos'
                      AND t.name = N'credito'
                      AND c.name = N'estado_operativo'
                      AND ty.name = N'nvarchar'
                      AND c.max_length < 60
               )
                ALTER TABLE creditos.credito ALTER COLUMN estado_operativo NVARCHAR(30) NOT NULL;

            IF COL_LENGTH(N'clientes.cliente', N'tipo_identificacion') IS NULL
                ALTER TABLE clientes.cliente ADD tipo_identificacion NVARCHAR(30) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'sucursal') IS NULL
                ALTER TABLE clientes.cliente ADD sucursal NVARCHAR(100) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'relacion_cliente') IS NULL
                ALTER TABLE clientes.cliente ADD relacion_cliente NVARCHAR(50) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'estado_cliente') IS NULL
                ALTER TABLE clientes.cliente ADD estado_cliente NVARCHAR(30) NOT NULL CONSTRAINT DF_clientes_cliente_estado_cliente DEFAULT (N'ACTIVO');
            IF COL_LENGTH(N'clientes.cliente', N'fecha_ingreso') IS NULL
                ALTER TABLE clientes.cliente ADD fecha_ingreso DATE NOT NULL CONSTRAINT DF_clientes_cliente_fecha_ingreso DEFAULT (CONVERT(date, SYSDATETIME()));
            IF COL_LENGTH(N'clientes.cliente', N'genero') IS NULL
                ALTER TABLE clientes.cliente ADD genero NVARCHAR(20) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'estado_civil') IS NULL
                ALTER TABLE clientes.cliente ADD estado_civil NVARCHAR(30) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'nombre_conyuge') IS NULL
                ALTER TABLE clientes.cliente ADD nombre_conyuge NVARCHAR(200) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'telefono_secundario') IS NULL
                ALTER TABLE clientes.cliente ADD telefono_secundario NVARCHAR(50) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'celular') IS NULL
                ALTER TABLE clientes.cliente ADD celular NVARCHAR(50) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'geografia_casa') IS NULL
                ALTER TABLE clientes.cliente ADD geografia_casa NVARCHAR(200) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'ocupacion') IS NULL
                ALTER TABLE clientes.cliente ADD ocupacion NVARCHAR(150) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'actividad_economica') IS NULL
                ALTER TABLE clientes.cliente ADD actividad_economica NVARCHAR(200) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'nombre_negocio') IS NULL
                ALTER TABLE clientes.cliente ADD nombre_negocio NVARCHAR(200) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'direccion_negocio') IS NULL
                ALTER TABLE clientes.cliente ADD direccion_negocio NVARCHAR(300) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'geografia_negocio') IS NULL
                ALTER TABLE clientes.cliente ADD geografia_negocio NVARCHAR(200) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'antiguedad_negocio_meses') IS NULL
                ALTER TABLE clientes.cliente ADD antiguedad_negocio_meses INT NOT NULL CONSTRAINT DF_clientes_cliente_antiguedad_negocio DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'ingresos_mensuales') IS NULL
                ALTER TABLE clientes.cliente ADD ingresos_mensuales DECIMAL(18,2) NOT NULL CONSTRAINT DF_clientes_cliente_ingresos DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'ingresos_conyuge') IS NULL
                ALTER TABLE clientes.cliente ADD ingresos_conyuge DECIMAL(18,2) NOT NULL CONSTRAINT DF_clientes_cliente_ingresos_conyuge DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'remesas') IS NULL
                ALTER TABLE clientes.cliente ADD remesas DECIMAL(18,2) NOT NULL CONSTRAINT DF_clientes_cliente_remesas DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'alquileres') IS NULL
                ALTER TABLE clientes.cliente ADD alquileres DECIMAL(18,2) NOT NULL CONSTRAINT DF_clientes_cliente_alquileres DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'otros_ingresos') IS NULL
                ALTER TABLE clientes.cliente ADD otros_ingresos DECIMAL(18,2) NOT NULL CONSTRAINT DF_clientes_cliente_otros_ingresos DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'egresos_mensuales') IS NULL
                ALTER TABLE clientes.cliente ADD egresos_mensuales DECIMAL(18,2) NOT NULL CONSTRAINT DF_clientes_cliente_egresos DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'origen_fondos') IS NULL
                ALTER TABLE clientes.cliente ADD origen_fondos NVARCHAR(250) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'proposito_relacion') IS NULL
                ALTER TABLE clientes.cliente ADD proposito_relacion NVARCHAR(250) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'pep') IS NULL
                ALTER TABLE clientes.cliente ADD pep BIT NOT NULL CONSTRAINT DF_clientes_cliente_pep DEFAULT (0);
            IF COL_LENGTH(N'clientes.cliente', N'nivel_riesgo') IS NULL
                ALTER TABLE clientes.cliente ADD nivel_riesgo NVARCHAR(20) NOT NULL CONSTRAINT DF_clientes_cliente_nivel_riesgo DEFAULT (N'MEDIO');
            IF COL_LENGTH(N'clientes.cliente', N'puntaje_riesgo') IS NULL
                ALTER TABLE clientes.cliente ADD puntaje_riesgo INT NOT NULL CONSTRAINT DF_clientes_cliente_puntaje_riesgo DEFAULT (50);
            IF COL_LENGTH(N'clientes.cliente', N'estado_expediente') IS NULL
                ALTER TABLE clientes.cliente ADD estado_expediente NVARCHAR(30) NOT NULL CONSTRAINT DF_clientes_cliente_estado_expediente DEFAULT (N'INCOMPLETO');
            IF COL_LENGTH(N'clientes.cliente', N'observaciones') IS NULL
                ALTER TABLE clientes.cliente ADD observaciones NVARCHAR(1000) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'usuario_registro') IS NULL
                ALTER TABLE clientes.cliente ADD usuario_registro NVARCHAR(100) NULL;
            IF COL_LENGTH(N'clientes.cliente', N'fecha_actualizacion') IS NULL
                ALTER TABLE clientes.cliente ADD fecha_actualizacion DATETIME2 NULL;

            IF OBJECT_ID(N'clientes.solicitud_eliminacion_cliente', N'U') IS NULL
            BEGIN
                CREATE TABLE clientes.solicitud_eliminacion_cliente
                (
                    id_solicitud_eliminacion BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_clientes_solicitud_eliminacion PRIMARY KEY,
                    id_cliente BIGINT NOT NULL,
                    motivo NVARCHAR(500) NOT NULL,
                    estado NVARCHAR(30) NOT NULL CONSTRAINT DF_clientes_solicitud_eliminacion_estado DEFAULT (N'PENDIENTE'),
                    usuario_solicita NVARCHAR(100) NOT NULL,
                    fecha_solicitud DATETIME2 NOT NULL CONSTRAINT DF_clientes_solicitud_eliminacion_fecha DEFAULT (SYSDATETIME()),
                    usuario_autoriza NVARCHAR(100) NULL,
                    fecha_autorizacion DATETIME2 NULL,
                    observacion_autorizacion NVARCHAR(500) NULL,
                    CONSTRAINT FK_clientes_solicitud_eliminacion_cliente
                        FOREIGN KEY (id_cliente) REFERENCES clientes.cliente(id_cliente)
                );
            END;

            IF COL_LENGTH(N'creditos.solicitud_credito', N'producto_credito') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD producto_credito NVARCHAR(100) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'frecuencia_pago') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD frecuencia_pago NVARCHAR(30) NOT NULL CONSTRAINT DF_creditos_solicitud_frecuencia_pago DEFAULT (N'MENSUAL');
            IF COL_LENGTH(N'creditos.solicitud_credito', N'tipo_cuota') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD tipo_cuota NVARCHAR(30) NOT NULL CONSTRAINT DF_creditos_solicitud_tipo_cuota DEFAULT (N'NIVELADA');
            IF COL_LENGTH(N'creditos.solicitud_credito', N'cuota_estimada') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD cuota_estimada DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_solicitud_cuota_estimada DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'ingresos_declarados') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD ingresos_declarados DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_solicitud_ingresos DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'egresos_declarados') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD egresos_declarados DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_solicitud_egresos DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'capacidad_pago') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD capacidad_pago DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_solicitud_capacidad DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'fuente_ingreso') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD fuente_ingreso NVARCHAR(200) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'actividad_financiada') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD actividad_financiada NVARCHAR(200) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'tipo_garantia') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD tipo_garantia NVARCHAR(80) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'descripcion_garantia') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD descripcion_garantia NVARCHAR(500) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'valor_garantia') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD valor_garantia DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_solicitud_valor_garantia DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'nombre_fiador') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD nombre_fiador NVARCHAR(200) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'cedula_fiador') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD cedula_fiador NVARCHAR(50) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'telefono_fiador') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD telefono_fiador NVARCHAR(50) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'requiere_comite') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD requiere_comite BIT NOT NULL CONSTRAINT DF_creditos_solicitud_requiere_comite DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'nivel_riesgo') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD nivel_riesgo NVARCHAR(20) NOT NULL CONSTRAINT DF_creditos_solicitud_nivel_riesgo DEFAULT (N'MEDIO');
            IF COL_LENGTH(N'creditos.solicitud_credito', N'clasificacion_conami') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD clasificacion_conami NVARCHAR(10) NOT NULL CONSTRAINT DF_creditos_solicitud_clasificacion DEFAULT (N'A');
            IF COL_LENGTH(N'creditos.solicitud_credito', N'checklist_json') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD checklist_json NVARCHAR(MAX) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'plan_generado_json') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD plan_generado_json NVARCHAR(MAX) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'usuario_registro') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD usuario_registro NVARCHAR(100) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'fecha_actualizacion') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD fecha_actualizacion DATETIME2 NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'usuario_resolucion') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD usuario_resolucion NVARCHAR(100) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'fecha_resolucion') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD fecha_resolucion DATETIME2 NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'etapa_prospeccion') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD etapa_prospeccion NVARCHAR(30) NOT NULL CONSTRAINT DF_creditos_solicitud_etapa_prospeccion DEFAULT (N'PROSPECTO');
            IF COL_LENGTH(N'creditos.solicitud_credito', N'motivo_descarte_rechazo') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD motivo_descarte_rechazo NVARCHAR(500) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'promotor_credito') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD promotor_credito NVARCHAR(150) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'sucursal_credito') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD sucursal_credito NVARCHAR(100) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'oficina_credito') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD oficina_credito NVARCHAR(100) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'fecha_sistema_prospeccion') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD fecha_sistema_prospeccion DATE NOT NULL CONSTRAINT DF_creditos_solicitud_fecha_sistema_prospeccion DEFAULT (CONVERT(date, SYSDATETIME()));
            IF COL_LENGTH(N'creditos.solicitud_credito', N'referencias_prospeccion_json') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD referencias_prospeccion_json NVARCHAR(MAX) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'visitas_prospeccion_json') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD visitas_prospeccion_json NVARCHAR(MAX) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'fecha_consulta_central') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD fecha_consulta_central DATE NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'central_riesgo_json') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD central_riesgo_json NVARCHAR(MAX) NULL;
            IF COL_LENGTH(N'creditos.solicitud_credito', N'tasa_comision_ascc') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD tasa_comision_ascc DECIMAL(18,6) NOT NULL CONSTRAINT DF_creditos_solicitud_tasa_comision_ascc DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'tasa_deslizamiento_anual') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD tasa_deslizamiento_anual DECIMAL(18,6) NOT NULL CONSTRAINT DF_creditos_solicitud_tasa_deslizamiento DEFAULT (0);
            IF COL_LENGTH(N'creditos.solicitud_credito', N'tasa_mora_anual') IS NULL
                ALTER TABLE creditos.solicitud_credito ADD tasa_mora_anual DECIMAL(18,6) NOT NULL CONSTRAINT DF_creditos_solicitud_tasa_mora DEFAULT (0);

            IF COL_LENGTH(N'creditos.plan_pago_credito', N'dias_interes') IS NULL
                ALTER TABLE creditos.plan_pago_credito ADD dias_interes INT NOT NULL CONSTRAINT DF_creditos_plan_pago_dias_interes DEFAULT (0);
            IF COL_LENGTH(N'creditos.plan_pago_credito', N'deslizamiento_programado') IS NULL
                ALTER TABLE creditos.plan_pago_credito ADD deslizamiento_programado DECIMAL(18,2) NOT NULL CONSTRAINT DF_creditos_plan_pago_deslizamiento DEFAULT (0);

            IF OBJECT_ID(N'creditos.tasa_variable_credito', N'U') IS NULL
            BEGIN
                CREATE TABLE creditos.tasa_variable_credito
                (
                    id_tasa_variable BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_creditos_tasa_variable PRIMARY KEY,
                    id_credito BIGINT NOT NULL,
                    fecha_tasa DATE NOT NULL,
                    tasa_interes_anual DECIMAL(18,6) NOT NULL,
                    observacion NVARCHAR(300) NULL,
                    usuario_registro NVARCHAR(100) NULL,
                    fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_creditos_tasa_variable_fecha DEFAULT (SYSDATETIME()),
                    CONSTRAINT FK_creditos_tasa_variable_credito
                        FOREIGN KEY (id_credito) REFERENCES creditos.credito(id_credito)
                );
            END;
            """;

        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
        EnsureClienteArchivoMovilSchema(connection);
    }

    /// <summary>
    /// Tabla auxiliar para fotos de cédula y documentos capturados desde la app móvil (cliente).
    /// </summary>
    public static void EnsureClienteArchivoMovilSchema(SqlConnection connection)
    {
        const string sql = """
            IF OBJECT_ID(N'clientes.archivo_movil', N'U') IS NULL
            BEGIN
                CREATE TABLE clientes.archivo_movil
                (
                    id_archivo_movil BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_clientes_archivo_movil PRIMARY KEY,
                    id_cliente BIGINT NOT NULL,
                    tipo_documento NVARCHAR(80) NOT NULL,
                    nombre_archivo NVARCHAR(255) NOT NULL,
                    ruta_relativa NVARCHAR(500) NOT NULL,
                    fecha_registro DATETIME2 NOT NULL CONSTRAINT DF_clientes_archivo_movil_fecha DEFAULT (SYSDATETIME()),
                    usuario_registro NVARCHAR(200) NULL,
                    CONSTRAINT FK_clientes_archivo_movil_cliente
                        FOREIGN KEY (id_cliente) REFERENCES clientes.cliente(id_cliente)
                );
                CREATE INDEX IX_clientes_archivo_movil_cliente ON clientes.archivo_movil(id_cliente);
            END;
            """;

        using var cmd = new SqlCommand(sql, connection);
        cmd.CommandTimeout = 60;
        cmd.ExecuteNonQuery();
    }

    public static string GetOperatorUser(HttpRequest request)
    {
        var value = request.Headers["X-Operator-User"].ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? "sistema.local" : value;
    }

    public static object TextOrDbNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    public static object DateOrDbNull(DateTime? value)
    {
        return value.HasValue ? value.Value.Date : DBNull.Value;
    }

    public static string NormalizeCode(string? value, string fallback)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
        return text;
    }

    public static decimal SafeDecimal(decimal value)
    {
        return value < 0 ? 0 : Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyList<PlanPaymentDto> GeneratePaymentPlan(
        decimal principal,
        decimal annualRate,
        int termMonths,
        string? frequency,
        DateTime startDate,
        decimal commissionRate = 0,
        decimal slidingRate = 0,
        decimal moraRate = 0,
        string? commissionMode = null)
    {
        principal = SafeDecimal(principal);
        annualRate = annualRate < 0 ? 0 : annualRate;
        commissionRate = commissionRate < 0 ? 0 : commissionRate;
        slidingRate = slidingRate < 0 ? 0 : slidingRate;
        moraRate = moraRate < 0 ? 0 : moraRate;
        termMonths = Math.Max(termMonths, 1);

        var normalizedFrequency = NormalizeCode(frequency, "MENSUAL");
        var normalizedCommissionMode = NormalizeCode(commissionMode, "PRORRATEADA");
        var installments = normalizedFrequency switch
        {
            "DIARIO" => Math.Max(1, termMonths * 30),
            "SEMANAL" => Math.Max(1, (int)Math.Round(termMonths * 4.333m, MidpointRounding.AwayFromZero)),
            "QUINCENAL" => Math.Max(1, termMonths * 2),
            _ => termMonths,
        };

        var capitalBase = SafeDecimal(principal / installments);
        var totalCommission = SafeDecimal(principal * (commissionRate / 100m));
        var distributeCommission = normalizedCommissionMode != "DESCONTADA";
        var commissionBase = distributeCommission && installments > 0 ? SafeDecimal(totalCommission / installments) : 0;
        var balance = principal;
        var items = new List<PlanPaymentDto>();
        var previousDate = startDate.Date;

        for (var index = 1; index <= installments; index += 1)
        {
            var dueDate = normalizedFrequency switch
            {
                "DIARIO" => startDate.Date.AddDays(index),
                "SEMANAL" => startDate.Date.AddDays(7 * index),
                "QUINCENAL" => startDate.Date.AddDays(15 * index),
                _ => startDate.Date.AddMonths(index),
            };
            dueDate = AdjustBusinessDueDate(dueDate);

            var interestDays = Math.Max(1, (dueDate - previousDate).Days);
            var sliding = SafeDecimal(balance * (slidingRate / 100m) * interestDays / 360m);
            var interest = SafeDecimal((balance + sliding) * (annualRate / 100m) * interestDays / 360m);
            var capital = index == installments ? balance : capitalBase;
            if (capital < 0)
            {
                capital = 0;
            }

            if (capital > balance || index == installments)
            {
                capital = balance;
            }

            balance = SafeDecimal(balance - capital);
            var commission = !distributeCommission ? 0 : index == installments
                ? SafeDecimal(totalCommission - items.Sum(item => item.Commission))
                : commissionBase;

            items.Add(new PlanPaymentDto
            {
                Number = index,
                DueDate = dueDate,
                InterestDays = interestDays,
                Capital = capital,
                Interest = interest,
                Commission = commission,
                Sliding = sliding,
                Mora = 0,
                MoraRate = moraRate,
                Total = SafeDecimal(capital + interest + commission + sliding),
                Balance = balance,
            });
            previousDate = dueDate;
        }

        return items;
    }

    private static DateTime AdjustBusinessDueDate(DateTime dueDate)
    {
        return dueDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => dueDate.AddDays(2),
            DayOfWeek.Sunday => dueDate.AddDays(1),
            _ => dueDate,
        };
    }

    public static decimal? CalculateEffectiveAnnualCostRate(
        decimal netDisbursed,
        DateTime startDate,
        IReadOnlyList<PlanPaymentDto> plan)
    {
        if (netDisbursed <= 0 || plan.Count == 0)
        {
            return null;
        }

        var cashFlows = new List<(DateTime Date, double Amount)>
        {
            (startDate.Date, -(double)netDisbursed),
        };
        cashFlows.AddRange(plan.Select(item => (item.DueDate.Date, (double)item.Total)));

        double Npv(double rate)
        {
            var result = 0d;
            var origin = cashFlows[0].Date;
            foreach (var flow in cashFlows)
            {
                var years = (flow.Date - origin).TotalDays / 365d;
                result += flow.Amount / Math.Pow(1d + rate, years);
            }

            return result;
        }

        var low = -0.9999d;
        var high = 10d;
        var lowValue = Npv(low);
        var highValue = Npv(high);

        for (var expand = 0; Math.Sign(lowValue) == Math.Sign(highValue) && expand < 8; expand += 1)
        {
            high *= 2d;
            highValue = Npv(high);
        }

        if (Math.Sign(lowValue) == Math.Sign(highValue))
        {
            return null;
        }

        for (var i = 0; i < 100; i += 1)
        {
            var mid = (low + high) / 2d;
            var midValue = Npv(mid);
            if (Math.Abs(midValue) < 0.000001d)
            {
                return Math.Round((decimal)mid * 100m, 4, MidpointRounding.AwayFromZero);
            }

            if (Math.Sign(midValue) == Math.Sign(lowValue))
            {
                low = mid;
                lowValue = midValue;
            }
            else
            {
                high = mid;
            }
        }

        return Math.Round((decimal)((low + high) / 2d) * 100m, 4, MidpointRounding.AwayFromZero);
    }

    public static void RegisterBitacora(
        SqlConnection connection,
        SqlTransaction? transaction,
        HttpContext httpContext,
        string modulo,
        string proceso,
        string tipoEvento,
        long idReferencia,
        string referenciaTexto,
        string descripcion,
        object resumen)
    {
        using var command = transaction is null
            ? new SqlCommand("operacion.usp_registrar_bitacora_operativa", connection)
            : new SqlCommand("operacion.usp_registrar_bitacora_operativa", connection, transaction);

        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add("@modulo", SqlDbType.NVarChar, 50).Value = modulo;
        command.Parameters.Add("@proceso", SqlDbType.NVarChar, 100).Value = proceso;
        command.Parameters.Add("@tipo_evento", SqlDbType.NVarChar, 50).Value = tipoEvento;
        command.Parameters.Add("@id_referencia", SqlDbType.BigInt).Value = idReferencia;
        command.Parameters.Add("@referencia_texto", SqlDbType.NVarChar, 100).Value = referenciaTexto;
        command.Parameters.Add("@descripcion_evento", SqlDbType.NVarChar, 1000).Value = descripcion;
        command.Parameters.Add("@datos_resumen", SqlDbType.NVarChar).Value = JsonSerializer.Serialize(resumen);
        command.Parameters.Add("@usuario_registro", SqlDbType.NVarChar, 100).Value = GetOperatorUser(httpContext.Request);
        command.Parameters.Add("@equipo", SqlDbType.NVarChar, 100).Value = Environment.MachineName;
        command.Parameters.Add("@ip_equipo", SqlDbType.NVarChar, 50).Value =
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "LOCAL";
        command.ExecuteNonQuery();
    }

    public static string NextCode(
        SqlConnection connection,
        string tableName,
        string columnName,
        string prefix,
        SqlTransaction? transaction = null)
    {
        var sql = $"""
            SELECT TOP (1) {columnName}
            FROM {tableName}
            WHERE {columnName} LIKE @prefix + N'%'
            ORDER BY TRY_CONVERT(INT, RIGHT({columnName}, 6)) DESC, {columnName} DESC;
            """;

        using var command = transaction is null
            ? new SqlCommand(sql, connection)
            : new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@prefix", SqlDbType.NVarChar, 20).Value = prefix;
        var last = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
        var suffix = 0;
        if (last.Length >= 6)
        {
            int.TryParse(last[^6..], out suffix);
        }

        return $"{prefix}{suffix + 1:000000}";
    }
}

public sealed class PlanPaymentDto
{
    public int Number { get; set; }
    public DateTime DueDate { get; set; }
    public int InterestDays { get; set; }
    public decimal Capital { get; set; }
    public decimal Interest { get; set; }
    public decimal Commission { get; set; }
    public decimal Sliding { get; set; }
    public decimal Mora { get; set; }
    public decimal MoraRate { get; set; }
    public decimal Total { get; set; }
    public decimal Balance { get; set; }
}
