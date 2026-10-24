#!/usr/bin/env python3
"""
Author ITEM 9b of the style-B menu redesign - the CREDITS window - as an authored prefab placed in
Menu_Main (Docs/MENU_REDESIGN_B.md section 18.2; the approved Credits board, revision 2, on the
"Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Panels/CreditsModal.prefab
        The whole modal: CanvasGroup + Animator (Standard.controller, which scales the child named
        Content) + CreditsModal (ModalType CREDITS = 18) on the root; the dim; the window frame and
        brackets; "Credits" (the manifest's title is written into it); the right-stick hint and Close
        (B); the scroll view - viewport, a Content column that opens on the title band ("Froglet Inc.
        presents / Cosmic Shore") and takes the generated sections after it, and a thin ControlActive
        scrollbar; and the hidden STYLES the generated rows copy (section heading H2, blurb Body, entry
        name H3, role and notice BodySmall, the rule above each entry). The rows themselves are still
        built from Resources/CreditsManifest at runtime - the manifest stays the one copy of the text.

    Assets/_Scenes/Menu_Main.unity
        One CreditsModal instance, last child of ModalWindows (drawn over Settings, which opens it),
        stretched, wired to the scene's ScreenSwitcher and appended to ScreenSwitcher.Modals - so
        EnsureCreditsModal finds an authored CREDITS window and its code-built fallback stands down.
        Nothing else in the scene is touched.

STANDING DOWN: every output is re-derived on each run; the scene edit removes its own documents and
list entries before writing them again, so a re-run is a no-op (--check).

    python3 Tools/Build/author_menu_credits.py              # write
    python3 Tools/Build/author_menu_credits.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_credits.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_profile_window", os.path.join(HERE, "author_menu_profile_window.py"))
W = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(W)

P, C, A, K, T, H, M = W.P, W.C, W.A, W.K, W.T, W.H, W.M
REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
OUT_DIR = "Assets/_Prefabs/UI Elements/Panels"
NAME = "CreditsModal"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono = C.TL, C.TR, C.ref, C.mono
text, label, le = H.text, H.label, H.le
image_node, ring_node = A.image_node, A.ring_node

STANDARD_CONTROLLER = "362e7fec2b6aecb4a92d91defb53b98d"   # Standard.controller (scales "Content")
SCROLLBAR = "2a4db7a114972834c8e4117be1d82ba3"            # UnityEngine.UI.Scrollbar
S = dict(credits=C.script("CreditsModal"), switcher=C.script("ScreenSwitcher"), close=C.script("ModalWindowCloseButton"),
         button=K.kit_script("KitButton"), entrance=K.kit_script("KitEntrance"))
PACKAGE = P.PACKAGE | {SCROLLBAR}
CREDITS_TYPE = 18                                         # ScreenSwitcher.ModalWindows.CREDITS

# Menu_Main, measured 2026-10-15.
SCENE_IDS = dict(modal_windows_go="3351705847530627495", modal_windows_rt="6818916199485298600",
                 switcher="4216411968520269688")

WIN_W, WIN_H, PAD = 1200, 900, 48
SCROLL_TOP, SCROLL_BOTTOM = 112, 40


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path():
    return f"{OUT_DIR}/{NAME}.prefab"


def F(key):
    return K.FileIds(NAME)(key)


# --------------------------------------------------------------------------------------------
# The prefab
# --------------------------------------------------------------------------------------------

def animator():
    return lambda ctx, go: (
        "Animator:\n  serializedVersion: 7\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Avatar: {{fileID: 0}}\n"
        f"  m_Controller: {{fileID: 9100000, guid: {STANDARD_CONTROLLER}, type: 2}}\n"
        "  m_CullingMode: 0\n  m_UpdateMode: 2\n  m_ApplyRootMotion: 0\n  m_LinearVelocityBlending: 0\n"
        "  m_StabilizeFeet: 0\n  m_AnimatePhysics: 0\n  m_WarningMessage: \n  m_HasTransformHierarchy: 1\n"
        "  m_AllowConstantClipSamplingOptimization: 1\n  m_KeepAnimatorStateOnDisable: 0\n"
        "  m_WriteDefaultValuesOnDisable: 0\n")


def hidden_group():
    return lambda ctx, go: ("CanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Alpha: 0\n"
                            "  m_Interactable: 0\n  m_BlocksRaycasts: 0\n  m_IgnoreParentGroups: 0\n")


def scrollbar(win, key):
    """A thin vertical bar: SurfaceDark track, ControlActive handle. Not a navigation stop - the pad
    scrolls with the right stick, and a focusable bar would only steal focus from Close."""
    sb = win.child(N(key, "Scrollbar", R((1, 0), (1, 1), (-PAD + 14, (SCROLL_BOTTOM - SCROLL_TOP) / 2),
                                         (6, -(SCROLL_TOP + SCROLL_BOTTOM)), (1, 0.5))))
    sb.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), raycast=True)) \
        .add("th", 114, H.themed("SurfaceDark"))
    area = sb.child(N(f"{key}/Area", "Sliding Area", STRETCH))
    hd = area.child(N(f"{key}/Area/Handle", "Handle", STRETCH))
    hd.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"), raycast=True)) \
        .add("th", 114, H.themed("ControlActive"))
    sb.add("bar", 114, lambda ctx, go: mono(go, SCROLLBAR,
           K.SELECTABLE_FIELDS.replace("    m_Mode: 3\n", "    m_Mode: 0\n")
           + f"  m_TargetGraphic: {ref(ctx.c(f'{key}/Area/Handle', 'img'))}\n"
           f"  m_HandleRect: {ref(ctx.rt(f'{key}/Area/Handle'))}\n"
           "  m_Direction: 2\n  m_Value: 1\n  m_Size: 0.2\n  m_NumberOfSteps: 0\n"
           "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    return key


STYLES = (  # key, role, token, sample
    ("Heading", "H2", "TextLight", "Section"),
    ("Body", "Body", "TextLight", "Blurb"),
    ("EntryName", "H3", "TextLight", "Name"),
    ("EntryRole", "BodySmall", "NeutralLightest", "Role"),
    ("Notice", "BodySmall", "InactiveLight", "Notice"),
)


def build_modal():
    root = N("root", NAME, STRETCH)
    root.add("cg", 225, hidden_group()).add("anim", 95, animator())
    image_node(root, "Dim", "Dim", STRETCH, "SurfaceBlack", alpha=0.88, raycast=True)
    content = root.child(N("Content", "Content", STRETCH))

    win = content.child(N("Window", "Window", R((0.5, 0.5), None, (0, -42), (WIN_W, WIN_H))))
    fill = win.child(N("Window/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack"))
    bord = win.child(N("Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True)).add("th", 114, H.themed("TextLight"))
    M.bracket_nodes(win, "Window/Bracket", inset=14)

    # The list first in the hierarchy; it holds nothing selectable, so focus lands on Close.
    sv = win.child(N("Window/Scroll", "Scroll View", R((0, 0), (1, 1), (-4, (SCROLL_BOTTOM - SCROLL_TOP) / 2),
                                                       (-2 * PAD - 8, -(SCROLL_TOP + SCROLL_BOTTOM)))))
    sv.add("in", 114, M.entrance("Rise", distance=14, delay=0.12, play_on_enable=False))
    sv.add("cg", 225, H.canvas_group(True))
    sv.add("scroll", 114, lambda ctx, go: mono(go, A.SCROLL_RECT,
           f"  m_Content: {ref(ctx.rt('Window/Scroll/Viewport/Content'))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
           "  m_MovementType: 1\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
           f"  m_ScrollSensitivity: 30\n  m_Viewport: {ref(ctx.rt('Window/Scroll/Viewport'))}\n"
           "  m_HorizontalScrollbar: {fileID: 0}\n"
           f"  m_VerticalScrollbar: {ref(ctx.c('Window/Scrollbar', 'bar'))}\n"
           "  m_HorizontalScrollbarVisibility: 0\n  m_VerticalScrollbarVisibility: 1\n"
           "  m_HorizontalScrollbarSpacing: 0\n  m_VerticalScrollbarSpacing: 0\n"
           "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    vp = sv.child(N("Window/Scroll/Viewport", "Viewport", R((0, 0), (1, 1), (0, 0), (-24, 0), (0, 1))))
    vp.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("mask", 114, A.rect_mask())
    col = vp.child(N("Window/Scroll/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    col.add("vlg", 114, K.hv_layout(True, (0, 0, 8, 32), 30, 0, (1, 1), (1, 0))) \
        .add("fit", 114, K.fitter(0, 2))
    band = col.child(N("Window/Scroll/Viewport/Content/Band", "TitleBand", R(size=(0, 132))))
    band.add("le", 114, le(ph=132)).add("cg", 225, H.canvas_group(True)) \
        .add("in", 114, M.entrance("Drop", distance=16, play_on_enable=False))
    label(band.child(N("Window/Scroll/Viewport/Content/Band/Presents", "Presents", TL(0, 18, 800, 18))),
          "Froglet Inc. presents", "Label", "NeutralLightest")
    label(band.child(N("Window/Scroll/Viewport/Content/Band/Title", "Title", TL(0, 42, 800, 60))),
          "Cosmic Shore", "Display", "TextLight")
    image_node(band, "Window/Scroll/Viewport/Content/Band/Rule", "Rule",
               R((0, 0), None, (0, 6), (120, 2), (0, 0)), "ControlActive")
    scrollbar(win, "Window/Scrollbar")

    t = win.child(N("Window/Title", "Title", TL(PAD, 34, 600, 46)))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Credits", "H1", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    W.close_button(win)
    # The right-stick hint, left of Close: "[RS] Scroll".
    close_w = 16 + (K.label_width("B", "Label") + 24) + 10 + K.label_width("CLOSE", "ButtonSmall") + 16
    scroll_w = K.label_width("Scroll", "BodySmall")
    rs_w = K.label_width("RS", "Label") + 24
    hint = win.child(N("Window/Hint", "ScrollHint", TR(44 + close_w + 20, 40, rs_w + 8 + scroll_w, 40)))
    hint.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 3, (0, 0), (0, 0)))
    hint.children.append(P.Inst("HintKeycap", "KitKeycap", "Keycap", R(size=(rs_w, K.line_height("Label") + 8)),
                                [("Label#tmp", "m_text", "RS", None)]))
    hl = hint.child(N("Window/Hint/Label", "Label", R(size=(scroll_w, K.line_height("BodySmall")))))
    label(hl, "Scroll", "BodySmall", "NeutralLightest")

    # The styles the generated rows copy. Hidden through their CanvasGroup; never shown.
    styles = win.child(N("Window/Styles", "Styles", TL(PAD, SCROLL_TOP, 600, 200)))
    styles.add("cg", 225, H.canvas_group(False))
    for i, (key, role, token, sample) in enumerate(STYLES):
        n = styles.child(N(f"Window/Styles/{key}", key, TL(0, i * 34, 600, 30)))
        label(n, sample, role, token)
    image_node(styles, "Window/Styles/Rule", "Rule", TL(0, len(STYLES) * 34, 600, 1), "SurfaceDark")

    root.add("credits", 114, lambda ctx, go: mono(go, S["credits"], credits_body(ctx)))
    return root


def credits_body(ctx):
    st = lambda key: ref(ctx.c(f"Window/Styles/{key}", "tmp"))
    return ("  sharpAnimations: 1\n  closeOnGamepadB: 1\n  blockUIBehind: 1\n"
            f"  ModalType: {CREDITS_TYPE}\n  windowAnimator: {ref(ctx.c('root', 'anim'))}\n  screenSwitcher: {{fileID: 0}}\n"
            f"  content: {ref(ctx.rt('Window/Scroll/Viewport/Content'))}\n"
            f"  headingTemplate: {st('Heading')}\n  bodyTemplate: {st('Body')}\n"
            "  rowSpacing: 30\n  contentPadding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 8\n    m_Bottom: 32\n"
            f"  titleText: {ref(ctx.c('Window/Title', 'tmp'))}\n"
            f"  entryNameTemplate: {st('EntryName')}\n  entryRoleTemplate: {st('EntryRole')}\n"
            f"  noticeTemplate: {st('Notice')}\n  dividerTemplate: {ref(ctx.c('Window/Styles/Rule', 'img'))}\n"
            "  sectionRowSpacing: 14\n  entryLineSpacing: 4\n  entryPadding: 12\n"
            "  stickScrollSpeed: 1400\n  stickDeadZone: 0.2\n"
            + C.refs("openEntrances", [ctx.c("Window/Scroll/Viewport/Content/Band", "in"), ctx.c("Window/Scroll", "in")]))


def prefab_outputs():
    return {prefab_path(): P.prefab_text(build_modal(), NAME),
            prefab_path() + ".meta": K.PREFAB_META.format(g=asset_guid(NAME))}


# --------------------------------------------------------------------------------------------
# Menu_Main: the instance, wired and registered
# --------------------------------------------------------------------------------------------

def scene_ctx():
    return P.Ctx("Menu_Main/CreditsModal")


def scene_ids():
    ctx = scene_ctx()
    inst, stub = str(ctx.fid("Credits#inst")), str(ctx.fid("Credits#stub"))
    comp = str(C.derived(F("root#credits"), inst))
    return inst, stub, comp


def scene_transform(text_):
    sc = T.Scene(text_)
    I = SCENE_IDS
    assert sc.field(I["modal_windows_go"], "m_Name") == "ModalWindows", "Menu_Main's ModalWindows moved"
    assert sc.rect_of(I["modal_windows_go"]) == I["modal_windows_rt"], "ModalWindows' RectTransform moved"
    assert sc.script(I["switcher"]) == S["switcher"], "Menu_Main's ScreenSwitcher moved"
    inst, stub, comp = scene_ids()
    for fid in (inst, stub, comp):
        if sc.has(fid):
            sc.remove(fid)
    out = []
    mods = [("root#credits", "screenSwitcher", None, ref(I["switcher"]))]
    K.emit_instance(scene_ctx(), K.Instance("Credits", NAME, asset_guid(NAME), NAME, STRETCH, mods),
                    I["modal_windows_rt"], out)
    out.append(T.stub_mono(comp, F("root#credits"), asset_guid(NAME), inst, S["credits"]))
    mw = sc.docs[I["modal_windows_rt"]]
    kids = [k for k in re.findall(r"^  - \{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", mw, re.M).group(1), re.M)
            if k != stub]
    anchor = kids[-1] if kids else I["modal_windows_rt"]
    # Insert after the last existing child's whole instance so file order follows hierarchy order.
    anchor = last_doc_of(sc, anchor)
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(I["modal_windows_rt"], T.set_block(mw, "m_Children", K.refs_list("m_Children", kids + [stub])))
    sw = sc.docs[I["switcher"]]
    modals = [m for m in re.findall(r"^  - \{fileID: (-?\d+)\}", re.search(r"^  Modals:\n((?:  - .*\n)*)", sw, re.M).group(1), re.M)
              if m != comp]
    sc.replace(I["switcher"], T.set_block(sw, "Modals", K.refs_list("Modals", modals + [comp])))
    return sc.text()


def last_doc_of(sc, rt):
    """The last document that belongs to the object at `rt` (for an instance stub: its PrefabInstance's
    last document), so new documents land after it."""
    head = sc.docs[rt].split("\n", 1)[0]
    if "stripped" in head:
        pid = re.search(r"m_PrefabInstance: \{fileID: (-?\d+)\}", sc.docs[rt]).group(1)
        owned = C.instance_docs(sc, pid)
    else:
        owned = C.subtree(sc, sc.go_of(rt))
    owned = set(owned)
    return [f for f in sc.order if f in owned][-1]


def outputs():
    files = prefab_outputs()
    files[SCENE] = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def validate(files):
    guids = T.known_guids() | PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    pf = files[prefab_path()]
    A.check_file(prefab_path(), pf, guids, minted)
    _, comp = A.doc_of(pf, F("root#credits"))
    assert comp and S["credits"] in comp, "CreditsModal.prefab: the root carries no CreditsModal"
    assert f"  ModalType: {CREDITS_TYPE}\n" in comp, "CreditsModal must register as ModalWindows.CREDITS (18)"
    assert f"  content: {{fileID: {F('Window/Scroll/Viewport/Content#rt')}}}\n" in comp, "CreditsModal.content is not the scroll Content"
    for field in ("headingTemplate", "bodyTemplate", "entryNameTemplate", "entryRoleTemplate", "noticeTemplate", "titleText"):
        C.wired(pf, comp, field, K.TMP_UGUI, "CreditsModal")
    C.wired(pf, comp, "dividerTemplate", K.IMAGE, "CreditsModal")
    assert comp.count("  - {fileID: ") == 2, "CreditsModal.openEntrances must list the band and the list"
    _, scroll = A.doc_of(pf, F("Window/Scroll#scroll"))
    assert scroll and A.SCROLL_RECT in scroll, "the Scroll View carries no ScrollRect"
    assert f"  m_Content: {{fileID: {F('Window/Scroll/Viewport/Content#rt')}}}\n" in scroll, "ScrollRect.m_Content is not the list"
    C.wired(pf, scroll, "m_VerticalScrollbar", SCROLLBAR, "ScrollRect")
    C.wired(pf, scroll, "m_Viewport", 224, "ScrollRect")
    _, styles_cg = A.doc_of(pf, F("Window/Styles#cg"))
    assert "  m_Alpha: 0\n" in styles_cg, "the row styles must be hidden"
    _, anim = A.doc_of(pf, F("root#anim"))
    assert STANDARD_CONTROLLER in anim, "the root's Animator must run Standard.controller"
    assert re.search(r"^  m_Name: Content$", pf, re.M), "Standard.controller animates a child named Content"
    assert pf.count("guid: " + S["close"]) == 1, "Close must close through ModalWindowCloseButton"

    sc_text = files[SCENE]
    inst, stub, comp_fid = scene_ids()
    assert sc_text.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid(NAME)}") == 1, \
        "Menu_Main must hold exactly one CreditsModal instance"
    sc = T.Scene(sc_text)
    I = SCENE_IDS
    kids = re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[I["modal_windows_rt"]], re.M).group(1)
    assert kids.rstrip("\n").split("\n")[-1] == f"  - {{fileID: {stub}}}", "CreditsModal must be ModalWindows' last child"
    assert f"m_TransformParent: {{fileID: {I['modal_windows_rt']}}}" in sc.docs[inst], "CreditsModal is not under ModalWindows"
    modals = re.search(r"^  Modals:\n((?:  - .*\n)*)", sc.docs[I["switcher"]], re.M).group(1)
    assert modals.count(f"{{fileID: {comp_fid}}}") == 1, "ScreenSwitcher.Modals does not list the CreditsModal"
    assert sc.has(comp_fid) and asset_guid(NAME) in sc.docs[comp_fid], "the CreditsModal stub is missing"
    assert re.search(r"propertyPath: screenSwitcher\n      value: \n      objectReference: \{fileID: %s\}" % I["switcher"],
                     sc.docs[inst]), "the CreditsModal instance is not wired to the ScreenSwitcher"
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", sc_text, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", sc_text):
        assert m.group(1) in anchors, f"Menu_Main: dangling local fileID {m.group(1)}"


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
    pf, (_, _, comp) = prefab_path(), scene_ids()
    cases = [
        ("the window left out of ScreenSwitcher.Modals", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  - {{fileID: {comp}}}\n", "", 1))),
        ("CreditsModal.content unwired", lambda f: f.__setitem__(pf, re.sub(
            r"(  content: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[pf], count=1))),
        ("the row styles left visible", lambda f: f.__setitem__(pf, f[pf].replace(
            f"--- !u!225 &{F('Window/Styles#cg')}\nCanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {F('Window/Styles#go')}}}\n"
            "  m_Enabled: 1\n  m_Alpha: 0\n",
            f"--- !u!225 &{F('Window/Styles#cg')}\nCanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {F('Window/Styles#go')}}}\n"
            "  m_Enabled: 1\n  m_Alpha: 1\n"))),
        ("a scroll view with no scrollbar", lambda f: f.__setitem__(pf, re.sub(
            r"(  m_VerticalScrollbar: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[pf], count=1))),
        ("the wrong modal type", lambda f: f.__setitem__(pf, f[pf].replace(f"  ModalType: {CREDITS_TYPE}\n", "  ModalType: 5\n"))),
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
    print("OK: CreditsModal.prefab and its Menu_Main instance validate")
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
