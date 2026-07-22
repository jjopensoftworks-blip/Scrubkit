namespace Scrubkit;

/// <summary>
/// One file's identity in a manifest — its path plus the cheap-to-read facts (size, last-write
/// time, optional content hash) used on a later run to tell whether the file changed, without
/// re-extracting it. Change detection compares <see cref="SizeBytes"/> and <see cref="Modified"/>.
/// (The <c>Manifest</c> container and incremental scan live in the core <c>Scrubkit</c> package.)
/// </summary>
public sealed record ManifestEntry
{
    /// <summary>Full path on disk, as produced by enumeration.</summary>
    public required string Path { get; init; }

    /// <summary>File size in bytes when the entry was recorded.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Last-write time in <b>UTC</b> when the entry was recorded.</summary>
    public DateTime Modified { get; init; }

    /// <summary>Content hash if one was computed (else <c>null</c>); carried through incremental runs.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Projects a <see cref="FileRecord"/> to its manifest identity.</summary>
    public static ManifestEntry From(FileRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        return new ManifestEntry
        {
            Path = record.Path,
            SizeBytes = record.SizeBytes,
            Modified = record.Modified,
            ContentHash = record.ContentHash,
        };
    }
}
