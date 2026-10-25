#!/usr/bin/env python3
"""
REV 13 (Docs/MENU_REDESIGN_B.md section 69): this window is the VOYAGE window now - the episode card, the quest track
(QuestRow.prefab, QuestTrackView moved here from the Profile) and the voyages (VoyageView: Tandava open, Episode 2
locked). The hub entry is Available and reads Voyage. The text below describes the Mission window it was built as.

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

PREFABS = ("MissionCard", "MissionView", "QuestRow")
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
LEFT_W, ROW_H, ROW_GAP = 620, 64, 10
EPISODE_H, FEATURE_H, NEXT_H = 250, 480, 220
FORMS = (("Great Serpent", "eats coiled, wrapped or in a figure-eight"), ("Many-Headed Serpent", "every head rears, then strikes"),
         ("Lord of the Dance", "rises in a halo of 12 rings to a drum"), ("Antlion", "lunges with its jaws wide"))


def build_quest_row():
    """One quest as a row of the Voyage window's track: numeral, name, goal line, state, and CLAIM / PLAY - the same
    KitQuestCard QuestTrackView drives on the old Profile card, with the art, goals and track node left out."""
    root = N("root", "QuestRow", R(size=(LEFT_W, ROW_H)))
    root.add("cg", 225, canvas_group(True)).add("le", 114, le(ph=ROW_H))
    root.add("in", 114, M.entrance("Rise", by_sibling=True))
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.86), K.kit_sprite("kit_sliver10_fill"), True))
    border = root.child(N("Border", "Border", STRETCH))
    border.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    border.add("pop", 114, M.entrance("Pop", play_on_enable=False, duration=M.CEREMONY))
    num = root.child(N("Numeral", "Numeral", R((0, 0.5), None, (18, 0), (46, 40), (0, 0.5))))
    num.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("01", "H2", tok("TextLight"))).add("kt", 114, K.kit_text("H2", "TextLight"))
    title = root.child(N("Title", "Title", R((0, 0.5), (1, 0.5), (16, 10), (-200, 26), (0.5, 0.5))))
    title.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Quest", "Body", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Body", "TextLight"))
    desc = root.child(N("Description", "Description", R((0, 0.5), (1, 0.5), (16, -12), (-200, 20), (0.5, 0.5))))
    desc.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("", "BodySmall", tok("NeutralLightest")))) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest"))
    state = root.child(N("State", "State", R((1, 0.5), None, (-18, 0), (140, 18), (1, 0.5))))
    state.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("In progress", "Label", tok("NeutralLightest"), align="right")) \
        .add("kt", 114, K.kit_text("Label", "NeutralLightest"))
    # CLAIM (ready) and PLAY (in progress) sit where the state word is; KitQuestCard shows one at a time.
    w, h = K.button_size("CLAIM", small=True)
    claim = root.child(N("Claim", "Claim", R((1, 0.5), None, (-160, 0), (w, h), (1, 0.5))))
    claim.add("cg", 225, canvas_group(False))
    claim.children.append(P.small("ClaimButton", "ClaimButton", "Cta", "CLAIM", STRETCH, opaque=True))
    w, h = K.button_size("PLAY", small=True)
    play = root.child(N("Play", "Play", R((1, 0.5), None, (-160, 0), (w, h), (1, 0.5))))
    play.add("cg", 225, canvas_group(False))
    play.children.append(P.small("PlayButton", "PlayButton", "Default", "PLAY", STRETCH))
    root.add("card", 114, lambda ctx, go: mono(go, P.S["card"],
             f"  nameText: {ref(ctx.c('Title', 'tmp'))}\n"
             f"  descriptionText: {ref(ctx.c('Description', 'tmp'))}\n"
             "  iconImage: {fileID: 0}\n  lockedOverlay: {fileID: 0}\n  completedOverlay: {fileID: 0}\n"
             f"  claimButton: {ref(ctx.iref('ClaimButton', 'root#button'))}\n"
             f"  cardButton: {ref(ctx.iref('PlayButton', 'root#button'))}\n"
             "  cardBackground: {fileID: 0}\n  lockedTint: {r: 0.35, g: 0.35, b: 0.35, a: 1}\n"
             "  glowBorder: {fileID: 0}\n"
             "  activeQuestGlowColor: {r: 0.4, g: 0.7, b: 1, a: 1}\n"
             "  readyToClaimGlowColor: {r: 0.4, g: 1, b: 0.55, a: 1}\n"
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n"
             f"  border: {ref(ctx.c('Border', 'img'))}\n"
             f"  borderStrong: {K.sprite_ref(K.kit_sprite('kit_sliver10_line2'))}\n"
             f"  borderQuiet: {K.sprite_ref(K.kit_sprite('kit_sliver10_line1'))}\n"
             f"  numeralText: {ref(ctx.c('Numeral', 'kt'))}\n"
             f"  stateText: {ref(ctx.c('State', 'kt'))}\n"
             f"  titleText: {ref(ctx.c('Title', 'kt'))}\n"
             "  art: {fileID: 0}\n  lockGroup: {fileID: 0}\n  completedChip: {fileID: 0}\n  goalsGroup: {fileID: 0}\n"
             "  goals: []\n"
             f"  claimGroup: {ref(ctx.c('Claim', 'cg'))}\n"
             f"  playGroup: {ref(ctx.c('Play', 'cg'))}\n"
             "  meterBars: []\n  meterText: {fileID: 0}\n"
             f"  playLabel: {ref(ctx.iref('PlayButton', 'Label#kt'))}\n"
             "  lockedLineGroup: {fileID: 0}\n  lockedLineText: {fileID: 0}\n"
             "  node: {fileID: 0}\n  nodeFilled: {fileID: 0}\n  nodeOutline: {fileID: 0}\n"
             "  segment: {fileID: 0}\n  segmentSpark: {fileID: 0}\n"
             f"  claimPop: {ref(ctx.c('Border', 'pop'))}\n"))
    return root


def panel_frame(node, key, accent="SurfaceDark"):
    """The Rev 10 panel: a cut-corner glass frame with corner brackets."""
    C.frame(node, f"{key}/Frame", 14, "SurfaceVeryDark", accent, fill_alpha=0.86)
    M.bracket_nodes(node, f"{key}/Bracket")


def build_view():
    root = N("root", "MissionView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=A.WINDOW_BACKDROP_ALPHA)
    # §67: the sea lines over the glass (Sea_Window.mat), slow wave lines in three depth layers.
    root.child(M.window_sea_node("BackdropLattice", rect=R((0, 0), (1, 1), (0, -42), (0, -84))))

    # Sibling order is FOCUS order (ScreenSwitcher.Refocus): Set sail first, the quests next, Back last.
    body = root.child(N("Body", "Body", A.WINDOW_BODY))
    body.add("hlg", 114, lambda ctx, go: K.hv_layout(False, (0, 0, 0, 0), 40, 0, (1, 1), (0, 1))(ctx, go)
             .replace("  m_ReverseArrangement: 0\n", "  m_ReverseArrangement: 1\n"))
    # Motion (section 22.8): the two columns rise in on every open (KitEntrance.ReplayUnder).
    body.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", stagger=1))

    # Right column (first in hierarchy, drawn on the right: the row is reversed): the voyages.
    right = body.child(N("Voyages", "Voyages", R((0, 1), None, (0, 0), (1100, 844), (0, 1))))
    right.add("le", 114, le(mw=0, fw=1))
    right.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 14, 0, (1, 1), (1, 0)))
    head = right.child(N("Voyages/Head", "Head", R((0, 1), None, (0, 0), (1100, 18), (0, 1))))
    head.add("le", 114, le(ph=18))
    label(head.child(N("Voyages/Head/Label", "Label", STRETCH)), "Voyages", "Label", "TextLight")
    label(head.child(N("Voyages/Head/Count", "Count", STRETCH)), "1 open", "Label", "NeutralLightest", align="right")

    feat = right.child(N("Featured", "Featured", R((0, 1), None, (0, 0), (1100, FEATURE_H), (0, 1))))
    feat.add("le", 114, le(ph=FEATURE_H))
    panel_frame(feat, "Featured", "SurfaceLight")
    clip = feat.child(N("Featured/Clip", "Clip", R((0, 0), (0, 1), (262, 0), (520, -4), (0.5, 0.5))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack"))).add("mask", 114, A.rect_mask())
    art = clip.child(N("Featured/Clip/Art", "Art", STRETCH))
    art.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, None)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], ""))
    image_node(clip, "Featured/Clip/Fade", "Fade", R((1, 0), (1, 1), (0, 0), (160, 0), (1, 0.5)), "SurfaceBlack",
               K.kit_sprite("kit_fade_v"), alpha=0.7)
    label(clip.child(N("Featured/Clip/Kind", "Kind", TL(22, 20, 200, 18))), "Co-op hunt", "Label", "TextLight")
    chip = clip.child(N("Featured/Clip/Open", "Open", TL(150, 16, 70, 26)))
    C.frame(chip, "Featured/Clip/Open/Frame", 10, "SurfaceBlack", "Cta", fill_alpha=0.6)
    label(chip.child(N("Featured/Clip/Open/Label", "Label", STRETCH)), "Open", "Label", "Cta", align="center")
    info = feat.child(N("Featured/Info", "Info", R((0, 0), (1, 1), (290, 0), (-620, -56), (0.5, 0.5))))
    label(info.child(N("Featured/Info/Eyebrow", "Eyebrow", R((0, 1), (1, 1), (0, 0), (0, 18), (0.5, 1)))), "Voyage 1", "Label", "NeutralLightest")
    t = info.child(N("Featured/Info/Title", "Title", R((0, 1), (1, 1), (0, -24), (0, 60), (0.5, 1))))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Voyage", "Display", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Display", "TextLight", driven=True))
    d = info.child(N("Featured/Info/Description", "Description", R((0, 1), (1, 1), (0, -92), (0, 92), (0.5, 1))))
    d.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("", "BodySmall", tok("NeutralLightest"), valign="top", wrap=True))) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest", driven=True))
    track = info.child(N("Featured/Info/Forms", "Forms", R((0, 1), (1, 1), (0, -200), (0, 110), (0.5, 1))))
    C.themed_line(track, "Featured/Info/Forms/Line", "Line", R((0, 1), (1, 1), (0, -10), (-120, 2), (0.5, 1)), "SurfaceDark")
    for i, (name, line) in enumerate(FORMS):
        k = f"Featured/Info/Forms/Form{i + 1}"
        f = track.child(N(k, f"Form{i + 1}", R((i / 4, 1), ((i + 1) / 4, 1), (0, 0), (-12, 110), (0.5, 1))))
        image_node(f, f"{k}/Node", "Node", TL(0, 0, 22, 20), "TextLight" if i == 0 else "SurfaceLight",
                   K.kit_sprite("kit_hex_fill" if i == 0 else "kit_hex_line2"))
        label(f.child(N(f"{k}/Name", "Name", R((0, 1), (1, 1), (0, -28), (0, 22), (0.5, 1)))), name, "BodySmall",
              "TextLight" if i == 0 else "NeutralLightest")
        label(f.child(N(f"{k}/Line", "Line", R((0, 1), (1, 1), (0, -54), (0, 40), (0.5, 1)))), line, "Caption" if "Caption" in K.ROLES else "BodySmall",
              "NeutralLightest", valign="top", wrap=True)
    label(info.child(N("Featured/Info/Crew", "Crew", R((0, 0), (1, 0), (0, 70), (0, 20), (0.5, 0)))),
          "Squirrel, Sparrow or Rhino  -  the whole party on one domain  -  no clock", "BodySmall", "NeutralLightest")
    w, h = K.button_size("SET SAIL")
    sail = info.child(N("Featured/Info/SetSail", "SetSail", R((0, 0), None, (0, 0), (w + 20, h), (0, 0))))
    sail.children.append(P.kit_button("SetSail", "SetSailButton", "Cta", "SET SAIL", STRETCH, opaque=True))
    feat.add("voyage", 114, lambda ctx, go: mono(go, S["voyage"],
             f"  voyage: {K.ref(11400000, TANDAVA_CARD, 2)}\n  configureModal: {{fileID: 0}}\n"
             f"  art: {ref(ctx.c('Featured/Clip/Art', 'img'))}\n"
             f"  titleText: {ref(ctx.c('Featured/Info/Title', 'tmp'))}\n"
             f"  descriptionText: {ref(ctx.c('Featured/Info/Description', 'tmp'))}\n"
             f"  setSailButton: {ref(ctx.iref('SetSail', 'root#button'))}\n"))

    nxt = right.child(N("Next", "Next", R((0, 1), None, (0, 0), (1100, NEXT_H), (0, 1))))
    nxt.add("le", 114, le(ph=NEXT_H))
    panel_frame(nxt, "Next")
    label(nxt.child(N("Next/Eyebrow", "Eyebrow", TL(28, 26, 300, 18))), "Voyage 2", "Label", "NeutralLightest")
    label(nxt.child(N("Next/Title", "Title", TL(28, 50, 700, 40))), "Episode 2", "H1", "NeutralLightest")
    label(nxt.child(N("Next/Line", "Line", TL(28, 98, 700, 22))), "The next voyage arrives with Episode 2.", "Body", "NeutralLightest")
    image_node(nxt, "Next/Lock", "Lock", R((1, 0), None, (-30, 26), (24, 28), (1, 0)), "SurfaceLight",
               K.chrome_sprite("chrome_lock"), preserve=True)

    # Left column: the episode and its quest track.
    left = body.child(N("Episode", "Episode", R((0, 1), None, (0, 0), (LEFT_W, 844), (0, 1))))
    left.add("le", 114, le(pw=LEFT_W, mw=LEFT_W))
    ep = left.child(N("Episode/Card", "Card", R((0, 1), (1, 1), (0, 0), (0, EPISODE_H), (0.5, 1))))
    panel_frame(ep, "Episode/Card")
    label(ep.child(N("Episode/Card/Eyebrow", "Eyebrow", TL(28, 26, 300, 18))), "Episode 1", "Label", "NeutralLightest")
    label(ep.child(N("Episode/Card/Title", "Title", TL(28, 50, 560, 44))), "The Hypersea", "H1", "TextLight")
    label(ep.child(N("Episode/Card/Line", "Line", TL(28, 102, 560, 44))),
          "Learn the sea, find a crew, then sail your first voyage. Each episode ends on one.", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)
    bar = ep.child(N("Episode/Card/Bar", "Bar", R((0, 0), (1, 0), (-40, 60), (-136, 4), (0.5, 0.5))))
    bar.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    fa = bar.child(N("Episode/Card/Bar/FillArea", "FillArea", STRETCH))
    fl = fa.child(N("Episode/Card/Bar/FillArea/Fill", "Fill", R((0, 0), (0.2, 1), (0, 0), (0, 0))))
    fl.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))).add("th", 114, H.themed("ControlActive"))
    bar.add("slider", 114, lambda ctx, go: K.mono(go, SLIDER,
            K.SELECTABLE_FIELDS.replace("  m_Interactable: 1\n", "  m_Interactable: 0\n")
            + f"  m_TargetGraphic: {ref(ctx.c('Episode/Card/Bar', 'img'))}\n"
            f"  m_FillRect: {ref(ctx.rt('Episode/Card/Bar/FillArea/Fill'))}\n  m_HandleRect: {{fileID: 0}}\n"
            "  m_Direction: 0\n  m_MinValue: 0\n  m_MaxValue: 5\n  m_WholeNumbers: 1\n  m_Value: 1\n"
            "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n", enabled=False))
    label(ep.child(N("Episode/Card/Summary", "Summary", R((1, 0), None, (-28, 60), (110, 18), (1, 0.5)))), "0 of 5", "Label",
          "NeutralLightest", align="right")
    qhead = left.child(N("Episode/QuestsHead", "QuestsHead", R((0, 1), (1, 1), (0, -(EPISODE_H + 24)), (0, 18), (0.5, 1))))
    label(qhead.child(N("Episode/QuestsHead/Label", "Label", STRETCH)), "Quests", "Label", "TextLight")
    rows = left.child(N("Episode/Quests", "Quests", R((0, 1), (1, 1), (0, -(EPISODE_H + 52)), (0, 5 * (ROW_H + ROW_GAP)), (0.5, 1))))
    rows.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), ROW_GAP, 0, (1, 1), (1, 0)))
    row_ref = f"{{fileID: {K.FileIds('QuestRow')('root#go')}, guid: {asset_guid('QuestRow')}, type: 3}}"
    left.add("track", 114, lambda ctx, go: mono(go, P.S["track"],
             f"  questList: {K.ref(11400000, QUEST_LIST, 2)}\n  questItemPrefab: {row_ref}\n"
             f"  questItemContainer: {ref(ctx.rt('Episode/Quests'))}\n"
             f"  progressBarSlider: {ref(ctx.c('Episode/Card/Bar', 'slider'))}\n"
             "  sliderAnimDuration: 1\n  sliderEase: 32\n  ghostSlider: {fileID: 0}\n"
             "  questDescriptionContainer: {fileID: 0}\n  questDescriptionPrefab: {fileID: 0}\n  descriptionFadeDuration: 0.4\n"
             "  scrollRect: {fileID: 0}\n  snapDuration: 0.6\n  snapSpeedThreshold: 50\n  minCardScale: 0.8\n"
             "  minCardAlpha: 0.7\n  parallaxFalloff: 0\n"
             f"  summaryText: {ref(ctx.c('Episode/Card/Summary', 'tmp'))}\n"
             "  summaryLine: '{claimed} of {total}'\n  comingSoonLine: Coming Soon\n"))

    header = root.child(N("Header", "Header", A.WINDOW_HEADER))
    A.drawer_dock(root)
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(header.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (300, 18), (0, 1)))), "VOYAGE", "Label",
          "NeutralLightest")
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))), "Where your story goes next", "H1",
          "TextLight")
    A.back_button(header)
    return root


def prefab_outputs():
    out = {}
    for name, build in (("MissionCard", build_card), ("MissionView", build_view), ("QuestRow", build_quest_row)):
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
    for field, want in (("art", IMG), ("titleText", TMP), ("descriptionText", TMP)):
        C.wired(view, vy, field, want, "VoyageView")
    C.wired(view, vy, "setSailButton", A.S["button"], "VoyageView")
    _, tr = A.doc_of(view, V("Episode#track"))
    assert tr and P.S["track"] in tr, "the Voyage window has no QuestTrackView (the quest track moved here from the Profile)"
    assert f"  questList: {{fileID: 11400000, guid: {QUEST_LIST}, type: 2}}\n" in tr, "the quest track does not read GameModeQuestList"
    assert f"guid: {asset_guid('QuestRow')}" in tr, "the quest track does not spawn QuestRow"
    for field, want in (("questItemContainer", 224), ("progressBarSlider", SLIDER), ("summaryText", TMP)):
        C.wired(view, tr, field, want, "QuestTrackView")
    assert "m_Name: Episode 2\n" not in view and "Episode 2" in view, "the locked voyage is not named Episode 2"
    row = files[prefab_path("QuestRow")]
    _, rb = A.doc_of(row, K.FileIds("QuestRow")("root#card"))
    assert rb and P.S["card"] in rb, "QuestRow: the root carries no KitQuestCard"
    for field in ("claimButton", "cardButton", "claimGroup", "playGroup", "titleText", "stateText", "numeralText"):
        assert re.search(r"^  " + field + r": \{fileID: [1-9-]", rb, re.M), f"QuestRow: {field} is not wired"
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
    card, view = prefab_path("MissionCard"), prefab_path("MissionView")
    cases = [
        ("the window's entrances stripped", lambda f: f.__setitem__(prefab_path("MissionView"), M.strip_entrances(f[prefab_path("MissionView")]))),
        ("the body back under the friends orb", lambda f: f.__setitem__(prefab_path("MissionView"), A.in_one(
            f[prefab_path("MissionView")], K.FileIds("MissionView")("Body#rt"), "m_AnchoredPosition: {x: 20", "m_AnchoredPosition: {x: -20"))),
        ("mission cards shipped Available", lambda f: f.__setitem__(card, f[card].replace("  availability: 1\n", "  availability: 0\n"))),
        ("a card with no threat count", lambda f: f.__setitem__(card, re.sub(r"(  threatsText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[card]))),
        ("the featured voyage not Tandava", lambda f: f.__setitem__(view, f[view].replace(TANDAVA_CARD, "3" * 32))),
        ("the quest track left out", lambda f: f.__setitem__(view, f[view].replace(f"guid: {P.S['track']}", "guid: " + "4" * 32))),
        ("a quest row that cannot claim", lambda f: f.__setitem__(prefab_path("QuestRow"), re.sub(
            r"(  claimButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[prefab_path("QuestRow")]))),
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
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
