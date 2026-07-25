using System.Text;
using Scrubkit;
using Xunit;

namespace Scrubkit.LegacyOffice.Tests;

public class LegacyOfficeExtractorTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "scrubkit-legacy-" + Guid.NewGuid().ToString("N"));

    public LegacyOfficeExtractorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData(".doc", true)]
    [InlineData(".xls", true)]
    [InlineData(".ppt", true)]
    [InlineData(".docx", false)]    // OOXML is the core's job
    [InlineData(".pptx", false)]
    [InlineData(".PPT", false)]     // caller passes a normalized lower-case extension
    public void CanHandle_matches_wired_formats(string ext, bool expected) =>
        Assert.Equal(expected, new LegacyOfficeExtractor().CanHandle(ext));

    [Fact]
    public void Reads_slide_text_from_a_ppt()
    {
        var b = new OleBuilder();
        b.Add("PowerPoint Document", Ppt.Document(
            Ppt.TextBytesAtom("Hello slide"),
            Ppt.TextCharsAtom("Second box")));
        var path = Write("deck.ppt", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("Hello slide", c.Text);
        Assert.Contains("Second box", c.Text);
    }

    [Fact]
    public void Reads_title_author_subject_from_summary_information()
    {
        var b = new OleBuilder();
        b.Add("PowerPoint Document", Ppt.Document(Ppt.TextBytesAtom("body")));
        b.Add("SummaryInformation", Hpsf.SummaryInfo(title: "Q3 Deck", author: "Jane", subject: "Numbers"));
        var path = Write("meta.ppt", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Equal("Q3 Deck", c.Metadata["Title"]);
        Assert.Equal("Jane", c.Metadata["Author"]);
        Assert.Equal("Numbers", c.Metadata["Subject"]);
    }

    [Fact]
    public void Reads_cell_strings_from_an_xls()
    {
        var workbook = Xls.Workbook(
            Xls.Sst("Apple", "Banana", "Cherry"),
            Xls.LabelSst(0, 0, 0),
            Xls.LabelSst(1, 0, 2),
            Xls.LabelSst(2, 0, 1));
        var b = new OleBuilder();
        b.Add("Workbook", workbook);
        var path = Write("book.xls", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("Apple", c.Text);
        Assert.Contains("Cherry", c.Text);
        Assert.Contains("Banana", c.Text);
    }

    [Fact]
    public void Reads_a_string_split_across_a_continue_record()
    {
        // "HelloWorld" with the character array split after "Hello" into a CONTINUE record,
        // which restarts with a fresh (compressed) option-flags byte — the fiddly SST path.
        var workbook = Xls.Workbook(
            Xls.SplitSst("Hello", "World"),
            Xls.LabelSst(0, 0, 0));
        var b = new OleBuilder();
        b.Add("Workbook", workbook);
        var path = Write("split.xls", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("HelloWorld", c.Text);
    }

    [Fact]
    public void Reads_body_text_from_a_doc()
    {
        var (wordDoc, table) = Doc.Build("Hello from Word\rSecond paragraph");
        var b = new OleBuilder();
        b.Add("WordDocument", wordDoc);
        b.Add("0Table", table);
        var path = Write("letter.doc", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("Hello from Word", c.Text);
        Assert.Contains("Second paragraph", c.Text);
    }

    [Fact]
    public void Not_a_compound_file_throws()
    {
        var path = Write("bad.ppt", Encoding.ASCII.GetBytes("this is not OLE2"));
        Assert.Throws<InvalidDataException>(() => new LegacyOfficeExtractor().Extract(path));
    }

    [Fact]
    public async Task Routes_through_FolderScrubber_as_a_presentation_row()
    {
        var b = new OleBuilder();
        b.Add("PowerPoint Document", Ppt.Document(Ppt.TextBytesAtom("Routed through the scrubber.")));
        Write("routed.ppt", b.Build());

        var options = new ReadOptions();
        options.Extractors.Add(new LegacyOfficeExtractor());

        var table = await new FolderScrubber(options).ReadAsync(_dir);

        var row = Assert.Single(table);
        Assert.Equal("Presentation", row.TypeBucket);
        Assert.Contains("Routed through the scrubber.", row.Text);
        Assert.Empty(row.Warnings);
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // ---- PowerPoint Document stream composition ----

    private static class Ppt
    {
        public static byte[] TextBytesAtom(string text) =>
            Record(0x0FA8, Encoding.GetEncoding("ISO-8859-1").GetBytes(text));

        public static byte[] TextCharsAtom(string text) =>
            Record(0x0FA0, Encoding.Unicode.GetBytes(text));

        // Wrap child records in a container record (recVer nibble = 0xF).
        public static byte[] Document(params byte[][] children)
        {
            var body = Concat(children);
            var rec = new byte[8 + body.Length];
            Array.Copy(BitConverter.GetBytes((ushort)0x000F), 0, rec, 0, 2);   // container
            Array.Copy(BitConverter.GetBytes((ushort)0x03E8), 0, rec, 2, 2);   // DocumentContainer
            Array.Copy(BitConverter.GetBytes((uint)body.Length), 0, rec, 4, 4);
            Array.Copy(body, 0, rec, 8, body.Length);
            return rec;
        }

        private static byte[] Record(ushort type, byte[] body)
        {
            var rec = new byte[8 + body.Length];
            Array.Copy(BitConverter.GetBytes((ushort)0x0000), 0, rec, 0, 2);   // atom (ver nibble 0)
            Array.Copy(BitConverter.GetBytes(type), 0, rec, 2, 2);
            Array.Copy(BitConverter.GetBytes((uint)body.Length), 0, rec, 4, 4);
            Array.Copy(body, 0, rec, 8, body.Length);
            return rec;
        }

        private static byte[] Concat(byte[][] parts)
        {
            var ms = new MemoryStream();
            foreach (var p in parts) ms.Write(p, 0, p.Length);
            return ms.ToArray();
        }
    }

    // ---- \x05SummaryInformation (HPSF property set) composition ----

    private static class Hpsf
    {
        public static byte[] SummaryInfo(string title, string author, string subject)
        {
            // Section: cb(4) cProps(4) then [pid(4) off(4)]* then the values.
            var props = new (int pid, byte[] value)[]
            {
                (0x02, Lpstr(title)),
                (0x04, Lpstr(author)),
                (0x03, Lpstr(subject)),
            };
            var indexSize = 8 + props.Length * 8;   // cb + cProps + index entries
            var section = new MemoryStream();
            var valuesStart = indexSize;
            var offset = valuesStart;
            var index = new MemoryStream();
            var values = new MemoryStream();
            foreach (var (pid, value) in props)
            {
                index.Write(BitConverter.GetBytes(pid), 0, 4);
                index.Write(BitConverter.GetBytes(offset), 0, 4);   // relative to section start
                values.Write(value, 0, value.Length);
                offset += value.Length;
            }
            var cb = indexSize + (int)values.Length;
            section.Write(BitConverter.GetBytes(cb), 0, 4);
            section.Write(BitConverter.GetBytes(props.Length), 0, 4);
            var idx = index.ToArray(); section.Write(idx, 0, idx.Length);
            var val = values.ToArray(); section.Write(val, 0, val.Length);
            var sectionBytes = section.ToArray();

            // Property-set header (48 bytes) + section.
            var stream = new MemoryStream();
            stream.Write(BitConverter.GetBytes((ushort)0xFFFE), 0, 2);   // byte order
            stream.Write(BitConverter.GetBytes((ushort)0x0000), 0, 2);   // version
            stream.Write(BitConverter.GetBytes(0x0002_0105), 0, 4);      // system id (arbitrary)
            stream.Write(new byte[16], 0, 16);                            // CLSID
            stream.Write(BitConverter.GetBytes(1), 0, 4);                 // 1 property set
            stream.Write(new byte[16], 0, 16);                            // FMTID
            stream.Write(BitConverter.GetBytes(48), 0, 4);               // section offset
            stream.Write(sectionBytes, 0, sectionBytes.Length);
            return stream.ToArray();
        }

        // VT_LPSTR: type(4)=0x1E, length(4, incl null), ANSI bytes + null.
        private static byte[] Lpstr(string s)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(s);
            var ms = new MemoryStream();
            ms.Write(BitConverter.GetBytes(0x1E), 0, 4);
            ms.Write(BitConverter.GetBytes(bytes.Length + 1), 0, 4);
            ms.Write(bytes, 0, bytes.Length);
            ms.WriteByte(0);
            return ms.ToArray();
        }
    }

    // ---- Workbook (BIFF) stream composition ----

    private static class Xls
    {
        public static byte[] Workbook(params byte[][] records) => Concat(records);

        // SST record: cstTotal(4) cstUnique(4) then each string (cch, grbit=0 compressed, bytes).
        public static byte[] Sst(params string[] strings)
        {
            var body = new MemoryStream();
            body.Write(BitConverter.GetBytes(strings.Length), 0, 4);
            body.Write(BitConverter.GetBytes(strings.Length), 0, 4);
            foreach (var s in strings)
            {
                body.Write(BitConverter.GetBytes((ushort)s.Length), 0, 2);
                body.WriteByte(0);                       // compressed
                var bytes = Latin1(s);
                body.Write(bytes, 0, bytes.Length);
            }
            return Record(0x00FC, body.ToArray());
        }

        // One string split across a CONTINUE boundary; the CONTINUE restarts with a flags byte.
        public static byte[] SplitSst(string part1, string part2)
        {
            var sst = new MemoryStream();
            sst.Write(BitConverter.GetBytes(1), 0, 4);
            sst.Write(BitConverter.GetBytes(1), 0, 4);
            sst.Write(BitConverter.GetBytes((ushort)(part1.Length + part2.Length)), 0, 2);
            sst.WriteByte(0);                            // compressed
            var a = Latin1(part1); sst.Write(a, 0, a.Length);

            var cont = new MemoryStream();
            cont.WriteByte(0);                           // fresh option-flags byte
            var b = Latin1(part2); cont.Write(b, 0, b.Length);

            return Concat(Record(0x00FC, sst.ToArray()), Record(0x003C, cont.ToArray()));
        }

        public static byte[] LabelSst(int row, int col, int isst)
        {
            var body = new MemoryStream();
            body.Write(BitConverter.GetBytes((ushort)row), 0, 2);
            body.Write(BitConverter.GetBytes((ushort)col), 0, 2);
            body.Write(BitConverter.GetBytes((ushort)0), 0, 2);   // ixfe
            body.Write(BitConverter.GetBytes(isst), 0, 4);
            return Record(0x00FD, body.ToArray());
        }

        private static byte[] Record(ushort type, byte[] body)
        {
            var rec = new byte[4 + body.Length];
            Array.Copy(BitConverter.GetBytes(type), 0, rec, 0, 2);
            Array.Copy(BitConverter.GetBytes((ushort)body.Length), 0, rec, 2, 2);
            Array.Copy(body, 0, rec, 4, body.Length);
            return rec;
        }

        private static byte[] Latin1(string s) => Encoding.GetEncoding("ISO-8859-1").GetBytes(s);

        private static byte[] Concat(params byte[][] parts)
        {
            var ms = new MemoryStream();
            foreach (var p in parts) ms.Write(p, 0, p.Length);
            return ms.ToArray();
        }
    }

    // ---- WordDocument + table stream composition (Word 97 shaped FIB) ----

    private static class Doc
    {
        public static (byte[] wordDoc, byte[] table) Build(string text)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(text);
            var cch = text.Length;
            const int textStart = 0x1AA;              // just past a Word97-shaped FIB

            var wd = new byte[textStart + bytes.Length];
            Put16(wd, 0x00, 0xA5EC);                  // wIdent
            Put16(wd, 0x02, 0x00C1);                  // nFib (Word 97)
            // FibBase flags at 0x0A left 0 -> fWhichTblStm clear -> use 0Table
            Put16(wd, 0x20, 14);                      // csw
            Put16(wd, 0x3E, 22);                      // cslw
            Put32(wd, 0x4C, (uint)cch);               // ccpText (fibRgLw[3])
            Put16(wd, 0x98, 34);                      // cbRgFcLcb (need FC/LCB pair 33)
            Put32(wd, 0x1A2, 0);                      // fcClx  -> offset 0 in the table stream
            Put32(wd, 0x1A6, 21);                     // lcbClx -> 1 + 4 + 16
            Array.Copy(bytes, 0, wd, textStart, bytes.Length);

            var table = new MemoryStream();
            table.WriteByte(0x02);                                       // clxt = Pcdt
            table.Write(BitConverter.GetBytes((uint)16), 0, 4);         // PlcPcd size
            table.Write(BitConverter.GetBytes((uint)0), 0, 4);          // CP[0]
            table.Write(BitConverter.GetBytes((uint)cch), 0, 4);        // CP[1]
            table.Write(BitConverter.GetBytes((ushort)0), 0, 2);        // PCD flags
            table.Write(BitConverter.GetBytes((uint)(textStart * 2) | 0x40000000u), 0, 4); // fc (compressed)
            table.Write(BitConverter.GetBytes((ushort)0), 0, 2);        // prm
            return (wd, table.ToArray());
        }

        private static void Put16(byte[] a, int off, int v) => BitConverter.GetBytes((ushort)v).CopyTo(a, off);
        private static void Put32(byte[] a, int off, uint v) => BitConverter.GetBytes(v).CopyTo(a, off);
    }

    // ---------------------------------------------------------------------
    // Minimal CFBF (OLE2) writer — small streams via the mini stream. Same
    // technique used by the .msg tests; the reader ignores the sibling tree,
    // so directory entries are written linearly.
    // ---------------------------------------------------------------------
    private sealed class OleBuilder
    {
        private const uint EndOfChain = 0xFFFFFFFE;
        private const uint FreeSect = 0xFFFFFFFF;
        private const uint NoStream = 0xFFFFFFFF;
        private const int Sector = 512;
        private const int MiniSector = 64;

        private readonly List<(string name, byte[] data)> _streams = new();

        public void Add(string name, byte[] data) => _streams.Add((name, data));

        public byte[] Build()
        {
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

            var numEntries = _streams.Count + 1;
            var dirSectors = (numEntries + 3) / 4;
            var dir = new byte[dirSectors * Sector];

            var firstDir = 1u;
            var firstMiniFat = firstDir + (uint)dirSectors;
            var firstMiniStream = firstMiniFat + 1;
            var miniStreamSectors = Math.Max(1, (miniStream.Length + Sector - 1) / Sector);

            WriteDirEntry(dir, 0, "Root Entry", 5, firstMiniStream, (ulong)miniStream.Length);
            for (var i = 0; i < _streams.Count; i++)
                WriteDirEntry(dir, i + 1, _streams[i].name, 2, starts[i], (ulong)_streams[i].data.Length);

            var fat = new uint[Sector / 4];
            for (var i = 0; i < fat.Length; i++) fat[i] = FreeSect;
            fat[0] = 0xFFFFFFFD;                               // FATSECT
            for (var i = 0; i < dirSectors; i++)
                fat[firstDir + i] = i == dirSectors - 1 ? EndOfChain : firstDir + (uint)i + 1;
            fat[firstMiniFat] = EndOfChain;
            for (var i = 0; i < miniStreamSectors; i++)
                fat[firstMiniStream + i] = i == miniStreamSectors - 1 ? EndOfChain : firstMiniStream + (uint)i + 1;

            var miniFatSector = new uint[Sector / 4];
            for (var i = 0; i < miniFatSector.Length; i++)
                miniFatSector[i] = i < miniFat.Count ? miniFat[i] : FreeSect;

            var file = new MemoryStream();
            file.Write(Header(firstDir, firstMiniFat), 0, Sector);
            file.Write(UintSector(fat), 0, Sector);
            file.Write(dir, 0, dir.Length);
            file.Write(UintSector(miniFatSector), 0, Sector);
            var padded = new byte[miniStreamSectors * Sector];
            Array.Copy(miniStream, padded, miniStream.Length);
            file.Write(padded, 0, padded.Length);
            return file.ToArray();
        }

        private static byte[] Header(uint firstDir, uint firstMiniFat)
        {
            var h = new byte[Sector];
            Array.Copy(BitConverter.GetBytes(0xE11AB1A1E011CFD0UL), 0, h, 0, 8);
            h[26] = 0x03; h[28] = 0xFE; h[29] = 0xFF;
            h[30] = 0x09;                                      // sector shift -> 512
            h[32] = 0x06;                                      // mini sector shift -> 64
            Array.Copy(BitConverter.GetBytes(1u), 0, h, 44, 4);
            Array.Copy(BitConverter.GetBytes(firstDir), 0, h, 48, 4);
            Array.Copy(BitConverter.GetBytes(4096u), 0, h, 56, 4);
            Array.Copy(BitConverter.GetBytes(firstMiniFat), 0, h, 60, 4);
            Array.Copy(BitConverter.GetBytes(1u), 0, h, 64, 4);
            Array.Copy(BitConverter.GetBytes(EndOfChain), 0, h, 68, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, h, 72, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, h, 76, 4);   // DIFAT[0] = FAT at sector 0
            for (var i = 1; i < 109; i++)
                Array.Copy(BitConverter.GetBytes(FreeSect), 0, h, 76 + i * 4, 4);
            return h;
        }

        private static void WriteDirEntry(byte[] dir, int index, string name, byte type, uint start, ulong size)
        {
            var b = index * 128;
            var nameBytes = Encoding.Unicode.GetBytes(name);
            Array.Copy(nameBytes, 0, dir, b, nameBytes.Length);
            var nameLen = (ushort)(nameBytes.Length + 2);
            Array.Copy(BitConverter.GetBytes(nameLen), 0, dir, b + 64, 2);
            dir[b + 66] = type;
            dir[b + 67] = 1;
            Array.Copy(BitConverter.GetBytes(NoStream), 0, dir, b + 68, 4);
            Array.Copy(BitConverter.GetBytes(NoStream), 0, dir, b + 72, 4);
            Array.Copy(BitConverter.GetBytes(NoStream), 0, dir, b + 76, 4);
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
