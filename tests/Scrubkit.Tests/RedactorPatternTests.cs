using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

/// <summary>Data-driven coverage of each redaction pattern — positives and negatives.</summary>
public class RedactorPatternTests
{
    private static RedactionResult Std(string s) => new StandardRedactor().Redact(s);
    private static RedactionResult Agg(string s) => new StandardRedactor(RedactionLevel.Aggressive).Redact(s);

    [Theory]
    [InlineData("a@b.com")]
    [InlineData("jane.doe@example.co.uk")]
    [InlineData("x+tag@sub.domain.io")]
    [InlineData("user_name@corp.org")]
    [InlineData("first.last@mail.example.com")]
    [InlineData("dev-team@my-company.net")]
    [InlineData("info@example.travel")]
    [InlineData("a.b.c@d.e.fg")]
    [InlineData("test123@numbers9.io")]
    [InlineData("MiXeD.Case@Example.COM")]
    public void Redacts_email(string value)
    {
        var r = Std($"contact {value} now");
        Assert.Contains("[EMAIL]", r.Text);
        Assert.DoesNotContain(value, r.Text);
        Assert.Equal(1, r.Counts["Email"]);
    }

    [Theory]
    [InlineData("4111111111111111")]          // Visa
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("4012888888881881")]          // Visa
    [InlineData("5555555555554444")]          // Mastercard
    [InlineData("5105 1051 0510 5100")]       // Mastercard
    [InlineData("378282246310005")]           // Amex (15)
    [InlineData("6011111111111117")]          // Discover
    public void Redacts_luhn_valid_card(string value)
    {
        var r = Std($"card {value} ok");
        Assert.Contains("[CARD]", r.Text);
        Assert.Equal(1, r.Counts["Card"]);
    }

    [Theory]
    [InlineData("123-45-6789")]
    [InlineData("001-23-4567")]
    [InlineData("999-99-9999")]
    public void Redacts_ssn(string value)
    {
        var r = Std($"ssn {value} end");
        Assert.Contains("[SSN]", r.Text);
        Assert.Equal(1, r.Counts["SSN"]);
    }

    [Theory]
    [InlineData("192.168.0.1")]
    [InlineData("10.0.0.255")]
    [InlineData("8.8.8.8")]
    [InlineData("255.255.255.255")]
    [InlineData("172.16.254.1")]
    public void Redacts_ipv4(string value)
    {
        var r = Std($"host {value} up");
        Assert.Contains("[IP]", r.Text);
        Assert.Equal(1, r.Counts["IP"]);
    }

    [Theory]
    [InlineData("2001:0db8:0000:0000:0000:ff00:0042:8329")]
    [InlineData("2001:db8:85a3::8a2e:370:7334")]
    [InlineData("fe80::1")]
    public void Redacts_ipv6(string value)
    {
        var r = Std($"addr {value} up");
        Assert.Contains("[IP]", r.Text);
        Assert.True(r.Counts.ContainsKey("IP"));
    }

    [Theory]
    [InlineData("00:1A:2B:3C:4D:5E")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("0A:0B:0C:0D:0E:0F")]
    public void Redacts_mac(string value)
    {
        var r = Std($"nic {value} link");
        Assert.Contains("[MAC]", r.Text);
        Assert.Equal(1, r.Counts["MAC"]);
    }

    [Theory]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    [InlineData("DE89370400440532013000")]
    [InlineData("FR1420041010050500013M02606")]
    public void Redacts_iban(string value)
    {
        var r = Std($"iban {value} paid");
        Assert.Contains("[IBAN]", r.Text);
        Assert.Equal(1, r.Counts["IBAN"]);
    }

    [Theory]
    [InlineData("12/31/1990")]
    [InlineData("01-01-2000")]
    [InlineData("31.12.1985")]
    [InlineData("7/4/1976")]
    public void Redacts_dob_at_aggressive(string value)
    {
        Assert.Contains(value, Std($"born {value}").Text);   // untouched at Standard
        Assert.Contains("[DATE]", Agg($"born {value}").Text);
    }

    [Theory]
    [InlineData("40.7128, -74.0060")]
    [InlineData("51.5074, -0.1278")]
    public void Redacts_geo_at_aggressive(string value)
    {
        Assert.Contains(value, Std($"at {value}").Text);     // untouched at Standard
        Assert.Contains("[GEO]", Agg($"at {value}").Text);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("1234567890")]
    [InlineData("999999999999")]
    public void Redacts_long_digit_run_at_aggressive(string value)
    {
        Assert.Contains(value, Std($"ref {value}").Text);    // untouched at Standard
        Assert.Contains("[NUMBER]", Agg($"ref {value}").Text);
    }

    [Theory]
    [InlineData("just some plain text")]
    [InlineData("meeting notes for today")]
    [InlineData("hello world")]
    [InlineData("chapter one begins")]
    [InlineData("see page 5 for details")]
    [InlineData("room 12 is booked")]
    [InlineData("buy 3 apples and 2 pears")]
    [InlineData("TODO fix this later")]
    [InlineData("figure 2 shows the trend")]
    [InlineData("part 4 of 6 complete")]
    [InlineData("the year was 2024")]
    [InlineData("lorem ipsum dolor sit amet")]
    [InlineData("no numbers here at all")]
    [InlineData("version bump and release")]
    [InlineData("1234567890 is a plain run")]   // long digits are Aggressive-only
    [InlineData("not-an-email@ has no domain")]
    [InlineData("12-34-56 is too short for an ssn")]
    [InlineData("born 12/31/1990 stays at standard")]   // DOB is Aggressive-only
    [InlineData("at 40.7128, -74.0060 stays at standard")]   // geo is Aggressive-only
    [InlineData("just letters and spaces only")]
    [InlineData("a small list: one, two, three")]
    public void Leaves_non_matching_text_untouched_at_standard(string input)
    {
        var r = Std(input);
        Assert.Equal(input, r.Text);
        Assert.Empty(r.Counts);
        Assert.Empty(r.Spans);
    }
}
