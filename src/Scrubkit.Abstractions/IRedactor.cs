namespace Scrubkit;

/// <summary>Outcome of a redaction pass over one piece of text.</summary>
public readonly struct RedactionResult
{
    /// <summary>Text with sensitive values replaced by tokens like <c>[EMAIL]</c>.</summary>
    public string Text { get; }

    /// <summary>Count of replacements per category.</summary>
    public IReadOnlyDictionary<string, int> Counts { get; }

    public RedactionResult(string text, IReadOnlyDictionary<string, int> counts)
    {
        Text = text;
        Counts = counts;
    }
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
