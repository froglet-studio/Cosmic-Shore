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
import hashlib
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SO = ROOT / "Assets/_SO_Assets"
TOY = SO / "Toys/Toy_SpawnMatrix.asset"

ELEMENTS = ("Charge", "Mass", "Space", "Time")


def lifeform_set(name):
    return [f"Lifeforms/{name} Flora {e}.asset" for e in ELEMENTS]


# ── Bench-only swarm configs ─────────────────────────────────────────────────────────────────
# The Swarm cell ships the SORT model only (round 7 repointed / deleted the configs of the other
# three sim models), but SWARM_FAUNA.md §14 keeps the FIELD, GRID and EVOFATE prefabs in the tree
# for the bench. Nothing pointed a FaunaConfigurationSO at them, so they were unreachable. These
# configs exist only for the Spawn Matrix: one per element per model, in their own folder so the
# Swarm-cell and Lifeforms generators never see them. Cloned from the shipped Sort config.
BENCH_DIR = "Swarm Fauna/Bench"
SWARM_DONOR = "Cell Configs/Swarm Cell/Swarm Inner Mass Swarm Fauna Config Data.asset"
SWARM_PREFAB_FILEID = "4174204561870355101"
# model: (prefab guid, band inner, band outer) - bands are each model's last shipped band.
SWARM_MODELS = {
    "Field": ("d38acaa5904e7a000730e2d5ad289ffc", 610, 740),
    "Grid": ("fdf92e28cdd61e8c14732eb0bc7c549d", 430, 560),
    "EvoFate": ("a6815be88f6a14d8e8197168459d2680", 970, 1120),
}
SORT_PREFAB = "dba51f6ae2c167ee689fa855305270f7"
ELEMENT_VALUES = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}


def bench_guid(name):
    return hashlib.md5(f"spawn-matrix-bench:{name}".encode()).hexdigest()


def bench_name(model, element):
    return f"Bench Swarm {model} {element} Fauna Config Data"


def bench_configs():
    """{relative path: (asset text, guid)} for every bench-only swarm config."""
    donor = (SO / SWARM_DONOR).read_text()
    out = {}
    plan = [(m, e, *SWARM_MODELS[m]) for m in SWARM_MODELS for e in ELEMENTS]
    # The shipped Sort swarm has Charge, Mass and Space; its Time element (the dragonfly) is
    # bench-only, in the shipped middle band.
    plan.append(("Sort", "Time", SORT_PREFAB, 690, 840))
    for model, element, prefab, inner, outer in plan:
        name = bench_name(model, element)
        text = donor
        for pattern, repl in (
            (r"^  m_Name: .*$", f"  m_Name: {name}"),
            (r"^  FaunaPrefab: .*$",
             f"  FaunaPrefab: {{fileID: {SWARM_PREFAB_FILEID}, guid: {prefab}, type: 3}}"),
            (r"^  BandInnerRadius: .*$", f"  BandInnerRadius: {inner}"),
            (r"^  BandOuterRadius: .*$", f"  BandOuterRadius: {outer}"),
            (r"^  Element: .*$", f"  Element: {ELEMENT_VALUES[element]}"),
        ):
            text, n = re.subn(pattern, repl, text, count=1, flags=re.M)
            if n != 1:
                sys.exit(f"author_spawn_matrix_roster: donor swarm config lacks '{pattern}'")
        out[f"{BENCH_DIR}/{name}.asset"] = (text, bench_guid(name))
    return out


ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def bench_set(model):
    return [f"{BENCH_DIR}/{bench_name(model, e)}.asset" for e in ELEMENTS]


# (row name, [config paths relative to Assets/_SO_Assets]) in display order.
FAUNA = [
    ("Piranha", ["Cell Configs/Astro League Cell/Astro League Piranha Fauna Config Data.asset"]),
    ("Swarm", [
        "Cell Configs/Swarm Cell/Swarm Middle Charge Swarm Fauna Config Data.asset",
        "Cell Configs/Swarm Cell/Swarm Inner Mass Swarm Fauna Config Data.asset",
        "Cell Configs/Swarm Cell/Swarm Outer Space Swarm Fauna Config Data.asset",
        f"{BENCH_DIR}/{bench_name('Sort', 'Time')}.asset",
    ]),
    ("Swarm Field", bench_set("Field")),
    ("Swarm Grid", bench_set("Grid")),
    ("Swarm EvoFate", bench_set("EvoFate")),
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
    ("Gyroid Topiary", ["Cell Configs/Hesperides Cell/Hesperides Gyroid Topiary Config Data.asset"]),
    ("SchwarzP Topiary", ["Cell Configs/Hesperides Cell/Hesperides SchwarzP Topiary Config Data.asset"]),
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

    # Bench-only configs first: the rows below resolve their guids from the metas on disk.
    drifted = []
    files = {}
    for rel, (text, guid) in bench_configs().items():
        files[SO / rel] = text
        files[SO / (rel + ".meta")] = ASSET_META.format(guid=guid)
    files[SO / (BENCH_DIR + ".meta")] = FOLDER_META.format(guid=bench_guid("folder"))
    for path, text in files.items():
        if path.exists() and path.read_text() == text:
            continue
        drifted.append(path.relative_to(ROOT))
        if not args.check:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text)
    if drifted and args.check:
        print("author_spawn_matrix_roster: bench configs drifted: " + ", ".join(map(str, drifted)))
        sys.exit(1)

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
