using System.Globalization;
using System.Text;
using ChetoGen.Configuration;

namespace ChetoGen.Generator;

internal sealed class TemplateRenderer
{
    private readonly string _builtInTemplatesRoot;
    private readonly string? _overrideTemplatesRoot;

    public TemplateRenderer(GeneratorConfig config)
    {
        // AppContext.BaseDirectory is robust for a packed dotnet tool (Assembly.Location can be empty).
        _builtInTemplatesRoot = Path.Combine(AppContext.BaseDirectory, "Templates");
        _overrideTemplatesRoot = config.TemplatesDirectory;
    }

    /// <summary>
    /// Renders a template against a pre-built token map. The map is constant for a given
    /// entity, so callers build it once (see <see cref="BuildTokens"/>) and reuse it for
    /// every template in the run instead of rebuilding it per file. A consumer template of the
    /// same file name in <c>TemplatesDirectory</c> wins over the built-in one.
    /// </summary>
    public string Render(string templateFileName, IReadOnlyDictionary<string, string> tokens)
    {
        var path = ResolveTemplatePath(templateFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template not found: {path}");

        var sb = new StringBuilder(File.ReadAllText(path));
        foreach (var (key, value) in tokens)
            sb.Replace("{{" + key + "}}", value);

        // Normalize to LF. Line endings otherwise come from three uncoordinated sources —
        // the template files (CRLF or LF depending on checkout/edit history), C# token
        // literals (always "\n"), and Environment.NewLine via StringBuilder.AppendLine
        // (CRLF on Windows, LF elsewhere) — which produces files with mixed endings that
        // also vary by the OS the tool runs on. Collapsing to LF makes output deterministic
        // and clean; LF is universally accepted by .NET tooling, editors, and Git.
        return sb.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
    }

    /// <summary>Consumer override folder first (if present), then the built-in template set.</summary>
    private string ResolveTemplatePath(string templateFileName)
    {
        if (_overrideTemplatesRoot is not null)
        {
            var overridePath = Path.Combine(_overrideTemplatesRoot, templateFileName);
            if (File.Exists(overridePath))
                return overridePath;
        }

        return Path.Combine(_builtInTemplatesRoot, templateFileName);
    }

    /// <summary>
    /// Builds the full <c>{{TOKEN}} → value</c> map for an entity. Every value is derived
    /// purely from <paramref name="entity"/> and <paramref name="config"/>, so this is computed
    /// once per generation run. Consumer <c>tokens</c> overrides are merged last (they win).
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildTokens(EntitySpec entity, GeneratorConfig config)
    {
        var ns = config.BaseNamespace;

        string Arch(string key) => ExpandArchitecture(config, entity, key);

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BASE_NS"] = ns,
            ["ENTITY"] = entity.Name,
            ["entity"] = entity.Lower,
            ["ENTITY_CAMEL"] = entity.Camel,
            ["ENTITY_PLURAL"] = entity.Plural,
            ["entity_plural"] = entity.PluralLower,
            ["ID_TYPE"] = entity.IdType,
            ["ID_ROUTE_CONSTRAINT"] = BuildIdRouteConstraint(entity.IdType),
            ["ENTITY_ICON"] = entity.Icon,
            ["ACCENT"] = entity.Accent,
            ["ACCENT_SUBTLE"] = $"{entity.Accent}-subtle",
            ["PAGE_SIZE"] = entity.PageSize.ToString(CultureInfo.InvariantCulture),
            ["PROPS_ENTITY"] = BuildEntityProps(entity),
            ["PROPS_MODEL"] = BuildModelProps(entity),
            ["PROPS_DBCONFIG"] = BuildDbConfigProps(entity),
            ["PROPS_MAPPER_TO_MODEL"] = BuildMapperLines(entity),
            ["PROPS_MAPPER_TO_ENTITY"] = BuildMapperLines(entity),
            ["PROPS_FORM_FIELDS"] = BuildFormFields(entity),
            ["PROPS_TABLE_HEAD"] = BuildTableHead(entity),
            ["PROPS_TABLE_BODY"] = BuildTableBody(entity),
            ["PROPS_FILTER_FIELDS"] = BuildFilterFields(entity),
            ["PROPS_FILTER_STATE"] = BuildFilterState(entity),
            ["PROPS_FILTER_LOGIC"] = BuildFilterLogic(entity),
            ["PROPS_RESET_LOGIC"] = BuildResetLogic(entity),
            ["PROPS_SORT_CASES"] = BuildSortCases(entity),
            ["PROPS_FILTER_DTO"] = BuildFilterDtoProps(entity),
            ["AUTHORIZE_ATTR"] = entity.RequireAuth ? "[Authorize]\n" : string.Empty,
            ["AUTHORIZE_USING"] = entity.RequireAuth ? "using Microsoft.AspNetCore.Authorization;\n" : string.Empty,
            ["EVENT_BUS_USING"] = entity.UseEventBus ? $"using {ns}.Application.Contracts.EventBus;\n" : string.Empty,
            ["EVENT_BUS_CTOR_PARAM"] = entity.UseEventBus ? ", IMessageBus messageBus" : string.Empty,
            ["EVENT_BUS_BASE_ARG"] = entity.UseEventBus ? ", messageBus" : string.Empty,
            ["DISPLAY_NAME_EXPR"] = BuildDisplayNameExpression(entity),
            ["PERSISTENCE_USINGS"] = BuildPersistenceUsings(entity, ns),
            ["PERSISTENCE_BODY"] = BuildPersistenceBody(entity),
            ["DA_USINGS"] = BuildDaUsings(entity, ns),
            ["DA_BODY"] = BuildDaBody(entity),
            ["CONTRACT_USINGS"] = BuildContractUsings(entity, ns),
            ["CONTRACT_BODY"] = BuildContractBody(entity),
            ["SERVICE_USINGS"] = BuildServiceUsings(entity, ns),
            ["SERVICE_BODY"] = BuildServiceBody(entity),
            ["CONTROLLER_EXTRA_USINGS"] = BuildControllerExtraUsings(entity),
            ["CONTROLLER_BODY"] = BuildControllerBody(entity),
            ["API_CLIENT_EXTRA_USINGS"] = BuildApiClientExtraUsings(entity, ns),
            ["API_CLIENT_EXTRA_METHODS"] = BuildApiClientExtraMethods(entity),

            // Architecture seams — base classes/interfaces, ROP wrapper, cache. Each value comes from
            // config.Architecture (overridable per chetogen.json) after placeholder expansion.
            ["ARCH_ENTITY_USINGS"] = Arch("EntityUsings"),
            ["ARCH_ENTITY_BASE"] = Arch("EntityBase"),
            ["ARCH_MODEL_BASE"] = Arch("ModelBase"),
            ["ARCH_MAPPER_BASE"] = Arch("MapperBase"),
            ["ARCH_SERVICE_CONTRACT_USINGS"] = Arch("ServiceContractUsings"),
            ["ARCH_SERVICE_CONTRACT_BASE"] = Arch("ServiceContractBase"),
            ["ARCH_PERSISTENCE_CONTRACT_USINGS"] = Arch("PersistenceContractUsings"),
            ["ARCH_PERSISTENCE_CONTRACT_BASE"] = Arch("PersistenceContractBase"),
            ["ARCH_SERVICE_IMPL_USINGS"] = Arch("ServiceImplUsings"),
            ["ARCH_CACHE_USING"] = Arch("CacheUsing"),
            ["ARCH_SERVICE_CTOR"] = Arch("ServiceCtor"),
            ["ARCH_SERVICE_BASE"] = Arch("ServiceBase"),
            ["ARCH_DATAACCESS_USING"] = Arch("DataAccessUsing"),
            ["ARCH_DATAACCESS_CTOR"] = Arch("DataAccessCtor"),
            ["ARCH_DATAACCESS_BASE"] = Arch("DataAccessBase"),
            ["ARCH_CONTROLLER_CTOR"] = Arch("ControllerCtor"),
            ["ARCH_CONTROLLER_BASE"] = Arch("ControllerBase"),
            ["ARCH_APICLIENT_USING"] = Arch("ApiClientUsing"),
            ["ARCH_APICLIENT_CTOR"] = Arch("ApiClientCtor"),
            ["ARCH_APICLIENT_BASE"] = Arch("ApiClientBase"),
            ["ARCH_RESULT"] = Arch("ResultType"),
            ["ARCH_RESULT_OK"] = Arch("ResultIsSuccess"),
            ["ARCH_RESULT_VALUE"] = Arch("ResultValue"),
            ["ARCH_RESULT_ERRORS"] = Arch("ResultErrors"),
        };

        // Consumer-provided static token overrides win over everything (including BASE_NS).
        foreach (var (key, value) in config.Tokens)
            tokens[key] = value;

        return tokens;
    }

    /// <summary>Expands an architecture seam value (base class, wrapper, etc.) against the entity/config.</summary>
    private static string ExpandArchitecture(GeneratorConfig config, EntitySpec entity, string key)
    {
        var raw = config.Architecture.TryGetValue(key, out var value) ? value : string.Empty;
        return raw
            .Replace("{BaseNamespace}", config.BaseNamespace, StringComparison.Ordinal)
            .Replace("{Entity}", entity.Name, StringComparison.Ordinal)
            .Replace("{EntityCamel}", entity.Camel, StringComparison.Ordinal)
            .Replace("{Id}", entity.IdType, StringComparison.Ordinal)
            .Replace("{EventBusCtorParam}", entity.UseEventBus ? ", IMessageBus messageBus" : string.Empty, StringComparison.Ordinal)
            .Replace("{EventBusBaseArg}", entity.UseEventBus ? ", messageBus" : string.Empty, StringComparison.Ordinal);
    }

    private static string BuildDisplayNameExpression(EntitySpec entity)
    {
        var displayProp =
            entity.Properties.FirstOrDefault(p => p.IsString && p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase))
            ?? entity.Properties.FirstOrDefault(p => p.IsString && p.Name.Equals("Title", StringComparison.OrdinalIgnoreCase))
            ?? entity.Properties.FirstOrDefault(p => p.IsString);

        return displayProp is null
            ? "$\"#{Id}\""
            : $"!string.IsNullOrWhiteSpace(Model?.{displayProp.Name}) ? Model!.{displayProp.Name} : $\"#{{Id}}\"";
    }

    private static string BuildIdRouteConstraint(string idType) => idType switch
    {
        "long" => ":long",
        "int" => ":int",
        "Guid" => ":guid",
        _ => string.Empty,
    };

    private static string BuildEntityProps(EntitySpec entity)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < entity.Properties.Count; i++)
        {
            var p = entity.Properties[i];
            sb.Append(CultureInfo.InvariantCulture, $"    public {p.Type} {p.Name} {{ get; set; }}{p.DefaultSuffix}");
            if (i < entity.Properties.Count - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildModelProps(EntitySpec entity)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < entity.Properties.Count; i++)
        {
            var p = entity.Properties[i];
            if (p.Required)
                sb.AppendLine("    [Required(ErrorMessage = \"Field {0} is required.\")]");
            if (p.IsString)
                sb.AppendLine(CultureInfo.InvariantCulture, $"    [MaxLength({(p.Required ? 256 : 2000)})]");
            sb.Append(CultureInfo.InvariantCulture, $"    public {p.Type} {p.Name} {{ get; set; }}{p.DefaultSuffix}");
            if (i < entity.Properties.Count - 1)
            {
                sb.AppendLine();
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private static string BuildDbConfigProps(EntitySpec entity)
    {
        var configurable = entity.Properties.Where(p => p.IsString).ToArray();
        if (configurable.Length == 0) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < configurable.Length; i++)
        {
            var p = configurable[i];
            var max = p.Required ? 256 : 2000;
            sb.Append(CultureInfo.InvariantCulture, $"            entity.Property(x => x.{p.Name}).HasMaxLength({max})");
            if (p.Required) sb.Append(".IsRequired()");
            sb.Append(';');
            if (i < configurable.Length - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildMapperLines(EntitySpec entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine("        Id = source.Id,");
        for (var i = 0; i < entity.Properties.Count; i++)
        {
            var p = entity.Properties[i];
            sb.Append(CultureInfo.InvariantCulture, $"        {p.Name} = source.{p.Name}");
            if (i < entity.Properties.Count - 1) sb.AppendLine(",");
        }
        return sb.ToString();
    }

    private static string BuildFormFields(EntitySpec entity)
    {
        if (entity.Properties.Count == 0)
        {
            return "                    <div class=\"col-12\">"
                 + Environment.NewLine
                 + "                        <p class=\"text-muted small mb-0\">Esta entidad no tiene campos. Agregalos en la clase modelo.</p>"
                 + Environment.NewLine
                 + "                    </div>";
        }
        var sb = new StringBuilder();
        for (var i = 0; i < entity.Properties.Count; i++)
        {
            var p = entity.Properties[i];
            var requiredMark = p.Required ? " *" : string.Empty;

            if (p.IsBool)
            {
                sb.AppendLine("                    <div class=\"col-md-6\">");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                        <div class=\"form-check form-switch mt-4\">");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                            <InputCheckbox id=\"{p.CamelName}\" class=\"form-check-input\" role=\"switch\" @bind-Value=\"Model.{p.Name}\" />");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                            <label for=\"{p.CamelName}\" class=\"form-check-label\">{p.Name}</label>");
                sb.AppendLine("                        </div>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                        <ValidationMessage For=\"() => Model.{p.Name}\" class=\"text-danger small d-block\" />");
                sb.Append("                    </div>");
            }
            else
            {
                var componentName = p.RazorInputComponent;
                var colClass = p.IsString ? "col-md-12" : "col-md-6";
                sb.AppendLine(CultureInfo.InvariantCulture, $"                    <div class=\"{colClass}\">");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                        <label for=\"{p.CamelName}\" class=\"form-label\">{p.Name}{requiredMark}</label>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                        <{componentName} id=\"{p.CamelName}\" class=\"form-control\" @bind-Value=\"Model.{p.Name}\" />");
                sb.AppendLine(CultureInfo.InvariantCulture, $"                        <ValidationMessage For=\"() => Model.{p.Name}\" class=\"text-danger small\" />");
                sb.Append("                    </div>");
            }

            if (i < entity.Properties.Count - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildTableHead(EntitySpec entity)
    {
        var listProps = entity.ListProperties;
        if (listProps.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < listProps.Count; i++)
        {
            var p = listProps[i];
            var alignClass = p.IsNumeric ? " class=\"text-end\"" : string.Empty;

            if (p.Sortable)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"                    <th scope=\"col\"{alignClass}><button type=\"button\" class=\"btn btn-link p-0 text-decoration-none text-reset fw-semibold\" @onclick='() => OnSort(\"{p.Name}\")'>{p.Name} <i class=\"bi @SortIcon(\"{p.Name}\")\"></i></button></th>");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture, $"                    <th scope=\"col\"{alignClass}>{p.Name}</th>");
            }

            if (i < listProps.Count - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildTableBody(EntitySpec entity)
    {
        var listProps = entity.ListProperties;
        if (listProps.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < listProps.Count; i++)
        {
            var p = listProps[i];
            string cell;
            if (p.IsBool)
            {
                cell = $"<td>@(item.{p.Name} ? \"Sí\" : \"No\")</td>";
            }
            else if (p.IsDateTime)
            {
                cell = $"<td class=\"text-nowrap\">@item.{p.Name}.ToString(\"yyyy-MM-dd\")</td>";
            }
            else if (p.IsNumeric)
            {
                cell = $"<td class=\"text-end font-monospace\">@item.{p.Name}</td>";
            }
            else if (p.IsString && p.Name.Equals("Email", StringComparison.OrdinalIgnoreCase))
            {
                cell = $"<td><a href=\"mailto:@item.{p.Name}\" class=\"text-decoration-none\">@item.{p.Name}</a></td>";
            }
            else if (p.IsString && (p.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("Mobile", StringComparison.OrdinalIgnoreCase)))
            {
                cell = $"<td><a href=\"tel:@item.{p.Name}\" class=\"text-decoration-none\">@item.{p.Name}</a></td>";
            }
            else
            {
                cell = $"<td>@item.{p.Name}</td>";
            }
            sb.Append("                        ").Append(cell);
            if (i < listProps.Count - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    // -------------- Smart filters --------------

    private static string BuildFilterFields(EntitySpec entity)
    {
        var filterable = entity.FilterableProperties;
        if (filterable.Count == 0)
        {
            return "            <div class=\"col-12\"><p class=\"text-muted small mb-0\">No hay campos filtrables.</p></div>";
        }

        var server = entity.IsServerFiltering;
        var sb = new StringBuilder();
        for (var i = 0; i < filterable.Count; i++)
        {
            var p = filterable[i];
            AppendFilterField(sb, p, server);
            if (i < filterable.Count - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    private static void AppendFilterField(StringBuilder sb, PropertySpec p, bool server)
    {
        if (p.IsString)
        {
            var binding = server ? $"filter.{p.Name}" : $"filter_{p.CamelName}";
            var oninput = server ? string.Empty : " @bind:event=\"oninput\"";
            sb.AppendLine("            <div class=\"col-md-4\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <label for=\"filter_{p.CamelName}\" class=\"form-label small text-muted\">{p.Name}</label>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <input id=\"filter_{p.CamelName}\" type=\"text\" class=\"form-control form-control-sm\" @bind=\"{binding}\"{oninput} placeholder=\"Buscar por {p.Name.ToLowerInvariant()}...\" />");
            sb.Append("            </div>");
        }
        else if (p.IsBool)
        {
            var binding = server ? $"filter.{p.Name}" : $"filter_{p.CamelName}";
            sb.AppendLine("            <div class=\"col-md-3\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <label for=\"filter_{p.CamelName}\" class=\"form-label small text-muted\">{p.Name}</label>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <select id=\"filter_{p.CamelName}\" class=\"form-select form-select-sm\" @bind=\"{binding}\">");
            sb.AppendLine("                    <option value=\"\">Todos</option>");
            sb.AppendLine("                    <option value=\"true\">Sí</option>");
            sb.AppendLine("                    <option value=\"false\">No</option>");
            sb.AppendLine("                </select>");
            sb.Append("            </div>");
        }
        else if (p.IsNumeric)
        {
            var minBind = server ? $"filter.{p.Name}Min" : $"filter_{p.CamelName}Min";
            var maxBind = server ? $"filter.{p.Name}Max" : $"filter_{p.CamelName}Max";
            sb.AppendLine("            <div class=\"col-md-4\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <label class=\"form-label small text-muted\">{p.Name} (mín – máx)</label>");
            sb.AppendLine("                <div class=\"input-group input-group-sm\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                    <input type=\"number\" class=\"form-control\" @bind=\"{minBind}\" placeholder=\"mín\" />");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                    <input type=\"number\" class=\"form-control\" @bind=\"{maxBind}\" placeholder=\"máx\" />");
            sb.AppendLine("                </div>");
            sb.Append("            </div>");
        }
        else if (p.IsDateTime)
        {
            var fromBind = server ? $"filter.{p.Name}From" : $"filter_{p.CamelName}From";
            var toBind = server ? $"filter.{p.Name}To" : $"filter_{p.CamelName}To";
            sb.AppendLine("            <div class=\"col-md-4\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <label class=\"form-label small text-muted\">{p.Name} (desde – hasta)</label>");
            sb.AppendLine("                <div class=\"input-group input-group-sm\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                    <input type=\"date\" class=\"form-control\" @bind=\"{fromBind}\" />");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                    <input type=\"date\" class=\"form-control\" @bind=\"{toBind}\" />");
            sb.AppendLine("                </div>");
            sb.Append("            </div>");
        }
        else
        {
            // Guid and other fallbacks: text contains
            var binding = server ? $"filter.{p.Name}" : $"filter_{p.CamelName}";
            sb.AppendLine("            <div class=\"col-md-4\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <label for=\"filter_{p.CamelName}\" class=\"form-label small text-muted\">{p.Name}</label>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"                <input id=\"filter_{p.CamelName}\" type=\"text\" class=\"form-control form-control-sm\" @bind=\"{binding}\" placeholder=\"{p.Name}...\" />");
            sb.Append("            </div>");
        }
    }

    // -------------- Client-mode filter state / logic / reset / sort --------------

    private static string BuildFilterState(EntitySpec entity)
    {
        if (entity.IsServerFiltering) return string.Empty;
        var filterable = entity.FilterableProperties;
        if (filterable.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var emitted = false;
        foreach (var p in filterable)
        {
            if (emitted) sb.AppendLine();
            emitted = true;

            if (p.IsString)
            {
                sb.Append(CultureInfo.InvariantCulture, $"    private string? filter_{p.CamelName};");
            }
            else if (p.IsBool)
            {
                sb.Append(CultureInfo.InvariantCulture, $"    private bool? filter_{p.CamelName};");
            }
            else if (p.IsNumeric)
            {
                sb.Append(CultureInfo.InvariantCulture, $"    private {p.Type}? filter_{p.CamelName}Min;");
                sb.AppendLine();
                sb.Append(CultureInfo.InvariantCulture, $"    private {p.Type}? filter_{p.CamelName}Max;");
            }
            else if (p.IsDateTime)
            {
                sb.Append(CultureInfo.InvariantCulture, $"    private {p.Type}? filter_{p.CamelName}From;");
                sb.AppendLine();
                sb.Append(CultureInfo.InvariantCulture, $"    private {p.Type}? filter_{p.CamelName}To;");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture, $"    private string? filter_{p.CamelName};");
            }
        }
        return sb.ToString();
    }

    private static string BuildFilterLogic(EntitySpec entity)
    {
        if (entity.IsServerFiltering) return string.Empty;
        var filterable = entity.FilterableProperties;
        if (filterable.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var emitted = false;
        foreach (var p in filterable)
        {
            if (emitted) sb.AppendLine();
            emitted = true;

            if (p.IsString)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        if (!string.IsNullOrWhiteSpace(filter_{p.CamelName}))");
                sb.Append(CultureInfo.InvariantCulture, $"            q = q.Where(x => x.{p.Name} != null && x.{p.Name}.Contains(filter_{p.CamelName}!, StringComparison.OrdinalIgnoreCase));");
            }
            else if (p.IsBool)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        if (filter_{p.CamelName} is bool b_{p.CamelName})");
                sb.Append(CultureInfo.InvariantCulture, $"            q = q.Where(x => x.{p.Name} == b_{p.CamelName});");
            }
            else if (p.IsNumeric)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        if (filter_{p.CamelName}Min.HasValue) q = q.Where(x => x.{p.Name} >= filter_{p.CamelName}Min.Value);");
                sb.Append(CultureInfo.InvariantCulture, $"        if (filter_{p.CamelName}Max.HasValue) q = q.Where(x => x.{p.Name} <= filter_{p.CamelName}Max.Value);");
            }
            else if (p.IsDateTime)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        if (filter_{p.CamelName}From.HasValue) q = q.Where(x => x.{p.Name} >= filter_{p.CamelName}From.Value);");
                sb.Append(CultureInfo.InvariantCulture, $"        if (filter_{p.CamelName}To.HasValue) q = q.Where(x => x.{p.Name} <= filter_{p.CamelName}To.Value);");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        if (!string.IsNullOrWhiteSpace(filter_{p.CamelName}))");
                sb.Append(CultureInfo.InvariantCulture, $"            q = q.Where(x => x.{p.Name}.ToString()!.Contains(filter_{p.CamelName}!, StringComparison.OrdinalIgnoreCase));");
            }
        }
        return sb.ToString();
    }

    private static string BuildResetLogic(EntitySpec entity)
    {
        if (entity.IsServerFiltering) return string.Empty;
        var filterable = entity.FilterableProperties;
        if (filterable.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var emitted = false;
        foreach (var p in filterable)
        {
            if (emitted) sb.AppendLine();
            emitted = true;

            if (p.IsString)
            {
                sb.Append(CultureInfo.InvariantCulture, $"        filter_{p.CamelName} = null;");
            }
            else if (p.IsBool)
            {
                sb.Append(CultureInfo.InvariantCulture, $"        filter_{p.CamelName} = null;");
            }
            else if (p.IsNumeric)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        filter_{p.CamelName}Min = null;");
                sb.Append(CultureInfo.InvariantCulture, $"        filter_{p.CamelName}Max = null;");
            }
            else if (p.IsDateTime)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        filter_{p.CamelName}From = null;");
                sb.Append(CultureInfo.InvariantCulture, $"        filter_{p.CamelName}To = null;");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture, $"        filter_{p.CamelName} = null;");
            }
        }
        return sb.ToString();
    }

    private static string BuildSortCases(EntitySpec entity)
    {
        if (entity.IsServerFiltering) return string.Empty;
        var sortable = entity.SortableProperties;
        if (sortable.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < sortable.Count; i++)
        {
            var p = sortable[i];
            sb.Append(CultureInfo.InvariantCulture, $"            \"{p.Name}\" => sortDesc ? q.OrderByDescending(x => x.{p.Name}) : q.OrderBy(x => x.{p.Name}),");
            if (i < sortable.Count - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    // -------------- Server-mode tokens --------------

    private static string BuildFilterDtoProps(EntitySpec entity)
    {
        var filterable = entity.FilterableProperties;
        if (filterable.Count == 0)
        {
            return "    // No filterable properties; paging & sort still apply.";
        }

        var sb = new StringBuilder();
        var emitted = false;
        foreach (var p in filterable)
        {
            if (emitted) sb.AppendLine();
            emitted = true;

            if (p.IsString)
            {
                sb.Append(CultureInfo.InvariantCulture, $"    public string? {p.Name} {{ get; set; }}");
            }
            else if (p.IsBool)
            {
                sb.Append(CultureInfo.InvariantCulture, $"    public bool? {p.Name} {{ get; set; }}");
            }
            else if (p.IsNumeric)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public {p.Type}? {p.Name}Min {{ get; set; }}");
                sb.Append(CultureInfo.InvariantCulture, $"    public {p.Type}? {p.Name}Max {{ get; set; }}");
            }
            else if (p.IsDateTime)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public {p.Type}? {p.Name}From {{ get; set; }}");
                sb.Append(CultureInfo.InvariantCulture, $"    public {p.Type}? {p.Name}To {{ get; set; }}");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture, $"    public string? {p.Name} {{ get; set; }}");
            }
        }
        return sb.ToString();
    }

    private static string BuildPersistenceUsings(EntitySpec entity, string ns) =>
        entity.IsServerFiltering
            ? "using System.Linq.Expressions;\n"
            : string.Empty;

    private static string BuildPersistenceBody(EntitySpec entity)
    {
        if (!entity.IsServerFiltering) return string.Empty;
        return $"    Task<(IReadOnlyList<{entity.Name}> Items, int Total)> GetPagedAsync(IEnumerable<Expression<Func<{entity.Name}, bool>>> predicates, string? sortBy, bool sortDesc, int skip, int take, CancellationToken ct);";
    }

    private static string BuildDaUsings(EntitySpec entity, string ns) =>
        entity.IsServerFiltering
            ? "using System.Linq.Expressions;\nusing Microsoft.EntityFrameworkCore;\n"
            : string.Empty;

    private static string BuildDaBody(EntitySpec entity)
    {
        if (!entity.IsServerFiltering) return ";\n";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine($"    public async Task<(IReadOnlyList<{entity.Name}> Items, int Total)> GetPagedAsync(IEnumerable<Expression<Func<{entity.Name}, bool>>> predicates, string? sortBy, bool sortDesc, int skip, int take, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        IQueryable<{entity.Name}> q = Context.Set<{entity.Name}>().AsNoTracking();");
        sb.AppendLine();
        sb.AppendLine("        foreach (var predicate in predicates)");
        sb.AppendLine("            q = q.Where(predicate);");
        sb.AppendLine();
        sb.AppendLine("        q = sortBy switch");
        sb.AppendLine("        {");
        foreach (var p in entity.SortableProperties)
        {
            sb.AppendLine($"            \"{p.Name}\" => sortDesc ? q.OrderByDescending(x => x.{p.Name}) : q.OrderBy(x => x.{p.Name}),");
        }
        sb.AppendLine("            _ => q.OrderBy(x => x.Id),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        var total = await q.CountAsync(ct);");
        sb.AppendLine("        var items = await q.Skip(skip).Take(take).ToListAsync(ct);");
        sb.AppendLine("        return (items, total);");
        sb.AppendLine("    }");
        sb.Append('}');
        return sb.ToString();
    }

    private static string BuildContractUsings(EntitySpec entity, string ns) =>
        entity.IsServerFiltering
            ? $"using {ns}.Application.Models;\nusing {ns}.Application.Models.App;\n"
            : string.Empty;

    private static string BuildContractBody(EntitySpec entity)
    {
        if (!entity.IsServerFiltering) return string.Empty;
        return $"    Task<PagedResult<Models.App.{entity.Name}>> GetPagedAsync({entity.Name}Filter filter, CancellationToken ct);";
    }

    private static string BuildServiceUsings(EntitySpec entity, string ns) =>
        entity.IsServerFiltering
            ? $"using System.Linq.Expressions;\nusing {ns}.Application.Models;\nusing {ns}.Application.Models.App;\n"
            : string.Empty;

    private static string BuildServiceBody(EntitySpec entity)
    {
        if (!entity.IsServerFiltering) return ";\n";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine($"    public async Task<PagedResult<{entity.Name}Model>> GetPagedAsync({entity.Name}Filter filter, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var predicates = new List<Expression<Func<{entity.Name}Entity, bool>>>();");

        foreach (var p in entity.FilterableProperties)
            AppendServicePredicate(sb, p, entity.Name);

        sb.AppendLine();
        sb.AppendLine("        var (skip, take) = filter.ToSkipTake();");
        sb.AppendLine($"        var (items, total) = await {entity.Camel}DA.GetPagedAsync(predicates, filter.SortBy, filter.SortDesc, skip, take, ct);");
        sb.AppendLine($"        return new PagedResult<{entity.Name}Model>([.. mapper.ToModelList(items)], total, filter.Page, filter.PageSize);");
        sb.AppendLine("    }");
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendServicePredicate(StringBuilder sb, PropertySpec p, string entityName)
    {
        if (p.IsString)
        {
            sb.AppendLine($"        if (!string.IsNullOrWhiteSpace(filter.{p.Name}))");
            sb.AppendLine($"            predicates.Add(x => x.{p.Name} != null && x.{p.Name}.Contains(filter.{p.Name}!));");
        }
        else if (p.IsBool)
        {
            sb.AppendLine($"        if (filter.{p.Name} is bool b_{p.CamelName})");
            sb.AppendLine($"            predicates.Add(x => x.{p.Name} == b_{p.CamelName});");
        }
        else if (p.IsNumeric)
        {
            sb.AppendLine($"        if (filter.{p.Name}Min.HasValue) predicates.Add(x => x.{p.Name} >= filter.{p.Name}Min.Value);");
            sb.AppendLine($"        if (filter.{p.Name}Max.HasValue) predicates.Add(x => x.{p.Name} <= filter.{p.Name}Max.Value);");
        }
        else if (p.IsDateTime)
        {
            sb.AppendLine($"        if (filter.{p.Name}From.HasValue) predicates.Add(x => x.{p.Name} >= filter.{p.Name}From.Value);");
            sb.AppendLine($"        if (filter.{p.Name}To.HasValue) predicates.Add(x => x.{p.Name} <= filter.{p.Name}To.Value);");
        }
        else
        {
            sb.AppendLine($"        if (!string.IsNullOrWhiteSpace(filter.{p.Name}))");
            sb.AppendLine($"            predicates.Add(x => x.{p.Name}.ToString()!.Contains(filter.{p.Name}!));");
        }
    }

    private static string BuildControllerExtraUsings(EntitySpec entity) =>
        entity.IsServerFiltering
            ? string.Empty // models namespace already imported in the template
            : string.Empty;

    private static string BuildControllerBody(EntitySpec entity)
    {
        if (!entity.IsServerFiltering) return ";\n";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("    [HttpPost(\"query\")]");
        sb.AppendLine($"    public async Task<IActionResult> GetPaged([FromBody] {entity.Name}Filter filter, CancellationToken ct) =>");
        sb.AppendLine("        Ok(await Service.GetPagedAsync(filter, ct));");
        sb.Append('}');
        return sb.ToString();
    }

    private static string BuildApiClientExtraUsings(EntitySpec entity, string ns) =>
        entity.IsServerFiltering
            ? $"using {ns}.Application.Models;\n"
            : string.Empty;

    private static string BuildApiClientExtraMethods(EntitySpec entity)
    {
        if (!entity.IsServerFiltering) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"    public Task<Result<PagedResult<{entity.Name}>>> GetPagedAsync({entity.Name}Filter filter, CancellationToken ct) =>");
        sb.Append($"        PostAsync<PagedResult<{entity.Name}>, {entity.Name}Filter>(\"api/{entity.Lower}/query\", filter, ct);");
        return sb.ToString();
    }
}
