#!/usr/bin/env python3
"""The Toy Box portraits: one angular white glyph per toy that TEACHES what the toy does, drawn through the icon kit.

    python3 Tools/Build/icon_kit/toy_portraits.py --preview   # render, gate, sheet; nothing under Assets/ changes
    python3 Tools/Build/icon_kit/toy_portraits.py             # ... and write the PNGs (metas and guids kept)
    python3 Tools/Build/icon_kit/toy_portraits.py --check     # drift gate: re-render, compare to the PNGs on disk (reads only)

Where they show: the app shell's Toy Box grid (`ToyCard.prefab`'s 128 px Portrait, through `ToyPortraitLibrary`),
the Today's-activity card, and the codex's Tool pages - one sprite per toy, resolved from `Codex.asset`'s entry
`Image` (CODEX.md §4). They replace the codex baker's `ToolPortraitBuilder` emblems (a ring round some spheres in
the toy's accent): seven near-identical discs that said "a station" and nothing about what flying through it does.
The baker now stands down on Tool pages (`CodexImageBaker.CanBake`), so a re-bake cannot put the spheres back.

The language is the ability-icon language (.claude/skills/ability-icon/references/ICON_LANGUAGE.md): angular,
filled mass first, one weight, pure white with the shape in alpha (the card tints; nothing is baked), a 0.6 ghost
for "the other one" / "the next one", a hull is its REAL mesh (`mesh_silhouette.py`), and an element is its petal
SHAPE (`Assets/Resources/ElementPetals/`) - never a colour (LOCKED, Garrett 2026-10-24). A portrait is drawn on the
kit's 128 px grid and written at 512 px (the codex's size); the gates measure the 128 px render.

The bar is Garrett's (2026-10-25): "the icons are teaching the ability". Each portrait names the ACT, and the three
"pick one of these" toys share one grammar - the thing you get SOLID, the others as GHOSTS beside it - so the
Pilot row reads as one family:

  Vessel Changer   a Squirrel (solid) and a Dolphin (ghost), both from above, with the swap between them: change ship
  Domain Changer   a fan of three swatch chips, the front one solid with your hull cut out of it: pick your colour
  Element Charger  the HUD's element flower with the four petal shapes (Charge, Mass, Space, Time), each grown most of
                   the way out of its ghost: what flying one of the toy's crystals does to that element
  Cell Selector    the cell you pick, solid, its crystal at the nucleus, between two ghost cells: pick a world
  Wander           your hull leaving through a gap in the cell membrane onto a path of prisms that runs on into the
                   distance: leave the cell, the world is built ahead of you
  Connect the Dots a star traced dot to dot, the last strokes still a ghost: fly the dots and your trail paints it
  Spawn Matrix     a creature, a plant and a hull standing on one release bench: the three kingdoms you release
"""
import hashlib
import io
import math
import os
import re
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
import canvas as C  # noqa: E402
import mesh_silhouette  # noqa: E402
import motifs  # noqa: E402
import verify  # noqa: E402

CODEX = os.path.join(ROOT, "Assets", "Resources", "Codex.asset")
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Codex")
DONOR_META = os.path.join(OUT_DIR, "tool_vessel-changer.png.meta")   # the codex's own sprite import settings
SHEET = os.path.join(ROOT, "Docs", "Icons", "sheets", "toybox.png")
PETALS = os.path.join(ROOT, "Assets", "Resources", "ElementPetals")
OUT_SIZE = 512
GHOST = 0.6                 # VOCABULARY.md: 0.5 sits on the gates' threshold and vanishes small
TOLERANCE = 2
BEFORE_REF = "74d0ec8fc6"   # the last commit with the baked emblems: the sheet's BEFORE column reads them from here

SQUIRREL = "Assets/_Models/Vessel Models/SquirrelVessel_CosmicShoresTest1.fbx"
DOLPHIN = "Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx"   # the live model (specs/dolphin.py)


# ---------------------------------------------------------------------------- shared pieces

def squirrel(box, cx, cy, roll=0.0, dilate_px=1.4):
    """The Squirrel from straight above, nose right at roll 0 - the freestyle default hull, so 'your ship'. Plan,
    not its ability icons' view D: two hulls side by side read as the same kind of thing only in the same view,
    and the Dolphin's is plan."""
    return mesh_silhouette.silhouette(SQUIRREL, box=box, cx=cx, cy=cy, roll=roll, dilate_px=dilate_px)


def dolphin(box, cx, cy, roll=0.0, dilate_px=1.6):
    """The Dolphin from straight above, nose right at roll 0 (specs/dolphin.py's VIEW_TOP: its file nose is -Y)."""
    return mesh_silhouette.silhouette(DOLPHIN, box=box, cx=cx, cy=cy, roll=roll, dilate_px=dilate_px, mirror=True)


def grown(mask, px):
    """A mask dilated by px icon px (the cut that keeps a ghost off the solid part it sits behind)."""
    if px <= 0:
        return mask
    r = int(px * C.SS) | 1
    return np.asarray(Image.fromarray(mask.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(r))) > 127


def petal(element, tip, direction, length):
    """An element's petal SHAPE from the HUD's own sprite (Assets/Resources/ElementPetals), so the toy and the
    flower can never disagree: its point at `tip`, its wide end `length` icon px away along `direction` (degrees,
    0 = +x, clockwise on screen) - the flower's layout, where every petal points in at the centre."""
    src = Image.open(os.path.join(PETALS, f"{element.lower()}_petal.png")).convert("RGBA").split()[3]
    src = src.crop(src.getbbox())                       # the sprite draws point DOWN: the point is the bottom edge
    k = length * C.SS / src.height
    src = src.resize((max(1, int(round(src.width * k))), max(1, int(round(src.height * k)))), Image.LANCZOS)
    # Turn the sprite so its base-to-point axis runs along -direction (the base lies out along +direction).
    rot = src.rotate(-90.0 - direction, expand=True, resample=Image.BICUBIC)   # PIL turns counter-clockwise
    a = math.radians(direction)
    cx = tip[0] + math.cos(a) * length / 2
    cy = tip[1] + math.sin(a) * length / 2
    im = Image.new("L", (C.N, C.N), 0)
    im.paste(rot, (int(round(cx * C.SS - rot.width / 2)), int(round(cy * C.SS - rot.height / 2))))
    return np.asarray(im) > 127


def swap_arrow(y, x0, x1, head=12, w=C.W_SECONDARY):
    """One half of the swap mark: a bar with an arrowhead at x1 (points left when x1 < x0)."""
    d = 1.0 if x1 > x0 else -1.0
    shaft = C.stroke([(x0, y), (x1 - d * head * 0.8, y)], w)
    return shaft | motifs.arrowhead((x1, y), (d, 0.0), head, spread=0.75)


def cell(cx, cy, r, w=C.W_PRIMARY, nucleus=None):
    """A cell as the toys show one: the membrane ring with its crystal at the nucleus."""
    s = r * 0.5 if nucleus is None else nucleus
    return C.ring(cx, cy, r, w=w) | (motifs.prism_lit(cx, cy, s) if s >= 11 else motifs.prism(cx, cy, s))


def fish(cx, cy, s):
    """A creature: the vocabulary's lifeform hexagon body with a forked tail, nose right; s = body radius."""
    body = C.fill(C.scale(C.ngon(6, s, cx, cy, rot=0.0), 1.25, 1.0, cx, cy))
    tail = C.fill([(cx - s * 1.0, cy), (cx - s * 1.9, cy - s * 0.9), (cx - s * 1.6, cy), (cx - s * 1.9, cy + s * 0.9)])
    eye = C.fill(C.ngon(4, s * 0.17, cx + s * 0.6, cy - s * 0.22, rot=45.0))
    return (body | tail) & ~eye


# ---------------------------------------------------------------------------- the seven portraits

def vessel_changer():
    """Fly through, change ship: your hull solid, the next hull a ghost beside it, nose up, the swap between them."""
    a = squirrel(72, 31, 64, roll=-90.0)
    b = dolphin(60, 90, 52, roll=-90.0)
    swap = swap_arrow(90, 48, 84, head=10) | swap_arrow(104, 84, 48, head=10)
    return [(b, GHOST), (swap, 1.0), (a, 1.0)]


def domain_changer():
    """Pick your colour: a fan of three swatch chips; the front one solid, your hull knocked out of it."""
    pivot = (64.0, 118.0)

    def chip(deg):
        x0, x1, y0, y1, c = 50, 78, 14, 106, 9
        pts = [(x0 + c, y0), (x1, y0), (x1, y1 - c), (x1 - c, y1), (x0, y1), (x0, y0 + c)]   # the corner sliver
        return C.fill(C.rotate(pts, deg, *pivot))

    front = chip(0.0)
    sides = (chip(-26.0) | chip(26.0)) & ~grown(front, 7.0)
    hull = dolphin(40, 64, 38, roll=-90.0, dilate_px=1.2)
    return [(sides, GHOST), (front & ~hull, 1.0)]


ELEMENTS = ("Charge", "Mass", "Space", "Time")   # ElementChargerToy.Elements (the HUD order), laid clockwise from the top


def element_charger():
    """Your vessel's four elements as the HUD flower (each its own petal shape, never a colour), each petal grown
    most of the way out of its ghost: what flying one of the toy's crystals does to that element."""
    solids, ghosts = C.empty(), C.empty()
    c = (64.0, 64.0)
    for i, e in enumerate(ELEMENTS):
        d = -90.0 + 90.0 * i                       # Charge at the top, then clockwise
        a = math.radians(d)
        tip = (c[0] + math.cos(a) * 9, c[1] + math.sin(a) * 9)
        full = petal(e, tip, d, 48)
        part = petal(e, tip, d, 31)
        solids |= part
        ghosts |= full
    ghosts &= ~grown(solids, 5.0)
    return [(ghosts, GHOST), (solids, 1.0)]


def cell_selector():
    """Pick a world: the chosen cell solid between two ghost cells, the carousel running on past the card."""
    centre = cell(64, 64, 34, w=10, nucleus=17)
    sides = (cell(8, 64, 22, w=7, nucleus=9) | cell(120, 64, 22, w=7, nucleus=9)) & C.rect(6, 0, 122, 128)
    sides &= ~grown(C.disc(64, 64, 39), 6.0)
    return [(sides, GHOST), (centre, 1.0)]


def wander():
    """Leave the cell: your hull out through a gap in its membrane, onto prisms that run on into the distance."""
    membrane = C.band(42, 86, 32, -5.0, 265.0, w=10) | motifs.prism_lit(42, 86, 13)
    hull = dolphin(54, 78, 48, roll=-45.0)
    path = motifs.prism(104, 23, 8) | motifs.prism(114, 12, 5)
    return [(path, GHOST), (membrane, 1.0), (hull, 1.0)]


def painting():
    """Connect the dots: a star traced dot to dot with your trail, the last strokes still to fly."""
    pts = C.ngon(5, 48, 64, 67, rot=-90.0)
    order = [pts[i] for i in (0, 2, 4, 1, 3)]
    done = C.stroke(order[:4], w=C.W_SECONDARY)
    todo = C.stroke([order[3], order[4], order[0]], w=C.W_SECONDARY)
    dots = C.empty()
    for x, y in pts:
        dots |= motifs.prism(x, y, 10)
    ahead = todo & ~(dots | done)
    return [(ahead, GHOST), (done | dots, 1.0)]


def spawn_matrix():
    """The release bench: a creature, a plant and a hull standing on one bench - the three kingdoms it releases."""
    bench = C.chamfered_rect(8, 106, 120, 118, 6)
    plant = C.stroke([(22, 106), (22, 44)], w=C.W_SECONDARY) \
        | C.stroke([(22, 84), (11, 70)], w=C.W_SECONDARY) | C.stroke([(22, 70), (33, 56)], w=C.W_SECONDARY) \
        | motifs.prism(11, 66, 7) | motifs.prism(33, 52, 7) | motifs.prism(22, 38, 9)
    creature = fish(74, 40, 15)
    hull = dolphin(52, 94, 74, roll=-90.0, dilate_px=1.4)
    return [(bench | plant | creature | hull, 1.0)]


# The gates are the ability-row icon's (verify.py), built for 60 px under the gauge fill and the cooldown veil. A
# portrait draws at 128 px on the Toy Box card, so a gate that only protects the 60 px read is exempted BY NAME,
# with the reason; every other gate (white, coverage, the optical box, specks where they are not the point, the
# 40 px mass) still blocks the write.
PORTRAIT = "a portrait draws at 128 px on the card, never at 60 px under the veil"
TOYS = [
    dict(entry="tool.vessel-changer", draw=vessel_changer, exempt={
        "gap": "the Squirrel's real mesh: its leg struts and tail ribs (the same exemption as its ability icons)",
        "components": "two hulls and two arrows by construction, plus the Squirrel's separate leg struts; " + PORTRAIT}),
    dict(entry="tool.domain-changer", draw=domain_changer, exempt={
        "gap": "the Dolphin knocked out of the front chip: its fins are slivers of negative space; " + PORTRAIT}),
    dict(entry="tool.element-charger", draw=element_charger, exempt={
        "gap": "each petal's ghost is a band 5 px off its solid petal - the growth IS the gap; " + PORTRAIT,
        "components": "four petals and four ghosts: the flower is four separate shapes on the HUD too"}),
    dict(entry="tool.cell-selector", draw=cell_selector, exempt={
        "components": "the two ghost cells are cut by the card's edge (the carousel runs on past it); " + PORTRAIT}),
    dict(entry="tool.wander", draw=wander, exempt={
        "components": "the path ahead is separate prisms, as the game lays them; " + PORTRAIT,
        "specks": "the farthest prism is small ON PURPOSE: it is the distance"}),
    dict(entry="tool.painting", draw=painting, exempt={
        "gap": "the star's strokes cross, enclosing small triangles at its heart - a traced star does; " + PORTRAIT}),
    dict(entry="tool.spawn-matrix", draw=spawn_matrix, exempt={
        "components": "a creature, a plant and a hull: the three kingdoms are three things; " + PORTRAIT}),
]


# ---------------------------------------------------------------------------- render

def render(layers, size):
    """The kit's render, at any size the 1024 grid divides into (128 for the gates, 512 for the asset)."""
    if not isinstance(layers, list):
        layers = [(layers, 1.0)]
    k = C.N // size
    a = np.zeros((size, size), dtype=np.float64)
    for mask, la in layers:
        top = mask.reshape(size, k, size, k).mean(axis=(1, 3)) * la
        a = top + a * (1.0 - top)
    rgba = np.zeros((size, size, 4), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.clip(np.round(a * 255), 0, 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


# ---------------------------------------------------------------------------- the codex: which file is each toy's

def codex_entries():
    """{entry id: {"name", "tagline", "accent", "image_guid"}} for the Tool pages of Codex.asset."""
    text = open(CODEX, encoding="utf-8").read()
    out = {}
    for m in re.finditer(r"^  - Id: (tool\.[\w-]+)\n(.*?)(?=^  - Id: |\Z)", text, re.M | re.S):
        body = m.group(2)
        # The entry's own Image is the first one at entry indent (4 spaces); variants' sit deeper.
        img = re.search(r"^    Image: \{fileID: (\d+)(?:, guid: ([0-9a-f]{32}), type: 3)?\}", body, re.M)
        name = re.search(r"^    DisplayName: (.*)$", body, re.M)
        tag = re.search(r"^    Tagline: (.*(?:\n      .*)*)", body, re.M)
        acc = re.search(r"^    AccentColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)", body, re.M)
        out[m.group(1)] = dict(
            name=name.group(1).strip().strip('"') if name else m.group(1),
            tagline=re.sub(r"\s+", " ", tag.group(1)).strip().strip('"') if tag else "",
            accent=tuple(int(float(v) * 255) for v in acc.groups()) if acc else (255, 255, 255),
            image_guid=img.group(2) if img else None,
        )
    return out


def guid_index():
    idx = {}
    for f in os.listdir(OUT_DIR):
        if f.endswith(".png.meta"):
            g = re.search(r"^guid: ([0-9a-f]{32})", open(os.path.join(OUT_DIR, f)).read(), re.M)
            if g:
                idx[g.group(1)] = os.path.join(OUT_DIR, f[:-5])
    return idx


def target_png(entry_id, entry, idx):
    """The file the codex page already points at (its guid is what every reader binds), else the baker's own name
    for a page that has none yet (CodexImageBaker.FileNameFor: the id with '.' -> '_')."""
    if entry and entry["image_guid"] in idx:
        return idx[entry["image_guid"]]
    return os.path.join(OUT_DIR, entry_id.replace(".", "_") + ".png")


def guid_for(entry_id):
    return hashlib.md5(f"CosmicShore/IconKit/Toybox/{entry_id}".encode()).hexdigest()


def sprite_meta(guid):
    lines = open(DONOR_META).read().splitlines()
    lines[1] = f"guid: {guid}"
    return "\n".join(lines) + "\n"


def bind_codex(entry_id, guid):
    """Point a page with NO image at the new sprite (CODEX.md §2: Image is 'filled only when empty', so this never
    overwrites a human's choice). Returns True when the asset changed."""
    text = open(CODEX, encoding="utf-8").read()
    m = re.search(rf"^  - Id: {re.escape(entry_id)}\n(.*?)(?=^  - Id: |\Z)", text, re.M | re.S)
    body = m.group(1)
    old = "\n    Image: {fileID: 0}\n"
    if old not in body:
        return False
    new_body = body.replace(old, f"\n    Image: {{fileID: 21300000, guid: {guid}, type: 3}}\n", 1)
    open(CODEX, "w", encoding="utf-8").write(text[:m.start(1)] + new_body + text[m.end(1):])
    return True


def before_image(png):
    """The portrait as it was before this generator drew it (BEFORE_REF), so the sheet keeps showing what was replaced
    however often it is re-run; None for a toy that had no portrait then (the Element Charger)."""
    rel = os.path.relpath(png, ROOT)
    try:
        data = subprocess.run(["git", "-C", ROOT, "show", f"{BEFORE_REF}:{rel}"], capture_output=True, check=True).stdout
        return Image.open(io.BytesIO(data)).convert("RGBA")
    except (subprocess.CalledProcessError, OSError):
        return None


# ---------------------------------------------------------------------------- the sheet: the card as the Toy Box draws it

FILL = (0, 4, 31, 255)          # ToyCard Fill
BORDER = (34, 38, 69, 255)      # ToyCard Border / Seam
WELL = (34, 38, 69, 115)        # ToyCard Well (Neutral Dark @ 45 %)
LIGHT = (230, 233, 255, 255)
MUTED = (116, 123, 173, 255)
NAVY = (14, 20, 44, 255)


def _font(path, size):
    try:
        return ImageFont.truetype(os.path.join(ROOT, path), size)
    except OSError:
        return ImageFont.load_default()


ALDRICH = "Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/ALDRICH-REGULAR.TTF"


def toy_card(portrait, entry, section):
    """An offline approximation of ToyCard.prefab (341 x 272: the accent stripe, the well, the 128 px portrait, the
    section eyebrow, the name, the tagline) - for LOOKING; the Editor is the truth."""
    W, H, sl = 341, 272, 14
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    outline = [(sl, 0), (W, 0), (W, H - sl), (W - sl, H), (0, H), (0, sl)]
    d.polygon(outline, fill=BORDER)
    d.polygon([(sl + 1, 1), (W - 1, 1), (W - 1, H - sl - 1), (W - sl - 1, H - 1), (1, H - 1), (1, sl + 1)], fill=FILL)
    well = Image.new("RGBA", (W - 2, 150), WELL)
    im.alpha_composite(well, (1, 1))
    d.rectangle([12, 1, W - 12, 3], fill=entry["accent"] + (255,))
    if portrait is not None:
        p = portrait.convert("RGBA").resize((128, 128), Image.LANCZOS)
        im.alpha_composite(p, ((W - 128) // 2, 12))
    else:
        d.text((W // 2 - 40, 70), "(no portrait)", fill=MUTED, font=_font(ALDRICH, 14))
    d.line([(1, 151), (W - 2, 151)], fill=BORDER)
    d.text((16, 160), section.upper(), fill=MUTED, font=_font(ALDRICH, 13))
    d.text((16, 180), entry["name"], fill=LIGHT, font=_font(ALDRICH, 22))
    words, line, lines = entry["tagline"].split(), "", []
    f = _font(ALDRICH, 14)
    for w in words:
        if d.textlength((line + " " + w).strip(), font=f) > W - 32:
            lines.append(line)
            line = w
        else:
            line = (line + " " + w).strip()
    lines.append(line)
    for i, ln in enumerate(lines[:2]):
        d.text((16, 214 + i * 19), ln, fill=LIGHT, font=f)
    return im


SECTIONS = {"tool.vessel-changer": "Pilot", "tool.domain-changer": "Pilot", "tool.element-charger": "Pilot",
            "tool.cell-selector": "World", "tool.wander": "World", "tool.painting": "Creation",
            "tool.spawn-matrix": "Creation"}


def draw_sheet(rows, out):
    """rows: [(entry_id, entry, before, after)]. BEFORE and AFTER on the card, then the raw portrait at 64 and 40."""
    pad, cw, ch = 16, 341, 272
    col = cw + 16 + 64 + 12 + 40 + 24
    W = pad * 2 + col * 2
    H = 44 + len(rows) * (ch + pad)
    sheet = Image.new("RGBA", (W, H), NAVY)
    d = ImageDraw.Draw(sheet)
    f = _font(ALDRICH, 16)
    d.text((pad, 12), "Toy Box portraits  -  BEFORE (codex emblem bake)", fill=LIGHT, font=f)
    d.text((pad + col, 12), "AFTER (icon_kit/toy_portraits.py)  card / 64 / 40", fill=LIGHT, font=f)
    for i, (eid, entry, before, after) in enumerate(rows):
        y = 44 + i * (ch + pad)
        for g, img in enumerate((before, after)):
            x = pad + g * col
            sheet.alpha_composite(toy_card(img, entry, SECTIONS.get(eid, "")), (x, y))
            if img is None:
                continue
            for s, dx in ((64, cw + 16), (40, cw + 16 + 64 + 12)):
                t = Image.new("RGBA", (s, s), FILL)
                t.alpha_composite(img.convert("RGBA").resize((s, s), Image.LANCZOS))
                sheet.alpha_composite(t, (x + dx, y + 40))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sheet.convert("RGB").save(out, optimize=True)
    return out


# ---------------------------------------------------------------------------- run

def gate(toy, im128):
    with tempfile.TemporaryDirectory() as td:
        p = os.path.join(td, toy["entry"].replace(".", "_") + ".png")
        im128.save(p)
        r = verify.measure(p)
        fails = verify.judge(r)
        exempt = [x for x in fails if x in toy["exempt"]]
        fails = [x for x in fails if x not in toy["exempt"]]
        print("  " + verify.fmt(r, fails) + "".join(f"  [exempt {e}: {toy['exempt'][e]}]" for e in exempt))
        return bool(fails)


def same_pixels(a, b):
    x = np.asarray(a.convert("RGBA"), dtype=np.int16)
    y = np.asarray(b.convert("RGBA"), dtype=np.int16)
    return x.shape == y.shape and int(np.abs(x - y).max()) <= TOLERANCE


def main(argv):
    check, preview = "--check" in argv, "--preview" in argv
    entries, idx = codex_entries(), guid_index()
    bad, drift, rows, renders = 0, 0, [], []
    print("Toy Box portraits: gates (on the 128 px render)")
    for toy in TOYS:
        layers = toy["draw"]()
        im128, im = render(layers, C.SIZE), render(layers, OUT_SIZE)
        bad += gate(toy, im128)
        entry = entries.get(toy["entry"])
        if entry is None:
            print(f"  MISSING codex page {toy['entry']} (run FrogletTools > UI > Codex > Scan & Merge)")
            bad += 1
            continue
        png = target_png(toy["entry"], entry, idx)
        before = before_image(png)
        rows.append((toy["entry"], entry, before, im))
        renders.append((toy, entry, png, im))
        if check:
            on_disk = Image.open(png).convert("RGBA") if os.path.exists(png) else None
            if on_disk is None or not same_pixels(on_disk, im):
                print(f"  DRIFT {os.path.relpath(png, ROOT)}")
                drift += 1
            elif entry["image_guid"] is None:
                print(f"  DRIFT {toy['entry']} has no Image in Codex.asset")
                drift += 1
    if check:
        print(f"  {'no drift' if not drift else str(drift) + ' portrait(s) drifted'}")
        return 1 if (drift or bad) else 0
    print(f"  sheet: {os.path.relpath(draw_sheet(rows, SHEET), ROOT)}")
    if preview:
        return 1 if bad else 0
    if bad:
        print("  NOT WRITTEN: a gate failed (fix the drawing, or exempt the gate with who said so and why)")
        return 1
    for toy, entry, png, im in renders:
        im.save(png, optimize=True)
        print(f"  wrote {os.path.relpath(png, ROOT)}")
        meta = png + ".meta"
        if not os.path.exists(meta):
            open(meta, "w").write(sprite_meta(guid_for(toy["entry"])))
            print(f"  wrote {os.path.relpath(meta, ROOT)} (new guid)")
        guid = re.search(r"^guid: ([0-9a-f]{32})", open(meta).read(), re.M).group(1)
        if entry["image_guid"] is None and bind_codex(toy["entry"], guid):
            print(f"  bound {toy['entry']} Image -> {os.path.basename(png)} in Codex.asset")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
