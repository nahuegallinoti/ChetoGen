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
# -> artifacts/nupkg/ChetoGen.0.1.0.nupkg
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
# 1. (Opcional) Crear un chetogen.json en la raíz de tu solución.
#    Infiere el baseNamespace del nombre del .slnx/.sln.
chetogen init                 # o:  chetogen init --with-templates

# 2. Generar una entidad.
chetogen generate Order
```

Sin `chetogen.json`, ChetoGen igual funciona: camina hacia arriba buscando un `*.slnx`/`*.sln`, **infiere el `baseNamespace` del nombre del archivo de solución** (p. ej. `Acme.slnx → "Acme"`) y usa el layout por defecto. El `chetogen.json` sólo existe para overridear eso.

### Modo interactivo

`chetogen generate` sin más argumentos te guía paso a paso:

- **Nombre y tipo de Id** (el nombre se normaliza a PascalCase, p. ej. `cliente` → `Cliente`).
- **Editor de propiedades** con tabla en vivo y menú **Agregar / Editar / Quitar / Listo**: si te equivocaste en algo (tipo, requerido, filtro…) **editás esa fila** en vez de reiniciar. Por campo elegís nombre, tipo y, en un multi-select, **Requerido / Filtrable / Mostrar en la tabla / Ordenable** (con defaults sensatos según el tipo).
- Luego: pantallas Blazor, NavLink, event bus y modo **client/server** (+ tamaño de página en server).

> En los pasos sí/no (Blazor, NavLink, event bus) y en el confirm final de generación, **`Esc` vuelve al paso anterior** (`Enter` = default · `S` = sí · `N` = no). `Esc` en el primer paso reabre el editor de propiedades.

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

  // ProjectReference que se agrega al .csproj de modelos en modo server.
  "pagingProjectReference": "..\\{BaseNamespace}.Domain.Paging\\{BaseNamespace}.Domain.Paging.csproj",

  // Pasos del pipeline a saltear, por su "friendly label" (p. ej. para no generar el Edit de Blazor).
  "excludeTemplates": ["Client.Edit.razor", "Client.Edit.razor.cs"],

  // Override de rutas individuales. Se expanden {BaseNamespace} y {Entity}; usá '/' como separador.
  "paths": {
    "DomainEntity": "{BaseNamespace}.Domain.Entities/{Entity}.cs"
  },

  // Tokens estáticos extra; ganan sobre los integrados (incluido BASE_NS).
  "tokens": {}
}
```

> Los comentarios `//` y las comas finales están permitidos: el loader parsea con `ReadCommentHandling = Skip` y `AllowTrailingCommas`.

### Claves de `paths`

`DomainEntity`, `ApplicationModel`, `ApplicationFilter`, `ApplicationContract`, `ApplicationService`, `ApplicationMapper`, `ApplicationPersistence`, `DataAccess`, `ApiController`, `ApiClient`, `BlazorIndexRazor`, `BlazorIndexCs`, `BlazorEditRazor`, `BlazorEditCs`, `ApplicationModelsCsproj`, `AppDbContext`, `DataAccessDI`, `ApplicationDI`, `MappersDI`, `ClientProgram`, `NavMenu`.

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

| Opción                  | Descripción                                                                       |
| ----------------------- | --------------------------------------------------------------------------------- |
| `--root <PATH>`         | Dónde escribir `chetogen.json`. Default: raíz de solución detectada.              |
| `--base-namespace <NS>` | Forzar el `baseNamespace`. Si se omite, se infiere del `.slnx`/`.sln`.            |
| `--with-templates`      | Copiar las templates integradas a `./chetogen-templates` para customizarlas.     |
| `--force`               | Sobrescribir un `chetogen.json` existente.                                        |

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

- `{BaseNamespace}.Application.Models/App/OrderFilter.cs` — DTO de filtros + paging + sort (hereda `PagedQuery`).
- `I{Entity}DA`, `{Entity}DA`, `I{Entity}Service`, `{Entity}Service`, `{Entity}Controller` y `{Entity}ApiClient` ganan `GetPagedAsync({Entity}Filter, ct)` con LINQ-to-EF (Skip/Take, Count, OrderBy por columna).
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

Los `*_USINGS`/`*_BODY` server-mode usan `{{BASE_NS}}` para sus `using` (p. ej. `using {BaseNamespace}.Domain.Paging;`). Un token que no esté en el mapa queda literal en el output, así que cableá el token antes de usarlo (ver `TemplateRenderer.BuildTokens`). Tu `chetogen.json` puede agregar/override tokens vía `"tokens"`.

---

## Convención por capas que sustenta server-mode

El set de templates por defecto asume la convención de Clean Architecture `{BaseNamespace}.<Capa>` con un proyecto de paging dedicado:

- `{BaseNamespace}.Domain.Paging` — `PagedResult<T>` (envelope `Items/Total/Page/PageSize`) y `PagedQuery` (`Page/PageSize/SortBy/SortDir` + `Normalize()`).
- `{BaseNamespace}.Application.Models` referencia `Domain.Paging` para que el `{Entity}Filter` herede `PagedQuery`. La primera generación server-mode agrega ese `<ProjectReference>` (idempotente). El resto de los proyectos lo ven transitivamente.
- El port `I{Entity}DA` (en `Application.Persistence`) sólo depende de Domain; el `{Entity}Filter` (DTO en `Application.Models`) lo traduce el Service a `List<Expression<Func<TEntity,bool>>>` antes de cruzar el port. La DA implementa con LINQ-to-EF.

Si tu solución usa otra convención, override `paths` y/o tus templates en `chetogen-templates`.

---

## Idempotencia

Si la entidad ya existe, los archivos existentes se saltan y los compartidos no se duplican (los mutadores detectan su ancla antes de insertar). Podés correrlo varias veces sin riesgo.
