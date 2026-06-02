using System.ComponentModel;
using System.Text;
using ChetoGen.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ChetoGen.Commands;

/// <summary>
/// Scaffolds a starter <c>chetogen.json</c> (and, optionally, a local copy of the templates)
/// so a consumer can point the generator at their own solution without editing the tool.
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
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(settings.Root);
        var baseNamespace = FirstNonEmpty(settings.BaseNamespace)
            ?? InferBaseNamespace(root)
            ?? new DirectoryInfo(root).Name;

        var configPath = Path.Combine(root, ConfigLoader.ConfigFileName);
        if (File.Exists(configPath) && !settings.Force)
        {
            AnsiConsole.MarkupLine($"[yellow]○ Ya existe[/] [white]{ConfigLoader.ConfigFileName}[/] [grey]en {root.EscapeMarkup()}. Usá[/] [white]--force[/] [grey]para sobrescribir.[/]");
            return Task.FromResult(1);
        }

        string? templatesDir = null;
        if (settings.WithTemplates)
        {
            templatesDir = CopyTemplates(root);
            if (templatesDir is null)
            {
                AnsiConsole.MarkupLine("[red]✗ No se encontró la carpeta de templates integrada para copiar.[/]");
                return Task.FromResult(1);
            }
        }

        File.WriteAllText(configPath, BuildSampleJson(baseNamespace, copyTemplates: templatesDir is not null), Encoding.UTF8);

        AnsiConsole.MarkupLine($"[green]✚ Escrito[/] [white]{ConfigLoader.ConfigFileName}[/] [grey]· baseNamespace =[/] [aqua]{baseNamespace.EscapeMarkup()}[/]");
        if (templatesDir is not null)
            AnsiConsole.MarkupLine($"[green]✚ Templates copiadas[/] [grey]→ {TemplatesFolderName}/ (editá las que quieras; el resto cae al set integrado)[/]");
        AnsiConsole.MarkupLine("[grey]Ajustá los valores a gusto y corré[/] [white]chetogen generate <Entity>[/][grey].[/]");
        return Task.FromResult(0);
    }

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

    /// <summary>
    /// Emits a documented config. Comments are legal here because <see cref="ConfigLoader"/>
    /// parses with <c>ReadCommentHandling = Skip</c> and <c>AllowTrailingCommas</c>.
    /// </summary>
    private static string BuildSampleJson(string baseNamespace, bool copyTemplates)
    {
        var templatesLine = copyTemplates
            ? $"  \"templatesDirectory\": \"{TemplatesFolderName}\","
            : $"  // \"templatesDirectory\": \"{TemplatesFolderName}\",";

        return $$"""
        {
          // Namespace / project prefix of your solution. Replaces {BaseNamespace} everywhere.
          "baseNamespace": "{{baseNamespace}}",

          // How the solution root is located when walking up from the working directory.
          // "rootMarkers": ["*.slnx", "*.sln"],

          // Folder (relative to this file) with templates that override/extend the built-in set.
        {{templatesLine}}

          // Project shown in the "next steps" run hint.
          // "appHostProject": "{BaseNamespace}.AppHost",

          // ProjectReference added to the models .csproj in server mode (the {Entity}Filter inherits PagedQuery).
          // "pagingProjectReference": "..\\{BaseNamespace}.Domain.Paging\\{BaseNamespace}.Domain.Paging.csproj",

          // Pipeline steps to skip, by friendly label (e.g. drop the Blazor edit page).
          // "excludeTemplates": [],

          // Shared-file mutators to skip, by key (DbContext, DataAccessDI, ApplicationDI, MappersDI, ClientProgram, NavMenu).
          // A different architecture (e.g. classic MVC) excludes the ones it doesn't have.
          // "excludeMutators": [],

          // Override individual output paths. {BaseNamespace} and {Entity} are expanded; use '/' separators.
          // "paths": {
          //   "DomainEntity": "{BaseNamespace}.Domain.Entities/{Entity}.cs"
          // },

          // Extra static template tokens; these win over the built-in ones.
          // "tokens": {}
        }

        """;
    }
}
