// Copyright © 2026 jjopensoftworks-blip

using System.Diagnostics;
using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class ReDosTests
{
    [Fact]
    public void Normalize_Handles_Empty_And_Whitespace()
    {
        Assert.Equal("", FolderScrubber.Normalize(""));
        Assert.Equal("", FolderScrubber.Normalize("   "));
        Assert.Equal("", FolderScrubber.Normalize("\t\r\n  "));
    }

    [Fact]
    public void Normalize_Leaves_Clean_String_Unchanged()
    {
        var input = "Hello world from Scrubkit";
        var result = FolderScrubber.Normalize(input);
        Assert.Same(input, result); // Same reference = 0 allocation!
    }

    [Fact]
    public void Normalize_Collapses_Multiple_Spaces_And_Newlines()
    {
        var input = "  Hello \t\n  world \r\n from   Scrubkit  ";
        var expected = "Hello world from Scrubkit";
        Assert.Equal(expected, FolderScrubber.Normalize(input));
    }

    [Fact]
    public void Normalize_Handles_Leading_Trailing_Spaces_Without_Inner_MultiSpace()
    {
        var input = "   hello world   ";
        var expected = "hello world";
        var result = FolderScrubber.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_Handles_Single_Char_And_Tab_Whitespace()
    {
        Assert.Equal("a", FolderScrubber.Normalize("a"));
        Assert.Equal("a", FolderScrubber.Normalize("  a  "));
        Assert.Equal("hello world", FolderScrubber.Normalize("hello\tworld"));
    }

    [Fact]
    public void BuiltIn_Rules_Do_Not_Hang_On_Pathological_Inputs()
    {
        var redactor = new StandardRedactor(RedactionLevel.Aggressive);

        // Pathological inputs that cause catastrophic backtracking in unbounded regexes
        var adversarialInputs = new[]
        {
            new string('a', 50000) + "@" + new string('b', 50000),
            "123-45-" + new string('9', 10000) + "x",
            "eyJ" + new string('a', 20000) + "." + new string('b', 20000),
            "http://user:" + new string('a', 20000) + "@" + new string('b', 20000),
            "password = " + new string('!', 20000),
        };

        var sw = Stopwatch.StartNew();
        foreach (var input in adversarialInputs)
        {
            var result = redactor.Redact(input);
            Assert.NotNull(result.Text);
        }
        sw.Stop();

        // Must complete safely under 3 seconds total across all adversarial cases
        Assert.True(sw.ElapsedMilliseconds < 3000, $"Adversarial redaction took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Custom_Pathological_Rule_Times_Out_Gracefully()
    {
        // A custom rule with catastrophic backtracking: (a+)+b matched against "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!"
        var customRule = new CustomRedactionRule
        {
            Category = "pathological",
            Pattern = @"(a+)+b"
        };

        var options = new StandardRedactorOptions();
        options.CustomRules.Add(customRule);

        var redactor = new StandardRedactor(options);
        var input = new string('a', 30) + "!";

        var sw = Stopwatch.StartNew();
        var result = redactor.Redact(input);
        sw.Stop();

        // Should time out gracefully (1 sec match timeout) without throwing exception or hanging
        Assert.Equal(input, result.Text);
        Assert.True(sw.ElapsedMilliseconds < 2500, $"Custom rule timeout took too long: {sw.ElapsedMilliseconds}ms");
    }
}
