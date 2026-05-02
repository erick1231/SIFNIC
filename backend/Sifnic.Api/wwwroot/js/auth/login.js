const sessionApi = window.SifnicSession;
const sessionLoader = document.getElementById("sessionLoader");
const loaderPercent = document.getElementById("loaderPercent");
const loaderPhase = document.getElementById("loaderPhase");
const loaderStatus = document.getElementById("loaderStatus");

const loginCard = document.querySelector(".login-card");
const loginTitle = document.getElementById("loginTitle");
const loginIntro = document.getElementById("loginIntro");

const loginForm = document.getElementById("loginForm");
const usernameInput = document.getElementById("username");
const passwordInput = document.getElementById("password");
const togglePassword = document.getElementById("togglePassword");
const clockEntryButton = document.getElementById("clockEntryButton");
const formNote = document.getElementById("formNote");
const loginButton = document.getElementById("loginButton");
const buttonText = document.getElementById("buttonText");

const passwordChangeForm = document.getElementById("passwordChangeForm");
const changeUsernameInput = document.getElementById("changeUsername");
const newPasswordInput = document.getElementById("newPassword");
const confirmPasswordInput = document.getElementById("confirmPassword");
const changeFormNote = document.getElementById("changeFormNote");
const changePasswordButton = document.getElementById("changePasswordButton");
const changePasswordButtonText = document.getElementById("changePasswordButtonText");
const cancelPasswordChange = document.getElementById("cancelPasswordChange");

const loadingSteps = [
  {
    limit: 25,
    phase: "Autenticando usuario",
    status: "Verificando credenciales institucionales.",
  },
  {
    limit: 55,
    phase: "Validando perfil",
    status: "Preparando roles y contexto de sesion.",
  },
  {
    limit: 82,
    phase: "Configurando acceso",
    status: "Sincronizando el entorno operativo.",
  },
  {
    limit: 100,
    phase: "Abriendo sistema",
    status: "Acceso autorizado. Iniciando la sesion.",
  },
];

const defaultNote = "Usa tus credenciales institucionales.";
const changeDefaultNote =
  "Tu usuario tiene una clave temporal. Debes actualizarla para continuar.";

const state = {
  pendingPasswordChange: null,
  loaderTimer: null,
  loaderProgress: 0,
};

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

const startLoader = () => {
  window.clearTimeout(state.loaderTimer);
  state.loaderProgress = 0;
  updateLoader(0);
  openSessionLoader();

  const advance = () => {
    const nextIncrement =
      state.loaderProgress < 30 ? 8 : state.loaderProgress < 64 ? 6 : state.loaderProgress < 88 ? 3 : 1;
    state.loaderProgress = Math.min(92, state.loaderProgress + nextIncrement);
    updateLoader(state.loaderProgress);

    if (state.loaderProgress < 92) {
      state.loaderTimer = window.setTimeout(advance, 110);
    }
  };

  state.loaderTimer = window.setTimeout(advance, 140);
};

const finishLoader = async (phase, status) => {
  window.clearTimeout(state.loaderTimer);

  if (phase) {
    loaderPhase.textContent = phase;
  }

  if (status) {
    loaderStatus.textContent = status;
  }

  updateLoader(100);
  await new Promise((resolve) => window.setTimeout(resolve, 260));
  closeSessionLoader();
};

const failLoader = () => {
  window.clearTimeout(state.loaderTimer);
  closeSessionLoader();
};

const setNotice = (message, type = "") => {
  formNote.textContent = message;
  formNote.classList.remove("is-error", "is-success");

  if (type) {
    formNote.classList.add(type === "error" ? "is-error" : "is-success");
  }
};

const setChangeNotice = (message, type = "") => {
  changeFormNote.textContent = message;
  changeFormNote.classList.remove("is-error", "is-success");

  if (type) {
    changeFormNote.classList.add(type === "error" ? "is-error" : "is-success");
  }
};

const clearFieldStates = () => {
  document.querySelectorAll(".field-control").forEach((control) => {
    control.classList.remove("has-error");
  });
};

const setFieldError = (input) => {
  input?.closest(".field-control")?.classList.add("has-error");
};

const clearFieldError = (input) => {
  input?.closest(".field-control")?.classList.remove("has-error");
};

const showLoginMode = () => {
  state.pendingPasswordChange = null;
  loginTitle.textContent = "Iniciar sesion";
  loginIntro.textContent = "Usa tus credenciales institucionales para entrar al sistema.";
  loginForm.hidden = false;
  passwordChangeForm.hidden = true;
  newPasswordInput.value = "";
  confirmPasswordInput.value = "";
  changeUsernameInput.value = "";
  loginCard.classList.remove("is-password-change");
  setChangeNotice(changeDefaultNote);
};

const showPasswordChangeMode = ({ username, currentPassword }) => {
  state.pendingPasswordChange = {
    username,
    currentPassword,
  };

  loginTitle.textContent = "Cambio de clave";
  loginIntro.textContent =
    "Por seguridad, este usuario debe cambiar la clave temporal antes de entrar.";
  loginForm.hidden = true;
  passwordChangeForm.hidden = false;
  changeUsernameInput.value = username;
  newPasswordInput.value = "";
  confirmPasswordInput.value = "";
  loginCard.classList.add("is-password-change");
  setChangeNotice(changeDefaultNote);
  window.setTimeout(() => newPasswordInput?.focus(), 120);
};

const setLoginBusy = (busy) => {
  loginButton.disabled = busy;
  loginButton.classList.toggle("is-loading", busy);
  buttonText.textContent = busy ? "Ingresando..." : "Ingresar";
};

const setChangeBusy = (busy) => {
  changePasswordButton.disabled = busy;
  changePasswordButton.classList.toggle("is-loading", busy);
  changePasswordButtonText.textContent = busy ? "Actualizando..." : "Actualizar clave";
};

const validateLogin = () => {
  clearFieldStates();

  if (!usernameInput.value.trim()) {
    setFieldError(usernameInput);
    return {
      input: usernameInput,
      message: "Debes ingresar tu usuario.",
    };
  }

  if (!passwordInput.value.trim()) {
    setFieldError(passwordInput);
    return {
      input: passwordInput,
      message: "Debes ingresar tu contrasena.",
    };
  }

  return null;
};

const validatePasswordChange = () => {
  clearFieldStates();

  if (!newPasswordInput.value.trim()) {
    setFieldError(newPasswordInput);
    return {
      input: newPasswordInput,
      message: "Ingresa la nueva clave.",
    };
  }

  if (newPasswordInput.value.trim().length < 6) {
    setFieldError(newPasswordInput);
    return {
      input: newPasswordInput,
      message: "La nueva clave debe tener al menos 6 caracteres.",
    };
  }

  if (!confirmPasswordInput.value.trim()) {
    setFieldError(confirmPasswordInput);
    return {
      input: confirmPasswordInput,
      message: "Confirma la nueva clave.",
    };
  }

  if (newPasswordInput.value !== confirmPasswordInput.value) {
    setFieldError(confirmPasswordInput);
    return {
      input: confirmPasswordInput,
      message: "La confirmacion no coincide con la nueva clave.",
    };
  }

  if (
    state.pendingPasswordChange?.currentPassword &&
    newPasswordInput.value === state.pendingPasswordChange.currentPassword
  ) {
    setFieldError(newPasswordInput);
    return {
      input: newPasswordInput,
      message: "La nueva clave debe ser diferente a la temporal.",
    };
  }

  return null;
};

const redirectToDashboard = () => {
  window.location.href = "/App/Dashboard";
};

const handleSuccessfulSession = async (session, successMessage) => {
  await finishLoader("Abriendo sistema", "Acceso autorizado. Iniciando la sesion.");
  sessionApi.saveSession(session);
  loginCard.classList.add("is-success");
  setNotice(successMessage, "success");

  window.setTimeout(() => {
    redirectToDashboard();
  }, 280);
};

const submitLogin = async () => {
  const error = validateLogin();

  if (error) {
    loginCard.classList.remove("is-success");
    setNotice(error.message, "error");
    error.input.focus();
    return;
  }

  setNotice("Validando credenciales...", "");
  setLoginBusy(true);
  loginCard.classList.remove("is-success");
  startLoader();

  try {
    const payload = await sessionApi.request("/Seguridad/Login", {
      method: "POST",
      body: JSON.stringify({
        username: usernameInput.value.trim(),
        password: passwordInput.value,
      }),
      headers: {
        Accept: "application/json",
      },
    });

    if (payload?.data?.requirePasswordChange) {
      await finishLoader("Clave temporal detectada", "Debes actualizar la clave para continuar.");
      showPasswordChangeMode({
        username: payload.data.username || usernameInput.value.trim(),
        currentPassword: passwordInput.value,
      });
      setNotice(
        "Tu usuario tiene una clave temporal. Actualizala para poder entrar.",
        "success",
      );
      return;
    }

    await handleSuccessfulSession(payload?.data, "Inicio de sesion con exito.");
  } catch (requestError) {
    failLoader();
    loginCard.classList.remove("is-success");
    setNotice(requestError.message || "No se pudo iniciar sesion.", "error");
  } finally {
    setLoginBusy(false);
  }
};

const submitPasswordChange = async () => {
  const validationError = validatePasswordChange();

  if (validationError) {
    setChangeNotice(validationError.message, "error");
    validationError.input.focus();
    return;
  }

  if (!state.pendingPasswordChange?.username || !state.pendingPasswordChange?.currentPassword) {
    showLoginMode();
    setNotice("La solicitud de cambio de clave ya no es valida. Inicia sesion de nuevo.", "error");
    return;
  }

  setChangeNotice("Actualizando tu clave...", "");
  setChangeBusy(true);
  startLoader();

  try {
    const payload = await sessionApi.request("/Seguridad/CambiarClave", {
      method: "POST",
      body: JSON.stringify({
        username: state.pendingPasswordChange.username,
        currentPassword: state.pendingPasswordChange.currentPassword,
        newPassword: newPasswordInput.value,
      }),
      headers: {
        Accept: "application/json",
      },
    });

    await handleSuccessfulSession(payload?.data, "Clave actualizada correctamente.");
  } catch (requestError) {
    failLoader();
    setChangeNotice(requestError.message || "No se pudo cambiar la clave.", "error");
  } finally {
    setChangeBusy(false);
  }
};

if (sessionApi.getSession()) {
  redirectToDashboard();
}

togglePassword?.addEventListener("click", () => {
  const isPassword = passwordInput.type === "password";
  passwordInput.type = isPassword ? "text" : "password";
  togglePassword.setAttribute(
    "aria-label",
    isPassword ? "Ocultar contrasena" : "Mostrar contrasena",
  );
});

loginForm?.addEventListener("submit", async (event) => {
  event.preventDefault();
  await submitLogin();
});

passwordChangeForm?.addEventListener("submit", async (event) => {
  event.preventDefault();
  await submitPasswordChange();
});

cancelPasswordChange?.addEventListener("click", () => {
  showLoginMode();
  setNotice(defaultNote);
  passwordInput.focus();
});

clockEntryButton?.addEventListener("click", () => {
  window.location.href = "/App/Reloj";
});

[usernameInput, passwordInput].forEach((input) => {
  input?.addEventListener("input", () => {
    clearFieldError(input);

    if (formNote.classList.contains("is-error")) {
      setNotice(defaultNote);
    }

    if (loginCard.classList.contains("is-success")) {
      loginCard.classList.remove("is-success");
    }
  });
});

[newPasswordInput, confirmPasswordInput].forEach((input) => {
  input?.addEventListener("input", () => {
    clearFieldError(input);

    if (changeFormNote.classList.contains("is-error")) {
      setChangeNotice(changeDefaultNote);
    }
  });
});

showLoginMode();
setNotice(defaultNote);
updateLoader(0);
window.setTimeout(() => usernameInput?.focus(), 180);
