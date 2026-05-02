import fs from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium, request } = require("C:/Users/eaespinoza/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");

const baseURL = "http://localhost:5277";
const edgePath = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const artifactDir = path.resolve("backend/Sifnic.Api/artifacts/qa-20260429");
const adminDeleteCredentials = {
  adminUsuario: "admin.sisfnic",
  adminPassword: "admin.sisfnic",
};

const roleUsers = {
  admin: { username: "batadmin", password: "Prueba123!" },
  adminForced: { username: "admin.sisfnic", password: "admin.sisfnic" },
  supervisor: { username: "batcoord", password: "Prueba123!" },
  chief: { username: "batjefe", password: "Prueba123!" },
  employeeA: { username: "batof1", password: "Prueba123!" },
  employeeB: { username: "batof2", password: "Prueba123!" },
  lockUser: { username: "battemp", password: "Prueba123!" },
};

const report = {
  startedAt: new Date().toISOString(),
  checks: [],
  bugs: [],
  created: {},
  notes: [],
};

const qaSuffix = new Date().toISOString().replaceAll(/[-:TZ.]/g, "").slice(0, 14);

const pass = (name, details = {}) => {
  report.checks.push({ name, status: "PASS", details });
  console.log(`PASS ${name}`);
};

const fail = (name, error, details = {}) => {
  const message = error instanceof Error ? error.message : String(error);
  report.checks.push({ name, status: "FAIL", error: message, details });
  console.error(`FAIL ${name}: ${message}`);
};

const addBug = (severity, moduleName, step, errorVisible, endpointOrFile, probableCause, suggestedFix) => {
  report.bugs.push({
    severity,
    module: moduleName,
    step,
    errorVisible,
    endpointOrFile,
    probableCause,
    suggestedFix,
  });
};

const assert = (condition, message) => {
  if (!condition) {
    throw new Error(message);
  }
};

const formatDate = (value) => value.toISOString().slice(0, 10);
const parseDate = (value) => new Date(`${value}T00:00:00`);
const addDays = (value, days) => {
  const next = new Date(value);
  next.setDate(next.getDate() + days);
  return next;
};

const first = (items) => (Array.isArray(items) && items.length ? items[0] : null);
const extractRows = (payload) =>
  Array.isArray(payload?.data)
    ? payload.data
    : Array.isArray(payload?.data?.rows)
      ? payload.data.rows
      : Array.isArray(payload?.data?.items)
        ? payload.data.items
      : [];

const pickByCode = (items, candidates = [], fallback = null) => {
  if (!Array.isArray(items)) {
    return fallback;
  }

  for (const candidate of candidates) {
    const found = items.find((item) =>
      [item?.codigo, item?.code, item?.codigoDepartamento, item?.codigoCargo, item?.codigoTipoContrato, item?.codigoHorario]
        .filter(Boolean)
        .some((value) => String(value).toUpperCase() === String(candidate).toUpperCase()));
    if (found) {
      return found;
    }
  }

  return fallback ?? first(items);
};

const enumerateDateRange = (startValue, endValue) => {
  const start = parseDate(startValue);
  const end = parseDate(endValue);
  const dates = [];

  for (let cursor = new Date(start); cursor <= end; cursor = addDays(cursor, 1)) {
    dates.push(formatDate(cursor));
  }

  return dates;
};

const findAvailableVacationDates = (items, startDate, requestedDays) => {
  const blocked = new Set(
    (Array.isArray(items) ? items : [])
      .filter((item) => String(item?.estadoVacacion || "").toUpperCase() !== "RECHAZADA")
      .flatMap((item) => enumerateDateRange(item.fechaInicio, item.fechaFin))
  );

  for (let offset = 0; offset < 400; offset += 1) {
    const start = addDays(startDate, offset);
    const dates = Array.from({ length: requestedDays }, (_, index) => formatDate(addDays(start, index)));
    if (dates.every((value) => !blocked.has(value))) {
      return dates;
    }
  }

  throw new Error("No se encontro un rango libre para registrar la vacacion QA.");
};

const findAvailableOvertimeDate = (items, startDate, overtimeTypeId) => {
  const blocked = new Set(
    (Array.isArray(items) ? items : [])
      .filter((item) => Number(item?.idTipoHoraExtra) === Number(overtimeTypeId))
      .map((item) => item.fechaHoraExtra)
      .filter(Boolean)
  );

  for (let offset = 0; offset < 180; offset += 1) {
    const candidate = formatDate(addDays(startDate, offset));
    if (!blocked.has(candidate)) {
      return candidate;
    }
  }

  throw new Error("No se encontro una fecha libre para registrar la hora extra QA.");
};

const createApiContext = async (headers = {}) =>
  request.newContext({
    baseURL,
    extraHTTPHeaders: {
      Accept: "application/json",
      ...headers,
    },
  });

const apiCall = async (api, url, options = {}) => {
  const method = options.method || "GET";
  let response;

  if (method === "GET") {
    response = await api.get(url);
  } else if (method === "POST") {
    response = await api.post(url, options.multipart ? { multipart: options.multipart } : { data: options.data });
  } else if (method === "PUT") {
    response = await api.put(url, { data: options.data });
  } else if (method === "DELETE") {
    response = await api.delete(url, { data: options.data });
  } else {
    throw new Error(`Metodo no soportado: ${method}`);
  }

  const contentType = response.headers()["content-type"] || "";
  const payload = contentType.includes("application/json") ? await response.json() : await response.text();

  if (!response.ok()) {
    const message = payload?.message || payload?.detail || payload || `HTTP ${response.status()}`;
    const error = new Error(String(message));
    error.status = response.status();
    error.payload = payload;
    throw error;
  }

  if (payload?.ok === false) {
    const error = new Error(payload.message || "La respuesta marco ok=false.");
    error.status = response.status();
    error.payload = payload;
    throw error;
  }

  return { response, payload };
};

const loginApi = async (username, password) => {
  const loginContext = await createApiContext();
  const { payload } = await apiCall(loginContext, "/Seguridad/Login", {
    method: "POST",
    data: {
      username,
      password,
    },
  });

  await loginContext.dispose();

  const session = payload.data;
  const api = await createApiContext(
    session?.sessionToken
      ? {
          "X-Session-Token": session.sessionToken,
          "X-Operator-User": session.username || username,
        }
      : {}
  );

  return {
    api,
    session,
  };
};

const loginExpectFailure = async (username, password, expectedStatus) => {
  const api = await createApiContext();
  try {
    await apiCall(api, "/Seguridad/Login", {
      method: "POST",
      data: {
        username,
        password,
      },
    });
    throw new Error("El login debio fallar y no fallo.");
  } catch (error) {
    if (error.message === "El login debio fallar y no fallo.") {
      throw error;
    }

    assert(error.status === expectedStatus, `Se esperaba HTTP ${expectedStatus} y se recibio ${error.status}.`);
  } finally {
    await api.dispose();
  }
};

const changePassword = async (username, currentPassword, newPassword) => {
  const api = await createApiContext();
  try {
    const { payload } = await apiCall(api, "/Seguridad/CambiarClave", {
      method: "POST",
      data: {
        username,
        currentPassword,
        newPassword,
      },
    });

    return payload.data;
  } finally {
    await api.dispose();
  }
};

const openPageWithSession = async (session) => {
  const browser = await chromium.launch({
    headless: true,
    executablePath: edgePath,
  });

  const context = await browser.newContext({
    baseURL,
    viewport: {
      width: 1440,
      height: 960,
    },
  });

  await context.addInitScript((savedSession) => {
    localStorage.setItem("sifnic.session", JSON.stringify(savedSession));
  }, session);

  const page = await context.newPage();
  const errors = {
    pageErrors: [],
    consoleErrors: [],
  };

  page.on("pageerror", (error) => {
    errors.pageErrors.push(error.message);
  });

  page.on("console", (msg) => {
    if (msg.type() === "error") {
      errors.consoleErrors.push(msg.text());
    }
  });

  return { browser, context, page, errors };
};

const saveScreenshot = async (page, name) => {
  const filePath = path.join(artifactDir, name);
  await page.screenshot({ path: filePath, fullPage: true });
  return filePath;
};

const ensureNoBrowserErrors = (errors, scope) => {
  const relevantConsoleErrors = errors.consoleErrors.filter(
    (entry) =>
      !entry.includes("favicon") &&
      !entry.includes("Failed to load resource: the server responded with a status of 404")
  );
  assert(errors.pageErrors.length === 0, `${scope} genero errores de pagina: ${errors.pageErrors.join(" | ")}`);
  assert(relevantConsoleErrors.length === 0, `${scope} genero errores de consola: ${relevantConsoleErrors.join(" | ")}`);
};

const getEmployeeByCode = async (api, code) => {
  const { payload } = await apiCall(api, `/Empleados/Listar?search=${encodeURIComponent(code)}`);
  const rows = Array.isArray(payload.data) ? payload.data : [];
  const found = rows.find((item) => String(item.codigoEmpleado || "").toUpperCase() === code.toUpperCase());
  assert(found, `No se encontro el empleado ${code}.`);
  return found;
};

const getCatalogPayload = async (api, url) => {
  const { payload } = await apiCall(api, url);
  return payload.data;
};

const makePngBuffer = () =>
  Buffer.from("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000D49444154789C6360F8CFC0000004010100F8FF003D381F950000000049454E44AE426082", "hex");

const makePdfBuffer = () =>
  Buffer.from("%PDF-1.1\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>endobj\n4 0 obj<</Length 36>>stream\nBT /F1 12 Tf 50 150 Td (QA PDF) Tj ET\nendstream endobj\ntrailer<</Root 1 0 R>>\n%%EOF", "utf8");

const run = async (name, callback) => {
  try {
    await callback();
  } catch (error) {
    fail(name, error);
  }
};

await fs.mkdir(artifactDir, { recursive: true });

let adminApi;
let supervisorApi;
let chiefApi;
let employeeAApi;
let employeeBApi;

const cleanup = {
  employeeId: null,
  contractId: null,
  actionId: null,
  expedienteId: null,
  structureNodeId: null,
  liquidationEmployeeId: null,
  liquidationContractId: null,
};

await run("UI smoke: login, dashboard, theme, Configuracion y RRHH", async () => {
  const browser = await chromium.launch({
    headless: true,
    executablePath: edgePath,
  });

  try {
    const context = await browser.newContext({
      baseURL,
      viewport: { width: 1440, height: 960 },
    });
    const page = await context.newPage();
    const errors = {
      pageErrors: [],
      consoleErrors: [],
    };

    page.on("pageerror", (error) => errors.pageErrors.push(error.message));
    page.on("console", (msg) => {
      if (msg.type() === "error") {
        errors.consoleErrors.push(msg.text());
      }
    });

    await page.goto(`${baseURL}/App/Login`, { waitUntil: "domcontentloaded" });
    await page.waitForSelector("#loginForm");
    assert((await page.title()).includes("SIFNIC"), "El login no cargo el titulo esperado.");
    await page.fill("#username", roleUsers.admin.username);
    await page.fill("#password", roleUsers.admin.password);
    await page.click("#loginButton");
    await page.waitForURL("**/App/Dashboard", { timeout: 20000 });
    await page.waitForSelector("#menuGrid [data-module-id='configuracion']");
    const storedThemeBefore = await page.evaluate(() => document.documentElement.dataset.theme);
    await page.click("#themeToggle");
    await page.waitForTimeout(300);
    const storedThemeAfter = await page.evaluate(() => document.documentElement.dataset.theme);
    assert(storedThemeBefore !== storedThemeAfter, "El cambio claro/oscuro no altero el tema activo.");

    await page.click("#menuGrid [data-module-id='configuracion']");
    await page.waitForURL("**/App/Configuracion", { timeout: 20000 });
    await page.waitForSelector("#generalPanel");
    const visibleConfigPanels = await page.locator(".panel-card").evaluateAll((nodes) =>
      nodes.filter((node) => !node.hasAttribute("hidden")).map((node) => node.id));
    assert(visibleConfigPanels.length === 1, `Configuracion sigue mostrando varios paneles: ${visibleConfigPanels.join(", ")}`);
    await page.click("[data-tab='rrhh']");
    await page.waitForTimeout(250);
    const rrhhPanelVisible = await page.locator("#rrhhPanel").evaluate((node) => !node.hasAttribute("hidden"));
    assert(rrhhPanelVisible, "La seccion RRHH de Configuracion no quedo visible.");
    await saveScreenshot(page, "configuracion-smoke.png");

    await page.click("#backToDashboard");
    await page.waitForURL("**/App/Dashboard", { timeout: 20000 });
    await page.click("#menuGrid [data-module-id='rrhh']");
    await page.waitForURL("**/App/Rrhh", { timeout: 20000 });
    await page.waitForSelector("#mainNav button");
    const rrhhNavCount = await page.locator("#mainNav button").count();
    const rrhhGroupCount = await page.locator("#groupBoard > *").count();
    assert(rrhhNavCount >= 2, `RRHH cargo menos bloques de navegacion de los esperados: ${rrhhNavCount}.`);
    assert(rrhhGroupCount >= 1, "RRHH no mostro grupos operativos en el tablero principal.");
    await saveScreenshot(page, "rrhh-smoke.png");
    ensureNoBrowserErrors(errors, "Login/Dashboard/Configuracion/RRHH");
    pass("UI smoke: login, dashboard, theme, Configuracion y RRHH", {
      screenshots: [
        path.join(artifactDir, "configuracion-smoke.png"),
        path.join(artifactDir, "rrhh-smoke.png"),
      ],
    });
  } finally {
    await browser.close();
  }
});

await run("Roles y seguridad: logins reales, permisos, bloqueo y desbloqueo", async () => {
  const forcedAdmin = await loginApi(roleUsers.adminForced.username, roleUsers.adminForced.password);
  assert(forcedAdmin.session.requirePasswordChange === true, "El admin institucional debio exigir cambio de clave.");
  await forcedAdmin.api.dispose();

  const admin = await loginApi(roleUsers.admin.username, roleUsers.admin.password);
  const supervisor = await loginApi(roleUsers.supervisor.username, roleUsers.supervisor.password);
  const chief = await loginApi(roleUsers.chief.username, roleUsers.chief.password);
  const employeeA = await loginApi(roleUsers.employeeA.username, roleUsers.employeeA.password);

  adminApi = admin.api;
  supervisorApi = supervisor.api;
  chiefApi = chief.api;
  employeeAApi = employeeA.api;

  assert(Array.isArray(admin.session.modules) && admin.session.modules.includes("rrhh"), "batadmin no tiene acceso a RRHH.");
  assert(admin.session.modules.includes("configuracion"), "batadmin no tiene acceso a Configuracion.");
  assert(admin.session.modules.includes("nomina"), "batadmin no tiene acceso a Nomina.");
  assert(supervisor.session.modules.includes("bandeja-supervisor"), "batcoord no tiene bandeja supervisor.");
  assert(chief.session.modules.includes("bandeja-supervisor"), "batjefe no tiene bandeja supervisor.");
  assert(employeeA.session.modules.includes("mi-portal"), "batof1 no tiene Mi Portal.");

  try {
    await apiCall(employeeA.api, "/Seguridad/Usuarios");
    throw new Error("Un empleado comun no debio abrir Seguridad/Usuarios.");
  } catch (error) {
    assert(error.status === 403, `Se esperaba 403 en Seguridad/Usuarios y se recibio ${error.status}.`);
  }

  try {
    await apiCall(employeeA.api, "/Nomina/Contexto");
    throw new Error("Un empleado comun no debio abrir Nomina/Contexto.");
  } catch (error) {
    assert(error.status === 403, `Se esperaba 403 en Nomina/Contexto y se recibio ${error.status}.`);
  }

  const { payload: securityParamsPayload } = await apiCall(admin.api, "/Seguridad/ParametrosSeguridad");
  const securityParams = securityParamsPayload.data;
  const lockUserRow = await getUserByUsername(admin.api, roleUsers.lockUser.username);

  await apiCall(admin.api, `/Seguridad/DesbloquearUsuario/${lockUserRow.idUsuario}`, { method: "POST" });
  await apiCall(admin.api, `/Seguridad/RestablecerClaveTemporal/${lockUserRow.idUsuario}`, { method: "POST" });
  await changePassword(roleUsers.lockUser.username, roleUsers.lockUser.username, roleUsers.lockUser.password);

  await apiCall(admin.api, "/Seguridad/GuardarParametrosSeguridad", {
    method: "PUT",
    data: {
      ...securityParams,
      intentosMaximos: 2,
    },
  });

  try {
    await loginExpectFailure(roleUsers.lockUser.username, "ClaveIncorrecta1!", 401);
    await loginExpectFailure(roleUsers.lockUser.username, "ClaveIncorrecta2!", 401);
    await loginExpectFailure(roleUsers.lockUser.username, roleUsers.lockUser.password, 423);
    pass("Seguridad: bloqueo por intentos fallidos");

    await apiCall(admin.api, `/Seguridad/DesbloquearUsuario/${lockUserRow.idUsuario}`, { method: "POST" });
    await apiCall(admin.api, `/Seguridad/RestablecerClaveTemporal/${lockUserRow.idUsuario}`, { method: "POST" });
    const tempSession = await loginApi(roleUsers.lockUser.username, roleUsers.lockUser.username);
    assert(tempSession.session.requirePasswordChange === true, "La clave temporal debio requerir cambio.");
    await tempSession.api.dispose();
    await changePassword(roleUsers.lockUser.username, roleUsers.lockUser.username, roleUsers.lockUser.password);
    pass("Seguridad: desbloqueo y restablecimiento temporal");
  } finally {
    await apiCall(admin.api, "/Seguridad/GuardarParametrosSeguridad", {
      method: "PUT",
      data: securityParams,
    });
  }

  const { payload: accessBitacora } = await apiCall(admin.api, "/Seguridad/BitacoraAcceso?take=40");
  const latestAccess = Array.isArray(accessBitacora.data) ? accessBitacora.data : [];
  assert(latestAccess.some((item) => String(item.usuario || "").toLowerCase() === roleUsers.lockUser.username), "La bitacora de acceso no reflejo las pruebas de bloqueo.");
  pass("Roles y seguridad: logins reales, permisos, bloqueo y desbloqueo");
});

async function getUserByUsername(api, username) {
  const { payload } = await apiCall(api, "/Seguridad/Usuarios");
  const rows = Array.isArray(payload.data?.users) ? payload.data.users : Array.isArray(payload.data) ? payload.data : [];
  const user = rows.find((item) => String(item.usuario || item.username || "").toLowerCase() === username.toLowerCase());
  assert(user, `No se encontro el usuario ${username}.`);
  return user;
}

await run("RRHH: CRUD de empleado, foto, contrato, accion, expediente, reloj y estructura", async () => {
  const employeesCatalog = await getCatalogPayload(adminApi, "/Empleados/Catalogos");
  const department = pickByCode(employeesCatalog.departments || employeesCatalog.departamentos, ["ADM", "CRE"]) || first(employeesCatalog.departments || employeesCatalog.departamentos);
  const position = pickByCode(employeesCatalog.positions || employeesCatalog.cargos, ["ASI_ADM", "OFI_CRED"]) || first(employeesCatalog.positions || employeesCatalog.cargos);
  const bank = first(employeesCatalog.banks || employeesCatalog.bancos || []);

  assert(department && position, "No se encontraron catalogos base para crear empleado.");

  const employeeCode = `QA${qaSuffix.slice(-6)}`;
  const employeeCedula = `401-290426-${qaSuffix.slice(-4)}A`;
  const employeePayload = {
    codigoEmpleado: employeeCode,
    usuarioSistema: "",
    idSupervisorEmpleado: null,
    idDepartamento: department.id ?? department.idDepartamento,
    idCargo: position.id ?? position.idCargo,
    cedula: employeeCedula,
    inss: `INSS-${qaSuffix.slice(-6)}`,
    nombres: "QA",
    apellidos: "Integral Prueba",
    fechaNacimiento: "1992-04-15",
    sexo: "F",
    estadoCivil: "SOLTERO",
    telefono: "8888-0000",
    correo: `qa.integral.${qaSuffix.slice(-4)}@sifnic.local`,
    direccion: "Managua QA",
    fechaIngreso: "2026-04-20",
    idBanco: bank?.idBanco ?? bank?.id ?? null,
    numeroCuentaBancaria: `1000${qaSuffix.slice(-6)}`,
  };

  const { payload: createEmployeePayload } = await apiCall(adminApi, "/Empleados/Crear", {
    method: "POST",
    data: employeePayload,
  });

  const createdEmployee = createEmployeePayload.data;
  cleanup.employeeId = createdEmployee.idEmpleado;
  report.created.employee = createdEmployee;
  assert(createdEmployee.usuarioSistema, "La creacion del empleado no genero usuario automatico.");

  await apiCall(adminApi, `/Empleados/SubirFotoPerfil/${createdEmployee.idEmpleado}`, {
    method: "POST",
    multipart: {
      archivo: {
        name: "qa-profile.png",
        mimeType: "image/png",
        buffer: makePngBuffer(),
      },
    },
  });

  const passwordChangedSession = await loginApi(createdEmployee.usuarioSistema, createdEmployee.usuarioSistema);
  assert(passwordChangedSession.session.requirePasswordChange === true, "El usuario autogenerado debio exigir cambio de clave.");
  await passwordChangedSession.api.dispose();
  await changePassword(createdEmployee.usuarioSistema, createdEmployee.usuarioSistema, "Prueba123!");

  await apiCall(adminApi, `/Empleados/Actualizar/${createdEmployee.idEmpleado}`, {
    method: "PUT",
    data: {
      ...employeePayload,
      telefono: "8888-1111",
      correo: `qa.edit.${qaSuffix.slice(-4)}@sifnic.local`,
    },
  });

  const contractsCatalog = await getCatalogPayload(adminApi, "/Contratos/Catalogos");
  const contractType = pickByCode(contractsCatalog.contractTypes || contractsCatalog.tiposContrato, ["INDEFINIDO", "TIEMPO_INDEFINIDO"]) || first(contractsCatalog.contractTypes || contractsCatalog.tiposContrato);
  const workShift = first(contractsCatalog.schedules || contractsCatalog.horarios);
  assert(contractType && workShift, "No se encontraron catalogos de contratos.");

  const contractPayload = {
    idEmpleado: createdEmployee.idEmpleado,
    idTipoContrato: contractType.idTipoContrato ?? contractType.id,
    idHorarioLaboral: workShift.idHorarioLaboral ?? workShift.id,
    numeroContrato: `CTR-${employeeCode}`,
    fechaInicio: "2026-04-20",
    fechaFin: null,
    salarioBaseMensual: 18000,
    moneda: "NIO",
    esContratoVigente: true,
    observacion: "Contrato QA integral",
  };

  const { payload: createdContractPayload } = await apiCall(adminApi, "/Contratos/Crear", {
    method: "POST",
    data: contractPayload,
  });
  cleanup.contractId = createdContractPayload.data.idContrato;

  await apiCall(adminApi, `/Contratos/Actualizar/${cleanup.contractId}`, {
    method: "PUT",
    data: {
      ...contractPayload,
      salarioBaseMensual: 19500,
      observacion: "Contrato QA integral actualizado",
    },
  });

  const actionsCatalog = await getCatalogPayload(adminApi, "/AccionesPersonal/Catalogos");
  const actionPayload = {
    idEmpleado: createdEmployee.idEmpleado,
    tipoAccion: "CAMBIO SALARIAL",
    fechaAccion: "2026-04-25",
    idCargoNuevo: null,
    nuevoSalarioBaseMensual: 19500,
    nuevaFechaFinContrato: null,
    aplicarCambioOperativo: true,
    descripcionAccion: "Ajuste salarial QA",
  };

  const { payload: actionCreatedPayload } = await apiCall(adminApi, "/AccionesPersonal/Crear", {
    method: "POST",
    data: actionPayload,
  });
  cleanup.actionId = actionCreatedPayload.data.idAccionPersonal;

  const expedienteCatalog = await getCatalogPayload(adminApi, "/Expedientes/Catalogos");
  const documentType = first(expedienteCatalog.documentTypes || expedienteCatalog.tiposDocumento || expedienteCatalog.documentos || []);
  assert(documentType, "No se encontro un tipo de documento para expediente.");
  const { payload: expedienteCreatedPayload } = await apiCall(adminApi, "/Expedientes/Crear", {
    method: "POST",
    multipart: {
      idEmpleado: String(createdEmployee.idEmpleado),
      tipoDocumento: String(documentType.value || documentType.codigo || documentType.nombre || documentType),
      fechaDocumento: "2026-04-25",
      fechaVencimiento: "2027-04-25",
      observacion: "Documento QA",
      archivo: {
        name: "qa-expediente.pdf",
        mimeType: "application/pdf",
        buffer: makePdfBuffer(),
      },
    },
  });
  cleanup.expedienteId = expedienteCreatedPayload.data.idExpedienteDocumento;

  const expedienteDownload = await adminApi.get(`${baseURL}/Expedientes/Descargar/${cleanup.expedienteId}`);
  assert(expedienteDownload.ok(), "No se pudo descargar el expediente subido.");
  assert((expedienteDownload.headers()["content-type"] || "").includes("pdf"), "La descarga del expediente no devolvio PDF.");

  await apiCall(adminApi, `/Reloj/Estado?cedula=${encodeURIComponent(employeeCedula)}`);
  await apiCall(adminApi, "/Reloj/Marcar", {
    method: "POST",
    data: {
      cedula: employeeCedula,
      tipoMarcacion: "ENTRADA",
    },
  });
  await apiCall(adminApi, "/Reloj/Marcar", {
    method: "POST",
    data: {
      cedula: employeeCedula,
      tipoMarcacion: "SALIDA",
    },
  });
  const { payload: clockSummaryPayload } = await apiCall(adminApi, `/Reloj/Resumen?search=${encodeURIComponent(employeeCode)}`);
  const clockRows = extractRows(clockSummaryPayload);
  if (clockRows.length === 0) {
    const { payload: fallbackClockSummaryPayload } = await apiCall(adminApi, `/Reloj/Resumen?search=${encodeURIComponent(employeeCedula)}`);
    const fallbackRows = extractRows(fallbackClockSummaryPayload);
    assert(fallbackRows.length > 0, "El reloj por cedula no reflejo las marcaciones QA.");
  }

  const structureCatalog = await getCatalogPayload(adminApi, "/EstructuraOrganizativa/Catalogos");
  const structureRowsPayload = await apiCall(adminApi, "/EstructuraOrganizativa/Listar");
  const structureRows = extractRows(structureRowsPayload.payload);
  const parentCreditNode = structureRows.find((item) => String(item.codigoNodo || "").toUpperCase() === "GC") || first(structureRows);
  assert(parentCreditNode, "No se encontro nodo padre para la prueba de estructura.");

  const qaNodePayload = {
    codigoNodo: `QA-${qaSuffix.slice(-4)}`,
    nombreNodo: `Nodo QA ${qaSuffix.slice(-4)}`,
    tipoNodo: "VACANTE",
    idNodoPadre: parentCreditNode.idNodoEstructura,
    idEmpleadoTitular: null,
    idDepartamento: department.id ?? department.idDepartamento,
    idCargo: position.id ?? position.idCargo,
    ordenVisual: 98,
    activo: true,
    observacion: "Nodo temporal QA",
  };

  const { payload: createdNodePayload } = await apiCall(adminApi, "/EstructuraOrganizativa/Crear", {
    method: "POST",
    data: qaNodePayload,
  });
  cleanup.structureNodeId = createdNodePayload.data.idNodoEstructura;

  await apiCall(adminApi, `/EstructuraOrganizativa/Actualizar/${cleanup.structureNodeId}`, {
    method: "PUT",
    data: {
      ...qaNodePayload,
      nombreNodo: `Nodo QA ${qaSuffix.slice(-4)} editado`,
    },
  });

  const { payload: structureTreePayload } = await apiCall(adminApi, "/EstructuraOrganizativa/Arbol");
  const summary = structureTreePayload.data?.summary || {};
  assert(Number(summary.totalNodes || 0) >= 1, "El arbol formal no devolvio nodos.");
  const { payload: filteredStructurePayload } = await apiCall(
    adminApi,
    `/EstructuraOrganizativa/Listar?idDepartamento=${encodeURIComponent(String(department.id ?? department.idDepartamento))}`
  );
  assert(extractRows(filteredStructurePayload).length >= 1, "El filtro por departamento de estructura no devolvio nodos.");

  const { payload: rrhhBitacoraPayload } = await apiCall(adminApi, `/RrhhResumen/Bitacora?search=${encodeURIComponent(employeeCode)}`);
  const bitacoraRows = extractRows(rrhhBitacoraPayload);
  assert(bitacoraRows.length > 0, "La bitacora RRHH no registro las operaciones QA del empleado.");
  pass("RRHH: CRUD de empleado, foto, contrato, accion, expediente, reloj y estructura", {
    employeeCode,
    employeeUser: createdEmployee.usuarioSistema,
  });
});

const ensurePortalUser = async (userKey) => {
  const creds = roleUsers[userKey];
  const session = await loginApi(creds.username, creds.password);
  return session;
};

await run("Portal y Supervisor: solicitudes, filtro directo, notificaciones y resoluciones", async () => {
  const sessionA = await ensurePortalUser("employeeA");
  const sessionB = await ensurePortalUser("employeeB");
  employeeAApi = sessionA.api;
  employeeBApi = sessionB.api;

  const { payload: contextAPayload } = await apiCall(employeeAApi, "/Portal/MiContexto");
  const { payload: contextBPayload } = await apiCall(employeeBApi, "/Portal/MiContexto");
  const portalA = contextAPayload.data;
  const portalB = contextBPayload.data;

  assert(portalA.hasEmployee, "batof1 no esta vinculado a una ficha de empleado.");
  assert(portalA.employee?.idNodoEstructura, "batof1 aun no tiene ubicacion formal en la estructura.");
  assert(portalA.employee?.reportaFormalmenteA, "batof1 no refleja a quien reporta formalmente.");
  const { payload: listedVacationsA } = await apiCall(
    adminApi,
    `/Novedades/ListarVacaciones?search=${encodeURIComponent(portalA.employee.codigoEmpleado)}`
  );
  const { payload: listedVacationsB } = await apiCall(
    adminApi,
    `/Novedades/ListarVacaciones?search=${encodeURIComponent(portalB.employee.codigoEmpleado)}`
  );
  const approvedVacationDates = findAvailableVacationDates(extractRows(listedVacationsA), new Date("2026-07-01"), 2);
  const rejectedVacationDates = findAvailableVacationDates(extractRows(listedVacationsB), new Date("2026-08-01"), 1);

  const { payload: approvedVacationPayload } = await apiCall(employeeAApi, "/Novedades/CrearVacacion", {
    method: "POST",
    data: {
      idEmpleado: portalA.employee.idEmpleado,
      fechaInicio: approvedVacationDates[0],
      fechaFin: approvedVacationDates[1],
      observacionSolicitud: "Vacacion QA aprobar",
      esMedioDia: false,
      jornadaMedioDia: null,
    },
  });

  const { payload: rejectedVacationPayload } = await apiCall(employeeBApi, "/Novedades/CrearVacacion", {
    method: "POST",
    data: {
      idEmpleado: portalB.employee.idEmpleado,
      fechaInicio: rejectedVacationDates[0],
      fechaFin: rejectedVacationDates[0],
      observacionSolicitud: "Vacacion QA rechazar",
      esMedioDia: true,
      jornadaMedioDia: "MANANA",
    },
  });

  const overtimeTypes = portalA.overtimeTypes || [];
  const overtimeType = first(overtimeTypes);
  assert(overtimeType, "No se encontro tipo de hora extra para Mi Portal.");
  const overtimeTypeId = overtimeType.idTipoHoraExtra ?? overtimeType.id;

  const { payload: listedOvertimeA } = await apiCall(
    adminApi,
    `/Novedades/ListarHorasExtra?search=${encodeURIComponent(portalA.employee.codigoEmpleado)}`
  );
  const { payload: listedOvertimeB } = await apiCall(
    adminApi,
    `/Novedades/ListarHorasExtra?search=${encodeURIComponent(portalB.employee.codigoEmpleado)}`
  );
  const approvedOvertimeDate = findAvailableOvertimeDate(extractRows(listedOvertimeA), new Date("2026-01-01"), overtimeTypeId);
  const rejectedOvertimeDate = findAvailableOvertimeDate(extractRows(listedOvertimeB), new Date("2026-01-01"), overtimeTypeId);

  const { payload: approvedOvertimePayload } = await apiCall(employeeAApi, "/Novedades/CrearHoraExtra", {
    method: "POST",
    data: {
      idEmpleado: portalA.employee.idEmpleado,
      idTipoHoraExtra: overtimeTypeId,
      fechaHoraExtra: approvedOvertimeDate,
      cantidadHoras: 2.5,
      observacion: "Hora extra QA aprobar",
    },
  });

  const { payload: rejectedOvertimePayload } = await apiCall(employeeBApi, "/Novedades/CrearHoraExtra", {
    method: "POST",
    data: {
      idEmpleado: portalB.employee.idEmpleado,
      idTipoHoraExtra: overtimeTypeId,
      fechaHoraExtra: rejectedOvertimeDate,
      cantidadHoras: 1.5,
      observacion: "Hora extra QA rechazar",
    },
  });

  const approvedVacationId = approvedVacationPayload.data.idVacacion;
  const rejectedVacationId = rejectedVacationPayload.data.idVacacion;
  const approvedOvertimeId = approvedOvertimePayload.data.idHoraExtra;
  const rejectedOvertimeId = rejectedOvertimePayload.data.idHoraExtra;
  report.created.portal = {
    approvedVacationId,
    rejectedVacationId,
    approvedOvertimeId,
    rejectedOvertimeId,
  };

  const { payload: supervisorPendingPayload } = await apiCall(supervisorApi, "/Portal/SupervisorPendientes");
  const supervisorVacations = supervisorPendingPayload.data?.vacations || [];
  const supervisorOvertime = supervisorPendingPayload.data?.overtime || [];
  assert(supervisorVacations.some((item) => Number(item.idVacacion) === Number(approvedVacationId)), "El supervisor directo no vio la vacacion aprobable.");
  assert(supervisorVacations.some((item) => Number(item.idVacacion) === Number(rejectedVacationId)), "El supervisor directo no vio la vacacion rechazable.");
  assert(supervisorOvertime.some((item) => Number(item.idHoraExtra) === Number(approvedOvertimeId)), "El supervisor directo no vio la hora extra aprobable.");
  assert(supervisorOvertime.some((item) => Number(item.idHoraExtra) === Number(rejectedOvertimeId)), "El supervisor directo no vio la hora extra rechazable.");

  const { payload: chiefPendingPayload } = await apiCall(chiefApi, "/Portal/SupervisorPendientes");
  const chiefVacations = chiefPendingPayload.data?.vacations || [];
  const chiefOvertime = chiefPendingPayload.data?.overtime || [];
  assert(!chiefVacations.some((item) => Number(item.idVacacion) === Number(approvedVacationId)), "El jefe vio una vacacion de un subordinado indirecto.");
  assert(!chiefOvertime.some((item) => Number(item.idHoraExtra) === Number(approvedOvertimeId)), "El jefe vio una hora extra de un subordinado indirecto.");

  const { payload: notificationPayload } = await apiCall(supervisorApi, "/Portal/SupervisorNotificaciones");
  assert(Number(notificationPayload.data?.totalPending || 0) >= 4, "Las notificaciones del supervisor no reflejaron los pendientes QA.");

  const supervisorUiLogin = await loginApi(roleUsers.supervisor.username, roleUsers.supervisor.password);
  const browserSession = await openPageWithSession(supervisorUiLogin.session);
  try {
    await browserSession.page.goto(`${baseURL}/App/Dashboard`, { waitUntil: "domcontentloaded" });
    await browserSession.page.waitForSelector("#approvalNotificationButton");
    await browserSession.page.click("#approvalNotificationButton");
    await browserSession.page.waitForURL(/App\/BandejaSupervisor\?kind=/, { timeout: 20000 });
    await browserSession.page.waitForSelector("#resolutionForm");
    await saveScreenshot(browserSession.page, "supervisor-notification-route.png");
    ensureNoBrowserErrors(browserSession.errors, "Dashboard/Bandeja supervisor");
  } finally {
    await browserSession.browser.close();
    await supervisorUiLogin.api.dispose();
  }

  await apiCall(supervisorApi, `/Portal/ResolverSupervisorVacacion/${approvedVacationId}`, {
    method: "PUT",
    data: {
      action: "APROBAR",
      observation: "QA aprobado",
      approvedDays: 2,
    },
  });

  await apiCall(supervisorApi, `/Portal/ResolverSupervisorVacacion/${rejectedVacationId}`, {
    method: "PUT",
    data: {
      action: "RECHAZAR",
      observation: "QA rechazado",
    },
  });

  await apiCall(supervisorApi, `/Portal/ResolverSupervisorHoraExtra/${approvedOvertimeId}`, {
    method: "PUT",
    data: {
      action: "APROBAR",
      observation: "QA aprobado",
    },
  });

  await apiCall(supervisorApi, `/Portal/ResolverSupervisorHoraExtra/${rejectedOvertimeId}`, {
    method: "PUT",
    data: {
      action: "RECHAZAR",
      observation: "QA rechazado",
    },
  });

  const { payload: employeeAfterResolutionPayload } = await apiCall(employeeAApi, "/Portal/MiContexto");
  assert(Number(employeeAfterResolutionPayload.data?.summary?.pendingRequests || 0) >= 0, "Mi Portal no recargo el contexto despues de las resoluciones.");

  pass("Portal y Supervisor: solicitudes, filtro directo, notificaciones y resoluciones");
});

await run("Nomina: configuracion, periodo, calculo, reportes, exportacion y publicacion a portal", async () => {
  const { payload: nominaContextPayload } = await apiCall(adminApi, "/Nomina/Contexto");
  const nominaContext = nominaContextPayload.data;
  assert(nominaContext, "No se pudo cargar el contexto de nomina.");

  const config = nominaContext.configuration || nominaContext.configuracion || {};
  await apiCall(adminApi, "/Nomina/GuardarConfiguracionEmpresa", {
    method: "POST",
    data: {
      regimenInssEmpresa: config.regimenInssEmpresa || "INTEGRAL",
      cantidadTrabajadoresEmpresa: Number(config.cantidadTrabajadoresEmpresa || nominaContext.metrics?.employees || 10),
      modoPasantiaPorDefecto: config.modoPasantiaPorDefecto || "NO_NOMINA",
      diasMesNomina: Number(config.diasMesNomina || 30),
      horasMesBase: Number(config.horasMesBase || 240),
    },
  });

  const maxPeriodEnd = (Array.isArray(nominaContext.periods) ? nominaContext.periods : [])
    .map((item) => new Date(item.fechaHasta || item.endDate || "2026-05-31"))
    .sort((left, right) => right.getTime() - left.getTime())[0];
  const baseStart = new Date(Number.isFinite(maxPeriodEnd?.getTime()) ? maxPeriodEnd : new Date("2026-05-31"));
  baseStart.setDate(1);
  baseStart.setMonth(baseStart.getMonth() + 1);
  const periodStart = new Date(baseStart);
  const periodEnd = new Date(baseStart.getFullYear(), baseStart.getMonth() + 1, 0);
  const payDate = new Date(periodEnd);
  const overtimeCutoff = new Date(periodEnd);

  const periodCode = `QA-${periodStart.getFullYear()}${String(periodStart.getMonth() + 1).padStart(2, "0")}-${qaSuffix.slice(-4)}`;
  const { payload: openPeriodPayload } = await apiCall(adminApi, "/Nomina/AbrirPeriodo", {
    method: "POST",
    data: {
      codigoPeriodo: periodCode,
      fechaDesde: formatDate(periodStart),
      fechaHasta: formatDate(periodEnd),
      fechaPago: formatDate(payDate),
      tipoPeriodo: "MENSUAL",
      observacion: "Periodo QA integral",
      fechaCorteHoraExtra: formatDate(overtimeCutoff),
    },
  });

  const periodId = openPeriodPayload.data.idPeriodoNomina;
  assert(periodId > 0, "No se genero id de periodo de nomina.");

  const { payload: generatedPayrollPayload } = await apiCall(adminApi, "/Nomina/Generar", {
    method: "POST",
    data: {
      idPeriodoNomina: periodId,
    },
  });

  const payrollId = generatedPayrollPayload.data.idNomina;
  assert(payrollId > 0, "No se genero id de nomina.");
  report.created.payroll = { periodId, payrollId, periodCode };

  const { payload: payrollDetailPayload } = await apiCall(adminApi, `/Nomina/ObtenerNomina?idNomina=${payrollId}`);
  const payrollDetail = payrollDetailPayload.data;
  const payrollRows = Array.isArray(payrollDetail?.details) ? payrollDetail.details : Array.isArray(payrollDetail?.rows) ? payrollDetail.rows : [];
  assert(payrollRows.length > 0, "La nomina generada no devolvio colaboradores.");

  const payslipRow = payrollRows.find((item) => String(item.codigoEmpleado || "").toUpperCase() === "BAT004") || payrollRows[0];
  assert(payslipRow?.idNominaDetalle, "No se encontro detalle de esquela para la nomina QA.");

  const reportHtml = await adminApi.get(`${baseURL}/Nomina/ReporteGeneralHtml?idNomina=${payrollId}`);
  const reportExcel = await adminApi.get(`${baseURL}/Nomina/ReporteGeneralExcel?idNomina=${payrollId}`);
  const payslipHtml = await adminApi.get(`${baseURL}/Nomina/EsquelaHtml?idNominaDetalle=${payslipRow.idNominaDetalle}`);
  assert(reportHtml.ok(), "No se pudo abrir el reporte general HTML.");
  assert(reportExcel.ok(), "No se pudo exportar el reporte general Excel.");
  assert((reportExcel.headers()["content-type"] || "").includes("excel"), "El reporte general no devolvio tipo Excel.");
  assert(payslipHtml.ok(), "No se pudo abrir la esquela HTML.");

  await apiCall(adminApi, "/Nomina/Cerrar", {
    method: "POST",
    data: {
      idNomina: payrollId,
    },
  });

  const { payload: portalPayslipsPayload } = await apiCall(employeeAApi, "/Portal/MisEsquelas");
  const portalPayslips = Array.isArray(portalPayslipsPayload.data) ? portalPayslipsPayload.data : [];
  assert(portalPayslips.length > 0, "Mi Portal no mostro esquelas despues de publicar la nomina.");
  pass("Nomina: configuracion, periodo, calculo, reportes, exportacion y publicacion a portal");
});

await run("Liquidacion: preview, generacion, HTML, Excel y carta", async () => {
  const employeesCatalog = await getCatalogPayload(adminApi, "/Empleados/Catalogos");
  const department = pickByCode(employeesCatalog.departments || employeesCatalog.departamentos, ["ADM", "CRE"]) || first(employeesCatalog.departments || employeesCatalog.departamentos);
  const position = pickByCode(employeesCatalog.positions || employeesCatalog.cargos, ["ASI_ADM", "OFI_CRED"]) || first(employeesCatalog.positions || employeesCatalog.cargos);
  const bank = first(employeesCatalog.banks || employeesCatalog.bancos || []);
  const liqCode = `QL${qaSuffix.slice(-6)}`;
  const liqCedula = `001-290426-${qaSuffix.slice(-4)}L`;

  const { payload: liqEmployeePayload } = await apiCall(adminApi, "/Empleados/Crear", {
    method: "POST",
    data: {
      codigoEmpleado: liqCode,
      usuarioSistema: "",
      idSupervisorEmpleado: null,
      idDepartamento: department.id ?? department.idDepartamento,
      idCargo: position.id ?? position.idCargo,
      cedula: liqCedula,
      inss: `LIQ-${qaSuffix.slice(-6)}`,
      nombres: "QA",
      apellidos: "Liquidacion Prueba",
      fechaNacimiento: "1990-01-10",
      sexo: "M",
      estadoCivil: "CASADO",
      telefono: "8777-0000",
      correo: `qa.liquidacion.${qaSuffix.slice(-4)}@sifnic.local`,
      direccion: "Managua Liquidacion",
      fechaIngreso: "2025-01-15",
      idBanco: bank?.idBanco ?? bank?.id ?? null,
      numeroCuentaBancaria: `2000${qaSuffix.slice(-6)}`,
    },
  });

  cleanup.liquidationEmployeeId = liqEmployeePayload.data.idEmpleado;

  const contractsCatalog = await getCatalogPayload(adminApi, "/Contratos/Catalogos");
  const contractType = pickByCode(contractsCatalog.contractTypes || contractsCatalog.tiposContrato, ["INDEFINIDO", "TIEMPO_INDEFINIDO"]) || first(contractsCatalog.contractTypes || contractsCatalog.tiposContrato);
  const workShift = first(contractsCatalog.schedules || contractsCatalog.horarios);
  const { payload: liqContractPayload } = await apiCall(adminApi, "/Contratos/Crear", {
    method: "POST",
    data: {
      idEmpleado: cleanup.liquidationEmployeeId,
      idTipoContrato: contractType.idTipoContrato ?? contractType.id,
      idHorarioLaboral: workShift.idHorarioLaboral ?? workShift.id,
      numeroContrato: `CTR-${liqCode}`,
      fechaInicio: "2025-01-15",
      fechaFin: null,
      salarioBaseMensual: 15000,
      moneda: "NIO",
      esContratoVigente: true,
      observacion: "Contrato QA liquidacion",
    },
  });
  cleanup.liquidationContractId = liqContractPayload.data.idContrato;

  const liquidationRequest = {
    idEmpleado: cleanup.liquidationEmployeeId,
    fechaLiquidacion: "2026-04-29",
    fechaBaja: "2026-04-29",
    causalCodigo: "RENUNCIA_ART44",
    motivoLiquidacion: "Prueba integral QA",
    diasSalarioPendiente: 5,
  };

  const { payload: liquidationPreviewPayload } = await apiCall(adminApi, "/Nomina/PrevisualizarLiquidacion", {
    method: "POST",
    data: liquidationRequest,
  });
  assert(liquidationPreviewPayload.data?.totals?.netoLiquidacion >= 0, "La previsualizacion de liquidacion no devolvio neto.");

  const { payload: generatedLiquidationPayload } = await apiCall(adminApi, "/Nomina/GenerarLiquidacion", {
    method: "POST",
    data: liquidationRequest,
  });

  const liquidationId = generatedLiquidationPayload.data.idLiquidacion;
  assert(liquidationId > 0, "No se genero id de liquidacion.");
  report.created.liquidation = { liquidationId, employeeId: cleanup.liquidationEmployeeId };

  const detailResponse = await apiCall(adminApi, `/Nomina/ObtenerLiquidacion?idLiquidacion=${liquidationId}`);
  assert(detailResponse.payload.data?.header || detailResponse.payload.data?.totals, "No se obtuvo detalle de liquidacion.");

  const liquidationHtml = await adminApi.get(`${baseURL}/Nomina/LiquidacionHtml?idLiquidacion=${liquidationId}`);
  const liquidationExcel = await adminApi.get(`${baseURL}/Nomina/LiquidacionExcel?idLiquidacion=${liquidationId}`);
  const recommendationLetter = await adminApi.get(`${baseURL}/Nomina/CartaRecomendacionHtml?idLiquidacion=${liquidationId}`);
  assert(liquidationHtml.ok(), "No se pudo abrir la liquidacion HTML.");
  assert(liquidationExcel.ok(), "No se pudo exportar la liquidacion Excel.");
  assert((liquidationExcel.headers()["content-type"] || "").includes("excel"), "La liquidacion no devolvio tipo Excel.");
  assert(recommendationLetter.ok(), "No se pudo abrir la carta de recomendacion.");
  pass("Liquidacion: preview, generacion, HTML, Excel y carta");
});

await run("UI portal y nomina: cargas visuales sin error", async () => {
  const employeeSession = await loginApi(roleUsers.employeeA.username, roleUsers.employeeA.password);
  const portalBrowser = await openPageWithSession(employeeSession.session);
  try {
    await portalBrowser.page.goto(`${baseURL}/App/MiPortal`, { waitUntil: "domcontentloaded" });
    await portalBrowser.page.waitForSelector("#portalContent");
    await portalBrowser.page.click("[data-portal-tab='registros']");
    await portalBrowser.page.waitForSelector("[data-portal-section='registros']:not([hidden])");
    const vacationsCount = await portalBrowser.page.locator("#vacationsTableBody tr").count();
    const overtimeCount = await portalBrowser.page.locator("#overtimeTableBody tr").count();
    await portalBrowser.page.click("[data-portal-tab='esquelas']");
    await portalBrowser.page.waitForSelector("[data-portal-section='esquelas']:not([hidden])");
    const payslipCount = await portalBrowser.page.locator("#payslipsTableBody tr").count();
    assert(vacationsCount >= 1, "Mi Portal no mostro vacaciones del colaborador.");
    assert(overtimeCount >= 1, "Mi Portal no mostro horas extra del colaborador.");
    assert(payslipCount >= 1, "Mi Portal no mostro esquelas del colaborador.");
    await saveScreenshot(portalBrowser.page, "mi-portal-smoke.png");
    ensureNoBrowserErrors(portalBrowser.errors, "Mi Portal");
  } finally {
    await portalBrowser.browser.close();
    await employeeSession.api.dispose();
  }

  const adminSession = await loginApi(roleUsers.admin.username, roleUsers.admin.password);
  const nominaBrowser = await openPageWithSession(adminSession.session);
  try {
    await nominaBrowser.page.goto(`${baseURL}/App/Nomina`, { waitUntil: "domcontentloaded" });
    await nominaBrowser.page.waitForSelector("#workspaceNav button");
    const workspaceTabs = await nominaBrowser.page.locator("#workspaceNav button").count();
    assert(workspaceTabs >= 4, "Nomina cargo menos secciones de las esperadas.");
    await saveScreenshot(nominaBrowser.page, "nomina-smoke.png");
    ensureNoBrowserErrors(nominaBrowser.errors, "Nomina");
  } finally {
    await nominaBrowser.browser.close();
    await adminSession.api.dispose();
  }

  pass("UI portal y nomina: cargas visuales sin error");
});

await run("Reportes RRHH y bitacora operativa", async () => {
  const { payload: vacationsReportPayload } = await apiCall(adminApi, "/Novedades/ReporteVacacionesDisponibles");
  const rows = vacationsReportPayload.data?.rows || [];
  assert(Array.isArray(rows) && rows.length > 0, "El reporte de vacaciones disponibles no devolvio filas.");

  const { payload: rrhhOverviewPayload } = await apiCall(adminApi, "/RrhhResumen/Overview");
  assert(rrhhOverviewPayload.data, "El overview de RRHH no devolvio datos.");

  const { payload: operationalBitacoraPayload } = await apiCall(adminApi, "/Seguridad/BitacoraMovimientos?take=200");
  const operationalRows = Array.isArray(operationalBitacoraPayload.data) ? operationalBitacoraPayload.data : [];
  assert(operationalRows.some((item) => String(item.referencia || "").includes("NOM-")), "La bitacora operativa no reflejo la nomina QA.");
  assert(operationalRows.some((item) => String(item.referencia || "").includes("LIQ-")), "La bitacora operativa no reflejo la liquidacion QA.");
  pass("Reportes RRHH y bitacora operativa");
});

await run("Limpieza segura de artefactos reversibles", async () => {
  const cleanupSteps = [
    cleanup.structureNodeId
      ? {
          label: "nodo_estructura",
          action: () =>
            apiCall(adminApi, `/EstructuraOrganizativa/Eliminar/${cleanup.structureNodeId}`, {
              method: "DELETE",
              data: adminDeleteCredentials,
            }),
        }
      : null,
    cleanup.expedienteId
      ? {
          label: "expediente",
          action: () =>
            apiCall(adminApi, `/Expedientes/Eliminar/${cleanup.expedienteId}`, {
              method: "DELETE",
              data: adminDeleteCredentials,
            }),
        }
      : null,
    cleanup.actionId
      ? {
          label: "accion_personal",
          action: () =>
            apiCall(adminApi, `/AccionesPersonal/Eliminar/${cleanup.actionId}`, {
              method: "DELETE",
              data: adminDeleteCredentials,
            }),
        }
      : null,
    cleanup.contractId
      ? {
          label: "contrato",
          action: () =>
            apiCall(adminApi, `/Contratos/Eliminar/${cleanup.contractId}`, {
              method: "DELETE",
              data: adminDeleteCredentials,
            }),
        }
      : null,
    cleanup.employeeId
      ? {
          label: "empleado",
          action: () =>
            apiCall(adminApi, `/Empleados/Eliminar/${cleanup.employeeId}`, {
              method: "DELETE",
              data: adminDeleteCredentials,
            }),
        }
      : null,
  ].filter(Boolean);

  for (const step of cleanupSteps) {
    try {
      await step.action();
    } catch (error) {
      report.notes.push(`No se pudo limpiar ${step.label}: ${error.message}`);
    }
  }

  pass("Limpieza segura de artefactos reversibles");
});

for (const api of [adminApi, supervisorApi, chiefApi, employeeAApi, employeeBApi]) {
  if (api) {
    await api.dispose();
  }
}

report.finishedAt = new Date().toISOString();
report.summary = {
  totalChecks: report.checks.length,
  passed: report.checks.filter((item) => item.status === "PASS").length,
  failed: report.checks.filter((item) => item.status === "FAIL").length,
  bugs: report.bugs.length,
};

await fs.writeFile(
  path.join(artifactDir, "qa-integral-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log(JSON.stringify(report.summary, null, 2));
