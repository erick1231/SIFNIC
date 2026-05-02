# Critica experta de funcionalidad y diseno (SIFNIC)

Fecha: 2026-05-01
Fuente: capturas recientes en carpeta `capturas/`

## Capturas generadas

- `capturas/00-login.png`
- `capturas/01-dashboard.png`
- `capturas/02-configuracion.png`
- `capturas/03-rrhh.png`
- `capturas/04-portal.png`
- `capturas/05-nomina.png`
- `capturas/06-clientes.png`
- `capturas/07-solicitudes-credito.png`
- `capturas/08-cartera.png`
- `capturas/09-caja.png`
- `capturas/10-contabilidad.png`

## Diagnostico ejecutivo (sin filtro)

- El producto tiene base funcional fuerte y cobertura modular amplia.
- Visualmente se percibe como **3 productos distintos** (temas, densidad, espaciado y jerarquia cambian entre modulos).
- El principal problema de UX no es estetico: es de **carga cognitiva** y consistencia operativa.
- Hay pantallas con buena intencion de data-rich UI, pero sin suficiente priorizacion visual.

## Calificacion rapida (0-10)

- Coherencia visual global: **5.0**
- Claridad de jerarquia: **6.0**
- Densidad/control de complejidad: **5.5**
- Escaneabilidad de datos: **6.5**
- Flujo funcional por tarea critica: **6.0**
- Madurez de sistema de diseno: **4.5**

## Critica por modulo (fuerte y directa)

## 1) Dashboard (`01-dashboard.png`)

Lo bueno:
- Navegacion lateral clara por dominios.
- Tarjetas y modulos detectables rapidamente.

Lo debil:
- Compite demasiada informacion en primer pantallazo (metricas + accesos + modulos + barra superior muy cargada).
- Falta una priorizacion de "lo urgente ahora" vs "exploracion de modulos".
- El header superior tiene demasiados elementos de bajo valor simultaneo.

Mejora clave:
- Separar en 2 zonas: **Operacion del dia** (alertas criticas y pendientes accionables) y **Catalogo de modulos**.
- Reducir ruido de topbar (mover fecha/sucursal/ambiente a un panel contextual discreto).

## 2) Clientes (`06-clientes.png`)

Lo bueno:
- Tabla + panel de detalle lateral es patron correcto para productividad.
- Filtros iniciales visibles arriba (busqueda + estado + tipo).

Lo debil:
- Demasiada competencia entre elementos de panel derecho (ficha, acciones, tabs, KPIs).
- Tipografia de tabla y densidad de filas puede fatigar en jornadas largas.
- Falta contraste semantico fuerte para estados criticos (ej. riesgo alto, expediente incompleto).

Mejora clave:
- Convertir panel derecho en bloques priorizados:
  1) Riesgo y estado operativo,
  2) Acciones primarias,
  3) Detalle ampliado.
- Aplicar codigos de color y badges con severidad consistente.

## 3) Caja (`09-caja.png`)

Lo bueno:
- Flujo por pasos es correcto para operaciones transaccionales.
- Navegacion lateral de caja esta bien orientada al trabajo diario.

Lo debil:
- Densidad extremadamente alta en una sola pantalla (campos, tarjetas, pasos, paneles, estado).
- Jerarquia visual no separa bien "dato editable" de "dato informativo".
- Acciones sensibles (registrar/imprimir/cerrar) comparten zona saturada con baja separacion de riesgo.

Mejora clave:
- Reestructurar en wizard real (1 bloque principal por paso) y mostrar contexto secundario colapsable.
- Botones de alto impacto deben estar aislados y con semantica de color inequívoca.

## 4) Nomina (`05-nomina.png`)

Lo bueno:
- Layout limpio y modular.
- Buen espacio en blanco y lectura relativamente descansada.

Lo debil:
- Cuando no hay datos, la pantalla se siente "vacia" y sin guidance operacional.
- Falta CTA dominante para primer paso real del usuario ("abrir periodo", "configurar", etc.).

Mejora clave:
- Introducir empty states dirigidos por rol y estado del proceso.
- Forzar un "Next Best Action" arriba, con explicacion y impacto.

## 5) Configuracion (`02-configuracion.png`)

Lo bueno:
- Buen intento de centro de control unificado.

Lo debil:
- Sobrecarga de paneles y poca separacion entre navegacion, resumen y detalle tecnico.
- Exceso de cajas similares reduce el contraste de importancia.

Mejora clave:
- Reorganizar en:
  - Resumen ejecutivo (4-6 KPIs max),
  - Navegacion por dominio,
  - Panel activo con foco unico.

## Problemas de sistema de diseno (transversales)

1. **Inconsistencia de tema y contraste**
- Hay pantallas muy oscuras, otras muy claras, y otras hibridas.
- Resultado: sensacion de producto fragmentado.

2. **Escalas tipograficas no unificadas**
- Titulos, subtitulos, labels y celdas no mantienen una escala estable entre modulos.

3. **Espaciado y densidad desalineados**
- Algunos modulos estan compactados; otros sobredimensionados.
- No parece existir una malla de espaciado unica (4/8/12/16/24, etc.).

4. **Semantica de color poco estricta**
- Colores de estado y accion no siempre significan lo mismo entre pantallas.

5. **Botoneria sin jerarquia universal**
- Primario/secundario/peligro no siempre se distingue por peso visual y ubicacion.

## Recomendaciones concretas (enfocadas a funcionalidad + diseno)

## Fase 1 (impacto inmediato, 1-2 semanas)

- Definir un **Design Token Baseline**:
  - Tipografia, spacing, radio, colores semanticos, sombras (o sin sombras), estados.
- Normalizar topbar, sidebar y paneles de accion.
- Unificar botones y estados (primario, secundario, ghost, danger, disabled, loading).

## Fase 2 (flujo operativo, 2-4 semanas)

- Redisenar flujos criticos como "task-first":
  - Caja (apertura, cobro, cierre),
  - Solicitudes (evaluar-resolver),
  - Nomina (abrir-procesar-cerrar-publicar).
- Implementar "next action" y estado de proceso visible.

## Fase 3 (madurez UX, 4-8 semanas)

- Implementar auditoria heuristica por modulo con checklist fijo:
  - claridad, jerarquia, feedback, errores, eficiencia, accesibilidad.
- Introducir pruebas de usabilidad moderadas por rol (caja, supervisor, rrhh, admin).

## Si tuviera que ser brutal: que "romperia" primero

1. Romperia la variabilidad visual entre modulos: un solo lenguaje UI obligatorio.
2. Romperia pantallas sobrecargadas (sobre todo Caja y Configuracion) en vistas por tarea.
3. Romperia la tabla+panel sin prioridades: acciones primarias deben dominar.
4. Romperia los empty states pasivos: toda pantalla sin datos debe decir que hacer despues.

## Conclusión

El sistema es potente funcionalmente, pero hoy paga una deuda seria de consistencia y carga cognitiva.  
No necesita "embellecerse"; necesita **disciplinar su sistema de diseno y simplificar los flujos de trabajo** para subir productividad y reducir errores operativos.
