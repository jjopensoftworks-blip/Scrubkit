using System.Globalization;
using System.Text;

namespace Scrubkit;

/// <summary>
/// Reads Outlook <c>.msg</c> messages. The useful properties
/// (From / To / Cc / Subject / Date) become metadata and the message body becomes text.
///
/// A <c>.msg</c> is an OLE2 / Compound File Binary Format container; this reads it with a
/// small built-in CFBF parser (see <see cref="CompoundFile"/>) — best-effort, fully offline,
/// and with no dependency beyond <c>Scrubkit.Abstractions</c>. Prefers the Unicode
/// (<c>001F</c>) property streams, falling back to the ANSI (<c>001E</c>) variants.
/// Attachments and embedded objects are skipped. Register it via
/// <see cref="ReadOptions.Extractors"/>.
/// </summary>
public sealed class MsgExtractor : IFileExtractor
{
    // MAPI property IDs (the "PPPP" half of a __substg1.0_PPPPTTTT stream name).
    private const string Subject = "0037";
    private const string SenderName = "0C1A";
    private const string SenderEmail = "0C1F";
    private const string SenderSmtp = "5D01";
    private const string DisplayTo = "0E04";
    private const string DisplayCc = "0E03";
    private const string Body = "1000";

    /// <inheritdoc/>
    public bool CanHandle(string extension) => extension == ".msg";

    /// <inheritdoc/>
    public ExtractedContent Extract(string path)
    {
        var cf = new CompoundFile(File.ReadAllBytes(path));

        var meta = new Dictionary<string, string>();
        Put(meta, "From", From(cf));
        Put(meta, "To", Prop(cf, DisplayTo));
        Put(meta, "Cc", Prop(cf, DisplayCc));
        Put(meta, "Subject", Prop(cf, Subject));
        Put(meta, "Date", Date(cf));

        var text = Prop(cf, Body)?.Replace("\r\n", "\n").Trim() ?? "";
        return new ExtractedContent(meta, text);
    }

    // Combine the sender's display name and address into an RFC-5322-ish "Name <addr>".
    private static string? From(CompoundFile cf)
    {
        var name = Prop(cf, SenderName);
        var addr = Prop(cf, SenderSmtp) ?? Prop(cf, SenderEmail);
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(addr))
            return $"{name!.Trim()} <{addr!.Trim()}>";
        return name ?? addr;
    }

    // Read a string property, preferring Unicode (001F) over ANSI (001E).
    private static string? Prop(CompoundFile cf, string id)
    {
        if (cf.TryRead($"__substg1.0_{id}001F", out var uni))
            return Encoding.Unicode.GetString(uni).TrimEnd('\0');
        if (cf.TryRead($"__substg1.0_{id}001E", out var ansi))
            return Latin1(ansi).TrimEnd('\0');
        return null;
    }

    // The send/delivery time lives as a FILETIME in the fixed-property stream, not a substg.
    private static string? Date(CompoundFile cf)
    {
        if (!cf.TryRead("__properties_version1.0", out var props) || props.Length < 32)
            return null;

        const uint PtypTime = 0x0040;
        const uint ClientSubmitTime = 0x0039, MessageDeliveryTime = 0x0E06;
        long submit = 0, delivery = 0;

        // Top-level property stream: 32-byte header, then 16-byte entries.
        for (var off = 32; off + 16 <= props.Length; off += 16)
        {
            var tag = BitConverter.ToUInt32(props, off);
            if ((tag & 0xFFFF) != PtypTime) continue;
            var id = tag >> 16;
            var value = BitConverter.ToInt64(props, off + 8);   // FILETIME
            if (id == ClientSubmitTime) submit = value;
            else if (id == MessageDeliveryTime) delivery = value;
        }

        var ticks = submit != 0 ? submit : delivery;
        if (ticks <= 0) return null;
        try
        {
            return DateTime.FromFileTimeUtc(ticks)
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static void Put(Dictionary<string, string> meta, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) meta[key] = value!.Trim();
    }

    private static string Latin1(byte[] data)
    {
        var chars = new char[data.Length];
        for (var i = 0; i < data.Length; i++) chars[i] = (char)data[i];
        return new string(chars);
    }
}
