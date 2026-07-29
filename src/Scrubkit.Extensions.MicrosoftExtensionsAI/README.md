# Scrubkit.Extensions.MicrosoftExtensionsAI

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Extensions.MicrosoftExtensionsAI.svg)](https://www.nuget.org/packages/Scrubkit.Extensions.MicrosoftExtensionsAI)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

`Microsoft.Extensions.AI` integration for [**Scrubkit**](https://www.nuget.org/packages/Scrubkit) — local, offline PII and secret redaction middleware for `IChatClient` and `IEmbeddingGenerator`.

It runs fully offline inside your process to scan and mask sensitive data before it is sent to external/cloud AI models.

## Install

```sh
dotnet add package Scrubkit.Extensions.MicrosoftExtensionsAI
```

## Chat Client Redaction

Wrap your `IChatClient` or construct it with a `ChatClientBuilder` pipeline to redact PII and secrets (like API keys, JWTs, connection strings) from user prompts automatically.

### Option A: Using the Fluent Builder

```csharp
using Microsoft.Extensions.AI;
using Scrubkit;

IChatClient chatClient = new ChatClientBuilder()
    .UseRedaction(new StandardRedactor()) // Intercepts and redacts prompts locally
    .UseChatClient(new OllamaChatClient(new Uri("http://localhost:11434"), "llama3"));
```

### Option B: Wrapping an Existing Client

```csharp
using Microsoft.Extensions.AI;
using Scrubkit;

IChatClient rawClient = new OpenAIInternalClient(...);
IChatClient secureClient = rawClient.UseRedaction(new StandardRedactor());
```

### Configuration: Redacting All Messages vs. User Messages Only

By default, only messages with the `ChatRole.User` role are redacted, leaving system instruction prompts intact. You can configure the client to redact all messages:

```csharp
chatClient.UseRedaction(new StandardRedactor(), redactUserMessagesOnly: false);
```

---

## Embedding Generator Redaction

Wrap an `IEmbeddingGenerator<string, TEmbedding>` to scrub inputs before they are sent to the embedding engine, ensuring sensitive text isn't embedded or transmitted.

### Option A: Using the Fluent Builder

```csharp
using Microsoft.Extensions.AI;
using Scrubkit;

IEmbeddingGenerator<string, Embedding<float>> secureGenerator = 
    new EmbeddingGeneratorBuilder<string, Embedding<float>>(baseGenerator)
        .UseRedaction(new StandardRedactor())
        .Build();
```

### Option B: Wrapping an Existing Generator

```csharp
using Microsoft.Extensions.AI;
using Scrubkit;

IEmbeddingGenerator<string, Embedding<float>> secureGenerator = 
    baseGenerator.UseRedaction(new StandardRedactor());
```

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
