using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

/// <summary>Stable tokens and format-preserving masks (de-identify while staying joinable).</summary>
public class RedactorDeidentifyTests
{
    // ---- stable tokens ----

    [Fact]
    public void Stable_token_replaces_the_plain_token_with_a_suffixed_one()
    {
        var options = new StandardRedactorOptions { StableTokens = true };
        var r = new StandardRedactor(options).Redact("mail jane@example.com");

        Assert.Matches(@"\[EMAIL_[0-9a-f]{8}\]", r.Text);
        Assert.DoesNotContain("jane@example.com", r.Text);
        Assert.Equal(1, r.Counts["Email"]);
    }

    [Fact]
    public void Equal_values_get_the_same_stable_token()
    {
        var options = new StandardRedactorOptions { StableTokens = true };
        var r = new StandardRedactor(options).Redact("from jane@example.com to jane@example.com");

        var token = System.Text.RegularExpressions.Regex.Match(r.Text, @"\[EMAIL_[0-9a-f]{8}\]").Value;
        Assert.NotEqual("", token);
        // Both occurrences collapse to the identical token → joinable.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(r.Text, System.Text.RegularExpressions.Regex.Escape(token)).Count);
    }

    [Fact]
    public void Different_values_get_different_stable_tokens()
    {
        var options = new StandardRedactorOptions { StableTokens = true };
        var r = new StandardRedactor(options).Redact("a@x.com and b@y.com");

        var matches = System.Text.RegularExpressions.Regex.Matches(r.Text, @"\[EMAIL_[0-9a-f]{8}\]");
        Assert.Equal(2, matches.Count);
        Assert.NotEqual(matches[0].Value, matches[1].Value);
    }

    [Fact]
    public void Salt_changes_the_stable_token()
    {
        var plain = new StandardRedactor(new StandardRedactorOptions { StableTokens = true })
            .Redact("mail jane@example.com").Text;
        var salted = new StandardRedactor(new StandardRedactorOptions { StableTokens = true, TokenSalt = "pepper" })
            .Redact("mail jane@example.com").Text;

        Assert.NotEqual(plain, salted);
    }

    [Fact]
    public void Stable_token_honours_a_custom_token_without_brackets()
    {
        var options = new StandardRedactorOptions { StableTokens = true };
        options.Tokens["Email"] = "EMAIL";   // no surrounding brackets
        var r = new StandardRedactor(options).Redact("mail jane@example.com");

        Assert.Matches(@"EMAIL_[0-9a-f]{8}", r.Text);
    }

    // ---- format-preserving mask ----

    [Fact]
    public void Reveal_last_keeps_the_trailing_digits_of_a_card()
    {
        var options = new StandardRedactorOptions();
        options.RevealLast["Card"] = 4;
        var r = new StandardRedactor(options).Redact("card 4111 1111 1111 1111 ok");

        Assert.Contains("**** **** **** 1111", r.Text);
        Assert.DoesNotContain("4111", r.Text);
        Assert.Equal(1, r.Counts["Card"]);
    }

    [Fact]
    public void Reveal_last_preserves_separators_for_an_ssn()
    {
        var options = new StandardRedactorOptions();
        options.RevealLast["SSN"] = 4;
        var r = new StandardRedactor(options).Redact("ssn 123-45-6789");

        Assert.Contains("***-**-6789", r.Text);
    }

    [Fact]
    public void Reveal_last_zero_masks_everything_but_keeps_shape()
    {
        var options = new StandardRedactorOptions();
        options.RevealLast["SSN"] = 0;
        var r = new StandardRedactor(options).Redact("ssn 123-45-6789");

        Assert.Contains("***-**-****", r.Text);
    }

    [Fact]
    public void Custom_mask_char_is_used()
    {
        var options = new StandardRedactorOptions { MaskChar = 'X' };
        options.RevealLast["Card"] = 4;
        var r = new StandardRedactor(options).Redact("card 4111111111111111");

        Assert.Contains("XXXXXXXXXXXX1111", r.Text);
    }

    [Fact]
    public void Reveal_last_takes_precedence_over_stable_tokens_for_that_category()
    {
        var options = new StandardRedactorOptions { StableTokens = true };
        options.RevealLast["Card"] = 4;
        var r = new StandardRedactor(options).Redact("card 4111111111111111 mail a@b.com");

        Assert.Contains("************1111", r.Text);            // card: format-preserving mask
        Assert.Matches(@"\[EMAIL_[0-9a-f]{8}\]", r.Text);      // email: still a stable token
    }

    [Fact]
    public void Spans_still_point_at_the_original_text()
    {
        var options = new StandardRedactorOptions();
        options.RevealLast["Email"] = 0;
        var input = "mail jane@example.com";
        var r = new StandardRedactor(options).Redact(input);

        var span = Assert.Single(r.Spans);
        Assert.Equal("jane@example.com", input.Substring(span.Start, span.Length));
    }
}
