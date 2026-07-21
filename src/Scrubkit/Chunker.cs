namespace Scrubkit;

/// <summary>
/// How to slice text into chunks. Defaults suit general-purpose embedding: ~1 000-character
/// windows with a 100-character overlap so context spans chunk boundaries.
/// </summary>
public sealed class ChunkOptions
{
    /// <summary>Maximum characters per chunk. Must be positive. Default: 1 000.</summary>
    public int MaxChars { get; set; } = 1_000;

    /// <summary>
    /// Characters each chunk repeats from the end of the previous one, so a phrase split across
    /// a boundary still appears whole in one chunk. Must be in <c>[0, MaxChars)</c>. Default: 100.
    /// </summary>
    public int OverlapChars { get; set; } = 100;

    /// <summary>
    /// When true (the default), a chunk boundary is nudged back to the nearest whitespace within
    /// the window so words aren't cut mid-token; if no whitespace is found the window is broken
    /// hard at <see cref="MaxChars"/>.
    /// </summary>
    public bool RespectWordBoundaries { get; set; } = true;
}

/// <summary>
/// Splits a <see cref="FileRecord"/>'s <see cref="FileRecord.Text"/> into overlapping
/// <see cref="T:Scrubkit.Chunk"/>s ready for embedding / retrieval, carrying the source path,
/// name, type, and metadata onto each chunk. Zero-dependency and offline, like the rest of Scrubkit.
///
/// Chunking is character-based with a configurable window and overlap (see
/// <see cref="ChunkOptions"/>); by default boundaries snap to whitespace so words stay intact.
/// A file whose text is shorter than the window yields a single chunk; empty text yields none.
/// </summary>
public sealed class Chunker
{
    private readonly ChunkOptions _options;

    /// <summary>Creates a chunker with default options (1 000-char windows, 100-char overlap).</summary>
    public Chunker() : this(new ChunkOptions()) { }

    /// <summary>Creates a chunker from explicit <see cref="ChunkOptions"/>.</summary>
    public Chunker(ChunkOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.MaxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxChars must be positive.");
        if (_options.OverlapChars < 0 || _options.OverlapChars >= _options.MaxChars)
            throw new ArgumentOutOfRangeException(nameof(options), "OverlapChars must be in [0, MaxChars).");
    }

    /// <summary>Chunks a single record's text. Records with empty text yield no chunks.</summary>
    public IReadOnlyList<Chunk> Chunk(FileRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        var text = record.Text ?? "";
        if (text.Length == 0) return Array.Empty<Chunk>();

        var windows = Windows(text);
        var chunks = new List<Chunk>(windows.Count);
        for (var i = 0; i < windows.Count; i++)
        {
            var (start, length) = windows[i];
            chunks.Add(new Chunk
            {
                Path = record.Path,
                Name = record.Name,
                TypeBucket = record.TypeBucket,
                Metadata = record.Metadata,
                Index = i,
                Count = windows.Count,
                StartOffset = start,
                Text = text.Substring(start, length),
            });
        }
        return chunks;
    }

    /// <summary>Chunks every record in order, flattening to one stream of chunks.</summary>
    public IEnumerable<Chunk> Chunk(IEnumerable<FileRecord> records)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        foreach (var record in records)
            foreach (var chunk in Chunk(record))
                yield return chunk;
    }

    // Compute the (start, length) window list over the text. Each window is at most MaxChars;
    // successive windows advance by (MaxChars - OverlapChars) and repeat OverlapChars of context.
    private List<(int Start, int Length)> Windows(string text)
    {
        var windows = new List<(int, int)>();
        var step = _options.MaxChars - _options.OverlapChars;   // > 0, validated in the ctor
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + _options.MaxChars, text.Length);

            // Snap the boundary back to the last whitespace in the window so words aren't cut —
            // but only within the final overlap region, so chunks never shrink below the step
            // (a single word longer than that is broken hard).
            if (_options.RespectWordBoundaries && end < text.Length)
            {
                var snap = LastWhitespace(text, start, end);
                if (snap >= start + step) end = snap;
            }

            windows.Add((start, end - start));

            if (end >= text.Length) break;

            // Advance by the step; the overlap is whatever the (possibly snapped) window left over.
            start += step;
        }

        return windows;
    }

    // Index just past the last whitespace run in [start, end); 'start' if none, so the caller
    // can detect "no snap" and keep the hard boundary.
    private static int LastWhitespace(string text, int start, int end)
    {
        for (var i = end - 1; i > start; i--)
            if (char.IsWhiteSpace(text[i]))
                return i + 1;
        return start;
    }
}
