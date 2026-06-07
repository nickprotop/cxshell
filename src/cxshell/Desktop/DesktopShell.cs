using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Panel;
using SharpConsoleUI.Windows;
using Ctl = SharpConsoleUI.Builders.Controls;
using cxshell.Apps;
using cxshell.Config;
using cxshell.FileManager;
using cxshell.Settings;
using cxshell.Shared;
using cxshell.Terminal;

namespace cxshell.Desktop;

public class DesktopShell
{
    private readonly CxShellConfig _config;
    private ConsoleWindowSystem _windowSystem = null!;
    private System.Timers.Timer? _trayTimer;

    // Persisted tray indicator prefs (read at startup from the registry; config is the fallback).
    private bool _showClock = true;
    private bool _showNetwork = true;
    private bool _showBattery = true;

    public DesktopShell(CxShellConfig? config = null)
    {
        _config = config ?? CxShellConfig.Default;
    }

    public async Task<int> RunAsync()
    {
        try
        {
            var driver = new NetConsoleDriver(RenderMode.Buffer);
            // InstallSynchronizationContext: true opts into the UI SynchronizationContext, so
            // `await` inside handlers resumes on the UI thread (WinForms/WPF model). HARD
            // CONSTRAINT: no UI handler may block on async work (.Result / .Wait() /
            // .GetAwaiter().GetResult()), or the captured continuation deadlocks against the
            // loop. cxshell has no such patterns; keep it that way.
            var options = new ConsoleWindowSystemOptions(
                EnableFrameRateLimiting: true,
                InstallSynchronizationContext: true,
                // Disable ConsoleEx's built-in Ctrl+Q quit. The desktop owns Ctrl+Q via a
                // declining global shortcut (registered below): when a window is active the
                // key is routed to that window (a terminal forwards it to its PTY); only on
                // the bare desktop does it raise a quit confirmation. See OnQuitRequested.
                ExitKey: null,
                DesktopBackground: DesktopPanels.ResolveBackground(_config.DesktopBackground),
                TopPanelConfig: DesktopPanels.ConfigureTopPanel,
                BottomPanelConfig: DesktopPanels.ConfigureBottomPanel
            );

            var registryConfig = RegistryConfiguration.ForFile(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "cxshell", "registry.json"));

            _windowSystem = new ConsoleWindowSystem(driver, options: options, registryConfiguration: registryConfig);

            // The registry is only available after construction. Apply the user's persisted
            // background choice, falling back to the config default set in the options above.
            var backgroundKey = DesktopPanels.ReadBackgroundKey(_windowSystem, _config.DesktopBackground);
            _windowSystem.DesktopBackground = DesktopPanels.ResolveBackground(backgroundKey);

            // Ctrl+Q policy. ExitKey is disabled (see options above); we register a
            // *declining* global shortcut instead. Returning false lets ConsoleEx keep
            // routing the key to the active window (so a focused terminal forwards Ctrl+Q
            // to its PTY); returning true consumes it. We only consume — and confirm a
            // quit — when no window is active (the bare desktop is focused).
            _windowSystem.RegisterGlobalShortcut(ConsoleModifiers.Control, ConsoleKey.Q, OnQuitRequested);

            // Console.CancelKeyPress may fail if the terminal isn't fully
            // initialized. Non-critical — Ctrl+C is a nice-to-have.
            try
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    _windowSystem.Shutdown(0);
                };
            }
            catch (Exception)
            {
                // Ignore — Ctrl+C handling is a nice-to-have, not essential
            }

            SetupTopStatusBar();
            RegisterApps();
            WatchAppsDir();
            StartSystemTray();
            CreateDesktopWindow();

            _windowSystem.PanelStateService.BottomStatus = "";

            await Task.Run(() => _windowSystem.Run());

            _trayTimer?.Dispose();
            _appsWatcher?.Dispose();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            Console.WriteLine($"cxshell fatal error: {ex}");
            return 1;
        }
    }

    private void SetupTopStatusBar()
    {
        _windowSystem.PanelStateService.TopStatus =
            $"[bold cyan]{_config.DesktopTitle}[/]";
    }

    /// <summary>
    /// The start-menu element lives in the bottom panel (built by DesktopPanels). The element
    /// id "startmenu" is the ConsoleEx default for <c>Elements.StartMenu()</c>.
    /// </summary>
    private StartMenuElement StartMenu =>
        _windowSystem.BottomPanel?.FindElement<StartMenuElement>("startmenu")
        ?? throw new InvalidOperationException(
            "StartMenu element not found in the bottom panel — check DesktopPanels.ConfigureBottomPanel.");

    private readonly ManifestStore _manifests = new();
    private FileSystemWatcher? _appsWatcher;

    /// <summary>Action names currently registered on the start menu (so a refresh can clear them).</summary>
    private readonly List<string> _registeredActions = new();

    /// <summary>Ids handled in-process by DotOS (Exec = dotos:builtin/...).</summary>
    private const string FilesId = "org.dotos.cxfiles";

    /// <summary>
    /// Register all Start-menu apps: the always-present built-ins (Terminal, Settings, Welcome,
    /// and the built-in Files fallback), then every installed <c>.desktop</c> manifest. A manifest
    /// with the same id supersedes a built-in fallback (the built-in is only registered when no
    /// manifest provides that id). Idempotent: clears prior registrations first so it can be
    /// re-run when manifests change.
    /// </summary>
    private void RegisterApps()
    {
        var menu = StartMenu;

        // Clear anything we registered previously (leave third-party registrations alone).
        foreach (var name in _registeredActions) menu.UnregisterAction(name);
        _registeredActions.Clear();

        void Register(string name, Action callback, string category, int order)
        {
            menu.RegisterAction(name, callback, category, order);
            _registeredActions.Add(name);
        }

        var manifests = _manifests.Enumerate();
        var manifestIds = manifests.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);

        // Built-in Files fallback — only when no installed manifest provides it (e.g. cxfiles).
        if (!manifestIds.Contains(FilesId))
        {
            Register("Files", () =>
            {
                var w = FileManagerWindow.CreateWindow(_windowSystem);
                _windowSystem.AddWindow(w);
                _windowSystem.SetActiveWindow(w);
            }, "Applications", 10);
        }

        Register("Terminal", () =>
        {
            var w = TerminalWindow.CreateWindow(_windowSystem);
            _windowSystem.AddWindow(w);
            _windowSystem.SetActiveWindow(w);
        }, "Applications", 20);

        Register("Settings", () => SettingsWindow.Open(_windowSystem), "System", 10);
        Register("Welcome", CreateWelcomeWindow, "System", 20);

        // Installed apps (baked + user/App-Manager), launched as external processes.
        foreach (var entry in manifests)
        {
            if (entry.Builtin) continue; // builtin pseudo-execs handled above by id
            var e = entry;
            Register(e.Name, () => LaunchExternal(e),
                e.Group_DotOS ?? MapGroup(e.Categories), e.Order);
        }
    }

    /// <summary>
    /// Watch the user manifest dir so apps the App Manager installs/removes (in a separate
    /// process) appear/disappear in the Start menu live, without a shell restart. The watcher
    /// fires on a background thread; the menu rebuild is marshalled onto the UI thread.
    /// </summary>
    private void WatchAppsDir()
    {
        var dir = _manifests.WritableDir;
        try
        {
            Directory.CreateDirectory(dir);
            _appsWatcher = new FileSystemWatcher(dir, "*.desktop")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            FileSystemEventHandler onChange = (_, _) =>
                _windowSystem.EnqueueOnUIThread(RegisterApps);
            _appsWatcher.Created += onChange;
            _appsWatcher.Deleted += onChange;
            _appsWatcher.Changed += onChange;
            _appsWatcher.Renamed += (_, _) => _windowSystem.EnqueueOnUIThread(RegisterApps);
        }
        catch
        {
            // Watching is a live-update convenience; failure just means a restart is needed
            // to pick up newly installed apps. Non-fatal.
        }
    }

    /// <summary>Map freedesktop Categories to a Start-menu group when X-DotOS-Group is absent.</summary>
    private static string MapGroup(IReadOnlyList<string> categories)
    {
        foreach (var c in categories)
        {
            switch (c)
            {
                case "System" or "Monitor" or "Settings": return "System";
                case "Development" or "TextEditor": return "Accessories";
            }
        }
        return "Applications";
    }

    /// <summary>
    /// Launch an installed app's Exec as an external process inside a hosted terminal window.
    /// Resolves the program (absolute / PATH / ~/.local/bin / /opt/cx); on failure, notifies the
    /// user rather than spawning a broken PTY.
    /// </summary>
    private void LaunchExternal(DesktopEntry entry)
    {
        var (exe, args) = ParseExec(entry.Exec ?? "");
        var resolved = ResolveProgram(exe);
        if (resolved == null)
        {
            _windowSystem.NotificationStateService.ShowNotification(
                entry.Name, $"{entry.Name} is not installed.", NotificationSeverity.Warning);
            return;
        }

        var terminal = new TerminalBuilder()
            .WithExe(resolved)
            .WithArgs(args)
            .WithWorkingDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            .Build();

        var builder = new WindowBuilder(_windowSystem)
            .WithTitle(entry.Name)
            .AddControl(terminal)
            .OnClosed((_, _) => terminal.Dispose());
        builder = entry.Maximize ? builder.Maximized() : builder.WithSize(100, 30).Centered();

        var window = builder.Build();
        _windowSystem.AddWindow(window);
        _windowSystem.SetActiveWindow(window);
    }

    /// <summary>Split an Exec line into program + args, dropping field codes (%f %F %u %U).</summary>
    private static (string exe, string[] args) ParseExec(string exec)
    {
        var parts = exec.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p is not ("%f" or "%F" or "%u" or "%U"))
            .Select(p => p.Replace("%%", "%"))
            .ToArray();
        if (parts.Length == 0) return ("", Array.Empty<string>());
        return (parts[0], parts[1..]);
    }

    /// <summary>Resolve a program: absolute path as-is, else search PATH, ~/.local/bin, /opt/cx.</summary>
    private static string? ResolveProgram(string exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return null;
        if (Path.IsPathRooted(exe)) return File.Exists(exe) ? exe : null;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dirs = new List<string>();
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries));
        dirs.Add(Path.Combine(home, ".local", "bin"));
        dirs.Add("/opt/cx");
        foreach (var d in dirs)
        {
            var candidate = Path.Combine(d, exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private const string RegistrySection = "cxshell/Welcome";
    private const string ShowWelcomeKey = "showOnStartup";

    private void CreateDesktopWindow()
    {
        var registry = _windowSystem.RegistryStateService;
        if (registry != null)
        {
            var section = registry.OpenSection(RegistrySection);
            var showWelcome = section.GetBool(ShowWelcomeKey, defaultValue: true);
            if (!showWelcome)
                return;
        }

        CreateWelcomeWindow();
    }

    private void CreateWelcomeWindow()
    {
        var window = new WindowBuilder(_windowSystem)
            .WithTitle("Welcome to DotOS")
            .WithSize(60, 20)
            .Centered()
            .Build();

        window.AddControl(new MarkupControl(new List<string>
        {
            "",
            "[bold cyan]Welcome to DotOS[/]",
            "",
            "A minimal .NET-powered Linux distribution.",
            "",
            "[bold]Desktop:[/] cxshell",
            "[bold]UI Framework:[/] ConsoleEx (SharpConsoleUI)",
            "",
            "[yellow]Press Ctrl+Space[/] or click [yellow]Start[/] to open the menu.",
            "",
            "[dim]File Manager, Terminal, and Settings are available[/]",
            "[dim]in the Start menu under Applications and System.[/]"
        }));

        var checkbox = new CheckboxControl("Show this window on startup", isChecked: true);
        checkbox.Margin = new Margin(2, 1, 0, 0);
        checkbox.CheckedChanged += (_, isChecked) =>
        {
            var reg = _windowSystem.RegistryStateService;
            if (reg != null)
            {
                var sec = reg.OpenSection(RegistrySection);
                sec.SetBool(ShowWelcomeKey, isChecked);
                reg.Save();
            }
        };

        window.AddControl(checkbox);

        window.OnClosed += (_, _) =>
        {
            _windowSystem.RegistryStateService?.Save();
        };

        _windowSystem.AddWindow(window);
        _windowSystem.SetActiveWindow(window);
    }

    // True while the quit-confirm modal is open, so repeated Ctrl+Q presses don't stack it.
    private bool _quitConfirmOpen;

    /// <summary>
    /// Ctrl+Q handler, registered as a *declining* global shortcut (runs before window
    /// routing). Return false to decline (ConsoleEx keeps routing the key to the active
    /// window — a terminal then forwards Ctrl+Q to its PTY). Return true to consume.
    /// Policy: if any window is active, decline; only on the bare desktop do we consume
    /// and raise a quit confirmation.
    /// </summary>
    private bool OnQuitRequested()
    {
        if (_windowSystem.ActiveWindow != null)
            return false; // let the focused window have the key (terminal -> PTY)

        if (!_quitConfirmOpen)
            ShowQuitConfirm();
        return true; // bare desktop: we own Ctrl+Q
    }

    private void ShowQuitConfirm()
    {
        _quitConfirmOpen = true;

        var window = new WindowBuilder(_windowSystem)
            .WithTitle("Quit cxshell")
            .WithSize(44, 9)
            .Centered()
            .Build();

        window.AddControl(new MarkupControl(new List<string>
        {
            "",
            "[bold]Quit cxshell?[/]",
            "",
            "[dim]This will close the desktop.[/]",
        }));

        var yes = Ctl.Button()
            .WithText("  Yes (Y)  ")
            .OnClick((_, _) => _windowSystem.Shutdown(0))
            .Build();
        var no = Ctl.Button()
            .WithText("  No (N)  ")
            .OnClick((_, _) => _windowSystem.CloseWindow(window))
            .Build();

        var grid = Ctl.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Center)
            .Column(col => col.Add(yes))
            .Column(col => col.Width(2))
            .Column(col => col.Add(no))
            .Build();
        grid.Margin = new Margin(0, 1, 0, 0);
        window.AddControl(grid);

        // Y / Enter = quit; N / Esc = dismiss. Keys are seen here because the modal is the
        // active window (so this also satisfies "active window gets Ctrl+Q" — pressing
        // Ctrl+Q again while the modal is open is just routed to it and ignored).
        window.KeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key is ConsoleKey.Y or ConsoleKey.Enter)
            {
                _windowSystem.Shutdown(0);
                e.Handled = true;
            }
            else if (e.KeyInfo.Key is ConsoleKey.N or ConsoleKey.Escape)
            {
                _windowSystem.CloseWindow(window);
                e.Handled = true;
            }
        };

        window.OnClosed += (_, _) => _quitConfirmOpen = false;

        _windowSystem.AddWindow(window);
        _windowSystem.SetActiveWindow(window);
    }

    private void StartSystemTray()
    {
        // Read persisted tray prefs (cxshell/Settings), falling back to config defaults.
        _showClock = SettingsStore.GetBool(_windowSystem, "tray.showClock", _config.ShowClock);
        _showNetwork = SettingsStore.GetBool(_windowSystem, "tray.showNetwork", _config.ShowNetworkStatus);
        _showBattery = SettingsStore.GetBool(_windowSystem, "tray.showBattery", _config.ShowBatteryStatus);

        UpdateTray();

        _trayTimer = new System.Timers.Timer(_config.SystemTrayUpdateIntervalSeconds * 1000);
        _trayTimer.Elapsed += (_, _) => UpdateTray();
        _trayTimer.AutoReset = true;
        _trayTimer.Start();
    }

    private void UpdateTray()
    {
        // The bottom-panel Clock element renders the time and self-updates, so the tray text
        // carries only network/battery (showClock: false) to avoid a duplicate clock.
        var trayText = SystemTray.GetStatusText(
            showClock: false,
            _showBattery,
            _showNetwork
        );

        // Runs on the System.Timers.Timer thread-pool thread — marshal the panel-state write
        // onto the UI thread; the render loop reads panel state without locking.
        _windowSystem.EnqueueOnUIThread(() =>
            _windowSystem.PanelStateService.TopStatus =
                $"[bold cyan]{_config.DesktopTitle}[/]                    {trayText}");
    }
}
