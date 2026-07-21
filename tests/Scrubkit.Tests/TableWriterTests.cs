using System.Globalization;
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
    public void Json_escapes_every_control_character()
    {
        // Backspace, form-feed, carriage-return and a bare control char (U+0001) each
        // hit a distinct escape arm in the hand-rolled writer.
        var control = "a" + (char)0x08 + "b" + (char)0x0c + "c" + (char)0x0d + "d" + (char)0x01 + "e";
        var json = TableWriter.ToJson(new[] { Rec(control) });

        Assert.Contains("\\b", json);
        Assert.Contains("\\f", json);
        Assert.Contains("\\r", json);
        Assert.Contains("\\u0001", json);

        using var doc = JsonDocument.Parse(json);   // and it must still round-trip
        Assert.Equal(control, doc.RootElement[0].GetProperty("text").GetString());
    }

    [Fact]
    public void Local_time_output_carries_an_explicit_offset_and_same_instant()
    {
        var rec = Rec() with { Modified = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc) };
        var expectedOffset = TimeZoneInfo.Local.GetUtcOffset(rec.Modified);

        var json = TableWriter.ToJson(new[] { rec }, utc: false);
        var modified = JsonDocument.Parse(json).RootElement[0].GetProperty("modified").GetString()!;
        var parsed = DateTimeOffset.Parse(modified, CultureInfo.InvariantCulture);

        Assert.Equal(rec.Modified, parsed.UtcDateTime);     // same instant, losslessly
        Assert.Equal(expectedOffset, parsed.Offset);        // tagged with the local offset

        // CSV carries the same offset-tagged value.
        var csvModified = TableWriter.ToCsv(new[] { rec }, utc: false)
            .Replace("\r\n", "\n").TrimEnd('\n').Split('\n')[1].Split(',')[5];
        Assert.Equal(rec.Modified, DateTimeOffset.Parse(csvModified, CultureInfo.InvariantCulture).UtcDateTime);
    }

    [Fact]
    public void Utc_output_is_the_default_and_ends_with_z()
    {
        var rec = Rec() with { Modified = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc) };
        var modified = JsonDocument.Parse(TableWriter.ToJson(new[] { rec }))
            .RootElement[0].GetProperty("modified").GetString()!;
        Assert.Equal("2026-07-19T10:00:00Z", modified);
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
        Assert.Equal("", TableWriter.ToJsonLines(Array.Empty<FileRecord>()));
    }

    [Fact]
    public void JsonLines_writes_one_parseable_object_per_line()
    {
        const string tricky = "line1 \" quote \n embedded newline \t tab";
        var jsonl = TableWriter.ToJsonLines(new[] { Rec("first"), Rec(tricky) });

        // Trailing newline, and each embedded newline was escaped — so line count is exactly N.
        Assert.EndsWith("\n", jsonl);
        var lines = jsonl.TrimEnd('\n').Split('\n');
        Assert.Equal(2, lines.Length);

        // Each line is a standalone JSON object carrying the full record.
        var first = JsonDocument.Parse(lines[0]).RootElement;
        Assert.Equal("first", first.GetProperty("text").GetString());
        Assert.Equal("Jane", first.GetProperty("metadata").GetProperty("Author").GetString());

        var second = JsonDocument.Parse(lines[1]).RootElement;
        Assert.Equal(tricky, second.GetProperty("text").GetString());
    }

    [Fact]
    public void JsonLines_honours_the_utc_flag()
    {
        var rec = Rec() with { Modified = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc) };

        var utcLine = TableWriter.ToJsonLines(new[] { rec }).TrimEnd('\n');
        Assert.Equal("2026-07-19T10:00:00Z", JsonDocument.Parse(utcLine).RootElement.GetProperty("modified").GetString());

        var localLine = TableWriter.ToJsonLines(new[] { rec }, utc: false).TrimEnd('\n');
        var parsed = DateTimeOffset.Parse(
            JsonDocument.Parse(localLine).RootElement.GetProperty("modified").GetString()!,
            CultureInfo.InvariantCulture);
        Assert.Equal(rec.Modified, parsed.UtcDateTime);
    }

    [Fact]
    public void Null_records_throws()
    {
        Assert.Throws<ArgumentNullException>(() => TableWriter.ToCsv(null!));
        Assert.Throws<ArgumentNullException>(() => TableWriter.ToJson(null!));
        Assert.Throws<ArgumentNullException>(() => TableWriter.ToJsonLines(null!));
    }
}
