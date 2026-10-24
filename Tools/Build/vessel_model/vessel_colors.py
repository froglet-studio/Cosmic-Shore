#!/usr/bin/env python3
"""
vessel_colors - the fleet's vessel colour scheme, in ONE place: the three shared vessel materials (Body, Window,
Engine) and, read from the palette, the three domain liveries. The game draws it, and `render_cards.py` renders
every card and hangar preview from the same numbers, so a card is the ship the player flies.

    python3 Tools/Build/vessel_model/vessel_colors.py            print the scheme the assets carry now
    python3 Tools/Build/vessel_model/vessel_colors.py --apply    write SCHEME into the three materials
    python3 Tools/Build/vessel_model/vessel_colors.py --check    fail when a material drifted from SCHEME

THE COLOUR LANGUAGE (Garrett, 2026-10-18). Each hue family is named and means one thing; saturation and value
inside a family are free for subtle differences within its use:
    Blue   the NEUTRAL family. Desaturated or valued down to grey belongs to it alone.
    Fire   (red with a little orange) DANGER: bad, avoid, address.
    Lime   CALL TO ACTION only: a categorically good thing to collect or click.
    Ruby, Gold, Jade   the three DOMAINS (teams, the cultures of freedom, commerce and order). Reserved.
A vessel wears: its domain on the Domain slot (the palette's ShipColor1/ShipColor2 for the pilot's team, set by
ThemeManager; never edited here, the UI reads the same colours), a neutral Blue body and Blue glass told apart by
saturation and value, and a subtle Fire at the jet exits (the Engine slot). No Lime anywhere on a hull.

THE SHADER this describes (VesselGraph.shadergraph, unlit): colour = lerp(_Color1, _Color2, facing) x
_ColorMultiplier, then the distance-gated vision band (VesselVisionShading.hlsl, off at close range). `facing` is
the view-space normal's z: 1 where a surface faces the camera, 0 at its silhouette. So _Color1 is the RIM (the
silhouette edge) and _Color2 the FACE. Colours are stored as sRGB and converted to linear by Unity (the project
is linear). The Engine slot's multiplier is 1 at rest and 5 under boost (VesselAnimation.FlareEngine), so the
exit is an ember at rest and a hot danger signal under thrust.
"""
import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
MATERIALS = {
    "body": "Assets/_Graphics/Materials/BlueBaseVesselMaterial.mat",
    # the Squirrel's body slot (Squirrel.prefab overrides [1] with this opaque twin); same colours as "body"
    "body_opaque": "Assets/_Graphics/Materials/BlueBaseOpaqueVesselMaterial.mat",
    "window": "Assets/_Graphics/Materials/ScreenVesselMaterial.mat",
    "engine": "Assets/_Graphics/Materials/EngineVesselMaterial.mat",
}
COLOR_SET = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset"
DOMAINS = ("Jade", "Ruby", "Gold")
ENGINE_BOOST_MULTIPLIER = 5.0   # VesselAnimation.FlareEngine

# THE SCHEME (sRGB, as the material stores it): rim = _Color1 (silhouette edge), face = _Color2 (facing the camera),
# mult = the material's resting _ColorMultiplier. Chosen 2026-10-18 on the comparison sheets (render_cards.py
# --compare); the reasoning is in Docs/VesselModels/COLOR.md.
SCHEME = {
    # Blue, low saturation, very low value: a dark gunmetal hull whose cool steel rim draws the silhouette against
    # the sky, so the domain plates and the glass carry the colour
    "body": {"rim": (0.18, 0.21, 0.33), "face": (0.028, 0.033, 0.065), "mult": 1.0},
    # Blue, saturated and brighter: sapphire glass that reads as glass, an ice glint at its edge (hue 228, clear of Jade)
    "window": {"rim": (0.70, 0.80, 1.00), "face": (0.06, 0.16, 0.62), "mult": 1.0},
    # Fire, low value: an ember at rest; FlareEngine's x5 makes it a hot danger signal under boost
    "engine": {"rim": (0.42, 0.11, 0.04), "face": (0.19, 0.04, 0.02), "mult": 1.0},
}


# The CLASS livery: what the Domain slot wears where no team applies (a class card, a hangar preview, an
# advertisement for the class). Blue family, light and low saturation: plates that read as "your team's colour goes
# here" without claiming Ruby, Gold or Jade for a player of another domain.
CLASS_LIVERY = {"rim": (0.86, 0.90, 1.00), "face": (0.50, 0.56, 0.70), "mult": 1.0}


def srgb_to_linear(c):
    if c <= 0.04045:
        return c / 12.92
    return ((c + 0.055) / 1.055) ** 2.4


def linear_to_srgb(c):
    if c <= 0.0031308:
        return 12.92 * c
    return 1.055 * c ** (1 / 2.4) - 0.055


def _read(path):
    with open(os.path.join(REPO, path), encoding="utf-8") as f:
        return f.read()


def _color(text, key):
    m = re.search(r"- %s: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+)" % re.escape(key), text)
    return tuple(float(x) for x in m.groups()) if m else None


def _float(text, key):
    m = re.search(r"- %s: ([-\d.eE]+)" % re.escape(key), text)
    return float(m.group(1)) if m else None


def material_colors(path):
    t = _read(path)
    return {"rim": _color(t, "_Color1"), "face": _color(t, "_Color2"), "mult": _float(t, "_ColorMultiplier")}


def domain_colors():
    """The palette's per-domain ship colours (ThemeManager writes them to the domain material): rim/face sRGB."""
    t = _read(COLOR_SET)
    out = {}
    for d in DOMAINS + ("Blue",):
        block = re.search(r"  %sColors:\n((?:    .*\n)+)" % d, t)
        if not block:
            continue
        b = block.group(1)
        c1 = re.search(r"ShipColor1: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+)", b)
        c2 = re.search(r"ShipColor2: \{r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+)", b)
        out[d] = {"rim": tuple(float(x) for x in c1.groups()), "face": tuple(float(x) for x in c2.groups()), "mult": 1.0}
    out["Class"] = dict(CLASS_LIVERY)
    return out


def current():
    """The scheme the assets carry now: role -> {rim, face, mult} (sRGB), plus `domains`."""
    out = {role: material_colors(path) for role, path in MATERIALS.items() if role in SCHEME}
    out["domains"] = domain_colors()
    return out


def scheme():
    """The authored scheme with the palette's domains: what the cards render and the materials should carry."""
    out = {role: dict(v) for role, v in SCHEME.items()}
    out["domains"] = domain_colors()
    return out


def linear_role(c, boost=False):
    """(rim_linear, face_linear) for a role, multiplier applied (the shader multiplies in linear)."""
    m = c.get("mult") or 1.0
    if boost:
        m = ENGINE_BOOST_MULTIPLIER
    rim = tuple(srgb_to_linear(max(0.0, x)) * m for x in c["rim"])
    face = tuple(srgb_to_linear(max(0.0, x)) * m for x in c["face"])
    return rim, face


def _fmt(c):
    return "{r: %s, g: %s, b: %s, a: 1}" % tuple(("%.4f" % x).rstrip("0").rstrip(".") if x else "0" for x in c)


def _want(role):
    return SCHEME["body" if role == "body_opaque" else role]


def apply():
    changed = []
    for role, path in MATERIALS.items():
        t = _read(path)
        s = _want(role)
        new = re.sub(r"(    - _Color1: )\{[^}]*\}", lambda m: m.group(1) + _fmt(s["rim"]), t, count=1)
        new = re.sub(r"(    - _Color2: )\{[^}]*\}", lambda m: m.group(1) + _fmt(s["face"]), new, count=1)
        new = re.sub(r"(    - _ColorMultiplier: )[-\d.eE]+", lambda m: m.group(1) + ("%g" % s["mult"]), new, count=1)
        if new != t:
            with open(os.path.join(REPO, path), "w", encoding="utf-8") as f:
                f.write(new)
            changed.append(path)
    return changed


def check():
    fails = []
    for role, path in MATERIALS.items():
        have = material_colors(path)
        want = _want(role)
        for k in ("rim", "face"):
            if not have[k] or max(abs(a - b) for a, b in zip(have[k], want[k])) > 1e-3:
                fails.append("%s %s is %s, the scheme says %s" % (path, k, have[k], want[k]))
        if have["mult"] is None or abs(have["mult"] - want["mult"]) > 1e-3:
            fails.append("%s _ColorMultiplier is %s, the scheme says %s" % (path, have["mult"], want["mult"]))
    return fails


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    if a.apply:
        for p in apply():
            print("wrote " + p)
        return 0
    if a.check:
        fails = check()
        for f in fails:
            print("DRIFT: " + f)
        print("OK: the vessel materials carry the scheme" if not fails else "%d drift(s)" % len(fails))
        return 1 if fails else 0
    import json
    print(json.dumps(current(), indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
