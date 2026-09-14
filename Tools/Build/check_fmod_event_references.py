#!/usr/bin/env python3
"""Cross-check every wired FMOD slot against the in-repo FMOD Studio project.

The gap census (`asset_gap_census.py`) answers *which slots are empty*. This answers the two
questions about the slots that are FULL, and one about the project on the other side:

  DANGLING   a slot points at a GUID no event in `Cosmic Shore/Cosmic Shore.fspro` carries — the
             event was deleted or the project was re-created, and the sound is silent at runtime
             with a perfectly plausible-looking path string still in the inspector.
  DRIFTED    the GUID resolves but the cached `Path` string disagrees with the event's real path.
             Harmless at runtime (FMOD resolves by GUID) and NOT harmless to a human: every tool,
             console message and code review reads the path, so the inspector is quietly lying.
  ORPHAN     an event authored in FMOD that nothing in the game references. Some are scratch, and
             some are a finished sound waiting for a slot nobody connected it to — which is the
             expensive case, because the work is already paid for.

The GUID layout is not documented anywhere and is not guessable: Unity serializes FMOD's four
int32s such that Data1 is big-endian, Data2 is big-endian with its 16-bit halves SWAPPED, and
Data3/Data4 are little-endian. `--self-test` pins that against a known pair from this repo, so a
future FMODUnity upgrade that changes it fails loudly instead of reporting every slot dangling.

Usage:
    python3 Tools/Build/check_fmod_event_references.py            # report
    python3 Tools/Build/check_fmod_event_references.py --check    # exit 1 on a DANGLING slot
    python3 Tools/Build/check_fmod_event_references.py --self-test

Reader only. Record: Docs/ASSET_GAPS/ARCHITECTURE.md.
"""
from __future__ import annotations

import argparse
import collections
import glob
import os
import re
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FMOD_PROJECT = os.path.join(ROOT, "Cosmic Shore", "Metadata")
ASSETS = os.path.join(ROOT, "Assets")
ASSET_EXTS = (".prefab", ".unity", ".asset")

SLOT_RE = re.compile(
    r"^(\s*)(\w+):\s*\n\1\s+Guid:\s*\n\1\s+Data1: (-?\d+)\s*\n\1\s+Data2: (-?\d+)\s*\n"
    r"\1\s+Data3: (-?\d+)\s*\n\1\s+Data4: (-?\d+)\s*\n\1\s+Path: ?(.*)$", re.M)
OVERRIDE_RE = re.compile(r"^\s+propertyPath: (\w+)\.(Guid\.Data[1-4]|Path)\n\s+value: ?(.*)$", re.M)
OBJ_RE = re.compile(r'<object class="(\w+)" id="\{([0-9a-f-]+)\}"')
NAME_RE = re.compile(r'<property name="name">\s*<value>(.*?)</value>', re.S)
FOLDER_RE = re.compile(r'<relationship name="folder">\s*<destination>\{([0-9a-f-]+)\}', re.S)


def swap16(x: int) -> int:
    return ((x & 0xFFFF) << 16) | ((x >> 16) & 0xFFFF)


def to_guid(d1: int, d2: int, d3: int, d4: int) -> str:
    b = (struct.pack(">I", d1 & 0xFFFFFFFF) + struct.pack(">I", swap16(d2 & 0xFFFFFFFF))
         + struct.pack("<I", d3 & 0xFFFFFFFF) + struct.pack("<I", d4 & 0xFFFFFFFF))
    h = b.hex()
    return f"{h[:8]}-{h[8:12]}-{h[12:16]}-{h[16:20]}-{h[20:]}"


def read_fmod_project():
    names, folders, parent = {}, set(), {}
    for f in glob.glob(os.path.join(FMOD_PROJECT, "Event", "*.xml")) + \
             glob.glob(os.path.join(FMOD_PROJECT, "EventFolder", "*.xml")):
        with open(f, encoding="utf-8", errors="ignore") as fh:
            t = fh.read()
        o = OBJ_RE.search(t)
        if not o:
            continue
        gid = o.group(2)
        nm = NAME_RE.search(t)
        names[gid] = nm.group(1) if nm else ""
        fo = FOLDER_RE.search(t)
        if fo:
            parent[gid] = fo.group(1)
        if o.group(1) == "EventFolder":
            folders.add(gid)
    return names, folders, parent


def event_path(gid, names, folders, parent) -> str:
    parts, p = [names[gid]], parent.get(gid)
    while p in folders:
        parts.append(names[p])
        p = parent.get(p)
    return "event:/" + "/".join(reversed(parts))


def collect_slots():
    """Every FMOD slot with a non-zero GUID, from assets AND from prefab-instance overrides."""
    rows = []
    for dp, dns, fns in os.walk(ASSETS):
        dns[:] = [d for d in dns if not d.startswith(".")]
        for fn in fns:
            if not fn.endswith(ASSET_EXTS):
                continue
            path = os.path.join(dp, fn)
            with open(path, encoding="utf-8", errors="ignore") as fh:
                t = fh.read()
            # NOT "Guid:" — a scene whose only FMOD data is prefab-instance overrides spells it
            # "propertyPath: x.Guid.Data1", so the tighter guard skipped every game scene and
            # under-reported this check by two thirds before anyone noticed.
            if "Guid" not in t:
                continue
            rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
            for m in SLOT_RE.finditer(t):
                d = [int(m.group(i)) for i in (3, 4, 5, 6)]
                if any(d):
                    rows.append((rel, m.group(2), to_guid(*d), m.group(7).strip()))
            vals = collections.defaultdict(dict)
            for m in OVERRIDE_RE.finditer(t):
                vals[m.group(1)][m.group(2)] = m.group(3).strip()
            for fldname, v in vals.items():
                d = [int(v.get(f"Guid.Data{i}", "0")) for i in (1, 2, 3, 4)]
                if any(d):
                    rows.append((rel, fldname, to_guid(*d), v.get("Path", "")))
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if not os.path.isdir(FMOD_PROJECT):
        print(f"FMOD project metadata not found at {FMOD_PROJECT}", file=sys.stderr)
        return 2
    names, folders, parent = read_fmod_project()
    events = {g for g in names if g not in folders}
    rows = collect_slots()
    dangling = [r for r in rows if r[2] not in names]
    resolved = [r for r in rows if r[2] in names]
    drifted = [(r, event_path(r[2], names, folders, parent)) for r in resolved
               if event_path(r[2], names, folders, parent) != r[3]]
    used = {r[2] for r in resolved}
    orphans = sorted(events - used, key=lambda g: event_path(g, names, folders, parent))
    print(f"FMOD reference check: {len(rows)} wired slots, {len(dangling)} dangling, {len(drifted)} path drift; "
          f"{len(events)} events authored, {len(used)} referenced, {len(orphans)} orphan")
    for r in dangling:
        print(f"  DANGLING {r[0]} :: {r[1]} -> {r[2]} (cached path {r[3]!r})")
    for r, real in drifted:
        print(f"  DRIFT    {r[0]} :: {r[1]} cached {r[3]!r} but the event is {real!r}")
    for g in orphans:
        print(f"  ORPHAN   {event_path(g, names, folders, parent)}")
    if a.check and dangling:
        return 1
    return 0


def self_test():
    # Pinned against this repo's own "Creature Death" event and the AudioSystem slot that points
    # at it. If FMODUnity ever changes how it packs the GUID, this fails rather than reporting
    # every slot in the project as dangling.
    assert to_guid(1638131852, 1318240636, 16663457, -705826648) == "61a3e88c-c17c-4e92-a143-fe00a8f0edd5"
    assert swap16(0x4E92C17C) == 0xC17C4E92
    doc = ("MonoBehaviour:\n  fooEvent:\n    Guid:\n      Data1: 1638131852\n      Data2: 1318240636\n"
           "      Data3: 16663457\n      Data4: -705826648\n    Path: event:/x\n")
    m = SLOT_RE.search(doc)
    assert m and m.group(2) == "fooEvent" and m.group(7).strip() == "event:/x"
    ovr = ("    - target: {fileID: 1}\n      propertyPath: barEvent.Guid.Data1\n      value: 5\n"
           "      objectReference: {fileID: 0}\n")
    assert OVERRIDE_RE.search(ovr).groups() == ("barEvent", "Guid.Data1", "5")
    print("self-test OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
