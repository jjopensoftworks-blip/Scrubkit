namespace Scrubkit;

/// <summary>
/// Fine-grained configuration for <see cref="StandardRedactor"/>. Defaults match the simple
/// <c>new StandardRedactor(level)</c> behaviour; set these to tune which categories fire,
/// how they're tokenised, and which literal terms to always keep or always remove.
/// </summary>
public sealed class StandardRedactorOptions
{
    /// <summary>How aggressively to match. Default: <see cref="RedactionLevel.Standard"/>.</summary>
    public RedactionLevel Level { get; set; } = RedactionLevel.Standard;

    /// <summary>
    /// Categories (see <see cref="RedactionCategories"/>) to leave untouched — e.g. add
    /// <see cref="RedactionCategories.Phone"/> to keep phone numbers.
    /// </summary>
    public ISet<string> DisabledCategories { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per-category replacement token overrides. Key is a category name; value is the token
    /// to emit (default is <c>[EMAIL]</c>, <c>[PHONE]</c>, …).
    /// </summary>
    public IDictionary<string, string> Tokens { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Exact values that must never be redacted, case-insensitive (e.g. a public support
    /// address). A match whose text is in this set is left as-is.
    /// </summary>
    public ISet<string> AllowList { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Literal terms that are always redacted, case-insensitive (e.g. an internal codename).
    /// Matches are reported under <see cref="RedactionCategories.Custom"/> and win over the
    /// pattern categories.
    /// </summary>
    public IList<string> DenyTerms { get; } = new List<string>();

    /// <summary>Token used for <see cref="DenyTerms"/> matches. Default <c>[REDACTED]</c>.</summary>
    public string DenyToken { get; set; } = "[REDACTED]";
}
