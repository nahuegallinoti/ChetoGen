using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChetoGen.Generator.Mutators;

internal sealed partial class NavMenuMutator(string targetPath) : IFileMutator
{
    public string TargetPath { get; } = targetPath;

    public MutationResult Mutate(string source, EntitySpec entity)
    {
        var marker = $"href=\"{entity.Lower}\"";
        if (source.Contains(marker, StringComparison.Ordinal))
            return new MutationResult(source, false, "already up to date");

        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var anchorIdx = -1;
        if (entity.RequireAuth)
        {
            var outerAuthMatch = OuterAuthorizedCloseRegex().Match(source);
            if (outerAuthMatch.Success)
                anchorIdx = outerAuthMatch.Index;
        }

        if (anchorIdx < 0)
            anchorIdx = source.LastIndexOf("</nav>", StringComparison.Ordinal);

        if (anchorIdx < 0)
            throw new InvalidOperationException("Could not find anchor in NavMenu.razor.");

        var lineStart = source.LastIndexOf('\n', anchorIdx) + 1;
        var anchorIndent = ExtractIndent(source, lineStart);
        var navItem = BuildNavItem(entity, newline, anchorIndent + "    ");

        var insertion = navItem + newline + newline;
        var updated = source[..lineStart] + insertion + source[lineStart..];

        return new MutationResult(updated, true, "+ NavLink");
    }

    private static string ExtractIndent(string source, int lineStart)
    {
        var sb = new StringBuilder();
        for (var i = lineStart; i < source.Length; i++)
        {
            var c = source[i];
            if (c == ' ' || c == '\t') sb.Append(c);
            else break;
        }
        return sb.ToString();
    }

    private static string BuildNavItem(EntitySpec entity, string newline, string divIndent)
    {
        var inner = divIndent + "    ";
        var deep = inner + "    ";

        var sb = new StringBuilder();
        sb.Append(divIndent).Append("<div class=\"nav-item px-3\">").Append(newline);
        sb.Append(inner).Append(CultureInfo.InvariantCulture, $"<NavLink class=\"nav-link\" href=\"{entity.Lower}\">").Append(newline);
        sb.Append(deep).Append(CultureInfo.InvariantCulture, $"<span class=\"bi bi-{entity.Icon}\" aria-hidden=\"true\"></span> {entity.Plural}").Append(newline);
        sb.Append(inner).Append("</NavLink>").Append(newline);
        sb.Append(divIndent).Append("</div>");
        return sb.ToString();
    }

    [GeneratedRegex(@"</Authorized>\s*<NotAuthorized>")]
    private static partial Regex OuterAuthorizedCloseRegex();
}
