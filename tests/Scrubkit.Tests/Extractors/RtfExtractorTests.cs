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

    [Fact]
    public void Maps_dashes_quotes_and_bullet()
    {
        var t = Extract(@"{\rtf1 \endash\lquote\rquote\ldblquote\rdblquote\bullet}");
        Assert.Contains("–", t);   // endash
        Assert.Contains("‘", t);   // lquote
        Assert.Contains("’", t);   // rquote
        Assert.Contains("“", t);   // ldblquote
        Assert.Contains("”", t);   // rdblquote
        Assert.Contains("•", t);   // bullet
    }

    [Fact]
    public void Tab_and_cell_become_spaces()
    {
        Assert.Equal("a b", Extract(@"{\rtf1 a\tab b}"));
        Assert.Equal("a b", Extract(@"{\rtf1 a\cell b}"));
    }

    [Fact]
    public void Handles_nbsp_and_hyphen_control_symbols()
    {
        Assert.Equal("a b-c-d", Extract(@"{\rtf1 a\~b\_c\-d}"));
    }

    [Fact]
    public void Uc_override_skips_multiple_fallback_chars()
    {
        // \uc2 sets the fallback width to 2, so both '?'s after \u233 are dropped.
        Assert.Equal("café", Extract(@"{\rtf1\uc2 caf\u233??}"));
    }
}
