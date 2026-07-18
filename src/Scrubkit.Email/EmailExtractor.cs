using System.Text;
using System.Text.RegularExpressions;

namespace Scrubkit;

/// <summary>
/// Reads <c>.eml</c> (RFC 5322 / MIME) email files: the useful headers
/// (From / To / Cc / Subject / Date) become metadata and the message body becomes text.
///
/// Best-effort and fully offline — it handles multipart messages, <c>base64</c> and
/// <c>quoted-printable</c> transfer encodings, common charsets, and RFC 2047 encoded-word
/// headers, preferring the <c>text/plain</c> part and falling back to <c>text/html</c>.
/// Attachments and non-text parts are skipped. Register it via
/// <see cref="ReadOptions.Extractors"/>; it references only <c>Scrubkit.Abstractions</c>.
/// </summary>
public sealed class EmailExtractor : IFileExtractor
{
    /// <inheritdoc/>
    public bool CanHandle(string extension) => extension == ".eml";

    /// <inheritdoc/>
    public ExtractedContent Extract(string path)
    {
        // Read the raw bytes as Latin-1 so every byte round-trips 1:1 to a char. This lets us
        // parse structure as text yet recover exact bytes for base64/quoted-printable/charset
        // decoding, with no dependency and no lossy up-front decode.
        var raw = Latin1(File.ReadAllBytes(path));
        var (headerBlock, body) = SplitHeaders(raw);
        var headers = ParseHeaders(headerBlock);

        var meta = BuildMetadata(headers);

        var plain = new StringBuilder();
        var html = new StringBuilder();
        Walk(headers, body, plain, html);
        var text = (plain.Length > 0 ? plain : html).ToString().Trim();

        return new ExtractedContent(meta, text);
    }

    // ---- headers -> metadata ------------------------------------------------

    private static Dictionary<string, string> BuildMetadata(List<KeyValuePair<string, string>> headers)
    {
        var meta = new Dictionary<string, string>();
        Put(meta, "From", DecodeWords(Header(headers, "From")));
        Put(meta, "To", DecodeWords(Header(headers, "To")));
        Put(meta, "Cc", DecodeWords(Header(headers, "Cc")));
        Put(meta, "Subject", DecodeWords(Header(headers, "Subject")));
        Put(meta, "Date", Header(headers, "Date"));
        return meta;
    }

    private static void Put(Dictionary<string, string> meta, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) meta[key] = value!.Trim();
    }

    // ---- MIME tree walk -----------------------------------------------------

    // Depth-first: recurse through multipart containers, decoding each text leaf into the
    // matching sink (plain vs html). The caller prefers the plain sink, falling back to html.
    private static void Walk(List<KeyValuePair<string, string>> headers, string body,
                             StringBuilder plain, StringBuilder html)
    {
        var (media, ct) = ContentType(Header(headers, "Content-Type"));

        if (media.StartsWith("multipart/", StringComparison.Ordinal))
        {
            if (!ct.TryGetValue("boundary", out var boundary) || boundary.Length == 0) return;
            foreach (var part in SplitParts(body, boundary))
            {
                var (ph, pb) = SplitHeaders(part);
                Walk(ParseHeaders(ph), pb, plain, html);
            }
            return;
        }

        if (!media.StartsWith("text/", StringComparison.Ordinal)) return;   // skip images, etc.

        var disposition = Header(headers, "Content-Disposition");
        if (disposition != null &&
            disposition.TrimStart().StartsWith("attachment", StringComparison.OrdinalIgnoreCase))
            return;                                                          // skip text attachments

        var bytes = DecodeTransfer(body, Header(headers, "Content-Transfer-Encoding"));
        var decoded = Decode(bytes, ct.TryGetValue("charset", out var cs) ? cs : null);

        (media == "text/html" ? html : plain).Append(decoded).Append('\n');
    }

    // ---- parsing primitives -------------------------------------------------

    private static (string headers, string body) SplitHeaders(string raw)
    {
        var i = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var sep = 4;
        if (i < 0) { i = raw.IndexOf("\n\n", StringComparison.Ordinal); sep = 2; }
        return i < 0 ? (raw, "") : (raw.Substring(0, i), raw.Substring(i + sep));
    }

    // Split header block into (name, value) pairs, unfolding continuation lines (RFC 5322 folding).
    private static List<KeyValuePair<string, string>> ParseHeaders(string block)
    {
        var result = new List<KeyValuePair<string, string>>();
        string? name = null;
        var value = new StringBuilder();

        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;

            if ((line[0] == ' ' || line[0] == '\t') && name != null)
            {
                value.Append(' ').Append(line.Trim());   // folded continuation
                continue;
            }

            if (name != null) result.Add(new(name, value.ToString()));
            value.Clear();

            var colon = line.IndexOf(':');
            if (colon < 0) { name = null; continue; }
            name = line.Substring(0, colon).Trim();
            value.Append(line.Substring(colon + 1).Trim());
        }
        if (name != null) result.Add(new(name, value.ToString()));
        return result;
    }

    private static string? Header(List<KeyValuePair<string, string>> headers, string name)
    {
        foreach (var h in headers)
            if (string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))
                return h.Value;
        return null;
    }

    // "text/plain; charset=\"utf-8\"" -> ("text/plain", { charset = utf-8 })
    private static (string media, Dictionary<string, string> parms) ContentType(string? raw)
    {
        var parms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return ("text/plain", parms);

        var segments = raw!.Split(';');
        var media = segments[0].Trim().ToLowerInvariant();
        for (var i = 1; i < segments.Length; i++)
        {
            var eq = segments[i].IndexOf('=');
            if (eq < 0) continue;
            var key = segments[i].Substring(0, eq).Trim();
            var val = segments[i].Substring(eq + 1).Trim().Trim('"');
            if (key.Length > 0) parms[key] = val;
        }
        return (media, parms);
    }

    private static IEnumerable<string> SplitParts(string body, string boundary)
    {
        var delim = "--" + boundary;
        var segments = body.Split(new[] { delim }, StringSplitOptions.None);
        for (var i = 1; i < segments.Length; i++)   // segments[0] is the preamble
        {
            var seg = segments[i];
            if (seg.StartsWith("--", StringComparison.Ordinal)) break;   // closing "--boundary--"
            if (seg.StartsWith("\r\n", StringComparison.Ordinal)) seg = seg.Substring(2);
            else if (seg.StartsWith("\n", StringComparison.Ordinal)) seg = seg.Substring(1);
            if (seg.EndsWith("\r\n", StringComparison.Ordinal)) seg = seg.Substring(0, seg.Length - 2);
            else if (seg.EndsWith("\n", StringComparison.Ordinal)) seg = seg.Substring(0, seg.Length - 1);
            yield return seg;
        }
    }

    // ---- transfer + charset decoding ---------------------------------------

    private static byte[] DecodeTransfer(string body, string? encoding)
    {
        switch (encoding?.Trim().ToLowerInvariant())
        {
            case "base64":
                var cleaned = new StringBuilder(body.Length);
                foreach (var c in body) if (!char.IsWhiteSpace(c)) cleaned.Append(c);
                try { return Convert.FromBase64String(cleaned.ToString()); }
                catch (FormatException) { return ToBytes(body); }
            case "quoted-printable":
                return DecodeQuotedPrintable(body);
            default:                                    // 7bit / 8bit / binary / none
                return ToBytes(body);
        }
    }

    private static byte[] DecodeQuotedPrintable(string s)
    {
        var bytes = new List<byte>(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c != '=') { bytes.Add((byte)c); continue; }

            if (i + 1 < s.Length && (s[i + 1] == '\r' || s[i + 1] == '\n'))
            {
                i++;                                    // soft line break: "=" then CRLF/LF
                if (s[i] == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                continue;
            }
            if (i + 2 < s.Length && Hex(s[i + 1], out var hi) && Hex(s[i + 2], out var lo))
            {
                bytes.Add((byte)((hi << 4) | lo));
                i += 2;
            }
            else bytes.Add((byte)c);                    // stray '=' — keep literal
        }
        return bytes.ToArray();
    }

    private static bool Hex(char c, out int value)
    {
        if (c >= '0' && c <= '9') { value = c - '0'; return true; }
        if (c >= 'A' && c <= 'F') { value = c - 'A' + 10; return true; }
        if (c >= 'a' && c <= 'f') { value = c - 'a' + 10; return true; }
        value = 0;
        return false;
    }

    // ---- RFC 2047 encoded words --------------------------------------------

    private static readonly Regex EncodedWord =
        new(@"=\?([^?]+)\?([BbQq])\?([^?]*)\?=", RegexOptions.Compiled);

    private static string? DecodeWords(string? value)
    {
        if (string.IsNullOrEmpty(value) || value!.IndexOf("=?", StringComparison.Ordinal) < 0)
            return value;

        return EncodedWord.Replace(value, m =>
        {
            var charset = m.Groups[1].Value;
            var kind = char.ToUpperInvariant(m.Groups[2].Value[0]);
            var data = m.Groups[3].Value;
            try
            {
                byte[] bytes;
                if (kind == 'B')
                {
                    bytes = Convert.FromBase64String(data);
                }
                else   // 'Q' — like quoted-printable but '_' means space
                {
                    bytes = DecodeQuotedPrintable(data.Replace('_', ' '));
                }
                return Decode(bytes, charset);
            }
            catch (FormatException) { return m.Value; }
        });
    }

    // ---- byte/char helpers --------------------------------------------------

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private static string Latin1(byte[] data)
    {
        var chars = new char[data.Length];
        for (var i = 0; i < data.Length; i++) chars[i] = (char)data[i];
        return new string(chars);
    }

    private static byte[] ToBytes(string s)
    {
        var bytes = new byte[s.Length];
        for (var i = 0; i < s.Length; i++) bytes[i] = (byte)s[i];
        return bytes;
    }

    // Decode bytes for a named charset, staying dependency-free: the charsets email actually
    // uses in practice are handled directly, and anything else falls back to UTF-8.
    private static string Decode(byte[] data, string? charset)
    {
        var name = charset?.Trim().Trim('"').ToLowerInvariant();
        switch (name)
        {
            case null:
            case "":
            case "utf-8":
            case "utf8":
                return Utf8NoBom.GetString(data);
            case "us-ascii":
            case "ascii":
                return Encoding.ASCII.GetString(data);
            case "iso-8859-1":
            case "iso8859-1":
            case "latin1":
            case "windows-1252":
            case "cp1252":
                return Latin1(data);            // close enough for best-effort text
            default:
                try { return Encoding.GetEncoding(name).GetString(data); }
                catch (ArgumentException) { return Utf8NoBom.GetString(data); }
        }
    }
}
