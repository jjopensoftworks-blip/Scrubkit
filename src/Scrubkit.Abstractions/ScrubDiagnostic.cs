namespace Scrubkit;

/// <summary>
/// A per-file diagnostic emitted while scrubbing — a successful read or a problem. Handed to
/// <see cref="ReadOptions.OnDiagnostic"/> as each file is processed. This is a neutral,
/// dependency-free hook: bridge it to <c>ILogger</c> (see the
/// <c>Scrubkit.Extensions.DependencyInjection</c> package) or handle it yourself.
/// </summary>
public readonly struct ScrubDiagnostic
{
    /// <summary>Full path of the file this diagnostic is about.</summary>
    public string Path { get; }

    /// <summary>
    /// Short event code — <c>"read"</c> for a successful read, or a warning code such as
    /// <c>"extract-failed"</c>, <c>"skipped-content"</c>, <c>"text-clipped"</c>,
    /// <c>"stat-failed"</c>, or <c>"hash-failed"</c> (mirrors <see cref="FileRecord.Warnings"/>).
    /// </summary>
    public string Event { get; }

    /// <summary>Human-readable detail for the event.</summary>
    public string Message { get; }

    /// <summary><c>true</c> for a problem (log at warning level); <c>false</c> for a normal read.</summary>
    public bool IsWarning { get; }

    public ScrubDiagnostic(string path, string @event, string message, bool isWarning)
    {
        Path = path;
        Event = @event;
        Message = message;
        IsWarning = isWarning;
    }
}
