using ChetoGen.Configuration;
using ChetoGen.Generator;

namespace ChetoGen.Tests;

public class ConfigTests
{
    // ---- token / namespace agnosticism ---------------------------------------------------

    [Fact]
    public void BuildTokensEmitsConfiguredBaseNamespace() =>
        TemplateRenderer.BuildTokens(Specs.Entity(), Specs.Config("Acme"))["BASE_NS"].Should().Be("Acme");

    [Fact]
    public void ServerUsingsUseConfiguredNamespace()
    {
        var tokens = TemplateRenderer.BuildTokens(
            Specs.Entity(props: [Specs.Prop("Name", "string", filter: true)], mode: FilterMode.Server),
            Specs.Config("Acme"));

        // Paging is self-contained now — nothing references an external Domain.Paging project.
        tokens["PERSISTENCE_USINGS"].Should().NotContain("Domain.Paging");
        tokens["SERVICE_USINGS"].Should().Contain("using Acme.Application.Models;");
        tokens["SERVICE_USINGS"].Should().Contain("using Acme.Application.Models.App;");
        tokens["API_CLIENT_EXTRA_USINGS"].Should().Contain("using Acme.Application.Models;");
    }

    [Fact]
    public void ConfigTokensOverrideBuiltIns()
    {
        var config = Specs.Config("Acme") with
        {
            Tokens = new Dictionary<string, string> { ["BASE_NS"] = "Overridden" },
        };

        TemplateRenderer.BuildTokens(Specs.Entity(), config)["BASE_NS"].Should().Be("Overridden");
    }

    // ---- path resolution -----------------------------------------------------------------

    [Fact]
    public void PathResolverExpandsBaseNamespaceAndEntity()
    {
        var resolver = new PathResolver(@"C:\root", Specs.Config("Acme"));

        Normalize(resolver.DomainEntity("Order")).Should().EndWith("Acme.Domain.Entities/Order.cs");
    }

    [Fact]
    public void PathResolverHonorsConfiguredPathOverride()
    {
        var config = Specs.Config("Acme") with
        {
            Paths = new Dictionary<string, string>(GeneratorConfig.DefaultPaths)
            {
                [PathKeys.DomainEntity] = "Custom/Models/{Entity}.entity.cs",
            },
        };

        Normalize(new PathResolver(@"C:\root", config).DomainEntity("Order"))
            .Should().EndWith("Custom/Models/Order.entity.cs");
    }

    // ---- loader --------------------------------------------------------------------------

    [Fact]
    public void LoaderInfersBaseNamespaceFromSolutionFile() => WithTempDir(dir =>
    {
        File.WriteAllText(Path.Combine(dir, "Contoso.slnx"), "<Solution />");

        var loaded = ConfigLoader.Load(startDirectory: dir);

        loaded.Config.BaseNamespace.Should().Be("Contoso");
        loaded.ConfigFilePath.Should().BeNull();
        loaded.SolutionRoot.Should().Be(dir);
    });

    [Fact]
    public void LoaderReadsConfigFileAndMergesPathOverride() => WithTempDir(dir =>
    {
        File.WriteAllText(Path.Combine(dir, "Whatever.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(dir, ConfigLoader.ConfigFileName), """
        {
            // base namespace wins over the inferred "Whatever"
            "baseNamespace": "Shop",
            "paths": { "DomainEntity": "Domain/{Entity}.cs" }
        }
        """);

        var loaded = ConfigLoader.Load(startDirectory: dir);

        loaded.Config.BaseNamespace.Should().Be("Shop");
        loaded.ConfigFilePath.Should().NotBeNull();

        var resolver = new PathResolver(loaded.SolutionRoot, loaded.Config);
        Normalize(resolver.DomainEntity("Order")).Should().EndWith("Domain/Order.cs");
        // a non-overridden key still falls back to the default layout
        Normalize(resolver.ApplicationMapper("Order")).Should().EndWith("Shop.Application.Mappers/OrderMapper.cs");
    });

    [Fact]
    public void LoaderDiscoversConfigFromExplicitRoot() => WithTempDir(dir =>
    {
        File.WriteAllText(Path.Combine(dir, "Whatever.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(dir, ConfigLoader.ConfigFileName), """
        { "baseNamespace": "Anchored" }
        """);

        // No startDirectory passed: discovery must follow --root (explicitRoot), not the test's CWD.
        var loaded = ConfigLoader.Load(explicitRoot: dir);

        loaded.Config.BaseNamespace.Should().Be("Anchored");
        loaded.ConfigFilePath.Should().NotBeNull();
        loaded.SolutionRoot.Should().Be(dir);
    });

    // ---- template override ---------------------------------------------------------------

    [Fact]
    public void RendererPrefersOverrideTemplateDirectory() => WithTempDir(dir =>
    {
        File.WriteAllText(Path.Combine(dir, "Domain.Entity.scriban"), "OVERRIDE {{ENTITY}}");

        var config = Specs.Config("Acme") with { TemplatesDirectory = dir };
        var renderer = new TemplateRenderer(config);
        var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order"), config);

        renderer.Render("Domain.Entity.scriban", tokens).Should().Be("OVERRIDE Order");
    });

    // ---- helpers -------------------------------------------------------------------------

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static void WithTempDir(Action<string> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "chetogen-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            body(dir);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
