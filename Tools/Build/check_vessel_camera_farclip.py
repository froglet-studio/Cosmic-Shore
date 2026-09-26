#!/usr/bin/env python3
"""Every vessel's camera must draw as far as the fleet's does.

WHY THIS EXISTS.

`CameraSettingsSO.farClipPlane` is per vessel, and a vessel-setup tool typically names only the
one or two fields that hull actually differs on — `followOffset` — leaving everything else at the
C# field initializer. That initializer was **1000** while all nine shipped assets say **12000**,
so a hull authored that way came out with a twelfth of the fleet's draw distance.

The Butterfly shipped exactly that way. 1000 does not even cross a standard 1200-radius cell
(2400 units across), so the far wall of the arena was clipped away entirely and the report was
"the draw distance goes way down with the Butterfly" — a bug read as being in the camera rather
than in a field nobody named. Nothing catches it: the asset carries the key, the value is a
perfectly legal float, and the only symptom is geometry that is not drawn.

WHAT THIS CHECKS

The fleet's own agreement, measured: the MODE of `farClipPlane` across every
`CameraSettingsSO` asset, and then every asset against it. A hull below the fleet value fails.

WHAT THIS DOES NOT PROVE

That the fleet value is *enough* for any particular arena. It is a consistency gate, not a
sufficiency one — but the sufficiency argument is worth stating, because it is what makes 12000
the right floor rather than an arbitrary one: the largest arena the game ships is Cleave's
3600-radius membrane, ~7200 units across, and the longest camera setback in the fleet is the
Serpent's 250, so a pilot at one edge looking across needs ~7450. 12000 clears it; 1000 clears
nothing larger than a 500-radius cell.

Run:  python3 Tools/Build/check_vessel_camera_farclip.py [--self-test]
"""
import collections
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# The Borromean tool's trap: a ROOT one dirname too shallow resolves to Tools/ and every
# path under it silently misses, which a reader reports as "nothing found" rather than as a
# bug in itself. Assert the tree is under it.
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is wrong: {ROOT}"
CAMERA_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "Camera")

# A hull may legitimately want a LONGER far plane; it may never want a shorter one. Put a hull
# here only with a reason, and the reason belongs in this dict, not in a commit message.
ALLOWED_BELOW_FLEET = {}          # name -> reason


def read(path):
    with open(path, "r", encoding="utf-8") as fh:
        return fh.read()


def far_clip(text):
    m = re.search(r"^  farClipPlane: ([-\d.eE+]+)\s*$", text, re.M)
    return float(m.group(1)) if m else None


def collect():
    """name -> farClipPlane (None when the asset does not carry the key)."""
    out = {}
    for path in sorted(glob.glob(os.path.join(CAMERA_DIR, "*CameraSettingsSO.asset"))):
        out[os.path.basename(path)[: -len("CameraSettingsSO.asset")]] = far_clip(read(path))
    return out


def evaluate(values):
    """Returns (fleet_value, list_of_failure_strings). Pure, so --self-test can drive it."""
    present = [v for v in values.values() if v is not None]
    if not present:
        return None, ["no CameraSettingsSO asset carries a farClipPlane — has the field been renamed?"]

    fleet = collections.Counter(present).most_common(1)[0][0]
    fails = []

    for name, value in sorted(values.items()):
        if value is None:
            # The key is absent, so the asset silently takes the C# initializer. That is the exact
            # shape this gate exists to catch, whatever the initializer currently is.
            fails.append(f"{name}: no farClipPlane authored — it takes whatever the C# "
                         f"initializer is, which is not a decision anybody made. Author {fleet:g}.")
            continue
        if value < fleet and name not in ALLOWED_BELOW_FLEET:
            fails.append(f"{name}: farClipPlane {value:g} is below the fleet's {fleet:g} "
                         f"({fleet / value:.1f}x less draw distance). A standard cell is 1200 "
                         f"radius = 2400 across.")
    return fleet, fails


def self_test():
    """Negative controls. A gate nobody has watched fail is a gate nobody should trust."""
    cases = [
        ("the shipped tree passes", collect(), 0),
        ("the Butterfly's shipped-broken value fails",
         dict(collect(), Butterfly=1000.0), 1),
        ("a missing key fails",
         dict(collect(), Butterfly=None), 1),
        ("a LONGER far plane is fine",
         dict(collect(), Butterfly=40000.0), 0),
    ]
    ok = True
    for label, values, expect in cases:
        _, fails = evaluate(values)
        got = 1 if fails else 0
        flag = "PASS" if got == expect else "FAIL"
        if got != expect:
            ok = False
        print(f"  [{flag}] {label}")
        if got != expect:
            for f in fails:
                print("         " + f)
    return 0 if ok else 1


def main():
    if "--self-test" in sys.argv:
        return self_test()

    values = collect()
    fleet, fails = evaluate(values)

    if fails:
        print("vessel camera far-clip: FAIL")
        for f in fails:
            print("  " + f)
        return 1

    print(f"vessel camera far-clip: OK — {len(values)} vessel cameras, all at or above the "
          f"fleet's {fleet:g}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
