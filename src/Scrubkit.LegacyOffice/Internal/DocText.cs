using System.Text;

namespace Scrubkit;

/// <summary>
/// Best-effort text from a Word 97-2003 (<c>.doc</c>) document. The text is in the
/// <c>WordDocument</c> stream, but its layout is described by the FIB (File Information Block)
/// and a <b>piece table</b> (CLX) stored in the <c>0Table</c>/<c>1Table</c> stream: each piece
/// says where its characters live and whether they're 8-bit (CP1252) or 16-bit (UTF-16). This
/// walks the piece table and stitches the main document text back together. The swappable
/// per-format reader behind <see cref="LegacyOfficeExtractor"/>.
/// </summary>
internal static class DocText
{
    public static string Read(CompoundFile cf)
    {
        if (!cf.TryRead("WordDocument", out var wd) || wd.Length < 0x60) return "";
        if (BitConverter.ToUInt16(wd, 0) != 0xA5EC) return "";   // wIdent

        // fWhichTblStm (bit 0x0200 of the FibBase flags) picks the table stream.
        var flags = BitConverter.ToUInt16(wd, 0x0A);
        var tableName = (flags & 0x0200) != 0 ? "1Table" : "0Table";
        if (!cf.TryRead(tableName, out var tbl) &&
            !cf.TryRead((flags & 0x0200) != 0 ? "0Table" : "1Table", out tbl))
            return "";

        // Walk the FIB's variable structure to find fcClx/lcbClx (the piece-table location) and
        // ccpText (the length of the main document text).
        var csw = BitConverter.ToUInt16(wd, 32);
        var fibRgLw = 32 + 2 + csw * 2 + 2;                       // after csw + fibRgW + cslw
        if (fibRgLw + 16 > wd.Length) return "";
        var cslw = BitConverter.ToUInt16(wd, fibRgLw - 2);
        var ccpText = (int)BitConverter.ToUInt32(wd, fibRgLw + 12);   // fibRgLw[3]
        var fibRgFcLcb = fibRgLw + cslw * 4 + 2;                  // after fibRgLw + cbRgFcLcb
        var clx = fibRgFcLcb + 33 * 8;                            // fcClx/lcbClx is pair 33
        if (clx + 8 > wd.Length) return "";
        var fcClx = (int)BitConverter.ToUInt32(wd, clx);
        var lcbClx = (int)BitConverter.ToUInt32(wd, clx + 4);
        if (fcClx < 0 || lcbClx <= 0 || fcClx + lcbClx > tbl.Length) return "";

        var pieces = ParsePieceTable(tbl, fcClx, lcbClx);
        if (pieces is null) return "";

        var sb = new StringBuilder();
        foreach (var (cpStart, cpEnd, offset, compressed) in pieces)
        {
            if (cpStart >= ccpText) break;                       // past the main document text
            var end = Math.Min(cpEnd, ccpText);
            ReadPiece(wd, offset, end - cpStart, compressed, sb);
        }
        return Clean(sb.ToString());
    }

    // CLX = zero or more Prc (clxt 0x01) then a Pcdt (clxt 0x02) whose data is the PlcPcd.
    private static List<(int cpStart, int cpEnd, int offset, bool compressed)>? ParsePieceTable(
        byte[] tbl, int start, int lcb)
    {
        var p = start;
        var end = start + lcb;
        while (p < end)
        {
            var clxt = tbl[p];
            if (clxt == 0x01)                                    // Prc — skip its property blob
            {
                if (p + 3 > end) return null;
                var cbGrpprl = BitConverter.ToUInt16(tbl, p + 1);
                p += 3 + cbGrpprl;
            }
            else if (clxt == 0x02)                               // Pcdt — the piece table
            {
                if (p + 5 > end) return null;
                var plcLen = (int)BitConverter.ToUInt32(tbl, p + 1);
                var plc = p + 5;
                if (plc + plcLen > end) plcLen = end - plc;
                return ParsePlcPcd(tbl, plc, plcLen);
            }
            else break;
        }
        return null;
    }

    // PlcPcd: (n+1) character-position markers (4 bytes each) then n piece descriptors (8 each).
    private static List<(int, int, int, bool)>? ParsePlcPcd(byte[] tbl, int start, int lcb)
    {
        var n = (lcb - 4) / 12;
        if (n <= 0) return null;
        var pcdStart = start + (n + 1) * 4;
        var pieces = new List<(int, int, int, bool)>(n);
        for (var i = 0; i < n; i++)
        {
            var cpStart = (int)BitConverter.ToUInt32(tbl, start + i * 4);
            var cpEnd = (int)BitConverter.ToUInt32(tbl, start + (i + 1) * 4);
            var fc = BitConverter.ToUInt32(tbl, pcdStart + i * 8 + 2);   // skip 2-byte flags
            var compressed = (fc & 0x40000000) != 0;
            var offset = (int)(fc & 0x3FFFFFFF);
            if (compressed) offset /= 2;                          // CP1252 pieces are half-offset
            pieces.Add((cpStart, cpEnd, offset, compressed));
        }
        return pieces;
    }

    private static void ReadPiece(byte[] wd, int offset, int cch, bool compressed, StringBuilder sb)
    {
        if (cch <= 0) return;
        if (compressed)
        {
            for (var k = 0; k < cch && offset + k < wd.Length; k++)
                sb.Append((char)wd[offset + k]);                 // CP1252 ~ Latin-1 (best-effort)
        }
        else
        {
            for (var k = 0; k < cch; k++)
            {
                var o = offset + k * 2;
                if (o + 2 > wd.Length) break;
                sb.Append((char)(wd[o] | (wd[o + 1] << 8)));
            }
        }
    }

    // Word marks paragraphs with \r and uses several low control codes (field markers, cell
    // marks). Turn breaks into newlines and drop the rest.
    private static string Clean(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c is '\r' or '\n' or '\v' or '\f') sb.Append('\n');
            else if (c == '\t' || c >= ' ') sb.Append(c);
            // else: a control marker (field begin/sep/end, cell mark, …) — drop it
        }
        return sb.ToString().Trim();
    }
}
