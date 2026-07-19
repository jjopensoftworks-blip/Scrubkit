using System.Text;
using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

/// <summary>
/// Property and fuzz tests for <see cref="StandardRedactor"/>: invariants must hold over a
/// large sweep of random input, and adversarial input must not trigger catastrophic regex
/// backtracking (ReDoS).
/// </summary>
public class RedactorFuzzTests
{
    // Default replacement tokens, mirrored here so we can reconstruct the redacted text from
    // the reported spans and check they line up exactly.
    private static readonly Dictionary<string, string> Tokens = new()
    {
        ["Email"] = "[EMAIL]",
        ["Card"] = "[CARD]",
        ["IBAN"] = "[IBAN]",
        ["SSN"] = "[SSN]",
        ["IP"] = "[IP]",
        ["MAC"] = "[MAC]",
        ["Phone"] = "[PHONE]",
        ["Geo"] = "[GEO]",
        ["DateOfBirth"] = "[DATE]",
        ["LongNumber"] = "[NUMBER]",
        ["Custom"] = "[REDACTED]",
    };

    // A charset rich in the characters that drive the patterns (digits, @ . : - / , spaces,
    // hex letters, brackets) so random strings actually exercise them.
    private static readonly char[] Charset =
        "abcdefABCDEF0123456789 @.:-/,\t\n[]()+GBUS".ToCharArray();

    [Theory]
    [InlineData(RedactionLevel.Standard)]
    [InlineData(RedactionLevel.Aggressive)]
    public void Random_input_never_throws_and_holds_invariants(RedactionLevel level)
    {
        var redactor = new StandardRedactor(level);
        var rnd = new Random(20260719);

        for (var iteration = 0; iteration < 1500; iteration++)
        {
            var input = RandomString(rnd, rnd.Next(0, 220));

            var r = redactor.Redact(input);   // must never throw

            // Counts and spans agree.
            Assert.Equal(r.Spans.Count, r.Counts.Values.Sum());

            // Spans are in-bounds, non-empty, sorted, and non-overlapping.
            var prevEnd = 0;
            foreach (var s in r.Spans)
            {
                Assert.True(s.Length > 0);
                Assert.InRange(s.Start, 0, input.Length);
                Assert.InRange(s.Start + s.Length, 0, input.Length);
                Assert.True(s.Start >= prevEnd, "spans overlap or are unsorted");
                prevEnd = s.Start + s.Length;
            }

            // The spans exactly explain the redacted text.
            Assert.Equal(r.Text, Rebuild(input, r));

            // Deterministic: same input, same result.
            Assert.Equal(r.Text, redactor.Redact(input).Text);

            // Idempotent: tokens never re-match, so a second pass is a no-op.
            Assert.Equal(r.Text, redactor.Redact(r.Text).Text);
        }
    }

    [Fact]
    public async Task Adversarial_input_does_not_catastrophically_backtrack()
    {
        // Inputs crafted to stress each pattern's backtracking. If any regex is quadratic
        // enough to matter — or exponential — Redact won't return within the budget.
        var inputs = new[]
        {
            new string('9', 5000),
            new string(':', 2000),
            new string('-', 2000),
            string.Concat(Enumerable.Repeat("a:", 80)) + "!",         // IPv6-ish, fails at the end
            string.Concat(Enumerable.Repeat("1-", 500)) + "1",        // card/phone-ish
            "GB00" + new string('A', 600),                            // IBAN-ish
            new string('a', 400) + "@" + new string('a', 400),        // email-ish, no TLD
            string.Concat(Enumerable.Repeat("1.2,", 400)),            // geo-ish
            string.Concat(Enumerable.Repeat("12:34:56:78:90:ab ", 150)),
            string.Concat(Enumerable.Repeat("2001:db8::1 ", 150)),
        };

        var redactor = new StandardRedactor(RedactionLevel.Aggressive);
        var budget = TimeSpan.FromSeconds(5);

        foreach (var input in inputs)
        {
            var work = Task.Run(() => redactor.Redact(input));
            var finished = await Task.WhenAny(work, Task.Delay(budget));
            Assert.True(finished == work,
                $"redaction did not finish within {budget.TotalSeconds}s on a {input.Length}-char input " +
                "— likely catastrophic regex backtracking");
        }
    }

    private static string RandomString(Random rnd, int length)
    {
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++) sb.Append(Charset[rnd.Next(Charset.Length)]);
        return sb.ToString();
    }

    private static string Rebuild(string input, RedactionResult r)
    {
        var sb = new StringBuilder(input.Length);
        var pos = 0;
        foreach (var s in r.Spans)
        {
            sb.Append(input, pos, s.Start - pos);
            sb.Append(Tokens[s.Category]);
            pos = s.Start + s.Length;
        }
        sb.Append(input, pos, input.Length - pos);
        return sb.ToString();
    }
}
