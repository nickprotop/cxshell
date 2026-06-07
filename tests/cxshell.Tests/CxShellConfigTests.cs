using cxshell.Config;

namespace cxshell.Tests;

public class CxShellConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var config = CxShellConfig.Default;

        Assert.Equal("cxshell", config.DesktopTitle);
        Assert.Equal("/usr/share/cxshell/apps", config.AppsDirectory);
        Assert.Equal("/etc/cxshell/autostart.conf", config.AutostartConfigPath);
        Assert.True(config.ShowClock);
        Assert.True(config.ShowNetworkStatus);
        Assert.True(config.ShowBatteryStatus);
        Assert.Equal(5, config.SystemTrayUpdateIntervalSeconds);
    }
}
