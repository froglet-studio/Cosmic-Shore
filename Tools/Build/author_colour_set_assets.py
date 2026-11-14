#!/usr/bin/env python3
"""Writes each colour set of Docs/VesselModels/colour_sets.json as an SO_ColorSet palette asset Unity can load.

A set (Tools/Build/colour_sets.py) gives every domain two pigments, a rim and a face. A palette asset has ~20 colours per
domain, each tuned in PhoticColorSetSO for its own job: HDR rims sized for bloom, near-black crystal bodies, whitened
shielded tiers. So the writer keeps everything the original tuned and takes only the HUE from the set:

  * every field wears one pigment of its domain (FIELD_ROLE: the prism rims and the bright, signal-like fields wear the
    rim; the prism faces and the dark fields wear the face);
  * a rim field keeps the original field's brightness (its brightest channel, HDR where the original is), so bloom
    is unchanged; a face field takes its face pigment's own lightness (the pigment at its mid L), times the original
    field's brightness over the original plain face's, so the tiers keep their steps. The face is most of a prism's
    surface, so the set, not the original, decides how bright the body is (2026-11-03: Dart Frog's faces at the
    original's brightness made each prism read as its face hue);
  * a set may name `rim_brightness` (default 1): every rim field's brightness is the original's times it, so a light
    set (Nacre, 2026-11-08) keeps its rims at paper white instead of the original's HDR bloom, the tiers keeping their
    steps. Danger and the CTA pair are signals and keep the original's brightness;
  * its alpha is the original's;
  * its whiteness is the original's whiteness beyond its domain's plain tier (InsideBlockColor for rim fields,
    OutsideBlockColor for face fields), so a shielded rim is as much paler than a plain rim as it was before.

Scaling linear RGB is lighting, which never moves a colour out of its pigment's chroma direction (rule 2 of the sets),
so an unwhitened field sits on its pigment's centre ray at any brightness. Whitening moves it toward grey; --check
fails if that carries any field into ANOTHER domain's pigment.

Fire and Lime are not DomainColorSets (colour_sets.json, prism_mapping):
  * Fire: EnvironmentColors.Danger wears the Fire rim. The Fire face has no field yet.
  * Lime: BrightCTA and DarkCTA both wear the Lime RIM, so free pickups still read lime (DarkCTA is ~93% of a crystal;
    the face there would make pickups read as the face). The Lime face has no field yet.
  * ElementalCrystalColors, SkyColor, LightColor and DarkColor are copied from the original unchanged.

The [ColorUsage(true, true)] fields hold LINEAR floats; UIAccentColor and ElementalCrystalColors hold sRGB.

TracedColors (every colour the game draws outside these blocks: the sky, materials, particles, the UI's own colours,
configs and code) come from Docs/VesselModels/colour_trace.json: each set's value for each id is computed by
Tools/Build/colour_trace.py (repaint, Docs/VesselModels/COLOR_TRACE.md), in the storage of the place it is drawn
(material and UI colours sRGB, HDR kept). PhoticColorSetSO's block is written by colour_trace.py --write-photic.

    python3 Tools/Build/author_colour_set_assets.py               # write every set's asset (keeps an existing guid)
    python3 Tools/Build/author_colour_set_assets.py --check       # exit 1 on drift or a field in another domain
    python3 Tools/Build/author_colour_set_assets.py --report      # every field: colour and which pigment holds it
    python3 Tools/Build/author_colour_set_assets.py --self-test   # proves --check names each failure it exists for
"""
import math
import os
import re
import sys
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import colour_sets as cs  # noqa: E402

PALETTES = os.path.join(REPO, "Assets", "_SO_Assets", "Color Palettes")
PHOTIC = os.path.join(PALETTES, "PhoticColorSetSO.asset")
SCRIPT_GUID = "a1c7edc523dbbef4bbe8c3e97ba840c7"   # SO_ColorSet.cs
DOMAIN_SETS = (("JadeColors", "jade"), ("RubyColors", "ruby"), ("GoldColors", "gold"), ("BlueColors", "blue"))
SRGB_FIELDS = {"UIAccentColor"}                    # not [ColorUsage(true, true)]: stored as sRGB
FIELD_ROLE = {
    "ShipColor1": "face", "ShipColor2": "rim",
    "OutsideBlockColor": "face", "ShieldedOutsideBlockColor": "face", "SuperShieldedOutsideBlockColor": "face",
    "InsideBlockColor": "rim", "ShieldedInsideBlockColor": "rim", "SuperShieldedInsideBlockColor": "rim",
    "AOETextureColor": "rim", "AOEFresnelColor": "face", "AOEConicColor": "face", "AOEConicEdgeColor": "rim",
    "SpikeLightColor": "rim", "SpikeDarkColor": "face", "SkimmerColor": "rim",
    "DullCrystalColor": "face", "BrightCrystalColor": "rim",
    "TrailHighlightColor": "rim", "TrailCoreColor": "face", "UIAccentColor": "rim",
}
PLAIN = {"rim": "InsideBlockColor", "face": "OutsideBlockColor"}
ENV_PAINTED = {"Danger": ("fire", "rim"), "BrightCTA": ("lime", "rim"), "DarkCTA": ("lime", "rim")}
COLOR_RE = re.compile(r"\{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}")


def asset_name(key):
    return "".join(w.capitalize() for w in key.split("_")) + "ColorSetSO"


def asset_path(key):
    return os.path.join(PALETTES, asset_name(key) + ".asset")


# ----------------------------------------------------------------------------- reading the original
def read_original(path=PHOTIC):
    with open(path, encoding="utf-8") as fh:
        return parse(fh.read())


def parse(text):
    """{block: {field: (r, g, b, a) or raw text}}, ElementalCrystalColors as its raw lines."""
    text = re.sub(r",\r?\n\s+", ", ", text)   # Unity folds long colour lines when it saves
    blocks, block = {}, None
    for line in text.splitlines():
        m = re.match(r"^  (\w+):\s*(\[\])?$", line)
        if m:
            block = blocks.setdefault(m.group(1), {})
            if m.group(1) == "TracedColors":
                block["_entries"] = []
            continue
        if block is not None and "_entries" in block:
            m = re.match(r"^  - Id: (.*)$", line)
            if m:
                block["_entries"].append([m.group(1).strip().strip("'"), None, ""])
                continue
            m = re.match(r"^    (Color|Note): (.*)$", line)
            if m and block["_entries"]:
                c = COLOR_RE.fullmatch(m.group(2))
                if m.group(1) == "Color" and c:
                    block["_entries"][-1][1] = tuple(float(v) for v in c.groups())
                else:
                    block["_entries"][-1][2] = m.group(2).strip()
            continue
        m = re.match(r"^    (\w+): (.*)$", line)
        if m and block is not None:
            c = COLOR_RE.fullmatch(m.group(2))
            block[m.group(1)] = tuple(float(v) for v in c.groups()) if c else m.group(2)
        m = re.match(r"^    - (\{.*\})$", line)
        if m and block is not None:
            block.setdefault("ElementalCrystalColors", []).append(m.group(1))
    return blocks


# ----------------------------------------------------------------------------- painting
def pigment_linear(p):
    """The pigment's centre ray in linear RGB, brightest channel 1 (the paint with no light on it named)."""
    lin = cs.linear_of(*cs.at(p, cs.mid(p)))
    peak = max(lin)
    return tuple(max(0.0, v) / peak for v in lin)


def whiteness(c):
    peak = max(c[:3])
    return min(c[:3]) / peak if peak > 1e-6 else 0.0


def paint(orig, plain, p, srgb=False):
    """`orig` re-hued to pigment `p`: its brightness, its alpha, and its whiteness beyond the plain tier `plain`."""
    peak = max(orig[:3])
    if peak <= 1e-6:
        return (0.0, 0.0, 0.0, orig[3])
    hue = pigment_linear(p)
    if srgb:
        hue = tuple(cs.to_gamma(v) for v in hue)
    w0 = whiteness(plain) if plain else 0.0
    s = max(0.0, (whiteness(orig) - w0) / (1 - w0)) if w0 < 1 else 0.0   # the share of the way to white
    return tuple(round(peak * (h + (1 - h) * s), 6) for h in hue) + (orig[3],)


def palette(s, orig):
    """{block: {field: colour}} for set `s`."""
    out = {}
    for block, dom in DOMAIN_SETS:
        src, fields = orig[block], {}
        face = s["domains"][dom]["face"]
        face_peak = max(cs.linear_of(*cs.at(face, cs.mid(face))))
        rim_k = s.get("rim_brightness", 1.0)
        for field, role in FIELD_ROLE.items():
            p = s["domains"][dom][role]
            c = paint(src[field], src[PLAIN[role]], p, field in SRGB_FIELDS)
            if role == "face" and field not in SRGB_FIELDS and max(c[:3]) > 1e-6:
                k = face_peak / max(src[PLAIN["face"]][:3])
                c = tuple(round(v * k, 6) for v in c[:3]) + (c[3],)
            elif role == "rim" and field not in SRGB_FIELDS and rim_k != 1.0:
                c = tuple(round(v * rim_k, 6) for v in c[:3]) + (c[3],)
            fields[field] = c
        out[block] = fields
    env = dict(orig["EnvironmentColors"])
    for field, (dom, role) in ENV_PAINTED.items():
        env[field] = paint(env[field], env[field], s["domains"][dom][role])   # its whiteness is its hue: none added
    out["EnvironmentColors"] = env
    return out


# ----------------------------------------------------------------------------- writing
def fmt(v):
    return "%g" % v if v == int(v) else ("%.6f" % v).rstrip("0")


def colour_yaml(c):
    return "{r: %s, g: %s, b: %s, a: %s}" % tuple(fmt(v) for v in c)


def render(key, s, orig):
    pal = palette(s, orig)
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % SCRIPT_GUID, "  m_Name: " + asset_name(key),
             "  m_EditorClassIdentifier: "]
    for block, _ in DOMAIN_SETS:
        lines.append("  %s:" % block)
        lines += ["    %s: %s" % (f, colour_yaml(c)) for f, c in pal[block].items()]
    lines.append("  EnvironmentColors:")
    for f, v in pal["EnvironmentColors"].items():
        if f == "ElementalCrystalColors":
            lines.append("    ElementalCrystalColors:")
            lines += ["    - " + e for e in v]
        else:
            lines.append("    %s: %s" % (f, colour_yaml(v) if isinstance(v, tuple) else v))
    lines += traced_lines(key, s, pal)
    return "\n".join(lines) + "\n"


_PHOTIC_REF = None


def traced_lines(key, s, pal):
    """The set's TracedColors block: its value for every id of colour_trace.json (empty until the table exists). A
    shipped set's values come from colour_trace.all_values, which keeps every set's values apart from every other's
    (ColorTrace names a colour on screen by its value, from whichever set painted it); a set that is not in
    colour_sets.json (a self-test's) gets its own values."""
    global _PHOTIC_REF
    import colour_trace as ct
    table = ct.load_table()
    if not table:
        return ["  TracedColors: []"]
    shipped = ct.all_values()
    if key in shipped and s == ct.set_defs().get(key):
        return ct.traced_yaml(shipped[key])
    if _PHOTIC_REF is None:
        _PHOTIC_REF = cs.photic_reference()
    return ct.traced_yaml(ct.set_values(key, s, table, _PHOTIC_REF, pal))


def meta_text(guid):
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


def write(defs, orig):
    for key, s in defs["sets"].items():
        path = asset_path(key)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(render(key, s, orig))
        if not os.path.exists(path + ".meta"):
            with open(path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
                fh.write(meta_text(uuid.uuid4().hex))
        print("wrote", os.path.relpath(path, REPO))


# ----------------------------------------------------------------------------- checking
def holder(s, c, srgb):
    """(domain, role) pigments whose chroma direction holds colour c at any lightness (the set's lighting rule)."""
    lin = tuple(cs.to_linear(v) for v in c[:3]) if srgb else c[:3]
    if max(lin) <= 1e-6:
        return None
    L, a, b = cs.oklab_linear(*lin)
    out = []
    for d, r, p in cs.pigments(s):
        ca, cb = cs.direction(p)
        if math.hypot(a / L - ca, b / L - cb) <= p["rho"]:
            out.append((d, r))
    return out


def same(a, b, tol=1e-5):
    """Equal as Unity reads them (values, not text: Unity re-folds lines and re-prints floats when it saves)."""
    if a.keys() != b.keys():
        return False
    for block in a:
        if a[block].keys() != b[block].keys():
            return False
        for f, v in a[block].items():
            w = b[block][f]
            if f == "_entries":
                if len(v) != len(w) or any(x[0] != y[0] or x[2] != y[2] or x[1] is None or y[1] is None or
                                           any(abs(p - q) > tol for p, q in zip(x[1], y[1])) for x, y in zip(v, w)):
                    return False
                continue
            if isinstance(v, tuple) and isinstance(w, tuple):
                if any(abs(x - y) > tol for x, y in zip(v, w)):
                    return False
            elif str(v).strip() != str(w).strip():
                return False
    return True


def field_problems(key, s, pal):
    found = []
    rows = [(block, dom, f, c, f in SRGB_FIELDS) for block, dom in DOMAIN_SETS for f, c in pal[block].items()]
    rows += [("EnvironmentColors", dom, f, pal["EnvironmentColors"][f], False) for f, (dom, _) in ENV_PAINTED.items()]
    for block, dom, f, c, srgb in rows:
        h = holder(s, c, srgb)
        foreign = [d for d, _ in (h or []) if d != dom]
        if foreign:
            found.append("%s %s.%s is in %s's pigment, not %s's" % (key, block, f, "/".join(foreign), dom))
    return found


def problems(defs, orig):
    found = []
    for key, s in defs["sets"].items():
        path = asset_path(key)
        want = render(key, s, orig)
        if not os.path.exists(path):
            found.append("%s: %s is missing (run the writer)" % (key, os.path.relpath(path, REPO)))
        else:
            with open(path, encoding="utf-8") as fh:
                if not same(parse(fh.read()), parse(want)):
                    found.append("%s: %s drifted from colour_sets.json (run the writer)" % (key, os.path.relpath(path, REPO)))
        if not os.path.exists(path + ".meta"):
            found.append("%s: %s.meta is missing" % (key, os.path.relpath(path, REPO)))
        found += field_problems(key, s, palette(s, orig))
    # An example is never written, but it must be ready to be: it keeps the writer's field rule too, so taking one
    # into the game is a move in colour_sets.json and never a re-derivation (2026-11-11: Dusk passed every rule as an
    # example and failed this one the day it was taken into the game).
    for key, s in defs.get("examples", {}).items():
        found += ["example " + p for p in field_problems(key, s, palette(s, orig))]
    return found


def report(defs, orig):
    for key, s in defs["sets"].items():
        pal = palette(s, orig)
        print("%s -> %s" % (key, os.path.relpath(asset_path(key), REPO)))
        for block, dom in DOMAIN_SETS + (("EnvironmentColors", None),):
            print("  " + block)
            for f, c in pal[block].items():
                if not isinstance(c, tuple) or (dom is None and f not in ENV_PAINTED):
                    continue
                srgb = f in SRGB_FIELDS
                h = holder(s, c, srgb)
                shown = c[:3] if srgb else tuple(cs.to_gamma(v / max(1.0, max(c[:3]))) for v in c[:3])
                where = "black" if h is None else (", ".join("%s %s" % x for x in h) or "unclaimed (whitened)")
                print("    %-31s %s  peak %5.3f  %s" % (f, cs.hexof(shown), max(c[:3]), where))


def self_test():
    defs, orig = cs.load(), read_original()
    key = next(iter(defs["sets"]))
    s = defs["sets"][key]
    caught = 0

    def planted(name, found):
        nonlocal caught
        ok = bool(found)
        caught += ok
        print("  %-48s %s" % (name, "caught" if ok else "MISSED"))

    pal = palette(s, orig)
    pal["JadeColors"]["InsideBlockColor"] = paint(orig["JadeColors"]["InsideBlockColor"], None,
                                                  s["domains"]["ruby"]["rim"])
    planted("a jade field painted with ruby's rim", field_problems(key, s, pal))
    pal = palette(s, orig)
    pal["EnvironmentColors"]["Danger"] = paint(orig["EnvironmentColors"]["Danger"], None, s["domains"]["gold"]["rim"])
    planted("danger painted with gold's rim", field_problems(key, s, pal))
    moved = dict(s, domains=dict(s["domains"], jade=dict(s["domains"]["jade"],
                                                         rim=dict(s["domains"]["jade"]["rim"], hue=180))))
    planted("the json moves, the asset does not", [1] if render(key, moved, orig) != render(key, s, orig) else [])
    if "dusk" in defs["sets"]:   # Dusk as it was the day it was promoted: its Lime face took a whitened Ruby field
        old = defs["sets"]["dusk"]
        lime = dict(old["domains"]["lime"], face=dict(old["domains"]["lime"]["face"], hue=316, sat=0.217))
        planted("an example a field lands in the wrong pigment of",
                [p for p in problems(dict(defs, sets={}, examples={"dusk": dict(old, domains=dict(old["domains"], lime=lime))}),
                                     orig) if p.startswith("example ")])
    else:   # never a silent pass: a case that cannot be planted any more is a missed case until it is replanted
        planted("an example a field lands in the wrong pigment of (no Dusk: replant this case)", [])
    print("self-test: %d/4 caught" % caught)
    return caught == 4


def main(argv):
    defs, orig = cs.load(), read_original()
    if "--self-test" in argv:
        return 0 if self_test() else 1
    if "--report" in argv:
        report(defs, orig)
        return 0
    if "--check" in argv:
        found = problems(defs, orig)
        for f in found:
            print("FAIL", f)
        print("ok" if not found else "%d problem(s)" % len(found))
        return 1 if found else 0
    write(defs, orig)
    found = problems(defs, orig)
    for f in found:
        print("FAIL", f)
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
