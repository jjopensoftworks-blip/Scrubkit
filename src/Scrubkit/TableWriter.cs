using System.Globalization;
using System.Text;

namespace Scrubkit;

/// <summary>
/// Serializes a table of <see cref="FileRecord"/>s to CSV or JSON for ingestion / indexing
/// pipelines. Zero-dependency and fully offline, like the rest of Scrubkit. CSV is a flat
/// summary (RFC 4180 quoting); JSON carries the full record including text, metadata,
/// redaction counts, warnings, and content hash.
/// </summary>
public static class TableWriter
{
    private static readonly char[] CsvSpecial = { ',', '"', '\n', '\r' };

    private static readonly string[] CsvHeaders =
    {
        "Path", "Name", "Extension", "Folder", "SizeBytes", "Modified", "TypeBucket",
        "Text", "Warnings", "Redactions", "ContentHash",
    };

    /// <summary>Returns the records as a CSV string (with a header row).</summary>
    public static string ToCsv(IEnumerable<FileRecord> records)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteCsv(records, writer);
        return writer.ToString();
    }

    /// <summary>Writes the records as CSV (with a header row) to <paramref name="writer"/>.</summary>
    public static void WriteCsv(IEnumerable<FileRecord> records, TextWriter writer)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        writer.Write(string.Join(",", CsvHeaders));
        writer.Write("\r\n");

        foreach (var r in records)
        {
            var redactions = string.Join(";", r.Redactions.Select(kv => $"{kv.Key}:{kv.Value}"));
            var row = new[]
            {
                r.Path,
                r.Name,
                r.Extension,
                r.Folder,
                r.SizeBytes.ToString(CultureInfo.InvariantCulture),
                r.Modified.ToString("s", CultureInfo.InvariantCulture),
                r.TypeBucket,
                r.Text,
                string.Join(";", r.Warnings),
                redactions,
                r.ContentHash ?? "",
            };
            writer.Write(string.Join(",", row.Select(CsvField)));
            writer.Write("\r\n");
        }
    }

    /// <summary>Returns the records as a JSON array string.</summary>
    public static string ToJson(IEnumerable<FileRecord> records)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteJson(records, writer);
        return writer.ToString();
    }

    /// <summary>Writes the records as a JSON array to <paramref name="writer"/>.</summary>
    public static void WriteJson(IEnumerable<FileRecord> records, TextWriter writer)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        writer.Write('[');
        var first = true;
        foreach (var r in records)
        {
            if (!first) writer.Write(',');
            first = false;
            writer.Write(RecordToJson(r));
        }
        writer.Write(']');
    }

    private static string RecordToJson(FileRecord r)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"path\":").Append(JsonString(r.Path)).Append(',');
        sb.Append("\"name\":").Append(JsonString(r.Name)).Append(',');
        sb.Append("\"extension\":").Append(JsonString(r.Extension)).Append(',');
        sb.Append("\"folder\":").Append(JsonString(r.Folder)).Append(',');
        sb.Append("\"sizeBytes\":").Append(r.SizeBytes.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"modified\":").Append(JsonString(r.Modified.ToString("s", CultureInfo.InvariantCulture))).Append(',');
        sb.Append("\"typeBucket\":").Append(JsonString(r.TypeBucket)).Append(',');
        sb.Append("\"text\":").Append(JsonString(r.Text)).Append(',');
        sb.Append("\"metadata\":").Append(JsonStringMap(r.Metadata)).Append(',');
        sb.Append("\"redactions\":").Append(JsonIntMap(r.Redactions)).Append(',');
        sb.Append("\"warnings\":").Append(JsonStringArray(r.Warnings)).Append(',');
        sb.Append("\"contentHash\":").Append(r.ContentHash is null ? "null" : JsonString(r.ContentHash));
        sb.Append('}');
        return sb.ToString();
    }

    private static string JsonStringMap(IReadOnlyDictionary<string, string> map)
    {
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

    private static string JsonIntMap(IReadOnlyDictionary<string, int> map)
    {
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

    private static string JsonStringArray(IReadOnlyList<string> items)
    {
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
