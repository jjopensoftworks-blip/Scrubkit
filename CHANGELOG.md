# Changelog

<!--
One section per release, newest first. Each header shows a Stable/Pre-release badge, the
tag, and the date, then changes grouped as Features / Bug Fixes / Maintenance so readers
can tell at a glance whether a release adds things or just fixes them. Versions are cut
from Git tags via MinVer.
-->

## 1.8.0

![Pre-release](https://img.shields.io/badge/release-Unreleased-orange?style=flat-square) &nbsp; 🏷️ `v1.8.0` &nbsp;·&nbsp; 📅 Unreleased

&nbsp;

---

Clean text out of HTML and RTF — markup stripped, not dumped.

### 🚀 HTML & RTF clean-text extractors

- **`.html` / `.htm`** now route to a new **`HtmlExtractor`**: it drops `<script>`/`<style>`
  blocks and comments, strips tags, decodes HTML entities, and pulls the document
  `<title>` into metadata — instead of returning the raw markup.
- **`.rtf`** now routes to a new **`RtfExtractor`**: it strips control words and groups,
  expands `\'hh` and `\uN` escapes, and drops non-text destinations (font/colour tables,
  pictures, document metadata).
- Both are **zero-dependency** (BCL only) and register ahead of the plain-text reader, so
  the previous "read verbatim, markup included" behaviour for these three extensions is
  replaced. `.xml` is still read as-is.

### 🚀 Outlook `.msg` support (`Scrubkit.Email`)

- **`MsgExtractor`** reads Outlook **`.msg`** (OLE2 / compound-file) messages, completing the
  email family alongside `.eml` — no new package. `From` / `To` / `Cc` / `Subject` / `Date`
  become metadata and the body becomes text; it prefers the Unicode MAPI property streams and
  falls back to ANSI. A small built-in compound-file reader keeps the add-on **zero-dependency**
  (only `Scrubkit.Abstractions`). Register it via `ReadOptions.Extractors.Add(new MsgExtractor())`.

### 🚀 De-identification: stable tokens & format-preserving masks

- **`StandardRedactorOptions.StableTokens`** gives each masked value a deterministic suffix
  (`[EMAIL_3f9a1c8e]`) so identical values collapse to the same token and records stay
  **joinable** for analytics. **`TokenSalt`** mixes in a secret so tokens can't be correlated
  across corpora or recovered by hashing candidate values.
- **`RevealLast`** renders a **format-preserving mask** that keeps the last _n_ characters of a
  value per category — e.g. `RevealLast["Card"] = 4` turns `4111 1111 1111 1111` into
  `**** **** **** 1111` (separators preserved; `MaskChar` configurable).
- Both surface through the CLI `--rules` JSON (`stableTokens`, `tokenSalt`, `revealLast`,
  `maskChar`). Spans and per-category counts are unchanged; de-identification is best-effort,
  not a cryptographic guarantee.

## 1.7.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.7.0` &nbsp;·&nbsp; 📅 2026-07-22

&nbsp;

---

Bring your own redaction rules — no code required.

### 🚀 Custom redaction rules

- **`StandardRedactorOptions.CustomRules`** takes caller-defined **`CustomRedactionRule`**s
  (`{ Category, Pattern, Token?, IgnoreCase }`) — your own regexes, reported under your own
  categories. They run **before** the built-in patterns, so a domain rule (e.g. an employee-ID
  format) wins an overlap with a looser built-in. Patterns compile with a **match timeout**, so a
  runaway regex can't hang a scan; an invalid pattern throws at construction.
- **CLI `--rules <file>`** loads custom rules plus `allow` / `deny` / `disable` lists from JSON
  and implies redaction (`--redact=aggressive` still composes). JSON parsing lives in the CLI, so
  the core stays zero-dependency.

## 1.6.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.6.0` &nbsp;·&nbsp; 📅 2026-07-22

&nbsp;

---

A capability release for real pipelines: only reprocess what changed.

### 🚀 Incremental scans + a manifest

- **`FolderScrubber.ReadChangesAsync(root, baseline)`** scrubs only the files that changed since
  a baseline **`Manifest`** — added / modified files are extracted and returned in
  `IncrementalResult.Changed`, gone-from-disk paths in `Removed`, and a complete up-to-date
  `Manifest` (changed + carried-forward unchanged) comes back to persist for next time.
  Unchanged files (same size + last-write time) are **never re-extracted**.
- **`Manifest`** is a zero-dependency text sidecar (`Manifest.Save` / `Manifest.Load`, and
  `Manifest.From(records)` after a full run) — one line per file, no extra packages.
- **CLI:** `scrubkit scan <folder> --since <manifest> --manifest <manifest> --out delta.jsonl`
  emits only the changed files and rewrites the manifest. A missing baseline = first run.

### 🚀 Exclude paths

- **`ReadOptions.ExcludePaths`** skips files by full path — so a run never ingests its own
  output. The CLI adds its `--out` and `--manifest` files automatically.

### 🔧 Fixes

- **CLI writes UTF-8 without a BOM** for CSV / JSON / JSON Lines / manifest output (a leading
  BOM tripped some downstream parsers).

## 1.5.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.5.0` &nbsp;·&nbsp; 📅 2026-07-21

&nbsp;

---

The tool release: run it from a shell, scrub secrets, and chunk for RAG.

### 🚀 A command-line tool — `Scrubkit.Tool`

- **New package `Scrubkit.Tool`** — a **`dotnet tool`** (`dotnet tool install -g Scrubkit.Tool`)
  that runs Scrubkit from any shell or CI with zero code:
  `scrubkit scan <folder> [--redact[=standard|aggressive]] [--format csv|json|jsonl|parquet]
  [--out <file>] [--hash] [--include .pdf,.docx] [--no-recurse] [--max-files N] [--max-bytes N]
  [--max-text N] [--local-time]`. The table goes to stdout (or `--out`); progress and a summary
  go to stderr, so output pipes cleanly. Exit code `0` on success, `1` on a usage / I/O error.
  Bundles the whole family (core + Email / OpenDocument / EPUB add-ons + Parquet output), so it
  handles every supported format out of the box.

### 🚀 Secret / credential detection

- **`StandardRedactor` now detects secrets** in recognisable formats at the **Standard** level:
  **PEM private keys** (`[PRIVATE_KEY]`), **JWTs** (`[JWT]`), **credentialed connection strings**
  (`scheme://user:pass@host` → `[CONNECTION_STRING]`), and cloud / service credentials — AWS
  access keys, Google API keys, GitHub and Slack tokens (`[API_KEY]`).
- At the **Aggressive** level it additionally flags `key = value` credential **assignments**
  (password / secret / token / api-key …) and long **high-entropy tokens** (`[SECRET]`).
- New `RedactionCategories`: `PrivateKey`, `ApiKey`, `Jwt`, `ConnectionString`, `Secret`. All the
  usual controls apply (per-category disable, custom tokens, allow / deny lists).

### 🚀 RAG kit — chunking + JSON Lines

- **New `Chunker`** splits a `FileRecord`'s text into overlapping windows ready for embedding /
  retrieval, carrying the source path, name, type, and metadata onto each `Chunk` along with its
  position (`Index` / `Count` / `StartOffset`). Configurable window and overlap via `ChunkOptions`;
  boundaries snap to whitespace by default so words aren't cut.
- **`TableWriter` gains JSON Lines output** — `ToJsonLines` / `WriteJsonLines` for both
  `FileRecord`s and `Chunk`s (one JSON object per line), the streaming-friendly default for
  embedding and log pipelines.

## 1.4.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.4.0` &nbsp;·&nbsp; 📅 2026-07-20

&nbsp;

---

A capability release: turn the scrubbed table into output, watch the run, and hash content.

### 🚀 Output: CSV / JSON and a new Parquet package

- **`TableWriter`** (in the core, zero-dependency) serializes the record table to **CSV** or
  **JSON** — `TableWriter.ToCsv(table)` / `ToJson(table)`. CSV is a flat, RFC 4180-quoted
  summary; JSON carries the full record including text, metadata, redaction counts, warnings,
  and content hash.
- **`Modified` timestamps are written in UTC** (trailing `Z`) by default. Pass **`utc: false`**
  to emit machine-local time instead — always with an explicit offset (e.g.
  `2026-07-19T15:30:00+05:30`) so the value stays unambiguous.
- **New package `Scrubkit.Parquet`** writes the table to Apache **Parquet** via Parquet.Net
  (`ParquetTableWriter`), for data-lake / analytics ingestion. **net8.0-only**; the core stays
  zero-dependency. Parquet always stores `Modified` as a UTC instant.
- **New meta-package `Scrubkit.All`** references the whole family (core + all extractor add-ons
  + DI + Parquet) in one install — for consumers who'd rather not assemble packages by hand.
  Ships no code of its own; Parquet joins only on net8.0.

### 🚀 Diagnostics and an ILogger seam

- **`ReadOptions.OnDiagnostic`** is a dependency-free `Action<ScrubDiagnostic>` hook the core
  fires per file — a `read` event on success, or a warning code (`extract-failed`,
  `skipped-content`, `text-clipped`, `stat-failed`, `hash-failed`) on a problem.
- **`AddScrubkit`** bridges that hook to **`ILogger`** when a logger factory is present, so the
  core takes no logging dependency.

### 🚀 Content hashing

- Opt-in **`ReadOptions.ComputeContentHash`** populates **`FileRecord.ContentHash`** with the
  file's lower-case hex **SHA-256**, bounded by `MaxBytesPerFile` (oversized files are
  stat-skipped and never read).

### 🔧 Changes

- **Default `MaxBytesPerFile` lowered to 10 MB.** Files over the limit are stat-skipped with a
  `skipped-content` warning rather than read into memory.
- **`FileRecord.Modified` is now UTC** (`DateTimeKind.Utc`) — the canonical, unambiguous form;
  convert consumer-side with `Modified.ToLocalTime()` for a local view.

### 🐛 Bug fixes

- **Redaction no longer drops a valid match that overlaps a higher-priority span.** The
  single-pass engine now masks claimed characters in a working copy, so a looser later pattern
  can't reach into text a more specific one already took.

### 🧰 Maintenance

- CI coverage floor raised to **99%** (actual line coverage ~99.8%).
- Added a **Privacy & disclaimer** section to the site and package READMEs.

## 1.3.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.3.0` &nbsp;·&nbsp; 📅 2026-07-19

&nbsp;

---

Two more add-on packages, a DI integration, and a published throughput number.

### 🚀 New package: Scrubkit.Epub

- **`Scrubkit.Epub`** reads **`.epub`** e-books: the OPF `Title` / `Author` / `Subject`
  become metadata and the XHTML spine (tags stripped, entities decoded) becomes text, in
  reading order. Zero dependencies beyond `Scrubkit.Abstractions`; `.epub` files come back
  as `Document` rows.

### 🚀 New package: Scrubkit.Extensions.DependencyInjection

- **`services.AddScrubkit(…)`** registers a configured `FolderScrubber` as a singleton for
  ASP.NET Core, worker services, and other generic hosts. Configure recursion, limits,
  add-on extractors, and an optional redactor via a `ReadOptions` hook (with an
  `IServiceProvider` overload for DI-resolved dependencies).

### 🏎️ Benchmarks

- Added a **BenchmarkDotNet** throughput harness (`benchmarks/`). A representative run
  extracts on the order of **~12,000 files/sec** (4-way parallel; ~4,900 sequential) over a
  mixed text-file corpus.

### 🐛 Bug fixes

- **`.epub` files now report `TypeBucket = "Document"`.** `Buckets.For` didn't map the
  extension, so `.epub` files extracted fine but bucketed as `Other`.

## 1.2.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.2.0` &nbsp;·&nbsp; 📅 2026-07-18

&nbsp;

---

Introduces the first **add-on packages**, exercising the `IFileExtractor` extension seam
end to end. Both reference only `Scrubkit.Abstractions`, so they pull in no PDF or image
libraries; register either via `ReadOptions.Extractors`.

### 🚀 New package: Scrubkit.Email

- **`Scrubkit.Email`** ships an `EmailExtractor` that reads **`.eml`** (MIME) files: the
  `From` / `To` / `Cc` / `Subject` / `Date` headers become metadata and the message body
  becomes text. It handles multipart messages, `base64` and `quoted-printable` transfer
  encodings, common charsets, and RFC 2047 encoded-word headers — preferring the
  `text/plain` part and falling back to `text/html`. Attachments are skipped. `.eml` files
  come back as `TypeBucket = "Email"` rows.

### 🚀 New package: Scrubkit.OpenDocument

- **`Scrubkit.OpenDocument`** ships an `OpenDocumentExtractor` that reads OpenDocument
  Format files from LibreOffice / OpenOffice — text documents (**`.odt`**), spreadsheets
  (**`.ods`**), and presentations (**`.odp`**). Body text becomes `Text` and the
  `Title` / `Author` / `Subject` properties become metadata. ODF is a zip of XML, read with
  the BCL — the same technique the built-in Office extractor uses. Files route to their
  natural buckets (`Document` / `Spreadsheet` / `Presentation`).

## 1.1.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.1.0` &nbsp;·&nbsp; 📅 2026-07-17

&nbsp;

---

Repositioned the package around **offline text + metadata extraction**. Redaction is now
**opt-in** instead of applied by default.

### ⚠️ Behavior change

- **Redaction is opt-in and caller-driven.** The core extracts and returns text + metadata
  exactly as read; it redacts only when you supply an `IRedactor` via `ReadOptions.Redactor`
  (or set a `Redaction` level). **1.0.0 redacted by default** — if you relied on that, set a
  redactor or level explicitly after upgrading.

### 📖 Positioning & docs

- Repositioned `Scrubkit` as **offline text + metadata extraction for .NET**. Updated the
  package `Title`, `Description`, tags, and both READMEs to lead with extraction.
- Refreshed the playground into a neutral extraction demo with an **extraction-view**
  screenshot (synthetic sample data only).
- Clarified that text-family formats (RTF / HTML / XML) are read as raw text.

### 🧪 Tests & tooling

- Expanded the test suite — extraction, extension filtering, record fields, opt-in and
  custom redaction, and edge cases (empty folders, uncapped limits, corrupt Office files,
  and a `stat-failed` guard).
- Added an **offline-guarantee test** that fails the build if either shipping assembly ever
  references a networking assembly.
- Added a **netstandard2.0 runtime test project** that runs the `netstandard2.0` build (its
  PolySharp polyfills and `GetValueOrDefault` shim) on the .NET 8 host, so the polyfilled
  paths are exercised at runtime — not just compiled.
- CI now publishes a **test-status summary** on every run.
- Tidied package author/copyright metadata and optimized the package icon.

### 🐛 Bug fixes

- **`.htm` files now report `TypeBucket = "Text"`.** `Buckets.For` mapped `.html` but not
  `.htm`, so `.htm` files extracted correctly yet were bucketed `Other`.


## 1.0.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.0.0` &nbsp;·&nbsp; 📅 2026-07-15

&nbsp;

---

Promoted from the preview to a **stable** release. Same engine and API as the preview —
**no new features and no bug fixes** — with a cleaner first impression and clearer, more
accurate wording throughout. The public API is stable under SemVer from here on.

### 📖 Documentation

- Redesigned the **NuGet package page** (README): a scannable highlights list, separate
  Install and Quick-start sections, a supported-formats table, and clear section dividers
  with more breathing room.
- Shortened the quick-start sample so long lines no longer cause a horizontal scrollbar on
  the package page.
- Redesigned the **`Scrubkit.Abstractions`** page with a focused example and a
  "what's inside" contract list.
- Added a **"Prepare a folder for RAG"** recipe to the package and project READMEs.
- Folded a short extension guide (writing a custom `IFileExtractor` or `IRedactor`) into the
  package README.

### 🔎 Discoverability & packaging

- Rewrote the package **description** to lead with the problem it solves — offline text +
  metadata extraction and scrubbing for RAG ingestion, search indexing, and data prep — and
  the formats it reads.
- Refreshed the **search tags** (added `text-extraction` and `office`; removed noise) so the
  right people can find it.

### 🛡️ Under the hood

- Enabled **package validation** — the build now checks that the `net8.0` and
  `netstandard2.0` API surfaces stay compatible within each package.

### ✍️ Wording & accuracy

- Removed references to add-on packages that aren't shipped yet, so the docs describe only
  what's available today.
- Reworded the scrubbing docs and code comments to plain, neutral language and softened
  absolute claims — the redactor is clearly described as **best-effort**.
- Tidied the playground output and XML doc comments to match.

### 🐛 Bug fixes

- _None — no behavior changed since the preview._

&nbsp;

## 0.1.0-preview.1

![Pre-release](https://img.shields.io/badge/release-Pre--release-d29922?style=flat-square) &nbsp; 🏷️ `v0.1.0-preview.1` &nbsp;·&nbsp; 📅 2026-07-13

&nbsp;

---

The first public release — everything Scrubkit does today shipped here.

### 🚀 Features

- **Point it at a folder, get a clean table back.** `FolderScrubber` walks a directory and
  returns one row per file — its text, metadata, and a tally of what was scrubbed — fully
  offline. Unreadable files never crash the run; problems show up as warnings instead.
- **Reads the common formats out of the box** — PDF, Word / Excel / PowerPoint
  (docx / xlsx / pptx), plain-text files (txt, md, csv, log, json, xml, html), and the
  camera/software info stored in image EXIF.
- **Scrubs common sensitive values** — emails, phone numbers, card numbers (validated with
  the Luhn check to cut false hits), SSNs, and IP addresses. An *Aggressive* mode also
  catches date-of-birth-like dates and long digit runs.
- **Handles big folders** — stream results as they're produced (`ReadStreamAsync`) and
  process several files at once (`MaxDegreeOfParallelism`), with output order preserved.
- **Extend it without forking** — plug in your own formats (`IFileExtractor`) or your own
  redaction (`IRedactor`); add-ons only need the tiny `Scrubkit.Abstractions` package.

### 🛠️ Under the hood

- Runs on **.NET 8** and **.NET Standard 2.0**.
- Reproducible, source-linked builds with symbol packages; automated tests and publishing.

### 🐛 Bug fixes

- _None — first release._
