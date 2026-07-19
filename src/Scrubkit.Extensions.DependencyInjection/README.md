# Scrubkit.Extensions.DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Extensions.DependencyInjection.svg)](https://www.nuget.org/packages/Scrubkit.Extensions.DependencyInjection)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

`Microsoft.Extensions.DependencyInjection` integration for
[**Scrubkit**](https://www.nuget.org/packages/Scrubkit) — register a configured
`FolderScrubber` for constructor injection in ASP.NET Core, worker services, and any other
generic host.

## Install

```sh
dotnet add package Scrubkit.Extensions.DependencyInjection
```

## Use it

```csharp
using Scrubkit;

builder.Services.AddScrubkit(options =>
{
    options.Recursion = Recursion.AllNested;
    options.MaxDegreeOfParallelism = 4;
    // options.Extractors.Add(new EmailExtractor());   // register add-ons
    // options.Redactor = new StandardRedactor();      // opt into redaction
});
```

Then inject it wherever you need it:

```csharp
public sealed class IngestService(FolderScrubber scrubber)
{
    public async Task RunAsync(string folder)
    {
        await foreach (var doc in scrubber.ReadStreamAsync(folder))
            // ... embed / index doc.Text + doc.Metadata ...
    }
}
```

`AddScrubkit` registers `FolderScrubber` as a **singleton** and is idempotent (calling it
more than once won't add duplicate registrations). It's fully offline — like the rest of
Scrubkit, nothing leaves your process.

### Wiring options from other services

Use the overload that hands you the `IServiceProvider` to pull DI-registered dependencies
(for example, your own `IRedactor`) into the options:

```csharp
builder.Services.AddSingleton<IRedactor, MyRedactor>();

builder.Services.AddScrubkit((sp, options) =>
{
    options.Redactor = sp.GetRequiredService<IRedactor>();
});
```

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
