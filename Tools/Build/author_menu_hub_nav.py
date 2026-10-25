#!/usr/bin/env python3
"""
Author the HUB WINDOW NAV into Menu_Main (Rev 7 of the style-B menu, Docs/MENU_REDESIGN_B.md section 47.2; the
approved "Window slide" board on the mockups canvas; the owner, 2026-10-17: "When in any of the arcade toybox or etc
screen pressing lt rt or pressing a new left right button should slide between the menus", then "slide stop").

    Safe Area/HubWindowNavHost   right after ModalWindows, so it draws over every hub window (and under the friends
    |                            drawer, which author_menu_friends_drawer.py keeps right after this host); a
    |                            CanvasGroup hidden at rest and HubWindowNav, which shows it while Toy Box, Arcade,
    |                            Arena or Mission is the top modal
    |- Strip                     under the top nav, centred: the LT keycap and a chevron, the four windows' words
    |                            (KitTextTab each: the shown window underlined in the Selection blue, a click jumps
    |                            there; the pad never lands on them), a chevron and the RT keycap
    |- LeftArrow / RightArrow    on the screen edges, inside the windows' 100 px gutters: a glass plate, the sky-blue
                                 chevron and the neighbour's name; hidden at the ends of the row (the slide stops)

ScreenSwitcher (the scene's one) gets its hub slide fields written explicitly: the order (Toy Box, Arcade, Arena,
Mission), the slide distance and its seconds. ScreenSwitcher reads LT / RT; HubWindowNav's arrows and words call
SlideHubWindow / SlideHubWindowTo.

STANDING DOWN: every document is minted with a deterministic fileID and re-emitted in place; a re-run is a no-op
(--check).

    python3 Tools/Build/author_menu_hub_nav.py              # write
    python3 Tools/Build/author_menu_hub_nav.py --check      # fail if the scene drifted
    python3 Tools/Build/author_menu_hub_nav.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))


def _load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, f"{name}.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


HM = _load("author_menu_home")
FD = _load("author_menu_friends_drawer")
P, C, A, K, T, H = HM.P, HM.C, HM.A, HM.K, HM.T, HM.H
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
ref, mono = C.ref, C.mono

REPO = K.REPO
SCENE = "Assets/_Scenes/Menu_Main.unity"
FILE_KEY = "Menu_Main/HubWindowNav"
S = dict(nav=C.script("HubWindowNav"), tab=K.kit_script("KitTextTab"))
SAFE_AREA_RT, MODAL_WINDOWS_RT, SWITCHER = FD.SAFE_AREA_RT, FD.MODAL_WINDOWS_RT, HM.SWITCHER

# The row, in ScreenSwitcher.ModalWindows values and the words the strip shows (authored on each tab).
HUB = (("ToyBox", 13, "Toy Box"), ("Arcade", 10, "Arcade"), ("Arena", 14, "Arena"), ("Mission", 15, "Voyage"))   # Rev 13: the Mission window is the Voyage window
HUB_WORD_TOKENS = (("restLabelToken", "NeutralLightest"), ("selectedLabelToken", "NeutralLightest"),
                   ("hoverLabelToken", "TextLight"))
SLIDE_DISTANCE, SLIDE_SECONDS = 160, 0.32   # ScreenSwitcher.hubSlideDistance / hubSlideSeconds

# The strip: under the 84-unit nav band, centred (the board: 120 down at 1080).
STRIP_TOP, STRIP_H = 104, 44
WORD_GAP = 30                     # between two words
CHEVRON = 14                      # the strip's small chevrons
# The strip's keycaps read LB / RB (Shombith, 2026-10-25): the shoulders slide the hub windows, the triggers page the
# top bar. Node keys keep their LT / RT names so every fileID stays put.
KEYCAP_W = max(K.label_width(w, "Label") for w in ("LB", "RB", "LT", "RT")) + 24
KEYCAP_H = K.line_height("Label") + 8
# The edge arrows: a glass plate inside the windows' gutters, centred on the screen's height, the chevron above the
# neighbour's name. The gutters are author_menu_arcade.py's WINDOW_LEFT / WINDOW_RIGHT (100 / 60): a plate 52 wide,
# 4 in, stays clear of the cards on both sides.
ARROW_X, ARROW_W, ARROW_H = 4, 52, 150
ARROW_GLYPH = 28
QUARTER_TURN = "  m_LocalRotation: {x: 0, y: 0, z: 0.7071068, w: 0.7071068}\n"
HALF_TURN = "  m_LocalRotation: {x: 0, y: 0, z: 1, w: 0}\n"


def ctx_new():
    return P.Ctx(FILE_KEY)


class Patch(HM.Patch):
    def __init__(self, text_):
        super().__init__(text_)
        self.ctx = ctx_new()


def word_width(word):
    return round(K.label_width(word.upper(), "Button"), 1)


def strip_layout():
    """x of each strip part from the strip's left edge, and the strip's width."""
    x, parts = 0.0, {}
    parts["LTKeycap"] = x
    x += KEYCAP_W + 10
    parts["LeftChevron"] = x
    x += CHEVRON + 18
    for key, _, word in HUB:
        parts[key] = x
        x += word_width(word) + WORD_GAP
    x += 18 - WORD_GAP
    parts["RightChevron"] = x
    x += CHEVRON + 10
    parts["RTKeycap"] = x
    x += KEYCAP_W
    return parts, round(x, 1)


def no_nav(fields):
    return fields.replace("    m_Mode: 3\n", "    m_Mode: 0\n", 1)


def strip_tab(strip, key, word, x):
    k = f"Strip/{key}"
    w = word_width(word)
    n = strip.child(N(k, key, R((0, 0.5), None, (x, 0), (w, STRIP_H), (0, 0.5))))
    hit = n.child(N(f"{k}/Hit", "Hit", STRETCH))
    hit.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    lab = n.child(N(f"{k}/Label", "Label", R((0, 0.5), (1, 0.5), (0, 3), (0, K.line_height("Button")))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(word, "Button", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("Button", "NeutralLightest", driven=True))
    und = n.child(N(f"{k}/Underline", "Underline", R((0, 0), (1, 0), (0, 4), (0, 2), (0.5, 0))))
    und.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Selection", 0.0)))
    n.add("tab", 114, lambda ctx, go: mono(go, S["tab"],
          no_nav(A.selectable()) + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Hit', 'img'))}\n"
          + A.calls([(ctx.c(k, "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {{fileID: 0}}\n"
          "  isSelected: 0\n"
          f"  label: {ref(ctx.c(f'{k}/Label', 'tmp'))}\n  underline: {ref(ctx.c(f'{k}/Underline', 'img'))}\n"
          # Arcade, Mission, Arena and Toy Box all read in the one neutral blue; the underline marks the shown one
          # (Shombith, 2026-10-24).
          + "".join(f"  {f}: {K.TOKEN_INDEX[t]}\n" for f, t in HUB_WORD_TOKENS)))
    n.add("audio", 114, A.menu_audio(11))


def chevron(parent, k, name, r, colour):
    g = parent.child(N(k, name, r))
    g.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(colour, K.chrome_sprite("chrome_chevron"), preserve=True))
    return g


def strip_node(host):
    parts, width = strip_layout()
    s = host.child(N("Strip", "Strip", R((0.5, 1), None, (0, -STRIP_TOP), (width, STRIP_H), (0.5, 1))))
    for side, text_ in (("LTKeycap", "LB"), ("RTKeycap", "RB")):
        slot = s.child(N(f"Strip/{side}", side, R((0, 0.5), None, (parts[side], 0), (KEYCAP_W, KEYCAP_H), (0, 0.5))))
        slot.children.append(P.Inst(f"Strip/{side}/Keycap", "KitKeycap", "Keycap", STRETCH, [("Label#tmp", "m_text", text_, None)]))
    for side in ("LeftChevron", "RightChevron"):
        chevron(s, f"Strip/{side}", side, R((0, 0.5), None, (parts[side] + CHEVRON / 2, 0), (CHEVRON, CHEVRON)),
                tok("NeutralLightest"))
    for key, _, word in HUB:
        strip_tab(s, key, word, parts[key])
    return s


def arrow_node(host, side):
    left = side == "Left"
    k = f"{side}Arrow"
    a = host.child(N(k, k, R((0, 0.5) if left else (1, 0.5), None, (ARROW_X if left else -ARROW_X, 0),
                             (ARROW_W, ARROW_H), (0, 0.5) if left else (1, 0.5))))
    fill = a.child(N(f"{k}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.55), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bord = a.child(N(f"{k}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver10_line1"), True))
    chevron(a, f"{k}/Chevron", "Chevron", R((0.5, 1), None, (0, -22 - ARROW_GLYPH / 2), (ARROW_GLYPH, ARROW_GLYPH)), K.WHITE)
    # The neighbour's name, turned to run up the plate (the board); HubWindowNav copies it from the strip's word.
    name = a.child(N(f"{k}/Label", "Label", R((0.5, 0), None, (0, 50), (ARROW_H - ARROW_GLYPH - 50, 20))))
    H.label(name, "", "Label", "NeutralLightest", align="center")
    tint = (no_nav(A.selectable()).replace("  m_Transition: 0\n", "  m_Transition: 1\n")
            .replace("    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n", f"    m_NormalColor: {K.col(tok('Selection'))}\n")
            .replace("    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
                     f"    m_HighlightedColor: {K.col(tok('TextLight'))}\n")
            .replace("    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}\n",
                     f"    m_PressedColor: {K.col(tok('TextLight'))}\n")
            .replace("    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n",
                     f"    m_SelectedColor: {K.col(tok('Selection'))}\n"))
    a.add("button", 114, lambda ctx, go: mono(go, A.BUTTON,
          tint + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Chevron', 'img'))}\n"
          + A.calls([(ctx.c(k, "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])))
    a.add("audio", 114, A.menu_audio(11))
    return a


def host_node():
    host = N("Host", "HubWindowNavHost", STRETCH)
    host.add("cg", 225, H.canvas_group(False))
    strip_node(host)
    arrow_node(host, "Left")
    arrow_node(host, "Right")
    host.add("nav", 114, lambda ctx, go: mono(go, S["nav"],
             f"  screenSwitcher: {{fileID: {SWITCHER}}}\n  group: {ref(ctx.c('Host', 'cg'))}\n"
             "  items:\n" + "".join(
                 f"  - window: {value}\n    tab: {ref(ctx.c(f'Strip/{key}', 'tab'))}\n"
                 f"    label: {ref(ctx.c(f'Strip/{key}/Label', 'tmp'))}\n" for key, value, _ in HUB)
             + f"  leftArrow: {ref(ctx.c('LeftArrow', 'button'))}\n  leftLabel: {ref(ctx.c('LeftArrow/Label', 'tmp'))}\n"
             f"  rightArrow: {ref(ctx.c('RightArrow', 'button'))}\n  rightLabel: {ref(ctx.c('RightArrow/Label', 'tmp'))}\n"))
    return host


def hub_order_hex():
    return "".join(struct.pack("<i", value).hex() for _, value, _ in HUB)


def switcher_fields(doc):
    """ScreenSwitcher's rev 7 fields, written explicitly right after hubCompanions (declaration order)."""
    doc = re.sub(r"  hubWindowOrder: .*\n  hubSlideDistance: .*\n  hubSlideSeconds: .*\n", "", doc)
    m = re.search(r"^  hubCompanions:\n(?:  - .*\n)*|^  hubCompanions: \[\]\n", doc, re.M)
    assert m, "ScreenSwitcher has no hubCompanions field (the anchor for the slide fields)"
    fields = (f"  hubWindowOrder: {hub_order_hex()}\n  hubSlideDistance: {SLIDE_DISTANCE}\n"
              f"  hubSlideSeconds: {SLIDE_SECONDS}\n")
    return doc[:m.end()] + fields + doc[m.end():]


def turn(p, rt, rotation):
    d = p.doc(rt)
    assert "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n" in d
    p.replace(rt, d.replace("  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n", rotation, 1)
              .replace("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n",
                       "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: %d}\n" % (90 if rotation == QUARTER_TURN else 180), 1))


def doc_anchor(sc):
    """The document the nav's documents follow in the file: the friends drawer's last one when the scene has the
    drawer (it re-emits its own right after ModalWindows, so following it keeps both generators' output stable
    whichever runs last), else ModalWindows."""
    drawer_rt = str(FD.ctx_new().rt("Host"))
    if not sc.has(drawer_rt):
        return MODAL_WINDOWS_RT
    docs = FD.subtree_docs(sc, drawer_rt)
    return max(docs, key=sc.order.index)


def transform(text_):
    p = Patch(text_)
    assert p.sc.field(p.sc.go_of(SAFE_AREA_RT), "m_Name") == "Safe Area", "Safe Area moved"
    assert p.sc.field(p.sc.go_of(MODAL_WINDOWS_RT), "m_Name") == "ModalWindows", "ModalWindows moved"
    host_rt = str(p.ctx.rt("Host"))
    if p.sc.has(host_rt):
        for fid in FD.subtree_docs(p.sc, host_rt):
            p.sc.remove(fid)
    rt = p.emit(host_node(), SAFE_AREA_RT, doc_anchor(p.sc))
    kids = [k for k in p.kids(SAFE_AREA_RT) if k != rt]
    at = kids.index(MODAL_WINDOWS_RT) + 1
    p.children(SAFE_AREA_RT, kids[:at] + [rt] + kids[at:])
    # The left arrow's chevron points left and both names run up their plates.
    turn(p, str(p.ctx.rt("LeftArrow/Chevron")), HALF_TURN)
    turn(p, str(p.ctx.rt("Strip/LeftChevron")), HALF_TURN)
    for side in ("Left", "Right"):
        turn(p, str(p.ctx.rt(f"{side}Arrow/Label")), QUARTER_TURN)
    p.replace(SWITCHER, switcher_fields(p.doc(SWITCHER)))
    return p.sc.text()


def outputs():
    return {SCENE: transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())}


def validate(files):
    sc = T.Scene(files[SCENE])
    ctx = ctx_new()
    host_rt = str(ctx.rt("Host"))
    assert sc.has(host_rt), "the hub window nav is missing"
    kids = re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", sc.docs[SAFE_AREA_RT], re.M).group(1))
    assert kids.index(host_rt) == kids.index(MODAL_WINDOWS_RT) + 1, "the hub nav must draw right after ModalWindows (over every window)"
    cg = sc.docs[str(ctx.c("Host", "cg"))]
    assert "  m_Alpha: 0\n" in cg and "  m_BlocksRaycasts: 0\n" in cg, "the hub nav must rest hidden (it shows only over a hub window)"
    nav = sc.docs[str(ctx.c("Host", "nav"))]
    assert S["nav"] in nav, "HubWindowNavHost carries no HubWindowNav"
    assert f"  screenSwitcher: {{fileID: {SWITCHER}}}\n" in nav, "HubWindowNav is not wired to the scene's ScreenSwitcher"
    windows = [int(v) for v in re.findall(r"  - window: (\d+)\n", nav)]
    assert windows == [v for _, v, _ in HUB], f"the strip's windows are {windows}, not Toy Box, Arcade, Arena, Mission"
    for key, _, word in HUB:
        tab = sc.docs[str(ctx.c(f"Strip/{key}", "tab"))]
        assert S["tab"] in tab, f"the strip's {word} is not a KitTextTab"
        assert re.search(r"  m_Navigation:\n    m_Mode: 0\n", tab), f"the pad can land on the strip's {word} (it must stay on the cards)"
        lab = sc.docs[str(ctx.c(f"Strip/{key}/Label", "tmp"))]
        assert f"  m_text: {word}\n" in lab, f"the strip's {word} lost its authored word"
        for f, t in HUB_WORD_TOKENS:
            assert f"  {f}: {K.TOKEN_INDEX[t]}\n" in tab, f"the strip's {word}: {f} is not {t} (all four words read in one blue)"
    for side in ("Left", "Right"):
        btn = sc.docs[str(ctx.c(f"{side}Arrow", "button"))]
        assert re.search(r"  m_Navigation:\n    m_Mode: 0\n", btn), f"the pad can land on the {side.lower()} arrow"
        assert f"  {side.lower()}Arrow: {{fileID: {ctx.c(f'{side}Arrow', 'button')}}}\n" in nav, f"the {side.lower()} arrow is not wired"
    assert QUARTER_TURN in sc.docs[str(ctx.rt("LeftArrow/Label"))], "the arrow's name no longer runs up the plate"
    assert HALF_TURN in sc.docs[str(ctx.rt("LeftArrow/Chevron"))], "the left arrow's chevron points right"
    sw = sc.docs[SWITCHER]
    assert f"  hubWindowOrder: {hub_order_hex()}\n" in sw, "ScreenSwitcher's hub order is not Toy Box, Arcade, Arena, Mission"
    assert sw.count("  hubWindowOrder: ") == 1, "ScreenSwitcher carries the hub order twice"
    # Every reference the new documents make resolves inside the scene.
    for fid in FD.subtree_docs(sc, host_rt):
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) == "0" or sc.has(m.group(1)) or m.group(1) in ("11400000", "11500000", "21300000", "2100000"), \
                f"the hub nav references a fileID the scene does not hold ({m.group(1)})"


def drift(files):
    return [p for p, t in files.items() if open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def self_test(files):
    ctx = ctx_new()
    ok = True

    def expect(label_, mutate):
        nonlocal ok
        bad = {SCENE: mutate(files[SCENE])}
        assert bad[SCENE] != files[SCENE], f"plant did not apply: {label_}"
        try:
            validate(bad)
            print(f"  MISSED: {label_}")
            ok = False
        except AssertionError as e:
            print(f"  caught: {label_} ({e})")

    def in_doc(text_, fid, old, new):
        head = f"&{fid}\n"
        i = text_.index(head)
        j = text_.find("\n--- ", i)
        j = len(text_) if j < 0 else j
        seg = text_[i:j]
        assert old in seg, f"{old!r} not in document {fid}"
        return text_[:i] + seg.replace(old, new, 1) + text_[j:]

    expect("the shown hub word turning white again", lambda t: in_doc(t, ctx.c("Strip/Arcade", "tab"),
           f"  selectedLabelToken: {K.TOKEN_INDEX['NeutralLightest']}\n", f"  selectedLabelToken: {K.TOKEN_INDEX['TextLight']}\n"))
    expect("the strip skips the Arena", lambda t: in_doc(t, ctx.c("Host", "nav"), "  - window: 14\n", "  - window: 15\n"))
    expect("the pad can land on a strip word", lambda t: in_doc(
        t, ctx.c("Strip/Arcade", "tab"), "  m_Navigation:\n    m_Mode: 0\n", "  m_Navigation:\n    m_Mode: 3\n"))
    expect("the nav rests visible over Home", lambda t: in_doc(t, ctx.c("Host", "cg"), "  m_Alpha: 0\n", "  m_Alpha: 1\n"))
    expect("ScreenSwitcher wraps Mission back to Toy Box", lambda t: in_doc(
        t, SWITCHER, f"  hubWindowOrder: {hub_order_hex()}\n", f"  hubWindowOrder: {hub_order_hex()}0d000000\n"))
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files = outputs()
    validate(files)
    if args.self_test:
        return 0 if self_test(files) else 1
    if args.check:
        bad = drift(files)
        for p in bad:
            print("DRIFT:", p)
        print(f"FAIL: {len(bad)} file(s) differ" if bad else "OK: no drift")
        return 1 if bad else 0
    for p, t in files.items():
        with open(os.path.join(REPO, p), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    print(f"wrote {len(files)} file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
