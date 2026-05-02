(() => {
  const sessionApi = window.SifnicSession;

  const state = {
    session: null,
    catalogs: null,
    clients: [],
    selectedId: null,
    selectedDetail: null,
    selectedLoanDetail: null,
    clientDetailTab: "resumen",
    formDirty: false,
    formReadOnly: false,
  };

  const $ = (id) => document.getElementById(id);
  const money = (value) =>
    new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value || 0));
  const currencyMoney = (currency, value) => `${escapeHtml(currency || "NIO")} ${money(value)}`;
  const date = (value) => {
    if (!value) return "";
    try {
      return new Intl.DateTimeFormat("es-NI", { day: "2-digit", month: "2-digit", year: "numeric", timeZone: "America/Managua" }).format(new Date(value));
    } catch {
      return String(value).slice(0, 10);
    }
  };
  const isoDate = (value) => (value ? String(value).slice(0, 10) : "");
  const text = (value, fallback = "-") => {
    const safe = String(value ?? "").trim();
    return safe || fallback;
  };
  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const nodes = {
    backToDashboard: $("backToDashboard"),
    closeSession: $("closeSession"),
    themeToggle: $("themeToggle"),
    themeToggleLabel: $("themeToggleLabel"),
    sessionUser: $("sessionUser"),
    sessionMeta: $("sessionMeta"),
    searchInput: $("searchInput"),
    statusFilter: $("statusFilter"),
    typeFilter: $("typeFilter"),
    opsMain: $("opsMain"),
    refreshButton: $("refreshButton"),
    newButton: $("newButton"),
    tableBody: $("tableBody"),
    tableCounter: $("tableCounter"),
    detailTitle: $("detailTitle"),
    detailStatus: $("detailStatus"),
    detailBody: $("detailBody"),
    clientDecisionStrip: $("clientDecisionStrip"),
    clientDetailTabs: $("clientDetailTabs"),
    relatedBody: $("relatedBody"),
    editButton: $("editButton"),
    creditRequestButton: $("creditRequestButton"),
    deleteButton: $("deleteButton"),
    metricClients: $("metricClients"),
    metricApplications: $("metricApplications"),
    metricBalance: $("metricBalance"),
    modalBackdrop: $("modalBackdrop"),
    modalTitle: $("modalTitle"),
    modalClose: $("modalClose"),
    backFormButton: $("backFormButton"),
    cancelFormButton: $("cancelFormButton"),
    clientForm: $("clientForm"),
    formMessage: $("formMessage"),
    deleteBackdrop: $("deleteBackdrop"),
    deleteClose: $("deleteClose"),
    deleteBack: $("deleteBack"),
    deleteCancel: $("deleteCancel"),
    deleteForm: $("deleteForm"),
    deleteReason: $("deleteReason"),
    adminUser: $("adminUser"),
    adminPassword: $("adminPassword"),
    deleteSubmitButton: $("deleteSubmitButton"),
    deleteMessage: $("deleteMessage"),
    detailPanel: $("detailPanel"),
    detailToggleButton: $("detailToggleButton"),
    detailToggleIcon: $("detailToggleIcon"),
  };

  const optionDescriptions = {
    TODOS: "Muestra todos los registros sin filtrar.",
    ACTIVO: "Cliente vigente para operar.",
    INACTIVO: "Cliente no operativo temporalmente.",
    BLOQUEADO: "Cliente restringido por validacion interna o cumplimiento.",
    PROSPECTO: "Cliente en etapa previa a credito.",
    INDIVIDUAL: "Persona natural con relacion individual.",
    GRUPAL: "Cliente asociado a credito grupal.",
    NEGOCIO: "Cliente con actividad economica propia.",
    ASALARIADO: "Cliente con ingreso principal por salario.",
    REMESANTE: "Cliente con ingreso relevante por remesas.",
    CEDULA: "Documento nacional de identidad.",
    RUC: "Registro unico de contribuyente.",
    PASAPORTE: "Documento de identidad internacional.",
    RESIDENCIA: "Documento migratorio de residencia.",
    NUEVO: "Primera relacion comercial.",
    RECURRENTE: "Cliente con historial previo.",
    BAJO: "Riesgo bajo segun informacion y expediente disponible.",
    MEDIO: "Riesgo medio; requiere seguimiento normal.",
    ALTO: "Riesgo alto; requiere mayor debida diligencia.",
    COMPLETO: "Expediente minimo completo para analisis.",
    INCOMPLETO: "Falta informacion obligatoria.",
    OBSERVADO: "Expediente con pendiente o hallazgo.",
  };

  const fieldDescriptions = {
    searchInput: "Busca por cedula, nombre, telefono o correo.",
    statusFilter: "Filtra clientes segun estado operativo.",
    typeFilter: "Filtra por tipo de cliente.",
    cedula: "Identificacion unica. En cedula se infiere fecha de nacimiento cuando aplica.",
    entryDate: "Fecha de ingreso automatica del dia, no editable.",
    fileStatus: "Estado documental del expediente del cliente.",
    riskLevel: "Calculado automaticamente con factores de debida diligencia y capacidad.",
    riskScore: "Puntaje calculado automaticamente; mayor puntaje implica mayor riesgo.",
    sourceOfFunds: "Origen declarado de los recursos del cliente.",
    relationshipPurpose: "Motivo de la relacion comercial con la institucion.",
    isPep: "PEP significa Persona Expuesta Politicamente. Requiere mayor debida diligencia segun enfoque de riesgo.",
  };

  const setFieldHelp = (node, description) => {
    if (!node || !description) return;
    node.removeAttribute("title");
    node.closest(".field, .check-field")?.removeAttribute("data-help");
  };

  const fields = [
    "clientId",
    "identificationType",
    "cedula",
    "branch",
    "clientType",
    "names",
    "lastNames",
    "relationship",
    "status",
    "entryDate",
    "birthDate",
    "gender",
    "civilStatus",
    "spouseName",
    "phone",
    "mobile",
    "secondaryPhone",
    "email",
    "address",
    "homeGeography",
    "occupation",
    "economicActivity",
    "businessName",
    "businessAgeMonths",
    "businessAddress",
    "businessGeography",
    "monthlyIncome",
    "spouseIncome",
    "remittances",
    "rentIncome",
    "otherIncome",
    "monthlyExpenses",
    "riskLevel",
    "riskScore",
    "fileStatus",
    "isPep",
    "sourceOfFunds",
    "relationshipPurpose",
    "notes",
  ].reduce((acc, id) => ({ ...acc, [id]: $(id) }), {});

  const setOptions = (select, values, includeAll = false) => {
    if (!select) return;
    const items = includeAll ? ["TODOS", ...(values || [])] : values || [];
    select.innerHTML = items
      .map((item) => {
        const description = optionDescriptions[item] || `Opcion ${formatOption(item)}`;
        return `<option value="${escapeHtml(item)}">${escapeHtml(formatOption(item))}</option>`;
      })
      .join("");
    setFieldHelp(select, fieldDescriptions[select.id] || select.title || "Selecciona una opcion.");
  };

  const formatOption = (value) => String(value || "").replaceAll("_", " ");

  const showMessage = (node, message, success = false) => {
    if (!node) return;
    node.hidden = false;
    node.textContent = message;
    node.classList.toggle("is-success", success);
  };

  const hideMessage = (node) => {
    if (!node) return;
    node.hidden = true;
    node.textContent = "";
    node.classList.remove("is-success");
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

  const getNumber = (id) => Number.parseFloat(String(fields[id]?.value || "0")) || 0;
  const getInteger = (id) => Number.parseInt(String(fields[id]?.value || "0"), 10) || 0;
  const ruleValue = (code, fallback) => {
    const rule = state.catalogs?.conamiRules?.[code];
    if (!rule) return fallback;
    if (rule.integerValue !== null && rule.integerValue !== undefined) return rule.integerValue;
    if (rule.decimalValue !== null && rule.decimalValue !== undefined) return rule.decimalValue;
    if (rule.booleanValue !== null && rule.booleanValue !== undefined) return rule.booleanValue;
    if (rule.textValue !== null && rule.textValue !== undefined) return rule.textValue;
    return fallback;
  };
  const ruleNumber = (code, fallback) => {
    const parsed = Number(ruleValue(code, fallback));
    return Number.isFinite(parsed) ? parsed : fallback;
  };
  const ruleBoolean = (code, fallback) => {
    const value = ruleValue(code, fallback);
    if (typeof value === "boolean") return value;
    return String(value).toLowerCase() === "true" || String(value).toUpperCase() === "SI" || String(value) === "1";
  };
  const normalizeIdentification = (value) =>
    String(value || "").replaceAll("-", "").replaceAll(" ", "").trim().toUpperCase();

  const inferBirthDateFromIdentification = (value) => {
    const clean = normalizeIdentification(value);
    if (!/^\d{13}[A-Z0-9]$/i.test(clean)) {
      return "";
    }

    const day = Number(clean.slice(3, 5));
    const month = Number(clean.slice(5, 7));
    const year2 = Number(clean.slice(7, 9));
    const currentYear2 = new Date().getFullYear() % 100;
    let year = (year2 > currentYear2 ? 1900 : 2000) + year2;
    let inferred = new Date(Date.UTC(year, month - 1, day));

    if (
      inferred.getUTCFullYear() !== year ||
      inferred.getUTCMonth() !== month - 1 ||
      inferred.getUTCDate() !== day
    ) {
      return "";
    }

    if (inferred > new Date()) {
      year -= 100;
      inferred = new Date(Date.UTC(year, month - 1, day));
    }

    return `${year.toString().padStart(4, "0")}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
  };

  const syncIdentification = () => {
    const clean = normalizeIdentification(fields.cedula.value);
    fields.cedula.value = clean;

    if (fields.identificationType.value === "CEDULA" && !fields.birthDate.value) {
      const inferredBirthDate = inferBirthDateFromIdentification(clean);
      if (inferredBirthDate) {
        fields.birthDate.value = inferredBirthDate;
      }
    }
  };

  const syncRiskAndFile = () => {
    const totalIncome =
      getNumber("monthlyIncome") +
      getNumber("spouseIncome") +
      getNumber("remittances") +
      getNumber("rentIncome") +
      getNumber("otherIncome");
    const expenses = getNumber("monthlyExpenses");
    const missingCoreData = !fields.address.value.trim() || !fields.phone.value.trim() || !fields.economicActivity.value.trim();
    const missingDueDiligence = !fields.sourceOfFunds.value.trim() || !fields.relationshipPurpose.value.trim();
    const baseScore = ruleNumber("CLIENTE_BASE_SCORE", 30);
    const mediumScore = ruleNumber("CLIENTE_SCORE_MEDIO_MIN", 45);
    const highScore = ruleNumber("CLIENTE_SCORE_ALTO_MIN", 70);
    const debtThreshold = ruleNumber("CLIENTE_ENDEUDAMIENTO_MEDIO_PCT", 70) / 100;
    const minimumObservedMonths = ruleNumber("CLIENTE_NEGOCIO_MIN_MESES_OBSERVADO", 6);
    const score = Math.min(
      100,
      Math.max(
        0,
        baseScore +
          (fields.isPep.checked && ruleBoolean("CLIENTE_PEP_ALTO", true) ? ruleNumber("CLIENTE_PEP_SCORE_ADD", 40) : 0) +
          (missingCoreData ? ruleNumber("CLIENTE_DATOS_MINIMOS_SCORE_ADD", 15) : 0) +
          (missingDueDiligence ? ruleNumber("CLIENTE_DDC_SCORE_ADD", 10) : 0) +
          (expenses > totalIncome * debtThreshold && totalIncome > 0 ? ruleNumber("CLIENTE_ENDEUDAMIENTO_SCORE_ADD", 15) : 0) +
          (getInteger("businessAgeMonths") < minimumObservedMonths ? ruleNumber("CLIENTE_NEGOCIO_NUEVO_SCORE_ADD", 10) : 0),
      ),
    );

    fields.riskScore.value = String(score);
    fields.riskLevel.disabled = false;
    fields.riskLevel.value = score >= highScore ? "ALTO" : score >= mediumScore ? "MEDIO" : "BAJO";
    fields.riskLevel.disabled = true;

    if (fields.fileStatus.value === "INCOMPLETO" || fields.fileStatus.value === "COMPLETO") {
      fields.fileStatus.value = missingCoreData ? "INCOMPLETO" : "COMPLETO";
    }
  };

  const payloadFromForm = () => ({
    identificationType: fields.identificationType.value,
    cedula: normalizeIdentification(fields.cedula.value),
    branch: fields.branch.value,
    clientType: fields.clientType.value,
    names: fields.names.value,
    lastNames: fields.lastNames.value,
    relationship: fields.relationship.value,
    status: fields.status.value,
    entryDate: fields.entryDate.value || null,
    birthDate: fields.birthDate.value || null,
    gender: fields.gender.value,
    civilStatus: fields.civilStatus.value,
    spouseName: fields.spouseName.value,
    phone: fields.phone.value,
    mobile: fields.mobile.value,
    secondaryPhone: fields.secondaryPhone.value,
    email: fields.email.value,
    address: fields.address.value,
    homeGeography: fields.homeGeography.value,
    occupation: fields.occupation.value,
    economicActivity: fields.economicActivity.value,
    businessName: fields.businessName.value,
    businessAgeMonths: getInteger("businessAgeMonths"),
    businessAddress: fields.businessAddress.value,
    businessGeography: fields.businessGeography.value,
    monthlyIncome: getNumber("monthlyIncome"),
    spouseIncome: getNumber("spouseIncome"),
    remittances: getNumber("remittances"),
    rentIncome: getNumber("rentIncome"),
    otherIncome: getNumber("otherIncome"),
    monthlyExpenses: getNumber("monthlyExpenses"),
    riskLevel: fields.riskLevel.value,
    riskScore: getInteger("riskScore"),
    fileStatus: fields.fileStatus.value,
    isPep: fields.isPep.checked,
    sourceOfFunds: fields.sourceOfFunds.value,
    relationshipPurpose: fields.relationshipPurpose.value,
    notes: fields.notes.value,
  });

  const validatePayload = (payload) => {
    const errors = [];
    const cleanId = normalizeIdentification(payload.cedula);
    if (!payload.names.trim()) errors.push("Ingresa los nombres.");
    if (!payload.lastNames.trim()) errors.push("Ingresa los apellidos.");
    if (!cleanId) errors.push("Ingresa la identificacion.");
    if (payload.identificationType === "CEDULA" && !/^\d{13}[A-Z0-9]$/i.test(cleanId)) {
      errors.push("La cedula debe tener 13 digitos y una letra o digito final.");
    }
    if (payload.email && !payload.email.includes("@")) errors.push("Ingresa un correo valido.");
    if (payload.riskScore < 0 || payload.riskScore > 100) errors.push("El puntaje de riesgo debe estar entre 0 y 100.");
    if (payload.monthlyExpenses < 0 || payload.monthlyIncome < 0) errors.push("Los montos no pueden ser negativos.");
    return errors;
  };

  const loadCatalogs = async () => {
    const payload = await sessionApi.request("/Clientes/Catalogos");
    state.catalogs = payload.data;
    setOptions(nodes.statusFilter, state.catalogs.statuses, true);
    setOptions(nodes.typeFilter, state.catalogs.clientTypes, true);
    setOptions(fields.identificationType, state.catalogs.identificationTypes);
    setOptions(fields.branch, state.catalogs.branches);
    setOptions(fields.clientType, state.catalogs.clientTypes);
    setOptions(fields.relationship, state.catalogs.relations);
    setOptions(fields.status, state.catalogs.statuses);
    setOptions(fields.gender, state.catalogs.genders);
    setOptions(fields.civilStatus, state.catalogs.civilStatuses);
    setOptions(fields.riskLevel, state.catalogs.riskLevels);
    setOptions(fields.fileStatus, state.catalogs.expedienteStatuses);
  };

  const loadClients = async () => {
    const query = new URLSearchParams({
      search: nodes.searchInput.value.trim(),
      status: nodes.statusFilter.value || "TODOS",
      type: nodes.typeFilter.value || "TODOS",
    });
    const payload = await sessionApi.request(`/Clientes/Listar?${query}`);
    state.clients = payload.data || [];
    renderTable();
    renderMetrics();
    if (state.selectedId && state.clients.some((item) => item.id === state.selectedId)) {
      await selectClient(state.selectedId);
    } else {
      state.selectedId = state.clients[0]?.id || null;
      if (state.selectedId) await selectClient(state.selectedId);
      else renderEmptyDetail();
    }
  };

  const renderMetrics = () => {
    nodes.metricClients.textContent = String(state.clients.length);
    nodes.metricApplications.textContent = String(state.clients.reduce((sum, item) => sum + Number(item.totalApplications || 0), 0));
    nodes.metricBalance.textContent = money(state.clients.reduce((sum, item) => sum + Number(item.principalBalance || 0), 0));
  };

  const renderTable = () => {
    nodes.tableCounter.textContent = `${state.clients.length} registro${state.clients.length === 1 ? "" : "s"}`;
    nodes.tableBody.innerHTML = state.clients.length
      ? state.clients
          .map(
            (item) => `
              <tr class="${item.id === state.selectedId ? "is-selected" : ""}" data-id="${item.id}" tabindex="0" title="Ver ficha de ${escapeHtml(item.fullName)}">
                <td><button type="button" data-id="${item.id}"><strong>${escapeHtml(item.cedula)}</strong></button></td>
                <td>${escapeHtml(item.fullName)}</td>
                <td>${escapeHtml(formatOption(item.clientType))}</td>
                <td><span class="badge ${item.riskLevel === "ALTO" ? "is-danger" : item.riskLevel === "MEDIO" ? "is-gold" : ""}">${escapeHtml(item.riskLevel)}</span></td>
                <td>${escapeHtml(formatOption(item.fileStatus))}</td>
                <td>${Number(item.totalApplications || 0)}</td>
                <td>${Number(item.totalLoans || 0)}</td>
                <td>${escapeHtml(item.status)}</td>
              </tr>
            `,
          )
          .join("")
      : `<tr><td colspan="8">Sin registros.</td></tr>`;
  };

  const renderEmptyDetail = () => {
    nodes.detailTitle.textContent = "Sin seleccion";
    nodes.detailStatus.textContent = "-";
    setClientTab("resumen");
    if (nodes.clientDecisionStrip) {
      nodes.clientDecisionStrip.innerHTML = `<article><span>Perfil</span><strong>Selecciona un cliente para evaluar riesgo, expediente y actividad reciente.</strong></article>`;
    }
    nodes.detailBody.innerHTML = "";
    nodes.relatedBody.innerHTML = "";
  };

  const selectClient = async (id) => {
    state.selectedId = Number(id);
    renderTable();
    const payload = await sessionApi.request(`/Clientes/Obtener?id=${encodeURIComponent(id)}`);
    state.selectedDetail = payload.data;
    state.selectedLoanDetail = null;
    renderDetail();
    setDetailCollapsed(false);
    await loadActiveLoanDetail();
  };

  const detailItem = (label, value) => `
    <article class="detail-item">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(text(value))}</strong>
    </article>
  `;

  const renderClientDecision = (client) => {
    if (!nodes.clientDecisionStrip) return;
    const applications = state.selectedDetail?.applications || [];
    const loans = state.selectedDetail?.loans || [];
    const activeLoans = loans.filter((loan) => Number(loan.principalBalance || 0) > 0);
    const score = Number(client.riskScore || 0);
    const risk = String(client.riskLevel || "MEDIO").toUpperCase();
    const missingFile = String(client.fileStatus || "").toUpperCase() !== "COMPLETO";
    const tone = risk === "ALTO" || missingFile ? "is-risk-high" : "is-risk-low";
    const profileSignal = missingFile
      ? "Expediente incompleto: validar documentos antes de nueva colocacion."
      : risk === "ALTO"
        ? "Cliente requiere debida diligencia reforzada y autorizacion segun politica."
        : "Perfil apto para gestion comercial con control normal de riesgo.";
    nodes.clientDecisionStrip.innerHTML = `
      <article class="${tone}">
        <span>Lectura del perfil</span>
        <strong>${escapeHtml(profileSignal)}</strong>
      </article>
      <article>
        <span>Actividad reciente</span>
        <strong>${applications.length} solicitud${applications.length === 1 ? "" : "es"} - ${activeLoans.length} prestamo${activeLoans.length === 1 ? "" : "s"} activo${activeLoans.length === 1 ? "" : "s"} - score ${score}</strong>
      </article>`;
  };

  const setClientTab = (tab) => {
    state.clientDetailTab = ["resumen", "solicitudes", "prestamos", "historial"].includes(tab) ? tab : "resumen";
    nodes.detailPanel?.setAttribute("data-client-tab", state.clientDetailTab);
    nodes.clientDetailTabs?.querySelectorAll("[data-client-tab]").forEach((button) => {
      button.classList.toggle("is-active", button.dataset.clientTab === state.clientDetailTab);
    });
  };

  const renderDetail = () => {
    const client = state.selectedDetail?.client;
    if (!client) {
      renderEmptyDetail();
      return;
    }

    nodes.detailTitle.textContent = client.fullName;
    nodes.detailStatus.textContent = client.status;
    setClientTab(state.clientDetailTab);
    nodes.detailBody.innerHTML = [
      detailItem("Identificacion", client.cedula),
      detailItem("Tipo", formatOption(client.clientType)),
      detailItem("Telefono", client.mobile || client.phone),
      detailItem("Correo", client.email),
      detailItem("Ingreso total", money(client.totalIncome)),
      detailItem("Capacidad", money(client.paymentCapacity)),
      detailItem("Riesgo", `${client.riskLevel} / ${client.riskScore}`),
      detailItem("Expediente", formatOption(client.fileStatus)),
      detailItem("Actividad", client.economicActivity),
      detailItem("Negocio", client.businessName),
    ].join("");
    renderClientDecision(client);

    const applications = state.selectedDetail.applications || [];
    const loans = state.selectedDetail.loans || [];
    const deletionRequests = state.selectedDetail.deletionRequests || [];
    nodes.relatedBody.innerHTML = `
      <div id="loanDetailBlock" data-client-tab-section="prestamos"></div>
      <article class="related-card" data-client-tab-section="solicitudes">
        <strong>Solicitudes recientes</strong>
        ${applications.length ? applications.slice(0, 5).map((item) => `<span>${escapeHtml(item.number)} - ${escapeHtml(item.status)} - ${money(item.amount)}</span>`).join("") : "<span>Sin solicitudes.</span>"}
      </article>
      <article class="related-card" data-client-tab-section="prestamos">
        <strong>Prestamos</strong>
        ${loans.length ? loans.slice(0, 5).map((item) => `<span>${escapeHtml(item.number)} - ${escapeHtml(item.status)} - saldo ${money(item.principalBalance)}</span>`).join("") : "<span>Sin prestamos.</span>"}
      </article>
      <article class="related-card" data-client-tab-section="historial">
        <strong>Historial y trazabilidad</strong>
        ${deletionRequests.length ? deletionRequests.slice(0, 3).map((item) => `<span>${escapeHtml(item.state)} - ${escapeHtml(item.reason)}</span>`).join("") : "<span>Sin excepciones ni solicitudes de eliminacion.</span>"}
      </article>
    `;
    setClientTab(state.clientDetailTab);
  };

  const loadActiveLoanDetail = async () => {
    const loans = state.selectedDetail?.loans || [];
    const activeLoan =
      loans.find((loan) => Number(loan.principalBalance || 0) > 0 && String(loan.status || "").toUpperCase() !== "CANCELADO") ||
      loans.find((loan) => Number(loan.principalBalance || 0) > 0);
    const block = $("loanDetailBlock");
    if (!block) return;
    if (!activeLoan) {
      block.innerHTML = `<article class="related-card"><strong>Prestamo activo</strong><span>Sin prestamo activo.</span></article>`;
      return;
    }

    block.innerHTML = `<article class="related-card"><strong>Prestamo activo</strong><span>Cargando detalle ${escapeHtml(activeLoan.number)}...</span></article>`;
    try {
      const response = await sessionApi.request(`/Clientes/PrestamoDetalle?id=${encodeURIComponent(activeLoan.id)}`);
      state.selectedLoanDetail = response.data;
      renderLoanDetail(response.data);
    } catch (error) {
      block.innerHTML = `<article class="related-card"><strong>Prestamo activo</strong><span>${escapeHtml(error.message || "No se pudo cargar el prestamo.")}</span></article>`;
    }
  };

  const renderLoanDetail = (loan) => {
    const block = $("loanDetailBlock");
    if (!block || !loan) return;
    const next = loan.nextPayment || {};
    const planRows = Array.isArray(loan.plan) ? loan.plan : [];
    const statementRows = Array.isArray(loan.statement) ? loan.statement : [];
    const rateRows = Array.isArray(loan.rates) ? loan.rates : [];
    block.innerHTML = `
      <section class="loan-detail-card">
        <div class="loan-detail-head">
          <div>
            <span class="eyebrow">Prestamo activo</span>
            <h3>${escapeHtml(loan.number)}</h3>
          </div>
          <span class="status-pill">${escapeHtml(loan.status || "-")}</span>
        </div>
        <div class="loan-actions">
          <button class="ghost-button" type="button" data-print-loan="estado" data-loan-id="${loan.id}">Estado cuenta</button>
          <button class="ghost-button" type="button" data-print-loan="plan" data-loan-id="${loan.id}">Plan de pago</button>
        </div>
        <div class="loan-summary-grid">
          ${detailItem("Codigo cliente", loan.clientId)}
          ${detailItem("No. prestamo", loan.number)}
          ${detailItem("Moneda", loan.currency)}
          ${detailItem("Producto", loan.product)}
          ${detailItem("Monto prestamo", currencyMoney(loan.currency, loan.approvedAmount))}
          ${detailItem("Comision", currencyMoney(loan.currency, loan.totalCommission))}
          ${detailItem("Cuota/plazo", loan.installmentProgress)}
          ${detailItem("Saldo capital", currencyMoney(loan.currency, loan.principalBalance))}
          ${detailItem("Intereses corrientes", currencyMoney(loan.currency, loan.currentInterest))}
          ${detailItem("Otros/seguro/comision", currencyMoney(loan.currency, loan.otherBalance))}
          ${detailItem("Tasa int. cte. anual", `${money(loan.annualRate)} %`)}
          ${detailItem("Total adeudado", currencyMoney(loan.currency, loan.totalOwed))}
        </div>
        <div class="loan-subsection">
          <h4>Proxima cuota</h4>
          <div class="loan-summary-grid">
            ${detailItem("Fecha maxima pago", date(next.dueDate))}
            ${detailItem("Capital", currencyMoney(loan.currency, next.capital))}
            ${detailItem("Interes corriente", currencyMoney(loan.currency, next.currentInterest))}
            ${detailItem("Otros", currencyMoney(loan.currency, next.other))}
            ${detailItem("Mora", currencyMoney(loan.currency, next.mora))}
            ${detailItem("Total proxima cuota", currencyMoney(loan.currency, next.total))}
          </div>
        </div>
        <div class="loan-tables">
          ${renderLoanTable("Estado de cuenta", ["Fecha", "Atraso", "Pagado", "Interes", "Mora", "Abono", "Saldo"], statementRows.slice(0, 8).map((row) => [date(row.paymentDate), row.lateDays || 0, money(row.paidAmount), money(row.currentInterest), money(row.moraInterest), money(row.capitalPayment), money(row.principalBalance)]))}
          ${renderLoanTable("Calendario de pagos", ["Cuota", "Fecha", "Saldo", "Capital", "Interes", "Otros", "Cuota", "Estado"], planRows.slice(0, 8).map((row) => [row.number, date(row.dueDate), money(row.balance), money(row.capital), money(row.interest), money(row.other), money(row.total), row.calendarStatus || row.status]))}
          ${renderLoanTable("Tasas variables", ["Fecha", "Tasa", "Observacion"], rateRows.map((row) => [date(row.date), `${money(row.annualRate)} %`, row.note || ""]))}
        </div>
      </section>
    `;
  };

  const renderLoanTable = (title, headers, rows) => `
    <article class="loan-table-card">
      <strong>${escapeHtml(title)}</strong>
      <div class="mini-table">
        <table>
          <thead><tr>${headers.map((header) => `<th>${escapeHtml(header)}</th>`).join("")}</tr></thead>
          <tbody>${rows.length ? rows.map((row) => `<tr>${row.map((cell) => `<td>${escapeHtml(cell)}</td>`).join("")}</tr>`).join("") : `<tr><td colspan="${headers.length}">Sin datos.</td></tr>`}</tbody>
        </table>
      </div>
    </article>
  `;

  const focusDetailPanel = () => {
    setDetailCollapsed(false);
    nodes.detailBody?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  };

  const openForm = (mode) => {
    hideMessage(nodes.formMessage);
    nodes.clientForm.reset();
    fields.clientId.value = "";
    fields.entryDate.value = new Date().toISOString().slice(0, 10);
    fields.entryDate.readOnly = true;
    fields.businessAgeMonths.value = "0";
    fields.monthlyIncome.value = "0";
    fields.spouseIncome.value = "0";
    fields.remittances.value = "0";
    fields.rentIncome.value = "0";
    fields.otherIncome.value = "0";
    fields.monthlyExpenses.value = "0";
    fields.riskScore.value = "50";
    fields.riskLevel.disabled = false;
    fields.riskLevel.value = "MEDIO";
    fields.riskLevel.disabled = true;
    fields.fileStatus.value = "INCOMPLETO";
    fields.status.value = "ACTIVO";
    nodes.modalTitle.textContent = mode === "edit" ? "Editar cliente" : mode === "view" ? "Ficha del cliente" : "Nuevo cliente";

    if (mode !== "new" && state.selectedDetail?.client) {
      fillForm(state.selectedDetail.client);
    }

    const readOnly = mode === "view";
    nodes.clientForm.querySelectorAll("input, select, textarea").forEach((input) => {
      if (input.id !== "clientId") input.disabled = readOnly;
    });
    fields.entryDate.disabled = false;
    fields.entryDate.readOnly = true;
    fields.riskLevel.disabled = true;
    fields.riskScore.readOnly = true;
    nodes.clientForm.querySelector('button[type="submit"]').hidden = readOnly;
    state.formDirty = false;
    state.formReadOnly = readOnly;
    closeDelete();
    openModal(nodes.modalBackdrop);
  };

  const fillForm = (client) => {
    fields.clientId.value = client.id;
    fields.identificationType.value = client.identificationType || "CEDULA";
    fields.cedula.value = client.cedula || "";
    fields.branch.value = client.branch || "CASA MATRIZ";
    fields.clientType.value = client.clientType || "INDIVIDUAL";
    fields.names.value = client.names || "";
    fields.lastNames.value = client.lastNames || "";
    fields.relationship.value = client.relationship || "NUEVO";
    fields.status.value = client.status || "ACTIVO";
    fields.entryDate.value = isoDate(client.entryDate);
    fields.birthDate.value = isoDate(client.birthDate);
    fields.gender.value = client.gender || "NO_APLICA";
    fields.civilStatus.value = client.civilStatus || "NO_APLICA";
    fields.spouseName.value = client.spouseName || "";
    fields.phone.value = client.phone || "";
    fields.mobile.value = client.mobile || "";
    fields.secondaryPhone.value = client.secondaryPhone || "";
    fields.email.value = client.email || "";
    fields.address.value = client.address || "";
    fields.homeGeography.value = client.homeGeography || "";
    fields.occupation.value = client.occupation || "";
    fields.economicActivity.value = client.economicActivity || "";
    fields.businessName.value = client.businessName || "";
    fields.businessAgeMonths.value = client.businessAgeMonths || 0;
    fields.businessAddress.value = client.businessAddress || "";
    fields.businessGeography.value = client.businessGeography || "";
    fields.monthlyIncome.value = client.monthlyIncome || 0;
    fields.spouseIncome.value = client.spouseIncome || 0;
    fields.remittances.value = client.remittances || 0;
    fields.rentIncome.value = client.rentIncome || 0;
    fields.otherIncome.value = client.otherIncome || 0;
    fields.monthlyExpenses.value = client.monthlyExpenses || 0;
    fields.riskLevel.value = client.riskLevel || "MEDIO";
    fields.riskLevel.disabled = true;
    fields.riskScore.value = client.riskScore ?? 50;
    fields.fileStatus.value = client.fileStatus || "INCOMPLETO";
    fields.isPep.checked = Boolean(client.isPep);
    fields.sourceOfFunds.value = client.sourceOfFunds || "";
    fields.relationshipPurpose.value = client.relationshipPurpose || "";
    fields.notes.value = client.notes || "";
  };

  const closeForm = () => {
    state.formDirty = false;
    state.formReadOnly = false;
    closeModal(nodes.modalBackdrop);
    nodes.clientForm.querySelectorAll("input, select, textarea").forEach((input) => {
      input.disabled = false;
    });
    nodes.clientForm.querySelector('button[type="submit"]').hidden = false;
    fields.riskLevel.disabled = true;
    fields.entryDate.readOnly = true;
  };

  const markFormDirty = () => {
    if (!nodes.modalBackdrop.hidden && !state.formReadOnly) {
      state.formDirty = true;
    }
  };

  const requestCloseForm = async () => {
    if (nodes.modalBackdrop.hidden) return true;
    if (!state.formDirty) {
      closeForm();
      return true;
    }

    if (!window.SifnicUnsavedGuard?.open) {
      if (window.confirm("Tienes cambios sin guardar. ¿Deseas salir sin guardar?")) {
        closeForm();
        return true;
      }
      return false;
    }

    const action = await window.SifnicUnsavedGuard.open({ onSave: () => submitForm() });
    if (action === "discard") {
      closeForm();
      return true;
    }
    return action === "save";
  };

  const setDetailCollapsed = (collapsed) => {
    nodes.detailPanel.classList.toggle("is-collapsed", collapsed);
    nodes.opsMain?.classList.toggle("detail-collapsed", collapsed);
    nodes.detailToggleButton.setAttribute("aria-expanded", collapsed ? "false" : "true");
    nodes.detailToggleButton.title = collapsed ? "Mostrar ficha del cliente" : "Ocultar ficha del cliente";
    nodes.detailToggleIcon.textContent = collapsed ? "<" : ">";
  };

  const toggleDetailPanel = () => {
    setDetailCollapsed(!nodes.detailPanel.classList.contains("is-collapsed"));
  };

  const submitForm = async (event) => {
    event?.preventDefault?.();
    hideMessage(nodes.formMessage);
    syncIdentification();
    syncRiskAndFile();
    const payload = payloadFromForm();
    const errors = validatePayload(payload);
    if (errors.length) {
      showMessage(nodes.formMessage, errors.join(" "));
      return false;
    }

    const id = Number(fields.clientId.value || 0);
    const url = id ? `/Clientes/Actualizar?id=${encodeURIComponent(id)}` : "/Clientes/Crear";
    try {
      const response = await sessionApi.request(url, {
        method: "POST",
        body: JSON.stringify(payload),
      });
      showMessage(nodes.formMessage, response.message || "Guardado.", true);
      state.selectedId = response.data?.id || id || state.selectedId;
      state.formDirty = false;
      await loadClients();
      window.setTimeout(closeForm, 500);
      return true;
    } catch (error) {
      showMessage(nodes.formMessage, error.message || "No se pudo guardar.");
      return false;
    }
  };

  const openDelete = async (event) => {
    event?.preventDefault();
    event?.stopPropagation();
    if (!state.selectedDetail?.client) return;
    if (!(await requestCloseForm())) return;
    hideMessage(nodes.deleteMessage);
    nodes.deleteForm.reset();
    openModal(nodes.deleteBackdrop);
  };

  const closeDelete = () => {
    closeModal(nodes.deleteBackdrop);
  };

  const closeAllModals = () => {
    closeDelete();
    closeForm();
  };

  const submitDelete = async (event) => {
    event.preventDefault();
    hideMessage(nodes.deleteMessage);
    const client = state.selectedDetail?.client;
    if (!client) return;

    const reason = nodes.deleteReason.value.trim();
    const adminUser = nodes.adminUser.value.trim();
    const adminPassword = nodes.adminPassword.value;

    if (reason.length < 12) {
      showMessage(nodes.deleteMessage, "Indica un motivo de al menos 12 caracteres.");
      nodes.deleteReason.focus();
      return;
    }

    if (!adminUser || !adminPassword) {
      showMessage(nodes.deleteMessage, "Para eliminar debes ingresar usuario y clave de administrador.");
      (adminUser ? nodes.adminPassword : nodes.adminUser).focus();
      return;
    }

    nodes.deleteSubmitButton.disabled = true;
    nodes.deleteSubmitButton.textContent = "Procesando...";
    try {
      const payload = {
        reason,
        adminUser,
        adminPassword,
      };
      const response = await sessionApi.request(`/Clientes/SolicitarEliminacion?id=${encodeURIComponent(client.id)}`, {
        method: "POST",
        body: JSON.stringify(payload),
      });
      showMessage(nodes.deleteMessage, response.message || "Procesado.", true);
      await loadClients();
      window.setTimeout(closeDelete, 800);
    } catch (error) {
      showMessage(nodes.deleteMessage, error.message || "No se pudo procesar.");
    } finally {
      nodes.deleteSubmitButton.disabled = false;
      nodes.deleteSubmitButton.textContent = "Procesar";
    }
  };

  const bindEvents = () => {
    nodes.backToDashboard.addEventListener("click", () => {
      window.location.href = "/App/Dashboard";
    });
    nodes.closeSession.addEventListener("click", async () => {
      await sessionApi.logout();
      window.location.href = "/App/Login";
    });
    nodes.refreshButton.addEventListener("click", loadClients);
    nodes.newButton.addEventListener("click", async () => {
      if (await requestCloseForm()) openForm("new");
    });
    nodes.editButton.addEventListener("click", async () => {
      if (await requestCloseForm()) openForm("edit");
    });
    nodes.deleteButton.addEventListener("click", openDelete);
    nodes.creditRequestButton.addEventListener("click", () => {
      const id = state.selectedDetail?.client?.id;
      window.location.href = id ? `/App/SolicitudesCredito?clientId=${encodeURIComponent(id)}` : "/App/SolicitudesCredito";
    });
    nodes.modalClose.addEventListener("click", requestCloseForm);
    nodes.backFormButton.addEventListener("click", requestCloseForm);
    nodes.cancelFormButton.addEventListener("click", requestCloseForm);
    nodes.clientForm.addEventListener("submit", submitForm);
    nodes.clientForm.addEventListener("input", markFormDirty);
    nodes.clientForm.addEventListener("change", markFormDirty);
    nodes.deleteClose.addEventListener("click", closeDelete);
    nodes.deleteBack.addEventListener("click", closeDelete);
    nodes.deleteCancel.addEventListener("click", closeDelete);
    nodes.deleteForm.addEventListener("submit", submitDelete);
    nodes.detailToggleButton.addEventListener("click", toggleDetailPanel);
    nodes.clientDetailTabs?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-client-tab]");
      if (!button) return;
      setClientTab(button.dataset.clientTab);
    });
    setDetailCollapsed(true);
    nodes.modalBackdrop.addEventListener("click", (event) => {
      if (event.target === nodes.modalBackdrop) requestCloseForm();
    });
    nodes.deleteBackdrop.addEventListener("click", (event) => {
      if (event.target === nodes.deleteBackdrop) closeDelete();
    });
    document.addEventListener("keydown", async (event) => {
      if (event.key !== "Escape") return;
      if (!nodes.modalBackdrop.hidden) {
        await requestCloseForm();
        return;
      }
      closeDelete();
    });
    window.addEventListener("pageshow", closeAllModals);
    window.addEventListener("beforeunload", (event) => {
      if (!state.formDirty) return;
      event.preventDefault();
      event.returnValue = "";
    });
    fields.cedula.addEventListener("blur", syncIdentification);
    fields.identificationType.addEventListener("change", syncIdentification);
    [
      fields.address,
      fields.phone,
      fields.economicActivity,
      fields.monthlyIncome,
      fields.spouseIncome,
      fields.remittances,
      fields.rentIncome,
      fields.otherIncome,
      fields.monthlyExpenses,
      fields.businessAgeMonths,
      fields.isPep,
      fields.sourceOfFunds,
      fields.relationshipPurpose,
    ].forEach((field) => field?.addEventListener("change", syncRiskAndFile));
    nodes.tableBody.addEventListener("click", (event) => {
      const row = event.target.closest("tr[data-id]");
      if (row) selectClient(row.dataset.id);
    });
    nodes.relatedBody.addEventListener("click", (event) => {
      const button = event.target.closest("[data-print-loan]");
      if (!button) return;
      const loanId = button.dataset.loanId;
      const mode = button.dataset.printLoan;
      const endpoint = mode === "plan" ? "PlanPagoPrestamoHtml" : "EstadoCuentaPrestamoHtml";
      sessionApi.openWithSession(`/Clientes/${endpoint}?id=${encodeURIComponent(loanId)}`);
    });
    nodes.tableBody.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") return;
      const row = event.target.closest("tr[data-id]");
      if (!row) return;
      event.preventDefault();
      selectClient(row.dataset.id);
    });
    [nodes.searchInput, nodes.statusFilter, nodes.typeFilter].forEach((node) => {
      node.addEventListener(node === nodes.searchInput ? "input" : "change", () => {
        window.clearTimeout(node._timer);
        node._timer = window.setTimeout(loadClients, 250);
      });
    });
  };

  const boot = async () => {
    state.session = sessionApi.getSession();
    if (!state.session) {
      window.location.href = "/App/Login";
      return;
    }
    nodes.sessionUser.textContent = state.session.displayName || state.session.user || "Usuario SIFNIC";
    nodes.sessionMeta.textContent = `${state.session.rolesLabel || "Sin rol"} - ${sessionApi.formatDateTime(state.session.loginAt)}`;
    window.SifnicTheme?.attachToggle(nodes.themeToggle, nodes.themeToggleLabel, null);
    Object.entries(fieldDescriptions).forEach(([id, description]) => {
      const node = $(id) || fields[id];
      setFieldHelp(node, description);
    });
    closeAllModals();
    document.body.classList.add("modals-ready");
    bindEvents();
    await loadCatalogs();
    await loadClients();
  };

  boot().catch((error) => {
    console.error(error);
    renderEmptyDetail();
  });
})();
