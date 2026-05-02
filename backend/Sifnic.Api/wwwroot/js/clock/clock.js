(() => {
  const sessionApi = window.SifnicSession;
  const clockService = window.ClockService;

  const isControlMode = new URLSearchParams(window.location.search).get("control") === "1";

  const elements = {
    clockPageTitle: document.getElementById("clockPageTitle"),
    clockBackButton: document.getElementById("clockBackButton"),
    clockLogoutButton: document.getElementById("clockLogoutButton"),
    clockSessionPill: document.getElementById("clockSessionPill"),
    clockSessionUser: document.getElementById("clockSessionUser"),
    clockSessionMeta: document.getElementById("clockSessionMeta"),
    publicClockPanel: document.getElementById("publicClockPanel"),
    controlClockPanel: document.getElementById("clockControlPanel"),
    clockForm: document.getElementById("clockForm"),
    clockCedula: document.getElementById("clockCedula"),
    clockSearchButton: document.getElementById("clockSearchButton"),
    clockEntryButton: document.getElementById("clockEntryButton"),
    clockExitButton: document.getElementById("clockExitButton"),
    clockFormNote: document.getElementById("clockFormNote"),
    clockEmployeeCard: document.getElementById("clockEmployeeCard"),
    clockHistoryCard: document.getElementById("clockHistoryCard"),
    controlSearch: document.getElementById("controlSearch"),
    controlDateFrom: document.getElementById("controlDateFrom"),
    controlDateTo: document.getElementById("controlDateTo"),
    controlEmployeeFilter: document.getElementById("controlEmployeeFilter"),
    controlRefreshButton: document.getElementById("controlRefreshButton"),
    controlExportExcelButton: document.getElementById("controlExportExcelButton"),
    controlExportPdfButton: document.getElementById("controlExportPdfButton"),
    controlCounter: document.getElementById("controlCounter"),
    controlTableBody: document.getElementById("controlTableBody"),
    controlDetailTitle: document.getElementById("controlDetailTitle"),
    controlDetailBody: document.getElementById("controlDetailBody"),
    clockToastRegion: document.getElementById("clockToastRegion"),
  };

  const state = {
    status: null,
    branding: null,
    controlRows: [],
    selectedControlIndex: -1,
    controlSearchTimer: null,
    lastResolvedCedula: "",
    lookupRequestId: 0,
    publicBusyAction: "",
  };

  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const sanitizeCedula = (value) => {
    const raw = String(value || "")
      .toUpperCase()
      .replace(/[^0-9A-Z]/g, "");

    const digits = raw.replace(/[^0-9]/g, "").slice(0, 13);
    const letter = raw.replace(/[^A-Z]/g, "").slice(0, 1);

    if (!digits) {
      return letter;
    }

    if (digits.length <= 3) {
      return `${digits}${letter}`;
    }

    if (digits.length <= 9) {
      return `${digits.slice(0, 3)}-${digits.slice(3)}${letter}`;
    }

    return `${digits.slice(0, 3)}-${digits.slice(3, 9)}-${digits.slice(9, 13)}${letter}`;
  };

  const isCompleteCedula = (value) => /^\d{3}-\d{6}-\d{4}[A-Z]$/.test(String(value || "").trim());

  const formatDate = (value) => {
    if (!value) {
      return "-";
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
      }).format(new Date(value.replace(" ", "T")));
    } catch {
      return value;
    }
  };

  const formatTime = (value) => {
    if (!value) {
      return "-";
    }

    try {
      return new Intl.DateTimeFormat("es-NI", {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hour12: false,
        timeZone: "America/Managua",
      }).format(new Date(value.replace(" ", "T")));
    } catch {
      return value;
    }
  };

  const getWorkedHours = (entrada, salida) => {
    if (!entrada || !salida) {
      return "-";
    }

    const entradaDate = new Date(String(entrada).replace(" ", "T"));
    const salidaDate = new Date(String(salida).replace(" ", "T"));
    const diffMs = salidaDate.getTime() - entradaDate.getTime();

    if (!Number.isFinite(diffMs) || diffMs <= 0) {
      return "-";
    }

    return `${(diffMs / (1000 * 60 * 60)).toFixed(2)} h`;
  };

  const showToast = (message, tone = "info") => {
    const toast = document.createElement("div");
    toast.className = `toast ${
      tone === "danger" ? "is-danger" : tone === "success" ? "is-success" : ""
    }`;
    toast.textContent = message;
    elements.clockToastRegion.appendChild(toast);

    window.setTimeout(() => {
      toast.remove();
    }, 3600);
  };

  const setNotice = (message, tone = "") => {
    elements.clockFormNote.textContent = message;
    elements.clockFormNote.style.color =
      tone === "danger"
        ? "#ffd8cf"
        : tone === "success"
          ? "var(--success)"
          : "var(--text-soft)";
  };

  const clearPublicStatus = () => {
    state.status = null;
    state.lastResolvedCedula = "";
  };

  const updatePublicActionButtons = () => {
    const nextAction = String(state.status?.nextAction || "").toUpperCase();
    const hasEmployee = Boolean(state.status?.employee);
    const busyAction = state.publicBusyAction;
    const isBusy = Boolean(busyAction);

    elements.clockSearchButton.disabled = isBusy;
    elements.clockSearchButton.textContent = busyAction === "SEARCH" ? "Buscando..." : "Buscar";

    elements.clockEntryButton.disabled = !hasEmployee || nextAction !== "ENTRADA" || isBusy;
    elements.clockExitButton.disabled = !hasEmployee || nextAction !== "SALIDA" || isBusy;

    elements.clockEntryButton.textContent = busyAction === "ENTRADA" ? "Marcando entrada..." : "Marcar entrada";
    elements.clockExitButton.textContent = busyAction === "SALIDA" ? "Marcando salida..." : "Marcar salida";

    elements.clockEntryButton.classList.toggle("is-active", hasEmployee && nextAction === "ENTRADA");
    elements.clockExitButton.classList.toggle("is-active", hasEmployee && nextAction === "SALIDA");
  };

  const renderEmployeeCard = () => {
    const payload = state.status;

    if (!payload?.employee) {
      elements.clockEmployeeCard.innerHTML = `
        <div class="detail-empty">
          <p>Ingresa la cedula para cargar los datos del colaborador.</p>
        </div>
      `;
      updatePublicActionButtons();
      return;
    }

    const employee = payload.employee;
    const nextActionLabel = payload.nextAction === "ENTRADA" ? "Marcar entrada" : "Marcar salida";

    elements.clockEmployeeCard.innerHTML = `
      <div class="detail-header">
        <span class="eyebrow">Colaborador</span>
        <h3>${escapeHtml(employee.nombreEmpleado)}</h3>
      </div>
      <div class="detail-grid">
        <div class="detail-row">
          <span>Cedula</span>
          <strong>${escapeHtml(employee.cedula)}</strong>
        </div>
        <div class="detail-row">
          <span>Accion disponible</span>
          <strong>${escapeHtml(nextActionLabel)}</strong>
        </div>
      </div>
    `;

    updatePublicActionButtons();
  };

  const renderHistoryCard = () => {
    const marks = Array.isArray(state.status?.todayMarks) ? state.status.todayMarks : [];

    if (!state.status?.employee) {
      elements.clockHistoryCard.innerHTML = `
        <div class="detail-empty">
          <p>Las marcaciones del colaborador apareceran aqui.</p>
        </div>
      `;
      return;
    }

    if (!marks.length) {
      elements.clockHistoryCard.innerHTML = `
        <div class="detail-empty">
          <p>Este colaborador aun no tiene marcaciones registradas hoy.</p>
        </div>
      `;
      return;
    }

    const groupedRows = Array.from(
      marks.reduce((map, mark) => {
        const key = String(mark.fechaOperacion || "");
        if (!map.has(key)) {
          map.set(key, {
            fechaOperacion: key,
            entrada: null,
            salida: null,
            origenes: new Set(),
          });
        }

        const row = map.get(key);
        row.origenes.add(String(mark.origen || "RELOJ").trim() || "RELOJ");

        if (String(mark.tipoMarcacion || "").toUpperCase() === "ENTRADA") {
          if (!row.entrada || new Date(String(mark.fechaHoraMarcacion).replace(" ", "T")) < new Date(String(row.entrada).replace(" ", "T"))) {
            row.entrada = mark.fechaHoraMarcacion;
          }
        }

        if (String(mark.tipoMarcacion || "").toUpperCase() === "SALIDA") {
          if (!row.salida || new Date(String(mark.fechaHoraMarcacion).replace(" ", "T")) > new Date(String(row.salida).replace(" ", "T"))) {
            row.salida = mark.fechaHoraMarcacion;
          }
        }

        return map;
      }, new Map()).values(),
    ).sort((left, right) => String(right.fechaOperacion).localeCompare(String(left.fechaOperacion)));

    const originLabel = Array.from(
      groupedRows.reduce((set, row) => {
        row.origenes.forEach((item) => set.add(item));
        return set;
      }, new Set()),
    ).join(", ");

    elements.clockHistoryCard.innerHTML = `
      <div class="panel-copy">
        <span class="eyebrow">Grid</span>
        <h3>Marcaciones del dia</h3>
      </div>
      <div class="table-wrap table-wrap-compact">
        <table class="data-table">
          <thead>
            <tr>
              <th>Fecha</th>
              <th>Entrada</th>
              <th>Salida</th>
              <th>Horas trabajadas</th>
            </tr>
          </thead>
          <tbody>
            ${groupedRows
              .map(
                (row) => `
                  <tr>
                    <td>${escapeHtml(formatDate(row.fechaOperacion))}</td>
                    <td>${escapeHtml(formatTime(row.entrada))}</td>
                    <td>${escapeHtml(formatTime(row.salida))}</td>
                    <td>${escapeHtml(getWorkedHours(row.entrada, row.salida))}</td>
                  </tr>
                `,
              )
              .join("")}
          </tbody>
        </table>
      </div>
      <p class="form-note">Origen: ${escapeHtml(originLabel || "RELOJ")}</p>
    `;
  };

  const requestStatus = async (cedula) => {
    const requestId = ++state.lookupRequestId;
    const payload = await clockService.getStatus(cedula);
    if (requestId !== state.lookupRequestId) {
      return null;
    }

    state.status = payload;
    state.lastResolvedCedula = cedula;
    renderEmployeeCard();
    renderHistoryCard();

    return payload;
  };

  const loadStatus = async () => {
    const cedula = sanitizeCedula(elements.clockCedula.value);
    elements.clockCedula.value = cedula;

    if (!cedula) {
      clearPublicStatus();
      renderEmployeeCard();
      renderHistoryCard();
      setNotice("Ingresa la cedula para cargar al colaborador.");
      return;
    }

    if (!isCompleteCedula(cedula)) {
      clearPublicStatus();
      renderEmployeeCard();
      renderHistoryCard();
      setNotice("Completa la cedula para cargar los datos del colaborador.");
      return;
    }

    state.publicBusyAction = "SEARCH";
    updatePublicActionButtons();
    setNotice("Buscando colaborador...");

    try {
      const payload = await requestStatus(cedula);
      if (!payload) {
        return;
      }

      setNotice(
        `Colaborador identificado. La siguiente accion disponible es ${
          payload.nextAction === "ENTRADA" ? "entrada" : "salida"
        }.`,
        "success",
      );
    } catch (error) {
      clearPublicStatus();
      renderEmployeeCard();
      renderHistoryCard();
      setNotice(error.message || "No se pudo consultar la cedula.", "danger");
    } finally {
      state.publicBusyAction = "";
      updatePublicActionButtons();
    }
  };

  const submitMark = async (action) => {
    const cedula = sanitizeCedula(elements.clockCedula.value);
    elements.clockCedula.value = cedula;

    if (!cedula) {
      setNotice("Ingresa la cedula y busca al colaborador antes de marcar.", "danger");
      elements.clockCedula.focus();
      return;
    }

    if (!state.status?.employee || state.lastResolvedCedula !== cedula) {
      setNotice("Primero busca la cedula para cargar los datos del colaborador.", "danger");
      elements.clockCedula.focus();
      return;
    }

    const expectedAction = String(state.status.nextAction || "").toUpperCase();
    if (expectedAction !== action) {
      setNotice(`La siguiente accion disponible es ${expectedAction.toLowerCase()}.`, "danger");
      showToast(`La siguiente accion disponible es ${expectedAction.toLowerCase()}.`, "danger");
      await loadStatus();
      return;
    }

    state.publicBusyAction = action;
    updatePublicActionButtons();

    try {
      const response = await clockService.mark({
        cedula,
        tipoMarcacion: action,
        observacion: null,
      });

      showToast(response?.message || "Marcacion registrada.", "success");
      await requestStatus(cedula);
      setNotice(
        `Marcacion registrada. La siguiente accion disponible es ${
          state.status?.nextAction === "ENTRADA" ? "entrada" : "salida"
        }.`,
        "success",
      );
    } catch (error) {
      setNotice(error.message || "No se pudo registrar la marcacion.", "danger");
      showToast(error.message || "No se pudo registrar la marcacion.", "danger");
    } finally {
      state.publicBusyAction = "";
      updatePublicActionButtons();
    }
  };

  const fillEmployeeFilter = (employees = [], selectedValue = "") => {
    elements.controlEmployeeFilter.innerHTML = [
      '<option value="">Todos los empleados</option>',
      ...employees.map(
        (employee) =>
          `<option value="${escapeHtml(employee.id)}"${
            String(employee.id) === String(selectedValue) ? " selected" : ""
          }>${escapeHtml(`${employee.code} - ${employee.name}`)}</option>`,
      ),
    ].join("");
  };

  const renderControlDetail = () => {
    const row = state.controlRows[state.selectedControlIndex] || null;

    if (!row) {
      elements.controlDetailTitle.textContent = "Sin seleccion";
      elements.controlDetailBody.innerHTML = `
        <div class="detail-empty">
          <p>Selecciona una jornada para ver el detalle.</p>
        </div>
      `;
      return;
    }

    elements.controlDetailTitle.textContent = `${row.nombreEmpleado} · ${formatDate(row.fechaOperacion)}`;

    elements.controlDetailBody.innerHTML = `
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
        <div class="detail-row">
          <span>Cedula</span>
          <strong>${escapeHtml(row.cedula)}</strong>
        </div>
        <div class="detail-row">
          <span>Entrada</span>
          <strong>${escapeHtml(row.horaEntrada || "-")}</strong>
        </div>
        <div class="detail-row">
          <span>Salida</span>
          <strong>${escapeHtml(row.horaSalida || "-")}</strong>
        </div>
        <div class="detail-row">
          <span>Horas trabajadas</span>
          <strong>${escapeHtml(`${row.horasTrabajadas.toFixed(2)} h`)}</strong>
        </div>
        <div class="detail-row">
          <span>Estado</span>
          <strong class="${row.estadoJornada === "CERRADA" ? "status-success" : "status-warning"}">
            ${escapeHtml(row.estadoJornada)}
          </strong>
        </div>
      </div>

      <div class="clock-history-card">
        <div class="panel-copy">
          <span class="eyebrow">Marcas</span>
          <h3>Secuencia consolidada</h3>
        </div>
        <div class="mark-list">
          ${row.marcas
            .map(
              (mark) => `
                <article class="mark-item">
                  <div class="cell-stack">
                    <strong>${escapeHtml(mark.tipoMarcacion)}</strong>
                    <small>${escapeHtml(mark.origen)}</small>
                  </div>
                  <strong>${escapeHtml(mark.fechaHoraMarcacion.slice(11))}</strong>
                </article>
              `,
            )
            .join("")}
        </div>
      </div>
    `;
  };

  const renderControlTable = () => {
    elements.controlCounter.textContent = `${state.controlRows.length} registros`;

    if (!state.controlRows.length) {
      elements.controlTableBody.innerHTML =
        '<tr><td class="table-message" colspan="6">No hay marcaciones para el filtro actual.</td></tr>';
      renderControlDetail();
      return;
    }

    elements.controlTableBody.innerHTML = state.controlRows
      .map(
        (row, index) => `
          <tr class="record-row${index === state.selectedControlIndex ? " is-active" : ""}" data-row-index="${index}">
            <td>${escapeHtml(formatDate(row.fechaOperacion))}</td>
            <td>
              <div class="cell-stack">
                <strong>${escapeHtml(row.nombreEmpleado)}</strong>
                <small>${escapeHtml(row.codigoEmpleado)} · ${escapeHtml(row.cedula)}</small>
              </div>
            </td>
            <td>${escapeHtml(row.horaEntrada || "-")}</td>
            <td>${escapeHtml(row.horaSalida || "-")}</td>
            <td>${escapeHtml(`${row.horasTrabajadas.toFixed(2)} h`)}</td>
            <td>
              <span class="status-pill ${row.estadoJornada === "CERRADA" ? "status-success" : "status-warning"}">
                ${escapeHtml(row.estadoJornada)}
              </span>
            </td>
          </tr>
        `,
      )
      .join("");

    renderControlDetail();
  };

  const loadControlSummary = async () => {
    elements.controlRefreshButton.disabled = true;

    try {
      const payload = await clockService.getSummary({
        search: elements.controlSearch.value.trim(),
        dateFrom: elements.controlDateFrom.value,
        dateTo: elements.controlDateTo.value,
        idEmpleado: elements.controlEmployeeFilter.value || null,
      });

      state.controlRows = Array.isArray(payload?.rows) ? payload.rows : [];
      state.branding = payload?.branding || null;
      state.selectedControlIndex = state.controlRows.length ? 0 : -1;
      renderControlTable();
    } catch (error) {
      state.controlRows = [];
      state.selectedControlIndex = -1;
      renderControlTable();
      showToast(error.message || "No se pudo cargar el reporte del reloj.", "danger");
    } finally {
      elements.controlRefreshButton.disabled = false;
    }
  };

  const buildReportHtml = () => {
    const branding = state.branding || {};
    const title = `Reporte de asistencia ${formatDate(elements.controlDateFrom.value)} al ${formatDate(
      elements.controlDateTo.value,
    )}`;
    const logoMarkup = branding.logoUrl
      ? `<img src="${escapeHtml(branding.logoUrl)}" alt="Logo empresa" style="height:56px;object-fit:contain;" />`
      : `<div style="width:56px;height:56px;border-radius:16px;background:#d6f6f0;color:#0b2430;display:grid;place-items:center;font-family:'Space Grotesk',sans-serif;font-weight:700;">${
          escapeHtml((branding.companyName || "SF").slice(0, 2).toUpperCase())
        }</div>`;

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
              <h1>${escapeHtml(branding.legalName || branding.companyName || "SISFNIC")}</h1>
              <div class="meta">
                <div>${escapeHtml(branding.address || "")}</div>
                <div>${escapeHtml(branding.email || "")} ${branding.phone ? "· " + escapeHtml(branding.phone) : ""}</div>
                <div>${branding.ruc ? "RUC: " + escapeHtml(branding.ruc) : ""}</div>
              </div>
            </div>
            <div class="meta">
              <strong>${escapeHtml(title)}</strong><br />
              Generado: ${escapeHtml(new Date().toLocaleString("es-NI"))}<br />
              ${branding.logoPending ? "Logo corporativo pendiente de configuracion." : ""}
            </div>
          </div>

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
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              ${
                state.controlRows.length
                  ? state.controlRows
                      .map(
                        (row) => `
                          <tr>
                            <td>${escapeHtml(formatDate(row.fechaOperacion))}</td>
                            <td>${escapeHtml(row.codigoEmpleado)}</td>
                            <td>${escapeHtml(row.nombreEmpleado)}</td>
                            <td>${escapeHtml(row.cedula)}</td>
                            <td>${escapeHtml(row.horaEntrada || "-")}</td>
                            <td>${escapeHtml(row.horaSalida || "-")}</td>
                            <td>${escapeHtml(row.horasTrabajadas.toFixed(2))}</td>
                            <td>${escapeHtml(row.estadoJornada)}</td>
                          </tr>
                        `,
                      )
                      .join("")
                  : '<tr><td colspan="8">Sin registros para el filtro actual.</td></tr>'
              }
            </tbody>
          </table>
          <div class="footer">${escapeHtml(branding.footerText || "")}</div>
        </body>
      </html>
    `;
  };

  const exportExcel = () => {
    const html = buildReportHtml();
    const blob = new Blob([html], { type: "application/vnd.ms-excel;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `reporte-reloj-${elements.controlDateFrom.value}-${elements.controlDateTo.value}.xls`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const exportPdf = () => {
    const reportWindow = window.open("", "_blank", "width=1080,height=900");
    if (!reportWindow) {
      showToast("El navegador bloqueo la ventana del reporte.", "danger");
      return;
    }

    reportWindow.document.open();
    reportWindow.document.write(buildReportHtml());
    reportWindow.document.close();
    reportWindow.focus();
    window.setTimeout(() => reportWindow.print(), 260);
  };

  const bootPublicMode = () => {
    elements.clockPageTitle.textContent = "Reloj";
    elements.publicClockPanel.hidden = false;
    elements.controlClockPanel.hidden = true;
    elements.clockBackButton.textContent = "Volver al login";
    elements.clockBackButton.addEventListener("click", () => {
      window.location.href = "/App/Login";
    });

    elements.clockCedula.addEventListener("input", (event) => {
      event.target.value = sanitizeCedula(event.target.value);
      const currentCedula = event.target.value.trim();

      if (!currentCedula) {
        clearPublicStatus();
        renderEmployeeCard();
        renderHistoryCard();
        setNotice("Ingresa la cedula para cargar al colaborador.");
        return;
      }

      if (state.lastResolvedCedula && state.lastResolvedCedula !== currentCedula) {
        clearPublicStatus();
        renderEmployeeCard();
        renderHistoryCard();
      }

      setNotice(
        isCompleteCedula(currentCedula)
          ? "Presiona Buscar o Enter para cargar los datos del colaborador."
          : "Completa la cedula para buscar al colaborador.",
      );
    });

    elements.clockForm.addEventListener("submit", (event) => {
      event.preventDefault();
      loadStatus();
    });
    elements.clockEntryButton.addEventListener("click", () => submitMark("ENTRADA"));
    elements.clockExitButton.addEventListener("click", () => submitMark("SALIDA"));
    elements.clockCedula.focus();
    renderEmployeeCard();
    renderHistoryCard();
    updatePublicActionButtons();
    setNotice("Ingresa la cedula y presiona Buscar o Enter para cargar al colaborador.");
  };

  const bootControlMode = async () => {
    const session = sessionApi.getSession();
    if (!session) {
      window.location.href = "/App/Login";
      return;
    }

    elements.clockPageTitle.textContent = "Reloj RRHH";
    elements.publicClockPanel.hidden = true;
    elements.controlClockPanel.hidden = false;
    elements.clockSessionPill.hidden = false;
    elements.clockLogoutButton.hidden = false;
    elements.clockSessionUser.textContent = session.displayName || session.user || "Usuario";
    elements.clockSessionMeta.textContent = session.rolesLabel || "Sesion activa";

    const today = new Date().toISOString().slice(0, 10);
    const weekAgo = new Date(Date.now() - 6 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
    elements.controlDateFrom.value = weekAgo;
    elements.controlDateTo.value = today;

    elements.clockBackButton.textContent = "Volver a RRHH";
    elements.clockBackButton.addEventListener("click", () => {
      window.location.href = "/App/Rrhh";
    });

    elements.clockLogoutButton.addEventListener("click", async () => {
      elements.clockLogoutButton.disabled = true;
      try {
        await sessionApi.logout();
      } finally {
        window.location.href = "/App/Login";
      }
    });

    try {
      const catalogs = await clockService.getCatalogs();
      state.branding = catalogs?.branding || null;
      fillEmployeeFilter(catalogs?.employees || []);
      await loadControlSummary();
    } catch (error) {
      showToast(error.message || "No se pudo iniciar el control del reloj.", "danger");
    }

    elements.controlSearch.addEventListener("input", () => {
      window.clearTimeout(state.controlSearchTimer);
      state.controlSearchTimer = window.setTimeout(loadControlSummary, 260);
    });
    elements.controlDateFrom.addEventListener("change", loadControlSummary);
    elements.controlDateTo.addEventListener("change", loadControlSummary);
    elements.controlEmployeeFilter.addEventListener("change", loadControlSummary);
    elements.controlRefreshButton.addEventListener("click", loadControlSummary);
    elements.controlExportExcelButton.addEventListener("click", exportExcel);
    elements.controlExportPdfButton.addEventListener("click", exportPdf);
    elements.controlTableBody.addEventListener("click", (event) => {
      const row = event.target.closest("[data-row-index]");
      if (!row) {
        return;
      }

      state.selectedControlIndex = Number(row.dataset.rowIndex);
      renderControlTable();
    });
  };

  if (isControlMode) {
    bootControlMode();
  } else {
    bootPublicMode();
  }
})();
