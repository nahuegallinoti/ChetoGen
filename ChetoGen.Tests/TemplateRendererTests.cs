using ChetoGen.Generator;

namespace ChetoGen.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void BuildTokensEmitsScalarIdentityTokens()
    {
        var tokens = TemplateRenderer.BuildTokens(Specs.Entity(name: "Order", idType: "long"), Specs.Config());

        tokens["ENTITY"].Should().Be("Order");
        tokens["entity"].Should().Be("order");
        tokens["ENTITY_PLURAL"].Should().Be("Orders");
        tokens["ID_TYPE"].Should().Be("long");
    }

    [Theory]
    [InlineData("long", ":long")]
    [InlineData("int", ":int")]
    [InlineData("Guid", ":guid")]
    public void BuildTokensMapsIdRouteConstraint(string idType, string expected) =>
        TemplateRenderer.BuildTokens(Specs.Entity(idType: idType), Specs.Config())["ID_ROUTE_CONSTRAINT"].Should().Be(expected);

    [Fact]
    public void AuthorizeTokensToggleWithRequireAuth()
    {
        TemplateRenderer.BuildTokens(Specs.Entity(auth: true), Specs.Config())["AUTHORIZE_ATTR"].Should().Contain("[Authorize]");
        TemplateRenderer.BuildTokens(Specs.Entity(auth: false), Specs.Config())["AUTHORIZE_ATTR"].Should().BeEmpty();
    }

    [Fact]
    public void EventBusTokensToggle()
    {
        var on = TemplateRenderer.BuildTokens(Specs.Entity(eventBus: true), Specs.Config());
        on["EVENT_BUS_CTOR_PARAM"].Should().Contain("IMessageBus");
        on["EVENT_BUS_BASE_ARG"].Should().Contain("messageBus");

        TemplateRenderer.BuildTokens(Specs.Entity(eventBus: false), Specs.Config())["EVENT_BUS_CTOR_PARAM"].Should().BeEmpty();
    }

    [Fact]
    public void DaBodyIsEmptyForClientAndImplementedForServer()
    {
        TemplateRenderer.BuildTokens(Specs.Entity(mode: FilterMode.Client), Specs.Config())["DA_BODY"].Should().Be(";\n");

        var server = TemplateRenderer.BuildTokens(Specs.Entity(
            props: [Specs.Prop("Name", "string", filter: true)],
            mode: FilterMode.Server), Specs.Config());
        server["DA_BODY"].Should().Contain("GetPagedAsync");
    }
}
