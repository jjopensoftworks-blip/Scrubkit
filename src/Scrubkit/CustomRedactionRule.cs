namespace Scrubkit;

/// <summary>
/// A caller-defined redaction pattern for <see cref="StandardRedactor"/> — a regex plus the
/// category it reports under and the token that replaces a match. Lets you scrub domain-specific
/// values (employee IDs, case numbers, internal codenames) without writing code. Add them via
/// <see cref="StandardRedactorOptions.CustomRules"/>.
///
/// Custom rules are matched <em>before</em> the built-in patterns, so a domain rule claims its
/// text ahead of a looser built-in (e.g. an employee-ID rule beats the Aggressive long-number
/// pattern). Patterns are compiled with a match timeout, so an accidental catastrophic regex
/// can't hang a run.
/// </summary>
public sealed class CustomRedactionRule
{
    /// <summary>Category reported on the span and in the counts (e.g. <c>"EmployeeId"</c>).</summary>
    public required string Category { get; init; }

    /// <summary>The .NET regular expression to match.</summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// Replacement token. Defaults to <c>[CATEGORY]</c> (the category upper-cased in brackets)
    /// when null or empty.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>Match case-insensitively. Default: <c>false</c>.</summary>
    public bool IgnoreCase { get; init; }
}
