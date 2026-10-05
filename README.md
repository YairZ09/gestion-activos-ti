# Sistema de Gestión y Resguardo de Activos TI

API REST para administrar activos de TI (laptops, monitores, celulares, periféricos, equipos arrendados): quién tiene cada activo, dónde está, qué proveedor lo suministra/arrienda/mantiene y su historial completo.

**Stack:** ASP.NET Core Web API (.NET 10) · SQL Server · ADO.NET (`Microsoft.Data.SqlClient`) · Stored Procedures · JWT · Swagger · xUnit.

---

## Instalación y ejecución

### Requisitos
- .NET SDK 10 o superior
- SQL Server (LocalDB, Express, Developer o Docker). Por defecto usa **LocalDB** (`(localdb)\MSSQLLocalDB`), que viene con Visual Studio.

### Pasos
```bash
# 1. Ejecutar la API (en Development crea la BD, tablas, SPs, usuarios y datos de ejemplo automáticamente)
dotnet run --project src/AssetManagement.Api --launch-profile http
# (el perfil "http" fija ASPNETCORE_ENVIRONMENT=Development y la URL http://localhost:5080)

# 2. Abrir Swagger
#    http://localhost:5080/swagger   (botón "Authorize" -> pegar el accessToken de /api/auth/login)

# 3. Pruebas automatizadas (crean/recrean una BD aparte: AssetManagement_Tests)
dotnet test
```

> **Windows con rutas largas:** si `dotnet` falla con "supera el límite máximo para la ruta de acceso", clona el repositorio en una carpeta corta (p. ej. `C:\dev\assets`).

Con otro SQL Server (Docker, Express, remoto) basta con sobrescribir la cadena de conexión:
```bash
# Linux/macOS
export ConnectionStrings__Default="Server=localhost,1433;Database=AssetManagement;User Id=sa;Password=<tu-password>;TrustServerCertificate=True"
# PowerShell
$env:ConnectionStrings__Default = "Server=localhost,1433;Database=AssetManagement;User Id=sa;Password=<tu-password>;TrustServerCertificate=True"
```

### Scripts de base de datos
Están en [`src/AssetManagement.Api/Database`](src/AssetManagement.Api/Database) y son **idempotentes** (se pueden correr varias veces):

| Script | Contenido |
|---|---|
| `001_schema.sql` | Tablas, restricciones CHECK/FK e índices únicos |
| `002_procedures.sql` | Todos los stored procedures |
| `003_sample_data.sql` | Datos de ejemplo (solo si `Database:SeedSampleData=true`) |

Con `Database:AutoMigrate=true` (por defecto) la API los ejecuta al arrancar. Para aplicarlos a mano en producción, ponlo en `false` y ejecuta `001` y `002` con `sqlcmd`/SSMS.

### Variables de entorno / configuración

| Variable | Descripción | Default |
|---|---|---|
| `ConnectionStrings__Default` | Cadena de conexión a SQL Server | LocalDB |
| `Jwt__Key` | **Obligatoria.** Clave HS256, mínimo 32 caracteres. La API no arranca sin ella. | (solo en `appsettings.Development.json`) |
| `Jwt__Issuer`, `Jwt__Audience` | Emisor/audiencia del token | `AssetManagement.Api` / `AssetManagement.Clients` |
| `Jwt__ExpiryMinutes` | Vigencia del token | `60` |
| `Security__MaxFailedLoginAttempts` | Fallos antes de bloquear la cuenta | `5` |
| `Security__LockoutMinutes` | Duración del bloqueo | `15` |
| `Security__LoginRequestsPerMinute` | Límite de `/login` por IP | `10` |
| `Seed__AdminPassword`, `Seed__OperatorPassword` | Si se definen, se crean los usuarios `admin` / `operador` con esa contraseña (guardada hasheada) | vacío |
| `Database__AutoMigrate` | Ejecutar scripts al arrancar | `true` |
| `Database__SeedSampleData` | Cargar datos de ejemplo | `false` (`true` en Development) |
| `Swagger__Enabled` | Swagger fuera de Development | `false` |

### Usuarios de prueba (solo entorno Development)

| Usuario | Contraseña | Perfil |
|---|---|---|
| `admin` | `Admin#2026!` | Administrador |
| `operador` | `Operador#2026!` | Operador |

Los datos de ejemplo incluyen el activo `TI-0001001` del escenario del enunciado, 3 colaboradores (uno inactivo, `E-0003`) y 2 proveedores.
Las contraseñas **no** están en el código ni en SQL: viven en `appsettings.Development.json` y la BD solo guarda el hash PBKDF2. En cualquier otro entorno hay que definirlas por variable de entorno.

### Colección de Postman
[`AssetManagement.postman_collection.json`](AssetManagement.postman_collection.json): importar y ejecutar primero *Auth → Login*; el script guarda el token en la variable `token`.

### Frontend (opcional, incluido)
Al ejecutar la API, abre **http://localhost:5080**. Es HTML + CSS + JavaScript sin frameworks, servido por la misma API desde `wwwroot/` (no requiere `npm` ni otro servidor). Cubre lo que pide el enunciado:

| Pantalla | Detalle |
|---|---|
| Inicio de sesión | Muestra el error del servidor (credenciales, bloqueo, límite de intentos) |
| Listado de activos | Filtros `search` / `status` / `category`, paginación, colaborador asignado |
| Alta de activo | Solo visible para Administrador; muestra los errores de validación del servidor |
| Asignación | Lista solo colaboradores activos; si otro usuario lo asignó antes, muestra el 409 |
| Devolución | Condición (Bueno / Regular / Dañado) y observaciones |
| Historial | Movimientos con fecha, detalle, usuario y observaciones |

Decisiones: el token se guarda en `sessionStorage` (se pierde al cerrar la pestaña y no viaja en cookies, así que no hay riesgo de CSRF); todo el contenido dinámico se inserta con `textContent`, nunca `innerHTML`, para evitar XSS; ocultar botones según el perfil es solo comodidad visual, la autorización real la aplica la API.

### Solución de problemas
- **"supera el límite máximo para la ruta de acceso"**: clona el repositorio en una ruta corta (p. ej. `C:\dev\assets`).
- **"Cannot create file '...AssetManagement.mdf' because it already exists"**: quedó un archivo `.mdf` de una base que ya no está registrada en LocalDB (típico si se recreó la instancia). Mueve o borra `AssetManagement*.mdf` / `*_log.ldf` de tu carpeta de usuario y vuelve a ejecutar.
- **No conecta a LocalDB**: `sqllocaldb start MSSQLLocalDB`.

---

## Estructura del proyecto

```
src/AssetManagement.Api/
├── Program.cs                 Configuración: DI, JWT, autorización, rate limiting, errores, Swagger, estáticos
├── Database/                  Scripts SQL (esquema, stored procedures, datos de ejemplo)
├── Data/                      SqlDb (ADO.NET + parámetros tipados) y DatabaseInitializer
├── Repositories/              Una clase por agregado: llaman a los SPs y mapean filas a DTOs
├── Security/                  PasswordHasher (PBKDF2), JwtTokenService, AuthService (login + bloqueo)
├── Errors/                    BusinessException, SqlErrorTranslator, GlobalExceptionHandler
├── Models/                    Requests (con validaciones) y Responses (DTOs)
├── Controllers/               Auth, Assets, Employees, Suppliers
└── wwwroot/                   Frontend (index.html, app.js, styles.css)
tests/AssetManagement.Tests/   Pruebas de integración (API real + SQL Server) y unitarias
```

Flujo de una petición: `Controller` (valida el modelo y el rol) → `Repository` (parámetros tipados) → **Stored Procedure** (regla de negocio + transacción + auditoría) → si el SP lanza `THROW 5000x`, `SqlErrorTranslator` lo convierte en `BusinessException` → `GlobalExceptionHandler` responde `ProblemDetails`.

---

## Endpoints

| Método | Ruta | Perfil | Descripción |
|---|---|---|---|
| POST | `/api/auth/login` | público | Devuelve JWT. Limitado por IP + bloqueo de cuenta |
| GET | `/api/assets?search=&status=&category=&page=&pageSize=` | cualquiera autenticado | Lista paginada con filtros (`pageSize` ≤ 100) |
| GET | `/api/assets/{id}` | autenticado | Detalle (incluye proveedor y colaborador asignado) |
| POST | `/api/assets` | Administrador | Alta (siempre nace `Disponible`) |
| PUT | `/api/assets/{id}` | Administrador | Edición, cambio de estado/ubicación |
| GET | `/api/assets/{id}/history` | autenticado | Historial de movimientos |
| POST | `/api/assets/{id}/assign` | Administrador, Operador | Asignar a colaborador (`{employeeId, notes}`) |
| POST | `/api/assets/{id}/return` | Administrador, Operador | Devolver (`{condition: Bueno\|Regular\|Danado, notes}`) |
| GET/POST | `/api/employees`, GET `/api/employees/{id}` | alta: Administrador | Colaboradores |
| GET/POST | `/api/suppliers`, GET `/api/suppliers/{id}` | alta: Administrador | Proveedores y sus servicios (Compra, Mantenimiento, Arrendamiento) |

Errores: siempre `application/problem+json` con `status`, `title`, `detail`, `code` (código estable para el cliente) y `traceId`.

| HTTP | Códigos `code` |
|---|---|
| 400 | validaciones del modelo, `SUPPLIER_REQUIRED`, `CONSTRAINT_VIOLATION` |
| 401 | `INVALID_CREDENTIALS` (o token ausente/expirado/inválido) |
| 403 | perfil sin permiso |
| 404 | `ASSET_NOT_FOUND`, `EMPLOYEE_NOT_FOUND`, `SUPPLIER_NOT_FOUND` |
| 409 | `ASSET_NOT_AVAILABLE`, `ASSET_ALREADY_ASSIGNED`, `EMPLOYEE_INACTIVE`, `NO_ACTIVE_ASSIGNMENT`, `INVALID_STATUS_TRANSITION`, `*_DUPLICATE` |
| 429 | `RATE_LIMITED`, `ACCOUNT_LOCKED` |
| 500 | `INTERNAL_ERROR` (mensaje genérico + `traceId`) |

---

## Decisiones técnicas

### 1. Concurrencia: un activo nunca queda asignado a dos colaboradores
Se combinan **tres defensas** (de la lógica a la garantía física):

1. **`usp_Asset_Assign` en una transacción** que lee la fila del activo con `UPDLOCK, HOLDLOCK`. Dos solicitudes simultáneas sobre el mismo activo se **serializan**: la segunda espera, ve el activo ya `Asignado` y recibe `50006 → 409 ASSET_ALREADY_ASSIGNED`. Activos distintos no se bloquean entre sí.
2. **Índice único filtrado** `UX_Assignments_ActiveAsset ON Assignments(AssetId) WHERE ReturnedAt IS NULL`: aunque alguien insertara una asignación por fuera del SP (otro servicio, script manual), SQL Server rechaza la segunda asignación abierta (error 2601, que la API también traduce a 409).
3. **Errores de negocio controlados**: el SP lanza `THROW 5000x`; la API traduce por *número* de error (`SqlErrorTranslator`) y jamás reenvía el texto de SQL.

La devolución usa el mismo patrón, por eso 10 devoluciones simultáneas producen un 200 y nueve 409.
Prueba: `AssignmentTests.Concurrent_assignments_of_the_same_asset_only_one_succeeds` (10 solicitudes paralelas: 1 × 201, 9 × 409, ningún 500 y un solo movimiento `Asignacion`).

### 2. Reglas de negocio en la base, validación de formato en la API
- **SPs** hacen lo que requiere atomicidad (asignar/devolver, transiciones de estado, auditoría). Así la regla se cumple sin importar qué cliente llame.
- **La API** valida forma y formato antes de tocar la BD (obligatorios, longitudes, listas permitidas, correo, regex de códigos, `RentalEndDate ≥ PurchaseDate`). La BD además tiene `CHECK`/`UNIQUE`/`FK` como red de seguridad final (p. ej. *arrendado ⇒ proveedor* está validado en la API, en el SP y como `CHECK`).
- Estados: `Disponible → Mantenimiento/Retirado`, `Mantenimiento → Disponible/Retirado`. `Asignado` solo se alcanza/abandona con asignar/devolver y `Retirado` es terminal.

### 3. Auditoría / historial
Tabla `AssetMovements` (alta, asignación, devolución, cambio de estado, cambio de ubicación) con usuario, fecha UTC y observaciones. Se escribe **dentro de la misma transacción** que el cambio, por lo que no puede haber cambio sin rastro. `Assignments` guarda además quién asignó/devolvió, fecha y condición de devolución.

### 4. Seguridad
- **Contraseñas:** PBKDF2-HMAC-SHA256, 310 000 iteraciones, sal de 16 bytes por usuario, comparación en tiempo constante, formato versionado (`v1.iter.sal.hash`). Nunca se guarda ni compara texto plano.
- **JWT:** HS256 con algoritmo fijado, valida emisor, audiencia, firma y expiración, **sin tolerancia de reloj** (`ClockSkew = 0`). La clave se exige por configuración (≥ 32 caracteres) y la API no arranca sin ella.
- **Autorización:** política por defecto = autenticado (cualquier endpoint nuevo nace protegido); `[AllowAnonymous]` solo en login. Roles `Administrador` y `Operador`.
- **Límite de intentos (dos capas):** (a) *rate limiter* de ASP.NET por IP en `/login` (429); (b) bloqueo temporal de la cuenta tras N fallos, persistido en BD con un único `UPDATE` atómico (resiste intentos en paralelo). El éxito reinicia el contador. Mensaje genérico en credenciales inválidas.
- **Manejo global de errores:** `IExceptionHandler` + `ProblemDetails`. Los errores inesperados se loguean en servidor y el cliente solo ve un mensaje genérico con `traceId`; no se expone SQL, stack traces, cadenas de conexión ni tokens. Hay prueba unitaria que lo verifica.
- **SQL:** solo SPs con parámetros tipados (sin concatenación → sin inyección).
- Cabeceras `X-Content-Type-Options: nosniff` y `Cache-Control: no-store`.

### 5. Otras decisiones
- **Sin interfaces/repositorio genérico:** el acceso a datos son 4 clases pequeñas sobre un helper (`SqlDb`); las pruebas de integración corren contra SQL Server real porque lo valioso aquí (bloqueos, índices, SPs) no se puede simular con mocks.
- **Fechas en UTC** (`SYSUTCDATETIME()`), serializadas con `Z`.
- **Estados y tipos en español** (`Disponible`, `Propio`…) tal como en el enunciado; campos y rutas en inglés.
- `AssetCode` no se puede modificar tras el alta (es el identificador físico/etiqueta).

---

## Pruebas automatizadas (68)

`dotnet test` levanta la API real con `WebApplicationFactory` contra una BD exclusiva (`AssetManagement_Tests`, se elimina y recrea en cada ejecución; sobrescribible con `TEST_DB_CONNECTION`).

| Archivo | Cubre |
|---|---|
| `AuthTests` | login válido/inválido, token expirado / firma ajena / válido, 401 sin token, 403 por perfil, bloqueo tras fallos, reinicio de contador |
| `AssetTests` | alta + movimiento `Alta`, duplicados (código, serie), serie opcional, arrendado ⇒ proveedor, validaciones, filtros + paginación, historial de cambios, transiciones inválidas, 404 |
| `AssignmentTests` | flujo asignar/devolver con auditoría, reasignación, activo ya asignado, colaborador inactivo, 404, activo en mantenimiento/retirado, devolución sin asignación, **concurrencia** |
| `EmployeeSupplierTests` | altas, formato de correo, unicidad, servicios de proveedor, filtros |
| `UnitTests` | hasher, validación de modelos, traductor de errores SQL, handler global (sin filtrar detalles) |

---

## Funcionalidades pendientes y riesgos

**Pendiente (no incluido):**
- Frontend: incluye las 6 pantallas pedidas, pero no la administración de colaboradores ni de proveedores, ni la edición de activos (esas operaciones existen en la API y se usan desde Swagger o Postman).
- Gestión de usuarios por API (alta, cambio de contraseña, desbloqueo): hoy los usuarios se siembran por configuración.
- Edición y baja/reactivación de colaboradores y proveedores (el enunciado pedía consulta y alta). Desactivar un colaborador con activos asignados requeriría una regla (bloquear o forzar devolución).
- Refresh tokens / revocación de JWT (hoy un token vive hasta su expiración).
- Paginación del historial (hoy devuelve todos los movimientos del activo).
- Registrar en el historial los cambios de marca, modelo, serie, etc. (hoy se registran los cambios de estado y de ubicación, que son los que pide el enunciado).
- Reporte de arrendamientos próximos a vencer (`RentalEndDate`) y alertas.
- Logging estructurado/centralizado y métricas.

**Cómo continuaría:** (1) endpoints de usuarios + política de contraseñas; (2) `PUT` de colaboradores/proveedores con la regla de desactivación; (3) paginación y filtros por fecha/usuario en el historial; (4) refresh tokens con tabla de revocación; (5) CI con SQL Server en contenedor para correr `dotnet test`.

**Riesgos identificados:**
- **Rate limiter en memoria por instancia:** con varias instancias de la API cada una cuenta aparte (el bloqueo de cuenta sí es global porque está en BD). Solución: límite en el *gateway* o limiter distribuido.
- **IP detrás de proxy:** el límite por IP necesita `UseForwardedHeaders` correctamente configurado; si no, todos comparten la IP del proxy.
- **Contraseñas semilla** de Development son conocidas: no usarlas fuera de desarrollo (por eso solo existen en `appsettings.Development.json`).
- **Enumeración de usuarios:** el mensaje de error es el mismo exista o no el usuario, pero un usuario bloqueado devuelve 429 y, como solo se calcula el hash cuando el usuario existe, el tiempo de respuesta podría delatar qué usuarios existen. Mitigación posible: verificar contra un hash falso cuando el usuario no existe (no se incluyó para no ir más allá del alcance pedido).
- **`AutoMigrate` en producción:** cómodo pero conviene desactivarlo y aplicar scripts con un proceso controlado.
- **Bloqueos:** el diseño serializa por activo; si el volumen crece mucho, vigilar duración de transacciones (son muy cortas) y deadlocks (el error 1205 ya se traduce a 409 reintentable).

---

## Uso de herramientas de IA

**Herramienta utilizada:** Claude Code (modelos Claude Sonnet 5.5 y Claude Opus 5.5).

**Mi participación en el desarrollo**

Utilicé la IA como herramienta de implementación bajo mi dirección. Mi trabajo se centró en dirigir, cuestionar, decidir y validar:

- **Dirección del trabajo:** analicé el instructivo, evalué si el alcance era viable en el plazo y prioricé backend, base de datos y seguridad, que son el foco de la evaluación. El frontend quedó como entregable adicional.
- **Preparación del entorno:** instalé y configuré SQL Server Management Studio y la conexión a LocalDB, y creé una base de práctica propia para trabajar el diseño de tablas.
- **Revisión crítica del alcance:** cuestioné si la solución generada era proporcional al plazo y a lo solicitado. Con base en esa revisión retiré funcionalidades que excedían los requerimientos y las dejé documentadas como riesgos o mejoras.
- **Verificación contra el enunciado:** revisé cada punto de la evaluación con una trazabilidad requisito → archivo → línea, para confirmar que cada regla tiene una implementación identificable y una prueba que la respalda.
- **Interpretación de requisitos:** contrasté las decisiones con el texto del instructivo. Por ejemplo, confirmé que un frontend sin framework cumple lo solicitado ("u otra tecnología de su preferencia") antes de decidir conservarlo.
- **Ajuste del código y la documentación:** limpié comentarios del código y corregí inconsistencias del README.

**Intervención de la IA**
- Generación de la implementación: esquema SQL, stored procedures, API, pruebas automatizadas, frontend, colección de Postman y borrador de este README.
- Aplicación de los ajustes que decidí: reducción del alcance y correcciones de la documentación.
- Apoyo para el análisis: explicación de cada componente y trazabilidad de los requisitos al código.

**Validaciones realizadas**
- Compilación sin errores ni advertencias.
- 68 pruebas automatizadas contra una instancia real de SQL Server, que cubren autenticación, autorización por perfil, bloqueo por intentos fallidos, validaciones, reglas de asignación y devolución, historial y manejo de errores.
- Regla crítica de concurrencia: 10 asignaciones simultáneas del mismo activo producen exactamente 1 éxito y 9 respuestas 409, sin errores 500 y con un solo movimiento de asignación en el historial.
- Recorrido manual en Swagger: login, filtros, asignación, doble asignación (409), colaborador inactivo (409), devolución, historial y acceso sin token (401).
- Verificación manual de que los errores (por ejemplo, un AssetCode duplicado) no exponen detalles internos de SQL.
- Verificación manual del bloqueo de cuenta tras intentos fallidos (429).

**Decisiones técnicas tomadas personalmente**
- **Ajuste del alcance a lo solicitado:** retiré protecciones adicionales que la propuesta inicial incluía y el enunciado no pedía (hash señuelo en el login, escape de comodines en la búsqueda y registro en el historial de cambios menores como marca o modelo). Preferí entregar solo lo que puedo justificar y documentar el resto como riesgos o mejoras.
- **Prueba de concurrencia con 10 solicitudes simultáneas** en lugar de 25: es la evidencia de la regla crítica principal, y 10 solicitudes bastan para demostrarla.
- **Frontend sin framework, servido por la propia API:** el instructivo lo permite. Evita dependencias adicionales (Node, npm) y mantiene el foco en backend y base de datos.
- **Convención de nombres:** identificadores en inglés (tablas, columnas, procedimientos, rutas y código) y valores de negocio en español tal como los define el enunciado (`Disponible`, `Asignado`, `Administrador`, `Operador`).
- **Documentar los riesgos conocidos** en lugar de omitirlos (sección "Funcionalidades pendientes y riesgos").

## Tiempo aproximado invertido

Aproximadamente **8.5 horas**:
- Análisis del instructivo y planeación: 1.5 h
- Preparación del entorno (SQL Server, SSMS, LocalDB): 1.5 h
- Generación de la solución con IA, revisión y ajustes: 3.5 h
- Verificación contra los requisitos y estudio del código: 2.0 h
