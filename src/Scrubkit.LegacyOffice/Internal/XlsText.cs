// Copyright © 2026 jjopensoftworks-blip

using System.Text;

namespace Scrubkit;

/// <summary>
/// Best-effort text from an Excel 97-2003 (<c>.xls</c>) workbook. Cell strings live in the
/// <c>Workbook</c> stream as BIFF records: unique strings in the shared string table
/// (<c>SST</c>, which can span <c>CONTINUE</c> records) and cells that reference them
/// (<c>LABELSST</c>), plus the occasional inline <c>LABEL</c>. This collects those. The
/// swappable per-format reader behind <see cref="LegacyOfficeExtractor"/>.
/// </summary>
internal static class XlsText
{
    private const int Sst = 0x00FC;
    private const int Continue = 0x003C;
    private const int LabelSst = 0x00FD;
    private const int Label = 0x0204;

    public static string Read(CompoundFile cf)
    {
        if (!cf.TryRead("Workbook", out var d) && !cf.TryRead("Book", out d)) return "";

        var sst = ReadSharedStrings(d);

        var sb = new StringBuilder();
        var i = 0;
        while (i + 4 <= d.Length)
        {
            var type = BitConverter.ToUInt16(d, i);
            var len = BitConverter.ToUInt16(d, i + 2);
            var body = i + 4;
            if (body + len > d.Length) break;

            if (type == LabelSst && len >= 10)
            {
                var isst = (int)BitConverter.ToUInt32(d, body + 6);
                if (isst >= 0 && isst < sst.Count) Append(sb, sst[isst]);
            }
            else if (type == Label && len >= 8)
            {
                Append(sb, InlineString(d, body + 6, body + len));
            }

            i = body + len;
        }
        return sb.ToString().Trim();
    }

    // ---- shared string table (handling CONTINUE splits) ----

    private static List<string> ReadSharedStrings(byte[] d)
    {
        var strings = new List<string>();
        var i = 0;
        while (i + 4 <= d.Length)
        {
            var type = BitConverter.ToUInt16(d, i);
            var len = BitConverter.ToUInt16(d, i + 2);
            var body = i + 4;
            if (body + len > d.Length) break;

            if (type == Sst)
            {
                // Flatten the SST body + every following CONTINUE body into one buffer, recording
                // where each continuation begins — a string's characters can split across that
                // boundary, and the continuation restarts with a fresh option-flags byte.
                var buf = new MemoryStream();
                var boundaries = new HashSet<int>();
                buf.Write(d, body, len);
                var j = body + len;
                while (j + 4 <= d.Length && BitConverter.ToUInt16(d, j) == Continue)
                {
                    var clen = BitConverter.ToUInt16(d, j + 2);
                    if (j + 4 + clen > d.Length) break;
                    boundaries.Add((int)buf.Length);
                    buf.Write(d, j + 4, clen);
                    j += 4 + clen;
                }
                ParseSst(buf.ToArray(), boundaries, strings);
                break;   // one SST per workbook
            }
            i = body + len;
        }
        return strings;
    }

    private static void ParseSst(byte[] b, HashSet<int> boundaries, List<string> strings)
    {
        if (b.Length < 8) return;
        var cUnique = (int)BitConverter.ToUInt32(b, 4);   // after cstTotal(4)
        var pos = 8;
        for (var n = 0; n < cUnique && pos + 3 <= b.Length; n++)
            strings.Add(ReadRichString(b, boundaries, ref pos));
    }

    // XLUnicodeRichExtendedString: cch(2) grbit(1) [cRun(2)] [cbExtRst(4)] chars [runs] [extrst].
    private static string ReadRichString(byte[] b, HashSet<int> boundaries, ref int pos)
    {
        var cch = BitConverter.ToUInt16(b, pos); pos += 2;
        var grbit = b[pos++];
        var high = (grbit & 0x01) != 0;
        var cRun = (grbit & 0x08) != 0 ? BitConverter.ToUInt16(b, pos) : 0; if ((grbit & 0x08) != 0) pos += 2;
        var cbExt = (grbit & 0x04) != 0 ? (int)BitConverter.ToUInt32(b, pos) : 0; if ((grbit & 0x04) != 0) pos += 4;

        var sb = new StringBuilder(cch);
        for (var i = 0; i < cch; i++)
        {
            if (boundaries.Contains(pos) && pos < b.Length)   // char array continued in next record
                high = (b[pos++] & 0x01) != 0;                // fresh option-flags byte
            if (high)
            {
                if (pos + 2 > b.Length) break;
                sb.Append((char)(b[pos] | (b[pos + 1] << 8))); pos += 2;
            }
            else
            {
                if (pos + 1 > b.Length) break;
                sb.Append((char)b[pos]); pos += 1;
            }
        }
        pos += cRun * 4 + cbExt;   // skip formatting runs + phonetic data (no text)
        return sb.ToString();
    }

    // A plain (non-shared) XLUnicodeString cell value: cch(2) grbit(1) chars.
    private static string InlineString(byte[] d, int start, int end)
    {
        if (start + 3 > end) return "";
        var cch = BitConverter.ToUInt16(d, start);
        var high = (d[start + 2] & 0x01) != 0;
        var pos = start + 3;
        var sb = new StringBuilder(cch);
        for (var i = 0; i < cch; i++)
        {
            if (high) { if (pos + 2 > end) break; sb.Append((char)(d[pos] | (d[pos + 1] << 8))); pos += 2; }
            else { if (pos + 1 > end) break; sb.Append((char)d[pos]); pos += 1; }
        }
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (sb.Length > 0) sb.Append('\n');
        sb.Append(text);
    }
}
