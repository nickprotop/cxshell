using System.Collections.Specialized;
using System.Web;

namespace cxshell.Apps;

public enum SourceKind { Binary, Source }

/// <summary>
/// A parsed <c>X-DotOS-Source</c> value (DotOS Application Standard §5.1):
/// <c>&lt;kind&gt;+&lt;scheme&gt;:&lt;locator&gt;[?params]</c>.
/// Example: <c>binary+github-release:nickprotop/cxfiles?asset=cxfiles-linux-{arch}&amp;archive=tar.gz&amp;exe=cxfiles</c>.
/// </summary>
public sealed class AppSource
{
    public required SourceKind Kind { get; init; }
    public required string Scheme { get; init; }   // github-release | url | apt | git | path
    public required string Locator { get; init; }   // owner/repo, https url, git url, path…
    public required string Raw { get; init; }
    public NameValueCollection Params { get; init; } = new();

    public string? Param(string key) => Params[key];

    // Common binary params (standard §5.1)
    public string? Asset => Params["asset"];
    public string? Tag => Params["tag"];
    public string Archive => Params["archive"] ?? "none";
    public string? Exe => Params["exe"];
    /// <summary>Release asset name of a publisher install script (e.g. install.sh). When set,
    /// the binary is installed by running this script rather than downloading a single asset.</summary>
    public string? Installer => Params["installer"];
    /// <summary>Release asset name of a publisher uninstall script (e.g. uninstall.sh).</summary>
    public string? Uninstaller => Params["uninstaller"];
    public int Strip => int.TryParse(Params["strip"], out var n) ? n : 0;
    public string? Sha256 => Params["sha256"];
    public string? VersionHint => Params["version"];
    // Source params
    public string? Ref => Params["ref"];

    /// <summary>
    /// Structured build pipeline for source distribution. Not part of the URI — attached by the
    /// caller from the catalog's <c>sources[].build</c> (or the installed manifest's
    /// <c>X-DotOS-Build</c>). Required for <see cref="SourceKind.Source"/>.
    /// </summary>
    public BuildSpec? Build { get; set; }

    public static bool TryParse(string? value, out AppSource source)
    {
        source = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var plus = value.IndexOf('+');
        var colon = value.IndexOf(':');
        if (plus <= 0 || colon <= plus + 1) return false;

        var kindStr = value[..plus];
        SourceKind kind;
        if (kindStr.Equals("binary", StringComparison.OrdinalIgnoreCase)) kind = SourceKind.Binary;
        else if (kindStr.Equals("source", StringComparison.OrdinalIgnoreCase)) kind = SourceKind.Source;
        else return false;

        var scheme = value[(plus + 1)..colon];
        var rest = value[(colon + 1)..];

        // Split params off the locator. For url/git the locator itself is a URL that may contain
        // '?', so only treat the FIRST '?' as the param separator and re-attach if the scheme is
        // url/git and the locator's own query is meaningful — here we keep it simple: the first
        // '?' begins DotOS params (URLs needing query strings should encode them).
        string locator = rest;
        var query = "";
        var q = rest.IndexOf('?');
        if (q >= 0) { locator = rest[..q]; query = rest[(q + 1)..]; }

        source = new AppSource
        {
            Kind = kind,
            Scheme = scheme,
            Locator = locator,
            Raw = value,
            Params = HttpUtility.ParseQueryString(query),
        };
        return true;
    }

    /// <summary>Substitute {arch}/{os}/{version} placeholders in a template.</summary>
    public static string Expand(string template, string arch, string os, string? version = null) =>
        template
            .Replace("{arch}", arch)
            .Replace("{os}", os)
            .Replace("{version}", version ?? "");

    /// <summary>Current runtime arch token (x64 / arm64).</summary>
    public static string CurrentArch =>
        System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            _ => "x64",
        };

    public const string CurrentOs = "linux";
}
