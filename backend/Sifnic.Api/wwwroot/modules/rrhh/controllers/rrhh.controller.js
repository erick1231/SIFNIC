(() => {
  const sessionApi = window.SifnicSession;
  const model = window.RRHHModel;
  const view = window.RRHHView;
  const employeeService = window.EmployeeService;
  const contractService = window.ContractService;
  const novedadesService = window.NovedadesService;
  const clockService = window.ClockService;
  const dashboardService = window.RRHHDashboardService;
  const catalogService = window.RRHHCatalogService;
  const personnelActionService = window.PersonnelActionService;
  const expedienteService = window.ExpedienteService;
  const structureService = window.OrganizationStructureService;

  const DEFAULT_GROUP_ID = "empleados";
  const MIN_DB_DATE = "1753-01-01";
  const wait = (ms) => new Promise((resolve) => window.setTimeout(resolve, ms));

  const todayIso = () => new Date().toISOString().slice(0, 10);
  const daysAgoIso = (days) => new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
  const normalizeHalfDayShift = (value) => {
    const text = String(value || "").trim().toUpperCase();
    if (text === "MANANA" || text === "MAÑANA") {
      return "MANANA";
    }

    return text === "TARDE" ? "TARDE" : "";
  };

  const WORKFLOW_CONFIGS = {
    solicitud_permiso: {
      noun: "Vacacion",
      idField: "idSolicitudPermiso",
      statusField: "estadoPermiso",
      pendingStatus: "SOLICITADO",
      list: (filters) => novedadesService.listPermisos(filters),
      get: (id) => novedadesService.getPermiso(id),
      create: (payload) => novedadesService.createPermiso(payload),
      update: (id, payload) => novedadesService.updatePermiso(id, payload),
      resolve: (id, payload) => novedadesService.resolvePermiso(id, payload),
      buildPayload: (formData) => ({
        idEmpleado: Number(formData.idEmpleado || 0),
        idTipoPermiso: Number(formData.idTipoPermiso || 0),
        fechaSolicitud: null,
        fechaInicio: model.displayDateToIso(formData.fechaInicio),
        fechaFin: model.displayDateToIso(formData.fechaFin),
        observacion: String(formData.observacion || "").trim(),
        esMedioDia: Boolean(formData.esMedioDia),
        jornadaMedioDia: normalizeHalfDayShift(formData.jornadaMedioDia),
      }),
      validate: (payload, formData) => {
        const errors = {};
        const fechaInicioTexto = model.sanitizeDateInput(formData.fechaInicio);
        const fechaFinTexto = model.sanitizeDateInput(formData.fechaFin);

        if (!payload.idEmpleado) {
          errors.idEmpleado = "Selecciona el empleado.";
        }

        if (!payload.idTipoPermiso) {
          errors.idTipoPermiso = "Selecciona la modalidad de vacacion.";
        }

        if (!fechaInicioTexto) {
          errors.fechaInicio = "Ingresa la fecha de inicio.";
        } else if (!isValidDisplayDate(fechaInicioTexto)) {
          errors.fechaInicio = "Usa el formato dd/mm/aaaa.";
        } else if (payload.fechaInicio < MIN_DB_DATE) {
          errors.fechaInicio = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }

        if (!fechaFinTexto) {
          errors.fechaFin = "Ingresa la fecha fin.";
        } else if (!isValidDisplayDate(fechaFinTexto)) {
          errors.fechaFin = "Usa el formato dd/mm/aaaa.";
        } else if (payload.fechaFin < MIN_DB_DATE) {
          errors.fechaFin = "Ingresa una fecha igual o mayor a 01/01/1753.";
        } else if (payload.fechaInicio && payload.fechaFin && payload.fechaFin < payload.fechaInicio) {
          errors.fechaFin = "La fecha fin debe ser igual o mayor a la fecha de inicio.";
        }

        if (payload.esMedioDia) {
          if (payload.fechaInicio && payload.fechaFin && payload.fechaInicio !== payload.fechaFin) {
            errors.fechaFin = "Si la vacacion es de medio dia, la fecha fin debe ser igual a la fecha de inicio.";
          }

          if (!payload.jornadaMedioDia) {
            errors.jornadaMedioDia = "Selecciona manana o tarde.";
          }
        }

        if (payload.observacion && payload.observacion.length > 320) {
          errors.observacion = "La observacion supera el limite permitido.";
        }

        return errors;
      },
      buildResolutionPayload: (formData) => ({
        action: formData.action,
        observation: formData.observation || null,
      }),
      validateResolution: (record, formData) => {
        if (formData.action === "RECHAZAR" && !formData.observation) {
          return "Explica el motivo del rechazo.";
        }

        return "";
      },
      getActionState: (record) => String(record?.estadoPermiso || "").toUpperCase(),
      resolveButtonLabel: (action) => (action === "APROBAR" ? "Confirmar aprobacion" : "Confirmar rechazo"),
    },
    vacacion: {
      noun: "Vacacion",
      idField: "idVacacion",
      statusField: "estadoVacacion",
      pendingStatus: "SOLICITADA",
      list: (filters) => novedadesService.listVacaciones(filters),
      get: (id) => novedadesService.getVacacion(id),
      create: (payload) => novedadesService.createVacacion(payload),
      update: (id, payload) => novedadesService.updateVacacion(id, payload),
      resolve: (id, payload) => novedadesService.resolveVacacion(id, payload),
      buildPayload: (formData) => ({
        idEmpleado: Number(formData.idEmpleado || 0),
        fechaSolicitud: null,
        fechaInicio: model.displayDateToIso(formData.fechaInicio),
        fechaFin: model.displayDateToIso(formData.fechaFin),
        observacionSolicitud: String(formData.observacionSolicitud || "").trim(),
        esMedioDia: Boolean(formData.esMedioDia),
        jornadaMedioDia: normalizeHalfDayShift(formData.jornadaMedioDia),
      }),
      validate: (payload, formData) => {
        const errors = {};
        const fechaInicioTexto = model.sanitizeDateInput(formData.fechaInicio);
        const fechaFinTexto = model.sanitizeDateInput(formData.fechaFin);

        if (!payload.idEmpleado) {
          errors.idEmpleado = "Selecciona el empleado.";
        }

        if (!fechaInicioTexto) {
          errors.fechaInicio = "Ingresa la fecha de inicio.";
        } else if (!isValidDisplayDate(fechaInicioTexto)) {
          errors.fechaInicio = "Usa el formato dd/mm/aaaa.";
        } else if (payload.fechaInicio < MIN_DB_DATE) {
          errors.fechaInicio = "Ingresa una fecha igual o mayor a 01/01/1753.";
        }

        if (!fechaFinTexto) {
          errors.fechaFin = "Ingresa la fecha fin.";
        } else if (!isValidDisplayDate(fechaFinTexto)) {
          errors.fechaFin = "Usa el formato dd/mm/aaaa.";
        } else if (payload.fechaFin < MIN_DB_DATE) {
          errors.fechaFin = "Ingresa una fecha igual o mayor a 01/01/1753.";
        } else if (payload.fechaInicio && payload.fechaFin && payload.fechaFin < payload.fechaInicio) {
          errors.fechaFin = "La fecha fin debe ser igual o mayor a la fecha de inicio.";
        }

        if (payload.esMedioDia) {
          if (payload.fechaInicio && payload.fechaFin && payload.fechaInicio !== payload.fechaFin) {
            errors.fechaFin = "Si la vacacion es de medio dia, la fecha fin debe ser igual a la fecha de inicio.";
          }

          if (!payload.jornadaMedioDia) {
            errors.jornadaMedioDia = "Selecciona manana o tarde.";
          }
        }

        if (payload.observacionSolicitud && payload.observacionSolicitud.length > 500) {
          errors.observacionSolicitud = "La observacion supera el limite permitido.";
        }

        return errors;
      },
      buildResolutionPayload: (formData) => ({
        action: formData.action,
        observation: formData.observation || null,
        approvedDays: Number.parseFloat(String(formData.approvedDays || "").trim()) || 0,
      }),
      validateResolution: (record, formData) => {
        if (formData.action === "RECHAZAR" && !formData.observation) {
          return "Explica el motivo del rechazo.";
        }

        if (formData.action === "APROBAR") {
          const approvedDays = Number.parseFloat(String(formData.approvedDays || "").trim());
          if (!(approvedDays > 0) || approvedDays > Number(record?.diasSolicitados || 0)) {
            return "Ingresa una cantidad de dias aprobados valida.";
          }
        }

        return "";
      },
      getActionState: (record) => String(record?.estadoVacacion || "").toUpperCase(),
      resolveButtonLabel: (action) => (action === "APROBAR" ? "Confirmar aprobacion" : "Confirmar rechazo"),
    },
    hora_extra: {
      noun: "Hora extra",
      idField: "idHoraExtra",
      statusField: "estadoHoraExtra",
      pendingStatus: "REGISTRADA",
      list: (filters) => novedadesService.listHorasExtra(filters),
      get: (id) => novedadesService.getHoraExtra(id),
      create: (payload) => novedadesService.createHoraExtra(payload),
      update: (id, payload) => novedadesService.updateHoraExtra(id, payload),
      resolve: (id, payload) => novedadesService.resolveHoraExtra(id, payload),
      buildPayload: (formData) => {
        const cantidadHoras = Number.parseFloat(String(formData.cantidadHoras || "").replace(",", ".").trim());

        return {
          idEmpleado: Number(formData.idEmpleado || 0),
          idTipoHoraExtra: Number(formData.idTipoHoraExtra || 0),
          fechaHoraExtra: model.displayDateToIso(formData.fechaHoraExtra),
          cantidadHoras: Number.isFinite(cantidadHoras) ? Number(cantidadHoras.toFixed(2)) : 0,
          observacion: String(formData.observacion || "").trim(),
        };
      },
      validate: (payload, formData) => {
        const errors = {};
        const fechaTexto = model.sanitizeDateInput(formData.fechaHoraExtra);
        const today = todayIso();

        if (!payload.idEmpleado) {
          errors.idEmpleado = "Selecciona el empleado.";
        }

        if (!payload.idTipoHoraExtra) {
          errors.idTipoHoraExtra = "Selecciona el tipo de hora extra.";
        }

        if (!fechaTexto) {
          errors.fechaHoraExtra = "Ingresa la fecha de la hora extra.";
        } else if (!isValidDisplayDate(fechaTexto)) {
          errors.fechaHoraExtra = "Usa el formato dd/mm/aaaa.";
        } else if (payload.fechaHoraExtra < MIN_DB_DATE) {
          errors.fechaHoraExtra = "Ingresa una fecha igual o mayor a 01/01/1753.";
        } else if (payload.fechaHoraExtra > today) {
          errors.fechaHoraExtra = "La fecha de la hora extra no puede ser futura.";
        }

        if (!(payload.cantidadHoras > 0) || payload.cantidadHoras > 16) {
          errors.cantidadHoras = "Ingresa una cantidad de horas valida.";
        }

        if (payload.observacion && payload.observacion.length > 500) {
          errors.observacion = "La observacion supera el limite permitido.";
        }

        return errors;
      },
      buildResolutionPayload: (formData) => ({
        action: formData.action,
        observation: formData.observation || null,
      }),
      validateResolution: (record, formData) => {
        if (formData.action === "RECHAZAR" && !formData.observation) {
          return "Explica el motivo del rechazo.";
        }

        return "";
      },
      getActionState: (record) => String(record?.estadoHoraExtra || "").toUpperCase(),
      resolveButtonLabel: (action) => (action === "APROBAR" ? "Confirmar aprobacion" : "Confirmar rechazo"),
    },
  };

  const REPORT_EMPLOYMENT_STATUS_OPTIONS = [
    { value: "TODOS", label: "Todos los empleados" },
    { value: "ACTIVOS", label: "Solo activos" },
    { value: "INACTIVOS", label: "Solo inactivos" },
  ];

  const state = {
    activeGroupId: DEFAULT_GROUP_ID,
    activeModuleId: null,
    employees: {
      search: "",
      status: "TODOS",
      items: [],
      catalogs: {
        departments: [],
        positions: [],
        banks: [],
        suggestedCode: "",
      },
      details: {},
      selectedId: null,
      detailVisible: false,
      busy: false,
      searchTimer: null,
      usernamePreviewTimer: null,
      usernamePreviewRequestId: 0,
    },
    contracts: {
      search: "",
      status: "TODOS",
      items: [],
      catalogs: {
        employees: [],
        contractTypes: [],
        schedules: [],
        currencies: [],
        defaultCurrency: "NIO",
      },
      selectedId: null,
      busy: false,
      searchTimer: null,
    },
    workflows: {
      search: "",
      status: "TODOS",
      items: [],
      catalogs: {
        employees: [],
        permissionTypes: [],
        overtimeTypes: [],
      },
      selectedId: null,
      detailVisible: false,
      busy: false,
      searchTimer: null,
      loadedModuleId: null,
    },
    catalogsAdmin: {
      search: "",
      status: "ACTIVOS",
      items: [],
      catalogs: {
        departments: [],
      },
      selectedId: null,
      detailVisible: false,
      busy: false,
      searchTimer: null,
      loadedModuleId: null,
    },
    actions: {
      search: "",
      status: "TODOS",
      items: [],
      catalogs: {
        employees: [],
        actionTypes: [],
      },
      selectedId: null,
      detailVisible: false,
      busy: false,
      searchTimer: null,
    },
    documents: {
      search: "",
      status: "TODOS",
      items: [],
      catalogs: {
        employees: [],
        documentTypes: [],
      },
      selectedId: null,
      busy: false,
      searchTimer: null,
    },
    clock: {
      search: "",
      dateFrom: daysAgoIso(6),
      dateTo: todayIso(),
      idEmpleado: "",
      rows: [],
      catalogs: {
        employees: [],
      },
      branding: null,
      selectedIndex: -1,
      busy: false,
      searchTimer: null,
    },
    reports: {
      search: "",
      cutoffDate: todayIso(),
      idDepartamento: "",
      status: "TODOS",
      rows: [],
      catalogs: {
        departments: [],
      },
      branding: null,
      selectedIndex: -1,
      busy: false,
      searchTimer: null,
    },
    structure: {
      search: "",
      idDepartamento: "",
      branchKey: "TODOS",
      tree: [],
      branches: [],
      summary: null,
      generalManagementName: "",
      catalogs: {
        departments: [],
        nodeTypes: [],
        employees: [],
        positions: [],
        parentNodes: [],
      },
      selectedId: null,
      busy: false,
      loaded: false,
      catalogsLoaded: false,
      searchTimer: null,
    },
    audit: {
      search: "",
      process: "",
      dateFrom: daysAgoIso(14),
      dateTo: todayIso(),
      rows: [],
      catalogs: {
        processes: [],
      },
      selectedIndex: -1,
      busy: false,
      searchTimer: null,
    },
    dashboard: {
      overview: null,
      loaded: false,
      busy: false,
    },
    genericDelete: {
      kind: "",
      moduleId: "",
      recordId: null,
    },
  };

  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const getActiveGroup = () => model.getGroupById(state.activeGroupId);

  const normalizeModuleId = (moduleId) =>
    String(moduleId || "").trim().toLowerCase() === "solicitud_permiso" ? "vacacion" : moduleId;

  const getActiveModule = () =>
    state.activeModuleId ? model.getModuleById(state.activeGroupId, normalizeModuleId(state.activeModuleId)) : null;

  const isClockLikeModule = (module) => module?.type === "clock" || module?.type === "hours_report";
  const isHoursReportModule = (module) => module?.type === "hours_report";

  const isValidDisplayDate = (value) => {
    const text = String(value || "").trim();
    if (!/^\d{2}\/\d{2}\/\d{4}$/.test(text)) {
      return false;
    }

    return Boolean(model.displayDateToIso(text));
  };

  const getWorkflowConfig = (moduleId = state.activeModuleId) =>
    moduleId ? WORKFLOW_CONFIGS[normalizeModuleId(moduleId)] || null : null;

  const getWorkflowRuntimeElements = () => ({
    employee: document.getElementById("workflowEmployeeId"),
    startDate: document.getElementById("workflowStartDate"),
    endDate: document.getElementById("workflowEndDate"),
    halfDay: document.getElementById("workflowHalfDay"),
    halfDayGroup: document.getElementById("workflowHalfDayGroup"),
    halfDayMorning: document.getElementById("workflowHalfDayMorning"),
    halfDayAfternoon: document.getElementById("workflowHalfDayAfternoon"),
    vacationBalance: document.getElementById("workflowVacationBalance"),
  });

  const setWorkflowVacationBalanceNote = (message, tone = "") => {
    const element = document.getElementById("workflowVacationBalance");
    if (!element) {
      return;
    }

    element.className = `balance-card form-field-full${tone ? ` is-${tone}` : ""}`;
    element.textContent = message;
  };

  const syncVacationHalfDayState = () => {
    if (view.elements.workflowForm?.dataset.moduleId !== "vacacion") {
      return;
    }

    const workflowElements = getWorkflowRuntimeElements();
    if (!workflowElements.halfDay || !workflowElements.endDate) {
      return;
    }

    const isHalfDay = workflowElements.halfDay.checked;
    if (workflowElements.halfDayGroup) {
      workflowElements.halfDayGroup.hidden = !isHalfDay;
    }

    workflowElements.endDate.disabled = isHalfDay;
    if (isHalfDay) {
      workflowElements.endDate.value = workflowElements.startDate?.value || workflowElements.endDate.value;
      view.clearWorkflowFieldError("fechaFin");
    }
  };

  const refreshWorkflowVacationBalance = async () => {
    const moduleId = view.elements.workflowForm?.dataset.moduleId;
    if (moduleId !== "vacacion") {
      return;
    }

    const workflowElements = getWorkflowRuntimeElements();
    const idEmpleado = Number(workflowElements.employee?.value || 0);
    if (!idEmpleado) {
      setWorkflowVacationBalanceNote("Selecciona colaborador y fechas para ver el saldo disponible de vacaciones.");
      return;
    }

    const cutoff =
      model.displayDateToIso(workflowElements.endDate?.value || "") ||
      model.displayDateToIso(workflowElements.startDate?.value || "") ||
      todayIso();

    setWorkflowVacationBalanceNote("Calculando saldo disponible de vacaciones...", "accent");

    try {
      const snapshot = await novedadesService.getVacationBalance({
        idEmpleado,
        fechaCorte: cutoff,
      });

      setWorkflowVacationBalanceNote(
        `Disponibles: ${Number(snapshot?.diasDisponibles || 0).toFixed(2)} d · Acumulados: ${Number(
          snapshot?.diasAcumulados || 0,
        ).toFixed(2)} d · Consumidos: ${Number(snapshot?.diasConsumidos || 0).toFixed(2)} d`,
        Number(snapshot?.diasDisponibles || 0) > 0 ? "success" : "warning",
      );
    } catch (error) {
      setWorkflowVacationBalanceNote(error.message || "No se pudo calcular el saldo de vacaciones.", "danger");
    }
  };

  const syncSelectedId = (collection, stateBucket, keyName) => {
    if (!Array.isArray(collection) || !collection.length) {
      stateBucket.selectedId = null;
      if (Object.prototype.hasOwnProperty.call(stateBucket, "detailVisible")) {
        stateBucket.detailVisible = false;
      }
      return;
    }

    const exists = collection.some((item) => Number(item[keyName]) === Number(stateBucket.selectedId));

    if (!exists) {
      stateBucket.selectedId = collection[0]?.[keyName] || null;
    }
  };

  const contractMatchesActiveFilters = (contract) => {
    if (!contract) {
      return false;
    }

    const status = String(state.contracts.status || "TODOS").toUpperCase();
    if (status === "VIGENTES" && !contract.esContratoVigente) return false;
    if (status === "HISTORICOS" && contract.esContratoVigente) return false;
    if (status === "POR_VENCER" && !contract.estaPorVencer) return false;
    if (status === "TEMPORALES" && !contract.esTemporal) return false;
    if (status === "TEMPORALES_POR_VENCER" && !(contract.esTemporal && contract.estaPorVencer)) return false;

    const search = String(state.contracts.search || "").trim().toLowerCase();
    if (!search) {
      return true;
    }

    return [
      contract.numeroContrato,
      contract.codigoEmpleado,
      contract.nombreEmpleado,
      contract.nombreTipoContrato,
      contract.nombreHorario,
    ].some((value) => String(value || "").toLowerCase().includes(search));
  };

  const upsertContractInState = (contract) => {
    if (!contract) {
      return;
    }

    const contractId = Number(contract.idContrato || 0);
    if (!contractId) {
      return;
    }

    state.contracts.items = state.contracts.items.filter((item) => Number(item.idContrato) !== contractId);

    if (contractMatchesActiveFilters(contract)) {
      state.contracts.items = [contract, ...state.contracts.items].sort(
        (left, right) => Number(right.idContrato || 0) - Number(left.idContrato || 0),
      );
    }

    syncSelectedId(state.contracts.items, state.contracts, "idContrato");
  };

  const isEligibleContractEmployee = (employee, selectedEmployeeId = null) => {
    if (!employee || !employee.id) {
      return false;
    }

    if (selectedEmployeeId && Number(employee.id) === Number(selectedEmployeeId)) {
      return true;
    }

    const alertCode = String(employee.contractAlertCode || "").trim().toUpperCase();
    return alertCode === "SIN_CONTRATO" || alertCode === "POR_VENCER";
  };

  const normalizeContractCatalogs = (catalogs = {}, selectedEmployeeId = null) => ({
    ...catalogs,
    employees: Array.isArray(catalogs.employees)
      ? catalogs.employees.filter((employee) =>
          isEligibleContractEmployee(employee, selectedEmployeeId),
        )
      : [],
  });

  const syncClockSelection = () => {
    if (!state.clock.rows.length) {
      state.clock.selectedIndex = -1;
      return;
    }

    if (state.clock.selectedIndex < 0 || state.clock.selectedIndex >= state.clock.rows.length) {
      state.clock.selectedIndex = 0;
    }
  };

  const syncReportSelection = () => {
    if (!state.reports.rows.length) {
      state.reports.selectedIndex = -1;
      return;
    }

    if (state.reports.selectedIndex < 0 || state.reports.selectedIndex >= state.reports.rows.length) {
      state.reports.selectedIndex = 0;
    }
  };

  const syncAuditSelection = () => {
    if (!state.audit.rows.length) {
      state.audit.selectedIndex = -1;
      return;
    }

    if (state.audit.selectedIndex < 0 || state.audit.selectedIndex >= state.audit.rows.length) {
      state.audit.selectedIndex = 0;
    }
  };

  const findStructureNodeById = (nodes, nodeId) => {
    const targetId = Number(nodeId || 0);
    if (!targetId) {
      return null;
    }

    const walk = (items) => {
      for (const node of items || []) {
        if (Number(node.idNodoEstructura) === targetId) {
          return node;
        }

        const child = walk(node.children || []);
        if (child) {
          return child;
        }
      }

      return null;
    };

    return walk(nodes);
  };

  const findFirstStructureNode = (nodes) => {
    for (const node of nodes || []) {
      if (node) {
        return node;
      }
    }

    return null;
  };

  const getStructureFilteredTree = () => {
    return Array.isArray(state.structure.tree) ? state.structure.tree : [];
  };

  const syncStructureSelection = (nodes = getStructureFilteredTree()) => {
    if (!Array.isArray(nodes) || !nodes.length) {
      state.structure.selectedId = null;
      return;
    }

    if (!findStructureNodeById(nodes, state.structure.selectedId)) {
      state.structure.selectedId = findFirstStructureNode(nodes)?.idNodoEstructura || null;
    }
  };

  const getSelectedEmployee = () =>
    state.employees.details[state.employees.selectedId] ||
    state.employees.items.find((item) => Number(item.idEmpleado) === Number(state.employees.selectedId)) ||
    null;

  const getSelectedContract = () =>
    state.contracts.items.find((item) => Number(item.idContrato) === Number(state.contracts.selectedId)) || null;

  const getSelectedWorkflowRecord = () => {
    const config = getWorkflowConfig();
    if (!config) {
      return null;
    }

    return (
      state.workflows.items.find((item) => Number(item[config.idField]) === Number(state.workflows.selectedId)) ||
      null
    );
  };

  const getSelectedCatalogRecord = () =>
    state.catalogsAdmin.items.find((item) => Number(item.idCatalogo) === Number(state.catalogsAdmin.selectedId)) ||
    null;

  const getSelectedActionRecord = () =>
    state.actions.items.find((item) => Number(item.idAccionPersonal) === Number(state.actions.selectedId)) || null;

  const getSelectedDocumentRecord = () =>
    state.documents.items.find((item) => Number(item.idExpedienteDocumento) === Number(state.documents.selectedId)) ||
    null;

  const getActiveWorkflowBucket = () => {
    const activeModule = getActiveModule();
    if (!activeModule) {
      return null;
    }

    if (activeModule.type === "catalog") {
      return state.catalogsAdmin;
    }

    if (activeModule.type === "action") {
      return state.actions;
    }

    if (activeModule.type === "document") {
      return state.documents;
    }

    if (activeModule.type === "workflow") {
      return state.workflows;
    }

    return null;
  };

  const getSelectedClockRow = () => state.clock.rows[state.clock.selectedIndex] || null;

  const getSelectedReportRow = () => state.reports.rows[state.reports.selectedIndex] || null;

  const getSelectedStructureNode = () => {
    const walk = (nodes) => {
      for (const node of nodes || []) {
        if (Number(node.idNodoEstructura) === Number(state.structure.selectedId)) {
          return node;
        }

        const child = walk(node.children || []);
        if (child) {
          return child;
        }
      }

      return null;
    };

    return walk(state.structure.tree);
  };

  const getSelectedAuditRow = () => state.audit.rows[state.audit.selectedIndex] || null;

  const redirectToLogin = () => {
    window.location.href = "/App/Login";
  };

  const redirectToDashboard = () => {
    window.location.href = "/App/Dashboard";
  };

  const canEditWorkflowRecord = (record) => {
    const config = getWorkflowConfig();
    if (!config || !record) {
      return false;
    }

    return config.getActionState(record) === config.pendingStatus;
  };

  const render = () => {
    const groups = model.getGroups();
    const group = getActiveGroup();
    const activeModule = getActiveModule();

    view.renderMainNav(groups, state.activeGroupId);

    if (!activeModule) {
      view.setWorkspaceHeader({
        kicker: "Modulo",
        title: group.label,
        subtitle: group.id === DEFAULT_GROUP_ID ? "Vista ejecutiva, alertas y accesos operativos del modulo." : "Selecciona un bloque.",
        trail: "",
        showBack: false,
      });
      view.renderGroupBoard(group, state.dashboard.overview, state.dashboard.busy);
      view.showBoard();
      return;
    }

    if (activeModule.id === "empleado") {
      view.setWorkspaceHeader({
        kicker: "Gestion",
        title: activeModule.label,
        subtitle: "Altas, actualizaciones, consulta y baja controlada.",
        trail: "",
        showBack: true,
      });
      view.showEmployeeShell();
      view.elements.searchInput.value = state.employees.search;
      view.renderStatusOptions(model.getStatusOptions(), state.employees.status);
      view.renderTable(state.employees.items, state.employees.selectedId);
      view.renderDetail(getSelectedEmployee());
      view.setActionState({
        hasSelection: Boolean(getSelectedEmployee()),
        busy: state.employees.busy,
        detailVisible: state.employees.detailVisible && Boolean(getSelectedEmployee()),
      });
      return;
    }

    if (activeModule.id === "contrato") {
      view.setWorkspaceHeader({
        kicker: "Gestion",
        title: activeModule.label,
        subtitle: "Vigencias, impresion y administracion contractual.",
        trail: "",
        showBack: true,
      });
      view.showContractShell();
      view.elements.contractSearchInput.value = state.contracts.search;
      view.renderContractStatusOptions(model.getContractStatusOptions(), state.contracts.status);
      view.renderContractTable(state.contracts.items, state.contracts.selectedId);
      view.renderContractDetail(getSelectedContract());
      view.setContractActionState({
        hasSelection: Boolean(getSelectedContract()),
        busy: state.contracts.busy,
      });
      return;
    }

    if (activeModule.type === "config") {
      view.setWorkspaceHeader({
        kicker: "Configuracion",
        title: "Configuraciones RRHH",
        subtitle: "Catalogos base y parametros administrativos del modulo.",
        trail: "",
        showBack: true,
      });
      view.showConfigShell();
      view.renderConfigShell(group);
      return;
    }

    if (activeModule.type === "catalog") {
      const selected = getSelectedCatalogRecord();
      view.setWorkspaceHeader({
        kicker: "Catalogo",
        title: activeModule.label,
        subtitle: "Configuracion base, activacion e inactivacion controlada.",
        trail: "",
        showBack: true,
      });
      view.showWorkflowShell();
      view.configureWorkflowShell({
        searchPlaceholder: "Buscar por codigo, nombre o descripcion",
        newLabel: "Nuevo",
        editLabel: "Editar",
        showApprove: false,
        rejectLabel: "Eliminar",
        showExtra: false,
      });
      view.elements.workflowSearchInput.value = state.catalogsAdmin.search;
      view.renderWorkflowStatusOptions(model.getCatalogStatusOptions(), state.catalogsAdmin.status);
      view.renderCatalogTable(activeModule.id, activeModule.label, state.catalogsAdmin.items, state.catalogsAdmin.selectedId);
      view.renderCatalogDetail(activeModule.id, selected);
      view.setWorkflowActionState({
        hasSelection: Boolean(selected),
        busy: state.catalogsAdmin.busy,
        canEdit: Boolean(selected),
        canResolve: Boolean(selected),
        showApprove: false,
        showReject: true,
        detailVisible: state.catalogsAdmin.detailVisible && Boolean(selected),
      });
      return;
    }

    if (activeModule.type === "action") {
      const selected = getSelectedActionRecord();
      view.setWorkspaceHeader({
        kicker: "Gestion",
        title: activeModule.label,
        subtitle: "Promociones, traslados, cambios internos y memo formal del colaborador.",
        trail: "",
        showBack: true,
      });
      view.showWorkflowShell();
      view.configureWorkflowShell({
        searchPlaceholder: "Buscar por empleado, tipo o descripcion",
        newLabel: "Nuevo",
        editLabel: "Editar",
        showApprove: false,
        rejectLabel: "Eliminar",
        extraLabel: "Imprimir memo",
        extraIcon: "print",
        showExtra: true,
      });
      view.elements.workflowSearchInput.value = state.actions.search;
      view.renderWorkflowStatusOptions(model.getActionStatusOptions(), state.actions.status);
      view.renderActionTable(state.actions.items, state.actions.selectedId);
      view.renderActionDetail(selected);
      view.setWorkflowActionState({
        hasSelection: Boolean(selected),
        busy: state.actions.busy,
        canEdit: Boolean(selected),
        canResolve: Boolean(selected),
        showApprove: false,
        showReject: true,
        showExtra: true,
        canExtra: Boolean(selected),
        extraNeedsSelection: true,
        detailVisible: state.actions.detailVisible && Boolean(selected),
      });
      return;
    }

    if (activeModule.type === "document") {
      const selected = getSelectedDocumentRecord();
      view.setWorkspaceHeader({
        kicker: "Expediente",
        title: activeModule.label,
        subtitle: "Documentos del colaborador, vencimientos y consulta del archivo adjunto.",
        trail: "",
        showBack: true,
      });
      view.showWorkflowShell();
      view.configureWorkflowShell({
        searchPlaceholder: "Buscar por empleado, documento, archivo u observacion",
        newLabel: "Nuevo",
        editLabel: "Editar",
        approveLabel: "Abrir archivo",
        rejectLabel: "Eliminar",
        showApprove: true,
        showReject: true,
        showExtra: false,
      });
      view.elements.workflowSearchInput.value = state.documents.search;
      view.renderWorkflowStatusOptions(model.getDocumentStatusOptions(), state.documents.status);
      view.renderDocumentTable(state.documents.items, state.documents.selectedId);
      view.renderDocumentDetail(selected);
      view.setWorkflowActionState({
        hasSelection: Boolean(selected),
        busy: state.documents.busy,
        canEdit: Boolean(selected),
        canResolve: Boolean(selected?.tieneArchivo),
        canApprove: Boolean(selected?.tieneArchivo),
        canReject: Boolean(selected),
        showApprove: true,
        showReject: true,
        detailVisible: state.documents.detailVisible && Boolean(selected),
      });
      return;
    }

    if (activeModule.type === "workflow") {
      const selected = getSelectedWorkflowRecord();
      const showExtra = activeModule.id === "vacacion";
      view.setWorkspaceHeader({
        kicker: "Flujo",
        title: activeModule.label,
        subtitle: "Solicitud, revision y resolucion operativa.",
        trail: "",
        showBack: true,
      });
      view.showWorkflowShell();
      view.configureWorkflowShell({
        searchPlaceholder: "Buscar por empleado, tipo o estado",
        newLabel: "Nuevo",
        editLabel: "Editar",
        approveLabel: "Aprobar",
        rejectLabel: "Rechazar",
        showApprove: true,
        showReject: true,
        extraLabel: showExtra ? "Ajuste masivo" : "Configurar",
        extraIcon: showExtra ? "settings" : "settings",
        showExtra,
      });
      view.elements.workflowSearchInput.value = state.workflows.search;
      view.renderWorkflowStatusOptions(model.getWorkflowStatusOptions(), state.workflows.status);
      view.renderWorkflowTable(activeModule.id, activeModule.label, state.workflows.items, state.workflows.selectedId);
      view.renderWorkflowDetail(activeModule.id, selected);
      view.setWorkflowActionState({
        hasSelection: Boolean(selected),
        busy: state.workflows.busy,
        canEdit: canEditWorkflowRecord(selected),
        canResolve: canEditWorkflowRecord(selected),
        showExtra,
        canExtra: showExtra,
        detailVisible: state.workflows.detailVisible && Boolean(selected),
      });
      return;
    }

    if (isClockLikeModule(activeModule)) {
      const isHoursReport = isHoursReportModule(activeModule);
      view.setWorkspaceHeader({
        kicker: isHoursReport ? "Reportes" : "Control",
        title: activeModule.label,
        subtitle: isHoursReport
          ? "Dashboard de asistencia, horas extra y diferencias contra la jornada esperada."
          : "Marcaciones, horas trabajadas y exportacion.",
        trail: "",
        showBack: true,
      });
      view.showClockShell();
      view.configureClockShell({
        panelKicker: isHoursReport ? "Dashboard" : "Control",
        panelTitle: isHoursReport ? "Horas trabajadas por periodo" : "Jornadas marcadas",
      });
      view.elements.clockSearchInput.value = state.clock.search;
      view.elements.clockDateFrom.value = state.clock.dateFrom;
      view.elements.clockDateTo.value = state.clock.dateTo;
      view.renderClockEmployeeOptions(state.clock.catalogs.employees || [], state.clock.idEmpleado);
      view.renderClockDashboard(state.clock.rows, {
        isReport: isHoursReport,
        dateFrom: state.clock.dateFrom,
        dateTo: state.clock.dateTo,
      });
      view.renderClockTable(state.clock.rows, state.clock.selectedIndex);
      view.renderClockDetail(getSelectedClockRow(), state.clock.branding);
      view.setClockActionState({
        busy: state.clock.busy,
        hasRows: state.clock.rows.length > 0,
      });
      return;
    }

    if (activeModule.type === "report") {
      view.setWorkspaceHeader({
        kicker: "Reportes",
        title: activeModule.label,
        subtitle: "Consulta consolidada del saldo de vacaciones por colaborador.",
        trail: "",
        showBack: true,
      });
      view.showReportShell();
      view.elements.reportSearchInput.value = state.reports.search;
      view.elements.reportCutoffDate.value = state.reports.cutoffDate;
      view.renderReportDepartmentOptions(state.reports.catalogs.departments || [], state.reports.idDepartamento);
      view.renderReportEmployeeStatusOptions(REPORT_EMPLOYMENT_STATUS_OPTIONS, state.reports.status);
      view.renderReportTable(state.reports.rows, state.reports.selectedIndex);
      view.renderReportDetail(getSelectedReportRow(), state.reports.branding);
      view.setReportActionState({
        busy: state.reports.busy,
        hasRows: state.reports.rows.length > 0,
      });
      return;
    }

    if (activeModule.type === "structure") {
      const filteredTree = getStructureFilteredTree();
      syncStructureSelection(filteredTree);
      const selectedNode = findStructureNodeById(filteredTree, state.structure.selectedId);

      view.setWorkspaceHeader({
        kicker: "Organigrama",
        title: activeModule.label,
        subtitle:
          state.structure.generalManagementName
            ? `Estructura formal institucional segmentada por ramas. Referente principal: ${state.structure.generalManagementName}.`
            : "Estructura formal institucional con nodos, titulares y vacantes.",
        trail: "",
        showBack: true,
      });
      view.showStructureShell();
      view.elements.structureSearchInput.value = state.structure.search;
      view.renderStructureDepartmentOptions(state.structure.catalogs.departments || [], state.structure.idDepartamento);
      view.renderStructureFilters(state.structure.branches, state.structure.branchKey);
      view.renderStructureSummary(state.structure.summary);
      view.renderStructureTree(filteredTree, state.structure.selectedId);
      view.renderStructureDetail(selectedNode);
      view.setStructureActionState({
        busy: state.structure.busy,
        hasSelection: Boolean(selectedNode),
      });
      return;
    }

    if (activeModule.type === "audit") {
      view.setWorkspaceHeader({
        kicker: "Auditoria",
        title: activeModule.label,
        subtitle: "Movimientos, trazabilidad y control historico del modulo.",
        trail: "",
        showBack: true,
      });
      view.showAuditShell();
      view.elements.auditSearchInput.value = state.audit.search;
      view.elements.auditDateFrom.value = state.audit.dateFrom;
      view.elements.auditDateTo.value = state.audit.dateTo;
      view.renderAuditProcessOptions(state.audit.catalogs.processes || [], state.audit.process);
      view.renderAuditTable(state.audit.rows, state.audit.selectedIndex);
      view.renderAuditDetail(getSelectedAuditRow());
      view.setAuditActionState({
        busy: state.audit.busy,
      });
      return;
    }

    view.setWorkspaceHeader({
      kicker: activeModule.bucketLabel,
      title: activeModule.label,
      subtitle: activeModule.subtitle,
      trail: "",
      showBack: true,
    });
    view.renderPlaceholder(activeModule);
  };

  const requestUsernamePreview = async () => {
    const nombres = view.elements.nombres?.value.trim() || "";
    const apellidos = view.elements.apellidos?.value.trim() || "";

    if (!nombres || !apellidos) {
      view.elements.usuarioSistema.value = "";
      return;
    }

    const requestId = ++state.employees.usernamePreviewRequestId;

    try {
      const payload = await sessionApi.request(
        `/Seguridad/SugerirUsuario?nombres=${encodeURIComponent(nombres)}&apellidos=${encodeURIComponent(apellidos)}`,
      );

      if (payload?.ok === false || requestId !== state.employees.usernamePreviewRequestId) {
        return;
      }

      view.elements.usuarioSistema.value = payload?.data?.usuario || "";
    } catch {
      // The backend still validates the final username on save.
    }
  };

  const scheduleUsernamePreview = () => {
    if (view.elements.employeeId?.value) {
      return;
    }

    window.clearTimeout(state.employees.usernamePreviewTimer);
    state.employees.usernamePreviewTimer = window.setTimeout(requestUsernamePreview, 220);
  };

  const invalidateDashboard = () => {
    state.dashboard.loaded = false;
  };

  const refreshDashboard = async (showToast = false) => {
    if (state.activeGroupId !== DEFAULT_GROUP_ID) {
      return;
    }

    state.dashboard.busy = true;
    render();

    try {
      state.dashboard.overview = await dashboardService.getOverview();
      state.dashboard.loaded = true;

      if (showToast) {
        view.showToast("Tablero de RRHH actualizado.", "success");
      }
    } catch (error) {
      if (showToast || !state.dashboard.overview) {
        view.showToast(error.message || "No se pudo cargar el tablero de RRHH.", "danger");
      }
    } finally {
      state.dashboard.busy = false;
      render();
    }
  };

  const ensureBoardDataLoaded = async () => {
    if (state.activeGroupId !== DEFAULT_GROUP_ID || state.activeModuleId || state.dashboard.loaded || state.dashboard.busy) {
      return;
    }

    await refreshDashboard(false);
  };

  const loadEmployeeDetail = async (employeeId, suppressErrors = true) => {
    const id = Number(employeeId || 0);
    if (!id) {
      return null;
    }

    const cached = state.employees.details[id];
    if (cached?.resumenLaboral) {
      return cached;
    }

    try {
      const detail = await employeeService.get(id);
      if (detail?.idEmpleado) {
        state.employees.details[detail.idEmpleado] = detail;
      }

      if (Number(state.employees.selectedId) === id) {
        render();
      }

      return detail;
    } catch (error) {
      if (!suppressErrors) {
        view.showToast(error.message || "No se pudo cargar el resumen del empleado.", "danger");
      }

      return null;
    }
  };

  const loadEmployees = async () => {
    if (getActiveModule()?.id === "empleado") {
      view.renderTableLoading();
    }

    const list = await employeeService.list({
      search: state.employees.search,
      status: state.employees.status,
    });

    state.employees.items = Array.isArray(list) ? list : [];
    syncSelectedId(state.employees.items, state.employees, "idEmpleado");

    const currentIds = new Set(state.employees.items.map((item) => Number(item.idEmpleado)));
    Object.keys(state.employees.details).forEach((key) => {
      if (!currentIds.has(Number(key))) {
        delete state.employees.details[key];
      }
    });
  };

  const refreshEmployeeData = async (showToast = false) => {
    state.employees.busy = true;
    render();

    try {
      const [catalogs] = await Promise.all([
        employeeService.getCatalogs().then((data) => {
          state.employees.catalogs = data;
          return data;
        }),
        loadEmployees(),
      ]);

      state.employees.catalogs = catalogs || state.employees.catalogs;
      await loadEmployeeDetail(state.employees.selectedId, true);
      render();

      if (showToast) {
        view.showToast("Registros actualizados.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudieron cargar los registros.", "danger");
    } finally {
      state.employees.busy = false;
      render();
    }
  };

  const loadContracts = async () => {
    if (getActiveModule()?.id === "contrato") {
      view.renderContractTableLoading();
    }

    const list = await contractService.list({
      search: state.contracts.search,
      status: state.contracts.status,
    });

    state.contracts.items = Array.isArray(list) ? list : [];
    syncSelectedId(state.contracts.items, state.contracts, "idContrato");
  };

  const refreshContractData = async (showToast = false) => {
    state.contracts.busy = true;
    render();

    try {
      const [catalogs] = await Promise.all([
        contractService.getCatalogs().then((data) => {
          const normalized = normalizeContractCatalogs(data);
          state.contracts.catalogs = normalized;
          return normalized;
        }),
        loadContracts(),
      ]);

      state.contracts.catalogs = catalogs || state.contracts.catalogs;
      render();

      if (showToast) {
        view.showToast("Contratos actualizados.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudieron cargar los contratos.", "danger");
    } finally {
      state.contracts.busy = false;
      render();
    }
  };

  const loadWorkflows = async () => {
    const activeModule = getActiveModule();
    const config = getWorkflowConfig();

    if (!activeModule || !config) {
      return;
    }

    view.renderWorkflowTableLoading(activeModule.label);

    const list = await config.list({
      search: state.workflows.search,
      status: state.workflows.status,
    });

    state.workflows.items = Array.isArray(list) ? list : [];
    state.workflows.loadedModuleId = activeModule.id;
    syncSelectedId(state.workflows.items, state.workflows, config.idField);
  };

  const refreshWorkflowData = async (showToast = false) => {
    const activeModule = getActiveModule();
    const config = getWorkflowConfig();

    if (!activeModule || !config) {
      return;
    }

    state.workflows.busy = true;
    render();

    try {
      const [catalogs] = await Promise.all([
        novedadesService.getCatalogs().then((data) => {
          state.workflows.catalogs = data;
          return data;
        }),
        loadWorkflows(),
      ]);

      state.workflows.catalogs = catalogs || state.workflows.catalogs;
      render();

      if (showToast) {
        view.showToast(`${activeModule.label} actualizados.`, "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || `No se pudieron cargar ${activeModule.label.toLowerCase()}.`, "danger");
    } finally {
      state.workflows.busy = false;
      render();
    }
  };

  const loadCatalogRecords = async () => {
    const activeModule = getActiveModule();
    if (!activeModule || activeModule.type !== "catalog") {
      return;
    }

    const colspan = activeModule.id === "cargo" || activeModule.id === "horario_laboral" ? 5 : 4;
    view.renderWorkflowTableLoading(activeModule.label, colspan);

    const list = await catalogService.list({
      moduleId: activeModule.id,
      search: state.catalogsAdmin.search,
      status: state.catalogsAdmin.status,
    });

    state.catalogsAdmin.items = Array.isArray(list) ? list : [];
    state.catalogsAdmin.loadedModuleId = activeModule.id;
    syncSelectedId(state.catalogsAdmin.items, state.catalogsAdmin, "idCatalogo");
  };

  const refreshCatalogData = async (showToast = false) => {
    const activeModule = getActiveModule();
    if (!activeModule || activeModule.type !== "catalog") {
      return;
    }

    state.catalogsAdmin.busy = true;
    render();

    try {
      const [catalogs] = await Promise.all([
        catalogService.getCatalogs().then((data) => {
          state.catalogsAdmin.catalogs = data || state.catalogsAdmin.catalogs;
          return data;
        }),
        loadCatalogRecords(),
      ]);

      state.catalogsAdmin.catalogs = catalogs || state.catalogsAdmin.catalogs;
      render();

      if (showToast) {
        view.showToast(`${activeModule.label} actualizados.`, "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || `No se pudieron cargar ${activeModule.label.toLowerCase()}.`, "danger");
    } finally {
      state.catalogsAdmin.busy = false;
      render();
    }
  };

  const loadActions = async () => {
    view.renderWorkflowTableLoading("Acciones de personal", 5);

    const list = await personnelActionService.list({
      search: state.actions.search,
      status: state.actions.status,
    });

    state.actions.items = Array.isArray(list) ? list : [];
    syncSelectedId(state.actions.items, state.actions, "idAccionPersonal");
  };

  const refreshActionData = async (showToast = false) => {
    state.actions.busy = true;
    render();

    try {
      const [catalogs] = await Promise.all([
        personnelActionService.getCatalogs().then((data) => {
          state.actions.catalogs = data || state.actions.catalogs;
          return data;
        }),
        loadActions(),
      ]);

      state.actions.catalogs = catalogs || state.actions.catalogs;
      render();

      if (showToast) {
        view.showToast("Acciones de personal actualizadas.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudieron cargar las acciones de personal.", "danger");
    } finally {
      state.actions.busy = false;
      render();
    }
  };

  const escapePrintHtml = (value) =>
    String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/\"/g, "&quot;")
      .replace(/'/g, "&#39;");

  const printSelectedActionMemo = () => {
    const record = getSelectedActionRecord();
    if (!record) {
      view.showToast("Selecciona una accion de personal.", "warning");
      return;
    }

    const printWindow = window.open("", "_blank", "noopener,noreferrer,width=980,height=760");
    if (!printWindow) {
      view.showToast("El navegador bloqueo la ventana de impresion.", "warning");
      return;
    }

    const memoHtml = String(record.memorandumTexto || record.descripcionAccion || "-")
      .split(/\r?\n/)
      .map((line) => `<p>${escapePrintHtml(line || " ")}</p>`)
      .join("");

    printWindow.document.write(`
      <!DOCTYPE html>
      <html lang="es">
        <head>
          <meta charset="UTF-8" />
          <title>Memorandum ${escapePrintHtml(record.codigoEmpleado || "")}</title>
          <style>
            body { font-family: "Segoe UI", Arial, sans-serif; margin: 32px; color: #10202d; }
            .sheet { max-width: 820px; margin: 0 auto; }
            .head { display: grid; gap: 8px; margin-bottom: 24px; }
            .head h1 { margin: 0; font-size: 24px; }
            .meta { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px 18px; margin-bottom: 22px; }
            .meta div { padding: 10px 12px; border: 1px solid #d6dee2; border-radius: 12px; }
            .meta strong, .body strong { display: block; margin-bottom: 4px; }
            .body { border: 1px solid #d6dee2; border-radius: 16px; padding: 18px; line-height: 1.65; }
            .body p { margin: 0 0 12px; }
          </style>
        </head>
        <body>
          <main class="sheet">
            <header class="head">
              <span>Capital Humano</span>
              <h1>Memorandum de accion de personal</h1>
              <span>${escapePrintHtml(model.formatShortDate(record.fechaAccion))}</span>
            </header>
            <section class="meta">
              <div><strong>Empleado</strong><span>${escapePrintHtml(record.nombreEmpleado || "-")}</span></div>
              <div><strong>Codigo</strong><span>${escapePrintHtml(record.codigoEmpleado || "-")}</span></div>
              <div><strong>Accion</strong><span>${escapePrintHtml(record.tipoAccion || "-")}</span></div>
              <div><strong>Cargo actual</strong><span>${escapePrintHtml(record.nombreCargo || "-")}</span></div>
              <div><strong>Nuevo cargo</strong><span>${escapePrintHtml(record.nombreCargoNuevo || "-")}</span></div>
              <div><strong>Departamento</strong><span>${escapePrintHtml(record.nombreDepartamento || "-")}</span></div>
            </section>
            <section class="body">
              ${memoHtml}
            </section>
          </main>
          <script>
            window.addEventListener("load", () => {
              window.print();
            });
          </script>
        </body>
      </html>
    `);
    printWindow.document.close();
  };

  const loadDocuments = async () => {
    view.renderWorkflowTableLoading("Expedientes", 5);

    const list = await expedienteService.list({
      search: state.documents.search,
      status: state.documents.status,
    });

    state.documents.items = Array.isArray(list) ? list : [];
    syncSelectedId(state.documents.items, state.documents, "idExpedienteDocumento");
  };

  const refreshDocumentData = async (showToast = false) => {
    state.documents.busy = true;
    render();

    try {
      const [catalogs] = await Promise.all([
        expedienteService.getCatalogs().then((data) => {
          state.documents.catalogs = data || state.documents.catalogs;
          return data;
        }),
        loadDocuments(),
      ]);

      state.documents.catalogs = catalogs || state.documents.catalogs;
      render();

      if (showToast) {
        view.showToast("Expedientes actualizados.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudieron cargar los expedientes.", "danger");
    } finally {
      state.documents.busy = false;
      render();
    }
  };

  const refreshActiveWorkflowShellData = async (showToast = false) => {
    const activeModule = getActiveModule();
    if (!activeModule) {
      return;
    }

    if (activeModule.type === "workflow") {
      await refreshWorkflowData(showToast);
      return;
    }

    if (activeModule.type === "catalog") {
      await refreshCatalogData(showToast);
      return;
    }

    if (activeModule.type === "action") {
      await refreshActionData(showToast);
      return;
    }

    if (activeModule.type === "document") {
      await refreshDocumentData(showToast);
    }
  };

  const loadClockRows = async () => {
    if (isClockLikeModule(getActiveModule())) {
      view.renderClockTableLoading();
    }

    const payload = await clockService.getSummary({
      search: state.clock.search,
      dateFrom: state.clock.dateFrom,
      dateTo: state.clock.dateTo,
      idEmpleado: state.clock.idEmpleado || null,
    });

    state.clock.rows = Array.isArray(payload?.rows) ? payload.rows : [];
    state.clock.branding = payload?.branding || state.clock.branding;
    syncClockSelection();
  };

  const loadVacationReportRows = async () => {
    if (getActiveModule()?.id === "reporte_vacaciones_disponibles") {
      view.renderReportTableLoading();
    }

    const payload = await novedadesService.getVacationAvailabilityReport({
      search: state.reports.search,
      fechaCorte: state.reports.cutoffDate,
      idDepartamento: state.reports.idDepartamento || null,
      status: state.reports.status,
    });

    state.reports.rows = Array.isArray(payload?.rows) ? payload.rows : [];
    state.reports.catalogs = {
      departments: Array.isArray(payload?.departments) ? payload.departments : [],
    };
    state.reports.branding = payload?.branding || state.reports.branding;
    syncReportSelection();
  };

  const refreshClockData = async (showToast = false) => {
    state.clock.busy = true;
    render();

    try {
      const [catalogs] = await Promise.all([
        clockService.getCatalogs().then((data) => {
          state.clock.catalogs = { employees: data?.employees || [] };
          state.clock.branding = data?.branding || state.clock.branding;
          return data;
        }),
        loadClockRows(),
      ]);

      if (!state.clock.catalogs.employees.some((employee) => String(employee.id) === String(state.clock.idEmpleado))) {
        state.clock.idEmpleado = "";
      }

      if (catalogs?.branding) {
        state.clock.branding = catalogs.branding;
      }

      render();

      if (showToast) {
        view.showToast("Reporte de reloj actualizado.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudo cargar el reporte del reloj.", "danger");
    } finally {
      state.clock.busy = false;
      render();
    }
  };

  const refreshVacationReportData = async (showToast = false) => {
    state.reports.busy = true;
    render();

    try {
      await loadVacationReportRows();
      render();

      if (showToast) {
        view.showToast("Reporte de vacaciones actualizado.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudo cargar el reporte de vacaciones.", "danger");
    } finally {
      state.reports.busy = false;
      render();
    }
  };

  const loadAuditRows = async () => {
    if (getActiveModule()?.id === "bitacora_rrhh") {
      view.renderAuditTableLoading();
    }

    const payload = await dashboardService.getAuditLog({
      search: state.audit.search,
      process: state.audit.process,
      dateFrom: state.audit.dateFrom,
      dateTo: state.audit.dateTo,
    });

    state.audit.rows = Array.isArray(payload?.rows) ? payload.rows : [];
    state.audit.catalogs = {
      processes: Array.isArray(payload?.processes) ? payload.processes : [],
    };
    syncAuditSelection();
  };

  const refreshAuditData = async (showToast = false) => {
    state.audit.busy = true;
    render();

    try {
      await loadAuditRows();
      render();

      if (showToast) {
        view.showToast("Bitacora de RRHH actualizada.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudo cargar la bitacora de RRHH.", "danger");
    } finally {
      state.audit.busy = false;
      render();
    }
  };

  const getStructureBranchNodeId = () => {
    if (state.structure.branchKey === "TODOS") {
      return "";
    }

    const normalized = String(state.structure.branchKey || "").replace("NODE-", "");
    const branchId = Number(normalized);
    return branchId > 0 ? String(branchId) : "";
  };

  const refreshStructureCatalogs = async () => {
    const payload = await structureService.getCatalogs();
    state.structure.catalogs = {
      departments: Array.isArray(payload?.departments) ? payload.departments : [],
      nodeTypes: Array.isArray(payload?.nodeTypes) ? payload.nodeTypes : [],
      employees: Array.isArray(payload?.employees) ? payload.employees : [],
      positions: Array.isArray(payload?.positions) ? payload.positions : [],
      parentNodes: Array.isArray(payload?.parentNodes) ? payload.parentNodes : [],
    };
    state.structure.catalogsLoaded = true;
  };

  const refreshStructureData = async (showToast = false) => {
    state.structure.busy = true;
    render();

    try {
      if (!state.structure.catalogsLoaded) {
        await refreshStructureCatalogs();
      }

      const payload = await structureService.getTree({
        search: state.structure.search,
        idDepartamento: state.structure.idDepartamento,
        idNodoGerencia: getStructureBranchNodeId(),
      });
      state.structure.summary = payload?.summary || null;
      state.structure.branches = Array.isArray(payload?.branches) ? payload.branches : [];
      state.structure.tree = Array.isArray(payload?.tree) ? payload.tree : [];
      state.structure.generalManagementName = payload?.generalManagementName || "";
      state.structure.loaded = true;

      if (
        state.structure.branchKey !== "TODOS" &&
        !state.structure.branches.some((branch) => String(branch.key) === String(state.structure.branchKey))
      ) {
        state.structure.branchKey = "TODOS";
      }

      syncStructureSelection();
      render();

      if (showToast) {
        view.showToast("Organigrama formal actualizado.", "success");
      }
    } catch (error) {
      render();
      view.showToast(error.message || "No se pudo cargar el organigrama formal.", "danger");
    } finally {
      state.structure.busy = false;
      render();
    }
  };

  const ensureActiveModuleLoaded = async () => {
    const activeModule = getActiveModule();
    if (!activeModule) {
      return;
    }

    if (activeModule.id === "empleado" && !state.employees.items.length && !state.employees.busy) {
      await refreshEmployeeData(false);
      return;
    }

    if (activeModule.id === "contrato" && !state.contracts.items.length && !state.contracts.busy) {
      await refreshContractData(false);
      return;
    }

    if (activeModule.type === "workflow" && (state.workflows.loadedModuleId !== activeModule.id || !state.workflows.items.length) && !state.workflows.busy) {
      await refreshWorkflowData(false);
      return;
    }

    if (activeModule.type === "catalog" && (state.catalogsAdmin.loadedModuleId !== activeModule.id || !state.catalogsAdmin.items.length) && !state.catalogsAdmin.busy) {
      await refreshCatalogData(false);
      return;
    }

    if (activeModule.type === "action" && !state.actions.items.length && !state.actions.busy) {
      await refreshActionData(false);
      return;
    }

    if (activeModule.type === "document" && !state.documents.items.length && !state.documents.busy) {
      await refreshDocumentData(false);
      return;
    }

    if (isClockLikeModule(activeModule) && !state.clock.rows.length && !state.clock.busy) {
      await refreshClockData(false);
      return;
    }

    if (activeModule.type === "report" && !state.reports.rows.length && !state.reports.busy) {
      await refreshVacationReportData(false);
      return;
    }

    if (
      activeModule.type === "structure" &&
      (!state.structure.loaded || !state.structure.catalogsLoaded) &&
      !state.structure.busy
    ) {
      await refreshStructureData(false);
      return;
    }

    if (activeModule.type === "audit" && !state.audit.rows.length && !state.audit.busy) {
      await refreshAuditData(false);
    }
  };

  const openCreateStructureModal = async () => {
    try {
      if (!state.structure.catalogsLoaded) {
        await refreshStructureCatalogs();
      }

      view.openStructureModal({
        mode: "create",
        node: {
          ordenVisual: 0,
          activo: true,
        },
        catalogs: state.structure.catalogs,
      });
    } catch (error) {
      view.showToast(error.message || "No se pudo preparar el formulario del organigrama.", "danger");
    }
  };

  const openEditStructureModal = async () => {
    const selected = getSelectedStructureNode();
    if (!selected) {
      view.showToast("Selecciona un nodo del organigrama.", "warning");
      return;
    }

    try {
      if (!state.structure.catalogsLoaded) {
        await refreshStructureCatalogs();
      }

      const latest = await structureService.get(selected.idNodoEstructura);
      view.openStructureModal({
        mode: "edit",
        node: latest || selected,
        catalogs: state.structure.catalogs,
      });
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar el nodo seleccionado.", "danger");
    }
  };

  const buildStructurePayload = (formData) => ({
    codigoNodo: String(formData.codigoNodo || "").trim(),
    nombreNodo: String(formData.nombreNodo || "").trim(),
    tipoNodo: String(formData.tipoNodo || "").trim(),
    idNodoPadre: Number(formData.idNodoPadre || 0) || null,
    idEmpleadoTitular: Number(formData.idEmpleadoTitular || 0) || null,
    idDepartamento: Number(formData.idDepartamento || 0) || null,
    idCargo: Number(formData.idCargo || 0) || null,
    ordenVisual: Number.parseInt(String(formData.ordenVisual || "0"), 10) || 0,
    activo: Boolean(formData.activo),
    observacion: String(formData.observacion || "").trim(),
  });

  const validateStructurePayload = (payload) => {
    const errors = {};

    if (!payload.codigoNodo) {
      errors.codigoNodo = "Ingresa el codigo del nodo.";
    }

    if (!payload.tipoNodo) {
      errors.tipoNodo = "Selecciona un tipo de nodo.";
    }

    if (!payload.nombreNodo) {
      errors.nombreNodo = "Ingresa el nombre del nodo.";
    }

    if (payload.ordenVisual < 0) {
      errors.ordenVisual = "El orden visual no puede ser negativo.";
    }

    return errors;
  };

  const saveStructure = async (event) => {
    event.preventDefault();

    const formData = view.readStructureForm();
    const payload = buildStructurePayload(formData);
    const errors = validateStructurePayload(payload);
    const isEdit = Number(formData.structureNodeId || 0) > 0;

    if (Object.keys(errors).length) {
      view.setStructureFormErrors(errors);
      view.focusStructureField(Object.keys(errors)[0]);
      return;
    }

    view.setStructureFormErrors({});
    view.setStructureSaveBusy(true);

    try {
      const saved = isEdit
        ? await structureService.update(Number(formData.structureNodeId), payload)
        : await structureService.create(payload);

      view.showStructureSaveSuccess(isEdit ? "Actualizado" : "Creado");
      state.structure.selectedId = saved?.idNodoEstructura || state.structure.selectedId;
      await refreshStructureCatalogs();
      await refreshStructureData(false);
      invalidateDashboard();

      window.setTimeout(() => {
        view.closeStructureModal();
        view.showToast(
          isEdit ? "Nodo formal actualizado correctamente." : "Nodo formal creado correctamente.",
          "success",
        );
      }, 320);
    } catch (error) {
      if (error?.errors && typeof error.errors === "object") {
        view.setStructureFormErrors(error.errors);
        view.focusStructureField(Object.keys(error.errors)[0]);
      } else {
        view.showToast(error.message || "No se pudo guardar el nodo formal.", "danger");
      }
    } finally {
      view.setStructureSaveBusy(false, isEdit ? "Guardar cambios" : "Guardar");
    }
  };

  const loadStructureDemo = async () => {
    const confirmed = window.confirm(
      "Se insertara la estructura base institucional solo si la tabla formal esta vacia. Deseas continuar?",
    );
    if (!confirmed) {
      return;
    }

    state.structure.busy = true;
    render();

    try {
      const result = await structureService.loadDemo();
      state.structure.branchKey = "TODOS";
      await refreshStructureCatalogs();
      await refreshStructureData(false);
      invalidateDashboard();
      view.showToast(
        result?.insertedCount ? `Se cargaron ${result.insertedCount} nodos base de referencia.` : "Estructura base cargada.",
        "success",
      );
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar la estructura base.", "danger");
    } finally {
      state.structure.busy = false;
      render();
    }
  };

  const openCreateEmployeeModal = () => {
    view.openEmployeeModal({
      mode: "create",
      employee: model.getEmptyEmployee(state.employees.catalogs.suggestedCode),
      catalogs: state.employees.catalogs,
    });
  };

  const openEditEmployeeModal = async () => {
    const selected = getSelectedEmployee();
    if (!selected) {
      view.showToast("Selecciona un empleado.", "warning");
      return;
    }

    state.employees.busy = true;
    render();

    try {
      const employee = await employeeService.get(selected.idEmpleado);
      view.openEmployeeModal({
        mode: "edit",
        employee,
        catalogs: state.employees.catalogs,
      });
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar el empleado.", "danger");
    } finally {
      state.employees.busy = false;
      render();
    }
  };

  const saveEmployee = async (event) => {
    event.preventDefault();

    const formData = view.readEmployeeForm();
    const payload = model.buildPayload(formData);
    const errors = model.validateEmployee(payload, formData);

    if (Object.keys(errors).length) {
      view.setFormErrors(errors);
      view.focusField(Object.keys(errors)[0]);
      return;
    }

    const isEdit = Boolean(formData.employeeId);
    view.setSaveBusy(true, isEdit ? "Guardar cambios" : "Guardar");

    try {
      const employee = isEdit
        ? await employeeService.update(formData.employeeId, payload)
        : await employeeService.create(payload);

      state.employees.details[employee.idEmpleado] = employee;
      state.employees.selectedId = employee.idEmpleado;
      invalidateDashboard();
      const refreshPromise = refreshEmployeeData(false);
      view.showSaveSuccess(isEdit ? "Cambios guardados" : "Guardado con exito");
      await wait(850);
      view.closeEmployeeModal();
      view.showToast(
        isEdit
          ? "Empleado actualizado correctamente."
          : employee.usuarioSistema
            ? `Empleado creado correctamente. Usuario: ${employee.usuarioSistema}.`
            : "Empleado creado correctamente.",
        "success",
      );
      await refreshPromise;
    } catch (error) {
      if (error.errors && Object.keys(error.errors).length) {
        view.setFormErrors(error.errors);
        view.focusField(Object.keys(error.errors)[0]);
      }

      view.showToast(error.message || "No se pudo guardar el empleado.", "danger");
    } finally {
      view.setSaveBusy(false, isEdit ? "Guardar cambios" : "Guardar");
    }
  };

  const openDeleteEmployeeModal = () => {
    const selected = getSelectedEmployee();
    if (!selected) {
      view.showToast("Selecciona un empleado.", "warning");
      return;
    }

    view.openDeleteModal(selected);
  };

  const confirmDeleteEmployee = async (event) => {
    event.preventDefault();

    const selected = getSelectedEmployee();
    if (!selected) {
      view.closeDeleteModal();
      return;
    }

    const auth = view.readDeleteForm();
    if (!auth.adminUsuario || !auth.adminPassword) {
      view.setDeleteError("Ingresa usuario y contrasena de administrador.");
      return;
    }

    view.setDeleteBusy(true);
    view.setDeleteError("");

    try {
      await employeeService.remove(selected.idEmpleado, auth);
      view.closeDeleteModal();
      view.showToast("Empleado eliminado correctamente.", "success");
      delete state.employees.details[selected.idEmpleado];
      state.employees.selectedId = null;
      invalidateDashboard();
      await refreshEmployeeData(false);
    } catch (error) {
      let message = error.message || "No se pudo eliminar el empleado.";

      if (Array.isArray(error.payload?.data) && error.payload.data.length) {
        const relaciones = error.payload.data.map((item) => `${item.table} (${item.total})`).join(", ");
        message = `${message} ${relaciones}`;
      }

      view.setDeleteError(message);
    } finally {
      view.setDeleteBusy(false);
    }
  };

  const requestSuggestedContractNumber = async () => {
    const idEmpleado = Number(view.elements.contractEmployeeId?.value || 0);
    const contractId = Number(view.elements.contractId?.value || 0);

    if (!idEmpleado) {
      return;
    }

    try {
      const payload = await contractService.suggestNumber(idEmpleado, contractId || null);
      view.elements.numeroContrato.value = payload?.numeroContrato || "";
    } catch {
      // The backend validates the number when saving.
    }
  };

  const openCreateContractModal = async () => {
    if (state.contracts.busy) {
      return;
    }

    state.contracts.busy = true;
    render();

    try {
      const catalogs = normalizeContractCatalogs(await contractService.getCatalogs());
      state.contracts.catalogs = catalogs;

      view.openContractModal({
        mode: "create",
        contract: model.getEmptyContract(state.contracts.catalogs.defaultCurrency),
        catalogs: state.contracts.catalogs,
      });
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar la lista de empleados elegibles.", "danger");
    } finally {
      state.contracts.busy = false;
      render();
    }
  };

  const openEditContractModal = async () => {
    const selected = getSelectedContract();
    if (!selected) {
      view.showToast("Selecciona un contrato.", "warning");
      return;
    }

    state.contracts.busy = true;
    render();

    try {
      const contract = await contractService.get(selected.idContrato);
      const catalogs = normalizeContractCatalogs(state.contracts.catalogs, contract.idEmpleado);
      state.contracts.catalogs = catalogs;

      view.openContractModal({
        mode: "edit",
        contract,
        catalogs,
      });
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar el contrato.", "danger");
    } finally {
      state.contracts.busy = false;
      render();
    }
  };

  const saveContract = async (event) => {
    event.preventDefault();

    const formData = view.readContractForm();
    const payload = model.buildContractPayload(formData);
    const errors = model.validateContract(payload, formData);

    if (Object.keys(errors).length) {
      view.setContractFormErrors(errors);
      view.focusContractField(Object.keys(errors)[0]);
      return;
    }

    const isEdit = Boolean(formData.contractId);
    view.setContractSaveBusy(true, isEdit ? "Guardar cambios" : "Guardar");

    try {
      const contract = isEdit
        ? await contractService.update(formData.contractId, payload)
        : await contractService.create(payload);

      state.contracts.selectedId = contract.idContrato;
      upsertContractInState(contract);
      render();
      invalidateDashboard();
      const refreshPromise = refreshContractData(false);
      view.showContractSaveSuccess(isEdit ? "Cambios guardados" : "Guardado con exito");
      await wait(850);
      view.closeContractModal();
      view.showToast(
        isEdit ? "Contrato actualizado correctamente." : "Contrato registrado correctamente.",
        "success",
      );
      await refreshPromise;
    } catch (error) {
      if (error.errors && Object.keys(error.errors).length) {
        view.setContractFormErrors(error.errors);
        view.focusContractField(Object.keys(error.errors)[0]);
      }

      view.showToast(error.message || "No se pudo guardar el contrato.", "danger");
    } finally {
      view.setContractSaveBusy(false, isEdit ? "Guardar cambios" : "Guardar");
    }
  };

  const openDeleteContractModal = () => {
    const selected = getSelectedContract();
    if (!selected) {
      view.showToast("Selecciona un contrato.", "warning");
      return;
    }

    view.openContractDeleteModal(selected);
  };

  const confirmDeleteContract = async (event) => {
    event.preventDefault();

    const selected = getSelectedContract();
    if (!selected) {
      view.closeContractDeleteModal();
      return;
    }

    const auth = view.readContractDeleteForm();
    if (!auth.adminUsuario || !auth.adminPassword) {
      view.setContractDeleteError("Ingresa usuario y contrasena de administrador.");
      return;
    }

    view.setContractDeleteBusy(true);
    view.setContractDeleteError("");

    try {
      await contractService.remove(selected.idContrato, auth);
      view.closeContractDeleteModal();
      view.showToast("Contrato eliminado correctamente.", "success");
      state.contracts.selectedId = null;
      invalidateDashboard();
      await refreshContractData(false);
    } catch (error) {
      let message = error.message || "No se pudo eliminar el contrato.";

      if (Array.isArray(error.payload?.data) && error.payload.data.length) {
        const relaciones = error.payload.data.map((item) => `${item.table} (${item.total})`).join(", ");
        message = `${message} ${relaciones}`;
      }

      view.setContractDeleteError(message);
    } finally {
      view.setContractDeleteBusy(false);
    }
  };

  const buildContractPrintHtml = (payload) => {
    const company = payload?.company || {};
    const contract = payload?.contract || {};
    const generatedAt = payload?.generatedAt || new Date().toISOString();
    const isFixedTerm =
      Boolean(contract.fechaFin) ||
      /determinado|temporal|plazo fijo/i.test(String(contract.nombreTipoContrato || ""));

    const vigenciaTexto = isFixedTerm
      ? `desde el ${model.formatShortDate(contract.fechaInicio)} hasta el ${model.formatShortDate(contract.fechaFin)}`
      : `por tiempo indeterminado a partir del ${model.formatShortDate(contract.fechaInicio)}`;

    const empresaNombre = company.razonSocial || company.nombreComercial || "SIFNIC";
    const sucursalNombre = company.nombreSucursal || "Casa Matriz";
    const direccionEmpresa = company.direccionSucursal || company.direccion || "Managua, Nicaragua";

    return `
      <!DOCTYPE html>
      <html lang="es">
        <head>
          <meta charset="UTF-8" />
          <title>${escapeHtml(contract.numeroContrato || "Contrato laboral")}</title>
          <style>
            body { font-family: Arial, sans-serif; color: #1b2832; margin: 34px; line-height: 1.55; }
            .header { display: grid; gap: 6px; margin-bottom: 26px; }
            .header h1 { margin: 0; text-transform: uppercase; font-size: 22px; letter-spacing: .05em; }
            .meta { color: #586776; font-size: 12px; }
            .section { margin-top: 18px; }
            .section h2 { margin: 0 0 8px; font-size: 14px; text-transform: uppercase; letter-spacing: .08em; }
            .box { border: 1px solid #d5dde5; border-radius: 14px; padding: 14px 16px; }
            .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px 16px; }
            .row strong { display: block; font-size: 11px; text-transform: uppercase; color: #607282; margin-bottom: 4px; }
            .clause { margin: 0 0 10px; text-align: justify; }
            .signatures { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 28px; margin-top: 54px; }
            .sign-box { padding-top: 34px; border-top: 1px solid #7f8f9c; text-align: center; }
            .sign-box strong { display: block; margin-bottom: 6px; }
            .footer { margin-top: 24px; color: #607282; font-size: 11px; }
            @media print { body { margin: 18px; } }
          </style>
        </head>
        <body>
          <div class="header">
            <h1>Contrato individual de trabajo</h1>
            <div class="meta">${escapeHtml(empresaNombre)} - ${escapeHtml(sucursalNombre)}</div>
            <div class="meta">${escapeHtml(direccionEmpresa)}</div>
            <div class="meta">Documento generado el ${escapeHtml(new Date(generatedAt).toLocaleString("es-NI"))}</div>
          </div>

          <section class="section">
            <h2>Partes</h2>
            <div class="box">
              <p class="clause">
                Comparecen por una parte <strong>${escapeHtml(empresaNombre)}</strong>, en adelante
                EL EMPLEADOR, y por la otra <strong>${escapeHtml(contract.nombreEmpleado || "")}</strong>,
                identificado con cedula <strong>${escapeHtml(contract.cedulaEmpleado || "")}</strong>, en adelante
                EL TRABAJADOR. Ambas partes celebran el presente contrato laboral conforme a la legislacion
                laboral vigente en la Republica de Nicaragua.
              </p>
            </div>
          </section>

          <section class="section">
            <h2>Datos principales</h2>
            <div class="box grid">
              <div class="row"><strong>No. contrato</strong>${escapeHtml(contract.numeroContrato || "-")}</div>
              <div class="row"><strong>Tipo</strong>${escapeHtml(contract.nombreTipoContrato || "-")}</div>
              <div class="row"><strong>Cargo</strong>${escapeHtml(contract.nombreCargo || "-")}</div>
              <div class="row"><strong>Departamento</strong>${escapeHtml(contract.nombreDepartamento || "-")}</div>
              <div class="row"><strong>Horario</strong>${escapeHtml(contract.nombreHorario || "-")}</div>
              <div class="row"><strong>Jornada</strong>${escapeHtml(`${Number(contract.horasDiarias || 0).toFixed(2)} h diarias / ${Number(contract.horasSemanales || 0).toFixed(2)} h semanales`)}</div>
              <div class="row"><strong>Inicio</strong>${escapeHtml(model.formatShortDate(contract.fechaInicio))}</div>
              <div class="row"><strong>Fin</strong>${escapeHtml(contract.fechaFin ? model.formatShortDate(contract.fechaFin) : "No aplica")}</div>
              <div class="row"><strong>Salario base mensual</strong>${escapeHtml(model.formatMoney(contract.salarioBaseMensual, contract.moneda))}</div>
              <div class="row"><strong>Moneda</strong>${escapeHtml(contract.moneda || company.monedaBase || "NIO")}</div>
            </div>
          </section>

          <section class="section">
            <h2>Clausulas</h2>
            <div class="box">
              <p class="clause"><strong>Primera.</strong> EL TRABAJADOR prestara sus servicios personales en el cargo de ${escapeHtml(contract.nombreCargo || "trabajador")} para ${escapeHtml(empresaNombre)}, cumpliendo las funciones que le sean asignadas conforme a la naturaleza del puesto y al reglamento interno.</p>
              <p class="clause"><strong>Segunda.</strong> La relacion laboral tendra vigencia ${escapeHtml(vigenciaTexto)}. Cualquier prorroga, modificacion o terminacion se realizara conforme al Codigo del Trabajo de Nicaragua y demas normativa aplicable.</p>
              <p class="clause"><strong>Tercera.</strong> EL TRABAJADOR laborara bajo el horario ${escapeHtml(contract.nombreHorario || "-")}, equivalente a ${escapeHtml(`${Number(contract.horasDiarias || 0).toFixed(2)} horas diarias y ${Number(contract.horasSemanales || 0).toFixed(2)} horas semanales`)}, salvo ajustes autorizados por EL EMPLEADOR y permitidos por la ley.</p>
              <p class="clause"><strong>Cuarta.</strong> EL EMPLEADOR pagara a EL TRABAJADOR un salario base mensual de ${escapeHtml(model.formatMoney(contract.salarioBaseMensual, contract.moneda))}, sujeto a las deducciones y prestaciones legales correspondientes.</p>
              <p class="clause"><strong>Quinta.</strong> Las partes acuerdan cumplir las disposiciones de seguridad, disciplina, confidencialidad, asistencia y demas obligaciones derivadas de la relacion laboral, del reglamento interno y de la normativa nicaraguense.</p>
              <p class="clause"><strong>Sexta.</strong> Para constancia y solo para firma de ambas partes, se imprime el presente contrato en ${escapeHtml(sucursalNombre)}, ${escapeHtml(direccionEmpresa)}.</p>
              ${contract.observacion ? `<p class="clause"><strong>Observacion.</strong> ${escapeHtml(contract.observacion)}</p>` : ""}
            </div>
          </section>

          <section class="signatures">
            <div class="sign-box">
              <strong>EL EMPLEADOR</strong>
              <span>${escapeHtml(empresaNombre)}</span>
            </div>
            <div class="sign-box">
              <strong>EL TRABAJADOR</strong>
              <span>${escapeHtml(contract.nombreEmpleado || "")}</span>
            </div>
          </section>

          <div class="footer">
            Codigo interno de empleado: ${escapeHtml(contract.codigoEmpleado || "-")} - Documento generado por SIFNIC.
          </div>
        </body>
      </html>
    `;
  };

  const printContract = async () => {
    const selected = getSelectedContract();
    if (!selected) {
      view.showToast("Selecciona un contrato.", "warning");
      return;
    }

    const printWindow = window.open("", "_blank", "width=1080,height=900");
    if (!printWindow) {
      view.showToast("El navegador bloqueo la ventana de impresion.", "danger");
      return;
    }

    printWindow.document.write("<p style='font-family:Arial,sans-serif;padding:24px;'>Preparando contrato...</p>");
    printWindow.document.close();

    try {
      const payload = await contractService.document(selected.idContrato);
      const html = buildContractPrintHtml(payload);
      printWindow.document.open();
      printWindow.document.write(html);
      printWindow.document.close();
      printWindow.focus();
      window.setTimeout(() => printWindow.print(), 260);
    } catch (error) {
      printWindow.document.open();
      printWindow.document.write(
        `<p style="font-family:Arial,sans-serif;padding:24px;">${escapeHtml(
          error.message || "No se pudo preparar la impresion del contrato.",
        )}</p>`,
      );
      printWindow.document.close();
      view.showToast(error.message || "No se pudo preparar la impresion del contrato.", "danger");
    }
  };

  const getActivePanelCatalogs = () => {
    const activeModule = getActiveModule();
    if (!activeModule) {
      return {};
    }

    if (activeModule.type === "catalog") {
      return state.catalogsAdmin.catalogs;
    }

    if (activeModule.type === "action") {
      return state.actions.catalogs;
    }

    if (activeModule.type === "document") {
      return state.documents.catalogs;
    }

    return state.workflows.catalogs;
  };

  const openCreateCatalogModal = () => {
    const activeModule = getActiveModule();
    if (!activeModule || activeModule.type !== "catalog") {
      return;
    }

    view.openWorkflowModal({
      moduleId: activeModule.id,
      moduleLabel: activeModule.label,
      mode: "create",
      record: model.getEmptyCatalog(activeModule.id),
      catalogs: state.catalogsAdmin.catalogs,
    });
  };

  const openEditCatalogModal = async () => {
    const activeModule = getActiveModule();
    const selected = getSelectedCatalogRecord();
    if (!activeModule || activeModule.type !== "catalog") {
      return;
    }

    if (!selected) {
      view.showToast("Selecciona un registro.", "warning");
      return;
    }

    state.catalogsAdmin.busy = true;
    render();

    try {
      const record = await catalogService.get(activeModule.id, selected.idCatalogo);
      view.openWorkflowModal({
        moduleId: activeModule.id,
        moduleLabel: activeModule.label,
        mode: "edit",
        record,
        catalogs: state.catalogsAdmin.catalogs,
      });
    } catch (error) {
      view.showToast(error.message || `No se pudo cargar ${activeModule.label.toLowerCase()}.`, "danger");
    } finally {
      state.catalogsAdmin.busy = false;
      render();
    }
  };

  const saveCatalog = async (activeModule, formData) => {
    const payload = model.buildCatalogPayload(activeModule.id, formData);
    const errors = model.validateCatalog(activeModule.id, payload);

    if (Object.keys(errors).length) {
      view.setWorkflowFormErrors(errors);
      view.focusWorkflowField(Object.keys(errors)[0]);
      return;
    }

    const isEdit = Boolean(formData.recordId);
    view.setWorkflowSaveBusy(true, isEdit ? "Guardar cambios" : "Guardar");

    try {
      const record = isEdit
        ? await catalogService.update(formData.recordId, payload)
        : await catalogService.create(payload);

      state.catalogsAdmin.selectedId = record.idCatalogo;
      invalidateDashboard();
      const refreshPromise = refreshCatalogData(false);
      view.showWorkflowSaveSuccess(isEdit ? "Cambios guardados" : "Guardado con exito");
      await wait(850);
      view.closeWorkflowModal();
      view.showToast(
        isEdit ? `${activeModule.label} actualizados.` : `${activeModule.label} registrados correctamente.`,
        "success",
      );
      await refreshPromise;
    } catch (error) {
      if (error.errors && Object.keys(error.errors).length) {
        view.setWorkflowFormErrors(error.errors);
        view.focusWorkflowField(Object.keys(error.errors)[0]);
      }

      view.showToast(error.message || `No se pudo guardar ${activeModule.label.toLowerCase()}.`, "danger");
    } finally {
      view.setWorkflowSaveBusy(false, isEdit ? "Guardar cambios" : "Guardar");
    }
  };

  const openCreateActionModal = () => {
    view.openWorkflowModal({
      moduleId: "accion_personal",
      moduleLabel: "accion personal",
      mode: "create",
      record: model.getEmptyAction(),
      catalogs: state.actions.catalogs,
    });
  };

  const openEditActionModal = async () => {
    const selected = getSelectedActionRecord();
    if (!selected) {
      view.showToast("Selecciona un registro.", "warning");
      return;
    }

    state.actions.busy = true;
    render();

    try {
      const record = await personnelActionService.get(selected.idAccionPersonal);
      view.openWorkflowModal({
        moduleId: "accion_personal",
        moduleLabel: "accion personal",
        mode: "edit",
        record,
        catalogs: state.actions.catalogs,
      });
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar la accion de personal.", "danger");
    } finally {
      state.actions.busy = false;
      render();
    }
  };

  const saveAction = async (formData) => {
    const payload = model.buildActionPayload(formData);
    const errors = model.validateAction(payload, formData);

    if (Object.keys(errors).length) {
      view.setWorkflowFormErrors(errors);
      view.focusWorkflowField(Object.keys(errors)[0]);
      return;
    }

    const isEdit = Boolean(formData.recordId);
    view.setWorkflowSaveBusy(true, isEdit ? "Guardar cambios" : "Guardar");

    try {
      const record = isEdit
        ? await personnelActionService.update(formData.recordId, payload)
        : await personnelActionService.create(payload);

      state.actions.selectedId = record.idAccionPersonal;
      invalidateDashboard();
      const refreshPromise = refreshActionData(false);
      view.showWorkflowSaveSuccess(isEdit ? "Cambios guardados" : "Guardado con exito");
      await wait(850);
      view.closeWorkflowModal();
      view.showToast(
        isEdit ? "Accion de personal actualizada correctamente." : "Accion de personal creada correctamente.",
        "success",
      );
      await refreshPromise;
    } catch (error) {
      if (error.errors && Object.keys(error.errors).length) {
        view.setWorkflowFormErrors(error.errors);
        view.focusWorkflowField(Object.keys(error.errors)[0]);
      }

      view.showToast(error.message || "No se pudo guardar la accion de personal.", "danger");
    } finally {
      view.setWorkflowSaveBusy(false, isEdit ? "Guardar cambios" : "Guardar");
    }
  };

  const buildDocumentFormData = (payload) => {
    const data = new FormData();
    data.set("IdEmpleado", String(payload.idEmpleado || 0));
    data.set("TipoDocumento", payload.tipoDocumento || "");
    data.set("FechaDocumento", payload.fechaDocumento || "");
    data.set("FechaVencimiento", payload.fechaVencimiento || "");
    data.set("Observacion", payload.observacion || "");
    data.set("RemoverArchivo", payload.removerArchivo ? "true" : "false");

    if (payload.archivo) {
      data.set("Archivo", payload.archivo);
    }

    return data;
  };

  const openCreateDocumentModal = () => {
    view.openWorkflowModal({
      moduleId: "expediente_documento",
      moduleLabel: "expediente",
      mode: "create",
      record: model.getEmptyDocument(),
      catalogs: state.documents.catalogs,
    });
  };

  const openEditDocumentModal = async () => {
    const selected = getSelectedDocumentRecord();
    if (!selected) {
      view.showToast("Selecciona un expediente.", "warning");
      return;
    }

    state.documents.busy = true;
    render();

    try {
      const record = await expedienteService.get(selected.idExpedienteDocumento);
      view.openWorkflowModal({
        moduleId: "expediente_documento",
        moduleLabel: "expediente",
        mode: "edit",
        record,
        catalogs: state.documents.catalogs,
      });
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar el expediente.", "danger");
    } finally {
      state.documents.busy = false;
      render();
    }
  };

  const saveDocument = async (formData) => {
    const payload = model.buildDocumentPayload(formData);
    const errors = model.validateDocument(payload, formData);

    if (Object.keys(errors).length) {
      view.setWorkflowFormErrors(errors);
      view.focusWorkflowField(Object.keys(errors)[0]);
      return;
    }

    const isEdit = Boolean(formData.recordId);
    view.setWorkflowSaveBusy(true, isEdit ? "Guardar cambios" : "Guardar");

    try {
      const record = isEdit
        ? await expedienteService.update(formData.recordId, buildDocumentFormData(payload))
        : await expedienteService.create(buildDocumentFormData(payload));

      state.documents.selectedId = record.idExpedienteDocumento;
      invalidateDashboard();
      const refreshPromise = refreshDocumentData(false);
      view.showWorkflowSaveSuccess(isEdit ? "Cambios guardados" : "Guardado con exito");
      await wait(850);
      view.closeWorkflowModal();
      view.showToast(
        isEdit ? "Expediente actualizado correctamente." : "Expediente registrado correctamente.",
        "success",
      );
      await refreshPromise;
    } catch (error) {
      if (error.errors && Object.keys(error.errors).length) {
        view.setWorkflowFormErrors(error.errors);
        view.focusWorkflowField(Object.keys(error.errors)[0]);
      }

      view.showToast(error.message || "No se pudo guardar el expediente.", "danger");
    } finally {
      view.setWorkflowSaveBusy(false, isEdit ? "Guardar cambios" : "Guardar");
    }
  };

  const downloadSelectedDocument = () => {
    const selected = getSelectedDocumentRecord();
    if (!selected) {
      view.showToast("Selecciona un expediente.", "warning");
      return;
    }

    if (!selected.tieneArchivo) {
      view.showToast("El expediente seleccionado no tiene archivo adjunto.", "warning");
      return;
    }

    window.open(selected.downloadUrl || expedienteService.buildDownloadUrl(selected.idExpedienteDocumento), "_blank", "noopener");
  };

  const openGenericDeleteModalForActiveModule = () => {
    const activeModule = getActiveModule();
    if (!activeModule) {
      return;
    }

    if (activeModule.type === "catalog") {
      const selected = getSelectedCatalogRecord();
      if (!selected) {
        view.showToast("Selecciona un registro.", "warning");
        return;
      }

      state.genericDelete = {
        kind: "catalog",
        moduleId: activeModule.id,
        recordId: selected.idCatalogo,
      };

      view.openGenericDeleteModal({
        title: `Eliminar ${activeModule.label.toLowerCase()}`,
        message: `Se desactivara el registro <strong>${escapeHtml(selected.codigo)}</strong> - <strong>${escapeHtml(selected.nombre)}</strong>. Ingresa autorizacion de administrador para continuar.`,
      });
      return;
    }

    if (activeModule.type === "action") {
      const selected = getSelectedActionRecord();
      if (!selected) {
        view.showToast("Selecciona un registro.", "warning");
        return;
      }

      state.genericDelete = {
        kind: "action",
        moduleId: activeModule.id,
        recordId: selected.idAccionPersonal,
      };

      view.openGenericDeleteModal({
        title: "Eliminar accion de personal",
        message: `Se eliminara la accion <strong>${escapeHtml(selected.tipoAccion)}</strong> del empleado <strong>${escapeHtml(selected.codigoEmpleado)} - ${escapeHtml(selected.nombreEmpleado)}</strong>.`,
      });
      return;
    }

    if (activeModule.type === "document") {
      const selected = getSelectedDocumentRecord();
      if (!selected) {
        view.showToast("Selecciona un expediente.", "warning");
        return;
      }

      state.genericDelete = {
        kind: "document",
        moduleId: activeModule.id,
        recordId: selected.idExpedienteDocumento,
      };

      view.openGenericDeleteModal({
        title: "Eliminar expediente",
        message: `Se eliminara el documento <strong>${escapeHtml(selected.tipoDocumento)}</strong> del empleado <strong>${escapeHtml(selected.codigoEmpleado)} - ${escapeHtml(selected.nombreEmpleado)}</strong>.`,
      });
      return;
    }

    if (activeModule.type === "structure") {
      const selected = getSelectedStructureNode();
      if (!selected) {
        view.showToast("Selecciona un nodo del organigrama.", "warning");
        return;
      }

      state.genericDelete = {
        kind: "structure",
        moduleId: activeModule.id,
        recordId: selected.idNodoEstructura,
      };

      view.openGenericDeleteModal({
        title: "Eliminar nodo formal",
        message: `Se eliminara el nodo <strong>${escapeHtml(selected.codigoNodo)}</strong> - <strong>${escapeHtml(selected.nombreNodo)}</strong>. Ingresa autorizacion de administrador para continuar.`,
      });
    }
  };

  const confirmGenericDelete = async (event) => {
    event.preventDefault();

    if (!state.genericDelete.kind || !state.genericDelete.recordId) {
      view.closeGenericDeleteModal();
      return;
    }

    const auth = view.readGenericDeleteForm();
    if (!auth.adminUsuario || !auth.adminPassword) {
      view.setGenericDeleteError("Ingresa usuario y contrasena de administrador.");
      return;
    }

    view.setGenericDeleteBusy(true);
    view.setGenericDeleteError("");

    try {
      if (state.genericDelete.kind === "catalog") {
        await catalogService.remove(state.genericDelete.recordId, {
          moduleId: state.genericDelete.moduleId,
          ...auth,
        });
        state.catalogsAdmin.selectedId = null;
        await refreshCatalogData(false);
      } else if (state.genericDelete.kind === "action") {
        await personnelActionService.remove(state.genericDelete.recordId, auth);
        state.actions.selectedId = null;
        await refreshActionData(false);
      } else if (state.genericDelete.kind === "document") {
        await expedienteService.remove(state.genericDelete.recordId, auth);
        state.documents.selectedId = null;
        await refreshDocumentData(false);
      } else if (state.genericDelete.kind === "structure") {
        await structureService.remove(state.genericDelete.recordId, auth);
        state.structure.selectedId = null;
        await refreshStructureCatalogs();
        await refreshStructureData(false);
      }

      view.closeGenericDeleteModal();
      state.genericDelete = { kind: "", moduleId: "", recordId: null };
      invalidateDashboard();
      view.showToast("Registro eliminado correctamente.", "success");
    } catch (error) {
      view.setGenericDeleteError(error.message || "No se pudo eliminar el registro.");
    } finally {
      view.setGenericDeleteBusy(false);
    }
  };

  const openCreateWorkflowModal = (prefillRecord = null) => {
    const activeModule = getActiveModule();
    if (!activeModule) {
      return;
    }

    if (activeModule.type === "catalog") {
      openCreateCatalogModal();
      return;
    }

    if (activeModule.type === "action") {
      openCreateActionModal();
      return;
    }

    if (activeModule.type === "document") {
      openCreateDocumentModal();
      return;
    }

    if (!getWorkflowConfig()) {
      return;
    }

    view.openWorkflowModal({
      moduleId: activeModule.id,
      moduleLabel: activeModule.label,
      mode: "create",
      record: prefillRecord || {},
      catalogs: state.workflows.catalogs,
    });
    syncVacationHalfDayState();
    refreshWorkflowVacationBalance();
  };

  const openEmployeeVacationShortcut = async (employeeId) => {
    const id = Number(employeeId || 0);
    if (!id) {
      view.showToast("Selecciona un empleado valido.", "warning");
      return;
    }

    if (state.activeModuleId !== "vacacion") {
      await handleModuleSelection("vacacion");
    } else {
      await ensureActiveModuleLoaded();
    }

    openCreateWorkflowModal({
      idEmpleado: id,
    });
  };

  const openEditWorkflowModal = async () => {
    const activeModule = getActiveModule();
    if (!activeModule) {
      return;
    }

    if (activeModule.type === "catalog") {
      await openEditCatalogModal();
      return;
    }

    if (activeModule.type === "action") {
      await openEditActionModal();
      return;
    }

    if (activeModule.type === "document") {
      await openEditDocumentModal();
      return;
    }

    const config = getWorkflowConfig();
    const selected = getSelectedWorkflowRecord();

    if (!config) {
      return;
    }

    if (!selected) {
      view.showToast("Selecciona un registro.", "warning");
      return;
    }

    if (!canEditWorkflowRecord(selected)) {
      view.showToast("Solo puedes editar registros pendientes.", "warning");
      return;
    }

    state.workflows.busy = true;
    render();

    try {
      const record = await config.get(selected[config.idField]);
      view.openWorkflowModal({
        moduleId: activeModule.id,
        moduleLabel: activeModule.label,
        mode: "edit",
        record,
        catalogs: state.workflows.catalogs,
      });
      syncVacationHalfDayState();
      refreshWorkflowVacationBalance();
    } catch (error) {
      view.showToast(error.message || "No se pudo cargar el registro.", "danger");
    } finally {
      state.workflows.busy = false;
      render();
    }
  };

  const saveWorkflow = async (event) => {
    event.preventDefault();

    const activeModule = getActiveModule();

    if (!activeModule) {
      return;
    }

    const formData = view.readWorkflowForm();

    if (activeModule.type === "catalog") {
      await saveCatalog(activeModule, formData);
      return;
    }

    if (activeModule.type === "action") {
      await saveAction(formData);
      return;
    }

    if (activeModule.type === "document") {
      await saveDocument(formData);
      return;
    }

    const config = getWorkflowConfig();
    if (!config) {
      return;
    }

    const payload = config.buildPayload(formData);
    const errors = config.validate(payload, formData);

    if (Object.keys(errors).length) {
      view.setWorkflowFormErrors(errors);
      view.focusWorkflowField(Object.keys(errors)[0]);
      return;
    }

    const isEdit = Boolean(formData.recordId);
    view.setWorkflowSaveBusy(true, isEdit ? "Guardar cambios" : "Guardar");

    try {
      const record = isEdit
        ? await config.update(formData.recordId, payload)
        : await config.create(payload);

      state.workflows.selectedId = record[config.idField];
      invalidateDashboard();
      const refreshPromise = refreshWorkflowData(false);
      view.showWorkflowSaveSuccess(isEdit ? "Cambios guardados" : "Guardado con exito");
      await wait(850);
      view.closeWorkflowModal();
      view.showToast(
        isEdit ? "Registro actualizado correctamente." : "Registro guardado correctamente.",
        "success",
      );
      await refreshPromise;
    } catch (error) {
      if (error.errors && Object.keys(error.errors).length) {
        view.setWorkflowFormErrors(error.errors);
        view.focusWorkflowField(Object.keys(error.errors)[0]);
      }

      view.showToast(error.message || "No se pudo guardar el registro.", "danger");
    } finally {
      view.setWorkflowSaveBusy(false, isEdit ? "Guardar cambios" : "Guardar");
    }
  };

  const openResolveWorkflowModal = (action) => {
    const activeModule = getActiveModule();
    const selected = getSelectedWorkflowRecord();

    if (!activeModule || !selected) {
      view.showToast("Selecciona un registro.", "warning");
      return;
    }

    if (!canEditWorkflowRecord(selected)) {
      view.showToast("Solo puedes resolver registros pendientes.", "warning");
      return;
    }

    view.openWorkflowResolveModal({
      moduleId: activeModule.id,
      moduleLabel: activeModule.label,
      record: selected,
      action,
    });
  };

  const submitWorkflowResolution = async (event) => {
    event.preventDefault();

    const activeModule = getActiveModule();
    const config = getWorkflowConfig();
    const selected = getSelectedWorkflowRecord();

    if (!activeModule || !config || !selected) {
      view.closeWorkflowResolveModal();
      return;
    }

    const formData = view.readWorkflowResolveForm();
    const validationMessage = config.validateResolution(selected, formData);

    if (validationMessage) {
      view.setWorkflowResolveError(validationMessage);
      return;
    }

    const payload = config.buildResolutionPayload(formData);
    const buttonLabel = config.resolveButtonLabel(formData.action);
    view.setWorkflowResolveBusy(true, buttonLabel);
    view.setWorkflowResolveError("");

    try {
      const updated = await config.resolve(selected[config.idField], payload);
      state.workflows.selectedId = updated[config.idField];
      view.closeWorkflowResolveModal();
      invalidateDashboard();
      view.showToast(
        formData.action === "APROBAR"
          ? `${config.noun} aprobado correctamente.`
          : `${config.noun} rechazado correctamente.`,
        "success",
      );
      await refreshWorkflowData(false);
    } catch (error) {
      view.setWorkflowResolveError(error.message || "No se pudo resolver el registro.");
    } finally {
      view.setWorkflowResolveBusy(false, buttonLabel);
    }
  };

  const submitVacationBulkAdjustment = async (event) => {
    event.preventDefault();

    const formData = view.readVacationBulkForm();
    if (!formData.fechaAjuste) {
      view.setVacationBulkError("Ingresa la fecha del ajuste.");
      return;
    }

    view.setVacationBulkBusy(true);
    view.setVacationBulkError("");

    try {
      const response = await novedadesService.applyVacationBulkAdjustment({
        fechaAjuste: formData.fechaAjuste,
        cantidadDias: Number(formData.cantidadDias || 0),
        observacion: formData.observacion || null,
      });

      view.closeVacationBulkModal();
      invalidateDashboard();
      await refreshWorkflowData(false);

      const skipped = Array.isArray(response?.skipped) ? response.skipped.length : 0;
      view.showToast(
        skipped > 0
          ? `Ajuste aplicado. ${skipped} colaborador(es) quedaron sin afectar por saldo insuficiente.`
          : "Ajuste masivo de vacaciones aplicado correctamente.",
        "success",
      );
    } catch (error) {
      view.setVacationBulkError(error.message || "No se pudo aplicar el ajuste masivo.");
    } finally {
      view.setVacationBulkBusy(false);
    }
  };

  const reportMonthLabels = [
    "Enero",
    "Febrero",
    "Marzo",
    "Abril",
    "Mayo",
    "Junio",
    "Julio",
    "Agosto",
    "Septiembre",
    "Octubre",
    "Noviembre",
    "Diciembre",
  ];

  const numberOrZero = (value) => {
    const numeric = Number(value ?? 0);
    return Number.isFinite(numeric) ? numeric : 0;
  };

  const formatReportDecimal = (value) => numberOrZero(value).toFixed(2);

  const formatSignedReportHours = (value) => {
    const numeric = numberOrZero(value);
    const sign = numeric > 0 ? "+" : numeric < 0 ? "-" : "";
    return `${sign}${formatReportDecimal(Math.abs(numeric))}`;
  };

  const getClockReportTitle = () => {
    const activeModule = getActiveModule();
    return isHoursReportModule(activeModule) ? "Reporte de horas trabajadas" : "Reporte de asistencia";
  };

  const buildClockReportSummary = () => {
    const months = new Map();
    const employees = new Set();

    const totals = state.clock.rows.reduce(
      (acc, row) => {
        const worked = numberOrZero(row.horasTrabajadas);
        const diff = numberOrZero(row.horasExtraMenos);
        const employeeKey = row.idEmpleado || row.codigoEmpleado || row.cedula;
        const monthKey = String(row.fechaOperacion || "").slice(0, 7) || "sin-fecha";

        if (employeeKey) {
          employees.add(String(employeeKey));
        }

        if (!months.has(monthKey)) {
          const [year, month] = monthKey.split("-");
          months.set(monthKey, {
            key: monthKey,
            label:
              monthKey === "sin-fecha"
                ? "Sin fecha"
                : `${reportMonthLabels[Number(month) - 1] || month} ${year}`,
            worked: 0,
            diff: 0,
            days: 0,
            open: 0,
          });
        }

        const month = months.get(monthKey);
        month.worked += worked;
        month.diff += diff;
        month.days += 1;
        if (String(row.estadoJornada || "").toUpperCase() !== "CERRADA") {
          month.open += 1;
        }

        acc.worked += worked;
        acc.diff += diff;
        acc.days += 1;
        if (String(row.estadoJornada || "").toUpperCase() !== "CERRADA") {
          acc.open += 1;
        }

        return acc;
      },
      { worked: 0, diff: 0, days: 0, open: 0 },
    );

    return {
      totals,
      employees: employees.size,
      months: Array.from(months.values()).sort((a, b) => b.key.localeCompare(a.key)),
    };
  };

  const buildClockReportHtml = () => {
    const branding = state.clock.branding || {};
    const reportTitle = getClockReportTitle();
    const title = `${reportTitle} ${model.formatShortDate(state.clock.dateFrom)} al ${model.formatShortDate(
      state.clock.dateTo,
    )}`;
    const summary = buildClockReportSummary();
    const logoMarkup = branding.logoUrl
      ? `<img src="${escapeHtml(branding.logoUrl)}" alt="Logo empresa" style="height:56px;object-fit:contain;" />`
      : `<div style="width:56px;height:56px;border-radius:16px;background:#d6f6f0;color:#0b2430;display:grid;place-items:center;font-family:Arial,sans-serif;font-weight:700;">${escapeHtml(
          String(branding.companyName || "SF")
            .slice(0, 2)
            .toUpperCase(),
        )}</div>`;

    return `
      <!DOCTYPE html>
      <html lang="es">
        <head>
          <meta charset="UTF-8" />
          <title>${escapeHtml(title)}</title>
          <style>
            body { font-family: Arial, sans-serif; margin: 32px; color: #13212b; }
            .header { display:flex; justify-content:space-between; gap:24px; align-items:flex-start; margin-bottom:24px; }
            .company { display:grid; gap:4px; }
            .company h1 { margin:0; font-size:20px; }
            .meta { color:#4f6170; font-size:12px; line-height:1.5; }
            .kpis { display:grid; grid-template-columns:repeat(4,1fr); gap:10px; margin:18px 0; }
            .kpi { border:1px solid #d7dee4; padding:12px; background:#f8fbfb; }
            .kpi span { display:block; text-transform:uppercase; letter-spacing:.08em; color:#5f7180; font-size:10px; font-weight:bold; }
            .kpi strong { display:block; margin-top:6px; font-size:20px; }
            table { width:100%; border-collapse:collapse; margin-top:18px; }
            th, td { border:1px solid #d7dee4; padding:10px; text-align:left; font-size:12px; }
            th { background:#eff7f6; text-transform:uppercase; letter-spacing:.08em; font-size:11px; }
            .footer { margin-top:18px; font-size:11px; color:#607282; }
          </style>
        </head>
        <body>
          <div class="header">
            <div class="company">
              ${logoMarkup}
              <h1>${escapeHtml(branding.legalName || branding.companyName || "SIFNIC")}</h1>
              <div class="meta">
                <div>${escapeHtml(branding.address || "")}</div>
                <div>${escapeHtml(branding.email || "")}${branding.phone ? ` - ${escapeHtml(branding.phone)}` : ""}</div>
                <div>${branding.ruc ? `RUC: ${escapeHtml(branding.ruc)}` : ""}</div>
              </div>
            </div>
            <div class="meta">
              <strong>${escapeHtml(title)}</strong><br />
              Generado: ${escapeHtml(new Date().toLocaleString("es-NI"))}<br />
              ${branding.logoPending ? "Logo corporativo pendiente de configuracion." : ""}
            </div>
          </div>

          <section class="kpis">
            <article class="kpi"><span>Horas trabajadas</span><strong>${escapeHtml(formatReportDecimal(summary.totals.worked))} h</strong></article>
            <article class="kpi"><span>Extra / menos</span><strong>${escapeHtml(formatSignedReportHours(summary.totals.diff))} h</strong></article>
            <article class="kpi"><span>Jornadas</span><strong>${escapeHtml(String(summary.totals.days))}</strong></article>
            <article class="kpi"><span>Colaboradores</span><strong>${escapeHtml(String(summary.employees))}</strong></article>
          </section>

          <table>
            <thead>
              <tr>
                <th>Mes</th>
                <th>Horas trabajadas</th>
                <th>Horas extra/menos</th>
                <th>Jornadas</th>
                <th>Abiertas</th>
              </tr>
            </thead>
            <tbody>
              ${
                summary.months.length
                  ? summary.months
                      .map(
                        (month) => `
                          <tr>
                            <td>${escapeHtml(month.label)}</td>
                            <td>${escapeHtml(formatReportDecimal(month.worked))}</td>
                            <td>${escapeHtml(formatSignedReportHours(month.diff))}</td>
                            <td>${escapeHtml(String(month.days))}</td>
                            <td>${escapeHtml(String(month.open))}</td>
                          </tr>
                        `,
                      )
                      .join("")
                  : '<tr><td colspan="5">Sin resumen mensual.</td></tr>'
              }
            </tbody>
          </table>

          <table>
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Codigo</th>
                <th>Empleado</th>
                <th>Cedula</th>
                <th>Entrada</th>
                <th>Salida</th>
                <th>Horas</th>
                <th>Extra/menos</th>
                <th>Horario</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              ${
                state.clock.rows.length
                  ? state.clock.rows
                      .map(
                        (row) => `
                          <tr>
                            <td>${escapeHtml(model.formatShortDate(row.fechaOperacion))}</td>
                            <td>${escapeHtml(row.codigoEmpleado)}</td>
                            <td>${escapeHtml(row.nombreEmpleado)}</td>
                            <td>${escapeHtml(row.cedula)}</td>
                            <td>${escapeHtml(row.horaEntrada || "-")}</td>
                            <td>${escapeHtml(row.horaSalida || "-")}</td>
                            <td>${escapeHtml(formatReportDecimal(row.horasTrabajadas))}</td>
                            <td>${escapeHtml(formatSignedReportHours(row.horasExtraMenos))}</td>
                            <td>${escapeHtml(row.nombreHorario || "Base 8 h/dia")}</td>
                            <td>${escapeHtml(row.estadoJornada)}</td>
                          </tr>
                        `,
                      )
                      .join("")
                  : '<tr><td colspan="10">Sin registros para el filtro actual.</td></tr>'
              }
            </tbody>
          </table>
          <div class="footer">La diferencia se calcula contra el horario laboral vigente del contrato. Si no hay horario, se usa 8 h/dia como base provisional.</div>
          <div class="footer">${escapeHtml(branding.footerText || "")}</div>
        </body>
      </html>
    `;
  };

  const exportClockExcel = () => {
    if (!state.clock.rows.length) {
      view.showToast("No hay datos para exportar.", "warning");
      return;
    }

    const html = buildClockReportHtml();
    const blob = new Blob([html], { type: "application/vnd.ms-excel;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${
      isHoursReportModule(getActiveModule()) ? "reporte-horas-trabajadas" : "reporte-reloj"
    }-${state.clock.dateFrom}-${state.clock.dateTo}.xls`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const exportClockPdf = () => {
    if (!state.clock.rows.length) {
      view.showToast("No hay datos para exportar.", "warning");
      return;
    }

    const reportWindow = window.open("", "_blank", "width=1080,height=900");
    if (!reportWindow) {
      view.showToast("El navegador bloqueo la ventana del reporte.", "danger");
      return;
    }

    reportWindow.document.open();
    reportWindow.document.write(buildClockReportHtml());
    reportWindow.document.close();
    reportWindow.focus();
    window.setTimeout(() => reportWindow.print(), 260);
  };

  const buildVacationReportHtml = () => {
    const branding = state.reports.branding || {};
    const title = `Reporte de vacaciones disponibles al ${model.formatShortDate(state.reports.cutoffDate)}`;
    const logoMarkup = branding.logoUrl
      ? `<img src="${escapeHtml(branding.logoUrl)}" alt="Logo empresa" style="height:56px;object-fit:contain;" />`
      : `<div style="width:56px;height:56px;border-radius:16px;background:#d6f6f0;color:#0b2430;display:grid;place-items:center;font-family:Arial,sans-serif;font-weight:700;">${escapeHtml(
          String(branding.companyName || "SF")
            .slice(0, 2)
            .toUpperCase(),
        )}</div>`;

    const statusLabel =
      REPORT_EMPLOYMENT_STATUS_OPTIONS.find((item) => item.value === state.reports.status)?.label ||
      "Todos los empleados";
    const selectedDepartment =
      state.reports.catalogs.departments.find(
        (department) => String(department.id) === String(state.reports.idDepartamento || ""),
      )?.name || "Todos los departamentos";

    return `
      <!DOCTYPE html>
      <html lang="es">
        <head>
          <meta charset="UTF-8" />
          <title>${escapeHtml(title)}</title>
          <style>
            body { font-family: Arial, sans-serif; margin: 32px; color: #13212b; }
            .header { display:flex; justify-content:space-between; gap:24px; align-items:flex-start; margin-bottom:24px; }
            .company { display:grid; gap:4px; }
            .company h1 { margin:0; font-size:20px; }
            .meta { color:#4f6170; font-size:12px; line-height:1.5; }
            .filters { margin: 16px 0 14px; font-size:12px; color:#415461; display:grid; gap:4px; }
            table { width:100%; border-collapse:collapse; margin-top:18px; }
            th, td { border:1px solid #d7dee4; padding:10px; text-align:left; font-size:12px; vertical-align:top; }
            th { background:#eff7f6; text-transform:uppercase; letter-spacing:.08em; font-size:11px; }
            .footer { margin-top:18px; font-size:11px; color:#607282; }
          </style>
        </head>
        <body>
          <div class="header">
            <div class="company">
              ${logoMarkup}
              <h1>${escapeHtml(branding.legalName || branding.companyName || "SIFNIC")}</h1>
              <div class="meta">
                <div>${escapeHtml(branding.address || "")}</div>
                <div>${escapeHtml(branding.email || "")}${branding.phone ? ` - ${escapeHtml(branding.phone)}` : ""}</div>
                <div>${branding.ruc ? `RUC: ${escapeHtml(branding.ruc)}` : ""}</div>
              </div>
            </div>
            <div class="meta">
              <strong>${escapeHtml(title)}</strong><br />
              Generado: ${escapeHtml(new Date().toLocaleString("es-NI"))}<br />
              ${branding.logoPending ? "Logo corporativo pendiente de configuracion." : ""}
            </div>
          </div>

          <div class="filters">
            <div>Fecha de corte: ${escapeHtml(model.formatShortDate(state.reports.cutoffDate))}</div>
            <div>Estado: ${escapeHtml(statusLabel)}</div>
            <div>Departamento: ${escapeHtml(selectedDepartment)}</div>
          </div>

          <table>
            <thead>
              <tr>
                <th>Codigo</th>
                <th>Empleado</th>
                <th>Fecha ingreso</th>
                <th>Departamento</th>
                <th>Cargo</th>
                <th>Contrato</th>
                <th>Disponibles</th>
              </tr>
            </thead>
            <tbody>
              ${
                state.reports.rows.length
                  ? state.reports.rows
                      .map(
                        (row) => `
                          <tr>
                            <td>${escapeHtml(row.codigoEmpleado || "-")}</td>
                            <td>${escapeHtml(row.nombreEmpleado || "-")}</td>
                            <td>${escapeHtml(model.formatShortDate(row.fechaIngreso))}</td>
                            <td>${escapeHtml(row.nombreDepartamento || "-")}</td>
                            <td>${escapeHtml(row.nombreCargo || "-")}</td>
                            <td>${escapeHtml(row.nombreTipoContratoVigente || "Sin contrato vigente")}</td>
                            <td>${escapeHtml(`${Number(row.diasDisponibles || 0).toFixed(2)} d`)}</td>
                          </tr>
                        `,
                      )
                      .join("")
                  : '<tr><td colspan="7">Sin registros para el filtro actual.</td></tr>'
              }
            </tbody>
          </table>
          <div class="footer">${escapeHtml(branding.footerText || "")}</div>
        </body>
      </html>
    `;
  };

  const exportVacationReportExcel = () => {
    if (!state.reports.rows.length) {
      view.showToast("No hay datos para exportar.", "warning");
      return;
    }

    const html = buildVacationReportHtml();
    const blob = new Blob([html], { type: "application/vnd.ms-excel;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `reporte-vacaciones-${state.reports.cutoffDate}.xls`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const exportVacationReportPdf = () => {
    if (!state.reports.rows.length) {
      view.showToast("No hay datos para exportar.", "warning");
      return;
    }

    const reportWindow = window.open("", "_blank", "width=1080,height=900");
    if (!reportWindow) {
      view.showToast("El navegador bloqueo la ventana del reporte.", "danger");
      return;
    }

    reportWindow.document.open();
    reportWindow.document.write(buildVacationReportHtml());
    reportWindow.document.close();
    reportWindow.focus();
    window.setTimeout(() => reportWindow.print(), 260);
  };

  const handleModuleSelection = async (moduleId) => {
    const normalizedModuleId = normalizeModuleId(moduleId);
    const nextModule = model.getModuleById(state.activeGroupId, normalizedModuleId);
    if (nextModule?.externalUrl) {
      window.location.href = nextModule.externalUrl;
      return;
    }

    if (nextModule?.type === "workflow" && state.workflows.loadedModuleId !== normalizedModuleId) {
      state.workflows.items = [];
      state.workflows.selectedId = null;
    }

    if (nextModule?.type === "catalog" && state.catalogsAdmin.loadedModuleId !== normalizedModuleId) {
      state.catalogsAdmin.items = [];
      state.catalogsAdmin.selectedId = null;
    }

    if (nextModule?.type === "action") {
      state.actions.selectedId = state.actions.items[0]?.idAccionPersonal || null;
    }

    if (nextModule?.type === "document") {
      state.documents.selectedId = state.documents.items[0]?.idExpedienteDocumento || null;
    }

    view.forceCloseOverlays();
    state.activeModuleId = normalizedModuleId;
    window.scrollTo({ top: 0, behavior: "smooth" });
    render();
    await ensureActiveModuleLoaded();
    render();
  };

  const bindSanitizers = () => {
    view.elements.codigoEmpleado?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeCode(event.target.value);
      view.clearFieldError("codigoEmpleado");
    });

    view.elements.cedula?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeCedula(event.target.value);
      view.clearFieldError("cedula");
    });

    view.elements.nombres?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeName(event.target.value);
      view.clearFieldError("nombres");
      scheduleUsernamePreview();
    });

    view.elements.apellidos?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeName(event.target.value);
      view.clearFieldError("apellidos");
      scheduleUsernamePreview();
    });

    view.elements.fechaIngreso?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeDateInput(event.target.value);
      view.clearFieldError("fechaIngreso");
    });

    view.elements.fechaNacimiento?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeDateInput(event.target.value);
      view.clearFieldError("fechaNacimiento");
    });

    view.elements.telefono?.addEventListener("input", (event) => {
      event.target.value = model.sanitizePhone(event.target.value);
      view.clearFieldError("telefono");
    });

    view.elements.inss?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeInss(event.target.value);
      view.clearFieldError("inss");
    });

    view.elements.numeroCuentaBancaria?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeAccount(event.target.value);
      view.clearFieldError("numeroCuentaBancaria");
    });

    view.elements.correo?.addEventListener("input", (event) => {
      event.target.value = model.sanitizeEmail(event.target.value);
      view.clearFieldError("correo");
    });

    [
      "idDepartamento",
      "idCargo",
      "fechaIngreso",
      "fechaNacimiento",
      "sexo",
      "estadoCivil",
      "idBanco",
      "direccion",
    ].forEach((fieldName) => {
      view.elements[fieldName]?.addEventListener("input", () => {
        view.clearFieldError(fieldName);
      });

      view.elements[fieldName]?.addEventListener("change", () => {
        view.clearFieldError(fieldName);
      });
    });

    view.elements.contractForm?.addEventListener("input", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      if (target.id === "numeroContrato") {
        target.value = model.sanitizeContractNumber(target.value);
        view.clearContractFieldError("numeroContrato");
      }

      if (target.id === "contractFechaInicio") {
        target.value = model.sanitizeDateInput(target.value);
        view.clearContractFieldError("fechaInicio");
      }

      if (target.id === "contractFechaFin") {
        target.value = model.sanitizeDateInput(target.value);
        view.clearContractFieldError("fechaFin");
      }

      if (target.id === "salarioBaseMensual") {
        view.clearContractFieldError("salarioBaseMensual");
      }

      if (target.id === "observacion") {
        view.clearContractFieldError("observacion");
      }
    });

    view.elements.contractForm?.addEventListener("change", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      if (target.id === "contractEmployeeId") {
        view.clearContractFieldError("idEmpleado");
        view.syncContractEmployeeHint(
          state.contracts.catalogs?.employees || [],
          target.value,
          view.elements.contractForm?.dataset.mode || "create",
        );
        if (!view.elements.contractId.value) {
          requestSuggestedContractNumber();
        }
      }

      if (target.id === "idTipoContrato") {
        view.clearContractFieldError("idTipoContrato");
      }

      if (target.id === "idHorarioLaboral") {
        view.clearContractFieldError("idHorarioLaboral");
      }

      if (target.id === "moneda") {
        view.clearContractFieldError("moneda");
      }

      if (target.id === "esContratoVigente" && view.elements.esContratoVigente.checked) {
        view.elements.contractFechaFin.value = "";
        view.clearContractFieldError("fechaFin");
      }
    });

    view.elements.structureForm?.addEventListener("input", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      if (target.id === "structureCodigoNodo") {
        target.value = model.sanitizeCatalogCode(target.value);
        view.clearStructureFieldError("codigoNodo");
      }

      if (target.id === "structureNombreNodo") {
        target.value = model.sanitizeLooseText(target.value, 200);
        view.clearStructureFieldError("nombreNodo");
      }

      if (target.id === "structureOrdenVisual") {
        view.clearStructureFieldError("ordenVisual");
      }

      if (target.id === "structureObservacion") {
        target.value = model.sanitizeLooseText(target.value, 500);
        view.clearStructureFieldError("observacion");
      }
    });

    view.elements.structureForm?.addEventListener("change", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      if (target.id === "structureTipoNodo") {
        view.clearStructureFieldError("tipoNodo");
      }

      if (target.id === "structureParentNodeId") {
        view.clearStructureFieldError("idNodoPadre");
      }

      if (target.id === "structureEmployeeId") {
        view.clearStructureFieldError("idEmpleadoTitular");
      }

      if (target.id === "structureDepartmentId") {
        view.clearStructureFieldError("idDepartamento");
      }

      if (target.id === "structurePositionId") {
        view.clearStructureFieldError("idCargo");
      }
    });

    view.elements.workflowForm?.addEventListener("input", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      const moduleId = view.elements.workflowForm.dataset.moduleId;
      const catalogConfig = model.getCatalogModuleConfig(moduleId);

      if (target.id === "workflowCode") {
        target.value = model.sanitizeCatalogCode(target.value);
        view.clearWorkflowFieldError("codigo");
      }

      if (target.id === "workflowName") {
        target.value = model.sanitizeLooseText(target.value, catalogConfig?.nameMaxLength || 150);
        view.clearWorkflowFieldError("nombre");
      }

      if (target.id === "workflowTypeText") {
        target.value = model.sanitizeLooseText(target.value, moduleId === "accion_personal" ? 50 : 100);
        view.clearWorkflowFieldError(moduleId === "accion_personal" ? "tipoAccion" : "tipoDocumento");

        if (moduleId === "accion_personal") {
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowStartDate") {
        target.value = model.sanitizeDateInput(target.value);
        view.clearWorkflowFieldError(
          moduleId === "accion_personal"
            ? "fechaAccion"
            : moduleId === "expediente_documento"
              ? "fechaDocumento"
              : "fechaInicio",
        );

        if (moduleId === "vacacion") {
          syncVacationHalfDayState();
        }

        if (moduleId === "accion_personal") {
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowEndDate") {
        target.value = model.sanitizeDateInput(target.value);
        view.clearWorkflowFieldError(
          moduleId === "expediente_documento"
            ? "fechaVencimiento"
            : moduleId === "accion_personal"
              ? "nuevaFechaFinContrato"
              : "fechaFin",
        );

        if (moduleId === "accion_personal") {
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowHoursDate") {
        target.value = model.sanitizeDateInput(target.value);
        view.clearWorkflowFieldError("fechaHoraExtra");
      }

      if (target.id === "workflowHoursAmount") {
        view.clearWorkflowFieldError("cantidadHoras");
      }

      if (target.id === "workflowNumberValue1") {
        view.clearWorkflowFieldError("numberValue1");

        if (moduleId === "accion_personal") {
          view.clearWorkflowFieldError("nuevoSalarioBaseMensual");
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowNumberValue2") {
        view.clearWorkflowFieldError("numberValue2");
      }

      if (target.id === "workflowIntegerValue1") {
        view.clearWorkflowFieldError("integerValue1");
      }

      if (target.id === "workflowObservation") {
        view.clearWorkflowFieldError(
          catalogConfig?.usesDescription
            ? "descripcion"
            : moduleId === "vacacion"
              ? "observacionSolicitud"
              : moduleId === "accion_personal"
                ? "descripcionAccion"
                : "observacion",
        );

        if (moduleId === "accion_personal") {
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowFile") {
        view.clearWorkflowFieldError("archivo");
      }

      if (["workflowEmployeeId", "workflowStartDate", "workflowEndDate"].includes(target.id) && moduleId === "vacacion") {
        refreshWorkflowVacationBalance();
      }
    });

    view.elements.workflowForm?.addEventListener("change", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      const moduleId = view.elements.workflowForm.dataset.moduleId;
      if (target.id === "workflowEmployeeId") {
        view.clearWorkflowFieldError("idEmpleado");

        if (moduleId === "accion_personal") {
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowTypeId") {
        view.clearWorkflowFieldError(moduleId === "hora_extra" ? "idTipoHoraExtra" : "idTipoPermiso");
      }

      if (target.id === "workflowRelatedId") {
        view.clearWorkflowFieldError(moduleId === "accion_personal" ? "idCargoNuevo" : "relatedId");

        if (moduleId === "accion_personal") {
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowFlagValue1") {
        view.clearWorkflowFieldError("flagValue1");

        if (moduleId === "accion_personal") {
          view.syncActionWorkflowForm(state.actions.catalogs, view.readWorkflowForm());
        }
      }

      if (target.id === "workflowActive") {
        view.clearWorkflowFieldError("activo");
      }

      if (target.id === "workflowRemoveFile") {
        view.clearWorkflowFieldError("archivo");
      }

      if (target.id === "workflowEmployeeId" && moduleId === "vacacion") {
        refreshWorkflowVacationBalance();
      }

      if (target.id === "workflowHalfDay") {
        view.clearWorkflowFieldError("jornadaMedioDia");
        syncVacationHalfDayState();
        refreshWorkflowVacationBalance();
      }

      if (target.id === "workflowHalfDayMorning" || target.id === "workflowHalfDayAfternoon") {
        view.clearWorkflowFieldError("jornadaMedioDia");
      }
    });

    view.elements.workflowResolutionObservation?.addEventListener("input", () => {
      view.setWorkflowResolveError("");
    });

    view.elements.workflowApprovedDays?.addEventListener("input", () => {
      view.setWorkflowResolveError("");
    });
  };

  const bindEvents = () => {
    view.elements.backToDashboard?.addEventListener("click", redirectToDashboard);

    view.elements.closeSession?.addEventListener("click", async () => {
      await sessionApi.logout();
      redirectToLogin();
    });

    view.elements.rrhhShortcutActions?.addEventListener("click", async (event) => {
      const button = event.target.closest("[data-rrhh-shortcut]");
      if (!button) {
        return;
      }

      state.activeGroupId = DEFAULT_GROUP_ID;
      await handleModuleSelection(button.dataset.rrhhShortcut);
    });

    view.elements.rrhhGlobalSearch?.addEventListener("keydown", async (event) => {
      if (event.key !== "Enter") {
        return;
      }

      event.preventDefault();
      state.activeGroupId = DEFAULT_GROUP_ID;
      state.employees.search = event.currentTarget.value.trim();
      if (view.elements.searchInput) {
        view.elements.searchInput.value = state.employees.search;
      }
      await handleModuleSelection("empleado");
    });

    view.elements.workspaceBackButton?.addEventListener("click", async () => {
      view.forceCloseOverlays();
      state.activeModuleId = null;
      window.scrollTo({ top: 0, behavior: "smooth" });
      render();
      await ensureBoardDataLoaded();
    });

    view.elements.mainNav?.addEventListener("click", async (event) => {
      const button = event.target.closest("[data-group-id]");
      if (!button) {
        return;
      }

      const nextGroup = button.dataset.groupId;
      if (!nextGroup || nextGroup === state.activeGroupId) {
        return;
      }

      state.activeGroupId = nextGroup;
      state.activeModuleId = null;
      view.forceCloseOverlays();
      window.scrollTo({ top: 0, behavior: "smooth" });
      render();
      await ensureBoardDataLoaded();
    });

    view.elements.groupBoard?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-module-id]");
      if (!button) {
        return;
      }

      const moduleId = button.dataset.moduleId || null;
      if (!moduleId) {
        return;
      }

      handleModuleSelection(moduleId);
    });

    view.elements.configShell?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-module-id]");
      if (!button) {
        return;
      }

      const moduleId = button.dataset.moduleId || null;
      if (!moduleId) {
        return;
      }

      handleModuleSelection(moduleId);
    });

    view.elements.refreshButton?.addEventListener("click", () => refreshEmployeeData(true));
    view.elements.newEmployeeButton?.addEventListener("click", openCreateEmployeeModal);
    view.elements.viewEmployeeButton?.addEventListener("click", () => {
      if (!getSelectedEmployee()) {
        view.showToast("Selecciona un empleado.", "warning");
        return;
      }

      state.employees.detailVisible = !state.employees.detailVisible;
      render();
    });
    view.elements.editEmployeeButton?.addEventListener("click", openEditEmployeeModal);
    view.elements.deleteEmployeeButton?.addEventListener("click", openDeleteEmployeeModal);

    view.elements.searchInput?.addEventListener("input", (event) => {
      state.employees.search = event.target.value.trim();
      window.clearTimeout(state.employees.searchTimer);
      state.employees.searchTimer = window.setTimeout(() => {
        refreshEmployeeData(false);
      }, 260);
    });

    view.elements.statusFilter?.addEventListener("change", (event) => {
      state.employees.status = event.target.value;
      refreshEmployeeData(false);
    });

    view.elements.employeeStatusChips?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-employee-status]");
      if (!button) {
        return;
      }

      state.employees.status = button.dataset.employeeStatus || "TODOS";
      if (view.elements.statusFilter) {
        view.elements.statusFilter.value = state.employees.status;
      }
      refreshEmployeeData(false);
    });

    view.elements.employeeTableBody?.addEventListener("click", (event) => {
      const row = event.target.closest("[data-employee-id]");
      if (!row) {
        return;
      }

      state.employees.selectedId = Number(row.dataset.employeeId);
      render();
      loadEmployeeDetail(state.employees.selectedId, true);
    });

    view.elements.employeeTableBody?.addEventListener("dblclick", (event) => {
      const row = event.target.closest("[data-employee-id]");
      if (!row) {
        return;
      }

      state.employees.selectedId = Number(row.dataset.employeeId);
      render();
      loadEmployeeDetail(state.employees.selectedId, true);
      openEditEmployeeModal();
    });

    view.elements.detailBody?.addEventListener("click", (event) => {
      const photoButton = event.target.closest("[data-employee-action='upload-photo']");
      if (photoButton) {
        const input = view.elements.detailBody.querySelector(
          `[data-employee-photo-input="${photoButton.dataset.employeeId || ""}"]`,
        );
        input?.click();
        return;
      }

      const button = event.target.closest("[data-employee-action='new-vacation']");
      if (button) {
        openEmployeeVacationShortcut(Number(button.dataset.employeeId || 0));
      }
    });

    view.elements.detailBody?.addEventListener("change", async (event) => {
      const input = event.target.closest("[data-employee-photo-input]");
      if (!input) {
        return;
      }

      const [file] = Array.from(input.files || []);
      input.value = "";

      if (!file) {
        return;
      }

      const employeeId = Number(input.dataset.employeePhotoInput || 0);
      if (!(employeeId > 0)) {
        view.showToast("No se pudo identificar el empleado para la foto.", "danger");
        return;
      }

      try {
        const updated = await employeeService.uploadPhoto(employeeId, file);
        if (updated?.idEmpleado) {
          state.employees.details[updated.idEmpleado] = updated;

          const listIndex = state.employees.items.findIndex(
            (item) => Number(item.idEmpleado) === Number(updated.idEmpleado),
          );
          if (listIndex >= 0) {
            state.employees.items[listIndex] = {
              ...state.employees.items[listIndex],
              fotoPerfilUrl: updated.fotoPerfilUrl || updated.fotoPerfilURL || updated.foto_perfil_url || null,
            };
          }
        } else {
          const detail = await employeeService.get(employeeId);
          if (detail?.idEmpleado) {
            state.employees.details[detail.idEmpleado] = detail;
          }
        }

        render();
        view.showToast("Foto del empleado actualizada correctamente.", "success");
      } catch (error) {
        view.showToast(error.message || "No se pudo actualizar la foto del empleado.", "danger");
      }
    });

    view.elements.cancelEmployeeModal?.addEventListener("click", view.closeEmployeeModal);
    view.elements.employeeForm?.addEventListener("submit", saveEmployee);
    view.elements.cancelDeleteModal?.addEventListener("click", view.closeDeleteModal);
    view.elements.deleteForm?.addEventListener("submit", confirmDeleteEmployee);

    view.elements.refreshContractButton?.addEventListener("click", () => refreshContractData(true));
    view.elements.newContractButton?.addEventListener("click", openCreateContractModal);
    view.elements.editContractButton?.addEventListener("click", openEditContractModal);
    view.elements.printContractButton?.addEventListener("click", printContract);
    view.elements.deleteContractButton?.addEventListener("click", openDeleteContractModal);
    view.elements.cancelContractModal?.addEventListener("click", view.closeContractModal);
    view.elements.contractForm?.addEventListener("submit", saveContract);
    view.elements.cancelStructureModal?.addEventListener("click", view.closeStructureModal);
    view.elements.structureForm?.addEventListener("submit", saveStructure);
    view.elements.cancelContractDeleteModal?.addEventListener("click", view.closeContractDeleteModal);
    view.elements.contractDeleteForm?.addEventListener("submit", confirmDeleteContract);

    view.elements.contractSearchInput?.addEventListener("input", (event) => {
      state.contracts.search = event.target.value.trim();
      window.clearTimeout(state.contracts.searchTimer);
      state.contracts.searchTimer = window.setTimeout(() => {
        refreshContractData(false);
      }, 260);
    });

    view.elements.contractStatusFilter?.addEventListener("change", (event) => {
      state.contracts.status = event.target.value;
      refreshContractData(false);
    });

    view.elements.contractTableBody?.addEventListener("click", (event) => {
      const row = event.target.closest("[data-contract-id]");
      if (!row) {
        return;
      }

      state.contracts.selectedId = Number(row.dataset.contractId);
      render();
    });

    view.elements.contractTableBody?.addEventListener("dblclick", (event) => {
      const row = event.target.closest("[data-contract-id]");
      if (!row) {
        return;
      }

      state.contracts.selectedId = Number(row.dataset.contractId);
      render();
      openEditContractModal();
    });

    view.elements.refreshWorkflowButton?.addEventListener("click", () => refreshActiveWorkflowShellData(true));
    view.elements.newWorkflowButton?.addEventListener("click", openCreateWorkflowModal);
    view.elements.editWorkflowButton?.addEventListener("click", openEditWorkflowModal);
    view.elements.approveWorkflowButton?.addEventListener("click", () => {
      const activeModule = getActiveModule();
      if (activeModule?.type === "document") {
        downloadSelectedDocument();
        return;
      }

      openResolveWorkflowModal("APROBAR");
    });
    view.elements.viewWorkflowButton?.addEventListener("click", () => {
      const bucket = getActiveWorkflowBucket();
      if (!bucket?.selectedId) {
        view.showToast("Selecciona un registro.", "warning");
        return;
      }

      bucket.detailVisible = !bucket.detailVisible;
      render();
    });
    view.elements.workflowExtraButton?.addEventListener("click", () => {
      const activeModule = getActiveModule();
      if (activeModule?.type === "action") {
        printSelectedActionMemo();
        return;
      }

      if (activeModule?.id === "vacacion") {
        view.openVacationBulkModal();
      }
    });
    view.elements.rejectWorkflowButton?.addEventListener("click", () => {
      const activeModule = getActiveModule();
      if (activeModule?.type === "workflow") {
        openResolveWorkflowModal("RECHAZAR");
        return;
      }

      openGenericDeleteModalForActiveModule();
    });
    view.elements.cancelWorkflowModal?.addEventListener("click", view.closeWorkflowModal);
    view.elements.workflowForm?.addEventListener("submit", saveWorkflow);
    view.elements.cancelWorkflowResolveModal?.addEventListener("click", view.closeWorkflowResolveModal);
    view.elements.workflowResolveForm?.addEventListener("submit", submitWorkflowResolution);
    view.elements.cancelVacationBulkModal?.addEventListener("click", view.closeVacationBulkModal);
    view.elements.vacationBulkForm?.addEventListener("submit", submitVacationBulkAdjustment);
    view.elements.cancelGenericDeleteModal?.addEventListener("click", view.closeGenericDeleteModal);
    view.elements.genericDeleteForm?.addEventListener("submit", confirmGenericDelete);

    view.elements.workflowSearchInput?.addEventListener("input", (event) => {
      const activeModule = getActiveModule();
      const value = event.target.value.trim();

      if (activeModule?.type === "catalog") {
        state.catalogsAdmin.search = value;
        window.clearTimeout(state.catalogsAdmin.searchTimer);
        state.catalogsAdmin.searchTimer = window.setTimeout(() => {
          refreshCatalogData(false);
        }, 260);
        return;
      }

      if (activeModule?.type === "action") {
        state.actions.search = value;
        window.clearTimeout(state.actions.searchTimer);
        state.actions.searchTimer = window.setTimeout(() => {
          refreshActionData(false);
        }, 260);
        return;
      }

      if (activeModule?.type === "document") {
        state.documents.search = value;
        window.clearTimeout(state.documents.searchTimer);
        state.documents.searchTimer = window.setTimeout(() => {
          refreshDocumentData(false);
        }, 260);
        return;
      }

      state.workflows.search = value;
      window.clearTimeout(state.workflows.searchTimer);
      state.workflows.searchTimer = window.setTimeout(() => {
        refreshWorkflowData(false);
      }, 260);
    });

    view.elements.workflowStatusFilter?.addEventListener("change", (event) => {
      const activeModule = getActiveModule();
      const value = event.target.value;

      if (activeModule?.type === "catalog") {
        state.catalogsAdmin.status = value;
        refreshCatalogData(false);
        return;
      }

      if (activeModule?.type === "action") {
        state.actions.status = value;
        refreshActionData(false);
        return;
      }

      if (activeModule?.type === "document") {
        state.documents.status = value;
        refreshDocumentData(false);
        return;
      }

      state.workflows.status = value;
      refreshWorkflowData(false);
    });

    view.elements.workflowStatusChips?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-workflow-status]");
      if (!button) {
        return;
      }

      const value = button.dataset.workflowStatus || "TODOS";
      const activeModule = getActiveModule();
      if (view.elements.workflowStatusFilter) {
        view.elements.workflowStatusFilter.value = value;
      }

      if (activeModule?.type === "catalog") {
        state.catalogsAdmin.status = value;
        refreshCatalogData(false);
        return;
      }

      if (activeModule?.type === "action") {
        state.actions.status = value;
        refreshActionData(false);
        return;
      }

      if (activeModule?.type === "document") {
        state.documents.status = value;
        refreshDocumentData(false);
        return;
      }

      state.workflows.status = value;
      refreshWorkflowData(false);
    });

    view.elements.workflowTableBody?.addEventListener("click", (event) => {
      const row = event.target.closest("[data-workflow-id]");
      if (!row) {
        return;
      }

      const activeModule = getActiveModule();
      const selectedId = Number(row.dataset.workflowId);

      if (activeModule?.type === "catalog") {
        state.catalogsAdmin.selectedId = selectedId;
      } else if (activeModule?.type === "action") {
        state.actions.selectedId = selectedId;
      } else if (activeModule?.type === "document") {
        state.documents.selectedId = selectedId;
      } else {
        state.workflows.selectedId = selectedId;
      }

      render();
    });

    view.elements.workflowTableBody?.addEventListener("dblclick", (event) => {
      const row = event.target.closest("[data-workflow-id]");
      if (!row) {
        return;
      }

      const activeModule = getActiveModule();
      const selectedId = Number(row.dataset.workflowId);

      if (activeModule?.type === "catalog") {
        state.catalogsAdmin.selectedId = selectedId;
      } else if (activeModule?.type === "action") {
        state.actions.selectedId = selectedId;
      } else if (activeModule?.type === "document") {
        state.documents.selectedId = selectedId;
      } else {
        state.workflows.selectedId = selectedId;
      }

      render();
      openEditWorkflowModal();
    });

    view.elements.refreshClockButton?.addEventListener("click", () => refreshClockData(true));
    view.elements.exportClockExcelButton?.addEventListener("click", exportClockExcel);
    view.elements.exportClockPdfButton?.addEventListener("click", exportClockPdf);

    view.elements.clockSearchInput?.addEventListener("input", (event) => {
      state.clock.search = event.target.value.trim();
      window.clearTimeout(state.clock.searchTimer);
      state.clock.searchTimer = window.setTimeout(() => {
        refreshClockData(false);
      }, 260);
    });

    view.elements.clockDateFrom?.addEventListener("change", (event) => {
      state.clock.dateFrom = event.target.value || state.clock.dateFrom;
      refreshClockData(false);
    });

    view.elements.clockDateTo?.addEventListener("change", (event) => {
      state.clock.dateTo = event.target.value || state.clock.dateTo;
      refreshClockData(false);
    });

    view.elements.clockEmployeeFilter?.addEventListener("change", (event) => {
      state.clock.idEmpleado = event.target.value;
      refreshClockData(false);
    });

    view.elements.clockTableBody?.addEventListener("click", (event) => {
      const row = event.target.closest("[data-clock-index]");
      if (!row) {
        return;
      }

      state.clock.selectedIndex = Number(row.dataset.clockIndex);
      render();
    });

    view.elements.refreshReportButton?.addEventListener("click", () => refreshVacationReportData(true));
    view.elements.exportReportExcelButton?.addEventListener("click", exportVacationReportExcel);
    view.elements.exportReportPdfButton?.addEventListener("click", exportVacationReportPdf);

    view.elements.reportSearchInput?.addEventListener("input", (event) => {
      state.reports.search = event.target.value.trim();
      window.clearTimeout(state.reports.searchTimer);
      state.reports.searchTimer = window.setTimeout(() => {
        refreshVacationReportData(false);
      }, 260);
    });

    view.elements.reportCutoffDate?.addEventListener("change", (event) => {
      state.reports.cutoffDate = event.target.value || state.reports.cutoffDate;
      refreshVacationReportData(false);
    });

    view.elements.reportDepartmentFilter?.addEventListener("change", (event) => {
      state.reports.idDepartamento = event.target.value || "";
      refreshVacationReportData(false);
    });

    view.elements.reportEmployeeStatusFilter?.addEventListener("change", (event) => {
      state.reports.status = event.target.value || "TODOS";
      refreshVacationReportData(false);
    });

    view.elements.reportTableBody?.addEventListener("click", (event) => {
      const row = event.target.closest("[data-report-index]");
      if (!row) {
        return;
      }

      state.reports.selectedIndex = Number(row.dataset.reportIndex);
      render();
    });

    view.elements.refreshStructureButton?.addEventListener("click", () => refreshStructureData(true));
    view.elements.loadStructureDemoButton?.addEventListener("click", loadStructureDemo);
    view.elements.newStructureButton?.addEventListener("click", openCreateStructureModal);
    view.elements.editStructureButton?.addEventListener("click", openEditStructureModal);
    view.elements.deleteStructureButton?.addEventListener("click", openGenericDeleteModalForActiveModule);

    view.elements.structureSearchInput?.addEventListener("input", (event) => {
      state.structure.search = event.target.value.trim();
      window.clearTimeout(state.structure.searchTimer);
      state.structure.searchTimer = window.setTimeout(() => {
        refreshStructureData(false);
      }, 260);
    });

    view.elements.structureDepartmentFilter?.addEventListener("change", (event) => {
      state.structure.idDepartamento = event.target.value || "";
      refreshStructureData(false);
    });

    view.elements.structureFilterRow?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-structure-branch]");
      if (!button) {
        return;
      }

      const nextKey = button.dataset.structureBranch || "TODOS";
      if (nextKey === state.structure.branchKey) {
        return;
      }

      state.structure.branchKey = nextKey;
      refreshStructureData(false);
    });

    view.elements.structureTreeBody?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-structure-id]");
      if (!button) {
        return;
      }

      state.structure.selectedId = Number(button.dataset.structureId || 0) || null;
      render();
    });

    view.elements.refreshAuditButton?.addEventListener("click", () => refreshAuditData(true));

    view.elements.auditSearchInput?.addEventListener("input", (event) => {
      state.audit.search = event.target.value.trim();
      window.clearTimeout(state.audit.searchTimer);
      state.audit.searchTimer = window.setTimeout(() => {
        refreshAuditData(false);
      }, 260);
    });

    view.elements.auditDateFrom?.addEventListener("change", (event) => {
      state.audit.dateFrom = event.target.value || state.audit.dateFrom;
      refreshAuditData(false);
    });

    view.elements.auditDateTo?.addEventListener("change", (event) => {
      state.audit.dateTo = event.target.value || state.audit.dateTo;
      refreshAuditData(false);
    });

    view.elements.auditProcessFilter?.addEventListener("change", (event) => {
      state.audit.process = event.target.value || "";
      refreshAuditData(false);
    });

    view.elements.auditTableBody?.addEventListener("click", (event) => {
      const row = event.target.closest("[data-audit-index]");
      if (!row) {
        return;
      }

      state.audit.selectedIndex = Number(row.dataset.auditIndex);
      render();
    });

    bindSanitizers();
  };

  const boot = async () => {
    const session = model.getSession();

    if (!session) {
      redirectToLogin();
      return;
    }

    view.forceCloseOverlays();
    view.setSession(session);
    bindEvents();
    render();
    await ensureBoardDataLoaded();
  };

  boot();
})();
