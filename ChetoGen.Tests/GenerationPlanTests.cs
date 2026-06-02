using ChetoGen.Generator;

namespace ChetoGen.Tests;

public class GenerationPlanTests
{
    private static readonly PathResolver Paths = new(@"C:\fake\root", Specs.Config());

    [Fact]
    public void ClientModeWithoutBlazorOmitsFilterAndPages()
    {
        var plan = GenerationPlan.Build(
            Specs.Entity(props: [Specs.Prop("Name", "string", filter: true)], blazor: false, nav: false, mode: FilterMode.Client),
            Paths,
            Specs.Config());

        var templates = plan.Creations.Select(c => c.TemplateName).ToArray();

        templates.Should().NotContain("Application.Filter.scriban");
        templates.Should().NotContain(t => t.StartsWith("Client.Index", StringComparison.Ordinal));
        templates.Should().Contain("Domain.Entity.scriban");
        templates.Should().Contain("Client.ApiClient.scriban");
    }

    [Fact]
    public void ServerModeAddsFilterAndServerIndex()
    {
        var plan = GenerationPlan.Build(
            Specs.Entity(props: [Specs.Prop("Name", "string", filter: true)], blazor: true, nav: true, mode: FilterMode.Server),
            Paths,
            Specs.Config());

        var templates = plan.Creations.Select(c => c.TemplateName).ToArray();

        templates.Should().Contain("Application.Filter.scriban");
        templates.Should().Contain("Client.IndexServer.razor.scriban");
        templates.Should().NotContain("Client.Index.razor.scriban");
    }

    [Fact]
    public void NavMenuMutatorOnlyWhenBlazorAndNav()
    {
        SharedFileNames(GenerationPlan.Build(Specs.Entity(blazor: true, nav: true), Paths, Specs.Config())).Should().Contain("NavMenu.razor");
        SharedFileNames(GenerationPlan.Build(Specs.Entity(blazor: true, nav: false), Paths, Specs.Config())).Should().NotContain("NavMenu.razor");
        SharedFileNames(GenerationPlan.Build(Specs.Entity(blazor: false, nav: true), Paths, Specs.Config())).Should().NotContain("NavMenu.razor");
    }

    [Fact]
    public void ServerModeAddsPagingProjectReferenceMutator()
    {
        // Application.Models.csproj is only patched (Domain.Paging reference) in server mode.
        SharedFileNames(GenerationPlan.Build(Specs.Entity(mode: FilterMode.Client), Paths, Specs.Config()))
            .Should().NotContain("AspireApp.Application.Models.csproj");
        SharedFileNames(GenerationPlan.Build(Specs.Entity(mode: FilterMode.Server), Paths, Specs.Config()))
            .Should().Contain("AspireApp.Application.Models.csproj");
    }

    [Fact]
    public void ExcludeTemplatesTrimsTheSlice()
    {
        var config = Specs.Config() with { ExcludeTemplates = ["Client.Edit.razor", "Client.Edit.razor.cs"] };
        var plan = GenerationPlan.Build(Specs.Entity(blazor: true), Paths, config);

        plan.Creations.Select(c => c.FriendlyLabel)
            .Should().NotContain("Client.Edit.razor")
            .And.Contain("Domain.Entity");
    }

    [Fact]
    public void ExcludeMutatorsDropsSharedFilePatches()
    {
        var config = Specs.Config() with
        {
            ExcludeMutators = ["DbContext", "DataAccessDI", "ApplicationDI", "MappersDI", "ClientProgram", "NavMenu"],
        };

        GenerationPlan.Build(Specs.Entity(blazor: true, nav: true), Paths, config)
            .Mutators.Should().BeEmpty();
    }

    private static IEnumerable<string> SharedFileNames(GenerationPlan plan) =>
        plan.Mutators.Select(m => Path.GetFileName(m.TargetPath));
}
