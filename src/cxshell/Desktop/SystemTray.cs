using System.Globalization;
using System.Net.NetworkInformation;

namespace cxshell.Desktop;

public static class SystemTray
{
    public static string GetStatusText(bool showClock, bool showBattery, bool showNetwork)
    {
        var parts = new List<string>();

        if (showNetwork)
        {
            var net = GetNetworkText();
            if (net.Length > 0)
                parts.Add(net);
        }

        if (showBattery)
        {
            var bat = GetBatteryText();
            if (bat.Length > 0)
                parts.Add(bat);
        }

        if (showClock)
        {
            parts.Add(GetClockText());
        }

        return string.Join("  ", parts);
    }

    public static string GetClockText()
    {
        return DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public static string GetBatteryText()
    {
        try
        {
            var capacityPath = "/sys/class/power_supply/BAT0/capacity";
            var statusPath = "/sys/class/power_supply/BAT0/status";

            if (!File.Exists(capacityPath))
                return "";

            var capacity = File.ReadAllText(capacityPath).Trim();
            var status = File.Exists(statusPath) ? File.ReadAllText(statusPath).Trim() : "Unknown";
            var icon = status == "Charging" ? "[yellow]CHG[/]" : "[green]BAT[/]";

            return $"{icon} {capacity}%";
        }
        catch
        {
            return "";
        }
    }

    public static string GetNetworkText()
    {
        try
        {
            var hasConnection = NetworkInterface.GetAllNetworkInterfaces()
                .Any(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            return hasConnection ? "[green]NET[/]" : "[red]NO NET[/]";
        }
        catch
        {
            return "[dim]NET?[/]";
        }
    }
}
