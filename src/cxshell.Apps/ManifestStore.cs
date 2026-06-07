namespace cxshell.Apps;

/// <summary>
/// Enumerates DotOS app manifests across the standard search paths and resolves them by id with
/// user-overrides-system precedence (DotOS Application Standard §3.3):
///   1. system (baked, read-only): <c>/usr/share/cxshell/apps</c>
///   2. user / App-Manager (writable): <c>$XDG_DATA_HOME/cxshell/apps</c>
///      (default <c>~/.local/share/cxshell/apps</c>)
/// A manifest in a higher-precedence dir wholly overrides a same-id one below it.
/// </summary>
public sealed class ManifestStore
{
    public string SystemDir { get; }
    public string UserDir { get; }

    public ManifestStore(string? systemDir = null, string? userDir = null)
    {
        SystemDir = systemDir ?? "/usr/share/cxshell/apps";
        UserDir = userDir ?? DefaultUserDir();
    }

    public static string DefaultUserDir()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");
        return Path.Combine(baseDir, "cxshell", "apps");
    }

    /// <summary>The directory where new (user-scope) manifests are written.</summary>
    public string WritableDir => UserDir;

    /// <summary>
    /// All launchable entries, deduped by id with user precedence, sorted by group then order
    /// then name. Non-application/NoDisplay entries are excluded.
    /// </summary>
    public IReadOnlyList<DesktopEntry> Enumerate()
    {
        var byId = new Dictionary<string, DesktopEntry>(StringComparer.Ordinal);

        // Lower precedence first, higher overwrites.
        foreach (var dir in new[] { SystemDir, UserDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.desktop"))
            {
                try
                {
                    var entry = DesktopEntry.ParseFile(file);
                    byId[entry.Id] = entry; // same id in a later dir overrides
                }
                catch
                {
                    // Skip malformed manifests rather than failing the whole scan.
                }
            }
        }

        return byId.Values
            .Where(e => e.IsLaunchable)
            .OrderBy(e => e.Group_DotOS ?? "")
            .ThenBy(e => e.Order)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>All entries (including NoDisplay/non-app) keyed by id, user precedence applied.</summary>
    public IReadOnlyDictionary<string, DesktopEntry> EnumerateAll()
    {
        var byId = new Dictionary<string, DesktopEntry>(StringComparer.Ordinal);
        foreach (var dir in new[] { SystemDir, UserDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.desktop"))
            {
                try { var e = DesktopEntry.ParseFile(file); byId[e.Id] = e; }
                catch { }
            }
        }
        return byId;
    }

    public string UserManifestPath(string id) => Path.Combine(UserDir, id + ".desktop");

    /// <summary>True if a system (baked) manifest with this id exists.</summary>
    public bool HasSystemManifest(string id) =>
        File.Exists(Path.Combine(SystemDir, id + ".desktop"));
}
