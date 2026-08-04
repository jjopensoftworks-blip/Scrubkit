// Copyright © 2026 jjopensoftworks-blip

using System.Text;

namespace Scrubkit;

/// <summary>
/// Best-effort text from a PowerPoint 97-2003 (<c>.ppt</c>) presentation. Text lives in the
/// "PowerPoint Document" stream as a tree of records; the slide/notes text is carried by
/// <c>TextBytesAtom</c> (ANSI) and <c>TextCharsAtom</c> (UTF-16) records. Walk the record tree
/// and collect those. This is the swappable per-format reader behind
/// <see cref="LegacyOfficeExtractor"/>.
/// </summary>
internal static class PptText
{
    private const string StreamName = "PowerPoint Document";
    private const ushort TextCharsAtom = 0x0FA0;   // 4000 — UTF-16LE text
    private const ushort TextBytesAtom = 0x0FA8;   // 4008 — ANSI/Latin-1 text

    public static string Read(CompoundFile cf)
    {
        if (!cf.TryRead(StreamName, out var d)) return "";
        var sb = new StringBuilder();
        Walk(d, 0, d.Length, sb);
        return sb.ToString().Trim();
    }

    // A record is: recVerInstance(2) recType(2) recLen(4) then body. When the low nibble of
    // recVerInstance is 0xF the record is a container — recurse into its children.
    private static void Walk(byte[] d, int start, int end, StringBuilder sb)
    {
        var i = start;
        while (i + 8 <= end)
        {
            var verInstance = BitConverter.ToUInt16(d, i);
            var type = BitConverter.ToUInt16(d, i + 2);
            var len = (int)BitConverter.ToUInt32(d, i + 4);
            var body = i + 8;
            if (len < 0 || body + len > end) break;

            if ((verInstance & 0x000F) == 0x000F)
                Walk(d, body, body + len, sb);                       // container
            else if (type == TextBytesAtom)
                Append(sb, Latin1(d, body, len));
            else if (type == TextCharsAtom)
                Append(sb, Encoding.Unicode.GetString(d, body, len));

            i = body + len;
        }
    }

    private static void Append(StringBuilder sb, string text)
    {
        // PowerPoint uses \r for paragraph breaks and \v for soft line breaks.
        text = text.Replace('\v', '\n').Replace('\r', '\n').Replace("\0", "");
        if (text.Length == 0) return;
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(text);
    }

    private static string Latin1(byte[] d, int start, int len)
    {
        var chars = new char[len];
        for (var i = 0; i < len; i++) chars[i] = (char)d[start + i];
        return new string(chars);
    }
}
