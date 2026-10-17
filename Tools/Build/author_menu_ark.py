#!/usr/bin/env python3
"""
Author ITEM 7 of the style-B menu redesign - the ARK screen's ELEMENTAL CONVERTER overlay - as a prefab,
and mount it in Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 16; the approved Ark / ArkConverting
boards, revision 2, on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Ark/
        ArkView.prefab   the overlay over the converter's 3D picture: the title band, the recipe panel
                         (four ConverterRecipeRows in fly-in order, the Omni block), and the converter bar
                         (the run's clock, four segments with their sparks, CONVERT with the sweep)

    Menu_Main.unity, `ArkScreen/ElementalConverter` (author_elemental_converter.py made it):
        One ArkView instance is mounted under the converter, which fills ArkScreen exactly (ScreenSwitcher
        lays the screen out at the full safe height, under the translucent nav);
        ElementalConverterView's style-B fields are wired into the view, which switches the first cut's
        code-built title and button off. The picture stays built in code, behind the overlay.

The documents this script owns are re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_ark.py              # write
    python3 Tools/Build/author_menu_ark.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_ark.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile", os.path.join(HERE, "author_menu_profile.py"))
P = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(P)

C, A, K, T, H, M = P.C, P.A, P.K, P.T, P.H, P.M
REPO = K.REPO
SCENE = P.SCENE
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Ark"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, refs = C.TL, C.TR, C.ref, C.mono, C.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node = A.image_node, A.art_node

VIEW = "ArkView"
S = dict(view=C.script("ElementalConverterView"), row=C.script("ConverterRecipeRow"),
         button=K.kit_script("KitButton"), kt=K.kit_script("KitText"), entrance=K.kit_script("KitEntrance"),
         ambient=K.kit_script("KitAmbient"), spark=K.kit_script("KitSpark"))
C.STUB_KIND.update({"recipe": (114, S["row"]), "entrance": (114, S["entrance"])})

ORDER = ("Space", "Charge", "Mass", "Time")       # OmniCombineCore.Order: the fly-in order
CRYSTAL = {k: K.meta_guid(f"Assets/_Graphics/Codex/ethirion_{k.lower()}.png") for k in ORDER}
OMNI = K.meta_guid("Assets/_Graphics/Codex/ethirion_omni.png")
# The converter fills its screen exactly. It used to grow 170.4 upward to cancel ArkScreen's authored top inset,
# but ScreenSwitcher.LayoutScreensToViewport lays every screen out at the full safe height at runtime, so the
# grow pushed the title and the recipe panel up under the nav (2026-10-17, the owner: "Ark screen is messed up").
TOP_GROW = 0


def c(node_key, comp):
    return lambda ctx: ctx.c(node_key, comp)


def filled_image(color):
    """An Image, Filled Horizontal from the left, empty."""
    base = K.image(color)
    return lambda ctx, go: base(ctx, go).replace("  m_Type: 0\n", "  m_Type: 3\n").replace(
        "  m_FillMethod: 4\n", "  m_FillMethod: 0\n").replace("  m_FillAmount: 1\n", "  m_FillAmount: 0\n")


def title_band(root):
    band = root.child(N("Title", "Title", TL(40, 108, 514, 116)))
    band.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise"))
    H.framed(band, "Title", 14, "SurfaceBlack", "SurfaceVeryDark")
    label(band.child(N("Title/Eyebrow", "Eyebrow", TL(22, 22, 300, 16))), "Ark", "Label", "NeutralLightest")
    label(band.child(N("Title/Heading", "Heading", TL(22, 38, 480, 44))), "Elemental converter", "H1", "TextLight")
    label(band.child(N("Title/Line", "Line", TL(22, 84, 480, 20))),
          "Space, Charge, Mass and Time combine into one omni crystal.", "BodySmall", "NeutralLightest")


def recipe_row(panel, i, name):
    k = f"Recipe/{name}"
    row = panel.child(N(k, name, TL(26, 58 + i * 104, 386, 92)))
    fill = row.child(N(f"{k}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True))
    bord = row.child(N(f"{k}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    label(row.child(N(f"{k}/Numeral", "Numeral", TL(14, 22, 64, 48))), f"{i + 1:02d}", "H1", "NeutralLightest")
    art = art_node(row, f"{k}/Art", "Art", TL(84, 18, 56, 56), CRYSTAL[name], preserve=True)
    art.add("bob", 114, lambda ctx, go: M.ambient("Float", period=1.2, amplitude=4)(ctx, go).replace(
        "  m_Enabled: 1\n", "  m_Enabled: 0\n", 1))
    label(row.child(N(f"{k}/Name", "Name", TL(158, 18, 150, 30))), name, "H3", "TextLight")
    st = row.child(N(f"{k}/State", "State", TR(14, 24, 110, 16)))
    label(st, "Ready", "Label", "NeutralLightest", align="right")
    image_node(row, f"{k}/Track", "Track", TL(158, 58, 214, 3), "SurfaceDark")
    bar = row.child(N(f"{k}/Bar", "Bar", TL(158, 58, 214, 3)))
    bar.add("cr", 222, K.canvas_renderer()).add("img", 114, filled_image(tok("ControlActive")))
    row.add("row", 114, lambda ctx, go: mono(go, S["row"],
            f"  theme: {K.ref(11400000, K.THEME, 2)}\n  stateText: {ref(ctx.c(f'{k}/State', 'kt'))}\n"
            f"  fill: {ref(ctx.c(f'{k}/Bar', 'img'))}\n  border: {ref(ctx.c(f'{k}/Border', 'img'))}\n"
            f"  art: {ref(ctx.c(f'{k}/Art', 'img'))}\n  bob: {ref(ctx.c(f'{k}/Art', 'bob'))}\n"))


def recipe_panel(root):
    pn = root.child(N("Recipe", "Recipe", TR(40, 108, 438, 760)))
    pn.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=1))
    C.frame(pn, "Recipe/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    M.rim_border(M.find_node(pn, "Recipe/Frame/Border"), "Omni")     # rev 6: the Ark is about every element
    pn.child(M.grid_node("Recipe/Grid"))
    M.bracket_nodes(pn, "Recipe/Bracket")
    C.section_label(pn, "Recipe/Caption", "Caption", "Recipe", TL(26, 26, 200, 18))
    label(pn.child(N("Recipe/Merged", "Merged", TR(26, 26, 220, 18))), "0 of 4 merged", "Label", "NeutralLightest",
          align="right")
    for i, name in enumerate(ORDER):
        recipe_row(pn, i, name)
    image_node(pn, "Recipe/RuleLeft", "RuleLeft", TL(26, 486, 150, 1), "SurfaceDark")
    image_node(pn, "Recipe/RuleRight", "RuleRight", TR(26, 486, 150, 1), "SurfaceDark")
    label(pn.child(N("Recipe/Makes", "Makes", TL(176, 477, 86, 18))), "Makes", "Label", "NeutralLightest",
          align="center")
    om = pn.child(N("Recipe/Omni", "Omni", TL(26, 516, 386, 216)))
    om.add("cg", 225, lambda ctx, go: canvas_group(True)(ctx, go).replace("  m_Alpha: 1\n", "  m_Alpha: 0.35\n"))
    om.add("pop", 114, M.entrance("Pop", play_on_enable=False, duration=M.CEREMONY))
    fill = om.child(N("Recipe/Omni/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True))
    bord = om.child(N("Recipe/Omni/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    art = art_node(om, "Recipe/Omni/Art", "Art", TL(26, 58, 100, 100), OMNI, preserve=True)
    art.add("amb", 114, M.ambient("Float", period=4.2, amplitude=5))
    label(om.child(N("Recipe/Omni/Name", "Name", TL(146, 66, 220, 30))), "Omni crystal", "H3", "TextLight")
    label(om.child(N("Recipe/Omni/Line", "Line", TL(146, 100, 220, 48))), "Forms when all four elements have merged.",
          "BodySmall", "NeutralLightest", valign="top", wrap=True)


SEG_X, SEG_W, SEG_GAP = 248, 150, 6
BAR_W, BAR_H = 1120, 112


def converter_bar(root):
    bar = root.child(N("Bar", "Bar", R((0.5, 0), None, (0, 68), (BAR_W, BAR_H), (0.5, 0))))
    bar.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Rise", stagger=2))
    C.frame(bar, "Bar/Frame", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.92)
    M.bracket_nodes(bar, "Bar/Bracket")
    label(bar.child(N("Bar/Status", "Status", TL(28, 22, 200, 16))), "Ready", "Label", "NeutralLightest")
    label(bar.child(N("Bar/Clock", "Clock", TL(28, 44, 210, 40))), "00:00 / --:--", "H2", "TextLight")
    for i, name in enumerate(ORDER):
        x = SEG_X + i * (SEG_W + SEG_GAP)
        k = f"Bar/Seg{name}"
        image_node(bar, f"{k}Track", f"Seg{name}Track", TL(x, 42, SEG_W, 4), "SurfaceDark")
        seg = bar.child(N(k, f"Seg{name}", TL(x, 42, SEG_W, 4)))
        seg.add("cr", 222, K.canvas_renderer()).add("img", 114, filled_image(tok("ControlActive"))) \
            .add("mask", 114, A.rect_mask())
        sg = seg.child(N(f"{k}/SparkGroup", "SparkGroup", STRETCH))
        sg.add("cg", 225, canvas_group(False))
        sp = sg.child(N(f"{k}/Spark", "Spark", R((0, 0), (0, 1), (-48, 0), (48, 0), (0, 0.5))))
        sp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_spark"))) \
            .add("sp", 114, M.spark_body(False, period=1.8, delay=0.3 * i))
        label(bar.child(N(f"Bar/Label{name}", f"Label{name}", TL(x, 60, SEG_W, 16))), name, "Label", "NeutralLightest")
    w, h = K.button_size("CONVERT")
    w = max(w, 210)
    holder = bar.child(N("Bar/Convert", "Convert", R((1, 0.5), None, (-28, 0), (w, h), (1, 0.5))))
    holder.children.append(P.Inst("ConvertButton", "KitButton", "ConvertButton", STRETCH,
                                  K.button_mods("KitButton", "Play", True, "CONVERT"),
                                  added=[("mask", 114, A.rect_mask())], added_nodes=[C.sweep_node()]))


def build_view():
    root = N("root", VIEW, STRETCH)
    title_band(root)
    recipe_panel(root)
    converter_bar(root)
    return root


def prefab_outputs():
    out = {}
    out[f"{OUT_DIR}/{VIEW}.prefab"] = P.prefab_text(build_view(), VIEW)
    out[f"{OUT_DIR}/{VIEW}.prefab.meta"] = K.PREFAB_META.format(g=P.asset_guid(VIEW))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=P.asset_guid("folder/Ark"))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

IDS = dict(go="7700130000000000001", rt="7700130000000000002", view="7700130000000000003")
VIEW_REFS = {
    "convertButton": ("ConvertButton|root#button", "button"),
    "convertLabel": ("ConvertButton|Label#tmp", "tmp"),
    "statusText": ("Bar/Status#kt", "kt"),
    "clockText": ("Bar/Clock#tmp", "tmp"),
    "mergedText": ("Recipe/Merged#kt", "kt"),
    "omniGroup": ("Recipe/Omni#cg", "cg"),
    "omniPop": ("Recipe/Omni#pop", "entrance"),
}
LISTS = {
    "segments": [(f"Bar/Seg{n}#img", "img") for n in ORDER],
    "segmentSparks": [(f"Bar/Seg{n}/SparkGroup#cg", "cg") for n in ORDER],
    "recipeRows": [(f"Recipe/{n}#row", "recipe") for n in ORDER],
}
OVERLAY_ORDER = ("convertButton", "convertLabel", "statusText", "clockText", "segments", "segmentSparks",
                 "recipeRows", "mergedText", "omniGroup", "omniPop")


class ArkMount(C.Mount):
    def __init__(self):
        self.view = VIEW
        self.ctx = P.Ctx("Menu_Main/Ark")
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = P.asset_guid(VIEW)
        self.kind = None

    def src(self, key):
        V = K.FileIds(VIEW)
        if "|" in key:
            inst, src_key = key.split("|")
            return C.derived(K.source_fid("KitButton", src_key), V(inst + "#inst"))
        return V(key)

    def refs(self):
        return list(VIEW_REFS.values()) + [x for items in LISTS.values() for x in items]

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", VIEW, self.guid, VIEW, STRETCH, mods), parent_rt, out)
        for key, comp in self.refs():
            out.append(self.stub_text(key, comp))
        return [K.unity_empty_scalars(d) for d in out]

    def ids(self):
        return {self.inst, self.stub} | {self.stub_fid(k) for k, _ in self.refs()}


def rebuild_view_doc(doc, mount):
    """ElementalConverterView: config and font kept, CONVERT's resting label, then the overlay fields."""
    head = doc[:doc.index("  config:")]
    config = re.search(r"^  config: .*\n", doc, re.M).group(0)
    font = re.search(r"^  font: .*\n", doc, re.M).group(0)
    body = config + "  buttonLabel: Convert\n" + font
    for k in OVERLAY_ORDER:
        if k in LISTS:
            body += K.refs_list(k, [mount.stub_fid(key) for key, _ in LISTS[k]])
        else:
            body += f"  {k}: {ref(mount.stub_fid(VIEW_REFS[k][0]))}\n"
    return head + body


def scene_transform(text_):
    sc = T.Scene(text_)
    assert sc.field(IDS["go"], "m_Name") == "ElementalConverter", "the converter moved"
    assert sc.script(IDS["view"]) == S["view"], "ElementalConverterView moved"
    mount = ArkMount()
    for fid in list(sc.order):
        if fid in mount.ids():
            sc.remove(fid)
    rt = IDS["rt"]
    anchor = rt
    for d in mount.docs(rt):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    doc = T.set_block(sc.docs[rt], "m_Children", K.refs_list("m_Children", [mount.stub]))
    doc = C.field_set(doc, "m_AnchoredPosition", f"{{x: 0, y: {K.f32(TOP_GROW / 2)}}}")
    doc = C.field_set(doc, "m_SizeDelta", f"{{x: 0, y: {K.f32(TOP_GROW)}}}")
    sc.replace(rt, doc)
    sc.replace(IDS["view"], rebuild_view_doc(sc.docs[IDS["view"]], mount))
    return sc.text(), mount


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def prefab_source(guid, files):
    if guid == P.asset_guid(VIEW):
        return files[f"{OUT_DIR}/{VIEW}.prefab"]
    for name in C.KIT_SOURCES:
        if K.prefab_guid(name) == guid:
            return open(os.path.join(REPO, K.PREFAB_DIR, name + ".prefab"), encoding="utf-8").read()
    return None


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
ROW_FIELDS = dict(stateText=S["kt"], fill=IMG, border=IMG, art=IMG, bob=S["ambient"])


def validate(files):
    guids = T.known_guids() | P.PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    memo = {}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)
        check_sources(path, text_, files, memo)

    view = files[f"{OUT_DIR}/{VIEW}.prefab"]
    V = K.FileIds(VIEW)
    for n in ORDER:
        _, body = A.doc_of(view, V(f"Recipe/{n}#row"))
        assert body and S["row"] in body, f"ArkView: the {n} row carries no ConverterRecipeRow"
        for field, want in ROW_FIELDS.items():
            C.wired(view, body, field, want, f"the {n} row")
        assert f"guid: {CRYSTAL[n]}" in A.doc_of(view, V(f"Recipe/{n}/Art#img"))[1], f"the {n} row shows the wrong crystal"
        _, seg = A.doc_of(view, V(f"Bar/Seg{n}#img"))
        assert "  m_Type: 3\n" in seg and "  m_FillMethod: 0\n" in seg, f"the {n} segment is not a horizontal fill"
    assert view.count("guid: " + S["spark"]) == len(ORDER) + 1, "ArkView needs a spark per segment and the CONVERT sweep"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files, memo)
    mount = ArkMount()
    doc = sc.docs[IDS["view"]]
    for field, (key, kind) in VIEW_REFS.items():
        fid = mount.stub_fid(key)
        assert f"  {field}: {{fileID: {fid}}}" in doc, f"ElementalConverterView.{field} does not point into the overlay"
        cls, scr = C.STUB_KIND[kind]
        assert sc.cls.get(fid) == cls and (scr is None or sc.script(fid) == scr), f"ElementalConverterView.{field} stub is the wrong kind"
    for field, items in LISTS.items():
        assert K.refs_list(field, [mount.stub_fid(k) for k, _ in items]) in doc, f"ElementalConverterView.{field} is not the four elements"
    assert re.search(r"^  config: \{fileID: 11400000, guid: [0-9a-f]{32}", doc, re.M), "the converter lost its config"
    rt = sc.docs[IDS["rt"]]
    assert K.refs_list("m_Children", [mount.stub]) in rt, "the converter holds more than its overlay"
    assert f"m_SizeDelta: {{x: 0, y: {K.f32(TOP_GROW)}}}" in rt, "the converter must fill its screen exactly (ScreenSwitcher already gives the screen the full height)"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def plain_segment(view):
    """The Space segment's Image turned back into a Simple image (the self-test's plant)."""
    fid = str(K.FileIds(VIEW)("Bar/SegSpace#img"))
    m = re.search(r"^--- !u!114 &" + fid + r"\n.*?(?=^--- )", view, re.M | re.S)
    return view[:m.start()] + m.group(0).replace("  m_Type: 3\n", "  m_Type: 0\n") + view[m.end():]


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
    view = f"{OUT_DIR}/{VIEW}.prefab"
    mount = ArkMount()
    cases = [
        ("a recipe row with no bar", lambda f: f.__setitem__(view, re.sub(r"(  fill: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("a segment that is a plain image", lambda f: f.__setitem__(view, plain_segment(f[view]))),
        ("CONVERT unwired", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  convertButton: {{fileID: {mount.stub_fid('ConvertButton|root#button')}}}", "  convertButton: {fileID: 0}"))),
        ("the converter grown up under the nav again", lambda f: f.__setitem__(SCENE, re.sub(
            r"(--- !u!224 &" + IDS["rt"] + r"\n(?:(?!--- ).*\n)*?  m_SizeDelta: )\{x: 0, y: 0\}", r"\1{x: 0, y: 170.4}",
            f[SCENE], count=1))),
        ("the wrong crystal on a row", lambda f: f.__setitem__(view, f[view].replace(CRYSTAL["Mass"], CRYSTAL["Time"], 1))),
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
    print("OK: ArkView and Menu_Main validate")
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
