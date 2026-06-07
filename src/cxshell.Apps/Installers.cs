using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace cxshell.Apps;

/// <summary>Progress callback payload for an install/update/remove operation.</summary>
public readonly record struct InstallProgress(string Message, double? Fraction = null);

/// <summary>Result of an install/update: where the executable landed and the resolved version.</summary>
public readonly record struct InstallResult(string ExecPath, string Version);

public interface IInstaller
{
    bool CanHandle(AppSource source);

    /// <summary>Fetch/build and place the executable at the appropriate location under
    /// <paramref name="installRoot"/>. Returns the exec path + resolved version.</summary>
    Task<InstallResult> InstallAsync(AppSource source, string idLeaf, string installRoot,
        IProgress<InstallProgress> progress, CancellationToken ct);

    /// <summary>Best-effort latest available version for update checks (null if unknown).</summary>
    Task<string?> QueryLatestVersionAsync(AppSource source, CancellationToken ct);
}

/// <summary>
/// Binary distribution: any HTTPS URL (or a github-release that resolves to one), optionally a
/// compressed archive (tar.gz/tar.xz/zip) from which an <c>exe</c> is selected. Standard §5.1.
/// </summary>
public sealed class BinaryInstaller : IInstaller
{
    private readonly HttpClient _http;
    public BinaryInstaller(HttpClient http) => _http = http;

    public bool CanHandle(AppSource s) =>
        s.Kind == SourceKind.Binary && (s.Scheme is "url" or "github-release") && s.Installer == null;

    public async Task<InstallResult> InstallAsync(AppSource source, string idLeaf, string installRoot,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var (downloadUrl, version) = await ResolveUrlAsync(source, ct);
        var assetName = Path.GetFileName(downloadUrl);

        var tmp = Directory.CreateTempSubdirectory("dotos-install-");
        try
        {
            var assetPath = Path.Combine(tmp.FullName, "asset");
            await DownloadAsync(downloadUrl, assetName, assetPath, progress, ct);

            if (source.Sha256 is { } sha) VerifySha256(assetPath, sha);

            string execSrc;
            if (source.Archive == "none")
            {
                execSrc = assetPath;
            }
            else
            {
                progress.Report(new($"Extracting ({source.Archive})…"));
                var extractDir = Path.Combine(tmp.FullName, "x");
                Directory.CreateDirectory(extractDir);
                Extract(assetPath, source.Archive, extractDir, source.Strip, ct);
                var exeRel = source.Exe
                    ?? throw new InvalidOperationException("archive source requires 'exe' param");
                exeRel = AppSource.Expand(exeRel, AppSource.CurrentArch, AppSource.CurrentOs, version);
                execSrc = Path.Combine(extractDir, exeRel);
                if (!File.Exists(execSrc))
                    throw new FileNotFoundException($"exe '{exeRel}' not found in archive");
            }

            var execPath = Path.Combine(installRoot, idLeaf);
            Directory.CreateDirectory(installRoot);
            AtomicInstall(execSrc, execPath);
            Chmod(execPath, "755");
            progress.Report(new($"Installed to {execPath}", 1.0));
            return new(execPath, version);
        }
        finally
        {
            try { tmp.Delete(recursive: true); } catch { }
        }
    }

    public async Task<string?> QueryLatestVersionAsync(AppSource source, CancellationToken ct)
    {
        if (source.Scheme == "github-release")
        {
            var (_, tag) = await GitHubLatestAsync(source, ct);
            return tag;
        }
        return source.VersionHint; // url: no version channel unless declared
    }

    private async Task<(string url, string version)> ResolveUrlAsync(AppSource source, CancellationToken ct)
    {
        if (source.Scheme == "url")
        {
            var url = AppSource.Expand(source.Locator, AppSource.CurrentArch, AppSource.CurrentOs,
                source.VersionHint);
            return (url, source.VersionHint ?? "0");
        }
        // github-release → resolve asset download URL from the (latest or pinned) release
        var (assetUrl, tag) = await GitHubLatestAsync(source, ct);
        return (assetUrl, tag);
    }

    private async Task<(string assetUrl, string tag)> GitHubLatestAsync(AppSource source, CancellationToken ct)
    {
        var repo = source.Locator; // owner/repo
        var apiUrl = source.Tag is { } t
            ? $"https://api.github.com/repos/{repo}/releases/tags/{t}"
            : $"https://api.github.com/repos/{repo}/releases/latest";

        using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        req.Headers.UserAgent.ParseAdd("DotOS-AppManager");
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "0";

        var assetPattern = AppSource.Expand(source.Asset ?? "",
            AppSource.CurrentArch, AppSource.CurrentOs, tag.TrimStart('v'));
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name == assetPattern)
                return (asset.GetProperty("browser_download_url").GetString()!, tag);
        }
        throw new FileNotFoundException($"No release asset matching '{assetPattern}' in {repo}@{tag}");
    }

    private async Task DownloadAsync(string url, string label, string dest, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(dest);
        var buf = new byte[81920];
        long read = 0; int n;
        // Use one stable message for every chunk: the modal coalesces identical messages to a
        // single log line, while the Fraction keeps driving the progress bar.
        var msg = $"Downloading {label}…";
        progress.Report(new(msg));
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            read += n;
            if (total is > 0)
                progress.Report(new(msg, (double)read / total.Value));
        }
    }

    private static void Extract(string archivePath, string fmt, string destDir, int strip, CancellationToken ct)
    {
        switch (fmt)
        {
            case "zip":
                ZipFile.ExtractToDirectory(archivePath, destDir, overwriteFiles: true);
                if (strip > 0) StripComponents(destDir, strip);
                break;
            case "tar.gz" or "tgz":
                RunTar($"-xzf \"{archivePath}\" -C \"{destDir}\" --strip-components={strip}", ct);
                break;
            case "tar.xz":
                RunTar($"-xJf \"{archivePath}\" -C \"{destDir}\" --strip-components={strip}", ct);
                break;
            default:
                throw new NotSupportedException($"Unsupported archive format '{fmt}'");
        }
    }

    private static void StripComponents(string dir, int strip)
    {
        // Best-effort for zip: if everything is under N leading dirs, hoist contents up.
        for (var i = 0; i < strip; i++)
        {
            var entries = Directory.GetFileSystemEntries(dir);
            if (entries.Length == 1 && Directory.Exists(entries[0]))
            {
                var inner = entries[0];
                foreach (var e in Directory.GetFileSystemEntries(inner))
                    Directory.Move(e, Path.Combine(dir, Path.GetFileName(e)));
                Directory.Delete(inner, true);
            }
        }
    }

    private static void RunTar(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("tar", args) { RedirectStandardError = true, UseShellExecute = false };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("failed to start tar");
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"tar failed: {p.StandardError.ReadToEnd()}");
    }

    private static void VerifySha256(string path, string expected)
    {
        using var fs = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
        if (!hash.Equals(expected.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            throw new InvalidOperationException("sha256 mismatch on downloaded asset");
    }

    private static void AtomicInstall(string src, string dest)
    {
        var staged = dest + ".new";
        File.Copy(src, staged, overwrite: true);
        if (File.Exists(dest)) File.Replace(staged, dest, dest + ".bak", ignoreMetadataErrors: true);
        else File.Move(staged, dest);
    }

    internal static void Chmod(string path, string mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("chmod", $"{mode} \"{path}\"")
                { UseShellExecute = false });
                p?.WaitForExit();
            }
            catch { }
        }
    }
}

/// <summary>
/// Source distribution: fetch a git ref (or use a local path), run a structured, **sandboxed**
/// build pipeline (Standard §5.1/§5.4), and install the produced artifact. The pipeline comes
/// from the chosen catalog source's build descriptor (or the recorded X-DotOS-Build on update).
/// </summary>
public sealed class SourceInstaller : IInstaller
{
    private readonly ISandbox _sandbox;

    /// <param name="sandbox">The isolation used for every build step. Pass a BwrapSandbox in
    /// production; NoneSandbox only as an explicit dev opt-out.</param>
    public SourceInstaller(ISandbox sandbox) => _sandbox = sandbox;

    public bool CanHandle(AppSource s) =>
        s.Kind == SourceKind.Source && (s.Scheme is "git" or "path");

    public async Task<InstallResult> InstallAsync(AppSource source, string idLeaf, string installRoot,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var build = source.Build
            ?? throw new InvalidOperationException("source distribution requires a build descriptor");
        if (!_sandbox.IsAvailable)
            throw new InvalidOperationException(
                $"build sandbox '{_sandbox.Name}' unavailable; refusing to build unsandboxed");

        var work = Directory.CreateTempSubdirectory("dotos-build-");
        try
        {
            string srcDir;
            string version;
            if (source.Scheme == "git")
            {
                progress.Report(new($"Cloning {source.Locator}…"));
                var cloneArgs = new List<string> { "clone", "--depth", "1" };
                if (source.Ref is { } r) { cloneArgs.Add("--branch"); cloneArgs.Add(r); }
                cloneArgs.Add(source.Locator);
                cloneArgs.Add(Path.Combine(work.FullName, "repo"));
                // Clone outside the sandbox (it only needs to write the work dir); the *build*
                // is what runs untrusted code, so that is what we sandbox.
                await ProcessRunner.RunAsync("git", cloneArgs, work.FullName,
                    l => progress.Report(new(l)), ct);
                srcDir = Path.Combine(work.FullName, "repo");
                version = await GitDescribeAsync(srcDir, ct);
            }
            else // path
            {
                srcDir = source.Locator;
                version = "0+path";
            }

            var outDir = Path.Combine(work.FullName, "out");
            Directory.CreateDirectory(outDir);
            var policy = new SandboxPolicy(
                ReadWritePaths: new[] { work.FullName, outDir },
                Network: build.Net,
                WorkingDirectory: srcDir);

            progress.Report(new($"Building in sandbox '{_sandbox.Name}'…"));
            foreach (var step in build.Steps)
            {
                var (exe, args) = ResolveStep(step, srcDir);
                progress.Report(new($"$ {exe} {string.Join(' ', args)}"));
                await _sandbox.RunAsync(exe, args, policy, l => progress.Report(new(l)), ct);
            }

            // Locate the produced artifact + entry exe.
            var artifactPath = Path.Combine(srcDir, build.Artifact);
            string builtExe = Directory.Exists(artifactPath)
                ? Path.Combine(artifactPath, build.Exe)
                : artifactPath;
            if (!File.Exists(builtExe))
                throw new FileNotFoundException($"build artifact exe '{build.Exe}' not found at {builtExe}");

            var execPath = Path.Combine(installRoot, idLeaf);
            Directory.CreateDirectory(installRoot);
            File.Copy(builtExe, execPath, overwrite: true);
            BinaryInstaller.Chmod(execPath, "755");
            progress.Report(new($"Installed to {execPath}", 1.0));
            return new(execPath, version);
        }
        finally
        {
            try { work.Delete(recursive: true); } catch { }
        }
    }

    public async Task<string?> QueryLatestVersionAsync(AppSource source, CancellationToken ct)
    {
        if (source.Scheme != "git") return null;
        try
        {
            var sb = new System.Text.StringBuilder();
            await ProcessRunner.RunAsync("git",
                new[] { "ls-remote", source.Locator, source.Ref ?? "HEAD" },
                Path.GetTempPath(), l => sb.AppendLine(l), ct);
            return sb.ToString().Split('\t').FirstOrDefault()?.Trim() is { Length: >= 12 } s
                ? s[..12] : null;
        }
        catch { return null; }
    }

    /// <summary>Resolve a build step to an (exe, args) pair. {run} is argv-split; {script} runs a tree-relative script.</summary>
    private static (string exe, string[] args) ResolveStep(BuildStep step, string srcDir)
    {
        if (step.Run is { } run)
        {
            var parts = run.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) throw new InvalidOperationException("empty run step");
            return (parts[0], parts[1..]);
        }
        if (step.Script is { } script)
            return ("/bin/sh", new[] { Path.Combine(srcDir, script) });
        throw new InvalidOperationException("build step must have 'run' or 'script'");
    }

    private static async Task<string> GitDescribeAsync(string repo, CancellationToken ct)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            await ProcessRunner.RunAsync("git", new[] { "-C", repo, "rev-parse", "--short", "HEAD" },
                repo, l => sb.AppendLine(l), ct);
            return "0.0.0+src." + sb.ToString().Trim();
        }
        catch { return "0.0.0+src"; }
    }
}
