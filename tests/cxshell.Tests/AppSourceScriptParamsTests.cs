using cxshell.Apps;
using Xunit;

public class AppSourceScriptParamsTests
{
    [Fact]
    public void Parses_installer_and_uninstaller_params()
    {
        var ok = AppSource.TryParse(
            "binary+github-release:nickprotop/ServerHub?installer=install.sh&uninstaller=uninstall.sh&exe=serverhub",
            out var s);
        Assert.True(ok);
        Assert.Equal("install.sh", s.Installer);
        Assert.Equal("uninstall.sh", s.Uninstaller);
        Assert.Equal("serverhub", s.Exe);
    }

    [Fact]
    public void Installer_is_null_when_absent()
    {
        AppSource.TryParse("binary+github-release:nickprotop/cxtop?asset=cxtop-linux-{arch}", out var s);
        Assert.Null(s.Installer);
        Assert.Null(s.Uninstaller);
    }
}
