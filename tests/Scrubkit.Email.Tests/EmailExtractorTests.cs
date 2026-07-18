using Scrubkit;
using Xunit;

namespace Scrubkit.Email.Tests;

public class EmailExtractorTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "scrubkit-email-" + Guid.NewGuid().ToString("N"));

    public EmailExtractorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    // Writes an .eml with CRLF line endings (as real mail uses) and returns its path.
    private string Eml(string name, string content) =>
        Write(name, content.Replace("\r\n", "\n").Replace("\n", "\r\n"));

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Theory]
    [InlineData(".eml", true)]
    [InlineData(".msg", false)]
    [InlineData(".txt", false)]
    [InlineData(".EML", false)]   // caller passes a normalized lower-case extension
    public void CanHandle_matches_only_eml(string ext, bool expected) =>
        Assert.Equal(expected, new EmailExtractor().CanHandle(ext));

    [Fact]
    public void Plain_email_yields_headers_and_body()
    {
        var path = Eml("plain.eml",
            "From: Alice <alice@example.com>\n" +
            "To: bob@example.com\n" +
            "Subject: Lunch tomorrow\n" +
            "Date: Mon, 14 Jul 2026 09:00:00 +0000\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "\n" +
            "Hi Bob, want to grab lunch tomorrow?\n");

        var c = new EmailExtractor().Extract(path);

        Assert.Equal("Alice <alice@example.com>", c.Metadata["From"]);
        Assert.Equal("bob@example.com", c.Metadata["To"]);
        Assert.Equal("Lunch tomorrow", c.Metadata["Subject"]);
        Assert.Equal("Mon, 14 Jul 2026 09:00:00 +0000", c.Metadata["Date"]);
        Assert.Contains("grab lunch tomorrow", c.Text);
    }

    [Fact]
    public void Folded_header_is_unfolded()
    {
        var path = Eml("folded.eml",
            "Subject: A rather long subject line that\n" +
            " wraps across two source lines\n" +
            "\n" +
            "body");

        var c = new EmailExtractor().Extract(path);

        Assert.Equal("A rather long subject line that wraps across two source lines",
            c.Metadata["Subject"]);
    }

    [Fact]
    public void QuotedPrintable_body_is_decoded()
    {
        var path = Eml("qp.eml",
            "Subject: Invoice\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "Content-Transfer-Encoding: quoted-printable\n" +
            "\n" +
            "Total is =E2=82=AC5 =\n" +
            "today.\n");   // trailing '=' is a soft line break

        var c = new EmailExtractor().Extract(path);

        Assert.Contains("Total is €5 today.", c.Text);
    }

    [Fact]
    public void Base64_body_is_decoded()
    {
        // "Hello, world!" in UTF-8, base64-encoded.
        var path = Eml("b64.eml",
            "Subject: Greetings\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "Content-Transfer-Encoding: base64\n" +
            "\n" +
            "SGVsbG8sIHdvcmxkIQ==\n");

        var c = new EmailExtractor().Extract(path);

        Assert.Contains("Hello, world!", c.Text);
    }

    [Fact]
    public void Rfc2047_subject_is_decoded()
    {
        var path = Eml("encoded.eml",
            "Subject: =?utf-8?B?w4Viw6ljYXRpb24=?= and =?utf-8?Q?caf=C3=A9?=\n" +
            "\n" +
            "body");

        var c = new EmailExtractor().Extract(path);

        Assert.Contains("café", c.Metadata["Subject"]);
    }

    [Fact]
    public void Multipart_alternative_prefers_plain_text()
    {
        var path = Eml("alt.eml",
            "Subject: Newsletter\n" +
            "Content-Type: multipart/alternative; boundary=\"BOUND\"\n" +
            "\n" +
            "--BOUND\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "\n" +
            "The plain version.\n" +
            "--BOUND\n" +
            "Content-Type: text/html; charset=utf-8\n" +
            "\n" +
            "<p>The <b>HTML</b> version.</p>\n" +
            "--BOUND--\n");

        var c = new EmailExtractor().Extract(path);

        Assert.Contains("The plain version.", c.Text);
        Assert.DoesNotContain("<b>", c.Text);          // html part not used when plain exists
    }

    [Fact]
    public void Html_only_body_is_used_as_fallback()
    {
        var path = Eml("html.eml",
            "Subject: HTML only\n" +
            "Content-Type: multipart/alternative; boundary=\"B\"\n" +
            "\n" +
            "--B\n" +
            "Content-Type: text/html; charset=utf-8\n" +
            "\n" +
            "<p>Only HTML here.</p>\n" +
            "--B--\n");

        var c = new EmailExtractor().Extract(path);

        Assert.Contains("Only HTML here.", c.Text);
    }

    [Fact]
    public void Attachment_part_is_skipped()
    {
        var path = Eml("attach.eml",
            "Subject: With attachment\n" +
            "Content-Type: multipart/mixed; boundary=\"MIX\"\n" +
            "\n" +
            "--MIX\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "\n" +
            "See the attached file.\n" +
            "--MIX\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "Content-Disposition: attachment; filename=\"secret.txt\"\n" +
            "\n" +
            "TOP SECRET ATTACHMENT CONTENT\n" +
            "--MIX--\n");

        var c = new EmailExtractor().Extract(path);

        Assert.Contains("See the attached file.", c.Text);
        Assert.DoesNotContain("TOP SECRET", c.Text);
    }

    [Fact]
    public async Task Routes_through_FolderScrubber_as_an_email_row()
    {
        Eml("msg.eml",
            "From: sender@example.com\n" +
            "Subject: Routed\n" +
            "Content-Type: text/plain; charset=utf-8\n" +
            "\n" +
            "Delivered through the scrubber.\n");

        var options = new ReadOptions();
        options.Extractors.Add(new EmailExtractor());

        var table = await new FolderScrubber(options).ReadAsync(_dir);

        var row = Assert.Single(table);
        Assert.Equal("Email", row.TypeBucket);
        Assert.Equal("Routed", row.Metadata["Subject"]);
        Assert.Contains("Delivered through the scrubber.", row.Text);
        Assert.Empty(row.Warnings);
    }
}
