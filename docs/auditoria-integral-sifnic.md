# Auditoria integral del proyecto SIFNIC

Fecha: 2026-05-01

## Alcance revisado

- Proyecto principal ejecutable: `backend/Sifnic.Api` (ASP.NET Core `net10.0`).
- Revision de arquitectura, seguridad, mantenibilidad, diseno tecnico y pruebas.
- Prueba de compilacion y prueba de humo HTTP sobre endpoints clave.

## Resultado ejecutivo

- El sistema **compila correctamente**, pero tiene **riesgos criticos de seguridad y mantenibilidad**.
- El principal riesgo es la **autenticacion inconsistente** entre controladores.
- No hay red de seguridad de pruebas automatizadas (no se detectaron suites de test).

## Hallazgos priorizados

## 1) Critico - Control de acceso inconsistente

- En `CajaController` se valida sesion en backend.
- En `ClientesController` hay endpoints que responden sin validar sesion (ej: catalogos).
- Evidencia de humo:
  - `GET /Caja/Catalogos` sin token => `401`.
  - `GET /Clientes/Catalogos` sin token => `200`.

Impacto:
- Posible exposicion de datos sin autenticacion.
- Superficie de ataque mayor y comportamiento impredecible entre modulos.

Recomendacion:
- Aplicar autenticacion/autorizacion global por defecto (deny-by-default).
- Permitir anonimo solo en rutas publicas explicitas (ej: login).

## 2) Critico - Divulgacion de errores internos

- Multiples controladores retornan `detail = ex.Message` en respuestas `500`.

Impacto:
- Fuga de informacion sensible sobre BD/infraestructura/logica interna.

Recomendacion:
- Estandarizar errores con `ProblemDetails` o esquema comun.
- No exponer mensajes tecnicos al cliente en produccion.
- Registrar detalle solo en logs internos.

## 3) Critico - Cadena de conexion hardcodeada

- `ConexionDb.Cadena` contiene la conexion fija en codigo.

Impacto:
- Riesgo de seguridad y mala portabilidad por ambiente.
- Dificulta rotacion de secretos y despliegues limpios.

Recomendacion:
- Mover conexion a `appsettings` por ambiente + secretos seguros.
- Inyectar via `IConfiguration`/`Options`.

## 4) Alto - Controladores monoliticos

Se detectaron controladores muy grandes (aprox.):
- `CajaController`: 3722 lineas
- `NovedadesController`: 3285 lineas
- `PortalController`: 2995 lineas
- `SeguridadController`: 2801 lineas
- `NominaController`: 2881 lineas

Impacto:
- Alto costo de cambio.
- Riesgo de regresiones funcionales.
- Dificultad para pruebas unitarias e integracion.

Recomendacion:
- Separar por vertical slices o capas:
  - Controllers delgados (HTTP)
  - Services (reglas de negocio)
  - Repositories/Data access (SQL)
  - Validadores dedicados

## 5) Alto - Sin pruebas automatizadas

- No se encontraron proyectos de test.
- `dotnet test` no ejecuta suites reales.

Impacto:
- Cambios sin proteccion contra regresiones.

Recomendacion:
- Crear pruebas de integracion para endpoints criticos:
  - Seguridad (login/sesion)
  - Caja (apertura/cierre/pagos)
  - Clientes y Solicitudes (CRUD + reglas)
- Agregar pruebas de regresion para reglas regulatorias/contables.

## 6) Medio - Criptografia mejorable

- Hash de contrasena por defecto: `PBKDF2SHA1`.

Impacto:
- Funciona, pero no es la opcion recomendada actual.

Recomendacion:
- Migrar a `PBKDF2SHA256` o `PBKDF2SHA512`.
- Hacer migracion progresiva con rehash al login exitoso.

## 7) Medio - Higiene de estructura y artefactos

- Hay muchos artefactos temporales (`tmp-*`, logs, imagenes) en raiz.
- No se detecto `.gitignore` en la raiz del workspace.

Impacto:
- Ruido operativo.
- Riesgo de ensuciar control de versiones.

Recomendacion:
- Estandarizar carpeta de artefactos temporales.
- Configurar exclusiones para logs/build/tmp/capturas.

## Prueba integral ejecutada (tecnica)

1. Entorno:
   - `dotnet --info` => OK (`10.0.202`).

2. Compilacion:
   - `dotnet build` en `backend/Sifnic.Api` => OK.
   - 0 warnings, 0 errors.

3. Pruebas:
   - `dotnet test` => sin suites detectadas.

4. Prueba de humo HTTP:
   - `GET /App/Login` => 200.
   - `POST /Seguridad/Login` con credenciales dummy => 401 esperado.
   - `GET /Caja/Catalogos` sin token => 401.
   - `GET /Clientes/Catalogos` sin token => 200 (hallazgo critico).

5. Ejecucion:
   - `dotnet run` no levanto una nueva instancia por puerto `5277` ya en uso.

## Que si conviene "romper" ahora (rompimiento controlado)

1. Romper contrato actual de acceso:
   - Exigir autenticacion en todos los endpoints por defecto.
   - Declarar excepciones puntuales publicas.

2. Romper monolitos:
   - Partir `CajaController` y `SeguridadController` en modulos pequenos.

3. Romper exposicion de errores:
   - Eliminar `ex.Message` del payload de respuesta.

4. Romper dependencia de conexion hardcodeada:
   - Migrar a configuracion por ambiente + inyeccion de dependencias.

## Orden sugerido de ejecucion (4 fases)

- Fase 1 (Seguridad inmediata):
  - Autorizacion global + excepciones.
  - Sanitizacion de errores.

- Fase 2 (Base tecnica):
  - Configuracion de BD por ambiente.
  - Manejo centralizado de acceso a datos.

- Fase 3 (Refactor funcional):
  - Dividir controladores grandes por casos de uso.
  - Reducir metodos y responsabilidades.

- Fase 4 (Calidad):
  - Introducir pruebas de integracion y humo automatizadas en CI.

## Conclusiones

- El proyecto esta funcional en compilacion, pero con deuda tecnica alta.
- El mayor riesgo real hoy es **seguridad de acceso inconsistente**.
- Si hay que romper algo, lo correcto es romper primero:
  1) autenticacion no uniforme,
  2) manejo de errores inseguro,
  3) acoplamiento de datos en controladores.
