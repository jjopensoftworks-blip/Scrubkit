using System.Text.Json;
using Scrubkit;
using Xunit;

namespace Scrubkit.Tests;

public class TableWriterTests
{
    private static FileRecord Rec(string text = "hello") => new()
    {
        Path = "C:/x/a.txt",
        Name = "a.txt",
        Extension = ".txt",
        Folder = "x",
        SizeBytes = 12,
        Modified = new DateTime(2026, 7, 19, 10, 0, 0),
        TypeBucket = "Text",
        Text = text,
        Metadata = new Dictionary<string, string> { ["Author"] = "Jane" },
        Redactions = new Dictionary<string, int> { ["Email"] = 1 },
        Warnings = new[] { "text-clipped" },
        ContentHash = "abc123",
    };

    [Fact]
    public void Csv_has_header_and_a_row_per_record()
    {
        var lines = TableWriter.ToCsv(new[] { Rec() }).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        Assert.StartsWith("Path,Name,Extension,Folder,SizeBytes,Modified,TypeBucket", lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.Contains("a.txt", lines[1]);
        Assert.Contains("abc123", lines[1]);
    }

    [Fact]
    public void Csv_quotes_fields_with_special_characters()
    {
        var csv = TableWriter.ToCsv(new[] { Rec("has, comma \"and quote\"\nand newline") });
        Assert.Contains("\"has, comma \"\"and quote\"\"\nand newline\"", csv);
    }

    [Fact]
    public void Json_is_valid_and_carries_the_full_record()
    {
        const string tricky = "quote \" back\\ slash \n newline \t tab é";
        var json = TableWriter.ToJson(new[] { Rec(tricky) });

        using var doc = JsonDocument.Parse(json);   // hand-rolled JSON must parse
        var el = doc.RootElement[0];
        Assert.Equal("C:/x/a.txt", el.GetProperty("path").GetString());
        Assert.Equal(tricky, el.GetProperty("text").GetString());
        Assert.Equal("Jane", el.GetProperty("metadata").GetProperty("Author").GetString());
        Assert.Equal(1, el.GetProperty("redactions").GetProperty("Email").GetInt32());
        Assert.Equal("text-clipped", el.GetProperty("warnings")[0].GetString());
        Assert.Equal("abc123", el.GetProperty("contentHash").GetString());
    }

    [Fact]
    public void Json_null_content_hash_serializes_as_null()
    {
        var json = TableWriter.ToJson(new[] { Rec() with { ContentHash = null } });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement[0].GetProperty("contentHash").ValueKind);
    }

    [Fact]
    public void Empty_input_produces_header_only_csv_and_empty_json_array()
    {
        Assert.Equal(
            "Path,Name,Extension,Folder,SizeBytes,Modified,TypeBucket,Text,Warnings,Redactions,ContentHash\r\n",
            TableWriter.ToCsv(Array.Empty<FileRecord>()));
        Assert.Equal("[]", TableWriter.ToJson(Array.Empty<FileRecord>()));
    }

    [Fact]
    public void Null_records_throws()
    {
        Assert.Throws<ArgumentNullException>(() => TableWriter.ToCsv(null!));
        Assert.Throws<ArgumentNullException>(() => TableWriter.ToJson(null!));
    }
}
