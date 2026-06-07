using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using cxshell.Shared;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace cxshell.Settings.Pages;

/// <summary>
/// Lets the user pick a desktop background. Each option applies live to
/// <c>windowSystem.DesktopBackground</c> via <see cref="DesktopPanels.ResolveBackground"/>,
/// the same resolver used at startup, so the preview and the persisted default stay in sync.
/// </summary>
public static class DesktopBackgroundPage
{
    private static readonly (string Key, string Label)[] Presets =
    {
        ("drift", "Drifting Gradient (animated)"),
        ("colorcycle", "Color Cycling (animated)"),
        ("pulse", "Pulse (animated)"),
        ("gradient", "Static Gradient"),
        ("solid", "Solid Color"),
        ("checkerboard", "Checkerboard"),
        ("dots", "Dots"),
        ("grid", "Grid"),
        ("none", "None"),
    };

    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        panel.AddControl(Ctl.Markup()
            .AddLine("[bold rgb(120,180,255)]Desktop Background[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Ctl.RuleBuilder()
            .WithTitle("Presets")
            .WithColor(new Color(60, 100, 160))
            .Build());

        foreach (var (key, label) in Presets)
        {
            var capturedKey = key;
            panel.AddControl(Ctl.Button($"  {label}  ")
                .WithBorder(ButtonBorderStyle.Rounded)
                .WithMargin(0, 1, 0, 0)
                .OnClick((_, _, _) =>
                {
                    windowSystem.DesktopBackground = DesktopPanels.ResolveBackground(capturedKey);
                    // Persist via the ConsoleEx registry so the choice survives restart.
                    DesktopPanels.SaveBackgroundKey(windowSystem, capturedKey);
                })
                .Build());
        }

        panel.AddControl(Ctl.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Animated effects respect the Animations setting. Your[/]")
            .AddLine("[dim]selection is saved and restored on next launch.[/]")
            .Build());
    }
}
