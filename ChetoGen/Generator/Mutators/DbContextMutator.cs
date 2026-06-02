using System.Globalization;
using System.Text;

namespace ChetoGen.Generator.Mutators;

internal sealed class DbContextMutator(string targetPath) : IFileMutator
{
    public string TargetPath { get; } = targetPath;

    public MutationResult Mutate(string source, EntitySpec entity)
    {
        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var changed = false;
        var notes = new List<string>();

        var dbSetLine = $"    public DbSet<{entity.Name}> {entity.Plural} => Set<{entity.Name}>();";
        if (!source.Contains(dbSetLine, StringComparison.Ordinal))
        {
            source = InsertAfterLastDbSet(source, dbSetLine, newline);
            changed = true;
            notes.Add($"+ DbSet<{entity.Name}>");
        }

        var modelConfig = BuildModelConfig(entity, newline);
        if (!string.IsNullOrEmpty(modelConfig) && !source.Contains($"modelBuilder.Entity<{entity.Name}>", StringComparison.Ordinal))
        {
            source = InsertModelBuilderConfig(source, modelConfig, newline);
            changed = true;
            notes.Add($"+ modelBuilder.Entity<{entity.Name}>");
        }

        var description = changed ? string.Join(", ", notes) : "already up to date";
        return new MutationResult(source, changed, description);
    }

    private static string InsertAfterLastDbSet(string source, string newLine, string newline)
    {
        var lines = source.Split([newline], StringSplitOptions.None).ToList();
        var lastDbSet = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("public DbSet<", StringComparison.Ordinal))
                lastDbSet = i;
        }

        if (lastDbSet < 0)
            throw new InvalidOperationException("Could not find any DbSet<> declaration in AppDbContext.");

        lines.Insert(lastDbSet + 1, newLine);
        return string.Join(newline, lines);
    }

    private static string InsertModelBuilderConfig(string source, string config, string newline)
    {
        // Find the closing brace of OnModelCreating method.
        const string anchor = "protected override void OnModelCreating(ModelBuilder modelBuilder)";
        var anchorIdx = source.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIdx < 0)
            throw new InvalidOperationException("Could not find OnModelCreating method in AppDbContext.");

        var braceIdx = source.IndexOf('{', anchorIdx);
        if (braceIdx < 0)
            throw new InvalidOperationException("Could not find OnModelCreating opening brace.");

        var endIdx = FindMatchingBrace(source, braceIdx);
        if (endIdx < 0)
            throw new InvalidOperationException("Could not find OnModelCreating closing brace.");

        // Walk back to last non-whitespace before closing brace.
        var insertAt = endIdx;
        while (insertAt > braceIdx && (source[insertAt - 1] == ' ' || source[insertAt - 1] == '\t' || source[insertAt - 1] == '\r' || source[insertAt - 1] == '\n'))
            insertAt--;

        var insertion = newline + newline + config + newline + "    ";
        return source[..insertAt] + insertion + source[insertAt..];
    }

    private static int FindMatchingBrace(string source, int openIdx)
    {
        var depth = 0;
        for (var i = openIdx; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static string BuildModelConfig(EntitySpec entity, string newline)
    {
        var stringProps = entity.Properties.Where(p => p.IsString).ToArray();
        if (stringProps.Length == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"        modelBuilder.Entity<{entity.Name}>(entity =>");
        sb.Append(newline);
        sb.Append("        {");
        sb.Append(newline);
        for (var i = 0; i < stringProps.Length; i++)
        {
            var p = stringProps[i];
            var max = p.Required ? 256 : 2000;
            sb.Append(CultureInfo.InvariantCulture, $"            entity.Property(x => x.{p.Name}).HasMaxLength({max})");
            if (p.Required) sb.Append(".IsRequired()");
            sb.Append(';');
            sb.Append(newline);
        }
        sb.Append("        });");
        return sb.ToString();
    }
}
