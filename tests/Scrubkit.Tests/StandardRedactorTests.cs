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
}
