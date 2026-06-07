using cxshell.AppManager.Catalog;

namespace cxshell.Tests;

public class CatalogTests
{
    [Fact]
    public async Task EmbeddedCatalog_LoadsAllApps()
    {
        var apps = await new EmbeddedCatalogProvider().GetCatalogAsync();
        var ids = apps.Select(a => a.Id).ToHashSet();

        Assert.Contains("org.dotos.cxfiles", ids);
        Assert.Contains("org.dotos.cxtop", ids);
        Assert.Contains("org.dotos.cxpost", ids);
        Assert.Contains("org.dotos.lazydotide", ids);
        Assert.Contains("org.dotos.lazynuget", ids);
        Assert.Contains("org.dotos.serverhub", ids);
        Assert.Equal(6, apps.Count);
    }

    [Fact]
    public async Task EveryApp_HasAtLeastOneSource_WithParseableUri()
    {
        var apps = await new EmbeddedCatalogProvider().GetCatalogAsync();
        foreach (var app in apps)
        {
            Assert.NotEmpty(app.Sources);
            foreach (var s in app.Sources)
                Assert.True(cxshell.Apps.AppSource.TryParse(s.Uri, out _), $"{app.Id}: {s.Uri}");
        }
    }

    [Fact]
    public async Task SourceKindEntries_CarryABuildSpec()
    {
        var apps = await new EmbeddedCatalogProvider().GetCatalogAsync();
        foreach (var s in apps.SelectMany(a => a.Sources).Where(s => s.Kind == "source"))
            Assert.NotNull(s.Build);
    }
}
