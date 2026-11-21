#!/usr/bin/env python3
"""
The Voyage window laid out as Garrett sketched it on 2026-11-01 (Docs/Voyages/VOYAGE_BOARD.md section 3):

    "I meant more like this where clicking on a voyage expands it over the others. And the voyages are like the arranged
    in a matrix like the different arcade games. Then the episodes and saga are top level above the fold modals that
    contain their own content." / "This way the map divides them as a fun interactive galaxy explorer deciding where
    you want your voyages to take you."

    SAGA   | EPISODES          two buttons at the top, each opening its own modal
    galaxy map (top view)      VoyageJobsView stands it at runtime, then the edge view under it
    edge view
    Voyage 1 Voyage 2 Voyage 3 the job cards in a grid (three across); an open card stands over the others

This rewrites MissionView.prefab IN PLACE from the window #1673/#1678 shipped (Rev 17 J's two columns, Shombith's
Episodes, with VoyageJobsView's job card hand-placed in the Voyages column). Nothing is deleted except Body's
HorizontalLayoutGroup; everything keeps its fileID, so the quest track, the saga voyages and Menu_Main's instance are
untouched:

    Body/Top/SagaButton, Body/Top/EpisodesButton   new: copies of the Episode 2 bar (its frame) with Episode 1's title
    Body/Voyages                                   now the whole body under Top; its "Voyages" head is hidden
    .../Content/Grid                               new: a GridLayoutGroup holding Voyage3 (VoyageJobsView's template)
    VoyageModals/SagaModal/Panel                   new: Featured (Tandava) and Voyage2 (Severing), side by side
    VoyageModals/EpisodesModal/Panel               new: the Episodes column (Episode 1's quests, Episode 2)
    each modal's Dim                               new: a press outside the panel closes it
    Body's VoyageWindowModals                      new: opens and closes the two modals

author_menu_mission.py, which first wrote this prefab, stands down on MissionView once this layout is on disk.

THE SECOND LAYOUT (Shombith, 2026-11-02, Docs/MENU_REDESIGN_B.md section 119; the picked boards Rev 44 I, Rev 46 and
Rev 49 C): "Episode, saga are separate things and so is quest tracker. So 4 tabs and Voyage opens up first", the map
on the left, the voyage cards small on the right, sliding open:

    LB  VOYAGES  SAGA  QUESTS  [lock] EPISODES  RB      Body/Top: four tabs (VoyageWindowTabs on Body)
    HyperSea map | voyage cards, two across           Voyages/MapColumn (the map and edge view) | Voyages/Scroll
    edge view    | (the open voyage slides over them, VoyageOpenCard, built at runtime)
    VoyagePages/SagaPage, QuestsPage (Episodes with its quests, no Episode 1 eyebrow), EpisodesPage (Episode 2, locked)

rewrite2() turns the first layout into this one in place (everything keeps its fileID; the two Dims and
VoyageWindowModals go). It too stands down once its marker (VoyagePages) is on disk.

STANDING DOWN: the rewrite runs only while the old two-column layout is on disk; afterwards this script validates the
layout (--check) and its negative controls (--self-test).

    python3 Tools/Build/author_voyage_window.py              # rewrite (once), then validate
    python3 Tools/Build/author_voyage_window.py --check      # validate the layout on disk
    python3 Tools/Build/author_voyage_window.py --self-test  # planted defects must all be caught
"""

import argparse
import os
import random
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PREFAB = "Assets/_Prefabs/UI Elements/Main Menu Screens/Mission/MissionView.prefab"
MARKER = "VoyageModals"          # the first layout's modals root (renamed by the second layout)
MARKER2 = "VoyagePages"          # the second layout's pages root: its presence means the four tabs are on disk

GUID = dict(
    image="fe87c0e1cc204ed48ad3b37840f39efc",
    button="4e29b1a8efbd4b44bb3f3716e73f07ff",
    grid="8a8695521f0d02e499659fee002a26c2",
    hlg="30649d3a9faa99c48a7b1166b86bf2a0",
    jobs="491d05bb0d3cf0dd744a4afd47ed8bb9",
    voyage="e342e7be076713ca896cf8f292b45077",
    pusher="1d953354217c86ae8eaec252ad2308e3",
)
MODALS_GUID = "8b13cf3793f3450c836b72f39775c5a2"   # VoyageWindowModals, the first layout's (retired by the second)
TABS_SCRIPT = os.path.join(REPO, "Assets/_Scripts/UI/Views/VoyageWindowTabs.cs.meta")
TABS = ("Voyages", "Saga", "Quests", "Episodes")
MAP_W, COL_GAP, EDGE_H, EDGE_GAP, KEY_W, TAB_GAP = 1000, 20, 180, 12, 48, 16
LIGHT = "{r: 0.9019608, g: 0.9137255, b: 1, a: 1}"
IDLE = "{r: 0.45490196, g: 0.48235294, b: 0.6784314, a: 1}"

TOP_H, TOP_GAP, BUTTON_GAP = 72, 20, 20
GRID_COLS, CELL_H, CELL_GAP = 3, 176, 20
CARD_W, CARD_H, CARD_GAP = 480, 556, 20
EPISODES_W, EPISODES_H = 1240, 720
DIM = "{r: 0, g: 0.003921569, b: 0.039215688, a: 0.8}"

# ── A small Unity YAML model ────────────────────────────────────────────────────

HDR = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?$", re.M)


class Doc:
    def __init__(self, cls, fid, stripped, body):
        self.cls, self.fid, self.stripped, self.body = cls, fid, stripped, body

    def text(self):
        return f"--- !u!{self.cls} &{self.fid}{' stripped' if self.stripped else ''}\n{self.body}"


class Prefab:
    def __init__(self, text):
        ms = list(HDR.finditer(text))
        self.head = text[:ms[0].start()]
        self.docs = []
        for i, m in enumerate(ms):
            end = ms[i + 1].start() if i + 1 < len(ms) else len(text)
            self.docs.append(Doc(m.group(1), m.group(2), bool(m.group(3)), text[m.end() + 1:end]))
        self.by = {d.fid: d for d in self.docs}
        self.used = set(self.by)

    def text(self):
        return self.head + "".join(d.text() for d in self.docs)

    # lookups
    @staticmethod
    def field(body, name):
        m = re.search(r"^  " + name + r": \{fileID: (-?\d+)", body, re.M)
        return m.group(1) if m else None

    def name(self, go):
        return re.search(r"^  m_Name: (.*)$", self.by[go].body, re.M).group(1)

    def comps(self, go):
        return re.findall(r"component: \{fileID: (-?\d+)\}", self.by[go].body)

    def rt(self, go):
        for c in self.comps(go):
            if self.by[c].cls == "224":
                return c
        raise KeyError(go)

    def go_of(self, rt):
        return self.field(self.by[rt].body, "m_GameObject")

    def children(self, rt):
        m = re.search(r"  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", self.by[rt].body)
        return re.findall(r"fileID: (-?\d+)", m.group(1)) if m else []

    def father(self, rt):
        return self.field(self.by[rt].body, "m_Father")

    def root(self):
        return next(d.fid for d in self.docs if d.cls == "224" and not d.stripped and self.father(d.fid) == "0")

    def child(self, go, name):
        for c in self.children(self.rt(go)):
            d = self.by.get(c)
            if d and not d.stripped and d.cls == "224" and self.name(self.go_of(c)) == name:
                return self.go_of(c)
        return None

    def find(self, path):
        go = self.go_of(self.root())
        parts = path.split("/")
        assert self.name(go) == parts[0], f"the prefab's root is not {parts[0]}"
        for p in parts[1:]:
            nxt = self.child(go, p)
            assert nxt, f"{path}: no {p}"
            go = nxt
        return go

    def has(self, path):
        try:
            self.find(path)
            return True
        except (AssertionError, KeyError):
            return False

    def comp(self, go, guid):
        for c in self.comps(go):
            if f"guid: {guid}" in self.by[c].body:
                return c
        return None

    # edits
    def new_id(self):
        while True:
            fid = str(random.randint(10 ** 17, 9 * 10 ** 17))
            if fid not in self.used:
                self.used.add(fid)
                return fid

    def add(self, cls, fid, body, after=None):
        d = Doc(cls, fid, False, body)
        self.by[fid] = d
        self.used.add(fid)
        if after is None:
            self.docs.append(d)
        else:
            self.docs.insert(self.docs.index(self.by[after]) + 1, d)
        return d

    def remove(self, fid):
        self.docs.remove(self.by.pop(fid))

    def set(self, fid, key, value):
        d = self.by[fid]
        new, n = re.subn(r"^(  " + re.escape(key) + r": ).*$", lambda m: m.group(1) + value, d.body, count=1, flags=re.M)
        assert n == 1, f"{fid}: no {key}"
        d.body = new

    def set_children(self, rt, kids):
        d = self.by[rt]
        listing = "  m_Children:\n" + "".join(f"  - {{fileID: {k}}}\n" for k in kids) if kids else "  m_Children: []\n"
        d.body = re.sub(r"  m_Children:(?: \[\])?\n(?:  - \{fileID: -?\d+\}\n)*", lambda m: listing, d.body, count=1)

    def reparent(self, go, new_parent_go, index=None):
        rt = self.rt(go)
        old = self.father(rt)
        self.set_children(old, [c for c in self.children(old) if c != rt])
        prt = self.rt(new_parent_go)
        kids = [c for c in self.children(prt) if c != rt]
        kids.insert(len(kids) if index is None else index, rt)
        self.set_children(prt, kids)
        self.set(rt, "m_Father", f"{{fileID: {prt}}}")

    def place(self, go, amin, amax, pos, size, pivot):
        rt = self.rt(go)
        n = lambda x: str(int(x)) if float(x).is_integer() else str(x)
        v = lambda p: f"{{x: {n(p[0])}, y: {n(p[1])}}}"
        self.set(rt, "m_AnchorMin", v(amin))
        self.set(rt, "m_AnchorMax", v(amax))
        self.set(rt, "m_AnchoredPosition", v(pos))
        self.set(rt, "m_SizeDelta", v(size))
        self.set(rt, "m_Pivot", v(pivot))

    def active(self, go, on):
        self.set(go, "m_IsActive", "1" if on else "0")

    def subtree(self, go):
        """Every doc of go and its descendants (no prefab instances below it)."""
        out = [go] + self.comps(go)
        for c in self.children(self.rt(go)):
            assert not self.by[c].stripped, f"{self.name(go)} holds a prefab instance; it cannot be copied"
            out += self.subtree(self.go_of(c))
        return out

    def clone(self, go, parent_go, name):
        """Copy go's subtree under parent_go with fresh fileIDs; returns the copy's GameObject id."""
        ids = self.subtree(go)
        remap = {old: self.new_id() for old in ids}
        last = self.docs[-1].fid
        for old in ids:
            body = re.sub(r"\{fileID: (-?\d+)\}", lambda m: f"{{fileID: {remap.get(m.group(1), m.group(1))}}}",
                          self.by[old].body)
            last = self.add(self.by[old].cls, remap[old], body, after=last).fid
        copy = remap[go]
        self.set(copy, "m_Name", name)
        rt = self.rt(copy)
        prt = self.rt(parent_go)
        self.set(rt, "m_Father", f"{{fileID: {prt}}}")
        self.set_children(prt, self.children(prt) + [rt])
        return copy

    def delete(self, go):
        rt = self.rt(go)
        parent = self.father(rt)
        self.set_children(parent, [c for c in self.children(parent) if c != rt])
        for fid in self.subtree(go):
            self.remove(fid)


# ── New objects ────────────────────────────────────────────────────────────────

def go_body(name, comps, active=True):
    return ("GameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n"
            + "".join(f"  - component: {{fileID: {c}}}\n" for c in comps)
            + f"  m_Layer: 5\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n"
            f"  m_StaticEditorFlags: 0\n  m_IsActive: {1 if active else 0}\n")


def rt_body(go, father):
    return ("RectTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n"
            "  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n"
            f"  m_Children: []\n  m_Father: {{fileID: {father}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
            "  m_AnchorMin: {x: 0, y: 0}\n  m_AnchorMax: {x: 1, y: 1}\n  m_AnchoredPosition: {x: 0, y: 0}\n"
            "  m_SizeDelta: {x: 0, y: 0}\n  m_Pivot: {x: 0.5, y: 0.5}\n")


def mono_head(go, guid):
    return ("MonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n")


def canvas_renderer(go):
    return ("CanvasRenderer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_CullTransparentMesh: 1\n")


def image(go, color):
    return mono_head(go, GUID["image"]) + (
        f"  m_Material: {{fileID: 0}}\n  m_Color: {color}\n  m_RaycastTarget: 1\n  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}\n"
        "  m_Maskable: 1\n  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_Sprite: {fileID: 0}\n"
        "  m_Type: 0\n  m_PreserveAspect: 0\n  m_FillCenter: 1\n  m_FillMethod: 4\n  m_FillAmount: 1\n  m_FillClockwise: 1\n"
        "  m_FillOrigin: 0\n  m_UseSpriteMesh: 0\n  m_PixelsPerUnitMultiplier: 1\n")


def button(go, target, transition):
    return mono_head(go, GUID["button"]) + (
        "  m_Navigation:\n    m_Mode: 3\n    m_WrapAround: 0\n    m_SelectOnUp: {fileID: 0}\n    m_SelectOnDown: {fileID: 0}\n"
        "    m_SelectOnLeft: {fileID: 0}\n    m_SelectOnRight: {fileID: 0}\n"
        f"  m_Transition: {transition}\n  m_Colors:\n    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}\n"
        "    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n"
        "    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}\n"
        "    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n"
        "    m_DisabledColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}\n"
        "    m_ColorMultiplier: 1\n    m_FadeDuration: 0.1\n  m_SpriteState:\n    m_HighlightedSprite: {fileID: 0}\n"
        "    m_PressedSprite: {fileID: 0}\n    m_SelectedSprite: {fileID: 0}\n    m_DisabledSprite: {fileID: 0}\n"
        "  m_AnimationTriggers:\n    m_NormalTrigger: Normal\n    m_HighlightedTrigger: Highlighted\n"
        "    m_PressedTrigger: Pressed\n    m_SelectedTrigger: Selected\n    m_DisabledTrigger: Disabled\n"
        f"  m_Interactable: 1\n  m_TargetGraphic: {{fileID: {target}}}\n  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n")


def grid(go):
    return mono_head(go, GUID["grid"]) + (
        "  m_Padding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 0\n    m_Bottom: 0\n  m_ChildAlignment: 0\n"
        f"  m_StartCorner: 0\n  m_StartAxis: 0\n  m_CellSize: {{x: {(1760 - (GRID_COLS - 1) * CELL_GAP) // GRID_COLS}, y: {CELL_H}}}\n"
        f"  m_Spacing: {{x: {CELL_GAP}, y: {CELL_GAP}}}\n  m_Constraint: 1\n  m_ConstraintCount: {GRID_COLS}\n")


def node(p, name, parent_go, comps=(), active=True, index=None):
    """A new GameObject with a RectTransform under parent_go; comps are (cls, body_fn(go)) pairs. Returns (go, ids)."""
    go, rt = p.new_id(), p.new_id()
    ids = [p.new_id() for _ in comps]
    last = p.docs[-1].fid
    last = p.add("1", go, go_body(name, [rt] + ids, active), after=last).fid
    last = p.add("224", rt, rt_body(go, p.rt(parent_go)), after=last).fid
    for fid, (cls, fn) in zip(ids, comps):
        last = p.add(cls, fid, fn(go), after=last).fid
    prt = p.rt(parent_go)
    kids = p.children(prt)
    kids.insert(len(kids) if index is None else index, rt)
    p.set_children(prt, kids)
    return go, ids


def script_guid():
    return re.search(r"^guid: (\w+)$", open(TABS_SCRIPT, encoding="utf-8").read(), re.M).group(1)


def tmp_of(p, go):
    return next(c for c in p.comps(go) if "m_text:" in p.by[c].body)


# ── The rewrite ────────────────────────────────────────────────────────────────

def rewrite(p):
    root = p.go_of(p.root())
    body = p.find("MissionView/Body")
    voyages = p.find("MissionView/Body/Voyages")
    episodes = p.find("MissionView/Body/Episodes")
    content = p.find("MissionView/Body/Voyages/Scroll/Viewport/Content")
    featured = p.child(content, "Featured")
    severing = p.child(content, "Voyage2")
    template = p.child(content, "Voyage3")
    assert featured and severing and template, "the old Voyages column is not as #1678 left it"
    assert p.comp(voyages, GUID["jobs"]), "Voyages carries no VoyageJobsView"

    # Body: no more two columns.
    hlg = p.comp(body, GUID["hlg"])
    if hlg:
        bd = p.by[body]
        bd.body = bd.body.replace(f"  - component: {{fileID: {hlg}}}\n", "")
        p.remove(hlg)

    # The top row: SAGA and EPISODES, copies of the Episode 2 bar's frame with Episode 1's title as the label.
    top, _ = node(p, "Top", body, index=0)
    p.place(top, (0, 1), (1, 1), (0, 0), (0, TOP_H), (0.5, 1))
    ep2 = p.child(episodes, "Episode2")
    title = p.find("MissionView/Body/Episodes/Episode1/Title")
    buttons = {}
    for key, text, amin, amax, x in (("SagaButton", "Saga", (0, 0), (0.5, 1), -BUTTON_GAP / 2),
                                     ("EpisodesButton", "Episodes", (0.5, 0), (1, 1), BUTTON_GAP / 2)):
        b = p.clone(ep2, top, key)
        p.delete(p.child(b, "Lock"))
        p.delete(p.child(b, "Eyebrow"))
        label = p.clone(title, b, "Label")
        tmp = next(c for c in p.comps(label) if "m_text:" in p.by[c].body)
        p.set(tmp, "m_text", text)
        p.set(tmp, "m_HorizontalAlignment", "2")
        p.place(label, (0, 0), (1, 1), (0, 0), (0, 0), (0.5, 0.5))
        p.place(b, amin, amax, (x, 0), (-BUTTON_GAP, 0), (0.5, 0.5))
        fill = p.child(b, "Fill")
        fill_img = p.comp(fill, GUID["image"])
        p.set(fill_img, "m_RaycastTarget", "1")
        bid = p.new_id()
        p.add("114", bid, button(b, fill_img, 1), after=p.rt(b))
        bd = p.by[b]
        bd.body = bd.body.replace("  m_Layer: 5\n", f"  - component: {{fileID: {bid}}}\n  m_Layer: 5\n", 1)
        buttons[key] = bid

    # The voyages fill the body under the top row; the map, its edge view and the grid scroll in it.
    p.place(voyages, (0, 0), (1, 1), (0, -(TOP_H + TOP_GAP) / 2), (0, -(TOP_H + TOP_GAP)), (0.5, 0.5))
    p.active(p.child(voyages, "Head"), False)
    p.place(p.child(voyages, "Scroll"), (0, 0), (1, 1), (0, 0), (0, 0), (0.5, 0.5))
    g, _ = node(p, "Grid", content, comps=(("114", grid),))
    p.place(g, (0, 1), (0, 1), (0, 0), (1760, CELL_H), (0, 1))
    p.reparent(template, g)

    # The modals, over the body (and pushed with it), under the header so Back stays on top.
    root_rt = p.rt(root)
    header_rt = p.rt(p.find("MissionView/Header"))
    modals, _ = node(p, MARKER, root, index=p.children(root_rt).index(header_rt))
    brt = p.by[p.rt(body)].body
    get = lambda k: re.search(r"^  " + k + r": (\{.*\})$", brt, re.M).group(1)
    for k in ("m_AnchorMin", "m_AnchorMax", "m_AnchoredPosition", "m_SizeDelta", "m_Pivot"):
        p.set(p.rt(modals), k, get(k))
    pusher = p.comp(root, GUID["pusher"])
    if pusher:
        pd = p.by[pusher]
        pd.body = pd.body.replace("  pushed:\n", f"  pushed:\n  - {{fileID: {p.rt(modals)}}}\n", 1)

    made = {}
    for key, size in (("SagaModal", (2 * CARD_W + CARD_GAP, CARD_H)), ("EpisodesModal", (EPISODES_W, EPISODES_H))):
        m, _ = node(p, key, modals, active=False)
        dim, (_, dim_img, dim_btn) = node(p, "Dim", m, comps=(
            ("222", canvas_renderer), ("114", lambda go: image(go, DIM)), ("114", lambda go: "")))
        p.by[dim_btn].body = button(dim, dim_img, 0)
        panel, _ = node(p, "Panel", m)
        p.place(panel, (0.5, 0.5), (0.5, 0.5), (0, 0), size, (0.5, 0.5))
        made[key] = (m, dim_btn, panel)

    saga_panel, ep_panel = made["SagaModal"][2], made["EpisodesModal"][2]
    p.reparent(featured, saga_panel)
    p.reparent(severing, saga_panel)
    p.place(featured, (0, 1), (0, 1), (0, 0), (CARD_W, CARD_H), (0, 1))
    p.place(severing, (0, 1), (0, 1), (CARD_W + CARD_GAP, 0), (CARD_W, CARD_H), (0, 1))
    p.reparent(episodes, ep_panel)
    p.place(episodes, (0, 1), (0, 1), (0, 0), (EPISODES_W, EPISODES_H), (0, 1))

    # The behaviour that opens them (VoyageWindowModals; rewrite2 swaps it for the four tabs).
    mid = p.new_id()
    p.add("114", mid, mono_head(body, MODALS_GUID)
          + f"  sagaButton: {{fileID: {buttons['SagaButton']}}}\n"
          f"  episodesButton: {{fileID: {buttons['EpisodesButton']}}}\n"
          f"  sagaModal: {{fileID: {made['SagaModal'][0]}}}\n"
          f"  episodesModal: {{fileID: {made['EpisodesModal'][0]}}}\n"
          f"  sagaClose: {{fileID: {made['SagaModal'][1]}}}\n"
          f"  episodesClose: {{fileID: {made['EpisodesModal'][1]}}}\n", after=p.rt(body))
    bd = p.by[body]
    bd.body = bd.body.replace("  m_Layer: 5\n", f"  - component: {{fileID: {mid}}}\n  m_Layer: 5\n", 1)


def rewrite2(p):
    """The first layout (Saga | Episodes over modals) into the four tabs, the map column and the cards column."""
    root = p.go_of(p.root())
    body = p.find("MissionView/Body")
    top = p.find("MissionView/Body/Top")
    voyages = p.find("MissionView/Body/Voyages")
    saga_b = p.find("MissionView/Body/Top/SagaButton")
    ep_b = p.find("MissionView/Body/Top/EpisodesButton")

    # Four tabs: Voyages and Quests are copies of the Saga button; the old two are renamed.
    voy_b = p.clone(saga_b, top, "VoyagesTab")
    quest_b = p.clone(saga_b, top, "QuestsTab")
    p.set(saga_b, "m_Name", "SagaTab")
    p.set(ep_b, "m_Name", "EpisodesTab")
    tabs = {"Voyages": voy_b, "Saga": saga_b, "Quests": quest_b, "Episodes": ep_b}
    label_from = p.child(saga_b, "Label")
    keys = {}
    for key in ("LB", "RB"):
        k = p.clone(label_from, top, key)
        t = tmp_of(p, k)
        p.set(t, "m_text", key)
        p.set(t, "m_fontSize", "16")
        keys[key] = k
    top_rt = p.rt(top)
    p.set_children(top_rt, [p.rt(keys["LB"])] + [p.rt(tabs[n]) for n in TABS] + [p.rt(keys["RB"])])
    width = 1760 - 2 * (KEY_W + 12)
    tab_w = (width - 3 * TAB_GAP) / 4
    p.place(keys["LB"], (0, 0), (0, 1), (0, 0), (KEY_W, 0), (0, 0.5))
    p.place(keys["RB"], (1, 0), (1, 1), (0, 0), (KEY_W, 0), (1, 0.5))
    lock_from = p.find("MissionView/VoyageModals/EpisodesModal/Panel/Episodes/Episode2/Lock")
    marks = {}
    for i, name in enumerate(TABS):
        tab = tabs[name]
        p.place(tab, (0, 0), (0, 1), (KEY_W + 12 + i * (tab_w + TAB_GAP), 0), (tab_w, 0), (0, 0.5))
        lab = p.child(tab, "Label")
        t = tmp_of(p, lab)
        p.set(t, "m_text", name)
        p.set(t, "m_fontColor", LIGHT if name == "Voyages" else IDLE)
        mark, _ = node(p, "Mark", tab, comps=(("222", canvas_renderer), ("114", lambda go: image(go, LIGHT))),
                       active=(name == "Voyages"))
        p.place(mark, (0, 0), (1, 0), (0, 0), (-48, 3), (0.5, 0))
        marks[name] = mark
        if name == "Episodes":
            lock = p.clone(lock_from, tab, "Lock")
            p.place(lock, (0.5, 0.5), (0.5, 0.5), (-70, 0), (14, 16), (0.5, 0.5))

    # The modals become pages under the tabs, without their dims.
    pages = p.find(f"MissionView/{MARKER}")
    p.set(pages, "m_Name", MARKER2)
    brt = p.by[p.rt(body)].body
    get = lambda k: re.search(r"^  " + k + r": \{x: (-?[\d.]+), y: (-?[\d.]+)\}$", brt, re.M).groups()
    bx, by = (float(v) for v in get("m_AnchoredPosition"))
    sx, sy = (float(v) for v in get("m_SizeDelta"))
    shift = TOP_H + TOP_GAP
    p.place(pages, (0, 0), (1, 1), (bx, by - shift / 2), (sx, sy - shift), (0.5, 0.5))
    saga = p.find(f"MissionView/{MARKER2}/SagaModal")
    quests = p.find(f"MissionView/{MARKER2}/EpisodesModal")
    p.set(saga, "m_Name", "SagaPage")
    p.set(quests, "m_Name", "QuestsPage")
    for page in (saga, quests):
        p.delete(p.child(page, "Dim"))
    episodes = p.find(f"MissionView/{MARKER2}/QuestsPage/Panel/Episodes")
    p.set(tmp_of(p, p.child(episodes, "Head")), "m_text", "Quests")
    p.active(p.find(f"MissionView/{MARKER2}/QuestsPage/Panel/Episodes/Episode1/Eyebrow"), False)

    # Episodes, locked: the Episode 2 bar and "Coming soon".
    ep_page, _ = node(p, "EpisodesPage", pages, active=False)
    ep_panel, _ = node(p, "Panel", ep_page)
    p.place(ep_panel, (0.5, 0.5), (0.5, 0.5), (0, 0), (EPISODES_W, 200), (0.5, 0.5))
    bar = p.child(episodes, "Episode2")
    p.reparent(bar, ep_panel)
    p.place(bar, (0, 1), (1, 1), (0, -32), (0, 64), (0.5, 0.5))
    soon = p.clone(p.child(episodes, "Head"), ep_panel, "Soon")
    st = tmp_of(p, soon)
    p.set(st, "m_text", "Coming soon")
    p.set(st, "m_fontSize", "28")
    p.set(st, "m_HorizontalAlignment", "2")
    p.place(soon, (0, 1), (1, 1), (0, -112), (0, 40), (0.5, 0.5))

    # Voyages: the map column on the left, the cards on the right.
    scroll = p.child(voyages, "Scroll")
    column, _ = node(p, "MapColumn", voyages, index=p.children(p.rt(voyages)).index(p.rt(scroll)))
    p.place(column, (0, 0), (0, 1), (0, 0), (MAP_W, 0), (0, 0.5))
    p.place(scroll, (0, 0), (1, 1), ((MAP_W + COL_GAP) / 2, 0), (-(MAP_W + COL_GAP), 0), (0.5, 0.5))
    jobs = p.comp(voyages, GUID["jobs"])
    jd = p.by[jobs]
    jd.body = re.sub(r"  mapHeight: .*\n", "  mapHeight: 360\n", jd.body)
    jd.body += (f"  edgeHeight: {EDGE_H}\n  columns: 2\n  mapColumn: {{fileID: {p.rt(column)}}}\n"
                f"  cardColumn: {{fileID: {p.rt(scroll)}}}\n  edgeGap: {EDGE_GAP}\n")

    # VoyageWindowModals goes; VoyageWindowTabs shows one page at a time.
    old = p.comp(body, MODALS_GUID)
    if old:
        p.by[body].body = p.by[body].body.replace(f"  - component: {{fileID: {old}}}\n", "")
        p.remove(old)
    pages_of = {"Voyages": voyages, "Saga": saga, "Quests": quests, "Episodes": ep_page}
    entries = "".join(
        f"  - button: {{fileID: {p.comp(tabs[n], GUID['button'])}}}\n"
        f"    label: {{fileID: {tmp_of(p, p.child(tabs[n], 'Label'))}}}\n"
        f"    mark: {{fileID: {marks[n]}}}\n"
        f"    page: {{fileID: {pages_of[n]}}}\n" for n in TABS)
    tid = p.new_id()
    p.add("114", tid, mono_head(body, script_guid()) + "  tabs:\n" + entries
          + f"  selectedColor: {LIGHT}\n  idleColor: {IDLE}\n  shoulderInput: 1\n", after=p.rt(body))
    bd = p.by[body]
    bd.body = bd.body.replace("  m_Layer: 5\n", f"  - component: {{fileID: {tid}}}\n  m_Layer: 5\n", 1)


# ── Validation ─────────────────────────────────────────────────────────────────

def validate(text):
    p = Prefab(text)
    # Every local reference resolves.
    for d in p.docs:
        for m in re.finditer(r"\{fileID: (-?\d+)\}(?!, guid)", d.body):
            assert m.group(1) == "0" or m.group(1) in p.by, f"dangling fileID {m.group(1)} in {d.fid}"
    # Every child lists its father and every father its children.
    for d in p.docs:
        if d.cls == "224" and not d.stripped:
            for c in p.children(d.fid):
                cd = p.by[c]
                if not cd.stripped:
                    assert p.father(c) == d.fid, f"{c} is listed under {d.fid} but names another father"

    body = p.find("MissionView/Body")
    assert not p.comp(body, GUID["hlg"]), "Body still lays two columns (its HorizontalLayoutGroup is back)"
    assert not p.comp(body, MODALS_GUID), "VoyageWindowModals is back (the window has four tabs now)"
    top_names = [p.name(p.go_of(c)) for c in p.children(p.rt(p.find("MissionView/Body/Top")))]
    assert top_names == ["LB"] + [f"{n}Tab" for n in TABS] + ["RB"], f"the top row is not LB, the four tabs, RB: {top_names}"
    for n in TABS:
        tab = p.find(f"MissionView/Body/Top/{n}Tab")
        assert p.comp(tab, GUID["button"]), f"{n} is not a button"
        assert f"  m_text: {n}\n" in p.by[tmp_of(p, p.child(tab, 'Label'))].body, f"the {n} tab does not read {n}"
        assert p.child(tab, "Mark"), f"the {n} tab has no mark"
    assert p.has("MissionView/Body/Top/EpisodesTab/Lock"), "Episodes is not shown locked"
    tabs = p.comp(body, script_guid())
    assert tabs, "Body carries no VoyageWindowTabs"
    tb = p.by[tabs].body
    entries = re.findall(r"  - button: \{fileID: (-?\d+)\}\n    label: \{fileID: (-?\d+)\}\n    mark: \{fileID: (-?\d+)\}\n    page: \{fileID: (-?\d+)\}\n", tb)
    assert len(entries) == 4 and all("0" not in e for e in entries), "VoyageWindowTabs does not hold four wired tabs"
    pages = [p.name(e[3]) for e in entries]
    assert pages == ["Voyages", "SagaPage", "QuestsPage", "EpisodesPage"], f"the tabs show the wrong pages: {pages}"
    for name in ("SagaPage", "QuestsPage", "EpisodesPage"):
        page = p.find(f"MissionView/{MARKER2}/{name}")
        assert "  m_IsActive: 0\n" in p.by[page].body, f"{name} opens with the window (Voyages opens first)"
        assert not p.child(page, "Dim"), f"{name} still has a modal's dim"
    for k in ("Featured", "Voyage2"):
        assert p.has(f"MissionView/{MARKER2}/SagaPage/Panel/{k}"), f"the Saga page does not hold {k}"
    assert p.comp(p.find(f"MissionView/{MARKER2}/SagaPage/Panel/Featured"), GUID["voyage"]), "Tandava's card lost its VoyageView"
    assert p.has(f"MissionView/{MARKER2}/QuestsPage/Panel/Episodes/Episode1/Quests"), "the quest tracker left the Quests page"
    assert not p.has(f"MissionView/{MARKER2}/QuestsPage/Panel/Episodes/Episode2"), "Episode 2 is still among the quests"
    assert p.has(f"MissionView/{MARKER2}/EpisodesPage/Panel/Episode2"), "the Episodes page does not hold Episode 2"
    eyebrow = p.find(f"MissionView/{MARKER2}/QuestsPage/Panel/Episodes/Episode1/Eyebrow")
    assert "  m_IsActive: 0\n" in p.by[eyebrow].body, "the quest tracker is still called Episode 1"

    content = "MissionView/Body/Voyages/Scroll/Viewport/Content"
    g = p.find(content + "/Grid")
    assert p.comp(g, GUID["grid"]), "the voyage cards are not in a grid"
    assert p.child(g, "Voyage3"), "VoyageJobsView's card is not in the grid"
    voyages = p.find("MissionView/Body/Voyages")
    jobs = p.comp(voyages, GUID["jobs"])
    jb = p.by[jobs].body
    tmpl = Prefab.field(jb, "template")
    assert tmpl in p.comps(p.child(g, "Voyage3")), "VoyageJobsView's template is not the grid's card"
    column = p.find("MissionView/Body/Voyages/MapColumn")
    assert Prefab.field(jb, "mapColumn") == p.rt(column), "VoyageJobsView does not stand the map in the map column"
    assert Prefab.field(jb, "cardColumn") == p.rt(p.find("MissionView/Body/Voyages/Scroll")), \
        "VoyageJobsView does not slide the open voyage over the cards' column"
    assert re.search(r"^  columns: 2$", jb, re.M), "the voyage cards are not two across"
    # The numbers rewrite2 wrote, read back off disk (a validator that only checks the shape passes a retuned value).
    for key, want in (("edgeHeight", EDGE_H), ("edgeGap", EDGE_GAP)):
        got = re.search(rf"^  {key}: (\S+)$", jb, re.M)
        assert got and float(got.group(1)) == float(want), f"VoyageJobsView {key} is {got and got.group(1)}, not {want}"
    assert not p.child(p.find(content), "Featured") and not p.child(p.find(content), "Voyage2"), \
        "the saga voyages are back among the job cards"
    root_rt = p.rt(p.go_of(p.root()))
    order = [p.name(p.go_of(c)) for c in p.children(root_rt) if not p.by[c].stripped]
    assert order.index(MARKER2) < order.index("Header"), "the pages cover the header (Back must stay on top)"
    return p


def self_test(text):
    def expect(label, mutate):
        try:
            validate(mutate(text))
        except AssertionError as e:
            print(f"  caught: {label} ({e})")
            return True
        print(f"  MISSED: {label}")
        return False
    first = lambda old, new: (lambda t: t.replace(old, new, 1))
    cases = [
        ("Quests renamed", lambda t: t.replace("m_text: Quests\n", "m_text: Missions\n")),
        ("the grid gone", first(f"guid: {GUID['grid']}", "guid: " + "5" * 32)),
        ("a page open at start", lambda t: re.sub(r"(m_Name: SagaPage\n(?:.*\n){4}  m_IsActive: )0", r"\g<1>1", t, count=1)),
        ("an unwired tab", lambda t: re.sub(r"(  - button: \{fileID: )-?\d+", r"\g<1>0", t, count=1)),
        ("a dangling reference", lambda t: re.sub(r"(    page: \{fileID: )-?\d+", r"\g<1>12345", t, count=1)),
        ("three cards across", first("  columns: 2\n", "  columns: 3\n")),
        ("the edge gap retuned", first(f"  edgeGap: {EDGE_GAP}\n", f"  edgeGap: {EDGE_GAP + 1}\n")),
        ("the modals' behaviour back", first(f"guid: {script_guid()}", f"guid: {MODALS_GUID}")),
    ]
    ok = all(expect(label, m) for label, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    path = os.path.join(REPO, PREFAB)
    text = open(path, encoding="utf-8").read()
    first_laid = f"m_Name: {MARKER}\n" in text or f"m_Name: {MARKER2}\n" in text
    second_laid = f"m_Name: {MARKER2}\n" in text
    if not second_laid:
        if args.check or args.self_test:
            print(f"FAIL: {PREFAB} is not the four-tab window yet; run without --check to lay it out")
            return 1
        p = Prefab(text)
        if not first_laid:
            random.seed(20261101)
            rewrite(p)
            p = Prefab(p.text())
        random.seed(20261102)
        rewrite2(p)
        text = p.text()
        validate(text)
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        print(f"wrote {PREFAB}")
    validate(text)
    print("OK: the Voyage window is laid out as picked (four tabs, the HyperSea map left, the voyage cards right)")
    if args.self_test:
        return 0 if self_test(text) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
