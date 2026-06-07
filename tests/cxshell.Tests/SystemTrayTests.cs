using cxshell.Desktop;

namespace cxshell.Tests;

public class SystemTrayTests
{
    [Fact]
    public void GetStatusText_WithAllEnabled_ContainsClockBatteryNetwork()
    {
        var text = SystemTray.GetStatusText(showClock: true, showBattery: true, showNetwork: true);
        Assert.Contains(":", text);
    }

    [Fact]
    public void GetStatusText_ClockOnly_ContainsTime()
    {
        var text = SystemTray.GetStatusText(showClock: true, showBattery: false, showNetwork: false);
        var now = DateTime.Now;
        Assert.Contains(now.ToString("HH:"), text);
    }

    [Fact]
    public void GetStatusText_AllDisabled_ReturnsEmpty()
    {
        var text = SystemTray.GetStatusText(showClock: false, showBattery: false, showNetwork: false);
        Assert.Equal("", text);
    }

    [Fact]
    public void GetBatteryText_ReturnsStringOrEmpty()
    {
        var text = SystemTray.GetBatteryText();
        Assert.NotNull(text);
    }

    [Fact]
    public void GetNetworkText_ReturnsString()
    {
        var text = SystemTray.GetNetworkText();
        Assert.NotNull(text);
    }
}
