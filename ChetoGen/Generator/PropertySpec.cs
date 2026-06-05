namespace ChetoGen.Generator;

internal sealed record PropertySpec(
    string Name,
    string Type,
    bool Required,
    bool Filterable,
    bool ShowInList,
    bool Sortable)
{
    public string CamelName => char.ToLowerInvariant(Name[0]) + Name[1..];

    public bool IsString => Type.Equals("string", StringComparison.OrdinalIgnoreCase);

    public string DefaultSuffix => Type switch
    {
        "string" => " = string.Empty;",
        _ => string.Empty
    };

    public bool IsNumeric => Type is "int" or "long" or "short" or "byte" or "decimal" or "double" or "float";

    public bool IsBool => Type == "bool";

    public bool IsDateTime => Type is "DateTime" or "DateTimeOffset" or "DateOnly" or "TimeOnly";

    public bool IsGuid => Type == "Guid";

    /// <summary>Razor input component name for this property.</summary>
    public string RazorInputComponent => Type switch
    {
        "string" => "InputText",
        "int" or "long" or "short" or "byte" => "InputNumber",
        "decimal" or "double" or "float" => "InputNumber",
        "bool" => "InputCheckbox",
        // InputDate binds DateTime/DateTimeOffset/DateOnly/TimeOnly (it infers the type param from the value).
        "DateTime" or "DateTimeOffset" or "DateOnly" or "TimeOnly" => "InputDate",
        _ => "InputText"
    };

    /// <summary>Default for "Filterable" when no flag is given: strings yes, everything else no.</summary>
    public static bool DefaultFilterableFor(string normalizedType) =>
        normalizedType.Equals("string", StringComparison.OrdinalIgnoreCase);

    public static PropertySpec Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var parts = raw.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new ArgumentException($"Invalid property '{raw}'. Expected format 'Name:type[:flags]'.");

        var name = parts[0];
        if (!Naming.IsValidIdentifier(name))
            throw new ArgumentException($"Invalid property name '{name}'. Use letters, digits or '_', starting with a letter or '_'.");

        var type = NormalizeType(parts[1]);

        var required = false;
        bool? filterable = null;
        bool? showInList = null;
        bool? sortable = null;

        for (var i = 2; i < parts.Length; i++)
        {
            var flag = parts[i].ToLowerInvariant();
            switch (flag)
            {
                case "required":
                    required = true;
                    break;
                case "filter":
                case "filterable":
                    filterable = true;
                    break;
                case "nofilter":
                case "no-filter":
                case "notfilterable":
                    filterable = false;
                    break;
                case "hidden":
                case "hide":
                case "nolist":
                case "no-list":
                    showInList = false;
                    break;
                case "list":
                    showInList = true;
                    break;
                case "sort":
                case "sortable":
                    sortable = true;
                    break;
                case "nosort":
                case "no-sort":
                    sortable = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown flag '{parts[i]}' for property '{name}'. Supported: required, filter/nofilter, hidden/list, sort/nosort.");
            }
        }

        return new PropertySpec(
            Naming.Capitalize(name),
            type,
            required,
            filterable ?? DefaultFilterableFor(type),
            showInList ?? true,
            sortable ?? true);
    }

    private static string NormalizeType(string type) => type.ToLowerInvariant() switch
    {
        "string" => "string",
        "int" or "int32" => "int",
        "long" or "int64" => "long",
        "short" or "int16" => "short",
        "byte" => "byte",
        "decimal" => "decimal",
        "double" => "double",
        "float" or "single" => "float",
        "bool" or "boolean" => "bool",
        "guid" => "Guid",
        "datetime" => "DateTime",
        "datetimeoffset" => "DateTimeOffset",
        "dateonly" => "DateOnly",
        "timeonly" => "TimeOnly",
        _ => type
    };
}
