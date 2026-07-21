namespace Scrubkit;

/// <summary>
/// Where a single redaction happened, as an offset/length into the <em>original</em>
/// (pre-redaction) text — so callers can highlight or audit exactly what was removed.
/// </summary>
public readonly struct RedactionSpan
{
    /// <summary>Start offset of the matched value in the original text.</summary>
    public int Start { get; }

    /// <summary>Length of the matched value in the original text.</summary>
    public int Length { get; }

    /// <summary>Category of the match (see <see cref="RedactionCategories"/>), e.g. <c>"Email"</c>.</summary>
    public string Category { get; }

    public RedactionSpan(int start, int length, string category)
    {
        Start = start;
        Length = length;
        Category = category;
    }
}

/// <summary>Outcome of a redaction pass over one piece of text.</summary>
public readonly struct RedactionResult
{
    /// <summary>Text with sensitive values replaced by tokens like <c>[EMAIL]</c>.</summary>
    public string Text { get; }

    /// <summary>Count of replacements per category.</summary>
    public IReadOnlyDictionary<string, int> Counts { get; }

    /// <summary>
    /// Each redaction's location in the original text, in reading order. Empty when the
    /// redactor doesn't report spans.
    /// </summary>
    public IReadOnlyList<RedactionSpan> Spans { get; }

    /// <summary>Creates a result without span detail.</summary>
    public RedactionResult(string text, IReadOnlyDictionary<string, int> counts)
        : this(text, counts, Array.Empty<RedactionSpan>()) { }

    /// <summary>Creates a result carrying per-match <see cref="Spans"/>.</summary>
    public RedactionResult(string text, IReadOnlyDictionary<string, int> counts, IReadOnlyList<RedactionSpan> spans)
    {
        Text = text;
        Counts = counts;
        Spans = spans;
    }
}

/// <summary>
/// Canonical category names used by the built-in redactor and reported on
/// <see cref="RedactionSpan.Category"/> and in <see cref="RedactionResult.Counts"/>.
/// Use these when configuring per-category behaviour (disable, custom tokens).
/// </summary>
public static class RedactionCategories
{
    public const string Email = "Email";
    public const string Card = "Card";
    public const string Iban = "IBAN";
    public const string Ssn = "SSN";
    public const string Ip = "IP";
    public const string Mac = "MAC";
    public const string Phone = "Phone";
    public const string Geo = "Geo";
    public const string DateOfBirth = "DateOfBirth";
    public const string LongNumber = "LongNumber";
    /// <summary>Reported for caller-supplied deny-list terms.</summary>
    public const string Custom = "Custom";
}

/// <summary>
/// Strips sensitive values from text. A small, swappable seam: the built-in
/// <c>StandardRedactor</c> is best-effort pattern matching; callers who need
/// stronger redaction plug in their own.
///
/// This is best-effort, not a guarantee. It reduces incidental exposure of common
/// sensitive values, but it will miss things.
/// </summary>
public interface IRedactor
{
    RedactionResult Redact(string text);
}
