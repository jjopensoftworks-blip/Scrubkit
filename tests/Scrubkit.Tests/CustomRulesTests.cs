using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class CustomRulesTests
{
    private static StandardRedactor Redactor(params CustomRedactionRule[] rules)
    {
        var opts = new StandardRedactorOptions();
        foreach (var r in rules) opts.CustomRules.Add(r);
        return new StandardRedactor(opts);
    }

    [Fact]
    public void Custom_rule_redacts_and_reports_its_category()
    {
        var r = Redactor(new CustomRedactionRule { Category = "EmployeeId", Pattern = @"\bE\d{6}\b", Token = "[EMP]" });

        var result = r.Redact("ticket for E123456 please");

        Assert.Equal("ticket for [EMP] please", result.Text);
        Assert.Equal(1, result.Counts["EmployeeId"]);
        var span = Assert.Single(result.Spans);
        Assert.Equal("EmployeeId", span.Category);
    }

    [Fact]
    public void Token_defaults_to_bracketed_upper_category()
    {
        var r = Redactor(new CustomRedactionRule { Category = "CaseNo", Pattern = @"\bC-\d+\b" });
        Assert.Equal("see [CASENO]", r.Redact("see C-99").Text);
    }

    [Fact]
    public void IgnoreCase_is_honoured()
    {
        var sensitive = Redactor(new CustomRedactionRule { Category = "Code", Pattern = "secret", Token = "[X]" });
        Assert.Equal("SECRET stays", sensitive.Redact("SECRET stays").Text);   // case-sensitive: no match

        var insensitive = Redactor(new CustomRedactionRule { Category = "Code", Pattern = "secret", Token = "[X]", IgnoreCase = true });
        Assert.Equal("[X] goes", insensitive.Redact("SECRET goes").Text);
    }

    [Fact]
    public void Custom_rule_wins_overlap_with_a_looser_builtin()
    {
        // At Aggressive, the built-in LongNumber ([NUMBER]) would grab a long digit run — but the
        // custom rule runs first and claims it under its own category/token.
        var opts = new StandardRedactorOptions { Level = RedactionLevel.Aggressive };
        opts.CustomRules.Add(new CustomRedactionRule { Category = "AccountId", Pattern = @"\b1234567\b", Token = "[ACCT]" });
        var r = new StandardRedactor(opts);

        var result = r.Redact("acct 1234567 end");
        Assert.Contains("[ACCT]", result.Text);
        Assert.DoesNotContain("[NUMBER]", result.Text);
        Assert.Equal(1, result.Counts["AccountId"]);
    }

    [Fact]
    public void Custom_rules_compose_with_builtins_allow_and_deny()
    {
        var opts = new StandardRedactorOptions();
        opts.CustomRules.Add(new CustomRedactionRule { Category = "Badge", Pattern = @"\bB\d{4}\b", Token = "[BADGE]" });
        opts.AllowList.Add("keep@example.com");
        var r = new StandardRedactor(opts);

        var result = r.Redact("B1234 mailto a@b.com and keep@example.com");
        Assert.Contains("[BADGE]", result.Text);            // custom
        Assert.Contains("[EMAIL]", result.Text);            // built-in still runs
        Assert.Contains("keep@example.com", result.Text);   // allow-listed, untouched
    }

    [Fact]
    public void Disabling_a_custom_category_skips_it()
    {
        var opts = new StandardRedactorOptions();
        opts.CustomRules.Add(new CustomRedactionRule { Category = "Badge", Pattern = @"\bB\d{4}\b" });
        opts.DisabledCategories.Add("Badge");
        Assert.Equal("B1234 stays", new StandardRedactor(opts).Redact("B1234 stays").Text);
    }

    [Fact]
    public void Invalid_pattern_throws_at_construction()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Redactor(new CustomRedactionRule { Category = "Bad", Pattern = "(" }));
        Assert.Contains("Bad", ex.Message);
    }

    [Fact]
    public void Empty_category_or_pattern_throws()
    {
        Assert.Throws<ArgumentException>(() => Redactor(new CustomRedactionRule { Category = "", Pattern = "x" }));
        Assert.Throws<ArgumentException>(() => Redactor(new CustomRedactionRule { Category = "C", Pattern = "" }));
    }

    [Fact]
    public void Catastrophic_pattern_times_out_and_is_skipped_not_thrown()
    {
        // A classic catastrophic-backtracking pattern against non-matching input: the 1s match
        // timeout kicks in and the rule is skipped rather than hanging or throwing.
        var r = Redactor(new CustomRedactionRule { Category = "Evil", Pattern = @"(a+)+$" });
        var input = new string('a', 40) + "!";   // forces heavy backtracking, never matches

        var result = r.Redact(input);   // must return (no throw, no hang)

        Assert.Equal(input, result.Text);   // rule contributed nothing
    }
}
