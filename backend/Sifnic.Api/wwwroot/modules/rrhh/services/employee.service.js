window.EmployeeService = (() => {
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
    getCatalogs: () => request("/Empleados/Catalogos"),
    list: ({ search, status }) =>
      request(`/Empleados/Listar${buildQuery({ search, status })}`),
    get: (id) => request(`/Empleados/Obtener/${id}`),
    create: (payload) =>
      request("/Empleados/Crear", {
        method: "POST",
        body: JSON.stringify(payload),
      }),
    update: (id, payload) =>
      request(`/Empleados/Actualizar/${id}`, {
        method: "PUT",
        body: JSON.stringify(payload),
      }),
    uploadPhoto: (id, file) => {
      const formData = new FormData();
      formData.append("archivo", file);

      return request(`/Empleados/SubirFotoPerfil/${id}`, {
        method: "POST",
        body: formData,
      });
    },
    remove: (id, payload) =>
      request(`/Empleados/Eliminar/${id}`, {
        method: "DELETE",
        body: JSON.stringify(payload),
      }),
  };
})();
