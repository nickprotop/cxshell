using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using cxshell.AppManager;
using cxshell.AppManager.App;
using cxshell.AppManager.Catalog;
using cxshell.Apps;

if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

var driver = new NetConsoleDriver(RenderMode.Buffer);
// Standalone full-screen app (launched as its own process by the desktop), so — like cxfiles/
// cxtop — it owns the whole surface: no desktop panels, no window-cycle key.
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: true,
    InstallSynchronizationContext: true,
    ShowTopPanel: false,
    ShowBottomPanel: false,
    WindowCycleKey: null);

var ws = new ConsoleWindowSystem(driver, options: options);

Console.CancelKeyPress += (_, e) => { e.Cancel = true; ws.Shutdown(0); };

// --- Composition root -------------------------------------------------------------------------
// Catalog: embedded (hardcoded) now. To go online later, swap to:
//   new HttpCatalogProvider(new HttpClient(), "https://apps.dotos.example/index.json")
ICatalogProvider catalog = new EmbeddedCatalogProvider();

var http = new HttpClient();
var store = new ManifestStore();
ISandbox sandbox = new BwrapSandbox();           // source builds run sandboxed (Standard §5.4)
var installers = new IInstaller[] { new ScriptInstaller(http), new BinaryInstaller(http), new SourceInstaller(sandbox) };
var installer = new InstallManager(store, installers);
var stateService = new AppStateService(catalog, store, installer);

var app = new AppManagerApp(ws, stateService, installer);
await app.RunAsync();
return 0;
