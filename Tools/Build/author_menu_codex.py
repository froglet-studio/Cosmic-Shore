#!/usr/bin/env python3
"""
Author the CODEX screen of the style-B menu - the top bar's first tab - as prefabs, and place it in Menu_Main
(Docs/MENU_REDESIGN_B.md section 88; the approved Rev 26 boards on the "Menu Redesign B" canvas, Shombith 2026-10-26:
"Go ahead implement the codex I like the design").

    Assets/_Prefabs/UI Elements/Main Menu Screens/Codex/
        CodexTile.prefab   one tile of the grid: fill, outline (painted by CodexTile), the entry's picture, name and
                           a quiet line; a uGUI Button that tints the picture, a CanvasGroup + LayoutElement so the grid
                           closes up around a hidden one
        CodexView.prefab   the whole screen: the sea under the top bar, then (drawn at the Hangar's 0.88) the chapter
                           row (LB, seven KitTextTabs, RB), the grid on the left (title, count, FLORA / FAUNA while
                           ECOLOGY is open, the scrolling tile grid, the empty line) and the open page on the right
                           (picture, kicker, title, then a scroll of body, facts, element cells and rows, each block
                           copied from a hidden CodexLine template); CodexScreen on the root

    Assets/_Scenes/Menu_Main.unity
        One CodexView instance, first child of Screens, and ScreenSwitcher.screens' CODEX entry pointed at its root -
        the slot section 84 left empty (root 0). The CODEX tab's lock is lifted by author_menu_topnav.py (LOCKED).
        Nothing else in the scene is touched.

STANDING DOWN: every output is re-derived on each run; the scene edit removes its own documents and its screens
entry before writing them again, so a re-run is a no-op (--check).

    python3 Tools/Build/author_menu_codex.py              # write
    python3 Tools/Build/author_menu_codex.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_codex.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile_window", os.path.join(HERE, "author_menu_profile_window.py"))
W = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(W)

P, C, A, K, T, H, M = W.P, W.C, W.A, W.K, W.T, W.H, W.M
REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Codex"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, refs = C.TL, C.TR, C.ref, C.mono, C.refs
text, label, le = H.text, H.label, H.le
image_node, art_node = A.image_node, A.art_node

PREFABS = ("CodexTile", "CodexView")
GRID = W.GRID                                       # UnityEngine.UI.GridLayoutGroup
PACKAGE = W.PACKAGE
S = dict(screen=C.script("CodexScreen"), tile=C.script("CodexTile"), page=C.script("CodexPageView"),
         line=C.script("CodexLine"), switcher=C.script("ScreenSwitcher"), tab=K.kit_script("KitTextTab"),
         mask=H.KIT_RECT_MASK_2D)
CODEX_ID = 6                                        # ScreenSwitcher.MenuScreens.CODEX
CAPTAIN_LIST = "Assets/_SO_Assets/Captains/Elemental/ElementalCaptains.asset"   # the Hangar's PILOTS list
VESSEL_LIST = "Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset"           # the Hangar's vessel list
ELEMENTS = ("Charge", "Mass", "Space", "Time")

# Menu_Main, measured 2026-10-26 (names asserted on every run).
SCENE_IDS = dict(screens_rt="2525219157635147276", switcher="4216411968520269688")

# --- layout, in the Hangar's content units (drawn at CONTENT_SCALE under the top bar) ------------------------------
CONTENT_SCALE, TOP_BAR = H.CONTENT_SCALE, H.TOP_BAR
CONTENT_H = round((1080 - TOP_BAR) / CONTENT_SCALE)
SIDE = 40
ORB_CLEAR = H.LIST_BOTTOM                           # the grid stops above the friends orb, bottom left
CHAPTERS = ("Ethirions", "Elements", "Ecology", "Pilots", "Vessels", "Artifacts", "Tools")
STOPS = ("Ethirions", "Elements", "Flora", "Fauna", "Pilots", "Vessels", "Artifacts", "Tools")
TAB_ROLE, TAB_PAD, TAB_GAP, TAB_H = "Body", 18, 22, 52
CHAPTER_TOP = 16
ZONE_TOP = CHAPTER_TOP + TAB_H + 28                 # the grid and the page start under the chapters
GRID_W = 1000
TILE_W, TILE_H, TILE_GAP, COLUMNS = 180, 212, 18, 5
PAGE_W = 1060
PAGE_PAD = 32
PORTRAIT = 220
HEAD_H = PORTRAIT + 24                              # the page's head: the picture, kicker and title beside it
CELL_W, CELL_H, CELL_GAP, CELL_COLUMNS = 232, 150, 16, 4
FACT_LABEL_W = 220
ROW_ICON = 56


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def F(name):
    return K.FileIds(name)


def ystr(value):
    """A serialized string as Unity writes it: non-ASCII double-quoted with escapes, else K.yaml_scalar."""
    v = str(value)
    if any(ord(ch) > 127 for ch in v):
        return '"' + "".join(f"\\u{ord(ch):04X}" if ord(ch) > 127 else ("\\\"" if ch == '"' else ch) for ch in v) + '"'
    return K.yaml_scalar(v)


def str_list(name, values, indent="  "):
    if not values:
        return f"{indent}{name}: []\n"
    return f"{indent}{name}:\n" + "".join(f"{indent}- {ystr(v)}\n" for v in values)


# --------------------------------------------------------------------------------------------
# CodexTile.prefab
# --------------------------------------------------------------------------------------------

def build_tile():
    root = N("root", "CodexTile", R(size=(TILE_W, TILE_H)))
    root.add("cg", 225, H.canvas_group(True)).add("le", 114, le())
    glow = root.child(N("Glow", "Glow", R((0, 0), (1, 1), (0, 0), (18, 18))))
    glow.add("cg", 225, H.canvas_group(False))
    C.plain_image(glow, "Glow/Image", "Image", STRETCH, tok("Selection", 0.35), K.kit_sprite("kit_sliver14_fill"), preserve=False)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.92), K.kit_sprite("kit_sliver14_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceVeryDark", 0.92))
    # The outline is painted by CodexTile (the selection blue on the open tile), so it carries no KitThemedGraphic.
    outline = root.child(N("Frame", "Frame", STRETCH))
    outline.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver14_line1"), True))
    art_node(root, "Art", "Art", TL(14, 12, TILE_W - 28, TILE_H - 76), None, preserve=True)
    H.ellipsize(label(root.child(N("Name", "Name", TL(14, TILE_H - 60, TILE_W - 28, 26))), "Entry", "Body", "TextLight"))
    H.ellipsize(label(root.child(N("Sub", "Sub", TL(14, TILE_H - 32, TILE_W - 28, 20))), "Line", "Label", "NeutralLightest"))
    root.add("button", 114, C.tint_button("Art", nav=3, normal=0.82))
    root.add("tile", 114, lambda ctx, go: mono(go, S["tile"],
             f"  button: {ref(ctx.c('root', 'button'))}\n  art: {ref(ctx.c('Art', 'img'))}\n"
             f"  nameText: {ref(ctx.c('Name', 'tmp'))}\n  subText: {ref(ctx.c('Sub', 'tmp'))}\n"
             f"  frame: {ref(ctx.c('Frame', 'img'))}\n  glow: {ref(ctx.c('Glow', 'cg'))}\n"
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n  restToken: {K.TOKEN_INDEX['SurfaceLight']}\n"
             f"  selectedToken: {K.TOKEN_INDEX['Selection']}\n"))
    return root


# --------------------------------------------------------------------------------------------
# CodexView.prefab
# --------------------------------------------------------------------------------------------

def chapter_row(content):
    row = content.child(N("Chapters", "Chapters", R((0, 1), (1, 1), (0, -CHAPTER_TOP), (-2 * SIDE, TAB_H), (0.5, 1))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), TAB_GAP, 3, (0, 0), (0, 0)))
    row.children.append(H.keycap("ChaptersLeftKeycap", "LB"))
    for name in CHAPTERS:
        H.text_tab(row, f"Chapters/{name}", name, name.upper(), TAB_ROLE, K.label_width(name.upper(), TAB_ROLE) + TAB_PAD, TAB_H)
    row.children.append(H.keycap("ChaptersRightKeycap", "RB"))
    width = sum(K.label_width(n.upper(), TAB_ROLE) + TAB_PAD for n in CHAPTERS) + 2 * 40 + TAB_GAP * (len(CHAPTERS) + 1)
    assert width <= GRID_W + PAGE_W, f"the chapter row is wider than the screen's zones ({width})"
    return width


def grid_zone(content):
    # Placed by its top-left corner: x SIDE, y ZONE_TOP down, as tall as the content less ZONE_TOP and the orb's clearance.
    z = content.child(N("Grid", "Grid", R((0, 0), (0, 1), (SIDE, -ZONE_TOP), (GRID_W, -(ZONE_TOP + ORB_CLEAR)), (0, 1))))
    z.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    label(z.child(N("Grid/Title", "Title", TL(0, 0, 300, 34))), "Ethirions", "H3", "TextLight")
    label(z.child(N("Grid/Count", "Count", TR(0, 6, 260, 24))), "5 entries", "Label", "NeutralLightest", align="right")
    eco = z.child(N("Grid/Ecology", "EcologyTabs", TL(260, -9, 320, TAB_H)))
    eco.add("cg", 225, H.canvas_group(False))
    eco.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), TAB_GAP, 3, (0, 0), (0, 0)))
    for name in ("Flora", "Fauna"):
        H.text_tab(eco, f"Grid/Ecology/{name}", name, name.upper(), "BodySmall", K.label_width(name.upper(), "BodySmall") + 12, TAB_H)

    sv = z.child(N("Grid/Scroll", "Scroll", R((0, 0), (1, 1), (0, -24), (0, -48))))
    vp = sv.child(N("Grid/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, lambda ctx, go: K.mono(go, S["mask"], "  m_Padding: {x: -20, y: -20, z: -20, w: -20}\n"
                                                              "  m_Softness: {x: 0, y: 0}\n"))
    gc = vp.child(N("Grid/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    gc.add("grid", 114, lambda ctx, go: mono(go, GRID,
           "  m_Padding:\n    m_Left: 4\n    m_Right: 4\n    m_Top: 6\n    m_Bottom: 12\n"
           "  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n"
           f"  m_CellSize: {{x: {TILE_W}, y: {TILE_H}}}\n  m_Spacing: {{x: {TILE_GAP}, y: {TILE_GAP}}}\n"
           f"  m_Constraint: 1\n  m_ConstraintCount: {COLUMNS}\n")).add("csf", 114, K.fitter(0, 2))
    sv.add("scroll", 114, lambda ctx, go: K.mono(go, A.SCROLL_RECT,
           f"  m_Content: {ref(ctx.rt('Grid/Scroll/Viewport/Content'))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
           "  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
           f"  m_ScrollSensitivity: 30\n  m_Viewport: {ref(ctx.rt('Grid/Scroll/Viewport'))}\n"
           "  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n"
           "  m_HorizontalScrollbarVisibility: 2\n  m_VerticalScrollbarVisibility: 2\n"
           "  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n"
           "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    empty = z.child(N("Grid/Empty", "Empty", R((0, 0.5), (1, 0.5), (0, 40), (-120, 120))))
    empty.add("cg", 225, H.canvas_group(False))
    label(empty, "Artifacts are found far out in the HyperSea. None found yet.", "Body", "NeutralLightest", align="center", wrap=True)


def line_template(parent, key, kind):
    """A CodexLine template: a fact (label | value), a cell (icon, name, stats) or a row (icon | name over a line)."""
    n = parent.child(N(key, "Template", R(size=(0, 0) if kind != "cell" else (CELL_W, CELL_H))))
    n.add("cg", 225, H.canvas_group(False)).add("le", 114, le())
    if kind == "fact":
        n.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 16, 0, (1, 1), (0, 0)))
        t = n.child(N(f"{key}/Title", "Label", R(size=(FACT_LABEL_W, 22))))
        t.add("le", 114, le(mw=FACT_LABEL_W, pw=FACT_LABEL_W))
        label(t, "Label", "Label", "NeutralLightest", valign="top", wrap=True)
        v = n.child(N(f"{key}/Line", "Value", R(size=(400, 22))))
        v.add("le", 114, le(fw=1))
        label(v, "Value", "Body", "TextLight", valign="top", wrap=True)
        return n, None, f"{key}/Title", f"{key}/Line"
    if kind == "cell":
        C.frame(n, f"{key}/Frame", 10, fill_alpha=0.92)
        art_node(n, f"{key}/Icon", "Icon", TL(12, 12, 44, 44), None, preserve=True)
        H.ellipsize(label(n.child(N(f"{key}/Title", "Title", TL(64, 20, CELL_W - 76, 28))), "Charge", "Body", "TextLight"))
        label(n.child(N(f"{key}/Line", "Lines", TL(12, 64, CELL_W - 24, CELL_H - 72))), "Stat: value", "BodySmall",
              "NeutralLightest", valign="top", wrap=True)
        return n, f"{key}/Icon", f"{key}/Title", f"{key}/Line"
    n.add("hlg", 114, K.hv_layout(False, (0, 0, 2, 2), 16, 3, (1, 1), (0, 0)))
    icon = n.child(N(f"{key}/Icon", "Icon", R(size=(ROW_ICON, ROW_ICON))))
    icon.add("le", 114, le(mw=ROW_ICON, mh=ROW_ICON, pw=ROW_ICON, ph=ROW_ICON))
    icon.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), None, preserve=True))
    col = n.child(N(f"{key}/Column", "Column", R(size=(400, ROW_ICON))))
    col.add("le", 114, le(fw=1)).add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 2, 0, (1, 1), (1, 0)))
    label(col.child(N(f"{key}/Title", "Title", R(size=(400, 26)))), "Title", "Body", "TextLight", wrap=True)
    label(col.child(N(f"{key}/Line", "Line", R(size=(400, 22)))), "Line", "BodySmall", "NeutralLightest", valign="top", wrap=True)
    return n, f"{key}/Icon", f"{key}/Title", f"{key}/Line"


def block(content, key, name, cells=False):
    b = content.child(N(key, name, R(size=(0, 0))))
    b.add("cg", 225, H.canvas_group(True)).add("le", 114, le())
    b.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 8, 0, (1, 1), (1, 0)))
    label(b.child(N(f"{key}/Heading", "Heading", R(size=(0, 22)))), name, "Label", "TextLight")
    holder = b
    if cells:
        holder = b.child(N(f"{key}/Cells", "Cells", R(size=(0, CELL_H))))
        holder.add("grid", 114, lambda ctx, go: mono(go, GRID,
                   "  m_Padding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 0\n    m_Bottom: 0\n"
                   "  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n"
                   f"  m_CellSize: {{x: {CELL_W}, y: {CELL_H}}}\n  m_Spacing: {{x: {CELL_GAP}, y: {CELL_GAP}}}\n"
                   f"  m_Constraint: 1\n  m_ConstraintCount: {CELL_COLUMNS}\n"))
        # No ContentSizeFitter: the block's layout group sizes the grid from its preferred height (a fitter under a
        # layout group is the "child of a layout group should not have a Content Size Fitter" warning).
    return b, holder


def page_zone(content):
    # Placed by its top-right corner: SIDE in from the right, y ZONE_TOP down, 40 clear of the foot.
    z = content.child(N("Page", "Page", R((1, 0), (1, 1), (-SIDE, -ZONE_TOP), (PAGE_W, -(ZONE_TOP + 40)), (1, 1))))
    z.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    C.frame(z, "Page/Frame", 14, fill_alpha=0.9)
    M.bracket_nodes(z, "Page/Bracket", inset=14)
    art_node(z, "Page/Portrait", "Portrait", TL(PAGE_PAD, PAGE_PAD, PORTRAIT, PORTRAIT), None, preserve=True)
    x = PAGE_PAD + PORTRAIT + 28
    label(z.child(N("Page/Kicker", "Kicker", R((0, 1), (1, 1), ((x - PAGE_PAD) / 2, -(PAGE_PAD + 30)), (-(x + PAGE_PAD), 20), (0.5, 1)))),
          "Ethirion", "Label", "NeutralLightest")
    H.ellipsize(label(z.child(N("Page/Title", "Title", R((0, 1), (1, 1), ((x - PAGE_PAD) / 2, -(PAGE_PAD + 54)), (-(x + PAGE_PAD), 60), (0.5, 1)))),
                      "Charge", "H1", "TextLight", valign="bottom"))
    sv = z.child(N("Page/Scroll", "Scroll", R((0, 0), (1, 1), (0, -(PAGE_PAD + HEAD_H) / 2), (-2 * PAGE_PAD, -(2 * PAGE_PAD + HEAD_H)))))
    vp = sv.child(N("Page/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, lambda ctx, go: K.mono(go, S["mask"], "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n"))
    pc = vp.child(N("Page/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    pc.add("vlg", 114, K.hv_layout(True, (0, 8, 0, 24), 22, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    label(pc.child(N("Page/Body", "Body", R(size=(0, 26)))), "", "Body", "TextLight", valign="top", wrap=True)
    keys = {}
    for bkey, bname, kind in (("Page/Facts", "Facts", "fact"), ("Page/Cells", "By element", "cell"), ("Page/Rows", "Rows", "row")):
        b, holder = block(pc, bkey, bname, cells=(kind == "cell"))
        tkey = f"{holder.key}/Template"
        keys[kind] = line_template(holder, tkey, kind)
    sv.add("scroll", 114, lambda ctx, go: K.mono(go, A.SCROLL_RECT,
           f"  m_Content: {ref(ctx.rt('Page/Scroll/Viewport/Content'))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
           "  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
           f"  m_ScrollSensitivity: 30\n  m_Viewport: {ref(ctx.rt('Page/Scroll/Viewport'))}\n"
           "  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n"
           "  m_HorizontalScrollbarVisibility: 2\n  m_VerticalScrollbarVisibility: 2\n"
           "  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n"
           "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    for kind, (n, icon, title, line) in keys.items():
        n.add("line", 114, (lambda i, t, l: lambda ctx, go: mono(go, S["line"],
              f"  group: {ref(ctx.c(n_key(t), 'cg'))}\n"
              f"  icon: {ref(ctx.c(i, 'img') if i else 0)}\n"
              f"  title: {ref(ctx.c(t, 'tmp'))}\n  line: {ref(ctx.c(l, 'tmp'))}\n"))(icon, title, line))
    z.add("page", 114, lambda ctx, go: mono(go, S["page"], page_body(ctx)))


def n_key(child_key):
    """The template's own key from one of its parts' keys (".../Template/Title" -> ".../Template")."""
    return child_key.rsplit("/", 1)[0]


def page_body(ctx):
    t = lambda b: f"Page/{b}" + ("/Cells" if b == "Cells" else "") + "/Template"
    return (f"  pageGroup: {ref(ctx.c('Page', 'cg'))}\n  portrait: {ref(ctx.c('Page/Portrait', 'img'))}\n"
            f"  kickerText: {ref(ctx.c('Page/Kicker', 'tmp'))}\n  titleText: {ref(ctx.c('Page/Title', 'tmp'))}\n"
            f"  bodyText: {ref(ctx.c('Page/Body', 'tmp'))}\n  scroll: {ref(ctx.c('Page/Scroll', 'scroll'))}\n"
            f"  factsBlock: {ref(ctx.c('Page/Facts', 'cg'))}\n  factsHeading: {ref(ctx.c('Page/Facts/Heading', 'tmp'))}\n"
            f"  factTemplate: {ref(ctx.c(t('Facts'), 'line'))}\n"
            f"  cellsBlock: {ref(ctx.c('Page/Cells', 'cg'))}\n  cellsHeading: {ref(ctx.c('Page/Cells/Heading', 'tmp'))}\n"
            f"  cellTemplate: {ref(ctx.c(t('Cells'), 'line'))}\n"
            f"  rowsBlock: {ref(ctx.c('Page/Rows', 'cg'))}\n  rowsHeading: {ref(ctx.c('Page/Rows/Heading', 'tmp'))}\n"
            f"  rowTemplate: {ref(ctx.c(t('Rows'), 'line'))}\n")


WORDS = (  # CodexWords, in declaration order: (field, value)
    ("elementNames", list(ELEMENTS)),
    ("elementLines", ["The strike you carry", "What you build", "How far you reach", "How fast you move"]),
    ("elementBody", "Skim {element} ethirions or fly the Element Charger to raise it. Its ability wakes at level 5, and so does the hull's level-5 upgrade."),
    ("elementKicker", "Element"),
    ("elementRowsHeading", "On every vessel"),
    ("elementRowTemplate", "{ability} - level 5: {upgrade}"),
    ("elementRowNoUpgrade", "{ability}"),
    ("openSlot", "Open slot"),
    ("ethirionKicker", "Ethirion"),
    ("floraKicker", "Flora"),
    ("faunaKicker", "Fauna"),
    ("toolKicker", "Tool - {group}"),
    ("vesselKicker", "Vessel"),
    ("pilotKicker", "{element} pilot - {vessel}"),
    ("factsHeading", "Facts"),
    ("cellsHeading", "By element"),
    ("cellLineTemplate", "{label}: {value}"),
    ("cellLines", 3),
    ("lawsHeading", "Elemental laws"),
    ("abilitiesHeading", "Abilities"),
    ("abilityLineTemplate", "Level 5: {upgrade}"),
    ("toolVariantsHeading", "What it offers"),
    ("pilotsHeading", "Pilots"),
    ("quoteTemplate", "“{quote}”"),
    ("signatureLabel", "Signature"),
    ("fliesLikeLabel", "Flies like"),
    ("answersToLabel", "Answers to"),
    ("answersToTemplate", "{element} - {ability}"),
    ("upgradeLabel", "Level-5 upgrade"),
)
STOP_TITLES = list(STOPS)
EMPTY_LINES = ["", "", "", "", "", "", "Artifacts are found far out in the HyperSea. None found yet.", ""]


def words_block():
    out = "  words:\n"
    for field, value in WORDS:
        if isinstance(value, list):
            out += str_list(field, value, "    ")
        else:
            out += f"    {field}: {ystr(value)}\n"
    return out


def screen_body(ctx):
    petals = [K.ref(21300000, H.PETAL_SPRITE[e], 3) for e in ELEMENTS]
    tile = K.ref(F("CodexTile")("root#tile"), asset_guid("CodexTile"))
    return (f"  captains: {K.ref(11400000, K.meta_guid(CAPTAIN_LIST), 2)}\n"
            f"  vessels: {K.ref(11400000, K.meta_guid(VESSEL_LIST), 2)}\n"
            "  elementPetals:\n" + "".join(f"  - {p}\n" for p in petals)
            + words_block()
            + str_list("stopTitles", STOP_TITLES)
            + f"  countTemplate: {ystr('{count} entries')}\n"
            + str_list("emptyLines", EMPTY_LINES)
            + refs("chapterTabs", [ctx.c(f"Chapters/{n}", "tab") for n in CHAPTERS])
            + f"  ecologyTabsGroup: {ref(ctx.c('Grid/Ecology', 'cg'))}\n"
            f"  floraTab: {ref(ctx.c('Grid/Ecology/Flora', 'tab'))}\n  faunaTab: {ref(ctx.c('Grid/Ecology/Fauna', 'tab'))}\n"
            "  shoulderInput: 1\n"
            f"  gridTitle: {ref(ctx.c('Grid/Title', 'tmp'))}\n  gridCount: {ref(ctx.c('Grid/Count', 'tmp'))}\n"
            f"  gridScroll: {ref(ctx.c('Grid/Scroll', 'scroll'))}\n  gridContent: {ref(ctx.rt('Grid/Scroll/Viewport/Content'))}\n"
            f"  tilePrefab: {tile}\n"
            f"  emptyGroup: {ref(ctx.c('Grid/Empty', 'cg'))}\n  emptyText: {ref(ctx.c('Grid/Empty', 'tmp'))}\n"
            f"  page: {ref(ctx.c('Page', 'page'))}\n")


def build_view():
    root = N("root", "CodexView", STRETCH)
    # The Hangar's backdrop exactly: the near-opaque veil and the sea lines over it, under the top bar.
    veil = root.child(N("Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84))))
    veil.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", H.WINDOW_BACKDROP_ALPHA), raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", H.WINDOW_BACKDROP_ALPHA))
    sea = root.child(N("Sea", "Sea", R((0, 0), (1, 1), (0, -42), (0, -84))))
    sea.add("cr", 222, K.canvas_renderer()).add("img", 114, M.lattice_image(M.SEA_WINDOW_MAT))
    frame = root.child(N("Frame", "Frame", R((0, 0), (1, 1), (0, -TOP_BAR / 2), (0, -TOP_BAR))))
    k = 0.5 / CONTENT_SCALE
    content = frame.child(N("Frame/Content", "Content", R((0.5 - k, 1 - 1 / CONTENT_SCALE), (0.5 + k, 1), (0, 0), (0, 0), (0.5, 1))))
    content.scale = CONTENT_SCALE
    # Hierarchy is focus order (ScreenSwitcher.Refocus takes the first live Selectable): the grid first, so the pad
    # lands on a tile; then the page; the chapter row last.
    grid_zone(content)
    page_zone(content)
    chapter_row(content)
    root.add("screen", 114, lambda ctx, go: mono(go, S["screen"], screen_body(ctx)))
    return root


FOLDER_META = ("fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def prefab_outputs():
    # The folder's own .meta, so Unity does not mint one with a random guid on every machine.
    out = {OUT_DIR + ".meta": FOLDER_META.format(g=asset_guid("CodexFolder"))}
    for name, build in (("CodexTile", build_tile), ("CodexView", build_view)):
        out[prefab_path(name)] = P.prefab_text(build(), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    return out


# --------------------------------------------------------------------------------------------
# Menu_Main: the instance under Screens, and the CODEX slot pointed at it
# --------------------------------------------------------------------------------------------

def scene_ctx():
    return P.Ctx("Menu_Main/CodexView")


def scene_ids():
    ctx = scene_ctx()
    return str(ctx.fid("Codex#inst")), str(ctx.fid("Codex#stub"))


def screens_entry(root_fid):
    return f"  - id: {CODEX_ID}\n    root: {{fileID: {root_fid}}}\n"


def scene_transform(text_):
    sc = T.Scene(text_)
    I = SCENE_IDS
    assert sc.script(I["switcher"]) == S["switcher"], "Menu_Main's ScreenSwitcher moved"
    scr = sc.docs[I["screens_rt"]]
    assert sc.field(sc.go_of(I["screens_rt"]), "m_Name") == "Screens", "Menu_Main's Screens container moved"
    inst, stub = scene_ids()
    owned = C.instance_docs(sc, inst) if sc.has(inst) else []
    for fid in list(owned) + [stub]:
        if sc.has(fid):
            sc.remove(fid)
    out = []
    K.emit_instance(scene_ctx(), K.Instance("Codex", "CodexView", asset_guid("CodexView"), "CodexView", STRETCH, []),
                    I["screens_rt"], out)
    kids = [k for k in re.findall(r"^  - \{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", scr, re.M).group(1), re.M)
            if k != stub]
    # File order: the new documents go just before the first existing child's documents.
    anchor = I["screens_rt"]
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(I["screens_rt"], T.set_block(scr, "m_Children", K.refs_list("m_Children", [stub] + kids)))
    sw = sc.docs[I["switcher"]]
    old = re.search(r"  - id: %d\n    root: \{fileID: -?\d+\}\n" % CODEX_ID, sw)
    assert old, "ScreenSwitcher.screens has no CODEX entry (author_menu_topnav.py writes it)"
    sc.replace(I["switcher"], sw.replace(old.group(0), screens_entry(stub), 1))
    return sc.text()


def outputs():
    files = prefab_outputs()
    files[SCENE] = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def validate(files):
    guids = T.known_guids() | PACKAGE | {asset_guid(n) for n in PREFABS}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for name in PREFABS:
        A.check_file(prefab_path(name), files[prefab_path(name)], guids, minted)

    tile = files[prefab_path("CodexTile")]
    _, tbody = A.doc_of(tile, F("CodexTile")("root#tile"))
    assert tbody and S["tile"] in tbody, "CodexTile.prefab: the root carries no CodexTile"
    for field, want in (("button", A.BUTTON), ("art", K.IMAGE), ("nameText", K.TMP_UGUI), ("subText", K.TMP_UGUI),
                        ("frame", K.IMAGE), ("glow", 225)):
        C.wired(tile, tbody, field, want, "CodexTile")
    frame_go = F("CodexTile")("Frame#go")
    assert not re.search(r"m_GameObject: \{fileID: %s\}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: \{fileID: 11500000, guid: %s"
                         % (frame_go, K.kit_script("KitThemedGraphic")), tile), \
        "CodexTile: the outline must not carry a KitThemedGraphic (it would repaint the selection colour)"
    assert "  m_Alpha: 0\n" in A.doc_of(tile, F("CodexTile")("Glow#cg"))[1], "CodexTile: the glow must start hidden"

    view = files[prefab_path("CodexView")]
    V = F("CodexView")
    _, sbody = A.doc_of(view, V("root#screen"))
    assert sbody and S["screen"] in sbody, "CodexView.prefab: the root carries no CodexScreen"
    tabs = re.findall(r"^  - \{fileID: (-?\d+)\}", sbody.split("  chapterTabs:")[1].split("  ecologyTabsGroup:")[0], re.M)
    assert tabs == [str(V(f"Chapters/{n}#tab")) for n in CHAPTERS], "CodexScreen.chapterTabs must be the seven tabs, in order"
    for field, want in (("ecologyTabsGroup", 225), ("floraTab", S["tab"]), ("faunaTab", S["tab"]), ("gridTitle", K.TMP_UGUI),
                        ("gridCount", K.TMP_UGUI), ("gridScroll", A.SCROLL_RECT), ("gridContent", 224), ("emptyGroup", 225),
                        ("emptyText", K.TMP_UGUI), ("page", S["page"])):
        C.wired(view, sbody, field, want, "CodexScreen")
    assert f"  tilePrefab: {K.ref(F('CodexTile')('root#tile'), asset_guid('CodexTile'))}\n" in sbody, "CodexScreen.tilePrefab is not CodexTile"
    assert sbody.count(f"guid: {K.meta_guid(CAPTAIN_LIST)}") == 1 and sbody.count(f"guid: {K.meta_guid(VESSEL_LIST)}") == 1, \
        "CodexScreen must read the Hangar's pilots and vessel lists"
    for e in ELEMENTS:
        assert f"guid: {H.PETAL_SPRITE[e]}" in sbody, f"CodexScreen.elementPetals is missing {e}"
    titles = re.search(r"^  stopTitles:\n((?:  - .*\n)*)", sbody, re.M)
    assert titles and len(re.findall(r"^  - ", titles.group(1), re.M)) == len(STOPS), "CodexScreen.stopTitles needs one title per stop"
    assert "Artifacts are found far out in the HyperSea" in sbody, "the empty ARTIFACTS stop must say where artifacts come from"

    _, pbody = A.doc_of(view, V("Page#page"))
    assert pbody and S["page"] in pbody, "the Page carries no CodexPageView"
    for field, want in (("pageGroup", 225), ("portrait", K.IMAGE), ("kickerText", K.TMP_UGUI), ("titleText", K.TMP_UGUI),
                        ("bodyText", K.TMP_UGUI), ("scroll", A.SCROLL_RECT), ("factsBlock", 225), ("factTemplate", S["line"]),
                        ("cellsBlock", 225), ("cellTemplate", S["line"]), ("rowsBlock", 225), ("rowTemplate", S["line"])):
        C.wired(view, pbody, field, want, "CodexPageView")
    for tkey in ("Page/Facts/Template", "Page/Cells/Cells/Template", "Page/Rows/Template"):
        _, cg = A.doc_of(view, V(f"{tkey}#cg"))
        assert cg and "  m_Alpha: 0\n" in cg, f"{tkey}: a line template must start hidden"
        _, lbody = A.doc_of(view, V(f"{tkey}#line"))
        assert lbody and f"  group: {{fileID: {V(tkey + '#cg')}}}\n" in lbody, f"{tkey}: its CodexLine must show and hide its own group"
    _, eco = A.doc_of(view, V("Grid/Ecology#cg"))
    assert "  m_Alpha: 0\n" in eco, "FLORA / FAUNA must start hidden (shown only while ECOLOGY is open)"
    content_rt = A.doc_of(view, V("Frame/Content#rt"))[1]
    assert f"m_LocalScale: {{x: {K.f32(CONTENT_SCALE)}, y: {K.f32(CONTENT_SCALE)}, z: 1}}" in content_rt, \
        "the Codex is drawn at the Hangar's scale"
    order = re.search(r"^  m_Children:\n((?:  - .*\n)*)", content_rt, re.M).group(1)
    kids = re.findall(r"fileID: (-?\d+)", order)
    assert kids[:2] == [str(V("Grid#rt")), str(V("Page#rt"))], "focus order: the grid first, then the page, the chapters last"

    sc_text = files[SCENE]
    inst, stub = scene_ids()
    assert sc_text.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid('CodexView')}") == 1, \
        "Menu_Main must hold exactly one CodexView instance"
    sc = T.Scene(sc_text)
    I = SCENE_IDS
    assert f"m_TransformParent: {{fileID: {I['screens_rt']}}}" in sc.docs[inst], "CodexView is not under Screens"
    kids = re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[I["screens_rt"]], re.M).group(1)
    assert kids.count(f"{{fileID: {stub}}}") == 1, "Screens does not list the CodexView"
    sw = sc.docs[I["switcher"]]
    assert screens_entry(stub) in sw, "ScreenSwitcher's CODEX slot is not the CodexView"
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", sc_text, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", sc_text):
        assert m.group(1) in anchors, f"Menu_Main: dangling local fileID {m.group(1)}"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def self_test(files):
    def expect(label_, mutate):
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
        except AssertionError as e:
            print(f"  caught: {label_} ({e})")
            return True
        print(f"  MISSED: {label_}")
        return False
    view, tile = prefab_path("CodexView"), prefab_path("CodexTile")
    inst, stub = scene_ids()
    V = F("CodexView")
    cases = [
        ("the CODEX slot left empty", lambda f: f.__setitem__(SCENE, f[SCENE].replace(screens_entry(stub), screens_entry(0), 1))),
        ("a chapter tab dropped", lambda f: f.__setitem__(view, f[view].replace(f"  - {{fileID: {V('Chapters/Pilots#tab')}}}\n", "", 1))),
        ("the page unwired", lambda f: f.__setitem__(view, re.sub(r"(  page: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("a fact template left visible", lambda f: f.__setitem__(view, re.sub(
            r"(--- !u!225 &%s\nCanvasGroup:\n(?:  .*\n)*?  m_Alpha: )0" % V("Page/Facts/Template#cg"), r"\g<1>1", f[view], count=1))),
        ("the outline repainted by a theme", lambda f: f.__setitem__(tile, f[tile] + (
            f"--- !u!114 &1\nMonoBehaviour:\n  m_GameObject: {{fileID: {F('CodexTile')('Frame#go')}}}\n  m_Enabled: 1\n"
            f"  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {K.kit_script('KitThemedGraphic')}, type: 3}}\n"))),
        ("the pilots read from no list", lambda f: f.__setitem__(view, re.sub(r"(  captains: )\{fileID: \d+, guid: \w+, type: 2\}", r"\1{fileID: 0}", f[view], count=1))),
        ("FLORA / FAUNA shown on every chapter", lambda f: f.__setitem__(view, re.sub(
            r"(--- !u!225 &%s\nCanvasGroup:\n(?:  .*\n)*?  m_Alpha: )0" % V("Grid/Ecology#cg"), r"\g<1>1", f[view], count=1))),
        ("the chapters before the grid in focus order", lambda f: f.__setitem__(view, f[view].replace(
            f"  m_Children:\n  - {{fileID: {V('Grid#rt')}}}\n  - {{fileID: {V('Page#rt')}}}\n",
            f"  m_Children:\n  - {{fileID: {V('Page#rt')}}}\n  - {{fileID: {V('Grid#rt')}}}\n", 1))),
    ]
    ok = all(expect(l, m) for l, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print("OK: CodexTile.prefab, CodexView.prefab and the Menu_Main CODEX slot validate")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        full = os.path.join(REPO, path)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
