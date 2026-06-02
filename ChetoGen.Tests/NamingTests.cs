using ChetoGen.Generator;

namespace ChetoGen.Tests;

public class NamingTests
{
    [Theory]
    [InlineData("order", "Order")]
    [InlineData("Order", "Order")]
    [InlineData("x", "X")]
    [InlineData("", "")]
    public void CapitalizeUppercasesFirstChar(string input, string expected) =>
        Naming.Capitalize(input).Should().Be(expected);

    [Theory]
    [InlineData("Order")]
    [InlineData("_value")]
    [InlineData("x1")]
    [InlineData("CamelCase123")]
    public void IsValidIdentifierAcceptsValidNames(string name) =>
        Naming.IsValidIdentifier(name).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1abc")]
    [InlineData("a b")]
    [InlineData("a-b")]
    [InlineData("a.b")]
    public void IsValidIdentifierRejectsInvalidNames(string? name) =>
        Naming.IsValidIdentifier(name).Should().BeFalse();
}
