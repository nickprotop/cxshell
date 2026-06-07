using System.Text.Json.Serialization;
using cxshell.Apps;

namespace cxshell.AppManager.Catalog;

/// <summary>One installable distribution option for an app (binary or source).</summary>
public sealed class CatalogSource
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "binary"; // binary | source
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("uri")] public string Uri { get; set; } = "";       // X-DotOS-Source value
    [JsonPropertyName("build")] public BuildSpec? Build { get; set; }      // for kind == source
}

/// <summary>
/// One app in the catalog. Schema is identical whether the catalog is embedded (now) or fetched
/// from an online index (later) — only the provider differs.
/// </summary>
public sealed class CatalogEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("descriptionHtml")] public string? DescriptionHtml { get; set; }
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("logo")] public string? Logo { get; set; }
    [JsonPropertyName("screenshots")] public List<string> Screenshots { get; set; } = new();
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = new();
    [JsonPropertyName("group")] public string? Group { get; set; }
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("sources")] public List<CatalogSource> Sources { get; set; } = new();
    [JsonPropertyName("homepage")] public string? Homepage { get; set; }

    public AppInstallRequest ToInstallRequest() => new(
        Id, Name, Summary, Icon, Categories, Group, Order);
}

/// <summary>The catalog index (embedded or online), versioned for forward compat.</summary>
public sealed class CatalogIndex
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 1;
    [JsonPropertyName("apps")] public List<CatalogEntry> Apps { get; set; } = new();
}
