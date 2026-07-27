using System.Text;

namespace Scrubkit;

/// <summary>
/// Reads Title / Author / Subject from the OLE2 SummaryInformation stream — the HPSF property
/// set shared by every binary Office format. Best-effort: handles the two string value types
/// (<c>VT_LPSTR</c> code-page and <c>VT_LPWSTR</c> UTF-16) that these fields use.
/// </summary>
internal static class SummaryInformation
{
    // Real files store this stream as "\x05SummaryInformation" (leading OLE control byte);
    // CompoundFile also indexes it under the stripped name, which is what we look up here.
    private const string StreamName = "SummaryInformation";
    private const int PidCodepage = 0x01, PidTitle = 0x02, PidSubject = 0x03, PidAuthor = 0x04;
    private const uint VtI2 = 0x02, VtLpstr = 0x1E, VtLpwstr = 0x1F;

    public static Dictionary<string, string> Read(CompoundFile cf)
    {
        var meta = new Dictionary<string, string>();
        if (!cf.TryRead(StreamName, out var d) || d.Length < 48) return meta;

        // Property-set header: byte-order(2) version(2) sysid(4) clsid(16) count(4),
        // then the first section's FMTID(16) + offset(4). Section offset lives at byte 44.
        var section = (int)BitConverter.ToUInt32(d, 44);
        if (section < 0 || section + 8 > d.Length) return meta;

        var cProps = (int)BitConverter.ToUInt32(d, section + 4);
        var props = new Dictionary<int, int>();   // property id -> absolute offset of its value
        for (var i = 0; i < cProps; i++)
        {
            var entry = section + 8 + i * 8;
            if (entry + 8 > d.Length) break;
            var pid = (int)BitConverter.ToUInt32(d, entry);
            var off = (int)BitConverter.ToUInt32(d, entry + 4);
            props[pid] = section + off;
        }

        var codepage = 0;
        if (props.TryGetValue(PidCodepage, out var cp) && cp + 6 <= d.Length &&
            BitConverter.ToUInt32(d, cp) == VtI2)
            codepage = BitConverter.ToInt16(d, cp + 4);

        Put(meta, "Title", ReadString(d, props, PidTitle, codepage));
        Put(meta, "Author", ReadString(d, props, PidAuthor, codepage));
        Put(meta, "Subject", ReadString(d, props, PidSubject, codepage));
        return meta;
    }

    private static string? ReadString(byte[] d, Dictionary<int, int> props, int pid, int codepage)
    {
        if (!props.TryGetValue(pid, out var off) || off + 8 > d.Length) return null;
        var type = BitConverter.ToUInt32(d, off);
        var len = (int)BitConverter.ToUInt32(d, off + 4);
        var start = off + 8;
        if (len <= 0) return null;

        if (type == VtLpstr)
        {
            if (start + len > d.Length) len = d.Length - start;
            while (len > 0 && d[start + len - 1] == 0) len--;   // drop trailing null(s)
            if (len <= 0) return null;
            // Stay dependency-free: UTF-8 code page decoded directly, everything else as Latin-1
            // (close enough for Western Title/Author/Subject; full code pages need an extra dep).
            return codepage == 65001 ? Encoding.UTF8.GetString(d, start, len) : Latin1(d, start, len);
        }
        if (type == VtLpwstr)
        {
            var bytes = len * 2;
            if (start + bytes > d.Length) bytes = ((d.Length - start) / 2) * 2;
            if (bytes <= 0) return null;
            return Encoding.Unicode.GetString(d, start, bytes).TrimEnd('\0');
        }
        return null;
    }

    private static string Latin1(byte[] d, int start, int len)
    {
        var chars = new char[len];
        for (var i = 0; i < len; i++) chars[i] = (char)d[start + i];
        return new string(chars);
    }

    private static void Put(Dictionary<string, string> meta, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) meta[key] = value!.Trim();
    }
}
