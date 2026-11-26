#!/usr/bin/env python3
"""Author the fleet's VESSEL ARCHETYPES asset (Assets/Resources/Vessels/VesselArchetypes.asset).

Vessel roles (Cultivator, Destroyer, Guardian) and their nine archetypes (Shombith, 2026-11-26; Docs/Vessels/ARCHETYPES.md).
The source of truth is Shombith's board (https://claude.ai/artifact/Duwb9xJ7XdAykxMKAnMyFe), copied to
Docs/Vessels/archetypes.json: each vessel's primary, secondary and tertiary archetype and the counter graph. Where the
board lists two secondaries, the second is the tertiary (Shombith, 2026-11-26). This is the ONE copy the Hangar, the
ecology page and voyages read (VesselArchetypesSO).

The role and archetype names and lines are a person's: they are seeded from the board when the asset has none and kept
on every re-run. The vessel and counter tables are this script's.

    python3 Tools/Build/author_vessel_archetypes.py             write the asset
    python3 Tools/Build/author_vessel_archetypes.py --check     report drift, write nothing (exit 1 on drift)
    python3 Tools/Build/author_vessel_archetypes.py --self-test the checks fire on planted defects
    python3 Tools/Build/author_vessel_archetypes.py --table     print each vessel's archetypes
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BOARD = ROOT / "Docs/Vessels/archetypes.json"
ASSET = ROOT / "Assets/Resources/Vessels/VesselArchetypes.asset"
SCRIPT_META = ROOT / "Assets/_Scripts/ScriptableObjects/VesselArchetypesSO.cs.meta"
ENUM = ROOT / "Assets/_Scripts/Data/Enums/VesselClassType.cs"
ARCH_CS = ROOT / "Assets/_Scripts/Data/VesselArchetype.cs"

ROLES = ["Cultivator", "Destroyer", "Guardian"]
ARCHS = ["Pumper", "Builder", "Burster", "Sapper", "Specialist", "Nuke", "Harraser", "Zoner", "Enhancer"]
# The board's ids (kept from its first draft) to the archetypes' names, and its role ids to the roles.
BOARD_ARCH = {"pumper": "Pumper", "scout": "Builder", "burster": "Burster", "sapper": "Sapper", "specialist": "Specialist",
              "nuke": "Nuke", "hunters": "Harraser", "builders": "Zoner", "enhancers": "Enhancer"}
BOARD_ROLE = {"cult": "Cultivator", "dest": "Destroyer", "guard": "Guardian"}
# The board's vessel names to VesselClassType names (a crew vessel by its hull class). A name that is neither here nor an
# enum name (the Wombat, not in the roster yet) is skipped and reported.
BOARD_VESSEL = {"Tiger Moth": "TigerMoth", "Brittle Star": "BrittleStar", "Goby & Pistol": "Goby",
                "Wrasse & Grouper": "Wrasse", "Honeyguide & Ratel": "Honeyguide", "Jewel Wasp": "JewelWasp"}


def enum_values(path, enum):
    text = path.read_text()
    body = re.search(r"enum\s+" + enum + r"\s*\{(.*?)\}", text, re.S).group(1)
    return {m.group(1): int(m.group(2)) for m in re.finditer(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,", body, re.M)}


def load_board(board, values):
    """(role words, archetype words, [(vessel, [archetypes primary first])], counters, skipped vessel names)."""
    role_words, arch_words = {}, {}
    for r in board["archetypes"]:
        role_words[BOARD_ROLE[r["id"]]] = (r["name"], r["goal"])
        for s in r["subs"]:
            line = re.sub(r"\s*\(.*?\)\s*$", "", s["desc"]).strip()
            arch_words[BOARD_ARCH[s["id"]]] = (s["name"], line)
    vessels, skipped = [], []
    for v in board["vessels"]:
        name = BOARD_VESSEL.get(v["name"], v["name"].replace(" ", ""))
        if name not in values:
            skipped.append(v["name"])
            continue
        prim = [BOARD_ARCH[k] for k, rank in v["roles"].items() if rank == "p"]
        rest = [BOARD_ARCH[k] for k, rank in v["roles"].items() if rank != "p"]
        vessels.append((name, prim + rest))
    counters = [(BOARD_ARCH[l["from"]], BOARD_ARCH[l["to"]]) for l in board["links"] if l["kind"] == "k_counter"]
    return role_words, arch_words, vessels, counters, skipped


def q(s):
    """A YAML scalar Unity reads back as the same string."""
    if s == "" or re.search(r"[:#\[\]{}&*!|>'\"%@`,]|^\s|\s$|^-", s):
        return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'
    return s


def unq(s):
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        return s[1:-1].replace('\\"', '"').replace("\\\\", "\\")
    if s.startswith("'") and s.endswith("'"):
        return s[1:-1].replace("''", "'")
    return s


def kept_words(text):
    """The names and lines already in the asset: {('role'|'arch', value): (name, line)}."""
    out = {}
    if not text:
        return out
    for block, key in (("roles", "Role"), ("archetypes", "Archetype")):
        m = re.search(r"^  " + block + r":\n((?:  - .*\n(?:    .*\n)*)*)", text, re.M)
        if not m:
            continue
        for item in re.finditer(r"  - " + key + r": (\d+)\n    Name: (.*)\n    Line: (.*)\n", m.group(1)):
            out[(block, int(item.group(1)))] = (unq(item.group(2)), unq(item.group(3)))
    return out


def render(script_guid, role_words, arch_words, vessels, counters, values, role_vals, arch_vals, kept):
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}", "  m_Name: VesselArchetypes",
             "  m_EditorClassIdentifier:", "  roles:"]
    for r in ROLES:
        name, line = kept.get(("roles", role_vals[r]), role_words[r])
        lines += [f"  - Role: {role_vals[r]}", f"    Name: {q(name)}", f"    Line: {q(line)}"]
    lines.append("  archetypes:")
    for a in ARCHS:
        name, line = kept.get(("archetypes", arch_vals[a]), arch_words[a])
        lines += [f"  - Archetype: {arch_vals[a]}", f"    Name: {q(name)}", f"    Line: {q(line)}"]
    lines.append("  vessels:")
    for v, archs in vessels:
        lines += [f"  - Vessel: {values[v]}", "    Archetypes:"] + [f"    - {arch_vals[a]}" for a in archs]
    lines.append("  counters:")
    for f, t in counters:
        lines += [f"  - From: {arch_vals[f]}", f"    To: {arch_vals[t]}"]
    return "\n".join(lines) + "\n"


def errors(role_words, arch_words, vessels, counters, values, role_vals, arch_vals):
    errs = []
    if [r for r in ROLES if r not in role_vals] or [a for a in ARCHS if a not in arch_vals]:
        errs.append("VesselArchetype.cs enums no longer name every role and archetype")
    for r in ROLES:
        if r not in role_words:
            errs.append(f"board has no role {r}")
    for a in ARCHS:
        if a not in arch_words:
            errs.append(f"board has no archetype {a}")
        elif arch_vals.get(a, 0) and (arch_vals[a] - 1) // 3 != ROLES.index(
                next(r for r, members in zip(ROLES, (ARCHS[0:3], ARCHS[3:6], ARCHS[6:9])) if a in members)):
            errs.append(f"{a}'s enum value is not with its role's three")
    seen = set()
    for v, archs in vessels:
        if v in seen:
            errs.append(f"{v} is on the board twice")
        seen.add(v)
        if not archs:
            errs.append(f"{v} has no archetype")
        if len(archs) > 3:
            errs.append(f"{v} has {len(archs)} archetypes; a vessel has at most a primary, a secondary and a tertiary")
        if len(set(archs)) != len(archs):
            errs.append(f"{v} lists an archetype twice")
    for f, t in counters:
        if f == t:
            errs.append(f"{f} counters itself")
    return errs


def build(board):
    values = enum_values(ENUM, "VesselClassType")
    role_vals = enum_values(ARCH_CS, "VesselRole")
    arch_vals = enum_values(ARCH_CS, "VesselArchetype")
    role_words, arch_words, vessels, counters, skipped = load_board(board, values)
    errs = errors(role_words, arch_words, vessels, counters, values, role_vals, arch_vals)
    guid = re.search(r"guid: (\w+)", SCRIPT_META.read_text()).group(1)
    existing = ASSET.read_text() if ASSET.exists() else ""
    text = render(guid, role_words, arch_words, vessels, counters, values, role_vals, arch_vals, kept_words(existing))
    return text, existing, errs, skipped, vessels


def self_test():
    board = json.loads(BOARD.read_text())
    values = enum_values(ENUM, "VesselClassType")
    rv, av = enum_values(ARCH_CS, "VesselRole"), enum_values(ARCH_CS, "VesselArchetype")
    base = load_board(board, values)
    assert not errors(*base[:4], values, rv, av), "the shipped board must pass"
    planted = 0
    rw, aw, vessels, counters, _ = base
    for name, mutate in (
            ("four archetypes", lambda v: [(v[0][0], v[0][1] + ["Zoner", "Nuke", "Burster"])] + v[1:]),
            ("no archetype", lambda v: [(v[0][0], [])] + v[1:]),
            ("twice", lambda v: v + [v[0]]),
            ("duplicate archetype", lambda v: [(v[0][0], [v[0][1][0], v[0][1][0]])] + v[1:])):
        assert errors(rw, aw, mutate(list(vessels)), counters, values, rv, av), f"planted '{name}' not caught"
        planted += 1
    assert errors(rw, aw, vessels, counters + [("Nuke", "Nuke")], values, rv, av), "self-counter not caught"
    assert errors(rw, {k: w for k, w in aw.items() if k != "Zoner"}, vessels, counters, values, rv, av), "missing archetype"
    planted += 2
    # Words survive a re-run; tertiary keeps its place.
    text = render("g", rw, aw, vessels, counters, values, rv, av, {})
    edited = text.replace("Name: Pumper", "Name: Racer")
    kept = kept_words(edited)
    assert kept[("archetypes", av["Pumper"])][0] == "Racer", "an edited name was not kept"
    assert "Name: Racer" in render("g", rw, aw, vessels, counters, values, rv, av, kept), "an edited name was overwritten"
    sparrow = dict(vessels)["Sparrow"]
    assert sparrow == ["Sapper", "Harraser", "Builder"], f"Sparrow should be Sapper, Harraser, Builder; got {sparrow}"
    print(f"self-test OK: {planted} planted defects caught, words kept, tertiary order held")


def main(argv):
    if "--self-test" in argv:
        self_test()
        return 0
    text, existing, errs, skipped, vessels = build(json.loads(BOARD.read_text()))
    if "--table" in argv:
        for v, archs in vessels:
            print(f"{v:12s} " + "  ".join(f"{rank}:{a}" for rank, a in zip(("P", "S", "T"), archs)))
        return 0
    for e in errs:
        print("ERROR:", e)
    if skipped:
        print("skipped (not in VesselClassType yet):", ", ".join(skipped))
    if errs:
        return 1
    if "--check" in argv:
        if text != existing:
            print("DRIFT:", ASSET.relative_to(ROOT))
            return 1
        print(f"OK: {len(vessels)} vessels, no drift")
        return 0
    ASSET.write_text(text)
    print(f"wrote {ASSET.relative_to(ROOT)} ({len(vessels)} vessels)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
