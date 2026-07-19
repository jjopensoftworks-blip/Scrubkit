using Parquet.Serialization;

namespace Scrubkit;

/// <summary>
/// Writes a table of <see cref="FileRecord"/>s to Apache Parquet (columnar) for data-lake and
/// analytics ingestion. A companion to the core's zero-dependency <c>TableWriter</c> (CSV /
/// JSON); this one takes a dependency on Parquet.Net, so it ships as a separate package.
///
/// Columns: <c>Path</c>, <c>Name</c>, <c>Extension</c>, <c>Folder</c>, <c>SizeBytes</c>,
/// <c>Modified</c>, <c>TypeBucket</c>, <c>Text</c>, <c>Warnings</c> (";"-joined),
/// <c>Redactions</c> ("cat:count;"-joined), and <c>ContentHash</c>.
/// </summary>
public static class ParquetTableWriter
{
    /// <summary>Writes the records as a Parquet file to <paramref name="stream"/>.</summary>
    public static async Task WriteAsync(
        IEnumerable<FileRecord> records, Stream stream, CancellationToken cancellationToken = default)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        var rows = records.Select(ToRow).ToList();
        await ParquetSerializer.SerializeAsync(rows, stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Writes the records as a Parquet file at <paramref name="filePath"/>.</summary>
    public static async Task WriteFileAsync(
        IEnumerable<FileRecord> records, string filePath, CancellationToken cancellationToken = default)
    {
        using var fileStream = File.Create(filePath);
        await WriteAsync(records, fileStream, cancellationToken).ConfigureAwait(false);
    }

    private static Row ToRow(FileRecord r) => new()
    {
        Path = r.Path,
        Name = r.Name,
        Extension = r.Extension,
        Folder = r.Folder,
        SizeBytes = r.SizeBytes,
        Modified = r.Modified.ToUniversalTime(),
        TypeBucket = r.TypeBucket,
        Text = r.Text,
        Warnings = r.Warnings is null ? "" : string.Join(";", r.Warnings),
        Redactions = r.Redactions is null ? "" : string.Join(";", r.Redactions.Select(kv => $"{kv.Key}:{kv.Value}")),
        ContentHash = r.ContentHash,
    };

    // Flat, Parquet-friendly projection of FileRecord — dictionaries and lists flattened to
    // strings so every property maps to a scalar column.
    private sealed class Row
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string Extension { get; set; } = "";
        public string Folder { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime Modified { get; set; }
        public string TypeBucket { get; set; } = "";
        public string Text { get; set; } = "";
        public string Warnings { get; set; } = "";
        public string Redactions { get; set; } = "";
        public string? ContentHash { get; set; }
    }
}
