#!/usr/bin/env python3
"""author_elemental_crystal_colors - each free elemental crystal wears its own shade of lime.

    python3 Tools/Build/author_elemental_crystal_colors.py              # write the materials and rewire the prefabs
    python3 Tools/Build/author_elemental_crystal_colors.py --check      # exit 1 on any drift
    python3 Tools/Build/author_elemental_crystal_colors.py --self-test  # prove the check fires

Garrett, 2026-10-27: "right now mass crystals and omnicrystals are the hardest to distinguish. lets shift the colors on
each element to match the color shifts we used for the wrasse's 2d elements. this should cause the mass and omni crystals
to have a distinguishing color difference."

THE COLOURS are the Wrasse's four lasso shades (Tools/Build/kite_mote_colors.py derives them inside lime's boundary
against Jade, Ruby, Gold, Blue and Fire, with distinct hue, saturation and value) and live in the palette,
OriginalColorSetSO.asset `EnvironmentColors.ElementalCrystalColors` (Charge, Mass, Space, Time; index = Element - 1).
This script reads them from there and never derives them itself.

WHERE EACH CRYSTAL GETS ITS COLOUR in the free (lime, anyone collects) state:
  Charge  ChargeCrystalMaterial (_BrightCrystalColor/_DullCrystalColor): the runtime tint paints it, Crystal.cs
          TryResolveTintColors, CTA pair -> the element's hue (Color.WithHueOf) -> ElementalCrystalDimming. Same for
          ActiveCrystalSpace's CrystalGraph material. Nothing to author here.
  Mass    MassDepthPrisms.mat (OmniDepthPrismShader, _BrightColor/_DarkColor - the tint does not reach that pair).
          author_mass_crystal_look.py authors it and calls recolor() below, so its --check holds the Mass shade.
  Space   CrystalSpace.prefab wore LimeCrystalFresnelMaterial, shared with Time and with the omni body's donor.
  Time    CrystalTime.prefab, the same. This script gives each its own clone, SpaceCrystalFresnelMaterial and
          TimeCrystalFresnelMaterial, and points the prefab's free (default) material at it.

THE RULE is the runtime tint's, in one place for both: the element's shade at the donor colour's brightness (its
brightest channel), so a shade whose brightest channel is 1 lands on the donor's peak and the darker shades stay
darker by the same ratio. Only the BRIGHT colour moves on the Fresnel materials: their dark end is near-black.
The omni (OmniCrystalBody, OmniDepthPrisms) keeps the palette's lime, and the embedded (blue-white) materials are
untouched: colour still says WHO can collect (Docs/PALETTE.md), and each lime shade says WHICH element.
"""
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import kite_mote_colors as KITE  # noqa: E402  (the palette's path and its crystal-colour reader)

ELEMENTS = KITE.ELEMENTS   # ("Charge", "Mass", "Space", "Time"), index = Element - 1
MAT_DIR = "Assets/_Graphics/Materials/CrystalMaterials"
DONOR = f"{MAT_DIR}/LimeCrystalFresnelMaterial.mat"
DONOR_NAME = "LimeCrystalFresnelMaterial"
DONOR_GUID = "3fbc5051c8a40814186ae69a0cba15ff"
OWN = {   # element -> (its own free material, the prefab whose free material it is)
    "Space": ("SpaceCrystalFresnelMaterial", "Assets/_Prefabs/Environment/CrystalSpace.prefab"),
    "Time": ("TimeCrystalFresnelMaterial", "Assets/_Prefabs/Environment/CrystalTime.prefab"),
}
NUM = KITE.NUM
COLOR_ROW = r"^    - %s: \{r: " + NUM + r", g: " + NUM + r", b: " + NUM + r", a: " + NUM + r"\}$"


def read(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8") as fh:
        return fh.read()


def element_color(element):
    """The palette's shade for one element (sRGB, no alpha)."""
    colors = KITE.palette_colors(KITE.read(KITE.PALETTE))
    if colors is None:
        raise SystemExit("author_elemental_crystal_colors: the palette has no ElementalCrystalColors "
                         "(run Tools/Build/kite_mote_colors.py)")
    return colors[ELEMENTS.index(element)]


def fmt(v):
    return KITE.fmt(round(v, 7))


def recolor(text, element, prop="_BrightColor"):
    """`text` (a material) with its `prop` colour moved to `element`'s shade at that colour's own brightest channel."""
    hue = element_color(element)
    rx = re.compile(COLOR_ROW % re.escape(prop), re.M)
    m = rx.search(text)
    if not m:
        raise SystemExit("author_elemental_crystal_colors: no %s colour row to recolour" % prop)
    r, g, b, a = (float(x) for x in m.groups())
    peak = max(r, g, b)
    row = "    - %s: {r: %s, g: %s, b: %s, a: %s}" % ((prop,) + tuple(fmt(v * peak) for v in hue) + (fmt(a),))
    return text[:m.start()] + row + text[m.end():]


def mat_guid(name):
    return hashlib.md5(f"CosmicShore/ElementalCrystal/{name}.mat".encode()).hexdigest()


def material_text(element):
    name = OWN[element][0]
    t = read(DONOR)
    t, n = re.subn(rf"^  m_Name: {DONOR_NAME}$", f"  m_Name: {name}", t, count=1, flags=re.M)
    if n != 1:
        raise SystemExit("author_elemental_crystal_colors: the donor material's name moved")
    return recolor(t, element)


def material_meta(element):
    meta = read(DONOR + ".meta")
    if f"guid: {DONOR_GUID}" not in meta:
        raise SystemExit("author_elemental_crystal_colors: the donor material's guid moved")
    return meta.replace(DONOR_GUID, mat_guid(OWN[element][0]))


def rewire(prefab_text, element):
    """The prefab's free (default) material is the element's own; its embedded and exploding materials stay."""
    own = mat_guid(OWN[element][0])
    t, n = re.subn(r"(^    defaultMaterial: \{fileID: 2100000, guid: )(%s|%s)(, type: 2\})" % (DONOR_GUID, own),
                   lambda m: m.group(1) + own + m.group(3), prefab_text, flags=re.M)
    if n != 1:
        raise SystemExit("author_elemental_crystal_colors: %s has %d free-material rows, expected 1" % (element, n))
    return t


def outputs():
    """Every file this script owns, path -> the text it should hold."""
    out = {}
    for element, (name, prefab) in OWN.items():
        out[f"{MAT_DIR}/{name}.mat"] = material_text(element)
        out[f"{MAT_DIR}/{name}.mat.meta"] = material_meta(element)
        out[prefab] = rewire(read(prefab), element)
    return out


def shade_errors(text, element):
    """Where a material's bright colour is not the element's shade (the hue check, independent of the writer)."""
    m = re.search(COLOR_ROW % "_BrightColor", text, re.M)
    if not m:
        return ["%s material has no _BrightColor" % element]
    have = [float(x) for x in m.groups()[:3]]
    peak = max(have)
    want = element_color(element)
    if peak <= 0 or max(abs(h / peak - w / max(want)) for h, w in zip(have, want)) > 0.002:
        return ["%s material's bright colour %s is not the palette's %s shade %s"
                % (element, KITE.hexof(have), element, KITE.hexof(want))]
    return []


def check(files):
    errs = []
    for rel, want in outputs().items():
        if files.get(rel) != want:
            errs.append("%s drifted from this script (run Tools/Build/author_elemental_crystal_colors.py)" % rel)
    for element, (name, _) in OWN.items():
        errs += shade_errors(files.get(f"{MAT_DIR}/{name}.mat", ""), element)
    import author_mass_crystal_look as MASS   # the Mass shade rides that script's material
    errs += shade_errors(files.get(MASS.mat_rel(MASS.MASS_DEPTH_NAME), ""), "Mass")
    omni = files.get(f"{MAT_DIR}/OmniCrystalBody.mat", "")
    mass = files.get(MASS.mat_rel(MASS.MASS_DEPTH_NAME), "")
    bright = lambda t: re.search(COLOR_ROW % "_BrightColor", t, re.M)   # noqa: E731
    if bright(omni) and bright(mass) and bright(omni).groups() == bright(mass).groups():
        errs.append("the Mass crystal wears the omni's colour again")
    return errs


def on_disk():
    import author_mass_crystal_look as MASS
    paths = list(outputs()) + [MASS.mat_rel(MASS.MASS_DEPTH_NAME), f"{MAT_DIR}/OmniCrystalBody.mat"]
    files = {}
    for rel in paths:
        p = os.path.join(REPO, rel)
        if os.path.exists(p):
            files[rel] = read(rel)
    return files


def self_test():
    bad = []
    files = on_disk()
    files.update(outputs())
    if check(files):
        bad.append("freshly written outputs fail the check: %s" % check(files))
    import author_mass_crystal_look as MASS
    mass_rel = MASS.mat_rel(MASS.MASS_DEPTH_NAME)
    tampered = dict(files)
    tampered[mass_rel] = files[f"{MAT_DIR}/OmniCrystalBody.mat"].replace("OmniCrystalBody", MASS.MASS_DEPTH_NAME)
    if not any("omni's colour" in e for e in check(tampered)):
        bad.append("a Mass crystal in the omni's colour passes")
    for element, (name, prefab) in OWN.items():
        tampered = dict(files)
        tampered[prefab] = files[prefab].replace(mat_guid(name), DONOR_GUID)
        if not check(tampered):
            bad.append("%s back on the shared lime material passes" % element)
        tampered = dict(files)
        tampered[f"{MAT_DIR}/{name}.mat"] = read(DONOR).replace(DONOR_NAME, name)
        if not any("not the palette's" in e for e in check(tampered)):
            bad.append("%s's material in the omni's lime passes the shade check" % element)
    if rewire(rewire(read(OWN["Space"][1]), "Space"), "Space") != rewire(read(OWN["Space"][1]), "Space"):
        bad.append("rewiring is not idempotent")
    print("author_elemental_crystal_colors self-test: " + ("ok" if not bad else "FAILED"))
    for b in bad:
        print("  -", b)
    return 1 if bad else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" in argv:
        errs = check(on_disk())
        for e in errs:
            print("FAIL:", e)
        print("author_elemental_crystal_colors --check:", "ok" if not errs else "%d problem(s)" % len(errs))
        return 1 if errs else 0
    for rel, text in outputs().items():
        p = os.path.join(REPO, rel)
        old = read(rel) if os.path.exists(p) else None
        if old != text:
            with open(p, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(text)
            print("wrote", rel)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
