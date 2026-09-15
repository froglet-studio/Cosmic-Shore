#!/usr/bin/env python3
"""
Author the Toy Box's LAYOUT in Menu_Main: the type scale, the card sizes, and the card plates.

    python3 Tools/Build/author_toybox_layout.py            # write the scene
    python3 Tools/Build/author_toybox_layout.py --check    # fail if the scene drifted

WHAT IT FIXES (measured on the authored scene, 2026-09-09):

  * The type was the ARCADE's, then re-banded UP: the toy's name at 42-58, its paragraph at
    22-44, the variants header at 34-44 and a variant's name at 22-34 - on a window whose whole
    left column is those three labels. It read as a poster. Every band is halved here, and they
    stay BANDS (autosize min..max, wrapping on) for the reason Docs/HomeHub/ARCHITECTURE.md
    section 5.4.1 gives: content of no fixed length breaks by clipping.

  * The toy grid's cells were 260x96 while the card template inside them was authored at 275x203
    with a 313x208 plate hanging off its top-left corner - so every card overran its cell and the
    grid read as a strip of small overlapping tiles. The cell is now 400x250, the plates STRETCH to
    it and draw SLICED (the sprites carry a 9-slice border since author_toy_card_sprites.py), the
    portrait fills the upper two thirds, and the tagline + category line the card was already
    written to show (ToyboxCard.taglineText / sectionText, unbound until now) are created and
    bound.

  * The variant row's plates are stretched and sliced too, so the chamfer is a chamfer at
    275x88 rather than a 228x170 picture squashed to 3:1.

The editor tool (FrogletTools > Interface > Home Hub Wiring) writes the SAME numbers, so a re-run
of WIRE IT after this cannot regress it; this script exists because the layout has to land on the
branch, and the tool's output is otherwise a working-tree change nobody pushed
(Docs/TOOLING.md, "Tool output is a deliverable"). Both read the constants below.
"""
import argparse
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENE = os.path.join(ROOT, "Assets", "_Scenes", "Menu_Main.unity")

INT64_MAX = 2 ** 63 - 1

# script guids (package scripts; stable across this project's Unity version)
TMP_GUID = "f4688fdb7df04437aeb418b961361dc5"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
FITTER_GUID = "3245ec927659c4140ac4f8d17403cc18"
GRID_GUID = "8a8695521f0d02e499659fee002a26c2"

PLATE_SPRITE = "54ad1e72"       # Group 1585.png        - the card body
RIM_SPRITE = "a0f080f0"         # Rectangle 1127 (2).png - the card rim

# ---- the numbers. The editor tool mirrors these (HomeHubWiringWindow.ToyLayout). --------------
TOY_CELL = (400.0, 250.0)
TOY_SPACING = (20.0, 20.0)
TOY_PADDING = 16
VARIANT_CELL = (275.0, 88.0)
VARIANT_SPACING = (16.0, 14.0)
VARIANT_PADDING = 12
# Both grids start UPPER-LEFT (GridLayoutGroup childAlignment 0): a centred grid puts a lone row
# (the Wanderway's one "Wander", the Arkway's one "Set sail") in the middle of an empty strip,
# which reads as a misplaced card rather than as a list of one. The extra left inset keeps the
# first column off the window's edge now that nothing centres it.
GRID_ALIGNMENT = 0
GRID_EXTRA_LEFT = 20

# The plate sprites are 9-sliced at author_toy_card_sprites.py's PPU 400 (= design scale x4), and
# UGUI divides a sprite's PPU by the CANVAS's referencePixelsPerUnit before slicing - so a border
# that is 20 design units on a 100-ppu canvas is 48 on Menu_Main's 240-ppu one, which is how the
# chamfer shipped at twice its size on the toy cards while the arcade (which draws the same sprite
# Simple) looked right. Read off the scene rather than written down: pixelsPerUnitMultiplier =
# referencePixelsPerUnit / 100 restores the design scale whatever the canvas is set to.
SLICE_MULTIPLIER_REF = 100.0

# (path under the window, min, max, label). Paths are name walks, root-relative.
CONFIGURE_BANDS = [
    ("ToyboxGameConfigureModal", "GameView/Game Name", 28.0, 36.0, "the toy's name"),
    ("ToyboxGameConfigureModal", "Game Description", 16.0, 22.0, "the toy's description"),
    ("ToyboxGameConfigureModal", "ConfigurationDetailView/Game Name", 22.0, 28.0, "the variants header"),
    ("ToyboxGameConfigureModal", "ToyVariantTemplate/GameTitle", 16.0, 22.0, "the variant name"),
    ("ToyboxGameConfigureModal", "ToyVariantTemplate/GameDetail", 12.0, 14.0, "the variant detail line"),
    ("ToyboxScreenModal", "ToyCardTemplate/GameTitle", 18.0, 26.0, "the toy card name"),
    ("ToyboxScreenModal", "ToyCardTemplate/Tagline", 11.0, 14.0, "the toy card tagline"),
    ("ToyboxScreenModal", "ToyCardTemplate/Section", 10.0, 13.0, "the toy card category"),
]

# card anchors, as FRACTIONS of the cell (the cell is the designer's to change)
CARD_PORTRAIT = ((0.06, 0.36), (0.94, 0.95))
CARD_TITLE = ((0.06, 0.06), (0.94, 0.34))
# The tagline and the category are authored, bound and SWITCHED OFF: on a grid the title is the
# whole card (the arcade's cards carry a title and art, nothing else), and the sentence lives on
# the detail window. Kept in the scene so the detail can be turned back on without re-authoring.
CARD_TAGLINE = ((0.06, 0.05), (0.68, 0.19))
CARD_SECTION = ((0.68, 0.05), (0.94, 0.19))
CARD_LABELS_ACTIVE = 0
# Variant row: the name sits BOTTOM-LEFT, its detail above it, both inset past the chamfer. The
# card's root Mask is switched off too - it clipped the plate's chamfer out of the first letter.
VARIANT_TITLE = ((0.06, 0.08), (0.74, 0.50))
VARIANT_DETAIL = ((0.06, 0.50), (0.94, 0.88))
MASK_GUID = "31a19414c41e5ae4aae2af33fee712f6"


# ---------------------------------------------------------------------------------------------
# scene model
# ---------------------------------------------------------------------------------------------
class Scene:
    def __init__(self, path):
        self.path = path
        txt = open(path, errors="ignore").read()
        marks = [(m.start(), m.group(0)) for m in
                 re.finditer(r'^--- !u!\d+ &-?\d+(?: stripped)?$', txt, re.M)]
        self.preamble = txt[:marks[0][0]]
        self.order = []
        self.docs = {}          # fid -> [header, body]
        for i, (start, header) in enumerate(marks):
            end = marks[i + 1][0] if i + 1 < len(marks) else len(txt)
            fid = re.search(r"&(-?\d+)", header).group(1)
            self.order.append(fid)
            self.docs[fid] = [header, txt[start + len(header):end]]
        self.changes = []
        self.rebuild_maps()

    def rebuild_maps(self):
        self.go_tf, self.tf_go, self.tf_parent, self.names = {}, {}, {}, {}
        for fid, (h, c) in self.docs.items():
            cls = re.search(r"!u!(\d+)", h).group(1)
            if cls in ("4", "224"):
                og = re.search(r"m_GameObject: \{fileID: (\d+)\}", c)
                if og:
                    self.go_tf[og.group(1)] = fid
                    self.tf_go[fid] = og.group(1)
                pm = re.search(r"m_Father: \{fileID: (\d+)\}", c)
                self.tf_parent[fid] = pm.group(1) if pm and pm.group(1) != "0" else None
            elif cls == "1":
                n = re.search(r"^  m_Name: (.*)$", c, re.M)
                if n:
                    self.names[fid] = n.group(1).strip()

    def cls(self, fid):
        return re.search(r"!u!(\d+)", self.docs[fid][0]).group(1)

    def body(self, fid):
        return self.docs[fid][1]

    def set_body(self, fid, new, why):
        if self.docs[fid][1] != new:
            self.docs[fid][1] = new
            self.changes.append(why)

    def components(self, go):
        return re.findall(r"component: \{fileID: (-?\d+)\}", self.body(go))

    def component_with(self, go, guid=None, field=None):
        for cid in self.components(go):
            if cid not in self.docs:
                continue
            b = self.body(cid)
            if guid and guid in b:
                return cid
            if field and re.search(r"^  %s:" % re.escape(field), b, re.M):
                return cid
        return None

    def rect_of(self, go):
        return self.go_tf.get(go)

    def children(self, go):
        tf = self.go_tf.get(go)
        out = []
        for t, p in self.tf_parent.items():
            if p == tf and t in self.tf_go:
                out.append(self.tf_go[t])
        return out

    def find_root(self, name):
        for fid, n in self.names.items():
            if n == name and self.cls(fid) == "1":
                return fid
        return None

    def walk(self, root_go, path):
        """A GameObject by a '/'-separated NAME walk under root_go - a search per step, so a name
        that appears twice in the window ('Game Name') is disambiguated by its parent."""
        cur = root_go
        for step in path.split("/"):
            found = None
            # breadth: the direct children first, then any descendant with that name
            for ch in self.children(cur):
                if self.names.get(ch) == step:
                    found = ch
                    break
            if not found:
                for cand in self.subtree(cur):
                    if cand != cur and self.names.get(cand) == step:
                        found = cand
                        break
            if not found:
                return None
            cur = found
        return cur

    def subtree(self, root_go):
        kids = {}
        for t, p in self.tf_parent.items():
            kids.setdefault(p, []).append(t)
        out, stack = [], [self.go_tf.get(root_go)]
        while stack:
            tf = stack.pop()
            if not tf:
                continue
            if tf in self.tf_go:
                out.append(self.tf_go[tf])
            stack.extend(kids.get(tf, []))
        return out

    def add_doc(self, fid, header, body):
        self.docs[fid] = [header, body]
        self.order.append(fid)
        self.changes.append(f"add {header}")

    def mint(self, key):
        taken = set(self.docs)
        for salt in range(64):
            h = hashlib.md5(f"CosmicShore/ToyboxLayout/{key}/{salt}".encode()).hexdigest()
            fid = str(int(h[:16], 16) % (INT64_MAX // 2))
            if fid not in taken:
                return fid
        raise SystemExit("could not mint a fileID for " + key)

    def write(self):
        with open(self.path, "w") as fh:
            fh.write(self.preamble)
            for fid in self.order:
                h, b = self.docs[fid]
                fh.write(h + b)


# ---------------------------------------------------------------------------------------------
# edits
# ---------------------------------------------------------------------------------------------
def fmt(v):
    s = f"{v:.6g}"
    return s


def sub1(body, pattern, repl):
    new, n = re.subn(pattern, repl, body, count=1, flags=re.M)
    return new


def set_field(sc, fid, field, value, why):
    b = sc.body(fid)
    new = sub1(b, r"^(  %s: ).*$" % re.escape(field), lambda m: m.group(1) + str(value))
    sc.set_body(fid, new, f"{why}: {field} = {value}")


def set_rect(sc, go, amin, amax, pos=(0, 0), size=(0, 0), pivot=None, why=""):
    rt = sc.rect_of(go)
    if not rt:
        return
    b = sc.body(rt)
    b = sub1(b, r"^  m_AnchorMin: \{.*\}$", f"  m_AnchorMin: {{x: {fmt(amin[0])}, y: {fmt(amin[1])}}}")
    b = sub1(b, r"^  m_AnchorMax: \{.*\}$", f"  m_AnchorMax: {{x: {fmt(amax[0])}, y: {fmt(amax[1])}}}")
    b = sub1(b, r"^  m_AnchoredPosition: \{.*\}$", f"  m_AnchoredPosition: {{x: {fmt(pos[0])}, y: {fmt(pos[1])}}}")
    b = sub1(b, r"^  m_SizeDelta: \{.*\}$", f"  m_SizeDelta: {{x: {fmt(size[0])}, y: {fmt(size[1])}}}")
    if pivot:
        b = sub1(b, r"^  m_Pivot: \{.*\}$", f"  m_Pivot: {{x: {fmt(pivot[0])}, y: {fmt(pivot[1])}}}")
    sc.set_body(rt, b, f"{why}: rect")


def set_band(sc, go, lo, hi, why, align=None):
    tmp = sc.component_with(go, guid=TMP_GUID)
    if not tmp:
        return False
    b = sc.body(tmp)
    b = sub1(b, r"^  m_enableAutoSizing: \d+$", "  m_enableAutoSizing: 1")
    b = sub1(b, r"^  m_fontSizeMin: [\d.]+$", f"  m_fontSizeMin: {fmt(lo)}")
    b = sub1(b, r"^  m_fontSizeMax: [\d.]+$", f"  m_fontSizeMax: {fmt(hi)}")
    b = sub1(b, r"^  m_TextWrappingMode: \d+$", "  m_TextWrappingMode: 1")
    if align:
        h, v = align
        b = sub1(b, r"^  m_HorizontalAlignment: \d+$", f"  m_HorizontalAlignment: {h}")
        b = sub1(b, r"^  m_VerticalAlignment: \d+$", f"  m_VerticalAlignment: {v}")
        b = sub1(b, r"^  m_textAlignment: \d+$", "  m_textAlignment: 65535")
    sc.set_body(tmp, b, f"{why}: band {lo:g}-{hi:g}")
    return True


def slice_multiplier(sc):
    """pixelsPerUnitMultiplier that draws the plates' 9-slice at design scale on THIS canvas."""
    refs = re.findall(r"^  m_ReferencePixelsPerUnit: ([\d.]+)$", "".join(b for _, b in sc.docs.values()), re.M)
    if len(refs) != 1:
        raise SystemExit(f"Menu_Main: expected one Canvas referencePixelsPerUnit, found {len(refs)}")
    return float(refs[0]) / SLICE_MULTIPLIER_REF


def make_sliced(sc, go, sprite_short, why):
    """Every Image on `go` drawing one of the two card plates draws it SLICED, stretched to the
    card. Simple would stretch the chamfer; Sliced keeps it a chamfer at any rect."""
    mult = fmt(slice_multiplier(sc))
    for cid in sc.components(go):
        if cid not in sc.docs:
            continue
        b = sc.body(cid)
        if IMAGE_GUID not in b or f"guid: {sprite_short}" not in b:
            continue
        b = sub1(b, r"^  m_Type: \d+$", "  m_Type: 1")
        b = sub1(b, r"^  m_FillCenter: \d+$", "  m_FillCenter: 1")
        b = sub1(b, r"^  m_PixelsPerUnitMultiplier: [\d.]+$", f"  m_PixelsPerUnitMultiplier: {mult}")
        sc.set_body(cid, b, f"{why}: sliced plate x{mult}")


def disable_mask(sc, go, why):
    """The card templates carry a root Mask that clips every child to the rim sprite's alpha -
    which is the chamfer, so the first letter of a name lost its corner. Off, not removed: the
    component's fileID is referenced by nothing, but a removal is a hand-edit of the object's
    component list this script does not need to make."""
    mask = sc.component_with(go, guid=MASK_GUID)
    if mask:
        set_field(sc, mask, "m_Enabled", 0, f"{why}: Mask off")


def set_active(sc, go, active, why):
    set_field(sc, go, "m_IsActive", 1 if active else 0, why)


def stretch(sc, go, why):
    set_rect(sc, go, (0, 0), (1, 1), (0, 0), (0, 0), (0.5, 0.5), why)


def set_grid(sc, go, cell, spacing, padding, why):
    grid = sc.component_with(go, guid=GRID_GUID)
    if not grid:
        return
    b = sc.body(grid)
    b = sub1(b, r"^  m_CellSize: \{.*\}$", f"  m_CellSize: {{x: {fmt(cell[0])}, y: {fmt(cell[1])}}}")
    b = sub1(b, r"^  m_Spacing: \{.*\}$", f"  m_Spacing: {{x: {fmt(spacing[0])}, y: {fmt(spacing[1])}}}")
    for side in ("Left", "Right", "Top", "Bottom"):
        inset = padding + (GRID_EXTRA_LEFT if side == "Left" else 0)
        b = sub1(b, r"^    m_%s: -?\d+$" % side, f"    m_{side}: {inset}")
    b = sub1(b, r"^  m_ChildAlignment: \d+$", f"  m_ChildAlignment: {GRID_ALIGNMENT}")   # UpperLeft
    sc.set_body(grid, b, f"{why}: grid {cell[0]:g}x{cell[1]:g}")


def ensure_fitter(sc, go, why):
    """A ContentSizeFitter (vertical: preferred) on a scroll content, so the grid can scroll -
    the same fix Docs/HomeHub/ARCHITECTURE.md 5.4.3 records for the variants list."""
    fitter = sc.component_with(go, guid=FITTER_GUID)
    if fitter:
        set_field(sc, fitter, "m_VerticalFit", 2, why)
        set_field(sc, fitter, "m_HorizontalFit", 0, why)
        return
    fid = sc.mint(f"fitter/{go}")
    sc.add_doc(fid, f"--- !u!114 &{fid}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FITTER_GUID}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.ContentSizeFitter
  m_HorizontalFit: 0
  m_VerticalFit: 2
""")
    b = sc.body(go)
    b = sub1(b, r"(^  m_Component:\n(?:  - component: \{fileID: -?\d+\}\n)*)",
             lambda m: m.group(1) + f"  - component: {{fileID: {fid}}}\n")
    sc.set_body(go, b, f"{why}: add ContentSizeFitter")


def ensure_label(sc, parent_go, name, donor_tmp_go, amin, amax, lo, hi, align, alpha, why):
    """A TMP label under `parent_go`, created from a shipped TMP document (the variant card's own
    GameDetail line) so it carries the project's font and material rather than TMP's defaults."""
    existing = None
    for ch in sc.children(parent_go):
        if sc.names.get(ch) == name:
            existing = ch
            break
    if existing:
        set_rect(sc, existing, amin, amax, (0, 0), (0, 0), (0.5, 0.5), why)
        set_band(sc, existing, lo, hi, why, align)
        return existing

    donor = sc.component_with(donor_tmp_go, guid=TMP_GUID)
    if not donor:
        raise SystemExit("no TMP donor to clone the card labels from")

    go = sc.mint(f"label/{parent_go}/{name}")
    rt = sc.mint(f"label-rect/{parent_go}/{name}")
    cr = sc.mint(f"label-cr/{parent_go}/{name}")
    tm = sc.mint(f"label-tmp/{parent_go}/{name}")

    sc.add_doc(go, f"--- !u!1 &{go}", f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt}}}
  - component: {{fileID: {cr}}}
  - component: {{fileID: {tm}}}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    parent_rt = sc.rect_of(parent_go)
    sc.add_doc(rt, f"--- !u!224 &{rt}", f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_rt}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {fmt(amin[0])}, y: {fmt(amin[1])}}}
  m_AnchorMax: {{x: {fmt(amax[0])}, y: {fmt(amax[1])}}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
""")
    sc.add_doc(cr, f"--- !u!222 &{cr}", f"""
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
""")
    b = sc.body(donor)
    b = sub1(b, r"^  m_GameObject: \{fileID: -?\d+\}$", f"  m_GameObject: {{fileID: {go}}}")
    b = sub1(b, r"^  m_text: .*$", "  m_text: ")
    b = sub1(b, r"^  m_fontColor: \{.*\}$", f"  m_fontColor: {{r: 1, g: 1, b: 1, a: {fmt(alpha)}}}")
    b = sub1(b, r"^  m_enableAutoSizing: \d+$", "  m_enableAutoSizing: 1")
    b = sub1(b, r"^  m_fontSizeMin: [\d.]+$", f"  m_fontSizeMin: {fmt(lo)}")
    b = sub1(b, r"^  m_fontSizeMax: [\d.]+$", f"  m_fontSizeMax: {fmt(hi)}")
    b = sub1(b, r"^  m_HorizontalAlignment: \d+$", f"  m_HorizontalAlignment: {align[0]}")
    b = sub1(b, r"^  m_VerticalAlignment: \d+$", f"  m_VerticalAlignment: {align[1]}")
    b = sub1(b, r"^  m_textAlignment: \d+$", "  m_textAlignment: 65535")
    b = sub1(b, r"^  m_TextWrappingMode: \d+$", "  m_TextWrappingMode: 1")
    b = sub1(b, r"^  m_overflowMode: \d+$", "  m_overflowMode: 1")           # ellipsis
    b = sub1(b, r"^  m_RaycastTarget: \d+$", "  m_RaycastTarget: 0")
    sc.add_doc(tm, f"--- !u!114 &{tm}", b)

    # register the child on the parent's transform, LAST so it draws over the plates
    pb = sc.body(parent_rt)
    if "m_Children: []" in pb:
        pb = pb.replace("  m_Children: []", f"  m_Children:\n  - {{fileID: {rt}}}", 1)
    else:
        pb = sub1(pb, r"(^  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*)",
                  lambda m: m.group(1) + f"  - {{fileID: {rt}}}\n")
    sc.set_body(parent_rt, pb, f"{why}: parent {name}")
    sc.rebuild_maps()
    return go


def bind(sc, comp, field, target_go, why):
    """Point a serialized reference at the TMP component on target_go."""
    tmp = sc.component_with(target_go, guid=TMP_GUID)
    if not tmp:
        return
    set_field(sc, comp, field, f"{{fileID: {tmp}}}", why)


# ---------------------------------------------------------------------------------------------
def author(sc):
    tsm = sc.find_root("ToyboxScreenModal")
    tgc = sc.find_root("ToyboxGameConfigureModal")
    if not tsm or not tgc:
        raise SystemExit("Menu_Main: the two Toy Box windows were not found - run Home Hub Wiring first")

    # ── the toy grid ────────────────────────────────────────────────────────
    grid = sc.walk(tsm, "Explore/GameSelectScrollView/Viewport/GameGrid")
    if grid:
        set_rect(sc, grid, (0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1), "GameGrid")
        set_grid(sc, grid, TOY_CELL, TOY_SPACING, TOY_PADDING, "GameGrid")
        ensure_fitter(sc, grid, "GameGrid")

    card = sc.walk(tsm, "ToyCardTemplate")
    if card:
        make_sliced(sc, card, RIM_SPRITE, "ToyCardTemplate")
        disable_mask(sc, card, "ToyCardTemplate")
        for child, sprite in (("Background", PLATE_SPRITE), ("Border", RIM_SPRITE)):
            go = sc.walk(card, child)
            if go:
                stretch(sc, go, f"ToyCardTemplate/{child}")
                make_sliced(sc, go, sprite, f"ToyCardTemplate/{child}")
        portrait = sc.walk(card, "VesselIcon")
        if portrait:
            set_rect(sc, portrait, *CARD_PORTRAIT, (0, 0), (0, 0), (0.5, 0.5), "ToyCardTemplate/VesselIcon")
            img = sc.component_with(portrait, guid=IMAGE_GUID)
            if img:
                set_field(sc, img, "m_PreserveAspect", 1, "ToyCardTemplate/VesselIcon")
        title = sc.walk(card, "GameTitle")
        if title:
            set_rect(sc, title, *CARD_TITLE, (0, 0), (0, 0), (0.5, 0.5), "ToyCardTemplate/GameTitle")
            set_band(sc, title, 18, 26, "ToyCardTemplate/GameTitle", (1, 512))
            tmp = sc.component_with(title, guid=TMP_GUID)
            if tmp:
                set_field(sc, tmp, "m_overflowMode", 1, "ToyCardTemplate/GameTitle")
        donor = sc.walk(tgc, "ToyVariantTemplate/GameDetail")
        tagline = ensure_label(sc, card, "Tagline", donor, *CARD_TAGLINE, 11, 14, (1, 256), 0.72,
                               "ToyCardTemplate/Tagline")
        section = ensure_label(sc, card, "Section", donor, *CARD_SECTION, 10, 13, (4, 256), 0.6,
                               "ToyCardTemplate/Section")
        toycard = sc.component_with(card, field="taglineText")
        if toycard:
            bind(sc, toycard, "taglineText", tagline, "ToyboxCard.taglineText")
            bind(sc, toycard, "sectionText", section, "ToyboxCard.sectionText")
        set_active(sc, tagline, CARD_LABELS_ACTIVE, "ToyCardTemplate/Tagline")
        set_active(sc, section, CARD_LABELS_ACTIVE, "ToyCardTemplate/Section")

    # ── the variants list ───────────────────────────────────────────────────
    content = sc.walk(tgc, "ConfigurationDetailView/Scroll View/Viewport/Content")
    if content:
        set_grid(sc, content, VARIANT_CELL, VARIANT_SPACING, VARIANT_PADDING, "Variants/Content")

    vt = sc.walk(tgc, "ToyVariantTemplate")
    if vt:
        make_sliced(sc, vt, RIM_SPRITE, "ToyVariantTemplate")
        disable_mask(sc, vt, "ToyVariantTemplate")
        for child, sprite in (("Background", PLATE_SPRITE), ("Border", RIM_SPRITE)):
            go = sc.walk(vt, child)
            if go:
                stretch(sc, go, f"ToyVariantTemplate/{child}")
                make_sliced(sc, go, sprite, f"ToyVariantTemplate/{child}")
        vtitle = sc.walk(vt, "GameTitle")
        if vtitle:
            set_rect(sc, vtitle, *VARIANT_TITLE, (0, 0), (0, 0), (0.5, 0.5), "ToyVariantTemplate/GameTitle")
            set_band(sc, vtitle, 16, 22, "ToyVariantTemplate/GameTitle", (1, 1024))    # left, bottom
            tmp = sc.component_with(vtitle, guid=TMP_GUID)
            if tmp:
                set_field(sc, tmp, "m_overflowMode", 1, "ToyVariantTemplate/GameTitle")
        vdetail = sc.walk(vt, "GameDetail")
        if vdetail:
            set_rect(sc, vdetail, *VARIANT_DETAIL, (0, 0), (0, 0), (0.5, 0.5), "ToyVariantTemplate/GameDetail")
            set_band(sc, vdetail, 12, 14, "ToyVariantTemplate/GameDetail", (1, 256))   # left, top

    # ── the type ────────────────────────────────────────────────────────────
    for root_name, path, lo, hi, label in CONFIGURE_BANDS:
        root = tsm if root_name == "ToyboxScreenModal" else tgc
        go = sc.walk(root, path)
        if not go:
            print(f"  ~ {root_name}/{path}: not found ({label})")
            continue
        set_band(sc, go, lo, hi, f"{path} ({label})")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    sc = Scene(SCENE)
    author(sc)

    if not sc.changes:
        print("OK  toybox layout matches")
        return 0
    if args.check:
        print(f"FAIL: Menu_Main is {len(sc.changes)} edit(s) away from the authored Toy Box layout:",
              file=sys.stderr)
        for c in sc.changes[:40]:
            print(f"  - {c}", file=sys.stderr)
        return 1
    sc.write()
    print(f"wrote {os.path.relpath(SCENE, ROOT)}: {len(sc.changes)} edit(s)")
    for c in sc.changes:
        print(f"  - {c}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
