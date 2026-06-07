using System.Text.Json;
using System.Text.Json.Serialization;

namespace cxshell.Apps;

/// <summary>One step in a source build pipeline (DotOS Application Standard §5.1).</summary>
public sealed class BuildStep
{
    /// <summary>An argv-split command run inside the sandbox (no shell).</summary>
    [JsonPropertyName("run")] public string? Run { get; set; }

    /// <summary>A script (relative to the source tree) executed inside the sandbox.</summary>
    [JsonPropertyName("script")] public string? Script { get; set; }
}

/// <summary>
/// Structured build pipeline for a source-distributed app: ordered steps producing an
/// <see cref="Artifact"/>, with <see cref="Exe"/> the entry executable. Persisted into the
/// installed manifest's <c>X-DotOS-Build</c> so updates can rebuild without the catalog.
/// </summary>
public sealed class BuildSpec
{
    [JsonPropertyName("steps")] public List<BuildStep> Steps { get; set; } = new();

    /// <summary>File or directory produced by the steps, relative to the work dir.</summary>
    [JsonPropertyName("artifact")] public string Artifact { get; set; } = "";

    /// <summary>Entry executable; relative to <see cref="Artifact"/> when it is a directory.</summary>
    [JsonPropertyName("exe")] public string Exe { get; set; } = "";

    /// <summary>Whether the build sandbox is granted network (default true, for restore).</summary>
    [JsonPropertyName("net")] public bool Net { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static BuildSpec? FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<BuildSpec>(json, JsonOpts);

    /// <summary>Human-readable summary for the pre-execution review the App Manager must show.</summary>
    public string Describe()
    {
        var lines = Steps.Select((s, i) =>
            $"  {i + 1}. {(s.Run != null ? "run: " + s.Run : "script: " + s.Script)}");
        return $"Build pipeline (net={Net}):\n{string.Join('\n', lines)}\n  → artifact: {Artifact} (exe: {Exe})";
    }
}
