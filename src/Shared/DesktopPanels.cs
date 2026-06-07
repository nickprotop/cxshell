using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Panel;
using SharpConsoleUI.Rendering;

namespace cxshell.Shared;

/// <summary>
/// Shared desktop chrome for cxshell and the standalone apps: the start-menu
/// styling, the top/bottom panel layout (start menu + taskbar + clock), and the
/// desktop-background presets. Compiled into each app project via a linked
/// &lt;Compile Include&gt; so there is one source of truth and no shared assembly.
/// </summary>
internal static class DesktopPanels
{
    /// <summary>Primary DotOS accent gradient, reused by the start menu and the default background.</summary>
    public static GradientBackground BrandGradient { get; } = new(
        ColorGradient.FromColors(new Color(25, 25, 60), new Color(15, 15, 35)),
        GradientDirection.Vertical);

    /// <summary>Rich start-menu styling shared by every entrypoint.</summary>
    public static StartMenuOptions DefaultStartMenuOptions() => new()
    {
        ShowWindowList = true,
        ShowIcons = true,
        HeaderIcon = ">_",
        AppName = "DotOS",
        SidebarStyle = StartMenuSidebarStyle.IconLabel,
        BackgroundGradient = BrandGradient,
    };

    /// <summary>
    /// Bottom panel: start menu (left), taskbar (center), clock (right).
    /// The clock self-updates via its own internal timer.
    /// </summary>
    public static PanelBuilder ConfigureBottomPanel(PanelBuilder panel) => panel
        .Left(Elements.StartMenu()
            .WithText("[bold cyan]>_ DotOS[/]")
            .WithOptions(DefaultStartMenuOptions()))
        .Center(Elements.TaskBar())
        .Right(Elements.Clock().WithFormat("HH:mm"));

    /// <summary>Top panel: a single status-text element that carries the desktop title.</summary>
    public static PanelBuilder ConfigureTopPanel(PanelBuilder panel) => panel
        .Left(Elements.StatusText(""));

    /// <summary>
    /// Resolve a background selection key (see <see cref="cxshell.Config.CxShellConfig"/>)
    /// to a concrete config. <paramref name="animationsEnabled"/> downgrades animated presets
    /// to their static equivalent so low-power targets aren't taxed. Returns null for "none".
    /// </summary>
    public static DesktopBackgroundConfig? ResolveBackground(string key, bool animationsEnabled = true)
    {
        switch (key)
        {
            case "none":
                return null;
            case "solid":
                return DesktopBackgroundConfig.FromColor(new Color(15, 15, 35));
            case "gradient":
                return DesktopBackgroundConfig.FromGradient(
                    ColorGradient.FromColors(new Color(10, 15, 40), new Color(25, 40, 80)),
                    GradientDirection.DiagonalDown);
            case "checkerboard":
                return DesktopBackgroundConfig.FromPattern(DesktopPatterns.Checkerboard);
            case "dots":
                return DesktopBackgroundConfig.FromPattern(DesktopPatterns.Dots);
            case "grid":
                return DesktopBackgroundConfig.FromPattern(DesktopPatterns.Grid);
            case "pulse":
                return animationsEnabled
                    ? DesktopEffects.Pulse(new Color(15, 25, 60))
                    : DesktopBackgroundConfig.FromColor(new Color(15, 25, 60));
            case "colorcycle":
                return animationsEnabled
                    ? DesktopEffects.ColorCycling()
                    : DesktopBackgroundConfig.FromColor(new Color(15, 15, 35));
            case "drift":
            default:
                return animationsEnabled
                    ? DesktopEffects.DriftingGradient(
                        new Color(10, 15, 40), new Color(25, 40, 80),
                        cycleDurationSeconds: 12, intervalMs: 180)
                    : DesktopBackgroundConfig.FromGradient(
                        ColorGradient.FromColors(new Color(10, 15, 40), new Color(25, 40, 80)),
                        GradientDirection.DiagonalDown);
        }
    }

    /// <summary>The default desktop background (subtle static dot pattern).</summary>
    public static DesktopBackgroundConfig? DefaultDesktopBackground(bool animationsEnabled = true)
        => ResolveBackground("dots", animationsEnabled);

    // --- Persistence via the ConsoleEx registry (RegistryStateService) ---------------------
    // The registry is configured by DesktopShell (RegistryConfiguration.ForFile). Standalone
    // apps may have no registry; the helpers below are null-safe and become no-ops there.

    private const string BackgroundSection = "cxshell/Desktop";
    private const string BackgroundKey = "background";

    /// <summary>
    /// Read the persisted background selection key, falling back to <paramref name="fallback"/>
    /// (typically the config default) when the registry is absent or unset.
    /// </summary>
    public static string ReadBackgroundKey(ConsoleWindowSystem windowSystem, string fallback)
    {
        var registry = windowSystem.RegistryStateService;
        if (registry == null) return fallback;
        var value = registry.OpenSection(BackgroundSection).GetString(BackgroundKey, fallback);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>
    /// Persist the chosen background selection key. No-op when no registry is configured.
    /// </summary>
    public static void SaveBackgroundKey(ConsoleWindowSystem windowSystem, string key)
    {
        var registry = windowSystem.RegistryStateService;
        if (registry == null) return;
        registry.OpenSection(BackgroundSection).SetString(BackgroundKey, key);
        registry.Save();
    }
}
