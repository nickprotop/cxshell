using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using cxshell.Shared;

namespace cxshell.Settings.Pages;

/// <summary>
/// Tray indicator toggles (clock / network / battery), persisted to the registry. DesktopShell
/// reads these at startup (falling back to CxShellConfig defaults). Keys live under
/// <c>cxshell/Settings</c> as <c>tray.showClock|showNetwork|showBattery</c>.
/// </summary>
public static class TrayPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(252,152,103)]Tray[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Indicators")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Checkbox("Show clock")
            .Checked(SettingsStore.GetBool(ws, "tray.showClock", true))
            .OnCheckedChanged((_, v) => SettingsStore.SetBool(ws, "tray.showClock", v))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Checkbox("Show network status")
            .Checked(SettingsStore.GetBool(ws, "tray.showNetwork", true))
            .OnCheckedChanged((_, v) => SettingsStore.SetBool(ws, "tray.showNetwork", v))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Checkbox("Show battery status")
            .Checked(SettingsStore.GetBool(ws, "tray.showBattery", true))
            .OnCheckedChanged((_, v) => SettingsStore.SetBool(ws, "tray.showBattery", v))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Applied to the desktop tray on next launch.[/]")
            .Build());
    }
}
