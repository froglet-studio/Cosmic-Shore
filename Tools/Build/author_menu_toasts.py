#!/usr/bin/env python3
"""
Author ITEM 11 of the style-B menu redesign - the MENU TOASTS (Docs/MENU_REDESIGN_B.md section 20; the
approved Toasts board on the "Menu Redesign B" canvas).

    Assets/Resources/UI/ToastNotificationItem.prefab
        The toast ToastNotificationManager loads when no prefab is wired (ItemResourcePath) - which is
        every manager ToastNotificationAPI creates, so until now every menu toast was the runtime
        fallback: a 100 x 100 square that cut most messages off. The new one hugs its text (360 - 720
        wide, measured by ToastNotificationItem), Neutral Very Dark with a Neutral Light hairline, Body 18,
        an optional leading glyph per ToastKind (lock, check, danger diamond, crystal) and a Jade drain bar
        along the bottom that empties over the 5 s hold. Everything visible sits in Body, which drops in
        from above; the root is the container's layout element.

    Assets/_Scenes/Menu_Main.unity, `Safe Area/ToastNotificationContainer`:
        Moves from the lower-left quadrant (under the party panels) to the top centre below the nav:
        stretched across the top, 108 down, upper-centre alignment, 10 apart, sizing its children from
        their own layout (child control on, force expand off). Nothing else in the scene is touched.

    Assets/Resources/ToastNotificationSettings.asset:
        newestOnTop (the board stacks newest on top) and dropDistance are written explicitly. Timing, the
        queue of 10, three visible and drag-to-dismiss are unchanged.

STANDING DOWN: every output is re-derived each run; a re-run is a no-op (--check).

    python3 Tools/Build/author_menu_toasts.py              # write
    python3 Tools/Build/author_menu_toasts.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_toasts.py --self-test  # planted defects must all be caught
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
SETTINGS = "Assets/Resources/ToastNotificationSettings.asset"
OUT_DIR = "Assets/Resources/UI"
NAME = "ToastNotificationItem"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
ref, mono = C.ref, C.mono
text, le = H.text, H.le

S = dict(item=C.script("ToastNotificationItem"))
PAD = (22, 22, 14, 16)          # left, right, top, bottom - ToastNotificationItem.padding mirrors it
GLYPH, GAP = 18, 14
KIND_SPRITES = (("refusalIcon", "chrome_lock"), ("successIcon", "chrome_confirm"),
                ("errorIcon", "chrome_diamond"), ("goalIcon", "chrome_crystal"))

# Menu_Main, measured 2026-10-15.
CONTAINER = dict(go="4632719343361558278", rt="5804654115376219870", vlg="4206201074433312421")


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path():
    return f"{OUT_DIR}/{NAME}.prefab"


def F(key):
    return K.FileIds(NAME)(key)


def item_group():
    """Alpha 0 until Show fades it in; raycastable so drag-to-dismiss still works on the toast itself."""
    return lambda ctx, go: ("CanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
                            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
                            f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Alpha: 0\n"
                            "  m_Interactable: 1\n  m_BlocksRaycasts: 1\n  m_IgnoreParentGroups: 0\n")


def build_item():
    root = N("root", NAME, R(size=(360, 50)))
    root.add("cg", 225, item_group()).add("le", 114, le(pw=360, ph=50))
    body = root.child(N("Body", "Body", STRETCH))
    body.add("hlg", 114, K.hv_layout(False, PAD, GAP, 3, (1, 1), (0, 0)))
    fill = body.child(N("Body/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceVeryDark")).add("le", 114, le(ignore=True))
    bord = body.child(N("Body/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver10_line1"), True)) \
        .add("th", 114, H.themed("SurfaceLight")).add("le", 114, le(ignore=True))
    g = body.child(N("Body/Glyph", "Glyph", R(size=(GLYPH, GLYPH))))
    g.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight"), K.chrome_sprite("chrome_lock"), preserve=True)) \
        .add("th", 114, H.themed("TextLight")).add("cg", 225, H.canvas_group(False)) \
        .add("le", 114, le(ignore=True, pw=GLYPH, ph=GLYPH))
    msg = body.child(N("Body/Message", "Message", R(size=(300, 24))))
    msg.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Not open yet.", "Body", tok("TextLight"), wrap=True)) \
        .add("kt", 114, K.kit_text("Body", "TextLight")).add("le", 114, le(pw=300))
    drain = body.child(N("Body/Drain", "Drain", R((0, 0), (1, 0), (0, 0), (0, 2), (0, 0))))
    drain.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))) \
        .add("th", 114, H.themed("ControlActive")).add("le", 114, le(ignore=True))
    root.add("item", 114, lambda ctx, go: mono(go, S["item"], item_body(ctx)))
    return root


def item_body(ctx):
    l, r, t, b = PAD
    return (f"  messageText: {ref(ctx.c('Body/Message', 'tmp'))}\n"
            f"  sizing: {ref(ctx.c('root', 'le'))}\n  messageSizing: {ref(ctx.c('Body/Message', 'le'))}\n"
            "  minWidth: 360\n  maxWidth: 720\n"
            f"  padding: {{x: {l}, y: {r}, z: {t}, w: {b}}}\n  glyphAdvance: {GLYPH + GAP}\n"
            f"  body: {ref(ctx.rt('Body'))}\n  drainBar: {ref(ctx.rt('Body/Drain'))}\n"
            f"  kindIcon: {ref(ctx.c('Body/Glyph', 'img'))}\n  kindPaint: {ref(ctx.c('Body/Glyph', 'th'))}\n"
            f"  kindGroup: {ref(ctx.c('Body/Glyph', 'cg'))}\n  kindSizing: {ref(ctx.c('Body/Glyph', 'le'))}\n"
            + "".join(f"  {field}: {K.sprite_ref(K.chrome_sprite(name))}\n" for field, name in KIND_SPRITES))


def prefab_outputs():
    return {prefab_path(): P.prefab_text(build_item(), NAME),
            prefab_path() + ".meta": K.PREFAB_META.format(g=asset_guid(NAME)),
            OUT_DIR + ".meta": K.FOLDER_META.format(g=asset_guid("folder/Resources/UI"))}


# --------------------------------------------------------------------------------------------
# Menu_Main: the container
# --------------------------------------------------------------------------------------------

def scene_transform(text_):
    sc = T.Scene(text_)
    assert sc.field(CONTAINER["go"], "m_Name") == "ToastNotificationContainer", "the toast container moved"
    assert sc.rect_of(CONTAINER["go"]) == CONTAINER["rt"], "the toast container's RectTransform moved"
    assert sc.script(CONTAINER["vlg"]) == K.VLAYOUT, "the toast container's VerticalLayoutGroup moved"
    rt = sc.docs[CONTAINER["rt"]]
    for f, v in (("m_AnchorMin", "{x: 0, y: 1}"), ("m_AnchorMax", "{x: 1, y: 1}"), ("m_AnchoredPosition", "{x: 0, y: -108}"),
                 ("m_SizeDelta", "{x: 0, y: 360}"), ("m_Pivot", "{x: 0.5, y: 1}")):
        rt = C.field_set(rt, f, v)
    sc.replace(CONTAINER["rt"], rt)
    vlg = sc.docs[CONTAINER["vlg"]]
    for f, v in (("m_ChildAlignment", "1"), ("m_Spacing", "10"), ("m_ChildForceExpandWidth", "0"),
                 ("m_ChildForceExpandHeight", "0"), ("m_ChildControlWidth", "1"), ("m_ChildControlHeight", "1")):
        vlg = C.field_set(vlg, f, v)
    sc.replace(CONTAINER["vlg"], vlg)
    return sc.text()


def settings_transform(text_):
    out = text_
    for field, value, after in (("newestOnTop", "1", "stackSpacing"), ("dropDistance", "16", "newestOnTop")):
        if re.search(r"^  %s:" % field, out, re.M):
            out = re.sub(r"^  %s:.*$" % field, f"  {field}: {value}", out, flags=re.M)
        else:
            out = re.sub(r"^(  %s:.*\n)" % after, lambda m: m.group(1) + f"  {field}: {value}\n", out, count=1, flags=re.M)
    return out


def outputs():
    files = prefab_outputs()
    files[SCENE] = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    files[SETTINGS] = settings_transform(open(os.path.join(REPO, SETTINGS), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def validate(files):
    guids = T.known_guids() | P.PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    pf = files[prefab_path()]
    A.check_file(prefab_path(), pf, guids, minted)
    _, item = A.doc_of(pf, F("root#item"))
    assert item and S["item"] in item, "the toast root carries no ToastNotificationItem"
    C.wired(pf, item, "messageText", K.TMP_UGUI, "ToastNotificationItem")
    C.wired(pf, item, "sizing", K.LAYOUT_ELEMENT, "ToastNotificationItem")
    C.wired(pf, item, "kindIcon", K.IMAGE, "ToastNotificationItem")
    C.wired(pf, item, "body", 224, "ToastNotificationItem")
    C.wired(pf, item, "drainBar", 224, "ToastNotificationItem")
    for field, _ in KIND_SPRITES:
        assert re.search(r"^  %s: \{fileID: 21300000, guid: \w{32}" % field, item, re.M), f"ToastNotificationItem.{field} has no sprite"
    l, r, t, b = PAD
    assert f"  padding: {{x: {l}, y: {r}, z: {t}, w: {b}}}\n" in item, "the measured padding must match the row's"
    _, hlg = A.doc_of(pf, F("Body#hlg"))
    assert f"    m_Left: {l}\n    m_Right: {r}\n    m_Top: {t}\n    m_Bottom: {b}\n" in hlg, "the row's padding drifted from the measured one"
    assert pf.count("  m_RaycastTarget: 1\n") == 1, "exactly one raycast target (the fill, for drag-to-dismiss)"
    assert re.search(r"^ToastNotificationItem|ItemResourcePath = \"UI/ToastNotificationItem\"",
                     open(os.path.join(REPO, "Assets/_Scripts/UI/ToastNotification/ToastNotificationManager.cs")).read(), re.M), \
        "ToastNotificationManager no longer loads UI/ToastNotificationItem"

    sc = T.Scene(files[SCENE])
    vlg = sc.docs[CONTAINER["vlg"]]
    assert "  m_ChildControlWidth: 1\n" in vlg and "  m_ChildControlHeight: 1\n" in vlg, \
        "the container must size toasts from their layout (the 100 x 100 square)"
    assert "  m_ChildAlignment: 1\n" in vlg, "toasts stack at the top centre"
    assert "  m_AnchorMin: {x: 0, y: 1}\n" in sc.docs[CONTAINER["rt"]], "the container is not across the top"
    assert "  newestOnTop: 1\n" in files[SETTINGS], "the settings must stack newest on top"


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
    pf = prefab_path()
    vlg_head = f"--- !u!114 &{CONTAINER['vlg']}\n"

    def container_uncontrolled(f):
        i = f[SCENE].index(vlg_head)
        j = f[SCENE].index("--- ", i + 4)
        f[SCENE] = f[SCENE][:i] + f[SCENE][i:j].replace("  m_ChildControlWidth: 1\n", "  m_ChildControlWidth: 0\n") + f[SCENE][j:]
    cases = [
        ("the container back to fixed squares", container_uncontrolled),
        ("a toast with no sizing", lambda f: f.__setitem__(pf, re.sub(r"(  sizing: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[pf]))),
        ("a kind with no glyph", lambda f: f.__setitem__(pf, re.sub(r"(  errorIcon: )\{[^}]*\}", r"\1{fileID: 0}", f[pf]))),
        ("the row padding drifted", lambda f: f.__setitem__(pf, f[pf].replace("    m_Left: 22\n", "    m_Left: 30\n", 1))),
        ("oldest on top", lambda f: f.__setitem__(SETTINGS, f[SETTINGS].replace("  newestOnTop: 1\n", "  newestOnTop: 0\n"))),
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
    print("OK: the toast prefab, the Menu_Main container and the settings validate")
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
