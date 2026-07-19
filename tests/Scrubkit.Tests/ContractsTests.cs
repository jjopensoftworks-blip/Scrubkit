using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

/// <summary>Construction and defaults for the shared contract types.</summary>
public class ContractsTests
{
    [Fact]
    public void RedactionSpan_exposes_its_fields()
    {
        var span = new RedactionSpan(3, 5, "Email");
        Assert.Equal(3, span.Start);
        Assert.Equal(5, span.Length);
        Assert.Equal("Email", span.Category);
    }

    [Fact]
    public void RedactionResult_two_arg_ctor_has_empty_spans()
    {
        var r = new RedactionResult("x", new Dictionary<string, int>());
        Assert.Equal("x", r.Text);
        Assert.Empty(r.Spans);
        Assert.Empty(r.Counts);
    }

    [Fact]
    public void RedactionResult_three_arg_ctor_keeps_spans()
    {
        var spans = new[] { new RedactionSpan(0, 1, "IP") };
        var r = new RedactionResult("y", new Dictionary<string, int> { ["IP"] = 1 }, spans);
        Assert.Single(r.Spans);
        Assert.Equal(1, r.Counts["IP"]);
    }

    [Fact]
    public void ExtractedContent_empty_is_empty()
    {
        Assert.Empty(ExtractedContent.Empty.Metadata);
        Assert.Equal("", ExtractedContent.Empty.Text);
    }

    [Fact]
    public void ExtractedContent_stores_metadata_and_text()
    {
        var content = new ExtractedContent(new Dictionary<string, string> { ["A"] = "1" }, "hi");
        Assert.Equal("hi", content.Text);
        Assert.Equal("1", content.Metadata["A"]);
    }

    [Theory]
    [InlineData(RedactionCategories.Email, "Email")]
    [InlineData(RedactionCategories.Card, "Card")]
    [InlineData(RedactionCategories.Iban, "IBAN")]
    [InlineData(RedactionCategories.Ssn, "SSN")]
    [InlineData(RedactionCategories.Ip, "IP")]
    [InlineData(RedactionCategories.Mac, "MAC")]
    [InlineData(RedactionCategories.Phone, "Phone")]
    [InlineData(RedactionCategories.Geo, "Geo")]
    [InlineData(RedactionCategories.DateOfBirth, "DateOfBirth")]
    [InlineData(RedactionCategories.LongNumber, "LongNumber")]
    [InlineData(RedactionCategories.Custom, "Custom")]
    public void Category_constants_have_stable_values(string actual, string expected) =>
        Assert.Equal(expected, actual);

    [Theory]
    [InlineData(Recursion.AllNested)]
    [InlineData(Recursion.TopOnly)]
    public void ReadOptions_round_trips_recursion(Recursion recursion) =>
        Assert.Equal(recursion, new ReadOptions { Recursion = recursion }.Recursion);

    [Fact]
    public void ReadOptions_defaults_are_sensible()
    {
        var o = new ReadOptions();
        Assert.Equal(Recursion.AllNested, o.Recursion);
        Assert.Equal(1000, o.MaxFiles);
        Assert.Equal(20_000, o.MaxTextLength);
        Assert.Equal(1, o.MaxDegreeOfParallelism);
        Assert.Equal(RedactionLevel.Off, o.Redaction);
        Assert.Null(o.Redactor);
        Assert.Empty(o.Extractors);
        Assert.Empty(o.IncludeExtensions);
    }
}
