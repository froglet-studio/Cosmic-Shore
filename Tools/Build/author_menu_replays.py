#!/usr/bin/env python3
"""
Author the REPLAY BROWSER - the window the Arcade header's Replays button opens - and the HUD a
replay is watched under, as style-B prefabs placed in Menu_Main (Docs/REPLAYS.md; the kit and the
window frame of Docs/MENU_REDESIGN_B.md, built the way author_menu_credits.py builds Credits).

    Assets/_Prefabs/UI Elements/Panels/ReplayRow.prefab
        One replay: ReplayRowView on a framed row - the mode's name, when and how long, who won
        (or that it ended without results), pilots and intensity, then Watch (Play) and Delete;
        or, on a replay recorded on another device, the note that says so instead of the buttons.

    Assets/_Prefabs/UI Elements/Panels/ReplayPlaybackHud.prefab
        The controls over a replay being watched: its OWN root canvas (sorting order 500, so it
        draws over everything and is the one canvas ReplayCurtain leaves up). It opens on a loading
        panel ("Building the arena", CANCEL / B) while the replay's arena stands, then shows the
        title and when,
        Exit (B), a scrub bar, the time and speed readouts, the transport row (skip back, pause,
        skip ahead, slower, faster, camera, next pilot), the shot and pilot readouts and a line
        naming the pad controls. Its buttons are not navigation stops: the pad drives the replay
        through shortcuts (ReplayPlaybackHud), because the sticks fly the replay camera.

    Assets/_Prefabs/UI Elements/Panels/ReplayBrowserModal.prefab
        The window: CanvasGroup + Animator (Standard.controller) + ReplayBrowserModal (ModalType
        REPLAYS = 19) on the root; the dim; the window frame and brackets; "Replays"; a status line;
        Close (B); a scroll view whose column holds the empty state, the ON THIS DEVICE section and
        the ON YOUR OTHER DEVICES section (rows are instantiated into their lists at runtime), and
        a thin scrollbar. Every word a row or the HUD shows is a field on these components.

    Assets/_Scenes/Menu_Main.unity
        One ReplayBrowserModal instance under ModalWindows - placed just BEFORE the CreditsModal,
        which author_menu_credits.py keeps last - wired to the scene's ScreenSwitcher and listed in
        ScreenSwitcher.Modals (before Credits too). Nothing else in the scene is touched. The two
        generators can run in either order and both stay idempotent.

The Replays button itself is authored by author_menu_arcade.py (the Arcade header).

STANDING DOWN: every output is re-derived on each run; the scene edit removes its own documents and
list entries before writing them again, so a re-run is a no-op (--check).

    python3 Tools/Build/author_menu_replays.py              # write
    python3 Tools/Build/author_menu_replays.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_replays.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_credits", os.path.join(HERE, "author_menu_credits.py"))
CR = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(CR)

W, P, C, A, K, T, H, M = CR.W, CR.P, CR.C, CR.A, CR.K, CR.T, CR.H, CR.M
REPO = K.REPO
SCENE = CR.SCENE
OUT_DIR = CR.OUT_DIR
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono = C.TL, C.TR, C.ref, C.mono
label, le = H.label, H.le
image_node = A.image_node

BROWSER, ROW, HUD = "ReplayBrowserModal", "ReplayRow", "ReplayPlaybackHud"
PREFABS = (ROW, HUD, BROWSER)
REPLAYS_TYPE = 19                                         # ScreenSwitcher.ModalWindows.REPLAYS
S = dict(browser=C.script("ReplayBrowserModal"), row=C.script("ReplayRowView"), hud=C.script("ReplayPlaybackHud"),
         switcher=C.script("ScreenSwitcher"), button=K.kit_script("KitButton"),
         scroll_into=K.kit_script("KitScrollIntoView"), audio=C.script("MenuAudio"))
SLIDER = P.SLIDER                                         # UnityEngine.UI.Slider
PACKAGE = CR.PACKAGE | {SLIDER, K.CANVAS_SCALER, K.GRAPHIC_RAYCASTER}
SCROLLBAR = CR.SCROLLBAR

WIN_W, WIN_H, PAD = CR.WIN_W, CR.WIN_H, CR.PAD
SCROLL_TOP, SCROLL_BOTTOM = CR.SCROLL_TOP, CR.SCROLL_BOTTOM
ROW_H = 104
HUD_SORTING = 500
TOP_H, BOTTOM_H = 104, 176
COL = "Window/Scroll/Viewport/Content"

# Every word, in one place. The browser's and the HUD's are component fields; a row's samples are
# overwritten by Bind and are only what the prefab shows in the editor.
BROWSER_WORDS = dict(whenTemplate="{date} - {duration}", dateFormat="d MMM yyyy, HH:mm",
                     winnerTemplate="Winner: {winner}", noResultLine="No final results",
                     detailTemplate="{pilots} pilots - Intensity {intensity}",
                     openFailedLine="That replay could not be opened.")
HUD_WORDS = dict(timeTemplate="{position} / {duration}", speedTemplate="x{speed}", pauseLabel="PAUSE", playLabel="PLAY")
SHOT_NAMES = ("FREE", "PILOT", "ORBIT", "CHASE")          # TheaterShot order
CONFIRM_DELETE = "CONFIRM"
EMPTY_LINE = "No replays yet. Finish a Regatta and its replay is saved here, ready to watch."
ELSEWHERE_LINE = "Recorded on another device"
PAD_HINT = "A pause    LB / RB skip    LT / RT speed    D-pad camera and pilot    B exit"
LOADING_LINE = "Building the arena"
LOADING_HINT = "The race plays in the arena it was flown in."
TRANSPORT = (  # key, caption, component field
    ("Back", "SKIP BACK", "backButton"), ("Pause", "PAUSE", "pauseButton"), ("Forward", "SKIP AHEAD", "forwardButton"),
    ("Slower", "SLOWER", "slowerButton"), ("Faster", "FASTER", "fasterButton"), ("Shot", "CAMERA", "shotButton"),
    ("Pilot", "NEXT PILOT", "pilotButton"))


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def F(name):
    return K.FileIds(name)


def q(value):
    return K.yaml_scalar(value)


def prefab_ref(name, comp):
    return f"{{fileID: {F(name)(comp)}, guid: {asset_guid(name)}, type: 3}}"


# --------------------------------------------------------------------------------------------
# A small kit button, built in place (no nested instance to reach into)
# --------------------------------------------------------------------------------------------

def kit_button(parent, key, name, caption, place=None, variant="Default", opaque=False, nav=3, keycap=None,
               scroll_into=False):
    """A KitButtonSmall-sized KitButton: focus ring, fill, border, optional keycap, label, MenuAudio press
    sound. `place(w, h)` positions it; without it a layout group does."""
    role = "ButtonSmall"
    w_label = K.label_width(caption, role)
    keycap_w = K.label_width(keycap, "Label") + 24 if keycap else 0
    w = 16 + (keycap_w + 10 if keycap else 0) + w_label + 16
    h = K.line_height(role) + 18
    b = parent.child(N(key, name, place(w, h) if place else R(size=(w, h))))
    A.ring_node(b, f"{key}/FocusRing", 10)
    border, fillc, content = K.button_palette(variant, opaque)
    f = b.child(N(f"{key}/Fill", "Fill", STRETCH))
    f.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)).add("le", 114, le(ignore=True))
    bd = b.child(N(f"{key}/Border", "Border", STRETCH))
    bd.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)).add("le", 114, le(ignore=True))
    if keycap:
        b.children.append(P.Inst(f"{key}Keycap", "KitKeycap", "Keycap", R(size=(keycap_w, K.line_height("Label") + 8)),
                                 [("Label#tmp", "m_text", keycap, None)]))
    lab = b.child(N(f"{key}/Label", "Label", R(size=(w_label, K.line_height(role)))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(caption, role, content)) \
        .add("kt", 114, K.kit_text(role, driven=True))
    b.add("button", 114, lambda ctx, go: mono(go, S["button"],
          A.selectable(nav) + f"  m_TargetGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n"
          + A.calls([(ctx.c(key, "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{key}/FocusRing', 'img'))}\n"
          f"  variant: {K.VARIANTS[variant]}\n  fill: {1 if opaque else 0}\n"
          f"  fillGraphic: {ref(ctx.c(f'{key}/Fill', 'img'))}\n  borderGraphic: {ref(ctx.c(f'{key}/Border', 'img'))}\n"
          + K.refs_list("contentGraphics", [ctx.c(f"{key}/Label", "tmp")]) + "  playGradient: {fileID: 0}\n"))
    b.add("audio", 114, A.menu_audio(1))
    if scroll_into:
        b.add("scroll", 114, A.scroll_into())
    b.add("hlg", 114, K.hv_layout(False, (16, 16, 9, 9), 10, 4, (0, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))
    return b


# --------------------------------------------------------------------------------------------
# ReplayRow.prefab
# --------------------------------------------------------------------------------------------

def build_row():
    root = N("root", ROW, R((0, 1), (1, 1), (0, 0), (0, ROW_H), (0.5, 1)))
    root.add("le", 114, le(mh=ROW_H, ph=ROW_H))
    H.framed(root, "Frame", 10, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.9)
    label(root.child(N("Title", "Title", TL(24, 18, 500, 30))), "Regatta", "H3", "TextLight")
    label(root.child(N("When", "When", TL(24, 56, 500, 22))), "1 Jan 2026, 12:00 - 3:07", "BodySmall", "NeutralLightest")
    label(root.child(N("Result", "Result", TL(540, 20, 250, 26))), "Winner: Pilot", "Body", "TextLight")
    label(root.child(N("Detail", "Detail", TL(540, 56, 250, 22))), "6 pilots - Intensity 2", "BodySmall", "NeutralLightest")

    acts = root.child(N("Actions", "Actions", R((1, 0.5), None, (-20, 0), (250, 40), (1, 0.5))))
    acts.add("cg", 225, H.canvas_group(True)).add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 5, (0, 0), (0, 0)))
    kit_button(acts, "Actions/Watch", "Watch", "WATCH", variant="Play", opaque=True, scroll_into=True)
    kit_button(acts, "Actions/Delete", "Delete", "DELETE", scroll_into=True)

    el = root.child(N("Elsewhere", "Elsewhere", R((1, 0.5), None, (-24, 0), (360, 22), (1, 0.5))))
    el.add("cg", 225, H.canvas_group(False))
    label(el.child(N("Elsewhere/Label", "Label", STRETCH)), ELSEWHERE_LINE, "BodySmall", "InactiveLight", align="right")

    root.add("row", 114, lambda ctx, go: mono(go, S["row"],
             f"  confirmDeleteLabel: {q(CONFIRM_DELETE)}\n"
             f"  title: {ref(ctx.c('Title', 'tmp'))}\n  when: {ref(ctx.c('When', 'tmp'))}\n"
             f"  result: {ref(ctx.c('Result', 'tmp'))}\n  detail: {ref(ctx.c('Detail', 'tmp'))}\n"
             f"  actions: {ref(ctx.c('Actions', 'cg'))}\n"
             f"  watchButton: {ref(ctx.c('Actions/Watch', 'button'))}\n"
             f"  deleteButton: {ref(ctx.c('Actions/Delete', 'button'))}\n"
             f"  deleteText: {ref(ctx.c('Actions/Delete/Label', 'tmp'))}\n"
             f"  elsewhere: {ref(ctx.c('Elsewhere', 'cg'))}\n  confirmSeconds: 3\n"))
    return root


# --------------------------------------------------------------------------------------------
# ReplayPlaybackHud.prefab
# --------------------------------------------------------------------------------------------

def canvas_body(sorting):
    return lambda ctx, go: (
        "Canvas:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
        "  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n"
        "  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n"
        "  m_OverridePixelPerfect: 0\n  m_SortingBucketNormalizedSize: 0\n  m_VertexColorAlwaysGammaSpace: 1\n"
        "  m_AdditionalShaderChannelsFlag: 25\n  m_UpdateRectTransformForStandalone: 0\n"
        f"  m_SortingLayerID: 0\n  m_SortingOrder: {sorting}\n  m_TargetDisplay: 0\n")


def scaler_body():
    return lambda ctx, go: mono(go, K.CANVAS_SCALER,
        "  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n"
        "  m_ReferenceResolution: {x: 1920, y: 1080}\n  m_ScreenMatchMode: 0\n  m_MatchWidthOrHeight: 0.5\n"
        "  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n"
        "  m_DynamicPixelsPerUnit: 1\n  m_PresetInfoIsWorld: 0\n")


def raycaster_body():
    return lambda ctx, go: mono(go, K.GRAPHIC_RAYCASTER,
        "  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n"
        "    serializedVersion: 2\n    m_Bits: 4294967295\n")


def scrub(parent, key):
    """A thin seek bar: SurfaceDark track, ControlActive fill, an octagon handle. Pointer only (no navigation)."""
    sl = parent.child(N(key, "Scrub", R((0, 1), (1, 1), (0, -30), (-2 * PAD, 10), (0.5, 0.5))))
    image_node(sl, f"{key}/Background", "Background", STRETCH, "SurfaceDark", raycast=True)
    fa = sl.child(N(f"{key}/FillArea", "Fill Area", STRETCH))
    image_node(fa, f"{key}/FillArea/Fill", "Fill", R((0, 0), (0, 1), (0, 0), (0, 0)), "ControlActive")
    ha = sl.child(N(f"{key}/HandleArea", "Handle Slide Area", R((0, 0), (1, 1), (0, 0), (-20, 0))))
    image_node(ha, f"{key}/HandleArea/Handle", "Handle", R((0, 0), (0, 1), (0, 0), (20, 14)), "TextLight",
               K.kit_sprite("kit_oct_fill"), raycast=True)
    sl.add("slider", 114, lambda ctx, go: mono(go, SLIDER,
           A.selectable(0) + f"  m_TargetGraphic: {ref(ctx.c(f'{key}/HandleArea/Handle', 'img'))}\n"
           f"  m_FillRect: {ref(ctx.rt(f'{key}/FillArea/Fill'))}\n"
           f"  m_HandleRect: {ref(ctx.rt(f'{key}/HandleArea/Handle'))}\n"
           "  m_Direction: 0\n  m_MinValue: 0\n  m_MaxValue: 1\n  m_WholeNumbers: 0\n  m_Value: 0\n"
           "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    return sl


def build_hud():
    root = N("root", HUD, R((0, 0), None, (0, 0), (0, 0), (0, 0)))
    root.add("canvas", 223, canvas_body(HUD_SORTING)).add("scaler", 114, scaler_body()) \
        .add("raycaster", 114, raycaster_body())

    # The two bands sit in one group, hidden while the arena builds; the loading panel shows instead.
    controls = root.child(N("Controls", "Controls", STRETCH))
    controls.add("cg", 225, H.canvas_group(False))
    top = controls.child(N("Top", "Top", R((0, 1), (1, 1), (0, 0), (0, TOP_H), (0.5, 1))))
    image_node(top, "Top/Shade", "Shade", STRETCH, "SurfaceBlack", alpha=0.72)
    label(top.child(N("Top/Title", "Title", TL(PAD, 20, 1100, 36))), "Regatta", "H2", "TextLight")
    label(top.child(N("Top/When", "When", TL(PAD, 62, 1100, 22))), "1 Jan 2026, 12:00 - 3:07", "BodySmall", "NeutralLightest")
    kit_button(top, "Top/Exit", "Exit", "EXIT", place=lambda w, h: TR(PAD, 32, w, h), nav=0, keycap="B")

    bot = controls.child(N("Bottom", "Bottom", R((0, 0), (1, 0), (0, 0), (0, BOTTOM_H), (0.5, 0))))
    image_node(bot, "Bottom/Shade", "Shade", STRETCH, "SurfaceBlack", alpha=0.72)
    scrub(bot, "Bottom/Scrub")
    label(bot.child(N("Bottom/Time", "Time", TL(PAD, 52, 360, 26))), "0:00 / 0:00", "Data", "TextLight")
    label(bot.child(N("Bottom/Speed", "Speed", TR(PAD, 52, 200, 26))), "x1", "Data", "TextLight", align="right")

    row = bot.child(N("Bottom/Transport", "Transport", R((0.5, 1), None, (0, -48), (1000, 40), (0.5, 1))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 4, (0, 0), (0, 0)))
    for key, caption, _ in TRANSPORT:
        kit_button(row, f"Bottom/Transport/{key}", key, caption, nav=0, opaque=key == "Pause",
                   variant="Play" if key == "Pause" else "Default")

    label(bot.child(N("Bottom/Shot", "Shot", TR(PAD, 92, 360, 18))), "ORBIT", "Label", "ControlActive", align="right")
    fg = bot.child(N("Bottom/Follow", "Follow", TR(PAD, 114, 360, 24)))
    fg.add("cg", 225, H.canvas_group(True))
    label(fg.child(N("Bottom/Follow/Name", "Name", STRETCH)), "Pilot", "Body", "TextLight", align="right")
    label(bot.child(N("Bottom/Hint", "Hint", R((0.5, 0), None, (0, 14), (1100, 20), (0.5, 0)))), PAD_HINT,
          "BodySmall", "NeutralLightest", align="center")

    # Shown first: the replay's arena builds before anything plays (ReplayArenaStage).
    loading = root.child(N("Loading", "Loading", STRETCH))
    loading.add("cg", 225, H.canvas_group(True))
    image_node(loading, "Loading/Dim", "Dim", STRETCH, "SurfaceBlack", alpha=0.9, raycast=True)
    label(loading.child(N("Loading/Title", "Title", R((0.5, 0.5), None, (0, 40), (1100, 36)))), LOADING_LINE, "H2",
          "TextLight", align="center")
    label(loading.child(N("Loading/Hint", "Hint", R((0.5, 0.5), None, (0, 0), (1100, 22)))), LOADING_HINT, "BodySmall",
          "NeutralLightest", align="center")
    kit_button(loading, "Loading/Cancel", "Cancel", "CANCEL",
               place=lambda w, h: R((0.5, 0.5), None, (0, -56), (w, h)), nav=0, keycap="B")

    root.add("hud", 114, lambda ctx, go: mono(go, S["hud"], hud_body(ctx)))
    return root


def hud_body(ctx):
    words = "".join(f"  {k}: {q(v)}\n" for k, v in HUD_WORDS.items())
    shots = "  shotNames:\n" + "".join(f"  - {q(s)}\n" for s in SHOT_NAMES)
    tmp = lambda key: ref(ctx.c(key, "tmp"))
    button = {field: ref(ctx.c(f"Bottom/Transport/{key}", "button")) for key, _, field in TRANSPORT}
    order = ("pauseButton", "backButton", "forwardButton", "slowerButton", "fasterButton", "shotButton", "pilotButton")
    return (words + shots
            + f"  titleText: {tmp('Top/Title')}\n  whenText: {tmp('Top/When')}\n"
            f"  timeText: {tmp('Bottom/Time')}\n  speedText: {tmp('Bottom/Speed')}\n  shotText: {tmp('Bottom/Shot')}\n"
            f"  followText: {tmp('Bottom/Follow/Name')}\n  followGroup: {ref(ctx.c('Bottom/Follow', 'cg'))}\n"
            f"  pauseText: {tmp('Bottom/Transport/Pause/Label')}\n  scrub: {ref(ctx.c('Bottom/Scrub', 'slider'))}\n"
            f"  loadingGroup: {ref(ctx.c('Loading', 'cg'))}\n  controlsGroup: {ref(ctx.c('Controls', 'cg'))}\n"
            f"  cancelButton: {ref(ctx.c('Loading/Cancel', 'button'))}\n"
            + "".join(f"  {f}: {button[f]}\n" for f in order)
            + f"  exitButton: {ref(ctx.c('Top/Exit', 'button'))}\n")


# --------------------------------------------------------------------------------------------
# ReplayBrowserModal.prefab
# --------------------------------------------------------------------------------------------

def section(col, key, name, caption, list_name):
    sec = col.child(N(f"{COL}/{key}", name, R(size=(0, 40))))
    sec.add("cg", 225, H.canvas_group(False)).add("le", 114, le(ignore=True)) \
        .add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 0, (1, 1), (1, 0)))
    head = sec.child(N(f"{COL}/{key}/Heading", "Heading", R(size=(0, 18))))
    label(head, caption, "Label", "NeutralLightest")
    head.add("le", 114, le(ph=18))
    lst = sec.child(N(f"{COL}/{key}/List", list_name, R(size=(0, 0))))
    lst.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 0, (1, 1), (1, 0)))
    return sec


def build_browser():
    root = N("root", BROWSER, STRETCH)
    root.add("cg", 225, CR.hidden_group()).add("anim", 95, CR.animator())
    image_node(root, "Dim", "Dim", STRETCH, "SurfaceBlack", alpha=0.88, raycast=True)
    content = root.child(N("Content", "Content", STRETCH))

    win = content.child(N("Window", "Window", R((0.5, 0.5), None, (0, -42), (WIN_W, WIN_H))))
    win.add("cg", 225, H.canvas_group(True))
    fill = win.child(N("Window/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack"))
    bord = win.child(N("Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True)).add("th", 114, H.themed("TextLight"))
    M.bracket_nodes(win, "Window/Bracket", inset=14)

    # The list first in the hierarchy, so the first Watch is reached before Close.
    sv = win.child(N("Window/Scroll", "Scroll View", R((0, 0), (1, 1), (-4, (SCROLL_BOTTOM - SCROLL_TOP) / 2),
                                                       (-2 * PAD - 8, -(SCROLL_TOP + SCROLL_BOTTOM)))))
    sv.add("cg", 225, H.canvas_group(True)).add("in", 114, M.entrance("Rise", distance=14, delay=0.12))
    sv.add("scroll", 114, lambda ctx, go: mono(go, A.SCROLL_RECT,
           f"  m_Content: {ref(ctx.rt(COL))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
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
    col = vp.child(N(COL, "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    col.add("vlg", 114, K.hv_layout(True, (0, 0, 8, 32), 28, 0, (1, 1), (1, 0))).add("fit", 114, K.fitter(0, 2))
    empty = col.child(N(f"{COL}/Empty", "Empty", R(size=(0, 60))))
    empty.add("cg", 225, H.canvas_group(False)).add("le", 114, le(ignore=True, ph=60))
    label(empty.child(N(f"{COL}/Empty/Label", "Label", STRETCH)), EMPTY_LINE, "Body", "NeutralLightest", wrap=True)
    section(col, "Local", "LocalSection", "ON THIS DEVICE", "LocalList")
    section(col, "Other", "OtherSection", "ON YOUR OTHER DEVICES", "OtherList")
    CR.scrollbar(win, "Window/Scrollbar")

    t = win.child(N("Window/Title", "Title", TL(PAD, 34, 600, 46)))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("Replays", "H1", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    st = win.child(N("Window/Status", "Status", TL(PAD, 84, 900, 20)))
    st.add("cg", 225, H.canvas_group(False))
    label(st, BROWSER_WORDS["openFailedLine"], "BodySmall", "Danger")
    W.close_button(win)

    root.add("browser", 114, lambda ctx, go: mono(go, S["browser"], browser_body(ctx)))
    return root


def browser_body(ctx):
    words = "".join(f"  {k}: {q(v)}\n" for k, v in BROWSER_WORDS.items())
    return ("  sharpAnimations: 1\n  closeOnGamepadB: 1\n  blockUIBehind: 1\n"
            f"  ModalType: {REPLAYS_TYPE}\n  windowAnimator: {ref(ctx.c('root', 'anim'))}\n  screenSwitcher: {{fileID: 0}}\n"
            + words
            + f"  localList: {ref(ctx.rt(f'{COL}/Local/List'))}\n  localSection: {ref(ctx.c(f'{COL}/Local', 'cg'))}\n"
            f"  otherList: {ref(ctx.rt(f'{COL}/Other/List'))}\n  otherSection: {ref(ctx.c(f'{COL}/Other', 'cg'))}\n"
            f"  emptyState: {ref(ctx.c(f'{COL}/Empty', 'cg'))}\n  rowPrefab: {prefab_ref(ROW, 'root#row')}\n"
            f"  statusText: {ref(ctx.c('Window/Status', 'tmp'))}\n  statusGroup: {ref(ctx.c('Window/Status', 'cg'))}\n"
            f"  playbackHudPrefab: {prefab_ref(HUD, 'root#hud')}\n  windowGroup: {ref(ctx.c('Window', 'cg'))}\n"
            f"  scroll: {ref(ctx.c('Window/Scroll', 'scroll'))}\n  stickScrollSpeed: 1400\n  stickDeadZone: 0.2\n")


def prefab_outputs():
    roots = {ROW: build_row(), HUD: build_hud(), BROWSER: build_browser()}
    out = {}
    for name in PREFABS:
        out[prefab_path(name)] = P.prefab_text(roots[name], name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=asset_guid(name))
    return out


# --------------------------------------------------------------------------------------------
# Menu_Main: the instance, wired and registered, just before Credits
# --------------------------------------------------------------------------------------------

def scene_ctx():
    return P.Ctx("Menu_Main/ReplayBrowserModal")


def scene_ids():
    ctx = scene_ctx()
    inst, stub = str(ctx.fid("Replays#inst")), str(ctx.fid("Replays#stub"))
    comp = str(C.derived(F(BROWSER)("root#browser"), inst))
    return inst, stub, comp


def list_ids(doc, field):
    m = re.search(r"^  " + field + r":\n((?:  - .*\n)*)", doc, re.M)
    return re.findall(r"^  - \{fileID: (-?\d+)\}", m.group(1), re.M) if m else []


def scene_transform(text_):
    sc = T.Scene(text_)
    I = CR.SCENE_IDS
    assert sc.field(I["modal_windows_go"], "m_Name") == "ModalWindows", "Menu_Main's ModalWindows moved"
    assert sc.rect_of(I["modal_windows_go"]) == I["modal_windows_rt"], "ModalWindows' RectTransform moved"
    assert sc.script(I["switcher"]) == S["switcher"], "Menu_Main's ScreenSwitcher moved"
    inst, stub, comp = scene_ids()
    for fid in (inst, stub, comp):
        if sc.has(fid):
            sc.remove(fid)
    out = []
    mods = [("root#browser", "screenSwitcher", None, ref(I["switcher"]))]
    K.emit_instance(scene_ctx(), K.Instance("Replays", BROWSER, asset_guid(BROWSER), BROWSER, STRETCH, mods),
                    I["modal_windows_rt"], out)
    out.append(T.stub_mono(comp, F(BROWSER)("root#browser"), asset_guid(BROWSER), inst, S["browser"]))

    _, credits_stub, credits_comp = CR.scene_ids()
    mw = sc.docs[I["modal_windows_rt"]]
    kids = [k for k in list_ids(mw, "m_Children") if k != stub]
    at = kids.index(credits_stub) if credits_stub in kids else len(kids)
    anchor = CR.last_doc_of(sc, kids[at - 1]) if at > 0 else I["modal_windows_rt"]
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(I["modal_windows_rt"], T.set_block(mw, "m_Children", K.refs_list("m_Children", kids[:at] + [stub] + kids[at:])))

    sw = sc.docs[I["switcher"]]
    modals = [m for m in list_ids(sw, "Modals") if m != comp]
    at = modals.index(credits_comp) if credits_comp in modals else len(modals)
    sc.replace(I["switcher"], T.set_block(sw, "Modals", K.refs_list("Modals", modals[:at] + [comp] + modals[at:])))
    return sc.text()


def outputs():
    files = prefab_outputs()
    files[SCENE] = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return files


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def theater_shot_count():
    src = open(os.path.join(REPO, "Assets/_Scripts/Utility/Theater/TheaterPlayback.cs"), encoding="utf-8").read()
    body = re.search(r"public enum TheaterShot\s*\{(.*?)\}", src, re.S).group(1)
    return len(re.findall(r"^\s*([A-Z]\w*)\s*,?\s*$", re.sub(r"///.*", "", body), re.M))


def words_of(comp, keys):
    return {k: re.search(r"^  " + k + r": (.*)$", comp, re.M) for k in keys}


def validate(files):
    guids = T.known_guids() | PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    for name in PREFABS:
        A.check_file(prefab_path(name), files[prefab_path(name)], guids, minted)

    # The row.
    rf = files[prefab_path(ROW)]
    _, row = A.doc_of(rf, F(ROW)("root#row"))
    assert row and S["row"] in row, "ReplayRow.prefab: the root carries no ReplayRowView"
    for field in ("title", "when", "result", "detail", "deleteText"):
        C.wired(rf, row, field, K.TMP_UGUI, "ReplayRowView")
    for field in ("watchButton", "deleteButton"):
        C.wired(rf, row, field, S["button"], "ReplayRowView")
    for field in ("actions", "elsewhere"):
        C.wired(rf, row, field, 225, "ReplayRowView")
    assert re.search(r"^  confirmDeleteLabel: \S", row, re.M), "ReplayRowView.confirmDeleteLabel is empty"
    _, lay = A.doc_of(rf, F(ROW)("root#le"))
    assert f"  m_PreferredHeight: {ROW_H}\n" in lay, "the row must say its height, or the list cannot lay it out"

    # The HUD.
    hf = files[prefab_path(HUD)]
    _, hud = A.doc_of(hf, F(HUD)("root#hud"))
    assert hud and S["hud"] in hud, "ReplayPlaybackHud.prefab: the root carries no ReplayPlaybackHud"
    cls, canvas = A.doc_of(hf, F(HUD)("root#canvas"))
    assert cls == 223 and "  m_RenderMode: 0\n" in canvas, "the HUD needs its own screen-space overlay ROOT canvas"
    assert f"  m_SortingOrder: {HUD_SORTING}\n" in canvas, "the HUD canvas must draw over the menu"
    assert K.GRAPHIC_RAYCASTER in A.doc_of(hf, F(HUD)("root#raycaster"))[1], "the HUD canvas cannot be clicked without a raycaster"
    for _, _, field in TRANSPORT:
        C.wired(hf, hud, field, S["button"], "ReplayPlaybackHud")
    C.wired(hf, hud, "exitButton", S["button"], "ReplayPlaybackHud")
    C.wired(hf, hud, "scrub", SLIDER, "ReplayPlaybackHud")
    C.wired(hf, hud, "followGroup", 225, "ReplayPlaybackHud")
    for field in ("loadingGroup", "controlsGroup"):
        C.wired(hf, hud, field, 225, "ReplayPlaybackHud")
    C.wired(hf, hud, "cancelButton", S["button"], "ReplayPlaybackHud")
    assert "  m_Alpha: 1\n" in A.doc_of(hf, F(HUD)("Loading#cg"))[1] and "  m_Alpha: 0\n" in A.doc_of(hf, F(HUD)("Controls#cg"))[1], \
        "the HUD must open on its loading panel (the arena builds before anything plays)"
    for field in ("titleText", "whenText", "timeText", "speedText", "shotText", "followText", "pauseText"):
        C.wired(hf, hud, field, K.TMP_UGUI, "ReplayPlaybackHud")
    for k, m in words_of(hud, HUD_WORDS).items():
        assert m and m.group(1).strip(), f"ReplayPlaybackHud.{k} is empty"
    shots = re.findall(r"^  - (.+)$", hud.split("  shotNames:\n")[1].split("  titleText:")[0], re.M)
    assert len(shots) == theater_shot_count(), f"ReplayPlaybackHud.shotNames has {len(shots)} names; TheaterShot has {theater_shot_count()}"
    for node in [f"Bottom/Transport/{key}" for key, _, _ in TRANSPORT] + ["Top/Exit", "Loading/Cancel"]:
        _, b = A.doc_of(hf, F(HUD)(f"{node}#button"))
        assert "    m_Mode: 0\n" in b, f"HUD button {node} is a navigation stop - the D-pad belongs to the replay"

    # The window.
    pf = files[prefab_path(BROWSER)]
    _, comp = A.doc_of(pf, F(BROWSER)("root#browser"))
    assert comp and S["browser"] in comp, "ReplayBrowserModal.prefab: the root carries no ReplayBrowserModal"
    assert f"  ModalType: {REPLAYS_TYPE}\n" in comp, "ReplayBrowserModal must register as ModalWindows.REPLAYS (19)"
    for field in ("localList", "otherList"):
        C.wired(pf, comp, field, 224, "ReplayBrowserModal")
    for field in ("localSection", "otherSection", "emptyState", "statusGroup", "windowGroup"):
        C.wired(pf, comp, field, 225, "ReplayBrowserModal")
    C.wired(pf, comp, "statusText", K.TMP_UGUI, "ReplayBrowserModal")
    C.wired(pf, comp, "scroll", A.SCROLL_RECT, "ReplayBrowserModal")
    assert f"  rowPrefab: {prefab_ref(ROW, 'root#row')}\n" in comp, "ReplayBrowserModal.rowPrefab does not name the row"
    assert f"  playbackHudPrefab: {prefab_ref(HUD, 'root#hud')}\n" in comp, "ReplayBrowserModal.playbackHudPrefab does not name the HUD"
    for k, m in words_of(comp, BROWSER_WORDS).items():
        assert m and m.group(1).strip(), f"ReplayBrowserModal.{k} is empty"
    _, anim = A.doc_of(pf, F(BROWSER)("root#anim"))
    assert CR.STANDARD_CONTROLLER in anim, "the root's Animator must run Standard.controller"
    assert re.search(r"^  m_Name: Content$", pf, re.M), "Standard.controller animates a child named Content"
    _, scroll = A.doc_of(pf, F(BROWSER)("Window/Scroll#scroll"))
    assert f"  m_Content: {{fileID: {F(BROWSER)(COL + '#rt')}}}\n" in scroll, "ScrollRect.m_Content is not the list column"
    C.wired(pf, scroll, "m_VerticalScrollbar", SCROLLBAR, "ScrollRect")

    # The scene.
    sc_text = files[SCENE]
    inst, stub, comp_fid = scene_ids()
    assert sc_text.count(f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid(BROWSER)}") == 1, \
        "Menu_Main must hold exactly one ReplayBrowserModal instance"
    sc = T.Scene(sc_text)
    I = CR.SCENE_IDS
    kids = list_ids(sc.docs[I["modal_windows_rt"]], "m_Children")
    assert stub in kids, "the ReplayBrowserModal is not a child of ModalWindows"
    _, credits_stub, credits_comp = CR.scene_ids()
    if credits_stub in kids:
        assert kids.index(stub) == kids.index(credits_stub) - 1, "the ReplayBrowserModal must sit just before Credits"
    assert f"m_TransformParent: {{fileID: {I['modal_windows_rt']}}}" in sc.docs[inst], "the ReplayBrowserModal is not under ModalWindows"
    modals = list_ids(sc.docs[I["switcher"]], "Modals")
    assert modals.count(comp_fid) == 1, "ScreenSwitcher.Modals does not list the ReplayBrowserModal once"
    assert sc.has(comp_fid) and asset_guid(BROWSER) in sc.docs[comp_fid], "the ReplayBrowserModal stub is missing"
    assert re.search(r"propertyPath: screenSwitcher\n      value: \n      objectReference: \{fileID: %s\}" % I["switcher"],
                     sc.docs[inst]), "the ReplayBrowserModal instance is not wired to the ScreenSwitcher"
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

    pf, hf, rf = prefab_path(BROWSER), prefab_path(HUD), prefab_path(ROW)
    _, _, comp = scene_ids()
    pause = F(HUD)("Bottom/Transport/Pause#button")

    def nav_on(f):
        t = f[hf]
        i = t.index(f"--- !u!114 &{pause}\n")
        f[hf] = t[:i] + t[i:].replace("    m_Mode: 0\n", "    m_Mode: 3\n", 1)

    cases = [
        ("the window left out of ScreenSwitcher.Modals", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  - {{fileID: {comp}}}\n", "", 1))),
        ("the wrong modal type", lambda f: f.__setitem__(pf, f[pf].replace(f"  ModalType: {REPLAYS_TYPE}\n", "  ModalType: 18\n"))),
        ("rowPrefab unwired", lambda f: f.__setitem__(pf, re.sub(r"(  rowPrefab: )\{[^}]*\}", r"\1{fileID: 0}", f[pf], count=1))),
        ("an empty authored line", lambda f: f.__setitem__(pf, re.sub(r"^(  noResultLine: ).*$", r"\1", f[pf], count=1, flags=re.M))),
        ("a non-ASCII word", lambda f: f.__setitem__(rf, f[rf].replace("Recorded on another device", "Recorded · elsewhere"))),
        ("a shot without a name", lambda f: f.__setitem__(hf, f[hf].replace("  - CHASE\n", "", 1))),
        ("a HUD button the D-pad can land on", nav_on),
        ("the HUD opening on its transport instead of the loading panel", lambda f: f.__setitem__(hf, f[hf].replace(
            f"--- !u!225 &{F(HUD)('Loading#cg')}\nCanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {F(HUD)('Loading#go')}}}\n"
            "  m_Enabled: 1\n  m_Alpha: 1\n",
            f"--- !u!225 &{F(HUD)('Loading#cg')}\nCanvasGroup:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {F(HUD)('Loading#go')}}}\n"
            "  m_Enabled: 1\n  m_Alpha: 0\n"))),
        ("the HUD drawn under the menu", lambda f: f.__setitem__(hf, f[hf].replace(f"  m_SortingOrder: {HUD_SORTING}\n", "  m_SortingOrder: 0\n"))),
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
    print("OK: ReplayBrowserModal, ReplayRow, ReplayPlaybackHud and the Menu_Main instance validate")
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
