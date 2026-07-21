namespace Scrubkit;

/// <summary>
/// A slice of one file's extracted text, sized for embedding / retrieval. Chunks carry enough
/// of the source <see cref="FileRecord"/> (path, name, type, metadata) to be indexed and cited
/// on their own, plus their position within the file so overlapping windows stay ordered.
/// </summary>
public sealed record Chunk
{
    /// <summary>Full path of the source file on disk.</summary>
    public required string Path { get; init; }

    /// <summary>Source file name including extension.</summary>
    public required string Name { get; init; }

    /// <summary>Coarse type of the source file (Document, Text, Email, …).</summary>
    public string TypeBucket { get; init; } = "Other";

    /// <summary>Zero-based ordinal of this chunk within its file.</summary>
    public int Index { get; init; }

    /// <summary>Total number of chunks produced from the same file.</summary>
    public int Count { get; init; }

    /// <summary>Character offset of this chunk's text within the source <see cref="FileRecord.Text"/>.</summary>
    public int StartOffset { get; init; }

    /// <summary>The chunk text.</summary>
    public required string Text { get; init; }

    /// <summary>Metadata carried over from the source file (title/author/…), for filtering or citation.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}
