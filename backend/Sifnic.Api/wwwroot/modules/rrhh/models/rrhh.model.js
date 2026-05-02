window.RRHHModel = (() => {
  const SESSION_KEY = "sifnic.session";

  const groups = [
    {
      id: "empleados",
      label: "Capital Humano",
      subtitle: "Centro operativo de personas, expedientes, estructura, asistencia y reportes.",
      buckets: [
        {
          id: "personal",
          label: "Operacion diaria",
          subtitle: "Colaboradores, movimientos, contratos y expedientes",
          modules: [
            {
              id: "empleado",
              label: "Empleados",
              subtitle: "Altas, consulta y ficha laboral",
              code: "EMP",
              schema: "rrhh",
              table: "empleado",
              type: "crud",
              procedures: ["rrhh.usp_crear_empleado"],
              cards: [
                { title: "Tabla principal", detail: "rrhh.empleado" },
                { title: "Operacion", detail: "Altas, modificaciones y eliminacion" },
                { title: "Bitacora", detail: "Insercion, modificacion y eliminacion" },
              ],
            },
            {
              id: "accion_personal",
              label: "Accion personal",
              subtitle: "Movimientos y cambios formales",
              code: "ACP",
              schema: "rrhh",
              table: "accion_personal",
              type: "action",
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.accion_personal" },
                { title: "Uso", detail: "Traslados, promociones y cambios internos" },
                { title: "Estado", detail: "Operacion activa con auditoria" },
              ],
            },
            {
              id: "contrato",
              label: "Contratos",
              subtitle: "Vigencias, altas y renovaciones",
              code: "CTR",
              schema: "rrhh",
              table: "contrato",
              type: "crud",
              procedures: ["rrhh.usp_registrar_contrato"],
              cards: [
                { title: "Tabla", detail: "rrhh.contrato" },
                { title: "Proceso", detail: "Registro de contrato" },
                { title: "Relacion", detail: "tipo_contrato y empleado" },
              ],
            },
            {
              id: "tipo_contrato",
              label: "Tipos de contrato",
              subtitle: "Catalogo contractual",
              code: "TPC",
              schema: "rrhh",
              table: "tipo_contrato",
              type: "catalog",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.tipo_contrato" },
                { title: "Uso", detail: "Parametros de contratos" },
                { title: "Estado", detail: "Catalogo base" },
              ],
            },
            {
              id: "estado_empleado",
              label: "Estados de empleado",
              subtitle: "Activo, suspendido, retirado y vacaciones",
              code: "EST",
              schema: "rrhh",
              table: "estado_empleado",
              type: "catalog",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.estado_empleado" },
                { title: "Uso", detail: "Control operativo del colaborador" },
                { title: "Estado", detail: "Catalogo base" },
              ],
            },
            {
              id: "expediente_documento",
              label: "Expedientes",
              subtitle: "Documentos del colaborador",
              code: "EXP",
              schema: "rrhh",
              table: "expediente_documento",
              type: "document",
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.expediente_documento" },
                { title: "Uso", detail: "Archivo y validacion documental" },
                { title: "Estado", detail: "Operacion activa con archivo adjunto" },
              ],
            },
          ],
        },
        {
          id: "estructura",
          label: "Organizacion",
          subtitle: "Organigrama formal, cargos, areas y catalogos",
          modules: [
            {
              id: "estructura_empresa",
              label: "Organigrama formal",
              subtitle: "Nodos institucionales, vacantes, titulares y arbol corporativo",
              code: "ORG",
              schema: "rrhh",
              table: "estructura_organizativa_nodo",
              type: "structure",
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.estructura_organizativa_nodo" },
                { title: "Incluye", detail: "Asamblea, junta, gerencias, unidades, puestos y vacantes" },
                { title: "Uso", detail: "Administracion del organigrama formal por ramas y departamentos" },
              ],
            },
            {
              id: "departamento",
              label: "Departamentos",
              subtitle: "Areas internas de la empresa",
              code: "DPT",
              schema: "rrhh",
              table: "departamento",
              type: "catalog",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.departamento" },
                { title: "Uso", detail: "Estructura organizativa" },
                { title: "Estado", detail: "Catalogo activo" },
              ],
            },
            {
              id: "cargo",
              label: "Cargos",
              subtitle: "Puestos de trabajo",
              code: "CRG",
              schema: "rrhh",
              table: "cargo",
              type: "catalog",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.cargo" },
                { title: "Uso", detail: "Puestos y responsabilidades" },
                { title: "Estado", detail: "Catalogo activo" },
              ],
            },
            {
              id: "horario_laboral",
              label: "Horarios laborales",
              subtitle: "Turnos y jornadas",
              code: "HRL",
              schema: "rrhh",
              table: "horario_laboral",
              type: "catalog",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.horario_laboral" },
                { title: "Uso", detail: "Turnos y asignacion de jornada" },
                { title: "Estado", detail: "Catalogo activo" },
              ],
            },
            {
              id: "banco",
              label: "Bancos",
              subtitle: "Catalogo bancario del colaborador",
              code: "BNK",
              schema: "rrhh",
              table: "banco",
              type: "catalog",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.banco" },
                { title: "Uso", detail: "Pago de nomina y cuentas" },
                { title: "Estado", detail: "Catalogo activo" },
              ],
            },
          ],
        },
        {
          id: "novedades",
          label: "Asistencia y novedades",
          subtitle: "Vacaciones, reloj, incidencias y movimientos operativos",
          modules: [
            {
              id: "configuracion_rrhh",
              label: "Configuraciones RRHH",
              subtitle: "Catalogos, reglas base y parametros operativos",
              code: "CFG",
              schema: "rrhh",
              table: "catalogos_base",
              type: "config",
              externalUrl: "/App/Configuracion",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Uso", detail: "Acceso centralizado a catalogos del modulo" },
                { title: "Incluye", detail: "Contratos, estructura, bancos y recargos" },
                { title: "Estado", detail: "Vista administrativa separada del tablero operativo" },
              ],
            },
            {
              id: "reloj",
              label: "Reloj",
              subtitle: "Marcaciones, control y reporte",
              code: "RLJ",
              schema: "rrhh",
              table: "marcacion_reloj",
              type: "clock",
              procedures: [],
              cards: [
                { title: "Control", detail: "Entradas, salidas y horas trabajadas" },
                { title: "Uso", detail: "Reloj por cedula y reporte operativo" },
                { title: "Reporte", detail: "Visualizacion y exportacion" },
              ],
            },
            {
              id: "bitacora_rrhh",
              label: "Bitacora RRHH",
              subtitle: "Auditoria y movimientos del modulo",
              code: "BIT",
              schema: "operacion",
              table: "bitacora_operativa",
              type: "audit",
              externalUrl: "/App/Configuracion?tab=movimientos",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "operacion.bitacora_operativa" },
                { title: "Uso", detail: "Trazabilidad de operaciones del modulo" },
                { title: "Estado", detail: "Consulta historica y auditoria" },
              ],
            },
            {
              id: "vacacion",
              label: "Vacaciones",
              subtitle: "Solicitud, saldo y aprobacion",
              code: "VAC",
              schema: "rrhh",
              table: "vacacion",
              type: "workflow",
              procedures: ["rrhh.usp_solicitar_vacacion", "rrhh.usp_aprobar_vacacion"],
              cards: [
                { title: "Tabla", detail: "rrhh.vacacion" },
                { title: "Proceso", detail: "Solicitud y aprobacion" },
                { title: "Uso", detail: "Saldo y programacion" },
              ],
            },
            {
              id: "hora_extra",
              label: "Horas extra",
              subtitle: "Registro y autorizacion",
              code: "HEX",
              schema: "rrhh",
              table: "hora_extra",
              type: "workflow",
              procedures: ["rrhh.usp_registrar_hora_extra", "rrhh.usp_aprobar_hora_extra"],
              cards: [
                { title: "Tabla", detail: "rrhh.hora_extra" },
                { title: "Proceso", detail: "Registro y aprobacion" },
                { title: "Relacion", detail: "tipo_hora_extra y empleado" },
              ],
            },
            {
              id: "tipo_hora_extra",
              label: "Tipos de hora extra",
              subtitle: "Catalogo de recargos",
              code: "THX",
              schema: "rrhh",
              table: "tipo_hora_extra",
              type: "catalog",
              hiddenOnBoard: true,
              procedures: [],
              cards: [
                { title: "Tabla", detail: "rrhh.tipo_hora_extra" },
                { title: "Uso", detail: "Clasificacion de recargos" },
                { title: "Estado", detail: "Catalogo base" },
              ],
            },
          ],
        },
        {
          id: "reportes",
          label: "Consultas y reportes",
          subtitle: "Indicadores, saldos, horas y exportaciones",
          modules: [
            {
              id: "reporte_vacaciones_disponibles",
              label: "Vacaciones disponibles",
              subtitle: "Saldo disponible y fecha de ingreso por colaborador",
              code: "RVD",
              schema: "rrhh",
              table: "empleado/vacacion",
              type: "report",
              procedures: [],
              cards: [
                { title: "Vista", detail: "Saldo de vacaciones por empleado" },
                { title: "Incluye", detail: "Fecha de ingreso, contrato y dias disponibles" },
                { title: "Salida", detail: "Consulta en pantalla, Excel y PDF" },
              ],
            },
            {
              id: "reporte_horas_trabajadas",
              label: "Horas trabajadas",
              subtitle: "Dashboard mensual de asistencia, jornada y horas extra",
              code: "RHT",
              schema: "rrhh",
              table: "marcacion_reloj",
              type: "hours_report",
              procedures: [],
              cards: [
                { title: "Base", detail: "rrhh.marcacion_reloj" },
                { title: "Cruce", detail: "Horario vigente del contrato" },
                { title: "Salida", detail: "Dashboard, Excel y PDF" },
              ],
            },
          ],
        },
      ],
    },
  ];

  const statusOptions = [
    { value: "TODOS", label: "Todos" },
    { value: "ACTIVO", label: "Activos" },
    { value: "SUSPENDIDO", label: "Suspendidos" },
    { value: "RETIRADO", label: "Retirados" },
    { value: "VACACIONES", label: "Vacaciones" },
  ];

  const contractStatusOptions = [
    { value: "TODOS", label: "Todos" },
    { value: "VIGENTES", label: "Vigentes" },
    { value: "POR_VENCER", label: "Por vencer" },
    { value: "TEMPORALES", label: "Temporales" },
    { value: "TEMPORALES_POR_VENCER", label: "Temporales por vencer" },
    { value: "HISTORICOS", label: "Historicos" },
  ];

  const workflowStatusOptions = [
    { value: "TODOS", label: "Todos" },
    { value: "PENDIENTES", label: "Pendientes" },
    { value: "APROBADOS", label: "Aprobados" },
    { value: "RECHAZADOS", label: "Rechazados" },
  ];

  const catalogStatusOptions = [
    { value: "ACTIVOS", label: "Activos" },
    { value: "INACTIVOS", label: "Inactivos" },
    { value: "TODOS", label: "Todos" },
  ];

  const actionStatusOptions = [
    { value: "TODOS", label: "Todos" },
    { value: "HOY", label: "Hoy" },
    { value: "30DIAS", label: "Ultimos 30 dias" },
    { value: "90DIAS", label: "Ultimos 90 dias" },
  ];

  const allowedActionTypes = new Set([
    "PROMOCION",
    "TRASLADO",
    "CAMBIO SALARIAL",
    "PRORROGA CONTRATO",
    "CAMBIO HORARIO",
  ]);

  const documentStatusOptions = [
    { value: "TODOS", label: "Todos" },
    { value: "VIGENTES", label: "Vigentes" },
    { value: "POR_VENCER", label: "Por vencer" },
    { value: "VENCIDOS", label: "Vencidos" },
    { value: "SIN_ARCHIVO", label: "Sin archivo" },
  ];

  const sexoOptions = [
    { value: "F", label: "Femenino" },
    { value: "M", label: "Masculino" },
  ];

  const estadoCivilOptions = [
    { value: "SOLTERO", label: "Soltero" },
    { value: "CASADO", label: "Casado" },
    { value: "UNION DE HECHO", label: "Union de hecho" },
    { value: "DIVORCIADO", label: "Divorciado" },
    { value: "VIUDO", label: "Viudo" },
  ];

  const catalogModuleConfigs = {
    tipo_contrato: {
      noun: "Tipo de contrato",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 100,
      usesDescription: true,
      descriptionLabel: "Descripcion",
    },
    estado_empleado: {
      noun: "Estado de empleado",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 100,
      usesDescription: false,
    },
    departamento: {
      noun: "Departamento",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 150,
      usesDescription: true,
      descriptionLabel: "Descripcion",
    },
    cargo: {
      noun: "Cargo",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 150,
      usesDescription: true,
      descriptionLabel: "Descripcion",
      usesRelatedId: true,
      relatedLabel: "Departamento",
      usesIntegerValue1: true,
      integerLabel: "Nivel jerarquico",
    },
    horario_laboral: {
      noun: "Horario laboral",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 100,
      usesDescription: false,
      usesNumberValue1: true,
      number1Label: "Horas semanales",
      usesNumberValue2: true,
      number2Label: "Horas diarias",
    },
    banco: {
      noun: "Banco",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 150,
      usesDescription: false,
    },
    tipo_permiso: {
      noun: "Tipo de permiso",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 100,
      usesDescription: false,
      usesFlagValue1: true,
      flagLabel: "Afecta salario",
    },
    tipo_hora_extra: {
      noun: "Tipo de hora extra",
      codeLabel: "Codigo",
      nameLabel: "Nombre",
      nameMaxLength: 100,
      usesDescription: false,
      usesNumberValue1: true,
      number1Label: "Factor de pago",
    },
  };

  const normalizeText = (value) =>
    String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .trim();

  const getSession = () => {
    try {
      const session = JSON.parse(localStorage.getItem(SESSION_KEY) || "null");
      return session && session.active ? session : null;
    } catch {
      return null;
    }
  };

  const clearSession = () => {
    localStorage.removeItem(SESSION_KEY);
  };

  const enrichModule = (module) => ({
    ...module,
    procedureCount: module.procedures?.length || 0,
  });

  const enrichBucket = (bucket) => {
    const modules = bucket.modules.map(enrichModule);
    return {
      ...bucket,
      modules,
      moduleCount: modules.length,
      procedureCount: modules.reduce((total, module) => total + module.procedureCount, 0),
    };
  };

  const enrichGroup = (group) => {
    const buckets = group.buckets.map(enrichBucket);
    return {
      ...group,
      buckets,
      bucketCount: buckets.length,
      moduleCount: buckets.reduce((total, bucket) => total + bucket.moduleCount, 0),
      procedureCount: buckets.reduce((total, bucket) => total + bucket.procedureCount, 0),
    };
  };

  const getGroups = () => groups.map(enrichGroup);

  const getGroupById = (groupId) => enrichGroup(groups.find((group) => group.id === groupId) || groups[0]);

  const getModuleById = (groupId, moduleId) => {
    const group = getGroupById(groupId);

    for (const bucket of group.buckets) {
      const found = bucket.modules.find((module) => module.id === moduleId);
      if (found) {
        return {
          ...found,
          externalUrl:
            found.schema === "nomina"
              ? "/App/Nomina"
              : found.externalUrl || "",
          bucketLabel: bucket.label,
          groupLabel: group.label,
          bucketProcedureCount: bucket.procedureCount,
        };
      }
    }

    return null;
  };

  const sanitizeCode = (value) =>
    String(value || "")
      .toUpperCase()
      .replace(/[^A-Z0-9-]/g, "")
      .slice(0, 30);

  const sanitizeCatalogCode = (value) =>
    String(value || "")
      .toUpperCase()
      .replace(/[^A-Z0-9_-]/g, "")
      .slice(0, 30);

  const sanitizeContractNumber = (value) =>
    String(value || "")
      .toUpperCase()
      .replace(/[^A-Z0-9-]/g, "")
      .replace(/-{2,}/g, "-")
      .slice(0, 100);

  const sanitizeName = (value) =>
    String(value || "")
      .replace(/[^\p{L} ]/gu, "")
      .replace(/\s{2,}/g, " ")
      .trimStart()
      .slice(0, 150);

  const sanitizeInss = (value) =>
    String(value || "")
      .toUpperCase()
      .replace(/[^A-Z0-9-]/g, "")
      .replace(/-{2,}/g, "-")
      .slice(0, 20);

  const sanitizeAccount = (value) =>
    String(value || "")
      .replace(/\D/g, "")
      .slice(0, 30);

  const sanitizeEmail = (value) =>
    String(value || "")
      .replace(/\s+/g, "")
      .toLowerCase()
      .slice(0, 150);

  const sanitizeLooseText = (value, maxLength = 100) =>
    String(value || "")
      .replace(/[^\p{L}\p{N} /_-]/gu, "")
      .replace(/\s{2,}/g, " ")
      .trimStart()
      .slice(0, maxLength);

  const sanitizePhone = (value) => {
    const digits = String(value || "")
      .replace(/\D/g, "")
      .slice(0, 8);

    if (digits.length <= 4) {
      return digits;
    }

    return `${digits.slice(0, 4)}-${digits.slice(4)}`;
  };

  const sanitizeCedula = (value) => {
    const raw = String(value || "")
      .toUpperCase()
      .replace(/[^0-9A-Z]/g, "");

    const digits = raw.replace(/[^0-9]/g, "").slice(0, 13);
    const letter = raw.replace(/[^A-Z]/g, "").slice(0, 1);

    if (!digits) {
      return letter;
    }

    if (digits.length <= 3) {
      return `${digits}${letter}`;
    }

    if (digits.length <= 9) {
      return `${digits.slice(0, 3)}-${digits.slice(3)}${letter}`;
    }

    return `${digits.slice(0, 3)}-${digits.slice(3, 9)}-${digits.slice(9, 13)}${letter}`;
  };

  const sanitizeDateInput = (value) => {
    const digits = String(value || "")
      .replace(/\D/g, "")
      .slice(0, 8);

    if (digits.length <= 2) {
      return digits;
    }

    if (digits.length <= 4) {
      return `${digits.slice(0, 2)}/${digits.slice(2)}`;
    }

    return `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
  };

  const isValidDisplayDate = (value) => {
    const normalized = sanitizeDateInput(value);

    if (!/^\d{2}\/\d{2}\/\d{4}$/.test(normalized)) {
      return false;
    }

    const [dayText, monthText, yearText] = normalized.split("/");
    const day = Number(dayText);
    const month = Number(monthText);
    const year = Number(yearText);

    if (!day || !month || !year || month < 1 || month > 12) {
      return false;
    }

    const date = new Date(Date.UTC(year, month - 1, day));
    return (
      date.getUTCFullYear() === year &&
      date.getUTCMonth() === month - 1 &&
      date.getUTCDate() === day
    );
  };

  const displayDateToIso = (value) => {
    const normalized = sanitizeDateInput(value);
    if (!isValidDisplayDate(normalized)) {
      return "";
    }

    const [day, month, year] = normalized.split("/");
    return `${year}-${month}-${day}`;
  };

  const isoDateToDisplay = (value) => {
    const normalized = String(value || "").trim();
    if (!/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
      return "";
    }

    const [year, month, day] = normalized.split("-");
    return `${day}/${month}/${year}`;
  };

  const getEmptyEmployee = (suggestedCode = "") => ({
    employeeId: "",
    codigoEmpleado: sanitizeCode(suggestedCode),
    usuarioSistema: "",
    idSupervisorEmpleado: "",
    cedula: "",
    idDepartamento: "",
    idCargo: "",
    nombres: "",
    apellidos: "",
    fechaIngreso: "",
    fechaNacimiento: "",
    sexo: "",
    estadoCivil: "",
    telefono: "",
    correo: "",
    inss: "",
    idBanco: "",
    numeroCuentaBancaria: "",
    direccion: "",
  });

  const getEmptyContract = (defaultCurrency = "NIO") => ({
    contractId: "",
    idEmpleado: "",
    idTipoContrato: "",
    idHorarioLaboral: "",
    numeroContrato: "",
    fechaInicio: "",
    fechaFin: "",
    salarioBaseMensual: "",
    moneda: String(defaultCurrency || "NIO").trim().toUpperCase(),
    esContratoVigente: true,
    observacion: "",
  });

  const getEmptyCatalog = (moduleId) => ({
    moduleId,
    idCatalogo: "",
    codigo: "",
    nombre: "",
    descripcion: "",
    relatedId: "",
    numberValue1: "",
    numberValue2: "",
    integerValue1: "",
    flagValue1: false,
    activo: true,
  });

  const getEmptyAction = () => ({
    recordId: "",
    idEmpleado: "",
    tipoAccion: "",
    fechaAccion: "",
    idCargoNuevo: "",
    nombreCargoNuevo: "",
    jerarquiaActual: "",
    jerarquiaNueva: "",
    nuevoSalarioBaseMensual: "",
    salarioActual: "",
    monedaSalario: "NIO",
    fechaFinContratoActual: "",
    nuevaFechaFinContrato: "",
    aplicarCambioOperativo: true,
    descripcionAccion: "",
  });

  const getEmptyDocument = () => ({
    recordId: "",
    idEmpleado: "",
    tipoDocumento: "",
    fechaDocumento: "",
    fechaVencimiento: "",
    observacion: "",
    archivo: null,
    removerArchivo: false,
    nombreArchivo: "",
    tieneArchivo: false,
  });

  const buildPayload = (formData) => ({
    codigoEmpleado: sanitizeCode(formData.codigoEmpleado),
    usuarioSistema: String(formData.usuarioSistema || "").trim() || null,
    idSupervisorEmpleado: formData.idSupervisorEmpleado ? Number(formData.idSupervisorEmpleado) : null,
    cedula: sanitizeCedula(formData.cedula),
    idDepartamento: Number(formData.idDepartamento || 0),
    idCargo: Number(formData.idCargo || 0),
    nombres: sanitizeName(formData.nombres),
    apellidos: sanitizeName(formData.apellidos),
    fechaIngreso: displayDateToIso(formData.fechaIngreso),
    fechaNacimiento: displayDateToIso(formData.fechaNacimiento) || null,
    sexo: String(formData.sexo || "").trim().toUpperCase() || null,
    estadoCivil: String(formData.estadoCivil || "").trim().toUpperCase() || null,
    telefono: sanitizePhone(formData.telefono),
    correo: sanitizeEmail(formData.correo),
    inss: sanitizeInss(formData.inss),
    idBanco: formData.idBanco ? Number(formData.idBanco) : null,
    numeroCuentaBancaria: sanitizeAccount(formData.numeroCuentaBancaria),
    direccion: String(formData.direccion || "").trim(),
  });

  const buildContractPayload = (formData) => {
    const salarioTexto = String(formData.salarioBaseMensual || "")
      .replace(/,/g, "")
      .trim();
    const salario = Number.parseFloat(salarioTexto);

    return {
      idEmpleado: Number(formData.idEmpleado || 0),
      idTipoContrato: Number(formData.idTipoContrato || 0),
      idHorarioLaboral: Number(formData.idHorarioLaboral || 0),
      numeroContrato: sanitizeContractNumber(formData.numeroContrato),
      fechaInicio: displayDateToIso(formData.fechaInicio),
      fechaFin: displayDateToIso(formData.fechaFin) || null,
      salarioBaseMensual: Number.isFinite(salario) ? Number(salario.toFixed(2)) : 0,
      moneda: String(formData.moneda || "")
        .trim()
        .toUpperCase(),
      esContratoVigente: Boolean(formData.esContratoVigente),
      observacion: String(formData.observacion || "").trim(),
    };
  };

  const buildCatalogPayload = (moduleId, formData) => {
    const numberValue1 = Number.parseFloat(String(formData.numberValue1 || "").replace(",", ".").trim());
    const numberValue2 = Number.parseFloat(String(formData.numberValue2 || "").replace(",", ".").trim());
    const integerValue1 = Number.parseInt(String(formData.integerValue1 || "").trim(), 10);

    return {
      moduleId,
      codigo: sanitizeCatalogCode(formData.codigo),
      nombre: String(formData.nombre || "").trim(),
      descripcion: String(formData.descripcion || "").trim() || null,
      relatedId: formData.relatedId ? Number(formData.relatedId) : null,
      numberValue1: Number.isFinite(numberValue1) ? Number(numberValue1.toFixed(2)) : null,
      numberValue2: Number.isFinite(numberValue2) ? Number(numberValue2.toFixed(2)) : null,
      integerValue1: Number.isInteger(integerValue1) ? integerValue1 : null,
      flagValue1: Boolean(formData.flagValue1),
      activo: Boolean(formData.activo),
    };
  };

  const buildActionPayload = (formData) => ({
    idEmpleado: Number(formData.idEmpleado || 0),
    tipoAccion: sanitizeLooseText(formData.tipoAccion, 50).trim(),
    fechaAccion: displayDateToIso(formData.fechaAccion),
    idCargoNuevo: formData.idCargoNuevo ? Number(formData.idCargoNuevo) : null,
    nuevoSalarioBaseMensual: (() => {
      const value = Number.parseFloat(String(formData.nuevoSalarioBaseMensual || "").replace(/,/g, "").trim());
      return Number.isFinite(value) ? Number(value.toFixed(2)) : null;
    })(),
    nuevaFechaFinContrato: displayDateToIso(formData.nuevaFechaFinContrato) || null,
    aplicarCambioOperativo: Boolean(formData.aplicarCambioOperativo),
    descripcionAccion: String(formData.descripcionAccion || "").trim(),
  });

  const buildDocumentPayload = (formData) => ({
    idEmpleado: Number(formData.idEmpleado || 0),
    tipoDocumento: sanitizeLooseText(formData.tipoDocumento, 100).trim(),
    fechaDocumento: displayDateToIso(formData.fechaDocumento) || "",
    fechaVencimiento: displayDateToIso(formData.fechaVencimiento) || "",
    observacion: String(formData.observacion || "").trim(),
    removerArchivo: Boolean(formData.removerArchivo),
    archivo: formData.archivo || null,
  });

  const validateEmployee = (payload, formData = {}) => {
    const errors = {};
    const today = new Date().toISOString().slice(0, 10);
    const minDbDate = "1753-01-01";
    const validEstadoCivil = ["SOLTERO", "CASADO", "UNION DE HECHO", "DIVORCIADO", "VIUDO"];
    const fechaIngresoTexto = sanitizeDateInput(formData.fechaIngreso);
    const fechaNacimientoTexto = sanitizeDateInput(formData.fechaNacimiento);

    if (!/^[A-Z0-9-]{4,30}$/.test(payload.codigoEmpleado)) {
      errors.codigoEmpleado = "Codigo invalido.";
    }

    if (!payload.idDepartamento) {
      errors.idDepartamento = "Selecciona un departamento.";
    }

    if (!payload.idCargo) {
      errors.idCargo = "Selecciona un cargo.";
    }

    if (!/^\d{3}-\d{6}-\d{4}[A-Z]$/.test(payload.cedula)) {
      errors.cedula = "Cedula invalida.";
    }

    if (!/^[\p{L} ]{2,150}$/u.test(payload.nombres)) {
      errors.nombres = "Nombres invalidos.";
    }

    if (!/^[\p{L} ]{2,150}$/u.test(payload.apellidos)) {
      errors.apellidos = "Apellidos invalidos.";
    }

    if (!fechaIngresoTexto) {
      errors.fechaIngreso = "Ingresa la fecha de ingreso.";
    } else if (!isValidDisplayDate(fechaIngresoTexto)) {
      errors.fechaIngreso = "Usa el formato dd/mm/aaaa.";
    } else if (payload.fechaIngreso < minDbDate) {
      errors.fechaIngreso = "Ingresa una fecha igual o mayor a 01/01/1753.";
    } else if (payload.fechaIngreso > today) {
      errors.fechaIngreso = "La fecha de ingreso no puede ser futura.";
    }

    if (!fechaNacimientoTexto) {
      errors.fechaNacimiento = "Ingresa la fecha de nacimiento.";
    } else if (!isValidDisplayDate(fechaNacimientoTexto)) {
      errors.fechaNacimiento = "Usa el formato dd/mm/aaaa.";
    } else if (payload.fechaNacimiento < minDbDate) {
      errors.fechaNacimiento = "Ingresa una fecha igual o mayor a 01/01/1753.";
    } else if (payload.fechaNacimiento > today) {
      errors.fechaNacimiento = "La fecha de nacimiento no puede ser futura.";
    } else if (payload.fechaIngreso && payload.fechaNacimiento >= payload.fechaIngreso) {
      errors.fechaNacimiento = "Debe ser menor a la fecha de ingreso.";
    }

    if (!payload.sexo) {
      errors.sexo = "Selecciona el sexo.";
    } else if (!["F", "M"].includes(payload.sexo)) {
      errors.sexo = "Sexo invalido.";
    }

    if (!payload.estadoCivil) {
      errors.estadoCivil = "Selecciona el estado civil.";
    } else if (!validEstadoCivil.includes(payload.estadoCivil)) {
      errors.estadoCivil = "Estado civil invalido.";
    }

    if (!payload.telefono) {
      errors.telefono = "Ingresa el telefono.";
    } else if (!/^\d{4}-\d{4}$/.test(payload.telefono)) {
      errors.telefono = "Telefono invalido.";
    }

    if (!payload.correo) {
      errors.correo = "Ingresa el correo.";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(payload.correo)) {
      errors.correo = "Correo invalido.";
    }

    if (!payload.inss) {
      errors.inss = "Ingresa el INSS.";
    } else if (!/^[A-Z0-9-]{4,20}$/.test(payload.inss)) {
      errors.inss = "INSS invalido.";
    }

    if (!payload.idBanco) {
      errors.idBanco = "Selecciona el banco.";
    }

    if (!payload.numeroCuentaBancaria) {
      errors.numeroCuentaBancaria = "Ingresa la cuenta bancaria.";
    } else if (!/^\d{6,30}$/.test(payload.numeroCuentaBancaria)) {
      errors.numeroCuentaBancaria = "Cuenta invalida.";
    }

    if (!payload.direccion) {
      errors.direccion = "Ingresa la direccion.";
    } else if (payload.direccion.length > 300) {
      errors.direccion = "Direccion demasiado larga.";
    }

    return errors;
  };

  const validateContract = (payload, formData = {}) => {
    const errors = {};
    const minDbDate = "1753-01-01";
    const fechaInicioTexto = sanitizeDateInput(formData.fechaInicio);
    const fechaFinTexto = sanitizeDateInput(formData.fechaFin);

    if (!payload.idEmpleado) {
      errors.idEmpleado = "Selecciona el empleado.";
    }

    if (!payload.idTipoContrato) {
      errors.idTipoContrato = "Selecciona el tipo de contrato.";
    }

    if (!payload.idHorarioLaboral) {
      errors.idHorarioLaboral = "Selecciona el horario laboral.";
    }

    if (!/^[A-Z0-9-]{4,100}$/.test(payload.numeroContrato)) {
      errors.numeroContrato = "Numero de contrato invalido.";
    }

    if (!fechaInicioTexto) {
      errors.fechaInicio = "Ingresa la fecha de inicio.";
    } else if (!isValidDisplayDate(fechaInicioTexto)) {
      errors.fechaInicio = "Usa el formato dd/mm/aaaa.";
    } else if (payload.fechaInicio < minDbDate) {
      errors.fechaInicio = "Ingresa una fecha igual o mayor a 01/01/1753.";
    }

    if (!payload.esContratoVigente && !fechaFinTexto) {
      errors.fechaFin = "Ingresa la fecha fin si el contrato no esta vigente.";
    } else if (fechaFinTexto && !isValidDisplayDate(fechaFinTexto)) {
      errors.fechaFin = "Usa el formato dd/mm/aaaa.";
    } else if (payload.fechaFin && payload.fechaFin < minDbDate) {
      errors.fechaFin = "Ingresa una fecha igual o mayor a 01/01/1753.";
    } else if (payload.fechaInicio && payload.fechaFin && payload.fechaFin < payload.fechaInicio) {
      errors.fechaFin = "La fecha fin debe ser igual o mayor a la fecha de inicio.";
    }

    if (!(payload.salarioBaseMensual > 0)) {
      errors.salarioBaseMensual = "Ingresa un salario base valido.";
    }

    if (!/^[A-Z]{3,20}$/.test(payload.moneda)) {
      errors.moneda = "Selecciona la moneda.";
    }

    if (payload.observacion && payload.observacion.length > 1000) {
      errors.observacion = "La observacion supera el limite permitido.";
    }

    return errors;
  };

  const validateCatalog = (moduleId, payload) => {
    const errors = {};
    const config = catalogModuleConfigs[moduleId];

    if (!config) {
      errors.codigo = "Catalogo no soportado.";
      return errors;
    }

    if (!/^[A-Z0-9_-]{2,30}$/.test(payload.codigo)) {
      errors.codigo = "Codigo invalido.";
    }

    if (!payload.nombre || payload.nombre.length < 2 || payload.nombre.length > (config.nameMaxLength || 150)) {
      errors.nombre = "Nombre invalido.";
    }

    if (config.usesDescription && payload.descripcion && payload.descripcion.length > 300) {
      errors.descripcion = "La descripcion supera el limite permitido.";
    }

    if (config.usesRelatedId && !payload.relatedId) {
      errors.relatedId = "Selecciona un departamento.";
    }

    if (config.usesIntegerValue1) {
      if (!Number.isInteger(payload.integerValue1) || payload.integerValue1 < 1 || payload.integerValue1 > 99) {
        errors.integerValue1 = "Ingresa un nivel jerarquico valido.";
      }
    }

    if (config.usesNumberValue1) {
      if (!(payload.numberValue1 > 0)) {
        errors.numberValue1 = `Ingresa ${config.number1Label?.toLowerCase() || "un valor"} valido.`;
      }

      if (moduleId === "horario_laboral" && payload.numberValue1 > 168) {
        errors.numberValue1 = "Ingresa las horas semanales validas.";
      }

      if (moduleId === "tipo_hora_extra" && payload.numberValue1 > 10) {
        errors.numberValue1 = "Ingresa un factor de pago valido.";
      }
    }

    if (config.usesNumberValue2) {
      if (!(payload.numberValue2 > 0) || payload.numberValue2 > 24) {
        errors.numberValue2 = "Ingresa las horas diarias validas.";
      } else if (payload.numberValue1 && payload.numberValue2 > payload.numberValue1) {
        errors.numberValue2 = "Las horas diarias no pueden superar las horas semanales.";
      }
    }

    return errors;
  };

  const validateAction = (payload, formData = {}) => {
    const errors = {};
    const fechaTexto = sanitizeDateInput(formData.fechaAccion);
    const fechaFinNuevaTexto = sanitizeDateInput(formData.nuevaFechaFinContrato);
    const tipoAccionNormalizado = String(payload.tipoAccion || "")
      .trim()
      .toUpperCase();

    if (!payload.idEmpleado) {
      errors.idEmpleado = "Selecciona el empleado.";
    }

    if (!payload.tipoAccion || payload.tipoAccion.length < 3 || payload.tipoAccion.length > 50) {
      errors.tipoAccion = "Ingresa un tipo de accion valido.";
    } else if (!allowedActionTypes.has(tipoAccionNormalizado)) {
      errors.tipoAccion = "Accion no permitida en este modulo. Usa promociones o cambios internos.";
    }

    if (!fechaTexto) {
      errors.fechaAccion = "Ingresa la fecha de la accion.";
    } else if (!isValidDisplayDate(fechaTexto)) {
      errors.fechaAccion = "Usa el formato dd/mm/aaaa.";
    } else if (payload.fechaAccion < "1753-01-01") {
      errors.fechaAccion = "Ingresa una fecha igual o mayor a 01/01/1753.";
    }

    if (["PROMOCION", "TRASLADO"].includes(tipoAccionNormalizado) && !payload.idCargoNuevo) {
      errors.idCargoNuevo = "Selecciona el nuevo cargo.";
    }

    if (["PROMOCION", "CAMBIO SALARIAL"].includes(tipoAccionNormalizado)) {
      if (!(payload.nuevoSalarioBaseMensual > 0)) {
        errors.nuevoSalarioBaseMensual = "Ingresa el nuevo salario base.";
      }
    }

    if (tipoAccionNormalizado === "PRORROGA CONTRATO") {
      if (!fechaFinNuevaTexto) {
        errors.nuevaFechaFinContrato = "Ingresa la nueva fecha fin.";
      } else if (!isValidDisplayDate(fechaFinNuevaTexto)) {
        errors.nuevaFechaFinContrato = "Usa el formato dd/mm/aaaa.";
      } else if (payload.nuevaFechaFinContrato < "1753-01-01") {
        errors.nuevaFechaFinContrato = "Ingresa una fecha igual o mayor a 01/01/1753.";
      } else if (payload.fechaAccion && payload.nuevaFechaFinContrato < payload.fechaAccion) {
        errors.nuevaFechaFinContrato = "Debe ser igual o mayor a la fecha de la accion.";
      }
    }

    if (!payload.descripcionAccion || payload.descripcionAccion.length < 5 || payload.descripcionAccion.length > 500) {
      errors.descripcionAccion = "Ingresa una descripcion valida.";
    }

    return errors;
  };

  const getHierarchyLabel = (value) => {
    const level = Number(value || 0);

    if (level >= 9) {
      return "Gerencia";
    }

    if (level >= 8) {
      return "Jefatura";
    }

    if (level >= 7) {
      return "Coordinacion";
    }

    if (level >= 6) {
      return "Especialista / Analista";
    }

    if (level >= 5) {
      return "Operacion";
    }

    return level > 0 ? "Apoyo" : "Sin jerarquia definida";
  };

  const validateDocument = (payload, formData = {}) => {
    const errors = {};
    const fechaDocumentoTexto = sanitizeDateInput(formData.fechaDocumento);
    const fechaVencimientoTexto = sanitizeDateInput(formData.fechaVencimiento);
    const allowedExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx"];
    const file = formData.archivo || null;

    if (!payload.idEmpleado) {
      errors.idEmpleado = "Selecciona el empleado.";
    }

    if (!payload.tipoDocumento || payload.tipoDocumento.length < 3 || payload.tipoDocumento.length > 100) {
      errors.tipoDocumento = "Ingresa un tipo de documento valido.";
    }

    if (fechaDocumentoTexto) {
      if (!isValidDisplayDate(fechaDocumentoTexto)) {
        errors.fechaDocumento = "Usa el formato dd/mm/aaaa.";
      } else if (payload.fechaDocumento < "1753-01-01") {
        errors.fechaDocumento = "Ingresa una fecha igual o mayor a 01/01/1753.";
      } else if (payload.fechaDocumento > new Date().toISOString().slice(0, 10)) {
        errors.fechaDocumento = "La fecha del documento no puede ser futura.";
      }
    }

    if (fechaVencimientoTexto) {
      if (!isValidDisplayDate(fechaVencimientoTexto)) {
        errors.fechaVencimiento = "Usa el formato dd/mm/aaaa.";
      } else if (payload.fechaVencimiento < "1753-01-01") {
        errors.fechaVencimiento = "Ingresa una fecha igual o mayor a 01/01/1753.";
      } else if (payload.fechaDocumento && payload.fechaVencimiento < payload.fechaDocumento) {
        errors.fechaVencimiento = "Debe ser igual o mayor a la fecha del documento.";
      }
    }

    if (payload.observacion && payload.observacion.length > 500) {
      errors.observacion = "La observacion supera el limite permitido.";
    }

    if (file) {
      const name = String(file.name || "").toLowerCase();
      const hasValidExtension = allowedExtensions.some((extension) => name.endsWith(extension));

      if (!hasValidExtension) {
        errors.archivo = "Adjunta un archivo PDF, Word o imagen valido.";
      } else if (!(file.size > 0) || file.size > 10 * 1024 * 1024) {
        errors.archivo = "El archivo debe pesar entre 1 byte y 10 MB.";
      }
    }

    return errors;
  };

  const formatShortDate = (value) => {
    if (!value) {
      return "-";
    }

    try {
      return new Intl.DateTimeFormat("es-NI", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        timeZone: "America/Managua",
      }).format(new Date(`${value}T00:00:00`));
    } catch {
      return value;
    }
  };

  const formatMoney = (value, currency = "NIO") => {
    const amount = Number(value || 0);
    const normalizedCurrency = String(currency || "NIO")
      .trim()
      .toUpperCase();

    try {
      return new Intl.NumberFormat("es-NI", {
        style: "currency",
        currency: normalizedCurrency,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(amount);
    } catch {
      return `${normalizedCurrency} ${amount.toFixed(2)}`;
    }
  };

  const getStatusTone = (status) => {
    const value = normalizeText(status);

    if (value.includes("activo")) {
      return "status-success";
    }

    if (value.includes("suspendido") || value.includes("vacaciones")) {
      return "status-warning";
    }

    if (value.includes("retirado")) {
      return "status-danger";
    }

    return "";
  };

  const getContractStatusTone = (isVigente) => (isVigente ? "status-success" : "status-warning");

  return {
    SESSION_KEY,
    getSession,
    clearSession,
    getGroups,
    getGroupById,
    getModuleById,
    getStatusOptions: () => statusOptions.map((item) => ({ ...item })),
    getContractStatusOptions: () => contractStatusOptions.map((item) => ({ ...item })),
    getWorkflowStatusOptions: () => workflowStatusOptions.map((item) => ({ ...item })),
    getCatalogStatusOptions: () => catalogStatusOptions.map((item) => ({ ...item })),
    getActionStatusOptions: () => actionStatusOptions.map((item) => ({ ...item })),
    getDocumentStatusOptions: () => documentStatusOptions.map((item) => ({ ...item })),
    getSexoOptions: () => sexoOptions.map((item) => ({ ...item })),
    getEstadoCivilOptions: () => estadoCivilOptions.map((item) => ({ ...item })),
    getCatalogModuleConfig: (moduleId) => (catalogModuleConfigs[moduleId] ? { ...catalogModuleConfigs[moduleId] } : null),
    getEmptyEmployee,
    getEmptyContract,
    getEmptyCatalog,
    getEmptyAction,
    getEmptyDocument,
    buildPayload,
    buildContractPayload,
    buildCatalogPayload,
    buildActionPayload,
    buildDocumentPayload,
    validateEmployee,
    validateContract,
    validateCatalog,
    validateAction,
    validateDocument,
    sanitizeCode,
    sanitizeCatalogCode,
    sanitizeContractNumber,
    sanitizeCedula,
    sanitizeDateInput,
    sanitizeName,
    sanitizeLooseText,
    sanitizePhone,
    sanitizeInss,
    sanitizeAccount,
    sanitizeEmail,
    isoDateToDisplay,
    formatShortDate,
    displayDateToIso,
    formatMoney,
    getStatusTone,
    getContractStatusTone,
    getHierarchyLabel,
  };
})();

