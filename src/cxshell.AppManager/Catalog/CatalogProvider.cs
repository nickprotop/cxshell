using System.Reflection;
using System.Text.Json;

namespace cxshell.AppManager.Catalog;

public interface ICatalogProvider
{
    Task<IReadOnlyList<CatalogEntry>> GetCatalogAsync(CancellationToken ct = default);
    string SourceDescription { get; }
}

internal static class CatalogJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

/// <summary>
/// Reads the hardcoded catalog bundled as an embedded resource. The schema is identical to the
/// future online index, so switching to <see cref="HttpCatalogProvider"/> requires no other change.
/// </summary>
public sealed class EmbeddedCatalogProvider : ICatalogProvider
{
    public string SourceDescription => "bundled catalog";

    public Task<IReadOnlyList<CatalogEntry>> GetCatalogAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("catalog.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("embedded catalog.json not found");
        using var stream = asm.GetManifestResourceStream(resName)!;
        var index = JsonSerializer.Deserialize<CatalogIndex>(stream, CatalogJson.Options)
            ?? new CatalogIndex();
        return Task.FromResult<IReadOnlyList<CatalogEntry>>(index.Apps);
    }
}

/// <summary>
/// Fetches the catalog index from an online repository (same schema as the embedded one).
/// Implemented for the future online repo; not wired into the composition root yet.
/// </summary>
public sealed class HttpCatalogProvider : ICatalogProvider
{
    private readonly HttpClient _http;
    private readonly string _indexUrl;

    public HttpCatalogProvider(HttpClient http, string indexUrl)
    {
        _http = http;
        _indexUrl = indexUrl;
    }

    public string SourceDescription => $"online ({_indexUrl})";

    public async Task<IReadOnlyList<CatalogEntry>> GetCatalogAsync(CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync(_indexUrl, ct);
        var index = JsonSerializer.Deserialize<CatalogIndex>(json, CatalogJson.Options)
            ?? new CatalogIndex();
        return index.Apps;
    }
}
