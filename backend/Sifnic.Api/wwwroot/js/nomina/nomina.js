(() => {
  const sessionApi = window.SifnicSession;
  const EMPLOYEE_CONTRACT_CODES = new Set(["FIJO", "TEMPORAL", "INDETERMINADO", "INDETERMINADA"]);
  const SERVICE_CONTRACT_CODES = new Set([
    "SERVICIOS",
    "PROFESIONALPERSONANATURAL",
    "SERVICIOGENERAL",
  ]);

  const state = {
    activeWorkspace: "resumen",
    context: null,
    configSnapshot: null,
    payrollDetail: null,
    selectedPeriodId: null,
    selectedPayrollId: null,
    selectedDetailId: null,
    liquidationPreview: null,
    liquidationDetail: null,
    selectedLiquidationId: null,
  };

  const elements = {
    backToDashboard: document.getElementById("backToDashboard"),
    refreshContextButton: document.getElementById("refreshContextButton"),
    logoutButton: document.getElementById("logoutButton"),
    sessionUser: document.getElementById("sessionUser"),
    sessionMeta: document.getElementById("sessionMeta"),
    toastStack: document.getElementById("toastStack"),
    workspaceButtons: [...document.querySelectorAll("[data-workspace]")],
    workspacePanels: [...document.querySelectorAll("[data-workspace-panel]")],
    cyclePeriodCode: document.getElementById("cyclePeriodCode"),
    cyclePeriodStatus: document.getElementById("cyclePeriodStatus"),
    cycleNextStep: document.getElementById("cycleNextStep"),
    cyclePayDate: document.getElementById("cyclePayDate"),
    cycleIncludedEmployees: document.getElementById("cycleIncludedEmployees"),
    cycleLastRun: document.getElementById("cycleLastRun"),
    overviewHighlights: document.getElementById("overviewHighlights"),
    contractPopulationBody: document.getElementById("contractPopulationBody"),
    configForm: document.getElementById("configForm"),
    companyRegimen: document.getElementById("companyRegimen"),
    companyHeadcount: document.getElementById("companyHeadcount"),
    internshipMode: document.getElementById("internshipMode"),
    payrollDaysMonth: document.getElementById("payrollDaysMonth"),
    payrollHoursBase: document.getElementById("payrollHoursBase"),
    saveConfigButton: document.getElementById("saveConfigButton"),
    configRules: document.getElementById("configRules"),
    periodForm: document.getElementById("periodForm"),
    periodCode: document.getElementById("periodCode"),
    periodStartDate: document.getElementById("periodStartDate"),
    periodEndDate: document.getElementById("periodEndDate"),
    periodPayDate: document.getElementById("periodPayDate"),
    periodType: document.getElementById("periodType"),
    overtimeCutoffDate: document.getElementById("overtimeCutoffDate"),
    periodObservation: document.getElementById("periodObservation"),
    clearPeriodButton: document.getElementById("clearPeriodButton"),
    openPeriodButton: document.getElementById("openPeriodButton"),
    periodsTableBody: document.getElementById("periodsTableBody"),
    contributionsBody: document.getElementById("contributionsBody"),
    irTableBody: document.getElementById("irTableBody"),
    selectedPeriodStatus: document.getElementById("selectedPeriodStatus"),
    selectedPeriodTitle: document.getElementById("selectedPeriodTitle"),
    selectedPeriodMeta: document.getElementById("selectedPeriodMeta"),
    selectedPeriodCutoff: document.getElementById("selectedPeriodCutoff"),
    generatePayrollButton: document.getElementById("generatePayrollButton"),
    payrollConfirmModal: document.getElementById("payrollConfirmModal"),
    closePayrollConfirmButton: document.getElementById("closePayrollConfirmButton"),
    cancelGeneratePayrollButton: document.getElementById("cancelGeneratePayrollButton"),
    confirmGeneratePayrollButton: document.getElementById("confirmGeneratePayrollButton"),
    confirmPeriodStatus: document.getElementById("confirmPeriodStatus"),
    confirmPeriodTitle: document.getElementById("confirmPeriodTitle"),
    confirmPeriodMeta: document.getElementById("confirmPeriodMeta"),
    confirmPeriodCutoff: document.getElementById("confirmPeriodCutoff"),
    closePayrollButton: document.getElementById("closePayrollButton"),
    generalReportButton: document.getElementById("generalReportButton"),
    generalReportExcelButton: document.getElementById("generalReportExcelButton"),
    generalReportButtonAlt: document.getElementById("generalReportButtonAlt"),
    generalReportExcelButtonAlt: document.getElementById("generalReportExcelButtonAlt"),
    employeePayslipButton: document.getElementById("employeePayslipButton"),
    liquidationForm: document.getElementById("liquidationForm"),
    liquidationEmployee: document.getElementById("liquidationEmployee"),
    liquidationDate: document.getElementById("liquidationDate"),
    liquidationTerminationDate: document.getElementById("liquidationTerminationDate"),
    liquidationCause: document.getElementById("liquidationCause"),
    pendingSalaryDays: document.getElementById("pendingSalaryDays"),
    liquidationReason: document.getElementById("liquidationReason"),
    clearLiquidationButton: document.getElementById("clearLiquidationButton"),
    reviewLiquidationButton: document.getElementById("reviewLiquidationButton"),
    processLiquidationButton: document.getElementById("processLiquidationButton"),
    liquidationReportButton: document.getElementById("liquidationReportButton"),
    liquidationExcelButton: document.getElementById("liquidationExcelButton"),
    recommendationLetterButton: document.getElementById("recommendationLetterButton"),
    liquidationPreviewTitle: document.getElementById("liquidationPreviewTitle"),
    liquidationPreviewMeta: document.getElementById("liquidationPreviewMeta"),
    liquidationPreviewNet: document.getElementById("liquidationPreviewNet"),
    liquidationDetailTitle: document.getElementById("liquidationDetailTitle"),
    liquidationDetailMeta: document.getElementById("liquidationDetailMeta"),
    liqTaxableTotal: document.getElementById("liqTaxableTotal"),
    liqNonTaxableTotal: document.getElementById("liqNonTaxableTotal"),
    liqDeductionsTotal: document.getElementById("liqDeductionsTotal"),
    liqNetTotal: document.getElementById("liqNetTotal"),
    liquidationLinesBody: document.getElementById("liquidationLinesBody"),
    liquidationEmployeeName: document.getElementById("liquidationEmployeeName"),
    liquidationEmployeeMeta: document.getElementById("liquidationEmployeeMeta"),
    liquidationFacts: document.getElementById("liquidationFacts"),
    liquidationNotes: document.getElementById("liquidationNotes"),
    liquidationHistoryBody: document.getElementById("liquidationHistoryBody"),
    liquidationConfirmModal: document.getElementById("liquidationConfirmModal"),
    closeLiquidationConfirmButton: document.getElementById("closeLiquidationConfirmButton"),
    cancelLiquidationConfirmButton: document.getElementById("cancelLiquidationConfirmButton"),
    confirmGenerateLiquidationButton: document.getElementById("confirmGenerateLiquidationButton"),
    liquidationConfirmStatus: document.getElementById("liquidationConfirmStatus"),
    liquidationConfirmEmployee: document.getElementById("liquidationConfirmEmployee"),
    liquidationConfirmCause: document.getElementById("liquidationConfirmCause"),
    liquidationConfirmNet: document.getElementById("liquidationConfirmNet"),
    payrollRunTitle: document.getElementById("payrollRunTitle"),
    payrollRunMeta: document.getElementById("payrollRunMeta"),
    detailsTableBody: document.getElementById("detailsTableBody"),
    selectedEmployeeName: document.getElementById("selectedEmployeeName"),
    selectedEmployeeMeta: document.getElementById("selectedEmployeeMeta"),
    selectedEmployeeFacts: document.getElementById("selectedEmployeeFacts"),
    conceptsTableBody: document.getElementById("conceptsTableBody"),
    metricEmployees: document.getElementById("metricEmployees"),
    metricInterns: document.getElementById("metricInterns"),
    metricServices: document.getElementById("metricServices"),
    metricOpenPeriods: document.getElementById("metricOpenPeriods"),
    metricLastCost: document.getElementById("metricLastCost"),
    metricLastCostMeta: document.getElementById("metricLastCostMeta"),
    sumGrossEmployees: document.getElementById("sumGrossEmployees"),
    sumInternships: document.getElementById("sumInternships"),
    sumServices: document.getElementById("sumServices"),
    sumInssLabor: document.getElementById("sumInssLabor"),
    sumInssEmployer: document.getElementById("sumInssEmployer"),
    sumIrEmployees: document.getElementById("sumIrEmployees"),
    sumServiceRetention: document.getElementById("sumServiceRetention"),
    sumNetTotal: document.getElementById("sumNetTotal"),
    reportEmployeeTitle: document.getElementById("reportEmployeeTitle"),
    reportEmployeeMeta: document.getElementById("reportEmployeeMeta"),
    reportPeriodTitle: document.getElementById("reportPeriodTitle"),
    reportPeriodMeta: document.getElementById("reportPeriodMeta"),
    obligationPeriodTitle: document.getElementById("obligationPeriodTitle"),
    obligationPeriodMeta: document.getElementById("obligationPeriodMeta"),
    obligationInssLabor: document.getElementById("obligationInssLabor"),
    obligationInssEmployer: document.getElementById("obligationInssEmployer"),
    obligationIrWorkers: document.getElementById("obligationIrWorkers"),
    obligationServiceRetention: document.getElementById("obligationServiceRetention"),
    obligationInssTotal: document.getElementById("obligationInssTotal"),
    obligationDgiTotal: document.getElementById("obligationDgiTotal"),
    obligationNetTotal: document.getElementById("obligationNetTotal"),
    obligationCompanyCost: document.getElementById("obligationCompanyCost"),
  };

  const emptyArray = (value) => (Array.isArray(value) ? value : []);
  const safeNumber = (value) => Number(value || 0);
  const normalizeCode = (value) => String(value || "").trim().toUpperCase().replace(/[^A-Z0-9]/g, "");

  const currentSession = () => sessionApi.getSession();

  const formatDate = (value) => {
    if (!value) {
      return "Sin fecha";
    }

    try {
      const normalized = String(value).includes("T") ? String(value) : `${value}T00:00:00`;
      return new Intl.DateTimeFormat("es-NI", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        timeZone: "America/Managua",
      }).format(new Date(normalized));
    } catch {
      return value;
    }
  };

  const formatDateTime = (value) => {
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

  const todayInputValue = () => {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
  };

  const formatAmount = (amount) =>
    new Intl.NumberFormat("es-NI", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(safeNumber(amount));

  const moneyLabel = (amount, currency = "NIO") => `${currency} ${formatAmount(amount)}`;

  const statusClass = (status) => {
    const code = normalizeCode(status);
    if (["ABIERTO", "GENERADO", "GENERADA"].includes(code)) {
      return "is-open";
    }

    if (["CERRADO", "CERRADA"].includes(code)) {
      return "is-closed";
    }

    return "is-empty";
  };

  const paymentTypeLabel = (value) => {
    switch (normalizeCode(value)) {
      case "EMPLEADONOMINA":
        return "Empleado nomina";
      case "PASANTEAYUDA":
        return "Pasante ayuda";
      case "SERVICIOPROFESIONAL":
        return "Servicio profesional";
      default:
        return "Sin clasificar";
    }
  };

  const openUrlWithSession = (url) => {
    const token = currentSession()?.sessionToken;
    const separator = url.includes("?") ? "&" : "?";
    const finalUrl = token ? `${url}${separator}sessionToken=${encodeURIComponent(token)}` : url;
    window.open(finalUrl, "_blank", "noopener");
  };

  const normalizeConfigPayload = (payload) => ({
    regimenInssEmpresa: String(payload?.regimenInssEmpresa || "INTEGRAL").trim().toUpperCase(),
    cantidadTrabajadoresEmpresa: safeNumber(payload?.cantidadTrabajadoresEmpresa),
    modoPasantiaPorDefecto: String(payload?.modoPasantiaPorDefecto || "NO_NOMINA").trim().toUpperCase(),
    diasMesNomina: safeNumber(payload?.diasMesNomina),
    horasMesBase: safeNumber(payload?.horasMesBase),
  });

  const buildConfigPayload = () => ({
    regimenInssEmpresa: elements.companyRegimen.value,
    cantidadTrabajadoresEmpresa: safeNumber(elements.companyHeadcount.value),
    modoPasantiaPorDefecto: elements.internshipMode.value,
    diasMesNomina: safeNumber(elements.payrollDaysMonth.value),
    horasMesBase: safeNumber(elements.payrollHoursBase.value),
  });

  const hasConfigChanges = (payload) =>
    JSON.stringify(normalizeConfigPayload(payload)) !== JSON.stringify(state.configSnapshot || {});

  const setButtonBusy = (button, busy, busyText) => {
    if (!button) {
      return;
    }

    if (!button.dataset.originalText) {
      button.dataset.originalText = button.textContent.trim();
    }

    button.disabled = busy;
    button.textContent = busy ? busyText : button.dataset.originalText;
  };

  const showToast = (message, tone = "success") => {
    if (!elements.toastStack) {
      return;
    }

    const toast = document.createElement("div");
    toast.className = `toast is-${tone}`;
    toast.textContent = message;
    elements.toastStack.appendChild(toast);

    window.setTimeout(() => {
      toast.remove();
    }, 3200);
  };

  const setWorkspace = (workspace) => {
    state.activeWorkspace = workspace;

    elements.workspaceButtons.forEach((button) => {
      button.classList.toggle("is-active", button.dataset.workspace === workspace);
    });

    elements.workspacePanels.forEach((panel) => {
      panel.hidden = panel.dataset.workspacePanel !== workspace;
    });
  };

  const closeGenerateConfirmation = () => {
    if (!elements.payrollConfirmModal) {
      return;
    }

    elements.payrollConfirmModal.hidden = true;
    document.body.classList.remove("modal-open");
  };

  const syncGenerateConfirmation = () => {
    const period = getSelectedPeriod();
    const payroll = getSelectedPayroll();

    elements.confirmPeriodStatus.className = `status-pill ${statusClass(payroll?.status || period?.status)}`;
    elements.confirmPeriodStatus.textContent = payroll?.statusLabel || period?.statusLabel || "Sin seleccion";

    if (!period) {
      elements.confirmPeriodTitle.textContent = "Sin periodo seleccionado";
      elements.confirmPeriodMeta.textContent = "Selecciona un periodo antes de procesar la nomina.";
      elements.confirmPeriodCutoff.textContent = "Sin fecha de corte configurada.";
      return;
    }

    elements.confirmPeriodTitle.textContent = `${period.code} | ${formatDate(period.startDate)} al ${formatDate(period.endDate)}`;
    elements.confirmPeriodMeta.textContent = `Pago ${formatDate(period.payDate)} | tipo ${String(period.periodType || "").toLowerCase()} | estado ${String(period.statusLabel || "").toLowerCase()}`;
    elements.confirmPeriodCutoff.textContent = period.overtimeCutoffDate
      ? `Horas extra incluidas hasta ${formatDate(period.overtimeCutoffDate)}.`
      : "Sin corte personalizado: se usara la fecha final del periodo.";
  };

  const openGenerateConfirmation = () => {
    if (!state.selectedPeriodId) {
      showToast("Selecciona un periodo.", "warning");
      return;
    }

    if (elements.generatePayrollButton?.disabled) {
      showToast("Ese periodo ya no se puede procesar.", "warning");
      return;
    }

    syncGenerateConfirmation();
    elements.payrollConfirmModal.hidden = false;
    document.body.classList.add("modal-open");
  };

  const closeLiquidationConfirmation = () => {
    if (!elements.liquidationConfirmModal) {
      return;
    }

    elements.liquidationConfirmModal.hidden = true;
    document.body.classList.remove("modal-open");
  };

  const currentLiquidationView = () => state.liquidationPreview || state.liquidationDetail || null;

  const syncLiquidationConfirmation = () => {
    const payload = currentLiquidationView();
    if (!payload) {
      elements.liquidationConfirmStatus.textContent = "Sin revision";
      elements.liquidationConfirmEmployee.textContent = "Sin colaborador seleccionado";
      elements.liquidationConfirmCause.textContent = "Primero revisa una liquidacion.";
      elements.liquidationConfirmNet.textContent = "Neto pendiente de calculo.";
      return;
    }

    elements.liquidationConfirmStatus.textContent = payload.persisted ? "Registrada" : "En revision";
    elements.liquidationConfirmStatus.className = `status-pill ${payload.persisted ? "is-closed" : "is-open"}`;
    elements.liquidationConfirmEmployee.textContent = `${payload.header.codigoEmpleado} | ${payload.header.nombreEmpleado}`;
    elements.liquidationConfirmCause.textContent = `${payload.cause.label} | baja ${formatDate(payload.header.fechaBaja)}`;
    elements.liquidationConfirmNet.textContent = `Neto final ${moneyLabel(payload.totals.netoLiquidacion, payload.header.moneda)}.`;
  };

  const openLiquidationConfirmation = () => {
    if (!state.liquidationPreview || state.liquidationPreview.persisted) {
      showToast("Revisa una liquidacion nueva antes de procesarla.", "warning");
      return;
    }

    syncLiquidationConfirmation();
    elements.liquidationConfirmModal.hidden = false;
    document.body.classList.add("modal-open");
  };

  const getSelectedPeriod = () =>
    emptyArray(state.context?.periods).find((item) => Number(item.id) === Number(state.selectedPeriodId)) || null;

  const getSelectedPayroll = () =>
    emptyArray(state.context?.payrolls).find((item) => Number(item.id) === Number(state.selectedPayrollId)) || null;

  const getSelectedDetail = () =>
    emptyArray(state.payrollDetail?.details).find(
      (item) => Number(item.idNominaDetalle) === Number(state.selectedDetailId),
    ) || null;

  const buildRules = () => {
    const config = state.context?.config || {};
    const rules = [
      {
        title: "INSS patronal",
        body: `${config.regimenInssEmpresa || "INTEGRAL"} con ${safeNumber(config.cantidadTrabajadoresEmpresa)} trabajadores para calcular el aporte empresa.`,
      },
      {
        title: "Horas extra",
        body: "Solo entran a nomina las horas extra aprobadas y dentro de la fecha de corte del periodo.",
      },
      {
        title: "Variables y deducciones",
        body:
          "Se suman salario base, vacaciones aprobadas, movimientos variables activos y devengados aprobados. Se descuentan INSS, IR, descuentos fijos, fondo de ahorro y prestamos vigentes.",
      },
      {
        title: "Pasantias",
        body:
          config.modoPasantiaPorDefecto === "COMO_EMPLEADO"
            ? "Las pasantias se procesan como empleado segun la parametrizacion actual."
            : "Las pasantias se dejan fuera de nomina como ayuda economica mientras siga ese parametro.",
      },
      {
        title: "IR e INSS",
        body: "Las tasas y tramos se leen desde tablas de configuracion vigentes, no quemadas en codigo.",
      },
      {
        title: "Base salario hora",
        body: `${formatAmount(config.diasMesNomina || 30)} dias mes y ${formatAmount(config.horasMesBase || 240)} horas base para variables operativas.`,
      },
    ];

    return rules;
  };

  const buildOverviewHighlights = () => {
    const config = state.context?.config || {};

    return [
      {
        label: "Regimen INSS",
        value: config.regimenInssEmpresa || "INTEGRAL",
        meta: "Configuracion patronal activa para la empresa.",
      },
      {
        label: "Plantilla empresa",
        value: `${safeNumber(config.cantidadTrabajadoresEmpresa || 0)}`,
        meta: "Cantidad usada para definir el aporte patronal.",
      },
      {
        label: "Tratamiento pasantia",
        value: config.modoPasantiaPorDefecto === "COMO_EMPLEADO" ? "Como empleado" : "No nomina",
        meta: "Comportamiento por defecto de ayuda economica.",
      },
      {
        label: "Base operativa",
        value: `${formatAmount(config.diasMesNomina || 30)} d / ${formatAmount(config.horasMesBase || 240)} h`,
        meta: "Dias de nomina y horas base para calculos variables.",
      },
    ];
  };

  const contractPopulationTotals = () => {
    return emptyArray(state.context?.contractPopulation).reduce(
      (accumulator, item) => {
        const code = normalizeCode(item.code);
        const total = safeNumber(item.total);

        if (EMPLOYEE_CONTRACT_CODES.has(code)) {
          accumulator.employees += total;
        } else if (code === "PASANTIA") {
          accumulator.interns += total;
        } else if (SERVICE_CONTRACT_CODES.has(code)) {
          accumulator.services += total;
        }

        return accumulator;
      },
      { employees: 0, interns: 0, services: 0 },
    );
  };

  const renderMetrics = () => {
    const population = contractPopulationTotals();
    const payrolls = emptyArray(state.context?.payrolls);
    const openPeriods = emptyArray(state.context?.periods).filter((item) => normalizeCode(item.status) === "ABIERTO");
    const lastPayroll = payrolls[0] || null;

    elements.metricEmployees.textContent = String(population.employees);
    elements.metricInterns.textContent = String(population.interns);
    elements.metricServices.textContent = String(population.services);
    elements.metricOpenPeriods.textContent = String(openPeriods.length);
    elements.metricLastCost.textContent = moneyLabel(lastPayroll?.totalEmployerCost || 0, "NIO");
    elements.metricLastCostMeta.textContent = lastPayroll
      ? `${lastPayroll.periodCode} | ${safeNumber(lastPayroll.employees)} colaboradores`
      : "Sin corte generado.";
  };

  const resolveCycleContext = () => {
    const periods = emptyArray(state.context?.periods);
    const payrolls = emptyArray(state.context?.payrolls);
    const selectedPeriod = getSelectedPeriod();
    const openPeriod = periods.find((item) => normalizeCode(item.status) === "ABIERTO") || null;
    const period = selectedPeriod || openPeriod || periods[0] || null;
    const payroll =
      getSelectedPayroll() ||
      payrolls.find((item) => Number(item.periodId) === Number(period?.id)) ||
      null;
    const lastPayroll = payrolls[0] || null;

    let nextStep = "Abrir periodo";
    if (period && !payroll && normalizeCode(period.status) !== "CERRADO") {
      nextStep = "Registrar variables y procesar";
    } else if (payroll && normalizeCode(payroll.status) !== "CERRADA") {
      nextStep = "Revisar resultados y cerrar";
    } else if (payroll) {
      nextStep = "Emitir reportes y obligaciones";
    }

    return { period, payroll, lastPayroll, nextStep };
  };

  const renderCycleStrip = () => {
    if (!elements.cyclePeriodCode) {
      return;
    }

    const { period, payroll, lastPayroll, nextStep } = resolveCycleContext();
    const status = payroll?.status || period?.status;
    const statusLabel = payroll?.statusLabel || period?.statusLabel || "Pendiente";

    elements.cyclePeriodCode.textContent = period?.code || "Sin periodo";
    elements.cyclePeriodStatus.className = `status-pill ${statusClass(status)}`;
    elements.cyclePeriodStatus.textContent = statusLabel;
    elements.cycleNextStep.textContent = nextStep;
    elements.cyclePayDate.textContent = formatDate(payroll?.payDate || period?.payDate);
    elements.cycleIncludedEmployees.textContent = `${safeNumber(payroll?.employees || contractPopulationTotals().employees)} colaboradores`;
    elements.cycleLastRun.textContent = lastPayroll?.code || lastPayroll?.periodCode || "Sin corte";
  };

  const renderOverview = () => {
    const highlights = buildOverviewHighlights();
    elements.overviewHighlights.innerHTML = highlights
      .map(
        (item) => `
          <article class="overview-highlight-card">
            <span>${item.label}</span>
            <strong>${item.value}</strong>
            <p>${item.meta}</p>
          </article>
        `,
      )
      .join("");

    const rows = emptyArray(state.context?.contractPopulation);
    elements.contractPopulationBody.innerHTML = rows.length
      ? rows
          .map(
            (row) => `
              <tr>
                <td>${row.name}</td>
                <td class="number">${safeNumber(row.total)}</td>
              </tr>
            `,
          )
          .join("")
      : `
          <tr>
            <td colspan="2">Sin contratos para mostrar.</td>
          </tr>
        `;
  };

  const renderConfigRules = () => {
    const rules = buildRules();
    elements.configRules.innerHTML = rules
      .map(
        (rule) => `
          <article class="rule-item">
            <strong>${rule.title}</strong>
            <p>${rule.body}</p>
          </article>
        `,
      )
      .join("");
  };

  const renderConfig = () => {
    const config = state.context?.config || {};
    elements.companyRegimen.value = config.regimenInssEmpresa || "INTEGRAL";
    elements.companyHeadcount.value = safeNumber(config.cantidadTrabajadoresEmpresa || 1);
    elements.internshipMode.value = config.modoPasantiaPorDefecto || "NO_NOMINA";
    elements.payrollDaysMonth.value = safeNumber(config.diasMesNomina || 30);
    elements.payrollHoursBase.value = safeNumber(config.horasMesBase || 240);
    state.configSnapshot = normalizeConfigPayload({
      regimenInssEmpresa: elements.companyRegimen.value,
      cantidadTrabajadoresEmpresa: elements.companyHeadcount.value,
      modoPasantiaPorDefecto: elements.internshipMode.value,
      diasMesNomina: elements.payrollDaysMonth.value,
      horasMesBase: elements.payrollHoursBase.value,
    });
  };

  const renderRateTables = () => {
    const contributionRows = emptyArray(state.context?.contributions);
    elements.contributionsBody.innerHTML = contributionRows.length
      ? contributionRows
          .map(
            (row) => `
              <tr>
                <td>${row.code}</td>
                <td>${row.type || "Sin tipo"}</td>
                <td class="number">${formatAmount(row.percent || 0)}%</td>
                <td>${formatDate(row.startDate)}${row.endDate ? ` al ${formatDate(row.endDate)}` : " en adelante"}</td>
              </tr>
            `,
          )
          .join("")
      : `
          <tr>
            <td colspan="4">No hay tasas vigentes registradas.</td>
          </tr>
        `;

    const irRows = emptyArray(state.context?.irTable);
    elements.irTableBody.innerHTML = irRows.length
      ? irRows
          .map(
            (row) => `
              <tr>
                <td>${moneyLabel(row.annualFrom || 0, "NIO")}</td>
                <td>${row.annualTo == null ? "Sin techo" : moneyLabel(row.annualTo, "NIO")}</td>
                <td class="number">${moneyLabel(row.annualBaseTax || 0, "NIO")}</td>
                <td class="number">${formatAmount(row.excessPercent || 0)}%</td>
              </tr>
            `,
          )
          .join("")
      : `
          <tr>
            <td colspan="4">No hay tramos IR vigentes.</td>
          </tr>
        `;
  };

  const renderSelectionSummary = () => {
    const period = getSelectedPeriod();
    const payroll = getSelectedPayroll();
    const selectedStatus = payroll?.statusLabel || period?.statusLabel || "Sin seleccion";

    elements.selectedPeriodStatus.className = `status-pill ${statusClass(payroll?.status || period?.status)}`;
    elements.selectedPeriodStatus.textContent = selectedStatus;

    if (!period) {
      elements.selectedPeriodTitle.textContent = "Selecciona un periodo para procesar.";
      elements.selectedPeriodMeta.textContent =
        "Aqui podras procesar la nomina, cerrarla y sacar el reporte general.";
      elements.selectedPeriodCutoff.textContent = "Sin fecha de corte configurada todavia.";
      elements.generatePayrollButton.disabled = true;
      elements.closePayrollButton.disabled = true;
      elements.generalReportButton.disabled = true;
      elements.generalReportExcelButton.disabled = true;
      elements.generalReportButtonAlt.disabled = true;
      elements.generalReportExcelButtonAlt.disabled = true;
      return;
    }

    elements.selectedPeriodTitle.textContent = `${period.code} | ${formatDate(period.startDate)} al ${formatDate(period.endDate)}`;
    elements.selectedPeriodMeta.textContent = payroll
      ? `Nomina ${String(payroll.statusLabel || "").toLowerCase()} | ${safeNumber(payroll.employees)} colaboradores | pago ${formatDate(payroll.payDate)}`
      : `Periodo ${String(period.statusLabel || "").toLowerCase()} | pago ${formatDate(period.payDate)} | tipo ${String(period.periodType || "").toLowerCase()}`;
    elements.selectedPeriodCutoff.textContent = period.overtimeCutoffDate
      ? `Horas extra incluidas hasta ${formatDate(period.overtimeCutoffDate)}.`
      : "Sin corte personalizado: se usara la fecha final del periodo.";

    elements.generatePayrollButton.disabled = Boolean(payroll) || normalizeCode(period.status) === "CERRADO";
    elements.closePayrollButton.disabled = !payroll || normalizeCode(payroll.status) === "CERRADA";
    elements.generalReportButton.disabled = !payroll;
    elements.generalReportExcelButton.disabled = !payroll;
    elements.generalReportButtonAlt.disabled = !payroll;
    elements.generalReportExcelButtonAlt.disabled = !payroll;
  };

  const renderPeriods = () => {
    const payrollMap = new Map(
      emptyArray(state.context?.payrolls).map((item) => [Number(item.periodId), item]),
    );

    const rows = emptyArray(state.context?.periods);
    elements.periodsTableBody.innerHTML = rows.length
      ? rows
          .map((period) => {
            const payroll = payrollMap.get(Number(period.id)) || null;
            return `
              <tr data-period-id="${period.id}" data-payroll-id="${payroll?.id || ""}" class="${
                Number(period.id) === Number(state.selectedPeriodId) ? "is-selected" : ""
              }">
                <td>
                  <div class="row-title">
                    <strong>${period.code}</strong>
                    <small>${period.periodType}</small>
                  </div>
                </td>
                <td>${formatDate(period.startDate)} - ${formatDate(period.endDate)}</td>
                <td>${formatDate(period.payDate)}</td>
                <td>${period.overtimeCutoffDate ? formatDate(period.overtimeCutoffDate) : "Final del periodo"}</td>
                <td><span class="status-pill ${statusClass(period.status)}">${period.statusLabel}</span></td>
                <td>${payroll ? `<span class="status-pill ${statusClass(payroll.status)}">${payroll.statusLabel}</span>` : "Pendiente"}</td>
                <td class="number">${safeNumber(payroll?.employees)}</td>
                <td class="number">${moneyLabel(payroll?.totalNet || 0, "NIO")}</td>
              </tr>
            `;
          })
          .join("")
      : `
          <tr>
            <td colspan="8">Todavia no hay periodos registrados.</td>
          </tr>
        `;
  };

  const renderPayrollSummary = () => {
    const summary = state.payrollDetail?.summary || {};
    elements.sumGrossEmployees.textContent = moneyLabel(summary.totalBrutoNomina || 0, "NIO");
    elements.sumInternships.textContent = moneyLabel(summary.totalPasantes || 0, "NIO");
    elements.sumServices.textContent = moneyLabel(summary.totalServicios || 0, "NIO");
    elements.sumInssLabor.textContent = moneyLabel(summary.totalInssLaboral || 0, "NIO");
    elements.sumInssEmployer.textContent = moneyLabel(summary.totalInssPatronal || 0, "NIO");
    elements.sumIrEmployees.textContent = moneyLabel(summary.totalIrTrabajadores || 0, "NIO");
    elements.sumServiceRetention.textContent = moneyLabel(summary.totalRetencionesServicios || 0, "NIO");
    elements.sumNetTotal.textContent = moneyLabel(summary.totalNeto || 0, "NIO");
  };

  const renderDetailsTable = () => {
    const details = emptyArray(state.payrollDetail?.details);
    elements.detailsTableBody.innerHTML = details.length
      ? details
          .map(
            (row) => `
              <tr data-detail-id="${row.idNominaDetalle}" class="${
                Number(row.idNominaDetalle) === Number(state.selectedDetailId) ? "is-selected" : ""
              }">
                <td>
                  <div class="row-title">
                    <strong>${row.nombreEmpleado}</strong>
                    <small>${row.codigoEmpleado} | ${row.cargo || "Sin cargo"}</small>
                  </div>
                </td>
                <td>${paymentTypeLabel(row.tipoPago)}</td>
                <td class="number">${moneyLabel(row.totalIngresos, row.moneda)}</td>
                <td class="number">${moneyLabel(row.totalDeducciones, row.moneda)}</td>
                <td class="number">${moneyLabel(row.netoPagar, row.moneda)}</td>
              </tr>
            `,
          )
          .join("")
      : `
          <tr>
            <td colspan="5">Todavia no hay detalle procesado para esta nomina.</td>
          </tr>
        `;
  };

  const renderSelectedEmployee = () => {
    const detail = getSelectedDetail();
    const concepts = emptyArray(state.payrollDetail?.concepts).filter(
      (item) => Number(item.detailId) === Number(state.selectedDetailId),
    );

    if (!detail) {
      elements.selectedEmployeeName.textContent = "Sin seleccion";
      elements.selectedEmployeeMeta.textContent = "Selecciona un empleado para ver su desglose.";
      elements.selectedEmployeeFacts.innerHTML = "";
      elements.conceptsTableBody.innerHTML = `
        <tr>
          <td colspan="3">No hay conceptos para mostrar.</td>
        </tr>
      `;
      elements.employeePayslipButton.disabled = true;
      return;
    }

    elements.employeePayslipButton.disabled = false;
    elements.selectedEmployeeName.textContent = detail.nombreEmpleado;
    elements.selectedEmployeeMeta.textContent = `${detail.codigoEmpleado} | ${detail.nombreTipoContrato} | ${paymentTypeLabel(detail.tipoPago)}`;

    const facts = [
      ["Departamento", detail.departamento || "Sin departamento"],
      ["Cargo", detail.cargo || "Sin cargo"],
      ["Cedula", detail.cedula || "Sin cedula"],
      ["INSS", detail.inss || "Sin INSS"],
      ["Cuenta", detail.cuentaBancaria || "Sin cuenta"],
      ["Correo", detail.correo || "Sin correo"],
      ["Ingresos", moneyLabel(detail.totalIngresos, detail.moneda)],
      ["Deducciones", moneyLabel(detail.totalDeducciones, detail.moneda)],
      ["Aportes patronales", moneyLabel(detail.totalAportesPatronales, detail.moneda)],
      ["Neto", moneyLabel(detail.netoPagar, detail.moneda)],
      ["Costo empresa", moneyLabel(safeNumber(detail.totalIngresos) + safeNumber(detail.totalAportesPatronales), detail.moneda)],
    ];

    elements.selectedEmployeeFacts.innerHTML = facts
      .map(
        ([label, value]) => `
          <article class="detail-item">
            <span>${label}</span>
            <strong>${value}</strong>
          </article>
        `,
      )
      .join("");

    elements.conceptsTableBody.innerHTML = concepts.length
      ? concepts
          .sort((left, right) => safeNumber(left.visualOrder) - safeNumber(right.visualOrder))
          .map(
            (concept) => `
              <tr>
                <td>
                  <div class="row-title">
                    <strong>${concept.name}</strong>
                    <small>${concept.reference || "Sin referencia"}</small>
                  </div>
                </td>
                <td>${concept.conceptType}</td>
                <td class="number">${moneyLabel(concept.amount, detail.moneda)}</td>
              </tr>
            `,
          )
          .join("")
      : `
          <tr>
            <td colspan="3">No hay conceptos asociados.</td>
          </tr>
        `;
  };

  const renderReports = () => {
    const detail = getSelectedDetail();
    const payroll = getSelectedPayroll();

    if (detail) {
      elements.reportEmployeeTitle.textContent = `${detail.nombreEmpleado}`;
      elements.reportEmployeeMeta.textContent = `${detail.codigoEmpleado} | ${detail.nombreTipoContrato} | neto ${moneyLabel(detail.netoPagar, detail.moneda)}`;
      elements.employeePayslipButton.disabled = false;
    } else {
      elements.reportEmployeeTitle.textContent = "Sin colaborador seleccionado.";
      elements.reportEmployeeMeta.textContent =
        "Selecciona un empleado dentro de la nomina procesada para abrir su esquela de pago.";
      elements.employeePayslipButton.disabled = true;
    }

    if (payroll) {
      elements.reportPeriodTitle.textContent = `${payroll.periodCode} | ${payroll.statusLabel}`;
      elements.reportPeriodMeta.textContent = `Pago ${formatDate(payroll.payDate)} | ${safeNumber(payroll.employees)} colaboradores | neto ${moneyLabel(payroll.totalNet, "NIO")}`;
      elements.generalReportButtonAlt.disabled = false;
      elements.generalReportExcelButtonAlt.disabled = false;
      elements.generalReportButton.disabled = false;
      elements.generalReportExcelButton.disabled = false;
    } else {
      elements.reportPeriodTitle.textContent = "Sin nomina generada seleccionada.";
      elements.reportPeriodMeta.textContent =
        "Desde aqui revisas el consolidado de pago neto, INSS laboral y patronal, IR retenido y costo empresa.";
      elements.generalReportButtonAlt.disabled = true;
      elements.generalReportExcelButtonAlt.disabled = true;
      elements.generalReportButton.disabled = true;
      elements.generalReportExcelButton.disabled = true;
    }
  };

  const renderObligations = () => {
    const run = state.payrollDetail?.run || null;
    const summary = state.payrollDetail?.summary || {};

    if (!run) {
      elements.obligationPeriodTitle.textContent = "Sin nomina generada seleccionada.";
      elements.obligationPeriodMeta.textContent =
        "Selecciona una nomina procesada para separar lo que se paga al colaborador de lo que se entera a terceros.";
    } else {
      elements.obligationPeriodTitle.textContent = `Periodo ${run.periodCode} | ${run.statusLabel}`;
      elements.obligationPeriodMeta.textContent = `Pago ${formatDate(run.payDate)} | generado ${formatDateTime(run.generatedAt)}`;
    }

    const inssLabor = safeNumber(summary.totalInssLaboral);
    const inssEmployer = safeNumber(summary.totalInssPatronal);
    const irWorkers = safeNumber(summary.totalIrTrabajadores);
    const retentionServices = safeNumber(summary.totalRetencionesServicios);
    const netTotal = safeNumber(summary.totalNeto);
    const employerCost = safeNumber(summary.totalCostoEmpresa);

    elements.obligationInssLabor.textContent = moneyLabel(inssLabor, "NIO");
    elements.obligationInssEmployer.textContent = moneyLabel(inssEmployer, "NIO");
    elements.obligationIrWorkers.textContent = moneyLabel(irWorkers, "NIO");
    elements.obligationServiceRetention.textContent = moneyLabel(retentionServices, "NIO");
    elements.obligationInssTotal.textContent = moneyLabel(inssLabor + inssEmployer, "NIO");
    elements.obligationDgiTotal.textContent = moneyLabel(irWorkers + retentionServices, "NIO");
    elements.obligationNetTotal.textContent = moneyLabel(netTotal, "NIO");
    elements.obligationCompanyCost.textContent = moneyLabel(employerCost, "NIO");
  };

  const renderPayrollPanel = () => {
    if (!state.payrollDetail) {
      elements.payrollRunTitle.textContent = "Detalle de nomina";
      elements.payrollRunMeta.textContent =
        "Selecciona un periodo con nomina generada para ver el desglose por empleado.";
      renderPayrollSummary();
      renderDetailsTable();
      renderSelectedEmployee();
      renderReports();
      renderObligations();
      return;
    }

    const run = state.payrollDetail.run || {};
    const snapshot = run.configSnapshot || {};
    const cutoff = snapshot.fechaCorteHoraExtra ? formatDate(snapshot.fechaCorteHoraExtra) : "final del periodo";

    elements.payrollRunTitle.textContent = `Nomina ${run.periodCode || ""}`;
    elements.payrollRunMeta.textContent =
      `${run.statusLabel || "Generada"} | pago ${formatDate(run.payDate)} | generado ${formatDateTime(run.generatedAt)} | corte horas extra ${cutoff}`;

    renderPayrollSummary();
    renderDetailsTable();
    renderSelectedEmployee();
    renderReports();
    renderObligations();
  };

  const renderLiquidationForm = () => {
    const candidates = emptyArray(state.context?.liquidationCandidates);
    const causes = emptyArray(state.context?.liquidationCauses);
    const selectedEmployee = elements.liquidationEmployee?.value || "";
    const selectedCause = elements.liquidationCause?.value || "";

    if (elements.liquidationEmployee) {
      elements.liquidationEmployee.innerHTML = candidates.length
        ? candidates
            .map(
              (item) => `
                <option value="${item.id}">
                  ${item.code} | ${item.name} | ${item.contractName}
                </option>
              `,
            )
            .join("")
        : `<option value="">Sin colaboradores activos</option>`;

      if (candidates.some((item) => String(item.id) === String(selectedEmployee))) {
        elements.liquidationEmployee.value = String(selectedEmployee);
      }
    }

    if (elements.liquidationCause) {
      elements.liquidationCause.innerHTML = causes.length
        ? causes
            .map(
              (item) => `
                <option value="${item.code}">
                  ${item.label}
                </option>
              `,
            )
            .join("")
        : `<option value="RENUNCIA_ART44">Renuncia con preaviso</option>`;

      if (causes.some((item) => item.code === selectedCause)) {
        elements.liquidationCause.value = selectedCause;
      }
    }

    if (elements.liquidationDate && !elements.liquidationDate.value) {
      elements.liquidationDate.value = todayInputValue();
    }

    if (elements.liquidationTerminationDate && !elements.liquidationTerminationDate.value) {
      elements.liquidationTerminationDate.value = elements.liquidationDate?.value || todayInputValue();
    }
  };

  const renderLiquidationHistory = () => {
    const rows = emptyArray(state.context?.liquidations);
    elements.liquidationHistoryBody.innerHTML = rows.length
      ? rows
          .map(
            (row) => `
              <tr data-liquidation-id="${row.id}" class="${
                Number(row.id) === Number(state.selectedLiquidationId) ? "is-selected" : ""
              }">
                <td>
                  <div class="row-title">
                    <strong>${row.employeeName}</strong>
                    <small>${row.employeeCode} | ${row.cargo || "Sin cargo"}</small>
                  </div>
                </td>
                <td>${row.causeLabel || "Sin causal"}</td>
                <td>${formatDate(row.terminationDate)}</td>
                <td class="number">${moneyLabel(row.netAmount || 0, row.currency || "NIO")}</td>
                <td>${row.registeredBy || "Sistema"}<br /><span class="table-note">${formatDateTime(row.registeredAt)}</span></td>
              </tr>
            `,
          )
          .join("")
      : `
          <tr>
            <td colspan="5">Todavia no hay liquidaciones registradas.</td>
          </tr>
        `;
  };

  const renderLiquidationPanel = () => {
    const payload = currentLiquidationView();

    if (!payload) {
      elements.liquidationPreviewTitle.textContent = "Sin liquidacion revisada.";
      elements.liquidationPreviewMeta.textContent =
        "Selecciona un colaborador, revisa el calculo y luego confirma el retiro.";
      elements.liquidationPreviewNet.textContent =
        "El neto final aparecera aqui cuando revises la liquidacion.";
      elements.liquidationDetailTitle.textContent = "Revision de liquidacion";
      elements.liquidationDetailMeta.textContent =
        "Aqui se separan prestaciones gravables, no gravables, deducciones y aportes patronales.";
      elements.liqTaxableTotal.textContent = moneyLabel(0, "NIO");
      elements.liqNonTaxableTotal.textContent = moneyLabel(0, "NIO");
      elements.liqDeductionsTotal.textContent = moneyLabel(0, "NIO");
      elements.liqNetTotal.textContent = moneyLabel(0, "NIO");
      elements.liquidationEmployeeName.textContent = "Sin colaborador";
      elements.liquidationEmployeeMeta.textContent =
        "La ficha del retiro se mostrara aqui luego de revisar o abrir una liquidacion guardada.";
      elements.liquidationFacts.innerHTML = "";
      elements.liquidationNotes.innerHTML = "<li>No hay observaciones todavia.</li>";
      elements.liquidationLinesBody.innerHTML = `
        <tr>
          <td colspan="5">No hay detalle para mostrar.</td>
        </tr>
      `;
      elements.processLiquidationButton.disabled = true;
      elements.liquidationReportButton.disabled = true;
      elements.liquidationExcelButton.disabled = true;
      elements.recommendationLetterButton.disabled = true;
      return;
    }

    elements.liquidationPreviewTitle.textContent = payload.persisted
      ? `Liquidacion #${payload.idLiquidacion}`
      : `Revision previa | ${payload.header.codigoEmpleado}`;
    elements.liquidationPreviewMeta.textContent = `${payload.cause.label} | baja ${formatDate(payload.header.fechaBaja)} | ${payload.header.nombreTipoContrato}`;
    elements.liquidationPreviewNet.textContent = `Neto estimado ${moneyLabel(payload.totals.netoLiquidacion, payload.header.moneda)}.`;

    elements.liquidationDetailTitle.textContent = payload.persisted
      ? `Liquidacion #${payload.idLiquidacion}`
      : `Previsualizacion ${payload.header.codigoEmpleado}`;
    elements.liquidationDetailMeta.textContent = `${payload.header.nombreEmpleado} | ${payload.header.departamento} | ${payload.cause.reference}`;
    elements.liqTaxableTotal.textContent = moneyLabel(payload.taxableSection.taxableSubtotal, payload.header.moneda);
    elements.liqNonTaxableTotal.textContent = moneyLabel(payload.nonTaxableSection.nonTaxableSubtotal, payload.header.moneda);
    elements.liqDeductionsTotal.textContent = moneyLabel(payload.deductions.totalDeductions, payload.header.moneda);
    elements.liqNetTotal.textContent = moneyLabel(payload.totals.netoLiquidacion, payload.header.moneda);

    elements.liquidationEmployeeName.textContent = `${payload.header.nombreEmpleado}`;
    elements.liquidationEmployeeMeta.textContent = `${payload.header.codigoEmpleado} | ${payload.header.cargo || "Sin cargo"} | ${payload.header.nombreTipoContrato}`;

    const facts = [
      ["Fecha ingreso", formatDate(payload.header.fechaIngreso)],
      ["Fecha baja", formatDate(payload.header.fechaBaja)],
      ["Tiempo laborado", payload.header.tiempoLaborado],
      ["Salario mensual", moneyLabel(payload.header.salarioMensual, payload.header.moneda)],
      ["Salario diario", moneyLabel(payload.header.salarioDiario, payload.header.moneda)],
      ["INSS", payload.header.inss || "Sin INSS"],
      ["Cedula", payload.header.cedula || "Sin cedula"],
      ["Departamento", payload.header.departamento || "Sin departamento"],
      ["Motivo", payload.header.motivoRetiro || payload.cause.label],
      ["Neto", moneyLabel(payload.totals.netoLiquidacion, payload.header.moneda)],
    ];

    elements.liquidationFacts.innerHTML = facts
      .map(
        ([label, value]) => `
          <article class="detail-item">
            <span>${label}</span>
            <strong>${value}</strong>
          </article>
        `,
      )
      .join("");

    elements.liquidationNotes.innerHTML = emptyArray(payload.notes).length
      ? payload.notes.map((note) => `<li>${note}</li>`).join("")
      : "<li>Sin observaciones.</li>";

    elements.liquidationLinesBody.innerHTML = emptyArray(payload.lines).length
      ? payload.lines
          .map(
            (line) => `
              <tr>
                <td>${line.groupLabel}</td>
                <td>${line.conceptName}</td>
                <td>${line.reference || "Sin referencia"}</td>
                <td class="number">${Number(line.days || 0) > 0 ? formatAmount(line.days) : ""}</td>
                <td class="number">${moneyLabel(line.amount || 0, payload.header.moneda)}</td>
              </tr>
            `,
          )
          .join("")
      : `
          <tr>
            <td colspan="5">No hay detalle para mostrar.</td>
          </tr>
        `;

    elements.processLiquidationButton.disabled = Boolean(payload.persisted);
    elements.liquidationReportButton.disabled = !payload.persisted;
    elements.liquidationExcelButton.disabled = !payload.persisted;
    elements.recommendationLetterButton.disabled = !payload.persisted;
  };

  const renderContext = () => {
    renderMetrics();
    renderCycleStrip();
    renderOverview();
    renderConfig();
    renderConfigRules();
    renderRateTables();
    renderPeriods();
    renderSelectionSummary();
    renderLiquidationForm();
    renderLiquidationHistory();
  };

  const buildLiquidationPayload = () => ({
    idEmpleado: safeNumber(elements.liquidationEmployee?.value),
    fechaLiquidacion: elements.liquidationDate?.value || "",
    fechaBaja: elements.liquidationTerminationDate?.value || "",
    causalCodigo: elements.liquidationCause?.value || "RENUNCIA_ART44",
    motivoLiquidacion: elements.liquidationReason?.value?.trim() || "",
    diasSalarioPendiente:
      elements.pendingSalaryDays?.value === "" || elements.pendingSalaryDays?.value == null
        ? null
        : safeNumber(elements.pendingSalaryDays.value),
  });

  const clearLiquidationForm = () => {
    elements.liquidationForm?.reset();
    elements.liquidationDate.value = todayInputValue();
    elements.liquidationTerminationDate.value = elements.liquidationDate.value;
    if (elements.liquidationCause?.options.length) {
      elements.liquidationCause.value = elements.liquidationCause.options[0].value;
    }
    state.liquidationPreview = null;
    renderLiquidationPanel();
  };

  const loadLiquidationDetail = async (idLiquidacion) => {
    if (!idLiquidacion) {
      state.liquidationDetail = null;
      renderLiquidationPanel();
      return;
    }

    const response = await sessionApi.request(`/Nomina/ObtenerLiquidacion?idLiquidacion=${idLiquidacion}`);
    state.liquidationDetail = response.data;
    renderLiquidationPanel();
  };

  const reviewLiquidation = async (event) => {
    event.preventDefault();
    const payload = buildLiquidationPayload();

    setButtonBusy(elements.reviewLiquidationButton, true, "Revisando...");
    try {
      const response = await sessionApi.request("/Nomina/PrevisualizarLiquidacion", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      state.liquidationPreview = response.data;
      state.selectedLiquidationId = null;
      state.liquidationDetail = null;
      setWorkspace("liquidaciones");
      renderLiquidationHistory();
      renderLiquidationPanel();
      showToast(response.message || "Liquidacion revisada.");
    } catch (error) {
      showToast(error.message || "No se pudo revisar la liquidacion.", "danger");
    } finally {
      setButtonBusy(elements.reviewLiquidationButton, false, "Revisando...");
    }
  };

  const generateLiquidation = async () => {
    const payload = buildLiquidationPayload();
    if (!state.liquidationPreview || state.liquidationPreview.persisted) {
      showToast("Primero revisa una liquidacion nueva.", "warning");
      return;
    }

    closeLiquidationConfirmation();
    setButtonBusy(elements.processLiquidationButton, true, "Procesando...");
    try {
      const response = await sessionApi.request("/Nomina/GenerarLiquidacion", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      state.liquidationPreview = null;
      await loadContext({
        periodId: state.selectedPeriodId,
        payrollId: state.selectedPayrollId,
        liquidationId: response.data?.idLiquidacion || null,
      });
      showToast(response.message || "Liquidacion generada.");
    } catch (error) {
      showToast(error.message || "No se pudo generar la liquidacion.", "danger");
    } finally {
      setButtonBusy(elements.processLiquidationButton, false, "Procesando...");
    }
  };

  const openLiquidationReport = () => {
    const payload = currentLiquidationView();
    if (!payload?.persisted || !payload?.idLiquidacion) {
      showToast("La impresion se habilita despues de generar la liquidacion.", "warning");
      return;
    }

    openUrlWithSession(`/Nomina/LiquidacionHtml?idLiquidacion=${payload.idLiquidacion}`);
  };

  const openLiquidationExcel = () => {
    const payload = currentLiquidationView();
    if (!payload?.persisted || !payload?.idLiquidacion) {
      showToast("El Excel se habilita despues de generar la liquidacion.", "warning");
      return;
    }

    openUrlWithSession(`/Nomina/LiquidacionExcel?idLiquidacion=${payload.idLiquidacion}`);
  };

  const pickSelections = (options = {}) => {
    const periods = emptyArray(state.context?.periods);
    const payrolls = emptyArray(state.context?.payrolls);
    const liquidations = emptyArray(state.context?.liquidations);

    if (options.periodId) {
      state.selectedPeriodId = Number(options.periodId);
    } else if (!periods.some((item) => Number(item.id) === Number(state.selectedPeriodId))) {
      state.selectedPeriodId = periods[0]?.id || null;
    }

    if (options.payrollId) {
      state.selectedPayrollId = Number(options.payrollId);
    } else {
      const linkedPayroll =
        payrolls.find((item) => Number(item.periodId) === Number(state.selectedPeriodId)) || null;
      state.selectedPayrollId = linkedPayroll?.id || null;
    }

    if (options.liquidationId) {
      state.selectedLiquidationId = Number(options.liquidationId);
    } else if (!liquidations.some((item) => Number(item.id) === Number(state.selectedLiquidationId))) {
      state.selectedLiquidationId = liquidations[0]?.id || null;
    }
  };

  const loadPayrollDetail = async (idNomina, preserveSelection = true) => {
    if (!idNomina) {
      state.payrollDetail = null;
      state.selectedDetailId = null;
      renderPayrollPanel();
      return;
    }

    const response = await sessionApi.request(`/Nomina/ObtenerNomina?idNomina=${idNomina}`);
    state.payrollDetail = response.data;

    const details = emptyArray(state.payrollDetail?.details);
    if (!preserveSelection || !details.some((item) => Number(item.idNominaDetalle) === Number(state.selectedDetailId))) {
      state.selectedDetailId = details[0]?.idNominaDetalle || null;
    }

    renderPayrollPanel();
  };

  const loadContext = async (options = {}) => {
    setButtonBusy(elements.refreshContextButton, true, "Actualizando...");

    try {
      const response = await sessionApi.request("/Nomina/Contexto");
      state.context = response.data;
      pickSelections(options);
      renderContext();

      if (state.selectedPayrollId) {
        await loadPayrollDetail(state.selectedPayrollId, true);
      } else {
        state.payrollDetail = null;
        state.selectedDetailId = null;
        renderPayrollPanel();
      }

      if (state.selectedLiquidationId) {
        await loadLiquidationDetail(state.selectedLiquidationId);
      } else {
        state.liquidationDetail = null;
        renderLiquidationPanel();
      }
    } finally {
      setButtonBusy(elements.refreshContextButton, false, "Actualizando...");
    }
  };

  const saveConfig = async (event) => {
    event.preventDefault();

    const payload = buildConfigPayload();

    if (!hasConfigChanges(payload)) {
      showToast("No hay cambios por guardar en la configuracion.", "warning");
      return;
    }

    const confirmed = window.confirm(
      [
        "Detectamos cambios en la configuracion de nomina.",
        "",
        "Estas seguro de guardar estos cambios?",
      ].join("\n"),
    );

    if (!confirmed) {
      showToast("Guardado cancelado.", "warning");
      return;
    }

    setButtonBusy(elements.saveConfigButton, true, "Guardando...");
    try {
      const response = await sessionApi.request("/Nomina/GuardarConfiguracionEmpresa", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      showToast(response.message || "Configuracion guardada.");
      await loadContext({ periodId: state.selectedPeriodId, payrollId: state.selectedPayrollId });
    } catch (error) {
      showToast(error.message || "No se pudo guardar la configuracion.", "danger");
    } finally {
      setButtonBusy(elements.saveConfigButton, false, "Guardando...");
    }
  };

  const clearPeriodForm = () => {
    elements.periodForm.reset();
    elements.periodType.value = "MENSUAL";
  };

  const syncPeriodPayDate = () => {
    elements.periodPayDate.value = elements.periodEndDate.value || "";
  };

  const openPeriod = async (event) => {
    event.preventDefault();

    if (!elements.periodCode.value.trim()) {
      showToast("Ingresa el codigo del periodo.", "warning");
      return;
    }

    const payload = {
      codigoPeriodo: elements.periodCode.value.trim(),
      fechaDesde: elements.periodStartDate.value,
      fechaHasta: elements.periodEndDate.value,
      fechaPago: elements.periodPayDate.value,
      tipoPeriodo: elements.periodType.value,
      observacion: elements.periodObservation.value.trim(),
      fechaCorteHoraExtra: elements.overtimeCutoffDate.value || null,
    };

    setButtonBusy(elements.openPeriodButton, true, "Abriendo...");
    try {
      const response = await sessionApi.request("/Nomina/AbrirPeriodo", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      showToast(response.message || "Periodo abierto.");
      clearPeriodForm();
      setWorkspace("periodos");
      await loadContext({ periodId: response.data?.idPeriodoNomina || null });
    } catch (error) {
      showToast(error.message || "No se pudo abrir el periodo.", "danger");
    } finally {
      setButtonBusy(elements.openPeriodButton, false, "Abriendo...");
    }
  };

  const generatePayroll = async () => {
    if (!state.selectedPeriodId) {
      showToast("Selecciona un periodo.", "warning");
      return;
    }

    closeGenerateConfirmation();
    setButtonBusy(elements.generatePayrollButton, true, "Procesando...");
    try {
      const response = await sessionApi.request("/Nomina/Generar", {
        method: "POST",
        body: JSON.stringify({
          idPeriodoNomina: Number(state.selectedPeriodId),
        }),
      });
      showToast(response.message || "Nomina generada.");
      setWorkspace("procesamiento");
      await loadContext({
        periodId: state.selectedPeriodId,
        payrollId: response.data?.idNomina || null,
      });
    } catch (error) {
      showToast(error.message || "No se pudo generar la nomina.", "danger");
    } finally {
      setButtonBusy(elements.generatePayrollButton, false, "Procesando...");
    }
  };

  const closePayroll = async () => {
    if (!state.selectedPayrollId) {
      showToast("Selecciona una nomina generada.", "warning");
      return;
    }

    setButtonBusy(elements.closePayrollButton, true, "Cerrando...");
    try {
      const response = await sessionApi.request("/Nomina/Cerrar", {
        method: "POST",
        body: JSON.stringify({
          idNomina: Number(state.selectedPayrollId),
        }),
      });
      showToast(response.message || "Nomina cerrada.");
      await loadContext({
        periodId: state.selectedPeriodId,
        payrollId: state.selectedPayrollId,
      });
    } catch (error) {
      showToast(error.message || "No se pudo cerrar la nomina.", "danger");
    } finally {
      setButtonBusy(elements.closePayrollButton, false, "Cerrando...");
    }
  };

  const openGeneralReport = () => {
    if (!state.selectedPayrollId) {
      showToast("Selecciona una nomina generada.", "warning");
      return;
    }

    openUrlWithSession(`/Nomina/ReporteGeneralHtml?idNomina=${state.selectedPayrollId}`);
  };

  const openGeneralReportExcel = () => {
    if (!state.selectedPayrollId) {
      showToast("Selecciona una nomina generada.", "warning");
      return;
    }

    openUrlWithSession(`/Nomina/ReporteGeneralExcel?idNomina=${state.selectedPayrollId}`);
  };

  const openPayslip = () => {
    if (!state.selectedDetailId) {
      showToast("Selecciona un colaborador dentro de la nomina.", "warning");
      return;
    }

    openUrlWithSession(`/Nomina/EsquelaHtml?idNominaDetalle=${state.selectedDetailId}`);
  };

  const openRecommendationLetter = () => {
    const payload = currentLiquidationView();
    if (!payload?.persisted || !payload?.idLiquidacion) {
      showToast("Primero procesa la liquidacion para emitir la carta.", "warning");
      return;
    }

    openUrlWithSession(`/Nomina/CartaRecomendacionHtml?idLiquidacion=${payload.idLiquidacion}`);
  };

  const bindEvents = () => {
    elements.workspaceButtons.forEach((button) => {
      button.addEventListener("click", () => {
        setWorkspace(button.dataset.workspace || "resumen");
      });
    });

    elements.backToDashboard?.addEventListener("click", () => {
      window.location.href = "/App/Dashboard";
    });

    elements.refreshContextButton?.addEventListener("click", () => {
      loadContext({ periodId: state.selectedPeriodId, payrollId: state.selectedPayrollId });
    });

    elements.logoutButton?.addEventListener("click", async () => {
      elements.logoutButton.disabled = true;
      try {
        await sessionApi.logout();
      } finally {
        window.location.href = "/App/Login";
      }
    });

    elements.configForm?.addEventListener("submit", saveConfig);
    elements.periodForm?.addEventListener("submit", openPeriod);
    elements.clearPeriodButton?.addEventListener("click", clearPeriodForm);
    elements.generatePayrollButton?.addEventListener("click", openGenerateConfirmation);
    elements.closePayrollConfirmButton?.addEventListener("click", closeGenerateConfirmation);
    elements.cancelGeneratePayrollButton?.addEventListener("click", closeGenerateConfirmation);
    elements.confirmGeneratePayrollButton?.addEventListener("click", generatePayroll);
    elements.closePayrollButton?.addEventListener("click", closePayroll);
    elements.generalReportButton?.addEventListener("click", openGeneralReport);
    elements.generalReportExcelButton?.addEventListener("click", openGeneralReportExcel);
    elements.generalReportButtonAlt?.addEventListener("click", openGeneralReport);
    elements.generalReportExcelButtonAlt?.addEventListener("click", openGeneralReportExcel);
    elements.employeePayslipButton?.addEventListener("click", openPayslip);
    elements.recommendationLetterButton?.addEventListener("click", openRecommendationLetter);
    elements.liquidationForm?.addEventListener("submit", reviewLiquidation);
    elements.clearLiquidationButton?.addEventListener("click", clearLiquidationForm);
    elements.processLiquidationButton?.addEventListener("click", openLiquidationConfirmation);
    elements.liquidationReportButton?.addEventListener("click", openLiquidationReport);
    elements.liquidationExcelButton?.addEventListener("click", openLiquidationExcel);
    elements.closeLiquidationConfirmButton?.addEventListener("click", closeLiquidationConfirmation);
    elements.cancelLiquidationConfirmButton?.addEventListener("click", closeLiquidationConfirmation);
    elements.confirmGenerateLiquidationButton?.addEventListener("click", generateLiquidation);
    elements.periodEndDate?.addEventListener("change", syncPeriodPayDate);
    elements.periodEndDate?.addEventListener("input", syncPeriodPayDate);
    elements.liquidationDate?.addEventListener("change", () => {
      if (!elements.liquidationTerminationDate?.value) {
        elements.liquidationTerminationDate.value = elements.liquidationDate.value;
      }
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && !elements.payrollConfirmModal?.hidden) {
        closeGenerateConfirmation();
      }

      if (event.key === "Escape" && !elements.liquidationConfirmModal?.hidden) {
        closeLiquidationConfirmation();
      }
    });

    elements.periodsTableBody?.addEventListener("click", async (event) => {
      const row = event.target.closest("[data-period-id]");
      if (!row) {
        return;
      }

      state.selectedPeriodId = Number(row.dataset.periodId || 0);
      state.selectedPayrollId = Number(row.dataset.payrollId || 0) || null;
      renderContext();

      if (state.selectedPayrollId) {
        await loadPayrollDetail(state.selectedPayrollId, false);
      } else {
        state.payrollDetail = null;
        state.selectedDetailId = null;
        renderPayrollPanel();
      }
    });

    elements.detailsTableBody?.addEventListener("click", (event) => {
      const row = event.target.closest("[data-detail-id]");
      if (!row) {
        return;
      }

      state.selectedDetailId = Number(row.dataset.detailId || 0);
      renderDetailsTable();
      renderSelectedEmployee();
      renderReports();
    });

    elements.liquidationHistoryBody?.addEventListener("click", async (event) => {
      const row = event.target.closest("[data-liquidation-id]");
      if (!row) {
        return;
      }

      state.selectedLiquidationId = Number(row.dataset.liquidationId || 0) || null;
      state.liquidationPreview = null;
      renderLiquidationHistory();
      await loadLiquidationDetail(state.selectedLiquidationId);
    });

    [elements.liquidationEmployee, elements.liquidationDate, elements.liquidationTerminationDate, elements.liquidationCause, elements.pendingSalaryDays, elements.liquidationReason]
      .filter(Boolean)
      .forEach((element) => {
        element.addEventListener("input", () => {
          if (state.liquidationPreview && !state.liquidationPreview.persisted) {
            state.liquidationPreview = null;
            renderLiquidationPanel();
          }
        });
        element.addEventListener("change", () => {
          if (state.liquidationPreview && !state.liquidationPreview.persisted) {
            state.liquidationPreview = null;
            renderLiquidationPanel();
          }
        });
      });
  };

  const boot = async () => {
    const session = currentSession();
    if (!session) {
      window.location.href = "/App/Login";
      return;
    }

    elements.sessionUser.textContent = session.displayName || session.user || "Usuario";
    elements.sessionMeta.textContent = `${session.rolesLabel || "Sin rol"} | acceso ${sessionApi.formatDateTime(session.loginAt)}`;

    bindEvents();
    setWorkspace(state.activeWorkspace);

    try {
      await loadContext();
    } catch (error) {
      showToast(error.message || "No se pudo cargar el modulo de nomina.", "danger");
    }
  };

  boot();
})();
