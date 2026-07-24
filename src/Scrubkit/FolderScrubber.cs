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
            new HtmlExtractor(),
            new RtfExtractor(),
            new PlainTextExtractor(),
            new ImageExtractor(),
        };

        // Normalize the exclusion set to full paths once, so a run never ingests its own output.
        _excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in _options.ExcludePaths)
        {
            try { _excluded.Add(Path.GetFullPath(p)); } catch { /* ignore an unparseable path */ }
        }
    }

    private readonly HashSet<string> _excluded;

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

    /// <summary>
    /// Scrub only the files that changed since <paramref name="baseline"/>. A file is
    /// <em>unchanged</em> when its path is in the baseline with the same size and last-write
    /// time; unchanged files are skipped (never re-extracted) and carried into the result
    /// manifest as-is. Added / modified files are extracted and returned in
    /// <see cref="IncrementalResult.Changed"/>; paths gone from disk are in
    /// <see cref="IncrementalResult.Removed"/>. Persist <see cref="IncrementalResult.Manifest"/>
    /// and pass it back as the baseline next time.
    ///
    /// Change detection is size + mtime only (no re-read), so a file that was touched but not
    /// edited counts as modified. Pass <see cref="Manifest.Empty"/> to treat every file as new.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The root folder does not exist.</exception>
    public Task<IncrementalResult> ReadChangesAsync(
        string rootPath, Manifest baseline, CancellationToken ct = default)
    {
        if (baseline is null) throw new ArgumentNullException(nameof(baseline));
        return Task.Run(() => ReadChanges(rootPath, baseline, ct), ct);
    }

    private IncrementalResult ReadChanges(string rootPath, Manifest baseline, CancellationToken ct)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Folder not found: {rootPath}");

        var changed = new List<FileRecord>();
        var entries = new List<ManifestEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Enumerate(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            seen.Add(path);

            long size = -1;
            DateTime modified = DateTime.MinValue;
            try { var info = new FileInfo(path); size = info.Length; modified = info.LastWriteTimeUtc; }
            catch { /* stat failed → treat as changed so it gets reprocessed */ }

            if (size >= 0 && baseline.TryGet(path, out var prev) &&
                prev.SizeBytes == size && prev.Modified == modified)
            {
                entries.Add(prev);   // unchanged: carry forward, no extraction
                _options.OnDiagnostic?.Invoke(new ScrubDiagnostic(path, "unchanged", "unchanged", isWarning: false));
                continue;
            }

            var record = ReadOne(path);
            changed.Add(record);
            entries.Add(ManifestEntry.From(record));
        }

        var removed = baseline.Entries
            .Select(e => e.Path)
            .Where(p => !seen.Contains(p))
            .ToList();

        return new IncrementalResult
        {
            Changed = changed,
            Removed = removed,
            Manifest = new Manifest(entries),
        };
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
            if (_excluded.Count > 0)
            {
                string full;
                try { full = Path.GetFullPath(f); } catch { full = f; }
                if (_excluded.Contains(full)) continue;
            }
            yield return f;
        }
    }

    private IFileExtractor? ExtractorFor(string ext) =>
        _extractors.FirstOrDefault(e => e.CanHandle(ext));

    private FileRecord ReadOne(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var bucket = Buckets.For(ext);
        var name = Path.GetFileName(path);
        var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
        long size = 0;
        DateTime modified = DateTime.MinValue;
        var warnings = new List<string>();

        try
        {
            var info = new FileInfo(path);
            size = info.Length;
            modified = info.LastWriteTimeUtc;
        }
        catch { warnings.Add("stat-failed"); }

        var meta = new Dictionary<string, string>();
        var text = "";
        var redactions = new Dictionary<string, int>();

        var extractor = ExtractorFor(ext);
        bool tooBig = _options.MaxBytesPerFile > 0 && size > _options.MaxBytesPerFile;

        string? contentHash = null;
        if (_options.ComputeContentHash && !tooBig)
        {
            try { contentHash = Sha256Hex(path); }
            catch { warnings.Add("hash-failed"); }
        }

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

        Emit(path, bucket, text, warnings);

        return new FileRecord
        {
            Path = path,
            Name = name,
            Extension = ext,
            Folder = folder,
            SizeBytes = size,
            Modified = modified,
            TypeBucket = bucket,
            Metadata = meta,
            Text = text,
            Redactions = redactions,
            Warnings = warnings,
            ContentHash = contentHash,
        };
    }

    // Fire the optional per-file diagnostics: one per warning, then a "read" event — but only
    // when the file was actually read (not skipped, extract-failed, or stat-failed).
    private void Emit(string path, string bucket, string text, List<string> warnings)
    {
        var onDiagnostic = _options.OnDiagnostic;
        if (onDiagnostic is null) return;

        var read = true;
        foreach (var w in warnings)
        {
            var colon = w.IndexOf(':');
            var evt = colon >= 0 ? w.Substring(0, colon) : w;
            onDiagnostic(new ScrubDiagnostic(path, evt, w, isWarning: true));
            if (evt is "skipped-content" or "extract-failed" or "stat-failed") read = false;
        }

        if (read)
            onDiagnostic(new ScrubDiagnostic(path, "read", $"{bucket}, {text.Length} chars", isWarning: false));
    }

    private static string Sha256Hex(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(path);
        var bytes = sha.ComputeHash(stream);
        var sb = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static string Normalize(string s) =>
        string.IsNullOrWhiteSpace(s) ? "" : Regex.Replace(s, @"\s+", " ").Trim();
}
