namespace Scrubkit;

/// <summary>
/// The outcome of an incremental scan (<see cref="FolderScrubber.ReadChangesAsync"/>): the files
/// added or modified since the baseline (extracted, in <see cref="Changed"/>), the paths that
/// were <see cref="Removed"/>, and the complete up-to-date <see cref="Manifest"/> to persist for
/// next time. Unchanged files are neither re-extracted nor listed in <see cref="Changed"/> — they
/// are carried into <see cref="Manifest"/> unchanged.
/// </summary>
public sealed record IncrementalResult
{
    /// <summary>Added or modified files, extracted this run — the delta to (re)index.</summary>
    public IReadOnlyList<FileRecord> Changed { get; init; } = Array.Empty<FileRecord>();

    /// <summary>Paths present in the baseline but no longer on disk — safe to drop downstream.</summary>
    public IReadOnlyList<string> Removed { get; init; } = Array.Empty<string>();

    /// <summary>The complete current manifest (changed + carried-forward unchanged). Persist it for the next run.</summary>
    public Manifest Manifest { get; init; } = Manifest.Empty;
}
