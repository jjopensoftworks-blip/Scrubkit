// Copyright © 2026 jjopensoftworks-blip

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Xunit;

namespace Scrubkit.Tests
{
    public class RedactingEmbeddingGeneratorTests
    {
        private class TestEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
        {
            public IEnumerable<string>? LastInputs { get; private set; }

            public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
                IEnumerable<string> inputs,
                EmbeddingGenerationOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                LastInputs = inputs;
                var list = inputs.Select(_ => new Embedding<float>(new float[] { 0.1f, 0.2f })).ToList();
                return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(list));
            }

            public object? GetService(Type serviceType, object? serviceKey = null) => null;

            public void Dispose() { }
        }

        [Fact]
        public async Task GenerateAsync_RedactsInputsBeforeGeneratorRuns()
        {
            // Arrange
            var inner = new TestEmbeddingGenerator();
            var redactor = new StandardRedactor();
            var generator = new RedactingEmbeddingGenerator<Embedding<float>>(inner, redactor);

            var inputs = new[]
            {
                "Contact me at user@example.com for info",
                "Clean message with no PII"
            };

            // Act
            var result = await generator.GenerateAsync(inputs);

            // Assert
            Assert.NotNull(inner.LastInputs);
            var list = inner.LastInputs.ToList();
            Assert.Contains("[EMAIL]", list[0]);
            Assert.DoesNotContain("user@example.com", list[0]);
            Assert.Equal("Clean message with no PII", list[1]);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_OnNullArguments()
        {
            var inner = new TestEmbeddingGenerator();
            var redactor = new StandardRedactor();

            Assert.Throws<ArgumentNullException>(() => new RedactingEmbeddingGenerator<Embedding<float>>(null!, redactor));
            Assert.Throws<ArgumentNullException>(() => new RedactingEmbeddingGenerator<Embedding<float>>(inner, null!));
        }

        [Fact]
        public async Task UseRedaction_ExtensionMethods_WrapGeneratorAndBuilder()
        {
            // Arrange
            var inner = new TestEmbeddingGenerator();
            var redactor = new StandardRedactor();

            // Generator wrapping
            var wrappedGen = inner.UseRedaction(redactor);
            Assert.IsType<RedactingEmbeddingGenerator<Embedding<float>>>(wrappedGen);

            // Builder wrapping
            var builtGen = new EmbeddingGeneratorBuilder<string, Embedding<float>>(inner)
                .UseRedaction(redactor)
                .Build();

            var inputs = new[] { "Send an email to admin@example.com" };

            await builtGen.GenerateAsync(inputs);

            Assert.NotNull(inner.LastInputs);
            Assert.Contains("[EMAIL]", inner.LastInputs.First());
        }
    }
}
