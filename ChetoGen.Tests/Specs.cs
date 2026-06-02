using ChetoGen.Configuration;
using ChetoGen.Generator;

namespace ChetoGen.Tests;

/// <summary>Terse factories so each test only states the fields it cares about.</summary>
internal static class Specs
{
    /// <summary>Default config for tests — "AspireApp" reproduces the original built-in layout.</summary>
    public static GeneratorConfig Config(string baseNamespace = "AspireApp") =>
        GeneratorConfig.CreateDefault(baseNamespace);

    public static PropertySpec Prop(
        string name,
        string type,
        bool required = false,
        bool filter = false,
        bool list = true,
        bool sort = true) =>
        new(name, type, required, filter, list, sort);

    public static EntitySpec Entity(
        string name = "Order",
        string idType = "long",
        IReadOnlyList<PropertySpec>? props = null,
        bool blazor = true,
        bool nav = true,
        bool auth = true,
        bool eventBus = false,
        FilterMode mode = FilterMode.Client,
        int pageSize = 25,
        string? icon = null,
        string? accent = null) =>
        new(name, idType, props ?? [], blazor, nav, auth, eventBus, mode, pageSize, icon, accent);
}
