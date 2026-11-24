// Copyright © 2026 jjopensoftworks-blip

namespace Scrubkit;

/// <summary>
/// Strategy used by <see cref="Chunker"/> to split text into chunks.
/// </summary>
public enum ChunkMode
{
    /// <summary>Fixed-size overlapping windows snapped to whitespace (default).</summary>
    FixedWindow = 0,

    /// <summary>Prefers splitting at paragraph breaks (<c>\n\n</c>) and sentence endings before falling back to whitespace.</summary>
    Paragraphs = 1,

    /// <summary>Splits on Markdown headings (<c>#</c>–<c>######</c>), recording the active heading breadcrumb in <c>Chunk.Metadata["Section"]</c>.</summary>
    MarkdownSections = 2,
}

/// <summary>
/// How to slice text into chunks. Defaults suit general-purpose embedding: ~1 000-character
/// windows with a 100-character overlap so context spans chunk boundaries.
/// </summary>
public sealed class ChunkOptions
{
    /// <summary>Chunking strategy. Default: <see cref="ChunkMode.FixedWindow"/>.</summary>
    public ChunkMode Mode { get; set; } = ChunkMode.FixedWindow;

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

        if (_options.Mode == ChunkMode.MarkdownSections)
            return ChunkMarkdownSections(record, text);

        var windows = Windows(text, 0, text.Length, _options.Mode == ChunkMode.Paragraphs);
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

    private IReadOnlyList<Chunk> ChunkMarkdownSections(FileRecord record, string text)
    {
        var sections = SplitMarkdownSections(text);
        var rawChunks = new List<(int Start, int Length, string? Section)>();

        foreach (var (secStart, secLen, breadcrumb) in sections)
        {
            foreach (var (wStart, wLen) in Windows(text, secStart, secStart + secLen, preferSemantic: true))
                rawChunks.Add((wStart, wLen, breadcrumb));
        }

        var chunks = new List<Chunk>(rawChunks.Count);
        for (var i = 0; i < rawChunks.Count; i++)
        {
            var (start, length, section) = rawChunks[i];
            var meta = record.Metadata;
            if (!string.IsNullOrEmpty(section))
            {
                var copy = new Dictionary<string, string>(record.Metadata.Count + 1);
                foreach (var kv in record.Metadata) copy[kv.Key] = kv.Value;
                copy["Section"] = section!;
                meta = copy;
            }

            chunks.Add(new Chunk
            {
                Path = record.Path,
                Name = record.Name,
                TypeBucket = record.TypeBucket,
                Metadata = meta,
                Index = i,
                Count = rawChunks.Count,
                StartOffset = start,
                Text = text.Substring(start, length),
            });
        }
        return chunks;
    }

    private static List<(int Start, int Length, string? Breadcrumb)> SplitMarkdownSections(string text)
    {
        var sections = new List<(int Start, int Length, string? Breadcrumb)>();
        var stack = new string?[6];
        string? currentBreadcrumb = null;
        var sectionStart = 0;
        var lineStart = 0;

        while (lineStart < text.Length)
        {
            var nl = text.IndexOf('\n', lineStart);
            var lineEnd = nl < 0 ? text.Length : nl + 1;
            var heading = ParseHeading(text, lineStart, nl < 0 ? text.Length : nl);

            if (heading is { } h)
            {
                if (lineStart > sectionStart)
                    sections.Add((sectionStart, lineStart - sectionStart, currentBreadcrumb));

                stack[h.Level - 1] = h.Title;
                for (var k = h.Level; k < stack.Length; k++) stack[k] = null;

                currentBreadcrumb = string.Join(" > ", stack.Where(s => !string.IsNullOrEmpty(s)));
                sectionStart = lineStart;
            }

            lineStart = lineEnd;
        }

        if (sectionStart < text.Length)
            sections.Add((sectionStart, text.Length - sectionStart, currentBreadcrumb));

        return sections;
    }

    private static (int Level, string Title)? ParseHeading(string text, int start, int end)
    {
        while (start < end && (text[start] == ' ' || text[start] == '\t')) start++;
        var hashes = 0;
        while (start + hashes < end && text[start + hashes] == '#' && hashes < 7) hashes++;
        if (hashes is < 1 or > 6 || start + hashes >= end || text[start + hashes] != ' ')
            return null;

        var title = text.Substring(start + hashes + 1, end - (start + hashes + 1)).Trim();
        return title.Length == 0 ? null : (hashes, title);
    }

    // Compute the (start, length) window list over [rangeStart, rangeEnd). Each window is at most
    // MaxChars; successive windows advance by (MaxChars - OverlapChars) and repeat OverlapChars.
    private List<(int Start, int Length)> Windows(string text, int rangeStart, int rangeEnd, bool preferSemantic)
    {
        var windows = new List<(int, int)>();
        var step = _options.MaxChars - _options.OverlapChars;   // > 0, validated in the ctor
        var start = rangeStart;

        while (start < rangeEnd)
        {
            var end = Math.Min(start + _options.MaxChars, rangeEnd);

            // Snap the boundary back to the best natural boundary in the window so context stays
            // intact — but only within the final overlap region, so chunks never shrink below step.
            if (end < rangeEnd)
            {
                if (preferSemantic)
                {
                    var snap = SemanticBreak(text, start + step, end);
                    if (snap >= start + step) end = snap;
                }
                else if (_options.RespectWordBoundaries)
                {
                    var snap = LastWhitespace(text, start, end);
                    if (snap >= start + step) end = snap;
                }
            }

            windows.Add((start, end - start));

            if (end >= rangeEnd) break;

            // Advance by the step; the overlap is whatever the (possibly snapped) window left over.
            start += step;
        }

        return windows;
    }

    private static int SemanticBreak(string text, int minEnd, int maxEnd)
    {
        // 1. Prefer paragraph breaks (\n\n or \r\n\r\n).
        for (var i = maxEnd - 1; i >= minEnd; i--)
        {
            if (text[i] == '\n')
            {
                if (i > 0 && text[i - 1] == '\n') return i + 1;
                if (i >= 2 && text[i - 1] == '\r' && text[i - 2] == '\n') return i + 1;
            }
        }

        // 2. Next prefer sentence endings (., !, ? followed by whitespace).
        for (var i = maxEnd - 1; i > minEnd; i--)
        {
            if (char.IsWhiteSpace(text[i]) && text[i - 1] is '.' or '!' or '?')
                return i + 1;
        }

        // 3. Fall back to whitespace.
        return LastWhitespace(text, minEnd - 1, maxEnd);
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
