#!/usr/bin/env python3
"""The fleet floor rule (2026-11-16): a rising ElementalFloat's Floor is half its Min.

Every element-scaled number on every hull is an `ElementalFloat`: `Min` at the resting level,
`Max` at level 10, extrapolated across the element band [-5, 15]. In the deficit band
(level < 0) a rising parameter falls below its resting value, and the `Floor` decides how far.
Before this rule the fleet disagreed with itself: half the hulls floored at the resting value
(a deficit did nothing to them), others at 0.05 or 0 (Meerkat's zap fell to 0.025 petals), and
some never authored a floor at all. Rhino's Heft and Carry rested at 0, so they did nothing
until the pilot banked Mass or Charge.

The rule: a rising parameter (Enabled, Max > Min > 0) has `UseFloor: 1` and `Floor: Min / 2`.
At level -5 an ability keeps at least half its resting strength, never less, never nothing.
A falling parameter (a cooldown, an interval, a minimum speed: Max < Min) is out of scope: its
deficit side is bounded by the band (at most 1.5x at level -5) and its Floor guards the level-15
end. A rising parameter that rests at 0 is a finding too: it does nothing at rest.

Covers serialized blocks in .asset/.prefab files and nested-prefab overrides of `Scale`
(the skimmer's Space reach). It does not see bespoke `ElementalScaling.Multiplier(..., minMul)`
call sites; their floors are listed in ELEMENT_SCALING_UNIFICATION.md "The fleet floor rule".

This is a READER (Docs/TOOLING.md): it writes nothing.

    python3 Tools/Build/elemental_floor_rule.py            # report every rising float and its floor
    python3 Tools/Build/elemental_floor_rule.py --check    # exit 1 on any breach
    python3 Tools/Build/elemental_floor_rule.py --self-test
"""
import argparse
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ELEMENTS = {0: "None", 1: "Charge", 2: "Mass", 3: "Space", 4: "Time"}

# Exceptions, each with its reason. Keep this list short and argued.
EXEMPT = {
    # The Jacks lab owns this curve (JacksMath.DangerShare, held by JacksScalingTests): the danger
    # share of the herd is 0 at Charge -5 and 1 at Charge 15 by design, a share not a strength.
    ("JacksConfig.asset", "dangerShare"),
}

BLOCK_RE = re.compile(
    r"^(?P<ind>[ \t]*)(?:- )?(?P<name>\w+):[ \t]*\n"
    r"(?P=ind)[ \t]+Enabled:[ \t]*(?P<en>\d)[ \t]*\n"
    r"(?P=ind)[ \t]+Value:[ \t]*(?P<val>-?[\d.eE+]+)[ \t]*\n"
    r"(?P=ind)[ \t]+Min:[ \t]*(?P<min>-?[\d.eE+]+)[ \t]*\n"
    r"(?P=ind)[ \t]+Max:[ \t]*(?P<max>-?[\d.eE+]+)[ \t]*\n"
    r"(?P=ind)[ \t]+element:[ \t]*(?P<el>\d)[ \t]*\n"
    r"(?:(?P=ind)[ \t]+UseFloor:[ \t]*(?P<uf>\d)[ \t]*\n"
    r"(?P=ind)[ \t]+Floor:[ \t]*(?P<fl>-?[\d.eE+]+)[ \t]*\n)?", re.M)

MOD_RE = re.compile(
    r"    - target: (?P<tgt>\{fileID: -?\d+, guid: \w+,\s*type: \d\})\s*\n"
    r"      propertyPath: Scale\.(?P<prop>\w+)\s*\n"
    r"      value: (?P<val>[^\n]*)\n")


def rising_blocks(text):
    """(name, Min, Max, element, UseFloor or None, Floor or None) for each enabled rising block."""
    for m in BLOCK_RE.finditer(text):
        mn, mx = float(m["min"]), float(m["max"])
        if m["en"] != "1" or not mx > mn:
            continue
        uf = int(m["uf"]) if m["uf"] is not None else None
        fl = float(m["fl"]) if m["fl"] is not None else None
        yield m["name"], mn, mx, int(m["el"]), uf, fl


def rising_overrides(text):
    """The same tuple for nested-prefab `Scale` overrides that author both Min and Max."""
    by_target = {}
    for m in MOD_RE.finditer(text):
        by_target.setdefault(m["tgt"], {})[m["prop"]] = m["val"].strip()
    for props in by_target.values():
        if "Min" not in props or "Max" not in props:
            continue
        mn, mx = float(props["Min"]), float(props["Max"])
        if not mx > mn:
            continue
        uf = int(props["UseFloor"]) if "UseFloor" in props else None
        fl = float(props["Floor"]) if "Floor" in props else None
        yield "Scale (override)", mn, mx, int(props.get("element", 0) or 0), uf, fl


def breach(mn, uf, fl):
    """Why this rising float breaks the rule, or None."""
    if mn <= 0:
        return f"rests at {mn:g}: the ability does nothing at rest"
    want = 0.5 * mn
    if uf != 1:
        return f"no floor (want UseFloor 1, Floor {want:g})"
    if abs(fl - want) > 1e-4 * max(1.0, want):
        return f"floor {fl:g}, want {want:g} (half of rest {mn:g})"
    return None


def scan(root):
    rows = []
    for dirpath, _dirs, files in os.walk(os.path.join(root, "Assets")):
        for name in files:
            if not name.endswith((".asset", ".prefab")):
                continue
            path = os.path.join(dirpath, name)
            try:
                text = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            if "Min:" not in text and "Scale.Min" not in text:
                continue
            found = list(rising_blocks(text))
            if name.endswith(".prefab"):
                found += list(rising_overrides(text))
            for field, mn, mx, el, uf, fl in found:
                why = None if (name, field) in EXEMPT else breach(mn, uf, fl)
                rows.append((os.path.relpath(path, root), field, mn, mx, el, uf, fl, why))
    return rows


def self_test():
    good = "  a:\n    Enabled: 1\n    Value: 2\n    Min: 2\n    Max: 4\n    element: 1\n    UseFloor: 1\n    Floor: 1\n"
    flat = good.replace("    Floor: 1\n", "    Floor: 2\n")
    none = "  b:\n    Enabled: 1\n    Value: 1\n    Min: 1\n    Max: 2\n    element: 3\n"
    zero = "  c:\n    Enabled: 1\n    Value: 0\n    Min: 0\n    Max: 60\n    element: 2\n    UseFloor: 1\n    Floor: 0\n"
    falling = "  d:\n    Enabled: 1\n    Value: 9\n    Min: 9\n    Max: 3\n    element: 4\n    UseFloor: 1\n    Floor: 1\n"
    off = none.replace("Enabled: 1", "Enabled: 0")
    def why(text):
        return [breach(mn, uf, fl) for _n, mn, _mx, _el, uf, fl in rising_blocks(text)]
    assert why(good) == [None], why(good)
    assert why(flat)[0] and "want 1" in why(flat)[0], why(flat)
    assert why(none)[0] and "no floor" in why(none)[0], why(none)
    assert why(zero)[0] and "rests at 0" in why(zero)[0], why(zero)
    assert why(falling) == [] and why(off) == [], "falling and disabled floats are out of scope"
    mod = ("    - target: {fileID: 1, guid: ab, type: 3}\n      propertyPath: Scale.Min\n      value: 10\n"
           "    - target: {fileID: 1, guid: ab, type: 3}\n      propertyPath: Scale.Max\n      value: 30\n")
    over = [breach(mn, uf, fl) for _n, mn, _mx, _el, uf, fl in rising_overrides(mod)]
    assert over and "no floor" in over[0], over
    floored = mod + ("    - target: {fileID: 1, guid: ab, type: 3}\n      propertyPath: Scale.UseFloor\n      value: 1\n"
                     "    - target: {fileID: 1, guid: ab, type: 3}\n      propertyPath: Scale.Floor\n      value: 5\n")
    assert [breach(mn, uf, fl) for _n, mn, _mx, _el, uf, fl in rising_overrides(floored)] == [None]
    print("elemental_floor_rule self-test PASS")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 on any breach")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    rows = scan(ROOT)
    bad = [r for r in rows if r[7]]
    for path, field, mn, mx, el, uf, fl, why in sorted(rows):
        mark = "BREACH" if why else "ok    "
        floor = f"{fl:g}" if uf == 1 else "-"
        print(f"{mark} {ELEMENTS.get(el, el):6} {mn:>9g} -> {mx:<9g} floor {floor:<7} {field}  {path}"
              + (f"\n       {why}" if why else ""))
    print(f"\n{len(rows)} rising elemental floats, {len(bad)} breach(es) of the fleet floor rule")
    return 1 if (args.check and bad) else 0


if __name__ == "__main__":
    sys.exit(main())
