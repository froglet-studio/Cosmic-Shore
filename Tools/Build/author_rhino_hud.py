#!/usr/bin/env python3
"""Author the Rhino's plough HUD in RhinoHUDVariant.prefab itself (the roster audit, 2026-11-13).

The sword left the Rhino for the Swordfish on 2026-11-07 (the Swordfish has its own SwordfishHUDVariant since #1930,
and the Jacks theirs since #1948), but RhinoHUDVariant was still the sword's HUD: transparent card containers the view
drew a katana, an afterimage trail and Blade Reach under, three afterimage part objects, and the view's sword fields.
The Rhino wore its plough cards only through overrides on its nested instance in Rhino.prefab. Now the variant IS the
plough HUD, so the hull needs no overrides:

  * the Charge, Mass and Space icon Images carry the icon kit's plough sprites (Gore, Heft, Blade; specs/rhino.py) at
    full alpha, and each card's level-5 picture is its binding's upgradedSprite (Gore Steal, Berm, Unstoppable); the
    Time card stays the Ramp Spool / Momentum container over its four parts (the view shows them by state);
  * the three afterimage part objects (AfterimageFar / Near / Blade, with their components) are deleted, and the
    view's katana / afterimage / Blade Reach fields with them (RhinoVesselHUDView no longer has those fields);
  * Rhino.prefab's overrides on the nested instance that did this job are removed, and its RhinoHUDController's sword
    references (swordDriver, swipe, afterimage) with the C# fields they named.

    python3 Tools/Build/author_rhino_hud.py           # write
    python3 Tools/Build/author_rhino_hud.py --check   # exit 1 on drift
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/RhinoHUDVariant.prefab"
VARIANT_GUID = "2656b477a4af7304180e65f2848a84b3"
VESSEL = "Assets/_Prefabs/Spacevessels/Rhino.prefab"
ICONS_DIR = "Assets/_Graphics/Icons/AbilityIcons/Rhino"
VIEW = "5161366343835098938"   # RhinoVesselHUDView on the variant
CONTROLLER_SCRIPT = "c636825408af4ed7bb3bc74ab877922d"   # RhinoHUDController, on Rhino.prefab
CONTROLLER_SWORD_FIELDS = ("swordDriver", "swipe", "afterimage")   # its sword refs, gone from the C# on 2026-11-13
# element -> (icon Image fileID, base sprite, level-5 sprite); Time (4) stays the kit's container
CARDS = {1: ("7720000000000003015", "Rhino_Gore", "Rhino_GoreSteal"),
         2: ("7720000000000003025", "Rhino_Heft", "Rhino_Berm"),
         3: ("7720000000000003035", "Rhino_Blade", "Rhino_Unstoppable")}
SWORD_PARTS = ("AfterimageFar", "AfterimageNear", "AfterimageBlade")
SWORD_FIELDS = ("katanaCurvedSprite", "katanaChopSprite", "katanaAuthoredSize", "katanaHilt", "katanaSwipeFraction",
                "katanaChopStraight01", "katanaChopLength01", "katanaDarkColor", "katanaLitColor", "afterimageBladeIcon",
                "afterimageNearIcon", "afterimageFarIcon", "afterimageNearAlpha", "afterimageFarAlpha", "reachHiltSprite",
                "reachBladeSprite", "reachPrismSprite", "reachPrismBrokenSprite", "reachPostsSprite", "reachPartHilt",
                "reachCardHilt", "reachAngle", "reachLengthRestMax", "reachPrismAlong", "reachPrismSidePerLength",
                "reachPostsAlpha")


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
        return f.read()


def guid_of(name):
    with open(os.path.join(ROOT, ICONS_DIR, name + ".png.meta"), encoding="utf-8") as f:
        return re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M).group(1)


def sprite(name):
    return f"{{fileID: 21300000, guid: {guid_of(name)}, type: 3}}"


def docs(text):
    """(head, [(fileID, document text)]) in file order."""
    parts = re.split(r"(?m)^(?=--- !u!\d+ &)", text)
    return parts[0], [(re.match(r"--- !u!\d+ &(-?\d+)", d).group(1), d) for d in parts[1:]]


def variant(text):
    head, ds = docs(text)
    by_id = dict(ds)
    dead = set()
    for fid, d in ds:
        if d.startswith("--- !u!1 &") and re.search(r"^  m_Name: (%s)$" % "|".join(SWORD_PARTS), d, re.M):
            dead.add(fid)
            dead.update(re.findall(r"component: \{fileID: (\d+)\}", d))
    dead_rts = {fid for fid in dead if by_id[fid].startswith("--- !u!224 ")}
    out = [head]
    for fid, d in ds:
        if fid in dead:
            continue
        if d.startswith("--- !u!224 ") and "m_Children:" in d:
            for rt in dead_rts:
                d = d.replace(f"  - {{fileID: {rt}}}\n", "")
            d = re.sub(r"  m_Children:\n(?=  m_Father:)", "  m_Children: []\n", d)
        if fid == VIEW:
            d = re.sub(r"^  (?:%s):.*\n(?:    .*\n)*" % "|".join(SWORD_FIELDS), "", d, flags=re.M)
            for el, (_icon, _base, up) in CARDS.items():
                d, n = re.subn(r"(  - element: %d\n    icon: \{fileID: \d+\}\n    upgradedSprite: ).*\n" % el,
                               lambda m: m.group(1) + sprite(up) + "\n", d, count=1)
                assert n == 1, f"RhinoHUDVariant: no abilityIcons entry for element {el}"
        for el, (icon, base, _up) in CARDS.items():
            if fid == icon:
                d, n1 = re.subn(r"(?m)^  m_Sprite: .*$", "  m_Sprite: " + sprite(base), d, count=1)
                d, n2 = re.subn(r"(?m)^  m_Color: \{r: 1, g: 1, b: 1, a: [\d.]+\}$", "  m_Color: {r: 1, g: 1, b: 1, a: 1}", d, count=1)
                assert n1 == 1 and n2 == 1, f"RhinoHUDVariant: icon {icon} shape changed"
        out.append(d)
    return "".join(out)


def vessel(text, dead_ids):
    """Drop the nested instance's overrides that did the variant's job (or name what the variant no longer has)."""
    icon_ids = {icon for icon, _b, _u in CARDS.values()}

    def keep(m):
        target, path = m.group(1), m.group(2)
        if target in icon_ids and path in ("m_Sprite", "m_Color.a"):
            return ""
        if target == VIEW and path in SWORD_FIELDS:
            return ""
        if target in dead_ids:
            return ""
        return m.group(0)

    text = re.sub(r"    - target: \{fileID: (\d+), guid: %s,\s+type: 3\}\n      propertyPath: (\S+)\n      value: .*\n"
                  r"      objectReference: [^\n]*(?:\n        [^\n]*)?\n" % VARIANT_GUID, keep, text)
    head, ds = docs(text)
    return head + "".join(re.sub(r"^  (?:%s): .*\n" % "|".join(CONTROLLER_SWORD_FIELDS), "", d, flags=re.M)
                          if f"guid: {CONTROLLER_SCRIPT}, type: 3}}" in d else d for _fid, d in ds)


def dead_ids(text):
    _head, ds = docs(text)
    out = set()
    for fid, d in ds:
        if d.startswith("--- !u!1 &") and re.search(r"^  m_Name: (%s)$" % "|".join(SWORD_PARTS), d, re.M):
            out.add(fid)
            out.update(re.findall(r"component: \{fileID: (\d+)\}", d))
    return out


def check(var, ves):
    errs = []
    if dead_ids(var):
        errs.append("RhinoHUDVariant: the afterimage part objects are still there")
    view = dict(docs(var)[1]).get(VIEW, "")
    for f in SWORD_FIELDS:
        if re.search(r"^  %s:" % f, view, re.M):
            errs.append(f"RhinoHUDVariant: the view still serializes the sword field {f}")
    for el, (icon, base, up) in CARDS.items():
        img = dict(docs(var)[1]).get(icon, "")
        if f"  m_Sprite: {sprite(base)}" not in img or "  m_Color: {r: 1, g: 1, b: 1, a: 1}" not in img:
            errs.append(f"RhinoHUDVariant: card {el} is not {base} at full alpha")
        if not re.search(r"  - element: %d\n    icon: \{fileID: %s\}\n    upgradedSprite: %s\n" % (el, icon, re.escape(sprite(up))), view):
            errs.append(f"RhinoHUDVariant: card {el}'s upgradedSprite is not {up}")
    if vessel(ves, set()) != ves:
        errs.append("Rhino.prefab: the nested HUD still overrides the cards' sprites or the view's sword fields, "
                    "or RhinoHUDController still names the sword")
    return errs


def main(argv):
    check_only = "--check" in argv
    var_have, ves_have = read(VARIANT), read(VESSEL)
    gone = dead_ids(var_have)
    var_want, ves_want = variant(var_have), vessel(ves_have, gone)
    drift = [rel for rel, have, want in ((VARIANT, var_have, var_want), (VESSEL, ves_have, ves_want)) if have != want]
    if not check_only:
        for rel, have, want in ((VARIANT, var_have, var_want), (VESSEL, ves_have, ves_want)):
            if have != want:
                with open(os.path.join(ROOT, rel), "w", encoding="utf-8") as f:
                    f.write(want)
        var_have, ves_have = var_want, ves_want
    errs = check(var_have, ves_have)
    for e in errs:
        print("FAIL:", e)
    if check_only:
        print("author_rhino_hud: " + ("ok" if not drift and not errs else "DRIFT " + ", ".join(drift)))
        return 1 if drift or errs else 0
    print("author_rhino_hud: wrote " + (", ".join(drift) if drift else "nothing (up to date)"))
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
