using SharpConsoleUI;

namespace cxshell.Shared;

/// <summary>
/// Registry-backed typed settings, persisted via ConsoleEx's <c>RegistryStateService</c> under the
/// <c>cxshell/Settings</c> section. Null-safe: when no registry is configured (e.g. the
/// standalone Settings app), reads return defaults and writes are no-ops. Every setter saves
/// immediately so changes are durable.
/// </summary>
internal static class SettingsStore
{
    private const string Section = "cxshell/Settings";

    public static bool GetBool(ConsoleWindowSystem ws, string key, bool def)
    {
        var reg = ws.RegistryStateService;
        return reg == null ? def : reg.OpenSection(Section).GetBool(key, def);
    }

    public static void SetBool(ConsoleWindowSystem ws, string key, bool val)
    {
        var reg = ws.RegistryStateService;
        if (reg == null) return;
        reg.OpenSection(Section).SetBool(key, val);
        reg.Save();
    }

    public static int GetInt(ConsoleWindowSystem ws, string key, int def)
    {
        var reg = ws.RegistryStateService;
        return reg == null ? def : reg.OpenSection(Section).GetInt(key, def);
    }

    public static void SetInt(ConsoleWindowSystem ws, string key, int val)
    {
        var reg = ws.RegistryStateService;
        if (reg == null) return;
        reg.OpenSection(Section).SetInt(key, val);
        reg.Save();
    }

    public static string GetString(ConsoleWindowSystem ws, string key, string def)
    {
        var reg = ws.RegistryStateService;
        return reg == null ? def : reg.OpenSection(Section).GetString(key, def);
    }

    public static void SetString(ConsoleWindowSystem ws, string key, string val)
    {
        var reg = ws.RegistryStateService;
        if (reg == null) return;
        reg.OpenSection(Section).SetString(key, val);
        reg.Save();
    }
}
