window.ClockService = (() => {
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
    getCatalogs: () => request("/Reloj/Catalogos"),
    getStatus: (cedula) => request(`/Reloj/Estado${buildQuery({ cedula })}`),
    mark: (payload) =>
      request("/Reloj/Marcar", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    getSummary: ({ search, dateFrom, dateTo, idEmpleado }) =>
      request(`/Reloj/Resumen${buildQuery({ search, dateFrom, dateTo, idEmpleado })}`),
  };
})();
