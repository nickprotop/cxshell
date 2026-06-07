using SharpConsoleUI;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Helpers;
using cxshell.Settings.Pages;

namespace cxshell.Settings;

/// <summary>
/// DotOS settings entry point. The framework pages (Theme, Rendering, Animations, Status Bar,
/// Logging, System) are provided by ConsoleEx's built-in <see cref="SettingsDialog"/>. DotOS
/// registers its own groups/pages through <c>SettingsRegistrationService</c>; the built-in
/// dialog appends them after its own sections.
/// </summary>
public static class SettingsWindow
{
    private static bool _registered;

    /// <summary>
    /// Register DotOS-specific settings groups once. Safe to call repeatedly (idempotent).
    /// </summary>
    public static void RegisterPages(ConsoleWindowSystem windowSystem)
    {
        if (_registered) return;
        _registered = true;

        var reg = windowSystem.SettingsRegistrationService;

        reg.RegisterGroup("Desktop", new Color(120, 180, 255), g =>
        {
            g.AddPage("Colors", icon: "◑", subtitle: "Customize theme colors",
                content: panel => ColorsPage.Build(panel, windowSystem));
            g.AddPage("Background", icon: "▩", subtitle: "Desktop background and effects",
                content: panel => DesktopBackgroundPage.Build(panel, windowSystem));
        });

        reg.RegisterGroup("Input", new Color(120, 220, 160), g =>
        {
            g.AddPage("Keyboard", icon: "⌨", subtitle: "Keyboard layout and repeat",
                content: panel => KeyboardPage.Build(panel, windowSystem));
            g.AddPage("Mouse", icon: "⊙", subtitle: "Mouse behavior",
                content: panel => MousePage.Build(panel, windowSystem));
        });

        reg.RegisterGroup("Panel", new Color(252, 152, 103), g =>
        {
            g.AddPage("Tray", icon: "▬", subtitle: "Clock, network and battery indicators",
                content: panel => TrayPage.Build(panel, windowSystem));
        });

        reg.RegisterGroup("Network", new Color(255, 97, 136), g =>
        {
            g.AddPage("Connections", icon: "⇄", subtitle: "Network interfaces and status",
                content: ConnectionsPage.Build);
        });

        reg.RegisterGroup("About", new Color(171, 157, 242), g =>
        {
            g.AddPage("System", icon: "⊞", subtitle: "OS and hardware information",
                content: InfoPage.Build);
            g.AddPage("DotOS", icon: "ℹ", subtitle: "Version and license",
                content: AboutDotOSPage.Build);
            g.AddPage("ConsoleEx", icon: "ℹ", subtitle: "UI framework internals",
                content: panel => AboutConsoleExPage.Build(panel, windowSystem));
        });
    }

    /// <summary>
    /// Register DotOS pages (if not already) and open the built-in settings dialog.
    /// </summary>
    public static void Open(ConsoleWindowSystem windowSystem, Window? parentWindow = null)
    {
        RegisterPages(windowSystem);
        SettingsDialog.Show(windowSystem, parentWindow);
    }
}
