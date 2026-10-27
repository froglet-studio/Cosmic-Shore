#!/usr/bin/env python3
"""
Author ITEM 7 of the style-B menu redesign - the ARK screen's ELEMENTAL CONVERTER overlay - as a prefab,
and mount it in Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 16; the approved Ark / ArkConverting
boards, revision 2, on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Ark/
        ArkView.prefab   the Ark's four tabs (ArkTabsView: CONVERTER, SQUADS, DECOR - Shombith, 2026-10-27; ARTIFACTS -
                         Garrett, 2026-10-27) and
                         their pages:
                         CONVERTER, the overlay over the converter's 3D picture: the title band, the recipe panel
                         (four ConverterRecipeRows in fly-in order, the Omni block), and the converter bar
                         (the run's clock, four segments with their sparks, CONVERT with the sweep);
                         SQUADS (SquadsPage): the player's squads (up to four, made and joined), the selected
                         squad's three squad mates with their vessel, element, pilot and artifact, NEW / JOIN /
                         DELETE, PLAY into Arcade / Arena / Mission, and the join list of friends' open seats;
                         ARTIFACTS (ArkArtifactsPage): the player's artifacts, the picked one's four element tracks
                         with RAISE (its own crystals) and OMNI, and the profile's five crystal balances;
                         DECOR: the banner and hull skins, LOCKED (MenuAvailabilityView) - not open yet

    Menu_Main.unity, `ArkScreen/ElementalConverter` (author_elemental_converter.py made it):
        One ArkView instance is mounted under the converter, which fills ArkScreen exactly (ScreenSwitcher
        lays the screen out at the full safe height, under the translucent nav);
        ElementalConverterView's style-B fields are wired into the view, which switches the first cut's
        code-built title and button off. The picture stays built in code, behind the overlay; its arkTabs
        field names the view's ArkTabsView (it hides the picture while another tab is up).

The documents this script owns are re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_ark.py              # write
    python3 Tools/Build/author_menu_ark.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_ark.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile", os.path.join(HERE, "author_menu_profile.py"))
P = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(P)

C, A, K, T, H, M = P.C, P.A, P.K, P.T, P.H, P.M
REPO = K.REPO
SCENE = P.SCENE
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Ark"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, refs = C.TL, C.TR, C.ref, C.mono, C.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node = A.image_node, A.art_node

VIEW = "ArkView"
S = dict(view=C.script("ElementalConverterView"), row=C.script("ConverterRecipeRow"),
         button=K.kit_script("KitButton"), kt=K.kit_script("KitText"), entrance=K.kit_script("KitEntrance"),
         ambient=K.kit_script("KitAmbient"), spark=K.kit_script("KitSpark"), tabs=C.script("ArkTabsView"),
         squads=C.script("SquadsPage"), squad_row=C.script("SquadRowView"), mate=C.script("SquadMateCard"),
         availability=C.script("MenuAvailabilityView"), text_tab=K.kit_script("KitTextTab"),
         chip=C.script("SquadPickChip"), nanobots=C.script("SquadNanobotSwap"), artifacts=C.script("ArkArtifactsPage"))
C.STUB_KIND.update({"recipe": (114, S["row"]), "entrance": (114, S["entrance"]), "tabs": (114, S["tabs"])})

ORDER = ("Space", "Charge", "Mass", "Time")       # OmniCombineCore.Order: the fly-in order
CRYSTAL = {k: K.meta_guid(f"Assets/_Graphics/Codex/ethirion_{k.lower()}.png") for k in ORDER}
OMNI = K.meta_guid("Assets/_Graphics/Codex/ethirion_omni.png")
# The converter fills its screen exactly. It used to grow 170.4 upward to cancel ArkScreen's authored top inset,
# but ScreenSwitcher.LayoutScreensToViewport lays every screen out at the full safe height at runtime, so the
# grow pushed the title and the recipe panel up under the nav (2026-10-17, the owner: "Ark screen is messed up").
TOP_GROW = 0


def c(node_key, comp):
    return lambda ctx: ctx.c(node_key, comp)


def filled_image(color):
    """An Image, Filled Horizontal from the left, empty."""
    base = K.image(color)
    return lambda ctx, go: base(ctx, go).replace("  m_Type: 0\n", "  m_Type: 3\n").replace(
        "  m_FillMethod: 4\n", "  m_FillMethod: 0\n").replace("  m_FillAmount: 1\n", "  m_FillAmount: 0\n")


def title_band(root, key="Title", heading="Elemental converter",
               line="Space, Charge, Mass and Time combine into one omni crystal."):
    band = root.child(N(key, "Title", TL(40, 108, 514, 116)))
    band.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise"))
    H.framed(band, key, 14, "SurfaceBlack", "SurfaceVeryDark")
    label(band.child(N(f"{key}/Eyebrow", "Eyebrow", TL(22, 22, 300, 16))), "Ark", "Label", "NeutralLightest")
    label(band.child(N(f"{key}/Heading", "Heading", TL(22, 38, 480, 44))), heading, "H1", "TextLight")
    label(band.child(N(f"{key}/Line", "Line", TL(22, 84, 480, 20))), line, "BodySmall", "NeutralLightest")


def recipe_row(panel, i, name):
    k = f"Recipe/{name}"
    row = panel.child(N(k, name, TL(26, 58 + i * 104, 386, 92)))
    fill = row.child(N(f"{k}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True))
    bord = row.child(N(f"{k}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    label(row.child(N(f"{k}/Numeral", "Numeral", TL(14, 22, 64, 48))), f"{i + 1:02d}", "H1", "NeutralLightest")
    art = art_node(row, f"{k}/Art", "Art", TL(84, 18, 56, 56), CRYSTAL[name], preserve=True)
    art.add("bob", 114, lambda ctx, go: M.ambient("Float", period=1.2, amplitude=4)(ctx, go).replace(
        "  m_Enabled: 1\n", "  m_Enabled: 0\n", 1))
    label(row.child(N(f"{k}/Name", "Name", TL(158, 18, 150, 30))), name, "H3", "TextLight")
    st = row.child(N(f"{k}/State", "State", TR(14, 24, 110, 16)))
    label(st, "Ready", "Label", "NeutralLightest", align="right")
    image_node(row, f"{k}/Track", "Track", TL(158, 58, 214, 3), "SurfaceDark")
    bar = row.child(N(f"{k}/Bar", "Bar", TL(158, 58, 214, 3)))
    bar.add("cr", 222, K.canvas_renderer()).add("img", 114, filled_image(tok("ControlActive")))
    row.add("row", 114, lambda ctx, go: mono(go, S["row"],
            f"  theme: {K.ref(11400000, K.THEME, 2)}\n  stateText: {ref(ctx.c(f'{k}/State', 'kt'))}\n"
            f"  fill: {ref(ctx.c(f'{k}/Bar', 'img'))}\n  border: {ref(ctx.c(f'{k}/Border', 'img'))}\n"
            f"  art: {ref(ctx.c(f'{k}/Art', 'img'))}\n  bob: {ref(ctx.c(f'{k}/Art', 'bob'))}\n"))


def recipe_panel(root):
    pn = root.child(N("Recipe", "Recipe", TR(40, 108, 438, 760)))
    pn.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    C.frame(pn, "Recipe/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    pn.child(M.panel_sea_node("Recipe/Grid"))
    M.bracket_nodes(pn, "Recipe/Bracket")
    C.section_label(pn, "Recipe/Caption", "Caption", "Recipe", TL(26, 26, 200, 18))
    label(pn.child(N("Recipe/Merged", "Merged", TR(26, 26, 220, 18))), "0 of 4 merged", "Label", "NeutralLightest",
          align="right")
    for i, name in enumerate(ORDER):
        recipe_row(pn, i, name)
    image_node(pn, "Recipe/RuleLeft", "RuleLeft", TL(26, 486, 150, 1), "SurfaceDark")
    image_node(pn, "Recipe/RuleRight", "RuleRight", TR(26, 486, 150, 1), "SurfaceDark")
    label(pn.child(N("Recipe/Makes", "Makes", TL(176, 477, 86, 18))), "Makes", "Label", "NeutralLightest",
          align="center")
    om = pn.child(N("Recipe/Omni", "Omni", TL(26, 516, 386, 216)))
    om.add("cg", 225, lambda ctx, go: canvas_group(True)(ctx, go).replace("  m_Alpha: 1\n", "  m_Alpha: 0.35\n"))
    om.add("pop", 114, M.entrance("Pop", play_on_enable=False, duration=M.CEREMONY))
    fill = om.child(N("Recipe/Omni/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True))
    bord = om.child(N("Recipe/Omni/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    art = art_node(om, "Recipe/Omni/Art", "Art", TL(26, 58, 100, 100), OMNI, preserve=True)
    label(om.child(N("Recipe/Omni/Name", "Name", TL(146, 66, 220, 30))), "Omni crystal", "H3", "TextLight")
    label(om.child(N("Recipe/Omni/Line", "Line", TL(146, 100, 220, 48))), "Forms when all four elements have merged.",
          "BodySmall", "NeutralLightest", valign="top", wrap=True)


SEG_X, SEG_W, SEG_GAP = 248, 150, 6
BAR_W, BAR_H = 1120, 112


def converter_bar(root):
    bar = root.child(N("Bar", "Bar", R((0.5, 0), None, (0, 68), (BAR_W, BAR_H), (0.5, 0))))
    bar.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    C.frame(bar, "Bar/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    M.bracket_nodes(bar, "Bar/Bracket")
    label(bar.child(N("Bar/Status", "Status", TL(28, 22, 200, 16))), "Ready", "Label", "NeutralLightest")
    label(bar.child(N("Bar/Clock", "Clock", TL(28, 44, 210, 40))), "00:00 / --:--", "H2", "TextLight")
    for i, name in enumerate(ORDER):
        x = SEG_X + i * (SEG_W + SEG_GAP)
        k = f"Bar/Seg{name}"
        image_node(bar, f"{k}Track", f"Seg{name}Track", TL(x, 42, SEG_W, 4), "SurfaceDark")
        seg = bar.child(N(k, f"Seg{name}", TL(x, 42, SEG_W, 4)))
        seg.add("cr", 222, K.canvas_renderer()).add("img", 114, filled_image(tok("ControlActive"))) \
            .add("mask", 114, A.rect_mask())
        sg = seg.child(N(f"{k}/SparkGroup", "SparkGroup", STRETCH))
        sg.add("cg", 225, canvas_group(False))
        sp = sg.child(N(f"{k}/Spark", "Spark", R((0, 0), (0, 1), (-48, 0), (48, 0), (0, 0.5))))
        sp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_spark"))) \
            .add("sp", 114, M.spark_body(False, period=1.8, delay=0.3 * i))
        label(bar.child(N(f"Bar/Label{name}", f"Label{name}", TL(x, 60, SEG_W, 16))), name, "Label", "NeutralLightest")
    w, h = K.button_size("CONVERT")
    w = max(w, 210)
    holder = bar.child(N("Bar/Convert", "Convert", R((1, 0.5), None, (-28, 0), (w, h), (1, 0.5))))
    holder.children.append(P.Inst("ConvertButton", "KitButton", "ConvertButton", STRETCH,
                                  K.button_mods("KitButton", "Purchase", True, "CONVERT"),
                                  added=[("mask", 114, A.rect_mask())], added_nodes=[C.sweep_node()]))


# --------------------------------------------------------------------------------------------
# The Ark's tabs (Shombith, 2026-10-27: "add two more tabs along with the converter. 1. Squads ... 2. Decor - lock it")
# --------------------------------------------------------------------------------------------

# ARTIFACTS (Garrett, 2026-10-27, Docs/Pilots/ARTIFACTS.md §4 and §8.3: "the Ark screen gets an artifact panel beside the
# Converter"): spend elemental crystals on an artifact's four tracks.
TABS = ("Converter", "Squads", "Artifacts", "Decor")
LOCKED_TABS = {"Decor"}                       # MenuAvailability.Locked: refuses with the sting and "Not open yet."
TAB_Y, TAB_H = 108, 52
MAX_SQUADS, SEATS, JOIN_ROWS = 4, 3, 6        # SquadBook.MaxSquads, SavedSquad.SlotCount, the join list's rows
PICKS = ("Vessel", "Element", "Pilot", "Artifact")   # a card's four rows
VESSEL_LIST = K.meta_guid("Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset")   # the Hangar's roster
PILOT_CATALOG = K.meta_guid("Assets/Resources/Pilots/PilotArtifactCatalog.asset")
CARD_ART = K.meta_guid("Assets/_SO_Assets/UI/HangarCardArt.asset")                  # the Hangar's hull renders
COLOR_SET = K.meta_guid("Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset")  # the domains' colours
DOMAIN_ORDER = (("Jade", 1), ("Ruby", 2), ("Gold", 4))                                 # Domains values
# The deck (Rev 31): the list down the left, the cards in a fan to its right, the picker under them in Make squad.
LIST_X, LIST_Y, LIST_W, LIST_H = 40, 176, 360, 864
COL_X, COL_W = 430, 1450                       # the deck's column, right of the list
DECK_Y, DECK_H, DECK_PIVOT_X = 270, 700, 1155  # the cards' parent; Make squad scales it about (1155, top)
CARD_W, CARD_H = 350, 600
CARDS = ((DECK_PIVOT_X, 12, 0), (DECK_PIVOT_X - 380, 30, 6), (DECK_PIVOT_X + 380, 30, -6))   # centre x, top, z degrees
PICKER_Y, BAR_Y = 780, 996
VESSEL_TILES, PILOT_CHIPS, ELEMENT_CHIPS, ARTIFACT_CHIPS = 7, 5, 4, 8
SAFE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9 .,()!?/-]*[A-Za-z0-9.!?)]$|^[A-Za-z0-9]$")


def ystr(v):
    """A YAML scalar as Unity writes one: bare when nothing in it needs quoting, else single-quoted."""
    if v == "":
        return ""
    return v if SAFE.match(v) else "'" + v.replace("'", "''") + "'"


def yfield(name, v):
    return f"  {name}: {ystr(v)}\n"


def ylist(name, values):
    return f"  {name}:\n" + "".join(f"  - {ystr(v)}\n" for v in values)


def page_node(root, key, shown):
    pg = root.child(N(key, key, STRETCH))
    pg.add("cg", 225, canvas_group(shown))
    return pg


# The window backdrop every style-B window draws (author_menu_arcade.WINDOW_BACKDROP_ALPHA; the Codex and the Hangar
# build the same pair): a near-opaque veil under the top bar, and the sea lines over it.
WINDOW_BACKDROP_ALPHA = 0.95
TOP_BAR = 84


def page_backdrop(page, key):
    """The veil and the sea behind a page that has no 3D picture of its own (Shombith 2026-10-27, of the Squads tab
    showing the game world straight through: "Ark no sea line"). First under the page, so its content draws over it;
    the page's CanvasGroup hides it with the page. The converter keeps its picture and draws none."""
    under_bar = R((0, 0), (1, 1), (0, -TOP_BAR / 2), (0, -TOP_BAR))
    veil = page.child(N(f"{key}/Backdrop", "Backdrop", under_bar))
    veil.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack", WINDOW_BACKDROP_ALPHA), raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", WINDOW_BACKDROP_ALPHA))
    page.children.append(M.window_sea_node(f"{key}/Sea", "Sea", under_bar))


def stretch_x(left, right, top, h):
    """A rect stretched across its parent between `left` and `right`, `top` down from the top, `h` tall."""
    return R((0, 1), (1, 1), ((left - right) / 2, -top), (-(left + right), h), (0.5, 1))


def tab_row(root):
    w = sum(K.label_width(t.upper(), H.TAB_ROLE) + H.TAB_PAD for t in TABS) + H.TAB_GAP * (len(TABS) - 1)
    row = root.child(N("Tabs", "Tabs", R((0.5, 1), None, (0, -TAB_Y), (w, TAB_H), (0.5, 1))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), H.TAB_GAP, 4, (0, 0), (0, 0)))
    for t in TABS:
        tab = H.text_tab(row, f"Tabs/{t}", t, t.upper(), H.TAB_ROLE, K.label_width(t.upper(), H.TAB_ROLE) + H.TAB_PAD, TAB_H)
        if t in LOCKED_TABS:
            tab.add("gate", 114, lambda ctx, go, k=f"Tabs/{t}": K.mono(go, S["availability"],
                    "  availability: 1\n  lockedOverlay: {fileID: 0}\n  unavailableOverlay: {fileID: 0}\n"
                    f"  label: {ref(ctx.c(f'{k}/Label', 'tmp'))}\n"
                    "  unavailableLabelColor: {r: 1, g: 1, b: 1, a: 0.4}\n  dimOwnGraphics: 1\n"
                    "  dimTint: {r: 1, g: 1, b: 1, a: 0.35}\n  lockedMessage: Not open yet.\n"))
    # Right of the row the Ark's three tabs never reach the title band (TL 40..554) or the recipe panel (TR 40, 438 wide).
    assert w <= 1920 - 2 * 554, f"the Ark's tab row runs into the title band ({w})"


def off(body):
    """A component body authored disabled (its owner switches it on)."""
    return lambda ctx, go: body(ctx, go).replace("  m_Enabled: 1\n", "  m_Enabled: 0\n", 1)


def kit_row_text(parent, key, name, r, h):
    """A selectable row (the Hangar's kit_row): a name, a line, the squad's domain and the active mark down its left edge."""
    row = H.kit_row(parent, key, name, r, h, content_keys=((f"{key}/Title", "tmp"),))
    row.add("cg", 225, canvas_group(True))
    image_node(row, f"{key}/Mark", "Mark", TL(0, 14, 3, h - 28), "ControlActive")
    H.ellipsize(label(row.child(N(f"{key}/Title", "Title", stretch_x(20, 110, 14, 30))), "", "H3", "TextLight"))
    label(row.child(N(f"{key}/Domain", "Domain", TR(16, 18, 90, 16))), "", "Label", "NeutralLightest", align="right")
    H.ellipsize(label(row.child(N(f"{key}/Line", "Line", stretch_x(20, 20, 52, 22))), "", "BodySmall", "NeutralLightest"))
    row.add("row", 114, lambda ctx, go: mono(go, S["squad_row"],
            f"  button: {ref(ctx.c(key, 'button'))}\n  group: {ref(ctx.c(key, 'cg'))}\n"
            f"  titleText: {ref(ctx.c(f'{key}/Title', 'tmp'))}\n  lineText: {ref(ctx.c(f'{key}/Line', 'tmp'))}\n"
            f"  activeMark: {ref(ctx.c(f'{key}/Mark', 'img'))}\n  domainText: {ref(ctx.c(f'{key}/Domain', 'tmp'))}\n"))
    return row


def small_button(parent, key, name, caption, r, variant="Default"):
    return H.text_button(parent, key, name, caption, r, None, None, None, None, variant=variant)


def pick_chip(parent, key, name, r, h, caption="", picture=None, picture_rect=None, line_rect=None, label_rect=None,
              label_role="ButtonSmall", align="center"):
    """One SquadPickChip: a kit_row button, its word, an optional picture and second line, and the picked mark."""
    chip = H.kit_row(parent, key, name, r, h, content_keys=((f"{key}/Label", "tmp"),))
    chip.add("cg", 225, canvas_group(True))
    chip.child(N(f"{key}/Picked", "Picked", R((0, 0), (1, 1), (0, 0), (2, 2)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, off(K.image(tok("Selection"), K.kit_sprite("kit_sliver10_line2"), True)))
    if picture_rect is not None:
        pic = chip.child(N(f"{key}/Picture", "Picture", picture_rect))
        pic.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, picture, preserve=True))
    label(chip.child(N(f"{key}/Label", "Label", label_rect or STRETCH)), caption, label_role, "TextLight", align=align)
    if line_rect is not None:
        H.ellipsize(label(chip.child(N(f"{key}/Line", "Line", line_rect)), "", "Label", "NeutralLightest"))

    def body(ctx, go):
        pic = ref(ctx.c(f"{key}/Picture", "img")) if picture_rect is not None else "{fileID: 0}"
        line = ref(ctx.c(f"{key}/Line", "tmp")) if line_rect is not None else "{fileID: 0}"
        return mono(go, S["chip"], f"  button: {ref(ctx.c(key, 'button'))}\n  group: {ref(ctx.c(key, 'cg'))}\n"
                    f"  label: {ref(ctx.c(f'{key}/Label', 'tmp'))}\n  line: {line}\n  image: {pic}\n"
                    f"  selectedMark: {ref(ctx.c(f'{key}/Picked', 'img'))}\n")
    chip.add("chip", 114, body)
    return chip


def squad_list(page):
    pn = page.child(N("Squads/List", "List", TL(LIST_X, LIST_Y, LIST_W, LIST_H)))
    pn.add("cg", 225, canvas_group(True))
    C.section_label(pn, "Squads/List/Caption", "Caption", "Your squads", TL(0, 0, 220, 18))
    label(pn.child(N("Squads/List/Count", "Count", TR(0, 0, 120, 18))), "0 of 4", "Label", "NeutralLightest", align="right")
    for i in range(MAX_SQUADS):
        kit_row_text(pn, f"Squads/List/Row{i}", f"Row{i}", TL(0, 32 + i * 108, LIST_W, 96), 96)
    small_button(pn, "Squads/List/New", "New", "Make squad", TL(0, LIST_H - 120, LIST_W, 52), "Cta")
    small_button(pn, "Squads/List/Join", "Join", "Join a friend's squad", TL(0, LIST_H - 60, LIST_W, 52))


def squad_header(page):
    hd = page.child(N("Squads/Head", "Head", TL(COL_X, 176, COL_W, 84)))
    hd.add("cg", 225, canvas_group(False))
    label(hd.child(N("Squads/Head/Kicker", "Kicker", TL(0, 0, 800, 16))), "Your squad", "Label", "NeutralLightest")
    H.ellipsize(label(hd.child(N("Squads/Head/Name", "Name", TL(0, 18, 800, 44))), "", "H1", "TextLight"))
    H.ellipsize(label(hd.child(N("Squads/Head/Line", "Line", TL(0, 64, 800, 20))), "", "BodySmall", "NeutralLightest"))
    label(hd.child(N("Squads/Head/DomainCaption", "DomainCaption", TR(3 * 118 + 8, 48, 80, 16))), "Domain", "Label",
          "NeutralLightest", align="right")
    for i, (name, _) in enumerate(DOMAIN_ORDER):
        pick_chip(hd, f"Squads/Head/Domain{name}", f"Domain{name}", TR((2 - i) * 118, 36, 110, 40), 40, name,
                  K.chrome_sprite("chrome_diamond"), TL(12, 13, 14, 14), label_rect=TL(32, 0, 74, 40))


def mate_card(deck, i):
    k = f"Squads/Deck/Card{i}"
    cx, top, rot = CARDS[i]
    card = H.kit_row(deck, k, f"Card{i}", TL(cx - CARD_W // 2, top, CARD_W, CARD_H), CARD_H,
                     content_keys=((f"{k}/Who", "tmp"),))
    card.rot = rot
    card.child(N(f"{k}/Edit", "Edit", R((0, 0), (1, 1), (0, 0), (4, 4)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, off(K.image(tok("Selection"), K.kit_sprite("kit_sliver14_line2"), True)))
    label(card.child(N(f"{k}/Seat", "Seat", TL(18, 16, 200, 16))), ("Leader", "Rogue One", "Rogue Two")[i], "Label", "NeutralLightest")
    label(card.child(N(f"{k}/Number", "Number", TR(18, 16, 100, 16))), f"0{i + 1} / 03", "Label", "NeutralLightest", align="right")
    hull = card.child(N(f"{k}/Hull", "Hull", TL(40, 60, 290, 97)))
    hull.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None, preserve=True))
    bot = hull.child(N(f"{k}/Hull/Bot", "Bot", R((0.5, 0.5), None, (0, 0), (6, 6)), active=False))
    bot.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    hull.add("swap", 114, lambda ctx, go: mono(go, S["nanobots"],
             f"  hull: {ref(ctx.c(f'{k}/Hull', 'img'))}\n  companion: {ref(ctx.c(f'{k}/Figure', 'img'))}\n"
             f"  botTemplate: {ref(ctx.c(f'{k}/Hull/Bot', 'img'))}\n  botCount: 34\n  seconds: 1.1\n  scatter: 130\n"
             "  sparkShare: 0.25\n  sparkColor: {r: 1, g: 1, b: 1, a: 1}\n  tintStrength: 0.45\n"))
    H.ellipsize(label(card.child(N(f"{k}/Who", "Who", TL(18, 318, CARD_W - 36, 30))), "", "H3", "TextLight"))
    for j, pick in enumerate(PICKS):
        y = 360 + j * 52
        image_node(card, f"{k}/{pick}Rule", f"{pick}Rule", TL(18, y, CARD_W - 36, 1), "SurfaceDark")
        label(card.child(N(f"{k}/{pick}Kicker", f"{pick}Kicker", TL(18, y + 14, 110, 16))), pick, "Label", "NeutralLightest")
        H.ellipsize(label(card.child(N(f"{k}/{pick}", pick, TR(18, y + 10, 200, 24))), "", "Body", "TextLight", align="right"))
    # The pilot steps out of the card: over its left edge, above the frame.
    pilot = card.child(N(f"{k}/Figure", "Figure", TL(-40, 70, 180, 290)))
    pilot.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None, preserve=True))
    open_ = card.child(N(f"{k}/OpenSeat", "OpenSeat", TL(-10, 110, 120, 240)))
    open_.add("cg", 225, canvas_group(False))
    open_.child(N(f"{k}/OpenSeat/Line", "Line", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceLight", 0.6), K.kit_sprite("kit_sliver10_line1"), True))
    label(open_.child(N(f"{k}/OpenSeat/Words", "Words", R((0, 0), (1, 1), (0, 0), (-16, 0)))), "A friend takes this seat",
          "BodySmall", "NeutralLightest", align="center", wrap=True)

    def body(ctx, go):
        out = (f"  button: {ref(ctx.c(k, 'button'))}\n  editMark: {ref(ctx.c(f'{k}/Edit', 'img'))}\n"
               f"  seatText: {ref(ctx.c(f'{k}/Seat', 'tmp'))}\n  numberText: {ref(ctx.c(f'{k}/Number', 'tmp'))}\n"
               f"  whoText: {ref(ctx.c(f'{k}/Who', 'tmp'))}\n")
        for pick in PICKS:
            out += f"  {pick.lower()}Text: {ref(ctx.c(f'{k}/{pick}', 'tmp'))}\n"
        out += (f"  hullImage: {ref(ctx.c(f'{k}/Hull', 'img'))}\n  pilotImage: {ref(ctx.c(f'{k}/Figure', 'img'))}\n"
                f"  openSeatGroup: {ref(ctx.c(f'{k}/OpenSeat', 'cg'))}\n  nanobots: {ref(ctx.c(f'{k}/Hull', 'swap'))}\n"
                "  artifactColor: {r: 0.902, g: 0.914, b: 1, a: 1}\n  noArtifactColor: {r: 0.361, g: 0.373, b: 0.439, a: 1}\n")
        return mono(go, S["mate"], out)
    card.add("mate", 114, body)


def squad_deck(page):
    deck = page.child(N("Squads/Deck", "Deck", R((0, 1), (1, 1), (0, -DECK_Y), (0, DECK_H), (DECK_PIVOT_X / 1920, 1))))
    # The rogues first, so the leader's card draws over them.
    for i in (1, 2, 0):
        mate_card(deck, i)


def squad_picker(page):
    pk = page.child(N("Squads/Picker", "Picker", TL(COL_X, PICKER_Y, COL_W, 210)))
    pk.add("cg", 225, canvas_group(False))
    label(pk.child(N("Squads/Picker/VesselCaption", "VesselCaption", TL(0, 21, 78, 16))), "Vessel", "Label", "NeutralLightest")
    small_button(pk, "Squads/Picker/Prev", "Prev", "<", TL(84, 4, 40, 50))
    for i in range(VESSEL_TILES):
        pick_chip(pk, f"Squads/Picker/Vessel{i}", f"Vessel{i}", TL(130 + i * 158, 0, 150, 58), 58, "",
                  None, TL(6, 2, 138, 38), label_rect=R((0, 0), (1, 0), (0, 4), (0, 16), (0.5, 0)), label_role="Label")
    small_button(pk, "Squads/Picker/Next", "Next", ">", TL(130 + VESSEL_TILES * 158, 4, 40, 50))
    label(pk.child(N("Squads/Picker/PilotCaption", "PilotCaption", TL(0, 82, 78, 16))), "Pilot", "Label", "NeutralLightest")
    # Chip 0 is the empty seat; the rest page through the callsign pilots between < and > (one per callsign, any
    # vessel: Garrett, 2026-10-27, Docs/Pilots/CALLSIGNS.md).
    def pilot_x(i):
        return 84 if i == 0 else 330 + (i - 1) * 198
    for i in range(PILOT_CHIPS):
        pick_chip(pk, f"Squads/Picker/Pilot{i}", f"Pilot{i}", TL(pilot_x(i), 66, 190, 50), 50, "",
                  None, TL(4, 2, 34, 46), line_rect=TL(46, 28, 136, 16), label_rect=TL(46, 6, 136, 20), label_role="Body",
                  align="left")
    small_button(pk, "Squads/Picker/PilotPrev", "PilotPrev", "<", TL(282, 66, 40, 50))
    small_button(pk, "Squads/Picker/PilotNext", "PilotNext", ">", TL(pilot_x(PILOT_CHIPS), 66, 40, 50))
    label(pk.child(N("Squads/Picker/ElementCaption", "ElementCaption", TL(0, 134, 78, 16))), "Element", "Label", "NeutralLightest")
    for i, name in enumerate(("Charge", "Mass", "Space", "Time")):
        pick_chip(pk, f"Squads/Picker/Element{i}", f"Element{i}", TL(84 + i * 118, 124, 110, 36), 36, name)
    seat = pk.child(N("Squads/Picker/SeatMode", "SeatMode", TL(600, 124, 600, 36)))
    seat.add("cg", 225, canvas_group(False))
    label(seat.child(N("Squads/Picker/SeatMode/Caption", "Caption", TL(0, 10, 110, 16))), "Who flies it", "Label", "NeutralLightest")
    for i, name in enumerate(("AI wingmate", "Open for a friend")):
        pick_chip(seat, f"Squads/Picker/SeatMode/Chip{i}", f"Chip{i}", TL(120 + i * 200, 0, 190, 36), 36, name)
    label(pk.child(N("Squads/Picker/ArtifactCaption", "ArtifactCaption", TL(0, 178, 78, 16))), "Artifact", "Label", "NeutralLightest")
    for i in range(ARTIFACT_CHIPS):
        pick_chip(pk, f"Squads/Picker/Artifact{i}", f"Artifact{i}", TL(84 + i * 150, 168, 142, 36), 36, "None" if i == 0 else "")


def squad_bars(page):
    mk = page.child(N("Squads/MakeBar", "MakeBar", TL(COL_X, BAR_Y, COL_W, 52)))
    mk.add("cg", 225, canvas_group(False))
    small_button(mk, "Squads/MakeBar/Cancel", "Cancel", "Cancel", TL(0, 0, 160, 52))
    label(mk.child(N("Squads/MakeBar/Hint", "Hint", TL(184, 16, 900, 20))),
          "Pick a card, then its vessel, pilot, element and artifact. Take into appears once the squad is made.",
          "BodySmall", "NeutralLightest")
    small_button(mk, "Squads/MakeBar/Create", "Create", "Create squad", TR(0, 0, 240, 52), "Cta")
    vw = page.child(N("Squads/ViewBar", "ViewBar", TL(COL_X, BAR_Y, COL_W, 52)))
    vw.add("cg", 225, canvas_group(False))
    small_button(vw, "Squads/ViewBar/Remove", "Remove", "Delete", TL(0, 6, 140, 40), "Danger")
    small_button(vw, "Squads/ViewBar/Edit", "Edit", "Edit", TL(152, 6, 120, 40))
    label(vw.child(N("Squads/ViewBar/Hint", "Hint", TL(292, 16, 520, 20))),
          "Three mates, four cards each: a squad is a twelve-card deck.", "BodySmall", "NeutralLightest")
    label(vw.child(N("Squads/ViewBar/Caption", "Caption", TR(470, 18, 220, 16))), "Take this squad into", "Label",
          "NeutralLightest", align="right")
    small_button(vw, "Squads/ViewBar/Mission", "Mission", "Mission", TR(236, 0, 220, 52))
    lock = vw.child(N("Squads/ViewBar/Mission/Lock", "Lock", TL(16, 16, 16, 20)))
    lock.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_lock"), preserve=True))
    small_button(vw, "Squads/ViewBar/Arena", "Arena", "Arena", TR(0, 0, 220, 52), "Play")


def squad_empty(page):
    e = page.child(N("Squads/Empty", "Empty", TL(COL_X, 250, COL_W, 760)))
    e.add("cg", 225, canvas_group(True))
    for i, (x, rot) in enumerate(((550, 0), (170, 6), (930, -6))):
        g = image_node(e, f"Squads/Empty/Ghost{i}", f"Ghost{i}", TL(x, 40, CARD_W, CARD_H), "SurfaceLight",
                       K.kit_sprite("kit_sliver14_line1"), sliced=True, alpha=0.35)
        g.rot = rot
    label(e.child(N("Squads/Empty/Heading", "Heading", stretch_x(0, 0, 250, 40))), "No squad yet", "H2", "TextLight", align="center")
    label(e.child(N("Squads/Empty/Line", "Line", stretch_x(400, 400, 300, 60))),
          "A squad is you and two squad mates, friends or AI wingmates: three cards. Make one, then take it into a mission "
          "or the arena.", "Body", "NeutralLightest", align="center", valign="top", wrap=True)
    small_button(e, "Squads/Empty/Make", "Make", "Make squad", TL(COL_W // 2 - 110, 380, 220, 52), "Cta")


def squad_join(page):
    j = page.child(N("Squads/Joining", "Joining", TL(COL_X, 176, COL_W, 864)))
    j.add("cg", 225, canvas_group(False))
    C.frame(j, "Squads/Joining/Frame", 14, "SurfaceBlack", "SurfaceDark", fill_alpha=0.96)
    label(j.child(N("Squads/Joining/Heading", "Heading", TL(28, 24, 600, 40))), "Join a friend's squad", "H2", "TextLight")
    small_button(j, "Squads/Joining/Close", "Close", "Close", TR(28, 24, 140, 44))
    label(j.child(N("Squads/Joining/Status", "Status", stretch_x(28, 28, 80, 22))), "", "BodySmall", "NeutralLightest")
    for i in range(JOIN_ROWS):
        kit_row_text(j, f"Squads/Joining/Row{i}", f"Row{i}", stretch_x(28, 28, 120 + i * 100, 88), 88)


SQUAD_WORDS = (
    yfield("countLine", "{count} of {max}") + yfield("madeLine", "Yours")
    + yfield("madeOpenLine", "Yours - {open} seat open") + yfield("madeFullLine", "Yours - every seat filled")
    + yfield("joinedLine", "{owner}'s squad - joined") + yfield("makingLine", "Making...")
    + yfield("ownKicker", "Your squad - you lead") + yfield("joinedKicker", "{owner}'s squad - you fly {seat}")
    + yfield("makeKicker", "Make squad") + yfield("editKicker", "Edit squad")
    + yfield("ownOpenDetail", "{open} seat open for a friend.") + yfield("ownFullDetail", "Every seat is filled.")
    + yfield("makeDetail", "Pick a card to edit it.")
    + yfield("seatedLine", "Only {owner} changes the squad.")
    + yfield("pendingLine", "Joined. {owner} seats you when they next open the Ark.")
    + yfield("seatLostLine", "{owner} gave your seat to someone else. Leave the squad, or ask them to open it again.")
    + yfield("disbandedLine", "{owner} deleted this squad.")
    + yfield("offlineLine", "Offline - showing the last copy of {owner}'s squad.")
    + ylist("seatNames", ("Leader", "Rogue One", "Rogue Two")) + yfield("numberLine", "0{n} / 03")
    + yfield("youLine", "You") + yfield("aiLine", "AI wingmate") + yfield("openLine", "Open seat")
    + yfield("unknownFriendLine", "A friend") + yfield("aiModeLabel", "AI wingmate") + yfield("openModeLabel", "Open for a friend")
    + ylist("elementNames", ("", "Charge", "Mass", "Space", "Time", "Omni"))
    + ylist("domainNames", ("", "Jade", "Ruby", "Blue", "Gold"))
    + yfield("basePilotLine", "No pilot") + yfield("basePilotDetail", "No sight on LB")
    + yfield("pilotDetail", "{sight}") + yfield("noArtifactLine", "No artifact") + yfield("noArtifactChip", "None")
    + yfield("createWord", "Create squad") + yfield("saveWord", "Save squad")
    + yfield("deleteLabel", "Delete") + yfield("leaveLabel", "Leave") + yfield("confirmLabel", "Press again")
    + yfield("newSquadName", "") + yfield("fullLine", "You are in 4 squads. Leave or delete one first.")
    + yfield("createdToast", "{squad} made. Take it into a mission or the arena.")
    + yfield("notLeaderLine", "Only {owner} changes the squad.")
    + yfield("joinLookingLine", "Looking at your friends' squads...")
    + yfield("joinNoneLine", "No friend has a seat open right now.")
    + yfield("joinOfflineLine", "Sign in to join a friend's squad.")
    + yfield("joinRowTitle", "{owner}'s {squad}") + yfield("joinRowLine", "{seat} open")
    + yfield("joinedToast", "Joined. {owner} seats you when they next open the Ark.")
    + yfield("alreadyInLine", "You are already in that squad.") + yfield("seatGoneLine", "That seat was just taken."))


def refs_of(ctx, keys, comp):
    return "".join(f"  - {ref(ctx.c(k, comp))}\n" for k in keys)


def squads_page(root):
    page = page_node(root, "Squads", False)
    page_backdrop(page, "Squads")
    squad_list(page)
    squad_header(page)
    squad_empty(page)
    squad_deck(page)
    squad_picker(page)
    squad_bars(page)
    squad_join(page)

    def body(ctx, go):
        lists = lambda name, keys, comp: f"  {name}:\n" + refs_of(ctx, keys, comp)
        return mono(go, S["squads"],
                    f"  vessels: {K.ref(11400000, VESSEL_LIST, 2)}\n  catalog: {K.ref(11400000, PILOT_CATALOG, 2)}\n"
                    f"  cardArt: {K.ref(11400000, CARD_ART, 2)}\n  colors: {K.ref(11400000, COLOR_SET, 2)}\n"
                    + lists("squadRows", [f"Squads/List/Row{i}" for i in range(MAX_SQUADS)], "row")
                    + f"  countText: {ref(ctx.c('Squads/List/Count', 'kt'))}\n"
                    f"  newButton: {ref(ctx.c('Squads/List/New', 'button'))}\n"
                    f"  joinButton: {ref(ctx.c('Squads/List/Join', 'button'))}\n"
                    f"  headerGroup: {ref(ctx.c('Squads/Head', 'cg'))}\n"
                    f"  kickerText: {ref(ctx.c('Squads/Head/Kicker', 'tmp'))}\n"
                    f"  squadNameText: {ref(ctx.c('Squads/Head/Name', 'tmp'))}\n"
                    f"  squadLineText: {ref(ctx.c('Squads/Head/Line', 'tmp'))}\n"
                    + lists("domainChips", [f"Squads/Head/Domain{n}" for n, _ in DOMAIN_ORDER], "chip")
                    + "  domainOrder:\n" + "".join(f"  - {v}\n" for _, v in DOMAIN_ORDER)
                    + f"  deckRoot: {ref(ctx.rt('Squads/Deck'))}\n  makeScale: 0.8\n"
                    + lists("mateCards", [f"Squads/Deck/Card{i}" for i in range(SEATS)], "mate")
                    + f"  emptyGroup: {ref(ctx.c('Squads/Empty', 'cg'))}\n"
                    f"  emptyMakeButton: {ref(ctx.c('Squads/Empty/Make', 'button'))}\n  swapStagger: 0.14\n"
                    f"  pickerGroup: {ref(ctx.c('Squads/Picker', 'cg'))}\n"
                    + lists("vesselTiles", [f"Squads/Picker/Vessel{i}" for i in range(VESSEL_TILES)], "chip")
                    + f"  vesselPrevButton: {ref(ctx.c('Squads/Picker/Prev', 'button'))}\n"
                    f"  vesselNextButton: {ref(ctx.c('Squads/Picker/Next', 'button'))}\n"
                    + lists("pilotChips", [f"Squads/Picker/Pilot{i}" for i in range(PILOT_CHIPS)], "chip")
                    + f"  pilotPrevButton: {ref(ctx.c('Squads/Picker/PilotPrev', 'button'))}\n"
                    f"  pilotNextButton: {ref(ctx.c('Squads/Picker/PilotNext', 'button'))}\n"
                    + f"  seatModeGroup: {ref(ctx.c('Squads/Picker/SeatMode', 'cg'))}\n"
                    + lists("seatModeChips", [f"Squads/Picker/SeatMode/Chip{i}" for i in range(2)], "chip")
                    + lists("elementChips", [f"Squads/Picker/Element{i}" for i in range(ELEMENT_CHIPS)], "chip")
                    + lists("artifactChips", [f"Squads/Picker/Artifact{i}" for i in range(ARTIFACT_CHIPS)], "chip")
                    + f"  makeBarGroup: {ref(ctx.c('Squads/MakeBar', 'cg'))}\n"
                    f"  cancelButton: {ref(ctx.c('Squads/MakeBar/Cancel', 'button'))}\n"
                    f"  createButton: {ref(ctx.c('Squads/MakeBar/Create', 'button'))}\n"
                    f"  createLabel: {ref(ctx.c('Squads/MakeBar/Create/Label', 'tmp'))}\n"
                    f"  viewBarGroup: {ref(ctx.c('Squads/ViewBar', 'cg'))}\n"
                    f"  removeButton: {ref(ctx.c('Squads/ViewBar/Remove', 'button'))}\n"
                    f"  removeLabel: {ref(ctx.c('Squads/ViewBar/Remove/Label', 'tmp'))}\n"
                    f"  editButton: {ref(ctx.c('Squads/ViewBar/Edit', 'button'))}\n"
                    f"  missionButton: {ref(ctx.c('Squads/ViewBar/Mission', 'button'))}\n"
                    f"  missionLock: {ref(ctx.c('Squads/ViewBar/Mission/Lock', 'img'))}\n"
                    f"  arenaButton: {ref(ctx.c('Squads/ViewBar/Arena', 'button'))}\n"
                    f"  joinGroup: {ref(ctx.c('Squads/Joining', 'cg'))}\n"
                    + lists("joinRows", [f"Squads/Joining/Row{i}" for i in range(JOIN_ROWS)], "row")
                    + f"  joinStatusText: {ref(ctx.c('Squads/Joining/Status', 'kt'))}\n"
                    f"  joinCloseButton: {ref(ctx.c('Squads/Joining/Close', 'button'))}\n"
                    "  refreshSeconds: 20\n  publishDelaySeconds: 2\n  confirmSeconds: 3\n  maxFriendsRead: 30\n"
                    "  newSquadDomain: 4\n"
                    + SQUAD_WORDS)
    page.add("logic", 114, body)
    return page


ART_ROWS = 4                                   # Javelin and Aegis, and room for two more
TRACK_ELEMENTS = ("Mass", "Space", "Time", "Charge")   # ArkArtifactsPage.TrackOrder
TRACK_Y, TRACK_H, TRACK_STEP = 120, 156, 170
ARTIFACT_WORDS = (
    ylist("elementNames", ("", "Charge", "Mass", "Space", "Time", "Omni"))
    + yfield("crystalTemplate", "{element} {count}")
    + yfield("levelsTemplate", "Mass {mass}  Space {space}  Time {time}  Charge {charge}")
    + yfield("levelTemplate", "{level} / {max}") + yfield("valueTemplate", "{now} -> {next}")
    + yfield("valueTopTemplate", "{now}") + yfield("twistLockedTemplate", "At {at}: {twist}")
    + yfield("twistOpenTemplate", "{twist}") + yfield("raiseTemplate", "Raise - {cost} {element}")
    + yfield("omniTemplate", "{cost} Omni instead") + yfield("topWord", "At the top")
    + yfield("notEnoughTemplate", "Not enough {element} crystals.")
    + yfield("raisedTemplate", "{artifact}: {element} is now {level}.")
    + yfield("twistedTemplate", "{artifact} twists: {twist}"))


def artifacts_page(root):
    """ARTIFACTS: the player's artifacts down the left (SquadRowView rows: the name, the four levels), and the picked
    one's four element tracks to the right, each with RAISE (its own crystals) and OMNI (omni crystals instead), above
    the profile's five crystal balances. ArkArtifactsPage draws it; every word is on that component."""
    page = page_node(root, "Artifacts", False)
    page_backdrop(page, "Artifacts")
    pn = page.child(N("Artifacts/List", "List", TL(LIST_X, LIST_Y, LIST_W, LIST_H)))
    pn.add("cg", 225, canvas_group(True))
    C.section_label(pn, "Artifacts/List/Caption", "Caption", "Your artifacts", TL(0, 0, 220, 18))
    for i in range(ART_ROWS):
        kit_row_text(pn, f"Artifacts/List/Row{i}", f"Row{i}", TL(0, 32 + i * 108, LIST_W, 96), 96)
    e = page.child(N("Artifacts/Empty", "Empty", TL(COL_X, 250, COL_W, 200)))
    e.add("cg", 225, canvas_group(False))
    label(e.child(N("Artifacts/Empty/Heading", "Heading", stretch_x(0, 0, 40, 40))), "No artifacts yet", "H2", "TextLight",
          align="center")
    label(e.child(N("Artifacts/Empty/Line", "Line", stretch_x(300, 300, 90, 60))),
          "Find them on voyages and bring them home to your Ark.", "Body", "NeutralLightest", align="center", wrap=True)
    d = page.child(N("Artifacts/Detail", "Detail", TL(COL_X, LIST_Y, COL_W, LIST_H)))
    d.add("cg", 225, canvas_group(True))
    label(d.child(N("Artifacts/Detail/Kicker", "Kicker", TL(0, 0, 600, 16))), "Artifact", "Label", "NeutralLightest")
    H.ellipsize(label(d.child(N("Artifacts/Detail/Name", "Name", TL(0, 18, 700, 44))), "", "H1", "TextLight"))
    label(d.child(N("Artifacts/Detail/Description", "Description", TL(0, 64, 760, 44))), "", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)
    label(d.child(N("Artifacts/Detail/CrystalCaption", "CrystalCaption", TR(0, 0, 640, 16))), "Crystals", "Label",
          "NeutralLightest", align="right")
    for i in range(5):
        label(d.child(N(f"Artifacts/Detail/Crystal{i}", f"Crystal{i}", TR((4 - i) * 128, 24, 120, 24))), "", "Body",
              "TextLight", align="right")
    for i, name in enumerate(TRACK_ELEMENTS):
        k = f"Artifacts/Detail/Track{name}"
        t = d.child(N(k, f"Track{name}", TL(0, TRACK_Y + i * TRACK_STEP, COL_W, TRACK_H)))
        H.framed(t, k, 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.9)
        label(t.child(N(f"{k}/Element", "Element", TL(24, 18, 200, 16))), name, "Label", "NeutralLightest")
        H.ellipsize(label(t.child(N(f"{k}/Label", "Label", TL(24, 38, 520, 30))), "", "H3", "TextLight"))
        label(t.child(N(f"{k}/Level", "Level", TL(560, 38, 160, 30))), "", "H3", "TextLight")
        label(t.child(N(f"{k}/Value", "Value", TL(740, 42, 300, 24))), "", "Body", "NeutralLightest")
        tw = t.child(N(f"{k}/Twist", "Twist", TL(24, 84, 1000, 56)))
        tw.add("cg", 225, canvas_group(True))
        label(tw.child(N(f"{k}/Twist/Words", "Words", STRETCH)), "", "BodySmall", "TextLight", valign="top", wrap=True)
        small_button(t, f"{k}/Raise", "Raise", "Raise", TR(24, 24, 260, 52), "Cta")
        small_button(t, f"{k}/Omni", "Omni", "Omni", TR(24, 88, 260, 44))
    label(d.child(N("Artifacts/Detail/Status", "Status", TL(0, TRACK_Y + 4 * TRACK_STEP, COL_W, 24))), "", "Body", "TextLight")

    def body(ctx, go):
        tracks = ""
        for name in TRACK_ELEMENTS:
            k = f"Artifacts/Detail/Track{name}"
            tracks += (f"  - elementText: {ref(ctx.c(f'{k}/Element', 'tmp'))}\n    labelText: {ref(ctx.c(f'{k}/Label', 'tmp'))}\n"
                       f"    levelText: {ref(ctx.c(f'{k}/Level', 'tmp'))}\n    valueText: {ref(ctx.c(f'{k}/Value', 'tmp'))}\n"
                       f"    twistText: {ref(ctx.c(f'{k}/Twist/Words', 'tmp'))}\n    twistGroup: {ref(ctx.c(f'{k}/Twist', 'cg'))}\n"
                       f"    raiseButton: {ref(ctx.c(f'{k}/Raise', 'button'))}\n    raiseLabel: {ref(ctx.c(f'{k}/Raise/Label', 'tmp'))}\n"
                       f"    omniButton: {ref(ctx.c(f'{k}/Omni', 'button'))}\n    omniLabel: {ref(ctx.c(f'{k}/Omni/Label', 'tmp'))}\n")
        return mono(go, S["artifacts"],
                    f"  catalog: {K.ref(11400000, PILOT_CATALOG, 2)}\n"
                    "  rows:\n" + refs_of(ctx, [f"Artifacts/List/Row{i}" for i in range(ART_ROWS)], "row")
                    + f"  emptyGroup: {ref(ctx.c('Artifacts/Empty', 'cg'))}\n"
                    f"  detailGroup: {ref(ctx.c('Artifacts/Detail', 'cg'))}\n"
                    f"  nameText: {ref(ctx.c('Artifacts/Detail/Name', 'tmp'))}\n"
                    f"  descriptionText: {ref(ctx.c('Artifacts/Detail/Description', 'tmp'))}\n"
                    "  tracks:\n" + tracks
                    + f"  statusText: {ref(ctx.c('Artifacts/Detail/Status', 'tmp'))}\n"
                    "  crystalTexts:\n" + refs_of(ctx, [f"Artifacts/Detail/Crystal{i}" for i in range(5)], "tmp")
                    + ARTIFACT_WORDS)
    page.add("logic", 114, body)
    return page


def decor_page(root):
    """DECOR, locked (its tab refuses): the banner and the Ark's hull, outside and in. Drawn so that opening the tab is
    the one change (MenuAvailability.Available on the tab); nothing behind it yet."""
    page = page_node(root, "Decor", False)
    page_backdrop(page, "Decor")
    title_band(page, "Decor/Title", "Decor", "The Ark's banner and its hull, outside and in.")
    pn = page.child(N("Decor/Slots", "Slots", stretch_x(COL_X, 40, 180, 320)))
    C.frame(pn, "Decor/Slots/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    for i, name in enumerate(("Banner", "Outer hull", "Inner hull")):
        k = f"Decor/Slots/{name.replace(' ', '')}"
        l, r = (28 if i == 0 else 8), (28 if i == 2 else 8)
        tile = pn.child(N(k, name.replace(" ", ""), R((i / 3, 1), ((i + 1) / 3, 1), ((l - r) / 2, -28), (-(l + r), 264),
                                                      (0.5, 1))))
        H.framed(tile, k, 10, "SurfaceBlack", "SurfaceDark", fill_alpha=0.85)
        label(tile.child(N(f"{k}/Name", "Name", stretch_x(18, 18, 20, 30))), name, "H3", "TextLight")
        label(tile.child(N(f"{k}/State", "State", stretch_x(18, 18, 220, 18))), "Locked", "Label", "NeutralLightest")
    return page


def build_view():
    root = N("root", VIEW, STRETCH)
    # The converter's overlay keeps its node keys (and so every fileID the scene's stubs point at) under its page.
    conv = page_node(root, "Converter", True)
    title_band(conv)
    recipe_panel(conv)
    converter_bar(conv)
    squads_page(root)
    artifacts_page(root)
    decor_page(root)
    tab_row(root)

    def body(ctx, go):
        entries = ""
        for t in TABS:
            logic = {"Squads": ref(ctx.c("Squads", "logic")), "Artifacts": ref(ctx.c("Artifacts", "logic"))}.get(t, "{fileID: 0}")
            gate = ref(ctx.c(f"Tabs/{t}", "gate")) if t in LOCKED_TABS else "{fileID: 0}"
            entries += (f"  - button: {ref(ctx.c(f'Tabs/{t}', 'tab'))}\n    page: {ref(ctx.c(t, 'cg'))}\n"
                        f"    pageLogic: {logic}\n    availability: {gate}\n")
        return mono(go, S["tabs"], "  tabs:\n" + entries + "  defaultTab: 0\n")
    root.add("tabs", 114, body)
    return root


def prefab_outputs():
    out = {}
    out[f"{OUT_DIR}/{VIEW}.prefab"] = P.prefab_text(build_view(), VIEW)
    out[f"{OUT_DIR}/{VIEW}.prefab.meta"] = K.PREFAB_META.format(g=P.asset_guid(VIEW))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=P.asset_guid("folder/Ark"))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

IDS = dict(go="7700130000000000001", rt="7700130000000000002", view="7700130000000000003")
VIEW_REFS = {
    "convertButton": ("ConvertButton|root#button", "button"),
    "convertLabel": ("ConvertButton|Label#tmp", "tmp"),
    "statusText": ("Bar/Status#kt", "kt"),
    "clockText": ("Bar/Clock#tmp", "tmp"),
    "mergedText": ("Recipe/Merged#kt", "kt"),
    "omniGroup": ("Recipe/Omni#cg", "cg"),
    "omniPop": ("Recipe/Omni#pop", "entrance"),
    "arkTabs": ("root#tabs", "tabs"),
}
LISTS = {
    "segments": [(f"Bar/Seg{n}#img", "img") for n in ORDER],
    "segmentSparks": [(f"Bar/Seg{n}/SparkGroup#cg", "cg") for n in ORDER],
    "recipeRows": [(f"Recipe/{n}#row", "recipe") for n in ORDER],
}
OVERLAY_ORDER = ("convertButton", "convertLabel", "statusText", "clockText", "segments", "segmentSparks",
                 "recipeRows", "mergedText", "omniGroup", "omniPop", "arkTabs")


class ArkMount(C.Mount):
    def __init__(self):
        self.view = VIEW
        self.ctx = P.Ctx("Menu_Main/Ark")
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = P.asset_guid(VIEW)
        self.kind = None

    def src(self, key):
        V = K.FileIds(VIEW)
        if "|" in key:
            inst, src_key = key.split("|")
            return C.derived(K.source_fid("KitButton", src_key), V(inst + "#inst"))
        return V(key)

    def refs(self):
        return list(VIEW_REFS.values()) + [x for items in LISTS.values() for x in items]

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", VIEW, self.guid, VIEW, STRETCH, mods), parent_rt, out)
        for key, comp in self.refs():
            out.append(self.stub_text(key, comp))
        return [K.unity_empty_scalars(d) for d in out]

    def ids(self):
        return {self.inst, self.stub} | {self.stub_fid(k) for k, _ in self.refs()}


def rebuild_view_doc(doc, mount):
    """ElementalConverterView: config and font kept, CONVERT's resting label, then the overlay fields."""
    head = doc[:doc.index("  config:")]
    config = re.search(r"^  config: .*\n", doc, re.M).group(0)
    font = re.search(r"^  font: .*\n", doc, re.M).group(0)
    body = config + "  buttonLabel: Convert\n" + font
    for k in OVERLAY_ORDER:
        if k in LISTS:
            body += K.refs_list(k, [mount.stub_fid(key) for key, _ in LISTS[k]])
        else:
            body += f"  {k}: {ref(mount.stub_fid(VIEW_REFS[k][0]))}\n"
    return head + body + "  converterTab: 0\n"


def scene_transform(text_):
    sc = T.Scene(text_)
    assert sc.field(IDS["go"], "m_Name") == "ElementalConverter", "the converter moved"
    assert sc.script(IDS["view"]) == S["view"], "ElementalConverterView moved"
    mount = ArkMount()
    for fid in list(sc.order):
        if fid in mount.ids():
            sc.remove(fid)
    rt = IDS["rt"]
    anchor = rt
    for d in mount.docs(rt):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    doc = T.set_block(sc.docs[rt], "m_Children", K.refs_list("m_Children", [mount.stub]))
    doc = C.field_set(doc, "m_AnchoredPosition", f"{{x: 0, y: {K.f32(TOP_GROW / 2)}}}")
    doc = C.field_set(doc, "m_SizeDelta", f"{{x: 0, y: {K.f32(TOP_GROW)}}}")
    sc.replace(rt, doc)
    sc.replace(IDS["view"], rebuild_view_doc(sc.docs[IDS["view"]], mount))
    return sc.text(), mount


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def prefab_source(guid, files):
    if guid == P.asset_guid(VIEW):
        return files[f"{OUT_DIR}/{VIEW}.prefab"]
    for name in C.KIT_SOURCES:
        if K.prefab_guid(name) == guid:
            return open(os.path.join(REPO, K.PREFAB_DIR, name + ".prefab"), encoding="utf-8").read()
    return None


def prefab_objects(guid, files, memo):
    if guid in memo:
        return memo[guid]
    text_ = prefab_source(guid, files)
    objs = A.anchors_of(text_) if text_ is not None else None
    if objs is not None:
        for m in re.finditer(r"^--- !u!1001 &(-?\d+)\n(.*?)(?=^--- |\Z)", text_, re.M | re.S):
            pid = int(m.group(1))
            src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", m.group(2)).group(1)
            inner = prefab_objects(src, files, memo)
            if inner is not None:
                objs |= {str(C.derived(o, pid)) for o in inner}
    memo[guid] = objs
    return objs


def check_sources(path, text_, files, memo):
    for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                             r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", text_):
        objs = prefab_objects(g, files, memo)
        if objs is not None:
            assert src in objs, f"{path}: source object {src} is not in the prefab {g}"


TMP, IMG = K.TMP_UGUI, K.IMAGE
ROW_FIELDS = dict(stateText=S["kt"], fill=IMG, border=IMG, art=IMG, bob=S["ambient"])


def validate(files):
    guids = T.known_guids() | P.PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    memo = {}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)
        check_sources(path, text_, files, memo)

    view = files[f"{OUT_DIR}/{VIEW}.prefab"]
    V = K.FileIds(VIEW)
    for n in ORDER:
        _, body = A.doc_of(view, V(f"Recipe/{n}#row"))
        assert body and S["row"] in body, f"ArkView: the {n} row carries no ConverterRecipeRow"
        for field, want in ROW_FIELDS.items():
            C.wired(view, body, field, want, f"the {n} row")
        assert f"guid: {CRYSTAL[n]}" in A.doc_of(view, V(f"Recipe/{n}/Art#img"))[1], f"the {n} row shows the wrong crystal"
        _, seg = A.doc_of(view, V(f"Bar/Seg{n}#img"))
        assert "  m_Type: 3\n" in seg and "  m_FillMethod: 0\n" in seg, f"the {n} segment is not a horizontal fill"
    assert view.count("guid: " + S["spark"]) == len(ORDER) + 1, "ArkView needs a spark per segment and the CONVERT sweep"
    validate_tabs(view, V)

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files, memo)
    mount = ArkMount()
    doc = sc.docs[IDS["view"]]
    for field, (key, kind) in VIEW_REFS.items():
        fid = mount.stub_fid(key)
        assert f"  {field}: {{fileID: {fid}}}" in doc, f"ElementalConverterView.{field} does not point into the overlay"
        cls, scr = C.STUB_KIND[kind]
        assert sc.cls.get(fid) == cls and (scr is None or sc.script(fid) == scr), f"ElementalConverterView.{field} stub is the wrong kind"
    for field, items in LISTS.items():
        assert K.refs_list(field, [mount.stub_fid(k) for k, _ in items]) in doc, f"ElementalConverterView.{field} is not the four elements"
    assert re.search(r"^  config: \{fileID: 11400000, guid: [0-9a-f]{32}", doc, re.M), "the converter lost its config"
    rt = sc.docs[IDS["rt"]]
    assert K.refs_list("m_Children", [mount.stub]) in rt, "the converter holds more than its overlay"
    assert f"m_SizeDelta: {{x: 0, y: {K.f32(TOP_GROW)}}}" in rt, "the converter must fill its screen exactly (ScreenSwitcher already gives the screen the full height)"
    assert re.search(r"^  converterTab: 0$", doc, re.M), "ElementalConverterView.converterTab is not the Converter tab"


MATE_FIELDS = dict(button=S["button"], editMark=IMG, seatText=TMP, numberText=TMP, whoText=TMP, vesselText=TMP,
                   elementText=TMP, pilotText=TMP, artifactText=TMP, hullImage=IMG, pilotImage=IMG, openSeatGroup=225,
                   nanobots=S["nanobots"])
SQUADS_FIELDS = dict(countText=S["kt"], newButton=S["button"], joinButton=S["button"], headerGroup=225, kickerText=TMP,
                     squadNameText=TMP, squadLineText=TMP, deckRoot=224, emptyGroup=225, emptyMakeButton=S["button"],
                     pickerGroup=225, vesselPrevButton=S["button"], vesselNextButton=S["button"], pilotPrevButton=S["button"],
                     pilotNextButton=S["button"], seatModeGroup=225,
                     makeBarGroup=225, cancelButton=S["button"], createButton=S["button"], createLabel=TMP,
                     viewBarGroup=225, removeButton=S["button"], removeLabel=TMP, editButton=S["button"],
                     missionButton=S["button"], missionLock=IMG, arenaButton=S["button"],
                     joinGroup=225, joinStatusText=S["kt"], joinCloseButton=S["button"])
ART_FIELDS = dict(emptyGroup=225, detailGroup=225, nameText=TMP, descriptionText=TMP, statusText=TMP)
TRACK_FIELDS = dict(elementText=TMP, labelText=TMP, levelText=TMP, valueText=TMP, twistText=TMP, twistGroup=225,
                    raiseButton=S["button"], raiseLabel=TMP, omniButton=S["button"], omniLabel=TMP)
ROW_VIEW_FIELDS = dict(button=S["button"], group=225, titleText=TMP, lineText=TMP, activeMark=IMG, domainText=TMP)
CHIP_FIELDS = dict(button=S["button"], group=225, label=TMP, selectedMark=IMG)
NANOBOT_FIELDS = dict(hull=IMG, companion=IMG, botTemplate=IMG)
# Rev 31: Take into is Mission and Arena only (Shombith, 2026-10-27: "Yes keep it out"); the first cut's fields are gone.
RETIRED_SQUAD_FIELDS = ("playArcadeButton", "playArenaButton", "playMissionButton", "detailGroup")
SQUAD_LISTS = (("squadRows", MAX_SQUADS, "squad_row", ROW_VIEW_FIELDS), ("mateCards", SEATS, "mate", MATE_FIELDS),
               ("joinRows", JOIN_ROWS, "squad_row", ROW_VIEW_FIELDS), ("domainChips", len(DOMAIN_ORDER), "chip", CHIP_FIELDS),
               ("vesselTiles", VESSEL_TILES, "chip", CHIP_FIELDS), ("pilotChips", PILOT_CHIPS, "chip", CHIP_FIELDS),
               ("seatModeChips", 2, "chip", CHIP_FIELDS), ("elementChips", ELEMENT_CHIPS, "chip", CHIP_FIELDS),
               ("artifactChips", ARTIFACT_CHIPS, "chip", CHIP_FIELDS))


def listed(body, field):
    m = re.search(r"^  " + field + r":\n((?:  - .*\n)*)", body, re.M)
    assert m, f"{field} is missing"
    return re.findall(r"\{fileID: (-?\d+)\}", m.group(1))


def validate_tabs(view, V):
    """The Ark's three tabs: each a KitTextTab over a CanvasGroup page, SQUADS driven by its page, DECOR locked."""
    _, tabs = A.doc_of(view, V("root#tabs"))
    assert tabs and S["tabs"] in tabs, "ArkView's root carries no ArkTabsView"
    entries = re.findall(r"  - button: \{fileID: (-?\d+)\}\n    page: \{fileID: (-?\d+)\}\n"
                         r"    pageLogic: \{fileID: (-?\d+)\}\n    availability: \{fileID: (-?\d+)\}\n", tabs)
    assert len(entries) == len(TABS), f"the Ark has {len(entries)} tabs, not {len(TABS)} ({', '.join(TABS)})"
    # The pages with no 3D picture draw the window backdrop: the veil, then the sea lines, under everything else.
    for t in ("Squads", "Artifacts", "Decor"):
        kids = re.findall(r"fileID: (-?\d+)", re.search(r"^  m_Children:\n((?:  - .*\n)*)", A.doc_of(view, V(f"{t}#rt"))[1], re.M).group(1))
        assert kids[:2] == [str(V(f"{t}/Backdrop#rt")), str(V(f"{t}/Sea#rt"))], \
            f"the {t} tab must open on the veil and the sea lines (\"Ark no sea line\")"
        assert f"guid: {M.SEA_WINDOW_MAT}" in A.doc_of(view, V(f"{t}/Sea#img"))[1], f"the {t} tab's sea is not the window sea"
    for t, (button, page, logic, gate) in zip(TABS, entries):
        assert A.kind_of(view, button)[1] == S["text_tab"], f"the {t} tab's button is not a KitTextTab"
        assert A.kind_of(view, page)[0] == 225, f"the {t} tab's page is not a CanvasGroup"
        if t == "Squads":
            assert logic != "0" and A.kind_of(view, logic)[1] == S["squads"], "the Squads tab is not driven by its SquadsPage"
        if t == "Artifacts":
            assert logic != "0" and A.kind_of(view, logic)[1] == S["artifacts"], "the Artifacts tab is not driven by its ArkArtifactsPage"
        if t in LOCKED_TABS:
            assert gate != "0", f"the {t} tab has no MenuAvailabilityView: it would open"
            _, g = A.doc_of(view, gate)
            assert S["availability"] in g and "  availability: 1\n" in g, f"the {t} tab is not Locked"
        else:
            assert gate == "0", f"the {t} tab is gated"
    _, art = A.doc_of(view, V("Artifacts#logic"))
    for field, want in ART_FIELDS.items():
        C.wired(view, art, field, want, "ArkArtifactsPage")
    rows_ = listed(art, "rows")
    assert len(rows_) == ART_ROWS and all(A.kind_of(view, i)[1] == S["squad_row"] for i in rows_), \
        f"ArkArtifactsPage.rows is not {ART_ROWS} SquadRowViews"
    assert len(listed(art, "crystalTexts")) == 5, "ArkArtifactsPage.crystalTexts is not the five crystals"
    for field, want in TRACK_FIELDS.items():
        found = re.findall(r"^  [- ] " + field + r": \{fileID: (-?\d+)\}", art, re.M)
        assert len(found) == len(TRACK_ELEMENTS), f"ArkArtifactsPage has {len(found)} track {field}s, not {len(TRACK_ELEMENTS)}"
        for fid in found:
            assert fid != "0", f"an ArkArtifactsPage track's {field} is not wired"
            cls, scr = A.kind_of(view, fid)
            assert (cls == want if isinstance(want, int) else scr == want), f"an ArkArtifactsPage track's {field} is the wrong kind"
    assert f"guid: {PILOT_CATALOG}" in art, "ArkArtifactsPage reads no pilot catalog"
    _, page = A.doc_of(view, V("Squads#logic"))
    for field, want in SQUADS_FIELDS.items():
        C.wired(view, page, field, want, "SquadsPage")
    for field in RETIRED_SQUAD_FIELDS:
        assert not re.search(r"^  " + field + ":", page, re.M), f"SquadsPage.{field} is back (Take into is Mission and Arena)"
    for field, n, kind, fields in SQUAD_LISTS:
        ids = listed(page, field)
        assert len(ids) == n and all(i != "0" and A.kind_of(view, i)[1] == S[kind] for i in ids), f"SquadsPage.{field} is not {n} wired"
        for i in ids:
            _, body = A.doc_of(view, i)
            for f, want in fields.items():
                C.wired(view, body, f, want, field)
            if field == "mateCards":
                swap = re.search(r"^  nanobots: \{fileID: (-?\d+)\}", body, re.M).group(1)
                for f, want in NANOBOT_FIELDS.items():
                    C.wired(view, A.doc_of(view, swap)[1], f, want, "the card's nanobots")
    assert re.search(r"^  domainOrder:\n  - 1\n  - 2\n  - 4\n", page, re.M), "SquadsPage.domainOrder is not Jade, Ruby, Gold"
    for guid, what in ((VESSEL_LIST, "the vessel list"), (PILOT_CATALOG, "the pilot catalog"), (CARD_ART, "the hull renders"),
                       (COLOR_SET, "the domain colours")):
        assert f"guid: {guid}" in page, f"SquadsPage reads no {what}"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def plain_segment(view):
    """The Space segment's Image turned back into a Simple image (the self-test's plant)."""
    fid = str(K.FileIds(VIEW)("Bar/SegSpace#img"))
    m = re.search(r"^--- !u!114 &" + fid + r"\n.*?(?=^--- )", view, re.M | re.S)
    return view[:m.start()] + m.group(0).replace("  m_Type: 3\n", "  m_Type: 0\n") + view[m.end():]


def unlogic_tab(view, index):
    """The view with tab `index`'s pageLogic cut (a negative control)."""
    n = [0]

    def cut(m):
        n[0] += 1
        return m.group(1) + ("{fileID: 0}" if n[0] - 1 == index else m.group(2))
    return re.sub(r"(  - button: \{fileID: -?\d+\}\n    page: \{fileID: -?\d+\}\n    pageLogic: )(\{fileID: -?\d+\})", cut, view)


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
    view = f"{OUT_DIR}/{VIEW}.prefab"
    mount = ArkMount()
    cases = [
        ("a recipe row with no bar", lambda f: f.__setitem__(view, re.sub(r"(  fill: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("a segment that is a plain image", lambda f: f.__setitem__(view, plain_segment(f[view]))),
        ("CONVERT unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  convertButton: {{fileID: {mount.stub_fid('ConvertButton|root#button')}}}", "  convertButton: {fileID: 0}"))),
        ("the converter grown up under the nav again", lambda f: f.__setitem__(SCENE, re.sub(
            r"(--- !u!224 &" + IDS["rt"] + r"\n(?:(?!--- ).*\n)*?  m_SizeDelta: )\{x: 0, y: 0\}", r"\1{x: 0, y: 170.4}",
            f[SCENE], count=1))),
        ("the wrong crystal on a row", lambda f: f.__setitem__(view, f[view].replace(CRYSTAL["Mass"], CRYSTAL["Time"], 1))),
        ("the Squads tab without its sea lines", lambda f: f.__setitem__(view, f[view].replace(
            f"guid: {M.SEA_WINDOW_MAT}", f"guid: {M.SEA_PANEL_MAT}", 1))),
        ("Decor opened", lambda f: f.__setitem__(view, f[view].replace("  availability: 1\n", "  availability: 0\n", 1))),
        ("a card with no hull", lambda f: f.__setitem__(view, re.sub(
            r"(  hullImage: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("Take into Arcade back", lambda f: f.__setitem__(view, f[view].replace(
            "  missionButton: ", "  playArcadeButton: {fileID: 0}\n  missionButton: ", 1))),
        ("the nanobots with no bot", lambda f: f.__setitem__(view, re.sub(
            r"(  botTemplate: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("an artifact track's RAISE unwired", lambda f: f.__setitem__(view, re.sub(
            r"(    raiseButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("the Artifacts tab with no page logic", lambda f: f.__setitem__(view, unlogic_tab(f[view], TABS.index("Artifacts")))),
        ("the Ark's arkTabs unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  arkTabs: {{fileID: {mount.stub_fid('root#tabs')}}}", "  arkTabs: {fileID: 0}"))),
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
    print("OK: ArkView and Menu_Main validate")
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
