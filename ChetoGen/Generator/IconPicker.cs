namespace ChetoGen.Generator;

/// <summary>
/// Picks a Bootstrap Icons class (without the "bi-" prefix) for an entity name or a property,
/// using lightweight keyword matching.
/// </summary>
internal static class IconPicker
{
    private static readonly (string Keyword, string Icon)[] EntityMap =
    {
        ("user", "person-circle"),
        ("person", "person-circle"),
        ("customer", "person-vcard"),
        ("client", "person-vcard"),
        ("employee", "person-badge"),
        ("member", "person-badge"),
        ("contact", "person-rolodex"),
        ("account", "person-gear"),
        ("role", "shield-lock"),
        ("permission", "key"),
        ("auth", "shield-check"),

        ("order", "receipt"),
        ("cart", "cart3"),
        ("purchase", "bag-check"),
        ("sale", "graph-up-arrow"),
        ("invoice", "file-earmark-text"),
        ("payment", "credit-card"),
        ("billing", "credit-card-2-front"),
        ("transaction", "arrow-left-right"),
        ("subscription", "arrow-repeat"),
        ("discount", "percent"),
        ("coupon", "ticket-perforated"),
        ("tax", "percent"),

        ("product", "box-seam"),
        ("item", "box"),
        ("inventory", "boxes"),
        ("stock", "boxes"),
        ("category", "tag"),
        ("brand", "award"),
        ("supplier", "truck"),
        ("shipment", "truck-front"),
        ("delivery", "truck-front"),
        ("warehouse", "buildings"),

        ("post", "pencil-square"),
        ("article", "journal-text"),
        ("blog", "journal-richtext"),
        ("comment", "chat-dots"),
        ("message", "chat-text"),
        ("notification", "bell"),
        ("alert", "exclamation-triangle"),
        ("task", "check2-square"),
        ("todo", "list-check"),
        ("project", "kanban"),
        ("milestone", "flag"),
        ("issue", "bug"),
        ("ticket", "ticket-detailed"),

        ("event", "calendar-event"),
        ("appointment", "calendar-check"),
        ("schedule", "calendar-week"),
        ("calendar", "calendar3"),
        ("reminder", "alarm"),

        ("file", "file-earmark"),
        ("document", "file-earmark-text"),
        ("attachment", "paperclip"),
        ("image", "image"),
        ("photo", "camera"),
        ("video", "camera-video"),
        ("media", "collection-play"),
        ("folder", "folder2"),

        ("country", "globe2"),
        ("city", "buildings"),
        ("region", "geo"),
        ("address", "geo-alt"),
        ("location", "pin-map"),
        ("place", "geo-alt"),
        ("store", "shop"),
        ("branch", "shop-window"),
        ("company", "building"),
        ("organization", "building"),
        ("department", "diagram-3"),
        ("team", "people"),
        ("group", "people"),

        ("email", "envelope"),
        ("mail", "envelope-paper"),
        ("phone", "telephone"),

        ("setting", "gear"),
        ("config", "sliders"),
        ("preference", "toggles"),
        ("log", "journal-text"),
        ("audit", "search"),
        ("report", "graph-up"),
        ("dashboard", "speedometer2"),
        ("metric", "bar-chart-line"),
        ("stat", "bar-chart"),

        ("show", "easel"),
        ("register", "person-plus"),
        ("login", "box-arrow-in-right"),
    };

    public static string PickFor(string entityName)
    {
        var lower = entityName.ToLowerInvariant();
        foreach (var (keyword, icon) in EntityMap)
            if (lower.Contains(keyword, StringComparison.Ordinal))
                return icon;

        return "collection";
    }

    private static readonly (string Keyword, string Icon)[] PropertyMap =
    {
        ("email", "envelope"),
        ("mail", "envelope"),
        ("phone", "telephone"),
        ("mobile", "phone"),
        ("url", "link-45deg"),
        ("link", "link-45deg"),
        ("website", "globe"),
        ("address", "geo-alt"),
        ("city", "buildings"),
        ("country", "globe2"),
        ("zip", "mailbox2"),
        ("postal", "mailbox2"),

        ("password", "key"),
        ("token", "key-fill"),
        ("secret", "shield-lock"),

        ("name", "tag"),
        ("title", "type"),
        ("code", "upc"),
        ("sku", "upc-scan"),
        ("description", "chat-left-text"),
        ("note", "sticky"),
        ("comment", "chat-dots"),
        ("message", "chat-text"),

        ("price", "currency-dollar"),
        ("amount", "cash-coin"),
        ("total", "cash-stack"),
        ("cost", "tag"),
        ("salary", "cash"),

        ("date", "calendar3"),
        ("time", "clock"),
        ("birth", "cake2"),
        ("expire", "hourglass-split"),

        ("count", "123"),
        ("quantity", "stack"),
        ("number", "123"),
        ("age", "person-arms-up"),
        ("year", "calendar-range"),
    };

    public static string PickForProperty(PropertySpec prop)
    {
        var lower = prop.Name.ToLowerInvariant();
        foreach (var (keyword, icon) in PropertyMap)
            if (lower.Contains(keyword, StringComparison.Ordinal))
                return icon;

        return prop.Type switch
        {
            "string" => "input-cursor-text",
            "bool" => "toggle2-on",
            "Guid" => "key-fill",
            "DateTime" or "DateTimeOffset" or "DateOnly" => "calendar3",
            "TimeOnly" => "clock",
            "decimal" or "double" or "float" => "currency-dollar",
            "int" or "long" or "short" or "byte" => "123",
            _ => "input-cursor-text",
        };
    }
}
