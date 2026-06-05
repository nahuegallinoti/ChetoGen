# ChetoGen — Architecture (for devs)

> 🌐 **English** · [Español](./ARCHITECTURE.md)

Companion to [`README.en.md`](./README.en.md). The README explains **how to use** the tool; this doc explains **how the code is wired** so you can read it without getting lost.

---

## TL;DR

```
chetogen.json ─► GeneratorConfig ──────────────┐ (BaseNamespace, Paths, Architecture, Tokens, TemplatesDir)
CLI args ─► Settings ─► EntitySpec ─► GenerationPlan ─► [TemplateRenderer + IFileMutator] ─► disk
            (Spectre)   (record)      (Creations +       Templates/*.scriban + {{TOKEN}} map   IO
                                       Mutators)         (BASE_NS + ARCH_* + config.Tokens) + mutations
```

A single command (`generate`, plus `init`) that takes one entity name, asks (or accepts) a handful of properties, and writes one full CRUD slice across **Domain → Application → Infrastructure → Api → Client** plus a Blazor Index/Edit pair. Shared files (DI registrations, NavMenu, AppDbContext) get patched in-place by idempotent mutators. Nothing is hard-wired to a solution: namespaces, paths, and templates come from `GeneratorConfig` (loaded by `ConfigLoader` from `chetogen.json`, or defaults with the namespace inferred from the `.slnx`).

---

## Repo layout (only files that matter)

```
ChetoGen/                            ← the tool project (PackAsTool → `chetogen` command)
├── Program.cs                       ← Spectre.Console.Cli bootstrap; CommandApp<GenerateCommand> + `init` command
├── Configuration/                   ← the layer that makes the generator agnostic
│   ├── GeneratorConfig.cs           ← record: BaseNamespace, Paths, Architecture, RootMarkers, TemplatesDirectory, Tokens… + DefaultPaths/DefaultArchitecture + Expand()
│   └── ConfigLoader.cs              ← discovers/parses chetogen.json, merges over defaults, infers BaseNamespace from the .slnx
├── Commands/
│   ├── GenerateCommand.cs           ← all UX: prompts, banner, preview panel, plan rendering, execution
│   └── InitCommand.cs               ← writes a starter chetogen.json (and optionally copies the templates)
├── Generator/                       ← pure logic, no console IO
│   ├── EntitySpec.cs                ← the immutable record describing what to generate
│   ├── PropertySpec.cs              ← one entity field (Name, Type, Required, Filterable, ShowInList, Sortable)
│   ├── PathResolver.cs              ← resolves every target path from config.Paths (expands {BaseNamespace}/{Entity})
│   ├── GenerationPlan.cs            ← Build(entity, paths, config) → list of (Creations, Mutators)
│   ├── TemplateRenderer.cs          ← reads Templates/*.scriban (override-able), replaces {{TOKEN}} from a token map
│   ├── IconPicker.cs                ← maps entity name → Bootstrap Icons class (deterministic)
│   ├── AccentPicker.cs              ← maps entity name → Bootstrap accent (currently always "primary")
│   └── Mutators/
│       ├── IFileMutator.cs          ← interface: TargetPath + Mutate(source, entity) → MutationResult
│       ├── DiRegistrationMutator.cs ← inserts using + AddScoped/AddSingleton lines after a marker
│       ├── DbContextMutator.cs      ← inserts DbSet<> + modelBuilder.Entity<>() config in AppDbContext
│       └── NavMenuMutator.cs        ← inserts a NavLink block inside <Authorized> or before </nav>
└── Templates/                       ← *.scriban text files; **NOT** real Scriban, just {{TOKEN}} substitution
    ├── Domain.Entity.scriban
    ├── Application.Model.scriban
    ├── Application.Filter.scriban   ← server-mode only
    ├── Application.Paging.scriban   ← server-mode only (self-contained PagedQuery + PagedResult)
    ├── Application.Contract.scriban
    ├── Application.Service.scriban
    ├── Application.Persistence.scriban
    ├── Application.Mapper.scriban
    ├── DataAccess.scriban
    ├── Api.Controller.scriban
    ├── Client.ApiClient.scriban
    ├── Client.Index.razor.scriban       ← client-mode index
    ├── Client.Index.razor.cs.scriban
    ├── Client.IndexServer.razor.scriban ← server-mode index
    ├── Client.IndexServer.razor.cs.scriban
    ├── Client.Edit.razor.scriban
    └── Client.Edit.razor.cs.scriban
```

Templates are copied to `bin/.../Templates/` on build and ship inside the `.nupkg` (`tools/<tfm>/any/Templates/`). `TemplateRenderer` reads them from `AppContext.BaseDirectory/Templates` at runtime; if `chetogen.json` sets `templatesDirectory`, it looks **there first** for each template by name and falls back to the built-in set. Editing the built-in templates needs a rebuild; the consumer's don't.

---

## Pipeline, step by step

### 1. Bootstrap — `Program.cs`
Sets UTF-8 console, builds a Spectre `CommandApp<GenerateCommand>` with an extra `init` command, runs. That's it.

### 2. Load config and discover the root — `ConfigLoader.Load(config?, root?)`
Returns a `LoadedConfig (GeneratorConfig, SolutionRoot, ConfigFilePath?)`:

- **Config file**: explicit `--config`, or walk up looking for `chetogen.json`. If found, parse JSON (comments + trailing commas allowed) to a DTO and **merge it over `GeneratorConfig.CreateDefault(...)`**.
- **Root**: explicit `--root` > the `chetogen.json` folder > walk up by `config.RootMarkers` (`*.slnx`/`*.sln`).
- **BaseNamespace**: config value > **inferred from the solution file name** (`Acme.slnx → "Acme"`) > root folder name.

With no `chetogen.json` the tool still works (all defaults + inferred namespace). Then `new PathResolver(root, config)` owns paths — every destination comes from `config.Paths` expanding `{BaseNamespace}`/`{Entity}`; no path-string math anywhere else.

### 3. Build the spec — `ResolveEntityBase` + `ResolveOptions`
Either fully interactive (Spectre prompts) or fully driven by CLI flags. The output is one `EntitySpec` record with everything decided. Key resolvers:

- `ResolveFilterMode` — `client` (default) or `server`. Determines whether the Index does in-browser filtering or talks to a paged endpoint.
- `ResolvePageSize` — server-mode only; defaults to 25.
- Properties come from `PropertySpec.Parse` (the `--prop` form) or, interactively, from `CollectPropertiesInteractive` — a live-table editor with an **Add / Edit / Remove / Done** menu. Each field is built in `PromptForProperty` (name + type + a multi-select of the Required/Filterable/ShowInList/Sortable flags), so a wrong answer is fixed by editing the row, not restarting. Names go through `Naming.IsValidIdentifier` and are normalized with `Naming.Capitalize`.

### 4. Build the plan — `GenerationPlan.Build(entity, paths, config)`
Pure function. Returns a record with two lists (filtering out any step listed in `config.ExcludeTemplates`):

- **Creations**: `(TargetPath, TemplateName, FriendlyLabel)`. Every file the generator will write (skipped if it exists).
- **Mutators**: list of `IFileMutator`. Every shared file that needs an idempotent patch.

This is where the **conditional templates** live (server-mode adds `Application.Filter.scriban` and swaps the Index templates) — `GenerationPlan.Build` is the single source of truth for "which files for this config".

### 5. Render the preview — `RenderEntitySummary` + `RenderPlan`
All Spectre. Two panels (entity summary + plan tree grouped by layer). If not `--yes` and not `--dry-run`, prompts to confirm.

### 6. Execute — `ExecutePlanAsync`
Two passes:

- **Creations**: the token map is built **once** via `TemplateRenderer.BuildTokens(entity, config)` and reused for every file. For each, `renderer.Render(templateName, tokens)` → if file exists, skip. Otherwise create dirs and write. In `--dry-run`, prints the path but doesn't write.
- **Mutators**: for each, read source → `mutator.Mutate(source, entity)` → if `Changed`, write back. If it can't find its anchor, it surfaces an error and continues with the rest (counted in `totals.Failed`).

### 7. Summary — `RenderResult`
Counts of created / mutated / skipped / failed in a panel, plus a "next steps" footer (build, migration, run).

---

## Core types (cheat sheet)

| Type | Role |
| --- | --- |
| `EntitySpec` | Immutable record. Everything the generator needs in one place: name, id type, properties, flags (Blazor/Nav/Auth/EventBus), `FilterMode`, `PageSize`, icon/accent overrides. Derived helpers: `Lower`, `Camel`, `Plural`, `IsServerFiltering`, `FilterableProperties`, `ListProperties`, `SortableProperties`. |
| `PropertySpec` | One field. `Name`, `Type` (normalized), `Required`, `Filterable`, `ShowInList`, `Sortable`. Knows its type bucket via `IsString` / `IsBool` / `IsNumeric` / `IsDateTime` / `IsGuid` and its Razor input component (`InputText`, `InputNumber`, etc.). `Parse(raw)` handles the `--prop "Name:type:flag:flag"` CLI shape. |
| `GeneratorConfig` | Record holding everything agnostic: `BaseNamespace`, `Paths` (logical-key → path with `{BaseNamespace}`/`{Entity}`), `Architecture` (key → architecture seam, feeds the `ARCH_*` tokens), `RootMarkers`, `TemplatesDirectory`, `AppHostProject`, `ExcludeTemplates`, `ExcludeMutators`, `Tokens`. `DefaultPaths`/`DefaultArchitecture` reproduce the built-in layout and Clean Architecture; `Expand(t, entity)` resolves placeholders. |
| `ConfigLoader` | `Load(config?, root?, startDir?) → LoadedConfig`. Discovers and parses `chetogen.json`, merges over defaults, resolves the root, and infers `BaseNamespace`. |
| `PathResolver` | Single owner of "where does file X go for entity Y?". `Resolve(key, entity)` expands `config.Paths[key]` and combines it with the root — the only place with path math. |
| `GenerationPlan` | Returned by `Build(entity, paths, config)`. Holds `Creations` + `Mutators`. Conditionals (server-mode, Blazor/NavMenu on/off) and the `config.ExcludeTemplates` filter live here. |
| `TemplateRenderer` | `new(config)` fixes the templates root (built-in + override). `BuildTokens(entity, config)` builds the `{{KEY}} → value` map (includes `BASE_NS`, the `ARCH_*` via `ExpandArchitecture`, and merges `config.Tokens`). `Render(file, tokens)` resolves the template (override dir first), runs `StringBuilder.Replace` and **normalizes to LF**. |
| `IFileMutator` / `MutationResult` | Tiny contract for shared-file patches. Mutate is **pure** (string in, string out) — actual write happens in `GenerateCommand`. |

---

## How templates work (it's NOT Scriban)

Files have a `.scriban` extension by convention but the renderer is dead simple — no AST, no expressions, no loops:

```csharp
foreach (var (key, value) in tokens)
    sb.Replace("{{" + key + "}}", value);
```

That's the whole engine. **Tokens are plain `{{KEY}}` strings**, no `{{ for x in ... }}` or `{{ if foo }}`. Everything that varies per entity is built ahead of time into a string by a `BuildXxx(entity)` method in `TemplateRenderer`, then dropped in as a token.

### The two-phase trick

Most generated files have:
1. **A template skeleton** — the bits that never change shape (namespace, class header, base class).
2. **Body-builder tokens** — `{{PROPS_ENTITY}}`, `{{PERSISTENCE_BODY}}`, `{{DA_BODY}}`, `{{SERVICE_BODY}}`, etc. — multi-line strings built by C# methods.

So `Application.Persistence.scriban` is just:
```
{{PERSISTENCE_USINGS}}using {{BASE_NS}}.Application.Persistence.Base;
using {{BASE_NS}}.Domain.Entities;

namespace {{BASE_NS}}.Application.Persistence;

public interface I{{ENTITY}}DA : IBaseDA<{{ENTITY}}, {{ID_TYPE}}>
{
{{PERSISTENCE_BODY}}
}
```

…and `BuildPersistenceBody(entity)` either returns `""` (client mode) or a `GetPagedAsync(...)` method signature (server mode). Same template, two outputs.

### When to add a new template file vs a new token

- **New file**: only if the file is structurally different and conditionally generated. We have two Index razor templates (`Client.Index.razor` vs `Client.IndexServer.razor`) because the markup diverges too much for a token to express cleanly.
- **New token**: anything that's a chunk of text inside an otherwise-stable file. Strongly preferred — keeps the template count low.

### Token map cheat sheet

Defined in `TemplateRenderer.BuildTokens(entity, config)`. Naming convention:

- `BASE_NS` — the `config.BaseNamespace`; **every namespace in the templates** uses it (`{{BASE_NS}}.Domain.Entities`, etc.).
- `ENTITY`, `ID_TYPE`, etc. — scalar identity tokens.
- `PROPS_*` — per-property lists (entity props, model props, table head, filter fields, etc.).
- `*_USINGS` — extra `using` directives for that file (server-mode adds `using {{BASE_NS}}.Application.Models;`, built from `config.BaseNamespace`).
- `*_BODY` — the main body content of the file (empty `;\n` for client mode, full implementation for server mode).
- `AUTHORIZE_*`, `EVENT_BUS_*` — conditional fragments toggled by entity flags.
- `ARCH_*` — the **architecture seams**: base classes / interfaces / ROP wrapper the code sits on (`ARCH_ENTITY_BASE`, `ARCH_SERVICE_BASE`, `ARCH_CONTROLLER_BASE`, `ARCH_RESULT`, …). Unlike the rest, their value is **not hardcoded in C#**: it comes from `config.Architecture[key]` (overridable via `chetogen.json`) through `ExpandArchitecture`, which resolves the `{BaseNamespace}`/`{Entity}`/`{EntityCamel}`/`{Id}`/`{EventBusCtorParam}`/`{EventBusBaseArg}` placeholders. The defaults (`GeneratorConfig.DefaultArchitecture`) reproduce the reference Clean Architecture; this is what lets the **same template set** target any architecture without editing templates.

If a token doesn't appear in the map, `{{IT}}` is left **literally in the output**. Always wire a token in `BuildTokens` before using it in a template. A consumer's `chetogen.json` can add/override tokens via `"tokens"` (merged last, they win), or remap the base classes via `"architecture"` (merged over `DefaultArchitecture`).

> **Line endings**: `Render` normalizes all output to LF. Lines otherwise come from three uncoordinated sources (the `.scriban` files per checkout, C# literals with `\n`, and `Environment.NewLine` via `AppendLine`); collapsing to LF at the end makes output deterministic and clean regardless of OS.

---

## How mutators work

`IFileMutator` exists because some files are **shared across entities** — DI containers, the DbContext, NavMenu — and a fresh write would clobber existing entities. So instead of templating them, we read them, patch them, write them.

Three implementations cover everything:

### `DiRegistrationMutator`
Generic: takes a list of `usingLines`, a `registrationLine` (e.g. `services.AddScoped<IOrderService, OrderService>();`), and a `markerForLastRegistration` (a substring like `"services.AddScoped<I"`). Idempotent — if the registration line already exists, no-op.

Where it's wired: `GenerationPlan.Build` constructs **one per DI file** (DataAccessDI, ApplicationDI, MappersDI, ClientProgram).

### `DbContextMutator`
- Inserts `public DbSet<X> Xs => Set<X>();` after the last existing DbSet.
- If the entity has string properties, also inserts a `modelBuilder.Entity<X>(entity => { ... });` block before the closing brace of `OnModelCreating`. Uses a brace-depth counter to find the matching close brace.

### `NavMenuMutator`
Looks for `href="entityname"` to detect existing entries (idempotent). If the entity has `RequireAuth`, inserts the `<div class="nav-item">…<NavLink>…` block before the outer `</Authorized>`; otherwise before `</nav>`. Indentation is auto-detected from the anchor line.

All three are **anchor-based**: they fail loud if their anchor is missing rather than silently dropping the change.

> Server-mode patches **no** `.csproj`: the paging types (`PagedQuery` + `PagedResult<T>`) are generated self-contained in `Application.Models/Paging.cs` (see `Application.Paging.scriban`), so no project references are ever added.

---

## How the layered files fit together (one entity, server mode)

> The `{BaseNamespace}` prefixes come from `config.Paths` (here with `BaseNamespace = AspireApp`).

```
Domain.Entity      → {BaseNamespace}.Domain.Entities/{Entity}.cs
Application.Model  → {BaseNamespace}.Application.Models/App/{Entity}.cs
Application.Filter → {BaseNamespace}.Application.Models/App/{Entity}Filter.cs   (server only)
Application.Paging → {BaseNamespace}.Application.Models/Paging.cs               (server only, self-contained)
Application.Contract / Service        ← business logic, builds predicates from Filter
Application.Persistence (I{Entity}DA) ← port, returns (IReadOnlyList<TEntity> Items, int Total); takes predicates + skip/take
Application.Mapper                    ← {Entity}Mapper, ToModel / ToEntity / ToModelList
DataAccess ({Entity}DA)               ← adapter, EF Core LINQ implementation
Api.Controller                        ← REST surface, POST /api/{entity}/query for server mode
Client.ApiClient                      ← typed HTTP wrapper, GetPagedAsync(filter, ct) extra in server mode
Client.Index.razor + .razor.cs        ← Blazor page (client or server variant)
Client.Edit.razor  + .razor.cs        ← Blazor page (single shared template)
```

For client mode, drop `Application.Filter` + `Application.Paging` and swap the Index variant.

---

## Recipes

### I want to add a new property type
1. `PropertySpec.NormalizeType` — map the user-facing name to the canonical C# type.
2. `PropertySpec` getters — add to whichever of `IsString` / `IsBool` / `IsNumeric` / `IsDateTime` / `IsGuid` makes sense (or add a new bucket).
3. `PropertySpec.RazorInputComponent` — Razor `InputXxx` to use in `Client.Edit.razor`.
4. `TemplateRenderer.BuildFilterFields` / `BuildFilterDtoProps` / `AppendServicePredicate` — emit the smart filter shape (range, dropdown, etc.) for the new bucket.

### I want to add a new generated file
1. `Configuration/GeneratorConfig.cs` — add a `PathKeys` constant and its default in `DefaultPaths` (with `{BaseNamespace}`/`{Entity}`).
2. `PathResolver` — add a `Resolve(PathKeys.MyKey, entity)` helper (optional, for readability).
3. `Templates/` — drop the new `.scriban` file (use `{{BASE_NS}}` for namespaces).
4. `GenerationPlan.Build` — add a `new FileCreation(...)` (wrap in `if (entity.IsServerFiltering)` etc. if conditional).
5. Run with `--dry-run` to confirm it appears in the plan tree.

### I want to add a new token
1. `TemplateRenderer.BuildTokens(entity, config)` — add `["MY_TOKEN"] = BuildMyToken(entity)` (or use `config.BaseNamespace` if emitting namespaces).
2. Implement `BuildMyToken(EntitySpec entity)` — return a `string`. Use a `StringBuilder` if multi-line.
3. Add `{{MY_TOKEN}}` to whichever template needs it. Rebuild.

### I want to target another solution / change the layout or templates
1. **No code**: add a `chetogen.json` (`chetogen init`) with `baseNamespace`, `paths`/`architecture` overrides, `excludeTemplates`/`excludeMutators`, and/or `tokens`.
2. **Own templates**: `chetogen init --with-templates` copies the set to `chetogen-templates/`; edit only what you want (the rest falls back to the built-in set) and point `templatesDirectory` at it.
3. Built-in layout defaults: `GeneratorConfig.DefaultPaths`. Namespace inference and loading live in `ConfigLoader`.

### I want to target ANOTHER architecture (different base classes / ROP wrapper)
1. **No code or templates**: in `chetogen.json`, override the `"architecture"` keys that differ (e.g. `ResultType`/`ResultIsSuccess` if your ROP wrapper is named differently, or `EntityBase`/`EntityUsings` for another base class). It merges over `DefaultArchitecture`; the rest falls back to defaults. `chetogen init` → *"map to my own base classes"* dumps the whole block.
2. Values accept `{BaseNamespace}`/`{Entity}`/`{EntityCamel}`/`{Id}`/`{EventBusCtorParam}`/`{EventBusBaseArg}` (see `ExpandArchitecture`).
3. **If you need a new architecture token**: add the key to `GeneratorConfig.DefaultArchitecture`, the `["ARCH_…"] = Arch("…")` entry in `BuildTokens`, and use `{{ARCH_…}}` in the template. Remember: these tokens **remap** base classes — they don't change the generated body (that's a template).

### I want to add a new shared-file patch
1. Implement `IFileMutator` (existing ones are 40–115 LOC; copy the closest fit).
2. Anchor on **a stable substring** in the target file. Never use line numbers.
3. Add an idempotency check (early-return with `Changed: false` if the patch is already there).
4. Wire it in `GenerationPlan.Build`'s `mutators` list.

### I want to change the CLI surface
- All flags live in `GenerateCommand.Settings` as `[CommandOption]` properties. Resolve them inside the dedicated resolvers (`ResolveEntityBase`/`ResolveOptions`, with an interactive fallback if it's a meaningful choice).

### I want to see what would be generated without writing
- `--dry-run`. Hits every code path except the actual `File.WriteAllTextAsync` and mutator writes.

---

## Conventions

- **Pure logic in `Generator/`, console UX in `Commands/`.** Don't print to `AnsiConsole` from `TemplateRenderer` or `EntitySpec`. The renderer/spec must be unit-testable headless.
- **`PathResolver` owns paths.** No string-concat path math anywhere else.
- **Mutators are idempotent.** Re-running the generator for the same entity must be a no-op on shared files.
- **Templates are dumb.** All logic in C#; templates are skeletons. If you find yourself wanting `{{ if }}` in a template, build the variant in a body-builder method instead and emit it as a token.
- **No partial state on failure.** A mutator that can't find its anchor fails its single file and is reported in `totals.Failed`; the rest of the plan still runs.
