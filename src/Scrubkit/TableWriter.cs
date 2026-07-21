using System.Globalization;
using System.Text;

namespace Scrubkit;

/// <summary>
/// Serializes a table of <see cref="FileRecord"/>s to CSV, JSON, or JSON Lines for ingestion /
/// indexing pipelines. Zero-dependency and fully offline, like the rest of Scrubkit. CSV is a
/// flat summary (RFC 4180 quoting); JSON and JSON Lines carry the full record including text,
/// metadata, redaction counts, warnings, and content hash. JSON Lines (one object per line) is
/// the streaming-friendly default for embedding / RAG and log pipelines.
///
/// Timestamps default to UTC (<c>...Z</c>). Pass <c>utc: false</c> to emit machine-local
/// time instead — always with an explicit offset (<c>...+05:30</c>) so the value stays
/// unambiguous.
/// </summary>
public static class TableWriter
{
    private static readonly char[] CsvSpecial = { ',', '"', '\n', '\r' };

    private static readonly string[] CsvHeaders =
    {
        "Path", "Name", "Extension", "Folder", "SizeBytes", "Modified", "TypeBucket",
        "Text", "Warnings", "Redactions", "ContentHash",
    };

    /// <summary>
    /// Returns the records as a CSV string (with a header row). Timestamps are UTC unless
    /// <paramref name="utc"/> is <c>false</c>, in which case they are machine-local with an
    /// explicit offset.
    /// </summary>
    public static string ToCsv(IEnumerable<FileRecord> records, bool utc = true)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteCsv(records, writer, utc);
        return writer.ToString();
    }

    /// <summary>Writes the records as CSV (with a header row) to <paramref name="writer"/>.</summary>
    public static void WriteCsv(IEnumerable<FileRecord> records, TextWriter writer, bool utc = true)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        writer.Write(string.Join(",", CsvHeaders));
        writer.Write("\r\n");

        foreach (var r in records)
        {
            var row = new[]
            {
                r.Path,
                r.Name,
                r.Extension,
                r.Folder,
                r.SizeBytes.ToString(CultureInfo.InvariantCulture),
                Iso(r.Modified, utc),
                r.TypeBucket,
                r.Text,
                Join(r.Warnings),
                JoinPairs(r.Redactions),
                r.ContentHash ?? "",
            };
            writer.Write(string.Join(",", row.Select(CsvField)));
            writer.Write("\r\n");
        }
    }

    // ISO-8601 with an explicit zone marker so consumers never guess: 'Z' for UTC, or a
    // numeric offset (e.g. +05:30) for machine-local time.
    private static string Iso(DateTime value, bool utc) =>
        utc
            ? value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : value.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string Join(IReadOnlyList<string>? items) =>
        items is null ? "" : string.Join(";", items);

    private static string JoinPairs(IReadOnlyDictionary<string, int>? map) =>
        map is null ? "" : string.Join(";", map.Select(kv => $"{kv.Key}:{kv.Value}"));

    /// <summary>
    /// Returns the records as a JSON array string. Timestamps are UTC unless
    /// <paramref name="utc"/> is <c>false</c>, in which case they are machine-local with an
    /// explicit offset.
    /// </summary>
    public static string ToJson(IEnumerable<FileRecord> records, bool utc = true)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteJson(records, writer, utc);
        return writer.ToString();
    }

    /// <summary>Writes the records as a JSON array to <paramref name="writer"/>.</summary>
    public static void WriteJson(IEnumerable<FileRecord> records, TextWriter writer, bool utc = true)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        writer.Write('[');
        var first = true;
        foreach (var r in records)
        {
            if (!first) writer.Write(',');
            first = false;
            writer.Write(RecordToJson(r, utc));
        }
        writer.Write(']');
    }

    /// <summary>
    /// Returns the records as JSON Lines (<see href="https://jsonlines.org/">NDJSON</see>) — one
    /// JSON object per line, separated by <c>\n</c>, with a trailing newline. This is the default
    /// shape for streaming into embedding / RAG and log pipelines, where each line is parsed
    /// independently. Timestamps are UTC unless <paramref name="utc"/> is <c>false</c>.
    /// </summary>
    public static string ToJsonLines(IEnumerable<FileRecord> records, bool utc = true)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteJsonLines(records, writer, utc);
        return writer.ToString();
    }

    /// <summary>
    /// Writes the records as JSON Lines (one object per line, <c>\n</c>-separated, trailing
    /// newline) to <paramref name="writer"/>.
    /// </summary>
    public static void WriteJsonLines(IEnumerable<FileRecord> records, TextWriter writer, bool utc = true)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        foreach (var r in records)
        {
            writer.Write(RecordToJson(r, utc));
            writer.Write('\n');
        }
    }

    /// <summary>
    /// Returns the chunks as JSON Lines — one JSON object per line — ready to stream into an
    /// embedding / vector-index pipeline. Each line carries the chunk text plus its source
    /// path, name, type, position (<c>index</c>/<c>count</c>/<c>startOffset</c>), and metadata.
    /// </summary>
    public static string ToJsonLines(IEnumerable<Chunk> chunks)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteJsonLines(chunks, writer);
        return writer.ToString();
    }

    /// <summary>Writes the chunks as JSON Lines (one object per line) to <paramref name="writer"/>.</summary>
    public static void WriteJsonLines(IEnumerable<Chunk> chunks, TextWriter writer)
    {
        if (chunks is null) throw new ArgumentNullException(nameof(chunks));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        foreach (var c in chunks)
        {
            writer.Write(ChunkToJson(c));
            writer.Write('\n');
        }
    }

    private static string ChunkToJson(Chunk c)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"path\":").Append(JsonString(c.Path)).Append(',');
        sb.Append("\"name\":").Append(JsonString(c.Name)).Append(',');
        sb.Append("\"typeBucket\":").Append(JsonString(c.TypeBucket)).Append(',');
        sb.Append("\"index\":").Append(c.Index.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"count\":").Append(c.Count.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"startOffset\":").Append(c.StartOffset.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"text\":").Append(JsonString(c.Text)).Append(',');
        sb.Append("\"metadata\":").Append(JsonStringMap(c.Metadata));
        sb.Append('}');
        return sb.ToString();
    }

    private static string RecordToJson(FileRecord r, bool utc)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"path\":").Append(JsonString(r.Path)).Append(',');
        sb.Append("\"name\":").Append(JsonString(r.Name)).Append(',');
        sb.Append("\"extension\":").Append(JsonString(r.Extension)).Append(',');
        sb.Append("\"folder\":").Append(JsonString(r.Folder)).Append(',');
        sb.Append("\"sizeBytes\":").Append(r.SizeBytes.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"modified\":").Append(JsonString(Iso(r.Modified, utc))).Append(',');
        sb.Append("\"typeBucket\":").Append(JsonString(r.TypeBucket)).Append(',');
        sb.Append("\"text\":").Append(JsonString(r.Text)).Append(',');
        sb.Append("\"metadata\":").Append(JsonStringMap(r.Metadata)).Append(',');
        sb.Append("\"redactions\":").Append(JsonIntMap(r.Redactions)).Append(',');
        sb.Append("\"warnings\":").Append(JsonStringArray(r.Warnings)).Append(',');
        sb.Append("\"contentHash\":").Append(r.ContentHash is null ? "null" : JsonString(r.ContentHash));
        sb.Append('}');
        return sb.ToString();
    }

    private static string JsonStringMap(IReadOnlyDictionary<string, string>? map)
    {
        if (map is null) return "{}";
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var kv in map)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(JsonString(kv.Key)).Append(':').Append(JsonString(kv.Value));
        }
        return sb.Append('}').ToString();
    }

    private static string JsonIntMap(IReadOnlyDictionary<string, int>? map)
    {
        if (map is null) return "{}";
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var kv in map)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(JsonString(kv.Key)).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
        }
        return sb.Append('}').ToString();
    }

    private static string JsonStringArray(IReadOnlyList<string>? items)
    {
        if (items is null) return "[]";
        var sb = new StringBuilder("[");
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonString(items[i]));
        }
        return sb.Append(']').ToString();
    }

    private static string CsvField(string? value)
    {
        value ??= "";
        return value.IndexOfAny(CsvSpecial) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static string JsonString(string? value)
    {
        if (value is null) return "null";
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
