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
# -> artifacts/nupkg/ChetoGen.0.1.0.nupkg
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
# 1. (Optional) Create a chetogen.json at your solution root.
#    Infers baseNamespace from the .slnx/.sln file name.
chetogen init                 # or:  chetogen init --with-templates

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

  // ProjectReference added to the models .csproj in server mode.
  "pagingProjectReference": "..\\{BaseNamespace}.Domain.Paging\\{BaseNamespace}.Domain.Paging.csproj",

  // Pipeline steps to skip, by friendly label (e.g. drop the Blazor edit page).
  "excludeTemplates": ["Client.Edit.razor", "Client.Edit.razor.cs"],

  // Override individual output paths. {BaseNamespace} and {Entity} are expanded; use '/' separators.
  "paths": { "DomainEntity": "{BaseNamespace}.Domain.Entities/{Entity}.cs" },

  // Extra static tokens; these win over the built-ins (including BASE_NS).
  "tokens": {}
}
```

> `//` comments and trailing commas are allowed (the loader parses with `ReadCommentHandling = Skip` and `AllowTrailingCommas`).

**`paths` keys:** `DomainEntity`, `ApplicationModel`, `ApplicationFilter`, `ApplicationContract`, `ApplicationService`, `ApplicationMapper`, `ApplicationPersistence`, `DataAccess`, `ApiController`, `ApiClient`, `BlazorIndexRazor`, `BlazorIndexCs`, `BlazorEditRazor`, `BlazorEditCs`, `ApplicationModelsCsproj`, `AppDbContext`, `DataAccessDI`, `ApplicationDI`, `MappersDI`, `ClientProgram`, `NavMenu`.

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

| Option                  | Description                                                                  |
| ----------------------- | ---------------------------------------------------------------------------- |
| `--root <PATH>`         | Where to write `chetogen.json`. Default: detected solution root.            |
| `--base-namespace <NS>` | Force `baseNamespace`. Inferred from the `.slnx`/`.sln` when omitted.       |
| `--with-templates`      | Also copy the built-in templates to `./chetogen-templates` to customize.    |
| `--force`               | Overwrite an existing `chetogen.json`.                                       |

### Property flags (`--prop "Name:type:flag1:flag2..."`)

`required`, `filter`/`filterable`, `nofilter`, `hidden`/`hide`/`nolist`, `list`, `sort`/`sortable`, `nosort`.
Defaults: `required=false`, `filterable=true` for strings only, `showInList=true`, `sortable=true`.

### Supported types

`string`, `int`, `long`, `short`, `byte`, `decimal`, `double`, `float`, `bool`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`.

---

## What it generates

For an `Order` entity (Id `long`, `baseNamespace = "AspireApp"`):

**Always (up to 13):** `Order.cs` entity, application `Order.cs` model, `IOrderService`/`OrderService`, `OrderMapper`, `IOrderDA`/`OrderDA`, `OrderController`, `OrderApiClient`, and (if UI) `OrderIndex.razor(.cs)` + `OrderEdit.razor(.cs)` — all under `{BaseNamespace}.*` paths from `paths`.

**Extra in `server` mode:** `OrderFilter.cs` (inherits `PagedQuery`); the DA/service/contract/controller/api-client gain `GetPagedAsync({Entity}Filter, ct)` (LINQ-to-EF Skip/Take/Count/OrderBy); endpoint `POST /api/{entity}/query`.

**Patched (idempotent):** `AppDbContext` (`DbSet<>` + EF string config), 3× `DependencyInjection.cs` + `Client/Program.cs` (registrations), `NavMenu.razor` (contextual `NavLink`).

**Blazor pages:** Index with smart per-type filters (`string`→contains, `bool`→All/Yes/No, numeric→min–max, date→from–to), sortable headers, type-aware cells (`bool`→Yes/No, `DateTime`→`yyyy-MM-dd`, `Email`→`mailto:`, `Phone`→`tel:`), empty/loading states, and client (browser) or server (POST `/query` + pagination) mode. Edit page with `EditForm` + validation, type-aware inputs (bool as switch), Save/Cancel.

---

## Template tokens

Plain text with `{{TOKEN}}` (literal substitution — no AST/loops/ifs). Anything that varies per entity is built in C# and inserted as a token.

Key tokens: `{{BASE_NS}}` (your `baseNamespace`), `{{ENTITY}}`/`{{entity}}`/`{{ENTITY_CAMEL}}`/`{{ENTITY_PLURAL}}`/`{{entity_plural}}`, `{{ID_TYPE}}`/`{{ID_ROUTE_CONSTRAINT}}`, `{{ENTITY_ICON}}`/`{{ACCENT}}`/`{{ACCENT_SUBTLE}}`, `{{PAGE_SIZE}}`, the `{{PROPS_*}}` family (entity/model props, table head/body, form fields, filter fields/state/logic/reset/sort cases, filter DTO), `{{AUTHORIZE_*}}`, `{{EVENT_BUS_*}}`, `{{DISPLAY_NAME_EXPR}}`, and per-layer `{{*_USINGS}}`/`{{*_BODY}}` (empty in client, full in server — server usings are built from `config.BaseNamespace`).

An unmapped `{{TOKEN}}` stays literal in the output, so wire it in `TemplateRenderer.BuildTokens` first. Your `chetogen.json` can add/override tokens via `"tokens"` (merged last, they win).

---

## Layered convention behind server mode

The default templates assume the Clean Architecture convention `{BaseNamespace}.<Layer>` with a dedicated paging project: `{BaseNamespace}.Domain.Paging` holds `PagedResult<T>` and `PagedQuery`; `{BaseNamespace}.Application.Models` references it so `{Entity}Filter` can inherit `PagedQuery` (the first server-mode run adds that `<ProjectReference>`, idempotently). The `I{Entity}DA` port depends only on Domain; the service translates the `{Entity}Filter` DTO to `List<Expression<Func<TEntity,bool>>>` before crossing the port; the DA implements with LINQ-to-EF. If your solution uses a different convention, override `paths` and/or supply your own templates.

---

## Idempotence

If the entity already exists, existing files are skipped and shared files aren't duplicated (mutators detect their anchor before inserting). Safe to run repeatedly.
