"""Build Docs/Studios/StarlingLab.html from src/lab.html and the card list.

    python3 Docs/Studios/StarlingLab/build.py

The card numbers come from Docs/Studios/SwarmCardVessel/cards.json (built by that folder's build.py),
so the lab and the deck page read one source.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "src", "lab.html")
CARDS = os.path.join(HERE, "..", "SwarmCardVessel", "cards.json")
OUT = os.path.join(HERE, "..", "StarlingLab.html")

page = open(SRC, encoding="utf-8").read()
marker = "/*CARDS*/null"
assert page.count(marker) == 1, "src/lab.html must hold exactly one /*CARDS*/null marker"
cards = json.load(open(CARDS, encoding="utf-8"))
page = page.replace(marker, json.dumps(cards, ensure_ascii=False, separators=(",", ":")))
open(OUT, "w", encoding="utf-8").write(page)
print("wrote", os.path.relpath(OUT), len(page), "bytes,", len(cards["cards"]), "cards")
