window.ContractService = (() => {
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
    getCatalogs: () => request("/Contratos/Catalogos"),
    suggestNumber: (idEmpleado, ignorarIdContrato) =>
      request(`/Contratos/SugerirNumero${buildQuery({ idEmpleado, ignorarIdContrato })}`),
    list: ({ search, status }) =>
      request(`/Contratos/Listar${buildQuery({ search, status })}`),
    get: (id) => request(`/Contratos/Obtener/${id}`),
    create: (payload) =>
      request("/Contratos/Crear", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    update: (id, payload) =>
      request(`/Contratos/Actualizar/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    remove: (id, payload) =>
      request(`/Contratos/Eliminar/${id}`, {
        method: "DELETE",
        body: JSON.stringify(payload),
      }),
    document: (id) => request(`/Contratos/Documento/${id}`),
  };
})();
