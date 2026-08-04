// Copyright © 2026 jjopensoftworks-blip

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

    // ---- complex scenarios ----

    [Fact]
    public void Reads_a_large_ppt_stored_in_regular_fat_sectors()
    {
        // A >4096-byte stream is stored in the regular FAT (not the mini stream) — the path
        // every real Office document actually uses.
        var big = new string('A', 6000);
        var b = new OleBuilder();
        b.Add("PowerPoint Document", Ppt.Document(Ppt.TextBytesAtom(big)));
        var path = Write("big.ppt", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.True(c.Text.Length >= 6000);
        Assert.StartsWith("AAAAAAAAAA", c.Text);
    }

    [Fact]
    public void Ppt_walks_nested_containers_skips_non_text_and_reads_unicode()
    {
        var doc = Ppt.Document(
            Ppt.Container(0x0FF0,                             // arbitrary nested container
                Ppt.Atom(0x0BC1, new byte[] { 1, 2, 3, 4 }), // non-text atom -> ignored
                Ppt.TextBytesAtom("Outline heading")),
            Ppt.TextCharsAtom("Résumé café ☕"));             // 16-bit, non-ASCII
        var b = new OleBuilder();
        b.Add("PowerPoint Document", doc);
        var path = Write("nested.ppt", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("Outline heading", c.Text);
        Assert.Contains("Résumé café", c.Text);
    }

    [Fact]
    public void Xls_reads_unicode_and_rich_text_strings()
    {
        var workbook = Xls.Workbook(
            Xls.SstRecord(
                Xls.SstString("Plain"),
                Xls.SstString("Café ☕", high: true),
                Xls.SstString("Bold", cRun: 2, cbExt: 6)),   // rich + phonetic trailers, skipped
            Xls.LabelSst(0, 0, 0),
            Xls.LabelSst(1, 0, 1),
            Xls.LabelSst(2, 0, 2));
        var b = new OleBuilder();
        b.Add("Workbook", workbook);
        var path = Write("rich.xls", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("Plain", c.Text);
        Assert.Contains("Café ☕", c.Text);
        Assert.Contains("Bold", c.Text);
    }

    [Fact]
    public void Xls_ignores_an_out_of_range_string_index()
    {
        var workbook = Xls.Workbook(Xls.Sst("Only"), Xls.LabelSst(0, 0, 0), Xls.LabelSst(1, 0, 99));
        var b = new OleBuilder();
        b.Add("Workbook", workbook);
        var path = Write("oor.xls", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Equal("Only", c.Text);   // bad index skipped, no crash
    }

    [Fact]
    public void Xls_reads_a_string_whose_encoding_changes_at_the_continue_boundary()
    {
        var workbook = Xls.Workbook(Xls.SplitSstMixed("ASCII", "Ünïcödé"), Xls.LabelSst(0, 0, 0));
        var b = new OleBuilder();
        b.Add("Workbook", workbook);
        var path = Write("mixsplit.xls", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("ASCIIÜnïcödé", c.Text);
    }

    [Fact]
    public void Doc_stitches_multiple_pieces_with_mixed_encoding()
    {
        var (wd, table) = Doc.BuildDoc(false, new[] { ("Hello ", true), ("Wörld", false) });
        var b = new OleBuilder();
        b.Add("WordDocument", wd);
        b.Add("0Table", table);
        var path = Write("multi.doc", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("Hello Wörld", c.Text);
    }

    [Fact]
    public void Doc_excludes_text_beyond_ccpText()
    {
        var (wd, table) = Doc.BuildDoc(false, new[] { ("Visible body", true) }, beyond: "FOOTNOTEONLY");
        var b = new OleBuilder();
        b.Add("WordDocument", wd);
        b.Add("0Table", table);
        var path = Write("beyond.doc", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("Visible body", c.Text);
        Assert.DoesNotContain("FOOTNOTEONLY", c.Text);
    }

    [Fact]
    public void Doc_reads_the_piece_table_from_the_1table_stream_when_flagged()
    {
        var (wd, table) = Doc.BuildDoc(true, new[] { ("From table one", true) });
        var b = new OleBuilder();
        b.Add("WordDocument", wd);
        b.Add("1Table", table);
        var path = Write("t1.doc", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Contains("From table one", c.Text);
    }

    [Fact]
    public void Missing_content_stream_yields_empty_text_but_still_reads_metadata()
    {
        var b = new OleBuilder();
        b.Add("SomethingElse", new byte[30]);      // valid OLE2, no known content stream
        b.Add("SummaryInformation", Hpsf.SummaryInfo(title: "Q3 Deck", author: "Jane", subject: "Numbers"));
        var path = Write("empty.ppt", b.Build());

        var c = new LegacyOfficeExtractor().Extract(path);

        Assert.Equal("", c.Text);
        Assert.Equal("Q3 Deck", c.Metadata["Title"]);
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
        // The top-level DocumentContainer wrapping the given children.
        public static byte[] Document(params byte[][] children) => Container(0x03E8, children);

        // A container record (recVer low nibble = 0xF) so the reader recurses into it.
        public static byte[] Container(ushort type, params byte[][] children) =>
            Rec(0x000F, type, Concat(children));

        public static byte[] Atom(ushort type, byte[] body) => Rec(0x0000, type, body);

        public static byte[] TextBytesAtom(string text) => Atom(0x0FA8, Latin1(text));
        public static byte[] TextCharsAtom(string text) => Atom(0x0FA0, Encoding.Unicode.GetBytes(text));

        private static byte[] Rec(ushort verInstance, ushort type, byte[] body)
        {
            var rec = new byte[8 + body.Length];
            Array.Copy(BitConverter.GetBytes(verInstance), 0, rec, 0, 2);
            Array.Copy(BitConverter.GetBytes(type), 0, rec, 2, 2);
            Array.Copy(BitConverter.GetBytes((uint)body.Length), 0, rec, 4, 4);
            Array.Copy(body, 0, rec, 8, body.Length);
            return rec;
        }

        private static byte[] Latin1(string s) => Encoding.GetEncoding("ISO-8859-1").GetBytes(s);

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

        // An SST record from pre-encoded strings (see SstString) — cstTotal = cstUnique = count.
        public static byte[] SstRecord(params byte[][] strings)
        {
            var body = new MemoryStream();
            body.Write(BitConverter.GetBytes(strings.Length), 0, 4);
            body.Write(BitConverter.GetBytes(strings.Length), 0, 4);
            foreach (var s in strings) body.Write(s, 0, s.Length);
            return Record(0x00FC, body.ToArray());
        }

        // XLUnicodeRichExtendedString: high = 16-bit chars; cRun/cbExt add (skippable) run and
        // phonetic trailers to exercise the reader's skip logic.
        public static byte[] SstString(string s, bool high = false, int cRun = 0, int cbExt = 0)
        {
            var body = new MemoryStream();
            body.Write(BitConverter.GetBytes((ushort)s.Length), 0, 2);
            var grbit = (byte)((high ? 0x01 : 0) | (cRun > 0 ? 0x08 : 0) | (cbExt > 0 ? 0x04 : 0));
            body.WriteByte(grbit);
            if (cRun > 0) body.Write(BitConverter.GetBytes((ushort)cRun), 0, 2);
            if (cbExt > 0) body.Write(BitConverter.GetBytes((uint)cbExt), 0, 4);
            var chars = high ? Encoding.Unicode.GetBytes(s) : Latin1(s);
            body.Write(chars, 0, chars.Length);
            if (cRun > 0) body.Write(new byte[cRun * 4], 0, cRun * 4);   // run formatting (skipped)
            if (cbExt > 0) body.Write(new byte[cbExt], 0, cbExt);        // phonetic data (skipped)
            return body.ToArray();
        }

        // One string split across a CONTINUE where the encoding flips compressed -> 16-bit.
        public static byte[] SplitSstMixed(string part1, string part2)
        {
            var sst = new MemoryStream();
            sst.Write(BitConverter.GetBytes(1), 0, 4);
            sst.Write(BitConverter.GetBytes(1), 0, 4);
            sst.Write(BitConverter.GetBytes((ushort)(part1.Length + part2.Length)), 0, 2);
            sst.WriteByte(0);                            // compressed
            var a = Latin1(part1); sst.Write(a, 0, a.Length);

            var cont = new MemoryStream();
            cont.WriteByte(0x01);                        // continuation is 16-bit
            var b = Encoding.Unicode.GetBytes(part2); cont.Write(b, 0, b.Length);

            return Concat(Record(0x00FC, sst.ToArray()), Record(0x003C, cont.ToArray()));
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

        // A document from several pieces (each 8-bit compressed or 16-bit), plus optional text
        // "beyond" ccpText (e.g. a footnote) that must be excluded. tableStream1 flips the FIB flag
        // so the piece table is read from 1Table instead of 0Table.
        public static (byte[] wordDoc, byte[] table) BuildDoc(
            bool tableStream1, (string text, bool compressed)[] pieces, string? beyond = null)
        {
            const int textStart = 0x1AA;
            var all = new List<(string text, bool compressed)>(pieces);
            var ccpText = pieces.Sum(p => p.text.Length);
            if (beyond != null) all.Add((beyond, true));
            var cch = all.Sum(p => p.text.Length);

            var region = new MemoryStream();
            var infos = new List<(int cp, int offset, bool compressed)>();
            var cp = 0;
            foreach (var (text, compressed) in all)
            {
                var offset = textStart + (int)region.Length;
                infos.Add((cp, offset, compressed));
                var bytes = compressed ? Latin1(text) : Encoding.Unicode.GetBytes(text);
                region.Write(bytes, 0, bytes.Length);
                cp += text.Length;
            }
            var textBytes = region.ToArray();

            var plc = new MemoryStream();
            foreach (var (c, _, _) in infos) plc.Write(BitConverter.GetBytes((uint)c), 0, 4);
            plc.Write(BitConverter.GetBytes((uint)cch), 0, 4);          // final CP
            foreach (var (_, offset, compressed) in infos)
            {
                plc.Write(BitConverter.GetBytes((ushort)0), 0, 2);     // PCD flags
                var fc = compressed ? (uint)(offset * 2) | 0x40000000u : (uint)offset;
                plc.Write(BitConverter.GetBytes(fc), 0, 4);
                plc.Write(BitConverter.GetBytes((ushort)0), 0, 2);     // prm
            }
            var plcBytes = plc.ToArray();

            var clx = new MemoryStream();
            clx.WriteByte(0x02);                                       // clxt = Pcdt
            clx.Write(BitConverter.GetBytes((uint)plcBytes.Length), 0, 4);
            clx.Write(plcBytes, 0, plcBytes.Length);
            var table = clx.ToArray();

            var wd = new byte[textStart + textBytes.Length];
            Put16(wd, 0x00, 0xA5EC);
            Put16(wd, 0x02, 0x00C1);
            if (tableStream1) Put16(wd, 0x0A, 0x0200);                 // fWhichTblStm -> 1Table
            Put16(wd, 0x20, 14);
            Put16(wd, 0x3E, 22);
            Put32(wd, 0x4C, (uint)ccpText);
            Put16(wd, 0x98, 34);
            Put32(wd, 0x1A2, 0);                                       // fcClx
            Put32(wd, 0x1A6, (uint)table.Length);                      // lcbClx
            Array.Copy(textBytes, 0, wd, textStart, textBytes.Length);
            return (wd, table);
        }

        private static byte[] Latin1(string s) => Encoding.GetEncoding("ISO-8859-1").GetBytes(s);

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
            const int cutoff = 4096;

            // Small streams live in the mini stream; large streams (>= 4096) get their own run of
            // regular 512-byte FAT sectors — exercising the real-Office-file read path.
            var mini = new MemoryStream();
            var miniFat = new List<uint>();
            var isMini = new bool[_streams.Count];
            var starts = new uint[_streams.Count];
            for (var s = 0; s < _streams.Count; s++)
            {
                var data = _streams[s].data;
                if (data.Length >= cutoff) continue;
                var sectors = Math.Max(1, (data.Length + MiniSector - 1) / MiniSector);
                isMini[s] = true;
                starts[s] = (uint)miniFat.Count;
                for (var i = 0; i < sectors; i++)
                    miniFat.Add(i == sectors - 1 ? EndOfChain : (uint)(miniFat.Count + 1));
                mini.Write(data, 0, data.Length);
                var pad = sectors * MiniSector - data.Length;
                if (pad > 0) mini.Write(new byte[pad], 0, pad);
            }
            var miniStream = mini.ToArray();

            var numEntries = _streams.Count + 1;
            var dirSectors = (numEntries + 3) / 4;
            var miniStreamSectors = (miniStream.Length + Sector - 1) / Sector;

            var firstDir = 1u;
            var firstMiniFat = firstDir + (uint)dirSectors;
            var firstMiniStream = firstMiniFat + 1;
            var next = firstMiniStream + (uint)miniStreamSectors;

            var regular = new List<(uint start, byte[] data, int sectors)>();
            for (var s = 0; s < _streams.Count; s++)
            {
                if (isMini[s]) continue;
                var data = _streams[s].data;
                var sectors = Math.Max(1, (data.Length + Sector - 1) / Sector);
                starts[s] = next;
                regular.Add((next, data, sectors));
                next += (uint)sectors;
            }

            var dir = new byte[dirSectors * Sector];
            var rootStart = miniStreamSectors > 0 ? firstMiniStream : EndOfChain;
            WriteDirEntry(dir, 0, "Root Entry", 5, rootStart, (ulong)miniStream.Length);
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
            foreach (var (start, _, sectors) in regular)
                for (var i = 0; i < sectors; i++)
                    fat[start + i] = i == sectors - 1 ? EndOfChain : start + (uint)i + 1;

            var miniFatSector = new uint[Sector / 4];
            for (var i = 0; i < miniFatSector.Length; i++)
                miniFatSector[i] = i < miniFat.Count ? miniFat[i] : FreeSect;

            var file = new MemoryStream();
            file.Write(Header(firstDir, firstMiniFat), 0, Sector);
            file.Write(UintSector(fat), 0, Sector);
            file.Write(dir, 0, dir.Length);
            file.Write(UintSector(miniFatSector), 0, Sector);
            var miniPadded = new byte[miniStreamSectors * Sector];
            Array.Copy(miniStream, miniPadded, miniStream.Length);
            file.Write(miniPadded, 0, miniPadded.Length);
            foreach (var (_, data, sectors) in regular)
            {
                var padded = new byte[sectors * Sector];
                Array.Copy(data, padded, data.Length);
                file.Write(padded, 0, padded.Length);
            }
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
