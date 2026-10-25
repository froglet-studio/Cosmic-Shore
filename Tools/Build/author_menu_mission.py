#!/usr/bin/env python3
"""
REV 17 J (Docs/MENU_REDESIGN_B.md section 71): this window is the VOYAGE window - Episodes on the left (Episode 1 in a
glass panel with its quest tracker, VoyageQuestCard.prefab - the Profile's old QuestCard, compact - driven by
QuestTrackView; Episode 2 a locked bar) and Voyages on the right, a scrolling column (VoyageView: Tandava full bleed;
Voyage 2 and 3 locked). Rev 13's QuestRow.prefab is retired. The hub entry is Available and reads Voyage. The text
below describes the Mission window it was built as.

Author the MISSION window of the style-B menu redesign (item 8, Docs/MENU_REDESIGN_B.md section 17.2;
the approved MissionWindow board on the "Menu Redesign B" canvas) as prefabs, and wire it into
Menu_Main.unity. It takes the Arena window's LOOK (author_menu_arena.py) and none of its data path:
missions are their own games (SO_Mission : SO_Game, never an SO_ArcadeGame), so the grid is drawn by
MissionExploreView from MissionGames (the SO_MissionList Arcade launches missions from), not by an
ArcadeExploreView.

    Assets/_Prefabs/UI Elements/Main Menu Screens/Mission/
        MissionCard.prefab   KitMissionCard on a KitTile - the art, "Mission - difficulty 1 - 9", the
                             title, "8 threats", the description; a MenuAvailabilityView set Locked
                             ("Missions open with the full release."), drawn undimmed as the board shows
        MissionView.prefab   nav dim, backdrop, header ("MISSIONS / Explore missions", Back (B)), the shared
                             PartyPanel (item 3, instanced), the explore scroll with ALL MISSIONS + count
                             and a two-column grid MissionExploreView fills at runtime

    Menu_Main.unity, `ModalWindows/MissionScreenModal`:
        The old Header, GameSelectScrollView, ArcadeLobbyList, CloseButton and FriendListPanel are deleted,
        as is the ArcadeExploreView on Mission_Panel/Explore (it never drew missions - with no roster it
        drew the arcade master list); ArcadeScreen.ExploreView is cleared (it is optional there). One
        MissionView instance goes under Explore. The window stretches full screen, the panel's gradient is
        off and the panel's OfflineUIGate gates the party panel. The hub entry stays Unavailable, and the
        mission data, Arcade.LaunchMission and the mission scenes are untouched.

STANDING DOWN: the migration runs only while the old objects exist; the documents this script owns are
re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_mission.py              # write
    python3 Tools/Build/author_menu_mission.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_mission.py --self-test  # planted defects must all be caught
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
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Mission"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
ref, mono, refs = A.ref, A.mono, A.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node, ring_node = A.image_node, A.art_node, A.ring_node
TL = C.TL
c = A.c

PREFABS = ("MissionCard", "MissionView", "VoyageQuestCard")
# Rev 13's quest rows, replaced by the compact QuestCard track (section 71).
RETIRED = tuple(f"{OUT_DIR}/QuestRow.prefab{x}" for x in ("", ".meta"))
S = dict(card=C.script("KitMissionCard"), view=C.script("MissionExploreView"), avail=C.script("MenuAvailabilityView"),
         arcade_view=A.S["view"], screen=C.script("ArcadeScreen"), gate=A.S["gate"], tile=A.S["tile"], kt=A.S["kt"],
         cover=A.S["cover"], close=A.S["close"], voyage=C.script("VoyageView"))
MISSION_GAMES = "7f11dbcad659c684e96f9673bc3f367e"       # MissionGames.asset (SO_MissionList)
GRID_LAYOUT = "8a8695521f0d02e499659fee002a26c2"         # UnityEngine.UI.GridLayoutGroup
LOCKED_MESSAGE = "Missions open with the full release."
CARD_W, CARD_H, GAP = 682, 372, 20
GRID_W = 2 * CARD_W + GAP
PLACEHOLDER_ART = A.sprite_guid("Assets/_SO_Assets/Games/Missions/SO_Mission_Protect.asset", "CardBackground")


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


# --------------------------------------------------------------------------------------------
# MissionCard.prefab
# --------------------------------------------------------------------------------------------

def build_card():
    root = N("root", "MissionCard", R(size=(CARD_W, CARD_H)))
    ring_node(root, "FocusRing", 14)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    clip = root.child(N("Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
    clip.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("mask", 114, A.mask(False)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], f"  image: {ref(ctx.c('Clip/Art', 'img'))}\n"))
    art_node(clip, "Clip/Art", "Art", R(size=(CARD_W, CARD_W * 510 / 768)), PLACEHOLDER_ART)
    image_node(clip, "Clip/Fade", "Fade", R((0, 0), (1, 0), (0, 0), (0, 200), (0.5, 0)), "SurfaceBlack",
               K.kit_sprite("kit_fade_v"))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))

    label(root.child(N("Eyebrow", "Eyebrow", R((0, 0), None, (26, 120), (420, 18), (0, 0)))), "Mission - difficulty 1 - 9",
          "Label", "NeutralLightest")
    title = root.child(N("Title", "Title", R((0, 0), None, (26, 70), (440, 46), (0, 0))))
    title.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Mission", "H1", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    label(root.child(N("Threats", "Threats", R((1, 0), None, (-26, 80), (220, 18), (1, 0)))), "8 threats", "Label",
          "TextLight", align="right")
    tag = root.child(N("Tagline", "Tagline", R((0, 0), None, (26, 22), (620, 42), (0, 0))))
    tag.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("", "BodySmall", tok("TextLight"), valign="top", wrap=True)) \
        .add("kt", 114, K.kit_text("BodySmall", "TextLight"))

    root.add("tile", 114, A.tile_body(c("FocusRing", "img"), c("Fill", "img"), c("Border", "img"), 14,
                                      primary=[c("Title", "tmp")], art=[c("Clip/Art", "img")], sel_border="Selection",
                                      on_click=[lambda ctx: (ctx.c("root", "card"), "CosmicShore.UI.KitMissionCard", "OnPressed")]))
    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  tile: {ref(ctx.c('root', 'tile'))}\n  art: {ref(ctx.c('Clip/Art', 'img'))}\n"
             f"  eyebrowText: {ref(ctx.c('Eyebrow', 'tmp'))}\n  titleText: {ref(ctx.c('Title', 'tmp'))}\n"
             f"  threatsText: {ref(ctx.c('Threats', 'tmp'))}\n  taglineText: {ref(ctx.c('Tagline', 'tmp'))}\n"
             f"  availability: {ref(ctx.c('root', 'avail'))}\n"))
    # Locked, and drawn as the board draws it: no dimming, no overlay - the press is the refusal.
    root.add("avail", 114, lambda ctx, go: mono(go, S["avail"],
             "  availability: 1\n  lockedOverlay: {fileID: 0}\n  unavailableOverlay: {fileID: 0}\n  label: {fileID: 0}\n"
             "  unavailableLabelColor: {r: 1, g: 1, b: 1, a: 0.4}\n  dimOwnGraphics: 0\n"
             f"  dimTint: {{r: 1, g: 1, b: 1, a: 0.35}}\n  lockedMessage: {LOCKED_MESSAGE}\n"))
    root.add("shake", 114, lambda ctx, go: mono(go, K.kit_script("KitShake"), ""))
    root.add("scroll", 114, A.scroll_into())
    return root


# --------------------------------------------------------------------------------------------
# MissionView.prefab
# --------------------------------------------------------------------------------------------

# --------------------------------------------------------------------------------------------
# Rev 13 (Shombith, 2026-10-25, §69): the Mission window is the VOYAGE window. "the episodes and quest tracker moves into
# the voyages ... and we can lead onto voyages (specific missions like the tandava)". Left: the episode card and the
# quest track (QuestTrackView, moved here from the Profile, spawning QuestRow). Right: the voyages - Tandava featured
# and open (VoyageView: Set sail opens its launch window), and Episode 2 locked. The approved R13Voyage board.
# --------------------------------------------------------------------------------------------

QUEST_LIST = "5eee61facaac4bb46b9e9892512f74cb"          # GameModeQuestList.asset (SO_UnlockList), the Profile's before
TANDAVA_CARD = "a7460784ce7229f7440d35280ae4b04f"        # Games/_Archive/ArcadeGameTandava.asset (author_tandava_assets VOYAGE)
SLIDER = P.SLIDER
COLUMN_GAP, HEAD_H = 40, 30
VOYAGES_W, FEATURE_H, LOCKED_H, VOYAGE_GAP, LOCKED_VOYAGES, FORM_ROW = 480, 556, 104, 16, 2, 24
EPISODES_W = 1760 - VOYAGES_W - COLUMN_GAP
TRACK_TOP, QUEST_GAP, QUEST_H, QUEST_ART_H = 78, 16, 500, 140
QUEST_W = (EPISODES_W - 56 - 4 * QUEST_GAP) // 5
EPISODE_H, EPISODE_GAP, LOCKED_EP_H = TRACK_TOP + QUEST_H + 22, 16, 64
FORMS = (("Great Serpent", "eats coiled, wrapped or in a figure-eight"), ("Many-Headed Serpent", "every head rears, then strikes"),
         ("Lord of the Dance", "rises in a halo of 12 rings to a drum"), ("Antlion", "lunges with its jaws wide"))


def panel_frame(node, key, accent="SurfaceDark"):
    """The Rev 10 panel: a cut-corner glass frame with corner brackets."""
    C.frame(node, f"{key}/Frame", 14, "SurfaceVeryDark", accent, fill_alpha=0.86)
    M.bracket_nodes(node, f"{key}/Bracket")


def build_view():
    """Rev 17 J (Docs/MENU_REDESIGN_B.md section 71, Shombith 2026-10-25 "Rev17 J implement"): Episodes on the left -
    Episode 1 open in a glass panel with its quest tracker (the Profile's old QuestCard track, compact), Episode 2 a locked
    bar under it - and Voyages on the right, a column that scrolls: Tandava as full-bleed art, the next voyages locked.
    An episode is not a voyage; the two never share a look. No line of text the game does not mean."""
    root = N("root", "MissionView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=A.WINDOW_BACKDROP_ALPHA)
    # §67: the sea lines over the glass (Sea_Window.mat), slow wave lines in three depth layers.
    root.child(M.window_sea_node("BackdropLattice", rect=R((0, 0), (1, 1), (0, -42), (0, -84))))

    # Sibling order is FOCUS order (ScreenSwitcher.Refocus): Set sail first, the quests next, Back last.
    body = root.child(N("Body", "Body", A.WINDOW_BODY))
    body.add("hlg", 114, lambda ctx, go: K.hv_layout(False, (0, 0, 0, 0), COLUMN_GAP, 0, (1, 1), (0, 1))(ctx, go)
             .replace("  m_ReverseArrangement: 0\n", "  m_ReverseArrangement: 1\n"))
    # Motion (section 22.8): the two columns rise in on every open (KitEntrance.ReplayUnder).
    body.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", stagger=1))

    # Right column (first in hierarchy, drawn on the right: the row is reversed): the voyages, a list that grows.
    right = body.child(N("Voyages", "Voyages", R((0, 1), None, (0, 0), (VOYAGES_W, 844), (0, 1))))
    right.add("le", 114, le(pw=VOYAGES_W, mw=VOYAGES_W))
    label(right.child(N("Voyages/Head", "Head", R((0, 1), (1, 1), (0, 0), (0, 18), (0.5, 1)))), "Voyages", "Label", "TextLight")
    sc = right.child(N("Voyages/Scroll", "Scroll", R((0, 0), (1, 1), (0, -HEAD_H / 2), (0, -HEAD_H))))
    sc.add("scroll", 114, A.scroll_rect("Voyages/Scroll/Viewport/Content", "Voyages/Scroll/Viewport", 20))
    vp = sc.child(N("Voyages/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, A.rect_mask())
    content = vp.child(N("Voyages/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), VOYAGE_GAP, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))

    # Tandava: the art full bleed, a fade at the foot, OPEN, its name, its four forms, SET SAIL.
    feat = content.child(N("Featured", "Featured", R((0, 1), None, (0, 0), (VOYAGES_W, FEATURE_H), (0, 1))))
    feat.add("le", 114, le(ph=FEATURE_H))
    clip = feat.child(N("Featured/Clip", "Clip", STRETCH))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack"))).add("mask", 114, A.rect_mask())
    art = clip.child(N("Featured/Clip/Art", "Art", STRETCH))
    art.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], ""))
    image_node(clip, "Featured/Clip/Fade", "Fade", R((0, 0), (1, 0), (0, FEATURE_H * 0.36), (0, FEATURE_H * 0.72), (0.5, 0.5)),
               "SurfaceBlack", K.kit_sprite("kit_fade_v"), alpha=0.96)
    image_node(feat, "Featured/Border", "Border", STRETCH, "SurfaceLight", K.kit_sprite("kit_sliver14_line1"), True)
    chip = feat.child(N("Featured/Open", "Open", TL(20, 18, 70, 26)))
    C.frame(chip, "Featured/Open/Frame", 10, "SurfaceBlack", "Cta", fill_alpha=0.6)
    label(chip.child(N("Featured/Open/Label", "Label", STRETCH)), "Open", "Label", "Cta", align="center")
    label(feat.child(N("Featured/Eyebrow", "Eyebrow", C.TR(20, 22, 160, 18))), "Voyage 1", "Label", "TextLight", align="right")
    w, h = K.button_size("SET SAIL")
    sail = feat.child(N("Featured/SetSail", "SetSail", R((0, 0), (1, 0), (0, 22), (-48, h), (0.5, 0))))
    sail.children.append(P.kit_button("SetSail", "SetSailButton", "Cta", "SET SAIL", STRETCH, opaque=True))
    forms_y = 22 + h + 18
    for i, (name, _) in enumerate(reversed(FORMS)):
        k = f"Featured/Forms/Form{len(FORMS) - i}"
        first = i == len(FORMS) - 1
        f = feat.child(N(k, f"Form{len(FORMS) - i}", R((0, 0), (1, 0), (0, forms_y + i * FORM_ROW + FORM_ROW / 2), (-48, FORM_ROW), (0.5, 0.5))))
        image_node(f, f"{k}/Node", "Node", R((0, 0.5), None, (0, 0), (14, 12), (0, 0.5)), "TextLight" if first else "SurfaceLight",
                   K.kit_sprite("kit_hex_fill"))
        label(f.child(N(f"{k}/Name", "Name", R((0, 0), (1, 1), (12, 0), (-24, 0)))), name, "BodySmall",
              "TextLight" if first else "NeutralLightest")
    t = feat.child(N("Featured/Title", "Title", R((0, 0), (1, 0), (0, forms_y + len(FORMS) * FORM_ROW + 12 + 31), (-48, 62), (0.5, 0.5))))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Voyage", "Display", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Display", "TextLight", driven=True))
    feat.add("voyage", 114, lambda ctx, go: mono(go, S["voyage"],
             f"  voyage: {K.ref(11400000, TANDAVA_CARD, 2)}\n  configureModal: {{fileID: 0}}\n"
             f"  art: {ref(ctx.c('Featured/Clip/Art', 'img'))}\n"
             f"  titleText: {ref(ctx.c('Featured/Title', 'tmp'))}\n"
             "  descriptionText: {fileID: 0}\n"
             f"  setSailButton: {ref(ctx.iref('SetSail', 'root#button'))}\n"))

    # The voyages still to come: locked, no art, no name yet - the number and a lock.
    for n in range(2, 2 + LOCKED_VOYAGES):
        k = f"Voyage{n}"
        v = content.child(N(k, k, R((0, 1), None, (0, 0), (VOYAGES_W, LOCKED_H), (0, 1))))
        v.add("le", 114, le(ph=LOCKED_H))
        image_node(v, f"{k}/Fill", "Fill", STRETCH, "SurfaceVeryDark", K.kit_sprite("kit_sliver14_fill"), True, alpha=0.86)
        image_node(v, f"{k}/Border", "Border", STRETCH, "SurfaceDark", K.kit_sprite("kit_sliver14_line1"), True)
        label(v.child(N(f"{k}/Eyebrow", "Eyebrow", R((0, 0), None, (22, 18), (200, 18), (0, 0)))), f"Voyage {n}", "Label", "SurfaceLight")
        image_node(v, f"{k}/Lock", "Lock", R((1, 0), None, (-22, 16), (18, 21), (1, 0)), "SurfaceLight",
                   K.chrome_sprite("chrome_lock"), preserve=True)

    # Left column: the episodes.
    left = body.child(N("Episodes", "Episodes", R((0, 1), None, (0, 0), (EPISODES_W, 844), (0, 1))))
    left.add("le", 114, le(mw=0, fw=1))
    label(left.child(N("Episodes/Head", "Head", R((0, 1), (1, 1), (0, 0), (0, 18), (0.5, 1)))), "Episodes", "Label", "TextLight")
    ep = left.child(N("Episode", "Episode1", R((0, 1), (1, 1), (0, -HEAD_H - EPISODE_H / 2), (0, EPISODE_H), (0.5, 0.5))))
    panel_frame(ep, "Episode")
    label(ep.child(N("Episode/Eyebrow", "Eyebrow", TL(28, 30, 110, 18))), "Episode 1", "Label", "NeutralLightest")
    label(ep.child(N("Episode/Title", "Title", TL(140, 22, 320, 32))), "The Hypersea", "H3", "TextLight")
    bar = ep.child(N("Episode/Bar", "Bar", TL(470, 37, 200, 4)))
    bar.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    fa = bar.child(N("Episode/Bar/FillArea", "FillArea", STRETCH))
    fl = fa.child(N("Episode/Bar/FillArea/Fill", "Fill", R((0, 0), (0.2, 1), (0, 0), (0, 0))))
    fl.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))).add("th", 114, H.themed("ControlActive"))
    bar.add("slider", 114, lambda ctx, go: K.mono(go, SLIDER,
            K.SELECTABLE_FIELDS.replace("  m_Interactable: 1\n", "  m_Interactable: 0\n")
            + f"  m_TargetGraphic: {ref(ctx.c('Episode/Bar', 'img'))}\n"
            f"  m_FillRect: {ref(ctx.rt('Episode/Bar/FillArea/Fill'))}\n  m_HandleRect: {{fileID: 0}}\n"
            "  m_Direction: 0\n  m_MinValue: 0\n  m_MaxValue: 5\n  m_WholeNumbers: 1\n  m_Value: 1\n"
            "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n", enabled=False))
    label(ep.child(N("Episode/Summary", "Summary", TL(686, 30, 100, 18))), "0 / 5", "Label", "NeutralLightest")
    row = ep.child(N("Episode/Quests", "Quests", R((0, 1), (1, 1), (0, -TRACK_TOP - QUEST_H / 2), (-56, QUEST_H), (0.5, 0.5))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), QUEST_GAP, 0, (1, 1), (1, 0)))
    card_ref = f"{{fileID: {K.FileIds('VoyageQuestCard')('root#go')}, guid: {asset_guid('VoyageQuestCard')}, type: 3}}"
    ep.add("track", 114, lambda ctx, go: mono(go, P.S["track"],
             f"  questList: {K.ref(11400000, QUEST_LIST, 2)}\n  questItemPrefab: {card_ref}\n"
             f"  questItemContainer: {ref(ctx.rt('Episode/Quests'))}\n"
             f"  progressBarSlider: {ref(ctx.c('Episode/Bar', 'slider'))}\n"
             "  sliderAnimDuration: 1\n  sliderEase: 32\n  ghostSlider: {fileID: 0}\n"
             "  questDescriptionContainer: {fileID: 0}\n  questDescriptionPrefab: {fileID: 0}\n  descriptionFadeDuration: 0.4\n"
             "  scrollRect: {fileID: 0}\n  snapDuration: 0.6\n  snapSpeedThreshold: 50\n  minCardScale: 0.8\n"
             "  minCardAlpha: 0.7\n  parallaxFalloff: 0\n"
             f"  summaryText: {ref(ctx.c('Episode/Summary', 'tmp'))}\n"
             "  summaryLine: '{claimed} / {total}'\n  comingSoonLine: Coming Soon\n"))
    nxt = left.child(N("Episode2", "Episode2", R((0, 1), (1, 1), (0, -HEAD_H - EPISODE_H - EPISODE_GAP - LOCKED_EP_H / 2),
                                                  (0, LOCKED_EP_H), (0.5, 0.5))))
    panel_frame(nxt, "Episode2")
    label(nxt.child(N("Episode2/Eyebrow", "Eyebrow", R((0, 0.5), None, (28, 0), (110, 18), (0, 0.5)))), "Episode 2", "Label", "SurfaceLight")
    image_node(nxt, "Episode2/Lock", "Lock", R((0, 0.5), None, (128, 0), (14, 16), (0, 0.5)), "SurfaceLight",
               K.chrome_sprite("chrome_lock"), preserve=True)

    header = root.child(N("Header", "Header", A.WINDOW_HEADER))
    A.drawer_dock(root)
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))), "Voyage", "H1", "TextLight")
    A.back_button(header)
    return root


def build_voyage_quest_card():
    """The Profile's old quest card (author_menu_profile.build_quest_card), compact: five across Episode 1."""
    return P.build_quest_card("VoyageQuestCard", QUEST_W, QUEST_H, QUEST_ART_H, QUEST_GAP, compact=True)


def prefab_outputs():
    out = {}
    for name, build in (("MissionCard", build_card), ("MissionView", build_view), ("VoyageQuestCard", build_voyage_quest_card)):
        out[prefab_path(name)] = P.prefab_text(build(), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Mission"))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

MODAL = dict(go="948404564", rt="948404565", gate="948404566", screen="948404569")
PANEL = dict(go="149581059", rt="149581060", img="149581062", gate="149581061")
EXPLORE = dict(go="484090327", rt="484090328", view="484090330")
RETIRED_ROOTS = {"1495346766": "Header", "1742401770": "GameSelectScrollView", "1994025746": "ArcadeLobbyList",
                 "1582282989": "CloseButton", "745667876": "FriendListPanel"}


class Ids:
    def __init__(self):
        self.ctx = P.Ctx("Menu_Main/Mission")
        self.inst = str(self.ctx.fid("MissionView#inst"))
        self.stub = str(self.ctx.fid("MissionView#stub"))
        V = K.FileIds("MissionView")
        self.party_src = C.derived(K.FileIds("PartyPanel")("root#go"), V("PartyPanel#inst"))
        self.party = str(C.derived(self.party_src, self.inst))

    def all(self):
        # self.party is the retired party stub's id: listed so a re-run removes it (rev 3 stand-down)
        return {self.inst, self.stub, self.party}

    def party_stub(self):
        return T.stub_go(self.party, self.party_src, asset_guid("MissionView"), self.inst)


def transform(text_):
    sc = T.Scene(text_)
    ids = Ids()
    assert sc.field(MODAL["go"], "m_Name") == "MissionScreenModal", "MissionScreenModal moved"
    assert sc.field(PANEL["go"], "m_Name") == "Mission_Panel", "Mission_Panel moved"
    assert sc.field(EXPLORE["go"], "m_Name") == "Explore", "the mission Explore moved"
    assert sc.script(MODAL["gate"]) == S["gate"] and sc.script(PANEL["gate"]) == S["gate"], "the mission OfflineUIGates moved"
    assert sc.script(MODAL["screen"]) == S["screen"], "the mission ArcadeScreen moved"
    assert sc.script(PANEL["img"]) == K.IMAGE, "Mission_Panel's Image moved"
    if any(sc.has(r) for r in RETIRED_ROOTS):
        clash = ids.all() & set(sc.order)
        assert not clash, f"minted fileIDs already in the scene: {sorted(clash)[:3]}"
    for root, name in RETIRED_ROOTS.items():
        if sc.has(root):
            assert sc.field(root, "m_Name") == name, f"retired root {root} is no longer {name}"
            for fid in dict.fromkeys(C.subtree(sc, root)):
                if sc.has(fid):
                    sc.remove(fid)
    if sc.has(EXPLORE["view"]):
        assert sc.script(EXPLORE["view"]) == S["arcade_view"], "the mission Explore's ArcadeExploreView moved"
        sc.remove(EXPLORE["view"])
        go = sc.docs[EXPLORE["go"]]
        sc.replace(EXPLORE["go"], go.replace(f"  - component: {{fileID: {EXPLORE['view']}}}\n", ""))
    for fid in list(sc.order):
        if fid in ids.all():
            sc.remove(fid)
    out = []
    K.emit_instance(ids.ctx, K.Instance("MissionView", "MissionView", asset_guid("MissionView"), "MissionView", STRETCH, ()),
                    EXPLORE["rt"], out)
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
    sc.replace(PANEL["gate"], T.set_block(sc.docs[PANEL["gate"]], "onlineOnlyObjects",
                                          "  onlineOnlyObjects: []\n"))
    sc.replace(MODAL["screen"], C.field_set(sc.docs[MODAL["screen"]], "ExploreView", "{fileID: 0}"))
    return sc.text(), ids


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

TMP, IMG = K.TMP_UGUI, K.IMAGE
CARD_FIELDS = dict(tile=S["tile"], art=IMG, eyebrowText=TMP, titleText=TMP, threatsText=TMP, taglineText=TMP,
                   availability=S["avail"])


def validate(files):
    M.require_entrances(files, {prefab_path("MissionView"): 2})
    guids = T.known_guids() | P.PACKAGE | {GRID_LAYOUT}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)

    card = files[prefab_path("MissionCard")]
    F = K.FileIds("MissionCard")
    _, body = A.doc_of(card, F("root#card"))
    assert body and S["card"] in body, "MissionCard: the root carries no KitMissionCard"
    for field, want in CARD_FIELDS.items():
        C.wired(card, body, field, want, "MissionCard")
    _, avail = A.doc_of(card, F("root#avail"))
    assert "  availability: 1\n" in avail, "mission cards must ship Locked"
    assert f"  lockedMessage: {LOCKED_MESSAGE}\n" in avail, "a Locked mission card needs its refusal message"
    _, tile = A.doc_of(card, F("root#tile"))
    assert "m_MethodName: OnPressed" in tile, "the card's tile must call KitMissionCard.OnPressed"
    assert "SO_ArcadeGame" not in card and A.S["view"] not in card, "a mission card must not ride the arcade data path"

    view = files[prefab_path("MissionView")]
    A.check_window_inset(view, "MissionView")      # clear of the friends orb (rev 6)
    V = K.FileIds("MissionView")
    # Rev 13 (§69): the Voyage window - the featured voyage is Tandava and Set sail launches it; the quest track lives here.
    _, vy = A.doc_of(view, V("Featured#voyage"))
    assert vy and S["voyage"] in vy, "the Voyage window has no VoyageView on its featured voyage"
    assert f"  voyage: {{fileID: 11400000, guid: {TANDAVA_CARD}, type: 2}}\n" in vy, "the featured voyage is not Tandava's card"
    for field, want in (("art", IMG), ("titleText", TMP)):
        C.wired(view, vy, field, want, "VoyageView")
    C.wired(view, vy, "setSailButton", A.S["button"], "VoyageView")
    _, tr = A.doc_of(view, V("Episode#track"))
    assert tr and P.S["track"] in tr, "the Voyage window has no QuestTrackView (the quest track moved here from the Profile)"
    assert f"  questList: {{fileID: 11400000, guid: {QUEST_LIST}, type: 2}}\n" in tr, "the quest track does not read GameModeQuestList"
    assert f"guid: {asset_guid('VoyageQuestCard')}" in tr, "the quest track does not spawn VoyageQuestCard (the old quest cards, §71)"
    for field, want in (("questItemContainer", 224), ("progressBarSlider", SLIDER), ("summaryText", TMP)):
        C.wired(view, tr, field, want, "QuestTrackView")
    # §71: Episode 2 is an episode (its own locked bar under Episode 1), not a voyage; the voyages after Tandava are numbered.
    assert "m_text: Episode 2\n" in view and "m_text: Voyage 2\n" in view, "Episode 2 and Voyage 2 must both be there, apart"
    for filler in ("Where your story goes next", "The next voyage arrives", "Learn the sea", "Co-op hunt"):
        assert filler not in view, f"the Voyage window carries filler text again: {filler!r} (Shombith: no useless text)"
    _, sr = A.doc_of(view, V("Voyages/Scroll#scroll"))
    assert sr and "  m_Vertical: 1\n" in sr, "the voyages column does not scroll (there will be more voyages)"
    card = files[prefab_path("VoyageQuestCard")]
    _, rb = A.doc_of(card, K.FileIds("VoyageQuestCard")("root#card"))
    assert rb and P.S["card"] in rb, "VoyageQuestCard: the root carries no KitQuestCard"
    for field in ("claimButton", "cardButton", "claimGroup", "playGroup", "titleText", "stateText", "numeralText", "art",
                  "goalsGroup", "node", "segment"):
        assert re.search(r"^  " + field + r": \{fileID: [1-9-]", rb, re.M), f"VoyageQuestCard: {field} is not wired"
    assert len(re.findall(r"^  - group: \{fileID: [1-9]", rb, re.M)) == 2, "VoyageQuestCard needs its two goal rows"
    assert f"guid: {A.asset_guid('PartyPanel')}" not in view, "MissionView still instances the PartyPanel (rev 3: the friends drawer)"
    assert view.count("guid: " + S["close"]) == 1, "MissionView: Back must close through ModalWindowCloseButton"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for root, name in RETIRED_ROOTS.items():
        assert not sc.has(root), f"the mission window still holds retired {name}"
    assert not sc.has(EXPLORE["view"]), "the mission window still carries an ArcadeExploreView"
    assert files[SCENE].count(f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid('MissionView')}") == 1, \
        "Menu_Main must mount exactly one MissionView"
    ids = Ids()
    assert "onlineOnlyObjects: []" in sc.docs[PANEL["gate"]], "the mission gate still names an object (the party panel moved to the drawer)"
    assert not sc.has(ids.party), "the retired party stub survives in the mission window"
    assert "  m_Enabled: 0\n" in sc.docs[PANEL["img"]], "the mission panel's gradient is still on"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t] \
        + [p for p in RETIRED if os.path.exists(os.path.join(REPO, p))]


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
    card, view = prefab_path("MissionCard"), prefab_path("MissionView")
    cases = [
        ("the window's entrances stripped", lambda f: f.__setitem__(prefab_path("MissionView"), M.strip_entrances(f[prefab_path("MissionView")]))),
        ("the body back under the friends orb", lambda f: f.__setitem__(prefab_path("MissionView"), A.in_one(
            f[prefab_path("MissionView")], K.FileIds("MissionView")("Body#rt"), "m_AnchoredPosition: {x: 20", "m_AnchoredPosition: {x: -20"))),
        ("mission cards shipped Available", lambda f: f.__setitem__(card, f[card].replace("  availability: 1\n", "  availability: 0\n"))),
        ("a card with no threat count", lambda f: f.__setitem__(card, re.sub(r"(  threatsText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[card]))),
        ("the featured voyage not Tandava", lambda f: f.__setitem__(view, f[view].replace(TANDAVA_CARD, "3" * 32))),
        ("the quest track left out", lambda f: f.__setitem__(view, f[view].replace(f"guid: {P.S['track']}", "guid: " + "4" * 32))),
        ("a quest card that cannot claim", lambda f: f.__setitem__(prefab_path("VoyageQuestCard"), re.sub(
            r"(  claimButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[prefab_path("VoyageQuestCard")]))),
        ("filler text back in the header", lambda f: f.__setitem__(view, f[view].replace("m_text: Voyage\n", "m_text: Where your story goes next\n", 1))),
        ("the voyages column frozen", lambda f: f.__setitem__(view, f[view].replace("  m_Vertical: 1\n", "  m_Vertical: 0\n", 1))),
        ("the party panel back in the window", lambda f: f.__setitem__(view, f[view] + f"# guid: {A.asset_guid('PartyPanel')}\n")),
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
    print(f"OK: {len(PREFABS)} mission prefabs and Menu_Main validate")
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
        if path in RETIRED:
            os.remove(full)
            print(f"removed {path}")
            continue
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
