using System.Text;

namespace cxshell.Apps;

/// <summary>
/// A freedesktop Desktop Entry (<c>.desktop</c>, INI syntax) profiled for DotOS, per the
/// DotOS Application Standard. Parses the <c>[Desktop Entry]</c> group, exposes the standard
/// keys DotOS uses plus the <c>X-DotOS-*</c> extensions, and round-trips back to text.
///
/// Forward-compatibility: unknown keys are preserved on round-trip, and unknown keys/extensions
/// are ignored by consumers.
/// </summary>
public sealed class DesktopEntry
{
    private const string Group = "Desktop Entry";

    /// <summary>All key/value pairs in the [Desktop Entry] group, in file order.</summary>
    private readonly List<KeyValuePair<string, string>> _pairs = new();

    /// <summary>The app id — the manifest filename without the <c>.desktop</c> suffix.</summary>
    public string Id { get; set; } = "";

    public string? Get(string key) =>
        _pairs.FirstOrDefault(p => p.Key.Equals(key, StringComparison.Ordinal)).Value;

    public void Set(string key, string? value)
    {
        var idx = _pairs.FindIndex(p => p.Key.Equals(key, StringComparison.Ordinal));
        if (value == null)
        {
            if (idx >= 0) _pairs.RemoveAt(idx);
            return;
        }
        if (idx >= 0) _pairs[idx] = new(key, value);
        else _pairs.Add(new(key, value));
    }

    // --- Standard keys -------------------------------------------------------------------
    public string Type => Get("Type") ?? "Application";
    public string Name => Get("Name") ?? Id;
    public string? Comment => Get("Comment");
    public string? Exec => Get("Exec");
    public string? Icon => Get("Icon");
    public string? TryExec => Get("TryExec");
    public bool NoDisplay => ParseBool(Get("NoDisplay"));

    /// <summary>Freedesktop Categories (the trailing <c>;</c>-separated list), empty if none.</summary>
    public IReadOnlyList<string> Categories =>
        (Get("Categories") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // --- X-DotOS-* extensions ------------------------------------------------------------
    public string? Group_DotOS => Get("X-DotOS-Group");
    public int Order => int.TryParse(Get("X-DotOS-Order"), out var n) ? n : 0;
    public bool Maximize => ParseBool(Get("X-DotOS-Maximize"), defaultValue: false);
    public bool Builtin => ParseBool(Get("X-DotOS-Builtin"));
    public string? Source => Get("X-DotOS-Source");
    public string? Version => Get("X-DotOS-Version");
    public string? InstallPath => Get("X-DotOS-InstallPath");
    public int Schema => int.TryParse(Get("X-DotOS-Schema"), out var n) ? n : 1;

    /// <summary>Whether this entry should appear in the launcher (Application, not hidden).</summary>
    public bool IsLaunchable =>
        Type.Equals("Application", StringComparison.Ordinal) && !NoDisplay;

    private static bool ParseBool(string? v, bool defaultValue = false) =>
        v == null ? defaultValue : v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parse a <c>.desktop</c> file's text. <paramref name="id"/> is the filename stem.
    /// Only the <c>[Desktop Entry]</c> group is read; other groups (actions) are ignored.
    /// </summary>
    public static DesktopEntry Parse(string text, string id)
    {
        var entry = new DesktopEntry { Id = id };
        var inGroup = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            if (line[0] == '[')
            {
                inGroup = line.Equals($"[{Group}]", StringComparison.Ordinal);
                continue;
            }
            if (!inGroup) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            entry._pairs.Add(new(key, val));
        }
        return entry;
    }

    public static DesktopEntry ParseFile(string path) =>
        Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));

    /// <summary>Serialize back to <c>.desktop</c> text.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(Group).Append("]\n");
        foreach (var (k, v) in _pairs)
            sb.Append(k).Append('=').Append(v).Append('\n');
        return sb.ToString();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ToString());
    }
}
