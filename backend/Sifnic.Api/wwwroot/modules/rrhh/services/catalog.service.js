window.RRHHCatalogService = (() => {
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
    getCatalogs: () => request("/CatalogosRrhh/Catalogos"),
    list: ({ moduleId, search, status }) =>
      request(`/CatalogosRrhh/Listar${buildQuery({ moduleId, search, status })}`),
    get: (moduleId, id) => request(`/CatalogosRrhh/Obtener${buildQuery({ moduleId, id })}`),
    create: (payload) =>
      request("/CatalogosRrhh/Crear", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    update: (id, payload) =>
      request(`/CatalogosRrhh/Actualizar/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    remove: (id, payload) =>
      request(`/CatalogosRrhh/Eliminar/${id}`, {
        method: "DELETE",
        body: JSON.stringify(payload),
      }),
  };
})();
