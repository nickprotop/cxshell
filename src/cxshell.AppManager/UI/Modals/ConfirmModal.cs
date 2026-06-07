using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Ctl = SharpConsoleUI.Builders.Controls;

namespace cxshell.AppManager.UI.Modals;

/// <summary>
/// A generic Yes/No confirmation modal. Returns true if confirmed, false if cancelled or Esc.
/// Keys: Y / Enter confirm, N / Esc cancel.
/// </summary>
public sealed class ConfirmModal : ModalBase<bool>
{
    private readonly string _title;
    private readonly IReadOnlyList<string> _bodyLines;
    private readonly string _confirmLabel;

    private ConfirmModal(string title, IReadOnlyList<string> bodyLines, string confirmLabel)
    {
        if (bodyLines.Count == 0)
            throw new ArgumentException("At least one body line is required.", nameof(bodyLines));

        _title = title;
        _bodyLines = bodyLines;
        _confirmLabel = SharpConsoleUI.Parsing.MarkupParser.Escape(confirmLabel);
    }

    /// <param name="bodyLines">Markup lines (already escaped where needed) shown in the body.</param>
    public static Task<bool> ShowAsync(
        ConsoleWindowSystem ws, string title, IReadOnlyList<string> bodyLines,
        string confirmLabel = "Install", Window? parent = null)
        => new ConfirmModal(title, bodyLines, confirmLabel).ShowAsync(ws, parent);

    protected override string GetTitle() => _title;
    protected override (int width, int height) GetSize() => (60, 8 + _bodyLines.Count);
    protected override bool GetDefaultResult() => false;

    protected override void BuildContent()
    {
        var body = Ctl.Markup().WithMargin(2, 1, 2, 1).WithAlignment(HorizontalAlignment.Left);
        foreach (var line in _bodyLines) body.AddLine(line);
        Modal.AddControl(body.Build());

        Modal.AddControl(Ctl.RuleBuilder().StickyBottom().WithColor(Color.Grey27).Build());

        var confirm = Ctl.Button()
            .WithText($"  {_confirmLabel} (Y)  ")
            .OnClick((_, _) => CloseWithResult(true))
            .Build();
        var cancel = Ctl.Button()
            .WithText("  Cancel (N)  ")
            .OnClick((_, _) => CloseWithResult(false))
            .Build();

        var grid = Ctl.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Center)
            .StickyBottom()
            .Column(col => col.Add(confirm))
            .Column(col => col.Width(2))
            .Column(col => col.Add(cancel))
            .Build();
        grid.Margin = new Margin(0, 1, 0, 1);
        Modal.AddControl(grid);
    }

    protected override void OnKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        if (e.KeyInfo.Key is ConsoleKey.Y or ConsoleKey.Enter)
        {
            CloseWithResult(true);
            e.Handled = true;
        }
        else if (e.KeyInfo.Key == ConsoleKey.N)
        {
            CloseWithResult(false);
            e.Handled = true;
        }
        else base.OnKeyPressed(sender, e);
    }
}
