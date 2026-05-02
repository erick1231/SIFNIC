window.RRHHDashboardService = (() => {
  const sessionApi = window.SifnicSession;

  const request = async (url, options = {}) => {
    const data = await sessionApi.request(url, options);
    return data?.data;
  };

  return {
    getOverview: () => request("/RrhhResumen/Overview"),
    getOrganizationStructure: () => request("/RrhhResumen/EstructuraEmpresa"),
    getAuditLog: ({ search = "", process = "", dateFrom = "", dateTo = "" } = {}) => {
      const params = new URLSearchParams();

      if (search) {
        params.set("search", search);
      }

      if (process) {
        params.set("process", process);
      }

      if (dateFrom) {
        params.set("dateFrom", dateFrom);
      }

      if (dateTo) {
        params.set("dateTo", dateTo);
      }

      const query = params.toString();
      return request(`/RrhhResumen/Bitacora${query ? `?${query}` : ""}`);
    },
  };
})();
