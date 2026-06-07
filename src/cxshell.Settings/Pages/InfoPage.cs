using System.Runtime.InteropServices;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace cxshell.Settings.Pages;

public static class InfoPage
{
    public static void Build(ScrollablePanelControl panel)
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var uptimeStr = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";

        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(171,157,242)]System Information[/]")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Operating System")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"[bold]OS:[/] {RuntimeInformation.OSDescription}")
            .AddLine($"[bold]Architecture:[/] {RuntimeInformation.OSArchitecture}")
            .AddLine($"[bold]Machine Name:[/] {Environment.MachineName}")
            .AddLine($"[bold]User:[/] {Environment.UserName}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Hardware")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"[bold]CPUs:[/] {Environment.ProcessorCount}")
            .AddLine($"[bold]Uptime:[/] {uptimeStr}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Runtime")
            .WithColor(new Color(60, 100, 160))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"[bold].NET:[/] {RuntimeInformation.FrameworkDescription}")
            .AddLine($"[bold]Timezone:[/] {TimeZoneInfo.Local.DisplayName}")
            .Build());
    }
}
