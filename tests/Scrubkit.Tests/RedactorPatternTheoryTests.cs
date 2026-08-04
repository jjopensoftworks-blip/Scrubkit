// Copyright © 2026 jjopensoftworks-blip

using Xunit;

namespace Scrubkit.Tests;

public class RedactorPatternTheoryTests
{
    private readonly StandardRedactor _redactor = new(new StandardRedactorOptions());

    [Theory]
    [InlineData("user@example.com", "[EMAIL]")]
    [InlineData("john.doe+tag@subdomain.domain.co.uk", "[EMAIL]")]
    [InlineData("firstname.lastname@company.org", "[EMAIL]")]
    [InlineData("admin@testdomain.com", "[EMAIL]")]
    [InlineData("contact_us_2026@test-domain.io", "[EMAIL]")]
    [InlineData("support@service.net", "[EMAIL]")]
    [InlineData("dev.team@tech.dev", "[EMAIL]")]
    [InlineData("sales-dept@enterprise.com", "[EMAIL]")]
    [InlineData("info@startup.ai", "[EMAIL]")]
    [InlineData("user12345@domain.app", "[EMAIL]")]
    [InlineData("Send email to alice@example.com for help.", "Send email to [EMAIL] for help.")]
    [InlineData("CC: bob@company.org, charlie@company.org", "CC: [EMAIL], [EMAIL]")]
    [InlineData("Contact <david@site.net> immediately.", "Contact <[EMAIL]> immediately.")]
    [InlineData("Reply-To: eve@mail.com", "Reply-To: [EMAIL]")]
    [InlineData("mailto:frank@domain.com", "mailto:[EMAIL]")]
    public void Redact_Emails_ReplacesWithPlaceholder(string input, string expected)
    {
        var result = _redactor.Redact(input).Text;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Call 1-800-555-0199 now", "Call [PHONE] now")]
    [InlineData("Phone: 555-123-4567", "Phone: [PHONE]")]
    [InlineData("Tel: (555) 234-5678", "Tel: [PHONE]")]
    [InlineData("Cell: 555.345.6789", "Cell: [PHONE]")]
    [InlineData("Direct: +1 (555) 456-7890", "Direct: [PHONE]")]
    [InlineData("Ring +1-555-678-9012 for support.", "Ring [PHONE] for support.")]
    [InlineData("Call (800) 555-0100 or (800) 555-0200.", "Call [PHONE] or [PHONE].")]
    [InlineData("Fax: +1-888-555-1212", "Fax: [PHONE]")]
    [InlineData("Main: 800.555.4321", "Main: [PHONE]")]
    [InlineData("Helpdesk: 1 (800) 555-1111", "Helpdesk: [PHONE]")]
    [InlineData("Line 1: 555-0101, Line 2: 555-0102", "Line 1: [PHONE], Line 2: [PHONE]")]
    [InlineData("Reach us at 1-800-555-7777 during hours.", "Reach us at [PHONE] during hours.")]
    public void Redact_Phones_ReplacesWithPlaceholder(string input, string expected)
    {
        var result = _redactor.Redact(input).Text;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("SSN: 000-12-3456", "SSN: [SSN]")]
    [InlineData("Tax ID: 123-45-6789", "Tax ID: [SSN]")]
    [InlineData("SSN is 987-65-4321.", "SSN is [SSN].")]
    [InlineData("ID: 111-22-3333", "ID: [SSN]")]
    [InlineData("Social Security: 456-78-9012", "Social Security: [SSN]")]
    [InlineData("Card: 4111-1111-1111-1111", "Card: [CARD]")]
    [InlineData("Visa: 4111 1111 1111 1111", "Visa: [CARD]")]
    [InlineData("MC: 5555-5555-5555-4444", "MC: [CARD]")]
    [InlineData("Amex: 3782-822463-10005", "Amex: [CARD]")]
    [InlineData("Card 4111-1111-1111-1111 on file.", "Card [CARD]on file.")]
    [InlineData("Pay with 5105 1051 0510 5100.", "Pay with [CARD].")]
    [InlineData("SSN 000-11-2222 and Card 4111-1111-1111-1111", "SSN [SSN] and Card [CARD]")]
    public void Redact_SSNAndCards_ReplacesWithPlaceholder(string input, string expected)
    {
        var result = _redactor.Redact(input).Text;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("IP: 192.168.1.1", "IP: [IP]")]
    [InlineData("Host: 10.0.0.254", "Host: [IP]")]
    [InlineData("Gateway: 172.16.254.1", "Gateway: [IP]")]
    [InlineData("Server 8.8.8.8 active.", "Server [IP] active.")]
    [InlineData("DNS 1.1.1.1 connected.", "DNS [IP] connected.")]
    [InlineData("Logged from 192.168.0.10", "Logged from [IP]")]
    [InlineData("Client 127.0.0.1 connected", "Client [IP] connected")]
    [InlineData("Router 192.168.1.254 status ok", "Router [IP] status ok")]
    [InlineData("Primary 8.8.4.4, Secondary 1.0.0.1", "Primary [IP], Secondary [IP]")]
    public void Redact_IPAddresses_ReplacesWithPlaceholder(string input, string expected)
    {
        var result = _redactor.Redact(input).Text;
        Assert.Equal(expected, result);
    }
}
