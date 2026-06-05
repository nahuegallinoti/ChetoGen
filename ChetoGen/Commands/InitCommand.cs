using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ChetoGen.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChetoGen.Commands;

/// <summary>
/// Scaffolds a <c>chetogen.json</c> so a consumer can point the generator at their own solution
/// without editing the tool. Runs an interactive wizard by default; falls back to a documented
/// defaults file with <c>--yes</c> (or when stdin is redirected, e.g. CI).
/// </summary>
internal sealed class InitCommand : AsyncCommand<InitCommand.Settings>
{
    public const string TemplatesFolderName = "chetogen-templates";

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--root <PATH>")]
        [Description("Where to write chetogen.json. Defaults to the discovered solution root (or the current directory).")]
        public string? Root { get; init; }

        [CommandOption("--base-namespace <NS>")]
        [Description("Namespace/project prefix to bake into the config. Inferred from the .slnx/.sln name when omitted.")]
        public string? BaseNamespace { get; init; }

        [CommandOption("--with-templates")]
        [Description("Also copy the built-in templates into ./chetogen-templates so you can customize them.")]
        public bool WithTemplates { get; init; }

        [CommandOption("--force")]
        [Description("Overwrite an existing chetogen.json.")]
        public bool Force { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip the interactive wizard and write a documented chetogen.json with defaults (plus the flags above).")]
        public bool Yes { get; init; }
    }

    // ----------------------------------------------------------------------------------
    // Friendly groupings — the wizard speaks in layers/features, not raw template labels.
    // Unchecking a group maps to the underlying friendly labels / mutator keys to skip.
    // ----------------------------------------------------------------------------------

    private static readonly (string Display, string[] Labels)[] LayerGroups =
    [
        ("Domain — entities", ["Domain.Entity"]),
        ("Application — model, contract, service, mapper, persistence",
            ["Application.Model", "Application.Contract", "Application.Service", "Application.Mapper", "Application.Persistence"]),
        ("Data access — EF Core", ["DataAccess"]),
        ("REST API — controllers", ["Api.Controller"]),
        ("Typed HTTP client — ApiClients", ["Client.ApiClient"]),
        ("Blazor UI — Index + Edit pages",
            ["Client.Index.razor", "Client.Index.razor.cs", "Client.Edit.razor", "Client.Edit.razor.cs"]),
    ];

    private static readonly (string Display, string[] Keys)[] MutatorGroups =
    [
        ("AppDbContext — DbSet<> + EF config", ["DbContext"]),
        ("Dependency injection — DependencyInjection.cs", ["DataAccessDI", "ApplicationDI", "MappersDI"]),
        ("Client/Program.cs — register the ApiClient", ["ClientProgram"]),
        ("NavMenu.razor — add the NavLink", ["NavMenu"]),
    ];

    /// <summary>Everything the wizard (or flags) decided, ready to serialize.</summary>
    internal sealed record InitPlan(
        string BaseNamespace,
        bool CopyTemplates,
        IReadOnlyList<string> ExcludeTemplates,
        IReadOnlyList<string> ExcludeMutators,
        string? AppHostProject,
        bool CustomPaths,
        bool CustomArchitecture = false);

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(settings.Root);
        var inferred = FirstNonEmpty(settings.BaseNamespace) ?? InferBaseNamespace(root) ?? new DirectoryInfo(root).Name;
        var configPath = Path.Combine(root, ConfigLoader.ConfigFileName);

        // Interactive unless explicitly opted out or stdin is not a console (CI, piped input).
        var interactive = !settings.Yes && !Console.IsInputRedirected;

        InitPlan plan;
        if (interactive)
        {
            var collected = RunWizard(root, inferred, configPath, settings);
            if (collected is null)
            {
                AnsiConsole.MarkupLine("[grey]✗ Cancelled. Nothing was written.[/]");
                return Task.FromResult(0);
            }
            plan = collected;
        }
        else
        {
            if (File.Exists(configPath) && !settings.Force)
            {
                AnsiConsole.MarkupLine($"[yellow]○ Already exists[/] [white]{ConfigLoader.ConfigFileName}[/] [grey]in {root.EscapeMarkup()}. Use[/] [white]--force[/] [grey]to overwrite.[/]");
                return Task.FromResult(1);
            }
            plan = new InitPlan(inferred, settings.WithTemplates, [], [], $"{inferred}.AppHost", CustomPaths: false, CustomArchitecture: false);
        }

        string? templatesDir = null;
        if (plan.CopyTemplates)
        {
            templatesDir = CopyTemplates(root);
            if (templatesDir is null)
            {
                AnsiConsole.MarkupLine("[red]✗ Could not find the built-in templates folder to copy.[/]");
                return Task.FromResult(1);
            }
        }

        File.WriteAllText(configPath, BuildConfigJson(plan), Encoding.UTF8);
        RenderResult(plan, configPath, templatesDir);
        return Task.FromResult(0);
    }

    // ----------------------------------------------------------------------------------
    // Interactive wizard
    // ----------------------------------------------------------------------------------

    private static InitPlan? RunWizard(string root, string inferred, string configPath, Settings settings)
    {
        RenderBanner();
        AnsiConsole.Write(new Padder(new Markup($"[grey]▸ Detected root:[/] [aqua]{root.EscapeMarkup()}[/]"), new Padding(0, 0, 0, 1)));

        if (File.Exists(configPath) && !settings.Force &&
            !AnsiConsole.Confirm($"[yellow]Already exists[/] [white]{ConfigLoader.ConfigFileName}[/][yellow].[/] Overwrite?", defaultValue: false))
        {
            return null;
        }

        var baseNs = AnsiConsole.Prompt(
            new TextPrompt<string>("[bold mediumpurple1]❯[/] Namespace/project prefix [grey](baseNamespace)[/]")
                .DefaultValue(inferred)
                .ShowDefaultValue()
                .PromptStyle("aqua")
                .Validate(static raw => string.IsNullOrWhiteSpace(raw)
                    ? ValidationResult.Error("Cannot be empty")
                    : ValidationResult.Success()))
            .Trim();

        AnsiConsole.WriteLine();
        var selectedLayers = AnsiConsole.Prompt(
            BuildPreselectedMultiSelect(
                "[bold mediumpurple1]❯[/] Which [bold]layers[/] do you want to generate?",
                "[grey]uncheck (space) whatever your architecture lacks · Enter to confirm[/]",
                LayerGroups.Select(g => g.Display)));
        var excludeTemplates = LayerGroups
            .Where(g => !selectedLayers.Contains(g.Display))
            .SelectMany(g => g.Labels)
            .ToList();

        AnsiConsole.WriteLine();
        var selectedMutators = AnsiConsole.Prompt(
            BuildPreselectedMultiSelect(
                "[bold mediumpurple1]❯[/] Which [bold]shared files[/] should ChetoGen update?",
                "[grey]uncheck the ones your solution lacks · Enter to confirm[/]",
                MutatorGroups.Select(g => g.Display)));
        var excludeMutators = MutatorGroups
            .Where(g => !selectedMutators.Contains(g.Display))
            .SelectMany(g => g.Keys)
            .ToList();

        AnsiConsole.WriteLine();
        string? appHost = null;
        if (AnsiConsole.Confirm("[bold mediumpurple1]❯[/] Add an [bold]AppHost[/] project to the \"next steps\" hint?", defaultValue: true))
        {
            appHost = AnsiConsole.Prompt(
                new TextPrompt<string>("  [grey]·[/] AppHost project")
                    .DefaultValue($"{baseNs}.AppHost")
                    .ShowDefaultValue()
                    .PromptStyle("aqua"))
                .Trim();
        }

        AnsiConsole.WriteLine();
        var defaultLayout = AnsiConsole.Confirm(
            $"[bold mediumpurple1]❯[/] Does your solution use the default layout [grey]({baseNs.EscapeMarkup()}.Domain.Entities, {baseNs.EscapeMarkup()}.Application.Models, …)[/]?",
            defaultValue: true);
        var customPaths = !defaultLayout;

        AnsiConsole.WriteLine();
        var defaultArchitecture = AnsiConsole.Confirm(
            "[bold mediumpurple1]❯[/] Does your code rely on ChetoGen's base classes [grey](BaseEntity, BaseService, BaseController, the Result wrapper…)[/]?",
            defaultValue: true);
        var customArchitecture = !defaultArchitecture;
        if (customArchitecture)
        {
            AnsiConsole.MarkupLine("  [grey]· I'll dump the full[/] [white]\"architecture\"[/] [grey]block in the JSON so you can map each base class / wrapper to your solution's.[/]");
        }

        var copyTemplates = settings.WithTemplates || AnsiConsole.Confirm(
            "[bold mediumpurple1]❯[/] Copy the templates to [white]chetogen-templates/[/] to rewrite the [bold]body[/] of the generated code [grey](beyond base classes and paths)[/]?",
            defaultValue: customPaths);

        var plan = new InitPlan(
            baseNs,
            copyTemplates,
            excludeTemplates,
            excludeMutators,
            string.IsNullOrWhiteSpace(appHost) ? null : appHost,
            customPaths,
            customArchitecture);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold mediumpurple1]◈ Preview · chetogen.json[/]").RuleStyle("grey39").LeftJustified());
        AnsiConsole.Write(new Panel(new Text(BuildConfigJson(plan)))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey39),
            Padding = new Padding(2, 1, 2, 1),
        });

        return AnsiConsole.Confirm("[bold]Write the file with this configuration?[/]", defaultValue: true)
            ? plan
            : null;
    }

    /// <summary>A multi-select with every option pre-checked, so the user only unchecks what they lack.</summary>
    private static MultiSelectionPrompt<string> BuildPreselectedMultiSelect(string title, string instructions, IEnumerable<string> choices)
    {
        var prompt = new MultiSelectionPrompt<string>()
            .Title(title)
            .NotRequired()
            .HighlightStyle(Style.Parse("aqua"))
            .InstructionsText(instructions);

        foreach (var choice in choices)
        {
            prompt.AddChoice(choice);
            prompt.Select(choice);
        }

        return prompt;
    }

    private static void RenderBanner()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold mediumpurple1]✦ ChetoGen · init[/]").RuleStyle("grey39").LeftJustified());
        AnsiConsole.MarkupLine("[grey]Configuration wizard — build your[/] [white]chetogen.json[/] [grey]in a few steps.[/]");
        AnsiConsole.WriteLine();
    }

    private static void RenderResult(InitPlan plan, string configPath, string? templatesDir)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✚ Written[/] [white]{Path.GetFileName(configPath)}[/] [grey]· baseNamespace =[/] [aqua]{plan.BaseNamespace.EscapeMarkup()}[/]");
        if (templatesDir is not null)
            AnsiConsole.MarkupLine($"[green]✚ Templates copied[/] [grey]→ {TemplatesFolderName}/ (edit whichever you want; the rest fall back to the built-in set)[/]");
        if (plan.ExcludeTemplates.Count > 0)
            AnsiConsole.MarkupLine($"[grey]○ Excluded layers:[/] [yellow]{string.Join(", ", plan.ExcludeTemplates).EscapeMarkup()}[/]");
        if (plan.ExcludeMutators.Count > 0)
            AnsiConsole.MarkupLine($"[grey]○ Excluded mutators:[/] [yellow]{string.Join(", ", plan.ExcludeMutators).EscapeMarkup()}[/]");
        if (plan.CustomArchitecture)
            AnsiConsole.MarkupLine("[grey]◈ Block[/] [white]\"architecture\"[/] [grey]dumped — map the base classes / wrapper to your solution's before generating.[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Tweak whatever you want and generate your first entity:[/] [white]chetogen generate <Entity>[/][grey].[/]");
    }

    // ----------------------------------------------------------------------------------
    // Config file rendering (pure — unit tested)
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// Emits a documented <c>chetogen.json</c> from a plan. Comments and trailing commas are legal
    /// because <see cref="ConfigLoader"/> parses with <c>ReadCommentHandling = Skip</c> and
    /// <c>AllowTrailingCommas</c>.
    /// </summary>
    internal static string BuildConfigJson(InitPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  // Generated by `chetogen init`. All keys are optional and merge over the defaults.");
        sb.AppendLine("  // // comments and trailing commas are allowed.");
        sb.AppendLine();

        sb.AppendLine("  // Your solution's namespace/project prefix. Replaces {BaseNamespace} everywhere.");
        sb.AppendLine($"  \"baseNamespace\": \"{plan.BaseNamespace}\",");
        sb.AppendLine();

        if (plan.CopyTemplates)
        {
            sb.AppendLine("  // Folder with your own templates (looked up first by name; the rest fall back to the built-in set).");
            sb.AppendLine($"  \"templatesDirectory\": \"{TemplatesFolderName}\",");
        }
        else
        {
            sb.AppendLine("  // Your own templates: run `chetogen init --with-templates` to copy and edit them.");
            sb.AppendLine($"  // \"templatesDirectory\": \"{TemplatesFolderName}\",");
        }
        sb.AppendLine();

        if (plan.AppHostProject is { } appHost)
        {
            sb.AppendLine("  // Project shown in the \"next steps\" hint.");
            sb.AppendLine($"  \"appHostProject\": \"{appHost}\",");
            sb.AppendLine();
        }

        sb.AppendLine("  // Layers that are NOT generated (by \"friendly label\"). Useful to trim the slice to your architecture.");
        sb.AppendLine($"  \"excludeTemplates\": [{FormatJsonArray(plan.ExcludeTemplates)}],");
        sb.AppendLine();

        sb.AppendLine("  // Shared files that are NOT patched (by key): DbContext, DataAccessDI, ApplicationDI, MappersDI, ClientProgram, NavMenu.");
        sb.AppendLine($"  \"excludeMutators\": [{FormatJsonArray(plan.ExcludeMutators)}],");
        sb.AppendLine();

        if (plan.CustomPaths)
        {
            sb.AppendLine("  // Output paths per logical slot. Adjust the ones that differ from your layout.");
            sb.AppendLine("  // {BaseNamespace} and {Entity} are expanded; use '/' as the separator (normalized per OS).");
            sb.AppendLine("  \"paths\": {");
            var entries = GeneratorConfig.DefaultPaths.ToList();
            for (var i = 0; i < entries.Count; i++)
            {
                var comma = i < entries.Count - 1 ? "," : string.Empty;
                sb.AppendLine($"    \"{entries[i].Key}\": \"{entries[i].Value}\"{comma}");
            }
            sb.AppendLine("  },");
        }
        else
        {
            sb.AppendLine("  // Path overrides (uncomment if your layout differs). {BaseNamespace}/{Entity} are expanded.");
            sb.AppendLine("  // \"paths\": { \"DomainEntity\": \"{BaseNamespace}.Domain.Entities/{Entity}.cs\" },");
        }
        sb.AppendLine();

        if (plan.CustomArchitecture)
        {
            sb.AppendLine("  // Base classes / interfaces / result wrapper the code is generated on top of.");
            sb.AppendLine("  // Map each key to YOUR solution's equivalent (or empty \"\" the ones you don't use).");
            sb.AppendLine("  // Placeholders: {BaseNamespace}, {Entity}, {EntityCamel}, {Id}, {EventBusCtorParam}, {EventBusBaseArg}.");
            sb.AppendLine("  \"architecture\": {");
            var arch = GeneratorConfig.DefaultArchitecture.ToList();
            for (var i = 0; i < arch.Count; i++)
            {
                var comma = i < arch.Count - 1 ? "," : string.Empty;
                sb.AppendLine($"    \"{arch[i].Key}\": {JsonString(arch[i].Value)}{comma}");
            }
            sb.AppendLine("  },");
        }
        else
        {
            sb.AppendLine("  // Base classes / wrappers (BaseEntity, BaseService, BaseController, Result, …).");
            sb.AppendLine("  // Uncomment and edit the keys that differ from ChetoGen's; the rest fall back to the default.");
            sb.AppendLine("  // \"architecture\": { \"ResultType\": \"Result\", \"ResultIsSuccess\": \"Success\", \"ResultValue\": \"Value\" },");
        }
        sb.AppendLine();

        sb.AppendLine("  // Extra static {{TOKEN}} tokens for your templates (merged last, win over the built-in ones).");
        sb.AppendLine("  \"tokens\": {}");
        sb.AppendLine("}");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatJsonArray(IReadOnlyList<string> items) =>
        items.Count == 0 ? string.Empty : string.Join(", ", items.Select(i => $"\"{i}\""));

    private static readonly JsonSerializerOptions ValueJsonOptions =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Quotes + escapes a string as a JSON literal. Architecture values carry newlines, angle
    /// brackets and braces; the relaxed encoder keeps <c>&lt;</c>/<c>&gt;</c> readable and only
    /// escapes what JSON requires (e.g. the trailing newline becomes <c>\n</c>).
    /// </summary>
    private static string JsonString(string value) => JsonSerializer.Serialize(value, ValueJsonOptions);

    // ----------------------------------------------------------------------------------
    // Shared helpers
    // ----------------------------------------------------------------------------------

    private static string ResolveRoot(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (current.GetFiles("*.slnx").Length > 0 || current.GetFiles("*.sln").Length > 0)
                return current.FullName;
            current = current.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static string? InferBaseNamespace(string root)
    {
        var dir = new DirectoryInfo(root);
        var solution = dir.GetFiles("*.slnx").FirstOrDefault() ?? dir.GetFiles("*.sln").FirstOrDefault();
        return solution is null ? null : Path.GetFileNameWithoutExtension(solution.Name);
    }

    private static string? CopyTemplates(string root)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Templates");
        if (!Directory.Exists(source))
            return null;

        var dest = Path.Combine(root, TemplatesFolderName);
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        return dest;
    }

    private static string? FirstNonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
