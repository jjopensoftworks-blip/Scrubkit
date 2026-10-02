// Copyright © 2026 jjopensoftworks-blip

using System;
using System.Collections.Generic;
using Xunit;

namespace Scrubkit.Tests;

public class ChunkerTheoryTests
{
    [Theory]
    [InlineData(100, 20)]
    [InlineData(200, 50)]
    [InlineData(50, 0)]
    [InlineData(500, 100)]
    [InlineData(1000, 200)]
    public void Chunk_VariousSizes_SplitsTextCorrectly(int chunkSize, int overlap)
    {
        var text = new string('A', 1250);
        var record = new FileRecord
        {
            Path = "test.txt",
            Name = "test.txt",
            Extension = ".txt",
            SizeBytes = 1250,
            Text = text
        };
        var chunker = new Chunker(new ChunkOptions { MaxChars = chunkSize, OverlapChars = overlap });

        var chunks = chunker.Chunk(record);

        Assert.NotEmpty(chunks);
        foreach (var c in chunks)
        {
            Assert.True(c.Text.Length <= chunkSize);
            Assert.Equal("test.txt", c.Path);
        }
    }

    [Theory]
    [InlineData("Line 1\nLine 2\nLine 3\nLine 4\nLine 5", 20, 5)]
    [InlineData("Paragraph 1.\n\nParagraph 2.\n\nParagraph 3.", 30, 10)]
    [InlineData("Short text", 100, 10)]
    [InlineData("", 100, 10)]
    [InlineData("Word1 Word2 Word3 Word4 Word5 Word6 Word7 Word8 Word9 Word10", 25, 5)]
    public void Chunk_TextStructures_GeneratesValidChunks(string text, int chunkSize, int overlap)
    {
        var record = new FileRecord
        {
            Path = "doc.txt",
            Name = "doc.txt",
            Extension = ".txt",
            SizeBytes = text.Length,
            Text = text
        };
        var chunker = new Chunker(new ChunkOptions { MaxChars = chunkSize, OverlapChars = overlap });

        var chunks = chunker.Chunk(record);

        if (string.IsNullOrEmpty(text))
        {
            Assert.Empty(chunks);
        }
        else
        {
            Assert.NotEmpty(chunks);
            foreach (var c in chunks)
            {
                Assert.NotNull(c.Path);
                Assert.True(c.Index >= 0);
            }
        }
    }
}
