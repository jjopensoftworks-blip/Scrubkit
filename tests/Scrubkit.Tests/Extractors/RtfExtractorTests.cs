using Scrubkit;
using Xunit;

namespace Scrubkit.Tests.Extractors;

public class RtfExtractorTests
{
    private static string Extract(string rtf)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".rtf");
        File.WriteAllText(path, rtf);
        try { return new RtfExtractor().Extract(path).Text; }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Strips_control_words_and_returns_body_text()
    {
        Assert.Equal("hello", Extract(@"{\rtf1\ansi\deff0 hello}"));
    }

    [Fact]
    public void Par_becomes_a_line_break()
    {
        Assert.Equal("one\ntwo", Extract(@"{\rtf1 one\par two}"));
    }

    [Fact]
    public void Drops_font_and_color_tables()
    {
        var text = Extract(
            @"{\rtf1{\fonttbl{\f0 Arial;}}{\colortbl;\red0\green0\blue0;}Body text}");
        Assert.Equal("Body text", text);
        Assert.DoesNotContain("Arial", text);
        Assert.DoesNotContain("red0", text);
    }

    [Fact]
    public void Ignores_starred_destinations()
    {
        var text = Extract(@"{\rtf1{\*\generator Riched20}Visible}");
        Assert.Equal("Visible", text);
        Assert.DoesNotContain("Riched20", text);
    }

    [Fact]
    public void Expands_hex_escape()
    {
        // \'e9 is é in Windows-1252/Latin-1.
        Assert.Equal("café", Extract(@"{\rtf1 caf\'e9}"));
    }

    [Fact]
    public void Expands_unicode_escape_and_skips_fallback()
    {
        // \u233 is é; the '?' is the ASCII fallback and must be dropped.
        Assert.Equal("café", Extract(@"{\rtf1 caf\u233?}"));
    }

    [Fact]
    public void Handles_escaped_braces_and_backslash()
    {
        Assert.Equal(@"a{b}c\d", Extract(@"{\rtf1 a\{b\}c\\d}"));
    }

    [Fact]
    public void Maps_common_symbol_control_words()
    {
        Assert.Equal("a—b", Extract(@"{\rtf1 a\emdash b}"));
    }
}
