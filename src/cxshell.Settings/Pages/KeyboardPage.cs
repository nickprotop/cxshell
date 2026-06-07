using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using cxshell.Shared;

namespace cxshell.Settings.Pages;

public static class KeyboardPage
{
    private static readonly (string Label, string Value)[] Layouts =
    {
        ("US (English)", "us"), ("UK (English)", "gb"), ("German", "de"),
        ("French", "fr"), ("Spanish", "es"), ("Greek", "gr"),
    };

    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,220,160)]Keyboard[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Layout")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var savedLayout = SettingsStore.GetString(ws, "keyboard.layout", "us");
        var selIndex = Math.Max(0, Array.FindIndex(Layouts, l => l.Value == savedLayout));

        var dd = Controls.Dropdown("Keyboard Layout");
        foreach (var (label, value) in Layouts) dd.AddItem(label, value);
        panel.AddControl(dd
            .SelectedIndex(selIndex)
            .OnSelectedValueChanged((_, value) =>
            {
                if (value != null) SettingsStore.SetString(ws, "keyboard.layout", value);
            })
            .WithMargin(0, 0, 0, 1)
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Repeat")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup().AddLine("[bold]Repeat delay (ms)[/]").Build());
        panel.AddControl(Controls.Slider()
            .WithRange(150, 1000).WithStep(50)
            .WithValue(SettingsStore.GetInt(ws, "keyboard.repeatDelayMs", 500))
            .OnValueChanged((_, v) => SettingsStore.SetInt(ws, "keyboard.repeatDelayMs", (int)v))
            .WithMargin(0, 0, 0, 1)
            .Build());

        panel.AddControl(Controls.Markup().AddLine("[bold]Repeat rate (ms)[/]").Build());
        panel.AddControl(Controls.Slider()
            .WithRange(20, 200).WithStep(10)
            .WithValue(SettingsStore.GetInt(ws, "keyboard.repeatRateMs", 50))
            .OnValueChanged((_, v) => SettingsStore.SetInt(ws, "keyboard.repeatRateMs", (int)v))
            .WithMargin(0, 0, 0, 1)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Saved to your profile. Layout and repeat are applied by kmscon[/]")
            .AddLine("[dim]on session start (advisory at runtime).[/]")
            .Build());
    }
}
