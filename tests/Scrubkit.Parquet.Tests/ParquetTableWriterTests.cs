using Parquet.Serialization;
using Scrubkit;
using Xunit;

namespace Scrubkit.Parquet.Tests;

public class ParquetTableWriterTests
{
    private static FileRecord Rec(string name, string text, string? hash) => new()
    {
        Path = "C:/docs/" + name,
        Name = name,
        Extension = ".txt",
        Folder = "docs",
        SizeBytes = text.Length,
        Modified = new DateTime(2026, 7, 19, 8, 30, 0, DateTimeKind.Utc),
        TypeBucket = "Text",
        Text = text,
        Metadata = new Dictionary<string, string>(),
        Redactions = new Dictionary<string, int> { ["Email"] = 1 },
        Warnings = new[] { "text-clipped" },
        ContentHash = hash,
    };

    [Fact]
    public async Task Round_trips_records_through_parquet()
    {
        var records = new[]
        {
            Rec("a.txt", "hello world", "abc123"),
            Rec("b.txt", "second file", null),
        };

        using var stream = new MemoryStream();
        await ParquetTableWriter.WriteAsync(records, stream);

        stream.Position = 0;
        var rows = (await ParquetSerializer.DeserializeAsync<Row>(stream)).Data;

        Assert.Equal(2, rows.Count);
        Assert.Equal("a.txt", rows[0].Name);
        Assert.Equal("hello world", rows[0].Text);
        Assert.Equal("Text", rows[0].TypeBucket);
        Assert.Equal("Email:1", rows[0].Redactions);
        Assert.Equal("text-clipped", rows[0].Warnings);
        Assert.Equal("abc123", rows[0].ContentHash);
        Assert.Null(rows[1].ContentHash);          // null content hash round-trips
    }

    [Fact]
    public async Task Empty_input_writes_a_valid_parquet_file()
    {
        using var stream = new MemoryStream();
        await ParquetTableWriter.WriteAsync(Array.Empty<FileRecord>(), stream);

        stream.Position = 0;
        var rows = (await ParquetSerializer.DeserializeAsync<Row>(stream)).Data;
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Null_arguments_throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ParquetTableWriter.WriteAsync(null!, new MemoryStream()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ParquetTableWriter.WriteAsync(Array.Empty<FileRecord>(), null!));
    }

    [Fact]
    public async Task Modified_round_trips_as_the_same_utc_instant()
    {
        var rec = Rec("a.txt", "hi", null);   // Modified = 2026-07-19T08:30:00Z

        using var stream = new MemoryStream();
        await ParquetTableWriter.WriteAsync(new[] { rec }, stream);
        stream.Position = 0;
        var row = (await ParquetSerializer.DeserializeAsync<Row>(stream)).Data[0];

        // Parquet stores an instant; it comes back as the same moment in UTC.
        Assert.Equal(rec.Modified, row.Modified.ToUniversalTime());
    }

    // Mirrors the writer's column names so the deserializer can map them.
    private sealed class Row
    {
        public string Name { get; set; } = "";
        public DateTime Modified { get; set; }
        public string Text { get; set; } = "";
        public string TypeBucket { get; set; } = "";
        public string Warnings { get; set; } = "";
        public string Redactions { get; set; } = "";
        public string? ContentHash { get; set; }
    }
}
