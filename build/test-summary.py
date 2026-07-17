#!/usr/bin/env python3
"""Write a test-run summary to the GitHub Actions job summary.

Parses the TRX report(s) produced by `dotnet test --logger trx` and appends a compact
pass/fail table (plus any failure details) to $GITHUB_STEP_SUMMARY, so every PR run shows
test status at a glance. Prints the same summary to stdout for local use. Exits non-zero
if any test failed, so the step reflects the run outcome even with `if: always()`.
"""
import argparse
import glob
import os
import sys
import xml.etree.ElementTree as ET

# Emoji in the summary; make stdout UTF-8 even on a legacy Windows console.
try:
    sys.stdout.reconfigure(encoding="utf-8")
except (AttributeError, ValueError):
    pass


def _local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]  # strip the {namespace}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default="./coverage", help="directory to search for *.trx")
    args = ap.parse_args()

    files = glob.glob(f"{args.dir}/**/*.trx", recursive=True)
    if not files:
        print(f"::warning::no .trx report found under {args.dir}")
        return 0

    total = passed = failed = skipped = 0
    failures: list[tuple[str, str]] = []

    for f in files:
        root = ET.parse(f).getroot()
        for el in root.iter():
            name = _local(el.tag)
            if name == "Counters":
                total += int(el.get("total", 0))
                passed += int(el.get("passed", 0))
                failed += int(el.get("failed", 0))
                # "skipped" isn't a single attribute; derive it below.
            if name == "UnitTestResult" and el.get("outcome") == "Failed":
                msg = ""
                for sub in el.iter():
                    if _local(sub.tag) == "Message":
                        msg = (sub.text or "").strip().splitlines()[0] if sub.text else ""
                        break
                failures.append((el.get("testName", "?"), msg))

    skipped = max(total - passed - failed, 0)
    icon = "✅" if failed == 0 else "❌"

    lines = [
        f"## {icon} Test results",
        "",
        "| Total | Passed | Failed | Skipped |",
        "|------:|-------:|-------:|--------:|",
        f"| {total} | {passed} | {failed} | {skipped} |",
    ]
    if failures:
        lines += ["", "### Failed tests", ""]
        lines += [f"- `{name}` — {msg}" for name, msg in failures]

    summary = "\n".join(lines) + "\n"
    print(summary)

    out = os.environ.get("GITHUB_STEP_SUMMARY")
    if out:
        with open(out, "a", encoding="utf-8") as fh:
            fh.write(summary)

    return 1 if failed > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
