#!/usr/bin/env python3
"""
Author ITEM 7 of the style-B menu redesign - the ARK screen's ELEMENTAL CONVERTER overlay - as a prefab,
and mount it in Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 16; the approved Ark / ArkConverting
boards, revision 2, on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Ark/
        ArkView.prefab   the Ark's five tabs (ArkTabsView: CONVERTER, SQUADS, DECOR - Shombith, 2026-10-27; ARTIFACTS -
                         Garrett, 2026-10-27; PILOTS - Garrett, 2026-11-12) and
                         their pages:
                         CONVERTER, the overlay over the pier (Rev 104 B, section 196: no cards): the shade, and the
                         holo dais at the pier's foot - the player's four elemental balances (ConverterBalance, fly-in
                         order) round its arc, each with its beam to the omni wallet at its centre, the line over it,
                         and CONVERT with the sweep under it;
                         SQUADS (SquadsPage): the player's squads (up to four, made and joined), the selected
                         squad's three squad mates with their vessel, element, pilot and artifact, NEW / JOIN /
                         DELETE, PLAY into Arcade / Arena / Mission, and the join list of friends' open seats;
                         PILOTS (ArkPilotsPage, section 92.1): the crew, one cell a pilot with the close-up of their
                         callsign's scene; a press opens that pilot's profile page (author_menu_pilot_profile.py);
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
import math
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
S = dict(view=C.script("ElementalConverterView"), balance=C.script("ConverterBalance"),
         button=K.kit_script("KitButton"), kt=K.kit_script("KitText"), entrance=K.kit_script("KitEntrance"),
         ambient=K.kit_script("KitAmbient"), spark=K.kit_script("KitSpark"), tabs=C.script("ArkTabsView"),
         squads=C.script("SquadsPage"), squad_row=C.script("SquadRowView"), mate=C.script("SquadMateCard"),
         availability=C.script("MenuAvailabilityView"), text_tab=K.kit_script("KitTextTab"),
         chip=C.script("SquadPickChip"), nanobots=C.script("SquadNanobotSwap"), artifacts=C.script("ArkArtifactsPage"),
         pilots=C.script("ArkPilotsPage"))
C.STUB_KIND.update({"balance": (114, S["balance"]), "entrance": (114, S["entrance"]), "tabs": (114, S["tabs"])})

ORDER = ("Space", "Charge", "Mass", "Time")       # OmniCombineCore.Order: the fly-in order
CRYSTAL = {k: K.meta_guid(f"Assets/_Graphics/Codex/ethirion_{k.lower()}.png") for k in ORDER}
OMNI = K.meta_guid("Assets/_Graphics/Codex/ethirion_omni.png")
# The converter fills its screen exactly. It used to grow 170.4 upward to cancel ArkScreen's authored top inset,
# but ScreenSwitcher.LayoutScreensToViewport lays every screen out at the full safe height at runtime, so the
# grow pushed the title and the recipe panel up under the nav (2026-10-17, the owner: "Ark screen is messed up").
TOP_GROW = 0


def c(node_key, comp):
    return lambda ctx: ctx.c(node_key, comp)


def title_band(root, key="Title", heading="Elemental converter",
               line="Space, Charge, Mass and Time combine into one omni crystal."):
    band = root.child(N(key, "Title", TL(40, 108, 514, 116)))
    band.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise"))
    H.framed(band, key, 14, "SurfaceBlack", "SurfaceVeryDark")
    label(band.child(N(f"{key}/Eyebrow", "Eyebrow", TL(22, 22, 300, 16))), "Ark", "Label", "NeutralLightest")
    label(band.child(N(f"{key}/Heading", "Heading", TL(22, 38, 480, 44))), heading, "H1", "TextLight")
    label(band.child(N(f"{key}/Line", "Line", TL(22, 84, 480, 20))), line, "BodySmall", "NeutralLightest")


# The holo dais (Rev 104 B, Shombith 2026-11-13: "I like B"; no cards on top, "the elemental converter should show the
# actual elemental crystals balance the user has"): a projection rises from the pier's edge like the bridge tables in the
# pilot scenes, the four balances standing round its arc and the omni wallet at its centre, a beam from each crystal to
# the omni, the line over the dais and CONVERT under it. Measured from the screen's foot (y up) and its centre line: the
# rings are centred on the foot, so only their upper halves show. The dais is board R104B's, pressed down under the
# pier's crystals (Shombith, of the first build: "The actual crystals are clipping and does not seem to be the center of
# attraction"): the recomposed clips (r104_converter_recomp.py) hold the crystals between 362 and 809 of 1080 from the
# top (the ceiling beam's foot is at 350), so everything here stays under DAIS_CEILING (270 over the foot) and the outer
# ring caps each column.
DAIS_CEILING = 270
DAIS_RINGS = (("Outer", "kit_ring_line", 1280, 600, 0.35), ("Inner", "kit_ring_line", 1000, 440, 0.18),
              ("Floor", "kit_pool_rim", 1300, 70, 0.26))
DAIS_GLOW = (1300, 600, 0.12)
DAIS_COL_W, DAIS_COL_H, DAIS_ART = 140, 140, 64
DAIS_POS = {"Space": (-470, 172), "Charge": (-230, 238), "Mass": (230, 238), "Time": (470, 172)}   # column top-centres
DAIS_OMNI_W, DAIS_OMNI_ART, DAIS_OMNI_TOP = 200, 92, 212
DAIS_OMNI_CENTRE = (0, DAIS_OMNI_TOP - DAIS_OMNI_ART // 2)
DAIS_LINE_Y, DAIS_LINE_W = 266, 1000
CONVERT_Y, CONVERT_MIN_W = 22, 300


def foot(x, y, w, h, pivot=(0.5, 1)):
    """A rect anchored at the screen's foot-centre: (x, y) is its pivot, y up from the foot."""
    return R((0.5, 0), None, (x, y), (w, h), pivot)


def dais_beam(x0, y0, x1, y1):
    """The beam's rect and its turn: pivoted at its start, as long as the gap, turned to the end."""
    dx, dy = x1 - x0, y1 - y0
    return foot(x0, y0, round(math.hypot(dx, dy), 2), 2, (0, 0.5)), round(math.degrees(math.atan2(dy, dx)), 3)


def dais(root):
    d = root.child(N("Dais", "Dais", R((0, 0), (1, 1), (0, 0), (0, 0))))
    d.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise"))
    gw, gh, ga = DAIS_GLOW
    image_node(d, "Dais/Glow", "Glow", foot(0, 0, gw, gh, (0.5, 0.5)), "TextLight", K.kit_sprite("kit_pool"), alpha=ga)
    for name, sprite, w, h, alpha in DAIS_RINGS:
        image_node(d, f"Dais/{name}Ring", f"{name}Ring", foot(0, 0, w, h, (0.5, 0.5)), "TextLight", K.kit_sprite(sprite),
                   alpha=alpha)
    label(d.child(N("Dais/Line", "Line", foot(0, DAIS_LINE_Y, DAIS_LINE_W, 20))), "One of each makes one omni crystal",
          "Label", "NeutralLightest", align="center")
    for name in ORDER:
        x, top = DAIS_POS[name]
        r, deg = dais_beam(x, top - DAIS_ART // 2, *DAIS_OMNI_CENTRE)
        image_node(d, f"Dais/{name}Beam", f"{name}Beam", r, "TextLight", alpha=0.18).rot = deg
    for name in ORDER:
        k = f"Dais/{name}"
        x, top = DAIS_POS[name]
        col = d.child(N(k, name, foot(x, top, DAIS_COL_W, DAIS_COL_H)))
        art_node(col, f"{k}/Art", "Art", TL((DAIS_COL_W - DAIS_ART) // 2, 0, DAIS_ART, DAIS_ART), CRYSTAL[name],
                 preserve=True)
        label(col.child(N(f"{k}/Count", "Count", TL(0, 68, DAIS_COL_W, 50))), "0", "DataLarge", "TextLight",
              align="center")
        label(col.child(N(f"{k}/Name", "Name", TL(0, 120, DAIS_COL_W, 18))), name, "Label", "NeutralLightest",
              align="center")
        col.add("balance", 114, lambda ctx, go, k=k, name=name: mono(go, S["balance"],
                f"  art: {ref(ctx.c(f'{k}/Art', 'img'))}\n  countText: {ref(ctx.c(f'{k}/Count', 'kt'))}\n"
                f"  nameText: {ref(ctx.c(f'{k}/Name', 'kt'))}\n  beam: {ref(ctx.c(f'Dais/{name}Beam', 'th'))}\n"))
    om = d.child(N("Dais/Omni", "Omni", foot(0, DAIS_OMNI_TOP, DAIS_OMNI_W, 140)))
    art_node(om, "Dais/Omni/Art", "Art", TL((DAIS_OMNI_W - DAIS_OMNI_ART) // 2, 0, DAIS_OMNI_ART, DAIS_OMNI_ART), OMNI,
             preserve=True)
    label(om.child(N("Dais/Omni/Count", "Count", TL(0, 96, DAIS_OMNI_W, 40))), "0", "Data", "TextLight", align="center")
    om.add("pop", 114, M.entrance("Pop", play_on_enable=False, duration=M.CEREMONY))
    w, h = K.button_size("CONVERT")
    w = max(w, CONVERT_MIN_W)
    holder = root.child(N("Convert", "Convert", R((0.5, 0), None, (0, CONVERT_Y), (w, h), (0.5, 0))))
    holder.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    holder.children.append(P.Inst("ConvertButton", "KitButton", "ConvertButton", STRETCH,
                                  K.button_mods("KitButton", "Purchase", True, "CONVERT"),
                                  added=[("mask", 114, A.rect_mask())], added_nodes=[C.sweep_node()]))


# --------------------------------------------------------------------------------------------
# The Ark's tabs (Shombith, 2026-10-27: "add two more tabs along with the converter. 1. Squads ... 2. Decor - lock it")
# --------------------------------------------------------------------------------------------

# ARTIFACTS (Garrett, 2026-10-27, Docs/Pilots/ARTIFACTS.md §4 and §8.3: "the Ark screen gets an artifact panel beside the
# Converter"): spend elemental crystals on an artifact's four tracks.
# PILOTS (Garrett, 2026-11-12: "use the full size art for each pilot's profile page in the ark"; section 92.1): the crew,
# each pilot a door to their profile page. Between Squads and Artifacts, the order of a squad mate's cards.
TABS = ("Converter", "Squads", "Pilots", "Artifacts", "Decor")
LOCKED_TABS = {"Decor"}                       # MenuAvailability.Locked: refuses with the sting and "Not open yet."
TAB_Y, TAB_H = 108, 52
MAX_SQUADS, SEATS, JOIN_ROWS = 4, 3, 6        # SquadBook.MaxSquads, SavedSquad.SlotCount, the join list's rows
PICKS = ("Vessel", "Element", "Pilot", "Artifact")   # a card's four rows
VESSEL_LIST = K.meta_guid("Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset")   # the Hangar's roster
PILOT_CATALOG = K.meta_guid("Assets/Resources/Pilots/PilotArtifactCatalog.asset")
CARD_ART = K.meta_guid("Assets/_SO_Assets/UI/HangarCardArt.asset")                  # the Hangar's hull renders
COLOR_SET = K.meta_guid("Assets/_SO_Assets/Color Palettes/NocturneColorSetSO.asset")  # the domains' colours
DOMAIN_ORDER = (("Jade", 1), ("Ruby", 2), ("Gold", 4))                                 # Domains values
# SQUADS as the approved board draws it (Rev 67, R67Ark, Docs/MENU_REDESIGN_B.md section 187; Shombith 2026-11-12: "do
# exactly how you showed me on Frogma"): the left two-thirds of the screen show the squad's vessels on the hangar's pads
# (the room behind the page), so the squads and the domain are two rows of glass chips top-left, the squad's name is
# top-right over its three seats as a compact glass column, and the bar runs along the foot.
EDGE = 40                                      # the boards' margin, left and right
LIST_Y, LIST_ROW_H = 214, 48                   # the squads row: its caption, the four squad chips, MAKE and JOIN
LIST_CAPTION_W, SQUAD_CHIP_W, CHIP_GAP = 110, 190, 8
HEAD_Y, HEAD_H = 172, 136                      # the squad's name (right) and the domain row (left, under the squads row)
DOMAIN_ROW_Y = LIST_Y + LIST_ROW_H + 10 - HEAD_Y   # 100 down the header
SEAT_W, SEAT_H, SEAT_GAP, SEAT_Y = 470, 176, 8, 262   # a seat card; the column stands `EDGE` in from the right
DECK_H = SEATS * SEAT_H + (SEATS - 1) * SEAT_GAP      # Make squad scales the column about its top-right corner
SEAT_ROW_Y, SEAT_ROW_STEP = 64, 26             # a card's Vessel / Element / Pilot / Artifact rows
COL_X, COL_W = 430, 1450                       # Make squad's picker and the join list span this
PICKER_Y, BAR_Y, BAR_H = 780, 1004, 48
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
    the page's CanvasGroup hides it with the page. Only DECOR draws it now: the converter, SQUADS and ARTIFACTS each
    show their room of the Ark behind them (ElementalConverterView's picture), and a veil there hid it (Shombith,
    2026-11-12: "why are the old UI images on top of our newly created Ark")."""
    under_bar = R((0, 0), (1, 1), (0, -TOP_BAR / 2), (0, -TOP_BAR))
    veil = page.child(N(f"{key}/Backdrop", "Backdrop", under_bar))
    veil.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack", WINDOW_BACKDROP_ALPHA), raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", WINDOW_BACKDROP_ALPHA))
    page.children.append(M.window_sea_node(f"{key}/Sea", "Sea", under_bar))


VEILED = {"Pilots", "Decor"}   # the tabs with no room behind them: the veil and the sea lines (validate_tabs holds both ways)
# The boards' glass (R67Ark / R86ArtifactsAB `.glass`: rgba(0,4,31,.62), SurfaceVeryDark at 62 %): the room reads through.
GLASS_ALPHA = 0.62
BUTTON_GLASS_ALPHA = 0.55   # the boards' `.def` button: rgba(0,4,31,.55)
SHADE_TOP, SHADE_TOP_ALPHA = 238, 0.72         # the boards' gradient over the room: SurfaceBlack .72 at the top to 0 at 22 %,
SHADE_FOOT, SHADE_FOOT_ALPHA = 324, 0.7        # and 0 at 70 % to .7 at the foot, so the words up there and down here read


def glass(node, sliver=10, alpha=GLASS_ALPHA):
    """The board's glass under `node`'s own drawing: SurfaceVeryDark at `alpha`, first child, so a kit row's or a
    button's own fill (the theme's 12 % tint) and border draw over it."""
    g = N(f"{node.key}/Glass", "Glass", STRETCH)
    g.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", alpha), K.kit_sprite(f"kit_sliver{sliver}_fill"), True)) \
        .add("th", 114, H.themed("SurfaceVeryDark", alpha))
    node.children.insert(0, g)
    return node


def glass_panel(parent, key, name, r):
    """A glass panel (the boards' `.glass sl`): the 14 slivered fill at GLASS_ALPHA and its hairline."""
    pn = parent.child(N(key, name, r))
    H.framed(pn, f"{key}/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=GLASS_ALPHA)
    return pn


def page_ground(page, key, veiled):
    """What a page stands on: the veil and the sea (a tab in `veiled`, no room behind it), or the shade over its room."""
    (page_backdrop if key in veiled else page_shade)(page, key)


def page_shade(page, key):
    """The boards' shade over the room (no veil: the middle stays clear): darker at the top and the foot only."""
    top = image_node(page, f"{key}/ShadeTop", "ShadeTop", R((0, 1), (1, 1), (0, 0), (0, SHADE_TOP), (0.5, 1)),
                     "SurfaceBlack", K.kit_sprite("kit_fade_v"), alpha=SHADE_TOP_ALPHA)
    top.rot = 180                               # kit_fade_v is clear at its top: turned, it is dark at the screen's top
    image_node(page, f"{key}/ShadeFoot", "ShadeFoot", R((0, 0), (1, 0), (0, 0), (0, SHADE_FOOT), (0.5, 0)),
               "SurfaceBlack", K.kit_sprite("kit_fade_v"), alpha=SHADE_FOOT_ALPHA)


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
    """A selectable row (the Hangar's kit_row): a name, a line, the squad's domain and the active mark down its left edge.
    Under 88 tall it is the boards' compact glass chip (R67's squad chips, R86's artifact rows): the name over the line."""
    row = H.kit_row(parent, key, name, r, h, content_keys=((f"{key}/Title", "tmp"),))
    row.add("cg", 225, canvas_group(True))
    if h < 88:
        glass(row)
        top = (h - 42) // 2
        image_node(row, f"{key}/Mark", "Mark", TL(0, 8, 3, h - 16), "ControlActive")
        H.ellipsize(label(row.child(N(f"{key}/Title", "Title", stretch_x(14, 70, top, 22))), "", "Body", "TextLight"))
        label(row.child(N(f"{key}/Domain", "Domain", TR(10, top + 3, 56, 16))), "", "Label", "NeutralLightest", align="right")
        H.ellipsize(label(row.child(N(f"{key}/Line", "Line", stretch_x(14, 10, top + 24, 18))), "", "BodySmall",
                          "NeutralLightest"))
    else:
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
    b = H.text_button(parent, key, name, caption, r, None, None, None, None, variant=variant)
    # A transparent button is only the theme's 12 % tint: over the room it takes the boards' `.def` glass under it.
    return glass(b, alpha=BUTTON_GLASS_ALPHA) if variant in ("Default", "Cancel") else b


def pick_chip(parent, key, name, r, h, caption="", picture=None, picture_rect=None, line_rect=None, label_rect=None,
              label_role="ButtonSmall", align="center", line_align="left"):
    """One SquadPickChip: a kit_row button, its word, an optional picture and second line, and the picked mark."""
    chip = glass(H.kit_row(parent, key, name, r, h, content_keys=((f"{key}/Label", "tmp"),)))
    chip.add("cg", 225, canvas_group(True))
    chip.child(N(f"{key}/Picked", "Picked", R((0, 0), (1, 1), (0, 0), (2, 2)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, off(K.image(tok("Selection"), K.kit_sprite("kit_sliver10_line2"), True)))
    if picture_rect is not None:
        pic = chip.child(N(f"{key}/Picture", "Picture", picture_rect))
        pic.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, picture, preserve=True))
    label(chip.child(N(f"{key}/Label", "Label", label_rect or STRETCH)), caption, label_role, "TextLight", align=align)
    if line_rect is not None:
        H.ellipsize(label(chip.child(N(f"{key}/Line", "Line", line_rect)), "", "Label", "NeutralLightest",
                          align=line_align))

    def body(ctx, go):
        pic = ref(ctx.c(f"{key}/Picture", "img")) if picture_rect is not None else "{fileID: 0}"
        line = ref(ctx.c(f"{key}/Line", "tmp")) if line_rect is not None else "{fileID: 0}"
        return mono(go, S["chip"], f"  button: {ref(ctx.c(key, 'button'))}\n  group: {ref(ctx.c(key, 'cg'))}\n"
                    f"  label: {ref(ctx.c(f'{key}/Label', 'tmp'))}\n  line: {line}\n  image: {pic}\n"
                    f"  selectedMark: {ref(ctx.c(f'{key}/Picked', 'img'))}\n")
    chip.add("chip", 114, body)
    return chip


def squad_list(page):
    """The squads row (R67): its caption and count, the four squad chips, MAKE and JOIN, glass, top-left."""
    pn = page.child(N("Squads/List", "List", stretch_x(EDGE, SEAT_W + 2 * EDGE, LIST_Y, LIST_ROW_H)))
    pn.add("cg", 225, canvas_group(True))
    C.section_label(pn, "Squads/List/Caption", "Caption", "Squads", TL(0, 6, LIST_CAPTION_W, 16))
    label(pn.child(N("Squads/List/Count", "Count", TL(0, 26, LIST_CAPTION_W, 16))), "0 of 4", "Label", "NeutralLightest")
    x = LIST_CAPTION_W + 10
    for i in range(MAX_SQUADS):
        kit_row_text(pn, f"Squads/List/Row{i}", f"Row{i}", TL(x, 0, SQUAD_CHIP_W, LIST_ROW_H), LIST_ROW_H)
        x += SQUAD_CHIP_W + CHIP_GAP
    small_button(pn, "Squads/List/New", "New", "Make", TL(x, 0, 110, LIST_ROW_H))
    small_button(pn, "Squads/List/Join", "Join", "Join a friend", TL(x + 110 + CHIP_GAP, 0, 170, LIST_ROW_H))


def squad_header(page):
    """The squad's name top-right over its seats, and the domain chips as the second row top-left (R67)."""
    hd = page.child(N("Squads/Head", "Head", stretch_x(EDGE, EDGE, HEAD_Y, HEAD_H)))
    hd.add("cg", 225, canvas_group(False))
    label(hd.child(N("Squads/Head/Kicker", "Kicker", TR(0, 0, SEAT_W, 16))), "Your squad", "Label", "NeutralLightest")
    H.ellipsize(label(hd.child(N("Squads/Head/Name", "Name", TR(0, 20, SEAT_W, 40))), "", "H1", "TextLight"))
    H.ellipsize(label(hd.child(N("Squads/Head/Line", "Line", TR(0, 62, SEAT_W, 20))), "", "BodySmall", "NeutralLightest"))
    label(hd.child(N("Squads/Head/DomainCaption", "DomainCaption", TL(0, DOMAIN_ROW_Y + 10, LIST_CAPTION_W, 16))), "Domain",
          "Label", "NeutralLightest")
    for i, (name, _) in enumerate(DOMAIN_ORDER):
        pick_chip(hd, f"Squads/Head/Domain{name}", f"Domain{name}", TL(LIST_CAPTION_W + 10 + i * 118, DOMAIN_ROW_Y, 110, 36),
                  36, name, K.chrome_sprite("chrome_diamond"), TL(12, 11, 14, 14), label_rect=TL(32, 0, 74, 36))


ACTION_W, ACTION_H = 150, 28   # a seat card's action button (CHANGE VESSEL, OPEN THE SEAT)


def mate_card(deck, i):
    """One seat (R67): its number and name, who flies it, then the Vessel / Element / Pilot / Artifact rows, glass. The
    vessel itself stands on its pad in the hangar behind, so the card's hull render and pilot figure stay off (their
    fields stay wired: SquadMateCard draws into them, the nanobots run on the hidden hull)."""
    k = f"Squads/Deck/Card{i}"
    card = glass(H.kit_row(deck, k, f"Card{i}", TL(0, i * (SEAT_H + SEAT_GAP), SEAT_W, SEAT_H), SEAT_H,
                           content_keys=((f"{k}/Who", "tmp"),)))
    card.child(N(f"{k}/Edit", "Edit", R((0, 0), (1, 1), (0, 0), (4, 4)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, off(K.image(tok("Selection"), K.kit_sprite("kit_sliver10_line2"), True)))
    label(card.child(N(f"{k}/Seat", "Seat", TL(18, 11, 260, 16))), ("Leader", "Rogue One", "Rogue Two")[i], "Label", "NeutralLightest")
    label(card.child(N(f"{k}/Number", "Number", TR(14, 11, 100, 16))), f"0{i + 1} / 03", "Label", "NeutralLightest", align="right")
    hull = card.child(N(f"{k}/Hull", "Hull", TL(40, 60, 290, 97), active=False))
    hull.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None, preserve=True))
    bot = hull.child(N(f"{k}/Hull/Bot", "Bot", R((0.5, 0.5), None, (0, 0), (6, 6)), active=False))
    bot.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    hull.add("swap", 114, lambda ctx, go: mono(go, S["nanobots"],
             f"  hull: {ref(ctx.c(f'{k}/Hull', 'img'))}\n  companion: {ref(ctx.c(f'{k}/Figure', 'img'))}\n"
             f"  botTemplate: {ref(ctx.c(f'{k}/Hull/Bot', 'img'))}\n  botCount: 34\n  seconds: 1.1\n  scatter: 130\n"
             "  sparkShare: 0.25\n  sparkColor: {r: 1, g: 1, b: 1, a: 1}\n  tintStrength: 0.45\n"))
    # The presence dot before who flies the seat (R67): lit while they are here, dim for an offline friend, none if open.
    for dot, colour in (("OnlineDot", tok("Selection")), ("OfflineDot", tok("NeutralLightest", 0.45))):
        card.child(N(f"{k}/{dot}", dot, TL(18, 39, 10, 10))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(colour, K.chrome_sprite("chrome_diamond"), preserve=True))
    H.ellipsize(label(card.child(N(f"{k}/Who", "Who", TL(34, 31, SEAT_W - 48 - ACTION_W - 8, 26))), "", "Body", "TextLight"))
    # The card's actions (R67), beside who flies it: CHANGE VESSEL on yours, OPEN THE SEAT (Lime) on an offline friend's;
    # SquadsPage shows the one that applies (SquadMateCard.SetActions).
    small_button(card, f"{k}/ChangeVessel", "ChangeVessel", "Change vessel", TR(14, 30, ACTION_W, ACTION_H))
    small_button(card, f"{k}/OpenSeat2", "OpenTheSeat", "Open the seat", TR(14, 30, ACTION_W, ACTION_H), variant="Cta")
    for j, pick in enumerate(PICKS):
        y = SEAT_ROW_Y + j * SEAT_ROW_STEP
        image_node(card, f"{k}/{pick}Rule", f"{pick}Rule", TL(18, y, SEAT_W - 32, 1), "NeutralLightest", alpha=0.25)
        label(card.child(N(f"{k}/{pick}Kicker", f"{pick}Kicker", TL(18, y + 5, 110, 16))), pick, "Label", "NeutralLightest")
        H.ellipsize(label(card.child(N(f"{k}/{pick}", pick, TR(14, y + 3, 300, 20))), "", "BodySmall", "TextLight",
                          align="right"))
    pilot = card.child(N(f"{k}/Figure", "Figure", TL(-40, 70, 180, 290), active=False))
    pilot.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None, preserve=True))
    open_ = card.child(N(f"{k}/OpenSeat", "OpenSeat", TL(-10, 110, 120, 240), active=False))
    open_.add("cg", 225, canvas_group(False))
    open_.child(N(f"{k}/OpenSeat/Line", "Line", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceLight", 0.6), K.kit_sprite("kit_sliver10_line1"), True))
    label(open_.child(N(f"{k}/OpenSeat/Words", "Words", R((0, 0), (1, 1), (0, 0), (-16, 0)))), "A friend takes this seat",
          "BodySmall", "NeutralLightest", align="center", wrap=True)

    def body(ctx, go):
        out = (f"  button: {ref(ctx.c(k, 'button'))}\n  editMark: {ref(ctx.c(f'{k}/Edit', 'img'))}\n"
               f"  changeVesselButton: {ref(ctx.c(f'{k}/ChangeVessel', 'button'))}\n"
               f"  openSeatButton: {ref(ctx.c(f'{k}/OpenSeat2', 'button'))}\n"
               f"  onlineDot: {ref(ctx.c(f'{k}/OnlineDot', 'img'))}\n  offlineDot: {ref(ctx.c(f'{k}/OfflineDot', 'img'))}\n"
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
    deck = page.child(N("Squads/Deck", "Deck", TR(EDGE, SEAT_Y, SEAT_W, DECK_H)))
    for i in range(SEATS):
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
    # The chips page through the callsign pilots between < and > (one per callsign, any vessel: Garrett, 2026-10-27,
    # Docs/Pilots/CALLSIGNS.md). There is no empty seat: every vessel needs a pilot.
    def pilot_x(i):
        return 132 + i * 198
    for i in range(PILOT_CHIPS):
        pick_chip(pk, f"Squads/Picker/Pilot{i}", f"Pilot{i}", TL(pilot_x(i), 66, 190, 50), 50, "",
                  None, TL(4, 2, 34, 46), line_rect=TL(46, 28, 136, 16), label_rect=TL(46, 6, 136, 20), label_role="Body",
                  align="left")
    small_button(pk, "Squads/Picker/PilotPrev", "PilotPrev", "<", TL(84, 66, 40, 50))
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
    """The foot (R67, top 1004): EDIT SQUAD on the left; Take this squad into, MISSION and ARENA on the right."""
    mk = page.child(N("Squads/MakeBar", "MakeBar", stretch_x(EDGE, EDGE, BAR_Y, BAR_H)))
    mk.add("cg", 225, canvas_group(False))
    small_button(mk, "Squads/MakeBar/Cancel", "Cancel", "Cancel", TL(0, 0, 160, BAR_H))
    label(mk.child(N("Squads/MakeBar/Hint", "Hint", TL(184, 14, 900, 20))),
          "Pick a card, then its vessel, pilot, element and artifact. Take into appears once the squad is made.",
          "BodySmall", "NeutralLightest")
    small_button(mk, "Squads/MakeBar/Create", "Create", "Create squad", TR(0, 0, 240, BAR_H), "Cta")
    vw = page.child(N("Squads/ViewBar", "ViewBar", stretch_x(EDGE, EDGE, BAR_Y, BAR_H)))
    vw.add("cg", 225, canvas_group(False))
    small_button(vw, "Squads/ViewBar/Edit", "Edit", "Edit squad", TL(0, 4, 170, 40))
    small_button(vw, "Squads/ViewBar/Remove", "Remove", "Delete", TL(180, 4, 130, 40), "Danger")
    label(vw.child(N("Squads/ViewBar/Caption", "Caption", TR(400, 16, 220, 16))), "Take this squad into", "Label",
          "NeutralLightest", align="right")
    mission = small_button(vw, "Squads/ViewBar/Mission", "Mission", "Mission", TR(200, 0, 190, BAR_H))
    # The lock in the button, before its word (R67); it used to sit on the bar's left edge, over DELETE.
    lock = mission.child(N("Squads/ViewBar/Mission/Lock", "Lock", TL(16, 14, 16, 20)))
    lock.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_lock"), preserve=True))
    small_button(vw, "Squads/ViewBar/Arena", "Arena", "Arena", TR(0, 0, 190, BAR_H), "Play")


def squad_empty(page):
    """No squad yet: the words and MAKE SQUAD over three empty seat outlines, in the seats' own column (the hangar's
    empty pads show in the middle)."""
    e = page.child(N("Squads/Empty", "Empty", TR(EDGE, HEAD_Y, SEAT_W, 190 + DECK_H)))
    e.add("cg", 225, canvas_group(True))
    for i in range(SEATS):
        image_node(e, f"Squads/Empty/Ghost{i}", f"Ghost{i}", TL(0, 190 + i * (SEAT_H + SEAT_GAP), SEAT_W, SEAT_H),
                   "SurfaceLight", K.kit_sprite("kit_sliver10_line1"), sliced=True, alpha=0.35)
    label(e.child(N("Squads/Empty/Heading", "Heading", TL(0, 0, SEAT_W, 40))), "No squad yet", "H2", "TextLight")
    label(e.child(N("Squads/Empty/Line", "Line", TL(0, 48, SEAT_W, 66))),
          "A squad is you and two squad mates, friends or AI wingmates: three cards. Make one, then take it into a mission "
          "or the arena.", "Body", "NeutralLightest", valign="top", wrap=True)
    small_button(e, "Squads/Empty/Make", "Make", "Make squad", TL(0, 124, 220, 52), "Cta")


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
    + yfield("seatOpenedToast", "{seat} is open: the pad stands empty until someone takes it.")
    + yfield("giveHintToast", "Pick the seat that carries {artifact}, then save.") + yfield("givenToast", "{seat} carries {artifact}.")
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
    + yfield("basePilotLine", "No pilot")
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


def squads_page(root, veiled=VEILED):
    page = page_node(root, "Squads", False)
    page_ground(page, "Squads", veiled)
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


# The PILOTS tab: one cell a callsign (a crew holds at most one pilot a callsign, Docs/Pilots/INVENTORY.md 3.1), six
# across and three down, right of the friends orb and inside the page's right and bottom margins.
CALLSIGNS = len(re.findall(r"^\s+\w+ = [1-9]\d*,", open(os.path.join(
    REPO, "Assets/_Scripts/Data/Enums/PilotVision.cs"), encoding="utf-8").read(), re.M))
# The page margins the grid was laid out in (Garrett, 2026-11-12): the Squads list's old box, kept here because
# Squads moved to the R67 board's row (LIST_Y above) and the crew grid did not.
PILOT_LIST_X, PILOT_LIST_Y, PILOT_LIST_H = 40, 176, 864
PILOT_COLS, PILOT_W, PILOT_H, PILOT_GAP = 6, 260, 256, 32
PILOT_X, PILOT_Y = A.ORB_CLEARANCE, PILOT_LIST_Y + 32          # the grid's top left; the caption sits on PILOT_LIST_Y
PILOT_FACE, PILOT_FACE_Y, PILOT_PAD = 180, 12, 16        # the close-up's side and top, and the callsign's inset
PILOT_NAME_Y, PILOT_NAME_H, PILOT_LINE_Y, PILOT_LINE_H = 200, 24, 226, 18   # the name and the callsign under it
# The name runs nearer the cell's edges than the callsign: it must hold the widest name PilotGenerator's tables
# compose (measured 2026-11-12 over its given and family names, in the UI font; 68 of 357,027 rolled names were wider
# than the callsign's row).
PILOT_NAME_PAD, WIDEST_NAME = 6, "Darthawen Lanternweaver"
SCENE_CATALOG = K.meta_guid("Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset")     # author_pilot_scene_assets.py
PILOT_LINES = (("nameTemplate", "{name}"), ("lineTemplate", "{callsign}"), ("countTemplate", "Pilots: {count}"))


def pilot_tokens():
    """The tokens ArkPilotsPage fills in each of its lines, read from its own two TemplateText.Fill calls, so the
    lists cannot drift from the page: a cell's two lines take the pilot's facts, the count takes {count} alone."""
    source = open(os.path.join(REPO, "Assets/_Scripts/UI/Elements/Ark/ArkPilotsPage.cs"), encoding="utf-8").read()
    cell = re.search(r"TemplateText\.Fill\(template,(.*?)\);", source, re.S)
    count = re.search(r"TemplateText\.Fill\(countTemplate,(.*?)\);", source, re.S)
    assert cell and count, "ArkPilotsPage.cs no longer fills its lines where author_menu_ark.py reads them"
    cell, count = (tuple(re.findall(r'"(\w+)"', m.group(1))) for m in (cell, count))
    assert cell and count, "ArkPilotsPage's filled tokens read as empty"
    return {"nameTemplate": cell, "lineTemplate": cell, "countTemplate": count}


PILOT_TOKENS = pilot_tokens()
PILOT_WORDS = "".join(yfield(field, words) for field, words in PILOT_LINES)


def pilot_cell_rect(i):
    """Cell `i` of the crew, left to right then down: (x, y) of its top left on the page."""
    return (PILOT_X + (i % PILOT_COLS) * (PILOT_W + PILOT_GAP), PILOT_Y + (i // PILOT_COLS) * (PILOT_H + PILOT_GAP))


def pilots_page(root, veiled=VEILED):
    """PILOTS: the crew in join order, one cell a pilot (SquadPickChip: the close-up of their callsign's scene, their
    name, their callsign). A press opens the pilot's profile page (ScreenSwitcher.OpenPilotProfile). ArkPilotsPage
    draws it; every word is on that component or the text of a caption here."""
    page = page_node(root, "Pilots", False)
    page_ground(page, "Pilots", veiled)
    grid_w = PILOT_COLS * PILOT_W + (PILOT_COLS - 1) * PILOT_GAP
    C.section_label(page, "Pilots/Caption", "Caption", "Your crew", TL(PILOT_X, PILOT_LIST_Y, 220, 18))
    label(page.child(N("Pilots/Count", "Count", TL(PILOT_X + grid_w - 220, PILOT_LIST_Y, 220, 18))), "", "Label",
          "NeutralLightest", align="right")
    e = page.child(N("Pilots/Empty", "Empty", TL(PILOT_X, 250, grid_w, 200)))
    e.add("cg", 225, canvas_group(False))
    label(e.child(N("Pilots/Empty/Heading", "Heading", stretch_x(0, 0, 40, 40))), "No pilots yet", "H2", "TextLight",
          align="center")
    label(e.child(N("Pilots/Empty/Line", "Line", stretch_x(300, 300, 90, 60))),
          "Your crew appears here once your account has loaded.", "Body", "NeutralLightest", align="center", wrap=True)
    words_w = PILOT_W - 2 * PILOT_PAD
    for i in range(CALLSIGNS):
        x, y = pilot_cell_rect(i)
        pick_chip(page, f"Pilots/Cell{i}", f"Cell{i}", TL(x, y, PILOT_W, PILOT_H), PILOT_H, "", None,
                  TL((PILOT_W - PILOT_FACE) // 2, PILOT_FACE_Y, PILOT_FACE, PILOT_FACE),
                  line_rect=TL(PILOT_PAD, PILOT_LINE_Y, words_w, PILOT_LINE_H),
                  label_rect=TL(PILOT_NAME_PAD, PILOT_NAME_Y, PILOT_W - 2 * PILOT_NAME_PAD, PILOT_NAME_H),
                  label_role="Body", line_align="center")

    def body(ctx, go):
        return mono(go, S["pilots"],
                    f"  scenes: {K.ref(11400000, SCENE_CATALOG, 2)}\n"
                    "  cells:\n" + refs_of(ctx, [f"Pilots/Cell{i}" for i in range(CALLSIGNS)], "chip")
                    + f"  emptyGroup: {ref(ctx.c('Pilots/Empty', 'cg'))}\n"
                    f"  countText: {ref(ctx.c('Pilots/Count', 'tmp'))}\n  screenSwitcher: {{fileID: 0}}\n"
                    + PILOT_WORDS)
    page.add("logic", 114, body)
    return page


ART_ROWS = 4                                   # Javelin and Aegis, and room for two more
TRACK_ELEMENTS = ("Mass", "Space", "Time", "Charge")   # ArkArtifactsPage.TrackOrder
# ARTIFACTS as the approved board draws it (Rev 86, R86ArtifactsAB, Docs/MENU_REDESIGN_B.md section 167): the room (the
# Reliquary's dais in the Gallery's hall) fills the middle, so the list is a glass panel on the left and the picked one
# is a glass card bottom-right; its crystals sit top-right and its four tracks in a second glass panel between them.
ART_LIST_Y, ART_LIST_W, ART_ROW_H, ART_ROW_GAP, ART_PAD = 300, 460, 64, 8, 22
ART_LIST_H = 48 + ART_ROWS * (ART_ROW_H + ART_ROW_GAP) - ART_ROW_GAP + 20
ART_COL_W = 520                                # the right column: the crystals, the tracks, the card
CRYSTAL_Y, CRYSTAL_W = 172, 104
TRACKS_Y, TRACK_PAD, TRACK_H, TRACK_GAP = 220, 14, 92, 6
TRACKS_H = 2 * TRACK_PAD + len(TRACK_ELEMENTS) * (TRACK_H + TRACK_GAP) - TRACK_GAP
ART_CARD_Y, ART_CARD_H = 640, 258
assert TRACKS_Y + TRACKS_H < ART_CARD_Y, "the artifact's tracks run into its card"
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


def artifacts_page(root, veiled=VEILED):
    """ARTIFACTS: the player's artifacts down the left (SquadRowView rows: the name, the four levels), and the picked
    one's four element tracks to the right, each with RAISE (its own crystals) and OMNI (omni crystals instead), above
    the profile's five crystal balances. ArkArtifactsPage draws it; every word is on that component."""
    page = page_node(root, "Artifacts", False)
    page_ground(page, "Artifacts", veiled)
    pn = glass_panel(page, "Artifacts/List", "List", TL(EDGE, ART_LIST_Y, ART_LIST_W, ART_LIST_H))
    pn.add("cg", 225, canvas_group(True))
    C.section_label(pn, "Artifacts/List/Caption", "Caption", "Your artifacts", TL(ART_PAD, 20, 300, 18))
    for i in range(ART_ROWS):
        kit_row_text(pn, f"Artifacts/List/Row{i}", f"Row{i}",
                     TL(ART_PAD, 48 + i * (ART_ROW_H + ART_ROW_GAP), ART_LIST_W - 2 * ART_PAD, ART_ROW_H), ART_ROW_H)
    # No artifacts: the words stand in the list's panel, where the rows would be.
    e = page.child(N("Artifacts/Empty", "Empty", TL(EDGE + ART_PAD, ART_LIST_Y + 48, ART_LIST_W - 2 * ART_PAD, 140)))
    e.add("cg", 225, canvas_group(False))
    label(e.child(N("Artifacts/Empty/Heading", "Heading", TL(0, 0, ART_LIST_W - 2 * ART_PAD, 36))), "No artifacts yet", "H2",
          "TextLight")
    label(e.child(N("Artifacts/Empty/Line", "Line", TL(0, 44, ART_LIST_W - 2 * ART_PAD, 60))),
          "Find them on voyages and bring them home to your Ark.", "Body", "NeutralLightest", valign="top", wrap=True)
    # The picked artifact: one group (ArkArtifactsPage.detailGroup) over the right column.
    d = page.child(N("Artifacts/Detail", "Detail", STRETCH))
    d.add("cg", 225, canvas_group(True))
    label(d.child(N("Artifacts/Detail/CrystalCaption", "CrystalCaption", TR(EDGE, CRYSTAL_Y, ART_COL_W, 16))), "Crystals",
          "Label", "NeutralLightest", align="right")
    for i in range(5):
        label(d.child(N(f"Artifacts/Detail/Crystal{i}", f"Crystal{i}", TR(EDGE + (4 - i) * CRYSTAL_W, CRYSTAL_Y + 20,
                                                                            CRYSTAL_W - 4, 24))), "", "Body", "TextLight", align="right")
    tp = glass_panel(d, "Artifacts/Detail/Tracks", "Tracks", TR(EDGE, TRACKS_Y, ART_COL_W, TRACKS_H))
    for i, name in enumerate(TRACK_ELEMENTS):
        k = f"Artifacts/Detail/Track{name}"
        t = tp.child(N(k, f"Track{name}", stretch_x(TRACK_PAD, TRACK_PAD, TRACK_PAD + i * (TRACK_H + TRACK_GAP), TRACK_H)))
        H.framed(t, k, 10, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.5)
        label(t.child(N(f"{k}/Element", "Element", TL(14, 10, 80, 16))), name, "Label", "NeutralLightest")
        H.ellipsize(label(t.child(N(f"{k}/Label", "Label", TL(96, 6, 170, 24))), "", "Body", "TextLight"))
        H.ellipsize(label(t.child(N(f"{k}/Level", "Level", TL(14, 32, 156, 20))), "", "BodySmall", "TextLight"))
        H.ellipsize(label(t.child(N(f"{k}/Value", "Value", TL(176, 32, 90, 20))), "", "BodySmall", "NeutralLightest"))
        tw = t.child(N(f"{k}/Twist", "Twist", TL(14, 54, 252, 36)))
        tw.add("cg", 225, canvas_group(True))
        label(tw.child(N(f"{k}/Twist/Words", "Words", STRETCH)), "", "BodySmall", "TextLight", valign="top", wrap=True)
        small_button(t, f"{k}/Raise", "Raise", "Raise", TR(12, 10, 200, 40), "Cta")
        small_button(t, f"{k}/Omni", "Omni", "Omni", TR(12, 56, 200, 30))
    card = glass_panel(d, "Artifacts/Detail/Card", "Card", TR(EDGE, ART_CARD_Y, ART_COL_W, ART_CARD_H))
    label(card.child(N("Artifacts/Detail/Kicker", "Kicker", TL(ART_PAD, 18, 300, 16))), "Artifact", "Label", "NeutralLightest")
    # GIVE TO A SEAT (R86): the picked artifact goes to the Squads tab, where the next seat picked carries it.
    small_button(card, "Artifacts/Detail/Give", "Give", "Give to a seat", TR(ART_PAD, 12, 170, 32))
    H.ellipsize(label(card.child(N("Artifacts/Detail/Name", "Name", TL(ART_PAD, 40, ART_COL_W - 2 * ART_PAD, 40))), "", "H2",
                      "TextLight"))
    label(card.child(N("Artifacts/Detail/Description", "Description", TL(ART_PAD, 86, ART_COL_W - 2 * ART_PAD, 100))), "",
          "BodySmall", "NeutralLightest", valign="top", wrap=True)
    label(card.child(N("Artifacts/Detail/Status", "Status", TL(ART_PAD, 196, ART_COL_W - 2 * ART_PAD, 44))), "", "BodySmall",
          "TextLight", valign="top", wrap=True)

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
                    f"  giveButton: {ref(ctx.c('Artifacts/Detail/Give', 'button'))}\n"
                    "  crystalTexts:\n" + refs_of(ctx, [f"Artifacts/Detail/Crystal{i}" for i in range(5)], "tmp")
                    + ARTIFACT_WORDS)
    page.add("logic", 114, body)
    return page


def decor_page(root, veiled=VEILED):
    """DECOR, locked (its tab refuses): the banner and the Ark's hull, outside and in. Drawn so that opening the tab is
    the one change (MenuAvailability.Available on the tab); nothing behind it yet."""
    page = page_node(root, "Decor", False)
    page_ground(page, "Decor", veiled)
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


def build_view(veiled=VEILED):
    root = N("root", VIEW, STRETCH)
    # The converter's overlay keeps its node keys (and so every fileID the scene's stubs point at) under its page.
    conv = page_node(root, "Converter", True)
    page_shade(conv, "Converter")
    dais(conv)
    squads_page(root, veiled)
    pilots_page(root, veiled)
    artifacts_page(root, veiled)
    decor_page(root, veiled)
    tab_row(root)

    def body(ctx, go):
        entries = ""
        for t in TABS:
            logic = ref(ctx.c(t, "logic")) if t in ("Squads", "Pilots", "Artifacts") else "{fileID: 0}"
            gate = ref(ctx.c(f"Tabs/{t}", "gate")) if t in LOCKED_TABS else "{fileID: 0}"
            entries += (f"  - button: {ref(ctx.c(f'Tabs/{t}', 'tab'))}\n    page: {ref(ctx.c(t, 'cg'))}\n"
                        f"    pageLogic: {logic}\n    availability: {gate}\n")
        return mono(go, S["tabs"], "  tabs:\n" + entries + "  defaultTab: 0\n")
    root.add("tabs", 114, body)
    return root


def view_text(veiled=VEILED):
    return P.prefab_text(build_view(veiled), VIEW)


def prefab_outputs():
    out = {}
    out[f"{OUT_DIR}/{VIEW}.prefab"] = view_text()
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
    "omniCountText": ("Dais/Omni/Count#kt", "kt"),
    "balanceLineText": ("Dais/Line#kt", "kt"),
    "omniPop": ("Dais/Omni#pop", "entrance"),
    "arkTabs": ("root#tabs", "tabs"),
}
LISTS = {
    "balances": [(f"Dais/{n}#balance", "balance") for n in ORDER],
}
OVERLAY_ORDER = ("convertButton", "convertLabel", "balances", "omniCountText", "balanceLineText", "omniPop", "arkTabs")
# The overlay fields Rev 104 A retired (section 196) with the cards they pointed into: their scene stubs are removed too.
RETIRED_REFS = (["Bar/Status#kt", "Bar/Clock#tmp", "Recipe/Merged#kt", "Recipe/Omni#cg", "Recipe/Omni#pop"]
                + [f"Bar/Seg{n}#img" for n in ORDER] + [f"Bar/Seg{n}/SparkGroup#cg" for n in ORDER]
                + [f"Recipe/{n}#row" for n in ORDER]
                # Rev 104 A's ledger, built first and replaced by B's dais (Shombith picked B, 2026-11-13)
                + ["Ledger/Omni/Count#kt", "Ledger/Line#kt", "Ledger/Omni#pop"] + [f"Ledger/{n}#balance" for n in ORDER])


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
        return ({self.inst, self.stub} | {self.stub_fid(k) for k, _ in self.refs()}
                | {self.stub_fid(k) for k in RETIRED_REFS})


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
BALANCE_FIELDS = dict(art=IMG, countText=S["kt"], nameText=S["kt"], beam=A.S["th"])


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
        _, body = A.doc_of(view, V(f"Dais/{n}#balance"))
        assert body and S["balance"] in body, f"ArkView: the {n} balance carries no ConverterBalance"
        for field, want in BALANCE_FIELDS.items():
            C.wired(view, body, field, want, f"the {n} balance")
        assert f"guid: {CRYSTAL[n]}" in A.doc_of(view, V(f"Dais/{n}/Art#img"))[1], f"the {n} balance shows the wrong crystal"
    assert f"guid: {OMNI}" in A.doc_of(view, V("Dais/Omni/Art#img"))[1], "the omni wallet shows the wrong crystal"
    assert max([t for _, t in DAIS_POS.values()] + [DAIS_OMNI_TOP, DAIS_LINE_Y]) <= DAIS_CEILING, \
        "the dais reaches up into the pier's crystals"
    for n in ORDER:                             # each beam runs from its crystal's centre to the omni's
        x, top = DAIS_POS[n]
        _, rt = A.doc_of(view, V(f"Dais/{n}Beam#rt"))
        start = [float(v) for v in re.search(r"m_AnchoredPosition: \{x: (-?[\d.e-]+), y: (-?[\d.e-]+)\}", rt).groups()]
        a = math.radians(float(re.search(r"m_LocalEulerAnglesHint: \{x: 0, y: 0, z: (-?[\d.e-]+)\}", rt).group(1)))
        length = float(re.search(r"m_SizeDelta: \{x: (-?[\d.e-]+)", rt).group(1))
        end = (start[0] + length * math.cos(a), start[1] + length * math.sin(a))
        assert abs(start[0] - x) < 0.5 and abs(start[1] - (top - DAIS_ART // 2)) < 0.5, f"the {n} beam does not start at its crystal"
        assert math.hypot(end[0] - DAIS_OMNI_CENTRE[0], end[1] - DAIS_OMNI_CENTRE[1]) < 1.0, f"the {n} beam ends at {end}, not the omni"
    # Rev 104: no cards on the converter (Shombith 2026-11-13: "The UI cards on top of each screen. I do not like them
    # at all"). The retired title band, recipe panel and converter bar stay gone.
    for retired in ("Title", "Recipe", "Bar"):
        assert not A.doc_of(view, V(f"{retired}#rt"))[1], f"the converter's {retired} card is back (Rev 104 has none)"
    assert view.count("guid: " + S["spark"]) == 1, "ArkView needs exactly the CONVERT sweep"
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


MATE_FIELDS = dict(button=S["button"], editMark=IMG, changeVesselButton=S["button"], openSeatButton=S["button"],
                   onlineDot=IMG, offlineDot=IMG, seatText=TMP, numberText=TMP, whoText=TMP, vesselText=TMP,
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
ART_FIELDS = dict(emptyGroup=225, detailGroup=225, nameText=TMP, descriptionText=TMP, statusText=TMP, giveButton=S["button"])
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


def page_children(view, V, t):
    """The RectTransform fileIDs of the page `t`'s children, in draw order."""
    return re.findall(r"fileID: (-?\d+)", re.search(r"^  m_Children:\n((?:  - .*\n)*)", A.doc_of(view, V(f"{t}#rt"))[1],
                                                    re.M).group(1))


VEIL_ALPHA = 0.5     # a full-page image at least this opaque hides the room


def veil_of(view, rt):
    """(name, reason) for a page child: the reason is set when the child covers the whole page with an Image at least
    VEIL_ALPHA opaque, or draws the window's sea lines; empty otherwise."""
    _, body = A.doc_of(view, rt)
    go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", body, re.M).group(1)
    _, gdoc = A.doc_of(view, go)
    name = re.search(r"^  m_Name: (.*)$", gdoc, re.M).group(1)
    full = "  m_AnchorMin: {x: 0, y: 0}\n" in body and "  m_AnchorMax: {x: 1, y: 1}\n" in body
    for comp in re.findall(r"^  - component: \{fileID: (-?\d+)\}", gdoc, re.M):
        _, cdoc = A.doc_of(view, comp)
        if not cdoc or f"guid: {IMG}" not in cdoc:
            continue
        if f"guid: {M.SEA_WINDOW_MAT}" in cdoc:
            return name, "sea"
        a = re.search(r"^  m_Color: \{r: [^,]+, g: [^,]+, b: [^,]+, a: ([^}]+)\}", cdoc, re.M)
        if full and a and float(a.group(1)) >= VEIL_ALPHA:
            return name, "veil"
    return name, ""


PILOT_CELL_FIELDS = dict(CHIP_FIELDS, line=TMP, image=IMG)      # a crew cell needs its callsign line and its close-up
PILOTS_FIELDS = dict(emptyGroup=225, countText=TMP)


def rect_xy(view, fid, field):
    m = re.search(r"^  " + field + r": \{x: (-?[\d.e-]+), y: (-?[\d.e-]+)\}", A.doc_of(view, fid)[1], re.M)
    return float(m.group(1)), float(m.group(2))


def validate_pilots(view, V):
    """The PILOTS tab: one wired cell a callsign, laid out clear of the friends orb and inside the page, every word
    authored and naming only tokens ArkPilotsPage fills."""
    _, page = A.doc_of(view, V("Pilots#logic"))
    for field, want in PILOTS_FIELDS.items():
        C.wired(view, page, field, want, "ArkPilotsPage")
    assert f"guid: {SCENE_CATALOG}" in page, "ArkPilotsPage reads no pilot scene catalog"
    cells = listed(page, "cells")
    assert len(cells) == CALLSIGNS and all(i != "0" and A.kind_of(view, i)[1] == S["chip"] for i in cells), \
        f"ArkPilotsPage.cells is not {CALLSIGNS} wired SquadPickChips (one a callsign)"
    assert len(set(cells)) == len(cells), "ArkPilotsPage.cells names a cell twice"
    for i in cells:
        for field, want in PILOT_CELL_FIELDS.items():
            C.wired(view, A.doc_of(view, i)[1], field, want, "a crew cell")
    # Each line names only the tokens the page fills in THAT line. Read from what was written, not from PILOT_LINES,
    # and before the next rule, so a planted token is caught by this one.
    written = re.findall(r"^  (\w+Template): (.*)$", page, re.M)
    assert {field for field, _ in written} == set(PILOT_TOKENS), \
        "ArkPilotsPage's lines in the prefab are not the ones its code fills"
    for field, value in written:
        for token in re.findall(r"\{(\w+)\}", value):
            assert token in PILOT_TOKENS[field], \
                f"ArkPilotsPage.{field} names {{{token}}}, which the page does not fill there"
    for field, words in PILOT_LINES:
        assert yfield(field, words) in page, f"ArkPilotsPage.{field} is not the authored line"
    for i in range(CALLSIGNS):
        rt = V(f"Pilots/Cell{i}#rt")
        assert rect_xy(view, rt, "m_AnchorMin") == (0, 1) and rect_xy(view, rt, "m_Pivot") == (0, 1), \
            f"crew cell {i} is not laid out from the page's top left"
        (x, y), (w, h) = rect_xy(view, rt, "m_AnchoredPosition"), rect_xy(view, rt, "m_SizeDelta")
        assert x >= A.ORB_CLEARANCE, f"crew cell {i} starts {x:g} from the left edge, under the friends orb"
        assert x + w <= 1920 - PILOT_LIST_X and -y + h <= PILOT_LIST_Y + PILOT_LIST_H, f"crew cell {i} runs outside the page's margins"
        assert -y >= PILOT_LIST_Y + 18, f"crew cell {i} runs over the caption"
        assert (x, -y) == pilot_cell_rect(i) and (w, h) == (PILOT_W, PILOT_H), f"crew cell {i} is off the grid"
        name_w = rect_xy(view, V(f"Pilots/Cell{i}/Label#rt"), "m_SizeDelta")[0]
        assert K.label_width(WIDEST_NAME, "Body") <= name_w <= PILOT_W, \
            f"crew cell {i}'s name row ({name_w:g}) does not hold the widest name a pilot can have"
    assert PILOT_FACE_Y + PILOT_FACE <= PILOT_NAME_Y and PILOT_NAME_Y + PILOT_NAME_H <= PILOT_LINE_Y \
        and PILOT_LINE_Y + PILOT_LINE_H + PILOT_FACE_Y <= PILOT_H, "a crew cell's close-up, name and callsign overlap"
    assert K.line_height("Body") <= PILOT_NAME_H and K.line_height("Label") <= PILOT_LINE_H + 0.5, \
        "a crew cell's rows are shorter than their text"


def validate_tabs(view, V):
    """The Ark's tabs: each a KitTextTab over a CanvasGroup page; SQUADS, PILOTS and ARTIFACTS driven by their pages;
    DECOR locked."""
    _, tabs = A.doc_of(view, V("root#tabs"))
    assert tabs and S["tabs"] in tabs, "ArkView's root carries no ArkTabsView"
    entries = re.findall(r"  - button: \{fileID: (-?\d+)\}\n    page: \{fileID: (-?\d+)\}\n"
                         r"    pageLogic: \{fileID: (-?\d+)\}\n    availability: \{fileID: (-?\d+)\}\n", tabs)
    assert len(entries) == len(TABS), f"the Ark has {len(entries)} tabs, not {len(TABS)} ({', '.join(TABS)})"
    # The page with no room behind it (DECOR) draws the window backdrop: the veil, then the sea lines, under everything
    # else. Every other page shows its room of the Ark (ElementalConverterView's picture) and must not hide it: no veil and
    # no sea under any name (Shombith 2026-11-12: "why are the old UI images on top of our newly created Ark").
    for t in TABS:
        kids = page_children(view, V, t)
        if t in VEILED:
            assert kids[:2] == [str(V(f"{t}/Backdrop#rt")), str(V(f"{t}/Sea#rt"))], \
                f"the {t} tab must open on the veil and the sea lines (\"Ark no sea line\")"
            assert f"guid: {M.SEA_WINDOW_MAT}" in A.doc_of(view, V(f"{t}/Sea#img"))[1], f"the {t} tab's sea is not the window sea"
            continue
        hiding = [name for name, why in (veil_of(view, rt) for rt in kids) if why]
        assert not hiding, f"the {t} tab hides its room of the Ark behind {', '.join(hiding)} (a veil or the sea lines)"
    for t, (button, page, logic, gate) in zip(TABS, entries):
        assert A.kind_of(view, button)[1] == S["text_tab"], f"the {t} tab's button is not a KitTextTab"
        assert A.kind_of(view, page)[0] == 225, f"the {t} tab's page is not a CanvasGroup"
        if t == "Squads":
            assert logic != "0" and A.kind_of(view, logic)[1] == S["squads"], "the Squads tab is not driven by its SquadsPage"
        if t == "Artifacts":
            assert logic != "0" and A.kind_of(view, logic)[1] == S["artifacts"], "the Artifacts tab is not driven by its ArkArtifactsPage"
        if t == "Pilots":
            assert logic != "0" and A.kind_of(view, logic)[1] == S["pilots"], \
                "the Pilots tab is not driven by its ArkPilotsPage"
        if t in LOCKED_TABS:
            assert gate != "0", f"the {t} tab has no MenuAvailabilityView: it would open"
            _, g = A.doc_of(view, gate)
            assert S["availability"] in g and "  availability: 1\n" in g, f"the {t} tab is not Locked"
        else:
            assert gate == "0", f"the {t} tab is gated"
    validate_pilots(view, V)
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


def beam_turn_plant(view, rt_fid):
    """Turn one beam ten degrees off its line to the omni."""
    head, doc = A.doc_of(view, rt_fid)
    z = float(re.search(r"m_LocalEulerAnglesHint: \{x: 0, y: 0, z: (-?[\d.e-]+)\}", doc).group(1))
    return view.replace(doc, re.sub(r"(m_LocalEulerAnglesHint: \{x: 0, y: 0, z: )-?[\d.e-]+", rf"\g<1>{z + 10}", doc), 1)


def recipe_card_plant(view):
    """The view with a RectTransform document under the retired Recipe card's fileID (the self-test's plant)."""
    fid = str(K.FileIds(VIEW)("Recipe#rt"))
    return view + f"--- !u!224 &{fid}\nRectTransform:\n  m_ObjectHideFlags: 0\n"


def veil_plant(view, key):
    """The view with node `key` (a shade) stretched over its whole page at 95 % (a veil by another name: a plant)."""
    V = K.FileIds(VIEW)

    def patch(text_, fid, old, new):
        m = re.search(r"^--- !u!\d+ &" + str(fid) + r"\n.*?(?=^--- )", text_, re.M | re.S)
        assert old in m.group(0), f"veil_plant: {old!r} not in {key}"
        return text_[:m.start()] + m.group(0).replace(old, new, 1) + text_[m.end():]
    view = patch(view, V(f"{key}#rt"), "  m_AnchorMax: {x: 1, y: 0}\n", "  m_AnchorMax: {x: 1, y: 1}\n")
    return re.sub(r"(--- !u!114 &" + str(V(f"{key}#img")) + r"\n(?:(?!--- ).*\n)*?  m_Color: \{r: [^,]+, g: [^,]+, b: [^,]+, a: )[^}]+",
                  r"\g<1>0.95", view, count=1)


def unlogic_tab(view, index):
    """The view with tab `index`'s pageLogic cut (a negative control)."""
    n = [0]

    def cut(m):
        n[0] += 1
        return m.group(1) + ("{fileID: 0}" if n[0] - 1 == index else m.group(2))
    return re.sub(r"(  - button: \{fileID: -?\d+\}\n    page: \{fileID: -?\d+\}\n    pageLogic: )(\{fileID: -?\d+\})", cut, view)


def in_doc(view, fid, change):
    """The view with `change` applied to the one document `fid` (a negative control's plant)."""
    m = re.search(r"^--- !u!\d+ &" + str(fid) + r"\n.*?(?=^--- |\Z)", view, re.M | re.S)
    out = change(m.group(0))
    assert out != m.group(0), f"the control changed nothing in document {fid}"
    return view[:m.start()] + out + view[m.end():]


def self_test(files):
    def expect(label_, mutate, by=""):
        """`by`: words of the rule that must catch the control. Caught by a neighbouring rule is not caught."""
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
        except AssertionError as e:
            if by not in str(e):
                print(f"  CAUGHT BY THE WRONG RULE: {label_} ({e}); expected the rule that says '{by}'")
                return False
            print(f"  caught: {label_} ({e})")
            return True
        print(f"  MISSED: {label_}")
        return False
    view = f"{OUT_DIR}/{VIEW}.prefab"
    mount = ArkMount()
    V = K.FileIds(VIEW)
    cases = [
        ("a balance with no count", lambda f: f.__setitem__(view, re.sub(r"(  countText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1)), "the Space balance"),
        ("the recipe card back", lambda f: f.__setitem__(view, recipe_card_plant(f[view])), "card is back"),
        ("a balance with no beam", lambda f: f.__setitem__(view, re.sub(r"(  beam: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1)), "the Space balance"),
        ("a beam turned off the omni", lambda f: f.__setitem__(view, beam_turn_plant(f[view], V("Dais/MassBeam#rt"))), "the Mass beam ends"),
        ("CONVERT unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  convertButton: {{fileID: {mount.stub_fid('ConvertButton|root#button')}}}", "  convertButton: {fileID: 0}"))),
        ("the converter grown up under the nav again", lambda f: f.__setitem__(SCENE, re.sub(
            r"(--- !u!224 &" + IDS["rt"] + r"\n(?:(?!--- ).*\n)*?  m_SizeDelta: )\{x: 0, y: 0\}", r"\1{x: 0, y: 170.4}",
            f[SCENE], count=1))),
        ("the wrong crystal on a balance", lambda f: f.__setitem__(view, f[view].replace(CRYSTAL["Mass"], CRYSTAL["Time"], 1)), "wrong crystal"),
        ("the Decor tab without its sea lines", lambda f: f.__setitem__(view, f[view].replace(
            f"guid: {M.SEA_WINDOW_MAT}", f"guid: {M.SEA_PANEL_MAT}", 1))),
        ("the Decor tab without its veil", lambda f: f.__setitem__(view, view_text(set()))),
        ("the veil and the sea back over the Squads room", lambda f: f.__setitem__(view, view_text(VEILED | {"Squads"}))),
        ("the veil back over the Artifacts room", lambda f: f.__setitem__(view, view_text(VEILED | {"Artifacts"}))),
        ("an opaque full-page image over the Artifacts room, under another name",
         lambda f: f.__setitem__(view, veil_plant(f[view], "Artifacts/ShadeFoot"))),
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
        ("the Pilots tab with no page logic", lambda f: f.__setitem__(view, unlogic_tab(f[view], TABS.index("Pilots")))),
        ("the Pilots tab reading no scene catalog", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(f"guid: {SCENE_CATALOG}", f"guid: {PILOT_CATALOG}")))),
        ("a callsign with no crew cell", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(f"  - {{fileID: {V('Pilots/Cell17#chip')}}}\n", "")))),
        ("a crew cell with no close-up", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Cell3#chip"), lambda d: re.sub(r"(  image: )\{fileID: -?\d+\}", r"\1{fileID: 0}", d)))),
        ("a crew cell under the friends orb", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Cell12#rt"), lambda d: d.replace(
                f"  m_AnchoredPosition: {{x: {PILOT_X}, y: ", f"  m_AnchoredPosition: {{x: {PILOT_LIST_X}, y: ")))),
        ("a crew cell too narrow for the widest name", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Cell7/Label#rt"), lambda d: d.replace(
                f"  m_SizeDelta: {{x: {PILOT_W - 2 * PILOT_NAME_PAD}, y: ", "  m_SizeDelta: {x: 228, y: ")))),
        ("a crew line naming a token the page does not fill", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(yfield("lineTemplate", "{callsign}"),
                                                             yfield("lineTemplate", "{sight}")))),
         "which the page does not fill there"),
        ("a crew line naming the count's token", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(yfield("nameTemplate", "{name}"),
                                                             yfield("nameTemplate", "{count}")))),
         "which the page does not fill there"),
        ("a crew line changed from its authored words", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(yfield("lineTemplate", "{callsign}"),
                                                             yfield("lineTemplate", "{given}")))),
         "is not the authored line"),
    ]
    ok = all([expect(*case) for case in cases])
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
