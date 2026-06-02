using ChetoGen.Configuration;
using ChetoGen.Generator.Mutators;

namespace ChetoGen.Generator;

internal sealed record FileCreation(string TargetPath, string TemplateName, string FriendlyLabel);

internal sealed class GenerationPlan
{
    public required IReadOnlyList<FileCreation> Creations { get; init; }
    public required IReadOnlyList<IFileMutator> Mutators { get; init; }

    public static GenerationPlan Build(EntitySpec entity, PathResolver paths, GeneratorConfig config)
    {
        // Consumer namespace prefix — every generated "using" of the target solution flows from here.
        var ns = config.BaseNamespace;

        var creations = new List<FileCreation>
        {
            new(paths.DomainEntity(entity.Name), "Domain.Entity.scriban", "Domain.Entity"),
            new(paths.ApplicationModel(entity.Name), "Application.Model.scriban", "Application.Model"),
            new(paths.ApplicationContract(entity.Name), "Application.Contract.scriban", "Application.Contract"),
            new(paths.ApplicationService(entity.Name), "Application.Service.scriban", "Application.Service"),
            new(paths.ApplicationMapper(entity.Name), "Application.Mapper.scriban", "Application.Mapper"),
            new(paths.ApplicationPersistence(entity.Name), "Application.Persistence.scriban", "Application.Persistence"),
            new(paths.DataAccess(entity.Name), "DataAccess.scriban", "DataAccess"),
            new(paths.ApiController(entity.Name), "Api.Controller.scriban", "Api.Controller"),
            new(paths.ApiClient(entity.Name), "Client.ApiClient.scriban", "Client.ApiClient"),
        };

        if (entity.IsServerFiltering)
        {
            creations.Add(new(paths.ApplicationFilter(entity.Name), "Application.Filter.scriban", "Application.Filter"));
        }

        if (entity.GenerateBlazorPage)
        {
            var indexRazor = entity.IsServerFiltering ? "Client.IndexServer.razor.scriban" : "Client.Index.razor.scriban";
            var indexCs = entity.IsServerFiltering ? "Client.IndexServer.razor.cs.scriban" : "Client.Index.razor.cs.scriban";

            creations.Add(new(paths.BlazorIndexRazor(entity.Name), indexRazor, "Client.Index.razor"));
            creations.Add(new(paths.BlazorIndexCs(entity.Name), indexCs, "Client.Index.razor.cs"));
            creations.Add(new(paths.BlazorEditRazor(entity.Name), "Client.Edit.razor.scriban", "Client.Edit.razor"));
            creations.Add(new(paths.BlazorEditCs(entity.Name), "Client.Edit.razor.cs.scriban", "Client.Edit.razor.cs"));
        }

        // Each shared-file mutator carries a stable key so a consumer can drop the ones its
        // architecture lacks via "excludeMutators" (a classic MVC app has none of these).
        var keyed = new List<(string Key, IFileMutator Mutator)>();

        if (entity.IsServerFiltering)
        {
            keyed.Add(("ModelsCsproj", new CsprojReferenceMutator(
                paths.ApplicationModelsCsproj,
                config.Expand(config.PagingProjectReference))));
        }

        keyed.Add(("DbContext", new DbContextMutator(paths.AppDbContext)));
        keyed.Add(("DataAccessDI", new DiRegistrationMutator(
            paths.DataAccessDI,
            usingLines: [],
            registrationLine: $"        services.AddScoped<I{entity.Name}DA, {entity.Name}DA>();",
            markerForLastRegistration: "AddScoped<I")));
        keyed.Add(("ApplicationDI", new DiRegistrationMutator(
            paths.ApplicationDI,
            usingLines:
            [
                $"using {ns}.Application.Contracts.{entity.Name};",
                $"using {ns}.Application.Implementations.{entity.Name};",
            ],
            registrationLine: $"        services.AddScoped<I{entity.Name}Service, {entity.Name}Service>();",
            markerForLastRegistration: "services.AddScoped<I")));
        keyed.Add(("MappersDI", new DiRegistrationMutator(
            paths.MappersDI,
            usingLines: [],
            registrationLine: $"        services.AddSingleton<{entity.Name}Mapper>();",
            markerForLastRegistration: "AddSingleton<")));
        keyed.Add(("ClientProgram", new DiRegistrationMutator(
            paths.ClientProgram,
            usingLines: [],
            registrationLine: $"builder.Services.AddScoped<{entity.Name}ApiClient>();",
            markerForLastRegistration: "builder.Services.AddScoped<")));

        if (entity.GenerateBlazorPage && entity.RegisterInNavMenu)
            keyed.Add(("NavMenu", new NavMenuMutator(paths.NavMenu)));

        // Consumers trim creations via "excludeTemplates" (friendly label) and shared-file
        // patches via "excludeMutators" (key).
        var keptCreations = creations.Where(c => !config.IsExcluded(c.FriendlyLabel)).ToList();
        var keptMutators = keyed.Where(x => !config.IsMutatorExcluded(x.Key)).Select(x => x.Mutator).ToList();

        return new GenerationPlan
        {
            Creations = keptCreations,
            Mutators = keptMutators,
        };
    }
}
