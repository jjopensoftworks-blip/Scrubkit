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

# Incremental: only output files changed since the last run, and update the manifest
scrubkit scan ./docs --since state.txt --manifest state.txt --out delta.jsonl
```

## Options

| Option | Description |
|--------|-------------|
| `--format <fmt>` | `csv`, `json`, `jsonl`, or `parquet`. Default: inferred from `--out`'s extension, else `csv`. |
| `--out <file>` | Write to a file instead of stdout. Required for `parquet`. |
| `--redact[=<level>]` | Redact PII + secrets. Level `standard` (default) or `aggressive`. Omit to extract without redacting. |
| `--rules <file>` | JSON file of custom redaction rules + allow/deny/disable lists (implies `--redact`). Format below. |
| `--no-recurse` | Only the top folder (default: recurse all nested). |
| `--hash` | Compute a SHA-256 content hash per file. |
| `--include <exts>` | Comma-separated extension filter, e.g. `--include .pdf,.docx`. |
| `--since <manifest>` | Incremental: only output files changed since `<manifest>` (a missing file = first run). Skips unchanged files. |
| `--manifest <file>` | Write a manifest of this scan to `<file>` (for a later `--since`). |
| `--max-files <n>` | Stop after `n` files (`0` = no limit). |
| `--max-bytes <n>` | Skip files larger than `n` bytes (`0` = no limit). |
| `--max-text <n>` | Clip extracted text to `n` characters (`0` = no clip). |
| `--local-time` | Emit local-time timestamps (with offset) instead of UTC. |
| `-h, --help` | Show help. |
| `-v, --version` | Show the version. |

The table goes to **stdout** (or `--out`); progress and a summary go to **stderr**, so you can
pipe the output cleanly. Exit code is `0` on success, `1` on a usage or I/O error.

Scrubbing is **best-effort, not a compliance tool.**

## Custom rules (`--rules`)

Add your own patterns (and allow/deny/disable lists) without code. Custom rules run **before** the
built-ins, so a domain pattern wins an overlap. Using `--rules` turns redaction on
(`--redact=aggressive` still composes). Example `rules.json`:

```json
{
  "rules": [
    { "category": "EmployeeId", "pattern": "\\bE\\d{6}\\b", "token": "[EMP]" },
    { "category": "CaseNo",     "pattern": "\\bC-\\d+\\b", "ignoreCase": false }
  ],
  "allow": ["support@ourco.com"],
  "deny":  ["Project Titan"],
  "disable": ["Phone"],
  "stableTokens": true,
  "tokenSalt": "per-deployment-secret",
  "revealLast": { "Card": 4 }
}
```

- `rules` — each is `{ category, pattern (.NET regex), token?, ignoreCase? }`; `token` defaults to
  `[CATEGORY]`. Patterns run with a match timeout, so a runaway regex can't hang a scan.
- `allow` — exact values never redacted; `deny` — literal terms always redacted; `disable` —
  built-in categories to switch off (e.g. `Email`, `Phone`).
- `stableTokens` — give each value a deterministic suffix so identical values share a token
  (e.g. `[EMAIL_3f9a1c8e]`) and stay **joinable** for analytics. `tokenSalt` mixes a secret into
  that hash so tokens can't be correlated across corpora or reversed by guessing.
- `revealLast` — per-category count of trailing characters to keep as a **format-preserving mask**,
  e.g. `{ "Card": 4 }` renders `**** **** **** 1111`. `maskChar` sets the mask character (default `*`).

```sh
scrubkit scan ./docs --rules rules.json --out clean.jsonl
```

## Links

- [Scrubkit on NuGet](https://www.nuget.org/packages/Scrubkit)
- [Project site](https://jjopensoftworks-blip.github.io/Scrubkit/)
- [Source & changelog](https://github.com/jjopensoftworks-blip/Scrubkit)
