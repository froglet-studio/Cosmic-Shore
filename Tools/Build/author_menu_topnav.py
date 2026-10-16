#!/usr/bin/env python3
"""
Author ITEM 1 of the style-B menu redesign - the TOP NAV - into Menu_Main.unity
(Docs/MENU_REDESIGN_B.md section 10; the approved Main / TopNavHover / TopNavLocked boards on the
"Menu Redesign B" canvas). Look and layout only: no navigation code or wiring changes.

WHAT `Safe Area/NavBar` BECOMES

    NavBar                    84 tall (was 180.35); anchors and CanvasGroup untouched
    |- Band                   SurfaceVeryDark fill at 60 % (replaces NavBarGradient; translucent since
    |                         the 2026-10-14 revision so the menu scene shows through)
    |- Rule                   1 px SurfaceLight bottom rule at 70 % (replaces NavBarLine)
    '- NavBarButtons          HorizontalLayoutGroup, centred, children 56 apart
       |- LeftArrrow          the SAME Button (OnClickLeftArrow), now a transparent hit area,
       |  '- Keycap           KitKeycap "LT"; a KitHoverTint on the button lifts it on hover
       |- HangarLink .. ProfileLink
       |                      KitTopNavTab instances; each keeps its EventTrigger
       |                      (OnClick<X>Nav), now an added component on the instance root
       '- RightArrow          as LeftArrrow, "RT"

    L1 Indicator / R1 Indicator are deleted: the keycaps carry the hint.
    ScreenSwitcher.NavActiveImages / NavInactiveImages point at each instance's ActiveImage /
    InactiveImage, so UpdateNavBar and ResolveNavLinkObject (whose answer is the image's PARENT,
    i.e. the instance root) behave exactly as before. NavBarLine / NavBarLineSprites are emptied:
    the per-tab line art retires with the line.
    PORT carries an AUTHORED MenuAvailabilityView - the one ScreenSwitcher.MarkDisabledNavLinks
    would otherwise add at runtime - wired to the tab's Lock glyph (lockedOverlay) and its word
    (label, dimmed to InactiveLight while Locked). Its availability is authored Available: the lock
    comes from disabledScreens at runtime, so emptying that list opens the tab with nothing else to
    change.

STANDING DOWN. The migration - deleting the old links, indicators, gradient and line - runs only
while those objects are present. After the first write the script owns the documents it
generates and the fields it patches, re-emits them identically, and validates the shipped wiring
on every run (including --check), so a spent migration never takes the checks down with it.

    python3 Tools/Build/author_menu_topnav.py              # write
    python3 Tools/Build/author_menu_topnav.py --check      # fail if the scene drifted from this
    python3 Tools/Build/author_menu_topnav.py --self-test  # negative controls: planted defects must fail
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_ui_kit_prefabs", os.path.join(HERE, "author_ui_kit_prefabs.py"))
K = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(K)

BAND_ALPHA = 0.6   # the nav band (MENU_REDESIGN_B §13.1)
RULE_ALPHA = 0.7   # its bottom rule
REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
FILE_KEY = "Menu_Main/TopNav"

# --- package script guids, harvested from Menu_Main itself (never recalled) -----------------
EVENT_TRIGGER = "d0b148fe25e99eb48b9724523833bab1"
BUTTON = "4e29b1a8efbd4b44bb3f3716e73f07ff"
SCREEN_SWITCHER = K.meta_guid("Assets/_Scripts/UI/ScreenSwitcher.cs")
AVAILABILITY_VIEW = K.meta_guid("Assets/_Scripts/UI/Elements/MenuAvailabilityView.cs")
TAB_PREFAB = "KitTopNavTab"
KEYCAP_PREFAB = "KitKeycap"

# --- the live objects this script edits (fileIDs measured 2026-10-13; names are asserted) ----
NAVBAR = dict(go="5207005607390993568", rt="5866104262863291742", parent_rt="1034510601983959698")
BUTTONS = dict(go="8900367420559236131", rt="629078397826271367", hlg="7959372451632240051")
SWITCHER = "4216411968520269688"
ARROWS = [
    dict(side="L", name="LeftArrrow", key="LT", method="OnClickLeftArrow", go="2213602630790364145",
         rt="534211216697212305", cr="4805880756659923145", img="6757017662035836823", btn="6036096898480121279"),
    dict(side="R", name="RightArrow", key="RT", method="OnClickRightArrow", go="520912455966647713",
         rt="8050805686329073856", cr="4848467573656329852", img="2786130128179163306", btn="8507622070610753978"),
]
# Strip order = MenuScreens order = the NavActiveImages / NavInactiveImages order.
TABS = [("HANGAR", "HangarLink", "OnClickHangarNav"), ("ARK", "ArkLink", "OnClickArkNav"),
        ("HOME", "HomeLink", "OnClickHomeNav"), ("PORT", "PortLink", "OnClickPortNav"),
        ("PROFILE", "ProfileLink", "OnClickProfileNav")]
HOME, PORT = 2, 3          # HOME is the screen the menu opens on; PORT is disabledScreens[0]

# The old subtree the migration deletes (root GameObjects; their components and children go too).
RETIRED_ROOTS = {
    "7170795054388275928": "NavBarGradient", "8073999750376392510": "NavBarLine",
    "5890912841618492611": "HangarLink", "3563313423898482566": "ArkLink",
    "3054726802078118506": "HomeLink", "5489399824647252620": "PortLink",
    "4920259406035720948": "ProfileLink", "5793753141633675692": "L1 Indicator",
    "2960798252982038086": "R1 Indicator",
}

BAND_H = 84
GAP = 56

HDR = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$", re.M)


# --------------------------------------------------------------------------------------------
# Scene documents
# --------------------------------------------------------------------------------------------

class Scene:
    """Ordered Unity YAML documents, addressable by fileID."""

    def __init__(self, text):
        ms = list(HDR.finditer(text))
        self.prefix = text[:ms[0].start()]
        self.order = []
        self.docs = {}
        self.cls = {}
        for i, m in enumerate(ms):
            end = ms[i + 1].start() if i + 1 < len(ms) else len(text)
            fid = m.group(2)
            assert fid not in self.docs, f"duplicate fileID anchor {fid}"
            self.order.append(fid)
            self.docs[fid] = text[m.start():end]
            self.cls[fid] = int(m.group(1))

    def text(self):
        return self.prefix + "".join(self.docs[f] for f in self.order)

    def has(self, fid):
        return fid in self.docs

    def field(self, fid, name):
        m = re.search(r"^  " + re.escape(name) + r": (.*)$", self.docs[fid], re.M)
        return m.group(1) if m else None

    def components(self, go):
        return re.findall(r"component: \{fileID: (-?\d+)\}", self.docs[go])

    def children(self, rt):
        m = re.search(r"^  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", self.docs[rt], re.M)
        return re.findall(r"fileID: (-?\d+)", m.group(1)) if m else []

    def go_of(self, comp):
        return re.search(r"m_GameObject: \{fileID: (-?\d+)\}", self.docs[comp]).group(1)

    def rect_of(self, go):
        return next(c for c in self.components(go) if self.cls.get(c) == 224)

    def script(self, fid):
        m = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", self.docs[fid])
        return m.group(1) if m else None

    def subtree(self, go):
        out = [go] + self.components(go)
        for ch in self.children(self.rect_of(go)):
            out += self.subtree(self.go_of(ch))
        return out

    def remove(self, fid):
        del self.docs[fid]
        del self.cls[fid]
        self.order.remove(fid)

    def replace(self, fid, text):
        assert fid in self.docs, f"no document {fid} to replace"
        self.docs[fid] = text

    def insert_after(self, anchor, fid, text):
        assert fid not in self.docs, f"fileID {fid} already in the scene"
        self.order.insert(self.order.index(anchor) + 1, fid)
        self.docs[fid] = text
        self.cls[fid] = int(HDR.match(text).group(1))


def doc_fid(text):
    return HDR.match(text).group(2)


# --------------------------------------------------------------------------------------------
# Emitters (the kit generator's, so every body is Unity's own serialization)
# --------------------------------------------------------------------------------------------

def rt_doc(fid, go, children, father, r):
    kids = ("  m_Children:\n" + "".join(f"  - {{fileID: {c}}}\n" for c in children)) if children else "  m_Children: []\n"
    return (f"--- !u!224 &{fid}\nRectTransform:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n" + kids
            + f"  m_Father: {{fileID: {father}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
            f"  m_AnchorMin: {K.v2(*r['amin'])}\n  m_AnchorMax: {K.v2(*r['amax'])}\n"
            f"  m_AnchoredPosition: {K.v2(*r['pos'])}\n  m_SizeDelta: {K.v2(*r['size'])}\n"
            f"  m_Pivot: {K.v2(*r['pivot'])}\n")


def go_doc(fid, name, comps, active=True):
    return (f"--- !u!1 &{fid}\nGameObject:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n"
            + "".join(f"  - component: {{fileID: {c}}}\n" for c in comps)
            + f"  m_Layer: 5\n  m_Name: {name}\n  m_TagString: Untagged\n"
            "  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n"
            f"  m_IsActive: {1 if active else 0}\n")


def stub_go(fid, src, guid, pid):
    return (f"--- !u!1 &{fid} stripped\nGameObject:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
            f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")


def stub_mono(fid, src, guid, pid, script):
    return (f"--- !u!114 &{fid} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {K.ref(src, guid)}\n"
            f"  m_PrefabInstance: {{fileID: {pid}}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {K.ref(11500000, script)}\n  m_Name: \n"
            "  m_EditorClassIdentifier: \n")


def persistent_call(method, indent):
    p = " " * indent
    return (f"{p}- m_Target: {{fileID: {SWITCHER}}}\n"
            f"{p}  m_TargetAssemblyTypeName: CosmicShore.App.UI.ScreenSwitcher, Assembly-CSharp\n"
            f"{p}  m_MethodName: {method}\n{p}  m_Mode: 1\n{p}  m_Arguments:\n"
            f"{p}    m_ObjectArgument: {{fileID: 0}}\n"
            f"{p}    m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine\n"
            f"{p}    m_IntArgument: 0\n{p}    m_FloatArgument: 0\n{p}    m_StringArgument: \n"
            f"{p}    m_BoolArgument: 0\n{p}  m_CallState: 2\n")


def event_trigger(fid, go, method):
    """A PointerClick EventTrigger -> ScreenSwitcher.<method>: the links' shipped wiring, verbatim."""
    return (f"--- !u!114 &{fid}\n" + K.mono(go, EVENT_TRIGGER,
            "  m_Delegates:\n  - eventID: 4\n    callback:\n      m_PersistentCalls:\n        m_Calls:\n"
            + persistent_call(method, 8)))


def body(fid, class_id, fn, go, ctx):
    return f"--- !u!{class_id} &{fid}\n" + fn(ctx, go)


# --------------------------------------------------------------------------------------------
# Layout
# --------------------------------------------------------------------------------------------

def keycap_size(key):
    return (K.label_width(key, "Label") + 24, K.line_height("Label") + 8)


NAV_SLOT = K.NAV_ICON + K.NAV_ICON_GAP      # the glyph's share of a tab, left of the word


def glyph_guid(i):
    """The tab's glyph (rev 3): the old nav's icon, cut by author_nav_glyphs.py."""
    return K.meta_guid(f"Assets/_Graphics/UI/NavGlyphs/nav_{TABS[i][0].lower()}.png")


def tab_width(i):
    return K.label_width(TABS[i][0], "Button") + NAV_SLOT + (K.LOCK_INSET if i == PORT else 0)


def row():
    """[(kind, index, width, height)] in strip order, and each child's centre x."""
    items = [("arrow", 0) + keycap_size("LT")]
    items += [("tab", i, tab_width(i), BAND_H) for i in range(len(TABS))]
    items += [("arrow", 1) + keycap_size("RT")]
    xs, cursor = [], 0.0
    for _, _, w, _ in items:
        xs.append(round(cursor + w / 2, 1))
        cursor += w + GAP
    return items, xs, round(cursor - GAP, 1)


# --------------------------------------------------------------------------------------------
# The transform
# --------------------------------------------------------------------------------------------

class Ids:
    """Every fileID this script mints, by role."""

    def __init__(self):
        self.ctx = K.Ctx(FILE_KEY)
        f = self.ctx.fid
        self.band = {k: str(f("Band#" + k)) for k in ("go", "rt", "cr", "img", "th")}
        self.rule = {k: str(f("Rule#" + k)) for k in ("go", "rt", "cr", "img", "th")}
        self.tabs = [{k: str(f(f"Tab{i}#{k}")) for k in
                      ("inst", "stub", "stub_go", "stub_inactive", "stub_active", "stub_lock", "stub_label",
                       "trigger", "availability")} for i in range(len(TABS))]
        self.keys = [{k: str(f(f"Key{a['side']}#{k}")) for k in ("inst", "stub", "stub_label_kt", "stub_border_th", "hover")}
                     for a in ARROWS]

    def all(self):
        out = set(self.band.values()) | set(self.rule.values())
        for i, t in enumerate(self.tabs):
            out |= {v for k, v in t.items() if i == PORT or k not in ("stub_lock", "stub_label", "availability")}
        for k in self.keys:
            out |= set(k.values())
        return out


def kit_src(prefab, key):
    return K.source_fid(prefab, key)


def tab_instance(ids, i, x):
    text, link, method = TABS[i]
    t = ids.tabs[i]
    guid = K.prefab_guid(TAB_PREFAB)
    lw = K.label_width(text, "Button")
    glyph = K.sprite_ref(glyph_guid(i))
    # The word's box gives up the glyph's slot (and PORT's lock inset) on its left, so the glyph +
    # word pair stays centred on the tab; the inactive glyph sits just left of the centred word.
    inset = NAV_SLOT + (K.LOCK_INSET if i == PORT else 0)
    mods = [("InactiveImage#tmp", "m_text", text, None), ("ActiveImage/Label#tmp", "m_text", text, None),
            ("ActiveImage/Label#rt", "m_SizeDelta.x", lw, None),
            ("ActiveImage/Label#rt", "m_AnchoredPosition.x", NAV_SLOT / 2, None),
            ("InactiveImage#rt", "m_SizeDelta.x", -inset, None),
            ("InactiveImage#rt", "m_AnchoredPosition.x", inset / 2, None),
            ("InactiveImage/Icon#go", "m_IsActive", 1, None),
            ("InactiveImage/Icon#img", "m_Sprite", None, glyph),
            ("InactiveImage/Icon#rt", "m_AnchoredPosition.x", -(lw / 2 + K.NAV_ICON_GAP + K.NAV_ICON / 2), None),
            ("ActiveImage/Icon#go", "m_IsActive", 1, None),
            ("ActiveImage/Icon#img", "m_Sprite", None, glyph)]
    if i == HOME:     # the state the menu opens in, so the scene reads as it plays
        mods += [("InactiveImage#go", "m_IsActive", 0, None), ("ActiveImage#go", "m_IsActive", 1, None)]
    if i == PORT:
        mods += [("InactiveImage#kt", "colorDrivenExternally", 1, None),   # MenuAvailabilityView owns it
                 ("root#hover", "m_Enabled", 0, None)]                      # its label is not the hover's
    inst = K.Instance(f"Tab{i}", TAB_PREFAB, guid, link,
                      K.rect((0, 1), None, (x, -BAND_H / 2), (tab_width(i), BAND_H)), mods)
    out = []
    K.emit_instance(ids.ctx, inst, BUTTONS["rt"], out)
    added = [t["trigger"]] + ([t["availability"]] if i == PORT else [])
    root_go = kit_src(TAB_PREFAB, "root#go")
    entries = "".join(f"    - targetCorrespondingSourceObject: {K.ref(root_go, guid)}\n      insertIndex: -1\n"
                      f"      addedObject: {{fileID: {a}}}\n" for a in added)
    out[0] = out[0].replace("    m_AddedComponents: []\n", "    m_AddedComponents:\n" + entries)
    pid = t["inst"]
    out.append(stub_go(t["stub_go"], root_go, guid, pid))
    out.append(stub_go(t["stub_inactive"], kit_src(TAB_PREFAB, "InactiveImage#go"), guid, pid))
    out.append(stub_go(t["stub_active"], kit_src(TAB_PREFAB, "ActiveImage#go"), guid, pid))
    out.append(event_trigger(t["trigger"], t["stub_go"], method))
    if i == PORT:
        out.append(stub_go(t["stub_lock"], kit_src(TAB_PREFAB, "Lock#go"), guid, pid))
        out.append(stub_mono(t["stub_label"], kit_src(TAB_PREFAB, "InactiveImage#tmp"), guid, pid, K.TMP_UGUI))
        out.append(f"--- !u!114 &{t['availability']}\n" + K.mono(t["stub_go"], AVAILABILITY_VIEW,
                   "  availability: 0\n"
                   f"  lockedOverlay: {{fileID: {t['stub_lock']}}}\n  unavailableOverlay: {{fileID: 0}}\n"
                   f"  label: {{fileID: {t['stub_label']}}}\n"
                   f"  unavailableLabelColor: {K.col(K.tok('InactiveLight'))}\n  dimOwnGraphics: 1\n"
                   "  dimTint: {r: 1, g: 1, b: 1, a: 0.35}\n  lockedMessage: Not open yet.\n"))
    return out


def keycap_instance(ids, a_index):
    a = ARROWS[a_index]
    k = ids.keys[a_index]
    guid = K.prefab_guid(KEYCAP_PREFAB)
    inst = K.Instance(f"Key{a['side']}", KEYCAP_PREFAB, guid, "Keycap",
                      K.rect((0.5, 0.5), None, (0, 0), keycap_size(a["key"])),
                      [("Label#tmp", "m_text", a["key"], None),
                       ("Label#rt", "m_SizeDelta.x", K.label_width(a["key"], "Label"), None)])
    out = []
    K.emit_instance(ids.ctx, inst, a["rt"], out)
    pid = k["inst"]
    out.append(stub_mono(k["stub_label_kt"], kit_src(KEYCAP_PREFAB, "Label#kt"), guid, pid, K.kit_script("KitText")))
    out.append(stub_mono(k["stub_border_th"], kit_src(KEYCAP_PREFAB, "Border#th"), guid, pid,
                         K.kit_script("KitThemedGraphic")))
    out.append(f"--- !u!114 &{k['hover']}\n" + K.mono(a["go"], K.kit_script("KitHoverTint"),
               f"  theme: {K.ref(11400000, K.THEME, 2)}\n  hoverToken: {K.TOKEN_INDEX['TextLight']}\n"
               + K.refs_list("texts", [k["stub_label_kt"]]) + K.refs_list("graphics", [k["stub_border_th"]])))
    return out


def set_block(doc, name, block):
    """Replace a top-level field (scalar or list) in one document."""
    pat = re.compile(r"^  " + re.escape(name) + r":(?: .*)?\n(?:  - .*\n(?:    .*\n)*)*", re.M)
    assert len(pat.findall(doc)) == 1, f"field {name} not found exactly once"
    return pat.sub(lambda _m: block, doc)


def transform(text):
    sc = Scene(text)
    ids = Ids()
    generated = ids.all()

    # Shape guards: the objects this script edits must still be the ones it was written against.
    for fid, name in ((NAVBAR["go"], "NavBar"), (BUTTONS["go"], "NavBarButtons")):
        assert sc.has(fid) and sc.field(fid, "m_Name") == name, f"{name} ({fid}) not found - re-measure the scene"
    for a in ARROWS:
        assert sc.has(a["go"]) and sc.field(a["go"], "m_Name") == a["name"], f"{a['name']} not found"
        assert sc.script(a["btn"]) == BUTTON and f"m_MethodName: {a['method']}" in sc.docs[a["btn"]]
    assert sc.script(SWITCHER) == SCREEN_SWITCHER, "ScreenSwitcher component moved"
    if any(sc.has(r) for r in RETIRED_ROOTS):   # first run: a minted fileID must be unclaimed
        clash = generated & set(sc.order)
        assert not clash, f"minted fileIDs already in the scene: {sorted(clash)[:3]}"

    # Migration - stands down once the old subtree is gone.
    for root, name in RETIRED_ROOTS.items():
        if not sc.has(root):
            continue
        assert sc.field(root, "m_Name") == name, f"retired root {root} is no longer {name}"
        tab = next((i for i, t in enumerate(TABS) if t[1] == name), None)
        if tab is not None:   # the wiring being re-authored must be the wiring that shipped
            trig = [c for c in sc.components(root) if sc.script(c) == EVENT_TRIGGER]
            assert len(trig) == 1 and f"m_MethodName: {TABS[tab][2]}" in sc.docs[trig[0]], f"{name}: unexpected EventTrigger"
        for fid in sc.subtree(root):
            sc.remove(fid)
    for fid in list(sc.order):
        if fid in generated:
            sc.remove(fid)

    items, xs, row_w = row()
    out = []
    # Band and Rule: plain nodes, emitted by the kit's own node writer.
    band = K.Node("Band", "Band", K.STRETCH)
    # Revision 2026-10-14 (MENU_REDESIGN_B §13.1): the band is translucent so the menu scene shows
    # through it, as it did under the old NavBarGradient; the rule is Neutral Light at 70 %.
    band.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.tok("SurfaceVeryDark", BAND_ALPHA))) \
        .add("th", 114, K.themed("SurfaceVeryDark", BAND_ALPHA))
    rule = K.Node("Rule", "Rule", K.rect((0, 0), (1, 0), (0, 0), (0, 1), (0.5, 0)))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.tok("SurfaceLight", RULE_ALPHA))) \
        .add("th", 114, K.themed("SurfaceLight", RULE_ALPHA))
    K.emit_node(ids.ctx, band, NAVBAR["rt"], out)
    K.emit_node(ids.ctx, rule, NAVBAR["rt"], out)
    for i in range(len(TABS)):
        out += tab_instance(ids, i, xs[1 + i])
    for a_index in range(len(ARROWS)):
        out += keycap_instance(ids, a_index)
    out = [K.unity_empty_scalars(d) for d in out]
    anchor = NAVBAR["rt"]
    for d in out:
        sc.insert_after(anchor, doc_fid(d), d)
        anchor = doc_fid(d)

    # Patched live documents.
    sc.replace(NAVBAR["rt"], rt_doc(NAVBAR["rt"], NAVBAR["go"], [ids.band["rt"], ids.rule["rt"], BUTTONS["rt"]],
                                     NAVBAR["parent_rt"], K.rect((0, 1), (1, 1), (0, -BAND_H / 2), (0, BAND_H))))
    strip = [ARROWS[0]["rt"]] + [t["stub"] for t in ids.tabs] + [ARROWS[1]["rt"]]
    sc.replace(BUTTONS["rt"], rt_doc(BUTTONS["rt"], BUTTONS["go"], strip, NAVBAR["rt"],
                                      K.rect((0.5, 0.5), None, (0, 0), (row_w, BAND_H))))
    sc.replace(BUTTONS["hlg"], body(BUTTONS["hlg"], 114, K.hv_layout(False, (0, 0, 0, 0), GAP, 4, (0, 0), (0, 0)),
                                    BUTTONS["go"], ids.ctx))
    for a_index, a in enumerate(ARROWS):
        k = ids.keys[a_index]
        x = xs[0] if a_index == 0 else xs[-1]
        sc.replace(a["go"], go_doc(a["go"], a["name"], [a["rt"], a["cr"], a["img"], a["btn"], k["hover"]]))
        sc.replace(a["rt"], rt_doc(a["rt"], a["go"], [k["stub"]], BUTTONS["rt"],
                                    K.rect((0, 1), None, (x, -BAND_H / 2), keycap_size(a["key"]))))
        sc.replace(a["img"], body(a["img"], 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True), a["go"], ids.ctx))
        sc.replace(a["btn"], re.sub(r"^  m_Transition: \d+$", "  m_Transition: 0", sc.docs[a["btn"]], flags=re.M))

    sw = sc.docs[SWITCHER]
    sw = set_block(sw, "NavBarLine", "  NavBarLine: {fileID: 0}\n")
    sw = set_block(sw, "NavBarLineSprites", "  NavBarLineSprites: []\n")
    sw = set_block(sw, "NavActiveImages", K.refs_list("NavActiveImages", [t["stub_active"] for t in ids.tabs]))
    sw = set_block(sw, "NavInactiveImages", K.refs_list("NavInactiveImages", [t["stub_inactive"] for t in ids.tabs]))
    sc.replace(SWITCHER, sw)
    return sc.text()


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

PATCHED = {NAVBAR["rt"], BUTTONS["rt"], BUTTONS["hlg"], SWITCHER} | {a[k] for a in ARROWS for k in ("go", "rt", "img", "btn")}


# Patched documents that are NOT regenerated whole: only these fields may differ.
PATCHED_FIELDS = {SWITCHER: ("NavBarLine", "NavBarLineSprites", "NavActiveImages", "NavInactiveImages")}
PATCHED_FIELDS.update({a["btn"]: ("m_Transition",) for a in ARROWS})


def strip_fields(doc, names):
    for name in names:
        doc = re.sub(r"^  " + re.escape(name) + r":(?: .*)?\n(?:  - .*\n(?:    .*\n)*)*", "", doc, flags=re.M)
    return doc


def known_guids():
    out = set(K.PACKAGE_GUIDS) | {EVENT_TRIGGER, BUTTON}
    for root, _dirs, names in os.walk(os.path.join(REPO, "Assets")):
        for n in names:
            if n.endswith(".meta"):
                with open(os.path.join(root, n), errors="ignore") as fh:
                    m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(400), re.M)
                if m:
                    out.add(m.group(1))
    return out


def prefab_anchors(prefab):
    with open(os.path.join(REPO, K.PREFAB_DIR, prefab + ".prefab")) as fh:
        return set(re.findall(r"^--- !u!\d+ &(-?\d+)", fh.read(), re.M))


def validate(before_text, after_text, guids):
    """Raise AssertionError on the first broken invariant."""
    before, sc = Scene(before_text), Scene(after_text)
    ids = Ids()
    generated = ids.all()
    anchors = set(sc.order) | {"0"}

    # 1. Integrity: every local fileID resolves; nothing references a retired object.
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for root in RETIRED_ROOTS:
        assert not sc.has(root), f"retired {RETIRED_ROOTS[root]} still in the scene"

    # 2. Blast radius: only retired docs left, only generated docs arrived, only patched docs changed.
    removed = set(before.order) - set(sc.order)
    added = set(sc.order) - set(before.order)
    assert added <= generated, f"unexpected new documents {sorted(added - generated)[:3]}"
    retired = set()
    for root in RETIRED_ROOTS:
        if before.has(root):
            retired |= set(before.subtree(root))
    assert removed <= retired | generated, f"unexpected removals {sorted(removed - retired - generated)[:3]}"
    changed = {f for f in set(before.order) & set(sc.order) if before.docs[f] != sc.docs[f]}
    assert changed <= PATCHED | generated, f"unexpected edits {sorted(changed - PATCHED - generated)[:3]}"

    # 2b. A patched live document changes only in the fields this script owns.
    for fid, fields in PATCHED_FIELDS.items():
        if before.has(fid) and sc.has(fid):
            assert strip_fields(before.docs[fid], fields) == strip_fields(sc.docs[fid], fields), \
                f"{fid} edited outside {fields}"

    # 3. Generated documents: ASCII, and every guid resolves.
    for fid in generated:
        assert sc.has(fid), f"generated document {fid} missing"
        d = sc.docs[fid]
        assert d.isascii(), f"non-ASCII in {fid}"
        for g in re.findall(r"guid: ([0-9a-f]{32})", d):
            assert g in guids, f"guid {g} in {fid} resolves to no asset"

    # 4. Prefab instances: every override target and stub source exists in its source prefab.
    by_guid = {K.prefab_guid(p): (p, prefab_anchors(p)) for p in (TAB_PREFAB, KEYCAP_PREFAB)}
    for fid in generated:
        d = sc.docs[fid]
        for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                                 r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", d):
            assert g in by_guid and src in by_guid[g][1], f"{fid}: source {src} not in {by_guid.get(g, ('?',))[0]}"

    # 5. Wiring: the nav still works exactly as it did.
    sw = sc.docs[SWITCHER]
    def ref_list(name):
        m = re.search(r"^  " + name + r":\n((?:  - \{fileID: -?\d+\}\n)*)", sw, re.M)
        assert m, f"ScreenSwitcher.{name} is empty"
        return re.findall(r"fileID: (-?\d+)", m.group(1))
    act, ina = ref_list("NavActiveImages"), ref_list("NavInactiveImages")
    assert len(act) == len(ina) == len(TABS)
    tab_guid = K.prefab_guid(TAB_PREFAB)
    for i, (text, link, method) in enumerate(TABS):
        t = ids.tabs[i]
        for stub, key in ((act[i], "ActiveImage#go"), (ina[i], "InactiveImage#go")):
            assert sc.cls[stub] == 1 and f"{{fileID: {kit_src(TAB_PREFAB, key)}, guid: {tab_guid}" in sc.docs[stub] \
                and f"m_PrefabInstance: {{fileID: {t['inst']}}}" in sc.docs[stub], f"{link}: {key} list entry is wrong"
        inst = sc.docs[t["inst"]]
        assert f"m_TransformParent: {{fileID: {BUTTONS['rt']}}}" in inst
        assert re.search(r"propertyPath: m_Name\n      value: " + link + r"\n", inst), f"{link}: name"
        assert re.search(r"propertyPath: m_text\n      value: " + text + r"\n", inst), f"{link}: label"
        assert f"addedObject: {{fileID: {t['trigger']}}}" in inst, f"{link}: EventTrigger not added"
        assert inst.count(f"guid: {glyph_guid(i)}, type: 3}}") == 2, f"{link}: glyph not set on both icons"
        trig = sc.docs[t["trigger"]]
        assert f"m_GameObject: {{fileID: {t['stub_go']}}}" in trig and f"m_MethodName: {method}\n" in trig \
            and f"m_Target: {{fileID: {SWITCHER}}}" in trig and "eventID: 4" in trig, f"{link}: click wiring"
    strip = sc.children(BUTTONS["rt"])
    assert strip == [ARROWS[0]["rt"]] + [t["stub"] for t in ids.tabs] + [ARROWS[1]["rt"]], "strip order"
    for a_index, a in enumerate(ARROWS):
        btn = sc.docs[a["btn"]]
        assert f"m_MethodName: {a['method']}\n" in btn and f"m_TargetGraphic: {{fileID: {a['img']}}}" in btn
        assert ids.keys[a_index]["hover"] in sc.components(a["go"]), f"{a['name']}: hover tint not attached"
        assert f"m_Father: {{fileID: {BUTTONS['rt']}}}" in sc.docs[a["rt"]]
    port = ids.tabs[PORT]
    av = sc.docs[port["availability"]]
    assert f"addedObject: {{fileID: {port['availability']}}}" in sc.docs[port["inst"]]
    assert f"lockedOverlay: {{fileID: {port['stub_lock']}}}" in av and f"label: {{fileID: {port['stub_label']}}}" in av
    assert "availability: 0\n" in av, "PORT must be authored Available: disabledScreens locks it at runtime"
    assert f"{{fileID: {kit_src(TAB_PREFAB, 'Lock#go')}, guid: {tab_guid}" in sc.docs[port["stub_lock"]]
    assert f"{{fileID: {kit_src(TAB_PREFAB, 'InactiveImage#tmp')}, guid: {tab_guid}" in sc.docs[port["stub_label"]]
    assert "m_SizeDelta: {x: 0, y: 84}" in sc.docs[NAVBAR["rt"]]
    assert "NavBarLineSprites: []" in sw and "NavBarLine: {fileID: 0}" in sw


def normalize(doc):
    """Unity folds long flow mappings over lines and may reorder documents; compare content only."""
    doc = re.sub(r",\n\s+", ", ", doc)
    return "\n".join(line.rstrip() for line in doc.split("\n")).strip()


def semantic_equal(a, b):
    sa, sb = Scene(a), Scene(b)
    if set(sa.order) != set(sb.order):
        return False, sorted(set(sa.order) ^ set(sb.order))[:5]
    diff = [f for f in sa.order if normalize(sa.docs[f]) != normalize(sb.docs[f])]
    return not diff, diff[:5]


# --------------------------------------------------------------------------------------------
# Negative controls
# --------------------------------------------------------------------------------------------

def in_doc(text, fid, old, new):
    """Replace inside ONE document, so a plant cannot trip an unrelated check by accident."""
    sc = Scene(text)
    sc.replace(fid, sc.docs[fid].replace(old, new, 1))
    return sc.text()


def self_test(current, guids):
    expected = transform(current)
    validate(current, expected, guids)
    ids = Ids()
    t = ids.tabs
    plants = {
        "swapped NavActiveImages": lambda s: s.replace(f"  - {{fileID: {t[0]['stub_active']}}}\n  - {{fileID: {t[1]['stub_active']}}}\n",
                                                       f"  - {{fileID: {t[1]['stub_active']}}}\n  - {{fileID: {t[0]['stub_active']}}}\n"),
        "HOME lost its EventTrigger": lambda s: s.replace(f"      addedObject: {{fileID: {t[HOME]['trigger']}}}\n", ""),
        "PORT authored Locked": lambda s: in_doc(s, t[PORT]["availability"], "  availability: 0\n", "  availability: 1\n"),
        "dangling reference": lambda s: s.replace(f"m_TargetGraphic: {{fileID: {ARROWS[0]['img']}}}", "m_TargetGraphic: {fileID: 42}"),
        "override targets a missing prefab object": lambda s: s.replace(
            f"target: {{fileID: {kit_src(TAB_PREFAB, 'InactiveImage#tmp')}, guid:", "target: {fileID: 4242, guid:", 1),
        "ScreenSwitcher edited outside the nav fields":
            lambda s: in_doc(s, SWITCHER, "disabledScreenMessage: Not open yet.", "disabledScreenMessage: Closed."),
        "unrelated document edited": lambda s: s.replace("  m_Name: Screens\n", "  m_Name: Screenz\n", 1),
        "ARK lost its glyph": lambda s: s.replace(f"guid: {glyph_guid(1)}, type: 3}}", "guid: 0, type: 3}", 1),
    }
    failed = []
    for name, plant in plants.items():
        bad = plant(expected)
        assert bad != expected, f"plant '{name}' did not apply - fix the self-test"
        try:
            validate(current, bad, guids)
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

    path = os.path.join(REPO, SCENE)
    with open(path, encoding="utf-8", newline="") as fh:
        current = fh.read()
    guids = known_guids()
    if args.self_test:
        self_test(current, guids)
        return 0
    expected = transform(current)
    validate(current, expected, guids)
    migrated = any(Scene(current).has(r) for r in RETIRED_ROOTS)

    if args.check:
        if migrated:
            print(f"FAIL: {SCENE} still carries the old nav subtree (run without --check)")
            return 1
        validate(current, current, guids)          # the wiring of the scene AS COMMITTED
        ok, diff = semantic_equal(current, expected)
        if ok:
            print(f"OK: {SCENE} top nav matches the generator; wiring validated on the committed scene")
            return 0
        print(f"FAIL: {SCENE} wiring is valid but its content drifted from the generator", diff)
        return 1

    if expected == current:
        print("no change")
        return 0
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(expected)
    print(f"wrote {SCENE}" + (" (migrated the old nav subtree)" if migrated else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main())
