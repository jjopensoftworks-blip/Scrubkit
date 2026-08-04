// Copyright © 2026 jjopensoftworks-blip

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Scrubkit.Tests;

public class FolderScrubberOptionsTheoryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task ReadFolder_Parallelism_ExecutesSuccessfully(int degree)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "scrubkit_parallel_" + Guid.NewGuid());
        Directory.CreateDirectory(tempFolder);
        try
        {
            for (int i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(tempFolder, $"doc_{i}.txt"), $"Content for document {i}");
            }

            var options = new ReadOptions
            {
                MaxDegreeOfParallelism = degree,
                Recursion = Recursion.TopOnly
            };
            var scrubber = new FolderScrubber(options);
            var records = await scrubber.ReadAsync(tempFolder);

            Assert.Equal(10, records.Count);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    [InlineData(5, 5)]
    [InlineData(100, 15)]
    public async Task ReadFolder_MaxTextLength_ClipsExtractedText(int maxLength, int textLen)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "scrubkit_cliplen_" + Guid.NewGuid());
        Directory.CreateDirectory(tempFolder);
        try
        {
            var text = new string('A', textLen);
            File.WriteAllText(Path.Combine(tempFolder, "sample.txt"), text);

            var options = new ReadOptions
            {
                MaxTextLength = maxLength,
                Recursion = Recursion.TopOnly
            };
            var scrubber = new FolderScrubber(options);
            var records = await scrubber.ReadAsync(tempFolder);

            Assert.Single(records);
            Assert.True(records[0].Text.Length <= maxLength);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadFolder_ComputeContentHash_GeneratesHashWhenRequested(bool computeHash)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "scrubkit_hash_" + Guid.NewGuid());
        Directory.CreateDirectory(tempFolder);
        try
        {
            File.WriteAllText(Path.Combine(tempFolder, "sample.txt"), "Sample hashing text");

            var options = new ReadOptions
            {
                ComputeContentHash = computeHash,
                Recursion = Recursion.TopOnly
            };
            var scrubber = new FolderScrubber(options);
            var records = await scrubber.ReadAsync(tempFolder);

            Assert.Single(records);
            if (computeHash)
            {
                Assert.NotNull(records[0].ContentHash);
                Assert.Equal(64, records[0].ContentHash!.Length);
            }
            else
            {
                Assert.Null(records[0].ContentHash);
            }
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData("txt", true)]
    [InlineData(".PDF", false)]
    [InlineData(".md", false)]
    public async Task ReadFolder_IncludeExtensions_FiltersFiles(string ext, bool expectMatch)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "scrubkit_extfilter_" + Guid.NewGuid());
        Directory.CreateDirectory(tempFolder);
        try
        {
            File.WriteAllText(Path.Combine(tempFolder, "test.txt"), "Text content");

            var options = new ReadOptions
            {
                Recursion = Recursion.TopOnly
            };
            options.IncludeExtensions.Add(ext);

            var scrubber = new FolderScrubber(options);
            var records = await scrubber.ReadAsync(tempFolder);

            if (expectMatch)
                Assert.Single(records);
            else
                Assert.Empty(records);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }
    }
}
