(() => {
  const sessionApi = window.SifnicSession;

  const state = {
    session: null,
    catalogs: null,
    cashSession: null,
    summary: null,
    receipts: [],
    credits: [],
    disbursements: [],
    selectedCredit: null,
    selectedDisbursement: null,
    selectedReceiptForVoid: null,
    cashMode: "open",
    searchTimer: null,
    lastPaymentPrintUrl: "",
    paymentStage: "search",
  };

  const $ = (id) => document.getElementById(id);
  const nodes = {
    backToDashboard: $("backToDashboard"),
    closeSession: $("closeSession"),
    themeToggle: $("themeToggle"),
    themeToggleLabel: $("themeToggleLabel"),
    sessionUser: $("sessionUser"),
    sessionMeta: $("sessionMeta"),
    cashSessionTitle: $("cashSessionTitle"),
    cashSessionBody: $("cashSessionBody"),
    openCashButton: $("openCashButton"),
    closeCashButton: $("closeCashButton"),
    cashReportButton: $("cashReportButton"),
    cashReportButtonMirror: $("cashReportButtonMirror"),
    metricNio: $("metricNio"),
    metricUsd: $("metricUsd"),
    metricIncome: $("metricIncome"),
    metricNioMirror: $("metricNioMirror"),
    metricUsdMirror: $("metricUsdMirror"),
    metricIncomeMirror: $("metricIncomeMirror"),
    cashSummaryBody: $("cashSummaryBody"),
    startCashCountButton: $("startCashCountButton"),
    disbursementCounter: $("disbursementCounter"),
    disbursementSearchInput: $("disbursementSearchInput"),
    refreshDisbursementsButton: $("refreshDisbursementsButton"),
    disbursementBody: $("disbursementBody"),
    creditSearchInput: $("creditSearchInput"),
    searchCreditButton: $("searchCreditButton"),
    creditResults: $("creditResults"),
    selectedCreditStatus: $("selectedCreditStatus"),
    cashStepper: $("cashStepper"),
    creditFocusTitle: $("creditFocusTitle"),
    creditFocusMeta: $("creditFocusMeta"),
    creditBalanceGrid: $("creditBalanceGrid"),
    creditPlanPreview: $("creditPlanPreview"),
    debtRibbon: $("debtRibbon"),
    paymentForm: $("paymentForm"),
    paymentAmount: $("paymentAmount"),
    paymentCurrency: $("paymentCurrency"),
    paymentMethod: $("paymentMethod"),
    paymentExchangeRate: $("paymentExchangeRate"),
    paymentAppliedAmount: $("paymentAppliedAmount"),
    paymentCurrencyHint: $("paymentCurrencyHint"),
    paymentAllocationPreview: $("paymentAllocationPreview"),
    paymentResultStrip: $("paymentResultStrip"),
    paymentAutoNote: $("paymentAutoNote"),
    payerDifferentToggle: $("payerDifferentToggle"),
    payerDrawer: $("payerDrawer"),
    payerName: $("payerName"),
    payerIdentification: $("payerIdentification"),
    payerPhone: $("payerPhone"),
    payerLookupHint: $("payerLookupHint"),
    manualReceipt: $("manualReceipt"),
    paymentObservation: $("paymentObservation"),
    paymentAuditStrip: $("paymentAuditStrip"),
    paymentMessage: $("paymentMessage"),
    paymentStatusPill: $("paymentStatusPill"),
    paymentConfirmList: $("paymentConfirmList"),
    receiptPreview: $("receiptPreview"),
    newPaymentButton: $("newPaymentButton"),
    reprintLastPaymentButton: $("reprintLastPaymentButton"),
    paymentSubmitButtons: [...document.querySelectorAll("[data-payment-submit]")],
    searchInput: $("searchInput"),
    dateFrom: $("dateFrom"),
    dateTo: $("dateTo"),
    refreshButton: $("refreshButton"),
    clearButton: $("clearButton"),
    tableCounter: $("tableCounter"),
    tableBody: $("tableBody"),
    cashModal: $("cashModal"),
    cashModalKicker: $("cashModalKicker"),
    cashModalTitle: $("cashModalTitle"),
    cashModalClose: $("cashModalClose"),
    cashForm: $("cashForm"),
    cashBranch: $("cashBranch"),
    cashBranchHint: $("cashBranchHint"),
    cashNioLabel: $("cashNioLabel"),
    cashUsdLabel: $("cashUsdLabel"),
    cashTheorySummary: $("cashTheorySummary"),
    cashCountSummary: $("cashCountSummary"),
    openingNio: $("openingNio"),
    openingUsd: $("openingUsd"),
    cashObservation: $("cashObservation"),
    breakdownNio: $("breakdownNio"),
    breakdownUsd: $("breakdownUsd"),
    cashMessage: $("cashMessage"),
    cashCancel: $("cashCancel"),
    cashSubmitButton: $("cashSubmitButton"),
    voidModal: $("voidModal"),
    voidModalClose: $("voidModalClose"),
    voidForm: $("voidForm"),
    voidVoucherSummary: $("voidVoucherSummary"),
    voidReason: $("voidReason"),
    voidMessage: $("voidMessage"),
    voidCancel: $("voidCancel"),
    voidSubmitButton: $("voidSubmitButton"),
    disbursementModal: $("disbursementModal"),
    disbursementModalClose: $("disbursementModalClose"),
    disbursementForm: $("disbursementForm"),
    disbursementSummaryTitle: $("disbursementSummaryTitle"),
    disbursementSummary: $("disbursementSummary"),
    disbursementAmount: $("disbursementAmount"),
    disbursementCurrency: $("disbursementCurrency"),
    disbursementMethod: $("disbursementMethod"),
    disbursementExchangeRate: $("disbursementExchangeRate"),
    disbursementObservation: $("disbursementObservation"),
    disbursementMessage: $("disbursementMessage"),
    disbursementCancel: $("disbursementCancel"),
    disbursementSubmitButton: $("disbursementSubmitButton"),
  };

  const money = (value) =>
    new Intl.NumberFormat("es-NI", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value || 0));
  const signedMoney = (value) => {
    const number = Number(value || 0);
    return `${number > 0 ? "+" : ""}${money(number)}`;
  };
  const date = (value) => {
    if (!value) return "-";
    try {
      return new Intl.DateTimeFormat("es-NI", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        hour12: false,
        timeZone: "America/Managua",
      }).format(new Date(value));
    } catch {
      return String(value);
    }
  };
  const shortDate = (value) => {
    if (!value) return "-";
    try {
      return new Intl.DateTimeFormat("es-NI", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        timeZone: "America/Managua",
      }).format(new Date(value));
    } catch {
      return String(value);
    }
  };
  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const setOptions = (select, items) => {
    select.innerHTML = (items || [])
      .map((item) => {
        const value = typeof item === "object" ? item.value : item;
        const label = typeof item === "object" ? item.label || item.value : item;
        return `<option value="${escapeHtml(value)}">${escapeHtml(label)}</option>`;
      })
      .join("");
  };

  const normalizeCurrency = (value, fallback = "NIO") => {
    const currency = String(value || fallback).trim().toUpperCase();
    return currency === "USD" ? "USD" : "NIO";
  };

  const getInstitutionalRateForDirection = () => {
    const creditCurrency = normalizeCurrency(state.selectedCredit?.currency || "NIO");
    const receivedCurrency = normalizeCurrency(nodes.paymentCurrency.value, creditCurrency);
    const rates = state.catalogs?.exchangeRates || {};
    if (receivedCurrency === "USD" && creditCurrency === "NIO") {
      return { value: Number(rates.buy || state.catalogs?.exchangeRate || 0), label: "Institucional compra" };
    }
    if (receivedCurrency === "NIO" && creditCurrency === "USD") {
      return { value: Number(rates.sell || state.catalogs?.exchangeRate || 0), label: "Institucional venta" };
    }
    return { value: Number(rates.buy || state.catalogs?.exchangeRate || 0), label: "Institucional" };
  };

  const syncExchangeRateForDirection = () => {
    const rate = getInstitutionalRateForDirection();
    if (rate.value > 0) {
      nodes.paymentExchangeRate.value = rate.value.toFixed(4);
    }
    if (nodes.paymentCurrencyHint) {
      nodes.paymentCurrencyHint.textContent = `${rate.label} ${money(rate.value)}`;
    }
  };

  const getExchangeRate = () => Number(nodes.paymentExchangeRate.value || getInstitutionalRateForDirection().value || 0);

  const updateCashStepper = () => {
    if (!nodes.cashStepper) return;
    const steps = ["search", "select", "capture", "validate", "confirm", "receipt"];
    const amount = Number(nodes.paymentAmount?.value || 0);
    let active = "search";
    if (state.paymentStage === "receipt") {
      active = "receipt";
    } else if (state.selectedCredit && amount > 0) {
      active = "confirm";
    } else if (state.selectedCredit) {
      active = "capture";
    } else if ((state.credits || []).length) {
      active = "select";
    }
    const activeIndex = steps.indexOf(active);
    nodes.cashStepper.querySelectorAll("[data-step]").forEach((item) => {
      const step = item.dataset.step;
      const index = steps.indexOf(step);
      item.classList.toggle("is-active", step === active);
      item.classList.toggle("is-done", index >= 0 && index < activeIndex);
      item.classList.toggle("is-disabled", index > activeIndex);
    });
    document.querySelectorAll("[data-flow-step]").forEach((block) => {
      const step = block.dataset.flowStep;
      const index = steps.indexOf(step);
      const isCurrent = step === active || (active === "receipt" && step === "confirm");
      block.classList.toggle("is-current-step", isCurrent);
      block.classList.toggle("is-complete-step", index >= 0 && index < activeIndex);
      block.classList.toggle("is-locked-step", index > activeIndex);
    });
    document.querySelector(".caja-teller-shell")?.setAttribute("data-active-step", active);
  };

  const pendingValue = (primary, fallback = 0) => Math.max(0, Number(primary ?? fallback ?? 0));

  const creditDueSnapshot = (credit) => {
    const currency = normalizeCurrency(credit?.currency || "NIO");
    const interest = pendingValue(credit?.totalInterest, credit?.nextInterest);
    const mora = pendingValue(credit?.totalMora, credit?.nextMora);
    const commission = pendingValue(credit?.totalCommission, Number(credit?.nextCommission || 0) + Number(credit?.nextSlide || 0));
    const capitalDue = pendingValue(credit?.totalCapitalDue, credit?.nextCapital);
    const capitalBalance = pendingValue(credit?.capitalBalance);
    const dueToday = pendingValue(credit?.dueTodayAmount, credit?.nextAmount);
    const overdue = pendingValue(credit?.overdueAmount);
    return {
      currency,
      interest,
      mora,
      commission,
      capitalDue,
      capitalBalance,
      dueToday: dueToday || interest + mora + commission + capitalDue,
      overdue,
      overdueDays: Number(credit?.overdueDays || 0),
      nextDate: credit?.followingDueDate || credit?.nextDueDate || credit?.nextDate,
      nextAmount: pendingValue(credit?.nextAmount),
    };
  };

  const convertToCreditCurrency = (amount, receivedCurrency, creditCurrency, exchangeRate) => {
    const received = normalizeCurrency(receivedCurrency);
    const credit = normalizeCurrency(creditCurrency);
    if (received === credit) return amount;
    if (received === "USD" && credit === "NIO") return amount * exchangeRate;
    if (received === "NIO" && credit === "USD") return exchangeRate > 0 ? amount / exchangeRate : 0;
    return amount;
  };

  const allocationRows = (credit, amount) => {
    const due = creditDueSnapshot(credit);
    let remaining = Math.max(0, Number(amount || 0));
    const apply = (value) => {
      const applied = Math.min(remaining, Math.max(0, Number(value || 0)));
      remaining -= applied;
      return applied;
    };
    const interest = apply(due.interest);
    const mora = apply(due.mora);
    const commission = apply(due.commission);
    const capitalDue = apply(due.capitalDue);
    const capitalAdvance = Math.min(remaining, Math.max(0, due.capitalBalance - capitalDue));
    remaining -= capitalAdvance;
    const capital = capitalDue + capitalAdvance;
    const rows = [
      { label: "Interes corriente", pending: due.interest, value: interest, balance: Math.max(0, due.interest - interest) },
      { label: "Mora", pending: due.mora, value: mora, balance: Math.max(0, due.mora - mora) },
      { label: "Comisiones / cargos", pending: due.commission, value: commission, balance: Math.max(0, due.commission - commission) },
      { label: "Capital", pending: due.capitalDue, value: capital, balance: Math.max(0, due.capitalBalance - capital) },
    ];
    return {
      rows,
      totalApplied: interest + mora + commission + capital,
      excess: Math.max(0, remaining),
      remainingCreditBalance: Math.max(0, due.capitalBalance - capital),
      suggestedTotal: due.dueToday,
      nextAmount: due.nextAmount,
      isPartial: Number(amount || 0) > 0 && Number(amount || 0) + 0.005 < due.dueToday,
      isExact: Math.abs(Number(amount || 0) - due.dueToday) <= 0.005 && due.dueToday > 0,
      isAdvance: Number(amount || 0) > due.dueToday + 0.005,
    };
  };

  const renderAllocationPreview = (credit, amount, currency) => {
    if (!nodes.paymentAllocationPreview) return;
    const allocation = allocationRows(credit, amount);
    nodes.paymentAllocationPreview.innerHTML = allocation.rows
      .map((row) => `
        <div class="allocation-row">
          <span>${escapeHtml(row.label)}</span>
          <strong>${escapeHtml(currency)} ${money(row.value)}</strong>
          <em>Pendiente ${escapeHtml(currency)} ${money(row.balance)}</em>
        </div>`)
      .join("");
    if (nodes.paymentResultStrip) {
      nodes.paymentResultStrip.innerHTML = `
        <article><span>Total aplicado</span><strong>${escapeHtml(currency)} ${money(allocation.totalApplied)}</strong></article>
        <article><span>Excedente / saldo a favor</span><strong>${escapeHtml(currency)} ${money(allocation.excess)}</strong></article>
        <article><span>Saldo restante</span><strong>${escapeHtml(currency)} ${money(allocation.remainingCreditBalance)}</strong></article>
        <article><span>Proxima cuota</span><strong>${allocation.nextAmount ? `${escapeHtml(currency)} ${money(allocation.nextAmount)}` : "-"}</strong></article>`;
    }
    return allocation;
  };

  const updatePaymentPreview = () => {
    if (!nodes.paymentAppliedAmount) return;
    if (!state.selectedCredit) {
      if (state.paymentStage !== "receipt") {
        state.paymentStage = state.credits.length ? "select" : "search";
      }
      nodes.paymentAppliedAmount.value = "";
      if (nodes.paymentAllocationPreview) {
        nodes.paymentAllocationPreview.innerHTML = '<div class="allocation-empty">Selecciona un credito para calcular la aplicacion.</div>';
      }
      if (nodes.paymentResultStrip) nodes.paymentResultStrip.innerHTML = "";
      if (nodes.paymentAutoNote) nodes.paymentAutoNote.textContent = "Selecciona un credito para preparar el abono.";
      if (nodes.paymentStatusPill) {
        nodes.paymentStatusPill.textContent = "Pendiente";
        nodes.paymentStatusPill.className = "counter-pill";
      }
      if (nodes.paymentConfirmList) nodes.paymentConfirmList.innerHTML = "<span>Selecciona un credito para preparar el cobro.</span>";
      updateCashStepper();
      return;
    }

    const amount = Number(nodes.paymentAmount.value || 0);
    const receivedCurrency = normalizeCurrency(nodes.paymentCurrency.value, state.selectedCredit.currency);
    const creditCurrency = normalizeCurrency(state.selectedCredit.currency);
    syncExchangeRateForDirection();
    const exchangeRate = getExchangeRate();
    if (amount <= 0) {
      state.paymentStage = "capture";
      nodes.paymentAppliedAmount.value = `${creditCurrency} 0.00`;
      renderAllocationPreview(state.selectedCredit, 0, creditCurrency);
      if (nodes.paymentAutoNote) nodes.paymentAutoNote.textContent = "Digita un monto mayor que cero.";
      if (nodes.paymentStatusPill) {
        nodes.paymentStatusPill.textContent = "Sin monto";
        nodes.paymentStatusPill.className = "counter-pill";
      }
      updateCashStepper();
      return;
    }

    if (receivedCurrency !== creditCurrency && exchangeRate <= 0) {
      state.paymentStage = "validate";
      nodes.paymentAppliedAmount.value = "TC requerido";
      renderAllocationPreview(state.selectedCredit, 0, creditCurrency);
      if (nodes.paymentAutoNote) nodes.paymentAutoNote.textContent = "Se requiere tipo de cambio para convertir la moneda recibida.";
      updateCashStepper();
      return;
    }

    state.paymentStage = "confirm";
    const applied = convertToCreditCurrency(amount, receivedCurrency, creditCurrency, exchangeRate);
    const allocation = renderAllocationPreview(state.selectedCredit, applied, creditCurrency);
    const rateLabel = getInstitutionalRateForDirection().label.toLowerCase();
    const detail = receivedCurrency === creditCurrency ? "sin conversion" : `${rateLabel} ${money(exchangeRate)}`;
    nodes.paymentAppliedAmount.value = `${creditCurrency} ${money(applied)} / ${detail}`;
    if (nodes.manualReceipt) {
      const requiresReference = String(nodes.paymentMethod.value || "EFECTIVO").toUpperCase() !== "EFECTIVO";
      nodes.manualReceipt.placeholder = requiresReference ? "Referencia obligatoria" : "Opcional en efectivo";
      nodes.manualReceipt.closest(".field")?.classList.toggle("is-required", requiresReference);
    }
    if (nodes.paymentStatusPill && allocation) {
      nodes.paymentStatusPill.className = allocation.isPartial
        ? "status-pill status-warn"
        : allocation.isAdvance
          ? "status-pill status-ok"
          : "status-pill status-ok";
      nodes.paymentStatusPill.textContent = allocation.isPartial ? "Parcial" : allocation.isAdvance ? "Adelantado" : "Exacto";
    }
    if (nodes.paymentConfirmList && allocation) {
      nodes.paymentConfirmList.innerHTML = `
        <span>Cliente: <strong>${escapeHtml(state.selectedCredit.clientName || "-")}</strong></span>
        <span>Credito: <strong>${escapeHtml(state.selectedCredit.creditNumber || "-")}</strong></span>
        <span>Monto recibido: <strong>${escapeHtml(receivedCurrency)} ${money(amount)}</strong></span>
        <span>Forma de pago: <strong>${escapeHtml(nodes.paymentMethod.value || "EFECTIVO")}</strong></span>
        <span>Total aplicado: <strong>${escapeHtml(creditCurrency)} ${money(allocation.totalApplied)}</strong></span>
        <span>Saldo restante: <strong>${escapeHtml(creditCurrency)} ${money(allocation.remainingCreditBalance)}</strong></span>`;
    }
    if (nodes.paymentAutoNote) {
      const base = receivedCurrency === creditCurrency
        ? `Se aplicara ${creditCurrency} ${money(applied)} al credito seleccionado.`
        : `Caja recibira ${receivedCurrency} ${money(amount)} y aplicara ${creditCurrency} ${money(applied)} al credito con ${detail}.`;
      const partial = allocation?.isPartial ? " Pago parcial: se cubren primero intereses, luego mora, cargos y de ultimo capital." : "";
      nodes.paymentAutoNote.textContent = `${base}${partial || " Prioridad automatica: intereses, mora, cargos y capital al final."}`;
    }
    updateCashStepper();
  };

  const normalizeIdentification = (value) =>
    String(value || "").trim().toUpperCase().replace(/[^A-Z0-9]/g, "");

  const setPayerHint = (message, kind = "info") => {
    if (!nodes.payerLookupHint) return;
    nodes.payerLookupHint.textContent = message;
    nodes.payerLookupHint.dataset.kind = kind;
  };

  const lookupPayer = async (force = false) => {
    if (nodes.payerDifferentToggle && !nodes.payerDifferentToggle.checked) return null;
    const identification = normalizeIdentification(nodes.payerIdentification.value);
    nodes.payerIdentification.value = identification;
    if (identification.length < 6) {
      if (force) setPayerHint("Cedula incompleta. Digita la cedula del abonante.", "warn");
      return null;
    }

    try {
      const payload = await sessionApi.request(`/Caja/BuscarAbonante?cedula=${encodeURIComponent(identification)}`);
      const payer = payload.data;
      if (payer?.found) {
        nodes.payerName.value = payer.name || nodes.payerName.value;
        nodes.payerPhone.value = payer.phone || nodes.payerPhone.value;
        setPayerHint(`${payer.source === "CLIENTE" ? "Cliente encontrado" : "Abonante encontrado"}: datos rellenados automaticamente.`, "ok");
        return payer;
      }

      setPayerHint("Abonante no registrado. Digita nombre completo y telefono para guardarlo en el voucher.", "warn");
      return null;
    } catch (error) {
      setPayerHint(error.message || "No se pudo validar la cedula del abonante.", "warn");
      return null;
    }
  };

  const detailItem = (label, value) => `
    <article class="detail-item">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(value ?? "-")}</strong>
    </article>`;

  const planRow = (label, currency, value) => `<tr><td>${escapeHtml(label)}</td><td>${escapeHtml(currency)} ${money(value)}</td></tr>`;

  const updateAuditStrip = () => {
    if (!nodes.paymentAuditStrip) return;
    const cash = state.cashSession;
    nodes.paymentAuditStrip.innerHTML = `
      <span>Sucursal: <strong>${escapeHtml(cash?.branch || state.catalogs?.assignedBranch?.label || "-")}</strong></span>
      <span>Caja: <strong>${escapeHtml(cash?.id ? `Caja ${cash.id}` : "-")}</strong></span>
      <span>Cajero: <strong>${escapeHtml(cash?.cashierUser || state.session?.displayName || state.session?.user || "-")}</strong></span>
      <span>Fecha: <strong>${date(new Date())}</strong></span>`;
  };

  const openModal = (node) => {
    node.hidden = false;
    node.classList.add("is-open");
  };

  const closeModal = (node) => {
    node.classList.remove("is-open");
    node.hidden = true;
  };

  const showMessage = (node, message) => {
    node.hidden = false;
    node.textContent = message;
  };

  const showCajaView = (view) => {
    document.querySelectorAll("[data-caja-view]").forEach((button) => {
      button.classList.toggle("is-active", button.dataset.cajaView === view);
    });
    document.querySelectorAll("[data-caja-panel]").forEach((panel) => {
      panel.classList.toggle("is-active", panel.dataset.cajaPanel === view);
    });
  };

  const receiptParams = () => {
    const query = new URLSearchParams();
    query.set("search", nodes.searchInput.value.trim());
    if (nodes.dateFrom.value) query.set("dateFrom", nodes.dateFrom.value);
    if (nodes.dateTo.value) query.set("dateTo", nodes.dateTo.value);
    return query.toString();
  };

  const renderCashSession = () => {
    const cash = state.cashSession;
    nodes.openCashButton.disabled = !!cash;
    nodes.closeCashButton.disabled = !cash;
    nodes.cashReportButton.disabled = !cash;
    if (nodes.cashReportButtonMirror) nodes.cashReportButtonMirror.disabled = !cash;
    if (nodes.startCashCountButton) nodes.startCashCountButton.disabled = !cash;
    nodes.paymentSubmitButtons.forEach((button) => {
      button.disabled = !cash;
    });
    if (nodes.disbursementSubmitButton) nodes.disbursementSubmitButton.disabled = !cash;

    if (!cash) {
      nodes.cashSessionTitle.textContent = "Sin apertura";
      nodes.cashSessionBody.innerHTML = [
        detailItem("Estado", "CERRADA"),
        detailItem("Cajero", state.session?.user || "-"),
        detailItem("Sucursal", state.catalogs?.assignedBranch?.label || "-"),
      ].join("");
      nodes.metricNio.textContent = "0.00";
      nodes.metricUsd.textContent = "0.00";
      nodes.metricIncome.textContent = "0.00";
      if (nodes.metricNioMirror) nodes.metricNioMirror.textContent = "0.00";
      if (nodes.metricUsdMirror) nodes.metricUsdMirror.textContent = "0.00";
      if (nodes.metricIncomeMirror) nodes.metricIncomeMirror.textContent = "0.00";
      nodes.cashSummaryBody.innerHTML = "<p>Abri caja para iniciar pagos y arqueo.</p>";
      updateAuditStrip();
      return;
    }

    nodes.cashSessionTitle.textContent = cash.status;
    nodes.cashSessionBody.innerHTML = [
      detailItem("Cajero", cash.cashierUser),
      detailItem("Sucursal", cash.branch),
      detailItem("Apertura", date(cash.openedAt)),
    ].join("");

    nodes.metricNio.textContent = money(cash.theoreticalNio);
    nodes.metricUsd.textContent = money(cash.theoreticalUsd);
    nodes.metricIncome.textContent = money(Number(cash.incomeNio || 0) + Number(cash.incomeUsd || 0));
    if (nodes.metricNioMirror) nodes.metricNioMirror.textContent = money(cash.theoreticalNio);
    if (nodes.metricUsdMirror) nodes.metricUsdMirror.textContent = money(cash.theoreticalUsd);
    if (nodes.metricIncomeMirror) nodes.metricIncomeMirror.textContent = money(Number(cash.incomeNio || 0) + Number(cash.incomeUsd || 0));
    updateAuditStrip();
    renderSummary();
  };

  const renderSummary = () => {
    const summary = state.summary;
    if (!summary) {
      nodes.cashSummaryBody.innerHTML = "<p>Sin movimientos.</p>";
      return;
    }

    const byMethod = summary.byMethod || [];
    const movements = summary.movements || [];
    nodes.cashSummaryBody.innerHTML = `
      <div class="badge-row">
        ${byMethod.map((item) => `<span class="badge">${escapeHtml(item.currency)} ${escapeHtml(item.method || "-")}: ${money(item.total)}</span>`).join("") || '<span class="badge">Sin ingresos</span>'}
      </div>
      <div class="mini-table">
        <table><thead><tr><th>Hora</th><th>Movimiento</th><th>Monto</th></tr></thead><tbody>
          ${movements.slice(0, 8).map((item) => `<tr><td>${date(item.date)}</td><td>${escapeHtml(item.voucherNumber || item.origin)}</td><td>${escapeHtml(item.currency)} ${money(item.amount)}</td></tr>`).join("") || '<tr><td colspan="3">Sin movimientos.</td></tr>'}
        </tbody></table>
      </div>`;
  };

  const renderReceipts = () => {
    nodes.tableCounter.textContent = `${state.receipts.length} registro${state.receipts.length === 1 ? "" : "s"}`;
    if (!state.receipts.length) {
      nodes.tableBody.innerHTML = `<tr><td colspan="7">No hay vouchers para los filtros seleccionados.</td></tr>`;
      return;
    }

    nodes.tableBody.innerHTML = state.receipts
      .map((item) => `
        <tr>
          <td><strong>${escapeHtml(item.voucherNumber)}</strong><br><span>${escapeHtml(item.officialReceiptNumber || "Sin recibo oficial")}</span></td>
          <td>${escapeHtml(item.creditNumber)}</td>
          <td>${escapeHtml(item.clientName)}<br><span>${escapeHtml(item.clientIdentification)}</span></td>
          <td>${date(item.date)}</td>
          <td>${escapeHtml(item.currency)} ${money(item.amount)}</td>
          <td>${escapeHtml(item.method || "-")}</td>
          <td class="row-actions">
            <button class="ghost-button compact-button" type="button" data-print="${item.paymentId}" data-reprint="true">Reimprimir</button>
            <button class="ghost-button compact-button" type="button" data-print="${item.paymentId}" data-reprint="false">Copia limpia</button>
            <button class="danger-button compact-button" type="button" data-void="${item.paymentId}">Anular</button>
          </td>
        </tr>`)
      .join("");
  };

  const renderCredits = () => {
    if (!state.credits.length) {
      nodes.creditResults.innerHTML = "<p>No hay prestamos para esa busqueda.</p>";
      return;
    }

    nodes.creditResults.innerHTML = state.credits
      .map((item) => {
        const due = creditDueSnapshot(item);
        const status = due.overdueDays > 0 || due.mora > 0 ? `${due.overdueDays} dias mora` : "Al dia";
        return `
          <button class="related-card credit-result-button" type="button" data-credit-id="${item.creditId}">
            <strong>${escapeHtml(item.creditNumber)} / ${escapeHtml(item.clientName)}</strong>
            <span>${escapeHtml(item.clientIdentification)} - ${escapeHtml(status)} - exigible ${escapeHtml(due.currency)} ${money(due.dueToday)}</span>
          </button>`;
      })
      .join("");
  };

  const renderDisbursements = () => {
    if (!nodes.disbursementBody) return;
    nodes.disbursementCounter.textContent = `${state.disbursements.length} registro${state.disbursements.length === 1 ? "" : "s"}`;
    if (!state.disbursements.length) {
      nodes.disbursementBody.innerHTML = `<tr><td colspan="6">No hay creditos aprobados pendientes de desembolso.</td></tr>`;
      return;
    }

    nodes.disbursementBody.innerHTML = state.disbursements
      .map((item) => `
        <tr>
          <td><strong>${escapeHtml(item.creditNumber)}</strong><br><span>${escapeHtml(item.requestNumber || "")}</span></td>
          <td>${escapeHtml(item.clientName)}<br><span>${escapeHtml(item.clientIdentification)}</span></td>
          <td>${escapeHtml(item.currency)} ${money(item.approvedAmount)}</td>
          <td>${escapeHtml(item.product || "-")}<br><span>${escapeHtml(item.destination || "")}</span></td>
          <td>${escapeHtml(item.promoter || "-")}<br><span>${escapeHtml(item.branch || "")}</span></td>
          <td><button class="primary-button compact-button" type="button" data-disburse="${item.creditId}">Desembolsar</button></td>
        </tr>`)
      .join("");
  };

  const renderCreditFocus = () => {
    const credit = state.selectedCredit;
    if (!credit) {
      nodes.creditFocusTitle.textContent = "Selecciona un prestamo";
      nodes.creditFocusMeta.textContent = "Sin datos";
      nodes.creditFocusMeta.className = "status-pill";
      nodes.creditBalanceGrid.innerHTML = [
        detailItem("Cliente", "-"),
        detailItem("Credito", "-"),
        detailItem("Estado", "-"),
        detailItem("Moneda", "-"),
        detailItem("Proximo vencimiento", "-"),
        detailItem("Dias mora", "-"),
      ].join("");
      if (nodes.debtRibbon) {
        nodes.debtRibbon.innerHTML = `
          <article><span>Cuota vencida</span><strong>NIO 0.00</strong></article>
          <article><span>Cuota del dia</span><strong>NIO 0.00</strong></article>
          <article><span>Mora pendiente</span><strong>NIO 0.00</strong></article>
          <article><span>Interes pendiente</span><strong>NIO 0.00</strong></article>
          <article><span>Capital pendiente</span><strong>NIO 0.00</strong></article>
          <article class="is-strong"><span>Total exigible hoy</span><strong>NIO 0.00</strong></article>`;
      }
      nodes.creditPlanPreview.hidden = true;
      nodes.creditPlanPreview.innerHTML = `
        <table>
          <thead><tr><th>Rubro</th><th>Monto</th></tr></thead>
          <tbody><tr><td colspan="2">Busca y selecciona un credito para ver la distribucion.</td></tr></tbody>
        </table>`;
      return;
    }

    const due = creditDueSnapshot(credit);
    const currency = due.currency;
    const isOverdue = due.overdueDays > 0 || due.overdue > 0 || due.mora > 0;
    nodes.creditFocusTitle.textContent = credit.clientName || credit.creditNumber;
    nodes.creditFocusMeta.textContent = isOverdue ? "En mora" : "Al dia";
    nodes.creditFocusMeta.className = isOverdue ? "status-pill status-danger" : "status-pill status-ok";
    nodes.creditBalanceGrid.innerHTML = [
      detailItem("Cliente", `${credit.clientIdentification || "-"} / ${credit.clientName || "-"}`),
      detailItem("Credito", credit.creditNumber || "-"),
      detailItem("Estado", credit.status || "ACTIVO"),
      detailItem("Moneda", currency),
      detailItem("Proximo vencimiento", shortDate(due.nextDate || credit.nextDueDate)),
      detailItem("Dias mora", String(due.overdueDays || 0)),
    ].join("");
    if (nodes.debtRibbon) {
      nodes.debtRibbon.innerHTML = `
        <article><span>Cuota vencida</span><strong>${currency} ${money(due.overdue)}</strong></article>
        <article><span>Cuota del dia</span><strong>${currency} ${money(credit.nextAmount || due.dueToday)}</strong></article>
        <article class="${due.mora > 0 ? "is-danger" : ""}"><span>Mora pendiente</span><strong>${currency} ${money(due.mora)}</strong></article>
        <article><span>Interes pendiente</span><strong>${currency} ${money(due.interest)}</strong></article>
        <article><span>Capital pendiente</span><strong>${currency} ${money(due.capitalBalance)}</strong></article>
        <article class="is-strong"><span>Total exigible hoy</span><strong>${currency} ${money(due.dueToday)}</strong></article>`;
    }

    const rows = [
      planRow("Intereses pendientes", currency, due.interest),
      planRow("Mora pendiente", currency, due.mora),
      planRow("Comisiones / cargos", currency, due.commission),
      planRow("Capital exigible", currency, due.capitalDue),
      planRow("Saldo capital total", currency, due.capitalBalance),
      planRow("Total exigible hoy", currency, due.dueToday),
    ].join("");
    nodes.creditPlanPreview.hidden = false;
    nodes.creditPlanPreview.innerHTML = `
      <table>
        <thead><tr><th>Deuda calculada</th><th>Monto</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>`;
  };

  const selectCredit = (creditId) => {
    state.selectedCredit = state.credits.find((item) => Number(item.creditId) === Number(creditId)) || null;
    const credit = state.selectedCredit;
    if (!credit) return;
    const due = creditDueSnapshot(credit);
    nodes.selectedCreditStatus.textContent = credit.creditNumber;
    nodes.paymentAmount.value = Number(due.dueToday || credit.nextAmount || 0).toFixed(2);
    nodes.paymentCurrency.value = credit.currency === "USD" ? "USD" : "NIO";
    if (nodes.payerDifferentToggle) nodes.payerDifferentToggle.checked = false;
    if (nodes.payerDrawer) nodes.payerDrawer.hidden = true;
    nodes.payerIdentification.value = "";
    nodes.payerName.value = "";
    nodes.payerPhone.value = "";
    nodes.manualReceipt.value = "";
    nodes.paymentObservation.value = "";
    nodes.paymentMessage.hidden = true;
    state.paymentStage = "capture";
    syncExchangeRateForDirection();
    renderCreditFocus();
    updatePaymentPreview();
    nodes.paymentAmount.focus();
  };

  const renderBreakdown = (container, currency, values) => {
    container.innerHTML = values
      .map((value) => `
        <label class="field denomination-line">
          <span>${currency} ${value}</span>
          <input type="number" min="0" step="1" value="0" data-currency="${currency}" data-denomination="${value}" />
        </label>`)
      .join("");
  };

  const readBreakdown = () =>
    [...nodes.breakdownNio.querySelectorAll("input"), ...nodes.breakdownUsd.querySelectorAll("input")]
      .map((input) => ({
        currency: input.dataset.currency,
        denomination: Number(input.dataset.denomination || 0),
        quantity: Number(input.value || 0),
      }))
      .filter((item) => item.quantity > 0);

  const breakdownTotal = (currency) =>
    readBreakdown()
      .filter((item) => item.currency === currency)
      .reduce((sum, item) => sum + item.denomination * item.quantity, 0);

  const cashAmountFromInputOrBreakdown = (input, currency) => {
    const raw = input.value.trim();
    const manual = raw === "" ? Number.NaN : Number(raw);
    const total = breakdownTotal(currency);
    if (Number.isFinite(manual) && manual < 0) return manual;
    if (total > 0) return total;
    return Number.isFinite(manual) ? manual : 0;
  };

  const validateCashPayload = (payload) => {
    const amountFields = state.cashMode === "open"
      ? [["openingNio", payload.openingNio], ["openingUsd", payload.openingUsd]]
      : [["physicalNio", payload.physicalNio], ["physicalUsd", payload.physicalUsd]];
    const badAmount = amountFields.find(([, value]) => Number(value) < 0);
    if (badAmount) return "No se permiten montos negativos en caja.";
    const badLine = (payload.breakdown || []).find((line) => Number(line.quantity) < 0 || Number(line.denomination) <= 0);
    if (badLine) return "El desglose solo acepta cantidades positivas y denominaciones validas.";
    return "";
  };

  const refreshCashCountPreview = () => {
    if (!["close", "count"].includes(state.cashMode)) {
      return;
    }

    const physicalNio = breakdownTotal("NIO");
    const physicalUsd = breakdownTotal("USD");
    nodes.openingNio.value = physicalNio.toFixed(2);
    nodes.openingUsd.value = physicalUsd.toFixed(2);
    const theoreticalNio = Number(state.cashSession?.theoreticalNio || 0);
    const theoreticalUsd = Number(state.cashSession?.theoreticalUsd || 0);
    const differenceNio = physicalNio - theoreticalNio;
    const differenceUsd = physicalUsd - theoreticalUsd;
    nodes.cashCountSummary.hidden = false;
    nodes.cashCountSummary.innerHTML = `
      <span>Conteo NIO: <strong>${money(physicalNio)}</strong></span>
      <span>Diferencia NIO: <strong>${signedMoney(differenceNio)}</strong></span>
      <span>Conteo USD: <strong>${money(physicalUsd)}</strong></span>
      <span>Diferencia USD: <strong>${signedMoney(differenceUsd)}</strong></span>`;
  };

  const loadCatalogs = async () => {
    const payload = await sessionApi.request("/Caja/Catalogos");
    state.catalogs = payload.data;
    state.cashSession = payload.data.session;
    state.summary = payload.data.summary;
    setOptions(nodes.paymentCurrency, state.catalogs.currencies);
    setOptions(nodes.paymentMethod, state.catalogs.methods);
    setOptions(nodes.disbursementCurrency, state.catalogs.currencies);
    setOptions(nodes.disbursementMethod, state.catalogs.methods);
    setOptions(nodes.cashBranch, state.catalogs.branches);
    if (state.catalogs.assignedBranch?.value) {
      nodes.cashBranch.value = state.catalogs.assignedBranch.value;
    }
    nodes.cashBranch.disabled = !!state.catalogs.branchLocked;
    if (nodes.cashBranchHint) {
      nodes.cashBranchHint.textContent = state.catalogs.branchLocked
        ? `Sucursal fija por usuario: ${state.catalogs.assignedBranch?.label || nodes.cashBranch.value || "asignada"}.`
        : `Sucursal sugerida por usuario: ${state.catalogs.assignedBranch?.label || nodes.cashBranch.value || "Casa Matriz"}.`;
    }
    syncExchangeRateForDirection();
    renderBreakdown(nodes.breakdownNio, "NIO", state.catalogs.denominations?.nio || state.catalogs.denominations?.NIO || []);
    renderBreakdown(nodes.breakdownUsd, "USD", state.catalogs.denominations?.usd || state.catalogs.denominations?.USD || []);
    renderCashSession();
    updateAuditStrip();
    updatePaymentPreview();
  };

  const loadReceipts = async () => {
    const payload = await sessionApi.request(`/Caja/ListarRecibos?${receiptParams()}`);
    state.receipts = payload.data || [];
    renderReceipts();
  };

  const searchCredits = async (autoSelect = false) => {
    const query = new URLSearchParams();
    query.set("search", nodes.creditSearchInput.value.trim());
    const payload = await sessionApi.request(`/Caja/BuscarCreditos?${query}`);
    state.credits = payload.data || [];
    renderCredits();
    state.paymentStage = state.credits.length ? "select" : "search";
    updateCashStepper();
    if (autoSelect && state.credits.length === 1) {
      selectCredit(state.credits[0].creditId);
    }
  };

  const loadDisbursements = async () => {
    const query = new URLSearchParams();
    query.set("search", nodes.disbursementSearchInput?.value?.trim() || "");
    const payload = await sessionApi.request(`/Caja/BuscarDesembolsos?${query}`);
    state.disbursements = payload.data || [];
    renderDisbursements();
  };

  const queueCreditSearch = () => {
    window.clearTimeout(state.searchTimer);
    const text = nodes.creditSearchInput.value.trim();
    if (text.length < 3) {
      return;
    }
    state.searchTimer = window.setTimeout(() => {
      searchCredits(true).catch((error) => showMessage(nodes.paymentMessage, error.message || "No se pudo buscar el prestamo."));
    }, 350);
  };

  const refreshAll = async () => {
    await loadCatalogs();
    await loadReceipts();
    await loadDisbursements();
  };

  const applyInitialRoute = async () => {
    const params = new URLSearchParams(window.location.search);
    const disbursementCreditId = params.get("credito") || params.get("desembolso") || params.get("disbursementId");
    const paymentCreditId = params.get("creditId") || params.get("creditoPago");

    if (disbursementCreditId) {
      showCajaView("desembolso");
      nodes.disbursementSearchInput.value = disbursementCreditId;
      await loadDisbursements();
      return;
    }

    if (paymentCreditId) {
      showCajaView("abono");
      nodes.creditSearchInput.value = paymentCreditId;
      await searchCredits(false);
      if (state.credits.some((item) => Number(item.creditId) === Number(paymentCreditId))) {
        selectCredit(paymentCreditId);
      } else if (state.credits.length === 1) {
        selectCredit(state.credits[0].creditId);
      }
    }
  };

  const openCashModal = (mode) => {
    state.cashMode = mode;
    const isOpen = mode === "open";
    const isCount = mode === "count";
    nodes.cashModalKicker.textContent = isOpen ? "Apertura" : isCount ? "Arqueo" : "Cierre";
    nodes.cashModalTitle.textContent = isOpen ? "Abrir caja" : isCount ? "Iniciar arqueo" : "Cerrar caja";
    nodes.cashSubmitButton.textContent = isOpen ? "Abrir caja" : isCount ? "Generar arqueo" : "Cerrar caja";
    nodes.cashNioLabel.textContent = isOpen ? "Apertura NIO" : "Conteo NIO";
    nodes.cashUsdLabel.textContent = isOpen ? "Apertura USD" : "Conteo USD";
    nodes.openingNio.readOnly = !isOpen;
    nodes.openingUsd.readOnly = !isOpen;
    nodes.openingNio.value = "0.00";
    nodes.openingUsd.value = "0.00";
    if (state.catalogs?.assignedBranch?.value) {
      nodes.cashBranch.value = state.catalogs.assignedBranch.value;
    }
    nodes.cashObservation.value = "";
    nodes.cashMessage.hidden = true;
    nodes.cashTheorySummary.hidden = isOpen;
    nodes.cashCountSummary.hidden = isOpen;
    if (!isOpen) {
      nodes.cashTheorySummary.innerHTML = `
        <span>Sistema NIO: <strong>${money(state.cashSession?.theoreticalNio)}</strong></span>
        <span>Sistema USD: <strong>${money(state.cashSession?.theoreticalUsd)}</strong></span>
        <span>Ingresos: <strong>${money(Number(state.cashSession?.incomeNio || 0) + Number(state.cashSession?.incomeUsd || 0))}</strong></span>`;
    }
    nodes.breakdownNio.querySelectorAll("input").forEach((input) => (input.value = "0"));
    nodes.breakdownUsd.querySelectorAll("input").forEach((input) => (input.value = "0"));
    refreshCashCountPreview();
    openModal(nodes.cashModal);
  };

  const submitCash = async (event) => {
    event.preventDefault();
    const breakdown = readBreakdown();
    const payload = state.cashMode === "open"
      ? {
          branch: nodes.cashBranch.value,
          openingNio: cashAmountFromInputOrBreakdown(nodes.openingNio, "NIO"),
          openingUsd: cashAmountFromInputOrBreakdown(nodes.openingUsd, "USD"),
          observation: nodes.cashObservation.value,
          breakdown,
        }
      : {
          physicalNio: cashAmountFromInputOrBreakdown(nodes.openingNio, "NIO"),
          physicalUsd: cashAmountFromInputOrBreakdown(nodes.openingUsd, "USD"),
          observation: nodes.cashObservation.value,
          breakdown,
        };
    const localValidation = validateCashPayload(payload);
    if (localValidation) {
      showMessage(nodes.cashMessage, localValidation);
      return;
    }

    try {
      const endpoint = state.cashMode === "open"
        ? "/Caja/AbrirSesion"
        : state.cashMode === "count"
          ? "/Caja/GenerarArqueo"
          : "/Caja/CerrarSesion";
      const response = await sessionApi.request(endpoint, {
        method: "POST",
        body: JSON.stringify(payload),
      });
      if (state.cashMode === "count") {
        showMessage(nodes.cashMessage, response.message || "Arqueo generado.");
      } else {
        closeModal(nodes.cashModal);
      }
      await refreshAll();
    } catch (error) {
      showMessage(nodes.cashMessage, error.message || "No se pudo procesar caja.");
    }
  };

  const submitPayment = async (event) => {
    event.preventDefault();
    if (!state.selectedCredit) {
      showMessage(nodes.paymentMessage, "Selecciona primero un prestamo.");
      return;
    }
    const method = String(nodes.paymentMethod.value || "EFECTIVO").toUpperCase();
    if (method !== "EFECTIVO" && !nodes.manualReceipt.value.trim()) {
      showMessage(nodes.paymentMessage, "La referencia es obligatoria cuando el pago no es efectivo.");
      nodes.manualReceipt.focus();
      return;
    }
    const payerIsDifferent = !!nodes.payerDifferentToggle?.checked;
    const payerIdentification = payerIsDifferent ? normalizeIdentification(nodes.payerIdentification.value) : "";
    if (payerIsDifferent) {
      if (!payerIdentification) {
        showMessage(nodes.paymentMessage, "Digita primero la cedula del abonante.");
        nodes.payerIdentification.focus();
        return;
      }
      if (!nodes.payerName.value.trim()) {
        showMessage(nodes.paymentMessage, "Digita el nombre completo del abonante.");
        nodes.payerName.focus();
        return;
      }
    }

    try {
      const shouldPrint = event.submitter?.dataset?.printAfter !== "false";
      const payload = await sessionApi.request("/Caja/AplicarPago", {
        method: "POST",
        body: JSON.stringify({
          creditId: state.selectedCredit.creditId,
          amount: Number(nodes.paymentAmount.value || 0),
          currency: nodes.paymentCurrency.value,
          method,
          exchangeRate: Number(nodes.paymentExchangeRate.value || 0) || null,
          payerName: payerIsDifferent ? nodes.payerName.value : "",
          payerIdentification,
          payerPhone: payerIsDifferent ? nodes.payerPhone.value : "",
          manualReceipt: nodes.manualReceipt.value,
          observation: payerIsDifferent ? nodes.paymentObservation.value : "",
        }),
      });
      state.lastPaymentPrintUrl = payload.data.printUrl || "";
      if (shouldPrint && state.lastPaymentPrintUrl) {
        sessionApi.openWithSession(state.lastPaymentPrintUrl);
      }
      if (nodes.receiptPreview) {
        nodes.receiptPreview.innerHTML = `
          <span>Comprobante generado</span>
          <strong>${escapeHtml(payload.data.voucherNumber || "Pago registrado")}</strong>
          <em>${shouldPrint ? "Impresion solicitada" : "Listo para reimprimir"}</em>`;
      }
      if (nodes.reprintLastPaymentButton) nodes.reprintLastPaymentButton.hidden = !state.lastPaymentPrintUrl;
      showMessage(nodes.paymentMessage, payload.message || "Cobro registrado correctamente.");
      state.paymentStage = "receipt";
      updateCashStepper();
      nodes.paymentForm.reset();
      if (nodes.payerDifferentToggle) nodes.payerDifferentToggle.checked = false;
      if (nodes.payerDrawer) nodes.payerDrawer.hidden = true;
      setPayerHint("Digite la cedula; si existe se rellena nombre y telefono.");
      state.selectedCredit = null;
      nodes.selectedCreditStatus.textContent = "Sin prestamo";
      renderCreditFocus();
      updatePaymentPreview();
      nodes.creditResults.innerHTML = "";
      nodes.creditSearchInput.value = "";
      await refreshAll();
    } catch (error) {
      showMessage(nodes.paymentMessage, error.message || "No se pudo aplicar el pago.");
    }
  };

  const resetPaymentFlow = () => {
    state.selectedCredit = null;
    state.paymentStage = "search";
    nodes.paymentForm.reset();
    nodes.creditSearchInput.value = "";
    nodes.creditResults.innerHTML = "";
    nodes.selectedCreditStatus.textContent = "Sin prestamo";
    if (nodes.payerDifferentToggle) nodes.payerDifferentToggle.checked = false;
    if (nodes.payerDrawer) nodes.payerDrawer.hidden = true;
    if (nodes.reprintLastPaymentButton) nodes.reprintLastPaymentButton.hidden = !state.lastPaymentPrintUrl;
    if (nodes.receiptPreview) {
      nodes.receiptPreview.innerHTML = "<span>Comprobante</span><strong>Se generara al confirmar</strong>";
    }
    nodes.paymentMessage.hidden = true;
    renderCreditFocus();
    updatePaymentPreview();
    nodes.creditSearchInput.focus();
  };

  const openVoidModal = (paymentId) => {
    const receipt = state.receipts.find((item) => Number(item.paymentId) === Number(paymentId));
    if (!receipt) return;
    state.selectedReceiptForVoid = receipt;
    nodes.voidVoucherSummary.innerHTML = [
      detailItem("Voucher", receipt.voucherNumber || "-"),
      detailItem("Recibo caja", receipt.officialReceiptNumber || "-"),
      detailItem("Cliente", `${receipt.clientIdentification || "-"} / ${receipt.clientName || "-"}`),
      detailItem("Monto", `${receipt.currency || "NIO"} ${money(receipt.amount)}`),
    ].join("");
    nodes.voidReason.value = "";
    nodes.voidMessage.hidden = true;
    openModal(nodes.voidModal);
  };

  const openDisbursementModal = (creditId) => {
    const item = state.disbursements.find((row) => Number(row.creditId) === Number(creditId));
    if (!item) return;
    state.selectedDisbursement = item;
    nodes.disbursementSummaryTitle.textContent = item.creditNumber || "Credito aprobado";
    nodes.disbursementSummary.innerHTML = [
      detailItem("Cliente", `${item.clientIdentification || "-"} / ${item.clientName || "-"}`),
      detailItem("Solicitud", item.requestNumber || "-"),
      detailItem("Monto aprobado", `${item.currency || "NIO"} ${money(item.approvedAmount)}`),
      detailItem("Destino", item.destination || "-"),
    ].join("");
    nodes.disbursementAmount.value = Number(item.approvedAmount || 0).toFixed(2);
    nodes.disbursementCurrency.value = item.currency || "NIO";
    nodes.disbursementMethod.value = "EFECTIVO";
    nodes.disbursementExchangeRate.value = Number(state.catalogs?.exchangeRates?.buy || state.catalogs?.exchangeRate || 0).toFixed(4);
    nodes.disbursementObservation.value = "";
    nodes.disbursementMessage.hidden = true;
    openModal(nodes.disbursementModal);
  };

  const submitDisbursement = async (event) => {
    event.preventDefault();
    const item = state.selectedDisbursement;
    if (!item) {
      showMessage(nodes.disbursementMessage, "Selecciona un credito aprobado.");
      return;
    }

    try {
      const payload = await sessionApi.request("/Caja/DesembolsarCredito", {
        method: "POST",
        body: JSON.stringify({
          creditId: item.creditId,
          amount: Number(nodes.disbursementAmount.value || 0),
          currency: nodes.disbursementCurrency.value,
          method: nodes.disbursementMethod.value,
          exchangeRate: Number(nodes.disbursementExchangeRate.value || 0) || null,
          observation: nodes.disbursementObservation.value,
        }),
      });
      closeModal(nodes.disbursementModal);
      state.selectedDisbursement = null;
      sessionApi.openWithSession(payload.data.printUrl);
      await refreshAll();
    } catch (error) {
      showMessage(nodes.disbursementMessage, error.message || "No se pudo desembolsar el credito.");
    }
  };

  const submitVoidPayment = async (event) => {
    event.preventDefault();
    const receipt = state.selectedReceiptForVoid;
    if (!receipt) {
      showMessage(nodes.voidMessage, "Selecciona un voucher para anular.");
      return;
    }

    try {
      await sessionApi.request("/Caja/AnularPago", {
        method: "POST",
        body: JSON.stringify({
          paymentId: receipt.paymentId,
          reason: nodes.voidReason.value,
        }),
      });
      closeModal(nodes.voidModal);
      state.selectedReceiptForVoid = null;
      await refreshAll();
    } catch (error) {
      showMessage(nodes.voidMessage, error.message || "No se pudo anular el voucher.");
    }
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
    nodes.openCashButton.addEventListener("click", () => openCashModal("open"));
    nodes.closeCashButton.addEventListener("click", () => openCashModal("close"));
    nodes.startCashCountButton?.addEventListener("click", () => openCashModal("count"));
    nodes.cashReportButton.addEventListener("click", () => {
      if (state.cashSession?.id) {
        sessionApi.openWithSession(`/Caja/HojaArqueoHtml?id=${encodeURIComponent(state.cashSession.id)}`);
      }
    });
    nodes.cashReportButtonMirror?.addEventListener("click", () => nodes.cashReportButton.click());
    document.querySelectorAll("[data-caja-view]").forEach((button) => {
      button.addEventListener("click", () => showCajaView(button.dataset.cajaView));
    });
    nodes.cashModalClose.addEventListener("click", () => closeModal(nodes.cashModal));
    nodes.cashCancel.addEventListener("click", () => closeModal(nodes.cashModal));
    nodes.cashForm.addEventListener("submit", submitCash);
    nodes.breakdownNio.addEventListener("input", refreshCashCountPreview);
    nodes.breakdownUsd.addEventListener("input", refreshCashCountPreview);
    nodes.voidModalClose.addEventListener("click", () => closeModal(nodes.voidModal));
    nodes.voidCancel.addEventListener("click", () => closeModal(nodes.voidModal));
    nodes.voidForm.addEventListener("submit", submitVoidPayment);
    nodes.disbursementModalClose.addEventListener("click", () => closeModal(nodes.disbursementModal));
    nodes.disbursementCancel.addEventListener("click", () => closeModal(nodes.disbursementModal));
    nodes.disbursementForm.addEventListener("submit", submitDisbursement);
    nodes.refreshDisbursementsButton.addEventListener("click", loadDisbursements);
    nodes.disbursementSearchInput.addEventListener("input", () => {
      window.clearTimeout(nodes.disbursementSearchInput._timer);
      nodes.disbursementSearchInput._timer = window.setTimeout(loadDisbursements, 300);
    });
    nodes.disbursementBody.addEventListener("click", (event) => {
      const button = event.target.closest("button[data-disburse]");
      if (button) openDisbursementModal(button.dataset.disburse);
    });
    nodes.searchCreditButton.addEventListener("click", () => searchCredits(false));
    nodes.creditSearchInput.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        searchCredits(false);
      }
    });
    nodes.creditSearchInput.addEventListener("input", () => {
      window.clearTimeout(nodes.creditSearchInput._timer);
      nodes.creditSearchInput._timer = window.setTimeout(searchCredits, 350);
    });
    nodes.creditResults.addEventListener("click", (event) => {
      const button = event.target.closest("[data-credit-id]");
      if (button) selectCredit(button.dataset.creditId);
    });
    nodes.paymentAmount.addEventListener("input", updatePaymentPreview);
    nodes.paymentCurrency.addEventListener("change", updatePaymentPreview);
    nodes.paymentMethod.addEventListener("change", updatePaymentPreview);
    nodes.manualReceipt.addEventListener("input", updatePaymentPreview);
    nodes.paymentExchangeRate.addEventListener("input", updatePaymentPreview);
    nodes.paymentForm.addEventListener("submit", submitPayment);
    nodes.payerDifferentToggle?.addEventListener("change", () => {
      const enabled = nodes.payerDifferentToggle.checked;
      if (nodes.payerDrawer) nodes.payerDrawer.hidden = !enabled;
      if (enabled) {
        nodes.payerIdentification.focus();
      } else {
        nodes.payerIdentification.value = "";
        nodes.payerName.value = "";
        nodes.payerPhone.value = "";
        nodes.paymentObservation.value = "";
        setPayerHint("Digite la cedula; si existe se rellena nombre y telefono.");
      }
    });
    nodes.newPaymentButton?.addEventListener("click", resetPaymentFlow);
    nodes.reprintLastPaymentButton?.addEventListener("click", () => {
      if (state.lastPaymentPrintUrl) sessionApi.openWithSession(state.lastPaymentPrintUrl);
    });
    nodes.payerIdentification.addEventListener("blur", () => lookupPayer(true));
    nodes.payerIdentification.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        lookupPayer(true).then((payer) => {
          if (payer?.found) nodes.paymentAmount.focus();
          else nodes.payerName.focus();
        });
      }
    });
    nodes.payerIdentification.addEventListener("input", () => {
      window.clearTimeout(nodes.payerIdentification._lookupTimer);
      const value = normalizeIdentification(nodes.payerIdentification.value);
      if (value.length < 6) return;
      nodes.payerIdentification._lookupTimer = window.setTimeout(() => lookupPayer(false), 450);
    });
    nodes.refreshButton.addEventListener("click", loadReceipts);
    nodes.clearButton.addEventListener("click", () => {
      nodes.searchInput.value = "";
      nodes.dateFrom.value = "";
      nodes.dateTo.value = "";
      loadReceipts();
    });
    nodes.searchInput.addEventListener("input", () => {
      window.clearTimeout(nodes.searchInput._timer);
      nodes.searchInput._timer = window.setTimeout(loadReceipts, 300);
    });
    nodes.dateFrom.addEventListener("change", loadReceipts);
    nodes.dateTo.addEventListener("change", loadReceipts);
    nodes.tableBody.addEventListener("click", (event) => {
      const printButton = event.target.closest("button[data-print]");
      if (printButton) {
        sessionApi.openWithSession(`/Caja/VoucherPagoHtml?id=${encodeURIComponent(printButton.dataset.print)}&reprint=${printButton.dataset.reprint === "true"}`);
        return;
      }

      const voidButton = event.target.closest("button[data-void]");
      if (voidButton) {
        openVoidModal(voidButton.dataset.void);
      }
    });
  };

  const init = async () => {
    document.body.classList.add("modals-ready");
    if (!initSession()) return;
    bindEvents();
    refreshThemeLabel();
    renderCreditFocus();
    nodes.creditSearchInput.focus();
    try {
      await refreshAll();
      await applyInitialRoute();
    } catch (error) {
      nodes.tableBody.innerHTML = `<tr><td colspan="7">${escapeHtml(error.message || "No se pudo cargar caja.")}</td></tr>`;
    }
  };

  init();
})();
