window.PersonnelActionService = (() => {
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
    getCatalogs: () => request("/AccionesPersonal/Catalogos"),
    list: ({ search, status }) =>
      request(`/AccionesPersonal/Listar${buildQuery({ search, status })}`),
    get: (id) => request(`/AccionesPersonal/Obtener/${id}`),
    create: (payload) =>
      request("/AccionesPersonal/Crear", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    update: (id, payload) =>
      request(`/AccionesPersonal/Actualizar/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    remove: (id, payload) =>
      request(`/AccionesPersonal/Eliminar/${id}`, {
        method: "DELETE",
        body: JSON.stringify(payload),
      }),
  };
})();
