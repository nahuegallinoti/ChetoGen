using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChetoGen.Configuration;

/// <summary>The fully-resolved config plus where the generator decided the solution root is.</summary>
internal sealed record LoadedConfig(GeneratorConfig Config, string SolutionRoot, string? ConfigFilePath);

/// <summary>
/// Finds and parses <c>chetogen.json</c>, merging it over the built-in defaults. The tool is
/// usable with no config at all: the solution root is found by <see cref="GeneratorConfig.RootMarkers"/>
/// and <c>BaseNamespace</c> is inferred from the solution file name (e.g. <c>AspireApp.slnx → "AspireApp"</c>).
/// A <c>chetogen.json</c> only exists to override that.
/// </summary>
internal static class ConfigLoader
{
    public const string ConfigFileName = "chetogen.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static LoadedConfig Load(string? explicitConfigPath = null, string? explicitRoot = null, string? startDirectory = null)
    {
        var startDir = startDirectory ?? Directory.GetCurrentDirectory();

        var configPath = ResolveConfigPath(explicitConfigPath, startDir);
        var dto = configPath is not null ? ParseDto(configPath) : null;

        var rootMarkers = dto?.RootMarkers is { Count: > 0 } ? dto.RootMarkers : ["*.slnx", "*.sln"];

        var root = ResolveRoot(explicitRoot, configPath, startDir, rootMarkers);

        var baseNamespace =
            FirstNonEmpty(dto?.BaseNamespace)
            ?? InferBaseNamespace(root, rootMarkers)
            ?? new DirectoryInfo(root).Name;

        var config = BuildConfig(baseNamespace, dto, configPath, rootMarkers);
        return new LoadedConfig(config, root, configPath);
    }

    // ---- config file ---------------------------------------------------------------------

    private static string? ResolveConfigPath(string? explicitConfigPath, string startDir)
    {
        if (!string.IsNullOrWhiteSpace(explicitConfigPath))
        {
            var full = Path.GetFullPath(explicitConfigPath);
            if (!File.Exists(full))
                throw new FileNotFoundException($"Config file not found: {full}");
            return full;
        }

        return FindUp(startDir, ConfigFileName);
    }

    private static ConfigDto ParseDto(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ConfigDto>(json, JsonOptions)
                   ?? throw new InvalidOperationException($"{ConfigFileName} is empty or null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Could not parse {Path.GetFileName(path)}: {ex.Message}", ex);
        }
    }

    // ---- solution root -------------------------------------------------------------------

    private static string ResolveRoot(string? explicitRoot, string? configPath, string startDir, IReadOnlyList<string> rootMarkers)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        // A config file anchors the root to its own folder.
        if (configPath is not null)
            return Path.GetDirectoryName(configPath)!;

        return FindRootByMarkers(startDir, rootMarkers)
            ?? throw new InvalidOperationException(
                $"Could not locate a solution root. Add a {ConfigFileName}, pass --root, or run inside a folder " +
                $"whose ancestor contains one of: {string.Join(", ", rootMarkers)}.");
    }

    private static string? FindRootByMarkers(string startDir, IReadOnlyList<string> markers)
    {
        var current = new DirectoryInfo(startDir);
        while (current is not null)
        {
            foreach (var marker in markers)
            {
                if (current.GetFiles(marker).Length > 0)
                    return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? InferBaseNamespace(string root, IReadOnlyList<string> markers)
    {
        var dir = new DirectoryInfo(root);
        foreach (var marker in markers)
        {
            var match = dir.GetFiles(marker).FirstOrDefault();
            if (match is not null)
                return Path.GetFileNameWithoutExtension(match.Name);
        }
        return null;
    }

    private static string? FindUp(string startDir, string fileName)
    {
        var current = new DirectoryInfo(startDir);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }
        return null;
    }

    // ---- merge ---------------------------------------------------------------------------

    private static GeneratorConfig BuildConfig(string baseNamespace, ConfigDto? dto, string? configPath, IReadOnlyList<string> rootMarkers)
    {
        var @default = GeneratorConfig.CreateDefault(baseNamespace);
        if (dto is null)
            return @default;

        var paths = new Dictionary<string, string>(GeneratorConfig.DefaultPaths, StringComparer.Ordinal);
        if (dto.Paths is not null)
        {
            foreach (var (key, value) in dto.Paths)
                paths[key] = value;
        }

        return @default with
        {
            RootMarkers = rootMarkers,
            TemplatesDirectory = ResolveTemplatesDirectory(dto.TemplatesDirectory, configPath),
            AppHostProject = FirstNonEmpty(dto.AppHostProject) ?? @default.AppHostProject,
            PagingProjectReference = FirstNonEmpty(dto.PagingProjectReference) ?? @default.PagingProjectReference,
            ExcludeTemplates = dto.ExcludeTemplates ?? [],
            ExcludeMutators = dto.ExcludeMutators ?? [],
            Paths = paths,
            Tokens = dto.Tokens is null
                ? @default.Tokens
                : new Dictionary<string, string>(dto.Tokens, StringComparer.Ordinal),
        };
    }

    private static string? ResolveTemplatesDirectory(string? configured, string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return null;
        if (Path.IsPathRooted(configured))
            return Path.GetFullPath(configured);

        var anchor = configPath is not null ? Path.GetDirectoryName(configPath)! : Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(anchor, configured));
    }

    private static string? FirstNonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>JSON shape of <c>chetogen.json</c>. Every field is optional and overrides a default.</summary>
    private sealed class ConfigDto
    {
        [JsonPropertyName("baseNamespace")] public string? BaseNamespace { get; set; }
        [JsonPropertyName("rootMarkers")] public List<string>? RootMarkers { get; set; }
        [JsonPropertyName("templatesDirectory")] public string? TemplatesDirectory { get; set; }
        [JsonPropertyName("appHostProject")] public string? AppHostProject { get; set; }
        [JsonPropertyName("pagingProjectReference")] public string? PagingProjectReference { get; set; }
        [JsonPropertyName("excludeTemplates")] public List<string>? ExcludeTemplates { get; set; }
        [JsonPropertyName("excludeMutators")] public List<string>? ExcludeMutators { get; set; }
        [JsonPropertyName("paths")] public Dictionary<string, string>? Paths { get; set; }
        [JsonPropertyName("tokens")] public Dictionary<string, string>? Tokens { get; set; }
    }
}
