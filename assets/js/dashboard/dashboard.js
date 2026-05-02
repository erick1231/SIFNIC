const SESSION_KEY = "sifnic.session";

const modules = [
  {
    id: "rrhh",
    name: "Recursos Humanos",
    subtitle: "Gestión de personal y nómina",
    accent: "#1fd0bc",
    code: "RRHH",
    tags: ["Empleados", "Nómina"],
    route: "modules/rrhh/index.html",
  },
  {
    id: "clientes",
    name: "Clientes",
    subtitle: "Relación comercial y prospectos",
    accent: "#f4be63",
    code: "CLT",
    tags: ["Cliente", "Prospecto"],
  },
  {
    id: "configuracion",
    name: "Configuración",
    subtitle: "Parámetros generales del sistema",
    accent: "#65d7ff",
    code: "CFG",
    tags: ["Moneda", "Secuencias"],
  },
  {
    id: "creditos",
    name: "Créditos",
    subtitle: "Colocación, pagos y expedientes",
    accent: "#8ee887",
    code: "CRD",
    tags: ["Solicitud", "Desembolso"],
  },
  {
    id: "cobranza",
    name: "Cobranza",
    subtitle: "Seguimiento y recuperación",
    accent: "#ff9f6e",
    code: "CBR",
    tags: ["Gestión", "Promesas"],
  },
  {
    id: "caja",
    name: "Caja",
    subtitle: "Sesiones, movimientos y arqueos",
    accent: "#a2b5ff",
    code: "CAJ",
    tags: ["Arqueo", "Recibos"],
  },
  {
    id: "bancos",
    name: "Bancos",
    subtitle: "Cuentas, movimientos y conciliación",
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
    tags: ["Asientos", "Períodos"],
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
    subtitle: "Productos, categorías y bodegas",
    accent: "#ffd36f",
    code: "INV",
    tags: ["Productos", "Bodegas"],
  },
  {
    id: "captaciones",
    name: "Captaciones",
    subtitle: "Ahorros, depósitos y movimientos",
    accent: "#87d9f4",
    code: "CAP",
    tags: ["Ahorros", "Captación"],
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
    subtitle: "Cierre, provisión y clasificación",
    accent: "#c6b0ff",
    code: "REG",
    tags: ["Cierres", "Provisión"],
  },
];

const sessionUser = document.getElementById("sessionUser");
const sessionMeta = document.getElementById("sessionMeta");
const logoutButton = document.getElementById("logoutButton");
const menuGrid = document.getElementById("menuGrid");
const activeModuleIndicator = document.getElementById("activeModuleIndicator");

let activeModuleId = modules[0].id;

const getSession = () => {
  try {
    const session = JSON.parse(localStorage.getItem(SESSION_KEY) || "null");
    return session && session.active ? session : null;
  } catch {
    return null;
  }
};

const formatSessionDate = (value) => {
  if (!value) {
    return "Sin registro reciente";
  }

  return new Intl.DateTimeFormat("es-NI", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "America/Managua",
  }).format(new Date(value));
};

const renderMenu = () => {
  menuGrid.innerHTML = modules
    .map(
      (module) => `
        <button class="menu-card${
          module.id === activeModuleId ? " is-active" : ""
        }" data-module-id="${module.id}" type="button">
          <div class="menu-card-top">
            <span class="menu-dot" style="background:${module.accent}"></span>
            <span class="menu-code">${module.code}</span>
          </div>

          <div class="menu-card-body">
            <strong>${module.name}</strong>
            <small>${module.subtitle}</small>
          </div>

          <div class="menu-tags">
            ${module.tags.map((tag) => `<span>${tag}</span>`).join("")}
          </div>
        </button>
      `,
    )
    .join("");

  menuGrid.querySelectorAll("[data-module-id]").forEach((button) => {
    button.addEventListener("click", () => {
      const module = modules.find((item) => item.id === button.dataset.moduleId);

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
  const module = modules.find((item) => item.id === activeModuleId) || modules[0];
  document.documentElement.style.setProperty("--module-accent", module.accent);
  activeModuleIndicator.textContent = module.name;
};

const bootDashboard = () => {
  const session = getSession();

  if (!session) {
    window.location.href = "index.html";
    return;
  }

  sessionUser.textContent = session.user || "Usuario SIFNIC";
  sessionMeta.textContent = `Acceso: ${formatSessionDate(session.loginAt)}`;

  renderMenu();
  renderActiveModule();
};

logoutButton?.addEventListener("click", () => {
  localStorage.removeItem(SESSION_KEY);
  window.location.href = "index.html";
});

bootDashboard();
