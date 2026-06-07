using System.Reflection;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;

namespace cxshell.Settings.Pages;

public static class AboutConsoleExPage
{
    public static void Build(ScrollablePanelControl panel, ConsoleWindowSystem windowSystem)
    {
        var driverType = windowSystem.ConsoleDriver.GetType().Name;
        var theme = windowSystem.ThemeStateService.CurrentTheme;
        var themeName = theme?.GetType().Name ?? "Unknown";
        var themeCount = ThemeRegistry.Count;
        var animationCount = windowSystem.Animations.ActiveCount;
        var assemblyVersion = typeof(ConsoleWindowSystem).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ConsoleWindowSystem).Assembly.GetName().Version?.ToString()
            ?? "Unknown";

        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(120,180,255)]SharpConsoleUI (ConsoleEx)[/]")
            .AddEmptyLine()
            .AddLine("[dim]Console-based UI framework for .NET[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Framework")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"[bold]Version:[/] {assemblyVersion}")
            .AddLine($"[bold]Driver:[/] {driverType}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Rendering")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"[bold]Active Animations:[/] {animationCount}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Theming")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"[bold]Current Theme:[/] {themeName}")
            .AddLine($"[bold]Registered Themes:[/] {themeCount}")
            .Build());
    }
}
