using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Windows;
using cxshell.AppManager.Catalog;
using cxshell.Apps;
using cxshell.AppManager.UI.Modals;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace cxshell.AppManager.App;

/// <summary>
/// The DotOS App Manager: a polished two-pane store (catalog list ↔ rich HTML detail) with a
/// context toolbar of install/update/remove actions and a status line — the cxfiles/cxtop look.
/// Catalog discovery comes from the injected <see cref="ICatalogProvider"/> (embedded now,
/// online-ready).
/// </summary>
public sealed class AppManagerApp
{
    private readonly ConsoleWindowSystem _ws;
    private readonly AppStateService _state;
    private readonly InstallManager _installer;

    private Window _window = null!;
    private ToolbarControl _toolbar = null!;
    private ListControl _list = null!;
    private HtmlControl _detail = null!;
    private StatusBarControl _statusLine = null!;

    private List<AppView> _views = new();
    private string _filter = "All";
    private AppView? _selected;
    private bool _busy;

    public AppManagerApp(ConsoleWindowSystem ws, AppStateService state, InstallManager installer)
    {
        _ws = ws;
        _state = state;
        _installer = installer;
    }

    public async Task RunAsync()
    {
        Build();
        _ws.AddWindow(_window);
        _ws.SetActiveWindow(_window);
        await LoadAsync();
        await Task.Run(() => _ws.Run());
    }

    private void Build()
    {
        var gradient = ColorGradient.FromColors(new Color(20, 25, 50), new Color(8, 8, 16));

        // Context toolbar (action buttons), populated per selection in UpdateToolbar().
        _toolbar = Ctl.Toolbar()
            .StickyTop()
            .WithSpacing(1)
            .WithWrap()
            .WithMargin(1, 0, 1, 0)
            .WithBackgroundColor(Color.Transparent)
            .WithBelowLineColor(Color.Grey27)
            .Build();

        // Filter tabs — the topmost, full-width sticky bar (no title rule above).
        var tabs = Ctl.TabControl()
            .StickyTop()
            .AddTab("All", () => Ctl.Markup(" ").Build())
            .AddTab("Installed", () => Ctl.Markup(" ").Build())
            .AddTab("Available", () => Ctl.Markup(" ").Build())
            .AddTab("Updates", () => Ctl.Markup(" ").Build())
            .Build();
        tabs.TabChanged += (_, e) =>
        {
            _filter = e.NewIndex switch { 1 => "Installed", 2 => "Available", 3 => "Updates", _ => "All" };
            RefreshList();
        };

        _list = Ctl.List("Apps")
            .OnSelectionChanged((_, idx) => OnSelect(idx))
            .Build();
        _list.HorizontalAlignment = HorizontalAlignment.Stretch;
        _list.VerticalAlignment = VerticalAlignment.Fill;

        // Rich detail pane: HtmlControl renders the catalog's HTML. The app icon is a glyph header
        // (catalog logos are SVG → not decodable), and the image protocol is auto-selected.
        _detail = HtmlBuilder.Create()
            .WithContent("<p>Select an app.</p>")
            .WithShowImages(true)
            .WithBackgroundColor(new Color(20, 25, 45))
            .WithForegroundColor(new Color(220, 225, 235))
            .WithLinkColor(new Color(120, 180, 255))
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // Left: app list (full height, scrolls). Right: per-app toolbar (sticky)
        // above the html detail (fills remaining height, scrolls). The grid itself
        // fills all vertical space between the tabs (top) and the status bar (bottom).
        var grid = Ctl.HorizontalGrid()
            .Column(col => col.Width(34).Add(_list))
            .Column(col => col.Flex(1.0).Add(_toolbar).Add(_detail))
            .WithSplitterAfter(0)
            .Build();
        grid.VerticalAlignment = VerticalAlignment.Fill;

        _statusLine = Ctl.StatusBar().Build();
        _statusLine.AddRightText("[grey50]F5[/] Refresh   [grey50]Enter[/] Open   [grey50]Esc[/] Quit");

        // Full-screen, chromeless, fixed — the cxfiles/cxtop pattern for a standalone app.
        _window = new WindowBuilder(_ws)
            .HideTitle()
            .HideTitleButtons()
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(Color.Grey27)
            .Maximized()
            .Movable(false)
            .Resizable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControl(tabs)
            .AddControl(grid)
            .AddControl(_statusLine)
            .OnKeyPressed(OnKey)
            .Build();

        UpdateToolbar();
    }

    private void OnKey(object? sender, KeyPressedEventArgs e)
    {
        if (e.KeyInfo.Key == ConsoleKey.Escape) { _ws.Shutdown(0); e.Handled = true; }
        else if (e.KeyInfo.Key == ConsoleKey.F5) { _ = LoadAsync(); e.Handled = true; }
    }

    private async Task LoadAsync()
    {
        try
        {
            var views = await _state.GetAppsAsync();
            views = await _state.WithUpdateChecksAsync(views);
            _ws.EnqueueOnUIThread(() =>
            {
                _views = views.ToList();
                // Keep the selection on the same app id across reloads, if still present.
                var keepId = _selected?.Entry.Id;
                RefreshList();
                if (keepId != null)
                    _selected = _views.FirstOrDefault(v => v.Entry.Id == keepId) ?? _selected;
                if (_selected != null) ShowDetail(_selected);
                UpdateToolbar();
                UpdateStatusLine();
            });
        }
        catch (Exception ex)
        {
            _ws.EnqueueOnUIThread(() => SetStatus($"[red]Catalog error: {ex.Message}[/]"));
        }
    }

    private IEnumerable<AppView> Filtered() => _filter switch
    {
        "Installed" => _views.Where(v => v.Status != AppStatus.Available),
        "Available" => _views.Where(v => v.Status == AppStatus.Available),
        "Updates" => _views.Where(v => v.Status == AppStatus.UpdateAvailable),
        _ => _views,
    };

    private void RefreshList()
    {
        _list.ClearItems();
        foreach (var v in Filtered())
        {
            var (glyph, color) = Badge(v.Status);
            _list.AddItem(new ListItem(v.Entry.Name, glyph, color) { Tag = v.Entry.Id });
        }
        _window.Invalidate(true);
    }

    private static (string glyph, Color color) Badge(AppStatus s) => s switch
    {
        AppStatus.Installed => ("●", new Color(120, 220, 160)),
        AppStatus.UpdateAvailable => ("▲", new Color(252, 200, 100)),
        _ => ("○", new Color(130, 130, 150)),
    };

    private void OnSelect(int idx)
    {
        var list = Filtered().ToList();
        if (idx < 0 || idx >= list.Count) return;
        _selected = list[idx];
        ShowDetail(_selected);
        UpdateToolbar();
    }

    // --- Toolbar (context actions) -----------------------------------------------------------

    private void AddToolbarButton(string label, Action action)
    {
        var btn = Ctl.Button()
            .WithText(label)
            .WithBorder(ButtonBorderStyle.None)
            .WithBackgroundColor(Color.Transparent)
            .OnClick((_, _) => action())
            .Build();
        _toolbar.AddItem(btn);
    }

    private void UpdateToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.Clear();

        var v = _selected;
        if (v == null) { _window.Invalidate(true); return; }

        if (_busy)
        {
            AddToolbarButton("[grey50]working…[/]", () => { });
            _window.Invalidate(true);
            return;
        }

        switch (v.Status)
        {
            case AppStatus.Available:
                // One install button per source (e.g. Prebuilt binary / Build from source).
                foreach (var src in v.Entry.Sources)
                {
                    var s = src;
                    var verb = s.Kind == "source" ? "⚙ " : "⤓ ";
                    AddToolbarButton($"{verb}{s.Label}", () => _ = InstallAsync(v, s));
                }
                break;

            case AppStatus.UpdateAvailable:
                AddToolbarButton("▲ Update", () => _ = UpdateAsync(v));
                AddToolbarButton("✕ Remove", () => _ = RemoveAsync(v));
                break;

            case AppStatus.Installed:
                AddToolbarButton("✕ Remove", () => _ = RemoveAsync(v));
                break;
        }

        if (!string.IsNullOrWhiteSpace(v.Entry.Homepage))
        {
            _toolbar.AddItem(new SeparatorControl());
            AddToolbarButton("⊕ Homepage", () => { /* surfaced in the detail pane link */ });
        }

        _window.Invalidate(true);
    }

    // --- Operations --------------------------------------------------------------------------

    private async Task InstallAsync(AppView v, CatalogSource src)
    {
        if (_busy) return;
        _busy = true;
        UpdateToolbar();
        var ran = false;
        try
        {
            var body = new List<string>
            {
                $"Install [bold]{MarkupParser.Escape(v.Entry.Name)}[/]?",
                "",
                $"[grey70]Source:[/] {MarkupParser.Escape(src.Label)}",
            };
            if (!string.IsNullOrWhiteSpace(v.Entry.Homepage))
                body.Add($"[grey70]Homepage:[/] {MarkupParser.Escape(v.Entry.Homepage)}");

            if (AppSource.TryParse(src.Uri, out var parsed) && parsed.Installer != null)
            {
                body.Add("");
                body.Add($"[yellow]Runs the publisher's {MarkupParser.Escape(parsed.Installer)} (not sandboxed).[/]");
            }

            var ok = await ConfirmModal.ShowAsync(_ws, "Install", body, confirmLabel: "Install", parent: _window);
            if (!ok) return;

            var success = await OperationProgressModal.ShowAsync(
                _ws,
                title: $"Installing {v.Entry.Name}",
                description: src.Label,
                operation: (ct, progress) =>
                    _installer.InstallAsync(v.Entry.ToInstallRequest(), src.Uri, progress, ct, src.Build),
                parent: _window);
            ran = true;
            _ws.NotificationStateService.ShowNotification(
                v.Entry.Name,
                success ? "Done." : "Operation failed.",
                success ? NotificationSeverity.Info : NotificationSeverity.Danger);
        }
        finally
        {
            _busy = false;
            if (ran) await LoadAsync();
        }
    }

    private async Task UpdateAsync(AppView v)
    {
        if (_busy) return;
        _busy = true;
        UpdateToolbar();
        try
        {
            var success = await OperationProgressModal.ShowAsync(
                _ws,
                title: $"Updating {v.Entry.Name}",
                description: "Fetching the latest version…",
                operation: (ct, progress) => _installer.UpdateAsync(v.Entry.Id, progress, ct),
                parent: _window);
            _ws.NotificationStateService.ShowNotification(
                v.Entry.Name,
                success ? "Done." : "Operation failed.",
                success ? NotificationSeverity.Info : NotificationSeverity.Danger);
        }
        finally
        {
            _busy = false;
            await LoadAsync();
        }
    }

    private async Task RemoveAsync(AppView v)
    {
        if (_busy) return;
        _busy = true;
        UpdateToolbar();
        var ran = false;
        try
        {
            var isScript = v.Entry.Sources.Any(s => AppSource.TryParse(s.Uri, out var ps) && ps.Uninstaller != null);
            var removeBody = new List<string>
            {
                $"Remove [bold]{MarkupParser.Escape(v.Entry.Name)}[/]?",
                "",
                "[grey70]This deletes the installed files.[/]",
            };
            if (isScript)
                removeBody.Add("[yellow]Runs the publisher's uninstall script (not sandboxed).[/]");
            var ok = await ConfirmModal.ShowAsync(_ws, "Remove", removeBody, confirmLabel: "Remove", parent: _window);
            if (!ok) return;

            var success = await OperationProgressModal.ShowAsync(
                _ws,
                title: $"Removing {v.Entry.Name}",
                description: "Deleting installed files…",
                operation: (ct, progress) => _installer.RemoveAsync(v.Entry.Id, progress, ct),
                parent: _window);
            ran = true;
            _ws.NotificationStateService.ShowNotification(
                v.Entry.Name,
                success ? "Done." : "Operation failed.",
                success ? NotificationSeverity.Info : NotificationSeverity.Danger);
        }
        finally
        {
            _busy = false;
            if (ran) await LoadAsync();
        }
    }

    private void ShowDetail(AppView v)
    {
        // The homepage is the base URL so the catalog's relative image/screenshot paths resolve.
        _detail.SetContent(BuildDetailHtml(v), v.Entry.Homepage ?? "");
        _window.Invalidate(true);
    }

    private static string BuildDetailHtml(AppView v)
    {
        var e = v.Entry;
        var sb = new System.Text.StringBuilder();

        var icon = string.IsNullOrWhiteSpace(e.Icon) ? "" : Esc(e.Icon) + "  ";
        sb.Append($"<h1><span style=\"color: rgb(120,180,255)\">{icon}{Esc(e.Name)}</span></h1>");
        sb.Append($"<p><i>{Esc(e.Summary)}</i></p>");

        var status = v.Status switch
        {
            AppStatus.Installed => $"<p><b><span style=\"color: lime\">● Installed</span></b> v{Esc(v.InstalledVersion ?? "?")}</p>",
            AppStatus.UpdateAvailable => $"<p><b><span style=\"color: yellow\">▲ Update available</span></b> (installed v{Esc(v.InstalledVersion ?? "?")})</p>",
            _ => "<p><span style=\"color: grey\">○ Not installed</span></p>",
        };
        sb.Append(status);

        if (!string.IsNullOrWhiteSpace(e.DescriptionHtml))
            sb.Append(e.DescriptionHtml);

        foreach (var shot in e.Screenshots)
            sb.Append($"<p><a href=\"{Esc(shot)}\">screenshot</a></p>");

        sb.Append("<h2>Install options</h2><ul>");
        foreach (var src in e.Sources)
            sb.Append($"<li><b>{Esc(src.Label)}</b> <span style=\"color: grey\">({Esc(src.Kind)})</span></li>");
        sb.Append("</ul>");
        sb.Append("<p><span style=\"color: grey\">Use the toolbar above to install, update or remove.</span></p>");

        if (!string.IsNullOrWhiteSpace(e.Homepage))
            sb.Append($"<p><a href=\"{Esc(e.Homepage)}\">{Esc(e.Homepage)}</a></p>");

        return sb.ToString();
    }

    private static string Esc(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private void SetStatus(string markup)
    {
        _statusLine.ClearLeft();
        _statusLine.AddLeftText(markup);
        _window.Invalidate(true);
    }

    private void UpdateStatusLine()
    {
        int total = _views.Count;
        int installed = _views.Count(v => v.Status != AppStatus.Available);
        int updates = _views.Count(v => v.Status == AppStatus.UpdateAvailable);
        SetStatus($"[dim]{total} apps · {installed} installed · {updates} updates · [/][bold]F5[/][dim] refresh · [/][bold]Esc[/][dim] quit[/]");
    }
}
