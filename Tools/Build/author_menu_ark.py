#!/usr/bin/env python3
"""
Author ITEM 7 of the style-B menu redesign - the ARK screen's ELEMENTAL CONVERTER overlay - as a prefab,
and mount it in Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 16; the approved Ark / ArkConverting
boards, revision 2, on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Ark/
        ArkView.prefab   the Ark's three tabs (ArkTabsView: CONVERTER, SQUADS, DECOR - Shombith, 2026-10-27) and
                         their pages:
                         CONVERTER, the overlay over the converter's 3D picture: the title band, the recipe panel
                         (four ConverterRecipeRows in fly-in order, the Omni block), and the converter bar
                         (the run's clock, four segments with their sparks, CONVERT with the sweep);
                         SQUADS (SquadsPage): the player's squads (up to four, made and joined), the selected
                         squad's three squad mates with their vessel, element, pilot and artifact, NEW / JOIN /
                         DELETE, PLAY into Arcade / Arena / Mission, and the join list of friends' open seats;
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
         availability=C.script("MenuAvailabilityView"), text_tab=K.kit_script("KitTextTab"))
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

TABS = ("Converter", "Squads", "Decor")
LOCKED_TABS = {"Decor"}                       # MenuAvailability.Locked: refuses with the sting and "Not open yet."
TAB_Y, TAB_H = 108, 52
MAX_SQUADS, SEATS, JOIN_ROWS = 4, 3, 6        # SquadBook.MaxSquads, SavedSquad.SlotCount, the join list's rows
PICKS = ("Vessel", "Element", "Pilot", "Artifact")
PICK_IMAGE = {"Vessel", "Pilot", "Artifact"}  # the element is a word: an element has no colour (LOCKED)
VESSEL_LIST = K.meta_guid("Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset")   # the Hangar's roster
PILOT_CATALOG = K.meta_guid("Assets/Resources/Pilots/PilotArtifactCatalog.asset")
LIST_X, LIST_Y, LIST_W, LIST_H = 40, 244, 514, 640
DETAIL_L, DETAIL_R, DETAIL_Y, DETAIL_H = 574, 40, 180, 800
CARD_Y, CARD_H, PICK_Y, PICK_STEP, PICK_H = 112, 540, 92, 110, 100
PLAY_Y = 672
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


def kit_row_text(parent, key, name, r, h):
    """A selectable row (the Hangar's kit_row) with a name, a line and the active mark down its left edge."""
    row = H.kit_row(parent, key, name, r, h, content_keys=((f"{key}/Title", "tmp"),))
    row.add("cg", 225, canvas_group(True))
    image_node(row, f"{key}/Mark", "Mark", TL(0, 14, 4, h - 28), "ControlActive")
    H.ellipsize(label(row.child(N(f"{key}/Title", "Title", stretch_x(20, 20, 12, 30))), "", "H3", "TextLight"))
    H.ellipsize(label(row.child(N(f"{key}/Line", "Line", stretch_x(20, 20, 46, 22))), "", "BodySmall", "NeutralLightest"))
    row.add("row", 114, lambda ctx, go: mono(go, S["squad_row"],
            f"  button: {ref(ctx.c(key, 'button'))}\n  group: {ref(ctx.c(key, 'cg'))}\n"
            f"  titleText: {ref(ctx.c(f'{key}/Title', 'tmp'))}\n  lineText: {ref(ctx.c(f'{key}/Line', 'tmp'))}\n"
            f"  activeMark: {ref(ctx.c(f'{key}/Mark', 'img'))}\n"))
    return row


def small_button(parent, key, name, caption, r, variant="Default"):
    return H.text_button(parent, key, name, caption, r, None, None, None, None, variant=variant)


def squad_list(page):
    pn = page.child(N("Squads/List", "List", TL(LIST_X, LIST_Y, LIST_W, LIST_H)))
    pn.add("cg", 225, canvas_group(True))
    C.frame(pn, "Squads/List/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    M.bracket_nodes(pn, "Squads/List/Bracket")
    C.section_label(pn, "Squads/List/Caption", "Caption", "Squads", TL(26, 26, 200, 18))
    label(pn.child(N("Squads/List/Count", "Count", TR(26, 26, 220, 18))), "0 of 4 squads", "Label", "NeutralLightest",
          align="right")
    for i in range(MAX_SQUADS):
        kit_row_text(pn, f"Squads/List/Row{i}", f"Row{i}", TL(26, 58 + i * 100, LIST_W - 52, 88), 88)
    small_button(pn, "Squads/List/New", "New", "New squad", TL(26, 470, 222, 56), "Cta")
    small_button(pn, "Squads/List/Join", "Join", "Join a friend", TL(266, 470, 222, 56))
    label(pn.child(N("Squads/List/Hint", "Hint", TL(26, 546, LIST_W - 52, 70))),
          "Make a squad, or join a friend's. You can be in four at once, and pick one before you play.",
          "BodySmall", "NeutralLightest", valign="top", wrap=True)


def mate_card(detail, i):
    k = f"Squads/Detail/Mate{i}"
    l, r = (28 if i == 0 else 8), (28 if i == SEATS - 1 else 8)
    card = detail.child(N(k, f"Mate{i}", R((i / SEATS, 1), ((i + 1) / SEATS, 1), ((l - r) / 2, -CARD_Y),
                                         (-(l + r), CARD_H), (0.5, 1))))
    H.framed(card, k, 10, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.85)
    label(card.child(N(f"{k}/Seat", "Seat", TL(18, 16, 200, 16))), ("Leader", "Rogue One", "Rogue Two")[i], "Label",
          "NeutralLightest")
    H.ellipsize(label(card.child(N(f"{k}/Member", "Member", stretch_x(18, 150, 36, 30))), "", "H3", "TextLight"))
    small_button(card, f"{k}/SeatButton", "SeatButton", "Open seat", TR(12, 12, 132, 36))
    image_node(card, f"{k}/Rule", "Rule", stretch_x(18, 18, 80, 1), "SurfaceDark")
    for j, pick in enumerate(PICKS):
        pk = f"{k}/{pick}"
        row = card.child(N(pk, pick, stretch_x(14, 14, PICK_Y + j * PICK_STEP, PICK_H)))
        x = 14
        if pick in PICK_IMAGE:
            row.child(N(f"{pk}/Image", "Image", TL(6, 14, 72, 72))).add("cr", 222, K.canvas_renderer()) \
                .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True))
            x = 92
        label(row.child(N(f"{pk}/Kicker", "Kicker", TL(x, 12, 160, 16))), pick, "Label", "NeutralLightest")
        H.ellipsize(label(row.child(N(f"{pk}/Value", "Value", stretch_x(x, 14, 30, 26))), "", "Body", "TextLight"))
        small_button(row, f"{pk}/Change", "Change", "Change", TR(6, 60, 116, 34))

    def body(ctx, go):
        out = (f"  seatText: {ref(ctx.c(f'{k}/Seat', 'tmp'))}\n  memberText: {ref(ctx.c(f'{k}/Member', 'tmp'))}\n"
               f"  seatButton: {ref(ctx.c(f'{k}/SeatButton', 'button'))}\n"
               f"  seatButtonLabel: {ref(ctx.c(f'{k}/SeatButton/Label', 'tmp'))}\n")
        for pick in PICKS:
            low = pick.lower()
            out += f"  {low}Button: {ref(ctx.c(f'{k}/{pick}/Change', 'button'))}\n"
            if pick in PICK_IMAGE:
                out += f"  {low}Image: {ref(ctx.c(f'{k}/{pick}/Image', 'img'))}\n"
            out += f"  {low}Text: {ref(ctx.c(f'{k}/{pick}/Value', 'tmp'))}\n"
        return mono(go, S["mate"], out)
    card.add("mate", 114, body)


def squad_detail(page):
    d = page.child(N("Squads/Detail", "Detail", stretch_x(DETAIL_L, DETAIL_R, DETAIL_Y, DETAIL_H)))
    d.add("cg", 225, canvas_group(False))
    C.frame(d, "Squads/Detail/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    M.bracket_nodes(d, "Squads/Detail/Bracket")
    H.ellipsize(label(d.child(N("Squads/Detail/Name", "Name", stretch_x(28, 260, 24, 44))), "", "H1", "TextLight"))
    H.ellipsize(label(d.child(N("Squads/Detail/Line", "Line", stretch_x(28, 260, 74, 22))), "", "BodySmall",
                      "NeutralLightest"))
    small_button(d, "Squads/Detail/Remove", "Remove", "Delete squad", TR(28, 24, 210, 48), "Danger")
    for i in range(SEATS):
        mate_card(d, i)
    label(d.child(N("Squads/Detail/PlayCaption", "PlayCaption", TL(28, PLAY_Y, 400, 18))), "Take this squad into",
          "Label", "NeutralLightest")
    for j, (name, caption, variant) in enumerate((("Arcade", "Arcade", "Play"), ("Arena", "Arena", "Default"),
                                                  ("Mission", "Mission", "Default"))):
        small_button(d, f"Squads/Detail/Play{name}", f"Play{name}", caption, TL(28 + j * 216, PLAY_Y + 28, 200, 60), variant)


def squad_empty(page):
    e = page.child(N("Squads/Empty", "Empty", stretch_x(DETAIL_L, DETAIL_R, DETAIL_Y, DETAIL_H)))
    e.add("cg", 225, canvas_group(True))
    C.frame(e, "Squads/Empty/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    label(e.child(N("Squads/Empty/Heading", "Heading", stretch_x(28, 28, 300, 40))), "No squad yet", "H2", "TextLight",
          align="center")
    label(e.child(N("Squads/Empty/Line", "Line", stretch_x(28, 28, 350, 60))),
          "A squad is you and two squad mates - friends, AI wingmates, or both. Make one, or join a friend's.",
          "Body", "NeutralLightest", align="center", valign="top", wrap=True)


def squad_join(page):
    j = page.child(N("Squads/Joining", "Joining", stretch_x(DETAIL_L, DETAIL_R, DETAIL_Y, DETAIL_H)))
    j.add("cg", 225, canvas_group(False))
    C.frame(j, "Squads/Joining/Frame", 14, "SurfaceBlack", "SurfaceDark", fill_alpha=0.98)
    M.bracket_nodes(j, "Squads/Joining/Bracket")
    label(j.child(N("Squads/Joining/Heading", "Heading", TL(28, 24, 600, 40))), "Join a friend's squad", "H2", "TextLight")
    small_button(j, "Squads/Joining/Close", "Close", "Close", TR(28, 24, 160, 48))
    label(j.child(N("Squads/Joining/Status", "Status", stretch_x(28, 28, 80, 22))), "", "BodySmall", "NeutralLightest")
    for i in range(JOIN_ROWS):
        kit_row_text(j, f"Squads/Joining/Row{i}", f"Row{i}", stretch_x(28, 28, 120 + i * 96, 84), 84)


SQUAD_WORDS = (
    yfield("countLine", "{count} of {max} squads") + yfield("madeLine", "Yours")
    + yfield("joinedLine", "{owner}'s squad") + yfield("activeSuffix", " - active")
    + yfield("madeDetailLine", "You lead this squad. {open} open for friends.")
    + yfield("seatedLine", "You fly in {owner}'s squad.")
    + yfield("pendingLine", "Joined. {owner} seats you when they next open the Ark.")
    + yfield("seatLostLine", "{owner} gave your seat to someone else. Leave the squad, or ask them to open it again.")
    + yfield("disbandedLine", "{owner} deleted this squad.")
    + yfield("offlineLine", "Offline - showing the last copy of {owner}'s squad.")
    + ylist("seatNames", ("Leader", "Rogue One", "Rogue Two"))
    + yfield("youLine", "You") + yfield("aiLine", "AI wingmate") + yfield("openLine", "Open for a friend")
    + yfield("unknownFriendLine", "A friend") + yfield("openSeatAction", "Open seat") + yfield("aiSeatAction", "Use AI")
    + yfield("removeFriendAction", "Remove")
    + ylist("elementNames", ("", "Charge", "Mass", "Space", "Time", "Omni"))
    + yfield("noPilotLine", "Base kit") + yfield("noArtifactLine", "No artifact")
    + yfield("deleteLabel", "Delete squad") + yfield("leaveLabel", "Leave squad") + yfield("confirmLabel", "Press again")
    + yfield("newSquadName", "") + yfield("fullLine", "You are in 4 squads. Leave or delete one first.")
    + yfield("noSquadLine", "Make or join a squad first.")
    + yfield("joinLookingLine", "Looking at your friends' squads...")
    + yfield("joinNoneLine", "No friend has a seat open right now.")
    + yfield("joinOfflineLine", "Sign in to join a friend's squad.")
    + yfield("joinRowTitle", "{squad}") + yfield("joinRowLine", "{owner} - {seat} open")
    + yfield("joinedToast", "You joined {owner}'s {squad}.")
    + yfield("alreadyInLine", "You are already in that squad.") + yfield("seatGoneLine", "That seat was just taken."))


def squads_page(root):
    page = page_node(root, "Squads", False)
    title_band(page, "Squads/Title", "Squads", "You and two squad mates: pick their vessel, element, pilot and artifact.")
    squad_list(page)
    squad_empty(page)
    squad_detail(page)
    squad_join(page)

    def body(ctx, go):
        rows = "".join(f"  - {ref(ctx.c(f'Squads/List/Row{i}', 'row'))}\n" for i in range(MAX_SQUADS))
        mates = "".join(f"  - {ref(ctx.c(f'Squads/Detail/Mate{i}', 'mate'))}\n" for i in range(SEATS))
        joins = "".join(f"  - {ref(ctx.c(f'Squads/Joining/Row{i}', 'row'))}\n" for i in range(JOIN_ROWS))
        return mono(go, S["squads"],
                    f"  vessels: {K.ref(11400000, VESSEL_LIST, 2)}\n  catalog: {K.ref(11400000, PILOT_CATALOG, 2)}\n"
                    f"  squadRows:\n{rows}  countText: {ref(ctx.c('Squads/List/Count', 'kt'))}\n"
                    f"  newButton: {ref(ctx.c('Squads/List/New', 'button'))}\n"
                    f"  joinButton: {ref(ctx.c('Squads/List/Join', 'button'))}\n"
                    f"  detailGroup: {ref(ctx.c('Squads/Detail', 'cg'))}\n  emptyGroup: {ref(ctx.c('Squads/Empty', 'cg'))}\n"
                    f"  squadNameText: {ref(ctx.c('Squads/Detail/Name', 'tmp'))}\n"
                    f"  squadLineText: {ref(ctx.c('Squads/Detail/Line', 'tmp'))}\n  mateCards:\n{mates}"
                    f"  removeButton: {ref(ctx.c('Squads/Detail/Remove', 'button'))}\n"
                    f"  removeLabel: {ref(ctx.c('Squads/Detail/Remove/Label', 'tmp'))}\n"
                    f"  playArcadeButton: {ref(ctx.c('Squads/Detail/PlayArcade', 'button'))}\n"
                    f"  playArenaButton: {ref(ctx.c('Squads/Detail/PlayArena', 'button'))}\n"
                    f"  playMissionButton: {ref(ctx.c('Squads/Detail/PlayMission', 'button'))}\n"
                    f"  joinGroup: {ref(ctx.c('Squads/Joining', 'cg'))}\n  joinRows:\n{joins}"
                    f"  joinStatusText: {ref(ctx.c('Squads/Joining/Status', 'kt'))}\n"
                    f"  joinCloseButton: {ref(ctx.c('Squads/Joining/Close', 'button'))}\n"
                    "  refreshSeconds: 20\n  publishDelaySeconds: 2\n  confirmSeconds: 3\n  maxFriendsRead: 30\n"
                    + SQUAD_WORDS)
    page.add("logic", 114, body)
    return page


def decor_page(root):
    """DECOR, locked (its tab refuses): the banner and the Ark's hull, outside and in. Drawn so that opening the tab is
    the one change (MenuAvailability.Available on the tab); nothing behind it yet."""
    page = page_node(root, "Decor", False)
    title_band(page, "Decor/Title", "Decor", "The Ark's banner and its hull, outside and in.")
    pn = page.child(N("Decor/Slots", "Slots", stretch_x(DETAIL_L, DETAIL_R, DETAIL_Y, 320)))
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
    decor_page(root)
    tab_row(root)

    def body(ctx, go):
        entries = ""
        for t in TABS:
            logic = {"Squads": ref(ctx.c("Squads", "logic"))}.get(t, "{fileID: 0}")
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


MATE_FIELDS = dict(seatText=TMP, memberText=TMP, seatButton=S["button"], seatButtonLabel=TMP,
                   vesselButton=S["button"], vesselImage=IMG, vesselText=TMP, elementButton=S["button"], elementText=TMP,
                   pilotButton=S["button"], pilotImage=IMG, pilotText=TMP, artifactButton=S["button"], artifactImage=IMG,
                   artifactText=TMP)
SQUADS_FIELDS = dict(countText=S["kt"], newButton=S["button"], joinButton=S["button"], detailGroup=225, emptyGroup=225,
                     squadNameText=TMP, squadLineText=TMP, removeButton=S["button"], removeLabel=TMP,
                     playArcadeButton=S["button"], playArenaButton=S["button"], playMissionButton=S["button"],
                     joinGroup=225, joinStatusText=S["kt"], joinCloseButton=S["button"])
ROW_VIEW_FIELDS = dict(button=S["button"], group=225, titleText=TMP, lineText=TMP, activeMark=IMG)


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
    for t, (button, page, logic, gate) in zip(TABS, entries):
        assert A.kind_of(view, button)[1] == S["text_tab"], f"the {t} tab's button is not a KitTextTab"
        assert A.kind_of(view, page)[0] == 225, f"the {t} tab's page is not a CanvasGroup"
        if t == "Squads":
            assert logic != "0" and A.kind_of(view, logic)[1] == S["squads"], "the Squads tab is not driven by its SquadsPage"
        if t in LOCKED_TABS:
            assert gate != "0", f"the {t} tab has no MenuAvailabilityView: it would open"
            _, g = A.doc_of(view, gate)
            assert S["availability"] in g and "  availability: 1\n" in g, f"the {t} tab is not Locked"
        else:
            assert gate == "0", f"the {t} tab is gated"
    _, page = A.doc_of(view, V("Squads#logic"))
    for field, want in SQUADS_FIELDS.items():
        C.wired(view, page, field, want, "SquadsPage")
    for field, n, kind in (("squadRows", MAX_SQUADS, S["squad_row"]), ("mateCards", SEATS, S["mate"]),
                           ("joinRows", JOIN_ROWS, S["squad_row"])):
        ids = listed(page, field)
        assert len(ids) == n and all(i != "0" and A.kind_of(view, i)[1] == kind for i in ids), f"SquadsPage.{field} is not {n} wired"
        for i in ids:
            _, body = A.doc_of(view, i)
            for f, want in (MATE_FIELDS if field == "mateCards" else ROW_VIEW_FIELDS).items():
                C.wired(view, body, f, want, field)
    for guid, what in ((VESSEL_LIST, "the vessel list"), (PILOT_CATALOG, "the pilot catalog")):
        assert f"guid: {guid}" in page, f"SquadsPage reads no {what}"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def plain_segment(view):
    """The Space segment's Image turned back into a Simple image (the self-test's plant)."""
    fid = str(K.FileIds(VIEW)("Bar/SegSpace#img"))
    m = re.search(r"^--- !u!114 &" + fid + r"\n.*?(?=^--- )", view, re.M | re.S)
    return view[:m.start()] + m.group(0).replace("  m_Type: 3\n", "  m_Type: 0\n") + view[m.end():]


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
        ("Decor opened", lambda f: f.__setitem__(view, f[view].replace("  availability: 1\n", "  availability: 0\n", 1))),
        ("a squad mate with no pilot button", lambda f: f.__setitem__(view, re.sub(
            r"(  pilotButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
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
