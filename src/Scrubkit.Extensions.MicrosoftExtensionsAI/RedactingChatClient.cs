// Copyright © 2026 jjopensoftworks-blip

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
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            RedactMessages(messages);

            return await base.GetResponseAsync(messages, options, cancellationToken);
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (messages == null)
            {
                throw new ArgumentNullException(nameof(messages));
            }

            RedactMessages(messages);

            return base.GetStreamingResponseAsync(messages, options, cancellationToken);
        }

        private void RedactMessages(IEnumerable<ChatMessage> messages)
        {
            foreach (var message in messages)
            {
                if (_redactUserMessagesOnly && message.Role != ChatRole.User)
                {
                    continue;
                }

                foreach (var content in message.Contents)
                {
                    if (content is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
                    {
                        textContent.Text = _redactor.Redact(textContent.Text).Text;
                    }
                }
            }
        }
    }
}
