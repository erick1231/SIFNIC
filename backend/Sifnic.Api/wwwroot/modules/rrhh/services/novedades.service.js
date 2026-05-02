window.NovedadesService = (() => {
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
    getCatalogs: () => request("/Novedades/Catalogos"),
    listPermisos: ({ search, status }) =>
      request(`/Novedades/ListarPermisos${buildQuery({ search, status })}`),
    getPermiso: (id) => request(`/Novedades/ObtenerPermiso/${id}`),
    createPermiso: (payload) =>
      request("/Novedades/CrearPermiso", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    updatePermiso: (id, payload) =>
      request(`/Novedades/ActualizarPermiso/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    resolvePermiso: (id, payload) =>
      request(`/Novedades/ResolverPermiso/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    getVacationBalance: ({ idEmpleado, fechaCorte }) =>
      request(`/Novedades/ObtenerSaldoVacaciones${buildQuery({ idEmpleado, fechaCorte })}`),
    getVacationAvailabilityReport: ({ search, fechaCorte, idDepartamento, status }) =>
      request(
        `/Novedades/ReporteVacacionesDisponibles${buildQuery({
          search,
          fechaCorte,
          idDepartamento,
          status,
        })}`,
      ),
    listVacaciones: ({ search, status }) =>
      request(`/Novedades/ListarVacaciones${buildQuery({ search, status })}`),
    getVacacion: (id) => request(`/Novedades/ObtenerVacacion/${id}`),
    createVacacion: (payload) =>
      request("/Novedades/CrearVacacion", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    updateVacacion: (id, payload) =>
      request(`/Novedades/ActualizarVacacion/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    resolveVacacion: (id, payload) =>
      request(`/Novedades/ResolverVacacion/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    applyVacationBulkAdjustment: (payload) =>
      request("/Novedades/AplicarAjusteVacacionesMasivo", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    listHorasExtra: ({ search, status }) =>
      request(`/Novedades/ListarHorasExtra${buildQuery({ search, status })}`),
    getHoraExtra: (id) => request(`/Novedades/ObtenerHoraExtra/${id}`),
    createHoraExtra: (payload) =>
      request("/Novedades/CrearHoraExtra", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    updateHoraExtra: (id, payload) =>
      request(`/Novedades/ActualizarHoraExtra/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    resolveHoraExtra: (id, payload) =>
      request(`/Novedades/ResolverHoraExtra/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
  };
})();
