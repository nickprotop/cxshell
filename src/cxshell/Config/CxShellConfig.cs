namespace cxshell.Config;

public record CxShellConfig(
    string DesktopTitle = "cxshell",
    string AppsDirectory = "/usr/share/cxshell/apps",
    string AutostartConfigPath = "/etc/cxshell/autostart.conf",
    bool ShowClock = true,
    bool ShowNetworkStatus = true,
    bool ShowBatteryStatus = true,
    int SystemTrayUpdateIntervalSeconds = 5,
    // Desktop background selection key, resolved by DesktopPanels.ResolveBackground.
    // Default "dots" is a subtle static dot pattern.
    string DesktopBackground = "dots"
)
{
    public static CxShellConfig Default => new();
}
