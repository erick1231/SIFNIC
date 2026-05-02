window.OrganizationStructureService = (() => {
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
    const params = new URLSearchParams();

    Object.entries(values || {}).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== "") {
        params.set(key, value);
      }
    });

    const query = params.toString();
    return query ? `?${query}` : "";
  };

  return {
    getCatalogs: () => request("/EstructuraOrganizativa/Catalogos"),
    list: ({ search = "", idDepartamento = "", tipoNodo = "", includeInactive = false } = {}) =>
      request(
        `/EstructuraOrganizativa/Listar${buildQuery({
          search,
          idDepartamento,
          tipoNodo,
          includeInactive: includeInactive ? "true" : "",
        })}`,
      ),
    getTree: ({ search = "", idDepartamento = "", idNodoGerencia = "", includeInactive = false } = {}) =>
      request(
        `/EstructuraOrganizativa/Arbol${buildQuery({
          search,
          idDepartamento,
          idNodoGerencia,
          includeInactive: includeInactive ? "true" : "",
        })}`,
      ),
    get: (id) => request(`/EstructuraOrganizativa/Obtener/${id}`),
    create: (payload) =>
      request("/EstructuraOrganizativa/Crear", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    update: (id, payload) =>
      request(`/EstructuraOrganizativa/Actualizar/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    remove: (id, payload) =>
      request(`/EstructuraOrganizativa/Eliminar/${id}`, {
        method: "DELETE",
        body: JSON.stringify(payload),
      }),
    loadDemo: () =>
      request("/EstructuraOrganizativa/CargarEstructuraBase", {
        method: "POST",
      }),
  };
})();
