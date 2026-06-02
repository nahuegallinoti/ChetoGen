namespace ChetoGen.Generator;

internal enum FilterMode
{
    Client,
    Server,
}

internal sealed record EntitySpec(
    string Name,
    string IdType,
    IReadOnlyList<PropertySpec> Properties,
    bool GenerateBlazorPage,
    bool RegisterInNavMenu,
    bool RequireAuth,
    bool UseEventBus,
    FilterMode FilterMode,
    int PageSize,
    string? IconOverride = null,
    string? AccentOverride = null)
{
    public string Lower => Name.ToLowerInvariant();
    public string Camel => char.ToLowerInvariant(Name[0]) + Name[1..];
    public string Plural => SimplePluralize(Name);
    public string PluralLower => Plural.ToLowerInvariant();
    public string PluralCamel => char.ToLowerInvariant(Plural[0]) + Plural[1..];

    /// <summary>Bootstrap Icons class name (without the leading "bi-") for this entity.</summary>
    public string Icon => IconOverride ?? IconPicker.PickFor(Name);

    /// <summary>Bootstrap theme accent (primary, success, info, warning, danger).</summary>
    public string Accent => AccentOverride ?? AccentPicker.PickFor(Name);

    public bool IsServerFiltering => FilterMode == FilterMode.Server;

    public IReadOnlyList<PropertySpec> FilterableProperties =>
        Properties.Where(p => p.Filterable).ToArray();

    public IReadOnlyList<PropertySpec> ListProperties =>
        Properties.Where(p => p.ShowInList).ToArray();

    public IReadOnlyList<PropertySpec> SortableProperties =>
        Properties.Where(p => p.ShowInList && p.Sortable).ToArray();

    private static string SimplePluralize(string singular)
    {
        if (string.IsNullOrEmpty(singular)) return singular;
        if (singular.EndsWith('y') && singular.Length > 1 && !"aeiou".Contains(singular[^2]))
            return singular[..^1] + "ies";
        if (singular.EndsWith('s') || singular.EndsWith('x') || singular.EndsWith("ch", StringComparison.Ordinal) || singular.EndsWith("sh", StringComparison.Ordinal))
            return singular + "es";
        return singular + "s";
    }
}
