#!/usr/bin/env python3
"""Author the VOYAGE ARCHETYPES asset (Assets/Resources/Voyages/VoyageArchetypes.asset).

Vessel roles (Cultivator, Destroyer, Guardian) and their nine archetypes replaced the seven Milestone 2 voyage classes
(Shombith, 2026-11-26; Docs/Voyages/ARCHETYPES.md). The source of truth is Shombith's board
(https://claude.ai/artifact/Duwb9xJ7XdAykxMKAnMyFe), copied to Docs/Voyages/archetypes.json: each vessel's primary,
secondary and tertiary archetype and the counter graph. This script adds what voyages do with it: the three archetype
seats each voyage type asks for, each named voyage's own ask (the type's, with its third seat swapped for a counter of
the crew it meets), the Hold the Core voyages' retake crews, and the starters. An archetype is never a kit change.

The words under the SO's "Words" header are a person's: this script writes only the tables, so word edits survive.

    python3 Tools/Build/author_voyage_archetypes.py             write the asset
    python3 Tools/Build/author_voyage_archetypes.py --check     report drift, write nothing (exit 1 on drift)
    python3 Tools/Build/author_voyage_archetypes.py --self-test the checks fire on planted defects
    python3 Tools/Build/author_voyage_archetypes.py --table     print each ask and the starters' best fit
"""
import itertools
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BOARD = ROOT / "Docs/Voyages/archetypes.json"
SLICE = ROOT / "Docs/Voyages/voyage_slice.json"
ASSET = ROOT / "Assets/Resources/Voyages/VoyageArchetypes.asset"
SCRIPT_META = ROOT / "Assets/_Scripts/ScriptableObjects/Voyages/VoyageArchetypesSO.cs.meta"
ENUM = ROOT / "Assets/_Scripts/Data/Enums/VesselClassType.cs"
ASSET_GUID = "5b0f3c7e2a9d4e6f8c1a2b3d4e5f6a7b"
# The quay's and moods' words (VoyageQuaySO): made once with the SO's own defaults, never rewritten; --check only asserts
# it exists on the right script.
QUAY = ROOT / "Assets/Resources/Voyages/VoyageQuay.asset"
QUAY_META = ROOT / "Assets/_Scripts/ScriptableObjects/Voyages/VoyageQuaySO.cs.meta"
QUAY_GUID = "7c1d2e3f4a5b46c7d8e9f0a1b2c3d4e5"

# VesselRole / VesselArchetype values (Assets/_Scripts/Data/Voyages/VesselArchetype.cs).
ROLE = {"Cultivator": 1, "Destroyer": 2, "Guardian": 3}
ARCH = {"Pumper": 1, "Builder": 2, "Burster": 3, "Sapper": 4, "Specialist": 5, "Nuke": 6,
        "Harraser": 7, "Zoner": 8, "Enhancer": 9}
ROLE_OF = {a: r for r, members in {"Cultivator": ["Pumper", "Builder", "Burster"],
                                   "Destroyer": ["Sapper", "Specialist", "Nuke"],
                                   "Guardian": ["Harraser", "Zoner", "Enhancer"]}.items() for a in members}
# The board's ids (kept from its first draft) to the archetypes' names, and its role ids to the roles.
BOARD_ARCH = {"pumper": "Pumper", "scout": "Builder", "burster": "Burster", "sapper": "Sapper", "specialist": "Specialist",
              "nuke": "Nuke", "hunters": "Harraser", "builders": "Zoner", "enhancers": "Enhancer"}
BOARD_ROLE = {"cult": "Cultivator", "dest": "Destroyer", "guard": "Guardian"}
# The board's vessel names to VesselClassType names (a crew vessel by its hull class). A name not here and not an enum
# name (the Wombat, not in the roster yet) is skipped and reported.
BOARD_VESSEL = {"Tiger Moth": "TigerMoth", "Brittle Star": "BrittleStar", "Goby & Pistol": "Goby",
                "Wrasse & Grouper": "Wrasse", "Honeyguide & Ratel": "Honeyguide", "Jewel Wasp": "JewelWasp"}

# The voyages' starters (Shombith, 2026-11-26).
STARTERS = ["Squirrel", "Sparrow", "Dolphin", "Thresher", "TigerMoth", "Swordfish"]

# What each voyage type asks for, and why each seat (Docs/Voyages/ARCHETYPES.md §3): the seats follow what decides the
# voyage today. The Crystal Run (job.raid.crystals) is left out on purpose: it stays as Milestone 1 built it.
ASKS = [
    ("job.salvage.drift", [("Nuke", "Cracks a wreck's mass open in one blow."),
                           ("Sapper", "Wears a wreck's mass down to its crystal."),
                           ("Harraser", "Keeps rival skimmers off the field.")]),
    ("escort", [("Enhancer", "Shores up the prisms round the Ark."),
                ("Harraser", "Silences what comes for her hull."),
                ("Nuke", "Clears an assault before it lands.")]),
    ("job.bounty.crew", [("Harraser", "Silenced escorts cannot wall their Ark."),
                         ("Specialist", "Breaks the walled hull."),
                         ("Sapper", "Strips the crew's mass as they run.")]),
    ("job.defence.aid", [("Pumper", "The hold runs on your volume in the nucleus."),
                         ("Burster", "Takes the core back in one surge."),
                         ("Sapper", "Clears their mass off the core.")]),
]

# The Hold the Core voyages' retake crews (they meet these at the core; VoyageRun.Core sends them in this order). The
# first hold has no retake crew at difficulty 1 and keeps its type's ask.
HOLD_CREWS = {
    "voyage.hold.ironmouth": ("the Ironmouth breakers", ["Sparrow", "Grizzly", "Stoat"]),
    "voyage.hold.ninefold": ("the Ninefold host", ["Rhino", "Dolphin", "Manta"]),
}


def vessel_values():
    text = ENUM.read_text()
    return {m.group(1): int(m.group(2)) for m in re.finditer(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,", text, re.M)}


def load_board(board, values):
    """The board as (archetype rows, role rows, {vessel: [archetypes, primary first]}, counters, skipped vessel names)."""
    arch_rows, role_rows = [], []
    for r in board["archetypes"]:
        role = BOARD_ROLE[r["id"]]
        role_rows.append((role, r["goal"]))
        for s in r["subs"]:
            name = BOARD_ARCH[s["id"]]
            line = re.sub(r"\s*\(.*?\)\s*$", "", s["desc"]).strip()
            arch_rows.append((name, line))
    vessels, skipped = {}, []
    for v in board["vessels"]:
        name = BOARD_VESSEL.get(v["name"], v["name"].replace(" ", ""))
        if name not in values:
            skipped.append(v["name"])
            continue
        prim = [BOARD_ARCH[k] for k, rank in v["roles"].items() if rank == "p"]
        rest = [BOARD_ARCH[k] for k, rank in v["roles"].items() if rank != "p"]
        vessels[name] = prim + rest
    counters = [(BOARD_ARCH[l["from"]], BOARD_ARCH[l["to"]]) for l in board["links"] if l["kind"] == "k_counter"]
    return arch_rows, role_rows, vessels, counters, skipped


def counter_for(threat, counters, vessels, taken):
    """The counter of THREAT a named voyage asks for: the counter most vessels hold as primary, a tie to the board's
    archetype order; one already asked for passes to the next; None when no vessel holds any counter as primary."""
    options = [f for f, t in counters if t == threat and f not in taken]
    held = {a: sum(1 for v in vessels.values() if v and v[0] == a) for a in options}
    options = [a for a in options if held[a] > 0]
    if not options:
        return None
    return sorted(options, key=lambda a: (-held[a], ARCH[a]))[0]


def voyage_asks(spec, vessels, counters):
    """Each named voyage that meets a crew: (voyage id, seats, crew name, crew hulls, threat, counter)."""
    rivals = {r["id"]: r for r in spec.get("rivals", [])}
    base = {job: [a for a, _ in seats] for job, seats in ASKS}
    out = []
    for v in spec.get("voyages", []):
        crew = None
        if v.get("rival") in rivals:
            r = rivals[v["rival"]]
            crew = (r["name"], r["hulls"])
        elif v["id"] in HOLD_CREWS:
            crew = HOLD_CREWS[v["id"]]
        if not crew or v["type"] not in base:
            continue
        seats = list(base[v["type"]])
        threat = vessels.get(crew[1][0], [None])[0]
        counter = counter_for(threat, counters, vessels, seats[:2]) if threat else None
        if counter:
            seats[2] = counter
        out.append((v["id"], seats, crew[0], crew[1], threat, counter))
    return out


def fit(seats, squad, vessels):
    """VesselArchetypes.Fit: each vessel at most one seat, the best total; 1 on a primary, 0.5 on a lesser archetype."""
    def meets(v, seat):
        a = vessels.get(v, [])
        return 0.0 if seat not in a else (1.0 if a[0] == seat else 0.5)
    best = 0.0
    for perm in itertools.permutations(squad, min(len(squad), len(seats))):
        best = max(best, sum(meets(v, s) for v, s in zip(perm, seats)))
    return best


def best_starter_fit(seats, vessels):
    return max(fit(seats, list(trio), vessels) for trio in itertools.combinations(STARTERS, 3))


def validate(vessels, counters, values, spec, asks=ASKS):
    errors = []
    for job, seats in asks:
        names = [a for a, _ in seats]
        if len(names) != 3 or len(set(names)) != 3:
            errors.append(f"{job} must ask for three different archetypes")
        for a, why in seats:
            if a not in ARCH:
                errors.append(f"{job}: unknown archetype {a}")
            elif not any(v and v[0] == a for v in vessels.values()):
                errors.append(f"{job}: no vessel has {a} as its primary, so no seat of it can be met fully")
            if not why:
                errors.append(f"{job}: seat {a} has no reason")
        if job == "job.raid.crystals":
            errors.append("the Crystal Run must stay unchanged: it asks for nothing")
    for s in STARTERS:
        if s not in values:
            errors.append(f"starter {s} is not a VesselClassType")
        elif not vessels.get(s):
            errors.append(f"starter {s} has no archetype on the board")
        elif not any(vessels[s][0] in [a for a, _ in seats] for _, seats in asks):
            errors.append(f"starter {s}'s primary ({vessels[s][0]}) is a full seat on no voyage type")
    for f, t in counters:
        if f not in ARCH or t not in ARCH:
            errors.append(f"counter {f} -> {t} names an unknown archetype")
    for vid, (_, hulls) in HOLD_CREWS.items():
        if not any(v["id"] == vid and v["type"] == "job.defence.aid" for v in spec.get("voyages", [])):
            errors.append(f"{vid} is not a Hold the Core voyage")
        for h in hulls:
            if h not in values:
                errors.append(f"{vid}: crew hull {h} is not a VesselClassType")
    return errors


def yaml_str(text):
    """A string as Unity writes it: single-quoted when plain YAML would misread it."""
    if text == "":
        return ""
    needs = ": " in text or " #" in text or text[:1] in "'\"&*!|>%@`#{[-?,"
    return "'" + text.replace("'", "''") + "'" if needs else text


def script_guid(meta=None):
    return re.search(r"guid: (\w+)", (meta or SCRIPT_META).read_text()).group(1)


def header(name, meta=None):
    return ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
            "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
            f"  m_Script: {{fileID: 11500000, guid: {script_guid(meta)}, type: 3}}", f"  m_Name: {name}",
            "  m_EditorClassIdentifier:"]


def quay_ok():
    return QUAY.exists() and f"guid: {script_guid(QUAY_META)}" in QUAY.read_text() \
        and QUAY.with_suffix(".asset.meta").exists()


def words_block(existing):
    """The words as they stand in the asset (a person's edits), else nothing (the SO's defaults apply)."""
    if not existing:
        return ""
    m = re.search(r"^  seatLine:.*\Z", existing, re.M | re.S)
    return m.group(0) if m else ""


def int_list(out, indent, key, items):
    if not items:
        out.append(f"{indent}{key}: []")
        return
    out.append(f"{indent}{key}:")
    out += [f"{indent}- {i}" for i in items]


def render(values, board, spec, existing=None):
    arch_rows, role_rows, vessels, counters, _ = load_board(board, values)
    out = header("VoyageArchetypes")
    out.append("  roles:")
    for role, goal in role_rows:
        out += [f"  - Role: {ROLE[role]}", f"    Name: {role}", f"    Line: {yaml_str(goal)}"]
        members = [v for v in vessels if vessels[v] and ROLE_OF[vessels[v][0]] == role]
        int_list(out, "    ", "Vessels", [values[v] for v in members])
    out.append("  archetypes:")
    for name, line in arch_rows:
        out += [f"  - Archetype: {ARCH[name]}", f"    Name: {name}", f"    Line: {yaml_str(line)}"]
    out.append("  vessels:")
    for v, archs in vessels.items():
        out.append(f"  - Vessel: {values[v]}")
        int_list(out, "    ", "Archetypes", [ARCH[a] for a in archs])
    out.append("  counters:")
    for f, t in counters:
        out += [f"  - From: {ARCH[f]}", f"    To: {ARCH[t]}"]
    out.append("  asks:")
    for job, seats in ASKS:
        out.append(f"  - Job: {job}")
        int_list(out, "    ", "Seats", [ARCH[a] for a, _ in seats])
        out.append("    Reasons:")
        out += [f"    - {yaml_str(why)}" for _, why in seats]
    asks = voyage_asks(spec, vessels, counters)
    if asks:
        out.append("  voyageAsks:")
    else:
        out.append("  voyageAsks: []")
    for vid, seats, crew, hulls, threat, counter in asks:
        out.append(f"  - Voyage: {vid}")
        int_list(out, "    ", "Seats", [ARCH[a] for a in seats])
        out.append(f"    Crew: {yaml_str(crew)}")
        int_list(out, "    ", "CrewHulls", [values[h] for h in hulls if h in values])
        out += [f"    Threat: {ARCH.get(threat, 0)}", f"    Countered: {ARCH.get(counter, 0)}"]
    int_list(out, "  ", "starters", [values[s] for s in STARTERS])
    out.append("  fitPayPerPoint: 0.1")
    words = words_block(existing)
    text = "\n".join(out) + "\n"
    return text + (words if words.endswith("\n") or not words else words + "\n")


def keep_fit_pay(text, existing):
    """A person's fitPayPerPoint edit survives a re-run like the words do."""
    m = existing and re.search(r"^  fitPayPerPoint: (.*)$", existing, re.M)
    return re.sub(r"^  fitPayPerPoint: .*$", f"  fitPayPerPoint: {m.group(1)}", text, flags=re.M) if m else text


META = f"""fileFormatVersion: 2
guid: {ASSET_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def table(values, board, spec):
    _, _, vessels, counters, skipped = load_board(board, values)
    for job, seats in ASKS:
        names = [a for a, _ in seats]
        print(f"{job:20} {', '.join(names):34} starters best {best_starter_fit(names, vessels)}")
    for vid, seats, crew, hulls, threat, counter in voyage_asks(spec, vessels, counters):
        print(f"{vid:24} {crew:22} {'/'.join(hulls):24} threat {threat:10} -> ask {', '.join(seats)}"
              f"  starters best {best_starter_fit(seats, vessels)}")
    if skipped:
        print("not in the roster, skipped:", ", ".join(skipped))


def self_test(values, board, spec):
    _, _, vessels, counters, _ = load_board(board, values)
    assert not validate(vessels, counters, values, spec), validate(vessels, counters, values, spec)
    bad = [("escort", [("Zoner", "x"), ("Harraser", "x"), ("Nuke", "x")])] + ASKS[1:]
    assert any("no vessel has Zoner" in e for e in validate(vessels, counters, values, spec, bad)), \
        "a seat nobody can fill must fail"
    dup = [("escort", [("Nuke", "x"), ("Nuke", "x"), ("Harraser", "x")])]
    assert any("three different" in e for e in validate(vessels, counters, values, spec, dup))
    assert any("Crystal Run" in e for e in validate(vessels, counters, values, spec,
                                                    ASKS + [("job.raid.crystals", ASKS[0][1])]))
    lone = [("escort", [("Enhancer", "x"), ("Harraser", "x"), ("Nuke", "x")])]
    assert any("TigerMoth" in e or "Squirrel" in e for e in validate(vessels, counters, values, spec, lone)), \
        "a starter that is a full seat nowhere must fail"
    # Fit: primary 1, lesser 0.5, one vessel per seat.
    assert fit(["Pumper", "Sapper", "Nuke"], ["Squirrel", "Sparrow", "Dolphin"], vessels) == 3.0
    assert fit(["Pumper", "Pumper", "Pumper"], ["Squirrel"], vessels) == 1.0, "one vessel fills one seat"
    assert fit(["Harraser"], ["Squirrel"], vessels) == 0.5
    # Counters: Sapper is countered by Builder (Serpent, Meerkat) and Zoner (nobody): Builder.
    assert counter_for("Sapper", counters, vessels, []) == "Builder"
    assert counter_for("Builder", counters, vessels, ["Nuke"]) is None, "a counter already asked for passes on"
    # The board's tertiary: Sparrow's archetypes keep the board's order after the primary.
    assert vessels["Sparrow"][0] == "Sapper" and len(vessels["Sparrow"]) == 3
    words = render(values, board, spec, render(values, board, spec) + "  seatLine: edited\n")
    assert words.endswith("  seatLine: edited\n"), "a person's word edits must survive a re-run"
    print("self-test ok")


def main():
    values = vessel_values()
    board = json.loads(BOARD.read_text())
    spec = json.loads(SLICE.read_text())
    if "--self-test" in sys.argv:
        self_test(values, board, spec)
        return 0
    if "--table" in sys.argv:
        table(values, board, spec)
        return 0
    _, _, vessels, counters, _ = load_board(board, values)
    errors = validate(vessels, counters, values, spec)
    if errors:
        print("\n".join("FAIL " + e for e in errors))
        return 1
    existing = ASSET.read_text() if ASSET.exists() else None
    text = keep_fit_pay(render(values, board, spec, existing), existing)
    meta_path = ASSET.with_suffix(".asset.meta")
    if "--check" in sys.argv:
        drift = existing != text or not meta_path.exists() or meta_path.read_text() != META or not quay_ok()
        print("drift: re-run author_voyage_archetypes.py" if drift else "VoyageArchetypes.asset up to date")
        return 1 if drift else 0
    ASSET.write_text(text)
    meta_path.write_text(META)
    print(f"wrote {ASSET.relative_to(ROOT)}")
    if not quay_ok():
        QUAY.write_text("\n".join(header("VoyageQuay", QUAY_META)) + "\n")
        QUAY.with_suffix(".asset.meta").write_text(META.replace(ASSET_GUID, QUAY_GUID))
        print(f"wrote {QUAY.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
