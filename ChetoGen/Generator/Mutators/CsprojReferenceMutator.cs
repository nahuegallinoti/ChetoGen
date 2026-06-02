using System.Text.RegularExpressions;

namespace ChetoGen.Generator.Mutators;

/// <summary>
/// Adds a &lt;ProjectReference&gt; to a .csproj file idempotently.
/// Tries to copy the indentation of an existing &lt;ProjectReference&gt; or &lt;ItemGroup&gt;,
/// otherwise inserts a new &lt;ItemGroup&gt; right before &lt;/Project&gt;.
/// </summary>
internal sealed partial class CsprojReferenceMutator : IFileMutator
{
    private readonly string _referenceInclude;

    public string TargetPath { get; }

    public CsprojReferenceMutator(string targetPath, string referenceInclude)
    {
        TargetPath = targetPath;
        _referenceInclude = referenceInclude;
    }

    public MutationResult Mutate(string source, EntitySpec entity)
    {
        var label = Path.GetFileNameWithoutExtension(_referenceInclude);

        if (source.Contains($"Include=\"{_referenceInclude}\"", StringComparison.Ordinal))
            return new MutationResult(source, false, "already up to date");

        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var existingRef = ProjectReferenceLineRegex().Matches(source);
        if (existingRef.Count > 0)
        {
            var last = existingRef[^1];
            var indent = last.Groups[1].Value;
            var lineEnd = source.IndexOf(newline, last.Index, StringComparison.Ordinal);
            if (lineEnd < 0) lineEnd = source.Length;

            var refLine = $"{indent}<ProjectReference Include=\"{_referenceInclude}\" />";
            return new MutationResult(source.Insert(lineEnd, newline + refLine), true, $"+ ref {label}");
        }

        var projectEndIdx = source.LastIndexOf("</Project>", StringComparison.Ordinal);
        if (projectEndIdx < 0)
            throw new InvalidOperationException($"Could not find </Project> in {TargetPath}.");

        var itemGroupMatch = ItemGroupOpenRegex().Match(source);
        var groupIndent = itemGroupMatch.Success ? itemGroupMatch.Groups[1].Value : "  ";
        var innerIndent = groupIndent + "  ";

        var lineStart = source.LastIndexOf('\n', projectEndIdx) + 1;
        var insertion =
            $"{groupIndent}<ItemGroup>{newline}" +
            $"{innerIndent}<ProjectReference Include=\"{_referenceInclude}\" />{newline}" +
            $"{groupIndent}</ItemGroup>{newline}{newline}";

        return new MutationResult(source.Insert(lineStart, insertion), true, $"+ ref {label}");
    }

    [GeneratedRegex(@"^([ \t]*)<ProjectReference\b", RegexOptions.Multiline)]
    private static partial Regex ProjectReferenceLineRegex();

    [GeneratedRegex(@"^([ \t]*)<ItemGroup>", RegexOptions.Multiline)]
    private static partial Regex ItemGroupOpenRegex();
}
