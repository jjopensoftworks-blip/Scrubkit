// Copyright © 2026 jjopensoftworks-blip

using System;
using System.Collections.Generic;
using Xunit;

namespace Scrubkit.Tests;

public class TableWriterTheoryTests
{
    [Theory]
    [InlineData("normal text", "normal text")]
    [InlineData("text with \"quotes\"", "\"text with \"\"quotes\"\"\"")]
    [InlineData("text, with, commas", "\"text, with, commas\"")]
    [InlineData("text with\nnewline", "\"text with\nnewline\"")]
    [InlineData("text with\r\nCRLF", "\"text with\r\nCRLF\"")]
    public void TableWriter_CsvEscaping_EscapesSpecialCharacters(string input, string expectedSubstring)
    {
        var record = new FileRecord
        {
            Path = "sample.txt",
            Name = "sample.txt",
            Extension = ".txt",
            SizeBytes = input.Length,
            Text = input
        };
        var csv = TableWriter.ToCsv(new[] { record });

        Assert.Contains(expectedSubstring, csv);
    }

    [Theory]
    [InlineData("key1", "val1")]
    [InlineData("author", "Jane Doe")]
    [InlineData("title", "Report 2026")]
    [InlineData("category", "Finance")]
    [InlineData("status", "Approved")]
    public void TableWriter_JsonMetadata_SerializesMetadata(string key, string val)
    {
        var metadata = new Dictionary<string, string> { { key, val } };
        var record = new FileRecord
        {
            Path = "sample.txt",
            Name = "sample.txt",
            Extension = ".txt",
            SizeBytes = 100,
            ContentHash = "abc123hash",
            Text = "Sample content",
            Metadata = metadata
        };
        
        var json = TableWriter.ToJson(new[] { record });

        Assert.Contains(key, json);
        Assert.Contains(val, json);
    }
}
