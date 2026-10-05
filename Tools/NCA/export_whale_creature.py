#!/usr/bin/env python3
"""Export the prism-whale 3D NCA: export_nca3d_creature.py with --name whale (kept for the P7b docs).

    python3 Tools/NCA/export_whale_creature.py [--run ... --out ... --no-build]   # -> nca_whale.js (window.NcaWhale)
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from export_nca3d_creature import main  # noqa: E402

if __name__ == "__main__":
    main(["--name", "whale"] + sys.argv[1:])
