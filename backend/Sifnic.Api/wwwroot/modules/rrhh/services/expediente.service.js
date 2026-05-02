window.ExpedienteService = (() => {
  const sessionApi = window.SifnicSession;

  const getOperatorUser = () => {
    const session = sessionApi.getSession();
    return session?.username || session?.user || "sistema.local";
  };

  const request = async (url, options = {}) => {
    const data = await sessionApi.request(url, {
      ...options,
      headers: {
        "X-Operator-User": getOperatorUser(),
        ...(options.headers || {}),
      },
    });

    return data?.data;
  };

  const buildQuery = (values) => {
    const search = new URLSearchParams();

    Object.entries(values).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== "") {
        search.set(key, value);
      }
    });

    const query = search.toString();
    return query ? `?${query}` : "";
  };

  return {
    getCatalogs: () => request("/Expedientes/Catalogos"),
    list: ({ search, status }) =>
      request(`/Expedientes/Listar${buildQuery({ search, status })}`),
    get: (id) => request(`/Expedientes/Obtener/${id}`),
    create: (payload) =>
      request("/Expedientes/Crear", {
        method: "POST",
        body: payload,
      }),
    update: (id, payload) =>
      request(`/Expedientes/Actualizar/${id}`, {
        method: "PUT",
        body: payload,
      }),
    remove: (id, payload) =>
      request(`/Expedientes/Eliminar/${id}`, {
        method: "DELETE",
        body: JSON.stringify(payload),
      }),
    buildDownloadUrl: (id) => `/Expedientes/Descargar/${id}`,
  };
})();
