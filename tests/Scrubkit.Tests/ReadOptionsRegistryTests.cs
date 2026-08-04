// Copyright © 2026 jjopensoftworks-blip

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Scrubkit.Tests;

public class ReadOptionsRegistryTests
{
    private class TestExtractor : IFileExtractor
    {
        public bool CanHandle(string extension) => extension.Equals(".test", StringComparison.OrdinalIgnoreCase);
        public ExtractedContent Extract(string path) => new ExtractedContent(new Dictionary<string, string>(), "test");
    }

    [Fact]
    public void AddExtractor_Instance_RegistersExtractor()
    {
        var options = new ReadOptions();
        var extractor = new TestExtractor();

        options.AddExtractor(extractor);

        Assert.Contains(extractor, options.Extractors);
    }

    [Fact]
    public void AddExtractor_Generic_RegistersExtractor()
    {
        var options = new ReadOptions();

        options.AddExtractor<TestExtractor>();

        Assert.Contains(options.Extractors, e => e is TestExtractor);
    }

    [Fact]
    public void AddExtractor_DuplicateInstance_IsIgnored()
    {
        var options = new ReadOptions();
        var extractor = new TestExtractor();

        options.AddExtractor(extractor);
        options.AddExtractor(extractor);

        Assert.Single(options.Extractors);
    }
}
