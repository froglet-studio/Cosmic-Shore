#!/usr/bin/env python3
"""
Fail the build if FaunaNeutralPalette's FALLBACK pair has drifted from the shipped palette.

`_FaunaNeutralBright` / `_FaunaNeutralDull` are UNEXPOSED Shader Graph properties — plain
shader globals — and an unset global is ZERO, so a scene with no ThemeManager would render
every creature BLACK. FaunaNeutralPalette therefore carries a literal copy of the pair and
publishes it at BeforeSceneLoad.

A literal copy of an authored value is a SECOND SOURCE OF TRUTH, and the one thing that keeps
it honest is a check that reads both. This reads:

  * Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset -> BlueColors shielded pair
  * Assets/_Scripts/.../FaunaNeutralPalette.cs                -> FallbackBright / FallbackDull

and requires them equal to float precision. It also asserts the ONE thing the C# cannot say
about itself: that OriginalColorSetSO is still the palette ThemeManagerDataContainer is wired
to, because a fallback measured off an unwired asset is worse than no fallback.

Usage:  python3 Tools/Build/check_fauna_neutral_palette.py [--self-test]
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PALETTE = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset"
CONTAINER = "Assets/_SO_Assets/ThemeManagerDataContainer.asset"
CS = "Assets/_Scripts/Controller/Environment/FloraAndFauna/FaunaNeutralPalette.cs"

# Docs/PALETTE.md §2: Inside* is the fresnel RIM, Outside* is the base FACE.
RIM_FIELD = "ShieldedInsideBlockColor"
BASE_FIELD = "ShieldedOutsideBlockColor"


def read(path):
    full = os.path.join(REPO, path)
    assert os.path.exists(full), "missing %s" % path
    return open(full, encoding="utf-8").read()


def blue_block(text):
    i = text.find("  BlueColors:")
    assert i >= 0, "OriginalColorSetSO has no BlueColors block"
    m = re.compile(r"\n  [A-Za-z_]+:").search(text, i + 4)
    return text[i:m.start() if m else len(text)]


def colour(block, field):
    # a colour can wrap across lines when the line would be long
    m = re.search(r"%s: \{([^}]*)\}" % re.escape(field), block, re.S)
    assert m, "no %s in the Blue block" % field
    body = m.group(1).replace("\n", " ")
    out = {}
    for k in "rgba":
        km = re.search(r"\b%s: (-?[0-9.eE+]+)" % k, body)
        out[k] = float(km.group(1)) if km else (1.0 if k == "a" else 0.0)
    return out


def cs_colour(text, name):
    m = re.search(r"%s\s*=\s*new Color\(([^)]*)\)" % re.escape(name), text)
    assert m, "no %s literal in FaunaNeutralPalette.cs" % name
    parts = [float(p.strip().rstrip("fF")) for p in m.group(1).split(",")]
    assert len(parts) == 4, "%s must be authored with all four channels" % name
    return dict(zip("rgba", parts))


def close(a, b):
    return all(abs(a[k] - b[k]) <= 1e-6 for k in "rgba")


def fmt(c):
    return "(%.7f, %.7f, %.7f, %.7f)" % (c["r"], c["g"], c["b"], c["a"])


def check(cs_text=None):
    problems = []

    container = read(CONTAINER)
    palette_guid = re.search(r"^guid: ([0-9a-f]{32})", read(PALETTE + ".meta"), re.M).group(1)
    if palette_guid not in container:
        problems.append(
            "ThemeManagerDataContainer is NOT wired to %s — the fallback is measured off a "
            "palette nothing renders with" % os.path.basename(PALETTE))

    blue = blue_block(read(PALETTE))
    want_rim = colour(blue, RIM_FIELD)
    want_base = colour(blue, BASE_FIELD)

    cs = cs_text if cs_text is not None else read(CS)
    got_rim = cs_colour(cs, "FallbackBright")
    got_base = cs_colour(cs, "FallbackDull")

    if not close(want_rim, got_rim):
        problems.append("FallbackBright %s != BlueColors.%s %s"
                        % (fmt(got_rim), RIM_FIELD, fmt(want_rim)))
    if not close(want_base, got_base):
        problems.append("FallbackDull %s != BlueColors.%s %s"
                        % (fmt(got_base), BASE_FIELD, fmt(want_base)))

    # Docs/PALETTE.md §4.0 — the invariant that outranks every per-tier contract.
    if max(got_rim["r"], got_rim["g"], got_rim["b"]) <= max(got_base["r"], got_base["g"], got_base["b"]):
        problems.append("the rim is not brighter than the base — PALETTE.md §4.0")

    return problems, want_rim, want_base


def main():
    if "--self-test" in sys.argv:
        bad = read(CS).replace("1.1131275f", "0.5f")
        problems, _, _ = check(bad)
        assert any("FallbackBright" in p for p in problems), \
            "SELF-TEST FAIL: a drifted literal did not fire"
        clean, _, _ = check()
        assert not clean, "SELF-TEST FAIL: the live tree is not clean, cannot prove the control"
        print("self-test OK: a drifted literal fires, the live tree is clean")
        return

    problems, rim, base = check()
    if problems:
        for p in problems:
            print("FAIL: %s" % p, file=sys.stderr)
        sys.exit(1)
    print("fauna neutral pair = Blue SHIELDED")
    print("  rim  (_FaunaNeutralBright) %s" % fmt(rim))
    print("  base (_FaunaNeutralDull)   %s" % fmt(base))
    print("OK")


if __name__ == "__main__":
    main()
