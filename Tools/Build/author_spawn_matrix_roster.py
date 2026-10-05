#!/usr/bin/env python3
"""
Spawn Matrix roster: every spawnable flora and fauna on the release bench.

The Spawn Matrix toy (Assets/_SO_Assets/Toys/Toy_SpawnMatrix.asset) is the one place a single
species can be released into a cell on demand, which is how a life form is inspected on its own
in the Barren cell (Cell Selector > Barren, then Spawn Matrix). Before this script its rows were
the original Lifeforms set plus whatever a species generator appended (Borromean, Mandelbulb), so
everything the Swarm cell grew - the swarms, the seven substrate species, the builders, the threat
flora, the Swarm Borromean bands - and eight generic 4-element flora sets were unreachable.

This script owns the rows it lists below and nothing else: a row is replaced in place if it is
already there and appended to the end of its kingdom otherwise, so rows owned by other generators
(author_borromean_flora_assets.py, author_mandelbulb_flora_assets.py) keep their content and
order. Configs are resolved by PATH and read for their guid, so a renamed or deleted asset fails
loudly here instead of leaving a dangling reference in the toy.

A species may express fewer than four elements (most Swarm-cell species are one element each);
the toy's variant row shows one station per element the species actually has.

    python3 Tools/Build/author_spawn_matrix_roster.py           # write
    python3 Tools/Build/author_spawn_matrix_roster.py --check   # exit 1 if the toy has drifted
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SO = ROOT / "Assets/_SO_Assets"
TOY = SO / "Toys/Toy_SpawnMatrix.asset"

ELEMENTS = ("Charge", "Mass", "Space", "Time")


def lifeform_set(name):
    return [f"Lifeforms/{name} Flora {e}.asset" for e in ELEMENTS]


# (row name, [config paths relative to Assets/_SO_Assets]) in display order.
FAUNA = [
    ("Piranha", ["Cell Configs/Astro League Cell/Astro League Piranha Fauna Config Data.asset"]),
    ("Swarm", [
        "Cell Configs/Swarm Cell/Swarm Middle Charge Swarm Fauna Config Data.asset",
        "Cell Configs/Swarm Cell/Swarm Inner Mass Swarm Fauna Config Data.asset",
        "Cell Configs/Swarm Cell/Swarm Outer Space Swarm Fauna Config Data.asset",
    ]),
    ("Pack Hunter", ["Substrate Fauna/Substrate Pack Hunter Fauna Config Data.asset"]),
    ("Locust", ["Substrate Fauna/Substrate Locust Fauna Config Data.asset"]),
    ("Lurker", ["Substrate Fauna/Substrate Lurker Fauna Config Data.asset"]),
    ("Stampede", ["Substrate Fauna/Substrate Stampede Fauna Config Data.asset"]),
    ("Mobber", ["Substrate Fauna/Substrate Mobber Fauna Config Data.asset"]),
    ("Leech", ["Substrate Fauna/Substrate Leech Fauna Config Data.asset"]),
    ("Leviathan", ["Substrate Fauna/Substrate Leviathan Fauna Config Data.asset"]),
    ("Fortress Builders", ["Cell Configs/Swarm Cell/Swarm Fortress Builder Fauna Config Data.asset"]),
    ("Thief Nest", ["Cell Configs/Swarm Cell/Swarm Thief Nest Builder Fauna Config Data.asset"]),
    ("Wearer Builders", ["Cell Configs/Swarm Cell/Swarm Wearer Builder Fauna Config Data.asset"]),
]

FLORA = [
    *[(n, lifeform_set(n)) for n in
      ("Arbor", "Coral", "Frond", "Lantern", "Reed", "Rosette", "Spire", "Tendril")],
    ("Swarm Borromean", [
        "Cell Configs/Swarm Cell/Swarm Inner Borromean Flora Mass Config Data.asset",
        "Cell Configs/Swarm Cell/Swarm Outer Borromean Flora Space Config Data.asset",
        "Cell Configs/Swarm Cell/Swarm Middle Borromean Flora Time Config Data.asset",
    ]),
    ("Physarum", ["Threat Flora/Swarm Physarum Flora Space Config Data.asset"]),
    ("Snap Trap", ["Threat Flora/Swarm Snap Trap Flora Time Config Data.asset"]),
]

def guid_of(rel):
    asset = SO / rel
    meta = SO / (rel + ".meta")
    if not asset.exists() or not meta.exists():
        sys.exit(f"author_spawn_matrix_roster: config missing: {rel}")
    m = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.M)
    if not m:
        sys.exit(f"author_spawn_matrix_roster: no guid in {rel}.meta")
    return m.group(1)


def element_of(rel):
    m = re.search(r"^  Element: (\d+)$", (SO / rel).read_text(), re.M)
    return int(m.group(1)) if m else 0


def row_text(name, paths):
    seen = set()
    for p in paths:
        e = element_of(p)
        if e == 0:
            sys.exit(f"author_spawn_matrix_roster: {p} has no element - the toy could not show it")
        if e in seen:
            sys.exit(f"author_spawn_matrix_roster: row '{name}' has two configs for element {e}")
        seen.add(e)
    body = "\n".join(f"    - {{fileID: 11400000, guid: {guid_of(p)}, type: 2}}" for p in paths)
    return f"  - Name: {name}\n    ElementConfigs:\n{body}\n"


def section_bounds(toy, key):
    m = re.search(rf"^  {key}:\n", toy, re.M)
    if not m:
        sys.exit(f"author_spawn_matrix_roster: {key} not found in the toy asset")
    nxt = re.search(r"^  (?!- |  )\S", toy[m.end():], re.M)
    return m.end(), m.end() + (nxt.start() if nxt else len(toy) - m.end())


def upsert(toy, key, name, row):
    start, end = section_bounds(toy, key)
    section = toy[start:end]
    header = f"  - Name: {name}\n"
    if header in section:
        s = section.index(header)
        rest = section[s + len(header):]
        nxt = re.search(r"^  - Name: ", rest, re.M)
        e = s + len(header) + (nxt.start() if nxt else len(rest))
        section = section[:s] + row + section[e:]
    else:
        section = section + row
    return toy[:start] + section + toy[end:]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    original = TOY.read_text()
    toy = original
    for name, paths in FAUNA:
        for p in paths:
            if "Flora" in Path(p).name:
                sys.exit(f"author_spawn_matrix_roster: flora config listed as fauna: {p}")
        toy = upsert(toy, "faunaSpecies", name, row_text(name, paths))
    for name, paths in FLORA:
        for p in paths:
            if "Fauna" in Path(p).name:
                sys.exit(f"author_spawn_matrix_roster: fauna config listed as flora: {p}")
        toy = upsert(toy, "floraSpecies", name, row_text(name, paths))

    # author_borromean_flora_assets.py re-appends its "Borromean" row at the END of the flora
    # list on every run; keep it last so both generators agree on the file byte-for-byte.
    start, end = section_bounds(toy, "floraSpecies")
    section = toy[start:end]
    m = re.search(r"^  - Name: Borromean\n(?:    .*\n)+", section, re.M)
    if m:
        section = section[:m.start()] + section[m.end():] + m.group(0)
        toy = toy[:start] + section + toy[end:]

    # Every guid in the toy must resolve to an asset somewhere (catches rows other generators own).
    known = set()
    for meta in (ROOT / "Assets").rglob("*.asset.meta"):
        m = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(errors="ignore"), re.M)
        if m:
            known.add(m.group(1))
    dangling = [g for g in re.findall(r"guid: ([0-9a-f]{32}), type: 2", toy) if g not in known]
    if dangling:
        sys.exit(f"author_spawn_matrix_roster: toy references missing assets: {dangling}")

    fauna_rows = len(re.findall(r"^  - Name: ", toy[slice(*section_bounds(toy, "faunaSpecies"))], re.M))
    flora_rows = len(re.findall(r"^  - Name: ", toy[slice(*section_bounds(toy, "floraSpecies"))], re.M))

    if toy == original:
        print(f"author_spawn_matrix_roster: up to date ({fauna_rows} fauna, {flora_rows} flora rows)")
        return
    if args.check:
        print("author_spawn_matrix_roster: Toy_SpawnMatrix.asset has drifted; run without --check")
        sys.exit(1)
    TOY.write_text(toy)
    print(f"author_spawn_matrix_roster: wrote Toy_SpawnMatrix.asset ({fauna_rows} fauna, {flora_rows} flora rows)")


if __name__ == "__main__":
    main()
