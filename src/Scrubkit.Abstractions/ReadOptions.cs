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
/// batch, and clip large files. Extracted text is returned as-is — redaction is off by
/// default (opt in via <see cref="Redaction"/> or a custom <see cref="Redactor"/>).
/// </summary>
public sealed class ReadOptions
{
    /// <summary>Walk the whole tree, or just the top folder. Default: <see cref="Recursion.AllNested"/>.</summary>
    public Recursion Recursion { get; set; } = Recursion.AllNested;

    /// <summary>Stop after this many files (0 = no limit). Default: 1000.</summary>
    public int MaxFiles { get; set; } = 1000;

    /// <summary>Skip files larger than this before opening them (0 = no limit). Default: 25 MB.</summary>
    public long MaxBytesPerFile { get; set; } = 25 * 1024 * 1024;

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

    /// <summary>Built-in redaction level, used when <see cref="Redactor"/> is null. Default: Off (text returned as-is).</summary>
    public RedactionLevel Redaction { get; set; } = RedactionLevel.Off;

    /// <summary>
    /// Custom redactor. When set, it fully replaces the built-in one. When null, a
    /// <c>StandardRedactor</c> honoring <see cref="Redaction"/> is used.
    /// </summary>
    public IRedactor? Redactor { get; set; }
}
