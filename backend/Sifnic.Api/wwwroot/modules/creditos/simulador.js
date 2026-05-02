(() => {
  const sessionApi = window.SifnicSession;
  const $ = (id) => document.getElementById(id);
  const money = (value, currency = "") =>
    `${currency ? `${currency} ` : ""}${new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value || 0))}`;
  const percent = (value) => `${new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(Number(value || 0))}%`;
  const date = (value) => {
    if (!value) return "";
    try {
      return new Intl.DateTimeFormat("es-NI", { day: "2-digit", month: "2-digit", year: "numeric", timeZone: "America/Managua" }).format(new Date(value));
    } catch {
      return String(value).slice(0, 10);
    }
  };

  const today = () => {
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    return now.toISOString().slice(0, 10);
  };

  const nodes = {
    backToDashboard: $("backToDashboard"),
    closeSession: $("closeSession"),
    themeToggle: $("themeToggle"),
    themeToggleLabel: $("themeToggleLabel"),
    sessionUser: $("sessionUser"),
    sessionMeta: $("sessionMeta"),
    calculateButton: $("calculateButton"),
    product: $("product"),
    currency: $("currency"),
    amount: $("amount"),
    startDate: $("startDate"),
    termMonths: $("termMonths"),
    annualRate: $("annualRate"),
    commissionRate: $("commissionRate"),
    commissionMode: $("commissionMode"),
    otherCharges: $("otherCharges"),
    slidingRate: $("slidingRate"),
    moraRate: $("moraRate"),
    frequency: $("frequency"),
    monthlyIncome: $("monthlyIncome"),
    spouseIncome: $("spouseIncome"),
    otherIncome: $("otherIncome"),
    monthlyExpenses: $("monthlyExpenses"),
    paymentCapacity: $("paymentCapacity"),
    externalInstallment: $("externalInstallment"),
    internalInstallment: $("internalInstallment"),
    healthyInstallment: $("healthyInstallment"),
    newInstallment: $("newInstallment"),
    debtStatus: $("debtStatus"),
    simulatorExecutive: $("simulatorExecutive"),
    resultGrid: $("resultGrid"),
    kpiStrip: $("kpiStrip"),
    resultAdvice: $("resultAdvice"),
    planBody: $("planBody"),
  };
  const state = {
    products: [],
  };

  const number = (node) => Number.parseFloat(String(node?.value || "0")) || 0;
  const integer = (node) => Number.parseInt(String(node?.value || "0"), 10) || 0;
  const detailItem = (label, value) => `<article class="detail-item"><span>${label}</span><strong>${value}</strong></article>`;
  const kpi = (label, value, tone = "") => `<article class="simulator-kpi ${tone}"><span>${label}</span><strong>${value}</strong></article>`;
  const productLabel = (product) => `${product.name || product.code} (${percent(product.annualRate)} anual / ${percent(product.commissionRate)} comision)`;

  const setProductOptions = () => {
    nodes.product.innerHTML = state.products
      .map((product) => `<option value="${product.code}">${productLabel(product)}</option>`)
      .join("");
  };

  const selectedProduct = () =>
    state.products.find((product) => product.code === nodes.product.value) || state.products[0] || null;

  const applySelectedProduct = () => {
    const product = selectedProduct();
    if (!product) return;
    nodes.product.value = product.code;
    nodes.currency.value = product.currency || "NIO";
    nodes.annualRate.value = Number(product.annualRate || 0).toFixed(6);
    nodes.commissionRate.value = Number(product.commissionRate || 0).toFixed(6);
    nodes.slidingRate.value = Number(product.slidingRate || 0).toFixed(6);
    nodes.moraRate.value = Number(product.moraRate || 0).toFixed(6);
    nodes.frequency.value = product.frequency || "MENSUAL";
    if (product.minTermMonths) nodes.termMonths.min = String(product.minTermMonths);
    if (product.maxTermMonths) nodes.termMonths.max = String(product.maxTermMonths);
    if (Number(nodes.termMonths.value || 0) < Number(product.minTermMonths || 1)) {
      nodes.termMonths.value = String(product.minTermMonths || 1);
    }
  };

  const loadProducts = async () => {
    const response = await sessionApi.request("/SolicitudesCredito/ProductosCredito");
    state.products = Array.isArray(response.data) ? response.data : [];
    setProductOptions();
    applySelectedProduct();
  };

  const classifyDebt = (ratio, availableAfterDebt) => {
    if (availableAfterDebt < 0) {
      return { label: "SIN CAPACIDAD", className: "is-danger", advice: "Las deudas actuales consumen la capacidad. No conviene formalizar sin cancelar o reestructurar obligaciones." };
    }

    if (ratio <= 35) return { label: "SANO", className: "is-done", advice: "La cuota proyectada se mantiene en rango saludable para precalificar." };
    if (ratio <= 50) return { label: "OBSERVAR", className: "is-pending", advice: "Revisar plazo, monto, deudas vigentes y soporte de ingresos antes de formalizar." };
    return { label: "ALTO", className: "is-danger", advice: "No conviene avanzar sin reducir cuota, ampliar plazo o documentar excepcion autorizada." };
  };

  const renderExecutive = (payload, summary, status, capacityInfo, ratio, installment, availableAfterDebt) => {
    if (!nodes.simulatorExecutive) return;
    const width = Math.max(0, Math.min(100, ratio));
    const riskColor = ratio <= 35 ? "var(--fin-ok)" : ratio <= 50 ? "var(--fin-warn)" : "var(--fin-danger)";
    const conclusion =
      status.className === "is-done"
        ? "Precalificacion favorable: la cuota cabe en capacidad sana y deja margen operativo."
        : status.className === "is-pending"
          ? "Requiere revision: ajustar monto, plazo o validar soporte de ingresos antes de avanzar."
          : "Alerta de politica: no conviene formalizar sin excepcion aprobada o redisenar la cuota.";
    nodes.simulatorExecutive.innerHTML = `
      <article class="simulator-executive-card">
        <span>Conclusion ejecutiva</span>
        <strong>${money(installment, payload.currency)} cuota estimada</strong>
        <p>${conclusion}</p>
      </article>
      <article class="risk-meter-card">
        <span>Endeudamiento total</span>
        <strong>${percent(ratio)} usado / ${money(availableAfterDebt, payload.currency)} disponible despues</strong>
        <div class="risk-meter" style="--risk-width:${width}%; --risk-color:${riskColor}"><i></i></div>
        <p>${money(summary.totalToPay, payload.currency)} total a pagar · ${money(summary.netDisbursed || payload.amount, payload.currency)} monto en mano</p>
      </article>`;
  };

  const buildPayload = () => ({
    product: nodes.product.value,
    currency: nodes.currency.value,
    amount: number(nodes.amount),
    termMonths: integer(nodes.termMonths),
    annualRate: number(nodes.annualRate),
    commissionRate: number(nodes.commissionRate),
    commissionMode: nodes.commissionMode.value,
    otherCharges: number(nodes.otherCharges),
    slidingRate: number(nodes.slidingRate),
    moraRate: number(nodes.moraRate),
    frequency: nodes.frequency.value,
    startDate: nodes.startDate.value || today(),
  });

  const hasMinimumInput = () => number(nodes.amount) > 0 && integer(nodes.termMonths) > 0;

  const renderEmptySimulation = () => {
    nodes.debtStatus.textContent = "Pendiente";
    nodes.debtStatus.className = "status-pill";
    nodes.simulatorExecutive.innerHTML = `
      <article class="simulator-executive-card">
        <span>Conclusion ejecutiva</span>
        <strong>Sin simulacion activa</strong>
        <p>Ingresa monto, plazo y capacidad para calcular una conclusion de credito.</p>
      </article>`;
    nodes.resultGrid.innerHTML = "";
    nodes.kpiStrip.innerHTML = "";
    nodes.resultAdvice.innerHTML = `
      <article class="workflow-card">
        <div class="checklist-strip">
          <span>Datos de entrada</span>
          <span>Capacidad</span>
          <span>Resultado</span>
          <span>Plan de pago</span>
        </div>
      </article>`;
    nodes.planBody.innerHTML = "";
  };

  const clearSimulationForm = () => {
    [
      nodes.amount,
      nodes.otherCharges,
      nodes.monthlyIncome,
      nodes.spouseIncome,
      nodes.otherIncome,
      nodes.monthlyExpenses,
      nodes.externalInstallment,
      nodes.internalInstallment,
      nodes.paymentCapacity,
      nodes.healthyInstallment,
      nodes.newInstallment,
    ]
      .filter(Boolean)
      .forEach((node) => {
        node.value = "";
      });

    nodes.termMonths.value = "";
    nodes.startDate.value = today();
    renderEmptySimulation();
  };

  const calculateCapacity = () => {
    const grossIncome = number(nodes.monthlyIncome) + number(nodes.spouseIncome) + number(nodes.otherIncome);
    const expenses = number(nodes.monthlyExpenses);
    const capacity = Math.max(0, grossIncome - expenses);
    const committed = number(nodes.externalInstallment) + number(nodes.internalInstallment);
    const healthyInstallment = Math.max(0, capacity * 0.35 - committed);
    nodes.paymentCapacity.value = capacity.toFixed(2);
    nodes.healthyInstallment.value = healthyInstallment.toFixed(2);
    return { grossIncome, expenses, capacity, committed, healthyInstallment };
  };

  const renderPlan = (rows, currency, summary) => {
    nodes.planBody.innerHTML = `
      <article class="related-card">
        <strong>Plan de pago estilo FIOL</strong>
        <div class="badge-row">
          <span class="badge">${rows.length} cuotas</span>
          <span class="badge">Dias reales / base 360</span>
          <span class="badge is-gold">Total ${money(summary?.totalToPay || 0, currency)}</span>
        </div>
      </article>
      <div class="mini-table simulator-plan-table">
        <table>
          <thead><tr><th>Cuota</th><th>Fecha</th><th>Dias</th><th>Capital</th><th>Interes</th><th>Comision</th><th>Desliz.</th><th>Total</th><th>Saldo</th></tr></thead>
          <tbody>${rows
            .slice(0, 120)
            .map((item) => `<tr><td>${item.number}</td><td>${date(item.dueDate)}</td><td>${item.interestDays || 0}</td><td>${money(item.capital, currency)}</td><td>${money(item.interest, currency)}</td><td>${money(item.commission, currency)}</td><td>${money(item.sliding, currency)}</td><td>${money(item.total, currency)}</td><td>${money(item.balance, currency)}</td></tr>`)
            .join("")}</tbody>
        </table>
      </div>`;
  };

  const renderWarnings = (payload, summary, status, capacityInfo, ratio, totalInstallment) => {
    const messages = [
      status.advice,
      "Formula base: capital fijo por cuota, interes diario sobre saldo + deslizamiento, base 360.",
      "La TCEA usa flujo tipo XIRR: monto neto recibido contra todas las cuotas.",
    ];

    if (payload.commissionMode === "DESCONTADA") {
      messages.push("La comision se descuenta al desembolso y afecta el monto en mano del cliente.");
    }

    if (summary.netDisbursed <= 0) {
      messages.push("El monto neto quedo en cero o negativo; revisa cargos y comision.");
    }

    if (totalInstallment > capacityInfo.healthyInstallment && capacityInfo.healthyInstallment > 0) {
      messages.push(`Para quedar en rango sano, la cuota nueva deberia acercarse a ${money(capacityInfo.healthyInstallment, payload.currency)}.`);
    }

    nodes.resultAdvice.innerHTML = `
      <article class="workflow-card">
        <div class="checklist-strip">
          <span class="${status.className}">${status.label}</span>
          <span>Endeudamiento total: ${percent(ratio)}</span>
        </div>
        <ul class="simulator-advice-list">${messages.map((item) => `<li>${item}</li>`).join("")}</ul>
      </article>`;
  };

  const calculate = async () => {
    const payload = buildPayload();
    const capacityInfo = calculateCapacity();

    if (!hasMinimumInput()) {
      renderEmptySimulation();
      return;
    }

    const response = await sessionApi.request("/SolicitudesCredito/GenerarPlan", {
      method: "POST",
      body: JSON.stringify(payload),
    });

    const summary = response.data?.summary || {};
    const rows = response.data?.paymentPlan || [];
    const installment = Number(summary.estimatedInstallment || rows[0]?.total || 0);
    const totalInstallment = installment + capacityInfo.committed;
    const ratio = capacityInfo.capacity > 0 ? (totalInstallment / capacityInfo.capacity) * 100 : 0;
    const availableAfterDebt = capacityInfo.capacity - capacityInfo.committed - installment;
    const status = classifyDebt(ratio, availableAfterDebt);

    nodes.newInstallment.value = installment.toFixed(2);
    nodes.debtStatus.textContent = status.label;
    nodes.debtStatus.className = "status-pill";
    nodes.debtStatus.classList.add(status.className);
    renderExecutive(payload, summary, status, capacityInfo, ratio, installment, availableAfterDebt);

    nodes.resultGrid.innerHTML = [
      detailItem("Tipo de credito", selectedProduct()?.name || payload.product),
      detailItem("Monto credito", money(payload.amount, payload.currency)),
      detailItem("Monto en mano", money(summary.netDisbursed || payload.amount, payload.currency)),
      detailItem("Cuota estimada", money(installment, payload.currency)),
      detailItem("Cuota promedio", money(summary.averageInstallment || installment, payload.currency)),
      detailItem("Cuota maxima sana", money(capacityInfo.healthyInstallment, payload.currency)),
      detailItem("Ingreso disponible", money(capacityInfo.capacity, payload.currency)),
      detailItem("Cuotas previas", money(capacityInfo.committed, payload.currency)),
      detailItem("Disponible despues", money(availableAfterDebt, payload.currency)),
    ].join("");

    nodes.kpiStrip.innerHTML = [
      kpi("TCEA simulada", summary.effectiveAnnualCostRate == null ? "-" : percent(summary.effectiveAnnualCostRate), "is-gold"),
      kpi("Interes total", money(summary.totalInterest, payload.currency)),
      kpi("Deslizamiento", money(summary.totalSliding, payload.currency)),
      kpi("Comision desembolso", money(summary.upfrontCommission, payload.currency)),
      kpi("Otros cargos", money(summary.otherCharges, payload.currency)),
      kpi("Total a pagar", money(summary.totalToPay, payload.currency), "is-strong"),
    ].join("");

    renderWarnings(payload, summary, status, capacityInfo, ratio, totalInstallment);
    renderPlan(rows, payload.currency, summary);
  };

  const boot = async () => {
    const session = sessionApi.getSession();
    if (!session) {
      window.location.href = "/App/Login";
      return;
    }

    nodes.startDate.value = today();
    nodes.sessionUser.textContent = session.displayName || session.user || "Usuario SIFNIC";
    nodes.sessionMeta.textContent = `${session.rolesLabel || "Sin rol"} - ${sessionApi.formatDateTime(session.loginAt)}`;
    window.SifnicTheme?.attachToggle(nodes.themeToggle, nodes.themeToggleLabel, null);
    nodes.backToDashboard.addEventListener("click", () => { window.location.href = "/App/Dashboard"; });
    nodes.closeSession.addEventListener("click", async () => {
      await sessionApi.logout();
      window.location.href = "/App/Login";
    });
    nodes.calculateButton.addEventListener("click", calculate);
    nodes.product.addEventListener("change", () => {
      applySelectedProduct();
      if (hasMinimumInput()) {
        calculate();
      }
    });

    Object.values(nodes)
      .filter((node) => node instanceof HTMLInputElement || node instanceof HTMLSelectElement)
      .forEach((node) => {
        if (node.readOnly) return;
        node.addEventListener("change", calculate);
        node.addEventListener("input", () => {
          calculateCapacity();
          window.clearTimeout(node._simulatorTimer);
          node._simulatorTimer = window.setTimeout(() => {
            if (hasMinimumInput()) {
              calculate();
            } else {
              renderEmptySimulation();
            }
          }, 350);
        });
      });

    await loadProducts();
    clearSimulationForm();
  };

  boot().catch((error) => {
    nodes.resultAdvice.innerHTML = `<article class="workflow-card"><span class="is-danger">${error.message || "No se pudo calcular."}</span></article>`;
  });
})();
