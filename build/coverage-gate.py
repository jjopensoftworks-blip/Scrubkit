#!/usr/bin/env python3
"""Fail the build if line coverage is below a threshold.

Parses the Cobertura report(s) produced by `dotnet test --collect:"XPlat Code Coverage"`
and prints a short per-class summary. Exits non-zero when the overall line rate is
under --threshold, so CI fails on a coverage regression.
"""
import argparse
import glob
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--threshold", type=float, default=99.0, help="minimum line coverage %")
    ap.add_argument("--dir", default="./coverage", help="directory to search for cobertura xml")
    args = ap.parse_args()

    files = glob.glob(f"{args.dir}/**/coverage.cobertura.xml", recursive=True)
    if not files:
        print(f"::error::no coverage.cobertura.xml found under {args.dir}")
        return 1

    # If multiple runs (e.g. matrix), gate on the lowest observed line rate.
    worst = 100.0
    for f in files:
        root = ET.parse(f).getroot()
        line_rate = float(root.get("line-rate", 0)) * 100
        branch_rate = float(root.get("branch-rate", 0)) * 100
        worst = min(worst, line_rate)
        print(f"\n{f}")
        print(f"  line:   {line_rate:5.1f}%")
        print(f"  branch: {branch_rate:5.1f}%")
        classes = sorted(root.iter("class"), key=lambda c: float(c.get("line-rate", 1)))
        for c in classes[:8]:
            print(f"    {float(c.get('line-rate', 0)) * 100:5.1f}%  {c.get('name')}")

    print(f"\nOverall line coverage: {worst:.1f}%  (threshold {args.threshold:.1f}%)")
    if worst < args.threshold:
        print(f"::error::line coverage {worst:.1f}% is below the {args.threshold:.1f}% floor")
        return 1
    print("Coverage gate passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
