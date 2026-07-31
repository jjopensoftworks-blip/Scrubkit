using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Scrubkit
{
    /// <summary>
    /// A delegating embedding generator that redacts PII and secrets from inputs before generating embeddings.
    /// </summary>
    /// <typeparam name="TEmbedding">The type of the embedding instance produced.</typeparam>
    public class RedactingEmbeddingGenerator<TEmbedding> : DelegatingEmbeddingGenerator<string, TEmbedding>
        where TEmbedding : Embedding
    {
        private readonly IRedactor _redactor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedactingEmbeddingGenerator{TEmbedding}"/> class.
        /// </summary>
        /// <param name="innerGenerator">The underlying embedding generator.</param>
        /// <param name="redactor">The redactor used to scan and mask sensitive information.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerGenerator"/> or <paramref name="redactor"/> is null.</exception>
        public RedactingEmbeddingGenerator(IEmbeddingGenerator<string, TEmbedding> innerGenerator, IRedactor redactor)
            : base(innerGenerator)
        {
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        /// <inheritdoc />
        public override async Task<GeneratedEmbeddings<TEmbedding>> GenerateAsync(
            IEnumerable<string> inputs,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            var redactedInputs = inputs.Select(input =>
                input != null ? _redactor.Redact(input).Text : string.Empty).ToList();

            return await base.GenerateAsync(redactedInputs, options, cancellationToken);
        }
    }
}
