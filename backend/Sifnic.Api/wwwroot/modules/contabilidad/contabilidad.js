(() => {
  const sessionApi = window.SifnicSession;

  const state = {
    session: null,
    catalogs: null,
    accounts: [],
    selectedAccount: null,
    entries: [],
    selectedEntry: null,
    fiscalRows: [],
    reportRows: [],
    primRows: [],
    activePanel: "catalogo",
  };

  const $ = (id) => document.getElementById(id);
  const nodes = {
    backToDashboard: $("backToDashboard"),
    closeSession: $("closeSession"),
    themeToggle: $("themeToggle"),
    themeToggleLabel: $("themeToggleLabel"),
    sessionUser: $("sessionUser"),
    sessionMeta: $("sessionMeta"),
    metricAccounts: $("metricAccounts"),
    metricEntries: $("metricEntries"),
    metricPeriods: $("metricPeriods"),
    metricFiscalDocs: $("metricFiscalDocs"),
    metricFiscalIva: $("metricFiscalIva"),
    metricDifference: $("metricDifference"),
    accountSearch: $("accountSearch"),
    accountClassFilter: $("accountClassFilter"),
    accountStatusFilter: $("accountStatusFilter"),
    movementOnlyFilter: $("movementOnlyFilter"),
    newAccountButton: $("newAccountButton"),
    refreshAccountsButton: $("refreshAccountsButton"),
    accountCounter: $("accountCounter"),
    accountTableBody: $("accountTableBody"),
    accountForm: $("accountForm"),
    accountFormTitle: $("accountFormTitle"),
    accountFormStatus: $("accountFormStatus"),
    accountCode: $("accountCode"),
    accountName: $("accountName"),
    accountClass: $("accountClass"),
    accountGroup: $("accountGroup"),
    accountLevel: $("accountLevel"),
    accountNature: $("accountNature"),
    accountLevel1: $("accountLevel1"),
    accountLevel2: $("accountLevel2"),
    accountLevel3: $("accountLevel3"),
    accountMovement: $("accountMovement"),
    accountActive: $("accountActive"),
    accountMessage: $("accountMessage"),
    clearAccountButton: $("clearAccountButton"),
    toggleAccountButton: $("toggleAccountButton"),
    fiscalPeriod: $("fiscalPeriod"),
    fiscalBook: $("fiscalBook"),
    fiscalSearch: $("fiscalSearch"),
    newFiscalRowButton: $("newFiscalRowButton"),
    refreshFiscalButton: $("refreshFiscalButton"),
    exportFiscalButton: $("exportFiscalButton"),
    fiscalCounter: $("fiscalCounter"),
    fiscalTableBody: $("fiscalTableBody"),
    fiscalStatusLine: $("fiscalStatusLine"),
    entryFrom: $("entryFrom"),
    entryTo: $("entryTo"),
    entryOrigin: $("entryOrigin"),
    entrySearch: $("entrySearch"),
    refreshEntriesButton: $("refreshEntriesButton"),
    entryCounter: $("entryCounter"),
    entryTableBody: $("entryTableBody"),
    entryDetailTitle: $("entryDetailTitle"),
    entryDetailBody: $("entryDetailBody"),
    reportType: $("reportType"),
    reportFrom: $("reportFrom"),
    reportTo: $("reportTo"),
    generateReportButton: $("generateReportButton"),
    exportReportButton: $("exportReportButton"),
    printReportButton: $("printReportButton"),
    reportHeading: $("reportHeading"),
    reportHead: $("reportHead"),
    reportBody: $("reportBody"),
    primTableBody: $("primTableBody"),
    exportPrimButton: $("exportPrimButton"),
  };

  const money = (value) =>
    new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value || 0));

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
    return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
  };

  const firstDay = () => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
  };

  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const setOptions = (select, items, includeAll = false) => {
    select.innerHTML = [
      ...(includeAll ? [{ value: "0", label: "TODAS" }] : []),
      ...(items || []),
    ]
      .map((item) => `<option value="${escapeHtml(item.value)}">${escapeHtml(item.label || item.value)}</option>`)
      .join("");
  };

  const setMessage = (node, text, type = "info") => {
    if (!node) return;
    node.hidden = !text;
    node.textContent = text || "";
    node.className = `form-message is-${type}`;
  };

  const request = (url, options = {}) => sessionApi.request(url, options);

  const buildQuery = (params) => {
    const search = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && String(value) !== "") {
        search.set(key, value);
      }
    });
    const query = search.toString();
    return query ? `?${query}` : "";
  };

  const classLabel = (code) => {
    const match = (state.catalogs?.classes || []).find((item) => Number(item.value) === Number(code));
    return match?.label || "-";
  };

  const inferFromCode = () => {
    const code = nodes.accountCode.value.replace(/\D+/g, "");
    nodes.accountCode.value = code;
    if (!code) return;
    const classCode = Number(code.slice(0, 1) || 1);
    nodes.accountClass.value = String(classCode);
    nodes.accountGroup.value = code.slice(0, 2) || classCode;
    nodes.accountLevel.value = code.length;
    nodes.accountNature.value = [1, 5, 8].includes(classCode) ? "D" : "A";
    if (!nodes.accountLevel1.value) nodes.accountLevel1.value = classLabel(classCode).replace(/^\d+\s-\s/, "");
    if (!nodes.accountLevel2.value) nodes.accountLevel2.value = `GRUPO ${code.slice(0, 2) || classCode}`;
  };

  const renderSummary = (summary) => {
    nodes.metricAccounts.textContent = money(summary.activeAccounts).replace(".00", "");
    nodes.metricEntries.textContent = money(summary.activeEntries).replace(".00", "");
    nodes.metricPeriods.textContent = money(summary.openPeriods).replace(".00", "");
    nodes.metricFiscalDocs.textContent = money(summary.fiscalDocuments).replace(".00", "");
    nodes.metricFiscalIva.textContent = money(summary.fiscalIva);
    nodes.metricDifference.textContent = money(summary.difference);
    nodes.metricDifference.classList.toggle("is-danger", Number(summary.difference || 0) !== 0);
  };

  const loadSummary = async () => {
    const payload = await request(`/Contabilidad/Resumen?fecha=${isoToday()}`);
    renderSummary(payload.data);
  };

  const renderAccounts = () => {
    nodes.accountCounter.textContent = `${state.accounts.length} registros`;
    if (!state.accounts.length) {
      nodes.accountTableBody.innerHTML = `<tr><td colspan="7">Sin cuentas para los filtros actuales.</td></tr>`;
      return;
    }

    nodes.accountTableBody.innerHTML = state.accounts
      .map(
        (account) => `
        <tr class="${state.selectedAccount?.code === account.code ? "is-selected" : ""}" data-account-code="${escapeHtml(account.code)}">
          <td><strong>${escapeHtml(account.code)}</strong></td>
          <td>${escapeHtml(account.name)}</td>
          <td>${escapeHtml(account.className || classLabel(account.classCode))}</td>
          <td>${escapeHtml(account.group ?? "-")}</td>
          <td><span class="status-pill">${account.nature === "D" ? "DEUDORA" : "ACREEDORA"}</span></td>
          <td>${account.movementAllowed ? "SI" : "NO"}</td>
          <td>${account.active ? "ACTIVA" : "INACTIVA"}</td>
        </tr>`,
      )
      .join("");

    nodes.accountTableBody.querySelectorAll("[data-account-code]").forEach((row) => {
      row.addEventListener("click", () => {
        const account = state.accounts.find((item) => item.code === row.dataset.accountCode);
        if (account) selectAccount(account);
      });
    });
  };

  const loadAccounts = async () => {
    const query = buildQuery({
      search: nodes.accountSearch.value.trim(),
      accountClass: nodes.accountClassFilter.value,
      status: nodes.accountStatusFilter.value,
      movementOnly: nodes.movementOnlyFilter.checked,
    });
    const payload = await request(`/Contabilidad/ListarCuentas${query}`);
    state.accounts = payload.data || [];
    renderAccounts();
  };

  const clearAccountForm = () => {
    state.selectedAccount = null;
    nodes.accountForm.reset();
    nodes.accountActive.checked = true;
    nodes.accountMovement.checked = true;
    nodes.accountFormTitle.textContent = "Nueva cuenta";
    nodes.accountFormStatus.textContent = "MUC";
    nodes.toggleAccountButton.disabled = true;
    nodes.toggleAccountButton.textContent = "Inactivar";
    nodes.accountCode.disabled = false;
    setMessage(nodes.accountMessage, "");
    renderAccounts();
  };

  const selectAccount = (account) => {
    state.selectedAccount = account;
    nodes.accountFormTitle.textContent = "Editar cuenta";
    nodes.accountFormStatus.textContent = account.active ? "ACTIVA" : "INACTIVA";
    nodes.accountCode.value = account.code || "";
    nodes.accountCode.disabled = true;
    nodes.accountName.value = account.name || "";
    nodes.accountClass.value = String(account.classCode || 1);
    nodes.accountGroup.value = account.group || "";
    nodes.accountLevel.value = account.level || "";
    nodes.accountNature.value = account.nature || "D";
    nodes.accountLevel1.value = account.level1 || "";
    nodes.accountLevel2.value = account.level2 || "";
    nodes.accountLevel3.value = account.level3 || "";
    nodes.accountMovement.checked = Boolean(account.movementAllowed);
    nodes.accountActive.checked = Boolean(account.active);
    nodes.toggleAccountButton.disabled = false;
    nodes.toggleAccountButton.textContent = account.active ? "Inactivar" : "Activar";
    setMessage(nodes.accountMessage, "");
    renderAccounts();
  };

  const saveAccount = async (event) => {
    event.preventDefault();
    const body = {
      code: nodes.accountCode.value,
      name: nodes.accountName.value,
      classCode: Number(nodes.accountClass.value || 0),
      group: Number(nodes.accountGroup.value || 0),
      level: Number(nodes.accountLevel.value || 0),
      nature: nodes.accountNature.value,
      level1: nodes.accountLevel1.value,
      level2: nodes.accountLevel2.value,
      level3: nodes.accountLevel3.value,
      movementAllowed: nodes.accountMovement.checked,
      active: nodes.accountActive.checked,
    };

    try {
      await request("/Contabilidad/GuardarCuenta", { method: "POST", body: JSON.stringify(body) });
      setMessage(nodes.accountMessage, "Cuenta contable guardada.", "success");
      await Promise.all([loadAccounts(), loadSummary()]);
      const saved = state.accounts.find((item) => item.code === body.code.replace(/\D+/g, ""));
      if (saved) selectAccount(saved);
    } catch (error) {
      setMessage(nodes.accountMessage, error.message, "error");
    }
  };

  const toggleAccount = async () => {
    if (!state.selectedAccount) return;
    const active = !state.selectedAccount.active;
    try {
      await request("/Contabilidad/CambiarEstadoCuenta", {
        method: "POST",
        body: JSON.stringify({ code: state.selectedAccount.code, active }),
      });
      await Promise.all([loadAccounts(), loadSummary()]);
      const updated = state.accounts.find((item) => item.code === state.selectedAccount.code);
      if (updated) selectAccount(updated);
    } catch (error) {
      setMessage(nodes.accountMessage, error.message, "error");
    }
  };

  const fiscalTemplate = () => ({
    id: null,
    period: nodes.fiscalPeriod.value || isoToday().slice(0, 7),
    book: ["TODOS", "0", ""].includes(nodes.fiscalBook.value) ? "COMPRAS_IVA" : nodes.fiscalBook.value,
    documentType: "FACTURA",
    documentNumber: "",
    documentDate: isoToday(),
    ruc: "",
    name: "",
    description: "",
    incomeWithoutIva: 0,
    ivaAmount: 0,
    rowCode: "",
    taxableBase: 0,
    retainedAmount: 0,
    retentionRate: 0,
    retentionCode: "",
    accountCode: "",
    status: "BORRADOR",
  });

  const fiscalColumns = [
    ["book", "select", () => state.catalogs?.fiscalBooks || []],
    ["documentDate", "date"],
    ["documentType", "select", () => state.catalogs?.fiscalDocumentTypes || []],
    ["documentNumber", "text"],
    ["ruc", "text"],
    ["name", "text"],
    ["description", "text"],
    ["incomeWithoutIva", "number"],
    ["ivaAmount", "number"],
    ["rowCode", "text"],
    ["taxableBase", "number"],
    ["retainedAmount", "number"],
    ["retentionRate", "number"],
    ["retentionCode", "text"],
    ["accountCode", "text"],
    ["status", "select", () => state.catalogs?.fiscalStatuses || []],
  ];

  const fiscalValue = (row, key) => {
    const value = row[key];
    if (key === "documentDate") return String(value || "").slice(0, 10);
    if (["incomeWithoutIva", "ivaAmount", "taxableBase", "retainedAmount", "retentionRate"].includes(key)) {
      return Number(value || 0);
    }
    return value ?? "";
  };

  const renderFiscalInput = (row, key, type, optionsProvider) => {
    const value = fiscalValue(row, key);
    if (type === "select") {
      const options = (optionsProvider?.() || []).filter((item) => item.value !== "TODOS");
      return `<select data-fiscal-field="${key}">${options
        .map((item) => `<option value="${escapeHtml(item.value)}" ${String(value) === String(item.value) ? "selected" : ""}>${escapeHtml(item.label || item.value)}</option>`)
        .join("")}</select>`;
    }

    return `<input data-fiscal-field="${key}" type="${type}" value="${escapeHtml(value)}" ${type === "number" ? 'step="0.01" min="0"' : ""} />`;
  };

  const renderFiscalRows = () => {
    nodes.fiscalCounter.textContent = `${state.fiscalRows.length} filas`;
    if (!state.fiscalRows.length) {
      nodes.fiscalTableBody.innerHTML = `<tr><td colspan="17">Sin documentos fiscales para el periodo.</td></tr>`;
      return;
    }

    nodes.fiscalTableBody.innerHTML = state.fiscalRows
      .map(
        (row, index) => `
        <tr data-fiscal-index="${index}" class="${row.id ? "" : "is-draft"}">
          ${fiscalColumns.map(([key, type, optionsProvider]) => `<td>${renderFiscalInput(row, key, type, optionsProvider)}</td>`).join("")}
          <td class="fiscal-actions-cell">
            <button class="ghost-button fiscal-save-row" type="button">Guardar</button>
            <button class="danger-button fiscal-void-row" type="button" ${row.id ? "" : "disabled"}>Anular</button>
          </td>
        </tr>`,
      )
      .join("");

    nodes.fiscalTableBody.querySelectorAll("[data-fiscal-index]").forEach((tr) => bindFiscalRow(tr));
  };

  const bindFiscalRow = (tr) => {
    const index = Number(tr.dataset.fiscalIndex);
    tr.querySelectorAll("[data-fiscal-field]").forEach((input) => {
      input.addEventListener("input", () => {
        const key = input.dataset.fiscalField;
        state.fiscalRows[index][key] = input.type === "number" ? Number(input.value || 0) : input.value;
      });
      input.addEventListener("keydown", (event) => moveFiscalCell(event, input));
    });
    tr.querySelector(".fiscal-save-row")?.addEventListener("click", () => saveFiscalRow(index).catch(showFiscalError));
    tr.querySelector(".fiscal-void-row")?.addEventListener("click", () => voidFiscalRow(index).catch(showFiscalError));
  };

  const moveFiscalCell = (event, input) => {
    const keys = ["Enter", "ArrowRight", "ArrowLeft", "ArrowDown", "ArrowUp"];
    if (!keys.includes(event.key)) return;
    const cells = Array.from(nodes.fiscalTableBody.querySelectorAll("[data-fiscal-field]"));
    const current = cells.indexOf(input);
    if (current < 0) return;
    event.preventDefault();
    const rowWidth = fiscalColumns.length;
    const nextIndex =
      event.key === "ArrowLeft" ? current - 1 :
      event.key === "ArrowUp" ? current - rowWidth :
      event.key === "ArrowDown" ? current + rowWidth :
      current + 1;
    cells[Math.max(0, Math.min(cells.length - 1, nextIndex))]?.focus();
  };

  const loadFiscalRows = async () => {
    const query = buildQuery({
      periodo: nodes.fiscalPeriod.value || isoToday().slice(0, 7),
      libro: nodes.fiscalBook.value,
      search: nodes.fiscalSearch.value.trim(),
    });
    const payload = await request(`/Contabilidad/ListarDocumentosFiscales${query}`);
    state.fiscalRows = payload.data || [];
    renderFiscalRows();
  };

  const addFiscalRow = () => {
    state.fiscalRows = [fiscalTemplate(), ...state.fiscalRows];
    renderFiscalRows();
    nodes.fiscalTableBody.querySelector("[data-fiscal-field='documentNumber']")?.focus();
  };

  const saveFiscalRow = async (index) => {
    const row = state.fiscalRows[index];
    const payload = {
      id: row.id,
      period: nodes.fiscalPeriod.value || row.period,
      book: row.book,
      documentType: row.documentType,
      documentNumber: row.documentNumber,
      documentDate: row.documentDate,
      ruc: row.ruc,
      name: row.name,
      description: row.description,
      incomeWithoutIva: Number(row.incomeWithoutIva || 0),
      ivaAmount: Number(row.ivaAmount || 0),
      rowCode: row.rowCode,
      taxableBase: Number(row.taxableBase || 0),
      retainedAmount: Number(row.retainedAmount || 0),
      retentionRate: Number(row.retentionRate || 0),
      retentionCode: row.retentionCode,
      accountCode: row.accountCode,
      status: row.status,
    };
    await request("/Contabilidad/GuardarDocumentoFiscal", { method: "POST", body: JSON.stringify(payload) });
    setMessage(nodes.fiscalStatusLine, "Fila fiscal guardada.", "success");
    await Promise.all([loadFiscalRows(), loadSummary()]);
  };

  const voidFiscalRow = async (index) => {
    const row = state.fiscalRows[index];
    if (!row?.id) return;
    await request("/Contabilidad/AnularDocumentoFiscal", { method: "POST", body: JSON.stringify({ id: row.id }) });
    setMessage(nodes.fiscalStatusLine, "Fila fiscal anulada.", "success");
    await Promise.all([loadFiscalRows(), loadSummary()]);
  };

  const showFiscalError = (error) => {
    setMessage(nodes.fiscalStatusLine, error.message || "No se pudo procesar la fila fiscal.", "error");
  };

  const renderEntries = () => {
    nodes.entryCounter.textContent = `${state.entries.length} registros`;
    if (!state.entries.length) {
      nodes.entryTableBody.innerHTML = `<tr><td colspan="7">No hay asientos en el rango.</td></tr>`;
      return;
    }

    nodes.entryTableBody.innerHTML = state.entries
      .map(
        (entry) => `
        <tr class="${state.selectedEntry?.entryId === entry.entryId ? "is-selected" : ""}" data-entry-id="${entry.entryId}">
          <td>${date(entry.entryDate)}</td>
          <td><strong>${escapeHtml(entry.reference || "-")}</strong><br><small>${escapeHtml(entry.description || "")}</small></td>
          <td>${escapeHtml(entry.type)}</td>
          <td>${escapeHtml(entry.origin || "-")}</td>
          <td>${money(entry.debit)}</td>
          <td>${money(entry.credit)}</td>
          <td><span class="status-pill ${entry.balanced ? "" : "is-danger"}">${entry.balanced ? "CUADRADO" : "DIF."}</span></td>
        </tr>`,
      )
      .join("");

    nodes.entryTableBody.querySelectorAll("[data-entry-id]").forEach((row) => {
      row.addEventListener("click", () => loadEntryDetail(Number(row.dataset.entryId)));
    });
  };

  const loadEntries = async () => {
    const query = buildQuery({
      desde: nodes.entryFrom.value,
      hasta: nodes.entryTo.value,
      origin: nodes.entryOrigin.value,
      search: nodes.entrySearch.value.trim(),
    });
    const payload = await request(`/Contabilidad/ListarAsientos${query}`);
    state.entries = payload.data || [];
    renderEntries();
  };

  const loadEntryDetail = async (entryId) => {
    const payload = await request(`/Contabilidad/DetalleAsiento?id=${entryId}`);
    state.selectedEntry = { entryId };
    const { header, lines } = payload.data;
    nodes.entryDetailTitle.textContent = `${header.reference || "Asiento"} / ${date(header.entryDate)}`;
    nodes.entryDetailBody.innerHTML = `
      <div class="detail-grid">
        <article class="detail-item"><span>Tipo</span><strong>${escapeHtml(header.type)}</strong></article>
        <article class="detail-item"><span>Origen</span><strong>${escapeHtml(header.origin || "-")}</strong></article>
        <article class="detail-item"><span>Estado</span><strong>${escapeHtml(header.status)}</strong></article>
      </div>
      <div class="mini-table">
        <table>
          <thead><tr><th>Cuenta</th><th>Descripcion</th><th>Debe</th><th>Haber</th></tr></thead>
          <tbody>
            ${(lines || [])
              .map(
                (line) => `<tr>
                  <td><strong>${escapeHtml(line.accountCode)}</strong><br><small>${escapeHtml(line.accountName || "")}</small></td>
                  <td>${escapeHtml(line.description || line.client || "-")}</td>
                  <td>${money(line.debit)}</td>
                  <td>${money(line.credit)}</td>
                </tr>`,
              )
              .join("")}
          </tbody>
        </table>
      </div>`;
    renderEntries();
  };

  const renderReport = (rows, title) => {
    state.reportRows = rows || [];
    nodes.reportHeading.textContent = `${title} - ${state.reportRows.length} filas`;
    if (!state.reportRows.length) {
      nodes.reportHead.innerHTML = "";
      nodes.reportBody.innerHTML = `<tr><td>Sin informacion para el rango seleccionado.</td></tr>`;
      return;
    }

    const columns = Object.keys(state.reportRows[0]);
    nodes.reportHead.innerHTML = `<tr>${columns.map((column) => `<th>${escapeHtml(column)}</th>`).join("")}</tr>`;
    nodes.reportBody.innerHTML = state.reportRows
      .map((row) => `<tr>${columns.map((column) => `<td>${formatCell(row[column])}</td>`).join("")}</tr>`)
      .join("");
  };

  const formatCell = (value) => {
    if (value === null || value === undefined || value === "") return "-";
    if (typeof value === "number") return money(value);
    if (/^\d{4}-\d{2}-\d{2}T/.test(String(value))) return date(value);
    return escapeHtml(value);
  };

  const reportEndpoint = () => {
    const type = nodes.reportType.value;
    if (type === "BALANCE_GENERAL") return `/Contabilidad/BalanceGeneral${buildQuery({ hasta: nodes.reportTo.value })}`;
    if (type === "ESTADO_RESULTADOS") return `/Contabilidad/EstadoResultados${buildQuery({ desde: nodes.reportFrom.value, hasta: nodes.reportTo.value })}`;
    if (type === "BALANCE_COMPROBACION") return `/Contabilidad/BalanceComprobacion${buildQuery({ desde: nodes.reportFrom.value, hasta: nodes.reportTo.value })}`;
    if (type === "CARTERA_CONTABLE") return `/Contabilidad/CarteraContable${buildQuery({ hasta: nodes.reportTo.value })}`;
    return "/Contabilidad/PrimBase";
  };

  const generateReport = async () => {
    const payload = await request(reportEndpoint());
    const label = nodes.reportType.options[nodes.reportType.selectedIndex]?.textContent || "Reporte";
    renderReport(payload.data || [], label);
  };

  const renderPrim = () => {
    if (!state.primRows.length) {
      nodes.primTableBody.innerHTML = `<tr><td colspan="6">Sin mapeo PRIM cargado.</td></tr>`;
      return;
    }

    nodes.primTableBody.innerHTML = state.primRows
      .map(
        (row) => `
        <tr>
          <td><span class="status-pill">${escapeHtml(row.bloque_prim || "-")}</span></td>
          <td><strong>${escapeHtml(row.codigo_cuenta)}</strong></td>
          <td>${escapeHtml(row.nombre_cuenta)}</td>
          <td>${escapeHtml(row.clase)}</td>
          <td>${escapeHtml(row.naturaleza)}</td>
          <td>${row.permite_movimiento ? "SI" : "NO"}</td>
        </tr>`,
      )
      .join("");
  };

  const loadPrim = async () => {
    const payload = await request("/Contabilidad/PrimBase");
    state.primRows = payload.data || [];
    renderPrim();
  };

  const exportCsv = (rows, filename) => {
    if (!rows?.length) return;
    const columns = Object.keys(rows[0]);
    const csv = [
      columns.join(","),
      ...rows.map((row) =>
        columns
          .map((column) => {
            const value = row[column] ?? "";
            return `"${String(value).replaceAll('"', '""')}"`;
          })
          .join(","),
      ),
    ].join("\n");
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  };

  const switchPanel = (panel) => {
    state.activePanel = panel;
    document.querySelectorAll("[data-accounting-view]").forEach((button) => {
      button.classList.toggle("is-active", button.dataset.accountingView === panel);
    });
    document.querySelectorAll("[data-accounting-panel]").forEach((section) => {
      section.classList.toggle("is-active", section.dataset.accountingPanel === panel);
    });
    if (panel === "fiscal" && !state.fiscalRows.length) loadFiscalRows().catch(showFiscalError);
    if (panel === "asientos" && !state.entries.length) loadEntries().catch(console.error);
    if (panel === "reportes" && !state.reportRows.length) generateReport().catch(console.error);
    if (panel === "prim" && !state.primRows.length) loadPrim().catch(console.error);
  };

  const bindEvents = () => {
    nodes.backToDashboard.addEventListener("click", () => (window.location.href = "/App/Dashboard"));
    nodes.closeSession.addEventListener("click", async () => {
      await sessionApi.logout();
      window.location.href = "/App/Login";
    });
    nodes.themeToggle.addEventListener("click", () => window.SifnicTheme?.toggle?.());
    document.querySelectorAll("[data-accounting-view]").forEach((button) => {
      button.addEventListener("click", () => switchPanel(button.dataset.accountingView));
    });
    nodes.accountSearch.addEventListener("input", () => loadAccounts().catch(console.error));
    nodes.accountClassFilter.addEventListener("change", () => loadAccounts().catch(console.error));
    nodes.accountStatusFilter.addEventListener("change", () => loadAccounts().catch(console.error));
    nodes.movementOnlyFilter.addEventListener("change", () => loadAccounts().catch(console.error));
    nodes.refreshAccountsButton.addEventListener("click", () => loadAccounts().catch(console.error));
    nodes.newAccountButton.addEventListener("click", clearAccountForm);
    nodes.accountCode.addEventListener("input", inferFromCode);
    nodes.accountClass.addEventListener("change", inferFromCode);
    nodes.accountForm.addEventListener("submit", saveAccount);
    nodes.clearAccountButton.addEventListener("click", clearAccountForm);
    nodes.toggleAccountButton.addEventListener("click", toggleAccount);
    nodes.fiscalPeriod.addEventListener("change", () => loadFiscalRows().catch(showFiscalError));
    nodes.fiscalBook.addEventListener("change", () => loadFiscalRows().catch(showFiscalError));
    nodes.fiscalSearch.addEventListener("input", () => loadFiscalRows().catch(showFiscalError));
    nodes.newFiscalRowButton.addEventListener("click", addFiscalRow);
    nodes.refreshFiscalButton.addEventListener("click", () => loadFiscalRows().catch(showFiscalError));
    nodes.exportFiscalButton.addEventListener("click", () => exportCsv(state.fiscalRows, `dgi_${nodes.fiscalPeriod.value || "periodo"}.csv`));
    nodes.refreshEntriesButton.addEventListener("click", () => loadEntries().catch(console.error));
    nodes.generateReportButton.addEventListener("click", () => generateReport().catch(console.error));
    nodes.exportReportButton.addEventListener("click", () => exportCsv(state.reportRows, `contabilidad_${nodes.reportType.value.toLowerCase()}.csv`));
    nodes.printReportButton.addEventListener("click", () => window.print());
    nodes.exportPrimButton.addEventListener("click", () => exportCsv(state.primRows, "contabilidad_base_prim.csv"));
  };

  const initSession = () => {
    state.session = sessionApi.getSession();
    if (!state.session) {
      window.location.href = "/App/Login";
      return false;
    }
    nodes.sessionUser.textContent = state.session.displayName || state.session.username || "Usuario";
    nodes.sessionMeta.textContent = `${(state.session.roles || []).join(", ") || "SESION"} - ${sessionApi.formatDateTime(state.session.lastActivityAt || state.session.startedAt)}`;
    return true;
  };

  const initTheme = () => {
    const sync = () => {
      const mode = window.SifnicTheme?.current?.() || "dark";
      nodes.themeToggleLabel.textContent = mode === "dark" ? "Modo oscuro" : "Modo claro";
      nodes.themeToggle.setAttribute("aria-pressed", mode === "light" ? "true" : "false");
    };
    sync();
    window.addEventListener("sifnic-theme-change", sync);
  };

  const init = async () => {
    if (!initSession()) return;
    initTheme();
    bindEvents();
    nodes.entryFrom.value = firstDay();
    nodes.reportFrom.value = firstDay();
    nodes.fiscalPeriod.value = isoToday().slice(0, 7);
    nodes.entryTo.value = isoToday();
    nodes.reportTo.value = isoToday();
    const catalogsPayload = await request("/Contabilidad/Catalogos");
    state.catalogs = catalogsPayload.data || {};
    setOptions(nodes.accountClassFilter, state.catalogs.classes || [], true);
    setOptions(nodes.accountClass, state.catalogs.classes || []);
    setOptions(nodes.accountNature, state.catalogs.natures || []);
    setOptions(nodes.fiscalBook, state.catalogs.fiscalBooks || [], true);
    setOptions(nodes.reportType, [
      { value: "BALANCE_GENERAL", label: "Balance general" },
      { value: "ESTADO_RESULTADOS", label: "Estado de resultado" },
      { value: "BALANCE_COMPROBACION", label: "Balance de comprobacion" },
      { value: "CARTERA_CONTABLE", label: "Cartera contable" },
      { value: "PRIM_BASE", label: "Base PRIM CONAMI" },
    ]);
    clearAccountForm();
    await Promise.all([loadSummary(), loadAccounts()]);
  };

  init().catch((error) => {
    console.error(error);
    alert(error.message || "No se pudo iniciar Contabilidad.");
  });
})();
