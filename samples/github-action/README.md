# Sample GitHub Action — Scrubkit in CI

[`scrubkit-scan.yml`](scrubkit-scan.yml) is a copy-paste workflow that runs the
[`scrubkit`](https://www.nuget.org/packages/Scrubkit.Tool) CLI on a folder. Drop it into your
repo's `.github/workflows/`.

It shows two patterns:

1. **Produce a scrubbed corpus** — extract + redact `./docs` into a `scrubbed.jsonl` artifact,
   ready for a downstream embedding / RAG job.
2. **Pre-share gate** — fail the build if aggressive redaction finds any secret/PII in a folder
   you're about to publish.

Both run entirely on the runner. Scrubbing is **best-effort, not a compliance tool** — a safety
net, not a guarantee.
