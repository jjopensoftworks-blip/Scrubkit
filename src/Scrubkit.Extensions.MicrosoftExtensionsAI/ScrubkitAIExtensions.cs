using System;
using Microsoft.Extensions.AI;

namespace Scrubkit
{
    /// <summary>
    /// Extension methods for registering Scrubkit redaction middleware in Microsoft.Extensions.AI pipelines.
    /// </summary>
    public static class ScrubkitAIExtensions
    {
        /// <summary>
        /// Wraps the chat client with a <see cref="RedactingChatClient"/> to redact PII and secrets from prompts before they are sent.
        /// </summary>
        /// <param name="client">The chat client to wrap.</param>
        /// <param name="redactor">The redactor used to scan and mask sensitive information.</param>
        /// <param name="redactUserMessagesOnly">If <c>true</c>, only redacts messages where <see cref="ChatMessage.Role"/> is <see cref="ChatRole.User"/>. Otherwise, redacts all text messages.</param>
        /// <returns>A new <see cref="RedactingChatClient"/> wrapping the original client.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="client"/> or <paramref name="redactor"/> is null.</exception>
        public static IChatClient UseRedaction(
            this IChatClient client,
            IRedactor redactor,
            bool redactUserMessagesOnly = true)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (redactor == null) throw new ArgumentNullException(nameof(redactor));
            return new RedactingChatClient(client, redactor, redactUserMessagesOnly);
        }

        /// <summary>
        /// Adds a <see cref="RedactingChatClient"/> to the chat client builder pipeline.
        /// </summary>
        /// <param name="builder">The chat client builder.</param>
        /// <param name="redactor">The redactor used to scan and mask sensitive information.</param>
        /// <param name="redactUserMessagesOnly">If <c>true</c>, only redacts messages where <see cref="ChatMessage.Role"/> is <see cref="ChatRole.User"/>. Otherwise, redacts all text messages.</param>
        /// <returns>The updated chat client builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> or <paramref name="redactor"/> is null.</exception>
        public static ChatClientBuilder UseRedaction(
            this ChatClientBuilder builder,
            IRedactor redactor,
            bool redactUserMessagesOnly = true)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (redactor == null) throw new ArgumentNullException(nameof(redactor));
            return builder.Use(inner => new RedactingChatClient(inner, redactor, redactUserMessagesOnly));
        }

        /// <summary>
        /// Wraps the embedding generator with a <see cref="RedactingEmbeddingGenerator{TEmbedding}"/> to redact PII and secrets from inputs before generating embeddings.
        /// </summary>
        /// <typeparam name="TEmbedding">The type of the embedding instance produced.</typeparam>
        /// <param name="generator">The embedding generator to wrap.</param>
        /// <param name="redactor">The redactor used to scan and mask sensitive information.</param>
        /// <returns>A new <see cref="RedactingEmbeddingGenerator{TEmbedding}"/> wrapping the original generator.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="generator"/> or <paramref name="redactor"/> is null.</exception>
        public static IEmbeddingGenerator<string, TEmbedding> UseRedaction<TEmbedding>(
            this IEmbeddingGenerator<string, TEmbedding> generator,
            IRedactor redactor)
            where TEmbedding : Embedding
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (redactor == null) throw new ArgumentNullException(nameof(redactor));
            return new RedactingEmbeddingGenerator<TEmbedding>(generator, redactor);
        }

        /// <summary>
        /// Adds a <see cref="RedactingEmbeddingGenerator{TEmbedding}"/> to the embedding generator builder pipeline.
        /// </summary>
        /// <typeparam name="TEmbedding">The type of the embedding instance produced.</typeparam>
        /// <param name="builder">The embedding generator builder.</param>
        /// <param name="redactor">The redactor used to scan and mask sensitive information.</param>
        /// <returns>The updated embedding generator builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> or <paramref name="redactor"/> is null.</exception>
        public static EmbeddingGeneratorBuilder<string, TEmbedding> UseRedaction<TEmbedding>(
            this EmbeddingGeneratorBuilder<string, TEmbedding> builder,
            IRedactor redactor)
            where TEmbedding : Embedding
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (redactor == null) throw new ArgumentNullException(nameof(redactor));
            return builder.Use(inner => new RedactingEmbeddingGenerator<TEmbedding>(inner, redactor));
        }
    }
}
