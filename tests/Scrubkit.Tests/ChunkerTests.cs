// Copyright © 2026 jjopensoftworks-blip

using System.Text.Json;
using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class ChunkerTests
{
    private static FileRecord Rec(string text) => new()
    {
        Path = "C:/x/a.txt",
        Name = "a.txt",
        Extension = ".txt",
        TypeBucket = "Text",
        Metadata = new Dictionary<string, string> { ["Author"] = "Jane" },
        Text = text,
    };

    [Fact]
    public void Short_text_yields_one_chunk_carrying_source_context()
    {
        var chunks = new Chunker().Chunk(Rec("a short document"));

        var c = Assert.Single(chunks);
        Assert.Equal("a short document", c.Text);
        Assert.Equal(0, c.Index);
        Assert.Equal(1, c.Count);
        Assert.Equal(0, c.StartOffset);
        Assert.Equal("C:/x/a.txt", c.Path);
        Assert.Equal("Text", c.TypeBucket);
        Assert.Equal("Jane", c.Metadata["Author"]);
    }

    [Fact]
    public void Empty_text_yields_no_chunks()
    {
        Assert.Empty(new Chunker().Chunk(Rec("")));
    }

    [Fact]
    public void Long_text_splits_into_ordered_overlapping_windows_covering_everything()
    {
        // No whitespace: exercises the hard-boundary path deterministically.
        var text = new string('x', 2500);
        var opts = new ChunkOptions { MaxChars = 1000, OverlapChars = 100, RespectWordBoundaries = false };
        var chunks = new Chunker(opts).Chunk(Rec(text));

        // step = 900 → starts at 0, 900, 1800; last window ends at 2500.
        Assert.Equal(3, chunks.Count);
        Assert.Equal(new[] { 0, 900, 1800 }, chunks.Select(c => c.StartOffset).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, chunks.Select(c => c.Index).ToArray());
        Assert.All(chunks, c => Assert.Equal(3, c.Count));
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 1000));

        // Consecutive chunks overlap by the configured amount, and every character is covered.
        Assert.Equal(text.Substring(0, 1000), chunks[0].Text);
        Assert.Equal(text.Substring(1800), chunks[2].Text);   // last window runs to the end (700 chars)
    }

    [Fact]
    public void Word_boundary_mode_does_not_cut_words_mid_token()
    {
        var text = string.Join(" ", Enumerable.Repeat("lorem", 400));   // ~2400 chars, spaced
        var opts = new ChunkOptions { MaxChars = 1000, OverlapChars = 100 };
        var chunks = new Chunker(opts).Chunk(Rec(text));

        Assert.True(chunks.Count > 1);
        // Every interior chunk ends on a whole "lorem" word — never a partial fragment like "lor".
        foreach (var c in chunks.Take(chunks.Count - 1))
            Assert.EndsWith("lorem", c.Text.TrimEnd());
    }

    [Fact]
    public void Word_boundary_mode_hard_breaks_a_token_with_no_whitespace()
    {
        // A single 2 500-char token in word-boundary mode: there is no whitespace to snap to,
        // so it falls back to hard boundaries and still covers everything without loss.
        var text = new string('z', 2500);
        var chunks = new Chunker(new ChunkOptions { MaxChars = 1000, OverlapChars = 100 }).Chunk(Rec(text));

        Assert.Equal(3, chunks.Count);
        Assert.Equal(1000, chunks[0].Text.Length);
        Assert.Equal(string.Concat(chunks.Select((c, i) => c.Text.Substring(i == 0 ? 0 : 100))), text);
    }

    [Fact]
    public void Chunks_serialize_to_json_lines()
    {
        var text = new string('y', 1500);
        var chunks = new Chunker(new ChunkOptions { RespectWordBoundaries = false }).Chunk(Rec(text));

        var jsonl = TableWriter.ToJsonLines(chunks);
        Assert.EndsWith("\n", jsonl);
        var lines = jsonl.TrimEnd('\n').Split('\n');
        Assert.Equal(chunks.Count, lines.Length);

        var first = JsonDocument.Parse(lines[0]).RootElement;
        Assert.Equal("C:/x/a.txt", first.GetProperty("path").GetString());
        Assert.Equal(0, first.GetProperty("index").GetInt32());
        Assert.Equal(chunks.Count, first.GetProperty("count").GetInt32());
        Assert.Equal("Jane", first.GetProperty("metadata").GetProperty("Author").GetString());
    }

    [Fact]
    public void Chunk_over_a_record_stream_flattens_in_order()
    {
        var recs = new[] { Rec("first file"), Rec("second file") };
        var chunks = new Chunker().Chunk((IEnumerable<FileRecord>)recs).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("first file", chunks[0].Text);
        Assert.Equal("second file", chunks[1].Text);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(100, 100)]    // overlap == max
    [InlineData(100, 150)]    // overlap > max
    public void Invalid_options_throw(int maxChars, int overlapChars)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Chunker(new ChunkOptions { MaxChars = maxChars, OverlapChars = overlapChars }));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => new Chunker(null!));
        Assert.Throws<ArgumentNullException>(() => new Chunker().Chunk((FileRecord)null!));
        Assert.Throws<ArgumentNullException>(() => new Chunker().Chunk((IEnumerable<FileRecord>)null!).ToList());
        Assert.Throws<ArgumentNullException>(() => TableWriter.ToJsonLines((IEnumerable<Chunk>)null!));
    }

    [Fact]
    public void Paragraphs_mode_prefers_paragraph_and_sentence_breaks()
    {
        var para1 = "First paragraph sentence one. First paragraph sentence two.";
        var para2 = "Second paragraph has more text to split across chunks cleanly.";
        var text = $"{para1}\n\n{para2}";

        var chunks = new Chunker(new ChunkOptions
        {
            Mode = ChunkMode.Paragraphs,
            MaxChars = 75,
            OverlapChars = 25,
        }).Chunk(Rec(text));

        Assert.True(chunks.Count >= 2);
        Assert.EndsWith("\n\n", chunks[0].Text);
    }

    [Fact]
    public void Paragraphs_mode_snaps_to_sentence_when_no_paragraph_break()
    {
        var text = "Alpha sentence ends here. Beta sentence continues with extra words for windowing.";
        var chunks = new Chunker(new ChunkOptions
        {
            Mode = ChunkMode.Paragraphs,
            MaxChars = 50,
            OverlapChars = 25,
        }).Chunk(Rec(text));

        Assert.True(chunks.Count >= 2);
        Assert.Contains("Alpha sentence ends here.", chunks[0].Text);
    }

    [Fact]
    public void MarkdownSections_mode_splits_on_headings_and_sets_section_breadcrumb()
    {
        var md = """
                 Intro text before headings.
                 # Overview
                 General overview content.
                 ## Architecture
                 Detailed architecture notes.
                 # Appendix
                 Final appendix notes.
                 """;

        var rec = Rec(md);
        var chunks = new Chunker(new ChunkOptions
        {
            Mode = ChunkMode.MarkdownSections,
            MaxChars = 200,
            OverlapChars = 20,
        }).Chunk(rec);

        Assert.Equal(4, chunks.Count);
        Assert.False(chunks[0].Metadata.ContainsKey("Section"));
        Assert.Equal("Overview", chunks[1].Metadata["Section"]);
        Assert.Equal("Overview > Architecture", chunks[2].Metadata["Section"]);
        Assert.Equal("Appendix", chunks[3].Metadata["Section"]);
        Assert.False(rec.Metadata.ContainsKey("Section"));   // source record metadata untouched
    }
}
