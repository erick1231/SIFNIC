# Mapeo FIOL/CONAMI contra SIFNIC

Fecha de revision: 2026-05-01

Este documento resume la revision de archivos externos de FIOL/Servicredito/CONAMI y donde deben integrarse en SIFNIC. No es un manual de usuario; es una guia tecnica de continuidad para implementar sin duplicar cosas que ya existen.

## Archivos revisados

- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\PLANPAGOS TCEA  SISTEMA FIOL DIARIO.xlsx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\Politicas de Creditos apegados a CONAMI  by Carlos Garcia Leiva  actualizado 18abr2024 v001.docx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\Manual de parametrizaciones del sistema , Tipos y Otros.xlsx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\Manual de producto crediticio.docx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\MANUAL Formula de calculos V20.docx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\Manual FORMULA DE CALCULOS.docx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\MATRIZ DE ALERTAS TEMPRANAS Y DDC 18marzo2023.xlsx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\MATRIZ DE ALERTAS TEMPRANAS Y DDC V003.xlsx`
- `C:\Users\eaespinoza\OneDrive - UNI\ProyectoPrestamos\Nueva carpeta\planilla en excel v001.xlsx`

## Estado actual encontrado en SIFNIC

Ya existe base fuerte para:

- Clientes: `clientes.cliente`, `clientes.prospecto_cliente`, expediente, riesgo, PEP, origen de fondos y relacion comercial.
- Solicitudes: `creditos.solicitud_credito` con producto textual, monto, plazo, tasa, capacidad, checklist, prospeccion, visitas, referencias y consulta central.
- Plan de pago: `creditos.plan_pago_credito` con capital, interes, comision, mora, dias de interes y deslizamiento programado.
- Caja: pago, voucher, desglose de abonos, anulacion, arqueo, desembolso y multimoneda.
- Cartera: `creditos.credito`, asignacion de oficial, vista de cartera, tasa variable y cierre regulatorio.
- Cumplimiento: expediente PLA, KYC, listas, alertas, casos y parametros AML.
- Regulatorio: `regulatorio.conami_norma`, `regulatorio.conami_regla`, `regulatorio.regla_clasificacion`, `regulatorio.regla_provision`, cierre de cartera.
- Contabilidad: catalogo MUC, asientos, periodo, configuracion de asientos y reporteria base.

Conteos revisados en base:

- `cumplimiento.matriz_alerta_temprana`: 5 filas genericas.
- `cumplimiento.parametro_alerta_aml`: 2 parametros.
- `regulatorio.conami_regla`: 25 reglas.
- `regulatorio.regla_clasificacion`: 5 reglas.
- `regulatorio.regla_provision`: 5 reglas.

## Brechas principales

### 1. Producto crediticio FIOL

El manual de producto crediticio describe un dossier de producto con:

- Sector economico.
- Codigo generado automaticamente.
- Descripcion, detalle, condiciones y destino.
- Periodicidades habilitadas.
- Rangos por producto con monto minimo, monto maximo, tasa corriente, tasa comision, tasa mora, cargo por atraso, cargo por desembolso y estado.

En SIFNIC solo existe `creditos.solicitud_credito.producto_credito` como texto. Falta el catalogo formal.

Agregar:

- `creditos.producto_crediticio`
- `creditos.producto_crediticio_frecuencia`
- `creditos.producto_crediticio_rango`
- `creditos.producto_crediticio_cargo`
- CRUD en Configuracion o Creditos/Productos.
- En solicitud: seleccionar producto y rango para autocompletar tasa, comision, mora, plazo, monto minimo/maximo y frecuencia.

### 2. TCEA/TIR no periodica

El Excel `PLANPAGOS TCEA SISTEMA FIOL DIARIO.xlsx` trae flujos de caja por credito y usa `XIRR` sobre montos y fechas. El flujo inicial va negativo y las cuotas positivas. Esto no esta en la base.

Hoy SIFNIC calcula plan con interes sobre saldo por dias reales base 360, comision prorrateada y deslizamiento programado, pero no persiste:

- TCEA.
- TIR no periodica.
- Flujo de caja usado para la TCEA.
- Costos incluidos en TCEA.

Agregar:

- Campos en `creditos.solicitud_credito`: `tcea_anual`, `tir_no_periodica`, `costo_total_credito`, `flujo_tcea_json`.
- Campos en `creditos.credito`: `tcea_anual`, `tir_no_periodica`, `costo_total_credito`.
- Funcion C# en `CreditOperationsSupport` para calcular XIRR/TCEA.
- Mostrar TCEA en simulador, solicitud, expediente y plan de pago.

### 3. Formula de cobro y mora diaria

El manual de formulas V20 confirma prelacion:

1. Mora.
2. Intereses.
3. Comisiones.
4. Deslizamientos.
5. Capital.

SIFNIC ya aplica `MORA`, `INTERES`, `COMISION`, `CAPITAL`, y si sobra aplica `CAPITAL_ANTICIPADO`.

Falta completar:

- Rubro `DESLIZAMIENTO` real en `creditos.aplicacion_pago_cuota`.
- Calculo de mora real al momento del pago: `capital_moroso * tasa_mora / 100 * dias_mora / 360`.
- Actualizacion diaria de interes corriente por dias reales al cobro, no solo interes programado.
- Diferencial cambiario real si el credito/pago cruza moneda.

Ubicacion:

- `CajaController.ApplyPaymentToSchedule`
- `CreditOperationsSupport`
- `creditos.plan_pago_credito`
- `creditos.aplicacion_pago_cuota`

### 4. Politicas de credito CONAMI

El documento de politicas trae reglas que estan parcialmente implementadas:

- Sujetos y no sujetos de credito.
- Edad minima 21 y limite operativo 65.
- Negocio con antiguedad minima.
- Referencias vecinales, comerciales y financieras.
- Capacidad de pago considerando obligaciones externas.
- Gradualidad de montos y plazos.
- Monto minimo y maximo por producto.
- Comite por niveles: sucursal, regional/jefe, gerente negocios, ejecutivo.
- Clasificacion y provisiones por tipo de cartera.
- Saneamiento por tipo de credito.

En SIFNIC ya existe parte en `regulatorio.conami_regla`, checklist de solicitud, capacidad y comite booleano, pero falta modelarlo como politica parametrizable.

Agregar:

- `creditos.politica_credito`
- `creditos.politica_credito_regla`
- `creditos.comite_credito_nivel`
- `creditos.comite_credito_facultad`
- `creditos.excepcion_politica_credito`
- `creditos.historial_excepcion_credito`

Tambien ajustar reglas:

- `SOL_EDAD_MINIMA`
- `SOL_EDAD_MAXIMA`
- `SOL_NEGOCIO_MIN_MESES`
- `SOL_CAPACIDAD_CUOTA_VECES_MIN`
- `SOL_INCREMENTO_MONTO_MAX_PCT`
- `SOL_MONTO_MINIMO`
- `SOL_MONTO_MAXIMO_PIB_VECES`
- `SOL_TASA_MORA_FACTOR`

### 5. Clasificacion CONAMI por tipo de cartera

La politica tiene tablas distintas de clasificacion:

- Microcreditos/personales/hipotecarios: cortes por dias de mora.
- CDE: cortes mas amplios.
- Bienes/deudores por venta de bienes: otra clasificacion.
- Saneamiento por tipo: microcredito 360, personales 181, vivienda 360, CDE 360.

SIFNIC tiene `regulatorio.regla_clasificacion` y `regulatorio.regla_provision`, pero la version actual es una matriz base A-E unica.

Agregar:

- Versiones/reglas por `tipo_agrupacion` o producto regulatorio.
- Tabla de saneamiento: `regulatorio.regla_saneamiento_credito`.
- Parametro de tipo CONAMI en producto crediticio.
- Ajuste del procedimiento `regulatorio.usp_calcular_cartera_conami_sifnic` para seleccionar regla por tipo de cartera/producto.

### 6. Parametrizaciones FIOL/ICC/PRIM

El Excel de parametrizaciones trae hojas:

- `catalogos_General`
- `ICC_CREDITO`
- `ICC_LINEA_CREDITO`
- `ICC_PERSONA`
- `ICC_CREDITO_PERSONA`
- `ICC_ANALISTA`
- `ICC_OBLIGACION`
- `ICC_RECUPERACIONES`
- `ICC_COLOCACIONES`
- `ICC_ADJUDICACIONES`
- `ICC_TRX_OBLIGACIONES`
- `Reg_Dep_Mun`
- `id_actividad_economica`
- `EstadosAdministrativos`
- `excepciones`
- `cargos`

SIFNIC aun no tiene un diccionario ICC formal ni catalogos de municipio/actividad economica regulatoria.

Agregar:

- `configuracion.catalogo_general`
- `configuracion.catalogo_general_valor`
- `configuracion.municipio_conami`
- `configuracion.actividad_economica_conami`
- `regulatorio.icc_tabla`
- `regulatorio.icc_campo`
- `regulatorio.icc_mapeo_campo`
- Vistas `reportes.vw_icc_credito`, `reportes.vw_icc_persona`, `reportes.vw_icc_recuperaciones`, `reportes.vw_icc_colocaciones`.

### 7. Matriz DDC y alertas tempranas UAF

Las matrices traen:

- DDC con componentes, subcomponentes, descripcion, valor de riesgo y alerta de expulsion.
- Alertas UAF001-UAF008 en version 2023.
- Alertas UAF001-UAF007 y UAF008 en version V003, con descripcion y excepciones.

SIFNIC tiene estructura AML, pero solo 5 alertas genericas y no tiene la matriz DDC completa.

Agregar o completar:

- `cumplimiento.matriz_ddc_componente`
- `cumplimiento.matriz_ddc_subcomponente`
- `cumplimiento.evaluacion_ddc_cliente`
- `cumplimiento.evaluacion_ddc_respuesta`
- Cargar alertas UAF001-UAF008 en `cumplimiento.matriz_alerta_temprana`.
- Agregar parametros AML para umbrales: USD 5,000 efectivo, USD 3,000 divisas, pagos superiores a ingresos, multiples creditos en 6 meses, abonos frecuentes, cancelacion anticipada.

### 8. Caja operaciones

El Excel trae `CAJA_OPERACIONES` con codigo, descripcion, tipo, cuentas de caja dolar/cordoba y contracuenta.

SIFNIC tiene caja operativa y contabilidad base, pero falta una tabla parametrica de operaciones de caja enlazada a asientos.

Agregar:

- `caja.tipo_operacion_caja`
- `caja.tipo_operacion_caja_cuenta`
- Mapeo con `contabilidad.configuracion_asiento_transaccion`.

### 9. Nomina/planilla contable

El Excel de planilla trae auxiliar contable con cuentas y detalle de movimiento. SIFNIC ya tiene nomina y contabilidad, pero el flujo de contabilizacion de planilla debe conectarse con plantillas de asiento.

Agregar despues del core de credito/caja:

- Plantilla contable de nomina.
- Generacion automatica de asiento desde nomina cerrada.
- Vista auxiliar de asiento por concepto y empleado.

## Orden recomendado de implementacion

1. Productos crediticios y rangos FIOL.
2. TCEA/XIRR y costos totales en simulador, solicitud y credito.
3. Politicas de credito parametrizables: sujetos, no sujetos, edad, capacidad, gradualidad, comite y excepciones.
4. Matriz DDC y alertas UAF001-UAF008.
5. Catalogos regulatorios: actividad economica, municipios, estados, excepciones, cargos.
6. Diccionario ICC/PRIM y vistas de exportacion.
7. Caja operaciones parametrica contra contabilidad.
8. Clasificacion CONAMI por tipo de cartera y saneamiento.

## Regla de arquitectura

No duplicar logica en pantallas. La regla debe vivir en base parametrizada y helpers C#:

- Catalogos generales: `configuracion`.
- Politicas de credito: `creditos`.
- Riesgo/CONAMI: `regulatorio`.
- PLA/FT/DDC/UAF: `cumplimiento`.
- Operacion de caja: `caja`.
- Asientos: `contabilidad`.

Las pantallas deben consumir estos catalogos para reducir digitacion y evitar errores de usuario.
