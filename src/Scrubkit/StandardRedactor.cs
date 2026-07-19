using System.Text;
using System.Text.RegularExpressions;

namespace Scrubkit;

/// <summary>
/// Best-effort pattern-based redactor. Replaces high-confidence sensitive values with
/// category tokens (<c>[EMAIL]</c>, <c>[PHONE]</c>, …). At
/// <see cref="RedactionLevel.Aggressive"/> it also removes lower-confidence patterns that
/// trade precision for recall. Card numbers are Luhn-checked to cut false hits.
///
/// Matching is a single pass: patterns are tried in priority order and the more specific
/// ones claim their text first, so nothing is double-counted. The result carries per-match
/// <see cref="RedactionResult.Spans"/> (offsets into the original text) alongside the counts.
/// Configure per-category behaviour, custom tokens, and allow/deny lists via
/// <see cref="StandardRedactorOptions"/>.
/// </summary>
public sealed class StandardRedactor : IRedactor
{
    private readonly StandardRedactorOptions _options;

    /// <summary>Creates a redactor at the given level with default behaviour.</summary>
    public StandardRedactor(RedactionLevel level = RedactionLevel.Standard)
        : this(new StandardRedactorOptions { Level = level }) { }

    /// <summary>Creates a redactor from fine-grained <see cref="StandardRedactorOptions"/>.</summary>
    public StandardRedactor(StandardRedactorOptions options) =>
        _options = options ?? throw new ArgumentNullException(nameof(options));

    // ---- patterns (declared before the rule table that references them) -----

    private static readonly Regex Email =
        new(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

    private static readonly Regex Ssn =
        new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);

    // 13–16 digit runs, optionally separated by spaces/dashes — Luhn-validated below.
    private static readonly Regex CardLike =
        new(@"\b(?:\d[ -]?){13,16}\b", RegexOptions.Compiled);

    // Country (2 letters) + check digits (2) + up to 30 alphanumerics, optionally spaced.
    private static readonly Regex Iban =
        new(@"\b[A-Z]{2}\d{2}(?:\s?[A-Z0-9]){11,30}\b", RegexOptions.Compiled);

    private static readonly Regex Mac =
        new(@"\b(?:[A-Fa-f0-9]{2}[:\-]){5}[A-Fa-f0-9]{2}\b", RegexOptions.Compiled);

    private static readonly Regex Ipv4 =
        new(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b", RegexOptions.Compiled);

    // Full and compressed IPv6 forms (Stephen Ryan's well-known pattern), bounded so it
    // doesn't nibble into surrounding words or MAC addresses.
    private static readonly Regex Ipv6 = new(
        @"(?<![\w:])(?:" +
        @"(?:[A-Fa-f0-9]{1,4}:){7}[A-Fa-f0-9]{1,4}" +
        @"|(?:[A-Fa-f0-9]{1,4}:){1,7}:" +
        @"|(?:[A-Fa-f0-9]{1,4}:){1,6}:[A-Fa-f0-9]{1,4}" +
        @"|(?:[A-Fa-f0-9]{1,4}:){1,5}(?::[A-Fa-f0-9]{1,4}){1,2}" +
        @"|(?:[A-Fa-f0-9]{1,4}:){1,4}(?::[A-Fa-f0-9]{1,4}){1,3}" +
        @"|(?:[A-Fa-f0-9]{1,4}:){1,3}(?::[A-Fa-f0-9]{1,4}){1,4}" +
        @"|(?:[A-Fa-f0-9]{1,4}:){1,2}(?::[A-Fa-f0-9]{1,4}){1,5}" +
        @"|[A-Fa-f0-9]{1,4}:(?::[A-Fa-f0-9]{1,4}){1,6}" +
        @"|:(?::[A-Fa-f0-9]{1,4}){1,7}" +
        @")(?![\w:])",
        RegexOptions.Compiled);

    private static readonly Regex Phone =
        new(@"(?<!\d)(?:\+?\d{1,3}[ .\-]?)?(?:\(\d{2,4}\)[ .\-]?)?\d{3,4}[ .\-]\d{3,4}(?:[ .\-]\d{2,4})?(?!\d)",
            RegexOptions.Compiled);

    // Aggressive-only:
    private static readonly Regex Geo =
        new(@"[-+]?\d{1,3}\.\d{3,}\s*,\s*[-+]?\d{1,3}\.\d{3,}", RegexOptions.Compiled);

    private static readonly Regex DobLike =
        new(@"\b(?:\d{1,2}[/\-.]){2}(?:19|20)\d{2}\b", RegexOptions.Compiled);

    private static readonly Regex LongDigitRun =
        new(@"\b\d{7,}\b", RegexOptions.Compiled);

    // ---- rule table (priority order: specific patterns claim their text first) ----

    private sealed class MatchRule
    {
        public Regex Regex { get; init; } = null!;
        public string Category { get; init; } = "";
        public string Token { get; init; } = "";
        public bool Aggressive { get; init; }
        public Func<Match, bool>? Validate { get; init; }
    }

    private static readonly MatchRule[] Rules =
    {
        new() { Regex = Email,        Category = RedactionCategories.Email,       Token = "[EMAIL]" },
        new() { Regex = CardLike,     Category = RedactionCategories.Card,        Token = "[CARD]", Validate = IsLuhnCard },
        new() { Regex = Iban,         Category = RedactionCategories.Iban,        Token = "[IBAN]" },
        new() { Regex = Ssn,          Category = RedactionCategories.Ssn,         Token = "[SSN]" },
        new() { Regex = Mac,          Category = RedactionCategories.Mac,         Token = "[MAC]" },
        new() { Regex = Ipv4,         Category = RedactionCategories.Ip,          Token = "[IP]" },
        new() { Regex = Ipv6,         Category = RedactionCategories.Ip,          Token = "[IP]" },
        new() { Regex = Phone,        Category = RedactionCategories.Phone,       Token = "[PHONE]" },
        new() { Regex = Geo,          Category = RedactionCategories.Geo,         Token = "[GEO]",    Aggressive = true },
        new() { Regex = DobLike,      Category = RedactionCategories.DateOfBirth, Token = "[DATE]",   Aggressive = true },
        new() { Regex = LongDigitRun, Category = RedactionCategories.LongNumber,  Token = "[NUMBER]", Aggressive = true },
    };

    private static readonly IReadOnlyDictionary<string, string> DefaultTokens = BuildDefaultTokens();
    private static readonly IReadOnlyDictionary<string, int> EmptyCounts = new Dictionary<string, int>();

    public RedactionResult Redact(string text)
    {
        if (string.IsNullOrEmpty(text) || _options.Level == RedactionLevel.Off)
            return new RedactionResult(text, EmptyCounts);

        var claimed = new bool[text.Length];
        var spans = new List<RedactionSpan>();

        // A working copy where claimed characters are masked out, so a later, looser pattern
        // can't reach back into text a more specific one already took (e.g. a phone pattern
        // grabbing the trailing octet of a claimed IP). Masking preserves length, so match
        // offsets still map 1:1 onto the original text.
        var work = text.ToCharArray();

        // Deny-list terms win over the pattern categories.
        foreach (var term in _options.DenyTerms)
        {
            if (string.IsNullOrEmpty(term)) continue;
            for (var i = 0; (i = text.IndexOf(term, i, StringComparison.OrdinalIgnoreCase)) >= 0; i += term.Length)
                if (TryClaim(i, term.Length, RedactionCategories.Custom, claimed, spans))
                    Mask(work, i, term.Length);
        }

        foreach (var rule in Rules)
        {
            if (rule.Aggressive && _options.Level != RedactionLevel.Aggressive) continue;
            if (_options.DisabledCategories.Contains(rule.Category)) continue;

            foreach (Match m in rule.Regex.Matches(new string(work)))
            {
                if (rule.Validate is not null && !rule.Validate(m)) continue;
                if (_options.AllowList.Contains(m.Value)) continue;
                if (TryClaim(m.Index, m.Length, rule.Category, claimed, spans))
                    Mask(work, m.Index, m.Length);
            }
        }

        if (spans.Count == 0)
            return new RedactionResult(text, EmptyCounts);

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));

        var sb = new StringBuilder(text.Length);
        var counts = new Dictionary<string, int>();
        var pos = 0;
        foreach (var span in spans)
        {
            sb.Append(text, pos, span.Start - pos);
            sb.Append(TokenFor(span.Category));
            counts[span.Category] = counts.GetValueOrDefault(span.Category) + 1;
            pos = span.Start + span.Length;
        }
        sb.Append(text, pos, text.Length - pos);

        return new RedactionResult(sb.ToString(), counts, spans);
    }

    private const char MaskChar = '￿';   // non-word, non-digit — matches no pattern

    // Claim [start, start+length) unless it overlaps an already-claimed (higher-priority)
    // range. Returns true when the claim was taken.
    private static bool TryClaim(int start, int length, string category, bool[] claimed, List<RedactionSpan> spans)
    {
        if (length <= 0 || start < 0 || start + length > claimed.Length) return false;
        for (var i = start; i < start + length; i++)
            if (claimed[i]) return false;
        for (var i = start; i < start + length; i++)
            claimed[i] = true;
        spans.Add(new RedactionSpan(start, length, category));
        return true;
    }

    private static void Mask(char[] work, int start, int length)
    {
        for (var i = start; i < start + length; i++) work[i] = MaskChar;
    }

    private string TokenFor(string category)
    {
        if (_options.Tokens.TryGetValue(category, out var custom)) return custom;
        if (category == RedactionCategories.Custom) return _options.DenyToken;
        return DefaultTokens[category];
    }

    private static Dictionary<string, string> BuildDefaultTokens()
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in Rules) tokens[rule.Category] = rule.Token;   // IP appears twice, same token
        return tokens;
    }

    private static bool IsLuhnCard(Match m)
    {
        var digits = m.Value.Where(char.IsDigit).ToArray();
        return digits.Length is >= 13 and <= 16 && LuhnValid(digits);
    }

    private static bool LuhnValid(char[] digits)
    {
        int sum = 0;
        bool alt = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int d = digits[i] - '0';
            if (alt) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            alt = !alt;
        }
        return sum % 10 == 0;
    }
}
