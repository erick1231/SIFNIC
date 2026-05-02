const sessionApi = window.SifnicSession;
const ADMIN_ROLES = ["ADMINISTRADOR", "ADMINISTRACION"];

const modules = [
  {
    id: "rrhh",
    name: "Recursos Humanos",
    subtitle: "Gestion de personal, contratos y novedades",
    accent: "#1fd0bc",
    code: "RRHH",
    tags: ["Empleados", "Contratos"],
    route: "/App/Rrhh",
  },
  {
    id: "nomina",
    name: "Nomina",
    subtitle: "Planilla, periodos, esquelas y obligaciones",
    accent: "#f4be63",
    code: "NOM",
    tags: ["Planilla", "Esquelas"],
    route: "/App/Nomina",
    roles: ADMIN_ROLES,
  },
  {
    id: "configuracion",
    name: "Configuracion",
    subtitle: "Parametros, reportes, seguridad y usuarios",
    accent: "#65d7ff",
    code: "CFG",
    tags: ["Sistema", "Reportes"],
    route: "/App/Configuracion",
    roles: ADMIN_ROLES,
  },
  {
    id: "mi-portal",
    name: "Mi Portal",
    subtitle: "Mi ficha, vacaciones y horas extra",
    accent: "#6ff3be",
    code: "MIO",
    tags: ["Mi ficha", "Solicitudes"],
    route: "/App/MiPortal",
  },
  {
    id: "bandeja-supervisor",
    name: "Bandeja Supervisor",
    subtitle: "Aprobaciones de vacaciones y horas extra",
    accent: "#65d7ff",
    code: "SUP",
    tags: ["Pendientes", "Aprobacion"],
    route: "/App/BandejaSupervisor",
    roles: ["SUPERVISOR", "ADMINISTRADOR", "ADMINISTRACION", "JEFE_CREDITO"],
  },
  {
    id: "clientes",
    name: "Clientes",
    subtitle: "Relacion comercial y prospectos",
    accent: "#f4be63",
    code: "CLT",
    tags: ["Cliente", "Prospecto"],
    route: "/App/Clientes",
  },
  {
    id: "creditos",
    name: "Solicitudes de Credito",
    subtitle: "Evaluacion, expediente y plan de pago",
    accent: "#8ee887",
    code: "CRD",
    tags: ["Solicitud", "Plan"],
    route: "/App/SolicitudesCredito",
  },
  {
    id: "simulador-credito",
    name: "Simulador de Credito",
    subtitle: "Cuota, plan y nivel de endeudamiento",
    accent: "#46d3a8",
    code: "SIM",
    tags: ["Cuota", "Endeudamiento"],
    route: "/App/SimuladorCredito",
  },
  {
    id: "cobranza",
    name: "Cartera y Cobranza",
    subtitle: "Cartera asignada, mora y recuperacion",
    accent: "#ff9f6e",
    code: "CBR",
    tags: ["Cartera", "Mora", "Recuperacion"],
    route: "/App/Cartera",
    roles: ["ADMINISTRADOR", "ADMINISTRACION", "JEFE_CREDITO", "GERENTE_CREDITO", "OFICIAL_CREDITO", "CREDITO"],
  },
  {
    id: "caja",
    name: "Caja",
    subtitle: "Sesiones, movimientos y arqueos",
    accent: "#a2b5ff",
    code: "CAJ",
    tags: ["Arqueo", "Recibos"],
    route: "/App/Caja",
    roles: ["ADMINISTRADOR", "ADMINISTRACION", "CAJA", "CAJERO", "CREDITO", "OFICIAL_CREDITO", "JEFE_CREDITO", "GERENTE_CREDITO"],
  },
  {
    id: "bancos",
    name: "Bancos",
    subtitle: "Cuentas, movimientos y conciliacion",
    accent: "#46d3a8",
    code: "BNK",
    tags: ["Cuentas", "Transferencias"],
  },
  {
    id: "contabilidad",
    name: "Contabilidad",
    subtitle: "Asientos y control contable",
    accent: "#d1c278",
    code: "CTB",
    tags: ["Asientos", "Periodos"],
    route: "/App/Contabilidad",
    roles: ["ADMINISTRADOR", "ADMINISTRACION", "CONTABILIDAD", "JEFE_CREDITO", "GERENTE_CREDITO"],
  },
  {
    id: "cxc",
    name: "Cuentas por Cobrar",
    subtitle: "Cobros, anticipos y documentos",
    accent: "#7fc8ff",
    code: "CXC",
    tags: ["Cobros", "Documentos"],
  },
  {
    id: "cxp",
    name: "Cuentas por Pagar",
    subtitle: "Pagos y obligaciones",
    accent: "#f08ca0",
    code: "CXP",
    tags: ["Pagos", "Proveedores"],
  },
  {
    id: "inventario",
    name: "Inventario",
    subtitle: "Productos, categorias y bodegas",
    accent: "#ffd36f",
    code: "INV",
    tags: ["Productos", "Bodegas"],
  },
  {
    id: "captaciones",
    name: "Captaciones",
    subtitle: "Ahorros, depositos y movimientos",
    accent: "#87d9f4",
    code: "CAP",
    tags: ["Ahorros", "Captacion"],
  },
  {
    id: "cumplimiento",
    name: "Cumplimiento",
    subtitle: "Monitoreo regulatorio y KYC",
    accent: "#ffb38a",
    code: "KYC",
    tags: ["PLA", "Alertas"],
  },
  {
    id: "regulatorio",
    name: "Regulatorio",
    subtitle: "Cierre, provision y clasificacion",
    accent: "#c6b0ff",
    code: "REG",
    tags: ["Cierres", "Provision"],
  },
];

const sessionUser = document.getElementById("sessionUser");
const sessionMeta = document.getElementById("sessionMeta");
const logoutButton = document.getElementById("logoutButton");
const menuGrid = document.getElementById("menuGrid");
const menuModuleCount = document.getElementById("menuModuleCount");
const themeToggle = document.getElementById("themeToggle");
const themeToggleLabel = document.getElementById("themeToggleLabel");
const themeToggleHint = document.getElementById("themeToggleHint");
const approvalNotificationShell = document.getElementById("approvalNotificationShell");
const approvalNotificationButton = document.getElementById("approvalNotificationButton");
const approvalNotificationBadge = document.getElementById("approvalNotificationBadge");
const approvalNotificationPopover = document.getElementById("approvalNotificationPopover");
const approvalNotificationSummary = document.getElementById("approvalNotificationSummary");
const approvalNotificationItems = document.getElementById("approvalNotificationItems");
const approvalNotificationNote = document.getElementById("approvalNotificationNote");
const approvalNotificationLink = document.getElementById("approvalNotificationLink");
const approvalNotificationTotal = document.getElementById("approvalNotificationTotal");
const categoryFilter = document.getElementById("categoryFilter");
const dashboardSearch = document.getElementById("dashboardSearch");
const quickAccessGrid = document.getElementById("quickAccessGrid");
const welcomeTitle = document.getElementById("welcomeTitle");
const operationalDate = document.getElementById("operationalDate");
const operationalBranch = document.getElementById("operationalBranch");
const collapseSidebarButton = document.getElementById("collapseSidebarButton");
const mobileMenuButton = document.getElementById("mobileMenuButton");
const dashboardSidebar = document.getElementById("dashboardSidebar");

let activeModuleId = null;
let currentSession = null;
let allowedModuleIds = null;
let moduleAccessLoaded = false;
let notificationPopoverOpen = false;
let supervisorNotificationRefreshHandle = null;
let lastSupervisorPendingTotal = null;
let lastSupervisorNotificationItems = [];

const categoryById = {
  clientes: "Operacion comercial",
  creditos: "Operacion comercial",
  "simulador-credito": "Operacion comercial",
  cobranza: "Operacion comercial",
  caja: "Operacion comercial",
  bancos: "Operacion financiera",
  cxc: "Operacion financiera",
  cxp: "Operacion financiera",
  contabilidad: "Operacion financiera",
  captaciones: "Operacion financiera",
  cumplimiento: "Control y cumplimiento",
  regulatorio: "Control y cumplimiento",
  "bandeja-supervisor": "Control y cumplimiento",
  configuracion: "Control y cumplimiento",
  rrhh: "Gestion interna y soporte",
  nomina: "Gestion interna y soporte",
  "mi-portal": "Gestion interna y soporte",
  inventario: "Gestion interna y soporte",
};

const iconById = {
  clientes: "users",
  creditos: "file",
  "simulador-credito": "calc",
  cobranza: "pie",
  caja: "cash",
  bancos: "bank",
  cxc: "invoice",
  cxp: "pay",
  contabilidad: "ledger",
  captaciones: "wallet",
  cumplimiento: "shield",
  regulatorio: "check",
  "bandeja-supervisor": "tray",
  configuracion: "gear",
  rrhh: "people",
  nomina: "coin",
  "mi-portal": "user",
  inventario: "box",
};
const categoryOrder = [
  "Operacion comercial",
  "Operacion financiera",
  "Control y cumplimiento",
  "Gestion interna y soporte",
  "Otros",
];

const getRoleBasedModules = () =>
  modules.filter((module) => !module.roles || sessionApi.hasAnyRole(currentSession, module.roles));

const getVisibleModules = () => {
  if (moduleAccessLoaded && allowedModuleIds instanceof Set) {
    return modules.filter((module) => allowedModuleIds.has(module.id));
  }

  return getRoleBasedModules();
};

const filteredModules = () => {
  const selectedCategory = categoryFilter?.value || "TODOS";
  const query = String(dashboardSearch?.value || "").trim().toLowerCase();
  return getVisibleModules().filter((module) => {
    const category = categoryById[module.id] || "Otros";
    const matchesCategory = selectedCategory === "TODOS" || selectedCategory === category;
    const haystack = [module.name, module.subtitle, module.code, category, ...(module.tags || [])]
      .join(" ")
      .toLowerCase();
    return matchesCategory && (!query || haystack.includes(query));
  });
};

const renderQuickAccess = () => {
  if (!quickAccessGrid) return;
  const preferred = ["caja", "clientes", "creditos", "cobranza"];
  const visible = getVisibleModules();
  const items = preferred
    .map((id) => visible.find((module) => module.id === id))
    .filter(Boolean)
    .slice(0, 4);
  quickAccessGrid.innerHTML = items.length
    ? items
        .map(
          (module) => `
            <a class="quick-access-item" href="${escapeHtml(module.route || "#")}">
              <span data-icon="${escapeHtml(iconById[module.id] || "box")}"></span>
              <strong>${escapeHtml(module.name.replace("Solicitudes de Credito", "Solicitudes").replace("Cartera y Cobranza", "Cartera"))}</strong>
            </a>`,
        )
        .join("")
    : '<span class="dashboard-empty">Sin accesos frecuentes.</span>';
};

const syncSidebarAccess = () => {
  const visibleIds = new Set(getVisibleModules().map((module) => module.id));
  document.querySelectorAll("[data-module-nav]").forEach((link) => {
    const id = link.dataset.moduleNav;
    if (id === "dashboard") return;
    link.hidden = !visibleIds.has(id);
  });
};

const renderMenu = () => {
  const visibleModules = filteredModules();
  const allVisible = getVisibleModules();

  if (menuModuleCount) {
    menuModuleCount.textContent = `${allVisible.length} modulo${allVisible.length === 1 ? "" : "s"} habilitado${allVisible.length === 1 ? "" : "s"}`;
  }

  if (!visibleModules.length) {
    menuGrid.innerHTML = `
      <article class="menu-card menu-card-empty">
        <div class="menu-card-body">
          <strong>Sin modulos disponibles</strong>
          <small>Tu usuario no tiene accesos visibles en el panel principal. Consulta con administracion.</small>
        </div>
      </article>
    `;
    return;
  }

  const grouped = visibleModules.reduce((acc, module) => {
    const category = categoryById[module.id] || "Otros";
    if (!acc.has(category)) acc.set(category, []);
    acc.get(category).push(module);
    return acc;
  }, new Map());

  menuGrid.innerHTML = categoryOrder
    .filter((category) => grouped.has(category))
    .map(
      (category) => {
        const items = grouped.get(category) || [];
        return `
        <section class="module-category">
          <h3>${escapeHtml(category)}</h3>
          <div class="module-card-grid">
            ${items
              .map(
                (module) => `
                  <button class="menu-card${module.id === activeModuleId ? " is-active" : ""}" data-module-id="${module.id}" type="button">
                    <span class="module-icon" data-icon="${escapeHtml(iconById[module.id] || "box")}"></span>
                    <div class="menu-card-body">
                      <strong>${escapeHtml(module.name)}</strong>
                      <small>${escapeHtml(module.subtitle)}</small>
                    </div>
                    <span class="menu-code">${escapeHtml(module.code)}</span>
                    <div class="menu-tags">
                      ${(module.tags || []).slice(0, 2).map((tag) => `<span>${escapeHtml(tag)}</span>`).join("")}
                    </div>
                  </button>`,
              )
              .join("")}
          </div>
        </section>`;
      },
    )
    .join("");

  menuGrid.querySelectorAll("[data-module-id]").forEach((button) => {
    button.addEventListener("click", () => {
      const module = visibleModules.find((item) => item.id === button.dataset.moduleId);

      if (module?.route) {
        window.location.href = module.route;
        return;
      }

      activeModuleId = button.dataset.moduleId;
      renderMenu();
      renderActiveModule();
    });
  });
};

const renderActiveModule = () => {
  const module = getVisibleModules().find((item) => item.id === activeModuleId);
  document.documentElement.style.setProperty("--module-accent", module?.accent || "#1fd0bc");
};

const hideNotificationPopover = () => {
  notificationPopoverOpen = false;

  if (approvalNotificationPopover) {
    approvalNotificationPopover.hidden = true;
  }

  if (approvalNotificationButton) {
    approvalNotificationButton.setAttribute("aria-expanded", "false");
  }
};

const renderSupervisorNotifications = (payload) => {
  if (!approvalNotificationShell) {
    return;
  }

  const data = payload || {};
  const available = Boolean(data.available);

  approvalNotificationShell.hidden = !available;
  if (!available) {
    hideNotificationPopover();
    return;
  }

  const counts = data.counts || {};
  const totalPending = Number(data.totalPending || 0);
  const previewItems = Array.isArray(data.items) ? data.items : [];
  lastSupervisorNotificationItems = previewItems;
  const items = [
    { label: "Vacaciones", value: Number(counts.pendingVacations || 0) },
    { label: "Horas extra", value: Number(counts.pendingOvertime || 0) },
  ];

  if (approvalNotificationBadge) {
    approvalNotificationBadge.hidden = totalPending <= 0;
    approvalNotificationBadge.textContent = String(totalPending);
  }

  if (approvalNotificationButton) {
    approvalNotificationButton.classList.toggle("has-pending", totalPending > 0);

    const hasIncrease =
      lastSupervisorPendingTotal !== null && Number(totalPending) > Number(lastSupervisorPendingTotal);

    if (hasIncrease) {
      approvalNotificationButton.classList.remove("is-pulse");
      window.requestAnimationFrame(() => {
        approvalNotificationButton.classList.add("is-pulse");
      });
      window.setTimeout(() => {
        approvalNotificationButton?.classList.remove("is-pulse");
      }, 2200);
    }
  }

  if (approvalNotificationTotal) {
    approvalNotificationTotal.textContent = `${totalPending} pendiente${totalPending === 1 ? "" : "s"}`;
  }

  if (approvalNotificationSummary) {
    approvalNotificationSummary.innerHTML = items
      .map(
        (item) => `
          <article class="notification-stat">
            <strong>${item.value}</strong>
            <span>${item.label}</span>
          </article>
        `,
      )
      .join("");
  }

  if (approvalNotificationItems) {
    approvalNotificationItems.innerHTML = previewItems.length
      ? previewItems
          .map(
            (item) => `
              <button
                class="notification-item"
                type="button"
                data-request-type="${escapeHtml(item.type || "")}"
                data-request-id="${escapeHtml(item.requestId || "")}">
                <div class="notification-item-top">
                  <span class="notification-item-type notification-item-type-${String(item.type || "").toLowerCase()}">${escapeHtml(item.typeLabel || item.type || "Pendiente")}</span>
                  <time>${formatNotificationDate(item.requestDate)}</time>
                </div>
                <strong>${escapeHtml(item.employeeCode || "")} - ${escapeHtml(item.employeeName || "")}</strong>
                <span>${escapeHtml(item.summary || item.positionName || "")}</span>
              </button>
            `,
          )
          .join("")
      : '<p class="notification-empty">No hay solicitudes pendientes por mostrar.</p>';
  }

  if (approvalNotificationNote) {
    approvalNotificationNote.textContent =
      data.note || "Solo se muestran solicitudes de los colaboradores que te reportan directamente.";
  }

  if (approvalNotificationLink) {
    const firstItem = previewItems[0] || null;
    approvalNotificationLink.href = firstItem
      ? buildSupervisorApprovalUrl(firstItem)
      : "/App/BandejaSupervisor";
  }

  lastSupervisorPendingTotal = totalPending;
};

const buildSupervisorApprovalUrl = (item) => {
  const type = String(item?.type || "").trim().toUpperCase();
  const requestId = Number(item?.requestId || 0);

  if (!type || !(requestId > 0)) {
    return "/App/BandejaSupervisor";
  }

  return `/App/BandejaSupervisor?kind=${encodeURIComponent(type)}&id=${encodeURIComponent(String(requestId))}`;
};

const escapeHtml = (value) =>
  String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");

const formatNotificationDate = (value) => {
  if (!value) {
    return "Sin fecha";
  }

  try {
    return new Intl.DateTimeFormat("es-NI", {
      day: "2-digit",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
      timeZone: "America/Managua",
    }).format(new Date(value));
  } catch {
    return String(value);
  }
};

const loadSupervisorNotifications = async () => {
  try {
    const payload = await sessionApi.request("/Portal/SupervisorNotificaciones");
    renderSupervisorNotifications(payload?.data || null);
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      window.location.href = "/App/Login";
      return;
    }

    if (approvalNotificationShell) {
      approvalNotificationShell.hidden = true;
    }
  }
};

const startSupervisorNotificationRefresh = () => {
  if (supervisorNotificationRefreshHandle || !approvalNotificationShell) {
    return;
  }

  supervisorNotificationRefreshHandle = window.setInterval(() => {
    if (!document.hidden) {
      loadSupervisorNotifications();
    }
  }, 20000);
};

const loadModuleAccess = async () => {
  const sessionModules = Array.isArray(currentSession?.modules)
    ? currentSession.modules
    : [];

  if (sessionModules.length) {
    allowedModuleIds = new Set(sessionModules.map((value) => String(value || "").toLowerCase()));
  }

  try {
    const payload = await sessionApi.request("/Seguridad/MisModulosDashboard");
    const modulesFromApi = Array.isArray(payload?.data?.modules) ? payload.data.modules : [];
    allowedModuleIds = new Set(modulesFromApi.map((value) => String(value || "").toLowerCase()));
    moduleAccessLoaded = true;
  } catch (requestError) {
    if (requestError.status === 401) {
      await sessionApi.logout();
      window.location.href = "/App/Login";
      return false;
    }

    moduleAccessLoaded = false;
    if (!(allowedModuleIds instanceof Set)) {
      allowedModuleIds = null;
    }
  }

  return true;
};

const bootDashboard = async () => {
  currentSession = sessionApi.getSession();

  if (!currentSession) {
    window.location.href = "/App/Login";
    return;
  }

  const rolesLabel = currentSession.rolesLabel || "Sin rol";
  sessionUser.textContent = currentSession.displayName || currentSession.user || "Usuario SIFNIC";
  sessionMeta.textContent = rolesLabel;
  if (welcomeTitle) {
    const firstName = String(currentSession.displayName || currentSession.user || "Usuario").split(" ")[0];
    welcomeTitle.textContent = `Bienvenido, ${firstName}`;
  }
  if (operationalDate) operationalDate.textContent = new Intl.DateTimeFormat("es-NI", { day: "2-digit", month: "2-digit", year: "numeric", timeZone: "America/Managua" }).format(new Date());
  if (operationalBranch) operationalBranch.textContent = currentSession.branchName || currentSession.branch || "Casa Matriz";

  window.SifnicTheme?.attachToggle(themeToggle, themeToggleLabel, themeToggleHint);

  const accessOk = await loadModuleAccess();
  if (accessOk === false) {
    return;
  }

  renderMenu();
  renderQuickAccess();
  syncSidebarAccess();
  renderActiveModule();
  await loadSupervisorNotifications();
  startSupervisorNotificationRefresh();
};

logoutButton?.addEventListener("click", async () => {
  logoutButton.disabled = true;

  try {
    await sessionApi.logout();
  } finally {
    window.location.href = "/App/Login";
  }
});

categoryFilter?.addEventListener("change", renderMenu);
dashboardSearch?.addEventListener("input", renderMenu);
dashboardSearch?.addEventListener("keydown", (event) => {
  if (event.key !== "Enter") return;
  const first = menuGrid?.querySelector("[data-module-id]");
  first?.click();
});
document.addEventListener("keydown", (event) => {
  if ((event.ctrlKey || event.metaKey) && String(event.key).toLowerCase() === "k") {
    event.preventDefault();
    dashboardSearch?.focus();
  }
});
collapseSidebarButton?.addEventListener("click", () => {
  document.body.classList.toggle("sidebar-collapsed");
});
mobileMenuButton?.addEventListener("click", () => {
  dashboardSidebar?.classList.toggle("is-open");
});

approvalNotificationButton?.addEventListener("click", (event) => {
  event.stopPropagation();

  const firstItem = Array.isArray(lastSupervisorNotificationItems)
    ? lastSupervisorNotificationItems[0]
    : null;

  if (firstItem) {
    window.location.href = buildSupervisorApprovalUrl(firstItem);
    return;
  }

  notificationPopoverOpen = !notificationPopoverOpen;
  if (approvalNotificationPopover) {
    approvalNotificationPopover.hidden = !notificationPopoverOpen;
  }

  approvalNotificationButton.setAttribute("aria-expanded", notificationPopoverOpen ? "true" : "false");
});

approvalNotificationItems?.addEventListener("click", (event) => {
  const itemButton = event.target.closest("[data-request-type][data-request-id]");
  if (!itemButton) {
    return;
  }

  const type = String(itemButton.dataset.requestType || "").trim().toUpperCase();
  const requestId = Number(itemButton.dataset.requestId || 0);
  if (!type || !(requestId > 0)) {
    return;
  }

  window.location.href = buildSupervisorApprovalUrl({
    type,
    requestId,
  });
});

document.addEventListener("click", (event) => {
  if (!notificationPopoverOpen) {
    return;
  }

  if (
    approvalNotificationShell &&
    event.target instanceof Node &&
    !approvalNotificationShell.contains(event.target)
  ) {
    hideNotificationPopover();
  }
});

bootDashboard();
