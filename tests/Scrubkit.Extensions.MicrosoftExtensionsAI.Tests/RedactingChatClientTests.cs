// Copyright © 2026 jjopensoftworks-blip

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Xunit;

namespace Scrubkit.Tests
{
    public class RedactingChatClientTests
    {
        private class TestChatClient : IChatClient
        {
            public IList<ChatMessage>? LastMessagesSent { get; private set; }

            public Task<ChatResponse> GetResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                LastMessagesSent = new List<ChatMessage>(messages);
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello!")));
            }

            public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                LastMessagesSent = new List<ChatMessage>(messages);
                yield return new ChatResponseUpdate(ChatRole.Assistant, "Hello!");
            }

            public object? GetService(Type serviceType, object? serviceKey = null) => null;

            public void Dispose() { }
        }

        [Fact]
        public async Task GetResponseAsync_RedactsUserMessages_WhenUserOnlyIsTrue()
        {
            // Arrange
            var inner = new TestChatClient();
            var redactor = new StandardRedactor();
            var client = new RedactingChatClient(inner, redactor, redactUserMessagesOnly: true);

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "My email is admin@example.com"),
                new ChatMessage(ChatRole.User, "Hello, email me at test@example.com please"),
                new ChatMessage(ChatRole.Assistant, "My email is support@example.com")
            };

            // Act
            await client.GetResponseAsync(messages);

            // Assert
            Assert.NotNull(inner.LastMessagesSent);
            // System message should NOT be redacted since UserOnly = true
            Assert.Equal("My email is admin@example.com", inner.LastMessagesSent[0].Text);
            // User message SHOULD be redacted
            Assert.Contains("[EMAIL]", inner.LastMessagesSent[1].Text);
            Assert.DoesNotContain("test@example.com", inner.LastMessagesSent[1].Text);
            // Assistant message should NOT be redacted
            Assert.Equal("My email is support@example.com", inner.LastMessagesSent[2].Text);
        }

        [Fact]
        public async Task GetResponseAsync_RedactsAllMessages_WhenUserOnlyIsFalse()
        {
            // Arrange
            var inner = new TestChatClient();
            var redactor = new StandardRedactor();
            var client = new RedactingChatClient(inner, redactor, redactUserMessagesOnly: false);

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "My email is admin@example.com"),
                new ChatMessage(ChatRole.User, "Hello, email me at test@example.com please")
            };

            // Act
            await client.GetResponseAsync(messages);

            // Assert
            Assert.NotNull(inner.LastMessagesSent);
            // System message SHOULD be redacted since UserOnly = false
            Assert.Contains("[EMAIL]", inner.LastMessagesSent[0].Text);
            Assert.DoesNotContain("admin@example.com", inner.LastMessagesSent[0].Text);
            // User message SHOULD be redacted
            Assert.Contains("[EMAIL]", inner.LastMessagesSent[1].Text);
            Assert.DoesNotContain("test@example.com", inner.LastMessagesSent[1].Text);
        }

        [Fact]
        public async Task GetStreamingResponseAsync_RedactsUserMessages()
        {
            // Arrange
            var inner = new TestChatClient();
            var redactor = new StandardRedactor();
            var client = new RedactingChatClient(inner, redactor, redactUserMessagesOnly: true);

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, "Send to user@example.com")
            };

            // Act
            var stream = client.GetStreamingResponseAsync(messages);
            await foreach (var update in stream)
            {
                // Consume stream
            }

            // Assert
            Assert.NotNull(inner.LastMessagesSent);
            Assert.Contains("[EMAIL]", inner.LastMessagesSent[0].Text);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_OnNullArguments()
        {
            var inner = new TestChatClient();
            var redactor = new StandardRedactor();

            Assert.Throws<ArgumentNullException>(() => new RedactingChatClient(null!, redactor));
            Assert.Throws<ArgumentNullException>(() => new RedactingChatClient(inner, null!));
        }

        [Fact]
        public async Task UseRedaction_ExtensionMethods_WrapClientAndBuilder()
        {
            // Arrange
            var inner = new TestChatClient();
            var redactor = new StandardRedactor();

            // Client wrapping
            var wrappedClient = inner.UseRedaction(redactor);
            Assert.IsType<RedactingChatClient>(wrappedClient);

            // Builder wrapping
            var builtClient = new ChatClientBuilder(inner)
                .UseRedaction(redactor)
                .Build();

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, "Send to user@example.com")
            };

            await builtClient.GetResponseAsync(messages);

            Assert.NotNull(inner.LastMessagesSent);
            Assert.Contains("[EMAIL]", inner.LastMessagesSent[0].Text);
        }
    }
}
