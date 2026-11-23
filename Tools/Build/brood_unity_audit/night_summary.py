#!/usr/bin/env python3
"""Write SUMMARY_night.md: the frame-rate comparison table, both blocks' per-hull tables (with the achieved frame
rate) and the hand-written findings in night_notes.md.

    py -3 Tools/Build/brood_unity_audit/night_summary.py
"""
import io
import os
import subprocess
import sys
from contextlib import redirect_stdout

here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, here)
from summarize import load, render  # noqa: E402

cmp_out = subprocess.run([sys.executable, os.path.join(here, "night_compare.py")], capture_output=True, text=True,
                         check=True).stdout
a, _ = render("Block A, pinned 60 fps", load(os.path.join(here, "results_60.csv")))
b, _ = render("Block B, pinned 30 fps", load(os.path.join(here, "results_30.csv")))
notes = open(os.path.join(here, "night_notes.md"), encoding="utf-8").read()
doc = notes.replace("{{COMPARE}}", cmp_out.strip()).replace("{{BLOCK_A}}", a.strip()).replace("{{BLOCK_B}}", b.strip())
open(os.path.join(here, "SUMMARY_night.md"), "w", encoding="utf-8", newline="\n").write(doc)
print("wrote SUMMARY_night.md")
