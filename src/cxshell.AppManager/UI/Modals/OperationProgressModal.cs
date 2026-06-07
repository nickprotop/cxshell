using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using cxshell.Apps;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace cxshell.AppManager.UI.Modals;

/// <summary>
/// Runs a long-running install/update/remove operation, streaming <see cref="InstallProgress"/>
/// into a scrollable log and a progress bar. Returns true on success. The window is non-closable
/// while running; on completion it shows ✓/✗, enables a Close button, and waits for manual close.
/// </summary>
public sealed class OperationProgressModal : ModalBase<bool>
{
    private readonly string _title;
    private readonly string _description;
    private readonly Func<CancellationToken, IProgress<InstallProgress>, Task> _operation;

    private readonly LogBuffer _log = new(maxLines: 500);
    private readonly CancellationTokenSource _cts = new();
    private readonly DateTime _startTime = DateTime.Now;

    private MarkupControl? _status;
    private ProgressBarControl? _bar;
    private MarkupControl? _logContent;
    private ButtonControl? _button;
    private bool _finished;
    private string? _lastLoggedMessage; // collapses repeated identical progress messages (e.g. download ticks)

    private OperationProgressModal(
        string title, string description,
        Func<CancellationToken, IProgress<InstallProgress>, Task> operation)
    {
        _title = title;
        _description = description;
        _operation = operation;
    }

    public static Task<bool> ShowAsync(
        ConsoleWindowSystem ws, string title, string description,
        Func<CancellationToken, IProgress<InstallProgress>, Task> operation, Window? parent = null)
        => new OperationProgressModal(title, description, operation).ShowAsync(ws, parent);

    protected override string GetTitle() => _title;
    protected override (int width, int height) GetSize() => (90, 24);
    protected override bool GetResizable() => true;
    protected override bool GetDefaultResult() => false;

    protected override Window CreateModal()
    {
        var modal = base.CreateModal();
        modal.IsClosable = false; // locked while the operation runs
        return modal;
    }

    protected override void BuildContent()
    {
        _status = Ctl.Markup()
            .AddLine($"[rgb(120,180,255)]{Escape(_title)}[/]")
            .AddLine($"[grey70]{Escape(_description)}[/]")
            .WithMargin(2, 1, 2, 0)
            .Build();

        _bar = Ctl.ProgressBar()
            .Indeterminate(true)
            .WithAnimationInterval(100)
            .ShowPercentage(false)
            .WithMargin(2, 1, 2, 1)
            .Build();

        var sep1 = Ctl.RuleBuilder().WithColor(Color.Grey27).Build();

        var logPanel = Ctl.ScrollablePanel()
            .WithVerticalScroll(ScrollMode.Scroll)
            .WithScrollbar(true)
            .WithMouseWheel(true)
            .WithAutoScroll(true)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithMargin(2, 0, 2, 1)
            .Build();

        _logContent = Ctl.Markup().AddLine("[grey50]Waiting for output…[/]").Build();
        logPanel.AddControl(_logContent);

        var sep2 = Ctl.RuleBuilder().StickyBottom().WithColor(Color.Grey27).Build();

        _button = Ctl.Button()
            .WithText("  Cancel (Esc)  ")
            .OnClick((_, _) => OnButton())
            .Build();
        var buttonRow = Ctl.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Center)
            .StickyBottom()
            .Column(col => col.Add(_button))
            .Build();
        buttonRow.Margin = new Margin(0, 1, 0, 1);

        Modal.AddControl(_status);
        Modal.AddControl(_bar);
        Modal.AddControl(sep1);
        Modal.AddControl(logPanel);
        Modal.AddControl(sep2);
        Modal.AddControl(buttonRow);

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        var progress = new Progress<InstallProgress>(p =>
            WindowSystem.EnqueueOnUIThread(() =>
            {
                if (_finished) return; // drop stale reports after completion

                // Fraction-bearing reports (e.g. a download) fire once per chunk — many times a
                // second with the same message. Drive the bar on every tick, but only append a log
                // line when the message actually changes, so the log shows "Downloading…" once
                // instead of a flood of identical timestamped lines.
                if (p.Fraction is { } f && _bar != null)
                {
                    _bar.IsIndeterminate = false;
                    _bar.MaxValue = 100;
                    _bar.Value = Math.Clamp(f * 100.0, 0, 100);
                }

                if (!string.IsNullOrWhiteSpace(p.Message) && p.Message != _lastLoggedMessage)
                {
                    _lastLoggedMessage = p.Message;
                    var elapsed = (DateTime.Now - _startTime).TotalSeconds;
                    _log.Append(p.Message, elapsed);
                    _logContent?.SetContent(_log.Snapshot());
                }
                Modal.Invalidate(true);
            }));

        try
        {
            await _operation(_cts.Token, progress);
            Finish(success: true, message: null);
        }
        catch (OperationCanceledException)
        {
            Finish(success: false, message: "Cancelled.", cancelled: true);
        }
        catch (Exception ex)
        {
            Finish(success: false, message: ex.Message);
        }
    }

    private void Finish(bool success, string? message, bool cancelled = false)
    {
        WindowSystem.EnqueueOnUIThread(() =>
        {
            _finished = true;
            var elapsed = (DateTime.Now - _startTime).TotalSeconds;

            if (_bar != null)
            {
                _bar.IsIndeterminate = false;
                _bar.MaxValue = 100;
                _bar.Value = 100;
            }

            if (success)
            {
                _status?.SetContent(new List<string>
                {
                    $"[green bold]✓ Completed in {elapsed:F1}s[/]",
                    "[grey50]Press Esc or click Close.[/]",
                });
                _log.AppendRaw($"[green bold]✓ Done in {elapsed:F1}s[/]");
            }
            else
            {
                var icon = cancelled ? "[yellow bold]✗ Cancelled[/]" : "[red bold]✗ Failed[/]";
                _status?.SetContent(new List<string>
                {
                    icon,
                    $"[grey50]{Escape(message ?? "Unknown error")}[/]",
                });
                _log.AppendRaw($"{icon} [grey70]{Escape(message ?? "")}[/]");
            }

            _logContent?.SetContent(_log.Snapshot());
            Result = success;

            if (_button != null) _button.Text = "  Close (Esc)  ";
            Modal.IsClosable = true;
            Modal.Invalidate(true);
        });
    }

    private void OnButton()
    {
        if (_finished) { CloseWithResult(Result); return; }
        _cts.Cancel(); // operation observes the token and throws OperationCanceledException
    }

    protected override void OnKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        if (e.KeyInfo.Key == ConsoleKey.Escape)
        {
            OnButton();
            e.Handled = true;
        }
        // Swallow all other keys while running so the modal stays put.
    }

    protected override void OnCleanup() => _cts.Dispose();

    private static string Escape(string s) => SharpConsoleUI.Parsing.MarkupParser.Escape(s);
}
