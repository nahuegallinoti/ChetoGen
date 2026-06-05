# ChetoGen

> 🌐 [Español](./README.md) · **English**

**A system-agnostic, layered CRUD slice generator for .NET.** Install it as a `dotnet tool`; it generates every layer file (Domain → Application → Infrastructure → API → Client) and patches the shared files (DI, `AppDbContext`, `NavMenu`, `Program.cs`) in a single run. The Blazor pages use **Bootstrap 5 + Bootstrap Icons** with type-aware smart filters, sortable columns, and optional server-side pagination.

Nothing is hard-wired to a specific solution: the **base namespace, paths, templates, and mutators** all come from configuration. It ships a default template set (the `{BaseNamespace}.<Layer>` convention) that **any consumer can override** by pointing at their own templates folder.

> 🛠 **Hacking on ChetoGen itself?** Start with [`ARCHITECTURE.md`](./ARCHITECTURE.md) — pipeline, configuration layer, layout, conventions, and recipes.

---

## Install

ChetoGen ships as a **.NET tool**. Build the `.nupkg` from this repo:

```pwsh
dotnet pack ChetoGen/ChetoGen.csproj -c Release
# -> artifacts/nupkg/ChetoGen.<version>.nupkg
```

Install it from that local feed (global, local, or portable):

```pwsh
# Global
dotnet tool install --global ChetoGen --add-source ./artifacts/nupkg

# Local (in the solution you'll generate into; uses a .config/dotnet-tools.json manifest)
dotnet new tool-manifest
dotnet tool install ChetoGen --add-source <path-to-nupkg>

# Portable (to a folder, without touching the repo manifest)
dotnet tool install ChetoGen --tool-path C:\tools\chetogen --add-source <path-to-nupkg>
```

The command is **`chetogen`** (global/portable tool) or **`dotnet chetogen`** (local tool).

---

## Quickstart

```pwsh
# 1. (Optional) Create a chetogen.json with the interactive wizard.
#    Asks for namespace, layers, shared files, layout, and templates.
chetogen init                 # wizard · or:  chetogen init --yes (defaults, no prompts)

# 2. Generate an entity.
chetogen generate Order
```

Without a `chetogen.json`, ChetoGen still works: it walks up looking for a `*.slnx`/`*.sln`, **infers `baseNamespace` from the solution file name** (e.g. `Acme.slnx → "Acme"`), and uses the default layout. `chetogen.json` only exists to override that.

### Interactive mode

`chetogen generate` with no extra args walks you through it: name + Id type, a **live property editor** (Add / Edit / Remove / Done — fix a wrong row instead of restarting; per field pick name, type, and a multi-select of **Required / Filterable / Show in table / Sortable**), then Blazor pages, NavLink, event bus, and **client/server** mode (+ page size for server).

### Non-interactive mode

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

## Configuration — `chetogen.json`

Place it at your solution root (ChetoGen discovers it by walking up). **Every key is optional** and merges over the defaults. `chetogen init` writes a commented one.

```jsonc
{
  // Namespace / project prefix of YOUR solution. Replaces {BaseNamespace} everywhere.
  "baseNamespace": "AspireApp",

  // How the solution root is located when walking up.
  "rootMarkers": ["*.slnx", "*.sln"],

  // Folder (relative to this file) with templates that override/extend the built-in set.
  "templatesDirectory": "chetogen-templates",

  // Project shown in the "next steps" hint.
  "appHostProject": "{BaseNamespace}.AppHost",

  // Pipeline steps to skip, by friendly label (e.g. drop the Blazor edit page).
  "excludeTemplates": ["Client.Edit.razor", "Client.Edit.razor.cs"],

  // Override individual output paths. {BaseNamespace} and {Entity} are expanded; use '/' separators.
  "paths": { "DomainEntity": "{BaseNamespace}.Domain.Entities/{Entity}.cs" },

  // Base classes / interfaces / result wrapper the generated code sits on.
  // Override only the keys that differ from your architecture; the rest fall back to defaults.
  "architecture": { "ResultType": "Result", "ResultIsSuccess": "Success" },

  // Extra static tokens; these win over the built-ins (including BASE_NS).
  "tokens": {}
}
```

> `//` comments and trailing commas are allowed (the loader parses with `ReadCommentHandling = Skip` and `AllowTrailingCommas`).

**`paths` keys:** `DomainEntity`, `ApplicationModel`, `ApplicationFilter`, `ApplicationPaging`, `ApplicationContract`, `ApplicationService`, `ApplicationMapper`, `ApplicationPersistence`, `DataAccess`, `ApiController`, `ApiClient`, `BlazorIndexRazor`, `BlazorIndexCs`, `BlazorEditRazor`, `BlazorEditCs`, `AppDbContext`, `DataAccessDI`, `ApplicationDI`, `MappersDI`, `ClientProgram`, `NavMenu`.

### Parameterizable architecture — `architecture`

The generated code sits on a base architecture: entities on `BaseEntity`, services on `BaseService`, controllers on `BaseController`, repos on `BaseDA`, a typed HTTP client on `BaseApiClient`, and a result wrapper (ROP) `Result<T>`. **Each of those seams is a configurable token** under `"architecture"`, so a single template set targets **any** architecture without editing templates. Values support the placeholders `{BaseNamespace}`, `{Entity}`, `{EntityCamel}`, `{Id}`, `{EventBusCtorParam}` and `{EventBusBaseArg}`.

Override **only** the keys that differ in your solution; the rest fall back to the default. Typical examples:

```jsonc
"architecture": {
  // Your result wrapper is named differently (e.g. an Either monad):
  "ResultType": "Either",
  "ResultIsSuccess": "IsRight",
  "ResultValue": "Right",
  "ResultErrors": "Error",

  // Your entities live in another namespace and inherit a different base:
  "EntityUsings": "using MyCompany.Core.Domain;\n",
  "EntityBase": " : Entity<{Id}>"
}
```

**Available keys** (24): `EntityUsings`, `EntityBase`, `ModelBase`, `MapperBase`, `ServiceContractUsings`, `ServiceContractBase`, `PersistenceContractUsings`, `PersistenceContractBase`, `ServiceImplUsings`, `CacheUsing`, `ServiceCtor`, `ServiceBase`, `DataAccessUsing`, `DataAccessCtor`, `DataAccessBase`, `ControllerCtor`, `ControllerBase`, `ApiClientUsing`, `ApiClientCtor`, `ApiClientBase`, `ResultType`, `ResultIsSuccess`, `ResultValue`, `ResultErrors`. Their full defaults live in [`chetogen.example.json`](./chetogen.example.json), and `chetogen init` (pick *"map to my own base classes"*) dumps the whole block ready to edit.

> The generated body **assumes your base class provides the CRUD** (`GetAll`/`Get`/`Create`/…). These tokens **remap** to your equivalent base classes — they don't remove them. If you don't use base classes and want full bodies, copy the templates (`--with-templates`) and edit them.

**Custom templates:** run `chetogen init --with-templates` to copy the built-in set to `./chetogen-templates`, or create the folder yourself. ChetoGen looks up **each template by name in `templatesDirectory` first**, falling back to the built-in. Override only what you want. Templates are plain text with `{{TOKEN}}` placeholders (not real Scriban — see the token table).

---

## CLI options

### `chetogen generate`

| Option              | Description                                                                            |
| ------------------- | -------------------------------------------------------------------------------------- |
| `ENTITY_NAME`       | Singular PascalCase entity name (e.g. `Order`).                                        |
| `--id <ID_TYPE>`    | Id type: `long`, `int` or `Guid`. Default `long`.                                     |
| `-p, --prop <PROP>` | Property as `Name:type[:flag1[:flag2...]]`. Repeatable. Flags below.                   |
| `--icon <ICON>`     | Override the Bootstrap Icon (no `bi-` prefix). Auto-detected by name.                  |
| `--accent <ACCENT>` | Bootstrap accent: `primary`, `success`, `info`, `warning`, `danger`, `secondary`.     |
| `--filter-mode <M>` | `client` (default) or `server` (Filter DTO + POST `/query` + pagination).             |
| `--page-size <N>`   | Default page size (server mode). Default 25.                                           |
| `--no-ui`           | Don't generate the Blazor pages.                                                       |
| `--no-nav`          | Don't add the `NavLink`.                                                               |
| `--no-auth`         | Don't decorate the controller with `[Authorize]`.                                      |
| `--event-bus`       | Inject `IMessageBus` and publish an event on create.                                   |
| `--dry-run`         | Show the plan without writing anything.                                                |
| `-y, --yes`         | Run without confirmation.                                                              |
| `--root <PATH>`     | Explicit solution root (auto-detected by default).                                     |
| `--config <PATH>`   | Explicit `chetogen.json` path (auto-discovered by default).                            |

### `chetogen init`

By default it opens an **interactive wizard** that builds `chetogen.json` for you: it asks for the namespace, which **layers** to generate, which **shared files** to patch, whether your **layout** differs from the default, whether you use ChetoGen's **base classes** or want to map them to your own (dumps the full `architecture` block), and whether to **copy the templates** to rewrite the code body. It ends with a preview before writing. `--yes` (or a redirected stdin, e.g. CI) skips the wizard and writes a defaults file.

| Option                  | Description                                                                  |
| ----------------------- | ---------------------------------------------------------------------------- |
| `--root <PATH>`         | Where to write `chetogen.json`. Default: detected solution root.            |
| `--base-namespace <NS>` | Force `baseNamespace`. Inferred from the `.slnx`/`.sln` when omitted.       |
| `--with-templates`      | Also copy the built-in templates to `./chetogen-templates` to customize.    |
| `--force`               | Overwrite an existing `chetogen.json`.                                       |
| `-y, --yes`             | Skip the wizard and write a defaults `chetogen.json` (CI mode).             |

### Property flags (`--prop "Name:type:flag1:flag2..."`)

`required`, `filter`/`filterable`, `nofilter`, `hidden`/`hide`/`nolist`, `list`, `sort`/`sortable`, `nosort`.
Defaults: `required=false`, `filterable=true` for strings only, `showInList=true`, `sortable=true`.

### Supported types

`string`, `int`, `long`, `short`, `byte`, `decimal`, `double`, `float`, `bool`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`.

---

## What it generates

For an `Order` entity (Id `long`, `baseNamespace = "AspireApp"`):

**Always (up to 13):** `Order.cs` entity, application `Order.cs` model, `IOrderService`/`OrderService`, `OrderMapper`, `IOrderDA`/`OrderDA`, `OrderController`, `OrderApiClient`, and (if UI) `OrderIndex.razor(.cs)` + `OrderEdit.razor(.cs)` — all under `{BaseNamespace}.*` paths from `paths`.

**Extra in `server` mode:** `Paging.cs` — **self-contained** paging types (`PagedQuery` + `PagedResult<T>`), written once and needing **no external project or `<ProjectReference>`**; `OrderFilter.cs` (inherits `PagedQuery`); the DA/service/contract/controller/api-client gain `GetPagedAsync(...)` (LINQ-to-EF Skip/Take/Count/OrderBy — the port returns `(IReadOnlyList<T> Items, int Total)` and the service builds the `PagedResult<T>`); endpoint `POST /api/{entity}/query`.

**Patched (idempotent):** `AppDbContext` (`DbSet<>` + EF string config), 3× `DependencyInjection.cs` + `Client/Program.cs` (registrations), `NavMenu.razor` (contextual `NavLink`).

**Blazor pages:** Index with smart per-type filters (`string`→contains, `bool`→All/Yes/No, numeric→min–max, date→from–to), sortable headers, type-aware cells (`bool`→Yes/No, `DateTime`→`yyyy-MM-dd`, `Email`→`mailto:`, `Phone`→`tel:`), empty/loading states, and client (browser) or server (POST `/query` + pagination) mode. Edit page with `EditForm` + validation, type-aware inputs (bool as switch), Save/Cancel.

---

## Template tokens

Plain text with `{{TOKEN}}` (literal substitution — no AST/loops/ifs). Anything that varies per entity is built in C# and inserted as a token.

Key tokens: `{{BASE_NS}}` (your `baseNamespace`), `{{ENTITY}}`/`{{entity}}`/`{{ENTITY_CAMEL}}`/`{{ENTITY_PLURAL}}`/`{{entity_plural}}`, `{{ID_TYPE}}`/`{{ID_ROUTE_CONSTRAINT}}`, `{{ENTITY_ICON}}`/`{{ACCENT}}`/`{{ACCENT_SUBTLE}}`, `{{PAGE_SIZE}}`, the `{{PROPS_*}}` family (entity/model props, table head/body, form fields, filter fields/state/logic/reset/sort cases, filter DTO), `{{AUTHORIZE_*}}`, `{{EVENT_BUS_*}}`, `{{DISPLAY_NAME_EXPR}}`, per-layer `{{*_USINGS}}`/`{{*_BODY}}` (empty in client, full in server — server usings are built from `config.BaseNamespace`), and the 24 `{{ARCH_*}}` tokens (base classes / interfaces / ROP wrapper) fed by the [`architecture`](#parameterizable-architecture--architecture) config after placeholder expansion.

An unmapped `{{TOKEN}}` stays literal in the output, so wire it in `TemplateRenderer.BuildTokens` first. Your `chetogen.json` can add/override tokens via `"tokens"` (merged last, they win), or remap the base classes via `"architecture"`.

---

## Self-contained server-mode pagination

Server mode needs **no external paging project**. The first server-mode run writes `{BaseNamespace}.Application.Models/Paging.cs` with two tiny types: `PagedQuery` (`Page`/`PageSize`/`SortBy`/`SortDesc` + `ToSkipTake()`) and the serializable `PagedResult<T>` (`Items`/`Total`/`Page`/`PageSize` + `TotalPages`/`HasNext`/`HasPrevious`). They live in the models project (shared by API and client), so **no `<ProjectReference>` is ever added**. The `{Entity}Filter` inherits `PagedQuery`; the `I{Entity}DA` port returns `(IReadOnlyList<TEntity> Items, int Total)` — plain Skip/Take, no shared types crossing layers — and the service builds the `PagedResult<TModel>`. If your solution already has its own paging types, override `Application.Paging.scriban` in `chetogen-templates`, or drop the `Application.Paging` step via `excludeTemplates`.

---

## Idempotence

If the entity already exists, existing files are skipped and shared files aren't duplicated (mutators detect their anchor before inserting). Safe to run repeatedly.
