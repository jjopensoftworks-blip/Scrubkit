namespace Scrubkit;

/// <summary>
/// One row of the output table: what we could learn about a single file,
/// with sensitive values already scrubbed out of <see cref="Text"/> and metadata.
/// </summary>
public sealed record FileRecord
{
    /// <summary>Full path on disk.</summary>
    public required string Path { get; init; }

    /// <summary>File name including extension.</summary>
    public required string Name { get; init; }

    /// <summary>Lower-case extension including the dot (e.g. <c>".pdf"</c>).</summary>
    public required string Extension { get; init; }

    /// <summary>Immediate parent folder name (not the full path).</summary>
    public string Folder { get; init; } = "";

    public long SizeBytes { get; init; }
    public DateTime Modified { get; init; }

    /// <summary>Coarse bucket: Document, Spreadsheet, Presentation, Text, Image, Email, Other.</summary>
    public string TypeBucket { get; init; } = "Other";

    /// <summary>Embedded metadata (title/author/camera/pages…), also passed through the redactor.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Extracted, scrubbed text. Empty when the type isn't text-bearing or reading failed.</summary>
    public string Text { get; init; } = "";

    /// <summary>How many values of each kind were scrubbed (e.g. <c>{"Email":2,"Phone":1}</c>).</summary>
    public IReadOnlyDictionary<string, int> Redactions { get; init; } =
        new Dictionary<string, int>();

    /// <summary>True if any value was scrubbed from this file.</summary>
    public bool HasSensitiveData => Redactions.Count > 0;

    /// <summary>Non-fatal problems (skipped-too-large, unreadable, clipped). Never throws to the caller.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
