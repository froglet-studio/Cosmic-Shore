#!/usr/bin/env python3
"""
Author ITEM 8c of the style-B menu redesign - the TOY CONFIGURE window - as prefabs, and wire it into
Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 17.4; the approved ToyConfigure / ToyConfigurePaint /
ToyConfigureSpawn boards on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Toybox/
        ToyVariantTile.prefab     one variant: KitToyVariantCard (ToyVariantCard) on a KitTile - the
                                  option's glyph, its name and its second line
        ToyConfigureView.prefab   the window body: header (the toy's section, its name, Back), the live
                                  preview with its caption, the About panel, and the variant panel -
                                  breadcrumb with the panel's own Back, the count, a three-column variant
                                  grid, the status line, SWITCH / NAVIGATE

    Menu_Main.unity, `ModalWindows/ToyboxGameConfigureModal`:
        Content's old Border and ConfigurationContent, and the inactive ToyVariantTemplate, are deleted;
        one ToyConfigureView instance goes under Content (the window's open animation scales Content).
        ToyConfigureModal is KEPT and re-pointed into the view; its card prefab is ToyVariantTile.prefab
        (it was a scene template). The two ControllerButtonPress objects that fired Navigate and Close
        on the same press as the focused control went with the old tree.

STANDING DOWN: the migration runs only while the old objects exist; the documents this script owns are
re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_toyconfigure.py              # write
    python3 Tools/Build/author_menu_toyconfigure.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_toyconfigure.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_toybox", os.path.join(HERE, "author_menu_toybox.py"))
TB = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(TB)

AR, P, C, A, K, T, H, M = TB.AR, TB.P, TB.C, TB.A, TB.K, TB.T, TB.H, TB.M
REPO = K.REPO
SCENE = P.SCENE
OUT_DIR = TB.OUT_DIR
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR = C.TL, C.TR
ref, mono, refs = A.ref, A.mono, A.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node, ring_node = A.image_node, A.art_node, A.ring_node
c = A.c

PREFABS = ("ToyVariantTile", "ToyConfigureView")
GRID = TB.AR_GRID
S = dict(card=C.script("KitToyVariantCard"), modal=C.script("ToyConfigureModal"), preview=C.script("ToyPreviewCamera"),
         tile=A.S["tile"], kt=A.S["kt"], button=A.S["button"], close=A.S["close"], crystal=TB.S["crystal"])
C.STUB_KIND.update({"preview": (114, S["preview"])})
TILE_W, TILE_H, GAP_X, GAP_Y = 270, 74, 17, 12
PANEL_W = 900


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def panel(root, key, name, r, stagger, grid=True):
    n = root.child(N(key, name, r))
    n.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=stagger))
    C.frame(n, f"{key}/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    if grid:
        n.child(M.panel_sea_node(f"{key}/Grid"))
    M.bracket_nodes(n, f"{key}/Bracket")
    return n


# --------------------------------------------------------------------------------------------
# ToyVariantTile.prefab
# --------------------------------------------------------------------------------------------

def build_tile():
    root = N("root", "ToyVariantTile", R(size=(TILE_W, TILE_H)))
    ring_node(root, "FocusRing", 10)
    fill = root.child(N("Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    art_node(root, "Icon", "Icon", R((0, 0.5), None, (14, 0), (44, 44), (0, 0.5)), None, preserve=True)
    nm = root.child(N("Name", "Name", R((0, 1), None, (70, -14), (186, 24), (0, 1))))
    nm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Variant", "Body", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Body", "TextLight", driven=True))
    dt = root.child(N("Detail", "Detail", R((0, 1), None, (70, -40), (186, 20), (0, 1))))
    dt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("", "BodySmall", tok("NeutralLightest")))) \
        .add("kt", 114, K.kit_text("BodySmall", "NeutralLightest"))
    body = A.tile_body(c("FocusRing", "img"), c("Fill", "img"), c("Border", "img"), 10,
                       primary=[c("Name", "tmp")], art=[c("Icon", "img")],
                       on_click=[lambda ctx: (ctx.c("root", "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])
    root.add("tile", 114, body)
    root.add("card", 114, lambda ctx, go: mono(go, S["card"],
             f"  background: {ref(ctx.c('Fill', 'img'))}\n  border: {ref(ctx.c('Border', 'img'))}\n"
             f"  nameText: {ref(ctx.c('Name', 'tmp'))}\n  detailText: {ref(ctx.c('Detail', 'tmp'))}\n"
             "  selectedMarker: {fileID: 0}\n"
             "  restFill: 0.35\n  selectedFill: 0.6\n  restRim: 0.55\n  bloomPadding: 12\n  selectedScale: 1.04\n"
             "  selectionDuration: 0.16\n  pulsePeriod: 1.6\n  pulseFloor: 0.45\n"
             "  fallbackCta: {r: 0.62, g: 1, b: 0.16, a: 1}\n"
             f"  tile: {ref(ctx.c('root', 'tile'))}\n  icon: {ref(ctx.c('Icon', 'img'))}\n"
             f"  detailStyle: {ref(ctx.c('Detail', 'kt'))}\n  vessels: {K.ref(11400000, A.VESSEL_LIST, 2)}\n"))
    root.add("audio", 114, A.menu_audio(11)).add("scroll", 114, A.scroll_into())
    # Motion (section 22.8): variants rise in by grid position, on spawn and on every open.
    root.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", by_sibling=True, distance=8))
    return root


# --------------------------------------------------------------------------------------------
# ToyConfigureView.prefab
# --------------------------------------------------------------------------------------------

def header_back(parent):
    """A.back_button WITHOUT ModalWindowCloseButton: this Back steps out of a branch first
    (ToyConfigureModal.OnBackPressed), and only closes the window from the toy's top layer."""
    b = A.back_button(parent)
    b.comps = [x for x in b.comps if x[0] != "close"]
    b.add("audio", 114, A.menu_audio(11))
    return b


def build_view():
    root = N("root", "ToyConfigureView", STRETCH)
    image_node(root, "NavDim", "NavDim", R((0, 1), (1, 1), (0, 0), (0, 84), (0.5, 1)), "SurfaceBlack", alpha=0.75)
    image_node(root, "Backdrop", "Backdrop", R((0, 0), (1, 1), (0, -42), (0, -84)), "SurfaceBlack", raycast=True)

    # Sibling order is focus order (ScreenSwitcher.Refocus): the variant panel first, so the pad lands
    # on the first variant; the header's Back last.
    vp = panel(root, "Variants", "Variants", TR(40, 196, PANEL_W, 844), 2)
    row = vp.child(N("Variants/Head", "Head", TL(28, 24, 844, 40)))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 3, (0, 0), (0, 0)))
    w, h = K.button_size("BACK", small=True)
    lb = row.child(N("Variants/Head/LayerBack", "LayerBack", R((0, 0.5), None, (0, 0), (w, h), (0, 0.5))))
    lb.add("cg", 225, canvas_group(False)).add("le", 114, le(ignore=True, pw=w, ph=h))
    lb.children.append(P.Inst("LayerBackButton", "KitButtonSmall", "LayerBackButton", STRETCH,
                              K.button_mods("KitButtonSmall", "Default", False, "BACK", small=True)))
    path = row.child(N("Variants/Head/Path", "Path", R((0, 0.5), None, (0, 0), (600, 18), (0, 0.5))))
    label(path, "Toy", "Label", "TextLight").add("le", 114, le(pw=600, ph=18))
    label(vp.child(N("Variants/Count", "Count", TR(28, 35, 120, 18))), "0", "Label", "NeutralLightest", align="right")
    sc = vp.child(N("Variants/Scroll", "Scroll", TL(28, 82, 844, 640)))
    sc.add("scroll", 114, A.scroll_rect("Variants/Scroll/Viewport/Content", "Variants/Scroll/Viewport", 20))
    view = sc.child(N("Variants/Scroll/Viewport", "Viewport", STRETCH))
    view.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, A.rect_mask())
    content = view.child(N("Variants/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("grid", 114, lambda ctx, go: mono(go, GRID,
                "  m_Padding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 4\n    m_Bottom: 4\n"
                "  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n"
                f"  m_CellSize: {{x: {TILE_W}, y: {TILE_H}}}\n  m_Spacing: {{x: {GAP_X}, y: {GAP_Y}}}\n"
                "  m_Constraint: 1\n  m_ConstraintCount: 3\n")).add("csf", 114, K.fitter(0, 2))
    image_node(vp, "Variants/Rule", "Rule", TL(28, 736, 844, 1), "SurfaceDark")
    label(vp.child(N("Variants/Status", "Status", TL(28, 770, 460, 26))), "Pick a variation.", "Body", "NeutralLightest")
    x = 28
    for key, variant, cap in (("Navigate", "Default", "NAVIGATE"), ("Switch", "Play", "SWITCH")):
        bw, bh = K.button_size(cap)
        bw = max(bw, 148)
        vp.children.append(P.Inst(f"{key}Button", "KitButton", f"{key}Button",
                                  R((1, 0), None, (-x, 30), (bw, bh), (1, 0)), K.button_mods("KitButton", variant, True, cap)))
        x += bw + 14

    pv = panel(root, "Preview", "Preview", TL(40, 196, PANEL_W, 506), 1)
    surf = pv.child(N("Preview/Surface", "Surface", R((0, 0), (1, 1), (0, 0), (-4, -4))))
    surf.add("cr", 222, K.canvas_renderer()).add("raw", 114, C.raw_image()) \
        .add("cam", 114, lambda ctx, go: mono(go, S["preview"],
             "  distanceFactor: 3.2\n  fieldOfView: 42\n  liftFactor: 0.35\n  orbitDegreesPerSecond: 12\n"
             "  renderRate: 20\n  resolution: 512\n  farReachFactor: 5\n"))
    image_node(pv, "Preview/CaptionBand", "CaptionBand", R((0, 0), (1, 0), (0, 0), (-4, 52), (0.5, 0)), "SurfaceBlack",
               K.kit_sprite("kit_fade_v"))
    label(pv.child(N("Preview/Live", "Live", R((0, 0), None, (22, 20), (300, 18), (0, 0)))), "Live preview", "Label", "TextLight")
    label(pv.child(N("Preview/Caption", "Caption", R((1, 0), None, (-22, 20), (300, 18), (1, 0)))), "", "Label",
          "NeutralLightest", align="right")

    ab = panel(root, "About", "About", TL(40, 722, PANEL_W, 318), 3, grid=False)
    label(ab.child(N("About/Heading", "Heading", TL(24, 28, 600, 18))), "About", "Label", "TextLight")
    d = ab.child(N("About/Text", "Text", TL(24, 54, 852, 240)))
    d.add("cr", 222, K.canvas_renderer()) \
        .add("tmp", 114, text("", "Body", tok("TextLight"), valign="top", wrap=True)) \
        .add("kt", 114, K.kit_text("Body", "TextLight"))

    hd = root.child(N("Header", "Header", R((0, 1), (1, 1), (0, -108), (-80, 64), (0.5, 1))))
    hd.add("cg", 225, M.canvas_group()).add("in", 114, M.entrance("Rise", distance=10))
    label(hd.child(N("Header/Eyebrow", "Eyebrow", R((0, 1), None, (0, 0), (500, 18), (0, 1)))), "Toy", "Label",
          "NeutralLightest")
    t = hd.child(N("Header/Title", "Title", R((0, 1), None, (0, -20), (1100, 46), (0, 1))))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Toy", "H1", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    header_back(hd)
    return root


def prefab_outputs():
    out = {}
    for name, build in (("ToyVariantTile", build_tile), ("ToyConfigureView", build_view)):
        out[prefab_path(name)] = P.prefab_text(build(), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

IDS = dict(go="1669082606718284088", modal="8758355337821682526", content_go="4818025234413526023",
           retired={"2067550918557342253": "Border", "2795622648916823503": "ConfigurationContent",
                    "129582194": "ToyVariantTemplate"})
CRYSTAL_HANDLER = TB.CRYSTAL_HANDLER
MODAL_ORDER = ("titleText", "descriptionText", "categoryText", "preview", "variantsRoot", "variantContent",
               "variantCardPrefab", "navigateButton", "switchButton", "backButton", "pathText", "countText",
               "layerBackButton", "layerBackGroup", "statusText", "previewCaption", "aboutHeading",
               "crystalClickHandler", "arrivalDistanceFactor", "freestyleHandoffTimeout")
BUTTONS = {"NavigateButton": "KitButton", "SwitchButton": "KitButton", "LayerBackButton": "KitButtonSmall"}
MODAL_REFS = {
    "titleText": ("Header/Title#tmp", "tmp"), "descriptionText": ("About/Text#tmp", "tmp"),
    "categoryText": ("Header/Eyebrow#tmp", "tmp"), "preview": ("Preview/Surface#cam", "preview"),
    "variantsRoot": ("Variants/Scroll#go", "go"), "variantContent": ("Variants/Scroll/Viewport/Content#rt", "rt"),
    "navigateButton": ("NavigateButton|root#button", "button"), "switchButton": ("SwitchButton|root#button", "button"),
    "backButton": ("Back#button", "button"), "pathText": ("Variants/Head/Path#tmp", "tmp"),
    "countText": ("Variants/Count#tmp", "tmp"), "layerBackButton": ("LayerBackButton|root#button", "button"),
    "layerBackGroup": ("Variants/Head/LayerBack#cg", "cg"), "statusText": ("Variants/Status#tmp", "tmp"),
    "previewCaption": ("Preview/Caption#tmp", "tmp"), "aboutHeading": ("About/Heading#tmp", "tmp"),
}


class ViewMount(C.Mount):
    def __init__(self):
        self.view = "ToyConfigureView"
        self.ctx = P.Ctx("Menu_Main/ToyConfigure")
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = asset_guid(self.view)
        self.kind = None

    def src(self, key):
        V = K.FileIds(self.view)
        if "|" in key:
            inst, src_key = key.split("|")
            return C.derived(K.source_fid(BUTTONS[inst], src_key), V(inst + "#inst"))
        return V(key)

    def refs(self):
        return list(MODAL_REFS.values())

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", self.view, self.guid, self.view, STRETCH, mods), parent_rt, out)
        for key, comp in self.refs():
            out.append(self.stub_text(key, comp))
        return [K.unity_empty_scalars(d) for d in out]

    def ids(self):
        return {self.inst, self.stub} | {self.stub_fid(k) for k, _ in self.refs()}


def tile_prefab_ref():
    return K.ref(K.FileIds("ToyVariantTile")("root#card"), asset_guid("ToyVariantTile"))


def modal_doc(old, mount):
    head = old[:old.index("  titleText:")]
    fields, _ = H.doc_fields(old[len(head):])
    values = dict(fields)
    assert set(values) <= set(MODAL_ORDER), f"ToyConfigureModal carries fields this script does not know: {set(values) - set(MODAL_ORDER)}"
    for k, (key, _) in MODAL_REFS.items():
        values[k] = f" {ref(mount.stub_fid(key))}"
    values["variantCardPrefab"] = f" {tile_prefab_ref()}"
    values["crystalClickHandler"] = f" {ref(CRYSTAL_HANDLER)}"
    values.setdefault("arrivalDistanceFactor", " 2.4")
    values.setdefault("freestyleHandoffTimeout", " 8")
    return head + "".join(f"  {k}:{values[k]}\n" for k in MODAL_ORDER)


def transform(text_):
    sc = T.Scene(text_)
    assert sc.field(IDS["go"], "m_Name") == "ToyboxGameConfigureModal", "ToyboxGameConfigureModal moved"
    assert sc.script(IDS["modal"]) == S["modal"], "ToyConfigureModal moved"
    assert sc.script(CRYSTAL_HANDLER) == S["crystal"], "the menu's MenuCrystalClickHandler moved"
    mount = ViewMount()
    C.mount_window(sc, IDS, mount)
    # The retired template hung off the window root, beside Content: the root keeps Content only.
    root_rt = sc.rect_of(IDS["go"])
    sc.replace(root_rt, T.set_block(sc.docs[root_rt], "m_Children",
                                    K.refs_list("m_Children", [sc.rect_of(IDS["content_go"])])))
    sc.replace(IDS["modal"], modal_doc(sc.docs[IDS["modal"]], mount))
    return sc.text(), mount


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def prefab_source(guid, files):
    for name in PREFABS:
        if asset_guid(name) == guid:
            return files[prefab_path(name)]
    for name in TB.PREFABS:
        if asset_guid(name) == guid:
            return open(os.path.join(REPO, TB.prefab_path(name)), encoding="utf-8").read()
    return TB.prefab_source(guid, files)


def prefab_objects(guid, files, memo):
    if guid in memo:
        return memo[guid]
    text_ = prefab_source(guid, files)
    objs = A.anchors_of(text_) if text_ is not None else None
    if objs is not None:
        for m in re.finditer(r"^--- !u!1001 &(-?\d+)\n(.*?)(?=^--- |\Z)", text_, re.M | re.S):
            pid = int(m.group(1))
            src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", m.group(2)).group(1)
            inner = prefab_objects(src, files, memo)
            if inner is not None:
                objs |= {str(C.derived(o, pid)) for o in inner}
    memo[guid] = objs
    return objs


def check_sources(path, text_, files, memo):
    for src, g in re.findall(r"(?:target|m_CorrespondingSourceObject|targetCorrespondingSourceObject): "
                             r"\{fileID: (-?\d+), guid: ([0-9a-f]{32})", text_):
        objs = prefab_objects(g, files, memo)
        if objs is not None:
            assert src in objs, f"{path}: source object {src} is not in the prefab {g}"


TMP, IMG = K.TMP_UGUI, K.IMAGE
CARD_FIELDS = dict(nameText=TMP, detailText=TMP, tile=S["tile"], icon=IMG, detailStyle=S["kt"])


def validate(files):
    M.require_entrances(files, {prefab_path("ToyConfigureView"): 4, prefab_path("ToyVariantTile"): 1})
    guids = T.known_guids() | P.PACKAGE | {GRID, C.RAW_IMAGE}
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    memo = {}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)
        check_sources(path, text_, files, memo)
    tile = files[prefab_path("ToyVariantTile")]
    _, body = A.doc_of(tile, K.FileIds("ToyVariantTile")("root#card"))
    assert body and S["card"] in body, "ToyVariantTile: the root carries no KitToyVariantCard"
    for field, want in CARD_FIELDS.items():
        C.wired(tile, body, field, want, "ToyVariantTile")
    view = files[prefab_path("ToyConfigureView")]
    assert "guid: " + S["close"] not in view, \
        "ToyConfigureView: no ModalWindowCloseButton - its Back steps out of a branch before it closes the window"
    _, cam = A.doc_of(view, K.FileIds("ToyConfigureView")("Preview/Surface#cam"))
    assert cam and S["preview"] in cam, "ToyConfigureView: the preview surface carries no ToyPreviewCamera"
    _, raw = A.doc_of(view, K.FileIds("ToyConfigureView")("Preview/Surface#raw"))
    assert raw and C.RAW_IMAGE in raw, "ToyConfigureView: ToyPreviewCamera needs its RawImage"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files, memo)
    for go, name in IDS["retired"].items():
        assert not sc.has(go), f"Toy configure still holds retired {name}"
    mount = ViewMount()
    doc = sc.docs[IDS["modal"]]
    for field, (key, kind) in MODAL_REFS.items():
        fid = mount.stub_fid(key)
        assert f"  {field}: {{fileID: {fid}}}" in doc, f"ToyConfigureModal.{field} does not point into the view"
        cls, scr = C.STUB_KIND[kind]
        assert sc.cls.get(fid) == cls and (scr is None or sc.script(fid) == scr), f"ToyConfigureModal.{field} stub is the wrong kind"
    assert f"  variantCardPrefab: {tile_prefab_ref()}" in doc, "ToyConfigureModal does not spawn ToyVariantTile.prefab"
    assert f"  crystalClickHandler: {{fileID: {CRYSTAL_HANDLER}}}" in doc, "ToyConfigureModal lost its freestyle toggle"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def self_test(files):
    def expect(label_, mutate):
        bad = dict(files)
        mutate(bad)
        try:
            validate(bad)
        except AssertionError as e:
            print(f"  caught: {label_} ({e})")
            return True
        print(f"  MISSED: {label_}")
        return False
    tile, view = prefab_path("ToyVariantTile"), prefab_path("ToyConfigureView")
    mount = ViewMount()
    close = A.mono("0", S["close"], "")
    cases = [
        ("the window's entrances stripped", lambda f: f.__setitem__(prefab_path("ToyConfigureView"), M.strip_entrances(f[prefab_path("ToyConfigureView")]))),
        ("a variant tile with no glyph", lambda f: f.__setitem__(tile, re.sub(r"(  icon: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[tile]))),
        ("a close-the-window button on the branch-aware Back", lambda f: f.__setitem__(view, f[view] + "--- !u!114 &1\n" + close)),
        ("the card prefab back on the scene template", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  variantCardPrefab: {tile_prefab_ref()}", "  variantCardPrefab: {fileID: 0}"))),
        ("the panel Back unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  layerBackButton: {{fileID: {mount.stub_fid('LayerBackButton|root#button')}}}", "  layerBackButton: {fileID: 0}"))),
    ]
    ok = all(expect(l, m) for l, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    print(f"OK: {len(PREFABS)} toy configure prefabs and Menu_Main validate")
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
