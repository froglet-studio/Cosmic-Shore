#!/usr/bin/env python3
"""Every vessel prefab claims a DISTINCT, DECLARED class, and the container resolves all of them.

    python3 Tools/Build/check_vessel_prefab_container.py             # fail on a wrong/duplicate claim
    python3 Tools/Build/check_vessel_prefab_container.py --self-test # prove it fires

`VesselPrefabContainer.TryGetShipPrefab` is CONTENT-ADDRESSED: it walks `_shipPrefabs` and returns
the first entry whose `VesselStatus.vesselType` matches the class asked for. So the serialized
`vesselType` on a prefab is not a label - it is the prefab's ADDRESS, and a wrong one makes a hull
unreachable while making another hull ambiguous, with nothing anywhere reporting either.

That shipped: `ButterflyVesselSetup` wrote the field through `SerializedProperty.enumValueIndex`,
which is the position in the enum's NAME LIST rather than the member's number. The two agree only
while an enum is zero-based and contiguous, and `VesselClassType` starts at `Any = -1` - so
`Butterfly` (13) was stored as the 14th declared member, **Scarab (12)**. The Butterfly could not
be resolved at all (absent from the Vessel Changer and the Spawn Matrix hangar) and became a
second claimant for Scarab. Nothing reports it: the write succeeds, the field holds a valid member
of the right type, and the inspector reads correctly because the inspector shows what is stored.

Checks, over `Assets/_SO_Assets/Vessel Prefab Container.asset` and the prefabs it references:

  1. every entry's guid resolves to a prefab under `Assets/_Prefabs/Spacevessels/`
  2. every referenced prefab declares a `vesselType`
  3. that value is a DECLARED member of `VesselClassType` (not an index that landed in range)
  4. it is not a meta value (`Any` / `Random` are not hulls)
  5. no two entries claim the same class
  6. the claim matches the prefab's FILE NAME where the name is itself a class name - the one
     independent witness available offline, and the thing an index-vs-value slip always breaks

Deliberately NOT checked: that a class the fleet rosters has an entry at all. A declared-but-unbuilt
vessel is a legitimate state (`ToyVesselRoster.Default` lists hulls before their prefabs exist, and
`ResolveOffered` drops them with a keyed warning), so absence is a report, not a fault.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
ENUM = os.path.join(ROOT, "Assets", "_Scripts", "Data", "Enums", "VesselClassType.cs")
VESSEL_DIR = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
META_VALUES = ("Any", "Random")


def read_enum(text):
    """name -> value, in declaration order."""
    out = {}
    for name, value in re.findall(r"^\s*(\w+)\s*=\s*(-?\d+)\s*,", text, re.M):
        out[name] = int(value)
    return out


def audit(entries, members, declared_types, names_by_path):
    """entries: [(guid, path_or_None)]; declared_types: path -> vesselType int or None."""
    findings = []
    by_value = {v: k for k, v in members.items()}
    seen = {}
    for guid, path in entries:
        if path is None:
            findings.append(f"entry guid {guid} resolves to no prefab under Assets/_Prefabs/Spacevessels/")
            continue
        name = names_by_path[path]
        value = declared_types.get(path)
        if value is None:
            findings.append(f"{name}: no VesselStatus.vesselType in the prefab")
            continue
        if value not in by_value:
            findings.append(f"{name}: vesselType {value} is not a declared VesselClassType member")
            continue
        claim = by_value[value]
        if claim in META_VALUES:
            findings.append(f"{name}: claims the meta value {claim} ({value}), which is not a hull")
            continue
        if claim in seen:
            findings.append(
                f"{name}: claims {claim} ({value}), already claimed by {seen[claim]} — "
                "the container returns the FIRST match, so one of these is unreachable")
            continue
        seen[claim] = name
        stem = os.path.splitext(name)[0]
        if stem in members and stem != claim:
            findings.append(
                f"{name}: claims {claim} ({value}) but is named for {stem} ({members[stem]}) — "
                "an enumValueIndex-vs-value slip looks exactly like this")
    return findings


def self_test():
    members = {"Any": -1, "Random": 0, "Manta": 1, "Scarab": 12, "Butterfly": 13}
    ok = [("g1", "Manta.prefab"), ("g2", "Butterfly.prefab")]
    names = {"Manta.prefab": "Manta.prefab", "Butterfly.prefab": "Butterfly.prefab"}
    assert audit(ok, members, {"Manta.prefab": 1, "Butterfly.prefab": 13}, names) == []
    # the shipped bug: Butterfly written by index, landing on Scarab
    bug = audit(ok, members, {"Manta.prefab": 1, "Butterfly.prefab": 12}, names)
    assert len(bug) == 1 and "named for Butterfly" in bug[0], bug
    # a duplicate claim
    dup = audit([("g1", "Manta.prefab"), ("g2", "Butterfly.prefab")], members,
                {"Manta.prefab": 1, "Butterfly.prefab": 1},
                names)
    assert len(dup) == 1 and "already claimed" in dup[0], dup
    # a meta value
    meta = audit([("g1", "Manta.prefab")], members, {"Manta.prefab": 0}, {"Manta.prefab": "Manta.prefab"})
    assert len(meta) == 1 and "meta value" in meta[0], meta
    # an undeclared value (an index that landed out of range)
    bad = audit([("g1", "Manta.prefab")], members, {"Manta.prefab": 99}, {"Manta.prefab": "Manta.prefab"})
    assert len(bad) == 1 and "not a declared" in bad[0], bad
    # a dangling entry
    gone = audit([("deadbeef", None)], members, {}, {})
    assert len(gone) == 1 and "resolves to no prefab" in gone[0], gone
    print("self-test OK (1 clean, 5 faults, each firing once)")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()

    members = read_enum(open(ENUM, encoding="utf-8").read())
    if not members:
        print("FAIL: could not read VesselClassType")
        return 1

    by_guid = {}
    for meta in glob.glob(os.path.join(VESSEL_DIR, "*.prefab.meta")):
        m = re.search(r"^guid: ([0-9a-f]{32})$", open(meta, encoding="utf-8").read(), re.M)
        if m:
            by_guid[m.group(1)] = meta[: -len(".meta")]

    text = open(CONTAINER, encoding="utf-8").read()
    guids = re.findall(r"- \{fileID: \d+, guid: ([0-9a-f]{32}), type: 3\}", text)
    if not guids:
        print("FAIL: Vessel Prefab Container has no entries")
        return 1

    entries, declared, names = [], {}, {}
    for guid in guids:
        path = by_guid.get(guid)
        entries.append((guid, path))
        if path is None:
            continue
        names[path] = os.path.basename(path)
        m = re.search(r"^  vesselType: (-?\d+)$", open(path, encoding="utf-8", errors="ignore").read(), re.M)
        declared[path] = int(m.group(1)) if m else None

    findings = audit(entries, members, declared, names)
    if findings:
        print("FAIL: Vessel Prefab Container entries that cannot be resolved as authored:")
        for f in findings:
            print("  - " + f)
        return 1
    print(f"OK: {len(guids)} vessel prefabs, every one a distinct declared VesselClassType.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
