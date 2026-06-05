# ChetoGen — Arquitectura (para devs)

> 🌐 [English](./ARCHITECTURE.en.md) · **Español**

Complemento de [`README.md`](./README.md). El README explica **cómo usar** la herramienta; este doc explica **cómo está cableado el código** para que puedas leerlo sin perderte.

---

## TL;DR

```
chetogen.json ─► GeneratorConfig ──────────────┐ (BaseNamespace, Paths, Architecture, Tokens, TemplatesDir)
CLI args ─► Settings ─► EntitySpec ─► GenerationPlan ─► [TemplateRenderer + IFileMutator] ─► disk
            (Spectre)   (record)      (Creations +       Templates/*.scriban + {{TOKEN}} map   IO
                                       Mutators)         (BASE_NS + ARCH_* + config.Tokens) + mutaciones
```

Un único comando (`generate`, más `init`) que toma el nombre de una entidad, te pregunta (o acepta) un puñado de propiedades, y escribe un slice CRUD completo a través de **Domain → Application → Infrastructure → Api → Client** más el par Blazor Index/Edit. Los archivos compartidos (registros de DI, NavMenu, AppDbContext) los parchea in-place con mutadores idempotentes. Nada está hardcodeado a una solución: namespaces, rutas y templates salen de `GeneratorConfig` (cargado por `ConfigLoader` desde `chetogen.json`, o defaults con el namespace inferido del `.slnx`).

---

## Layout del repo (sólo los archivos que importan)

```
ChetoGen/                            ← proyecto del tool (PackAsTool → comando `chetogen`)
├── Program.cs                       ← bootstrap Spectre.Console.Cli; CommandApp<GenerateCommand> + comando `init`
├── Configuration/                   ← la capa que hace al generador agnóstico
│   ├── GeneratorConfig.cs           ← record con BaseNamespace, Paths, Architecture, RootMarkers, TemplatesDirectory, Tokens… + DefaultPaths/DefaultArchitecture + Expand()
│   └── ConfigLoader.cs              ← descubre/parsea chetogen.json, mergea sobre defaults, infiere BaseNamespace del .slnx
├── Commands/
│   ├── GenerateCommand.cs           ← toda la UX: prompts, banner, panel de preview, render del plan, ejecución
│   └── InitCommand.cs               ← escribe un chetogen.json inicial (y opcionalmente copia las templates)
├── Generator/                       ← lógica pura, sin IO de consola
│   ├── EntitySpec.cs                ← el record inmutable que describe qué generar
│   ├── PropertySpec.cs              ← un campo de la entidad (Name, Type, Required, Filterable, ShowInList, Sortable)
│   ├── PathResolver.cs              ← resuelve cada path destino desde config.Paths (expande {BaseNamespace}/{Entity})
│   ├── GenerationPlan.cs            ← Build(entity, paths, config) → lista de (Creations, Mutators)
│   ├── TemplateRenderer.cs          ← lee Templates/*.scriban (override-able), reemplaza {{TOKEN}} desde un mapa de tokens
│   ├── IconPicker.cs                ← mapea nombre de entidad → clase de Bootstrap Icons (determinista)
│   ├── AccentPicker.cs              ← mapea nombre de entidad → acento Bootstrap (actualmente siempre "primary")
│   └── Mutators/
│       ├── IFileMutator.cs          ← interface: TargetPath + Mutate(source, entity) → MutationResult
│       ├── DiRegistrationMutator.cs ← inserta líneas using + AddScoped/AddSingleton después de un marcador
│       ├── DbContextMutator.cs      ← inserta DbSet<> + config modelBuilder.Entity<>() en AppDbContext
│       └── NavMenuMutator.cs        ← inserta un bloque NavLink dentro de <Authorized> o antes de </nav>
└── Templates/                       ← archivos *.scriban de texto; **NO** es Scriban real, sólo sustitución {{TOKEN}}
    ├── Domain.Entity.scriban
    ├── Application.Model.scriban
    ├── Application.Filter.scriban   ← sólo server-mode
    ├── Application.Paging.scriban   ← sólo server-mode (PagedQuery + PagedResult autocontenidos)
    ├── Application.Contract.scriban
    ├── Application.Service.scriban
    ├── Application.Persistence.scriban
    ├── Application.Mapper.scriban
    ├── DataAccess.scriban
    ├── Api.Controller.scriban
    ├── Client.ApiClient.scriban
    ├── Client.Index.razor.scriban       ← index client-mode
    ├── Client.Index.razor.cs.scriban
    ├── Client.IndexServer.razor.scriban ← index server-mode
    ├── Client.IndexServer.razor.cs.scriban
    ├── Client.Edit.razor.scriban
    └── Client.Edit.razor.cs.scriban
```

Los templates se copian a `bin/.../Templates/` al compilar y viajan dentro del `.nupkg` (`tools/<tfm>/any/Templates/`). `TemplateRenderer` los lee de `AppContext.BaseDirectory/Templates` en runtime; si el `chetogen.json` define `templatesDirectory`, busca **ahí primero** cada template por nombre y cae al set integrado si no está. Editar las templates integradas requiere rebuild; las del consumidor no.

---

## Pipeline, paso a paso

### 1. Bootstrap — `Program.cs`
Setea la consola en UTF-8, construye un Spectre `CommandApp<GenerateCommand>` con un comando `init` adicional, y lo corre. Eso es todo.

### 2. Cargar la config y descubrir la raíz — `ConfigLoader.Load(config?, root?)`
Devuelve un `LoadedConfig (GeneratorConfig, SolutionRoot, ConfigFilePath?)`:

- **Config file**: `--config` explícito, o camina hacia arriba buscando `chetogen.json`. Si lo encuentra, parsea JSON (con comentarios y trailing commas) a un DTO y lo **mergea sobre `GeneratorConfig.CreateDefault(...)`**.
- **Root**: `--root` explícito > carpeta del `chetogen.json` > caminata hacia arriba por `config.RootMarkers` (`*.slnx`/`*.sln`).
- **BaseNamespace**: valor del config > **inferido del nombre del archivo de solución** (`Acme.slnx → "Acme"`) > nombre de la carpeta raíz.

Sin `chetogen.json` el tool funciona igual (todo default + namespace inferido). Después `new PathResolver(root, config)` es el dueño único de los paths — cada destino sale de `config.Paths` expandiendo `{BaseNamespace}`/`{Entity}`; no hay concatenación de paths en ningún otro lado.

### 3. Construir el spec — `ResolveEntityBase` + `ResolveOptions`
O totalmente interactivo (Spectre prompts) o totalmente manejado por flags de CLI. La salida es un único record `EntitySpec` con todo decidido. Resolvers clave:

- `ResolveFilterMode` — `client` (default) o `server`. Define si el Index filtra in-browser o habla con un endpoint paginado.
- `ResolvePageSize` — sólo server-mode; default 25.
- Las propiedades vienen de `PropertySpec.Parse` (forma `--prop`) o, en modo interactivo, de `CollectPropertiesInteractive` — un editor con tabla en vivo y menú **Add / Edit / Remove / Done**. Cada campo se arma en `PromptForProperty` (nombre + tipo + multi-select de flags Required/Filterable/ShowInList/Sortable), así que un error se corrige editando la fila, sin reiniciar. Los nombres pasan por `Naming.IsValidIdentifier` y se normalizan con `Naming.Capitalize`.

### 4. Construir el plan — `GenerationPlan.Build(entity, paths, config)`
Función pura. Devuelve un record con dos listas (filtrando los pasos listados en `config.ExcludeTemplates`):

- **Creations**: `(TargetPath, TemplateName, FriendlyLabel)`. Todos los archivos que el generator va a escribir (saltado si ya existe).
- **Mutators**: lista de `IFileMutator`. Todos los archivos compartidos que necesitan un parche idempotente.

Acá viven los **templates condicionales** (server-mode agrega `Application.Filter.scriban` e intercambia los templates del Index) — `GenerationPlan.Build` es la única fuente de verdad para "qué archivos para esta config".

### 5. Render del preview — `RenderEntitySummary` + `RenderPlan`
Todo Spectre. Dos paneles (resumen de la entidad + árbol del plan agrupado por capa). Si no es `--yes` y no es `--dry-run`, pide confirmación.

### 6. Ejecución — `ExecutePlanAsync`
Dos pasadas:

- **Creations**: el mapa de tokens se construye **una sola vez** con `TemplateRenderer.BuildTokens(entity, config)` y se reusa para todos los archivos. Para cada uno, `renderer.Render(templateName, tokens)` → si el archivo existe, saltea. Si no, crea directorios y escribe. En `--dry-run`, imprime el path pero no escribe.
- **Mutators**: para cada uno, lee source → `mutator.Mutate(source, entity)` → si `Changed`, escribe. Si no encuentra su ancla, surface un error y continúa con el resto (cuenta en `totals.Failed`).

### 7. Summary — `RenderResult`
Conteo de creados / mutados / saltados / fallidos en un panel, más un pie de "próximos pasos" (build, migración, run).

---

## Tipos core (cheat sheet)

| Tipo | Rol |
| --- | --- |
| `EntitySpec` | Record inmutable. Todo lo que el generator necesita en un solo lugar: nombre, tipo de id, propiedades, flags (Blazor/Nav/Auth/EventBus), `FilterMode`, `PageSize`, overrides de icono/acento. Helpers derivados: `Lower`, `Camel`, `Plural`, `IsServerFiltering`, `FilterableProperties`, `ListProperties`, `SortableProperties`. |
| `PropertySpec` | Un campo. `Name`, `Type` (normalizado), `Required`, `Filterable`, `ShowInList`, `Sortable`. Conoce su bucket de tipo vía `IsString` / `IsBool` / `IsNumeric` / `IsDateTime` / `IsGuid` y su componente Razor (`InputText`, `InputNumber`, etc.). `Parse(raw)` maneja la forma `--prop "Name:type:flag:flag"` de la CLI. |
| `GeneratorConfig` | Record con todo lo agnóstico: `BaseNamespace`, `Paths` (logical-key → ruta con `{BaseNamespace}`/`{Entity}`), `Architecture` (clave → costura de arquitectura, alimenta los tokens `ARCH_*`), `RootMarkers`, `TemplatesDirectory`, `AppHostProject`, `ExcludeTemplates`, `ExcludeMutators`, `Tokens`. `DefaultPaths`/`DefaultArchitecture` reproducen el layout y la Clean Architecture integrados; `Expand(t, entity)` resuelve los placeholders. |
| `ConfigLoader` | `Load(config?, root?, startDir?) → LoadedConfig`. Descubre y parsea `chetogen.json`, lo mergea sobre los defaults, resuelve la raíz e infiere `BaseNamespace`. |
| `PathResolver` | Dueño único de "¿dónde va el archivo X para la entidad Y?". `Resolve(key, entity)` expande `config.Paths[key]` y lo combina con la raíz — único lugar con math de paths. |
| `GenerationPlan` | Devuelto por `Build(entity, paths, config)`. Tiene `Creations` + `Mutators`. Los condicionales (server-mode, Blazor/NavMenu on/off) y el filtro por `config.ExcludeTemplates` viven acá. |
| `TemplateRenderer` | `new(config)` fija el root de templates (built-in + override). `BuildTokens(entity, config)` arma el mapa `{{KEY}} → value` (incluye `BASE_NS`, los `ARCH_*` vía `ExpandArchitecture`, y mergea `config.Tokens`). `Render(file, tokens)` busca el template (override-dir primero), corre `StringBuilder.Replace` y **normaliza a LF**. |
| `IFileMutator` / `MutationResult` | Contrato chiquito para parches de archivos compartidos. Mutate es **puro** (string entra, string sale) — la escritura real pasa en `GenerateCommand`. |

---

## Cómo funcionan los templates (NO es Scriban)

Los archivos tienen extensión `.scriban` por convención pero el renderer es muy simple — sin AST, sin expresiones, sin loops:

```csharp
foreach (var (key, value) in tokens)
    sb.Replace("{{" + key + "}}", value);
```

Ese es el engine entero. **Los tokens son strings `{{KEY}}` planos**, no hay `{{ for x in ... }}` ni `{{ if foo }}`. Todo lo que varía por entidad se construye de antemano en un string por un método `BuildXxx(entity)` en `TemplateRenderer`, y después se inserta como token.

### El truco de las dos fases

La mayoría de los archivos generados tienen:
1. **Un esqueleto de template** — los bits que nunca cambian de forma (namespace, header de clase, base class).
2. **Tokens body-builder** — `{{PROPS_ENTITY}}`, `{{PERSISTENCE_BODY}}`, `{{DA_BODY}}`, `{{SERVICE_BODY}}`, etc. — strings multi-línea construidos por métodos C#.

Por ejemplo `Application.Persistence.scriban` es simplemente:
```
{{PERSISTENCE_USINGS}}using {{BASE_NS}}.Application.Persistence.Base;
using {{BASE_NS}}.Domain.Entities;

namespace {{BASE_NS}}.Application.Persistence;

public interface I{{ENTITY}}DA : IBaseDA<{{ENTITY}}, {{ID_TYPE}}>
{
{{PERSISTENCE_BODY}}
}
```

…y `BuildPersistenceBody(entity)` devuelve `""` (client mode) o una signature de método `GetPagedAsync(...)` (server mode). Mismo template, dos outputs.

### Cuándo agregar un template nuevo vs un token nuevo

- **Archivo nuevo**: sólo si el archivo es estructuralmente distinto y se genera condicionalmente. Tenemos dos templates de Index razor (`Client.Index.razor` vs `Client.IndexServer.razor`) porque el markup diverge demasiado para que un token lo exprese limpio.
- **Token nuevo**: cualquier cosa que sea un bloque de texto dentro de un archivo estable por lo demás. Fuertemente preferido — mantiene bajo el conteo de templates.

### Cheat sheet del mapa de tokens

Definido en `TemplateRenderer.BuildTokens(entity, config)`. Convención de naming:

- `BASE_NS` — el `config.BaseNamespace`; **todos los namespaces de los templates** lo usan (`{{BASE_NS}}.Domain.Entities`, etc.).
- `ENTITY`, `ID_TYPE`, etc. — tokens escalares de identidad.
- `PROPS_*` — listas por-propiedad (props de entity, props de model, head de tabla, filter fields, etc.).
- `*_USINGS` — directivas `using` extras para ese archivo (server-mode agrega `using {{BASE_NS}}.Application.Models;`, construido desde `config.BaseNamespace`).
- `*_BODY` — el contenido principal del body del archivo (vacío `;\n` para client mode, implementación completa para server mode).
- `AUTHORIZE_*`, `EVENT_BUS_*` — fragmentos condicionales toggleados por flags de entidad.
- `ARCH_*` — las **costuras de arquitectura**: clases base / interfaces / wrapper ROP sobre los que se apoya el código (`ARCH_ENTITY_BASE`, `ARCH_SERVICE_BASE`, `ARCH_CONTROLLER_BASE`, `ARCH_RESULT`, …). A diferencia del resto, su valor **no se hardcodea en C#**: sale de `config.Architecture[clave]` (overridable por `chetogen.json`) pasando por `ExpandArchitecture`, que resuelve los placeholders `{BaseNamespace}`/`{Entity}`/`{EntityCamel}`/`{Id}`/`{EventBusCtorParam}`/`{EventBusBaseArg}`. Los defaults (`GeneratorConfig.DefaultArchitecture`) reproducen la Clean Architecture de referencia; esto es lo que permite apuntar la **misma plantilla** a cualquier arquitectura sin editar templates.

Si un token no aparece en el mapa, `{{ESO}}` queda **literalmente en el output**. Siempre cableá el token en `BuildTokens` antes de usarlo en un template. El `chetogen.json` del consumidor puede agregar/override tokens vía `"tokens"` (se mergean al final, ganan), o remapear las clases base vía `"architecture"` (se mergea sobre `DefaultArchitecture`).

> **Saltos de línea**: `Render` normaliza todo el output a LF. Las líneas vienen de tres fuentes descoordinadas (los `.scriban` según el checkout, los literales C# con `\n`, y `Environment.NewLine` vía `AppendLine`); colapsar a LF al final hace el output determinista y limpio sin importar el SO.

---

## Cómo funcionan los mutadores

`IFileMutator` existe porque algunos archivos están **compartidos entre entidades** — contenedores de DI, el DbContext, NavMenu — y una escritura fresca clobberaría las entidades existentes. Así que en vez de templatear, los leemos, los parcheamos, los escribimos.

Tres implementaciones cubren todo:

### `DiRegistrationMutator`
Genérico: toma una lista de `usingLines`, una `registrationLine` (e.g. `services.AddScoped<IOrderService, OrderService>();`), y un `markerForLastRegistration` (un substring como `"services.AddScoped<I"`). Idempotente — si la línea de registración ya existe, no-op.

Dónde se cablea: `GenerationPlan.Build` construye **uno por archivo de DI** (DataAccessDI, ApplicationDI, MappersDI, ClientProgram).

### `DbContextMutator`
- Inserta `public DbSet<X> Xs => Set<X>();` después del último DbSet existente.
- Si la entidad tiene propiedades string, también inserta un bloque `modelBuilder.Entity<X>(entity => { ... });` antes de la llave de cierre de `OnModelCreating`. Usa un contador de profundidad de llaves para encontrar la llave de cierre que matchea.

### `NavMenuMutator`
Busca `href="entityname"` para detectar entradas existentes (idempotente). Si la entidad tiene `RequireAuth`, inserta el bloque `<div class="nav-item">…<NavLink>…` antes del `</Authorized>` del `AuthorizeView` externo (detectado por el patrón `</Authorized>\s*<NotAuthorized>`); si no, lo inserta antes de `</nav>`. La indentación se detecta automáticamente desde la línea del ancla.

Los tres son **anchor-based**: fallan ruidosos si su ancla falta, en vez de droppear el cambio silenciosamente.

> Server-mode **no** parchea ningún `.csproj`: los tipos de paginación (`PagedQuery` + `PagedResult<T>`) se generan autocontenidos en `Application.Models/Paging.cs` (ver `Application.Paging.scriban`), así que no hace falta agregar referencias de proyecto.

---

## Cómo encajan los archivos por capa (una entidad, server mode)

> Los prefijos `{BaseNamespace}` salen de `config.Paths` (acá con `BaseNamespace = AspireApp`).

```
Domain.Entity      → {BaseNamespace}.Domain.Entities/{Entity}.cs
Application.Model  → {BaseNamespace}.Application.Models/App/{Entity}.cs
Application.Filter → {BaseNamespace}.Application.Models/App/{Entity}Filter.cs   (sólo server)
Application.Paging → {BaseNamespace}.Application.Models/Paging.cs               (sólo server, autocontenido)
Application.Contract / Service        ← lógica de negocio, construye predicados desde Filter
Application.Persistence (I{Entity}DA) ← port, devuelve (IReadOnlyList<TEntity> Items, int Total); recibe predicados + skip/take
Application.Mapper                    ← {Entity}Mapper, ToModel / ToEntity / ToModelList
DataAccess ({Entity}DA)               ← adapter, implementación LINQ-to-EF
Api.Controller                        ← surface REST, POST /api/{entity}/query para server mode
Client.ApiClient                      ← wrapper HTTP tipado, GetPagedAsync(filter, ct) extra en server mode
Client.Index.razor + .razor.cs        ← página Blazor (variante client o server)
Client.Edit.razor  + .razor.cs        ← página Blazor (template compartido único)
```

Para client mode, droppeás `Application.Filter` + `Application.Paging` e intercambiás la variante de Index.

---

## Recetas

### Quiero agregar un nuevo tipo de propiedad
1. `PropertySpec.NormalizeType` — mapeá el nombre user-facing al tipo C# canónico.
2. Getters de `PropertySpec` — agregá al que tenga sentido entre `IsString` / `IsBool` / `IsNumeric` / `IsDateTime` / `IsGuid` (o agregá un bucket nuevo).
3. `PropertySpec.RazorInputComponent` — el `InputXxx` de Razor que se usa en `Client.Edit.razor`.
4. `TemplateRenderer.BuildFilterFields` / `BuildFilterDtoProps` / `AppendServicePredicate` — emití la forma del filtro inteligente (range, dropdown, etc.) para el nuevo bucket.

### Quiero agregar un nuevo archivo generado
1. `Configuration/GeneratorConfig.cs` — agregá una constante en `PathKeys` y su ruta default en `DefaultPaths` (con `{BaseNamespace}`/`{Entity}`).
2. `PathResolver` — agregá un helper `Resolve(PathKeys.MiKey, entity)` (opcional, por legibilidad).
3. `Templates/` — droppeá el nuevo archivo `.scriban` (usá `{{BASE_NS}}` para namespaces).
4. `GenerationPlan.Build` — agregá un `new FileCreation(...)` (envolvélo en `if (entity.IsServerFiltering)` etc. si es condicional).
5. Corré con `--dry-run` para confirmar que aparezca en el árbol del plan.

### Quiero agregar un token nuevo
1. `TemplateRenderer.BuildTokens(entity, config)` — agregá `["MI_TOKEN"] = BuildMiToken(entity)` (o usá `config.BaseNamespace` si emitís namespaces).
2. Implementá `BuildMiToken(EntitySpec entity)` — devolvé un `string`. Usá `StringBuilder` si es multi-línea.
3. Agregá `{{MI_TOKEN}}` al template que lo necesite. Rebuild.

### Quiero apuntar a otra solución / cambiar el layout o las templates
1. **Sin tocar código**: agregá un `chetogen.json` (`chetogen init`) con `baseNamespace`, override de `paths`, `architecture`, `excludeTemplates`/`excludeMutators` y/o `tokens`.
2. **Templates propias**: `chetogen init --with-templates` copia el set a `chetogen-templates/`; editá sólo las que quieras (el resto cae al set integrado) y apuntá `templatesDirectory`.
3. Defaults del layout integrado: `GeneratorConfig.DefaultPaths`. La inferencia de `baseNamespace` y la carga viven en `ConfigLoader`.

### Quiero apuntar a OTRA arquitectura (otras clases base / wrapper ROP)
1. **Sin tocar código ni templates**: en `chetogen.json`, override las claves de `"architecture"` que difieran (p. ej. `ResultType`/`ResultIsSuccess` si tu wrapper ROP se llama distinto, o `EntityBase`/`EntityUsings` para otra clase base). Se mergea sobre `DefaultArchitecture`; el resto cae al default. `chetogen init` → *"mapear a mis propias clases base"* vuelca el bloque completo.
2. Los valores admiten `{BaseNamespace}`/`{Entity}`/`{EntityCamel}`/`{Id}`/`{EventBusCtorParam}`/`{EventBusBaseArg}` (ver `ExpandArchitecture`).
3. **Si necesitás otro token de arquitectura**: agregá la clave en `GeneratorConfig.DefaultArchitecture`, el entry `["ARCH_…"] = Arch("…")` en `BuildTokens`, y usá `{{ARCH_…}}` en el template. Recordá: estos tokens **remapean** clases base, no cambian el cuerpo generado (eso es un template).

### Quiero agregar un patch nuevo de archivo compartido
1. Implementá `IFileMutator` (los existentes tienen 40–115 LOC; copiá el más parecido).
2. Anclá en **un substring estable** en el archivo destino. Nunca uses números de línea.
3. Agregá un check de idempotencia (early-return con `Changed: false` si el patch ya está).
4. Cableálo en la lista `mutators` de `GenerationPlan.Build`.

### Quiero cambiar la surface de la CLI
- Todas las flags viven en `GenerateCommand.Settings` como propiedades `[CommandOption]`. Resolvélas dentro de los resolvers dedicados (`ResolveEntityBase`/`ResolveOptions`, con un fallback interactivo si es una elección significativa).

### Quiero ver qué se generaría sin escribir
- `--dry-run`. Pega en cada code path excepto el `File.WriteAllTextAsync` y las escrituras de los mutadores.

---

## Convenciones

- **Lógica pura en `Generator/`, UX de consola en `Commands/`.** No imprimas en `AnsiConsole` desde `TemplateRenderer` o `EntitySpec`. El renderer/spec deben ser unit-testables headless.
- **`PathResolver` es dueño de los paths.** Sin matemática de paths con concat de strings en ningún otro lado.
- **Los mutadores son idempotentes.** Re-correr el generator para la misma entidad debe ser un no-op en archivos compartidos.
- **Los templates son tontos.** Toda la lógica en C#; los templates son esqueletos. Si te encontrás queriendo `{{ if }}` en un template, construí la variante en un método body-builder en su lugar y emitílo como token.
- **Sin estado parcial en fallos.** Un mutador que no puede encontrar su ancla falla su único archivo y se reporta en `totals.Failed`; el resto del plan sigue corriendo.
