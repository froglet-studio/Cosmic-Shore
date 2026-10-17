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

TILE_H = 64


def tile_variant_text():
    base = K.prefab_guid("KitTile")
    F = K.FileIds("HangarVesselTile")
    pid = F("variant#inst")

    def d(key):
        return derived(K.source_fid("KitTile", key), pid)

    mods = [("root#go", "m_Name", "HangarVesselTile"),
            ("root#rt", "m_SizeDelta.x", 330), ("root#rt", "m_SizeDelta.y", TILE_H),
            ("Art#rt", "m_AnchoredPosition.x", 14), ("Art#rt", "m_SizeDelta.x", 48), ("Art#rt", "m_SizeDelta.y", 48),
            ("Lock#rt", "m_AnchoredPosition.x", 38), ("Lock#rt", "m_SizeDelta.x", 26), ("Lock#rt", "m_SizeDelta.y", 26),
            ("Name#rt", "m_AnchoredPosition.x", 31), ("Name#rt", "m_AnchoredPosition.y", 10),
            ("Name#rt", "m_SizeDelta.x", -86), ("Name#rt", "m_SizeDelta.y", 24),
            ("Status#rt", "m_AnchoredPosition.x", 31), ("Status#rt", "m_AnchoredPosition.y", -12),
            ("Status#rt", "m_SizeDelta.x", -86), ("Status#rt", "m_SizeDelta.y", 18)]
    lines = "".join(f"    - target: {K.ref(K.source_fid('KitTile', k), base)}\n      propertyPath: {p}\n"
                    f"      value: {K.f32(v) if not isinstance(v, str) else v}\n      objectReference: {{fileID: 0}}\n"
                    for k, p, v in mods)
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
                f"  statusText: {ref(d('Status#kt'))}\n  canvasGroup: {ref(cg_c)}\n"))
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
TRY_SCRIPT = K.meta_guid("Assets/_Scripts/UI/Elements/Hangar/HangarAbilityPreview.cs")
PREVIEW_WINDOW = K.meta_guid("Assets/_Scripts/UI/View/ModePreviewWindow.cs")
PREVIEW_DEF_SCRIPT = K.meta_guid("Assets/_Scripts/ScriptableObjects/ModePreviewDefinitionSO.cs")
BARREN_CELL = K.meta_guid("Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset")
# TRY's bare arena (T-20261016-1ec1). Its own folder, NOT Mode Previews: generators and the card-art
# renderer glob that folder for one definition per arcade card, and this one is no card.
TRY_DIR = "Assets/_SO_Assets/Hangar"
TRY_ASSET = f"{TRY_DIR}/HangarTryPreview.asset"
KIT_TEXT_TAB = K.kit_script("KitTextTab")
MENU_AUDIO = K.meta_guid("Assets/_Scripts/UI/MenuAudio.cs")
VESSEL_PREFABS = K.meta_guid("Assets/_SO_Assets/Vessel Prefab Container.asset")
GLYPH_SET = K.meta_guid("Assets/Resources/ControlGlyphSet.asset")
LOCKUP_STYLE = K.meta_guid("Assets/Resources/AbilityLockupStyle.asset")
CAPTAINS = K.meta_guid("Assets/_SO_Assets/Captains/Elemental/ElementalCaptains.asset")
GAME_LISTS = [K.meta_guid("Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"),
              K.meta_guid("Assets/_SO_Assets/Games/GameLists/ArenaGames.asset")]
ELEMENTS = [("Charge", 1), ("Mass", 2), ("Space", 3), ("Time", 4)]
TAB_NAMES = ["Abilities", "Upgrades", "Games", "Pilots", "Artifacts"]
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


def kit_row(parent, key, name, r, layout_h, content_keys=()):
    """A selectable row: focus ring, glass fill, hairline, KitButton (Cancel variant, outline) and the
    menu's press sound. The row's own content draws itself; the button paints only its frame."""
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
        return K.mono(go, KIT_BUTTON,
                      K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n" + EMPTY_CALLS
                      + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{key}/FocusRing', 'img'))}\n"
                      f"  variant: {K.VARIANTS['Cancel']}\n  fill: 0\n"
                      f"  fillGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n  borderGraphic: {ref(ctx.c(f'{key}/Border', 'img'))}\n"
                      + (f"  contentGraphics:\n{content}" if content else "  contentGraphics: []\n") + "  playGradient: {fileID: 0}\n")
    b.add("button", 114, body)
    b.add("audio", 114, lambda ctx, go: K.mono(go, MENU_AUDIO, "  category: 11\n"))
    return b


def page_node(panel, key, shown, own_canvas=False):
    p = panel.child(N(f"Tabs/{key}", key, R((0, 0), (1, 1), (0, -36), (-44, -132))))
    p.add("cg", 225, canvas_group(shown))
    if own_canvas:
        # A page that animates every frame (the element halos) gets its own Canvas, so its writes
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


def abilities_page(panel):
    page = page_node(panel, "Abilities", True, own_canvas=True)
    page_header(page, "Tabs/Abilities", "PRESS A CONTROL TO TRY IT", "Labels follow your device. The vessel performs each one.")
    lst = list_node(page, "Tabs/Abilities/List", 54)
    for name, _ in ELEMENTS:
        k = f"Tabs/Abilities/{name}"
        row = kit_row(lst, k, name, R((0, 1), (1, 1), (0, 0), (0, 94), (0.5, 1)), 94)
        halo(row, f"{k}/Halo", TLr(-6, -1, 96, 96), round(0.25 * [n for n, _ in ELEMENTS].index(name), 2))
        row.child(N(f"{k}/Hex", "Hex", TLr(14, 19, 56, 56))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("SurfaceLight", 0.35), K.kit_sprite("kit_hex_fill"), preserve=True))
        row.child(N(f"{k}/Icon", "Icon", TLr(24, 29, 36, 36))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True))
        ellipsize(label(row.child(N(f"{k}/Name", "Name", R((0, 1), (1, 1), (86, -14), (-(86 + 96), 26), (0, 1)))), name, "H3", "TextLight"))
        row.child(N(f"{k}/Glyph", "Glyph", TLr(86, 46, 16, 16))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), element_glyph(name), preserve=True))
        label(row.child(N(f"{k}/Element", "Element", TLr(108, 45, 160, 18))), name, "Label", "NeutralLightest")
        ellipsize(label(row.child(N(f"{k}/Desc", "Desc", R((0, 1), (1, 1), (86, -66), (-(86 + 96), 20), (0, 1)))), "", "BodySmall", "NeutralLightest"))
        cap = row.child(N(f"{k}/Keycap", "Keycap", TRr(18, 27, 64, 40)))
        cap.add("cg", 225, canvas_group(True))
        cap.child(N(f"{k}/Keycap/Fill", "Fill", STRETCH)).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("SurfaceVeryDark", 0.7), K.kit_sprite("kit_sliver10_fill"), True))
        cap.child(N(f"{k}/Keycap/Border", "Border", STRETCH)).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver10_line1"), True))
        label(cap.child(N(f"{k}/Keycap/Label", "Label", STRETCH)), "RT", "Label", "TextLight", align="center")
    label(page.child(N("Tabs/Abilities/Core", "Core", R((0, 0), (1, 0), (0, 8), (0, 18), (0.5, 0)))),
          "Core: skim by flying close to a prism, drift with the left stick.", "BodySmall", "NeutralLightest")
    # TRY (T-20261016-1ec1): the real thing - the selected vessel flown in open water, its abilities
    # firing for real and the drill coaching them. The rows above still play the stage's stand-in.
    try_w = 16 + K.label_width("TRY IT FOR REAL", "ButtonSmall") + 16
    text_button(page, "Tabs/Abilities/Try", "TryButton", "TRY IT FOR REAL", R((0, 0), None, (0, 40), (try_w, 44), (0, 0)),
                "Tabs/Abilities", "try", "CosmicShore.UI.HangarAbilityPreview", "Try", "Cta")
    label(page.child(N("Tabs/Abilities/TryLine", "TryLine", R((0, 0), (1, 0), ((try_w + 16) / 2, 52), (-(try_w + 16), 20), (0.5, 0)))),
          "Fly it in open water. START or Esc hands it back.", "BodySmall", "NeutralLightest")

    def body(ctx, go):
        rows = ""
        for name, idx in ELEMENTS:
            k = f"Tabs/Abilities/{name}"
            rows += (f"  - element: {idx}\n    button: {ref(ctx.c(k, 'button'))}\n    icon: {ref(ctx.c(f'{k}/Icon', 'img'))}\n"
                     f"    nameText: {ref(ctx.c(f'{k}/Name', 'tmp'))}\n    elementText: {ref(ctx.c(f'{k}/Element', 'tmp'))}\n"
                     f"    descriptionText: {ref(ctx.c(f'{k}/Desc', 'tmp'))}\n    elementGlyph: {ref(ctx.c(f'{k}/Glyph', 'img'))}\n"
                     f"    keycapGroup: {ref(ctx.c(f'{k}/Keycap', 'cg'))}\n    keycapText: {ref(ctx.c(f'{k}/Keycap/Label', 'tmp'))}\n"
                     f"    keycapBorder: {ref(ctx.c(f'{k}/Keycap/Border', 'img'))}\n    glow: {ref(ctx.c(f'{k}/Glow', 'img'))}\n"
                     f"    hex: {ref(ctx.c(f'{k}/Hex', 'img'))}\n    halo: {ref(ctx.c(f'{k}/Halo', 'halo'))}\n")
        return K.mono(go, ABILITIES_SCRIPT,
                      "  rows:\n" + rows
                      + f"  prefabs: {K.ref(11400000, VESSEL_PREFABS, 2)}\n  glyphSet: {K.ref(11400000, GLYPH_SET, 2)}\n"
                      f"  lockupStyle: {K.ref(11400000, LOCKUP_STYLE, 2)}\n"
                      "  elementNames:\n  - Charge\n  - Mass\n  - Space\n  - Time\n"
                      "  openSlotName: Open slot\n  openSlotLine: Not designed yet.\n  passiveWord: Passive\n  glowSeconds: 0.9\n"
                      f"  tryPreview: {ref(ctx.c('Tabs/Abilities', 'try'))}\n")
    page.add("logic", 114, body)
    page.add("try", 114, lambda ctx, go: K.mono(go, TRY_SCRIPT,
             f"  window: {ref(ctx.c('Hero/Clip/Try', 'window'))}\n"
             f"  windowGroup: {ref(ctx.c('Hero/Clip/Try', 'cg'))}\n"
             f"  flyingHint: {ref(ctx.c('Hero/TryHint', 'cg'))}\n"
             f"  definition: {K.ref(11400000, asset_guid('HangarTryPreview'), 2)}\n"
             "  session: {fileID: 0}\n"
             f"  stage: {ref(ctx.c('Hero/Clip/Stage', 'stage'))}\n  placeName: Hangar\n"))
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


def upgrades_page(panel):
    page = page_node(panel, "Upgrades", False, own_canvas=True)
    page_header(page, "Tabs/Upgrades", "ELEMENTAL LEVELS", "Spend crystals to raise an element. The hull changes as it grows.")
    lst = list_node(page, "Tabs/Upgrades/List", 54)
    for name, _ in ELEMENTS:
        k = f"Tabs/Upgrades/{name}"
        row = kit_row(lst, k, name, R((0, 1), (1, 1), (0, 0), (0, 112), (0.5, 1)), 112)
        halo(row, f"{k}/Halo", TLr(-9, -9, 80, 80), round(0.25 * [n for n, _ in ELEMENTS].index(name), 2))
        row.child(N(f"{k}/Glyph", "Glyph", TLr(16, 16, 30, 30))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), element_glyph(name), preserve=True))
        ellipsize(label(row.child(N(f"{k}/Title", "Title", R((0, 1), (1, 1), (60, -14), (-(60 + 80), 24), (0, 1)))), name, "Body", "TextLight"))
        label(row.child(N(f"{k}/LevelLine", "LevelLine", TLr(60, 40, 220, 18))), "Level 0 of 10", "BodySmall", "NeutralLightest")
        label(row.child(N(f"{k}/Level", "Level", TRr(18, 14, 48, 26))), "0", "Data", "TextLight", align="right")
        for p in range(10):
            row.child(N(f"{k}/Pip{p}", f"Pip{p}", TLr(60 + p * 26, 66, 20, 10))).add("cr", 222, K.canvas_renderer()) \
                .add("img", 114, K.image(tok("SurfaceDark")))
        ellipsize(label(row.child(N(f"{k}/Upgrade", "Upgrade", R((0, 1), (1, 1), (60, -86), (-(60 + 200), 18), (0, 1)))), "", "Label", "NeutralLightest"))
        # Rev 4: UPGRADE - buys the next level (HangarUpgradesPage wires the click; the price is on the label).
        text_button(row, f"{k}/Buy", "UpgradeButton", "UPGRADE  100", TRr(18, 52, 176, 38), None, None, None, None, "Cta")

    def body(ctx, go):
        rows = ""
        for name, idx in ELEMENTS:
            k = f"Tabs/Upgrades/{name}"
            pips = "".join(f"    - {ref(ctx.c(f'{k}/Pip{p}', 'img'))}\n" for p in range(10))
            rows += (f"  - element: {idx}\n    button: {ref(ctx.c(k, 'button'))}\n    titleText: {ref(ctx.c(f'{k}/Title', 'tmp'))}\n"
                     f"    lineText: {ref(ctx.c(f'{k}/LevelLine', 'tmp'))}\n    upgradeText: {ref(ctx.c(f'{k}/Upgrade', 'tmp'))}\n"
                     f"    levelText: {ref(ctx.c(f'{k}/Level', 'tmp'))}\n    pips:\n{pips}"
                     f"    elementGlyph: {ref(ctx.c(f'{k}/Glyph', 'img'))}\n    glow: {ref(ctx.c(f'{k}/Glow', 'img'))}\n"
                     f"    halo: {ref(ctx.c(f'{k}/Halo', 'halo'))}\n"
                     f"    upgradeButton: {ref(ctx.c(f'{k}/Buy', 'button'))}\n"
                     f"    upgradeButtonText: {ref(ctx.c(f'{k}/Buy/Label', 'tmp'))}\n")
        return K.mono(go, UPGRADES_SCRIPT,
                      "  rows:\n" + rows + f"  headerLineText: {ref(ctx.c('Tabs/Upgrades/Line', 'tmp'))}\n"
                      f"  lockupStyle: {K.ref(11400000, LOCKUP_STYLE, 2)}\n"
                      "  crystalsPerLevel: 100\n"
                      "  pipOff: {r: 0.13, g: 0.15, b: 0.27, a: 1}\n  pipFlashSeconds: 0.45\n  morphSeconds: 0.5\n"
                      "  elementNames:\n  - Charge\n  - Mass\n  - Space\n  - Time\n"
                      "  defaultLine: Spend crystals to raise an element. The hull changes as it grows.\n"
                      "  noMorphLine: This hull keeps its shape at every level.\n"
                      "  titleTemplate: '{element} - {ability}'\n  levelTemplate: Level {level} of 10\n"
                      "  upgradeTemplate: L5 {upgrade}\n  openSlotTitle: Open slot\n"
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
        .add("mask", 114, lambda ctx, go: K.mono(go, RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n"))
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
    page = page_node(panel, "Pilots", False)
    page_header(page, "Tabs/Pilots", "PILOTS", "Each pilot leans the vessel toward one element.")
    hero = page.child(N("Tabs/Pilots/Hero", "Hero", R((0, 1), (1, 1), (0, -54), (0, 214), (0.5, 1))))
    hero.add("cg", 225, canvas_group(True))
    framed(hero, "Tabs/Pilots/Hero", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.8)
    hero.child(N("Tabs/Pilots/Hero/Frame", "Frame", TLr(16, 20, 174, 174))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_oct_fill"), preserve=True))
    hero.child(N("Tabs/Pilots/Hero/Portrait", "Portrait", TLr(20, 24, 166, 166))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True))
    label(hero.child(N("Tabs/Pilots/Hero/Eyebrow", "Eyebrow", R((0, 1), (1, 1), (100, -22), (-224, 16), (0.5, 1)))), "", "Label", "ControlActive")
    label(hero.child(N("Tabs/Pilots/Hero/Name", "Name", R((0, 1), (1, 1), (100, -42), (-224, 34), (0.5, 1)))), "", "H2", "TextLight")
    label(hero.child(N("Tabs/Pilots/Hero/Desc", "Desc", R((0, 1), (1, 1), (100, -82), (-224, 44), (0.5, 1)))), "", "BodySmall", "TextLight",
          valign="top", wrap=True)
    label(hero.child(N("Tabs/Pilots/Hero/Quote", "Quote", R((0, 1), (1, 1), (100, -130), (-224, 64), (0.5, 1)))), "", "BodySmall",
          "NeutralLightest", valign="top", wrap=True)
    label(page.child(N("Tabs/Pilots/Ai", "Ai", R((0, 1), (1, 1), (0, -280), (0, 20), (0.5, 1)))), "", "BodySmall", "NeutralLightest")
    cards = page.child(N("Tabs/Pilots/Cards", "Cards", R((0, 1), (1, 1), (0, -316), (0, 180), (0.5, 1))))
    cards.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 0, (1, 1), (1, 1)))
    for i in range(4):
        k = f"Tabs/Pilots/Card{i}"
        c = kit_row(cards, k, f"Card{i}", R(size=(120, 180)), 180)
        c.add("cg", 225, canvas_group(True))
        c.child(N(f"{k}/Portrait", "Portrait", R((0.5, 1), None, (0, -14), (96, 96), (0.5, 1)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True))
        label(c.child(N(f"{k}/Name", "Name", R((0, 0), (1, 0), (0, 44), (-8, 22), (0.5, 0)))), "", "Body", "TextLight", align="center")
        c.child(N(f"{k}/Glyph", "Glyph", R((0.5, 0), None, (0, 22), (16, 16), (0.5, 0)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), element_glyph("Charge"), preserve=True))
        c.child(N(f"{k}/Underline", "Underline", R((0, 0), (1, 0), (0, -4), (-16, 2), (0.5, 0)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 0.0)))
    label(page.child(N("Tabs/Pilots/Empty", "Empty", R((0, 1), (1, 1), (0, -60), (0, 40), (0.5, 1)))),
          "No pilots fly this vessel yet.", "BodySmall", "NeutralLightest").add("cg", 225, canvas_group(False))

    def body(ctx, go):
        cards_ = ""
        for i in range(4):
            k = f"Tabs/Pilots/Card{i}"
            cards_ += (f"  - group: {ref(ctx.c(k, 'cg'))}\n    button: {ref(ctx.c(k, 'button'))}\n    portrait: {ref(ctx.c(f'{k}/Portrait', 'img'))}\n"
                       f"    nameText: {ref(ctx.c(f'{k}/Name', 'tmp'))}\n    elementGlyph: {ref(ctx.c(f'{k}/Glyph', 'img'))}\n"
                       f"    underline: {ref(ctx.c(f'{k}/Underline', 'img'))}\n")
        h = "Tabs/Pilots/Hero"
        return K.mono(go, PILOTS_SCRIPT,
                      f"  captains: {K.ref(11400000, CAPTAINS, 2)}\n  lockupStyle: {K.ref(11400000, LOCKUP_STYLE, 2)}\n"
                      f"  heroGroup: {ref(ctx.c(h, 'cg'))}\n  heroPortrait: {ref(ctx.c(f'{h}/Portrait', 'img'))}\n"
                      f"  heroFrame: {ref(ctx.c(f'{h}/Frame', 'img'))}\n  heroEyebrowText: {ref(ctx.c(f'{h}/Eyebrow', 'tmp'))}\n"
                      f"  heroNameText: {ref(ctx.c(f'{h}/Name', 'tmp'))}\n  heroDescriptionText: {ref(ctx.c(f'{h}/Desc', 'tmp'))}\n"
                      f"  heroQuoteText: {ref(ctx.c(f'{h}/Quote', 'tmp'))}\n  heroAiText: {ref(ctx.c('Tabs/Pilots/Ai', 'tmp'))}\n"
                      f"  emptyGroup: {ref(ctx.c('Tabs/Pilots/Empty', 'cg'))}\n  cards:\n{cards_}"
                      "  eyebrowTemplate: The {element} {vessel}\n  quoteTemplate: \"\\u201C{quote}\\u201D\"\n"
                      "  aiTemplate: 'As your wingmate: {behaviour}'\n"
                      "  elementNames:\n  - Charge\n  - Mass\n  - Space\n  - Time\n")
    page.add("logic", 114, body)
    return page


def artifacts_page(panel):
    page = page_node(panel, "Artifacts", False)
    page_header(page, "Tabs/Artifacts", "ARTIFACTS",
                "A real upgrade to a knob the elements do not reach. Carry one and you can lose it.")
    slot = page.child(N("Tabs/Artifacts/Slot", "Slot", R((0, 1), (1, 1), (0, -60), (0, 110), (0.5, 1))))
    framed(slot, "Tabs/Artifacts/Slot", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.65)
    slot.child(N("Tabs/Artifacts/Slot/Hex", "Hex", TLr(20, 27, 64, 56))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceLight", 0.4), K.kit_sprite("kit_hex_line2"), preserve=True))
    label(slot.child(N("Tabs/Artifacts/Slot/Title", "Title", R((0, 1), (1, 1), (52, -28), (-144, 26), (0.5, 1)))), "Undiscovered", "H3", "TextLight")
    label(slot.child(N("Tabs/Artifacts/Slot/Line", "Line", R((0, 1), (1, 1), (52, -58), (-144, 40), (0.5, 1)))),
          "Artifacts are found in the HyperSea. Fly far and bring one home.", "BodySmall", "NeutralLightest", valign="top", wrap=True)
    return page


def tab_header(panel):
    head = panel.child(N("Tabs/Header", "Header", R((0, 1), (1, 1), (0, -18), (-44, 46), (0.5, 1))))
    head.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 4, 4, (1, 1), (1, 1)))
    for name in TAB_NAMES:
        k = f"Tabs/Header/{name}"
        n = head.child(N(k, name, R(size=(100, 46))))
        ring = n.child(N(f"{k}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10))))
        ring.add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("TextLight", 0.0), K.kit_sprite("kit_sliver10_line2"), True)).add("le", 114, le(ignore=True))
        n.child(N(f"{k}/Hit", "Hit", STRETCH)).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
        lab = n.child(N(f"{k}/Label", "Label", STRETCH))
        lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(name.upper(), "ButtonSmall", tok("NeutralLightest"), "center")) \
            .add("kt", 114, K.kit_text("ButtonSmall", "NeutralLightest", driven=True))
        n.child(N(f"{k}/Underline", "Underline", R((0, 0), (1, 0), (0, 0), (0, 2), (0.5, 0)))).add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("ControlActive", 0.0)))
        n.add("tab", 114, (lambda kk: lambda ctx, go: K.mono(go, KIT_TEXT_TAB,
              K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c(f'{kk}/Hit', 'img'))}\n" + EMPTY_CALLS
              + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{kk}/FocusRing', 'img'))}\n"
              "  isSelected: 0\n"
              f"  label: {ref(ctx.c(f'{kk}/Label', 'tmp'))}\n  underline: {ref(ctx.c(f'{kk}/Underline', 'img'))}\n"))(k))
        n.add("audio", 114, lambda ctx, go: K.mono(go, MENU_AUDIO, "  category: 11\n"))


def build_tabs(panel):
    tab_header(panel)
    scan = panel.child(N("Tabs/Scan", "Scan", R((0, 1), (1, 1), (0, -70), (-8, 3), (0.5, 1))))
    scan.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive", 0.9), K.kit_sprite("kit_spark"))) \
        .add("cg", 225, lambda ctx, go: canvas_group(False)(ctx, go))
    pages = [abilities_page(panel), upgrades_page(panel), games_page(panel), pilots_page(panel), artifacts_page(panel)]
    label(panel.child(N("Tabs/Footer", "Footer", R((0, 0), (1, 0), (0, 18), (-44, 18), (0.5, 0)))),
          "LB / RB  switch tab", "Label", "NeutralLightest")

    def body(ctx, go):
        entries = ""
        for name in TAB_NAMES:
            logic = f"{{fileID: {ctx.c(f'Tabs/{name}', 'logic')}}}" if name != "Artifacts" else "{fileID: 0}"
            entries += (f"  - button: {ref(ctx.c(f'Tabs/Header/{name}', 'tab'))}\n    page: {ref(ctx.c(f'Tabs/{name}', 'cg'))}\n"
                        f"    pageLogic: {logic}\n")
        return K.mono(go, TABS_SCRIPT,
                      "  tabs:\n" + entries + "  swapSeconds: 0.32\n  travel: 60\n  skewDegrees: 6\n"
                      f"  scanLine: {ref(ctx.rt('Tabs/Scan'))}\n  scanGroup: {ref(ctx.c('Tabs/Scan', 'cg'))}\n"
                      "  scanTravel: 820\n  shoulderInput: 1\n")
    panel.add("tabs", 114, body)
    return pages


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
    glow_img(bay, "Hero/Clip/Bay/Deep", "Deep", R(size=(900, 620)), "ControlActive", 0.16)
    glow_img(bay, "Hero/Clip/Bay/RimL", "RimLeft", R((0, 0.5), None, (60, 40), (620, 640)), "SurfaceLight", 0.55)
    glow_img(bay, "Hero/Clip/Bay/RimR", "RimRight", R((1, 0.5), None, (-60, 60), (620, 640)), "ControlActive", 0.22)
    glow_img(bay, "Hero/Clip/Bay/Pool", "FloorPool", R((0.5, 0), None, (0, 96), (700, 130)), "ControlActive", 0.32)
    for i, (w, h) in enumerate(((640, 120), (480, 90), (330, 62))):
        ring = glow_img(bay, f"Hero/Clip/Bay/Ring{i}", f"Ring{i}", R((0.5, 0), None, (0, 96), (w, h)), "ControlActive", 0.5,
                        K.kit_sprite("kit_hex_line2"))
        ring.add("cg", 225, canvas_group(True)).add("amb", 114, M.ambient("Breathe", period=2.4, amplitude=0.25, phase=i * 0.33))
    scan = glow_img(bay, "Hero/Clip/Bay/Scan", "Scan", R((0, 0.5), (1, 0.5), (0, 0), (0, 3)), "ControlActive", 0.45,
                    K.kit_sprite("kit_spark"))
    scan.add("amb", 114, M.ambient("Float", period=7.0, amplitude=240))
    for i in range(8):
        x, y = (i * 113) % 760 - 380, (i * 71) % 420 - 160
        mote = glow_img(bay, f"Hero/Clip/Bay/Mote{i}", f"Mote{i}", R(pos=(x, y), size=(5, 5)), "TextLight", 0.8,
                        K.kit_sprite("kit_hex_fill"))
        mote.add("cg", 225, canvas_group(True)).add("amb", 114, M.ambient("Breathe", period=2.0 + (i % 3), amplitude=0.1, phase=i * 0.21))


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
    root = N("root", "HangarView", STRETCH)

    # ---- header ----
    header = root.child(N("Header", "Header", R((0, 1), (1, 1), (0, -108), (-80, 64), (0.5, 1))))
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    # A screen, not a modal: ScreenSwitcher replays these on every enter (KitEntrance.ReplayUnder).
    label(header.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (300, 18), (0, 1)))),
          "HANGAR", "Label", "NeutralLightest")
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))),
          "Select a vessel", "H1", "TextLight")
    bal = header.child(N("Header/Balance", "Balance", R((1, 0), None, (0, 0), (220, 40), (1, 0))))
    bal.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 5, (0, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))
    label(bal.child(N("Header/Balance/Label", "Label", R(size=(K.label_width("CRYSTALS", "Label"), K.line_height("Label"))))),
          "CRYSTALS", "Label", "NeutralLightest")
    bal.children.append(pill_instance("HeaderPill", "CrystalBalance"))

    # ---- three zones: the list is fixed; the centre and the column share the rest 62 : 38 ----
    body = root.child(N("Body", "Body", R((0, 0), (1, 1), (-2.5, -78), (-75, -236))))
    body.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 19, 0, (1, 1), (0, 1)))
    body.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", stagger=1))

    lz = body.child(N("List", "VesselList", R(size=(350, 844))))
    lz.add("le", 114, le(pw=350, fw=0))
    lh = lz.child(N("List/Header", "Header", R((0, 1), (1, 1), (0, 0), (-10, 18), (0.5, 1))))
    label(lh.child(N("List/Header/Label", "Label", STRETCH)), "VESSELS", "Label", "TextLight")
    label(lh.child(N("List/Header/Count", "OwnedCount", STRETCH)), "1 / 11 owned", "Label", "NeutralLightest", align="right")
    scroll = lz.child(N("List/Scroll", "Scroll", R((0, 0), (1, 1), (0, -13), (0, -26))))
    vp = scroll.child(N("List/Scroll/Viewport", "Viewport", STRETCH))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, lambda ctx, go: K.mono(go, RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n"
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

    # ---- rev 3 (2026-10-16): centre = the 3D vessel on its turntable + the old "how it plays" sliders;
    # right = five tabs (Abilities, Upgrades, Games, Pilots, Artifacts) with an animated swap ----
    cz = body.child(N("Selected", "SelectedVessel", R(size=(894, 844))))
    cz.add("le", 114, le(mw=0, pw=0, fw=62))
    hero = cz.child(N("Hero", "Hero", R((0, 1), (1, 1), (0, 0), (0, HERO_H), (0.5, 1))))
    clip = hero.child(N("Hero/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
    # Glass, not a slab: the hangar backdrop reads through around the model (20 % more opaque since
    # 2026-10-17, the owner: "make the bg opaque by 20%" - the lattice was fighting the text).
    clip.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.55), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("th", 114, themed("SurfaceVeryDark", 0.55)) \
        .add("mask", 114, lambda ctx, go: K.mono(go, MASK, "  m_ShowMaskGraphic: 1\n"))
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
             "  stickDegreesPerSecond: 160\n  idleResumeSeconds: 2.5\n  performSeconds: 0.55\n  performLunge: 1.4\n"
             "  performRollDegrees: 90\n  performLightIntensity: 6\n"
             f"  fx: {ref(ctx.c('Hero/Clip/Fx', 'fx'))}\n  pulseSeconds: 0.6\n  pulseScale: 0.12\n"
             "  renderRate: 30\n  resolution: 768\n"))
    stage_fx(clip)
    # TRY's window over the stage (HangarAbilityPreview): hidden until TRY, then the live picture of the
    # selected vessel flown in open water. The tap surface is navigation None - TRY already asked for
    # the stick, and the pad must not land on an invisible control.
    tw = clip.child(N("Hero/Clip/Try", "TryPreview", STRETCH))
    tw.add("cg", 225, canvas_group(False))
    tw.child(N("Hero/Clip/Try/Backing", "Backing", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack"))).add("th", 114, themed("SurfaceBlack"))
    tw.child(N("Hero/Clip/Try/Surface", "Surface", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("raw", 114, lambda ctx, go: K.mono(go, RAW_IMAGE,
             "  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 0\n"
             "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
             "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
             "  m_Texture: {fileID: 0}\n  m_UVRect:\n    serializedVersion: 2\n    x: 0\n    y: 0\n    width: 1\n    height: 1\n",
             enabled=False))
    st = tw.child(N("Hero/Clip/Try/Status", "Status", R(size=(600, 60))))
    st.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("LOADING...", "Label", tok("NeutralLightest"), align="center", wrap=True)) \
        .add("kt", 114, K.kit_text("Label", "NeutralLightest"))
    fc = tw.child(N("Hero/Clip/Try/Focus", "Focus", STRETCH))
    fc.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("button", 114, lambda ctx, go: K.mono(go, A_BUTTON,
             K.SELECTABLE_FIELDS.replace("    m_Mode: 3\n", "    m_Mode: 0\n").replace("  m_Transition: 1\n", "  m_Transition: 0\n")
             + f"  m_TargetGraphic: {ref(ctx.c('Hero/Clip/Try/Focus', 'img'))}\n" + EMPTY_CALLS))
    tw.add("window", 114, lambda ctx, go: K.mono(go, PREVIEW_WINDOW,
           f"  surface: {ref(ctx.c('Hero/Clip/Try/Surface', 'raw'))}\n"
           f"  statusLabel: {ref(ctx.c('Hero/Clip/Try/Status', 'tmp'))}\n"
           f"  focusButton: {ref(ctx.c('Hero/Clip/Try/Focus', 'button'))}\n"
           "  focusHint: {fileID: 0}\n  renderHeight: 360\n  maxRenderHeight: 720\n"
           "  doubleTapToFocus: 0\n  doubleTapSeconds: 0.4\n  guideTarget: {fileID: 0}\n"))
    clip.child(N("Hero/Clip/Fallback", "FallbackIcon", R(size=(256, 256)))) \
        .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), preserve=True)) \
        .add("cg", 225, canvas_group(False))
    clip.child(N("Hero/Clip/Fade", "Fade", R((0, 0), (1, 0), (0, 0), (0, 150), (0.5, 0)))) \
        .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.7), K.kit_sprite("kit_fade_v"))) \
        .add("th", 114, themed("SurfaceBlack", 0.7))
    hero.child(N("Hero/Border", "Border", STRETCH)).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True)).add("th", 114, themed("SurfaceDark"))
    chip = hero.child(N("Hero/Chip", "StatusChip", R((0, 1), None, (26, -24), (110, 30), (0, 1))))
    chip.add("hlg", 114, K.hv_layout(False, (12, 12, 5, 5), 8, 3, (1, 1), (0, 0))).add("csf", 114, K.fitter(2, 2))
    framed(chip, "Hero/Chip", 10, "SurfaceVeryDark", "SurfaceLight")
    chip.child(N("Hero/Chip/Lock", "Lock", R(size=(20, 20)))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("Purchase", 0.0), K.chrome_sprite("chrome_lock"), preserve=True)) \
        .add("th", 114, themed("Purchase", 0.0)).add("le", 114, le(ignore=True, pw=20, ph=20))
    label(chip.child(N("Hero/Chip/Label", "Label", R(size=(K.label_width("OWNED", "Label"), K.line_height("Label"))))),
          "Owned", "Label", "TextLight")
    label(hero.child(N("Hero/Turn", "TurnHint", R((1, 1), None, (-24, -28), (360, 18), (1, 1)))),
          "Drag or right stick to turn", "Label", "NeutralLightest", align="right")
    # While TRY runs: how to hand the vessel back, and STOP for touch (no START / Esc there) - which
    # also cancels a slow build. Navigation None: the pad's way back is START.
    th = hero.child(N("Hero/TryHint", "TryHint", R((0.5, 1), None, (0, -20), (560, 40), (0.5, 1))))
    th.add("cg", 225, canvas_group(False))
    th.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 16, 4, (0, 0), (0, 0)))
    tt = th.child(N("Hero/TryHint/Text", "Text", R((0, 0.5), None, (0, 0), (300, 18), (0, 0.5))))
    tt.add("le", 114, le(pw=300, ph=18))
    label(tt, "START or Esc to stop flying", "Label", "TextLight", align="right")
    stop_w = 16 + K.label_width("STOP", "ButtonSmall") + 16
    text_button(th, "Hero/TryHint/Stop", "StopButton", "STOP", R((0, 0.5), None, (0, 0), (stop_w, 40), (0, 0.5)),
                "Tabs/Abilities", "try", "CosmicShore.UI.HangarAbilityPreview", "End", nav=0) \
        .add("le", 114, le(pw=stop_w, ph=40))
    label(hero.child(N("Hero/Name", "Name", R((0, 0), None, (28, 64), (820, 62), (0, 0)))),
          "SQUIRREL", "Display", "TextLight", valign="bottom")
    label(hero.child(N("Description", "Description", R((0, 0), None, (30, 14), (820, 48), (0, 0)))),
          "Get in your flow and stay there.", "BodySmall", "NeutralLightest", valign="top", wrap=True)

    # The old Hangar's three sliders, in its own rail and thumb art.
    vz = cz.child(N("Vibe", "Vibe", R((0, 1), (1, 1), (0, -(HERO_H + 16)), (0, VIBE_H), (0.5, 1))))
    framed(vz, "Vibe", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.65)
    vz.child(N("Vibe/Icon", "Icon", TLr(24, 16, 24, 24))).add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("ControlActive"), sprite_guid("Assets/_Graphics/Hangar/Hanger_New/Frame 1085.png"), preserve=True))
    label(vz.child(N("Vibe/Title", "Title", TLr(56, 18, 200, 20))), "VIBE", "Label", "TextLight")
    label(vz.child(N("Vibe/Sub", "Sub", TRr(24, 20, 260, 16))), "how it plays", "Label", "NeutralLightest", align="right")
    for i in range(3):
        k = f"Vibe/Row{i}"
        row = vz.child(N(k, f"Row{i}", R((0, 1), (1, 1), (0, -(52 + i * 34)), (-48, 26), (0.5, 1))))
        label(row.child(N(f"{k}/Left", "Left", R((0, 0.5), None, (0, 0), (170, 26), (0, 0.5)))), "Casual", "Body", "TextLight", align="right")
        label(row.child(N(f"{k}/Right", "Right", R((1, 0.5), None, (0, 0), (170, 26), (1, 0.5)))), "Challenging", "Body", "TextLight")
        rail = row.child(N(f"{k}/Rail", "Rail", R((0, 0.5), (1, 0.5), (0, 0), (-388, 4))))
        rail.add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("NeutralLightest", 0.6), sprite_guid("Assets/_Graphics/Hangar/GameplayParameterSliderRail.png")))
        rail.child(N(f"{k}/Fill", "Fill", R((0, 0), (0.5, 1), (0, 0), (0, 0), (0, 0.5)))) \
            .add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive", 0.85))).add("th", 114, themed("ControlActive", 0.85))
        rail.child(N(f"{k}/Thumb", "Thumb", R((0.5, 0.5), None, (0, 0), (18, 23)))) \
            .add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), sprite_guid("Assets/_Graphics/Hangar/GameplayParameterSliderThumb.png"), preserve=True))
    vz.add("vibe", 114, lambda ctx, go: K.mono(go, VIBE_SCRIPT, "  sliders:\n" + "".join(
        f"  - leftText: {ref(ctx.c(f'Vibe/Row{i}/Left', 'tmp'))}\n    rightText: {ref(ctx.c(f'Vibe/Row{i}/Right', 'tmp'))}\n"
        f"    thumb: {ref(ctx.rt(f'Vibe/Row{i}/Thumb'))}\n    fill: {ref(ctx.rt(f'Vibe/Row{i}/Fill'))}\n" for i in range(3))))

    slot = cz.child(N("Unlock", "Unlock", R((0, 0), None, (5, 6), (340, 60), (0, 0))))
    locked = slot.child(N("Unlock/Locked", "Locked", STRETCH)).add("cg", 225, canvas_group(False))
    locked.children.append(button_instance("UnlockButton", "UnlockButton", "Purchase", False, "UNLOCK - 4000", icon=True))
    owned = slot.child(N("Unlock/Owned", "Owned", STRETCH)).add("cg", 225, canvas_group(True))
    owned.children.append(button_instance("UnlockedButton", "UnlockedButton", "Inactive", False, "UNLOCKED", disabled=True))

    rz = body.child(N("Abilities", "Tabs", R(size=(548, 844))))
    rz.add("le", 114, le(mw=0, pw=0, fw=38))
    panel = rz.child(N("Abilities/Panel", "Panel", R((0, 0), (1, 1), (2.5, 0), (-5, 0))))
    framed(panel, "Abilities/Panel", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.8)
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
                      f"  vesselNameText: {ref(c('Hero/Name', 'tmp'))}\n"
                      f"  vesselDescriptionText: {ref(c('Description', 'tmp'))}\n"
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
                      "  lockedWord: Locked\n  ownedWord: Owned\n  unlockTemplate: UNLOCK - {cost}\n"
                      "  unlockFreeWord: UNLOCK - FREE\n  spendTemplate: '{cost} to unlock {vessel}'\n"
                      "  confirmTemplate: CONFIRM - {cost}\n  shortfallTemplate: You need {amount} more crystals.\n")
    root.add("detail", 114, detail_body)
    return root


# The detail view's fields, each with the class / script its target must be (validation).
DETAIL_FIELDS = {
    "vesselNameText": K.TMP_UGUI, "vesselDescriptionText": K.TMP_UGUI, "stage": STAGE_SCRIPT, "tabs": TABS_SCRIPT,
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
    out[TRY_ASSET] = try_definition()
    out[TRY_ASSET + ".meta"] = ("fileFormatVersion: 2\nguid: " + asset_guid("HangarTryPreview") +
                                "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
                                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    out[TRY_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/SOHangar"))
    return out, tile_comp


def try_definition():
    """TRY's bare arena: the Barren cell, the vessel the Hangar hands in (Vessel -1 = Any), no
    objective, no clock (DurationSeconds 0), spawned on the cell's ring. Mode 0 (Random) is the
    session's idle mode: no card, no gate course, no first-login guide step points at it."""
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {PREVIEW_DEF_SCRIPT}, type: 3}}\n"
            "  m_Name: HangarTryPreview\n  m_EditorClassIdentifier: \n  Mode: 0\n"
            "  Notes: 'The Hangar''s TRY (author_menu_hangar.py): the selected vessel in open water on the\n"
            "    Barren cell, so its abilities can be fired for real and the drill can coach them. Not\n"
            "    an arcade card.'\n"
            f"  PreviewCell: {{fileID: 11400000, guid: {BARREN_CELL}, type: 2}}\n"
            "  PreviewCellsByIntensity: []\n  StructurePrefab: {fileID: 0}\n  TrackSpawnablesByIntensity: []\n"
            "  CourseStructurePrefab: {fileID: 0}\n  PreviewFauna: {fileID: 0}\n  PreviewFaunaCount: 4\n"
            "  Vessel: -1\n  ObjectiveText: \n  ObjectiveMetric: 0\n  ObjectiveTarget: 0\n  DurationSeconds: 0\n"
            "  SpawnFromCellRing: 1\n  SpawnDistanceOutsideNucleus: 70\n  SpawnRingRadiusFloor: 0\n"
            "  SpawnRingRadiusFloorByIntensity: []\n  SpawnFormation: 0\n  SpawnPoints: []\n")


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
    for name in ("KitButton", "KitTile", "KitDivider", "KitCurrencyPill"):
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
    detail = K.FileIds("HangarView")("root#detail")
    _, body = doc_of(view, detail)
    for field, want in DETAIL_FIELDS.items():
        m = re.search(r"^  " + field + r": \{fileID: (-?\d+)\}", body, re.M)
        assert m and m.group(1) != "0", f"HangarVesselDetailView.{field} is not wired"
        cls, script = target_kind(view, m.group(1), files)
        ok = cls == want if isinstance(want, int) else script == want
        assert ok, f"HangarVesselDetailView.{field} points at the wrong kind of object ({cls}, {script})"
    assert "abilityRowPrefab" not in body, "HangarVesselDetailView still names the retired ability-row prefab"
    # Rev 3: five tabs, each wired to its button and page; four of them carry a page component.
    hv = K.FileIds("HangarView")
    _, tabs = doc_of(view, hv("Abilities/Panel#tabs"))
    assert tabs and tabs.count("  - button: {fileID: ") == len(TAB_NAMES), "HangarTabsView must name five tabs"
    for name in TAB_NAMES:
        assert f"button: {{fileID: {hv(f'Tabs/Header/{name}#tab')}}}" in tabs, f"tab {name}: button unwired"
        assert f"page: {{fileID: {hv(f'Tabs/{name}#cg')}}}" in tabs, f"tab {name}: page unwired"
        if name != "Artifacts":
            assert f"pageLogic: {{fileID: {hv(f'Tabs/{name}#logic')}}}" in tabs, f"tab {name}: page logic unwired"
    _, stage = doc_of(view, hv("Hero/Clip/Stage#stage"))
    assert stage and f"prefabs: {{fileID: 11400000, guid: {VESSEL_PREFABS}" in stage, "the stage has no vessel prefab table - it would build nothing"
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
    for name, _ in ELEMENTS:
        k = f"Tabs/Upgrades/{name}"
        assert f"    upgradeButton: {{fileID: {hv(f'{k}/Buy#button')}}}" in upg, f"{name}: UPGRADE button unwired"
        assert f"    upgradeButtonText: {{fileID: {hv(f'{k}/Buy/Label#tmp')}}}" in upg, f"{name}: UPGRADE label unwired"
    _, abil = doc_of(view, hv("Tabs/Abilities#logic"))
    assert abil and [int(x) for x in re.findall(r"  - element: (\d+)", abil)] == [i for _, i in ELEMENTS], \
        "the Abilities rows must be Charge, Mass, Space, Time"
    # TRY (T-20261016-1ec1): the page forwards to it, it reaches its window, group, hint and bare arena,
    # TRY / STOP call it, and the hidden tap surface never takes the pad's focus.
    assert f"  tryPreview: {{fileID: {hv('Tabs/Abilities#try')}}}" in abil, "the Abilities page does not forward to TRY"
    _, trp = doc_of(view, hv("Tabs/Abilities#try"))
    for field, want in (("window", hv("Hero/Clip/Try#window")), ("windowGroup", hv("Hero/Clip/Try#cg")),
                        ("flyingHint", hv("Hero/TryHint#cg"))):
        assert f"  {field}: {{fileID: {want}}}" in trp, f"HangarAbilityPreview.{field} is not wired"
    assert f"definition: {{fileID: 11400000, guid: {asset_guid('HangarTryPreview')}" in trp, "TRY has no bare-arena definition"
    for key, method in (("Tabs/Abilities/Try", "Try"), ("Hero/TryHint/Stop", "End")):
        _, btn = doc_of(view, hv(f"{key}#button"))
        assert f"m_Target: {{fileID: {hv('Tabs/Abilities#try')}}}" in btn and f"m_MethodName: {method}\n" in btn, \
            f"{key.split('/')[-1]} must call HangarAbilityPreview.{method}"
    _, focus = doc_of(view, hv("Hero/Clip/Try/Focus#button"))
    assert "m_Navigation:\n    m_Mode: 0\n" in focus, "TRY's hidden tap surface must be navigation None"
    tdef = files[TRY_ASSET]
    assert f"PreviewCell: {{fileID: 11400000, guid: {BARREN_CELL}" in tdef and "\n  Mode: 0\n" in tdef \
        and "\n  DurationSeconds: 0\n" in tdef and "\n  ObjectiveTarget: 0\n" in tdef, \
        "TRY's definition must be the Barren cell, Mode 0, no clock and no objective"
    # A label stretched across its row with a LEFT pivot starts at its anchoredPosition.x: a negative
    # one starts outside the row (2026-10-17: every ability name and line sat under the icon, x -38).
    for m in re.finditer(r"^--- !u!224 &(-?\d+)\n((?:(?!--- ).*\n)*)", view, re.M):
        d = m.group(2)
        if ("m_AnchorMin: {x: 0," in d and "m_AnchorMax: {x: 1," in d and "m_Pivot: {x: 0," in d):
            x = float(re.search(r"m_AnchoredPosition: \{x: (-?[\d.e-]+)", d).group(1))
            assert x >= 0, f"RectTransform {m.group(1)} is stretched with a left pivot and starts {x:g} px left of its parent"
    _, vibe = doc_of(view, hv("Vibe#vibe"))
    assert vibe and vibe.count("    thumb: {fileID: ") == 3, "the vibe view needs its three sliders"
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

    _, dbody = doc_of(files[view_path], detail)
    name_ref = re.search(r"vesselNameText: \{fileID: (-?\d+)\}", dbody).group(1)
    desc_ref = re.search(r"vesselDescriptionText: \{fileID: (-?\d+)\}", dbody).group(1)
    plants = {
        "the screen's entrances stripped": view_plant(M.strip_entrances),
        "detail field swapped onto the wrong kind": view_plant(lambda t: t.replace(
            f"chipLabel: {{fileID:", f"chipLabel: {{fileID: {name_ref}}}\n  chipLabelX: {{fileID:", 1)),
        "detail field unwired": view_plant(lambda t: t.replace(
            f"vesselDescriptionText: {{fileID: {desc_ref}}}", "vesselDescriptionText: {fileID: 0}", 1)),
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
            r"(m_AnchorMin: \{x: 0, y: 1\}\n  m_AnchorMax: \{x: 1, y: 1\}\n  m_AnchoredPosition: \{x: )86(, y: -14\})", r"\g<1>-38\2", t, count=1)),
        "TRY calling nothing": view_plant(lambda t: t.replace("m_MethodName: Try\n", "m_MethodName: Hide\n", 1)),
        "the Abilities page not forwarding to TRY": view_plant(lambda t: re.sub(
            r"(  tryPreview: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "the stage not reaching its effects": view_plant(lambda t: re.sub(
            r"(\n  fx: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "the bay batching on the screen's canvas": view_plant(lambda t: t.replace(
            f"&{K.FileIds('HangarView')('Hero/Clip/Bay#canvas')}\nCanvas:", f"&{K.FileIds('HangarView')('Hero/Clip/Bay#canvas')}\nCanvasGroup:", 1)),
        "an UPGRADE button unwired": view_plant(lambda t: re.sub(
            r"(    upgradeButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
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
