using cxshell.AppManager.Catalog;
using cxshell.Apps;

namespace cxshell.AppManager;

public enum AppStatus { Available, Installed, UpdateAvailable }

/// <summary>A catalog entry joined with its installed state.</summary>
public sealed record AppView(CatalogEntry Entry, AppStatus Status, string? InstalledVersion);

/// <summary>
/// Joins the catalog with installed manifests to compute each app's status. Update detection is
/// best-effort/async (queries release tags / git refs) and refreshed on demand.
/// </summary>
public sealed class AppStateService
{
    private readonly ICatalogProvider _catalog;
    private readonly ManifestStore _store;
    private readonly InstallManager _installer;

    public AppStateService(ICatalogProvider catalog, ManifestStore store, InstallManager installer)
    {
        _catalog = catalog;
        _store = store;
        _installer = installer;
    }

    public async Task<IReadOnlyList<AppView>> GetAppsAsync(CancellationToken ct = default)
    {
        var catalog = await _catalog.GetCatalogAsync(ct);
        var installed = _store.EnumerateAll();

        var views = new List<AppView>();
        foreach (var entry in catalog)
        {
            if (installed.TryGetValue(entry.Id, out var manifest))
                views.Add(new AppView(entry, AppStatus.Installed, manifest.Version));
            else
                views.Add(new AppView(entry, AppStatus.Available, null));
        }
        return views.OrderBy(v => v.Entry.Order).ThenBy(v => v.Entry.Name).ToList();
    }

    /// <summary>Re-check update availability for installed apps (mutates statuses).</summary>
    public async Task<IReadOnlyList<AppView>> WithUpdateChecksAsync(
        IReadOnlyList<AppView> views, CancellationToken ct = default)
    {
        var result = new List<AppView>(views.Count);
        foreach (var v in views)
        {
            if (v.Status == AppStatus.Installed &&
                await _installer.IsUpdateAvailableAsync(v.Entry.Id, ct))
                result.Add(v with { Status = AppStatus.UpdateAvailable });
            else
                result.Add(v);
        }
        return result;
    }
}
