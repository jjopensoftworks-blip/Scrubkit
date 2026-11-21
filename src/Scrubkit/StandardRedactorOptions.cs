// Copyright © 2026 jjopensoftworks-blip

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

    /// <summary>
    /// Caller-defined regex rules, matched <b>before</b> the built-in patterns (so a domain rule
    /// wins an overlap with a looser built-in). Each reports under its own category; an invalid
    /// pattern throws when the redactor is constructed. See <see cref="CustomRedactionRule"/>.
    /// </summary>
    public IList<CustomRedactionRule> CustomRules { get; } = new List<CustomRedactionRule>();

    /// <summary>
    /// When <c>true</c>, each masked value gets a <b>stable</b>, deterministic suffix derived from
    /// the value itself, so identical values collapse to the same token (e.g. every
    /// <c>jane@example.com</c> becomes <c>[EMAIL_3f9a1c8e]</c>). This de-identifies the text while
    /// keeping records <b>joinable</b> for analytics. Categories listed in <see cref="RevealLast"/>
    /// are rendered as a format-preserving mask instead and are unaffected. Default <c>false</c>.
    /// </summary>
    public bool StableTokens { get; set; }

    /// <summary>
    /// Optional secret mixed into the <see cref="StableTokens"/> hash. Set it to a per-deployment
    /// secret so tokens can't be correlated across corpora and low-entropy values (SSNs, cards)
    /// can't be recovered by hashing candidates. With no salt, tokens are stable but guessable —
    /// this is de-identification, not a cryptographic guarantee.
    /// </summary>
    public string? TokenSalt { get; set; }

    /// <summary>
    /// Per-category count of trailing alphanumeric characters to keep in the clear as a
    /// <b>format-preserving mask</b> — e.g. <c>{ ["Card"] = 4 }</c> renders
    /// <c>4111 1111 1111 1111</c> as <c>**** **** **** 1111</c>. Separators are preserved; the
    /// revealed tail keeps the value joinable. Best used on long, structured categories (card,
    /// phone, IBAN, SSN). A category here is masked in place and ignores <see cref="StableTokens"/>.
    /// </summary>
    public IDictionary<string, int> RevealLast { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Character used to mask hidden positions in a <see cref="RevealLast"/> mask. Default <c>'*'</c>.</summary>
    public char MaskChar { get; set; } = '*';

    /// <summary>
    /// Preset targeting credentials and secrets only (<see cref="RedactionCategories.PrivateKey"/>,
    /// <see cref="RedactionCategories.ApiKey"/>, <see cref="RedactionCategories.Jwt"/>,
    /// <see cref="RedactionCategories.ConnectionString"/>, <see cref="RedactionCategories.Secret"/>),
    /// leaving personal PII (emails, phones, cards, SSNs, IPs, dates) untouched. Ideal for source
    /// code and CI secret scanning.
    /// </summary>
    public static StandardRedactorOptions ForSecretsOnly()
    {
        var opts = new StandardRedactorOptions { Level = RedactionLevel.Aggressive };
        opts.DisabledCategories.Add(RedactionCategories.Email);
        opts.DisabledCategories.Add(RedactionCategories.Card);
        opts.DisabledCategories.Add(RedactionCategories.Iban);
        opts.DisabledCategories.Add(RedactionCategories.Ssn);
        opts.DisabledCategories.Add(RedactionCategories.Ip);
        opts.DisabledCategories.Add(RedactionCategories.Mac);
        opts.DisabledCategories.Add(RedactionCategories.Phone);
        opts.DisabledCategories.Add(RedactionCategories.Geo);
        opts.DisabledCategories.Add(RedactionCategories.DateOfBirth);
        opts.DisabledCategories.Add(RedactionCategories.LongNumber);
        return opts;
    }

    /// <summary>
    /// Preset tuned for payment and financial data (PCI-DSS workflows): masks payment cards with
    /// format-preserving last-4 reveal (<c>**** **** **** 1111</c>), redacts IBANs and secrets,
    /// and leaves general contact fields (emails, phones, IPs) untouched.
    /// </summary>
    public static StandardRedactorOptions ForPciDss()
    {
        var opts = new StandardRedactorOptions { Level = RedactionLevel.Standard };
        opts.RevealLast[RedactionCategories.Card] = 4;
        opts.DisabledCategories.Add(RedactionCategories.Email);
        opts.DisabledCategories.Add(RedactionCategories.Phone);
        opts.DisabledCategories.Add(RedactionCategories.Ip);
        opts.DisabledCategories.Add(RedactionCategories.Mac);
        opts.DisabledCategories.Add(RedactionCategories.Ssn);
        return opts;
    }

    /// <summary>
    /// Preset tuned for broad personal-data de-identification (GDPR workflows): runs at
    /// <see cref="RedactionLevel.Aggressive"/> with deterministic <see cref="StableTokens"/> enabled
    /// so emails, phones, IPs, dates of birth, geo coordinates, and identifiers collapse to
    /// joinable pseudonyms.
    /// </summary>
    /// <param name="tokenSalt">Optional deployment secret mixed into stable token hashes.</param>
    public static StandardRedactorOptions ForGdpr(string? tokenSalt = null) =>
        new()
        {
            Level = RedactionLevel.Aggressive,
            StableTokens = true,
            TokenSalt = tokenSalt,
        };

    /// <summary>
    /// Preset tuned for healthcare / PHI de-identification (HIPAA workflows): runs at
    /// <see cref="RedactionLevel.Aggressive"/> with deterministic <see cref="StableTokens"/> enabled
    /// and format-preserving last-4 masks on <see cref="RedactionCategories.Ssn"/> and
    /// <see cref="RedactionCategories.Phone"/>.
    /// </summary>
    /// <param name="tokenSalt">Optional deployment secret mixed into stable token hashes.</param>
    public static StandardRedactorOptions ForHipaa(string? tokenSalt = null)
    {
        var opts = new StandardRedactorOptions
        {
            Level = RedactionLevel.Aggressive,
            StableTokens = true,
            TokenSalt = tokenSalt,
        };
        opts.RevealLast[RedactionCategories.Ssn] = 4;
        opts.RevealLast[RedactionCategories.Phone] = 4;
        return opts;
    }
}
