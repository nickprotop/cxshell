using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Windows;

namespace cxshell.Terminal;

public static class TerminalWindow
{
    public static Window CreateWindow(ConsoleWindowSystem windowSystem, string? workingDirectory = null)
    {
        var terminal = new TerminalBuilder()
            .WithExe("/bin/bash")
            .WithWorkingDirectory(workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            .Build();

        var window = new WindowBuilder(windowSystem)
            .WithTitle("Terminal")
            .WithSize(100, 30)
            .Centered()
            .AddControl(terminal)
            .OnClosed((sender, e) => terminal.Dispose())
            .Build();

        return window;
    }
}
