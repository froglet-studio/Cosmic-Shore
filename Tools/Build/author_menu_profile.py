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

PREFABS = ("QuestCard", "ProfileView")

S = dict(
    card=C.script("KitQuestCard"), screen=C.script("ProfileScreen"), track=C.script("QuestTrackView"),
    episodes=C.script("EpisodeScreen"), affordance=C.script("CommerceAffordance"),
    availability=C.script("MenuAvailabilityView"), crystals=C.script("CrystalCurrencyDisplay"),
    modal=C.script("ProfileModal"), tile=K.kit_script("KitTile"), button=K.kit_script("KitButton"),
    kt=K.kit_script("KitText"), cover=K.kit_script("KitCoverImage"),
)
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


def build_quest_card():
    root = N("root", "QuestCard", R(size=(CARD_W, CARD_H)))
    root.add("cg", 225, canvas_group(True)).add("le", 114, le(mw=0, fw=1, ph=CARD_H))
    root.add("in", 114, M.entrance("Rise", by_sibling=True))

    # The track: the segment back to the previous card's node, then this card's node.
    seg = image_node(root, "Segment", "Segment", TL(-(CARD_W + GAP) // 2, 15, CARD_W + GAP, 2), "SurfaceDark")
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

    clip = body.child(N("Body/Art", "Art", TL(2, 2, CARD_W - 4, ART_H)))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack"))) \
        .add("th", 114, H.themed("SurfaceBlack")).add("mask", 114, A.rect_mask())
    art = clip.child(N("Body/Art/Image", "Image", STRETCH))
    art.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, C.SQUIRREL_ICON)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], "")) \
        .add("amb", 114, M.ambient("Pan", period=14, amplitude=0.04))
    label(clip.child(N("Body/Art/Numeral", "Numeral", TL(16, 6, 120, 54))), "01", "Display", "TextLight")
    chip = clip.child(N("Body/Art/Completed", "Completed", TR(12, 14, 112, 26)))
    chip.add("cg", 225, canvas_group(False))
    frame(chip, "Body/Art/Completed/Frame", 10, "SurfaceBlack", "SurfaceLight", fill_alpha=0.85)
    label(chip.child(N("Body/Art/Completed/Label", "Label", STRETCH)), "Completed", "Label", "TextLight", align="center")
    lock = clip.child(N("Body/Art/Lock", "Lock", R((0.5, 0.5), None, (0, 0), (40, 46))))
    lock.add("cg", 225, canvas_group(False))
    lk = image_node(lock, "Body/Art/Lock/Glyph", "Glyph", STRETCH, "TextLight", K.chrome_sprite("chrome_lock"),
                    preserve=True)

    y = ART_H + 22
    label(body.child(N("Body/State", "State", TL(20, y, 312, 18))), "In progress", "Label", "NeutralLightest")
    label(body.child(N("Body/Title", "Title", TL(20, y + 22, 312, 30))), "QUEST", "H3", "TextLight")
    label(body.child(N("Body/Description", "Description", TL(20, y + 58, 312, 44))), "", "BodySmall",
          "NeutralLightest", valign="top", wrap=True)
    C.themed_line(body, "Body/Rule", "Rule", TL(20, y + 110, 312, 1), "SurfaceDark")

    goals = body.child(N("Body/Goals", "Goals", TL(20, y + 130, 312, 120)))
    goals.add("cg", 225, canvas_group(True))
    section_label(goals, "Body/Goals/Title", "Title", "Goals", TL(0, 0, 200, 18), "NeutralLightest")
    for i in range(2):
        k = f"Body/Goals/Row{i + 1}"
        row = goals.child(N(k, f"Goal{i + 1}", TL(0, 26 + i * 46, 312, 42)))
        row.add("cg", 225, canvas_group(True))
        done = image_node(row, f"{k}/Done", "Done", TL(0, 4, 14, 14), "Cta", K.chrome_sprite("chrome_confirm"), preserve=True)
        done.comps = [x for x in done.comps if x[0] != "th"]
        pend = image_node(row, f"{k}/Pending", "Pending", TL(3, 7, 9, 9), "TextLight")
        pend.comps = [x for x in pend.comps if x[0] != "th"]
        pend.rot = 45
        cap = row.child(N(f"{k}/Caption", "Caption", TL(24, 2, 288, 16)))
        cap.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("UNLOCK INTENSITY 3", "Label", tok("NeutralLightest"))) \
            .add("kt", 114, K.kit_text("Label", "NeutralLightest"))
        desc = row.child(N(f"{k}/Description", "Description", TL(24, 20, 288, 22)))
        desc.add("cr", 222, K.canvas_renderer()) \
            .add("tmp", 114, A.ellipsis(text("Play Intensity 2 to unlock", "BodySmall", tok("TextLight")))) \
            .add("kt", 114, K.kit_text("BodySmall", "TextLight"))

    foot = body.child(N("Body/Footer", "Footer", R((0, 0), (1, 0), (0, 20), (-40, 100), (0.5, 0))))
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
    mt = play.child(N("Body/Footer/Play/Meter", "Meter", R((0, 1), None, (58, -28), (220, 22), (0, 0.5))))
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


def build_view():
    root = N("root", "ProfileView", STRETCH)
    image_node(root, "Veil", "Veil", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=0.72)

    # The quest row comes first in hierarchy: Refocus lands on the first live Selectable, and the
    # frontier card's Claim or Play is the control that matters here.
    quests = root.child(N("Quests", "Quests", R((0, 1), (1, 1), (0, -322), (-80, 718), (0.5, 1))))
    quests.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    section_label(quests, "Quests/Title", "Title", "Quests", TL(0, 8, 120, 18))
    label(quests.child(N("Quests/Summary", "Summary", TR(0, 8, 240, 18))), "0 of 5 complete", "Label", "NeutralLightest",
          align="right")
    bar = quests.child(N("Quests/Bar", "Bar", R((0, 1), (1, 1), ((88 - 168) / 2, -16), (-(88 + 168), 4), (0.5, 0.5))))
    bar.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"))).add("th", 114, H.themed("SurfaceDark"))
    fa = bar.child(N("Quests/Bar/FillArea", "FillArea", STRETCH))
    fl = fa.child(N("Quests/Bar/FillArea/Fill", "Fill", R((0, 0), (0.2, 1), (0, 0), (0, 0))))
    fl.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))) \
        .add("th", 114, H.themed("ControlActive"))
    bar.add("slider", 114, lambda ctx, go: K.mono(go, SLIDER,
            K.SELECTABLE_FIELDS.replace("  m_Interactable: 1\n", "  m_Interactable: 0\n")
            + f"  m_TargetGraphic: {ref(ctx.c('Quests/Bar', 'img'))}\n"
            f"  m_FillRect: {ref(ctx.rt('Quests/Bar/FillArea/Fill'))}\n  m_HandleRect: {{fileID: 0}}\n"
            "  m_Direction: 0\n  m_MinValue: 0\n  m_MaxValue: 5\n  m_WholeNumbers: 1\n  m_Value: 1\n"
            "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n", enabled=False))
    row = quests.child(N("Quests/Row", "Row", R((0, 0), (1, 1), (0, -24), (0, -48))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), GAP, 0, (1, 1), (1, 0)))

    card = root.child(N("Pilot", "Pilot", TL(40, 108, 1180, 196)))
    card.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise"))
    frame(card, "Pilot/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    card.child(M.grid_node("Pilot/Grid"))
    M.bracket_nodes(card, "Pilot/Bracket")
    av = card.child(N("Pilot/Avatar", "Avatar", TL(30, 22, 152, 152)))
    image_node(av, "Pilot/Avatar/Ring", "Ring", STRETCH, "SurfaceLight", K.kit_sprite("kit_oct_fill"), raycast=True)
    image_node(av, "Pilot/Avatar/Back", "Back", R((0, 0), (1, 1), (0, 0), (-6, -6)), "SurfaceDark", K.kit_sprite("kit_oct_fill"))
    icon = art_node(av, "Pilot/Avatar/Icon", "Icon", R((0, 0), (1, 1), (0, 0), (-10, -10)), A.AVATAR_PLACEHOLDER, preserve=True)
    icon.add("amb", 114, M.ambient("Float", period=4.2, amplitude=4))
    av.add("button", 114, C.plain_button(c("Pilot/Avatar/Ring", "img"))).add("audio", 114, A.menu_audio(11))
    label(card.child(N("Pilot/Eyebrow", "Eyebrow", TL(210, 36, 300, 18))), "Pilot", "Label", "NeutralLightest")
    nm = card.child(N("Pilot/Name", "Name", TL(210, 56, 400, 56)))
    nm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Pilot", "Display", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Display", "TextLight"))
    btns = card.child(N("Pilot/Buttons", "Buttons", TL(210, 124, 400, 40)))
    btns.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 12, 3, (0, 0), (0, 0)))
    for key, cap in (("ChangeAvatar", "CHANGE AVATAR"), ("Rename", "RENAME")):
        w, h = K.button_size(cap, small=True)
        btns.children.append(kit_button(key, key, "Default", cap, R((0, 0.5), None, (0, 0), (w, h), (0, 0.5)),
                                        opaque=False, small=True))
    C.themed_line(card, "Pilot/Divider", "Divider", TL(620, 34, 1, 128), "SurfaceDark")
    for cap, key, val, col, rw in STATS:
        x, y = 650 + col * 262, 40 + rw * 66
        label(card.child(N(f"Pilot/{key}Label", f"{key}Label", TL(x, y, 240, 16))), cap, "Label", "NeutralLightest")
        if key == "Crystals":
            image_node(card, "Pilot/CrystalIcon", "CrystalIcon", TL(x, y + 26, 18, 18), "Purchase",
                       K.chrome_sprite("chrome_crystal"), preserve=True)
            v = card.child(N("Pilot/CrystalsValue", "CrystalsValue", TL(x + 26, y + 18, 220, 34)))
            label(v, val, "H2", "Purchase")
            v.add("display", 114, lambda ctx, go: mono(go, S["crystals"], f"  balanceText: {ref(ctx.c('Pilot/CrystalsValue', 'tmp'))}\n"))
        else:
            label(card.child(N(f"Pilot/{key}Value", f"{key}Value", TL(x, y + 18, 240, 34))), val, "H2", "TextLight")

    ep = root.child(N("Episodes", "Episodes", R((1, 1), None, (-40, -108), (640, 196), (1, 1))))
    ep.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    A.ring_node(ep, "Episodes/FocusRing", 14)
    efill, ebord = A.frame_nodes(ep, "Episodes/Frame", 14, "SurfaceVeryDark", "SurfaceDark", raycast=True, themed_paint=False)
    M.bracket_nodes(ep, "Episodes/Bracket")
    lk = ep.child(N("Episodes/Lock", "Lock", R((0, 0.5), None, (76, 0), (84, 72))))
    image_node(lk, "Episodes/Lock/Hex", "Hex", STRETCH, "SurfaceLight", K.kit_sprite("kit_hex_line2"))
    image_node(lk, "Episodes/Lock/Glyph", "Glyph", R((0.5, 0.5), None, (0, 0), (24, 28)), "NeutralLightest",
               K.chrome_sprite("chrome_lock"), preserve=True)
    cap = ep.child(N("Episodes/Caption", "Caption", R((0, 0.5), None, (144, 16), (440, 18), (0, 0.5))))
    label(cap, "Episodes", "Label", "NeutralLightest")
    line = ep.child(N("Episodes/Line", "Line", R((0, 0.5), None, (144, -14), (460, 30), (0, 0.5))))
    line.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Arrive with the full release", "H3", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("H3", "NeutralLightest", driven=True))
    ep.add("tile", 114, A.tile_body(c("Episodes/FocusRing", "img"), c("Episodes/Frame/Fill", "img"),
                                    c("Episodes/Frame/Border", "img"), 14, primary=[c("Episodes/Line", "tmp")],
                                    unsel_primary="NeutralLightest", lock=c("Episodes/Lock/Glyph", "img")))
    ep.comps[-1] = ("tile", 114, locked_tile(ep.comps[-1][2]))
    ep.add("affordance", 114, lambda ctx, go: mono(go, S["affordance"], "  surface: 1\n"))
    ep.add("availability", 114, lambda ctx, go: mono(go, S["availability"],
           "  availability: 1\n"
           f"  lockedOverlay: {ref(ctx.go('Episodes/Lock'))}\n  unavailableOverlay: {{fileID: 0}}\n"
           f"  label: {ref(ctx.c('Episodes/Line', 'tmp'))}\n"
           "  unavailableLabelColor: {r: 0.36078432, g: 0.37254903, b: 0.4392157, a: 1}\n"
           "  dimOwnGraphics: 0\n  dimTint: {r: 1, g: 1, b: 1, a: 0.35}\n"
           "  lockedMessage: Episodes arrive with the full release.\n"))
    ep.add("shake", 114, M.shake()).add("audio", 114, A.menu_audio(11))
    return root


def locked_tile(body):
    def wrap(ctx, go):
        return body(ctx, go).replace("  isLocked: 0\n", "  isLocked: 1\n")
    return wrap


BUILDERS = {"QuestCard": build_quest_card, "ProfileView": build_view}


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
C.STUB_KIND.update({"ubutton": (114, A.BUTTON)})
# Scene fields -> (object key in ProfileView, stub kind[, nested instance key])
SCREEN_REFS = {"displayNameText": ("Pilot/Name#tmp", "tmp"), "avatarImage": ("Pilot/Avatar/Icon#img", "img"),
               "gamesCompletedText": ("Pilot/GamesValue#tmp", "tmp"), "flightTimeText": ("Pilot/FlightValue#tmp", "tmp"),
               "sessionsText": ("Pilot/SessionsValue#tmp", "tmp"), "renameButton": ("Rename|root#button", "button"),
               "episodesButton": ("Episodes#tile", "tile")}
AVATAR_BUTTONS = (("Pilot/Avatar#button", "ubutton"), ("ChangeAvatar|root#button", "button"))
TRACK_REFS = {"questItemContainer": ("Quests/Row#rt", "rt"), "progressBarSlider": ("Quests/Bar#slider", "slider"),
              "summaryText": ("Quests/Summary#tmp", "tmp")}
TRACK_DROPPED = ("ghostSlider", "questDescriptionContainer", "questDescriptionPrefab", "scrollRect")


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
        return list(SCREEN_REFS.values()) + list(AVATAR_BUTTONS) + list(TRACK_REFS.values()) + [("Episodes#go", "go")]

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


def scene_transform(text):
    sc = T.Scene(text)
    assert sc.field(IDS["root_go"], "m_Name") == "ProfileScreen", "ProfileScreen moved"
    assert sc.script(IDS["screen"]) == S["screen"] and sc.script(IDS["track"]) == S["track"], "Profile components moved"
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
    for fid in list(sc.order):
        if fid in mount.ids():
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

    doc = sc.docs[IDS["track"]]
    doc = set_field(doc, "questItemPrefab",
                    f"{{fileID: {K.FileIds('QuestCard')('root#go')}, guid: {asset_guid('QuestCard')}, type: 3}}")
    for field, (key, _) in TRACK_REFS.items():
        if re.search(r"^  " + field + ":", doc, re.M):
            doc = set_field(doc, field, ref(mount.stub_fid(key)))
        else:
            doc = doc.rstrip("\n") + "\n" + f"  {field}: {ref(mount.stub_fid(key))}\n"
    for field in TRACK_DROPPED:
        doc = set_field(doc, field, "{fileID: 0}")
    doc = set_field(doc, "parallaxFalloff", "0")
    sc.replace(IDS["track"], doc)

    doc = sc.docs[IDS["episodes"]]
    doc = insert_field_before(doc, "episodePanel", f"  refusalHost: {ref(mount.stub_fid('Episodes#go'))}")
    sc.replace(IDS["episodes"], doc)

    doc = sc.docs[IDS["old_modal"]]
    for field in ("profileIconImage", "profileNameLabel"):
        doc = set_field(doc, field, "{fileID: 0}")
    sc.replace(IDS["old_modal"], doc)
    return sc.text(), mount


SCREEN_ORDER = ("displayNameText", "avatarImage", "gamesCompletedText", "flightTimeText", "sessionsText",
                "profileWindow", "avatarButtons", "renameButton", "episodesButton", "episodes")


def rebuild_screen_doc(doc, mount):
    """ProfileScreen's fields, in declaration order (what Unity would write)."""
    head = doc[:doc.index("  displayNameText:")]
    vals = {k: f" {ref(mount.stub_fid(v[0]))}" for k, v in SCREEN_REFS.items()}
    vals["profileWindow"] = f" {ref(IDS['window'])}"
    vals["episodes"] = f" {ref(IDS['episodes'])}"
    body = ""
    for k in SCREEN_ORDER:
        if k == "avatarButtons":
            body += K.refs_list("avatarButtons", [mount.stub_fid(key) for key, _ in AVATAR_BUTTONS])
        else:
            body += f"  {k}:{vals[k]}\n"
    return head + body


def outputs():
    out = prefab_outputs()
    out.update(quest_outputs())
    out[SCENE], _ = scene_transform(asset_text(SCENE))
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
    track = sc.docs[IDS["track"]]
    assert f"questItemPrefab: {{fileID: {K.FileIds('QuestCard')('root#go')}, guid: {asset_guid('QuestCard')}" in track, \
        "QuestTrackView does not spawn QuestCard"
    for field in TRACK_DROPPED:
        assert re.search(r"^  " + field + r": \{fileID: 0\}", track, re.M), f"QuestTrackView still names {field}"
    assert re.search(r"^  parallaxFalloff: 0$", track, re.M), "five cards fit: parallax must be off"
    assert f"refusalHost: {{fileID: {mount.stub_fid('Episodes#go')}}}" in sc.docs[IDS["episodes"]], \
        "EpisodeScreen does not refuse on the Episodes card"


def drift(files):
    stale = []
    for path, text in files.items():
        full = os.path.join(REPO, path)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != text:
            stale.append(path)
    return stale


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
        ("parallax left on", lambda f: f.__setitem__(SCENE, re.sub(r"^  parallaxFalloff: 0$", "  parallaxFalloff: 400", f[SCENE], flags=re.M))),
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
