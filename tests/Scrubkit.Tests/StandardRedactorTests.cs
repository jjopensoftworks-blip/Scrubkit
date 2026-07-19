using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class StandardRedactorTests
{
    [Fact]
    public void Redacts_email_and_counts_it()
    {
        var r = new StandardRedactor().Redact("ping me at jane.doe@example.com please");

        Assert.Contains("[EMAIL]", r.Text);
        Assert.DoesNotContain("jane.doe@example.com", r.Text);
        Assert.Equal(1, r.Counts["Email"]);
    }

    [Fact]
    public void Luhn_gates_card_redaction()
    {
        // 4111...1111 is a well-known Luhn-valid test number; 1234...3456 is not.
        var valid = new StandardRedactor().Redact("card 4111 1111 1111 1111 here");
        var invalid = new StandardRedactor().Redact("code 1234 5678 9012 3456 here");

        // Only the Luhn-valid run is tagged as a card.
        Assert.Contains("[CARD]", valid.Text);
        Assert.Equal(1, valid.Counts["Card"]);
        Assert.DoesNotContain("[CARD]", invalid.Text);
        Assert.False(invalid.Counts.ContainsKey("Card"));
    }

    [Fact]
    public void Redacts_ssn_and_ip()
    {
        var r = new StandardRedactor().Redact("ssn 123-45-6789 from host 192.168.0.1");

        Assert.Contains("[SSN]", r.Text);
        Assert.Contains("[IP]", r.Text);
    }

    [Fact]
    public void Redacts_phone_number()
    {
        var r = new StandardRedactor().Redact("call 555-123-4567 when ready");

        Assert.Contains("[PHONE]", r.Text);
        Assert.Equal(1, r.Counts["Phone"]);
    }

    [Fact]
    public void Email_and_phone_are_each_categorized_correctly()
    {
        // Ordering invariant: email is claimed before the looser phone pattern,
        // so neither cannibalizes the other.
        var r = new StandardRedactor().Redact("reach me at jo@corp.com or 555-123-4567");

        Assert.Contains("[EMAIL]", r.Text);
        Assert.Contains("[PHONE]", r.Text);
        Assert.Equal(1, r.Counts["Email"]);
        Assert.Equal(1, r.Counts["Phone"]);
    }

    [Fact]
    public void Off_level_returns_text_untouched()
    {
        const string input = "email a@b.com phone 555-123-4567";
        var r = new StandardRedactor(RedactionLevel.Off).Redact(input);

        Assert.Equal(input, r.Text);
        Assert.Empty(r.Counts);
    }

    [Fact]
    public void Aggressive_level_strips_long_digit_runs()
    {
        var standard = new StandardRedactor(RedactionLevel.Standard).Redact("ref 1234567890");
        var aggressive = new StandardRedactor(RedactionLevel.Aggressive).Redact("ref 1234567890");

        Assert.Contains("1234567890", standard.Text);   // untouched at Standard
        Assert.Contains("[NUMBER]", aggressive.Text);     // stripped at Aggressive
    }

    [Fact]
    public void Aggressive_level_strips_dob_like_dates()
    {
        var standard = new StandardRedactor(RedactionLevel.Standard).Redact("born 12/31/1990");
        var aggressive = new StandardRedactor(RedactionLevel.Aggressive).Redact("born 12/31/1990");

        Assert.Contains("12/31/1990", standard.Text);   // untouched at Standard
        Assert.Contains("[DATE]", aggressive.Text);       // stripped at Aggressive
        Assert.Equal(1, aggressive.Counts["DateOfBirth"]);
    }

    // ---- Phase 8: spans, options, and additional patterns ----

    [Fact]
    public void Reports_spans_into_the_original_text()
    {
        const string input = "email jane@x.com and phone 555-123-4567";
        var r = new StandardRedactor().Redact(input);

        Assert.Equal(2, r.Spans.Count);   // in reading order

        Assert.Equal("Email", r.Spans[0].Category);
        Assert.Equal("jane@x.com", input.Substring(r.Spans[0].Start, r.Spans[0].Length));

        Assert.Equal("Phone", r.Spans[1].Category);
        Assert.Equal("555-123-4567", input.Substring(r.Spans[1].Start, r.Spans[1].Length));
    }

    [Fact]
    public void Disabled_category_is_left_alone()
    {
        var options = new StandardRedactorOptions();
        options.DisabledCategories.Add(RedactionCategories.Phone);

        var r = new StandardRedactor(options).Redact("mail a@b.com call 555-123-4567");

        Assert.Contains("[EMAIL]", r.Text);
        Assert.Contains("555-123-4567", r.Text);          // phone kept
        Assert.False(r.Counts.ContainsKey("Phone"));
    }

    [Fact]
    public void Custom_token_overrides_the_default()
    {
        var options = new StandardRedactorOptions();
        options.Tokens[RedactionCategories.Email] = "<redacted-email>";

        var r = new StandardRedactor(options).Redact("write to a@b.com today");

        Assert.Contains("<redacted-email>", r.Text);
        Assert.DoesNotContain("[EMAIL]", r.Text);
    }

    [Fact]
    public void Allow_list_keeps_specific_values()
    {
        var options = new StandardRedactorOptions();
        options.AllowList.Add("support@corp.com");

        var r = new StandardRedactor(options).Redact("ours support@corp.com theirs jane@x.com");

        Assert.Contains("support@corp.com", r.Text);      // allow-listed, kept
        Assert.Contains("[EMAIL]", r.Text);                // the other is still redacted
        Assert.Equal(1, r.Counts["Email"]);
    }

    [Fact]
    public void Deny_terms_are_always_redacted()
    {
        var options = new StandardRedactorOptions { DenyToken = "[SECRET]" };
        options.DenyTerms.Add("Project Zeus");

        var r = new StandardRedactor(options).Redact("codename Project Zeus ships soon");

        Assert.Contains("[SECRET]", r.Text);
        Assert.DoesNotContain("Project Zeus", r.Text);
        Assert.Equal(1, r.Counts["Custom"]);
    }

    [Theory]
    [InlineData("iban GB82 WEST 1234 5698 7654 32 ok", "[IBAN]", "IBAN")]
    [InlineData("mac 00:1A:2B:3C:4D:5E here", "[MAC]", "MAC")]
    [InlineData("host 2001:0db8:0000:0000:0000:ff00:0042:8329 up", "[IP]", "IP")]
    public void Recognizes_additional_standard_patterns(string input, string token, string category)
    {
        var r = new StandardRedactor().Redact(input);

        Assert.Contains(token, r.Text);
        Assert.Equal(1, r.Counts[category]);
    }

    [Fact]
    public void Geo_coordinates_are_aggressive_only()
    {
        const string input = "at 40.7128, -74.0060 downtown";
        var standard = new StandardRedactor(RedactionLevel.Standard).Redact(input);
        var aggressive = new StandardRedactor(RedactionLevel.Aggressive).Redact(input);

        Assert.Contains("40.7128, -74.0060", standard.Text);   // untouched at Standard
        Assert.Contains("[GEO]", aggressive.Text);
    }
}
