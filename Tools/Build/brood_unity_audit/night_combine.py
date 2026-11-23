#!/usr/bin/env python3
"""Join the night run's chunk results into one CSV per frame-rate block (a `run` column names the chunk, so a match
is keyed by chunk and index).

    py -3 Tools/Build/brood_unity_audit/night_combine.py <out.csv> <chunk_dir> [<chunk_dir> ...]
"""
import csv
import os
import sys


def main(out, dirs):
    w = None
    n = 0
    with open(out, "w", newline="", encoding="utf-8") as fh:
        for d in dirs:
            p = os.path.join(d, "results.csv")
            if not os.path.exists(p):
                continue
            for r in csv.DictReader(open(p, encoding="utf-8")):
                r = {"run": os.path.basename(d.rstrip("/\\")), **r}
                if w is None:
                    w = csv.DictWriter(fh, fieldnames=list(r.keys()))
                    w.writeheader()
                w.writerow(r)
                n += 1
    print(out, n, "seats")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:])
