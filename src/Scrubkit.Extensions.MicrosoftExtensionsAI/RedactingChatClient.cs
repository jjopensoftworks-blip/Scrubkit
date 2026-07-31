using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace Scrubkit
{
    /// <summary>
    /// A delegating chat client that redacts PII and secrets from chat messages before passing them to the inner chat client.
    /// </summary>
    public class RedactingChatClient : DelegatingChatClient
    {
        private readonly IRedactor _redactor;
        private readonly bool _redactUserMessagesOnly;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedactingChatClient"/> class.
        /// </summary>
        /// <param name="innerClient">The underlying chat client.</param>
        /// <param name="redactor">The redactor used to scan and mask sensitive information.</param>
        /// <param name="redactUserMessagesOnly">If <c>true</c>, only redacts messages where <see cref="ChatMessage.Role"/> is <see cref="ChatRole.User"/>. Otherwise, redacts all text messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerClient"/> or <paramref name="redactor"/> is null.</exception>
        public RedactingChatClient(IChatClient innerClient, IRedactor redactor, bool redactUserMessagesOnly = true)
            : base(innerClient)
        {
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _redactUserMessagesOnly = redactUserMessagesOnly;
        }

        /// <inheritdoc />
        public override async Task<ChatCompletion> CompleteAsync(
            IList<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (chatMessages == null)
            {
                throw new ArgumentNullException(nameof(chatMessages));
            }

            foreach (var message in chatMessages)
            {
                if (!_redactUserMessagesOnly || message.Role == ChatRole.User)
                {
                    if (message.Text is string text && !string.IsNullOrEmpty(text))
                    {
                        var result = _redactor.Redact(text);
                        message.Text = result.Text;
                    }
                }
            }

            return await base.CompleteAsync(chatMessages, options, cancellationToken);
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
            IList<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (chatMessages == null)
            {
                throw new ArgumentNullException(nameof(chatMessages));
            }

            foreach (var message in chatMessages)
            {
                if (!_redactUserMessagesOnly || message.Role == ChatRole.User)
                {
                    if (message.Text is string text && !string.IsNullOrEmpty(text))
                    {
                        var result = _redactor.Redact(text);
                        message.Text = result.Text;
                    }
                }
            }

            return base.CompleteStreamingAsync(chatMessages, options, cancellationToken);
        }
    }
}
