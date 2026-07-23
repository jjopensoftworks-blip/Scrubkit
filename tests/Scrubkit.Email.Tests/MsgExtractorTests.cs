using System.Text;
using Scrubkit;
using Xunit;

namespace Scrubkit.Email.Tests;

public class MsgExtractorTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "scrubkit-msg-" + Guid.NewGuid().ToString("N"));

    public MsgExtractorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData(".msg", true)]
    [InlineData(".eml", false)]
    [InlineData(".txt", false)]
    [InlineData(".MSG", false)]   // caller passes a normalized lower-case extension
    public void CanHandle_matches_only_msg(string ext, bool expected) =>
        Assert.Equal(expected, new MsgExtractor().CanHandle(ext));

    [Fact]
    public void Reads_subject_sender_recipients_and_body()
    {
        var b = new MsgBuilder();
        b.AddUnicode("0037", "Quarterly report");                 // Subject
        b.AddUnicode("0C1A", "Jane Doe");                          // SenderName
        b.AddUnicode("5D01", "jane@example.com");                  // SenderSmtpAddress
        b.AddUnicode("0E04", "Bob <bob@example.com>");             // DisplayTo
        b.AddUnicode("0E03", "Carol <carol@example.com>");         // DisplayCc
        b.AddUnicode("1000", "Numbers attached.\r\nThanks,\r\nJane");  // Body
        var path = Write("report.msg", b.Build());

        var c = new MsgExtractor().Extract(path);

        Assert.Equal("Quarterly report", c.Metadata["Subject"]);
        Assert.Equal("Jane Doe <jane@example.com>", c.Metadata["From"]);
        Assert.Equal("Bob <bob@example.com>", c.Metadata["To"]);
        Assert.Equal("Carol <carol@example.com>", c.Metadata["Cc"]);
        Assert.Equal("Numbers attached.\nThanks,\nJane", c.Text);
    }

    [Fact]
    public void Falls_back_to_ansi_property_and_bare_sender()
    {
        var b = new MsgBuilder();
        b.AddAnsi("0037", "Legacy subject");     // only the ANSI (001E) variant exists
        b.AddUnicode("0C1F", "sender@old.example");  // SenderEmailAddress, no display name
        var path = Write("legacy.msg", b.Build());

        var c = new MsgExtractor().Extract(path);

        Assert.Equal("Legacy subject", c.Metadata["Subject"]);
        Assert.Equal("sender@old.example", c.Metadata["From"]);   // bare address, no "Name <...>"
    }

    [Fact]
    public void Reads_submit_time_from_the_property_stream()
    {
        var when = new DateTime(2026, 7, 22, 9, 30, 0, DateTimeKind.Utc);
        var b = new MsgBuilder();
        b.AddUnicode("0037", "Timed");
        b.AddFileTime(0x0039, when);   // PR_CLIENT_SUBMIT_TIME
        var path = Write("timed.msg", b.Build());

        var c = new MsgExtractor().Extract(path);

        Assert.Equal("2026-07-22T09:30:00Z", c.Metadata["Date"]);
    }

    [Fact]
    public void Not_a_compound_file_throws()
    {
        var path = Write("bad.msg", Encoding.ASCII.GetBytes("this is not OLE2"));
        Assert.Throws<InvalidDataException>(() => new MsgExtractor().Extract(path));
    }

    [Fact]
    public async Task Routes_through_FolderScrubber_as_an_email_row()
    {
        var b = new MsgBuilder();
        b.AddUnicode("0037", "Routed");
        b.AddUnicode("1000", "Delivered through the scrubber.");
        Write("msg.msg", b.Build());

        var options = new ReadOptions();
        options.Extractors.Add(new MsgExtractor());

        var table = await new FolderScrubber(options).ReadAsync(_dir);

        var row = Assert.Single(table);
        Assert.Equal("Email", row.TypeBucket);
        Assert.Equal("Routed", row.Metadata["Subject"]);
        Assert.Contains("Delivered through the scrubber.", row.Text);
        Assert.Empty(row.Warnings);
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // ---------------------------------------------------------------------
    // Minimal CFBF (OLE2) writer — enough to round-trip small streams through
    // CompoundFile. v3 layout: 512-byte sectors, 64-byte mini sectors, every
    // stream stored in the mini stream. Directory entries are written linearly
    // (the reader ignores the red-black sibling tree).
    // ---------------------------------------------------------------------
    private sealed class MsgBuilder
    {
        private const uint EndOfChain = 0xFFFFFFFE;
        private const uint FreeSect = 0xFFFFFFFF;
        private const uint NoStream = 0xFFFFFFFF;
        private const int Sector = 512;
        private const int MiniSector = 64;

        private readonly List<(string name, byte[] data)> _streams = new();

        public void AddUnicode(string id, string value) =>
            _streams.Add(($"__substg1.0_{id}001F", Encoding.Unicode.GetBytes(value)));

        public void AddAnsi(string id, string value) =>
            _streams.Add(($"__substg1.0_{id}001E", Encoding.ASCII.GetBytes(value)));

        // A fixed-property stream carrying one PtypTime (0x0040) property as a FILETIME.
        public void AddFileTime(uint propId, DateTime utc)
        {
            var s = new byte[32 + 16];                        // header + one entry
            var tag = (propId << 16) | 0x0040u;
            Array.Copy(BitConverter.GetBytes(tag), 0, s, 32, 4);
            Array.Copy(BitConverter.GetBytes(utc.ToFileTimeUtc()), 0, s, 32 + 8, 8);
            _streams.Add(("__properties_version1.0", s));
        }

        public byte[] Build()
        {
            // ---- lay streams into the mini stream, one mini-FAT chain each ----
            var mini = new MemoryStream();
            var miniFat = new List<uint>();
            var starts = new List<uint>();
            foreach (var (_, data) in _streams)
            {
                var sectors = Math.Max(1, (data.Length + MiniSector - 1) / MiniSector);
                starts.Add((uint)miniFat.Count);
                for (var i = 0; i < sectors; i++)
                    miniFat.Add(i == sectors - 1 ? EndOfChain : (uint)(miniFat.Count + 1));
                mini.Write(data, 0, data.Length);
                var pad = sectors * MiniSector - data.Length;
                if (pad > 0) mini.Write(new byte[pad], 0, pad);
            }
            var miniStream = mini.ToArray();

            // ---- directory: Root Entry + one entry per stream ----
            var numEntries = _streams.Count + 1;
            var dirSectors = (numEntries + 3) / 4;             // 4 entries per 512-byte sector
            var dir = new byte[dirSectors * Sector];

            // sector map: [0]=FAT, [1..dirSectors]=dir, [+1]=miniFAT, [+n]=mini stream
            var firstDir = 1u;
            var firstMiniFat = firstDir + (uint)dirSectors;
            var firstMiniStream = firstMiniFat + 1;
            var miniStreamSectors = Math.Max(1, (miniStream.Length + Sector - 1) / Sector);

            WriteDirEntry(dir, 0, "Root Entry", 5, firstMiniStream, (ulong)miniStream.Length);
            for (var i = 0; i < _streams.Count; i++)
                WriteDirEntry(dir, i + 1, _streams[i].name, 2, starts[i], (ulong)_streams[i].data.Length);

            // ---- FAT (one sector = 128 entries) ----
            var totalSectors = 1 + dirSectors + 1 + miniStreamSectors;
            var fat = new uint[Sector / 4];
            for (var i = 0; i < fat.Length; i++) fat[i] = FreeSect;
            fat[0] = 0xFFFFFFFD;                               // FATSECT
            for (var i = 0; i < dirSectors; i++)
                fat[firstDir + i] = i == dirSectors - 1 ? EndOfChain : firstDir + (uint)i + 1;
            fat[firstMiniFat] = EndOfChain;
            for (var i = 0; i < miniStreamSectors; i++)
                fat[firstMiniStream + i] = i == miniStreamSectors - 1 ? EndOfChain : firstMiniStream + (uint)i + 1;

            // ---- mini FAT (one sector) ----
            var miniFatSector = new uint[Sector / 4];
            for (var i = 0; i < miniFatSector.Length; i++)
                miniFatSector[i] = i < miniFat.Count ? miniFat[i] : FreeSect;

            // ---- assemble: header + sectors ----
            var file = new MemoryStream();
            file.Write(Header(firstDir, firstMiniFat), 0, Sector);
            file.Write(UintSector(fat), 0, Sector);
            file.Write(dir, 0, dir.Length);
            file.Write(UintSector(miniFatSector), 0, Sector);
            var padded = new byte[miniStreamSectors * Sector];
            Array.Copy(miniStream, padded, miniStream.Length);
            file.Write(padded, 0, padded.Length);
            _ = totalSectors;
            return file.ToArray();
        }

        private static byte[] Header(uint firstDir, uint firstMiniFat)
        {
            var h = new byte[Sector];
            Array.Copy(BitConverter.GetBytes(0xE11AB1A1E011CFD0UL), 0, h, 0, 8);   // signature
            h[26] = 0x03; h[28] = 0xFE; h[29] = 0xFF;          // major v3, byte order
            h[30] = 0x09;                                      // sector shift -> 512
            h[32] = 0x06;                                      // mini sector shift -> 64
            Array.Copy(BitConverter.GetBytes(1u), 0, h, 44, 4);           // # FAT sectors
            Array.Copy(BitConverter.GetBytes(firstDir), 0, h, 48, 4);     // first dir sector
            Array.Copy(BitConverter.GetBytes(4096u), 0, h, 56, 4);        // mini cutoff
            Array.Copy(BitConverter.GetBytes(firstMiniFat), 0, h, 60, 4); // first mini FAT
            Array.Copy(BitConverter.GetBytes(1u), 0, h, 64, 4);           // # mini FAT sectors
            Array.Copy(BitConverter.GetBytes(EndOfChain), 0, h, 68, 4);   // first DIFAT
            Array.Copy(BitConverter.GetBytes(0u), 0, h, 72, 4);           // # DIFAT sectors
            Array.Copy(BitConverter.GetBytes(0u), 0, h, 76, 4);           // DIFAT[0] = FAT at sector 0
            for (var i = 1; i < 109; i++)
                Array.Copy(BitConverter.GetBytes(FreeSect), 0, h, 76 + i * 4, 4);
            return h;
        }

        private static void WriteDirEntry(byte[] dir, int index, string name, byte type, uint start, ulong size)
        {
            var b = index * 128;
            var nameBytes = Encoding.Unicode.GetBytes(name);
            Array.Copy(nameBytes, 0, dir, b, nameBytes.Length);
            var nameLen = (ushort)(nameBytes.Length + 2);      // include null terminator
            Array.Copy(BitConverter.GetBytes(nameLen), 0, dir, b + 64, 2);
            dir[b + 66] = type;
            dir[b + 67] = 1;                                   // colour = black
            Array.Copy(BitConverter.GetBytes(NoStream), 0, dir, b + 68, 4);   // left
            Array.Copy(BitConverter.GetBytes(NoStream), 0, dir, b + 72, 4);   // right
            Array.Copy(BitConverter.GetBytes(NoStream), 0, dir, b + 76, 4);   // child
            Array.Copy(BitConverter.GetBytes(start), 0, dir, b + 116, 4);
            Array.Copy(BitConverter.GetBytes(size), 0, dir, b + 120, 8);
        }

        private static byte[] UintSector(uint[] values)
        {
            var bytes = new byte[values.Length * 4];
            for (var i = 0; i < values.Length; i++)
                Array.Copy(BitConverter.GetBytes(values[i]), 0, bytes, i * 4, 4);
            return bytes;
        }
    }
}
