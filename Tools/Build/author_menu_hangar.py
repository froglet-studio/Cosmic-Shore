#!/usr/bin/env python3
"""
Author ITEM 2 of the style-B menu redesign - the HANGAR - as prefabs, and wire it into
Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 11; the approved Hangar boards on the
"Menu Redesign B" canvas). One view, three zones: the vessel list, the selected vessel, its abilities,
plus the spend-crystals popup.

    Assets/_Prefabs/UI Elements/Main Menu Screens/Hangar/
        HangarVesselTile.prefab   a PREFAB VARIANT of KitTile + HangarVesselTile, CanvasGroup,
                                  LayoutElement - one list row
        HangarView.prefab         the whole screen body: header (title, crystal balance), the list
                                  (ScrollRect), the hero / description / unlock zone, the ability
                                  column and the popup; HangarVesselDetailView on its root

    Menu_Main.unity, `Screens/Hangar Screen`:
        GridPanel and DetailPanel are deleted (the grid-to-detail hop, its Back button, the Vibe
        tile, the static Overview / Elements / Progress images and the eye toggle go with them);
        one HangarView instance takes their place. HangarScreen's list / detail fields point into
        the instance; its legacy fields (the inactive Hangar_Panel) are untouched.

Built from the kit generator's own node model and component bodies (author_ui_kit_prefabs.py), so
every body is Unity's serialization and every colour is a UIThemeSO token. References into nested
kit instances are stripped stubs whose fileID is Unity's derived id (source XOR instance).

STANDING DOWN. The scene migration (deleting the two old panels) runs only while they exist; after
that the script re-emits the documents it owns, and validates the wiring of the scene AS COMMITTED
on every run, --check included.

    python3 Tools/Build/author_menu_hangar.py              # write
    python3 Tools/Build/author_menu_hangar.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_hangar.py --self-test  # planted defects must all be caught
"""

import argparse
import hashlib
import importlib.util
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


K = _load("author_ui_kit_prefabs")      # node model, component bodies, tokens, roles
T = _load("author_menu_topnav")         # Scene (ordered documents), stub writers, normalize
M = _load("menu_b_motion")              # revision-2 motion (entrances)

REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Hangar"
MASK63 = 0x7FFFFFFFFFFFFFFF

# --- package script guids, harvested from Menu_Main (never recalled) --------------------------
SCROLL_RECT = "1aa08ab6e0800fa44ae55d278d1423e3"
RECT_MASK_2D = "3312d7739989d2b4e91e6319e9a96d76"
MASK = "31a19414c41e5ae4aae2af33fee712f6"
PACKAGE = K.PACKAGE_GUIDS | {SCROLL_RECT, RECT_MASK_2D, MASK, "1344c3c82d62a2a41a3576d8abb8e3ea"}
# The masks the menu authors: a RectMask2D that stops re-culling while nothing under it can be seen
# (a closed window, a screen slid off the strip). Docs/PERFORMANCE_OPTIMIZATION.md, the 10-26 Menu_Main row.
KIT_RECT_MASK_2D = K.kit_script("KitRectMask2D")

HANGAR_SCREEN = K.meta_guid("Assets/_Scripts/UI/Screens/HangarScreen.cs")
DETAIL_VIEW = K.meta_guid("Assets/_Scripts/UI/Views/HangarVesselDetailView.cs")
VESSEL_TILE = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarVesselTile.cs")
COVER = K.kit_script("KitCoverImage")
KIT_TEXT = K.kit_script("KitText")
KIT_THEMED = K.kit_script("KitThemedGraphic")
KIT_BUTTON = K.kit_script("KitButton")
A_BUTTON = "4e29b1a8efbd4b44bb3f3716e73f07ff"            # UnityEngine.UI.Button
KIT_TILE = K.kit_script("KitTile")

# --- the live objects the scene patch edits (fileIDs measured 2026-10-14; names asserted) ------
HANGAR = dict(go="1526813611221386648", rt="38145210908179955", screen="3082319601828466910",
              legacy_rt="7569480448419162635")
RETIRED_ROOTS = {"5036150567944460954": "GridPanel", "8076139419038489131": "DetailPanel"}

PREFABS = ("HangarVesselTile", "HangarView")
# Retired in rev 3 (2026-10-16): the ability column became the Abilities TAB, so its row prefab and
# script are gone; a re-run deletes the prefab if a checkout still has it.
RETIRED_PREFABS = ("HangarAbilityRow",)


def asset_guid(name):
    return hashlib.md5(f"CosmicShore/MenuB/assets/{name}".encode()).hexdigest()


def prefab_guid(name):
    return asset_guid(name) if name in PREFABS else K.prefab_guid(name)


def derived(src, pid):
    """Unity's fileID for an object of a nested instance / variant base: source XOR instance."""
    return (int(src) ^ int(pid)) & MASK63


# --------------------------------------------------------------------------------------------
# Stub-aware context: a component body can reference an object inside a nested kit instance
# --------------------------------------------------------------------------------------------

STUB_SCRIPT = {"tmp": K.TMP_UGUI, "img": K.IMAGE, "kt": KIT_TEXT, "th": KIT_THEMED, "button": KIT_BUTTON,
               "tile": KIT_TILE}


def stub_doc(fid, src_key, src, guid, pid):
    comp = src_key.split("#")[1]
    if comp == "go":
        return T.stub_go(fid, src, guid, pid)
    if comp == "rt":
        return (f"--- !u!224 &{fid} stripped\nRectTransform:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
                f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return T.stub_mono(fid, src, guid, pid, STUB_SCRIPT[comp])


class PCtx(K.Ctx):
    def __init__(self, file_key):
        super().__init__(file_key)
        self.instances = {}
        self.stubs = {}

    def register(self, node):
        for ch in node.children:
            if isinstance(ch, K.Instance):
                self.instances[ch.key] = ch.prefab_key
            else:
                self.register(ch)

    def iref(self, inst_key, src_key):
        prefab = self.instances[inst_key]
        src = K.source_fid(prefab, src_key)
        pid = self.fid(inst_key + "#inst")
        fid = derived(src, pid)
        self.stubs.setdefault(fid, stub_doc(fid, src_key, src, prefab_guid(prefab), pid))
        return fid


def prefab_text(root, file_key):
    ctx = PCtx(file_key)
    ctx.register(root)
    out = []
    K.emit_node(ctx, root, 0, out)
    return K.unity_empty_scalars(K.HEADER + "".join(out) + "".join(ctx.stubs.values()))


# --------------------------------------------------------------------------------------------
# Component bodies the kit generator does not have
# --------------------------------------------------------------------------------------------

N = K.Node
STRETCH = K.STRETCH
R = K.rect
tok = K.tok

VALIGN = {"top": 256, "middle": 512, "bottom": 1024}


def text(t, role, color, align="left", valign="middle", wrap=False):
    base = K.tmp(t, role, color, align)

    def body(ctx, go):
        s = base(ctx, go).replace("  m_VerticalAlignment: 512\n", f"  m_VerticalAlignment: {VALIGN[valign]}\n")
        return s.replace("  m_TextWrappingMode: 0\n", "  m_TextWrappingMode: 1\n") if wrap else s
    return body


def label(node, t, role, token, **kw):
    """A TMP + KitText pair: the role sets the type, the token the colour."""
    node.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text(t, role, tok(token), **kw)) \
        .add("kt", 114, K.kit_text(role, token))
    return node


def le(ignore=False, mw=-1, mh=-1, pw=-1, ph=-1, fw=-1, fh=-1):
    return lambda ctx, go: K.mono(go, K.LAYOUT_ELEMENT,
                                  f"  m_IgnoreLayout: {1 if ignore else 0}\n  m_MinWidth: {K.f32(mw)}\n"
                                  f"  m_MinHeight: {K.f32(mh)}\n  m_PreferredWidth: {K.f32(pw)}\n"
                                  f"  m_PreferredHeight: {K.f32(ph)}\n  m_FlexibleWidth: {K.f32(fw)}\n"
                                  f"  m_FlexibleHeight: {K.f32(fh)}\n  m_LayoutPriority: 1\n")


def canvas_group(shown):
    v = 1 if shown else 0
    return lambda ctx, go: ("CanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Alpha: {v}\n"
                            f"  m_Interactable: {v}\n  m_BlocksRaycasts: {v}\n  m_IgnoreParentGroups: 0\n")


def themed(token, alpha=1.0):
    return lambda ctx, go: K.mono(go, KIT_THEMED, f"  theme: {K.ref(11400000, K.THEME, 2)}\n"
                                                  f"  token: {K.TOKEN_INDEX[token]}\n  alpha: {K.f32(alpha)}\n")


def framed(root, key, sliver, fill_token, border_token, line="line1", fill_alpha=1.0):
    """A slivered fill + border pair, both painted by KitThemedGraphic, both out of layout."""
    fill = root.child(N(f"{key}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok(fill_token, fill_alpha), K.kit_sprite(f"kit_sliver{sliver}_fill"), True)) \
        .add("th", 114, themed(fill_token, fill_alpha)).add("le", 114, le(ignore=True))
    bord = root.child(N(f"{key}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok(border_token), K.kit_sprite(f"kit_sliver{sliver}_{line}"), True)) \
        .add("th", 114, themed(border_token)).add("le", 114, le(ignore=True))
    return fill, bord


def ref(fid):
    return "{fileID: %s}" % fid


# --------------------------------------------------------------------------------------------
# HangarVesselTile.prefab - a variant of KitTile
# --------------------------------------------------------------------------------------------

# Rev 25 round 2 (Shombith 2026-10-26: "Left side vessel holders should use the open border card as well. The mockup's
# vessels were colorful and looked way better"): each vessel is a card in the Hangar's open angled border (its body and
# outline are kit sprites drawn at this size, so KitTile still swaps the outline on selection), the hull large on top,
# the name and status under it - never on the picture. Section 81 ("Go ahead with the colourful card renders"): the
# picture is the hull's own colourful three-quarter render (HangarCardArtSO, Tools/Build/author_hangar_card_art.py), a
# 3:1 cut-out that fills the art band, drawn as rendered (no material: Hull_Lit would grey its colours).
TILE_W, TILE_H = 220, 124                     # author_ui_kit_sprites.TILE_W / TILE_H: the open card's sprites
TILE_ART_TOP, TILE_ART_H = 8, 62             # the art band, from the card's top
TILE_NAME_Y, TILE_STATUS_Y = 28, 8           # the name and status rows, from the card's foot
CARD_ART = "Assets/_SO_Assets/UI/HangarCardArt.asset"   # written by author_hangar_card_art.py (run it first)
WINDOW_BACKDROP_ALPHA = 0.95                  # author_menu_arcade.WINDOW_BACKDROP_ALPHA (asserted by that generator's load)


def tile_variant_text():
    base = K.prefab_guid("KitTile")
    F = K.FileIds("HangarVesselTile")
    pid = F("variant#inst")

    def d(key):
        return derived(K.source_fid("KitTile", key), pid)

    def rect_mods(key, amin, amax, pivot, pos, size):
        return [(key, "m_AnchorMin.x", amin[0]), (key, "m_AnchorMin.y", amin[1]), (key, "m_AnchorMax.x", amax[0]),
                (key, "m_AnchorMax.y", amax[1]), (key, "m_Pivot.x", pivot[0]), (key, "m_Pivot.y", pivot[1]),
                (key, "m_AnchoredPosition.x", pos[0]), (key, "m_AnchoredPosition.y", pos[1]),
                (key, "m_SizeDelta.x", size[0]), (key, "m_SizeDelta.y", size[1])]

    sprite = lambda name: ("ref", f"{{fileID: 21300000, guid: {K.kit_sprite(name)}, type: 3}}")
    lean_room = 16 + 14                        # the right side leans in 14 at the floor
    mods = [("root#go", "m_Name", "HangarVesselTile"),
            ("root#rt", "m_SizeDelta.x", TILE_W), ("root#rt", "m_SizeDelta.y", TILE_H),
            ("Fill#img", "m_Sprite", sprite("kit_open_tile_fill")), ("Fill#img", "m_Type", 0),
            ("Border#img", "m_Sprite", sprite("kit_open_tile_line1")), ("Border#img", "m_Type", 0),
            ("FocusRing#img", "m_Sprite", sprite("kit_open_tile_line2")), ("FocusRing#img", "m_Type", 0),
            ("root#tile", "borderSelectedSprite", sprite("kit_open_tile_line2")),
            ("root#tile", "borderUnselectedSprite", sprite("kit_open_tile_line1"))]
    # The art band spans the card inside its border (the right side leans in at the floor, so the band stops short of
    # it); the 3:1 art keeps its aspect (KitTile's Art preserves it) and fills the band's height.
    mods += rect_mods("Art#rt", (0, 1), (1, 1), (0.5, 1), (-lean_room / 2 + 4, -TILE_ART_TOP), (-8 - lean_room, TILE_ART_H))
    mods += rect_mods("Lock#rt", (1, 1), (1, 1), (1, 1), (-22, -16), (20, 20))
    mods += rect_mods("Name#rt", (0, 0), (1, 0), (0, 0), (14, TILE_NAME_Y), (-14 - lean_room, 24))
    mods += rect_mods("Status#rt", (0, 0), (1, 0), (0, 0), (14, TILE_STATUS_Y), (-14 - lean_room, 18))
    assert TILE_ART_TOP + TILE_ART_H <= TILE_H - (TILE_NAME_Y + 24), "the hull would sit on the vessel's name"

    def mod_line(k, p, v):
        if isinstance(v, tuple):
            return (f"    - target: {K.ref(K.source_fid('KitTile', k), base)}\n      propertyPath: {p}\n"
                    f"      value: \n      objectReference: {v[1]}\n")
        return (f"    - target: {K.ref(K.source_fid('KitTile', k), base)}\n      propertyPath: {p}\n"
                f"      value: {K.f32(v) if not isinstance(v, str) else v}\n      objectReference: {{fileID: 0}}\n")
    lines = "".join(mod_line(k, p, v) for k, p, v in mods)
    tile_c, cg_c, le_c, in_c = F("root#hangarTile"), F("root#cg"), F("root#le"), F("root#in")
    root_go = d("root#go")
    added = "".join(f"    - targetCorrespondingSourceObject: {K.ref(K.source_fid('KitTile', 'root#go'), base)}\n"
                    f"      insertIndex: -1\n      addedObject: {{fileID: {c}}}\n" for c in (tile_c, cg_c, le_c, in_c))
    docs = [f"--- !u!1001 &{pid}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
            "  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 0}\n"
            "    m_Modifications:\n" + lines
            + "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n"
            "    m_AddedComponents:\n" + added + f"  m_SourcePrefab: {K.ref(100100000, base)}\n"]
    for key in ("root#go", "root#tile", "Art#img", "Name#tmp", "Status#kt"):
        docs.append(stub_doc(d(key), key, K.source_fid("KitTile", key), base, pid))
    docs.append(f"--- !u!114 &{tile_c}\n" + K.mono(root_go, VESSEL_TILE,
                f"  tile: {ref(d('root#tile'))}\n  icon: {ref(d('Art#img'))}\n  nameText: {ref(d('Name#tmp'))}\n"
                f"  statusText: {ref(d('Status#kt'))}\n  canvasGroup: {ref(cg_c)}\n"
                f"  cardArt: {{fileID: 11400000, guid: {K.meta_guid(CARD_ART)}, type: 2}}\n"))
    docs.append(f"--- !u!225 &{cg_c}\n" + canvas_group(True)(None, root_go))
    docs.append(f"--- !u!114 &{le_c}\n" + le(ph=TILE_H)(None, root_go))
    # Motion (section 22.8): rows rise in by list position. The tile dims itself through the same
    # CanvasGroup; KitEntrance fades up to the alpha it found, so a locked row stays dim.
    docs.append(f"--- !u!114 &{in_c}\n" + M.entrance("Rise", by_sibling=True, distance=8)(None, root_go))
    return K.unity_empty_scalars(K.HEADER + "".join(docs)), tile_c


# --------------------------------------------------------------------------------------------
# HangarView.prefab
# --------------------------------------------------------------------------------------------

def button_instance(key, name, variant, opaque, caption, icon=False, disabled=False):
    w, h = K.button_size(caption)
    if icon:
        w += 18 + 10
    mods = K.button_mods("KitButton", variant, opaque, caption, disabled=disabled)
    if icon:
        mods.append(("Icon#go", "m_IsActive", 1, None))
    return K.Instance(key, "KitButton", K.prefab_guid("KitButton"), name, R((0, 0.5), None, (w / 2, 0), (w, h)), mods)


def pill_instance(key, name):
    size = (K.label_width("0", "Data") + 60, K.line_height("Data") + 16)
    return K.Instance(key, "KitCurrencyPill", K.prefab_guid("KitCurrencyPill"), name, R(size=size))


# --------------------------------------------------------------------------------------------
# Rev 3 (2026-10-16): the five-tab panel and its pages
# --------------------------------------------------------------------------------------------

HERO_H = 600
VIBE_H = 156
# Section 82: the stage's how-to-hold line, one per device (HangarVesselStage picks it; empty shows nothing).
POINTER_HINT = "Drag to turn, Shift-drag to roll, wheel to zoom, right-drag to move, double-click to reset"
PAD_HINT = "Right stick to turn, hold Y with it to zoom and roll, press it to reset"
TOUCH_HINT = "Drag to turn, pinch to zoom, twist to roll, two fingers to move, double-tap to reset"
RAW_IMAGE = "1344c3c82d62a2a41a3576d8abb8e3ea"     # UnityEngine.UI.RawImage
STAGE_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarVesselStage.cs")
VIBE_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarVibeView.cs")
TABS_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarTabsView.cs")
ABILITIES_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarAbilitiesPage.cs")
UPGRADES_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarUpgradesPage.cs")
GAMES_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarGamesPage.cs")
PILOTS_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarPilotsPage.cs")
HALO_SCRIPT = K.kit_script("KitElementHalo")
FX_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarStageFx.cs")
BLOOM = "006e134e05ff461bbd48ab3fd31629b4"            # AbilityLockupStyle.bloomSprite: the soft round glow
# Retired with TRY (2026-10-19, the owner: the Hangar preview is played by the AI, on the stage, at very low cost).
# The generator stands guard that they stay gone.
RETIRED_FILES = ("Assets/_SO_Assets/Hangar/HangarTryPreview.asset", "Assets/_SO_Assets/Hangar/HangarTryPreview.asset.meta",
                 "Assets/_SO_Assets/Hangar.meta", "Assets/_Scripts/UI/Elements/Hangar/HangarAbilityPreview.cs",
                 "Assets/_Scripts/UI/Elements/Hangar/HangarAbilityPreview.cs.meta")
KIT_TEXT_TAB = K.kit_script("KitTextTab")
MENU_AUDIO = K.meta_guid("Assets/_Scripts/UI/MenuAudio.cs")
VESSEL_PREFABS = K.meta_guid("Assets/_SO_Assets/Vessel Prefab Container.asset")
GLYPH_SET = K.meta_guid("Assets/Resources/ControlGlyphSet.asset")
PILOT_CATALOG = K.meta_guid("Assets/Resources/Pilots/PilotArtifactCatalog.asset")   # written by author_pilot_assets.py (run it first)
ARTIFACTS_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarArtifactsPage.cs")
ARTIFACT_SLOTS = 4                          # slot frames drawn; PilotCatalogSO.slotsPerVessel shows the first N
ARTIFACT_ROWS = 8                           # a vessel's two artifacts and the fleet's five, and one spare
GAME_LISTS = [K.meta_guid("Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"),
              K.meta_guid("Assets/_SO_Assets/Games/GameLists/ArenaGames.asset")]
ELEMENTS = [("Charge", 1), ("Mass", 2), ("Space", 3), ("Time", 4)]
TAB_NAMES = ["Abilities", "Upgrades", "Games", "Pilots", "Artifacts"]
# Rev 24 (Shombith 2026-10-26, option C taken forward): three tabs on Y, OVERVIEW's three parts on LB / RB.
# Rev 25 round 2 (Shombith 2026-10-26: "Overview, Abilities, Upgrades, Games, Pilots and artifacts all are on the same
# tab nav bars"): one row of five tabs between LB and RB, every page its own tab (a group of one, so the header still
# follows the tab). LB / RB step through all five; Y is free.
GROUPS = [(name, i, 1) for i, name in enumerate(TAB_NAMES)]
PARTS = ()
# Section 82 (Shombith, 2026-10-26: "The whole hangar is a bit cramped ... reducing the sizes a little"): the tabs read
# in the smaller body size with more room between them.
TAB_ROLE, TAB_PAD, TAB_GAP = "BodySmall", 12, 18
SIDE = 40                                   # the screen's side margins
LIST_W = 230                                # the vessel list, on the left
# Section 83 (Shombith, 2026-10-26: "in the hangar we can reduce the size of each component it looks too big in the
# game"): everything under the top bar is laid out in a larger space and drawn at CONTENT_SCALE, so every component is
# smaller by the same amount at any aspect (the canvas matches height, so the space is (1080 - TOP_BAR) / scale tall).
# Every top offset below is measured from the content's top, just under the top bar.
CONTENT_SCALE, TOP_BAR = 0.88, 84
CONTENT_H = round((1080 - TOP_BAR) / CONTENT_SCALE)
ORB_TOP = 44 + 92 + 14          # author_menu_arcade.ORB_Y + ORB_SIZE, plus air: the friends orb, bottom left (screen px)
LIST_BOTTOM = ORB_TOP / CONTENT_SCALE    # the vessel list stops above the orb (content units)
DOSSIER_W, DOSSIER_TOP = 710, 66
DOSSIER_H = CONTENT_H - DOSSIER_TOP - 40    # author_ui_kit_sprites.CRADLE_W / _H: the open cradle's fill
CENTRE_L, CENTRE_R = SIDE + LIST_W + 44, SIDE + DOSSIER_W + 30   # section 83: the list header and the kicker apart
HEADER_TOP, CENTRE_TOP, BOTTOM = 16, 164, 40
PAD = 30                                    # the dossier's content inset
PAD_R = 60                                  # a page's right inset: clears the right side leaning in to the floor
PAGE_TOP_PARTS, PAGE_TOP, PAGE_BOTTOM = 84, 84, 30
VIBE_W, VIBE_H, VIBE_SEGMENTS = 490, 112, 10
# The Rail border (R24Borders "2 Rail"): Bracket_Large's open cradle, closed on the right by a chamfered rail that stops
# short of the tabs, with a corner mark. Lines in y-down dossier units.
RAIL_LEAN, RAIL_KNEE, RAIL_STUB = 56, 0.46, 0.62
RAIL_Y, RAIL_CHAMFER, RAIL_START = 64, 34, 0.70
LINE_W = 1.5
GAME_TILES = 6
EMPTY_CALLS = "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"


def TLr(x, y, w, h):
    return R((0, 1), None, (x, -y), (w, h), (0, 1))


def TRr(x, y, w, h):
    return R((1, 1), None, (-x, -y), (w, h), (1, 1))


def sprite_guid(path):
    return K.meta_guid(path)


def element_glyph(name):
    return sprite_guid(f"Assets/_Graphics/ElementIcons/icon_{name.lower()}Upgrade_3_active.png")


FLOWER_SCRIPT = K.kit_script("KitElementFlower")
PETAL_SPRITE = {n: K.meta_guid(f"Assets/Resources/ElementPetals/{n.lower()}_petal.png") for n, _ in ELEMENTS}
START_LEVEL = 1                                    # HangarUpgradeStore.StartLevel: every element starts at level 1
# ElementalBarsConfig's tick colours (-1 fire, 0 grey, 1 white, 2 blue, 3 lime); a flower is authored at the start level.
TICK = {-1: (1.0, 0.33, 0.1, 1.0), 0: (0.51, 0.51, 0.54, 1.0), 1: (0.96, 0.96, 1.0, 1.0), 2: (0.22, 0.51, 1.0, 1.0),
        3: (0.59, 0.92, 0.16, 1.0)}


def petal_ticks(level):
    """ElementalBarsConfigSO.DistributePetalValues: a level in [-5, 15] round-robin over five petals."""
    inc = max(-5, min(15, level)) + 5
    return [-1 + inc // 5 + (1 if i < inc % 5 else 0) for i in range(5)]


def element_flower(parent, key, name, r, element="Charge", level=START_LEVEL, active=True):
    """The HUD's element flower (KitElementFlower, Rev 24): five copies of the element's petal, stretched over the flower
    and rotated 72 degrees apart (ElementalBarsView.ConfigurePetal), coloured for `level` as a match colours them. The
    component recolours it at runtime; the authored colours are what the editor shows before then."""
    f = parent.child(N(key, name, r, active=active))
    for i, tick in enumerate(petal_ticks(level)):
        p = f.child(N(f"{key}/Petal{i}", f"Petal{i}", STRETCH))
        p.rot = -72 * i
        p.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(TICK[tick], PETAL_SPRITE[element], preserve=True))
    f.add("flower", 114, lambda ctx, go: K.mono(go, FLOWER_SCRIPT,
          f"  element: {dict(ELEMENTS)[element]}\n  petals:\n"
          + "".join(f"  - {ref(ctx.c(f'{key}/Petal{i}', 'img'))}\n" for i in range(5)) + "  config: {fileID: 0}\n"))
    return f


def kit_row(parent, key, name, r, layout_h, content_keys=(), interactive=True):
    """A selectable row: focus ring, glass fill, hairline, KitButton (Cancel variant, outline) and the
    menu's press sound. The row's own content draws itself; the button paints only its frame.
    interactive=False keeps the look and drops the input (an information row, section 66): out of pad
    navigation, and a CanvasGroup that blocks no raycasts, so no click, tap or A reaches it; no press sound."""
    b = parent.child(N(key, name, r))
    b.add("le", 114, le(ph=layout_h))
    ring = b.child(N(f"{key}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10))))
    ring.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight", 0.0), K.kit_sprite("kit_sliver10_line2"), True)).add("le", 114, le(ignore=True))
    fill = b.child(N(f"{key}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.8), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bord = b.child(N(f"{key}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    glow = b.child(N(f"{key}/Glow", "Glow", R((0, 0), (1, 1), (0, 0), (4, 4))))
    glow.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), K.kit_sprite("kit_sliver10_line2"), True))

    def body(ctx, go):
        content = "".join(f"  - {ref(ctx.c(k, c))}\n" for k, c in content_keys)
        nav = K.SELECTABLE_FIELDS if interactive else K.SELECTABLE_FIELDS.replace("    m_Mode: 3\n", "    m_Mode: 0\n")
        return K.mono(go, KIT_BUTTON,
                      nav + f"  m_TargetGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n" + EMPTY_CALLS
                      + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{key}/FocusRing', 'img'))}\n"
                      f"  variant: {K.VARIANTS['Cancel']}\n  fill: 0\n"
                      f"  fillGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n  borderGraphic: {ref(ctx.c(f'{key}/Border', 'img'))}\n"
                      + (f"  contentGraphics:\n{content}" if content else "  contentGraphics: []\n") + "  playGradient: {fileID: 0}\n")
    b.add("button", 114, body)
    if not interactive:
        b.add("cg", 225, lambda ctx, go: canvas_group(True)(ctx, go).replace("  m_BlocksRaycasts: 1\n", "  m_BlocksRaycasts: 0\n"))
        return b
    b.add("audio", 114, lambda ctx, go: K.mono(go, MENU_AUDIO, "  category: 11\n"))
    return b


def page_node(panel, key, shown, own_canvas=False, top=PAGE_TOP_PARTS):
    """A page inside the dossier: under the tab row (and OVERVIEW's parts row when `top` leaves room for it)."""
    p = panel.child(N(f"Tabs/{key}", key, R((0, 0), (1, 1), ((PAD - PAD_R) / 2, (PAGE_BOTTOM - top) / 2),
                                            (-(PAD + PAD_R), -(top + PAGE_BOTTOM)))))
    p.add("cg", 225, canvas_group(shown))
    if own_canvas:
        # A page that animates (the element halos, the upgrade pop) gets its own Canvas, so its writes
        # re-batch the page and never Menu_Main's whole canvas; a nested Canvas needs its own raycaster.
        p.add("canvas", 223, lambda ctx, go: (
            "Canvas:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n"
            "  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n"
            "  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_VertexColorAlwaysGammaSpace: 1\n"
            "  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n"
            "  m_SortingLayerID: 0\n  m_SortingOrder: 0\n  m_TargetDisplay: 0\n"))
        p.add("raycaster", 114, lambda ctx, go: K.mono(go, K.GRAPHIC_RAYCASTER,
              "  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n"
              "    serializedVersion: 2\n    m_Bits: 4294967295\n"))
    return p


def ellipsize(node):
    """Clip a label's overflow with an ellipsis (a long ability line ran under the keycap)."""
    node.comps = [(k, cid, (lambda b: (lambda ctx, go: b(ctx, go).replace("  m_overflowMode: 0\n", "  m_overflowMode: 1\n")))(body))
                  if k == "tmp" else (k, cid, body) for k, cid, body in node.comps]
    return node


def halo(row, key, r, phase):
    """The breathing element glow behind a row's glyph (KitElementHalo); the page sets its colour."""
    h = row.child(N(key, "Halo", r))
    h.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), K.kit_sprite("kit_spark"))) \
        .add("halo", 114, lambda ctx, go: K.mono(go, HALO_SCRIPT,
             f"  period: 2.6\n  phase: {phase}\n  alphaRange: {{x: 0.18, y: 0.6}}\n  whiteDrift: 0.3\n  flashSeconds: 0.6\n"))
    return h


def list_node(page, key, top, spacing=10):
    lst = page.child(N(key, "List", R((0, 1), (1, 1), (0, -top), (0, 0), (0.5, 1))))
    lst.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), spacing, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    return lst


def page_header(page, key, title, line):
    label(page.child(N(f"{key}/Head", "Head", TLr(0, 0, 360, 18))), title, "Label", "TextLight")
    label(page.child(N(f"{key}/Line", "Line", R((0, 1), (1, 1), (0, -24), (0, 18), (0.5, 1)))), line, "BodySmall", "NeutralLightest")


ABILITY_ROW_H, ABILITY_TEXT_X = 100, 172


def abilities_page(panel):
    page = page_node(panel, "Abilities", True, own_canvas=True)
    lst = list_node(page, "Tabs/Abilities/List", 0, spacing=8)
    for n, (name, _) in enumerate(ELEMENTS):
        k = f"Tabs/Abilities/{name}"
        # Information, not buttons (section 66): the AI demo plays each ability; nothing the player presses does.
        # Section 82: a little smaller all through (100 tall, was 112).
        # Section 83: a press (click, tap, A) plays this ability on the stage - and only a press.
        row = kit_row(lst, k, name, R((0, 1), (1, 1), (0, 0), (0, ABILITY_ROW_H), (0.5, 1)), ABILITY_ROW_H)
        # Rev 24: the element as the HUD's own flower, at the level the player bought on this vessel.
        element_flower(row, f"{k}/Flower", "Flower", TLr(12, 16, 68, 68), name)
        halo(row, f"{k}/Halo", TLr(80, 4, 92, 92), round(0.25 * n, 2))
        row.child(N(f"{k}/Hex", "Hex", TLr(101, 25, 50, 50))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("SurfaceLight", 0.35), K.kit_sprite("kit_hex_fill"), preserve=True))
        row.child(N(f"{k}/Icon", "Icon", TLr(110, 34, 32, 32))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True))
        tx = ABILITY_TEXT_X
        ellipsize(label(row.child(N(f"{k}/Name", "Name", R((0, 1), (1, 1), (tx, -14), (-(tx + 90), 26), (0, 1)))), name, "H3", "TextLight"))
        label(row.child(N(f"{k}/Element", "Element", TLr(tx, 42, 200, 18))), name, "Label", "NeutralLightest")
        ellipsize(label(row.child(N(f"{k}/Desc", "Desc", R((0, 1), (1, 1), (tx, -64), (-(tx + 90), 20), (0, 1)))), "", "BodySmall", "NeutralLightest"))
        cap = row.child(N(f"{k}/Keycap", "Keycap", TRr(18, 33, 56, 34)))
        cap.add("cg", 225, canvas_group(True))
        cap.child(N(f"{k}/Keycap/Fill", "Fill", STRETCH)).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("SurfaceVeryDark", 0.7), K.kit_sprite("kit_sliver10_fill"), True))
        cap.child(N(f"{k}/Keycap/Border", "Border", STRETCH)).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver10_line1"), True))
        label(cap.child(N(f"{k}/Keycap/Label", "Label", STRETCH)), "RT", "Label", "TextLight", align="center")
    label(page.child(N("Tabs/Abilities/Core", "Core", R((0, 0), (1, 0), (0, 8), (0, 18), (0.5, 0)))),
          "Core: skim by flying close to a prism, drift with the left stick.", "BodySmall", "NeutralLightest")

    def body(ctx, go):
        rows = ""
        for name, idx in ELEMENTS:
            k = f"Tabs/Abilities/{name}"
            rows += (f"  - element: {idx}\n    button: {ref(ctx.c(k, 'button'))}\n    icon: {ref(ctx.c(f'{k}/Icon', 'img'))}\n"
                     f"    nameText: {ref(ctx.c(f'{k}/Name', 'tmp'))}\n    elementText: {ref(ctx.c(f'{k}/Element', 'tmp'))}\n"
                     f"    descriptionText: {ref(ctx.c(f'{k}/Desc', 'tmp'))}\n    elementGlyph: {{fileID: 0}}\n"
                     f"    flower: {ref(ctx.c(f'{k}/Flower', 'flower'))}\n"
                     f"    keycapGroup: {ref(ctx.c(f'{k}/Keycap', 'cg'))}\n    keycapText: {ref(ctx.c(f'{k}/Keycap/Label', 'tmp'))}\n"
                     f"    keycapBorder: {ref(ctx.c(f'{k}/Keycap/Border', 'img'))}\n    glow: {ref(ctx.c(f'{k}/Glow', 'img'))}\n"
                     f"    hex: {ref(ctx.c(f'{k}/Hex', 'img'))}\n    halo: {ref(ctx.c(f'{k}/Halo', 'halo'))}\n")
        return K.mono(go, ABILITIES_SCRIPT,
                      "  rows:\n" + rows
                      + f"  prefabs: {K.ref(11400000, VESSEL_PREFABS, 2)}\n  glyphSet: {K.ref(11400000, GLYPH_SET, 2)}\n"
                      f"  elementTint: {K.TOKEN_INDEX['NeutralLightest']}\n"
                      "  elementNames:\n  - Charge\n  - Mass\n  - Space\n  - Time\n"
                      "  openSlotName: Open slot\n  openSlotLine: Not designed yet.\n  passiveWord: Passive\n  glowSeconds: 0.9\n"
                      "  kindOverrides: []\n"
                      "  keyPressSeconds: 0.35\n  keyPressScale: 0.84\n")
    page.add("logic", 114, body)
    return page


def text_button(parent, key, name, caption, r, target_key, target_comp, type_name, method, variant="Default", nav=3):
    """A style-B KitButton drawn in place (focus ring, fill, border, label) whose click calls
    <type_name>.<method> on ctx.c(target_key, target_comp), plus the menu's press sound."""
    b = parent.child(N(key, name, r))
    ring = b.child(N(f"{key}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10))))
    ring.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight", 0.0), K.kit_sprite("kit_sliver10_line2"), True)).add("le", 114, le(ignore=True))
    border, fillc, content = K.button_palette(variant, False)
    b.child(N(f"{key}/Fill", "Fill", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    b.child(N(f"{key}/Border", "Border", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True))
    b.child(N(f"{key}/Label", "Label", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, K.tmp(caption, "ButtonSmall", content, align="center")) \
        .add("kt", 114, K.kit_text("ButtonSmall", driven=True))
    b.add("audio", 114, lambda ctx, go: K.mono(go, MENU_AUDIO, "  category: 11\n"))

    def body(ctx, go):
        calls = ("  m_OnClick:\n    m_PersistentCalls:\n      m_Calls:\n"
                 + "".join(f"      - m_Target: {{fileID: {t}}}\n"
                           f"        m_TargetAssemblyTypeName: {tn}, Assembly-CSharp\n"
                           f"        m_MethodName: {m}\n        m_Mode: 1\n        m_Arguments:\n"
                           "          m_ObjectArgument: {fileID: 0}\n"
                           "          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine\n"
                           "          m_IntArgument: 0\n          m_FloatArgument: 0\n          m_StringArgument: \n"
                           "          m_BoolArgument: 0\n        m_CallState: 2\n"
                           for t, tn, m in (([(ctx.c(target_key, target_comp), type_name, method)] if method else [])
                                            + [(ctx.c(key, "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])))
        return K.mono(go, KIT_BUTTON,
                      K.SELECTABLE_FIELDS.replace("    m_Mode: 3\n", f"    m_Mode: {nav}\n")
                      + f"  m_TargetGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n" + calls
                      + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{key}/FocusRing', 'img'))}\n"
                      f"  variant: {K.VARIANTS[variant]}\n  fill: 0\n"
                      f"  fillGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n  borderGraphic: {ref(ctx.c(f'{key}/Border', 'img'))}\n"
                      f"  contentGraphics:\n  - {ref(ctx.c(f'{key}/Label', 'tmp'))}\n  playGradient: {{fileID: 0}}\n")
    b.add("button", 114, body)
    return b


UPG_COL = 120                     # the element column, on the right of the UPGRADES part
UPG_MAIN = UPG_COL + 24           # what the column takes off the focused element's side


def main_r(y, h):
    """A row across the focused element's side (left of the element column), `y` down from the top."""
    return R((0, 1), (1, 1), (-UPG_MAIN / 2, -y), (-UPG_MAIN, h), (0.5, 1))


def upgrades_page(panel):
    """Rev 24: the four elements as the HUD's flowers down the right (each at the bought level, from 1); the focused one's
    ability, its flower NOW and NEXT, its level-5 upgrade and the UPGRADE button on the left."""
    page = page_node(panel, "Upgrades", False, own_canvas=True)
    U = "Tabs/Upgrades"
    label(page.child(N(f"{U}/Line", "Line", main_r(0, 18))), "Spend crystals to raise an element. The hull changes as it grows.",
          "BodySmall", "NeutralLightest")
    for i, (name, _) in enumerate(ELEMENTS):
        k = f"{U}/{name}"
        tile = kit_row(page, k, name, TRr(0, i * 150, UPG_COL, 140), 140)
        tile.child(N(f"{k}/Selected", "Selected", R((0, 0), (1, 1), (0, 0), (6, 6)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("Selection", 0.0), K.kit_sprite("kit_sliver10_line2"), True))
        element_flower(tile, f"{k}/Flower", "Flower", R((0.5, 1), None, (0, -12), (92, 92), (0.5, 1)), name)
        label(tile.child(N(f"{k}/Level", "Level", R((0, 0), (1, 0), (0, 12), (0, 26), (0.5, 0)))), "1", "Data", "TextLight", align="center")
    label(page.child(N(f"{U}/Element", "Element", TLr(0, 34, 96, 18))), "CHARGE", "Label", "TextLight")
    label(page.child(N(f"{U}/Step", "Step", TLr(96, 34, 300, 18))), "LEVEL 1 TO 2", "Label", "NeutralLightest")
    ellipsize(label(page.child(N(f"{U}/Ability", "Ability", main_r(60, 40))), "", "H2", "TextLight"))
    ellipsize(label(page.child(N(f"{U}/Desc", "Desc", main_r(106, 64))), "", "BodySmall", "NeutralLightest", valign="top", wrap=True))
    element_flower(page, f"{U}/Now", "NowFlower", TLr(0, 182, 112, 112))
    label(page.child(N(f"{U}/NowLabel", "NowLabel", TLr(0, 298, 112, 18))), "NOW 1", "Label", "NeutralLightest", align="center")
    label(page.child(N(f"{U}/Arrow", "Arrow", TLr(124, 222, 30, 32))), ">", "H2", "NeutralLightest", align="center")
    nxt = page.child(N(f"{U}/Next", "Next", TLr(166, 182, 112, 134)))
    nxt.add("cg", 225, canvas_group(True))
    element_flower(nxt, f"{U}/Next/Flower", "NextFlower", TLr(0, 0, 112, 112), level=START_LEVEL + 1)
    label(nxt.child(N(f"{U}/Next/Label", "NextLabel", TLr(0, 116, 112, 18))), "NEXT 2", "Label", "TextLight", align="center")
    box = page.child(N(f"{U}/L5", "LevelFive", main_r(340, 112)))
    box.add("cg", 225, canvas_group(True))
    box.child(N(f"{U}/L5/Fill", "Fill", STRETCH)).add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight", 0.14)))
    box.child(N(f"{U}/L5/Accent", "Accent", R((0, 0), (0, 1), (1, 0), (2, 0), (0.5, 0.5)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("NeutralLightest")))
    label(box.child(N(f"{U}/L5/Eyebrow", "Eyebrow", TLr(18, 14, 360, 16))), "AT LEVEL 5", "Label", "NeutralLightest")
    ellipsize(label(box.child(N(f"{U}/L5/Title", "Title", R((0, 1), (1, 1), (18, -36), (-36, 28), (0, 1)))), "", "H3", "TextLight"))
    ellipsize(label(box.child(N(f"{U}/L5/Line", "Line", R((0, 1), (1, 1), (18, -68), (-36, 36), (0, 1)))), "", "BodySmall",
                    "NeutralLightest", valign="top", wrap=True))
    # The UPGRADE button (gold: spending crystals), across the focused element's side at the bottom.
    text_button(page, f"{U}/Buy", "UpgradeButton", "UPGRADE  200",
                R((0, 0), (1, 0), (-UPG_MAIN / 2, 30), (-UPG_MAIN, 60), (0.5, 0.5)), None, None, None, None, "Purchase")

    def body(ctx, go):
        rows = ""
        for name, idx in ELEMENTS:
            k = f"{U}/{name}"
            rows += (f"  - element: {idx}\n    button: {ref(ctx.c(k, 'button'))}\n    flower: {ref(ctx.c(f'{k}/Flower', 'flower'))}\n"
                     f"    levelText: {ref(ctx.c(f'{k}/Level', 'tmp'))}\n    selected: {ref(ctx.c(f'{k}/Selected', 'img'))}\n")
        c = ctx.c
        return K.mono(go, UPGRADES_SCRIPT,
                      "  rows:\n" + rows
                      + f"  elementText: {ref(c(f'{U}/Element', 'tmp'))}\n  stepText: {ref(c(f'{U}/Step', 'tmp'))}\n"
                      f"  abilityText: {ref(c(f'{U}/Ability', 'tmp'))}\n  descriptionText: {ref(c(f'{U}/Desc', 'tmp'))}\n"
                      f"  nowFlower: {ref(c(f'{U}/Now', 'flower'))}\n  nowText: {ref(c(f'{U}/NowLabel', 'tmp'))}\n"
                      f"  nextGroup: {ref(c(f'{U}/Next', 'cg'))}\n  nextFlower: {ref(c(f'{U}/Next/Flower', 'flower'))}\n"
                      f"  nextText: {ref(c(f'{U}/Next/Label', 'tmp'))}\n  upgradeGroup: {ref(c(f'{U}/L5', 'cg'))}\n"
                      f"  upgradeEyebrowText: {ref(c(f'{U}/L5/Eyebrow', 'tmp'))}\n  upgradeTitleText: {ref(c(f'{U}/L5/Title', 'tmp'))}\n"
                      f"  upgradeLineText: {ref(c(f'{U}/L5/Line', 'tmp'))}\n"
                      f"  upgradeButton: {ref(c(f'{U}/Buy', 'button'))}\n  upgradeButtonText: {ref(c(f'{U}/Buy/Label', 'tmp'))}\n"
                      f"  headerLineText: {ref(c(f'{U}/Line', 'tmp'))}\n"
                      f"  elementTint: {K.TOKEN_INDEX['Purchase']}\n"
                      "  crystalsPerLevel: 100\n  popScale: 0.28\n  popSeconds: 0.45\n  morphSeconds: 0.5\n"
                      "  elementNames:\n  - Charge\n  - Mass\n  - Space\n  - Time\n"
                      "  defaultLine: Spend crystals to raise an element. The hull changes as it grows.\n"
                      "  noMorphLine: This hull keeps its shape at every level.\n"
                      "  stepTemplate: LEVEL {level} TO {next}\n  maxedStepTemplate: LEVEL {level} - MAX\n"
                      "  nowTemplate: NOW {level}\n  nextTemplate: NEXT {level}\n"
                      "  upgradeAheadTemplate: AT LEVEL {level}\n  upgradeReachedTemplate: UNLOCKED AT LEVEL {level}\n"
                      "  upgradeLevel: 5\n  openSlotTitle: Open slot\n  openSlotLine: Not designed yet.\n"
                      "  buyTemplate: UPGRADE  {cost}\n  maxedWord: MAX\n  bannerTemplate: LEVEL {level}\n"
                      "  bannerLineTemplate: '{ability} grows'\n")
    page.add("logic", 114, body)
    return page


def games_page(panel):
    page = page_node(panel, "Games", False)
    label(page.child(N("Tabs/Games/Head", "Head", TLr(0, 0, 460, 18))), "GAMES FOR THE VESSEL", "Label", "TextLight")
    label(page.child(N("Tabs/Games/Line", "Line", R((0, 1), (1, 1), (0, -24), (0, 18), (0.5, 1)))),
          "Every mode this hull can fly - press one to set it up.", "BodySmall", "NeutralLightest")
    scroll = page.child(N("Tabs/Games/Scroll", "Scroll", R((0, 0), (1, 1), (0, -27), (0, -54))))
    vp = scroll.child(N("Tabs/Games/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, lambda ctx, go: K.mono(go, KIT_RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n"))
    content = vp.child(N("Tabs/Games/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (5, 5, 5, 5), 10, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    scroll.add("scroll", 114, lambda ctx, go: K.mono(go, SCROLL_RECT,
               f"  m_Content: {ref(ctx.rt('Tabs/Games/Scroll/Viewport/Content'))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
               "  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
               f"  m_ScrollSensitivity: 20\n  m_Viewport: {ref(ctx.rt('Tabs/Games/Scroll/Viewport'))}\n"
               "  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n"
               "  m_HorizontalScrollbarVisibility: 2\n  m_VerticalScrollbarVisibility: 2\n"
               "  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n"
               "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    for i in range(GAME_TILES):
        k = f"Tabs/Games/Tile{i}"
        t = kit_row(content, k, f"Tile{i}", R((0, 1), (1, 1), (0, 0), (0, 128), (0.5, 1)), 128)
        t.add("cg", 225, canvas_group(i == 0))
        clip = t.child(N(f"{k}/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
        clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_sliver10_fill"), True)) \
            .add("mask", 114, lambda ctx, go: K.mono(go, MASK, "  m_ShowMaskGraphic: 0\n")) \
            .add("cover", 114, (lambda kk: lambda ctx, go: K.mono(go, COVER, f"  image: {ref(ctx.c(f'{kk}/Clip/Art', 'img'))}\n"))(k))
        clip.child(N(f"{k}/Clip/Art", "Art", R(size=(560, 315)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0)))
        clip.child(N(f"{k}/Clip/Fade", "Fade", STRETCH)).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("SurfaceBlack", 0.9), K.kit_sprite("kit_fade_h")))
        label(t.child(N(f"{k}/Eyebrow", "Eyebrow", TLr(20, 16, 300, 16))), "ANY VESSEL", "Label", "ControlActive")
        label(t.child(N(f"{k}/Title", "Title", TLr(20, 36, 400, 34))), "Skim Race", "H2", "TextLight")
        # No description line (the owner, 2026-10-17: "description should not be showing"): the tile is
        # the art, the eyebrow and the name; the configure window it opens carries the briefing.
    label(page.child(N("Tabs/Games/Empty", "Empty", R((0, 1), (1, 1), (0, -60), (0, 40), (0.5, 1)))),
          "No games list this vessel yet.", "BodySmall", "NeutralLightest").add("cg", 225, canvas_group(False))

    def body(ctx, go):
        tiles = ""
        for i in range(GAME_TILES):
            k = f"Tabs/Games/Tile{i}"
            tiles += (f"  - group: {ref(ctx.c(k, 'cg'))}\n    button: {ref(ctx.c(k, 'button'))}\n    art: {ref(ctx.c(f'{k}/Clip/Art', 'img'))}\n"
                      f"    eyebrowText: {ref(ctx.c(f'{k}/Eyebrow', 'tmp'))}\n    titleText: {ref(ctx.c(f'{k}/Title', 'tmp'))}\n"
                      "    lineText: {fileID: 0}\n")
        lists = "".join(f"  - {K.ref(11400000, g, 2)}\n" for g in GAME_LISTS)
        return K.mono(go, GAMES_SCRIPT,
                      "  tiles:\n" + tiles + "  lists:\n" + lists + "  screenSwitcher: {fileID: 0}\n"
                      f"  headerText: {ref(ctx.c('Tabs/Games/Head', 'tmp'))}\n  emptyGroup: {ref(ctx.c('Tabs/Games/Empty', 'cg'))}\n"
                      "  headerTemplate: GAMES FOR THE {vessel}\n  onlyThisTemplate: '{vessel} only'\n  anyVesselWord: Any vessel\n")
    page.add("logic", 114, body)
    return page


def pilots_page(panel):
    """Docs/Pilots/PILOTS_AND_ARTIFACTS.md: the vessel's pilots (two each, up to four rows) - portrait, name, the ability it
    rewrites and how, and its state (in the seat, stand-in art, art to come). A pilot is not tied to an element, so a row
    carries no element flower (the rev-24 rows read the per-element captains). A press seats the pilot; the header above
    the vessel names the focused one (HangarPilotsPage is an IHangarHeader)."""
    page = page_node(panel, "Pilots", False, top=PAGE_TOP)
    label(page.child(N("Tabs/Pilots/Line", "Line", R((0, 1), (1, 1), (0, 0), (0, 36), (0.5, 1)))),
          "Seat one pilot. Each one rewrites one ability. Press the seated pilot again to fly the base kit.", "BodySmall",
          "NeutralLightest", valign="top", wrap=True)
    lst = list_node(page, "Tabs/Pilots/List", 48, spacing=10)
    for i in range(4):
        k = f"Tabs/Pilots/Card{i}"
        c = kit_row(lst, k, f"Card{i}", R((0, 1), (1, 1), (0, 0), (0, 124), (0.5, 1)), 124)
        c.add("cg", 225, canvas_group(True))
        c.child(N(f"{k}/Underline", "Selected", R((0, 0), (1, 1), (0, 0), (6, 6)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), K.kit_sprite("kit_sliver10_line2"), True))
        c.child(N(f"{k}/Portrait", "Portrait", TLr(12, 14, 76, 96))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True))
        ellipsize(label(c.child(N(f"{k}/Name", "Name", R((0, 1), (1, 1), (104, -18), (-(104 + 180), 30), (0, 1)))), "", "H3", "TextLight"))
        label(c.child(N(f"{k}/State", "State", TRr(18, 22, 170, 18))), "", "Label", "ControlActive", align="right")
        ellipsize(label(c.child(N(f"{k}/Line", "Line", R((0, 1), (1, 1), (104, -54), (-(104 + 24), 56), (0, 1)))), "", "BodySmall",
                        "NeutralLightest", valign="top", wrap=True))
    label(page.child(N("Tabs/Pilots/Empty", "Empty", R((0, 1), (1, 1), (0, -60), (0, 40), (0.5, 1)))),
          "No pilots fly this vessel yet.", "BodySmall", "NeutralLightest").add("cg", 225, canvas_group(False))

    def body(ctx, go):
        cards_ = ""
        for i in range(4):
            k = f"Tabs/Pilots/Card{i}"
            cards_ += (f"  - group: {ref(ctx.c(k, 'cg'))}\n    button: {ref(ctx.c(k, 'button'))}\n    portrait: {ref(ctx.c(f'{k}/Portrait', 'img'))}\n"
                       f"    nameText: {ref(ctx.c(f'{k}/Name', 'tmp'))}\n    lineText: {ref(ctx.c(f'{k}/Line', 'tmp'))}\n"
                       f"    stateText: {ref(ctx.c(f'{k}/State', 'tmp'))}\n    underline: {ref(ctx.c(f'{k}/Underline', 'img'))}\n")
        return K.mono(go, PILOTS_SCRIPT,
                      f"  catalog: {K.ref(11400000, PILOT_CATALOG, 2)}\n"
                      f"  emptyGroup: {ref(ctx.c('Tabs/Pilots/Empty', 'cg'))}\n  cards:\n{cards_}"
                      "  lineTemplate: '{ability}: {rule}'\n  seatedWord: IN THE SEAT\n  standInWord: STAND-IN ART\n"
                      "  noArtWord: ART TO COME\n  baseKitWord: Base kit\n"
                      "  headerKickerTemplate: \"PILOT \\xB7 {vessel} \\xB7 {ability}\"\n  headerLineTemplate: '{rule}'\n")
    page.add("logic", 114, body)
    return page


def artifacts_page(panel):
    """Docs/Pilots/PILOTS_AND_ARTIFACTS.md: the vessel's artifact slots across the top (ARTIFACT_SLOTS frames; the catalog's
    slot count shows the first N) and every artifact that fits it below (its own, then the fleet's). A found artifact shows
    its rank and value and a press slots it; one not found yet reads Undiscovered. The header above the vessel names the
    slots (HangarArtifactsPage is an IHangarHeader)."""
    page = page_node(panel, "Artifacts", False, top=PAGE_TOP)
    slots = page.child(N("Tabs/Artifacts/Slots", "Slots", R((0, 1), (1, 1), (0, 0), (0, 96), (0.5, 1))))
    slots.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 0, (1, 1), (1, 1)))
    for i in range(ARTIFACT_SLOTS):
        k = f"Tabs/Artifacts/Slot{i}"
        sl = slots.child(N(k, f"Slot{i}", R(size=(140, 96))))
        sl.add("cg", 225, canvas_group(True))
        framed(sl, k, 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.65)
        sl.child(N(f"{k}/Glow", "Glow", R((0, 0), (1, 1), (0, 0), (4, 4)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), K.kit_sprite("kit_sliver14_line2"), True)).add("le", 114, le(ignore=True))
        sl.child(N(f"{k}/Hex", "Hex", TLr(14, 14, 26, 22))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("SurfaceLight", 0.4), K.kit_sprite("kit_hex_line2"), preserve=True)).add("le", 114, le(ignore=True))
        ellipsize(label(sl.child(N(f"{k}/Name", "Name", R((0, 1), (1, 1), (0, -44), (-28, 22), (0.5, 1)))), "Empty", "Body", "TextLight"))
        ellipsize(label(sl.child(N(f"{k}/Value", "Value", R((0, 1), (1, 1), (0, -70), (-28, 18), (0.5, 1)))), "", "BodySmall",
                        "ControlActive"))
    lst = list_node(page, "Tabs/Artifacts/List", 116, spacing=8)
    for i in range(ARTIFACT_ROWS):
        k = f"Tabs/Artifacts/Row{i}"
        c = kit_row(lst, k, f"Row{i}", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), 84)
        c.add("cg", 225, canvas_group(True))
        c.child(N(f"{k}/Underline", "Selected", R((0, 0), (1, 1), (0, 0), (6, 6)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), K.kit_sprite("kit_sliver10_line2"), True))
        ellipsize(label(c.child(N(f"{k}/Name", "Name", R((0, 1), (1, 1), (20, -14), (-(20 + 200), 28), (0, 1)))), "", "H3", "TextLight"))
        ellipsize(label(c.child(N(f"{k}/Line", "Line", R((0, 1), (1, 1), (20, -46), (-(20 + 200), 24), (0, 1)))), "", "BodySmall",
                        "NeutralLightest"))
        label(c.child(N(f"{k}/Tag", "Tag", TRr(18, 16, 170, 18))), "", "Label", "NeutralLightest", align="right")
        ellipsize(label(c.child(N(f"{k}/Value", "Value", TRr(18, 40, 170, 30))), "", "H3", "ControlActive", align="right"))
    label(page.child(N("Tabs/Artifacts/Empty", "Empty", R((0, 1), (1, 1), (0, -140), (0, 40), (0.5, 1)))),
          "No artifacts fit this vessel yet.", "BodySmall", "NeutralLightest").add("cg", 225, canvas_group(False))

    def body(ctx, go):
        slots_ = "".join(f"  - group: {ref(ctx.c(f'Tabs/Artifacts/Slot{i}', 'cg'))}\n"
                         f"    nameText: {ref(ctx.c(f'Tabs/Artifacts/Slot{i}/Name', 'tmp'))}\n"
                         f"    valueText: {ref(ctx.c(f'Tabs/Artifacts/Slot{i}/Value', 'tmp'))}\n"
                         f"    fill: {ref(ctx.c(f'Tabs/Artifacts/Slot{i}/Glow', 'img'))}\n" for i in range(ARTIFACT_SLOTS))
        rows_ = ""
        for i in range(ARTIFACT_ROWS):
            k = f"Tabs/Artifacts/Row{i}"
            rows_ += (f"  - group: {ref(ctx.c(k, 'cg'))}\n    button: {ref(ctx.c(k, 'button'))}\n"
                      f"    nameText: {ref(ctx.c(f'{k}/Name', 'tmp'))}\n    lineText: {ref(ctx.c(f'{k}/Line', 'tmp'))}\n"
                      f"    valueText: {ref(ctx.c(f'{k}/Value', 'tmp'))}\n    tagText: {ref(ctx.c(f'{k}/Tag', 'tmp'))}\n"
                      f"    underline: {ref(ctx.c(f'{k}/Underline', 'img'))}\n")
        return K.mono(go, ARTIFACTS_SCRIPT,
                      f"  catalog: {K.ref(11400000, PILOT_CATALOG, 2)}\n  slots:\n{slots_}  rows:\n{rows_}"
                      f"  emptyGroup: {ref(ctx.c('Tabs/Artifacts/Empty', 'cg'))}\n"
                      "  emptySlotWord: Empty\n  emptySlotLine: Press a found artifact\n  undiscoveredWord: Undiscovered\n"
                      "  hullTagTemplate: HULL {rank}\n  fleetTagTemplate: FLEET {rank}\n"
                      "  rankWords:\n  - \n  - RANK I\n  - RANK II\n  - RANK III\n"
                      "  headerKickerTemplate: \"{vessel} \\xB7 {slots} SLOTS\"\n  headerTitle: ARTIFACTS\n"
                      "  headerLineTemplate: '{slotted} of {slots} slotted. {found} found. Each one moves one number of the kit.'\n")
    page.add("logic", 114, body)
    return page


def text_tab(parent, k, name, caption, role, w, h):
    """A KitTextTab: its word, an underline that glides to the shown tab, a focus ring and the press sound."""
    n = parent.child(N(k, name, R(size=(w, h))))
    ring = n.child(N(f"{k}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10))))
    ring.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight", 0.0), K.kit_sprite("kit_sliver10_line2"), True)).add("le", 114, le(ignore=True))
    n.child(N(f"{k}/Hit", "Hit", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    lab = n.child(N(f"{k}/Label", "Label", STRETCH))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(caption, role, tok("NeutralLightest"), "center")) \
        .add("kt", 114, K.kit_text(role, "NeutralLightest", driven=True))
    n.child(N(f"{k}/Underline", "Underline", R((0, 0), (1, 0), (0, 0), (0, 2), (0.5, 0)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("Selection", 0.0)))
    n.add("tab", 114, lambda ctx, go: K.mono(go, KIT_TEXT_TAB,
          K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Hit', 'img'))}\n" + EMPTY_CALLS
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{k}/FocusRing', 'img'))}\n"
          "  isSelected: 0\n"
          f"  label: {ref(ctx.c(f'{k}/Label', 'tmp'))}\n  underline: {ref(ctx.c(f'{k}/Underline', 'img'))}\n"))
    n.add("audio", 114, lambda ctx, go: K.mono(go, MENU_AUDIO, "  category: 11\n"))
    return n


def keycap(key, caption):
    w = max(40, K.label_width(caption, "Label") + 18)
    return K.Instance(key, "KitKeycap", K.prefab_guid("KitKeycap"), "Keycap", R(size=(w, K.line_height("Label") + 8)),
                      [("Label#tmp", "m_text", caption, None)])


def tab_header(panel):
    """One row along the top, above the rail: LB, the five tabs, RB (Rev 25 round 2). LB / RB step the tabs."""
    head = panel.child(N("Tabs/Header", "Header", R((0, 1), (1, 1), (0, -8), (-2 * PAD, 52), (0.5, 1))))
    head.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), TAB_GAP, 3, (0, 0), (0, 0)))
    head.children.append(keycap("PartsLeftKeycap", "LB"))
    for name, _, _ in GROUPS:
        text_tab(head, f"Tabs/Header/{name}", name, name.upper(), TAB_ROLE, K.label_width(name.upper(), TAB_ROLE) + TAB_PAD, 52)
    head.children.append(keycap("PartsRightKeycap", "RB"))
    width = sum(K.label_width(n.upper(), TAB_ROLE) + TAB_PAD for n, _, _ in GROUPS) + 2 * 40 + TAB_GAP * (len(GROUPS) + 1)
    assert width <= DOSSIER_W - 2 * PAD - 20, f"the tab row runs into the rail's corner ({width})"


def build_tabs(panel):
    tab_header(panel)
    scan = panel.child(N("Tabs/Scan", "Scan", R((0, 1), (1, 1), (0, -PAGE_TOP_PARTS + 6), (-2 * PAD, 3), (0.5, 1))))
    scan.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive", 0.9), K.kit_sprite("kit_spark"))) \
        .add("cg", 225, lambda ctx, go: canvas_group(False)(ctx, go))
    pages = [abilities_page(panel), upgrades_page(panel), games_page(panel), pilots_page(panel), artifacts_page(panel)]

    def body(ctx, go):
        entries = ""
        for name in TAB_NAMES:
            logic = f"{{fileID: {ctx.c(f'Tabs/{name}', 'logic')}}}"
            button = ref(ctx.c(f'Tabs/Parts/{name}', 'tab')) if name in PARTS else "{fileID: 0}"
            entries += f"  - button: {button}\n    page: {ref(ctx.c(f'Tabs/{name}', 'cg'))}\n    pageLogic: {logic}\n"
        groups = ""
        for name, first, count in GROUPS:
            parts = "{fileID: 0}"
            if name == "Artifacts":
                # The page names its own slots (HangarArtifactsPage.TryGetHeader); these words show only without a catalog.
                words = ("    kickerTemplate: '{vessel}'\n    titleTemplate: ARTIFACTS\n"
                         "    lineTemplate: Each artifact moves one number of the kit.\n")
            else:
                words = "    kickerTemplate: \n    titleTemplate: \n    lineTemplate: \n"
            groups += (f"  - button: {ref(ctx.c(f'Tabs/Header/{name}', 'tab'))}\n    firstPage: {first}\n    pageCount: {count}\n"
                       f"    partsRow: {parts}\n" + words)
        assert not PARTS, "a parts row is back: every page is its own tab on the one row"
        return K.mono(go, TABS_SCRIPT,
                      "  tabs:\n" + entries + "  groups:\n" + groups
                      + f"  headerKickerText: {ref(ctx.c('Header/Kicker', 'tmp'))}\n"
                      f"  headerTitleText: {ref(ctx.c('Header/Title', 'tmp'))}\n"
                      f"  headerLineText: {ref(ctx.c('Header/Line', 'tmp'))}\n"
                      "  swapSeconds: 0.32\n  travel: 60\n  skewDegrees: 6\n"
                      f"  scanLine: {ref(ctx.rt('Tabs/Scan'))}\n  scanGroup: {ref(ctx.c('Tabs/Scan', 'cg'))}\n"
                      "  scanTravel: 720\n  shoulderInput: 1\n  northInput: 0\n")
    panel.add("tabs", 114, body)
    return pages


def line_node(parent, key, a, b, token, alpha, width=LINE_W):
    """A straight line from a to b (dossier units, y down): a thin Image, rotated, a half width longer at each end so the
    corners close."""
    (x0, y0), (x1, y1) = a, b
    length = math.hypot(x1 - x0, y1 - y0) + width
    n = parent.child(N(key, key.rsplit("/", 1)[-1], R((0, 1), None, ((x0 + x1) / 2, -(y0 + y1) / 2), (length, width))))
    n.rot = round(-math.degrees(math.atan2(y1 - y0, x1 - x0)), 4)
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok(token, alpha)))
    return n


def rail_points(w, h, lean=RAIL_LEAN, knee=RAIL_KNEE, rail_y=RAIL_Y, chamfer=RAIL_CHAMFER, start=RAIL_START):
    return [(0, h * RAIL_STUB), (0, h), (w - lean, h), (w, h * knee), (w, rail_y + chamfer),
            (w - chamfer, rail_y), (w * start, rail_y)]


def rail_border(panel, w, h, prefix="Abilities/Panel/Border", **shape):
    """Rev 24's border (R24Borders "2 Rail", Shombith 2026-10-26: "I just care about the angled panel border"): the old
    Hangar's Bracket_Large - open at the top, a short stub on the left, the right side leaning out on a long diagonal -
    closed on the right by a chamfered rail that stops short of the tabs, a faint dash where it ends and a corner mark.
    `shape` overrides rail_points' lean / knee / rail_y / chamfer / start (Rev 25's configure card runs the rail along
    its top edge)."""
    border = panel.child(N(prefix, "Border", STRETCH))
    pts = rail_points(w, h, **shape)
    for i, (a, b) in enumerate(zip(pts, pts[1:])):
        line_node(border, f"{prefix}/Line{i}", a, b, "NeutralLightest", 1.0)
    x, y = pts[-1]
    line_node(border, f"{prefix}/Dash", (x - 30, y), (x - 14, y), "NeutralLightest", 0.35)
    border.child(N(f"{prefix}/Corner", "Corner", TLr(0, h - 22, 22, 22))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight", 0.8), K.kit_sprite("kit_corner_tri")))
    return border


def nested_canvas():
    return lambda ctx, go: (
        "Canvas:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
        "  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n"
        "  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n"
        "  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_VertexColorAlwaysGammaSpace: 1\n"
        "  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n"
        "  m_SortingLayerID: 0\n  m_SortingOrder: 0\n  m_TargetDisplay: 0\n")


def glow_img(parent, key, name, r, token, alpha, sprite=None):
    n = parent.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok(token, alpha), sprite or BLOOM)) \
        .add("th", 114, themed(token, alpha))
    return n


def hangar_bay(clip):
    """Rev 4 (the owner, 2026-10-17: "don't like the black screen ... make this part way cooler"): a lit
    hangar bay behind the vessel instead of a black box - a deep glow, two coloured rim lights, a floor
    pool, three pulsing hex rings, a scan line sweeping and a few drifting motes. Its own nested Canvas:
    the ambient loops re-batch only the bay (and sleep while the Hangar is hidden, KitSightline)."""
    bay = clip.child(N("Hero/Clip/Bay", "Bay", STRETCH))
    bay.add("canvas", 223, nested_canvas())
    # Rev 25 round 2 (Shombith 2026-10-26: "In the middle remove the hued image behind the vessel"): the deep glow, the two
    # coloured rim lights and the floor pool are gone; the vessel stands on the sea with only its floor rings.
    for i, (w, h) in enumerate(((640, 120), (480, 90), (330, 62))):
        ring = glow_img(bay, f"Hero/Clip/Bay/Ring{i}", f"Ring{i}", R((0.5, 0), None, (0, 96), (w, h)), "ControlActive", 0.5,
                        K.kit_sprite("kit_hex_line2"))
        ring.add("cg", 225, canvas_group(True))
    # Still since 2026-10-24 (Garrett: "every pixel is important"): the rings no longer breathe, and the sweeping scan
    # line and the drifting motes are gone. The bay is a place, not a show; the vessel's moves are the motion here.


def stage_fx(clip):
    """Rev 4: the effects over the 3D vessel (HangarStageFx) - ability bolts and a muzzle flash, and the
    upgrade moment (crystals flying in, a ring, LEVEL N). Its own nested Canvas."""
    fx = clip.child(N("Hero/Clip/Fx", "Fx", STRETCH))
    fx.add("canvas", 223, nested_canvas())
    for i in range(6):
        b = fx.child(N(f"Hero/Clip/Fx/Bolt{i}", f"Bolt{i}", R(size=(130, 6))))
        b.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_spark")))
    fx.child(N("Hero/Clip/Fx/Flash", "Flash", R(size=(120, 120)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(K.WHITE, BLOOM))
    fx.child(N("Hero/Clip/Fx/Ring", "Ring", R(size=(220, 190)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_hex_line2")))
    for i in range(6):
        c = fx.child(N(f"Hero/Clip/Fx/Crystal{i}", f"Crystal{i}", R(size=(22, 22))))
        c.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Purchase"), K.chrome_sprite("chrome_crystal"), preserve=True))
    bn = fx.child(N("Hero/Clip/Fx/Banner", "Banner", R(pos=(0, -170), size=(600, 130))))
    bn.add("cg", 225, canvas_group(False))
    label(bn.child(N("Hero/Clip/Fx/Banner/Eyebrow", "Eyebrow", R((0, 1), (1, 1), (0, 0), (0, 18), (0.5, 1)))), "Mass", "Label",
          "ControlActive", align="center")
    label(bn.child(N("Hero/Clip/Fx/Banner/Title", "Title", R((0, 1), (1, 1), (0, -22), (0, 62), (0.5, 1)))), "LEVEL 1", "Display",
          "TextLight", align="center")
    label(bn.child(N("Hero/Clip/Fx/Banner/Line", "Line", R((0, 0), (1, 0), (0, 0), (0, 22), (0.5, 0)))), "", "Body",
          "TextLight", align="center")
    fx.add("fx", 114, lambda ctx, go: K.mono(go, FX_SCRIPT,
           "  bolts:\n" + "".join(f"  - {ref(ctx.c(f'Hero/Clip/Fx/Bolt{i}', 'img'))}\n" for i in range(6))
           + f"  flash: {ref(ctx.c('Hero/Clip/Fx/Flash', 'img'))}\n"
           "  boltSeconds: 0.35\n  boltStagger: 0.11\n  boltTravel: 0.55\n"
           f"  ring: {ref(ctx.c('Hero/Clip/Fx/Ring', 'img'))}\n"
           "  crystals:\n" + "".join(f"  - {ref(ctx.c(f'Hero/Clip/Fx/Crystal{i}', 'img'))}\n" for i in range(6))
           + f"  banner: {ref(ctx.c('Hero/Clip/Fx/Banner', 'cg'))}\n"
           f"  bannerEyebrow: {ref(ctx.c('Hero/Clip/Fx/Banner/Eyebrow', 'tmp'))}\n"
           f"  bannerTitle: {ref(ctx.c('Hero/Clip/Fx/Banner/Title', 'tmp'))}\n"
           f"  bannerLine: {ref(ctx.c('Hero/Clip/Fx/Banner/Line', 'tmp'))}\n"
           "  flySeconds: 0.55\n  ringSeconds: 0.6\n  bannerSeconds: 1.6\n"))
    return fx


def build_view(row_guid):
    """Rev 24 (Shombith 2026-10-26, option C taken forward): the vessel list on the LEFT; the vessel's header (it follows
    the tab) over the 3D vessel and the vibe bars in the centre; the dossier on the right in the old Hangar's open angled
    border, with OVERVIEW / PILOTS / ARTIFACTS on Y and OVERVIEW's parts on LB / RB."""
    root = N("root", "HangarView", STRETCH)
    # §70 (Shombith 2026-10-25: "Hangar should have it too"): the sea behind the whole screen, under the top bar, first so
    # every zone draws over it. Its wash dims the menu world behind the Hangar the way a hub window's veil does.
    # Rev 25 round 2 (Shombith 2026-10-26: "Hangar screen whole bg is too transparent. Make the bg look like the arcade
    # bg"): the Arcade window's backdrop exactly - the near-opaque veil (author_menu_arcade.WINDOW_BACKDROP_ALPHA), then
    # the sea lines over it.
    veil = root.child(N("Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84))))
    veil.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", WINDOW_BACKDROP_ALPHA), raycast=True)) \
        .add("th", 114, themed("SurfaceBlack", WINDOW_BACKDROP_ALPHA))
    sea = root.child(N("Sea", "Sea", R((0, 0), (1, 1), (0, -42), (0, -84))))
    sea.add("cr", 222, K.canvas_renderer()).add("img", 114, M.lattice_image(M.SEA_WINDOW_MAT))

    # ---- the header: the vessel on OVERVIEW, the pilot on PILOTS, the slot on ARTIFACTS (HangarTabsView writes it) ----
    frame = root.child(N("Frame", "Frame", R((0, 0), (1, 1), (0, -TOP_BAR / 2), (0, -TOP_BAR))))
    k = 0.5 / CONTENT_SCALE
    content = frame.child(N("Frame/Content", "Content", R((0.5 - k, 1 - 1 / CONTENT_SCALE), (0.5 + k, 1), (0, 0), (0, 0), (0.5, 1))))
    content.scale = CONTENT_SCALE
    header = content.child(N("Header", "Header", R((0, 1), (1, 1), ((CENTRE_L - CENTRE_R) / 2, -HEADER_TOP),
                                                (-(CENTRE_L + CENTRE_R), CENTRE_TOP - HEADER_TOP - 10), (0.5, 1))))
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    # A screen, not a modal: ScreenSwitcher replays these on every enter (KitEntrance.ReplayUnder).
    label(header.child(N("Header/Kicker", "Kicker", R((0, 1), (1, 1), (0, 0), (0, 18), (0.5, 1)))),
          "VESSEL - OWNED", "Label", "NeutralLightest")
    ellipsize(label(header.child(N("Header/Title", "Title", R((0, 1), (1, 1), (0, -22), (0, 56), (0.5, 1)))),
                    "SQUIRREL", "H1", "TextLight", valign="bottom"))
    label(header.child(N("Header/Line", "Line", R((0, 1), (1, 1), (0, -84), (0, 44), (0.5, 1)))),
          "Get in your flow and stay there.", "Body", "NeutralLightest", valign="top", wrap=True)
    # Section 82 ("Crystals is looking too clamped up"): clear of the top bar, the word and the pill apart, the pill sized
    # for the balance it shows (the kit pill's own padding, author_ui_kit_prefabs.build_currency_pill).
    bal = content.child(N("Header/Balance", "Balance", R((1, 1), None, (-SIDE, -16), (220, 40), (1, 1))))
    bal.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 20, 5, (0, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))
    label(bal.child(N("Header/Balance/Label", "Label", R(size=(K.label_width("CRYSTALS", "Label"), K.line_height("Label"))))),
          "CRYSTALS", "Label", "NeutralLightest")
    bal.children.append(pill_instance("HeaderPill", "CrystalBalance"))

    # ---- three zones over the whole screen, each placed on its own anchors ----
    body = content.child(N("Body", "Body", STRETCH))
    body.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", stagger=1))

    # Section 83: the list ends above the friends orb (bottom left, outside this scaled content).
    lz = body.child(N("List", "VesselList", R((0, 0), (0, 1), (SIDE, -HEADER_TOP), (LIST_W, -(HEADER_TOP + LIST_BOTTOM)), (0, 1))))
    lh = lz.child(N("List/Header", "Header", R((0, 1), (1, 1), (0, 0), (-10, 18), (0.5, 1))))
    label(lh.child(N("List/Header/Label", "Label", STRETCH)), "VESSELS", "Label", "TextLight")
    label(lh.child(N("List/Header/Count", "OwnedCount", STRETCH)), "1 / 11 owned", "Label", "NeutralLightest", align="right")
    scroll = lz.child(N("List/Scroll", "Scroll", R((0, 0), (1, 1), (0, -13), (0, -26))))
    vp = scroll.child(N("List/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, lambda ctx, go: K.mono(go, KIT_RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n"
                                                                   "  m_Softness: {x: 0, y: 0}\n"))
    content = vp.child(N("List/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (5, 5, 5, 5), 10, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    scroll.add("scroll", 114, lambda ctx, go: K.mono(go, SCROLL_RECT,
               f"  m_Content: {ref(ctx.rt('List/Scroll/Viewport/Content'))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
               "  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
               f"  m_ScrollSensitivity: 20\n  m_Viewport: {ref(ctx.rt('List/Scroll/Viewport'))}\n"
               "  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n"
               "  m_HorizontalScrollbarVisibility: 2\n  m_VerticalScrollbarVisibility: 2\n"
               "  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n"
               "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))

    # ---- centre: the 3D vessel over the sea (no box: "keep the vessel like this"), the vibe bars, the unlock button ----
    cz = body.child(N("Selected", "SelectedVessel", R((0, 0), (1, 1), ((CENTRE_L - CENTRE_R) / 2, (BOTTOM - CENTRE_TOP) / 2),
                                                      (-(CENTRE_L + CENTRE_R), -(CENTRE_TOP + BOTTOM)))))
    hero = cz.child(N("Hero", "Hero", R((0, 0), (1, 1), (0, (VIBE_H + 8) / 2), (0, -(VIBE_H + 8)))))
    clip = hero.child(N("Hero/Clip", "Clip", STRETCH))
    clip.add("mask", 114, lambda ctx, go: K.mono(go, KIT_RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n"
                                                                     "  m_Softness: {x: 0, y: 0}\n"))
    hangar_bay(clip)
    stage = clip.child(N("Hero/Clip/Stage", "Stage", STRETCH))
    stage.add("cr", 222, K.canvas_renderer()) \
        .add("raw", 114, lambda ctx, go: K.mono(go, RAW_IMAGE,
             "  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 1\n"
             "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
             "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
             "  m_Texture: {fileID: 0}\n  m_UVRect:\n    serializedVersion: 2\n    x: 0\n    y: 0\n    width: 1\n    height: 1\n")) \
        .add("stage", 114, lambda ctx, go: K.mono(go, STAGE_SCRIPT,
             f"  prefabs: {K.ref(11400000, VESSEL_PREFABS, 2)}\n  modelRadius: 4\n  distanceFactor: 2.6\n"
             "  fieldOfView: 34\n  pitchDegrees: 18\n  idleDegreesPerSecond: 14\n  dragDegreesPerPixel: 0.35\n"
             "  stickDegreesPerSecond: 160\n  idleResumeSeconds: 2.5\n"
             "  zoomRange: {x: 0.6, y: 2.2}\n  wheelZoomStep: 1.12\n  padZoomPerSecond: 2.5\n  panLimit: 1.5\n"
             "  resetSeconds: 0.35\n"
             f"  hintText: {ref(ctx.c('Hero/Turn', 'tmp'))}\n"
             f"  pointerHint: {POINTER_HINT}\n  padHint: {PAD_HINT}\n  touchHint: {TOUCH_HINT}\n"
             "  performSeconds: 0.55\n  performLunge: 1.4\n"
             "  performRollDegrees: 90\n  performLightIntensity: 6\n"
             f"  fx: {ref(ctx.c('Hero/Clip/Fx', 'fx'))}\n  pulseSeconds: 0.6\n  pulseScale: 0.12\n"
             "  renderRate: 30\n  resolution: 768\n"))
    stage_fx(clip)
    clip.child(N("Hero/Clip/Fallback", "FallbackIcon", R(size=(256, 256)))) \
        .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True)) \
        .add("cg", 225, canvas_group(False))
    chip = hero.child(N("Hero/Chip", "StatusChip", R((0, 1), None, (0, -4), (110, 30), (0, 1))))
    chip.add("hlg", 114, K.hv_layout(False, (12, 12, 5, 5), 8, 3, (1, 1), (0, 0))).add("csf", 114, K.fitter(2, 2))
    framed(chip, "Hero/Chip", 10, "SurfaceVeryDark", "SurfaceLight")
    chip.child(N("Hero/Chip/Lock", "Lock", R(size=(20, 20)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("Purchase", 0.0), K.chrome_sprite("chrome_lock"), preserve=True)) \
        .add("th", 114, themed("Purchase", 0.0)).add("le", 114, le(ignore=True, pw=20, ph=20))
    label(chip.child(N("Hero/Chip/Label", "Label", R(size=(K.label_width("OWNED", "Label"), K.line_height("Label"))))),
          "Owned", "Label", "TextLight")
    # At the stage's foot, under the floor rings (Rev 25 round 2: nothing written over a picture). Section 82: how to hold
    # the model (turn on any axis, zoom, move, reset), for the device in hand - the stage writes the line.
    ellipsize(label(hero.child(N("Hero/Turn", "TurnHint", R((0, 0), (1, 0), (0, 4), (-40, 18), (0.5, 0)))),
                    POINTER_HINT, "Label", "NeutralLightest", align="center"))

    # Rev 24 ("for the vibe they look like sliders ... they can be like bars"): each of the vessel's three scales is a row
    # of segments, lit from the left up to its value.
    vz = cz.child(N("Vibe", "Vibe", R((0, 0), None, (0, 0), (VIBE_W, VIBE_H), (0, 0))))
    label(vz.child(N("Vibe/Title", "Title", TLr(0, 0, 360, 18))), "VIBE - HOW IT PLAYS", "Label", "TextLight")
    seg_x, seg_w, seg_gap = 100, 22, 4
    for i in range(3):
        k = f"Vibe/Row{i}"
        row = vz.child(N(k, f"Row{i}", R((0, 1), (1, 1), (0, -(28 + i * 28)), (0, 22), (0.5, 1))))
        label(row.child(N(f"{k}/Left", "Left", TLr(0, 0, 90, 22))), "Casual", "BodySmall", "TextLight", align="right")
        for j in range(VIBE_SEGMENTS):
            row.child(N(f"{k}/Seg{j}", f"Seg{j}", TLr(seg_x + j * (seg_w + seg_gap), 7, seg_w, 8))) \
                .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight" if j < 5 else "SurfaceDark")))
        right_x = seg_x + VIBE_SEGMENTS * (seg_w + seg_gap) + 6
        assert right_x + 120 <= VIBE_W, "the vibe bars run out of their box"
        label(row.child(N(f"{k}/Right", "Right", TLr(right_x, 0, 120, 22))), "Challenging", "BodySmall", "TextLight")
    vz.add("vibe", 114, lambda ctx, go: K.mono(go, VIBE_SCRIPT, "  sliders:\n" + "".join(
        f"  - leftText: {ref(ctx.c(f'Vibe/Row{i}/Left', 'tmp'))}\n    rightText: {ref(ctx.c(f'Vibe/Row{i}/Right', 'tmp'))}\n"
        "    segments:\n" + "".join(f"    - {ref(ctx.c(f'Vibe/Row{i}/Seg{j}', 'img'))}\n" for j in range(VIBE_SEGMENTS))
        + "    thumb: {fileID: 0}\n    fill: {fileID: 0}\n" for i in range(3))
        + f"  litToken: {K.TOKEN_INDEX['TextLight']}\n  unlitToken: {K.TOKEN_INDEX['SurfaceDark']}\n"))

    # At the stage's top right, across from the status chip (Rev 25 round 2: it sat on the floor rings). Chip and button
    # together stay inside the centre zone at 16:10 (1728 wide).
    slot = cz.child(N("Unlock", "Unlock", R((1, 1), None, (0, -2), (300, 60), (1, 1))))
    assert 110 + 300 + 20 <= 1728 - CENTRE_L - CENTRE_R, "the status chip and UNLOCK meet at 16:10"
    locked = slot.child(N("Unlock/Locked", "Locked", STRETCH)).add("cg", 225, canvas_group(False))
    locked.children.append(button_instance("UnlockButton", "UnlockButton", "Purchase", False, "UNLOCK - 4000", icon=True))
    owned = slot.child(N("Unlock/Owned", "Owned", STRETCH)).add("cg", 225, canvas_group(True))
    owned.children.append(button_instance("UnlockedButton", "UnlockedButton", "Inactive", False, "UNLOCKED", disabled=True))

    # ---- right: the dossier in the open angled border (R24Borders "2 Rail") ----
    rz = body.child(N("Abilities", "Dossier", R((1, 1), None, (-SIDE, -DOSSIER_TOP), (DOSSIER_W, DOSSIER_H), (1, 1))))
    panel = rz.child(N("Abilities/Panel", "Panel", STRETCH))
    panel.child(N("Abilities/Panel/Fill", "Fill", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_cradle_fill")))
    rail_border(panel, DOSSIER_W, DOSSIER_H)
    build_tabs(panel)

    # ---- the spend-crystals popup ----
    pop = root.child(N("Popup", "SpendCrystals", STRETCH)).add("cg", 225, canvas_group(False))
    pop.child(N("Popup/Overlay", "Overlay", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack", 0.886), raycast=True)).add("th", 114, themed("SurfaceBlack", 0.886))
    dlg = pop.child(N("Popup/Dialog", "Dialog", R(pos=(0, 40), size=(640, 230))))
    dlg.add("vlg", 114, K.hv_layout(True, (36, 36, 30, 62), 16, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    framed(dlg, "Popup/Dialog", 14, "SurfaceBlack", "TextLight")
    label(dlg.child(N("Popup/Dialog/Eyebrow", "Eyebrow", R(size=(568, K.line_height("Label"))))), "SPEND", "Label", "NeutralLightest")
    label(dlg.child(N("Popup/Dialog/Title", "Title", R(size=(568, K.line_height("H2"))))),
          "4000 to unlock Manta", "H2", "TextLight", wrap=True)
    brow = dlg.child(N("Popup/Dialog/Balance", "Balance", R(size=(568, 40))))
    brow.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 3, (0, 0), (0, 0)))
    label(brow.child(N("Popup/Dialog/Balance/Label", "Label", R(size=(K.label_width("BALANCE", "Label"), K.line_height("Label"))))),
          "BALANCE", "Label", "NeutralLightest")
    brow.children.append(pill_instance("PopupPill", "CrystalBalance"))
    label(dlg.child(N("Popup/Dialog/Shortfall", "Shortfall", R(size=(568, K.line_height("BodySmall"))))),
          "You need 1240 more crystals.", "BodySmall", "NeutralLightest", wrap=True) \
        .add("cg", 225, canvas_group(False)).add("le", 114, le(ignore=True))
    btns = dlg.child(N("Popup/Dialog/Buttons", "Buttons", R((0, 0), None, (36, 0), (440, 52), (0, 0.5))))
    btns.add("le", 114, le(ignore=True)).add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 18, 3, (0, 0), (0, 0))) \
        .add("csf", 114, K.fitter(2, 2))
    btns.children.append(button_instance("ConfirmButton", "Confirm", "Purchase", True, "CONFIRM - 4000", icon=True))
    btns.children.append(button_instance("CancelButton", "Cancel", "Cancel", True, "CANCEL"))

    def detail_body(ctx, go):
        c = ctx.c
        return K.mono(go, DETAIL_VIEW,
                      "  vesselNameText: {fileID: 0}\n  vesselDescriptionText: {fileID: 0}\n"
                      f"  stage: {ref(c('Hero/Clip/Stage', 'stage'))}\n"
                      f"  tabs: {ref(c('Abilities/Panel', 'tabs'))}\n"
                      f"  vibe: {ref(c('Vibe', 'vibe'))}\n"
                      f"  fallbackIcon: {ref(c('Hero/Clip/Fallback', 'img'))}\n"
                      f"  fallbackGroup: {ref(c('Hero/Clip/Fallback', 'cg'))}\n"
                      "  lockedArtAlpha: 0.5\n"
                      f"  chipLabel: {ref(c('Hero/Chip/Label', 'kt'))}\n"
                      f"  chipBorder: {ref(c('Hero/Chip/Border', 'th'))}\n"
                      f"  chipLock: {ref(c('Hero/Chip/Lock', 'th'))}\n"
                      f"  chipLockLayout: {ref(c('Hero/Chip/Lock', 'le'))}\n"
                      f"  unlockButton: {ref(ctx.iref('UnlockButton', 'root#button'))}\n"
                      f"  unlockLabel: {ref(ctx.iref('UnlockButton', 'Label#kt'))}\n"
                      f"  unlockGroup: {ref(c('Unlock/Locked', 'cg'))}\n"
                      f"  unlockedGroup: {ref(c('Unlock/Owned', 'cg'))}\n"
                      f"  headerBalanceText: {ref(ctx.iref('HeaderPill', 'Amount#kt'))}\n"
                      f"  popupBalanceText: {ref(ctx.iref('PopupPill', 'Amount#kt'))}\n"
                      f"  unlockPanel: {ref(c('Popup', 'cg'))}\n"
                      f"  spendCrystalsDetailText: {ref(c('Popup/Dialog/Title', 'tmp'))}\n"
                      f"  shortfallText: {ref(c('Popup/Dialog/Shortfall', 'kt'))}\n"
                      f"  shortfallGroup: {ref(c('Popup/Dialog/Shortfall', 'cg'))}\n"
                      f"  shortfallLayout: {ref(c('Popup/Dialog/Shortfall', 'le'))}\n"
                      f"  confirmButton: {ref(ctx.iref('ConfirmButton', 'root#button'))}\n"
                      f"  confirmLabel: {ref(ctx.iref('ConfirmButton', 'Label#kt'))}\n"
                      f"  cancelButton: {ref(ctx.iref('CancelButton', 'root#button'))}\n"
                      "  lockedWord: Locked\n  ownedWord: Owned\n  kickerTemplate: \"VESSEL \\xB7 {state}\"\n"
                      "  unlockTemplate: UNLOCK - {cost}\n"
                      "  unlockFreeWord: UNLOCK - FREE\n  spendTemplate: '{cost} to unlock {vessel}'\n"
                      "  confirmTemplate: CONFIRM - {cost}\n  shortfallTemplate: You need {amount} more crystals.\n")
    root.add("detail", 114, detail_body)
    return root


# The detail view's fields, each with the class / script its target must be (validation).
DETAIL_FIELDS = {
    "stage": STAGE_SCRIPT, "tabs": TABS_SCRIPT,
    "vibe": VIBE_SCRIPT,
    "fallbackIcon": K.IMAGE, "fallbackGroup": 225, "chipLabel": KIT_TEXT, "chipBorder": KIT_THEMED,
    "chipLock": KIT_THEMED, "chipLockLayout": K.LAYOUT_ELEMENT,
    "unlockButton": KIT_BUTTON, "unlockLabel": KIT_TEXT, "unlockGroup": 225, "unlockedGroup": 225,
    "headerBalanceText": KIT_TEXT, "popupBalanceText": KIT_TEXT, "unlockPanel": 225,
    "spendCrystalsDetailText": K.TMP_UGUI, "shortfallText": KIT_TEXT, "shortfallGroup": 225,
    "shortfallLayout": K.LAYOUT_ELEMENT, "confirmButton": KIT_BUTTON, "confirmLabel": KIT_TEXT,
    "cancelButton": KIT_BUTTON,
}


# --------------------------------------------------------------------------------------------
# Prefab outputs
# --------------------------------------------------------------------------------------------

PREFAB_META = K.PREFAB_META


def outputs():
    out = {}
    tile_text, tile_comp = tile_variant_text()
    out[f"{OUT_DIR}/HangarVesselTile.prefab"] = tile_text
    view = build_view(None)
    out[f"{OUT_DIR}/HangarView.prefab"] = prefab_text(view, "HangarView")
    for name in PREFABS:
        out[f"{OUT_DIR}/{name}.prefab.meta"] = PREFAB_META.format(g=asset_guid(name))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Hangar"))
    return out, tile_comp


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

SCREEN_FIELDS_RETIRED = ("gridPanel", "gridContainer", "gridCardPrefab", "eyeButton", "detailPanel")
SCREEN_FIELDS_OWNED = ("vesselList", "vesselScroll", "vesselTilePrefab", "ownedCountText", "detailView")


def doc_fields(doc):
    """Top-level `key: value` pairs of a MonoBehaviour body, continuation lines joined."""
    body = re.sub(r",\n\s{4,}", ", ", doc)
    return re.findall(r"^  (\w+):(.*)$", body, re.M), body


class Ids:
    def __init__(self):
        self.ctx = PCtx("Menu_Main/Hangar")
        f = self.ctx.fid
        self.inst = str(f("HangarView#inst"))
        self.stub = str(f("HangarView#stub"))
        src = lambda key: K.FileIds("HangarView")(key)
        self.refs = {
            "vesselList": ("List/Scroll/Viewport/Content#rt", 224, None),
            "vesselScroll": ("List/Scroll#scroll", 114, SCROLL_RECT),
            "ownedCountText": ("List/Header/Count#kt", 114, KIT_TEXT),
            "detailView": ("root#detail", 114, DETAIL_VIEW),
        }
        self.src = {k: src(v[0]) for k, v in self.refs.items()}
        self.stubs = {k: str(derived(self.src[k], self.inst)) for k in self.refs}

    def all(self):
        return {self.inst, self.stub} | set(self.stubs.values())


def stub_for(ids, field):
    key, cls, script = ids.refs[field]
    fid, src, guid, pid = ids.stubs[field], ids.src[field], asset_guid("HangarView"), ids.inst
    if cls == 224:
        return (f"--- !u!224 &{fid} stripped\nRectTransform:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
                f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return T.stub_mono(fid, src, guid, pid, script)


def screen_doc(old, ids, tile_comp):
    head = old[:old.index("  ShipList:")]
    fields, _ = doc_fields(old[len(head):])
    values = dict(fields)
    new = {
        "vesselList": f" {ref(ids.stubs['vesselList'])}",
        "vesselScroll": f" {ref(ids.stubs['vesselScroll'])}",
        "vesselTilePrefab": f" {K.ref(tile_comp, asset_guid('HangarVesselTile'))}",
        "ownedCountText": f" {ref(ids.stubs['ownedCountText'])}",
        "detailView": f" {ref(ids.stubs['detailView'])}",
    }
    order = ["ShipList", "vesselList", "vesselScroll", "vesselTilePrefab", "ownedCountText", "cardStaggerDelay",
             "cardFadeDuration", "detailView"] + [k for k, _ in fields if k not in SCREEN_FIELDS_RETIRED
                                                  and k not in new and k not in
                                                  ("ShipList", "cardStaggerDelay", "cardFadeDuration")]
    lines = "".join(f"  {k}:{new.get(k, values.get(k))}\n" for k in order)
    return head + lines


def transform(text, prefab_out=None):
    sc = T.Scene(text)
    ids = Ids()
    generated = ids.all()
    _, tile_comp = prefab_out if prefab_out else outputs()

    assert sc.has(HANGAR["go"]) and sc.field(HANGAR["go"], "m_Name") == "Hangar Screen", "Hangar Screen moved"
    assert sc.script(HANGAR["screen"]) == HANGAR_SCREEN, "HangarScreen component moved"
    assert sc.has(HANGAR["legacy_rt"]), "Hangar_Panel moved"
    if any(sc.has(r) for r in RETIRED_ROOTS):
        clash = generated & set(sc.order)
        assert not clash, f"minted fileIDs already in the scene: {sorted(clash)[:3]}"

    for root, name in RETIRED_ROOTS.items():
        if sc.has(root):
            assert sc.field(root, "m_Name") == name, f"retired root {root} is no longer {name}"
            for fid in sc.subtree(root):
                sc.remove(fid)
    for fid in list(sc.order):
        if fid in generated:
            sc.remove(fid)

    out = []
    K.emit_instance(ids.ctx, K.Instance("HangarView", "HangarView", asset_guid("HangarView"), "HangarView", STRETCH),
                    HANGAR["rt"], out)
    out += [stub_for(ids, k) for k in ids.refs]
    out = [K.unity_empty_scalars(d) for d in out]
    anchor = HANGAR["rt"]
    for d in out:
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)

    kids = [c for c in sc.children(HANGAR["rt"]) if sc.has(c) and c != ids.stub]
    sc.replace(HANGAR["rt"], T.set_block(sc.docs[HANGAR["rt"]], "m_Children",
                                         K.refs_list("m_Children", kids + [ids.stub])))
    sc.replace(HANGAR["screen"], screen_doc(sc.docs[HANGAR["screen"]], ids, tile_comp))
    return sc.text()


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def known_guids():
    return T.known_guids() | PACKAGE


def check_file(path, text, guids, minted):
    anchors = re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M)
    dupes = sorted({a for a in anchors if anchors.count(a) > 1})
    assert not dupes, f"{path}: duplicate fileID anchors {dupes[:3]}"
    aset = set(anchors) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", text):
        assert m.group(1) in aset, f"{path}: dangling local fileID {m.group(1)}"
    for g in set(re.findall(r"guid: ([0-9a-f]{32})", text)):
        assert g in guids or g in minted, f"{path}: guid {g} resolves to no asset"
    assert text.isascii(), f"{path}: non-ASCII text"


def source_anchors(guid, prefab_files):
    """fileIDs that exist in the prefab a guid names - generated output first, then the repo."""
    for path, text in prefab_files.items():
        if path.endswith(".prefab") and asset_guid(os.path.basename(path)[:-7]) == guid:
            return set(re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M)), text
    for name in ("KitButton", "KitTile", "KitDivider", "KitCurrencyPill", "KitKeycap"):
        if K.prefab_guid(name) == guid:
            t = open(os.path.join(REPO, K.PREFAB_DIR, name + ".prefab")).read()
            return set(re.findall(r"^--- !u!\d+ &(-?\d+)", t, re.M)), t
    raise AssertionError(f"no source prefab for guid {guid}")


def check_sources(path, text, files):
    """Every stub and every override names an object that exists in its source prefab."""
    for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                             r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", text):
        anchors, _ = source_anchors(g, files)
        assert src in anchors, f"{path}: source object {src} not in the prefab {g}"


def doc_of(text, fid):
    m = re.search(r"^--- !u!(\d+) &" + re.escape(str(fid)) + r"( stripped)?\n(.*?)(?=^--- |\Z)", text, re.M | re.S)
    return (int(m.group(1)), m.group(3)) if m else (None, None)


def target_kind(text, fid, files):
    """(class id, script guid) of the object a local fileID names, following a stub to its source."""
    cls, body = doc_of(text, fid)
    assert cls is not None, f"fileID {fid} has no document"
    m = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
    return cls, (m.group(1) if m else None)


def check_flowers(text):
    """Every KitElementFlower: five petals, each its own Image, rotated 72 degrees from the last (ElementalBarsView's
    flower); a petal out of place draws a flower the HUD never shows."""
    flowers = [m for m in re.finditer(r"^--- !u!114 &(-?\d+)\n((?:(?!--- ).*\n)*)", text, re.M)
               if f"guid: {FLOWER_SCRIPT}" in m.group(2)]
    assert flowers, "no element flower in the Hangar"
    for m in flowers:
        petals = re.findall(r"^  - \{fileID: (-?\d+)\}", m.group(2).split("  petals:\n")[1], re.M)[:5]
        assert len(petals) == 5 and "0" not in petals, f"flower {m.group(1)} needs five petals"
        for i, img in enumerate(petals):
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", doc_of(text, img)[1]).group(1)
            rt = re.search(r"^--- !u!224 &(-?\d+)\nRectTransform:\n(?:(?!--- ).*\n)*?  m_GameObject: \{fileID: " + go + r"\}\n((?:(?!--- ).*\n)*)",
                           text, re.M)
            hint = float(re.search(r"m_LocalEulerAnglesHint: \{x: 0, y: 0, z: (-?[\d.e-]+)\}", rt.group(0)).group(1))
            assert abs(hint + 72 * i) < 1e-3, f"flower {m.group(1)}: petal {i} is turned {hint:g}, not {-72 * i}"


def validate_prefabs(files, guids):
    M.require_entrances(files, {f"{OUT_DIR}/HangarView.prefab": 2, f"{OUT_DIR}/HangarVesselTile.prefab": 1})
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for path, text in files.items():
        if path.endswith(".meta"):
            continue
        check_file(path, text, guids, minted)
        check_sources(path, text, files)
    # The detail view: every field set, each to the right kind of object.
    view = files[f"{OUT_DIR}/HangarView.prefab"]
    assert f"guid: {M.SEA_WINDOW_MAT}, type: 2" in view, "the Hangar has no sea behind it (§70)"
    detail = K.FileIds("HangarView")("root#detail")
    _, body = doc_of(view, detail)
    for field, want in DETAIL_FIELDS.items():
        m = re.search(r"^  " + field + r": \{fileID: (-?\d+)\}", body, re.M)
        assert m and m.group(1) != "0", f"HangarVesselDetailView.{field} is not wired"
        cls, script = target_kind(view, m.group(1), files)
        ok = cls == want if isinstance(want, int) else script == want
        assert ok, f"HangarVesselDetailView.{field} points at the wrong kind of object ({cls}, {script})"
    assert "abilityRowPrefab" not in body, "HangarVesselDetailView still names the retired ability-row prefab"
    # Rev 3: five pages, each wired to its page and its component (ARTIFACTS too, since the pilots work). Rev 24: OVERVIEW's three parts carry their
    # buttons in the parts row; the three tabs (OVERVIEW, PILOTS, ARTIFACTS) are groups over the pages, and the header the
    # tabs write is wired.
    hv = K.FileIds("HangarView")
    _, tabs = doc_of(view, hv("Abilities/Panel#tabs"))
    pages_part, groups_part = tabs.split("  groups:\n")[0], tabs.split("  groups:\n")[1].split("  headerKickerText:")[0]
    assert pages_part.count("  - button: ") == len(TAB_NAMES), "HangarTabsView must name five pages"
    for name in TAB_NAMES:
        button = f"{{fileID: {hv(f'Tabs/Parts/{name}#tab')}}}" if name in PARTS else "{fileID: 0}"
        assert f"button: {button}\n    page: {{fileID: {hv(f'Tabs/{name}#cg')}}}" in pages_part, f"page {name}: button or page unwired"
        assert f"pageLogic: {{fileID: {hv(f'Tabs/{name}#logic')}}}" in pages_part, f"page {name}: page logic unwired"
    assert groups_part.count("  - button: ") == len(TAB_NAMES), "HangarTabsView must name five tabs on the one row"
    for name, first, count in GROUPS:
        parts = "{fileID: 0}"
        assert (f"  - button: {{fileID: {hv(f'Tabs/Header/{name}#tab')}}}\n    firstPage: {first}\n    pageCount: {count}\n"
                f"    partsRow: {parts}\n") in groups_part, f"tab {name}: button, pages or parts row wrong"
    for field, key in (("headerKickerText", "Header/Kicker"), ("headerTitleText", "Header/Title"), ("headerLineText", "Header/Line")):
        assert f"  {field}: {{fileID: {hv(f'{key}#tmp')}}}\n" in tabs, f"HangarTabsView.{field} unwired (the header would not follow the tab)"
    assert "  northInput: 0\n" in tabs and "  shoulderInput: 1\n" in tabs, "LB / RB step the tabs; Y is free"
    assert "m_Name: Parts\n" not in view and "m_Name: TabHint\n" not in view, \
        "a second tab row is back (Shombith: all the tabs on the same bar)"
    check_flowers(view)
    # The pages clear the right side where it leans in: at a page's floor the side is RAIL_LEAN x (how far below the
    # knee) in from the edge, and the fill is the sprite drawn at exactly the dossier's size.
    floor = DOSSIER_H - PAGE_BOTTOM
    lean = RAIL_LEAN * (floor - RAIL_KNEE * DOSSIER_H) / ((1 - RAIL_KNEE) * DOSSIER_H)
    assert PAD_R >= lean, f"a page's right edge crosses the leaning side ({PAD_R} < {lean:.1f} at the page floor)"
    SP = _load("author_ui_kit_sprites")
    assert (SP.CRADLE_W, SP.CRADLE_H, SP.CRADLE_LEAN, SP.CRADLE_KNEE) == (DOSSIER_W, DOSSIER_H, RAIL_LEAN, RAIL_KNEE), \
        "kit_cradle_fill was drawn for another dossier: the fill and the rail would not register"
    # The open angled border: the cradle fill and the rail's lines, ending on the corner mark.
    assert f"guid: {K.kit_sprite('kit_cradle_fill')}" in view and f"guid: {K.kit_sprite('kit_corner_tri')}" in view, \
        "the dossier lost its open cradle (fill or corner mark)"
    assert all(f"m_Name: Line{i}\n" in view for i in range(len(rail_points(1, 1)) - 1)), "the dossier's rail is missing a line"
    _, stage = doc_of(view, hv("Hero/Clip/Stage#stage"))
    assert stage and f"prefabs: {{fileID: 11400000, guid: {VESSEL_PREFABS}" in stage, "the stage has no vessel prefab table - it would build nothing"
    # Section 82: the player holds the model - zoom range, and the line that says how, for each device.
    assert f"  hintText: {{fileID: {hv('Hero/Turn#tmp')}}}" in stage, "the stage does not write its how-to-hold line"
    for field, line in (("pointerHint", POINTER_HINT), ("padHint", PAD_HINT), ("touchHint", TOUCH_HINT)):
        assert f"  {field}: {line}\n" in stage, f"the stage's {field} is not the authored line"
    zr = re.search(r"  zoomRange: \{x: ([\d.]+), y: ([\d.]+)\}", stage)
    assert zr and float(zr.group(1)) < 1 < float(zr.group(2)) <= 2.6, "the stage's zoom range must straddle the framed view " \
        "and stop before the camera enters the hull (distanceFactor 2.6)"
    # Rev 4: the stage reaches its effects, the bay and the effects each batch on their own canvas, and
    # every UPGRADE row reaches its button and label (an unwired one would show a dead price).
    assert f"  fx: {{fileID: {hv('Hero/Clip/Fx#fx')}}}" in stage, "the stage does not reach its effects (HangarStageFx)"
    _, fxb = doc_of(view, hv("Hero/Clip/Fx#fx"))
    assert fxb and fxb.count("  - {fileID: ") >= 12 and f"  flash: {{fileID: {hv('Hero/Clip/Fx/Flash#img')}}}" in fxb, \
        "HangarStageFx needs its six bolts, its flash and its six crystals"
    for key in ("Hero/Clip/Bay", "Hero/Clip/Fx"):
        _, cv = doc_of(view, hv(f"{key}#canvas"))
        assert cv and cv.startswith("Canvas:"), f"{key} must carry its own nested Canvas (its loops re-batch only it)"
    _, upg = doc_of(view, hv("Tabs/Upgrades#logic"))
    assert f"  upgradeButton: {{fileID: {hv('Tabs/Upgrades/Buy#button')}}}" in upg, "the UPGRADE button is unwired"
    assert f"  upgradeButtonText: {{fileID: {hv('Tabs/Upgrades/Buy/Label#tmp')}}}" in upg, "the UPGRADE label is unwired"
    for name, idx in ELEMENTS:
        k = f"Tabs/Upgrades/{name}"
        assert (f"  - element: {idx}\n    button: {{fileID: {hv(f'{k}#button')}}}\n    flower: {{fileID: {hv(f'{k}/Flower#flower')}}}\n")\
            in upg, f"{name}: the UPGRADES element tile or its flower is unwired"
    for field in ("nowFlower", "nextFlower"):
        m = re.search(r"^  " + field + r": \{fileID: (-?\d+)\}", upg, re.M)
        assert m and target_kind(view, m.group(1), files)[1] == FLOWER_SCRIPT, f"HangarUpgradesPage.{field} is not a flower"
    _, abil = doc_of(view, hv("Tabs/Abilities#logic"))
    assert abil and [int(x) for x in re.findall(r"  - element: (\d+)", abil)] == [i for _, i in ELEMENTS], \
        "the Abilities rows must be Charge, Mass, Space, Time"
    # Section 83: a press plays an ability and nothing plays on its own - no AI demo timings, and nothing of TRY
    # survives (no forward to a preview, no window over the stage, no hint, no bare-arena asset on disk).
    for field in ("keyPressSeconds", "keyPressScale"):
        assert f"\n  {field}: " in abil, f"the Abilities page has no press field {field}"
    assert "demoStepSeconds" not in abil, "the Abilities page plays on its own again (the AI demo is retired, section 84)"
    for name, _ in ELEMENTS:
        k = f"Tabs/Abilities/{name}"
        _, btn = doc_of(view, hv(f"{k}#button"))
        assert re.search(r"  m_Navigation:\n    m_Mode: 3\n", btn), f"the {name} ability row is not a pad stop (A plays it)"
        assert not doc_of(view, hv(f"{k}#cg"))[1], f"the {name} ability row blocks its clicks"
        assert doc_of(view, hv(f"{k}#audio"))[1], f"the {name} ability row has no press sound"
    # Section 83: one scaled content under the top bar, and the list clear of the friends orb.
    _, content_rt = doc_of(view, hv("Frame/Content#rt"))
    assert f"m_LocalScale: {{x: {K.f32(CONTENT_SCALE)}, y: {K.f32(CONTENT_SCALE)}, z: 1}}" in content_rt, \
        "the Hangar's content is not drawn at its scale"
    assert LIST_BOTTOM * CONTENT_SCALE >= ORB_TOP - 1, "the vessel list runs under the friends orb"
    assert "tryPreview" not in abil, "the Abilities page still forwards to TRY"
    for gone in ("TryButton", "TryPreview", "TryHint", "TryLine"):
        assert f"m_Name: {gone}\n" not in view, f"retired TRY node {gone} is back in HangarView"
    for path in RETIRED_FILES:
        assert path not in files and not os.path.exists(os.path.join(REPO, path)), f"retired TRY file is back: {path}"
    # A label stretched across its row with a LEFT pivot starts at its anchoredPosition.x: a negative
    # one starts outside the row (2026-10-17: every ability name and line sat under the icon, x -38).
    for m in re.finditer(r"^--- !u!224 &(-?\d+)\n((?:(?!--- ).*\n)*)", view, re.M):
        d = m.group(2)
        if ("m_AnchorMin: {x: 0," in d and "m_AnchorMax: {x: 1," in d and "m_Pivot: {x: 0," in d):
            x = float(re.search(r"m_AnchoredPosition: \{x: (-?[\d.e-]+)", d).group(1))
            assert x >= 0, f"RectTransform {m.group(1)} is stretched with a left pivot and starts {x:g} px left of its parent"
    _, vibe = doc_of(view, hv("Vibe#vibe"))
    assert vibe and vibe.count("  - leftText: ") == 3 and vibe.count("    - {fileID: ") == 3 * VIBE_SEGMENTS, \
        f"the vibe view needs its three bars of {VIBE_SEGMENTS} segments"
    assert "    - {fileID: 0}" not in vibe, "a vibe segment is unwired"
    for name in RETIRED_PREFABS:
        assert f"guid: {asset_guid(name)}" not in view, f"HangarView still references the retired {name}"
    # The tile variant: its component reaches the KitTile and its parts.
    tile = files[f"{OUT_DIR}/HangarVesselTile.prefab"]
    tcomp = K.FileIds("HangarVesselTile")("root#hangarTile")
    _, tbody = doc_of(tile, tcomp)
    for field, want in (("tile", KIT_TILE), ("icon", K.IMAGE), ("nameText", K.TMP_UGUI), ("statusText", KIT_TEXT),
                        ("canvasGroup", 225)):
        m = re.search(r"^  " + field + r": \{fileID: (-?\d+)\}", tbody, re.M)
        assert m, f"HangarVesselTile.{field} missing"
        cls, script = target_kind(tile, m.group(1), files)
        assert (cls == want) if isinstance(want, int) else (script == want), f"HangarVesselTile.{field} is wrong"
    # Section 81: the list shows the hulls' colourful renders, so the tile must reach the art table.
    assert re.search(r"^  cardArt: \{fileID: 11400000, guid: " + K.meta_guid(CARD_ART) + r", type: 2\}$", tbody, re.M), \
        "HangarVesselTile.cardArt must name the Hangar card art table (author_hangar_card_art.py)"
    assert f"guid: {M._lattice_guid('hull_lit_mat')}" not in tile, "the colourful card art must not be drawn with Hull_Lit"


def validate_scene(before_text, after_text, files, guids):
    before, sc = T.Scene(before_text), T.Scene(after_text)
    ids = Ids()
    generated = ids.all()
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for root in RETIRED_ROOTS:
        assert not sc.has(root), f"retired {RETIRED_ROOTS[root]} still in the scene"

    removed = set(before.order) - set(sc.order)
    added = set(sc.order) - set(before.order)
    retired = set()
    for root in RETIRED_ROOTS:
        if before.has(root):
            retired |= set(before.subtree(root))
    assert added <= generated, f"unexpected new documents {sorted(added - generated)[:3]}"
    assert removed <= retired | generated, f"unexpected removals {sorted(removed - retired - generated)[:3]}"
    changed = {f for f in set(before.order) & set(sc.order)
               if T.normalize(before.docs[f]) != T.normalize(sc.docs[f])}
    assert changed <= {HANGAR["rt"], HANGAR["screen"]} | generated, f"unexpected edits {sorted(changed)[:3]}"

    # HangarScreen changes only in the fields this item owns; its legacy wiring is untouched.
    if before.has(HANGAR["screen"]):
        bf, _ = doc_fields(before.docs[HANGAR["screen"]])
        af, _ = doc_fields(sc.docs[HANGAR["screen"]])
        owned = set(SCREEN_FIELDS_RETIRED) | set(SCREEN_FIELDS_OWNED)
        keep_b = {k: v.strip() for k, v in bf if k not in owned}
        keep_a = {k: v.strip() for k, v in af if k not in owned}
        assert keep_b == keep_a, "HangarScreen edited outside the list / detail fields"
    af = dict((k, v.strip()) for k, v in doc_fields(sc.docs[HANGAR["screen"]])[0])
    for k in SCREEN_FIELDS_RETIRED:
        assert k not in af, f"HangarScreen still serializes the retired field {k}"
    for field in ids.refs:
        assert af.get(field) == ref(ids.stubs[field]), f"HangarScreen.{field} is not wired to the view"
        stub = sc.docs[ids.stubs[field]]
        assert f"{{fileID: {ids.src[field]}, guid: {asset_guid('HangarView')}" in stub, f"{field}: wrong source"
        assert f"m_PrefabInstance: {{fileID: {ids.inst}}}" in stub, f"{field}: wrong instance"
    tile_comp = K.FileIds("HangarVesselTile")("root#hangarTile")
    assert af.get("vesselTilePrefab") == K.ref(tile_comp, asset_guid("HangarVesselTile")), "vesselTilePrefab"
    view_text = files[f"{OUT_DIR}/HangarView.prefab"]
    for field in ids.refs:
        cls, script = target_kind(view_text, ids.src[field], files)
        want = ids.refs[field]
        assert cls == want[1] and (want[2] is None or script == want[2]), f"{field}: wrong kind in HangarView"
    inst = sc.docs[ids.inst]
    assert f"m_TransformParent: {{fileID: {HANGAR['rt']}}}" in inst
    assert sc.children(HANGAR["rt"]) == [HANGAR["legacy_rt"], ids.stub], "Hangar Screen children"


def drift(files):
    out = []
    for p, t in files.items():
        full = os.path.join(REPO, p)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != t:
            out.append(p)
    return out


def self_test(current, files, guids):
    expected = transform(current, (files, K.FileIds("HangarVesselTile")("root#hangarTile")))
    validate_prefabs(files, guids)
    validate_scene(current, expected, files, guids)
    ids = Ids()
    view_path = f"{OUT_DIR}/HangarView.prefab"
    detail = K.FileIds("HangarView")("root#detail")

    def scene_plant(fn):
        return lambda: validate_scene(current, fn(expected), files, guids)

    def view_plant(fn):
        f2 = dict(files)
        f2[view_path] = fn(files[view_path])
        return lambda: validate_prefabs(f2, guids)

    def tile_plant(fn):
        f2 = dict(files)
        tile_path = f"{OUT_DIR}/HangarVesselTile.prefab"
        f2[tile_path] = fn(files[tile_path])
        return lambda: validate_prefabs(f2, guids)

    _, dbody = doc_of(files[view_path], detail)
    name_ref = str(K.FileIds("HangarView")("Header/Title#tmp"))
    vibe_ref = re.search(r"  vibe: \{fileID: (-?\d+)\}", dbody).group(1)
    plants = {
        "the screen's entrances stripped": view_plant(M.strip_entrances),
        "the Hangar without its sea": view_plant(lambda t: t.replace(M.SEA_WINDOW_MAT, "0" * 32)),
        "an ability row out of pad navigation again": view_plant(lambda t: T.in_doc(
            t, str(K.FileIds("HangarView")("Tabs/Abilities/Charge#button")), "    m_Mode: 3\n", "    m_Mode: 0\n")),
        "the Hangar drawn at full size": view_plant(lambda t: T.in_doc(
            t, str(K.FileIds("HangarView")("Frame/Content#rt")), f"m_LocalScale: {{x: {K.f32(CONTENT_SCALE)}, y: {K.f32(CONTENT_SCALE)}, z: 1}}",
            "m_LocalScale: {x: 1, y: 1, z: 1}")),
        "detail field swapped onto the wrong kind": view_plant(lambda t: t.replace(
            f"chipLabel: {{fileID:", f"chipLabel: {{fileID: {name_ref}}}\n  chipLabelX: {{fileID:", 1)),
        "detail field unwired": view_plant(lambda t: t.replace(
            f"  vibe: {{fileID: {vibe_ref}}}", "  vibe: {fileID: 0}", 1)),
        "stub names an object missing from KitButton": view_plant(lambda t: re.sub(
            r"(m_CorrespondingSourceObject: \{fileID: )\d+(, guid: " + K.prefab_guid("KitButton") + ")", r"\g<1>4242\2", t, count=1)),
        "HangarScreen.detailView unwired": scene_plant(lambda s: s.replace(
            f"detailView: {{fileID: {ids.stubs['detailView']}}}", "detailView: {fileID: 0}")),
        "legacy HangarScreen field edited": scene_plant(lambda s: T.in_doc(
            s, HANGAR["screen"], "  HangarTrainingModal: {fileID: 3673826220853711041}", "  HangarTrainingModal: {fileID: 0}")),
        "retired field left serialized": scene_plant(lambda s: s.replace(
            "  cardStaggerDelay:", "  eyeButton: {fileID: 0}\n  cardStaggerDelay:", 1)),
        "unrelated document edited": scene_plant(lambda s: s.replace("  m_Name: Screens\n", "  m_Name: Screenz\n", 1)),
        "the Games tab's page unwired": view_plant(lambda t: t.replace(
            f"page: {{fileID: {K.FileIds('HangarView')('Tabs/Games#cg')}}}", "page: {fileID: 0}", 1)),
        "an ability name starting left of its row": view_plant(lambda t: re.sub(
            r"(m_AnchorMin: \{x: 0, y: 1\}\n  m_AnchorMax: \{x: 1, y: 1\}\n  m_AnchoredPosition: \{x: )" + str(ABILITY_TEXT_X)
            + r"(, y: -14\})", r"\g<1>-38\2", t, count=1)),
        "the AI demo back": view_plant(lambda t: t.replace("\n  keyPressSeconds:", "\n  demoStepSeconds: 2.4\n  keyPressSeconds:", 1)),
        "TRY's window back over the stage": view_plant(lambda t: t.replace("m_Name: FallbackIcon\n", "m_Name: TryPreview\n", 1)),
        "the stage not reaching its effects": view_plant(lambda t: re.sub(
            r"(\n  fx: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "the bay batching on the screen's canvas": view_plant(lambda t: t.replace(
            f"&{K.FileIds('HangarView')('Hero/Clip/Bay#canvas')}\nCanvas:", f"&{K.FileIds('HangarView')('Hero/Clip/Bay#canvas')}\nCanvasGroup:", 1)),
        "the UPGRADE button unwired": view_plant(lambda t: re.sub(
            r"(\n  upgradeButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "a flower petal turned out of place": view_plant(lambda t: t.replace(
            "m_LocalEulerAnglesHint: {x: 0, y: 0, z: -144}", "m_LocalEulerAnglesHint: {x: 0, y: 0, z: -140}", 1)),
        "a second tab row back": view_plant(lambda t: t.replace("m_Name: Header\n", "m_Name: Parts\n", 1)),
        "Y stepping the tabs again": view_plant(lambda t: t.replace("  northInput: 0\n", "  northInput: 1\n", 1)),
        "the header not following the tab": view_plant(lambda t: re.sub(
            r"(\n  headerTitleText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "a vibe segment unwired": view_plant(lambda t: re.sub(r"(\n    segments:\n    - )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "the rail missing a line": view_plant(lambda t: t.replace("m_Name: Line3\n", "m_Name: Lime3\n", 1)),
        "the list's tile without its card art": tile_plant(lambda t: re.sub(
            r"(\n  cardArt: )\{[^}]*\}", r"\1{fileID: 0}", t, count=1)),
        "the card art drawn through Hull_Lit": tile_plant(lambda t: t.replace(
            "    m_RemovedComponents: []", "    - target: {fileID: 0}\n      propertyPath: m_Material\n      value: \n"
            f"      objectReference: {{fileID: 2100000, guid: {M._lattice_guid('hull_lit_mat')}, type: 2}}\n"
            "    m_RemovedComponents: []", 1)),
        "the stage that cannot zoom in": view_plant(lambda t: t.replace("  zoomRange: {x: 0.6, y: 2.2}", "  zoomRange: {x: 0.6, y: 1}", 1)),
        "the stage's how-to-hold line unwired": view_plant(lambda t: re.sub(r"(\n  hintText: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "the stage with no vessel table": view_plant(lambda t: t.replace(
            f"prefabs: {{fileID: 11400000, guid: {VESSEL_PREFABS}", "prefabs: {fileID: 0, guid: 0", 1)),
    }
    failed = []
    for name, fn in plants.items():
        try:
            fn()
            failed.append(name)
        except AssertionError as e:
            print(f"  caught: {name}: {e}")
    assert not failed, f"validation MISSED planted defects: {failed}"
    print(f"OK: {len(plants)} planted defects all caught")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    guids = known_guids()
    files, tile_comp = outputs()
    path = os.path.join(REPO, SCENE)
    with open(path, encoding="utf-8", newline="") as fh:
        current = fh.read()
    if args.self_test:
        self_test(current, files, guids)
        return 0

    validate_prefabs(files, guids)
    expected = transform(current, (files, tile_comp))
    validate_scene(current, expected, files, guids)
    migrated = any(T.Scene(current).has(r) for r in RETIRED_ROOTS)

    retired = [f"{OUT_DIR}/{n}.prefab" for n in RETIRED_PREFABS if os.path.exists(os.path.join(REPO, OUT_DIR, n + ".prefab"))]
    if args.check:
        problems = drift(files) + [f"{r} (retired - re-run to delete)" for r in retired]
        if migrated:
            problems.append(f"{SCENE} (still carries GridPanel / DetailPanel)")
        else:
            validate_scene(current, current, files, guids)
            ok, diff = T.semantic_equal(current, expected)
            if not ok:
                problems.append(f"{SCENE} {diff}")
        for p in problems:
            print("DRIFT:", p)
        print(f"FAIL: {len(problems)} output(s) differ" if problems
              else "OK: Hangar prefabs and Menu_Main match the generator; wiring validated on the committed scene")
        return 1 if problems else 0

    os.makedirs(os.path.join(REPO, OUT_DIR), exist_ok=True)
    for p, t in files.items():
        os.makedirs(os.path.dirname(os.path.join(REPO, p)), exist_ok=True)
        with open(os.path.join(REPO, p), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    for r in retired:
        for f in (r, r + ".meta"):
            if os.path.exists(os.path.join(REPO, f)):
                os.remove(os.path.join(REPO, f))
    if expected != current:
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(expected)
    print(f"wrote {len(files)} prefab files" + ("; migrated Menu_Main's Hangar" if migrated else "; Menu_Main unchanged"
                                                 if expected == current else "; updated Menu_Main"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
