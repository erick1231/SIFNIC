const sessionApi = window.SifnicSession;

const backToDashboard = document.getElementById("backToDashboard");
const refreshPortal = document.getElementById("refreshPortal");
const logoutButton = document.getElementById("logoutButton");

const sessionUser = document.getElementById("sessionUser");
const sessionMeta = document.getElementById("sessionMeta");
const portalTitle = document.getElementById("portalTitle");
const portalNote = document.getElementById("portalNote");
const portalEmptyState = document.getElementById("portalEmptyState");
const portalContent = document.getElementById("portalContent");
const portalTabs = Array.from(document.querySelectorAll("[data-portal-tab]"));
const portalSections = Array.from(document.querySelectorAll("[data-portal-section]"));
const requestTabs = Array.from(document.querySelectorAll("[data-request-tab]"));
const requestPanels = Array.from(document.querySelectorAll("[data-request-panel]"));

const metricVacationAvailable = document.getElementById("metricVacationAvailable");
const metricVacationMeta = document.getElementById("metricVacationMeta");
const metricWeekHours = document.getElementById("metricWeekHours");
const metricMonthHoursMeta = document.getElementById("metricMonthHoursMeta");
const metricPendingRequests = document.getElementById("metricPendingRequests");
const metricPendingMeta = document.getElementById("metricPendingMeta");
const metricContractType = document.getElementById("metricContractType");
const metricContractMeta = document.getElementById("metricContractMeta");

const employeeProfile = document.getElementById("employeeProfile");
const employeeSummary = document.getElementById("employeeSummary");
const profileAvatarImage = document.getElementById("profileAvatarImage");
const profileAvatarFallback = document.getElementById("profileAvatarFallback");
const profileAvatarName = document.getElementById("profileAvatarName");
const profileAvatarRole = document.getElementById("profileAvatarRole");
const profileAvatarSupervisor = document.getElementById("profileAvatarSupervisor");
const profilePhotoInput = document.getElementById("profilePhotoInput");
const changeProfilePhotoButton = document.getElementById("changeProfilePhotoButton");

const vacationForm = document.getElementById("vacationForm");
const vacationStartDate = document.getElementById("vacationStartDate");
const vacationEndDate = document.getElementById("vacationEndDate");
const vacationHalfDay = document.getElementById("vacationHalfDay");
const vacationHalfDayShiftRow = document.getElementById("vacationHalfDayShiftRow");
const vacationHalfDayShiftOptions = Array.from(
  document.querySelectorAll('input[name="vacationHalfDayShift"]'),
);
const vacationObservation = document.getElementById("vacationObservation");
const clearVacationForm = document.getElementById("clearVacationForm");
const submitVacationButton = document.getElementById("submitVacationButton");

const overtimeForm = document.getElementById("overtimeForm");
const overtimeDate = document.getElementById("overtimeDate");
const overtimeType = document.getElementById("overtimeType");
const overtimeStartTime = document.getElementById("overtimeStartTime");
const overtimeEndTime = document.getElementById("overtimeEndTime");
const overtimeObservation = document.getElementById("overtimeObservation");
const overtimeComputedHours = document.getElementById("overtimeComputedHours");
const clearOvertimeForm = document.getElementById("clearOvertimeForm");
const submitOvertimeButton = document.getElementById("submitOvertimeButton");

const vacationsTableBody = document.getElementById("vacationsTableBody");
const vacationsEmptyState = document.getElementById("vacationsEmptyState");
const overtimeTableBody = document.getElementById("overtimeTableBody");
const overtimeEmptyState = document.getElementById("overtimeEmptyState");
const recordsVacationAvailable = document.getElementById("recordsVacationAvailable");
const recordsVacationTaken = document.getElementById("recordsVacationTaken");
const recordsVacationPending = document.getElementById("recordsVacationPending");
const recordsOvertimePending = document.getElementById("recordsOvertimePending");
const payslipsTableBody = document.getElementById("payslipsTableBody");
const payslipsEmptyState = document.getElementById("payslipsEmptyState");
const toastRegion = document.getElementById("toastRegion");

const state = {
  session: null,
  context: null,
  activePortalSection: "ficha",
  activeRequestSection: "vacaciones",
  records: {
    vacations: [],
    overtime: [],
    payslips: [],
  },
  editing: {
    vacationId: null,
    overtimeId: null,
  },
};

const formatDate = (value) => {
  if (!value) {
    return "Sin registro";
  }

  try {
    return new Intl.DateTimeFormat("es-NI", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      timeZone: "America/Managua",
    }).format(new Date(`${value}T00:00:00`));
  } catch {
    return value;
  }
};

const formatDecimal = (value, suffix = "") => {
  const numeric = Number(value || 0);
  return `${numeric.toFixed(2)}${suffix}`;
};

const formatCurrency = (value, currencyCode = "NIO") => {
  const numeric = Number(value || 0);

  try {
    return new Intl.NumberFormat("es-NI", {
      style: "currency",
      currency: currencyCode || "NIO",
      maximumFractionDigits: 2,
    }).format(numeric);
  } catch {
    return `${currencyCode || "NIO"} ${numeric.toFixed(2)}`;
  }
};

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

const getStatusClass = (value) => {
  const normalized = String(value || "").toUpperCase();

  if (["APROBADA", "APROBADO"].includes(normalized)) {
    return "is-success";
  }

  if (["RECHAZADA", "RECHAZADO"].includes(normalized)) {
    return "is-danger";
  }

  return "is-warning";
};

const isEditableVacation = (item) =>
  String(item?.estadoVacacion || "").toUpperCase() === "SOLICITADA";

const isEditableOvertime = (item) =>
  String(item?.estadoHoraExtra || "").toUpperCase() === "REGISTRADA";

const showToast = (message, type = "success") => {
  const toast = document.createElement("div");
  toast.className = `toast is-${type}`;
  toast.textContent = message;
  toastRegion.appendChild(toast);

  window.setTimeout(() => {
    toast.remove();
  }, 3200);
};

const requestWithOperator = async (url, options = {}) => {
  const username = state.session?.username || state.session?.user || "sistema.local";

  return sessionApi.request(url, {
    ...options,
    headers: {
      "X-Operator-User": username,
      ...(options.headers || {}),
    },
  });
};

const openUrlWithSession = (url) => {
  const token = state.session?.sessionToken || sessionApi.getSession()?.sessionToken || "";
  const separator = url.includes("?") ? "&" : "?";
  const finalUrl = token ? `${url}${separator}sessionToken=${encodeURIComponent(token)}` : url;
  window.open(finalUrl, "_blank", "noopener");
};

const renderDetailItemMarkup = (item) => `
  <article class="detail-item">
    <span>${item.label}</span>
    <strong>${item.value || "Sin registro"}</strong>
  </article>
`;

const renderDetailItems = (container, items) => {
  container.innerHTML = items.map(renderDetailItemMarkup).join("");
};

const renderProfileSections = (container, sections) => {
  container.innerHTML = sections
    .map(
      (section) => `
        <section class="profile-section-card">
          <header class="profile-section-head">
            <span class="eyebrow">${section.eyebrow}</span>
            <h4>${section.title}</h4>
          </header>
          <div class="detail-grid">
            ${section.items.map(renderDetailItemMarkup).join("")}
          </div>
        </section>
      `,
    )
    .join("");
};

const setPortalSection = (sectionKey) => {
  state.activePortalSection = sectionKey;

  portalTabs.forEach((button) => {
    const isActive = button.dataset.portalTab === sectionKey;
    button.classList.toggle("is-active", isActive);
    button.setAttribute("aria-selected", String(isActive));
  });

  portalSections.forEach((section) => {
    section.hidden = section.dataset.portalSection !== sectionKey;
  });
};

const setRequestSection = (sectionKey) => {
  state.activeRequestSection = sectionKey;

  requestTabs.forEach((button) => {
    const isActive = button.dataset.requestTab === sectionKey;
    button.classList.toggle("is-active", isActive);
    button.setAttribute("aria-selected", String(isActive));
  });

  requestPanels.forEach((panel) => {
    panel.hidden = panel.dataset.requestPanel !== sectionKey;
  });
};

const renderContext = (context) => {
  state.context = context;

  sessionUser.textContent =
    context.session?.displayName || context.session?.username || "Usuario";
  sessionMeta.textContent =
    context.session?.rolesLabel || context.session?.username || "Sesion activa";

  if (!context.hasEmployee) {
    portalTitle.textContent = "Tu usuario aun no esta vinculado";
    portalNote.textContent =
      context.message || "Hace falta enlazar tu usuario con una ficha de empleado en RRHH.";
    portalEmptyState.hidden = false;
    portalContent.hidden = true;
    return;
  }

  portalEmptyState.hidden = true;
  portalContent.hidden = false;

  const employee = context.employee;
  const vacationBalance = context.vacationBalance;
  const summary = context.summary;
  const hasFormalStructure = Boolean(employee.idNodoEstructura);
  const portalMeta = [
    employee.codigoEmpleado,
    employee.departamento,
    employee.cargo,
    employee.jefeInmediato ? `Jefe inmediato: ${employee.jefeInmediato}` : "",
    employee.reportaFormalmenteA ? `Reporta formalmente a ${employee.reportaFormalmenteA}` : "",
  ].filter(Boolean);

  portalTitle.textContent = employee.nombreEmpleado;
  portalNote.textContent = portalMeta.join(" | ");

  const hasPhoto = Boolean(employee.fotoPerfilUrl);
  if (profileAvatarImage) {
    profileAvatarImage.hidden = !hasPhoto;
    profileAvatarImage.src = hasPhoto ? employee.fotoPerfilUrl : "";
  }

  if (profileAvatarFallback) {
    profileAvatarFallback.hidden = hasPhoto;
    profileAvatarFallback.textContent = getInitials(employee.nombreEmpleado);
  }

  if (profileAvatarName) {
    profileAvatarName.textContent = employee.nombreEmpleado || "Colaborador";
  }

  if (profileAvatarRole) {
    profileAvatarRole.textContent = employee.cargo || "Sin cargo asignado";
  }

  if (profileAvatarSupervisor) {
    profileAvatarSupervisor.textContent = employee.reportaFormalmenteA
      ? `Ubicacion formal: ${employee.nombreNodoEstructura || "Nodo institucional"}`
      : employee.jefeInmediato
        ? `Bajo supervision de ${employee.jefeInmediato}`
        : "Sin supervision asignada";
  }

  metricVacationAvailable.textContent = `${formatDecimal(vacationBalance.diasDisponibles)} d`;
  metricVacationMeta.textContent =
    `${formatDecimal(vacationBalance.diasAcumulados)} acumulados | ${formatDecimal(vacationBalance.diasConsumidos || 0)} consumidos`;
  metricWeekHours.textContent = `${formatDecimal(summary.horasSemana)} h`;
  metricMonthHoursMeta.textContent = `${formatDecimal(summary.horasMes)} h este mes`;

  const pendingTotal = Number(summary.vacacionesPendientes || 0) + Number(summary.horasExtraPendientes || 0);
  metricPendingRequests.textContent = String(pendingTotal);
  metricPendingMeta.textContent =
    `${summary.vacacionesPendientes || 0} vacaciones / ${summary.horasExtraPendientes || 0} horas`;
  metricContractType.textContent = employee.contratoTipo || "Sin contrato";
  metricContractMeta.textContent = employee.contratoNumero
    ? `${employee.contratoNumero} | ${formatCurrency(employee.salarioBase, employee.moneda)}`
    : "Sin contrato vigente";

  renderProfileSections(employeeProfile, [
    {
      eyebrow: "Identidad",
      title: "Datos personales",
      items: [
        { label: "Codigo", value: employee.codigoEmpleado },
        { label: "Cedula", value: employee.cedula },
        { label: "Nacimiento", value: formatDate(employee.fechaNacimiento) },
        { label: "Estado", value: employee.estado },
      ],
    },
    {
      eyebrow: "Contacto",
      title: "Canales del colaborador",
      items: [
        { label: "Correo", value: employee.correo },
        { label: "Telefono", value: employee.telefono },
        { label: "Direccion", value: employee.direccion },
      ],
    },
    {
      eyebrow: "Laboral",
      title: "Relacion con la empresa",
      items: [
        { label: "Ingreso", value: formatDate(employee.fechaIngreso) },
        { label: "Departamento", value: employee.departamento },
        { label: "Cargo", value: employee.cargo },
        {
          label: "Jefe inmediato",
          value: employee.jefeInmediato
            ? `${employee.jefeInmediato}${employee.codigoSupervisor ? ` (${employee.codigoSupervisor})` : ""}`
            : "Sin asignar",
        },
        { label: "Contrato", value: employee.contratoNumero || "Sin contrato" },
        { label: "Tipo de contrato", value: employee.contratoTipo || "Sin contrato" },
      ],
    },
    {
      eyebrow: "Organigrama",
      title: "Ubicacion formal",
      items: [
        { label: "Nodo actual", value: employee.nombreNodoEstructura || "Sin nodo formal asignado" },
        { label: "Tipo de nodo", value: employee.tipoNodoEstructuraLabel || "-" },
        { label: "Reporta formalmente a", value: employee.reportaFormalmenteA || "Sin nodo padre" },
        { label: "Departamento formal", value: employee.departamentoFormal || employee.departamento || "-" },
        { label: "Cargo formal", value: employee.cargoFormal || employee.cargo || "-" },
        { label: "Ruta organizativa", value: employee.rutaOrganizativa || "No disponible" },
      ],
    },
  ]);

  renderDetailItems(employeeSummary, [
    { label: "Vacaciones acumuladas", value: `${formatDecimal(vacationBalance.diasAcumulados)} dias` },
    {
      label: "Vacaciones consumidas",
      value: `${formatDecimal(vacationBalance.diasConsumidos || 0)} dias`,
    },
    { label: "Vacaciones pendientes", value: `${formatDecimal(vacationBalance.diasPendientes || 0)} dias` },
    { label: "Horas extra semana", value: `${formatDecimal(summary.horasSemana)} h` },
    { label: "Horas extra mes", value: `${formatDecimal(summary.horasMes)} h` },
    { label: "Total horas extra", value: String(summary.totalHorasExtra || 0) },
    { label: "Nodo formal", value: hasFormalStructure ? employee.nombreNodoEstructura : "Sin asignar" },
    { label: "Dependencia formal", value: employee.reportaFormalmenteA || "Sin dependencia formal" },
  ]);

  overtimeType.innerHTML = ['<option value="">Selecciona tipo</option>', ...(context.overtimeTypes || []).map(
    (item) => `<option value="${item.id}">${item.name}</option>`,
  )].join("");
};

const buildObservationDetail = (primary, secondary) => {
  const values = [primary, secondary].filter(Boolean);
  return values.length ? values.join(" | ") : "Sin observacion";
};

const buildTraceMarkup = (entries) => {
  const rows = (entries || [])
    .filter((entry) => entry?.value)
    .map(
      (entry) => `
        <div class="table-trace-line">
          <span>${entry.label}</span>
          <strong>${entry.value}</strong>
        </div>
      `,
    )
    .join("");

  if (!rows) {
    return '<span class="table-subtext">Sin detalle adicional</span>';
  }

  return `<div class="table-trace-stack">${rows}</div>`;
};

const renderTable = (target, emptyTarget, rows, mapper) => {
  if (!rows.length) {
    target.innerHTML = "";
    emptyTarget.hidden = false;
    return;
  }

  emptyTarget.hidden = true;
  target.innerHTML = rows.map(mapper).join("");
};

const renderRecords = () => {
  renderTable(vacationsTableBody, vacationsEmptyState, state.records.vacations, (item) => `
      <tr>
        <td>${formatDate(item.fechaInicio)} al ${formatDate(item.fechaFin)}</td>
        <td>${formatDecimal(item.diasAprobados ?? item.diasSolicitados)}${item.esMedioDia ? " (medio dia)" : ""}</td>
        <td><span class="status-pill ${getStatusClass(item.estadoVacacion)}">${item.estadoVacacion}</span></td>
        <td>${buildTraceMarkup([
          item.jornadaMedioDia
            ? { label: "Modalidad", value: item.jornadaMedioDia }
            : null,
          item.observacionSolicitud
            ? { label: "Solicitud", value: item.observacionSolicitud }
            : null,
          item.observacionAprobacion
            ? { label: "Resolucion", value: item.observacionAprobacion }
            : null,
          item.usuarioAprueba || item.fechaAprobacion
            ? {
                label: "Atendida por",
                value: [item.usuarioAprueba, item.fechaAprobacion].filter(Boolean).join(" | "),
              }
            : null,
        ])}</td>
        <td>
          ${
            isEditableVacation(item)
              ? `
                <div class="table-action-row">
                  <button class="ghost-button ghost-button-compact" type="button" data-action="edit-vacation" data-id="${item.idVacacion}">
                    Editar
                  </button>
                  <button class="ghost-button ghost-button-compact is-danger" type="button" data-action="delete-vacation" data-id="${item.idVacacion}">
                    Retirar
                  </button>
                </div>
              `
              : '<span class="table-subtext">Solo lectura</span>'
          }
        </td>
      </tr>
    `);

  renderTable(overtimeTableBody, overtimeEmptyState, state.records.overtime, (item) => `
      <tr>
        <td>${formatDate(item.fechaHoraExtra)}</td>
        <td>${formatDecimal(item.cantidadHoras)} h</td>
        <td><span class="status-pill ${getStatusClass(item.estadoHoraExtra)}">${item.estadoHoraExtra}</span></td>
        <td>${buildTraceMarkup([
          { label: "Tipo", value: item.nombreTipoHoraExtra },
          item.observacion
            ? { label: "Detalle", value: item.observacion }
            : null,
          item.usuarioAprueba || item.fechaAprobacion
            ? {
                label: "Atendida por",
                value: [item.usuarioAprueba, item.fechaAprobacion].filter(Boolean).join(" | "),
              }
            : null,
        ])}</td>
        <td>
          ${
            isEditableOvertime(item)
              ? `
                <div class="table-action-row">
                  <button class="ghost-button ghost-button-compact" type="button" data-action="edit-overtime" data-id="${item.idHoraExtra}">
                    Editar
                  </button>
                  <button class="ghost-button ghost-button-compact is-danger" type="button" data-action="delete-overtime" data-id="${item.idHoraExtra}">
                    Retirar
                  </button>
                </div>
              `
              : '<span class="table-subtext">Solo lectura</span>'
          }
        </td>
      </tr>
    `);

  renderTable(payslipsTableBody, payslipsEmptyState, state.records.payslips, (item) => `
      <tr>
        <td>${item.codigoPeriodo}<br /><span class="table-subtext">${formatDate(item.fechaDesde)} al ${formatDate(item.fechaHasta)}</span></td>
        <td>${formatDate(item.fechaPago)}</td>
        <td>${formatCurrency(item.netoPagar)}</td>
        <td>${sessionApi.formatDateTime(item.fechaGeneracion)}</td>
        <td>
          <button
            class="ghost-button ghost-button-compact"
            type="button"
            data-action="open-payslip"
            data-detail-id="${item.idNominaDetalle}">
            Ver esquela
          </button>
        </td>
      </tr>
    `);

  const vacationContext = state.context?.vacationBalance || {};
  const summary = state.context?.summary || {};
  if (recordsVacationAvailable) {
    recordsVacationAvailable.textContent = `${formatDecimal(vacationContext.diasDisponibles)} dias`;
  }
  if (recordsVacationTaken) {
    recordsVacationTaken.textContent = `${formatDecimal(vacationContext.diasConsumidos || 0)} dias`;
  }
  if (recordsVacationPending) {
    recordsVacationPending.textContent = `${formatDecimal(vacationContext.diasPendientes || 0)} dias`;
  }
  if (recordsOvertimePending) {
    recordsOvertimePending.textContent = String(summary.horasExtraPendientes || 0);
  }
};

const loadRecords = async () => {
  if (!state.context?.hasEmployee) {
    return;
  }

  const employeeCode = state.context.employee.codigoEmpleado;

  const [vacationsPayload, overtimePayload, payslipsPayload] = await Promise.all([
    requestWithOperator(`/Novedades/ListarVacaciones?search=${encodeURIComponent(employeeCode)}&status=TODOS`),
    requestWithOperator(`/Novedades/ListarHorasExtra?search=${encodeURIComponent(employeeCode)}&status=TODOS`),
    requestWithOperator("/Portal/MisEsquelas"),
  ]);

  state.records.vacations = vacationsPayload?.data || [];
  state.records.overtime = overtimePayload?.data || [];
  state.records.payslips = payslipsPayload?.data || [];
  renderRecords();
};

const clearVacationFields = () => {
  vacationStartDate.value = "";
  vacationEndDate.value = "";
  if (vacationHalfDay) {
    vacationHalfDay.checked = false;
  }
  vacationHalfDayShiftOptions.forEach((option) => {
    option.checked = false;
  });
  vacationObservation.value = "";
  syncVacationHalfDayState();
};

const applyVacationFormMode = () => {
  const isEditing = Number(state.editing.vacationId || 0) > 0;
  if (submitVacationButton) {
    submitVacationButton.textContent = isEditing ? "Actualizar vacacion" : "Guardar vacacion";
  }
  if (clearVacationForm) {
    clearVacationForm.textContent = isEditing ? "Cancelar edicion" : "Limpiar";
  }
};

const getSelectedVacationHalfDayShift = () => {
  const selected = vacationHalfDayShiftOptions.find((option) => option.checked);
  return selected?.value || "";
};

const syncVacationHalfDayChoiceStyles = () => {
  vacationHalfDay?.closest(".toggle-card")?.classList.toggle("is-active", Boolean(vacationHalfDay?.checked));

  vacationHalfDayShiftOptions.forEach((option) => {
    option.closest(".choice-pill")?.classList.toggle("is-active", option.checked);
  });
};

const syncVacationHalfDayState = () => {
  const isHalfDay = Boolean(vacationHalfDay?.checked);
  if (vacationHalfDayShiftRow) {
    vacationHalfDayShiftRow.hidden = !isHalfDay;
  }
  vacationEndDate.disabled = isHalfDay;

  if (isHalfDay) {
    vacationEndDate.value = vacationStartDate.value;
    if (!getSelectedVacationHalfDayShift() && vacationHalfDayShiftOptions[0]) {
      vacationHalfDayShiftOptions[0].checked = true;
    }
  } else {
    vacationHalfDayShiftOptions.forEach((option) => {
      option.checked = false;
    });
  }

  syncVacationHalfDayChoiceStyles();
};

const updateComputedHours = () => {
  const start = overtimeStartTime.value;
  const end = overtimeEndTime.value;

  if (!start || !end) {
    overtimeComputedHours.textContent = "Horas calculadas: 0.00";
    return 0;
  }

  const [startHour, startMinute] = start.split(":").map(Number);
  const [endHour, endMinute] = end.split(":").map(Number);
  const totalStart = startHour * 60 + startMinute;
  const totalEnd = endHour * 60 + endMinute;

  if (!Number.isFinite(totalStart) || !Number.isFinite(totalEnd) || totalEnd <= totalStart) {
    overtimeComputedHours.textContent = "Horas calculadas: horario invalido";
    return null;
  }

  const hours = (totalEnd - totalStart) / 60;
  overtimeComputedHours.textContent = `Horas calculadas: ${formatDecimal(hours)}`;
  return hours;
};

const clearOvertimeFields = () => {
  overtimeDate.value = "";
  overtimeType.value = "";
  overtimeStartTime.value = "";
  overtimeEndTime.value = "";
  overtimeObservation.value = "";
  overtimeComputedHours.textContent = "Horas calculadas: 0.00";
};

const applyOvertimeFormMode = () => {
  const isEditing = Number(state.editing.overtimeId || 0) > 0;
  if (submitOvertimeButton) {
    submitOvertimeButton.textContent = isEditing ? "Actualizar hora extra" : "Guardar hora extra";
  }
  if (clearOvertimeForm) {
    clearOvertimeForm.textContent = isEditing ? "Cancelar edicion" : "Limpiar";
  }
};

const startVacationEdit = (record) => {
  if (!record) {
    return;
  }

  state.editing.vacationId = Number(record.idVacacion || 0);
  setPortalSection("solicitudes");
  setRequestSection("vacaciones");
  vacationStartDate.value = record.fechaInicio || "";
  vacationEndDate.value = record.fechaFin || "";
  if (vacationHalfDay) {
    vacationHalfDay.checked = Boolean(record.esMedioDia);
  }
  vacationHalfDayShiftOptions.forEach((option) => {
    option.checked = String(option.value || "").toUpperCase() === String(record.jornadaMedioDia || "").toUpperCase();
  });
  vacationObservation.value = record.observacionSolicitud || "";
  syncVacationHalfDayState();
  applyVacationFormMode();
};

const startOvertimeEdit = (record) => {
  if (!record) {
    return;
  }

  state.editing.overtimeId = Number(record.idHoraExtra || 0);
  setPortalSection("solicitudes");
  setRequestSection("horas-extra");
  overtimeDate.value = record.fechaHoraExtra || "";
  overtimeType.value = String(record.idTipoHoraExtra || "");

  const totalMinutes = Math.max(0, Math.round(Number(record.cantidadHoras || 0) * 60));
  overtimeStartTime.value = "18:00";
  const endMinutes = 18 * 60 + totalMinutes;
  const endHour = Math.floor(endMinutes / 60);
  const endMinute = endMinutes % 60;
  overtimeEndTime.value = `${String(endHour).padStart(2, "0")}:${String(endMinute).padStart(2, "0")}`;
  overtimeObservation.value = record.observacion || "";
  updateComputedHours();
  applyOvertimeFormMode();
};

const resetVacationEdit = () => {
  state.editing.vacationId = null;
  clearVacationFields();
  applyVacationFormMode();
};

const resetOvertimeEdit = () => {
  state.editing.overtimeId = null;
  clearOvertimeFields();
  applyOvertimeFormMode();
};

const loadPortal = async ({ notify = false } = {}) => {
  const payload = await sessionApi.request("/Portal/MiContexto");
  renderContext(payload.data);
  await loadRecords();

  if (notify) {
    showToast("Portal actualizado.", "success");
  }
};

const uploadProfilePhoto = async (file) => {
  if (!file) {
    return;
  }

  const formData = new FormData();
  formData.append("archivo", file);

  await requestWithOperator("/Portal/SubirMiFotoPerfil", {
    method: "POST",
    body: formData,
  });

  await loadPortal();
  showToast("Foto de perfil actualizada.", "success");
};

const submitVacation = async (event) => {
  event.preventDefault();

  if (!state.context?.hasEmployee) {
    showToast("Tu usuario no tiene ficha de empleado vinculada.", "danger");
    return;
  }

  const isHalfDay = Boolean(vacationHalfDay?.checked);
  if (!vacationStartDate.value || !vacationEndDate.value) {
    showToast("Selecciona la fecha inicio y la fecha fin.", "danger");
    return;
  }

  if (isHalfDay && !getSelectedVacationHalfDayShift()) {
    showToast("Selecciona si el medio dia corresponde a manana o tarde.", "danger");
    return;
  }

  const isEditing = Number(state.editing.vacationId || 0) > 0;
  const vacationUrl = isEditing
    ? `/Portal/ActualizarMiVacacion/${state.editing.vacationId}`
    : "/Novedades/CrearVacacion";

  await requestWithOperator(vacationUrl, {
    method: isEditing ? "PUT" : "POST",
    body: JSON.stringify({
      idEmpleado: state.context.employee.idEmpleado,
      fechaInicio: vacationStartDate.value,
      fechaFin: isHalfDay ? vacationStartDate.value : vacationEndDate.value,
      observacionSolicitud: vacationObservation.value.trim(),
      esMedioDia: isHalfDay,
      jornadaMedioDia: isHalfDay ? getSelectedVacationHalfDayShift() : null,
    }),
  });

  resetVacationEdit();
  await loadPortal();
  setPortalSection("registros");
  showToast(isEditing ? "Vacacion actualizada correctamente." : "Vacacion registrada correctamente.", "success");
};

const submitOvertime = async (event) => {
  event.preventDefault();

  if (!state.context?.hasEmployee) {
    showToast("Tu usuario no tiene ficha de empleado vinculada.", "danger");
    return;
  }

  const hours = updateComputedHours();
  if (!overtimeDate.value || !overtimeType.value || !overtimeStartTime.value || !overtimeEndTime.value) {
    showToast("Completa la fecha, el tipo y el horario.", "danger");
    return;
  }

  if (!(hours > 0)) {
    showToast("El horario de la hora extra no es valido.", "danger");
    return;
  }

  const isEditing = Number(state.editing.overtimeId || 0) > 0;
  const overtimeUrl = isEditing
    ? `/Portal/ActualizarMiHoraExtra/${state.editing.overtimeId}`
    : "/Novedades/CrearHoraExtra";

  await requestWithOperator(overtimeUrl, {
    method: isEditing ? "PUT" : "POST",
    body: JSON.stringify({
      idEmpleado: state.context.employee.idEmpleado,
      idTipoHoraExtra: Number(overtimeType.value),
      fechaHoraExtra: overtimeDate.value,
      cantidadHoras: Number(hours.toFixed(2)),
      observacion: overtimeObservation.value.trim(),
    }),
  });

  resetOvertimeEdit();
  await loadPortal();
  setPortalSection("registros");
  showToast(isEditing ? "Hora extra actualizada correctamente." : "Hora extra registrada correctamente.", "success");
};

const boot = async () => {
  state.session = sessionApi.getSession();

  if (!state.session) {
    window.location.href = "/App/Login";
    return;
  }

  sessionUser.textContent = state.session.displayName || state.session.user || "Usuario";
  sessionMeta.textContent = state.session.rolesLabel || state.session.username || "Sesion activa";

  setPortalSection(state.activePortalSection);
  setRequestSection(state.activeRequestSection);
  applyVacationFormMode();
  applyOvertimeFormMode();
  await loadPortal();
};

backToDashboard?.addEventListener("click", () => {
  window.location.href = "/App/Dashboard";
});

refreshPortal?.addEventListener("click", async () => {
  try {
    refreshPortal.disabled = true;
    await loadPortal({ notify: true });
  } catch (error) {
    showToast(error.message || "No se pudo actualizar el portal.", "danger");
  } finally {
    refreshPortal.disabled = false;
  }
});

logoutButton?.addEventListener("click", async () => {
  logoutButton.disabled = true;

  try {
    await sessionApi.logout();
  } finally {
    window.location.href = "/App/Login";
  }
});

clearVacationForm?.addEventListener("click", resetVacationEdit);
clearOvertimeForm?.addEventListener("click", resetOvertimeEdit);

changeProfilePhotoButton?.addEventListener("click", () => {
  profilePhotoInput?.click();
});

profilePhotoInput?.addEventListener("change", async (event) => {
  const [file] = Array.from(event.target.files || []);
  if (!file) {
    return;
  }

  try {
    if (changeProfilePhotoButton) {
      changeProfilePhotoButton.disabled = true;
    }
    await uploadProfilePhoto(file);
  } catch (error) {
    showToast(error.message || "No se pudo actualizar la foto de perfil.", "danger");
  } finally {
    event.target.value = "";
    if (changeProfilePhotoButton) {
      changeProfilePhotoButton.disabled = false;
    }
  }
});

vacationForm?.addEventListener("submit", async (event) => {
  try {
    await submitVacation(event);
  } catch (error) {
    showToast(error.message || "No se pudo guardar la vacacion.", "danger");
  }
});

overtimeForm?.addEventListener("submit", async (event) => {
  try {
    await submitOvertime(event);
  } catch (error) {
    showToast(error.message || "No se pudo guardar la hora extra.", "danger");
  }
});

[overtimeStartTime, overtimeEndTime].forEach((input) => {
  input?.addEventListener("input", updateComputedHours);
});

vacationHalfDay?.addEventListener("change", syncVacationHalfDayState);
vacationHalfDayShiftOptions.forEach((option) => {
  option.addEventListener("change", syncVacationHalfDayChoiceStyles);
});

vacationStartDate?.addEventListener("change", () => {
  if (vacationHalfDay?.checked) {
    vacationEndDate.value = vacationStartDate.value;
  }
});

portalTabs.forEach((button) => {
  button.addEventListener("click", () => {
    setPortalSection(button.dataset.portalTab || "ficha");
  });
});

requestTabs.forEach((button) => {
  button.addEventListener("click", () => {
    setRequestSection(button.dataset.requestTab || "vacaciones");
  });
});

payslipsTableBody?.addEventListener("click", (event) => {
  const button = event.target.closest('[data-action="open-payslip"]');
  if (!button) {
    return;
  }

  const detailId = Number(button.dataset.detailId || 0);
  if (!(detailId > 0)) {
    showToast("No se encontro la esquela solicitada.", "danger");
    return;
  }

  openUrlWithSession(`/Nomina/EsquelaHtml?idNominaDetalle=${detailId}`);
});

vacationsTableBody?.addEventListener("click", async (event) => {
  const editButton = event.target.closest('[data-action="edit-vacation"]');
  if (editButton) {
    const record = state.records.vacations.find(
      (item) => Number(item.idVacacion) === Number(editButton.dataset.id || 0),
    );
    startVacationEdit(record);
    return;
  }

  const deleteButton = event.target.closest('[data-action="delete-vacation"]');
  if (!deleteButton) {
    return;
  }

  const record = state.records.vacations.find(
    (item) => Number(item.idVacacion) === Number(deleteButton.dataset.id || 0),
  );
  if (!record) {
    return;
  }

  if (!window.confirm("Esta seguro de retirar esta solicitud de vacacion?")) {
    return;
  }

  try {
    await requestWithOperator(`/Portal/EliminarMiVacacion/${record.idVacacion}`, {
      method: "DELETE",
    });
    if (Number(state.editing.vacationId || 0) === Number(record.idVacacion)) {
      resetVacationEdit();
    }
    await loadPortal();
    showToast("Vacacion retirada correctamente.", "success");
  } catch (error) {
    showToast(error.message || "No se pudo retirar la vacacion.", "danger");
  }
});

overtimeTableBody?.addEventListener("click", async (event) => {
  const editButton = event.target.closest('[data-action="edit-overtime"]');
  if (editButton) {
    const record = state.records.overtime.find(
      (item) => Number(item.idHoraExtra) === Number(editButton.dataset.id || 0),
    );
    startOvertimeEdit(record);
    return;
  }

  const deleteButton = event.target.closest('[data-action="delete-overtime"]');
  if (!deleteButton) {
    return;
  }

  const record = state.records.overtime.find(
    (item) => Number(item.idHoraExtra) === Number(deleteButton.dataset.id || 0),
  );
  if (!record) {
    return;
  }

  if (!window.confirm("Esta seguro de retirar esta solicitud de hora extra?")) {
    return;
  }

  try {
    await requestWithOperator(`/Portal/EliminarMiHoraExtra/${record.idHoraExtra}`, {
      method: "DELETE",
    });
    if (Number(state.editing.overtimeId || 0) === Number(record.idHoraExtra)) {
      resetOvertimeEdit();
    }
    await loadPortal();
    showToast("Hora extra retirada correctamente.", "success");
  } catch (error) {
    showToast(error.message || "No se pudo retirar la hora extra.", "danger");
  }
});

boot().catch((error) => {
  showToast(error.message || "No se pudo cargar Mi Portal.", "danger");
});
