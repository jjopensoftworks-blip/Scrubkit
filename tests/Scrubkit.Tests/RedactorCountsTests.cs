using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class RedactorCountsTests
{
    [Theory]
    [InlineData("one a@b.com", "Email", 1)]
    [InlineData("two a@b.com and c@d.com", "Email", 2)]
    [InlineData("ssn 123-45-6789 and 001-23-4567", "SSN", 2)]
    [InlineData("ips 8.8.8.8 1.1.1.1 9.9.9.9", "IP", 3)]
    [InlineData("card 4111111111111111", "Card", 1)]
    [InlineData("phone 555-123-4567", "Phone", 1)]
    [InlineData("nic 00:1A:2B:3C:4D:5E and 0A:0B:0C:0D:0E:0F", "MAC", 2)]
    public void Counts_matches_per_category(string input, string category, int expected) =>
        Assert.Equal(expected, new StandardRedactor().Redact(input).Counts[category]);

    [Fact]
    public void Total_count_equals_span_count()
    {
        var r = new StandardRedactor().Redact("a@b.com 8.8.8.8 555-123-4567");
        Assert.Equal(r.Spans.Count, r.Counts.Values.Sum());
        Assert.Equal(3, r.Spans.Count);
    }

    [Fact]
    public void Empty_input_yields_no_redactions()
    {
        var r = new StandardRedactor().Redact("");
        Assert.Equal("", r.Text);
        Assert.Empty(r.Counts);
        Assert.Empty(r.Spans);
    }
}
