using System.Diagnostics;

namespace cxshell.Apps;

/// <summary>
/// Installs a binary app by running the publisher's release install script (e.g. install.sh)
/// instead of downloading a single asset. The script is NOT sandboxed — callers MUST obtain
/// user consent first (Application Standard §5). Used for multi-asset apps like ServerHub
/// (binary + widgets). Removal (added separately) runs the publisher uninstall script under a
/// pty, with best-effort known-path deletion as a fallback.
/// </summary>
public sealed class ScriptInstaller : IInstaller
{
    private readonly HttpClient _http;
    public ScriptInstaller(HttpClient http) => _http = http;

    public bool CanHandle(AppSource s) =>
        s.Kind == SourceKind.Binary && s.Scheme == "github-release" && s.Installer != null;

    public async Task<InstallResult> InstallAsync(AppSource source, string idLeaf, string installRoot,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var installerAsset = source.Installer
            ?? throw new InvalidOperationException("ScriptInstaller requires an 'installer' param");
        var exe = source.Exe ?? idLeaf;

        var (scriptPath, tag) = await DownloadScriptAsync(source, installerAsset, progress, ct);
        await RunScriptAsync(scriptPath, feedYes: false, progress, ct);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var execPath = Path.Combine(home, ".local", "bin", exe);
        progress.Report(new($"Installed via {installerAsset} ({tag})", 1.0));
        return new InstallResult(execPath, tag);
    }

    public async Task<string?> QueryLatestVersionAsync(AppSource source, CancellationToken ct)
    {
        var tag = await GitHubLatestTagAsync(source, ct);
        return tag;
    }

    /// <summary>Remove a script-installed app: run the publisher uninstaller (pty-fed 'y'); on
    /// any failure, fall back to deleting the known paths the uninstaller targets.</summary>
    public async Task RemoveAsync(AppSource source, string idLeaf,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var exe = source.Exe ?? idLeaf;
        if (source.Uninstaller is { } asset)
        {
            try
            {
                var (scriptPath, _) = await DownloadScriptAsync(source, asset, progress, ct);
                await RunScriptAsync(scriptPath, feedYes: true, progress, ct);
                return;
            }
            catch (Exception ex)
            {
                progress.Report(new($"uninstaller failed ({ex.Message}); removing known paths"));
            }
        }
        KnownPathRemove(home, exe, idLeaf);
        progress.Report(new("Removed.", 1.0));
    }

    /// <summary>Delete the paths a publisher uninstaller targets: binary, its -uninstall helper,
    /// and the app's share dir. Config (~/.config/&lt;idLeaf&gt;) is left in place by default.</summary>
    public static void KnownPathRemove(string home, string exe, string idLeaf)
    {
        var bin = Path.Combine(home, ".local", "bin");
        TryDeleteFile(Path.Combine(bin, exe));
        TryDeleteFile(Path.Combine(bin, $"{exe}-uninstall"));
        TryDeleteDir(Path.Combine(home, ".local", "share", idLeaf));
    }

    private static void TryDeleteFile(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    private static void TryDeleteDir(string p) { try { if (Directory.Exists(p)) Directory.Delete(p, true); } catch { } }

    // --- helpers ---

    private async Task<(string path, string tag)> DownloadScriptAsync(
        AppSource source, string asset, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var tag = await GitHubLatestTagAsync(source, ct);
        var url = $"https://github.com/{source.Locator}/releases/download/{tag}/{asset}";
        progress.Report(new($"Downloading {asset} ({tag})…"));
        var tmp = Path.Combine(Path.GetTempPath(), $"dotos-{Guid.NewGuid():N}-{asset}");
        var bytes = await _http.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(tmp, bytes, ct);
        Chmod755(tmp);
        return (tmp, tag);
    }

    private async Task<string> GitHubLatestTagAsync(AppSource source, CancellationToken ct)
    {
        var api = $"https://api.github.com/repos/{source.Locator}/releases/latest";
        using var req = new HttpRequestMessage(HttpMethod.Get, api);
        req.Headers.UserAgent.ParseAdd("DotOS-AppManager");
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("tag_name").GetString() ?? "0";
    }

    private static void Chmod755(string path)
    {
        using var p = Process.Start(new ProcessStartInfo("chmod", $"755 \"{path}\"") { UseShellExecute = false });
        p?.WaitForExit();
    }

    /// <summary>Run a downloaded script. feedYes=true runs it under a pty answering "y" to
    /// /dev/tty prompts (for the interactive uninstaller); false runs it plainly (installer).</summary>
    internal static async Task RunScriptAsync(string scriptPath, bool feedYes,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var (exe, args) = feedYes
            ? ("/usr/bin/script", $"-qec \"yes y | {scriptPath}\" /dev/null")
            : ("/bin/sh", $"\"{scriptPath}\"");

        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"failed to start {exe}");
        p.OutputDataReceived += (_, e) => { if (e.Data != null) progress.Report(new(e.Data)); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) progress.Report(new(e.Data)); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"script exited with code {p.ExitCode}");
    }
}
