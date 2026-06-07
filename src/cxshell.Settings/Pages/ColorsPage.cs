using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using cxshell.Shared;

namespace cxshell.Settings.Pages;

public static class ColorsPage
{
    private static readonly (string Label, string Value)[] Accents =
    {
        ("Blue", "blue"), ("Green", "green"), ("Orange", "orange"),
        ("Pink", "pink"), ("Purple", "purple"),
    };

    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem ws)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,180,255)]Colors[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Accent")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var saved = SettingsStore.GetString(ws, "colors.accent", "blue");
        var selIndex = Math.Max(0, Array.FindIndex(Accents, a => a.Value == saved));

        var dd = Controls.Dropdown("Accent color");
        foreach (var (label, value) in Accents) dd.AddItem(label, value);
        panel.AddControl(dd
            .SelectedIndex(selIndex)
            .OnSelectedValueChanged((_, value) =>
            {
                if (value != null) SettingsStore.SetString(ws, "colors.accent", value);
            })
            .WithMargin(0, 1, 0, 1)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine("[bold]Theme palette preview[/]")
            .AddLine("[on rgb(40,80,160)]  Window Active Border   [/]")
            .AddLine("[on rgb(0,120,215)]  Button Background       [/]")
            .AddLine("[on rgb(50,50,50)]  Menu Background          [/]")
            .AddEmptyLine()
            .AddLine("[dim]Accent is saved to your profile and applied on next launch.[/]")
            .Build());
    }
}
