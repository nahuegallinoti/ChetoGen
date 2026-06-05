namespace ChetoGen.Configuration;

/// <summary>
/// Logical keys for every path the generator can write or patch. A consumer's
/// <c>chetogen.json</c> overrides these by key; <see cref="GeneratorConfig.DefaultPaths"/>
/// supplies the built-in layout (the AspireApp convention <c>{BaseNamespace}.&lt;Layer&gt;</c>).
/// </summary>
internal static class PathKeys
{
    public const string DomainEntity = "DomainEntity";
    public const string ApplicationModel = "ApplicationModel";
    public const string ApplicationFilter = "ApplicationFilter";
    public const string ApplicationPaging = "ApplicationPaging";
    public const string ApplicationContract = "ApplicationContract";
    public const string ApplicationService = "ApplicationService";
    public const string ApplicationMapper = "ApplicationMapper";
    public const string ApplicationPersistence = "ApplicationPersistence";
    public const string DataAccess = "DataAccess";
    public const string ApiController = "ApiController";
    public const string ApiClient = "ApiClient";
    public const string BlazorIndexRazor = "BlazorIndexRazor";
    public const string BlazorIndexCs = "BlazorIndexCs";
    public const string BlazorEditRazor = "BlazorEditRazor";
    public const string BlazorEditCs = "BlazorEditCs";
    public const string AppDbContext = "AppDbContext";
    public const string DataAccessDI = "DataAccessDI";
    public const string ApplicationDI = "ApplicationDI";
    public const string MappersDI = "MappersDI";
    public const string ClientProgram = "ClientProgram";
    public const string NavMenu = "NavMenu";
}

/// <summary>
/// Everything the generator needs to target a *specific* consumer solution without any
/// hard-coded names. Loaded (and merged over the defaults) by <see cref="ConfigLoader"/>.
/// Placeholders <c>{BaseNamespace}</c> and <c>{Entity}</c> are expanded via <see cref="Expand"/>.
/// </summary>
internal sealed record GeneratorConfig
{
    /// <summary>Namespace/project prefix of the consumer solution (e.g. "AspireApp"). Replaces {BaseNamespace} everywhere.</summary>
    public required string BaseNamespace { get; init; }

    /// <summary>Glob patterns used to locate the solution root by walking up from the working directory.</summary>
    public IReadOnlyList<string> RootMarkers { get; init; } = ["*.slnx", "*.sln"];

    /// <summary>Optional folder where the consumer keeps overriding/added templates. Looked up before the built-in set.</summary>
    public string? TemplatesDirectory { get; init; }

    /// <summary>AppHost project used only for the "next steps" run hint.</summary>
    public string AppHostProject { get; init; } = "{BaseNamespace}.AppHost";

    /// <summary>Friendly labels of pipeline steps to skip (e.g. "Client.Edit.razor"). Lets a consumer trim the generated slice.</summary>
    public IReadOnlyList<string> ExcludeTemplates { get; init; } = [];

    /// <summary>
    /// Keys of shared-file mutators to skip. The built-in mutators assume the default layout
    /// (AppDbContext, DI files, NavMenu); a different architecture excludes the ones it lacks.
    /// Keys: DbContext, DataAccessDI, ApplicationDI, MappersDI, ClientProgram, NavMenu.
    /// </summary>
    public IReadOnlyList<string> ExcludeMutators { get; init; } = [];

    /// <summary>Logical-key → relative-path map. Placeholders {BaseNamespace} and {Entity}; '/' is normalized per-OS.</summary>
    public IReadOnlyDictionary<string, string> Paths { get; init; } = DefaultPaths;

    /// <summary>Extra static <c>{{TOKEN}}</c> overrides merged into the template token map last (so they win).</summary>
    public IReadOnlyDictionary<string, string> Tokens { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The base types / wrappers the generated code sits on (base classes, base interfaces, the ROP
    /// result wrapper, the cache). Defaults reproduce the built-in Clean Architecture layout; override
    /// any key under <c>"architecture"</c> to target a different architecture <b>without editing templates</b>.
    /// Values support the placeholders <c>{BaseNamespace}</c>, <c>{Entity}</c>, <c>{EntityCamel}</c>,
    /// <c>{Id}</c>, <c>{EventBusCtorParam}</c>, <c>{EventBusBaseArg}</c>. See <see cref="DefaultArchitecture"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Architecture { get; init; } = DefaultArchitecture;

    public static GeneratorConfig CreateDefault(string baseNamespace) =>
        new() { BaseNamespace = baseNamespace };

    /// <summary>Expands {BaseNamespace} and {Entity} placeholders against this config.</summary>
    public string Expand(string template, string entity = "") =>
        template
            .Replace("{BaseNamespace}", BaseNamespace, StringComparison.Ordinal)
            .Replace("{Entity}", entity, StringComparison.Ordinal);

    /// <summary>True when the pipeline step with this friendly label should be skipped.</summary>
    public bool IsExcluded(string friendlyLabel) =>
        ExcludeTemplates.Contains(friendlyLabel, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the shared-file mutator with this key should be skipped.</summary>
    public bool IsMutatorExcluded(string key) =>
        ExcludeMutators.Contains(key, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, string> DefaultPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PathKeys.DomainEntity]            = "{BaseNamespace}.Domain.Entities/{Entity}.cs",
            [PathKeys.ApplicationModel]        = "{BaseNamespace}.Application.Models/App/{Entity}.cs",
            [PathKeys.ApplicationFilter]       = "{BaseNamespace}.Application.Models/App/{Entity}Filter.cs",
            [PathKeys.ApplicationPaging]       = "{BaseNamespace}.Application.Models/Paging.cs",
            [PathKeys.ApplicationContract]     = "{BaseNamespace}.Application.Contracts/{Entity}/I{Entity}Service.cs",
            [PathKeys.ApplicationService]      = "{BaseNamespace}.Application.Implementations/{Entity}/{Entity}Service.cs",
            [PathKeys.ApplicationMapper]       = "{BaseNamespace}.Application.Mappers/{Entity}Mapper.cs",
            [PathKeys.ApplicationPersistence]  = "{BaseNamespace}.Application.Persistence/I{Entity}DA.cs",
            [PathKeys.DataAccess]              = "{BaseNamespace}.DataAccess.Implementations/{Entity}DA.cs",
            [PathKeys.ApiController]           = "{BaseNamespace}.Api/Controllers/{Entity}Controller.cs",
            [PathKeys.ApiClient]               = "{BaseNamespace}.Client.ApiClients/{Entity}ApiClient.cs",
            [PathKeys.BlazorIndexRazor]        = "{BaseNamespace}.Client/Components/Pages/{Entity}Index.razor",
            [PathKeys.BlazorIndexCs]           = "{BaseNamespace}.Client/Components/Pages/{Entity}Index.razor.cs",
            [PathKeys.BlazorEditRazor]         = "{BaseNamespace}.Client/Components/Pages/{Entity}Edit.razor",
            [PathKeys.BlazorEditCs]            = "{BaseNamespace}.Client/Components/Pages/{Entity}Edit.razor.cs",
            [PathKeys.AppDbContext]            = "{BaseNamespace}.DataAccess.Implementations/AppDbContext.cs",
            [PathKeys.DataAccessDI]            = "{BaseNamespace}.DataAccess.Implementations/DependencyInjection.cs",
            [PathKeys.ApplicationDI]           = "{BaseNamespace}.Application.Implementations/DependencyInjection.cs",
            [PathKeys.MappersDI]               = "{BaseNamespace}.Application.Mappers/DependencyInjection.cs",
            [PathKeys.ClientProgram]           = "{BaseNamespace}.Client/Program.cs",
            [PathKeys.NavMenu]                 = "{BaseNamespace}.Client/Components/Layout/NavMenu.razor",
        };

    /// <summary>
    /// Built-in architecture seams (the Clean Architecture reference layout). Every value feeds an
    /// <c>{{ARCH_*}}</c> template token after placeholder expansion. A consumer overrides only the
    /// keys their architecture differs on (e.g. set <c>"ModelBase"</c> to <c>""</c> for plain models,
    /// or remap <c>"ResultType"</c> if their ROP wrapper is named differently).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultArchitecture =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EntityUsings"]              = "using {BaseNamespace}.Domain.Entities.Base;\n",
            ["EntityBase"]                = " : BaseEntity<{Id}>",
            ["ModelBase"]                 = " : BaseModel<{Id}>",
            ["MapperBase"]                = " : BaseMapper<{Entity}Model, {Entity}Entity>",
            ["ServiceContractUsings"]     = "using {BaseNamespace}.Application.Contracts.Base;\n",
            ["ServiceContractBase"]       = " : IBaseService<Models.App.{Entity}, {Id}>",
            ["PersistenceContractUsings"] = "using {BaseNamespace}.Application.Persistence.Base;\n",
            ["PersistenceContractBase"]   = " : IBaseDA<{Entity}, {Id}>",
            ["ServiceImplUsings"]         = "using {BaseNamespace}.Application.Implementations.Base;\n",
            ["CacheUsing"]                = "using Microsoft.Extensions.Caching.Hybrid;\n",
            ["ServiceCtor"]               = "I{Entity}DA {EntityCamel}DA, {Entity}Mapper mapper, HybridCache hybridCache",
            ["ServiceBase"]               = ": BaseService<{Entity}Entity, {Entity}Model, {Id}>({EntityCamel}DA, mapper, hybridCache), I{Entity}Service",
            ["DataAccessUsing"]           = "using {BaseNamespace}.DataAccess.Implementations.Base;\n",
            ["DataAccessCtor"]            = "AppDbContext context",
            ["DataAccessBase"]            = " : BaseDA<{Entity}, {Id}>(context), I{Entity}DA",
            ["ControllerCtor"]            = "I{Entity}Service {EntityCamel}Service{EventBusCtorParam}, ILogger<{Entity}Controller> logger",
            ["ControllerBase"]            = ": BaseController<{Entity}, {Id}, I{Entity}Service>({EntityCamel}Service{EventBusBaseArg}, logger)",
            ["ApiClientUsing"]            = "using {BaseNamespace}.Domain.ROP;\n",
            ["ApiClientCtor"]             = "IHttpClientFactory httpClientFactory",
            ["ApiClientBase"]             = ": BaseApiClient(httpClientFactory, HttpClientNames.Api)",
            ["ResultType"]                = "Result",
            ["ResultIsSuccess"]           = "Success",
            ["ResultValue"]               = "Value",
            ["ResultErrors"]              = "Errors.FormatErrorMessages()",
        };
}
