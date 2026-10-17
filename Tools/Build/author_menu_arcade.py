#!/usr/bin/env python3
"""
Author ITEM 3 of the style-B menu redesign - the ARCADE and the SHARED PARTY PANEL - as prefabs,
and wire them into Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 12; the approved Arcade boards
on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Party/        shared by every mode window (D7)
        PartySlot.prefab        one seat: KitPartySlot (FriendInfoSlot) - the framed occupied row
                                (domain ring, avatar, name, role, kick) over the "Open slot" tile
        PartyRequestRow.prefab  one party invite / friend request: KitRequestRow (RequestInfoEntry)
        PartyOnlineRow.prefab   one online pilot: KitOnlineRow (OnlineInfoEntry) - Invite, Join /
                                Spectate, and the cancel / kick icon button
        PartyPanel.prefab       the unified panel: ArcadeLobbyList + FriendsListPanel on one root,
                                four seats, Leave, then the INVITES and ONLINE lists in one scroll
    Assets/_Prefabs/UI Elements/Main Menu Screens/Arcade/
        ArcadeGameCard.prefab   one grid card: KitGameCard (GameCard) on a KitTile
        ArcadeView.prefab       the whole window body: nav dim, backdrop, header with Back (B), the
                                party panel, and the explore scroll - weekly challenge and Maelstrom
                                feature cards, ALL GAMES, a 7 x 4 grid of cards

    Menu_Main.unity, `ModalWindows/ArcadeScreenModal`:
        The old Header, GameSelectScrollView, ArcadeLobbyList, CloseButton and FriendListPanel are
        deleted; one ArcadeView instance goes under `Arcade_Panel/Explore`, whose ArcadeExploreView is KEPT (four
        scene objects reference the view) and re-pointed into the instance. The window root stretches full screen (the view draws its own nav dim), the panel's
        old gradient Image is switched off, the two OfflineUIGates gate the new party panel, and the
        FTUE QuestGraphRunner's gameCards list names the 35 cards (7 rows of 5).

Built from the kit generator's node model and component bodies (author_ui_kit_prefabs.py), so every
body is Unity's serialization and every colour a UIThemeSO token. References into nested instances
are stripped stubs whose fileID is Unity's derived id (source XOR instance, applied per level).

STANDING DOWN. The scene migration (deleting the old objects) runs only while they exist; after that
the script re-emits the documents it owns, and validates the wiring of the scene AS COMMITTED on
every run, --check included.

    python3 Tools/Build/author_menu_arcade.py              # write
    python3 Tools/Build/author_menu_arcade.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_arcade.py --self-test  # planted defects must all be caught
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
H = _load("author_menu_hangar")         # text / label / le / canvas_group / themed helpers
M = _load("menu_b_motion")              # revision-2 motion (entrances)

REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
PARTY_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Party"
ARCADE_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Arcade"
MASK63 = 0x7FFFFFFFFFFFFFFF

# --- package script guids, harvested from Menu_Main (never recalled) --------------------------
SCROLL_RECT, RECT_MASK_2D, MASK = H.SCROLL_RECT, H.RECT_MASK_2D, H.MASK
BUTTON = T.BUTTON
PACKAGE = H.PACKAGE | {BUTTON}

# --- project scripts ----------------------------------------------------------------------------
def script(path):
    return K.meta_guid(path)


S = dict(
    lobby=script("Assets/_Scripts/UI/Elements/ArcadeLobbyList.cs"),
    friends=script("Assets/_Scripts/UI/Elements/FriendsListPanel.cs"),
    slot=script("Assets/_Scripts/UI/Elements/Party/KitPartySlot.cs"),
    online=script("Assets/_Scripts/UI/Elements/Party/KitOnlineRow.cs"),
    request=script("Assets/_Scripts/UI/Elements/Party/KitRequestRow.cs"),
    card=script("Assets/_Scripts/UI/Elements/Arcade/KitGameCard.cs"),
    weekly=script("Assets/_Scripts/UI/Elements/WeeklyChallengeCard.cs"),
    audio=script("Assets/_Scripts/UI/MenuAudio.cs"),
    view=script("Assets/_Scripts/UI/Views/ArcadeExploreView.cs"),
    runner=script("Assets/FTUE/Scripts/Graph/Runtime/QuestGraphRunner.cs"),
    gate=script("Assets/_Scripts/UI/Elements/OfflineUIGate.cs"),
    close=script("Assets/_Scripts/UI/Modals/ModalWindowCloseButton.cs"),
    scroll_into=K.kit_script("KitScrollIntoView"),
    counter=K.kit_script("KitListCounter"),
    keeper=K.kit_script("KitFocusKeeper"),
    hover=K.kit_script("KitHoverTint"),
    cover=K.kit_script("KitCoverImage"),
    kt=K.kit_script("KitText"),
    th=K.kit_script("KitThemedGraphic"),
    button=K.kit_script("KitButton"),
    tile=K.kit_script("KitTile"),
)

# --- data assets (guids read off the shipped wiring they replace) -------------------------------
CONNECTION_DATA = "69a7bf449c11416dac2d81ebd3ff61f5"     # HostConnectionDataSO
FRIENDS_DATA = "e69f5c7256384d1cb59d57e12551c123"        # FriendsDataSO
PROFILE_ICONS = "3e6cb5b838253c7468480627fd86af5f"       # SO_DefaultProfileIcons
ALL_GAMES = "25a98002ea18f5d45b5e8b2098a06c57"           # OrganicRematchGames - the injected master list
VESSEL_LIST = "49c7c10ad2901c74589c28317a4eb17b"         # SO_VesselList
AVATAR_PLACEHOLDER = "cd2f05a67ee6cb34bb6887c9ba512f83"  # the first default profile icon
MAELSTROM_ASSET = "Assets/_SO_Assets/Games/ArcadeGameMaelstrom.asset"
WEEKLY_PLACEHOLDER_ASSET = "Assets/_SO_Assets/Games/ArcadeGameSkimRace.asset"

PARTY_PREFABS = ("PartySlot", "PartyRequestRow", "PartyOnlineRow", "PartyPanel")
ARCADE_PREFABS = ("ArcadeGameCard", "ArcadeView")
PREFABS = PARTY_PREFABS + ARCADE_PREFABS

# Rev 3: five across - the friends panel gave its 444 px back. 35 slots hold the roster without a clone,
# and every rev-2 card key (Card r_c, c <= 4) still exists, so no scene stub goes stale.
GRID_ROWS, GRID_COLS = 7, 5


def asset_guid(name):
    return hashlib.md5(f"CosmicShore/MenuB/assets/{name}".encode()).hexdigest()


def prefab_guid(name):
    return asset_guid(name) if name in PREFABS else K.prefab_guid(name)


def prefab_path(name):
    return f"{PARTY_DIR if name in PARTY_PREFABS else ARCADE_DIR}/{name}.prefab"


def derived(src, pid):
    """Unity's fileID for an object of a nested instance / variant base: source XOR instance."""
    return (int(src) ^ int(pid)) & MASK63


def ref(fid):
    return "{fileID: %s}" % fid


def asset_field(path, field):
    """One top-level field of a ScriptableObject asset, continuation lines joined."""
    text = open(os.path.join(REPO, path), encoding="utf-8").read()
    m = re.search(r"^  " + field + r":(.*?)(?=^  \w+:)", text, re.M | re.S)
    assert m, f"{path}: no {field}"
    return " ".join(line.strip() for line in m.group(1).strip().split("\n"))


def sprite_guid(path, field):
    m = re.search(r"guid: ([0-9a-f]{32})", asset_field(path, field))
    assert m, f"{path}.{field} names no sprite"
    return m.group(1)


# --------------------------------------------------------------------------------------------
# Node model extensions: rotation, instances with ADDED components, stub-aware context
# --------------------------------------------------------------------------------------------

N = K.Node
STRETCH = K.STRETCH
R = K.rect
tok = K.tok
text, label, le, canvas_group, themed = H.text, H.label, H.le, H.canvas_group, H.themed


class Inst(K.Instance):
    """A nested prefab instance that may carry components added to its root."""

    def __init__(self, key, prefab_key, name, r, mods=(), added=()):
        super().__init__(key, prefab_key, prefab_guid(prefab_key), name, r, mods)
        self.added = list(added)       # (comp_key, class_id, body(ctx, go))


STUB_KIND = {"go": (1, None), "rt": (224, None), "tmp": (114, K.TMP_UGUI), "img": (114, K.IMAGE),
             "kt": (114, S["kt"]), "th": (114, S["th"]), "button": (114, S["button"]),
             "tile": (114, S["tile"]), "slot": (114, S["slot"]), "card": (114, S["card"]),
             "online": (114, S["online"]), "request": (114, S["request"]), "cg": (225, None)}


def stub_doc(fid, comp, src, guid, pid):
    cls, scr = STUB_KIND[comp]
    if cls == 1:
        return T.stub_go(fid, src, guid, pid)
    if cls == 114:
        return T.stub_mono(fid, src, guid, pid, scr)
    name = {224: "RectTransform", 225: "CanvasGroup"}[cls]
    return (f"--- !u!{cls} &{fid} stripped\n{name}:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
            f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


def src_fid(prefab_key, key):
    """The fileID of `key` in a prefab this script or the kit generator authors."""
    return K.source_fid(prefab_key, key)


class PCtx(K.Ctx):
    def __init__(self, file_key):
        super().__init__(file_key)
        self.instances = {}
        self.stubs = {}

    def register(self, node):
        for ch in node.children:
            if is_instance(ch):
                self.instances[ch.key] = ch.prefab_key
            else:
                self.register(ch)

    def iref(self, inst_key, src_key):
        prefab = self.instances[inst_key]
        src = src_fid(prefab, src_key)
        pid = self.fid(inst_key + "#inst")
        fid = derived(src, pid)
        self.stubs.setdefault(fid, stub_doc(fid, src_key.split("#")[1], src, prefab_guid(prefab), pid))
        return fid


def is_instance(x):
    """Duck-typed: the hangar helpers build nodes from their own copy of the kit module."""
    return hasattr(x, "prefab_key")


def rot_z(deg):
    h = math.radians(deg) / 2
    return "{x: 0, y: 0, z: %s, w: %s}" % (K.f32(math.sin(h)), K.f32(math.cos(h)))


def emit_node(ctx, node, parent_rt, out):
    """K.emit_node, plus a z rotation and instances with added components."""
    go, rt = ctx.go(node.key), ctx.rt(node.key)
    comp_ids = [rt] + [ctx.c(node.key, k) for k, _, _ in node.comps]
    out.append(f"--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n"
               "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
               "  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n"
               + "".join(f"  - component: {{fileID: {c}}}\n" for c in comp_ids)
               + f"  m_Layer: 5\n  m_Name: {node.name}\n  m_TagString: Untagged\n"
               "  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n"
               f"  m_IsActive: {1 if node.active else 0}\n")
    kids = [ctx.fid(ch.key + "#stub") if is_instance(ch) else ctx.rt(ch.key) for ch in node.children]
    r = node.rect
    deg = getattr(node, "rot", 0)
    children = ("  m_Children:\n" + "".join(f"  - {{fileID: {c}}}\n" for c in kids)) if kids else "  m_Children: []\n"
    out.append(f"--- !u!224 &{rt}\nRectTransform:\n  m_ObjectHideFlags: 0\n"
               "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
               f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
               f"  m_LocalRotation: {rot_z(deg) if deg else '{x: 0, y: 0, z: 0, w: 1}'}\n"
               "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
               "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n" + children
               + f"  m_Father: {{fileID: {parent_rt}}}\n"
               f"  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: {K.f32(deg)}}}\n"
               f"  m_AnchorMin: {K.v2(*r['amin'])}\n  m_AnchorMax: {K.v2(*r['amax'])}\n"
               f"  m_AnchoredPosition: {K.v2(*r['pos'])}\n  m_SizeDelta: {K.v2(*r['size'])}\n"
               f"  m_Pivot: {K.v2(*r['pivot'])}\n")
    for k, class_id, body in node.comps:
        out.append(f"--- !u!{class_id} &{ctx.c(node.key, k)}\n" + body(ctx, go))
    for ch in node.children:
        if is_instance(ch):
            emit_instance(ctx, ch, rt, out)
        else:
            emit_node(ctx, ch, rt, out)


def emit_instance(ctx, inst, parent_rt, out):
    """K.emit_instance, with m_AddedComponents on the instance root."""
    tmp = []
    K.emit_instance(ctx, inst, parent_rt, tmp)
    doc = tmp[0]
    added = getattr(inst, "added", [])
    if added:
        root_go_src = src_fid(inst.prefab_key, "root#go")
        go_stub = ctx.iref(inst.key, "root#go")
        lines = "".join(f"    - targetCorrespondingSourceObject: {K.ref(root_go_src, inst.prefab_guid)}\n"
                        f"      insertIndex: -1\n      addedObject: {{fileID: {ctx.fid(inst.key + '#' + k)}}}\n"
                        for k, _, _ in added)
        doc = doc.replace("    m_AddedComponents: []\n", "    m_AddedComponents:\n" + lines)
        for k, cls, body in added:
            tmp.append(f"--- !u!{cls} &{ctx.fid(inst.key + '#' + k)}\n" + body(ctx, go_stub))
    out.append(doc)
    out.extend(tmp[1:])


def prefab_text(root, file_key):
    ctx = PCtx(file_key)
    ctx.register(root)
    out = []
    emit_node(ctx, root, 0, out)
    return K.unity_empty_scalars(K.HEADER + "".join(out) + "".join(ctx.stubs.values()))


# --------------------------------------------------------------------------------------------
# Component bodies
# --------------------------------------------------------------------------------------------

def mono(go, guid, fields):
    return K.mono(go, guid, fields)


def refs(name, ids):
    return K.refs_list(name, ids)


def calls(entries):
    """A persistent-call list: (target fileID, assembly type name, method)."""
    if not entries:
        return "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"
    body = ""
    for target, type_name, method in entries:
        body += (f"      - m_Target: {{fileID: {target}}}\n"
                 f"        m_TargetAssemblyTypeName: {type_name}, Assembly-CSharp\n"
                 f"        m_MethodName: {method}\n        m_Mode: 1\n        m_Arguments:\n"
                 "          m_ObjectArgument: {fileID: 0}\n"
                 "          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine\n"
                 "          m_IntArgument: 0\n          m_FloatArgument: 0\n          m_StringArgument: \n"
                 "          m_BoolArgument: 0\n        m_CallState: 2\n")
    return "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls:\n" + body


def selectable(nav_mode=3):
    return K.SELECTABLE_FIELDS.replace("    m_Mode: 3\n", f"    m_Mode: {nav_mode}\n")


def tile_body(ring, fill, border, sliver, primary=(), unsel_primary="TextLight", secondary=(), art=(),
              lock=None, lock_label=None, sel_border="TextLight", on_click=()):
    """KitTile, every reference a callable ctx -> fileID."""
    def body(ctx, go):
        f = lambda x: x(ctx)
        return mono(go, S["tile"],
                    selectable() + f"  m_TargetGraphic: {ref(f(fill))}\n" + calls([e(ctx) for e in on_click])
                    + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(f(ring))}\n"
                    "  isSelected: 0\n  isLocked: 0\n"
                    f"  fillImage: {ref(f(fill))}\n  borderImage: {ref(f(border))}\n"
                    f"  borderSelectedSprite: {K.sprite_ref(K.kit_sprite(f'kit_sliver{sliver}_line2'))}\n"
                    f"  borderUnselectedSprite: {K.sprite_ref(K.kit_sprite(f'kit_sliver{sliver}_line1'))}\n"
                    f"  selectedBorderToken: {K.TOKEN_INDEX[sel_border]}\n"
                    + refs("primaryGraphics", [f(x) for x in primary])
                    + f"  unselectedPrimaryToken: {K.TOKEN_INDEX[unsel_primary]}\n"
                    + refs("secondaryGraphics", [f(x) for x in secondary])
                    + f"  lockedSecondaryToken: {K.TOKEN_INDEX['Purchase']}\n"
                    + refs("artGraphics", [f(x) for x in art])
                    + f"  lockGraphic: {ref(f(lock) if lock else 0)}\n"
                    + f"  lockLabel: {ref(f(lock_label) if lock_label else 0)}\n")
    return body


def c(node_key, comp):
    return lambda ctx: ctx.c(node_key, comp)


def rt_of(node_key):
    return lambda ctx: ctx.rt(node_key)


def ir(inst_key, src_key):
    return lambda ctx: ctx.iref(inst_key, src_key)


def menu_audio(category):
    return lambda ctx, go: mono(go, S["audio"], f"  category: {category}\n")


def scroll_into(target=None, margin=12):
    return lambda ctx, go: mono(go, S["scroll_into"],
                                f"  target: {ref(target(ctx) if target else 0)}\n  margin: {K.f32(margin)}\n")


def mask(show):
    return lambda ctx, go: mono(go, MASK, f"  m_ShowMaskGraphic: {1 if show else 0}\n")


def rect_mask():
    return lambda ctx, go: mono(go, RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n")


def scroll_rect(content_key, viewport_key, sensitivity=30):
    return lambda ctx, go: mono(go, SCROLL_RECT,
                                f"  m_Content: {ref(ctx.rt(content_key))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
                                "  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
                                f"  m_ScrollSensitivity: {sensitivity}\n  m_Viewport: {ref(ctx.rt(viewport_key))}\n"
                                "  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n"
                                "  m_HorizontalScrollbarVisibility: 2\n  m_VerticalScrollbarVisibility: 2\n"
                                "  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n"
                                "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n")


def counter(count_kt, filled=(), empty=None):
    return lambda ctx, go: mono(go, S["counter"],
                                f"  countText: {ref(count_kt(ctx) if count_kt else 0)}\n"
                                + refs("shownWhileFilled", [x(ctx) for x in filled])
                                + f"  shownWhileEmpty: {ref(empty(ctx) if empty else 0)}\n")


def image_node(parent, key, name, r, color_token, sprite=None, sliced=False, alpha=1.0, raycast=False,
               preserve=False, layout=None):
    n = parent.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok(color_token, alpha), sprite, sliced, raycast, preserve)) \
        .add("th", 114, themed(color_token, alpha))
    if layout:
        n.add("le", 114, layout)
    return n


def art_node(parent, key, name, r, sprite, preserve=False, raycast=False):
    n = parent.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), sprite, raycast=raycast,
                                                                  preserve=preserve))
    return n


def ring_node(parent, key, sliver):
    """The CTA focus ring 5 units outside a control; KitSelectableBase fades it in."""
    n = parent.child(N(key, "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10))))
    n.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("Cta", 0.0), K.kit_sprite(f"kit_sliver{sliver}_line2"), True)) \
        .add("le", 114, le(ignore=True))
    return n


def frame_nodes(parent, key, sliver, fill_token, border_token, raycast=False, themed_paint=True):
    """Fill + border (kit-tinted when `themed_paint`, else left to a KitTile / KitButton)."""
    fill = parent.child(N(f"{key}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok(fill_token), K.kit_sprite(f"kit_sliver{sliver}_fill"), True, raycast))
    bord = parent.child(N(f"{key}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok(border_token), K.kit_sprite(f"kit_sliver{sliver}_line1"), True))
    if themed_paint:
        fill.add("th", 114, themed(fill_token))
        bord.add("th", 114, themed(border_token))
    fill.add("le", 114, le(ignore=True))
    bord.add("le", 114, le(ignore=True))
    return fill, bord


def ellipsis(body):
    def wrap(ctx, go):
        return body(ctx, go).replace("  m_overflowMode: 0\n", "  m_overflowMode: 1\n")
    return wrap


def label_e(node, t, role, token, driven=False, **kw):
    """A TMP + KitText pair that ellipsizes instead of overflowing."""
    node.add("cr", 222, K.canvas_renderer()).add("tmp", 114, ellipsis(text(t, role, tok(token), **kw))) \
        .add("kt", 114, K.kit_text(role, token, driven))
    return node


def small_button(key, name, variant, caption, r_anchor=(0, 0.5), pivot=(0, 0.5), icon=None, added=()):
    """A KitButtonSmall instance: a caption button, or icon-only (caption None)."""
    if caption is None:
        w = h = 32
        mods = K.button_mods("KitButtonSmall", variant, False, "", small=True)
        mods = [m for m in mods if m[0] != "Label#tmp" or m[1] != "m_text"]
        mods += [("Icon#go", "m_IsActive", 1, None), ("Label#go", "m_IsActive", 0, None),
                 ("Icon#img", "m_Sprite", None, K.sprite_ref(K.chrome_sprite(icon))),
                 ("root#hlg", "m_Padding.m_Left", 9, None), ("root#hlg", "m_Padding.m_Right", 9, None),
                 ("root#hlg", "m_Padding.m_Top", 9, None), ("root#hlg", "m_Padding.m_Bottom", 9, None)]
    else:
        w, h = K.button_size(caption, small=True)
        mods = K.button_mods("KitButtonSmall", variant, False, caption, small=True)
    return Inst(key, "KitButtonSmall", name, R(r_anchor, None, (0, 0), (w, h), pivot), mods, added)


def wrap_slot(parent, key, name, shown=True):
    """A CanvasGroup + LayoutElement wrapper sized by its one child: a part that hides and closes up."""
    n = parent.child(N(key, name, R((0, 0.5), None, (0, 0), (60, 32), (0, 0.5))))
    n.add("cg", 225, canvas_group(shown)).add("le", 114, le(ignore=not shown)) \
        .add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 0, 4, (0, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))
    return n


def avatar_nodes(parent, key, size, ring=False, x=0):
    """An avatar octagon: (ring, gap,) backing and the icon, centred on (x + size / 2, 0)."""
    outer = size + 6 if ring else size
    cx = x + outer / 2
    nodes = {}
    if ring:
        nodes["ring"] = image_node(parent, f"{key}/Ring", "DomainRing", R((0, 0.5), None, (cx, 0), (outer, outer)),
                                   "SurfaceDark", K.kit_sprite("kit_oct_fill"))
        image_node(parent, f"{key}/Gap", "RingGap", R((0, 0.5), None, (cx, 0), (size + 2, size + 2)),
                   "SurfaceVeryDark", K.kit_sprite("kit_oct_fill"))
    image_node(parent, f"{key}/Back", "AvatarBack", R((0, 0.5), None, (cx, 0), (size, size)),
               "SurfaceDark", K.kit_sprite("kit_oct_fill"))
    nodes["icon"] = art_node(parent, f"{key}/Icon", "Avatar", R((0, 0.5), None, (cx, 0), (size, size)),
                             AVATAR_PLACEHOLDER, preserve=True)
    return nodes, x + outer


# --------------------------------------------------------------------------------------------
# PartySlot.prefab
# --------------------------------------------------------------------------------------------

ROW_W = 376          # the party panel's inner width at 420 (22 + 22 padding)
SLOT_H, ROW_H = 58, 46


def build_slot():
    root = N("root", "PartySlot", R(size=(ROW_W, SLOT_H)))
    # Motion (section 22.8): the seats rise in one after another. The request and online rows are
    # not given one - RequestInfoEntry / OnlineInfoEntry already fade them in (entryFadeInSeconds).
    root.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    occ = root.child(N("Occupied", "Occupied", STRETCH)).add("cg", 225, canvas_group(False))
    frame_nodes(occ, "Occupied", 10, "SurfaceVeryDark", "SurfaceDark")
    _, x = avatar_nodes(occ, "Occupied/Avatar", 40, ring=True, x=12)
    label_e(occ.child(N("Occupied/Name", "Name", R((0, 0.5), (1, 0.5), (x + 12 + (ROW_W - x - 12 - 56) / 2 - ROW_W / 2, 10),
                                                    (-(x + 12 + 56), 24)))), "Pilot", "Body", "TextLight", align="left")
    label_e(occ.child(N("Occupied/Role", "Role", R((0, 0.5), (1, 0.5), (x + 12 + (ROW_W - x - 12 - 56) / 2 - ROW_W / 2, -13),
                                                    (-(x + 12 + 56), 18)))), "Pilot", "Label", "NeutralLightest", align="left")
    kick = occ.child(N("Occupied/Kick", "Kick", R((1, 0.5), None, (-12, 0), (32, 32), (1, 0.5))))
    kick.add("cg", 225, canvas_group(False))
    kick.children.append(small_button("KickButton", "KickButton", "Cancel", None, (0.5, 0.5), (0.5, 0.5),
                                      icon="chrome_close"))

    empty = root.child(N("Empty", "OpenSlot", STRETCH)).add("cg", 225, canvas_group(True))
    ring_node(empty, "Empty/FocusRing", 10)
    fill = empty.child(N("Empty/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bord = empty.child(N("Empty/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    image_node(empty, "Empty/PlusRing", "PlusRing", R((0, 0.5), None, (12 + 23, 0), (46, 46)),
               "SurfaceDark", K.kit_sprite("kit_oct_fill"))
    image_node(empty, "Empty/PlusBack", "PlusBack", R((0, 0.5), None, (12 + 23, 0), (44, 44)),
               "SurfaceBlack", K.kit_sprite("kit_oct_fill"))
    plus = empty.child(N("Empty/Plus", "Plus", R((0, 0.5), None, (12 + 23, 0), (22, 22))))
    plus.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_plus"), preserve=True))
    lab = empty.child(N("Empty/Label", "Label", R((0, 0.5), (1, 0.5), ((70 - 12) / 2, 0), (-(70 + 12), 22))))
    lab.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, ellipsis(text("Open slot - invite a friend below", "BodySmall", tok("NeutralLightest")))) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest", driven=True))
    empty.add("tile", 114, tile_body(c("Empty/FocusRing", "img"), c("Empty/Fill", "img"), c("Empty/Border", "img"), 10,
                                     primary=[c("Empty/Plus", "img"), c("Empty/Label", "tmp")],
                                     unsel_primary="NeutralLightest"))

    root.add("slot", 114, lambda ctx, go: mono(go, S["slot"],
             f"  avatarIcon: {ref(ctx.c('Occupied/Avatar/Icon', 'img'))}\n"
             f"  addButton: {ref(ctx.c('Empty', 'tile'))}\n"
             f"  displayNameText: {ref(ctx.c('Occupied/Name', 'tmp'))}\n"
             f"  kickButton: {ref(ctx.iref('KickButton', 'root#button'))}\n"
             "  domainGlow: {fileID: 0}\n"
             f"  occupiedGroup: {ref(ctx.c('Occupied', 'cg'))}\n"
             f"  emptyGroup: {ref(ctx.c('Empty', 'cg'))}\n"
             f"  kickGroup: {ref(ctx.c('Occupied/Kick', 'cg'))}\n"
             f"  roleText: {ref(ctx.c('Occupied/Role', 'kt'))}\n"
             f"  domainRing: {ref(ctx.c('Occupied/Avatar/Ring', 'img'))}\n"
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n"))
    root.add("le", 114, le(ph=SLOT_H, mw=0, fw=1))
    return root


# --------------------------------------------------------------------------------------------
# PartyRequestRow.prefab / PartyOnlineRow.prefab
# --------------------------------------------------------------------------------------------

def row_frame(name):
    root = N("root", name, R(size=(ROW_W, ROW_H)))
    _, x = avatar_nodes(root, "Avatar", 40)
    return root, x + 12


def text_columns(root, right, name_text, line_text, line_role_token, line_driven=False):
    """Name over a status line, between the avatar (52) and the buttons (`right` from the edge)."""
    left = 52
    cx = left + (ROW_W - left - right) / 2 - ROW_W / 2
    label_e(root.child(N("Name", "Name", R((0, 0.5), (1, 0.5), (cx, 10), (-(left + right), 22)))),
            name_text, "Body", "TextLight", align="left")
    label_e(root.child(N("Line", "Status", R((0, 0.5), (1, 0.5), (cx, -12), (-(left + right), 18)))),
            line_text, "Label", line_role_token, driven=line_driven, align="left")


def row_scroll_into():
    return ("scroll", 114, scroll_into(lambda ctx: ctx.rt("root")))


def build_request_row():
    root, _ = row_frame("PartyRequestRow")
    text_columns(root, 84, "Pilot", "PARTY INVITE", "ControlActive", line_driven=True)
    btns = root.child(N("Buttons", "Buttons", R((1, 0.5), None, (0, 0), (72, 32), (1, 0.5))))
    btns.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 5, (0, 0), (0, 0)))
    btns.children.append(small_button("Accept", "Accept", "Space", None, icon="chrome_confirm", added=[row_scroll_into()]))
    btns.children.append(small_button("Decline", "Decline", "Cancel", None, icon="chrome_close", added=[row_scroll_into()]))
    root.add("row", 114, lambda ctx, go: mono(go, S["request"],
             f"  avatarIcon: {ref(ctx.c('Avatar/Icon', 'img'))}\n"
             f"  usernameText: {ref(ctx.c('Name', 'tmp'))}\n"
             f"  labelText: {ref(ctx.c('Line', 'tmp'))}\n"
             f"  acceptButton: {ref(ctx.iref('Accept', 'root#button'))}\n"
             f"  declineButton: {ref(ctx.iref('Decline', 'root#button'))}\n"
             "  friendRequestColor: {r: 1, g: 1, b: 1, a: 1}\n"
             "  partyInviteColor: {r: 1, g: 0.85, b: 0.2, a: 1}\n"
             "  expiringSoonColor: {r: 0.9, g: 0.3, b: 0.2, a: 1}\n"
             "  entryFadeInSeconds: 0.25\n  buttonPressPunchSeconds: 0.18\n  buttonPressPunchScale: 1.15\n"
             f"  kindText: {ref(ctx.c('Line', 'kt'))}\n"
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n"))
    root.add("cg", 225, canvas_group(True)).add("le", 114, le(ph=ROW_H, mw=0, fw=1))
    return root


def build_online_row():
    root, _ = row_frame("PartyOnlineRow")
    # INVITE + JOIN is 162 wide and JOIN can read SPECTATE (202), so the strip and the text beside
    # it are sized for the widest pair. The strip CONTROLS its slots' widths (from their preferred
    # size): reading each slot's own rect instead took its 60-wide placeholder until the slot's
    # fitter caught up, which drew JOIN over INVITE.
    text_columns(root, 210, "Pilot", "ONLINE", "ElementSpace")
    btns = root.child(N("Buttons", "Buttons", R((1, 0.5), None, (0, 0), (202, 36), (1, 0.5))))
    btns.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 5, (1, 0), (0, 0)))
    for key, caption, variant, shown in (("Invite", "INVITE", "Space", True), ("Join", "JOIN", "Space", False)):
        slot = wrap_slot(btns, f"{key}Slot", f"{key}Slot", shown)
        slot.children.append(small_button(f"{key}Button", f"{key}Button", variant, caption, added=[row_scroll_into()]))
    cancel = wrap_slot(btns, "CancelSlot", "CancelSlot", False)
    cancel.children.append(small_button("CancelButton", "CancelButton", "Cancel", None, icon="chrome_close",
                                        added=[row_scroll_into()]))
    root.add("row", 114, lambda ctx, go: mono(go, S["online"],
             f"  avatarIcon: {ref(ctx.c('Avatar/Icon', 'img'))}\n"
             f"  usernameText: {ref(ctx.c('Name', 'tmp'))}\n"
             f"  labelText: {ref(ctx.c('Line', 'tmp'))}\n"
             "  backgroundImage: {fileID: 0}\n"
             f"  inviteButton: {ref(ctx.iref('InviteButton', 'root#button'))}\n"
             f"  cancelButton: {ref(ctx.iref('CancelButton', 'root#button'))}\n"
             f"  joinButton: {ref(ctx.iref('JoinButton', 'root#button'))}\n"
             "  joinButtonIcon: {fileID: 0}\n  joinSprite: {fileID: 0}\n  spectateSprite: {fileID: 0}\n"
             "  joinTint: {r: 0.4, g: 0.885, b: 1, a: 1}\n  spectateTint: {r: 1, g: 0.85, b: 0.25, a: 1}\n"
             "  joinDisabledAlpha: 0.35\n"
             "  onlineColor: {r: 1, g: 1, b: 1, a: 1}\n  inLobbyColor: {r: 0.4, g: 0.8, b: 1, a: 1}\n"
             "  inMatchColor: {r: 0.9, g: 0.2, b: 0.2, a: 1}\n  lobbyFullColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}\n"
             "  inYourPartyColor: {r: 0.6, g: 0.9, b: 0.6, a: 1}\n  defaultTint: {r: 1, g: 1, b: 1, a: 1}\n"
             "  pendingInviteTint: {r: 1, g: 0.85, b: 0.2, a: 1}\n"
             "  pendingInviteTintBright: {r: 1, g: 0.95, b: 0.5, a: 1}\n"
             "  disabledTint: {r: 0.35, g: 0.35, b: 0.35, a: 1}\n  pendingPulsePeriodSeconds: 1.1\n"
             "  pendingRequestLabel: PENDING REQUEST\n  entryFadeInSeconds: 0.25\n"
             "  invitePressPunchSeconds: 0.2\n  invitePressPunchScale: 1.08\n  actionCooldownSeconds: 0.4\n"
             f"  statusText: {ref(ctx.c('Line', 'kt'))}\n"
             f"  inviteLabel: {ref(ctx.iref('InviteButton', 'Label#kt'))}\n"
             f"  joinLabel: {ref(ctx.iref('JoinButton', 'Label#kt'))}\n"
             f"  inviteGroup: {ref(ctx.c('InviteSlot', 'cg'))}\n"
             f"  joinGroup: {ref(ctx.c('JoinSlot', 'cg'))}\n"
             f"  cancelGroup: {ref(ctx.c('CancelSlot', 'cg'))}\n"
             "  joinWord: Join\n  spectateWord: Spectate\n  inviteWord: Invite\n  invitedWord: Invited\n"))
    root.add("cg", 225, canvas_group(True)).add("le", 114, le(ph=ROW_H, mw=0, fw=1))
    return root


# --------------------------------------------------------------------------------------------
# PartyPanel.prefab
# --------------------------------------------------------------------------------------------

PANEL_W = 420
# The window's backdrop is glass, not a slab: the menu scene reads through it (the user, 2026-10-16:
# "I liked the transparent backgrounds way more than the solid black backgrounds"). Shared by the
# Arena, Toy Box and Mission windows.
# 0.55 -> 0.75 on 2026-10-17 (the owner: "make the bg opaque by 20%"): still glass, but Home no longer
# reads through behind a window's title and Back.
WINDOW_BACKDROP_ALPHA = 0.75
# Rev 3: the four windows' body and header, shared. The left inset clears the friends drawer's handle
# (author_menu_friends_drawer.HANDLE, 76 px on the left edge, drawn over every window) with 24 px of
# air; the right keeps 60. Vertical: 196 under the top (nav + header), 40 over the bottom.
WINDOW_LEFT, WINDOW_RIGHT = 100, 60
WINDOW_BODY = R((0, 0), (1, 1), ((WINDOW_LEFT - WINDOW_RIGHT) / 2, -78), (-(WINDOW_LEFT + WINDOW_RIGHT), -236))
WINDOW_HEADER = R((0, 1), (1, 1), ((WINDOW_LEFT - WINDOW_RIGHT) / 2, -108), (-(WINDOW_LEFT + WINDOW_RIGHT), 64), (0.5, 1))
EXPLORE_W = 1920 - WINDOW_LEFT - WINDOW_RIGHT      # at the 1920 reference width
CONTENT_W = EXPLORE_W - 10 - 12                     # viewport inset, content padding
EMPTY_LINE = "No one else is online right now. Pilots who come online show here, ready to invite."


def section_header(parent, key, name, title, shown=True, count="0"):
    n = parent.child(N(key, name, R((0, 1), None, (0, 0), (ROW_W, 18), (0, 1))))
    n.add("le", 114, le(ph=18, ignore=not shown))
    if not shown or name.startswith("Invites"):
        n.add("cg", 225, canvas_group(shown))
    label(n.child(N(f"{key}/Label", "Label", STRETCH)), title, "Label", "TextLight")
    label(n.child(N(f"{key}/Count", "Count", STRETCH)), count, "Label", "NeutralLightest", align="right")
    return n


def build_panel():
    root = N("root", "PartyPanel", R(size=(PANEL_W, 844)))
    H.framed(root, "Frame", 14, "SurfaceVeryDark", "SurfaceDark")
    inner = root.child(N("Inner", "Inner", R((0, 0), (1, 1), (0, -2), (-44, -40))))
    inner.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 14, 0, (1, 1), (1, 0)))

    top = inner.child(N("Top", "Top", R((0, 1), None, (0, 0), (ROW_W, 46), (0, 1))))
    top.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 8, 0, (1, 1), (1, 0))).add("le", 114, le(ph=46))
    head = top.child(N("Top/Header", "Header", R((0, 1), None, (0, 0), (ROW_W, 18), (0, 1)))).add("le", 114, le(ph=18))
    label(head.child(N("Top/Header/Label", "Label", STRETCH)), "PARTY", "Label", "TextLight")
    label(head.child(N("Top/Header/Count", "Count", STRETCH)), "1 / 4", "Label", "NeutralLightest", align="right")
    online = top.child(N("Top/Online", "OnlineCount", R((0, 1), None, (0, -26), (ROW_W, 20), (0, 1))))
    online.add("le", 114, le(ph=20)).add("hlg", 114, K.hv_layout(False, (1, 0, 0, 0), 10, 3, (0, 0), (0, 0)))
    diamond = image_node(online, "Top/Online/Diamond", "Diamond", R((0, 0.5), None, (5, 0), (8, 8)), "Cta")
    diamond.rot = 45
    label(online.child(N("Top/Online/Text", "Text", R((0, 0.5), None, (20, 0), (300, 20), (0, 0.5)))),
          "1 Player Online", "BodySmall", "NeutralLightest")

    slots = inner.child(N("Slots", "Slots", R((0, 1), None, (0, 0), (ROW_W, 4 * SLOT_H + 24), (0, 1))))
    slots.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 8, 0, (1, 1), (1, 0))).add("le", 114, le(ph=4 * SLOT_H + 24))
    for i in range(4):
        slots.children.append(Inst(f"Slot{i + 1}", "PartySlot", f"Slot{i + 1}",
                                   R((0, 1), None, (0, -i * (SLOT_H + 8)), (ROW_W, SLOT_H), (0, 1))))

    leave = inner.child(N("Leave", "Leave", R((0, 1), None, (0, 0), (ROW_W, 36), (0, 1))))
    leave.add("cg", 225, canvas_group(False)).add("le", 114, le(ph=36, ignore=True)) \
        .add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 0, 3, (0, 0), (0, 0)))
    leave.children.append(small_button("LeaveButton", "LeaveButton", "Cancel", "LEAVE PARTY"))

    image_node(inner, "Divider", "Divider", R((0, 1), None, (0, 0), (ROW_W, 1), (0, 1)), "SurfaceDark", layout=le(ph=1))

    lists = inner.child(N("Lists", "Lists", R((0, 1), None, (0, 0), (ROW_W, 400), (0, 1))))
    lists.add("le", 114, le(mh=0, fh=1)).add("scroll", 114, scroll_rect("Lists/Viewport/Content", "Lists/Viewport", 20))
    vp = lists.child(N("Lists/Viewport", "Viewport", R((0, 0), (1, 1), (0, 0), (12, 0))))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, rect_mask())
    content = vp.child(N("Lists/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (6, 6, 6, 6), 12, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))
    section_header(content, "Invites", "InvitesHeader", "INVITES", shown=False)
    req = content.child(N("Requests", "Requests", R((0, 1), None, (0, 0), (ROW_W, 0), (0, 1))))
    req.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 12, 0, (1, 1), (1, 0))) \
        .add("count", 114, counter(c("Invites/Count", "kt"), filled=[c("Invites", "cg"), c("InvitesDivider", "cg")]))
    div = image_node(content, "InvitesDivider", "InvitesDivider", R((0, 1), None, (0, 0), (ROW_W, 1), (0, 1)),
                     "SurfaceDark", layout=le(ph=1, ignore=True))
    div.add("cg", 225, canvas_group(False))
    section_header(content, "OnlineHead", "OnlineHeader", "ONLINE")
    onl = content.child(N("OnlinePlayers", "OnlinePlayers", R((0, 1), None, (0, 0), (ROW_W, 0), (0, 1))))
    onl.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 12, 0, (1, 1), (1, 0))) \
        .add("count", 114, counter(c("OnlineHead/Count", "kt"), empty=c("EmptyLine", "cg")))
    label(content.child(N("EmptyLine", "EmptyLine", R((0, 1), None, (0, 0), (ROW_W, 44), (0, 1)))),
          EMPTY_LINE, "BodySmall", "NeutralLightest", valign="top", wrap=True) \
        .add("cg", 225, canvas_group(True)).add("le", 114, le(ph=44))

    root.add("lobby", 114, lambda ctx, go: mono(go, S["lobby"],
             f"  connectionData: {K.ref(11400000, CONNECTION_DATA, 2)}\n"
             f"  profileIcons: {K.ref(11400000, PROFILE_ICONS, 2)}\n"
             + refs("slots", [ctx.iref(f"Slot{i + 1}", "root#slot") for i in range(4)])
             + f"  onlineStatusText: {ref(ctx.c('Top/Online/Text', 'tmp'))}\n"
             f"  leaveButton: {ref(ctx.iref('LeaveButton', 'root#button'))}\n"
             f"  friendsListPanel: {ref(ctx.c('root', 'friends'))}\n"
             f"  partyCountText: {ref(ctx.c('Top/Header/Count', 'kt'))}\n"
             f"  leaveGroup: {ref(ctx.c('Leave', 'cg'))}\n"))
    root.add("friends", 114, lambda ctx, go: mono(go, S["friends"],
             f"  onlineContent: {ref(ctx.rt('OnlinePlayers'))}\n"
             f"  requestsContent: {ref(ctx.rt('Requests'))}\n"
             f"  onlineInfoPrefab: {K.ref(K.FileIds('PartyOnlineRow')('root#row'), asset_guid('PartyOnlineRow'))}\n"
             f"  requestInfoPrefab: {K.ref(K.FileIds('PartyRequestRow')('root#row'), asset_guid('PartyRequestRow'))}\n"
             f"  connectionData: {K.ref(11400000, CONNECTION_DATA, 2)}\n"
             f"  friendsData: {K.ref(11400000, FRIENDS_DATA, 2)}\n"
             f"  profileIcons: {K.ref(11400000, PROFILE_ICONS, 2)}\n"
             "  closeButton: {fileID: 0}\n  embedded: 1\n  listPartyMembers: 0\n"
             "  inviteReceivedAudio: 9\n  friendRequestExpirationSeconds: 600\n"
             "  partyInviteExpirationSeconds: 60\n"))
    root.add("keeper", 114, lambda ctx, go: mono(go, S["keeper"], ""))
    root.add("le", 114, le(mw=PANEL_W, pw=PANEL_W, fw=0))
    return root


# --------------------------------------------------------------------------------------------
# ArcadeGameCard.prefab
# --------------------------------------------------------------------------------------------

CARD_W, CARD_H = 341, 218


def build_card(placeholder_art):
    root = N("root", "ArcadeGameCard", R(size=(CARD_W, CARD_H)))
    ring_node(root, "FocusRing", 14)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    clip = root.child(N("Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
    clip.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("mask", 114, mask(False)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], f"  image: {ref(ctx.c('Clip/Art', 'img'))}\n"))
    art_node(clip, "Clip/Art", "Art", R(size=(CARD_W, CARD_W * 510 / 768)), placeholder_art)
    image_node(clip, "Clip/Fade", "Fade", R((0, 0), (1, 0), (0, 0), (0, 110), (0.5, 0)), "SurfaceBlack",
               K.kit_sprite("kit_fade_v"))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))

    chip = root.child(N("VesselChip", "VesselChip", R((0, 1), None, (12, -12), (44, 44), (0, 1))))
    H.framed(chip, "VesselChip", 10, "SurfaceDark", "SurfaceLight")
    art_node(chip, "VesselIcon", "VesselIcon", R(size=(38, 38)), None, preserve=True)
    for i, key in enumerate(("Petal", "PetalSecondary")):
        n = root.child(N(key, "GenrePetal" if i == 0 else "GenrePetalSecondary",
                         R((0, 1), None, (64, -12 - i * 24), (22, 22), (0, 1))))
        n.add("cr", 222, K.canvas_renderer()).add("img", 114, lambda ctx, go: K.image(
            (1.0, 1.0, 1.0, 1.0), None, preserve=True)(ctx, go).replace("  m_Enabled: 1\n", "  m_Enabled: 0\n", 1))

    star = root.child(N("Star", "FavoriteStar", R((1, 1), None, (-6, -6), (44, 44), (1, 1))))
    star.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    glyph = image_node(star, "Star/Glyph", "Glyph", R(size=(24, 24)), "NeutralLightest", K.chrome_sprite("chrome_star"),
                       preserve=True)
    star.add("audio", 114, menu_audio(5)) \
        .add("button", 114, lambda ctx, go: mono(go, BUTTON,
             selectable(nav_mode=0) + f"  m_TargetGraphic: {ref(ctx.c('Star', 'img'))}\n"
             + calls([(ctx.c("root", "card"), "CosmicShore.UI.KitGameCard", "ToggleFavorite"),
                      (ctx.c("Star", "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")]))) \
        .add("hover", 114, lambda ctx, go: mono(go, S["hover"],
             f"  theme: {K.ref(11400000, K.THEME, 2)}\n  hoverToken: {K.TOKEN_INDEX['TextLight']}\n"
             + refs("texts", []) + refs("graphics", [ctx.c("Star/Glyph", "th")])))
    assert glyph

    lock = root.child(N("Lock", "Lock", R(pos=(0, 22), size=(30, 30))))
    lock.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight", 0.0), K.chrome_sprite("chrome_lock"),
                                                                   preserve=True))
    lab = root.child(N("LockLabel", "LockLabel", R(pos=(0, -6), size=(200, 18))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("LOCKED", "Label", tok("TextLight", 0.0), align="center")) \
        .add("kt", 114, K.kit_text("Label", "TextLight", driven=True))

    bottom = root.child(N("Bottom", "Bottom", R((0, 0), (1, 0), (0, 12), (-28, 58), (0.5, 0))))
    bottom.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 6, (1, 1), (0, 0)))
    title = bottom.child(N("Bottom/Title", "Title", R((0, 0), None, (0, 0), (240, 58), (0, 0))))
    title.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Game", "H3", tok("TextLight"), valign="bottom", wrap=True)) \
        .add("kt", 114, K.kit_text("H3", "TextLight", driven=True)).add("le", 114, le(mw=0, fw=1))
    picks = bottom.child(N("Bottom/Picks", "AvatarSpace", R((0, 0), None, (0, 0), (22, 58), (0, 0))))
    picks.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 4, 8, (0, 0), (0, 0))).add("le", 114, le(mw=0))
    pick = picks.child(N("Bottom/Picks/Pick", "AvatarIcon", R(size=(22, 22))))
    pick.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), AVATAR_PLACEHOLDER, preserve=True)) \
        .add("cg", 225, canvas_group(False)).add("le", 114, le(ignore=True, pw=22, ph=22))

    root.add("tile", 114, tile_body(c("FocusRing", "img"), c("Fill", "img"), c("Border", "img"), 14,
                                    primary=[c("Bottom/Title", "tmp")], art=[c("Clip/Art", "img"), c("VesselIcon", "img")],
                                    lock=c("Lock", "img"), lock_label=c("LockLabel", "tmp"), sel_border="ControlActive",
                                    on_click=[lambda ctx: (ctx.c("root", "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")]))
    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  AllGames: {K.ref(11400000, ALL_GAMES, 2)}\n"
             f"  StarIconActive: {K.sprite_ref(K.chrome_sprite('chrome_star_fill'))}\n"
             f"  StarIconInActive: {K.sprite_ref(K.chrome_sprite('chrome_star'))}\n"
             "  ExploreView: {fileID: 0}\n"
             f"  GameTitle: {ref(ctx.c('Bottom/Title', 'tmp'))}\n"
             f"  BackgroundImage: {ref(ctx.c('Clip/Art', 'img'))}\n"
             f"  StarImage: {ref(ctx.c('Star/Glyph', 'img'))}\n"
             "  Index: 0\n"
             f"  VesselIcon: {ref(ctx.c('VesselIcon', 'img'))}\n"
             f"  VesselList: {K.ref(11400000, VESSEL_LIST, 2)}\n"
             f"  GenrePetal: {ref(ctx.c('Petal', 'img'))}\n"
             f"  GenrePetalSecondary: {ref(ctx.c('PetalSecondary', 'img'))}\n"
             f"  AvatarSpace: {ref(ctx.rt('Bottom/Picks'))}\n"
             f"  ProfileIcons: {K.ref(11400000, PROFILE_ICONS, 2)}\n"
             "  LocalPickBorder: {fileID: 0}\n"
             "  localPickBorderColor: {r: 0.45, g: 1, b: 0.85, a: 1}\n"
             "  lockOverlay: {fileID: 0}\n"
             "  lockedTintColor: {r: 0.3, g: 0.3, b: 0.3, a: 1}\n"
             f"  tile: {ref(ctx.c('root', 'tile'))}\n"
             f"  starPaint: {ref(ctx.c('Star/Glyph', 'th'))}\n"))
    root.add("audio", 114, menu_audio(2)).add("scroll", 114, scroll_into()) \
        .add("le", 114, le(mw=0, fw=1, ph=CARD_H))
    return root


# --------------------------------------------------------------------------------------------
# ArcadeView.prefab
# --------------------------------------------------------------------------------------------

# Rev 3: taller - the quest-view rhythm of the R3Arcade board, two features across the full width.
FEATURE_H = 300
FEATURE_W = (CONTENT_W - 20) // 2


def feature_card(parent, key, name, art, eyebrow, eyebrow_token, title, extra=None):
    n = parent.child(N(key, name, R((0, 1), None, (0, 0), (FEATURE_W, FEATURE_H), (0, 1))))
    ring_node(n, f"{key}/FocusRing", 14)
    fill = n.child(N(f"{key}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    clip = n.child(N(f"{key}/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
    clip.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("mask", 114, mask(False)) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], f"  image: {ref(ctx.c(key + '/Clip/Art', 'img'))}\n"))
    art_node(clip, f"{key}/Clip/Art", "Art", R(size=(FEATURE_W, FEATURE_W * 510 / 768)), art)
    image_node(clip, f"{key}/Clip/Scrim", "Scrim", STRETCH, "SurfaceBlack", K.kit_sprite("kit_fade_h"))
    bord = n.child(N(f"{key}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))
    label(n.child(N(f"{key}/Eyebrow", "Eyebrow", R((0, 1), None, (26, -24), (420, 18), (0, 1)))), eyebrow, "Label", eyebrow_token)
    t = n.child(N(f"{key}/Title", "Title", R((0, 1), None, (26, -48), (600, 46), (0, 1))))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text(title, "H1", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    if extra:
        extra(n)
    n.add("tile", 114, tile_body(c(f"{key}/FocusRing", "img"), c(f"{key}/Fill", "img"), c(f"{key}/Border", "img"), 14,
                                 primary=[c(f"{key}/Title", "tmp")], art=[c(f"{key}/Clip/Art", "img")],
                                 on_click=[lambda ctx: (ctx.c(key, "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")]))
    n.add("audio", 114, menu_audio(2)).add("scroll", 114, scroll_into()).add("le", 114, le(mw=0, fw=1, ph=FEATURE_H))
    return n


def back_button(parent):
    w_label = K.label_width("BACK", "ButtonSmall")
    keycap_w = K.label_width("B", "Label") + 24
    w = 16 + keycap_w + 10 + w_label + 16
    b = parent.child(N("Back", "Back", R((1, 0), None, (0, 0), (w, 40), (1, 0))))
    ring_node(b, "Back/FocusRing", 10)
    border, fillc, content = K.button_palette("Default", False)
    f = b.child(N("Back/Fill", "Fill", STRETCH))
    f.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)).add("le", 114, le(ignore=True))
    bd = b.child(N("Back/Border", "Border", STRETCH))
    bd.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)).add("le", 114, le(ignore=True))
    b.children.append(Inst("Keycap", "KitKeycap", "Keycap", R(size=(keycap_w, K.line_height("Label") + 8)),
                           [("Label#tmp", "m_text", "B", None)]))
    lab = b.child(N("Back/Label", "Label", R(size=(w_label, K.line_height("ButtonSmall")))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp("BACK", "ButtonSmall", content)) \
        .add("kt", 114, K.kit_text("ButtonSmall", driven=True))
    b.add("button", 114, lambda ctx, go: mono(go, S["button"],
          K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c('Back/Fill', 'img'))}\n" + calls([])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c('Back/FocusRing', 'img'))}\n"
          "  variant: 0\n  fill: 0\n"
          f"  fillGraphic: {ref(ctx.c('Back/Fill', 'img'))}\n  borderGraphic: {ref(ctx.c('Back/Border', 'img'))}\n"
          + refs("contentGraphics", [ctx.c("Back/Label", "tmp")]) + "  playGradient: {fileID: 0}\n"))
    b.add("close", 114, lambda ctx, go: mono(go, S["close"], ""))
    b.add("hlg", 114, K.hv_layout(False, (16, 16, 9, 9), 10, 4, (0, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))
    return b


def build_view(maelstrom_art, maelstrom_desc, weekly_art):
    root = N("root", "ArcadeView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=WINDOW_BACKDROP_ALPHA)

    # Sibling order is FOCUS order: ScreenSwitcher.Refocus lands on the first live Selectable in
    # hierarchy order, so the explore scroll comes before the party panel (the Body lays them out
    # reversed, panel on the left) and the header - whose Back would close the window on the first
    # A press - comes last.
    body = root.child(N("Body", "Body", WINDOW_BODY))
    body.add("hlg", 114, lambda ctx, go: K.hv_layout(False, (0, 0, 0, 0), 24, 0, (1, 1), (0, 1))(ctx, go)
             .replace("  m_ReverseArrangement: 0\n", "  m_ReverseArrangement: 1\n"))
    # Motion (section 22.8): the window's two blocks rise in on every open - the modal never
    # re-enables, so ModalWindowManager replays them (KitEntrance.ReplayUnder).
    body.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", stagger=1))

    ex = body.child(N("Explore", "ExploreScroll", R((0, 1), None, (0, 0), (EXPLORE_W, 844), (0, 1))))
    ex.add("le", 114, le(mw=0, fw=1)).add("scroll", 114, scroll_rect("Explore/Viewport/Content", "Explore/Viewport"))
    vp = ex.child(N("Explore/Viewport", "Viewport", R((0, 0), (1, 1), (-5, 0), (-10, 0))))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, rect_mask())
    content = vp.child(N("Explore/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (6, 6, 6, 6), 0, 0, (1, 1), (1, 0))).add("csf", 114, K.fitter(0, 2))

    feats = content.child(N("Features", "Features", R((0, 1), None, (0, 0), (CONTENT_W, FEATURE_H), (0, 1))))
    feats.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 20, 0, (1, 1), (1, 1))).add("le", 114, le(ph=FEATURE_H))

    def weekly_extra(n):
        cd = n.child(N("Weekly/Countdown", "Countdown", R((0, 0), None, (26, 22), (420, 26), (0, 0))))
        cd.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("ENDS IN 6d 3h", "Data", tok("TextLight"))) \
            .add("kt", 114, K.kit_text("Data", "TextLight"))

    def mael_extra(n):
        d = n.child(N("Maelstrom/Description", "Description", R((0, 1), None, (26, -102), (380, 66), (0, 1))))
        d.add("cr", 222, K.canvas_renderer()) \
            .add("tmp", 114, text(maelstrom_desc, "BodySmall", tok("TextLight"), valign="top", wrap=True)) \
            .add("kt", 114, K.kit_text("BodySmall", "TextLight"))

    wk = feature_card(feats, "Weekly", "WeeklyChallenge", weekly_art, "WEEKLY CHALLENGE", "ElementTime", "Skim Race",
                      weekly_extra)
    wk.add("weekly", 114, lambda ctx, go: mono(go, S["weekly"],
           f"  GameTitle: {ref(ctx.c('Weekly/Title', 'tmp'))}\n"
           f"  TimeRemaining: {ref(ctx.c('Weekly/Countdown', 'tmp'))}\n"
           f"  BackgroundImage: {ref(ctx.c('Weekly/Clip/Art', 'img'))}\n"
           "  ObjectiveText: {fileID: 0}\n  StatusText: {fileID: 0}\n  CompletedBadge: {fileID: 0}\n"))
    feature_card(feats, "Maelstrom", "Maelstrom", maelstrom_art, "GAUNTLET", "ControlActive", "Maelstrom", mael_extra)

    content.child(N("Gap1", "Gap", R((0, 1), None, (0, 0), (CONTENT_W, 26), (0, 1)))).add("le", 114, le(ph=26))
    allg = content.child(N("AllGames", "AllGames", R((0, 1), None, (0, 0), (CONTENT_W, 18), (0, 1))))
    allg.add("le", 114, le(ph=18))
    label(allg.child(N("AllGames/Label", "Label", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "ALL GAMES", "Label", "TextLight")
    label(allg.child(N("AllGames/Count", "Count", R((0, 0), (1, 1), (0, 0), (-10, 0)))), "28", "Label", "NeutralLightest",
          align="right")
    content.child(N("Gap2", "Gap", R((0, 1), None, (0, 0), (CONTENT_W, 10), (0, 1)))).add("le", 114, le(ph=10))

    grid = content.child(N("Grid", "GameGrid", R((0, 1), None, (0, 0), (CONTENT_W, GRID_ROWS * (CARD_H + 10) - 10), (0, 1))))
    grid.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 0, (1, 1), (1, 0)))
    for r in range(GRID_ROWS):
        row = grid.child(N(f"Row{r + 1}", f"GameListRow{r + 1}", R((0, 1), None, (0, -r * (CARD_H + 10)), (CONTENT_W, CARD_H), (0, 1))))
        row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 0, (1, 1), (1, 1))).add("le", 114, le(ph=CARD_H))
        for col in range(GRID_COLS):
            row.children.append(Inst(card_key(r, col), "ArcadeGameCard", "GameCard",
                                     R((0, 1), None, (col * (CARD_W + 10), 0), (CARD_W, CARD_H), (0, 1))))
    # Rev 3 (2026-10-16): the party panel left every window for ONE friends drawer on Menu_Main
    # (author_menu_friends_drawer.py), so the explore scroll takes the whole body.

    header = root.child(N("Header", "Header", WINDOW_HEADER))
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(header.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (300, 18), (0, 1)))), "ARCADE", "Label",
          "NeutralLightest")
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))), "Explore games", "H1",
          "TextLight")
    back_button(header)
    return root


def card_key(r, col):
    return f"Card{r + 1}_{col + 1}"


# --------------------------------------------------------------------------------------------
# Prefab outputs
# --------------------------------------------------------------------------------------------

def content_data():
    mael_art = sprite_guid(MAELSTROM_ASSET, "CardBackground")
    mael_desc = asset_field(MAELSTROM_ASSET, "Description").strip("'\"")
    weekly_art = sprite_guid(WEEKLY_PLACEHOLDER_ASSET, "CardBackground")
    assert mael_desc.isascii(), "the Maelstrom description is not ASCII"
    return mael_art, mael_desc, weekly_art


def outputs():
    mael_art, mael_desc, weekly_art = content_data()
    roots = {"PartySlot": build_slot(), "PartyRequestRow": build_request_row(), "PartyOnlineRow": build_online_row(),
             "PartyPanel": build_panel(), "ArcadeGameCard": build_card(weekly_art),
             "ArcadeView": build_view(mael_art, mael_desc, weekly_art)}
    out = {}
    for name in PREFABS:
        out[prefab_path(name)] = prefab_text(roots[name], name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    out[PARTY_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Party"))
    out[ARCADE_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Arcade"))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

# The live objects the patch edits (fileIDs measured 2026-10-14; names and scripts are asserted).
MODAL = dict(go="5018536842734414741", rt="461152924818656390", gate="9200687658501214767")
PANEL = dict(go="1588431009219518451", rt="3611855818750974332", img="3611855818750974333",
             gate="6633029320521394827")
EXPLORE = dict(go="829334092218458952", rt="4722418330166964489", view="1267710959564374252")
RUNNER = "654228613"
RETIRED_ROOTS = {"7767652896660694874": "Header", "3184225246935049006": "GameSelectScrollView",
                 "2496229280682334806": "ArcadeLobbyList", "7399798317413155242": "CloseButton",
                 "688672352": "FriendListPanel"}

VIEW_OWNED = ("GameSelectionView", "GameSelectionGrid", "WeeklyChallengeCard", "maelstromCard", "gameCountText")
# Fields the view no longer declares; a migration may drop them (ArcadeDPadNav: retired, see
# Tools/Build/retire_arcade_dpad_nav.py).
VIEW_RETIRED = ("ArcadeDPadNav",)


class Ids:
    """Every fileID the scene patch mints, by role."""

    def __init__(self):
        self.ctx = PCtx("Menu_Main/Arcade")
        self.inst = str(self.ctx.fid("ArcadeView#inst"))
        self.stub = str(self.ctx.fid("ArcadeView#stub"))
        V = K.FileIds("ArcadeView")
        # Direct objects of ArcadeView.prefab the scene names: (source fileID in the prefab, class, script)
        self.refs = {
            "GameSelectionView": (V("Explore#go"), 1, None),
            "GameSelectionGrid": (V("Grid#rt"), 224, None),
            "WeeklyChallengeCard": (V("Weekly#weekly"), 114, S["weekly"]),
            "maelstromCard": (V("Maelstrom#tile"), 114, S["tile"]),
            "gameCountText": (V("AllGames/Count#kt"), 114, S["kt"]),
        }
        # Objects of NESTED instances inside ArcadeView: their id in ArcadeView is derived once.
        self.cards = []
        for r in range(GRID_ROWS):
            for col in range(GRID_COLS):
                pid = V(card_key(r, col) + "#inst")
                key = f"card{r}_{col}"
                self.refs[key] = (derived(K.FileIds("ArcadeGameCard")("root#card"), pid), 114, S["card"])
                self.cards.append(key)
        self.stubs = {k: str(derived(v[0], self.inst)) for k, v in self.refs.items()}
        # The ScrollRect stub only the retired ArcadeDPadNav named. Removed if present.
        self.stale = {str(derived(V("Explore#scroll"), self.inst)),
                      # rev 3: the party panel left ArcadeView for the friends drawer; its stub goes too
                      str(derived(derived(K.FileIds("PartyPanel")("root#go"), V("PartyPanel#inst")), self.inst))}

    def all(self):
        return {self.inst, self.stub} | set(self.stubs.values())


def stub_for(ids, field):
    src, cls, scr = ids.refs[field]
    fid, guid, pid = ids.stubs[field], asset_guid("ArcadeView"), ids.inst
    if cls == 1:
        return T.stub_go(fid, src, guid, pid)
    if cls == 224:
        return (f"--- !u!224 &{fid} stripped\nRectTransform:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
                f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return T.stub_mono(fid, src, guid, pid, scr)


def doc_fields(doc):
    return H.doc_fields(doc)


def view_doc(old, ids):
    head = old[:old.index("  rosterOverride:")]
    fields, _ = doc_fields(old[len(head):])
    values = {k: v for k, v in fields if k not in VIEW_RETIRED}
    new = {k: f" {ref(ids.stubs[k])}" for k in VIEW_OWNED}
    order = ["rosterOverride", "GameSelectionView", "GameSelectionGrid", "WeeklyChallengeCard",
             "maelstromCard", "gameCountText", "ArcadeGameConfigureModal", "selectedVesselClassType",
             "cardRevealSettings"]
    assert set(values) <= set(order), f"ArcadeExploreView carries fields this script does not know: {set(values) - set(order)}"
    return head + "".join(f"  {k}:{new.get(k, values.get(k))}\n" for k in order)


def field_set(doc, name, value_line):
    """Replace one single-line top-level field."""
    pat = re.compile(r"^  " + re.escape(name) + r":.*$", re.M)
    assert len(pat.findall(doc)) == 1, f"field {name} not found exactly once"
    return pat.sub(lambda _m: f"  {name}: {value_line}", doc)


def transform(text, files=None):
    sc = T.Scene(text)
    ids = Ids()
    generated = ids.all()

    assert sc.field(MODAL["go"], "m_Name") == "ArcadeScreenModal", "ArcadeScreenModal moved"
    assert sc.field(PANEL["go"], "m_Name") == "Arcade_Panel", "Arcade_Panel moved"
    assert sc.field(EXPLORE["go"], "m_Name") == "Explore", "Explore moved"
    assert sc.script(EXPLORE["view"]) == S["view"], "ArcadeExploreView moved"
    assert sc.script(RUNNER) == S["runner"], "QuestGraphRunner moved"
    assert sc.script(MODAL["gate"]) == S["gate"] and sc.script(PANEL["gate"]) == S["gate"], "OfflineUIGates moved"
    assert sc.script(PANEL["img"]) == K.IMAGE, "Arcade_Panel's Image moved"
    migrating = any(sc.has(r) for r in RETIRED_ROOTS)
    if migrating:
        clash = generated & set(sc.order)
        assert not clash, f"minted fileIDs already in the scene: {sorted(clash)[:3]}"

    for root, name in RETIRED_ROOTS.items():
        if sc.has(root):
            assert sc.field(root, "m_Name") == name, f"retired root {root} is no longer {name}"
            for fid in sc.subtree(root):
                sc.remove(fid)
    for fid in list(sc.order):
        if fid in generated or fid in ids.stale:
            sc.remove(fid)

    out = []
    K.emit_instance(ids.ctx, K.Instance("ArcadeView", "ArcadeView", asset_guid("ArcadeView"), "ArcadeView", STRETCH),
                    EXPLORE["rt"], out)
    out += [stub_for(ids, k) for k in ids.refs]
    out = [K.unity_empty_scalars(d) for d in out]
    anchor = EXPLORE["rt"]
    for d in out:
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)

    sc.replace(EXPLORE["rt"], T.set_block(sc.docs[EXPLORE["rt"]], "m_Children", K.refs_list("m_Children", [ids.stub])))
    sc.replace(PANEL["rt"], T.set_block(sc.docs[PANEL["rt"]], "m_Children", K.refs_list("m_Children", [EXPLORE["rt"]])))
    mrt = T.set_block(sc.docs[MODAL["rt"]], "m_Children", K.refs_list("m_Children", [PANEL["rt"]]))
    mrt = field_set(mrt, "m_AnchoredPosition", "{x: 0, y: 0}")
    sc.replace(MODAL["rt"], field_set(mrt, "m_SizeDelta", "{x: 0, y: 0}"))
    sc.replace(PANEL["img"], field_set(sc.docs[PANEL["img"]], "m_Enabled", "0"))
    sc.replace(MODAL["gate"], T.set_block(sc.docs[MODAL["gate"]], "onlineOnlyObjects", "  onlineOnlyObjects: []\n"))
    sc.replace(PANEL["gate"], T.set_block(sc.docs[PANEL["gate"]], "onlineOnlyObjects", "  onlineOnlyObjects: []\n"))
    sc.replace(EXPLORE["view"], view_doc(sc.docs[EXPLORE["view"]], ids))
    sc.replace(RUNNER, T.set_block(sc.docs[RUNNER], "gameCards", K.refs_list("gameCards", [ids.stubs[k] for k in ids.cards])))
    return sc.text()


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def known_guids():
    return T.known_guids() | PACKAGE


def anchors_of(text):
    return set(re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M))


def prefab_source(guid, files):
    for name in PREFABS:
        if asset_guid(name) == guid:
            return files[prefab_path(name)]
    for name in ("KitButton", "KitButtonSmall", "KitTile", "KitDivider", "KitKeycap", "KitCurrencyPill"):
        if K.prefab_guid(name) == guid:
            return open(os.path.join(REPO, K.PREFAB_DIR, name + ".prefab"), encoding="utf-8").read()
    raise AssertionError(f"no source prefab for guid {guid}")


def prefab_objects(guid, files, _memo=None):
    """Every object a prefab asset holds: its own documents, plus - recursively - every object of each
    nested instance or variant base, at its derived id. What a stub in another file may name."""
    memo = {} if _memo is None else _memo
    if guid in memo:
        return memo[guid]
    text = prefab_source(guid, files)
    objs = anchors_of(text)
    for m in re.finditer(r"^--- !u!1001 &(-?\d+)\n(.*?)(?=^--- |\Z)", text, re.M | re.S):
        pid = int(m.group(1))
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", m.group(2)).group(1)
        objs |= {str(derived(o, pid)) for o in prefab_objects(src, files, memo)}
    memo[guid] = objs
    return objs


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


def check_sources(path, text, files):
    """Every stub, override target and added-component target names an object its prefab holds."""
    for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                             r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", text):
        assert src in prefab_objects(g, files), f"{path}: source object {src} is not in the prefab {g}"


def doc_of(text, fid):
    m = re.search(r"^--- !u!(\d+) &" + re.escape(str(fid)) + r"( stripped)?\n(.*?)(?=^--- |\Z)", text, re.M | re.S)
    return (int(m.group(1)), m.group(3)) if m else (None, None)


def kind_of(text, fid):
    cls, body = doc_of(text, fid)
    assert cls is not None, f"fileID {fid} has no document"
    m = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
    return cls, (m.group(1) if m else None)


def check_fields(files, name, comp_key, wanted):
    """Every listed field of one component is wired, each to the right kind of object."""
    text = files[prefab_path(name)]
    _, body = doc_of(text, K.FileIds(name)(comp_key))
    assert body, f"{name}: no {comp_key} component"
    for field, want in wanted.items():
        m = re.search(r"^  " + field + r": \{fileID: (-?\d+)\}", body, re.M)
        assert m and m.group(1) != "0", f"{name}.{field} is not wired"
        cls, scr = kind_of(text, m.group(1))
        ok = cls == want if isinstance(want, int) else scr == want
        assert ok, f"{name}.{field} points at the wrong kind of object ({cls}, {scr})"


TMP, IMG = K.TMP_UGUI, K.IMAGE
FIELDS = [
    ("PartySlot", "root#slot", dict(avatarIcon=IMG, addButton=S["tile"], displayNameText=TMP, kickButton=S["button"],
                                    occupiedGroup=225, emptyGroup=225, kickGroup=225, roleText=S["kt"], domainRing=IMG)),
    ("PartyRequestRow", "root#row", dict(avatarIcon=IMG, usernameText=TMP, labelText=TMP, acceptButton=S["button"],
                                         declineButton=S["button"], kindText=S["kt"])),
    ("PartyOnlineRow", "root#row", dict(avatarIcon=IMG, usernameText=TMP, labelText=TMP, inviteButton=S["button"],
                                        cancelButton=S["button"], joinButton=S["button"], statusText=S["kt"],
                                        inviteLabel=S["kt"], joinLabel=S["kt"], inviteGroup=225, joinGroup=225,
                                        cancelGroup=225)),
    ("PartyPanel", "root#lobby", dict(onlineStatusText=TMP, leaveButton=S["button"], friendsListPanel=S["friends"],
                                      partyCountText=S["kt"], leaveGroup=225)),
    ("PartyPanel", "root#friends", dict(onlineContent=224, requestsContent=224)),
    ("ArcadeGameCard", "root#card", dict(GameTitle=TMP, BackgroundImage=IMG, StarImage=IMG, VesselIcon=IMG,
                                         GenrePetal=IMG, GenrePetalSecondary=IMG, AvatarSpace=224, tile=S["tile"],
                                         starPaint=S["th"])),
    ("ArcadeView", "Weekly#weekly", dict(GameTitle=TMP, TimeRemaining=TMP, BackgroundImage=IMG)),
]


def validate_prefabs(files, guids):
    M.require_entrances(files, {prefab_path("ArcadeView"): 2, prefab_path("PartySlot"): 1})
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for path, text in files.items():
        if path.endswith(".meta"):
            continue
        check_file(path, text, guids, minted)
        check_sources(path, text, files)
    for name, comp, wanted in FIELDS:
        check_fields(files, name, comp, wanted)

    panel = files[prefab_path("PartyPanel")]
    _, lobby = doc_of(panel, K.FileIds("PartyPanel")("root#lobby"))
    slots = re.findall(r"^  - \{fileID: (-?\d+)\}", lobby.split("  slots:")[1].split("  onlineStatusText")[0], re.M)
    assert len(slots) == 4 and len(set(slots)) == 4, "ArcadeLobbyList needs four distinct slots"
    for s in slots:
        assert kind_of(panel, s) == (114, S["slot"]), "an ArcadeLobbyList slot is not a KitPartySlot"
    _, friends = doc_of(panel, K.FileIds("PartyPanel")("root#friends"))
    for field, name in (("onlineInfoPrefab", "PartyOnlineRow"), ("requestInfoPrefab", "PartyRequestRow")):
        want = f"{field}: {{fileID: {K.FileIds(name)('root#row')}, guid: {asset_guid(name)}"
        assert want in friends, f"FriendsListPanel.{field} does not name the {name} row component"
    assert "  embedded: 1\n" in friends and "  listPartyMembers: 0\n" in friends, "the panel's lists must be embedded"

    view = files[prefab_path("ArcadeView")]
    V = K.FileIds("ArcadeView")
    for r in range(GRID_ROWS):
        _, row = doc_of(view, V(f"Row{r + 1}#rt"))
        assert len(re.findall(r"^  - \{fileID:", row, re.M)) == GRID_COLS, f"grid row {r + 1} does not hold {GRID_COLS} cards"
    order = [doc_of(view, k)[1] for k in re.findall(r"^  - \{fileID: (-?\d+)\}", doc_of(view, V("root#rt"))[1], re.M)]
    names = [re.search(r"m_Name: (.*)", doc_of(view, re.search(r"m_GameObject: \{fileID: (-?\d+)\}", b).group(1))[1]).group(1)
             for b in order]
    assert names.index("Body") < names.index("Header"), "the header (Back) must come after the body in focus order"
    check_window_inset(view, "ArcadeView")
    _, body_rt = doc_of(view, V("Body#rt"))
    first = re.search(r"^  - \{fileID: (-?\d+)\}", body_rt, re.M).group(1)
    assert first == str(V("Explore#rt")), "the explore scroll must be the body's first child (focus lands on a card)"
    assert "m_ReverseArrangement: 1" in doc_of(view, V("Body#hlg"))[1], "the body must lay out reversed (panel left)"
    _, back = doc_of(view, V("Back#button"))
    assert back and doc_of(view, V("Back#close"))[0] == 114, "Back has no ModalWindowCloseButton"
    assert "m_Calls: []" in back, "Back must close through ModalWindowCloseButton, not a persistent call"


def validate_scene(before_text, after_text, files):
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
    assert removed <= retired | generated | ids.stale, \
        f"unexpected removals {sorted(removed - retired - generated - ids.stale)[:3]}"
    for fid in ids.stale:
        assert not sc.has(fid), "the retired DPadNav's ScrollRect stub is still in the scene"
    patched = {MODAL["rt"], MODAL["gate"], PANEL["rt"], PANEL["img"], PANEL["gate"], EXPLORE["rt"], EXPLORE["view"],
               RUNNER}
    changed = {f for f in set(before.order) & set(sc.order) if T.normalize(before.docs[f]) != T.normalize(sc.docs[f])}
    assert changed <= patched | generated, f"unexpected edits {sorted(changed - patched - generated)[:3]}"

    # Field scope: every patched document changes only in the fields this item owns.
    scope = {MODAL["rt"]: {"m_Children", "m_AnchoredPosition", "m_SizeDelta"}, MODAL["gate"]: {"onlineOnlyObjects"},
             PANEL["rt"]: {"m_Children"}, PANEL["img"]: {"m_Enabled"}, PANEL["gate"]: {"onlineOnlyObjects"},
             EXPLORE["rt"]: {"m_Children"}, EXPLORE["view"]: set(VIEW_OWNED) | set(VIEW_RETIRED),
             RUNNER: {"gameCards"}}
    for fid, owned in scope.items():
        if not before.has(fid):
            continue
        bf = {k: v for k, v in block_fields(before.docs[fid]).items() if k not in owned}
        af = {k: v for k, v in block_fields(sc.docs[fid]).items() if k not in owned}
        assert bf == af, f"{fid} edited outside {sorted(owned)}: {sorted(set(bf.items()) ^ set(af.items()))[:2]}"

    # The wiring, against the prefab it points into.
    vf = block_fields(sc.docs[EXPLORE["view"]])
    for field in VIEW_OWNED:
        assert vf.get(field) == ref(ids.stubs[field]), f"ArcadeExploreView.{field} is not wired to the view"
    for field in VIEW_RETIRED:
        assert field not in vf, f"ArcadeExploreView still serializes the retired {field}"
    gate = sc.docs[PANEL["gate"]]
    assert "onlineOnlyObjects: []" in gate, "Arcade_Panel's OfflineUIGate still names an object (the party panel moved to the drawer)"
    assert f"guid: {asset_guid('PartyPanel')}" not in files[prefab_path("ArcadeView")], \
        "ArcadeView still instances the PartyPanel (rev 3 moved it to the friends drawer)"
    assert "onlineOnlyObjects: []" in sc.docs[MODAL["gate"]], "the modal's gate still names a retired object"
    cards = re.findall(r"\{fileID: (-?\d+)\}", block_fields(sc.docs[RUNNER])["gameCards"])
    assert cards == [ids.stubs[k] for k in ids.cards], f"QuestGraphRunner.gameCards does not name the {GRID_ROWS * GRID_COLS} cards in order"
    assert "m_Enabled: 0" in sc.docs[PANEL["img"]], "the old Arcade_Panel gradient still draws"
    assert "m_SizeDelta: {x: 0, y: 0}" in sc.docs[MODAL["rt"]] and "m_AnchoredPosition: {x: 0, y: 0}" in sc.docs[MODAL["rt"]]
    assert sc.children(MODAL["rt"]) == [PANEL["rt"]], "ArcadeScreenModal children"
    assert sc.children(PANEL["rt"]) == [EXPLORE["rt"]], "Arcade_Panel children"
    assert sc.children(EXPLORE["rt"]) == [ids.stub], "Explore children"
    view_text = files[prefab_path("ArcadeView")]
    for key, (src, cls, scr) in ids.refs.items():
        stub = sc.docs[ids.stubs[key]]
        assert f"{{fileID: {src}, guid: {asset_guid('ArcadeView')}" in stub, f"{key}: wrong source"
        assert f"m_PrefabInstance: {{fileID: {ids.inst}}}" in stub, f"{key}: wrong instance"
        assert str(src) in prefab_objects(asset_guid("ArcadeView"), files), f"{key}: source not in ArcadeView"
    for key in ("GameSelectionGrid", "WeeklyChallengeCard", "maelstromCard", "gameCountText"):
        src, cls, scr = ids.refs[key]
        got = kind_of(view_text, src)
        assert got[0] == cls and (scr is None or got[1] == scr), f"{key}: wrong kind in ArcadeView"
    assert f"m_TransformParent: {{fileID: {EXPLORE['rt']}}}" in sc.docs[ids.inst]


def block_fields(doc):
    """Top-level fields of one document - list items and folded continuation lines joined into the
    value, whitespace normalised so Unity's line folding never reads as an edit."""
    raw, key = {}, None
    for line in doc.split("\n")[1:]:
        m = re.match(r"^  (\w+):(.*)$", line)
        if m:
            key = m.group(1)
            raw[key] = m.group(2)
        elif key and (line.startswith("  - ") or line.startswith("    ")):
            raw[key] += " " + line.strip()
    return {k: re.sub(r"\s+", " ", v).strip() for k, v in raw.items()}


HANDLE_CLEARANCE = 76 + 16     # the friends drawer's handle (author_menu_friends_drawer.HANDLE) plus air


def check_window_inset(view_text, view_name):
    """Rev 3: a window's Body and Header start right of the friends handle, which draws over every
    window on the left edge - a card under it can be neither read nor pressed. Shared by the four
    windows' generators."""
    V = K.FileIds(view_name)
    for key in ("Body#rt", "Header#rt"):
        _, doc = doc_of(view_text, V(key))
        x = float(re.search(r"m_AnchoredPosition: \{x: (-?[\d.e-]+)", doc).group(1))
        w = float(re.search(r"m_SizeDelta: \{x: (-?[\d.e-]+)", doc).group(1))
        left = x - w / 2          # stretched across the parent with a centre pivot
        assert left >= HANDLE_CLEARANCE, \
            f"{view_name} {key.split('#')[0]} starts {left:g} px from the left edge, under the friends handle (needs {HANDLE_CLEARANCE})"


def drift(files):
    out = []
    for p, t in files.items():
        full = os.path.join(REPO, p)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != t:
            out.append(p)
    return out


def self_test(current, files, guids):
    expected = transform(current, files)
    validate_prefabs(files, guids)
    validate_scene(current, expected, files)
    ids = Ids()
    panel = prefab_path("PartyPanel")
    card = prefab_path("ArcadeGameCard")

    def scene_plant(fn):
        return lambda: validate_scene(current, fn(expected), files)

    def prefab_plant(path, fn):
        f2 = dict(files)
        f2[path] = fn(files[path])
        return lambda: validate_prefabs(f2, guids)

    _, lobby = doc_of(files[panel], K.FileIds("PartyPanel")("root#lobby"))
    slot1 = re.search(r"  slots:\n  - \{fileID: (-?\d+)\}", lobby).group(1)
    _, cbody = doc_of(files[card], K.FileIds("ArcadeGameCard")("root#card"))
    title = re.search(r"GameTitle: \{fileID: (-?\d+)\}", cbody).group(1)
    plants = {
        "the window's entrances stripped": prefab_plant(prefab_path("ArcadeView"), M.strip_entrances),
        "two seats share one slot": prefab_plant(panel, lambda t: re.sub(
            r"(  slots:\n  - \{fileID: (-?\d+)\}\n  - \{fileID: )-?\d+", lambda m: m.group(1) + m.group(2), t, count=1)),
        "card field swapped onto the wrong kind": prefab_plant(card, lambda t: t.replace(
            f"  StarImage: {{fileID:", f"  StarImage: {{fileID: {title}}}\n  StarImageX: {{fileID:", 1)
            .replace(f"  BackgroundImage: {{fileID:", f"  BackgroundImage: {{fileID: {title}}}\n  BackgroundImageX: {{fileID:", 1)),
        "stub names an object missing from KitButtonSmall": prefab_plant(panel, lambda t: re.sub(
            r"(m_CorrespondingSourceObject: \{fileID: )\d+(, guid: " + K.prefab_guid("KitButtonSmall") + ")",
            r"\g<1>4242\2", t, count=1)),
        "lists not embedded": prefab_plant(panel, lambda t: t.replace("  embedded: 1\n", "  embedded: 0\n", 1)),
        "slot field unwired": prefab_plant(panel, lambda t: t.replace(f"  - {{fileID: {slot1}}}\n", "  - {fileID: 0}\n", 1)),
        "Back closes by persistent call": prefab_plant(prefab_path("ArcadeView"), lambda t: in_one(
            t, K.FileIds("ArcadeView")("Back#button"), "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n",
            "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls:\n      - m_Target: {fileID: 0}\n")),
        "ArcadeExploreView.GameSelectionGrid unwired": scene_plant(lambda s: s.replace(
            f"GameSelectionGrid: {{fileID: {ids.stubs['GameSelectionGrid']}}}", "GameSelectionGrid: {fileID: 0}")),
        "a legacy ArcadeExploreView field edited": scene_plant(lambda s: T.in_doc(
            s, EXPLORE["view"], "  ArcadeGameConfigureModal: {fileID: 391329118}", "  ArcadeGameConfigureModal: {fileID: 0}")),
        "QuestGraphRunner misses a card": scene_plant(lambda s: T.in_doc(
            s, RUNNER, f"  - {{fileID: {ids.stubs[ids.cards[-1]]}}}\n", "")),
        "the party panel back in the window": lambda: validate_scene(current, expected, dict(files, **{
            prefab_path("ArcadeView"): files[prefab_path("ArcadeView")] + f"# guid: {asset_guid('PartyPanel')}\n"})),
        "Back first in focus order": prefab_plant(prefab_path("ArcadeView"), lambda t: in_one(
            t, K.FileIds("ArcadeView")("root#rt"),
            f"  - {{fileID: {K.FileIds('ArcadeView')('Body#rt')}}}\n  - {{fileID: {K.FileIds('ArcadeView')('Header#rt')}}}\n",
            f"  - {{fileID: {K.FileIds('ArcadeView')('Header#rt')}}}\n  - {{fileID: {K.FileIds('ArcadeView')('Body#rt')}}}\n")),
        "the body back under the friends handle": prefab_plant(prefab_path("ArcadeView"), lambda t: in_one(
            t, K.FileIds("ArcadeView")("Body#rt"), "m_AnchoredPosition: {x: 20", "m_AnchoredPosition: {x: -20")),
        "unrelated document edited": scene_plant(lambda s: s.replace("  m_Name: Screens\n", "  m_Name: Screenz\n", 1)),
    }
    failed = []
    for name, fn in plants.items():
        try:
            fn()
            failed.append(name)
        except (AssertionError, KeyError) as e:
            print(f"  caught: {name}: {str(e)[:110]}")
    assert not failed, f"validation MISSED planted defects: {failed}"
    print(f"OK: {len(plants)} planted defects all caught")


def in_one(text, fid, old, new):
    """Replace inside one document of a prefab text."""
    m = re.search(r"^--- !u!\d+ &" + str(fid) + r"\n.*?(?=^--- |\Z)", text, re.M | re.S)
    doc = m.group(0)
    assert old in doc
    return text[:m.start()] + doc.replace(old, new, 1) + text[m.end():]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    guids = known_guids()
    files = outputs()
    path = os.path.join(REPO, SCENE)
    with open(path, encoding="utf-8", newline="") as fh:
        current = fh.read()
    if args.self_test:
        self_test(current, files, guids)
        return 0

    validate_prefabs(files, guids)
    expected = transform(current, files)
    validate_scene(current, expected, files)
    migrating = any(T.Scene(current).has(r) for r in RETIRED_ROOTS)

    if args.check:
        problems = drift(files)
        if migrating:
            problems.append(f"{SCENE} (still carries the old Arcade objects)")
        else:
            validate_scene(current, current, files)
            ok, diff = T.semantic_equal(current, expected)
            if not ok:
                problems.append(f"{SCENE} {diff}")
        for p in problems:
            print("DRIFT:", p)
        print(f"FAIL: {len(problems)} output(s) differ" if problems
              else "OK: Arcade / party prefabs and Menu_Main match the generator; wiring validated on the committed scene")
        return 1 if problems else 0

    for d in (PARTY_DIR, ARCADE_DIR):
        os.makedirs(os.path.join(REPO, d), exist_ok=True)
    for p, t in files.items():
        with open(os.path.join(REPO, p), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    if expected != current:
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(expected)
    print(f"wrote {len(files)} prefab files" + ("; migrated Menu_Main's Arcade" if migrating else
                                                "; Menu_Main unchanged" if expected == current else "; updated Menu_Main"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
