using System.Text.Json;
using ChetoGen.Commands;
using ChetoGen.Configuration;
using ChetoGen.Generator;

namespace ChetoGen.Tests;

public class InitCommandTests
{
    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Fact]
    public void BuildConfigJsonIsParseableAndCarriesChoices()
    {
        var plan = new InitCommand.InitPlan(
            BaseNamespace: "Acme",
            CopyTemplates: true,
            ExcludeTemplates: ["Api.Controller", "Client.ApiClient"],
            ExcludeMutators: ["NavMenu"],
            AppHostProject: "Acme.AppHost",
            CustomPaths: true,
            CustomArchitecture: true);

        using var doc = JsonDocument.Parse(InitCommand.BuildConfigJson(plan), Lenient);
        var root = doc.RootElement;

        root.GetProperty("baseNamespace").GetString().Should().Be("Acme");
        root.GetProperty("templatesDirectory").GetString().Should().Be(InitCommand.TemplatesFolderName);
        root.GetProperty("appHostProject").GetString().Should().Be("Acme.AppHost");
        root.GetProperty("excludeTemplates").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["Api.Controller", "Client.ApiClient"]);
        root.GetProperty("excludeMutators").EnumerateArray().Select(e => e.GetString())
            .Should().ContainSingle().Which.Should().Be("NavMenu");
        // CustomPaths emits the full, editable paths block (sourced from DefaultPaths).
        root.GetProperty("paths").GetProperty("ApplicationPaging").GetString().Should().Contain("Paging.cs");
        // CustomArchitecture emits the full, editable architecture block (sourced from DefaultArchitecture),
        // and the embedded newline in a usings value survives JSON escaping.
        root.GetProperty("architecture").GetProperty("EntityBase").GetString().Should().Be(" : BaseEntity<{Id}>");
        root.GetProperty("architecture").GetProperty("EntityUsings").GetString()
            .Should().Be("using {BaseNamespace}.Domain.Entities.Base;\n");
    }

    [Fact]
    public void DefaultsPlanOmitsTemplatesAndPathsBlocks()
    {
        var plan = new InitCommand.InitPlan("Demo", CopyTemplates: false,
            ExcludeTemplates: [], ExcludeMutators: [], AppHostProject: "Demo.AppHost", CustomPaths: false);

        using var doc = JsonDocument.Parse(InitCommand.BuildConfigJson(plan), Lenient);
        var root = doc.RootElement;

        root.TryGetProperty("templatesDirectory", out _).Should().BeFalse();
        root.TryGetProperty("paths", out _).Should().BeFalse();
        root.TryGetProperty("architecture", out _).Should().BeFalse();
        root.GetProperty("excludeTemplates").GetArrayLength().Should().Be(0);
        root.GetProperty("excludeMutators").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void GeneratedConfigRoundTripsThroughLoader()
    {
        var dir = Path.Combine(Path.GetTempPath(), "chetogen-init-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Ignored.slnx"), "<Solution />");

            var plan = new InitCommand.InitPlan("Shop", CopyTemplates: false,
                ExcludeTemplates: ["Client.Edit.razor"], ExcludeMutators: ["NavMenu"],
                AppHostProject: "Shop.AppHost", CustomPaths: false);
            File.WriteAllText(Path.Combine(dir, ConfigLoader.ConfigFileName), InitCommand.BuildConfigJson(plan));

            var loaded = ConfigLoader.Load(startDirectory: dir);

            // baseNamespace from the file wins over the inferred "Ignored".
            loaded.Config.BaseNamespace.Should().Be("Shop");
            loaded.Config.IsExcluded("Client.Edit.razor").Should().BeTrue();
            loaded.Config.IsMutatorExcluded("NavMenu").Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void CustomArchitectureBlockRoundTripsToTheBuiltInSeams()
    {
        var dir = Path.Combine(Path.GetTempPath(), "chetogen-init-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Shop.slnx"), "<Solution />");

            var plan = new InitCommand.InitPlan("Shop", CopyTemplates: false,
                ExcludeTemplates: [], ExcludeMutators: [], AppHostProject: null,
                CustomPaths: false, CustomArchitecture: true);
            File.WriteAllText(Path.Combine(dir, ConfigLoader.ConfigFileName), InitCommand.BuildConfigJson(plan));

            var loaded = ConfigLoader.Load(startDirectory: dir);
            var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", idType: "long"), loaded.Config);

            // The block is the defaults dumped verbatim, so it must reproduce the built-in seams exactly.
            tokens["ARCH_ENTITY_USINGS"].Should().Be("using Shop.Domain.Entities.Base;\n");
            tokens["ARCH_ENTITY_BASE"].Should().Be(" : BaseEntity<long>");
            tokens["ARCH_CONTROLLER_BASE"].Should().Be(": BaseController<Order, long, IOrderService>(orderService, logger)");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
