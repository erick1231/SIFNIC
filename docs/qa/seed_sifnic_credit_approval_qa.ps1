param(
    [string]$ConnectionString = "Data Source=GCPPA367ITTC\SQLEXPRESS;Initial Catalog=CREDITO;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=False;",
    [string]$QaPassword = "SifnicQA2026!"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Data

function New-SifnicPasswordHash {
    param([string]$Password)
    $salt = [byte[]]::new(16)
    $rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::new()
    $rng.GetBytes($salt)
    $derive = [System.Security.Cryptography.Rfc2898DeriveBytes]::new($Password, $salt, 100000)
    $hash = $derive.GetBytes(32)
    "PBKDF2SHA1|100000|$([Convert]::ToBase64String($salt))|$([Convert]::ToBase64String($hash))"
}

$connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$connection.Open()

function Invoke-Scalar {
    param([string]$Sql, [hashtable]$Parameters = @{})
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 120
    foreach ($key in $Parameters.Keys) {
        [void]$cmd.Parameters.AddWithValue("@$key", $(if ($null -eq $Parameters[$key]) { [DBNull]::Value } else { $Parameters[$key] }))
    }
    $cmd.ExecuteScalar()
}

function Invoke-NonQuery {
    param([string]$Sql, [hashtable]$Parameters = @{})
    [void](Invoke-Scalar -Sql $Sql -Parameters $Parameters)
}

$users = @(
    @{ User="qa.admin"; Names="Admin"; Last="SIFNIC"; Role="ADMINISTRADOR"; Cargo=9; Modules=@("clientes","creditos","simulador-credito","cobranza","caja","nomina","rrhh","configuracion","contabilidad","bandeja-supervisor") },
    @{ User="qa.gerente.credito"; Names="Gerardo"; Last="Montenegro"; Role="GERENTE_CREDITO"; Cargo=5; Modules=@("clientes","creditos","simulador-credito","cobranza","bandeja-supervisor") },
    @{ User="qa.jefe.credito"; Names="Julia"; Last="Alvarez"; Role="JEFE_CREDITO"; Cargo=11; Modules=@("clientes","creditos","simulador-credito","cobranza","bandeja-supervisor") },
    @{ User="qa.supervisor.cobranza"; Names="Sergio"; Last="Rivas"; Role="SUPERVISOR"; Cargo=7; Modules=@("clientes","cobranza","bandeja-supervisor") },
    @{ User="qa.caja01"; Names="Carla"; Last="Reyes"; Role="CAJERO"; Cargo=6; Modules=@("caja","clientes") },
    @{ User="qa.caja02"; Names="Carlos"; Last="Mena"; Role="CAJERO"; Cargo=6; Modules=@("caja","clientes") },
    @{ User="qa.oficial01"; Names="Olivia"; Last="Lopez"; Role="OFICIAL_CREDITO"; Cargo=10; Modules=@("clientes","creditos","simulador-credito","cobranza") },
    @{ User="qa.oficial02"; Names="Oscar"; Last="Martinez"; Role="OFICIAL_CREDITO"; Cargo=10; Modules=@("clientes","creditos","simulador-credito","cobranza") },
    @{ User="qa.oficial03"; Names="Paola"; Last="Castillo"; Role="OFICIAL_CREDITO"; Cargo=10; Modules=@("clientes","creditos","simulador-credito","cobranza") },
    @{ User="qa.oficial04"; Names="Pablo"; Last="Gaitan"; Role="OFICIAL_CREDITO"; Cargo=10; Modules=@("clientes","creditos","simulador-credito","cobranza") },
    @{ User="qa.oficial05"; Names="Rosa"; Last="Navarro"; Role="OFICIAL_CREDITO"; Cargo=10; Modules=@("clientes","creditos","simulador-credito","cobranza") },
    @{ User="qa.oficial06"; Names="Rene"; Last="Solorzano"; Role="OFICIAL_CREDITO"; Cargo=10; Modules=@("clientes","creditos","simulador-credito","cobranza") },
    @{ User="qa.recuperador01"; Names="Ramon"; Last="Obando"; Role="CXC"; Cargo=7; Modules=@("clientes","cobranza","cxc") },
    @{ User="qa.recuperador02"; Names="Raquel"; Last="Bermudez"; Role="CXC"; Cargo=7; Modules=@("clientes","cobranza","cxc") },
    @{ User="qa.nomina"; Names="Nadia"; Last="Flores"; Role="ADMINISTRACION"; Cargo=4; Modules=@("nomina","rrhh","mi-portal") }
)

$activeEmployeeState = [int64](Invoke-Scalar "SELECT TOP (1) id_estado_empleado FROM rrhh.estado_empleado WHERE codigo_estado_empleado = N'ACTIVO' ORDER BY id_estado_empleado")
$fixedContractType = [int64](Invoke-Scalar "SELECT TOP (1) id_tipo_contrato FROM rrhh.tipo_contrato WHERE codigo_tipo_contrato IN (N'FIJO', N'INDETERMINADO') ORDER BY id_tipo_contrato")
$workSchedule = [int64](Invoke-Scalar "SELECT TOP (1) id_horario_laboral FROM rrhh.horario_laboral ORDER BY id_horario_laboral")
$adminDept = [int64](Invoke-Scalar "SELECT TOP (1) id_departamento FROM rrhh.departamento ORDER BY id_departamento")

$userIds = @{}
$employeeIds = @{}
foreach ($u in $users) {
    $hash = New-SifnicPasswordHash -Password $QaPassword
    $email = "$($u.User)@sifnic.qa"
    $existingUserId = Invoke-Scalar "SELECT id_usuario FROM seguridad.usuario WHERE usuario = @usuario" @{ usuario = $u.User }
    if ($null -eq $existingUserId -or $existingUserId -eq [DBNull]::Value) {
        $userId = [int64](Invoke-Scalar @"
INSERT INTO seguridad.usuario
(usuario, nombres, apellidos, correo, telefono, hash_clave, cambiar_clave_en_proximo_inicio, bloqueado, activo, intentos_fallidos, fecha_registro)
OUTPUT INSERTED.id_usuario
VALUES (@usuario, @nombres, @apellidos, @correo, N'8888-0000', @hash, 0, 0, 1, 0, SYSDATETIME());
"@ @{ usuario=$u.User; nombres=$u.Names; apellidos=$u.Last; correo=$email; hash=$hash })
    } else {
        $userId = [int64]$existingUserId
        Invoke-NonQuery "UPDATE seguridad.usuario SET nombres=@nombres, apellidos=@apellidos, correo=@correo, hash_clave=@hash, activo=1, bloqueado=0, cambiar_clave_en_proximo_inicio=0, fecha_actualizacion=SYSDATETIME() WHERE id_usuario=@id" @{ id=$userId; nombres=$u.Names; apellidos=$u.Last; correo=$email; hash=$hash }
    }
    $userIds[$u.User] = $userId

    $roleId = Invoke-Scalar "SELECT id_rol FROM seguridad.rol WHERE codigo_rol = @rol" @{ rol=$u.Role }
    if ($null -ne $roleId -and $roleId -ne [DBNull]::Value) {
        Invoke-NonQuery @"
IF EXISTS (SELECT 1 FROM seguridad.usuario_rol WHERE id_usuario=@usuario AND id_rol=@rol)
    UPDATE seguridad.usuario_rol SET activo=1 WHERE id_usuario=@usuario AND id_rol=@rol;
ELSE
    INSERT INTO seguridad.usuario_rol (id_usuario, id_rol, activo, fecha_registro) VALUES (@usuario, @rol, 1, SYSDATETIME());
"@ @{ usuario=$userId; rol=[int64]$roleId }
    }

    Invoke-NonQuery "UPDATE seguridad.usuario_modulo SET activo=0, fecha_actualizacion=SYSDATETIME() WHERE id_usuario=@usuario" @{ usuario=$userId }
    foreach ($module in $u.Modules) {
        Invoke-NonQuery @"
IF EXISTS (SELECT 1 FROM seguridad.usuario_modulo WHERE id_usuario=@usuario AND codigo_modulo=@modulo)
    UPDATE seguridad.usuario_modulo SET activo=1, usuario_registro=N'qa.seed', fecha_actualizacion=SYSDATETIME() WHERE id_usuario=@usuario AND codigo_modulo=@modulo;
ELSE
    INSERT INTO seguridad.usuario_modulo (id_usuario, codigo_modulo, activo, usuario_registro, fecha_registro) VALUES (@usuario, @modulo, 1, N'qa.seed', SYSDATETIME());
"@ @{ usuario=$userId; modulo=$module }
    }

    $cedulaEmpleado = "QA-EMP-$($u.User.Replace('qa.','').Replace('.','-'))"
    $deptCandidate = Invoke-Scalar "SELECT TOP (1) id_departamento FROM rrhh.cargo WHERE id_cargo=@cargo" @{ cargo=[int64]$u.Cargo }
    $dept = if ($null -ne $deptCandidate -and $deptCandidate -ne [DBNull]::Value) { [int64]$deptCandidate } else { $adminDept }
    $existingEmployeeId = Invoke-Scalar "SELECT id_empleado FROM rrhh.empleado WHERE cedula = @cedula" @{ cedula=$cedulaEmpleado }
    if ($null -eq $existingEmployeeId -or $existingEmployeeId -eq [DBNull]::Value) {
        $employeeId = [int64](Invoke-Scalar @"
INSERT INTO rrhh.empleado
(codigo_empleado, id_departamento, id_cargo, id_estado_empleado, cedula, inss, nombres, apellidos, telefono, correo, direccion, fecha_ingreso, id_banco, numero_cuenta_bancaria, activo, fecha_registro, foto_perfil_url)
OUTPUT INSERTED.id_empleado
VALUES (@codigo, @departamento, @cargo, @estado, @cedula, @inss, @nombres, @apellidos, N'8888-0000', @correo, N'Direccion QA', CONVERT(date, '2026-01-01'), NULL, NULL, 1, SYSDATETIME(), @foto);
"@ @{ codigo="QA-$($userId.ToString('0000'))"; departamento=$dept; cargo=[int64]$u.Cargo; estado=$activeEmployeeState; cedula=$cedulaEmpleado; inss="QA-INSS-$($userId.ToString('0000'))"; nombres=$u.Names; apellidos=$u.Last; correo=$email; foto="/uploads/expedientes/qa/foto_colaborador.png" })
    } else {
        $employeeId = [int64]$existingEmployeeId
        Invoke-NonQuery "UPDATE rrhh.empleado SET correo=@correo, activo=1, foto_perfil_url=@foto, fecha_actualizacion=SYSDATETIME() WHERE id_empleado=@id" @{ id=$employeeId; correo=$email; foto="/uploads/expedientes/qa/foto_colaborador.png" }
    }
    $employeeIds[$u.User] = $employeeId

    Invoke-NonQuery @"
IF NOT EXISTS (SELECT 1 FROM rrhh.contrato WHERE id_empleado=@empleado AND numero_contrato=@contrato)
BEGIN
    INSERT INTO rrhh.contrato
    (id_empleado, id_tipo_contrato, id_horario_laboral, numero_contrato, fecha_inicio, salario_base_mensual, moneda, es_contrato_vigente, observacion, fecha_registro)
    VALUES (@empleado, @tipo, @horario, @contrato, CONVERT(date, '2026-01-01'), @salario, N'NIO', 1, N'Contrato QA para pruebas integrales.', SYSDATETIME());
END;
"@ @{ empleado=$employeeId; tipo=$fixedContractType; horario=$workSchedule; contrato="QA-CON-$($u.User)"; salario=if ($u.Role -eq "CAJERO") { 18000 } elseif ($u.Role -eq "OFICIAL_CREDITO") { 22000 } else { 30000 } }
}

$docTypes = @{}
$docTable = @("DOC_ID","SOL_CRED","PLAN_PAGO","CENTRAL_RIESGO","RES_COMITE")
foreach ($code in $docTable) {
    $docTypes[$code] = [int64](Invoke-Scalar "SELECT TOP (1) id_tipo_documento_expediente FROM parametros.tipo_documento_expediente WHERE codigo_tipo_documento=@codigo ORDER BY id_tipo_documento_expediente" @{ codigo=$code })
}

$officers = @("qa.oficial01","qa.oficial02","qa.oficial03","qa.oficial04","qa.oficial05","qa.oficial06")
$recoverers = @("qa.recuperador01","qa.recuperador02")
$products = @("MICROCREDITO","CAPITAL_TRABAJO","CONSUMO_PERSONAL","MEJORA_VIVIENDA","GRUPO_SOLIDARIO")
$states = @("VE","PR","RR","SA","CA")

for ($i = 1; $i -le 200; $i++) {
    $suffix = $i.ToString("000")
    $cedula = "QA-CLI-2026-$suffix"
    $firstName = "Cliente QA $suffix"
    $lastName = if ($i -le 100) { "Cartera Activa" } else { "Cartera Tramo" }
    $risk = if ($i % 11 -eq 0) { "ALTO" } elseif ($i % 5 -eq 0) { "MEDIO" } else { "BAJO" }
    $income = 18000 + ($i * 120)
    $expenses = 6500 + ($i * 45)
    $clientId = Invoke-Scalar "SELECT id_cliente FROM clientes.cliente WHERE cedula=@cedula" @{ cedula=$cedula }
    if ($null -eq $clientId -or $clientId -eq [DBNull]::Value) {
        $clientId = [int64](Invoke-Scalar @"
INSERT INTO clientes.cliente
(cedula, nombres, apellidos, tipo_cliente, activo, telefono, celular, direccion, estado_cliente, fecha_ingreso, actividad_economica, ingresos_mensuales, egresos_mensuales, nivel_riesgo, puntaje_riesgo, estado_expediente, usuario_registro, observaciones)
OUTPUT INSERTED.id_cliente
VALUES (@cedula, @nombres, @apellidos, N'INDIVIDUAL', 1, N'8888-0000', N'8888-0000', N'Direccion cliente QA', N'ACTIVO', CONVERT(date,'2026-01-01'), N'Comercio', @ingresos, @egresos, @riesgo, 65, N'COMPLETO', N'qa.seed', N'Cliente sembrado para aprobacion, cartera y caja.');
"@ @{ cedula=$cedula; nombres=$firstName; apellidos=$lastName; ingresos=$income; egresos=$expenses; riesgo=$risk })
    } else {
        $clientId = [int64]$clientId
    }

    $product = $products[($i - 1) % $products.Count]
    $baseAmount = [decimal](10000 + (($i % 25) * 1200))
    $commission = [decimal]($baseAmount * 0.05)
    $financed = [decimal]($baseAmount + $commission)
    $term = 10 + ($i % 9)
    $rate = [decimal](24 + ($i % 7))
    $installment = [decimal]([Math]::Round(($financed / $term) + ($financed * ($rate / 100) / 12), 2))
    $officerUser = $officers[($i - 1) % $officers.Count]
    $status = if ($i -le 100) { "VI" } else { $states[($i - 101) % $states.Count] }
    $requestNumber = "QA-SOL-2026-$suffix"
    $creditNumber = "QA-CRD-2026-$suffix"
    $checklistJson = '{"Identification":true,"FileCompleted":true,"HomeBusinessVisit":true,"PaymentCapacity":true,"ConamiReview":true,"ListCheck":true,"GuaranteeReview":true}'
    $bureauJson = '{"Consulted":true,"BureauName":"SIN_RIESGO","Result":"ACEPTABLE","Score":720,"Classification":"A","ExternalDebt":0,"ExternalInstallment":0,"InternalDebt":0,"InternalInstallment":0,"RequestedAmount":0,"RequestedInstallment":0,"TotalDebt":0,"TotalInstallment":0,"PaymentCapacity":0,"DebtCapacityRatio":25,"Alerts":[],"Notes":"QA"}'

    $requestId = Invoke-Scalar "SELECT id_solicitud_credito FROM creditos.solicitud_credito WHERE numero_solicitud=@numero" @{ numero=$requestNumber }
    if ($null -eq $requestId -or $requestId -eq [DBNull]::Value) {
        $requestId = [int64](Invoke-Scalar @"
INSERT INTO creditos.solicitud_credito
(id_cliente, numero_solicitud, fecha_solicitud, monto_solicitado, plazo_meses, tasa_interes_anual, moneda, destino_credito, estado_solicitud, observacion, producto_credito, frecuencia_pago, tipo_cuota, cuota_estimada, ingresos_declarados, egresos_declarados, capacidad_pago, fuente_ingreso, actividad_financiada, tipo_garantia, descripcion_garantia, valor_garantia, requiere_comite, nivel_riesgo, clasificacion_conami, checklist_json, plan_generado_json, usuario_registro, etapa_prospeccion, promotor_credito, sucursal_credito, oficina_credito, fecha_sistema_prospeccion, referencias_prospeccion_json, visitas_prospeccion_json, fecha_consulta_central, central_riesgo_json, tasa_comision_ascc, tasa_deslizamiento_anual, tasa_mora_anual)
OUTPUT INSERTED.id_solicitud_credito
VALUES (@cliente, @numero, CONVERT(date,'2026-05-01'), @monto, @plazo, @tasa, N'NIO', N'Capital de trabajo QA', N'APROBADA', N'Solicitud QA aprobada para cartera.', @producto, N'MENSUAL', N'NIVELADA', @cuota, @ingresos, @egresos, @capacidad, N'Negocio propio', N'Comercio minorista', N'FIADOR', N'Garantia QA', @monto, 0, @riesgo, N'A', @checklist, NULL, @usuario, N'SOLICITUD_FORMAL', @usuario, N'Casa Matriz', N'CASA', CONVERT(date,'2026-05-01'), N'{}', N'{}', CONVERT(date,'2026-05-01'), @bureau, 5, 0, 48);
"@ @{ cliente=$clientId; numero=$requestNumber; monto=$baseAmount; plazo=$term; tasa=$rate; producto=$product; cuota=$installment; ingresos=$income; egresos=$expenses; capacidad=($income-$expenses); riesgo=$risk; checklist=$checklistJson; usuario=$officerUser; bureau=$bureauJson })
    } else {
        $requestId = [int64]$requestId
    }

    $creditId = Invoke-Scalar "SELECT id_credito FROM creditos.credito WHERE numero_credito=@numero" @{ numero=$creditNumber }
    if ($null -eq $creditId -or $creditId -eq [DBNull]::Value) {
        $disbDate = [DateTime]::Parse("2026-01-15").AddDays($i % 45)
        $dueDate = $disbDate.AddMonths($term)
        $balance = if ($status -eq "CA") { 0 } else { [Math]::Round([double]($financed * 0.72), 2) }
        $mora = if ($status -eq "VE") { [Math]::Round([double]($balance * 0.08), 2) } else { 0 }
        $interest = if ($status -in @("VE","PR","RR")) { [Math]::Round([double]($balance * 0.035), 2) } else { 0 }
        $creditId = [int64](Invoke-Scalar @"
INSERT INTO creditos.credito
(cedula_id_cliente_ofic_ciclo, cedula_id_cliente, nom_cliente, tipo_agrupacion, garantia, oficina, fecha_desembolso, fecha_vencimiento, estado_operativo, saldo_capital, interes_acumulado, interes_pagado, mora_acumulada, cargos_acumulados, comision_acumulada, comision_pagada, activo, id_cliente, id_solicitud_credito, numero_credito, moneda, monto_aprobado, plazo_meses, tasa_interes_anual, fecha_aprobacion, fecha_cancelacion)
OUTPUT INSERTED.id_credito
VALUES (@numero, @cedula, @clienteNombre, 1, N'FIADOR', N'CASA', @desembolso, @vencimiento, @estado, @saldo, @interes, 0, @mora, 0, @comision, 0, 1, @cliente, @solicitud, @numero, N'NIO', @financiado, @plazo, @tasa, CONVERT(date,'2026-05-01'), CASE WHEN @estado = N'CA' THEN CONVERT(date,'2026-04-25') ELSE NULL END);
"@ @{ numero=$creditNumber; cedula=$cedula; clienteNombre="$firstName $lastName"; desembolso=$disbDate; vencimiento=$dueDate; estado=$status; saldo=$balance; interes=$interest; mora=$mora; comision=$commission; cliente=$clientId; solicitud=$requestId; financiado=$financed; plazo=$term; tasa=$rate })
    } else {
        $creditId = [int64]$creditId
    }

    $assignedUserId = $userIds[$officerUser]
    Invoke-NonQuery @"
IF NOT EXISTS (SELECT 1 FROM creditos.asignacion_oficial_credito WHERE id_credito=@credito AND activo=1)
INSERT INTO creditos.asignacion_oficial_credito (id_credito, id_usuario_oficial, id_usuario_asigna, fecha_asignacion, motivo, observacion, activo, fecha_registro)
VALUES (@credito, @oficial, @asigna, SYSDATETIME(), N'Asignacion QA', N'Cartera sembrada para pruebas.', 1, SYSDATETIME());
"@ @{ credito=$creditId; oficial=$assignedUserId; asigna=$userIds["qa.jefe.credito"] }

    $hasPlan = Invoke-Scalar "SELECT COUNT(1) FROM creditos.plan_pago_credito WHERE id_credito=@credito" @{ credito=$creditId }
    if ([int]$hasPlan -eq 0) {
        for ($cuota = 1; $cuota -le 6; $cuota++) {
            $due = ([DateTime]::Parse("2026-02-01")).AddMonths($cuota - 1)
            $daysLate = if ($status -eq "VE") { [Math]::Max(1, (New-TimeSpan -Start $due -End ([DateTime]::Parse("2026-05-01"))).Days) } else { 0 }
            $feeState = if ($status -eq "VE" -and $cuota -le 3) { "VENCIDA" } elseif ($status -eq "CA") { "PAGADA" } else { "PENDIENTE" }
            Invoke-NonQuery @"
INSERT INTO creditos.plan_pago_credito
(cedula_id_cliente_ofic_ciclo, numero_cuota, fecha_cuota, saldo_capital_cuota, saldo_interes_cuota, saldo_comision_cuota, saldo_mora_cuota, pagada, capital_programado, interes_programado, comision_programada, mora_programada, capital_pagado_cuota, interes_pagado_cuota, comision_pagada_cuota, mora_pagada_cuota, dias_mora, estado_cuota, id_credito, capital_dispensado_cuota, interes_dispensado_cuota, comision_dispensada_cuota, mora_dispensada_cuota, dias_interes, deslizamiento_programado)
VALUES (@numero, @cuota, @fecha, @saldo, @interes, 0, @mora, CASE WHEN @estado = N'PAGADA' THEN 1 ELSE 0 END, @capital, @interes, 0, @mora, 0, 0, 0, 0, @dias, @estado, @credito, 0, 0, 0, 0, 30, 0);
"@ @{ numero=$creditNumber; cuota=$cuota; fecha=$due; saldo=([decimal]($balance / 6)); interes=([decimal]($interest / 6)); mora=([decimal]($mora / 6)); capital=([decimal]($financed / 6)); dias=$daysLate; estado=$feeState; credito=$creditId }
        }
    }

    $expedientId = Invoke-Scalar "SELECT id_expediente_credito FROM creditos.expediente_credito WHERE id_credito=@credito" @{ credito=$creditId }
    if ($null -eq $expedientId -or $expedientId -eq [DBNull]::Value) {
        $expedientId = [int64](Invoke-Scalar @"
INSERT INTO creditos.expediente_credito (id_credito, codigo_expediente, estado_expediente, observacion, usuario_responsable, fecha_creacion)
OUTPUT INSERTED.id_expediente_credito
VALUES (@credito, @codigo, N'COMPLETO', N'Expediente QA con documentos simulados.', @usuario, SYSDATETIME());
"@ @{ credito=$creditId; codigo="QA-EXP-$suffix"; usuario=$officerUser })
    } else {
        $expedientId = [int64]$expedientId
    }

    foreach ($docCode in @("DOC_ID","SOL_CRED","PLAN_PAGO")) {
        Invoke-NonQuery @"
IF NOT EXISTS (SELECT 1 FROM creditos.documento_expediente WHERE id_expediente_credito=@expediente AND id_tipo_documento_expediente=@tipo)
INSERT INTO creditos.documento_expediente
(id_expediente_credito, id_tipo_documento_expediente, nombre_archivo, ruta_archivo, fecha_documento, entregado, validado, observacion, usuario_registro, fecha_creacion)
VALUES (@expediente, @tipo, @archivo, @ruta, CONVERT(date,'2026-05-01'), 1, 1, N'Documento QA para expediente.', N'qa.seed', SYSDATETIME());
"@ @{ expediente=$expedientId; tipo=$docTypes[$docCode]; archivo="$docCode-$creditNumber.pdf"; ruta="/uploads/expedientes/qa/$docCode.pdf" }
    }
}

for ($i = 1; $i -le 25; $i++) {
    $suffix = (300 + $i).ToString("000")
    $clientId = [int64](Invoke-Scalar "SELECT TOP (1) id_cliente FROM clientes.cliente WHERE cedula LIKE N'QA-CLI-2026-%' ORDER BY NEWID()")
    $officerUser = $officers[($i - 1) % $officers.Count]
    $status = @("TRAMITE","PRECALIFICADA","COMITE","MEJORA")[($i - 1) % 4]
    $stage = if ($status -in @("COMITE","MEJORA")) { "SOLICITUD_FORMAL" } elseif ($status -eq "PRECALIFICADA") { "PRECALIFICADO" } else { "PROSPECTO" }
    $checklistJson = if ($stage -eq "SOLICITUD_FORMAL") { '{"Identification":true,"FileCompleted":true,"HomeBusinessVisit":true,"PaymentCapacity":true,"ConamiReview":true,"ListCheck":true,"GuaranteeReview":true}' } else { '{"Identification":true,"FileCompleted":false,"HomeBusinessVisit":false,"PaymentCapacity":true,"ConamiReview":false,"ListCheck":false,"GuaranteeReview":false}' }
    $referencesJson = '{"Personal":{"Name":"Referencia personal QA","Phone":"8888-0101","Result":"POSITIVA"},"Commercial":{"Name":"Referencia comercial QA","Phone":"8888-0202","Result":"POSITIVA"},"Financial":{"Name":"Referencia financiera QA","Phone":"8888-0303","Result":"POSITIVA"}}'
    $visitsJson = '{"Home":{"Date":"2026-05-01","Result":"REALIZADA","Observation":"Visita domiciliar QA positiva","Evidence":"/uploads/expedientes/qa/DOC_ID.pdf"},"Business":{"Date":"2026-05-01","Result":"REALIZADA","Observation":"Visita negocio QA positiva","Evidence":"/uploads/expedientes/qa/SOL_CRED.pdf"}}'
    $bureauJson = '{"Consulted":true,"BureauName":"SIN_RIESGO","ConsultationDate":"2026-05-01","ReportNumber":"SIN-QA-2026","Result":"ACEPTABLE","Score":710,"Classification":"A","ExternalDebt":1000,"ExternalInstallment":120,"InternalDebt":0,"InternalInstallment":0,"RequestedAmount":18000,"RequestedInstallment":2100,"TotalDebt":1000,"TotalInstallment":120,"PaymentCapacity":12500,"DebtCapacityRatio":18,"Alerts":[],"Notes":"QA expediente aprobable"}'
    Invoke-NonQuery @"
IF NOT EXISTS (SELECT 1 FROM creditos.solicitud_credito WHERE numero_solicitud=@numero)
INSERT INTO creditos.solicitud_credito
(id_cliente, numero_solicitud, fecha_solicitud, monto_solicitado, plazo_meses, tasa_interes_anual, moneda, destino_credito, estado_solicitud, observacion, producto_credito, frecuencia_pago, tipo_cuota, cuota_estimada, ingresos_declarados, egresos_declarados, capacidad_pago, fuente_ingreso, actividad_financiada, tipo_garantia, descripcion_garantia, valor_garantia, requiere_comite, nivel_riesgo, clasificacion_conami, checklist_json, usuario_registro, etapa_prospeccion, promotor_credito, sucursal_credito, oficina_credito, fecha_sistema_prospeccion, referencias_prospeccion_json, visitas_prospeccion_json, fecha_consulta_central, central_riesgo_json, tasa_comision_ascc, tasa_deslizamiento_anual, tasa_mora_anual)
VALUES (@cliente, @numero, CONVERT(date,'2026-05-01'), @monto, 12, 28, N'NIO', N'Capital de trabajo', @estado, N'Expediente QA para bandeja de aprobacion.', N'MICROCREDITO', N'MENSUAL', N'NIVELADA', 2050, 24000, 8500, 15500, N'Negocio propio', N'Pulperia', N'FIADOR', N'Fiador solidario QA', @monto, CASE WHEN @estado = N'COMITE' THEN 1 ELSE 0 END, N'BAJO', N'A', @checklist, @usuario, @stage, @usuario, N'Casa Matriz', N'CASA', CONVERT(date,'2026-05-01'), @referencias, @visitas, CONVERT(date,'2026-05-01'), @bureau, 5, 0, 48);
"@ @{ cliente=$clientId; numero="QA-EVAL-SOL-2026-$suffix"; monto=(15000 + ($i * 850)); estado=$status; checklist=$checklistJson; usuario=$officerUser; stage=$stage; bureau=$bureauJson; referencias=$referencesJson; visitas=$visitsJson }
}

$conceptCommission = [int64](Invoke-Scalar "SELECT TOP (1) id_concepto_nomina FROM nomina.concepto_nomina WHERE codigo_concepto=N'COMISION' ORDER BY id_concepto_nomina")
$schemeTypePlacement = [int64](Invoke-Scalar "SELECT TOP (1) id_tipo_esquema_variable FROM nomina.tipo_esquema_variable WHERE codigo_tipo_esquema=N'COMISION_COLOCACION'")
$schemeTypeRecovery = [int64](Invoke-Scalar "SELECT TOP (1) id_tipo_esquema_variable FROM nomina.tipo_esquema_variable WHERE codigo_tipo_esquema=N'COMISION_RECUPERACION'")

foreach ($officerUser in $officers) {
    $employeeId = $employeeIds[$officerUser]
    $schemeId = Invoke-Scalar "SELECT id_esquema_variable_empleado FROM nomina.esquema_variable_empleado WHERE id_empleado=@empleado AND nombre_esquema=N'QA Comision por colocacion'" @{ empleado=$employeeId }
    if ($null -eq $schemeId -or $schemeId -eq [DBNull]::Value) {
        $schemeId = [int64](Invoke-Scalar "INSERT INTO nomina.esquema_variable_empleado (id_empleado, id_tipo_esquema_variable, nombre_esquema, aplica_desde, porcentaje_base, meta_minima, meta_objetivo, meta_sobrecumplimiento, activo, observacion, fecha_registro) OUTPUT INSERTED.id_esquema_variable_empleado VALUES (@empleado, @tipo, N'QA Comision por colocacion', CONVERT(date,'2026-05-01'), 1.25, 150000, 250000, 350000, 1, N'Esquema QA para oficiales de credito.', SYSDATETIME())" @{ empleado=$employeeId; tipo=$schemeTypePlacement })
        Invoke-NonQuery "INSERT INTO nomina.regla_esquema_variable (id_esquema_variable_empleado, tramo_desde, tramo_hasta, porcentaje_pago, monto_pago, orden_regla, activo, fecha_registro) VALUES (@esquema, 0, 149999, 0.50, NULL, 1, 1, SYSDATETIME()), (@esquema, 150000, 249999, 1.00, NULL, 2, 1, SYSDATETIME()), (@esquema, 250000, NULL, 1.50, NULL, 3, 1, SYSDATETIME())" @{ esquema=$schemeId }
    }
    $placed = [decimal](Invoke-Scalar "SELECT COALESCE(SUM(monto_aprobado),0) FROM creditos.credito c INNER JOIN creditos.asignacion_oficial_credito a ON a.id_credito=c.id_credito AND a.activo=1 WHERE a.id_usuario_oficial=@usuario AND c.numero_credito LIKE N'QA-CRD-2026-%'" @{ usuario=$userIds[$officerUser] })
    Invoke-NonQuery "IF NOT EXISTS (SELECT 1 FROM nomina.meta_variable_empleado WHERE id_empleado=@empleado AND periodo_referencia=N'2026-05' AND tipo_meta=N'COLOCACION') INSERT INTO nomina.meta_variable_empleado (id_empleado, periodo_referencia, tipo_meta, meta_asignada, meta_lograda, porcentaje_cumplimiento, observacion, fecha_registro) VALUES (@empleado, N'2026-05', N'COLOCACION', 250000, @lograda, CASE WHEN @lograda > 0 THEN (@lograda / 250000) * 100 ELSE 0 END, N'Meta QA colocacion mayo 2026.', SYSDATETIME())" @{ empleado=$employeeId; lograda=$placed }
    Invoke-NonQuery "IF NOT EXISTS (SELECT 1 FROM nomina.movimiento_variable_empleado WHERE id_empleado=@empleado AND observacion=N'Comision QA por colocacion mayo 2026') INSERT INTO nomina.movimiento_variable_empleado (id_empleado, id_concepto_nomina, fecha_movimiento, monto, observacion, aplicado_en_nomina, activo, fecha_registro) VALUES (@empleado, @concepto, CONVERT(date,'2026-05-31'), @monto, N'Comision QA por colocacion mayo 2026', 0, 1, SYSDATETIME())" @{ empleado=$employeeId; concepto=$conceptCommission; monto=([decimal]($placed * 0.0125)) }
}

foreach ($recovererUser in $recoverers) {
    $employeeId = $employeeIds[$recovererUser]
    if ($null -eq (Invoke-Scalar "SELECT id_esquema_variable_empleado FROM nomina.esquema_variable_empleado WHERE id_empleado=@empleado AND nombre_esquema=N'QA Comision por recuperacion'" @{ empleado=$employeeId })) {
        [void](Invoke-Scalar "INSERT INTO nomina.esquema_variable_empleado (id_empleado, id_tipo_esquema_variable, nombre_esquema, aplica_desde, porcentaje_base, meta_minima, meta_objetivo, meta_sobrecumplimiento, activo, observacion, fecha_registro) OUTPUT INSERTED.id_esquema_variable_empleado VALUES (@empleado, @tipo, N'QA Comision por recuperacion', CONVERT(date,'2026-05-01'), 0.75, 80000, 150000, 220000, 1, N'Esquema QA recuperacion.', SYSDATETIME())" @{ empleado=$employeeId; tipo=$schemeTypeRecovery })
    }
    Invoke-NonQuery "IF NOT EXISTS (SELECT 1 FROM nomina.meta_variable_empleado WHERE id_empleado=@empleado AND periodo_referencia=N'2026-05' AND tipo_meta=N'RECUPERACION') INSERT INTO nomina.meta_variable_empleado (id_empleado, periodo_referencia, tipo_meta, meta_asignada, meta_lograda, porcentaje_cumplimiento, observacion, fecha_registro) VALUES (@empleado, N'2026-05', N'RECUPERACION', 150000, 93500, 62.33, N'Meta QA recuperacion mayo 2026.', SYSDATETIME())" @{ empleado=$employeeId }
    Invoke-NonQuery "IF NOT EXISTS (SELECT 1 FROM nomina.movimiento_variable_empleado WHERE id_empleado=@empleado AND observacion=N'Comision QA por recuperacion mayo 2026') INSERT INTO nomina.movimiento_variable_empleado (id_empleado, id_concepto_nomina, fecha_movimiento, monto, observacion, aplicado_en_nomina, activo, fecha_registro) VALUES (@empleado, @concepto, CONVERT(date,'2026-05-31'), 701.25, N'Comision QA por recuperacion mayo 2026', 0, 1, SYSDATETIME())" @{ empleado=$employeeId; concepto=$conceptCommission }
}

$connection.Close()

$assetPath = Join-Path (Get-Location) "backend\Sifnic.Api\wwwroot\uploads\expedientes\qa"
New-Item -ItemType Directory -Path $assetPath -Force | Out-Null
"Placeholder QA documento identidad" | Set-Content -Path (Join-Path $assetPath "DOC_ID.pdf") -Encoding UTF8
"Placeholder QA solicitud de credito" | Set-Content -Path (Join-Path $assetPath "SOL_CRED.pdf") -Encoding UTF8
"Placeholder QA plan de pago" | Set-Content -Path (Join-Path $assetPath "PLAN_PAGO.pdf") -Encoding UTF8
[System.IO.File]::WriteAllBytes(
    (Join-Path $assetPath "foto_colaborador.png"),
    [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="))

Write-Host "Seed QA completado: usuarios, cartera, solicitudes, expedientes, metas y comisiones." -ForegroundColor Green
