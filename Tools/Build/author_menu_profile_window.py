#!/usr/bin/env python3
"""
Author ITEM 5b of the style-B menu redesign - the PROFILE WINDOW - as prefabs, and mount it in
Menu_Main.unity (Docs/MENU_REDESIGN_B.md section 15.2; the approved ProfileModal / ProfileName /
ProfileNameError boards on the "Menu Redesign B" canvas).

    Assets/_Prefabs/UI Elements/Main Menu Screens/Profile/
        ProfileAvatarButton.prefab  one avatar of the 6 x 3 grid: an octagon KitTile (selected ring,
                                    gamepad focus ring) carrying ProfileIconSelectButton
        ProfileWindowView.prefab    the window body: the "How others see you" preview column, the
                                    AVATAR / DISPLAY NAME tabs (KitTextTab), the Close keycap button,
                                    the Avatar page (grid, save note, Done) and the Display name page
                                    (field + count, the four rule ticks, the danger strip, the
                                    checking hex, Submit / Cancel)

    Menu_Main.unity, `PlayerDataSelectModal` (ProfileIconSelectView, fileID 8675064058193410727):
        The old AvatarContent and DisplayNameContent subtrees are deleted and one ProfileWindowView
        instance is mounted in their place. The component keeps its fileID - ScreenSwitcher.Modals,
        ProfileScreen.profileWindow, the arcade profile widget and the five persistent calls that
        name it all still resolve - and its fields are rewritten into the view.

STANDING DOWN: the migration runs only while the old subtrees exist; the documents this script owns
are re-emitted and validated on every run, --check included.

    python3 Tools/Build/author_menu_profile_window.py              # write
    python3 Tools/Build/author_menu_profile_window.py --check      # fail if any output drifted
    python3 Tools/Build/author_menu_profile_window.py --self-test  # planted defects must all be caught
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
OUT_DIR = P.OUT_DIR
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok
TL, TR, ref, mono, refs = C.TL, C.TR, C.ref, C.mono, C.refs
text, label, le, canvas_group = H.text, H.label, H.le, H.canvas_group
image_node, art_node = A.image_node, A.art_node
c = P.c

PREFABS = ("ProfileAvatarButton", "ProfileWindowView")

GRID = "8a8695521f0d02e499659fee002a26c2"           # UnityEngine.UI.GridLayoutGroup
INPUT = "2da0c512f12947e489f739169773d7ca"          # TMPro.TMP_InputField
S = dict(view=C.script("ProfileIconSelectView"), avatar=C.script("ProfileIconSelectButton"),
         tab=K.kit_script("KitTextTab"), tile=K.kit_script("KitTile"), button=K.kit_script("KitButton"),
         kt=K.kit_script("KitText"), entrance=K.kit_script("KitEntrance"), close=C.script("ModalWindowCloseButton"))
PACKAGE = P.PACKAGE | {GRID, INPUT}
C.STUB_KIND.update({"grid": (114, GRID), "input": (114, INPUT), "ttab": (114, S["tab"]),
                    "entrance": (114, S["entrance"])})
PROFILE_ICONS = "3e6cb5b838253c7468480627fd86af5f"   # SO_DefaultProfileIcons (18)


def prefab_guid(name):
    return P.asset_guid(name) if name in PREFABS else P.prefab_guid(name)


def prefab_path(name):
    return f"{OUT_DIR}/{name}.prefab"


# --------------------------------------------------------------------------------------------
# ProfileAvatarButton.prefab - one avatar (112 x 112)
# --------------------------------------------------------------------------------------------

CELL = 112
OCT = K.kit_sprite("kit_oct_fill")


def build_avatar_button():
    root = N("root", "ProfileAvatarButton", R(size=(CELL, CELL)))
    ring = root.child(N("FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (12, 12))))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Cta", 0.0), OCT))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), OCT, raycast=True))
    fill = root.child(N("Fill", "Fill", R((0, 0), (1, 1), (0, 0), (-6, -6))))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark"), OCT))
    art_node(root, "Icon", "Icon", R((0, 0), (1, 1), (0, 0), (-10, -10)), A.AVATAR_PLACEHOLDER, preserve=True)
    body = A.tile_body(c("FocusRing", "img"), c("Fill", "img"), c("Border", "img"), 14)
    root.add("tile", 114, lambda ctx, go: body(ctx, go).replace(
        K.sprite_ref(K.kit_sprite("kit_sliver14_line1")), K.sprite_ref(OCT)).replace(
        K.sprite_ref(K.kit_sprite("kit_sliver14_line2")), K.sprite_ref(OCT)))
    root.add("avatar", 114, lambda ctx, go: mono(go, S["avatar"],
             "  BorderImage: {fileID: 0}\n"
             f"  IconImage: {ref(ctx.c('Icon', 'img'))}\n  tile: {ref(ctx.c('root', 'tile'))}\n"))
    return root


# --------------------------------------------------------------------------------------------
# ProfileWindowView.prefab - the window body
# --------------------------------------------------------------------------------------------

W, HGT, SIDE, X0, CW = 1260, 680, 340, 384, 832
RULES = ("3 to 25 characters", "Letters, numbers, spaces, _ - .", "Starts and ends with a letter or number",
         "No doubled spaces or punctuation")
BUTTONS = {"Done": "KitButton", "Submit": "KitButton", "Cancel": "KitButton"}


def avatar_oct(parent, key, r, ring_token, inset=(6, 10), ambient=False):
    av = parent.child(N(key, key.split("/")[-1], r))
    image_node(av, f"{key}/Ring", "Ring", STRETCH, ring_token, OCT)
    image_node(av, f"{key}/Back", "Back", R((0, 0), (1, 1), (0, 0), (-inset[0], -inset[0])), "SurfaceDark", OCT)
    icon = art_node(av, f"{key}/Icon", "Icon", R((0, 0), (1, 1), (0, 0), (-inset[1], -inset[1])),
                    A.AVATAR_PLACEHOLDER, preserve=True)
    return av


def preview(win):
    pv = win.child(N("Window/Preview", "Preview", TL(0, 0, SIDE, HGT)))
    image_node(pv, "Window/Preview/Fill", "Fill", R((0, 0), (1, 1), (1, 0), (-2, -2)), "SurfaceVeryDark")
    pv.child(M.panel_sea_node("Window/Preview/Grid"))
    M.bracket_nodes(pv, "Window/Preview/Bracket")
    image_node(pv, "Window/Preview/Edge", "Edge", R((1, 0), (1, 1), (0, 0), (1, -2), (1, 0.5)), "SurfaceDark")
    label(pv.child(N("Window/Preview/Caption", "Caption", TL(0, 50, SIDE, 18))), "How others see you", "Label",
          "NeutralLightest", align="center")
    avatar_oct(pv, "Window/Preview/Avatar", TL(78, 86, 184, 184), "SurfaceLight", ambient=True)
    A.label_e(pv.child(N("Window/Preview/Name", "Name", TL(10, 288, SIDE - 20, 44))), "Pilot", "H2", "TextLight",
              align="center")
    label(pv.child(N("Window/Preview/Online", "Online", TL(0, 340, SIDE, 18))), "Online", "Label", "Cta",
          align="center")
    chip = pv.child(N("Window/Preview/Slot", "Slot", TL(60, 582, 220, 56)))
    H.framed(chip, "Window/Preview/Slot", 10, "SurfaceVeryDark", "SurfaceDark")
    avatar_oct(chip, "Window/Preview/Slot/Avatar", TL(8, 8, 40, 40), "ControlActive", inset=(3, 5))
    A.label_e(chip.child(N("Window/Preview/Slot/Name", "Name", TL(58, 9, 154, 22))), "Pilot", "Body", "TextLight")
    label(chip.child(N("Window/Preview/Slot/Role", "Role", TL(58, 32, 154, 16))), "In a party slot", "Label",
          "NeutralLightest")


def tab_node(win, key, caption, x):
    k = f"Window/Tabs/{key}"
    w = K.label_width(caption.upper(), "Button")
    n = win.child(N(k, key, TL(x, 40, w, 44)))
    A.ring_node(n, f"{k}/FocusRing", 10)
    hit = n.child(N(f"{k}/Hit", "Hit", STRETCH))
    hit.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True))
    lab = n.child(N(f"{k}/Label", "Label", R((0, 1), (1, 1), (0, 0), (0, K.line_height("Button")), (0.5, 1))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(caption.upper(), "Button", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("Button", "NeutralLightest", driven=True))
    und = n.child(N(f"{k}/Underline", "Underline", R((0, 0), (1, 0), (0, 4), (0, 2), (0.5, 0))))
    und.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive", 0.0)))
    n.add("tab", 114, lambda ctx, go: mono(go, S["tab"],
          A.selectable() + f"  m_TargetGraphic: {ref(ctx.c(f'{k}/Hit', 'img'))}\n" + A.calls([])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c(f'{k}/FocusRing', 'img'))}\n"
          "  isSelected: 0\n"
          f"  label: {ref(ctx.c(f'{k}/Label', 'tmp'))}\n  underline: {ref(ctx.c(f'{k}/Underline', 'img'))}\n"))
    n.add("audio", 114, A.menu_audio(11))
    return x + w + 36


def close_button(win):
    """The Close keycap button: A.back_button's build with its own word."""
    w_label = K.label_width("CLOSE", "ButtonSmall")
    keycap_w = K.label_width("B", "Label") + 24
    w = 16 + keycap_w + 10 + w_label + 16
    b = win.child(N("Window/Close", "Close", TR(44, 40, w, 40)))
    A.ring_node(b, "Window/Close/FocusRing", 10)
    border, fillc, content = K.button_palette("Default", False)
    f = b.child(N("Window/Close/Fill", "Fill", STRETCH))
    f.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)).add("le", 114, le(ignore=True))
    bd = b.child(N("Window/Close/Border", "Border", STRETCH))
    bd.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)).add("le", 114, le(ignore=True))
    b.children.append(P.Inst("Keycap", "KitKeycap", "Keycap", R(size=(keycap_w, K.line_height("Label") + 8)),
                             [("Label#tmp", "m_text", "B", None)]))
    lab = b.child(N("Window/Close/Label", "Label", R(size=(w_label, K.line_height("ButtonSmall")))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp("CLOSE", "ButtonSmall", content)) \
        .add("kt", 114, K.kit_text("ButtonSmall", driven=True))
    b.add("button", 114, lambda ctx, go: mono(go, S["button"],
          K.SELECTABLE_FIELDS + f"  m_TargetGraphic: {ref(ctx.c('Window/Close/Fill', 'img'))}\n" + A.calls([])
          + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {ref(ctx.c('Window/Close/FocusRing', 'img'))}\n"
          "  variant: 0\n  fill: 0\n"
          f"  fillGraphic: {ref(ctx.c('Window/Close/Fill', 'img'))}\n"
          f"  borderGraphic: {ref(ctx.c('Window/Close/Border', 'img'))}\n"
          + refs("contentGraphics", [ctx.c("Window/Close/Label", "tmp")]) + "  playGradient: {fileID: 0}\n"))
    b.add("close", 114, lambda ctx, go: mono(go, S["close"], ""))
    b.add("audio", 114, A.menu_audio(11))
    b.add("hlg", 114, K.hv_layout(False, (16, 16, 9, 9), 10, 4, (0, 0), (0, 0))).add("csf", 114, K.fitter(2, 2))


def footer_button(page, key, variant, caption, x):
    """A footer button straddling the window's bottom edge, as the boards draw it."""
    w, h = K.button_size(caption)
    page.children.append(P.Inst(key, "KitButton", key, R((0, 0), None, (x, 0), (w, h), (0, 0.5)),
                                K.button_mods("KitButton", variant, True, caption)))
    return x + w + 16


def avatar_page(win):
    pg = win.child(N("Window/AvatarPage", "AvatarPage", STRETCH))
    pg.add("cg", 225, canvas_group(True))
    gap = (CW - 6 * CELL) // 5
    grid = pg.child(N("Window/AvatarPage/Grid", "Grid", TL(X0, 100, CW, 3 * CELL + 2 * 18)))
    grid.add("grid", 114, lambda ctx, go: mono(go, GRID,
             "  m_Padding:\n    m_Left: 0\n    m_Right: 0\n    m_Top: 0\n    m_Bottom: 0\n"
             "  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n"
             f"  m_CellSize: {{x: {CELL}, y: {CELL}}}\n  m_Spacing: {{x: {gap}, y: 18}}\n"
             "  m_Constraint: 1\n  m_ConstraintCount: 6\n"))
    grid.add("in", 114, M.entrance("Rise", play_on_enable=False))
    label(pg.child(N("Window/AvatarPage/SaveNote", "SaveNote", TL(X0, 494, CW, 22))),
          "Your avatar saves as soon as you pick it.", "BodySmall", "NeutralLightest")
    footer_button(pg, "Done", "Play", "DONE", X0)


def input_field(ctx, go):
    font = K.ALDRICH
    return mono(go, INPUT,
                K.SELECTABLE_FIELDS.replace("  m_Transition: 1\n", "  m_Transition: 0\n")
                + f"  m_TargetGraphic: {ref(ctx.c('Window/NamePage/Field/Fill', 'img'))}\n"
                f"  m_TextViewport: {ref(ctx.rt('Window/NamePage/Field/TextArea'))}\n"
                f"  m_TextComponent: {ref(ctx.c('Window/NamePage/Field/TextArea/Text', 'tmp'))}\n"
                f"  m_Placeholder: {ref(ctx.c('Window/NamePage/Field/TextArea/Placeholder', 'tmp'))}\n"
                "  m_VerticalScrollbar: {fileID: 0}\n  m_VerticalScrollbarEventHandler: {fileID: 0}\n"
                "  m_LayoutGroup: {fileID: 0}\n  m_ScrollSensitivity: 1\n  m_ContentType: 0\n  m_InputType: 0\n"
                "  m_AsteriskChar: 42\n  m_KeyboardType: 0\n  m_LineType: 0\n  m_HideMobileInput: 0\n"
                "  m_HideSoftKeyboard: 0\n  m_CharacterValidation: 0\n  m_RegexValue: \n  m_GlobalPointSize: 22\n"
                "  m_CharacterLimit: 0\n"
                + "".join(f"  {e}:\n    m_PersistentCalls:\n      m_Calls: []\n" for e in (
                    "m_OnEndEdit", "m_OnSubmit", "m_OnSelect", "m_OnDeselect", "m_OnTextSelection",
                    "m_OnEndTextSelection", "m_OnValueChanged", "m_OnTouchScreenKeyboardStatusChanged"))
                + f"  m_CaretColor: {K.col(tok('TextLight'))}\n  m_CustomCaretColor: 1\n"
                f"  m_SelectionColor: {K.col(tok('ControlActive', 0.35))}\n  m_Text: \n"
                "  m_CaretBlinkRate: 0.85\n  m_CaretWidth: 2\n  m_ReadOnly: 0\n  m_RichText: 0\n"
                f"  m_GlobalFontAsset: {K.ref(11400000, font[0], 2)}\n"
                "  m_OnFocusSelectAll: 1\n  m_ResetOnDeActivation: 1\n  m_KeepTextSelectionVisible: 0\n"
                "  m_RestoreOriginalTextOnEscape: 1\n  m_isRichTextEditingAllowed: 0\n  m_LineLimit: 0\n"
                "  isAlert: 0\n  m_InputValidator: {fileID: 0}\n  m_ShouldActivateOnSelect: 1\n")


def name_page(win):
    pg = win.child(N("Window/NamePage", "NamePage", STRETCH))
    pg.add("cg", 225, canvas_group(False))
    label(pg.child(N("Window/NamePage/Title", "Title", TL(X0, 100, CW, 40))), "Choose the name of your Ark", "H2",
          "TextLight")
    label(pg.child(N("Window/NamePage/FieldLabel", "FieldLabel", TL(X0, 152, 400, 18))), "Display name", "Label",
          "NeutralLightest")
    f = pg.child(N("Window/NamePage/Field", "Field", TL(X0, 176, CW, 60)))
    fill = f.child(N("Window/NamePage/Field/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver10_fill"), True, raycast=True))
    bord = f.child(N("Window/NamePage/Field/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("ControlActive"), K.kit_sprite("kit_sliver10_line1"), True))
    area = f.child(N("Window/NamePage/Field/TextArea", "TextArea", R((0, 0), (1, 1), (-44, 0), (-132, -8))))
    area.add("mask", 114, A.rect_mask())
    ph = area.child(N("Window/NamePage/Field/TextArea/Placeholder", "Placeholder", STRETCH))
    ph.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("Your name", "H3", tok("NeutralLightest"))) \
        .add("kt", 114, K.kit_text("H3", "NeutralLightest"))
    tx = area.child(N("Window/NamePage/Field/TextArea/Text", "Text", STRETCH))
    tx.add("cr", 222, K.canvas_renderer()).add("tmp", 114, text("", "H3", tok("TextLight"))) \
        .add("kt", 114, K.kit_text("H3", "TextLight"))
    cnt = f.child(N("Window/NamePage/Field/Count", "Count", R((1, 0), None, (-18, 8), (90, 18), (1, 0))))
    label(cnt, "0 / 25", "DataSmall", "NeutralLightest", align="right")
    f.add("input", 114, input_field).add("shake", 114, M.shake())

    for i, cap in enumerate(RULES):
        x, y = X0 + (i % 2) * 426, 252 + (i // 2) * 24
        mk = pg.child(N(f"Window/NamePage/Rule{i + 1}Mark", f"Rule{i + 1}Mark", TL(x, y + 3, 14, 14)))
        mk.add("cr", 222, K.canvas_renderer()) \
            .add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_diamond"), preserve=True))
        label(pg.child(N(f"Window/NamePage/Rule{i + 1}", f"Rule{i + 1}", TL(x + 24, y, 400, 20))), cap, "BodySmall",
              "NeutralLightest")

    msg = pg.child(N("Window/NamePage/Message", "Message", TL(X0, 310, CW, 40)))
    msg.add("cg", 225, canvas_group(False))
    image_node(msg, "Window/NamePage/Message/Fill", "Fill", STRETCH, "Danger", K.kit_sprite("kit_sliver10_fill"), True)
    label(msg.child(N("Window/NamePage/Message/Text", "Text", TL(18, 0, CW - 36, 40))), "", "Body", "SurfaceBlack")
    chk = pg.child(N("Window/NamePage/Checking", "Checking", TL(X0, 310, CW, 40)))
    chk.add("cg", 225, canvas_group(False))
    hx = image_node(chk, "Window/NamePage/Checking/Hex", "Hex", TL(0, 9, 22, 22), "ControlActive",
                    K.kit_sprite("kit_hex_line2"), preserve=True)
    hx.add("amb", 114, M.ambient("Spin", period=1.2, amplitude=1))
    label(chk.child(N("Window/NamePage/Checking/Label", "Label", TL(34, 0, 400, 40))), "Checking the name...",
          "Label", "TextLight")

    x = footer_button(pg, "Submit", "Play", "SUBMIT", X0)
    footer_button(pg, "Cancel", "Cancel", "CANCEL", x)


def build_view():
    root = N("root", "ProfileWindowView", STRETCH)
    win = root.child(N("Window", "Window", R((0.5, 0.5), None, (0, 50), (W, HGT))))
    win.add("cg", 225, canvas_group(True)).add("in", 114, M.entrance("Pop", play_on_enable=False))
    fill = win.child(N("Window/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceBlack", 0.96), K.kit_sprite("kit_sliver14_fill"), True, raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", 0.96))
    bord = win.child(N("Window/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()) \
        .add("img", 114, K.image(tok("SurfaceLight"), K.kit_sprite("kit_sliver14_line1"), True)) \
        .add("th", 114, H.themed("SurfaceLight"))
    preview(win)
    # Pages before the tabs in hierarchy: the modal's Refocus lands on the first live Selectable,
    # which is then the first avatar (Avatar tab) or the name field (Display name tab).
    avatar_page(win)
    name_page(win)
    x = tab_node(win, "Avatar", "Avatar", X0)
    tab_node(win, "Name", "Display name", x)
    close_button(win)
    return root


BUILDERS = {"ProfileAvatarButton": build_avatar_button, "ProfileWindowView": build_view}


def prefab_outputs():
    out = {}
    for name in PREFABS:
        out[prefab_path(name)] = P.prefab_text(BUILDERS[name](), name)
        out[prefab_path(name) + ".meta"] = K.PREFAB_META.format(g=P.asset_guid(name))
    return out


# --------------------------------------------------------------------------------------------
# The Menu_Main patch
# --------------------------------------------------------------------------------------------

IDS = dict(window_go="3006443216857350328", window="8675064058193410727",
           retired={"3094231083540484076": "AvatarContent", "6150679731584708451": "DisplayNameContent"})

# ProfileIconSelectView's own fields, in declaration order -> (object key in ProfileWindowView, stub kind).
VIEW_REFS = {
    "iconGrid": ("Window/AvatarPage/Grid#grid", "grid"),
    "displayNameInput": ("Window/NamePage/Field#input", "input"),
    "displayNameSaveButton": ("Submit|root#button", "button"),
    "displayNameCancelButton": ("Cancel|root#button", "button"),
    "avatarTab": ("Window/Tabs/Avatar#tab", "ttab"),
    "nameTab": ("Window/Tabs/Name#tab", "ttab"),
    "avatarPage": ("Window/AvatarPage#cg", "cg"),
    "namePage": ("Window/NamePage#cg", "cg"),
    "doneButton": ("Done|root#button", "button"),
    "windowEntrance": ("Window#in", "entrance"),
    "previewAvatar": ("Window/Preview/Avatar/Icon#img", "img"),
    "previewName": ("Window/Preview/Name#tmp", "tmp"),
    "previewSlotAvatar": ("Window/Preview/Slot/Avatar/Icon#img", "img"),
    "previewSlotName": ("Window/Preview/Slot/Name#tmp", "tmp"),
    "saveNoteText": ("Window/AvatarPage/SaveNote#kt", "kt"),
    "characterCountText": ("Window/NamePage/Field/Count#tmp", "tmp"),
    "fieldBorder": ("Window/NamePage/Field/Border#img", "img"),
    "messageGroup": ("Window/NamePage/Message#cg", "cg"),
    "messageText": ("Window/NamePage/Message/Text#kt", "kt"),
    "checkingGroup": ("Window/NamePage/Checking#cg", "cg"),
}
RULE_LABELS = [(f"Window/NamePage/Rule{i + 1}#kt", "kt") for i in range(4)]
RULE_MARKS = [(f"Window/NamePage/Rule{i + 1}Mark#img", "img") for i in range(4)]
VIEW_ORDER = ("profileIcons", "iconGrid", "iconButtonPrefab", "displayNameInput", "displayNameSaveButton",
              "displayNameCancelButton", "avatarTab", "nameTab", "avatarPage", "namePage", "doneButton",
              "windowEntrance", "previewAvatar", "previewName", "previewSlotAvatar", "previewSlotName",
              "saveNoteText", "theme", "characterCountText", "fieldBorder", "ruleLabels", "ruleMarks",
              "ruleMetSprite", "ruleOpenSprite", "messageGroup", "messageText", "checkingGroup")


class WindowMount(C.Mount):
    def __init__(self):
        self.view = "ProfileWindowView"
        self.ctx = P.Ctx("Menu_Main/ProfileWindow")
        self.inst = str(self.ctx.fid("View#inst"))
        self.stub = str(self.ctx.fid("View#stub"))
        self.guid = P.asset_guid(self.view)
        self.kind = None

    def src(self, key):
        V = K.FileIds(self.view)
        if "|" in key:
            inst, src_key = key.split("|")
            return C.derived(K.source_fid(BUTTONS[inst], src_key), V(inst + "#inst"))
        return V(key)

    def refs(self):
        return list(VIEW_REFS.values()) + RULE_LABELS + RULE_MARKS

    def docs(self, parent_rt, mods=()):
        out = []
        K.emit_instance(self.ctx, K.Instance("View", self.view, self.guid, self.view, STRETCH, mods), parent_rt, out)
        for key, comp in self.refs():
            out.append(self.stub_text(key, comp))
        return [K.unity_empty_scalars(d) for d in out]

    def ids(self):
        return {self.inst, self.stub} | {self.stub_fid(k) for k, _ in self.refs()}


def rebuild_view_doc(doc, mount):
    """ProfileIconSelectView's fields from profileIcons on, in declaration order (what Unity writes)."""
    head = doc[:doc.index("  profileIcons:")]
    vals = {k: ref(mount.stub_fid(v[0])) for k, v in VIEW_REFS.items()}
    vals["profileIcons"] = K.ref(11400000, PROFILE_ICONS, 2)
    vals["iconButtonPrefab"] = K.ref(K.FileIds("ProfileAvatarButton")("root#avatar"), P.asset_guid("ProfileAvatarButton"))
    vals["theme"] = K.ref(11400000, K.THEME, 2)
    vals["ruleMetSprite"] = K.sprite_ref(K.chrome_sprite("chrome_confirm"))
    vals["ruleOpenSprite"] = K.sprite_ref(K.chrome_sprite("chrome_diamond"))
    body = ""
    for k in VIEW_ORDER:
        if k == "ruleLabels":
            body += K.refs_list(k, [mount.stub_fid(key) for key, _ in RULE_LABELS])
        elif k == "ruleMarks":
            body += K.refs_list(k, [mount.stub_fid(key) for key, _ in RULE_MARKS])
        else:
            body += f"  {k}: {vals[k]}\n"
    return head + body


def scene_transform(text):
    sc = T.Scene(text)
    assert sc.field(IDS["window_go"], "m_Name") == "PlayerDataSelectModal", "the profile window moved"
    assert sc.script(IDS["window"]) == S["view"], "ProfileIconSelectView moved"
    mount = WindowMount()
    if any(sc.has(g) for g in IDS["retired"]):
        clash = mount.ids() & set(sc.order)
        assert not clash, f"minted fileIDs already present: {sorted(clash)[:3]}"
    for go, name in IDS["retired"].items():
        if sc.has(go):
            assert sc.field(go, "m_Name") == name, f"retired {go} is no longer {name}"
            for fid in dict.fromkeys(C.subtree(sc, go)):
                if sc.has(fid):
                    sc.remove(fid)
    for fid in list(sc.order):
        if fid in mount.ids():
            sc.remove(fid)
    win_rt = sc.rect_of(IDS["window_go"])
    anchor = win_rt
    for d in mount.docs(win_rt):
        sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    sc.replace(win_rt, T.set_block(sc.docs[win_rt], "m_Children", K.refs_list("m_Children", [mount.stub])))
    sc.replace(IDS["window"], rebuild_view_doc(sc.docs[IDS["window"]], mount))
    return sc.text(), mount


def outputs():
    out = prefab_outputs()
    out[SCENE], _ = scene_transform(open(os.path.join(REPO, SCENE), encoding="utf-8").read())
    return out


# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------

def prefab_source(guid, files):
    for name in PREFABS:
        if P.asset_guid(name) == guid:
            return files[prefab_path(name)]
    for name in P.PREFABS:
        if P.asset_guid(name) == guid:
            return open(os.path.join(REPO, P.prefab_path(name)), encoding="utf-8").read()
    for name in C.KIT_SOURCES:
        if K.prefab_guid(name) == guid:
            return open(os.path.join(REPO, K.PREFAB_DIR, name + ".prefab"), encoding="utf-8").read()
    return None


def prefab_objects(guid, files, _memo=None):
    memo = {} if _memo is None else _memo
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
TAB_FIELDS = dict(focusRing=IMG, label=TMP, underline=IMG)
# The view's fields as the scene stubs carry them: stub kind -> (class, script).
KIND_WANT = {k: C.STUB_KIND[k] for k in ("grid", "input", "button", "ttab", "cg", "entrance", "img", "tmp", "kt")}


def validate(files):
    guids = T.known_guids() | PACKAGE
    minted = {re.search(r"^guid: (\w+)$", t, re.M).group(1) for p, t in files.items() if p.endswith(".meta")}
    minted |= {P.asset_guid(n) for n in P.PREFABS}
    memo = {}
    for path, text_ in files.items():
        if path.endswith(".meta") or path == SCENE:
            continue
        A.check_file(path, text_, guids, minted)
        check_sources(path, text_, files, memo)

    btn = files[prefab_path("ProfileAvatarButton")]
    _, body = A.doc_of(btn, K.FileIds("ProfileAvatarButton")("root#avatar"))
    assert body and S["avatar"] in body, "ProfileAvatarButton: the root carries no ProfileIconSelectButton"
    C.wired(btn, body, "IconImage", IMG, "ProfileAvatarButton")
    C.wired(btn, body, "tile", S["tile"], "ProfileAvatarButton")
    assert K.sprite_ref(K.kit_sprite("kit_sliver14_line1")) not in btn, "ProfileAvatarButton: the octagon tile swaps to a sliver border"

    view = files[prefab_path("ProfileWindowView")]
    for key in ("Window/Tabs/Avatar#tab", "Window/Tabs/Name#tab"):
        _, tb = A.doc_of(view, K.FileIds("ProfileWindowView")(key))
        assert tb and S["tab"] in tb, f"ProfileWindowView: {key} is not a KitTextTab"
        for field, want in TAB_FIELDS.items():
            C.wired(view, tb, field, want, key)
    _, inp = A.doc_of(view, K.FileIds("ProfileWindowView")("Window/NamePage/Field#input"))
    for field, want in (("m_TextViewport", 224), ("m_TextComponent", TMP), ("m_Placeholder", TMP), ("m_TargetGraphic", IMG)):
        C.wired(view, inp, field, want, "the name field")
    assert "m_OnSubmit:\n    m_PersistentCalls:\n      m_Calls: []" in inp, "the name field carries a persistent submit call"
    assert view.count("guid: " + S["close"]) == 1, "the window needs exactly one ModalWindowCloseButton"

    sc = T.Scene(files[SCENE])
    anchors = set(sc.order) | {"0"}
    for fid in sc.order:
        for m in re.finditer(r"\{fileID: (-?\d+)\}", sc.docs[fid]):
            assert m.group(1) in anchors, f"dangling local fileID {m.group(1)} in {fid}"
    for fid in sc.order:
        check_sources(SCENE, sc.docs[fid], files, memo)
    for go, name in IDS["retired"].items():
        assert not sc.has(go), f"the profile window still holds retired {name}"
    mount = WindowMount()
    doc = sc.docs[IDS["window"]]
    for field, (key, kind) in VIEW_REFS.items():
        fid = mount.stub_fid(key)
        assert f"  {field}: {{fileID: {fid}}}" in doc, f"ProfileIconSelectView.{field} does not point into the view"
        cls, scr = KIND_WANT[kind]
        got = sc.cls.get(fid)
        assert got == cls and (scr is None or sc.script(fid) == scr), f"ProfileIconSelectView.{field} stub is the wrong kind"
    for field, items in (("ruleLabels", RULE_LABELS), ("ruleMarks", RULE_MARKS)):
        assert K.refs_list(field, [mount.stub_fid(k) for k, _ in items]) in doc, f"ProfileIconSelectView.{field} is not the four rules"
    assert f"iconButtonPrefab: {{fileID: {K.FileIds('ProfileAvatarButton')('root#avatar')}, guid: {P.asset_guid('ProfileAvatarButton')}" in doc, \
        "the window does not spawn ProfileAvatarButton"
    for gone in ("displayNamePanel", "avatarPanelRoot"):
        assert f"  {gone}:" not in doc, f"ProfileIconSelectView still serializes the retired {gone}"
    win_rt = sc.rect_of(IDS["window_go"])
    assert K.refs_list("m_Children", [mount.stub]) in sc.docs[win_rt], "the window holds more than its view"


def drift(files):
    stale = []
    for path, text_ in files.items():
        full = os.path.join(REPO, path)
        if not os.path.exists(full) or open(full, encoding="utf-8").read() != text_:
            stale.append(path)
    return stale


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
    btn, view = prefab_path("ProfileAvatarButton"), prefab_path("ProfileWindowView")
    mount = WindowMount()
    cases = [
        ("an avatar button without its tile", lambda f: f.__setitem__(btn, re.sub(r"(  tile: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[btn]))),
        ("a tab with no underline", lambda f: f.__setitem__(view, re.sub(r"(  underline: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[view], count=1))),
        ("the Submit field nulled", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"  displayNameSaveButton: {{fileID: {mount.stub_fid('Submit|root#button')}}}", "  displayNameSaveButton: {fileID: 0}"))),
        ("a retired panel left behind", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            "  avatarTab:", "  avatarPanelRoot: {fileID: 0}\n  avatarTab:", 1))),
        ("a stub pointing at an object the view lacks", lambda f: f.__setitem__(SCENE, f[SCENE].replace(
            f"m_CorrespondingSourceObject: {{fileID: {K.FileIds('ProfileWindowView')('Window/NamePage#cg')},",
            "m_CorrespondingSourceObject: {fileID: 12345,", 1))),
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
    print(f"OK: {len(PREFABS)} profile-window prefabs and Menu_Main validate")
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
