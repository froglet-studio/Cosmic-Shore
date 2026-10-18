#!/usr/bin/env python3
"""check_crew_vessels.py - every CREW vessel keeps the one notion of a crew vessel.

A crew vessel is one hull class whose prefab carries a second seat: a component that implements ICrewSeat
(Assets/_Scripts/Controller/Vessel/Crew/CrewSeat.cs). Today: the Wrasse (KiteNavigator, card "Wrasse and Grouper") and
the Goby (GobyPistolDuo, card "Goby and Pistol"). The contract is .claude/skills/duo-vessel/SKILL.md "One notion of a
crew vessel"; this gate holds the parts of it a file can show:

    seat      every hull prefab under Assets/_Prefabs/Spacevessels/ whose components include an ICrewSeat script is a
              crew hull, and carries exactly one seat
    card      its class asset (SO_Class_*.asset with the hull's vesselType as Class) names both subvessels:
              CrewNames with two entries and a CrewNameTemplate that uses {0} and {1} (SO_Vessel.CardName), and its
              Name stays the one-word key (the hangar's unlocks are keyed by it)
    name      the seat's SeatName is one of the subvessels its card names (CrewNames)
    reach     it is in ToyVesselRoster.Default (the Vessel Changer offers it) and the Vessel Prefab Container
    only      a class that names a crew has a hull that carries a seat (no card claims two subvessels it lacks)

    python3 Tools/Build/check_crew_vessels.py              # the shipped set
    python3 Tools/Build/check_crew_vessels.py --self-test  # each check fires on a broken copy (negative controls)

A READER: it writes nothing.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
ROSTER = os.path.join(SCRIPTS, "Controller", "Toys", "ToyVesselRoster.cs")
ENUM = os.path.join(SCRIPTS, "Data", "Enums", "VesselClassType.cs")


def read(path):
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def guid_of(path):
    m = re.search(r"^guid: ([0-9a-f]{32})", read(path + ".meta"), re.M)
    return m.group(1) if m else None


def seat_scripts():
    """{guid: (class name, SeatName)} of every MonoBehaviour that implements ICrewSeat."""
    out = {}
    for d, _, files in os.walk(SCRIPTS):
        for f in files:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(d, f)
            for m in re.finditer(r"class\s+(\w+)\s*:\s*([^{\n]*)", read(p)):
                bases = [b.strip() for b in m.group(2).split(",")]
                if "ICrewSeat" in bases and m.group(1) + ".cs" == f and os.path.exists(p + ".meta"):
                    n = re.search(r'string SeatName\s*=>\s*"([^"]*)"', read(p))
                    out[guid_of(p)] = (m.group(1), n.group(1) if n else None)
    return out


def enum_members():
    return {name: int(val) for name, val in re.findall(r"^\s+(\w+)\s*=\s*(-?\d+)", read(ENUM), re.M)}


def roster_members():
    m = re.search(r"Default\s*=.*?\{(.*?)\};", read(ROSTER), re.S)
    return set(re.findall(r"VesselClassType\.(\w+)", m.group(1))) if m else set()


def class_assets():
    """{class id: (file name, text)}"""
    out = {}
    for f in os.listdir(CLASSES):
        if f.startswith("SO_Class_") and f.endswith(".asset"):
            text = read(os.path.join(CLASSES, f))
            m = re.search(r"^  Class: (-?\d+)$", text, re.M)
            if m:
                out[int(m.group(1))] = (f, text)
    return out


def crew_names(text):
    m = re.search(r"^  CrewNames:\n((?:  - .*\n)*)", text, re.M)
    names = re.findall(r"^  - (.*)$", m.group(1), re.M) if m else []
    t = re.search(r"^  CrewNameTemplate: (.*)$", text, re.M)
    template = t.group(1).strip().strip("'\"") if t else ""
    return names, template


def check(prefabs, classes, seats, roster, members, container):
    """prefabs: {file: text}; classes: {id: (file, text)}; seats: {guid: (name, SeatName)}. Returns (errors, crew hulls)."""
    errs, hulls = [], []
    by_value = {v: k for k, v in members.items()}
    crew_ids = set()
    for f, text in sorted(prefabs.items()):
        found_seats = [seats[g] for g in re.findall(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})", text) if g in seats]
        found = [n for n, _ in found_seats]
        if not found:
            continue
        vt = re.search(r"^  vesselType: (-?\d+)$", text, re.M)
        if not vt:
            errs.append(f"{f}: carries a crew seat ({', '.join(found)}) but no vesselType")
            continue
        vid = int(vt.group(1)); crew_ids.add(vid)
        name = by_value.get(vid, str(vid))
        hulls.append((name, found[0]))
        if len(found) != 1:
            errs.append(f"{f}: {len(found)} crew seats ({', '.join(found)}); a crew hull carries one")
        cls = classes.get(vid)
        if not cls:
            errs.append(f"{f}: no SO_Class_*.asset with Class: {vid}")
        else:
            names, template = crew_names(cls[1])
            if len(names) < 2 or not all(n.strip() for n in names[:2]):
                errs.append(f"{cls[0]}: a crew hull's card names both subvessels (CrewNames, two entries; found {names})")
            seat_name = found_seats[0][1]
            if seat_name not in names:
                errs.append(f"{cls[0]}: {found[0]}.SeatName {seat_name!r} is not one of the card's CrewNames {names}")
            if "{0}" not in template or "{1}" not in template:
                errs.append(f"{cls[0]}: CrewNameTemplate {template!r} does not join both names ({{0}} and {{1}})")
            key = re.search(r"^  Name: (.*)$", cls[1], re.M)
            if not key or not re.fullmatch(r"\w+", key.group(1).strip()):
                errs.append(f"{cls[0]}: Name must stay the one-word vessel key (found {key.group(1) if key else None!r})")
        if name not in roster:
            errs.append(f"ToyVesselRoster.Default: {name} is a crew hull the Vessel Changer does not offer")
        g = guid_of(os.path.join(VESSELS, f)) if os.path.exists(os.path.join(VESSELS, f + ".meta")) else None
        if g and g not in container:
            errs.append(f"Vessel Prefab Container: no entry for {f}")
    for vid, (f, text) in sorted(classes.items()):
        names, _ = crew_names(text)
        if len(names) >= 2 and vid not in crew_ids:
            errs.append(f"{f}: names a crew ({' and '.join(names[:2])}) but no hull of class {vid} carries a crew seat")
    return errs, hulls


def load():
    prefabs = {f: read(os.path.join(VESSELS, f)) for f in os.listdir(VESSELS) if f.endswith(".prefab")}
    return prefabs, class_assets(), seat_scripts(), roster_members(), enum_members(), read(CONTAINER)


def self_test():
    prefabs, classes, seats, roster, members, container = load()
    errs, hulls = check(prefabs, classes, seats, roster, members, container)
    assert not errs, errs
    assert len(hulls) >= 2, f"expected the Wrasse and the Goby, found {hulls}"
    fails = 0

    def expect(label, **broken):
        nonlocal fails
        args = dict(prefabs=prefabs, classes=classes, seats=seats, roster=roster, members=members, container=container)
        args.update(broken)
        e, _ = check(**args)
        if not e:
            print("self-test: a broken copy passed:", label); fails += 1

    name, _ = hulls[0]
    vid = members[name]
    f, text = classes[vid]
    expect("no crew names", classes={**classes, vid: (f, re.sub(r"  CrewNames:\n(?:  - .*\n)*", "  CrewNames: []\n", text))})
    expect("a template without {1}", classes={**classes, vid: (f, re.sub(r"(  CrewNameTemplate: ).*", r"\1'{0}'", text))})
    expect("Name made the card", classes={**classes, vid: (f, re.sub(r"(?m)^  Name: .*$", "  Name: A and B", text))})
    expect("off the roster", roster=roster - {name})
    hull_file = next(pf for pf, t in prefabs.items() if re.search(rf"^  vesselType: {vid}$", t, re.M))
    expect("out of the container", container=container.replace(guid_of(os.path.join(VESSELS, hull_file)), "0" * 32))
    seat_guid = next(iter(seats))
    expect("a card with no seat", prefabs={k: v.replace(seat_guid, "f" * 32) for k, v in prefabs.items()})
    expect("a seat its card does not name", seats={g: (n, "Nobody") for g, (n, _) in seats.items()})
    expect("two seats", prefabs={**prefabs, hull_file: prefabs[hull_file] + f"\n  m_Script: {{fileID: 11500000, guid: {seat_guid}, type: 3}}\n"})
    if fails:
        sys.exit(1)
    print(f"check_crew_vessels self-test: ok ({len(hulls)} crew hulls; 8 negative controls fire)")


def main(argv):
    if "--self-test" in argv:
        self_test(); return 0
    errs, hulls = check(*load())
    for e in errs:
        print("FAIL", e)
    if errs:
        print(f"check_crew_vessels: {len(errs)} problem(s)")
        return 1
    print("check_crew_vessels: ok - " + ", ".join(f"{n} ({s})" for n, s in hulls))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
