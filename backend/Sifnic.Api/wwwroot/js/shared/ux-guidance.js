(function () {
  "use strict";

  const ICONS = {
    "arrow-left": '<path d="M19 12H5"/><path d="m12 19-7-7 7-7"/>',
    "arrow-right": '<path d="M5 12h14"/><path d="m12 5 7 7-7 7"/>',
    "moon": '<path d="M12 3a6 6 0 0 0 9 7.4A9 9 0 1 1 12 3Z"/>',
    "log-out": '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="M16 17l5-5-5-5"/><path d="M21 12H9"/>',
    "plus": '<path d="M12 5v14"/><path d="M5 12h14"/>',
    "refresh": '<path d="M21 12a9 9 0 0 1-15.5 6.2"/><path d="M3 12A9 9 0 0 1 18.5 5.8"/><path d="M18 2v4h-4"/><path d="M6 22v-4h4"/>',
    "search": '<circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/>',
    "save": '<path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2Z"/><path d="M17 21v-8H7v8"/><path d="M7 3v5h8"/>',
    "x": '<path d="M18 6 6 18"/><path d="m6 6 12 12"/>',
    "trash": '<path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/>',
    "pencil": '<path d="M12 20h9"/><path d="m16.5 3.5 4 4L7 21H3v-4L16.5 3.5Z"/>',
    "eye": '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/>',
    "file-plus": '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6"/><path d="M12 12v6"/><path d="M9 15h6"/>',
    "file-text": '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6"/><path d="M8 13h8"/><path d="M8 17h6"/>',
    "table": '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 10h18"/><path d="M9 4v16"/><path d="M15 4v16"/>',
    "printer": '<path d="M6 9V3h12v6"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><path d="M6 14h12v8H6z"/>',
    "wallet": '<path d="M19 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-2"/><path d="M16 12h6v5h-6a2.5 2.5 0 0 1 0-5Z"/>',
    "calculator": '<rect x="4" y="2" width="16" height="20" rx="2"/><path d="M8 6h8"/><path d="M8 10h.01"/><path d="M12 10h.01"/><path d="M16 10h.01"/><path d="M8 14h.01"/><path d="M12 14h.01"/><path d="M16 14h.01"/><path d="M8 18h.01"/><path d="M12 18h.01"/><path d="M16 18h.01"/>',
    "check": '<path d="M20 6 9 17l-5-5"/>',
    "calendar": '<path d="M8 2v4"/><path d="M16 2v4"/><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M3 10h18"/>',
    "send": '<path d="m22 2-7 20-4-9-9-4 20-7Z"/><path d="M22 2 11 13"/>',
    "user": '<path d="M20 21a8 8 0 0 0-16 0"/><circle cx="12" cy="7" r="4"/>',
    "settings": '<path d="M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6V21h-4v-.1a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1-2.8-2.8.1-.1A1.7 1.7 0 0 0 4.6 15a1.7 1.7 0 0 0-1.6-1H3v-4h.1a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.3-1.9l-.1-.1L7.1 4.2l.1.1a1.7 1.7 0 0 0 1.9.3 1.7 1.7 0 0 0 1-1.6V3h4v.1a1.7 1.7 0 0 0 1 1.6 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.9 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.6 1h.1v4h-.1a1.7 1.7 0 0 0-1.7 1Z"/>'
  };

  const BUTTON_ICON_RULES = [
    [/volver|panel principal|anterior/, "arrow-left"],
    [/cerrar sesion|salir/, "log-out"],
    [/modo oscuro|modo claro/, "moon"],
    [/nuevo|nueva|agregar|crear/, "plus"],
    [/actualizar|refrescar|limpiar/, "refresh"],
    [/buscar|consulta/, "search"],
    [/guardar|procesar|confirmar/, "save"],
    [/cancelar|cerrar$/, "x"],
    [/eliminar|anular|rechazar|descartar/, "trash"],
    [/editar/, "pencil"],
    [/ver|detalle/, "eye"],
    [/solicitud/, "file-plus"],
    [/pdf|exp\./, "file-text"],
    [/excel|archivo/, "table"],
    [/imprimir|voucher|copia|recibo/, "printer"],
    [/cerrar caja/, "x"],
    [/abrir caja|caja/, "wallet"],
    [/arqueo|cuadre/, "calculator"],
    [/cobrar|aplicar|aprobar|ok/, "check"],
    [/plan|calendario/, "calendar"],
    [/desembolsar|transferencia/, "send"],
    [/usuario|cliente|empleado|capital humano|rrhh|mi ficha/, "user"],
    [/reporte|normativa|conami|muc|expediente/, "file-text"],
    [/nomina|planilla|periodo|calculo/, "calculator"],
    [/tipo de cambio|moneda|banco|saldo/, "wallet"],
    [/bitacora|movimiento|listado/, "table"],
    [/credito|prestamo|solicitud/, "file-plus"],
    [/general|seguridad|configuracion|parametro|sistema/, "settings"]
  ];

  const MODULE_ICONS = {
    rrhh: "user",
    nomina: "calculator",
    configuracion: "settings",
    portal: "user",
    supervisor: "check",
    clientes: "user",
    solicitudes: "file-plus",
    cobranza: "wallet",
    caja: "wallet",
    bancos: "table",
    contabilidad: "calculator",
    cxc: "file-text",
    cartera: "file-text",
    simulador: "calculator"
  };

  function normalize(value) {
    return String(value || "")
      .toLowerCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/\s+/g, " ")
      .trim();
  }

  function collectMatches(root, selector) {
    const matches = [];
    if (root instanceof Element && root.matches(selector)) matches.push(root);
    if (root.querySelectorAll) matches.push(...root.querySelectorAll(selector));
    return matches;
  }

  function addFieldGuidance(root) {
    const selector = [
      ".field",
      ".form-field",
      ".select-field",
      ".search-field",
      ".check-field",
      ".checkbox-field",
      ".switch-field",
      ".inline-field",
      ".inline-check",
      ".remember-option",
      ".toggle-card"
    ].join(",");
    collectMatches(root, selector).forEach((field) => {
      if (field.dataset.uxGuided === "true") return;
      const control = field.querySelector("input, select, textarea");
      if (!control) return;

      field.dataset.uxGuided = "true";
      enhanceControl(control, field);
    });
  }

  function enhanceControl(control, field) {
    if (!control) return;
    const labelText = field.querySelector(":scope > span")?.textContent || "";
    const key = normalize([labelText, control.id, control.name, control.placeholder].filter(Boolean).join(" "));
    const tag = control.tagName.toLowerCase();
    const type = normalize(control.getAttribute("type") || "text");

    if (tag === "input" && ["number"].includes(type)) {
      control.setAttribute("inputmode", "decimal");
    }

    if (tag === "input" && !control.getAttribute("autocomplete")) {
      if (/correo|email/.test(key)) control.setAttribute("autocomplete", "email");
      else if (/telefono|celular/.test(key)) control.setAttribute("autocomplete", "tel");
      else if (/nombre|cliente|empleado|abonante/.test(key)) control.setAttribute("autocomplete", "name");
      else if (/direccion/.test(key)) control.setAttribute("autocomplete", "street-address");
    }

    if (!control.getAttribute("placeholder") && tag !== "select" && type !== "date" && type !== "hidden") {
      let placeholder = "";
      if (/buscar|busqueda/.test(key)) placeholder = "Escribe para buscar...";
      else if (/monto|saldo|cuota|capital|interes|egreso|ingreso|comision|mora/.test(key)) placeholder = "0.00";
      else if (/telefono|celular/.test(key)) placeholder = "8888 0000";
      else if (/correo|email/.test(key)) placeholder = "correo@dominio.com";
      else if (/cedula|identificacion/.test(key)) placeholder = "0010000000000A";
      else if (/observacion|motivo/.test(key)) placeholder = "Detalle breve para auditoria";
      else if (/nombre|cliente|abonante/.test(key)) placeholder = "Nombre completo";
      if (placeholder) control.setAttribute("placeholder", placeholder);
    }
  }

  function svg(name) {
    const body = ICONS[name] || ICONS.settings;
    const node = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    node.setAttribute("viewBox", "0 0 24 24");
    node.setAttribute("aria-hidden", "true");
    node.classList.add("ux-action-icon");
    node.innerHTML = body;
    return node;
  }

  function iconNameFor(text) {
    const key = normalize(text);
    return BUTTON_ICON_RULES.find(([regex]) => regex.test(key))?.[1] || null;
  }

  function addButtonIcons(root) {
    const selector = [
      "button",
      "a.button",
      "a.primary-button",
      "a.ghost-button",
      ".workspace-button",
      ".caja-tab"
    ].join(",");

    collectMatches(root, selector).forEach((button) => {
      if (button.dataset.uxIconified === "true") return;
      if (button.classList.contains("menu-card")) return;
      if (button.querySelector("svg, img, .ux-action-icon")) return;
      const text = button.textContent || button.getAttribute("aria-label") || button.getAttribute("title") || "";
      const name = iconNameFor(text);
      if (!name) return;
      button.dataset.uxIconified = "true";
      button.classList.add("ux-iconified");
      button.prepend(svg(name));
    });
  }

  function addModuleCardIcons(root) {
    collectMatches(root, ".menu-card[data-module-id]").forEach((card) => {
      if (card.dataset.uxModuleIcon === "true") return;
      const iconName = MODULE_ICONS[normalize(card.dataset.moduleId)] || "settings";
      const top = card.querySelector(".menu-card-top") || card;
      const icon = svg(iconName);
      icon.classList.add("menu-module-icon");
      top.prepend(icon);
      card.dataset.uxModuleIcon = "true";
    });
  }

  function improveSessionRole() {
    const meta = document.getElementById("sessionMeta");
    if (!meta) return;
    const text = normalize(meta.textContent);
    if (!text.includes("sin rol") && !text.includes("sesion activa")) return;

    const session = window.SifnicSession?.getSession?.();
    const roles = session?.roles || session?.Roles || [];
    const role = Array.isArray(roles) ? roles[0] : roles;
    if (!role) return;

    const prettyRole = String(role)
      .replace(/_/g, " ")
      .toLowerCase()
      .replace(/\b\w/g, (char) => char.toUpperCase());
    meta.textContent = meta.textContent.replace(/Sin rol|Sesion activa/i, prettyRole);
  }

  function observeLateUi() {
    const observer = new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        if (mutation.type === "characterData" || mutation.target?.id === "sessionMeta") {
          improveSessionRole();
        }

        mutation.addedNodes.forEach((node) => {
          if (!(node instanceof HTMLElement)) return;
          addFieldGuidance(node);
          addButtonIcons(node);
          addModuleCardIcons(node);
          if (node.id === "sessionMeta" || node.querySelector?.("#sessionMeta")) improveSessionRole();
        });
      }
    });
    observer.observe(document.body, { childList: true, subtree: true, characterData: true });
  }

  function boot() {
    addFieldGuidance(document);
    addButtonIcons(document);
    addModuleCardIcons(document);
    improveSessionRole();
    window.setTimeout(improveSessionRole, 250);
    window.setTimeout(improveSessionRole, 1000);
    observeLateUi();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", boot, { once: true });
  } else {
    boot();
  }
})();
