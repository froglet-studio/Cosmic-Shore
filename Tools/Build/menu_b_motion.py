#!/usr/bin/env python3
"""
Revision-2 building blocks for the style-B menu generators (Docs/MENU_REDESIGN_B.md section 13.4).

Every item generator (author_menu_*.py) emits the SAME motion and surface vocabulary through this
module, so a screen built in one item moves and reads like a screen built in another:

    entrance(...)      KitEntrance body: rise / drop / pop / grow, the panel or std token, a stagger slot
    ambient(...)       KitAmbient body: float / breathe / spin / sway / pan (the separable ambient layer)
    spark_body(...)    KitSpark body: the travelling highlight, or a button sweep (pause > 0)
    shake() / drain()  KitShake / KitDrain bodies
    grid_node(...)     the living lattice backdrop (rev 6; Lattice_Panel.mat), window_lattice_node for windows
    bracket_nodes(...) four corner brackets (kit_bracket, turned by z rotation)
    spark_line(...)    a 2 px route line that clips a KitSpark streak (RectMask2D)

Library only - it writes nothing. `python3 Tools/Build/menu_b_motion.py --self-test` proves the bodies
carry the enum values the C# reads (KitEntranceMode / KitAmbientMode / KitEase, which must stay in step).
"""

import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_ui_kit_prefabs", os.path.join(HERE, "author_ui_kit_prefabs.py"))
K = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(K)

REPO = K.REPO
RECT_MASK_2D = "3312d7739989d2b4e91e6319e9a96d76"
CANVAS_GROUP = 225

# Mirrors of the C# enums (Assets/_Scripts/UI/Kit/KitEntrance.cs, KitAmbient.cs, KitMotion.cs).
ENTRANCE = {"Rise": 0, "Drop": 1, "Pop": 2, "GrowX": 3, "GrowY": 4, "Fade": 5}
AMBIENT = {"Float": 0, "Breathe": 1, "Spin": 2, "Sway": 3, "Pan": 4}
EASE = {"OutQuint": 0, "OutCubic": 1, "OutBack": 2, "Linear": 3, "InOutSine": 4}
PANEL, STD, CEREMONY = 0.32, 0.2, 0.5


def entrance(mode="Rise", stagger=0, delay=0.0, by_sibling=False, ease=None, duration=None, distance=14,
             play_on_enable=True):
    """KitEntrance. Rise/Drop/Fade default to the panel token + OutQuint, GrowX/GrowY to std + OutCubic,
    Pop to the panel token + OutBack. A node carrying it should also carry its own CanvasGroup."""
    if ease is None:
        ease = {"Pop": "OutBack", "GrowX": "OutCubic", "GrowY": "OutCubic"}.get(mode, "OutQuint")
    if duration is None:
        duration = STD if mode in ("GrowX", "GrowY") else PANEL
    return lambda ctx, go: K.mono(go, K.kit_script("KitEntrance"),
                                  f"  mode: {ENTRANCE[mode]}\n  ease: {EASE[ease]}\n  duration: {K.f32(duration)}\n"
                                  f"  distance: {K.f32(distance)}\n  delay: {K.f32(delay)}\n  staggerIndex: {stagger}\n"
                                  f"  staggerBySibling: {1 if by_sibling else 0}\n  playOnEnable: {1 if play_on_enable else 0}\n")


KIT_ENTRANCE = K.kit_script("KitEntrance")


def require_entrances(files, wants):
    """The motion pass's floor (Docs/MENU_REDESIGN_B.md section 22.8): {prefab path: minimum KitEntrance
    count}. Run it FIRST in a generator's validate, so a planted strip is caught by this message."""
    for path, n in wants.items():
        got = files[path].count(f"guid: {KIT_ENTRANCE}")
        assert got >= n, f"{path}: {got} KitEntrance(s), the motion pass authors {n} (section 22.8)"


def strip_entrances(text):
    """Negative control for require_entrances: every KitEntrance becomes a KitAmbient (a known script)."""
    return text.replace(f"guid: {KIT_ENTRANCE}", f"guid: {K.kit_script('KitAmbient')}")


def ambient(mode="Float", period=3.2, amplitude=6.0, phase=0.0):
    return lambda ctx, go: K.mono(go, K.kit_script("KitAmbient"),
                                  f"  mode: {AMBIENT[mode]}\n  period: {K.f32(period)}\n"
                                  f"  amplitude: {K.f32(amplitude)}\n  phase: {K.f32(phase)}\n")


def spark_body(vertical=False, period=1.8, pause=0.0, delay=0.4):
    return lambda ctx, go: K.mono(go, K.kit_script("KitSpark"),
                                  f"  vertical: {1 if vertical else 0}\n  period: {K.f32(period)}\n"
                                  f"  pause: {K.f32(pause)}\n  delay: {K.f32(delay)}\n")


def shake(amplitude=6.0, duration=0.24):
    return lambda ctx, go: K.mono(go, K.kit_script("KitShake"),
                                  f"  amplitude: {K.f32(amplitude)}\n  duration: {K.f32(duration)}\n")


def drain(duration=5.0, play_on_enable=True):
    return lambda ctx, go: K.mono(go, K.kit_script("KitDrain"),
                                  f"  duration: {K.f32(duration)}\n  playOnEnable: {1 if play_on_enable else 0}\n")


def canvas_group(alpha=1.0, interactable=True, blocks=True):
    return lambda ctx, go: ("CanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Alpha: {K.f32(alpha)}\n"
                            f"  m_Interactable: {1 if interactable else 0}\n  m_BlocksRaycasts: {1 if blocks else 0}\n"
                            "  m_IgnoreParentGroups: 0\n")


def rect_mask():
    return lambda ctx, go: K.mono(go, RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n")


def tiled_image(color, sprite):
    """An Image in Tiled mode (m_Type 2) - the blueprint grid."""
    def body(ctx, go):
        return K.mono(go, K.IMAGE,
                      "  m_Material: {fileID: 0}\n"
                      f"  m_Color: {K.col(color)}\n  m_RaycastTarget: 0\n"
                      "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
                      "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"
                      f"  m_Sprite: {K.sprite_ref(sprite)}\n  m_Type: 2\n  m_PreserveAspect: 0\n"
                      "  m_FillCenter: 1\n  m_FillMethod: 4\n  m_FillAmount: 1\n  m_FillClockwise: 1\n"
                      "  m_FillOrigin: 0\n  m_UseSpriteMesh: 0\n  m_PixelsPerUnitMultiplier: 1\n")
    return body


def _lattice_guid(key):
    """author_ui_lattice.py's deterministic material guids."""
    import hashlib
    return hashlib.md5(f"CosmicShore/MenuB/lattice/{key}".encode()).hexdigest()


LATTICE_PANEL_MAT = _lattice_guid("panel_mat")
LATTICE_WINDOW_MAT = _lattice_guid("window_mat")
SEA_WINDOW_MAT = _lattice_guid("sea_window_mat")


def lattice_image(material_guid):
    """A sprite-less Image (its UVs span the rect - the lattice's edge fade and reveal read them) drawn with a
    lattice material. White tint: the material carries the colours; the vertex alpha grows it in."""
    base = K.image((1.0, 1.0, 1.0, 1.0))
    return lambda ctx, go: base(ctx, go).replace(
        "  m_Material: {fileID: 0}\n", f"  m_Material: {{fileID: 2100000, guid: {material_guid}, type: 2}}\n", 1)


# Rev 6 (§45.4): the presence orb's and the party bonds' materials (author_ui_lattice.py).
PRESENCE_MAT = _lattice_guid("presence_mat")
BOND_MAT = _lattice_guid("bond_mat")


def material_image(color, material_guid, raycast=False):
    """A sprite-less Image drawn with one of the generated UI materials, tinted `color` (vertex colour)."""
    base = K.image(color, raycast=raycast)
    return lambda ctx, go: base(ctx, go).replace(
        "  m_Material: {fileID: 0}\n", f"  m_Material: {{fileID: 2100000, guid: {material_guid}, type: 2}}\n", 1)


RIM_SCRIPT = "b403ee3874d54f79d0f27e7013879c0f"      # KitRimMesh.cs


def rim_material(element):
    """The guid author_ui_lattice.py's ElementRim_<element>.mat had (Charge / Mass / Space / Time / Omni). The element
    rims were RETIRED 2026-10-24 (Docs/UI_COLOR_RULES.md: elements carry no colour in the UI, and a light running round a
    panel nobody selected read as the selection); the guids are kept so the gates can catch a stray reference."""
    return _lattice_guid(f"rim_{element.lower()}_mat")


RETIRED_RIM_GUIDS = tuple(rim_material(e) for e in ("Charge", "Mass", "Space", "Time", "Omni"))


def find_node(node, key):
    if getattr(node, "key", None) == key:
        return node
    for c in getattr(node, "children", []):
        hit = find_node(c, key)
        if hit:
            return hit
    return None


def grid_node(key, name="Lattice"):
    """Rev 6 (Docs/MENU_REDESIGN_B.md §45.2): the living crystal lattice that replaced the blueprint grid
    (kit_grid). Full-stretch; put it first under a panel's fill so content draws over it."""
    n = K.Node(key, name, K.STRETCH)
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, lattice_image(LATTICE_PANEL_MAT))
    return n


def window_lattice_node(key, name="Lattice", rect=None):
    """The two-layer lattice for a whole window's backdrop (Lattice_Window.mat)."""
    n = K.Node(key, name, rect or K.STRETCH)
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, lattice_image(LATTICE_WINDOW_MAT))
    return n


def window_sea_node(key, name="SeaLines", rect=None):
    """The sea lines behind a whole window (§63, Sea_Window.mat): slow wave lines in three depth layers over the
    window's veil. Put it right after the veil so the window's content draws over it."""
    n = K.Node(key, name, rect or K.STRETCH)
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, lattice_image(SEA_WINDOW_MAT))
    return n


BRACKET = 22


def bracket_nodes(parent, key, inset=10, token="SurfaceLight"):
    """Four corner brackets: one kit_bracket sprite (a top-left L), turned for the other corners. The node
    emitter writes a z rotation from a node's `rot` (author_menu_arcade.emit_node)."""
    h = BRACKET / 2
    corners = (("tl", (0, 1), (inset + h, -inset - h), 0), ("tr", (1, 1), (-inset - h, -inset - h), -90),
               ("br", (1, 0), (-inset - h, inset + h), 180), ("bl", (0, 0), (inset + h, inset + h), 90))
    for ck, anchor, pos, deg in corners:
        n = K.Node(f"{key}.{ck}", f"Bracket {ck.upper()}", K.rect(anchor, anchor, pos, (BRACKET, BRACKET)))
        n.rot = deg
        n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.tok(token), K.kit_sprite("kit_bracket"))) \
            .add("th", 114, K.themed(token))
        parent.child(n)


SPARK_LEN = 48


def spark_line(parent, key, name, r, vertical=False, token="TextLight", period=1.8, delay=0.4, grow_delay=0.0,
               spark=False):
    """A route line (Image, token colour) that draws in on the std token and clips a travelling spark."""
    line = K.Node(key, name, r)
    line.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.tok(token))).add("th", 114, K.themed(token)) \
        .add("mask", 114, rect_mask()).add("in", 114, entrance("GrowY" if vertical else "GrowX", delay=grow_delay))
    if spark:
        if vertical:
            sr = K.rect((0, 1), (1, 1), (0, SPARK_LEN), (0, SPARK_LEN), (0.5, 1))
        else:
            sr = K.rect((0, 0), (0, 1), (-SPARK_LEN, 0), (SPARK_LEN, 0), (0, 0.5))
        s = K.Node(key + ".spark", "Spark", sr)
        sprite = "kit_spark_v" if vertical else "kit_spark"
        s.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite(sprite))) \
            .add("sp", 114, spark_body(vertical, period, 0.0, delay + grow_delay))
        line.child(s)
    parent.child(line)
    return line


def self_test():
    """The enum mirrors must match the C# declarations they write into."""
    def enum_values(path, name):
        text = open(os.path.join(REPO, path), encoding="utf-8").read()
        body = re.search(r"enum " + name + r"\s*\{(.*?)\}", text, re.S).group(1)
        return {m.group(1): int(m.group(2)) for m in re.finditer(r"(\w+)\s*=\s*(\d+)", body)}
    checks = [("Assets/_Scripts/UI/Kit/KitEntrance.cs", "KitEntranceMode", ENTRANCE),
              ("Assets/_Scripts/UI/Kit/KitAmbient.cs", "KitAmbientMode", AMBIENT),
              ("Assets/_Scripts/UI/Kit/KitMotion.cs", "KitEase", EASE)]
    ok = True
    for path, name, mirror in checks:
        have = enum_values(path, name)
        if have != mirror:
            ok = False
            print(f"MISMATCH {name}: C# {have} vs generator {mirror}")
    # negative control: a drifted mirror must be caught
    bad = dict(ENTRANCE, Pop=9)
    caught = enum_values(checks[0][0], checks[0][1]) != bad
    print("  caught: a drifted KitEntranceMode mirror" if caught else "  MISSED: drifted mirror")
    body = entrance("Pop", stagger=3)(None, "1")
    ok &= "mode: 2" in body and "ease: 2" in body and "staggerIndex: 3" in body and caught
    print("OK: motion mirrors match the C# enums" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    print(__doc__)
