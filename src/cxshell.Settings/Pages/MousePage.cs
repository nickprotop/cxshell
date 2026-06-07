using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using cxshell.Shared;

namespace cxshell.Settings.Pages;

public static class MousePage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,220,160)]Mouse[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Behavior")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Checkbox("Enable mouse input")
            .Checked(SettingsStore.GetBool(ws, "mouse.enabled", true))
            .OnCheckedChanged((_, isChecked) => SettingsStore.SetBool(ws, "mouse.enabled", isChecked))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Checkbox("Natural scrolling")
            .Checked(SettingsStore.GetBool(ws, "mouse.naturalScroll", false))
            .OnCheckedChanged((_, isChecked) => SettingsStore.SetBool(ws, "mouse.naturalScroll", isChecked))
            .WithMargin(0, 1, 0, 1)
            .Build());

        panel.AddControl(Controls.Markup().AddLine("[bold]Pointer speed[/]").Build());
        panel.AddControl(Controls.Slider()
            .WithRange(1, 10).WithStep(1)
            .WithValue(SettingsStore.GetInt(ws, "mouse.speed", 5))
            .OnValueChanged((_, v) => SettingsStore.SetInt(ws, "mouse.speed", (int)v))
            .WithMargin(0, 0, 0, 1)
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Status")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine("[bold]Console Mouse Support:[/] Provided by kmscon")
            .AddEmptyLine()
            .AddLine("[dim]Settings are saved to your profile. Mouse input is handled by[/]")
            .AddLine("[dim]kmscon; speed/scroll are advisory at runtime.[/]")
            .Build());
    }
}
