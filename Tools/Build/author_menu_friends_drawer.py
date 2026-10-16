#!/usr/bin/env python3
"""
Author the FRIENDS DRAWER into Menu_Main (rev 3 of the style-B menu, 2026-10-16: "move the friends tab
out of all the tabs like arcade, arena - make it independent, like a popup from the left side we can
open, minimise, maximise whenever we want").

    Safe Area/FriendsDrawerHost     after ModalWindows, so it draws over every window; CanvasGroup (the
    |                               freestyle fade list holds it), OfflineUIGate, FriendsDrawer
    '- Drawer                       the gated object: offline it is deactivated, as the panel was
       |- Handle                    44 x 220 tab on the left edge: people glyph, FRIENDS written up the
       |                            tab; pressing it opens the drawer
       '- Window                    glass panel that slides in from the left (460 x 860, maximised
          |                         720 x 900): title, a minimise (slide back) and a maximise / restore
          '- PartyPanel             THE shared party panel (item 3's prefab, instanced - never copied),
                                    the same one the Arcade / Arena / Toy Box / Mission windows used to
                                    nest; those generators no longer instance it.

Also: MenuCrystalClickHandler.menuCanvasGroups gains the host's group, so the drawer fades with the
rest of the menu when the player flies.

STANDING DOWN: every document is minted with a deterministic fileID and re-emitted in place; a re-run is
a no-op (--check).

    python3 Tools/Build/author_menu_friends_drawer.py              # write
    python3 Tools/Build/author_menu_friends_drawer.py --check      # fail if the scene drifted
    python3 Tools/Build/author_menu_friends_drawer.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, f"{name}.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


HM = _load("author_menu_home")
AR = _load("author_menu_arcade")
P, C, A, K, T, H, M = HM.P, HM.C, HM.A, HM.K, HM.T, HM.H, HM.M
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, label = C.TL, C.TR, C.ref, C.mono, H.label

REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
S = dict(drawer=C.script("FriendsDrawer"), gate=C.script("OfflineUIGate"), crystal=C.script("MenuCrystalClickHandler"))
PARTY_GUID = AR.asset_guid("PartyPanel")

# Menu_Main, measured 2026-10-16 (names asserted).
SAFE_AREA_RT = "1034510601983959698"
MODAL_WINDOWS_RT = "6818916199485298600"

NORMAL = (460, 860)
MAXIMISED = (720, 900)
HANDLE = (76, 112)
INSET = 16
BTN = 28


def ctx_new():
    return P.Ctx("Menu_Main/FriendsDrawer")


class Patch(HM.Patch):
    def __init__(self, text_):
        super().__init__(text_)
        self.ctx = ctx_new()


def handle_node(parent):
    h = parent.child(N("Drawer/Handle", "Handle", R((0, 0.5), None, (0, 0), HANDLE, (0, 0.5))))
    h.add("cg", 225, H.canvas_group(True))
    A.ring_node(h, "Drawer/Handle/FocusRing", 10)
    f = h.child(N("Drawer/Handle/Fill", "Fill", STRETCH))
    f.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.62), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bd = h.child(N("Drawer/Handle/Border", "Border", STRETCH))
    bd.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver10_line1"), True))
    g = h.child(N("Drawer/Handle/Glyph", "Glyph", R((0.5, 1), None, (0, -18), (30, 30), (0.5, 1))))
    g.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"), K.chrome_sprite("chrome_people"), preserve=True))
    lab = h.child(N("Drawer/Handle/Label", "Label", R((0, 0), (1, 0), (0, 16), (-8, 20), (0.5, 0))))
    label(lab, "Friends", "Label", "TextLight", align="center")
    h.add("button", 114, lambda ctx, go: mono(go, HM.S["kitbutton"],
          K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c('Drawer/Handle/Fill', 'img'))}\n" + A.calls([])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c('Drawer/Handle/FocusRing', 'img'))}\n"
          "  variant: 0\n  fill: 0\n"
          f"  fillGraphic: {ref(ctx.c('Drawer/Handle/Fill', 'img'))}\n"
          f"  borderGraphic: {ref(ctx.c('Drawer/Handle/Border', 'img'))}\n"
          f"  contentGraphics:\n  - {ref(ctx.c('Drawer/Handle/Glyph', 'img'))}\n  playGradient: {{fileID: 0}}\n"))
    h.add("audio", 114, A.menu_audio(11))
    return h


def window_node(parent):
    w = parent.child(N("Drawer/Window", "Window", R((0, 0.5), None, (-NORMAL[0] - 8, 0), NORMAL, (0, 0.5))))
    w.add("cg", 225, H.canvas_group(False))
    fill = w.child(N("Drawer/Window/Fill", "Fill", STRETCH))
    # Glass, not a slab (the user, 2026-10-16: "I liked the transparent backgrounds way more").
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.62), K.kit_sprite("kit_sliver14_fill"), True, raycast=True))
    bord = w.child(N("Drawer/Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver14_line1"), True))
    label(w.child(N("Drawer/Window/Title", "Title", TL(22, 18, 200, 22))), "Friends", "Label", "TextLight")
    # Minimise (a bar): slides the window back to the handle.
    HM.icon_button(w, "Drawer/Window/Minimise", "Minimise", None, TR(16 + BTN + 6, 14, BTN, BTN))
    # Maximise / restore: one button, two glyphs the drawer swaps (a screen to grow, a chevron to shrink).
    mx = HM.icon_button(w, "Drawer/Window/Maximise", "Maximise", "chrome_display", TR(16, 14, BTN, BTN))
    next(c for c in mx.children if c.key == "Drawer/Window/Maximise/Glyph").add("cg", 225, H.canvas_group(True))
    rs = mx.child(N("Drawer/Window/Maximise/Restore", "Restore", R(size=(14, 14))))
    rs.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.chrome_sprite("chrome_arrow_down"), preserve=True)) \
        .add("cg", 225, H.canvas_group(False))
    w.children.append(K.Instance("Drawer/Window/PartyPanel", "PartyPanel", PARTY_GUID, "PartyPanel",
                                 R((0, 0), (1, 1), (0, -28), (-40, -76)), ()))
    return w


def host_node():
    host = N("Host", "FriendsDrawerHost", STRETCH)
    host.add("cg", 225, H.canvas_group(True))
    drawer = host.child(N("Drawer", "Drawer", STRETCH))
    handle_node(drawer)
    window_node(drawer)
    host.add("gate", 114, lambda ctx, go: mono(go, S["gate"],
             "  style: 0\n"
             f"  onlineOnlyObjects:\n  - {ref(ctx.go('Drawer'))}\n  onlineOnlyControls: []\n  offlineOnlyObjects: []\n"
             "  disabledAlpha: 0.4\n"))
    host.add("drawer", 114, lambda ctx, go: mono(go, S["drawer"],
             f"  handleGroup: {ref(ctx.c('Drawer/Handle', 'cg'))}\n  handleButton: {ref(ctx.c('Drawer/Handle', 'button'))}\n"
             f"  windowGroup: {ref(ctx.c('Drawer/Window', 'cg'))}\n  window: {ref(ctx.rt('Drawer/Window'))}\n"
             f"  minimiseButton: {ref(ctx.c('Drawer/Window/Minimise', 'button'))}\n"
             f"  maximiseButton: {ref(ctx.c('Drawer/Window/Maximise', 'button'))}\n"
             f"  restoreGlyph: {ref(ctx.c('Drawer/Window/Maximise/Restore', 'cg'))}\n"
             f"  maximiseGlyph: {ref(ctx.c('Drawer/Window/Maximise/Glyph', 'cg'))}\n"
             f"  normalSize: {{x: {NORMAL[0]}, y: {NORMAL[1]}}}\n  maximisedSize: {{x: {MAXIMISED[0]}, y: {MAXIMISED[1]}}}\n"
             f"  openInset: {INSET}\n  moveSeconds: 0.22\n"))
    return host


def crystal_handler(sc):
    found = [f for f in sc.order if sc.cls[f] == 114 and sc.script(f) == S["crystal"]]
    assert len(found) == 1, "Menu_Main should hold one MenuCrystalClickHandler"
    return found[0]


def transform(text_):
    p = Patch(text_)
    assert p.sc.rect_of(p.sc.go_of(SAFE_AREA_RT)) == SAFE_AREA_RT and p.sc.field(p.sc.go_of(SAFE_AREA_RT), "m_Name") == "Safe Area", "Safe Area moved"
    assert p.sc.field(p.sc.go_of(MODAL_WINDOWS_RT), "m_Name") == "ModalWindows", "ModalWindows moved"
    host = host_node()
    rt = p.emit(host, SAFE_AREA_RT, MODAL_WINDOWS_RT)
    kids = [k for k in p.kids(SAFE_AREA_RT) if k != rt]
    at = kids.index(MODAL_WINDOWS_RT) + 1
    p.children(SAFE_AREA_RT, kids[:at] + [rt] + kids[at:])
    # Flying fades the menu through MenuCrystalClickHandler.menuCanvasGroups: the drawer joins it.
    ch = crystal_handler(p.sc)
    doc = p.doc(ch)
    m = re.search(r"^  menuCanvasGroups:\n((?:  - .*\n)*)", doc, re.M)
    groups = re.findall(r"\{fileID: (-?\d+)\}", m.group(1)) if m else []
    mine = str(p.ctx.c("Host", "cg"))
    if mine not in groups:
        groups.append(mine)
    p.replace(ch, T.set_block(doc, "menuCanvasGroups", K.refs_list("menuCanvasGroups", groups)))
    return p.sc.text()


def outputs():
    return {SCENE: transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())}


def validate(files):
    sc = T.Scene(files[SCENE])
    ctx = ctx_new()
    host_rt = str(ctx.rt("Host"))
    kids = re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[SAFE_AREA_RT], re.M).group(1))
    assert host_rt in kids, "the drawer is not under Safe Area"
    assert kids.index(host_rt) == kids.index(MODAL_WINDOWS_RT) + 1, "the drawer must draw right after ModalWindows (over every window)"
    dr = sc.docs[str(ctx.c("Host", "drawer"))]
    for f, want in (("handleButton", ctx.c("Drawer/Handle", "button")), ("window", ctx.rt("Drawer/Window")),
                    ("minimiseButton", ctx.c("Drawer/Window/Minimise", "button")), ("maximiseButton", ctx.c("Drawer/Window/Maximise", "button")),
                    ("windowGroup", ctx.c("Drawer/Window", "cg")), ("handleGroup", ctx.c("Drawer/Handle", "cg"))):
        assert f"  {f}: {{fileID: {want}}}" in dr, f"FriendsDrawer.{f} is not wired"
    gate = sc.docs[str(ctx.c("Host", "gate"))]
    assert f"  - {{fileID: {ctx.go('Drawer')}}}" in gate, "the drawer is not gated offline (the party panel was)"
    pis = [f for f in sc.order if sc.cls[f] == 1001 and f"guid: {PARTY_GUID}" in sc.docs[f]]
    assert len(pis) == 1, f"Menu_Main must instance the PartyPanel exactly once (the drawer); found {len(pis)}"
    assert f"m_TransformParent: {{fileID: {ctx.rt('Drawer/Window')}}}" in sc.docs[pis[0]], "the PartyPanel is not in the drawer window"
    ch = sc.docs[crystal_handler(sc)]
    assert f"  - {{fileID: {ctx.c('Host', 'cg')}}}" in ch, "the drawer does not fade with the menu in freestyle"
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) == "0" or sc.has(m.group(1)), f"dangling local fileID {m.group(1)} in {fid}"


def self_test(files):
    ctx = ctx_new()

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
    cases = [
        ("the handle unwired", lambda f: f.__setitem__(SCENE, re.sub(r"(  handleButton: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[SCENE], count=1))),
        ("the drawer ungated offline", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  onlineOnlyObjects:\n  - {{fileID: {ctx.go('Drawer')}}}\n", "  onlineOnlyObjects: []\n", 1))),
        ("the drawer left out of the freestyle fade", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  - {{fileID: {ctx.c('Host', 'cg')}}}\n", "", 1))),
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
    print("OK: the friends drawer validates in Menu_Main")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = [p for p, t in files.items() if open(os.path.join(REPO, p), encoding="utf-8").read() != t]
    if args.check:
        for p in stale:
            print(f"DRIFT: {p}")
        print("OK: no drift" if not stale else f"FAIL: {len(stale)} file(s) drifted")
        return 1 if stale else 0
    for path in stale:
        with open(os.path.join(REPO, path), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(files[path])
        print(f"wrote {path}")
    if not stale:
        print("no files written (already current)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
