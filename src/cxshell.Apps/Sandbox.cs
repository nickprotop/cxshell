using System.Diagnostics;

namespace cxshell.Apps;

/// <summary>What a sandboxed command is allowed to touch (DotOS Application Standard §5.4).</summary>
public sealed record SandboxPolicy(
    IReadOnlyList<string> ReadWritePaths,
    bool Network,
    string WorkingDirectory);

/// <summary>
/// Runs a build command under an isolation policy. Every source build MUST go through this; only
/// the implementation varies (bwrap by default; none for explicit dev opt-out).
/// </summary>
public interface ISandbox
{
    /// <summary>Whether this sandbox is usable on the current host.</summary>
    bool IsAvailable { get; }
    string Name { get; }

    /// <summary>
    /// Run <paramref name="exe"/> + <paramref name="args"/> under <paramref name="policy"/>,
    /// streaming output lines to <paramref name="onLine"/>. Throws on non-zero exit.
    /// </summary>
    Task RunAsync(string exe, IReadOnlyList<string> args, SandboxPolicy policy,
        Action<string> onLine, CancellationToken ct);
}

/// <summary>
/// bubblewrap sandbox: read-only root, private /tmp + PID namespace, RW only the policy's paths,
/// network only when requested. Unprivileged (Flatpak's mechanism).
/// </summary>
public sealed class BwrapSandbox : ISandbox
{
    public string Name => "bwrap";

    public bool IsAvailable => ResolveBwrap() != null;

    public async Task RunAsync(string exe, IReadOnlyList<string> args, SandboxPolicy policy,
        Action<string> onLine, CancellationToken ct)
    {
        var bwrap = ResolveBwrap() ?? throw new InvalidOperationException("bwrap not found");

        var a = new List<string>
        {
            "--ro-bind", "/", "/",          // read-only system + toolchain
            "--dev", "/dev",
            "--proc", "/proc",
            "--tmpfs", "/tmp",              // private tmp
            "--unshare-pid",
            "--die-with-parent",
        };
        if (!policy.Network) a.Add("--unshare-net");
        foreach (var rw in policy.ReadWritePaths)
        {
            a.Add("--bind"); a.Add(rw); a.Add(rw);   // writable build + output dirs
        }
        a.Add("--chdir"); a.Add(policy.WorkingDirectory);
        a.Add("--"); a.Add(exe); a.AddRange(args);

        await ProcessRunner.RunAsync(bwrap, a, policy.WorkingDirectory, onLine, ct);
    }

    private static string? ResolveBwrap()
    {
        foreach (var p in new[] { "/usr/bin/bwrap", "/bin/bwrap", "/usr/local/bin/bwrap" })
            if (File.Exists(p)) return p;
        return null;
    }
}

/// <summary>
/// No isolation — explicit dev/CI opt-out (e.g. where bwrap is unavailable). Logs loudly via the
/// output stream so its use is never silent. Source installs default to bwrap and fail closed if
/// neither a working sandbox nor an explicit NoneSandbox is provided.
/// </summary>
public sealed class NoneSandbox : ISandbox
{
    public string Name => "none";
    public bool IsAvailable => true;

    public async Task RunAsync(string exe, IReadOnlyList<string> args, SandboxPolicy policy,
        Action<string> onLine, CancellationToken ct)
    {
        onLine($"[sandbox=none] {exe} {string.Join(' ', args)}");
        await ProcessRunner.RunAsync(exe, args, policy.WorkingDirectory, onLine, ct);
    }
}

internal static class ProcessRunner
{
    public static async Task RunAsync(string exe, IReadOnlyList<string> args, string cwd,
        Action<string> onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"failed to start {exe}");
        p.OutputDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{exe} exited {p.ExitCode}");
    }
}
