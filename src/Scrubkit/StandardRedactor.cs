using System.Text.RegularExpressions;

namespace Scrubkit;

/// <summary>
/// Best-effort pattern-based redactor. Replaces high-confidence sensitive values with
/// category tokens (<c>[EMAIL]</c>, <c>[PHONE]</c>, …). At
/// <see cref="RedactionLevel.Aggressive"/> it also removes lower-confidence patterns
/// that trade precision for recall. Card numbers are Luhn-checked to cut false hits.
/// </summary>
public sealed class StandardRedactor : IRedactor
{
    private readonly RedactionLevel _level;

    public StandardRedactor(RedactionLevel level = RedactionLevel.Standard) => _level = level;

    private static readonly Regex Email =
        new(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

    private static readonly Regex Ssn =
        new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);

    // 13–16 digit runs, optionally separated by spaces/dashes — Luhn-validated below.
    private static readonly Regex CardLike =
        new(@"\b(?:\d[ -]?){13,16}\b", RegexOptions.Compiled);

    private static readonly Regex Phone =
        new(@"(?<!\d)(?:\+?\d{1,3}[ .\-]?)?(?:\(\d{2,4}\)[ .\-]?)?\d{3,4}[ .\-]\d{3,4}(?:[ .\-]\d{2,4})?(?!\d)",
            RegexOptions.Compiled);

    private static readonly Regex Ipv4 =
        new(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b", RegexOptions.Compiled);

    // Aggressive-only:
    private static readonly Regex DobLike =
        new(@"\b(?:\d{1,2}[/\-.]){2}(?:19|20)\d{2}\b", RegexOptions.Compiled);

    private static readonly Regex LongDigitRun =
        new(@"\b\d{7,}\b", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, int> EmptyCounts = new Dictionary<string, int>();

    public RedactionResult Redact(string text)
    {
        if (string.IsNullOrEmpty(text) || _level == RedactionLevel.Off)
            return new RedactionResult(text, EmptyCounts);

        var counts = new Dictionary<string, int>();

        // Order matters: email and card before the looser phone/digit patterns.
        text = Replace(text, Email, "[EMAIL]", "Email", counts);
        text = ReplaceCards(text, counts);
        text = Replace(text, Ssn, "[SSN]", "SSN", counts);
        text = Replace(text, Ipv4, "[IP]", "IP", counts);
        text = Replace(text, Phone, "[PHONE]", "Phone", counts);

        if (_level == RedactionLevel.Aggressive)
        {
            text = Replace(text, DobLike, "[DATE]", "DateOfBirth", counts);
            text = Replace(text, LongDigitRun, "[NUMBER]", "LongNumber", counts);
        }

        return new RedactionResult(text, counts);
    }

    private static string Replace(string text, Regex rx, string token, string category, Dictionary<string, int> counts)
    {
        int n = 0;
        var result = rx.Replace(text, _ => { n++; return token; });
        if (n > 0) counts[category] = counts.GetValueOrDefault(category) + n;
        return result;
    }

    private static string ReplaceCards(string text, Dictionary<string, int> counts)
    {
        int n = 0;
        var result = CardLike.Replace(text, m =>
        {
            var digits = m.Value.Where(char.IsDigit).ToArray();
            if (digits.Length is < 13 or > 16 || !LuhnValid(digits)) return m.Value;
            n++;
            return "[CARD]";
        });
        if (n > 0) counts["Card"] = counts.GetValueOrDefault("Card") + n;
        return result;
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
