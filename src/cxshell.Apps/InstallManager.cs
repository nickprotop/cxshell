namespace cxshell.Apps;

/// <summary>
/// Orchestrates install / update / remove against the manifest store, dispatching to the right
/// <see cref="IInstaller"/> by source kind/scheme and writing the resulting user-scope
/// <c>.desktop</c> manifest. DotOS Application Standard §5.2.
/// </summary>
public sealed class InstallManager
{
    private readonly ManifestStore _store;
    private readonly IReadOnlyList<IInstaller> _installers;

    /// <summary>User-scope binary install root (default <c>~/.local/bin</c>).</summary>
    public string UserBinDir { get; }

    public InstallManager(ManifestStore store, IEnumerable<IInstaller> installers, string? userBinDir = null)
    {
        _store = store;
        _installers = installers.ToList();
        UserBinDir = userBinDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
    }

    /// <summary>
    /// Install <paramref name="meta"/> from the chosen <paramref name="sourceUri"/>, then write a
    /// user manifest. <paramref name="meta"/> supplies display fields (Name/Comment/Categories/etc).
    /// </summary>
    public async Task InstallAsync(AppInstallRequest meta, string sourceUri,
        IProgress<InstallProgress> progress, CancellationToken ct = default, BuildSpec? build = null)
    {
        if (!AppSource.TryParse(sourceUri, out var source))
            throw new ArgumentException($"invalid source: {sourceUri}");
        if (source.Kind == SourceKind.Source)
        {
            source.Build = build
                ?? throw new ArgumentException("source distribution requires a build descriptor");
        }

        var installer = _installers.FirstOrDefault(i => i.CanHandle(source))
            ?? throw new NotSupportedException($"no installer for {source.Kind}+{source.Scheme}");

        var idLeaf = meta.Id.Split('.').Last();
        var result = await installer.InstallAsync(source, idLeaf, UserBinDir, progress, ct);

        progress.Report(new("Writing app manifest…"));
        WriteManifest(meta, source, result);
        progress.Report(new($"Registered '{meta.Name}' in the App Manager and Start menu."));
        progress.Report(new($"{meta.Name} {result.Version} installed."));
    }

    /// <summary>Check whether a newer version is available for an installed app.</summary>
    public async Task<bool> IsUpdateAvailableAsync(string id, CancellationToken ct = default)
    {
        if (!_store.EnumerateAll().TryGetValue(id, out var entry)) return false;
        if (!AppSource.TryParse(entry.Source, out var source)) return false;
        var installer = _installers.FirstOrDefault(i => i.CanHandle(source));
        if (installer == null) return false;
        var latest = await installer.QueryLatestVersionAsync(source, ct);
        return latest != null && !string.Equals(latest, entry.Version, StringComparison.Ordinal);
    }

    /// <summary>Reinstall from the installed app's recorded source (its update channel).</summary>
    public async Task UpdateAsync(string id, IProgress<InstallProgress> progress, CancellationToken ct = default)
    {
        if (!_store.EnumerateAll().TryGetValue(id, out var entry) || entry.Source == null)
            throw new InvalidOperationException($"{id} is not installed or has no source");
        var meta = AppInstallRequest.FromEntry(entry);
        var build = BuildSpec.FromJson(entry.Get("X-DotOS-Build")); // recorded at install for source apps
        await InstallAsync(meta, entry.Source, progress, ct, build);
    }

    /// <summary>
    /// Remove a user-installed app (delete binary + user manifest). A baked system app can't be
    /// deleted; instead a user override with NoDisplay=true is written to hide it.
    /// </summary>
    public void Remove(string id)
    {
        var all = _store.EnumerateAll();
        if (all.TryGetValue(id, out var entry) && entry.InstallPath is { } p && File.Exists(p))
        {
            try { File.Delete(p); } catch { }
        }

        var userManifest = _store.UserManifestPath(id);
        if (_store.HasSystemManifest(id))
        {
            // Can't delete the baked system manifest → mask it with a user override.
            var mask = new DesktopEntry { Id = id };
            mask.Set("Type", "Application");
            mask.Set("Name", entry?.Name ?? id);
            mask.Set("NoDisplay", "true");
            mask.Save(userManifest);
        }
        else if (File.Exists(userManifest))
        {
            File.Delete(userManifest);
        }
    }

    /// <summary>Remove an app. For script-installed apps (manifest source has an uninstaller),
    /// run the publisher uninstaller / known-path cleanup first, then remove the manifest.</summary>
    public async Task RemoveAsync(string id, IProgress<InstallProgress> progress, CancellationToken ct = default)
    {
        var all = _store.EnumerateAll();
        all.TryGetValue(id, out var entry);

        if (entry?.Source is { } srcUri
            && AppSource.TryParse(srcUri, out var source)
            && source.Uninstaller != null)
        {
            var scriptInstaller = _installers.OfType<ScriptInstaller>().FirstOrDefault();
            if (scriptInstaller != null)
            {
                var idLeaf = id.Split('.').Last();
                progress.Report(new($"Removing {entry.Name ?? id}…"));
                await scriptInstaller.RemoveAsync(source, idLeaf, progress, ct);
            }
        }

        // Manifest cleanup (and binary delete for non-script apps) — reuse existing logic.
        Remove(id);
        progress.Report(new("Removed.", 1.0));
    }

    private void WriteManifest(AppInstallRequest meta, AppSource source, InstallResult result)
    {
        var e = new DesktopEntry { Id = meta.Id };
        e.Set("Type", "Application");
        e.Set("Name", meta.Name);
        if (meta.Comment is { } c) e.Set("Comment", c);
        e.Set("Exec", result.ExecPath);
        if (meta.Icon is { } ic) e.Set("Icon", ic);
        if (meta.Categories.Count > 0) e.Set("Categories", string.Join(';', meta.Categories) + ";");
        e.Set("Terminal", "true");
        e.Set("TryExec", result.ExecPath);
        if (meta.Group is { } g) e.Set("X-DotOS-Group", g);
        if (meta.Order != 0) e.Set("X-DotOS-Order", meta.Order.ToString());
        // Host windows open centered by default; apps opt into maximized via X-DotOS-Maximize=true.
        e.Set("X-DotOS-Source", source.Raw);
        e.Set("X-DotOS-Version", result.Version);
        e.Set("X-DotOS-InstallPath", result.ExecPath);
        if (source.Build is { } b) e.Set("X-DotOS-Build", b.ToJson()); // recorded for source rebuilds
        e.Save(_store.UserManifestPath(meta.Id));
    }
}

/// <summary>Display + identity fields needed to write an installed app's manifest.</summary>
public sealed record AppInstallRequest(
    string Id,
    string Name,
    string? Comment = null,
    string? Icon = null,
    IReadOnlyList<string>? CategoriesList = null,
    string? Group = null,
    int Order = 0)
{
    public IReadOnlyList<string> Categories => CategoriesList ?? Array.Empty<string>();

    public static AppInstallRequest FromEntry(DesktopEntry e) => new(
        e.Id, e.Name, e.Comment, e.Icon, e.Categories, e.Group_DotOS, e.Order);
}
