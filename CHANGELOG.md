# Changelog

<!--
One section per release, newest first. Each header shows a Stable/Pre-release badge, the
tag, and the date, then changes grouped as Features / Bug Fixes / Maintenance so readers
can tell at a glance whether a release adds things or just fixes them. Versions are cut
from Git tags via MinVer.
-->

## 1.2.0

![Unreleased](https://img.shields.io/badge/release-Unreleased-8957e5?style=flat-square) &nbsp; 🏷️ `v1.2.0` &nbsp;·&nbsp; 📅 _unreleased_

&nbsp;

---

Introduces the first **add-on package**, exercising the `IFileExtractor` extension seam
end to end.

### 🚀 New package: Scrubkit.Email

- **`Scrubkit.Email`** ships an `EmailExtractor` that reads **`.eml`** (MIME) files: the
  `From` / `To` / `Cc` / `Subject` / `Date` headers become metadata and the message body
  becomes text. It handles multipart messages, `base64` and `quoted-printable` transfer
  encodings, common charsets, and RFC 2047 encoded-word headers — preferring the
  `text/plain` part and falling back to `text/html`. Attachments are skipped.
- **No extra dependencies.** The package references only `Scrubkit.Abstractions`, so adding
  it pulls in no PDF or image libraries. Register it via `ReadOptions.Extractors`; `.eml`
  files then route to it and come back as `TypeBucket = "Email"` rows.

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
