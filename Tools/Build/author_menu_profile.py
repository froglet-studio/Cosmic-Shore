#!/usr/bin/env python3
"""
Author ITEM 5a of the style-B menu redesign - the PROFILE SCREEN - as prefabs, and wire it into
Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 15; the approved Profile / ProfileFresh boards on the
"Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Profile/
        QuestCard.prefab     one quest card: KitQuestCard (QuestItemCard) - art + numeral, state line,
                             name, blurb, the two intensity goals, Claim / Play / "Unlocks after",
                             and its node + segment on the quest track (QuestTrackView spawns it)
        ProfileView.prefab   the screen body: the pilot card (avatar, name, Change avatar / Rename,
                             the pilot record), the Episodes card (locked), the quest progress bar and
                             the quest row

    Menu_Main.unity, `Screens/ProfileScreen`:
        The old Background, NamePanel, AvatarDisplay, Scroll View and Episodes button are deleted and one
        ProfileView instance is mounted. ProfileScreen and QuestTrackView (kept on the root - the switcher
        names the root) are re-pointed into it; the inactive Episodes panel refuses on the new card; the
        retired ProfileModal's two references into the deleted avatar / name are nulled.

    Data (decision Q5): each quest's Icon is its mode's arcade card art; the Vessel Hangar quest's is the
    Squirrel's hangar render.

STANDING DOWN: the migration runs only while the old objects exist; the documents the script owns are
re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_profile.py              # write
    python3 Tools/Build/author_menu_profile.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_profile.py --self-test  # planted defects must all be caught
"""

import argparse
import hashlib
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_configure", os.path.join(HERE, "author_menu_configure.py"))
C = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(C)

A, K, T, H, M = C.A, C.K, C.T, C.H, C.M
REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Profile"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, refs = C.TL, C.TR, C.ref, C.mono, C.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node, frame, section_label = A.image_node, A.art_node, C.frame, C.section_label

PREFABS = ("QuestCard", "ProfileView", "NowPlayingCard")

S = dict(
    card=C.script("KitQuestCard"), screen=C.script("ProfileScreen"), track=C.script("QuestTrackView"),
    episodes=C.script("EpisodeScreen"), affordance=C.script("CommerceAffordance"),
    availability=C.script("MenuAvailabilityView"), crystals=C.script("CrystalCurrencyDisplay"),
    modal=C.script("ProfileModal"), tile=K.kit_script("KitTile"), button=K.kit_script("KitButton"),
    kt=K.kit_script("KitText"), cover=K.kit_script("KitCoverImage"),
    record=C.script("ProfileRecordWindow"), studio=C.script("AudioStudioPanel"), picker=C.script("FavouritePilotPicker"),
    nowplaying=C.script("NowPlayingCard"), cbp=C.script("ControllerButtonPress"),
)
# Rev 10 (section 56): the pilot record window's data.
VESSEL_LIST = "Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset"   # the Hangar's roster (HangarScreen.ShipList)
CAPTAIN_LIST = "Assets/_SO_Assets/Captains/Elemental/ElementalCaptains.asset"
SOUNDTRACK_ASSET = "Assets/_SO_Assets/Audio/Soundtrack.asset"
SOUNDTRACK_GUID = "6a1c3e5b7d9f4b2a8c0e1f3a5b7c9d10"
# The nine tracks in event:/Music/Music today (the FMOD project's Music event, a random multi-sound).
TRACKS = (("space-gliding", "Space Gliding"), ("sky-fire", "Sky Fire"), ("cosmic-shore-2-tune", "Cosmic Shore 2 Tune"),
          ("chill-time-3", "Chill Time 3"), ("ive-waited-so-long", "I've Waited So Long"), ("cosmic-shore-3", "Cosmic Shore 3"),
          ("severity", "Severity"), ("disoriented", "Disoriented"), ("secret-hide-away", "Secret Hide Away"))
# Each track's audio file in that random event, as FMOD names the sound it starts (extension dropped). AudioSystem's
# SOUND_PLAYED callback reports the name and the soundtrack matches it here to name the song (2026-10-19).
SOUND_NAMES = {"space-gliding": "SPACE GLIDING", "sky-fire": "SKY FIRE", "cosmic-shore-2-tune": "COSMIC SHORE 2 TUNE",
               "chill-time-3": "cosmic shore chill time 3", "ive-waited-so-long": "I've waited so long",
               "cosmic-shore-3": "cosmic shore 3 looped 1", "severity": "SEVERITY", "disoriented": "DISORIENTED",
               "secret-hide-away": "SECRET HIDE AWAY"}
FMOD_METADATA = "Cosmic Shore/Metadata"
FMOD_MUSIC_EVENT = "{03de9ea9-9b51-400a-b0cb-8bcc12a12697}"   # event:/Music/Music


def fmod_music_sounds():
    """The audio files in the FMOD project's Music event, extension dropped and lower-cased; None without the project."""
    ev = os.path.join(REPO, FMOD_METADATA, "Event", FMOD_MUSIC_EVENT + ".xml")
    if not os.path.exists(ev):
        return None
    names = set()
    for fid in re.findall(r'<relationship name="audioFile">\s*<destination>(\{[^}]+\})', open(ev, encoding="utf-8").read()):
        af = os.path.join(REPO, FMOD_METADATA, "AudioFile", fid + ".xml")
        m = re.search(r'name="assetPath">\s*<value>([^<]+)', open(af, encoding="utf-8").read()) if os.path.exists(af) else None
        if m:
            names.add(os.path.splitext(m.group(1))[0].strip().lower())
    return names
VESSEL_ROWS, PILOT_TILES = 8, 4
VESSEL_PITCH, VESSEL_ROW_H = 33, 30         # Rev 13: eight rows in the 326-tall panel under the favourites
STUDIO_PITCH, STUDIO_ROW_H = 46, 40         # Rev 13: nine track rows in the 664-tall Audio Studio column
FAV_STATS = (("Games", "Games"), ("Drift", "Best drift"), ("Boost", "Best boost"), ("Prisms", "Prisms broken"))
PAD_B, PAD_LB, PAD_RB = 5, 10, 11            # UnityEngine.InputSystem.LowLevel.GamepadButton
REPLAYS_MODAL = 19                           # ScreenSwitcher.ModalWindows.REPLAYS - My Replays (author_menu_replays.py)
OPEN_MODAL = C.script("OpenModalButton")
SLIDER = "67db9e8f0e2ae9c40bc1e2b64352a6b4"
PACKAGE = C.PACKAGE | {SLIDER}
C.STUB_KIND.update({"track": (114, S["track"]), "slider": (114, SLIDER), "screen": (114, S["screen"])})

QUEST_LIST = "Assets/_SO_Assets/GameModeQuest/GameModeQuestList.asset"
SQUIRREL_VESSEL = "Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset"


def asset_guid(name):
    return hashlib.md5(f"CosmicShore/MenuB/assets/{name}".encode()).hexdigest()


def prefab_guid(name):
    return asset_guid(name) if name in PREFABS else C.prefab_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


class Inst(K.Instance):
    def __init__(self, key, prefab_key, name, r, mods=(), added=(), added_nodes=()):
        super().__init__(key, prefab_key, prefab_guid(prefab_key), name, r, mods)
        self.added = list(added)
        self.added_nodes = list(added_nodes)


class Ctx(C.Ctx):
    def iref(self, inst_key, src_key):
        prefab = self.instances[inst_key]
        src = K.source_fid(prefab, src_key)
        pid = self.fid(inst_key + "#inst")
        fid = C.derived(src, pid)
        self.stubs.setdefault(fid, C.stub_doc(fid, src_key.split("#")[1], src, prefab_guid(prefab), pid))
        return fid


def prefab_text(root, file_key):
    ctx = Ctx(file_key)
    ctx.register(root)
    out = []
    C.emit_node(ctx, root, 0, out)
    return K.unity_empty_scalars(K.HEADER + "".join(out) + "".join(ctx.stubs.values()))


def c(node_key, comp):
    return lambda ctx: ctx.c(node_key, comp)


def kit_button(key, name, variant, caption, r, opaque=True, small=False):
    prefab = "KitButtonSmall" if small else "KitButton"
    return Inst(key, prefab, name, r, K.button_mods(prefab, variant, opaque, caption, small=small))


# --------------------------------------------------------------------------------------------
# QuestCard.prefab - one quest card (352 x 670, the top 50 is its track node)
# --------------------------------------------------------------------------------------------

CARD_W, CARD_H, TRACK_H, GAP = 352, 670, 50, 20
ART_H = 190


def build_quest_card(name="QuestCard", card_w=CARD_W, card_h=CARD_H, art_h=ART_H, gap=GAP, compact=False):
    """One quest card on the track. `compact` is the Voyage window's (Docs/MENU_REDESIGN_B.md section 71, the approved
    R17 J board: five across an episode): tighter padding, the name in Body, the goal rows without their title."""
    pad = 16 if compact else 20
    cw = card_w - 2 * pad
    root = N("root", name, R(size=(card_w, card_h)))
    root.add("cg", 225, canvas_group(True)).add("le", 114, le(mw=0, fw=1, ph=card_h))
    root.add("in", 114, M.entrance("Rise", by_sibling=True))

    # The track: the segment back to the previous card's node, then this card's node.
    seg = image_node(root, "Segment", "Segment", TL(-(card_w + gap) // 2, 15, card_w + gap, 2), "SurfaceDark")
    seg.comps = [x for x in seg.comps if x[0] != "th"]
    seg.add("mask", 114, A.rect_mask())
    spark = seg.child(N("Segment/SparkGroup", "SparkGroup", STRETCH))
    spark.add("cg", 225, canvas_group(False))
    sp = spark.child(N("Segment/Spark", "Spark", R((0, 0), (0, 1), (-48, 0), (48, 0), (0, 0.5))))
    sp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_spark"))) \
        .add("sp", 114, M.spark_body(False, period=1.8, delay=0.6))
    node = image_node(root, "Node", "Node", R((0.5, 1), None, (0, -1), (34, 30), (0.5, 1)), "ControlActive",
                      K.kit_sprite("kit_hex_line2"))
    node.comps = [x for x in node.comps if x[0] != "th"]

    body = root.child(N("Body", "Body", R((0, 0), (1, 1), (0, -TRACK_H / 2), (0, -TRACK_H))))
    fill = body.child(N("Body/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.94), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("th", 114, H.themed("SurfaceVeryDark", 0.94))
    border = body.child(N("Body/Border", "Border", STRETCH))
    border.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))
    border.add("pop", 114, M.entrance("Pop", play_on_enable=False, duration=M.CEREMONY))

    clip = body.child(N("Body/Art", "Art", TL(2, 2, card_w - 4, art_h)))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack"))) \
        .add("th", 114, H.themed("SurfaceBlack")).add("mask", 114, A.rect_mask())
    art = clip.child(N("Body/Art/Image", "Image", STRETCH))
    art.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, C.SQUIRREL_ICON)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], ""))
    label(clip.child(N("Body/Art/Numeral", "Numeral", TL(16, 6, 120, 54))), "01", "Display", "TextLight")
    chip = clip.child(N("Body/Art/Completed", "Completed", TR(12, 14, 112, 26)))
    chip.add("cg", 225, canvas_group(False))
    frame(chip, "Body/Art/Completed/Frame", 10, "SurfaceBlack", "SurfaceLight", fill_alpha=0.85)
    label(chip.child(N("Body/Art/Completed/Label", "Label", STRETCH)), "Completed", "Label", "TextLight", align="center")
    lock = clip.child(N("Body/Art/Lock", "Lock", R((0.5, 0.5), None, (0, 0), (40, 46))))
    lock.add("cg", 225, canvas_group(False))
    lk = image_node(lock, "Body/Art/Lock/Glyph", "Glyph", STRETCH, "TextLight", K.chrome_sprite("chrome_lock"),
                    preserve=True)

    y = art_h + (16 if compact else 22)
    label(body.child(N("Body/State", "State", TL(pad, y, cw, 18))), "In progress", "Label", "NeutralLightest")
    label(body.child(N("Body/Title", "Title", TL(pad, y + (20 if compact else 22), cw, 26 if compact else 30))), "QUEST",
          "Body" if compact else "H3", "TextLight")
    label(body.child(N("Body/Description", "Description", TL(pad, y + (48 if compact else 58), cw, 40 if compact else 44))), "",
          "BodySmall", "NeutralLightest", valign="top", wrap=True)
    C.themed_line(body, "Body/Rule", "Rule", TL(pad, y + (94 if compact else 110), cw, 1), "SurfaceDark")

    goals = body.child(N("Body/Goals", "Goals", TL(pad, y + (106 if compact else 130), cw, 80 if compact else 120)))
    goals.add("cg", 225, canvas_group(True))
    if not compact:
        section_label(goals, "Body/Goals/Title", "Title", "Goals", TL(0, 0, 200, 18), "NeutralLightest")
    for i in range(2):
        k = f"Body/Goals/Row{i + 1}"
        row = goals.child(N(k, f"Goal{i + 1}", TL(0, i * 40, cw, 36) if compact else TL(0, 26 + i * 46, 312, 42)))
        row.add("cg", 225, canvas_group(True))
        done = image_node(row, f"{k}/Done", "Done", TL(0, 4, 14, 14), "Cta", K.chrome_sprite("chrome_confirm"), preserve=True)
        done.comps = [x for x in done.comps if x[0] != "th"]
        pend = image_node(row, f"{k}/Pending", "Pending", TL(3, 7, 9, 9), "TextLight")
        pend.comps = [x for x in pend.comps if x[0] != "th"]
        pend.rot = 45
        cap = row.child(N(f"{k}/Caption", "Caption", TL(24, 2, cw - 24, 16)))
        cap.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("UNLOCK INTENSITY 3", "Label", tok("NeutralLightest"))) \
            .add("kt", 114, K.kit_text("Label", "NeutralLightest"))
        desc = row.child(N(f"{k}/Description", "Description", TL(24, 18 if compact else 20, cw - 24, 18 if compact else 22)))
        desc.add("cr", 222, K.canvas_renderer()) \
            .add("tmp", 114, A.ellipsis(text("Play Intensity 2 to unlock", "BodySmall", tok("TextLight")))) \
            .add("kt", 114, K.kit_text("BodySmall", "TextLight"))

    foot = body.child(N("Body/Footer", "Footer", R((0, 0), (1, 0), (0, pad), (-2 * pad, 100), (0.5, 0))))
    claim = foot.child(N("Body/Footer/Claim", "Claim", R((0, 0), (1, 0), (0, 0), (0, 54), (0.5, 0))))
    claim.add("cg", 225, canvas_group(False))
    claim.children.append(Inst("ClaimButton", "KitButton", "ClaimButton", STRETCH,
                               K.button_mods("KitButton", "Cta", True, "CLAIM"),
                               added=[("mask", 114, A.rect_mask())], added_nodes=[C.sweep_node()]))
    play = foot.child(N("Body/Footer/Play", "Play", STRETCH))
    play.add("cg", 225, canvas_group(False))
    for i, h in enumerate((10, 15, 20, 25)):
        bar = image_node(play, f"Body/Footer/Play/Bar{i + 1}", f"Bar{i + 1}", R((0, 1), None, (i * 12, -40), (8, h), (0, 0)),
                         "SurfaceDark")
        bar.comps = [x for x in bar.comps if x[0] != "th"]
    mt = play.child(N("Body/Footer/Play/Meter", "Meter", R((0, 1), None, (58, -28), (min(220, cw - 58), 22), (0, 0.5))))
    mt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Intensity 1 of 4", "Body", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("Body", "TextLight"))
    w, h = K.button_size("PLAY QUEST", small=True)
    play.children.append(kit_button("PlayButton", "PlayButton", "Default", "PLAY QUEST",
                                    R((0, 0), (1, 0), (0, 0), (0, 40), (0.5, 0)), opaque=False, small=True))
    lockline = foot.child(N("Body/Footer/Locked", "Locked", R((0, 0), (1, 0), (0, 4), (0, 22), (0.5, 0))))
    lockline.add("cg", 225, canvas_group(False))
    label(lockline, "Unlocks after", "Body", "NeutralLightest")

    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  nameText: {ref(ctx.c('Body/Title', 'tmp'))}\n"
             f"  descriptionText: {ref(ctx.c('Body/Description', 'tmp'))}\n"
             f"  iconImage: {ref(ctx.c('Body/Art/Image', 'img'))}\n"
             "  lockedOverlay: {fileID: 0}\n  completedOverlay: {fileID: 0}\n"
             f"  claimButton: {ref(ctx.iref('ClaimButton', 'root#button'))}\n"
             f"  cardButton: {ref(ctx.iref('PlayButton', 'root#button'))}\n"
             "  cardBackground: {fileID: 0}\n  lockedTint: {r: 0.35, g: 0.35, b: 0.35, a: 1}\n"
             "  glowBorder: {fileID: 0}\n"
             "  activeQuestGlowColor: {r: 0.4, g: 0.7, b: 1, a: 1}\n"
             "  readyToClaimGlowColor: {r: 0.4, g: 1, b: 0.55, a: 1}\n"
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n"
             f"  border: {ref(ctx.c('Body/Border', 'img'))}\n"
             f"  borderStrong: {K.sprite_ref(K.kit_sprite('kit_sliver14_line2'))}\n"
             f"  borderQuiet: {K.sprite_ref(K.kit_sprite('kit_sliver14_line1'))}\n"
             f"  numeralText: {ref(ctx.c('Body/Art/Numeral', 'kt'))}\n"
             f"  stateText: {ref(ctx.c('Body/State', 'kt'))}\n"
             f"  titleText: {ref(ctx.c('Body/Title', 'kt'))}\n"
             f"  art: {ref(ctx.c('Body/Art/Image', 'img'))}\n"
             f"  lockGroup: {ref(ctx.c('Body/Art/Lock', 'cg'))}\n"
             f"  completedChip: {ref(ctx.c('Body/Art/Completed', 'cg'))}\n"
             f"  goalsGroup: {ref(ctx.c('Body/Goals', 'cg'))}\n"
             "  goals:\n" + "".join(
                 f"  - group: {ref(ctx.c(f'Body/Goals/Row{i + 1}', 'cg'))}\n"
                 f"    doneMark: {ref(ctx.c(f'Body/Goals/Row{i + 1}/Done', 'img'))}\n"
                 f"    pendingMark: {ref(ctx.c(f'Body/Goals/Row{i + 1}/Pending', 'img'))}\n"
                 f"    caption: {ref(ctx.c(f'Body/Goals/Row{i + 1}/Caption', 'kt'))}\n"
                 f"    description: {ref(ctx.c(f'Body/Goals/Row{i + 1}/Description', 'kt'))}\n" for i in range(2))
             + f"  claimGroup: {ref(ctx.c('Body/Footer/Claim', 'cg'))}\n"
             f"  playGroup: {ref(ctx.c('Body/Footer/Play', 'cg'))}\n"
             + refs("meterBars", [ctx.c(f"Body/Footer/Play/Bar{i + 1}", "img") for i in range(4)])
             + f"  meterText: {ref(ctx.c('Body/Footer/Play/Meter', 'kt'))}\n"
             f"  playLabel: {ref(ctx.iref('PlayButton', 'Label#kt'))}\n"
             f"  lockedLineGroup: {ref(ctx.c('Body/Footer/Locked', 'cg'))}\n"
             f"  lockedLineText: {ref(ctx.c('Body/Footer/Locked', 'kt'))}\n"
             f"  node: {ref(ctx.c('Node', 'img'))}\n"
             f"  nodeFilled: {K.sprite_ref(K.kit_sprite('kit_hex_fill'))}\n"
             f"  nodeOutline: {K.sprite_ref(K.kit_sprite('kit_hex_line2'))}\n"
             f"  segment: {ref(ctx.c('Segment', 'img'))}\n"
             f"  segmentSpark: {ref(ctx.c('Segment/SparkGroup', 'cg'))}\n"
             f"  claimPop: {ref(ctx.c('Body/Border', 'pop'))}\n"))
    return root


# --------------------------------------------------------------------------------------------
# ProfileView.prefab - the screen body
# --------------------------------------------------------------------------------------------

STATS = (("Crystals", "Crystals", "0", 0, 0), ("Games completed", "Games", "0", 1, 0),
         ("Flight time", "Flight", "0m", 0, 1), ("Sessions", "Sessions", "0", 1, 1))


# Rev 13 (Shombith, 2026-10-25, §69): "let the profile be the expanded version completely". The Profile has no tab: it
# opens from the Home pilot (or Y) over Home, with Back. The pilot record is no longer a window over the screen - it IS
# the screen, under a full-width pilot card. Episodes and the quest track moved to the Voyage window
# (author_menu_mission.py). Layout, canvas units from the top: header 100, pilot card 168, the record's panels 376.
PILOT_Y, PILOT_H = 168, 190
STATS = (("Fly time", "Flight", "0m"), ("Games", "Games", "0"), ("Sessions", "Sessions", "0"), ("Crystals", "Crystals", "0"),
         ("Pilot since", "Since", ""))


def build_view():
    root = N("root", "ProfileView", STRETCH)
    image_node(root, "Veil", "Veil", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=0.95)
    # §67: the sea lines over the veil (Sea_Window.mat).
    root.child(M.window_sea_node("VeilSea", rect=R((0, 0), (1, 1), (0, -42), (0, -84))))

    # The record first in hierarchy: Refocus lands on the first live Selectable (the favourite pilot's CHANGE).
    build_record(root)

    head = root.child(N("Header", "Header", TL(40, 96, 900, 64)))
    head.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", distance=10))
    label(head.child(N("Header/Eyebrow", "Eyebrow", TL(0, 4, 300, 18))), "Profile", "Label", "NeutralLightest")
    label(head.child(N("Header/Title", "Title", TL(0, 24, 600, 40))), "Your pilot", "H2", "TextLight")
    w, h = K.button_size("BACK", small=True)
    root.children.append(pad_button("Back", "BackButton", "Default", "BACK", TR(40, 108, w, h), PAD_B, "Record"))
    # My Replays (Yash, 2026-10-26: "inside profiles should have my replay area where I can choose the last 10
    # games"): left of BACK, opening ModalWindows.REPLAYS through OpenModalButton - the window names the player's
    # last games and plays one inside its arena (Docs/REPLAYS.md section 6).
    rw, rh = K.button_size("MY REPLAYS", small=True)
    root.children.append(Inst("Replays", "KitButtonSmall", "ReplaysButton", TR(40 + w + 12, 108, rw, rh),
                              K.button_mods("KitButtonSmall", "Default", False, "MY REPLAYS", small=True),
                              added=[("open", 114, lambda ctx, go: mono(go, OPEN_MODAL,
                                      f"  target: {REPLAYS_MODAL}\n  screenSwitcher: {{fileID: 0}}\n"))]))

    card = root.child(N("Pilot", "Pilot", R((0, 1), (1, 1), (0, -PILOT_Y), (-80, PILOT_H), (0.5, 1))))
    card.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise"))
    frame(card, "Pilot/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    card.child(M.panel_sea_node("Pilot/Grid"))
    M.bracket_nodes(card, "Pilot/Bracket")
    av = card.child(N("Pilot/Avatar", "Avatar", TL(30, 19, 152, 152)))
    image_node(av, "Pilot/Avatar/Ring", "Ring", STRETCH, "SurfaceLight", K.kit_sprite("kit_oct_fill"), raycast=True)
    image_node(av, "Pilot/Avatar/Back", "Back", R((0, 0), (1, 1), (0, 0), (-6, -6)), "SurfaceDark", K.kit_sprite("kit_oct_fill"))
    icon = art_node(av, "Pilot/Avatar/Icon", "Icon", R((0, 0), (1, 1), (0, 0), (-10, -10)), A.AVATAR_PLACEHOLDER, preserve=True)
    av.add("button", 114, C.plain_button(c("Pilot/Avatar/Ring", "img"))).add("audio", 114, A.menu_audio(11))
    label(card.child(N("Pilot/Eyebrow", "Eyebrow", TL(210, 30, 300, 18))), "Pilot", "Label", "NeutralLightest")
    nm = card.child(N("Pilot/Name", "Name", TL(210, 50, 400, 56)))
    nm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Pilot", "Display", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Display", "TextLight"))
    # Rev 13: the name opens the name editor (the avatar opens the avatar editor). Its text takes the raycast.
    nm.comps = [(k, cid, (lambda b: (lambda ctx, go: b(ctx, go).replace("  m_RaycastTarget: 0\n", "  m_RaycastTarget: 1\n", 1)))(body))
                if k == "tmp" else (k, cid, body) for k, cid, body in nm.comps]
    nm.add("button", 114, C.plain_button(c("Pilot/Name", "tmp")))
    btns = card.child(N("Pilot/Buttons", "Buttons", TL(210, 118, 400, 40)))
    btns.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 12, 3, (0, 0), (0, 0)))
    for key, cap in (("ChangeAvatar", "CHANGE AVATAR"), ("Rename", "RENAME")):
        w, h = K.button_size(cap, small=True)
        btns.children.append(kit_button(key, key, "Default", cap, R((0, 0.5), None, (0, 0), (w, h), (0, 0.5)),
                                        opaque=False, small=True))
    C.themed_line(card, "Pilot/Divider", "Divider", TL(630, 30, 1, 130), "SurfaceDark")
    for i, (cap, key, val) in enumerate(STATS):
        x = 664 + i * 230
        C.themed_line(card, f"Pilot/{key}Rule", f"{key}Rule", TL(x, 52, 2, 84), "SurfaceDark")
        label(card.child(N(f"Pilot/{key}Label", f"{key}Label", TL(x + 18, 56, 200, 16))), cap, "Label", "NeutralLightest")
        if key == "Crystals":
            image_node(card, "Pilot/CrystalIcon", "CrystalIcon", TL(x + 18, 90, 18, 18), "Purchase",
                       K.chrome_sprite("chrome_crystal"), preserve=True)
            v = card.child(N("Pilot/CrystalsValue", "CrystalsValue", TL(x + 44, 80, 180, 40)))
            label(v, val, "H1", "TextLight")
            v.add("display", 114, lambda ctx, go: mono(go, S["crystals"], f"  balanceText: {ref(ctx.c('Pilot/CrystalsValue', 'tmp'))}\n"))
        else:
            label(card.child(N(f"Pilot/{key}Value", f"{key}Value", TL(x + 18, 80, 200, 40))), val, "H1", "TextLight")
    return root


# --------------------------------------------------------------------------------------------
# Rev 10 (section 56): the pilot record window, over the whole profile on a solid background
# --------------------------------------------------------------------------------------------

def asset_ref(path):
    return K.ref(11400000, K.meta_guid(path), 2)


def pad_button(key, name, variant, caption, r, pad, group_key, opaque=False):
    """A KitButtonSmall that a pad button also presses (ControllerButtonPress, gated by `group_key`'s CanvasGroup)."""
    cbp = ("cbp", 114, lambda ctx, go: mono(go, S["cbp"],
           "  ActiveMenuScreens: \n  ActiveModalWindows: \n"
           f"  activationButton: {pad}\n  activationButtonImage: {{fileID: 0}}\n"
           "  TriangleButtonSprite: {fileID: 0}\n  CircleButtonSprite: {fileID: 0}\n  CrossButtonSprite: {fileID: 0}\n"
           "  SquareButtonSprite: {fileID: 0}\n  YButtonSprite: {fileID: 0}\n  BButtonSprite: {fileID: 0}\n"
           "  AButtonSprite: {fileID: 0}\n  XButtonSprite: {fileID: 0}\n"
           f"  canvasGroup: {ref(ctx.c(group_key, 'cg'))}\n"))
    return Inst(key, "KitButtonSmall", name, r, K.button_mods("KitButtonSmall", variant, opaque, caption, small=True), added=[cbp])


def small(key, name, variant, caption, r, opaque=False):
    return kit_button(key, name, variant, caption, r, opaque=opaque, small=True)


def panel(parent, key, name, r, eyebrow, eyebrow_token):
    n = parent.child(N(key, name, r))
    frame(n, f"{key}/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.9)
    M.bracket_nodes(n, f"{key}/Bracket")
    section_label(n, f"{key}/Eyebrow", "Eyebrow", eyebrow, TL(24, 20, 400, 18), token=eyebrow_token)
    return n


def stat(parent, key, caption, token, x):
    label(parent.child(N(f"{key}Label", f"{key.split('/')[-1]}Label", TL(x, 22, 280, 16))), caption, "Label", token)
    label(parent.child(N(f"{key}Value", f"{key.split('/')[-1]}Value", TL(x, 44, 280, 40))), "0", "H1", "TextLight")


def tile_nodes(parent, key, sliver=10, raycast=True):
    A.ring_node(parent, f"{key}/FocusRing", sliver)
    A.frame_nodes(parent, f"{key}/Frame", sliver, "SurfaceVeryDark", "SurfaceDark", raycast=raycast, themed_paint=False)


RECORD_TINT = 0.6


def build_record(root):
    W_, X2 = 1000, 1060
    # The record's panels, laid out from the top of the canvas (the Record rect starts under the nav at 84).
    top = lambda y: y - 84
    rec = root.child(N("Record", "Record", R((0, 0), (1, 1), (0, -42), (0, -84))))
    rec.add("cg", 225, canvas_group(False))
    # Favourites
    fav = panel(rec, "Record/Favourites", "Favourites", TL(40, top(376), W_, 320), "Favourites", "NeutralLightest")
    fv = fav.child(N("Record/Favourites/Vessel", "Vessel", TL(24, 52, 460, 248)))
    frame(fv, "Record/Favourites/Vessel/Frame", 10, "SurfaceBlack", "SurfaceDark", fill_alpha=0.6)
    label(fv.child(N("Record/Favourites/Vessel/Caption", "Caption", TL(20, 18, 300, 16))), "Favourite vessel", "Label", "NeutralLightest")
    label(fv.child(N("Record/Favourites/Vessel/Name", "Name", TL(20, 40, 420, 40))), "", "H2", "TextLight")
    label(fv.child(N("Record/Favourites/Vessel/Line", "Line", TL(20, 84, 260, 44))), "", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)
    # Its records, from the same hangar entry (the owner, 2026-10-18: "the UI seems too empty").
    for i, (key, cap) in enumerate(FAV_STATS):
        x, y = 20 + (i % 2) * 130, 136 + (i // 2) * 54
        label(fv.child(N(f"Record/Favourites/Vessel/{key}Label", f"{key}Label", TL(x, y, 120, 16))), cap, "Label", "NeutralLightest")
        label(fv.child(N(f"Record/Favourites/Vessel/{key}Value", f"{key}Value", TL(x, y + 18, 120, 28))), "-", "Data", "TextLight")
    art_node(fv, "Record/Favourites/Vessel/Icon", "Icon", R((1, 0), None, (-20, 20), (150, 150), (1, 0)), None, preserve=True)
    fp = fav.child(N("Record/Favourites/Pilot", "Pilot", TL(516, 52, 460, 248)))
    frame(fp, "Record/Favourites/Pilot/Frame", 10, "SurfaceBlack", "SurfaceDark", fill_alpha=0.6)
    label(fp.child(N("Record/Favourites/Pilot/Caption", "Caption", TL(20, 18, 300, 16))), "Favourite pilot", "Label", "NeutralLightest")
    label(fp.child(N("Record/Favourites/Pilot/Name", "Name", TL(20, 40, 280, 40))), "", "H2", "TextLight")
    label(fp.child(N("Record/Favourites/Pilot/Line", "Line", TL(20, 84, 260, 44))), "", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)
    pr = fp.child(N("Record/Favourites/Pilot/Portrait", "Portrait", R((1, 0), None, (-20, 20), (140, 140), (1, 0))))
    image_node(pr, "Record/Favourites/Pilot/Portrait/Ring", "Ring", STRETCH, "NeutralLightest", K.kit_sprite("kit_oct_fill"))
    image_node(pr, "Record/Favourites/Pilot/Portrait/Back", "Back", R((0, 0), (1, 1), (0, 0), (-6, -6)), "SurfaceDark", K.kit_sprite("kit_oct_fill"))
    art_node(pr, "Record/Favourites/Pilot/Portrait/Image", "Image", R((0, 0), (1, 1), (0, 0), (-10, -10)), None, preserve=True)
    w, h = K.button_size("CHANGE", small=True)
    fp.children.append(small("ChangePilot", "ChangePilot", "Default", "CHANGE", R((0, 0), None, (20, 20), (w, h), (0, 0))))

    # Fly time by vessel
    by = panel(rec, "Record/ByVessel", "ByVessel", TL(40, top(714), W_, 326), "Fly time by vessel", "NeutralLightest")
    for i in range(VESSEL_ROWS):
        k = f"Record/ByVessel/Row{i}"
        row = by.child(N(k, f"Row{i}", TL(24, 52 + i * VESSEL_PITCH, W_ - 48, VESSEL_ROW_H)))
        row.add("cg", 225, canvas_group(False))
        art_node(row, f"{k}/Icon", "Icon", R((0, 0.5), None, (16, 0), (28, 28)), None, preserve=True)
        label(row.child(N(f"{k}/Name", "Name", R((0, 0.5), None, (52, 0), (200, 26), (0, 0.5)))), "", "Body", "TextLight")
        track = row.child(N(f"{k}/Track", "Track", R((0, 0.5), (1, 0.5), (110, 0), (-420, 6))))
        track.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
        fill = track.child(N(f"{k}/Track/Fill", "Fill", R((0, 0), (0.5, 1), (0, 0), (0, 0), (0, 0.5))))
        fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive")))
        label(row.child(N(f"{k}/Time", "Time", R((1, 0.5), None, (0, 0), (160, 26), (1, 0.5)))), "", "Data", "TextLight", align="right")
    label(by.child(N("Record/ByVessel/Empty", "Empty", TL(24, 60, W_ - 48, 24))), "", "Body", "NeutralLightest")

    build_studio(rec, X2, top(376))
    build_picker(rec)
    rec.add("record", 114, lambda ctx, go: mono(go, S["record"], record_body(ctx)))


def build_studio(rec, x, y):
    k0 = "Record/Studio"
    st = panel(rec, k0, "Studio", TL(x, y, 820, 664), "Audio Studio", "NeutralLightest")
    label(st.child(N(f"{k0}/Count", "Count", TR(24, 20, 360, 18))), "", "Label", "NeutralLightest", align="right")
    label(st.child(N(f"{k0}/Title", "Title", TL(24, 52, 750, 40))), "", "H2", "TextLight")
    label(st.child(N(f"{k0}/Artist", "Artist", TL(24, 94, 750, 20))), "", "BodySmall", "NeutralLightest")
    tr = st.child(N(f"{k0}/Progress", "Progress", TL(24, 126, 752, 3)))
    tr.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    pf = tr.child(N(f"{k0}/Progress/Fill", "Fill", R((0, 0), (0, 1), (0, 0), (0, 0), (0, 0.5))))
    pf.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))).add("th", 114, H.themed("ControlActive"))
    row = st.child(N(f"{k0}/Transport", "Transport", TL(24, 146, 752, 40)))
    # Each button is sized for the longest word AudioStudioPanel can put on it (Shuffle: on / off, Start / Stop music),
    # not its first caption, and the row controls the widths - sized for the caption, they overlapped (2026-10-18).
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 3, (1, 0), (0, 0)))
    for key, cap, variant, words in (("Back", "BACK", "Default", ()), ("Pause", "PAUSE", "Default", ("PLAY",)),
                                     ("Skip", "SKIP", "Default", ()), ("Shuffle", "SHUFFLE", "Default", ("SHUFFLE: ON", "SHUFFLE: OFF")),
                                     ("Stop", "STOP MUSIC", "Cancel", ("START MUSIC",))):
        w = max(K.button_size(x, small=True)[0] for x in (cap,) + words) + 8
        h = K.button_size(cap, small=True)[1]
        holder = row.child(N(f"{k0}/Transport/{key}", key, R((0, 0.5), None, (0, 0), (w, h), (0, 0.5))))
        holder.add("le", 114, le(pw=w, ph=h))
        holder.children.append(small(f"Studio{key}", f"{key}Button", variant, cap, STRETCH, opaque=key == "Pause"))
    label(st.child(N(f"{k0}/Notice", "Notice", TL(24, 198, 752, 40))),
          "Picking tracks arrives with the next music update. Until then the soundtrack plays at random.",
          "BodySmall", "NeutralLightest", valign="top", wrap=True)
    for i in range(len(TRACKS)):
        k = f"{k0}/Row{i}"
        r = st.child(N(k, f"Row{i}", TL(24, 250 + i * STUDIO_PITCH, 772, STUDIO_ROW_H)))
        r.add("cg", 225, canvas_group(True))
        tile_nodes(r, k)
        tick = r.child(N(f"{k}/Tick", "Tick", R((0, 0.5), None, (30, 0), (26, 26))))
        image_node(tick, f"{k}/Tick/Box", "Box", STRETCH, "NeutralLightest", K.kit_sprite("kit_sliver10_line1"), sliced=True, raycast=True)
        mark = image_node(tick, f"{k}/Tick/Mark", "Mark", R((0.5, 0.5), None, (0, 0), (18, 18)), "Cta",
                          K.chrome_sprite("chrome_confirm"), preserve=True)
        tick.add("button", 114, C.plain_button(c(f"{k}/Tick/Box", "img")))
        label(r.child(N(f"{k}/Number", "Number", R((0, 0.5), None, (64, 0), (40, 24), (0, 0.5)))), f"{i + 1:02d}", "Data", "NeutralLightest")
        label(r.child(N(f"{k}/Title", "Title", R((0, 0.5), (1, 0.5), (110, 0), (-240, 26), (0, 0.5)))), TRACKS[i][1], "Body", "TextLight")
        label(r.child(N(f"{k}/Playing", "Playing", R((1, 0.5), None, (-20, 0), (140, 18), (1, 0.5)), active=False)),
              "Playing", "Label", "ControlActive", align="right")
        r.add("tile", 114, A.tile_body(c(f"{k}/FocusRing", "img"), c(f"{k}/Frame/Fill", "img"), c(f"{k}/Frame/Border", "img"), 10,
                                       primary=[c(f"{k}/Title", "tmp")]))
    st.add("studio", 114, lambda ctx, go: mono(go, S["studio"], studio_body(ctx)))


def build_picker(rec):
    k0 = "Record/Picker"
    pk = rec.child(N(k0, "Picker", STRETCH))
    pk.add("cg", 225, canvas_group(False))
    image_node(pk, f"{k0}/Solid", "Solid", STRETCH, "SurfaceBlack", raycast=True)
    image_node(pk, f"{k0}/Tint", "Tint", STRETCH, "SurfaceDark", alpha=RECORD_TINT)
    pk.child(M.window_sea_node(f"{k0}/Lattice", name="Sea"))
    label(pk.child(N(f"{k0}/Eyebrow", "Eyebrow", R((0.5, 1), None, (0, -90), (600, 18)))), "Profile", "Label", "NeutralLightest", align="center")
    label(pk.child(N(f"{k0}/Title", "Title", R((0.5, 1), None, (0, -126), (900, 52)))), "Favourite pilot", "H1", "TextLight", align="center")
    vs = pk.child(N(f"{k0}/Vessel", "Vessel", R((0.5, 1), None, (0, -196), (700, 44))))
    label(vs.child(N(f"{k0}/Vessel/Name", "Name", R((0.5, 0.5), None, (0, 0), (360, 36)))), "", "H2", "TextLight", align="center")
    for key, cap, pad, ax in (("Prev", "LB", PAD_LB, 0), ("Next", "RB", PAD_RB, 1)):
        w, h = K.button_size(cap, small=True)
        vs.children.append(pad_button(f"Picker{key}", f"{key}Button", "Default", cap,
                                      R((ax, 0.5), None, (0, 0), (w, h), (ax, 0.5)), pad, k0))
    tw, th, gap = 300, 340, 40
    x0 = -(PILOT_TILES * tw + (PILOT_TILES - 1) * gap) // 2
    for i in range(PILOT_TILES):
        k = f"{k0}/Tile{i}"
        t = pk.child(N(k, f"Tile{i}", R((0.5, 0.5), None, (x0 + i * (tw + gap) + tw // 2, 10), (tw, th))))
        t.add("cg", 225, canvas_group(True))
        tile_nodes(t, k, 14)
        po = t.child(N(f"{k}/Portrait", "Portrait", R((0.5, 1), None, (0, -24), (150, 150), (0.5, 1))))
        image_node(po, f"{k}/Portrait/Back", "Back", STRETCH, "SurfaceDark", K.kit_sprite("kit_oct_fill"))
        art_node(po, f"{k}/Portrait/Image", "Image", R((0, 0), (1, 1), (0, 0), (-8, -8)), None, preserve=True)
        label(t.child(N(f"{k}/Name", "Name", R((0.5, 1), None, (0, -196), (tw - 30, 34), (0.5, 1)))), "", "H2", "TextLight", align="center")
        label(t.child(N(f"{k}/Line", "Line", R((0.5, 1), None, (0, -236), (tw - 30, 20), (0.5, 1)))), "", "BodySmall", "NeutralLightest", align="center")
        label(t.child(N(f"{k}/Favourite", "Favourite", R((0.5, 1), None, (0, -270), (tw - 30, 18), (0.5, 1)), active=False)),
              "Favourite", "Label", "NeutralLightest", align="center")
        t.add("tile", 114, A.tile_body(c(f"{k}/FocusRing", "img"), c(f"{k}/Frame/Fill", "img"), c(f"{k}/Frame/Border", "img"), 14,
                                       primary=[c(f"{k}/Name", "tmp")]))
    btns = pk.child(N(f"{k0}/Buttons", "Buttons", R((0.5, 0.5), None, (0, -230), (520, 40))))
    btns.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 16, 4, (0, 0), (0, 0)))
    for key, cap, variant in (("Set", "SET AS FAVOURITE", "Cta"), ("Back", "BACK", "Cancel")):
        w, h = K.button_size(cap, small=True)
        holder = btns.child(N(f"{k0}/Buttons/{key}", key, R((0, 0.5), None, (0, 0), (w, h), (0, 0.5))))
        holder.add("le", 114, le(pw=w, ph=h))
        holder.children.append(small(f"Picker{key}Button", f"{key}Button", variant, cap, STRETCH, opaque=key == "Set"))
    pk.add("picker", 114, lambda ctx, go: mono(go, S["picker"], picker_body(ctx)))


def record_body(ctx):
    rows = "".join(
        f"  - group: {ref(ctx.c(f'Record/ByVessel/Row{i}', 'cg'))}\n"
        f"    icon: {ref(ctx.c(f'Record/ByVessel/Row{i}/Icon', 'img'))}\n"
        f"    nameText: {ref(ctx.c(f'Record/ByVessel/Row{i}/Name', 'tmp'))}\n"
        f"    fill: {ref(ctx.rt(f'Record/ByVessel/Row{i}/Track/Fill'))}\n"
        f"    fillImage: {ref(ctx.c(f'Record/ByVessel/Row{i}/Track/Fill', 'img'))}\n"
        f"    timeText: {ref(ctx.c(f'Record/ByVessel/Row{i}/Time', 'tmp'))}\n" for i in range(VESSEL_ROWS))
    return (f"  group: {ref(ctx.c('Record', 'cg'))}\n"
            "  closeButton: {fileID: 0}\n"
            f"  picker: {ref(ctx.c('Record/Picker', 'picker'))}\n"
            f"  vessels: {asset_ref(VESSEL_LIST)}\n  captains: {asset_ref(CAPTAIN_LIST)}\n"
            f"  vesselBarTint: {K.TOKEN_INDEX['ControlActive']}\n"
            "  nameText: {fileID: 0}\n  flightTimeText: {fileID: 0}\n  gamesText: {fileID: 0}\n  sessionsText: {fileID: 0}\n"
            f"  sinceText: {ref(ctx.c('Pilot/SinceValue', 'tmp'))}\n"
            f"  favouriteVesselName: {ref(ctx.c('Record/Favourites/Vessel/Name', 'tmp'))}\n"
            f"  favouriteVesselLine: {ref(ctx.c('Record/Favourites/Vessel/Line', 'tmp'))}\n"
            f"  favouriteVesselIcon: {ref(ctx.c('Record/Favourites/Vessel/Icon', 'img'))}\n"
            + "".join(f"  favouriteVessel{key}: {ref(ctx.c(f'Record/Favourites/Vessel/{key}Value', 'tmp'))}\n"
                      for key, _ in FAV_STATS)
            + f"  favouritePilotName: {ref(ctx.c('Record/Favourites/Pilot/Name', 'tmp'))}\n"
            f"  favouritePilotLine: {ref(ctx.c('Record/Favourites/Pilot/Line', 'tmp'))}\n"
            f"  favouritePilotPortrait: {ref(ctx.c('Record/Favourites/Pilot/Portrait/Image', 'img'))}\n"
            f"  changePilotButton: {ref(ctx.iref('ChangePilot', 'root#button'))}\n"
            "  rows:\n" + rows
            + f"  noFlightLine: {ref(ctx.c('Record/ByVessel/Empty', 'tmp'))}\n")


def studio_body(ctx):
    k0 = "Record/Studio"
    rows = "".join(
        f"  - group: {ref(ctx.c(f'{k0}/Row{i}', 'cg'))}\n"
        f"    tile: {ref(ctx.c(f'{k0}/Row{i}', 'tile'))}\n"
        f"    titleText: {ref(ctx.c(f'{k0}/Row{i}/Title', 'tmp'))}\n"
        f"    tickButton: {ref(ctx.c(f'{k0}/Row{i}/Tick', 'button'))}\n"
        f"    tickMark: {ref(ctx.go(f'{k0}/Row{i}/Tick/Mark'))}\n"
        f"    playingMark: {ref(ctx.go(f'{k0}/Row{i}/Playing'))}\n" for i in range(len(TRACKS)))
    btn = lambda f, key: f"  {f}: {ref(ctx.iref(f'Studio{key}', 'root#button'))}\n"
    lab_ = lambda f, key: f"  {f}: {ref(ctx.iref(f'Studio{key}', 'Label#tmp'))}\n"
    return (f"  titleText: {ref(ctx.c(f'{k0}/Title', 'tmp'))}\n  artistText: {ref(ctx.c(f'{k0}/Artist', 'tmp'))}\n"
            f"  progressFill: {ref(ctx.rt(f'{k0}/Progress/Fill'))}\n  progressTrack: {ref(ctx.go(f'{k0}/Progress'))}\n"
            + btn("backButton", "Back") + btn("pauseButton", "Pause") + lab_("pauseLabel", "Pause") + btn("skipButton", "Skip")
            + btn("shuffleButton", "Shuffle") + lab_("shuffleLabel", "Shuffle") + btn("stopButton", "Stop") + lab_("stopLabel", "Stop")
            + "  rows:\n" + rows
            + f"  countText: {ref(ctx.c(f'{k0}/Count', 'tmp'))}\n  randomNotice: {ref(ctx.go(f'{k0}/Notice'))}\n"
            f"  visibleGroup: {ref(ctx.c('Record', 'cg'))}\n")


def picker_body(ctx):
    k0 = "Record/Picker"
    tiles = "".join(
        f"  - group: {ref(ctx.c(f'{k0}/Tile{i}', 'cg'))}\n"
        f"    tile: {ref(ctx.c(f'{k0}/Tile{i}', 'tile'))}\n"
        f"    portrait: {ref(ctx.c(f'{k0}/Tile{i}/Portrait/Image', 'img'))}\n"
        f"    nameText: {ref(ctx.c(f'{k0}/Tile{i}/Name', 'tmp'))}\n"
        f"    lineText: {ref(ctx.c(f'{k0}/Tile{i}/Line', 'tmp'))}\n"
        f"    favouriteMark: {ref(ctx.go(f'{k0}/Tile{i}/Favourite'))}\n" for i in range(PILOT_TILES))
    return (f"  group: {ref(ctx.c(k0, 'cg'))}\n  captains: {asset_ref(CAPTAIN_LIST)}\n"
            f"  record: {ref(ctx.c('Record', 'record'))}\n"
            f"  vesselText: {ref(ctx.c(f'{k0}/Vessel/Name', 'tmp'))}\n"
            f"  previousButton: {ref(ctx.iref('PickerPrev', 'root#button'))}\n"
            f"  nextButton: {ref(ctx.iref('PickerNext', 'root#button'))}\n"
            "  tiles:\n" + tiles
            + f"  setButton: {ref(ctx.iref('PickerSetButton', 'root#button'))}\n"
            f"  backButton: {ref(ctx.iref('PickerBackButton', 'root#button'))}\n")


# --------------------------------------------------------------------------------------------
# Rev 10: the now-playing card (its own prefab, mounted in Menu_Main's safe area)
# --------------------------------------------------------------------------------------------

def build_now_playing():
    root = N("root", "NowPlayingCard", R((1, 0), None, (-40, 40), (440, 150), (1, 0)))
    pn = root.child(N("Panel", "Panel", STRETCH))
    # The hover reaches NowPlayingCard on the root through this transparent plate, which slides with the panel and
    # stops catching clicks once the card hides (the panel's CanvasGroup stops blocking raycasts).
    pn.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    pn.add("cg", 225, canvas_group(True))
    frame(pn, "Panel/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    image_node(pn, "Panel/Disc", "Disc", TL(18, 18, 56, 56), "SurfaceLight", K.kit_sprite("kit_oct_fill"))
    image_node(pn, "Panel/DiscCore", "DiscCore", TL(21, 21, 50, 50), "SurfaceBlack", K.kit_sprite("kit_oct_fill"))
    eq = image_node(pn, "Panel/Eq", "Eq", TL(32, 32, 28, 28), "TextLight", K.chrome_sprite("chrome_gauge"), preserve=True)
    eq.add("amb", 114, M.ambient("Breathe", period=1.1, amplitude=0.45))
    label(pn.child(N("Panel/Eyebrow", "Eyebrow", TL(90, 18, 330, 14))), "Now playing", "Label", "NeutralLightest")
    label(pn.child(N("Panel/Title", "Title", TL(90, 36, 330, 28))), "", "H3", "TextLight")
    label(pn.child(N("Panel/Artist", "Artist", TL(90, 66, 330, 18))), "", "BodySmall", "NeutralLightest")
    tr = pn.child(N("Panel/Progress", "Progress", TL(18, 92, 404, 2)))
    tr.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    pf = tr.child(N("Panel/Progress/Fill", "Fill", R((0, 0), (0, 1), (0, 0), (0, 0), (0, 0.5))))
    pf.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))).add("th", 114, H.themed("ControlActive"))
    ctl = pn.child(N("Panel/Controls", "Controls", TL(18, 104, 404, 36)))
    ctl.add("cg", 225, canvas_group(False))
    ctl.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 3, (0, 0), (0, 0)))
    for key, cap, variant in (("Back", "BACK", "Default"), ("Pause", "PAUSE", "Default"), ("Skip", "SKIP", "Default")):
        w, h = K.button_size(cap, small=True)
        holder = ctl.child(N(f"Panel/Controls/{key}", key, R((0, 0.5), None, (0, 0), (w, h), (0, 0.5))))
        holder.add("le", 114, le(pw=w, ph=h))
        holder.children.append(small(f"Card{key}", f"{key}Button", variant, cap, STRETCH, opaque=key == "Pause"))
    root.add("card", 114, lambda ctx, go: mono(go, S["nowplaying"],
             f"  panel: {ref(ctx.rt('Panel'))}\n  group: {ref(ctx.c('Panel', 'cg'))}\n"
             f"  titleText: {ref(ctx.c('Panel/Title', 'tmp'))}\n  artistText: {ref(ctx.c('Panel/Artist', 'tmp'))}\n"
             f"  progressFill: {ref(ctx.rt('Panel/Progress/Fill'))}\n  progressTrack: {ref(ctx.go('Panel/Progress'))}\n"
             f"  controls: {ref(ctx.c('Panel/Controls', 'cg'))}\n"
             f"  backButton: {ref(ctx.iref('CardBack', 'root#button'))}\n"
             f"  pauseButton: {ref(ctx.iref('CardPause', 'root#button'))}\n"
             f"  pauseLabel: {ref(ctx.iref('CardPause', 'Label#tmp'))}\n"
             f"  skipButton: {ref(ctx.iref('CardSkip', 'root#button'))}\n"
             "  restingOn:\n  - 2\n  toastOffScreens: 0\n"))
    return root


def add_sound_names(text):
    """Adds a missing `soundName` to each track of an existing soundtrack (after `onByDefault`), leaving every line the
    audio owner authored alone. A track whose soundName is already set keeps it."""
    def one(m):
        block = m.group(0)
        if "\n    soundName:" in block:
            return block
        name = SOUND_NAMES.get(m.group(1).strip())
        return block if name is None else re.sub(r"(\n    onByDefault: \d\n)", r"\1    soundName: " + name.replace("\\", "") + "\n", block, count=1)
    return re.sub(r"  - id: (.+)\n(?:    .*\n|      .*\n|        .*\n)*", one, text)


def soundtrack_asset():
    rows = "".join(
        f"  - id: {tid}\n    title: {title}\n    artist: \n"
        "    trackEvent:\n      Guid:\n        Data1: 0\n        Data2: 0\n        Data3: 0\n        Data4: 0\n      Path: \n"
        "    onByDefault: 1\n"
        f"    soundName: {SOUND_NAMES[tid]}\n" for tid, title in TRACKS)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {C.script('SoundtrackSO')}, type: 3}}\n"
            "  m_Name: Soundtrack\n  m_EditorClassIdentifier: \n  tracks:\n" + rows
            + "  artistLine: Cosmic Shore Original Soundtrack\n")


def locked_tile(body):
    def wrap(ctx, go):
        return body(ctx, go).replace("  isLocked: 0\n", "  isLocked: 1\n")
    return wrap


BUILDERS = {"QuestCard": build_quest_card, "ProfileView": build_view, "NowPlayingCard": build_now_playing}


# --------------------------------------------------------------------------------------------
# Prefab outputs, quest art (Q5)
# --------------------------------------------------------------------------------------------

def prefab_outputs():
    out = {}
    for name in PREFABS:
        out[prefab_path(name)] = prefab_text(BUILDERS[name](), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Profile"))
    return out


def asset_text(path):
    return open(os.path.join(REPO, path), encoding="utf-8").read()


def card_art_for_mode(mode):
    """The CardBackground sprite of the arcade card whose Mode is `mode`."""
    games = os.path.join(REPO, "Assets/_SO_Assets/Games")
    for f in sorted(os.listdir(games)):
        if not f.endswith(".asset"):
            continue
        t = open(os.path.join(games, f), encoding="utf-8").read()
        if re.search(r"^  Mode: " + str(mode) + r"$", t, re.M):
            m = re.search(r"^  CardBackground: (\{fileID: \d+, guid: [0-9a-f]{32}, type: 3\})", t, re.M)
            if m:
                return m.group(1)
    raise AssertionError(f"no arcade card with CardBackground for mode {mode}")


def quest_paths():
    out = []
    for g in re.findall(r"guid: ([0-9a-f]{32}), type: 2", asset_text(QUEST_LIST)):
        out.append(T.guid_path(g) if hasattr(T, "guid_path") else find_by_guid(g))
    return out


def find_by_guid(g):
    for root, _, files in os.walk(os.path.join(REPO, "Assets/_SO_Assets/GameModeQuest")):
        for f in files:
            if f.endswith(".asset.meta") and f"guid: {g}" in open(os.path.join(root, f)).read():
                return os.path.relpath(os.path.join(root, f[:-5]), REPO)
    raise AssertionError(f"quest {g} not found")


def quest_outputs():
    """Decision Q5: each quest's Icon is its mode's card art; the hangar's is the Squirrel render."""
    out = {}
    squirrel = re.search(r"^  PreviewImage: (\{fileID: \d+, guid: [0-9a-f]{32}, type: 3\})", asset_text(SQUIRREL_VESSEL), re.M)
    assert squirrel, "the Squirrel has no PreviewImage"
    for path in quest_paths():
        t = asset_text(path)
        kind = int(re.search(r"^  FeatureKind: (\d+)", t, re.M).group(1))
        mode = int(re.search(r"^  GameMode: (\d+)", t, re.M).group(1))
        art = card_art_for_mode(mode) if kind == 0 else squirrel.group(1)
        new = re.sub(r"^  Icon: \{[^}]*\}\n", f"  Icon: {art}\n", t, count=1, flags=re.M)
        out[path] = new
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

IDS = dict(root_go="8348156532203949861", screen="1697402598610725113", track="6149825079732421923",
           episodes="6984484221034217978", window="8675064058193410727", old_modal="2899345459383930630",
           episodes_panel_go="8894437194939472548",
           retired={"4075035317805227399": "Background", "7338261586984045584": "NamePanel",
                    "9092047784427285133": "AvatarDisplay", "2271212850603988334": "Scroll View",
                    "8063665426652658442": "EpisodeScreen"})
C.STUB_KIND.update({"ubutton": (114, A.BUTTON), "record": (114, S["record"])})
# Scene fields -> (object key in ProfileView, stub kind[, nested instance key])
SCREEN_REFS = {"displayNameText": ("Pilot/Name#tmp", "tmp"), "avatarImage": ("Pilot/Avatar/Icon#img", "img"),
               "gamesCompletedText": ("Pilot/GamesValue#tmp", "tmp"), "flightTimeText": ("Pilot/FlightValue#tmp", "tmp"),
               "sessionsText": ("Pilot/SessionsValue#tmp", "tmp"), "renameButton": ("Rename|root#button", "button"),
               "nameButton": ("Pilot/Name#button", "ubutton"),
               "recordWindow": ("Record#record", "record"), "backButton": ("Back|root#button", "button")}
SWITCHER = "4216411968520269688"            # Menu_Main's ScreenSwitcher (Back returns through it)
AVATAR_BUTTONS = (("Pilot/Avatar#button", "ubutton"), ("ChangeAvatar|root#button", "button"))
# Rev 13: the stubs the scene used to hold into ProfileView for the quest row, the Episodes card and EXPAND.
RECORD_UNUSED = ("closeButton", "nameText", "flightTimeText", "gamesText", "sessionsText")
RETIRED_REFS = ("Quests/Row#rt", "Quests/Bar#slider", "Quests/Summary#tmp", "Episodes#go", "Episodes#tile", "Expand|root#button")
# Rev 13: the quest track left the Profile for the Voyage window, which carries its own QuestTrackView
# (author_menu_mission.py). The scene's old one, on the ProfileScreen root, is removed.


class ViewMount(C.Mount):
    def __init__(self):
        self.view = "ProfileView"
        self.ctx = Ctx("Menu_Main/Profile")
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = asset_guid(self.view)
        self.kind = None

    def src(self, key):
        """The object's id INSIDE ProfileView: direct ("Node#comp"), or through one nested kit
        instance ("Inst|srcKey"), where Unity derives it as source XOR that instance."""
        V = K.FileIds("ProfileView")
        if "|" in key:
            inst, src_key = key.split("|")
            prefab = "KitButtonSmall"
            return C.derived(K.source_fid(prefab, src_key), V(inst + "#inst"))
        return V(key)

    def refs(self):
        return list(SCREEN_REFS.values()) + list(AVATAR_BUTTONS)

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", self.view, self.guid, self.view, STRETCH, mods), parent_rt, out)
        for key, comp in self.refs():
            out.append(self.stub_text(key, comp))
        return [K.unity_empty_scalars(d) for d in out]

    def ids(self):
        return {self.inst, self.stub} | {self.stub_fid(k) for k, _ in self.refs()}


def set_field(doc, name, value):
    return C.field_set(doc, name, value)


def insert_field_before(doc, before, line):
    name = line.split(":")[0].strip()
    doc = re.sub(r"^  " + re.escape(name) + r": .*\n", "", doc, flags=re.M)
    return re.sub(r"(^  " + re.escape(before) + r":)", line + r"\n\1", doc, count=1, flags=re.M)


SAFE_AREA_GO = "127559492761314651"         # Menu_Main's Safe Area (the NavBar's parent)
AUDIO_PREFAB = "Assets/_Prefabs/CORE/AudioSystem.prefab"
AUDIO_SYSTEM = "5246419679807688196"        # the AudioSystem component in it


class CardMount(ViewMount):
    def __init__(self):
        self.view = "NowPlayingCard"
        self.ctx = Ctx("Menu_Main/NowPlaying")
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = asset_guid(self.view)
        self.kind = None

    def refs(self):
        return []

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", self.view, self.guid, self.view, R((1, 0), None, (-40, 40), (440, 150), (1, 0)), mods),
                        parent_rt, out)
        return [K.unity_empty_scalars(d) for d in out]


def audio_transform(text):
    """AudioSystem.prefab gets the soundtrack (Rev 10): the Audio Studio's track list."""
    sc = T.Scene(text)
    assert sc.script(AUDIO_SYSTEM) == C.script("AudioSystem"), "AudioSystem.prefab's component moved"
    doc = sc.docs[AUDIO_SYSTEM]
    value = f"{{fileID: 11400000, guid: {SOUNDTRACK_GUID}, type: 2}}"
    if re.search(r"^  soundtrack:", doc, re.M):
        doc = set_field(doc, "soundtrack", value)
    else:
        # The prefab serializes few of the newer fields (Bootstrap overrides the music event), so the soundtrack
        # goes after the last field written.
        doc = doc.rstrip("\n") + f"\n  soundtrack: {value}\n"
    sc.replace(AUDIO_SYSTEM, doc)
    return sc.text()


def scene_transform(text):
    sc = T.Scene(text)
    assert sc.field(IDS["root_go"], "m_Name") == "ProfileScreen", "ProfileScreen moved"
    assert sc.script(IDS["screen"]) == S["screen"], "ProfileScreen moved"
    assert not sc.has(IDS["track"]) or sc.script(IDS["track"]) == S["track"], "the old QuestTrackView's id was reused"
    assert sc.script(IDS["episodes"]) == S["episodes"], "EpisodeScreen moved"
    mount = ViewMount()
    if any(sc.has(g) for g in IDS["retired"]):
        clash = mount.ids() & set(sc.order)
        assert not clash, f"minted fileIDs already present: {sorted(clash)[:3]}"
    for go, name in IDS["retired"].items():
        if sc.has(go):
            assert sc.field(go, "m_Name") == name, f"retired {go} is no longer {name}"
            for fid in dict.fromkeys(C.subtree(sc, go)):
                if sc.has(fid):
                    sc.remove(fid)
    retired_stubs = {mount.stub_fid(k) for k in RETIRED_REFS}
    for fid in list(sc.order):
        if fid in mount.ids() or fid in retired_stubs:
            sc.remove(fid)
    root_rt = sc.rect_of(IDS["root_go"])
    anchor = root_rt
    for d in mount.docs(root_rt):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    panel_rt = sc.rect_of(IDS["episodes_panel_go"])
    sc.replace(root_rt, T.set_block(sc.docs[root_rt], "m_Children", K.refs_list("m_Children", [mount.stub, panel_rt])))

    doc = sc.docs[IDS["screen"]]
    for field, (key, _) in SCREEN_REFS.items():
        if re.search(r"^  " + field + ":", doc, re.M):
            doc = set_field(doc, field, ref(mount.stub_fid(key)))
        else:
            doc = doc.rstrip("\n") + "\n" + f"  {field}: {ref(mount.stub_fid(key))}\n"
    doc = rebuild_screen_doc(doc, mount)
    sc.replace(IDS["screen"], doc)

    # Rev 13: the quest track moved to the Voyage window; the old QuestTrackView leaves the ProfileScreen root.
    if sc.has(IDS["track"]):
        go = IDS["root_go"]
        sc.replace(go, re.sub(r"^  - component: \{fileID: " + IDS["track"] + r"\}\n", "", sc.docs[go], flags=re.M))
        sc.remove(IDS["track"])

    # The Episodes card left the Profile too: EpisodeScreen refuses without a host of its own.
    doc = sc.docs[IDS["episodes"]]
    doc = insert_field_before(doc, "episodePanel", "  refusalHost: {fileID: 0}")
    sc.replace(IDS["episodes"], doc)

    # The now-playing card (Rev 10) is off the menu (Shombith, 2026-10-26: "Remove this man ASAP"): its instance leaves
    # Menu_Main's safe area. The prefab and NowPlayingCard stay for whoever brings a music surface back; the Audio Studio
    # on the Profile still plays and picks the soundtrack.
    card = CardMount()
    for fid in list(sc.order):
        if fid in card.ids():
            sc.remove(fid)
    safe_rt = sc.rect_of(SAFE_AREA_GO)
    kids = re.findall(r"\{fileID: (-?\d+)\}", (re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[safe_rt], re.M) or [None, ""])[1] or "")
    kids = [k for k in kids if k != card.stub]
    sc.replace(safe_rt, T.set_block(sc.docs[safe_rt], "m_Children", K.refs_list("m_Children", kids)))

    doc = sc.docs[IDS["old_modal"]]
    for field in ("profileIconImage", "profileNameLabel"):
        doc = set_field(doc, field, "{fileID: 0}")
    sc.replace(IDS["old_modal"], doc)
    return sc.text(), mount


SCREEN_ORDER = ("displayNameText", "avatarImage", "gamesCompletedText", "flightTimeText", "sessionsText",
                "profileWindow", "avatarButtons", "renameButton", "nameButton", "episodesButton", "episodes", "recordButton", "recordWindow",
                "backButton", "screenSwitcher")


def rebuild_screen_doc(doc, mount):
    """ProfileScreen's fields, in declaration order (what Unity would write)."""
    head = doc[:doc.index("  displayNameText:")]
    vals = {k: f" {ref(mount.stub_fid(v[0]))}" for k, v in SCREEN_REFS.items()}
    vals["profileWindow"] = f" {ref(IDS['window'])}"
    vals["episodes"] = f" {ref(IDS['episodes'])}"
    vals["episodesButton"] = vals["recordButton"] = " {fileID: 0}"
    vals["screenSwitcher"] = f" {ref(SWITCHER)}"
    body = ""
    for k in SCREEN_ORDER:
        if k == "avatarButtons":
            body += K.refs_list("avatarButtons", [mount.stub_fid(key) for key, _ in AVATAR_BUTTONS])
        else:
            body += f"  {k}:{vals[k]}\n"
    # Fields after ProfileScreen's references (its authored words) are kept as they are.
    tail = re.search(r"^  hoursTemplate:.*\n(?:  .*\n)*", doc, re.M)
    return head + body + (tail.group(0) if tail else "")


def outputs():
    out = prefab_outputs()
    out.update(quest_outputs())
    out[SCENE], _ = scene_transform(asset_text(SCENE))
    out[AUDIO_PREFAB] = audio_transform(asset_text(AUDIO_PREFAB))
    # Created once, then the asset is the audio owner's to edit (each track's event is wired in the inspector):
    # a re-run keeps what is on disk.
    st = os.path.join(REPO, SOUNDTRACK_ASSET)
    out[SOUNDTRACK_ASSET] = add_sound_names(open(st, encoding="utf-8").read()) if os.path.exists(st) else soundtrack_asset()
    out[SOUNDTRACK_ASSET + ".meta"] = ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                                      "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % SOUNDTRACK_GUID)
    out["Assets/_SO_Assets/Audio.meta"] = K.FOLDER_META.format(g=asset_guid("folder/SO_Audio"))
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def prefab_source(guid, files):
    for name in PREFABS:
        if asset_guid(name) == guid:
            return files[prefab_path(name)]
    for name in C.KIT_SOURCES:
        if K.prefab_guid(name) == guid:
            return open(os.path.join(REPO, K.PREFAB_DIR, name + ".prefab"), encoding="utf-8").read()
    raise AssertionError(f"no source prefab for guid {guid}")


def prefab_objects(guid, files, _memo=None):
    memo = {} if _memo is None else _memo
    if guid in memo:
        return memo[guid]
    text = prefab_source(guid, files)
    objs = A.anchors_of(text)
    for m in re.finditer(r"^--- !u!1001 &(-?\d+)\n(.*?)(?=^--- |\Z)", text, re.M | re.S):
        pid = int(m.group(1))
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", m.group(2)).group(1)
        objs |= {str(C.derived(o, pid)) for o in prefab_objects(src, files, memo)}
    memo[guid] = objs
    return objs


def check_sources(path, text, files):
    owned = {asset_guid(n) for n in PREFABS} | {K.prefab_guid(n) for n in C.KIT_SOURCES}
    for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                             r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", text):
        if g in owned:
            assert src in prefab_objects(g, files), f"{path}: source object {src} is not in the prefab {g}"


TMP, IMG = K.TMP_UGUI, K.IMAGE
CARD_FIELDS = dict(nameText=TMP, descriptionText=TMP, iconImage=IMG, claimButton=S["button"], cardButton=S["button"],
                   border=IMG, numeralText=S["kt"], stateText=S["kt"], titleText=S["kt"], art=IMG, lockGroup=225,
                   completedChip=225, goalsGroup=225, claimGroup=225, playGroup=225, meterText=S["kt"],
                   playLabel=S["kt"], lockedLineGroup=225, lockedLineText=S["kt"], node=IMG, segment=IMG,
                   segmentSpark=225, claimPop=C.S["entrance"])


def validate(files):
    guids = T.known_guids() | PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for path, text in files.items():
        if path.endswith(".meta") or path == SCENE or path.endswith(".asset"):
            continue
        A.check_file(path, text, guids, minted)
        check_sources(path, text, files)
    _, opener = A.doc_of(files[prefab_path("ProfileView")], K.FileIds("ProfileView")("Replays#open"))
    assert opener and OPEN_MODAL in opener and f"  target: {REPLAYS_MODAL}\n" in opener, \
        "ProfileView: MY REPLAYS must open ModalWindows.REPLAYS (19) through OpenModalButton"
    card = files[prefab_path("QuestCard")]
    _, body = A.doc_of(card, K.FileIds("QuestCard")("root#card"))
    assert body and S["card"] in body, "QuestCard: the root carries no KitQuestCard"
    for field, want in CARD_FIELDS.items():
        C.wired(card, body, field, want, "QuestCard")
    assert len(re.findall(r"^  - group: \{fileID: [1-9]", body, re.M)) == 2, "QuestCard needs its two goal rows"

    # Q5: every quest shows its mode's card art (the hangar: the Squirrel render).
    for path in quest_paths():
        t = files[path]
        assert "guid: 587d2203114c8004c9985d0112c89585" not in t and "guid: b5b9538f2e3a8c24d9a9f5219064fcdc" not in t, \
            f"{path}: still wears a legacy video still"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files)
    for go, name in IDS["retired"].items():
        assert not sc.has(go), f"ProfileScreen still holds retired {name}"
    mount = ViewMount()
    screen = sc.docs[IDS["screen"]]
    for field, (key, _) in SCREEN_REFS.items():
        assert f"  {field}: {{fileID: {mount.stub_fid(key)}}}" in screen, f"ProfileScreen.{field} does not point into the view"
    assert f"  profileWindow: {{fileID: {IDS['window']}}}" in screen, "ProfileScreen has no profile window"
    assert not sc.has(IDS["track"]), "the Profile still carries a QuestTrackView (Rev 13: the quest track is the Voyage window's)"
    assert re.search(r"^  (episodesButton|recordButton): \{fileID: 0\}\n(?:.*\n)*?  (episodesButton|recordButton): \{fileID: 0\}", screen, re.M), \
        "ProfileScreen still names the Episodes card or the EXPAND button"
    assert f"  screenSwitcher: {{fileID: {SWITCHER}}}" in screen, "ProfileScreen.screenSwitcher is not the scene's switcher (Back)"
    view = files[prefab_path("ProfileView")]
    for gone in ("m_Name: Episodes\n", "m_Name: Quests\n", "m_Name: Expand\n"):
        assert gone not in view, f"ProfileView still has {gone.split(': ')[1].strip()} (Rev 13)"
    validate_rev10(files, sc)


def validate_rev10(files, sc):
    """Rev 10 (section 56): the pilot record window, the Audio Studio, the pilot picker, the now-playing card."""
    view = files[prefab_path("ProfileView")]
    V = K.FileIds("ProfileView")
    for key, script, name in (("Record#record", S["record"], "ProfileRecordWindow"), ("Record/Studio#studio", S["studio"], "AudioStudioPanel"),
                              ("Record/Picker#picker", S["picker"], "FavouritePilotPicker")):
        _, body = A.doc_of(view, V(key))
        assert body and script in body, f"ProfileView: no {name}"
        for m in re.finditer(r"^  (?!m_)(\w+): \{fileID: 0\}", body, re.M):
            # Rev 13: the record is the profile's body - its own close, name and stat lines are the screen's now.
            if name == "ProfileRecordWindow" and m.group(1) in RECORD_UNUSED:
                continue
            raise AssertionError(f"{name}.{m.group(1)} is not wired")
    for key in ("Record#cg", "Record/Picker#cg"):
        _, cg = A.doc_of(view, V(key))
        assert "  m_Alpha: 0\n" in cg and "  m_BlocksRaycasts: 0\n" in cg, f"ProfileView: {key.split('#')[0]} ships shown (it would cover the profile)"
    _, sea = A.doc_of(view, V("VeilSea#img"))
    assert sea and f"guid: {M.SEA_WINDOW_MAT}" in sea, "the profile has no sea lines behind it"
    _, studio = A.doc_of(view, V("Record/Studio#studio"))
    assert studio.count("    tile: {fileID: ") == len(TRACKS), f"the Audio Studio needs {len(TRACKS)} track rows"
    _, rec = A.doc_of(view, V("Record#record"))
    assert rec.count("    fill: {fileID: ") == VESSEL_ROWS, f"the pilot record needs {VESSEL_ROWS} vessel rows"
    for path in (VESSEL_LIST, CAPTAIN_LIST):
        assert f"guid: {K.meta_guid(path)}" in rec, f"ProfileRecordWindow does not read {path}"
    # Rev 13: the record is the profile's body; ProfileScreen opens it when the screen comes on.
    mount = ViewMount()
    screen = sc.docs[IDS["screen"]]
    assert f"  recordWindow: {{fileID: {mount.stub_fid('Record#record')}}}" in screen, "ProfileScreen.recordWindow is not the record window"
    # The now-playing card is off the menu (2026-10-26): no instance of it in Menu_Main, nothing in the safe area naming it.
    card = CardMount()
    safe = sc.docs[sc.rect_of(SAFE_AREA_GO)]
    kids = re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", safe, re.M).group(1))
    assert card.stub not in kids and not sc.has(card.inst), "the now-playing card is back in Menu_Main"
    assert f"guid: {asset_guid('NowPlayingCard')}" not in "".join(sc.docs.values()), "Menu_Main still instances the NowPlayingCard prefab"
    cardp = files[prefab_path("NowPlayingCard")]
    _, cb = A.doc_of(cardp, K.FileIds("NowPlayingCard")("root#card"))
    assert cb and S["nowplaying"] in cb and not re.search(r"^  (?!m_)\w+: \{fileID: 0\}", cb, re.M), "NowPlayingCard is not wired"
    # The owner, 2026-10-18: the card shows on the Profile tab only, with no pop-ups elsewhere.
    assert "  restingOn:\n  - 2\n" in cb and "  toastOffScreens: 0\n" in cb, "the now-playing card does not rest on Home (Rev 13: the Profile carries the Audio Studio)"
    # Audio
    audio = files[AUDIO_PREFAB]
    assert f"  soundtrack: {{fileID: 11400000, guid: {SOUNDTRACK_GUID}, type: 2}}" in audio, "AudioSystem has no soundtrack"
    st = files[SOUNDTRACK_ASSET]
    ids = re.findall(r"^  - id: (.+)$", st, re.M)
    assert ids and len(ids) == len(set(ids)), "the soundtrack's track ids are missing or repeat (they key the player's ticks)"
    # Song names while the random event plays: every track names its audio file, and each names a file that is really
    # in the FMOD project's Music event (checked when the FMOD project is in the checkout).
    sounds = re.findall(r"^    soundName: (.*)$", st, re.M)
    assert len(sounds) == len(ids) and all(s.strip() for s in sounds), \
        "every soundtrack track needs a soundName (its audio file in event:/Music/Music), or its title never shows"
    fmod = fmod_music_sounds()
    if fmod is not None:
        for s in sounds:
            assert os.path.splitext(s.strip())[0].lower() in fmod, \
                f"soundName '{s.strip()}' is not an audio file in the FMOD Music event ({sorted(fmod)})"


def drift(files):
    stale = []
    for path, text in files.items():
        full = os.path.join(REPO, path)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != text:
            stale.append(path)
    return stale


def _show(text_, key):
    fid = K.FileIds("ProfileView")(key)
    i = text_.index(f" &{fid}\n")
    j = text_.find("\n--- ", i)
    return text_[:i] + text_[i:j].replace("  m_Alpha: 0\n", "  m_Alpha: 1\n").replace("  m_BlocksRaycasts: 0\n", "  m_BlocksRaycasts: 1\n") + text_[j:]


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
    card = prefab_path("QuestCard")
    first = quest_paths()[1]
    cases = [
        ("an unwired claim button", lambda f: f.__setitem__(card, re.sub(r"(  claimButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[card]))),
        ("a quest back on the legacy still", lambda f: f.__setitem__(first, re.sub(r"^  Icon: .*$",
            "  Icon: {fileID: 21300000, guid: 587d2203114c8004c9985d0112c89585, type: 3}", f[first], flags=re.M))),
        ("the Profile's Back unwired", lambda f: f.__setitem__(SCENE, T.in_doc(f[SCENE], IDS["screen"],
            f"  screenSwitcher: {{fileID: {SWITCHER}}}", "  screenSwitcher: {fileID: 0}"))),
        ("the pilot record ships shown", lambda f: f.__setitem__(prefab_path("ProfileView"), _show(f[prefab_path("ProfileView")], "Record#cg"))),
        ("the Audio Studio's skip unwired", lambda f: f.__setitem__(prefab_path("ProfileView"),
            re.sub(r"(  skipButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[prefab_path("ProfileView")]))),
        ("MY REPLAYS opening the wrong window", lambda f: f.__setitem__(prefab_path("ProfileView"),
            f[prefab_path("ProfileView")].replace(f"  target: {REPLAYS_MODAL}\n", "  target: 18\n", 1))),
        ("the profile without its sea lines", lambda f: f.__setitem__(prefab_path("ProfileView"),
            f[prefab_path("ProfileView")].replace(M.SEA_WINDOW_MAT, "0" * 32))),
        ("the Episodes card back on the Profile", lambda f: f.__setitem__(prefab_path("ProfileView"),
            f[prefab_path("ProfileView")] + "  m_Name: Episodes\n")),
        ("AudioSystem without the soundtrack", lambda f: f.__setitem__(AUDIO_PREFAB, re.sub(r"  soundtrack: .*\n", "", f[AUDIO_PREFAB]))),
        ("a track without its sound name", lambda f: f.__setitem__(SOUNDTRACK_ASSET, f[SOUNDTRACK_ASSET].replace("    soundName: SKY FIRE\n", "", 1))),
        ("a sound name that is not in the FMOD event", lambda f: f.__setitem__(SOUNDTRACK_ASSET, f[SOUNDTRACK_ASSET].replace("soundName: SEVERITY", "soundName: SEVERITY 2", 1))),
        ("two tracks sharing an id", lambda f: f.__setitem__(SOUNDTRACK_ASSET, f[SOUNDTRACK_ASSET].replace("id: sky-fire", "id: space-gliding"))),
        ("the now-playing card mounted in Menu_Main again", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            "  m_SourcePrefab: {fileID: 100100000, guid: ", f"  m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid('NowPlayingCard')}, type: 3}}\n  m_X: {{fileID: 100100000, guid: ", 1))),
        ("the card rests on Home again", lambda f: f.__setitem__(prefab_path("NowPlayingCard"),
            f[prefab_path("NowPlayingCard")].replace("  restingOn:\n  - 2\n", "  restingOn:\n  - 5\n"))),
        ("a caption that breaks the YAML", lambda f: f.__setitem__(prefab_path("ProfileView"),
            f[prefab_path("ProfileView")].replace("      value: SHUFFLE\n", "      value: SHUFFLE: ON\n", 1))),
        ("a dangling profile reference", lambda f: f.__setitem__(SCENE, f[SCENE].replace("  profileIconImage: {fileID: 0}",
                                                                                          "  profileIconImage: {fileID: 5397632936483963836}", 1))),
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
    print(f"OK: {len(PREFABS)} profile prefabs, the quest art and Menu_Main validate")
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
