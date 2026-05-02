# SIFNIC - QA aprobacion de credito y cartera

Fecha: 2026-05-01

## Objetivo

Este paquete deja datos y flujo de prueba para evaluar:

- bandeja de solicitudes de credito;
- apertura de expediente completo desde el boton `Ver`;
- decision de aprobar, rechazar o solicitar mejora;
- monto aprobado distinto al solicitado;
- comision de desembolso financiada dentro del credito;
- paso a Caja para desembolso;
- cartera con 200 creditos distribuidos por estado;
- expedientes con documentos;
- metas y comisiones de nomina para oficiales de credito y recuperadores.

## Script ejecutable

Script:

```powershell
docs\qa\seed_sifnic_credit_approval_qa.ps1
```

Ejecucion:

```powershell
powershell -ExecutionPolicy Bypass -File .\docs\qa\seed_sifnic_credit_approval_qa.ps1
```

El script es idempotente por prefijos QA:

- usuarios: `qa.*`
- clientes: `QA-CLI-2026-*`
- solicitudes de cartera: `QA-SOL-2026-*`
- solicitudes de aprobacion: `QA-EVAL-SOL-2026-*`
- creditos: `QA-CRD-2026-*`
- expedientes: `QA-EXP-*`

## Usuarios QA

Clave temporal para todos:

```text
SifnicQA2026!
```

| Usuario | Rol | Modulos principales |
|---|---|---|
| `qa.admin` | Administrador | Todo QA operativo |
| `qa.gerente.credito` | Gerente credito | Clientes, creditos, simulador, cobranza, bandeja supervisor |
| `qa.jefe.credito` | Jefe credito | Clientes, creditos, simulador, cobranza, bandeja supervisor |
| `qa.supervisor.cobranza` | Supervisor | Clientes, cobranza, bandeja supervisor |
| `qa.caja01` | Cajero | Caja, clientes |
| `qa.caja02` | Cajero | Caja, clientes |
| `qa.oficial01` | Oficial credito | Clientes, creditos, simulador, cobranza |
| `qa.oficial02` | Oficial credito | Clientes, creditos, simulador, cobranza |
| `qa.oficial03` | Oficial credito | Clientes, creditos, simulador, cobranza |
| `qa.oficial04` | Oficial credito | Clientes, creditos, simulador, cobranza |
| `qa.oficial05` | Oficial credito | Clientes, creditos, simulador, cobranza |
| `qa.oficial06` | Oficial credito | Clientes, creditos, simulador, cobranza |
| `qa.recuperador01` | Cuentas por cobrar | Clientes, cobranza, CxC |
| `qa.recuperador02` | Cuentas por cobrar | Clientes, cobranza, CxC |
| `qa.nomina` | Administracion | Nomina, RRHH, Mi portal |

## Datos sembrados

Conteos verificados:

| Entidad | Cantidad |
|---|---:|
| Usuarios QA | 15 |
| Clientes QA | 200 |
| Creditos QA | 200 |
| Solicitudes para bandeja de aprobacion | 25 |
| Expedientes QA | 200 |
| Documentos de expediente QA | 600 |
| Metas variables de nomina | 8 |

Distribucion de creditos QA:

| Estado operativo | Cantidad |
|---|---:|
| `VI` vigente | 100 |
| `VE` vencido | 20 |
| `PR` prorrogado | 20 |
| `RR` refinanciado/reestructurado | 20 |
| `SA` saneado | 20 |
| `CA` cancelado | 20 |

## Flujo de aprobacion implementado

1. En `Solicitudes de credito`, la bandeja usa tarjetas tipo grid.
2. Seleccionar una tarjeta carga un resumen corto en el panel derecho.
3. El boton `Ver` abre una ventana amplia con:
   - resumen de cliente;
   - solicitud;
   - estado y riesgo;
   - simulacion;
   - checklist;
   - primeras cuotas del plan;
   - panel de decision.
4. Desde esa ventana se puede:
   - aprobar y enviar a Caja;
   - solicitar mejora;
   - rechazar.
5. Si se aprueba:
   - el backend calcula la comision de desembolso;
   - suma la comision al capital del credito;
   - genera el credito aprobado;
   - deja el credito visible para Caja en desembolso.

Regla de comision:

```text
monto a desembolsar = monto aprobado por comite
comision financiada = monto a desembolsar * tasa_comision_ascc
capital del credito = monto a desembolsar + comision financiada
```

Ejemplo validado:

```text
monto a desembolsar: NIO 30,650.00
comision financiada: NIO 1,532.50
capital del credito: NIO 32,182.50
```

## Tablas usadas por la semilla

Seguridad:

- `seguridad.usuario`
- `seguridad.rol`
- `seguridad.usuario_rol`
- `seguridad.usuario_modulo`

Clientes y credito:

- `clientes.cliente`
- `creditos.solicitud_credito`
- `creditos.aprobacion_solicitud_credito`
- `creditos.credito`
- `creditos.plan_pago_credito`
- `creditos.asignacion_oficial_credito`
- `creditos.expediente_credito`
- `creditos.documento_expediente`
- `parametros.tipo_documento_expediente`

RRHH y nomina:

- `rrhh.empleado`
- `rrhh.contrato`
- `rrhh.departamento`
- `rrhh.cargo`
- `rrhh.estado_empleado`
- `rrhh.tipo_contrato`
- `rrhh.horario_laboral`
- `nomina.tipo_esquema_variable`
- `nomina.concepto_nomina`
- `nomina.esquema_variable_empleado`
- `nomina.regla_esquema_variable`
- `nomina.meta_variable_empleado`
- `nomina.movimiento_variable_empleado`

Archivos:

- `backend/Sifnic.Api/wwwroot/uploads/expedientes/qa/DOC_ID.pdf`
- `backend/Sifnic.Api/wwwroot/uploads/expedientes/qa/SOL_CRED.pdf`
- `backend/Sifnic.Api/wwwroot/uploads/expedientes/qa/PLAN_PAGO.pdf`
- `backend/Sifnic.Api/wwwroot/uploads/expedientes/qa/foto_colaborador.png`

## Procedimiento de prueba

1. Iniciar sesion como `qa.jefe.credito`.
2. Entrar a Solicitudes de credito.
3. Filtrar `QA-EVAL`.
4. Seleccionar una tarjeta.
5. Presionar `Ver`.
6. Revisar simulacion, checklist y plan.
7. Cambiar `Monto a desembolsar`.
8. Presionar `Aprobar y enviar a caja`.
9. Iniciar sesion como `qa.caja01`.
10. Entrar a Caja, subflujo de desembolsos.
11. Buscar el credito generado.
12. Validar que el monto aprobado es el capital financiado.

## Pruebas ejecutadas

Build:

```text
dotnet build backend/Sifnic.Api/Sifnic.Api.csproj -p:UseAppHost=false -o artifacts/build-validation-solicitudes-approval-3
Resultado: 0 errores, 0 advertencias.
```

Smoke HTTP sobre build nuevo en `http://localhost:5288`:

```text
Login qa.jefe.credito: 200
SolicitudesCredito/Listar QA-EVAL COMITE: 200
SolicitudesCredito/Obtener: 200
SolicitudesCredito/Resolver MEJORA: 200
SolicitudesCredito/Resolver APROBAR: 200
Caja/BuscarDesembolsos para credito aprobado: 200
```

Resultado clave:

```json
{
  "status": "APROBADA",
  "creditId": 229,
  "creditNumber": "CRD-2026-000007",
  "approvedBaseAmount": 30650.00,
  "commissionAmount": 1532.50,
  "financedAmount": 32182.50,
  "disbursementUrl": "/App/Caja?credito=229"
}
```

## Cambios tecnicos relevantes

- Se agrego estado de solicitud `MEJORA`.
- El endpoint `SolicitudesCredito/Resolver` acepta:
  - `approvedAmount`;
  - `approvedTermMonths`;
  - `approvedAnnualRate`.
- La aprobacion ya no usa siempre el monto solicitado.
- El credito se genera con capital financiado: monto aprobado mas comision.
- El plan se genera sobre el capital financiado y no duplica la comision como cuota separada.
- Se amplio `creditos.credito.estado_operativo` a `NVARCHAR(30)` desde `EnsureSchema`, porque la base tenia longitud 5 y no soportaba estados como `APROBADO`.

## Base de datos revisada y no usada en esta semilla

El inventario actual tiene 222 tablas y 166 procedimientos. Para esta semilla no se usaron, entre otros:

- captaciones y productos de ahorro;
- bancos y conciliaciones;
- inventario y ventas;
- compras, CxP y flujos de proveedores;
- contabilidad regulatoria completa;
- dispensa de credito;
- prorroga, refinanciamiento y reestructuracion con documentos formales;
- UAF/cumplimiento avanzado;
- garantias detalladas por bien;
- personas vinculadas del credito;
- cierres regulatorios;
- reporteria CONAMI profunda;
- procesos completos de liquidacion de nomina.

## Brechas detectadas

1. Falta separar formalmente `monto entregado al cliente` y `capital financiado` en el modelo de desembolso. Hoy Caja ve el capital aprobado financiado; para produccion conviene que el comprobante muestre ambos valores.
2. Falta una tabla de condiciones aprobadas por comite con versionado: monto solicitado, monto aprobado, comision financiada, plazo, tasa, observacion y usuario aprobador.
3. Los documentos de expediente guardan rutas, pero no hay control fuerte de version, hash, vigencia, tamano ni tipo MIME.
4. El flujo de aprobacion necesita bitacora de cambios por etapa mas granular: mejora solicitada, reingreso, comite, aprobacion final.
5. Las metas y comisiones de nomina ya tienen estructura, pero falta amarrar automaticamente colocaciones/desembolsos reales a calculo de comision.
6. Caja debe distinguir en pantalla de desembolso: monto en mano, comision financiada y capital del credito.
7. Falta validacion visual de documentos adjuntos reales en la ventana de expediente.
8. Falta politica parametrizable por producto para si la comision se descuenta, se cobra aparte o se financia.
