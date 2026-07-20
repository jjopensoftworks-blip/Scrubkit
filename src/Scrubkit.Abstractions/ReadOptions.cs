namespace Scrubkit;

/// <summary>How deep to walk from the root path.</summary>
public enum Recursion
{
    /// <summary>Only files directly in the given folder.</summary>
    TopOnly,
    /// <summary>The folder and every nested subfolder, to any depth.</summary>
    AllNested,
}

/// <summary>How aggressively to strip sensitive-looking values from extracted text.</summary>
public enum RedactionLevel
{
    /// <summary>Return raw text untouched.</summary>
    Off,
    /// <summary>Strip high-confidence patterns (emails, phones, card/SSN-like, IPs).</summary>
    Standard,
    /// <summary>Standard plus lower-confidence patterns (long digit runs, DOB-like dates).</summary>
    Aggressive,
}

/// <summary>
/// Knobs for a single extraction run. Sensible defaults: recurse everything, cap the
/// batch, and clip large files. Extracted text is returned exactly as read — redaction
/// is entirely the caller's choice: supply a <see cref="Redactor"/> (or set a
/// <see cref="Redaction"/> level) to opt in.
/// </summary>
public sealed class ReadOptions
{
    /// <summary>Walk the whole tree, or just the top folder. Default: <see cref="Recursion.AllNested"/>.</summary>
    public Recursion Recursion { get; set; } = Recursion.AllNested;

    /// <summary>Stop after this many files (0 = no limit). Default: 1000.</summary>
    public int MaxFiles { get; set; } = 1000;

    /// <summary>
    /// Skip files larger than this before opening them — checked against the file's size via
    /// a cheap stat, so oversized files are never read (they get a <c>skipped-content</c>
    /// warning). Default: 10 MB, which comfortably covers everyday documents while keeping the
    /// run fast. <c>0</c> = no limit — use with care: a multi-GB file will then be read in
    /// full (and, with <see cref="ComputeContentHash"/>, hashed in full).
    /// </summary>
    public long MaxBytesPerFile { get; set; } = 10 * 1024 * 1024;

    /// <summary>Clip extracted text to this many characters (0 = no clip). Default: 20 000.</summary>
    public int MaxTextLength { get; set; } = 20_000;

    /// <summary>
    /// How many files to process concurrently. Default: 1 (sequential). Raising it bounds
    /// parallel extraction to this many files at once while preserving result order. When
    /// &gt; 1, your <see cref="Extractors"/> and <see cref="Redactor"/> must be thread-safe
    /// (the built-ins are). Values &lt; 1 are treated as 1.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// If non-empty, only these extensions are opened (e.g. <c>".pdf"</c>). Case-insensitive,
    /// leading dot optional. Empty = every extension a registered extractor can handle.
    /// </summary>
    public ISet<string> IncludeExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Extra format extractors (your own or from an add-on package). Tried BEFORE the
    /// built-ins, so an add-on can also override a built-in extractor.
    /// </summary>
    public IList<IFileExtractor> Extractors { get; } = new List<IFileExtractor>();

    /// <summary>
    /// Convenience level for the built-in redactor, applied only when <see cref="Redactor"/>
    /// is null and this is not <see cref="RedactionLevel.Off"/>. Default: <c>Off</c> — the
    /// core extracts and does not redact unless you ask it to.
    /// </summary>
    public RedactionLevel Redaction { get; set; } = RedactionLevel.Off;

    /// <summary>
    /// The redactor applied to extracted text and metadata — this is how you opt into
    /// redaction. When set, it is used as-is (and takes precedence over
    /// <see cref="Redaction"/>). When null and <see cref="Redaction"/> is <c>Off</c>,
    /// nothing is redacted. Plug in <c>StandardRedactor</c> or your own <see cref="IRedactor"/>.
    /// </summary>
    public IRedactor? Redactor { get; set; }

    /// <summary>
    /// When true, compute a SHA-256 hash of each file's bytes and expose it on
    /// <see cref="FileRecord.ContentHash"/> — a stable dedup key for indexes. Off by default.
    /// It reads the whole file, so it is <b>bounded by <see cref="MaxBytesPerFile"/></b>:
    /// files over that limit are skipped (not hashed), which keeps very large files from
    /// hanging the run. Don't enable this together with <see cref="MaxBytesPerFile"/> = 0 on
    /// multi-GB files.
    /// </summary>
    public bool ComputeContentHash { get; set; }

    /// <summary>
    /// Optional per-file diagnostics sink, invoked as each file is processed — successful
    /// reads and problems alike (see <see cref="ScrubDiagnostic.IsWarning"/>). Dependency-free:
    /// bridge it to <c>ILogger</c> via the <c>Scrubkit.Extensions.DependencyInjection</c>
    /// package, or handle it yourself. Must be thread-safe when
    /// <see cref="MaxDegreeOfParallelism"/> &gt; 1.
    /// </summary>
    public Action<ScrubDiagnostic>? OnDiagnostic { get; set; }
}
