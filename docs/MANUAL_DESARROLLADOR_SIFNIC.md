# Manual de desarrollador SIFNIC

## Actualizacion 2026-05-01 - Aprobacion de credito

Se agrego una pasada funcional para aprobacion de credito desde Solicitudes:

- la bandeja de solicitudes usa grid operativo;
- el boton `Ver` abre una ventana completa de expediente, simulacion, checklist, plan y decision;
- `Resolver` acepta `APROBAR`, `RECHAZAR` y `MEJORA`;
- la aprobacion permite monto aprobado distinto al solicitado;
- la comision de desembolso se suma al capital del credito;
- el credito aprobado queda disponible en Caja para desembolso;
- se amplio `creditos.credito.estado_operativo` a `NVARCHAR(30)` desde `CreditOperationsSupport.EnsureSchema`.

Datos QA y procedimiento completo:

```text
docs/QA_APROBACION_CREDITO_2026_05_01.md
docs/qa/seed_sifnic_credit_approval_qa.ps1
```

Regla aplicada:

```text
capital del credito = monto a desembolsar + comision de desembolso financiada
```

Fecha de actualizacion: 2026-05-01

## 1. Proposito

SIFNIC es un sistema financiero interno para microfinancieras. Cubre operacion comercial, solicitudes de credito, clientes, cartera, cobranza, caja, contabilidad, RRHH, nomina, portal del colaborador, bandeja supervisor y configuracion.

Este manual resume la arquitectura actual, los modulos, la base de datos, los procedimientos de trabajo y las pruebas ejecutadas. Debe usarse como punto de entrada antes de modificar el sistema.

## 2. Proyecto ejecutable

Proyecto principal:

```text
backend/Sifnic.Api
```

Tecnologia principal:

```text
ASP.NET Core MVC
Target framework: net10.0
Servidor local usado: http://localhost:5277
```

Comandos base:

```powershell
cd C:\Users\eaespinoza\Pictures\SIFNIC\backend\Sifnic.Api
dotnet build .\Sifnic.Api.csproj -p:UseAppHost=false -o ..\..\artifacts\build-validation
dotnet run --project .\Sifnic.Api.csproj
```

Cuando el ejecutable queda bloqueado por una instancia en uso, compilar con salida alterna:

```powershell
dotnet build .\Sifnic.Api.csproj -p:UseAppHost=false -o ..\..\artifacts\build-validation-no-lock
```

## 3. Configuracion de base de datos

La conexion actual apunta a SQL Server Express:

```text
Data Source=GCPPA367ITTC\SQLEXPRESS;Initial Catalog=CREDITO;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=False;
```

Ubicaciones relevantes:

```text
backend/Sifnic.Api/appsettings.json
backend/Sifnic.Api/appsettings.Development.json
backend/Sifnic.Api/Datos/ConexionDb.cs
```

`ConexionDb.Cadena` ya soporta estas fuentes, en orden:

1. Variable de entorno `SIFNIC_CONNECTION_STRING`.
2. Variable de entorno `ConnectionStrings__Credito`.
3. Valor configurado en `appsettings`.

Recomendacion de produccion: no dejar secretos reales en codigo fuente. Usar variables de entorno, secretos administrados o configuracion por ambiente.

## 4. Inventario de base de datos

Inventario exportado:

```text
docs/db-inventory/tables.csv
docs/db-inventory/columns.csv
docs/db-inventory/procedures.csv
docs/db-inventory/schema-table-counts.csv
docs/db-inventory/foreign-keys.csv
```

Resultado de inventario:

```text
Tablas: 222
Procedimientos almacenados: 166
Llaves foraneas: 358
Columnas: 4759
Esquemas con tablas: 25
```

Conteo por esquema:

```text
administracion: 3
auditoria: 1
bancos: 7
caja: 6
captaciones: 3
clientes: 4
cobranza: 11
compras: 2
configuracion: 14
contabilidad: 16
creditos: 22
cumplimiento: 21
cxc: 6
cxp: 6
empresa: 3
inventario: 4
menu: 2
nomina: 25
operacion: 12
parametros: 9
proveedores: 1
regulatorio: 11
rrhh: 18
seguridad: 10
ventas: 5
```

Tablas principales por dominio:

```text
seguridad:
usuario, rol, usuario_rol, permiso, rol_permiso, sesion_usuario, usuario_modulo, parametro_seguridad, bitacora_acceso, solicitud_recuperacion_clave

clientes:
cliente, prospecto_cliente, historial_prospecto_cliente, solicitud_eliminacion_cliente

creditos:
solicitud_credito, credito, producto_crediticio, plan_pago_credito, pago_credito, aplicacion_pago_credito, aplicacion_pago_cuota, desembolso_credito, recibo_pago_credito, expediente_credito, documento_expediente, garantia_credito, asignacion_oficial_credito, historial_asignacion_oficial_credito, tasa_variable_credito

caja:
sesion_caja, movimiento_caja, recibo_oficial_caja, arqueo_caja, desglose_arqueo_caja, detalle_denominacion_arqueo

cobranza:
gestion_cobranza_credito, bitacora_gestion_cobranza, promesa_pago_credito, asignacion_cobranza_credito, clasificacion_cobranza, canal_gestion_cobranza, tipo_gestion_cobranza, tipo_contacto_gestion_cobranza, resultado_gestion_cobranza, estado_promesa_pago, conciliacion_promesa_pago_credito

rrhh:
empleado, contrato, cargo, departamento, estructura_organizativa_nodo, empleado_supervision, expediente_documento, accion_personal, vacacion, hora_extra, solicitud_permiso, marcacion_reloj

nomina:
periodo_nomina, nomina, nomina_detalle, nomina_detalle_concepto, concepto_nomina, parametro_nomina, liquidacion, liquidacion_detalle, esquela_pago, envio_esquela_pago, prestamo_empleado

contabilidad:
catalogo_cuenta_muc, asiento, asiento_detalle, periodo_contable, centro_costo, configuracion_asiento_transaccion, configuracion_asiento_pago_credito, cartera_contable y configuraciones regulatorias relacionadas
```

## 5. Inventario de endpoints

Inventario exportado:

```text
docs/app-inventory/routes.csv
```

Resultado:

```text
Endpoints detectados: 180
```

Conteo por controlador:

```text
AccionesPersonalController: 6
CajaController: 15
CarteraController: 9
CatalogosRrhhController: 6
ClientesController: 10
ContabilidadController: 14
ContratosController: 8
EmpleadosController: 6
EstructuraOrganizativaController: 8
ExpedientesController: 5
NominaController: 15
NovedadesController: 19
PortalController: 12
RelojController: 4
RrhhResumenController: 3
SeguridadController: 23
SolicitudesCreditoController: 17
```

Endpoints criticos:

```text
Seguridad/Login
Seguridad/Logout
Seguridad/MisModulosDashboard

Caja/Catalogos
Caja/Resumen
Caja/BuscarCreditos
Caja/AplicarPago
Caja/DesembolsarCredito
Caja/AnularPago
Caja/VoucherPagoHtml

Clientes/Catalogos
Clientes/Listar
Clientes/Obtener
Clientes/Crear
Clientes/Actualizar
Clientes/SolicitarEliminacion

Cartera/Catalogos
Cartera/Resumen
Cartera/Listar
Cartera/Obtener
Cartera/RecuperacionDiaria
Cartera/Reasignar
Cartera/Desasignar

SolicitudesCredito/Catalogos
SolicitudesCredito/Listar
SolicitudesCredito/Obtener
SolicitudesCredito/Crear
SolicitudesCredito/Actualizar
SolicitudesCredito/GenerarPlan
SolicitudesCredito/Resolver

Nomina/Contexto
Nomina/AbrirPeriodo
Nomina/Generar
Nomina/Cerrar
Nomina/GenerarLiquidacion

Portal/MiContexto
Portal/SupervisorContexto
Portal/SupervisorPendientes
Portal/ResolverSupervisorVacacion
Portal/ResolverSupervisorPermiso
Portal/ResolverSupervisorHoraExtra
```

## 6. Autenticacion y sesion

El frontend guarda la sesion en:

```text
localStorage["sifnic.session"]
```

El cliente HTTP agrega el token en:

```text
X-Session-Token
```

Archivo frontend:

```text
backend/Sifnic.Api/wwwroot/js/shared/auth-session.js
```

Tabla backend:

```text
seguridad.sesion_usuario
```

Regla de seguridad:

```text
Todo endpoint operativo debe exigir sesion. Login y vistas publicas son la excepcion.
```

Prueba ejecutada:

```text
Caja/Catalogos sin token: 401
Clientes/Catalogos sin token: 401
Cartera/Catalogos sin token: 401
SolicitudesCredito/Catalogos sin token: 401
```

## 7. Sistema visual y UX

Archivos principales:

```text
backend/Sifnic.Api/wwwroot/css/enterprise-system.css
backend/Sifnic.Api/wwwroot/css/finance-design-system.css
backend/Sifnic.Api/wwwroot/modules/creditos/assets/css/creditos.css
backend/Sifnic.Api/wwwroot/js/shared/theme.js
backend/Sifnic.Api/wwwroot/js/shared/unsaved-guard.js
```

Principios actuales:

```text
Modo claro como base.
Modo oscuro disponible como preferencia global.
Sin boton flotante de tema dentro del contenido.
Maximo tres niveles visuales: fondo, seccion, componente.
Tablas y maestro-detalle como patron operativo.
Accion primaria unica por flujo.
Acciones peligrosas con color semantico y validacion.
```

Reglas de maquetacion:

```text
Objetivo fuerte: 1366px y 1440px.
Evitar scroll horizontal.
Usar min-width: 0 en hijos flex/grid con texto.
Permitir wrap en toolbars.
Truncar textos secundarios largos.
Separar vista resumen de vista detalle cuando el contenido pesa demasiado.
```

## 8. Modulos y flujo funcional

### Dashboard

Rol:

```text
Portal operativo de entrada.
```

Debe mostrar:

```text
Operacion del dia.
Pendientes accionables.
Alertas.
Accesos frecuentes.
Catalogo de modulos.
```

### Clientes

Rol:

```text
Maestro-detalle de clientes y prospectos.
```

Reglas UX:

```text
Seleccionar fila carga resumen corto en panel derecho.
No repetir boton Ver si la seleccion ya muestra el resumen.
Eliminar requiere motivo, usuario administrador y clave administrador.
```

Validacion ejecutada:

```text
Solicitar eliminacion sin motivo/admin: 400
Solicitar eliminacion con motivo pero sin admin: 400
```

### Solicitudes de credito

Rol:

```text
Bandeja de evaluacion y expediente de credito.
```

Estructura recomendada:

```text
Bandeja: filtros, KPIs minimos, listado y resumen corto.
Expediente: cliente, credito, estado, riesgo, validaciones, documentos y decision.
Detalle tecnico: documentos, plan de pago, analisis y trazabilidad.
```

### Cartera

Rol:

```text
Gestion, seguimiento y analisis de cartera/cobranza.
```

Responsabilidades permitidas:

```text
Ver saldo y mora.
Analizar vencimiento.
Priorizar gestion.
Registrar seguimiento.
Ver estado de cuenta.
Ver plan de pago.
Asignar o desasignar oficial.
Preparar atencion en caja como accion secundaria.
```

Responsabilidad excluida:

```text
Cartera no aplica pagos. La aplicacion de pagos pertenece a Caja.
```

Arquitectura actual:

```text
Header compacto.
Barra unica de filtros.
KPIs compactos.
Segmentos: Gestion, Call center / mora, Recuperacion.
Tabla principal como zona protagonista.
Ficha operativa derecha.
Call center y recuperacion como pantallas secundarias del modulo, no como bloques apilados.
```

Archivos trabajados:

```text
backend/Sifnic.Api/Views/App/Cartera.cshtml
backend/Sifnic.Api/wwwroot/modules/creditos/cartera.js
backend/Sifnic.Api/wwwroot/css/enterprise-system.css
```

### Caja

Rol:

```text
Estacion transaccional.
```

Responsabilidades:

```text
Aplicacion de pagos.
Desembolsos.
Reimpresion de voucher con control.
Arqueo.
Apertura y cierre de caja.
```

Flujo de pago:

```text
1. Buscar credito.
2. Seleccionar credito.
3. Capturar pago.
4. Validar aplicacion automatica.
5. Confirmar.
6. Emitir comprobante.
```

Regla de aplicacion parcial:

```text
Para abono parcial se prioriza primero interes, luego mora y de ultimo capital.
```

### RRHH

Rol:

```text
Centro operativo de capital humano.
```

Separacion tecnica importante:

```text
rrhh.estructura_organizativa_nodo = estructura formal.
rrhh.empleado_supervision = flujo operativo de aprobaciones.
```

No mezclar esas dos responsabilidades.

### Nomina

Rol:

```text
Motor de periodo, procesamiento, cierre, liquidacion y reportes.
```

Flujo recomendado:

```text
Configurar.
Abrir periodo.
Registrar variables.
Procesar.
Revisar incidencias.
Cerrar.
Generar reportes/liquidaciones.
```

## 9. Pruebas ejecutadas el 2026-05-01

### Prueba funcional y validaciones de datos

Se ejecuto una sesion QA temporal con `admin.sisfnic` y token tecnico para validar endpoints.

Checks ejecutados:

```text
App Dashboard carga vista: 200
App Cartera carga vista reconstruida: 200
CSS enterprise disponible: 200
JS cartera disponible: 200
Caja catalogos bloquea sin token: 401
Clientes catalogos bloquea sin token: 401
Cartera catalogos bloquea sin token: 401
Solicitudes catalogos bloquea sin token: 401
Clientes catalogos con token: 200
Cartera resumen con token: 200
Cartera listar con token: 200
Caja catalogos con token: 200
Caja resumen con token: 200
Solicitudes catalogos con token: 200
Login credenciales dummy: 401
Validacion eliminar cliente sin motivo/admin: 400
Validacion eliminar cliente sin admin: 400
```

Resultado:

```text
17/17 OK
```

### Prueba de maquetacion y recorrido

Se ejecuto recorrido headless a 1366x768 sobre:

```text
Dashboard
Clientes
SolicitudesCredito
Cartera
Caja
Configuracion
Rrhh
MiPortal
Nomina
```

Artefactos:

```text
artifacts/layout-qa-live3/*.png
artifacts/layout-qa-live3/layout-report.csv
```

Resultado medido:

```text
No se detecto scroll horizontal en 1366px para las pantallas recorridas.
No se detecto boton flotante global de tema tapando contenido.
Se corrigio contraste de sidebar en Caja despues del recorrido.
```

Limitacion:

```text
El runtime Node del plugin de navegador fallo con "Acceso denegado"; se uso Microsoft Edge headless via DevTools Protocol como alternativa.
```

## 10. Procedimiento para agregar o modificar un modulo

1. Definir responsabilidad del modulo.
2. Confirmar si es vista operativa, bandeja, expediente, wizard o dashboard.
3. Reutilizar componentes del sistema visual.
4. Separar vista resumen de detalle completo si la pantalla se carga demasiado.
5. Proteger endpoints con sesion.
6. Validar entradas en backend antes de tocar datos.
7. No exponer `ex.Message` al cliente en produccion.
8. Registrar bitacora operativa si cambia datos sensibles.
9. Compilar con salida alterna si el apphost esta bloqueado.
10. Ejecutar prueba funcional y prueba visual a 1366px.

## 11. Procedimiento para cambios en base de datos

1. Identificar esquema y tabla en `docs/db-inventory/tables.csv`.
2. Revisar columnas en `docs/db-inventory/columns.csv`.
3. Revisar llaves foraneas en `docs/db-inventory/foreign-keys.csv`.
4. Revisar procedimientos relacionados en `docs/db-inventory/procedures.csv`.
5. Crear script idempotente cuando sea posible.
6. Evitar cambios destructivos sin respaldo.
7. Agregar migracion o script versionado.
8. Actualizar este manual si cambia el modelo relevante.

## 12. Procedimiento de QA minimo antes de entregar

Backend:

```powershell
dotnet build .\backend\Sifnic.Api\Sifnic.Api.csproj -p:UseAppHost=false -o .\artifacts\build-validation
```

HTTP:

```text
GET /App/Login
POST /Seguridad/Login con credenciales invalidas
GET /Caja/Catalogos sin token
GET /Clientes/Catalogos sin token
GET /Cartera/Catalogos sin token
GET /SolicitudesCredito/Catalogos sin token
GET endpoints criticos con token QA
```

Visual:

```text
1366x768
1440x900
Sin scroll horizontal
Sin solapes
Sin botones flotantes tapando contenido
Tabla y panel derecho legibles
Accion principal clara
```

## 13. Comentarios y documentacion en codigo

Criterio aplicado:

```text
No comentar literalmente cada linea.
Comentar bloques donde la intencion de producto o arquitectura no es obvia.
Preferir nombres claros y comentarios que expliquen por que existe una decision.
Evitar comentarios vacios que repiten lo que ya dice el codigo.
```

Bloques comentados en esta pasada:

```text
Cartera.cshtml: shell principal, filtros, segmentos y area maestro-detalle.
cartera.js: pantalla activa y control de vistas gestion/mora/recuperacion.
enterprise-system.css: contraste propio del sidebar de Caja.
```

## 14. Deuda tecnica priorizada

1. Reducir controladores monoliticos.
2. Estandarizar respuestas de error sin exponer mensajes internos.
3. Mover reglas de negocio a servicios.
4. Crear pruebas automatizadas reales.
5. Formalizar migraciones de base de datos.
6. Mantener un solo sistema visual para todos los modulos.
7. Reemplazar pruebas manuales por suite de humo automatizada.

## 15. Archivos de referencia rapida

```text
CONTEXTO_CONTINUIDAD_SIFNIC.md
docs/MANUAL_DESARROLLADOR_SIFNIC.md
docs/db-inventory/tables.csv
docs/db-inventory/columns.csv
docs/db-inventory/procedures.csv
docs/db-inventory/foreign-keys.csv
docs/app-inventory/routes.csv
backend/Sifnic.Api/Program.cs
backend/Sifnic.Api/Datos/ConexionDb.cs
backend/Sifnic.Api/wwwroot/js/shared/auth-session.js
backend/Sifnic.Api/wwwroot/js/shared/theme.js
backend/Sifnic.Api/wwwroot/js/shared/unsaved-guard.js
backend/Sifnic.Api/wwwroot/css/enterprise-system.css
```
