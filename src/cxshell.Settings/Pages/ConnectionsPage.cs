using System.Net;
using System.Net.NetworkInformation;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace cxshell.Settings.Pages;

public static class ConnectionsPage
{
    public static void Build(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold rgb(255,97,136)]Network Status[/]")
            .AddEmptyLine()
            .AddLine($"[bold]Hostname:[/] {Dns.GetHostName()}")
            .AddEmptyLine()
            .Build());

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("Interfaces")
            .WithColor(new Color(60, 100, 160))
            .Build());

        var markup = Controls.Markup();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                             && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                markup.AddLine($"[cyan]{ni.Name}[/] ({ni.NetworkInterfaceType})");
                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                        || addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        markup.AddLine($"  {addr.Address}");
                    }
                }
                markup.AddEmptyLine();
            }
        }
        catch
        {
            markup.AddLine("[red]Unable to enumerate network interfaces[/]");
        }

        panel.AddControl(markup.Build());
    }
}
