(() => {
  const sessionApi = window.SifnicSession;

  const state = {
    session: null,
    catalogs: null,
    requests: [],
    selectedId: null,
    selectedDetail: null,
    requestView: "bandeja",
    formDirty: false,
    formReadOnly: false,
  };

  const $ = (id) => document.getElementById(id);
  const money = (value) =>
    new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value || 0));
  const date = (value) => {
    if (!value) return "";
    try {
      return new Intl.DateTimeFormat("es-NI", { day: "2-digit", month: "2-digit", year: "numeric", timeZone: "America/Managua" }).format(new Date(value));
    } catch {
      return String(value).slice(0, 10);
    }
  };
  const isoDate = (value) => (value ? String(value).slice(0, 10) : "");
  const formatOption = (value) => String(value || "").replaceAll("_", " ");
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
    opsMain: $("opsMain"),
    reportCutoffDate: $("reportCutoffDate"),
    conamiPdfButton: $("conamiPdfButton"),
    conamiExcelButton: $("conamiExcelButton"),
    moraPdfButton: $("moraPdfButton"),
    moraExcelButton: $("moraExcelButton"),
    refreshButton: $("refreshButton"),
    newButton: $("newButton"),
    tableBody: $("tableBody"),
    tableCounter: $("tableCounter"),
    detailTitle: $("detailTitle"),
    detailStatus: $("detailStatus"),
    detailBody: $("detailBody"),
    requestDecisionStrip: $("requestDecisionStrip"),
    workflowBody: $("workflowBody"),
    planBody: $("planBody"),
    viewButton: $("viewButton"),
    editButton: $("editButton"),
    planButton: $("planButton"),
    filePdfButton: $("filePdfButton"),
    fileExcelButton: $("fileExcelButton"),
    planPdfButton: $("planPdfButton"),
    planExcelButton: $("planExcelButton"),
    requestWorkspaceTabs: $("requestWorkspaceTabs"),
    approveButton: $("approveButton"),
    improveButton: $("improveButton"),
    rejectButton: $("rejectButton"),
    metricRequests: $("metricRequests"),
    metricAmount: $("metricAmount"),
    metricCommittee: $("metricCommittee"),
    modalBackdrop: $("modalBackdrop"),
    modalTitle: $("modalTitle"),
    modalClose: $("modalClose"),
    backFormButton: $("backFormButton"),
    cancelFormButton: $("cancelFormButton"),
    requestForm: $("requestForm"),
    formMessage: $("formMessage"),
    lookupClientButton: $("lookupClientButton"),
    showQuickClientButton: $("showQuickClientButton"),
    clientLookupResult: $("clientLookupResult"),
    quickClientSection: $("quickClientSection"),
    quickClientMessage: $("quickClientMessage"),
    createQuickClientButton: $("createQuickClientButton"),
    prepareSinRiesgoButton: $("prepareSinRiesgoButton"),
    previewPlanButton: $("previewPlanButton"),
    modalPlanSummary: $("modalPlanSummary"),
    modalPlanTable: $("modalPlanTable"),
    resolutionBackdrop: $("resolutionBackdrop"),
    resolutionTitle: $("resolutionTitle"),
    resolutionClose: $("resolutionClose"),
    resolutionBack: $("resolutionBack"),
    resolutionCancel: $("resolutionCancel"),
    resolutionForm: $("resolutionForm"),
    resolutionMessage: $("resolutionMessage"),
    approvalBackdrop: $("approvalBackdrop"),
    approvalTitle: $("approvalTitle"),
    approvalClose: $("approvalClose"),
    approvalHero: $("approvalHero"),
    approvalSimulation: $("approvalSimulation"),
    approvalChecklist: $("approvalChecklist"),
    approvalPlanPreview: $("approvalPlanPreview"),
    approvalAmount: $("approvalAmount"),
    approvalCommissionAmount: $("approvalCommissionAmount"),
    approvalFinancedAmount: $("approvalFinancedAmount"),
    approvalNetAmount: $("approvalNetAmount"),
    approvalObservation: $("approvalObservation"),
    approvalMessage: $("approvalMessage"),
    approvalApproveButton: $("approvalApproveButton"),
    approvalImproveButton: $("approvalImproveButton"),
    approvalRejectButton: $("approvalRejectButton"),
    detailPanel: $("detailPanel"),
    detailToggleButton: $("detailToggleButton"),
    detailToggleIcon: $("detailToggleIcon"),
  };

  const fields = [
    "requestId",
    "prospectionStage",
    "promoter",
    "branch",
    "office",
    "systemDate",
    "discardRejectReason",
    "personalReferenceName",
    "personalReferencePhone",
    "personalReferenceResult",
    "commercialReferenceName",
    "commercialReferencePhone",
    "commercialReferenceResult",
    "financialReferenceName",
    "financialReferencePhone",
    "financialReferenceResult",
    "homeVisitDate",
    "homeVisitResult",
    "homeVisitObservation",
    "homeVisitEvidence",
    "businessVisitDate",
    "businessVisitResult",
    "businessVisitObservation",
    "businessVisitEvidence",
    "sinRiesgoConsulted",
    "sinRiesgoSource",
    "sinRiesgoReportNumber",
    "sinRiesgoDate",
    "sinRiesgoResult",
    "sinRiesgoScore",
    "sinRiesgoClassification",
    "externalDebt",
    "externalInstallment",
    "internalDebt",
    "internalInstallment",
    "requestedInstallment",
    "totalDebt",
    "totalInstallment",
    "debtCapacityRatio",
    "sinRiesgoAlerts",
    "sinRiesgoNotes",
    "clientCedulaLookup",
    "clientId",
    "requestDate",
    "product",
    "currency",
    "amount",
    "termMonths",
    "annualRate",
    "commissionRate",
    "slidingRate",
    "moraRate",
    "frequency",
    "installmentType",
    "status",
    "destination",
    "declaredIncome",
    "declaredExpenses",
    "incomeSource",
    "financedActivity",
    "riskLevel",
    "conamiClassification",
    "requiresCommittee",
    "guaranteeType",
    "guaranteeValue",
    "guaranteeDescription",
    "guarantorName",
    "guarantorIdentification",
    "guarantorPhone",
    "chkIdentification",
    "chkFileCompleted",
    "chkHomeBusinessVisit",
    "chkPaymentCapacity",
    "chkConamiReview",
    "chkListCheck",
    "chkGuaranteeReview",
    "notes",
    "quickClientNames",
    "quickClientLastNames",
    "quickClientPhone",
    "quickClientMonthlyIncome",
    "quickClientMonthlyExpenses",
    "quickClientAddress",
    "quickClientEconomicActivity",
    "resolutionAction",
    "resolutionObservation",
  ].reduce((acc, id) => ({ ...acc, [id]: $(id) }), {});

  const setOptions = (select, values, includeAll = false) => {
    if (!select) return;
    const items = includeAll ? ["TODOS", ...(values || [])] : values || [];
    select.innerHTML = items
      .map((item) => {
        const isObject = item && typeof item === "object";
        const value = isObject ? item.code || item.value || item.name : item;
        const label = isObject ? item.name || item.label || item.code || item.value : formatOption(item);
        const title = isObject ? item.description || "" : "";
        const data = isObject
          ? Object.entries(item)
              .filter(([, val]) => val !== null && val !== undefined && typeof val !== "object")
              .map(([key, val]) => ` data-${key.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`)}="${escapeHtml(val)}"`)
              .join("")
          : "";
        return `<option value="${escapeHtml(value)}" title="${escapeHtml(title)}"${data}>${escapeHtml(label)}</option>`;
      })
      .join("");
  };
  const percent = (value) =>
    `${new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(Number(value || 0))}%`;
  const productLabel = (product) => `${product.name || product.code} (${percent(product.annualRate)} anual / ${percent(product.commissionRate)} comision)`;
  const setProductOptions = () => {
    fields.product.innerHTML = (state.catalogs?.products || [])
      .map((product) => `<option value="${escapeHtml(product.code)}">${escapeHtml(productLabel(product))}</option>`)
      .join("");
  };
  const selectedProduct = () =>
    (state.catalogs?.products || []).find((product) => product.code === fields.product.value) ||
    (state.catalogs?.products || [])[0] ||
    null;
  const lockPolicyFields = () => {
    ["annualRate", "commissionRate", "slidingRate", "moraRate"].forEach((id) => {
      if (fields[id]) fields[id].readOnly = true;
    });
    ["currency", "frequency", "installmentType"].forEach((id) => {
      if (fields[id]) fields[id].disabled = true;
    });
  };
  const applySelectedProduct = () => {
    const product = selectedProduct();
    if (!product) return;
    fields.product.value = product.code;
    fields.currency.value = product.currency || "NIO";
    fields.annualRate.value = Number(product.annualRate || 0).toFixed(6);
    fields.commissionRate.value = Number(product.commissionRate || 0).toFixed(6);
    fields.slidingRate.value = Number(product.slidingRate || 0).toFixed(6);
    fields.moraRate.value = Number(product.moraRate || 0).toFixed(6);
    fields.frequency.value = product.frequency || "MENSUAL";
    fields.installmentType.value = product.installmentType || "NIVELADA";
    fields.amount.min = String(product.minAmount || 0);
    fields.amount.max = product.maxAmount ? String(product.maxAmount) : "";
    fields.termMonths.min = String(product.minTermMonths || 1);
    fields.termMonths.max = String(product.maxTermMonths || 120);
    if (Number(fields.termMonths.value || 0) < Number(product.minTermMonths || 1)) {
      fields.termMonths.value = String(product.minTermMonths || 1);
    }
    if (product.requiresGuarantee && fields.guaranteeType.value === "NINGUNA") {
      fields.guaranteeType.value = "FIDUCIARIA";
    }
    lockPolicyFields();
    recalculateDebtLevel();
  };

  const clientOptionHtml = (client) =>
    `<option value="${client.id}" data-identification="${escapeHtml(client.identification)}" data-income="${client.totalIncome}" data-expenses="${client.monthlyExpenses}" data-capacity="${client.paymentCapacity}" data-risk="${escapeHtml(client.riskLevel)}" data-file-status="${escapeHtml(client.fileStatus)}" data-activity="${escapeHtml(client.economicActivity || "")}">${escapeHtml(client.identification)} - ${escapeHtml(client.name)}</option>`;

  const setClientOptions = () => {
    fields.clientId.innerHTML = (state.catalogs?.clients || [])
      .map((client) => clientOptionHtml(client))
      .join("");
  };

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
  const normalizeIdentification = (value) => String(value || "").trim().toUpperCase().replace(/[^A-Z0-9]/g, "");

  const normalizeClientForOption = (client) => {
    const totalIncome =
      Number(client.totalIncome ?? client.monthlyIncome ?? 0) +
      Number(client.spouseIncome ?? 0) +
      Number(client.remittances ?? 0) +
      Number(client.rentIncome ?? 0) +
      Number(client.otherIncome ?? 0);
    const monthlyExpenses = Number(client.monthlyExpenses ?? 0);
    return {
      id: client.id,
      identification: client.identification || client.cedula,
      name: client.name || client.fullName || `${client.names || ""} ${client.lastNames || ""}`.trim(),
      clientType: client.clientType || "INDIVIDUAL",
      status: client.status || "PROSPECTO",
      totalIncome,
      monthlyExpenses,
      paymentCapacity: Number(client.paymentCapacity ?? totalIncome - monthlyExpenses),
      riskLevel: client.riskLevel || "MEDIO",
      fileStatus: client.fileStatus || "INCOMPLETO",
      economicActivity: client.economicActivity || "",
    };
  };

  const upsertClientOption = (client) => {
    const normalized = normalizeClientForOption(client);
    if (!normalized.id) return null;
    const existing = Array.from(fields.clientId.options).find((option) => option.value === String(normalized.id));
    if (existing) {
      existing.outerHTML = clientOptionHtml(normalized);
    } else {
      fields.clientId.insertAdjacentHTML("afterbegin", clientOptionHtml(normalized));
    }
    return normalized;
  };

  const payloadFromForm = () => ({
    clientId: Number(fields.clientId.value || 0),
    prospectionStage: fields.prospectionStage.value,
    discardRejectReason: fields.discardRejectReason.value,
    promoter: fields.promoter.value,
    branch: fields.branch.value,
    office: fields.office.value,
    systemDate: fields.systemDate.value || null,
    references: {
      personal: {
        name: fields.personalReferenceName.value,
        phone: fields.personalReferencePhone.value,
        result: fields.personalReferenceResult.value,
      },
      commercial: {
        name: fields.commercialReferenceName.value,
        phone: fields.commercialReferencePhone.value,
        result: fields.commercialReferenceResult.value,
      },
      financial: {
        name: fields.financialReferenceName.value,
        phone: fields.financialReferencePhone.value,
        result: fields.financialReferenceResult.value,
      },
    },
    visits: {
      home: {
        date: fields.homeVisitDate.value || null,
        result: fields.homeVisitResult.value,
        observation: fields.homeVisitObservation.value,
        evidence: fields.homeVisitEvidence.value,
      },
      business: {
        date: fields.businessVisitDate.value || null,
        result: fields.businessVisitResult.value,
        observation: fields.businessVisitObservation.value,
        evidence: fields.businessVisitEvidence.value,
      },
    },
    creditBureau: {
      consulted: fields.sinRiesgoConsulted.checked,
      bureauName: "SIN_RIESGO",
      consultationDate: fields.sinRiesgoDate.value || null,
      reportNumber: fields.sinRiesgoReportNumber.value,
      result: fields.sinRiesgoResult.value,
      score: getInteger("sinRiesgoScore"),
      classification: fields.sinRiesgoClassification.value,
      externalDebt: getNumber("externalDebt"),
      externalInstallment: getNumber("externalInstallment"),
      internalDebt: getNumber("internalDebt"),
      internalInstallment: getNumber("internalInstallment"),
      requestedAmount: getNumber("amount"),
      requestedInstallment: getNumber("requestedInstallment"),
      totalDebt: getNumber("totalDebt"),
      totalInstallment: getNumber("totalInstallment"),
      paymentCapacity: getNumber("declaredIncome") - getNumber("declaredExpenses"),
      debtCapacityRatio: getNumber("debtCapacityRatio"),
      alerts: fields.sinRiesgoAlerts.value ? fields.sinRiesgoAlerts.value.split(" | ").filter(Boolean) : [],
      notes: fields.sinRiesgoNotes.value,
    },
    requestDate: fields.requestDate.value || null,
    product: fields.product.value,
    currency: fields.currency.value,
    amount: getNumber("amount"),
    termMonths: getInteger("termMonths"),
    annualRate: getNumber("annualRate"),
    commissionRate: getNumber("commissionRate"),
    slidingRate: getNumber("slidingRate"),
    moraRate: getNumber("moraRate"),
    frequency: fields.frequency.value,
    installmentType: fields.installmentType.value,
    status: fields.status.value,
    destination: fields.destination.value,
    declaredIncome: getNumber("declaredIncome"),
    declaredExpenses: getNumber("declaredExpenses"),
    incomeSource: fields.incomeSource.value,
    financedActivity: fields.financedActivity.value,
    riskLevel: fields.riskLevel.value,
    conamiClassification: fields.conamiClassification.value,
    requiresCommittee: fields.requiresCommittee.checked,
    guaranteeType: fields.guaranteeType.value,
    guaranteeValue: getNumber("guaranteeValue"),
    guaranteeDescription: fields.guaranteeDescription.value,
    guarantorName: fields.guarantorName.value,
    guarantorIdentification: fields.guarantorIdentification.value,
    guarantorPhone: fields.guarantorPhone.value,
    checklist: {
      identification: fields.chkIdentification.checked,
      fileCompleted: fields.chkFileCompleted.checked,
      homeBusinessVisit: fields.chkHomeBusinessVisit.checked,
      paymentCapacity: fields.chkPaymentCapacity.checked,
      conamiReview: fields.chkConamiReview.checked,
      listCheck: fields.chkListCheck.checked,
      guaranteeReview: fields.chkGuaranteeReview.checked,
    },
    notes: fields.notes.value,
  });

  const validatePayload = (payload) => {
    const errors = [];
    if (!payload.clientId) errors.push("Selecciona el cliente.");
    if (payload.amount <= 0) errors.push("El monto debe ser mayor que cero.");
    if (payload.termMonths < 1 || payload.termMonths > 120) errors.push("El plazo debe estar entre 1 y 120 meses.");
    if (payload.annualRate < 0 || payload.annualRate > 200) errors.push("La tasa anual debe estar entre 0 y 200.");
    if (payload.commissionRate < 0 || payload.commissionRate > 100) errors.push("La comision ASCC debe estar entre 0 y 100.");
    if (payload.slidingRate < 0 || payload.slidingRate > 100) errors.push("El deslizamiento debe estar entre 0 y 100.");
    if (payload.moraRate < 0 || payload.moraRate > 200) errors.push("La mora anual debe estar entre 0 y 200.");
    if (!payload.destination.trim()) errors.push("Indica el destino del credito.");
    if (payload.declaredIncome > 0 && payload.declaredExpenses >= payload.declaredIncome) {
      errors.push("Los egresos no deben superar los ingresos declarados.");
    }
    const stage = String(payload.prospectionStage || "").toUpperCase();
    if ((stage === "DESCARTADO" || payload.status === "RECHAZADA") && !payload.discardRejectReason.trim()) {
      errors.push("Indica el motivo de descarte o rechazo.");
    }
    if (["PRECALIFICADO", "SOLICITUD_FORMAL"].includes(stage) || ["PRECALIFICADA", "COMITE", "APROBADA"].includes(payload.status)) {
      if (!payload.promoter.trim()) errors.push("Indica el promotor responsable.");
      if (!payload.branch.trim()) errors.push("Indica la sucursal.");
      if (!payload.office.trim()) errors.push("Indica la oficina de credito.");
    }
    if (stage === "SOLICITUD_FORMAL" || ["COMITE", "APROBADA"].includes(payload.status)) {
      if (!payload.creditBureau.consulted) errors.push("Registra el reporte oficial de SIN RIESGO.");
      if (!payload.creditBureau.reportNumber.trim()) errors.push("Indica el numero de reporte SIN RIESGO.");
      if (!payload.creditBureau.consultationDate) errors.push("Indica la fecha de consulta SIN RIESGO.");
      if (payload.creditBureau.result === "SIN_CONSULTA") errors.push("Registra el resultado de SIN RIESGO.");
      if (["COMITE", "APROBADA"].includes(payload.status) && payload.creditBureau.result === "BLOQUEADO") {
        errors.push("SIN RIESGO bloqueado no permite enviar a comite ni aprobar.");
      }
      if (!payload.references.personal.name.trim()) errors.push("Registra una referencia personal.");
      if (!payload.references.commercial.name.trim() && !payload.references.financial.name.trim()) {
        errors.push("Registra una referencia comercial o financiera.");
      }
      if (payload.visits.home.result !== "REALIZADA") errors.push("La visita domiciliar debe estar realizada.");
      if (payload.visits.business.result !== "REALIZADA") errors.push("La visita al negocio debe estar realizada.");
    }
    return errors;
  };

  const openReportExport = (type, format) => {
    const dateValue = nodes.reportCutoffDate?.value || new Date().toISOString().slice(0, 10);
    const endpoint = format === "excel" ? "ReporteConamiExcel" : "ReporteConamiHtml";
    const params = new URLSearchParams({ fechaCorte: dateValue, tipo: type });
    sessionApi.openWithSession(`/SolicitudesCredito/${endpoint}?${params}`);
  };

  const openSelectedExport = (kind, format) => {
    const request = state.selectedDetail?.request;
    if (!request?.id) return;
    const endpointMap = {
      expediente: format === "excel" ? "ExpedienteExcel" : "ExpedienteHtml",
      plan: format === "excel" ? "PlanPagoExcel" : "PlanPagoHtml",
    };
    sessionApi.openWithSession(`/SolicitudesCredito/${endpointMap[kind]}?id=${encodeURIComponent(request.id)}`);
  };

  const loadCatalogs = async () => {
    const payload = await sessionApi.request("/SolicitudesCredito/Catalogos");
    state.catalogs = payload.data;
    setOptions(nodes.statusFilter, state.catalogs.statuses, true);
    setProductOptions();
    setOptions(fields.currency, state.catalogs.currencies);
    setOptions(fields.frequency, state.catalogs.frequencies);
    setOptions(fields.installmentType, state.catalogs.installmentTypes);
    setOptions(fields.status, state.catalogs.statuses);
    setOptions(fields.prospectionStage, state.catalogs.prospectionStages);
    setOptions(fields.homeVisitResult, state.catalogs.visitResults);
    setOptions(fields.businessVisitResult, state.catalogs.visitResults);
    setOptions(fields.sinRiesgoResult, state.catalogs.creditBureauResults);
    setOptions(fields.riskLevel, state.catalogs.riskLevels);
    setOptions(fields.conamiClassification, state.catalogs.conamiClassifications);
    setOptions(fields.guaranteeType, state.catalogs.guaranteeTypes);
    applySelectedProduct();
    setClientOptions();
  };

  const loadRequests = async () => {
    const params = new URLSearchParams({
      search: nodes.searchInput.value.trim(),
      status: nodes.statusFilter.value || "TODOS",
    });
    const initialClientId = new URLSearchParams(window.location.search).get("clientId");
    if (initialClientId && !state._clientParamUsed) {
      params.set("clientId", initialClientId);
      state._clientParamUsed = true;
    }
    const payload = await sessionApi.request(`/SolicitudesCredito/Listar?${params}`);
    state.requests = payload.data || [];
    renderTable();
    renderMetrics();
    if (state.selectedId && state.requests.some((item) => item.id === state.selectedId)) {
      await selectRequest(state.selectedId);
    } else {
      state.selectedId = state.requests[0]?.id || null;
      if (state.selectedId) await selectRequest(state.selectedId);
      else renderEmptyDetail();
    }
  };

  const renderMetrics = () => {
    nodes.metricRequests.textContent = String(state.requests.length);
    nodes.metricAmount.textContent = money(state.requests.reduce((sum, item) => sum + Number(item.amount || 0), 0));
    nodes.metricCommittee.textContent = String(state.requests.filter((item) => item.requiresCommittee).length);
  };

  const renderTable = () => {
    nodes.tableCounter.textContent = `${state.requests.length} registro${state.requests.length === 1 ? "" : "s"}`;
    nodes.tableBody.innerHTML = state.requests.length
      ? state.requests
          .map(
            (item) => `
              <article class="request-card ${item.id === state.selectedId ? "is-selected" : ""}" data-id="${item.id}" tabindex="0" role="listitem">
                <button class="request-card-main" type="button" data-select-id="${item.id}">
                  <span class="request-card-number">${escapeHtml(item.number)}</span>
                  <strong>${escapeHtml(item.clientName)}</strong>
                  <span>${escapeHtml(item.clientIdentification)} · ${escapeHtml(formatOption(item.product || ""))}</span>
                </button>
                <div class="request-card-metrics">
                  <article><span>Monto</span><strong>${escapeHtml(item.currency)} ${money(item.amount)}</strong></article>
                  <article><span>Cuota</span><strong>${money(item.estimatedInstallment)}</strong></article>
                  <article><span>Plazo</span><strong>${Number(item.termMonths || 0)} meses</strong></article>
                </div>
                <div class="request-card-footer">
                  <span class="badge ${item.riskLevel === "ALTO" ? "is-danger" : item.riskLevel === "MEDIO" ? "is-gold" : ""}">${escapeHtml(item.riskLevel)}</span>
                  <span class="status-pill">${escapeHtml(item.status)}</span>
                  <button class="ghost-button compact-button" type="button" data-view-id="${item.id}">Ver</button>
                </div>
              </article>
            `,
          )
          .join("")
      : `<article class="empty-state">Sin solicitudes en esta bandeja.</article>`;
  };

  const renderEmptyDetail = () => {
    nodes.detailTitle.textContent = "Sin seleccion";
    nodes.detailStatus.textContent = "-";
    nodes.detailBody.innerHTML = "";
    if (nodes.requestDecisionStrip) {
      nodes.requestDecisionStrip.innerHTML = `<article><span>Decision</span><strong>Selecciona una solicitud para ver faltantes y bloqueos.</strong></article>`;
    }
    if (nodes.workflowBody) nodes.workflowBody.innerHTML = "";
    nodes.planBody.innerHTML = "";
    [nodes.filePdfButton, nodes.fileExcelButton, nodes.planPdfButton, nodes.planExcelButton].forEach((button) => {
      if (button) button.disabled = true;
    });
    [nodes.viewButton, nodes.editButton, nodes.approveButton, nodes.improveButton, nodes.rejectButton].forEach((button) => {
      if (button) button.disabled = true;
    });
  };

  const selectRequest = async (id) => {
    state.selectedId = Number(id);
    renderTable();
    const payload = await sessionApi.request(`/SolicitudesCredito/Obtener?id=${encodeURIComponent(id)}`);
    state.selectedDetail = payload.data;
    renderDetail();
    setDetailCollapsed(false);
  };

  const detailItem = (label, value) => `
    <article class="detail-item">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(value ?? "-")}</strong>
    </article>
  `;

  const renderDetail = () => {
    const request = state.selectedDetail?.request;
    if (!request) {
      renderEmptyDetail();
      return;
    }

    nodes.detailTitle.textContent = request.number;
    nodes.detailStatus.textContent = request.status;
    nodes.detailBody.innerHTML = [
      detailItem("Cliente", request.clientName),
      detailItem("Identificacion", request.clientIdentification),
      detailItem("Prospeccion", formatOption(request.prospectionStage || "PROSPECTO")),
      detailItem("Promotor", request.promoter || "-"),
      detailItem("Sucursal", request.branch || "-"),
      detailItem("Oficina", request.office || "-"),
      detailItem("Monto", `${request.currency} ${money(request.amount)}`),
      detailItem("Cuota", money(request.estimatedInstallment)),
      detailItem("Capacidad", money(request.paymentCapacity)),
      detailItem("Frecuencia", formatOption(request.frequency)),
      detailItem("Riesgo", `${request.riskLevel} / ${request.conamiClassification}`),
      detailItem("SIN RIESGO", `${request.creditBureau?.result || "SIN_CONSULTA"} / ${money(request.creditBureau?.debtCapacityRatio)}%`),
      detailItem("Comite", request.requiresCommittee ? "SI" : "NO"),
      detailItem("Destino", request.destination),
      detailItem("Motivo", request.discardRejectReason || "-"),
      detailItem("Prestamo", request.creditNumber || "No generado"),
    ].join("");
    renderRequestDecision(request);
    renderWorkflow(request);
    renderPlan(state.selectedDetail.paymentPlan || []);
    [nodes.viewButton, nodes.editButton].forEach((button) => {
      if (button) button.disabled = false;
    });
    nodes.approveButton.disabled = request.status === "APROBADA" || request.status === "RECHAZADA" || request.status === "ANULADA";
    if (nodes.improveButton) nodes.improveButton.disabled = request.status === "APROBADA" || request.status === "RECHAZADA" || request.status === "ANULADA";
    nodes.rejectButton.disabled = request.status === "APROBADA" || request.status === "RECHAZADA" || request.status === "ANULADA";
    [nodes.filePdfButton, nodes.fileExcelButton, nodes.planPdfButton, nodes.planExcelButton].forEach((button) => {
      if (button) button.disabled = false;
    });
  };

  const renderWorkflow = (request) => {
    if (!nodes.workflowBody) return;
    const checklist = request.checklist || {};
    const status = String(request.status || "").toUpperCase();
    const stage = String(request.prospectionStage || "PROSPECTO").toUpperCase();
    const refs = request.references || {};
    const visits = request.visits || {};
    const bureau = request.creditBureau || {};
    const completedStatuses = new Set(["APROBADA", "RECHAZADA", "ANULADA"]);
    const steps = [
      { label: "Prospeccion", done: stage !== "PROSPECTO" },
      { label: "Precalificacion", done: stage === "PRECALIFICADO" || stage === "SOLICITUD_FORMAL" || completedStatuses.has(status) },
      { label: "Solicitud formal", done: stage === "SOLICITUD_FORMAL" || completedStatuses.has(status) },
      { label: "Analisis", done: Boolean(request.amount && request.paymentCapacity >= 0) },
      { label: "Expediente", done: Boolean(checklist.identification && checklist.fileCompleted) },
      { label: request.requiresCommittee ? "Comite" : "Ratificacion", done: status === "COMITE" || completedStatuses.has(status) },
      { label: "Aprobacion", done: status === "APROBADA" },
      { label: "Desembolso", done: Boolean(request.creditNumber) },
    ];
    const checks = [
      ["Identificacion", checklist.identification],
      ["Expediente", checklist.fileCompleted],
      ["Visita", checklist.homeBusinessVisit],
      ["Capacidad", checklist.paymentCapacity],
      ["CONAMI", checklist.conamiReview],
      ["Listas", checklist.listCheck],
      ["Garantia", checklist.guaranteeReview],
      ["Referencias", Boolean(refs.personal?.name && (refs.commercial?.name || refs.financial?.name))],
      ["Visita casa", visits.home?.result === "REALIZADA"],
      ["Visita negocio", visits.business?.result === "REALIZADA"],
      ["SIN RIESGO", Boolean(bureau.consulted && bureau.reportNumber && bureau.result !== "SIN_CONSULTA")],
    ];
    nodes.workflowBody.innerHTML = `
      <article class="workflow-card">
        <div class="workflow-steps">
          ${steps
            .map((step) => `<span class="${step.done ? "is-done" : ""}">${escapeHtml(step.label)}</span>`)
            .join("")}
        </div>
        <div class="checklist-strip">
          ${checks
            .map(([label, done]) => `<span class="${done ? "is-done" : "is-pending"}">${done ? "OK" : "Pend."} ${escapeHtml(label)}</span>`)
            .join("")}
        </div>
      </article>
    `;
  };

  const approvalBlockers = (request) => {
    const checklist = request?.checklist || {};
    const refs = request?.references || {};
    const visits = request?.visits || {};
    const bureau = request?.creditBureau || {};
    return [
      ["Solicitud formalizada", request?.prospectionStage === "SOLICITUD_FORMAL"],
      ["Referencia personal", Boolean(refs.personal?.name)],
      ["Referencia comercial o financiera", Boolean(refs.commercial?.name || refs.financial?.name)],
      ["Visita domiciliar realizada", visits.home?.result === "REALIZADA"],
      ["Visita negocio realizada", visits.business?.result === "REALIZADA"],
      ["SIN RIESGO registrado", Boolean(bureau.consulted && bureau.reportNumber && bureau.result !== "SIN_CONSULTA")],
      ["SIN RIESGO sin bloqueo", bureau.result !== "BLOQUEADO"],
      ["Identificacion validada", checklist.identification],
      ["Expediente completo", checklist.fileCompleted],
      ["Visita casa/negocio", checklist.homeBusinessVisit],
      ["Capacidad de pago", checklist.paymentCapacity],
      ["Revision CONAMI", checklist.conamiReview],
    ]
      .filter(([, done]) => !done)
      .map(([label]) => label);
  };

  const renderRequestDecision = (request) => {
    if (!nodes.requestDecisionStrip) return;
    const blockers = approvalBlockers(request);
    const status = String(request.status || "").toUpperCase();
    const ratio = Number(request.creditBureau?.debtCapacityRatio || 0);
    const canResolve = !["APROBADA", "RECHAZADA", "ANULADA"].includes(status);
    const tone = blockers.length ? "is-blocked" : "is-ready";
    const decision = blockers.length
      ? `Bloqueada por ${blockers.length} faltante${blockers.length === 1 ? "" : "s"}: ${blockers.slice(0, 3).join(", ")}.`
      : canResolve
        ? "Expediente listo para decision; aprobar o rechazar con registro de auditoria."
        : `Solicitud ${formatOption(status)}; conservar trazabilidad y comprobantes.`;
    nodes.requestDecisionStrip.innerHTML = `
      <article class="${tone}">
        <span>Estado de decision</span>
        <strong>${escapeHtml(decision)}</strong>
      </article>
      <article>
        <span>Senales de riesgo</span>
        <strong>${escapeHtml(request.riskLevel || "MEDIO")} / ${escapeHtml(request.conamiClassification || "-")} · Endeudamiento ${money(ratio)}%</strong>
      </article>`;
  };

  const setRequestView = (view) => {
    const nextView = ["bandeja", "expediente", "documentos"].includes(view) ? view : "bandeja";
    state.requestView = nextView;
    document.body.dataset.requestView = nextView;
    nodes.requestWorkspaceTabs?.querySelectorAll("[data-request-view]").forEach((button) => {
      const active = button.dataset.requestView === nextView;
      button.classList.toggle("is-active", active);
      button.setAttribute("aria-pressed", String(active));
    });

    if (nextView === "bandeja") {
      setDetailCollapsed(true);
      return;
    }

    setDetailCollapsed(false);
    if (!state.selectedDetail) {
      nodes.detailTitle.textContent = "Selecciona una solicitud";
      nodes.detailStatus.textContent = "-";
    }
  };

  const focusDetailPanel = () => {
    setRequestView("expediente");
    setDetailCollapsed(false);
    nodes.detailBody?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  };

  const normalizePlan = (plan) =>
    (Array.isArray(plan) ? plan : []).map((item) => ({
      number: item.number,
      dueDate: item.dueDate,
      capital: item.capital,
      interest: item.interest,
      commission: item.commission,
      sliding: item.sliding,
      mora: item.mora,
      interestDays: item.interestDays,
      total: item.total,
      balance: item.balance,
      status: item.status || "",
    }));

  const renderPlan = (plan) => {
    const rows = normalizePlan(plan);
    if (!rows.length) {
      nodes.planBody.innerHTML = `<article class="related-card"><strong>Plan de pago</strong><span>Sin plan generado.</span></article>`;
      return;
    }
    const total = rows.reduce((sum, item) => sum + Number(item.total || 0), 0);
    nodes.planBody.innerHTML = `
      <article class="related-card">
        <strong>Plan de pago</strong>
        <div class="badge-row">
          <span class="badge">${rows.length} cuotas</span>
          <span class="badge is-gold">Total ${money(total)}</span>
        </div>
      </article>
      <div class="mini-table">
        <table>
          <thead><tr><th>Cuota</th><th>Fecha</th><th>Dias</th><th>Capital</th><th>Interes</th><th>Comision</th><th>Desliz.</th><th>Total</th><th>Saldo</th></tr></thead>
          <tbody>
            ${rows
              .slice(0, 24)
              .map(
                (item) => `<tr><td>${item.number}</td><td>${date(item.dueDate)}</td><td>${item.interestDays || 0}</td><td>${money(item.capital)}</td><td>${money(item.interest)}</td><td>${money(item.commission)}</td><td>${money(item.sliding)}</td><td>${money(item.total)}</td><td>${money(item.balance)}</td></tr>`,
              )
              .join("")}
          </tbody>
        </table>
      </div>
    `;
  };

  const openForm = (mode) => {
    closeResolution();
    hideMessage(nodes.formMessage);
    nodes.requestForm.reset();
    fields.requestId.value = "";
    fields.requestDate.value = new Date().toISOString().slice(0, 10);
    fields.systemDate.value = new Date().toISOString().slice(0, 10);
    fields.prospectionStage.value = "PROSPECTO";
    fields.promoter.value = state.session?.displayName || state.session?.user || "";
    fields.branch.value = "CASA MATRIZ";
    fields.office.value = "CENTRAL";
    fields.discardRejectReason.value = "";
    fields.homeVisitResult.value = "PENDIENTE";
    fields.businessVisitResult.value = "PENDIENTE";
    resetSinRiesgoFields();
    fields.currency.value = "NIO";
    fields.amount.value = "0";
    fields.termMonths.value = "6";
    applySelectedProduct();
    fields.status.value = "TRAMITE";
    fields.frequency.value = "MENSUAL";
    fields.installmentType.value = "NIVELADA";
    fields.riskLevel.value = "MEDIO";
    fields.conamiClassification.value = "A";
    fields.declaredIncome.value = "0";
    fields.declaredExpenses.value = "0";
    fields.guaranteeValue.value = "0";
    fields.clientCedulaLookup.value = "";
    fields.quickClientNames.value = "";
    fields.quickClientLastNames.value = "";
    fields.quickClientPhone.value = "";
    fields.quickClientMonthlyIncome.value = "";
    fields.quickClientMonthlyExpenses.value = "";
    fields.quickClientAddress.value = "";
    fields.quickClientEconomicActivity.value = "";
    nodes.quickClientSection.hidden = true;
    hideClientLookupResult();
    hideMessage(nodes.quickClientMessage);
    nodes.modalTitle.textContent = mode === "edit" ? "Editar solicitud" : mode === "view" ? "Expediente de solicitud" : "Nueva solicitud";
    nodes.modalPlanSummary.innerHTML = "";
    nodes.modalPlanTable.innerHTML = "";

    if (mode !== "new" && state.selectedDetail?.request) {
      fillForm(state.selectedDetail.request);
      renderModalPlan(state.selectedDetail.paymentPlan || []);
    } else {
      const clientId = new URLSearchParams(window.location.search).get("clientId");
      if (
        clientId &&
        Array.from(fields.clientId.options).some((option) => option.value === String(clientId))
      ) {
        fields.clientId.value = clientId;
      }
      syncClientFinancials();
      applySelectedProduct();
    }

    const readOnly = mode === "view";
    nodes.requestForm.querySelectorAll("input, select, textarea").forEach((input) => {
      if (input.id !== "requestId") input.disabled = readOnly;
    });
    nodes.requestForm.querySelector('button[type="submit"]').hidden = readOnly;
    nodes.previewPlanButton.hidden = readOnly;
    if (!readOnly) lockPolicyFields();
    state.formDirty = false;
    state.formReadOnly = readOnly;
    openModal(nodes.modalBackdrop);
  };

  const fillForm = (request) => {
    fields.requestId.value = request.id;
    fields.prospectionStage.value = request.prospectionStage || "PROSPECTO";
    fields.promoter.value = request.promoter || "";
    fields.branch.value = request.branch || "CASA MATRIZ";
    fields.office.value = request.office || "CENTRAL";
    fields.systemDate.value = isoDate(request.systemDate) || new Date().toISOString().slice(0, 10);
    fields.discardRejectReason.value = request.discardRejectReason || "";
    fields.personalReferenceName.value = request.references?.personal?.name || "";
    fields.personalReferencePhone.value = request.references?.personal?.phone || "";
    fields.personalReferenceResult.value = request.references?.personal?.result || "";
    fields.commercialReferenceName.value = request.references?.commercial?.name || "";
    fields.commercialReferencePhone.value = request.references?.commercial?.phone || "";
    fields.commercialReferenceResult.value = request.references?.commercial?.result || "";
    fields.financialReferenceName.value = request.references?.financial?.name || "";
    fields.financialReferencePhone.value = request.references?.financial?.phone || "";
    fields.financialReferenceResult.value = request.references?.financial?.result || "";
    fields.homeVisitDate.value = isoDate(request.visits?.home?.date);
    fields.homeVisitResult.value = request.visits?.home?.result || "PENDIENTE";
    fields.homeVisitObservation.value = request.visits?.home?.observation || "";
    fields.homeVisitEvidence.value = request.visits?.home?.evidence || "";
    fields.businessVisitDate.value = isoDate(request.visits?.business?.date);
    fields.businessVisitResult.value = request.visits?.business?.result || "PENDIENTE";
    fields.businessVisitObservation.value = request.visits?.business?.observation || "";
    fields.businessVisitEvidence.value = request.visits?.business?.evidence || "";
    fillSinRiesgo(request.creditBureau || {});
    fields.clientId.value = request.clientId;
    fields.requestDate.value = isoDate(request.requestDate);
    fields.product.value = request.product || "MICROCREDITO";
    fields.amount.value = request.amount || 0;
    fields.termMonths.value = request.termMonths || 6;
    applySelectedProduct();
    fields.status.value = request.status || "TRAMITE";
    fields.destination.value = request.destination || "";
    fields.declaredIncome.value = request.declaredIncome || 0;
    fields.declaredExpenses.value = request.declaredExpenses || 0;
    fields.incomeSource.value = request.incomeSource || "";
    fields.financedActivity.value = request.financedActivity || "";
    fields.riskLevel.value = request.riskLevel || "MEDIO";
    fields.conamiClassification.value = request.conamiClassification || "A";
    fields.requiresCommittee.checked = Boolean(request.requiresCommittee);
    fields.guaranteeType.value = request.guaranteeType || "FIDUCIARIA";
    fields.guaranteeValue.value = request.guaranteeValue || 0;
    fields.guaranteeDescription.value = request.guaranteeDescription || "";
    fields.guarantorName.value = request.guarantorName || "";
    fields.guarantorIdentification.value = request.guarantorIdentification || "";
    fields.guarantorPhone.value = request.guarantorPhone || "";
    fields.chkIdentification.checked = Boolean(request.checklist?.identification);
    fields.chkFileCompleted.checked = Boolean(request.checklist?.fileCompleted);
    fields.chkHomeBusinessVisit.checked = Boolean(request.checklist?.homeBusinessVisit);
    fields.chkPaymentCapacity.checked = Boolean(request.checklist?.paymentCapacity);
    fields.chkConamiReview.checked = Boolean(request.checklist?.conamiReview);
    fields.chkListCheck.checked = Boolean(request.checklist?.listCheck);
    fields.chkGuaranteeReview.checked = Boolean(request.checklist?.guaranteeReview);
    fields.notes.value = request.notes || "";
  };

  const closeForm = () => {
    state.formDirty = false;
    state.formReadOnly = false;
    closeModal(nodes.modalBackdrop);
    nodes.requestForm.querySelectorAll("input, select, textarea").forEach((input) => {
      input.disabled = false;
    });
    lockPolicyFields();
    nodes.requestForm.querySelector('button[type="submit"]').hidden = false;
    nodes.previewPlanButton.hidden = false;
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

  const showClientLookupResult = (message, success = true) => {
    if (!nodes.clientLookupResult) return;
    nodes.clientLookupResult.hidden = false;
    nodes.clientLookupResult.textContent = message;
    nodes.clientLookupResult.classList.toggle("is-warning", !success);
  };

  const hideClientLookupResult = () => {
    if (!nodes.clientLookupResult) return;
    nodes.clientLookupResult.hidden = true;
    nodes.clientLookupResult.textContent = "";
    nodes.clientLookupResult.classList.remove("is-warning");
  };

  const resetSinRiesgoFields = () => {
    fields.sinRiesgoConsulted.checked = false;
    fields.sinRiesgoSource.value = "SIN_RIESGO";
    fields.sinRiesgoReportNumber.value = "";
    fields.sinRiesgoDate.value = new Date().toISOString().slice(0, 10);
    fields.sinRiesgoResult.value = "SIN_CONSULTA";
    fields.sinRiesgoScore.value = "0";
    fields.sinRiesgoClassification.value = "";
    fields.externalDebt.value = "0";
    fields.externalInstallment.value = "0";
    fields.internalDebt.value = "0";
    fields.internalInstallment.value = "0";
    fields.requestedInstallment.value = "0";
    fields.totalDebt.value = "0";
    fields.totalInstallment.value = "0";
    fields.debtCapacityRatio.value = "0";
    fields.sinRiesgoAlerts.value = "";
    fields.sinRiesgoNotes.value = "";
  };

  const fillSinRiesgo = (bureau = {}) => {
    fields.sinRiesgoConsulted.checked = Boolean(bureau.consulted);
    fields.sinRiesgoSource.value = "SIN_RIESGO";
    fields.sinRiesgoReportNumber.value = bureau.reportNumber || "";
    fields.sinRiesgoDate.value = isoDate(bureau.consultationDate) || new Date().toISOString().slice(0, 10);
    fields.sinRiesgoResult.value = bureau.result || "SIN_CONSULTA";
    fields.sinRiesgoScore.value = bureau.score || 0;
    fields.sinRiesgoClassification.value = bureau.classification || "";
    fields.externalDebt.value = Number(bureau.externalDebt || 0).toFixed(2);
    fields.externalInstallment.value = Number(bureau.externalInstallment || 0).toFixed(2);
    fields.internalDebt.value = Number(bureau.internalDebt || 0).toFixed(2);
    fields.internalInstallment.value = Number(bureau.internalInstallment || 0).toFixed(2);
    fields.requestedInstallment.value = Number(bureau.requestedInstallment || 0).toFixed(2);
    fields.totalDebt.value = Number(bureau.totalDebt || 0).toFixed(2);
    fields.totalInstallment.value = Number(bureau.totalInstallment || 0).toFixed(2);
    fields.debtCapacityRatio.value = Number(bureau.debtCapacityRatio || 0).toFixed(2);
    fields.sinRiesgoAlerts.value = (bureau.alerts || []).join(" | ");
    fields.sinRiesgoNotes.value = bureau.notes || "";
    recalculateDebtLevel();
  };

  const recalculateDebtLevel = () => {
    const capacity = Math.max(0, getNumber("declaredIncome") - getNumber("declaredExpenses"));
    const externalDebt = getNumber("externalDebt");
    const externalInstallment = getNumber("externalInstallment");
    const internalDebt = getNumber("internalDebt");
    const internalInstallment = getNumber("internalInstallment");
    const requestedAmount = getNumber("amount");
    const requestedInstallment = getNumber("requestedInstallment");
    const totalDebt = externalDebt + internalDebt + requestedAmount;
    const totalInstallment = externalInstallment + internalInstallment + requestedInstallment;
    const ratio = capacity > 0 ? (totalInstallment / capacity) * 100 : 0;
    const alerts = (fields.sinRiesgoAlerts.value || "")
      .split(" | ")
      .filter(Boolean)
      .filter((alert) => !alert.includes("Endeudamiento proyectado"));
    if (ratio > 50) alerts.push(`Endeudamiento proyectado ${ratio.toFixed(2)}%: urgente atender.`);
    else if (ratio > 35) alerts.push(`Endeudamiento proyectado ${ratio.toFixed(2)}%: riesgoso.`);

    fields.totalDebt.value = totalDebt.toFixed(2);
    fields.totalInstallment.value = totalInstallment.toFixed(2);
    fields.debtCapacityRatio.value = ratio.toFixed(2);
    fields.sinRiesgoAlerts.value = alerts.join(" | ");
    if (ratio > 50) fields.riskLevel.value = "ALTO";
    else if (ratio > 35 && fields.riskLevel.value !== "ALTO") fields.riskLevel.value = "MEDIO";
  };

  const prepareSinRiesgo = async (preserveManual = false) => {
    hideMessage(nodes.formMessage);
    const clientId = Number(fields.clientId.value || 0);
    if (!clientId) {
      showMessage(nodes.formMessage, "Selecciona el cliente antes de preparar SIN RIESGO.");
      return;
    }

    let planData = null;
    if (!preserveManual && getNumber("amount") > 0 && getInteger("termMonths") > 0) {
      planData = await previewPlan();
    }

    try {
      const response = await sessionApi.request("/SolicitudesCredito/ConsultarCentral", {
        method: "POST",
        body: JSON.stringify({
          clientId,
          amount: getNumber("amount"),
          estimatedInstallment: Number(planData?.summary?.estimatedInstallment || getNumber("requestedInstallment") || 0),
        }),
      });
      const manual = {
        consulted: fields.sinRiesgoConsulted.checked,
        reportNumber: fields.sinRiesgoReportNumber.value,
        consultationDate: fields.sinRiesgoDate.value,
        result: fields.sinRiesgoResult.value,
        score: fields.sinRiesgoScore.value,
        classification: fields.sinRiesgoClassification.value,
        externalDebt: fields.externalDebt.value,
        externalInstallment: fields.externalInstallment.value,
        notes: fields.sinRiesgoNotes.value,
      };
      fillSinRiesgo(response.data || {});
      if (preserveManual && manual.reportNumber.trim()) {
        fields.sinRiesgoConsulted.checked = manual.consulted;
        fields.sinRiesgoReportNumber.value = manual.reportNumber;
        fields.sinRiesgoDate.value = manual.consultationDate;
        fields.sinRiesgoResult.value = manual.result;
        fields.sinRiesgoScore.value = manual.score;
        fields.sinRiesgoClassification.value = manual.classification;
        fields.externalDebt.value = manual.externalDebt;
        fields.externalInstallment.value = manual.externalInstallment;
        fields.sinRiesgoNotes.value = manual.notes;
        recalculateDebtLevel();
      }
      if (!preserveManual) showMessage(nodes.formMessage, "Base SIN RIESGO preparada. Solo registra reporte, deudas externas y cuota externa.", true);
    } catch (error) {
      showMessage(nodes.formMessage, error.message || "No se pudo preparar SIN RIESGO.");
    }
  };

  const selectClient = (client, force = true) => {
    const normalized = upsertClientOption(client);
    if (!normalized) return;
    fields.clientId.value = String(normalized.id);
    fields.clientCedulaLookup.value = normalized.identification || "";
    syncClientFinancials(force);
    nodes.quickClientSection.hidden = true;
    hideMessage(nodes.quickClientMessage);
    showClientLookupResult(`Cliente verificado: ${normalized.identification} - ${normalized.name}. Datos financieros cargados.`, true);
  };

  const lookupClientByCedula = async () => {
    hideMessage(nodes.quickClientMessage);
    const cedula = normalizeIdentification(fields.clientCedulaLookup.value);
    fields.clientCedulaLookup.value = cedula;
    if (!cedula) {
      showClientLookupResult("Ingresa la cedula del cliente para buscar.", false);
      return null;
    }

    const localOption = Array.from(fields.clientId.options).find(
      (option) => normalizeIdentification(option.dataset.identification) === cedula,
    );
    if (localOption) {
      fields.clientId.value = localOption.value;
      syncClientFinancials(true);
      nodes.quickClientSection.hidden = true;
      showClientLookupResult(`Cliente encontrado en catalogo: ${localOption.textContent.trim()}.`, true);
      return localOption.value;
    }

    try {
      const response = await sessionApi.request(`/SolicitudesCredito/BuscarCliente?cedula=${encodeURIComponent(cedula)}`);
      if (response.data?.found && response.data.client) {
        selectClient(response.data.client, true);
        return response.data.client.id;
      }

      nodes.quickClientSection.hidden = false;
      fields.quickClientNames.focus();
      showClientLookupResult("Cliente no existe o no esta activo. Puedes crearlo aqui mismo y seguir con la solicitud.", false);
      return null;
    } catch (error) {
      showClientLookupResult(error.message || "No se pudo buscar el cliente.", false);
      return null;
    }
  };

  const showQuickClientSection = () => {
    const cedula = normalizeIdentification(fields.clientCedulaLookup.value);
    fields.clientCedulaLookup.value = cedula;
    nodes.quickClientSection.hidden = false;
    hideMessage(nodes.quickClientMessage);
    showClientLookupResult(
      cedula ? `Alta rapida preparada para ${cedula}.` : "Ingresa primero la cedula para crear el cliente.",
      Boolean(cedula),
    );
    fields.quickClientNames.focus();
  };

  const createQuickClient = async () => {
    hideMessage(nodes.quickClientMessage);
    const cedula = normalizeIdentification(fields.clientCedulaLookup.value);
    fields.clientCedulaLookup.value = cedula;
    const names = fields.quickClientNames.value.trim();
    const lastNames = fields.quickClientLastNames.value.trim();
    if (!cedula || !names || !lastNames) {
      showMessage(nodes.quickClientMessage, "Ingresa cedula, nombres y apellidos.");
      return null;
    }

    try {
      const response = await sessionApi.request("/Clientes/Crear", {
        method: "POST",
        body: JSON.stringify({
          identificationType: "CEDULA",
          cedula,
          branch: "CENTRAL",
          clientType: "INDIVIDUAL",
          names,
          lastNames,
          relationship: "PROSPECTO",
          status: "PROSPECTO",
          entryDate: fields.requestDate.value || new Date().toISOString().slice(0, 10),
          phone: fields.quickClientPhone.value,
          mobile: fields.quickClientPhone.value,
          address: fields.quickClientAddress.value,
          occupation: fields.quickClientEconomicActivity.value,
          economicActivity: fields.quickClientEconomicActivity.value,
          businessAgeMonths: 0,
          monthlyIncome: getNumber("quickClientMonthlyIncome"),
          monthlyExpenses: getNumber("quickClientMonthlyExpenses"),
          riskLevel: "MEDIO",
          riskScore: 50,
          fileStatus: "INCOMPLETO",
          sourceOfFunds: "Pendiente de completar",
          relationshipPurpose: "Solicitud de credito",
          notes: "Creado desde solicitud de credito.",
        }),
      });
      const client = normalizeClientForOption(response.data?.client || response.data || {});
      selectClient(client, true);
      showMessage(nodes.quickClientMessage, "Cliente creado y seleccionado.", true);
      await loadCatalogs();
      fields.clientId.value = String(client.id);
      syncClientFinancials(true);
      return client.id;
    } catch (error) {
      const detail = error.errors ? Object.values(error.errors).join(" ") : "";
      showMessage(nodes.quickClientMessage, `${error.message || "No se pudo crear el cliente."} ${detail}`.trim());
      return null;
    }
  };

  const syncClientFinancials = (force = false) => {
    const selected = fields.clientId.selectedOptions[0];
    const income = Number(selected?.dataset.income || 0);
    const expenses = Number(selected?.dataset.expenses || 0);
    const capacity = Number(selected?.dataset.capacity || 0);
    const risk = selected?.dataset.risk || "MEDIO";
    const fileStatus = String(selected?.dataset.fileStatus || "").toUpperCase();
    const hasIdentification = Boolean(String(selected?.dataset.identification || "").trim());
    if (selected?.dataset.identification) {
      fields.clientCedulaLookup.value = selected.dataset.identification;
    }
    if ((force || !Number(fields.declaredIncome.value || 0)) && income > 0) {
      fields.declaredIncome.value = income.toFixed(2);
    }
    if ((force || !Number(fields.declaredExpenses.value || 0)) && expenses > 0) {
      fields.declaredExpenses.value = expenses.toFixed(2);
    }
    if ((force || !fields.financedActivity.value.trim()) && selected?.dataset.activity) {
      fields.financedActivity.value = selected.dataset.activity;
    }
    fields.riskLevel.value = risk;
    fields.requiresCommittee.checked =
      fields.requiresCommittee.checked ||
      risk === "ALTO" ||
      (getNumber("amount") > 0 && capacity > 0 && getNumber("amount") > capacity * ruleNumber("SOL_MONTO_CAPACIDAD_VECES_COMITE", 2));
    fields.chkIdentification.checked = fields.chkIdentification.checked || hasIdentification;
    fields.chkFileCompleted.checked = fields.chkFileCompleted.checked || fileStatus === "COMPLETO";
  };

  const syncCommitteeAndChecklist = () => {
    const selected = fields.clientId.selectedOptions[0];
    const capacity = Number(selected?.dataset.capacity || 0);
    const installment = Number(
      nodes.modalPlanSummary.querySelector("article:nth-child(2) strong")?.textContent?.replaceAll(",", "") || 0,
    );
    fields.requiresCommittee.checked =
      fields.requiresCommittee.checked ||
      fields.riskLevel.value === "ALTO" ||
      getNumber("amount") >= ruleNumber("SOL_COMITE_MONTO_MIN", 50000) ||
      (installment > 0 && capacity > 0 && installment > capacity * (ruleNumber("SOL_COMITE_CUOTA_CAPACIDAD_PCT", 50) / 100));

    fields.chkPaymentCapacity.checked =
      fields.chkPaymentCapacity.checked ||
      (getNumber("declaredIncome") > 0 && getNumber("declaredIncome") > getNumber("declaredExpenses"));
    fields.chkConamiReview.checked = fields.chkConamiReview.checked || fields.conamiClassification.value === "A";
    fields.chkGuaranteeReview.checked =
      fields.chkGuaranteeReview.checked ||
      fields.guaranteeType.value === "NINGUNA" ||
      getNumber("guaranteeValue") >= getNumber("amount") * 0.5;
  };

  const previewPlan = async () => {
    hideMessage(nodes.formMessage);
    const payload = payloadFromForm();
    const errors = [];
    if (payload.amount <= 0) errors.push("Ingresa el monto.");
    if (payload.termMonths < 1) errors.push("Ingresa el plazo.");
    if (errors.length) {
      showMessage(nodes.formMessage, errors.join(" "));
      return null;
    }
    try {
      const response = await sessionApi.request("/SolicitudesCredito/GenerarPlan", {
        method: "POST",
        body: JSON.stringify({
          amount: payload.amount,
          product: payload.product,
          termMonths: payload.termMonths,
          annualRate: payload.annualRate,
          commissionRate: payload.commissionRate,
          slidingRate: payload.slidingRate,
          moraRate: payload.moraRate,
          frequency: payload.frequency,
          startDate: payload.requestDate,
        }),
      });
      renderModalPlan(response.data.paymentPlan || [], response.data.summary);
      fields.requestedInstallment.value = Number(response.data.summary?.estimatedInstallment || 0).toFixed(2);
      recalculateDebtLevel();
      syncCommitteeAndChecklist();
      if (Number(fields.clientId.value || 0) > 0 && !fields.sinRiesgoReportNumber.value.trim()) {
        prepareSinRiesgo(true);
      }
      return response.data;
    } catch (error) {
      showMessage(nodes.formMessage, error.message || "No se pudo generar el plan.");
      return null;
    }
  };

  const renderModalPlan = (plan, summary) => {
    const rows = normalizePlan(plan);
    const fallbackSummary = {
      installments: rows.length,
      estimatedInstallment: rows[0]?.total || 0,
      totalCapital: rows.reduce((sum, item) => sum + Number(item.capital || 0), 0),
      totalInterest: rows.reduce((sum, item) => sum + Number(item.interest || 0), 0),
      totalCommission: rows.reduce((sum, item) => sum + Number(item.commission || 0), 0),
      totalSliding: rows.reduce((sum, item) => sum + Number(item.sliding || 0), 0),
      totalToPay: rows.reduce((sum, item) => sum + Number(item.total || 0), 0),
    };
    const data = summary || fallbackSummary;
    nodes.modalPlanSummary.innerHTML = `
      <article><span>Cuotas</span><strong>${Number(data.installments || rows.length)}</strong></article>
      <article><span>Cuota estimada</span><strong>${money(data.estimatedInstallment)}</strong></article>
      <article><span>Interes total</span><strong>${money(data.totalInterest)}</strong></article>
      <article><span>Comision</span><strong>${money(data.totalCommission)}</strong></article>
      <article><span>Total</span><strong>${money(data.totalToPay)}</strong></article>
    `;
    if (fields.requestedInstallment) {
      fields.requestedInstallment.value = Number(data.estimatedInstallment || 0).toFixed(2);
      recalculateDebtLevel();
    }
    nodes.modalPlanTable.innerHTML = rows.length
      ? `<table><thead><tr><th>No.</th><th>Fecha</th><th>Dias</th><th>Capital</th><th>Interes</th><th>Comision</th><th>Desliz.</th><th>Total</th><th>Saldo</th></tr></thead><tbody>${rows
          .slice(0, 24)
          .map((item) => `<tr><td>${item.number}</td><td>${date(item.dueDate)}</td><td>${item.interestDays || 0}</td><td>${money(item.capital)}</td><td>${money(item.interest)}</td><td>${money(item.commission)}</td><td>${money(item.sliding)}</td><td>${money(item.total)}</td><td>${money(item.balance)}</td></tr>`)
          .join("")}</tbody></table>`
      : "";
  };

  const submitForm = async (event) => {
    event?.preventDefault?.();
    hideMessage(nodes.formMessage);
    syncCommitteeAndChecklist();
    const payload = payloadFromForm();
    const errors = validatePayload(payload);
    if (errors.length) {
      showMessage(nodes.formMessage, errors.join(" "));
      return false;
    }

    await previewPlan();
    const id = Number(fields.requestId.value || 0);
    const url = id ? `/SolicitudesCredito/Actualizar?id=${encodeURIComponent(id)}` : "/SolicitudesCredito/Crear";
    try {
      const response = await sessionApi.request(url, {
        method: "POST",
        body: JSON.stringify(payload),
      });
      showMessage(nodes.formMessage, response.message || "Guardado.", true);
      state.selectedId = response.data?.request?.id || id || state.selectedId;
      state.formDirty = false;
      await loadRequests();
      window.setTimeout(closeForm, 600);
      return true;
    } catch (error) {
      showMessage(nodes.formMessage, error.message || "No se pudo guardar.");
      return false;
    }
  };

  const openResolution = async (action) => {
    const request = state.selectedDetail?.request;
    if (!request) return;
    if (!(await requestCloseForm())) return;
    hideMessage(nodes.resolutionMessage);
    fields.resolutionAction.value = action;
    fields.resolutionObservation.value = "";
    nodes.resolutionTitle.textContent = action === "APROBAR" ? "Aprobar solicitud" : "Rechazar solicitud";
    openModal(nodes.resolutionBackdrop);
    if (action === "APROBAR") {
      const blockers = approvalBlockers(request);
      if (blockers.length) {
        showMessage(nodes.resolutionMessage, `Antes de aprobar completa: ${blockers.join(", ")}.`);
      }
    }
  };

  const closeResolution = () => {
    closeModal(nodes.resolutionBackdrop);
  };

  const updateApprovalAmounts = () => {
    const request = state.selectedDetail?.request;
    if (!request) return;
    const approvedAmount = Number(nodes.approvalAmount?.value || request.amount || 0);
    const commissionRate = Number(request.commissionRate || 0);
    const commissionAmount = approvedAmount * commissionRate / 100;
    const financedAmount = approvedAmount + commissionAmount;
    nodes.approvalCommissionAmount.textContent = `${request.currency} ${money(commissionAmount)}`;
    nodes.approvalFinancedAmount.textContent = `${request.currency} ${money(financedAmount)}`;
    nodes.approvalNetAmount.textContent = `${request.currency} ${money(approvedAmount)}`;
  };

  const renderApprovalModal = () => {
    const detail = state.selectedDetail || {};
    const request = detail.request;
    if (!request) return;
    const checklist = request.checklist || {};
    const plan = detail.paymentPlan || [];
    const bureau = request.creditBureau || {};
    const blockers = approvalBlockers(request);
    nodes.approvalTitle.textContent = `${request.number} · ${request.clientName}`;
    nodes.approvalHero.innerHTML = `
      <article>
        <span>Cliente</span>
        <strong>${escapeHtml(request.clientName)}</strong>
        <small>${escapeHtml(request.clientIdentification)} · ${escapeHtml(request.clientType || "INDIVIDUAL")}</small>
      </article>
      <article>
        <span>Solicitud</span>
        <strong>${escapeHtml(request.number)}</strong>
        <small>${escapeHtml(formatOption(request.product || ""))} · ${escapeHtml(request.currency)}</small>
      </article>
      <article>
        <span>Estado</span>
        <strong>${escapeHtml(formatOption(request.status))}</strong>
        <small>${escapeHtml(formatOption(request.prospectionStage || "PROSPECTO"))}</small>
      </article>
      <article>
        <span>Riesgo</span>
        <strong>${escapeHtml(request.riskLevel || "-")}</strong>
        <small>CONAMI ${escapeHtml(request.conamiClassification || "-")}</small>
      </article>
    `;
    nodes.approvalSimulation.innerHTML = [
      ["Monto solicitado", `${request.currency} ${money(request.amount)}`],
      ["Cuota estimada", `${request.currency} ${money(request.estimatedInstallment)}`],
      ["Capacidad", `${request.currency} ${money(request.paymentCapacity)}`],
      ["Tasa anual", `${money(request.annualRate)}%`],
      ["Comision desembolso", `${money(request.commissionRate)}% financiada`],
      ["Endeudamiento", `${money(bureau.debtCapacityRatio)}%`],
    ]
      .map(([label, value]) => `<article><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></article>`)
      .join("");
    nodes.approvalChecklist.innerHTML = [
      ["Identificacion", checklist.identification],
      ["Expediente", checklist.fileCompleted],
      ["Visita casa/negocio", checklist.homeBusinessVisit],
      ["Capacidad de pago", checklist.paymentCapacity],
      ["Revision CONAMI", checklist.conamiReview],
      ["Listas y garantia", checklist.listCheck && checklist.guaranteeReview],
    ]
      .map(([label, ok]) => `<article class="${ok ? "is-ok" : "is-pending"}"><strong>${ok ? "OK" : "Pendiente"}</strong><span>${escapeHtml(label)}</span></article>`)
      .join("") + (blockers.length ? `<div class="approval-blockers">Faltantes: ${escapeHtml(blockers.join(", "))}</div>` : `<div class="approval-ready">Expediente listo para decision.</div>`);
    nodes.approvalPlanPreview.innerHTML = plan.length
      ? `<table><thead><tr><th>Cuota</th><th>Fecha</th><th>Capital</th><th>Interes</th><th>Total</th></tr></thead><tbody>${plan
          .slice(0, 6)
          .map((item) => `<tr><td>${item.number}</td><td>${date(item.dueDate)}</td><td>${money(item.capital)}</td><td>${money(item.interest)}</td><td>${money(Number(item.capital || 0) + Number(item.interest || 0) + Number(item.commission || 0) + Number(item.sliding || 0))}</td></tr>`)
          .join("")}</tbody></table>`
      : `<div class="empty-state">Sin plan generado.</div>`;
    nodes.approvalAmount.value = Number(request.amount || 0).toFixed(2);
    nodes.approvalObservation.value = "";
    hideMessage(nodes.approvalMessage);
    updateApprovalAmounts();
  };

  const openApprovalModal = async () => {
    if (!state.selectedDetail?.request) return;
    if (!(await requestCloseForm())) return;
    renderApprovalModal();
    openModal(nodes.approvalBackdrop);
  };

  const closeApprovalModal = () => {
    closeModal(nodes.approvalBackdrop);
  };

  const submitApprovalDecision = async (action) => {
    const request = state.selectedDetail?.request;
    if (!request) return;
    hideMessage(nodes.approvalMessage);
    const observation = nodes.approvalObservation.value.trim();
    if ((action === "RECHAZAR" || action === "MEJORA") && !observation) {
      showMessage(nodes.approvalMessage, "Indica el motivo para rechazar o solicitar mejora.");
      return;
    }
    try {
      const response = await sessionApi.request(`/SolicitudesCredito/Resolver?id=${encodeURIComponent(request.id)}`, {
        method: "POST",
        body: JSON.stringify({
          action,
          observation,
          approvedAmount: Number(nodes.approvalAmount.value || request.amount || 0),
          approvedTermMonths: Number(request.termMonths || 0),
          approvedAnnualRate: Number(request.annualRate || 0),
        }),
      });
      showMessage(nodes.approvalMessage, response.message || "Procesado.", true);
      await loadRequests();
      if (action === "APROBAR" && response.data?.disbursementUrl) {
        window.setTimeout(() => {
          window.location.href = response.data.disbursementUrl;
        }, 900);
        return;
      }
      window.setTimeout(closeApprovalModal, 900);
    } catch (error) {
      const detail = error.errors ? Object.values(error.errors).join(" ") : "";
      showMessage(nodes.approvalMessage, `${error.message || "No se pudo procesar."} ${detail}`.trim());
    }
  };

  const closeAllModals = () => {
    closeResolution();
    closeApprovalModal();
    closeForm();
  };

  const setDetailCollapsed = (collapsed) => {
    nodes.detailPanel.classList.toggle("is-collapsed", collapsed);
    nodes.opsMain?.classList.toggle("detail-collapsed", collapsed);
    nodes.detailToggleButton.setAttribute("aria-expanded", collapsed ? "false" : "true");
    nodes.detailToggleButton.title = collapsed ? "Mostrar expediente de credito" : "Ocultar expediente de credito";
    nodes.detailToggleIcon.textContent = collapsed ? "<" : ">";
  };

  const toggleDetailPanel = () => {
    setDetailCollapsed(!nodes.detailPanel.classList.contains("is-collapsed"));
  };

  const submitResolution = async (event) => {
    event.preventDefault();
    hideMessage(nodes.resolutionMessage);
    const request = state.selectedDetail?.request;
    if (!request) return;
    try {
      const response = await sessionApi.request(`/SolicitudesCredito/Resolver?id=${encodeURIComponent(request.id)}`, {
        method: "POST",
        body: JSON.stringify({
          action: fields.resolutionAction.value,
          observation: fields.resolutionObservation.value,
        }),
      });
      showMessage(nodes.resolutionMessage, response.message || "Procesado.", true);
      await loadRequests();
      window.setTimeout(closeResolution, 900);
    } catch (error) {
      const detail = error.errors ? Object.values(error.errors).join(" ") : "";
      showMessage(nodes.resolutionMessage, `${error.message || "No se pudo procesar."} ${detail}`.trim());
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
    nodes.refreshButton.addEventListener("click", loadRequests);
    nodes.newButton.addEventListener("click", async () => {
      if (await requestCloseForm()) openForm("new");
    });
    nodes.conamiPdfButton?.addEventListener("click", () => openReportExport("CARTERA", "pdf"));
    nodes.conamiExcelButton?.addEventListener("click", () => openReportExport("CARTERA", "excel"));
    nodes.moraPdfButton?.addEventListener("click", () => openReportExport("MORA", "pdf"));
    nodes.moraExcelButton?.addEventListener("click", () => openReportExport("MORA", "excel"));
    nodes.requestWorkspaceTabs?.addEventListener("click", (event) => {
      const button = event.target.closest("[data-request-view]");
      if (!button) return;
      setRequestView(button.dataset.requestView);
    });
    nodes.viewButton.addEventListener("click", openApprovalModal);
    nodes.editButton.addEventListener("click", async () => {
      if (await requestCloseForm()) openForm("edit");
    });
    nodes.planButton.addEventListener("click", () => {
      setRequestView("documentos");
      nodes.planBody?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    });
    nodes.filePdfButton?.addEventListener("click", () => openSelectedExport("expediente", "pdf"));
    nodes.fileExcelButton?.addEventListener("click", () => openSelectedExport("expediente", "excel"));
    nodes.planPdfButton?.addEventListener("click", () => openSelectedExport("plan", "pdf"));
    nodes.planExcelButton?.addEventListener("click", () => openSelectedExport("plan", "excel"));
    nodes.approveButton.addEventListener("click", openApprovalModal);
    nodes.improveButton?.addEventListener("click", openApprovalModal);
    nodes.rejectButton.addEventListener("click", openApprovalModal);
    nodes.modalClose.addEventListener("click", requestCloseForm);
    nodes.backFormButton.addEventListener("click", requestCloseForm);
    nodes.cancelFormButton.addEventListener("click", requestCloseForm);
    nodes.requestForm.addEventListener("submit", submitForm);
    nodes.requestForm.addEventListener("input", markFormDirty);
    nodes.requestForm.addEventListener("change", markFormDirty);
    nodes.lookupClientButton.addEventListener("click", lookupClientByCedula);
    nodes.showQuickClientButton.addEventListener("click", showQuickClientSection);
    nodes.createQuickClientButton.addEventListener("click", createQuickClient);
    nodes.prepareSinRiesgoButton.addEventListener("click", () => prepareSinRiesgo(false));
    fields.clientCedulaLookup.addEventListener("blur", () => {
      fields.clientCedulaLookup.value = normalizeIdentification(fields.clientCedulaLookup.value);
    });
    fields.clientCedulaLookup.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        lookupClientByCedula();
      }
    });
    nodes.previewPlanButton.addEventListener("click", previewPlan);
    nodes.resolutionClose.addEventListener("click", closeResolution);
    nodes.resolutionBack.addEventListener("click", closeResolution);
    nodes.resolutionCancel.addEventListener("click", closeResolution);
    nodes.resolutionForm.addEventListener("submit", submitResolution);
    nodes.approvalClose?.addEventListener("click", closeApprovalModal);
    nodes.approvalAmount?.addEventListener("input", updateApprovalAmounts);
    nodes.approvalApproveButton?.addEventListener("click", () => submitApprovalDecision("APROBAR"));
    nodes.approvalImproveButton?.addEventListener("click", () => submitApprovalDecision("MEJORA"));
    nodes.approvalRejectButton?.addEventListener("click", () => submitApprovalDecision("RECHAZAR"));
    nodes.detailToggleButton.addEventListener("click", toggleDetailPanel);
    setRequestView("bandeja");
    fields.prospectionStage.addEventListener("change", () => {
      if (fields.prospectionStage.value === "DESCARTADO") {
        fields.status.value = "RECHAZADA";
      } else if (fields.prospectionStage.value === "PRECALIFICADO" && fields.status.value === "TRAMITE") {
        fields.status.value = "PRECALIFICADA";
      }
      syncCommitteeAndChecklist();
    });
    [fields.homeVisitResult, fields.businessVisitResult].forEach((field) => {
      field?.addEventListener("change", () => {
        fields.chkHomeBusinessVisit.checked =
          fields.homeVisitResult.value === "REALIZADA" && fields.businessVisitResult.value === "REALIZADA";
      });
    });
    [fields.externalDebt, fields.externalInstallment, fields.declaredIncome, fields.declaredExpenses, fields.amount].forEach((field) => {
      field?.addEventListener("input", recalculateDebtLevel);
      field?.addEventListener("change", recalculateDebtLevel);
    });
    nodes.modalBackdrop.addEventListener("click", (event) => {
      if (event.target === nodes.modalBackdrop) requestCloseForm();
    });
    nodes.approvalBackdrop?.addEventListener("click", (event) => {
      if (event.target === nodes.approvalBackdrop) closeApprovalModal();
    });
    nodes.resolutionBackdrop.addEventListener("click", (event) => {
      if (event.target === nodes.resolutionBackdrop) closeResolution();
    });
    document.addEventListener("keydown", async (event) => {
      if (event.key !== "Escape") return;
      if (!nodes.modalBackdrop.hidden) {
        await requestCloseForm();
        return;
      }
      closeResolution();
    });
    window.addEventListener("pageshow", closeAllModals);
    window.addEventListener("beforeunload", (event) => {
      if (!state.formDirty) return;
      event.preventDefault();
      event.returnValue = "";
    });
    fields.clientId.addEventListener("change", () => syncClientFinancials(true));
    fields.product.addEventListener("change", () => {
      applySelectedProduct();
      previewPlan();
    });
    [
      fields.amount,
      fields.termMonths,
      fields.requestDate,
      fields.declaredIncome,
      fields.declaredExpenses,
      fields.guaranteeType,
      fields.guaranteeValue,
      fields.riskLevel,
      fields.conamiClassification,
    ].forEach((field) => {
      field?.addEventListener("change", () => {
        syncCommitteeAndChecklist();

        if (nodes.modalBackdrop.hidden || Number(fields.amount.value || 0) <= 0) {
          return;
        }

        window.clearTimeout(state.planPreviewTimer);
        state.planPreviewTimer = window.setTimeout(previewPlan, 350);
      });
    });
    nodes.tableBody.addEventListener("click", (event) => {
      const viewButton = event.target.closest("[data-view-id]");
      if (viewButton) {
        selectRequest(viewButton.dataset.viewId).then(openApprovalModal);
        return;
      }
      const card = event.target.closest("[data-id]");
      if (card) selectRequest(card.dataset.id);
    });
    nodes.tableBody.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") return;
      const card = event.target.closest("[data-id]");
      if (!card) return;
      event.preventDefault();
      selectRequest(card.dataset.id);
    });
    [nodes.searchInput, nodes.statusFilter].forEach((node) => {
      node.addEventListener(node === nodes.searchInput ? "input" : "change", () => {
        window.clearTimeout(node._timer);
        node._timer = window.setTimeout(loadRequests, 250);
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
    if (nodes.reportCutoffDate) {
      nodes.reportCutoffDate.value = new Date().toISOString().slice(0, 10);
    }
    window.SifnicTheme?.attachToggle(nodes.themeToggle, nodes.themeToggleLabel, null);
    closeAllModals();
    document.body.classList.add("modals-ready");
    bindEvents();
    await loadCatalogs();
    await loadRequests();
  };

  boot().catch((error) => {
    console.error(error);
    renderEmptyDetail();
  });
})();
