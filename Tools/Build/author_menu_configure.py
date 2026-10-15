#!/usr/bin/env python3
"""
Author ITEM 4 of the style-B menu redesign - the GAME CONFIGURE windows - as prefabs, and wire them
into ArcadeGameConfigureModal.prefab and Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 13; the
approved Configure, ConfigureArena and ConfigureMaelstrom boards on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Configure/
        ConfigureSeatChip.prefab       one seat row under a domain: DomainAvatarChip - avatar, name,
                                       role, the AI kick button (the modal's chipPrefab)
        ConfigureAbilityRow.prefab     one controls row: VesselControlRow - icon, petal, name, glyph
        ConfigurePoolRow.prefab        one Maelstrom pool row: MaelstromPoolEntry
        ConfigureIntensityNode.prefab  one intensity node: KitIntensityNode on a KitTile - numeral,
                                       caption, bar meter, lock + goal, and its route stub
        ConfigureDomainColumn.prefab   one domain column: KitDomainTile - tile, seat strip, open seat
                                       with "+ AI" (decision Q2)
        ConfigureHullTile.prefab       one hull of the Arena hull strip: KitHullTile on a KitTile
        ConfigureView.prefab           the arcade card window body: MinigameLaunchPanel
        ConfigureArenaView.prefab      the Arena window body: ArenaLaunchPanel (hull strip + Claim)
        ConfigureMaelstromView.prefab  the Maelstrom window body: MaelstromLaunchPanel (clip, format,
                                       pool)

    The three views are built by ONE builder (build_view) with a per-window variant, because each
    root carries a different launch-panel CLASS - which a Prefab Variant cannot change. Everything
    they share is a nested prefab (the node, the column, the chip, the rows), so a change to any part
    lands once.

    ArcadeGameConfigureModal.prefab: Content's old Border and ConfigurationContent subtrees are
    replaced by one ConfigureView instance; the modal's ModalType moves 10 -> 1 (ARCADE_GAME_CONFIGURE:
    10 is the Arcade window's own, so one B press closed both); selectedGame* point into the view;
    the hidden steppers are dropped; chipPrefab is ConfigureSeatChip; launchPanels[1] is the view.

    Menu_Main.unity: the instance's overrides that named retired objects are dropped and launchPanels
    [0] / [2] point at the new Arena / Maelstrom panels; the Arena and Maelstrom windows' Content is
    replaced the same way, with each panel's hostModal set to its own window's modal copy.

STANDING DOWN. The migrations (deleting the old subtrees) run only while those subtrees exist; after
that the script re-emits the documents it owns and validates the wiring AS COMMITTED on every run,
--check included.

    python3 Tools/Build/author_menu_configure.py              # write
    python3 Tools/Build/author_menu_configure.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_configure.py --self-test  # planted defects must all be caught
"""

import argparse
import hashlib
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


A = _load("author_menu_arcade")         # node emitter (rotation, added comps), part helpers
K = A.K                                 # node model, component bodies, tokens, roles
T = A.T                                 # Scene, stub writers
H = A.H                                 # text / label / le / canvas_group / themed
M = _load("menu_b_motion")              # revision-2 motion + surfaces

REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
MODAL_PREFAB = "Assets/_Prefabs/ArcadeGameConfigureModal.prefab"
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Configure"
MASK63 = 0x7FFFFFFFFFFFFFFF

N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
text, label, le, canvas_group, themed = H.text, H.label, H.le, H.canvas_group, H.themed
ref, mono, refs = A.ref, A.mono, A.refs
image_node, art_node, ring_node, frame_nodes, label_e = A.image_node, A.art_node, A.ring_node, A.frame_nodes, A.label_e


def script(cls, folder="Assets/_Scripts"):
    """A project script's guid, found by its class declaration (never recalled)."""
    for root, _, files in os.walk(os.path.join(REPO, folder)):
        for f in files:
            if f == cls + ".cs":
                return K.meta_guid(os.path.relpath(os.path.join(root, f), REPO))
    raise AssertionError(f"no script {cls}.cs")


S = dict(
    minigame=script("MinigameLaunchPanel"), aipicker=script("AIDifficultyPicker"), arena=script("ArenaLaunchPanel"), maelstrom=script("MaelstromLaunchPanel"),
    briefing=script("GameBriefingView"), objective=script("ObjectiveBoxView"), preview=script("ModePreviewWindow"),
    toast=script("PreviewMicroToast"), favorite=script("FavoriteIcon"), controls=script("VesselControlsPanel"),
    row=script("VesselControlRow"), video=script("ModeVideoView"), pool=script("MaelstromPoolListView"),
    pool_entry=script("MaelstromPoolEntry"), chip=script("DomainAvatarChip"),
    lbpanel=script("WeeklyChallengeLeaderboardPanel"), lbmodal=script("WeeklyChallengeLeaderboardModal"),
    podium=script("LeaderboardPodiumSlot"), band=script("LeaderboardRewardBand"), node=script("KitIntensityNode"),
    dtile=script("KitDomainTile"), hull=script("KitHullTile"), close=script("ModalWindowCloseButton"),
    modal=script("ArcadeGameConfigureModal"), audio=script("MenuAudio"),
    kt=K.kit_script("KitText"), th=K.kit_script("KitThemedGraphic"), button=K.kit_script("KitButton"),
    tile=K.kit_script("KitTile"), cover=K.kit_script("KitCoverImage"), entrance=K.kit_script("KitEntrance"),
    ambient=K.kit_script("KitAmbient"), spark=K.kit_script("KitSpark"), shake=K.kit_script("KitShake"),
)
RAW_IMAGE = "1344c3c82d62a2a41a3576d8abb8e3ea"
PACKAGE = A.PACKAGE | {RAW_IMAGE, M.RECT_MASK_2D}

# --- data assets (read off the shipped wiring the rebuild replaces) ------------------------------
INTENSITY_VAR = "490485287c38a2f4e9178566394efd69"       # the IntVariable every intensity button writes
COLOR_SET = "8d1c1bdcb636a424d9c54fbd1afc952f"           # ThemeManagerDataContainer.ColorSet
VESSEL_PREFABS = "05f8f1205bd90e14b8e59cf78da1a6f2"      # VesselControlsPanel.vesselPrefabs
TOURNAMENT = "f1738a31735a43a196f1b3a711c91947"          # MaelstromData.asset
ARENA_ROSTER = "45f4a55e49b44cc7ac47e0bf96411234"        # ArenaGames.asset
STAR_ON, STAR_OFF = "09f81e9c4fc304748819eca2110c75c0", "2f19932d871d57543a48ef3270061ffb"
SQUIRREL_ICON = K.VESSEL_ICON["Squirrel"]               # placeholder art until the card binds
AVATAR_PLACEHOLDER = A.AVATAR_PLACEHOLDER

PART_PREFABS = ("ConfigureSeatChip", "ConfigureAbilityRow", "ConfigurePoolRow", "ConfigureIntensityNode",
                "ConfigureDomainColumn", "ConfigureHullTile")
VIEW_PREFABS = ("ConfigureView", "ConfigureArenaView", "ConfigureMaelstromView")
BOARD_PREFABS = ("LeaderboardRow", "LeaderboardView")
PREFABS = PART_PREFABS + VIEW_PREFABS + BOARD_PREFABS

DOMAINS = (("Jade", 1), ("Ruby", 2), ("Gold", 4))         # Domains enum values (Blue = 3 is never drawn)


def asset_guid(name):
    return hashlib.md5(f"CosmicShore/MenuB/assets/{name}".encode()).hexdigest()


def prefab_guid(name):
    return asset_guid(name) if name in PREFABS else A.prefab_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def derived(src, pid):
    return (int(src) ^ int(pid)) & MASK63


def TL(x, y, w, h):
    """A rect placed by its top-left corner inside a top-left-anchored parent."""
    return R((0, 1), None, (x, -y), (w, h), (0, 1))


def TR(x, y, w, h):
    """A rect placed by its top-right corner, `x` in from the parent's right edge."""
    return R((1, 1), None, (-x, -y), (w, h), (1, 1))


# --------------------------------------------------------------------------------------------
# Node model: instances of this script's prefabs, added GameObjects, stubs for nested refs
# --------------------------------------------------------------------------------------------

class Inst(K.Instance):
    """A nested prefab instance that may carry added components and added GameObjects."""

    def __init__(self, key, prefab_key, name, r, mods=(), added=(), added_nodes=()):
        super().__init__(key, prefab_key, prefab_guid(prefab_key), name, r, mods)
        self.added = list(added)
        self.added_nodes = list(added_nodes)


STUB_KIND = {"go": (1, None), "rt": (224, None), "tmp": (114, K.TMP_UGUI), "img": (114, K.IMAGE),
             "raw": (114, RAW_IMAGE), "kt": (114, S["kt"]), "button": (114, S["button"]), "tile": (114, S["tile"]),
             "node": (114, S["node"]), "dtile": (114, S["dtile"]), "hull": (114, S["hull"]), "chip": (114, S["chip"]),
             "row": (114, S["row"]), "entry": (114, S["pool_entry"]), "cg": (225, None),
             "minigame": (114, S["minigame"]), "arena": (114, S["arena"]), "maelstrom": (114, S["maelstrom"]),
             "briefing": (114, S["briefing"]), "favorite": (114, S["favorite"]),
             "lbpanel": (114, S["lbpanel"])}


def stub_doc(fid, comp, src, guid, pid):
    cls, scr = STUB_KIND[comp]
    if cls == 1:
        return T.stub_go(fid, src, guid, pid)
    if cls == 114:
        return T.stub_mono(fid, src, guid, pid, scr)
    name = {224: "RectTransform", 225: "CanvasGroup"}[cls]
    return (f"--- !u!{cls} &{fid} stripped\n{name}:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
            f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


class Ctx(A.PCtx):
    def iref(self, inst_key, src_key):
        prefab = self.instances[inst_key]
        src = K.source_fid(prefab, src_key)
        pid = self.fid(inst_key + "#inst")
        fid = derived(src, pid)
        self.stubs.setdefault(fid, stub_doc(fid, src_key.split("#")[1], src, prefab_guid(prefab), pid))
        return fid

    def register(self, node):
        for ch in node.children:
            if A.is_instance(ch):
                self.instances[ch.key] = ch.prefab_key
                for added in getattr(ch, "added_nodes", []):
                    self.register_node(added)
            else:
                self.register(ch)

    def register_node(self, node):
        self.register(node)


_ARCADE_EMIT_INSTANCE = A.emit_instance


def emit_instance(ctx, inst, parent_rt, out):
    """A.emit_instance, plus m_AddedGameObjects: children parented under the instance root."""
    tmp = []
    _ARCADE_EMIT_INSTANCE(ctx, inst, parent_rt, tmp)
    nodes = getattr(inst, "added_nodes", [])
    if nodes:
        root_rt_src = K.source_fid(inst.prefab_key, "root#rt")
        stub_rt = ctx.fid(inst.key + "#stub")
        lines = "".join(f"    - targetCorrespondingSourceObject: {K.ref(root_rt_src, inst.prefab_guid)}\n"
                        f"      insertIndex: -1\n      addedObject: {{fileID: {ctx.rt(n.key)}}}\n" for n in nodes)
        tmp[0] = tmp[0].replace("    m_AddedGameObjects: []\n", "    m_AddedGameObjects:\n" + lines)
        for n in nodes:
            emit_node(ctx, n, stub_rt, tmp)
    out.extend(tmp)


def emit_node(ctx, node, parent_rt, out):
    """A.emit_node, routing instances through this script's emit_instance."""
    saved = A.emit_instance
    A.emit_instance = emit_instance
    try:
        A.emit_node(ctx, node, parent_rt, out)
    finally:
        A.emit_instance = saved


def prefab_text(root, file_key):
    ctx = Ctx(file_key)
    ctx.register(root)
    out = []
    emit_node(ctx, root, 0, out)
    return K.unity_empty_scalars(K.HEADER + "".join(out) + "".join(ctx.stubs.values()))


# --------------------------------------------------------------------------------------------
# Small builders
# --------------------------------------------------------------------------------------------

def c(node_key, comp):
    return lambda ctx: ctx.c(node_key, comp)


def ir(inst_key, src_key):
    return lambda ctx: ctx.iref(inst_key, src_key)


def kt_ref(node_key):
    return c(node_key, "kt")


def themed_line(parent, key, name, r, token, alpha=1.0, layout=None):
    return image_node(parent, key, name, r, token, alpha=alpha, layout=layout)


def raw_image(color=(1.0, 1.0, 1.0, 1.0), raycast=False, enabled=True):
    def body(ctx, go):
        return K.mono(go, RAW_IMAGE,
                      "  m_Material: {fileID: 0}\n"
                      f"  m_Color: {K.col(color)}\n  m_RaycastTarget: {1 if raycast else 0}\n"
                      "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
                      "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
                      "  m_Texture: {fileID: 0}\n  m_UVRect:\n    serializedVersion: 2\n"
                      "    x: 0\n    y: 0\n    width: 1\n    height: 1\n", enabled=enabled)
    return body


def plain_button(target_img, nav=3):
    """A uGUI Button (transition None) whose target graphic is `target_img`."""
    return lambda ctx, go: mono(go, A.BUTTON,
                                A.selectable(nav).replace("  m_Transition: 1\n", "  m_Transition: 0\n")
                                + f"  m_TargetGraphic: {ref(target_img(ctx))}\n" + A.calls([]))


def chip_node(parent, key, name, caption, kt_driven=False):
    """A summary chip: slivered frame, one Label, sized by its text (HLG + fitter)."""
    n = parent.child(N(key, name, R((0, 0.5), None, (0, 0), (K.label_width(caption, "Label") + 20, 26), (0, 0.5))))
    H.framed(n, key, 10, "SurfaceVeryDark", "SurfaceDark")
    lab = n.child(N(f"{key}/Label", "Label", R(size=(K.label_width(caption, "Label"), K.line_height("Label")))))
    label(lab, caption, "Label", "TextLight")
    n.add("hlg", 114, K.hv_layout(False, (10, 10, 5, 5), 0, 4, (1, 1), (0, 0))).add("csf", 114, K.fitter(2, 2))
    return n


def section_label(parent, key, name, caption, r, token="TextLight", align="left"):
    return label(parent.child(N(key, name, r)), caption, "Label", token, align=align)


# --------------------------------------------------------------------------------------------
# ConfigureSeatChip.prefab - one seat row (206 x 40)
# --------------------------------------------------------------------------------------------

COL_W, SEAT_H, TILE_H = 206, 40, 62


def build_seat_chip():
    root = N("root", "ConfigureSeatChip", R(size=(COL_W, SEAT_H)))
    frame_nodes(root, "Frame", 10, "SurfaceVeryDark", "SurfaceDark")
    outline = image_node(root, "LocalOutline", "LocalOutline", STRETCH, "ControlActive",
                         K.kit_sprite("kit_sliver10_line2"), sliced=True)
    outline.active = False
    outline.add("le", 114, le(ignore=True))
    _, x = A.avatar_nodes(root, "Avatar", 28, x=8)
    label_e(root.child(N("Name", "Name", R((0, 0.5), (1, 0.5), ((x + 8 - 92) / 2, 0), (-(x + 8 + 92), 22)))),
            "Pilot", "Body", "TextLight", align="left", driven=False)
    label_e(root.child(N("Role", "Role", R((1, 0.5), None, (-40, 0), (72, 18), (1, 0.5)))),
            "Pilot", "Label", "NeutralLightest", align="right")
    kick = A.small_button("Kick", "Kick", "Cancel", None, (1, 0.5), (1, 0.5), icon="chrome_close")
    kick.rect = R((1, 0.5), None, (-4, 0), (32, 32), (1, 0.5))
    kick.active = False
    root.children.append(kick)
    root.add("chip", 114, lambda ctx, go: mono(go, S["chip"],
             f"  avatarImage: {ref(ctx.c('Avatar/Icon', 'img'))}\n"
             f"  localPlayerOutline: {ref(ctx.go('LocalOutline'))}\n"
             f"  nameText: {ref(ctx.c('Name', 'tmp'))}\n"
             f"  roleText: {ref(ctx.c('Role', 'tmp'))}\n"
             f"  kickButton: {ref(ctx.iref('Kick', 'root#button'))}\n"
             "  hullPickButton: {fileID: 0}\n"))
    root.add("le", 114, le(ph=SEAT_H, pw=COL_W))
    root.add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    root.add("cg", 225, canvas_group(True))
    return root


# --------------------------------------------------------------------------------------------
# ConfigureAbilityRow.prefab - one controls row (396 x 34)
# --------------------------------------------------------------------------------------------

ROW_W = 396


def build_ability_row():
    root = N("root", "ConfigureAbilityRow", R(size=(ROW_W, 34)))
    root.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    art_node(root, "Icon", "Icon", R((0, 0.5), None, (13, 0), (26, 26)), K.kit_sprite("kit_hex_fill"), preserve=True)
    petal = art_node(root, "Petal", "Petal", R((0, 0.5), None, (24, -8), (12, 12)), None, preserve=True)
    petal.active = False
    label_e(root.child(N("Name", "Name", R((0, 0.5), (1, 0.5), ((36 - 96) / 2, 0), (-(36 + 96), 22)))),
            "Ability", "Body", "TextLight", align="left")
    desc = label_e(root.child(N("Description", "Description", R((1, 0.5), None, (-44, 0), (140, 18), (1, 0.5)))),
                   "", "BodySmall", "NeutralLightest", align="right")
    desc.active = False
    glyph = art_node(root, "Glyph", "Glyph", R((1, 0.5), None, (-4, 0), (32, 22), (1, 0.5)), None, preserve=True)
    glyph.active = False
    key = root.child(N("KeyLabel", "KeyLabel", R((1, 0.5), None, (-4, 0), (36, 22), (1, 0.5))))
    label(key, "", "Label", "NeutralLightest", align="right")
    key.active = False
    root.add("row", 114, lambda ctx, go: mono(go, S["row"],
             f"  icon: {ref(ctx.c('Icon', 'img'))}\n"
             f"  elementPetal: {ref(ctx.c('Petal', 'img'))}\n"
             f"  nameText: {ref(ctx.c('Name', 'tmp'))}\n"
             f"  descriptionText: {ref(ctx.c('Description', 'tmp'))}\n"
             f"  chipGlyph: {ref(ctx.c('Glyph', 'img'))}\n"
             f"  chipLabel: {ref(ctx.c('KeyLabel', 'tmp'))}\n"
             "  highlightScale: 1.1\n  restAlpha: 0.55\n  demonstrateCooldown: 1\n"))
    root.add("le", 114, le(ph=34))
    return root


# --------------------------------------------------------------------------------------------
# ConfigurePoolRow.prefab - one Maelstrom pool row (836 x 44)
# --------------------------------------------------------------------------------------------

POOL_W = 836


def build_pool_row():
    root = N("root", "ConfigurePoolRow", R(size=(POOL_W, 44)))
    root.add("cg", 225, canvas_group(True))
    image_node(root, "Fill", "Fill", STRETCH, "SurfaceDark", K.kit_sprite("kit_sliver10_fill"), sliced=True,
               alpha=0.55, layout=le(ignore=True))
    art_node(root, "Icon", "Icon", R((0, 0.5), None, (30, 0), (26, 26)), SQUIRREL_ICON, preserve=True)
    label_e(root.child(N("Name", "Name", R((0, 0.5), None, (58, 0), (420, 24), (0, 0.5)))), "Mode", "Body", "TextLight",
            align="left")
    label_e(root.child(N("Detail", "Detail", R((1, 0.5), None, (-20, 0), (320, 18), (1, 0.5)))), "TIER 1  -  HULL",
            "Label", "NeutralLightest", align="right")
    root.add("entry", 114, lambda ctx, go: mono(go, S["pool_entry"],
             f"  icon: {ref(ctx.c('Icon', 'img'))}\n"
             f"  nameText: {ref(ctx.c('Name', 'tmp'))}\n"
             f"  detailText: {ref(ctx.c('Detail', 'tmp'))}\n"
             "  lockedText: {fileID: 0}\n"
             "  lockedFormat: Intensity {0}\n  neverUnlockedText: Not in pool\n"
             "  detailFormat: TIER {0}  -  {1}\n  lockedAlpha: 0.4\n"))
    root.add("le", 114, le(ph=44))
    root.add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    return root


# --------------------------------------------------------------------------------------------
# ConfigureIntensityNode.prefab - one intensity node (150 x 148) and its route stub
# --------------------------------------------------------------------------------------------

NODE_W, NODE_H, STUB_H = 150, 148, 26
BAR_HEIGHTS = (14, 22, 30, 38)


def build_intensity_node():
    root = N("root", "ConfigureIntensityNode", R(size=(NODE_W, NODE_H)))
    root.add("cg", 225, canvas_group(True))
    ring_node(root, "FocusRing", 14)
    fill, bord = frame_nodes(root, "Frame", 14, "SurfaceVeryDark", "SurfaceDark", raycast=True, themed_paint=False)
    num = root.child(N("Numeral", "Numeral", TL(18, 10, 80, 54)))
    num.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("1", "Display", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("Display", "NeutralLightest", driven=True))
    cap = root.child(N("Caption", "Caption", TL(18, 64, 120, 18)))
    cap.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("INTENSITY", "Label", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("Label", "NeutralLightest", driven=True))
    lock = image_node(root, "Lock", "Lock", TR(14, 14, 16, 18), "TextLight", K.chrome_sprite("chrome_lock"), preserve=True)
    lock.comps = [x for x in lock.comps if x[0] != "th"]

    meter = root.child(N("Meter", "Meter", R((0, 0), None, (18, 16), (4 * 14 + 3 * 6, 38), (0, 0))))
    meter.add("cg", 225, canvas_group(True)) \
        .add("in", 114, M.entrance("GrowY", play_on_enable=False, distance=0))
    for i, h in enumerate(BAR_HEIGHTS):
        image_node(meter, f"Meter/Bar{i + 1}", f"Bar{i + 1}", R((0, 0), None, (i * 20, 0), (14, h), (0, 0)), "SurfaceDark")
        meter.children[-1].comps = [x for x in meter.children[-1].comps if x[0] != "th"]
    goal = root.child(N("Goal", "Goal", R((0, 0), (1, 0), (0, 14), (-36, 40), (0.5, 0))))
    goal.add("cg", 225, canvas_group(False))
    goal.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("Play Intensity 2 to unlock", "BodySmall", tok("NeutralLightest"), valign="bottom", wrap=True)) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest", driven=True))

    stub = root.child(N("RouteStub", "RouteStub", R((0.5, 0), None, (0, 0), (2, STUB_H), (0.5, 1))))
    stub.add("cg", 225, canvas_group(False))
    M.spark_line(stub, "RouteStub/Line", "Line", STRETCH, vertical=True, token="TextLight", period=1.4)

    root.add("tile", 114, A.tile_body(c("FocusRing", "img"), c("Frame/Fill", "img"), c("Frame/Border", "img"), 14,
                                      primary=[c("Numeral", "tmp"), c("Caption", "tmp")], unsel_primary="NeutralLightest",
                                      secondary=[c("Goal", "tmp")], lock=c("Lock", "img")))
    root.comps[-1] = ("tile", 114, locked_secondary(root.comps[-1][2], "NeutralLightest"))
    root.add("node", 114, lambda ctx, go: mono(go, S["node"],
             "  BorderImage: {fileID: 0}\n  IntensityImage: {fileID: 0}\n  IntensityText: {fileID: 0}\n"
             "  BorderSpriteSelected: {fileID: 0}\n  BorderSpriteUnselected: {fileID: 0}\n"
             "  IntensityOneSpriteSelected: {fileID: 0}\n  IntensityOneSpriteUnselected: {fileID: 0}\n"
             "  IntensityTwoSpriteSelected: {fileID: 0}\n  IntensityTwoSpriteUnselected: {fileID: 0}\n"
             "  IntensityThreeSpriteSelected: {fileID: 0}\n  IntensityThreeSpriteUnselected: {fileID: 0}\n"
             "  IntensityFourSpriteSelected: {fileID: 0}\n  IntensityFourSpriteUnselected: {fileID: 0}\n"
             "  IntensityColorSelected:\n    serializedVersion: 2\n    rgba: 4294967295\n"
             "  IntensityColorUnselected:\n    serializedVersion: 2\n    rgba: 4294967295\n"
             "  IntensityColorActive:\n    serializedVersion: 2\n    rgba: 4294967295\n"
             "  IntensityColorInactive:\n    serializedVersion: 2\n    rgba: 4294967295\n"
             "  IntensityColorLocked:\n    serializedVersion: 2\n    rgba: 4282137720\n"
             f"  selectedIntensityCount: {K.ref(11400000, INTENSITY_VAR, 2)}\n"
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n"
             f"  tile: {ref(ctx.c('root', 'tile'))}\n"
             f"  nodeGroup: {ref(ctx.c('root', 'cg'))}\n"
             f"  numeralText: {ref(ctx.c('Numeral', 'kt'))}\n"
             f"  captionText: {ref(ctx.c('Caption', 'kt'))}\n"
             + refs("bars", [ctx.c(f"Meter/Bar{i + 1}", "img") for i in range(4)])
             + f"  meterGroup: {ref(ctx.c('Meter', 'cg'))}\n"
             f"  meterGrow: {ref(ctx.c('Meter', 'in'))}\n"
             f"  goalText: {ref(ctx.c('Goal', 'kt'))}\n"
             f"  goalGroup: {ref(ctx.c('Goal', 'cg'))}\n"
             f"  routeStub: {ref(ctx.c('RouteStub', 'cg'))}\n"))
    root.add("shake", 114, M.shake())
    root.add("in", 114, M.entrance("Rise", by_sibling=True))
    return root


def locked_secondary(body, token):
    def wrap(ctx, go):
        return re.sub(r"  lockedSecondaryToken: \d+\n", f"  lockedSecondaryToken: {K.TOKEN_INDEX[token]}\n", body(ctx, go))
    return wrap


# --------------------------------------------------------------------------------------------
# ConfigureDomainColumn.prefab - one domain column (206 wide)
# --------------------------------------------------------------------------------------------

def build_domain_column():
    root = N("root", "ConfigureDomainColumn", R(size=(COL_W, TILE_H + 10 + 2 * (SEAT_H + 6))))
    root.add("cg", 225, canvas_group(True))
    root.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 6, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))

    tile = root.child(N("Tile", "Tile", TL(0, 0, COL_W, TILE_H)))
    tile.add("le", 114, le(ph=TILE_H))
    ring_node(tile, "Tile/FocusRing", 14)
    fill = tile.child(N("Tile/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    bord = tile.child(N("Tile/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("ControlActive"), K.kit_sprite("kit_sliver14_line1"), True))
    image_node(tile, "Tile/Accent", "Accent", R((0, 0), (0, 1), (2, 0), (4, -16), (0, 0.5)), "ControlActive")
    tile.children[-1].comps = [x for x in tile.children[-1].comps if x[0] != "th"]
    name = tile.child(N("Tile/Name", "Name", TL(18, 12, 120, 22)))
    name.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("JADE", "Label", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("Label", "TextLight", driven=True))
    line = tile.child(N("Tile/Line", "Line", TL(18, 34, 130, 18)))
    line.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Tap to join", "BodySmall", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest", driven=True))
    count = tile.child(N("Tile/Count", "Count", TR(16, 10, 40, 40)))
    count.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("0", "H2", tok("ControlActive"), align="right")) \
        .add("kt", 114, K.kit_text("H2", "ControlActive", driven=True))
    tile.add("tile", 114, A.tile_body(c("Tile/FocusRing", "img"), c("Tile/Fill", "img"), lambda ctx: 0, 14,
                                      primary=[c("Tile/Name", "tmp")], secondary=[c("Tile/Line", "tmp")]))

    strip = root.child(N("Strip", "Seats", TL(0, TILE_H + 6, COL_W, SEAT_H)))
    strip.add("vlg", 114, K.hv_layout(True, (0, 0, 4, 0), 6, 0, (1, 1), (1, 0)))

    open_seat = root.child(N("OpenSeat", "OpenSeat", TL(0, 0, COL_W, SEAT_H)))
    open_seat.add("cg", 225, canvas_group(True)).add("le", 114, le(ph=SEAT_H))
    image_node(open_seat, "OpenSeat/Fill", "Fill", STRETCH, "SurfaceVeryDark", K.kit_sprite("kit_sliver10_fill"),
               sliced=True, alpha=0.6, layout=le(ignore=True))
    image_node(open_seat, "OpenSeat/Border", "Border", STRETCH, "SurfaceDark", K.kit_sprite("kit_sliver10_line1"),
               sliced=True, layout=le(ignore=True))
    label(open_seat.child(N("OpenSeat/Label", "Label", R((0, 0.5), None, (14, 0), (110, 20), (0, 0.5)))),
          "Open seat", "BodySmall", "NeutralLightest")
    add = open_seat.child(N("OpenSeat/AddAI", "AddAI", R((1, 0.5), None, (-4, 0), (56, 28), (1, 0.5))))
    add.add("cg", 225, canvas_group(True))
    w, h = K.button_size("+ AI", small=True)
    add.children.append(Inst("AddAIButton", "KitButtonSmall", "AddAIButton", R((1, 0.5), None, (0, 0), (w, h), (1, 0.5)),
                             K.button_mods("KitButtonSmall", "Default", False, "+ AI", small=True)))

    root.add("dtile", 114, lambda ctx, go: mono(go, S["dtile"],
             "  domain: 1\n"
             f"  button: {ref(ctx.c('Tile', 'tile'))}\n"
             "  backgroundImage: {fileID: 0}\n  selectedSprite: {fileID: 0}\n  unselectedSprite: {fileID: 0}\n"
             f"  labelText: {ref(ctx.c('Tile/Name', 'tmp'))}\n"
             "  selectedTextColor: {r: 1, g: 1, b: 1, a: 1}\n  unselectedTextColor: {r: 1, g: 1, b: 1, a: 1}\n"
             f"  avatarStrip: {ref(ctx.c('Strip', 'vlg'))}\n"
             f"  addAiButton: {ref(ctx.iref('AddAIButton', 'root#button'))}\n"
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n"
             f"  colorSet: {K.ref(11400000, COLOR_SET, 2)}\n"
             f"  tile: {ref(ctx.c('Tile', 'tile'))}\n"
             f"  borderImage: {ref(ctx.c('Tile/Border', 'img'))}\n"
             f"  borderSelectedSprite: {K.sprite_ref(K.kit_sprite('kit_sliver14_line2'))}\n"
             f"  borderUnselectedSprite: {K.sprite_ref(K.kit_sprite('kit_sliver14_line1'))}\n"
             f"  accent: {ref(ctx.c('Tile/Accent', 'img'))}\n"
             f"  columnGroup: {ref(ctx.c('root', 'cg'))}\n"
             f"  lineText: {ref(ctx.c('Tile/Line', 'kt'))}\n"
             f"  countText: {ref(ctx.c('Tile/Count', 'kt'))}\n"
             f"  openSeatGroup: {ref(ctx.c('OpenSeat', 'cg'))}\n"
             f"  addAiGroup: {ref(ctx.c('OpenSeat/AddAI', 'cg'))}\n"))
    root.add("in", 114, M.entrance("Rise", by_sibling=True))
    return root


# --------------------------------------------------------------------------------------------
# ConfigureHullTile.prefab - one hull of the Arena strip (76 x 84)
# --------------------------------------------------------------------------------------------

HULL_W, HULL_H, HULL_GAP = 76, 84, 7


def build_hull_tile():
    root = N("root", "ConfigureHullTile", R(size=(HULL_W, HULL_H)))
    root.add("cg", 225, canvas_group(True))
    ring_node(root, "FocusRing", 10)
    frame_nodes(root, "Frame", 10, "SurfaceVeryDark", "SurfaceDark", raycast=True, themed_paint=False)
    art_node(root, "Art", "Art", R((0.5, 1), None, (0, -10), (48, 44), (0.5, 1)), SQUIRREL_ICON, preserve=True)
    nm = root.child(N("Name", "Name", R((0, 0), (1, 0), (0, 8), (-8, 18), (0.5, 0))))
    nm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("HULL", "Label", tok("NeutralLightest"), align="center"))) \
        .add("kt", 114, K.kit_text("Label", "NeutralLightest", driven=True))
    lock = image_node(root, "Lock", "Lock", TR(6, 6, 12, 14), "TextLight", K.chrome_sprite("chrome_lock"), preserve=True)
    lock.comps = [x for x in lock.comps if x[0] != "th"]
    root.add("tile", 114, A.tile_body(c("FocusRing", "img"), c("Frame/Fill", "img"), c("Frame/Border", "img"), 10,
                                      primary=[c("Name", "tmp")], unsel_primary="NeutralLightest",
                                      art=[c("Art", "img")], lock=c("Lock", "img")))
    root.add("hull", 114, lambda ctx, go: mono(go, S["hull"],
             f"  tile: {ref(ctx.c('root', 'tile'))}\n"
             f"  art: {ref(ctx.c('Art', 'img'))}\n"
             f"  nameText: {ref(ctx.c('Name', 'kt'))}\n"
             f"  group: {ref(ctx.c('root', 'cg'))}\n"))
    root.add("shake", 114, M.shake())
    root.add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    return root


# --------------------------------------------------------------------------------------------
# The views - ConfigureView / ConfigureArenaView / ConfigureMaelstromView
# --------------------------------------------------------------------------------------------

LEFT_TOP, COL_H = 196, 844           # both columns run from y 196 to y 1040 at 1080
PREVIEW_H, OBJECTIVE_H = 506, 64
PANEL_W = 900
NODE_X = (200, 374, 548, 722)       # intensity node lefts inside the tree panel
COL_X = (200, 432, 664)             # domain column lefts
ROW_Y = (30, 228, 426)              # the three step rows
FOOT_Y = 720
EYEBROW = {"arcade": "Arcade", "arena": "Arena", "maelstrom": "Arcade - Maelstrom"}
PANEL_CLASS = {"arcade": "minigame", "arena": "arena", "maelstrom": "maelstrom"}
VIEW_NAME = {"arcade": "ConfigureView", "arena": "ConfigureArenaView", "maelstrom": "ConfigureMaelstromView"}
HULL_SLOTS = 8


def frame(parent, key, sliver=14, fill="SurfaceVeryDark", border="SurfaceDark", fill_alpha=1.0):
    H.framed(parent, key, sliver, fill, border, fill_alpha=fill_alpha)


def header(root, kind):
    hd = root.child(N("Header", "Header", R((0, 1), (1, 1), (0, -108), (-80, 64), (0.5, 1))))
    hd.add("in", 114, M.entrance("Rise", distance=10)).add("cg", 225, canvas_group(True))
    label(hd.child(N("Header/Eyebrow", "Eyebrow", TL(0, 0, 600, 18))), EYEBROW[kind], "Label", "NeutralLightest")
    row = hd.child(N("Header/TitleRow", "TitleRow", TL(0, 20, 1100, 46)))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 16, 3, (1, 1), (0, 0)))
    title = row.child(N("Header/Title", "Title", R((0, 0.5), None, (0, 0), (300, 46), (0, 0.5))))
    title.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Game", "H1", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("H1", "TextLight"))
    star = row.child(N("Header/Star", "Star", R((0, 0.5), None, (0, 0), (26, 26), (0, 0.5))))
    star.add("le", 114, le(pw=26, ph=26))
    star.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight"), K.chrome_sprite("chrome_star"), raycast=True, preserve=True)) \
        .add("button", 114, plain_button(c("Header/Star", "img"))) \
        .add("fav", 114, lambda ctx, go: mono(go, S["favorite"],
             f"  IconActive: {K.sprite_ref(K.chrome_sprite('chrome_star_fill'))}\n"
             f"  IconInActive: {K.sprite_ref(K.chrome_sprite('chrome_star'))}\n"
             f"  IconImage: {ref(ctx.c('Header/Star', 'img'))}\n")) \
        .add("audio", 114, A.menu_audio(11))
    back = A.back_button(hd)
    # The leaderboard is offered on the weekly challenge only (ArcadeLaunchPanel.SetLeaderboardAvailable).
    back_w = 16 + (K.label_width("B", "Label") + 24) + 10 + K.label_width("BACK", "ButtonSmall") + 16
    lb_w, lb_h = K.button_size("LEADERBOARD", small=True)
    lb = hd.child(N("Header/Leaderboard", "Leaderboard", R((1, 0), None, (-(back_w + 12), 0), (lb_w, 40), (1, 0))))
    lb.add("cg", 225, canvas_group(False))
    lb.children.append(Inst("LeaderboardButton", "KitButtonSmall", "LeaderboardButton",
                            R((0.5, 0.5), None, (0, 0), (lb_w, lb_h)),
                            K.button_mods("KitButtonSmall", "Default", False, "LEADERBOARD", small=True)))
    return back


def preview_window(left, kind):
    """The live arena (ModePreviewWindow) or, for Maelstrom, the clip (ModeVideoView)."""
    pv = left.child(N("Preview", "Preview", R((0, 1), (1, 1), (0, 0), (0, PREVIEW_H), (0.5, 1))))
    # No entrance on the preview: the window opens by a CanvasGroup fade, and a rise that started
    # under it could leave the preview at alpha 0 - the frame, the live arena and the tap-to-play
    # target all invisible. The preview is the window's main feature; it is simply always there.
    pv.add("cg", 225, canvas_group(True))
    frame(pv, "Preview/Frame", 14, "SurfaceBlack", "SurfaceDark")
    surf = pv.child(N("Preview/Surface", "Surface", R((0, 0), (1, 1), (0, 0), (-4, -4))))
    surf.add("cr", 222, K.canvas_renderer()).add("raw", 114, raw_image(enabled=False))
    strip = pv.child(N("Preview/Strip", "Strip", R((0, 0), (1, 0), (0, 2), (-4, 52), (0.5, 0))))
    strip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.6))) \
        .add("th", 114, themed("SurfaceBlack", 0.6))
    if kind == "maelstrom":
        label(strip.child(N("Preview/Strip/Hint", "Hint", R((0, 0.5), None, (20, 0), (300, 18), (0, 0.5)))), "Clip",
              "Label", "TextLight")
        na = pv.child(N("Preview/Unavailable", "Unavailable", R((0.5, 0.5), None, (0, 0), (600, 60))))
        label(na, "No clip for this card yet.", "Body", "NeutralLightest", align="center")
        na.active = False
        pv.add("video", 114, lambda ctx, go: mono(go, S["video"],
               f"  surface: {ref(ctx.c('Preview/Surface', 'raw'))}\n"
               f"  unavailableLabel: {ref(ctx.go('Preview/Unavailable'))}\n"
               "  loop: 1\n  playAudio: 0\n  textureHeight: 360\n"))
        pv.add("vp", 328, video_player)
        return pv
    hint = strip.child(N("Preview/Strip/Hint", "FocusHint", R((0, 0.5), None, (20, 0), (300, 18), (0, 0.5))))
    hint.add("cg", 225, canvas_group(True)).add("amb", 114, M.ambient("Breathe", period=2.4, amplitude=0.45))
    label(hint, "Tap to fly", "Label", "TextLight")
    vname = strip.child(N("Preview/Strip/Vessel", "Vessel", R((1, 0.5), None, (-20, 0), (300, 18), (1, 0.5))))
    label(vname, "Squirrel", "Label", "NeutralLightest", align="right")
    status = pv.child(N("Preview/Status", "Status", R((0.5, 0.5), None, (0, 26), (600, 60))))
    status.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("LOADING PREVIEW...", "Label", tok("NeutralLightest"), align="center", wrap=True)) \
        .add("kt", 114, K.kit_text("Label", "NeutralLightest"))
    focus = pv.child(N("Preview/Focus", "Focus", R((0, 0), (1, 1), (0, 0), (-4, -4))))
    focus.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("button", 114, plain_button(c("Preview/Focus", "img")))
    toast = pv.child(N("Preview/Toast", "Toast", TR(24, 24, 120, 44)))
    toast.active = False
    toast.add("cg", 225, canvas_group(True))
    art_node(toast, "Preview/Toast/Icon", "Icon", R((0, 0.5), None, (18, 0), (28, 28)), None, preserve=True)
    tl = toast.child(N("Preview/Toast/Label", "Label", R((0, 0.5), None, (40, 0), (80, 30), (0, 0.5))))
    tl.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("+1", "Data", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("Data", "TextLight", driven=True))
    pv.add("window", 114, lambda ctx, go: mono(go, S["preview"],
           f"  surface: {ref(ctx.c('Preview/Surface', 'raw'))}\n"
           f"  statusLabel: {ref(ctx.c('Preview/Status', 'tmp'))}\n"
           f"  focusButton: {ref(ctx.c('Preview/Focus', 'button'))}\n"
           f"  focusHint: {ref(ctx.go('Preview/Strip/Hint'))}\n"
           "  renderHeight: 360\n  maxRenderHeight: 1080\n"))
    pv.add("toast", 114, lambda ctx, go: mono(go, S["toast"],
           f"  group: {ref(ctx.c('Preview/Toast', 'cg'))}\n"
           f"  label: {ref(ctx.c('Preview/Toast/Label', 'tmp'))}\n"
           f"  icon: {ref(ctx.c('Preview/Toast/Icon', 'img'))}\n"
           "  holdSeconds: 0.8\n  fadeSeconds: 0.4\n  popScale: 1.3\n"))
    return pv


def video_player(ctx, go):
    """Unity's VideoPlayer, cloned from the Maelstrom window's shipped one (render to a texture)."""
    return ("VideoPlayer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_VideoClip: {{fileID: 0}}\n"
            "  m_TargetCameraAlpha: 1\n  m_TargetCamera3DLayout: 0\n  m_TargetCamera: {fileID: 0}\n"
            "  m_TargetTexture: {fileID: 0}\n  m_TimeReference: 0\n  m_TargetMaterialRenderer: {fileID: 0}\n"
            "  m_TargetMaterialProperty: <noninit>\n  m_RenderMode: 2\n  m_AspectRatio: 2\n  m_DataSource: 0\n"
            "  m_TimeUpdateMode: 2\n  m_PlaybackSpeed: 1\n  m_AudioOutputMode: 2\n  m_TargetAudioSources:\n"
            "  - {fileID: 0}\n  m_DirectAudioVolumes:\n  - 1\n  m_Url: \n  m_EnabledAudioTracks: 01\n"
            "  m_DirectAudioMutes: 00\n  m_ControlledAudioTrackCount: 1\n  m_PlayOnAwake: 1\n  m_SkipOnDrop: 1\n"
            "  m_Looping: 0\n  m_WaitForFirstFrame: 1\n  m_FrameReadyEventEnabled: 0\n  m_VideoShaders: []\n")


def objective_bar(left):
    ob = left.child(N("Objective", "Objective", R((0, 1), (1, 1), (0, -(PREVIEW_H + 20)), (0, OBJECTIVE_H), (0.5, 1))))
    ob.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    frame(ob, "Objective/Frame", 14)
    ic = image_node(ob, "Objective/Icon", "Icon", R((0, 0.5), None, (32, 0), (22, 22)), "TextLight",
                    K.kit_sprite("kit_hex_line2"), preserve=True)
    ic.comps = [x for x in ic.comps if x[0] != "th"]
    label(ob.child(N("Objective/Label", "Label", R((0, 0.5), None, (56, 0), (140, 18), (0, 0.5)))), "Objective", "Label",
          "NeutralLightest")
    label_e(ob.child(N("Objective/Text", "Text", R((0, 0.5), (1, 0.5), ((196 - 80) / 2, 0), (-(196 + 80), 24)))),
            "Fly around. Get a feel for it.", "Body", "TextLight", align="left")
    cnt = ob.child(N("Objective/Count", "Count", R((1, 0.5), None, (-24, 0), (60, 26), (1, 0.5))))
    cnt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("0", "Data", tok("ControlActive"), align="right")) \
        .add("kt", 114, K.kit_text("Data", "ControlActive", driven=True))
    ob.add("view", 114, lambda ctx, go: mono(go, S["objective"],
           f"  icon: {ref(ctx.c('Objective/Icon', 'img'))}\n"
           f"  countText: {ref(ctx.c('Objective/Count', 'tmp'))}\n"
           f"  objectiveText: {ref(ctx.c('Objective/Text', 'tmp'))}\n"
           "  pulseScale: 1.35\n  pulseSeconds: 0.35\n"))
    return ob


def lower_block(left, key, name, title, top, height, half, stagger):
    """One of the two lower panels: Briefing (left half) / Controls or Format (right half)."""
    x, w = (-5, -10) if half == 0 else (5, -10)
    amin, amax = ((0, 1), (0.5, 1)) if half == 0 else ((0.5, 1), (1, 1))
    n = left.child(N(key, name, R(amin, amax, (x, -top), (w, height), (0.5, 1))))
    n.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=stagger))
    frame(n, f"{key}/Frame", 14)
    section_label(n, f"{key}/Title", "Title", title, TL(22, 22, 200, 18))
    return n


def briefing(left, top, height):
    b = lower_block(left, "Briefing", "Briefing", "Briefing", top, height, 0, 2)
    body = b.child(N("Briefing/Body", "Body", R((0, 0), (1, 1), (0, -18), (-44, -80))))
    body.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("Find the fastest path to the crystal.", "Body", tok("TextLight"), valign="top", wrap=True)) \
        .add("kt", 114, K.kit_text("Body", "TextLight"))
    b.add("view", 114, lambda ctx, go: mono(go, S["briefing"],
          f"  bodyText: {ref(ctx.c('Briefing/Body', 'tmp'))}\n"
          "  tipPrefix: 'Tip: '\n  dwellSeconds: 6\n  fadeSeconds: 0.4\n  descriptionExtraDwell: 3\n"))
    return b


def controls(left, top, height):
    cb = lower_block(left, "Controls", "Controls", "Controls", top, height, 1, 3)
    label(cb.child(N("Controls/Vessel", "Vessel", TR(22, 22, 200, 18))), "Squirrel", "Label", "NeutralLightest",
          align="right")
    sc = cb.child(N("Controls/Scroll", "Scroll", R((0, 0), (1, 1), (0, -14), (-44, -76))))
    sc.add("scroll", 114, A.scroll_rect("Controls/Scroll/Viewport/Content", "Controls/Scroll/Viewport", 20))
    vp = sc.child(N("Controls/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, A.rect_mask())
    content = vp.child(N("Controls/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 6, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    cb.add("controls", 114, lambda ctx, go: mono(go, S["controls"],
           f"  rowContainer: {ref(ctx.rt('Controls/Scroll/Viewport/Content'))}\n"
           f"  rowPrefab: {K.ref(K.FileIds('ConfigureAbilityRow')('root#row'), asset_guid('ConfigureAbilityRow'))}\n"
           "  flightControls:\n  - Headline: Left stick\n    Description: Steer - yaw and pitch.\n"
           "    Icon: {fileID: 0}\n    Control: 0\n  - Headline: Right stick\n    Description: Throttle and roll.\n"
           "    Icon: {fileID: 0}\n    Control: 0\n"
           "  controlsLibrary: {fileID: 0}\n  glyphSet: {fileID: 0}\n  barsConfig: {fileID: 0}\n"
           f"  vesselPrefabs: {K.ref(11400000, VESSEL_PREFABS, 2)}\n"
           "  deviceSwitcher: {fileID: 0}\n  vesselNameText: {fileID: 0}\n  vesselIcon: {fileID: 0}\n"
           "  flightSectionTitle: \n  abilitySectionTitle: \n"
           "  rowDwellSeconds: 2.2\n  sweepRampFraction: 0.25\n  cooldownFraction: 0.6\n"))
    return cb


FORMAT_ROWS = (("First to", "WinTarget", "6 points"), ("Games", "GameCap", "up to 7"),
               ("Points by place", "Points", "2 / 1 / 0"), ("Seats", "Seats", "4"))


def format_block(left, top, height):
    fb = lower_block(left, "Format", "Format", "Format", top, height, 1, 3)
    for i, (cap, key, value) in enumerate(FORMAT_ROWS):
        y = 52 + i * 41
        row = fb.child(N(f"Format/{key}", key, R((0, 1), (1, 1), (0, -y), (-44, 40), (0.5, 1))))
        label(row.child(N(f"Format/{key}/Caption", "Caption", STRETCH)), cap, "Body", "NeutralLightest")
        label(row.child(N(f"Format/{key}/Value", "Value", STRETCH)), value, "Data", "TextLight", align="right")
        image_node(row, f"Format/{key}/Rule", "Rule", R((0, 0), (1, 0), (0, 0), (0, 1), (0.5, 0)), "SurfaceDark")
    return fb


def gutter(panel, i, num, caption, value_key, value, hint):
    y = ROW_Y[i]
    g = panel.child(N(f"Step{i + 1}", f"Step{i + 1}", TL(30, y, 150, 190)))
    g.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=i))
    label(g.child(N(f"Step{i + 1}/Num", "Num", TL(0, 0, 120, 54))), num, "Display", "SurfaceLight")
    label(g.child(N(f"Step{i + 1}/Caption", "Caption", TL(0, 56, 150, 18))), caption, "Label", "TextLight")
    label(g.child(N(f"Step{i + 1}/Value", value_key, TL(0, 78, 150, 24))), value, "Body", "TextLight")
    label(g.child(N(f"Step{i + 1}/Hint", "Hint", TL(0, 106, 150, 66))), hint, "BodySmall", "NeutralLightest",
          valign="top", wrap=True)
    return g


def vessel_row(panel):
    vn = panel.child(N("VesselNode", "VesselNode", TL(NODE_X[0], ROW_Y[0], NODE_W, NODE_H)))
    vn.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Pop", stagger=1))
    frame(vn, "VesselNode/Frame", 14, "SurfaceDark", "TextLight")
    vn.children[-1].comps = [(k, cid, b) if k != "img" else
                             ("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line2"), True))
                             for k, cid, b in vn.children[-1].comps]
    clip = vn.child(N("VesselNode/Clip", "Clip", TL(8, 8, NODE_W - 16, 96)))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack"))) \
        .add("th", 114, themed("SurfaceBlack")).add("mask", 114, A.rect_mask())
    art = clip.child(N("VesselNode/Clip/Art", "Art", STRETCH))
    art.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, SQUIRREL_ICON)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], "")) \
        .add("amb", 114, M.ambient("Float", period=3.6, amplitude=6))
    label(vn.child(N("VesselNode/Caption", "Caption", R((0, 0), (1, 0), (0, 14), (-16, 18), (0.5, 0)))), "Squirrel",
          "Label", "TextLight", align="center")
    info = panel.child(N("ClassInfo", "ClassInfo", TL(377, 40, 493, 150)))
    info.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    label(info.child(N("ClassInfo/Eyebrow", "Eyebrow", TL(0, 0, 300, 18))), "Class", "Label", "NeutralLightest")
    label(info.child(N("ClassInfo/Name", "Name", TL(0, 22, 493, 36))), "Squirrel", "H2", "TextLight")
    label(info.child(N("ClassInfo/Description", "Description", TL(0, 64, 493, 86))), "", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)


def hull_row(panel):
    strip = panel.child(N("HullStrip", "HullStrip", TL(NODE_X[0], ROW_Y[0], HULL_SLOTS * (HULL_W + HULL_GAP), HULL_H)))
    strip.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), HULL_GAP, 0, (0, 0), (0, 0)))
    for i in range(HULL_SLOTS):
        strip.children.append(Inst(f"Hull{i + 1}", "ConfigureHullTile", f"Hull{i + 1}",
                                   TL(i * (HULL_W + HULL_GAP), 0, HULL_W, HULL_H)))
    info = panel.child(N("ClassInfo", "ClassInfo", TL(NODE_X[0], ROW_Y[0] + HULL_H + 12, 670, 72)))
    info.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    label(info.child(N("ClassInfo/Name", "Name", TL(0, 0, 670, 30))), "Dolphin", "H3", "TextLight")
    label(info.child(N("ClassInfo/Description", "Description", TL(0, 32, 670, 40))), "", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)
    w, h = K.button_size("CLAIM", small=True)
    claim = panel.child(N("Claim", "Claim", TL(30, 176, w, h)))
    claim.children.append(Inst("ClaimButton", "KitButtonSmall", "ClaimButton", STRETCH,
                               K.button_mods("KitButtonSmall", "Cta", False, "CLAIM", small=True)))


def routes(panel, first_row_bottom):
    """The tree's connectors: vessel -> intensity bus, and the intensity -> pilots bus. Each live
    route draws in (std) and carries a spark (ambient, Q1)."""
    cx = NODE_X[0] + NODE_W // 2 - 1
    M.spark_line(panel, "Route1/Stem", "Route1Stem", TL(cx, first_row_bottom, 2, 200 - first_row_bottom + 2), vertical=True,
                 grow_delay=0.2)
    M.spark_line(panel, "Route1/Bus", "Route1Bus", TL(cx, 200, NODE_X[3] + NODE_W // 2 - cx + 1, 2), grow_delay=0.3)
    for i, x in enumerate(NODE_X):
        themed_line(panel, f"Route1/Drop{i + 1}", f"Route1Drop{i + 1}", TL(x + NODE_W // 2 - 1, 202, 2, ROW_Y[1] - 202),
                    "SurfaceLight")
    bus2_y = ROW_Y[1] + NODE_H + STUB_H - 1
    M.spark_line(panel, "Route2/Bus", "Route2Bus", TL(cx, bus2_y, NODE_X[3] + NODE_W // 2 - cx + 1, 2), grow_delay=0.5)
    for i, x in enumerate(COL_X):
        themed_line(panel, f"Route2/Drop{i + 1}", f"Route2Drop{i + 1}",
                    TL(x + COL_W // 2 - 1, bus2_y + 2, 2, ROW_Y[2] - bus2_y - 2), "SurfaceLight")


def footer(panel, kind):
    themed_line(panel, "Rule", "Rule", TL(30, FOOT_Y, 840, 1), "SurfaceDark")
    status = panel.child(N("Status", "Status", TL(30, FOOT_Y + 22, 560, 28)))
    rdy = status.child(N("Status/Ready", "Ready", STRETCH))
    rdy.add("cg", 225, canvas_group(True))
    label(rdy, "Ready to launch", "Body", "TextLight")
    why = status.child(N("Status/Reason", "Reason", STRETCH))
    why.add("cg", 225, canvas_group(False))
    label(why, "", "Body", "Purchase")
    wait = status.child(N("Status/Waiting", "Waiting", STRETCH))
    wait.add("cg", 225, canvas_group(False)).add("amb", 114, M.ambient("Breathe", period=1.6, amplitude=0.5))
    label(wait, "Waiting for others...", "Body", "ControlActive")
    chips = panel.child(N("Chips", "Chips", TL(30, FOOT_Y + 60, 560, 26)))
    chips.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 3, (0, 0), (0, 0)))
    chip_node(chips, "Chips/Vessel", "Vessel", "Maelstrom" if kind == "maelstrom" else "Squirrel")
    chip_node(chips, "Chips/Intensity", "Intensity", "Intensity 1")
    chip_node(chips, "Chips/Pilots", "Pilots", "1 pilot")
    w, h = K.button_size("START GAME")
    start = panel.child(N("Start", "Start", TR(30, FOOT_Y + 26, w, h)))
    start.children.append(Inst("StartButton", "KitButton", "StartButton", STRETCH,
                               K.button_mods("KitButton", "Play", True, "START GAME"),
                               added=[("mask", 114, A.rect_mask())],
                               added_nodes=[sweep_node()]))


def sweep_node():
    """The highlight sweep across Start (ambient, Q1): a KitSpark with a pause between passes,
    clipped by the button's RectMask2D and kept out of its layout."""
    n = N("StartSweep", "Sweep", R((0, 0), (0, 1), (-48, 0), (48, 0), (0, 0.5)))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.35), K.kit_sprite("kit_spark"))) \
        .add("le", 114, le(ignore=True)).add("sp", 114, M.spark_body(False, period=3.2, pause=0.45, delay=1.0))
    return n


AI_Y, AI_H, AI_BTN_H = 584, 96, 52
AI_LEVELS = (("Easy", 1), ("Medium", 2), ("Hard", 3))   # AIDifficulty (Assets/_Scripts/Data), easiest first


def ai_row(body):
    """The AI DIFFICULTY row (AIDifficultyPicker, bleeding-edge 2026-10-14) drawn in style B: three
    plates under the domain columns, one lit. The panel shows it only on a card whose AI reads it
    (AIDifficultyRules.IsOfferedFor - Skim Race, Slingshot, Warpline), so it ships HIDDEN; the
    picker's CanvasGroup is the one ArcadeLaunchPanel.SetAIDifficultyAvailable drives, which also
    takes the buttons out of pad navigation while it is hidden."""
    row = body.child(N("AIRow", "AIDifficulty", TL(30, AI_Y, 840, AI_H)))
    row.add("cg", 225, canvas_group(False)).add("in", 114, M.entrance("Rise", stagger=9))
    label(row.child(N("AIRow/Caption", "Caption", TL(0, 0, 150, 18))), "AI pilots", "Label", "TextLight")
    label(row.child(N("AIRow/Hint", "Hint", TL(0, 22, 150, 60))), "How hard the AI races. The host picks.",
          "BodySmall", "NeutralLightest", valign="top", wrap=True)
    for i, (name, _) in enumerate(AI_LEVELS):
        key = f"AIRow/{name}"
        opt = row.child(N(key, name, TL(COL_X[i] - 30, 4, COL_W, AI_BTN_H)))
        plate = opt.child(N(f"{key}/Plate", "Plate", STRETCH))
        plate.add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver10_line1"), True, raycast=True))
        cap = opt.child(N(f"{key}/Label", "Label", STRETCH))
        cap.add("cr", 222, K.canvas_renderer()) \
            .add("tmp", 114, text(name, "Label", tok("TextLight"), align="center")) \
            .add("kt", 114, K.kit_text("Label", "TextLight", driven=True))
        opt.add("audio", 114, A.menu_audio(2)) \
            .add("button", 114, lambda ctx, go, key=key: mono(go, A.BUTTON,
                 A.selectable().replace("  m_Transition: 0\n", "  m_Transition: 1\n")
                 .replace("    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n",
                          "    m_NormalColor: {r: 0.8, g: 0.8, b: 0.8, a: 1}\n")
                 + f"  m_TargetGraphic: {ref(ctx.c(f'{key}/Plate', 'img'))}\n"
                 + A.calls([(ctx.c(key, "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])))
    row.add("picker", 114, lambda ctx, go: mono(go, S["aipicker"],
            "  options:\n" + "".join(
                f"  - Difficulty: {val}\n    Button: {ref(ctx.c(f'AIRow/{name}', 'button'))}\n"
                f"    Plate: {ref(ctx.c(f'AIRow/{name}/Plate', 'img'))}\n"
                f"    Label: {ref(ctx.c(f'AIRow/{name}/Label', 'tmp'))}\n" for name, val in AI_LEVELS)
            + f"  plateSelected: {K.sprite_ref(K.kit_sprite('kit_sliver10_fill'))}\n"
            f"  plateUnselected: {K.sprite_ref(K.kit_sprite('kit_sliver10_line1'))}\n"
            f"  labelSelected: {color('SurfaceBlack')}\n  labelUnselected: {color('TextLight')}\n"))


def tree_panel(root, kind):
    panel = root.child(N("Tree", "Tree", R((0.5, 1), (1, 1), (0, -LEFT_TOP), (-60, COL_H), (0.5, 1))))
    panel.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise"))
    fill = panel.child(N("Tree/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.92), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("th", 114, themed("SurfaceVeryDark", 0.92))
    grid = M.grid_node("Tree/Grid")
    panel.child(grid)
    bord = panel.child(N("Tree/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True)) \
        .add("th", 114, themed("SurfaceDark"))
    M.bracket_nodes(panel, "Tree/Bracket")
    # The canvas is laid out at 900 wide; the tree's nodes sit at fixed offsets from its left edge.
    body = panel.child(N("Tree/Body", "Body", TL(0, 0, PANEL_W, COL_H)))

    if kind == "maelstrom":
        gutter(body, 0, "01", "Intensity", "Value", "1", "Higher levels unlock as you play.")
        gutter(body, 1, "02", "Pilots", "Value", "1 of 4", "Pick your domain; fill open seats with AI.")
    else:
        gutter(body, 0, "01", "Vessel", "Value", "Squirrel",
               "Every pilot flies a different hull." if kind == "arena" else "Set by the card.")
        gutter(body, 1, "02", "Intensity", "Value", "1", "Higher levels unlock as you play.")
        gutter(body, 2, "03", "Pilots", "Value", "1 of 4", "Pick your domain; fill open seats with AI.")
        if kind == "arena":
            hull_row(body)
            routes(body, ROW_Y[0] + HULL_H + 4)
        else:
            vessel_row(body)
            routes(body, ROW_Y[0] + NODE_H)
            ai_row(body)

    rows = (0, 1) if kind == "maelstrom" else (1, 2)
    for i, x in enumerate(NODE_X):
        body.children.append(Inst(f"Intensity{i + 1}", "ConfigureIntensityNode", f"Intensity{i + 1}",
                                  TL(x, ROW_Y[rows[0]], NODE_W, NODE_H),
                                  [("root#in", "staggerIndex", i + 2, None)]))
    if kind == "maelstrom":
        bus_y = ROW_Y[0] + NODE_H + STUB_H - 1
        cx = NODE_X[0] + NODE_W // 2 - 1
        M.spark_line(body, "Route2/Bus", "Route2Bus", TL(cx, bus_y, NODE_X[3] + NODE_W // 2 - cx + 1, 2), grow_delay=0.4)
        for i, x in enumerate(COL_X):
            themed_line(body, f"Route2/Drop{i + 1}", f"Route2Drop{i + 1}",
                        TL(x + COL_W // 2 - 1, bus_y + 2, 2, ROW_Y[1] - bus_y - 2), "SurfaceLight")
    for i, (dname, dval) in enumerate(DOMAINS):
        body.children.append(Inst(f"Domain{dname}", "ConfigureDomainColumn", f"{dname}Column",
                                  TL(COL_X[i], ROW_Y[rows[1]], COL_W, TILE_H + 6 + SEAT_H),
                                  [("root#dtile", "domain", dval, None), ("root#in", "staggerIndex", i + 6, None)]))
    if kind == "maelstrom":
        pool = body.child(N("Pool", "Pool", TL(30, 446, 840, 262)))
        pool.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=5))
        section_label(pool, "Pool/Title", "Title", "Game pool", TL(0, 0, 300, 18))
        label(pool.child(N("Pool/Summary", "Summary", TR(0, 0, 300, 18))), "10 of 16 games", "Label", "NeutralLightest",
              align="right")
        sc = pool.child(N("Pool/Scroll", "Scroll", R((0, 0), (1, 1), (0, -14), (0, -28))))
        sc.add("scroll", 114, A.scroll_rect("Pool/Scroll/Viewport/Content", "Pool/Scroll/Viewport", 20))
        vp = sc.child(N("Pool/Scroll/Viewport", "Viewport", STRETCH))
        vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
            .add("mask", 114, A.rect_mask())
        content = vp.child(N("Pool/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
        content.add("vlg", 114, K.hv_layout(True, (0, 4, 0, 0), 6, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
        pool.add("list", 114, lambda ctx, go: mono(go, S["pool"],
                 f"  rowContainer: {ref(ctx.rt('Pool/Scroll/Viewport/Content'))}\n"
                 f"  rowPrefab: {K.ref(K.FileIds('ConfigurePoolRow')('root#entry'), asset_guid('ConfigurePoolRow'))}\n"
                 f"  tournamentData: {K.ref(11400000, TOURNAMENT, 2)}\n"
                 f"  summaryText: {ref(ctx.c('Pool/Summary', 'tmp'))}\n"
                 "  summaryFormat: '{0} of {1} games'\n"))
    footer(body, kind)
    return panel


def build_view(kind):
    root = N("root", VIEW_NAME[kind], STRETCH)
    # Near-opaque (the owner, 2026-10-15: "make the bg of these tabs a little less transparent"):
    # at 72% the Arcade's own title and game grid read through the window.
    image_node(root, "Veil", "Veil", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=0.94, raycast=True)

    # Sibling order is FOCUS order (ScreenSwitcher.Refocus takes the first live Selectable): the
    # tree comes first so the pad lands on an intensity node or a domain, never on Back.
    tree_panel(root, kind)
    left = root.child(N("Left", "Left", R((0, 1), (0.5, 1), (10, -LEFT_TOP), (-60, COL_H), (0.5, 1))))
    preview_window(left, kind)
    if kind == "maelstrom":
        briefing(left, PREVIEW_H + 20, COL_H - PREVIEW_H - 20)
        format_block(left, PREVIEW_H + 20, COL_H - PREVIEW_H - 20)
    else:
        objective_bar(left)
        lower = PREVIEW_H + 20 + OBJECTIVE_H + 20
        briefing(left, lower, COL_H - lower)
        controls(left, lower, COL_H - lower)
    header(root, kind)

    root.add("panel", 114, lambda ctx, go: panel_body(ctx, go, kind))
    return root


def vessel_names(ctx, kind):
    out = []
    if kind != "maelstrom":
        out += [ctx.c("Step1/Value", "tmp"), ctx.c("ClassInfo/Name", "tmp"), ctx.c("Chips/Vessel/Label", "tmp"),
                ctx.c("Preview/Strip/Vessel", "tmp"), ctx.c("Controls/Vessel", "tmp")]
        if kind == "arcade":
            out.insert(2, ctx.c("VesselNode/Caption", "tmp"))
    return out


def panel_body(ctx, go, kind):
    nodes = [ctx.iref(f"Intensity{i + 1}", "root#node") for i in range(4)] + [0, 0, 0]
    tiles = [ctx.iref(f"Domain{d}", "root#dtile") for d, _ in DOMAINS]
    step_int = 1 if kind == "maelstrom" else 2
    step_pil = 2 if kind == "maelstrom" else 3
    fields = (f"  gameNameText: {ref(ctx.c('Header/Title', 'tmp'))}\n"
              f"  briefing: {ref(ctx.c('Briefing', 'view'))}\n"
              f"  favoriteIcon: {ref(ctx.c('Header/Star', 'fav'))}\n"
              + refs("intensityButtons", nodes) + refs("domainTiles", tiles)
              + f"  startButton: {ref(ctx.iref('StartButton', 'root#button'))}\n"
              f"  waitingForOthersLabel: {ref(ctx.go('Status/Waiting'))}\n"
              f"  leaderboardButton: {ref(ctx.iref('LeaderboardButton', 'root#button'))}\n"
              f"  startUnavailableLabel: {ref(ctx.c('Status/Reason', 'tmp'))}\n"
              "  hostModal: {fileID: 0}\n  lobbyRow: {fileID: 0}\n  fillWithAIToggle: {fileID: 0}\n"
              + refs("vesselNameTexts", vessel_names(ctx, kind))
              + f"  eyebrowText: {ref(ctx.c('Header/Eyebrow', 'tmp'))}\n"
              f"  vesselDescriptionText: {ref(ctx.c('ClassInfo/Description', 'tmp') if kind != 'maelstrom' else 0)}\n"
              f"  vesselArt: {ref(ctx.c('VesselNode/Clip/Art', 'cover') if kind == 'arcade' else 0)}\n"
              f"  intensityValueText: {ref(ctx.c(f'Step{step_int}/Value', 'tmp'))}\n"
              f"  intensityChipText: {ref(ctx.c('Chips/Intensity/Label', 'tmp'))}\n"
              f"  seatValueText: {ref(ctx.c(f'Step{step_pil}/Value', 'tmp'))}\n"
              f"  pilotsChipText: {ref(ctx.c('Chips/Pilots/Label', 'tmp'))}\n"
              f"  readyLabel: {ref(ctx.go('Status/Ready'))}\n")
    if kind == "arcade":
        fields += f"  aiDifficultyPicker: {ref(ctx.c('AIRow', 'picker'))}\n"
    if kind in ("arcade", "arena"):
        fields += (f"  previewWindow: {ref(ctx.c('Preview', 'window'))}\n"
                   f"  controlsPanel: {ref(ctx.c('Controls', 'controls'))}\n"
                   f"  objectiveBox: {ref(ctx.c('Objective', 'view'))}\n"
                   f"  microToast: {ref(ctx.c('Preview', 'toast'))}\n")
    if kind == "arena":
        fields += (f"  roster: {K.ref(11400000, ARENA_ROSTER, 2)}\n"
                   "  vesselIcon: {fileID: 0}\n  prevButton: {fileID: 0}\n  nextButton: {fileID: 0}\n"
                   f"  selectVesselButton: {ref(ctx.iref('ClaimButton', 'root#button'))}\n"
                   + refs("hullTiles", [ctx.iref(f"Hull{i + 1}", "root#hull") for i in range(HULL_SLOTS)]))
    if kind == "maelstrom":
        fields += (f"  videoView: {ref(ctx.c('Preview', 'video'))}\n"
                   f"  poolList: {ref(ctx.c('Pool', 'list'))}\n"
                   f"  tournamentData: {K.ref(11400000, TOURNAMENT, 2)}\n"
                   f"  winTargetText: {ref(ctx.c('Format/WinTarget/Value', 'tmp'))}\n"
                   f"  gameCapText: {ref(ctx.c('Format/GameCap/Value', 'tmp'))}\n"
                   f"  pointsByPlaceText: {ref(ctx.c('Format/Points/Value', 'tmp'))}\n"
                   f"  seatsText: {ref(ctx.c('Format/Seats/Value', 'tmp'))}\n")
    return mono(go, S[PANEL_CLASS[kind]], fields)


# --------------------------------------------------------------------------------------------
# Prefab outputs
# --------------------------------------------------------------------------------------------


# --------------------------------------------------------------------------------------------
# The weekly leaderboard - LeaderboardRow / LeaderboardView (item 4b)
# --------------------------------------------------------------------------------------------

LB_ROW_W, LB_ROW_H = 1336, 58
REWARD_BANDS = ((1, 1, "1st", "100"), (2, 2, "2nd", "50"), (3, 3, "3rd", "20"), (4, 20, "4th - 20th", "10"),
                (21, 0, "21st and below", "5"))
PODIUM = (("Podium1", 1, "Purchase"), ("Podium2", 2, "TextLight"), ("Podium3", 3, "Purchase"))
TABS = (("World", "chrome_globe", "World"), ("Regional", "chrome_pin", "Regional"), ("Friends", "chrome_people", "Friends"))
ALL_GAMES = A.ALL_GAMES
PROFILE_ICONS = A.PROFILE_ICONS


def color(tok_name, alpha=1.0):
    return K.col(tok(tok_name, alpha))


def build_leaderboard_row():
    root = N("root", "LeaderboardRow", R(size=(LB_ROW_W, LB_ROW_H)))
    root.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark", 0.35)))
    root.add("cg", 225, canvas_group(True)).add("le", 114, le(ph=LB_ROW_H)) \
        .add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    rank = root.child(N("Rank", "Rank", R((0, 0.5), None, (32, 0), (60, 28), (0, 0.5))))
    rank.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("4", "Data", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("Data", "TextLight", driven=True))
    image_node(root, "AvatarBack", "AvatarBack", R((0, 0.5), None, (156, 0), (40, 40)), "SurfaceDark",
               K.kit_sprite("kit_oct_fill"))
    art_node(root, "Avatar", "Avatar", R((0, 0.5), None, (156, 0), (38, 38)), AVATAR_PLACEHOLDER, preserve=True)
    name = root.child(N("Name", "Name", R((0, 0.5), None, (196, 0), (700, 26), (0, 0.5))))
    name.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("PILOT", "Body", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Body", "TextLight", driven=True))
    t = root.child(N("Time", "Time", R((1, 0.5), None, (-24, 0), (200, 28), (1, 0.5))))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("1:04.88", "Data", tok("TextLight"), align="right")) \
        .add("kt", 114, K.kit_text("Data", "TextLight", driven=True))
    image_node(root, "Rule", "Rule", R((0, 0), (1, 0), (0, 0), (0, 1), (0.5, 0)), "SurfaceDark")
    return root


def podium_slot(parent, key, rank, accent, amin, amax, top, height):
    n = parent.child(N(key, key, R(amin, amax, (0, -top), (0, height), (0.5, 1))))
    n.add("cg", 225, canvas_group(False)).add("in", 114, M.entrance("Pop" if rank == 1 else "Rise", stagger=rank))
    first = rank == 1
    frame(n, f"{key}/Frame", 14, "SurfaceVeryDark", accent if first else "SurfaceDark")
    if first:
        n.children[-1].comps = [(k, cid, b) if k != "img" else
                                ("img", 114, K.image(tok(accent), K.kit_sprite("kit_sliver14_line2"), True))
                                for k, cid, b in n.children[-1].comps]
    badge = image_node(n, f"{key}/Badge", "Badge", R((0, 0.5), None, (46, 0), (46, 46)), accent,
                       K.kit_sprite("kit_hex_fill"), alpha=1.0 if rank != 3 else 0.55, preserve=True)
    num = badge.child(N(f"{key}/Badge/Num", "Num", STRETCH))
    label(num, str(rank), "H3", "SurfaceBlack", align="center")
    image_node(n, f"{key}/AvatarBack", "AvatarBack", R((0, 0.5), None, (122, 0), (62, 62)), "SurfaceDark",
               K.kit_sprite("kit_oct_fill"))
    av = art_node(n, f"{key}/Avatar", "Avatar", R((0, 0.5), None, (122, 0), (58, 58)), AVATAR_PLACEHOLDER, preserve=True)
    if first:
        av.add("amb", 114, M.ambient("Float", period=3.4, amplitude=5))
    nm = n.child(N(f"{key}/Name", "Name", R((0, 0.5), (1, 0.5), ((170 - 20) / 2, 14), (-(170 + 20), 26))))
    nm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("PILOT", "H3", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H3", "TextLight"))
    tm = n.child(N(f"{key}/Time", "Time", R((0, 0.5), None, (170, -16), (200, 26), (0, 0.5))))
    label(tm, "0:58.12", "Data", "TextLight")
    n.add("slot", 114, lambda ctx, go, k=key: mono(go, S["podium"],
          f"  group: {ref(ctx.c(k, 'cg'))}\n"
          f"  avatar: {ref(ctx.c(k + '/Avatar', 'img'))}\n"
          f"  nameText: {ref(ctx.c(k + '/Name', 'tmp'))}\n"
          f"  timeText: {ref(ctx.c(k + '/Time', 'tmp'))}\n"))
    return n


def reward_band(parent, i, lo, hi, caption, amount):
    key = f"Rewards/Band{i + 1}"
    n = parent.child(N(key, f"Band{i + 1}", TL(22, 52 + i * 48, 376, 44)))
    mark = n.child(N(f"{key}/Mark", "Mark", STRETCH))
    mark.add("cg", 225, canvas_group(False))
    frame(mark, f"{key}/Mark/Frame", 10, "SurfaceDark", "ControlActive")
    label(mark.child(N(f"{key}/Mark/You", "You", R((0, 0.5), None, (150, 0), (60, 16), (0, 0.5)))), "You", "Label",
          "ControlActive")
    label(n.child(N(f"{key}/Rank", "Rank", R((0, 0.5), None, (14, 0), (200, 24), (0, 0.5)))), caption, "Body", "TextLight")
    label(n.child(N(f"{key}/Amount", "Amount", R((1, 0.5), None, (-40, 0), (100, 26), (1, 0.5)))), amount, "Data",
          "Purchase", align="right")
    ic = image_node(n, f"{key}/Crystal", "Crystal", R((1, 0.5), None, (-14, 0), (18, 18), (1, 0.5)), "Purchase",
                    K.chrome_sprite("chrome_crystal"), preserve=True)
    n.add("band", 114, lambda ctx, go: mono(go, S["band"],
          f"  minRank: {lo}\n  maxRank: {hi}\n  localMark: {ref(ctx.c(key + '/Mark', 'cg'))}\n"))
    return n


def tab_node(parent, i, key, icon, caption):
    k = f"Tabs/{key}"
    n = parent.child(N(k, key, TL(i * 120, 0, 120, 108)))
    ring = n.child(N(f"{k}/FocusRing", "FocusRing", R((0.5, 1), None, (0, 6), (100, 88), (0.5, 1))))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Cta", 0.0), K.kit_sprite("kit_hex_line2")))
    fill = n.child(N(f"{k}/Fill", "Fill", R((0.5, 1), None, (0, 0), (88, 76), (0.5, 1))))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_hex_fill"), raycast=True))
    bord = n.child(N(f"{k}/Border", "Border", R((0.5, 1), None, (0, 0), (88, 76), (0.5, 1))))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_hex_line2")))
    ic = n.child(N(f"{k}/Icon", "Icon", R((0.5, 1), None, (0, -22), (32, 32), (0.5, 1))))
    ic.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite(icon), preserve=True))
    lab = n.child(N(f"{k}/Label", "Label", R((0, 0), (1, 0), (0, 0), (0, 18), (0.5, 0))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text(caption.upper(), "Label", tok("NeutralLightest"), align="center")) \
        .add("kt", 114, K.kit_text("Label", driven=True))
    n.add("tile", 114, A.tile_body(c(f"{k}/FocusRing", "img"), c(f"{k}/Fill", "img"), c(f"{k}/Border", "img"), 14,
                                   primary=[c(f"{k}/Icon", "img"), c(f"{k}/Label", "tmp")], unsel_primary="NeutralLightest",
                                   lock=None))
    # The hex tile keeps the 2px band in both states (KitTile's hex convention).
    body = n.comps[-1][2]
    n.comps[-1] = ("tile", 114, lambda ctx, go, b=body: b(ctx, go).replace(
        K.sprite_ref(K.kit_sprite("kit_sliver14_line1")), K.sprite_ref(K.kit_sprite("kit_hex_line2"))).replace(
        K.sprite_ref(K.kit_sprite("kit_sliver14_line2")), K.sprite_ref(K.kit_sprite("kit_hex_line2"))))
    n.add("audio", 114, A.menu_audio(11))
    return n


def build_leaderboard_view():
    root = N("root", "LeaderboardView", STRETCH)
    image_node(root, "Veil", "Veil", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=0.72, raycast=True)

    right = root.child(N("Board", "Board", R((0, 1), (1, 1), ((500 - 40) / 2, -LEFT_TOP), (-(500 + 40), COL_H), (0.5, 1))))
    tabs = right.child(N("Tabs", "Tabs", TL(8, 0, 360, 108)))
    tabs.add("in", 114, M.entrance("Rise")).add("cg", 225, canvas_group(True))
    for i, (key, icon, cap) in enumerate(TABS):
        tab_node(tabs, i, key, icon, cap)
    thirds = (((0, 1), (0.326, 1)), ((0.337, 1), (0.663, 1)), ((0.674, 1), (1, 1)))
    podium_slot(right, "Podium1", 1, "Purchase", thirds[1][0], thirds[1][1], 112, 130)
    podium_slot(right, "Podium2", 2, "TextLight", thirds[0][0], thirds[0][1], 132, 110)
    podium_slot(right, "Podium3", 3, "Purchase", thirds[2][0], thirds[2][1], 132, 110)

    lst = right.child(N("List", "List", R((0, 1), (1, 1), (0, -260), (0, COL_H - 260), (0.5, 1))))
    lst.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=4))
    fill = lst.child(N("List/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.92), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("th", 114, themed("SurfaceVeryDark", 0.92))
    lst.child(M.grid_node("List/Grid"))
    bord = lst.child(N("List/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True)).add("th", 114, themed("SurfaceDark"))
    M.bracket_nodes(lst, "List/Bracket")
    section_label(lst, "List/HeadRank", "HeadRank", "Rank", TL(42, 28, 100, 18), "NeutralLightest")
    section_label(lst, "List/HeadPilot", "HeadPilot", "Pilot", TL(126, 28, 200, 18), "NeutralLightest")
    section_label(lst, "List/HeadTime", "HeadTime", "Time", TR(42, 28, 120, 18), "NeutralLightest", align="right")
    themed_line(lst, "List/HeadRule", "HeadRule", R((0, 1), (1, 1), (0, -54), (-44, 1), (0.5, 1)), "SurfaceDark")
    sc = lst.child(N("List/Scroll", "Scroll", R((0, 0), (1, 1), (0, -30), (-44, -100))))
    sc.add("scroll", 114, A.scroll_rect("List/Scroll/Viewport/Content", "List/Scroll/Viewport", 20))
    vp = sc.child(N("List/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, A.rect_mask())
    content = vp.child(N("List/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 4, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    empty = lst.child(N("List/Empty", "Empty", R((0.5, 0.5), None, (0, -20), (700, 90))))
    empty.add("cg", 225, canvas_group(False))
    label(empty.child(N("List/Empty/Text", "Text", TL(0, 0, 700, 36))), "No times yet - be the first.", "H3", "TextLight",
          align="center")
    label(empty.child(N("List/Empty/Hint", "Hint", TL(0, 44, 700, 40))),
          "Finish this week's challenge to put a time on the board.", "Body", "NeutralLightest", align="center")

    left = root.child(N("Side", "Side", TL(40, LEFT_TOP, 420, COL_H)))
    best = left.child(N("Best", "Best", TL(0, 0, 420, 200)))
    best.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    frame(best, "Best/Frame", 14)
    M.bracket_nodes(best, "Best/Bracket")
    section_label(best, "Best/Title", "Title", "Your best run", TL(22, 22, 300, 18))
    label(best.child(N("Best/Rank", "Rank", TL(22, 46, 130, 58))), "-", "Display", "TextLight")
    label(best.child(N("Best/Time", "Time", TL(150, 66, 220, 30))), "--:--.--", "Data", "NeutralLightest")
    label(best.child(N("Best/Line", "Line", TL(22, 124, 376, 52))),
          "Your rank on this board and your best time this week. One attempt a day.", "BodySmall", "NeutralLightest",
          valign="top", wrap=True)
    rw = left.child(N("Rewards", "Rewards", TL(0, 220, 420, 340)))
    rw.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    frame(rw, "Rewards/Frame", 14)
    M.bracket_nodes(rw, "Rewards/Bracket")
    section_label(rw, "Rewards/Title", "Title", "Rewards", TL(22, 22, 300, 18))
    for i, band in enumerate(REWARD_BANDS):
        reward_band(rw, i, *band)
    label(rw.child(N("Rewards/Note", "Note", TL(22, 300, 376, 22))), "Paid in crystals when the week closes.",
          "BodySmall", "NeutralLightest")

    hd = root.child(N("Header", "Header", R((0, 1), (1, 1), (0, -108), (-80, 64), (0.5, 1))))
    hd.add("in", 114, M.entrance("Rise", distance=10)).add("cg", 225, canvas_group(True))
    label(hd.child(N("Header/Eyebrow", "Eyebrow", TL(0, 0, 600, 18))), "Weekly challenge - Leaderboard", "Label",
          "NeutralLightest")
    label(hd.child(N("Header/Title", "Title", TL(0, 20, 1100, 46))), "Weekly challenge", "H1", "TextLight")
    A.back_button(hd)
    back_w = 16 + (K.label_width("B", "Label") + 24) + 10 + K.label_width("BACK", "ButtonSmall") + 16
    cd = hd.child(N("Header/Countdown", "Countdown", R((1, 0), None, (-(back_w + 24), 6), (200, 28), (1, 0))))
    label(cd, "6d 03:12:09", "Data", "TextLight", align="right")
    label(hd.child(N("Header/EndsIn", "EndsIn", R((1, 0), None, (-(back_w + 24 + 212), 10), (120, 18), (1, 0)))),
          "Ends in", "Label", "NeutralLightest", align="right")

    lst.add("panel", 114, lambda ctx, go: mono(go, S["lbpanel"],
            f"  rowContainer: {ref(ctx.rt('List/Scroll/Viewport/Content'))}\n"
            f"  rowTemplate: {K.ref(K.FileIds('LeaderboardRow')('root#rt'), asset_guid('LeaderboardRow'))}\n"
            "  rowCount: 10\n"
            f"  templateRank: {K.ref(K.FileIds('LeaderboardRow')('Rank#tmp'), asset_guid('LeaderboardRow'))}\n"
            f"  templateAvatar: {K.ref(K.FileIds('LeaderboardRow')('Avatar#img'), asset_guid('LeaderboardRow'))}\n"
            f"  templateName: {K.ref(K.FileIds('LeaderboardRow')('Name#tmp'), asset_guid('LeaderboardRow'))}\n"
            f"  templateScore: {K.ref(K.FileIds('LeaderboardRow')('Time#tmp'), asset_guid('LeaderboardRow'))}\n"
            f"  templateBackground: {K.ref(K.FileIds('LeaderboardRow')('root#img'), asset_guid('LeaderboardRow'))}\n"
            "  titleText: {fileID: 0}\n"
            f"  emptyState: {ref(ctx.go('List/Empty'))}\n"
            f"  emptyStateText: {ref(ctx.c('List/Empty/Text', 'tmp'))}\n"
            "  emptyBoardMessage: No times yet - be the first.\n  loadingMessage: Loading...\n"
            f"  profileIcons: {K.ref(11400000, PROFILE_ICONS, 2)}\n"
            f"  localRowColor: {color('ControlActive')}\n  rowColor: {color('TextLight')}\n"
            "  localNameSuffix: ' *'\n"
            "  podiumColors:\n  - {r: 1, g: 0.84, b: 0.25, a: 1}\n  - {r: 0.78, g: 0.82, b: 0.86, a: 1}\n"
            "  - {r: 0.8, g: 0.53, b: 0.3, a: 1}\n"
            + refs("podium", [ctx.c(k, "slot") for k, _, _ in PODIUM])
            + refs("rewardBands", [ctx.c(f"Rewards/Band{i + 1}", "band") for i in range(len(REWARD_BANDS))])
            + f"  bestRankText: {ref(ctx.c('Best/Rank', 'tmp'))}\n"
            f"  bestTimeText: {ref(ctx.c('Best/Time', 'tmp'))}\n"
            f"  localRowBackground: {color('SurfaceDark')}\n"
            "  rowFadeDuration: 0.22\n  rowStagger: 0.04\n  maxStaggerTotal: 0.32\n  rowStartScale: 0.94\n"))
    return root


BUILDERS = {"ConfigureSeatChip": build_seat_chip, "ConfigureAbilityRow": build_ability_row,
            "ConfigurePoolRow": build_pool_row, "ConfigureIntensityNode": build_intensity_node,
            "ConfigureDomainColumn": build_domain_column, "ConfigureHullTile": build_hull_tile,
            "ConfigureView": lambda: build_view("arcade"), "ConfigureArenaView": lambda: build_view("arena"),
            "ConfigureMaelstromView": lambda: build_view("maelstrom"),
            "LeaderboardRow": build_leaderboard_row, "LeaderboardView": build_leaderboard_view}


def prefab_outputs():
    out = {}
    for name in PREFABS:
        out[prefab_path(name)] = prefab_text(BUILDERS[name](), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Configure"))
    return out


# --------------------------------------------------------------------------------------------
# The ArcadeGameConfigureModal.prefab and Menu_Main.unity patches
# --------------------------------------------------------------------------------------------

# Objects the patches edit, measured 2026-10-14 (names and scripts are asserted before any write).
PREFAB_IDS = dict(root_go="3548093454574644931", content_go="7262487156279510012", modal="4357675176818844460",
                  retired={"4226051420078605782": "Border", "60959156505399860": "ConfigurationContent"})
SCENE_PI = "2098022921"
WINDOWS = {
    "arena": dict(root_go="3044068640210271855", content_go="9208280041627865936", modal="2844720265101833088",
                  retired={"2424655266495518074": "Border", "2007332722038735512": "ConfigurationContent"},
                  name="ArenaGameConfigureModal", panel_slot=0),
    "maelstrom": dict(root_go="2862063298324705436", content_go="8236171957964135843", modal="3097589415591017843",
                      retired={"3179743876454734729": "Border", "1611683190258469995": "ConfigurationContent"},
                      name="MaelstromGameConfigurationModal", panel_slot=2),
}
# Modal fields that named objects inside the retired subtrees, and what each becomes.
MODAL_REFS = {"selectedGameName": ("Header/Title#tmp", "tmp"), "selectedGameDescription": ("Briefing/Body#tmp", "tmp"),
              "selectedGameFavoriteIcon": ("Header/Star#fav", "favorite")}
MODAL_DROPPED = ("pcStepper", "dcStepper")       # the hidden steppers the d-pad used to drive


class Mount:
    """One view instance mounted under a window's Content, and the stubs its referrers need."""

    def __init__(self, file_key, view):
        self.view = view
        self.ctx = Ctx(file_key)
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = asset_guid(view)
        self.kind = {"ConfigureView": "minigame", "ConfigureArenaView": "arena", "ConfigureMaelstromView": "maelstrom"}[view]

    def src(self, key):
        return K.FileIds(self.view)(key)

    def stub_fid(self, key):
        return str(derived(self.src(key), self.inst))

    def stub_text(self, key, comp):
        return stub_doc(self.stub_fid(key), comp, self.src(key), self.guid, self.inst)

    def panel(self):
        return self.stub_fid("root#panel")

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", self.view, self.guid, self.view, STRETCH, mods), parent_rt, out)
        out.append(self.stub_text("root#panel", self.kind))
        for key, comp in MODAL_REFS.values():
            out.append(self.stub_text(key, comp))
        return [K.unity_empty_scalars(d) for d in out]

    def ids(self):
        return {self.inst, self.stub, self.panel()} | {self.stub_fid(k) for k, _ in MODAL_REFS.values()}


def field_set(doc, name, value):
    pat = re.compile(r"^  " + re.escape(name) + r":.*(?:\n    .*)?$", re.M)
    found = pat.findall(doc)
    assert len(found) == 1, f"field {name} not found exactly once"
    return pat.sub(lambda _m: f"  {name}: {value}", doc)


def stretch_rt(doc):
    for f, v in (("m_AnchorMin", "{x: 0, y: 0}"), ("m_AnchorMax", "{x: 1, y: 1}"), ("m_AnchoredPosition", "{x: 0, y: 0}"),
                 ("m_SizeDelta", "{x: 0, y: 0}"), ("m_Pivot", "{x: 0.5, y: 0.5}")):
        doc = field_set(doc, f, v)
    return doc


def chip_ref():
    return f"{{fileID: {K.FileIds('ConfigureSeatChip')('root#chip')}, guid: {asset_guid('ConfigureSeatChip')}, type: 3}}"


def rewrite_modal(doc, mount, modal_type=None, panels=None):
    for field, (key, _) in MODAL_REFS.items():
        doc = field_set(doc, field, ref(mount.stub_fid(key)))
    for field in MODAL_DROPPED:
        doc = field_set(doc, field, "{fileID: 0}")
    doc = field_set(doc, "chipPrefab", chip_ref())
    if modal_type is not None:
        doc = field_set(doc, "ModalType", str(modal_type))
    if panels is not None:
        doc = T.set_block(doc, "launchPanels", K.refs_list("launchPanels", panels))
    return doc


def subtree(sc, go):
    """Every document under a GameObject - its components, child objects and, for a nested prefab
    instance, the PrefabInstance with every stripped stub and added object that belongs to it."""
    out = [go] + [c for c in sc.components(go) if sc.has(c)]
    for ch in sc.children(sc.rect_of(go)):
        head = sc.docs[ch].split("\n", 1)[0]
        if "stripped" in head:
            pid = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)\}", sc.docs[ch]).group(1)
            out += instance_docs(sc, pid)
        else:
            out += subtree(sc, sc.go_of(ch))
    return out


def instance_docs(sc, pid):
    out = [pid]
    for fid in sc.order:
        if fid != pid and re.search(r"m_PrefabInstance: \{fileID: " + pid + r"\}", sc.docs[fid]):
            out.append(fid)
    # Added GameObjects / components (non-stripped documents parented into the instance).
    pi = sc.docs[pid]
    for added in re.findall(r"addedObject: \{fileID: (-?\d+)\}", pi):
        if sc.has(added):
            out += subtree(sc, added if sc.cls[added] == 1 else sc.go_of(added)) if sc.cls[added] in (1, 224) else [added]
    return out


def mount_window(sc, ids, mount, mods=()):
    """Retire a window's old Content children (while they exist) and mount `mount` there."""
    assert sc.has(ids["content_go"]) and sc.field(ids["content_go"], "m_Name") == "Content", "a window's Content moved"
    migrating = any(sc.has(g) for g in ids["retired"])
    if migrating:
        clash = mount.ids() & set(sc.order)
        assert not clash, f"minted fileIDs already present: {sorted(clash)[:3]}"
    for go, name in ids["retired"].items():
        if sc.has(go):
            assert sc.field(go, "m_Name") == name, f"retired root {go} is no longer {name}"
            for fid in dict.fromkeys(subtree(sc, go)):
                if sc.has(fid):
                    sc.remove(fid)
    for fid in list(sc.order):
        if fid in mount.ids():
            sc.remove(fid)
    content_rt = sc.rect_of(ids["content_go"])
    anchor = content_rt
    for d in mount.docs(content_rt, mods):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(content_rt, stretch_rt(T.set_block(sc.docs[content_rt], "m_Children",
                                                  K.refs_list("m_Children", [mount.stub]))))


def prefab_transform(text):
    sc = T.Scene(text)
    P = PREFAB_IDS
    assert sc.field(P["root_go"], "m_Name") == "ArcadeGameConfigureModal", "the modal prefab's root moved"
    assert sc.script(P["modal"]) == S["modal"], "ArcadeGameConfigureModal moved"
    mount = Mount("ArcadeGameConfigureModal/Configure", "ConfigureView")
    mount_window(sc, P, mount)
    sc.replace(P["modal"], rewrite_modal(sc.docs[P["modal"]], mount, modal_type=1,
                                         panels=["0", mount.panel(), "0"]))
    return sc.text(), mount


def scene_transform(text, new_prefab_text):
    sc = T.Scene(text)
    mounts = {}
    for kind, ids in WINDOWS.items():
        assert sc.field(ids["root_go"], "m_Name") == ids["name"], f"{ids['name']} moved"
        assert sc.script(ids["modal"]) == S["modal"], f"{ids['name']}'s modal copy moved"
        view = "ConfigureArenaView" if kind == "arena" else "ConfigureMaelstromView"
        mount = Mount(f"Menu_Main/Configure/{kind}", view)
        mods = [("root#panel", "hostModal", None, ref(ids["modal"]))]
        mount_window(sc, ids, mount, mods)
        sc.replace(ids["modal"], rewrite_modal(sc.docs[ids["modal"]], mount, panels=[]))
        mounts[kind] = mount

    lb = LEADERBOARD
    assert sc.field(lb["root_go"], "m_Name") == lb["name"], "the leaderboard window moved"
    assert sc.script(lb["modal"]) == S["lbmodal"], "WeeklyChallengeLeaderboardModal moved"
    board = BoardMount()
    mount_window(sc, lb, board)
    sc.replace(lb["modal"], rewrite_board_modal(sc.docs[lb["modal"]], board))
    mounts["leaderboard"] = board

    # The arcade modal's instance: overrides naming objects the prefab no longer has go, and the two
    # scene panels it lists are the new Arena / Maelstrom panels.
    pi = sc.docs[SCENE_PI]
    live = set(re.findall(r"^--- !u!\d+ &(-?\d+)", new_prefab_text, re.M))
    kept, seen = [], 0
    for m in re.finditer(r"    - target: \{fileID: (-?\d+), guid: [0-9a-f]{32},?\s*type: 3\}\n(?:      .*\n)+?"
                         r"      objectReference: .*\n(?:        .*\n)?", pi):
        seen += 1
        if m.group(1) in live:
            kept.append(m.group(0))
    assert seen == pi.count("    - target:"), "the arcade modal's overrides did not all parse"
    head, rest = pi.split("    m_Modifications:\n", 1)
    tail = rest[rest.index("    m_RemovedComponents:"):]
    body = "".join(kept)
    for kind, ids in WINDOWS.items():
        body = re.sub(r"(propertyPath: 'launchPanels\.Array\.data\[" + str(ids["panel_slot"]) +
                      r"\]'\n      value: \n      objectReference: )\{fileID: -?\d+\}",
                      lambda mm, k=kind: mm.group(1) + ref(mounts[k].panel()), body)
    sc.replace(SCENE_PI, head + "    m_Modifications:\n" + body + tail)
    return sc.text(), mounts


LEADERBOARD = dict(root_go="6411554931226249784", content_go="940898935156434695", modal="6411554931226249785",
                   name="LeaderboardConfigureModal",
                   retired={"1321063439027614107": "Background", "1620340554": "Header",
                            "3510227859404491540": "LeaderboardHeader", "1589591837": "Time",
                            "1368105608": "ButtonTabs", "1559680147": "LeaderboardScrollView",
                            "6857392746860362337": "CloseButton", "2024813792": "RankRewardButton",
                            "156670760": "RankRewardPanel"})
LB_REFS = {"panel": ("List#panel", "lbpanel"), "timeLeftText": ("Header/Countdown#tmp", "tmp"),
           "challengeTitleText": ("Header/Title#tmp", "tmp"), "worldTab": ("Tabs/World#tile", "tile"),
           "regionalTab": ("Tabs/Regional#tile", "tile"), "friendsTab": ("Tabs/Friends#tile", "tile")}
LB_DROPPED = ("rankRewardButton", "rankRewardPanel", "rankRewardBackdrop", "closeButton")


class BoardMount(Mount):
    def __init__(self):
        self.view = "LeaderboardView"
        self.ctx = Ctx("Menu_Main/Leaderboard")
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = asset_guid(self.view)
        self.kind = None

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", self.view, self.guid, self.view, STRETCH, mods), parent_rt, out)
        for key, comp in LB_REFS.values():
            out.append(self.stub_text(key, comp))
        return [K.unity_empty_scalars(d) for d in out]

    def ids(self):
        return {self.inst, self.stub} | {self.stub_fid(k) for k, _ in LB_REFS.values()}


def rewrite_board_modal(doc, mount):
    for field, (key, _) in LB_REFS.items():
        doc = field_set(doc, field, ref(mount.stub_fid(key)))
    for field in LB_DROPPED:
        doc = field_set(doc, field, "{fileID: 0}")
    doc = field_set(doc, "timeLeftPrefix", "")
    # Fields added by item 4b (WeeklyChallengeLeaderboardModal.games / countdownInDays), in
    # declaration order right after challengeTitleText.
    doc = re.sub(r"^  games: .*\n|^  countdownInDays: .*\n", "", doc, flags=re.M)
    doc = re.sub(r"(^  challengeTitleText: .*\n)",
                 lambda m: m.group(1) + f"  games: {{fileID: 11400000, guid: {ALL_GAMES}, type: 2}}\n"
                 "  countdownInDays: 1\n", doc, count=1, flags=re.M)
    return doc


def outputs():
    out = prefab_outputs()
    modal_text = open(os.path.join(REPO, MODAL_PREFAB), encoding="utf-8").read()
    new_modal, _ = prefab_transform(modal_text)
    out[MODAL_PREFAB] = new_modal
    scene_text = open(os.path.join(REPO, SCENE), encoding="utf-8").read()
    out[SCENE], _ = scene_transform(scene_text, new_modal)
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

KIT_SOURCES = ("KitButton", "KitButtonSmall", "KitTile", "KitDivider", "KitKeycap", "KitCurrencyPill", "KitChip")


def known_guids():
    return T.known_guids() | PACKAGE


def prefab_source(guid, files):
    for name in PREFABS:
        if asset_guid(name) == guid:
            return files[prefab_path(name)]
    for name in KIT_SOURCES:
        if K.prefab_guid(name) == guid:
            return open(os.path.join(REPO, K.PREFAB_DIR, name + ".prefab"), encoding="utf-8").read()
    if guid == K.meta_guid(MODAL_PREFAB):
        return files[MODAL_PREFAB]
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
        objs |= {str(derived(o, pid)) for o in prefab_objects(src, files, memo)}
    memo[guid] = objs
    return objs


def check_sources(path, text, files, guids_owned):
    """Every stub, override target and added-object target names an object its prefab holds - for
    the prefabs this script authors (and the modal prefab); scene objects of other prefabs are not
    this script's to judge."""
    for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                             r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", text):
        if g in guids_owned:
            assert src in prefab_objects(g, files), f"{path}: source object {src} is not in the prefab {g}"


def comp_doc(text, fid):
    return A.doc_of(text, fid)


def wired(text, body, field, want, name):
    m = re.search(r"^  " + field + r": \{fileID: (-?\d+)", body, re.M)
    assert m and m.group(1) != "0", f"{name}.{field} is not wired"
    cls, scr = A.kind_of(text, m.group(1))
    ok = cls == want if isinstance(want, int) else scr == want
    assert ok, f"{name}.{field} points at the wrong kind of object ({cls}, {scr})"


TMP, IMG = K.TMP_UGUI, K.IMAGE
PANEL_FIELDS = dict(gameNameText=TMP, briefing=S["briefing"], favoriteIcon=S["favorite"], startButton=S["button"],
                    waitingForOthersLabel=1, leaderboardButton=S["button"], startUnavailableLabel=TMP,
                    eyebrowText=TMP, intensityValueText=TMP, intensityChipText=TMP, seatValueText=TMP,
                    pilotsChipText=TMP, readyLabel=1)
PART_FIELDS = [
    ("ConfigureSeatChip", "root#chip", dict(avatarImage=IMG, localPlayerOutline=1, nameText=TMP, roleText=TMP,
                                            kickButton=S["button"])),
    ("ConfigureAbilityRow", "root#row", dict(icon=IMG, elementPetal=IMG, nameText=TMP, descriptionText=TMP,
                                             chipGlyph=IMG, chipLabel=TMP)),
    ("ConfigurePoolRow", "root#entry", dict(icon=IMG, nameText=TMP, detailText=TMP)),
    ("ConfigureIntensityNode", "root#node", dict(tile=S["tile"], nodeGroup=225, numeralText=S["kt"],
                                                 captionText=S["kt"], meterGroup=225, meterGrow=S["entrance"],
                                                 goalText=S["kt"], goalGroup=225, routeStub=225)),
    ("ConfigureDomainColumn", "root#dtile", dict(button=S["tile"], labelText=TMP, addAiButton=S["button"],
                                                 tile=S["tile"], borderImage=IMG, accent=IMG, columnGroup=225,
                                                 lineText=S["kt"], countText=S["kt"], openSeatGroup=225,
                                                 addAiGroup=225)),
    ("ConfigureHullTile", "root#hull", dict(tile=S["tile"], art=IMG, nameText=S["kt"], group=225)),
]


def validate_prefabs(files):
    M.require_entrances(files, {prefab_path("ConfigureAbilityRow"): 1, prefab_path("LeaderboardRow"): 1})
    guids = known_guids()
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    owned = {asset_guid(n) for n in PREFABS} | {K.prefab_guid(n) for n in KIT_SOURCES}
    for path, text in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text, guids, minted)
        check_sources(path, text, files, owned)
    for name, comp, wanted in PART_FIELDS:
        text = files[prefab_path(name)]
        _, body = comp_doc(text, K.FileIds(name)(comp))
        assert body, f"{name}: no {comp}"
        for field, want in wanted.items():
            wired(text, body, field, want, name)

    # The intensity node writes the shared IntVariable, or the modal's selection never reaches the
    # session (IntensitySelectButton.SetSelected).
    node = files[prefab_path("ConfigureIntensityNode")]
    assert f"selectedIntensityCount: {{fileID: 11400000, guid: {INTENSITY_VAR}" in node, \
        "the intensity node does not write the shared intensity variable"

    # The domain strip is a layout group the modal parents chips under (DomainInfoData.avatarStrip).
    col = files[prefab_path("ConfigureDomainColumn")]
    _, dtile = comp_doc(col, K.FileIds("ConfigureDomainColumn")("root#dtile"))
    strip = re.search(r"avatarStrip: \{fileID: (-?\d+)\}", dtile).group(1)
    assert A.kind_of(col, strip) == (114, K.VLAYOUT), "the domain column's avatarStrip is not its seat VLG"

    board = files[prefab_path("LeaderboardView")]
    BV = K.FileIds("LeaderboardView")
    _, panel = comp_doc(board, BV("List#panel"))
    assert panel, "LeaderboardView: no WeeklyChallengeLeaderboardPanel"
    for field, want in (("emptyState", 1), ("emptyStateText", TMP), ("bestRankText", TMP), ("bestTimeText", TMP)):
        wired(board, panel, field, want, "LeaderboardView")
    slots = re.findall(r"^  - \{fileID: (-?\d+)\}", panel.split("  podium:")[1].split("  rewardBands:")[0], re.M)
    assert len(slots) == 3 and all(A.kind_of(board, x) == (114, S["podium"]) for x in slots), "the podium needs three slots"
    bands = re.findall(r"^  - \{fileID: (-?\d+)\}", panel.split("  rewardBands:")[1].split("  bestRankText:")[0], re.M)
    assert len(bands) == len(REWARD_BANDS) and all(A.kind_of(board, x) == (114, S["band"]) for x in bands), \
        "the rewards table needs its five bands"
    ranges = []
    for x in bands:
        b = comp_doc(board, x)[1]
        ranges.append((int(re.search(r"minRank: (\d+)", b).group(1)), int(re.search(r"maxRank: (\d+)", b).group(1))))
    for (lo, hi), (nlo, _) in zip(ranges, ranges[1:]):
        assert hi and nlo == hi + 1, f"reward bands must tile without overlap (Q4): {ranges}"
    assert ranges[-1][1] == 0, "the last reward band must be open-ended"
    tmpl = f"rowTemplate: {{fileID: {K.FileIds('LeaderboardRow')('root#rt')}, guid: {asset_guid('LeaderboardRow')}"
    assert tmpl in panel, "the row template is not the LeaderboardRow prefab"

    for kind, view in VIEW_NAME.items():
        text = files[prefab_path(view)]
        V = K.FileIds(view)
        cls, body = comp_doc(text, V("root#panel"))
        assert body and S[PANEL_CLASS[kind]] in body, f"{view}: the root carries no {PANEL_CLASS[kind]} launch panel"
        for field, want in PANEL_FIELDS.items():
            wired(text, body, field, want, view)
        nodes = re.findall(r"^  - \{fileID: (-?\d+)\}", body.split("  intensityButtons:")[1].split("  domainTiles:")[0], re.M)
        live = [n for n in nodes if n != "0"]
        assert len(live) == 4 and len(set(live)) == 4, f"{view}: needs four distinct intensity nodes, ascending"
        assert all(A.kind_of(text, n) == (114, S["node"]) for n in live), f"{view}: an intensity entry is not a node"
        tiles = re.findall(r"^  - \{fileID: (-?\d+)\}", body.split("  domainTiles:")[1].split("  startButton:")[0], re.M)
        assert len(tiles) == 3 and all(A.kind_of(text, t) == (114, S["dtile"]) for t in tiles), \
            f"{view}: needs the three domain tiles"
        assert "  hostModal: {fileID: 0}\n" in body, f"{view}: hostModal must stay empty in the prefab (scene-only)"
        if kind == "arcade":
            # Bleeding-edge's Skim Race / Slingshot / Warpline AI difficulty row (merged 2026-10-15).
            wired(text, body, "aiDifficultyPicker", S["aipicker"], view)
            _, pick = comp_doc(text, V("AIRow#picker"))
            assert pick.count("  - Difficulty: ") == 3 and "Button: {fileID: 0}" not in pick, \
                f"{view}: the AI difficulty row needs Easy, Medium and Hard, each with its button"
        doms = []
        for d, _ in DOMAINS:
            m = re.search(r"target: \{fileID: (\d+), guid: " + asset_guid("ConfigureDomainColumn") +
                          r", type: 3\}\n      propertyPath: domain\n      value: (\d+)", text)
            doms = re.findall(r"propertyPath: domain\n      value: (\d+)", text)
        assert sorted(doms) == ["1", "2", "4"], f"{view}: the columns must be Jade, Ruby and Gold ({doms})"
        if kind != "maelstrom":
            for field, want in (("previewWindow", S["preview"]), ("controlsPanel", S["controls"]),
                                ("objectiveBox", S["objective"]), ("microToast", S["toast"])):
                wired(text, body, field, want, view)
        if kind == "arena":
            wired(text, body, "selectVesselButton", S["button"], view)
            hulls = re.findall(r"^  - \{fileID: (-?\d+)\}", body.split("  hullTiles:")[1], re.M)
            assert len(hulls) == HULL_SLOTS and all(A.kind_of(text, h) == (114, S["hull"]) for h in hulls), \
                f"{view}: the hull strip needs {HULL_SLOTS} hull tiles"
            assert f"roster: {{fileID: 11400000, guid: {ARENA_ROSTER}" in body, f"{view}: no Arena roster (Handles is false)"
        if kind == "maelstrom":
            for field, want in (("videoView", S["video"]), ("poolList", S["pool"]), ("winTargetText", TMP)):
                wired(text, body, field, want, view)
            _, video = comp_doc(text, V("Preview#video"))
            assert video and A.doc_of(text, V("Preview#vp"))[0] == 328, f"{view}: ModeVideoView needs its VideoPlayer"
        # Focus order: the tree before the header (Back), so the first A press never closes.
        order = re.findall(r"^  - \{fileID: (-?\d+)\}", A.doc_of(text, V("root#rt"))[1], re.M)
        assert order.index(str(V("Tree#rt"))) < order.index(str(V("Header#rt"))), f"{view}: Back must come last in focus order"
        _, back = comp_doc(text, V("Back#button"))
        assert back and A.doc_of(text, V("Back#close"))[0] == 114 and "m_Calls: []" in back, \
            f"{view}: Back must close through ModalWindowCloseButton, not a persistent call"


def validate_modal(files):
    text = files[MODAL_PREFAB]
    sc = T.Scene(text)
    P = PREFAB_IDS
    for go in P["retired"]:
        assert not sc.has(go), f"the modal prefab still holds retired {P['retired'][go]}"
    body = sc.docs[P["modal"]]
    assert re.search(r"^  ModalType: 1$", body, re.M), "the configure modal must be ARCADE_GAME_CONFIGURE (1), not 10"
    for f in MODAL_DROPPED:
        assert re.search(r"^  " + f + r": \{fileID: 0\}", body, re.M), f"the modal still names its hidden {f}"
    assert chip_ref().split(", type")[0] in body, "chipPrefab is not ConfigureSeatChip"
    panels = re.findall(r"^  - \{fileID: (-?\d+)\}", body.split("  launchPanels:")[1].split("  arcadeConfigSyncManager:")[0], re.M)
    assert len(panels) == 3 and panels[0] == "0" and panels[2] == "0" and panels[1] != "0", \
        "launchPanels must be [scene Arena, ConfigureView, scene Maelstrom] with the scene slots left to the scene"
    assert A.kind_of(text, panels[1]) == (114, S["minigame"]), "launchPanels[1] is not the view's MinigameLaunchPanel"
    content_rt = sc.rect_of(P["content_go"])
    kids = re.findall(r"^  - \{fileID: (-?\d+)\}", sc.docs[content_rt], re.M)
    assert len(kids) == 1 and "stripped" in sc.docs[kids[0]].split("\n")[0], "Content must hold exactly the view instance"


def validate_scene(text, files):
    sc = T.Scene(text)
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    owned = {asset_guid(n) for n in PREFABS} | {K.meta_guid(MODAL_PREFAB)}
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files, owned)
    for kind, ids in WINDOWS.items():
        for go in ids["retired"]:
            assert not sc.has(go), f"{ids['name']} still holds retired {ids['retired'][go]}"
        view = "ConfigureArenaView" if kind == "arena" else "ConfigureMaelstromView"
        mount = Mount(f"Menu_Main/Configure/{kind}", view)
        assert sc.has(mount.inst), f"{ids['name']}: the view instance is missing"
        pi = sc.docs[mount.inst]
        assert re.search(r"propertyPath: hostModal\n      value: \n      objectReference: \{fileID: " + ids["modal"] + r"\}", pi), \
            f"{ids['name']}: the panel's hostModal must be its own window's modal copy"
        copy = sc.docs[ids["modal"]]
        assert re.search(r"^  launchPanels: \[\]$", copy, re.M), f"{ids['name']}: a modal COPY must keep launchPanels empty"
        apis = sc.docs[SCENE_PI]
        assert re.search(r"launchPanels\.Array\.data\[" + str(ids["panel_slot"]) + r"\]'\n      value: \n"
                         r"      objectReference: \{fileID: " + mount.panel() + r"\}", apis), \
            f"the arcade modal's launchPanels[{ids['panel_slot']}] is not the new {kind} panel"
    board = BoardMount()
    lb = LEADERBOARD
    for go in lb["retired"]:
        assert not sc.has(go), f"the leaderboard still holds retired {lb['retired'][go]}"
    assert sc.has(board.inst), "the leaderboard view instance is missing"
    mdoc = sc.docs[lb["modal"]]
    for field, (key, comp) in LB_REFS.items():
        assert re.search(r"^  " + field + r": \{fileID: " + board.stub_fid(key) + r"\}", mdoc, re.M), \
            f"WeeklyChallengeLeaderboardModal.{field} does not point into the view"
    for field in LB_DROPPED:
        assert re.search(r"^  " + field + r": \{fileID: 0\}", mdoc, re.M), f"the leaderboard still names its old {field}"
    assert f"games: {{fileID: 11400000, guid: {ALL_GAMES}" in mdoc, "the leaderboard title has no card list"
    live = A.anchors_of(files[MODAL_PREFAB])
    for target in re.findall(r"- target: \{fileID: (-?\d+), guid: " + K.meta_guid(MODAL_PREFAB), sc.docs[SCENE_PI]):
        assert target in live, f"the arcade modal instance still overrides a retired object ({target})"


def drift(files):
    stale = []
    for path, text in files.items():
        full = os.path.join(REPO, path)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != text:
            stale.append(path)
    return stale


# --------------------------------------------------------------------------------------------
# Self-test: planted defects must all be caught
# --------------------------------------------------------------------------------------------

def self_test(files):
    def expect(label, mutate, check):
        bad = dict(files)
        mutate(bad)
        try:
            check(bad)
        except AssertionError as e:
            print(f"  caught: {label} ({e})")
            return True
        print(f"  MISSED: {label}")
        return False

    view, node, modal = prefab_path("ConfigureView"), prefab_path("ConfigureIntensityNode"), MODAL_PREFAB
    V = K.FileIds("ConfigureView")
    lb_row = prefab_path("LeaderboardRow")
    cases = [
        ("the leaderboard row's entrance stripped", lambda f: f.__setitem__(lb_row, M.strip_entrances(f[lb_row])),
         validate_prefabs),
        ("the AI difficulty row unwired", lambda f: f.__setitem__(view, re.sub(
            r"(  aiDifficultyPicker: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view])), validate_prefabs),
        ("an unwired start button", lambda f: f.__setitem__(view, re.sub(r"(  startButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view])),
         validate_prefabs),
        ("a node that forgot the intensity variable", lambda f: f.__setitem__(node, f[node].replace(INTENSITY_VAR, "0" * 32)),
         validate_prefabs),
        ("the header ahead of the tree in focus order",
         lambda f: f.__setitem__(view, f[view].replace(f"  - {{fileID: {V('Tree#rt')}}}\n", "", 1).replace(
             f"  - {{fileID: {V('Header#rt')}}}\n", f"  - {{fileID: {V('Header#rt')}}}\n  - {{fileID: {V('Tree#rt')}}}\n", 1)),
         validate_prefabs),
        ("a persistent call on Back", lambda f: f.__setitem__(view, f[view].replace(
            A.doc_of(f[view], V("Back#button"))[1], A.doc_of(f[view], V("Back#button"))[1].replace(
                "      m_Calls: []", "      m_Calls:\n      - m_Target: {fileID: 0}"), 1)), validate_prefabs),
        ("ModalType left at 10", lambda f: f.__setitem__(modal, re.sub(r"^  ModalType: 1$", "  ModalType: 10", f[modal], flags=re.M)),
         validate_modal),
        ("a modal still naming its stepper", lambda f: f.__setitem__(modal, re.sub(r"^  pcStepper: \{fileID: 0\}", "  pcStepper: {fileID: 1318977896088687642}",
                                                                                     f[modal], flags=re.M)), validate_modal),
        ("a dangling scene reference", lambda f: f.__setitem__(SCENE, f[SCENE].replace("  launchPanels: []", "  launchPanels:\n  - {fileID: 7316217782193027112}", 1)),
         lambda f: validate_scene(f[SCENE], f)),
        ("a modal copy given a panel", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            "  launchPanels: []", f"  launchPanels:\n  - {{fileID: {Mount('Menu_Main/Configure/arena', 'ConfigureArenaView').panel()}}}", 1)),
         lambda f: validate_scene(f[SCENE], f)),
    ]
    lbv = prefab_path("LeaderboardView")
    cases += [
        ("overlapping reward bands", lambda f: f.__setitem__(lbv, f[lbv].replace("  minRank: 21\n", "  minRank: 20\n", 1)),
         validate_prefabs),
        ("a leaderboard still naming its reward tooltip",
         lambda f: f.__setitem__(SCENE, re.sub(r"^  rankRewardButton: \{fileID: 0\}", "  rankRewardButton: {fileID: 7}",
                                               f[SCENE], flags=re.M)), lambda f: validate_scene(f[SCENE], f)),
    ]
    ok = all(expect(l, m, c) for l, m, c in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate_prefabs(files)
    validate_modal(files)
    validate_scene(files[SCENE], files)
    print(f"OK: {len(PREFABS)} configure prefabs, the modal prefab and Menu_Main validate")
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
