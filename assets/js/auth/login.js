const SESSION_KEY = "sifnic.session";
const sessionLoader = document.getElementById("sessionLoader");
const loaderPercent = document.getElementById("loaderPercent");
const loaderPhase = document.getElementById("loaderPhase");
const loaderStatus = document.getElementById("loaderStatus");

const loginForm = document.getElementById("loginForm");
const usernameInput = document.getElementById("username");
const passwordInput = document.getElementById("password");
const togglePassword = document.getElementById("togglePassword");
const formNote = document.getElementById("formNote");
const loginButton = document.getElementById("loginButton");
const buttonText = document.getElementById("buttonText");
const loginCard = document.querySelector(".login-card");

const loadingSteps = [
  {
    limit: 25,
    phase: "Autenticando usuario",
    status: "Verificando credenciales institucionales.",
  },
  {
    limit: 55,
    phase: "Validando perfil",
    status: "Preparando permisos y contexto de sesión.",
  },
  {
    limit: 82,
    phase: "Configurando acceso",
    status: "Sincronizando el entorno operativo.",
  },
  {
    limit: 100,
    phase: "Abriendo sistema",
    status: "Acceso autorizado. Iniciando la sesión.",
  },
];

const defaultNote = "Usa tus credenciales institucionales.";

const updateLoader = (progress) => {
  const safeValue = Math.min(100, Math.max(0, progress));
  const activeStep =
    loadingSteps.find((step) => safeValue <= step.limit) ||
    loadingSteps[loadingSteps.length - 1];

  document.documentElement.style.setProperty("--loader-value", safeValue);
  document.documentElement.style.setProperty("--loader-progress", `${safeValue}%`);

  loaderPercent.textContent = `${safeValue}%`;
  loaderPhase.textContent = activeStep.phase;
  loaderStatus.textContent = activeStep.status;
};

const openSessionLoader = () => {
  sessionLoader.classList.add("is-active");
  sessionLoader.setAttribute("aria-hidden", "false");
  document.body.classList.add("is-session-loading");
};

const closeSessionLoader = () => {
  sessionLoader.classList.remove("is-active");
  sessionLoader.setAttribute("aria-hidden", "true");
  document.body.classList.remove("is-session-loading");
};

const setNotice = (message, type = "") => {
  formNote.textContent = message;
  formNote.classList.remove("is-error", "is-success");

  if (type) {
    formNote.classList.add(type === "error" ? "is-error" : "is-success");
  }
};

const clearFieldStates = () => {
  document.querySelectorAll(".field-control").forEach((control) => {
    control.classList.remove("has-error");
  });
};

const validateFields = () => {
  clearFieldStates();

  const errors = [];

  if (!usernameInput.value.trim()) {
    errors.push({
      input: usernameInput,
      message: "Debes ingresar tu usuario.",
    });
  }

  if (!passwordInput.value.trim()) {
    errors.push({
      input: passwordInput,
      message: "Debes ingresar tu contraseña.",
    });
  }

  errors.forEach(({ input }) => {
    input.closest(".field-control")?.classList.add("has-error");
  });

  return errors;
};

const simulateLogin = () => {
  loginButton.disabled = true;
  loginButton.classList.add("is-loading");
  loginCard.classList.remove("is-success");
  buttonText.textContent = "Ingresando...";

  updateLoader(0);
  openSessionLoader();

  let progress = 0;

  const tick = () => {
    const increment = progress < 30 ? 8 : progress < 64 ? 7 : progress < 86 ? 5 : 3;
    progress += increment;
    updateLoader(progress);

    if (progress < 100) {
      window.setTimeout(tick, 95);
      return;
    }

    window.setTimeout(() => {
      const sessionData = {
        active: true,
        user: usernameInput.value.trim() || "Usuario SIFNIC",
        loginAt: new Date().toISOString(),
      };

      localStorage.setItem(SESSION_KEY, JSON.stringify(sessionData));
      closeSessionLoader();
      loginButton.disabled = false;
      loginButton.classList.remove("is-loading");
      loginCard.classList.add("is-success");
      buttonText.textContent = "Acceso autorizado";
      setNotice("Inicio de sesión con éxito.", "success");

      window.setTimeout(() => {
        window.location.href = "dashboard.html";
      }, 320);
    }, 480);
  };

  window.setTimeout(tick, 160);
};

togglePassword?.addEventListener("click", () => {
  const isPassword = passwordInput.type === "password";
  passwordInput.type = isPassword ? "text" : "password";
  togglePassword.setAttribute(
    "aria-label",
    isPassword ? "Ocultar contraseña" : "Mostrar contraseña",
  );
});

loginForm?.addEventListener("submit", (event) => {
  event.preventDefault();

  const errors = validateFields();

  if (errors.length > 0) {
    const firstError = errors[0];
    firstError.input.focus();
    loginCard.classList.remove("is-success");
    setNotice(firstError.message, "error");
    return;
  }

  setNotice("Credenciales capturadas correctamente.", "");
  simulateLogin();
});

[usernameInput, passwordInput].forEach((input) => {
  input?.addEventListener("input", () => {
    input.closest(".field-control")?.classList.remove("has-error");

    if (formNote.classList.contains("is-error")) {
      setNotice(defaultNote);
    }

    if (buttonText.textContent === "Acceso autorizado") {
      buttonText.textContent = "Ingresar";
      loginCard.classList.remove("is-success");
      setNotice(defaultNote);
    }
  });
});

setNotice(defaultNote);
updateLoader(0);
window.setTimeout(() => usernameInput?.focus(), 180);
