window.SifnicSession = (() => {
  const SESSION_KEY = "sifnic.session";

  const getSession = () => {
    try {
      const session = JSON.parse(localStorage.getItem(SESSION_KEY) || "null");
      return session && session.active ? session : null;
    } catch {
      return null;
    }
  };

  const saveSession = (session) => {
    localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  };

  const clearSession = () => {
    localStorage.removeItem(SESSION_KEY);
  };

  const hasAnyRole = (session, expectedRoles = []) => {
    const roles = Array.isArray(session?.roles) ? session.roles : [];
    const roleSet = new Set(roles.map((role) => String(role || "").toUpperCase()));
    return expectedRoles.some((role) => roleSet.has(String(role || "").toUpperCase()));
  };

  const withSessionHeaders = (headers = {}) => {
    const session = getSession();
    return {
      ...(session?.sessionToken ? { "X-Session-Token": session.sessionToken } : {}),
      ...headers,
    };
  };

  const withSessionQuery = (url) => {
    const session = getSession();
    if (!session?.sessionToken) return url;
    const separator = String(url).includes("?") ? "&" : "?";
    return `${url}${separator}sessionToken=${encodeURIComponent(session.sessionToken)}`;
  };

  const openWithSession = (url, target = "_blank", features = "noopener") => {
    window.open(withSessionQuery(url), target, features);
  };

  const parseJson = async (response) => {
    try {
      return await response.json();
    } catch {
      return null;
    }
  };

  const request = async (url, options = {}) => {
    const hasBody = options.body !== undefined && options.body !== null;
    const isFormData =
      typeof FormData !== "undefined" && hasBody && options.body instanceof FormData;

    const headers = withSessionHeaders({
      ...(hasBody && !isFormData ? { "Content-Type": "application/json" } : {}),
      ...(options.headers || {}),
    });

    const response = await fetch(url, {
      method: options.method || "GET",
      headers,
      body: options.body,
    });

    const payload = await parseJson(response);

    if (!response.ok || payload?.ok === false) {
      const error = new Error(payload?.message || "No se pudo completar la operacion.");
      error.status = response.status;
      error.payload = payload;
      error.errors = payload?.errors || {};
      error.detail = payload?.detail || null;
      throw error;
    }

    return payload;
  };

  const logout = async () => {
    try {
      const session = getSession();
      if (session?.sessionToken) {
        await fetch("/Seguridad/Logout", {
          method: "POST",
          headers: withSessionHeaders(),
        });
      }
    } catch {
      // The local session is cleared even if the request fails.
    } finally {
      clearSession();
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

  return {
    SESSION_KEY,
    getSession,
    saveSession,
    clearSession,
    hasAnyRole,
    withSessionHeaders,
    withSessionQuery,
    openWithSession,
    request,
    logout,
    formatDateTime,
  };
})();
