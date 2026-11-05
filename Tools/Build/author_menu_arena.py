#!/usr/bin/env python3
"""
Author ITEM 8a of the style-B menu redesign - the ARENA window - as prefabs, and wire it into
Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 17; the approved ArenaWindow board on the
"Menu Redesign B" canvas). It is the Arcade window's shape (item 3, author_menu_arcade.py) with the
arena's own cards:

    Assets/_Prefabs/UI Elements/Main Menu Screens/Arena/
        ArenaGameCard.prefab   one arena card: KitArenaGameCard (KitGameCard) on a KitTile - the art, the
                               favourite star, "Arena - 2 - 6 pilots", the title, the tagline, "2 - 3
                               domains" and one chip per hull the card seats
        ArenaView.prefab       the window body: nav dim, backdrop, header with Back (B), the shared
                               PartyPanel (item 3's prefab, instanced - never copied), and the explore
                               scroll with ALL GAMES and a two-column grid of ArenaGameCards

    Menu_Main.unity, `ModalWindows/ArenaScreenModal`:
        The old Header, GameSelectScrollView, ArcadeLobbyList, CloseButton and FriendListPanel are
        deleted; one ArenaView instance goes under `Arena_Panel/Explore`, whose ArcadeExploreView is KEPT
        (the switcher and the arena flow name it) and re-pointed into the instance; the roster stays
        ArenaGames. The window stretches full screen, the panel's gradient is switched off, and the two
        OfflineUIGates gate the new party panel.

STANDING DOWN: the migration runs only while the old objects exist; the documents this script owns are
re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_arena.py              # write
    python3 Tools/Build/author_menu_arena.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_arena.py --self-test  # planted defects must all be caught
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
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Arena"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
ref, mono, refs, calls = A.ref, A.mono, A.refs, A.calls
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node, ring_node = A.image_node, A.art_node, A.ring_node
c = A.c

PREFABS = ("ArenaGameCard", "ArenaView")
S = dict(card=C.script("KitArenaGameCard"), view=A.S["view"], gate=A.S["gate"], tile=A.S["tile"],
         kt=A.S["kt"], button=A.S["button"], hover=A.S["hover"], cover=A.S["cover"], close=A.S["close"])
ARENA_GAMES = "45f4a55e49b44cc7ac47e0bf96411234"
# Rev 3: the friends panel gave its width back, so the four arena games stand in ONE row of tall
# cards (the R3Arcade board's quest-view rhythm) instead of a 2 x 2 of wide ones. The cards flex
# (flexibleWidth 1); these are their sizes at the 1920 reference width.
COLS, ROWS, GAP = 4, 1, 20
CARD_W, CARD_H = (A.CONTENT_W - (COLS - 1) * GAP) // COLS, 560
GRID_W = COLS * CARD_W + (COLS - 1) * GAP
TEXT_W = -52                    # stretched text: 26 px either side
ART_BAND_H = CARD_H // 2        # Rev 18 A: the art on top, the body under it
HULL_PLATE = 52
PLACEHOLDER_ART = A.sprite_guid("Assets/_SO_Assets/Games/ArcadeGameSkimRace.asset", "CardBackground")


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def instance(key, prefab_key, name, r, guid=None):
    return K.Instance(key, prefab_key, guid or asset_guid(prefab_key), name, r, ())


# --------------------------------------------------------------------------------------------
# ArenaGameCard.prefab
# --------------------------------------------------------------------------------------------

def build_card():
    root = N("root", "ArenaGameCard", R(size=(CARD_W, CARD_H)))
    ring_node(root, "FocusRing", 14)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    # Rev 18 A (Docs/MENU_REDESIGN_B.md section 72): the art drifts in a band on top; the words sit on the opaque body.
    clip = root.child(N("Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
    clip.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("mask", 114, A.mask(False))
    A.art_band(clip, "Clip/Band", CARD_W, ART_BAND_H, PLACEHOLDER_ART)
    A.body_sea(clip, "Clip/Sea", CARD_H - ART_BAND_H)
    image_node(root, "Seam", "Seam", R((0, 0), (1, 0), (0, CARD_H - ART_BAND_H), (-2, 1), (0.5, 0.5)), "SurfaceDark")
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))

    star = root.child(N("Star", "FavoriteStar", R((1, 1), None, (-8, -8), (44, 44), (1, 1))))
    # On a phone the card is drawn at PH_SCALE: the star's hit area grows so it is still a thumb's width there.
    star.add("phone", 114, K.phone_rect(R((1, 1), None, (-4, -4), (PH_STAR, PH_STAR), (1, 1))))
    star.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    image_node(star, "Star/Glyph", "Glyph", R(size=(24, 24)), "NeutralLightest", K.chrome_sprite("chrome_star"), preserve=True)
    star.add("audio", 114, A.menu_audio(5)) \
        .add("button", 114, lambda ctx, go: mono(go, A.BUTTON,
             A.selectable(nav_mode=0) + f"  m_TargetGraphic: {ref(ctx.c('Star', 'img'))}\n"
             + calls([(ctx.c("root", "card"), "CosmicShore.UI.KitArenaGameCard", "ToggleFavorite"),
                      (ctx.c("Star", "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")]))) \
        .add("hover", 114, lambda ctx, go: mono(go, S["hover"],
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n  hoverToken: {K.TOKEN_INDEX['TextLight']}\n"
             + refs("texts", []) + refs("graphics", [ctx.c("Star/Glyph", "th")])))

    lock = root.child(N("Lock", "Lock", R(pos=(0, CARD_H / 2 - ART_BAND_H / 2 + 14), size=(34, 34))))
    lock.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight", 0.0), K.chrome_sprite("chrome_lock"),
                                                                   preserve=True))
    lab = root.child(N("LockLabel", "LockLabel", R(pos=(0, CARD_H / 2 - ART_BAND_H / 2 - 18), size=(200, 18))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("LOCKED", "Label", tok("TextLight", 0.0), align="center")) \
        .add("kt", 114, K.kit_text("Label", "TextLight", driven=True))

    picks = root.child(N("Picks", "AvatarSpace", R((0, 1), None, (16, -16), (240, 24), (0, 1))))
    picks.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 4, 3, (0, 0), (0, 0)))
    pick = picks.child(N("Picks/Pick", "AvatarIcon", R(size=(24, 24))))
    pick.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), A.AVATAR_PLACEHOLDER, preserve=True)) \
        .add("cg", 225, canvas_group(False)).add("le", 114, le(pw=24, ph=24))

    # The body reads down from the seam: pilots and domains on one line, the name, the description; the hulls the
    # card seats sit lit along the foot (Hull_Lit.mat, section 73).
    body_top = CARD_H - ART_BAND_H
    label(root.child(N("Eyebrow", "Eyebrow", R((0, 0), None, (26, body_top - 30), (240, 18), (0, 0.5)))), "Arena - 2 - 6 pilots",
          "Label", "NeutralLightest")
    label(root.child(N("Domains", "Domains", R((1, 0), None, (-26, body_top - 30), (120, 18), (1, 0.5)))), "2 domains", "Label",
          "NeutralLightest", align="right")
    title = root.child(N("Title", "Title", R((0, 0), (1, 0), (0, body_top - 70), (TEXT_W, 46), (0.5, 0.5))))
    title.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Game", "H1", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    tag = root.child(N("Tagline", "Tagline", R((0, 0), (1, 0), (0, body_top - 132), (TEXT_W, 66), (0.5, 0.5))))
    tag.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("", "BodySmall", tok("TextLight"), valign="top", wrap=True)) \
        .add("kt", 114, K.kit_text("BodySmall", "TextLight"))
    hulls = root.child(N("Hulls", "Hulls", R((0, 0), (1, 0), (0, 22 + HULL_PLATE / 2), (TEXT_W, HULL_PLATE), (0.5, 0.5))))
    hulls.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 3, (0, 0), (0, 0)))
    chip = A.hull_icon(hulls, "Hulls/Chip", R(size=(HULL_PLATE, HULL_PLATE)), "Hulls/Chip/Icon", HULL_PLATE)
    chip.add("cg", 225, canvas_group(False)).add("le", 114, le(ignore=True, pw=HULL_PLATE, ph=HULL_PLATE))

    root.add("tile", 114, A.tile_body(c("FocusRing", "img"), c("Fill", "img"), c("Border", "img"), 14,
                                      primary=[c("Title", "tmp")], art=[c("Clip/Band/Art", "img")],
                                      lock=c("Lock", "img"), lock_label=c("LockLabel", "tmp"), sel_border="Selection",
                                      on_click=[lambda ctx: (ctx.c("root", "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")]))
    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  AllGames: {K.ref(11400000, A.ALL_GAMES, 2)}\n"
             f"  StarIconActive: {K.sprite_ref(K.chrome_sprite('chrome_star_fill'))}\n"
             f"  StarIconInActive: {K.sprite_ref(K.chrome_sprite('chrome_star'))}\n"
             "  ExploreView: {fileID: 0}\n"
             f"  GameTitle: {ref(ctx.c('Title', 'tmp'))}\n"
             f"  BackgroundImage: {ref(ctx.c('Clip/Band/Art', 'img'))}\n"
             f"  StarImage: {ref(ctx.c('Star/Glyph', 'img'))}\n"
             "  Index: 0\n  VesselIcon: {fileID: 0}\n"
             f"  VesselList: {K.ref(11400000, A.VESSEL_LIST, 2)}\n"
             "  GenrePetal: {fileID: 0}\n  GenrePetalSecondary: {fileID: 0}\n"
             f"  AvatarSpace: {ref(ctx.rt('Picks'))}\n"
             f"  ProfileIcons: {K.ref(11400000, A.PROFILE_ICONS, 2)}\n"
             "  LocalPickBorder: {fileID: 0}\n"
             "  localPickBorderColor: {r: 0.45, g: 1, b: 0.85, a: 1}\n"
             "  lockOverlay: {fileID: 0}\n"
             "  lockedTintColor: {r: 0.3, g: 0.3, b: 0.3, a: 1}\n"
             f"  tile: {ref(ctx.c('root', 'tile'))}\n"
             f"  starPaint: {ref(ctx.c('Star/Glyph', 'th'))}\n"
             f"  eyebrowText: {ref(ctx.c('Eyebrow', 'kt'))}\n"
             f"  taglineText: {ref(ctx.c('Tagline', 'tmp'))}\n"
             f"  domainsText: {ref(ctx.c('Domains', 'kt'))}\n"
             f"  hullRow: {ref(ctx.rt('Hulls'))}\n"))
    root.add("audio", 114, A.menu_audio(2)).add("scroll", 114, A.scroll_into()) \
        .add("le", 114, le(mw=0, fw=1, ph=CARD_H))
    return root


# --------------------------------------------------------------------------------------------
# ArenaView.prefab
# --------------------------------------------------------------------------------------------

def card_key(r, col):
    return f"Card{r + 1}_{col + 1}"


def build_view():
    root = N("root", "ArenaView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=A.WINDOW_BACKDROP_ALPHA)
    # §67: the sea lines over the glass (Sea_Window.mat), slow wave lines in three depth layers.
    root.child(M.window_sea_node("BackdropLattice", rect=R((0, 0), (1, 1), (0, -42), (0, -84))))

    # Sibling order is FOCUS order (ScreenSwitcher.Refocus): the cards first, the party panel (laid
    # out on the left by the reversed row) next, the header's Back last - as the arcade window.
    body = root.child(N("Body", "Body", A.WINDOW_BODY))
    body.add("hlg", 114, lambda ctx, go: K.hv_layout(False, (0, 0, 0, 0), 24, 0, (1, 1), (0, 1))(ctx, go)
             .replace("  m_ReverseArrangement: 0\n", "  m_ReverseArrangement: 1\n"))
    # Motion (section 22.8): the window's two blocks rise in on every open - the modal never
    # re-enables, so ModalWindowManager replays them (KitEntrance.ReplayUnder).
    body.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", stagger=1))
    ex = body.child(N("Explore", "ExploreScroll", R((0, 1), None, (0, 0), (A.EXPLORE_W, 844), (0, 1))))
    ex.add("le", 114, le(mw=0, fw=1)).add("scroll", 114, A.scroll_rect("Explore/Viewport/Content", "Explore/Viewport"))
    vp = ex.child(N("Explore/Viewport", "Viewport", R((0, 0), (1, 1), (-5, 0), (-10, 0))))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, A.rect_mask())
    content = vp.child(N("Explore/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (6, 6, 6, 6), 0, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2)) \
        .add("phone", 114, K.phone_rect(width_scale=PH_SCALE))
    allg = content.child(N("AllGames", "AllGames", R((0, 1), None, (0, 0), (GRID_W, 18), (0, 1))))
    allg.add("le", 114, le(ph=18))
    label(allg.child(N("AllGames/Label", "Label", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "ALL GAMES", "Label", "TextLight")
    label(allg.child(N("AllGames/Count", "Count", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "4", "Label", "NeutralLightest",
          align="right")
    content.child(N("Gap", "Gap", R((0, 1), None, (0, 0), (GRID_W, 10), (0, 1)))).add("le", 114, le(ph=10))
    # ONE wrapping grid, the cards its own children (ArcadeExploreView reads either shape, as Arcade's since section 129):
    # COLS across on PC at the width the old flexing row gave each card, PH_COLS on a phone, where the scroll's content is
    # drawn at PH_SCALE.
    grid = content.child(N("Grid", "GameGrid", R((0, 1), None, (0, 0), (GRID_W, ROWS * (CARD_H + GAP) - GAP), (0, 1))))
    grid.add("grid", 114, lambda ctx, go: mono(go, GRID_LAYOUT,
             "  m_Padding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 0\n    m_Bottom: 0\n"
             "  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n"
             f"  m_CellSize: {{x: {K.f32(GRID_CELL_W)}, y: {CARD_H}}}\n  m_Spacing: {{x: {GAP}, y: {GAP}}}\n"
             f"  m_Constraint: 1\n  m_ConstraintCount: {COLS}\n"))
    grid.add("phone", 114, K.phone_rect(grid=dict(columns=PH_COLS, cell=(PH_CELL_W, CARD_H))))
    for r in range(ROWS):
        for col in range(COLS):
            grid.children.append(instance(card_key(r, col), "ArenaGameCard", "GameCard",
                                          R((0, 1), None, (col * (GRID_CELL_W + GAP), -r * (CARD_H + GAP)), (GRID_CELL_W, CARD_H), (0, 1))))
    # Rev 3: the party panel moved to the one friends drawer on Menu_Main; the explore scroll fills the body.

    header = root.child(N("Header", "Header", A.WINDOW_HEADER))
    A.drawer_dock(root)
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(header.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (300, 18), (0, 1)))), "ARENA", "Label",
          "NeutralLightest")
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))), "Explore arena", "H1",
          "TextLight")
    A.back_button(header)
    phone_window(root)
    return root


# --------------------------------------------------------------------------------------------
# The phone layout (Docs/UI_STYLE_GUIDE.md section 8; Docs/MENU_REDESIGN_B.md section 129). The hub windows beside Arcade
# take Arcade's phone window exactly (author_menu_arcade.py, R55Arcade) so the four read as one family: the nav dim over
# the 96-wide rail, the glass right of it, the header 52 tall from y 10 with its buttons 48 tall, the body from y 72 to
# 10 above the foot; the explore scroll's content drawn at PH_SCALE and re-flowed, its card grid PH_COLS across.
# --------------------------------------------------------------------------------------------

PHONE_RAIL, PHONE_X = A.PHONE_RAIL, A.PHONE_X
PH_HEADER = R((0, 1), (1, 1), ((PHONE_X - 16) / 2, -10), (-(PHONE_X + 16), 52), (0.5, 1))
PH_BODY = R((0, 0), (1, 1), ((PHONE_X - 16) / 2, -31), (-(PHONE_X + 16), -82))
PH_W, PH_BODY_H = 800 - PHONE_X - 16, 450 - 72 - 10      # 672 x 368 on the narrowest (800-wide) phone canvas
PH_TOUCH = 44                                             # the smallest control on a phone, in phone units
# Arena: four tall cards. 0.6 keeps a whole card (560 tall) in the body under ALL GAMES, and three across leaves each
# card wide enough for its title ("Astro League") at the PC H1 size.
PH_SCALE, PH_COLS = 0.6, 3
GRID_LAYOUT = "8a8695521f0d02e499659fee002a26c2"         # UnityEngine.UI.GridLayoutGroup
GRID_CELL_W = (A.CONTENT_W - (COLS - 1) * GAP) / COLS     # what the old flexing row gave each card (419.5)


def phone_content_w(scale):
    """The explore content's inner width on an 800-wide phone, in its own (PC) units: the viewport (the body less its
    10-unit inset) drawn at `scale`, less the content's 6 + 6 padding."""
    return (PH_W - 10) / scale - 12


def phone_cell_w(scale, cols, gap):
    return int((phone_content_w(scale) - (cols - 1) * gap) // cols)


PH_CELL_W = phone_cell_w(PH_SCALE, PH_COLS, GAP)
PH_STAR = 74                                              # the favourite star's hit area on a phone: 74 x PH_SCALE >= 44


def phone_window(root):
    """Arcade's phone window on a hub window's root (NavDim, Backdrop, BackdropLattice, Header, Body), its header's
    eyebrow, title and Back."""
    for child in root.children:
        if child.key == "NavDim":
            child.add("phone", 114, K.phone_rect(R((0, 0), (0, 1), (PHONE_RAIL / 2, 0), (PHONE_RAIL, 0))))
        elif child.key in ("Backdrop", "BackdropLattice"):
            child.add("phone", 114, K.phone_rect(R((0, 0), (1, 1), (PHONE_RAIL / 2, 0), (-PHONE_RAIL, 0))))
        elif child.key == "Header":
            child.add("phone", 114, K.phone_rect(PH_HEADER))
            for h in child.children:
                if h.key == "Header/Eyebrow":
                    h.add("phone", 114, K.phone_rect(R((0, 1), None, (0, 0), (300, 14), (0, 1))))
                elif h.key == "Header/Title":
                    h.add("phone", 114, K.phone_rect(R((0, 1), None, (0, -16), (420, 32), (0, 1))))
                elif h.key == "Back":
                    h.add("phone", 114, K.phone_rect(R((1, 0), None, (0, 0), (A.back_width(), PH_TOUCH + 4), (1, 0))))
        elif child.key == "Body":
            child.add("phone", 114, K.phone_rect(PH_BODY))


PHONE_GUID = K.meta_guid("Assets/_Scripts/UI/PhoneRect.cs")


def phone_of(text_, comp_fid):
    """A PhoneRect's fields, by its component fileID (None when it is not one)."""
    _, body = A.doc_of(text_, comp_fid)
    if not body or f"guid: {PHONE_GUID}" not in body:
        return None
    out = {}
    for m in re.finditer(r"^  (\w+): (.*)$", body, re.M):
        v = m.group(2)
        vec = re.match(r"\{x: (-?[\d.e+-]+), y: (-?[\d.e+-]+)", v)
        out[m.group(1)] = (float(vec.group(1)), float(vec.group(2))) if vec else v
    return out


def check_phone_window(text_, name):
    """The window's phone header, body, rail dim and Back are Arcade's (a later edit that drops one is caught here)."""
    V = K.FileIds(name)
    for key, size in (("Header", (-(PHONE_X + 16), 52)), ("Body", (-(PHONE_X + 16), -82)), ("NavDim", (PHONE_RAIL, 0)),
                      ("Back", (A.back_width(), PH_TOUCH + 4))):
        ph = phone_of(text_, V(f"{key}#phone"))
        assert ph, f"{name}: {key} has no phone pose"
        assert ph["movesRect"] == "1" and ph["sizeDelta"] == tuple(float(K.f32(v)) for v in size), \
            f"{name}: {key}'s phone pose is not Arcade's ({ph['sizeDelta']})"
    assert phone_of(text_, V("Back#phone"))["sizeDelta"][1] >= PH_TOUCH, f"{name}: Back is under {PH_TOUCH} tall on a phone"


def prefab_outputs():
    out = {}
    for name, build in (("ArenaGameCard", build_card), ("ArenaView", build_view)):
        out[prefab_path(name)] = P.prefab_text(build(), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Arena"))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

MODAL = dict(go="2072797374", rt="2072797375", gate="2072797376")
PANEL = dict(go="1513083448", rt="1513083449", img="1513083451", gate="1513083450")
EXPLORE = dict(go="1145696103", rt="1145696104", view="1145696106")
RETIRED_ROOTS = {"1199705106": "Header", "1256025064": "GameSelectScrollView", "1470385305": "ArcadeLobbyList",
                 "1538953838": "CloseButton", "122070150": "FriendListPanel"}
VIEW_ORDER = ["rosterOverride", "GameSelectionView", "GameSelectionGrid", "WeeklyChallengeCard", "maelstromCard",
              "gameCountText", "ArcadeGameConfigureModal", "selectedVesselClassType", "cardRevealSettings"]


class Ids:
    def __init__(self):
        self.ctx = P.Ctx("Menu_Main/Arena")
        self.inst = str(self.ctx.fid("ArenaView#inst"))
        self.stub = str(self.ctx.fid("ArenaView#stub"))
        V = K.FileIds("ArenaView")
        self.refs = {
            "GameSelectionView": (V("Explore#go"), 1, None),
            "GameSelectionGrid": (V("Grid#rt"), 224, None),
            "gameCountText": (V("AllGames/Count#kt"), 114, S["kt"]),
        }
        self.stubs = {k: str(C.derived(v[0], self.inst)) for k, v in self.refs.items()}

    def all(self):
        return {self.inst, self.stub} | set(self.stubs.values())

    def stub_doc(self, field):
        src, cls, scr = self.refs[field]
        fid, guid = self.stubs[field], asset_guid("ArenaView")
        if cls == 1:
            return T.stub_go(fid, src, guid, self.inst)
        if cls == 224:
            return (f"--- !u!224 &{fid} stripped\nRectTransform:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
                    f"  m_PrefabInstance: {{fileID: {self.inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
        return T.stub_mono(fid, src, guid, self.inst, scr)


def view_doc(old, ids):
    head = old[:old.index("  rosterOverride:")]
    fields, _ = H.doc_fields(old[len(head):])
    values = dict(fields)
    assert set(values) <= set(VIEW_ORDER), f"ArcadeExploreView carries fields this script does not know: {set(values) - set(VIEW_ORDER)}"
    values.update({k: f" {ref(ids.stubs[k])}" for k in ("GameSelectionView", "GameSelectionGrid", "gameCountText")})
    values["rosterOverride"] = f" {K.ref(11400000, ARENA_GAMES, 2)}"
    values["WeeklyChallengeCard"] = " {fileID: 0}"
    values["maelstromCard"] = " {fileID: 0}"
    values.setdefault("cardRevealSettings", " {fileID: 0}")
    return head + "".join(f"  {k}:{values[k]}\n" for k in VIEW_ORDER)


def transform(text_):
    sc = T.Scene(text_)
    ids = Ids()
    assert sc.field(MODAL["go"], "m_Name") == "ArenaScreenModal", "ArenaScreenModal moved"
    assert sc.field(PANEL["go"], "m_Name") == "Arena_Panel", "Arena_Panel moved"
    assert sc.field(EXPLORE["go"], "m_Name") == "Explore", "the arena Explore moved"
    assert sc.script(EXPLORE["view"]) == S["view"], "the arena ArcadeExploreView moved"
    assert sc.script(MODAL["gate"]) == S["gate"] and sc.script(PANEL["gate"]) == S["gate"], "the arena OfflineUIGates moved"
    assert sc.script(PANEL["img"]) == K.IMAGE, "Arena_Panel's Image moved"
    if any(sc.has(r) for r in RETIRED_ROOTS):
        clash = ids.all() & set(sc.order)
        assert not clash, f"minted fileIDs already in the scene: {sorted(clash)[:3]}"
    for root, name in RETIRED_ROOTS.items():
        if sc.has(root):
            assert sc.field(root, "m_Name") == name, f"retired root {root} is no longer {name}"
            for fid in dict.fromkeys(C.subtree(sc, root)):
                if sc.has(fid):
                    sc.remove(fid)
    for fid in list(sc.order):
        if fid in ids.all():
            sc.remove(fid)
    # Rev 3 stand-down: the party panel's stub (from when ArenaView nested it) goes with it.
    legacy_party = str(C.derived(C.derived(K.FileIds("PartyPanel")("root#go"), K.FileIds("ArenaView")("PartyPanel#inst")), ids.inst))
    if sc.has(legacy_party):
        sc.remove(legacy_party)
    out = []
    K.emit_instance(ids.ctx, K.Instance("ArenaView", "ArenaView", asset_guid("ArenaView"), "ArenaView", STRETCH, ()),
                    EXPLORE["rt"], out)
    out += [ids.stub_doc(k) for k in ids.refs]
    anchor = EXPLORE["rt"]
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(EXPLORE["rt"], T.set_block(sc.docs[EXPLORE["rt"]], "m_Children", K.refs_list("m_Children", [ids.stub])))
    sc.replace(PANEL["rt"], T.set_block(sc.docs[PANEL["rt"]], "m_Children", K.refs_list("m_Children", [EXPLORE["rt"]])))
    mrt = T.set_block(sc.docs[MODAL["rt"]], "m_Children", K.refs_list("m_Children", [PANEL["rt"]]))
    mrt = C.field_set(mrt, "m_AnchoredPosition", "{x: 0, y: 0}")
    sc.replace(MODAL["rt"], C.field_set(mrt, "m_SizeDelta", "{x: 0, y: 0}"))
    sc.replace(PANEL["img"], C.field_set(sc.docs[PANEL["img"]], "m_Enabled", "0"))
    sc.replace(MODAL["gate"], T.set_block(sc.docs[MODAL["gate"]], "onlineOnlyObjects", "  onlineOnlyObjects: []\n"))
    sc.replace(PANEL["gate"], T.set_block(sc.docs[PANEL["gate"]], "onlineOnlyObjects", "  onlineOnlyObjects: []\n"))
    sc.replace(EXPLORE["view"], view_doc(sc.docs[EXPLORE["view"]], ids))
    return sc.text(), ids


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def prefab_source(guid, files):
    for name in PREFABS:
        if asset_guid(name) == guid:
            return files[prefab_path(name)]
    if guid == A.asset_guid("PartyPanel"):
        return open(os.path.join(REPO, A.prefab_path("PartyPanel")), encoding="utf-8").read()
    for name in A.PARTY_PREFABS:
        if A.asset_guid(name) == guid:
            return open(os.path.join(REPO, A.prefab_path(name)), encoding="utf-8").read()
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
CARD_FIELDS = dict(GameTitle=TMP, BackgroundImage=IMG, StarImage=IMG, AvatarSpace=224, tile=S["tile"],
                   eyebrowText=S["kt"], taglineText=TMP, domainsText=S["kt"], hullRow=224)


def validate(files):
    M.require_entrances(files, {prefab_path("ArenaView"): 2})
    guids = T.known_guids() | P.PACKAGE | {GRID_LAYOUT}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    memo = {}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)
        check_sources(path, text_, files, memo)

    card = files[prefab_path("ArenaGameCard")]
    _, body = A.doc_of(card, K.FileIds("ArenaGameCard")("root#card"))
    assert body and S["card"] in body, "ArenaGameCard: the root carries no KitArenaGameCard"
    for field, want in CARD_FIELDS.items():
        C.wired(card, body, field, want, "ArenaGameCard")
    A.check_card_a(card, "ArenaGameCard", "Clip/Band", (A.CONTENT_W - (COLS - 1) * GAP) / COLS, ART_BAND_H)
    chip_le = A.doc_of(card, K.FileIds("ArenaGameCard")("Hulls/Chip#le"))[1]
    assert chip_le and "  m_IgnoreLayout: 1\n" in chip_le, "the hull chip template must start out of layout (hidden)"
    view = files[prefab_path("ArenaView")]
    A.check_window_inset(view, "ArenaView")      # clear of the friends orb (rev 6)
    assert view.count(f"guid: {asset_guid('ArenaGameCard')}") >= 2 * ROWS, "ArenaView: the grid does not hold ArenaGameCards"
    assert f"guid: {A.asset_guid('PartyPanel')}" not in view, "ArenaView still instances the PartyPanel (rev 3: the friends drawer)"
    assert view.count("guid: " + S["close"]) == 1, "ArenaView: Back must close through ModalWindowCloseButton"
    # The phone layout: Arcade's window, the content at PH_SCALE, the grid PH_COLS across in cells that fit the phone.
    check_phone_window(view, "ArenaView")
    V = K.FileIds("ArenaView")
    _, g = A.doc_of(view, V("Grid#grid"))
    assert g and f"guid: {GRID_LAYOUT}" in g, "ArenaView: the cards are not in one wrapping grid (a phone lays them fewer across)"
    assert f"  m_ConstraintCount: {COLS}\n" in g, f"ArenaView: the grid is not {COLS} across on PC"
    ph = phone_of(view, V("Grid#phone"))
    assert ph and ph["gridColumns"] == str(PH_COLS), f"ArenaView: the grid is not {PH_COLS} across on a phone"
    assert PH_COLS * ph["gridCellSize"][0] + (PH_COLS - 1) * GAP <= phone_content_w(PH_SCALE), "ArenaView: the phone grid is wider than the phone"
    assert ph["gridCellSize"][0] - 52 >= K.label_width("Astro League", "H1"), "ArenaView: a phone card is too narrow for its title"
    ph = phone_of(view, V("Explore/Viewport/Content#phone"))
    assert ph and float(ph["widthScale"]) == PH_SCALE, "ArenaView: the explore content is not drawn at the phone scale"
    assert (6 + 18 + 10 + CARD_H) * PH_SCALE <= PH_BODY_H, "ArenaView: a phone card does not fit the body under ALL GAMES"
    ph = phone_of(card, K.FileIds("ArenaGameCard")("Star#phone"))
    assert ph and min(ph["sizeDelta"]) * PH_SCALE >= PH_TOUCH, "ArenaGameCard: the favourite star is under a thumb's size on a phone"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files, memo)
    for root, name in RETIRED_ROOTS.items():
        assert not sc.has(root), f"the arena window still holds retired {name}"
    ids = Ids()
    doc = sc.docs[EXPLORE["view"]]
    for field in ("GameSelectionView", "GameSelectionGrid", "gameCountText"):
        assert f"  {field}: {{fileID: {ids.stubs[field]}}}" in doc, f"the arena ArcadeExploreView.{field} does not point into ArenaView"
    assert f"rosterOverride: {{fileID: 11400000, guid: {ARENA_GAMES}" in doc, "the arena window no longer draws ArenaGames"
    assert "onlineOnlyObjects: []" in sc.docs[PANEL["gate"]], "the arena gate still names an object (the party panel moved to the drawer)"
    assert "  m_Enabled: 0\n" in sc.docs[PANEL["img"]], "the arena panel's gradient is still on"


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
    card = prefab_path("ArenaGameCard")
    ids = Ids()
    cases = [
        ("the party panel back in the window", lambda f: f.__setitem__(prefab_path("ArenaView"), f[prefab_path("ArenaView")] + f"# guid: {A.asset_guid('PartyPanel')}\n")),
        ("the window's entrances stripped", lambda f: f.__setitem__(prefab_path("ArenaView"), M.strip_entrances(f[prefab_path("ArenaView")]))),
        ("the body back under the friends orb", lambda f: f.__setitem__(prefab_path("ArenaView"), A.in_one(
            f[prefab_path("ArenaView")], K.FileIds("ArenaView")("Body#rt"), "m_AnchoredPosition: {x: 20", "m_AnchoredPosition: {x: -20"))),
        ("a card with no hull row", lambda f: f.__setitem__(card, re.sub(r"(  hullRow: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[card]))),
        ("the hull template left in layout", lambda f: f.__setitem__(card, f[card].replace("  m_IgnoreLayout: 1\n", "  m_IgnoreLayout: 0\n"))),
        ("the roster back to the master list", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"rosterOverride: {{fileID: 11400000, guid: {ARENA_GAMES}, type: 2}}", "rosterOverride: {fileID: 0}", 1))),
        ("the phone header off Arcade's", lambda f: f.__setitem__(prefab_path("ArenaView"), f[prefab_path("ArenaView")].replace(
            "  sizeDelta: {x: -128, y: 52}\n", "  sizeDelta: {x: -128, y: 64}\n"))),
        ("the phone grid four across", lambda f: f.__setitem__(prefab_path("ArenaView"), f[prefab_path("ArenaView")].replace(
            f"  gridColumns: {PH_COLS}\n", "  gridColumns: 4\n"))),
        ("the star a PC-sized target on a phone", lambda f: f.__setitem__(card, f[card].replace(
            f"  sizeDelta: {{x: {PH_STAR}, y: {PH_STAR}}}\n", "  sizeDelta: {x: 44, y: 44}\n"))),
        ("the grid field nulled", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  GameSelectionGrid: {{fileID: {ids.stubs['GameSelectionGrid']}}}", "  GameSelectionGrid: {fileID: 0}"))),
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
    print(f"OK: {len(PREFABS)} arena prefabs and Menu_Main validate")
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
