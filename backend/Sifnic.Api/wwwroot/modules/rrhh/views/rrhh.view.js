window.RRHHView = (() => {
  const model = window.RRHHModel;

  const elements = {
    sessionUser: document.getElementById("sessionUser"),
    sessionMeta: document.getElementById("sessionMeta"),
    backToDashboard: document.getElementById("backToDashboard"),
    closeSession: document.getElementById("closeSession"),
    rrhhGlobalSearch: document.getElementById("rrhhGlobalSearch"),
    rrhhShortcutActions: document.querySelector(".rrhh-command-actions"),
    mainNav: document.getElementById("mainNav"),
    workspaceKicker: document.getElementById("workspaceKicker"),
    workspaceTitle: document.getElementById("workspaceTitle"),
    workspaceSubtitle: document.getElementById("workspaceSubtitle"),
    workspaceTrail: document.getElementById("workspaceTrail"),
    workspaceBackButton: document.getElementById("workspaceBackButton"),
    groupBoard: document.getElementById("groupBoard"),
    placeholderShell: document.getElementById("placeholderShell"),
    placeholderStats: document.getElementById("placeholderStats"),
    placeholderActions: document.getElementById("placeholderActions"),
    employeeShell: document.getElementById("employeeShell"),
    contractShell: document.getElementById("contractShell"),
    workflowShell: document.getElementById("workflowShell"),
    configShell: document.getElementById("configShell"),
    configGrid: document.getElementById("configGrid"),
    clockShell: document.getElementById("clockShell"),
    reportShell: document.getElementById("reportShell"),
    structureShell: document.getElementById("structureShell"),
    auditShell: document.getElementById("auditShell"),
    searchInput: document.getElementById("searchInput"),
    statusFilter: document.getElementById("statusFilter"),
    refreshButton: document.getElementById("refreshButton"),
    newEmployeeButton: document.getElementById("newEmployeeButton"),
    viewEmployeeButton: document.getElementById("viewEmployeeButton"),
    editEmployeeButton: document.getElementById("editEmployeeButton"),
    deleteEmployeeButton: document.getElementById("deleteEmployeeButton"),
    employeeStatusChips: document.getElementById("employeeStatusChips"),
    employeeContentGrid: document.getElementById("employeeContentGrid"),
    employeeDetailCard: document.getElementById("employeeDetailCard"),
    tableCounter: document.getElementById("tableCounter"),
    employeeTableBody: document.getElementById("employeeTableBody"),
    detailTitle: document.getElementById("detailTitle"),
    detailBody: document.getElementById("detailBody"),
    contractSearchInput: document.getElementById("contractSearchInput"),
    contractStatusFilter: document.getElementById("contractStatusFilter"),
    refreshContractButton: document.getElementById("refreshContractButton"),
    newContractButton: document.getElementById("newContractButton"),
    editContractButton: document.getElementById("editContractButton"),
    printContractButton: document.getElementById("printContractButton"),
    deleteContractButton: document.getElementById("deleteContractButton"),
    contractTableCounter: document.getElementById("contractTableCounter"),
    contractTableBody: document.getElementById("contractTableBody"),
    contractDetailTitle: document.getElementById("contractDetailTitle"),
    contractDetailBody: document.getElementById("contractDetailBody"),
    workflowSearchInput: document.getElementById("workflowSearchInput"),
    workflowStatusFilter: document.getElementById("workflowStatusFilter"),
    refreshWorkflowButton: document.getElementById("refreshWorkflowButton"),
    newWorkflowButton: document.getElementById("newWorkflowButton"),
    viewWorkflowButton: document.getElementById("viewWorkflowButton"),
    editWorkflowButton: document.getElementById("editWorkflowButton"),
    workflowExtraButton: document.getElementById("workflowExtraButton"),
    approveWorkflowButton: document.getElementById("approveWorkflowButton"),
    rejectWorkflowButton: document.getElementById("rejectWorkflowButton"),
    workflowStatusChips: document.getElementById("workflowStatusChips"),
    workflowContentGrid: document.getElementById("workflowContentGrid"),
    workflowDetailCard: document.getElementById("workflowDetailCard"),
    workflowPanelTitle: document.getElementById("workflowPanelTitle"),
    workflowTableCounter: document.getElementById("workflowTableCounter"),
    workflowTableHead: document.getElementById("workflowTableHead"),
    workflowTableBody: document.getElementById("workflowTableBody"),
    workflowDetailTitle: document.getElementById("workflowDetailTitle"),
    workflowDetailBody: document.getElementById("workflowDetailBody"),
    clockSearchInput: document.getElementById("clockSearchInput"),
    clockDateFrom: document.getElementById("clockDateFrom"),
    clockDateTo: document.getElementById("clockDateTo"),
    clockEmployeeFilter: document.getElementById("clockEmployeeFilter"),
    refreshClockButton: document.getElementById("refreshClockButton"),
    exportClockExcelButton: document.getElementById("exportClockExcelButton"),
    exportClockPdfButton: document.getElementById("exportClockPdfButton"),
    clockPanelKicker: document.getElementById("clockPanelKicker"),
    clockPanelTitle: document.getElementById("clockPanelTitle"),
    clockDashboardSummary: document.getElementById("clockDashboardSummary"),
    clockTableCounter: document.getElementById("clockTableCounter"),
    clockTableBody: document.getElementById("clockTableBody"),
    clockDetailTitle: document.getElementById("clockDetailTitle"),
    clockDetailBody: document.getElementById("clockDetailBody"),
    reportSearchInput: document.getElementById("reportSearchInput"),
    reportCutoffDate: document.getElementById("reportCutoffDate"),
    reportDepartmentFilter: document.getElementById("reportDepartmentFilter"),
    reportEmployeeStatusFilter: document.getElementById("reportEmployeeStatusFilter"),
    refreshReportButton: document.getElementById("refreshReportButton"),
    exportReportExcelButton: document.getElementById("exportReportExcelButton"),
    exportReportPdfButton: document.getElementById("exportReportPdfButton"),
    reportTableCounter: document.getElementById("reportTableCounter"),
    reportTableBody: document.getElementById("reportTableBody"),
    reportDetailTitle: document.getElementById("reportDetailTitle"),
    reportDetailBody: document.getElementById("reportDetailBody"),
    structureSearchInput: document.getElementById("structureSearchInput"),
    structureDepartmentFilter: document.getElementById("structureDepartmentFilter"),
    loadStructureDemoButton: document.getElementById("loadStructureDemoButton"),
    newStructureButton: document.getElementById("newStructureButton"),
    editStructureButton: document.getElementById("editStructureButton"),
    deleteStructureButton: document.getElementById("deleteStructureButton"),
    structureFilterRow: document.getElementById("structureFilterRow"),
    refreshStructureButton: document.getElementById("refreshStructureButton"),
    structureSummaryGrid: document.getElementById("structureSummaryGrid"),
    structureTableCounter: document.getElementById("structureTableCounter"),
    structureTreeBody: document.getElementById("structureTreeBody"),
    structureDetailTitle: document.getElementById("structureDetailTitle"),
    structureDetailBody: document.getElementById("structureDetailBody"),
    auditSearchInput: document.getElementById("auditSearchInput"),
    auditDateFrom: document.getElementById("auditDateFrom"),
    auditDateTo: document.getElementById("auditDateTo"),
    auditProcessFilter: document.getElementById("auditProcessFilter"),
    refreshAuditButton: document.getElementById("refreshAuditButton"),
    auditTableCounter: document.getElementById("auditTableCounter"),
    auditTableBody: document.getElementById("auditTableBody"),
    auditDetailTitle: document.getElementById("auditDetailTitle"),
    auditDetailBody: document.getElementById("auditDetailBody"),
    toastRegion: document.getElementById("toastRegion"),
    employeeModal: document.getElementById("employeeModal"),
    employeeModalKicker: document.getElementById("employeeModalKicker"),
    employeeModalTitle: document.getElementById("employeeModalTitle"),
    cancelEmployeeModal: document.getElementById("cancelEmployeeModal"),
    employeeForm: document.getElementById("employeeForm"),
    employeeId: document.getElementById("employeeId"),
    codigoEmpleado: document.getElementById("codigoEmpleado"),
    usuarioSistema: document.getElementById("usuarioSistema"),
    cedula: document.getElementById("cedula"),
    idDepartamento: document.getElementById("idDepartamento"),
    idCargo: document.getElementById("idCargo"),
    idSupervisorEmpleado: document.getElementById("idSupervisorEmpleado"),
    nombres: document.getElementById("nombres"),
    apellidos: document.getElementById("apellidos"),
    fechaIngreso: document.getElementById("fechaIngreso"),
    fechaNacimiento: document.getElementById("fechaNacimiento"),
    sexo: document.getElementById("sexo"),
    estadoCivil: document.getElementById("estadoCivil"),
    telefono: document.getElementById("telefono"),
    correo: document.getElementById("correo"),
    inss: document.getElementById("inss"),
    idBanco: document.getElementById("idBanco"),
    numeroCuentaBancaria: document.getElementById("numeroCuentaBancaria"),
    direccion: document.getElementById("direccion"),
    saveEmployeeButton: document.getElementById("saveEmployeeButton"),
    contractModal: document.getElementById("contractModal"),
    contractModalKicker: document.getElementById("contractModalKicker"),
    contractModalTitle: document.getElementById("contractModalTitle"),
    cancelContractModal: document.getElementById("cancelContractModal"),
    contractForm: document.getElementById("contractForm"),
    contractId: document.getElementById("contractId"),
    contractEmployeeId: document.getElementById("contractEmployeeId"),
    contractEmployeeHint: document.getElementById("contractEmployeeHint"),
    numeroContrato: document.getElementById("numeroContrato"),
    idTipoContrato: document.getElementById("idTipoContrato"),
    idHorarioLaboral: document.getElementById("idHorarioLaboral"),
    contractFechaInicio: document.getElementById("contractFechaInicio"),
    contractFechaFin: document.getElementById("contractFechaFin"),
    salarioBaseMensual: document.getElementById("salarioBaseMensual"),
    moneda: document.getElementById("moneda"),
    esContratoVigente: document.getElementById("esContratoVigente"),
    observacion: document.getElementById("observacion"),
    saveContractButton: document.getElementById("saveContractButton"),
    workflowModal: document.getElementById("workflowModal"),
    workflowModalKicker: document.getElementById("workflowModalKicker"),
    workflowModalTitle: document.getElementById("workflowModalTitle"),
    cancelWorkflowModal: document.getElementById("cancelWorkflowModal"),
    workflowForm: document.getElementById("workflowForm"),
    workflowRecordId: document.getElementById("workflowRecordId"),
    workflowFormFields: document.getElementById("workflowFormFields"),
    saveWorkflowButton: document.getElementById("saveWorkflowButton"),
    workflowResolveModal: document.getElementById("workflowResolveModal"),
    workflowResolveKicker: document.getElementById("workflowResolveKicker"),
    workflowResolveTitle: document.getElementById("workflowResolveTitle"),
    workflowResolveText: document.getElementById("workflowResolveText"),
    workflowResolveForm: document.getElementById("workflowResolveForm"),
    workflowApprovedDaysField: document.getElementById("workflowApprovedDaysField"),
    workflowApprovedDays: document.getElementById("workflowApprovedDays"),
    workflowResolutionObservation: document.getElementById("workflowResolutionObservation"),
    workflowResolveError: document.getElementById("workflowResolveError"),
    cancelWorkflowResolveModal: document.getElementById("cancelWorkflowResolveModal"),
    confirmWorkflowResolveButton: document.getElementById("confirmWorkflowResolveButton"),
    vacationBulkModal: document.getElementById("vacationBulkModal"),
    vacationBulkForm: document.getElementById("vacationBulkForm"),
    vacationBulkDate: document.getElementById("vacationBulkDate"),
    vacationBulkAmountHalf: document.getElementById("vacationBulkAmountHalf"),
    vacationBulkAmountFull: document.getElementById("vacationBulkAmountFull"),
    vacationBulkObservation: document.getElementById("vacationBulkObservation"),
    vacationBulkError: document.getElementById("vacationBulkError"),
    cancelVacationBulkModal: document.getElementById("cancelVacationBulkModal"),
    confirmVacationBulkButton: document.getElementById("confirmVacationBulkButton"),
    deleteModal: document.getElementById("deleteModal"),
    deleteTargetText: document.getElementById("deleteTargetText"),
    deleteForm: document.getElementById("deleteForm"),
    adminUsuario: document.getElementById("adminUsuario"),
    adminPassword: document.getElementById("adminPassword"),
    deleteError: document.getElementById("deleteError"),
    cancelDeleteModal: document.getElementById("cancelDeleteModal"),
    confirmDeleteButton: document.getElementById("confirmDeleteButton"),
    contractDeleteModal: document.getElementById("contractDeleteModal"),
    contractDeleteTargetText: document.getElementById("contractDeleteTargetText"),
    contractDeleteForm: document.getElementById("contractDeleteForm"),
    contractAdminUsuario: document.getElementById("contractAdminUsuario"),
    contractAdminPassword: document.getElementById("contractAdminPassword"),
    contractDeleteError: document.getElementById("contractDeleteError"),
    cancelContractDeleteModal: document.getElementById("cancelContractDeleteModal"),
    confirmContractDeleteButton: document.getElementById("confirmContractDeleteButton"),
    genericDeleteModal: document.getElementById("genericDeleteModal"),
    genericDeleteKicker: document.getElementById("genericDeleteKicker"),
    genericDeleteTitle: document.getElementById("genericDeleteTitle"),
    genericDeleteTargetText: document.getElementById("genericDeleteTargetText"),
    genericDeleteForm: document.getElementById("genericDeleteForm"),
    genericAdminUsuario: document.getElementById("genericAdminUsuario"),
    genericAdminPassword: document.getElementById("genericAdminPassword"),
    genericDeleteError: document.getElementById("genericDeleteError"),
    cancelGenericDeleteModal: document.getElementById("cancelGenericDeleteModal"),
    confirmGenericDeleteButton: document.getElementById("confirmGenericDeleteButton"),
    structureModal: document.getElementById("structureModal"),
    structureModalKicker: document.getElementById("structureModalKicker"),
    structureModalTitle: document.getElementById("structureModalTitle"),
    cancelStructureModal: document.getElementById("cancelStructureModal"),
    structureForm: document.getElementById("structureForm"),
    structureNodeId: document.getElementById("structureNodeId"),
    structureCodigoNodo: document.getElementById("structureCodigoNodo"),
    structureTipoNodo: document.getElementById("structureTipoNodo"),
    structureNombreNodo: document.getElementById("structureNombreNodo"),
    structureParentNodeId: document.getElementById("structureParentNodeId"),
    structureEmployeeId: document.getElementById("structureEmployeeId"),
    structureDepartmentId: document.getElementById("structureDepartmentId"),
    structurePositionId: document.getElementById("structurePositionId"),
    structureOrdenVisual: document.getElementById("structureOrdenVisual"),
    structureActivo: document.getElementById("structureActivo"),
    structureObservacion: document.getElementById("structureObservacion"),
    saveStructureButton: document.getElementById("saveStructureButton"),
  };

  const employeeFieldMap = {
    codigoEmpleado: elements.codigoEmpleado,
    usuarioSistema: elements.usuarioSistema,
    cedula: elements.cedula,
    idDepartamento: elements.idDepartamento,
    idCargo: elements.idCargo,
    idSupervisorEmpleado: elements.idSupervisorEmpleado,
    nombres: elements.nombres,
    apellidos: elements.apellidos,
    fechaIngreso: elements.fechaIngreso,
    fechaNacimiento: elements.fechaNacimiento,
    sexo: elements.sexo,
    estadoCivil: elements.estadoCivil,
    telefono: elements.telefono,
    correo: elements.correo,
    inss: elements.inss,
    idBanco: elements.idBanco,
    numeroCuentaBancaria: elements.numeroCuentaBancaria,
    direccion: elements.direccion,
  };

  const contractFieldMap = {
    idEmpleado: elements.contractEmployeeId,
    numeroContrato: elements.numeroContrato,
    idTipoContrato: elements.idTipoContrato,
    idHorarioLaboral: elements.idHorarioLaboral,
    fechaInicio: elements.contractFechaInicio,
    fechaFin: elements.contractFechaFin,
    salarioBaseMensual: elements.salarioBaseMensual,
    moneda: elements.moneda,
    observacion: elements.observacion,
  };

  const structureFieldMap = {
    codigoNodo: elements.structureCodigoNodo,
    tipoNodo: elements.structureTipoNodo,
    nombreNodo: elements.structureNombreNodo,
    idNodoPadre: elements.structureParentNodeId,
    idEmpleadoTitular: elements.structureEmployeeId,
    idDepartamento: elements.structureDepartmentId,
    idCargo: elements.structurePositionId,
    ordenVisual: elements.structureOrdenVisual,
    observacion: elements.structureObservacion,
  };

  let workflowFieldMap = {};

  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const getInitials = (value) => {
    const parts = String(value || "")
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2);

    if (!parts.length) {
      return "--";
    }

    return parts.map((item) => item.charAt(0).toUpperCase()).join("");
  };

  const getIconSvg = (name) => {
    const iconBody = {
      home: '<path d="M3 10.5 12 3l9 7.5" /><path d="M5 9.8V21h14V9.8" /><path d="M9 21v-6h6v6" />',
      logout:
        '<path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" /><path d="M10 17l5-5-5-5" /><path d="M15 12H3" />',
      back: '<path d="M15 18 9 12l6-6" /><path d="M9 12h10" />',
      eye:
        '<path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z" /><circle cx="12" cy="12" r="2.8" />',
      refresh: '<path d="M20 11a8 8 0 1 0 2 5.5" /><path d="M20 4v7h-7" />',
      plus: '<path d="M12 5v14" /><path d="M5 12h14" />',
      edit: '<path d="M12 20h9" /><path d="m16.5 3.5 4 4L8 20l-4 1 1-4Z" />',
      trash:
        '<path d="M4 7h16" /><path d="M10 11v6" /><path d="M14 11v6" /><path d="M6 7l1 13h10l1-13" /><path d="M9 7V4h6v3" />',
      print:
        '<path d="M7 9V4h10v5" /><rect x="6" y="14" width="12" height="6" rx="1" /><path d="M6 17H4a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2h-2" />',
      excel:
        '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z" /><path d="M14 3v5h5" /><path d="m8 12 4 6" /><path d="m12 12-4 6" />',
      pdf:
        '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z" /><path d="M14 3v5h5" /><path d="M8 13h3" /><path d="M8 17h6" /><path d="M8 9h4" />',
      approve: '<circle cx="12" cy="12" r="9" /><path d="m8.5 12 2.5 2.5 4.5-5" />',
      reject: '<circle cx="12" cy="12" r="9" /><path d="M9 9l6 6" /><path d="M15 9 9 15" />',
      save:
        '<path d="M5 21h14a1 1 0 0 0 1-1V7.5L16.5 4H5a1 1 0 0 0-1 1v15a1 1 0 0 0 1 1Z" /><path d="M8 21v-6h8v6" /><path d="M8 4v5h7" />',
      close: '<path d="M6 6 18 18" /><path d="M18 6 6 18" />',
      settings:
        '<path d="M4 6h6" /><path d="M14 6h6" /><circle cx="12" cy="6" r="2" /><path d="M4 12h10" /><path d="M18 12h2" /><circle cx="16" cy="12" r="2" /><path d="M4 18h2" /><path d="M10 18h10" /><circle cx="8" cy="18" r="2" />',
      audit: '<path d="M4 19h16" /><path d="M7 16V8" /><path d="M12 16V5" /><path d="M17 16v-4" />',
      users:
        '<path d="M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" /><circle cx="10" cy="7" r="3" /><path d="M20 21v-2a4 4 0 0 0-3-3.9" /><path d="M17 4.2a3 3 0 0 1 0 5.8" />',
      structure: '<path d="M12 5v4" /><path d="M6 13h12" /><path d="M6 13v4" /><path d="M12 13v4" /><path d="M18 13v4" /><rect x="4" y="17" width="4" height="3" rx="1" /><rect x="10" y="17" width="4" height="3" rx="1" /><rect x="16" y="17" width="4" height="3" rx="1" /><rect x="10" y="5" width="4" height="3" rx="1" />',
      calendar: '<rect x="3" y="5" width="18" height="16" rx="2" /><path d="M16 3v4M8 3v4M3 10h18" />',
      folder: '<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z" />',
      briefcase: '<rect x="3" y="7" width="18" height="12" rx="2" /><path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" /><path d="M3 12h18" />',
      bank: '<path d="M3 10 12 4l9 6" /><path d="M5 10v8M9 10v8M15 10v8M19 10v8" /><path d="M3 20h18" />',
      clock: '<circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" />',
      report: '<path d="M5 20V10" /><path d="M12 20V4" /><path d="M19 20v-7" />',
      shield: '<path d="M12 3 5 6v6c0 4.5 3 7.5 7 9 4-1.5 7-4.5 7-9V6l-7-3Z" /><path d="m9.5 12 1.8 1.8 3.7-4" />',
      contract: '<path d="M7 3h8l4 4v14H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z" /><path d="M15 3v4h4" /><path d="M9 12h6M9 16h6" />',
      money: '<path d="M3 7h18v10H3Z" /><path d="M12 10v4" /><path d="M10 12h4" /><path d="M7 10h.01M17 14h.01" />',
    };

    const body = iconBody[name] || iconBody.settings;
    return `
      <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        ${body}
      </svg>
    `;
  };

  const createButtonContent = (label, iconName, options = {}) => {
    const { spin = false } = options;
    return `
      <span class="button-inline${spin ? " is-spinning" : ""}">
        <span class="button-icon${spin ? " is-spinning" : ""}">
          ${getIconSvg(iconName)}
        </span>
        <span>${escapeHtml(label)}</span>
      </span>
    `;
  };

  const setButtonLabel = (button, label, iconName, options = {}) => {
    if (!button) {
      return;
    }

    const { rememberDefault = true, spin = false } = options;
    button.innerHTML = createButtonContent(label, iconName, { spin });

    if (rememberDefault) {
      button.dataset.defaultLabel = label;
      button.dataset.defaultIcon = iconName;
    }
  };

  const getGroupIconName = (groupId) => {
    switch (String(groupId || "").toLowerCase()) {
      case "empleados":
        return "users";
      case "nomina":
        return "money";
      default:
        return "folder";
    }
  };

  const getModuleIconName = (moduleId) => {
    const value = String(moduleId || "").toLowerCase();
    const map = {
      empleado: "users",
      accion_personal: "briefcase",
      contrato: "contract",
      tipo_contrato: "contract",
      estado_empleado: "shield",
      expediente_documento: "folder",
      departamento: "structure",
      cargo: "briefcase",
      horario_laboral: "clock",
      banco: "bank",
      configuracion_rrhh: "settings",
      reloj: "clock",
      bitacora_rrhh: "audit",
      solicitud_permiso: "calendar",
      tipo_permiso: "calendar",
      vacacion: "calendar",
      hora_extra: "clock",
      tipo_hora_extra: "clock",
      reporte_vacaciones_disponibles: "report",
      reporte_horas_trabajadas: "clock",
      nomina: "money",
      nomina_detalle: "money",
      nomina_detalle_concepto: "money",
      periodo_nomina: "calendar",
      estado_nomina: "shield",
      estado_periodo_nomina: "shield",
      concepto_nomina: "money",
      tipo_concepto_nomina: "money",
      parametro_nomina: "settings",
      parametro_contribucion: "settings",
      tabla_ir_laboral: "report",
      esquema_variable_empleado: "money",
      tipo_esquema_variable: "settings",
      regla_esquema_variable: "settings",
      meta_variable_empleado: "report",
      movimiento_variable_empleado: "money",
      devengado_variable_periodo: "money",
      descuento_fijo_empleado: "money",
      prestamo_empleado: "money",
      esquela_pago: "report",
      envio_esquela_pago: "report",
      estado_esquela_pago: "shield",
      estado_envio_esquela: "shield",
      liquidacion: "report",
      liquidacion_detalle: "report",
    };

    return map[value] || "folder";
  };

  const getActionIconName = (label) => {
    const value = String(label || "").toLowerCase();
    if (value.includes("consulta") || value.includes("reporte")) {
      return "report";
    }
    if (value.includes("registro") || value.includes("alta")) {
      return "plus";
    }
    if (value.includes("control") || value.includes("config")) {
      return "settings";
    }
    return "folder";
  };

  const formatSessionDate = (value) => {
    if (!value) {
      return "Sin registro";
    }

    try {
      return new Intl.DateTimeFormat("es-NI", {
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        hour12: false,
        timeZone: "America/Managua",
      }).format(new Date(value));
    } catch {
      return value;
    }
  };

  const formatSex = (value) => {
    if (value === "M") {
      return "Masculino";
    }

    if (value === "F") {
      return "Femenino";
    }

    return value || "-";
  };

  const formatHours = (value) => {
    if (value === null || value === undefined || value === "") {
      return "-";
    }

    const numeric = Number(value);
    if (!Number.isFinite(numeric)) {
      return String(value);
    }

    return Number.isInteger(numeric) ? String(numeric) : numeric.toFixed(2);
  };

  const formatDateTime = (value) => {
    if (!value) {
      return "-";
    }

    try {
      return new Intl.DateTimeFormat("es-NI", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hour12: false,
        timeZone: "America/Managua",
      }).format(new Date(String(value).replace(" ", "T")));
    } catch {
      return value;
    }
  };

  const formatDecimal = (value) => {
    const numeric = Number(value ?? 0);
    if (!Number.isFinite(numeric)) {
      return "-";
    }

    return Number.isInteger(numeric) ? String(numeric) : numeric.toFixed(2);
  };

  const monthLabels = [
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

  const toNumber = (value) => {
    const numeric = Number(value ?? 0);
    return Number.isFinite(numeric) ? numeric : 0;
  };

  const formatSignedHours = (value) => {
    const numeric = toNumber(value);
    const sign = numeric > 0 ? "+" : numeric < 0 ? "-" : "";
    return `${sign}${formatDecimal(Math.abs(numeric))} h`;
  };

  const getHoursDiffTone = (value) => {
    const numeric = toNumber(value);
    if (numeric > 0.004) {
      return "is-success";
    }

    if (numeric < -0.004) {
      return "is-danger";
    }

    return "is-accent";
  };

  const getClockMonthKey = (isoDate) => {
    const value = String(isoDate || "");
    const match = value.match(/^(\d{4})-(\d{2})/);
    if (!match) {
      return "sin-fecha";
    }

    return `${match[1]}-${match[2]}`;
  };

  const getClockMonthLabel = (key) => {
    if (key === "sin-fecha") {
      return "Sin fecha";
    }

    const [year, month] = key.split("-");
    const monthIndex = Number(month) - 1;
    return `${monthLabels[monthIndex] || month} ${year}`;
  };

  const buildHoursDashboardData = (rows = []) => {
    const monthMap = new Map();
    const employeeIds = new Set();

    const totals = rows.reduce(
      (acc, row) => {
        const worked = toNumber(row.horasTrabajadas);
        const diff = toNumber(row.horasExtraMenos);
        const employeeKey = row.idEmpleado || row.codigoEmpleado || row.cedula;
        const monthKey = getClockMonthKey(row.fechaOperacion);

        if (employeeKey) {
          employeeIds.add(String(employeeKey));
        }

        if (!monthMap.has(monthKey)) {
          monthMap.set(monthKey, {
            key: monthKey,
            label: getClockMonthLabel(monthKey),
            worked: 0,
            diff: 0,
            days: 0,
            open: 0,
          });
        }

        const month = monthMap.get(monthKey);
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
      employees: employeeIds.size,
      months: Array.from(monthMap.values()).sort((a, b) => b.key.localeCompare(a.key)),
    };
  };

  const normalizeWorkflowStatus = (status) => String(status || "").trim().toUpperCase();

  const getWorkflowTone = (status) => {
    const value = normalizeWorkflowStatus(status);

    if (value.includes("APROBAD")) {
      return "status-success";
    }

    if (value.includes("RECHAZ")) {
      return "status-danger";
    }

    return "status-warning";
  };

  const getCatalogTone = (active) => (active ? "status-success" : "status-warning");

  const getDocumentTone = (status) => {
    const value = normalizeWorkflowStatus(status);

    if (value.includes("VENCID")) {
      return "status-danger";
    }

    if (value.includes("POR_VENCER") || value.includes("SIN_ARCHIVO")) {
      return "status-warning";
    }

    return "status-success";
  };

  const fillSelect = (element, items, placeholder, selectedValue = "") => {
    if (!element) {
      return;
    }

    element.innerHTML = [
      `<option value="">${escapeHtml(placeholder)}</option>`,
      ...items.map(
        (item) =>
          `<option value="${escapeHtml(item.value ?? item.id)}"${
            String(item.value ?? item.id) === String(selectedValue) ? " selected" : ""
          }>${escapeHtml(item.label ?? item.name)}</option>`,
      ),
    ].join("");
  };

  const getActionTypeBehavior = (value) => {
    const actionType = String(value || "").trim().toUpperCase();
    return {
      actionType,
      needsPosition: ["PROMOCION", "TRASLADO"].includes(actionType),
      needsSalary: ["PROMOCION", "CAMBIO SALARIAL"].includes(actionType),
      needsContractEnd: actionType === "PRORROGA CONTRATO",
    };
  };

  const findEmployeeOption = (employees, employeeId) =>
    (employees || []).find((employee) => Number(employee.id) === Number(employeeId)) || null;

  const findPositionOption = (positions, positionId) =>
    (positions || []).find((position) => Number(position.id) === Number(positionId)) || null;

  const resolveContractEmployeeAlert = (employee) => {
    const rawCode = String(employee?.contractAlertCode || "").trim().toUpperCase();
    const currentContractId = Number(employee?.currentContractId || 0);
    const endDateIso = String(employee?.currentContractEndDate || "").trim();
    const fallbackLabel = String(employee?.contractAlertLabel || "").trim();

    if (rawCode === "SIN_CONTRATO" || rawCode === "POR_VENCER") {
      return {
        code: rawCode,
        label: fallbackLabel || (rawCode === "SIN_CONTRATO" ? "Sin contrato vigente" : "Por vencer"),
      };
    }

    if (!currentContractId) {
      return {
        code: "SIN_CONTRATO",
        label: fallbackLabel || "Sin contrato vigente",
      };
    }

    if (endDateIso) {
      const today = new Date();
      today.setHours(0, 0, 0, 0);

      const endDate = new Date(`${endDateIso}T00:00:00`);
      if (!Number.isNaN(endDate.getTime())) {
        const diffDays = Math.floor((endDate.getTime() - today.getTime()) / (24 * 60 * 60 * 1000));
        if (diffDays >= 0 && diffDays <= 30) {
          return {
            code: "POR_VENCER",
            label: fallbackLabel || `Por vencer ${model.formatShortDate(endDateIso)}`,
          };
        }
      }
    }

    if (/sin contrato/i.test(fallbackLabel)) {
      return { code: "SIN_CONTRATO", label: fallbackLabel };
    }

    if (/por vencer/i.test(fallbackLabel)) {
      return { code: "POR_VENCER", label: fallbackLabel };
    }

    return {
      code: "",
      label: "",
    };
  };

  const buildContractEmployeeOptionLabel = (employee) => {
    const base = `${employee.code} - ${employee.name}`;
    const alert = resolveContractEmployeeAlert(employee);
    if (!alert.label) {
      return base;
    }

    return `${base} [${alert.label}]`;
  };

  const isEligibleContractEmployee = (employee, selectedEmployeeId = null) => {
    if (!employee || !employee.id) {
      return false;
    }

    if (selectedEmployeeId && Number(employee.id) === Number(selectedEmployeeId)) {
      return true;
    }

    const alert = resolveContractEmployeeAlert(employee);
    return alert.code === "SIN_CONTRATO" || alert.code === "POR_VENCER";
  };

  const renderContractEmployeeHint = (employee, mode = "create") => {
    if (!elements.contractEmployeeHint) {
      return;
    }

    if (!employee) {
      elements.contractEmployeeHint.hidden = true;
      elements.contractEmployeeHint.innerHTML = "";
      return;
    }

    const alert = resolveContractEmployeeAlert(employee);
    const tone =
      alert.code === "SIN_CONTRATO"
        ? "success"
        : alert.code === "POR_VENCER"
          ? "warning"
          : "accent";
    const lines = [
      `<strong>${escapeHtml(employee.code)} - ${escapeHtml(employee.name)}</strong>`,
      `${escapeHtml(employee.position || "Sin cargo")} · ${escapeHtml(employee.department || "Sin departamento")}`,
    ];

    if (alert.label) {
      lines.push(`<span class="status-pill status-pill-inline is-${tone}">${escapeHtml(alert.label)}</span>`);
    } else if (mode === "edit") {
      lines.push(`<span class="status-pill status-pill-inline is-accent">Contrato actual</span>`);
    }

    if (employee.currentContractNumber) {
      lines.push(
        `<span class="detail-note">Contrato vigente: ${escapeHtml(employee.currentContractNumber)}${employee.currentContractEndDate ? ` · vence ${escapeHtml(model.formatShortDate(employee.currentContractEndDate))}` : ""}</span>`,
      );
    }

    elements.contractEmployeeHint.className = `balance-card form-field-full is-${tone}`;
    elements.contractEmployeeHint.innerHTML = lines.join("<br />");
    elements.contractEmployeeHint.hidden = false;
  };

  const formatActionMoney = (amount, currency) =>
    amount === null || amount === undefined || amount === ""
      ? "-"
      : model.formatMoney(Number(amount), currency || "NIO");

  const buildActionMemoPreview = (record, employee, nextPosition, actionType) => {
    const pieces = [
      `Por medio del presente se notifica a ${employee?.name || record.nombreEmpleado || "el colaborador"} la accion de personal con fecha ${escapeHtml(model.formatShortDate(record.fechaAccion || record.startDate || todayIsoPlaceholder()))}.`,
    ];

    if (actionType === "PROMOCION") {
      pieces.push(`Se oficializa su promocion al puesto de ${nextPosition?.name || record.nombreCargoNuevo || "nuevo cargo"}.`);
    } else if (actionType === "TRASLADO") {
      pieces.push(`Se oficializa su traslado al puesto de ${nextPosition?.name || record.nombreCargoNuevo || "nuevo cargo"} en ${nextPosition?.department || record.nombreDepartamentoNuevo || "la nueva area"}.`);
    } else if (actionType === "CAMBIO SALARIAL") {
      pieces.push(`Se comunica el ajuste salarial a ${formatActionMoney(record.nuevoSalarioBaseMensual || record.numberValue1, record.monedaSalario || employee?.currentCurrency || "NIO")}.`);
    } else if (actionType === "PRORROGA CONTRATO") {
      pieces.push(`Se comunica la prorroga del contrato temporal hasta el ${escapeHtml(model.formatShortDate(record.nuevaFechaFinContrato || record.fechaFin || ""))}.`);
    } else if (actionType) {
      pieces.push(`Se registra la accion ${actionType.toLowerCase()}.`);
    }

    if (nextPosition?.hierarchyLabel || record.jerarquiaNueva) {
      pieces.push(`Jerarquia asignada: ${nextPosition?.hierarchyLabel || record.jerarquiaNueva}.`);
    }

    if (record.descripcionAccion || record.observacion || record.descripcion) {
      pieces.push(`Detalle: ${record.descripcionAccion || record.observacion || record.descripcion}.`);
    }

    return pieces.join(" ");
  };

  const todayIsoPlaceholder = () => new Date().toISOString().slice(0, 10);

  const syncActionWorkflowForm = (catalogs = {}, record = {}) => {
    const workflowElements = getWorkflowElements();
    if (!workflowElements.employee || !workflowElements.typeText) {
      return;
    }

    const employee = findEmployeeOption(catalogs.employees, workflowElements.employee.value) || null;
    const nextPosition = findPositionOption(catalogs.positions, workflowElements.related?.value || record.idCargoNuevo) || null;
    const behavior = getActionTypeBehavior(workflowElements.typeText.value || record.tipoAccion);

    if (workflowElements.employeeHint) {
      if (employee) {
        const hintTone =
          employee.contractAlertCode === "SIN_CONTRATO"
            ? "warning"
            : employee.contractAlertCode === "POR_VENCER"
              ? "warning"
              : "accent";
        workflowElements.employeeHint.className = `balance-card form-field-full is-${hintTone}`;
        workflowElements.employeeHint.hidden = false;
        workflowElements.employeeHint.innerHTML = [
          `<strong>${escapeHtml(employee.code)} - ${escapeHtml(employee.name)}</strong>`,
          `${escapeHtml(employee.position || "Sin cargo")} · ${escapeHtml(employee.department || "Sin departamento")}`,
          employee.contractAlertLabel
            ? `<span class="status-pill status-pill-inline is-${hintTone}">${escapeHtml(employee.contractAlertLabel)}</span>`
            : "",
        ]
          .filter(Boolean)
          .join("<br />");
      } else {
        workflowElements.employeeHint.hidden = true;
        workflowElements.employeeHint.innerHTML = "";
      }
    }

    if (workflowElements.actionPositionGroup) {
      workflowElements.actionPositionGroup.hidden = !behavior.needsPosition;
    }

    if (workflowElements.actionSalaryGroup) {
      workflowElements.actionSalaryGroup.hidden = !behavior.needsSalary;
    }

    if (workflowElements.actionContractGroup) {
      workflowElements.actionContractGroup.hidden = !behavior.needsContractEnd;
    }

    if (workflowElements.actionCurrentCard) {
      const currentHierarchy = employee?.hierarchyLabel || record.jerarquiaActual || "Sin jerarquia definida";
      const currentSalary =
        formatActionMoney(
          employee?.currentSalary ?? record.salarioActual,
          employee?.currentCurrency || record.monedaSalario || "NIO",
        );
      const currentContractEnd = employee?.currentContractEndDate
        ? model.formatShortDate(employee.currentContractEndDate)
        : record.fechaFinContratoActual
          ? model.formatShortDate(record.fechaFinContratoActual)
          : "Sin fecha fin";

      workflowElements.actionCurrentCard.innerHTML = `
        <div class="action-snapshot-grid">
          <div class="detail-row"><span>Puesto actual</span><strong>${escapeHtml(employee?.position || record.nombreCargo || "-")}</strong></div>
          <div class="detail-row"><span>Jerarquia actual</span><strong>${escapeHtml(currentHierarchy)}</strong></div>
          <div class="detail-row"><span>Salario actual</span><strong>${escapeHtml(currentSalary)}</strong></div>
          <div class="detail-row"><span>Contrato actual</span><strong>${escapeHtml(employee?.currentContractNumber || record.currentContractNumber || "Sin contrato vigente")}</strong></div>
          <div class="detail-row"><span>Vence</span><strong>${escapeHtml(currentContractEnd)}</strong></div>
        </div>
      `;
    }

    if (workflowElements.actionCurrentHierarchy) {
      workflowElements.actionCurrentHierarchy.textContent = employee?.hierarchyLabel || record.jerarquiaActual || "Sin jerarquia definida";
    }

    if (workflowElements.actionCurrentSalary) {
      workflowElements.actionCurrentSalary.textContent = formatActionMoney(
        employee?.currentSalary ?? record.salarioActual,
        employee?.currentCurrency || record.monedaSalario || "NIO",
      );
    }

    if (workflowElements.actionCurrentContractEnd) {
      workflowElements.actionCurrentContractEnd.textContent = employee?.currentContractEndDate
        ? model.formatShortDate(employee.currentContractEndDate)
        : record.fechaFinContratoActual
          ? model.formatShortDate(record.fechaFinContratoActual)
          : "Sin fecha fin";
    }

    if (workflowElements.actionNextHierarchy) {
      workflowElements.actionNextHierarchy.textContent =
        nextPosition?.hierarchyLabel ||
        record.jerarquiaNueva ||
        (behavior.needsPosition ? "Selecciona el nuevo cargo." : "No aplica para esta accion.");
    }

    if (workflowElements.actionMemoPreview) {
      workflowElements.actionMemoPreview.textContent = buildActionMemoPreview(
        {
          ...record,
          fechaAccion: workflowElements.startDate?.value || record.fechaAccion,
          descripcionAccion: workflowElements.observation?.value || record.descripcionAccion,
          nuevoSalarioBaseMensual: workflowElements.numberValue1?.value || record.nuevoSalarioBaseMensual,
          nuevaFechaFinContrato: workflowElements.endDate?.value || record.nuevaFechaFinContrato,
        },
        employee,
        nextPosition,
        behavior.actionType,
      );
    }
  };

  const getWorkflowElements = () => ({
    code: document.getElementById("workflowCode"),
    name: document.getElementById("workflowName"),
    employee: document.getElementById("workflowEmployeeId"),
    employeeHint: document.getElementById("workflowActionEmployeeHint"),
    type: document.getElementById("workflowTypeId"),
    typeText: document.getElementById("workflowTypeText"),
    related: document.getElementById("workflowRelatedId"),
    startDate: document.getElementById("workflowStartDate"),
    endDate: document.getElementById("workflowEndDate"),
    numberValue1: document.getElementById("workflowNumberValue1"),
    numberValue2: document.getElementById("workflowNumberValue2"),
    hoursDate: document.getElementById("workflowHoursDate"),
    hoursAmount: document.getElementById("workflowHoursAmount"),
    integerValue1: document.getElementById("workflowIntegerValue1"),
    flagValue1: document.getElementById("workflowFlagValue1"),
    halfDay: document.getElementById("workflowHalfDay"),
    halfDayGroup: document.getElementById("workflowHalfDayGroup"),
    halfDayMorning: document.getElementById("workflowHalfDayMorning"),
    halfDayAfternoon: document.getElementById("workflowHalfDayAfternoon"),
    vacationBalance: document.getElementById("workflowVacationBalance"),
    active: document.getElementById("workflowActive"),
    observation: document.getElementById("workflowObservation"),
    file: document.getElementById("workflowFile"),
    removeFile: document.getElementById("workflowRemoveFile"),
    actionPositionGroup: document.getElementById("workflowActionPositionGroup"),
    actionSalaryGroup: document.getElementById("workflowActionSalaryGroup"),
    actionContractGroup: document.getElementById("workflowActionContractGroup"),
    actionCurrentCard: document.getElementById("workflowActionCurrentCard"),
    actionCurrentHierarchy: document.getElementById("workflowActionCurrentHierarchy"),
    actionCurrentSalary: document.getElementById("workflowActionCurrentSalary"),
    actionCurrentContractEnd: document.getElementById("workflowActionCurrentContractEnd"),
    actionNextHierarchy: document.getElementById("workflowActionNextHierarchy"),
    actionMemoPreview: document.getElementById("workflowActionMemoPreview"),
  });

  const buildWorkflowFieldMap = (moduleId) => {
    const workflowElements = getWorkflowElements();
    const catalogConfig = model.getCatalogModuleConfig(moduleId);

    if (catalogConfig) {
      return {
        codigo: workflowElements.code,
        nombre: workflowElements.name,
        descripcion: workflowElements.observation,
        relatedId: workflowElements.related,
        numberValue1: workflowElements.numberValue1,
        numberValue2: workflowElements.numberValue2,
        integerValue1: workflowElements.integerValue1,
        flagValue1: workflowElements.flagValue1,
        activo: workflowElements.active,
      };
    }

    if (moduleId === "accion_personal") {
      return {
        idEmpleado: workflowElements.employee,
        tipoAccion: workflowElements.typeText,
        fechaAccion: workflowElements.startDate,
        idCargoNuevo: workflowElements.related,
        nuevoSalarioBaseMensual: workflowElements.numberValue1,
        nuevaFechaFinContrato: workflowElements.endDate,
        aplicarCambioOperativo: workflowElements.flagValue1,
        descripcionAccion: workflowElements.observation,
      };
    }

    if (moduleId === "expediente_documento") {
      return {
        idEmpleado: workflowElements.employee,
        tipoDocumento: workflowElements.typeText,
        fechaDocumento: workflowElements.startDate,
        fechaVencimiento: workflowElements.endDate,
        archivo: workflowElements.file,
        observacion: workflowElements.observation,
      };
    }

    if (moduleId === "solicitud_permiso") {
      return {
        idEmpleado: workflowElements.employee,
        idTipoPermiso: workflowElements.type,
        fechaInicio: workflowElements.startDate,
        fechaFin: workflowElements.endDate,
        jornadaMedioDia: workflowElements.halfDayMorning,
        observacion: workflowElements.observation,
      };
    }

    if (moduleId === "vacacion") {
      return {
        idEmpleado: workflowElements.employee,
        fechaInicio: workflowElements.startDate,
        fechaFin: workflowElements.endDate,
        esMedioDia: workflowElements.halfDay,
        jornadaMedioDia: workflowElements.halfDayMorning,
        observacionSolicitud: workflowElements.observation,
      };
    }

    return {
      idEmpleado: workflowElements.employee,
      idTipoHoraExtra: workflowElements.type,
      fechaHoraExtra: workflowElements.hoursDate,
      cantidadHoras: workflowElements.hoursAmount,
      observacion: workflowElements.observation,
    };
  };

  const syncBodyState = () => {
    const anyOpen = [
      elements.employeeModal,
      elements.contractModal,
      elements.structureModal,
      elements.workflowModal,
      elements.workflowResolveModal,
      elements.vacationBulkModal,
      elements.deleteModal,
      elements.contractDeleteModal,
      elements.genericDeleteModal,
    ].some((item) => item && !item.hidden);

    document.body.classList.toggle("modal-open", anyOpen);
  };

  const clearFormErrors = (formElement) => {
    formElement
      ?.querySelectorAll(".is-invalid")
      .forEach((item) => item.classList.remove("is-invalid"));

    formElement
      ?.querySelectorAll("[data-error-for]")
      .forEach((item) => {
        item.textContent = "";
      });
  };

  const setFormErrors = (formElement, fieldMap, errors = {}) => {
    clearFormErrors(formElement);

    Object.entries(errors).forEach(([fieldName, message]) => {
      fieldMap[fieldName]?.classList.add("is-invalid");

      const error = formElement?.querySelector(`[data-error-for="${fieldName}"]`);
      if (error) {
        error.textContent = message;
      }
    });
  };

  const clearFieldError = (formElement, fieldMap, fieldName) => {
    fieldMap[fieldName]?.classList.remove("is-invalid");

    const error = formElement?.querySelector(`[data-error-for="${fieldName}"]`);
    if (error) {
      error.textContent = "";
    }
  };

  const focusField = (fieldMap, fieldName) => {
    fieldMap[fieldName]?.focus();
  };

  const resetSaveButton = (button, label = button?.dataset.defaultLabel || "Guardar") => {
    if (!button) {
      return;
    }

    button.disabled = false;
    button.classList.remove("is-loading", "is-success");
    setButtonLabel(button, label, button.dataset.defaultIcon || "save");
  };

  const setSaveButtonState = (button, label, state) => {
    if (!button) {
      return;
    }

    const icon =
      state === "loading"
        ? '<span class="button-spinner" aria-hidden="true"></span>'
        : '<span class="button-check" aria-hidden="true">&#10003;</span>';

    button.innerHTML = `
      <span class="button-inline-state">
        ${icon}
        <span>${escapeHtml(label)}</span>
      </span>
    `;
  };

  const hideAllShells = () => {
    elements.groupBoard.hidden = true;
    elements.placeholderShell.hidden = true;
    elements.employeeShell.hidden = true;
    elements.contractShell.hidden = true;
    elements.workflowShell.hidden = true;
    elements.configShell.hidden = true;
    elements.clockShell.hidden = true;
    elements.reportShell.hidden = true;
    elements.structureShell.hidden = true;
    elements.auditShell.hidden = true;
  };

  const setSession = (session) => {
    elements.sessionUser.textContent = session.user || "Usuario";
    elements.sessionMeta.textContent = `Acceso ${formatSessionDate(session.loginAt)}`;
  };

  const applyStaticButtonDecorations = () => {
    setButtonLabel(elements.backToDashboard, "Panel principal", "home");
    setButtonLabel(elements.closeSession, "Cerrar sesion", "logout");
    setButtonLabel(elements.workspaceBackButton, "Volver", "back");
    setButtonLabel(elements.refreshButton, "Actualizar", "refresh");
    setButtonLabel(elements.newEmployeeButton, "Nuevo", "plus");
    setButtonLabel(elements.viewEmployeeButton, "Ver ficha", "eye");
    setButtonLabel(elements.editEmployeeButton, "Editar", "edit");
    setButtonLabel(elements.deleteEmployeeButton, "Eliminar", "trash");
    setButtonLabel(elements.refreshContractButton, "Actualizar", "refresh");
    setButtonLabel(elements.newContractButton, "Nuevo", "plus");
    setButtonLabel(elements.editContractButton, "Editar", "edit");
    setButtonLabel(elements.printContractButton, "Imprimir", "print");
    setButtonLabel(elements.deleteContractButton, "Eliminar", "trash");
    setButtonLabel(elements.refreshWorkflowButton, "Actualizar", "refresh");
    setButtonLabel(elements.newWorkflowButton, "Nuevo", "plus");
    setButtonLabel(elements.viewWorkflowButton, "Ver detalle", "eye");
    setButtonLabel(elements.editWorkflowButton, "Editar", "edit");
    setButtonLabel(elements.workflowExtraButton, "Configurar", "settings");
    setButtonLabel(elements.approveWorkflowButton, "Aprobar", "approve");
    setButtonLabel(elements.rejectWorkflowButton, "Rechazar", "reject");
    setButtonLabel(elements.refreshClockButton, "Actualizar", "refresh");
    setButtonLabel(elements.exportClockExcelButton, "Excel", "excel");
    setButtonLabel(elements.exportClockPdfButton, "PDF", "pdf");
    setButtonLabel(elements.refreshReportButton, "Actualizar", "refresh");
    setButtonLabel(elements.exportReportExcelButton, "Excel", "excel");
    setButtonLabel(elements.exportReportPdfButton, "PDF", "pdf");
    setButtonLabel(elements.loadStructureDemoButton, "Cargar estructura base", "structure");
    setButtonLabel(elements.newStructureButton, "Nuevo", "plus");
    setButtonLabel(elements.editStructureButton, "Editar", "edit");
    setButtonLabel(elements.deleteStructureButton, "Eliminar", "trash");
    setButtonLabel(elements.refreshStructureButton, "Actualizar", "refresh");
    setButtonLabel(elements.refreshAuditButton, "Actualizar", "refresh");
    setButtonLabel(elements.cancelEmployeeModal, "Cancelar", "close");
    setButtonLabel(elements.saveEmployeeButton, "Guardar", "save");
    setButtonLabel(elements.cancelContractModal, "Cancelar", "close");
    setButtonLabel(elements.saveContractButton, "Guardar", "save");
    setButtonLabel(elements.cancelWorkflowModal, "Cancelar", "close");
    setButtonLabel(elements.saveWorkflowButton, "Guardar", "save");
    setButtonLabel(elements.cancelWorkflowResolveModal, "Cancelar", "close");
    setButtonLabel(elements.cancelVacationBulkModal, "Cancelar", "close");
    setButtonLabel(elements.confirmVacationBulkButton, "Aplicar ajuste", "calendar");
    setButtonLabel(elements.cancelDeleteModal, "Cancelar", "close");
    setButtonLabel(elements.confirmDeleteButton, "Confirmar eliminacion", "trash");
    setButtonLabel(elements.cancelContractDeleteModal, "Cancelar", "close");
    setButtonLabel(elements.confirmContractDeleteButton, "Confirmar eliminacion", "trash");
    setButtonLabel(elements.cancelGenericDeleteModal, "Cancelar", "close");
    setButtonLabel(elements.confirmGenericDeleteButton, "Confirmar eliminacion", "trash");
    setButtonLabel(elements.cancelStructureModal, "Cancelar", "close");
    setButtonLabel(elements.saveStructureButton, "Guardar", "save");
  };

  const renderMainNav = (groups, activeGroupId) => {
    const shouldShow = Array.isArray(groups) && groups.length > 1;
    elements.mainNav.hidden = !shouldShow;
    if (!shouldShow) {
      elements.mainNav.innerHTML = "";
      return;
    }

    elements.mainNav.innerHTML = groups
      .map(
        (group) => `
          <button
            class="tab-button${group.id === activeGroupId ? " is-active" : ""}"
            data-group-id="${escapeHtml(group.id)}"
            type="button"
          >
            <div class="tab-copy">
              <span class="tab-icon" aria-hidden="true">${getIconSvg(getGroupIconName(group.id))}</span>
              <strong>${escapeHtml(group.label)}</strong>
            </div>
          </button>
        `,
      )
      .join("");
  };

  const setWorkspaceHeader = ({ kicker, title, subtitle, trail, showBack }) => {
    elements.workspaceKicker.textContent = kicker || "";
    elements.workspaceKicker.hidden = !kicker;
    elements.workspaceTitle.textContent = title;
    elements.workspaceSubtitle.textContent = subtitle || "";
    elements.workspaceSubtitle.hidden = !subtitle;
    elements.workspaceTrail.textContent = trail || "";
    elements.workspaceTrail.hidden = !trail;
    elements.workspaceBackButton.hidden = !showBack;
  };

  const getMetricToneClass = (tone) => {
    switch (String(tone || "").toLowerCase()) {
      case "success":
        return "is-success";
      case "warning":
        return "is-warning";
      case "danger":
        return "is-danger";
      case "accent":
        return "is-accent";
      default:
        return "";
    }
  };

  const renderBoardOverview = (group, overview, busy) => {
    if (group?.id !== "empleados") {
      return "";
    }

    const summary = overview?.summary || null;
    const alerts = Array.isArray(overview?.alerts) ? overview.alerts : [];

    const summaryCards = summary
      ? [
          {
            label: "Colaboradores activos",
            value: summary.activeEmployees,
            detail: `${summary.totalEmployees} totales / ${summary.inactiveEmployees} inactivos`,
            tone: summary.employeesWithoutCurrentContract > 0 ? "warning" : "success",
          },
          {
            label: "Contratos vigentes",
            value: summary.currentContracts,
            detail: `${summary.expiringContracts} por vencer / ${summary.employeesWithoutCurrentContract} sin contrato`,
            tone: summary.expiringContracts > 0 ? "warning" : "success",
          },
          {
            label: "Pendientes RRHH",
            value: summary.pendingApprovals,
            detail: `${summary.pendingVacations} vacaciones / ${summary.pendingOvertime} horas extra`,
            tone: summary.pendingApprovals > 0 ? "warning" : "success",
          },
          {
            label: "Expedientes",
            value: summary.expiredDocuments,
            detail: `${summary.expiringDocuments} por vencer / ${summary.documentsWithoutFile} sin archivo`,
            tone: summary.expiredDocuments > 0 ? "danger" : summary.expiringDocuments > 0 ? "warning" : "success",
          },
          {
            label: "Reloj de hoy",
            value: summary.todayClockMarks,
            detail: `${summary.openClockShiftsToday} jornadas abiertas / ${summary.employeesMarkedToday} empleados marcados`,
            tone: summary.openClockShiftsToday > 0 ? "warning" : "accent",
          },
        ]
      : [];

    return `
      <section class="board-overview">
        <div class="board-overview-head">
          <div class="panel-copy">
            <span class="eyebrow">Control ejecutivo</span>
            <h3>Resumen de RRHH</h3>
          </div>

          <div class="board-overview-actions">
            <button class="ghost-button" data-module-id="configuracion_rrhh" type="button">
              ${createButtonContent("Configuraciones", "settings")}
            </button>
          </div>
        </div>

        <div class="board-summary-grid">
          ${
            summaryCards.length
              ? summaryCards
                  .map(
                    (card) => `
                      <article class="board-summary-card">
                        <span class="eyebrow">${escapeHtml(card.label)}</span>
                        <strong>${escapeHtml(String(card.value ?? 0))}</strong>
                        <span class="module-metric ${getMetricToneClass(card.tone)}">${escapeHtml(card.detail)}</span>
                      </article>
                    `,
                  )
                  .join("")
              : `
                <article class="board-summary-card board-summary-card-empty">
                  <strong>${busy ? "Cargando tablero..." : "Sin datos disponibles"}</strong>
                  <span>${busy ? "Estamos preparando los indicadores de RRHH." : "Aun no hay informacion consolidada."}</span>
                </article>
              `
          }
        </div>

        <div class="board-side-grid">
          <article class="board-side-card">
            <div class="board-side-head">
              <span class="eyebrow">Alertas</span>
              <strong>Atencion prioritaria</strong>
            </div>
            <div class="board-list">
              ${
                alerts.length
                  ? alerts
                      .map(
                        (alert) => `
                          <button class="board-list-item" data-module-id="${escapeHtml(alert.moduleId || "")}" type="button">
                            <span class="module-metric ${getMetricToneClass(alert.tone)}">${escapeHtml(alert.title)}</span>
                            <small>${escapeHtml(alert.detail || "")}</small>
                          </button>
                        `,
                      )
                      .join("")
                  : `
                    <div class="board-list-item is-static">
                      <span class="module-metric">Sin alertas abiertas</span>
                      <small>Los indicadores principales de RRHH se mantienen estables.</small>
                    </div>
                  `
              }
            </div>
          </article>
        </div>
      </section>
    `;
  };

  const renderGroupBoard = (group, overview = null, busy = false) => {
    const metrics = overview?.modules || {};

    elements.groupBoard.innerHTML = `${renderBoardOverview(group, overview, busy)}${group.buckets
      .map((bucket) => {
        const visibleModules = bucket.modules.filter((module) => !module.hiddenOnBoard);
        if (!visibleModules.length) {
          return "";
        }

        return `
          <section class="board-block">
            <header class="board-block-head">
              <div class="panel-copy">
                <h3>${escapeHtml(bucket.label)}</h3>
                <p class="board-block-subtitle">${escapeHtml(bucket.subtitle || "")}</p>
              </div>
            </header>

            <div class="module-grid">
              ${visibleModules
                .map(
                  (module) => {
                    const metric = metrics[module.id] || null;

                    return `
                    <button class="module-card" data-module-id="${escapeHtml(module.id)}" type="button">
                      <div class="module-card-top">
                        <span class="module-glyph" aria-hidden="true">${getIconSvg(getModuleIconName(module.id))}</span>
                        <span class="module-code">${escapeHtml(module.code)}</span>
                      </div>

                      <div class="module-card-copy">
                        <strong>${escapeHtml(module.label)}</strong>
                        <small>${escapeHtml(module.subtitle || "")}</small>
                      </div>

                      <div class="module-card-bottom">
                        ${
                          metric
                            ? `
                              <div class="module-metric-block">
                                <span class="module-metric ${getMetricToneClass(metric.tone)}">
                                  ${escapeHtml(String(metric.value ?? 0))} ${escapeHtml(metric.caption || "")}
                                </span>
                                <small>${escapeHtml(metric.detail || "")}</small>
                              </div>
                            `
                            : ""
                        }
                      </div>
                    </button>
                  `;
                  },
                )
                .join("")}
            </div>
          </section>
        `;
      })
      .join("")}`;
  };

  const showBoard = () => {
    hideAllShells();
    elements.groupBoard.hidden = false;
  };

  const showEmployeeShell = () => {
    hideAllShells();
    elements.employeeShell.hidden = false;
  };

  const showContractShell = () => {
    hideAllShells();
    elements.contractShell.hidden = false;
  };

  const showWorkflowShell = () => {
    hideAllShells();
    elements.workflowShell.hidden = false;
  };

  const showConfigShell = () => {
    hideAllShells();
    elements.configShell.hidden = false;
  };

  const showClockShell = () => {
    hideAllShells();
    elements.clockShell.hidden = false;
  };

  const configureClockShell = ({ panelKicker = "Control", panelTitle = "Jornadas marcadas" } = {}) => {
    if (elements.clockPanelKicker) {
      elements.clockPanelKicker.textContent = panelKicker;
    }

    if (elements.clockPanelTitle) {
      elements.clockPanelTitle.textContent = panelTitle;
    }
  };

  const showReportShell = () => {
    hideAllShells();
    elements.reportShell.hidden = false;
  };

  const showStructureShell = () => {
    hideAllShells();
    elements.structureShell.hidden = false;
  };

  const showAuditShell = () => {
    hideAllShells();
    elements.auditShell.hidden = false;
  };

  const renderPlaceholder = (module) => {
    const cards = (module.cards || []).slice(0, 3);
    const actionItems = cards.length ? cards.map((card) => card.title) : ["Consulta", "Registro", "Control"];

    elements.placeholderStats.innerHTML = cards
      .map(
        (card) => `
          <article class="placeholder-card">
            <strong>${escapeHtml(card.title)}</strong>
            <span>${escapeHtml(card.detail)}</span>
          </article>
        `,
      )
      .join("");

    elements.placeholderActions.innerHTML = actionItems
      .map(
        (item) => `
          <article class="placeholder-action">
            <span class="placeholder-action-icon" aria-hidden="true">${getIconSvg(getActionIconName(item))}</span>
            <strong>${escapeHtml(item)}</strong>
          </article>
        `,
      )
      .join("");

    hideAllShells();
    elements.placeholderShell.hidden = false;
  };

  const renderFilterToggleRow = (container, options, selected, dataAttribute) => {
    if (!container) {
      return;
    }

    container.innerHTML = options
      .map(
        (option) => `
          <button
            class="filter-toggle-chip${option.value === selected ? " is-active" : ""}"
            type="button"
            ${dataAttribute}="${escapeHtml(option.value)}"
          >
            ${escapeHtml(option.label)}
          </button>
        `,
      )
      .join("");
  };

  const renderStatusOptions = (options, selected) => {
    elements.statusFilter.innerHTML = options
      .map(
        (option) => `
          <option value="${escapeHtml(option.value)}"${option.value === selected ? " selected" : ""}>
            ${escapeHtml(option.label)}
          </option>
        `,
      )
      .join("");
    renderFilterToggleRow(elements.employeeStatusChips, options, selected, "data-employee-status");
  };

  const renderContractStatusOptions = (options, selected) => {
    elements.contractStatusFilter.innerHTML = options
      .map(
        (option) => `
          <option value="${escapeHtml(option.value)}"${option.value === selected ? " selected" : ""}>
            ${escapeHtml(option.label)}
          </option>
        `,
      )
      .join("");
  };

  const renderWorkflowStatusOptions = (options, selected) => {
    elements.workflowStatusFilter.innerHTML = options
      .map(
        (option) => `
          <option value="${escapeHtml(option.value)}"${option.value === selected ? " selected" : ""}>
            ${escapeHtml(option.label)}
          </option>
        `,
      )
      .join("");
    renderFilterToggleRow(elements.workflowStatusChips, options, selected, "data-workflow-status");
  };

  const setEmployeeDetailVisibility = (visible) => {
    if (!elements.employeeContentGrid || !elements.employeeDetailCard) {
      return;
    }

    elements.employeeContentGrid.classList.toggle("is-detail-collapsed", !visible);
    elements.employeeDetailCard.hidden = !visible;
    setButtonLabel(elements.viewEmployeeButton, visible ? "Ocultar ficha" : "Ver ficha", visible ? "arrowLeft" : "eye");
  };

  const setWorkflowDetailVisibility = (visible) => {
    if (!elements.workflowContentGrid || !elements.workflowDetailCard) {
      return;
    }

    elements.workflowContentGrid.classList.toggle("is-detail-collapsed", !visible);
    elements.workflowDetailCard.hidden = !visible;
    setButtonLabel(
      elements.viewWorkflowButton,
      visible ? "Ocultar detalle" : "Ver detalle",
      visible ? "arrowLeft" : "eye",
    );
  };

  const configureWorkflowShell = ({
    searchPlaceholder,
    newLabel = "Nuevo",
    editLabel = "Editar",
    approveLabel = "Accion",
    rejectLabel = "Eliminar",
    showApprove = true,
    showReject = true,
    extraLabel = "Configurar",
    extraIcon = "settings",
    showExtra = false,
  }) => {
    elements.workflowSearchInput.placeholder = searchPlaceholder || "Buscar";
    setButtonLabel(elements.newWorkflowButton, newLabel, "plus");
    setButtonLabel(elements.editWorkflowButton, editLabel, "edit");
    setButtonLabel(elements.approveWorkflowButton, approveLabel, "approve");
    setButtonLabel(elements.rejectWorkflowButton, rejectLabel, "reject");
    setButtonLabel(elements.workflowExtraButton, extraLabel, extraIcon);
    elements.approveWorkflowButton.hidden = !showApprove;
    elements.rejectWorkflowButton.hidden = !showReject;
    elements.workflowExtraButton.hidden = !showExtra;
  };

  const renderConfigShell = (group) => {
    const blocks = (group?.buckets || [])
      .map((bucket) => {
        const modules = (bucket.modules || []).filter((module) => module.type === "catalog");
        if (!modules.length) {
          return "";
        }

        return `
          <section class="board-block">
            <header class="board-block-head">
              <div class="panel-copy">
                <h3>${escapeHtml(bucket.label)}</h3>
              </div>
              <span class="counter-pill">${escapeHtml(String(modules.length))} catalogos</span>
            </header>

            <div class="module-grid">
              ${modules
                .map(
                  (module) => `
                    <button class="module-card" data-module-id="${escapeHtml(module.id)}" type="button">
                      <div class="module-card-top">
                        <span class="module-glyph" aria-hidden="true">${getIconSvg(getModuleIconName(module.id))}</span>
                        <span class="module-code">${escapeHtml(module.code)}</span>
                      </div>
                      <div class="module-card-copy">
                        <strong>${escapeHtml(module.label)}</strong>
                        <small>${escapeHtml(module.subtitle || "")}</small>
                      </div>
                      <div class="module-card-bottom">
                        <span class="module-metric">Administrar catalogo</span>
                      </div>
                    </button>
                  `,
                )
                .join("")}
            </div>
          </section>
        `;
      })
      .join("");

    elements.configGrid.innerHTML =
      blocks ||
      `
        <div class="detail-empty">
          <p>No hay configuraciones disponibles para este grupo.</p>
        </div>
      `;
  };

  const renderClockEmployeeOptions = (employees, selectedValue = "") => {
    fillSelect(
      elements.clockEmployeeFilter,
      employees.map((employee) => ({
        id: employee.id,
        name: `${employee.code} - ${employee.name}`,
      })),
      "Todos los empleados",
      selectedValue,
    );
  };

  const renderAuditProcessOptions = (processes, selectedValue = "") => {
    fillSelect(
      elements.auditProcessFilter,
      processes.map((process) => ({
        value: process,
        label: process,
      })),
      "Todos los procesos",
      selectedValue,
    );
  };

  const renderReportDepartmentOptions = (departments, selectedValue = "") => {
    fillSelect(
      elements.reportDepartmentFilter,
      departments.map((department) => ({
        id: department.id,
        name: department.name,
      })),
      "Todos los departamentos",
      selectedValue,
    );
  };

  const renderStructureDepartmentOptions = (departments, selectedValue = "") => {
    fillSelect(
      elements.structureDepartmentFilter,
      (departments || []).map((department) => ({
        value: department.id ?? department.Id ?? department.IdDepartamento,
        label: department.label || department.name || department.nombre || "",
      })),
      "Todos los departamentos",
      selectedValue,
    );
  };

  const renderReportEmployeeStatusOptions = (items, selectedValue = "") => {
    fillSelect(
      elements.reportEmployeeStatusFilter,
      items.map((item) => ({
        value: item.value,
        label: item.label,
      })),
      "Todos los estados",
      selectedValue,
    );
  };

  const renderTableLoading = () => {
    elements.employeeTableBody.innerHTML =
      '<tr><td class="table-message" colspan="6">Cargando registros...</td></tr>';
  };

  const renderContractTableLoading = () => {
    elements.contractTableBody.innerHTML =
      '<tr><td class="table-message" colspan="7">Cargando contratos...</td></tr>';
  };

  const renderWorkflowTableLoading = (label = "Novedades", colspan = 6) => {
    elements.workflowPanelTitle.textContent = label;
    elements.workflowTableHead.innerHTML = "";
    elements.workflowTableBody.innerHTML =
      `<tr><td class="table-message" colspan="${colspan}">Cargando registros...</td></tr>`;
  };

  const renderClockTableLoading = () => {
    elements.clockTableBody.innerHTML =
      '<tr><td class="table-message" colspan="7">Cargando marcaciones...</td></tr>';

    if (elements.clockDashboardSummary) {
      elements.clockDashboardSummary.innerHTML = `
        <div class="hours-loading-card">
          Preparando dashboard de horas trabajadas...
        </div>
      `;
    }
  };

  const renderReportTableLoading = () => {
    elements.reportTableBody.innerHTML =
      '<tr><td class="table-message" colspan="6">Cargando reporte de vacaciones...</td></tr>';
  };

  const renderAuditTableLoading = () => {
    elements.auditTableBody.innerHTML =
      '<tr><td class="table-message" colspan="5">Cargando bitacora...</td></tr>';
  };

  const renderTable = (employees, selectedId) => {
    elements.tableCounter.textContent = `${employees.length} registros`;

    if (!employees.length) {
      elements.employeeTableBody.innerHTML =
        '<tr><td class="table-message" colspan="6">No hay empleados para el filtro actual.</td></tr>';
      return;
    }

    elements.employeeTableBody.innerHTML = employees
      .map((employee) => {
        const tone = model.getStatusTone(employee.nombreEstadoEmpleado);
        const selected = Number(employee.idEmpleado) === Number(selectedId);

        return `
          <tr class="record-row${selected ? " is-active" : ""}" data-employee-id="${employee.idEmpleado}">
            <td><span class="code-chip">${escapeHtml(employee.codigoEmpleado)}</span></td>
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(employee.nombreCompleto)}</strong>
                <small>${escapeHtml(employee.correo || employee.telefono || "Sin contacto")}</small>
              </div>
            </td>
            <td>${escapeHtml(employee.cedula)}</td>
            <td>${escapeHtml(employee.nombreCargo)}</td>
            <td>${escapeHtml(employee.nombreDepartamento)}</td>
            <td><span class="status-pill ${tone}">${escapeHtml(employee.nombreEstadoEmpleado)}</span></td>
          </tr>
        `;
      })
      .join("");
  };

  const renderContractTable = (contracts, selectedId) => {
    elements.contractTableCounter.textContent = `${contracts.length} registros`;

    if (!contracts.length) {
      elements.contractTableBody.innerHTML =
        '<tr><td class="table-message" colspan="7">No hay contratos para el filtro actual.</td></tr>';
      return;
    }

    elements.contractTableBody.innerHTML = contracts
      .map((contract) => {
        const tone = model.getContractStatusTone(contract.esContratoVigente);
        const selected = Number(contract.idContrato) === Number(selectedId);
        const alertTone = contract.estaPorVencer ? "warning" : contract.esTemporal ? "accent" : "success";

        return `
          <tr class="record-row${selected ? " is-active" : ""}" data-contract-id="${contract.idContrato}">
            <td><span class="code-chip">${escapeHtml(contract.numeroContrato)}</span></td>
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(contract.nombreEmpleado)}</strong>
                <small>${escapeHtml(contract.codigoEmpleado)} - ${escapeHtml(contract.cedulaEmpleado)}</small>
              </div>
            </td>
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(contract.nombreTipoContrato)}</strong>
                ${
                  contract.etiquetaAlerta
                    ? `<small><span class="status-pill status-pill-inline is-${alertTone}">${escapeHtml(contract.etiquetaAlerta)}</span></small>`
                    : ""
                }
              </div>
            </td>
            <td>${escapeHtml(contract.nombreHorario)}</td>
            <td>${escapeHtml(model.formatShortDate(contract.fechaInicio))}</td>
            <td>${escapeHtml(model.formatMoney(contract.salarioBaseMensual, contract.moneda))}</td>
            <td>
              <span class="status-pill ${tone}">
                ${escapeHtml(contract.esContratoVigente ? "Vigente" : "Historico")}
              </span>
            </td>
          </tr>
        `;
      })
      .join("");
  };

  const renderWorkflowTable = (moduleId, moduleLabel, records, selectedId) => {
    elements.workflowPanelTitle.textContent = moduleLabel;
    elements.workflowTableCounter.textContent = `${records.length} registros`;

    if (moduleId === "solicitud_permiso") {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Empleado</th>
          <th>Tipo</th>
          <th>Inicio</th>
          <th>Fin</th>
          <th>Dias</th>
          <th>Estado</th>
        </tr>
      `;
    } else if (moduleId === "vacacion") {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Empleado</th>
          <th>Inicio</th>
          <th>Fin</th>
          <th>Dias</th>
          <th>Aprobados</th>
          <th>Estado</th>
        </tr>
      `;
    } else {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Empleado</th>
          <th>Tipo</th>
          <th>Fecha</th>
          <th>Horas</th>
          <th>Factor</th>
          <th>Estado</th>
        </tr>
      `;
    }

    if (!records.length) {
      elements.workflowTableBody.innerHTML =
        '<tr><td class="table-message" colspan="6">No hay registros para el filtro actual.</td></tr>';
      return;
    }

    elements.workflowTableBody.innerHTML = records
      .map((record) => {
        let idValue = 0;
        let body = "";

        if (moduleId === "solicitud_permiso") {
          idValue = record.idSolicitudPermiso;
          body = `
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(record.nombreEmpleado)}</strong>
                <small>${escapeHtml(record.codigoEmpleado)}</small>
              </div>
            </td>
            <td>${escapeHtml(record.nombreTipoPermiso)}</td>
            <td>${escapeHtml(model.formatShortDate(record.fechaInicio))}</td>
            <td>${escapeHtml(model.formatShortDate(record.fechaFin))}</td>
            <td>${escapeHtml(formatDecimal(record.cantidadDias))}</td>
            <td>
              <span class="status-pill ${getWorkflowTone(record.estadoPermiso)}">
                ${escapeHtml(record.estadoPermiso)}
              </span>
            </td>
          `;
        } else if (moduleId === "vacacion") {
          idValue = record.idVacacion;
          const requestedDaysLabel = `${formatDecimal(record.diasSolicitados)}${record.esMedioDia ? " (medio dia)" : ""}`;
          body = `
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(record.nombreEmpleado)}</strong>
                <small>${escapeHtml(record.codigoEmpleado)}</small>
              </div>
            </td>
            <td>${escapeHtml(model.formatShortDate(record.fechaInicio))}</td>
            <td>${escapeHtml(model.formatShortDate(record.fechaFin))}</td>
            <td>${escapeHtml(requestedDaysLabel)}</td>
            <td>${escapeHtml(record.diasAprobados === null || record.diasAprobados === undefined ? "-" : formatDecimal(record.diasAprobados))}</td>
            <td>
              <span class="status-pill ${getWorkflowTone(record.estadoVacacion)}">
                ${escapeHtml(record.estadoVacacion)}
              </span>
            </td>
          `;
        } else {
          idValue = record.idHoraExtra;
          body = `
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(record.nombreEmpleado)}</strong>
                <small>${escapeHtml(record.codigoEmpleado)}</small>
              </div>
            </td>
            <td>${escapeHtml(record.nombreTipoHoraExtra)}</td>
            <td>${escapeHtml(model.formatShortDate(record.fechaHoraExtra))}</td>
            <td>${escapeHtml(formatDecimal(record.cantidadHoras))}</td>
            <td>${escapeHtml(`${formatDecimal(record.factorPago)}x`)}</td>
            <td>
              <span class="status-pill ${getWorkflowTone(record.estadoHoraExtra)}">
                ${escapeHtml(record.estadoHoraExtra)}
              </span>
            </td>
          `;
        }

        const selected = Number(idValue) === Number(selectedId);

        return `
          <tr class="record-row${selected ? " is-active" : ""}" data-workflow-id="${idValue}">
            ${body}
          </tr>
        `;
      })
      .join("");
  };

  const renderClockDashboard = (rows, options = {}) => {
    if (!elements.clockDashboardSummary) {
      return;
    }

    const { isReport = false, dateFrom = "", dateTo = "" } = options;
    const dashboard = buildHoursDashboardData(rows);
    const rangeLabel =
      dateFrom && dateTo
        ? `${model.formatShortDate(dateFrom)} al ${model.formatShortDate(dateTo)}`
        : "Filtro actual";
    const monthlyRows = dashboard.months.length
      ? dashboard.months
          .map(
            (month) => `
              <tr>
                <td>${escapeHtml(month.label)}</td>
                <td>${escapeHtml(`${formatDecimal(month.worked)} h`)}</td>
                <td><span class="status-pill ${getHoursDiffTone(month.diff)}">${escapeHtml(formatSignedHours(month.diff))}</span></td>
                <td>${escapeHtml(String(month.days))}</td>
                <td>${escapeHtml(String(month.open))}</td>
              </tr>
            `,
          )
          .join("")
      : '<tr><td class="table-message" colspan="5">Sin informacion para el rango seleccionado.</td></tr>';

    elements.clockDashboardSummary.innerHTML = `
      <section class="hours-kpi-grid" aria-label="Indicadores de horas trabajadas">
        <article class="hours-kpi-card">
          <span>Horas trabajadas</span>
          <strong>${escapeHtml(`${formatDecimal(dashboard.totals.worked)} h`)}</strong>
          <small>${escapeHtml(rangeLabel)}</small>
        </article>
        <article class="hours-kpi-card">
          <span>Extra / menos</span>
          <strong class="${getHoursDiffTone(dashboard.totals.diff)}">${escapeHtml(formatSignedHours(dashboard.totals.diff))}</strong>
          <small>Diferencia contra jornada esperada</small>
        </article>
        <article class="hours-kpi-card">
          <span>Jornadas</span>
          <strong>${escapeHtml(String(dashboard.totals.days))}</strong>
          <small>${escapeHtml(`${dashboard.employees} colaborador(es)`)}</small>
        </article>
        <article class="hours-kpi-card">
          <span>Abiertas</span>
          <strong>${escapeHtml(String(dashboard.totals.open))}</strong>
          <small>Revisar salida pendiente</small>
        </article>
      </section>

      ${
        isReport
          ? `
            <section class="hours-report-grid">
              <article class="hours-month-card">
                <div class="panel-head">
                  <div class="panel-copy">
                    <span class="eyebrow">Resumen mensual</span>
                    <h3>Control horas trabajadas</h3>
                  </div>
                </div>
                <div class="table-wrap table-wrap-compact">
                  <table class="data-table hours-month-table">
                    <thead>
                      <tr>
                        <th>Mes</th>
                        <th>Horas trabajadas</th>
                        <th>Horas extra/menos</th>
                        <th>Jornadas</th>
                        <th>Abiertas</th>
                      </tr>
                    </thead>
                    <tbody>${monthlyRows}</tbody>
                  </table>
                </div>
              </article>

              <aside class="hours-note-card">
                <span class="eyebrow">Regla usada</span>
                <strong>Horario vigente del contrato</strong>
                <p>La diferencia se calcula contra las horas diarias del horario laboral asignado. Si el colaborador no tiene horario vigente, se usa 8 h/dia como base provisional.</p>
              </aside>
            </section>
          `
          : ""
      }
    `;
  };

  const renderClockTable = (rows, selectedIndex) => {
    elements.clockTableCounter.textContent = `${rows.length} registros`;

    if (!rows.length) {
      elements.clockTableBody.innerHTML =
        '<tr><td class="table-message" colspan="7">No hay marcaciones para el filtro actual.</td></tr>';
      return;
    }

    elements.clockTableBody.innerHTML = rows
      .map(
        (row, index) => `
          <tr class="record-row${index === selectedIndex ? " is-active" : ""}" data-clock-index="${index}">
            <td>${escapeHtml(model.formatShortDate(row.fechaOperacion))}</td>
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(row.nombreEmpleado)}</strong>
                <small>${escapeHtml(row.codigoEmpleado)} - ${escapeHtml(row.cedula)}</small>
              </div>
            </td>
            <td>${escapeHtml(row.horaEntrada || "-")}</td>
            <td>${escapeHtml(row.horaSalida || "-")}</td>
            <td>${escapeHtml(`${formatDecimal(row.horasTrabajadas)} h`)}</td>
            <td>
              <span class="status-pill ${getHoursDiffTone(row.horasExtraMenos)}">
                ${escapeHtml(formatSignedHours(row.horasExtraMenos))}
              </span>
            </td>
            <td>
              <span class="status-pill ${row.estadoJornada === "CERRADA" ? "is-success" : "is-warning"}">
                ${escapeHtml(row.estadoJornada)}
              </span>
            </td>
          </tr>
        `,
      )
      .join("");
  };

  const renderReportTable = (rows, selectedIndex) => {
    elements.reportTableCounter.textContent = `${rows.length} registros`;

    if (!rows.length) {
      elements.reportTableBody.innerHTML =
        '<tr><td class="table-message" colspan="6">No hay empleados para el filtro actual.</td></tr>';
      return;
    }

    elements.reportTableBody.innerHTML = rows
      .map(
        (row, index) => `
          <tr class="record-row${index === selectedIndex ? " is-active" : ""}" data-report-index="${index}">
            <td><span class="code-chip">${escapeHtml(row.codigoEmpleado || "-")}</span></td>
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(row.nombreEmpleado || "-")}</strong>
                <small>${escapeHtml(row.nombreCargo || "Sin cargo")}</small>
              </div>
            </td>
            <td>${escapeHtml(model.formatShortDate(row.fechaIngreso))}</td>
            <td>${escapeHtml(row.nombreDepartamento || "-")}</td>
            <td>${escapeHtml(row.nombreTipoContratoVigente || "Sin contrato vigente")}</td>
            <td>
              <span class="status-pill ${row.acumulaVacaciones ? "status-success" : "status-warning"}">
                ${escapeHtml(`${formatDecimal(row.diasDisponibles)} d`)}
              </span>
            </td>
          </tr>
        `,
      )
      .join("");
  };

  const renderStructureFilters = (branches, selectedKey = "TODOS") => {
    const items = [{ key: "TODOS", label: "Vista general", subtitle: "Sin recorte por gerencia" }, ...(branches || [])];

    elements.structureFilterRow.innerHTML = items
      .map(
        (item) => `
          <button
            class="filter-chip-button${String(item.key) === String(selectedKey) ? " is-active" : ""}"
            data-structure-branch="${escapeHtml(item.key)}"
            type="button"
          >
            <strong>${escapeHtml(item.label)}</strong>
            <small>${escapeHtml(item.subtitle || "")}</small>
          </button>
        `,
      )
      .join("");
  };

  const renderStructureSummary = (summary) => {
    const cards = summary
      ? [
          { label: "Nodos", value: summary.totalNodes, tone: "accent", detail: "Estructura formal publicada" },
          { label: "Con titular", value: summary.nodesWithTitular, tone: "success", detail: "Tarjetas con colaborador asignado" },
          { label: "Vacantes o sin titular", value: summary.vacantNodes, tone: "warning", detail: "Plazas institucionales abiertas" },
          { label: "Gerencias", value: summary.managementCount, tone: "accent", detail: "Ramas ejecutivas" },
          { label: "Jefaturas", value: summary.headquartersCount, tone: "warning", detail: "Mandos intermedios" },
          { label: "Coordinaciones", value: summary.coordinationCount, tone: "success", detail: "Seguimiento operativo" },
          { label: "Puestos", value: summary.positionCount, tone: "accent", detail: "Posiciones finales del arbol" },
          { label: "Unidades", value: summary.unitCount, tone: "success", detail: "Areas sin titular o institucionales" },
        ]
      : [];

    elements.structureSummaryGrid.innerHTML = cards
      .map(
        (card) => `
          <article class="board-summary-card structure-summary-card">
            <span class="eyebrow">${escapeHtml(card.label)}</span>
            <strong>${escapeHtml(String(card.value ?? 0))}</strong>
            <span class="module-metric ${getMetricToneClass(card.tone)}">${escapeHtml(card.detail)}</span>
          </article>
        `,
      )
      .join("");
  };

  const renderStructureTree = (nodes, selectedId = null) => {
    const countNodes = (items) =>
      (items || []).reduce((total, item) => total + 1 + countNodes(item.children || []), 0);

    const renderNodes = (items, depth = 0) =>
      (items || [])
        .map((node) => {
          const selected = Number(node.idNodoEstructura) === Number(selectedId);
          const tone =
            node.tipoNodo === "GERENCIA_GENERAL"
              ? "is-success"
              : node.tipoNodo === "GERENCIA"
                ? "is-accent"
                : node.tipoNodo === "JEFATURA"
                  ? "is-warning"
                  : node.tipoNodo === "COORDINACION"
                    ? "is-success"
                    : node.tipoNodo === "VACANTE"
                      ? "is-danger"
                      : "";
          const hasPhoto = Boolean(node.fotoPerfilUrl);
          const initials = getInitials(node.nombreEmpleadoTitular || node.nombreNodo);
          const title = node.nombreNodo || "Nodo";
          const titleMeta = node.nombreEmpleadoTitular
            ? `${node.nombreEmpleadoTitular}${node.codigoEmpleadoTitular ? ` (${node.codigoEmpleadoTitular})` : ""}`
            : node.tipoNodo === "VACANTE"
              ? "Plaza vacante"
              : "Sin titular";
          const footerParts = [node.nombreCargo, node.nombreDepartamento].filter(Boolean);

          return `
            <div class="structure-node depth-${depth}">
              <button
                class="structure-node-card${selected ? " is-active" : ""}"
                data-structure-id="${node.idNodoEstructura}"
                type="button"
              >
                <div class="structure-node-top">
                  <span class="code-chip">${escapeHtml(node.codigoNodo)}</span>
                  <span class="status-pill status-pill-inline ${tone}">${escapeHtml(node.tipoNodoLabel)}</span>
                </div>

                <div class="structure-card-body">
                  <div class="structure-avatar${hasPhoto ? " has-photo" : ""}">
                    ${
                      hasPhoto
                        ? `<img src="${escapeHtml(node.fotoPerfilUrl)}" alt="${escapeHtml(titleMeta)}" />`
                        : `<span>${escapeHtml(initials)}</span>`
                    }
                  </div>

                  <div class="structure-card-copy">
                    <strong>${escapeHtml(title)}</strong>
                    <span>${escapeHtml(titleMeta)}</span>
                    <small>${escapeHtml(footerParts.length ? footerParts.join(" · ") : "Sin cargo o departamento asociado")}</small>
                  </div>
                </div>

                <div class="structure-node-bottom">
                  <small>${escapeHtml(`${node.directChildCount || 0} hijo(s) directos · ${1 + Number(node.totalDescendantCount || 0)} en la rama`)}</small>
                </div>
              </button>
              ${
                Array.isArray(node.children) && node.children.length
                  ? `<div class="structure-children">${renderNodes(node.children, depth + 1)}</div>`
                  : ""
              }
            </div>
          `;
        })
        .join("");

    const total = countNodes(nodes || []);
    elements.structureTableCounter.textContent = `${total} nodo${total === 1 ? "" : "s"}`;

    if (!total) {
      elements.structureTreeBody.innerHTML = `
        <div class="detail-empty">
          <p>No hay nodos de estructura para el filtro actual.</p>
        </div>
      `;
      return;
    }

    elements.structureTreeBody.innerHTML = renderNodes(nodes || []);
  };

  const renderStructureDetail = (node) => {
    if (!node) {
      elements.structureDetailTitle.textContent = "Sin seleccion";
      elements.structureDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un nodo del organigrama formal.</p>
        </div>
      `;
      return;
    }

    elements.structureDetailTitle.textContent = node.nombreNodo;

    const breadcrumb = Array.isArray(node.breadcrumb) && node.breadcrumb.length
      ? node.breadcrumb.join(" > ")
      : node.nombreNodo;
    const rows = [
      ["Codigo", node.codigoNodo],
      ["Tipo", node.tipoNodoLabel],
      ["Titular", node.nombreEmpleadoTitular || "Sin titular"],
      ["Codigo titular", node.codigoEmpleadoTitular || "-"],
      ["Cargo asociado", node.nombreCargo || "-"],
      ["Departamento asociado", node.nombreDepartamento || "-"],
      ["Nodo padre", node.nombreNodoPadre || "Sin padre"],
      ["Ruta", breadcrumb || "-"],
      ["Hijos directos", String(node.directChildCount || 0)],
      ["Total en rama", String(1 + Number(node.totalDescendantCount || 0))],
      ["Estado", node.activo ? "Activo" : "Inactivo"],
      ["Observacion", node.observacion || "-"],
    ];

    elements.structureDetailBody.innerHTML = `
      <div class="detail-stack">
        <article class="detail-header detail-structure-hero">
          <div class="detail-title-row">
            <span class="status-pill status-pill-inline ${node.activo ? "is-success" : "is-warning"}">${escapeHtml(node.tipoNodoLabel)}</span>
            <strong>${escapeHtml(node.nombreNodo)}</strong>
          </div>
          <p class="detail-note">
            ${
              node.nombreEmpleadoTitular
                ? `${escapeHtml(node.nombreEmpleadoTitular)} ocupa actualmente este nodo formal.`
                : "Este nodo formal no tiene titular asignado por ahora."
            }
          </p>
        </article>

        <article class="detail-grid-card">
          ${rows
            .map(
              ([label, value]) => `
                <div class="detail-row">
                  <span>${escapeHtml(label)}</span>
                  <strong>${escapeHtml(value || "-")}</strong>
                </div>
              `,
            )
            .join("")}
        </article>
      </div>
    `;
  };

  const renderAuditTable = (rows, selectedIndex) => {
    elements.auditTableCounter.textContent = `${rows.length} registros`;

    if (!rows.length) {
      elements.auditTableBody.innerHTML =
        '<tr><td class="table-message" colspan="5">No hay movimientos de bitacora para el filtro actual.</td></tr>';
      return;
    }

    elements.auditTableBody.innerHTML = rows
      .map(
        (row, index) => `
          <tr class="record-row${index === selectedIndex ? " is-active" : ""}" data-audit-index="${index}">
            <td>${escapeHtml(formatDateTime(row.occurredAt))}</td>
            <td>${escapeHtml(row.process || "-")}</td>
            <td>${escapeHtml(row.eventType || "-")}</td>
            <td>${escapeHtml(row.reference || "-")}</td>
            <td>${escapeHtml(row.user || "sistema")}</td>
          </tr>
        `,
      )
      .join("");
  };

  const renderDetail = (employee) => {
    if (!employee) {
      elements.detailTitle.textContent = "Sin seleccion";
      elements.detailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un registro.</p>
        </div>
      `;
      return;
    }

    elements.detailTitle.textContent = employee.nombreCompleto;

    const tone = model.getStatusTone(employee.nombreEstadoEmpleado);
    const summary = employee.resumenLaboral || null;
    const rows = [
      ["Codigo", employee.codigoEmpleado],
      ["Usuario", employee.usuarioSistema || "-"],
      ["Cedula", employee.cedula],
      ["Area", employee.nombreDepartamento],
      ["Cargo", employee.nombreCargo],
      [
        "Jefe inmediato",
        employee.nombreSupervisorEmpleado
          ? `${employee.nombreSupervisorEmpleado}${employee.codigoSupervisorEmpleado ? ` (${employee.codigoSupervisorEmpleado})` : ""}`
          : "-",
      ],
      ["Ingreso", model.formatShortDate(employee.fechaIngreso)],
      ["Nacimiento", model.formatShortDate(employee.fechaNacimiento)],
      ["Sexo", formatSex(employee.sexo)],
      ["Estado civil", employee.estadoCivil || "-"],
      ["Telefono", employee.telefono || "-"],
      ["Correo", employee.correo || "-"],
      ["INSS", employee.inss || "-"],
      ["Banco", employee.nombreBanco || "-"],
      ["Cuenta", employee.numeroCuentaBancaria || "-"],
      ["Direccion", employee.direccion || "-"],
    ];

    const summaryCards = summary
      ? [
          {
            label: "Contrato",
            value: summary.contratoVigenteNumero || "Sin contrato vigente",
            detail:
              summary.contratoVigenteNumero && summary.contratoVigenteTipo
                ? `${summary.contratoVigenteTipo} Â· ${
                    summary.contratoVigenteHasta ? `vence ${model.formatShortDate(summary.contratoVigenteHasta)}` : "sin fecha fin"
                  }`
                : "Revisa la vigencia contractual del colaborador.",
            tone: summary.tieneContratoVigente ? "success" : "warning",
          },
          {
            label: "Pendientes",
            value: String((summary.vacacionesPendientes || 0) + (summary.horasExtraPendientes || 0)),
            detail: `${summary.vacacionesPendientes || 0} vacaciones / ${summary.horasExtraPendientes || 0} horas extra`,
            tone:
              (summary.vacacionesPendientes || 0) + (summary.horasExtraPendientes || 0) >
              0
                ? "warning"
                : "success",
          },
          {
            label: "Expedientes",
            value: String(summary.totalExpedientes || 0),
            detail: `${summary.expedientesVencidos || 0} vencidos / ${summary.expedientesPorVencer || 0} por vencer`,
            tone:
              (summary.expedientesVencidos || 0) > 0
                ? "danger"
                : (summary.expedientesPorVencer || 0) > 0
                  ? "warning"
                  : "success",
          },
          {
            label: "Subordinados",
            value: String(summary.totalSubordinados || 0),
            detail:
              Number(summary.totalSubordinados || 0) > 0
                ? "Colaboradores que le reportan directamente."
                : "No tiene colaboradores asignados.",
            tone: Number(summary.totalSubordinados || 0) > 0 ? "accent" : "warning",
          },
          {
            label: "Reloj",
            value: summary.ultimaMarcacionTipo || "Sin marcaciones",
            detail: summary.ultimaMarcacionFechaHora
              ? `${formatDateTime(summary.ultimaMarcacionFechaHora)} Â· ${summary.marcacionesHoy || 0} marcaciones hoy`
              : "Aun no registra marcaciones.",
            tone: summary.ultimaMarcacionTipo === "ENTRADA" ? "warning" : "accent",
          },
          {
            label: "Vacaciones",
            value: `${formatDecimal(summary.diasVacacionesDisponibles || 0)} d`,
            detail: `${formatDecimal(summary.diasVacacionesAcumulados || 0)} acumulados / ${formatDecimal(
              summary.diasVacacionesTomados || 0,
            )} consumidos`,
            tone: Number(summary.diasVacacionesDisponibles || 0) > 0 ? "success" : "warning",
          },
        ]
      : [];

    const summarySalary =
      summary?.salarioBaseMensual && summary?.monedaContrato
        ? model.formatMoney(summary.salarioBaseMensual, summary.monedaContrato)
        : "";
    const hasPhoto = Boolean(employee.fotoPerfilUrl);
    const initials = getInitials(employee.nombreCompleto);
    const isRetired = String(employee.nombreEstadoEmpleado || "")
      .trim()
      .toUpperCase()
      .includes("RETIRADO");

    elements.detailBody.innerHTML = `
      <div class="employee-profile-banner">
        <div class="employee-profile-avatar">
          ${
            hasPhoto
              ? `<img src="${escapeHtml(employee.fotoPerfilUrl)}" alt="Foto de ${escapeHtml(employee.nombreCompleto)}" />`
              : `<span>${escapeHtml(initials)}</span>`
          }
        </div>

        <div class="employee-profile-copy">
          <strong>${escapeHtml(employee.nombreCompleto)}</strong>
          <span>${escapeHtml(employee.nombreCargo || "Sin cargo asignado")}</span>
          <small>${
            employee.nombreSupervisorEmpleado
              ? `Reporta a ${escapeHtml(employee.nombreSupervisorEmpleado)}`
              : "Sin jefe inmediato asignado"
          }</small>
        </div>

        <div class="employee-profile-actions">
          <input
            type="file"
            id="employeeDetailPhotoInput"
            accept=".png,.jpg,.jpeg,.webp"
            data-employee-photo-input="${escapeHtml(String(employee.idEmpleado))}"
            hidden
          />
          <button class="ghost-button" data-employee-action="upload-photo" data-employee-id="${escapeHtml(
            String(employee.idEmpleado),
          )}" type="button">
            Subir foto
          </button>
        </div>
      </div>

      <div class="detail-header">
        <div class="detail-row">
          <span>Estado</span>
          <strong class="${tone}">${escapeHtml(employee.nombreEstadoEmpleado)}</strong>
        </div>
        <div class="detail-row">
          <span>Ultima actualizacion</span>
          <strong>${escapeHtml(formatDateTime(employee.fechaRegistro || employee.fechaModificacion || employee.fechaIngreso))}</strong>
        </div>
        ${
          summarySalary
            ? `
              <div class="detail-row">
                <span>Salario base vigente</span>
                <strong>${escapeHtml(summarySalary)}</strong>
              </div>
            `
            : ""
        }
      </div>

      ${
        summaryCards.length
          ? `
            <div class="employee-summary-grid">
              ${summaryCards
                .map(
                  (card) => `
                    <article class="employee-summary-card">
                      <span class="eyebrow">${escapeHtml(card.label)}</span>
                      <strong>${escapeHtml(card.value || "-")}</strong>
                      <span class="module-metric ${getMetricToneClass(card.tone)}">${escapeHtml(card.detail || "")}</span>
                    </article>
                  `,
                )
                .join("")}
            </div>
          `
          : ""
      }

      ${
        summary
          ? `
            <div class="detail-actions-row">
              ${
                isRetired
                  ? `
                    <span class="status-pill is-warning">
                      Empleado retirado: ya no genera nuevas vacaciones ni horas extra.
                    </span>
                  `
                  : `
                    <button class="ghost-button" data-employee-action="new-vacation" data-employee-id="${escapeHtml(
                      String(employee.idEmpleado),
                    )}" type="button">
                      Registrar vacacion
                    </button>
                  `
              }
              <span class="detail-note">
                Disponible hoy: ${escapeHtml(formatDecimal(summary.diasVacacionesDisponibles || 0))} dia(s) de vacaciones.
              </span>
            </div>
          `
          : ""
      }

      <div class="detail-grid">
        ${rows
          .map(
            ([label, value]) => `
              <div class="detail-row">
                <span>${escapeHtml(label)}</span>
                <strong>${escapeHtml(value || "-")}</strong>
              </div>
            `,
          )
          .join("")}
      </div>
    `;
  };

  const renderContractDetail = (contract) => {
    if (!contract) {
      elements.contractDetailTitle.textContent = "Sin seleccion";
      elements.contractDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un contrato para ver su detalle.</p>
        </div>
      `;
      return;
    }

    elements.contractDetailTitle.textContent = contract.numeroContrato;

    const tone = model.getContractStatusTone(contract.esContratoVigente);
    const alertTone = contract.estaPorVencer ? "warning" : contract.esTemporal ? "accent" : "success";
    const rows = [
      ["Empleado", `${contract.codigoEmpleado} - ${contract.nombreEmpleado}`],
      ["Cedula", contract.cedulaEmpleado],
      ["Departamento", contract.nombreDepartamento],
      ["Cargo", contract.nombreCargo],
      ["Ingreso empleado", model.formatShortDate(contract.fechaIngresoEmpleado)],
      ["Tipo contrato", contract.nombreTipoContrato],
      ["Horario", contract.nombreHorario],
      ["Codigo horario", contract.codigoHorario],
      ["Horas semanales", `${formatDecimal(contract.horasSemanales)} h`],
      ["Horas diarias", `${formatDecimal(contract.horasDiarias)} h`],
      ["Inicio", model.formatShortDate(contract.fechaInicio)],
      ["Fin", contract.fechaFin ? model.formatShortDate(contract.fechaFin) : "-"],
      ["Salario base", model.formatMoney(contract.salarioBaseMensual, contract.moneda)],
      ["Moneda", contract.moneda],
      ["Observacion", contract.observacion || "-"],
    ];

    elements.contractDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="detail-row">
          <span>Estado</span>
          <strong class="${tone}">${escapeHtml(contract.esContratoVigente ? "Vigente" : "Historico")}</strong>
        </div>
        ${
          contract.etiquetaAlerta
            ? `
              <div class="detail-row">
                <span>Alerta</span>
                <strong><span class="status-pill status-pill-inline is-${alertTone}">${escapeHtml(contract.etiquetaAlerta)}</span></strong>
              </div>
            `
            : ""
        }
        <div class="detail-row">
          <span>Registrado</span>
          <strong>${escapeHtml(formatDateTime(contract.fechaRegistro))}</strong>
        </div>
      </div>

      <div class="detail-grid">
        ${rows
          .map(
            ([label, value]) => `
              <div class="detail-row">
                <span>${escapeHtml(label)}</span>
                <strong>${escapeHtml(value || "-")}</strong>
              </div>
            `,
          )
          .join("")}
      </div>
    `;
  };

  const renderWorkflowDetail = (moduleId, record) => {
    if (!record) {
      elements.workflowDetailTitle.textContent = "Sin seleccion";
      elements.workflowDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un registro para ver su detalle.</p>
        </div>
      `;
      return;
    }

    if (moduleId === "solicitud_permiso") {
      elements.workflowDetailTitle.textContent = `${record.codigoEmpleado} - Vacacion`;
      elements.workflowDetailBody.innerHTML = `
        <div class="detail-header">
          <div class="detail-row">
            <span>Empleado</span>
            <strong>${escapeHtml(record.nombreEmpleado)}</strong>
          </div>
          <div class="detail-row">
            <span>Estado</span>
            <strong class="${getWorkflowTone(record.estadoPermiso)}">${escapeHtml(record.estadoPermiso)}</strong>
          </div>
        </div>

        <div class="detail-grid">
          <div class="detail-row"><span>Codigo</span><strong>${escapeHtml(record.codigoEmpleado)}</strong></div>
          <div class="detail-row"><span>Modalidad</span><strong>${escapeHtml(record.nombreTipoPermiso)}</strong></div>
          <div class="detail-row"><span>Afecta salario</span><strong>${escapeHtml(record.afectaSalario ? "Si" : "No")}</strong></div>
          <div class="detail-row"><span>Inicio</span><strong>${escapeHtml(model.formatShortDate(record.fechaInicio))}</strong></div>
          <div class="detail-row"><span>Fin</span><strong>${escapeHtml(model.formatShortDate(record.fechaFin))}</strong></div>
          <div class="detail-row"><span>Dias</span><strong>${escapeHtml(formatDecimal(record.cantidadDias))}</strong></div>
          <div class="detail-row"><span>Medio dia</span><strong>${escapeHtml(record.esMedioDia ? "Si" : "No")}</strong></div>
          <div class="detail-row"><span>Jornada</span><strong>${escapeHtml(record.jornadaMedioDia || "-")}</strong></div>
          <div class="detail-row"><span>Vacaciones disponibles</span><strong>${escapeHtml(formatDecimal(record.diasVacacionesDisponibles))}</strong></div>
          <div class="detail-row"><span>Solicitado por</span><strong>${escapeHtml(record.usuarioSolicita)}</strong></div>
          <div class="detail-row"><span>Aprobado por</span><strong>${escapeHtml(record.usuarioAprueba || "-")}</strong></div>
          <div class="detail-row"><span>Fecha solicitud</span><strong>${escapeHtml(model.formatShortDate(record.fechaSolicitud))}</strong></div>
          <div class="detail-row"><span>Fecha aprobacion</span><strong>${escapeHtml(record.fechaAprobacion ? formatDateTime(record.fechaAprobacion) : "-")}</strong></div>
          <div class="detail-row"><span>Observacion solicitud</span><strong>${escapeHtml(record.observacion || "-")}</strong></div>
          <div class="detail-row"><span>Observacion resolucion</span><strong>${escapeHtml(record.observacionResolucion || "-")}</strong></div>
        </div>
      `;
      return;
    }

    if (moduleId === "vacacion") {
      elements.workflowDetailTitle.textContent = `${record.codigoEmpleado} - Vacacion`;
      elements.workflowDetailBody.innerHTML = `
        <div class="detail-header">
          <div class="detail-row">
            <span>Empleado</span>
            <strong>${escapeHtml(record.nombreEmpleado)}</strong>
          </div>
          <div class="detail-row">
            <span>Estado</span>
            <strong class="${getWorkflowTone(record.estadoVacacion)}">${escapeHtml(record.estadoVacacion)}</strong>
          </div>
        </div>

        <div class="detail-grid">
          <div class="detail-row"><span>Codigo</span><strong>${escapeHtml(record.codigoEmpleado)}</strong></div>
          <div class="detail-row"><span>Inicio</span><strong>${escapeHtml(model.formatShortDate(record.fechaInicio))}</strong></div>
          <div class="detail-row"><span>Fin</span><strong>${escapeHtml(model.formatShortDate(record.fechaFin))}</strong></div>
          <div class="detail-row"><span>Dias solicitados</span><strong>${escapeHtml(`${formatDecimal(record.diasSolicitados)}${record.esMedioDia ? " (medio dia)" : ""}`)}</strong></div>
          <div class="detail-row"><span>Dias aprobados</span><strong>${escapeHtml(record.diasAprobados === null || record.diasAprobados === undefined ? "-" : formatDecimal(record.diasAprobados))}</strong></div>
          <div class="detail-row"><span>Medio dia</span><strong>${escapeHtml(record.esMedioDia ? "Si" : "No")}</strong></div>
          <div class="detail-row"><span>Jornada</span><strong>${escapeHtml(record.jornadaMedioDia || "-")}</strong></div>
          <div class="detail-row"><span>Vacaciones disponibles</span><strong>${escapeHtml(formatDecimal(record.diasVacacionesDisponibles))}</strong></div>
          <div class="detail-row"><span>Dias acumulados</span><strong>${escapeHtml(formatDecimal(record.diasVacacionesAcumulados))}</strong></div>
          <div class="detail-row"><span>Pagada en nomina</span><strong>${escapeHtml(record.pagadaEnNomina ? "Si" : "No")}</strong></div>
          <div class="detail-row"><span>Solicitado por</span><strong>${escapeHtml(record.usuarioSolicita)}</strong></div>
          <div class="detail-row"><span>Aprobado por</span><strong>${escapeHtml(record.usuarioAprueba || "-")}</strong></div>
          <div class="detail-row"><span>Observacion solicitud</span><strong>${escapeHtml(record.observacionSolicitud || "-")}</strong></div>
          <div class="detail-row"><span>Observacion aprobacion</span><strong>${escapeHtml(record.observacionAprobacion || "-")}</strong></div>
          <div class="detail-row"><span>Fecha aprobacion</span><strong>${escapeHtml(record.fechaAprobacion ? formatDateTime(record.fechaAprobacion) : "-")}</strong></div>
        </div>
      `;
      return;
    }

    elements.workflowDetailTitle.textContent = `${record.codigoEmpleado} - Hora extra`;
    elements.workflowDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="detail-row">
          <span>Empleado</span>
          <strong>${escapeHtml(record.nombreEmpleado)}</strong>
        </div>
        <div class="detail-row">
          <span>Estado</span>
          <strong class="${getWorkflowTone(record.estadoHoraExtra)}">${escapeHtml(record.estadoHoraExtra)}</strong>
        </div>
      </div>

      <div class="detail-grid">
        <div class="detail-row"><span>Codigo</span><strong>${escapeHtml(record.codigoEmpleado)}</strong></div>
        <div class="detail-row"><span>Tipo</span><strong>${escapeHtml(record.nombreTipoHoraExtra)}</strong></div>
        <div class="detail-row"><span>Factor</span><strong>${escapeHtml(`${formatDecimal(record.factorPago)}x`)}</strong></div>
        <div class="detail-row"><span>Fecha</span><strong>${escapeHtml(model.formatShortDate(record.fechaHoraExtra))}</strong></div>
        <div class="detail-row"><span>Horas</span><strong>${escapeHtml(formatDecimal(record.cantidadHoras))}</strong></div>
        <div class="detail-row"><span>Pagada en nomina</span><strong>${escapeHtml(record.pagadaEnNomina ? "Si" : "No")}</strong></div>
        <div class="detail-row"><span>Registrado por</span><strong>${escapeHtml(record.usuarioRegistra)}</strong></div>
        <div class="detail-row"><span>Aprobado por</span><strong>${escapeHtml(record.usuarioAprueba || "-")}</strong></div>
        <div class="detail-row"><span>Fecha aprobacion</span><strong>${escapeHtml(record.fechaAprobacion ? formatDateTime(record.fechaAprobacion) : "-")}</strong></div>
        <div class="detail-row"><span>Observacion</span><strong>${escapeHtml(record.observacion || "-")}</strong></div>
      </div>
    `;
  };

  const renderCatalogTable = (moduleId, moduleLabel, records, selectedId) => {
    elements.workflowPanelTitle.textContent = moduleLabel;
    elements.workflowTableCounter.textContent = `${records.length} registros`;

    if (moduleId === "cargo") {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Codigo</th>
          <th>Nombre</th>
          <th>Departamento</th>
          <th>Nivel</th>
          <th>Estado</th>
        </tr>
      `;
    } else if (moduleId === "horario_laboral") {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Codigo</th>
          <th>Nombre</th>
          <th>Semanal</th>
          <th>Diaria</th>
          <th>Estado</th>
        </tr>
      `;
    } else if (moduleId === "tipo_permiso") {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Codigo</th>
          <th>Nombre</th>
          <th>Afecta salario</th>
          <th>Estado</th>
        </tr>
      `;
    } else if (moduleId === "tipo_hora_extra") {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Codigo</th>
          <th>Nombre</th>
          <th>Factor</th>
          <th>Estado</th>
        </tr>
      `;
    } else {
      elements.workflowTableHead.innerHTML = `
        <tr>
          <th>Codigo</th>
          <th>Nombre</th>
          <th>Descripcion</th>
          <th>Estado</th>
        </tr>
      `;
    }

    if (!records.length) {
      const colspan = ["cargo", "horario_laboral"].includes(moduleId) ? 5 : moduleId === "tipo_permiso" || moduleId === "tipo_hora_extra" ? 4 : 4;
      elements.workflowTableBody.innerHTML =
        `<tr><td class="table-message" colspan="${colspan}">No hay registros para el filtro actual.</td></tr>`;
      return;
    }

    elements.workflowTableBody.innerHTML = records
      .map((record) => {
        const selected = Number(record.idCatalogo) === Number(selectedId);
        const tone = getCatalogTone(record.activo);

        let cells = "";
        if (moduleId === "cargo") {
          cells = `
            <td><span class="code-chip">${escapeHtml(record.codigo)}</span></td>
            <td>${escapeHtml(record.nombre)}</td>
            <td>${escapeHtml(record.relatedName || "-")}</td>
            <td>${escapeHtml(String(record.integerValue1 ?? "-"))}</td>
            <td><span class="status-pill ${tone}">${escapeHtml(record.activo ? "Activo" : "Inactivo")}</span></td>
          `;
        } else if (moduleId === "horario_laboral") {
          cells = `
            <td><span class="code-chip">${escapeHtml(record.codigo)}</span></td>
            <td>${escapeHtml(record.nombre)}</td>
            <td>${escapeHtml(`${formatDecimal(record.numberValue1)} h`)}</td>
            <td>${escapeHtml(`${formatDecimal(record.numberValue2)} h`)}</td>
            <td><span class="status-pill ${tone}">${escapeHtml(record.activo ? "Activo" : "Inactivo")}</span></td>
          `;
        } else if (moduleId === "tipo_permiso") {
          cells = `
            <td><span class="code-chip">${escapeHtml(record.codigo)}</span></td>
            <td>${escapeHtml(record.nombre)}</td>
            <td>${escapeHtml(record.flagValue1 ? "Si" : "No")}</td>
            <td><span class="status-pill ${tone}">${escapeHtml(record.activo ? "Activo" : "Inactivo")}</span></td>
          `;
        } else if (moduleId === "tipo_hora_extra") {
          cells = `
            <td><span class="code-chip">${escapeHtml(record.codigo)}</span></td>
            <td>${escapeHtml(record.nombre)}</td>
            <td>${escapeHtml(`${formatDecimal(record.numberValue1)}x`)}</td>
            <td><span class="status-pill ${tone}">${escapeHtml(record.activo ? "Activo" : "Inactivo")}</span></td>
          `;
        } else {
          cells = `
            <td><span class="code-chip">${escapeHtml(record.codigo)}</span></td>
            <td>${escapeHtml(record.nombre)}</td>
            <td>${escapeHtml(record.descripcion || "-")}</td>
            <td><span class="status-pill ${tone}">${escapeHtml(record.activo ? "Activo" : "Inactivo")}</span></td>
          `;
        }

        return `
          <tr class="record-row${selected ? " is-active" : ""}" data-workflow-id="${record.idCatalogo}">
            ${cells}
          </tr>
        `;
      })
      .join("");
  };

  const renderCatalogDetail = (moduleId, record) => {
    const config = model.getCatalogModuleConfig(moduleId);
    if (!record || !config) {
      elements.workflowDetailTitle.textContent = "Sin seleccion";
      elements.workflowDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un registro.</p>
        </div>
      `;
      return;
    }

    elements.workflowDetailTitle.textContent = record.nombre;
    const rows = [
      ["Codigo", record.codigo],
      [config.nameLabel || "Nombre", record.nombre],
      ...(config.usesDescription ? [[config.descriptionLabel || "Descripcion", record.descripcion || "-"]] : []),
      ...(config.usesRelatedId ? [[config.relatedLabel || "Relacionado", record.relatedName || "-"]] : []),
      ...(config.usesIntegerValue1 ? [[config.integerLabel || "Nivel", record.integerValue1 ?? "-"]] : []),
      ...(config.usesNumberValue1 ? [[config.number1Label || "Valor", moduleId === "tipo_hora_extra" ? `${formatDecimal(record.numberValue1)}x` : `${formatDecimal(record.numberValue1)} h`]] : []),
      ...(config.usesNumberValue2 ? [[config.number2Label || "Valor 2", `${formatDecimal(record.numberValue2)} h`]] : []),
      ...(config.usesFlagValue1 ? [[config.flagLabel || "Bandera", record.flagValue1 ? "Si" : "No"]] : []),
      ["Estado", record.activo ? "Activo" : "Inactivo"],
      ["Registro", record.fechaRegistro ? formatDateTime(record.fechaRegistro) : "-"],
    ];

    elements.workflowDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="detail-row">
          <span>Catalogo</span>
          <strong>${escapeHtml(config.noun)}</strong>
        </div>
        <div class="detail-row">
          <span>Estado</span>
          <strong class="${getCatalogTone(record.activo)}">${escapeHtml(record.activo ? "Activo" : "Inactivo")}</strong>
        </div>
      </div>

      <div class="detail-grid">
        ${rows
          .map(
            ([label, value]) => `
              <div class="detail-row">
                <span>${escapeHtml(label)}</span>
                <strong>${escapeHtml(value ?? "-")}</strong>
              </div>
            `,
          )
          .join("")}
      </div>
    `;
  };

  const renderActionTable = (records, selectedId) => {
    elements.workflowPanelTitle.textContent = "Acciones de personal";
    elements.workflowTableCounter.textContent = `${records.length} registros`;
    elements.workflowTableHead.innerHTML = `
      <tr>
        <th>Empleado</th>
        <th>Movimiento</th>
        <th>Cambio propuesto</th>
        <th>Fecha</th>
        <th>Aplicado</th>
      </tr>
    `;

    if (!records.length) {
      elements.workflowTableBody.innerHTML =
        '<tr><td class="table-message" colspan="5">No hay registros para el filtro actual.</td></tr>';
      return;
    }

    elements.workflowTableBody.innerHTML = records
      .map((record) => {
        const selected = Number(record.idAccionPersonal) === Number(selectedId);
        const changeSummary =
          record.tipoAccion === "PRORROGA CONTRATO"
            ? `Hasta ${record.nuevaFechaFinContrato ? model.formatShortDate(record.nuevaFechaFinContrato) : "-"}`
            : record.nombreCargoNuevo
              ? `${record.nombreCargo} -> ${record.nombreCargoNuevo}`
              : record.nuevoSalarioBaseMensual
                ? `Nuevo salario ${formatActionMoney(record.nuevoSalarioBaseMensual, record.monedaSalario || "NIO")}`
                : record.descripcionAccion;

        return `
          <tr class="record-row${selected ? " is-active" : ""}" data-workflow-id="${record.idAccionPersonal}">
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(record.nombreEmpleado)}</strong>
                <small>${escapeHtml(record.codigoEmpleado)} · ${escapeHtml(record.nombreCargo)}</small>
              </div>
            </td>
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(record.tipoAccion)}</strong>
                <small>${escapeHtml(record.jerarquiaActual || "Sin jerarquia")}</small>
              </div>
            </td>
            <td>${escapeHtml(changeSummary || "-")}</td>
            <td>${escapeHtml(model.formatShortDate(record.fechaAccion))}</td>
            <td><span class="status-pill ${record.aplicarCambioOperativo ? "is-success" : "is-warning"}">${escapeHtml(record.aplicarCambioOperativo ? "Aplicado" : "Solo memo")}</span></td>
          </tr>
        `;
      })
      .join("");
  };

  const renderActionDetail = (record) => {
    if (!record) {
      elements.workflowDetailTitle.textContent = "Sin seleccion";
      elements.workflowDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un registro.</p>
        </div>
      `;
      return;
    }

    elements.workflowDetailTitle.textContent = `${record.codigoEmpleado} - ${record.tipoAccion}`;
    elements.workflowDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="cell-stack">
          <strong>${escapeHtml(record.nombreEmpleado)}</strong>
          <small>${escapeHtml(record.nombreCargo)} / ${escapeHtml(record.nombreDepartamento)}</small>
        </div>
        <div class="detail-row">
          <span>Fecha</span>
          <strong>${escapeHtml(model.formatShortDate(record.fechaAccion))}</strong>
        </div>
      </div>

      <div class="detail-grid">
        <div class="detail-row"><span>Codigo</span><strong>${escapeHtml(record.codigoEmpleado)}</strong></div>
        <div class="detail-row"><span>Cedula</span><strong>${escapeHtml(record.cedula)}</strong></div>
        <div class="detail-row"><span>Tipo de accion</span><strong>${escapeHtml(record.tipoAccion)}</strong></div>
        <div class="detail-row"><span>Jerarquia actual</span><strong>${escapeHtml(record.jerarquiaActual || "-")}</strong></div>
        <div class="detail-row"><span>Jerarquia nueva</span><strong>${escapeHtml(record.jerarquiaNueva || "-")}</strong></div>
        <div class="detail-row"><span>Puesto nuevo</span><strong>${escapeHtml(record.nombreCargoNuevo || "-")}</strong></div>
        <div class="detail-row"><span>Area nueva</span><strong>${escapeHtml(record.nombreDepartamentoNuevo || "-")}</strong></div>
        <div class="detail-row"><span>Salario actual</span><strong>${escapeHtml(record.salarioActual !== null && record.salarioActual !== undefined ? formatActionMoney(record.salarioActual, record.monedaSalario || "NIO") : "-")}</strong></div>
        <div class="detail-row"><span>Salario nuevo</span><strong>${escapeHtml(record.nuevoSalarioBaseMensual !== null && record.nuevoSalarioBaseMensual !== undefined ? formatActionMoney(record.nuevoSalarioBaseMensual, record.monedaSalario || "NIO") : "-")}</strong></div>
        <div class="detail-row"><span>Contrato vigente</span><strong>${escapeHtml(record.currentContractNumber || "-")}</strong></div>
        <div class="detail-row"><span>Vigencia actual</span><strong>${escapeHtml(record.fechaFinContratoActual ? model.formatShortDate(record.fechaFinContratoActual) : "-")}</strong></div>
        <div class="detail-row"><span>Nueva vigencia</span><strong>${escapeHtml(record.nuevaFechaFinContrato ? model.formatShortDate(record.nuevaFechaFinContrato) : "-")}</strong></div>
        <div class="detail-row"><span>Aplicacion operativa</span><strong>${escapeHtml(record.aplicarCambioOperativo ? "Si" : "No")}</strong></div>
        <div class="detail-row"><span>Registrado por</span><strong>${escapeHtml(record.usuarioRegistro)}</strong></div>
        <div class="detail-row"><span>Fecha registro</span><strong>${escapeHtml(formatDateTime(record.fechaRegistro))}</strong></div>
        <div class="detail-row"><span>Descripcion</span><strong>${escapeHtml(record.descripcionAccion)}</strong></div>
      </div>

      <div class="detail-grid action-memo-card">
        <div class="detail-row">
          <span>Memorandum sugerido</span>
          <strong>Vista previa</strong>
        </div>
        <p class="detail-note">${escapeHtml(record.memorandumTexto || record.descripcionAccion || "-")}</p>
      </div>
    `;
  };

  const renderDocumentTable = (records, selectedId) => {
    elements.workflowPanelTitle.textContent = "Expedientes";
    elements.workflowTableCounter.textContent = `${records.length} registros`;
    elements.workflowTableHead.innerHTML = `
      <tr>
        <th>Empleado</th>
        <th>Tipo</th>
        <th>Documento</th>
        <th>Vencimiento</th>
        <th>Estado</th>
      </tr>
    `;

    if (!records.length) {
      elements.workflowTableBody.innerHTML =
        '<tr><td class="table-message" colspan="5">No hay registros para el filtro actual.</td></tr>';
      return;
    }

    elements.workflowTableBody.innerHTML = records
      .map((record) => {
        const selected = Number(record.idExpedienteDocumento) === Number(selectedId);

        return `
          <tr class="record-row${selected ? " is-active" : ""}" data-workflow-id="${record.idExpedienteDocumento}">
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(record.nombreEmpleado)}</strong>
                <small>${escapeHtml(record.codigoEmpleado)}</small>
              </div>
            </td>
            <td>${escapeHtml(record.tipoDocumento)}</td>
            <td>${escapeHtml(record.nombreArchivo || "Sin archivo")}</td>
            <td>${escapeHtml(record.fechaVencimiento ? model.formatShortDate(record.fechaVencimiento) : "-")}</td>
            <td><span class="status-pill ${getDocumentTone(record.estadoDocumento)}">${escapeHtml(record.estadoDocumento)}</span></td>
          </tr>
        `;
      })
      .join("");
  };

  const renderDocumentDetail = (record) => {
    if (!record) {
      elements.workflowDetailTitle.textContent = "Sin seleccion";
      elements.workflowDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un expediente.</p>
        </div>
      `;
      return;
    }

    elements.workflowDetailTitle.textContent = `${record.codigoEmpleado} - ${record.tipoDocumento}`;
    elements.workflowDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="cell-stack">
          <strong>${escapeHtml(record.nombreEmpleado)}</strong>
          <small>${escapeHtml(record.nombreCargo)} / ${escapeHtml(record.nombreDepartamento)}</small>
        </div>
        <div class="detail-row">
          <span>Estado</span>
          <strong class="${getDocumentTone(record.estadoDocumento)}">${escapeHtml(record.estadoDocumento)}</strong>
        </div>
      </div>

      <div class="detail-grid">
        <div class="detail-row"><span>Codigo</span><strong>${escapeHtml(record.codigoEmpleado)}</strong></div>
        <div class="detail-row"><span>Cedula</span><strong>${escapeHtml(record.cedula)}</strong></div>
        <div class="detail-row"><span>Documento</span><strong>${escapeHtml(record.tipoDocumento)}</strong></div>
        <div class="detail-row"><span>Archivo</span><strong>${escapeHtml(record.nombreArchivo || "Sin archivo")}</strong></div>
        <div class="detail-row"><span>Fecha documento</span><strong>${escapeHtml(record.fechaDocumento ? model.formatShortDate(record.fechaDocumento) : "-")}</strong></div>
        <div class="detail-row"><span>Vencimiento</span><strong>${escapeHtml(record.fechaVencimiento ? model.formatShortDate(record.fechaVencimiento) : "-")}</strong></div>
        <div class="detail-row"><span>Registro</span><strong>${escapeHtml(formatDateTime(record.fechaRegistro))}</strong></div>
        <div class="detail-row"><span>Observacion</span><strong>${escapeHtml(record.observacion || "-")}</strong></div>
      </div>

      <div class="detail-note">
        ${
          record.tieneArchivo
            ? `<a class="ghost-button" href="${escapeHtml(record.downloadUrl)}" target="_blank" rel="noreferrer">Abrir archivo</a>`
            : "Este expediente no tiene archivo adjunto."
        }
      </div>
    `;
  };

  const renderClockDetail = (row, branding) => {
    if (!row) {
      elements.clockDetailTitle.textContent = "Sin seleccion";
      elements.clockDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona una jornada para ver el detalle.</p>
        </div>
      `;
      return;
    }

    elements.clockDetailTitle.textContent = `${row.nombreEmpleado} - ${model.formatShortDate(row.fechaOperacion)}`;
    elements.clockDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="detail-row">
          <span>Empleado</span>
          <strong>${escapeHtml(row.codigoEmpleado)}</strong>
        </div>
        <div class="cell-stack">
          <strong>${escapeHtml(row.nombreEmpleado)}</strong>
          <small>${escapeHtml(row.nombreCargo)} / ${escapeHtml(row.nombreDepartamento)}</small>
        </div>
      </div>

      <div class="detail-grid">
        <div class="detail-row"><span>Cedula</span><strong>${escapeHtml(row.cedula)}</strong></div>
        <div class="detail-row"><span>Entrada</span><strong>${escapeHtml(row.horaEntrada || "-")}</strong></div>
        <div class="detail-row"><span>Salida</span><strong>${escapeHtml(row.horaSalida || "-")}</strong></div>
        <div class="detail-row"><span>Horas trabajadas</span><strong>${escapeHtml(`${formatDecimal(row.horasTrabajadas)} h`)}</strong></div>
        <div class="detail-row"><span>Extra / menos</span><strong class="${getHoursDiffTone(row.horasExtraMenos)}">${escapeHtml(formatSignedHours(row.horasExtraMenos))}</strong></div>
        <div class="detail-row"><span>Horario</span><strong>${escapeHtml(row.nombreHorario || "Base 8 h/dia")}</strong></div>
        <div class="detail-row"><span>Jornada esperada</span><strong>${escapeHtml(`${formatDecimal(row.horasDiarias || 8)} h`)}</strong></div>
        <div class="detail-row"><span>Estado</span><strong class="${row.estadoJornada === "CERRADA" ? "is-success" : "is-warning"}">${escapeHtml(row.estadoJornada)}</strong></div>
        <div class="detail-row"><span>Total marcaciones</span><strong>${escapeHtml(String(row.totalMarcaciones ?? 0))}</strong></div>
        <div class="detail-row"><span>Ultima accion</span><strong>${escapeHtml(row.ultimaAccion || "-")}</strong></div>
      </div>

      <div class="clock-history-card">
        <div class="panel-copy">
          <span class="eyebrow">Marcas</span>
          <h3>Secuencia consolidada</h3>
        </div>
        <div class="mark-list">
          ${(row.marcas || [])
            .map(
              (mark) => `
                <article class="mark-item">
                  <div class="cell-stack">
                    <strong>${escapeHtml(mark.tipoMarcacion)}</strong>
                    <small>${escapeHtml(mark.origen || "RELOJ")}</small>
                  </div>
                  <strong>${escapeHtml(String(mark.fechaHoraMarcacion || "").slice(11) || "-")}</strong>
                </article>
              `,
            )
            .join("")}
        </div>
      </div>

      <p class="detail-note">
        ${escapeHtml(
          branding?.logoPending
            ? "El logo corporativo del reporte queda pendiente de configuracion."
            : "El reporte tomara el logo corporativo configurado.",
        )}
      </p>
    `;
  };

  const renderReportDetail = (row, branding) => {
    if (!row) {
      elements.reportDetailTitle.textContent = "Sin seleccion";
      elements.reportDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un empleado para ver su saldo de vacaciones.</p>
        </div>
      `;
      return;
    }

    const consumidos = Number(row.diasConsumidos || row.diasTomadosVacacion || 0);
    const pendientes = Number(row.diasPendientes || row.diasPendientesVacacion || 0);

    elements.reportDetailTitle.textContent = row.nombreEmpleado || "Empleado";
    elements.reportDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="detail-row">
          <span>Codigo</span>
          <strong>${escapeHtml(row.codigoEmpleado || "-")}</strong>
        </div>
        <div class="detail-row">
          <span>Disponibles</span>
          <strong>${escapeHtml(`${formatDecimal(row.diasDisponibles)} dia(s)`)}</strong>
        </div>
      </div>

      <div class="detail-grid">
        <div class="detail-row"><span>Fecha ingreso</span><strong>${escapeHtml(model.formatShortDate(row.fechaIngreso))}</strong></div>
        <div class="detail-row"><span>Departamento</span><strong>${escapeHtml(row.nombreDepartamento || "-")}</strong></div>
        <div class="detail-row"><span>Cargo</span><strong>${escapeHtml(row.nombreCargo || "-")}</strong></div>
        <div class="detail-row"><span>Estado</span><strong class="${model.getStatusTone(row.nombreEstadoEmpleado)}">${escapeHtml(row.nombreEstadoEmpleado || "-")}</strong></div>
        <div class="detail-row"><span>Contrato vigente</span><strong>${escapeHtml(row.nombreTipoContratoVigente || "Sin contrato vigente")}</strong></div>
        <div class="detail-row"><span>Fecha corte</span><strong>${escapeHtml(model.formatShortDate(row.fechaCorte))}</strong></div>
      </div>

      <div class="clock-history-card">
        <div class="panel-copy">
          <span class="eyebrow">Saldo al corte</span>
          <h3>Resumen de vacaciones</h3>
        </div>

        <div class="detail-grid">
          <div class="detail-row"><span>Dias acumulados</span><strong>${escapeHtml(`${formatDecimal(row.diasAcumulados)} d`)}</strong></div>
          <div class="detail-row"><span>Vacaciones consumidas</span><strong>${escapeHtml(`${formatDecimal(consumidos)} d`)}</strong></div>
          <div class="detail-row"><span>Vacaciones pendientes</span><strong>${escapeHtml(`${formatDecimal(pendientes)} d`)}</strong></div>
        </div>
      </div>

      <p class="detail-note">
        ${escapeHtml(
          row.acumulaVacaciones
            ? "El colaborador acumula vacaciones con base en su contrato vigente."
            : row.motivoNoAcumulacion || "El contrato vigente no acumula vacaciones.",
        )}
      </p>

      <p class="detail-note">
        ${escapeHtml(
          branding?.logoPending
            ? "El logo corporativo del reporte queda pendiente de configuracion."
            : "El reporte usara el logo corporativo configurado.",
        )}
      </p>
    `;
  };

  const renderAuditDetail = (row) => {
    if (!row) {
      elements.auditDetailTitle.textContent = "Sin seleccion";
      elements.auditDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona un movimiento para ver su detalle.</p>
        </div>
      `;
      return;
    }

    elements.auditDetailTitle.textContent = `${row.process || "RRHH"} - ${row.eventType || "MOVIMIENTO"}`;
    elements.auditDetailBody.innerHTML = `
      <div class="detail-header">
        <div class="detail-row">
          <span>Proceso</span>
          <strong>${escapeHtml(row.process || "-")}</strong>
        </div>
        <div class="detail-row">
          <span>Fecha</span>
          <strong>${escapeHtml(formatDateTime(row.occurredAt))}</strong>
        </div>
      </div>

      <div class="detail-grid">
        <div class="detail-row"><span>Evento</span><strong>${escapeHtml(row.eventType || "-")}</strong></div>
        <div class="detail-row"><span>Usuario</span><strong>${escapeHtml(row.user || "sistema")}</strong></div>
        <div class="detail-row"><span>Referencia</span><strong>${escapeHtml(row.reference || "-")}</strong></div>
        <div class="detail-row"><span>Modulo</span><strong>RRHH</strong></div>
      </div>

      <div class="clock-history-card">
        <div class="panel-copy">
          <span class="eyebrow">Descripcion</span>
          <h3>Detalle auditado</h3>
        </div>
        <p class="detail-note">${escapeHtml(row.description || "Sin descripcion adicional.")}</p>
      </div>
    `;
  };

  const setActionState = ({ hasSelection, busy, detailVisible = false }) => {
    elements.editEmployeeButton.disabled = !hasSelection || busy;
    elements.deleteEmployeeButton.disabled = !hasSelection || busy;
    elements.refreshButton.disabled = busy;
    elements.newEmployeeButton.disabled = busy;
    elements.viewEmployeeButton.disabled = !hasSelection || busy;
    setEmployeeDetailVisibility(detailVisible);
  };

  const setContractActionState = ({ hasSelection, busy }) => {
    elements.editContractButton.disabled = !hasSelection || busy;
    elements.deleteContractButton.disabled = !hasSelection || busy;
    elements.printContractButton.disabled = !hasSelection || busy;
    elements.refreshContractButton.disabled = busy;
    elements.newContractButton.disabled = busy;
  };

  const setWorkflowActionState = ({
    hasSelection,
    busy,
    canEdit,
    canResolve,
    canApprove = canResolve,
    canReject = canResolve,
    showApprove = true,
    showReject = true,
    showExtra = false,
    canExtra = true,
    extraNeedsSelection = false,
    detailVisible = false,
  }) => {
    elements.refreshWorkflowButton.disabled = busy;
    elements.newWorkflowButton.disabled = busy;
    elements.viewWorkflowButton.disabled = !hasSelection || busy;
    elements.editWorkflowButton.disabled = !hasSelection || !canEdit || busy;
    elements.approveWorkflowButton.disabled = !showApprove || !hasSelection || !canApprove || busy;
    elements.rejectWorkflowButton.disabled = !showReject || !hasSelection || !canReject || busy;
    elements.workflowExtraButton.disabled =
      !showExtra || !canExtra || busy || (extraNeedsSelection && !hasSelection);
    setWorkflowDetailVisibility(detailVisible);
  };

  const setClockActionState = ({ busy, hasRows }) => {
    elements.refreshClockButton.disabled = busy;
    elements.exportClockExcelButton.disabled = busy || !hasRows;
    elements.exportClockPdfButton.disabled = busy || !hasRows;
  };

  const setReportActionState = ({ busy, hasRows }) => {
    elements.refreshReportButton.disabled = busy;
    elements.exportReportExcelButton.disabled = busy || !hasRows;
    elements.exportReportPdfButton.disabled = busy || !hasRows;
  };

  const setAuditActionState = ({ busy }) => {
    elements.refreshAuditButton.disabled = busy;
  };

  const setStructureActionState = ({ busy, hasSelection }) => {
    elements.structureSearchInput.disabled = busy;
    elements.structureDepartmentFilter.disabled = busy;
    elements.refreshStructureButton.disabled = busy;
    elements.loadStructureDemoButton.disabled = busy;
    elements.newStructureButton.disabled = busy;
    elements.editStructureButton.disabled = !hasSelection || busy;
    elements.deleteStructureButton.disabled = !hasSelection || busy;
  };

  const populateForm = ({ mode, employee, catalogs }) => {
    clearFormErrors(elements.employeeForm);
    elements.employeeForm.reset();

    const employeeId = Number(employee.employeeId || employee.idEmpleado || 0);
    const supervisorOptions = (catalogs.supervisors || []).filter(
      (supervisor) => Number(supervisor.id || 0) !== employeeId,
    );

    fillSelect(
      elements.idDepartamento,
      catalogs.departments || [],
      "Seleccione departamento",
      employee.idDepartamento || "",
    );
    fillSelect(elements.idCargo, catalogs.positions || [], "Seleccione cargo", employee.idCargo || "");
    fillSelect(
      elements.idSupervisorEmpleado,
      supervisorOptions.map((item) => ({
        id: item.id,
        name: item.username
          ? `${item.code} - ${item.name} (${item.position || "Sin cargo"})`
          : `${item.code} - ${item.name}`,
      })),
      "Seleccione jefe inmediato",
      employee.idSupervisorEmpleado || "",
    );
    fillSelect(elements.idBanco, catalogs.banks || [], "Seleccione banco", employee.idBanco || "");
    fillSelect(elements.sexo, model.getSexoOptions(), "Seleccione", employee.sexo || "");
    fillSelect(
      elements.estadoCivil,
      model.getEstadoCivilOptions(),
      "Seleccione",
      employee.estadoCivil || "",
    );

    elements.employeeId.value = employee.employeeId || employee.idEmpleado || "";
    elements.codigoEmpleado.value = employee.codigoEmpleado || catalogs.suggestedCode || "";
    elements.usuarioSistema.value = employee.usuarioSistema || "";
    elements.cedula.value = employee.cedula || "";
    elements.idSupervisorEmpleado.value = employee.idSupervisorEmpleado || "";
    elements.nombres.value = employee.nombres || "";
    elements.apellidos.value = employee.apellidos || "";
    elements.fechaIngreso.value = model.isoDateToDisplay(employee.fechaIngreso) || "";
    elements.fechaNacimiento.value = model.isoDateToDisplay(employee.fechaNacimiento) || "";
    elements.telefono.value = employee.telefono || "";
    elements.correo.value = employee.correo || "";
    elements.inss.value = employee.inss || "";
    elements.numeroCuentaBancaria.value = employee.numeroCuentaBancaria || "";
    elements.direccion.value = employee.direccion || "";

    elements.employeeModalKicker.textContent = mode === "edit" ? "Edicion" : "Alta";
    elements.employeeModalTitle.textContent = mode === "edit" ? "Editar empleado" : "Crear empleado";
    elements.saveEmployeeButton.dataset.defaultLabel = mode === "edit" ? "Guardar cambios" : "Guardar";
    resetSaveButton(elements.saveEmployeeButton, elements.saveEmployeeButton.dataset.defaultLabel);
  };

  const populateContractForm = ({ mode, contract, catalogs }) => {
    clearFormErrors(elements.contractForm);
    elements.contractForm.reset();
    elements.contractForm.dataset.mode = mode;

    const employeeOptions = [...(catalogs.employees || [])]
      .map((employee) => {
        const alert = resolveContractEmployeeAlert(employee);
        return {
          ...employee,
          contractAlertCode: alert.code || employee.contractAlertCode || "",
          contractAlertLabel: alert.label || employee.contractAlertLabel || "",
        };
      })
      .filter((employee) => isEligibleContractEmployee(employee, contract.idEmpleado))
      .sort((left, right) => {
        const leftCode = String(left.contractAlertCode || "").toUpperCase();
        const rightCode = String(right.contractAlertCode || "").toUpperCase();
        if (leftCode !== rightCode) {
          return leftCode === "SIN_CONTRATO" ? -1 : 1;
        }

        return String(left.name || "").localeCompare(String(right.name || ""), "es-NI");
      });

    if (
      contract.idEmpleado &&
      !employeeOptions.some((employee) => Number(employee.id) === Number(contract.idEmpleado))
    ) {
      employeeOptions.unshift({
        id: contract.idEmpleado,
        code: contract.codigoEmpleado,
        name: contract.nombreEmpleado,
        department: contract.nombreDepartamento,
        position: contract.nombreCargo,
        currentContractId: contract.idContrato,
        currentContractNumber: contract.numeroContrato,
        currentContractEndDate: contract.fechaFin || null,
        contractAlertCode: null,
        contractAlertLabel: "Contrato actual",
      });
    }

    fillSelect(
      elements.contractEmployeeId,
      employeeOptions.map((employee) => ({
        id: employee.id,
        name: buildContractEmployeeOptionLabel(employee),
      })),
      employeeOptions.length
        ? "Seleccione empleado"
        : "No hay empleados sin contrato o por vencer",
      contract.idEmpleado || "",
    );
    elements.contractEmployeeId.disabled = employeeOptions.length === 0 && mode !== "edit";
    fillSelect(
      elements.idTipoContrato,
      (catalogs.contractTypes || []).map((item) => ({
        id: item.id,
        name: item.name,
      })),
      "Seleccione tipo de contrato",
      contract.idTipoContrato || "",
    );
    fillSelect(
      elements.idHorarioLaboral,
      (catalogs.schedules || []).map((item) => ({
        id: item.id,
        name: `${item.name} (${formatHours(item.weeklyHours)} h/sem)`,
      })),
      "Seleccione horario laboral",
      contract.idHorarioLaboral || "",
    );
    fillSelect(
      elements.moneda,
      (catalogs.currencies || []).map((item) => ({
        value: item.value,
        label: item.label,
      })),
      "Seleccione moneda",
      contract.moneda || catalogs.defaultCurrency || "NIO",
    );

    elements.contractId.value = contract.contractId || contract.idContrato || "";
    elements.numeroContrato.value = contract.numeroContrato || "";
    elements.contractFechaInicio.value = model.isoDateToDisplay(contract.fechaInicio) || "";
    elements.contractFechaFin.value = model.isoDateToDisplay(contract.fechaFin) || "";
    elements.salarioBaseMensual.value =
      contract.salarioBaseMensual !== null &&
      contract.salarioBaseMensual !== undefined &&
      contract.salarioBaseMensual !== ""
        ? Number(contract.salarioBaseMensual)
        : "";
    elements.esContratoVigente.checked = contract.esContratoVigente !== false;
    elements.observacion.value = contract.observacion || "";

    elements.contractModalKicker.textContent = mode === "edit" ? "Edicion" : "Alta";
    elements.contractModalTitle.textContent =
      mode === "edit" ? "Editar contrato laboral" : "Registrar contrato laboral";
    elements.saveContractButton.dataset.defaultLabel = mode === "edit" ? "Guardar cambios" : "Guardar";
    resetSaveButton(elements.saveContractButton, elements.saveContractButton.dataset.defaultLabel);
    renderContractEmployeeHint(findEmployeeOption(employeeOptions, contract.idEmpleado), mode);
  };

  const populateStructureForm = ({ mode, node = {}, catalogs = {} }) => {
    clearFormErrors(elements.structureForm);
    elements.structureForm.reset();
    elements.structureForm.dataset.mode = mode;

    const currentNodeId = Number(node.idNodoEstructura || 0);
    const parentOptions = (catalogs.parentNodes || [])
      .filter((item) => Number(item.id || item.Id || 0) !== currentNodeId)
      .map((item) => ({
        value: item.id || item.Id,
        label: item.code ? `${item.code} - ${item.label || item.name}` : item.label || item.name,
      }));

    fillSelect(elements.structureTipoNodo, catalogs.nodeTypes || [], "Seleccione tipo", node.tipoNodo || "");
    fillSelect(elements.structureParentNodeId, parentOptions, "Sin nodo padre", node.idNodoPadre || "");
    fillSelect(
      elements.structureEmployeeId,
      catalogs.employees || [],
      "Sin titular asignado",
      node.idEmpleadoTitular || "",
    );
    fillSelect(
      elements.structureDepartmentId,
      catalogs.departments || [],
      "Sin departamento asociado",
      node.idDepartamento || "",
    );
    fillSelect(
      elements.structurePositionId,
      catalogs.positions || [],
      "Sin cargo asociado",
      node.idCargo || "",
    );

    elements.structureNodeId.value = node.idNodoEstructura || "";
    elements.structureCodigoNodo.value = node.codigoNodo || "";
    elements.structureNombreNodo.value = node.nombreNodo || "";
    elements.structureOrdenVisual.value = node.ordenVisual ?? 0;
    elements.structureActivo.checked = node.activo !== false;
    elements.structureObservacion.value = node.observacion || "";

    elements.structureModalKicker.textContent = mode === "edit" ? "Edicion" : "Alta";
    elements.structureModalTitle.textContent = mode === "edit" ? "Editar nodo formal" : "Crear nodo formal";
    elements.saveStructureButton.dataset.defaultLabel = mode === "edit" ? "Guardar cambios" : "Guardar";
    resetSaveButton(elements.saveStructureButton, elements.saveStructureButton.dataset.defaultLabel);
  };

  const getWorkflowFormMarkup = (moduleId) => {
    const catalogConfig = model.getCatalogModuleConfig(moduleId);

    if (catalogConfig) {
      return `
        <label class="form-field">
          <span>${escapeHtml(catalogConfig.codeLabel || "Codigo")}</span>
          <input id="workflowCode" maxlength="30" autocomplete="off" />
          <small class="field-error" data-error-for="codigo"></small>
        </label>

        <label class="form-field">
          <span>${escapeHtml(catalogConfig.nameLabel || "Nombre")}</span>
          <input id="workflowName" maxlength="${escapeHtml(String(catalogConfig.nameMaxLength || 150))}" autocomplete="off" />
          <small class="field-error" data-error-for="nombre"></small>
        </label>

        ${
          catalogConfig.usesRelatedId
            ? `
              <label class="form-field form-field-full">
                <span>${escapeHtml(catalogConfig.relatedLabel || "Relacionado")}</span>
                <select id="workflowRelatedId"></select>
                <small class="field-error" data-error-for="relatedId"></small>
              </label>
            `
            : ""
        }

        ${
          catalogConfig.usesNumberValue1
            ? `
              <label class="form-field">
                <span>${escapeHtml(catalogConfig.number1Label || "Valor 1")}</span>
                <input id="workflowNumberValue1" type="number" inputmode="decimal" min="0" step="0.01" autocomplete="off" />
                <small class="field-error" data-error-for="numberValue1"></small>
              </label>
            `
            : ""
        }

        ${
          catalogConfig.usesNumberValue2
            ? `
              <label class="form-field">
                <span>${escapeHtml(catalogConfig.number2Label || "Valor 2")}</span>
                <input id="workflowNumberValue2" type="number" inputmode="decimal" min="0" step="0.01" autocomplete="off" />
                <small class="field-error" data-error-for="numberValue2"></small>
              </label>
            `
            : ""
        }

        ${
          catalogConfig.usesIntegerValue1
            ? `
              <label class="form-field">
                <span>${escapeHtml(catalogConfig.integerLabel || "Valor entero")}</span>
                <input id="workflowIntegerValue1" type="number" inputmode="numeric" min="1" step="1" autocomplete="off" />
                <small class="field-error" data-error-for="integerValue1"></small>
              </label>
            `
            : ""
        }

        ${
          catalogConfig.usesFlagValue1
            ? `
              <label class="checkbox-field form-field-full" for="workflowFlagValue1">
                <input id="workflowFlagValue1" type="checkbox" />
                <div class="checkbox-copy">
                  <strong>${escapeHtml(catalogConfig.flagLabel || "Bandera")}</strong>
                  <span>Activa esta opcion si aplica para este registro.</span>
                </div>
              </label>
            `
            : ""
        }

        ${
          catalogConfig.usesDescription
            ? `
              <label class="form-field form-field-full">
                <span>${escapeHtml(catalogConfig.descriptionLabel || "Descripcion")}</span>
                <textarea id="workflowObservation" rows="4" maxlength="300"></textarea>
                <small class="field-error" data-error-for="descripcion"></small>
              </label>
            `
            : ""
        }

        <label class="checkbox-field form-field-full" for="workflowActive">
          <input id="workflowActive" type="checkbox" />
          <div class="checkbox-copy">
            <strong>Registro activo</strong>
            <span>Si se desactiva, dejara de salir como opcion operativa.</span>
          </div>
        </label>
      `;
    }

    if (moduleId === "accion_personal") {
      return `
        <label class="form-field form-field-full">
          <span>Empleado</span>
          <select id="workflowEmployeeId"></select>
          <small class="field-error" data-error-for="idEmpleado"></small>
        </label>

        <div class="balance-card form-field-full" id="workflowActionEmployeeHint" hidden></div>

        <label class="form-field">
          <span>Tipo de accion</span>
          <input id="workflowTypeText" list="workflowActionTypeOptions" maxlength="50" autocomplete="off" />
          <datalist id="workflowActionTypeOptions"></datalist>
          <small class="field-error" data-error-for="tipoAccion"></small>
        </label>

        <label class="form-field">
          <span>Fecha accion</span>
          <input id="workflowStartDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="fechaAccion"></small>
        </label>

        <div class="detail-grid form-field-full action-current-card" id="workflowActionCurrentCard"></div>

        <div class="detail-grid form-field-full action-current-meta">
          <div class="detail-row">
            <span>Jerarquia actual</span>
            <strong id="workflowActionCurrentHierarchy">Sin jerarquia definida</strong>
          </div>
          <div class="detail-row">
            <span>Salario actual</span>
            <strong id="workflowActionCurrentSalary">NIO 0.00</strong>
          </div>
          <div class="detail-row">
            <span>Vigencia actual</span>
            <strong id="workflowActionCurrentContractEnd">Sin fecha fin</strong>
          </div>
        </div>

        <label class="form-field form-field-full" id="workflowActionPositionGroup" hidden>
          <span>Nuevo cargo</span>
          <select id="workflowRelatedId"></select>
          <small class="field-error" data-error-for="idCargoNuevo"></small>
        </label>

        <div class="balance-card form-field-full" id="workflowActionNextHierarchy">
          Selecciona el nuevo cargo para conocer la jerarquia propuesta.
        </div>

        <label class="form-field" id="workflowActionSalaryGroup" hidden>
          <span>Nuevo salario base mensual</span>
          <input id="workflowNumberValue1" type="number" inputmode="decimal" min="0" step="0.01" autocomplete="off" />
          <small class="field-error" data-error-for="nuevoSalarioBaseMensual"></small>
        </label>

        <label class="form-field" id="workflowActionContractGroup" hidden>
          <span>Nueva fecha fin de contrato</span>
          <input id="workflowEndDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="nuevaFechaFinContrato"></small>
        </label>

        <label class="checkbox-field form-field-full" for="workflowFlagValue1">
          <input id="workflowFlagValue1" type="checkbox" />
          <div class="checkbox-copy">
            <strong>Aplicar cambio operativo</strong>
            <span>Actualiza puesto, salario o vigencia contractual cuando este movimiento deba impactar al colaborador.</span>
          </div>
        </label>

        <label class="form-field form-field-full">
          <span>Descripcion / motivo</span>
          <textarea id="workflowObservation" rows="4" maxlength="500"></textarea>
          <small class="field-error" data-error-for="descripcionAccion"></small>
        </label>

        <div class="detail-grid form-field-full action-memo-card">
          <div class="detail-row">
            <span>Memorandum sugerido</span>
            <strong>Vista previa</strong>
          </div>
          <p class="detail-note" id="workflowActionMemoPreview">
            Se generara una vista previa del memorandum segun el movimiento seleccionado.
          </p>
        </div>
      `;
    }

    if (moduleId === "expediente_documento") {
      return `
        <label class="form-field form-field-full">
          <span>Empleado</span>
          <select id="workflowEmployeeId"></select>
          <small class="field-error" data-error-for="idEmpleado"></small>
        </label>

        <label class="form-field">
          <span>Tipo documento</span>
          <input id="workflowTypeText" list="workflowDocumentTypeOptions" maxlength="100" autocomplete="off" />
          <datalist id="workflowDocumentTypeOptions"></datalist>
          <small class="field-error" data-error-for="tipoDocumento"></small>
        </label>

        <label class="form-field">
          <span>Fecha documento</span>
          <input id="workflowStartDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="fechaDocumento"></small>
        </label>

        <label class="form-field">
          <span>Fecha vencimiento</span>
          <input id="workflowEndDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="fechaVencimiento"></small>
        </label>

        <label class="form-field form-field-full">
          <span>Archivo</span>
          <input id="workflowFile" type="file" accept=".pdf,.png,.jpg,.jpeg,.doc,.docx" />
          <small class="field-error" data-error-for="archivo"></small>
        </label>

        <p class="detail-note" id="workflowCurrentFileNote" hidden></p>

        <label class="checkbox-field form-field-full" id="workflowRemoveFileWrap" hidden for="workflowRemoveFile">
          <input id="workflowRemoveFile" type="checkbox" />
          <div class="checkbox-copy">
            <strong>Remover archivo actual</strong>
            <span>Si adjuntas otro archivo, este reemplazo se hara automaticamente.</span>
          </div>
        </label>

        <label class="form-field form-field-full">
          <span>Observacion</span>
          <textarea id="workflowObservation" rows="4" maxlength="500"></textarea>
          <small class="field-error" data-error-for="observacion"></small>
        </label>
      `;
    }

    if (moduleId === "solicitud_permiso") {
      return `
        <label class="form-field form-field-full">
          <span>Empleado</span>
          <select id="workflowEmployeeId"></select>
          <small class="field-error" data-error-for="idEmpleado"></small>
        </label>

        <div class="balance-card form-field-full" id="workflowVacationBalance">
          Selecciona colaborador y fechas para ver el saldo disponible de vacaciones.
        </div>

        <label class="form-field">
          <span>Modalidad de vacacion</span>
          <select id="workflowTypeId"></select>
          <small class="field-error" data-error-for="idTipoPermiso"></small>
        </label>

        <label class="form-field">
          <span>Fecha inicio</span>
          <input id="workflowStartDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="fechaInicio"></small>
        </label>

        <label class="form-field">
          <span>Fecha fin</span>
          <input id="workflowEndDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="fechaFin"></small>
        </label>

        <label class="checkbox-field form-field-full" for="workflowHalfDay">
          <input id="workflowHalfDay" type="checkbox" />
          <div class="checkbox-copy">
            <strong>Medio dia</strong>
            <span>Si se activa, vacaciones descuenta 0.5 dia y la fecha fin queda igual a la fecha de inicio.</span>
          </div>
        </label>

        <div class="halfday-group form-field-full" id="workflowHalfDayGroup" hidden>
          <span class="halfday-group-title">Jornada del medio dia</span>
          <div class="halfday-options">
            <label class="halfday-option" for="workflowHalfDayMorning">
              <input id="workflowHalfDayMorning" name="workflowHalfDayShift" type="radio" value="MANANA" />
              <span>Manana</span>
            </label>
            <label class="halfday-option" for="workflowHalfDayAfternoon">
              <input id="workflowHalfDayAfternoon" name="workflowHalfDayShift" type="radio" value="TARDE" />
              <span>Tarde</span>
            </label>
          </div>
          <small class="field-error" data-error-for="jornadaMedioDia"></small>
        </div>

        <label class="form-field form-field-full">
          <span>Observacion</span>
          <textarea id="workflowObservation" rows="4" maxlength="320"></textarea>
          <small class="field-error" data-error-for="observacion"></small>
        </label>
      `;
    }

    if (moduleId === "vacacion") {
      return `
        <label class="form-field form-field-full">
          <span>Empleado</span>
          <select id="workflowEmployeeId"></select>
          <small class="field-error" data-error-for="idEmpleado"></small>
        </label>

        <div class="balance-card form-field-full" id="workflowVacationBalance">
          Selecciona colaborador y fechas para ver el saldo disponible de vacaciones.
        </div>

        <label class="form-field">
          <span>Fecha inicio</span>
          <input id="workflowStartDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="fechaInicio"></small>
        </label>

        <label class="form-field">
          <span>Fecha fin</span>
          <input id="workflowEndDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
          <small class="field-error" data-error-for="fechaFin"></small>
        </label>

        <label class="checkbox-field form-field-full" for="workflowHalfDay">
          <input id="workflowHalfDay" type="checkbox" />
          <div class="checkbox-copy">
            <strong>Medio dia</strong>
            <span>Si se activa, vacaciones descuenta 0.5 dia y la fecha fin queda igual a la fecha de inicio.</span>
          </div>
        </label>

        <div class="halfday-group form-field-full" id="workflowHalfDayGroup" hidden>
          <span class="halfday-group-title">Jornada del medio dia</span>
          <div class="halfday-options">
            <label class="halfday-option" for="workflowHalfDayMorning">
              <input id="workflowHalfDayMorning" name="workflowHalfDayShift" type="radio" value="MANANA" />
              <span>Manana</span>
            </label>
            <label class="halfday-option" for="workflowHalfDayAfternoon">
              <input id="workflowHalfDayAfternoon" name="workflowHalfDayShift" type="radio" value="TARDE" />
              <span>Tarde</span>
            </label>
          </div>
          <small class="field-error" data-error-for="jornadaMedioDia"></small>
        </div>

        <label class="form-field form-field-full">
          <span>Observacion solicitud</span>
          <textarea id="workflowObservation" rows="4" maxlength="500"></textarea>
          <small class="field-error" data-error-for="observacionSolicitud"></small>
        </label>
      `;
    }

    return `
      <label class="form-field form-field-full">
        <span>Empleado</span>
        <select id="workflowEmployeeId"></select>
        <small class="field-error" data-error-for="idEmpleado"></small>
      </label>

      <label class="form-field">
        <span>Tipo hora extra</span>
        <select id="workflowTypeId"></select>
        <small class="field-error" data-error-for="idTipoHoraExtra"></small>
      </label>

      <label class="form-field">
        <span>Fecha</span>
        <input id="workflowHoursDate" type="text" inputmode="numeric" maxlength="10" placeholder="dd/mm/aaaa" autocomplete="off" />
        <small class="field-error" data-error-for="fechaHoraExtra"></small>
      </label>

      <label class="form-field">
        <span>Cantidad horas</span>
        <input id="workflowHoursAmount" type="number" inputmode="decimal" min="0" max="16" step="0.01" autocomplete="off" />
        <small class="field-error" data-error-for="cantidadHoras"></small>
      </label>

      <label class="form-field form-field-full">
        <span>Observacion</span>
        <textarea id="workflowObservation" rows="4" maxlength="500"></textarea>
        <small class="field-error" data-error-for="observacion"></small>
      </label>
    `;
  };

  const populateWorkflowForm = ({ moduleId, moduleLabel, mode, record, catalogs }) => {
    clearFormErrors(elements.workflowForm);
    elements.workflowForm.reset();
    elements.workflowForm.dataset.mode = mode;
    elements.workflowForm.dataset.moduleId = moduleId;
    elements.workflowFormFields.innerHTML = getWorkflowFormMarkup(moduleId);
    workflowFieldMap = buildWorkflowFieldMap(moduleId);

    const workflowElements = getWorkflowElements();
    fillSelect(
      workflowElements.employee,
      (catalogs.employees || []).map((employee) => ({
        id: employee.id,
        name: `${employee.code} - ${employee.name}`,
      })),
      "Seleccione empleado",
      record.idEmpleado || "",
    );

    const catalogConfig = model.getCatalogModuleConfig(moduleId);
    if (catalogConfig) {
      if (catalogConfig.usesRelatedId) {
        fillSelect(
          workflowElements.related,
          (catalogs.departments || []).map((department) => ({
            id: department.id,
            name: department.active ? `${department.code} - ${department.name}` : `${department.code} - ${department.name} (inactivo)`,
          })),
          "Seleccione departamento",
          record.relatedId || "",
        );
      }

      if (workflowElements.code) {
        workflowElements.code.value = record.codigo || "";
      }

      if (workflowElements.name) {
        workflowElements.name.value = record.nombre || "";
      }

      if (workflowElements.numberValue1) {
        workflowElements.numberValue1.value =
          record.numberValue1 !== null && record.numberValue1 !== undefined ? Number(record.numberValue1) : "";
      }

      if (workflowElements.numberValue2) {
        workflowElements.numberValue2.value =
          record.numberValue2 !== null && record.numberValue2 !== undefined ? Number(record.numberValue2) : "";
      }

      if (workflowElements.integerValue1) {
        workflowElements.integerValue1.value =
          record.integerValue1 !== null && record.integerValue1 !== undefined ? Number(record.integerValue1) : "";
      }

      if (workflowElements.flagValue1) {
        workflowElements.flagValue1.checked = Boolean(record.flagValue1);
      }

      if (workflowElements.active) {
        workflowElements.active.checked = record.activo !== false;
      }

      if (workflowElements.observation) {
        workflowElements.observation.value = record.descripcion || "";
      }
    } else if (moduleId === "accion_personal") {
      const actionOptions = document.getElementById("workflowActionTypeOptions");
      const employeeOptions = [...(catalogs.employees || [])];
      const positionOptions = [...(catalogs.positions || [])];

      if (record.idEmpleado && !employeeOptions.some((employee) => Number(employee.id) === Number(record.idEmpleado))) {
        employeeOptions.unshift({
          id: record.idEmpleado,
          code: record.codigoEmpleado,
          name: record.nombreEmpleado,
          department: record.nombreDepartamento,
          position: record.nombreCargo,
          hierarchyLabel: record.jerarquiaActual,
          currentContractId: record.currentContractId,
          currentContractNumber: record.currentContractNumber,
          currentContractEndDate: record.fechaFinContratoActual,
          currentSalary: record.salarioActual,
          currentCurrency: record.monedaSalario,
          contractAlertCode: null,
          contractAlertLabel: record.currentContractNumber ? "Contrato actual" : "Sin contrato vigente",
        });
      }

      if (
        record.idCargoNuevo &&
        !positionOptions.some((position) => Number(position.id) === Number(record.idCargoNuevo))
      ) {
        positionOptions.unshift({
          id: record.idCargoNuevo,
          name: record.nombreCargoNuevo || "Cargo actualizado",
          department: record.nombreDepartamentoNuevo || record.nombreDepartamento,
          hierarchyLabel: record.jerarquiaNueva || "Jerarquia definida",
        });
      }

      if (actionOptions) {
        actionOptions.innerHTML = (catalogs.actionTypes || [])
          .map((item) => `<option value="${escapeHtml(item.label || item.value)}"></option>`)
          .join("");
      }

      fillSelect(
        workflowElements.employee,
        employeeOptions.map((employee) => ({
          id: employee.id,
          name: `${employee.code} - ${employee.name}`,
        })),
        "Seleccione empleado",
        record.idEmpleado || "",
      );
      fillSelect(
        workflowElements.related,
        positionOptions.map((position) => ({
          id: position.id,
          name: `${position.name} · ${position.department || "Sin area"} · ${position.hierarchyLabel || "Sin jerarquia"}`,
        })),
        "Seleccione nuevo cargo",
        record.idCargoNuevo || "",
      );
      workflowElements.typeText.value = record.tipoAccion || "";
      workflowElements.startDate.value = model.isoDateToDisplay(record.fechaAccion) || "";
      if (workflowElements.numberValue1) {
        workflowElements.numberValue1.value =
          record.nuevoSalarioBaseMensual !== null && record.nuevoSalarioBaseMensual !== undefined
            ? Number(record.nuevoSalarioBaseMensual)
            : "";
      }
      if (workflowElements.endDate) {
        workflowElements.endDate.value = model.isoDateToDisplay(record.nuevaFechaFinContrato) || "";
      }
      if (workflowElements.flagValue1) {
        workflowElements.flagValue1.checked = record.aplicarCambioOperativo !== false;
      }
      workflowElements.observation.value = record.descripcionAccion || "";
      syncActionWorkflowForm(
        {
          employees: employeeOptions,
          positions: positionOptions,
        },
        record,
      );
    } else if (moduleId === "expediente_documento") {
      const documentOptions = document.getElementById("workflowDocumentTypeOptions");
      const currentFileNote = document.getElementById("workflowCurrentFileNote");
      const removeFileWrap = document.getElementById("workflowRemoveFileWrap");

      if (documentOptions) {
        documentOptions.innerHTML = (catalogs.documentTypes || [])
          .map((item) => `<option value="${escapeHtml(item.label || item.value)}"></option>`)
          .join("");
      }

      workflowElements.typeText.value = record.tipoDocumento || "";
      workflowElements.startDate.value = model.isoDateToDisplay(record.fechaDocumento) || "";
      workflowElements.endDate.value = model.isoDateToDisplay(record.fechaVencimiento) || "";
      workflowElements.observation.value = record.observacion || "";

      if (workflowElements.removeFile) {
        workflowElements.removeFile.checked = false;
      }

      if (currentFileNote) {
        currentFileNote.hidden = !record.tieneArchivo;
        currentFileNote.textContent = record.tieneArchivo
          ? `Archivo actual: ${record.nombreArchivo || "Adjunto existente"}`
          : "Este expediente aun no tiene archivo adjunto.";
      }

      if (removeFileWrap) {
        removeFileWrap.hidden = !record.tieneArchivo;
      }
    } else if (moduleId === "solicitud_permiso") {
      fillSelect(
        workflowElements.type,
        (catalogs.permissionTypes || []).map((item) => ({
          id: item.id,
          name: item.name,
        })),
        "Seleccione modalidad de vacacion",
        record.idTipoPermiso || "",
      );
      workflowElements.startDate.value = model.isoDateToDisplay(record.fechaInicio) || "";
      workflowElements.endDate.value = model.isoDateToDisplay(record.fechaFin) || "";
      workflowElements.observation.value = record.observacion || "";
      if (workflowElements.halfDay) {
        workflowElements.halfDay.checked = Boolean(record.esMedioDia);
      }
      if (workflowElements.halfDayMorning) {
        workflowElements.halfDayMorning.checked = String(record.jornadaMedioDia || "").toUpperCase() === "MANANA";
      }
      if (workflowElements.halfDayAfternoon) {
        workflowElements.halfDayAfternoon.checked = String(record.jornadaMedioDia || "").toUpperCase() === "TARDE";
      }
    } else if (moduleId === "vacacion") {
      workflowElements.startDate.value = model.isoDateToDisplay(record.fechaInicio) || "";
      workflowElements.endDate.value = model.isoDateToDisplay(record.fechaFin) || "";
      workflowElements.observation.value = record.observacionSolicitud || "";
      if (workflowElements.halfDay) {
        workflowElements.halfDay.checked = Boolean(record.esMedioDia);
      }
      if (workflowElements.halfDayMorning) {
        workflowElements.halfDayMorning.checked = String(record.jornadaMedioDia || "").toUpperCase() === "MANANA";
      }
      if (workflowElements.halfDayAfternoon) {
        workflowElements.halfDayAfternoon.checked = String(record.jornadaMedioDia || "").toUpperCase() === "TARDE";
      }
    } else {
      fillSelect(
        workflowElements.type,
        (catalogs.overtimeTypes || []).map((item) => ({
          id: item.id,
          name: `${item.name} (${formatDecimal(item.factor)}x)`,
        })),
        "Seleccione tipo de hora extra",
        record.idTipoHoraExtra || "",
      );
      workflowElements.hoursDate.value = model.isoDateToDisplay(record.fechaHoraExtra) || "";
      workflowElements.hoursAmount.value =
        record.cantidadHoras !== null && record.cantidadHoras !== undefined ? Number(record.cantidadHoras) : "";
      workflowElements.observation.value = record.observacion || "";
    }

    elements.workflowRecordId.value =
      record.workflowRecordId ||
      record.idCatalogo ||
      record.idAccionPersonal ||
      record.idExpedienteDocumento ||
      record.idSolicitudPermiso ||
      record.idVacacion ||
      record.idHoraExtra ||
      "";
    elements.workflowModalKicker.textContent = mode === "edit" ? "Edicion" : "Alta";
    elements.workflowModalTitle.textContent =
      mode === "edit" ? `Editar ${moduleLabel.toLowerCase()}` : `Registrar ${moduleLabel.toLowerCase()}`;
    elements.saveWorkflowButton.dataset.defaultLabel = mode === "edit" ? "Guardar cambios" : "Guardar";
    resetSaveButton(elements.saveWorkflowButton, elements.saveWorkflowButton.dataset.defaultLabel);
  };

  const openEmployeeModal = (payload) => {
    populateForm(payload);
    elements.employeeModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => elements.cedula.focus(), 30);
  };

  const closeEmployeeModal = () => {
    elements.employeeModal.hidden = true;
    clearFormErrors(elements.employeeForm);
    resetSaveButton(elements.saveEmployeeButton);
    syncBodyState();
  };

  const openContractModal = (payload) => {
    populateContractForm(payload);
    elements.contractModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => elements.contractEmployeeId.focus(), 30);
  };

  const closeContractModal = () => {
    elements.contractModal.hidden = true;
    clearFormErrors(elements.contractForm);
    resetSaveButton(elements.saveContractButton);
    syncBodyState();
  };

  const openStructureModal = (payload) => {
    populateStructureForm(payload);
    elements.structureModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => elements.structureCodigoNodo.focus(), 30);
  };

  const closeStructureModal = () => {
    elements.structureModal.hidden = true;
    clearFormErrors(elements.structureForm);
    resetSaveButton(elements.saveStructureButton);
    syncBodyState();
  };

  const openWorkflowModal = (payload) => {
    populateWorkflowForm(payload);
    elements.workflowModal.hidden = false;
    syncBodyState();

    if (payload.moduleId === "accion_personal") {
      syncActionWorkflowForm(payload.catalogs, payload.record);
    }

    const firstField = elements.workflowFormFields.querySelector("input:not([type='hidden']), select, textarea");
    window.setTimeout(() => firstField?.focus(), 30);
  };

  const closeWorkflowModal = () => {
    elements.workflowModal.hidden = true;
    clearFormErrors(elements.workflowForm);
    workflowFieldMap = {};
    resetSaveButton(elements.saveWorkflowButton);
    syncBodyState();
  };

  const openDeleteModal = (employee) => {
    elements.deleteTargetText.innerHTML = `
      Se eliminara el empleado
      <strong>${escapeHtml(employee.codigoEmpleado)}</strong>
      -
      <strong>${escapeHtml(employee.nombreCompleto)}</strong>.
      Ingresa autorizacion de administrador para continuar.
    `;
    elements.adminUsuario.value = "";
    elements.adminPassword.value = "";
    elements.deleteError.textContent = "";
    elements.deleteModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => elements.adminUsuario.focus(), 30);
  };

  const openContractDeleteModal = (contract) => {
    elements.contractDeleteTargetText.innerHTML = `
      Se eliminara el contrato
      <strong>${escapeHtml(contract.numeroContrato)}</strong>
      del empleado
      <strong>${escapeHtml(contract.codigoEmpleado)} - ${escapeHtml(contract.nombreEmpleado)}</strong>.
      Ingresa autorizacion de administrador para continuar.
    `;
    elements.contractAdminUsuario.value = "";
    elements.contractAdminPassword.value = "";
    elements.contractDeleteError.textContent = "";
    elements.contractDeleteModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => elements.contractAdminUsuario.focus(), 30);
  };

  const openGenericDeleteModal = ({ title, kicker = "Autorizacion", message }) => {
    elements.genericDeleteKicker.textContent = kicker;
    elements.genericDeleteTitle.textContent = title;
    elements.genericDeleteTargetText.innerHTML = message;
    elements.genericAdminUsuario.value = "";
    elements.genericAdminPassword.value = "";
    elements.genericDeleteError.textContent = "";
    elements.confirmGenericDeleteButton.disabled = false;
    setButtonLabel(elements.confirmGenericDeleteButton, "Confirmar eliminacion", "trash");
    elements.genericDeleteModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => elements.genericAdminUsuario.focus(), 30);
  };

  const openWorkflowResolveModal = ({ moduleId, moduleLabel, record, action }) => {
    const actionLabel = action === "APROBAR" ? "Aprobar" : "Rechazar";
    const employeeName = escapeHtml(record.nombreEmpleado || record.codigoEmpleado || "registro");

    elements.workflowResolveModal.dataset.moduleId = moduleId;
    elements.workflowResolveModal.dataset.action = action;
    elements.workflowResolveModal.dataset.recordId =
      String(record.idSolicitudPermiso || record.idVacacion || record.idHoraExtra || "");
    elements.workflowResolveKicker.textContent = actionLabel;
    elements.workflowResolveTitle.textContent = `${actionLabel} ${moduleLabel.toLowerCase()}`;
    elements.workflowResolveText.innerHTML = `
      ${actionLabel}as el registro de
      <strong>${employeeName}</strong>.
      ${action === "RECHAZAR" ? "Debes explicar el motivo del rechazo." : "Confirma la operacion para continuar."}
    `;
    elements.workflowApprovedDaysField.hidden = !(moduleId === "vacacion" && action === "APROBAR");
    elements.workflowApprovedDays.value =
      moduleId === "vacacion" && action === "APROBAR"
        ? String(record.diasSolicitados ?? "")
        : "";
    elements.workflowResolutionObservation.value = "";
    elements.workflowResolveError.textContent = "";
    setButtonLabel(
      elements.confirmWorkflowResolveButton,
      action === "APROBAR" ? "Confirmar aprobacion" : "Confirmar rechazo",
      action === "APROBAR" ? "approve" : "reject",
    );
    elements.confirmWorkflowResolveButton.disabled = false;
    elements.workflowResolveModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => {
      if (!elements.workflowApprovedDaysField.hidden) {
        elements.workflowApprovedDays.focus();
        return;
      }

      elements.workflowResolutionObservation.focus();
    }, 30);
  };

  const openVacationBulkModal = () => {
    elements.vacationBulkForm?.reset();
    elements.vacationBulkDate.value = new Date().toISOString().slice(0, 10);
    elements.vacationBulkAmountHalf.checked = true;
    elements.vacationBulkError.textContent = "";
    elements.confirmVacationBulkButton.disabled = false;
    setButtonLabel(elements.confirmVacationBulkButton, "Aplicar ajuste", "calendar");
    elements.vacationBulkModal.hidden = false;
    syncBodyState();
    window.setTimeout(() => elements.vacationBulkDate.focus(), 30);
  };

  const closeDeleteModal = () => {
    elements.deleteModal.hidden = true;
    elements.deleteError.textContent = "";
    syncBodyState();
  };

  const closeContractDeleteModal = () => {
    elements.contractDeleteModal.hidden = true;
    elements.contractDeleteError.textContent = "";
    syncBodyState();
  };

  const closeGenericDeleteModal = () => {
    elements.genericDeleteModal.hidden = true;
    elements.genericDeleteError.textContent = "";
    syncBodyState();
  };

  const closeWorkflowResolveModal = () => {
    elements.workflowResolveModal.hidden = true;
    elements.workflowResolveError.textContent = "";
    elements.workflowApprovedDays.value = "";
    elements.workflowResolutionObservation.value = "";
    syncBodyState();
  };

  const closeVacationBulkModal = () => {
    elements.vacationBulkModal.hidden = true;
    elements.vacationBulkError.textContent = "";
    syncBodyState();
  };

  const forceCloseOverlays = () => {
    [
      elements.employeeModal,
      elements.contractModal,
      elements.structureModal,
      elements.workflowModal,
      elements.workflowResolveModal,
      elements.vacationBulkModal,
      elements.deleteModal,
      elements.contractDeleteModal,
      elements.genericDeleteModal,
    ].forEach((item) => {
      if (item) {
        item.hidden = true;
      }
    });

    clearFormErrors(elements.employeeForm);
    clearFormErrors(elements.contractForm);
    clearFormErrors(elements.structureForm);
    clearFormErrors(elements.workflowForm);
    workflowFieldMap = {};
    if (elements.deleteError) {
      elements.deleteError.textContent = "";
    }
    if (elements.contractDeleteError) {
      elements.contractDeleteError.textContent = "";
    }
    if (elements.genericDeleteError) {
      elements.genericDeleteError.textContent = "";
    }
    if (elements.workflowResolveError) {
      elements.workflowResolveError.textContent = "";
    }
    if (elements.vacationBulkError) {
      elements.vacationBulkError.textContent = "";
    }
    syncBodyState();
  };

  const readEmployeeForm = () => ({
    employeeId: elements.employeeId.value,
    codigoEmpleado: elements.codigoEmpleado.value,
    usuarioSistema: elements.usuarioSistema.value,
    cedula: elements.cedula.value,
    idDepartamento: elements.idDepartamento.value,
    idCargo: elements.idCargo.value,
    idSupervisorEmpleado: elements.idSupervisorEmpleado.value,
    nombres: elements.nombres.value,
    apellidos: elements.apellidos.value,
    fechaIngreso: elements.fechaIngreso.value,
    fechaNacimiento: elements.fechaNacimiento.value,
    sexo: elements.sexo.value,
    estadoCivil: elements.estadoCivil.value,
    telefono: elements.telefono.value,
    correo: elements.correo.value,
    inss: elements.inss.value,
    idBanco: elements.idBanco.value,
    numeroCuentaBancaria: elements.numeroCuentaBancaria.value,
    direccion: elements.direccion.value,
  });

  const readContractForm = () => ({
    contractId: elements.contractId.value,
    idEmpleado: elements.contractEmployeeId.value,
    numeroContrato: elements.numeroContrato.value,
    idTipoContrato: elements.idTipoContrato.value,
    idHorarioLaboral: elements.idHorarioLaboral.value,
    fechaInicio: elements.contractFechaInicio.value,
    fechaFin: elements.contractFechaFin.value,
    salarioBaseMensual: elements.salarioBaseMensual.value,
    moneda: elements.moneda.value,
    esContratoVigente: elements.esContratoVigente.checked,
    observacion: elements.observacion.value,
  });

  const readStructureForm = () => ({
    structureNodeId: elements.structureNodeId.value,
    codigoNodo: elements.structureCodigoNodo.value,
    tipoNodo: elements.structureTipoNodo.value,
    nombreNodo: elements.structureNombreNodo.value,
    idNodoPadre: elements.structureParentNodeId.value,
    idEmpleadoTitular: elements.structureEmployeeId.value,
    idDepartamento: elements.structureDepartmentId.value,
    idCargo: elements.structurePositionId.value,
    ordenVisual: elements.structureOrdenVisual.value,
    activo: elements.structureActivo.checked,
    observacion: elements.structureObservacion.value,
  });

  const readWorkflowForm = () => {
    const workflowElements = getWorkflowElements();
    const moduleId = elements.workflowForm.dataset.moduleId;

    const values = {
      recordId: elements.workflowRecordId.value,
      codigo: workflowElements.code?.value || "",
      nombre: workflowElements.name?.value || "",
      relatedId: workflowElements.related?.value || "",
      tipoAccion: workflowElements.typeText?.value || "",
      tipoDocumento: workflowElements.typeText?.value || "",
      idEmpleado: workflowElements.employee?.value || "",
      idCargoNuevo: workflowElements.related?.value || "",
      idTipoPermiso: workflowElements.type?.value || "",
      idTipoHoraExtra: workflowElements.type?.value || "",
      fechaAccion: workflowElements.startDate?.value || "",
      fechaDocumento: workflowElements.startDate?.value || "",
      fechaInicio: workflowElements.startDate?.value || "",
      numberValue1: workflowElements.numberValue1?.value || "",
      nuevoSalarioBaseMensual: workflowElements.numberValue1?.value || "",
      numberValue2: workflowElements.numberValue2?.value || "",
      fechaFin: workflowElements.endDate?.value || "",
      nuevaFechaFinContrato: workflowElements.endDate?.value || "",
      fechaVencimiento: workflowElements.endDate?.value || "",
      fechaHoraExtra: workflowElements.hoursDate?.value || "",
      cantidadHoras: workflowElements.hoursAmount?.value || "",
      integerValue1: workflowElements.integerValue1?.value || "",
      flagValue1: workflowElements.flagValue1?.checked || false,
      aplicarCambioOperativo: workflowElements.flagValue1?.checked || false,
      esMedioDia: workflowElements.halfDay?.checked || false,
      jornadaMedioDia: workflowElements.halfDayMorning?.checked
        ? "MANANA"
        : workflowElements.halfDayAfternoon?.checked
          ? "TARDE"
          : "",
      activo: workflowElements.active?.checked ?? true,
      observacion: workflowElements.observation?.value || "",
      observacionSolicitud: workflowElements.observation?.value || "",
      descripcion: workflowElements.observation?.value || "",
      descripcionAccion: workflowElements.observation?.value || "",
      archivo: workflowElements.file?.files?.[0] || null,
      removerArchivo: workflowElements.removeFile?.checked || false,
    };

    return values;
  };

  const readDeleteForm = () => ({
    adminUsuario: elements.adminUsuario.value.trim(),
    adminPassword: elements.adminPassword.value,
  });

  const readContractDeleteForm = () => ({
    adminUsuario: elements.contractAdminUsuario.value.trim(),
    adminPassword: elements.contractAdminPassword.value,
  });

  const readWorkflowResolveForm = () => ({
    action: elements.workflowResolveModal.dataset.action || "",
    recordId: elements.workflowResolveModal.dataset.recordId || "",
    approvedDays: elements.workflowApprovedDays.value,
    observation: elements.workflowResolutionObservation.value.trim(),
  });

  const readVacationBulkForm = () => ({
    fechaAjuste: elements.vacationBulkDate.value,
    cantidadDias: elements.vacationBulkAmountFull.checked ? "1" : "0.5",
    observacion: elements.vacationBulkObservation.value.trim(),
  });

  const readGenericDeleteForm = () => ({
    adminUsuario: elements.genericAdminUsuario.value.trim(),
    adminPassword: elements.genericAdminPassword.value,
  });

  const setSaveBusy = (busy, label) => {
    if (!busy) {
      resetSaveButton(elements.saveEmployeeButton, label);
      return;
    }

    elements.saveEmployeeButton.disabled = true;
    elements.saveEmployeeButton.classList.remove("is-success");
    elements.saveEmployeeButton.classList.add("is-loading");
    setSaveButtonState(elements.saveEmployeeButton, "Guardando...", "loading");
  };

  const setContractSaveBusy = (busy, label) => {
    if (!busy) {
      resetSaveButton(elements.saveContractButton, label);
      return;
    }

    elements.saveContractButton.disabled = true;
    elements.saveContractButton.classList.remove("is-success");
    elements.saveContractButton.classList.add("is-loading");
    setSaveButtonState(elements.saveContractButton, "Guardando...", "loading");
  };

  const setStructureSaveBusy = (busy, label) => {
    if (!busy) {
      resetSaveButton(elements.saveStructureButton, label);
      return;
    }

    elements.saveStructureButton.disabled = true;
    elements.saveStructureButton.classList.remove("is-success");
    elements.saveStructureButton.classList.add("is-loading");
    setSaveButtonState(elements.saveStructureButton, "Guardando...", "loading");
  };

  const setWorkflowSaveBusy = (busy, label) => {
    if (!busy) {
      resetSaveButton(elements.saveWorkflowButton, label);
      return;
    }

    elements.saveWorkflowButton.disabled = true;
    elements.saveWorkflowButton.classList.remove("is-success");
    elements.saveWorkflowButton.classList.add("is-loading");
    setSaveButtonState(elements.saveWorkflowButton, "Guardando...", "loading");
  };

  const showSaveSuccess = (label = "Guardado con exito") => {
    elements.saveEmployeeButton.disabled = true;
    elements.saveEmployeeButton.classList.remove("is-loading");
    elements.saveEmployeeButton.classList.add("is-success");
    setSaveButtonState(elements.saveEmployeeButton, label, "success");
  };

  const showContractSaveSuccess = (label = "Guardado con exito") => {
    elements.saveContractButton.disabled = true;
    elements.saveContractButton.classList.remove("is-loading");
    elements.saveContractButton.classList.add("is-success");
    setSaveButtonState(elements.saveContractButton, label, "success");
  };

  const showStructureSaveSuccess = (label = "Guardado con exito") => {
    elements.saveStructureButton.disabled = true;
    elements.saveStructureButton.classList.remove("is-loading");
    elements.saveStructureButton.classList.add("is-success");
    setSaveButtonState(elements.saveStructureButton, label, "success");
  };

  const showWorkflowSaveSuccess = (label = "Guardado con exito") => {
    elements.saveWorkflowButton.disabled = true;
    elements.saveWorkflowButton.classList.remove("is-loading");
    elements.saveWorkflowButton.classList.add("is-success");
    setSaveButtonState(elements.saveWorkflowButton, label, "success");
  };

  const setDeleteError = (message = "") => {
    elements.deleteError.textContent = message;
  };

  const setContractDeleteError = (message = "") => {
    elements.contractDeleteError.textContent = message;
  };

  const setWorkflowResolveError = (message = "") => {
    elements.workflowResolveError.textContent = message;
  };

  const setGenericDeleteError = (message = "") => {
    elements.genericDeleteError.textContent = message;
  };

  const setDeleteBusy = (busy) => {
    elements.confirmDeleteButton.disabled = busy;
    setButtonLabel(
      elements.confirmDeleteButton,
      busy ? "Eliminando..." : "Confirmar eliminacion",
      busy ? "refresh" : "trash",
      { rememberDefault: !busy, spin: busy },
    );
  };

  const setContractDeleteBusy = (busy) => {
    elements.confirmContractDeleteButton.disabled = busy;
    setButtonLabel(
      elements.confirmContractDeleteButton,
      busy ? "Eliminando..." : "Confirmar eliminacion",
      busy ? "refresh" : "trash",
      { rememberDefault: !busy, spin: busy },
    );
  };

  const setWorkflowResolveBusy = (busy, actionLabel) => {
    elements.confirmWorkflowResolveButton.disabled = busy;
    setButtonLabel(
      elements.confirmWorkflowResolveButton,
      busy ? "Procesando..." : actionLabel,
      busy ? "refresh" : elements.confirmWorkflowResolveButton.dataset.defaultIcon || "approve",
      { rememberDefault: !busy, spin: busy },
    );
  };

  const setVacationBulkError = (message = "") => {
    elements.vacationBulkError.textContent = message;
  };

  const setVacationBulkBusy = (busy) => {
    elements.confirmVacationBulkButton.disabled = busy;
    setButtonLabel(
      elements.confirmVacationBulkButton,
      busy ? "Aplicando..." : "Aplicar ajuste",
      busy ? "refresh" : "calendar",
      { rememberDefault: !busy, spin: busy },
    );
  };

  const setGenericDeleteBusy = (busy) => {
    elements.confirmGenericDeleteButton.disabled = busy;
    setButtonLabel(
      elements.confirmGenericDeleteButton,
      busy ? "Eliminando..." : "Confirmar eliminacion",
      busy ? "refresh" : "trash",
      { rememberDefault: !busy, spin: busy },
    );
  };

  const setEmployeeFormErrors = (errors = {}) => {
    setFormErrors(elements.employeeForm, employeeFieldMap, errors);
  };

  const clearEmployeeFieldError = (fieldName) => {
    clearFieldError(elements.employeeForm, employeeFieldMap, fieldName);
  };

  const focusEmployeeField = (fieldName) => {
    focusField(employeeFieldMap, fieldName);
  };

  const setContractFormErrors = (errors = {}) => {
    setFormErrors(elements.contractForm, contractFieldMap, errors);
  };

  const clearContractFieldError = (fieldName) => {
    clearFieldError(elements.contractForm, contractFieldMap, fieldName);
  };

  const focusContractField = (fieldName) => {
    focusField(contractFieldMap, fieldName);
  };

  const setStructureFormErrors = (errors = {}) => {
    setFormErrors(elements.structureForm, structureFieldMap, errors);
  };

  const clearStructureFieldError = (fieldName) => {
    clearFieldError(elements.structureForm, structureFieldMap, fieldName);
  };

  const focusStructureField = (fieldName) => {
    focusField(structureFieldMap, fieldName);
  };

  const setWorkflowFormErrors = (errors = {}) => {
    setFormErrors(elements.workflowForm, workflowFieldMap, errors);
  };

  const clearWorkflowFieldError = (fieldName) => {
    clearFieldError(elements.workflowForm, workflowFieldMap, fieldName);
  };

  const focusWorkflowField = (fieldName) => {
    focusField(workflowFieldMap, fieldName);
  };

  const showToast = (message, tone = "info") => {
    const toast = document.createElement("div");
    toast.className = `toast ${
      tone === "danger"
        ? "is-danger"
        : tone === "success"
          ? "is-success"
          : tone === "warning"
            ? "is-warning"
            : ""
    }`;
    toast.textContent = message;
    elements.toastRegion.appendChild(toast);

    window.setTimeout(() => {
      toast.remove();
    }, 3600);
  };

  applyStaticButtonDecorations();

  return {
    elements,
    setSession,
    renderMainNav,
    setWorkspaceHeader,
    renderGroupBoard,
    showBoard,
    showEmployeeShell,
    showContractShell,
    showWorkflowShell,
    showConfigShell,
    showClockShell,
    configureClockShell,
    showReportShell,
    showStructureShell,
    showAuditShell,
    renderConfigShell,
    renderPlaceholder,
    renderStatusOptions,
    renderContractStatusOptions,
    renderWorkflowStatusOptions,
    configureWorkflowShell,
    renderClockEmployeeOptions,
    renderReportDepartmentOptions,
    renderStructureDepartmentOptions,
    renderReportEmployeeStatusOptions,
    renderAuditProcessOptions,
    renderTableLoading,
    renderContractTableLoading,
    renderWorkflowTableLoading,
    renderClockTableLoading,
    renderReportTableLoading,
    renderAuditTableLoading,
    renderTable,
    renderContractTable,
    renderWorkflowTable,
    renderCatalogTable,
    renderActionTable,
    renderDocumentTable,
    renderClockDashboard,
    renderClockTable,
    renderReportTable,
    renderStructureFilters,
    renderStructureSummary,
    renderStructureTree,
    renderAuditTable,
    renderDetail,
    renderContractDetail,
    syncContractEmployeeHint: (employees, employeeId, mode) =>
      renderContractEmployeeHint(
        findEmployeeOption(employees, employeeId),
        mode || elements.contractForm?.dataset.mode || "create",
      ),
    renderWorkflowDetail,
    renderCatalogDetail,
    renderActionDetail,
    renderDocumentDetail,
    renderClockDetail,
    renderReportDetail,
    renderStructureDetail,
    renderAuditDetail,
    setEmployeeDetailVisibility,
    setWorkflowDetailVisibility,
    setActionState,
    setContractActionState,
    setWorkflowActionState,
    setClockActionState,
    setReportActionState,
    setStructureActionState,
    setAuditActionState,
    openEmployeeModal,
    closeEmployeeModal,
    openContractModal,
    closeContractModal,
    openStructureModal,
    closeStructureModal,
    openWorkflowModal,
    closeWorkflowModal,
    openDeleteModal,
    closeDeleteModal,
    openContractDeleteModal,
    closeContractDeleteModal,
    openGenericDeleteModal,
    closeGenericDeleteModal,
    openWorkflowResolveModal,
    closeWorkflowResolveModal,
    openVacationBulkModal,
    closeVacationBulkModal,
    forceCloseOverlays,
    readEmployeeForm,
    readContractForm,
    readStructureForm,
    readWorkflowForm,
    readDeleteForm,
    readContractDeleteForm,
    readGenericDeleteForm,
    readWorkflowResolveForm,
    readVacationBulkForm,
    setFormErrors: setEmployeeFormErrors,
    setEmployeeFormErrors,
    clearFieldError: clearEmployeeFieldError,
    clearEmployeeFieldError,
    focusField: focusEmployeeField,
    focusEmployeeField,
    setSaveBusy,
    showSaveSuccess,
    setContractFormErrors,
    clearContractFieldError,
    focusContractField,
    setContractSaveBusy,
    showContractSaveSuccess,
    setStructureFormErrors,
    clearStructureFieldError,
    focusStructureField,
    setStructureSaveBusy,
    showStructureSaveSuccess,
    setWorkflowFormErrors,
    clearWorkflowFieldError,
    focusWorkflowField,
    setWorkflowSaveBusy,
    showWorkflowSaveSuccess,
    syncActionWorkflowForm,
    setDeleteError,
    setDeleteBusy,
    setContractDeleteError,
    setContractDeleteBusy,
    setGenericDeleteError,
    setGenericDeleteBusy,
    setWorkflowResolveError,
    setWorkflowResolveBusy,
    setVacationBulkError,
    setVacationBulkBusy,
    showToast,
  };
})();

