window.RRHHModel = (() => {
  const SESSION_KEY = "sifnic.session";

  const normalizeText = (value) =>
    String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .trim();

  const sections = [
    {
      id: "empleados",
      label: "Colaboradores",
      subtitle: "Directorio y estructura",
      schema: "rrhh.empleado",
      accent: "#1fd0bc",
      createLabel: "Nuevo colaborador",
      actions: ["Importar padrón", "Exportar plantilla"],
      filters: ["Todos", "Activos", "Permiso", "Vacaciones"],
      queue: [
        {
          title: "Validar expedientes incompletos",
          detail: "7 colaboradores requieren actualización documental.",
          owner: "Gestión RRHH",
          priority: "Alta",
        },
        {
          title: "Confirmar ingresos del mes",
          detail: "6 altas siguen pendientes de asignación final.",
          owner: "Administración",
          priority: "Media",
        },
        {
          title: "Revisar cambio de plaza",
          detail: "2 movimientos internos deben aprobarse hoy.",
          owner: "Dirección Operativa",
          priority: "Alta",
        },
      ],
      columns: ["Código", "Nombre", "Cargo", "Sucursal", "Estado"],
      records: [
        {
          id: "EMP-001",
          name: "María López",
          position: "Gerente RRHH",
          branch: "Casa Matriz",
          status: "Activo",
          summary: "Responsable del área, coordinación de nómina y estructura.",
          details: [
            ["Jefe inmediato", "Dirección General"],
            ["Ingreso", "04/01/2022"],
            ["Contrato", "Indefinido"],
            ["Turno", "Administrativo"],
          ],
          timeline: [
            ["Hoy", "Aprobó tres solicitudes de permiso."],
            ["Ayer", "Cerró revisión de planilla preliminar."],
            ["19 abr", "Validó ingreso de analista de nómina."],
          ],
        },
        {
          id: "EMP-014",
          name: "Carlos Méndez",
          position: "Analista Nómina",
          branch: "Casa Matriz",
          status: "Activo",
          summary: "Opera cálculo de nómina mensual y control de variables.",
          details: [
            ["Jefe inmediato", "María López"],
            ["Ingreso", "15/03/2023"],
            ["Contrato", "Indefinido"],
            ["Turno", "Administrativo"],
          ],
          timeline: [
            ["Hoy", "Cargó movimientos variables del período."],
            ["Ayer", "Emitió 34 esquelas de pago."],
            ["18 abr", "Consolidó horas extra para cierre."],
          ],
        },
        {
          id: "EMP-027",
          name: "Ana Ruiz",
          position: "Oficial Crédito Senior",
          branch: "León",
          status: "Activo",
          summary: "Oficial con cartera senior y apoyo en inducción comercial.",
          details: [
            ["Jefe inmediato", "Jefatura Comercial"],
            ["Ingreso", "01/06/2021"],
            ["Contrato", "Plazo fijo"],
            ["Turno", "Campo"],
          ],
          timeline: [
            ["Hoy", "Pendiente confirmación de hora extra."],
            ["22 abr", "Solicitó vacaciones de junio."],
            ["16 abr", "Recibió ajuste de comisión."],
          ],
        },
        {
          id: "EMP-042",
          name: "Jorge Téllez",
          position: "Supervisor Caja",
          branch: "Managua",
          status: "Vacaciones",
          summary: "Encargado del control operativo de caja y arqueos.",
          details: [
            ["Jefe inmediato", "Gerencia Operativa"],
            ["Ingreso", "15/07/2020"],
            ["Contrato", "Plazo fijo"],
            ["Turno", "Rotativo"],
          ],
          timeline: [
            ["Hoy", "Ausente por vacaciones programadas."],
            ["20 abr", "Inició período vacacional."],
            ["17 abr", "Delegó cierre de caja al supervisor alterno."],
          ],
        },
        {
          id: "EMP-063",
          name: "Sofía Gaitán",
          position: "Gestor Cobranza",
          branch: "Masaya",
          status: "Permiso",
          summary: "Atiende recuperación preventiva y convenios de pago.",
          details: [
            ["Jefe inmediato", "Coordinación de Cobranza"],
            ["Ingreso", "01/10/2024"],
            ["Contrato", "Indefinido"],
            ["Turno", "Campo"],
          ],
          timeline: [
            ["Hoy", "Permiso autorizado por cita médica."],
            ["Ayer", "Cerró 14 gestiones de campo."],
            ["18 abr", "Actualizó promesas de pago."],
          ],
        },
      ],
      process: [
        ["Alta", "Registro del colaborador y datos generales."],
        ["Asignación", "Cargo, sucursal y jefe inmediato."],
        ["Expediente", "Validación documental y contractual."],
        ["Activación", "Habilitación para nómina y operación."],
      ],
    },
    {
      id: "contratos",
      label: "Contratos",
      subtitle: "Vigencias y renovaciones",
      schema: "rrhh.contrato",
      accent: "#65d7ff",
      createLabel: "Nuevo contrato",
      actions: ["Renovar lote", "Emitir borrador"],
      filters: ["Todos", "Vigente", "Por vencer", "Revisión"],
      queue: [
        {
          title: "Renovaciones de abril",
          detail: "5 contratos requieren firma esta semana.",
          owner: "RRHH Legal",
          priority: "Alta",
        },
        {
          title: "Contratos por archivar",
          detail: "11 expedientes deben digitalizarse.",
          owner: "Archivo",
          priority: "Media",
        },
      ],
      columns: ["Contrato", "Empleado", "Tipo", "Vigencia", "Estado"],
      records: [
        {
          id: "CTR-210",
          name: "María López",
          type: "Indefinido",
          validity: "01/01/2025 - Abierto",
          status: "Vigente",
          summary: "Contrato de liderazgo del área de RRHH.",
          details: [
            ["Salario base", "C$ 38,000"],
            ["Modalidad", "Tiempo completo"],
            ["Cláusula", "Confidencialidad activa"],
            ["Archivo", "Expediente digitalizado"],
          ],
          timeline: [
            ["Hoy", "Contrato sin observaciones."],
            ["11 abr", "Revisión interna completada."],
          ],
        },
        {
          id: "CTR-228",
          name: "Ana Ruiz",
          type: "Plazo fijo",
          validity: "01/06/2025 - 31/05/2026",
          status: "Por vencer",
          summary: "Contrato comercial próximo a renovación.",
          details: [
            ["Salario base", "C$ 19,500"],
            ["Modalidad", "Tiempo completo"],
            ["Renovación", "Pendiente de aprobación"],
            ["Archivo", "Requiere adenda"],
          ],
          timeline: [
            ["Hoy", "Renovación pendiente de jefatura."],
            ["20 abr", "Se envió borrador contractual."],
          ],
        },
        {
          id: "CTR-231",
          name: "Jorge Téllez",
          type: "Plazo fijo",
          validity: "15/07/2025 - 14/07/2026",
          status: "Revisión",
          summary: "Contrato bajo validación administrativa.",
          details: [
            ["Salario base", "C$ 17,800"],
            ["Modalidad", "Rotativo"],
            ["Renovación", "Validación legal"],
            ["Archivo", "Pendiente firma"],
          ],
          timeline: [
            ["Hoy", "Revisión de cláusulas operativas."],
            ["18 abr", "Solicitud remitida a legal."],
          ],
        },
      ],
      process: [
        ["Borrador", "Generación del contrato."],
        ["Validación", "Revisión de salario y condiciones."],
        ["Firma", "Aceptación de colaborador y empresa."],
        ["Archivo", "Resguardo y control de vigencia."],
      ],
    },
    {
      id: "permisos",
      label: "Permisos",
      subtitle: "Solicitudes y aprobación",
      schema: "rrhh.solicitud_permiso",
      accent: "#f4be63",
      createLabel: "Nuevo permiso",
      actions: ["Aprobar lote", "Descargar pendientes"],
      filters: ["Todos", "Pendiente", "Aprobado", "Rechazado"],
      queue: [
        {
          title: "Permisos sin respaldo",
          detail: "3 solicitudes carecen de documento adjunto.",
          owner: "RRHH",
          priority: "Alta",
        },
        {
          title: "Permisos urgentes",
          detail: "4 casos requieren respuesta hoy.",
          owner: "Jefaturas",
          priority: "Media",
        },
      ],
      columns: ["Solicitud", "Empleado", "Motivo", "Fecha", "Estado"],
      records: [
        {
          id: "PRM-104",
          name: "Jorge Téllez",
          reason: "Permiso médico",
          date: "23/04/2026",
          status: "Pendiente",
          summary: "Solicitud médica en espera de evidencia documental.",
          details: [
            ["Duración", "1 día"],
            ["Supervisor", "Gerencia Operativa"],
            ["Sustento", "Pendiente adjunto"],
            ["Impacto", "Cubre supervisor alterno"],
          ],
          timeline: [
            ["Hoy", "Registrado en bandeja de aprobación."],
            ["09:15", "Notificación enviada a supervisor."],
          ],
        },
        {
          id: "PRM-107",
          name: "Sofía Gaitán",
          reason: "Trámite personal",
          date: "24/04/2026",
          status: "Aprobado",
          summary: "Permiso aprobado sin impacto mayor sobre operación.",
          details: [
            ["Duración", "Medio día"],
            ["Supervisor", "Coordinación Cobranza"],
            ["Sustento", "Validado"],
            ["Impacto", "Sin reprogramación"],
          ],
          timeline: [
            ["Hoy", "Aprobación final registrada."],
            ["Ayer", "Solicitud ingresada por portal interno."],
          ],
        },
        {
          id: "PRM-118",
          name: "Karla Pérez",
          reason: "Permiso especial",
          date: "26/04/2026",
          status: "Rechazado",
          summary: "Solicitud denegada por falta de disponibilidad del área.",
          details: [
            ["Duración", "1 día"],
            ["Supervisor", "Jefatura Caja"],
            ["Sustento", "No aplica"],
            ["Impacto", "Cierre de agencia comprometido"],
          ],
          timeline: [
            ["Hoy", "Rechazo notificado."],
            ["Ayer", "Jefatura dejó observación operativa."],
          ],
        },
      ],
      process: [
        ["Registro", "Captura de solicitud."],
        ["Validación", "Revisión de motivo y respaldo."],
        ["Resolución", "Aprobación o rechazo."],
        ["Aplicación", "Impacto en asistencia y operación."],
      ],
    },
    {
      id: "vacaciones",
      label: "Vacaciones",
      subtitle: "Programación y saldo",
      schema: "rrhh.vacacion",
      accent: "#8ee887",
      createLabel: "Programar vacaciones",
      actions: ["Ver saldos", "Aprobar calendario"],
      filters: ["Todos", "En curso", "Programada", "Pendiente"],
      queue: [
        {
          title: "Cobertura de ausencias",
          detail: "4 equipos requieren reemplazo temporal.",
          owner: "Operaciones",
          priority: "Alta",
        },
        {
          title: "Saldos acumulados",
          detail: "9 colaboradores con días por encima del umbral.",
          owner: "RRHH",
          priority: "Media",
        },
      ],
      columns: ["Empleado", "Saldo", "Programación", "Supervisor", "Estado"],
      records: [
        {
          id: "VAC-021",
          name: "Jorge Téllez",
          balance: "12 días",
          schedule: "20/04/2026 - 30/04/2026",
          status: "En curso",
          summary: "Vacaciones activas con cobertura ya asignada.",
          details: [
            ["Supervisor", "Gerencia Operativa"],
            ["Reemplazo", "Supervisor alterno activo"],
            ["Saldo posterior", "0 días"],
            ["Observación", "Sin impacto crítico"],
          ],
          timeline: [
            ["Hoy", "Cobertura confirmada."],
            ["20 abr", "Inicio de período vacacional."],
          ],
        },
        {
          id: "VAC-028",
          name: "Ana Ruiz",
          balance: "15 días",
          schedule: "01/06/2026 - 15/06/2026",
          status: "Programada",
          summary: "Vacaciones proyectadas para junio.",
          details: [
            ["Supervisor", "Jefatura Comercial"],
            ["Reemplazo", "Oficial senior en entrenamiento"],
            ["Saldo posterior", "0 días"],
            ["Observación", "Confirmada"],
          ],
          timeline: [
            ["Hoy", "Programación aceptada."],
            ["18 abr", "Revisión de cobertura completada."],
          ],
        },
        {
          id: "VAC-031",
          name: "Luis Medina",
          balance: "10 días",
          schedule: "Pendiente",
          status: "Pendiente",
          summary: "Solicitud cargada y esperando autorización final.",
          details: [
            ["Supervisor", "Jefatura RRHH"],
            ["Reemplazo", "Pendiente"],
            ["Saldo posterior", "Por definir"],
            ["Observación", "Alta carga operativa"],
          ],
          timeline: [
            ["Hoy", "En bandeja de aprobación."],
            ["Ayer", "Solicitud registrada."],
          ],
        },
      ],
      process: [
        ["Saldo", "Consulta de días disponibles."],
        ["Solicitud", "Programación del período."],
        ["Cobertura", "Reemplazo y validación operativa."],
        ["Aplicación", "Bloqueo y seguimiento."],
      ],
    },
    {
      id: "horas_extra",
      label: "Horas extra",
      subtitle: "Control y autorización",
      schema: "rrhh.hora_extra",
      accent: "#ff9f6e",
      createLabel: "Registrar horas",
      actions: ["Enviar a nómina", "Validar jornada"],
      filters: ["Todos", "Pendiente", "Aprobada", "Aplicada"],
      queue: [
        {
          title: "Autorizaciones pendientes",
          detail: "9 registros siguen en espera de gerencia.",
          owner: "Gerencia",
          priority: "Alta",
        },
        {
          title: "Desviaciones por agencia",
          detail: "2 agencias con horas extra fuera del promedio.",
          owner: "Operaciones",
          priority: "Media",
        },
      ],
      columns: ["Registro", "Empleado", "Horas", "Motivo", "Estado"],
      records: [
        {
          id: "HEX-009",
          name: "Luis Medina",
          hours: "4.5",
          reason: "Cierre de cartera",
          status: "Pendiente",
          summary: "Horas extra esperando aprobación gerencial.",
          details: [
            ["Supervisor", "Jefatura Crédito"],
            ["Agencia", "León"],
            ["Impacto", "C$ 1,200 estimado"],
            ["Observación", "Cierre excepcional"],
          ],
          timeline: [
            ["Hoy", "En cola de aprobación."],
            ["Ayer", "Registro capturado."],
          ],
        },
        {
          id: "HEX-011",
          name: "Karla Pérez",
          hours: "3.0",
          reason: "Cierre de caja",
          status: "Aprobada",
          summary: "Horas validadas para inclusión en nómina.",
          details: [
            ["Supervisor", "Jefatura Caja"],
            ["Agencia", "Masaya"],
            ["Impacto", "C$ 880 estimado"],
            ["Observación", "Sin ajustes"],
          ],
          timeline: [
            ["Hoy", "Validación confirmada."],
            ["Ayer", "Asignada a nómina."],
          ],
        },
      ],
      process: [
        ["Registro", "Captura de jornada extendida."],
        ["Validación", "Revisión del supervisor."],
        ["Autorización", "Confirmación gerencial."],
        ["Aplicación", "Carga en nómina."],
      ],
    },
    {
      id: "nomina",
      label: "Nómina",
      subtitle: "Cálculo y publicación",
      schema: "nomina.nomina",
      accent: "#a2b5ff",
      createLabel: "Abrir período",
      actions: ["Calcular nómina", "Emitir esquelas"],
      filters: ["Todos", "Abierta", "Cerrada", "Enviando"],
      queue: [
        {
          title: "Variables pendientes",
          detail: "14 ajustes siguen pendientes de consolidación.",
          owner: "Analista Nómina",
          priority: "Alta",
        },
        {
          title: "Incidencias del período",
          detail: "3 registros requieren revisión manual.",
          owner: "RRHH",
          priority: "Media",
        },
      ],
      columns: ["Nómina", "Tipo", "Período", "Colaboradores", "Estado"],
      records: [
        {
          id: "NOM-2026-04",
          type: "Mensual",
          period: "Abril 2026",
          people: "128",
          status: "Abierta",
          summary: "Nómina actual en preparación de cierre.",
          details: [
            ["Devengado", "C$ 2.8M"],
            ["Responsable", "Carlos Méndez"],
            ["Variables", "14 pendientes"],
            ["Esquelas", "78% emitidas"],
          ],
          timeline: [
            ["Hoy", "Se aplicaron horas extra validadas."],
            ["Ayer", "Se recalculó detalle principal."],
          ],
        },
        {
          id: "NOM-2026-03",
          type: "Mensual",
          period: "Marzo 2026",
          people: "126",
          status: "Cerrada",
          summary: "Período cerrado y conciliado con administración.",
          details: [
            ["Devengado", "C$ 2.7M"],
            ["Responsable", "Carlos Méndez"],
            ["Variables", "0 pendientes"],
            ["Esquelas", "100% emitidas"],
          ],
          timeline: [
            ["31 mar", "Cierre definitivo ejecutado."],
            ["30 mar", "Conciliación administrativa completa."],
          ],
        },
        {
          id: "ESP-2026-04",
          type: "Esquela",
          period: "Abril 2026",
          people: "100",
          status: "Enviando",
          summary: "Publicación masiva en proceso de distribución.",
          details: [
            ["Canal", "Correo corporativo"],
            ["Pendientes", "28 envíos"],
            ["Errores", "0 críticos"],
            ["Observación", "Proceso automatizado"],
          ],
          timeline: [
            ["Hoy", "100 esquelas enviadas."],
            ["Hace 1 hora", "Lote inicial despachado."],
          ],
        },
      ],
      process: [
        ["Apertura", "Creación del período."],
        ["Carga", "Variables e incidencias."],
        ["Cálculo", "Detalle y conceptos."],
        ["Publicación", "Cierre y esquelas."],
      ],
    },
    {
      id: "liquidaciones",
      label: "Liquidaciones",
      subtitle: "Bajas y finiquitos",
      schema: "nomina.liquidacion",
      accent: "#f08ca0",
      createLabel: "Nueva liquidación",
      actions: ["Generar finiquito", "Revisar descuentos"],
      filters: ["Todos", "En cálculo", "Revisión", "Aprobada"],
      queue: [
        {
          title: "Prestaciones pendientes",
          detail: "4 expedientes están en cálculo final.",
          owner: "RRHH / Administración",
          priority: "Alta",
        },
        {
          title: "Casos observados",
          detail: "2 expedientes requieren confirmación legal.",
          owner: "Legal",
          priority: "Media",
        },
      ],
      columns: ["Expediente", "Empleado", "Fecha salida", "Tipo", "Estado"],
      records: [
        {
          id: "LIQ-021",
          name: "Pedro Solís",
          departure: "18/04/2026",
          type: "Renuncia",
          status: "En cálculo",
          summary: "Expediente en consolidación de prestaciones y descuentos.",
          details: [
            ["Último salario", "C$ 15,200"],
            ["Prestaciones", "Pendiente cálculo"],
            ["Préstamos", "Sin saldo"],
            ["Archivo", "Expediente abierto"],
          ],
          timeline: [
            ["Hoy", "Prestaciones en validación."],
            ["Ayer", "Se registró salida oficial."],
          ],
        },
        {
          id: "LIQ-022",
          name: "Marta Rivas",
          departure: "20/04/2026",
          type: "Mutuo acuerdo",
          status: "Revisión",
          summary: "Caso remitido a revisión por administración.",
          details: [
            ["Último salario", "C$ 13,800"],
            ["Prestaciones", "Calculadas"],
            ["Préstamos", "Sin saldo"],
            ["Archivo", "Pendiente firma"],
          ],
          timeline: [
            ["Hoy", "En revisión administrativa."],
            ["Ayer", "Cálculo final generado."],
          ],
        },
      ],
      process: [
        ["Solicitud", "Registro de salida."],
        ["Cálculo", "Prestaciones y descuentos."],
        ["Aprobación", "Validación administrativa."],
        ["Cierre", "Finiquito y archivo."],
      ],
    },
    {
      id: "prestamos_variables",
      label: "Préstamos y variables",
      subtitle: "Movimientos complementarios",
      schema: "nomina.prestamo_empleado",
      accent: "#ffd36f",
      createLabel: "Nuevo movimiento",
      actions: ["Registrar préstamo", "Enviar a planilla"],
      filters: ["Todos", "Activo", "Pendiente", "Aplicado"],
      queue: [
        {
          title: "Variables por aplicar",
          detail: "8 movimientos siguen fuera del cálculo principal.",
          owner: "Nómina",
          priority: "Alta",
        },
        {
          title: "Préstamos en mora",
          detail: "3 créditos internos requieren seguimiento.",
          owner: "RRHH",
          priority: "Media",
        },
      ],
      columns: ["Movimiento", "Empleado", "Tipo", "Monto", "Estado"],
      records: [
        {
          id: "VAR-084",
          name: "Carlos Méndez",
          movementType: "Bonificación",
          amount: "C$ 3,500",
          status: "Aplicado",
          summary: "Bonificación cargada al período actual.",
          details: [
            ["Origen", "Reconocimiento mensual"],
            ["Período", "Abril 2026"],
            ["Aplicación", "Planilla abierta"],
            ["Observación", "Confirmado"],
          ],
          timeline: [
            ["Hoy", "Aplicado al cálculo."],
            ["Ayer", "Autorizado por RRHH."],
          ],
        },
        {
          id: "PRE-014",
          name: "Luis Medina",
          movementType: "Préstamo personal",
          amount: "C$ 12,000",
          status: "Activo",
          summary: "Préstamo interno con cuota activa en nómina.",
          details: [
            ["Cuota", "C$ 1,200"],
            ["Saldo", "C$ 7,200"],
            ["Vencimiento", "Octubre 2026"],
            ["Observación", "Sin mora"],
          ],
          timeline: [
            ["Hoy", "Cuota descontada en proceso."],
            ["15 abr", "Seguimiento de saldo actualizado."],
          ],
        },
        {
          id: "VAR-087",
          name: "Ana Ruiz",
          movementType: "Comisión",
          amount: "C$ 6,800",
          status: "Pendiente",
          summary: "Comisión en espera de confirmación comercial.",
          details: [
            ["Origen", "Meta trimestral"],
            ["Período", "Abril 2026"],
            ["Aplicación", "Pendiente"],
            ["Observación", "Esperando validación"],
          ],
          timeline: [
            ["Hoy", "Pendiente de autorización."],
            ["Ayer", "Jefatura comercial remitió soporte."],
          ],
        },
      ],
      process: [
        ["Registro", "Captura del movimiento."],
        ["Validación", "Aprobación del responsable."],
        ["Aplicación", "Carga al cálculo de planilla."],
        ["Seguimiento", "Control de descuentos y saldo."],
      ],
    },
  ];

  const getSession = () => {
    try {
      const session = JSON.parse(localStorage.getItem(SESSION_KEY) || "null");
      return session && session.active ? session : null;
    } catch {
      return null;
    }
  };

  const clearSession = () => {
    localStorage.removeItem(SESSION_KEY);
  };

  const getSections = () => sections.map((section) => ({ ...section }));

  const getSectionById = (sectionId) =>
    sections.find((section) => section.id === sectionId) || sections[0];

  const filterRecords = (section, searchTerm, activeFilter) => {
    const term = normalizeText(searchTerm);
    const filter = normalizeText(activeFilter || "Todos");

    const filterCandidates = [filter];
    if (filter.endsWith("s")) {
      filterCandidates.push(filter.slice(0, -1));
    }
    if (filter.endsWith("a")) {
      filterCandidates.push(`${filter.slice(0, -1)}o`);
    }
    if (filter.endsWith("o")) {
      filterCandidates.push(`${filter.slice(0, -1)}a`);
    }

    return section.records.filter((record) => {
      const haystack = normalizeText(Object.values(record).join(" "));
      const matchesSearch = !term || haystack.includes(term);
      const matchesFilter =
        filter === "todos" ||
        filterCandidates.some((candidate) => candidate && haystack.includes(candidate));
      return matchesSearch && matchesFilter;
    });
  };

  return {
    getSession,
    clearSession,
    getSections,
    getSectionById,
    filterRecords,
  };
})();
