# StockDesk

Sistema de gestión de inventario multi-almacén con **costeo promedio ponderado**
(weighted average cost), concurrencia optimista y control de acceso por roles.
Proyecto de portafolio construido en C# / .NET 8.

## Por qué este proyecto

Está inspirado en experiencia real trabajando con sistemas de inventario
conectados por API (control de existencias en bodega, entradas/salidas,
transferencias entre sucursales), pero el foco está puesto en una regla de
negocio contable que no aparece en otros proyectos del portafolio: el
**cálculo de costo promedio ponderado** tras cada entrada de mercancía y su
propagación correcta en transferencias entre almacenes, todo bajo concurrencia
optimista real con reintentos.

## Qué resuelve

- Registrar existencias de productos por almacén (`Stock`), con cantidad y
  costo promedio vigente.
- Registrar movimientos de inventario: **entradas** (`In`), **salidas**
  (`Out`) y **transferencias** (`Transfer`) entre almacenes, cada uno con sus
  propias reglas de validación.
- Mantener un **kardex** (historial de movimientos) paginado y auditable por
  producto/almacén/rango de fechas.
- Alertar sobre **bajo stock** (cantidad por debajo del punto de reorden).
- Controlar el acceso con autenticación **JWT** y dos roles: `Admin` (gestión
  de catálogos y usuarios) y `Operator` (registro de movimientos y consultas).

## Arquitectura

El proyecto está dividido en dos servicios independientes que se comunican
por HTTP, simulando una arquitectura de microservicios simple:

```
┌─────────────────────┐        HTTP (JWT Bearer)       ┌──────────────────────┐
│  StockDesk.Web       │ ──────────────────────────────▶│  StockDesk.Api        │
│  (Razor Pages)        │◀────────────────────────────── │  (ASP.NET Core Web API│
│  - Login con cookie   │     IHttpClientFactory          │  + EF Core)           │
│  - Dashboard/forms     │                                 │                       │
└─────────────────────┘                                 └──────────┬───────────┘
                                                                     │
                                                           ┌─────────▼─────────┐
                                                           │   SQL Server       │
                                                           │ (Docker / local)   │
                                                           └────────────────────┘
```

- **StockDesk.Api**: API REST con ASP.NET Core, EF Core, autenticación JWT y
  autorización por rol. Es la única capa que habla con la base de datos.
- **StockDesk.Web**: sitio Razor Pages tradicional (sin SPA ni JS pesado). Los
  *page handlers* del servidor llaman a la API usando `IHttpClientFactory` y
  reenvían el token JWT (guardado dentro de una cookie de autenticación del
  propio sitio) en el header `Authorization`. El navegador nunca ve ni maneja
  el JWT directamente ni hace llamadas AJAX a la API.
- Ambos proyectos son desplegables por separado (dos Dockerfiles, dos
  servicios en `docker-compose.yml`), como corresponde a servicios
  desacoplados.

## Decisiones técnicas relevantes

### Costeo promedio ponderado

Al registrar una entrada (`POST /api/movements/in`) con cantidad y costo
unitario, se recalcula el costo promedio del `Stock` afectado:

```
nuevoPromedio = (cantidadActual * promedioActual + cantidadEntrada * costoUnitarioEntrada)
                / (cantidadActual + cantidadEntrada)
```

Si no existe `Stock` previo para ese producto/almacén, se crea con
`Quantity = 0` y `AverageCost = 0`, de modo que el promedio resultante
coincide con el costo unitario de la primera entrada.

Las **salidas** (`POST /api/movements/out`) consumen al costo promedio
vigente sin modificarlo, y se valida que no se pueda sacar más cantidad de la
disponible (de lo contrario, `409 Conflict` con `ProblemDetails`).

Las **transferencias** (`POST /api/movements/transfer`) son atómicas (una
transacción de base de datos): restan del almacén origen sin alterar su
costo promedio, y sobre el almacén destino aplican la misma fórmula de
promedio ponderado usando el costo promedio del origen como "costo de
entrada". Se valida que origen y destino sean distintos.

### Concurrencia optimista (RowVersion + retry)

La tabla `Stock` tiene una columna `RowVersion` (`[Timestamp]`, mapeada a
`rowversion` en SQL Server) usada como token de concurrencia por EF Core. Si
dos movimientos concurrentes intentan actualizar el mismo `Stock`, EF Core
lanza `DbUpdateConcurrencyException` al guardar el segundo de ellos.

`MovementService` encapsula cada operación (entrada/salida/transferencia) en
un patrón *read-modify-write con reintento*: si ocurre un conflicto de
concurrencia, se destraquean las entidades, se vuelve a leer el `Stock`
actual y se recalcula desde cero, hasta un máximo de 3 intentos. Si se
agotan los reintentos, se devuelve `409 Conflict` (`ProblemDetails`).

Esto está verificado con una prueba de integración
(`ConcurrencyTests.ConcurrentStockIn_OnSameProductWarehouse_NoLostUpdates`)
que dispara 10 entradas concurrentes sobre el mismo producto/almacén con
`Task.WhenAll` y comprueba que la cantidad final es exactamente la suma
esperada (sin "lost updates"), además de una variante equivalente para
salidas concurrentes que verifica que no hay sobreventa.

> Nota sobre SQLite en pruebas: SQLite no tiene un tipo `rowversion` nativo.
> Para las pruebas (que usan SQLite in-memory) el `DbContext` asigna
> manualmente un nuevo token de 8 bytes en cada inserción/actualización de
> `Stock` (ver `StockDeskDbContext.StampSqliteRowVersions`), reproduciendo el
> mismo comportamiento de "cambia en cada escritura" que logra `rowversion`
> en SQL Server, sin depender de infraestructura externa para los tests.

### Paginación sin N+1

Los endpoints de stock, alertas de bajo stock y kardex devuelven resultados
paginados (`page`/`pageSize`) y proyectan directamente con `Select(...)` a
DTOs (incluyendo nombre de producto/almacén), en vez de usar `Include` y
materializar entidades completas. Esto evita el problema N+1 y hace que EF
Core genere una única consulta SQL con los `JOIN` necesarios.

### JWT + PBKDF2 + roles

- Las contraseñas se almacenan con **PBKDF2** (`Rfc2898DeriveBytes.Pbkdf2`,
  SHA-256, 100 000 iteraciones, salt aleatorio de 128 bits), sin
  dependencias externas.
- La autenticación usa JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`).
- Autorización por rol: `Admin` puede crear productos, almacenes y usuarios;
  `Operator` puede registrar movimientos y consultar. Ambos roles pueden
  consultar stock, kardex y alertas.
- Se siembra (`DbSeeder`) un usuario administrador de desarrollo:
  **usuario `admin`, contraseña `Admin123!`**. Esto es solo para arrancar el
  sistema localmente — **cámbielo (o cree un usuario nuevo y elimine este)
  antes de usar el proyecto en cualquier entorno real.**

### Errores con ProblemDetails

Un middleware (`ExceptionHandlingMiddleware`) traduce:
- `BusinessRuleException` → `400`/`409` según el caso (p. ej. stock
  insuficiente, SKU duplicado, origen == destino en transferencias).
- `ConcurrencyConflictException` (reintentos agotados) → `409`.
- Cualquier otra excepción no controlada → `500`.

Todas como `application/problem+json` estándar de ASP.NET Core.

## Migraciones de EF Core

Sí se generaron migraciones reales de EF Core (no se usó únicamente
`EnsureCreated`). Se instaló `dotnet-ef` como herramienta local
(`dotnet tool install dotnet-ef`, ver `dotnet-tools.json`) y se generó:

```
dotnet tool run dotnet-ef migrations add InitialCreate \
  --project src/StockDesk.Api/StockDesk.Api.csproj \
  --startup-project src/StockDesk.Api/StockDesk.Api.csproj \
  -o Data/Migrations
```

El resultado está en `src/StockDesk.Api/Data/Migrations/`. En el arranque de
la API (`Program.cs`):

- Si el proveedor configurado es **SQL Server** (desarrollo normal / Docker /
  producción), se aplica `Database.Migrate()` con esas migraciones reales.
- Si el proveedor es **SQLite** (únicamente usado por
  `WebApplicationFactory` en las pruebas de integración, ya que las
  migraciones generadas son específicas de SQL Server y no son compatibles
  con SQLite), se usa `Database.EnsureCreated()` en su lugar. Esto se declara
  explícitamente en el código (`Program.cs`) y aquí para ser honestos sobre
  el alcance: **las pruebas no corren sobre las migraciones de SQL Server**,
  corren sobre un esquema equivalente creado con `EnsureCreated()`.

## Estructura del repositorio

```
StockDesk.sln
src/
  StockDesk.Api/        API REST (.NET 8, EF Core, JWT, Swagger)
  StockDesk.Web/         Razor Pages (.NET 8, IHttpClientFactory, cookie auth)
tests/
  StockDesk.Api.Tests/   xUnit (SQLite in-memory, WebApplicationFactory)
.github/workflows/ci.yml Build + test en GitHub Actions
Dockerfile.api
Dockerfile.web
docker-compose.yml       sqlserver + api + web
```

## Cómo correrlo

### Opción 1: local con `dotnet run` (requiere SQL Server accesible)

1. Levante un SQL Server local (puede usar el mismo `docker-compose.yml`
   solo para el servicio `sqlserver`: `docker compose up sqlserver -d`) o
   ajuste `ConnectionStrings:Default` en
   `src/StockDesk.Api/appsettings.json` / variables de entorno para apuntar
   a su propia instancia.
2. Backend:
   ```
   dotnet run --project src/StockDesk.Api/StockDesk.Api.csproj
   ```
   La API aplica migraciones automáticamente al arrancar y siembra el
   usuario `admin`/`Admin123!`. Swagger queda disponible en `/swagger` en
   entorno de desarrollo.
3. Frontend (en otra terminal):
   ```
   dotnet run --project src/StockDesk.Web/StockDesk.Web.csproj
   ```
   Configure `Api:BaseUrl` en `src/StockDesk.Web/appsettings.json` (o la
   variable de entorno `Api__BaseUrl`) para que apunte a la URL donde quedó
   escuchando la API.

### Opción 2: Docker Compose (todo junto)

```
docker compose up --build
```

Esto levanta `sqlserver`, `api` (puerto `5080`) y `web` (puerto `5081`). Las
contraseñas y la clave JWT en `docker-compose.yml` son valores de ejemplo
**solo para desarrollo** (claramente marcados en el archivo); cualquier uso
fuera de su máquina local debe reemplazarlos por secretos reales inyectados
de forma segura.

## Cómo probarlo

### Pruebas automatizadas

```
dotnet test StockDesk.sln
```

17 pruebas xUnit, todas corren sin SQL Server ni Docker (SQLite in-memory +
`WebApplicationFactory<Program>`). Cubren:

- Costeo promedio ponderado (primera entrada y entradas sucesivas).
- Salida rechazada por stock insuficiente (`409`).
- Transferencia atómica correcta (resta origen, recalcula promedio destino).
- Transferencia rechazada si origen y destino son el mismo almacén.
- Transferencia rechazada por stock insuficiente en origen.
- Concurrencia optimista: movimientos concurrentes (`Task.WhenAll`) sobre el
  mismo `Stock`, verificando que no se pierden actualizaciones (ni en
  entradas ni en salidas).
- Alertas de bajo stock (solo productos en o bajo su punto de reorden).
- Kardex paginado (orden descendente por fecha, conteo total correcto).
- Autenticación: login válido devuelve JWT, login inválido devuelve `401`,
  rol `Operator` recibe `403` al intentar crear un producto, rol `Admin`
  puede crearlo (`201`).

### Prueba manual vía Swagger

1. Levante la API (`dotnet run` o Docker Compose).
2. Abra `http://localhost:5080/swagger` (o el puerto que corresponda).
3. `POST /api/auth/login` con `{"username": "admin", "password": "Admin123!"}`
   para obtener un JWT.
4. Use el botón **Authorize** de Swagger con `Bearer <token>` para probar el
   resto de los endpoints protegidos.

### Prueba manual vía la web

1. Levante API y Web (local o Docker Compose).
2. Entre a la Web (`http://localhost:5081` en Docker Compose) e inicie
   sesión con `admin` / `Admin123!`.
3. Desde el menú puede: crear almacenes y productos (como Admin), registrar
   entradas/salidas/transferencias, ver el dashboard de stock, el kardex de
   un producto y las alertas de bajo stock.

## Seguridad y valores de ejemplo

- La contraseña del usuario `admin` sembrado, la contraseña de SQL Server y
  la clave de firma JWT que aparecen en este repositorio (`appsettings.json`,
  `docker-compose.yml`) son **valores de desarrollo**, explícitamente
  marcados como tales en el código. Deben reemplazarse por secretos reales
  gestionados de forma segura (variables de entorno del entorno de
  despliegue, un gestor de secretos, etc.) antes de cualquier uso fuera de
  una máquina de desarrollo local.
