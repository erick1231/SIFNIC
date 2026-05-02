window.SifnicTheme = (() => {
  const STORAGE_KEY = "sifnic.theme";
  const BASELINE_KEY = "sifnic.theme.lightBaseline";
  const THEMES = ["dark", "light"];

  const normalizeTheme = (value) =>
    THEMES.includes(String(value || "").toLowerCase())
      ? String(value || "").toLowerCase()
      : "light";

  const getStoredTheme = () => {
    try {
      if (localStorage.getItem(BASELINE_KEY) !== "2026-05") {
        localStorage.setItem(BASELINE_KEY, "2026-05");
        localStorage.setItem(STORAGE_KEY, "light");
      }
      return normalizeTheme(localStorage.getItem(STORAGE_KEY));
    } catch {
      return "light";
    }
  };

  const applyTheme = (theme, persist = true) => {
    const normalized = normalizeTheme(theme);
    document.documentElement.dataset.theme = normalized;
    document.documentElement.style.colorScheme = normalized;

    if (persist) {
      try {
        localStorage.setItem(STORAGE_KEY, normalized);
      } catch {
        // Se ignora si el navegador no permite persistencia.
      }
    }

    document.dispatchEvent(
      new CustomEvent("sifnic-theme-change", {
        detail: {
          theme: normalized,
        },
      }),
    );

    return normalized;
  };

  const getTheme = () => document.documentElement.dataset.theme || getStoredTheme();

  const toggleTheme = () => applyTheme(getTheme() === "light" ? "dark" : "light");

  const getThemeCopy = (theme) =>
    theme === "light"
      ? {
          label: "Modo claro",
          hint: "Cambiar a oscuro",
        }
      : {
          label: "Modo oscuro",
          hint: "Cambiar a claro",
        };

  const syncToggle = (button, labelNode, hintNode, theme) => {
    if (!button) {
      return;
    }

    const copy = getThemeCopy(theme);
    button.dataset.theme = theme;
    button.setAttribute("aria-pressed", theme === "light" ? "true" : "false");
    button.setAttribute("aria-label", copy.hint);

    if (labelNode) {
      labelNode.textContent = copy.label;
    }

    if (hintNode) {
      hintNode.textContent = copy.hint;
    }
  };

  const attachToggle = (button, labelNode, hintNode) => {
    if (!button) {
      return;
    }
    if (button.dataset.themeAttached === "true") {
      syncToggle(button, labelNode, hintNode, getTheme());
      return;
    }
    button.dataset.themeAttached = "true";

    const refresh = (theme = getTheme()) => {
      syncToggle(button, labelNode, hintNode, theme);
    };

    refresh();

    button.addEventListener("click", () => {
      const theme = toggleTheme();
      refresh(theme);
    });

    document.addEventListener("sifnic-theme-change", (event) => {
      refresh(event.detail?.theme || getTheme());
    });
  };

  const attachHeaderToggles = () => {
    const toggles = document.querySelectorAll("[data-theme-toggle]");
    toggles.forEach((button) => {
      const label = button.querySelector("#themeToggleLabel, [data-theme-label]");
      const hint = button.querySelector("#themeToggleHint, [data-theme-hint]");
      attachToggle(button, label, hint);
    });
  };

  applyTheme(getStoredTheme(), false);

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", attachHeaderToggles, { once: true });
  } else {
    attachHeaderToggles();
  }

  return {
    STORAGE_KEY,
    applyTheme,
    getTheme,
    toggleTheme,
    attachToggle,
  };
})();
