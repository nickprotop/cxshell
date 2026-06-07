using cxshell.AppManager.UI.Modals;

namespace cxshell.Tests;

public class LogBufferTests
{
    [Fact]
    public void Append_PrefixesTimestampAndMessage()
    {
        var buf = new LogBuffer(maxLines: 500);
        buf.Append("Downloading", elapsedSeconds: 1.2);

        var lines = buf.Snapshot();
        Assert.Single(lines);
        Assert.Contains("1.2s", lines[0]);
        Assert.Contains("Downloading", lines[0]);
    }

    [Fact]
    public void Append_EscapesMarkupInMessage()
    {
        var buf = new LogBuffer(maxLines: 500);
        buf.Append("progress [50%]", elapsedSeconds: 0.0);

        // The raw bracket must be escaped so the markup parser does not treat it as a tag.
        Assert.Contains("[[50%]]", buf.Snapshot()[0]);
    }

    [Fact]
    public void Append_CapsAtMaxLinesDroppingOldest()
    {
        var buf = new LogBuffer(maxLines: 3);
        buf.Append("a", 0); buf.Append("b", 0); buf.Append("c", 0); buf.Append("d", 0);

        var lines = buf.Snapshot();
        Assert.Equal(3, lines.Count);
        Assert.Contains("b", lines[0]);
        Assert.Contains("d", lines[2]);
    }

    [Fact]
    public void AppendRaw_AddsLineVerbatimWithoutTimestamp()
    {
        var buf = new LogBuffer(maxLines: 500);
        buf.AppendRaw("[green bold]Done[/]");

        Assert.Equal("[green bold]Done[/]", buf.Snapshot()[0]);
    }

    [Fact]
    public void AppendRaw_IgnoresBlankLines()
    {
        var buf = new LogBuffer(maxLines: 500);
        buf.AppendRaw("   ");
        Assert.Empty(buf.Snapshot());
    }

    [Fact]
    public void IgnoresBlankMessages()
    {
        var buf = new LogBuffer(maxLines: 500);
        buf.Append("   ", 0);
        Assert.Empty(buf.Snapshot());
    }
}
