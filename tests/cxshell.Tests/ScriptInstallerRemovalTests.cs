using cxshell.Apps;
using Xunit;

public class ScriptInstallerRemovalTests
{
    [Fact]
    public void KnownPathRemoval_deletes_binary_widgets_and_uninstaller()
    {
        var root = Directory.CreateTempSubdirectory("dotos-rm-");
        var bin = Path.Combine(root.FullName, ".local", "bin");
        var share = Path.Combine(root.FullName, ".local", "share", "serverhub");
        Directory.CreateDirectory(bin);
        Directory.CreateDirectory(share);
        File.WriteAllText(Path.Combine(bin, "serverhub"), "x");
        File.WriteAllText(Path.Combine(bin, "serverhub-uninstall"), "x");
        File.WriteAllText(Path.Combine(share, "w.txt"), "x");

        ScriptInstaller.KnownPathRemove(root.FullName, exe: "serverhub", idLeaf: "serverhub");

        Assert.False(File.Exists(Path.Combine(bin, "serverhub")));
        Assert.False(File.Exists(Path.Combine(bin, "serverhub-uninstall")));
        Assert.False(Directory.Exists(share));
    }
}
