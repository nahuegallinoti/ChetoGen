namespace ChetoGen.Generator;

/// <summary>
/// Small, pure helpers for turning user input into valid C# names.
/// Lives in <c>Generator/</c> so it stays headless and unit-testable.
/// </summary>
internal static class Naming
{
    /// <summary>Upper-cases the first character, leaving the rest untouched.</summary>
    public static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>
    /// True when <paramref name="value"/> is a valid C# identifier: starts with a
    /// letter or underscore and contains only letters, digits or underscores.
    /// </summary>
    public static bool IsValidIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (!(char.IsLetter(value[0]) || value[0] == '_')) return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_'))
                return false;
        }

        return true;
    }
}
