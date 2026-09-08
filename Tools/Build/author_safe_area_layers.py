#!/usr/bin/env python3
"""
Author the SAFE-AREA CONTENT LAYERS decided in Docs/UI_ARCHITECTURE_AUDIT.md §1.3.

Every canvas that carries player-facing UI is split into a FULL-BLEED layer - whose job is to
cover the screen (a fade, a scrim, a dimmer, a transition wipe, branded splash art) - and a
CONTENT layer, which is everything readable or tappable. SafeAreaFitter goes on the content
layer, NEVER on a canvas root and never on background art.

Three constraints from §1.3 decide the shape of each edit, and they are why some layers get the
component and others get a new parent instead:

  1. The host must be a DIRECT CHILD of the canvas root - ComputeAnchors normalises against the
     SCREEN, so a rect whose parent is smaller than the canvas resolves against the wrong rect.
  2. The host must be STRETCH-anchored on both axes - the fitter replaces both anchors, so a
     top-strip rect (NavBar) or a point-anchored corner widget needs a fitted PARENT.
  3. Nothing else may write the host's anchors.

The runtime-built canvases (SceneTransitionManager's fade, EnvironmentLoadVeil, the privacy
overlay) and the vessel HUDs are handled in C#, not here - see SafeAreaLayer.cs.

Reparenting preserves relative sibling order in every case, so no draw order changes.

Idempotent: re-running prints "already authored" and exits 0.
Validate-before-write: the whole file is rebuilt in memory and asserted before it is touched.

    python3 Tools/Build/author_safe_area_layers.py [--check]

--check exits 1 if any target does not already carry its layer (for CI).
"""
import hashlib, re, sys, pathlib

SAFE_AREA_FITTER = "2c1afdc693c3463281f69af27bf7461c"   # Assets/_Scripts/UI/SafeAreaFitter.cs
UI_LAYER = 5

# ── YAML plumbing ──────────────────────────────────────────────────────────────────────

class Doc:
    __slots__ = ("cls", "fid", "body")
    def __init__(self, cls, fid, body):
        self.cls, self.fid, self.body = cls, fid, body
    def text(self):
        return f"--- !u!{self.cls} &{self.fid}{self.body}"

class UnityFile:
    def __init__(self, path):
        self.path = pathlib.Path(path)
        raw = self.path.read_text(encoding="utf-8")
        parts = raw.split("\n--- ")
        self.header = parts[0]
        self.docs = []
        for p in parts[1:]:
            m = re.match(r"!u!(\d+) &(\d+)(.*)", p, re.S)
            assert m, f"unparsable document head in {path}: {p[:60]!r}"
            self.docs.append(Doc(m.group(1), m.group(2), m.group(3)))

    def render(self):
        return self.header + "".join("\n" + d.text() for d in self.docs)

    def by_fid(self, fid):
        for d in self.docs:
            if d.fid == fid:
                return d
        return None

    def all_fids(self):
        return {d.fid for d in self.docs}

    def go_named(self, name):
        """The one GameObject with this m_Name. Asserts uniqueness - a name that matches twice is
        a target this script must not guess at."""
        hits = [d for d in self.docs
                if d.cls == "1" and re.search(rf"^  m_Name: {re.escape(name)}$", d.body, re.M)]
        assert len(hits) == 1, f"{self.path.name}: expected 1 GameObject named {name!r}, found {len(hits)}"
        return hits[0]

    def transform_of(self, go):
        """The RectTransform among a GameObject's components."""
        for m in re.finditer(r"- component: \{fileID: (\d+)\}", go.body):
            d = self.by_fid(m.group(1))
            if d is not None and d.cls in ("224", "4"):
                return d
        raise AssertionError(f"{self.path.name}: no transform on {go.fid}")

    def rt_named(self, name):
        return self.transform_of(self.go_named(name))

    def children(self, rt):
        seg = rt.body.split("m_Children:")[1].split("m_Father:")[0]
        return re.findall(r"- \{fileID: (\d+)\}", seg)

    def set_children(self, rt, fids):
        head, rest = rt.body.split("m_Children:", 1)
        _, tail = rest.split("m_Father:", 1)
        listing = "".join(f"\n  - {{fileID: {f}}}" for f in fids) if fids else " []"
        if fids:
            rt.body = head + "m_Children:" + listing + "\n  m_Father:" + tail
        else:
            rt.body = head + "m_Children:" + listing + "\n  m_Father:" + tail

    def prefab_instance_of(self, stripped_fid):
        """For a stripped transform (a nested prefab instance's root), the PrefabInstance doc that
        owns it - which is where its parent link lives, as m_TransformParent."""
        d = self.by_fid(stripped_fid)
        if d is None:
            return None
        m = re.search(r"m_PrefabInstance: \{fileID: (\d+)\}", d.body)
        return self.by_fid(m.group(1)) if m else None

    def fresh_fid(self, seed):
        """A deterministic fileID that is free in this file. Deterministic so a re-run of the
        generator produces the same asset rather than a spurious diff."""
        n = int(hashlib.md5(seed.encode()).hexdigest()[:15], 16)
        taken = self.all_fids()
        while str(n) in taken or n < 1000:
            n += 1
        return str(n)

def make_layer(uf, name, parent_rt, seed, layer=UI_LAYER):
    """A full-stretch, zero-offset GameObject carrying only a SafeAreaFitter. Returns its
    RectTransform doc. Not yet parented - the caller places it."""
    go_fid = uf.fresh_fid(seed + ":go")
    rt_fid = uf.fresh_fid(seed + ":rt")
    mb_fid = uf.fresh_fid(seed + ":mb")
    assert len({go_fid, rt_fid, mb_fid}) == 3

    go = Doc("1", go_fid, f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {rt_fid}}}
  - component: {{fileID: {mb_fid}}}
  m_Layer: {layer}
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
""")
    rt = Doc("224", rt_fid, f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_fid}}}
  m_LocalRotation: {{x: -0, y: -0, z: -0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_rt.fid}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 1, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0.5, y: 0.5}}
""")
    mb = Doc("114", mb_fid, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_fid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SAFE_AREA_FITTER}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
""")
    uf.docs += [go, rt, mb]
    return rt

def attach_fitter(uf, go, seed):
    """Adds a SafeAreaFitter to an EXISTING GameObject - only valid where that object is already a
    full-stretch, zero-offset direct child of the canvas root and nothing else writes its anchors."""
    rt = uf.transform_of(go)
    assert re.search(r"m_AnchorMin: \{x: 0, y: 0\}", rt.body), f"{go.fid}: not stretch-anchored (min)"
    assert re.search(r"m_AnchorMax: \{x: 1, y: 1\}", rt.body), f"{go.fid}: not stretch-anchored (max)"
    mb_fid = uf.fresh_fid(seed + ":mb")
    go_fid = go.fid
    uf.docs.append(Doc("114", mb_fid, f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go_fid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SAFE_AREA_FITTER}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
"""))
    go.body = re.sub(r"(\n  m_Layer:)", f"\n  - component: {{fileID: {mb_fid}}}\\1", go.body, count=1)
    return mb_fid

def reparent(uf, child_fids, old_rt, new_rt):
    """Move transforms from one parent to another, preserving their relative order. Handles both
    plain transforms (m_Father on the transform) and nested prefab instances, whose parent link is
    m_TransformParent inside the PrefabInstance doc - the stripped transform has no m_Father."""
    remaining = [f for f in uf.children(old_rt) if f not in child_fids]
    uf.set_children(old_rt, remaining)
    uf.set_children(new_rt, uf.children(new_rt) + list(child_fids))
    for f in child_fids:
        d = uf.by_fid(f)
        assert d is not None, f"child {f} not in {uf.path.name}"
        if "m_Father:" in d.body:
            d.body = re.sub(r"m_Father: \{fileID: \d+\}",
                            f"m_Father: {{fileID: {new_rt.fid}}}", d.body, count=1)
        else:
            pi = uf.prefab_instance_of(f)
            assert pi is not None, f"{f} has no m_Father and no PrefabInstance"
            pi.body = re.sub(r"m_TransformParent: \{fileID: \d+\}",
                             f"m_TransformParent: {{fileID: {new_rt.fid}}}", pi.body, count=1)

def has_fitter(uf):
    return SAFE_AREA_FITTER in uf.render()

# ── the edits ──────────────────────────────────────────────────────────────────────────

GAME_CANVASES = [
    (pathlib.Path("Assets/_Prefabs/CORE/GameCanvas.prefab"), "GameCanvas"),
    (pathlib.Path("Assets/_Prefabs/GameCanvas-SkimRace.prefab"), "GameCanvas-SkimRace"),
]

def author_game_canvas(path, root_name):
    uf = UnityFile(path)
    root = uf.rt_named(root_name)

    # MiniGameHUD and Pause Screen are full-stretch, zero-offset, art-free direct children of the
    # canvas root - the textbook host, so the component goes ON them and nothing is reparented.
    # MiniGameHUD carries every edge-pinned HUD element; Pause Screen's Resume/Home buttons sit at
    # y=47 with height 42, inside the ~60-unit bottom inset.
    for name in ("MiniGameHUD", "Pause Screen"):
        attach_fitter(uf, uf.go_named(name), f"{root_name}:{name}")

    # The three invite widgets are point-anchored at (0,0) + (141, 31) with height 43, so they span
    # 9.5-52.5 canvas units - entirely inside the bottom inset. Point-anchored means they cannot
    # host the fitter (driving their anchors would slide them, not resize them), so they get a
    # fitted parent. It goes LAST so they keep drawing above the end-game panels.
    invites = ["MultiplayerInviteRequest", "MultiplayerInviteRequestDenied", "MultiplayerInvited"]
    invite_fids = [uf.rt_named(n).fid for n in invites]
    assert uf.children(root)[-3:] == invite_fids, \
        f"{path.name}: invite widgets are no longer the last three children - re-check draw order"
    layer = make_layer(uf, "Safe Area (Invites)", root, f"{root_name}:invites")
    uf.set_children(root, uf.children(root) + [layer.fid])
    reparent(uf, invite_fids, root, layer)
    return uf

def author_connecting_panel():
    path = pathlib.Path("Assets/_Prefabs/UI Elements/In Game/ConnectingPanel.prefab")
    uf = UnityFile(path)
    root = uf.rt_named("ConnectingPanel")
    # The root carries the panel's own full-screen Image - it BLEEDS. Its content does not:
    # PlayerIcons is anchored bottom-right at (-224, 97), and Slider / GameModeText / Status Text
    # are 1000 wide, i.e. nearly full width. Camera is a non-UI child and stays put.
    names = ["Status Text", "MaelstromRankText", "GameModeText", "Level Preview", "Slider", "PlayerIcons"]
    fids = [uf.rt_named(n).fid for n in names]
    kids = uf.children(root)
    assert all(f in kids for f in fids), f"{path.name}: expected content children are not all at the root"
    layer = make_layer(uf, "Safe Area", root, "ConnectingPanel:content")
    uf.set_children(root, uf.children(root) + [layer.fid])
    reparent(uf, fids, root, layer)
    # Camera (non-UI) then the layer: the content keeps drawing in the same order above the Image.
    assert uf.children(root)[-1] == layer.fid
    return uf, path

def author_menu_main():
    path = pathlib.Path("Assets/_Scenes/Menu_Main.unity")
    uf = UnityFile(path)
    root = uf.rt_named("UI_Refactored")
    # The menu canvas has NO background art - the backdrop is the 3D lava-lamp scene behind an
    # overlay canvas - so every child is content and one layer takes all of them. NavBar's arrows
    # sit at ~3% / ~95% of width and ToggleGameMenuButton is 120x120 point-anchored at (0,0):
    # neither can host the fitter itself, which is why this is a wrap rather than N components.
    kids = list(uf.children(root))
    assert len(kids) == 7, f"{path.name}: expected 7 canvas children, found {len(kids)}"
    layer = make_layer(uf, "Safe Area", root, "Menu_Main:content")
    uf.set_children(root, kids + [layer.fid])
    reparent(uf, kids, root, layer)
    assert uf.children(root) == [layer.fid]
    return uf, path

def author_bootstrap():
    path = pathlib.Path("Assets/_Scenes/Bootstrap.unity")
    uf = UnityFile(path)
    # LoadingPanel is both the branded splash art AND the surface SceneTransitionManager adopts as
    # the app-wide fade (bumped to sort order 32767), so the panel itself BLEEDS. Its Status Text
    # sits at y=-460 with height 92 - bottom edge at -506, 34 units off the screen bottom.
    panel = uf.rt_named("LoadingPanel")
    fids = [uf.rt_named("Status Text").fid, uf.rt_named("Button").fid]
    loader = [f for f in uf.children(panel) if f not in fids]
    assert len(loader) == 1, f"{path.name}: expected Status Text + Loader + Button under LoadingPanel"
    ordered = [f for f in uf.children(panel)]           # keep authored order
    layer = make_layer(uf, "Safe Area", panel, "Bootstrap:splash")
    uf.set_children(panel, ordered + [layer.fid])
    reparent(uf, ordered, panel, layer)
    return uf, path

def author_authentication():
    path = pathlib.Path("Assets/_Scenes/Authentication.unity")
    uf = UnityFile(path)
    root = uf.rt_named("Canvas")
    bg = uf.rt_named("Background").fid                   # full-stretch Image - BLEEDS
    kids = list(uf.children(root))
    assert kids[0] == bg, f"{path.name}: Background is not the first child - re-check draw order"
    content = kids[1:]                                   # AuthPanel, UsernameSetupPanel, Status Text, Loader
    assert len(content) == 4, f"{path.name}: expected 4 content children, found {len(content)}"
    layer = make_layer(uf, "Safe Area", root, "Authentication:content")
    uf.set_children(root, kids + [layer.fid])
    reparent(uf, content, root, layer)
    assert uf.children(root) == [bg, layer.fid]
    return uf, path

# ── driver ─────────────────────────────────────────────────────────────────────────────

def main():
    check = "--check" in sys.argv

    targets = [(path, (lambda p=path, r=root: author_game_canvas(p, r)))
               for path, root in GAME_CANVASES]
    targets += [(_path_of(fn), fn)
                for fn in (author_connecting_panel, author_menu_main,
                           author_bootstrap, author_authentication)]

    written, already = [], []
    for path, fn in targets:
        if has_fitter(UnityFile(path)):
            already.append(path)
            continue
        if check:
            print(f"MISSING safe-area layer: {path}")
            return 1
        out = fn()
        uf = out[0] if isinstance(out, tuple) else out
        text = uf.render()
        _assert_sane(uf, text, path)
        path.write_text(text, encoding="utf-8")
        written.append(path)

    for p in already:
        print(f"already authored: {p}")
    for p in written:
        print(f"authored: {p}")
    if check:
        print("safe-area layers: OK")
    return 0

def _path_of(fn):
    return {
        author_connecting_panel: pathlib.Path("Assets/_Prefabs/UI Elements/In Game/ConnectingPanel.prefab"),
        author_menu_main:        pathlib.Path("Assets/_Scenes/Menu_Main.unity"),
        author_bootstrap:        pathlib.Path("Assets/_Scenes/Bootstrap.unity"),
        author_authentication:   pathlib.Path("Assets/_Scenes/Authentication.unity"),
    }[fn]

def _assert_sane(uf, text, path):
    """Every local fileID a document references must exist, and no fileID may be defined twice.
    A dangling reference is what a hand-authored YAML edit produces when it goes wrong, and Unity
    reports it as a silently missing object rather than as an error."""
    fids = [d.fid for d in uf.docs]
    assert len(fids) == len(set(fids)), f"{path}: duplicate fileID after edit"
    known = set(fids)
    for d in uf.docs:
        for m in re.finditer(r"\{fileID: (\d+)\}", d.body):
            f = m.group(1)
            if f == "0" or f == "11500000":
                continue
            # Cross-file references carry a guid on the same line; local ones do not.
            line = d.body[d.body.rfind("\n", 0, m.start()) + 1: d.body.find("\n", m.end())]
            if "guid:" in line:
                continue
            assert f in known, f"{path}: dangling local fileID {f} in doc {d.fid}"

if __name__ == "__main__":
    sys.exit(main())
