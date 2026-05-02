(() => {
  const sessionApi = window.SifnicSession;

  const state = {
    session: null,
    catalogs: null,
    items: [],
    selectedId: null,
    selectedDetail: null,
    activeScreen: "activa",
    activeDetailTab: "resumen",
    assignmentMode: "assign",
    regulatoryRows: [],
  };

  const $ = (id) => document.getElementById(id);
  const nodes = {
    backToDashboard: $("backToDashboard"),
    closeSession: $("closeSession"),
    themeToggle: $("themeToggle"),
    themeToggleLabel: $("themeToggleLabel"),
    sessionUser: $("sessionUser"),
    sessionMeta: $("sessionMeta"),
    searchInput: $("searchInput"),
    statusFilter: $("statusFilter"),
    officerField: $("officerField"),
    officerFilter: $("officerFilter"),
    cutoffDate: $("cutoffDate"),
    refreshButton: $("refreshButton"),
    clearButton: $("clearButton"),
    regulatoryButton: $("regulatoryButton"),
    portfolioWorkspace: $("portfolioWorkspace"),
    portfolioModeButton: $("portfolioModeButton"),
    overdueModeButton: $("overdueModeButton"),
    restructuredModeButton: $("restructuredModeButton"),
    unassignedModeButton: $("unassignedModeButton"),
    analyticsModeButton: $("analyticsModeButton"),
    portfolioModeMeta: $("portfolioModeMeta"),
    overdueModeMeta: $("overdueModeMeta"),
    restructuredModeMeta: $("restructuredModeMeta"),
    unassignedModeMeta: $("unassignedModeMeta"),
    recoverySegmentMeta: $("recoverySegmentMeta"),
    metricScope: $("metricScope"),
    metricCredits: $("metricCredits"),
    metricBalance: $("metricBalance"),
    metricOverdue: $("metricOverdue"),
    metricOverdueCredits: $("metricOverdueCredits"),
    metricUnassigned: $("metricUnassigned"),
    portfolioInsightStrip: $("portfolioInsightStrip"),
    portfolioTableCard: $("portfolioTableCard"),
    analyticsPanel: $("analyticsPanel"),
    tableEyebrow: $("tableEyebrow"),
    tableTitle: $("tableTitle"),
    tableCounter: $("tableCounter"),
    tableBody: $("tableBody"),
    detailPanel: $("detailPanel"),
    detailToggleButton: $("detailToggleButton"),
    detailToggleIcon: $("detailToggleIcon"),
    detailTitle: $("detailTitle"),
    detailStatus: $("detailStatus"),
    detailRisk: $("detailRisk"),
    detailBody: $("detailBody"),
    nextQuotaBody: $("nextQuotaBody"),
    paymentsBody: $("paymentsBody"),
    planBody: $("planBody"),
    ratesBody: $("ratesBody"),
    managementBody: $("managementBody"),
    managementButton: $("managementButton"),
    statementButton: $("statementButton"),
    planButton: $("planButton"),
    assignButton: $("assignButton"),
    unassignButton: $("unassignButton"),
    prepareCashButton: $("prepareCashButton"),
    refreshDetailButton: $("refreshDetailButton"),
    moraBucketGrid: $("moraBucketGrid"),
    moraPanelCounter: $("moraPanelCounter"),
    moraBody: $("moraBody"),
    recoveryDate: $("recoveryDate"),
    refreshRecoveryButton: $("refreshRecoveryButton"),
    recoveryProgress: $("recoveryProgress"),
    recoverySummary: $("recoverySummary"),
    recoveryBody: $("recoveryBody"),
    managementBackdrop: $("managementBackdrop"),
    managementClose: $("managementClose"),
    managementCancel: $("managementCancel"),
    managementForm: $("managementForm"),
    managementTitle: $("managementTitle"),
    managementCreditTitle: $("managementCreditTitle"),
    managementType: $("managementType"),
    managementResult: $("managementResult"),
    promiseDate: $("promiseDate"),
    promiseAmount: $("promiseAmount"),
    managementObservation: $("managementObservation"),
    managementMessage: $("managementMessage"),
    managementSubmit: $("managementSubmit"),
    assignmentBackdrop: $("assignmentBackdrop"),
    assignmentClose: $("assignmentClose"),
    assignmentCancel: $("assignmentCancel"),
    assignmentForm: $("assignmentForm"),
    assignmentTitle: $("assignmentTitle"),
    assignmentCreditTitle: $("assignmentCreditTitle"),
    assignmentOfficerField: $("assignmentOfficerField"),
    assignmentOfficer: $("assignmentOfficer"),
    assignmentReason: $("assignmentReason"),
    assignmentObservation: $("assignmentObservation"),
    assignmentMessage: $("assignmentMessage"),
    assignmentSubmit: $("assignmentSubmit"),
    regulatoryBackdrop: $("regulatoryBackdrop"),
    regulatoryClose: $("regulatoryClose"),
    regulatoryCutoffDate: $("regulatoryCutoffDate"),
    regulatoryClosureType: $("regulatoryClosureType"),
    regulatoryReprocess: $("regulatoryReprocess"),
    regulatoryPreview: $("regulatoryPreview"),
    regulatoryPersist: $("regulatoryPersist"),
    regulatoryExport: $("regulatoryExport"),
    regulatoryMessage: $("regulatoryMessage"),
    regulatoryTotals: $("regulatoryTotals"),
    regulatorySummary: $("regulatorySummary"),
    regulatoryBody: $("regulatoryBody"),
  };

  const money = (value) =>
    new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value || 0));
  const currencyMoney = (currency, value) => `${escapeHtml(currency || "NIO")} ${money(value)}`;
  const date = (value) => {
    if (!value) return "-";
    try {
      return new Intl.DateTimeFormat("es-NI", { day: "2-digit", month: "2-digit", year: "numeric", timeZone: "America/Managua" }).format(new Date(value));
    } catch {
      return String(value).slice(0, 10);
    }
  };
  const isoToday = () => {
    const now = new Date();
    const offset = now.getTimezoneOffset() * 60000;
    return new Date(now.getTime() - offset).toISOString().slice(0, 10);
  };
  const cleanText = (value) =>
    String(value ?? "")
      .replaceAll("CrÃƒÂ©dito", "Credito")
      .replaceAll("crÃƒÂ©dito", "credito")
      .replaceAll("ÃƒÂ©", "e")
      .replaceAll("ÃƒÂ¡", "a")
      .replaceAll("ÃƒÂ­", "i")
      .replaceAll("ÃƒÂ³", "o")
      .replaceAll("ÃƒÂº", "u")
      .replaceAll("ÃƒÂ±", "n");
  const escapeHtml = (value) =>
    cleanText(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const STATUS_LABELS = {
    CA: "Cancelado",
    PR: "Prorrogado",
    RR: "Reestructurado",
    SA: "Saneado",
    VE: "Vencido",
    VI: "Vigente",
  };

  const statusLabel = (value) => {
    const code = String(value || "").toUpperCase();
    return STATUS_LABELS[code] ? STATUS_LABELS[code] : (value || "-");
  };

  const statusKind = (value) => {
    const code = String(value || "").toUpperCase();
    if (code === "VE") return "danger";
    if (code === "VI") return "ok";
    if (code === "RR" || code === "PR") return "warning";
    return "";
  };

  const moraBucket = (days) => {
    const value = Number(days || 0);
    if (value <= 0) return { key: "SIN", label: "Sin mora", range: "0 dias", kind: "ok" };
    if (value <= 30) return { key: "A", label: "Tramo A", range: "1 a 30 dias", kind: "ok" };
    if (value <= 60) return { key: "B", label: "Tramo B", range: "31 a 60 dias", kind: "warning" };
    if (value <= 90) return { key: "C", label: "Tramo C", range: "61 a 90 dias", kind: "warning" };
    if (value <= 180) return { key: "D", label: "Tramo D", range: "91 a 180 dias", kind: "danger" };
    return { key: "E", label: "Tramo E", range: "Mas de 180 dias", kind: "danger" };
  };

  const badge = (value, kind = "") =>
    `<span class="status-pill ${kind ? `status-${escapeHtml(kind)}` : ""}">${escapeHtml(value || "-")}</span>`;

  const detailItem = (label, value) => `
    <article class="portfolio2-detail-item">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(value ?? "-")}</strong>
    </article>`;

  const overdueItems = () => state.items.filter((item) => Number(item.overdueDays || 0) >= 1 || Number(item.overdueBalance || 0) > 0);
  const restructuredItems = () => state.items.filter((item) => ["RR", "PR"].includes(String(item.status || "").toUpperCase()));
  const unassignedItems = () => state.items.filter((item) => !item.officerId && !item.officerName && !item.officerUser);

  const visibleItems = () => {
    if (state.activeScreen === "mora") return overdueItems();
    if (state.activeScreen === "reestructurados") return restructuredItems();
    if (state.activeScreen === "sin-oficial") return unassignedItems();
    return state.items;
  };

  const params = () => {
    const query = new URLSearchParams();
    query.set("search", nodes.searchInput.value.trim());
    query.set("status", nodes.statusFilter.value || "TODOS");
    query.set("cutoffDate", nodes.cutoffDate.value || isoToday());
    if (!nodes.officerField.hidden && nodes.officerFilter.value) query.set("officerId", nodes.officerFilter.value);
    return query.toString();
  };

  const setOptions = (select, items, includeAll = false, getValue = (item) => item, getText = (item) => item, allValue = "TODOS", allText = "TODOS") => {
    const values = includeAll
      ? [{ value: allValue, text: allText }, ...items.map((item) => ({ value: getValue(item), text: getText(item) }))]
      : items.map((item) => ({ value: getValue(item), text: getText(item) }));
    select.innerHTML = values.map((item) => `<option value="${escapeHtml(item.value)}">${escapeHtml(item.text)}</option>`).join("");
  };

  const updateSegments = () => {
    const counts = {
      activa: state.items.length,
      mora: overdueItems().length,
      reestructurados: restructuredItems().length,
      sinOficial: unassignedItems().length,
    };
    nodes.portfolioModeMeta.textContent = counts.activa;
    nodes.overdueModeMeta.textContent = counts.mora;
    nodes.restructuredModeMeta.textContent = counts.reestructurados;
    nodes.unassignedModeMeta.textContent = counts.sinOficial;
    document.querySelectorAll(".portfolio2-segment").forEach((button) => {
      button.classList.toggle("is-active", button.dataset.screen === state.activeScreen);
    });
    nodes.portfolioWorkspace.dataset.screen = state.activeScreen;
    nodes.analyticsPanel.hidden = state.activeScreen !== "analitica";
    nodes.portfolioTableCard.hidden = state.activeScreen === "analitica";
  };

  const renderSummary = (summary) => {
    nodes.metricScope.textContent = summary.scope === "GLOBAL" ? "GLOBAL" : "ASIGNADA";
    nodes.metricCredits.textContent = summary.totalCredits || 0;
    nodes.metricBalance.textContent = money(summary.capitalBalance);
    nodes.metricOverdue.textContent = money(summary.overdueBalance);
    nodes.metricOverdueCredits.textContent = summary.overdueCredits || 0;
    nodes.metricUnassigned.textContent = summary.unassignedCredits || 0;
  };

  const renderInsights = () => {
    const loan = state.selectedDetail?.loan;
    if (!loan) {
      nodes.portfolioInsightStrip.innerHTML = `
        <article>
          <span>Foco operativo</span>
          <strong>Filtra, selecciona un credito y gestiona segun prioridad.</strong>
        </article>
        <article>
          <span>Accion sugerida</span>
          <strong>La vista Mora muestra solo creditos con 1+ dia de atraso.</strong>
        </article>`;
      return;
    }

    const hasMora = Number(loan.overdueDays || 0) >= 1 || Number(loan.overdueBalance || 0) > 0;
    const bucket = moraBucket(loan.overdueDays);
    nodes.portfolioInsightStrip.innerHTML = `
      <article class="${hasMora ? "is-danger" : "is-ok"}">
        <span>Foco operativo</span>
        <strong>${hasMora ? `${bucket.label}: ${loan.overdueDays || 0} dia(s), ${currencyMoney(loan.currency, loan.overdueBalance)} vencido.` : `Al dia: proxima cuota ${date(loan.nextDueDate)}.`}</strong>
      </article>
      <article>
        <span>Accion sugerida</span>
        <strong>${hasMora ? "Priorizar contacto, promesa y seguimiento." : "Mantener seguimiento preventivo."}</strong>
      </article>`;
  };

  const renderTable = () => {
    updateSegments();
    const rows = visibleItems();
    const titles = {
      activa: ["Cartera activa", "Creditos activos"],
      mora: ["Mora / Cobranza", "Creditos con mora de 1+ dia"],
      reestructurados: ["Reestructurados", "Creditos reestructurados o prorrogados"],
      "sin-oficial": ["Sin oficial", "Creditos pendientes de asignacion"],
      analitica: ["Analitica", "Recuperacion y tramos"],
    };
    const title = titles[state.activeScreen] || titles.activa;
    nodes.tableEyebrow.textContent = title[0];
    nodes.tableTitle.textContent = title[1];
    nodes.tableCounter.textContent = `${rows.length} registro${rows.length === 1 ? "" : "s"}`;
    if (!rows.length) {
      nodes.tableBody.innerHTML = `<tr><td colspan="9">No hay creditos para esta vista y filtros.</td></tr>`;
      return;
    }

    nodes.tableBody.innerHTML = rows.map((item) => {
      const selected = Number(item.id) === Number(state.selectedId);
      const bucket = moraBucket(item.overdueDays);
      const overdueKind = Number(item.overdueBalance || 0) > 0 ? "danger" : "ok";
      return `
        <tr data-id="${item.id}" class="${selected ? "is-selected" : ""}">
          <td><strong>${escapeHtml(item.number)}</strong><br><span>${escapeHtml(item.product || "MICROCREDITO")}</span></td>
          <td>${escapeHtml(item.clientName)}<br><span>${escapeHtml(item.clientIdentification)}</span></td>
          <td>${escapeHtml(item.officerName || item.officerUser || "Sin asignar")}</td>
          <td>${currencyMoney(item.currency, item.capitalBalance)}<br><span>${item.termMonths || 0} meses / ${money(item.annualRate)}%</span></td>
          <td>${badge(currencyMoney(item.currency, item.overdueBalance), overdueKind)}<br><span>${item.overdueDays || 0} dias</span></td>
          <td>${date(item.nextDueDate)}<br><span>Cuota ${item.nextInstallment || "-"}</span></td>
          <td>${date(item.lastPaymentDate)}<br><span>${item.lastPaymentReceipt ? `${escapeHtml(item.lastPaymentReceipt)} / ${money(item.lastPaymentAmount)}` : "Sin pagos"}</span></td>
          <td>${badge(state.activeScreen === "mora" ? bucket.label : `${item.riskLevel || "MEDIO"} / ${item.conamiClassification || "A"}`, overdueKind)}</td>
          <td>${badge(statusLabel(item.status), statusKind(item.status))}</td>
        </tr>`;
    }).join("");
  };

  const renderMoraBuckets = () => {
    const rows = overdueItems();
    const seed = [
      { key: "A", sample: 1 },
      { key: "B", sample: 31 },
      { key: "C", sample: 61 },
      { key: "D", sample: 91 },
      { key: "E", sample: 181 },
    ];
    nodes.moraBucketGrid.innerHTML = seed.map((meta) => {
      const bucket = moraBucket(meta.sample);
      const bucketRows = rows.filter((item) => moraBucket(item.overdueDays).key === meta.key);
      const total = bucketRows.reduce((sum, item) => sum + Number(item.overdueBalance || 0), 0);
      return `
        <article class="mora-bucket mora-${escapeHtml(bucket.kind)}">
          <span>${escapeHtml(bucket.label)}</span>
          <strong>${bucketRows.length}</strong>
          <small>${escapeHtml(bucket.range)}</small>
          <em>${money(total)}</em>
        </article>`;
    }).join("");
    if (nodes.moraPanelCounter) nodes.moraPanelCounter.textContent = `${rows.length} creditos`;
  };

  const renderDetailTabs = () => {
    document.querySelectorAll("[data-detail-tab]").forEach((button) => {
      button.classList.toggle("is-active", button.dataset.detailTab === state.activeDetailTab);
    });
    document.querySelectorAll("[data-detail-panel]").forEach((panel) => {
      panel.classList.toggle("is-active", panel.dataset.detailPanel === state.activeDetailTab);
    });
  };

  const renderDetail = () => {
    const detail = state.selectedDetail;
    if (!detail) {
      nodes.detailTitle.textContent = "Sin seleccion";
      nodes.detailStatus.textContent = "-";
      nodes.detailRisk.textContent = "-";
      nodes.detailBody.innerHTML = "";
      nodes.nextQuotaBody.innerHTML = "";
      nodes.paymentsBody.innerHTML = "<p>Selecciona un credito para ver pagos.</p>";
      nodes.planBody.innerHTML = "<p>Selecciona un credito para ver el plan.</p>";
      nodes.ratesBody.innerHTML = "";
      nodes.managementBody.innerHTML = "<p>Selecciona un credito para gestionar seguimiento.</p>";
      nodes.assignButton.hidden = !state.catalogs?.canSeeFullPortfolio;
      nodes.unassignButton.hidden = !state.catalogs?.canSeeFullPortfolio;
      renderInsights();
      return;
    }

    const loan = detail.loan;
    const totalDebt = Number(loan.capitalBalance || 0) + Number(loan.interestBalance || 0) + Number(loan.moraBalance || 0) +
      Number(loan.chargeBalance || 0) + Number(loan.commissionBalance || 0);
    nodes.detailTitle.textContent = loan.number || "Credito";
    nodes.detailStatus.textContent = statusLabel(loan.status);
    nodes.detailStatus.className = `status-pill status-${statusKind(loan.status)}`;
    nodes.detailRisk.textContent = `${loan.riskLevel || "MEDIO"} / ${loan.conamiClassification || "A"}`;
    nodes.assignButton.hidden = !state.catalogs?.canSeeFullPortfolio;
    nodes.unassignButton.hidden = !state.catalogs?.canSeeFullPortfolio;

    nodes.detailBody.innerHTML = [
      detailItem("Credito", loan.number),
      detailItem("Cliente", loan.clientName),
      detailItem("Identificacion", loan.clientIdentification),
      detailItem("Producto", loan.product),
      detailItem("Oficial", loan.officerName || loan.officerUser || "Sin asignar"),
      detailItem("Estado", statusLabel(loan.status)),
      detailItem("Riesgo", `${loan.riskLevel || "MEDIO"} / ${loan.conamiClassification || "A"}`),
      detailItem("Saldo capital", currencyMoney(loan.currency, loan.capitalBalance)),
      detailItem("Saldo vencido", currencyMoney(loan.currency, loan.overdueBalance)),
      detailItem("Total adeudado", currencyMoney(loan.currency, totalDebt)),
      detailItem("Desembolso", date(loan.disbursementDate)),
      detailItem("Plazo", `${loan.termMonths || 0} meses`),
      detailItem("Tasa", `${money(loan.annualRate)}%`),
      detailItem("Destino", loan.destination || "-"),
    ].join("");
    nodes.nextQuotaBody.innerHTML = [
      detailItem("Proxima cuota", `Cuota ${loan.nextInstallment || "-"}`),
      detailItem("Fecha proxima", date(loan.nextDueDate)),
      detailItem("Dias mora", `${loan.overdueDays || 0}`),
      detailItem("Cuotas vencidas", `${loan.overdueInstallments || 0}`),
    ].join("");

    nodes.paymentsBody.innerHTML = `
      <table class="mini-table portfolio2-mini-table">
        <thead><tr><th>Fecha</th><th>Recibo</th><th>Monto</th><th>Estado</th><th>Canal</th></tr></thead>
        <tbody>
          ${detail.payments.length ? detail.payments.map((payment) => `
            <tr>
              <td>${date(payment.date)}</td>
              <td>${escapeHtml(payment.receipt || "-")}</td>
              <td>${currencyMoney(payment.currency, payment.amount)}</td>
              <td>${escapeHtml(payment.status || "-")}</td>
              <td>${escapeHtml(payment.method || "Caja")}</td>
            </tr>`).join("") : `<tr><td colspan="5">Sin pagos registrados.</td></tr>`}
        </tbody>
      </table>`;

    nodes.planBody.innerHTML = `
      <table class="mini-table portfolio2-mini-table">
        <thead><tr><th>Cuota</th><th>Fecha</th><th>Capital</th><th>Interes</th><th>Pendiente</th><th>Estado</th></tr></thead>
        <tbody>
          ${detail.plan.length ? detail.plan.map((row) => `
            <tr>
              <td>${row.number}</td>
              <td>${date(row.dueDate)}</td>
              <td>${money(row.capital)}</td>
              <td>${money(row.interest)}</td>
              <td>${money(row.pendingTotal)}</td>
              <td>${escapeHtml(row.status || "-")}</td>
            </tr>`).join("") : `<tr><td colspan="6">Sin plan de pago.</td></tr>`}
        </tbody>
      </table>`;

    nodes.ratesBody.innerHTML = `
      <h3>Tasas variables</h3>
      ${detail.rates.length ? `
        <table class="mini-table portfolio2-mini-table">
          <thead><tr><th>Fecha</th><th>Tasa</th><th>Observacion</th></tr></thead>
          <tbody>${detail.rates.map((rate) => `<tr><td>${date(rate.date)}</td><td>${money(rate.annualRate)}%</td><td>${escapeHtml(rate.note || "-")}</td></tr>`).join("")}</tbody>
        </table>` : "<p>Sin cambios de tasa registrados.</p>"}`;

    const bucket = moraBucket(loan.overdueDays);
    nodes.managementBody.innerHTML = `
      <section class="portfolio2-management-summary">
        ${detailItem("Ultima gestion", detail.management?.lastAction || "Sin gestion registrada")}
        ${detailItem("Promesa", detail.management?.promise || "Sin promesa activa")}
        ${detailItem("Tramo", bucket.label)}
        ${detailItem("Siguiente accion", Number(loan.overdueDays || 0) > 0 ? "Contactar y documentar resultado" : "Seguimiento preventivo")}
      </section>`;

    renderInsights();
    renderDetailTabs();
  };

  const renderRecovery = (report) => {
    const data = report || { expected: 0, recovered: 0, pending: 0, progress: 0, rows: [] };
    nodes.recoveryProgress.textContent = `${money(data.progress)}%`;
    nodes.recoverySummary.innerHTML = [
      detailItem("A recuperar", money(data.expected)),
      detailItem("Recuperado", money(data.recovered)),
      detailItem("Pendiente", money(data.pending)),
    ].join("");
    nodes.recoveryBody.innerHTML = (data.rows || []).length
      ? data.rows.map((row) => `
        <tr>
          <td>${escapeHtml(row.officerName || row.officerUser || "Sin asignar")}<br><span>${escapeHtml(row.officerUser || "")}</span></td>
          <td>${row.credits || 0}</td>
          <td>${money(row.expected)}</td>
          <td>${money(row.recovered)}</td>
          <td>${money(row.pending)}</td>
          <td>${money(row.progress)}%</td>
        </tr>`).join("")
      : `<tr><td colspan="6">No hay cuotas programadas para la fecha seleccionada.</td></tr>`;
  };

  const openModal = (node) => {
    if (!node) return;
    node.hidden = false;
    node.classList.add("is-open");
  };

  const closeModal = (node) => {
    if (!node) return;
    node.classList.remove("is-open");
    node.hidden = true;
  };

  const openManagementModal = () => {
    const loan = state.selectedDetail?.loan;
    if (!loan) return;
    nodes.managementCreditTitle.textContent = `${loan.number} / ${loan.clientName}`;
    nodes.managementObservation.value = "";
    nodes.promiseAmount.value = "";
    nodes.promiseDate.value = "";
    nodes.managementMessage.hidden = true;
    openModal(nodes.managementBackdrop);
  };

  const submitManagement = async (event) => {
    event.preventDefault();
    const loan = state.selectedDetail?.loan;
    if (!loan) return;
    try {
      const payload = {
        creditId: loan.id,
        type: nodes.managementType.value,
        result: nodes.managementResult.value,
        promiseDate: nodes.promiseDate.value || null,
        promiseAmount: Number(nodes.promiseAmount.value || 0),
        observation: nodes.managementObservation.value,
      };
      await sessionApi.request("/Cartera/RegistrarGestion", { method: "POST", body: JSON.stringify(payload) });
      closeModal(nodes.managementBackdrop);
      await selectLoan(loan.id);
      state.activeDetailTab = "gestion";
      renderDetailTabs();
    } catch (error) {
      nodes.managementMessage.hidden = false;
      nodes.managementMessage.textContent = error.message || "No se pudo guardar el seguimiento.";
    }
  };

  const openAssignmentModal = (mode) => {
    const loan = state.selectedDetail?.loan;
    if (!loan || !state.catalogs?.canSeeFullPortfolio) return;
    state.assignmentMode = mode;
    nodes.assignmentTitle.textContent = mode === "unassign" ? "Desasignar cartera" : "Asignar cartera";
    nodes.assignmentSubmit.textContent = mode === "unassign" ? "Desasignar" : "Asignar";
    nodes.assignmentOfficerField.hidden = mode === "unassign";
    nodes.assignmentCreditTitle.textContent = `${loan.number} / ${loan.clientName}`;
    nodes.assignmentReason.value = mode === "unassign" ? "Desasignacion operativa" : "Reasignacion operativa";
    nodes.assignmentObservation.value = "";
    nodes.assignmentMessage.hidden = true;
    openModal(nodes.assignmentBackdrop);
  };

  const submitAssignment = async (event) => {
    event.preventDefault();
    const loan = state.selectedDetail?.loan;
    if (!loan) return;
    const endpoint = state.assignmentMode === "unassign" ? "/Cartera/Desasignar" : "/Cartera/Reasignar";
    try {
      await sessionApi.request(endpoint, {
        method: "POST",
        body: JSON.stringify({
          creditId: loan.id,
          officerId: Number(nodes.assignmentOfficer.value || 0),
          reason: nodes.assignmentReason.value,
          observation: nodes.assignmentObservation.value,
        }),
      });
      closeModal(nodes.assignmentBackdrop);
      await loadPortfolio();
      await selectLoan(loan.id);
    } catch (error) {
      nodes.assignmentMessage.hidden = false;
      nodes.assignmentMessage.textContent = error.message || "No se pudo cambiar la asignacion.";
    }
  };

  const renderRegulatory = (data) => {
    const totals = data?.totals || {};
    const summary = data?.summary || [];
    state.regulatoryRows = data?.rows || [];
    nodes.regulatoryTotals.innerHTML = [
      detailItem("Corte", date(data?.cutoffDate)),
      detailItem("Creditos", totals.credits || 0),
      detailItem("Saldo capital", money(totals.capitalBalance)),
      detailItem("Mora", money(totals.overdueAmount)),
      detailItem("Provision", money(totals.provisionAmount)),
      detailItem("Lote", data?.batchId || (data?.persisted ? "Guardado" : "Simulacion")),
    ].join("");
    nodes.regulatorySummary.innerHTML = `
      <h3>Resumen por categoria</h3>
      ${summary.length ? `
        <table class="mini-table"><thead><tr><th>Categoria</th><th>Estado</th><th>Creditos</th><th>Saldo</th><th>Mora</th><th>Provision</th></tr></thead><tbody>
          ${summary.map((row) => `<tr><td>${escapeHtml(row.category || "-")}</td><td>${escapeHtml(row.finalStatus || "-")}</td><td>${row.credits || 0}</td><td>${money(row.capitalBalance)}</td><td>${money(row.overdueAmount)}</td><td>${money(row.provisionAmount)}</td></tr>`).join("")}
        </tbody></table>` : "<p>Sin datos calculados.</p>"}`;
    nodes.regulatoryBody.innerHTML = state.regulatoryRows.length ? state.regulatoryRows.map((row) => `
      <tr>
        <td><strong>${escapeHtml(row.cycle)}</strong></td>
        <td>${escapeHtml(row.clientName)}<br><span>${escapeHtml(row.clientIdentification)}</span></td>
        <td>${escapeHtml(row.sourceStatus)} -> ${escapeHtml(row.finalStatus)}</td>
        <td>${row.overdueDays || 0} dias<br><span>${row.overdueInstallments || 0} cuotas</span></td>
        <td>${badge(`${row.category || "-"} / ${row.classification || "-"}`, row.category === "E" ? "danger" : row.category === "A" ? "ok" : "")}</td>
        <td>${money(row.provisionRate * 100)}%<br><span>${money(row.provisionAmount)}</span></td>
        <td>${money(row.capitalBalance)}<br><span>${money(row.totalBalance)}</span></td>
      </tr>`).join("") : `<tr><td colspan="7">Calcula un corte para ver la cartera regulatoria.</td></tr>`;
  };

  const showRegulatoryMessage = (message, isError = false) => {
    nodes.regulatoryMessage.hidden = false;
    nodes.regulatoryMessage.textContent = message;
    nodes.regulatoryMessage.classList.toggle("is-error", isError);
  };

  const runRegulatory = async (persist) => {
    try {
      nodes.regulatoryPreview.disabled = true;
      nodes.regulatoryPersist.disabled = true;
      const query = new URLSearchParams();
      query.set("fechaCorte", nodes.regulatoryCutoffDate.value || nodes.cutoffDate.value || isoToday());
      query.set("persistir", persist ? "true" : "false");
      query.set("reprocesar", nodes.regulatoryReprocess.checked ? "true" : "false");
      query.set("tipoCierre", nodes.regulatoryClosureType.value || "DIARIO");
      const payload = await sessionApi.request(`/Cartera/ClasificacionRegulatoria?${query.toString()}`);
      renderRegulatory(payload.data);
      showRegulatoryMessage(persist ? `Cierre guardado. Lote ${payload.data.batchId || "-"}.` : "Calculo listo sin guardar cierre.");
    } catch (error) {
      showRegulatoryMessage(error.message || "No se pudo calcular la cartera regulatoria.", true);
    } finally {
      nodes.regulatoryPreview.disabled = false;
      nodes.regulatoryPersist.disabled = !state.catalogs?.canSeeFullPortfolio;
    }
  };

  const openRegulatoryModal = () => {
    nodes.regulatoryCutoffDate.value = nodes.cutoffDate.value || isoToday();
    nodes.regulatoryClosureType.value = "DIARIO";
    nodes.regulatoryPersist.hidden = !state.catalogs?.canSeeFullPortfolio;
    nodes.regulatoryPersist.disabled = !state.catalogs?.canSeeFullPortfolio;
    nodes.regulatoryMessage.hidden = true;
    if (!state.regulatoryRows.length) renderRegulatory({ totals: {}, summary: [], rows: [] });
    openModal(nodes.regulatoryBackdrop);
  };

  const exportRegulatoryCsv = () => {
    if (!state.regulatoryRows.length) {
      showRegulatoryMessage("Primero calcula un corte para exportar.", true);
      return;
    }
    const headers = ["ciclo", "cedula", "cliente", "estado_origen", "estado_final", "dias_mora", "cuotas_vencidas", "categoria", "clasificacion", "porcentaje_provision", "monto_provision", "saldo_capital", "saldo_total", "monto_en_mora"];
    const csvEscape = (value) => `"${String(value ?? "").replaceAll('"', '""')}"`;
    const lines = [
      headers.join(","),
      ...state.regulatoryRows.map((row) => [row.cycle, row.clientIdentification, row.clientName, row.sourceStatus, row.finalStatus, row.overdueDays, row.overdueInstallments, row.category, row.classification, row.provisionRate, row.provisionAmount, row.capitalBalance, row.totalBalance, row.overdueAmount].map(csvEscape).join(",")),
    ];
    const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `cartera-conami-${nodes.regulatoryCutoffDate.value || isoToday()}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const loadCatalogs = async () => {
    const payload = await sessionApi.request("/Cartera/Catalogos");
    state.catalogs = payload.data;
    setOptions(nodes.statusFilter, state.catalogs.statuses || [], true, (item) => item, statusLabel);
    if (state.catalogs.canSeeFullPortfolio) {
      nodes.officerField.hidden = false;
      setOptions(nodes.officerFilter, state.catalogs.officers || [], true, (item) => item.id, (item) => `${item.name || item.user} (${item.user})`, "", "TODOS");
      setOptions(nodes.assignmentOfficer, state.catalogs.officers || [], false, (item) => item.id, (item) => `${item.name || item.user} (${item.user})`);
    } else {
      nodes.officerField.hidden = true;
    }
  };

  const loadPortfolio = async () => {
    const query = params();
    const [summaryPayload, listPayload] = await Promise.all([
      sessionApi.request(`/Cartera/Resumen?${query}`),
      sessionApi.request(`/Cartera/Listar?${query}`),
    ]);
    renderSummary(summaryPayload.data);
    state.items = listPayload.data || [];
    updateSegments();
    const rows = visibleItems();
    if (!rows.some((item) => Number(item.id) === Number(state.selectedId))) {
      state.selectedId = rows[0]?.id || null;
      state.selectedDetail = null;
    }
    renderMoraBuckets();
    renderTable();
    if (state.selectedId) await selectLoan(state.selectedId);
    else renderDetail();
  };

  const loadRecovery = async () => {
    try {
      const query = new URLSearchParams();
      query.set("fecha", nodes.recoveryDate.value || isoToday());
      const payload = await sessionApi.request(`/Cartera/RecuperacionDiaria?${query}`);
      renderRecovery(payload.data);
    } catch (error) {
      nodes.recoveryBody.innerHTML = `<tr><td colspan="6">${escapeHtml(error.message || "No se pudo cargar recuperacion diaria.")}</td></tr>`;
    }
  };

  const selectLoan = async (id) => {
    state.selectedId = Number(id);
    renderTable();
    const payload = await sessionApi.request(`/Cartera/Obtener?id=${encodeURIComponent(id)}&cutoffDate=${encodeURIComponent(nodes.cutoffDate.value || isoToday())}`);
    state.selectedDetail = payload.data;
    renderDetail();
  };

  const switchScreen = async (screen) => {
    state.activeScreen = screen || "activa";
    updateSegments();
    renderMoraBuckets();
    renderTable();
    const rows = visibleItems();
    if (state.activeScreen === "analitica") {
      await loadRecovery();
      renderDetail();
      return;
    }
    if (!rows.some((item) => Number(item.id) === Number(state.selectedId))) {
      state.selectedId = rows[0]?.id || null;
      state.selectedDetail = null;
    }
    if (state.selectedId) await selectLoan(state.selectedId);
    else renderDetail();
  };

  const toggleDetail = () => {
    const collapsed = !nodes.portfolioWorkspace.classList.contains("detail-collapsed");
    nodes.portfolioWorkspace.classList.toggle("detail-collapsed", collapsed);
    nodes.detailPanel.classList.toggle("is-collapsed", collapsed);
    nodes.detailToggleButton.setAttribute("aria-expanded", String(!collapsed));
    nodes.detailToggleIcon.textContent = collapsed ? "Abrir detalle" : "Cerrar";
  };

  const refreshThemeLabel = () => {
    const isDark = document.documentElement.dataset.theme !== "light";
    nodes.themeToggleLabel.textContent = isDark ? "Modo oscuro" : "Modo claro";
    nodes.themeToggle.setAttribute("aria-pressed", String(isDark));
  };

  const initSession = () => {
    state.session = sessionApi.getSession();
    if (!state.session) {
      window.location.href = "/App/Login";
      return false;
    }
    nodes.sessionUser.textContent = state.session.displayName || state.session.user || "Usuario SIFNIC";
    nodes.sessionMeta.textContent = `${state.session.rolesLabel || "Sin rol"} - ${sessionApi.formatDateTime(state.session.loginAt)}`;
    return true;
  };

  const bindEvents = () => {
    nodes.backToDashboard.addEventListener("click", () => (window.location.href = "/App/Dashboard"));
    nodes.closeSession.addEventListener("click", async () => {
      await sessionApi.logout();
      window.location.href = "/App/Login";
    });
    nodes.themeToggle.addEventListener("click", () => {
      window.SifnicTheme?.toggle();
      refreshThemeLabel();
    });
    nodes.refreshButton.addEventListener("click", loadPortfolio);
    nodes.clearButton.addEventListener("click", () => {
      nodes.searchInput.value = "";
      nodes.statusFilter.value = "TODOS";
      if (!nodes.officerField.hidden) nodes.officerFilter.value = "";
      nodes.cutoffDate.value = isoToday();
      loadPortfolio();
    });
    nodes.regulatoryButton.addEventListener("click", openRegulatoryModal);
    [nodes.portfolioModeButton, nodes.overdueModeButton, nodes.restructuredModeButton, nodes.unassignedModeButton, nodes.analyticsModeButton].forEach((button) => {
      button?.addEventListener("click", () => switchScreen(button.dataset.screen));
    });
    nodes.searchInput.addEventListener("input", () => {
      window.clearTimeout(nodes.searchInput._timer);
      nodes.searchInput._timer = window.setTimeout(loadPortfolio, 300);
    });
    nodes.statusFilter.addEventListener("change", loadPortfolio);
    nodes.officerFilter.addEventListener("change", loadPortfolio);
    nodes.cutoffDate.addEventListener("change", loadPortfolio);
    nodes.tableBody.addEventListener("click", (event) => {
      const row = event.target.closest("tr[data-id]");
      if (row) selectLoan(row.dataset.id);
    });
    document.querySelectorAll("[data-detail-tab]").forEach((button) => {
      button.addEventListener("click", () => {
        state.activeDetailTab = button.dataset.detailTab;
        renderDetailTabs();
      });
    });
    nodes.detailToggleButton.addEventListener("click", toggleDetail);
    nodes.refreshDetailButton.addEventListener("click", () => state.selectedId && selectLoan(state.selectedId));
    nodes.managementButton.addEventListener("click", openManagementModal);
    nodes.statementButton.addEventListener("click", () => {
      if (state.selectedId) sessionApi.openWithSession(`/Clientes/EstadoCuentaPrestamoHtml?id=${encodeURIComponent(state.selectedId)}`);
    });
    nodes.planButton.addEventListener("click", () => {
      state.activeDetailTab = "plan";
      renderDetailTabs();
    });
    nodes.prepareCashButton.addEventListener("click", () => {
      const loan = state.selectedDetail?.loan;
      if (loan) window.location.href = `/App/Caja?creditId=${encodeURIComponent(loan.id)}`;
    });
    nodes.assignButton.addEventListener("click", () => openAssignmentModal("assign"));
    nodes.unassignButton.addEventListener("click", () => openAssignmentModal("unassign"));
    nodes.assignmentClose.addEventListener("click", () => closeModal(nodes.assignmentBackdrop));
    nodes.assignmentCancel.addEventListener("click", () => closeModal(nodes.assignmentBackdrop));
    nodes.assignmentForm.addEventListener("submit", submitAssignment);
    nodes.managementClose.addEventListener("click", () => closeModal(nodes.managementBackdrop));
    nodes.managementCancel.addEventListener("click", () => closeModal(nodes.managementBackdrop));
    nodes.managementForm.addEventListener("submit", submitManagement);
    nodes.regulatoryClose.addEventListener("click", () => closeModal(nodes.regulatoryBackdrop));
    nodes.regulatoryPreview.addEventListener("click", () => runRegulatory(false));
    nodes.regulatoryPersist.addEventListener("click", () => runRegulatory(true));
    nodes.regulatoryExport.addEventListener("click", exportRegulatoryCsv);
    nodes.refreshRecoveryButton.addEventListener("click", loadRecovery);
    nodes.recoveryDate.addEventListener("change", loadRecovery);
  };

  const init = async () => {
    document.body.classList.add("modals-ready");
    if (!initSession()) return;
    nodes.cutoffDate.value = isoToday();
    nodes.recoveryDate.value = isoToday();
    bindEvents();
    refreshThemeLabel();
    renderDetailTabs();
    try {
      await loadCatalogs();
      await Promise.all([loadPortfolio(), loadRecovery()]);
    } catch (error) {
      if (error.status === 401 || error.status === 403) {
        alert("Tu sesion no tiene permiso para consultar cartera.");
        window.location.href = "/App/Dashboard";
        return;
      }
      nodes.tableBody.innerHTML = `<tr><td colspan="9">${escapeHtml(error.message || "No se pudo cargar cartera.")}</td></tr>`;
    }
  };

  init();
})();
