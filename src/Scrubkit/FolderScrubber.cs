using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Scrubkit;

/// <summary>
/// The public entry point. Point it at a folder and get back a table of
/// <see cref="FileRecord"/> — text + metadata per file. Fully offline and non-throwing
/// per file (problems surface as <see cref="FileRecord.Warnings"/>). Redaction is opt-in:
/// supply an <see cref="IRedactor"/> via <see cref="ReadOptions.Redactor"/>.
///
/// The fast built-in extractors cover PDF, Office (docx/pptx/xlsx), plain text and
/// image metadata. Register your own <see cref="IFileExtractor"/> implementations via
/// <see cref="ReadOptions.Extractors"/>.
/// </summary>
public sealed class FolderScrubber
{
    private readonly ReadOptions _options;
    private readonly IRedactor? _redactor;
    private readonly List<IFileExtractor> _extractors;

    public FolderScrubber(ReadOptions? options = null)
    {
        _options = options ?? new ReadOptions();

        // Redaction is the caller's choice: an explicit Redactor wins; otherwise the
        // built-in is used only when a level was requested. With neither, _redactor stays
        // null and extracted text is returned exactly as read.
        _redactor = _options.Redactor
            ?? (_options.Redaction != RedactionLevel.Off ? new StandardRedactor(_options.Redaction) : null);

        // Registered add-ons first (can override), then the built-ins.
        _extractors = new List<IFileExtractor>(_options.Extractors)
        {
            new PdfExtractor(),
            new OfficeExtractor(),
            new PlainTextExtractor(),
            new ImageExtractor(),
        };
    }

    /// <summary>
    /// Scrub every eligible file under <paramref name="rootPath"/> and return the whole
    /// table. Buffers all records in memory — for large trees prefer
    /// <see cref="ReadStreamAsync"/>.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The root folder does not exist.</exception>
    public async Task<IReadOnlyList<FileRecord>> ReadAsync(string rootPath, CancellationToken ct = default)
    {
        var results = new List<FileRecord>();
        await foreach (var record in ReadStreamAsync(rootPath, ct).ConfigureAwait(false))
            results.Add(record);
        return results;
    }

    /// <summary>
    /// Scrub every eligible file under <paramref name="rootPath"/>, yielding each
    /// <see cref="FileRecord"/> as it is produced — so callers can process huge trees
    /// without buffering the whole table. Records are yielded in enumeration order even
    /// when <see cref="ReadOptions.MaxDegreeOfParallelism"/> &gt; 1.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The root folder does not exist.</exception>
    public async IAsyncEnumerable<FileRecord> ReadStreamAsync(
        string rootPath, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Folder not found: {rootPath}");

        int dop = Math.Max(1, _options.MaxDegreeOfParallelism);
        int max = _options.MaxFiles;
        int started = 0;

        // A sliding FIFO window of in-flight tasks: bounds concurrency to `dop` while
        // preserving order (we dequeue — and yield — in the order work was started).
        var window = new Queue<Task<FileRecord>>(dop);

        using var paths = Enumerate(rootPath).GetEnumerator();
        bool hasMore = paths.MoveNext();

        while (true)
        {
            while (hasMore && window.Count < dop && (max <= 0 || started < max))
            {
                ct.ThrowIfCancellationRequested();
                var path = paths.Current;
                window.Enqueue(Task.Run(() => ReadOne(path), ct));
                started++;
                hasMore = paths.MoveNext();
            }

            if (window.Count == 0)
                yield break;

            yield return await window.Dequeue().ConfigureAwait(false);
        }
    }

    private IEnumerable<string> Enumerate(string root)
    {
        var opt = _options.Recursion == Recursion.AllNested
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(root, "*", opt); }
        catch { yield break; }

        foreach (var f in files)
        {
            var ext = Path.GetExtension(f);
            if (_options.IncludeExtensions.Count > 0 &&
                !_options.IncludeExtensions.Contains(ext.TrimStart('.')) &&
                !_options.IncludeExtensions.Contains(ext))
                continue;
            yield return f;
        }
    }

    private IFileExtractor? ExtractorFor(string ext) =>
        _extractors.FirstOrDefault(e => e.CanHandle(ext));

    private FileRecord ReadOne(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileName(path);
        var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
        long size = 0;
        DateTime modified = DateTime.MinValue;
        var warnings = new List<string>();

        try
        {
            var info = new FileInfo(path);
            size = info.Length;
            modified = info.LastWriteTime;
        }
        catch { warnings.Add("stat-failed"); }

        var meta = new Dictionary<string, string>();
        var text = "";
        var redactions = new Dictionary<string, int>();

        var extractor = ExtractorFor(ext);
        bool tooBig = _options.MaxBytesPerFile > 0 && size > _options.MaxBytesPerFile;

        if (extractor is null)
        {
            // Unknown type: metadata-only row, no content.
        }
        else if (tooBig)
        {
            warnings.Add($"skipped-content: {size} bytes over limit");
        }
        else
        {
            ExtractedContent content;
            try { content = extractor.Extract(path); }
            catch (Exception e) { content = ExtractedContent.Empty; warnings.Add($"extract-failed: {e.GetType().Name}"); }

            meta = content.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value);
            var t = Normalize(content.Text);

            if (_options.MaxTextLength > 0 && t.Length > _options.MaxTextLength)
            {
                t = t[.._options.MaxTextLength];
                warnings.Add("text-clipped");
            }

            text = t;

            // Redaction runs only when the caller opted in (see the constructor).
            if (_redactor is not null)
            {
                var red = _redactor.Redact(t);
                text = red.Text;
                redactions = red.Counts.ToDictionary(kv => kv.Key, kv => kv.Value);

                // Metadata can carry the same values (authors, subjects) — redact it too.
                foreach (var key in meta.Keys.ToList())
                {
                    var mr = _redactor.Redact(meta[key]);
                    meta[key] = mr.Text;
                    foreach (var kv in mr.Counts)
                        redactions[kv.Key] = redactions.GetValueOrDefault(kv.Key) + kv.Value;
                }
            }
        }

        return new FileRecord
        {
            Path = path,
            Name = name,
            Extension = ext,
            Folder = folder,
            SizeBytes = size,
            Modified = modified,
            TypeBucket = Buckets.For(ext),
            Metadata = meta,
            Text = text,
            Redactions = redactions,
            Warnings = warnings,
        };
    }

    private static string Normalize(string s) =>
        string.IsNullOrWhiteSpace(s) ? "" : Regex.Replace(s, @"\s+", " ").Trim();
}
