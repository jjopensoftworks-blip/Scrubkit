// Copyright © 2026 jjopensoftworks-blip

using System.IO;
using Xunit;

namespace Scrubkit.Email.Tests;

public class EmailTheoryTests
{
    [Theory]
    [InlineData(".eml", true)]
    [InlineData(".msg", true)]
    [InlineData(".txt", false)]
    [InlineData(".pdf", false)]
    [InlineData(".docx", false)]
    public void EmailExtractor_CanHandleExtensions_ReturnsExpected(string ext, bool expected)
    {
        var emailExtractor = new EmailExtractor();
        var msgExtractor = new MsgExtractor();

        bool canHandle = emailExtractor.CanHandle(ext) || msgExtractor.CanHandle(ext);

        Assert.Equal(expected, canHandle);
    }

    [Theory]
    [InlineData("From: alice@example.com\r\nTo: bob@example.com\r\nSubject: Meeting\r\n\r\nHello Bob", "alice@example.com", "bob@example.com", "Meeting")]
    [InlineData("From: support@tech.io\r\nTo: user@domain.net\r\nSubject: Ticket Update\r\n\r\nResolved", "support@tech.io", "user@domain.net", "Ticket Update")]
    [InlineData("From: news@site.com\r\nTo: sub@site.com\r\nSubject: Weekly Digest\r\n\r\nContent", "news@site.com", "sub@site.com", "Weekly Digest")]
    public void EmailExtractor_ExtractHeaders_ParsesMimeHeaders(string rawMime, string expectedFrom, string expectedTo, string expectedSubject)
    {
        var extractor = new EmailExtractor();
        var tempFile = Path.GetTempFileName() + ".eml";
        try
        {
            File.WriteAllText(tempFile, rawMime);
            var result = extractor.Extract(tempFile);

            Assert.Equal(expectedFrom, result.Metadata["From"]);
            Assert.Equal(expectedTo, result.Metadata["To"]);
            Assert.Equal(expectedSubject, result.Metadata["Subject"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
