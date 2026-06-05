# ChetoGen

> 🌐 **Español** · [English](./README.en.md)

**Generador de slices CRUD por capas para .NET, agnóstico al sistema que lo use.** Se instala como un `dotnet tool`, genera todos los archivos de las capas (Domain → Application → Infrastructure → API → Client) y parchea los archivos compartidos (DI, `AppDbContext`, `NavMenu`, `Program.cs`) en una sola corrida. Las pantallas Blazor usan **Bootstrap 5 + Bootstrap Icons** con filtros inteligentes por tipo, columnas ordenables y paginación server-side opcional.

Nada está hardcodeado a una solución concreta: el **namespace base, las rutas, las templates y los mutadores** salen de la configuración. Trae un set de templates por defecto (la convención `{BaseNamespace}.<Capa>`) que **cualquier cliente puede sobrescribir** apuntando a su propia carpeta de templates.

> 🛠 **¿Vas a tocar el código de ChetoGen?** Empezá por [`ARCHITECTURE.md`](./ARCHITECTURE.md) — pipeline, capa de configuración, layout, convenciones y recetas.

---

## Instalación

ChetoGen se empaqueta como **.NET tool**. Para generar el `.nupkg` desde este repo:

```pwsh
dotnet pack ChetoGen/ChetoGen.csproj -c Release
# -> artifacts/nupkg/ChetoGen.<version>.nupkg
```

Instalarlo desde ese feed local (global, local o portable):

```pwsh
# Global
dotnet tool install --global ChetoGen --add-source ./artifacts/nupkg

# Local (en la solución que vas a generar; usa un manifest .config/dotnet-tools.json)
dotnet new tool-manifest
dotnet tool install ChetoGen --add-source <ruta-al-nupkg>

# Portable (a una carpeta, sin tocar el manifest del repo)
dotnet tool install ChetoGen --tool-path C:\tools\chetogen --add-source <ruta-al-nupkg>
```

Una vez instalado, el comando es **`chetogen`** (tool global/portable) o **`dotnet chetogen`** (tool local).

---

## Quickstart

```pwsh
# 1. (Opcional) Crear un chetogen.json con el asistente interactivo.
#    Te pregunta namespace, capas, archivos compartidos, layout y templates.
chetogen init                 # asistente · o:  chetogen init --yes (defaults, sin preguntas)

# 2. Generar una entidad.
chetogen generate Order
```

Sin `chetogen.json`, ChetoGen igual funciona: camina hacia arriba buscando un `*.slnx`/`*.sln`, **infiere el `baseNamespace` del nombre del archivo de solución** (p. ej. `Acme.slnx → "Acme"`) y usa el layout por defecto. El `chetogen.json` sólo existe para overridear eso.

### Modo interactivo

`chetogen generate` sin más argumentos te guía paso a paso:

- **Nombre y tipo de Id** (el nombre se normaliza a PascalCase, p. ej. `cliente` → `Cliente`).
- **Editor de propiedades** con tabla en vivo y menú **Add / Edit / Remove / Done**: si te equivocaste en algo (tipo, requerido, filtro…) **editás esa fila** en vez de reiniciar. Por campo elegís nombre, tipo y, en un multi-select, **Required / Filterable / Show in table / Sortable** (con defaults sensatos según el tipo).
- Luego: pantallas Blazor, NavLink, event bus y modo **client/server** (+ tamaño de página en server).

> En los pasos sí/no (Blazor, NavLink, event bus) y en el confirm final de generación, **`Esc` vuelve al paso anterior** (`Enter` = default · `Y` = sí · `N` = no). `Esc` en el primer paso reabre el editor de propiedades.

### Modo no-interactivo

```pwsh
chetogen generate Order `
  --id long `
  --prop "Total:decimal:required:filter:sort" `
  --prop "Notes:string:hidden" `
  --prop "PlacedAt:DateTime:required:filter:sort" `
  --filter-mode server `
  --page-size 25 `
  --event-bus `
  --yes
```

---

## Configuración — `chetogen.json`

Se ubica en la raíz de tu solución (ChetoGen lo descubre caminando hacia arriba). **Todas las claves son opcionales** y se mergean sobre los defaults. `chetogen init` te escribe uno comentado.

```jsonc
{
  // Namespace / prefijo de proyectos de TU solución. Reemplaza {BaseNamespace} en todos lados.
  "baseNamespace": "AspireApp",

  // Cómo se ubica la raíz de la solución al caminar hacia arriba.
  "rootMarkers": ["*.slnx", "*.sln"],

  // Carpeta (relativa a este archivo) con templates que sobrescriben/extienden las integradas.
  "templatesDirectory": "chetogen-templates",

  // Proyecto que aparece en el hint de "próximos pasos".
  "appHostProject": "{BaseNamespace}.AppHost",

  // Pasos del pipeline a saltear, por su "friendly label" (p. ej. para no generar el Edit de Blazor).
  "excludeTemplates": ["Client.Edit.razor", "Client.Edit.razor.cs"],

  // Override de rutas individuales. Se expanden {BaseNamespace} y {Entity}; usá '/' como separador.
  "paths": {
    "DomainEntity": "{BaseNamespace}.Domain.Entities/{Entity}.cs"
  },

  // Clases base / interfaces / wrapper de resultado sobre los que se genera el código.
  // Override sólo las claves que difieran de tu arquitectura; el resto cae al default.
  "architecture": {
    "ResultType": "Result",
    "ResultIsSuccess": "Success"
  },

  // Tokens estáticos extra; ganan sobre los integrados (incluido BASE_NS).
  "tokens": {}
}
```

> Los comentarios `//` y las comas finales están permitidos: el loader parsea con `ReadCommentHandling = Skip` y `AllowTrailingCommas`.

### Claves de `paths`

`DomainEntity`, `ApplicationModel`, `ApplicationFilter`, `ApplicationPaging`, `ApplicationContract`, `ApplicationService`, `ApplicationMapper`, `ApplicationPersistence`, `DataAccess`, `ApiController`, `ApiClient`, `BlazorIndexRazor`, `BlazorIndexCs`, `BlazorEditRazor`, `BlazorEditCs`, `AppDbContext`, `DataAccessDI`, `ApplicationDI`, `MappersDI`, `ClientProgram`, `NavMenu`.

### Arquitectura parametrizable — `architecture`

El código generado se apoya en una arquitectura base: entidades sobre `BaseEntity`, servicios sobre `BaseService`, controllers sobre `BaseController`, repos sobre `BaseDA`, un cliente HTTP sobre `BaseApiClient` y un wrapper de resultado (ROP) `Result<T>`. **Cada uno de esos puntos de apoyo es un token configurable** bajo `"architecture"`, así una sola plantilla sirve para **cualquier** arquitectura sin tocar las templates. Los valores admiten los placeholders `{BaseNamespace}`, `{Entity}`, `{EntityCamel}`, `{Id}`, `{EventBusCtorParam}` y `{EventBusBaseArg}`.

Override **sólo** las claves que difieran en tu solución; el resto cae al default. Ejemplos típicos:

```jsonc
"architecture": {
  // Tu wrapper de resultado se llama distinto (p. ej. una mónada Either):
  "ResultType": "Either",
  "ResultIsSuccess": "IsRight",
  "ResultValue": "Right",
  "ResultErrors": "Error",

  // Tus entidades viven en otro namespace y heredan de otra base:
  "EntityUsings": "using MiEmpresa.Core.Domain;\n",
  "EntityBase": " : Entidad<{Id}>"
}
```

**Claves disponibles** (24): `EntityUsings`, `EntityBase`, `ModelBase`, `MapperBase`, `ServiceContractUsings`, `ServiceContractBase`, `PersistenceContractUsings`, `PersistenceContractBase`, `ServiceImplUsings`, `CacheUsing`, `ServiceCtor`, `ServiceBase`, `DataAccessUsing`, `DataAccessCtor`, `DataAccessBase`, `ControllerCtor`, `ControllerBase`, `ApiClientUsing`, `ApiClientCtor`, `ApiClientBase`, `ResultType`, `ResultIsSuccess`, `ResultValue`, `ResultErrors`. Sus defaults completos están en [`chetogen.example.json`](./chetogen.example.json) y `chetogen init` (elegí *"mapear a mis propias clases base"*) te vuelca el bloque entero listo para editar.

> El cuerpo generado **asume que tu clase base provee el CRUD** (`GetAll`/`Get`/`Create`/…). Estos tokens **remapean** a tus clases base equivalentes, no las eliminan. Si no usás clases base y querés cuerpos completos, copiá las templates (`--with-templates`) y editalas.

### Templates propias (override)

Corré `chetogen init --with-templates` para copiar el set integrado a `./chetogen-templates`, o creá la carpeta vos. ChetoGen busca **cada template por nombre primero en `templatesDirectory`** y, si no la encuentra, cae a la integrada. Así sobrescribís sólo las que querés y dejás el resto. Las templates son texto plano con tokens `{{TOKEN}}` (no es Scriban real — ver la tabla de tokens abajo).

---

## Opciones de CLI

### `chetogen generate`

| Opción                | Descripción                                                                                            |
| --------------------- | ------------------------------------------------------------------------------------------------------ |
| `ENTITY_NAME`         | Nombre PascalCase singular de la entidad (e.g. `Order`).                                               |
| `--id <ID_TYPE>`      | Tipo del Id: `long`, `int` o `Guid`. Default `long`.                                                   |
| `-p, --prop <PROP>`   | Propiedad en formato `Name:type[:flag1[:flag2...]]`. Repetible. Flags abajo.                           |
| `--icon <ICON>`       | Override del Bootstrap Icon (sin el prefijo `bi-`). Auto-detectado por nombre.                         |
| `--accent <ACCENT>`   | Acento Bootstrap: `primary`, `success`, `info`, `warning`, `danger`, `secondary`.                     |
| `--filter-mode <M>`   | `client` (default) o `server`. Server agrega Filter DTO, endpoint POST `/query` y paginación.          |
| `--page-size <N>`     | Tamaño de página por defecto (server-mode). Default 25.                                                |
| `--no-ui`             | No generar las pantallas Blazor (`Index` + `Edit`).                                                    |
| `--no-nav`            | No agregar el `NavLink` a `NavMenu.razor`.                                                             |
| `--no-auth`           | No decorar el controller con `[Authorize]`.                                                            |
| `--event-bus`         | Inyectar `IMessageBus` y publicar un evento al crear.                                                  |
| `--dry-run`           | Mostrar el plan sin tocar nada en disco.                                                               |
| `-y, --yes`           | Ejecutar sin pedir confirmación.                                                                       |
| `--root <PATH>`       | Path explícito a la raíz de la solución (auto-detectado por defecto).                                  |
| `--config <PATH>`     | Path explícito a un `chetogen.json` (auto-descubierto por defecto).                                    |

### `chetogen init`

Por defecto abre un **asistente interactivo** que arma el `chetogen.json` por vos: pregunta el namespace, qué **capas** generar, qué **archivos compartidos** parchear, si tu **layout** difiere del default, si usás las **clases base** de ChetoGen o querés mapearlas a las tuyas (vuelca el bloque `architecture` completo) y si querés **copiar las templates** para reescribir el cuerpo del código. Cierra con una vista previa del archivo antes de escribirlo. Con `--yes` (o si stdin está redirigido, p. ej. en CI) salta el asistente y escribe un archivo con defaults.

| Opción                  | Descripción                                                                       |
| ----------------------- | --------------------------------------------------------------------------------- |
| `--root <PATH>`         | Dónde escribir `chetogen.json`. Default: raíz de solución detectada.              |
| `--base-namespace <NS>` | Forzar el `baseNamespace`. Si se omite, se infiere del `.slnx`/`.sln`.            |
| `--with-templates`      | Copiar las templates integradas a `./chetogen-templates` para customizarlas.     |
| `--force`               | Sobrescribir un `chetogen.json` existente.                                        |
| `-y, --yes`             | Saltar el asistente y escribir un `chetogen.json` con defaults (modo CI).         |

### Flags de propiedad (`--prop "Name:type:flag1:flag2..."`)

| Flag                                 | Efecto                                                                                  |
| ------------------------------------ | --------------------------------------------------------------------------------------- |
| `required`                           | `[Required]` en el model + `IsRequired()` en EF + asterisco en el form.                 |
| `filter` / `filterable`              | Aparece en la card de **Filtros** del Index con un control acorde al tipo.              |
| `nofilter`                           | No aparece en filtros (default para no-strings).                                        |
| `hidden` / `hide` / `nolist`         | No aparece en la **tabla** del Index (sigue en el form de edición).                     |
| `list`                               | Aparece en la tabla (default).                                                          |
| `sort` / `sortable`                  | Columna ordenable click-to-sort en la tabla (default).                                  |
| `nosort`                             | Columna no ordenable.                                                                   |

Defaults: `required=false`, `filterable=true` sólo para strings, `showInList=true`, `sortable=true`.

### Tipos soportados

`string`, `int`, `long`, `short`, `byte`, `decimal`, `double`, `float`, `bool`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`.

---

## Qué genera

Para una entidad `Order` con Id `long` y `baseNamespace = "AspireApp"`:

**Archivos siempre creados (hasta 13):**

- `AspireApp.Domain.Entities/Order.cs`
- `AspireApp.Application.Models/App/Order.cs`
- `AspireApp.Application.Contracts/Order/IOrderService.cs`
- `AspireApp.Application.Implementations/Order/OrderService.cs`
- `AspireApp.Application.Mappers/OrderMapper.cs`
- `AspireApp.Application.Persistence/IOrderDA.cs`
- `AspireApp.DataAccess.Implementations/OrderDA.cs`
- `AspireApp.Api/Controllers/OrderController.cs`
- `AspireApp.Client.ApiClients/OrderApiClient.cs`
- `AspireApp.Client/Components/Pages/OrderIndex.razor(.cs)` *(si UI)*
- `AspireApp.Client/Components/Pages/OrderEdit.razor(.cs)` *(si UI)*

(El prefijo `AspireApp` es tu `baseNamespace`; las rutas salen de `paths`.)

**Extra en modo `server`:**

- `{BaseNamespace}.Application.Models/Paging.cs` — tipos de paginación **autocontenidos** (`PagedQuery` + `PagedResult<T>`). Se generan una sola vez (se saltean en las siguientes entidades) y **no requieren ningún proyecto externo ni `<ProjectReference>`**.
- `{BaseNamespace}.Application.Models/App/OrderFilter.cs` — DTO de filtros que hereda `PagedQuery` (`Page`/`PageSize`/`SortBy`/`SortDesc`).
- `I{Entity}DA`, `{Entity}DA`, `I{Entity}Service`, `{Entity}Service`, `{Entity}Controller` y `{Entity}ApiClient` ganan `GetPagedAsync(...)` con LINQ-to-EF (Skip/Take, Count, OrderBy por columna). El port devuelve `(IReadOnlyList<T> Items, int Total)` y el Service arma el `PagedResult<T>`.
- Endpoint `POST /api/{entity}/query` con el filtro en el body.

**Archivos parcheados (idempotente, no duplica):**

- `AppDbContext.cs` — `DbSet<Order>` + config EF para strings (MaxLength/IsRequired).
- 3× `DependencyInjection.cs` (DataAccess, Application, Mappers) + `Client/Program.cs` — registraciones.
- `NavMenu.razor` — `NavLink` a `/order` con el icono contextual.

### Pantallas Blazor (resumen)

- **Index**: header (icono + título + Refrescar/Nuevo), card de **filtros inteligentes** (`string`→contains, `bool`→Todos/Sí/No, numéricos→mín–máx, fechas→desde–hasta), tabla con cabecera **ordenable**, render por tipo en celdas (`bool`→Sí/No, `DateTime`→`yyyy-MM-dd`, numéricos monospace, `Email`→`mailto:`, `Phone`→`tel:`), empty/loading states, y modo **client** (filtra en browser) o **server** (POST `/query` + paginación).
- **Edit/Detail**: `EditForm` + `DataAnnotationsValidator` + `ValidationMessage` por campo, inputs por tipo (booleans como switch), Guardar/Cancelar.

### Iconos y acento

ChetoGen detecta un Bootstrap Icon por keyword (`Order → receipt`, `User → person-circle`, `Product → box-seam`…). Forzable con `--icon`. Acento por defecto `primary`; override con `--accent`. `danger` se reserva para acciones destructivas.

---

## Tokens de template

Texto plano con `{{TOKEN}}` (sustitución literal, sin AST/loops/ifs). Lo que varía por entidad se construye en C# y se inserta como token.

| Token                          | Descripción                                                              |
| ------------------------------ | ------------------------------------------------------------------------ |
| `{{BASE_NS}}`                  | El `baseNamespace` configurado (p. ej. `AspireApp`).                     |
| `{{ENTITY}}` / `{{entity}}`    | `Order` / `order`                                                        |
| `{{ENTITY_CAMEL}}`             | `order`                                                                  |
| `{{ENTITY_PLURAL}}` / `{{entity_plural}}` | `Orders` / `orders`                                          |
| `{{ID_TYPE}}` / `{{ID_ROUTE_CONSTRAINT}}` | `long` / `:long`, `:int`, `:guid` o vacío                   |
| `{{ENTITY_ICON}}` / `{{ACCENT}}` / `{{ACCENT_SUBTLE}}` | `receipt` / `primary` / `primary-subtle`         |
| `{{PAGE_SIZE}}`                | `25`                                                                     |
| `{{PROPS_ENTITY}}` / `{{PROPS_MODEL}}` | Propiedades (entity / + DataAnnotations)                        |
| `{{PROPS_DBCONFIG}}`           | `entity.Property(...).HasMaxLength(...).IsRequired();`                   |
| `{{PROPS_MAPPER_TO_MODEL}}` / `{{PROPS_MAPPER_TO_ENTITY}}` | Líneas del mapper                            |
| `{{PROPS_FORM_FIELDS}}`        | Inputs del Edit                                                          |
| `{{PROPS_TABLE_HEAD}}` / `{{PROPS_TABLE_BODY}}` | `<th>` sort / `<td>` por tipo                          |
| `{{PROPS_FILTER_FIELDS}}`      | Inputs de filtro inteligentes por tipo                                   |
| `{{PROPS_FILTER_STATE}}` / `{{PROPS_FILTER_LOGIC}}` / `{{PROPS_RESET_LOGIC}}` / `{{PROPS_SORT_CASES}}` | Client-mode |
| `{{PROPS_FILTER_DTO}}`         | Propiedades del `{Entity}Filter` (server-mode)                           |
| `{{AUTHORIZE_ATTR}}` / `{{AUTHORIZE_USING}}` | `[Authorize]` y su using, o vacío                          |
| `{{EVENT_BUS_USING}}` / `{{EVENT_BUS_CTOR_PARAM}}` / `{{EVENT_BUS_BASE_ARG}}` | Fragmentos del event bus    |
| `{{DISPLAY_NAME_EXPR}}`        | Expresión de "nombre visible" para headers                               |
| `{{PERSISTENCE_USINGS/BODY}}`, `{{DA_USINGS/BODY}}`, `{{CONTRACT_USINGS/BODY}}`, `{{SERVICE_USINGS/BODY}}`, `{{CONTROLLER_*}}`, `{{API_CLIENT_*}}` | Usings/cuerpos extra por capa (vacíos en client, completos en server) |
| `{{ARCH_*}}` (24 tokens)       | Clases base / interfaces / wrapper ROP — alimentados por la config [`architecture`](#arquitectura-parametrizable--architecture) tras expandir placeholders |

Los `*_USINGS`/`*_BODY` server-mode usan `{{BASE_NS}}` para sus `using` (p. ej. `using {BaseNamespace}.Application.Models;`). Un token que no esté en el mapa queda literal en el output, así que cableá el token antes de usarlo (ver `TemplateRenderer.BuildTokens`). Tu `chetogen.json` puede agregar/override tokens vía `"tokens"`, o remapear las clases base vía `"architecture"`.

---

## Paginación server-mode (autocontenida)

El server-mode **no depende de ningún proyecto de paginación externo**. La primera vez que generás una entidad en modo server, ChetoGen escribe `{BaseNamespace}.Application.Models/Paging.cs` con dos tipos chiquitos:

- `PagedQuery` — base de los filtros: `Page` / `PageSize` / `SortBy` / `SortDesc` + `ToSkipTake()`.
- `PagedResult<T>` — envelope serializable (`Items` / `Total` / `Page` / `PageSize` + `TotalPages` / `HasNext` / `HasPrevious`) que viaja de la API al cliente.

Viven en el proyecto de modelos (compartido por API y cliente), así que **no hace falta agregar ninguna `<ProjectReference>`**. El `{Entity}Filter` hereda `PagedQuery`; el port `I{Entity}DA` devuelve `(IReadOnlyList<TEntity> Items, int Total)` —Skip/Take de toda la vida, sin tipos cruzando capas— y el Service arma el `PagedResult<TModel>`.

Si tu solución ya tiene sus propios tipos de paginación, sobrescribí `Application.Paging.scriban` en tu carpeta `chetogen-templates` (y, si hace falta, los `using` vía templates propias), o salteá el paso `Application.Paging` con `excludeTemplates`.

---

## Idempotencia

Si la entidad ya existe, los archivos existentes se saltan y los compartidos no se duplican (los mutadores detectan su ancla antes de insertar). Podés correrlo varias veces sin riesgo.
