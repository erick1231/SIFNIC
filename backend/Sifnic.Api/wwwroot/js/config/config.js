const sessionApi = window.SifnicSession;
const ADMIN_ROLES = ["ADMINISTRADOR", "ADMINISTRACION"];
const DEFAULT_PANEL_SECTIONS = {
  general: "empresa",
  reportes: "logos",
  seguridad: "politicas",
  rrhh: "catalogos",
  nomina: "empresa",
  "tipo-cambio": "oficial",
  conami: "riesgo-cliente",
};

const state = {
  session: null,
  activeTab: "general",
  users: [],
  accessRows: [],
  movementRows: [],
  generalConfig: null,
  securityConfig: null,
  payrollConfig: null,
  exchangeConfig: null,
  conamiConfig: null,
  creditProducts: [],
  loadingTab: null,
  panelSections: { ...DEFAULT_PANEL_SECTIONS },
  moduleAccess: {
    userId: null,
    loading: false,
    saving: false,
    username: "",
    fullName: "",
    roles: [],
    hasCustomConfiguration: false,
    modules: [],
  },
};

const VALID_TABS = new Set([
  "general",
  "reportes",
  "seguridad",
  "rrhh",
  "nomina",
  "tipo-cambio",
  "conami",
  "creditos",
  "usuarios",
  "accesos",
  "movimientos",
]);

const elements = {
  sessionUser: document.getElementById("sessionUser"),
  sessionMeta: document.getElementById("sessionMeta"),
  backToDashboard: document.getElementById("backToDashboard"),
  logoutButton: document.getElementById("logoutButton"),
  refreshCurrentView: document.getElementById("refreshCurrentView"),
  tabRow: document.getElementById("tabRow"),
  toastRegion: document.getElementById("toastRegion"),
  generalPanel: document.getElementById("generalPanel"),
  reportPanel: document.getElementById("reportPanel"),
  securityPanel: document.getElementById("securityPanel"),
  payrollPanel: document.getElementById("payrollPanel"),
  exchangePanel: document.getElementById("exchangePanel"),
  conamiPanel: document.getElementById("conamiPanel"),
  creditProductsPanel: document.getElementById("creditProductsPanel"),
  rrhhPanel: document.getElementById("rrhhPanel"),
  usersPanel: document.getElementById("usersPanel"),
  accessPanel: document.getElementById("accessPanel"),
  movementPanel: document.getElementById("movementPanel"),
  usersTableBody: document.getElementById("usersTableBody"),
  accessTableBody: document.getElementById("accessTableBody"),
  movementTableBody: document.getElementById("movementTableBody"),
  usersEmptyState: document.getElementById("usersEmptyState"),
  accessEmptyState: document.getElementById("accessEmptyState"),
  movementEmptyState: document.getElementById("movementEmptyState"),
  metricActiveUsers: document.getElementById("metricActiveUsers"),
  metricPendingReset: document.getElementById("metricPendingReset"),
  metricAccessRows: document.getElementById("metricAccessRows"),
  metricMovementRows: document.getElementById("metricMovementRows"),
  generalCompanyId: document.getElementById("generalCompanyId"),
  generalConfigId: document.getElementById("generalConfigId"),
  companyLegalNameInput: document.getElementById("companyLegalNameInput"),
  companyTradeNameInput: document.getElementById("companyTradeNameInput"),
  companyRucInput: document.getElementById("companyRucInput"),
  companyPhoneInput: document.getElementById("companyPhoneInput"),
  companyEmailInput: document.getElementById("companyEmailInput"),
  companyAddressInput: document.getElementById("companyAddressInput"),
  systemNameInput: document.getElementById("systemNameInput"),
  themeColorInput: document.getElementById("themeColorInput"),
  companyLogoUrlInput: document.getElementById("companyLogoUrlInput"),
  saveGeneralButton: document.getElementById("saveGeneralButton"),
  reportLogoUrlInput: document.getElementById("reportLogoUrlInput"),
  loginLogoUrlInput: document.getElementById("loginLogoUrlInput"),
  footerTextInput: document.getElementById("footerTextInput"),
  supportEmailInput: document.getElementById("supportEmailInput"),
  supportPhoneInput: document.getElementById("supportPhoneInput"),
  showLoginLogoInput: document.getElementById("showLoginLogoInput"),
  saveReportButton: document.getElementById("saveReportButton"),
  uploadCompanyLogoButton: document.getElementById("uploadCompanyLogoButton"),
  uploadCompanyLogoInput: document.getElementById("companyLogoUploadInput"),
  uploadReportLogoButton: document.getElementById("uploadReportLogoButton"),
  uploadReportLogoInput: document.getElementById("reportLogoUploadInput"),
  uploadLoginLogoButton: document.getElementById("uploadLoginLogoButton"),
  uploadLoginLogoInput: document.getElementById("loginLogoUploadInput"),
  companyLogoPreviewShell: document.getElementById("companyLogoPreviewShell"),
  companyLogoPreview: document.getElementById("companyLogoPreview"),
  reportLogoPreviewShell: document.getElementById("reportLogoPreviewShell"),
  reportLogoPreview: document.getElementById("reportLogoPreview"),
  loginLogoPreviewShell: document.getElementById("loginLogoPreviewShell"),
  loginLogoPreview: document.getElementById("loginLogoPreview"),
  hrManagerNameInput: document.getElementById("hrManagerNameInput"),
  securityAttemptsInput: document.getElementById("securityAttemptsInput"),
  securitySessionMinutesInput: document.getElementById("securitySessionMinutesInput"),
  securityRecoveryHoursInput: document.getElementById("securityRecoveryHoursInput"),
  saveSecurityButton: document.getElementById("saveSecurityButton"),
  payrollRegimenInput: document.getElementById("payrollRegimenInput"),
  payrollWorkersInput: document.getElementById("payrollWorkersInput"),
  payrollPasantiaModeInput: document.getElementById("payrollPasantiaModeInput"),
  payrollDaysInput: document.getElementById("payrollDaysInput"),
  payrollHoursInput: document.getElementById("payrollHoursInput"),
  savePayrollButton: document.getElementById("savePayrollButton"),
  exchangeBaseCurrency: document.getElementById("exchangeBaseCurrency"),
  exchangeCompanyName: document.getElementById("exchangeCompanyName"),
  exchangeOfficialValue: document.getElementById("exchangeOfficialValue"),
  exchangeOfficialMeta: document.getElementById("exchangeOfficialMeta"),
  exchangeOfficialBatchMeta: document.getElementById("exchangeOfficialBatchMeta"),
  exchangeInstitutionalBuy: document.getElementById("exchangeInstitutionalBuy"),
  exchangeInstitutionalBuyMeta: document.getElementById("exchangeInstitutionalBuyMeta"),
  exchangeInstitutionalSell: document.getElementById("exchangeInstitutionalSell"),
  exchangeInstitutionalSellMeta: document.getElementById("exchangeInstitutionalSellMeta"),
  uploadOfficialRateButton: document.getElementById("uploadOfficialRateButton"),
  officialRateUploadInput: document.getElementById("officialRateUploadInput"),
  institutionalDateInput: document.getElementById("institutionalDateInput"),
  institutionalReferenceInput: document.getElementById("institutionalReferenceInput"),
  institutionalBuyInput: document.getElementById("institutionalBuyInput"),
  institutionalSellInput: document.getElementById("institutionalSellInput"),
  institutionalObservationInput: document.getElementById("institutionalObservationInput"),
  saveInstitutionalRateButton: document.getElementById("saveInstitutionalRateButton"),
  officialRateHistoryBody: document.getElementById("officialRateHistoryBody"),
  officialRateHistoryEmpty: document.getElementById("officialRateHistoryEmpty"),
  institutionalRateHistoryBody: document.getElementById("institutionalRateHistoryBody"),
  institutionalRateHistoryEmpty: document.getElementById("institutionalRateHistoryEmpty"),
  conamiNormsGrid: document.getElementById("conamiNormsGrid"),
  conamiRulesClientBody: document.getElementById("conamiRulesClientBody"),
  conamiRulesCreditBody: document.getElementById("conamiRulesCreditBody"),
  conamiRulesFileBody: document.getElementById("conamiRulesFileBody"),
  conamiRulesPortfolioBody: document.getElementById("conamiRulesPortfolioBody"),
  conamiRulesReportsBody: document.getElementById("conamiRulesReportsBody"),
  saveConamiRulesButton: document.getElementById("saveConamiRulesButton"),
  creditProductsBody: document.getElementById("creditProductsBody"),
  creditProductsEmpty: document.getElementById("creditProductsEmpty"),
  modulesModal: document.getElementById("modulesModal"),
  closeModulesModal: document.getElementById("closeModulesModal"),
  modulesModalTitle: document.getElementById("modulesModalTitle"),
  modulesModalNote: document.getElementById("modulesModalNote"),
  modulesModeBadge: document.getElementById("modulesModeBadge"),
  modulesModeHelp: document.getElementById("modulesModeHelp"),
  modulesGrid: document.getElementById("modulesGrid"),
  modulesEmptyState: document.getElementById("modulesEmptyState"),
  restoreAutomaticModules: document.getElementById("restoreAutomaticModules"),
  saveModulesButton: document.getElementById("saveModulesButton"),
};

const resolveInitialTab = () => {
  const params = new URLSearchParams(window.location.search);
  const requestedTab = (params.get("tab") || "").trim().toLowerCase();
  return VALID_TABS.has(requestedTab) ? requestedTab : "general";
};

const escapeHtml = (value) =>
  String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");

const toNumber = (value, fallback = 0) => {
  const parsed = Number.parseFloat(String(value ?? "").trim());
  return Number.isFinite(parsed) ? parsed : fallback;
};

const getIconSvg = (name) => {
  const iconBody = {
    home: '<path d="M3 10.5 12 3l9 7.5" /><path d="M5 9.8V21h14V9.8" /><path d="M9 21v-6h6v6" />',
    logout:
      '<path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" /><path d="M10 17l5-5-5-5" /><path d="M15 12H3" />',
    refresh:
      '<path d="M20 11a8 8 0 1 0 2 5.5" /><path d="M20 4v7h-7" />',
    building:
      '<path d="M4 21V6l8-2v17" /><path d="M12 21V10l8-2v13" /><path d="M8 8v1M8 12v1M8 16v1M16 12v1M16 16v1" />',
    image:
      '<rect x="3" y="5" width="18" height="14" rx="2" /><circle cx="8.5" cy="10" r="1.5" /><path d="m21 15-4.5-4.5L9 18" />',
    shield:
      '<path d="M12 3 5 6v6c0 4.5 3 7.5 7 9 4-1.5 7-4.5 7-9V6l-7-3Z" /><path d="m9.5 12 1.8 1.8 3.7-4" />',
    sitemap:
      '<path d="M12 5v4" /><path d="M6 13h12" /><path d="M6 13v4" /><path d="M12 13v4" /><path d="M18 13v4" /><rect x="4" y="17" width="4" height="3" rx="1" /><rect x="10" y="17" width="4" height="3" rx="1" /><rect x="16" y="17" width="4" height="3" rx="1" /><rect x="10" y="5" width="4" height="3" rx="1" />',
    calculator:
      '<rect x="5" y="3" width="14" height="18" rx="2" /><path d="M8 7h8" /><path d="M8 11h2M12 11h2M16 11h.01M8 15h2M12 15h2M16 15h.01" />',
    sliders:
      '<path d="M4 6h6" /><path d="M14 6h6" /><circle cx="12" cy="6" r="2" /><path d="M4 12h10" /><path d="M18 12h2" /><circle cx="16" cy="12" r="2" /><path d="M4 18h2" /><path d="M10 18h10" /><circle cx="8" cy="18" r="2" />',
    fileChart:
      '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z" /><path d="M14 3v5h5" /><path d="M9 17v-4" /><path d="M13 17V9" /><path d="M17 17v-6" />',
    users:
      '<path d="M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" /><circle cx="10" cy="7" r="3" /><path d="M20 21v-2a4 4 0 0 0-3-3.9" /><path d="M17 4.2a3 3 0 0 1 0 5.8" />',
    history:
      '<path d="M3 12a9 9 0 1 0 3-6.7" /><path d="M3 4v5h5" /><path d="M12 7v5l3 2" />',
    save:
      '<path d="M5 21h14a1 1 0 0 0 1-1V7.5L16.5 4H5a1 1 0 0 0-1 1v15a1 1 0 0 0 1 1Z" /><path d="M8 21v-6h8v6" /><path d="M8 4v5h7" />',
    upload:
      '<path d="M12 16V5" /><path d="m7 10 5-5 5 5" /><path d="M5 19h14" />',
    copy:
      '<rect x="9" y="9" width="10" height="10" rx="2" /><rect x="5" y="5" width="10" height="10" rx="2" />',
    grid:
      '<rect x="4" y="4" width="6" height="6" rx="1.5" /><rect x="14" y="4" width="6" height="6" rx="1.5" /><rect x="4" y="14" width="6" height="6" rx="1.5" /><rect x="14" y="14" width="6" height="6" rx="1.5" />',
    unlock:
      '<rect x="5" y="11" width="14" height="10" rx="2" /><path d="M9 11V8a4 4 0 0 1 7.7-1.5" />',
    key:
      '<circle cx="8" cy="14" r="3" /><path d="M10.5 14H21" /><path d="M18 11v6" />',
    close: '<path d="M6 6 18 18" /><path d="M18 6 6 18" />',
  };

  const body = iconBody[name] || iconBody.sliders;
  return `
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      ${body}
    </svg>
  `;
};

const createButtonContent = (label, iconName, options = {}) => {
  const { spin = false } = options;
  return `
    <span class="button-inline${spin ? " is-spinning" : ""}">
      <span class="button-icon${spin ? " is-spinning" : ""}">
        ${getIconSvg(iconName)}
      </span>
      <span>${escapeHtml(label)}</span>
    </span>
  `;
};

const setButtonLabel = (button, label, iconName, options = {}) => {
  if (!button) {
    return;
  }

  const { rememberDefault = true, spin = false } = options;
  button.innerHTML = createButtonContent(label, iconName, { spin });

  if (rememberDefault) {
    button.dataset.defaultLabel = label;
    button.dataset.defaultIcon = iconName;
    button.dataset.defaultHtml = button.innerHTML;
  }
};

const decorateShortcutCard = (button, iconName) => {
  if (!button || button.dataset.decorated === "true") {
    return;
  }

  const kicker = button.querySelector(".shortcut-kicker")?.outerHTML || "";
  const title = button.querySelector("strong")?.outerHTML || "";
  const copy = button.querySelector("small")?.outerHTML || "";
  button.innerHTML = `
    <span class="shortcut-icon" aria-hidden="true">${getIconSvg(iconName)}</span>
    ${kicker}
    ${title}
    ${copy}
  `;
  button.dataset.decorated = "true";
};

const decorateTabButton = (button, iconName) => {
  if (!button || button.dataset.decorated === "true") {
    return;
  }

  const label = button.textContent.trim();
  button.innerHTML = `
    <span class="tab-inline">
      <span class="tab-icon" aria-hidden="true">${getIconSvg(iconName)}</span>
      <span>${escapeHtml(label)}</span>
    </span>
  `;
  button.dataset.decorated = "true";
};

const applyStaticDecorations = () => {
  setButtonLabel(elements.backToDashboard, "Panel principal", "home");
  setButtonLabel(elements.logoutButton, "Cerrar sesion", "logout");
  setButtonLabel(elements.refreshCurrentView, "Actualizar vista", "refresh");
  setButtonLabel(elements.saveGeneralButton, "Guardar configuracion general", "save");
  setButtonLabel(elements.saveReportButton, "Guardar branding y reportes", "save");
  setButtonLabel(elements.saveSecurityButton, "Guardar parametros de seguridad", "save");
  setButtonLabel(elements.savePayrollButton, "Guardar parametros de nomina", "save");
  setButtonLabel(elements.uploadOfficialRateButton, "Importar archivo BCN", "upload");
  setButtonLabel(elements.saveInstitutionalRateButton, "Guardar tipo de cambio institucional", "save");
  setButtonLabel(elements.saveConamiRulesButton, "Guardar reglas CONAMI", "save");
  setButtonLabel(elements.uploadCompanyLogoButton, "Cargar imagen", "upload");
  setButtonLabel(elements.uploadReportLogoButton, "Cargar imagen", "upload");
  setButtonLabel(elements.uploadLoginLogoButton, "Cargar imagen", "upload");
  setButtonLabel(elements.closeModulesModal, "Cerrar", "close");
  setButtonLabel(elements.restoreAutomaticModules, "Restaurar automatico", "refresh");
  setButtonLabel(elements.saveModulesButton, "Guardar modulos", "save");

  decorateShortcutCard(document.querySelector('[data-tab-jump="general"]'), "building");
  decorateShortcutCard(document.querySelector('[data-tab-jump="reportes"]'), "image");
  decorateShortcutCard(document.querySelector('[data-tab-jump="seguridad"]'), "shield");
  decorateShortcutCard(document.querySelector('[data-tab-jump="rrhh"]'), "sitemap");
  decorateShortcutCard(document.querySelector('[data-tab-jump="nomina"]'), "calculator");
  decorateShortcutCard(document.querySelector('[data-tab-jump="tipo-cambio"]'), "sliders");

  decorateTabButton(document.querySelector('[data-tab="general"]'), "building");
  decorateTabButton(document.querySelector('[data-tab="reportes"]'), "fileChart");
  decorateTabButton(document.querySelector('[data-tab="seguridad"]'), "shield");
  decorateTabButton(document.querySelector('[data-tab="rrhh"]'), "sitemap");
  decorateTabButton(document.querySelector('[data-tab="nomina"]'), "calculator");
  decorateTabButton(document.querySelector('[data-tab="tipo-cambio"]'), "sliders");
  decorateTabButton(document.querySelector('[data-tab="conami"]'), "fileChart");
  decorateTabButton(document.querySelector('[data-tab="usuarios"]'), "users");
  decorateTabButton(document.querySelector('[data-tab="accesos"]'), "history");
  decorateTabButton(document.querySelector('[data-tab="movimientos"]'), "grid");
};

const showToast = (message, tone = "success") => {
  const toast = document.createElement("div");
  toast.className = `toast is-${tone}`;
  toast.textContent = message;
  elements.toastRegion.appendChild(toast);

  window.setTimeout(() => {
    toast.remove();
  }, 3400);
};

const redirectToLogin = () => {
  window.location.href = "/App/Login";
};

const setButtonsDisabled = (disabled) => {
  elements.refreshCurrentView.disabled = disabled;
  elements.logoutButton.disabled = disabled;
};

const setModuleButtonsDisabled = (disabled) => {
  elements.closeModulesModal.disabled = disabled;
  elements.restoreAutomaticModules.disabled = disabled;
  elements.saveModulesButton.disabled = disabled;
};

const setButtonBusy = (button, busy, busyText) => {
  if (!button) {
    return;
  }

  if (!button.dataset.defaultHtml) {
    button.dataset.defaultHtml = button.innerHTML;
    button.dataset.defaultLabel = button.textContent.trim();
    button.dataset.defaultIcon = button.dataset.defaultIcon || "save";
  }

  button.disabled = busy;
  button.innerHTML = busy
    ? createButtonContent(busyText, "refresh", { spin: true })
    : button.dataset.defaultHtml;
};

const getStatusTone = (user) => {
  if (!user.activo || user.bloqueado) {
    return "danger";
  }

  if (user.requiereCambioClave) {
    return "warning";
  }

  return "success";
};

const getStatusLabel = (user) => {
  if (!user.activo) {
    return "Inactivo";
  }

  if (user.bloqueado) {
    return "Bloqueado";
  }

  if (user.requiereCambioClave) {
    return "Clave temporal";
  }

  return "Activo";
};

const getResultTone = (result) => {
  const value = String(result || "").toUpperCase();
  if (value === "AUTORIZADO") {
    return "success";
  }
  if (value === "CAMBIO_CLAVE_REQUERIDO") {
    return "warning";
  }
  return "danger";
};

const getModuleConfigPill = (user) =>
  user.tieneConfiguracionModulos
    ? '<span class="module-config-pill is-custom">Personalizado</span>'
    : '<span class="module-config-pill is-auto">Automatico</span>';

const renderMetrics = () => {
  const activeUsers = state.users.filter((user) => user.activo && !user.bloqueado).length;
  const pendingReset = state.users.filter((user) => user.requiereCambioClave).length;

  elements.metricActiveUsers.textContent = String(activeUsers);
  elements.metricPendingReset.textContent = String(pendingReset);
  elements.metricAccessRows.textContent = String(state.accessRows.length);
  elements.metricMovementRows.textContent = String(state.movementRows.length);
};

const resolvePanelSection = (scope) =>
  state.panelSections[scope] || DEFAULT_PANEL_SECTIONS[scope] || "";

const renderPanelSections = () => {
  document.querySelectorAll("[data-section-panel]").forEach((panel) => {
    const [scope = "", target = ""] = String(panel.dataset.sectionPanel || "").split(":");
    panel.hidden = resolvePanelSection(scope) !== target;
  });

  document
    .querySelectorAll("[data-section-scope][data-section-target]")
    .forEach((button) => {
      const isActive = resolvePanelSection(button.dataset.sectionScope) === button.dataset.sectionTarget;
      button.classList.toggle("is-active", isActive);
    });
};

const renderUsers = () => {
  const users = state.users;
  elements.usersEmptyState.hidden = users.length > 0;

  elements.usersTableBody.innerHTML = users
    .map(
      (user) => `
        <tr>
          <td>
            <strong>${escapeHtml(user.nombreCompleto || "-")}</strong>
            <div class="muted">${escapeHtml(user.correo || "Sin correo")}</div>
          </td>
          <td>${escapeHtml(user.cargo || "Sin cargo")}</td>
          <td>
            <strong>${escapeHtml(user.usuario)}</strong>
            <div class="muted">${escapeHtml(user.telefono || "Sin telefono")}</div>
          </td>
          <td>
            <span class="role-pill">${escapeHtml(user.roles || "Sin rol")}</span>
            <div class="muted" style="margin-top:8px;">${getModuleConfigPill(user)}</div>
          </td>
          <td>
            <span class="status-pill is-${getStatusTone(user)}">
              ${escapeHtml(getStatusLabel(user))}
            </span>
          </td>
          <td>${escapeHtml(sessionApi.formatDateTime(user.fechaUltimoAcceso))}</td>
          <td>
            <div class="table-actions">
              <button class="table-action" type="button" data-modules-user="${user.idUsuario}">
                ${createButtonContent("Modulos", "grid")}
              </button>
              <button
                class="table-action"
                type="button"
                data-unlock-user="${user.idUsuario}"
                ${user.bloqueado ? "" : "disabled"}
              >
                ${createButtonContent("Desbloquear", "unlock")}
              </button>
              <button class="table-action is-danger" type="button" data-reset-user="${user.idUsuario}">
                ${createButtonContent("Restablecer clave", "key")}
              </button>
            </div>
          </td>
        </tr>
      `,
    )
    .join("");
};

const renderAccess = () => {
  const rows = state.accessRows;
  elements.accessEmptyState.hidden = rows.length > 0;

  elements.accessTableBody.innerHTML = rows
    .map(
      (row) => `
        <tr>
          <td>${escapeHtml(sessionApi.formatDateTime(row.fechaEvento))}</td>
          <td>${escapeHtml(row.usuario || "-")}</td>
          <td>${escapeHtml(row.nombreCompleto || "-")}</td>
          <td>
            <span class="result-pill is-${getResultTone(row.resultado)}">
              ${escapeHtml(row.resultado || "-")}
            </span>
          </td>
          <td>${escapeHtml(row.detalle || "-")}</td>
          <td>${escapeHtml(row.equipo || "-")}</td>
          <td>${escapeHtml(row.ip || "-")}</td>
        </tr>
      `,
    )
    .join("");
};

const renderMovements = () => {
  const rows = state.movementRows;
  elements.movementEmptyState.hidden = rows.length > 0;

  elements.movementTableBody.innerHTML = rows
    .map(
      (row) => `
        <tr>
          <td>${escapeHtml(sessionApi.formatDateTime(row.fechaEvento))}</td>
          <td>${escapeHtml(row.modulo || "-")}</td>
          <td>${escapeHtml(row.proceso || "-")}</td>
          <td>${escapeHtml(row.tipoEvento || "-")}</td>
          <td>${escapeHtml(row.usuario || "-")}</td>
          <td>${escapeHtml(row.descripcion || "-")}</td>
          <td>${escapeHtml(row.referencia || "-")}</td>
        </tr>
      `,
    )
    .join("");
};

const setLogoPreview = (url, shell, image) => {
  const normalized = String(url || "").trim();
  const hasImage = normalized.length > 0;

  shell.hidden = !hasImage;
  image.hidden = !hasImage;

  if (hasImage) {
    image.src = normalized;
  } else {
    image.removeAttribute("src");
  }
};

const renderGeneralAndReportForms = () => {
  const config = state.generalConfig || {};

  elements.generalCompanyId.value = config.idEmpresa || "";
  elements.generalConfigId.value = config.idConfiguracionGeneral || "";
  elements.companyLegalNameInput.value = config.razonSocial || "";
  elements.companyTradeNameInput.value = config.nombreComercial || "";
  elements.companyRucInput.value = config.ruc || "";
  elements.companyPhoneInput.value = config.telefonoEmpresa || "";
  elements.companyEmailInput.value = config.correoEmpresa || "";
  elements.companyAddressInput.value = config.direccionEmpresa || "";
  elements.systemNameInput.value = config.nombreSistema || "";
  elements.themeColorInput.value = config.temaColor || "";
  elements.companyLogoUrlInput.value = config.logoEmpresaUrl || "";
  elements.reportLogoUrlInput.value = config.logoSidebarUrl || "";
  elements.loginLogoUrlInput.value = config.logoLoginUrl || "";
  elements.hrManagerNameInput.value = config.nombreGerenteRrhh || "";
  elements.footerTextInput.value = config.textoFooter || "";
  elements.supportEmailInput.value = config.correoSoporte || "";
  elements.supportPhoneInput.value = config.telefonoSoporte || "";
  elements.showLoginLogoInput.checked = Boolean(config.mostrarLogoLogin);

  setLogoPreview(config.logoEmpresaUrl, elements.companyLogoPreviewShell, elements.companyLogoPreview);
  setLogoPreview(config.logoSidebarUrl, elements.reportLogoPreviewShell, elements.reportLogoPreview);
  setLogoPreview(config.logoLoginUrl, elements.loginLogoPreviewShell, elements.loginLogoPreview);
};

const renderSecurityForm = () => {
  const config = state.securityConfig || {};
  elements.securityAttemptsInput.value = config.intentosMaximos ?? 6;
  elements.securitySessionMinutesInput.value = config.minutosExpiracionSesion ?? 30;
  elements.securityRecoveryHoursInput.value = config.horasExpiracionRecuperacion ?? 24;
};

const renderPayrollForm = () => {
  const config = state.payrollConfig || {};
  elements.payrollRegimenInput.value = config.regimenInssEmpresa || "INTEGRAL";
  elements.payrollWorkersInput.value = config.cantidadTrabajadoresEmpresa ?? 1;
  elements.payrollPasantiaModeInput.value = config.modoPasantiaPorDefecto || "NO_NOMINA";
  elements.payrollDaysInput.value = config.diasMesNomina ?? 30;
  elements.payrollHoursInput.value = config.horasMesBase ?? 240;
};

const formatRate = (value) => {
  const numericValue = Number(value || 0);
  return Number.isFinite(numericValue) ? numericValue.toFixed(4) : "0.0000";
};

const renderExchangeRateConfiguration = () => {
  const config = state.exchangeConfig || {};
  const official = config.oficialActual || null;
  const institutional = config.institucionalActual || null;
  const batch = config.ultimoLoteOficial || null;
  const companyName = config.nombreComercial || config.razonSocial || "Empresa activa";

  elements.exchangeBaseCurrency.textContent = String(config.monedaBaseEmpresa || "NIO").toUpperCase();
  elements.exchangeCompanyName.textContent = companyName;
  elements.exchangeOfficialValue.textContent = formatRate(official?.valorTipoCambio);
  elements.exchangeOfficialMeta.textContent = official
    ? `${official.fechaTipoCambio} | ${official.fuente || "BCN"}`
    : "Sin fecha cargada";
  elements.exchangeOfficialBatchMeta.textContent = batch
    ? `${batch.nombreArchivo} | ${batch.fechaImportacion || "Sin fecha"} | ${batch.estadoLote}`
    : "Aun no hay importaciones registradas de tipo de cambio oficial.";
  elements.exchangeInstitutionalBuy.textContent = formatRate(institutional?.valorCompra);
  elements.exchangeInstitutionalBuyMeta.textContent = institutional
    ? `${institutional.fechaTipoCambio} | compra empresa`
    : "Sin fecha cargada";
  elements.exchangeInstitutionalSell.textContent = formatRate(institutional?.valorVenta);
  elements.exchangeInstitutionalSellMeta.textContent = institutional
    ? `${institutional.fechaTipoCambio} | venta empresa`
    : "Sin fecha cargada";

  elements.institutionalDateInput.value = institutional?.fechaTipoCambio || "";
  elements.institutionalReferenceInput.value =
    institutional?.valorReferencia != null ? String(institutional.valorReferencia) : "";
  elements.institutionalBuyInput.value =
    institutional?.valorCompra != null ? String(institutional.valorCompra) : "";
  elements.institutionalSellInput.value =
    institutional?.valorVenta != null ? String(institutional.valorVenta) : "";
  elements.institutionalObservationInput.value = institutional?.observacion || "";

  const officialHistory = Array.isArray(config.historialOficial) ? config.historialOficial : [];
  elements.officialRateHistoryEmpty.hidden = officialHistory.length > 0;
  elements.officialRateHistoryBody.innerHTML = officialHistory
    .map(
      (row) => `
        <tr>
          <td>${escapeHtml(row.fechaTipoCambio || "-")}</td>
          <td>${escapeHtml(formatRate(row.valorTipoCambio))}</td>
          <td>${escapeHtml(row.fuente || "BCN")}</td>
          <td>${escapeHtml(row.nombreArchivo || "-")}</td>
        </tr>
      `,
    )
    .join("");

  const institutionalHistory = Array.isArray(config.historialInstitucional)
    ? config.historialInstitucional
    : [];
  elements.institutionalRateHistoryEmpty.hidden = institutionalHistory.length > 0;
  elements.institutionalRateHistoryBody.innerHTML = institutionalHistory
    .map(
      (row) => `
        <tr>
          <td>${escapeHtml(row.fechaTipoCambio || "-")}</td>
          <td>${escapeHtml(row.valorCompra != null ? formatRate(row.valorCompra) : "-")}</td>
          <td>${escapeHtml(row.valorVenta != null ? formatRate(row.valorVenta) : "-")}</td>
          <td>${escapeHtml(row.valorReferencia != null ? formatRate(row.valorReferencia) : "-")}</td>
          <td>${escapeHtml(row.usuarioRegistro || "-")}</td>
        </tr>
      `,
    )
    .join("");
};

const renderPanels = () => {
  elements.generalPanel.hidden = state.activeTab !== "general";
  elements.reportPanel.hidden = state.activeTab !== "reportes";
  elements.securityPanel.hidden = state.activeTab !== "seguridad";
  elements.rrhhPanel.hidden = state.activeTab !== "rrhh";
  elements.payrollPanel.hidden = state.activeTab !== "nomina";
  elements.exchangePanel.hidden = state.activeTab !== "tipo-cambio";
  elements.conamiPanel.hidden = state.activeTab !== "conami";
  elements.creditProductsPanel.hidden = state.activeTab !== "creditos";
  elements.usersPanel.hidden = state.activeTab !== "usuarios";
  elements.accessPanel.hidden = state.activeTab !== "accesos";
  elements.movementPanel.hidden = state.activeTab !== "movimientos";

  elements.tabRow.querySelectorAll("[data-tab]").forEach((button) => {
    button.classList.toggle("is-active", button.dataset.tab === state.activeTab);
  });

  renderPanelSections();
};

const openModulesModal = () => {
  elements.modulesModal.hidden = false;
  document.body.style.overflow = "hidden";
};

const closeModulesModal = () => {
  if (state.moduleAccess.saving) {
    return;
  }

  elements.modulesModal.hidden = true;
  document.body.style.overflow = "";
  state.moduleAccess = {
    userId: null,
    loading: false,
    saving: false,
    username: "",
    fullName: "",
    roles: [],
    hasCustomConfiguration: false,
    modules: [],
  };
};

const renderModulesModal = () => {
  const { fullName, username, roles, hasCustomConfiguration, modules, loading, saving } =
    state.moduleAccess;

  elements.modulesModalTitle.textContent = fullName || "Accesos del usuario";
  elements.modulesModalNote.textContent = username
    ? `${username}${Array.isArray(roles) && roles.length ? ` - ${roles.join(", ")}` : ""}`
    : "Selecciona los modulos que estaran visibles en su dashboard.";

  elements.modulesModeBadge.textContent = hasCustomConfiguration ? "Personalizado" : "Automatico";
  elements.modulesModeBadge.className = hasCustomConfiguration
    ? "status-pill is-warning"
    : "status-pill is-success";
  elements.modulesModeHelp.textContent = hasCustomConfiguration
    ? "Este usuario ya tiene una configuracion manual y el dashboard usa solo lo marcado aqui."
    : "Si no personalizas accesos, el sistema toma los modulos por rol y jefatura.";

  if (loading) {
    elements.modulesGrid.innerHTML = `
      <div class="modules-group">
        <p class="empty-state">Cargando modulos del usuario...</p>
      </div>
    `;
    elements.modulesEmptyState.hidden = true;
    setModuleButtonsDisabled(true);
    return;
  }

  const groups = modules.reduce((accumulator, module) => {
    const groupKey = module.group || "General";
    accumulator[groupKey] = accumulator[groupKey] || [];
    accumulator[groupKey].push(module);
    return accumulator;
  }, {});

  const groupNames = Object.keys(groups);
  elements.modulesEmptyState.hidden = groupNames.length > 0;

  elements.modulesGrid.innerHTML = groupNames
    .map(
      (groupName) => `
        <section class="modules-group">
          <h4 class="modules-group-title">${escapeHtml(groupName)}</h4>
          <div class="modules-checklist">
            ${groups[groupName]
              .map(
                (module) => `
                  <label class="module-check">
                    <input
                      type="checkbox"
                      data-module-key="${escapeHtml(module.key)}"
                      ${module.selected ? "checked" : ""}
                    />
                    <span class="module-check-copy">
                      <span class="module-check-title">
                        <strong>${escapeHtml(module.name)}</strong>
                        <span class="module-code-pill">${escapeHtml(module.code)}</span>
                      </span>
                      <small>${escapeHtml(module.description || "Sin descripcion")}</small>
                    </span>
                  </label>
                `,
              )
              .join("")}
          </div>
        </section>
      `,
    )
    .join("");

  setModuleButtonsDisabled(saving);
};

const conamiBodiesByCategory = () => ({
  "riesgo-cliente": elements.conamiRulesClientBody,
  credito: elements.conamiRulesCreditBody,
  expediente: elements.conamiRulesFileBody,
  mora: elements.conamiRulesPortfolioBody,
  reportes: elements.conamiRulesReportsBody,
});

const normalizeConamiValue = (rule) => {
  if (!rule) {
    return "";
  }

  if (rule.value !== null && rule.value !== undefined) {
    return String(rule.value);
  }

  const type = String(rule.type || "").toUpperCase();
  if (type === "BOOLEANO") {
    return rule.booleanValue ? "true" : "false";
  }
  if (type === "DECIMAL") {
    return String(rule.decimalValue ?? 0);
  }
  if (type === "ENTERO") {
    return String(rule.integerValue ?? 0);
  }
  return rule.textValue || "";
};

const conamiValueControl = (rule) => {
  const value = normalizeConamiValue(rule);
  const disabled = rule.editable ? "" : "disabled";
  const code = escapeHtml(rule.code);
  const type = String(rule.type || "").toUpperCase();

  if (type === "BOOLEANO") {
    return `
      <select data-conami-value="${code}" ${disabled}>
        <option value="true" ${value === "true" ? "selected" : ""}>SI</option>
        <option value="false" ${value === "false" ? "selected" : ""}>NO</option>
      </select>
    `;
  }

  if (type === "DECIMAL" || type === "ENTERO") {
    return `<input data-conami-value="${code}" type="number" step="${type === "DECIMAL" ? "0.000001" : "1"}" value="${escapeHtml(value)}" ${disabled} />`;
  }

  return `<input data-conami-value="${code}" type="text" value="${escapeHtml(value)}" ${disabled} />`;
};

const renderConamiRules = () => {
  const config = state.conamiConfig || { norms: [], rules: [] };
  const bodies = conamiBodiesByCategory();

  Object.values(bodies).forEach((body) => {
    if (body) {
      body.innerHTML = "";
    }
  });

  elements.conamiNormsGrid.innerHTML = (config.norms || [])
    .map(
      (norm) => `
        <article class="conami-source-card">
          <span>${escapeHtml(norm.category || "CONAMI")}</span>
          <strong>${escapeHtml(norm.name || norm.code)}</strong>
          <small>${escapeHtml(norm.description || "Fuente normativa registrada.")}</small>
          ${
            norm.sourceUrl
              ? `<a href="${escapeHtml(norm.sourceUrl)}" target="_blank" rel="noreferrer">Ver fuente</a>`
              : ""
          }
        </article>
      `,
    )
    .join("");

  (config.rules || []).forEach((rule) => {
    const body = bodies[rule.category] || elements.conamiRulesReportsBody;
    if (!body) {
      return;
    }

    body.insertAdjacentHTML(
      "beforeend",
      `
        <tr data-conami-rule="${escapeHtml(rule.code)}">
          <td>
            <strong>${escapeHtml(rule.name)}</strong>
            <div class="muted">${escapeHtml(rule.description)}</div>
            <div class="muted">${escapeHtml(rule.code)} · ${escapeHtml(rule.normCode || "")}</div>
          </td>
          <td>${conamiValueControl(rule)}</td>
          <td>${escapeHtml(rule.type)}</td>
          <td><span class="status-pill ${rule.severity === "ALTA" ? "is-danger" : rule.severity === "MEDIA" ? "is-warning" : "is-success"}">${escapeHtml(rule.severity)}</span></td>
          <td>
            <label class="mini-check">
              <input type="checkbox" data-conami-active="${escapeHtml(rule.code)}" ${rule.active ? "checked" : ""} ${rule.editable ? "" : "disabled"} />
              <span>Activa</span>
            </label>
          </td>
        </tr>
      `,
    );
  });

  Object.entries(bodies).forEach(([, body]) => {
    if (body && !body.children.length) {
      body.innerHTML = '<tr><td colspan="5">No hay reglas en este cajon.</td></tr>';
    }
  });
};

const loadConamiRules = async (force = false) => {
  if (!force && state.conamiConfig) {
    renderConamiRules();
    return;
  }

  const payload = await sessionApi.request("/Seguridad/ReglasConami");
  state.conamiConfig = payload?.data || { norms: [], rules: [] };
  renderConamiRules();
};

const renderCreditProducts = () => {
  const products = Array.isArray(state.creditProducts) ? state.creditProducts : [];
  if (elements.creditProductsEmpty) {
    elements.creditProductsEmpty.hidden = products.length > 0;
  }

  if (!elements.creditProductsBody) {
    return;
  }

  elements.creditProductsBody.innerHTML = products
    .map(
      (product) => `
        <tr data-credit-product="${escapeHtml(product.code)}">
          <td>
            <div class="form-field">
              <span>${escapeHtml(product.code)}</span>
              <input data-product-field="name" value="${escapeHtml(product.name || "")}" maxlength="150" />
            </div>
          </td>
          <td>
            <select data-product-field="currency">
              ${["NIO", "USD"]
                .map((currency) => `<option value="${currency}" ${currency === product.currency ? "selected" : ""}>${currency}</option>`)
                .join("")}
            </select>
          </td>
          <td>
            <div class="field-grid">
              <input data-product-field="minAmount" type="number" min="0" step="0.01" value="${Number(product.minAmount || 0)}" />
              <input data-product-field="maxAmount" type="number" min="0" step="0.01" value="${Number(product.maxAmount || 0)}" />
            </div>
          </td>
          <td>
            <div class="field-grid">
              <input data-product-field="minTermMonths" type="number" min="1" step="1" value="${Number(product.minTermMonths || 1)}" />
              <input data-product-field="maxTermMonths" type="number" min="1" step="1" value="${Number(product.maxTermMonths || 1)}" />
            </div>
          </td>
          <td><input data-product-field="annualRate" type="number" min="0" max="200" step="0.000001" value="${Number(product.annualRate || 0)}" /></td>
          <td><input data-product-field="commissionRate" type="number" min="0" max="100" step="0.000001" value="${Number(product.commissionRate || 0)}" /></td>
          <td>
            <select data-product-field="frequency">
              ${["MENSUAL", "QUINCENAL", "SEMANAL", "DIARIO"]
                .map((frequency) => `<option value="${frequency}" ${frequency === product.frequency ? "selected" : ""}>${frequency}</option>`)
                .join("")}
            </select>
          </td>
          <td>
            <button class="table-action" type="button" data-save-credit-product="${escapeHtml(product.code)}">Guardar</button>
          </td>
        </tr>`,
    )
    .join("");
};

const loadCreditProducts = async (force = false) => {
  if (!force && state.creditProducts.length > 0) {
    renderCreditProducts();
    return;
  }

  const payload = await sessionApi.request("/SolicitudesCredito/ProductosCredito");
  state.creditProducts = Array.isArray(payload?.data) ? payload.data : [];
  renderCreditProducts();
};

const collectCreditProductPayload = (row) => {
  const original = state.creditProducts.find((item) => item.code === row.dataset.creditProduct) || {};
  const value = (field) => row.querySelector(`[data-product-field="${field}"]`)?.value ?? "";
  return {
    ...original,
    code: original.code || row.dataset.creditProduct,
    name: value("name").trim(),
    currency: value("currency"),
    minAmount: toNumber(value("minAmount"), 0),
    maxAmount: toNumber(value("maxAmount"), 0),
    minTermMonths: Math.trunc(toNumber(value("minTermMonths"), 1)),
    maxTermMonths: Math.trunc(toNumber(value("maxTermMonths"), 1)),
    annualRate: toNumber(value("annualRate"), 0),
    commissionRate: toNumber(value("commissionRate"), 0),
    frequency: value("frequency"),
    active: true,
  };
};

const saveCreditProduct = async (button) => {
  const row = button?.closest("[data-credit-product]");
  if (!row) {
    return;
  }

  const payload = collectCreditProductPayload(row);
  if (!payload.name || payload.annualRate < 0 || payload.commissionRate < 0) {
    showToast("Completa nombre, tasa anual y comision por desembolso.", "warning");
    return;
  }

  setButtonBusy(button, true, "Guardando...");
  try {
    const response = await sessionApi.request("/SolicitudesCredito/GuardarProductoCredito", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
    const saved = response?.data || payload;
    state.creditProducts = state.creditProducts.map((item) => (item.code === saved.code ? saved : item));
    renderCreditProducts();
    showToast(response?.message || "Tipo de credito actualizado.", "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo guardar el tipo de credito.", "danger");
  } finally {
    setButtonBusy(button, false, "Guardando...");
  }
};

const collectConamiRulesPayload = () => ({
  rules: Array.from(document.querySelectorAll("[data-conami-rule]")).map((row) => {
    const code = row.dataset.conamiRule;
    return {
      code,
      value: row.querySelector("[data-conami-value]")?.value || "",
      active: row.querySelector("[data-conami-active]")?.checked ?? true,
    };
  }),
});

const saveConamiRules = async () => {
  if (!elements.saveConamiRulesButton) {
    return;
  }

  setButtonBusy(elements.saveConamiRulesButton, true, "Guardando");
  try {
    const response = await sessionApi.request("/Seguridad/GuardarReglasConami", {
      method: "PUT",
      body: JSON.stringify(collectConamiRulesPayload()),
    });
    state.conamiConfig = response?.data || state.conamiConfig;
    renderConamiRules();
    showToast(response?.message || "Reglas CONAMI actualizadas.", "success");
  } catch (error) {
    showToast(error.message || "No se pudieron guardar las reglas CONAMI.", "danger");
  } finally {
    setButtonBusy(elements.saveConamiRulesButton, false);
  }
};

const loadGeneralConfiguration = async (force = false) => {
  if (!force && state.generalConfig) {
    renderGeneralAndReportForms();
    return;
  }

  const payload = await sessionApi.request("/Seguridad/ConfiguracionGeneral");
  state.generalConfig = payload?.data || {};
  renderGeneralAndReportForms();
};

const loadSecurityConfiguration = async (force = false) => {
  if (!force && state.securityConfig) {
    renderSecurityForm();
    return;
  }

  const payload = await sessionApi.request("/Seguridad/ParametrosSeguridad");
  state.securityConfig = payload?.data || {};
  renderSecurityForm();
};

const loadPayrollConfiguration = async (force = false) => {
  if (!force && state.payrollConfig) {
    renderPayrollForm();
    return;
  }

  const payload = await sessionApi.request("/Nomina/Contexto");
  state.payrollConfig = payload?.data?.config || {};
  renderPayrollForm();
};

const loadExchangeRateConfiguration = async (force = false) => {
  if (!force && state.exchangeConfig) {
    renderExchangeRateConfiguration();
    return;
  }

  const payload = await sessionApi.request("/Seguridad/TiposCambioConfiguracion");
  state.exchangeConfig = payload?.data || {};
  renderExchangeRateConfiguration();
};

const refreshView = async (tab = state.activeTab, force = false) => {
  if (state.loadingTab) {
    return false;
  }

  state.loadingTab = tab;
  setButtonsDisabled(true);

  try {
    if ((tab === "general" || tab === "reportes") && (force || !state.generalConfig)) {
      await loadGeneralConfiguration(force);
    }

    if (tab === "seguridad" && (force || !state.securityConfig)) {
      await loadSecurityConfiguration(force);
    }

    if (tab === "nomina" && (force || !state.payrollConfig)) {
      await loadPayrollConfiguration(force);
    }

    if (tab === "tipo-cambio" && (force || !state.exchangeConfig)) {
      await loadExchangeRateConfiguration(force);
    }

    if (tab === "conami" && (force || !state.conamiConfig)) {
      await loadConamiRules(force);
    }

    if (tab === "creditos" && (force || state.creditProducts.length === 0)) {
      await loadCreditProducts(force);
    }

    if (tab === "usuarios" && (force || state.users.length === 0)) {
      const payload = await sessionApi.request("/Seguridad/Usuarios");
      state.users = Array.isArray(payload?.data) ? payload.data : [];
      renderUsers();
    }

    if (tab === "accesos" && (force || state.accessRows.length === 0)) {
      const payload = await sessionApi.request("/Seguridad/BitacoraAcceso?take=120");
      state.accessRows = Array.isArray(payload?.data) ? payload.data : [];
      renderAccess();
    }

    if (tab === "movimientos" && (force || state.movementRows.length === 0)) {
      const payload = await sessionApi.request("/Seguridad/BitacoraMovimientos?take=120");
      state.movementRows = Array.isArray(payload?.data) ? payload.data : [];
      renderMovements();
    }

    renderMetrics();
    return true;
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return false;
    }

    if (requestError.status === 403) {
      showToast(
        requestError.message || "Tu usuario no tiene permisos para ver esta configuracion.",
        "danger",
      );
      return false;
    }

    showToast(requestError.message || "No se pudo cargar la informacion.", "danger");
    return false;
  } finally {
    state.loadingTab = null;
    setButtonsDisabled(false);
  }
};

const resetTemporaryPassword = async (idUsuario) => {
  const user = state.users.find((item) => Number(item.idUsuario) === Number(idUsuario));
  if (!user) {
    return;
  }

  const confirmed = window.confirm(
    `Se restablecera la clave temporal de ${user.usuario}. La clave sera su mismo usuario y debera cambiarla al ingresar.`,
  );

  if (!confirmed) {
    return;
  }

  setButtonsDisabled(true);

  try {
    const payload = await sessionApi.request(`/Seguridad/RestablecerClaveTemporal/${idUsuario}`, {
      method: "POST",
    });

    user.requiereCambioClave = true;
    user.bloqueado = false;
    renderUsers();
    renderMetrics();

    const usuario = payload?.data?.usuario || user.usuario;
    showToast(
      payload?.message ||
        `Clave temporal restablecida para ${usuario}. Debe cambiarla al ingresar.`,
      "success",
    );
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo restablecer la clave.", "danger");
  } finally {
    setButtonsDisabled(false);
  }
};

const unlockUser = async (idUsuario) => {
  const user = state.users.find((item) => Number(item.idUsuario) === Number(idUsuario));
  if (!user) {
    return;
  }

  const confirmed = window.confirm(
    `Se desbloqueara el usuario ${user.usuario} y se reiniciaran sus intentos fallidos. Deseas continuar?`,
  );

  if (!confirmed) {
    return;
  }

  setButtonsDisabled(true);

  try {
    const payload = await sessionApi.request(`/Seguridad/DesbloquearUsuario/${idUsuario}`, {
      method: "POST",
    });

    user.bloqueado = false;
    user.activo = true;
    renderUsers();
    renderMetrics();

    showToast(payload?.message || `Usuario ${user.usuario} desbloqueado.`, "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo desbloquear el usuario.", "danger");
  } finally {
    setButtonsDisabled(false);
  }
};

const loadModuleAccess = async (idUsuario) => {
  const user = state.users.find((item) => Number(item.idUsuario) === Number(idUsuario));
  if (!user) {
    return;
  }

  state.moduleAccess = {
    userId: Number(idUsuario),
    loading: true,
    saving: false,
    username: user.usuario || "",
    fullName: user.nombreCompleto || user.usuario || "",
    roles: String(user.roles || "")
      .split(",")
      .map((value) => value.trim())
      .filter(Boolean),
    hasCustomConfiguration: Boolean(user.tieneConfiguracionModulos),
    modules: [],
  };

  openModulesModal();
  renderModulesModal();

  try {
    const payload = await sessionApi.request(`/Seguridad/ModulosUsuario/${idUsuario}`);
    const data = payload?.data || {};

    state.moduleAccess = {
      userId: Number(data.idUsuario || idUsuario),
      loading: false,
      saving: false,
      username: data.usuario || user.usuario || "",
      fullName: data.nombreCompleto || user.nombreCompleto || user.usuario || "",
      roles: Array.isArray(data.roles) ? data.roles : [],
      hasCustomConfiguration: Boolean(data.hasCustomConfiguration),
      modules: Array.isArray(data.modules) ? data.modules : [],
    };

    renderModulesModal();
  } catch (requestError) {
    closeModulesModal();

    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo cargar la configuracion de modulos.", "danger");
  }
};

const getSelectedModuleKeys = () =>
  Array.from(elements.modulesGrid.querySelectorAll("[data-module-key]:checked")).map(
    (input) => input.dataset.moduleKey,
  );

const saveModuleAccess = async ({ useAutomatic = false } = {}) => {
  if (!state.moduleAccess.userId || state.moduleAccess.loading || state.moduleAccess.saving) {
    return;
  }

  state.moduleAccess.saving = true;
  renderModulesModal();

  try {
    const payload = await sessionApi.request(
      `/Seguridad/GuardarModulosUsuario/${state.moduleAccess.userId}`,
      {
        method: "PUT",
        body: JSON.stringify({
          moduleKeys: useAutomatic ? [] : getSelectedModuleKeys(),
          useAutomatic,
        }),
      },
    );

    const data = payload?.data || {};
    state.moduleAccess = {
      ...state.moduleAccess,
      saving: false,
      loading: false,
      hasCustomConfiguration: Boolean(data.hasCustomConfiguration),
      modules: Array.isArray(data.modules) ? data.modules : state.moduleAccess.modules,
    };

    const user = state.users.find(
      (item) => Number(item.idUsuario) === Number(state.moduleAccess.userId),
    );
    if (user) {
      user.tieneConfiguracionModulos = state.moduleAccess.hasCustomConfiguration;
      renderUsers();
      renderMetrics();
    }

    renderModulesModal();
    showToast(payload?.message || "Configuracion de modulos actualizada.", "success");
  } catch (requestError) {
    state.moduleAccess.saving = false;
    renderModulesModal();

    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudieron guardar los modulos.", "danger");
  }
};

const buildSystemConfigurationPayload = () => ({
  idEmpresa: Number(elements.generalCompanyId.value || 0),
  idConfiguracionGeneral: Number(elements.generalConfigId.value || 0),
  nombreSistema: elements.systemNameInput.value.trim(),
  temaColor: elements.themeColorInput.value.trim(),
  logoEmpresaUrl: elements.companyLogoUrlInput.value.trim(),
  logoSidebarUrl: elements.reportLogoUrlInput.value.trim(),
  logoLoginUrl: elements.loginLogoUrlInput.value.trim(),
  nombreGerenteRrhh: elements.hrManagerNameInput.value.trim(),
  textoFooter: elements.footerTextInput.value.trim(),
  correoSoporte: elements.supportEmailInput.value.trim(),
  telefonoSoporte: elements.supportPhoneInput.value.trim(),
  mostrarLogoLogin: elements.showLoginLogoInput.checked,
  razonSocial: elements.companyLegalNameInput.value.trim(),
  nombreComercial: elements.companyTradeNameInput.value.trim(),
  ruc: elements.companyRucInput.value.trim(),
  telefonoEmpresa: elements.companyPhoneInput.value.trim(),
  correoEmpresa: elements.companyEmailInput.value.trim(),
  direccionEmpresa: elements.companyAddressInput.value.trim(),
});

const saveSystemConfiguration = async (button, successMessage) => {
  const payload = buildSystemConfigurationPayload();
  if (!payload.nombreSistema || !payload.razonSocial || !payload.nombreComercial) {
    showToast(
      "Completa el nombre del sistema, la razon social y el nombre comercial antes de guardar.",
      "warning",
    );
    return;
  }

  setButtonBusy(button, true, "Guardando...");

  try {
    const response = await sessionApi.request("/Seguridad/GuardarConfiguracionGeneral", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
    state.generalConfig = response?.data || payload;
    renderGeneralAndReportForms();
    showToast(response?.message || successMessage, "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo guardar la configuracion general.", "danger");
  } finally {
    setButtonBusy(button, false, "Guardando...");
  }
};

const saveSecurityConfiguration = async () => {
  const payload = {
    intentosMaximos: Math.max(1, Math.trunc(toNumber(elements.securityAttemptsInput.value, 6))),
    minutosExpiracionSesion: Math.max(
      1,
      Math.trunc(toNumber(elements.securitySessionMinutesInput.value, 30)),
    ),
    horasExpiracionRecuperacion: Math.max(
      1,
      Math.trunc(toNumber(elements.securityRecoveryHoursInput.value, 24)),
    ),
  };

  setButtonBusy(elements.saveSecurityButton, true, "Guardando...");

  try {
    const response = await sessionApi.request("/Seguridad/GuardarParametrosSeguridad", {
      method: "PUT",
      body: JSON.stringify(payload),
    });
    state.securityConfig = response?.data || payload;
    renderSecurityForm();
    showToast(response?.message || "Parametros de seguridad actualizados.", "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudieron guardar los parametros de seguridad.", "danger");
  } finally {
    setButtonBusy(elements.saveSecurityButton, false, "Guardando...");
  }
};

const savePayrollConfiguration = async () => {
  const payload = {
    regimenInssEmpresa: elements.payrollRegimenInput.value,
    cantidadTrabajadoresEmpresa: toNumber(elements.payrollWorkersInput.value, 1),
    modoPasantiaPorDefecto: elements.payrollPasantiaModeInput.value,
    diasMesNomina: toNumber(elements.payrollDaysInput.value, 30),
    horasMesBase: toNumber(elements.payrollHoursInput.value, 240),
  };

  setButtonBusy(elements.savePayrollButton, true, "Guardando...");

  try {
    const response = await sessionApi.request("/Nomina/GuardarConfiguracionEmpresa", {
      method: "POST",
      body: JSON.stringify(payload),
    });
    state.payrollConfig = response?.data?.config || payload;
    renderPayrollForm();
    showToast(response?.message || "Parametros de nomina actualizados.", "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudieron guardar los parametros de nomina.", "danger");
  } finally {
    setButtonBusy(elements.savePayrollButton, false, "Guardando...");
  }
};

const importOfficialExchangeRates = async (files) => {
  if (!files?.length) {
    return;
  }

  const formData = new FormData();
  [...files].forEach((file) => {
    formData.append("archivos", file);
  });

  setButtonBusy(elements.uploadOfficialRateButton, true, "Importando...");

  try {
    const response = await sessionApi.request("/Seguridad/ImportarTipoCambioOficial", {
      method: "POST",
      body: formData,
    });

    state.exchangeConfig = response?.data?.contexto || response?.data || state.exchangeConfig;
    renderExchangeRateConfiguration();
    showToast(response?.message || "Tipo de cambio oficial importado correctamente.", "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo importar el tipo de cambio oficial.", "danger");
  } finally {
    setButtonBusy(elements.uploadOfficialRateButton, false, "Importando...");
  }
};

const saveInstitutionalExchangeRate = async () => {
  const payload = {
    fechaTipoCambio: elements.institutionalDateInput.value,
    valorCompra:
      elements.institutionalBuyInput.value.trim().length > 0
        ? toNumber(elements.institutionalBuyInput.value, 0)
        : null,
    valorVenta:
      elements.institutionalSellInput.value.trim().length > 0
        ? toNumber(elements.institutionalSellInput.value, 0)
        : null,
    valorReferencia:
      elements.institutionalReferenceInput.value.trim().length > 0
        ? toNumber(elements.institutionalReferenceInput.value, 0)
        : null,
    observacion: elements.institutionalObservationInput.value.trim(),
  };

  if (!payload.fechaTipoCambio) {
    showToast("Selecciona la fecha del tipo de cambio institucional.", "warning");
    return;
  }

  setButtonBusy(elements.saveInstitutionalRateButton, true, "Guardando...");

  try {
    const response = await sessionApi.request("/Seguridad/GuardarTipoCambioInstitucional", {
      method: "POST",
      body: JSON.stringify(payload),
    });

    state.exchangeConfig = response?.data || state.exchangeConfig;
    renderExchangeRateConfiguration();
    showToast(response?.message || "Tipo de cambio institucional guardado.", "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo guardar el tipo de cambio institucional.", "danger");
  } finally {
    setButtonBusy(elements.saveInstitutionalRateButton, false, "Guardando...");
  }
};

const handleLogoUploadResult = (target, url) => {
  if (target === "company") {
    const reportWasEmpty = !elements.reportLogoUrlInput.value.trim();
    elements.companyLogoUrlInput.value = url;
    if (reportWasEmpty) {
      elements.reportLogoUrlInput.value = url;
      setLogoPreview(url, elements.reportLogoPreviewShell, elements.reportLogoPreview);
    }
    setLogoPreview(url, elements.companyLogoPreviewShell, elements.companyLogoPreview);
    return;
  }

  if (target === "report") {
    elements.reportLogoUrlInput.value = url;
    setLogoPreview(url, elements.reportLogoPreviewShell, elements.reportLogoPreview);
    return;
  }

  elements.loginLogoUrlInput.value = url;
  setLogoPreview(url, elements.loginLogoPreviewShell, elements.loginLogoPreview);
};

const uploadLogo = async (file, target) => {
  const formData = new FormData();
  formData.append("archivo", file);
  formData.append("destino", target);

  try {
    const response = await sessionApi.request("/Seguridad/SubirLogoSistema", {
      method: "POST",
      body: formData,
    });
    const url = response?.data?.url || "";
    handleLogoUploadResult(target, url);
    showToast(response?.message || "Logo cargado correctamente.", "success");
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      redirectToLogin();
      return;
    }

    showToast(requestError.message || "No se pudo cargar la imagen del logo.", "danger");
  }
};

const changeTab = async (nextTab) => {
  if (!nextTab || !VALID_TABS.has(nextTab) || nextTab === state.activeTab) {
    return;
  }

  if (DEFAULT_PANEL_SECTIONS[nextTab] && !state.panelSections[nextTab]) {
    state.panelSections[nextTab] = DEFAULT_PANEL_SECTIONS[nextTab];
  }

  state.activeTab = nextTab;
  renderPanels();
  await refreshView(nextTab, false);
};

const bindEvents = () => {
  elements.backToDashboard?.addEventListener("click", () => {
    window.location.href = "/App/Dashboard";
  });

  elements.logoutButton?.addEventListener("click", async () => {
    await sessionApi.logout();
    redirectToLogin();
  });

  elements.refreshCurrentView?.addEventListener("click", async () => {
    const ok = await refreshView(state.activeTab, true);
    if (ok) {
      showToast("Vista actualizada.", "success");
    }
  });

  elements.tabRow?.addEventListener("click", async (event) => {
    const button = event.target.closest("[data-tab]");
    if (!button) {
      return;
    }

    await changeTab(button.dataset.tab);
  });

  document.querySelectorAll("[data-tab-jump]").forEach((button) => {
    button.addEventListener("click", async () => {
      await changeTab(button.dataset.tabJump);
    });
  });

  document.querySelectorAll("[data-section-scope][data-section-target]").forEach((button) => {
    button.addEventListener("click", () => {
      const scope = button.dataset.sectionScope;
      const target = button.dataset.sectionTarget;
      if (!scope || !target) {
        return;
      }

      state.panelSections[scope] = target;
      renderPanelSections();
    });
  });

  elements.usersTableBody?.addEventListener("click", async (event) => {
    const modulesButton = event.target.closest("[data-modules-user]");
    if (modulesButton) {
      await loadModuleAccess(modulesButton.dataset.modulesUser);
      return;
    }

    const unlockButton = event.target.closest("[data-unlock-user]");
    if (unlockButton && !unlockButton.disabled) {
      await unlockUser(unlockButton.dataset.unlockUser);
      return;
    }

    const resetButton = event.target.closest("[data-reset-user]");
    if (resetButton) {
      await resetTemporaryPassword(resetButton.dataset.resetUser);
    }
  });

  elements.creditProductsBody?.addEventListener("click", async (event) => {
    const saveButton = event.target.closest("[data-save-credit-product]");
    if (saveButton) {
      await saveCreditProduct(saveButton);
    }
  });

  elements.closeModulesModal?.addEventListener("click", closeModulesModal);

  elements.saveModulesButton?.addEventListener("click", async () => {
    await saveModuleAccess({ useAutomatic: false });
  });

  elements.restoreAutomaticModules?.addEventListener("click", async () => {
    const confirmed = window.confirm(
      "Se restaurara la configuracion automatica para este usuario segun sus roles y jefatura. Deseas continuar?",
    );

    if (!confirmed) {
      return;
    }

    await saveModuleAccess({ useAutomatic: true });
  });

  elements.saveGeneralButton?.addEventListener("click", async () => {
    await saveSystemConfiguration(
      elements.saveGeneralButton,
      "Configuracion general actualizada correctamente.",
    );
  });

  elements.saveReportButton?.addEventListener("click", async () => {
    await saveSystemConfiguration(
      elements.saveReportButton,
      "Branding y configuracion de reportes actualizados.",
    );
  });

  elements.saveSecurityButton?.addEventListener("click", saveSecurityConfiguration);
  elements.savePayrollButton?.addEventListener("click", savePayrollConfiguration);
  elements.saveInstitutionalRateButton?.addEventListener("click", saveInstitutionalExchangeRate);
  elements.saveConamiRulesButton?.addEventListener("click", saveConamiRules);

  elements.uploadCompanyLogoButton?.addEventListener("click", () => {
    elements.uploadCompanyLogoInput?.click();
  });

  elements.uploadReportLogoButton?.addEventListener("click", () => {
    elements.uploadReportLogoInput?.click();
  });

  elements.uploadLoginLogoButton?.addEventListener("click", () => {
    elements.uploadLoginLogoInput?.click();
  });

  elements.uploadOfficialRateButton?.addEventListener("click", () => {
    elements.officialRateUploadInput?.click();
  });

  elements.uploadCompanyLogoInput?.addEventListener("change", async (event) => {
    const file = event.target.files?.[0];
    if (file) {
      await uploadLogo(file, "company");
    }
    event.target.value = "";
  });

  elements.uploadReportLogoInput?.addEventListener("change", async (event) => {
    const file = event.target.files?.[0];
    if (file) {
      await uploadLogo(file, "report");
    }
    event.target.value = "";
  });

  elements.uploadLoginLogoInput?.addEventListener("change", async (event) => {
    const file = event.target.files?.[0];
    if (file) {
      await uploadLogo(file, "login");
    }
    event.target.value = "";
  });

  elements.officialRateUploadInput?.addEventListener("change", async (event) => {
    const files = event.target.files;
    if (files?.length) {
      await importOfficialExchangeRates(files);
    }
    event.target.value = "";
  });

  elements.companyLogoUrlInput?.addEventListener("input", () => {
    setLogoPreview(
      elements.companyLogoUrlInput.value,
      elements.companyLogoPreviewShell,
      elements.companyLogoPreview,
    );
  });

  elements.reportLogoUrlInput?.addEventListener("input", () => {
    setLogoPreview(
      elements.reportLogoUrlInput.value,
      elements.reportLogoPreviewShell,
      elements.reportLogoPreview,
    );
  });

  elements.loginLogoUrlInput?.addEventListener("input", () => {
    setLogoPreview(
      elements.loginLogoUrlInput.value,
      elements.loginLogoPreviewShell,
      elements.loginLogoPreview,
    );
  });
};

const boot = async () => {
  state.session = sessionApi.getSession();

  if (!state.session) {
    redirectToLogin();
    return;
  }

  if (!sessionApi.hasAnyRole(state.session, ADMIN_ROLES)) {
    showToast("Tu usuario no tiene acceso al modulo de configuracion.", "danger");
    window.setTimeout(() => {
      window.location.href = "/App/Dashboard";
    }, 800);
    return;
  }

  state.activeTab = resolveInitialTab();
  elements.sessionUser.textContent =
    state.session.displayName || state.session.user || "Usuario SIFNIC";
  elements.sessionMeta.textContent = `${state.session.rolesLabel || "Sin rol"} - ${sessionApi.formatDateTime(state.session.loginAt)}`;

  applyStaticDecorations();
  bindEvents();
  renderPanels();

  await refreshView("general", true);
  await refreshView("seguridad", true);
  await refreshView("nomina", true);
  await refreshView("tipo-cambio", true);
  await refreshView("conami", true);
  await refreshView("usuarios", true);
  await refreshView("accesos", true);
  await refreshView("movimientos", true);

  renderMetrics();
};

boot();
