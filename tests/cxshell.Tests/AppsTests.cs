using cxshell.Apps;

namespace cxshell.Tests;

public class DesktopEntryTests
{
    [Fact]
    public void Parses_StandardAndExtensionKeys()
    {
        var text = """
        [Desktop Entry]
        Type=Application
        Name=Files
        Comment=Browse files
        Exec=/opt/cx/cxfiles %F
        Icon=
        Categories=System;FileManager;
        X-DotOS-Group=Applications
        X-DotOS-Order=10
        X-DotOS-Source=binary+github-release:nickprotop/cxfiles?asset=cxfiles-linux-{arch}
        X-DotOS-Version=1.4.0
        """;
        var e = DesktopEntry.Parse(text, "org.dotos.cxfiles");

        Assert.Equal("org.dotos.cxfiles", e.Id);
        Assert.Equal("Files", e.Name);
        Assert.Equal("/opt/cx/cxfiles %F", e.Exec);
        Assert.Equal("Applications", e.Group_DotOS);
        Assert.Equal(10, e.Order);
        Assert.False(e.Maximize); // default false — apps open centered unless X-DotOS-Maximize=true
        Assert.Contains("FileManager", e.Categories);
        Assert.Equal("1.4.0", e.Version);
        Assert.True(e.IsLaunchable);
    }

    [Fact]
    public void NoDisplay_IsNotLaunchable()
    {
        var e = DesktopEntry.Parse("[Desktop Entry]\nType=Application\nName=X\nNoDisplay=true\n", "x");
        Assert.False(e.IsLaunchable);
    }

    [Fact]
    public void RoundTrips_PreservingKeys()
    {
        var text = "[Desktop Entry]\nType=Application\nName=X\nX-Custom-Key=keepme\n";
        var e = DesktopEntry.Parse(text, "x");
        var outText = e.ToString();
        Assert.Contains("X-Custom-Key=keepme", outText);
        Assert.Contains("Name=X", outText);
    }
}

public class AppSourceTests
{
    [Fact]
    public void Parses_BinaryGithubRelease_WithArchiveParams()
    {
        Assert.True(AppSource.TryParse(
            "binary+github-release:nickprotop/cxfiles?asset=cxfiles-linux-{arch}&archive=tar.gz&exe=cxfiles&strip=1",
            out var s));
        Assert.Equal(SourceKind.Binary, s.Kind);
        Assert.Equal("github-release", s.Scheme);
        Assert.Equal("nickprotop/cxfiles", s.Locator);
        Assert.Equal("cxfiles-linux-{arch}", s.Asset);
        Assert.Equal("tar.gz", s.Archive);
        Assert.Equal("cxfiles", s.Exe);
        Assert.Equal(1, s.Strip);
    }

    [Fact]
    public void Parses_BinaryUrl_AnyLocation()
    {
        Assert.True(AppSource.TryParse("binary+url:https://example.com/dl/app-{arch}?sha256=abc", out var s));
        Assert.Equal("url", s.Scheme);
        Assert.Equal("https://example.com/dl/app-{arch}", s.Locator);
        Assert.Equal("abc", s.Sha256);
    }

    [Fact]
    public void Parses_SourceGit()
    {
        Assert.True(AppSource.TryParse(
            "source+git:https://github.com/nickprotop/cxtop?ref=main",
            out var s));
        Assert.Equal(SourceKind.Source, s.Kind);
        Assert.Equal("git", s.Scheme);
        Assert.Equal("https://github.com/nickprotop/cxtop", s.Locator);
        Assert.Equal("main", s.Ref);
        // The build pipeline is a structured descriptor attached separately, not in the URI.
        Assert.Null(s.Build);
    }

    [Theory]
    [InlineData("")]
    [InlineData("notasource")]
    [InlineData("bogus+url:x")]
    public void Rejects_Invalid(string v) => Assert.False(AppSource.TryParse(v, out _));

    [Fact]
    public void Expand_SubstitutesPlaceholders() =>
        Assert.Equal("app-linux-x64-1.0",
            AppSource.Expand("app-{os}-{arch}-{version}", "x64", "linux", "1.0"));
}

public class BuildSpecTests
{
    [Fact]
    public void RoundTrips_Json()
    {
        var spec = new BuildSpec
        {
            Steps = { new BuildStep { Run = "dotnet publish App.csproj -c Release -o out" },
                      new BuildStep { Script = "post.sh" } },
            Artifact = "out",
            Exe = "App",
            Net = true,
        };
        var json = spec.ToJson();
        var back = BuildSpec.FromJson(json)!;
        Assert.Equal(2, back.Steps.Count);
        Assert.Equal("dotnet publish App.csproj -c Release -o out", back.Steps[0].Run);
        Assert.Equal("post.sh", back.Steps[1].Script);
        Assert.Equal("out", back.Artifact);
        Assert.Equal("App", back.Exe);
        Assert.True(back.Net);
    }

    [Fact]
    public void Describe_ListsStepsForReview()
    {
        var spec = new BuildSpec { Steps = { new BuildStep { Run = "make" } }, Artifact = "bin/app", Exe = "app" };
        var d = spec.Describe();
        Assert.Contains("run: make", d);
        Assert.Contains("artifact: bin/app", d);
    }

    [Fact]
    public void FromJson_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(BuildSpec.FromJson(null));
        Assert.Null(BuildSpec.FromJson("  "));
    }
}

public class SandboxTests
{
    [Fact]
    public void NoneSandbox_IsAlwaysAvailable() => Assert.True(new NoneSandbox().IsAvailable);

    [Fact]
    public async Task NoneSandbox_RunsCommand_AndStreamsOutput()
    {
        var lines = new List<string>();
        var policy = new SandboxPolicy(Array.Empty<string>(), Network: false, Path.GetTempPath());
        await new NoneSandbox().RunAsync("/bin/echo", new[] { "hello" }, policy, lines.Add, default);
        Assert.Contains(lines, l => l.Contains("hello"));
    }
}

public class ManifestStoreTests : IDisposable
{
    private readonly string _sys;
    private readonly string _user;

    public ManifestStoreTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dotos-ms-{Guid.NewGuid()}");
        _sys = Path.Combine(root, "system");
        _user = Path.Combine(root, "user");
        Directory.CreateDirectory(_sys);
        Directory.CreateDirectory(_user);
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_sys)!;
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private void Write(string dir, string id, string body) =>
        File.WriteAllText(Path.Combine(dir, id + ".desktop"), body);

    [Fact]
    public void Enumerate_SortsByGroupOrderName()
    {
        Write(_sys, "a", "[Desktop Entry]\nType=Application\nName=Bravo\nX-DotOS-Group=System\nX-DotOS-Order=20\n");
        Write(_sys, "b", "[Desktop Entry]\nType=Application\nName=Alpha\nX-DotOS-Group=Applications\nX-DotOS-Order=10\n");
        var store = new ManifestStore(_sys, _user);
        var list = store.Enumerate();
        Assert.Equal(2, list.Count);
        Assert.Equal("Alpha", list[0].Name); // Applications < System
    }

    [Fact]
    public void UserManifest_OverridesSystem_ById()
    {
        Write(_sys, "org.x", "[Desktop Entry]\nType=Application\nName=SystemVer\n");
        Write(_user, "org.x", "[Desktop Entry]\nType=Application\nName=UserVer\n");
        var store = new ManifestStore(_sys, _user);
        var e = Assert.Single(store.Enumerate());
        Assert.Equal("UserVer", e.Name);
    }

    [Fact]
    public void UserOverride_CanMaskSystemApp_WithNoDisplay()
    {
        Write(_sys, "org.x", "[Desktop Entry]\nType=Application\nName=SystemVer\n");
        Write(_user, "org.x", "[Desktop Entry]\nType=Application\nName=SystemVer\nNoDisplay=true\n");
        var store = new ManifestStore(_sys, _user);
        Assert.Empty(store.Enumerate());
        Assert.True(store.HasSystemManifest("org.x"));
    }
}
