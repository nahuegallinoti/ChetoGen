namespace ChetoGen.Generator.Mutators;

/// <summary>
/// Generic mutator for DependencyInjection-style files.
/// Adds (idempotently) using lines and a service registration line.
/// </summary>
internal sealed class DiRegistrationMutator : IFileMutator
{
    private readonly IReadOnlyList<string> _usingLines;
    private readonly string _registrationLine;
    private readonly string _markerForLastRegistration;

    public string TargetPath { get; }

    public DiRegistrationMutator(
        string targetPath,
        IReadOnlyList<string> usingLines,
        string registrationLine,
        string markerForLastRegistration)
    {
        TargetPath = targetPath;
        _usingLines = usingLines;
        _registrationLine = registrationLine;
        _markerForLastRegistration = markerForLastRegistration;
    }

    public MutationResult Mutate(string source, EntitySpec entity)
    {
        var newline = DetectNewline(source);
        var changed = false;
        var notes = new List<string>();

        foreach (var u in _usingLines)
        {
            if (string.IsNullOrEmpty(u)) continue;
            if (source.Contains(u, StringComparison.Ordinal)) continue;

            source = InsertUsing(source, u, newline);
            changed = true;
        }

        if (_usingLines.Count > 0 && changed)
            notes.Add("+ usings");

        if (!source.Contains(_registrationLine, StringComparison.Ordinal))
        {
            source = InsertAfterMarker(source, _markerForLastRegistration, _registrationLine, newline);
            changed = true;
            notes.Add("+ registration");
        }

        var description = changed ? string.Join(", ", notes) : "already up to date";
        return new MutationResult(source, changed, description);
    }

    private static string DetectNewline(string source) =>
        source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static string InsertUsing(string source, string usingLine, string newline)
    {
        var lines = SplitLines(source, newline).ToList();

        var lastUsingIdx = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("using ", StringComparison.Ordinal))
                lastUsingIdx = i;
        }

        var insertIdx = lastUsingIdx >= 0 ? lastUsingIdx + 1 : 0;
        lines.Insert(insertIdx, usingLine);
        return string.Join(newline, lines);
    }

    private static string InsertAfterMarker(string source, string marker, string newLine, string newline)
    {
        var lines = SplitLines(source, newline).ToList();

        var lastMatch = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(marker, StringComparison.Ordinal))
                lastMatch = i;
        }

        if (lastMatch >= 0)
        {
            lines.Insert(lastMatch + 1, newLine);
        }
        else
        {
            throw new InvalidOperationException(
                $"Could not find marker '{marker}' to insert after. File: maybe outdated structure.");
        }

        return string.Join(newline, lines);
    }

    private static string[] SplitLines(string source, string newline) =>
        source.Split([newline], StringSplitOptions.None);
}
