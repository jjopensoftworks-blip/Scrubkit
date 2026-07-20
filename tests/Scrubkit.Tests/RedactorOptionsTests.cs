using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

/// <summary>Data-driven coverage of <see cref="StandardRedactorOptions"/>.</summary>
public class RedactorOptionsTests
{
    [Theory]
    [InlineData("Email", "mail a@b.com", "a@b.com")]
    [InlineData("Phone", "call 555-123-4567", "555-123-4567")]
    [InlineData("SSN", "ssn 123-45-6789", "123-45-6789")]
    [InlineData("IP", "host 8.8.8.8", "8.8.8.8")]
    [InlineData("Card", "card 4111111111111111", "4111111111111111")]
    [InlineData("MAC", "nic 00:1A:2B:3C:4D:5E", "00:1A:2B:3C:4D:5E")]
    public void Disabled_category_keeps_its_values(string category, string input, string kept)
    {
        var options = new StandardRedactorOptions();
        options.DisabledCategories.Add(category);

        var r = new StandardRedactor(options).Redact(input);

        Assert.Contains(kept, r.Text);
        Assert.False(r.Counts.ContainsKey(category));
    }

    [Theory]
    [InlineData("Email", "[E]", "mail a@b.com")]
    [InlineData("Phone", "<phone>", "call 555-123-4567")]
    [InlineData("SSN", "***", "ssn 123-45-6789")]
    [InlineData("IP", "#IP#", "host 8.8.8.8")]
    public void Custom_token_is_used(string category, string token, string input)
    {
        var options = new StandardRedactorOptions();
        options.Tokens[category] = token;

        Assert.Contains(token, new StandardRedactor(options).Redact(input).Text);
    }

    [Theory]
    [InlineData("safe@corp.com", "ping safe@corp.com only")]
    [InlineData("555-123-4567", "call 555-123-4567 today")]
    [InlineData("8.8.8.8", "resolver 8.8.8.8 here")]
    public void Allow_listed_value_is_kept(string value, string input)
    {
        var options = new StandardRedactorOptions();
        options.AllowList.Add(value);

        Assert.Contains(value, new StandardRedactor(options).Redact(input).Text);
    }

    [Theory]
    [InlineData("Project Zeus")]
    [InlineData("codename BLUE")]
    [InlineData("internal-only")]
    [InlineData("Acme Roadmap")]
    public void Deny_term_is_redacted_as_custom(string term)
    {
        var options = new StandardRedactorOptions();
        options.DenyTerms.Add(term);

        var r = new StandardRedactor(options).Redact($"the {term} document");

        Assert.DoesNotContain(term, r.Text);
        Assert.Equal(1, r.Counts["Custom"]);
    }

    [Fact]
    public void Deny_term_is_case_insensitive()
    {
        var options = new StandardRedactorOptions();
        options.DenyTerms.Add("Zeus");

        var r = new StandardRedactor(options).Redact("project zeus and ZEUS");

        Assert.Equal(2, r.Counts["Custom"]);
    }

    [Fact]
    public void Null_options_throws() =>
        Assert.Throws<ArgumentNullException>(() => new StandardRedactor((StandardRedactorOptions)null!));

    [Fact]
    public void Options_ctor_matches_level_ctor()
    {
        const string input = "mail a@b.com call 555-123-4567";
        var viaLevel = new StandardRedactor(RedactionLevel.Standard).Redact(input);
        var viaOptions = new StandardRedactor(new StandardRedactorOptions { Level = RedactionLevel.Standard }).Redact(input);

        Assert.Equal(viaLevel.Text, viaOptions.Text);
    }
}
