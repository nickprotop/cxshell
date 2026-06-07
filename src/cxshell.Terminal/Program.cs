using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using cxshell.Terminal;
using cxshell.Shared;

if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

var driver = new NetConsoleDriver(NetConsoleDriverOptions.Default);
// InstallSynchronizationContext: true opts into the UI SynchronizationContext, so `await`
// in handlers resumes on the UI thread. Constraint: no UI handler may block on async work
// (.Result/.Wait()/.GetAwaiter().GetResult()), or the captured continuation deadlocks.
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: true,
    InstallSynchronizationContext: true,
    DesktopBackground: DesktopPanels.DefaultDesktopBackground(),
    TopPanelConfig: DesktopPanels.ConfigureTopPanel,
    BottomPanelConfig: DesktopPanels.ConfigureBottomPanel
);
var windowSystem = new ConsoleWindowSystem(driver, options: options);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    windowSystem.Shutdown(0);
};

var window = TerminalWindow.CreateWindow(windowSystem);
windowSystem.AddWindow(window);
windowSystem.SetActiveWindow(window);

await Task.Run(() => windowSystem.Run());

return 0;
