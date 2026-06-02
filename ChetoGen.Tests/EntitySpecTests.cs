using ChetoGen.Generator;

namespace ChetoGen.Tests;

public class EntitySpecTests
{
    [Theory]
    [InlineData("Order", "Orders")]
    [InlineData("City", "Cities")]    // consonant + y => ies
    [InlineData("Boy", "Boys")]       // vowel + y => ys
    [InlineData("Box", "Boxes")]
    [InlineData("Dish", "Dishes")]
    [InlineData("Class", "Classes")]
    public void PluralizesName(string name, string expectedPlural) =>
        Specs.Entity(name: name).Plural.Should().Be(expectedPlural);

    [Fact]
    public void DerivesCaseVariants()
    {
        var e = Specs.Entity(name: "Order");

        e.Lower.Should().Be("order");
        e.Camel.Should().Be("order");
        e.PluralLower.Should().Be("orders");
        e.PluralCamel.Should().Be("orders");
    }

    [Fact]
    public void ProjectionsRespectFlags()
    {
        var props = new[]
        {
            Specs.Prop("A", "string", filter: true, list: true, sort: true),
            Specs.Prop("B", "int", filter: false, list: true, sort: false),
            Specs.Prop("C", "string", filter: true, list: false, sort: true), // hidden => not sortable
        };
        var e = Specs.Entity(props: props);

        e.FilterableProperties.Select(p => p.Name).Should().Equal("A", "C");
        e.ListProperties.Select(p => p.Name).Should().Equal("A", "B");
        e.SortableProperties.Select(p => p.Name).Should().Equal("A");
    }

    [Fact]
    public void IsServerFilteringReflectsMode()
    {
        Specs.Entity(mode: FilterMode.Server).IsServerFiltering.Should().BeTrue();
        Specs.Entity(mode: FilterMode.Client).IsServerFiltering.Should().BeFalse();
    }

    [Fact]
    public void IconAndAccentHonorOverrides()
    {
        var e = Specs.Entity(name: "Order", icon: "star", accent: "danger");

        e.Icon.Should().Be("star");
        e.Accent.Should().Be("danger");
    }

    [Fact]
    public void IconFallsBackToKeywordPicker() =>
        Specs.Entity(name: "Order").Icon.Should().Be("receipt");
}
