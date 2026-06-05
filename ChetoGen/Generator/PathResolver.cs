using ChetoGen.Configuration;

namespace ChetoGen.Generator;

/// <summary>
/// Single owner of "where does file X for entity Y live?". Every target path flows through here;
/// there is no path-string math anywhere else. Layout is fully driven by <see cref="GeneratorConfig.Paths"/>,
/// so the same generator targets any solution by swapping the config.
/// </summary>
internal sealed class PathResolver
{
    private readonly GeneratorConfig _config;

    public string SolutionRoot { get; }

    public PathResolver(string solutionRoot, GeneratorConfig config)
    {
        SolutionRoot = solutionRoot ?? throw new ArgumentNullException(nameof(solutionRoot));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Absolute path for a logical key, expanding {BaseNamespace}/{Entity} and normalizing separators.</summary>
    public string Resolve(string key, string entity = "")
    {
        if (!_config.Paths.TryGetValue(key, out var template))
            throw new InvalidOperationException(
                $"No path configured for '{key}'. Add it under \"paths\" in {ConfigLoader.ConfigFileName}.");

        var rel = _config.Expand(template, entity).Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(SolutionRoot, rel);
    }

    public string DomainEntity(string entity) => Resolve(PathKeys.DomainEntity, entity);
    public string ApplicationModel(string entity) => Resolve(PathKeys.ApplicationModel, entity);
    public string ApplicationFilter(string entity) => Resolve(PathKeys.ApplicationFilter, entity);
    public string ApplicationContract(string entity) => Resolve(PathKeys.ApplicationContract, entity);
    public string ApplicationService(string entity) => Resolve(PathKeys.ApplicationService, entity);
    public string ApplicationMapper(string entity) => Resolve(PathKeys.ApplicationMapper, entity);
    public string ApplicationPersistence(string entity) => Resolve(PathKeys.ApplicationPersistence, entity);
    public string DataAccess(string entity) => Resolve(PathKeys.DataAccess, entity);
    public string ApiController(string entity) => Resolve(PathKeys.ApiController, entity);
    public string ApiClient(string entity) => Resolve(PathKeys.ApiClient, entity);
    public string BlazorIndexRazor(string entity) => Resolve(PathKeys.BlazorIndexRazor, entity);
    public string BlazorIndexCs(string entity) => Resolve(PathKeys.BlazorIndexCs, entity);
    public string BlazorEditRazor(string entity) => Resolve(PathKeys.BlazorEditRazor, entity);
    public string BlazorEditCs(string entity) => Resolve(PathKeys.BlazorEditCs, entity);

    public string ApplicationPaging => Resolve(PathKeys.ApplicationPaging);
    public string AppDbContext => Resolve(PathKeys.AppDbContext);
    public string DataAccessDI => Resolve(PathKeys.DataAccessDI);
    public string ApplicationDI => Resolve(PathKeys.ApplicationDI);
    public string MappersDI => Resolve(PathKeys.MappersDI);
    public string ClientProgram => Resolve(PathKeys.ClientProgram);
    public string NavMenu => Resolve(PathKeys.NavMenu);
}
