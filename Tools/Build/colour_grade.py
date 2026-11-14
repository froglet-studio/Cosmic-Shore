#!/usr/bin/env python3
"""Colour grade: every texture pixel the game draws follows the worn colour set, as every authored colour does.

Garrett, 2026-11-10 (LOCKED): "all colors come from a color set. If that rule is broken then it is a sign of
incompleteness or a bug." And on textures: "if rerendering textures per set is the best you can do, but perhaps you can
get clever." The trace (colour_trace.py) repaints colours; a texture is millions of them. So the colour trace's own
rule (colour_trace.repaint: a colour of family F wears the set's F rim at its brightness and whiteness, a UI ground
takes the set's Jade face hue, a grey keeps) is applied to every texel through a small 3D table per set, the grade:

* SMOOTH. A single authored colour is classified once; a picture has gradients across family borders, the grey
  threshold and the UI ground's edge, so the grade blends there: across the band between two families (the families'
  signed distances, colour_domains.json's margin), from the grey threshold (OKLab chroma NEUTRAL to 3 x NEUTRAL) and
  over the UI ground's edge. Elsewhere it IS the trace's rule.
* PER SET AND ROLE. A 33^3 table for the UI (canvas art, sprites, card art, portraits, the codex) and one for the world
  (the static sky), per colour set; Photic, the set the game was authored in, has none: its grade is the identity, and
  the shader keyword is off (no cost).
* WHERE. Assets/Resources/ColorGrades/<SetAsset>_ui.bytes and _world.bytes: a header (b"CSGR", version, size) and the
  table's RGB bytes, r fastest then g then b (Texture3D order), each the sRGB encoding of the graded colour
  at the sRGB grid point, uncompressed (a build compresses Resources itself, and a player needs no inflater). ColorGrade.cs
  builds the Texture3D when a set is worn; the shaders' ColorGrade.cginc samples
  it. Every UI Graphic drawn with the default UI material (CosmicShore/UI/GradedDefault), CardDrift, HullLit,
  SilhouetteHolo, ElementRim and the static sky go through it.
* WHAT IS NOT GRADED, each for a reason (EXEMPT): the sign-in providers' own marks (their brand rules fix them; the
  views that show them call ColorGrade.KeepAsAuthored), the app icon and splash (the platform draws them before any
  script runs), TextMesh Pro's emoji atlas (no text in the game draws one).

    python3 Tools/Build/colour_grade.py            # write the tables and the texture record
    python3 Tools/Build/colour_grade.py --check    # the gate: the tables and the record match the sets and textures
    python3 Tools/Build/colour_grade.py --report   # every coloured texture and how it follows the set
    python3 Tools/Build/colour_grade.py --self-test
"""
import contextlib
import hashlib
import io
import json
import math
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import colour_trace as ct  # noqa: E402
import colour_sets as cs  # noqa: E402
import colour_domains as cd  # noqa: E402
import kite_mote_colors as kite  # noqa: E402

REPO = ct.REPO
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "ColorGrades")
RECORD = os.path.join(REPO, "Docs", "VesselModels", "colour_grade.json")
N = 33
MAGIC, VERSION = b"CSGR", 1
ROLES = ("ui", "world")
TEX_EXT = (".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".exr", ".hdr")
COLOURED_FRACTION = 0.01   # a texture whose alpha-weighted texels at chroma >= NEUTRAL pass this has colour to grade
# Textures that keep their own colours, each with its reason (path prefix -> why).
EXEMPT = {
    "Assets/_Graphics/UI/Brands/": "a sign-in provider's own mark: its brand rules fix its colours (the views that show "
                                   "it call ColorGrade.KeepAsAuthored)",
    "Assets/_Graphics/Icons/App Icons/": "the app icon and splash: the platform draws them before any script runs",
    "Assets/Unity Assests/TextMesh Pro/Sprites/EmojiOne.png": "TextMesh Pro's default emoji atlas: no text in the game "
                                                             "draws a <sprite> from it",
}
# The world textures the grade reaches, by the shader that draws them (every UI texture is graded: the default UI
# material and the repo's UI shaders).
WORLD_GRADED = {"Assets/_Graphics/Skyboxes/StaticHyperSeaSky.png": "CosmicShore/StaticSkyPanorama"}


# ----------------------------------------------------------------------------- the grade
def smooth(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3.0 - 2.0 * t)


def family_weights(rgb):
    """{family: weight}: 1 inside a family's cell, a linear blend across the band between two (colour_domains.json's
    cells and margin), normalised."""
    truth = ct.truth()
    cells, margin = truth["cells"], truth["margin"]
    x = kite.oklab(rgb)
    score = {f: sum(n * v for n, v in zip(cells[f]["n"], x)) + cells[f]["c"] for f in cd.FAMILIES}
    w = {}
    for f in cd.FAMILIES:
        d = min((score[f] - score[g]) / math.hypot(*[a - b for a, b in zip(cells[f]["n"], cells[g]["n"])])
                for g in cd.FAMILIES if g != f)
        v = min(1.0, max(0.0, 0.5 + d / margin))
        if v > 0:
            w[f] = v
    total = sum(w.values())
    return {f: v / total for f, v in w.items()}


def to_srgb(lab):
    lin = cs.linear_of(*lab)
    return tuple(min(1.0, max(0.0, cs.to_gamma(max(0.0, v)))) for v in lin)


def mix(p, q, t):
    a, b = cs.oklab(tuple(p)), cs.oklab(tuple(q))
    return to_srgb(tuple(x + (y - x) * t for x, y in zip(a, b)))


def paint_as(role, rgb, fam, s, photic):
    """colour_trace.repaint for a colour read as family `fam`, with the UI ground's edge blended."""
    out = ct.paint(rgb, photic["domains"][fam]["rim"], s["domains"][fam]["rim"])
    if role == "ui" and fam == "blue":
        L, a, b = cs.oklab(rgb)
        C = math.hypot(a, b)
        g = (1.0 - smooth(0.07, 0.13, C)) * (1.0 - smooth(0.64, 0.76, L))
        if g > 0:
            out = mix(out, ct.tint(rgb, ct.jade_face_screen(s), C), g)
    return out


def grade(role, rgb, s, photic):
    """Set `s`'s colour for the sRGB colour `rgb` in `role`: the trace's rule, made smooth for pictures."""
    rgb = tuple(rgb)
    if max(rgb) <= 0:
        return rgb
    L, a, b = cs.oklab(rgb)
    t = smooth(ct.NEUTRAL, 3 * ct.NEUTRAL, math.hypot(a, b))
    if t <= 0:
        return rgb
    acc = [0.0, 0.0, 0.0]
    for f, wf in family_weights(rgb).items():
        o = cs.oklab(tuple(paint_as(role, rgb, f, s, photic)))
        for i in range(3):
            acc[i] += wf * o[i]
    return to_srgb(tuple(x0 + (y - x0) * t for y, x0 in zip(acc, (L, a, b))))


def table(role, s, photic, n=N):
    """The grade on the n^3 sRGB grid, r fastest: bytes of R, G, B."""
    out = bytearray()
    for bi in range(n):
        for gi in range(n):
            for ri in range(n):
                c = grade(role, (ri / (n - 1), gi / (n - 1), bi / (n - 1)), s, photic)
                out += bytes(max(0, min(255, int(round(v * 255)))) for v in c)
    return bytes(out)


def encode(raw, n=N):
    return MAGIC + struct.pack("<BB", VERSION, n) + raw


def decode(blob):
    if blob[:4] != MAGIC or blob[4] != VERSION:
        raise ValueError("not a colour grade table")
    n = blob[5]
    raw = bytes(blob[6:])
    if len(raw) != n * n * n * 3:
        raise ValueError("a colour grade table of the wrong size")
    return n, raw


# ----------------------------------------------------------------------------- the sets
def graded_sets():
    """[(set key, its asset's name)] for every shipped set but Photic, whose grade is the identity."""
    import author_colour_set_assets as writer
    return [(k, writer.asset_name(k)) for k in ct.set_defs()]


def table_path(asset_name, role):
    return os.path.join(OUT_DIR, "%s_%s.bytes" % (asset_name, role))


TEXT_META = ("fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n"
             "  assetBundleName: \n  assetBundleVariant: \n")
FOLDER_META = ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def stable_guid(name):
    return hashlib.md5(("colour-grade:" + name).encode()).hexdigest()


def write_meta(path, template):
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
            fh.write(template % stable_guid(ct.rel(path)))


def tables():
    """{path: bytes} every table the shipped sets need."""
    defs, photic = ct.set_defs(), cs.photic_reference()
    return {table_path(name, role): encode(table(role, defs[key], photic))
            for key, name in graded_sets() for role in ROLES}


# ----------------------------------------------------------------------------- the textures
_OKLAB = None


def coloured_fraction(path):
    """The alpha-weighted share of a texture's texels whose OKLab chroma is at least NEUTRAL (where the grade acts)."""
    import numpy as np
    from PIL import Image
    im = Image.open(path)
    im.draft("RGB", (128, 128))
    im = im.convert("RGBA")
    im.thumbnail((96, 96))
    a = np.asarray(im, dtype=np.float64) / 255.0
    rgb, alpha = a[..., :3].reshape(-1, 3), a[..., 3].reshape(-1)
    lin = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    m1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929], [0.2119034982, 0.6806995451, 0.1073969566],
                   [0.0883024619, 0.2817188376, 0.6299787005]])
    m2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468], [1.9779984951, -2.4285922050, 0.4505937099],
                   [0.0259040371, 0.7827717662, -0.8086757660]])
    lab = np.cbrt(lin @ m1.T) @ m2.T
    chroma = np.hypot(lab[:, 1], lab[:, 2])
    total = alpha.sum()
    return round(float((alpha * (chroma >= ct.NEUTRAL)).sum() / total), 4) if total > 0 else 0.0


def texture_kind(path):
    """sprite (drawn on a canvas) or texture (a material's or a RawImage's), from the importer."""
    meta = ct.read(os.path.join(REPO, path + ".meta"))
    m = re.search(r"textureType: (-?\d+)", meta)
    return "sprite" if m and m.group(1) == "8" else "texture"


def how_graded(path, kind):
    for prefix, why in EXEMPT.items():
        if path.startswith(prefix):
            return "exempt", why
    if path in WORLD_GRADED:
        return "world", WORLD_GRADED[path]
    if kind == "sprite" or path in ui_textures():
        return "ui", "drawn on a canvas: the default UI material or a repo UI shader grades it"
    return None, "a coloured texture no graded shader draws: give its shader ColorGrade.cginc (or name it in EXEMPT)"


_UI_TEX = None


def ui_textures():
    """Textures a RawImage or a canvas view draws (referenced from a prefab or scene component that is no material)."""
    global _UI_TEX
    if _UI_TEX is None:
        _UI_TEX = set()
        gm = ct.guid_map()
        for root in ("Assets/_Prefabs", "Assets/_Scenes", "Assets/Resources"):
            for dp, _, files in os.walk(os.path.join(REPO, root)):
                for f in files:
                    if f.endswith((".prefab", ".unity")):
                        text = ct.read(os.path.join(dp, f))
                        for g in re.findall(r"\{fileID: 2800000, guid: ([0-9a-f]{32})", text):
                            p = gm.get(g)
                            if p:
                                _UI_TEX.add(p)
    return _UI_TEX


def survey(previous=None):
    """{path: {kind, coloured, graded, why, sha}} for every texture a build can load; texels are read only for a
    texture whose content changed since `previous`."""
    prev = (previous or {}).get("textures", {})
    out = {}
    for path in sorted(p for p in ct.reachable() if p.lower().endswith(TEX_EXT)):
        full = os.path.join(REPO, path)
        with open(full, "rb") as fh:
            sha = hashlib.sha1(fh.read()).hexdigest()[:16]
        old = prev.get(path)
        frac = old["coloured"] if old and old.get("sha") == sha else coloured_fraction(full)
        kind = texture_kind(path)
        entry = {"kind": kind, "coloured": frac, "sha": sha}
        if frac >= COLOURED_FRACTION:
            entry["graded"], entry["why"] = how_graded(path, kind)
        out[path] = entry
    return out


def record(textures):
    counts = {}
    for e in textures.values():
        k = e.get("graded") or ("grey" if e["coloured"] < COLOURED_FRACTION else "ungraded")
        counts[k] = counts.get(k, 0) + 1
    return {"format": "cosmic-shore-colour-grade/1", "doc": "Docs/VesselModels/COLOR_TRACE.md",
            "tool": "Tools/Build/colour_grade.py",
            "rule": "Every texture pixel follows the worn colour set: the trace's rule on every texel, through a 33^3 "
                    "table per set and role (Photic's is the identity). A coloured texture is graded (ui or world) "
                    "or exempt for a named reason; grey ones need nothing.",
            "counts": dict(sorted(counts.items())), "textures": textures}


# ----------------------------------------------------------------------------- the gate
def check():
    errs = []
    for path, blob in tables().items():
        if not os.path.exists(path):
            errs.append("%s is missing: run python3 Tools/Build/colour_grade.py" % ct.rel(path))
            continue
        try:
            n, got = decode(open(path, "rb").read())
        except ValueError as e:
            errs.append("%s: %s" % (ct.rel(path), e))
            continue
        _, want = decode(blob)
        off = max(abs(a - b) for a, b in zip(got, want)) if n == N else 999
        if off > 1:
            errs.append("%s drifted from its set (by %d/255): run python3 Tools/Build/colour_grade.py" % (ct.rel(path), off))
    names = {os.path.basename(table_path(name, role)) for _, name in graded_sets() for role in ROLES}
    if os.path.isdir(OUT_DIR):
        for f in sorted(os.listdir(OUT_DIR)):
            if f.endswith(".bytes") and f not in names:
                errs.append("Assets/Resources/ColorGrades/%s is no shipped set's: delete it" % f)
    try:
        old = json.load(open(RECORD, encoding="utf-8"))
    except (OSError, ValueError):
        old = None
    now = survey(old)
    for path, e in now.items():
        if e.get("graded") is None and e["coloured"] >= COLOURED_FRACTION:
            errs.append("%s has colour (%.0f%% of its texels) and nothing grades it: %s" % (path, 100 * e["coloured"], e["why"]))
    if old is None or old.get("textures") != now:
        errs.append("colour_grade.json is stale: run python3 Tools/Build/colour_grade.py")
    return errs


def write():
    os.makedirs(OUT_DIR, exist_ok=True)
    write_meta(OUT_DIR, FOLDER_META)
    for path, blob in tables().items():
        with open(path, "wb") as fh:
            fh.write(blob)
        write_meta(path, TEXT_META)
        print("wrote", ct.rel(path), "(%d bytes)" % len(blob))
    try:
        old = json.load(open(RECORD, encoding="utf-8"))
    except (OSError, ValueError):
        old = None
    rec = record(survey(old))
    with open(RECORD, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(rec, fh, indent=1, ensure_ascii=False, sort_keys=True)
        fh.write("\n")
    print("wrote %s: %s" % (ct.rel(RECORD), rec["counts"]))


def report():
    rec = json.load(open(RECORD, encoding="utf-8"))
    print("textures a build can load: %d  %s" % (len(rec["textures"]), rec["counts"]))
    for path, e in sorted(rec["textures"].items()):
        if e["coloured"] >= COLOURED_FRACTION:
            print("  %-7s %5.1f%%  %s" % (e.get("graded") or "UNGRADED", 100 * e["coloured"], path))


def self_test():
    bad = []

    def expect(name, cond):
        if not cond:
            bad.append(name)
    defs, photic = ct.set_defs(), cs.photic_reference()
    s = defs["nocturne"]
    for grey in ((0.5, 0.5, 0.5), (0.1, 0.1, 0.1), (1, 1, 1), (0, 0, 0)):
        expect("a grey keeps %s" % (grey,), all(abs(a - b) < 1e-6 for a, b in zip(grade("ui", grey, s, photic), grey)))
    lime = (0.45, 0.95, 0.2)
    expect("a lime stays lime", cd.classify(ct.truth(), grade("world", lime, s, photic))[1] == "lime")
    inside = (0.95, 0.15, 0.1)          # deep in Fire's cell: the grade is the trace's own repaint there
    want = ct.repaint("world", list(inside) + [1], s, photic)[:3]
    expect("inside a cell the grade is the trace's rule",
           math.dist(cs.oklab(grade("world", inside, s, photic)), cs.oklab(tuple(want))) < 1e-6)
    a, b = (0.30, 0.55, 0.90), (0.302, 0.55, 0.90)
    expect("a small step stays small", math.dist(cs.oklab(grade("ui", a, s, photic)), cs.oklab(grade("ui", b, s, photic))) < 0.02)
    raw = table("world", s, photic, n=5)
    expect("a table round-trips", decode(encode(raw, 5)) == (5, raw))
    expect("the identity is no table", all(k != "photic" for k, _ in graded_sets()))
    expect("an exempt mark is exempt", how_graded("Assets/_Graphics/UI/Brands/google_logo_dark_pill.png", "sprite")[0] == "exempt")
    expect("a coloured world texture nobody grades is caught", how_graded("Assets/_Graphics/Materials/x.png", "texture")[0] is None)
    with contextlib.redirect_stderr(io.StringIO()):
        refused = main(["report"]) == 2 and main(["--check", "--x"]) == 2
    expect("an unknown argument writes nothing", refused)
    print("colour_grade self-test: " + ("ok" if not bad else "FAILED " + "; ".join(bad)))
    return 0 if not bad else 1


COMMANDS = ("--check", "--report", "--self-test")


def main(argv):
    if argv in (["-h"], ["--help"]):
        print(__doc__)
        return 0
    # Writing is the default, so anything this does not know (a typo, `report` without its dashes) must not fall
    # through to it.
    if len(argv) > 1 or (argv and argv[0] not in COMMANDS):
        print("colour_grade: unknown arguments: %s (nothing written; --help lists the commands)" % " ".join(argv),
              file=sys.stderr)
        return 2
    if "--self-test" in argv:
        return self_test()
    if "--check" in argv:
        errs = check()
        for e in errs[:40]:
            print("colour_grade: " + e)
        if not errs:
            print("colour_grade --check: ok")
        return 1 if errs else 0
    if "--report" in argv:
        report()
        return 0
    write()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
