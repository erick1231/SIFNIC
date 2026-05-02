window.RRHHView = (() => {
  const elements = {
    sessionUser: document.getElementById("sessionUser"),
    sessionMeta: document.getElementById("sessionMeta"),
    backToDashboard: document.getElementById("backToDashboard"),
    closeSession: document.getElementById("closeSession"),
    createRecordButton: document.getElementById("createRecordButton"),
    sectionNav: document.getElementById("sectionNav"),
    deskSchema: document.getElementById("deskSchema"),
    deskTitle: document.getElementById("deskTitle"),
    deskSubtitle: document.getElementById("deskSubtitle"),
    deskActions: document.getElementById("deskActions"),
    recordSearch: document.getElementById("recordSearch"),
    toolbarFilters: document.getElementById("toolbarFilters"),
    recordsTitle: document.getElementById("recordsTitle"),
    recordCounter: document.getElementById("recordCounter"),
    tableHead: document.getElementById("tableHead"),
    tableBody: document.getElementById("tableBody"),
    inspectorTitle: document.getElementById("inspectorTitle"),
    inspectorBody: document.getElementById("inspectorBody"),
  };

  const normalizeText = (value) =>
    String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

  const formatSessionDate = (value) => {
    if (!value) {
      return "Sin registro reciente";
    }

    return new Intl.DateTimeFormat("es-NI", {
      day: "2-digit",
      month: "short",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
      timeZone: "America/Managua",
    }).format(new Date(value));
  };

  const getStatusClass = (value) => {
    const normalized = normalizeText(value);

    if (
      normalized.includes("activo") ||
      normalized.includes("vigente") ||
      normalized.includes("aprobad") ||
      normalized.includes("abierta") ||
      normalized.includes("aplicado") ||
      normalized.includes("cerrada") ||
      normalized.includes("programada")
    ) {
      return "is-success";
    }

    if (
      normalized.includes("pendiente") ||
      normalized.includes("revisi") ||
      normalized.includes("por vencer") ||
      normalized.includes("en calculo") ||
      normalized.includes("enviando") ||
      normalized.includes("por autorizar") ||
      normalized.includes("vacaciones") ||
      normalized.includes("permiso") ||
      normalized.includes("en curso")
    ) {
      return "is-warning";
    }

    if (normalized.includes("rechazado") || normalized.includes("alta")) {
      return "is-danger";
    }

    return "";
  };

  const getTableValues = (sectionId, record) => {
    const supervisor =
      record.details?.find(([label]) => normalizeText(label).includes("supervisor"))?.[1] || "-";

    switch (sectionId) {
      case "empleados":
        return [record.id, record.name, record.position, record.branch, record.status];
      case "contratos":
        return [record.id, record.name, record.type, record.validity, record.status];
      case "permisos":
        return [record.id, record.name, record.reason, record.date, record.status];
      case "vacaciones":
        return [record.name, record.balance, record.schedule, supervisor, record.status];
      case "horas_extra":
        return [record.id, record.name, record.hours, record.reason, record.status];
      case "nomina":
        return [record.id, record.type, record.period, record.people, record.status];
      case "liquidaciones":
        return [record.id, record.name, record.departure, record.type, record.status];
      case "prestamos_variables":
        return [record.id, record.name, record.movementType, record.amount, record.status];
      default:
        return [record.id || "-", record.name || "-", "-", "-", record.status || "-"];
    }
  };

  const setSession = (session) => {
    elements.sessionUser.textContent = session.user || "Usuario SIFNIC";
    elements.sessionMeta.textContent = `Acceso: ${formatSessionDate(session.loginAt)}`;
  };

  const renderSectionNav = (sections, activeSectionId) => {
    elements.sectionNav.innerHTML = sections
      .map(
        (section) => `
          <button class="section-button${
            section.id === activeSectionId ? " is-active" : ""
          }" data-section-id="${section.id}" type="button">
            <div class="section-button-copy">
              <strong>${section.label}</strong>
              <small>${section.subtitle}</small>
            </div>
            <div class="section-button-top">
              <span class="section-dot" style="background:${section.accent}"></span>
              <span class="section-count">${section.records.length}</span>
            </div>
          </button>
        `,
      )
      .join("");
  };

  const renderActions = (actions) => {
    elements.deskActions.innerHTML = actions
      .map((action) => `<button class="action-button" type="button">${action}</button>`)
      .join("");
  };

  const renderFilters = (filters, activeFilter) => {
    elements.toolbarFilters.innerHTML = filters
      .map(
        (filter) => `
          <button class="filter-chip${
            filter === activeFilter ? " is-active" : ""
          }" data-filter-name="${filter}" type="button">
            ${filter}
          </button>
        `,
      )
      .join("");
  };

  const renderTable = (section, records, activeRecordId) => {
    elements.recordsTitle.textContent = section.label;
    elements.recordCounter.textContent = `${records.length} registros`;
    elements.tableHead.innerHTML = `
      <tr>
        ${section.columns.map((column) => `<th>${column}</th>`).join("")}
      </tr>
    `;

    const rows = records
      .map((record) => {
        const values = getTableValues(section.id, record);
        return `
          <tr class="record-row${
            record.id === activeRecordId ? " is-active" : ""
          }" data-record-id="${record.id}">
            ${values
              .map((value, index) => {
                const isStatus = index === values.length - 1;
                return `<td>${
                  isStatus
                    ? `<span class="status-chip ${getStatusClass(value)}">${value}</span>`
                    : value
                }</td>`;
              })
              .join("")}
          </tr>
        `;
      })
      .join("");

    elements.tableBody.innerHTML =
      rows ||
      `<tr><td colspan="${section.columns.length}">No hay registros para el filtro actual.</td></tr>`;
  };

  const renderInspector = (record) => {
    if (!record) {
      elements.inspectorTitle.textContent = "Sin seleccion";
      elements.inspectorBody.innerHTML =
        '<p class="inspector-copy">Selecciona un registro para revisar su detalle.</p>';
      return;
    }

    elements.inspectorTitle.textContent = record.name || record.id;
    elements.inspectorBody.innerHTML = `
      <div class="inspector-header">
        <span class="status-chip ${getStatusClass(record.status)}">${record.status}</span>
        <strong class="inspector-code">${record.id || "Registro"}</strong>
        <p class="inspector-copy">${record.summary || "Registro listo para gestion."}</p>
      </div>

      <div class="detail-group">
        <span class="detail-group-title">Ficha</span>
        ${record.details
          .map(
            ([label, value]) => `
              <div class="detail-row">
                <span>${label}</span>
                <strong>${value}</strong>
              </div>
            `,
          )
          .join("")}
      </div>

      <div class="detail-group">
        <span class="detail-group-title">Actividad reciente</span>
        <div class="timeline-list">
          ${record.timeline
            .map(
              ([moment, detail]) => `
                <article class="timeline-item">
                  <span>${moment}</span>
                  <p>${detail}</p>
                </article>
              `,
            )
            .join("")}
        </div>
      </div>
    `;
  };

  const renderDesk = (section, records, activeRecordId, activeFilter) => {
    elements.deskSchema.textContent = section.schema;
    elements.deskTitle.textContent = section.label;
    elements.deskSubtitle.textContent = section.subtitle;
    elements.createRecordButton.textContent = section.createLabel;
    renderActions(section.actions);
    renderFilters(section.filters, activeFilter);
    renderTable(section, records, activeRecordId);
  };

  return {
    elements,
    setSession,
    renderSectionNav,
    renderDesk,
    renderInspector,
  };
})();
