#!/usr/bin/env python3
"""
Author the STYLE-B COMPONENT KIT prefabs, its type-role asset and its test scene
(Docs/MENU_REDESIGN_B.md, item 0 - the approved `MenuKit.dc.html` board on the mockup canvas).

    Assets/_Prefabs/UI Elements/Kit/
        KitButton.prefab          §10.1 button: slivered fill + border + caps label (+ optional icon)
        KitButtonSmall.prefab     a PREFAB VARIANT of KitButton - the 14pt Button-small size
        KitTile.prefab            the style-B left-list tile / §10.13 class card (KitTile)
        KitHexNavTile.prefab      the §10.5 / §10.12 hexagon nav tile (KitTile on hex sprites)
        KitTopNavTab.prefab       one top-nav tab: InactiveImage / ActiveImage children, the shape
                                  ScreenSwitcher.UpdateNavBar already toggles
        KitKeycap.prefab          the LT / RT keycap
        KitPanel.prefab           §5 slivered panel (Very Dark fill, Neutral Dark hairline)
        KitPopup.prefab           §10.3 popup: buttons straddle the bottom border, confirm left
        KitChip.prefab            keyword chip
        KitSectionLabel.prefab    caps label (the Label role)
        KitDivider.prefab         hairline section divider
        KitCurrencyPill.prefab    §10.4 currency bar (single currency)
    Assets/_SO_Assets/UI/UITypeRoles.asset           the §4 role table KitText reads
    Assets/_Scenes/Game_TestDesign/UIKitTestScene.unity
        every kit sprite at three widths (T7) and every component in its states; not in Build
        Settings

Nothing here touches a shipping prefab or scene - T7's "no shipping prefab touched" criterion.

HOW IT IS BUILT. Every document is generated from a small node model with deterministic guids
and fileIDs (md5 of a stable key, so `--check` compares content, not identity). Component bodies
are cloned from Unity's own serialization of each type (harvested from shipped prefabs - see the
guid table), and every colour is computed here from the same §11 tokens and the same palette rules
as KitButton / KitTile / KitText, so Unity finds nothing to re-serialize when the kit components
apply themselves in edit mode. Before anything is written: every local fileID resolves, no fileID
repeats inside a file, every guid this script mints is owned by exactly one .meta, and every
referenced guid resolves to a .meta in the repo (or is a known package script).

    python3 Tools/Build/author_ui_kit_prefabs.py           # write
    python3 Tools/Build/author_ui_kit_prefabs.py --check   # fail if any output drifted
    python3 Tools/Build/author_ui_kit_prefabs.py --self-test  # planted defects must all be caught
"""

import argparse
import hashlib
import os
import re
import struct
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PREFAB_DIR = "Assets/_Prefabs/UI Elements/Kit"
ROLES_ASSET = "Assets/_SO_Assets/UI/UITypeRoles.asset"
SCENE = "Assets/_Scenes/Game_TestDesign/UIKitTestScene.unity"
INT64_MAX = 9223372036854775807

# --- package script guids, harvested from shipped assets (never recalled) -------------------
IMAGE = "fe87c0e1cc204ed48ad3b37840f39efc"
TMP_UGUI = "f4688fdb7df04437aeb418b961361dc5"
HLAYOUT = "30649d3a9faa99c48a7b1166b86bf2a0"      # children vary in X (asset-surgery skill)
VLAYOUT = "59f8146938fff824cb5fd77236b75775"
LAYOUT_ELEMENT = "306cc8c2b49d7114eaa3623786fc2126"
FITTER = "3245ec927659c4140ac4f8d17403cc18"
CANVAS_SCALER = "0cd44c1031e13a943bb63640046fad76"
GRAPHIC_RAYCASTER = "dc42784cf147c0c48a680349fa168899"
EVENT_SYSTEM = "76c392e42b5098c458856cdf6ecaaaa1"
INPUT_MODULE = "01614664b831546d2ae94a42149d80ac"
INPUT_ACTIONS = "ca9f5fa95ffab41fb9a615ab714db018"      # DefaultInputActions, as Menu_Main wires it
TMP_FONT_ASSET_SCRIPT = "71c1514a6bd24e1e882cebbe1904ce04"
# Unity's built-in resource bundles, referenced by the copied scene-settings documents.
BUILTIN_GUIDS = {"0000000000000000e000000000000000", "0000000000000000f000000000000000"}
PACKAGE_GUIDS = {IMAGE, TMP_UGUI, HLAYOUT, VLAYOUT, LAYOUT_ELEMENT, FITTER, CANVAS_SCALER,
                 GRAPHIC_RAYCASTER, EVENT_SYSTEM, INPUT_MODULE, INPUT_ACTIONS} | BUILTIN_GUIDS

THEME = "0aef6398681c4ec98118cc9b9740060a"                 # _SO_Assets/UI/UITheme.asset
ALDRICH = ("6ab8eca0e6e2b7c4a8a495d9afae2053", 3995749905058258831)


def meta_guid(path):
    with open(os.path.join(REPO, path + ".meta")) as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    assert m, f"no guid in {path}.meta"
    return m.group(1)


def kit_script(name):
    return meta_guid(f"Assets/_Scripts/UI/Kit/{name}.cs")


def kit_sprite(name):
    return meta_guid(f"Assets/_Graphics/UI/Kit/{name}.png")


def chrome_sprite(name):
    return meta_guid(f"Assets/_Graphics/UI/Chrome/{name}.png")


SEMIBOLD = (meta_guid("Assets/_Graphics/Fonts/ChakraPetch/ChakraPetch-SemiBold SDF.asset"),
            4519786210045101501)
VESSEL_ICON = {"Squirrel": "5d8f022f56d65ef4fae2cea4428a990f",   # SO_Class_*.IconActive
               "Manta": "3cf78c88592fff3419945dc7d6a4889a",
               "Dolphin": "c07463db6e8244c45b639760efd18f9e"}


def guid_for(name):
    return hashlib.md5(f"CosmicShore/UIKit/assets/{name}".encode()).hexdigest()


# --------------------------------------------------------------------------------------------
# Tokens and palettes - mirrors of UITheme.cs, KitButton.cs, KitTile.cs (keep in step)
# --------------------------------------------------------------------------------------------

def hexc(h, a=1.0):
    return (int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a)


TOK = {
    "TextLight": "E6E9FF", "TextInactive": "25262D", "InactiveLight": "5C5F70",
    "SurfaceBlack": "00010A", "SurfaceVeryDark": "00041F", "SurfaceDark": "222645",
    "SurfaceLight": "434C89", "NeutralLightest": "747BAD", "Cta": "99FF80", "Danger": "FF4B3A",
    "ControlActive": "00D4FF", "Purchase": "FFAE00", "Tooltip": "A600FF",
    # Rev 6: the elements as the colours of meaning (AbilityLockupStyleSO's element colours).
    "ElementCharge": "FFD34D", "ElementMass": "7FA8FF", "ElementSpace": "B07CFF", "ElementTime": "5FF0C8",
    # Rev 7: whatever is selected or focused (UITheme.Selection).
    "Selection": "3DB2FF",
}
TOKEN_INDEX = {"TextLight": 0, "TextInactive": 1, "InactiveLight": 2, "SurfaceBlack": 3,
               "SurfaceVeryDark": 4, "SurfaceDark": 5, "SurfaceLight": 6, "NeutralLightest": 7,
               "Cta": 8, "Danger": 9, "ControlActive": 10, "Purchase": 11, "Tooltip": 12,
               "ElementCharge": 13, "ElementMass": 14, "ElementSpace": 15, "ElementTime": 16, "Selection": 17}


def tok(name, alpha=1.0):
    return hexc(TOK[name], alpha)


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


WHITE = (1.0, 1.0, 1.0, 1.0)
VARIANTS = {"Default": 0, "Inactive": 1, "Cancel": 2, "Purchase": 3, "Cta": 4, "Play": 5, "Danger": 6,
            "Mass": 7, "Space": 8, "Time": 9}
ELEMENT_VARIANT = {"Purchase": "ElementMass", "Mass": "ElementMass", "Space": "ElementSpace", "Time": "ElementTime"}


def button_palette(variant, opaque, disabled=False):
    """(border, fill, content) in the Normal state - KitButton.PaletteFor."""
    v = "Inactive" if disabled else variant
    opaque = opaque or v == "Danger"
    dark = tok("SurfaceBlack")
    if v == "Inactive":
        return ((tok("InactiveLight"), tok("InactiveLight"), tok("TextInactive")) if opaque else
                (tok("InactiveLight"), tok("InactiveLight", 0.12), tok("InactiveLight")))
    if v == "Cancel":
        return ((tok("NeutralLightest"), tok("NeutralLightest"), dark) if opaque else
                (tok("NeutralLightest"), tok("NeutralLightest", 0.12), tok("TextLight")))
    if v in ELEMENT_VARIANT:   # rev 6: a spend is Mass; people Space; progress Time
        t = ELEMENT_VARIANT[v]
        return ((tok(t), tok(t), dark) if opaque else (tok(t), tok(t, 0.12), tok(t)))
    if v == "Cta":
        return ((tok("Cta"), tok("Cta"), dark) if opaque else
                (tok("Cta"), tok("Cta", 0.12), tok("Cta")))
    if v == "Play":            # rev 6: Play is Charge
        charge = tok("ElementCharge")
        return ((lerp(charge, WHITE, 0.5), charge, dark) if opaque else
                (charge, tok("ElementCharge", 0.24), charge))
    if v == "Danger":
        return (tok("Danger"), tok("Danger"), dark)
    return ((tok("TextLight"), tok("TextLight"), dark) if opaque else
            (tok("ControlActive"), tok("ControlActive", 0.12), tok("TextLight")))


SELECTED_FILL_TINT = 0.22   # KitTile.SelectedFillTint


def selected_fill():
    """KitTile's selected fill: SurfaceVeryDark tinted toward the Selection blue, at SurfaceVeryDark's alpha."""
    a, b = tok("SurfaceVeryDark"), tok("Selection")
    return tuple(a[i] + (b[i] - a[i]) * SELECTED_FILL_TINT for i in range(3)) + (a[3],)


def tile_palette(selected, locked, unselected_primary="TextLight", locked_secondary="Purchase"):
    """KitTile.CollectColors in the Normal state."""
    return dict(
        fill=selected_fill() if selected else tok("SurfaceVeryDark"),
        border=tok("Selection" if selected else "SurfaceDark"),
        primary=tok("NeutralLightest" if locked else "TextLight" if selected else unselected_primary),
        secondary=tok(locked_secondary if locked else "TextLight" if selected else "NeutralLightest"),
        art=(1.0, 1.0, 1.0, 0.35 if locked else 1.0),
        lock=tok("TextLight", 1.0 if locked else 0.0),
    )


# §4 roles - mirrors UITypeRolesSO.Defaults()
ROLES = {  # role: (index, face, size, spacing, upper, tabular)
    "Display": (0, "Aldrich", 48, 0, False, False), "H1": (1, "Aldrich", 36, 0, False, False),
    "H2": (2, "Aldrich", 28, 0, False, False), "H3": (3, "Aldrich", 22, 0, False, False),
    "Body": (4, "Aldrich", 18, 0, False, False), "BodySmall": (5, "Aldrich", 15, 0, False, False),
    "Button": (6, "Chakra", 18, 8, True, True), "ButtonSmall": (7, "Chakra", 14, 8, True, True),
    "Label": (8, "Chakra", 14, 14, True, True), "DataLarge": (9, "Aldrich", 44, 0, False, True),
    "Data": (10, "Aldrich", 20, 0, False, True), "DataSmall": (11, "Aldrich", 15, 0, False, True),
}

# --------------------------------------------------------------------------------------------
# Serialization helpers
# --------------------------------------------------------------------------------------------


def f32(x):
    """Unity's float spelling: float32 shortest round-trip, integers bare."""
    v = struct.unpack("<f", struct.pack("<f", float(x)))[0]
    if v == int(v) and abs(v) < 1e15:
        return str(int(v))
    for p in range(6, 12):
        s = f"{v:.{p}g}"
        if struct.unpack("<f", struct.pack("<f", float(s)))[0] == v:
            return s
    return repr(v)


def col(c):
    return "{r: %s, g: %s, b: %s, a: %s}" % tuple(f32(x) for x in c)


def col32(c):
    r, g, b, a = (max(0, min(255, int(round(x * 255)))) for x in c)
    return r | (g << 8) | (b << 16) | (a << 24)


def v2(x, y):
    return "{x: %s, y: %s}" % (f32(x), f32(y))


def ref(fid=0, guid=None, kind=3):
    if guid is None:
        return "{fileID: %d}" % fid
    return "{fileID: %d, guid: %s, type: %d}" % (fid, guid, kind)


def sprite_ref(guid):
    return ref(21300000, guid, 3) if guid else ref(0)


class FileIds:
    def __init__(self, file_key):
        self.file_key = file_key
        self.used = {}

    def __call__(self, key):
        h = int(hashlib.md5(f"{self.file_key}/{key}".encode()).hexdigest()[:15], 16) + 1_000_000
        assert h <= INT64_MAX
        prev = self.used.get(h)
        assert prev in (None, key), f"fileID collision in {self.file_key}: {prev} / {key}"
        self.used[h] = key
        return h


def source_fid(prefab_key, key):
    """The fileID of `key` inside a kit prefab - what an instance's overrides target. A VARIANT's
    objects are not authored in its file: Unity derives each one as base fileID XOR the variant's
    own PrefabInstance fileID (asset-surgery skill §3), so that is what an override must name."""
    if prefab_key == "KitButtonSmall":
        return FileIds("KitButton")(key) ^ FileIds("KitButtonSmall")("variant#inst")
    return FileIds(prefab_key)(key)


# --------------------------------------------------------------------------------------------
# Node model
# --------------------------------------------------------------------------------------------

def rect(amin=(0.5, 0.5), amax=None, pos=(0, 0), size=(100, 100), pivot=(0.5, 0.5)):
    return dict(amin=amin, amax=amax if amax is not None else amin, pos=pos, size=size, pivot=pivot)


STRETCH = rect((0, 0), (1, 1), (0, 0), (0, 0))


class Node:
    def __init__(self, key, name, r=None, active=True):
        self.key, self.name, self.rect, self.active = key, name, r or rect(), active
        self.comps = []      # (comp_key, class_id, body(ctx) -> str)
        self.children = []   # Node | Instance

    def add(self, comp_key, class_id, body):
        self.comps.append((comp_key, class_id, body))
        return self

    def child(self, node):
        self.children.append(node)
        return node


class Instance:
    """A nested prefab instance: PrefabInstance + stripped root RectTransform."""

    def __init__(self, key, prefab_key, prefab_guid, name, r, mods=(), active=True):
        self.key, self.prefab_key, self.prefab_guid = key, prefab_key, prefab_guid
        self.name, self.rect, self.mods, self.active = name, r, list(mods), active


class Ctx:
    def __init__(self, file_key):
        self.fid = FileIds(file_key)

    def go(self, node_key):
        return self.fid(node_key + "#go")

    def rt(self, node_key):
        return self.fid(node_key + "#rt")

    def c(self, node_key, comp_key):
        return self.fid(node_key + "#" + comp_key)


HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


def emit_node(ctx, node, parent_rt, out):
    go, rt = ctx.go(node.key), ctx.rt(node.key)
    comp_ids = [rt] + [ctx.c(node.key, k) for k, _, _ in node.comps]
    out.append(f"--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n"
               "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
               "  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n"
               + "".join(f"  - component: {{fileID: {c}}}\n" for c in comp_ids)
               + f"  m_Layer: 5\n  m_Name: {node.name}\n  m_TagString: Untagged\n"
               "  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n"
               f"  m_IsActive: {1 if node.active else 0}\n")
    child_rts = []
    for ch in node.children:
        child_rts.append(ctx.rt(ch.key) if isinstance(ch, Node) else ctx.fid(ch.key + "#stub"))
    r = node.rect
    children = ("  m_Children:\n" + "".join(f"  - {{fileID: {c}}}\n" for c in child_rts)
                if child_rts else "  m_Children: []\n")
    out.append(f"--- !u!224 &{rt}\nRectTransform:\n  m_ObjectHideFlags: 0\n"
               "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
               f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
               "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
               "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n"
               + children
               + f"  m_Father: {{fileID: {parent_rt}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
               f"  m_AnchorMin: {v2(*r['amin'])}\n  m_AnchorMax: {v2(*r['amax'])}\n"
               f"  m_AnchoredPosition: {v2(*r['pos'])}\n  m_SizeDelta: {v2(*r['size'])}\n"
               f"  m_Pivot: {v2(*r['pivot'])}\n")
    for k, class_id, body in node.comps:
        out.append(f"--- !u!{class_id} &{ctx.c(node.key, k)}\n" + body(ctx, go))
    for ch in node.children:
        if isinstance(ch, Node):
            emit_node(ctx, ch, rt, out)
        else:
            emit_instance(ctx, ch, rt, out)


def emit_instance(ctx, inst, parent_rt, out):
    pid, stub = ctx.fid(inst.key + "#inst"), ctx.fid(inst.key + "#stub")
    root_rt = source_fid(inst.prefab_key, "root#rt")
    root_go = source_fid(inst.prefab_key, "root#go")
    r = inst.rect
    base = [
        (root_go, "m_Name", inst.name, None),
        (root_go, "m_IsActive", 1 if inst.active else 0, None),
        (root_rt, "m_Pivot.x", r["pivot"][0], None), (root_rt, "m_Pivot.y", r["pivot"][1], None),
        (root_rt, "m_AnchorMax.x", r["amax"][0], None), (root_rt, "m_AnchorMax.y", r["amax"][1], None),
        (root_rt, "m_AnchorMin.x", r["amin"][0], None), (root_rt, "m_AnchorMin.y", r["amin"][1], None),
        (root_rt, "m_SizeDelta.x", r["size"][0], None), (root_rt, "m_SizeDelta.y", r["size"][1], None),
        (root_rt, "m_LocalPosition.x", 0, None), (root_rt, "m_LocalPosition.y", 0, None),
        (root_rt, "m_LocalPosition.z", 0, None),
        (root_rt, "m_LocalRotation.w", 1, None), (root_rt, "m_LocalRotation.x", 0, None),
        (root_rt, "m_LocalRotation.y", 0, None), (root_rt, "m_LocalRotation.z", 0, None),
        (root_rt, "m_AnchoredPosition.x", r["pos"][0], None),
        (root_rt, "m_AnchoredPosition.y", r["pos"][1], None),
        (root_rt, "m_LocalEulerAnglesHint.x", 0, None), (root_rt, "m_LocalEulerAnglesHint.y", 0, None),
        (root_rt, "m_LocalEulerAnglesHint.z", 0, None),
    ]
    mods = base + [(source_fid(inst.prefab_key, k), p, v, o) for k, p, v, o in inst.mods]
    lines = []
    for target, path, value, obj in mods:
        if isinstance(value, float):
            value = f32(value)
        lines.append(f"    - target: {ref(target, inst.prefab_guid)}\n      propertyPath: {path}\n"
                     f"      value: {'' if value is None else yaml_scalar(value)}\n"
                     f"      objectReference: {obj if obj else ref(0)}\n")
    out.append(f"--- !u!1001 &{pid}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
               "  m_Modification:\n    serializedVersion: 3\n"
               f"    m_TransformParent: {{fileID: {parent_rt}}}\n    m_Modifications:\n" + "".join(lines)
               + "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n"
               "    m_AddedComponents: []\n"
               f"  m_SourcePrefab: {ref(100100000, inst.prefab_guid)}\n")
    out.append(f"--- !u!224 &{stub} stripped\nRectTransform:\n"
               f"  m_CorrespondingSourceObject: {ref(root_rt, inst.prefab_guid)}\n"
               f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


# --------------------------------------------------------------------------------------------
# Component bodies (cloned from Unity's own serialization of each type)
# --------------------------------------------------------------------------------------------

def yaml_scalar(value):
    """A modification value as Unity writes it: a string with ": " (or a leading YAML indicator) is single-quoted,
    else the YAML does not parse ("SHUFFLE: ON" broke a prefab, 2026-10-18)."""
    v = str(value)
    if isinstance(value, str) and (": " in v or v.endswith(":") or " #" in v or v.startswith(("- ", "? ")) or v in ("-", "?")
                                   or (v and v[0] in "!&*{}[]#|>@`'\"%")):
        return "'" + v.replace("'", "''") + "'"
    return v


def mono(go, guid, fields, enabled=True):
    return ("MonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: {1 if enabled else 0}\n"
            f"  m_EditorHideFlags: 0\n  m_Script: {ref(11500000, guid)}\n  m_Name: \n"
            "  m_EditorClassIdentifier: \n" + fields)


def canvas_renderer():
    return lambda ctx, go: ("CanvasRenderer:\n  m_ObjectHideFlags: 0\n"
                            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
                            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
                            "  m_CullTransparentMesh: 1\n")


def image(color, sprite=None, sliced=False, raycast=False, preserve=False):
    def body(ctx, go):
        return mono(go, IMAGE,
                    "  m_Material: {fileID: 0}\n"
                    f"  m_Color: {col(color)}\n  m_RaycastTarget: {1 if raycast else 0}\n"
                    "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
                    "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
                    f"  m_Sprite: {sprite_ref(sprite)}\n  m_Type: {1 if sliced else 0}\n"
                    f"  m_PreserveAspect: {1 if preserve else 0}\n  m_FillCenter: 1\n  m_FillMethod: 4\n"
                    "  m_FillAmount: 1\n  m_FillClockwise: 1\n  m_FillOrigin: 0\n  m_UseSpriteMesh: 0\n"
                    "  m_PixelsPerUnitMultiplier: 1\n")
    return body


FIT_MIN_SCALE = 0.6   # KitText.MinSizeScale: every kit label shrinks to fit its rect, never below 60 % of its role size


def fit_min(size):
    """The auto-size floor KitText.Apply writes (Mathf.Max(8, Mathf.Round(size * MinSizeScale)))."""
    return max(8, round(size * FIT_MIN_SCALE))


def tmp(text, role, color, align="center", raycast=False):
    _, face, size, spacing, upper, _ = ROLES[role]
    font = SEMIBOLD if face == "Chakra" else ALDRICH
    h = {"left": 1, "center": 2, "right": 4}[align]

    def body(ctx, go):
        return mono(go, TMP_UGUI,
                    "  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n"
                    f"  m_RaycastTarget: {1 if raycast else 0}\n"
                    "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
                    "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
                    f"  m_text: {yaml_scalar(text)}\n  m_isRightToLeft: 0\n"
                    f"  m_fontAsset: {ref(11400000, font[0], 2)}\n"
                    f"  m_sharedMaterial: {ref(font[1], font[0], 2)}\n"
                    "  m_fontSharedMaterials: []\n  m_fontMaterial: {fileID: 0}\n  m_fontMaterials: []\n"
                    f"  m_fontColor32:\n    serializedVersion: 2\n    rgba: {col32(color)}\n"
                    f"  m_fontColor: {col(color)}\n  m_enableVertexGradient: 0\n  m_colorMode: 3\n"
                    "  m_fontColorGradient:\n    topLeft: {r: 1, g: 1, b: 1, a: 1}\n"
                    "    topRight: {r: 1, g: 1, b: 1, a: 1}\n    bottomLeft: {r: 1, g: 1, b: 1, a: 1}\n"
                    "    bottomRight: {r: 1, g: 1, b: 1, a: 1}\n  m_fontColorGradientPreset: {fileID: 0}\n"
                    "  m_spriteAsset: {fileID: 0}\n  m_tintAllSprites: 0\n  m_StyleSheet: {fileID: 0}\n"
                    "  m_TextStyleHashCode: -1183493901\n  m_overrideHtmlColors: 0\n"
                    "  m_faceColor:\n    serializedVersion: 2\n    rgba: 4294967295\n"
                    f"  m_fontSize: {f32(size)}\n  m_fontSizeBase: {f32(size)}\n  m_fontWeight: 400\n"
                    f"  m_enableAutoSizing: 1\n  m_fontSizeMin: {f32(fit_min(size))}\n  m_fontSizeMax: {f32(size)}\n"
                    f"  m_fontStyle: {16 if upper else 0}\n  m_HorizontalAlignment: {h}\n"
                    "  m_VerticalAlignment: 512\n  m_textAlignment: 65535\n"
                    f"  m_characterSpacing: {f32(spacing)}\n  m_wordSpacing: 0\n  m_lineSpacing: 0\n"
                    "  m_lineSpacingMax: 0\n  m_paragraphSpacing: 0\n  m_charWidthMaxAdj: 0\n"
                    "  m_TextWrappingMode: 0\n  m_wordWrappingRatios: 0.4\n  m_overflowMode: 0\n"
                    "  m_linkedTextComponent: {fileID: 0}\n  parentLinkedComponent: {fileID: 0}\n"
                    "  m_enableKerning: 0\n  m_ActiveFontFeatures: 6e72656b\n  m_enableExtraPadding: 0\n"
                    "  checkPaddingRequired: 0\n  m_isRichText: 1\n  m_EmojiFallbackSupport: 1\n"
                    "  m_parseCtrlCharacters: 1\n  m_isOrthographic: 1\n  m_isCullingEnabled: 0\n"
                    "  m_horizontalMapping: 0\n  m_verticalMapping: 0\n  m_uvLineOffset: 0\n"
                    "  m_geometrySortingOrder: 0\n  m_IsTextObjectScaleStatic: 0\n"
                    "  m_VertexBufferAutoSizeReduction: 0\n  m_useMaxVisibleDescender: 1\n"
                    "  m_pageToDisplay: 1\n  m_margin: {x: 0, y: 0, z: 0, w: 0}\n"
                    "  m_isUsingLegacyAnimationComponent: 0\n  m_isVolumetricText: 0\n"
                    "  m_hasFontAssetChanged: 0\n  m_baseMaterial: {fileID: 0}\n"
                    "  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}\n")
    return body


def kit_text(role, color_token="TextLight", driven=False):
    roles_guid = guid_for("UITypeRoles")
    return lambda ctx, go: mono(go, kit_script("KitText"),
                                f"  roles: {ref(11400000, roles_guid, 2)}\n  role: {ROLES[role][0]}\n"
                                f"  theme: {ref(11400000, THEME, 2)}\n  colorToken: {TOKEN_INDEX[color_token]}\n"
                                f"  colorDrivenExternally: {1 if driven else 0}\n")


def themed(token, alpha=1.0):
    return lambda ctx, go: mono(go, kit_script("KitThemedGraphic"),
                                f"  theme: {ref(11400000, THEME, 2)}\n  token: {TOKEN_INDEX[token]}\n"
                                f"  alpha: {f32(alpha)}\n")


def hover_tint(token, text_keys, graphic_keys):
    """KitHoverTint; targets are `node#comp` keys inside the same file."""
    def body(ctx, go):
        texts = [ctx.c(*k.split("#")) for k in text_keys]
        graphics = [ctx.c(*k.split("#")) for k in graphic_keys]
        return mono(go, kit_script("KitHoverTint"),
                    f"  theme: {ref(11400000, THEME, 2)}\n  hoverToken: {TOKEN_INDEX[token]}\n"
                    + refs_list("texts", texts) + refs_list("graphics", graphics))
    return body


# A locked top-nav tab insets its word by the lock glyph's visible width plus the 8-unit gap.
LOCK_INSET = 23
NAV_ICON = 24        # the top-nav tab glyph (KitTopNavTab Icon children)
NAV_ICON_GAP = 8     # between the glyph and its word


def layout_element(ignore=False, pw=-1, ph=-1):
    return lambda ctx, go: mono(go, LAYOUT_ELEMENT,
                                f"  m_IgnoreLayout: {1 if ignore else 0}\n  m_MinWidth: -1\n  m_MinHeight: -1\n"
                                f"  m_PreferredWidth: {f32(pw)}\n  m_PreferredHeight: {f32(ph)}\n"
                                "  m_FlexibleWidth: -1\n  m_FlexibleHeight: -1\n  m_LayoutPriority: 1\n")


def hv_layout(vertical, pad=(0, 0, 0, 0), spacing=0, align=4, control=(1, 1), expand=(0, 0)):
    l, r, t, b = pad
    return lambda ctx, go: mono(go, VLAYOUT if vertical else HLAYOUT,
                                f"  m_Padding:\n    m_Left: {l}\n    m_Right: {r}\n    m_Top: {t}\n"
                                f"    m_Bottom: {b}\n  m_ChildAlignment: {align}\n  m_Spacing: {f32(spacing)}\n"
                                f"  m_ChildForceExpandWidth: {expand[0]}\n  m_ChildForceExpandHeight: {expand[1]}\n"
                                f"  m_ChildControlWidth: {control[0]}\n  m_ChildControlHeight: {control[1]}\n"
                                "  m_ChildScaleWidth: 0\n  m_ChildScaleHeight: 0\n  m_ReverseArrangement: 0\n")


def fitter(h=2, v=2):
    return lambda ctx, go: mono(go, FITTER, f"  m_HorizontalFit: {h}\n  m_VerticalFit: {v}\n")


SELECTABLE_FIELDS = (
    "  m_Navigation:\n    m_Mode: 3\n    m_WrapAround: 0\n    m_SelectOnUp: {fileID: 0}\n"
    "    m_SelectOnDown: {fileID: 0}\n    m_SelectOnLeft: {fileID: 0}\n    m_SelectOnRight: {fileID: 0}\n"
    "  m_Transition: 0\n  m_Colors:\n    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n"
    "    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n"
    "    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}\n"
    "    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n"
    "    m_DisabledColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}\n"
    "    m_ColorMultiplier: 1\n    m_FadeDuration: 0.1\n  m_SpriteState:\n"
    "    m_HighlightedSprite: {fileID: 0}\n    m_PressedSprite: {fileID: 0}\n"
    "    m_SelectedSprite: {fileID: 0}\n    m_DisabledSprite: {fileID: 0}\n  m_AnimationTriggers:\n"
    "    m_NormalTrigger: Normal\n    m_HighlightedTrigger: Highlighted\n    m_PressedTrigger: Pressed\n"
    "    m_SelectedTrigger: Selected\n    m_DisabledTrigger: Disabled\n  m_Interactable: 1\n")


def refs_list(name, ids):
    if not ids:
        return f"  {name}: []\n"
    return f"  {name}:\n" + "".join(f"  - {{fileID: {i}}}\n" for i in ids)


# --------------------------------------------------------------------------------------------
# The prefabs
# --------------------------------------------------------------------------------------------

def label_width(text, role):
    """Rough preferred width, so authored sizes are close to what the layout computes."""
    _, face, size, spacing, upper, _ = ROLES[role]
    s = text.upper() if upper else text
    try:
        from PIL import ImageFont
        ttf = (os.path.join(REPO, "Assets/_Graphics/Fonts/ChakraPetch/ChakraPetch-SemiBold.ttf") if face == "Chakra"
               else os.path.join(REPO, "Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/ALDRICH-REGULAR.TTF"))
        w = ImageFont.truetype(ttf, int(size * 10)).getlength(s) / 10.0
    except Exception:
        w = len(s) * size * 0.62
    return round(w + spacing / 100.0 * size * max(0, len(s) - 1), 1)


def line_height(role):
    _, face, size, *_ = ROLES[role]
    return round(size * (1.3 if face == "Chakra" else 1.29), 1)


def build_button(key, small=False):
    role = "ButtonSmall" if small else "Button"
    padx, pady = (18, 9) if small else (28, 14)
    text = "CONFIRM"
    border, fillc, content = button_palette("Default", False)
    w = label_width(text, role) + 2 * padx
    h = line_height(role) + 2 * pady
    root = Node("root", "KitButton", rect(size=(w, h)))
    ring = root.child(Node("FocusRing", "FocusRing", rect((0, 0), (1, 1), (0, 0), (10, 10))))
    ring.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("Cta", 0.0), kit_sprite("kit_sliver10_line2"), True)) \
        .add("le", 114, layout_element(ignore=True))
    fill = root.child(Node("Fill", "Fill", STRETCH))
    fill.add("cr", 222, canvas_renderer()).add("img", 114, image(fillc, kit_sprite("kit_sliver10_fill"), True, raycast=True)) \
        .add("grad", 114, lambda ctx, go: mono(go, kit_script("KitGradient"), "  lightenRight: 0.5\n", enabled=False)) \
        .add("le", 114, layout_element(ignore=True))
    bord = root.child(Node("Border", "Border", STRETCH))
    bord.add("cr", 222, canvas_renderer()).add("img", 114, image(border, kit_sprite("kit_sliver10_line1"), True)) \
        .add("le", 114, layout_element(ignore=True))
    icon_size = 14 if small else 18
    icon = root.child(Node("Icon", "Icon", rect(size=(icon_size, icon_size)), active=False))
    icon.add("cr", 222, canvas_renderer()).add("img", 114, image(content, chrome_sprite("chrome_crystal"), preserve=True)) \
        .add("le", 114, layout_element(pw=icon_size, ph=icon_size))
    lab = root.child(Node("Label", "Label", rect(size=(label_width(text, role), line_height(role)))))
    lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp(text, role, content)) \
        .add("kt", 114, kit_text(role, driven=True))

    def button_body(ctx, go):
        return mono(go, kit_script("KitButton"),
                    SELECTABLE_FIELDS
                    + f"  m_TargetGraphic: {{fileID: {ctx.c('Fill', 'img')}}}\n"
                    "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"
                    f"  theme: {ref(11400000, THEME, 2)}\n  focusRing: {{fileID: {ctx.c('FocusRing', 'img')}}}\n"
                    "  variant: 0\n  fill: 0\n"
                    f"  fillGraphic: {{fileID: {ctx.c('Fill', 'img')}}}\n"
                    f"  borderGraphic: {{fileID: {ctx.c('Border', 'img')}}}\n"
                    + refs_list("contentGraphics", [ctx.c("Label", "tmp"), ctx.c("Icon", "img")])
                    + f"  playGradient: {{fileID: {ctx.c('Fill', 'grad')}}}\n")
    root.add("button", 114, button_body)
    root.add("hlg", 114, hv_layout(False, (padx, padx, pady, pady), 10, 4, (1, 1), (0, 0)))
    root.add("csf", 114, fitter(2, 2))
    return root


def build_tile():
    pal = tile_palette(False, False)
    root = Node("root", "KitTile", rect(size=(300, 96)))
    ring = root.child(Node("FocusRing", "FocusRing", rect((0, 0), (1, 1), (0, 0), (10, 10))))
    ring.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("Cta", 0.0), kit_sprite("kit_sliver14_line2"), True))
    fill = root.child(Node("Fill", "Fill", STRETCH))
    fill.add("cr", 222, canvas_renderer()).add("img", 114, image(pal["fill"], kit_sprite("kit_sliver14_fill"), True, raycast=True))
    bord = root.child(Node("Border", "Border", STRETCH))
    bord.add("cr", 222, canvas_renderer()).add("img", 114, image(pal["border"], kit_sprite("kit_sliver14_line1"), True))
    art = root.child(Node("Art", "Art", rect((0, 0.5), None, (16, 0), (72, 72), (0, 0.5))))
    art.add("cr", 222, canvas_renderer()).add("img", 114, image(pal["art"], chrome_sprite("chrome_vessel_placeholder"), preserve=True))
    lock = root.child(Node("Lock", "Lock", rect((0, 0.5), None, (40, 0), (28, 28), (0.5, 0.5))))
    lock.add("cr", 222, canvas_renderer()).add("img", 114, image(pal["lock"], chrome_sprite("chrome_lock"), preserve=True))
    name = root.child(Node("Name", "Name", rect((0, 0.5), (1, 0.5), (52, 12), (-120, 24), (0.5, 0.5))))
    name.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("Vessel", "Body", pal["primary"], "left")) \
        .add("kt", 114, kit_text("Body", driven=True))
    status = root.child(Node("Status", "Status", rect((0, 0.5), (1, 0.5), (52, -16), (-120, 18), (0.5, 0.5))))
    status.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("STATUS", "Label", pal["secondary"], "left")) \
        .add("kt", 114, kit_text("Label", driven=True))
    root.add("tile", 114, tile_body("Fill", "Border", "kit_sliver14_line2", "kit_sliver14_line1",
                                    ["Name"], "TextLight", ["Status"], ["Art"], "Lock"))
    return root


def tile_body(fill, border, sel_sprite, unsel_sprite, primary, unsel_primary, secondary, art, lock,
              sel_border="Selection", lock_label=None):
    def body(ctx, go):
        return mono(go, kit_script("KitTile"),
                    SELECTABLE_FIELDS
                    + f"  m_TargetGraphic: {{fileID: {ctx.c(fill, 'img')}}}\n"
                    "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"
                    f"  theme: {ref(11400000, THEME, 2)}\n  focusRing: {{fileID: {ctx.c('FocusRing', 'img')}}}\n"
                    "  isSelected: 0\n  isLocked: 0\n"
                    f"  fillImage: {{fileID: {ctx.c(fill, 'img')}}}\n"
                    f"  borderImage: {{fileID: {ctx.c(border, 'img')}}}\n"
                    f"  borderSelectedSprite: {sprite_ref(kit_sprite(sel_sprite))}\n"
                    f"  borderUnselectedSprite: {sprite_ref(kit_sprite(unsel_sprite))}\n"
                    f"  selectedBorderToken: {TOKEN_INDEX[sel_border]}\n"
                    + refs_list("primaryGraphics", [ctx.c(k, "tmp" if k in ("Name", "Label") else "img") for k in primary])
                    + f"  unselectedPrimaryToken: {TOKEN_INDEX[unsel_primary]}\n"
                    + refs_list("secondaryGraphics", [ctx.c(k, "tmp") for k in secondary])
                    + f"  lockedSecondaryToken: {TOKEN_INDEX['Purchase']}\n"
                    + refs_list("artGraphics", [ctx.c(k, "img") for k in art])
                    + (f"  lockGraphic: {{fileID: {ctx.c(lock, 'img')}}}\n" if lock else "  lockGraphic: {fileID: 0}\n")
                    + (f"  lockLabel: {{fileID: {ctx.c(lock_label, 'tmp')}}}\n" if lock_label else "  lockLabel: {fileID: 0}\n"))
    return body


def build_hex_tile():
    pal = tile_palette(False, False, unselected_primary="NeutralLightest")
    root = Node("root", "KitHexNavTile", rect(size=(120, 108)))
    ring = root.child(Node("FocusRing", "FocusRing", rect((0.5, 1), None, (0, 6), (100, 88), (0.5, 1))))
    ring.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("Cta", 0.0), kit_sprite("kit_hex_line2")))
    fill = root.child(Node("Fill", "Fill", rect((0.5, 1), None, (0, 0), (88, 76), (0.5, 1))))
    fill.add("cr", 222, canvas_renderer()).add("img", 114, image(pal["fill"], kit_sprite("kit_hex_fill"), raycast=True))
    bord = root.child(Node("Border", "Border", rect((0.5, 1), None, (0, 0), (88, 76), (0.5, 1))))
    bord.add("cr", 222, canvas_renderer()).add("img", 114, image(pal["border"], kit_sprite("kit_hex_line2")))
    icon = root.child(Node("Icon", "Icon", rect((0.5, 1), None, (0, -22), (32, 32), (0.5, 1))))
    icon.add("cr", 222, canvas_renderer()).add("img", 114, image(pal["primary"], chrome_sprite("chrome_vessel_placeholder"), preserve=True))
    lab = root.child(Node("Label", "Label", rect((0, 0), (1, 0), (0, 0), (0, 18), (0.5, 0))))
    lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("SECTION", "Label", pal["primary"])) \
        .add("kt", 114, kit_text("Label", driven=True))
    root.add("tile", 114, tile_body("Fill", "Border", "kit_hex_line2", "kit_hex_line2",
                                    ["Icon", "Label"], "NeutralLightest", [], [], None))
    return root


def build_top_nav_tab():
    root = Node("root", "KitTopNavTab", rect(size=(140, 84)))
    # The pad's focus ring (Docs/MENU_REDESIGN_B.md §64): the tab is a pad stop, and LT / RT page the screens only
    # while the pad is on the bar. Behind the words; inset from the 84-tall band so it stays inside it.
    ring = root.child(Node("FocusRing", "FocusRing", rect((0, 0), (1, 1), (0, 0), (8, -16))))
    ring.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("Cta", 0.0), kit_sprite("kit_sliver10_line2"), True))
    # The whole tab is the hit area (labels do not raycast): an invisible Image, culled by its
    # CanvasRenderer, so the host's EventTrigger and the hover lift answer anywhere on the tab.
    root.add("cr", 222, canvas_renderer()).add("img", 114, image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("hover", 114, hover_tint("TextLight", ["InactiveImage#kt"], ["InactiveImage/Icon#th"]))
    # KitTopNavTab: a Selectable that owns only the ring. Its onClick stays empty - the host's EventTrigger
    # presses the tab, on a click and on Submit (A) - so a click never presses twice.
    root.add("tab", 114, lambda ctx, go: mono(go, kit_script("KitTopNavTab"),
                                               SELECTABLE_FIELDS
                                               + f"  m_TargetGraphic: {{fileID: {ctx.c('root', 'img')}}}\n"
                                               "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"
                                               f"  theme: {ref(11400000, THEME, 2)}\n"
                                               f"  focusRing: {{fileID: {ctx.c('FocusRing', 'img')}}}\n"))
    inactive = root.child(Node("InactiveImage", "InactiveImage", STRETCH))
    inactive.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("TAB", "Button", tok("NeutralLightest"))) \
        .add("kt", 114, kit_text("Button", "NeutralLightest"))
    # The tab's glyph (rev 3: the old nav's icons back beside the word). Off until a host sets its
    # sprite and its x - the word is centred, so only the host knows where the word's left edge is.
    iicon = inactive.child(Node("InactiveImage/Icon", "Icon", rect(size=(NAV_ICON, NAV_ICON)), active=False))
    iicon.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("NeutralLightest"), None, preserve=True)) \
        .add("th", 114, themed("NeutralLightest"))
    # The locked look, off by default. A host's MenuAvailabilityView shows it as its lockedOverlay;
    # the host also insets InactiveImage by LOCK_INSET so the word clears the glyph. The glyph's
    # visible box is 59% x 64% of the sprite, so a 25-unit rect draws it about 15 x 16, as the board.
    lock = root.child(Node("Lock", "Lock", rect((0, 0.5), None, (7.5, 0), (25, 25)), active=False))
    lock.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("InactiveLight"), chrome_sprite("chrome_lock"), preserve=True)) \
        .add("th", 114, themed("InactiveLight"))
    active = root.child(Node("ActiveImage", "ActiveImage", STRETCH, active=False))
    # The label sizes itself to its text so the 2px underline beneath it spans the word, not the tab.
    alab = active.child(Node("ActiveImage/Label", "Label",
                             rect(size=(label_width("TAB", "Button"), line_height("Button")))))
    # Rev 7: the shown screen's word and underline in the sky-blue Selection; the underline carries the selection
    # light (SelectionBar.mat: a sweep running along it, breathing), placed by KitRimMesh.
    alab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("TAB", "Button", tok("Selection"))) \
        .add("kt", 114, kit_text("Button", "Selection")).add("csf", 114, fitter(2, 0))
    under = alab.child(Node("ActiveImage/Underline", "Underline", rect((0, 0), (1, 0), (0, -6), (0, 2), (0.5, 0.5))))
    bar_mat = hashlib.md5(b"CosmicShore/MenuB/lattice/selection_bar_mat").hexdigest()
    under.add("cr", 222, canvas_renderer()).add("img", 114, lambda ctx, go: image(tok("Selection"))(ctx, go).replace(
        "  m_Material: {fileID: 0}\n", f"  m_Material: {{fileID: 2100000, guid: {bar_mat}, type: 2}}\n", 1)) \
        .add("th", 114, themed("Selection")) \
        .add("rim", 114, lambda ctx, go: mono(go, "b403ee3874d54f79d0f27e7013879c0f", ""))
    # The lit glyph hangs off the word's left edge, so it follows the fitted label without a layout.
    aicon = alab.child(Node("ActiveImage/Icon", "Icon", rect((0, 0.5), None, (-(NAV_ICON_GAP + NAV_ICON / 2), 0),
                                                              (NAV_ICON, NAV_ICON)), active=False))
    aicon.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("TextLight"), None, preserve=True)) \
        .add("th", 114, themed("TextLight"))
    return root


def focus_ring(parent, key, size=(8, 8), sprite="kit_sliver10_line2"):
    """A pad focus ring under `parent` (node key `<key>/FocusRing`), painted by KitFocusRing on the host: the kit's
    sky-blue Selection while the pad or the keyboard has the control, hidden for a pointer (Docs/MENU_REDESIGN_B.md
    §65: every press the menu offers is a pad stop, and one the player cannot see is not). Returns the host's
    KitFocusRing component body."""
    ring = parent.child(Node(f"{key}/FocusRing", "FocusRing", rect((0, 0), (1, 1), (0, 0), size)))
    ring.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("Selection", 0.0), kit_sprite(sprite), True))
    return lambda ctx, go: mono(go, kit_script("KitFocusRing"),
                                f"  theme: {ref(11400000, THEME, 2)}\n"
                                f"  ring: {{fileID: {ctx.c(f'{key}/FocusRing', 'img')}}}\n"
                                f"  ringToken: {TOKEN_INDEX['Selection']}\n")


def framed(root, sliver, fill_token, border_token, line="line1", raycast=False):
    """Slivered fill + border children, each painted by KitThemedGraphic."""
    fill = root.child(Node("Fill", "Fill", STRETCH))
    fill.add("cr", 222, canvas_renderer()).add("img", 114, image(tok(fill_token), kit_sprite(f"kit_sliver{sliver}_fill"), True, raycast)) \
        .add("th", 114, themed(fill_token)).add("le", 114, layout_element(ignore=True))
    bord = root.child(Node("Border", "Border", STRETCH))
    bord.add("cr", 222, canvas_renderer()).add("img", 114, image(tok(border_token), kit_sprite(f"kit_sliver{sliver}_{line}"), True)) \
        .add("th", 114, themed(border_token)).add("le", 114, layout_element(ignore=True))


def build_keycap():
    w = label_width("LT", "Label") + 24
    root = Node("root", "KitKeycap", rect(size=(w, line_height("Label") + 8)))
    framed(root, 10, "SurfaceVeryDark", "SurfaceLight")
    lab = root.child(Node("Label", "Label", rect(size=(label_width("LT", "Label"), line_height("Label")))))
    lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("LT", "Label", tok("NeutralLightest"))) \
        .add("kt", 114, kit_text("Label", "NeutralLightest"))
    root.add("hlg", 114, hv_layout(False, (12, 12, 4, 4), 0, 4, (1, 1), (0, 0)))
    root.add("csf", 114, fitter(2, 2))
    # A phone or tablet with no pad names no controls: the cap hides and its slot closes up
    # (KitKeycapVisibility, Docs/MENU_REDESIGN_B.md section 28).
    root.add("cg", 225, lambda ctx, go: ("CanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                                         "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                                         f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Alpha: 1\n"
                                         "  m_Interactable: 0\n  m_BlocksRaycasts: 0\n  m_IgnoreParentGroups: 0\n"))
    root.add("vis", 114, lambda ctx, go: mono(go, kit_script("KitKeycapVisibility"), "  collapse: {fileID: 0}\n"))
    return root


def build_panel():
    root = Node("root", "KitPanel", rect(size=(560, 400)))
    framed(root, 14, "SurfaceVeryDark", "SurfaceDark")
    content = root.child(Node("Content", "Content", rect((0, 0), (1, 1), (0, 0), (-52, -44))))
    content.add("vlg", 114, hv_layout(True, (0, 0, 0, 0), 16, 0, (1, 0), (1, 0)))
    return root


def build_section_label():
    root = Node("root", "KitSectionLabel", rect(size=(300, line_height("Label"))))
    root.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("SECTION", "Label", tok("NeutralLightest"), "left")) \
        .add("kt", 114, kit_text("Label", "NeutralLightest"))
    return root


def build_divider():
    root = Node("root", "KitDivider", rect(size=(300, 1)))
    root.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("SurfaceDark"))) \
        .add("th", 114, themed("SurfaceDark")).add("le", 114, layout_element(ph=1))
    return root


def build_chip():
    text = "Keyword"
    root = Node("root", "KitChip", rect(size=(label_width(text, "BodySmall") + 24, line_height("BodySmall") + 10)))
    framed(root, 10, "SurfaceVeryDark", "SurfaceLight")
    lab = root.child(Node("Label", "Label", rect(size=(label_width(text, "BodySmall"), line_height("BodySmall")))))
    lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp(text, "BodySmall", tok("TextLight"))) \
        .add("kt", 114, kit_text("BodySmall"))
    root.add("hlg", 114, hv_layout(False, (12, 12, 5, 5), 0, 4, (1, 1), (0, 0)))
    root.add("csf", 114, fitter(2, 2))
    return root


def build_currency_pill():
    root = Node("root", "KitCurrencyPill", rect(size=(label_width("0", "Data") + 60, line_height("Data") + 16)))
    framed(root, 10, "SurfaceVeryDark", "TextLight")
    icon = root.child(Node("Icon", "Icon", rect(size=(18, 18))))
    icon.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("Purchase"), chrome_sprite("chrome_crystal"), preserve=True)) \
        .add("th", 114, themed("Purchase")).add("le", 114, layout_element(pw=18, ph=18))
    amt = root.child(Node("Amount", "Amount", rect(size=(label_width("0", "Data"), line_height("Data")))))
    amt.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("0", "Data", tok("TextLight"), "left")) \
        .add("kt", 114, kit_text("Data"))
    root.add("hlg", 114, hv_layout(False, (16, 16, 8, 8), 10, 4, (1, 1), (0, 0)))
    root.add("csf", 114, fitter(2, 2))
    return root


def button_mods(prefab_key, variant, opaque, text, disabled=False, small=False):
    """Instance overrides that set a KitButton's variant, fill and label - with the colours its
    KitButton computes, so nothing re-serializes when it applies itself."""
    role = "ButtonSmall" if small else "Button"
    border, fillc, content = button_palette(variant, opaque, disabled)
    mods = [("root#button", "variant", VARIANTS[variant], None),
            ("root#button", "fill", 1 if opaque else 0, None),
            ("Label#tmp", "m_text", text, None),
            ("Fill#img", "m_Color.r", fillc[0], None), ("Fill#img", "m_Color.g", fillc[1], None),
            ("Fill#img", "m_Color.b", fillc[2], None), ("Fill#img", "m_Color.a", fillc[3], None),
            ("Border#img", "m_Color.r", border[0], None), ("Border#img", "m_Color.g", border[1], None),
            ("Border#img", "m_Color.b", border[2], None), ("Border#img", "m_Color.a", border[3], None),
            ("Label#tmp", "m_fontColor.r", content[0], None), ("Label#tmp", "m_fontColor.g", content[1], None),
            ("Label#tmp", "m_fontColor.b", content[2], None), ("Label#tmp", "m_fontColor.a", content[3], None),
            ("Label#tmp", "m_fontColor32.rgba", col32(content), None),
            ("Icon#img", "m_Color.r", content[0], None), ("Icon#img", "m_Color.g", content[1], None),
            ("Icon#img", "m_Color.b", content[2], None), ("Icon#img", "m_Color.a", content[3], None)]
    if disabled:
        mods.append(("root#button", "m_Interactable", 0, None))
    if variant == "Play" and opaque:
        mods.append(("Fill#grad", "m_Enabled", 1, None))
    return mods


def button_size(text, small=False):
    role = "ButtonSmall" if small else "Button"
    padx, pady = (18, 9) if small else (28, 14)
    return (label_width(text, role) + 2 * padx, line_height(role) + 2 * pady)


def build_popup(button_guid):
    root = Node("root", "KitPopup", rect(size=(640, 220)))
    framed(root, 14, "SurfaceBlack", "TextLight")
    content = root.child(Node("Content", "Content", rect((0, 0), (1, 1), (0, 12), (-80, -96))))
    content.add("vlg", 114, hv_layout(True, (0, 0, 0, 0), 16, 0, (1, 0), (1, 0)))
    eyebrow = content.child(Node("Content/Eyebrow", "Eyebrow", rect(size=(560, line_height("Label")))))
    eyebrow.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("EYEBROW", "Label", tok("NeutralLightest"), "left")) \
        .add("kt", 114, kit_text("Label", "NeutralLightest"))
    title = content.child(Node("Content/Title", "Title", rect(size=(560, line_height("H2")))))
    title.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("Popup title", "H2", tok("TextLight"), "left")) \
        .add("kt", 114, kit_text("H2"))
    body = content.child(Node("Content/Body", "Body", rect(size=(560, line_height("Body")))))
    body.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("Body copy.", "Body", tok("TextLight"), "left")) \
        .add("kt", 114, kit_text("Body"))
    # §10.3: the action buttons straddle the bottom border - the row's CENTRE line is the border.
    row = root.child(Node("Buttons", "Buttons", rect((0, 0), None, (40, 0), (400, 52), (0, 0.5))))
    row.add("hlg", 114, hv_layout(False, (0, 0, 0, 0), 18, 3, (0, 0), (0, 0)))
    row.add("csf", 114, fitter(2, 2))
    sz = button_size("CONFIRM")
    row.children.append(Instance("Confirm", "KitButton", button_guid, "Confirm",
                                 rect((0, 0.5), None, (sz[0] / 2, 0), sz),
                                 button_mods("KitButton", "Cta", True, "CONFIRM")))
    sz2 = button_size("CANCEL")
    row.children.append(Instance("Cancel", "KitButton", button_guid, "Cancel",
                                 rect((0, 0.5), None, (sz[0] + 18 + sz2[0] / 2, 0), sz2),
                                 button_mods("KitButton", "Cancel", True, "CANCEL")))
    return root


# --------------------------------------------------------------------------------------------
# Files
# --------------------------------------------------------------------------------------------

PREFABS = ["KitButton", "KitTile", "KitHexNavTile", "KitTopNavTab", "KitKeycap", "KitPanel",
           "KitPopup", "KitChip", "KitSectionLabel", "KitDivider", "KitCurrencyPill"]


def prefab_guid(name):
    return guid_for(f"prefab/{name}")


def prefab_text(name):
    builders = {"KitButton": lambda: build_button("KitButton"), "KitTile": build_tile,
                "KitHexNavTile": build_hex_tile, "KitTopNavTab": build_top_nav_tab,
                "KitKeycap": build_keycap, "KitPanel": build_panel,
                "KitPopup": lambda: build_popup(prefab_guid("KitButton")), "KitChip": build_chip,
                "KitSectionLabel": build_section_label, "KitDivider": build_divider,
                "KitCurrencyPill": build_currency_pill}
    root = builders[name]()
    root.name = name
    ctx = Ctx(name)
    out = []
    emit_node(ctx, root, 0, out)
    return HEADER + "".join(out)


def variant_text():
    """KitButtonSmall - a Prefab Variant of KitButton (CLAUDE.md: a variant, never a copy)."""
    base = prefab_guid("KitButton")
    pid = FileIds("KitButtonSmall")("variant#inst")
    w, h = button_size("CONFIRM", small=True)
    _, _, _, spacing, _, _ = ROLES["ButtonSmall"]
    mods = [
        ("root#go", "m_Name", "KitButtonSmall"),
        ("root#rt", "m_SizeDelta.x", f32(w)), ("root#rt", "m_SizeDelta.y", f32(h)),
        ("root#hlg", "m_Padding.m_Left", 18), ("root#hlg", "m_Padding.m_Right", 18),
        ("root#hlg", "m_Padding.m_Top", 9), ("root#hlg", "m_Padding.m_Bottom", 9),
        ("Label#tmp", "m_fontSize", 14), ("Label#tmp", "m_fontSizeBase", 14),
        ("Label#kt", "role", ROLES["ButtonSmall"][0]),
        ("Label#rt", "m_SizeDelta.x", f32(label_width("CONFIRM", "ButtonSmall"))),
        ("Label#rt", "m_SizeDelta.y", f32(line_height("ButtonSmall"))),
        ("Icon#rt", "m_SizeDelta.x", 14), ("Icon#rt", "m_SizeDelta.y", 14),
        ("Icon#le", "m_PreferredWidth", 14), ("Icon#le", "m_PreferredHeight", 14),
    ]
    lines = "".join(f"    - target: {ref(source_fid('KitButton', k), base)}\n      propertyPath: {p}\n"
                    f"      value: {v}\n      objectReference: {{fileID: 0}}\n" for k, p, v in mods)
    return (HEADER + f"--- !u!1001 &{pid}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
            "  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 0}\n"
            "    m_Modifications:\n" + lines
            + "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n"
            f"    m_AddedComponents: []\n  m_SourcePrefab: {ref(100100000, base)}\n")


def roles_asset_text():
    rows = "".join(
        f"  - role: {idx}\n    face: {0 if face == 'Aldrich' else 1}\n    size: {f32(size)}\n"
        f"    characterSpacing: {f32(sp)}\n    upperCase: {1 if up else 0}\n    tabular: {1 if tab else 0}\n"
        for _, (idx, face, size, sp, up, tab) in sorted(ROLES.items(), key=lambda kv: kv[1][0]))
    return (HEADER + "--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n"
            f"  m_EditorHideFlags: 0\n  m_Script: {ref(11500000, kit_script('UITypeRolesSO'))}\n"
            "  m_Name: UITypeRoles\n  m_EditorClassIdentifier: \n"
            f"  aldrich: {ref(11400000, ALDRICH[0], 2)}\n"
            f"  chakraPetchSemiBold: {ref(11400000, SEMIBOLD[0], 2)}\n  roles:\n" + rows)


# ----- the test scene ------------------------------------------------------------------------

SCENE_SETTINGS_DONOR = "Assets/_Scenes/Game_TestDesign/SafeAreaFitterTestScene.unity"


def scene_settings():
    """The four scene-settings documents (&1-&4), copied from a sibling test scene."""
    t = open(os.path.join(REPO, SCENE_SETTINGS_DONOR)).read()
    m = re.search(r"(--- !u!29 &1\n.*?)(?=--- !u!1 &)", t, re.S)
    assert m, "scene settings block not found in the donor"
    return m.group(1)


def sprite_row(parent, key, y, label, sprite, widths, height, sliced=True):
    lab = parent.child(Node(f"{key}_lab", label, rect((0, 1), None, (0, y), (240, height), (0, 1))))
    lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp(label, "Label", tok("NeutralLightest"), "left")) \
        .add("kt", 114, kit_text("Label", "NeutralLightest"))
    x = 250
    for i, w in enumerate(widths):
        h = height if sliced else height * w / widths[1]
        n = parent.child(Node(f"{key}_{i}", f"{label} {int(w)}", rect((0, 1), None, (x, y), (w, h), (0, 1))))
        n.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("TextLight"), kit_sprite(sprite), sliced))
        x += w + 24


def build_scene():
    btn_guid, tile_guid = prefab_guid("KitButton"), prefab_guid("KitTile")
    canvas = Node("Canvas", "Canvas", rect((0, 0), None, (0, 0), (0, 0), (0, 0)))
    canvas.add("canvas", 223, lambda ctx, go: (
        "Canvas:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
        "  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n"
        "  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n"
        "  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_VertexColorAlwaysGammaSpace: 1\n"
        "  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n"
        "  m_SortingLayerID: 0\n  m_SortingOrder: 0\n  m_TargetDisplay: 0\n"))
    canvas.add("scaler", 114, lambda ctx, go: mono(go, CANVAS_SCALER,
        "  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 240\n  m_ScaleFactor: 1\n"
        "  m_ReferenceResolution: {x: 1920, y: 1080}\n  m_ScreenMatchMode: 0\n  m_MatchWidthOrHeight: 0.5\n"
        "  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n"
        "  m_DynamicPixelsPerUnit: 1\n  m_PresetInfoIsWorld: 0\n"))
    canvas.add("raycaster", 114, lambda ctx, go: mono(go, GRAPHIC_RAYCASTER,
        "  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n"
        "    serializedVersion: 2\n    m_Bits: 4294967295\n"))

    bg = canvas.child(Node("Background", "Background", STRETCH))
    bg.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("SurfaceBlack"))).add("th", 114, themed("SurfaceBlack"))

    # ---- left: every sprite at three widths (T7) ----
    left = canvas.child(Node("Sprites", "Sprites", rect((0, 1), None, (32, -24), (900, 1032), (0, 1))))
    hdr = left.child(Node("hdr_left", "Header", rect((0, 1), None, (0, 0), (900, 18), (0, 1))))
    hdr.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("UI KIT / SPRITES AT THREE WIDTHS", "Label", tok("TextLight"), "left")) \
        .add("kt", 114, kit_text("Label"))
    y = -34
    for cut, height, widths in ((14, 44, (48, 160, 320)), (10, 34, (40, 140, 300))):
        for kind in ("fill", "line1", "line2"):
            for flip in ("", "_flip"):
                name = f"kit_sliver{cut}_{kind}{flip}"
                sprite_row(left, name, y, name, name, widths, height)
                y -= height + 8
        y -= 8
    for name, size in (("kit_hex_fill", (88, 76)), ("kit_hex_line1", (88, 76)),
                       ("kit_hex_line2", (88, 76)), ("kit_hex_handle", (32, 28))):
        w0, h0 = size
        widths = (w0 * 0.5, w0 * 0.75, w0)       # three sizes, uniform scale (a hexagon cannot slice)
        lab = left.child(Node(f"{name}_lab", name, rect((0, 1), None, (0, y), (240, 18), (0, 1))))
        lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp(name, "Label", tok("NeutralLightest"), "left")) \
            .add("kt", 114, kit_text("Label", "NeutralLightest"))
        x = 250
        for i, w in enumerate(widths):
            h = h0 * w / w0
            n = left.child(Node(f"{name}_{i}", f"{name} {int(w)}", rect((0, 1), None, (x, y), (w, h), (0, 1))))
            n.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("TextLight"), kit_sprite(name)))
            x += w + 24
        y -= h0 + 10
    lab = left.child(Node("banner_lab", "kit_banner_*", rect((0, 1), None, (0, y), (240, 18), (0, 1))))
    lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("kit_banner_*", "Label", tok("NeutralLightest"), "left")) \
        .add("kt", 114, kit_text("Label", "NeutralLightest"))
    x = 250
    for i, w in enumerate((60, 120, 200)):
        for part, pw, dx in (("kit_banner_cap_left", 28, 0), ("kit_banner_body", w, 28), ("kit_banner_cap_right", 28, 28 + w)):
            n = left.child(Node(f"banner_{i}_{part}", f"{part} {w}", rect((0, 1), None, (x + dx, y), (pw, 56), (0, 1))))
            alpha = 0.5 if "cap" in part else 1.0
            n.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("NeutralLightest", alpha), kit_sprite(part), part == "kit_banner_body"))
        x += w + 56 + 24

    # ---- right: components in their states ----
    right = canvas.child(Node("Components", "Components", rect((0, 1), None, (980, -24), (908, 1032), (0, 1))))
    h2 = right.child(Node("hdr_right", "Header", rect((0, 1), None, (0, 0), (908, 18), (0, 1))))
    h2.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp("UI KIT / COMPONENTS", "Label", tok("TextLight"), "left")) \
        .add("kt", 114, kit_text("Label"))

    nav = right.child(Node("TopNav", "TopNav", rect((0, 1), None, (0, -30), (908, 84), (0, 1))))
    nav.add("cr", 222, canvas_renderer()).add("img", 114, image(tok("SurfaceVeryDark"))).add("th", 114, themed("SurfaceVeryDark"))
    x = 16
    nav.children.append(Instance("KeyL", "KitKeycap", prefab_guid("KitKeycap"), "LT",
                                 rect((0, 0.5), None, (x, 0), (label_width("LT", "Label") + 24, line_height("Label") + 8), (0, 0.5))))
    x = 88
    for i, (tab, state) in enumerate((("HANGAR", "active"), ("ARK", ""), ("HOME", ""), ("PORT", "locked"), ("PROFILE", ""))):
        nav.children.append(Instance(f"Tab{i}", "KitTopNavTab", prefab_guid("KitTopNavTab"), f"{tab.title()}Tab",
                                     rect((0, 0.5), None, (x, 0), (140, 84), (0, 0.5)),
                                     [("InactiveImage#tmp", "m_text", tab, None), ("ActiveImage/Label#tmp", "m_text", tab, None),
                                      ("InactiveImage#go", "m_IsActive", 0 if state == "active" else 1, None),
                                      ("ActiveImage#go", "m_IsActive", 1 if state == "active" else 0, None)]
                                     + ([("InactiveImage#kt", "colorToken", TOKEN_INDEX["InactiveLight"], None),
                                         ("InactiveImage#tmp", "m_fontColor.r", tok("InactiveLight")[0], None),
                                         ("InactiveImage#tmp", "m_fontColor.g", tok("InactiveLight")[1], None),
                                         ("InactiveImage#tmp", "m_fontColor.b", tok("InactiveLight")[2], None),
                                         ("InactiveImage#tmp", "m_fontColor32.rgba", col32(tok("InactiveLight")), None),
                                         ("InactiveImage#rt", "m_SizeDelta.x", -LOCK_INSET, None),
                                         ("InactiveImage#rt", "m_AnchoredPosition.x", LOCK_INSET / 2, None),
                                         ("Lock#go", "m_IsActive", 1, None),
                                         ("root#hover", "hoverToken", TOKEN_INDEX["NeutralLightest"], None)]
                                        if state == "locked" else [])))
        x += 140
    nav.children.append(Instance("KeyR", "KitKeycap", prefab_guid("KitKeycap"), "RT",
                                 rect((1, 0.5), None, (-16, 0), (label_width("RT", "Label") + 24, line_height("Label") + 8), (1, 0.5)),
                                 [("Label#tmp", "m_text", "RT", None)]))

    y = -134
    rows = [("Default", "CONFIRM"), ("Inactive", "UNLOCKED"), ("Cancel", "CANCEL"), ("Purchase", "UNLOCK - 4000"),
            ("Cta", "FREE"), ("Play", "TAP TO PLAY"), ("Danger", "DELETE MY DATA")]
    for i, (variant, text) in enumerate(rows):
        lab = right.child(Node(f"btnlab{i}", f"{variant} label", rect((0, 1), None, (0, y - 16), (120, 18), (0, 1))))
        lab.add("cr", 222, canvas_renderer()).add("tmp", 114, tmp(variant.upper(), "Label", tok("NeutralLightest"), "left")) \
            .add("kt", 114, kit_text("Label", "NeutralLightest"))
        sz = button_size(text)
        if variant != "Danger":
            right.children.append(Instance(f"btnT{i}", "KitButton", btn_guid, f"{variant} Transparent",
                                           rect((0, 1), None, (130, y), sz, (0, 1)),
                                           button_mods("KitButton", variant, False, text, disabled=variant == "Inactive")))
        right.children.append(Instance(f"btnO{i}", "KitButton", btn_guid, f"{variant} Opaque",
                                       rect((0, 1), None, (430, y), sz, (0, 1)),
                                       button_mods("KitButton", variant, True, text, disabled=variant == "Inactive")))
        y -= 60
    small_sz = button_size("BACK", small=True)
    right.children.append(Instance("btnSmall", "KitButtonSmall", prefab_guid("KitButtonSmall"), "Small Default",
                                   rect((0, 1), None, (730, -134), small_sz, (0, 1)),
                                   button_mods("KitButton", "Default", False, "BACK", small=True)))

    y = -566
    tiles = [("Squirrel", "SELECTED", True, False, "Squirrel"), ("Dolphin", "LOCKED / 4000", False, True, "Dolphin"),
             ("Manta", "LOCKED / 4000", False, True, "Manta")]
    for i, (vessel, status, sel, locked, icon) in enumerate(tiles):
        pal = tile_palette(sel, locked)
        mods = [("root#tile", "isSelected", 1 if sel else 0, None), ("root#tile", "isLocked", 1 if locked else 0, None),
                ("Name#tmp", "m_text", vessel, None), ("Status#tmp", "m_text", status, None),
                ("Art#img", "m_Sprite", None, sprite_ref(VESSEL_ICON[icon]))]
        for part, key in (("Fill#img", "fill"), ("Border#img", "border"), ("Art#img", "art"), ("Lock#img", "lock")):
            for ch, v in zip("rgba", pal[key]):
                mods.append((part, f"m_Color.{ch}", v, None))
        for part, key in (("Name#tmp", "primary"), ("Status#tmp", "secondary")):
            for ch, v in zip("rgba", pal[key]):
                mods.append((part, f"m_fontColor.{ch}", v, None))
            mods.append((part, "m_fontColor32.rgba", col32(pal[key]), None))
        if sel:
            mods.append(("Border#img", "m_Sprite", None, sprite_ref(kit_sprite("kit_sliver14_line2"))))
        right.children.append(Instance(f"tile{i}", "KitTile", tile_guid, f"{vessel} Tile",
                                       rect((0, 1), None, (i * 296, y), (280, 96), (0, 1)), mods))

    y = -684
    for i, (section, sel) in enumerate((("GENERAL", True), ("DISPLAY", False), ("PERFORMANCE", False), ("OTHER", False))):
        pal = tile_palette(sel, False, unselected_primary="NeutralLightest")
        mods = [("root#tile", "isSelected", 1 if sel else 0, None), ("Label#tmp", "m_text", section, None)]
        for part, key in (("Fill#img", "fill"), ("Border#img", "border"), ("Icon#img", "primary")):
            for ch, v in zip("rgba", pal[key]):
                mods.append((part, f"m_Color.{ch}", v, None))
        for ch, v in zip("rgba", pal["primary"]):
            mods.append(("Label#tmp", f"m_fontColor.{ch}", v, None))
        mods.append(("Label#tmp", "m_fontColor32.rgba", col32(pal["primary"]), None))
        right.children.append(Instance(f"hex{i}", "KitHexNavTile", prefab_guid("KitHexNavTile"), f"{section.title()} Hex",
                                       rect((0, 1), None, (i * 128, y), (120, 108), (0, 1)), mods))
    x = 528
    for i, chip in enumerate(("Solo", "Social", "Casual")):
        w = label_width(chip, "BodySmall") + 24
        right.children.append(Instance(f"chip{i}", "KitChip", prefab_guid("KitChip"), f"{chip} Chip",
                                       rect((0, 1), None, (x, y), (w, line_height("BodySmall") + 10), (0, 1)),
                                       [("Label#tmp", "m_text", chip, None)]))
        x += w + 8
    right.children.append(Instance("pill", "KitCurrencyPill", prefab_guid("KitCurrencyPill"), "Currency",
                                   rect((0, 1), None, (528, y - 48), (label_width("4000", "Data") + 60, line_height("Data") + 16), (0, 1)),
                                   [("Amount#tmp", "m_text", "4000", None)]))

    y = -812
    right.children.append(Instance("panel", "KitPanel", prefab_guid("KitPanel"), "Panel",
                                   rect((0, 1), None, (0, y), (420, 196), (0, 1))))
    right.children.append(Instance("popup", "KitPopup", prefab_guid("KitPopup"), "Popup",
                                   rect((0, 1), None, (440, y), (468, 170), (0, 1)),
                                   [("Content/Eyebrow#tmp", "m_text", "SPEND", None),
                                    ("Content/Title#tmp", "m_text", "4000 to unlock Manta", None),
                                    ("Content/Body#tmp", "m_text", "Your balance stays in the bar above.", None)]))

    ctx = Ctx("UIKitTestScene")
    out = []
    emit_node(ctx, canvas, 0, out)
    eg, et = ctx.fid("EventSystem#go"), ctx.fid("EventSystem#tr")
    e1, e2 = ctx.fid("EventSystem#es"), ctx.fid("EventSystem#im")
    out.append(f"--- !u!1 &{eg}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
               "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n"
               f"  m_Component:\n  - component: {{fileID: {et}}}\n  - component: {{fileID: {e1}}}\n"
               f"  - component: {{fileID: {e2}}}\n  m_Layer: 0\n  m_Name: EventSystem\n  m_TagString: Untagged\n"
               "  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n")
    out.append(f"--- !u!4 &{et}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
               f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {eg}}}\n"
               "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
               "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n"
               "  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")
    out.append(f"--- !u!114 &{e1}\n" + mono(eg, EVENT_SYSTEM, "  m_FirstSelected: {fileID: 0}\n"
                                                            "  m_sendNavigationEvents: 1\n  m_DragThreshold: 10\n"))
    acts = [("m_ActionsAsset", -944628639613478452), ("m_PointAction", -1654692200621890270),
            ("m_MoveAction", -8784545083839296357), ("m_SubmitAction", 392368643174621059),
            ("m_CancelAction", 7727032971491509709), ("m_LeftClickAction", 3001919216989983466),
            ("m_MiddleClickAction", -2185481485913320682), ("m_RightClickAction", -4090225696740746782),
            ("m_ScrollWheelAction", 6240969308177333660), ("m_TrackedDevicePositionAction", 6564999863303420839),
            ("m_TrackedDeviceOrientationAction", 7970375526676320489)]
    out.append(f"--- !u!114 &{e2}\n" + mono(eg, INPUT_MODULE,
               "  m_SendPointerHoverToParent: 1\n  m_MoveRepeatDelay: 0.5\n  m_MoveRepeatRate: 0.1\n"
               "  m_XRTrackingOrigin: {fileID: 0}\n"
               + "".join(f"  {k}: {ref(v, INPUT_ACTIONS)}\n" for k, v in acts)
               + "  m_DeselectOnBackgroundClick: 1\n  m_PointerBehavior: 0\n  m_CursorLockBehavior: 0\n"
               "  m_ScrollDeltaPerTick: 6\n"))
    return HEADER + scene_settings() + "".join(out)


# --------------------------------------------------------------------------------------------
# Metas, validation, main
# --------------------------------------------------------------------------------------------

PREFAB_META = """fileFormatVersion: 2
guid: {g}
PrefabImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
ASSET_META = """fileFormatVersion: 2
guid: {g}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""
SCENE_META = """fileFormatVersion: 2
guid: {g}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
FOLDER_META = """fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def outputs():
    out = {}
    for name in PREFABS:
        out[f"{PREFAB_DIR}/{name}.prefab"] = prefab_text(name)
        out[f"{PREFAB_DIR}/{name}.prefab.meta"] = PREFAB_META.format(g=prefab_guid(name))
    out[f"{PREFAB_DIR}/KitButtonSmall.prefab"] = variant_text()
    out[f"{PREFAB_DIR}/KitButtonSmall.prefab.meta"] = PREFAB_META.format(g=prefab_guid("KitButtonSmall"))
    out[f"{PREFAB_DIR}.meta"] = FOLDER_META.format(g=guid_for("folder/Kit"))
    out[ROLES_ASSET] = roles_asset_text()
    out[ROLES_ASSET + ".meta"] = ASSET_META.format(g=guid_for("UITypeRoles"))
    out[SCENE] = build_scene()
    out[SCENE + ".meta"] = SCENE_META.format(g=guid_for("scene/UIKitTestScene"))
    return out


def validate(out):
    # Rev 6: the elements are the colours of meaning - Play is Charge, a spend is Mass, people Space,
    # progress Time (Docs/MENU_REDESIGN_B.md §45). KitButton.PaletteFor mirrors this table.
    for variant, token in (("Play", "ElementCharge"), ("Purchase", "ElementMass"), ("Space", "ElementSpace"),
                           ("Time", "ElementTime")):
        assert button_palette(variant, True)[1] == tok(token), f"the {variant} button is not drawn in {token}"
    """Every local fileID resolves; anchors are unique; every referenced guid has an owner."""
    # Rev 7: a selection is the glowing sky blue everywhere (Docs/MENU_REDESIGN_B.md §47.1, the owner 2026-10-17).
    assert tok("Selection")[:3] == tuple(int("3DB2FF"[i:i + 2], 16) / 255 for i in (0, 2, 4)), "Selection is not sky blue"
    assert tile_palette(True, False)["border"] == tok("Selection"), "a selected tile's border is not the Selection blue"
    tile = out[f"{PREFAB_DIR}/KitTile.prefab"]
    assert f"  selectedBorderToken: {TOKEN_INDEX['Selection']}\n" in tile, "KitTile's selected border is not Selection"
    nav = out[f"{PREFAB_DIR}/KitTopNavTab.prefab"]
    bar_mat = hashlib.md5(b"CosmicShore/MenuB/lattice/selection_bar_mat").hexdigest()
    assert f"guid: {bar_mat}, type: 2}}" in nav, "the nav underline lost the selection light (SelectionBar.mat)"
    assert nav.count("guid: b403ee3874d54f79d0f27e7013879c0f, type: 3}") == 1, \
        "the nav underline has no KitRimMesh (the selection light would sweep the wrong UVs)"
    known_guids = set(PACKAGE_GUIDS)
    for root, _dirs, names in os.walk(os.path.join(REPO, "Assets")):
        for n in names:
            if n.endswith(".meta"):
                with open(os.path.join(root, n), errors="ignore") as fh:
                    m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(400), re.M)
                if m:
                    known_guids.add(m.group(1))
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1): p for p, t in out.items() if p.endswith(".meta")}
    for g, p in minted.items():
        owners = [q for q in minted if q == g]
        assert len(owners) == 1
    for path, text in out.items():
        if path.endswith(".meta"):
            continue
        anchors = re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M)
        dupes = sorted({a for a in anchors if anchors.count(a) > 1})
        assert not dupes, f"{path}: duplicate fileID anchors {dupes[:5]}"
        aset = set(anchors) | {"0", "11400000"}
        for m in re.finditer(r"\{fileID: (-?\d+)\}", text):
            assert m.group(1) in aset, f"{path}: dangling local fileID {m.group(1)}"
        for g in set(re.findall(r"guid: ([0-9a-f]{32})", text)):
            assert g in known_guids or g in minted, f"{path}: guid {g} resolves to no asset"
        assert text.isascii(), f"{path}: non-ASCII text (the UI font has 97 glyphs)"
        # Every kit label shrinks to fit its rect (KitText.shrinkToFit, 2026-10-17: labels spilled out of their
        # panels in Menu_Main while auto-size was off).
        for m in re.finditer(r"  m_enableAutoSizing: (\d)\n  m_fontSizeMin: ([\d.]+)\n  m_fontSizeMax: ([\d.]+)\n", text):
            assert m.group(1) == "1" and float(m.group(2)) < float(m.group(3)) or float(m.group(3)) <= 8, \
                f"{path}: a kit label does not auto-size (it would spill out of its rect)"
    # §64: the pad reaches the tab bar - each tab is a Selectable with a focus ring, and its own onClick stays empty
    # (the host's EventTrigger presses it; a wired onClick would press it twice per click).
    nav = out[f"{PREFAB_DIR}/KitTopNavTab.prefab"]
    tab_doc = re.search(r"^--- !u!114 &-?\d+\n(?:(?!^--- ).*\n)*?  m_Script: \{fileID: 11500000, guid: "
                        + kit_script("KitTopNavTab") + r", type: 3\}\n(?:(?!^--- ).*\n)*", nav, re.M)
    assert tab_doc, "the nav tab is not a pad stop (no KitTopNavTab Selectable on its root)"
    assert "    m_Mode: 3\n" in tab_doc.group(0), "the nav tab is out of pad navigation"
    assert re.search(r"  focusRing: \{fileID: [1-9]\d*\}\n", tab_doc.group(0)), "the nav tab has no focus ring"
    assert "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n" in tab_doc.group(0), \
        "the nav tab's onClick is wired (a click would press it twice: the host's EventTrigger already does)"


def unity_empty_scalars(text):
    """Unity writes `key: ` (trailing space) for an EMPTY scalar - see author_ui_kit_sprites.py."""
    lines = text.split("\n")
    out = []
    for i, line in enumerate(lines):
        if re.match(r"^\s*[^\s#][^:]*:$", line) and not line.lstrip().startswith("- "):
            indent = len(line) - len(line.lstrip())
            nxt = lines[i + 1] if i + 1 < len(lines) else ""
            n_indent = len(nxt) - len(nxt.lstrip())
            parent = nxt.strip() != "" and (n_indent > indent or
                                            (n_indent == indent and nxt.lstrip().startswith("- ")))
            if not parent:
                line += " "
        out.append(line)
    return "\n".join(out)


def self_test(out):
    """Planted defects the validation must catch (the Rev 7 selection look)."""
    nav, tile = f"{PREFAB_DIR}/KitTopNavTab.prefab", f"{PREFAB_DIR}/KitTile.prefab"
    bar_mat = hashlib.md5(b"CosmicShore/MenuB/lattice/selection_bar_mat").hexdigest()
    plants = {
        "the nav underline loses the selection light": (nav, lambda t: t.replace(
            f"  m_Material: {{fileID: 2100000, guid: {bar_mat}, type: 2}}\n", "  m_Material: {fileID: 0}\n", 1)),
        "the selected tile border falls back to the green CTA": (tile, lambda t: t.replace(
            f"  selectedBorderToken: {TOKEN_INDEX['Selection']}\n", f"  selectedBorderToken: {TOKEN_INDEX['Cta']}\n", 1)),
        "the nav tab is no pad stop": (nav, lambda t: t.replace(kit_script("KitTopNavTab"), kit_script("KitHoverTint"), 1)),
        "the nav tab loses its focus ring": (nav, lambda t: re.sub(r"  focusRing: \{fileID: -?\d+\}\n",
                                                                   "  focusRing: {fileID: 0}\n", t, count=1)),
    }
    ok = True
    for label, (path, plant) in plants.items():
        bad = dict(out)
        bad[path] = plant(out[path])
        assert bad[path] != out[path], f"plant did not apply: {label}"
        try:
            validate(bad)
            print(f"  MISSED: {label}")
            ok = False
        except AssertionError as e:
            print(f"  caught: {label} ({e})")
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    out = {p: unity_empty_scalars(t) for p, t in outputs().items()}
    validate(out)
    if args.self_test:
        return 0 if self_test(out) else 1

    if args.check:
        drift = [p for p, t in out.items()
                 if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p)).read() != t]
        for p in drift:
            print("DRIFT:", p)
        print(f"FAIL: {len(drift)} file(s) differ" if drift else f"OK: {len(out)} kit files match the generator")
        return 1 if drift else 0

    os.makedirs(os.path.join(REPO, PREFAB_DIR), exist_ok=True)
    for p, t in out.items():
        with open(os.path.join(REPO, p), "w", newline="\n") as fh:
            fh.write(t)
    print(f"wrote {len(out)} files")
    for p in sorted(out):
        if not p.endswith(".meta"):
            print("  ", p, f"({out[p].count(chr(10) + '--- ')} documents)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
