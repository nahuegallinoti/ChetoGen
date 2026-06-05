using ChetoGen.Configuration;
using ChetoGen.Generator;

namespace ChetoGen.Tests;

/// <summary>
/// The architecture seams (<c>{{ARCH_*}}</c> tokens) are what let one template set target any
/// architecture. These lock the built-in Clean Architecture defaults (the happy path must never
/// drift) and prove a consumer can retarget base types / the ROP wrapper purely via config.
/// </summary>
public class ArchitectureConfigTests
{
    // ---- defaults: the happy path must stay exactly as shipped ----------------------------

    [Fact]
    public void DefaultArchitectureProducesBuiltInCleanArchitectureSeams()
    {
        var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", idType: "long"), Specs.Config("Acme"));

        tokens["ARCH_ENTITY_USINGS"].Should().Be("using Acme.Domain.Entities.Base;\n");
        tokens["ARCH_ENTITY_BASE"].Should().Be(" : BaseEntity<long>");
        tokens["ARCH_MODEL_BASE"].Should().Be(" : BaseModel<long>");
        tokens["ARCH_MAPPER_BASE"].Should().Be(" : BaseMapper<OrderModel, OrderEntity>");
        tokens["ARCH_DATAACCESS_BASE"].Should().Be(" : BaseDA<Order, long>(context), IOrderDA");
        tokens["ARCH_RESULT"].Should().Be("Result");
        tokens["ARCH_RESULT_OK"].Should().Be("Success");
        tokens["ARCH_RESULT_VALUE"].Should().Be("Value");
        tokens["ARCH_RESULT_ERRORS"].Should().Be("Errors.FormatErrorMessages()");
        tokens["ARCH_CONTROLLER_BASE"].Should().Be(": BaseController<Order, long, IOrderService>(orderService, logger)");
    }

    // ---- placeholder expansion -------------------------------------------------------------

    [Fact]
    public void ArchitecturePlaceholdersExpandAgainstEntityAndConfig()
    {
        var config = Specs.Config("Acme") with
        {
            Architecture = new Dictionary<string, string>(GeneratorConfig.DefaultArchitecture)
            {
                ["EntityBase"] = " : Foo<{Entity}, {Id}, {EntityCamel}, {BaseNamespace}>",
            },
        };

        TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", idType: "long"), config)["ARCH_ENTITY_BASE"]
            .Should().Be(" : Foo<Order, long, order, Acme>");
    }

    [Fact]
    public void EventBusPlaceholdersToggleInsideArchitectureValues()
    {
        // The controller seam carries the event-bus ctor param / base arg via {EventBus*} placeholders.
        var on = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", eventBus: true), Specs.Config("Acme"));
        on["ARCH_CONTROLLER_CTOR"].Should().Contain(", IMessageBus messageBus");
        on["ARCH_CONTROLLER_BASE"].Should().Contain("(orderService, messageBus, logger)");

        var off = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", eventBus: false), Specs.Config("Acme"));
        off["ARCH_CONTROLLER_CTOR"].Should().NotContain("IMessageBus");
        off["ARCH_CONTROLLER_BASE"].Should().Be(": BaseController<Order, long, IOrderService>(orderService, logger)");
    }

    // ---- overrides change output -----------------------------------------------------------

    [Fact]
    public void OverridingArchitectureKeysRetargetsTheSeams()
    {
        var config = Specs.Config("Acme") with
        {
            Architecture = new Dictionary<string, string>(GeneratorConfig.DefaultArchitecture)
            {
                ["ModelBase"] = "",                 // plain models, no base class
                ["ResultType"] = "Either",          // a differently-named ROP wrapper
                ["ResultIsSuccess"] = "IsRight",
                ["ResultValue"] = "Right",
            },
        };

        var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order"), config);
        tokens["ARCH_MODEL_BASE"].Should().BeEmpty();
        tokens["ARCH_RESULT"].Should().Be("Either");
        tokens["ARCH_RESULT_OK"].Should().Be("IsRight");
        tokens["ARCH_RESULT_VALUE"].Should().Be("Right");
        // a non-overridden key keeps the default
        tokens["ARCH_ENTITY_BASE"].Should().Be(" : BaseEntity<long>");
    }

    // ---- end-to-end render: a "flat" architecture with no base types ----------------------

    [Fact]
    public void FlatArchitectureRendersPlainEntityWithoutBaseTypeOrBaseUsing()
    {
        var config = Specs.Config("Acme") with
        {
            Architecture = new Dictionary<string, string>(GeneratorConfig.DefaultArchitecture)
            {
                ["EntityUsings"] = "",
                ["EntityBase"] = "",
            },
        };
        var renderer = new TemplateRenderer(config);
        var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order"), config);

        var rendered = renderer.Render("Domain.Entity.scriban", tokens);

        rendered.Should().Contain("public class Order\n{");
        rendered.Should().NotContain("BaseEntity");
        rendered.Should().NotContain("Domain.Entities.Base");
    }

    [Fact]
    public void DefaultArchitectureRendersEntityOnBaseType()
    {
        var config = Specs.Config("Acme");
        var renderer = new TemplateRenderer(config);
        var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", idType: "long"), config);

        var rendered = renderer.Render("Domain.Entity.scriban", tokens);

        rendered.Should().Contain("using Acme.Domain.Entities.Base;");
        rendered.Should().Contain("public class Order : BaseEntity<long>");
    }

    // ---- loader merge: a partial "architecture" block falls back to defaults ---------------

    [Fact]
    public void LoaderMergesArchitectureOverrideKeepingDefaultsForUnspecifiedKeys() => WithTempDir(dir =>
    {
        File.WriteAllText(Path.Combine(dir, "Shop.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(dir, ConfigLoader.ConfigFileName), """
        {
            "architecture": {
                "ModelBase": "",
                "ResultType": "Outcome"
            }
        }
        """);

        var loaded = ConfigLoader.Load(startDirectory: dir);
        var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", idType: "long"), loaded.Config);

        // overridden
        tokens["ARCH_MODEL_BASE"].Should().BeEmpty();
        tokens["ARCH_RESULT"].Should().Be("Outcome");
        // not overridden → still the built-in default
        tokens["ARCH_ENTITY_BASE"].Should().Be(" : BaseEntity<long>");
        tokens["ARCH_RESULT_OK"].Should().Be("Success");
    });

    // ---- helpers ---------------------------------------------------------------------------

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
