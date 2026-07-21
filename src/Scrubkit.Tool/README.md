# Scrubkit.Tool

The **`scrubkit`** command-line tool — run [Scrubkit](https://www.nuget.org/packages/Scrubkit)
from any shell or CI, no code required. Point it at a folder and it extracts text + metadata
from PDFs, Office / OpenDocument, email, EPUB, and text / image files, optionally redacts PII
and secrets, and writes a **CSV / JSON / JSON Lines / Parquet** table. Everything runs on your
machine — **no network calls**.

## Install

```
dotnet tool install --global Scrubkit.Tool
```

## Use

```
scrubkit scan <folder> [options]
```

```
# Extract a folder to CSV on stdout
scrubkit scan ./docs

# Redact PII + secrets and write JSON Lines for a RAG / embedding pipeline
scrubkit scan ./docs --redact --format jsonl --out docs.jsonl

# Aggressive redaction, only PDFs and email, with a content hash per file
scrubkit scan ./docs --redact=aggressive --include .pdf,.eml --hash

# Columnar output for analytics
scrubkit scan ./data --format parquet --out data.parquet
```

## Options

| Option | Description |
|--------|-------------|
| `--format <fmt>` | `csv`, `json`, `jsonl`, or `parquet`. Default: inferred from `--out`'s extension, else `csv`. |
| `--out <file>` | Write to a file instead of stdout. Required for `parquet`. |
| `--redact[=<level>]` | Redact PII + secrets. Level `standard` (default) or `aggressive`. Omit to extract without redacting. |
| `--no-recurse` | Only the top folder (default: recurse all nested). |
| `--hash` | Compute a SHA-256 content hash per file. |
| `--include <exts>` | Comma-separated extension filter, e.g. `--include .pdf,.docx`. |
| `--max-files <n>` | Stop after `n` files (`0` = no limit). |
| `--max-bytes <n>` | Skip files larger than `n` bytes (`0` = no limit). |
| `--max-text <n>` | Clip extracted text to `n` characters (`0` = no clip). |
| `--local-time` | Emit local-time timestamps (with offset) instead of UTC. |
| `-h, --help` | Show help. |
| `-v, --version` | Show the version. |

The table goes to **stdout** (or `--out`); progress and a summary go to **stderr**, so you can
pipe the output cleanly. Exit code is `0` on success, `1` on a usage or I/O error.

Scrubbing is **best-effort, not a compliance tool.**

## Links

- [Scrubkit on NuGet](https://www.nuget.org/packages/Scrubkit)
- [Project site](https://jjopensoftworks-blip.github.io/Scrubkit/)
- [Source & changelog](https://github.com/jjopensoftworks-blip/Scrubkit)
