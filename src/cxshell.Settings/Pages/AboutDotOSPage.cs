using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace cxshell.Settings.Pages;

public static class AboutDotOSPage
{
    public static void Build(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold cyan]DotOS[/]")
            .AddEmptyLine()
            .AddLine("A minimal .NET-powered operating system shell")
            .AddLine("built with SharpConsoleUI (ConsoleEx).")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Details")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine("[bold]Version:[/] 0.1.0")
            .AddLine("[bold]License:[/] MIT")
            .AddLine("[bold]Desktop:[/] cxshell")
            .AddLine("[bold]UI Framework:[/] ConsoleEx / SharpConsoleUI")
            .AddEmptyLine()
            .AddLine("[dim]DotOS provides a console-based desktop environment[/]")
            .AddLine("[dim]with a terminal, file manager, and settings panel.[/]")
            .Build());
    }
}
