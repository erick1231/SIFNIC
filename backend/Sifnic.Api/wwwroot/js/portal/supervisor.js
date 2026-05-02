const sessionApi = window.SifnicSession;

const backToDashboard = document.getElementById("backToDashboard");
const refreshSupervisor = document.getElementById("refreshSupervisor");
const logoutButton = document.getElementById("logoutButton");

const sessionUser = document.getElementById("sessionUser");
const sessionMeta = document.getElementById("sessionMeta");
const supervisorTitle = document.getElementById("supervisorTitle");
const supervisorNote = document.getElementById("supervisorNote");
const scopeNote = document.getElementById("scopeNote");

const metricPendingVacations = document.getElementById("metricPendingVacations");
const metricPendingOvertime = document.getElementById("metricPendingOvertime");
const metricPendingVisible = document.getElementById("metricPendingVisible");

const supervisorFilterRow = document.getElementById("supervisorFilterRow");
const pendingRecordList = document.getElementById("pendingRecordList");
const pendingRecordEmptyState = document.getElementById("pendingRecordEmptyState");
const detailTitle = document.getElementById("detailTitle");
const detailGrid = document.getElementById("detailGrid");
const resolutionForm = document.getElementById("resolutionForm");
const approvedDaysField = document.getElementById("approvedDaysField");
const approvedDaysInput = document.getElementById("approvedDaysInput");
const resolutionComment = document.getElementById("resolutionComment");
const rejectButton = document.getElementById("rejectButton");
const approveButton = document.getElementById("approveButton");
const toastRegion = document.getElementById("toastRegion");
const resolutionFeedbackModal = document.getElementById("resolutionFeedbackModal");
const resolutionFeedbackIcon = document.getElementById("resolutionFeedbackIcon");
const resolutionFeedbackKicker = document.getElementById("resolutionFeedbackKicker");
const resolutionFeedbackTitle = document.getElementById("resolutionFeedbackTitle");
const resolutionFeedbackText = document.getElementById("resolutionFeedbackText");
const resolutionFeedbackCommentCard = document.getElementById("resolutionFeedbackCommentCard");
const resolutionFeedbackComment = document.getElementById("resolutionFeedbackComment");
const resolutionFeedbackRouteNote = document.getElementById("resolutionFeedbackRouteNote");
const resolutionFeedbackContinue = document.getElementById("resolutionFeedbackContinue");

const monthNames = ["ENE", "FEB", "MAR", "ABR", "MAY", "JUN", "JUL", "AGO", "SEP", "OCT", "NOV", "DIC"];

const state = {
  session: null,
  context: null,
  filter: "TODOS",
  records: [],
  selectedKey: null,
  pendingTarget: null,
  postResolution: null,
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

const formatDateTime = (value) => sessionApi.formatDateTime(value);
const formatDecimal = (value, suffix = "") => `${Number(value || 0).toFixed(2)}${suffix}`;

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

const getKindLabel = (value) => {
  switch (String(value || "").toUpperCase()) {
    case "VACACION":
      return "Vacacion";
    case "HORA_EXTRA":
      return "Hora extra";
    default:
      return value || "Registro";
  }
};

const getBadgeDate = (value) => {
  if (!value) {
    return {
      day: "--",
      month: "SIN",
      year: "FECHA",
    };
  }

  try {
    const date = new Date(`${value}T00:00:00`);
    return {
      day: String(date.getDate()).padStart(2, "0"),
      month: monthNames[date.getMonth()] || "---",
      year: String(date.getFullYear()),
    };
  } catch {
    return {
      day: "--",
      month: "ERR",
      year: "FECHA",
    };
  }
};

const showToast = (message, type = "success") => {
  const toast = document.createElement("div");
  toast.className = `toast is-${type}`;
  toast.textContent = message;
  toastRegion.appendChild(toast);

  window.setTimeout(() => {
    toast.remove();
  }, 3200);
};

const parseTargetFromUrl = () => {
  const params = new URLSearchParams(window.location.search);
  const kind = String(params.get("kind") || "").trim().toUpperCase();
  const id = Number(params.get("id") || 0);

  if (!kind || !(id > 0)) {
    return null;
  }

  return {
    kind,
    id,
    key: `${kind}-${id}`,
  };
};

const updateUrlWithSelection = (record) => {
  const url = new URL(window.location.href);

  if (!record?.kind || !(Number(record?.id || 0) > 0)) {
    url.searchParams.delete("kind");
    url.searchParams.delete("id");
    window.history.replaceState({}, "", url);
    return;
  }

  url.searchParams.set("kind", String(record.kind).toUpperCase());
  url.searchParams.set("id", String(record.id));
  window.history.replaceState({}, "", url);
};

const showResolutionFeedback = ({ action, comment, remaining }) => {
  if (!resolutionFeedbackModal) {
    return;
  }

  const approved = action === "APROBAR";
  const hasPending = remaining > 0;

  resolutionFeedbackIcon.className = `modal-icon-shell ${approved ? "is-success" : "is-danger"}`;
  resolutionFeedbackIcon.innerHTML = approved
    ? '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><path d="m8.5 12 2.5 2.5 4.5-5"></path></svg>'
    : '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><path d="M9 9l6 6"></path><path d="M15 9 9 15"></path></svg>';
  resolutionFeedbackKicker.textContent = approved ? "Aprobado" : "Rechazado";
  resolutionFeedbackTitle.textContent = approved
    ? "Solicitud aprobada correctamente"
    : "Solicitud rechazada";
  resolutionFeedbackText.textContent = approved
    ? "La solicitud ya fue aplicada y se actualizo la bandeja."
    : "La solicitud se rechazo y se guardo el motivo indicado.";

  if (!approved && comment) {
    resolutionFeedbackCommentCard.hidden = false;
    resolutionFeedbackComment.textContent = comment;
  } else {
    resolutionFeedbackCommentCard.hidden = true;
    resolutionFeedbackComment.textContent = "";
  }

  resolutionFeedbackRouteNote.textContent = hasPending
    ? `Quedan ${remaining} pendiente${remaining === 1 ? "" : "s"} por revisar.`
    : "No quedan pendientes; volveras al panel principal.";
  resolutionFeedbackContinue.textContent = hasPending ? "Continuar en bandeja" : "Volver al panel";
  resolutionFeedbackModal.hidden = false;
};

const hideResolutionFeedback = () => {
  if (resolutionFeedbackModal) {
    resolutionFeedbackModal.hidden = true;
  }
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

const buildRecordKey = (item) => `${item.kind}-${item.id}`;

const mapVacation = (item) => ({
  kind: "VACACION",
  id: item.idVacacion,
  key: buildRecordKey({ kind: "VACACION", id: item.idVacacion }),
  title: `${item.codigoEmpleado} - ${item.nombreEmpleado}`,
  subtitle: item.nombreCargo,
  status: item.estadoVacacion,
  requestDate: item.fechaSolicitud,
  primaryDate: item.fechaInicio,
  displayDate: `${formatDate(item.fechaInicio)} al ${formatDate(item.fechaFin)}`,
  amountLabel: `${formatDecimal(item.diasSolicitados)} dias`,
  note: item.observacionSolicitud || "Sin observacion",
  raw: item,
});

const mapOvertime = (item) => ({
  kind: "HORA_EXTRA",
  id: item.idHoraExtra,
  key: buildRecordKey({ kind: "HORA_EXTRA", id: item.idHoraExtra }),
  title: `${item.codigoEmpleado} - ${item.nombreEmpleado}`,
  subtitle: item.nombreTipoHoraExtra,
  status: item.estadoHoraExtra,
  requestDate: item.fechaRegistro,
  primaryDate: item.fechaHoraExtra,
  displayDate: formatDate(item.fechaHoraExtra),
  amountLabel: `${formatDecimal(item.cantidadHoras)} h`,
  note: item.observacion || "Sin observacion",
  raw: item,
});

const getFilteredRecords = () =>
  state.records.filter((item) => state.filter === "TODOS" || item.kind === state.filter);

const setActiveFilterButton = () => {
  supervisorFilterRow.querySelectorAll("[data-filter]").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.filter === state.filter);
  });
};

const renderDetailItems = (items) =>
  items
    .map(
      (item) => `
        <article class="detail-item">
          <span>${item.label}</span>
          <strong>${item.value || "Sin registro"}</strong>
        </article>
      `,
    )
    .join("");

const renderSelectedRecord = () => {
  const selected = getFilteredRecords().find((item) => item.key === state.selectedKey);

  if (!selected) {
    state.selectedKey = null;
    updateUrlWithSelection(null);
    detailTitle.textContent = "Sin seleccion";
    detailGrid.innerHTML = `
      <div class="detail-empty">
        Selecciona un registro de la bandeja para aprobarlo o rechazarlo.
      </div>
    `;
    resolutionForm.hidden = true;
    return;
  }

  const detailItems = [
    { label: "Empleado", value: selected.title },
    { label: "Tipo", value: getKindLabel(selected.kind) },
    { label: "Estado", value: selected.status },
    {
      label: "Fecha solicitud",
      value: selected.raw.fechaRegistro
        ? formatDateTime(selected.raw.fechaRegistro)
        : formatDate(selected.raw.fechaSolicitud),
    },
    { label: "Periodo", value: selected.displayDate },
    { label: selected.kind === "HORA_EXTRA" ? "Horas" : "Cantidad", value: selected.amountLabel },
    { label: "Departamento", value: selected.raw.nombreDepartamento },
    { label: "Cargo", value: selected.raw.nombreCargo },
  ];

  if (selected.kind === "VACACION" && selected.raw.diasVacacionesDisponibles !== undefined) {
    detailItems.push({
      label: "Saldo disponible",
      value: `${formatDecimal(selected.raw.diasVacacionesDisponibles)} dias`,
    });
  }

  if (selected.kind === "HORA_EXTRA") {
    detailItems.push({
      label: "Registrado por",
      value: selected.raw.usuarioRegistra,
    });
  }

  detailTitle.textContent = selected.title;
  updateUrlWithSelection(selected);
  detailGrid.innerHTML = `
    <article class="detail-hero-card">
      <div class="detail-hero-top">
        <span class="record-type">${getKindLabel(selected.kind)}</span>
        <span class="status-pill ${getStatusClass(selected.status)}">${selected.status}</span>
      </div>

      <h4>${selected.title}</h4>
      <p>${selected.subtitle || "Pendiente por revisar"}</p>

      <div class="detail-hero-meta">
        <span>${selected.displayDate}</span>
        <strong>${selected.amountLabel}</strong>
      </div>
    </article>

    <section class="detail-grid-split">
      ${renderDetailItems(detailItems)}
    </section>

    <article class="detail-note-card">
      <span>Detalle de la solicitud</span>
      <p>${selected.note}</p>
    </article>
  `;

  resolutionComment.value = "";
  resolutionForm.hidden = false;

  if (selected.kind === "VACACION") {
    approvedDaysField.hidden = false;
    approvedDaysInput.textContent = `${formatDecimal(selected.raw.diasSolicitados)} dias`;
  } else {
    approvedDaysField.hidden = true;
    approvedDaysInput.textContent = "0.00 dias";
  }
};

const renderRecordList = () => {
  const items = getFilteredRecords();
  metricPendingVisible.textContent = String(items.length);

  if (!items.length && state.records.length && state.filter !== "TODOS") {
    state.filter = "TODOS";
    state.pendingTarget = null;
    setActiveFilterButton();
    renderRecordList();
    return;
  }

  if (!items.length) {
    pendingRecordList.innerHTML = "";
    pendingRecordEmptyState.hidden = false;
    renderSelectedRecord();
    return;
  }

  pendingRecordEmptyState.hidden = true;
  pendingRecordList.innerHTML = items
    .map((item) => {
      const badge = getBadgeDate(item.primaryDate);

      return `
        <article class="record-card${
          item.key === state.selectedKey ? " is-active" : ""
        }" data-record-key="${item.key}">
          <div class="record-card-rail">
            <span class="record-type">${getKindLabel(item.kind)}</span>
            <strong class="record-day">${badge.day}</strong>
            <span class="record-month">${badge.month}</span>
            <span class="record-year">${badge.year}</span>
          </div>

          <div class="record-card-content">
            <div class="record-card-headline">
              <strong class="record-title">${item.title}</strong>
              <span class="record-subtitle">${item.subtitle || "Sin detalle"}</span>
            </div>

            <div class="record-card-meta">
              <span>${item.displayDate}</span>
              <span>${item.raw.nombreDepartamento || "Sin departamento"}</span>
            </div>

            <p class="record-note">${item.note}</p>
          </div>

          <div class="record-card-side">
            <span class="status-pill ${getStatusClass(item.status)}">${item.status}</span>
            <strong class="record-amount">${item.amountLabel}</strong>
            <small>${formatDateTime(item.requestDate)}</small>
          </div>
        </article>
      `;
    })
    .join("");

  pendingRecordList.querySelectorAll("[data-record-key]").forEach((element) => {
    element.addEventListener("click", () => {
      state.selectedKey = element.dataset.recordKey;
      renderRecordList();
      renderSelectedRecord();
    });
  });

  if (state.pendingTarget?.kind) {
    const matchingRecord = items.find(
      (item) =>
        item.kind === state.pendingTarget.kind &&
        Number(item.id) === Number(state.pendingTarget.id),
    );

    if (matchingRecord) {
      state.selectedKey = matchingRecord.key;
      state.filter = state.pendingTarget.kind;
      setActiveFilterButton();
      state.pendingTarget = null;
    }
  }

  if (!items.some((item) => item.key === state.selectedKey)) {
    state.selectedKey = items[0].key;
  }

  renderSelectedRecord();
};

const renderContext = (context) => {
  state.context = context;
  sessionUser.textContent =
    context.session?.displayName || context.session?.username || "Usuario";
  sessionMeta.textContent =
    context.session?.rolesLabel || context.session?.username || "Sesion activa";
  supervisorTitle.textContent = context.employee?.nombreEmpleado
    ? `Centro de aprobaciones de ${context.employee.nombreEmpleado}`
    : "Pendientes del sistema";
  supervisorNote.textContent =
    "Resuelve vacaciones y horas extra desde una sola bandeja.";
  scopeNote.textContent = context.note || "";
  metricPendingVacations.textContent = String(context.counts?.pendingVacations || 0);
  metricPendingOvertime.textContent = String(context.counts?.pendingOvertime || 0);
};

const loadSupervisorRecords = async () => {
  const payload = await requestWithOperator("/Portal/SupervisorPendientes");
  const vacationsPayload = payload?.data?.vacations || [];
  const overtimePayload = payload?.data?.overtime || [];

  state.records = [
    ...vacationsPayload.map(mapVacation),
    ...overtimePayload.map(mapOvertime),
  ].sort((left, right) => {
    const leftValue = new Date(
      left.raw.fechaRegistro || left.raw.fechaSolicitud || left.raw.fechaHoraExtra,
    ).getTime();
    const rightValue = new Date(
      right.raw.fechaRegistro || right.raw.fechaSolicitud || right.raw.fechaHoraExtra,
    ).getTime();
    return rightValue - leftValue;
  });

  renderRecordList();
};

const loadSupervisor = async ({ notify = false } = {}) => {
  const payload = await sessionApi.request("/Portal/SupervisorContexto");
  renderContext(payload.data);
  await loadSupervisorRecords();

  if (notify) {
    showToast("Bandeja actualizada.", "success");
  }
};

const resolveSelectedRecord = async (action) => {
  const selected = getFilteredRecords().find((item) => item.key === state.selectedKey);

  if (!selected) {
    showToast("Selecciona un registro para continuar.", "danger");
    return;
  }

  const observation = resolutionComment.value.trim();
  if (action === "RECHAZAR" && !observation) {
    showToast("Escribe el motivo del rechazo.", "danger");
    return;
  }
  let url = "";
  let payload = {
    action,
    observation,
  };

  if (selected.kind === "VACACION") {
    payload = {
      ...payload,
      approvedDays: Number(selected.raw.diasSolicitados || 0),
    };
    url = `/Portal/ResolverSupervisorVacacion/${selected.id}`;
  } else {
    url = `/Portal/ResolverSupervisorHoraExtra/${selected.id}`;
  }

  await requestWithOperator(url, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

  await loadSupervisor();
  if (state.records.length && !getFilteredRecords().length) {
    state.filter = "TODOS";
    setActiveFilterButton();
    renderRecordList();
  }
  state.postResolution = {
    redirectDashboard: state.records.length === 0,
  };
  showResolutionFeedback({
    action,
    comment: action === "RECHAZAR" ? observation : "",
    remaining: state.records.length,
  });
};

const boot = async () => {
  state.session = sessionApi.getSession();

  if (!state.session) {
    window.location.href = "/App/Login";
    return;
  }

  sessionUser.textContent = state.session.displayName || state.session.user || "Usuario";
  sessionMeta.textContent = state.session.rolesLabel || state.session.username || "Sesion activa";
  state.pendingTarget = parseTargetFromUrl();
  if (state.pendingTarget?.kind) {
    state.filter = state.pendingTarget.kind;
  }
  setActiveFilterButton();
  await loadSupervisor();
};

backToDashboard?.addEventListener("click", () => {
  window.location.href = "/App/Dashboard";
});

refreshSupervisor?.addEventListener("click", async () => {
  try {
    refreshSupervisor.disabled = true;
    await loadSupervisor({ notify: true });
  } catch (error) {
    showToast(error.message || "No se pudo actualizar la bandeja.", "danger");
  } finally {
    refreshSupervisor.disabled = false;
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

supervisorFilterRow?.querySelectorAll("[data-filter]").forEach((button) => {
  button.addEventListener("click", () => {
    state.filter = button.dataset.filter || "TODOS";
    state.pendingTarget = null;
    setActiveFilterButton();
    renderRecordList();
  });
});

approveButton?.addEventListener("click", async () => {
  try {
    approveButton.disabled = true;
    rejectButton.disabled = true;
    await resolveSelectedRecord("APROBAR");
  } catch (error) {
    showToast(error.message || "No se pudo aprobar la solicitud.", "danger");
  } finally {
    approveButton.disabled = false;
    rejectButton.disabled = false;
  }
});

rejectButton?.addEventListener("click", async () => {
  try {
    approveButton.disabled = true;
    rejectButton.disabled = true;
    await resolveSelectedRecord("RECHAZAR");
  } catch (error) {
    showToast(error.message || "No se pudo rechazar la solicitud.", "danger");
  } finally {
    approveButton.disabled = false;
    rejectButton.disabled = false;
  }
});

resolutionFeedbackContinue?.addEventListener("click", () => {
  const shouldReturnDashboard = Boolean(state.postResolution?.redirectDashboard);
  state.postResolution = null;
  hideResolutionFeedback();

  if (shouldReturnDashboard) {
    window.location.href = "/App/Dashboard";
  }
});

boot().catch((error) => {
  showToast(error.message || "No se pudo cargar la bandeja del supervisor.", "danger");
});
