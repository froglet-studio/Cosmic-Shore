#!/usr/bin/env python3
"""
Author ITEM 9 of the style-B menu redesign - the SETTINGS window - as a prefab, put it inside the
shared SettingsModal.prefab, and prune the overrides the change orphans (Docs/MENU_REDESIGN_B.md
section 18; the approved Settings / SettingsOther boards, revision 2, on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Settings/
        SettingsView.prefab     the whole window: the dim, the frame and brackets, "Settings" + the active
                                tab's name, the five hex tabs, Close (B), four pages of rows (label, one-
                                line description, and a dropdown, an ON / OFF pair or a slider with its
                                value), the General links, the Performance notices and buttons, the version,
                                and (Rev 9, section 54) the ACCOUNT page - the pilot, the guest notice, a row
                                per sign-in provider with LINK, Sign out and Delete account - with its window
                                (the link conflict and the confirmations) over the settings.
                                GameSettingsPanelController, SettingsTabBar and BenchmarkSceneLauncher ride
                                on its root.

    Assets/_Prefabs/UI Elements/Panels/SettingsModal.prefab (shared: Menu_Main, and every game scene
    through R_Pause_Menu_Panel inside GameCanvas):
        Content's old Border chrome and the nested OptionsMenuContent instance (with the three components
        added to it) are replaced by one SettingsView instance; Content stretches. The SettingsModal
        component on the root keeps its fileID, so every opener and ScreenSwitcher.Modals still resolve.

    Assets/_Scenes/Menu_Main.unity and Assets/_Prefabs/CORE/GameCanvas.prefab:
        Every override, removed-object entry and stripped stub that addressed an object the rebuild
        retired is pruned - exactly the set (old object set - new object set) of each instance, computed
        through the nesting - plus the old 1920 x 1080 size overrides on Content, which stretches now.
        Nothing else in either file is touched.

STANDING DOWN: the prefab and the two pruned files are re-derived on every run; once pruned, a re-run
finds nothing to prune and the outputs are unchanged (--check).

    python3 Tools/Build/author_menu_settings.py              # write
    python3 Tools/Build/author_menu_settings.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_settings.py --self-test  # planted defects must all be caught
"""

import argparse
import functools
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
GAMECANVAS = "Assets/_Prefabs/CORE/GameCanvas.prefab"
MODAL_PREFAB = "Assets/_Prefabs/UI Elements/Panels/SettingsModal.prefab"
OUT_DIR = "Assets/_Prefabs/UI Elements/Main Menu Screens/Settings"
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, refs = C.TL, C.TR, C.ref, C.mono, C.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node, ring_node = A.image_node, A.art_node, A.ring_node
c = A.c
MASK = (1 << 63) - 1

VIEW = "SettingsView"
MODAL_GUID = "0b36fa278f9a30d4c8f44e846674c904"        # SettingsModal.prefab
OPTIONS_GUID = "2d8c7d12373dc3049ba919b74e17f4cb"      # OptionsMenuContent.prefab (no longer nested)
DROPDOWN = "7b743370ac3e4ec2a1668f5455a8ef8a"          # TMPro.TMP_Dropdown
TOGGLE = "9085046f02f69544eb97fd06b6048fe2"            # UnityEngine.UI.Toggle
SCROLL = "1aa08ab6e0800fa44ae55d278d1423e3"            # UnityEngine.UI.ScrollRect
SLIDER = P.SLIDER
S = dict(controller=C.script("GameSettingsPanelController"), tabbar=C.script("SettingsTabBar"),
         bench=C.script("BenchmarkSceneLauncher"), tab=K.kit_script("KitTextTab"), tile=K.kit_script("KitTile"),
         button=K.kit_script("KitButton"), close=C.script("ModalWindowCloseButton"), modal=C.script("SettingsModal"),
         account=C.script("AccountSettingsView"), prompt=C.script("AccountPromptWindow"))
# Rev 9 (section 54): the Account tab. SignInProviders.asset is written by author_start_flow.py (PROVIDERS_GUID there).
PROVIDERS_GUID = "5b0c1f7e9a2d4c6e8f1a3b5d7c9e0f21"
ACCOUNT_ROWS = (("Steam", 2, None), ("Google", 3, None), ("Apple", 4, None))   # marks come from SignInProviders.asset (R9b)
ACCOUNT_ROWS_TOP, ACCOUNT_PITCH, ACCOUNT_ACTIONS_GAP = 34, 84, 26
PROMPT_W, PROMPT_H, PROMPT_CONFIRM_H = 1180, 680, 330
PACKAGE = P.PACKAGE | {DROPDOWN, TOGGLE, SCROLL}

# The rows, as the approved board draws them (revision 2: a one-line description under every label).
# kind: dd (dropdown) / tg (ON-OFF pair) / sl (slider + value); field: the controller field it fills.
PAGES = [
    ("General", "chrome_gear", [
        ("dd", "Colorblind mode", "Adjusts team colours for colour vision deficiency.", "colorblindDropdown"),
        ("tg", "Subtitles", "Captions for spoken lines.", "subtitles"),
        ("dd", "Subtitle size", "How large the captions are.", "subtitleScaleDropdown"),
        ("tg", "Analytics and crash reports", "Share anonymous usage and crash reports so bugs get fixed.", "analyticsConsent"),
    ]),
    ("Display", "chrome_display", [
        ("dd", "Display mode", "Fullscreen, a borderless window, or a window.", "displayModeDropdown"),
        ("dd", "Resolution", "Native follows your monitor.", "resolutionDropdown"),
        ("dd", "Frame rate limit", "Caps how many frames are drawn each second.", "frameCapDropdown"),
        ("tg", "V-Sync", "Syncs frames to the monitor to stop tearing.", "vsync"),
        ("sl", "Field of view", "Wider shows more around your vessel.", "fovSlider"),
    ]),
    ("Performance", "chrome_gauge", [
        ("dd", "Quality preset", "Sets every option below in one step.", "qualityDropdown"),
        ("dd", "Anti-aliasing", "Smooths jagged edges.", "antiAliasingDropdown"),
        ("dd", "Texture quality", "Lower saves video memory.", "textureQualityDropdown"),
        ("dd", "Upscaling", "Draws fewer pixels and scales up for speed.", "upscalingDropdown"),
        ("dd", "Adaptive performance", "Lowers detail when the device runs hot.", "adaptivePerformanceDropdown"),
        ("dd", "Physics / collider detail", "Lower uses simpler collision shapes.", "physicsDetailDropdown"),
    ]),
    ("Other", "chrome_sliders", [
        ("tg", "Invert Y", "Pull back to climb.", "invertY"),
        ("tg", "Invert throttle", "Swap which way the throttle runs.", "invertThrottle"),
        ("tg", "Music", "Play the soundtrack.", "music"),
        ("sl", "Music volume", "How loud the soundtrack is.", "musicSlider"),
        ("tg", "SFX", "Play sound effects.", "sfx"),
        ("sl", "SFX volume", "How loud the effects are.", "sfxSlider"),
        ("tg", "Haptics", "Controller rumble.", "haptics"),
    ]),
    ("Account", "chrome_person", []),
]
READOUTS = {"fovSlider": "fovValueText", "musicSlider": "musicValueText", "sfxSlider": "sfxValueText"}
LINKS = (("BugReport", "BUG REPORT", "Default", "bugReportButton"), ("Privacy", "PRIVACY POLICY", "Default", "privacyPolicyButton"),
         ("DeleteData", "DELETE MY DATA", "Danger", "deleteDataButton"), ("Credits", "CREDITS", "Default", "creditsButton"),
         ("Quit", "QUIT GAME", "Cancel", "quitGameButton"))
PERF_BUTTONS = (("AutoDetect", "AUTO DETECT", "autoDetectButton"), ("Benchmark", "RUN BENCHMARK", "benchmarkButton"))
WIN_W, WIN_H, PAD, ROW_H, CTRL_X = 1600, 900, 48, 84, 970
PAGE_W = WIN_W - 2 * PAD


def asset_guid(name):
    return P.asset_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


def col(token, alpha=1.0):
    return K.col(tok(token, alpha))


def tinted(normal, highlighted, pressed, selected, disabled, nav=3):
    """Selectable fields with a colour-tint transition (the colours are theme token values)."""
    return K.SELECTABLE_FIELDS.replace("    m_Mode: 3\n", f"    m_Mode: {nav}\n").replace(
        "  m_Transition: 0\n", "  m_Transition: 1\n").replace(
        "    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n", f"    m_NormalColor: {normal}\n").replace(
        "    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n", f"    m_HighlightedColor: {highlighted}\n").replace(
        "    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}\n", f"    m_PressedColor: {pressed}\n").replace(
        "    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}\n", f"    m_SelectedColor: {selected}\n").replace(
        "    m_DisabledColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}\n", f"    m_DisabledColor: {disabled}\n")


# The control states, as tint colours over a white graphic: rest SurfaceLight, hover TextLight, press
# ControlActive, gamepad focus CTA, disabled SurfaceDark at half.
CONTROL_TINT = (col("SurfaceLight"), col("TextLight"), col("ControlActive"), col("Cta"), col("SurfaceDark", 0.5))


# --------------------------------------------------------------------------------------------
# Controls
# --------------------------------------------------------------------------------------------

def dropdown(row, k):
    d = row.child(N(f"{k}/Dropdown", "Dropdown", TL(CTRL_X, 16, 440, 52)))
    d.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bord = d.child(N(f"{k}/Dropdown/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_sliver10_line1"), True))
    cap = d.child(N(f"{k}/Dropdown/Label", "Label", R((0, 0), (1, 1), (-14, 0), (-64, 0))))
    cap.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Option", "Body", tok("TextLight")))) \
        .add("kt", 114, K.kit_text("Body", "TextLight"))
    image_node(d, f"{k}/Dropdown/Arrow", "Arrow", R((1, 0.5), None, (-20, 0), (16, 16), (1, 0.5)), "NeutralLightest",
               K.chrome_sprite("chrome_arrow_down"), preserve=True)
    # The list: TMP_Dropdown clones Template (inactive) under its own canvas on open.
    tp = d.child(N(f"{k}/Dropdown/Template", "Template", R((0, 0), (1, 0), (0, -4), (0, 240), (0.5, 1)), active=False))
    tp.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True, raycast=True)) \
        .add("scroll", 114, lambda ctx, go: mono(go, SCROLL,
             f"  m_Content: {ref(ctx.rt(f'{k}/Dropdown/Template/Viewport/Content'))}\n  m_Horizontal: 0\n  m_Vertical: 1\n"
             "  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n"
             f"  m_ScrollSensitivity: 20\n  m_Viewport: {ref(ctx.rt(f'{k}/Dropdown/Template/Viewport'))}\n"
             "  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n"
             "  m_HorizontalScrollbarVisibility: 2\n  m_VerticalScrollbarVisibility: 2\n"
             "  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n"
             "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    tb = tp.child(N(f"{k}/Dropdown/Template/Border", "Border", STRETCH))
    tb.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver10_line1"), True))
    vp = tp.child(N(f"{k}/Dropdown/Template/Viewport", "Viewport", R((0, 0), (1, 1), (0, 0), (-8, -8))))
    vp.add("mask", 114, A.rect_mask())
    ct = vp.child(N(f"{k}/Dropdown/Template/Viewport/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 46), (0.5, 1))))
    it = ct.child(N(f"{k}/Dropdown/Template/Viewport/Content/Item", "Item", R((0, 0.5), (1, 0.5), (0, 0), (0, 46))))
    ib = it.child(N(f"{k}/Dropdown/Template/Viewport/Content/Item/Background", "Item Background", STRETCH))
    ib.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive", 0.35), raycast=True))
    image_node(it, f"{k}/Dropdown/Template/Viewport/Content/Item/Checkmark", "Item Checkmark",
               R((0, 0.5), None, (14, 0), (18, 18), (0, 0.5)), "Cta", K.chrome_sprite("chrome_confirm"), preserve=True)
    il = it.child(N(f"{k}/Dropdown/Template/Viewport/Content/Item/Label", "Item Label", R((0, 0), (1, 1), (10, 0), (-60, 0))))
    il.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(text("Option", "Body", tok("TextLight"))))
    clear, half = "{r: 1, g: 1, b: 1, a: 0}", "{r: 1, g: 1, b: 1, a: 1}"
    it.add("toggle", 114, lambda ctx, go: mono(go, TOGGLE,
           tinted(clear, half, half, half, clear)
           + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Dropdown/Template/Viewport/Content/Item/Background', 'img'))}\n"
           "  toggleTransition: 1\n"
           f"  graphic: {ref(ctx.c(f'{k}/Dropdown/Template/Viewport/Content/Item/Checkmark', 'img'))}\n"
           "  m_Group: {fileID: 0}\n  onValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_IsOn: 1\n"))
    d.add("dd", 114, lambda ctx, go: mono(go, DROPDOWN,
          tinted(*CONTROL_TINT) + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Dropdown/Border', 'img'))}\n"
          f"  m_Template: {ref(ctx.rt(f'{k}/Dropdown/Template'))}\n"
          f"  m_CaptionText: {ref(ctx.c(f'{k}/Dropdown/Label', 'tmp'))}\n  m_CaptionImage: {{fileID: 0}}\n"
          "  m_Placeholder: {fileID: 0}\n"
          f"  m_ItemText: {ref(ctx.c(f'{k}/Dropdown/Template/Viewport/Content/Item/Label', 'tmp'))}\n"
          "  m_ItemImage: {fileID: 0}\n  m_Value: 0\n  m_MultiSelect: 0\n  m_Options:\n    m_Options: []\n"
          "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n  m_AlphaFadeSpeed: 0.15\n"))
    d.add("audio", 114, A.menu_audio(11))
    return f"{k}/Dropdown"


def word_tab(row, k, key, word, x):
    kk = f"{k}/{key}"
    w = K.label_width(word, "Button")
    n = row.child(N(kk, key, TL(x, 22, w, 40)))
    ring_node(n, f"{kk}/FocusRing", 10)
    hit = n.child(N(f"{kk}/Hit", "Hit", STRETCH))
    hit.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    lab = n.child(N(f"{kk}/Label", "Label", R((0, 1), (1, 1), (0, 0), (0, K.line_height("Button")), (0.5, 1))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(word, "Button", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("Button", "NeutralLightest", driven=True))
    und = n.child(N(f"{kk}/Underline", "Underline", R((0, 0), (1, 0), (0, 4), (0, 2), (0.5, 0))))
    und.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive", 0.0)))
    n.add("tab", 114, lambda ctx, go: mono(go, S["tab"],
          A.selectable() + f"  m_TargetGraphic: {ref(ctx.c(f'{kk}/Hit', 'img'))}\n"
          + A.calls([(ctx.c(kk, "audio"), "CosmicShore.UI.MenuAudio", "PlayAudio")])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{kk}/FocusRing', 'img'))}\n"
          "  isSelected: 0\n"
          f"  label: {ref(ctx.c(f'{kk}/Label', 'tmp'))}\n  underline: {ref(ctx.c(f'{kk}/Underline', 'img'))}\n"))
    n.add("audio", 114, A.menu_audio(11))
    return kk, x + w + 36


def slider(row, k):
    s = row.child(N(f"{k}/Slider", "Slider", TL(CTRL_X, 29, 400, 26)))
    image_node(s, f"{k}/Slider/Track", "Track", R((0, 0.5), (1, 0.5), (0, 0), (0, 4)), "SurfaceDark")
    fa = s.child(N(f"{k}/Slider/FillArea", "Fill Area", R((0, 0.5), (1, 0.5), (0, 0), (-30, 4))))
    fill = fa.child(N(f"{k}/Slider/FillArea/Fill", "Fill", R((0, 0), (0, 1), (0, 0), (15, 0), (0, 0.5))))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"))).add("th", 114, H.themed("ControlActive"))
    ha = s.child(N(f"{k}/Slider/HandleArea", "Handle Slide Area", R((0, 0), (1, 1), (0, 0), (-30, 0))))
    hd = ha.child(N(f"{k}/Slider/HandleArea/Handle", "Handle", R((0, 0), (0, 1), (0, 0), (30, 0))))
    hd.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_hex_handle"), raycast=True, preserve=True))
    s.add("slider", 114, lambda ctx, go: mono(go, SLIDER,
          tinted(col("TextLight"), col("ControlActive"), col("ControlActive"), col("Cta"), col("SurfaceDark", 0.5))
          + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Slider/HandleArea/Handle', 'img'))}\n"
          f"  m_FillRect: {ref(ctx.rt(f'{k}/Slider/FillArea/Fill'))}\n"
          f"  m_HandleRect: {ref(ctx.rt(f'{k}/Slider/HandleArea/Handle'))}\n"
          "  m_Direction: 0\n  m_MinValue: 0\n  m_MaxValue: 1\n  m_WholeNumbers: 0\n  m_Value: 0.7\n"
          "  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n"))
    v = row.child(N(f"{k}/Value", "Value", TL(CTRL_X + 420, 22, 64, 40)))
    label(v, "70", "Data", "TextLight", align="right")
    return f"{k}/Slider", f"{k}/Value"


def row_node(page, pk, i, kind, caption, desc, field, top):
    k = f"{pk}/Row{i + 1}"
    r = page.child(N(k, f"Row{i + 1}", TL(0, top + i * ROW_H, PAGE_W, ROW_H)))
    # A transparent raycast plate: SettingsRowDropdownHitArea makes the whole row open its dropdown.
    r.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    r.add("in", 114, M.entrance("Rise", by_sibling=True, distance=10))
    r.add("cg", 225, canvas_group(True))
    label(r.child(N(f"{k}/Label", "Label", TL(0, 14, 900, 26))), caption, "Body", "TextLight")
    label(r.child(N(f"{k}/Desc", "Desc", TL(0, 44, 900, 20))), desc, "BodySmall", "NeutralLightest")
    image_node(r, f"{k}/Rule", "Rule", R((0, 0), (1, 0), (0, 0), (0, 1), (0.5, 0)), "SurfaceDark")
    wiring = {}
    if kind == "dd":
        wiring[field] = ("dd", dropdown(r, k))
    elif kind == "tg":
        on, x = word_tab(r, k, "On", "ON", CTRL_X)
        off, _ = word_tab(r, k, "Off", "OFF", x)
        wiring[field] = ("tg", (on, off))
    else:
        sk, vk = slider(r, k)
        wiring[field] = ("sl", sk)
        wiring[READOUTS[field]] = ("tmp", vk)
    return wiring


def notice(page, key, name, line, token):
    n = page.child(N(key, name, TL(0, 0, PAGE_W, 44), active=False))
    H.framed(n, key, 10, "SurfaceVeryDark", token)
    label(n.child(N(f"{key}/Text", "Text", TL(18, 0, PAGE_W - 36, 44))), line, "Body", "TextLight")


def small_buttons(page, key, top, items):
    row = page.child(N(key, key.split("/")[-1], TL(0, top, PAGE_W, 40)))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 3, (0, 0), (0, 0)))
    for k, cap, variant in items:
        w, h = K.button_size(cap, small=True)
        holder = row.child(N(f"{key}/{k}", k, R((0, 0.5), None, (0, 0), (w, h), (0, 0.5))))
        holder.add("le", 114, le(pw=w, ph=h))
        holder.children.append(P.Inst(f"{k}Button", "KitButtonSmall", f"{k}Button", STRETCH,
                                      K.button_mods("KitButtonSmall", variant, False, cap, small=True)))


def account_page(page, pk):
    """Rev 9 (section 54, the approved R9Account board): who you are, the guest notice, the linked accounts and the
    account actions. AccountSettingsView lays the rows out (rows the platform does not offer collapse)."""
    label(page.child(N(f"{pk}/Name", "PilotName", TL(0, 0, 900, 40))), "", "H2", "TextLight")
    label(page.child(N(f"{pk}/Id", "PlayerId", TL(0, 46, 900, 20))), "", "Label", "NeutralLightest")
    g = page.child(N(f"{pk}/Guest", "GuestNotice", TL(0, 84, PAGE_W, 56)))
    H.framed(g, f"{pk}/Guest", 10, "SurfaceVeryDark", "Danger")
    ic = g.child(N(f"{pk}/Guest/Icon", "Icon", R((0, 0.5), None, (32, 0), (24, 24))))
    ic.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Danger"), K.chrome_sprite("chrome_person"), preserve=True))
    label(g.child(N(f"{pk}/Guest/Text", "Text", TL(60, 0, PAGE_W - 80, 56))),
          "Guest pilot. Your progress lives on this device. Link an account to keep it if you reinstall or change devices.",
          "Body", "TextLight")
    label(page.child(N(f"{pk}/Caption", "LinkedCaption", TL(0, 164, 600, 18))), "Linked accounts", "Label", "NeutralLightest")
    rows = page.child(N(f"{pk}/Rows", "Rows", TL(0, 170, PAGE_W, 480)))
    for i, (name, _, _) in enumerate(ACCOUNT_ROWS):
        rk = f"{pk}/Rows/{name}"
        r = rows.child(N(rk, name, TL(0, ACCOUNT_ROWS_TOP + i * ACCOUNT_PITCH, PAGE_W, ACCOUNT_PITCH)))
        # R9b (section 57): the mark is the provider's own logo-only button, untinted, set from SignInProviders.asset at
        # runtime; none is shipped here, and Steam's stays empty (Valve allows only its approved artwork).
        mk = r.child(N(f"{rk}/Mark", "Mark", TL(0, 22, 40, 40)))
        img = K.image((1, 1, 1, 1), None, preserve=True)
        mk.add("cr", 222, K.canvas_renderer()).add("img", 114, lambda ctx, go, img=img: img(ctx, go).replace("  m_Enabled: 1\n", "  m_Enabled: 0\n", 1))
        label(r.child(N(f"{rk}/Name", "Name", TL(56, 14, 600, 26))), name, "Body", "TextLight")
        label(r.child(N(f"{rk}/Line", "Line", TL(56, 44, 600, 20))), "Not linked", "BodySmall", "NeutralLightest")
        image_node(r, f"{rk}/Rule", "Rule", R((0, 0), (1, 0), (0, 0), (0, 1), (0.5, 0)), "SurfaceDark")
        w, h = K.button_size("LINK", small=True)
        holder = r.child(N(f"{rk}/Link", "Link", R((1, 0.5), None, (0, 0), (w, h), (1, 0.5))))
        holder.children.append(P.Inst(f"Account{name}LinkButton", "KitButtonSmall", "LinkButton", STRETCH,
                                      K.button_mods("KitButtonSmall", "Default", False, "LINK", small=True)))
        label(r.child(N(f"{rk}/Linked", "Linked", R((1, 0.5), None, (0, 0), (200, 24), (1, 0.5)), active=False)),
              "Linked", "Label", "Cta", align="right")
    act = rows.child(N(f"{pk}/Rows/Actions", "Actions",
                       TL(0, ACCOUNT_ROWS_TOP + len(ACCOUNT_ROWS) * ACCOUNT_PITCH + ACCOUNT_ACTIONS_GAP, PAGE_W, 40)))
    for k, cap, variant, anchor in (("SignOut", "SIGN OUT", "Cancel", 0), ("Delete", "DELETE ACCOUNT", "Danger", 1)):
        w, h = K.button_size(cap, small=True)
        holder = act.child(N(f"{pk}/Rows/Actions/{k}", k, R((anchor, 0.5), None, (0, 0), (w, h), (anchor, 0.5))))
        holder.children.append(P.Inst(f"Account{k}Button", "KitButtonSmall", f"{k}Button", STRETCH,
                                      K.button_mods("KitButtonSmall", variant, False, cap, small=True)))
    page.add("account", 114, lambda ctx, go: mono(go, S["account"], account_body(ctx, pk)))


def account_body(ctx, pk):
    return (f"  providers: {K.ref(11400000, PROVIDERS_GUID, 2)}\n"
            f"  pilotName: {ref(ctx.c(f'{pk}/Name', 'tmp'))}\n"
            f"  playerIdText: {ref(ctx.c(f'{pk}/Id', 'tmp'))}\n"
            f"  guestNotice: {ref(ctx.go(f'{pk}/Guest'))}\n"
            "  rows:\n" + "".join(
                f"  - provider: {v}\n    root: {ref(ctx.rt(f'{pk}/Rows/{n}'))}\n"
                f"    mark: {ref(ctx.c(f'{pk}/Rows/{n}/Mark', 'img'))}\n"
                f"    line: {ref(ctx.c(f'{pk}/Rows/{n}/Line', 'tmp'))}\n"
                f"    linkButton: {ref(ctx.iref(f'Account{n}LinkButton', 'root#button'))}\n"
                f"    linkedMark: {ref(ctx.go(f'{pk}/Rows/{n}/Linked'))}\n" for n, v, _ in ACCOUNT_ROWS)
            + f"  rowsTop: {ACCOUNT_ROWS_TOP}\n  rowPitch: {ACCOUNT_PITCH}\n"
            f"  actionsRow: {ref(ctx.rt(f'{pk}/Rows/Actions'))}\n  actionsGap: {ACCOUNT_ACTIONS_GAP}\n"
            f"  signOutButton: {ref(ctx.iref('AccountSignOutButton', 'root#button'))}\n"
            f"  deleteButton: {ref(ctx.iref('AccountDeleteButton', 'root#button'))}\n"
            f"  prompt: {ref(ctx.c('Prompt', 'prompt'))}\n")


def prompt_node(root):
    """The Account tab's window over the settings (R9Conflict): the link conflict, and - shorter, without the pilot
    cards - the sign-out and delete confirmations. Its foot and buttons hang from its bottom edge."""
    pr = root.child(N("Prompt", "AccountPrompt", STRETCH))
    pr.add("cg", 225, canvas_group(False))
    image_node(pr, "Prompt/Dim", "Dim", STRETCH, "SurfaceBlack", alpha=0.7, raycast=True)
    win = pr.child(N("Prompt/Window", "Window", R((0.5, 0.5), None, (0, 0), (PROMPT_W, PROMPT_H))))
    fill = win.child(N("Prompt/Window/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack"))
    bord = win.child(N("Prompt/Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True)).add("th", 114, H.themed("TextLight"))
    M.bracket_nodes(win, "Prompt/Window/Bracket", inset=14)
    inner = PROMPT_W - 2 * PAD
    label(win.child(N("Prompt/Window/Eyebrow", "Eyebrow", TL(PAD, 40, inner, 18))), "Link Google", "Label", "NeutralLightest")
    label(win.child(N("Prompt/Window/Title", "Title", TL(PAD, 64, inner, 46))), "This Google account already has a pilot", "H2", "TextLight")
    label(win.child(N("Prompt/Window/Body", "Body", TL(PAD, 118, inner, 48))),
          "Pilots cannot be merged. Choose which one to fly; the other stays where it is.", "Body", "NeutralLightest",
          valign="top", wrap=True)
    cards = win.child(N("Prompt/Window/Cards", "Cards", TL(PAD, 180, inner, 280)))
    card_w = (inner - 60) // 2
    for i, side in enumerate(("This", "Other")):
        ck = f"Prompt/Window/Cards/{side}"
        card = cards.child(N(ck, side, TL(i * (card_w + 60), 0, card_w, 280)))
        H.framed(card, ck, 10, "SurfaceVeryDark", "SurfaceLight")
        label(card.child(N(f"{ck}/Caption", "Caption", TL(28, 24, card_w - 56, 18))), "", "Label", "NeutralLightest")
        label(card.child(N(f"{ck}/Name", "Name", TL(28, 52, card_w - 56, 36))), "", "H2", "TextLight")
        for j, (stat, cap) in enumerate((("Crystals", "Crystals"), ("Vessels", "Vessels"), ("Quests", "Quests done"))):
            y = 110 + j * 52
            label(card.child(N(f"{ck}/{stat}Caption", f"{stat}Caption", TL(28, y, 260, 26))), cap, "Body", "NeutralLightest")
            label(card.child(N(f"{ck}/{stat}", stat, TL(28, y, card_w - 56, 26))), "0", "Body", "TextLight", align="right")
            image_node(card, f"{ck}/{stat}Rule", f"{stat}Rule", TL(28, y + 38, card_w - 56, 1), "SurfaceDark")
    label(win.child(N("Prompt/Window/Foot", "Foot", R((0, 0), (1, 0), (0, 118), (-2 * PAD, 40), (0.5, 0)))),
          "", "BodySmall", "NeutralLightest", valign="top", wrap=True)
    row = win.child(N("Prompt/Window/Buttons", "Buttons", R((0, 0), (1, 0), (0, 44), (-2 * PAD, 52), (0.5, 0))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 18, 4, (0, 0), (0, 0)))
    for k, cap, variant in (("Primary", "FLY SHOMBOSS", "Cta"), ("Secondary", "KEEP NOVA RUNNER", "Default"), ("Cancel", "CANCEL", "Cancel")):
        w, h = K.button_size(cap, small=True)
        holder = row.child(N(f"Prompt/Window/Buttons/{k}", k, R(size=(w + 80, h))))
        holder.add("le", 114, le(pw=w + 80, ph=h))
        holder.children.append(P.Inst(f"Prompt{k}Button", "KitButtonSmall", f"{k}Button", STRETCH,
                                      K.button_mods("KitButtonSmall", variant, False, cap, small=True)))
    pr.add("prompt", 114, lambda ctx, go: mono(go, S["prompt"], prompt_body(ctx)))


def prompt_body(ctx):
    def card(side):
        ck = f"Prompt/Window/Cards/{side}"
        return (f"    caption: {ref(ctx.c(f'{ck}/Caption', 'tmp'))}\n    name: {ref(ctx.c(f'{ck}/Name', 'tmp'))}\n"
                f"    crystals: {ref(ctx.c(f'{ck}/Crystals', 'tmp'))}\n    vessels: {ref(ctx.c(f'{ck}/Vessels', 'tmp'))}\n"
                f"    questsDone: {ref(ctx.c(f'{ck}/Quests', 'tmp'))}\n")
    return (f"  group: {ref(ctx.c('Prompt', 'cg'))}\n  window: {ref(ctx.rt('Prompt/Window'))}\n"
            f"  conflictHeight: {PROMPT_H}\n  confirmHeight: {PROMPT_CONFIRM_H}\n"
            + "".join(f"  {f}: {ref(ctx.c(f'Prompt/Window/{k}', 'tmp'))}\n"
                      for f, k in (("eyebrow", "Eyebrow"), ("title", "Title"), ("body", "Body"), ("foot", "Foot")))
            + f"  cards: {ref(ctx.go('Prompt/Window/Cards'))}\n"
            f"  thisPilot:\n{card('This')}  otherPilot:\n{card('Other')}"
            + "".join(f"  {f}Button: {ref(ctx.iref(f'Prompt{k}Button', 'root#button'))}\n"
                      f"  {f}Label: {ref(ctx.iref(f'Prompt{k}Button', 'Label#tmp'))}\n"
                      for f, k in (("primary", "Primary"), ("secondary", "Secondary"), ("cancel", "Cancel"))))


def build_view():
    root = N("root", VIEW, STRETCH)
    image_node(root, "Dim", "Dim", STRETCH, "SurfaceBlack", alpha=0.82, raycast=True)
    win = root.child(N("Window", "Window", R((0.5, 0.5), None, (0, -42), (WIN_W, WIN_H))))
    win.add("cg", 225, canvas_group(True))
    fill = win.child(N("Window/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack"), K.kit_sprite("kit_sliver14_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack"))
    bord = win.child(N("Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True)).add("th", 114, H.themed("TextLight"))
    M.bracket_nodes(win, "Window/Bracket", inset=14)

    wiring = {}
    # Pages first in hierarchy: the switcher's Refocus lands on the open tab's first control.
    for pi, (name, _, rows) in enumerate(PAGES):
        pk = f"Window/{name}"
        page = win.child(N(pk, name, TL(PAD, 150, PAGE_W, 700)))
        page.add("cg", 225, canvas_group(pi == 0))
        top = 56 if name == "Performance" else 0
        if name == "Performance":
            notice(page, f"{pk}/Restart", "RestartNotice", "Some of these changes apply after you restart the game.", "SurfaceLight")
            notice(page, f"{pk}/MenuOnly", "MenuOnlyHint", "Change these from the main menu.", "SurfaceLight")
        for i, (kind, cap, desc, field) in enumerate(rows):
            wiring.update(row_node(page, pk, i, kind, cap, desc, field, top))
        if name == "General":
            small_buttons(page, f"{pk}/Links", len(rows) * ROW_H + 26, [(k, cap, v) for k, cap, v, _ in LINKS])
        if name == "Performance":
            small_buttons(page, f"{pk}/Tools", top + len(rows) * ROW_H + 22, [(k, cap, "Default") for k, cap, _ in PERF_BUTTONS])
        if name == "Account":
            account_page(page, pk)

    label(win.child(N("Window/Eyebrow", "Eyebrow", TL(PAD, 38, 300, 18))), "Settings", "Label", "NeutralLightest")
    t = win.child(N("Window/Title", "Title", TL(PAD, 58, 520, 46)))
    t.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("General", "H1", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("H1", "TextLight", driven=True))
    tabs = win.child(N("Window/Tabs", "Tabs", TL((WIN_W - len(PAGES) * 120) // 2, 26, len(PAGES) * 120, 108)))
    for i, (name, icon, _) in enumerate(PAGES):
        C.tab_node(tabs, i, name, icon, name)
    W.close_button(win)
    v = win.child(N("Window/Version", "Version", R((1, 0), None, (-PAD, 30), (400, 18), (1, 0))))
    label(v, "Version", "Label", "NeutralLightest", align="right")

    prompt_node(root)

    root.add("controller", 114, lambda ctx, go: mono(go, S["controller"], controller_body(ctx, wiring)))
    root.add("tabbar", 114, lambda ctx, go: mono(go, S["tabbar"],
             "  tabs:\n" + "".join(
                 f"  - button: {ref(ctx.c(f'Tabs/{name}', 'tile'))}\n    content: {{fileID: 0}}\n"
                 f"    underline: {{fileID: 0}}\n    label: {{fileID: 0}}\n"
                 f"    page: {ref(ctx.c(f'Window/{name}', 'cg'))}\n    title: {name}\n" for name, _, _ in PAGES)
             + "  defaultTab: 0\n  activeScale: 1.1\n  inactiveScale: 0.95\n  scaleLabels: 0\n"
             f"  titleText: {ref(ctx.c('Window/Title', 'tmp'))}\n"))
    root.add("bench", 114, lambda ctx, go: mono(go, S["bench"], "  intensity: 2\n"))
    return root


def controller_body(ctx, wiring):
    def onoff(field):
        on, off = wiring[field][1]
        return (f"  {field}:\n    onButton: {ref(ctx.c(on, 'tab'))}\n    onUnderline: {{fileID: 0}}\n"
                f"    offButton: {ref(ctx.c(off, 'tab'))}\n    offUnderline: {{fileID: 0}}\n")

    def dd(field):
        return f"  {field}: {ref(ctx.c(wiring[field][1], 'dd'))}\n"

    def sl(field):
        return f"  {field}: {ref(ctx.c(wiring[field][1], 'slider'))}\n"

    def btn(field, key):
        return f"  {field}: {ref(ctx.iref(key + 'Button', 'root#button'))}\n"

    links = {f: k for k, _, _, f in LINKS}
    tools = {f: k for k, _, f in PERF_BUTTONS}
    return ("  optionsMenuContent: {fileID: 0}\n"
            f"  menuOnlyHint: {ref(ctx.go('Window/Performance/MenuOnly'))}\n"
            f"  restartRequiredNotice: {ref(ctx.go('Window/Performance/Restart'))}\n"
            "  selectedColor: {r: 1, g: 1, b: 1, a: 1}\n  unselectedColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}\n"
            "  selectedScale: 1.1\n  unselectedScale: 0.95\n"
            + dd("colorblindDropdown") + onoff("subtitles") + dd("subtitleScaleDropdown") + onoff("analyticsConsent")
            + btn("bugReportButton", links["bugReportButton"]) + btn("privacyPolicyButton", links["privacyPolicyButton"])
            + btn("deleteDataButton", links["deleteDataButton"]) + btn("creditsButton", links["creditsButton"])
            + "  screenSwitcher: {fileID: 0}\n" + btn("quitGameButton", links["quitGameButton"])
            + f"  versionText: {ref(ctx.c('Window/Version', 'tmp'))}\n"
            + dd("displayModeDropdown") + dd("resolutionDropdown") + dd("frameCapDropdown") + onoff("vsync")
            + sl("fovSlider") + "  fovMin: 60\n  fovMax: 90\n"
            + dd("qualityDropdown") + dd("antiAliasingDropdown") + dd("textureQualityDropdown") + dd("upscalingDropdown")
            + dd("adaptivePerformanceDropdown") + dd("physicsDetailDropdown")
            + btn("autoDetectButton", tools["autoDetectButton"]) + btn("benchmarkButton", tools["benchmarkButton"])
            + f"  benchmarkLauncher: {ref(ctx.c('root', 'bench'))}\n"
            + onoff("invertY") + onoff("invertThrottle") + onoff("music") + sl("musicSlider") + onoff("sfx")
            + sl("sfxSlider") + onoff("haptics") + "  hapticsSlider: {fileID: 0}\n"
            + "".join(f"  {f}: {ref(ctx.c(wiring[f][1], 'tmp'))}\n" for f in ("fovValueText", "musicValueText", "sfxValueText"))
            + "".join(f"  {field}: {STORE_SITE}{path}\n" for field, path in STORE_LINKS)
            + f"  accountView: {ref(ctx.c('Window/Account', 'account'))}\n")


def prefab_outputs():
    out = {}
    out[prefab_path(VIEW)] = P.prefab_text(build_view(), VIEW)
    out[prefab_path(VIEW) + ".meta"] = K.PREFAB_META.format(g=asset_guid(VIEW))
    out[OUT_DIR + ".meta"] = K.FOLDER_META.format(g=asset_guid("folder/Settings"))
    return out


# --------------------------------------------------------------------------------------------
# SettingsModal.prefab: the view replaces the old content
# --------------------------------------------------------------------------------------------

MODAL = dict(root_go="3039400609167150240", modal="1111638120248792970", content_go="228058625907025442",
             content_rt="9018503884673417258", border_go="3713524858177433765", options_pi="5895363110174207280")


def modal_transform(text_):
    sc = T.Scene(text_)
    assert sc.field(MODAL["root_go"], "m_Name") == "SettingsModal", "SettingsModal.prefab's root moved"
    assert sc.script(MODAL["modal"]) == S["modal"], "the SettingsModal component moved"
    assert sc.field(MODAL["content_go"], "m_Name") == "Content", "SettingsModal's Content moved"
    ctx = P.Ctx("SettingsModal/View")
    inst, stub = str(ctx.fid("View#inst")), str(ctx.fid("View#stub"))
    retired = []
    if sc.has(MODAL["border_go"]):
        retired += C.subtree(sc, MODAL["border_go"])
    if sc.has(MODAL["options_pi"]):
        retired += C.instance_docs(sc, MODAL["options_pi"])
    for fid in dict.fromkeys(retired):
        if sc.has(fid):
            sc.remove(fid)
    for fid in (inst, stub):
        if sc.has(fid):
            sc.remove(fid)
    out = []
    K.emit_instance(ctx, K.Instance("View", VIEW, asset_guid(VIEW), VIEW, STRETCH, ()), MODAL["content_rt"], out)
    anchor = MODAL["content_rt"]
    for d in (K.unity_empty_scalars(d) for d in out):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(MODAL["content_rt"], C.stretch_rt(T.set_block(sc.docs[MODAL["content_rt"]], "m_Children",
                                                             K.refs_list("m_Children", [stub]))))
    return sc.text()


# --------------------------------------------------------------------------------------------
# Object sets through nesting, and the pruning of what the rebuild orphaned
# --------------------------------------------------------------------------------------------

@functools.lru_cache(None)
def guid_paths():
    out = {}
    for root, _, files in os.walk(os.path.join(REPO, "Assets")):
        for f in files:
            if f.endswith(".prefab.meta"):
                p = os.path.join(root, f)
                m = re.search(r"^guid: (\w+)", open(p, encoding="utf-8", errors="ignore").read(), re.M)
                if m:
                    out[m.group(1)] = os.path.relpath(p[:-5], REPO)
    return out


def pis(text_):
    for m in re.finditer(r"^--- !u!1001 &(-?\d+)\n(.*?)(?=^--- |\Z)", text_, re.M | re.S):
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", m.group(2))
        yield m.group(1), (src.group(1) if src else None), m.start(), m.end()


def object_sets(texts):
    """guid -> frozenset of every object id addressable in that prefab, through nesting; `texts`
    overrides a prefab's text by guid (the generated, not-yet-written versions)."""
    memo = {}

    def objects(g):
        if g in memo:
            return memo[g]
        memo[g] = frozenset()
        path = guid_paths().get(g)
        t = texts.get(g)
        if t is None:
            if path is None:
                return None
            t = open(os.path.join(REPO, path), encoding="utf-8", errors="ignore").read()
        objs = {int(x) for x in re.findall(r"^--- !u!\d+ &(-?\d+)", t, re.M)}
        for pid, src, _, _ in pis(t):
            inner = objects(src) if src else None
            if inner:
                objs |= {(o ^ int(pid)) & MASK for o in inner}
        memo[g] = frozenset(objs)
        return memo[g]
    return objects


# The legacy SettingsModal's own objects (Border subtree) that this rewrite retired, kept so the
# "nothing addresses a retired object" check still has a BEFORE after the rewrite is on disk; the
# OptionsMenuContent objects come through its PrefabInstance, whose source is still in the tree.
LEGACY_MODAL_IDS = (
    102583700666580311, 572751919054375548, 1234620292411860571, 1527419319348381580,
    1543698032408425834, 1593754393584545332, 1599466629431838946, 1916100133067390982,
    2339490367754774360, 3247071710259981796, 3388162534572095365, 3713524858177433765,
    3853841893356484423, 3923598778816109285, 3963464098013828920, 4032878227596607529,
    4156855757067525011, 4262235813992452501, 4925083677052544391, 5082813120236017681,
    5421132078502217407, 5551027660536081085, 5569416990254907359, 5802291577222592890,
    5808447782291626140, 6102779866324036360, 6752722124868991203, 6788038161726496069,
    6880940571450187248, 7250526083047410910, 7696108014880002975, 7825926093119264978,
    7989449918441489869, 8123080634570829183, 8642730898929829042, 8850207266001822177,
    8880519059763811029, 9060239024941696391,
)


def legacy_modal_docs():
    """Header-only documents for the retired objects - enough for `object_sets` to count them."""
    docs = "".join(f"--- !u!1 &{i}\n" for i in LEGACY_MODAL_IDS)
    return docs + (f"--- !u!1001 &{MODAL['options_pi']}\nPrefabInstance:\n  m_SourcePrefab: "
                   f"{{fileID: 100100000, guid: {OPTIONS_GUID}, type: 3}}\n")


TARGET_RE = re.compile(r"target: \{fileID: (-?\d+), guid: (\w+),\s*type: \d+\}")
LAYOUT = ("m_AnchorMin.", "m_AnchorMax.", "m_AnchoredPosition.", "m_SizeDelta.", "m_Pivot.")


def prune(text_, old_objects, new_objects, extra_drop=lambda guid, fid, path: False):
    """Remove, from every PrefabInstance in `text_`, the modifications / removed entries that address
    an object of its source prefab present in the old set and absent from the new one, plus any
    `extra_drop(guid, fid, propertyPath)` asks for; and remove stripped stubs that address such an
    object. Returns (new text, counts, stubs removed)."""
    out, pos, counts, gone_stubs = [], 0, {"mods": 0, "removed": 0}, []
    for pid, src, start, end in list(pis(text_)):
        if src is None:
            continue
        old, new = old_objects(src), new_objects(src)
        if not old or old == new:
            continue
        lost = old - new
        body = text_[start:end]
        mods = re.search(r"(    m_Modifications:\n)(.*?)(?=    m_RemovedComponents:)", body, re.S)
        entries = re.split(r"(?=^    - target: )", mods.group(2), flags=re.M)
        kept = []
        for e in entries:
            if not e.strip():
                continue
            m = TARGET_RE.search(e)
            pm = re.search(r"propertyPath: (.*)", e)
            if m and m.group(2) == src and (int(m.group(1)) in lost or extra_drop(src, int(m.group(1)), pm.group(1) if pm else "")):
                counts["mods"] += 1
                continue
            kept.append(e)
        body = body[:mods.start(2)] + "".join(kept) + body[mods.end(2):]
        for block in ("m_RemovedComponents", "m_RemovedGameObjects"):
            bm = re.search(r"(    %s:)(\n(?:    - \{[^\n]*\n(?:      [^\n]*\n)*)*| \[\]\n)" % block, body)
            if not bm or bm.group(2).strip() == "[]":
                continue
            items = re.findall(r"    - (\{[^}]*\})\n", bm.group(2).replace(",\n        ", ", "))
            keep = []
            for it in items:
                im = re.search(r"fileID: (-?\d+), guid: (\w+)", it)
                if im and im.group(2) == src and int(im.group(1)) in lost:
                    counts["removed"] += 1
                    continue
                keep.append(it)
            repl = (f"    {block}: []\n" if not keep else f"    {block}:\n" + "".join(f"    - {it}\n" for it in keep))
            body = body[:bm.start()] + repl + body[bm.end():]
        added = re.findall(r"targetCorrespondingSourceObject: \{fileID: (-?\d+), guid: (\w+)", body.replace(",\n        ", ", "))
        assert not any(g == src and int(f) in lost for f, g in added), \
            f"PrefabInstance {pid}: an added object hangs off a retired object - resolve it by hand"
        out.append(text_[pos:start] + body)
        pos = end
        for sm in re.finditer(r"^--- !u!(\d+) &(-?\d+) stripped\n(.*?)(?=^--- |\Z)", text_, re.M | re.S):
            if f"m_PrefabInstance: {{fileID: {pid}}}" not in sm.group(3):
                continue
            cm = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: (\w+)", sm.group(3).replace(",\n    ", ", "))
            if cm and cm.group(2) == src and int(cm.group(1)) in lost:
                gone_stubs.append(sm.group(2))
    out.append(text_[pos:])
    result = "".join(out)
    for fid in gone_stubs:
        result = re.sub(r"^--- !u!\d+ &%s stripped\n.*?(?=^--- |\Z)" % fid, "", result, flags=re.M | re.S)
    return result, counts, gone_stubs


def content_layout(xor):
    """Drop the old 1920 x 1080 layout overrides on Content, which stretches now."""
    target = (int(MODAL["content_rt"]) ^ xor) & MASK if xor else int(MODAL["content_rt"])
    return lambda guid, fid, path: fid == target and path.startswith(LAYOUT)


def pause_guid():
    return next(g for g, p in guid_paths().items() if p.endswith("R_Pause_Menu_Panel.prefab"))


def gamecanvas_guid():
    return next(g for g, p in guid_paths().items() if p.replace("\\", "/").endswith("CORE/GameCanvas.prefab"))


def outputs():
    out = prefab_outputs()
    modal_old = open(os.path.join(REPO, MODAL_PREFAB), encoding="utf-8").read()
    out[MODAL_PREFAB] = modal_transform(modal_old)
    old = object_sets({})
    new = object_sets({MODAL_GUID: out[MODAL_PREFAB], asset_guid(VIEW): out[prefab_path(VIEW)]})
    scene, sc_counts, sc_stubs = prune(open(os.path.join(REPO, SCENE), encoding="utf-8").read(), old, new,
                                       lambda g, f, p: g == MODAL_GUID and content_layout(0)(g, f, p))
    out[SCENE] = scene
    pg = pause_guid()
    rpid = next(int(pid) for pid, src, _, _ in pis(open(os.path.join(REPO, guid_paths()[pg]), encoding="utf-8").read())
                if src == MODAL_GUID)
    gc, gc_counts, gc_stubs = prune(open(os.path.join(REPO, GAMECANVAS), encoding="utf-8").read(), old, new,
                                    lambda g, f, p: g == pg and content_layout(rpid)(g, f, p))
    out[GAMECANVAS] = gc
    return out, (sc_counts, sc_stubs, gc_counts, gc_stubs)


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

TMP, IMG = K.TMP_UGUI, K.IMAGE
DROPDOWN_FIELDS = dict(m_TargetGraphic=IMG, m_Template=224, m_CaptionText=TMP, m_ItemText=TMP)


def every_target_resolves(path, text_, objects, before):
    """Every override / stub this change could orphan still resolves. Only an id the source prefab
    HAD (`before`) and no longer has (`objects`) is this change's to answer for - a target that was
    already stale before it (EndGameStatsPanel carries one, untouched since #912) is left alone,
    the same rule `prune` removes by."""
    def lost(g, f):
        objs, was = objects(g), before(g)
        return bool(objs) and bool(was) and f in was and f not in objs

    for pid, src, start, end in pis(text_):
        if not src:
            continue
        for f, g in TARGET_RE.findall(text_[start:end]):
            assert g != src or not lost(src, int(f)), f"{path}: PrefabInstance {pid} still targets {f}, which {src} no longer has"
    for sm in re.finditer(r"^--- !u!\d+ &(-?\d+) stripped\n(.*?)(?=^--- |\Z)", text_, re.M | re.S):
        cm = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: (\w+)", sm.group(2).replace(",\n    ", ", "))
        if cm:
            assert not lost(cm.group(2), int(cm.group(1))), f"{path}: stub {sm.group(1)} addresses {cm.group(1)}, which {cm.group(2)} no longer has"


# The settings links point at the store site (merge c659fe209f, claude/store-page-urls-kg5qx7).
STORE_SITE = "https://www.froglet.games"
STORE_LINKS = (("privacyPolicyUrl", "/privacy"), ("deleteDataUrl", "/delete-my-data"), ("bugReportUrl", "/support"))


def validate(files):
    guids = T.known_guids() | PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    view = files[prefab_path(VIEW)]
    A.check_file(prefab_path(VIEW), view, guids, minted)
    A.check_file(MODAL_PREFAB, files[MODAL_PREFAB], guids, minted)
    V = K.FileIds(VIEW)
    _, ctl = A.doc_of(view, V("root#controller"))
    assert ctl and S["controller"] in ctl, "SettingsView: the root carries no GameSettingsPanelController"
    for field, path in STORE_LINKS:
        assert f"  {field}: {STORE_SITE}{path}\n" in ctl, f"controller.{field} does not point at {STORE_SITE}{path} (the store page)"
    for _, _, rows in PAGES:
        for kind, cap, _, field in rows:
            if kind == "tg":
                block = re.search(r"^  %s:\n((?:    .*\n){4})" % field, ctl, re.M)
                assert block, f"controller.{field} is not an ON / OFF pair"
                for side in ("onButton", "offButton"):
                    fid = re.search(r"%s: \{fileID: (-?\d+)" % side, block.group(1)).group(1)
                    assert fid != "0" and A.kind_of(view, fid)[1] == S["tab"], f"controller.{field}.{side} is not a KitTextTab"
            else:
                C.wired(view, ctl, field, DROPDOWN if kind == "dd" else SLIDER, "GameSettingsPanelController")
    for f in READOUTS.values():
        C.wired(view, ctl, f, TMP, "GameSettingsPanelController")
    for k, _, _, f in LINKS:
        C.wired(view, ctl, f, S["button"], "GameSettingsPanelController")
    for name, _, rows in PAGES:
        for i, (kind, _, _, _) in enumerate(rows):
            if kind != "dd":
                continue
            _, dd = A.doc_of(view, V(f"Window/{name}/Row{i + 1}/Dropdown#dd"))
            for field, want in DROPDOWN_FIELDS.items():
                C.wired(view, dd, field, want, f"{name} row {i + 1} dropdown")
            tmpl = A.doc_of(view, V(f"Window/{name}/Row{i + 1}/Dropdown/Template#go"))[1]
            assert "  m_IsActive: 0\n" in tmpl, f"{name} row {i + 1}: the dropdown template must start inactive"
    _, tb = A.doc_of(view, V("root#tabbar"))
    assert tb.count("  - button: {fileID: ") == len(PAGES) == 5 and "  scaleLabels: 0\n" in tb, "SettingsTabBar: five kit tabs, unscaled"
    # Rev 9: the Account tab and its window.
    _, acc = A.doc_of(view, V("Window/Account#account"))
    assert acc and S["account"] in acc, "SettingsView: the Account page carries no AccountSettingsView"
    assert re.search(r"^  accountView: \{fileID: %s\}" % V("Window/Account#account"), ctl, re.M), \
        "GameSettingsPanelController.accountView is not the Account page (its actions would escape the in-game lock)"
    assert f"  providers: {K.ref(11400000, PROVIDERS_GUID, 2)}" in acc, "AccountSettingsView reads another providers asset"
    assert os.path.exists(os.path.join(REPO, "Assets/_SO_Assets/UI/SignInProviders.asset.meta")) and \
        f"guid: {PROVIDERS_GUID}" in open(os.path.join(REPO, "Assets/_SO_Assets/UI/SignInProviders.asset.meta")).read(), \
        "SignInProviders.asset is missing (run author_start_flow.py)"
    rows_ = re.findall(r"  - provider: (\d+)\n    root: \{fileID: (-?\d+)\}\n    mark: .*\n    line: .*\n    linkButton: \{fileID: (-?\d+)\}", acc)
    assert sorted(int(v) for v, _, _ in rows_) == [2, 3, 4], "the Account tab needs a row each for Steam, Google and Apple"
    assert all(b != "0" for _, _, b in rows_), "an Account row has no LINK button"
    # R9b (section 57): a row's mark comes from SignInProviders.asset (the providers' own logo buttons); the prefab ships
    # none, untinted and disabled, so no stand-in or recoloured mark can show.
    for name, _, _ in ACCOUNT_ROWS:
        _, md = A.doc_of(view, V(f"Window/Account/Rows/{name}/Mark#img"))
        assert md and "  m_Sprite: {fileID: 0}\n" in md, f"the {name} Account row ships a mark sprite (marks come from SignInProviders.asset)"
        assert "  m_Color: {r: 1, g: 1, b: 1, a: 1}\n" in md and "  m_Enabled: 0\n" in md, f"the {name} Account mark is tinted or ships enabled"
    for f in ("pilotName", "playerIdText", "guestNotice", "actionsRow", "signOutButton", "deleteButton", "prompt"):
        assert not re.search(r"^  %s: \{fileID: 0\}" % f, acc, re.M), f"AccountSettingsView.{f} is not wired"
    _, pw = A.doc_of(view, V("Prompt#prompt"))
    assert pw and S["prompt"] in pw, "SettingsView: no AccountPromptWindow"
    for f in ("group", "window", "cards", "primaryButton", "secondaryButton", "cancelButton", "primaryLabel"):
        assert not re.search(r"^  %s: \{fileID: 0\}" % f, pw, re.M), f"AccountPromptWindow.{f} is not wired"
    _, pcg = A.doc_of(view, V("Prompt#cg"))
    assert "  m_Alpha: 0\n" in pcg and "  m_BlocksRaycasts: 0\n" in pcg, "the Account window ships shown (it would cover the settings)"
    assert view.count("guid: " + S["close"]) == 1, "SettingsView: Close must close through ModalWindowCloseButton"

    modal = files[MODAL_PREFAB]
    assert f"guid: {OPTIONS_GUID}" not in modal, "SettingsModal.prefab still nests OptionsMenuContent"
    assert f"m_SourcePrefab: {{fileID: 100100000, guid: {asset_guid(VIEW)}" in modal, "SettingsModal.prefab does not nest SettingsView"
    objects = object_sets({MODAL_GUID: modal, asset_guid(VIEW): view})
    before = object_sets({MODAL_GUID: modal + legacy_modal_docs()})
    for path in (MODAL_PREFAB, SCENE, GAMECANVAS):
        t = files[path]
        anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", t, re.M)) | {"0"}
        for m in re.finditer(r"\{fileID: (-?\d+)\}", t):
            assert m.group(1) in anchors, f"{path}: dangling local fileID {m.group(1)}"
        every_target_resolves(path, t, objects, before)
    for path in (SCENE, GAMECANVAS):
        assert not re.search(r"target: \{fileID: %s, guid: %s" % (MODAL["content_rt"], MODAL_GUID), files[path]) or \
            not re.search(r"propertyPath: m_SizeDelta", files[path]), "Content's old size overrides survive"


def drift(files):
    return [p for p, t in files.items()
            if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p), encoding="utf-8").read() != t]


def _show_prompt(text_):
    fid = K.FileIds(VIEW)("Prompt#cg")
    i = text_.index(f" &{fid}\n")
    j = text_.find("\n--- ", i)
    return text_[:i] + text_[i:j].replace("  m_Alpha: 0\n", "  m_Alpha: 1\n").replace("  m_BlocksRaycasts: 0\n", "  m_BlocksRaycasts: 1\n") + text_[j:]


def _stand_in_mark(text_):
    fid = K.FileIds(VIEW)("Window/Account/Rows/Google/Mark#img")
    i = text_.index(f" &{fid}\n")
    j = text_.find("\n--- ", i)
    return text_[:i] + text_[i:j].replace("  m_Sprite: {fileID: 0}", "  m_Sprite: " + K.sprite_ref(K.chrome_sprite("chrome_provider_google"))) + text_[j:]


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
    view = prefab_path(VIEW)
    V = K.FileIds(VIEW)
    some_retired = "5130616657160765095"     # the old colorblind dropdown, inside OptionsMenuContent
    orphan = (f"    - target: {{fileID: {(int(some_retired) ^ int(MODAL['options_pi'])) & MASK}, guid: {MODAL_GUID}, type: 3}}\n"
              "      propertyPath: m_Value\n      value: 0\n      objectReference: {fileID: 0}\n")
    cases = [
        ("an orphaned override left in Menu_Main", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"    - target: {{fileID: {MODAL['modal']}, guid: {MODAL_GUID}", orphan + f"    - target: {{fileID: {MODAL['modal']}, guid: {MODAL_GUID}", 1))),
        ("a dropdown with no template", lambda f: f.__setitem__(view, re.sub(r"(  m_Template: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("an ON / OFF pair on plain buttons", lambda f: f.__setitem__(view, f[view].replace("    onButton: {fileID: ", "    onButton: {fileID: 1", 1))),
        ("the Account page unhooked from the in-game lock", lambda f: f.__setitem__(view, re.sub(r"(  accountView: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view]))),
        ("a stand-in mark on an Account row", lambda f: f.__setitem__(view, _stand_in_mark(f[view]))),
        ("an Account row missing", lambda f: f.__setitem__(view, re.sub(r"  - provider: 4\n    root: .*\n    mark: .*\n    line: .*\n    linkButton: .*\n    linkedMark: .*\n", "", f[view]))),
        ("the Account window ships shown", lambda f: f.__setitem__(view, _show_prompt(f[view]))),
        ("a settings link back on the old site", lambda f: f.__setitem__(view, f[view].replace(STORE_SITE, "https://cosmicshore.com", 1))),
        ("OptionsMenuContent left nested", lambda f: f.__setitem__(MODAL_PREFAB, f[MODAL_PREFAB] + f"# guid: {OPTIONS_GUID}\n")),
    ]
    ok = all(expect(l, m) for l, m in cases)
    print("OK: every negative control fires" if ok else "FAIL: a negative control did not fire")
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    files, (scc, scs, gcc, gcs) = outputs()
    validate(files)
    print(f"OK: SettingsView, SettingsModal.prefab, Menu_Main and GameCanvas validate "
          f"(pruned: Menu_Main {scc['mods']} overrides, {scc['removed']} removed entries, {len(scs)} stubs; "
          f"GameCanvas {gcc['mods']} overrides, {gcc['removed']} removed entries, {len(gcs)} stubs)")
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
