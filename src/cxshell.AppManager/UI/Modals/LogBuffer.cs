using SharpConsoleUI.Parsing;

namespace cxshell.AppManager.UI.Modals;

/// <summary>
/// A bounded, markup-safe buffer of log lines for the operation progress modal. Pure logic,
/// no UI — so it is unit-testable without a window. <see cref="Append"/> timestamps and escapes
/// caller messages; <see cref="AppendRaw"/> adds pre-formatted markup lines verbatim.
/// </summary>
public sealed class LogBuffer
{
    private readonly int _maxLines;
    private readonly List<string> _lines = new();
    private readonly object _lock = new();

    public LogBuffer(int maxLines = 500) => _maxLines = maxLines;

    /// <summary>Append a timestamped, markup-escaped message. Blank messages are ignored.</summary>
    public void Append(string message, double elapsedSeconds)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var line = $"[grey50]{elapsedSeconds:F1}s[/] [grey70]{MarkupParser.Escape(message)}[/]";
        AddCapped(line);
    }

    /// <summary>Append a pre-formatted markup line verbatim (caller owns escaping).
    /// Null/whitespace-only lines are silently discarded.</summary>
    public void AppendRaw(string markupLine)
    {
        if (string.IsNullOrWhiteSpace(markupLine)) return;
        AddCapped(markupLine);
    }

    /// <summary>A copy of the current lines, oldest first.</summary>
    public List<string> Snapshot()
    {
        lock (_lock) return new List<string>(_lines);
    }

    private void AddCapped(string line)
    {
        lock (_lock)
        {
            _lines.Add(line);
            while (_lines.Count > _maxLines) _lines.RemoveAt(0);
        }
    }
}
