using System.Text;
using System.Text.RegularExpressions;

namespace Scrubkit;

/// <summary>
/// Best-effort pattern-based redactor. Replaces high-confidence sensitive values with
/// category tokens (<c>[EMAIL]</c>, <c>[PHONE]</c>, …), including secrets in recognisable
/// formats — PEM private keys, JWTs, AWS / Google / GitHub / Slack credentials, and
/// credentialed connection strings. At <see cref="RedactionLevel.Aggressive"/> it also removes
/// lower-confidence patterns that trade precision for recall, including <c>key = value</c>
/// credential assignments and high-entropy tokens. Card numbers are Luhn-checked to cut false hits.
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
    private readonly MatchRule[] _customRules;
    private readonly IReadOnlyDictionary<string, string> _customTokens;

    /// <summary>Creates a redactor at the given level with default behaviour.</summary>
    public StandardRedactor(RedactionLevel level = RedactionLevel.Standard)
        : this(new StandardRedactorOptions { Level = level }) { }

    /// <summary>Creates a redactor from fine-grained <see cref="StandardRedactorOptions"/>.</summary>
    /// <exception cref="ArgumentException">A <see cref="CustomRedactionRule"/> has an empty category/pattern or an invalid regex.</exception>
    public StandardRedactor(StandardRedactorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        (_customRules, _customTokens) = BuildCustomRules(_options.CustomRules);
    }

    // Compile the caller's custom rules once, failing fast on a bad category/pattern/regex. Each
    // pattern gets a match timeout so an accidental catastrophic regex can't hang a run.
    private static (MatchRule[] Rules, IReadOnlyDictionary<string, string> Tokens) BuildCustomRules(
        IEnumerable<CustomRedactionRule> rules)
    {
        var compiled = new List<MatchRule>();
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in rules)
        {
            if (r is null) continue;
            if (string.IsNullOrEmpty(r.Category))
                throw new ArgumentException("A custom redaction rule has an empty Category.");
            if (string.IsNullOrEmpty(r.Pattern))
                throw new ArgumentException($"Custom rule '{r.Category}' has an empty Pattern.");

            Regex regex;
            try
            {
                var opts = RegexOptions.CultureInvariant | (r.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                regex = new Regex(r.Pattern, opts, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Custom rule '{r.Category}' has an invalid regex: {ex.Message}", ex);
            }

            var token = string.IsNullOrEmpty(r.Token) ? $"[{r.Category.ToUpperInvariant()}]" : r.Token!;
            compiled.Add(new MatchRule { Regex = regex, Category = r.Category, Token = token });
            tokens[r.Category] = token;
        }
        return (compiled.ToArray(), tokens);
    }

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

    // ---- secrets / credentials -----

    // A whole PEM private-key block, header to footer, across lines.
    private static readonly Regex PrivateKey = new(
        @"-----BEGIN (?:[A-Z0-9 ]+ )?PRIVATE KEY-----[\s\S]*?-----END (?:[A-Z0-9 ]+ )?PRIVATE KEY-----",
        RegexOptions.Compiled);

    // header.payload.signature, each base64url; the header always starts "eyJ" ('{"' in base64).
    private static readonly Regex Jwt = new(
        @"\beyJ[A-Za-z0-9_\-]+\.eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\b", RegexOptions.Compiled);

    // AWS access key id: AKIA/ASIA + 16 uppercase alphanumerics (20 total).
    private static readonly Regex AwsKey = new(@"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b", RegexOptions.Compiled);

    // Google API key: "AIza" + 35 url-safe chars.
    private static readonly Regex GcpKey = new(@"\bAIza[A-Za-z0-9_\-]{35}\b", RegexOptions.Compiled);

    // GitHub token (ghp_/gho_/ghu_/ghs_/ghr_) and Slack token (xoxb-/xoxp-/…).
    private static readonly Regex GitHubToken = new(@"\bgh[pousr]_[A-Za-z0-9]{36,}\b", RegexOptions.Compiled);
    private static readonly Regex SlackToken = new(@"\bxox[baprs]-[A-Za-z0-9\-]{10,}\b", RegexOptions.Compiled);

    // A credentialed URI: scheme://user:pass@host (mongodb, postgres, redis, amqp, …).
    private static readonly Regex ConnectionUri = new(
        @"\b[a-z][a-z0-9+.\-]*://[^\s:@/]+:[^\s:@/]+@[^\s/]+", RegexOptions.Compiled);

    // Aggressive-only: a "key = value" credential assignment (password/secret/token/api key …).
    private static readonly Regex SecretAssignment = new(
        @"(?i)\b(?:pass(?:word|wd)?|pwd|secret|token|api[_\- ]?key|access[_\- ]?key|client[_\- ]?secret)\b\s*[=:]\s*[""']?[^\s""';,}{]{6,}",
        RegexOptions.Compiled);

    // Aggressive-only: a long high-entropy token (base64/hex-ish). Entropy-gated below.
    private static readonly Regex HighEntropyToken = new(
        @"(?<![A-Za-z0-9+/_\-])[A-Za-z0-9+/_\-]{32,}={0,2}(?![A-Za-z0-9+/_\-])", RegexOptions.Compiled);

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
        // Secrets first — the most specific, highest-confidence formats claim their text before
        // any looser pattern (a JWT/URI can look phone- or number-ish in places).
        new() { Regex = PrivateKey,    Category = RedactionCategories.PrivateKey,       Token = "[PRIVATE_KEY]" },
        new() { Regex = Jwt,           Category = RedactionCategories.Jwt,              Token = "[JWT]" },
        new() { Regex = ConnectionUri, Category = RedactionCategories.ConnectionString, Token = "[CONNECTION_STRING]" },
        new() { Regex = AwsKey,        Category = RedactionCategories.ApiKey,           Token = "[API_KEY]" },
        new() { Regex = GcpKey,        Category = RedactionCategories.ApiKey,           Token = "[API_KEY]" },
        new() { Regex = GitHubToken,   Category = RedactionCategories.ApiKey,           Token = "[API_KEY]" },
        new() { Regex = SlackToken,    Category = RedactionCategories.ApiKey,           Token = "[API_KEY]" },

        new() { Regex = Email,        Category = RedactionCategories.Email,       Token = "[EMAIL]" },
        new() { Regex = CardLike,     Category = RedactionCategories.Card,        Token = "[CARD]", Validate = IsLuhnCard },
        new() { Regex = Iban,         Category = RedactionCategories.Iban,        Token = "[IBAN]" },
        new() { Regex = Ssn,          Category = RedactionCategories.Ssn,         Token = "[SSN]" },
        new() { Regex = Mac,          Category = RedactionCategories.Mac,         Token = "[MAC]" },
        new() { Regex = Ipv4,         Category = RedactionCategories.Ip,          Token = "[IP]" },
        new() { Regex = Ipv6,         Category = RedactionCategories.Ip,          Token = "[IP]" },
        new() { Regex = Phone,        Category = RedactionCategories.Phone,       Token = "[PHONE]" },

        // Aggressive, broad patterns last so specific rules above win every overlap. The two
        // heuristic secret rules (keyword assignment, then high-entropy) sit at the very end —
        // high-entropy is the loosest, so it claims only what nothing else did.
        new() { Regex = Geo,              Category = RedactionCategories.Geo,         Token = "[GEO]",    Aggressive = true },
        new() { Regex = DobLike,          Category = RedactionCategories.DateOfBirth, Token = "[DATE]",   Aggressive = true },
        new() { Regex = SecretAssignment, Category = RedactionCategories.Secret,      Token = "[SECRET]", Aggressive = true },
        new() { Regex = LongDigitRun,     Category = RedactionCategories.LongNumber,  Token = "[NUMBER]", Aggressive = true },
        new() { Regex = HighEntropyToken, Category = RedactionCategories.Secret,      Token = "[SECRET]", Aggressive = true, Validate = IsHighEntropy },
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

        // Reuse one string view of the masked buffer across rules, rebuilding it only after a
        // rule actually masked something (most rules match nothing on a given text).
        var current = new string(work);

        // Caller's custom rules run before the built-ins, so a domain pattern claims its text
        // ahead of a looser built-in.
        foreach (var rule in _customRules)
        {
            if (_options.DisabledCategories.Contains(rule.Category)) continue;
            if (ApplyRule(rule, current, work, claimed, spans)) current = new string(work);
        }

        foreach (var rule in Rules)
        {
            if (rule.Aggressive && _options.Level != RedactionLevel.Aggressive) continue;
            if (_options.DisabledCategories.Contains(rule.Category)) continue;
            if (ApplyRule(rule, current, work, claimed, spans)) current = new string(work);
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

    // Match one rule against the masked working text, claiming + masking each accepted match.
    // Returns true if anything was masked (so the caller rebuilds the string view). Custom-rule
    // regexes carry a match timeout; a timeout is treated as "no match" and never throws.
    private bool ApplyRule(MatchRule rule, string current, char[] work, bool[] claimed, List<RedactionSpan> spans)
    {
        var masked = false;
        try
        {
            foreach (Match m in rule.Regex.Matches(current))
            {
                if (rule.Validate is not null && !rule.Validate(m)) continue;
                if (_options.AllowList.Contains(m.Value)) continue;
                if (TryClaim(m.Index, m.Length, rule.Category, claimed, spans))
                {
                    Mask(work, m.Index, m.Length);
                    masked = true;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // A pathological custom pattern timed out — skip it rather than fail the redaction.
        }
        return masked;
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
        if (_customTokens.TryGetValue(category, out var customRuleToken)) return customRuleToken;
        if (category == RedactionCategories.Custom) return _options.DenyToken;
        return DefaultTokens[category];
    }

    private static Dictionary<string, string> BuildDefaultTokens()
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in Rules) tokens[rule.Category] = rule.Token;   // IP appears twice, same token
        return tokens;
    }

    // Gate the high-entropy rule: long enough, a letter+digit mix (skips prose and pure hyphenated
    // words), and Shannon entropy over ~4 bits/char — enough to flag random-looking tokens while
    // leaving ordinary identifiers alone. Aggressive-only, so some false positives are acceptable.
    private static bool IsHighEntropy(Match m)
    {
        var s = m.Value;
        if (s.Length < 32) return false;

        var hasLetter = false;
        var hasDigit = false;
        foreach (var c in s)
        {
            if (char.IsLetter(c)) hasLetter = true;
            else if (char.IsDigit(c)) hasDigit = true;
        }
        if (!hasLetter || !hasDigit) return false;

        return ShannonEntropy(s) >= 4.0;
    }

    private static double ShannonEntropy(string s)
    {
        var counts = new Dictionary<char, int>();
        foreach (var c in s) counts[c] = counts.GetValueOrDefault(c) + 1;

        double entropy = 0;
        foreach (var count in counts.Values)
        {
            double p = (double)count / s.Length;
            entropy -= p * Math.Log(p, 2);
        }
        return entropy;
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
