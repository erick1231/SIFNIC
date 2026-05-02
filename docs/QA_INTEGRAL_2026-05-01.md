# QA integral SIFNIC - 2026-05-01

## Alcance

Prueba realizada como usuario comun y como QA funcional sobre los modulos principales de SIFNIC:

- Dashboard.
- Clientes.
- Solicitudes de credito.
- Caja.
- Cartera.
- Contabilidad.
- Nomina.
- RRHH.
- Configuracion.
- Mi Portal.

Tambien se contrasto contra el contexto revisado de Servicredito/FIOL/SIAF y los archivos externos del bloque FIOL/CONAMI ya mapeados en `docs/MAPEO_FIOL_CONAMI_CORE.md`.

## Evidencia tecnica

- Build limpio ejecutado:
  - `dotnet build backend\Sifnic.Api\Sifnic.Api.csproj -p:UseAppHost=false -o backend\Sifnic.Api\artifacts\tmp-build-integral-qa`
  - Resultado: correcto, 0 errores, 0 advertencias.
- Sistema probado en:
  - `http://localhost:5277`
- Sesion QA usada:
  - `admin.sisfnic`
- Capturas generadas:
  - `tmp-qa-dashboard-1366.png`
  - `tmp-qa-clientes-1366.png`
  - `tmp-qa-solicitudes-1366.png`
  - `tmp-qa-caja-1366.png`
  - `tmp-qa-cartera-1366.png`
  - `tmp-qa-contabilidad-1366.png`
  - `tmp-qa-nomina-1366.png`
  - `tmp-qa-rrhh-1366.png`
  - `tmp-qa-configuracion-1366.png`
  - `tmp-qa-portal-1366.png`

## Base revisada

Conteos principales al momento de prueba:

- Clientes: 13.
- Solicitudes de credito: 9.
- Creditos: 18.
- Empleados: 35.
- Cuentas contables activas: 1,719.
- Asientos contables: 15.

Asignacion de cartera:

- `batof1`: 2 creditos.
- `batof2`: 1 credito.
- `oficial.credito`: 12 creditos.

La separacion de cartera por oficial respondio: `batof1` y `batof2` ven carteras distintas, mientras el administrador ve la cartera global.

## Lo que funciono

### Seguridad y Dashboard

- Dashboard carga correctamente.
- `Seguridad/MisModulosDashboard` responde con modulos habilitados.
- La navegacion principal llega a los modulos probados.

### Clientes

- Catalogos cargan.
- Listado carga.
- Obtener cliente existente funciona.
- Crear cliente sin datos responde con mensaje claro:
  - `Corrige los datos del cliente.`
  - Campos: nombres, apellidos, identificacion.

### Solicitudes de credito

- Catalogos cargan.
- Listado carga.
- Buscar cliente inexistente responde `found=false`.
- Generar plan invalido responde con errores entendibles.
- Generar plan valido genera cuotas con capital, interes, comision y saldo.

### Cartera

- Resumen admin carga.
- Listado admin carga.
- Listado por oficial respeta cartera asignada.
- Clasificacion regulatoria CONAMI al 2026-04-30 responde.
- Recuperacion diaria responde estructurada, aunque sin datos esperados para 2026-04-30.

### Caja

- Catalogos cargan.
- Busqueda de creditos carga.
- Pago invalido responde con mensaje claro:
  - `Corrige los datos del pago.`
- Voucher de pago renderiza:
  - Dos copias.
  - Marca de reimpresion cuando corresponde.
  - Desglose de pago.

### Contabilidad

- Catalogos cargan.
- Resumen carga.
- Balance general carga.
- Cuenta inexistente responde:
  - `Cuenta contable no encontrada.`

### Nomina y RRHH

- Nomina carga.
- Contexto de nomina carga.
- Abrir periodo con fechas invalidas responde mensaje claro.
- Generar nomina sin periodo responde mensaje claro.
- RRHH catalogos y listado de empleados cargan.
- Permiso invalido responde con mensaje claro.

## Fallas encontradas

### P0 - Caja acepta apertura con monto negativo

Prueba:

- `Caja/AbrirSesion`
- Payload con `openingNio=-10`.

Resultado:

- El sistema respondio `Sesion de caja abierta.`
- Creo sesion de caja.

Impacto:

- En produccion esto permite abrir caja con saldo negativo.
- Rompe cuadre, auditoria y control de efectivo.

Correccion requerida:

- Validar `OpeningNio >= 0` y `OpeningUsd >= 0`.
- Validar tambien cantidades negativas en desglose.
- El mensaje debe decir algo como:
  - `El monto de apertura no puede ser negativo.`

### P0 - Arqueo no calcula diferencia correctamente

Prueba:

- Apertura NIO 100.
- Pago NIO 1.
- Teorico mostrado por API: NIO 101.
- Fisico digitado: NIO 100.

Resultado:

- El sistema respondio:
  - `Arqueo generado sin diferencias.`
  - `differenceNio = 0`.

Impacto:

- El cajero podria cerrar con faltantes sin alerta.
- El reporte de cuadre queda incorrecto.

Correccion requerida:

- Diferencia debe ser `fisico - teorico`.
- En el caso probado debio ser `-1.00`.
- La UI debe resaltar `FALTANTE` o `SOBRANTE`.

### P0 - Contabilidad no cuadra debito/credito

En `Contabilidad/Resumen?fecha=2026-04-30`:

- Debitos: 94,609.94.
- Creditos: 91,869.94.
- Diferencia: 2,740.00.

Impacto:

- El sistema contable no puede pasar a cierre operativo si hay asientos descuadrados.

Correccion requerida:

- Bloquear cierre contable con diferencia.
- Marcar asiento origen que descuadra.
- Completar plantillas automaticas de caja, cartera, desembolso y nomina.

### P1 - Anulacion de pago queda bloqueada despues de cerrar caja

Prueba:

- Se aplico pago QA de NIO 1.
- Se cerro caja.
- Se intento anular el voucher.

Resultado:

- `Debe abrir caja antes de anular un voucher.`
- Luego con caja abierta:
  - `Solo se pueden anular vouchers de la sesion de caja abierta.`

Impacto:

- Operativamente puede ser correcto que no se anule libremente una caja cerrada, pero falta flujo alterno.
- Debe existir reversa controlada con autorizacion, asiento inverso, bitacora y afectacion de caja/contabilidad.

Nota QA:

- Se hizo limpieza directa del pago de prueba porque la API no permitio revertirlo despues del cierre.

### P1 - Nomina no cubre todos los escenarios solicitados

Conceptos existentes:

- `COMISION`
- `OTRO_DEVENGADO`
- `DEDUCCION_FIJA`
- `PRESTAMO`
- `FONDO_AHORRO`
- `INSS_LABORAL`
- `IR_LABORAL`

Faltantes especificos:

- `COMISION_COLOCACION`
- `EMBARGO_JUDICIAL`
- `PENSION_ALIMENTICIA`

Tambien falta una pantalla/API clara para capturar movimientos variables de nomina:

- Pago de comisiones por colocaciones.
- Otros devengados.
- Deducciones manuales.
- Embargo judicial.
- Pension alimenticia.

Correccion requerida:

- Agregar catalogo de conceptos parametrizables.
- Agregar CRUD de movimientos variables por empleado y periodo.
- Marcar si afecta INSS, IR, neto, patronal y contabilidad.
- Agregar validaciones por tope y autorizacion.

### P1 - Errores tecnicos visibles para usuario

Algunas pruebas invalidas respondieron con formato tecnico de ASP.NET:

- `The model field is required.`
- `The JSON value could not be converted...`

Casos:

- Hora extra con fecha invalida.
- Liquidacion con payload mal formado.

Impacto:

- El usuario comun no entiende el error.

Correccion requerida:

- Desactivar respuesta cruda de model binding para estos endpoints.
- Normalizar a mensajes de negocio:
  - `Ingresa una fecha valida.`
  - `La cantidad de horas debe ser mayor que cero.`

### P1 - Liquidacion acepta causal desconocida

Prueba:

- `causalCodigo = DESCONOCIDA`.

Resultado:

- El endpoint respondio `ok=true` y genero previsualizacion.

Impacto:

- Puede calcular una liquidacion con causal no parametrizada.

Correccion requerida:

- Validar causal contra catalogo activo.
- Si no existe:
  - `Selecciona una causal de liquidacion valida.`

### P1 - UI con desbordes internos en tablas

La pagina completa no tiene scroll horizontal global, pero se detectaron elementos fuera del viewport dentro de paneles:

- Clientes: tabla de plan/estado de cuenta del panel derecho llega fuera del ancho.
- Solicitudes: tabla de plan de pago del panel derecho llega fuera del ancho.
- Cartera: tabla principal supera el contenedor visible.

Correccion requerida:

- Contenedores internos con scroll horizontal propio o columnas compactas.
- En pantallas de 1366x768, no debe quedar texto cortado ni tabla escondida tras panel derecho.

### P2 - Cabecera muestra "Sin rol"

En varias pantallas la cabecera muestra:

- `Administrador SISFNIC Sin rol`

Pero la sesion tiene rol:

- `ADMINISTRADOR`

Impacto:

- Confunde a usuario y soporte.

Correccion requerida:

- Usar `roles[0]` o descripcion de rol desde sesion.

### P2 - Dashboard tiene recurso 404

La consola registro:

- `Failed to load resource: the server responded with a status of 404`

Correccion requerida:

- Identificar asset faltante desde DevTools/logs.
- Corregir ruta o eliminar referencia.

### P2 - Contabilidad sigue demasiado larga

La pagina de contabilidad carga, pero `scrollHeight` fue 24,564 px.

Impacto:

- La pantalla se siente como un documento largo, no como modulo operativo.

Correccion requerida:

- Separar en vistas/tabs reales:
  - Catalogo MUC.
  - Asientos.
  - Periodos.
  - Plantillas automaticas.
  - Reportes.

### P2 - Mi Portal no vincula usuario admin a ficha

Mensaje:

- `Tu usuario aun no esta vinculado`

Esto puede ser correcto para admin, pero si el usuario necesita autoservicio debe existir vinculacion empleado-usuario.

## Comparacion contra Servicredito/FIOL/SIAF y archivos externos

Falta llevar a core parametrizable:

- Productos crediticios FIOL con rangos, periodicidades, tasas y cargos.
- TCEA/TIR tipo XIRR.
- Matriz DDC completa y alertas UAF001-UAF008.
- Catalogos PRIM/ICC, actividades economicas y geografia.
- Operaciones de caja parametrizadas contra contabilidad.
- Nomina con conceptos variables y deducciones legales/administrativas.

## Prioridad recomendada

1. Corregir caja:
   - Apertura negativa.
   - Diferencia de arqueo.
   - Reversa de pagos de caja cerrada con autorizacion.
2. Corregir contabilidad:
   - Asientos descuadrados.
   - Plantillas automaticas desde caja/cartera/nomina.
3. Completar nomina:
   - Comision por colocacion.
   - Otros devengados.
   - Deducciones.
   - Embargo judicial.
   - Pension alimenticia.
4. Normalizar errores tecnicos:
   - Nunca mostrar errores crudos de model binding.
5. Ajustar UI:
   - Paneles derechos y tablas internas.
   - Rol en cabecera.
   - Reorganizar contabilidad por secciones.

