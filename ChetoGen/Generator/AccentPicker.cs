namespace ChetoGen.Generator;

/// <summary>
/// Picks a Bootstrap theme accent for an entity. Defaults to a single cohesive
/// accent for consistency across the app; per-entity color identity comes from
/// the icon, not the accent. Override per-entity with --accent.
/// </summary>
internal static class AccentPicker
{
    private const string DefaultAccent = "primary";

    public static string PickFor(string entityName) => DefaultAccent;
}
