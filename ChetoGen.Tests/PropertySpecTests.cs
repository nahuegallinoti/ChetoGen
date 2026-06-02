using ChetoGen.Generator;

namespace ChetoGen.Tests;

public class PropertySpecTests
{
    [Fact]
    public void ParseStringDefaultsToFilterableListedSortable()
    {
        var p = PropertySpec.Parse("Name:string");

        p.Name.Should().Be("Name");
        p.Type.Should().Be("string");
        p.Required.Should().BeFalse();
        p.Filterable.Should().BeTrue();   // strings default to filterable
        p.ShowInList.Should().BeTrue();
        p.Sortable.Should().BeTrue();
    }

    [Fact]
    public void ParseNonStringDefaultsToNotFilterable() =>
        PropertySpec.Parse("Age:int").Filterable.Should().BeFalse();

    [Fact]
    public void ParseHonorsAllFlags()
    {
        var p = PropertySpec.Parse("Total:decimal:required:filter:sort");

        p.Required.Should().BeTrue();
        p.Filterable.Should().BeTrue();
        p.Sortable.Should().BeTrue();
    }

    [Fact]
    public void ParseHiddenClearsShowInList() =>
        PropertySpec.Parse("Notes:string:hidden").ShowInList.Should().BeFalse();

    [Fact]
    public void ParseNoFilterOverridesStringDefault() =>
        PropertySpec.Parse("Name:string:nofilter").Filterable.Should().BeFalse();

    [Fact]
    public void ParseCapitalizesName() =>
        PropertySpec.Parse("total:decimal").Name.Should().Be("Total");

    [Theory]
    [InlineData("Foo:int32", "int")]
    [InlineData("Foo:int64", "long")]
    [InlineData("Foo:boolean", "bool")]
    [InlineData("Foo:guid", "Guid")]
    [InlineData("Foo:datetime", "DateTime")]
    public void ParseNormalizesType(string raw, string expectedType) =>
        PropertySpec.Parse(raw).Type.Should().Be(expectedType);

    [Theory]
    [InlineData("Total")]                 // missing type
    [InlineData("1bad:int")]              // invalid identifier
    [InlineData("Foo:string:bogusflag")] // unknown flag
    public void ParseRejectsBadInput(string raw)
    {
        var act = () => PropertySpec.Parse(raw);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("string", true)]
    [InlineData("int", false)]
    [InlineData("DateTime", false)]
    public void DefaultFilterableForOnlyStrings(string type, bool expected) =>
        PropertySpec.DefaultFilterableFor(type).Should().Be(expected);
}
