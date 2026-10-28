#!/usr/bin/env python3
"""
Author ITEM 3 of the style-B menu redesign - the ARCADE and the SHARED PARTY PANEL - as prefabs,
and wire them into Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 12; the approved Arcade boards
on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Party/        shared by every mode window (D7)
        PartySlot.prefab        one seat of the party molecule (rev 6, section 45.4): KitPartySlot
                                (FriendInfoSlot) - the avatar in its domain ring over the name and role,
                                the kick on its shoulder, over the "Invite a friend" tile
        PartyRequestRow.prefab  one party invite / friend request: KitRequestRow (RequestInfoEntry)
        PartyOnlineRow.prefab   one online pilot: KitOnlineRow (OnlineInfoEntry) - Invite, Join /
                                Spectate, and the cancel / kick icon button
        PartyPanel.prefab       the unified panel: ArcadeLobbyList + FriendsListPanel on one root,
                                the molecule (the leader's seat at the centre, three seats bonded
                                round it), Leave, then the INVITES and ONLINE lists in one scroll
    Assets/_Prefabs/UI Elements/Main Menu Screens/Arcade/
        ArcadeGameCard.prefab   one grid card: KitGameCard (GameCard) on a KitTile
        ArcadeView.prefab       the whole window body: nav dim, backdrop, header with Replays (opens the
                                replay browser, author_menu_replays.py) and Back (B), the
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
import glob
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
KIT_RECT_MASK_2D = H.KIT_RECT_MASK_2D
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
    profile=script("Assets/_Scripts/UI/Elements/Party/FriendProfileHover.cs"),
    request=script("Assets/_Scripts/UI/Elements/Party/KitRequestRow.cs"),
    card=script("Assets/_Scripts/UI/Elements/Arcade/KitGameCard.cs"),
    weekly=script("Assets/_Scripts/UI/Elements/WeeklyChallengeCard.cs"),
    audio=script("Assets/_Scripts/UI/MenuAudio.cs"),
    view=script("Assets/_Scripts/UI/Views/ArcadeExploreView.cs"),
    dock=script("Assets/_Scripts/UI/Elements/Party/FriendsDrawerDock.cs"),
    runner=script("Assets/FTUE/Scripts/Graph/Runtime/QuestGraphRunner.cs"),
    gate=script("Assets/_Scripts/UI/Elements/OfflineUIGate.cs"),
    close=script("Assets/_Scripts/UI/Modals/ModalWindowCloseButton.cs"),
    open_modal=script("Assets/_Scripts/UI/Elements/OpenModalButton.cs"),
    scroll_into=K.kit_script("KitScrollIntoView"),
    counter=K.kit_script("KitListCounter"),
    keeper=K.kit_script("KitFocusKeeper"),
    hover=K.kit_script("KitHoverTint"),
    cover=K.kit_script("KitCoverImage"),
    kt=K.kit_script("KitText"),
    th=K.kit_script("KitThemedGraphic"),
    button=K.kit_script("KitButton"),
    tile=K.kit_script("KitTile"),
    grid_options=script("Assets/_Scripts/UI/Elements/ArcadeGridOptions.cs"),
    text_tab=K.kit_script("KitTextTab"),
)

# Section 90 (Shombith, 2026-10-27: "Favorites to the top by default, A-Z under. Drop the sort controls, the
# locked/unlocked filter and solo/multiplayer. Two dropdowns: elements and vessels"). The order is fixed in
# ArcadeGridOrder; the ELEMENT dropdown is All then Charge..Time (the tab's index is the choice), the VESSEL dropdown is
# All then every hull an arcade card seats, in the Hangar's vessel-list order (vessel_choices()).
ELEMENT_NAMES = ("All", "Charge", "Mass", "Space", "Time")
ARCADE_LIST = "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"
VESSEL_LIST = "Assets/_SO_Assets/Classes/SO_Classlist_Classes.asset"
ANY_VESSEL = -1                                         # VesselClassType.Any: the "All" choice


def vessel_choices():
    """[(name, VesselClassType value)]: All, then each hull some arcade card seats, the Hangar's list order first."""
    by_guid = {}
    for meta in glob.glob(os.path.join(REPO, "Assets/_SO_Assets/**/*.asset.meta"), recursive=True):
        g = re.search(r"^guid: (\w+)", open(meta, encoding="utf-8").read(), re.M)
        if g:
            by_guid[g.group(1)] = meta[:-5]

    def read(guid):
        path = by_guid.get(guid)
        return open(path, encoding="utf-8").read() if path else ""

    def vessel(text_):
        name, cls = re.search(r"^  Name: (.*)$", text_, re.M), re.search(r"^  Class: (-?\d+)$", text_, re.M)
        return (name.group(1).strip(), int(cls.group(1))) if name and cls else None

    seated = set()
    for g in re.findall(r"guid: (\w+)", open(os.path.join(REPO, ARCADE_LIST), encoding="utf-8").read()):
        block = re.search(r"^  Vessels:\n((?:  - .*\n)*)", read(g), re.M)
        for vg in re.findall(r"guid: (\w+)", block.group(1)) if block else []:
            v = vessel(read(vg))
            if v:
                seated.add(v)
    listed = [vessel(read(g)) for g in re.findall(r"guid: (\w+)", open(os.path.join(REPO, VESSEL_LIST), encoding="utf-8").read())]
    order = [v for v in listed if v in seated] + sorted(seated - set(listed), key=lambda v: v[1])
    assert order, "no arcade card seats a vessel (ArcadeGames.asset moved?)"
    return [("All", ANY_VESSEL)] + order


VESSEL_CHOICES = vessel_choices()
VESSEL_NAMES = tuple(n for n, _ in VESSEL_CHOICES)
ALLG_H, OPT_ROW_H = 34, 34

# --- data assets (guids read off the shipped wiring they replace) -------------------------------
CONNECTION_DATA = "69a7bf449c11416dac2d81ebd3ff61f5"     # HostConnectionDataSO
FRIENDS_DATA = "e69f5c7256384d1cb59d57e12551c123"        # FriendsDataSO
PROFILE_ICONS = "3e6cb5b838253c7468480627fd86af5f"       # SO_DefaultProfileIcons
ALL_GAMES = "25a98002ea18f5d45b5e8b2098a06c57"           # OrganicRematchGames - the injected master list
VESSEL_LIST = "49c7c10ad2901c74589c28317a4eb17b"         # SO_VesselList
AVATAR_PLACEHOLDER = "cd2f05a67ee6cb34bb6887c9ba512f83"  # the first default profile icon
MAELSTROM_ASSET = "Assets/_SO_Assets/Games/ArcadeGameMaelstrom.asset"
WEEKLY_PLACEHOLDER_ASSET = "Assets/_SO_Assets/Games/ArcadeGameSkimRace.asset"

REPLAYS_MODAL = 19                                        # ScreenSwitcher.ModalWindows.REPLAYS (Docs/REPLAYS.md)

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
    sc = getattr(node, "scale", 1)    # a uniform scale (the Hangar's and the Codex's content); 1 on nearly every node
    children = ("  m_Children:\n" + "".join(f"  - {{fileID: {c}}}\n" for c in kids)) if kids else "  m_Children: []\n"
    out.append(f"--- !u!224 &{rt}\nRectTransform:\n  m_ObjectHideFlags: 0\n"
               "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
               f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
               f"  m_LocalRotation: {rot_z(deg) if deg else '{x: 0, y: 0, z: 0, w: 1}'}\n"
               "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
               f"  m_LocalScale: {{x: {K.f32(sc)}, y: {K.f32(sc)}, z: 1}}\n  m_ConstrainProportionsScale: 0\n" + children
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
              lock=None, lock_label=None, sel_border="Selection", on_click=(), border_at_rest=True):
    """KitTile, every reference a callable ctx -> fileID. border_at_rest False outlines only the pick and the pointed-at
    tile (KitTile.borderAtRest; omitted when on, its C# default, so every other tile is unchanged)."""
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
                    + ("" if border_at_rest else "  borderAtRest: 0\n")
                    + refs("primaryGraphics", [f(x) for x in primary])
                    + f"  unselectedPrimaryToken: {K.TOKEN_INDEX[unsel_primary]}\n"
                    + refs("secondaryGraphics", [f(x) for x in secondary])
                    + f"  lockedSecondaryToken: {K.TOKEN_INDEX['NeutralLightest']}\n"
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


def profile_hover(source, anchor):
    """FriendProfileHover (rev 7, Docs/MENU_REDESIGN_B.md section 47.3): opens the profile card for `source` (a row or
    seat component) beside `anchor` (the avatar). Both are callables ctx -> fileID."""
    return lambda ctx, go: mono(go, S["profile"], f"  source: {ref(source(ctx))}\n  anchor: {ref(anchor(ctx))}\n")


def profile_hit(parent, key, r, source, anchor):
    """A transparent hit area over an avatar carrying FriendProfileHover: the avatar icon hides when a pilot has no
    picture, so the pointer target is its own graphic."""
    hit = parent.child(N(key, "ProfileHit", r))
    hit.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("profile", 114, profile_hover(source, anchor))
    return hit


def mask(show):
    return lambda ctx, go: mono(go, MASK, f"  m_ShowMaskGraphic: {1 if show else 0}\n")


def rect_mask():
    return lambda ctx, go: mono(go, KIT_RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n")


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
    """A CanvasGroup + LayoutElement wrapper sized by its one child: a part that hides and closes up.

    The slot CONTROLS its child's width, so it reads the button's preferred width (its label) in the same layout pass.
    Left uncontrolled, a layout group takes a child's current rect as its size, which the button's own fitter only sets
    later in the pass: a JOIN that turned into SPECTATE kept JOIN's slot and ran past the drawer's edge (2026-10-26)."""
    n = parent.child(N(key, name, R((0, 0.5), None, (0, 0), (60, 32), (0, 0.5))))
    n.add("cg", 225, canvas_group(shown)).add("le", 114, le(ignore=not shown)) \
        .add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 0, 4, (1, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))
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
ROW_H = 46


# Rev 6 (Docs/MENU_REDESIGN_B.md §45.4): the party is a MOLECULE - the leader's seat (slot 1, the host on every
# device) at the centre and the other three seats bonded round it, each bond lit in its pilot's domain colour.
SOCK_W, SOCK_H = 112, 112      # one seat: the avatar octagon over the name and the role
SOCK_AV = 58                   # the avatar; its domain ring is 6 wider
SOCK_AV_Y = 36                 # the avatar's centre below the seat's top
MOL_R = 120                    # the centre seat's avatar to a member's, canvas units
MOL_ANGLES = (155, 25, 270)    # the three member seats, degrees counter-clockwise from the right
MOL_H = 304
MOL_CY = MOL_H / 2 - 92        # the centre avatar, above the molecule's middle
BOND_H = 14                    # a bond's rect: the core plus its glow


def socket_pos(i):
    """Seat i's avatar centre in the molecule (slot 1 is the centre)."""
    if i == 0:
        return 0.0, MOL_CY
    a = math.radians(MOL_ANGLES[i - 1])
    return round(MOL_R * math.cos(a), 2), round(MOL_CY + MOL_R * math.sin(a), 2)


def oct_nodes(parent, key, size, token, sprite_name="kit_oct_fill", y=-SOCK_AV_Y):
    return image_node(parent, key, key.rsplit("/", 1)[-1], R((0.5, 1), None, (0, y), (size, size)), token,
                      K.kit_sprite(sprite_name))


def build_slot():
    root = N("root", "PartySlot", R(size=(SOCK_W, SOCK_H)))
    # Motion (section 22.8): the seats rise in one after another. The request and online rows are
    # not given one - RequestInfoEntry / OnlineInfoEntry already fade them in (entryFadeInSeconds).
    root.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    occ = root.child(N("Occupied", "Occupied", STRETCH)).add("cg", 225, canvas_group(False))
    oct_nodes(occ, "Occupied/Avatar/Ring", SOCK_AV + 6, "SurfaceDark")
    oct_nodes(occ, "Occupied/Avatar/Gap", SOCK_AV + 2, "SurfaceVeryDark")
    oct_nodes(occ, "Occupied/Avatar/Back", SOCK_AV, "SurfaceDark")
    art_node(occ, "Occupied/Avatar/Icon", "Avatar", R((0.5, 1), None, (0, -SOCK_AV_Y), (SOCK_AV, SOCK_AV)),
             AVATAR_PLACEHOLDER, preserve=True)
    # Rev 7: hovering a member's avatar opens their profile card (FriendProfilePopup); under the kick button.
    profile_hit(occ, "Occupied/Avatar/Hit", R((0.5, 1), None, (0, -SOCK_AV_Y), (SOCK_AV + 6, SOCK_AV + 6)),
                lambda ctx: ctx.c("root", "slot"), lambda ctx: ctx.rt("Occupied/Avatar/Icon"))
    name_top = SOCK_AV_Y + SOCK_AV / 2 + 7
    label_e(occ.child(N("Occupied/Name", "Name", R((0, 1), (1, 1), (0, -name_top), (0, 20), (0.5, 1)))),
            "Pilot", "Body", "TextLight", align="center")
    label_e(occ.child(N("Occupied/Role", "Role", R((0, 1), (1, 1), (0, -name_top - 22), (0, 16), (0.5, 1)))),
            "Pilot", "Label", "NeutralLightest", align="center")
    # The host's kick, on the avatar's shoulder.
    kick = occ.child(N("Occupied/Kick", "Kick", R((0.5, 1), None, (SOCK_AV / 2 + 4, -SOCK_AV_Y + SOCK_AV / 2 + 4), (32, 32))))
    kick.add("cg", 225, canvas_group(False))
    kick.children.append(small_button("KickButton", "KickButton", "Cancel", None, (0.5, 0.5), (0.5, 0.5),
                                      icon="chrome_close"))

    # The open seat: a glass tile, an octagon socket with a + in Space (people), "Invite a friend" under it.
    empty = root.child(N("Empty", "OpenSlot", STRETCH)).add("cg", 225, canvas_group(True))
    ring_node(empty, "Empty/FocusRing", 10)
    fill = empty.child(N("Empty/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark", 0.55), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bord = empty.child(N("Empty/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    oct_nodes(empty, "Empty/PlusRing", SOCK_AV - 8, "NeutralLightest")
    oct_nodes(empty, "Empty/PlusBack", SOCK_AV - 12, "SurfaceBlack")
    plus = empty.child(N("Empty/Plus", "Plus", R((0.5, 1), None, (0, -SOCK_AV_Y), (22, 22))))
    plus.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_plus"), preserve=True))
    lab = empty.child(N("Empty/Label", "Label", R((0, 1), (1, 1), (0, -name_top - 4), (-12, 30), (0.5, 1))))
    lab.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, ellipsis(text("Invite a friend", "Label", tok("NeutralLightest"), align="center"))) \
        .add("kt", 114, K.kit_text("Label", "NeutralLightest", driven=True))
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
    btns.children.append(small_button("Accept", "Accept", "Cta", None, icon="chrome_confirm", added=[row_scroll_into()]))
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
    text_columns(root, 210, "Pilot", "ONLINE", "NeutralLightest")
    # Rev 7: the pilot's profile card opens on their avatar (pointer) and on any of the row's buttons (pad focus).
    row_comp, row_avatar = (lambda ctx: ctx.c("root", "row")), (lambda ctx: ctx.rt("Avatar/Icon"))
    profile_hit(root, "Avatar/Hit", R((0, 0.5), None, (20, 0), (40, 40)), row_comp, row_avatar)
    relay = ("profile", 114, profile_hover(row_comp, row_avatar))
    btns = root.child(N("Buttons", "Buttons", R((1, 0.5), None, (0, 0), (202, 36), (1, 0.5))))
    btns.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 5, (1, 0), (0, 0)))
    for key, caption, variant, shown in (("Invite", "INVITE", "Default", True), ("Join", "JOIN", "Cta", False)):
        slot = wrap_slot(btns, f"{key}Slot", f"{key}Slot", shown)
        slot.children.append(small_button(f"{key}Button", f"{key}Button", variant, caption, added=[row_scroll_into(), relay]))
    cancel = wrap_slot(btns, "CancelSlot", "CancelSlot", False)
    cancel.children.append(small_button("CancelButton", "CancelButton", "Cancel", None, icon="chrome_close",
                                        added=[row_scroll_into(), relay]))
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
OPEN_BOND_ALPHA = 0.35     # an open seat's bond (ArcadeLobbyList.openBondAlpha)
# The window's backdrop is glass, not a slab: the menu scene reads through it (the user, 2026-10-16:
# "I liked the transparent backgrounds way more than the solid black backgrounds"). Shared by the
# Arena, Toy Box and Mission windows.
# 0.55 -> 0.75 on 2026-10-17 (the owner: "make the bg opaque by 20%"): still glass, but Home no longer
# reads through behind a window's title and Back.
# 0.75 -> 0.95 on 2026-10-25 (Shombith, with the sea lines, §67: "make it 95% opaque").
WINDOW_BACKDROP_ALPHA = 0.95
# Rev 3: the four windows' body and header, shared. The left inset is a gutter for the friends control
# drawn over every window (rev 3's 76 px handle on the left edge; since rev 6 the presence orb, ORB_*
# below); the right keeps 60. Vertical: 196 under the top (nav + header), 40 over the bottom.
WINDOW_LEFT, WINDOW_RIGHT = 100, 60
# Rev 6 (§45.4): the friends drawer's presence orb (author_menu_friends_drawer.py) sits in the bottom-left corner over
# every window, inside the 100 px left gutter above: x 4 .. 96, y 44 .. 136 up from the bottom, its count under it.
ORB_X, ORB_Y, ORB_SIZE = 4, 44, 92
WINDOW_BODY = R((0, 0), (1, 1), ((WINDOW_LEFT - WINDOW_RIGHT) / 2, -78), (-(WINDOW_LEFT + WINDOW_RIGHT), -236))
WINDOW_HEADER = R((0, 1), (1, 1), ((WINDOW_LEFT - WINDOW_RIGHT) / 2, -108), (-(WINDOW_LEFT + WINDOW_RIGHT), 64), (0.5, 1))
EXPLORE_W = 1920 - WINDOW_LEFT - WINDOW_RIGHT      # at the 1920 reference width
CONTENT_W = EXPLORE_W - 10 - 12                     # viewport inset, content padding
EMPTY_LINE = "No one else is online right now. Pilots who come online show here, ready to invite."


DOCK_GAP = 24


def drawer_dock(root):
    """§68: the hub window makes room for the open friends drawer - its Header and Body start right of it
    (FriendsDrawerDock), so the cards move right. Shared by the Arcade, Arena, Mission and Toy Box windows."""
    root.add("dock", 114, lambda ctx, go: mono(go, S["dock"],
             f"  pushed:\n  - {ref(ctx.rt('Header'))}\n  - {ref(ctx.rt('Body'))}\n  gap: {DOCK_GAP}\n"))


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
    # Rev 7 (the owner, 2026-10-17: "not the blue bg make it more interesting like the lattice bg"): no fill of its
    # own - the friends drawer's window behind it draws the living lattice through glass.
    H.framed(root, "Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.0)
    inner = root.child(N("Inner", "Inner", R((0, 0), (1, 1), (0, -2), (-44, -40))))
    inner.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 14, 0, (1, 1), (1, 0)))

    top = inner.child(N("Top", "Top", R((0, 1), None, (0, 0), (ROW_W, 46), (0, 1))))
    top.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 8, 0, (1, 1), (1, 0))).add("le", 114, le(ph=46))
    head = top.child(N("Top/Header", "Header", R((0, 1), None, (0, 0), (ROW_W, 18), (0, 1)))).add("le", 114, le(ph=18))
    label(head.child(N("Top/Header/Label", "Label", STRETCH)), "Your party", "Label", "NeutralLightest")
    label(head.child(N("Top/Header/Count", "Count", STRETCH)), "1 / 4", "Label", "NeutralLightest", align="right")
    online = top.child(N("Top/Online", "OnlineCount", R((0, 1), None, (0, -26), (ROW_W, 20), (0, 1))))
    online.add("le", 114, le(ph=20)).add("hlg", 114, K.hv_layout(False, (1, 0, 0, 0), 10, 3, (0, 0), (0, 0)))
    diamond = image_node(online, "Top/Online/Diamond", "Diamond", R((0, 0.5), None, (5, 0), (8, 8)), "Cta")
    diamond.rot = 45
    label(online.child(N("Top/Online/Text", "Text", R((0, 0.5), None, (20, 0), (300, 20), (0, 0.5)))),
          "1 Player Online", "BodySmall", "NeutralLightest")

    # The molecule (§45.4): bonds first so the seats draw over their ends, then the four seats.
    mol = inner.child(N("Molecule", "Molecule", R((0, 1), None, (0, 0), (ROW_W, MOL_H), (0, 1))))
    mol.add("le", 114, le(ph=MOL_H))
    for i, angle in enumerate(MOL_ANGLES):
        bond = mol.child(N(f"Bond{i + 2}", f"Bond{i + 2}", R((0.5, 0.5), None, socket_pos(0), (MOL_R, BOND_H), (0, 0.5))))
        bond.rot = angle
        bond.add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, M.material_image(tok("NeutralLightest", OPEN_BOND_ALPHA), M.BOND_MAT)) \
            .add("rim", 114, lambda ctx, go: mono(go, M.RIM_SCRIPT, ""))
    for i in range(4):
        x, y = socket_pos(i)
        mol.children.append(Inst(f"Slot{i + 1}", "PartySlot", f"Slot{i + 1}",
                                 R((0.5, 0.5), None, (x, round(y - (SOCK_H / 2 - SOCK_AV_Y), 2)), (SOCK_W, SOCK_H))))

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
             "  onlineAloneText: 1 Player Online\n  onlineCountTemplate: '{count} Players Online'\n"
             f"  leaveButton: {ref(ctx.iref('LeaveButton', 'root#button'))}\n"
             f"  friendsListPanel: {ref(ctx.c('root', 'friends'))}\n"
             f"  partyCountText: {ref(ctx.c('Top/Header/Count', 'kt'))}\n"
             f"  leaveGroup: {ref(ctx.c('Leave', 'cg'))}\n"
             + refs("seatBonds", [0] + [ctx.c(f"Bond{i + 2}", "img") for i in range(3)])
             + f"  openBondAlpha: {OPEN_BOND_ALPHA}\n  theme: {K.ref(11400000, K.THEME, 2)}\n"))
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

CARD_W, CARD_H = 341, 296
# Rev 18 A (Docs/MENU_REDESIGN_B.md section 72, Shombith 2026-10-25 "implement a"): the art in a band on top, an opaque body
# under it - the hull, the name, the genre petals. Nothing is written on the art. The art drifts slowly (Card_ArtDrift.mat)
# inside a margin KitCoverImage draws it larger by. Section 73: the hull is lit (Hull_Lit.mat) instead of sitting on a
# pale plate, and the body carries the sea lines (Sea_Panel.mat).
ART_BAND_H = 200
BODY_H = CARD_H - ART_BAND_H
ART_OVERSCAN = 1.05
ART_DRIFT_MAT = M._lattice_guid("card_art_drift_mat")
PORTRAIT_BOB_MAT = M._lattice_guid("card_portrait_bob_mat")
ART_DRIFT_REACH = (5, 3)               # author_ui_lattice.ART_DRIFT: must stay inside the overscan margin
HULL_ICON = 60                         # the hull, lit (Hull_Lit.mat) - no plate behind it (section 73)
HULL_LIT_MAT = M._lattice_guid("hull_lit_mat")
FLOWER = 34                            # the petals reach 0.42 of the sprite from its centre: the flower reads about 29 across
GENRE_SHAPE = 24                       # the genre's element shape (ElementShapes/*_label.png), inside the 34 px slot
GENRE_SHAPES = [K.meta_guid(f"Assets/_Graphics/ElementShapes/{e}_label.png")        # GameCard.GenreShapes: Charge..Time
                for e in ("charge_pentagon", "mass_triangle", "space_kite", "time_diamond")]


def drift_art(parent, key, name, r, sprite, material=None, preserve=False):
    """An art Image drawn with one of the card picture materials (CardDrift.shader)."""
    n = art_node(parent, key, name, r, sprite, preserve=preserve)
    mat = material or ART_DRIFT_MAT
    n.comps = [(k, cid, (lambda body: (lambda ctx, go: body(ctx, go).replace(
        "  m_Material: {fileID: 0}\n", f"  m_Material: {{fileID: 2100000, guid: {mat}, type: 2}}\n", 1)))(b))
               if k == "img" else (k, cid, b) for k, cid, b in n.comps]
    return n


def art_band(parent, key, w, h, sprite):
    """The card's art band: a RectMask2D the art drifts under, the art fitted COVER x ART_OVERSCAN."""
    band = parent.child(N(f"{key}", "ArtBand", R((0, 1), (1, 1), (0, -h / 2), (0, h), (0.5, 0.5))))
    band.add("mask", 114, rect_mask()) \
        .add("cover", 114, lambda ctx, go: mono(go, S["cover"], f"  image: {ref(ctx.c(key + '/Art', 'img'))}\n"
                                                                f"  overscan: {ART_OVERSCAN}\n"))
    drift_art(band, f"{key}/Art", "Art", R(size=(w * ART_OVERSCAN, w * ART_OVERSCAN * 510 / 768)), sprite)
    return band


def hull_icon(parent, key, r, icon_key, icon_size):
    """The hull with no plate behind it (section 73, Shombith 2026-10-25: "dont like the white bg"): the near-black render
    drawn with Hull_Lit.mat - lifted toward a cool grey and edged in white - so it reads on the navy body. `key` is a
    plain holder (the arena chip's CanvasGroup and LayoutElement go on it); the icon is its `Icon` / `VesselIcon` child."""
    holder = parent.child(N(key, "Hull", r))
    drift_art(holder, icon_key, "VesselIcon" if icon_key == "VesselIcon" else "Icon", R(size=(icon_size, icon_size)), None,
              material=HULL_LIT_MAT, preserve=True)
    return holder


def body_sea(parent, key, body_h):
    """The sea inside a card's body (section 73: "the blue part can have the sea lines"): Sea_Panel.mat over the body,
    under the words. Inside the card's clip, so the cut corner crops it."""
    sea = parent.child(N(key, "Sea", R((0, 0), (1, 0), (0, body_h / 2), (0, body_h), (0.5, 0.5))))
    sea.add("cr", 222, K.canvas_renderer()).add("img", 114, M.lattice_image(M.SEA_PANEL_MAT))
    return sea


def build_card(placeholder_art):
    root = N("root", "ArcadeGameCard", R(size=(CARD_W, CARD_H)))
    ring_node(root, "FocusRing", 14)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    # The card's cut-corner shape clips everything (stencil); the band clips the drifting art to the top.
    clip = root.child(N("Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-2, -2))))
    clip.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("mask", 114, mask(False))
    art_band(clip, "Clip/Band", CARD_W, ART_BAND_H, placeholder_art)
    body_sea(clip, "Clip/Sea", BODY_H)
    image_node(root, "Seam", "Seam", R((0, 0), (1, 0), (0, BODY_H), (-2, 1), (0.5, 0.5)), "SurfaceDark")
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))

    # The body: the hull, the name, the petals.
    hull_icon(root, "VesselChip", R((0, 0), None, (12, BODY_H / 2), (HULL_ICON, HULL_ICON), (0, 0.5)), "VesselIcon", HULL_ICON)
    text_x = 12 + HULL_ICON + 12
    flowers_w = 12 + FLOWER + 8
    title = root.child(N("Title", "Title", R((0, 0), (1, 0), ((text_x - flowers_w) / 2, BODY_H / 2), (-(text_x + flowers_w), 30),
                                             (0.5, 0.5))))
    title.add("cr", 222, K.canvas_renderer()).add("tmp", 114, ellipsis(text("Game", "H3", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H3", "TextLight", driven=True))
    # Section 90 (Shombith 2026-10-27: "Arcade shows a single petal, not per-vessel upgrade numbers"): the genre's ONE
    # element icon (GameCard.GenrePetal, in white), not the flower filled to a hull's bought level. On
    # the body's right edge, small: a column centred in the body, so one petal sits in the middle and a two-genre mode's
    # second stacks under it (GameCard hides an empty petal by disabling its image).
    col = root.child(N("Flowers", "Flowers", R((1, 0), None, (-12, BODY_H / 2), (FLOWER, BODY_H - 8), (1, 0.5))))
    col.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 4, 4, (0, 0), (0, 0)))
    for key in ("Petal", "PetalSecondary"):
        slot = col.child(N(key, key, R(size=(FLOWER, FLOWER))))
        slot.add("le", 114, le(mw=FLOWER, mh=FLOWER, pw=FLOWER, ph=FLOWER))
        # 2026-10-28 (Shombith: "the arcade cards again are using the old elelmental icons"): the image draws the
        # element's SHAPE (GameCard.GenreShapes, the Codex's and the ability rows' icon), centred; the shape fills 0.95
        # of its sprite, so it is drawn at 0.7 of the slot.
        image_node(slot, f"{key}/Image", "Image", R(size=(GENRE_SHAPE, GENRE_SHAPE)), "TextLight", None, preserve=True)

    # On the art: who in the party asked for it (top left) and the favourite star (top right, on a dark disc).
    picks = root.child(N("Picks", "AvatarSpace", R((0, 1), None, (12, -12), (200, 24), (0, 1))))
    picks.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 4, 3, (0, 0), (0, 0)))
    pick = picks.child(N("Picks/Pick", "AvatarIcon", R(size=(24, 24))))
    pick.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 1.0), AVATAR_PLACEHOLDER, preserve=True)) \
        .add("cg", 225, canvas_group(False)).add("le", 114, le(ignore=True, pw=24, ph=24))
    star = root.child(N("Star", "FavoriteStar", R((1, 1), None, (-6, -6), (44, 44), (1, 1))))
    star.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    image_node(star, "Star/Disc", "Disc", R(size=(32, 32)), "SurfaceBlack", K.kit_sprite("kit_oct_fill"), alpha=0.7, preserve=True)
    glyph = image_node(star, "Star/Glyph", "Glyph", R(size=(20, 20)), "NeutralLightest", K.chrome_sprite("chrome_star"),
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

    # Locked: the lock and its word in the middle of the art.
    lock = root.child(N("Lock", "Lock", R(pos=(0, CARD_H / 2 - ART_BAND_H / 2 + 12), size=(30, 30))))
    lock.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight", 0.0), K.chrome_sprite("chrome_lock"),
                                                                   preserve=True))
    lab = root.child(N("LockLabel", "LockLabel", R(pos=(0, CARD_H / 2 - ART_BAND_H / 2 - 16), size=(200, 18))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("LOCKED", "Label", tok("TextLight", 0.0), align="center")) \
        .add("kt", 114, K.kit_text("Label", "TextLight", driven=True))

    root.add("tile", 114, tile_body(c("FocusRing", "img"), c("Fill", "img"), c("Border", "img"), 14,
                                    primary=[c("Title", "tmp")], art=[c("Clip/Band/Art", "img"), c("VesselIcon", "img")],
                                    lock=c("Lock", "img"), lock_label=c("LockLabel", "tmp"), sel_border="Selection",
                                    on_click=[lambda ctx: (ctx.c("root", "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")]))
    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  AllGames: {K.ref(11400000, ALL_GAMES, 2)}\n"
             f"  StarIconActive: {K.sprite_ref(K.chrome_sprite('chrome_star_fill'))}\n"
             f"  StarIconInActive: {K.sprite_ref(K.chrome_sprite('chrome_star'))}\n"
             "  ExploreView: {fileID: 0}\n"
             f"  GameTitle: {ref(ctx.c('Title', 'tmp'))}\n"
             f"  BackgroundImage: {ref(ctx.c('Clip/Band/Art', 'img'))}\n"
             f"  StarImage: {ref(ctx.c('Star/Glyph', 'img'))}\n"
             "  Index: 0\n"
             f"  VesselIcon: {ref(ctx.c('VesselIcon', 'img'))}\n"
             f"  VesselList: {K.ref(11400000, VESSEL_LIST, 2)}\n"
             f"  GenrePetal: {ref(ctx.c('Petal/Image', 'img'))}\n"
             f"  GenrePetalSecondary: {ref(ctx.c('PetalSecondary/Image', 'img'))}\n"
             "  GenreShapes:\n" + "".join(f"  - {K.ref(21300000, g, 3)}\n" for g in GENRE_SHAPES) +
             "  GenreFlower: {fileID: 0}\n"
             "  GenreFlowerSecondary: {fileID: 0}\n"
             f"  AvatarSpace: {ref(ctx.rt('Picks'))}\n"
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


def back_width():
    return 16 + (K.label_width("B", "Label") + 24) + 10 + K.label_width("BACK", "ButtonSmall") + 16


def back_button(parent):
    w_label = K.label_width("BACK", "ButtonSmall")
    keycap_w = K.label_width("B", "Label") + 24
    w = back_width()
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


def replays_button(parent):
    """Replays, left of Back: opens the replay browser (ModalWindows.REPLAYS) through OpenModalButton, which
    names the window by TYPE and lets the ScreenSwitcher find it (Docs/REPLAYS.md, author_menu_replays.py)."""
    w_label = K.label_width("REPLAYS", "ButtonSmall")
    b = parent.child(N("Replays", "Replays", R((1, 0), None, (-(back_width() + 12), 0), (16 + w_label + 16, 40), (1, 0))))
    ring_node(b, "Replays/FocusRing", 10)
    border, fillc, content = K.button_palette("Default", False)
    f = b.child(N("Replays/Fill", "Fill", STRETCH))
    f.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)).add("le", 114, le(ignore=True))
    bd = b.child(N("Replays/Border", "Border", STRETCH))
    bd.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)).add("le", 114, le(ignore=True))
    lab = b.child(N("Replays/Label", "Label", R(size=(w_label, K.line_height("ButtonSmall")))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp("REPLAYS", "ButtonSmall", content)) \
        .add("kt", 114, K.kit_text("ButtonSmall", driven=True))
    b.add("button", 114, lambda ctx, go: mono(go, S["button"],
          K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c('Replays/Fill', 'img'))}\n" + calls([])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c('Replays/FocusRing', 'img'))}\n"
          "  variant: 0\n  fill: 0\n"
          f"  fillGraphic: {ref(ctx.c('Replays/Fill', 'img'))}\n  borderGraphic: {ref(ctx.c('Replays/Border', 'img'))}\n"
          + refs("contentGraphics", [ctx.c("Replays/Label", "tmp")]) + "  playGradient: {fileID: 0}\n"))
    b.add("open", 114, lambda ctx, go: mono(go, S["open_modal"], f"  target: {REPLAYS_MODAL}\n  screenSwitcher: {{fileID: 0}}\n"))
    b.add("hlg", 114, K.hv_layout(False, (16, 16, 9, 9), 10, 4, (0, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))
    return b


def all_games_header(content):
    """ALL GAMES and its count on the left; the ELEMENT and VESSEL dropdowns on the right (sections 85, 97). Each button
    names its choice ("Vessel: Sparrow") and opens its list under this line; ArcadeGridOptions owns the lists and the
    choice."""
    allg = content.child(N("AllGames", "AllGames", R((0, 1), None, (0, 0), (CONTENT_W, ALLG_H), (0, 1))))
    allg.add("le", 114, le(ph=ALLG_H))
    lw = K.label_width("ALL GAMES", "Label")
    label(allg.child(N("AllGames/Label", "Label", R((0, 0.5), None, (0, 0), (lw + 4, 18), (0, 0.5)))), "ALL GAMES", "Label",
          "TextLight")
    label(allg.child(N("AllGames/Count", "Count", R((0, 0.5), None, (lw + 16, 0), (60, 18), (0, 0.5)))), "28", "Label",
          "NeutralLightest")
    opts = allg.child(N("AllGames/Options", "Options", R((1, 0.5), None, (-10, 0), (520, ALLG_H), (1, 0.5))))
    opts.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 5, (1, 0), (0, 0)))
    for key, caption in (("Element", f"Element: {ELEMENT_NAMES[0]}"), ("Vessel", f"Vessel: {VESSEL_NAMES[0]}")):
        slot = wrap_slot(opts, f"AllGames/Options/{key}Slot", f"{key}Slot")
        slot.children.append(small_button(f"{key}Button", f"{key}Button", "Default", caption))
    longest = max(K.button_size(f"Element: {n}", small=True)[0] for n in ELEMENT_NAMES) + 10 \
        + max(K.button_size(f"Vessel: {n}", small=True)[0] for n in VESSEL_NAMES)
    assert longest <= 520 and lw + 16 + 60 + 520 + 10 <= CONTENT_W, "ELEMENT and VESSEL do not fit beside ALL GAMES"
    allg.add("options", 114, lambda ctx, go: mono(go, S["grid_options"],
             f"  elementButton: {ref(ctx.iref('ElementButton', 'root#button'))}\n"
             f"  elementLabel: {ref(ctx.iref('ElementButton', 'Label#tmp'))}\n"
             "  elementCaption: 'Element: {choice}'\n"
             "  elementNames:\n" + "".join(f"  - {n}\n" for n in ELEMENT_NAMES)
             + f"  elementRow: {ref(ctx.c('ElementRow', 'cg'))}\n"
             + refs("elementTabs", [ctx.c(f"ElementRow/Tab{i}", "tab") for i in range(len(ELEMENT_NAMES))])
             + f"  vesselButton: {ref(ctx.iref('VesselButton', 'root#button'))}\n"
             f"  vesselLabel: {ref(ctx.iref('VesselButton', 'Label#tmp'))}\n"
             "  vesselCaption: 'Vessel: {choice}'\n"
             "  vesselNames:\n" + "".join(f"  - {n}\n" for n in VESSEL_NAMES)
             + "  vesselChoices:\n"
             + "".join(f"  - {c}\n" for _, c in VESSEL_CHOICES)
             + f"  vesselRow: {ref(ctx.c('VesselRow', 'cg'))}\n"
             + refs("vesselTabs", [ctx.c(f"VesselRow/Tab{i}", "tab") for i in range(len(VESSEL_NAMES))])))


def option_row(content, key, names):
    """A row of option tabs under ALL GAMES, hidden (out of the layout) until its button opens it; the chosen one is
    underlined in the selection blue (KitTextTab)."""
    row = content.child(N(key, key, R((0, 1), None, (0, 0), (CONTENT_W, OPT_ROW_H), (0, 1))))
    row.add("cg", 225, canvas_group(False)).add("le", 114, le(ph=OPT_ROW_H, ignore=True)) \
        .add("hlg", 114, K.hv_layout(False, (0, 0, 2, 2), 14, 3, (0, 0), (0, 0)))
    for i, n in enumerate(names):
        H.text_tab(row, f"{key}/Tab{i}", f"Tab{i}", n.upper(), "Label", K.label_width(n.upper(), "Label") + 16, 30)
    width = sum(K.label_width(n.upper(), "Label") + 16 for n in names) + 14 * (len(names) - 1)
    assert width <= CONTENT_W, f"{key}: the options run past the grid's width ({width})"


def build_view(maelstrom_art, maelstrom_desc, weekly_art):
    root = N("root", "ArcadeView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", alpha=WINDOW_BACKDROP_ALPHA)
    # §67: the sea lines over the glass (Sea_Window.mat), slow wave lines in three depth layers.
    root.child(M.window_sea_node("BackdropLattice", rect=R((0, 0), (1, 1), (0, -42), (0, -84))))

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

    wk = feature_card(feats, "Weekly", "WeeklyChallenge", weekly_art, "WEEKLY CHALLENGE", "NeutralLightest", "Skim Race",
                      weekly_extra)
    wk.add("weekly", 114, lambda ctx, go: mono(go, S["weekly"],
           f"  GameTitle: {ref(ctx.c('Weekly/Title', 'tmp'))}\n"
           f"  TimeRemaining: {ref(ctx.c('Weekly/Countdown', 'tmp'))}\n"
           f"  BackgroundImage: {ref(ctx.c('Weekly/Clip/Art', 'img'))}\n"
           "  ObjectiveText: {fileID: 0}\n  StatusText: {fileID: 0}\n  CompletedBadge: {fileID: 0}\n"))
    feature_card(feats, "Maelstrom", "Maelstrom", maelstrom_art, "GAUNTLET", "ControlActive", "Maelstrom", mael_extra)

    content.child(N("Gap1", "Gap", R((0, 1), None, (0, 0), (CONTENT_W, 18), (0, 1)))).add("le", 114, le(ph=18))
    all_games_header(content)
    for key, names in (("ElementRow", ELEMENT_NAMES), ("VesselRow", VESSEL_NAMES)):
        option_row(content, key, names)
    content.child(N("Gap2", "Gap", R((0, 1), None, (0, 0), (CONTENT_W, 10), (0, 1)))).add("le", 114, le(ph=10))
    empty = content.child(N("EmptyFilter", "EmptyFilter", R((0, 1), None, (0, 0), (CONTENT_W, 24), (0, 1)), active=False))
    empty.add("le", 114, le(ph=24))
    label(empty.child(N("EmptyFilter/Line", "Line", STRETCH)), "No games match this filter.", "BodySmall", "NeutralLightest")

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
    drawer_dock(root)
    header.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(header.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (300, 18), (0, 1)))), "ARCADE", "Label",
          "NeutralLightest")
    label(header.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (760, 46), (0, 1)))), "Explore games", "H1",
          "TextLight")
    replays_button(header)
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

VIEW_OWNED = ("GameSelectionView", "GameSelectionGrid", "WeeklyChallengeCard", "maelstromCard", "gameCountText",
              "gridOptions", "emptyFilterLine")
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
            "gridOptions": (V("AllGames#options"), 114, S["grid_options"]),
            "emptyFilterLine": (V("EmptyFilter#go"), 1, None),
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
             "maelstromCard", "gameCountText", "gridOptions", "emptyFilterLine", "ArcadeGameConfigureModal",
             "selectedVesselClassType",
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
    bad = re.search(r"^ *(?:value|m_text): ([^'\"{\[\n][^\n]*?: [^\n]*)$", text, re.M)
    assert not bad, f"{path}: an unquoted ': ' in a scalar breaks the YAML ({bad.group(1)[:40]})"


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


def _num(doc, pattern):
    m = re.search(pattern, doc)
    assert m, f"no {pattern} in {doc[:80]!r}"
    return float(m.group(1))


def check_molecule(panel, lobby):
    """Rev 6 (§45.4), the party molecule as shipped: ArcadeLobbyList.seatBonds holds the centre seat's empty entry then
    three bonds; each bond is drawn with the bond material and KitRimMesh (the shader reads UV1), starts at the centre
    seat's avatar and, along its own rotation, ends at its member seat's avatar."""
    V = K.FileIds("PartyPanel")
    bonds = re.findall(r"^  - \{fileID: (-?\d+)\}", lobby.split("  seatBonds:\n")[1].split("  openBondAlpha")[0], re.M)
    assert len(bonds) == 4 and bonds[0] == "0" and len(set(bonds[1:])) == 3, \
        f"ArcadeLobbyList.seatBonds must be the centre's empty entry then three distinct bonds, not {bonds}"

    def avatar_centre(slot):
        _, inst = doc_of(panel, V(f"{slot}#inst"))
        x = _num(inst, r"propertyPath: m_AnchoredPosition\.x\n\s+value: (-?[\d.]+)")
        y = _num(inst, r"propertyPath: m_AnchoredPosition\.y\n\s+value: (-?[\d.]+)")
        return x, y + (SOCK_H / 2 - SOCK_AV_Y)
    centre = avatar_centre("Slot1")
    for i, fid in enumerate(bonds[1:]):
        key = f"Bond{i + 2}"
        assert fid == str(V(f"{key}#img")), f"seatBonds[{i + 1}] is not {key}"
        _, img = doc_of(panel, fid)
        assert f"guid: {M.BOND_MAT}" in img, f"{key} is not drawn with the bond material"
        _, go = doc_of(panel, V(f"{key}#go"))
        assert any(f"guid: {M.RIM_SCRIPT}" in (doc_of(panel, c)[1] or "")
                   for c in re.findall(r"component: \{fileID: (-?\d+)\}", go)), f"{key} has no KitRimMesh (the bond shader reads UV1)"
        _, rt = doc_of(panel, V(f"{key}#rt"))
        start = (_num(rt, r"m_AnchoredPosition: \{x: (-?[\d.e-]+)"), _num(rt, r"m_AnchoredPosition: \{x: -?[\d.e-]+, y: (-?[\d.e-]+)"))
        assert abs(start[0] - centre[0]) < 0.5 and abs(start[1] - centre[1]) < 0.5, f"{key} does not start at the centre seat's avatar"
        a = math.radians(_num(rt, r"m_LocalEulerAnglesHint: \{x: 0, y: 0, z: (-?[\d.e-]+)"))
        length = _num(rt, r"m_SizeDelta: \{x: (-?[\d.e-]+)")
        end = (start[0] + length * math.cos(a), start[1] + length * math.sin(a))
        seat = avatar_centre(f"Slot{i + 2}")
        assert math.hypot(end[0] - seat[0], end[1] - seat[1]) < 1.0, f"{key} ends at {end}, not at Slot{i + 2}'s avatar {seat}"
    assert "  onlineCountTemplate: '{count} Players Online'\n" in lobby, "the online line lost its authored template"


def check_profile_hovers(files):
    """Rev 7 (section 47.3): the profile card opens from the online row's avatar and from each of its three buttons
    (pad focus), and from a seat's avatar; every relay names its own row or seat and that pilot's avatar."""
    for name, comp, avatar, wanted in (("PartyOnlineRow", "root#row", "Avatar/Icon#rt", 4),
                                       ("PartySlot", "root#slot", "Occupied/Avatar/Icon#rt", 1)):
        text, F = files[prefab_path(name)], K.FileIds(name)
        want = f"  source: {{fileID: {F(comp)}}}\n  anchor: {{fileID: {F(avatar)}}}\n"
        relays = [m.group(0) for m in re.finditer(r"^--- !u!114 &-?\d+\n(?:(?!^--- ).*\n)*", text, re.M)
                  if f"guid: {S['profile']}, type: 3}}" in m.group(0)]
        assert len(relays) == wanted, f"{name} has {len(relays)} profile relays, not {wanted} (avatar + pad-focus buttons)"
        for r in relays:
            assert want in r, f"a {name} profile relay does not name its own {comp.split('#')[1]} and avatar"
    hit = doc_of(files[prefab_path("PartyOnlineRow")], K.FileIds("PartyOnlineRow")("Avatar/Hit#img"))[1]
    assert "  m_RaycastTarget: 1\n" in hit, "the online row's avatar hit area does not take the pointer"


def check_card_a(card, name, band_key, card_w, band_h):
    """Rev 18 A (section 72): the art drifts (Card_ArtDrift.mat) under a band that crops it, the drift never shows an edge;
    section 73: the hull is lit (no plate) and the body carries the sea. Shared with author_menu_arena.py."""
    F = K.FileIds(name)
    _, art = doc_of(card, F(f"{band_key}/Art#img"))
    assert art and f"m_Material: {{fileID: 2100000, guid: {ART_DRIFT_MAT}, type: 2}}" in art, \
        f"{name}: the card art does not drift (Card_ArtDrift.mat)"
    _, band = doc_of(card, F(f"{band_key}#cover"))
    m = band and re.search(r"^  overscan: ([\d.]+)$", band, re.M)
    assert m, f"{name}: the art band has no overscan - the drifting art would show its edge"
    ov = float(m.group(1))
    fit_w = max(card_w, band_h * 768 / 510) * ov
    fit_h = max(card_w * 510 / 768, band_h) * ov
    assert (fit_w - card_w) / 2 > ART_DRIFT_REACH[0] and (fit_h - band_h) / 2 > ART_DRIFT_REACH[1], \
        f"{name}: overscan {ov} leaves less margin than the drift's reach {ART_DRIFT_REACH} - an edge would show"
    _, mk = doc_of(card, F(f"{band_key}#mask"))
    assert mk and KIT_RECT_MASK_2D in mk, f"{name}: the art band does not crop the drifting art"
    assert f"guid: {HULL_LIT_MAT}" in card, f"{name}: the hull is not lit (Hull_Lit.mat) - it vanishes on the navy body"
    assert f"guid: {M.SEA_PANEL_MAT}" in card, f"{name}: the body has no sea lines (Sea_Panel.mat)"


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
    check_molecule(panel, lobby)
    _, friends = doc_of(panel, K.FileIds("PartyPanel")("root#friends"))
    for field, name in (("onlineInfoPrefab", "PartyOnlineRow"), ("requestInfoPrefab", "PartyRequestRow")):
        want = f"{field}: {{fileID: {K.FileIds(name)('root#row')}, guid: {asset_guid(name)}"
        assert want in friends, f"FriendsListPanel.{field} does not name the {name} row component"
    assert "  embedded: 1\n" in friends and "  listPartyMembers: 0\n" in friends, "the panel's lists must be embedded"
    # A button slot must size its button from the label, or a JOIN that becomes SPECTATE runs out of the row.
    row = files[prefab_path("PartyOnlineRow")]
    for slot in ("InviteSlot", "JoinSlot", "CancelSlot"):
        _, hlg = doc_of(row, K.FileIds("PartyOnlineRow")(f"{slot}#hlg"))
        assert "  m_ChildControlWidth: 1\n" in hlg, f"PartyOnlineRow {slot} must control its button's width"
    inv, join = (K.button_size(w, small=True)[0] for w in ("INVITE", "SPECTATE"))
    assert inv + 8 + join <= 202, f"INVITE + SPECTATE ({inv + 8 + join:.0f}) do not fit the row's button strip"
    check_profile_hovers(files)

    check_card_a(files[prefab_path("ArcadeGameCard")], "ArcadeGameCard", "Clip/Band", (CONTENT_W - (GRID_COLS - 1) * 10) / GRID_COLS,
                 ART_BAND_H)
    # 2026-10-28: the genre icon is the element's shape (Charge, Mass, Space, Time), never the lone HUD petal.
    _, card = doc_of(files[prefab_path("ArcadeGameCard")], K.FileIds("ArcadeGameCard")("root#card"))
    shapes = re.search(r"^  GenreShapes:\n((?:  - .*\n)*)", card, re.M)
    assert shapes and re.findall(r"guid: (\w+)", shapes.group(1)) == GENRE_SHAPES, \
        "GameCard.GenreShapes must name the four ElementShapes icons, Charge to Time"

    view = files[prefab_path("ArcadeView")]
    V = K.FileIds("ArcadeView")
    # Sections 85 and 90: ELEMENT and VESSEL reach their buttons, labels and lists, with one tab per choice in order;
    # no sort control is left.
    _, opts = doc_of(view, V("AllGames#options"))
    assert opts, "ArcadeView has no ArcadeGridOptions on ALL GAMES"
    assert "sortButton" not in opts and "m_Name: SortRow" not in view and "m_text: 'Sort:" not in view, \
        "the sort control is retired: favourites first, A-Z under (section 97)"
    for field in ("elementButton", "elementLabel", "elementRow", "vesselButton", "vesselLabel", "vesselRow"):
        assert re.search(r"^  " + field + r": \{fileID: -?[1-9]\d*\}", opts, re.M), f"ArcadeGridOptions.{field} is unwired"
    assert "  vesselChoices:\n" + "".join(f"  - {c}\n" for _, c in VESSEL_CHOICES) in opts, \
        "ArcadeGridOptions.vesselChoices must match the vessel tabs, All (-1) first"
    for field, names, row in (("elementTabs", ELEMENT_NAMES, "ElementRow"), ("vesselTabs", VESSEL_NAMES, "VesselRow")):
        block = re.search(r"^  " + field + r":\n((?:  - \{fileID: -?\d+\}\n)*)", opts, re.M)
        tabs = re.findall(r"fileID: (-?\d+)", block.group(1)) if block else []
        assert tabs == [str(V(f"{row}/Tab{i}#tab")) for i in range(len(names))], \
            f"ArcadeGridOptions.{field} must list the {row} tabs in enum order"
        assert f"  {field[:-4]}Names:\n" + "".join(f"  - {n}\n" for n in names) in opts, f"ArcadeGridOptions.{field[:-4]}Names drifted"
    for row in ("ElementRow", "VesselRow"):
        assert "  m_Alpha: 0\n" in doc_of(view, V(f"{row}#cg"))[1] and "  m_IgnoreLayout: 1\n" in doc_of(view, V(f"{row}#le"))[1], \
            f"{row} must start closed and out of the layout"
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
    cls, opener = doc_of(view, V("Replays#open"))
    assert cls == 114 and S["open_modal"] in opener, "Replays has no OpenModalButton"
    assert f"  target: {REPLAYS_MODAL}\n" in opener, "Replays must open ModalWindows.REPLAYS (19)"
    assert "m_Calls: []" in doc_of(view, V("Replays#button"))[1], "Replays must open through OpenModalButton, not a persistent call"


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


ORB_CLEARANCE = ORB_X + ORB_SIZE + 4     # the friends orb (ORB_*) plus air


def check_window_inset(view_text, view_name):
    """Rev 3 / rev 6: a window's Body and Header start right of the friends orb, which draws over every
    window in the bottom-left corner - a card under it can be neither read nor pressed. Shared by the four
    windows' generators."""
    V = K.FileIds(view_name)
    for key in ("Body#rt", "Header#rt"):
        _, doc = doc_of(view_text, V(key))
        x = float(re.search(r"m_AnchoredPosition: \{x: (-?[\d.e-]+)", doc).group(1))
        w = float(re.search(r"m_SizeDelta: \{x: (-?[\d.e-]+)", doc).group(1))
        left = x - w / 2          # stretched across the parent with a centre pivot
        assert left >= ORB_CLEARANCE, \
            f"{view_name} {key.split('#')[0]} starts {left:g} px from the left edge, under the friends orb (needs {ORB_CLEARANCE})"


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
        "the card art standing still": prefab_plant(card, lambda t: t.replace(ART_DRIFT_MAT, "0" * 32)),
        "the genre icon back to the petal": prefab_plant(card, lambda t: t.replace(GENRE_SHAPES[0], GENRE_SHAPES[1], 1)),
        "the hull back to a dark render": prefab_plant(card, lambda t: t.replace(HULL_LIT_MAT, ART_DRIFT_MAT)),
        "the art band with no overscan": prefab_plant(card, lambda t: re.sub(r"  overscan: [\d.]+\n", "  overscan: 1\n", t)),
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
        "Replays opens the wrong window": prefab_plant(prefab_path("ArcadeView"), lambda t: in_one(
            t, K.FileIds("ArcadeView")("Replays#open"), f"  target: {REPLAYS_MODAL}\n", "  target: 18\n")),
        "Back first in focus order": prefab_plant(prefab_path("ArcadeView"), lambda t: in_one(
            t, K.FileIds("ArcadeView")("root#rt"),
            f"  - {{fileID: {K.FileIds('ArcadeView')('Body#rt')}}}\n  - {{fileID: {K.FileIds('ArcadeView')('Header#rt')}}}\n",
            f"  - {{fileID: {K.FileIds('ArcadeView')('Header#rt')}}}\n  - {{fileID: {K.FileIds('ArcadeView')('Body#rt')}}}\n")),
        "a bond aimed away from its seat": prefab_plant(panel, lambda t: in_one(
            t, K.FileIds("PartyPanel")("Bond3#rt"), f"m_LocalEulerAnglesHint: {{x: 0, y: 0, z: {K.f32(MOL_ANGLES[1])}}}",
            "m_LocalEulerAnglesHint: {x: 0, y: 0, z: 40}")),
        "a bond without KitRimMesh": prefab_plant(panel, lambda t: in_one(
            t, K.FileIds("PartyPanel")("Bond2#go"), f"  - component: {{fileID: {K.FileIds('PartyPanel')('Bond2#rim')}}}\n", "")),
        "the centre seat given a bond": prefab_plant(panel, lambda t: t.replace(
            "  seatBonds:\n  - {fileID: 0}\n", f"  seatBonds:\n  - {{fileID: {K.FileIds('PartyPanel')('Bond2#img')}}}\n", 1)),
        "the body back under the friends orb": prefab_plant(prefab_path("ArcadeView"), lambda t: in_one(
            t, K.FileIds("ArcadeView")("Body#rt"), "m_AnchoredPosition: {x: 20", "m_AnchoredPosition: {x: -20")),
        "unrelated document edited": scene_plant(lambda s: s.replace("  m_Name: Screens\n", "  m_Name: Screenz\n", 1)),
        "the Join button loses its profile relay (pad focus shows no card)": prefab_plant(
            prefab_path("PartyOnlineRow"), lambda t: re.sub(
                r"(?m)^--- !u!114 &-?\d+\n(?:(?!^--- ).*\n)*?.*guid: " + S["profile"] + r", type: 3\}\n(?:(?!^--- ).*\n)*",
                "", t, count=1)),
        "the Join slot reading its button's rect (SPECTATE overflows)": prefab_plant(
            prefab_path("PartyOnlineRow"), lambda t: in_one(
                t, K.FileIds("PartyOnlineRow")("JoinSlot#hlg"), "  m_ChildControlWidth: 1\n", "  m_ChildControlWidth: 0\n")),
        "ELEMENT without its button": prefab_plant(prefab_path("ArcadeView"), lambda t: re.sub(
            r"(\n  elementButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", t, count=1)),
        "an element tab dropped": prefab_plant(prefab_path("ArcadeView"), lambda t: t.replace(
            f"  - {{fileID: {K.FileIds('ArcadeView')('ElementRow/Tab4#tab')}}}\n", "", 1)),
        "a vessel choice out of step with its tab": prefab_plant(prefab_path("ArcadeView"), lambda t: t.replace(
            f"  vesselChoices:\n  - -1\n  - {VESSEL_CHOICES[1][1]}\n", f"  vesselChoices:\n  - -1\n  - 0\n", 1)),
        "the vessel list open on arrival": prefab_plant(prefab_path("ArcadeView"), lambda t: in_one(
            t, K.FileIds("ArcadeView")("VesselRow#cg"), "  m_Alpha: 0\n", "  m_Alpha: 1\n")),
        "a legacy ArcadeExploreView without its dropdowns": scene_plant(lambda s: s.replace(
            f"gridOptions: {{fileID: {ids.stubs['gridOptions']}}}", "gridOptions: {fileID: 0}")),
        "a seat's relay opens the card on the wrong avatar": prefab_plant(prefab_path("PartySlot"), lambda t: t.replace(
            f"  anchor: {{fileID: {K.FileIds('PartySlot')('Occupied/Avatar/Icon#rt')}}}\n",
            f"  anchor: {{fileID: {K.FileIds('PartySlot')('Occupied/Name#rt')}}}\n", 1)),
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
