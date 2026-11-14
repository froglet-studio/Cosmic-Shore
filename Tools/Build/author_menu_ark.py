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
                         PILOTS (ArkPilotsPage, sections 92.1 and 191): the crew PDA on the hangar's workbench (Rev
                         99 C, the nautilus of Rev 109 B): lying down, the last pilot read and PICK UP; picked up, the
                         crew on its glass, one portrait card a pilot, and a pilot's file, whose FULL PROFILE opens
                         their profile page (author_menu_pilot_profile.py);
                         ARTIFACTS (ArkArtifactsPage, Rev 105 B, section 201): the hoard as a rail of medallions
                         (ArtifactMedallion); a pick opens the lens (ArtifactLens: the payload over prisms, the four
                         tracks round the rim) and the artifact's words; FEED IT shows its level, crystals and FEED;
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
import math
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
         squads=C.script("SquadsPage"), squad_row=C.script("SquadRowView"), plate=C.script("SquadSeatPlate"),
         availability=C.script("MenuAvailabilityView"), text_tab=K.kit_script("KitTextTab"),
         chip=C.script("SquadPickChip"), artifacts=C.script("ArkArtifactsPage"),
         pilots=C.script("ArkPilotsPage"), medal=C.script("ArtifactMedallion"), lens=C.script("ArtifactLens"),
         glass=C.script("PdaGlassImage"))
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
MAX_SQUADS, SEATS, JOIN_ROWS = 4, 3, 4        # SquadBook.MaxSquads, SavedSquad.SlotCount, the join list's rows
VESSEL_LIST = K.meta_guid("Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset")   # the Hangar's roster
PILOT_CATALOG = K.meta_guid("Assets/Resources/Pilots/PilotArtifactCatalog.asset")
COLOR_SET = K.meta_guid("Assets/_SO_Assets/Color Palettes/NocturneColorSetSO.asset")  # the domains' colours
DOMAIN_ORDER = (("Jade", 1), ("Ruby", 2), ("Gold", 4))                                 # Domains values
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


VEILED = {"Decor"}   # the tabs with no room behind them (PILOTS has the bench since Rev 99 C, section 191): the veil and the sea lines (validate_tabs holds both ways)
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


# SQUADS as Rev 110 B draws it (the holo table; Shombith 2026-11-13: "B is good"; Docs/MENU_REDESIGN_B.md sections 194,
# 200 and 203): the hangar is the screen, the squad's real vessels stand on its pads (SquadVesselLayer, section 202), and
# in front of them a dome of light carries the three seats as plates. The picked plate projects its seat up (its four
# picks and who flies it, each opening only that list in place); the brass rail along the foot carries the squad dial,
# MAKE, JOIN, DELETE, the squad's light and Take into. Every number is the board's (R110B, 1920 x 1080), measured from
# the screen's foot-centre.
EDGE = 40                                      # the boards' margin, left and right
RAIL_H = 136                                   # the brass rail: the board's 944..1080
RAIL_Y = 40                                    # its controls' top, down from the rail's top edge
RAIL_BTN_H = 48
DOME = (0, 144, 1260, 520)                     # centre x, centre y up from the foot, width, height (R110B left 330 top 676)
DOME_GLOW_ALPHA, DOME_RIM_ALPHA = 0.12, 0.45
LAMP = (70, 90, 420, 300, 0.22)                # the bridge's two warm lamps in the foot's corners (R110B's radial glows)
PLATE_W, PLATE_H, PLATE_TOP = 300, 82, 310     # a seat's plate; its top 310 up from the foot (board top 770)
PLATE_X = (0, -320, 320)                       # by seat: the leader in the middle, Rogue One left, Rogue Two right
PLATE_RISE = 26                                # the picked plate rises (board top 744)
PROJ_W, PROJ_BOTTOM, PROJ_PAD = 420, 420, 18   # the projection: its bottom 420 up from the foot, over the beam
BEAM_W, BEAM_H = 300, PROJ_BOTTOM - (PLATE_TOP + PLATE_RISE)   # from the picked plate's top to the projection
PROJ_HEAD, TILE_STEP, TILE_H = 46, 46, 44      # the first line's top, one line's step, a line's height
TILES = ("Who flies it", "Vessel", "Element", "Pilot", "Artifact")
CHOICE_COLS, CHOICE_ROWS, CHOICE_W, CHOICE_H = 2, 6, 186, 40
PILOT_COLS, PILOT_ROWS, PILOT_CHIP_W, PILOT_CHIP_H, PILOT_STEP = 2, 4, 186, 52, 58
CHOICES, PILOT_CHOICES = CHOICE_COLS * CHOICE_ROWS, PILOT_COLS * PILOT_ROWS
JOIN_ROW_H, JOIN_STEP = 56, 62
CHOICES_H = PROJ_HEAD + CHOICE_ROWS * TILE_STEP + 52          # the lists' panel heights (SquadsPage's layout fields)
PILOT_CHOICES_H = PROJ_HEAD + PILOT_ROWS * PILOT_STEP + 52
JOIN_H = PROJ_HEAD + JOIN_ROWS * JOIN_STEP + 36
EMPTY_W, EMPTY_H = 560, 190


def lit(parent, key, name, r, sprite, alpha, sliced=False):
    """An Image SquadsPage lights in the squad's domain colour (SquadsPage.domainLit keeps its alpha): authored white."""
    n = parent.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, alpha), sprite, sliced))
    return n


def squad_table(page):
    """The bridge: the two warm lamps, the dome of light the plates stand on."""
    lx, ly, lw, lh, la = LAMP
    image_node(page, "Squads/LampLeft", "LampLeft", R((0, 0), None, (lx, ly), (lw, lh), (0.5, 0.5)), "Purchase",
               K.kit_sprite("kit_pool"), alpha=la)
    image_node(page, "Squads/LampRight", "LampRight", R((1, 0), None, (-lx, ly), (lw, lh), (0.5, 0.5)), "Purchase",
               K.kit_sprite("kit_pool"), alpha=la)
    x, y, w, h = DOME
    lit(page, "Squads/DomeGlow", "DomeGlow", foot(x, y, w, h, (0.5, 0.5)), K.kit_sprite("kit_pool"), DOME_GLOW_ALPHA)
    lit(page, "Squads/DomeRim", "DomeRim", foot(x, y, w, h, (0.5, 0.5)), K.kit_sprite("kit_ring_line"), DOME_RIM_ALPHA)


def squad_rail(page):
    """The brass rail (R110B): the squad dial, MAKE, JOIN and DELETE on the left; the squad's light in the middle; Take
    into MISSION and ARENA on the right."""
    rail = page.child(N("Squads/Rail", "Rail", R((0, 0), (1, 0), (0, 0), (0, RAIL_H), (0.5, 0))))
    image_node(rail, "Squads/Rail/Ground", "Ground", STRETCH, "SurfaceBlack", alpha=0.92, raycast=True)
    image_node(rail, "Squads/Rail/Brass", "Brass", R((0, 1), (1, 1), (0, 0), (0, 3), (0.5, 1)), "Purchase", alpha=0.55)
    image_node(rail, "Squads/Rail/Sheen", "Sheen", R((0, 1), (1, 1), (0, 0), (0, 40), (0.5, 0)), "Purchase",
               K.kit_sprite("kit_fade_v"), alpha=0.12)
    small_button(rail, "Squads/Rail/Prev", "Prev", "<", TL(EDGE, RAIL_Y, 44, RAIL_BTN_H))
    H.ellipsize(label(rail.child(N("Squads/Rail/Name", "Name", TL(EDGE + 54, RAIL_Y - 6, 196, 30))), "", "H3", "TextLight"))
    H.ellipsize(label(rail.child(N("Squads/Rail/Whose", "Whose", TL(EDGE + 54, RAIL_Y + 28, 196, 18))), "", "Label",
                      "NeutralLightest"))
    small_button(rail, "Squads/Rail/Next", "Next", ">", TL(EDGE + 256, RAIL_Y, 44, RAIL_BTN_H))
    image_node(rail, "Squads/Rail/Divider", "Divider", TL(EDGE + 312, RAIL_Y + 6, 1, 36), "Purchase", alpha=0.35)
    small_button(rail, "Squads/Rail/Make", "Make", "Make", TL(EDGE + 326, RAIL_Y, 100, RAIL_BTN_H))
    small_button(rail, "Squads/Rail/Join", "Join", "Join", TL(EDGE + 434, RAIL_Y, 100, RAIL_BTN_H))
    sq = rail.child(N("Squads/Rail/Squad", "Squad", STRETCH))
    sq.add("cg", 225, canvas_group(False))
    small_button(sq, "Squads/Rail/Squad/Remove", "Remove", "Delete", TL(EDGE + 542, RAIL_Y, 110, RAIL_BTN_H), "Danger")
    light_w = 3 * 118 + 2 * 8
    lt = sq.child(N("Squads/Rail/Squad/Light", "Light", R((0.5, 1), None, (0, -RAIL_Y), (light_w, RAIL_BTN_H), (0.5, 1))))
    for i, (name, _) in enumerate(DOMAIN_ORDER):
        pick_chip(lt, f"Squads/Rail/Squad/Light/{name}", name, TL(i * 126, 0, 118, RAIL_BTN_H), RAIL_BTN_H, name,
                  K.chrome_sprite("chrome_diamond"), TL(14, 17, 14, 14), label_rect=TL(34, 0, 80, RAIL_BTN_H))
    mission = small_button(sq, "Squads/Rail/Squad/Mission", "Mission", "Mission", TR(EDGE + 188, RAIL_Y, 180, RAIL_BTN_H))
    # The lock in the button, before its word (R67): Mission refuses as Home's entry does.
    lock = mission.child(N("Squads/Rail/Squad/Mission/Lock", "Lock", TL(16, 14, 16, 20)))
    lock.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_lock"), preserve=True))
    small_button(sq, "Squads/Rail/Squad/Arena", "Arena", "Arena", TR(EDGE, RAIL_Y, 180, RAIL_BTN_H), "Play")


def seat_plate(page, seat):
    """One seat's plate on the dome (R110B): the pilot's face (a plus on an open seat), who flies it, the vessel and
    element, the presence diamond; lit in the squad's domain."""
    k = f"Squads/Plate{seat}"
    plate = H.kit_row(page, k, f"Plate{seat}", foot(PLATE_X[seat], PLATE_TOP, PLATE_W, PLATE_H), PLATE_H,
                      content_keys=((f"{k}/Who", "tmp"),))
    plate.add("cg", 225, canvas_group(True))
    lit(plate, f"{k}/Light", "Light", STRETCH, K.kit_sprite("kit_sliver10_fill"), 0.08, sliced=True)
    lit(plate, f"{k}/Edge", "Edge", STRETCH, K.kit_sprite("kit_sliver10_line1"), 0.45, sliced=True)
    face = plate.child(N(f"{k}/Face", "Face", TL(12, 13, 56, 56)))
    face.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None, preserve=True))
    plus = plate.child(N(f"{k}/Plus", "Plus", TL(26, 27, 28, 28)))
    plus.add("cr", 222, K.canvas_renderer()).add("img", 114, off(K.image(K.WHITE, K.chrome_sprite("chrome_plus"), preserve=True)))
    H.ellipsize(label(plate.child(N(f"{k}/Who", "Who", TL(80, 14, PLATE_W - 110, 28))), "", "H3", "TextLight"))
    H.ellipsize(label(plate.child(N(f"{k}/Line", "Line", TL(80, 46, PLATE_W - 96, 20))), "", "BodySmall", "NeutralLightest"))
    dot = plate.child(N(f"{k}/Dot", "Dot", TR(12, 12, 9, 9)))
    dot.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.chrome_sprite("chrome_diamond"), preserve=True))
    plate.add("plate", 114, lambda ctx, go: mono(go, S["plate"],
              f"  button: {ref(ctx.c(k, 'button'))}\n  group: {ref(ctx.c(k, 'cg'))}\n"
              f"  fill: {ref(ctx.c(f'{k}/Light', 'img'))}\n  edge: {ref(ctx.c(f'{k}/Edge', 'img'))}\n"
              f"  avatar: {ref(ctx.c(f'{k}/Face', 'img'))}\n  openMark: {ref(ctx.c(f'{k}/Plus', 'img'))}\n"
              f"  whoText: {ref(ctx.c(f'{k}/Who', 'tmp'))}\n  lineText: {ref(ctx.c(f'{k}/Line', 'tmp'))}\n"
              f"  dot: {ref(ctx.c(f'{k}/Dot', 'img'))}\n  pickedRise: {PLATE_RISE}\n  fillAlpha: 0.08\n"
              "  pickedFillAlpha: 0.26\n  edgeAlpha: 0.45\n  offEdgeAlpha: 0.25\n  restAlpha: 0.72\n"
              "  awayAlpha: 0.45\n"))


def squad_projection(page):
    """What the table projects over the picked plate (R110B): the seat's lines and its action, one list, or the friends'
    squads. Grows up from its foot; SquadsPage moves it over the plate and sets its height."""
    k = "Squads/Projection"
    pj = page.child(N(k, "Projection", foot(0, PROJ_BOTTOM, PROJ_W, 300, (0.5, 0))))
    pj.add("cg", 225, canvas_group(False))
    lit(pj, f"{k}/Beam", "Beam", R((0.5, 0), None, (0, 0), (BEAM_W, BEAM_H), (0.5, 1)), K.kit_sprite("kit_fade_v"), 0.35)
    image_node(pj, f"{k}/Ground", "Ground", STRETCH, "SurfaceBlack", K.kit_sprite("kit_sliver14_fill"), sliced=True,
               alpha=0.6, raycast=True)
    lit(pj, f"{k}/Glow", "Glow", R((0, 0), (1, 0), (0, 0), (0, 120), (0.5, 0)), K.kit_sprite("kit_fade_v"), 0.3)
    lit(pj, f"{k}/Edge", "Edge", STRETCH, K.kit_sprite("kit_sliver14_line1"), 0.6, sliced=True)
    H.ellipsize(label(pj.child(N(f"{k}/Title", "Title", TL(PROJ_PAD, 16, PROJ_W - 2 * PROJ_PAD - 140, 18))), "", "Label",
                      "NeutralLightest"))
    small_button(pj, f"{k}/Back", "Back", "Back", TR(12, 8, 84, 32))
    small_button(pj, f"{k}/Prev", "Prev", "<", TR(146, 8, 36, 32))
    small_button(pj, f"{k}/Next", "Next", ">", TR(104, 8, 36, 32))
    for i, name in enumerate(TILES):
        tk = f"{k}/Tile{i}"
        tile = pick_chip(pj, tk, f"Tile{i}", stretch_x(12, 12, PROJ_HEAD + i * TILE_STEP, TILE_H), TILE_H, "",
                         label_rect=TL(118, 0, PROJ_W - 24 - 118 - 34, TILE_H), label_role="Body", align="left",
                         line_rect=TL(12, 15, 100, 16))
        chev = tile.child(N(f"{tk}/Chevron", "Chevron", TR(12, 14, 16, 16)))
        chev.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_chevron"), preserve=True))
    small_button(pj, f"{k}/Action", "Action", "Change vessel", TL(PROJ_PAD, PROJ_HEAD, 220, 44))
    label(pj.child(N(f"{k}/ReadOnly", "ReadOnly", TL(PROJ_PAD, PROJ_HEAD, PROJ_W - 2 * PROJ_PAD, 40))), "", "BodySmall",
          "NeutralLightest", valign="top", wrap=True)
    ch = pj.child(N(f"{k}/Choices", "Choices", STRETCH))
    ch.add("cg", 225, canvas_group(False))
    for i in range(CHOICES):
        c, r = i % CHOICE_COLS, i // CHOICE_COLS
        pick_chip(ch, f"{k}/Choices/Chip{i}", f"Chip{i}", TL(PROJ_PAD + c * (CHOICE_W + 12), PROJ_HEAD + r * TILE_STEP,
                  CHOICE_W, CHOICE_H), CHOICE_H, "", label_role="BodySmall")
    pc = pj.child(N(f"{k}/Pilots", "Pilots", STRETCH))
    pc.add("cg", 225, canvas_group(False))
    for i in range(PILOT_CHOICES):
        c, r = i % PILOT_COLS, i // PILOT_COLS
        pick_chip(pc, f"{k}/Pilots/Chip{i}", f"Chip{i}", TL(PROJ_PAD + c * (PILOT_CHIP_W + 12), PROJ_HEAD + r * PILOT_STEP,
                  PILOT_CHIP_W, PILOT_CHIP_H), PILOT_CHIP_H, "", None, TL(4, 4, 44, 44),
                  line_rect=TL(54, 30, PILOT_CHIP_W - 62, 16), label_rect=TL(54, 6, PILOT_CHIP_W - 62, 22),
                  label_role="BodySmall", align="left")
    label(pj.child(N(f"{k}/Hint", "Hint", R((0, 0), (1, 0), (0, 12), (-2 * PROJ_PAD, 36), (0.5, 0)))), "", "BodySmall",
          "NeutralLightest", valign="top", wrap=True)
    jn = pj.child(N(f"{k}/Join", "Join", STRETCH))
    jn.add("cg", 225, canvas_group(False))
    for i in range(JOIN_ROWS):
        kit_row_text(jn, f"{k}/Join/Row{i}", f"Row{i}", stretch_x(12, 12, PROJ_HEAD + i * JOIN_STEP, JOIN_ROW_H), JOIN_ROW_H)
    label(jn.child(N(f"{k}/Join/Status", "Status", stretch_x(PROJ_PAD, PROJ_PAD, PROJ_HEAD + JOIN_ROWS * JOIN_STEP, 20))),
          "", "BodySmall", "NeutralLightest")


def squad_empty(page):
    """No squad yet: the words and MAKE over the dome (the hangar's pads stand empty behind)."""
    e = page.child(N("Squads/Empty", "Empty", foot(0, PROJ_BOTTOM, EMPTY_W, EMPTY_H, (0.5, 0))))
    e.add("cg", 225, canvas_group(True))
    label(e.child(N("Squads/Empty/Heading", "Heading", TL(0, 0, EMPTY_W, 40))), "No squad yet", "H2", "TextLight",
          align="center")
    label(e.child(N("Squads/Empty/Line", "Line", TL(0, 48, EMPTY_W, 66))),
          "A squad is you and two squad mates, friends or AI wingmates. Make one, then take it into a mission or the arena.",
          "Body", "NeutralLightest", align="center", valign="top", wrap=True)
    small_button(e, "Squads/Empty/Make", "Make", "Make squad", R((0.5, 1), None, (0, -124), (220, 52), (0.5, 1)), "Cta")


SQUAD_WORDS = (
    yfield("ownWhose", "Your squad") + yfield("joinedWhose", "{owner}'s squad - joined")
    + yfield("giveHintToast", "Pick the seat that carries {artifact}.") + yfield("givenToast", "{seat} carries {artifact}.")
    + yfield("seatOpenedToast", "{seat} is open: the pad stands empty until someone takes it.")
    + yfield("seatedLine", "Only {owner} changes the squad.")
    + yfield("pendingLine", "Joined. {owner} seats you when they next open the Ark.")
    + yfield("seatLostLine", "{owner} gave your seat to someone else. Leave the squad, or ask them to open it again.")
    + yfield("disbandedLine", "{owner} deleted this squad.")
    + yfield("offlineLine", "Offline - showing the last copy of {owner}'s squad.")
    + ylist("seatNames", ("Leader", "Rogue One", "Rogue Two"))
    + yfield("youLine", "You") + yfield("aiLine", "AI wingmate") + yfield("openLine", "Open seat")
    + yfield("unknownFriendLine", "A friend") + yfield("aiModeLabel", "AI wingmate") + yfield("openModeLabel", "Open for a friend")
    + yfield("plateLine", "{vessel} - {element}") + yfield("openPlateLine", "Pick who flies it")
    + yfield("openPlateJoinedLine", "Anyone in the squad can take it")
    + yfield("seatTitle", "{who} - {state}") + yfield("onlineState", "Online") + yfield("offlineState", "Offline")
    + yfield("openState", "Open") + yfield("pickTitle", "Pick - {list}")
    + ylist("tileNames", TILES)
    + ylist("choiceHints", ("An AI wingmate flies the seat now; a friend who joins takes it from them.",
                            "The seat's pad in the hangar changes to the vessel you pick.",
                            "The vessel's element: which of its four abilities it leads with.",
                            "The pilot's callsign biases how an AI flies this seat.",
                            "Carried into the match; None leaves the slot empty."))
    + yfield("changeVesselWord", "Change vessel") + yfield("whoFliesWord", "Who flies it")
    + yfield("openSeatWord", "Open the seat") + yfield("joinTitle", "Friends' squads with an open seat")
    + ylist("elementNames", ("", "Charge", "Mass", "Space", "Time", "Omni"))
    + ylist("domainNames", ("", "Jade", "Ruby", "Blue", "Gold"))
    + yfield("basePilotLine", "No pilot") + yfield("pilotChipWord", "{name} - {callsign}") + yfield("pilotDetail", "{sight}")
    + "  artifactChipChars: 16\n"
    + yfield("noArtifactLine", "No artifact") + yfield("noArtifactChip", "None")
    + yfield("deleteLabel", "Delete") + yfield("leaveLabel", "Leave") + yfield("confirmLabel", "Press again")
    + yfield("newSquadName", "") + yfield("fullLine", "You are in 4 squads. Leave or delete one first.")
    + yfield("createdToast", "{squad}: you, and two open seats.")
    + yfield("notLeaderLine", "Only {owner} changes the squad.")
    + yfield("joinLookingLine", "Looking at your friends' squads...")
    + yfield("joinNoneLine", "No friend has a seat open right now.")
    + yfield("joinOfflineLine", "Sign in to join a friend's squad.")
    + yfield("joinRowTitle", "{owner}'s {squad}") + yfield("joinRowLine", "{seat} open")
    + yfield("joinedToast", "Joined. {owner} seats you when they next open the Ark.")
    + yfield("alreadyInLine", "You are already in that squad.") + yfield("seatGoneLine", "That seat was just taken."))

DOMAIN_LIT = ["Squads/DomeGlow", "Squads/DomeRim", "Squads/Projection/Beam", "Squads/Projection/Glow",
              "Squads/Projection/Edge"]


def refs_of(ctx, keys, comp):
    return "".join(f"  - {ref(ctx.c(k, comp))}\n" for k in keys)


def squads_page(root, veiled=VEILED):
    page = page_node(root, "Squads", False)
    page_ground(page, "Squads", veiled)
    squad_table(page)
    for seat in range(SEATS):
        seat_plate(page, seat)
    squad_empty(page)
    squad_projection(page)
    squad_rail(page)

    def body(ctx, go):
        lists = lambda name, keys, comp: f"  {name}:\n" + refs_of(ctx, keys, comp)
        P_ = "Squads/Projection"
        return mono(go, S["squads"],
                    f"  vessels: {K.ref(11400000, VESSEL_LIST, 2)}\n  catalog: {K.ref(11400000, PILOT_CATALOG, 2)}\n"
                    f"  colors: {K.ref(11400000, COLOR_SET, 2)}\n"
                    f"  prevSquadButton: {ref(ctx.c('Squads/Rail/Prev', 'button'))}\n"
                    f"  nextSquadButton: {ref(ctx.c('Squads/Rail/Next', 'button'))}\n"
                    f"  squadNameText: {ref(ctx.c('Squads/Rail/Name', 'tmp'))}\n"
                    f"  whoseText: {ref(ctx.c('Squads/Rail/Whose', 'tmp'))}\n"
                    f"  newButton: {ref(ctx.c('Squads/Rail/Make', 'button'))}\n"
                    f"  joinButton: {ref(ctx.c('Squads/Rail/Join', 'button'))}\n"
                    f"  removeButton: {ref(ctx.c('Squads/Rail/Squad/Remove', 'button'))}\n"
                    f"  removeLabel: {ref(ctx.c('Squads/Rail/Squad/Remove/Label', 'tmp'))}\n"
                    + lists("domainChips", [f"Squads/Rail/Squad/Light/{n}" for n, _ in DOMAIN_ORDER], "chip")
                    + "  domainOrder:\n" + "".join(f"  - {v}\n" for _, v in DOMAIN_ORDER)
                    + f"  squadGroup: {ref(ctx.c('Squads/Rail/Squad', 'cg'))}\n"
                    f"  missionButton: {ref(ctx.c('Squads/Rail/Squad/Mission', 'button'))}\n"
                    f"  missionLock: {ref(ctx.c('Squads/Rail/Squad/Mission/Lock', 'img'))}\n"
                    f"  arenaButton: {ref(ctx.c('Squads/Rail/Squad/Arena', 'button'))}\n"
                    + lists("domainLit", DOMAIN_LIT, "img")
                    + lists("plates", [f"Squads/Plate{i}" for i in range(SEATS)], "plate")
                    + f"  emptyGroup: {ref(ctx.c('Squads/Empty', 'cg'))}\n"
                    f"  emptyMakeButton: {ref(ctx.c('Squads/Empty/Make', 'button'))}\n"
                    f"  projectionGroup: {ref(ctx.c(P_, 'cg'))}\n  projectionRoot: {ref(ctx.rt(P_))}\n"
                    f"  projectionTitle: {ref(ctx.c(f'{P_}/Title', 'tmp'))}\n"
                    f"  backButton: {ref(ctx.c(f'{P_}/Back', 'button'))}\n"
                    + lists("tiles", [f"{P_}/Tile{i}" for i in range(len(TILES))], "chip")
                    + lists("tileChevrons", [f"{P_}/Tile{i}/Chevron" for i in range(len(TILES))], "img")
                    + f"  actionButton: {ref(ctx.c(f'{P_}/Action', 'button'))}\n"
                    f"  actionLabel: {ref(ctx.c(f'{P_}/Action/Label', 'tmp'))}\n"
                    f"  readOnlyText: {ref(ctx.c(f'{P_}/ReadOnly', 'tmp'))}\n"
                    f"  choicesGroup: {ref(ctx.c(f'{P_}/Choices', 'cg'))}\n"
                    + lists("choiceChips", [f"{P_}/Choices/Chip{i}" for i in range(CHOICES)], "chip")
                    + f"  pilotChoicesGroup: {ref(ctx.c(f'{P_}/Pilots', 'cg'))}\n"
                    + lists("pilotChips", [f"{P_}/Pilots/Chip{i}" for i in range(PILOT_CHOICES)], "chip")
                    + f"  choicePrevButton: {ref(ctx.c(f'{P_}/Prev', 'button'))}\n"
                    f"  choiceNextButton: {ref(ctx.c(f'{P_}/Next', 'button'))}\n"
                    f"  choiceHintText: {ref(ctx.c(f'{P_}/Hint', 'tmp'))}\n"
                    f"  joinGroup: {ref(ctx.c(f'{P_}/Join', 'cg'))}\n"
                    + lists("joinRows", [f"{P_}/Join/Row{i}" for i in range(JOIN_ROWS)], "row")
                    + f"  joinStatusText: {ref(ctx.c(f'{P_}/Join/Status', 'kt'))}\n"
                    f"  projectionHead: {PROJ_HEAD}\n  tileStep: {TILE_STEP}\n  actionHeight: 44\n  readOnlyHeight: 44\n"
                    f"  projectionPad: {PROJ_PAD}\n  choicesHeight: {CHOICES_H}\n  pilotChoicesHeight: {PILOT_CHOICES_H}\n"
                    f"  joinHeight: {JOIN_H}\n"
                    "  refreshSeconds: 20\n  publishDelaySeconds: 2\n  confirmSeconds: 3\n  maxFriendsRead: 30\n"
                    "  newSquadDomain: 4\n"
                    + SQUAD_WORDS)
    page.add("logic", 114, body)
    return page


# The PILOTS tab, built from Rev 99 C (Shombith picked it 2026-11-13; section 191): the crew PDA lying on the hangar's
# workbench (the backdrop, ArkRoom.Bench). Lying down, its glass shows the last pilot read (PdaGlassImage over the
# render's own glass corners) and PICK UP stands beside it; picked up, the backdrop holds the PDA square to the view and
# the crew (one cell a callsign, a crew holds at most one pilot a callsign, Docs/Pilots/INVENTORY.md 3.1) or a pilot's
# file is laid on its glass. The corners are the renders' (Docs/MenuRedesignB/rounds/r99_ark_pilots_render.py, pda_c
# and pda_c PDA_HELD=1), measured at 2400 x 1350 and written here at the backdrop's 1920 x 1080.
CALLSIGNS = len(re.findall(r"^\s+\w+ = [1-9]\d*,", open(os.path.join(
    REPO, "Assets/_Scripts/Data/Enums/PilotVision.cs"), encoding="utf-8").read(), re.M))
# TL TR BR BL of the display lying on the bench: the nautilus's standing sheet of light (Rev 109 B, picked 2026-11-13)
PDA_REST_CORNERS = ((709.5, 682.9), (1186.6, 671.2), (1139.9, 870.4), (743.6, 885.2))
PDA_GLASS_ASPECT = 0.325 / 0.236                     # the display's own width over height (r109_ark_pda_render.py SW / SD)
GLASS_X, GLASS_Y, GLASS_W, GLASS_H = 403, 194, 1114, 809   # held up: the glass, square to the view
LAST_X, LAST_Y, LAST_W, LAST_H = 1280, 812, 440, 176       # lying down: the last pilot read and PICK UP, right of the PDA
SCREEN_PAD, SCREEN_HEAD = 28, 52                    # the glass's margin and its header strip
SCREEN_GROUND_ALPHA = 0.9                           # the lit screen's ground over the render's pale glass
# The ground is near-black, not the theme's navy (Shombith, 2026-11-13: "I don't like the blue background, it looks so
# dull"): the crew's paintings carry the colour, as the pilot scenes inside the Ark do.
SCREEN_GROUND = "SurfaceBlack"
PILOT_COLS, PILOT_W, PILOT_H, PILOT_GAP = 6, 164, 212, 14
PILOT_X = (GLASS_W - (PILOT_COLS * PILOT_W + (PILOT_COLS - 1) * PILOT_GAP)) // 2
PILOT_Y = SCREEN_HEAD + 20                          # the grid's top left on the glass
# A crew cell is a portrait card: the close-up fills it (square, cut to the card's width), a fade darkens its foot, and
# the words stand on the fade, left-aligned: the callsign large, the name, and what the callsign sees.
PILOT_FACE, PILOT_FACE_Y, PILOT_PAD = PILOT_H, 0, 10    # the close-up's side and top, and the words' inset
PILOT_FADE_Y = 56                                   # where the foot's fade starts
PILOT_LINE_Y, PILOT_LINE_H, PILOT_LINE_ROLE = 106, 28, "H3"     # the callsign
PILOT_NAME_Y, PILOT_NAME_H = 134, 20                # the name under it
# Under the name, the pilot's ability: what their sight shows, in two lines at most (Shombith, 2026-11-13).
PILOT_SIGHT_Y, PILOT_SIGHT_LINES, PILOT_SIGHT_ROLE = 158, 2, "BodySmall"
PILOT_SIGHT_H = PILOT_SIGHT_LINES * 20
PILOT_NAME_ROLE = "BodySmall"
FOOT_Y, FOOT_H = GLASS_H - 20 - 44, 44              # the glass's foot row: LAY DOWN, or CREW / PREV / NEXT / FULL PROFILE
# The name must hold the widest name PilotGenerator's tables compose (measured 2026-11-12 over its given and family names,
# in the UI font): on the PDA's glass at the kit's shrink-to-fit floor (KitText shrinks a label to 60 % before it clips).
PILOT_NAME_PAD, WIDEST_NAME = PILOT_PAD, "Darthawen Lanternweaver"
FILE_FACE = 320                                     # the file's close-up
FILE_COL_X = SCREEN_PAD + FILE_FACE + SCREEN_PAD    # the file's words column
FILE_COL_W = GLASS_W - FILE_COL_X - SCREEN_PAD
SCENE_CATALOG = K.meta_guid("Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset")     # author_pilot_scene_assets.py
PILOT_LINES = (("nameTemplate", "{name}"), ("lineTemplate", "{callsign}"), ("sightTemplate", "Sees {brief}"),
               ("countTemplate", "Pilots: {count}"),
               ("lastCallsignTemplate", "{callsign}"), ("lastNameTemplate", "{name}"),
               ("fileKickerTemplate", "{callsign}"), ("fileNameTemplate", "{name}"), ("fileSpeciesTemplate", "{species}"),
               ("fileFactionTemplate", "{faction} - {homeworld}"), ("fileSightCaptionTemplate", "Sight"),
               ("fileSightTemplate", "{sight}"), ("fileStoryCaptionTemplate", "Story"),
               ("fileStoryTemplate", "{backstory}"), ("fileCountTemplate", "{index} / {count}"))
FILE_TEMPLATES = [f for f, _ in PILOT_LINES if f.startswith(("last", "file"))]


def pilot_tokens():
    """The tokens ArkPilotsPage fills in each of its lines, read from its own code, so the lists cannot drift from the
    page: a cell's two lines take the pilot's facts (its TemplateText.Fill call), the count takes {count} alone, and the
    file's and the last read's lines take every token of its FileTokens."""
    source = open(os.path.join(REPO, "Assets/_Scripts/UI/Elements/Ark/ArkPilotsPage.cs"), encoding="utf-8").read()
    cell = re.search(r"TemplateText\.Fill\(template,(.*?)\);", source, re.S)
    count = re.search(r"TemplateText\.Fill\(countTemplate,(.*?)\);", source, re.S)
    filed = re.search(r"FileTokens\s*=\s*\{(.*?)\};", source, re.S)
    assert cell and count and filed, "ArkPilotsPage.cs no longer fills its lines where author_menu_ark.py reads them"
    cell, count, filed = (tuple(re.findall(r'"(\w+)"', m.group(1))) for m in (cell, count, filed))
    assert cell and count and filed, "ArkPilotsPage's filled tokens read as empty"
    return dict({"nameTemplate": cell, "lineTemplate": cell, "sightTemplate": cell, "countTemplate": count},
                **{f: filed for f in FILE_TEMPLATES})


PILOT_TOKENS = pilot_tokens()
PILOT_WORDS = "".join(yfield(field, words) for field, words in PILOT_LINES)


def callsign_briefs():
    """Every callsign's brief, as CallsignSet.asset holds it (author_callsign_assets.py writes it)."""
    path = os.path.join(REPO, "Assets/Resources/Pilots/CallsignSet.asset")
    found = [v.strip().strip("'").replace("''", "'") for v in
             re.findall(r"^    brief: (.*)$", open(path, encoding="utf-8").read(), re.M)]
    assert len(found) == CALLSIGNS and all(found), "CallsignSet.asset does not hold a brief for every callsign"
    return found


def callsign_words():
    """Every callsign's word, as CallsignSet.asset holds it."""
    path = os.path.join(REPO, "Assets/Resources/Pilots/CallsignSet.asset")
    found = [v.strip().strip("'") for v in re.findall(r"^    word: (.*)$", open(path, encoding="utf-8").read(), re.M)]
    assert len(found) == CALLSIGNS and all(found), "CallsignSet.asset does not hold a word for every callsign"
    return found


def pilot_card(parent, i):
    """Crew cell `i` (a SquadPickChip): the callsign's close-up filling the card, cut by a mask to its width, a fade up
    from its foot, the callsign large, the name, the sight, and the picked mark over all of it."""
    key = f"Pilots/Cell{i}"
    x, y = pilot_cell_rect(i)
    chip = glass(H.kit_row(parent, key, f"Cell{i}", TL(x, y, PILOT_W, PILOT_H), PILOT_H,
                           content_keys=((f"{key}/Label", "tmp"),)), alpha=1.0)
    chip.add("cg", 225, canvas_group(True))
    clip = chip.child(N(f"{key}/Clip", "Clip", STRETCH))
    clip.add("cr", 222, K.canvas_renderer()).add("rmask", 114, A.rect_mask())
    clip.child(N(f"{key}/Picture", "Picture", TL((PILOT_W - PILOT_FACE) // 2, PILOT_FACE_Y, PILOT_FACE, PILOT_FACE))) \
        .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None, preserve=True))
    image_node(clip, f"{key}/Fade", "Fade", TL(0, PILOT_FADE_Y, PILOT_W, PILOT_H - PILOT_FADE_Y), "SurfaceBlack",
               K.kit_sprite("kit_fade_v"))
    words_w = PILOT_W - 2 * PILOT_PAD
    H.ellipsize(label(chip.child(N(f"{key}/Line", "Line", TL(PILOT_PAD, PILOT_LINE_Y, words_w, PILOT_LINE_H))), "",
                      PILOT_LINE_ROLE, "TextLight"))
    label(chip.child(N(f"{key}/Label", "Label", TL(PILOT_NAME_PAD, PILOT_NAME_Y, PILOT_W - 2 * PILOT_NAME_PAD,
                                                   PILOT_NAME_H))), "", PILOT_NAME_ROLE, "TextLight")
    label(chip.child(N(f"{key}/Sight", "Sight", TL(PILOT_PAD, PILOT_SIGHT_Y, words_w, PILOT_SIGHT_H))),
          "", PILOT_SIGHT_ROLE, "TextLight", valign="top", wrap=True)
    chip.child(N(f"{key}/Picked", "Picked", R((0, 0), (1, 1), (0, 0), (2, 2)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, off(K.image(tok("Selection"), K.kit_sprite("kit_sliver10_line2"), True)))

    def body(ctx, go):
        return mono(go, S["chip"], f"  button: {ref(ctx.c(key, 'button'))}\n  group: {ref(ctx.c(key, 'cg'))}\n"
                    f"  label: {ref(ctx.c(f'{key}/Label', 'tmp'))}\n  line: {ref(ctx.c(f'{key}/Line', 'tmp'))}\n"
                    f"  image: {ref(ctx.c(f'{key}/Picture', 'img'))}\n"
                    f"  selectedMark: {ref(ctx.c(f'{key}/Picked', 'img'))}\n")
    chip.add("chip", 114, body)
    return chip


def pilot_cell_rect(i):
    """Cell `i` of the crew, left to right then down: (x, y) of its top left on the PDA's glass."""
    return (PILOT_X + (i % PILOT_COLS) * (PILOT_W + PILOT_GAP), PILOT_Y + (i // PILOT_COLS) * (PILOT_H + PILOT_GAP))


def frac(x, y):
    """A point of the 1920 x 1080 backdrop as a fraction of the page, from its bottom left (an anchor)."""
    return (round(x / 1920, 5), round(1 - y / 1080, 5))


def glass_image(parent, key, name):
    """The PDA's glass lying on the bench (PdaGlassImage): the last pilot's close-up drawn through the render's corners."""
    n = parent.child(N(key, name, STRETCH))
    corners = "".join(f"  - {{x: {round(x / 1920, 5)}, y: {round(y / 1080, 5)}}}\n" for x, y in PDA_REST_CORNERS)
    n.add("cr", 222, K.canvas_renderer()).add("glass", 114, lambda ctx, go: mono(
        go, S["glass"],
        "  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 0\n"
        "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
        "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
        "  sprite: {fileID: 0}\n  corners:\n" + corners
        + f"  glassAspect: {round(PDA_GLASS_ASPECT, 4)}\n  cutFromTop: 0.3\n  grid: 8\n"))
    return n


def pilots_rest(page):
    """Lying down: the PDA's glass shows the last pilot read; their callsign and name and PICK UP stand right of it."""
    rest = page.child(N("Pilots/Rest", "Rest", STRETCH))
    rest.add("cg", 225, canvas_group(True))
    glass_image(rest, "Pilots/Rest/Glass", "Glass")
    pn = glass_panel(rest, "Pilots/Rest/Last", "Last", TL(LAST_X, LAST_Y, LAST_W, LAST_H))
    C.section_label(pn, "Pilots/Rest/Last/Kicker", "Kicker", "Last read", TL(22, 18, LAST_W - 44, 16))
    H.ellipsize(label(pn.child(N("Pilots/Rest/Last/Callsign", "Callsign", TL(22, 38, LAST_W - 44, 38))), "", "H2",
                      "TextLight"))
    H.ellipsize(label(pn.child(N("Pilots/Rest/Last/Name", "Name", TL(22, 78, LAST_W - 44, 24))), "", "Body",
                      "NeutralLightest"))
    small_button(pn, "Pilots/Rest/Last/PickUp", "PickUp", "Pick up", TL(22, 114, 200, 44), "Cta")


def pilots_crew(screen):
    """Picked up, the crew on the glass: one portrait card a pilot (pilot_card), the empty words, and LAY DOWN."""
    crew = screen.child(N("Pilots/Held/Screen/Crew", "Crew", STRETCH))
    crew.add("cg", 225, canvas_group(True))
    e = crew.child(N("Pilots/Empty", "Empty", TL(0, 250, GLASS_W, 200)))
    e.add("cg", 225, canvas_group(False))
    label(e.child(N("Pilots/Empty/Heading", "Heading", stretch_x(0, 0, 40, 40))), "No pilots yet", "H2", "TextLight",
          align="center")
    label(e.child(N("Pilots/Empty/Line", "Line", stretch_x(200, 200, 90, 60))),
          "Your crew appears here once your account has loaded.", "Body", "NeutralLightest", align="center", wrap=True)
    for i in range(CALLSIGNS):
        pilot_card(crew, i)
    label(crew.child(N("Pilots/Held/Screen/Crew/Hint", "Hint", TL(SCREEN_PAD, FOOT_Y + 13, 500, 18))),
          "Tap a pilot to read their file.", "BodySmall", "NeutralLightest")
    small_button(crew, "Pilots/Held/Screen/Crew/PutDown", "PutDown", "Lay down", TR(SCREEN_PAD, FOOT_Y, 170, FOOT_H))


def pilots_file(screen):
    """A pilot's file on the glass: the close-up and the callsign's sight down the left; the callsign, name, animals,
    faction and home, and the story to the right; CREW, PREV / NEXT with the count, and FULL PROFILE along the foot."""
    f = screen.child(N("Pilots/Held/Screen/File", "File", STRETCH))
    f.add("cg", 225, canvas_group(False))
    k = "Pilots/Held/Screen/File"
    art_node(f, f"{k}/Picture", "Picture", TL(SCREEN_PAD, PILOT_Y, FILE_FACE, FILE_FACE), None, preserve=True)
    label(f.child(N(f"{k}/SightCaption", "SightCaption", TL(SCREEN_PAD, PILOT_Y + FILE_FACE + 16, FILE_FACE, 16))), "",
          "Label", "NeutralLightest")
    label(f.child(N(f"{k}/Sight", "Sight", TL(SCREEN_PAD, PILOT_Y + FILE_FACE + 38, FILE_FACE, FOOT_Y - PILOT_Y - FILE_FACE - 54))),
          "", "BodySmall", "TextLight", valign="top", wrap=True)
    x, w = FILE_COL_X, FILE_COL_W
    H.ellipsize(label(f.child(N(f"{k}/Kicker", "Kicker", TL(x, PILOT_Y, w, 18))), "", "Label", "NeutralLightest"))
    H.ellipsize(label(f.child(N(f"{k}/Name", "Name", TL(x, PILOT_Y + 22, w, 48))), "", "H1", "TextLight"))
    H.ellipsize(label(f.child(N(f"{k}/Species", "Species", TL(x, PILOT_Y + 76, w, 24))), "", "Body", "NeutralLightest"))
    H.ellipsize(label(f.child(N(f"{k}/Faction", "Faction", TL(x, PILOT_Y + 102, w, 24))), "", "Body", "NeutralLightest"))
    image_node(f, f"{k}/Rule", "Rule", TL(x, PILOT_Y + 140, w, 2), "SurfaceDark")
    label(f.child(N(f"{k}/StoryCaption", "StoryCaption", TL(x, PILOT_Y + 156, w, 16))), "", "Label", "NeutralLightest")
    label(f.child(N(f"{k}/Story", "Story", TL(x, PILOT_Y + 180, w, FOOT_Y - PILOT_Y - 196))), "", "BodySmall", "TextLight",
          valign="top", wrap=True)
    small_button(f, f"{k}/Back", "Back", "Crew", TL(SCREEN_PAD, FOOT_Y, 140, FOOT_H))
    mid = GLASS_W // 2
    small_button(f, f"{k}/Previous", "Previous", "Prev", TL(mid - 60 - 8 - 120, FOOT_Y, 120, FOOT_H))
    label(f.child(N(f"{k}/Count", "Count", TL(mid - 60, FOOT_Y + 10, 120, 24))), "", "Body", "TextLight", align="center")
    small_button(f, f"{k}/Next", "Next", "Next", TL(mid + 60 + 8, FOOT_Y, 120, FOOT_H))
    small_button(f, f"{k}/Profile", "Profile", "Full profile", TR(SCREEN_PAD, FOOT_Y, 200, FOOT_H))


def pilots_held(page):
    """Picked up: the glass square to the view (anchored to the render's corners, so it follows the stretched picture),
    its header (the caption and the crew's count), and the crew or a pilot's file."""
    held = page.child(N("Pilots/Held", "Held", STRETCH))
    held.add("cg", 225, canvas_group(False))
    screen = held.child(N("Pilots/Held/Screen", "Screen", R(frac(GLASS_X, GLASS_Y + GLASS_H), frac(GLASS_X + GLASS_W, GLASS_Y),
                                                             (0, 0), (0, 0))))
    # The screen lit: the render's glass is pale under the hangar's grade, so the words stand on the theme's dark ground.
    image_node(screen, "Pilots/Held/Screen/Ground", "Ground", STRETCH, SCREEN_GROUND, alpha=SCREEN_GROUND_ALPHA)
    image_node(screen, "Pilots/Held/Screen/Head", "Head", TL(0, 0, GLASS_W, SCREEN_HEAD), "SurfaceBlack", alpha=0.55)
    C.section_label(screen, "Pilots/Caption", "Caption", "Your crew", TL(SCREEN_PAD, 17, 300, 18))
    label(screen.child(N("Pilots/Count", "Count", TR(SCREEN_PAD, 17, 220, 18))), "", "Label", "NeutralLightest",
          align="right")
    pilots_crew(screen)
    pilots_file(screen)


def pilots_page(root, veiled=VEILED):
    """PILOTS (Rev 99 C, section 191): the crew PDA on the hangar's workbench. ArkPilotsPage draws it; every word is on
    that component or the text of a caption here."""
    page = page_node(root, "Pilots", False)
    page_ground(page, "Pilots", veiled)
    pilots_rest(page)
    pilots_held(page)
    F = "Pilots/Held/Screen/File"

    def body(ctx, go):
        def r(key, comp):
            return ref(ctx.c(key, comp))
        return mono(go, S["pilots"],
                    f"  scenes: {K.ref(11400000, SCENE_CATALOG, 2)}\n"
                    "  cells:\n" + refs_of(ctx, [f"Pilots/Cell{i}" for i in range(CALLSIGNS)], "chip")
                    + "  sightLines:\n" + refs_of(ctx, [f"Pilots/Cell{i}/Sight" for i in range(CALLSIGNS)], "tmp")
                    + f"  emptyGroup: {r('Pilots/Empty', 'cg')}\n"
                    f"  countText: {r('Pilots/Count', 'tmp')}\n  screenSwitcher: {{fileID: 0}}\n"
                    f"  restGroup: {r('Pilots/Rest', 'cg')}\n  pickUpButton: {r('Pilots/Rest/Last/PickUp', 'button')}\n"
                    f"  glass: {r('Pilots/Rest/Glass', 'glass')}\n"
                    f"  lastCallsignText: {r('Pilots/Rest/Last/Callsign', 'tmp')}\n"
                    f"  lastNameText: {r('Pilots/Rest/Last/Name', 'tmp')}\n"
                    f"  heldGroup: {r('Pilots/Held', 'cg')}\n  crewGroup: {r('Pilots/Held/Screen/Crew', 'cg')}\n"
                    f"  putDownButton: {r('Pilots/Held/Screen/Crew/PutDown', 'button')}\n"
                    "  settleSeconds: 0.4\n  fadeSeconds: 0.2\n"
                    f"  fileGroup: {r(F, 'cg')}\n  filePicture: {r(F + '/Picture', 'img')}\n"
                    f"  fileKickerText: {r(F + '/Kicker', 'tmp')}\n  fileNameText: {r(F + '/Name', 'tmp')}\n"
                    f"  fileSpeciesText: {r(F + '/Species', 'tmp')}\n  fileFactionText: {r(F + '/Faction', 'tmp')}\n"
                    f"  fileSightCaptionText: {r(F + '/SightCaption', 'tmp')}\n  fileSightText: {r(F + '/Sight', 'tmp')}\n"
                    f"  fileStoryCaptionText: {r(F + '/StoryCaption', 'tmp')}\n  fileStoryText: {r(F + '/Story', 'tmp')}\n"
                    f"  fileCountText: {r(F + '/Count', 'tmp')}\n  backButton: {r(F + '/Back', 'button')}\n"
                    f"  previousButton: {r(F + '/Previous', 'button')}\n  nextButton: {r(F + '/Next', 'button')}\n"
                    f"  profileButton: {r(F + '/Profile', 'button')}\n"
                    + PILOT_WORDS)
    page.add("logic", 114, body)
    return page


# ARTIFACTS as Rev 105 B draws it (Docs/MENU_REDESIGN_B.md sections 193 and 201; Shombith, 2026-11-13: "B is good",
# after "the UI cards on top of each screen, I do not like them at all"): no card and no panel behind any word, the
# Reliquary whole behind the page, and three levels, one press each (ArkArtifactsPage.Level): the hoard (a rail of
# medallions along the floor), one artifact (the lens opens on the right and plays its payload over prisms, its four
# tracks round the rim; its words stand on the left), and feeding it (the attuned track's level, its crystals, FEED).
_gspec = importlib.util.spec_from_file_location("author_artifact_glyphs", os.path.join(HERE, "author_artifact_glyphs.py"))
GLYPHS = importlib.util.module_from_spec(_gspec)
_gspec.loader.exec_module(GLYPHS)

TRACK_ELEMENTS = ("Mass", "Space", "Time", "Charge")   # ArkArtifactsPage.TrackOrder: the lens's arcs, in this order
# Each kind: its catalog id, its mark, its payload (ArtifactLens.Payload: Fan 0, Line 1, Repeat 2, Shell 3), the track
# whose value counts the payload (every kind counts on Charge: heads, cracks, shocks, re-raises) and what is added to it.
ART_KINDS = (("javelin", 1, 0), ("hydra", 0, 0), ("aftershock", 2, 0), ("aegis", 3, 1))
ELEMENT_VALUE = dict(Charge=1, Mass=2, Space=3, Time=4)       # Element
COUNT_TRACK = "Charge"
CRYSTAL_BY_VALUE = (None, CRYSTAL["Charge"], CRYSTAL["Mass"], CRYSTAL["Space"], CRYSTAL["Time"], OMNI)
# The rail (the board's BODY_B): a line along the floor, medallions 150 wide from x 170 every 186, the disc 112 across.
MEDALLIONS, MED_X, MED_STEP, MED_W, MED_Y, MED_DISC = 7, 170, 186, 150, 834, 112
RAIL_Y = MED_Y + MED_DISC // 2
HINT_Y = 786
# The lens: 640 across, its top left at (1180, 196); the field of prisms is 300 in radius, the arcs run at 318 and their
# words at 370, each arc 80 degrees, the first starting at 95 (SVG degrees, clockwise from the right).
LENS_X, LENS_Y, LENS = 1180, 196, 640
FIELD_R, ARC_R, WORDS_R, ARC_SPAN, ARC_START = 300, 318, 370, 80, 95
ARC_WORDS_W, ARC_WORDS_H = 150, 66
# The words on the left, no box: from (80, 232), 560 wide (520 for the feed's meter and its buttons).
COL_X, COL_Y, COL_W, METER_W = 80, 232, 560, 520
ACTIONS_Y, BUTTON_H = 488, 44
BAND_Y, CRYSTAL_ICONS, CRYSTAL_ICON, CRYSTAL_STEP = 524, 10, 42, 48
STATUS_Y = 776
assert STATUS_Y + 50 < MED_Y, "the feed's status line runs into the rail"
assert COL_X + COL_W < LENS_X - (WORDS_R - LENS // 2) - ARC_WORDS_W // 2, "the artifact's words run under the lens's arc words"


def artifact_words():
    """Every word ArkArtifactsPage shows, as (field, words). Templates name only the tokens ART_TOKENS lists."""
    return (
        ("trailHoard", "Artifacts"), ("trailArtifact", "Artifacts  /  {kind}"),
        ("trailFeed", "Artifacts  /  {kind}  /  Feed"), ("kickerTemplate", "{kind} - {serial}"),
        ("attunedTemplate", "{element} - {aspect}  {value}"), ("heldTemplate", "Carried in {squad}, the {seat} seat"),
        ("notHeldWord", "In your hoard - no seat carries it"), ("giveWord", "Give to a seat"),
        ("giveAgainWord", "Give to another seat"), ("levelNowTemplate", "{level}"),
        ("levelNextTemplate", "{level}  >  {next}"),
        ("valueTemplate", "Next feed: {now} > {next} {aspect}. {fed} crystals fed so far."),
        ("twistLockedTemplate", "At {at}: {twist}"), ("twistOpenTemplate", "{twist}"),
        ("balanceTemplate", "{element} crystals {count} - Omni {omni} - a feed takes {batch}"),
        ("feedTemplate", "Feed {count} {element}"), ("omniTemplate", "Feed {count} Omni"),
        ("notEnoughTemplate", "Not enough {element} crystals. The Converter makes more."),
        ("wrongElementTemplate", "{artifact} takes only {element}."),
        ("goneWord", "That artifact is no longer in your hoard."),
        ("fedTemplate", "{artifact}: {aspect} is now level {level} (+{gain})."),
        ("twistedTemplate", "{artifact} twists: {twist}"))


# The tokens ArkArtifactsPage fills in each line (its TemplateText.Fill calls).
ART_TOKENS = dict(trailHoard={"kind"}, trailArtifact={"kind"}, trailFeed={"kind"}, kickerTemplate={"kind", "serial"},
                  attunedTemplate={"element", "aspect", "value"}, heldTemplate={"squad", "seat"},
                  levelNowTemplate={"level"}, levelNextTemplate={"level", "next"},
                  valueTemplate={"now", "next", "aspect", "fed"}, twistLockedTemplate={"twist", "at"},
                  twistOpenTemplate={"twist"}, balanceTemplate={"element", "count", "omni", "batch"},
                  feedTemplate={"count", "element"}, omniTemplate={"count"}, notEnoughTemplate={"element"},
                  wrongElementTemplate={"artifact", "element"}, fedTemplate={"artifact", "aspect", "element", "level", "gain"},
                  twistedTemplate={"artifact", "twist"})
ARTIFACT_WORDS = (ylist("elementNames", ("", "Charge", "Mass", "Space", "Time", "Omni"))
                  + ylist("seatNames", ("Leader", "Rogue One", "Rogue Two"))
                  + "".join(yfield(f, w) for f, w in artifact_words()))


def filled(parent, key, name, r, token, alpha=1.0, sprite=None, method=0, origin=0, amount=0.0):
    """A themed Image, Filled: `method` 0 horizontal (from the left), 4 radial 360 (from `origin`, clockwise)."""
    base = K.image(tok(token, alpha), sprite)
    n = parent.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, lambda ctx, go: base(ctx, go)
        .replace("  m_Type: 0\n", "  m_Type: 3\n").replace("  m_FillMethod: 4\n", f"  m_FillMethod: {method}\n")
        .replace("  m_FillAmount: 1\n", f"  m_FillAmount: {K.f32(amount)}\n")
        .replace("  m_FillOrigin: 0\n", f"  m_FillOrigin: {origin}\n")).add("th", 114, H.themed(token, alpha))
    return n


def ring(parent, key, name, r, token, alpha=1.0, sprite="kit_ring_line", raycast=False, shown=True):
    n = image_node(parent, key, name, r, token, K.kit_sprite(sprite), alpha=alpha, raycast=raycast)
    if not shown:
        n.comps = [(k, cid, off(b)) if k == "img" else (k, cid, b) for k, cid, b in n.comps]
    return n


def medallion(page, i):
    """One medallion on the rail (ArtifactMedallion): a held artifact's mark in a round glass with its kind's name under
    it, or an empty slot. The glass is a KitButton (its own fill the theme's tint over the glass disc, the resting ring its
    border); the picked ring is a second ring the medallion switches on."""
    k = f"Artifacts/Rail/Medallion{i}"
    m = page.child(N(k, f"Medallion{i}", TL(MED_X + i * MED_STEP, MED_Y, MED_W, MED_W)))
    disc = TL((MED_W - MED_DISC) // 2, 0, MED_DISC, MED_DISC)
    held = m.child(N(f"{k}/Held", "Held", STRETCH))
    held.add("cg", 225, canvas_group(True))
    ring(held, f"{k}/Held/Glass", "Glass", disc, "SurfaceVeryDark", 0.9, "kit_ring_fill")
    focus = held.child(N(f"{k}/Held/FocusRing", "FocusRing", TL((MED_W - MED_DISC) // 2 - 6, -6, MED_DISC + 12, MED_DISC + 12)))
    focus.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight", 0.0), K.kit_sprite("kit_ring_line")))
    held.child(N(f"{k}/Held/Fill", "Fill", disc)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.0), K.kit_sprite("kit_ring_fill"), raycast=True))
    held.child(N(f"{k}/Held/Rest", "Rest", disc)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_ring_line")))
    ring(held, f"{k}/Held/Picked", "Picked", TL((MED_W - MED_DISC) // 2 - 2, -2, MED_DISC + 4, MED_DISC + 4), "ControlActive",
         shown=False)
    held.child(N(f"{k}/Held/Glyph", "Glyph", TL((MED_W - 64) // 2, (MED_DISC - 64) // 2, 64, 64))) \
        .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None, preserve=True)) \
        .add("th", 114, H.themed("TextLight"))
    ng = held.child(N(f"{k}/Held/NameGroup", "NameGroup", TL(0, MED_DISC + 10, MED_W, 20)))
    ng.add("cg", 225, canvas_group(True))
    H.ellipsize(label(ng.child(N(f"{k}/Held/Name", "Name", STRETCH)), "", "Label", "TextLight", align="center"))
    empty = m.child(N(f"{k}/Empty", "Empty", STRETCH))
    empty.add("cg", 225, canvas_group(False))
    ring(empty, f"{k}/Empty/Ring", "Ring", disc, "NeutralLightest", 0.35)
    label(empty.child(N(f"{k}/Empty/Mark", "Mark", disc)), "?", "H2", "NeutralLightest", align="center")

    def button(ctx, go):
        return K.mono(go, H.KIT_BUTTON,
                      K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Held/Fill', 'img'))}\n" + H.EMPTY_CALLS
                      + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{k}/Held/FocusRing', 'img'))}\n"
                      f"  variant: {K.VARIANTS['Cancel']}\n  fill: 0\n"
                      f"  fillGraphic: {ref(ctx.c(f'{k}/Held/Fill', 'img'))}\n"
                      f"  borderGraphic: {ref(ctx.c(f'{k}/Held/Rest', 'img'))}\n"
                      f"  contentGraphics:\n  - {ref(ctx.c(f'{k}/Held/Name', 'tmp'))}\n  playGradient: {{fileID: 0}}\n")
    m.add("button", 114, button)
    m.add("audio", 114, lambda ctx, go: K.mono(go, H.MENU_AUDIO, "  category: 11\n"))
    m.add("medal", 114, lambda ctx, go: mono(go, S["medal"],
          f"  button: {ref(ctx.c(k, 'button'))}\n  heldGroup: {ref(ctx.c(f'{k}/Held', 'cg'))}\n"
          f"  emptyGroup: {ref(ctx.c(f'{k}/Empty', 'cg'))}\n  glyph: {ref(ctx.c(f'{k}/Held/Glyph', 'img'))}\n"
          f"  nameText: {ref(ctx.c(f'{k}/Held/Name', 'tmp'))}\n  restRing: {ref(ctx.c(f'{k}/Held/Rest', 'img'))}\n"
          f"  pickedRing: {ref(ctx.c(f'{k}/Held/Picked', 'img'))}\n  nameGroup: {ref(ctx.c(f'{k}/Held/NameGroup', 'cg'))}\n"
          "  dimmedNameAlpha: 0.6\n"))
    return m


def arc_start(i):
    """Arc `i`'s start in SVG degrees (clockwise from the right, as the board draws it)."""
    return ARC_START + 90 * i


def lens(page):
    """The lens (ArtifactLens): a round glass with a rim, the field of prisms masked inside it, and the four tracks as arcs
    round the rim with their words outside. Hidden until an artifact is picked."""
    L = "Artifacts/Lens"
    ln = page.child(N(L, "Lens", TL(LENS_X, LENS_Y, LENS, LENS)))
    ln.add("cg", 225, canvas_group(False))
    body = ln.child(N(f"{L}/Body", "Body", STRETCH))
    ring(body, f"{L}/Body/Halo", "Halo", R((0, 0), (1, 1), (0, 0), (30, 30)), "ControlActive", 0.35)
    ring(body, f"{L}/Body/Glass", "Glass", STRETCH, "SurfaceVeryDark", 0.94, "kit_ring_fill")
    ring(body, f"{L}/Body/Rim", "Rim", STRETCH, "ControlActive", 0.75)
    inset = LENS - 2 * FIELD_R
    fd = body.child(N(f"{L}/Body/Field", "Field", R((0, 0), (1, 1), (0, 0), (-inset, -inset))))
    fd.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack", 0.35), K.kit_sprite("kit_ring_fill"))) \
        .add("th", 114, H.themed("SurfaceBlack", 0.35)).add("mask", 114, A.mask(True))
    centre = lambda w, h, y=0: R((0.5, 0.5), None, (0, y), (w, h))
    image_node(fd, f"{L}/Body/Field/Release", "Release", centre(28, 28, -0.82 * FIELD_R), "TextLight",
               K.chrome_sprite("chrome_diamond"), alpha=0.6, preserve=True)
    image_node(fd, f"{L}/Body/Field/Prism", "Prism", centre(22, 12), "SurfaceLight", alpha=0.5)
    image_node(fd, f"{L}/Body/Field/Head", "Head", centre(14, 14), "ControlActive", K.kit_sprite("kit_ring_fill"))
    image_node(fd, f"{L}/Body/Field/Burst", "Burst", centre(40, 40), "TextLight", K.kit_sprite("kit_ring_line"))
    arc_rect = R((0, 0), (1, 1), (0, 0), (2 * ARC_R - LENS, 2 * ARC_R - LENS))
    span = ARC_SPAN / 360
    for i, name in enumerate(TRACK_ELEMENTS):
        k = f"{L}/Body/Arc{name}"
        a = body.child(N(k, f"Arc{name}", STRETCH))
        for part, token, alpha, amount in (("Track", "NeutralLightest", 0.35, span), ("Fixed", "TextLight", 0.55, span / 2),
                                            ("Attuned", "ControlActive", 1.0, span / 2)):
            f = filled(a, f"{k}/{part}", part, arc_rect, token, alpha, K.kit_sprite("kit_ring_line"), method=4, origin=1,
                       amount=amount)
            f.rot = -arc_start(i)                  # radial 360 from the right, clockwise: turned to start at the arc's start
        mid = math.radians(arc_start(i) + ARC_SPAN / 2)
        x, y = LENS / 2 + WORDS_R * math.cos(mid), LENS / 2 + WORDS_R * math.sin(mid)
        w = a.child(N(f"{k}/Words", "Words", TL(round(x - ARC_WORDS_W / 2), round(y - ARC_WORDS_H / 2), ARC_WORDS_W,
                                                ARC_WORDS_H)))
        w.add("cg", 225, canvas_group(True))
        H.element_flower(w, f"{k}/Words/Flower", "Flower", TL((ARC_WORDS_W - 24) // 2, 0, 24, 24), name, 5)
        label(w.child(N(f"{k}/Words/Value", "Value", TL(0, 26, ARC_WORDS_W, 20))), "", "DataSmall", "TextLight", align="center")
        H.ellipsize(label(w.child(N(f"{k}/Words/Label", "Label", TL(0, 46, ARC_WORDS_W, 18))), "", "Label", "NeutralLightest",
                          align="center"))

    def body_(ctx, go):
        arcs = ""
        for name in TRACK_ELEMENTS:
            k = f"{L}/Body/Arc{name}"
            arcs += (f"  - attunedFill: {ref(ctx.c(f'{k}/Attuned', 'img'))}\n    fixedFill: {ref(ctx.c(f'{k}/Fixed', 'img'))}\n"
                     f"    flower: {ref(ctx.c(f'{k}/Words/Flower', 'flower'))}\n"
                     f"    valueText: {ref(ctx.c(f'{k}/Words/Value', 'tmp'))}\n"
                     f"    labelText: {ref(ctx.c(f'{k}/Words/Label', 'tmp'))}\n"
                     f"    wordsGroup: {ref(ctx.c(f'{k}/Words', 'cg'))}\n")
        return mono(go, S["lens"],
                    f"  group: {ref(ctx.c(L, 'cg'))}\n  body: {ref(ctx.rt(f'{L}/Body'))}\n"
                    f"  field: {ref(ctx.rt(f'{L}/Body/Field'))}\n"
                    f"  prismTemplate: {ref(ctx.c(f'{L}/Body/Field/Prism', 'img'))}\n"
                    f"  headTemplate: {ref(ctx.c(f'{L}/Body/Field/Head', 'img'))}\n"
                    f"  burstTemplate: {ref(ctx.c(f'{L}/Body/Field/Burst', 'img'))}\n"
                    "  arcs:\n" + arcs + f"  arcSpan: {K.f32(span)}\n  fixedWordsAlpha: 0.6\n  prismCount: 40\n"
                    "  cycleSeconds: 2.4\n  flightShare: 0.55\n  openSeconds: 0.6\n  closedScale: 0.18\n  burstSize: 0.16\n"
                    "  maxCount: 8\n")
    ln.add("lens", 114, body_)
    return ln


def artifact_detail(page):
    """The picked artifact's words on the left, no box (levels 2 and 3), its two actions (level 2) and the feed (level 3)."""
    D = "Artifacts/Detail"
    d = page.child(N(D, "Detail", TL(COL_X, COL_Y, COL_W, STATUS_Y + 50 - COL_Y)))
    d.add("cg", 225, canvas_group(False))
    y = lambda v: v - COL_Y
    label(d.child(N(f"{D}/Kicker", "Kicker", TL(0, 0, COL_W, 16))), "", "Label", "NeutralLightest")
    H.ellipsize(label(d.child(N(f"{D}/Name", "Name", TL(0, 22, COL_W, 44))), "", "H1", "TextLight"))
    label(d.child(N(f"{D}/Description", "Description", TL(0, 74, COL_W, 84))), "", "Body", "NeutralLightest", valign="top",
          wrap=True)
    H.element_flower(d, f"{D}/Flower", "Flower", TL(0, 168, 30, 30), "Charge", 5)
    H.ellipsize(label(d.child(N(f"{D}/Attuned", "Attuned", TL(42, 170, COL_W - 42, 26))), "", "Body", "TextLight"))
    H.ellipsize(label(d.child(N(f"{D}/Held", "Held", TL(0, 210, COL_W, 20))), "", "BodySmall", "NeutralLightest"))
    act = d.child(N(f"{D}/Actions", "Actions", TL(0, y(ACTIONS_Y), COL_W, BUTTON_H)))
    act.add("cg", 225, canvas_group(True))
    small_button(act, f"{D}/Actions/FeedIt", "FeedIt", "Feed it", TL(0, 0, 160, BUTTON_H))
    small_button(act, f"{D}/Actions/Give", "Give", "Give to a seat", TL(172, 0, 240, BUTTON_H), "Cta")
    fg = d.child(N(f"{D}/Feed", "Feed", TL(0, y(ACTIONS_Y), COL_W, STATUS_Y + 50 - ACTIONS_Y)))
    fg.add("cg", 225, canvas_group(False))
    F = f"{D}/Feed"
    fy = lambda v: v - ACTIONS_Y
    label(fg.child(N(f"{F}/Caption", "Caption", TL(0, 8, 200, 16))), "Level", "Label", "NeutralLightest")
    label(fg.child(N(f"{F}/Level", "Level", TL(METER_W - 300, 0, 300, 28))), "", "Data", "TextLight", align="right")
    band = fg.child(N(f"{F}/Band", "Band", TL(0, fy(BAND_Y), METER_W, 6)))
    image_node(band, f"{F}/Band/Track", "Track", STRETCH, "NeutralLightest", alpha=0.3)
    filled(band, f"{F}/Band/Next", "Next", STRETCH, "Cta", 0.45, amount=0.5)
    filled(band, f"{F}/Band/Now", "Now", STRETCH, "ControlActive", amount=0.4)
    image_node(band, f"{F}/Band/TwistMark", "TwistMark", R((0.5, 0), (0.5, 1), (0, 0), (2, 12)), "TextLight", alpha=0.8)
    image_node(band, f"{F}/Band/Ceiling", "Ceiling", R((1, 0), (1, 1), (0, 0), (1, 8)), "NeutralLightest")
    tw = fg.child(N(f"{F}/Twist", "Twist", TL(0, fy(BAND_Y) + 20, METER_W, 44)))
    tw.add("cg", 225, canvas_group(True))
    label(tw.child(N(f"{F}/Twist/Words", "Words", STRETCH)), "", "BodySmall", "TextLight", valign="top", wrap=True)
    H.ellipsize(label(fg.child(N(f"{F}/Value", "Value", TL(0, fy(BAND_Y) + 70, METER_W, 20))), "", "DataSmall",
                      "NeutralLightest"))
    icons_y = fy(BAND_Y) + 104
    for i in range(CRYSTAL_ICONS):
        fg.child(N(f"{F}/Crystal{i}", f"Crystal{i}", TL(i * CRYSTAL_STEP, icons_y, CRYSTAL_ICON, CRYSTAL_ICON))) \
            .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, CRYSTAL["Charge"], preserve=True))
    H.ellipsize(label(fg.child(N(f"{F}/Balance", "Balance", TL(0, icons_y + CRYSTAL_ICON + 8, METER_W, 20))), "", "DataSmall",
                      "NeutralLightest"))
    buttons_y = icons_y + CRYSTAL_ICON + 36
    small_button(fg, f"{F}/FeedButton", "FeedButton", "Feed", TL(0, buttons_y, 250, BUTTON_H), "Cta")
    small_button(fg, f"{F}/Omni", "Omni", "Feed Omni", TL(262, buttons_y, 250, BUTTON_H))
    assert ACTIONS_Y + buttons_y + BUTTON_H + 8 <= STATUS_Y, "the feed's buttons run into its status line"
    label(d.child(N(f"{D}/Status", "Status", TL(0, y(STATUS_Y), COL_W, 44))), "", "BodySmall", "TextLight", valign="top",
          wrap=True)
    return d


def artifacts_page(root, veiled=VEILED):
    """ARTIFACTS (Rev 105 B): the hoard as a rail of medallions along the floor; a pick opens the lens and the artifact's
    words; FEED IT goes one level deeper; BACK one level up. ArkArtifactsPage draws it; every word is on that component."""
    page = page_node(root, "Artifacts", False)
    page_ground(page, "Artifacts", veiled)
    rail = page.child(N("Artifacts/Rail", "Rail", stretch_x(60, 60, RAIL_Y - 1, 2)))
    image_node(rail, "Artifacts/Rail/Line", "Line", STRETCH, "ControlActive", K.kit_sprite("kit_fade_h"), alpha=0.55)
    for i in range(MEDALLIONS):
        medallion(page, i)
    hint = page.child(N("Artifacts/Hint", "Hint", TL(MED_X, HINT_Y, 900, 28)))
    hint.add("cg", 225, canvas_group(True))
    label(hint.child(N("Artifacts/Hint/Words", "Words", STRETCH)), "Pick one to see what it does.", "Body", "TextLight")
    # No artifacts: the words stand over the rail, where the medallions would be.
    e = page.child(N("Artifacts/Empty", "Empty", TL(MED_X, MED_Y - 6, 1000, 110)))
    e.add("cg", 225, canvas_group(False))
    label(e.child(N("Artifacts/Empty/Heading", "Heading", TL(0, 0, 1000, 36))), "No artifacts yet", "H2", "TextLight")
    label(e.child(N("Artifacts/Empty/Line", "Line", TL(0, 44, 1000, 28))),
          "Find them on voyages and bring them home to your Ark.", "Body", "NeutralLightest")
    label(page.child(N("Artifacts/Trail", "Trail", TR(EDGE, TAB_Y + 16, 460, 20))), "", "Label", "TextLight", align="right")
    back = page.child(N("Artifacts/Back", "Back", TL(EDGE, TAB_Y + TAB_H + 4, 110, 38)))
    back.add("cg", 225, canvas_group(False))
    small_button(back, "Artifacts/Back/Button", "Button", "Back", STRETCH)
    lens(page)
    artifact_detail(page)
    D, F = "Artifacts/Detail", "Artifacts/Detail/Feed"

    def body(ctx, go):
        kinds = "".join(f"  - kindId: artifact.{kind}\n    glyph: {K.sprite_ref(GLYPHS.glyph_guid(kind))}\n"
                        f"    payload: {payload}\n    countTrack: {ELEMENT_VALUE[COUNT_TRACK]}\n    countOffset: {offset}\n"
                        for kind, payload, offset in ART_KINDS)
        sprites = "".join(f"  - {K.sprite_ref(g)}\n" for g in CRYSTAL_BY_VALUE)
        return mono(go, S["artifacts"],
                    f"  catalog: {K.ref(11400000, PILOT_CATALOG, 2)}\n  kinds:\n" + kinds
                    + "  medallions:\n" + refs_of(ctx, [f"Artifacts/Rail/Medallion{i}" for i in range(MEDALLIONS)], "medal")
                    + f"  hintGroup: {ref(ctx.c('Artifacts/Hint', 'cg'))}\n"
                    f"  emptyGroup: {ref(ctx.c('Artifacts/Empty', 'cg'))}\n"
                    f"  trailText: {ref(ctx.c('Artifacts/Trail', 'tmp'))}\n"
                    f"  lens: {ref(ctx.c('Artifacts/Lens', 'lens'))}\n"
                    f"  detailGroup: {ref(ctx.c(D, 'cg'))}\n  kickerText: {ref(ctx.c(f'{D}/Kicker', 'tmp'))}\n"
                    f"  nameText: {ref(ctx.c(f'{D}/Name', 'tmp'))}\n"
                    f"  descriptionText: {ref(ctx.c(f'{D}/Description', 'tmp'))}\n"
                    f"  attunedFlower: {ref(ctx.c(f'{D}/Flower', 'flower'))}\n"
                    f"  attunedText: {ref(ctx.c(f'{D}/Attuned', 'tmp'))}\n  heldText: {ref(ctx.c(f'{D}/Held', 'tmp'))}\n"
                    f"  actionsGroup: {ref(ctx.c(f'{D}/Actions', 'cg'))}\n"
                    f"  feedItButton: {ref(ctx.c(f'{D}/Actions/FeedIt', 'button'))}\n"
                    f"  giveButton: {ref(ctx.c(f'{D}/Actions/Give', 'button'))}\n"
                    f"  giveLabel: {ref(ctx.c(f'{D}/Actions/Give/Label', 'tmp'))}\n"
                    f"  backGroup: {ref(ctx.c('Artifacts/Back', 'cg'))}\n"
                    f"  backButton: {ref(ctx.c('Artifacts/Back/Button', 'button'))}\n"
                    f"  feedGroup: {ref(ctx.c(F, 'cg'))}\n  levelText: {ref(ctx.c(f'{F}/Level', 'tmp'))}\n"
                    f"  levelFill: {ref(ctx.c(f'{F}/Band/Now', 'img'))}\n  nextFill: {ref(ctx.c(f'{F}/Band/Next', 'img'))}\n"
                    f"  ceilingMark: {ref(ctx.rt(f'{F}/Band/Ceiling'))}\n"
                    f"  twistText: {ref(ctx.c(f'{F}/Twist/Words', 'tmp'))}\n  twistGroup: {ref(ctx.c(f'{F}/Twist', 'cg'))}\n"
                    f"  valueText: {ref(ctx.c(f'{F}/Value', 'tmp'))}\n"
                    "  crystalIcons:\n" + refs_of(ctx, [f"{F}/Crystal{i}" for i in range(CRYSTAL_ICONS)], "img")
                    + "  crystalSprites:\n" + sprites
                    + f"  balanceText: {ref(ctx.c(f'{F}/Balance', 'tmp'))}\n"
                    f"  feedButton: {ref(ctx.c(f'{F}/FeedButton', 'button'))}\n"
                    f"  feedLabel: {ref(ctx.c(f'{F}/FeedButton/Label', 'tmp'))}\n"
                    f"  omniButton: {ref(ctx.c(f'{F}/Omni', 'button'))}\n"
                    f"  omniLabel: {ref(ctx.c(f'{F}/Omni/Label', 'tmp'))}\n"
                    f"  statusText: {ref(ctx.c(f'{D}/Status', 'tmp'))}\n  feedBatch: 5\n"
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


PLATE_FIELDS = dict(button=S["button"], group=225, fill=IMG, edge=IMG, avatar=IMG, openMark=IMG, whoText=TMP, lineText=TMP,
                    dot=IMG)
SQUADS_FIELDS = dict(prevSquadButton=S["button"], nextSquadButton=S["button"], squadNameText=TMP, whoseText=TMP,
                     newButton=S["button"], joinButton=S["button"], removeButton=S["button"], removeLabel=TMP,
                     squadGroup=225, missionButton=S["button"], missionLock=IMG, arenaButton=S["button"],
                     emptyGroup=225, emptyMakeButton=S["button"], projectionGroup=225, projectionRoot=224,
                     projectionTitle=TMP, backButton=S["button"], actionButton=S["button"], actionLabel=TMP,
                     readOnlyText=TMP, choicesGroup=225, pilotChoicesGroup=225, choicePrevButton=S["button"],
                     choiceNextButton=S["button"], choiceHintText=TMP, joinGroup=225, joinStatusText=S["kt"])
FLOWER = H.FLOWER_SCRIPT
ART_FIELDS = dict(hintGroup=225, emptyGroup=225, trailText=TMP, lens=S["lens"], detailGroup=225, kickerText=TMP,
                  nameText=TMP, descriptionText=TMP, attunedFlower=FLOWER, attunedText=TMP, heldText=TMP, actionsGroup=225,
                  feedItButton=S["button"], giveButton=S["button"], giveLabel=TMP, backGroup=225, backButton=S["button"],
                  feedGroup=225, levelText=TMP, levelFill=IMG, nextFill=IMG, ceilingMark=224, twistText=TMP,
                  twistGroup=225, valueText=TMP, balanceText=TMP, feedButton=S["button"], feedLabel=TMP,
                  omniButton=S["button"], omniLabel=TMP, statusText=TMP)
MEDAL_FIELDS = dict(button=S["button"], heldGroup=225, emptyGroup=225, glyph=IMG, nameText=TMP, restRing=IMG, pickedRing=IMG,
                    nameGroup=225)
LENS_FIELDS = dict(group=225, body=224, field=224, prismTemplate=IMG, headTemplate=IMG, burstTemplate=IMG)
ARC_FIELDS = dict(attunedFill=IMG, fixedFill=IMG, flower=FLOWER, valueText=TMP, labelText=TMP, wordsGroup=225)
# The first cut's fields (a list of rows, the four tracks with RAISE each, the five crystal counts): gone with Rev 105 B.
RETIRED_ART_FIELDS = ("rows", "tracks", "crystalTexts", "raiseTemplate", "topWord")
ROW_VIEW_FIELDS = dict(button=S["button"], group=225, titleText=TMP, lineText=TMP, activeMark=IMG, domainText=TMP)
CHIP_FIELDS = dict(button=S["button"], group=225, label=TMP, selectedMark=IMG)
NANOBOT_FIELDS = dict(hull=IMG, companion=IMG, botTemplate=IMG)
# Rev 31: Take into is Mission and Arena only (Shombith, 2026-10-27: "Yes keep it out"); Rev 110 B (section 206) retired
# the Rev 67 squads row, header, seat cards, picker and bars for the holo table.
RETIRED_SQUAD_FIELDS = ("playArcadeButton", "playArenaButton", "playMissionButton", "detailGroup", "squadRows", "mateCards",
                        "headerGroup", "deckRoot", "pickerGroup", "vesselTiles", "makeBarGroup", "viewBarGroup", "editButton",
                        "createButton", "cardArt")
SQUAD_LISTS = (("plates", SEATS, "plate", PLATE_FIELDS), ("joinRows", JOIN_ROWS, "squad_row", ROW_VIEW_FIELDS),
               ("domainChips", len(DOMAIN_ORDER), "chip", CHIP_FIELDS), ("tiles", len(TILES), "chip", CHIP_FIELDS),
               ("choiceChips", CHOICES, "chip", CHIP_FIELDS), ("pilotChips", PILOT_CHOICES, "chip", CHIP_FIELDS))


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
PILOTS_FIELDS = dict(emptyGroup=225, countText=TMP, restGroup=225, pickUpButton=S["button"], glass=S["glass"],
                     lastCallsignText=TMP, lastNameText=TMP, heldGroup=225, crewGroup=225, putDownButton=S["button"],
                     fileGroup=225, filePicture=IMG, fileKickerText=TMP, fileNameText=TMP, fileSpeciesText=TMP,
                     fileFactionText=TMP, fileSightCaptionText=TMP, fileSightText=TMP, fileStoryCaptionText=TMP,
                     fileStoryText=TMP, fileCountText=TMP, backButton=S["button"], previousButton=S["button"],
                     nextButton=S["button"], profileButton=S["button"])


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
    sights = listed(page, "sightLines")
    assert sights == [str(V(f"Pilots/Cell{i}/Sight#tmp")) for i in range(CALLSIGNS)], \
        "ArkPilotsPage.sightLines is not each crew cell's own Sight line, in cell order"
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
            f"crew cell {i} is not laid out from the glass's top left"
        (x, y), (w, h) = rect_xy(view, rt, "m_AnchoredPosition"), rect_xy(view, rt, "m_SizeDelta")
        assert x >= 0 and x + w <= GLASS_W and -y >= SCREEN_HEAD and -y + h <= FOOT_Y, \
            f"crew cell {i} runs off the PDA's glass (over its header or its foot)"
        assert (x, -y) == pilot_cell_rect(i) and (w, h) == (PILOT_W, PILOT_H), f"crew cell {i} is off the grid"
        name_w = rect_xy(view, V(f"Pilots/Cell{i}/Label#rt"), "m_SizeDelta")[0]
        assert K.label_width(WIDEST_NAME, PILOT_NAME_ROLE) * K.FIT_MIN_SCALE <= name_w <= PILOT_W, \
            f"crew cell {i}'s name row ({name_w:g}) does not hold the widest name a pilot can have"
    assert PILOT_FADE_Y <= PILOT_LINE_Y and PILOT_LINE_Y + PILOT_LINE_H <= PILOT_NAME_Y \
        and PILOT_NAME_Y + PILOT_NAME_H <= PILOT_SIGHT_Y and PILOT_SIGHT_Y + PILOT_SIGHT_H + PILOT_PAD // 2 <= PILOT_H, \
        "a crew cell's callsign, name and sight overlap, leave the fade or run off the card"
    for w in callsign_words():
        assert K.label_width(w, PILOT_LINE_ROLE) <= PILOT_W - 2 * PILOT_PAD, f"the callsign {w} does not fit its card"
    assert K.line_height(PILOT_SIGHT_ROLE) * PILOT_SIGHT_LINES <= PILOT_SIGHT_H, "a crew cell's sight rows are too short"
    # Every callsign's sight, as the cell words it, wraps into the rows it has.
    words_w = PILOT_W - 2 * PILOT_PAD
    for brief in callsign_briefs():
        said = dict(PILOT_LINES)["sightTemplate"].replace("{brief}", brief)
        lines, cur = 1, ""
        for word in said.split():
            nxt = f"{cur} {word}".strip()
            if cur and K.label_width(nxt, PILOT_SIGHT_ROLE) > words_w:
                lines, cur = lines + 1, word
            else:
                cur = nxt
        assert lines <= PILOT_SIGHT_LINES, f"a crew cell's sight needs more than {PILOT_SIGHT_LINES} lines: {said}"
    assert K.line_height(PILOT_NAME_ROLE) <= PILOT_NAME_H and K.line_height(PILOT_LINE_ROLE) <= PILOT_LINE_H + 0.5, \
        "a crew cell's rows are shorter than their text"
    # The held glass is anchored to the held render's glass, so the words stay on it however the picture stretches.
    srt = V("Pilots/Held/Screen#rt")
    assert (rect_xy(view, srt, "m_AnchorMin"), rect_xy(view, srt, "m_AnchorMax")) == \
        (frac(GLASS_X, GLASS_Y + GLASS_H), frac(GLASS_X + GLASS_W, GLASS_Y)) and \
        rect_xy(view, srt, "m_SizeDelta") == (0, 0), "the PDA's screen is not anchored to the held render's glass"
    # Lying down, the picture is drawn through the rest render's glass corners.
    _, g = A.doc_of(view, V("Pilots/Rest/Glass#glass"))
    corners = [tuple(float(v) for v in m) for m in re.findall(r"^  - \{x: ([\d.]+), y: ([\d.]+)\}", g, re.M)]
    assert corners == [(round(x / 1920, 5), round(y / 1080, 5)) for x, y in PDA_REST_CORNERS], \
        "the PDA's glass on the bench is not drawn through the rest render's corners"
    last_x = LAST_X
    assert last_x >= PDA_REST_CORNERS[2][0] + 40, "the last pilot read and PICK UP stand over the PDA lying on the bench"


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
    validate_artifacts(view, V)
    _, page = A.doc_of(view, V("Squads#logic"))
    for field, want in SQUADS_FIELDS.items():
        C.wired(view, page, field, want, "SquadsPage")
    for field in RETIRED_SQUAD_FIELDS:
        assert not re.search(r"^  " + field + ":", page, re.M), f"SquadsPage.{field} is back (retired: Take into is Mission and Arena; the holo table replaced the Rev 67 squad screen)"
    for field, n, kind, fields in SQUAD_LISTS:
        ids = listed(page, field)
        assert len(ids) == n and all(i != "0" and A.kind_of(view, i)[1] == S[kind] for i in ids), f"SquadsPage.{field} is not {n} wired"
        for i in ids:
            _, body = A.doc_of(view, i)
            for f, want in fields.items():
                C.wired(view, body, f, want, field)
    for field, n in (("domainLit", len(DOMAIN_LIT)), ("tileChevrons", len(TILES))):
        ids = listed(page, field)
        assert len(ids) == n and all(i != "0" and A.kind_of(view, i)[1] == IMG for i in ids), f"SquadsPage.{field} is not {n} wired Images"
    # The holo table (R110B): the plates sit under their pads in the board's order (Rogue One left, the leader in the
    # middle, Rogue Two right), on the dome and clear of the rail; the projection stands over them.
    plates = listed(page, "plates")
    xs = []
    for seat, fid in enumerate(plates):
        _, plate = A.doc_of(view, fid)
        go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", plate, re.M).group(1)
        rt = V(f"Squads/Plate{seat}#rt")
        assert A.doc_of(view, rt)[1] and f"m_GameObject: {{fileID: {go}}}" in A.doc_of(view, rt)[1], f"SquadsPage.plates[{seat}] is not Plate{seat}"
        x, y = rect_xy(view, rt, "m_AnchoredPosition")
        xs.append(x)
        assert y - PLATE_H >= RAIL_H, f"seat {seat}'s plate runs into the rail"
    assert xs[1] < xs[0] < xs[2], "the plates are not Rogue One left, the leader in the middle, Rogue Two right (their pads)"
    assert PROJ_BOTTOM > PLATE_TOP + PLATE_RISE, "the projection stands on the picked plate"
    assert re.search(r"^  domainOrder:\n  - 1\n  - 2\n  - 4\n", page, re.M), "SquadsPage.domainOrder is not Jade, Ruby, Gold"
    for guid, what in ((VESSEL_LIST, "the vessel list"), (PILOT_CATALOG, "the pilot catalog"),
                       (COLOR_SET, "the domain colours")):
        assert f"guid: {guid}" in page, f"SquadsPage reads no {what}"


def fill_of(view, fid):
    """(type, method, origin) of an Image."""
    _, body = A.doc_of(view, fid)
    return tuple(int(re.search(r"^  " + f + r": (\d+)$", body, re.M).group(1)) for f in ("m_Type", "m_FillMethod", "m_FillOrigin"))


def validate_artifacts(view, V):
    """The ARTIFACTS tab as Rev 105 B draws it: the rail's medallions, the lens with its four arcs, the words on the left,
    the feed, every field wired; every kind's mark and payload; every word authored and naming only tokens the page fills;
    and no card or panel behind the words (the board's whole point)."""
    _, art = A.doc_of(view, V("Artifacts#logic"))
    for field, want in ART_FIELDS.items():
        C.wired(view, art, field, want, "ArkArtifactsPage")
    for field in RETIRED_ART_FIELDS:
        assert not re.search(r"^  " + field + ":", art, re.M), f"ArkArtifactsPage.{field} is back (Rev 105 B retired it)"
    assert f"guid: {PILOT_CATALOG}" in art, "ArkArtifactsPage reads no pilot catalog"
    medals = listed(art, "medallions")
    assert len(medals) == MEDALLIONS and all(i != "0" and A.kind_of(view, i)[1] == S["medal"] for i in medals), \
        f"ArkArtifactsPage.medallions is not {MEDALLIONS} wired ArtifactMedallions"
    for i in medals:
        for field, want in MEDAL_FIELDS.items():
            C.wired(view, A.doc_of(view, i)[1], field, want, "a medallion")
    icons = listed(art, "crystalIcons")
    assert len(icons) == CRYSTAL_ICONS and all(i != "0" and A.kind_of(view, i)[0] == 114 and A.kind_of(view, i)[1] == IMG
                                               for i in icons), f"ArkArtifactsPage.crystalIcons is not {CRYSTAL_ICONS} Images"
    m = re.search(r"^  crystalSprites:\n((?:  - .*\n)*)", art, re.M)
    assert m and m.group(1) == "".join(f"  - {K.sprite_ref(g)}\n" for g in CRYSTAL_BY_VALUE), \
        "ArkArtifactsPage.crystalSprites is not None, Charge, Mass, Space, Time, Omni"
    kinds = re.findall(r"^  - kindId: artifact\.(\w+)\n    glyph: \{fileID: 21300000, guid: ([0-9a-f]{32}), type: 3\}\n"
                       r"    payload: (\d)\n    countTrack: (\d)\n    countOffset: (\d)\n", art, re.M)
    assert [(k, int(p), int(o)) for k, _, p, _, o in kinds] == list(ART_KINDS), \
        "ArkArtifactsPage.kinds is not Javelin (Line), Hydra (Fan), Aftershock (Repeat), Aegis (Shell, re-raises + 1)"
    for kind, guid, _, track, _ in kinds:
        assert guid == GLYPHS.glyph_guid(kind), f"the {kind} medallion shows another kind's mark"
        assert os.path.exists(os.path.join(REPO, GLYPHS.OUT, f"artifact_{kind}.png")), \
            f"the {kind} mark is not on disk (run author_artifact_glyphs.py)"
        assert int(track) == ELEMENT_VALUE[COUNT_TRACK], f"the {kind} payload is not counted on {COUNT_TRACK}"
        cat = os.path.join(REPO, f"Assets/_SO_Assets/Pilots/Artifacts/Artifact_{kind.capitalize()}.asset")
        assert re.search(r"^  id: artifact\." + kind + "$", open(cat, encoding="utf-8").read(), re.M), \
            f"no artifact kind artifact.{kind} in the catalog"
    lens_ = re.search(r"^  lens: \{fileID: (-?\d+)\}", art, re.M).group(1)
    _, ld = A.doc_of(view, lens_)
    for field, want in LENS_FIELDS.items():
        C.wired(view, ld, field, want, "ArtifactLens")
    field_go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", A.doc_of(view, V("Artifacts/Lens/Body/Field#rt"))[1],
                         re.M).group(1)
    assert any(A.kind_of(view, c)[1] == A.MASK for c in re.findall(r"^  - component: \{fileID: (-?\d+)\}",
                                                                    A.doc_of(view, field_go)[1], re.M)), \
        "the lens's field is not masked to its disc: the prisms would spill past the rim"
    arcs = re.findall(r"^  - attunedFill: \{fileID: (-?\d+)\}\n    fixedFill: \{fileID: (-?\d+)\}\n"
                      r"    flower: \{fileID: (-?\d+)\}\n    valueText: \{fileID: (-?\d+)\}\n"
                      r"    labelText: \{fileID: (-?\d+)\}\n    wordsGroup: \{fileID: (-?\d+)\}\n", ld, re.M)
    assert len(arcs) == len(TRACK_ELEMENTS), f"the lens has {len(arcs)} arcs, not {len(TRACK_ELEMENTS)}"
    for name, ids in zip(TRACK_ELEMENTS, arcs):
        for (field, want), fid in zip(ARC_FIELDS.items(), ids):
            assert fid != "0", f"the lens's {name} arc's {field} is not wired"
            cls, scr = A.kind_of(view, fid)
            assert (cls == want if isinstance(want, int) else scr == want), f"the lens's {name} arc's {field} is the wrong kind"
        for fid in ids[:2]:
            assert fill_of(view, fid) == (3, 4, 1), f"the lens's {name} arc is not a radial fill from the right"
        _, fl = A.doc_of(view, ids[2])
        assert f"  element: {dict(H.ELEMENTS)[name]}\n" in fl, f"the lens's {name} arc wears another element's flower"
    for key in ("Artifacts/Detail/Feed/Band/Now", "Artifacts/Detail/Feed/Band/Next"):
        assert fill_of(view, V(f"{key}#img"))[:2] == (3, 0), f"{key} is not a horizontal fill"
    # Every word: authored, and naming only the tokens the page fills there.
    written = dict(re.findall(r"^  (\w+(?:Template|Word)|trail(?:Hoard|Artifact|Feed)): (.*)$", art, re.M))
    assert set(written) == {f for f, _ in artifact_words()}, "ArkArtifactsPage's words in the prefab are not the authored set"
    for field, value in written.items():
        assert value.strip(), f"ArkArtifactsPage.{field} is blank: the line would show nothing"
        for token in re.findall(r"\{(\w+)\}", value):
            assert token in ART_TOKENS.get(field, ()), f"ArkArtifactsPage.{field} names {{{token}}}, which the page does not fill there"
    # No card or panel behind the words (the cards Shombith turned down): nothing on the page wears a panel's sliver.
    panel = {K.kit_sprite(f"kit_sliver14_{p}") for p in ("fill", "line1", "line2")}
    for name, cdoc in page_images(view, V("Artifacts#rt")):
        assert not any(f"guid: {g}" in cdoc for g in panel), f"the Artifacts tab draws a card again ({name})"


def page_images(view, rt):
    """(GameObject name, Image document) for every Image under the RectTransform `rt`, depth first."""
    _, body = A.doc_of(view, rt)
    go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", body, re.M).group(1)
    _, gdoc = A.doc_of(view, go)
    name = re.search(r"^  m_Name: (.*)$", gdoc, re.M).group(1)
    for comp in re.findall(r"^  - component: \{fileID: (-?\d+)\}", gdoc, re.M):
        _, cdoc = A.doc_of(view, comp)
        if cdoc and f"guid: {IMG}" in cdoc:
            yield name, cdoc
    kids = re.search(r"^  m_Children:\n((?:  - .*\n)*)", body, re.M)
    for kid in re.findall(r"fileID: (-?\d+)", kids.group(1) if kids else ""):
        yield from page_images(view, kid)

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
        ("a seat plate with no face", lambda f: f.__setitem__(view, re.sub(
            r"(  avatar: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1)), "avatar is not wired"),
        ("Take into Arcade back", lambda f: f.__setitem__(view, f[view].replace(
            "  missionButton: ", "  playArcadeButton: {fileID: 0}\n  missionButton: ", 1))),
        ("the projection with no choices", lambda f: f.__setitem__(view, re.sub(
            r"(  choiceChips:\n  - )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1)), "choiceChips is not"),
        ("the leader's plate moved to the left", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Squads/Plate0#rt"), lambda d: re.sub(r"(  m_AnchoredPosition: \{x: )0(,)", r"\g<1>-640\2", d))),
         "the plates are not"),
        ("the Rev 67 seat cards back", lambda f: f.__setitem__(view, f[view].replace(
            "  domainLit:", "  mateCards: []\n  domainLit:", 1)), "is back"),
        ("FEED unwired", lambda f: f.__setitem__(view, re.sub(
            r"(  feedButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1)), "feedButton is not wired"),
        ("the lens's attuned fill unwired", lambda f: f.__setitem__(view, re.sub(
            r"(  - attunedFill: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1)), "attunedFill is not wired"),
        ("a lens arc that is a plain image", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Artifacts/Lens/Body/ArcTime/Fixed#img"), lambda d: d.replace("  m_Type: 3\n", "  m_Type: 0\n"))),
         "not a radial fill"),
        ("the lens's field unmasked", lambda f: f.__setitem__(view, f[view].replace(f"guid: {A.MASK}", f"guid: {A.KIT_RECT_MASK_2D}")),
         "not masked"),
        ("a glass card behind the artifact's words", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Artifacts/Rail/Line#img"), lambda d: re.sub(r"(  m_Sprite: \{fileID: 21300000, guid: )[0-9a-f]{32}",
                                                                    r"\g<1>" + K.kit_sprite("kit_sliver14_fill"), d))),
         "draws a card again"),
        ("a medallion with no glyph", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Artifacts/Rail/Medallion2#medal"), lambda d: re.sub(r"(  glyph: )\{fileID: -?\d+\}", r"\1{fileID: 0}", d))),
         "glyph is not wired"),
        ("the Hydra medallion wearing the Javelin's mark", lambda f: f.__setitem__(view, f[view].replace(
            GLYPHS.glyph_guid("hydra"), GLYPHS.glyph_guid("javelin"), 1)), "another kind's mark"),
        ("an artifact line naming a token the page does not fill", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Artifacts#logic"), lambda d: d.replace(yfield("omniTemplate", "Feed {count} Omni"),
                                                                yfield("omniTemplate", "Feed {count} {element}")))),
         "which the page does not fill there"),
        ("an artifact line blanked", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Artifacts#logic"), lambda d: d.replace(yfield("goneWord", "That artifact is no longer in your hoard."),
                                                                "  goneWord: \n"))), "is blank"),
        ("the first cut's track rows back", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Artifacts#logic"), lambda d: d.replace("  medallions:\n", "  rows: []\n  medallions:\n"))),
         "Rev 105 B retired it"),
        ("the Artifacts tab with no page logic", lambda f: f.__setitem__(view, unlogic_tab(f[view], TABS.index("Artifacts")))),
        ("the Ark's arkTabs unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  arkTabs: {{fileID: {mount.stub_fid('root#tabs')}}}", "  arkTabs: {fileID: 0}"))),
        ("the Pilots tab with no page logic", lambda f: f.__setitem__(view, unlogic_tab(f[view], TABS.index("Pilots")))),
        ("the Pilots tab reading no scene catalog", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(f"guid: {SCENE_CATALOG}", f"guid: {PILOT_CATALOG}")))),
        ("a callsign with no crew cell", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(f"  - {{fileID: {V('Pilots/Cell17#chip')}}}\n", "")))),
        ("a crew cell with no sight line", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(f"  - {{fileID: {V('Pilots/Cell5/Sight#tmp')}}}\n", "")))),
        ("a crew cell with no close-up", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Cell3#chip"), lambda d: re.sub(r"(  image: )\{fileID: -?\d+\}", r"\1{fileID: 0}", d)))),
        ("a crew cell off the PDA's glass", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Cell17#rt"), lambda d: re.sub(
                r"  m_AnchoredPosition: \{x: [\d.]+, y: ", f"  m_AnchoredPosition: {{x: {GLASS_W - 40}, y: ", d))),
         "runs off the PDA's glass"),
        ("a crew cell too narrow for the widest name", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Cell7/Label#rt"), lambda d: d.replace(
                f"  m_SizeDelta: {{x: {PILOT_W - 2 * PILOT_NAME_PAD}, y: ", "  m_SizeDelta: {x: 100, y: "))),
         "does not hold the widest name"),
        ("the veil back over the Pilots bench", lambda f: f.__setitem__(view, view_text(VEILED | {"Pilots"})),
         "hides its room"),
        ("the PDA's screen off its glass", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Held/Screen#rt"), lambda d: re.sub(r"  m_AnchorMin: \{x: [\d.]+", "  m_AnchorMin: {x: 0", d))),
         "not anchored to the held render's glass"),
        ("the glass drawn off the bench's corners", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots/Rest/Glass#glass"), lambda d: d.replace(f"{{x: {round(PDA_REST_CORNERS[0][0] / 1920, 5)},", "{x: 0.1,"))),
         "not drawn through the rest render's corners"),
        ("PICK UP unwired", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: re.sub(r"(  pickUpButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", d)))),
        ("a file template naming a token the page does not fill", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(yfield("fileStoryTemplate", "{backstory}"),
                                                             yfield("fileStoryTemplate", "{relic}")))),
         "which the page does not fill there"),
        ("a crew line naming a token the page does not fill", lambda f: f.__setitem__(view, in_doc(
            f[view], V("Pilots#logic"), lambda d: d.replace(yfield("lineTemplate", "{callsign}"),
                                                             yfield("lineTemplate", "{homeworld}")))),
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
