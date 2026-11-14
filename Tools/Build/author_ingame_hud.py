#!/usr/bin/env python3
"""
Author ITEM 12 of the style-B redesign - the IN-GAME UI - in place in the shared in-game prefabs
(Docs/MENU_REDESIGN_B.md section 22; the approved "In-game HUD" page of the Menu Redesign B canvas).

The in-game canvas is ONE prefab (`_Prefabs/CORE/GameCanvas.prefab`, Docs/GAMECANVAS.md) that every
mode drops in, so it is restyled IN PLACE: every object keeps its GameObject, its fileID and its name,
and only its look changes. Nothing here is a scene edit - no game-mode scene carries an override of
any property written below (gamecanvas_unification_report.py --check holds them at zero).

12a - the HUD's always-on chrome:
    Ability row (LOCKED structure, Docs/claude/VESSEL_HUD_AND_ELEMENTS.md): only the chrome colours
        and the control chip change. AbilityLockupStyle.asset takes the style-B plate (Neutral Very
        Dark at 86%) and a Neutral Lightest slant edge; its geometry, petal ladder and upgrade signal
        are untouched. The chip becomes a style-B KEYCAP carrying the control's name (LT, RT, A, B -
        LSHIFT on a keyboard), so ControlGlyphSet.asset names each pad control (padLabel).
    Goal stack: each row's generated trapezoid plate is re-scripted under the SAME fileID as a slivered
        glass Image (kit_sliver10_fill, Neutral Very Dark at 55%); the label takes Chakra Petch SemiBold
        15 caps; the bloom rests dark (glow is state - it still flares on a score); the target half of
        the count wears Neutral Lightest; the bar wears the local pilot's domain colour (GoalStack.
        SetAccent, fed by MiniGameHUD).
    Domain score columns (DomainScorePanel.prefab, instantiated per domain at runtime): 168 x 96, a
        black base at 35% under Team Dark at 12% (never a full team fill), a slivered hairline in the
        domain's Light tone, a 44 x 3 team-colour cap on the top edge, the score in Aldrich 44, the
        pilot chips under it. The breathing glow rests dark and only flares on a score.
    Pause hex: a DomainVolumeIndicator is authored on the pause button so it can carry the centre glyph
        (chrome_pause) - the hex used to be a gauge with nothing saying what pressing it does.

STANDING DOWN: every edit is re-derived from the objects above each run (found by script guid and by
the fields that point at them, not by remembered ids); a re-run is a no-op (--check).

    python3 Tools/Build/author_ingame_hud.py              # write
    python3 Tools/Build/author_ingame_hud.py --check      # fail if any output drifted
    python3 Tools/Build/author_ingame_hud.py --self-test  # planted defects must all be caught
"""

import argparse
import importlib.util
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("author_menu_home", os.path.join(HERE, "author_menu_home.py"))
HM = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HM)

P, C, A, K, T, H = HM.P, HM.C, HM.A, HM.K, HM.T, HM.H
REPO = K.REPO
N, STRETCH, R, tok = K.Node, K.STRETCH, K.rect, K.tok

CANVAS = "Assets/_Prefabs/CORE/GameCanvas.prefab"
PANEL = "Assets/_Prefabs/UI Elements/In Game/DomainScorePanel.prefab"
LOCKUP = "Assets/Resources/AbilityLockupStyle.asset"
GLYPHS = "Assets/Resources/ControlGlyphSet.asset"


def script_guid(path):
    return K.meta_guid(path)


S = dict(
    goalrow=script_guid("Assets/_Scripts/UI/Elements/GoalRow.cs"),
    trapezoid=script_guid(next(os.path.join(dp, f)[len(REPO) + 1:] for dp, _, fs in os.walk(os.path.join(REPO, "Assets/_Scripts"))
                               for f in fs if f == "TrapezoidGraphic.cs")),
    volume=script_guid("Assets/_Scripts/UI/DomainVolumeIndicator.cs"),
    vui=script_guid(next(os.path.join(dp, f)[len(REPO) + 1:] for dp, _, fs in os.walk(os.path.join(REPO, "Assets/_Scripts"))
                         for f in fs if f == "VolumeUI.cs")),
    hud=script_guid("Assets/_Scripts/UI/MultiplayerHUD.cs"),
    panel=script_guid("Assets/_Scripts/UI/Elements/DomainScorePanel.cs"),
    themed=H.KIT_THEMED,
    image=K.IMAGE,
)
PAUSE_GLYPH = K.chrome_sprite("chrome_pause")
PANEL_ROOT_GO, PANEL_COMP = "1000000000000000001", "1000000000000000007"
PANEL = dict(path=PANEL, root_rt="1000000000000000002", vlg="1000000000000000005", sum_rt="2000000000000000002",
             sum_tmp="2000000000000000004", avatars_rt="3000000000000000002", accent_go="4000000000000000001",
             accent_rt="4000000000000000002", glow_rt="5000000000000000002", glow_le="5000000000000000005")
COL_W, COL_H, CAP_W, CAP_H = 168, 96, 44, 3

# Style B, Docs/MENU_REDESIGN_B.md section 22 (the board's ROW colours).
LOCKUP_FIELDS = {
    "plateColor": "{r: 0, g: 0.016, b: 0.122, a: 0.86}",          # Neutral Very Dark #00041F at 86%
    "slantEdgeColor": "{r: 0.455, g: 0.482, b: 0.678, a: 0.85}",  # Neutral Lightest #747BAD at 85%
    "lockedPlateColor": "{r: 0, g: 0.016, b: 0.122, a: 0.55}",    # the same surface, dimmer
}
# Elements carry no colour in any UI (Garrett's palette, 2026-10-24; Docs/UI_COLOR_RULES.md): the rev 3 lava lamp is
# off, so a card shows its element by its flower and glyph, not by a coloured plate, and nothing drifts behind it.
NO_ELEMENT_COLOUR_FIELDS = [("elementColour", "0"), ("lavaMotion", "0")]
KEYCAP_FIELDS = [
    ("keycapChips", "1"),
    ("keycapFillSprite", K.sprite_ref(K.kit_sprite("kit_sliver10_fill"))),
    ("keycapBorderSprite", K.sprite_ref(K.kit_sprite("kit_sliver10_line1"))),
    ("keycapFont", K.ref(11400000, K.SEMIBOLD[0], 2)),
    ("keycapTextSize", "13"),
    ("keycapPadding", "8"),
    ("keycapFillToken", str(K.TOKEN_INDEX["SurfaceVeryDark"])),
    ("keycapBorderToken", str(K.TOKEN_INDEX["SurfaceLight"])),
    ("keycapTextToken", str(K.TOKEN_INDEX["TextLight"])),
    ("keycapHeldFillToken", str(K.TOKEN_INDEX["TextLight"])),
    ("keycapHeldTextToken", str(K.TOKEN_INDEX["SurfaceBlack"])),
]
# HintBinding -> the control's name on a pad (InputDeviceIconSetSwitcher.HintBinding).
PAD_LABELS = {1: "A", 2: "Y", 3: "B", 4: "X", 5: "LB", 6: "RB", 7: "LT", 8: "RT"}

GOALROW_FIELDS = [
    ("primaryLabelSize", "15"), ("secondaryLabelSize", "13"),
    ("primaryGlowAlpha", "0"), ("secondaryGlowAlpha", "0"), ("glowPunchAlpha", "0.6"),
    ("primaryFillToken", str(K.TOKEN_INDEX["ControlActive"])),
    ("targetToken", str(K.TOKEN_INDEX["NeutralLightest"])),
]
RETIRED_GOALROW_FIELDS = ("primaryFillColor", "targetHexColor")
GOAL_PLATE_ALPHA = 0.55


# --------------------------------------------------------------------------------------------
# Text helpers
# --------------------------------------------------------------------------------------------

def set_field(doc, name, value):
    """Set a top-level field, appending it when the doc does not carry it yet (a new C# field)."""
    pat = re.compile(r"^  " + re.escape(name) + r":.*(?:\n    .*)?$", re.M)
    if pat.search(doc):
        return pat.sub(lambda _m: f"  {name}: {value}", doc, count=1)
    return doc.rstrip("\n") + f"\n  {name}: {value}\n"


def drop_field(doc, name):
    return re.sub(r"^  " + re.escape(name) + r":.*\n(?:    .*\n)?", "", doc, flags=re.M)


def insert_after_field(doc, after, name, value):
    """Place a new field directly after `after` (keeps an SO asset in its C# declaration order)."""
    if re.search(r"^  " + re.escape(name) + r":", doc, re.M):
        return set_field(doc, name, value)
    m = re.search(r"^  " + re.escape(after) + r":.*\n(?:    .*\n)*", doc, re.M)
    assert m, f"field {after} not found"
    return doc[:m.end()] + f"  {name}: {value}\n" + doc[m.end():]


class Patch(HM.Patch):
    def __init__(self, text_, key):
        super().__init__(text_)
        self.ctx = P.Ctx(key)

    def by_script(self, guid):
        return [fid for fid, d in self.sc.docs.items() if f"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}" in d]

    def ref(self, fid, name):
        m = re.search(r"^  " + re.escape(name) + r": \{fileID: (-?\d+)\}", self.doc(fid), re.M)
        return m.group(1) if m else None


# --------------------------------------------------------------------------------------------
# 12a - ability row chrome (style + glyph assets)
# --------------------------------------------------------------------------------------------

def lockup_text(text_):
    for k, v in LOCKUP_FIELDS.items():
        text_ = C.field_set(text_, k, v)
    after = "bloomPadding"
    for k, v in KEYCAP_FIELDS:
        text_ = insert_after_field(text_, after, k, v)
        after = k
    after = "plateColor"
    for k, v in NO_ELEMENT_COLOUR_FIELDS:
        text_ = insert_after_field(text_, after, k, v)
        after = k
    return text_


def glyphs_text(text_):
    def entry(m):
        binding = int(m.group(1))
        if binding not in PAD_LABELS:
            # A binding drawn by its sprites alone (Garrett's Rhino 13 two-trigger / 14 sticks-apart, 2026-10-17):
            # leave the entry exactly as its author wrote it.
            return m.group(0)
        body = re.sub(r"^    padLabel: .*\n", "", m.group(2), flags=re.M)
        label = PAD_LABELS[binding]
        body = re.sub(r"(    padGlyphHeld: .*\n)", lambda mm: mm.group(1) + f"    padLabel: {label}\n", body, count=1)
        return f"  - binding: {binding}\n" + body
    return re.sub(r"^  - binding: (\d+)\n((?:    .*\n)*)", entry, text_, flags=re.M)


# --------------------------------------------------------------------------------------------
# 12a - goal stack and pause hex (GameCanvas)
# --------------------------------------------------------------------------------------------

def label_doc(doc, size):
    doc = C.field_set(doc, "m_fontAsset", K.ref(11400000, K.SEMIBOLD[0], 2))
    doc = C.field_set(doc, "m_sharedMaterial", K.ref(K.SEMIBOLD[1], K.SEMIBOLD[0], 2))
    for f, v in (("m_fontSize", size), ("m_fontSizeBase", size), ("m_fontStyle", "16"), ("m_characterSpacing", "12")):
        doc = C.field_set(doc, f, v)
    return doc


def patch_goal_rows(p):
    rows = p.by_script(S["goalrow"])
    assert len(rows) == 3, f"GameCanvas should carry 3 GoalRows, found {len(rows)}"
    for row in rows:
        d = p.doc(row)
        for f in RETIRED_GOALROW_FIELDS:
            d = drop_field(d, f)
        for f, v in GOALROW_FIELDS:
            d = set_field(d, f, v)
        p.replace(row, d)
        plate = p.ref(row, "plate")
        go = p.sc.go_of(plate)
        # The generated trapezoid becomes a slivered glass Image under the SAME fileID, so
        # GoalRow.plate (a Graphic) still points at it.
        p.body(plate, HM.img_body(tok("SurfaceVeryDark", GOAL_PLATE_ALPHA), K.kit_sprite("kit_sliver10_fill"), sliced=True), go)
        p.add_comp(go, "th", 114, K.themed("SurfaceVeryDark", GOAL_PLATE_ALPHA))
        label = p.ref(row, "label")
        p.replace(label, label_doc(p.doc(label), "15"))


def pause_button_go(p):
    vui = p.by_script(S["vui"])
    assert len(vui) == 1, "GameCanvas should carry exactly one VolumeUI (the pause button)"
    return p.sc.go_of(vui[0])


def patch_pause(p):
    go = pause_button_go(p)
    assert p.sc.field(go, "m_Name") == "Volume / Pause Button", "the pause button was renamed"
    fid = p.add_comp(go, "volume", 114, lambda ctx, g: K.mono(g, S["volume"],
        "  hexGraphic: {fileID: 0}\n"
        f"  centreGlyph: {K.sprite_ref(PAUSE_GLYPH)}\n"
        "  centreGlyphSize: 44\n"
        "  padKeycap: Start\n"
        "  keyboardKeycap: Esc\n"
        "  themeDataOverride: {fileID: 0}\n"
        "  sampleIntervalSeconds: 0.25\n"
        "  fillLerpSpeed: 8\n"
        "  explicitCell: {fileID: 0}\n"))
    huds = p.by_script(S["hud"])
    assert len(huds) == 1, "GameCanvas should carry one MultiplayerHUD"
    p.replace(huds[0], C.field_set(p.doc(huds[0]), "volumeIndicator", f"{{fileID: {fid}}}"))


# --------------------------------------------------------------------------------------------
# 12b - Ready, waiting for pilots, the countdown (GameCanvas)
# --------------------------------------------------------------------------------------------

READY_W, READY_H = 300, 76
READY_POS = (0, -200)
WAIT_W, WAIT_H = 600, 84
WAIT_POS = (0, -222)
CHIP_W, CHIP_H, CHIP_RING = 60, 80, 52
BEAT_SIZE = 168        # Rev 33 board A: the numbers in their hex (CountdownTimer.numberSize; GO is goSize, 132)
BEAT_HEX_R = 124       # the count's hex, centre to corner
READY_CAPTION = "The match starts when every pilot is ready."
WAIT_TITLE = "Waiting for pilots"
WAIT_NOTE = "You are ready. The countdown starts with the last pilot."
TOAST_CHANNEL = K.meta_guid("Assets/Resources/Channels/GameToastChannel.asset")
COUNTDOWN_PREFAB = K.meta_guid("Assets/_Prefabs/UI Elements/In Game/CountdownTimer.prefab")
COUNTDOWN_COMP = "1536147401936628604"      # CountdownTimer.prefab's CountdownTimer component
S.update(kitbutton=K.kit_script("KitButton"), button=A.BUTTON, cbp=script_guid(next(
    os.path.join(dp, f)[len(REPO) + 1:] for dp, _, fs in os.walk(os.path.join(REPO, "Assets/_Scripts"))
    for f in fs if f == "ControllerButtonPress.cs")),
    hudview=script_guid("Assets/_Scripts/UI/View/MultiplayerHUDView.cs"),
    waiting=C.script("ReadyWaitingPanel"), chip=C.script("ReadyWaitingChip"))


def ready_nodes():
    """FocusRing, Fill, Border, KeycapSlot, Label, Caption - under the Ready button's own RT."""
    border, fillc, content = K.button_palette("Play", True)
    ring = N("Ready/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10)))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Cta", 0.0), K.kit_sprite("kit_sliver10_line2"), True)) \
        .add("le", 114, H.le(ignore=True))
    fill = N("Ready/Fill", "Fill", STRETCH)
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)) \
        .add("grad", 114, lambda ctx, go: K.mono(go, K.kit_script("KitGradient"), "  lightenRight: 0.5\n")) \
        .add("le", 114, H.le(ignore=True))
    bord = N("Ready/Border", "Border", STRETCH)
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)) \
        .add("le", 114, H.le(ignore=True))
    kw, kh = K.label_width("A", "Label") + 24, K.line_height("Label") + 8
    slot = N("Ready/KeycapSlot", "KeycapSlot", R(size=(kw, kh)))
    slot.add("le", 114, H.le(pw=kw, ph=kh))
    slot.children.append(P.Inst("ReadyKeycap", "KitKeycap", "Keycap", STRETCH, [("Label#tmp", "m_text", "A", None)]))
    lab = N("Ready/Label", "Label", R(size=(K.label_width("READY", "Button"), K.line_height("Button"))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp("Ready", "Button", content)) \
        .add("kt", 114, K.kit_text("Button", driven=True))
    cap = N("Ready/Caption", "Caption", R((0.5, 0), (0.5, 0), (0, -16), (720, 24), (0.5, 1)))
    cap.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text(READY_CAPTION, "BodySmall", tok("TextLight"), align="center")) \
        .add("kt", 114, K.kit_text("BodySmall", "TextLight")).add("le", 114, H.le(ignore=True)) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Fade", delay=0.18))
    return [ring, fill, bord, slot, lab, cap]


def ready_button_doc(old, ctx):
    head, rest = old.split("  m_Script:", 1)
    doc = head + f"  m_Script: {K.ref(11500000, S['kitbutton'])}\n" + rest.split("\n", 1)[1]
    doc = re.sub(r"^  m_TargetGraphic: .*$", f"  m_TargetGraphic: {{fileID: {ctx.c('Ready/Fill', 'img')}}}", doc, flags=re.M)
    doc = re.sub(r"\n  theme: .*\Z", "\n", doc, flags=re.S)          # re-run: drop the KitButton tail
    return (doc.rstrip("\n") + "\n"
            + f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {{fileID: {ctx.c('Ready/FocusRing', 'img')}}}\n"
            + f"  variant: {K.VARIANTS['Play']}\n  fill: 1\n"
            + f"  fillGraphic: {{fileID: {ctx.c('Ready/Fill', 'img')}}}\n"
            + f"  borderGraphic: {{fileID: {ctx.c('Ready/Border', 'img')}}}\n"
            + K.refs_list("contentGraphics", [ctx.c("Ready/Label", "tmp")])
            + f"  playGradient: {{fileID: {ctx.c('Ready/Fill', 'grad')}}}\n")


def hud_view(p):
    v = p.by_script(S["hudview"])
    assert len(v) == 1, "GameCanvas should carry one MultiplayerHUDView"
    return v[0]


def patch_ready(p):
    view = hud_view(p)
    btn = p.ref(view, "readyButton")
    go = p.sc.go_of(btn)
    assert p.sc.field(go, "m_Name") == "ReadyButton", "the Ready button was renamed"
    rt = p.sc.rect_of(go)
    img = next(c for c in p.comp_ids(go) if p.sc.has(c) and f"guid: {K.IMAGE}" in p.doc(c))
    cbp = next(c for c in p.comp_ids(go) if p.sc.has(c) and f"guid: {S['cbp']}" in p.doc(c))
    # The legacy controller-hint child (a 48 px pad glyph in the corner) is replaced by the A keycap.
    ours = {str(p.ctx.rt(n.key)) for n in ready_nodes()}
    for kid in p.kids(rt):
        if kid not in ours:
            p.remove_subtree(p.sc.go_of(kid))
    p.field(cbp, "activationButtonImage", "{fileID: 0}")
    p.rect(rt, R((0.5, 0.5), (0.5, 0.5), READY_POS, (READY_W, READY_H)))
    p.body(img, HM.img_body((1.0, 1.0, 1.0, 0.0), raycast=True), go)
    new_ids, anchor = [], rt
    for n in ready_nodes():
        new_ids.append(p.emit(n, rt, anchor))
        anchor = HM._last_doc(p, n)
    p.children(rt, new_ids)
    p.replace(btn, ready_button_doc(p.doc(btn), p.ctx))
    p.add_comp(go, "hlg", 114, K.hv_layout(False, (32, 32, 0, 0), 14, 4, (1, 1), (0, 0)))
    # Motion: the button rises in every time it is shown (ToggleReadyButton enables it).
    p.add_comp(go, "cg", 225, H.canvas_group(True))
    p.add_comp(go, "in", 114, HM.M.entrance("Rise", distance=18))


def chip_template():
    n = N("Wait/ChipTemplate", "ChipTemplate", R((0.5, 0), (0.5, 0), (0, -400), (CHIP_W, CHIP_H), (0.5, 1)))
    n.add("cg", 225, H.canvas_group(False))
    ring = n.child(N("Wait/Chip/Ring", "Ring", R((0.5, 1), (0.5, 1), (0, 0), (CHIP_RING, CHIP_RING), (0.5, 1))))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_oct_fill"), True))
    clip = ring.child(N("Wait/Chip/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-6, -6))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_oct_fill"), True)) \
        .add("mask", 114, A.mask(False))
    av = clip.child(N("Wait/Chip/Avatar", "Avatar", STRETCH))
    av.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, A.AVATAR_PLACEHOLDER))
    st = n.child(N("Wait/Chip/State", "State", R((0, 0), (1, 0), (0, 0), (20, 18), (0.5, 0))))
    st.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("Picking", "Label", tok("NeutralLightest"), align="center"))
    n.add("chip", 114, lambda ctx, go: K.mono(go, S["chip"],
        f"  ring: {{fileID: {ctx.c('Wait/Chip/Ring', 'img')}}}\n  avatar: {{fileID: {ctx.c('Wait/Chip/Avatar', 'img')}}}\n"
        f"  state: {{fileID: {ctx.c('Wait/Chip/State', 'tmp')}}}\n  group: {{fileID: {ctx.c('Wait/ChipTemplate', 'cg')}}}\n"
        "  readyPunch: 0.2\n"))
    return n


def waiting_node():
    n = N("Wait", "ReadyWaiting", R((0.5, 0.5), (0.5, 0.5), WAIT_POS, (WAIT_W, WAIT_H)))
    n.add("cg", 225, H.canvas_group(False)).add("in", 114, HM.M.entrance("Rise", distance=18, play_on_enable=False))
    H.framed(n, "Wait", 14, "SurfaceVeryDark", "SurfaceLight", fill_alpha=0.92)
    dia = A.image_node(n, "Wait/Diamond", "Diamond", HM.TL(26, 37, 12, 12), "NeutralLightest", K.chrome_sprite("chrome_diamond"), preserve=True)
    dia.add("breathe", 114, HM.M.ambient("Breathe", period=1.6, amplitude=0.6))
    H.label(n.child(N("Wait/Title", "Title", HM.TL(54, 18, 420, 20))), WAIT_TITLE, "Label", "TextLight")
    H.label(n.child(N("Wait/Note", "Note", HM.TL(54, 44, 440, 22))), WAIT_NOTE, "BodySmall", "NeutralLightest")
    cnt = n.child(N("Wait/Count", "Count", R((1, 0.5), (1, 0.5), (-26, 0), (110, 40), (1, 0.5))))
    cnt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("1 / 2", "H2", tok("TextLight"), align="right")) \
        .add("kt", 114, K.kit_text("H2", "TextLight"))
    roster = n.child(N("Wait/Roster", "Roster", R((0.5, 0), (0.5, 0), (0, -14), (WAIT_W, CHIP_H), (0.5, 1))))
    roster.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 1, (0, 0), (0, 0)))
    n.children.append(chip_template())
    n.add("panel", 114, lambda ctx, go: K.mono(go, S["waiting"],
        f"  canvasGroup: {{fileID: {ctx.c('Wait', 'cg')}}}\n  countText: {{fileID: {ctx.c('Wait/Count', 'tmp')}}}\n"
        f"  roster: {{fileID: {ctx.rt('Wait/Roster')}}}\n  chipTemplate: {{fileID: {ctx.c('Wait/ChipTemplate', 'chip')}}}\n"
        f"  toastChannel: {K.ref(11400000, TOAST_CHANNEL, 2)}\n"
        f"  entrance: {{fileID: {ctx.c('Wait', 'in')}}}\n"
        "  readyWord: Ready\n  waitingWord: Picking\n"
        f"  waitingRingToken: {K.TOKEN_INDEX['SurfaceDark']}\n  readyTextToken: {K.TOKEN_INDEX['Cta']}\n"
        f"  waitingTextToken: {K.TOKEN_INDEX['NeutralLightest']}\n  waitingAlpha: 0.5\n"))
    return n


def beat_node():
    n = N("Beat", "Beat", R((0.5, 0.5), (0.5, 0.5), (0, 0), (600, 400)))

    def body(ctx, go):
        # No auto-size: it would cap the beat at the Display role's 48 px (it did, until Rev 33), and CountdownTimer
        # sets the size per beat (numberSize, goSize).
        b = H.text("3", "Display", tok("TextLight"), align="center")(ctx, go)
        b = sub1(b, f"  m_fontSize: {K.f32(48)}\n  m_fontSizeBase: {K.f32(48)}\n",
                 f"  m_fontSize: {BEAT_SIZE}\n  m_fontSizeBase: {BEAT_SIZE}\n")
        return sub1(b, f"  m_enableAutoSizing: 1\n  m_fontSizeMin: {K.f32(K.fit_min(48))}\n  m_fontSizeMax: {K.f32(48)}\n",
                    f"  m_enableAutoSizing: 0\n  m_fontSizeMin: {BEAT_SIZE}\n  m_fontSizeMax: {BEAT_SIZE}\n")
    n.add("cr", 222, K.canvas_renderer()).add("tmp", 114, body)
    return n


def beat_hex_nodes():
    """The count's hex (fills through each number; bursts at GO) and the lime ring that leaves it at GO."""
    s = round(BEAT_HEX_R / 0.4, 2)
    hexn = N("Beat/Hex", "BeatHex", R((0.5, 0.5), (0.5, 0.5), (0, 0), (s, s)))
    hexn.add("cr", 222, K.canvas_renderer()).add("hex", 114, crystal_graphic(
        (1, 1, 1, 1), glass=tok("SurfaceVeryDark", 0.86), width=3, fill_alpha=1)).add("cg", 225, H.canvas_group(False))
    ring = N("Beat/GoRing", "GoRing", R((0.5, 0.5), (0.5, 0.5), (0, 0), (s, s)))
    ring.add("cr", 222, K.canvas_renderer()).add("hex", 114, crystal_graphic(tok("Cta"), width=4, color=(1, 1, 1, 0)))
    return [hexn, ring]


def countdown_instance(p):
    pis = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1001 ") and f"m_SourcePrefab: {{fileID: 100100000, guid: {COUNTDOWN_PREFAB}" in d]
    assert len(pis) == 1, "GameCanvas should nest one CountdownTimer"
    return pis[0]


def set_instance_ref(p, pi, comp_src, prop, fid):
    """(Re)write one objectReference modification on a nested instance."""
    d = p.doc(pi)
    entry = (f"    - target: {{fileID: {comp_src}, guid: {COUNTDOWN_PREFAB},\n        type: 3}}\n"
             f"      propertyPath: {prop}\n      value: \n      objectReference: {{fileID: {fid}}}\n")
    d = re.sub(r"    - target: \{fileID: %s, guid: %s,\n        type: 3\}\n      propertyPath: %s\n(?:      .*\n)*?      objectReference: .*\n"
               % (comp_src, COUNTDOWN_PREFAB, re.escape(prop)), "", d)
    d = d.replace("    m_Modifications:\n", "    m_Modifications:\n" + entry, 1)
    p.replace(pi, d)


def instance_ref(p, pi, prop):
    m = re.search(r"      propertyPath: %s\n      value: .*\n      objectReference: \{fileID: (-?\d+)\}" % re.escape(prop), p.doc(pi))
    return m.group(1) if m else None


def patch_waiting_and_countdown(p):
    view = hud_view(p)
    hud_rt = p.sc.rect_of(p.sc.go_of(view))
    ready_rt = p.sc.rect_of(p.sc.go_of(p.ref(view, "readyButton")))
    wait_rt = p.emit(waiting_node(), hud_rt, HM._last_doc(p, ready_nodes()[-1]))
    kids = [k for k in p.kids(hud_rt) if k != wait_rt]
    kids.insert(kids.index(ready_rt) + 1, wait_rt)
    p.children(hud_rt, kids)
    p.replace(view, set_field(p.doc(view), "readyWaiting", f"{{fileID: {p.ctx.c('Wait', 'panel')}}}"))
    # The countdown: a 260 px Aldrich beat under the existing display, wired into the nested timer.
    pi = countdown_instance(p)
    disp = instance_ref(p, pi, "countdownDisplay")
    assert disp, "the CountdownTimer's display is unwired"
    disp_go = p.sc.go_of(disp)
    disp_rt = p.sc.rect_of(disp_go)
    hex_ids, after = [], disp
    for n in beat_hex_nodes():
        hex_ids.append(p.emit(n, disp_rt, after))
        after = HM._last_doc(p, n)
    beat_rt = p.emit(beat_node(), disp_rt, after)
    p.children(disp_rt, [k for k in p.kids(disp_rt) if k not in hex_ids and k != beat_rt] + hex_ids + [beat_rt])
    p.rect(disp_rt, R((0.5, 0.5), (0.5, 0.5), (0, 0), (600, 400)))
    set_instance_ref(p, pi, COUNTDOWN_COMP, "countdownText", p.ctx.c("Beat", "tmp"))
    set_instance_ref(p, pi, COUNTDOWN_COMP, "waitingPanel", p.ctx.c("Wait", "panel"))
    set_instance_ref(p, pi, COUNTDOWN_COMP, "beatHex", p.ctx.c("Beat/Hex", "hex"))
    set_instance_ref(p, pi, COUNTDOWN_COMP, "beatHexGroup", p.ctx.c("Beat/Hex", "cg"))
    set_instance_ref(p, pi, COUNTDOWN_COMP, "goRing", p.ctx.c("Beat/GoRing", "hex"))


# --------------------------------------------------------------------------------------------
# 12c - the pause window (R_Pause_Menu_Panel.prefab, nested in GameCanvas)
# --------------------------------------------------------------------------------------------

PAUSE = "Assets/_Prefabs/UI Elements/Panels/R_Pause_Menu_Panel.prefab"
PAUSE_GUID = K.meta_guid(PAUSE)
PAUSE_ROOT_GO, PAUSE_ROOT_RT, PAUSE_COMP = "2010564259793320838", "2565551909505468755", "5080318904164001789"
# The legacy Buttons subtree (RESUME on Y, MAIN MENU on A), measured 2026-10-15. Deleted here; the
# canvas's overrides into it are pruned with it, and validation proves none survives.
LEGACY_PAUSE_IDS = frozenset("""2798801317077596910 2771496209953452151 7953647533024396834 1647945603392338223
7897223945397071121 3047425174249578987 7426885803112301023 2187375227035800055 7981886868796528559 9048880161382045756
1629839579805033125 3912233643792528712 917713729474389886 4371548018803103402 1584814524383992767 2510543142522570841
3270211977748828679 3662333403955279312 833406477967934425 2015185681349518760 5188429541548807423 1655856202926348515
5099527740768031357 8521775435001194992 8468013336432161012 8794888177767081067 2138776890088369646 7265396418819021505
8533151908561320973 4533968937199431159 6660150165728680669 732860958106874703""".split())
WIN_W, WIN_H, WIN_PAD = 1000, 440, 44
BTN_W, BTN_H, BTN_GAP = 340, 64, 14
ROWS_Y, ROW_H = 160, 52
S.update(pause=script_guid("Assets/_Scripts/UI/PauseMenu.cs"))
# GamepadButton: North 4, East 5, South 6, West 7.
PAUSE_BUTTONS = [  # key, caption, variant, opaque, keycap, pad button (ControllerButtonPress)
    ("Resume", "Resume", "Play", True, "Start", 5),
    ("Settings", "Settings", "Default", False, "X", 7),
    ("Leave", "Leave match", "Cancel", False, "Y", 4),
]


def cbp_body(pad_button, group_key):
    return lambda ctx, go: K.mono(go, S["cbp"],
        "  ActiveMenuScreens: \n  ActiveModalWindows: \n"
        f"  activationButton: {pad_button}\n  activationButtonImage: {{fileID: 0}}\n"
        "  TriangleButtonSprite: {fileID: 0}\n  CircleButtonSprite: {fileID: 0}\n  CrossButtonSprite: {fileID: 0}\n"
        "  SquareButtonSprite: {fileID: 0}\n  YButtonSprite: {fileID: 0}\n  BButtonSprite: {fileID: 0}\n"
        "  AButtonSprite: {fileID: 0}\n  XButtonSprite: {fileID: 0}\n"
        f"  canvasGroup: {{fileID: {ctx.c(group_key, 'cg') if group_key else 0}}}\n")


def pause_button(i, key, caption, variant, opaque, cap, pad):
    b = kit_button(f"Pause/{key}", f"{key}Button", HM.TL(WIN_W - WIN_PAD - BTN_W, 36 + i * (BTN_H + BTN_GAP), BTN_W, BTN_H),
                   variant, opaque, caption, cap, pad, "Pause/Window", f"Pause{key}Keycap")
    # Motion: the three actions step in after the window, one stagger apart.
    b.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", stagger=i + 2, distance=10))
    return b


def kit_button(k, name, r, variant, opaque, caption, cap, pad, group_key=None, keycap_key=None):
    """A KitButton with its focus ring, fill, border, keycap and label, answering pad button `pad`
    through ControllerButtonPress (gated by `group_key`'s CanvasGroup when given)."""
    border, fillc, content = K.button_palette(variant, opaque)
    n = N(k, name, r)
    ring = n.child(N(f"{k}/FocusRing", "FocusRing", R((0, 0), (1, 1), (0, 0), (10, 10))))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Cta", 0.0), K.kit_sprite("kit_sliver10_line2"), True)) \
        .add("le", 114, H.le(ignore=True))
    fill = n.child(N(f"{k}/Fill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(fillc, K.kit_sprite("kit_sliver10_fill"), True, raycast=True)) \
        .add("grad", 114, lambda ctx, go: K.mono(go, K.kit_script("KitGradient"), "  lightenRight: 0.5\n", enabled=opaque)) \
        .add("le", 114, H.le(ignore=True))
    bord = n.child(N(f"{k}/Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(border, K.kit_sprite("kit_sliver10_line1"), True)) \
        .add("le", 114, H.le(ignore=True))
    kw, kh = K.label_width(cap.upper(), "Label") + 24, K.line_height("Label") + 8
    slot = n.child(N(f"{k}/KeycapSlot", "KeycapSlot", R(size=(kw, kh))))
    slot.add("le", 114, H.le(pw=kw, ph=kh))
    slot.children.append(P.Inst(keycap_key or k.replace("/", "") + "Keycap", "KitKeycap", "Keycap", STRETCH, [("Label#tmp", "m_text", cap, None)]))
    lab = n.child(N(f"{k}/Label", "Label", R(size=(K.label_width(caption.upper(), "Button"), K.line_height("Button")))))
    lab.add("cr", 222, K.canvas_renderer()).add("tmp", 114, K.tmp(caption, "Button", content)) \
        .add("kt", 114, K.kit_text("Button", driven=True))
    n.add("button", 114, lambda ctx, go: K.mono(go, S["kitbutton"],
        K.SELECTABLE_FIELDS
        + f"  m_TargetGraphic: {{fileID: {ctx.c(f'{k}/Fill', 'img')}}}\n"
        "  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []\n"
        f"  theme: {K.ref(11400000, K.THEME, 2)}\n  focusRing: {{fileID: {ctx.c(f'{k}/FocusRing', 'img')}}}\n"
        f"  variant: {K.VARIANTS[variant]}\n  fill: {1 if opaque else 0}\n"
        f"  fillGraphic: {{fileID: {ctx.c(f'{k}/Fill', 'img')}}}\n"
        f"  borderGraphic: {{fileID: {ctx.c(f'{k}/Border', 'img')}}}\n"
        + K.refs_list("contentGraphics", [ctx.c(f"{k}/Label", "tmp")])
        + f"  playGradient: {{fileID: {ctx.c(f'{k}/Fill', 'grad')}}}\n"))
    n.add("hlg", 114, K.hv_layout(False, (24, 24, 0, 0), 12, 3, (1, 1), (0, 0)))
    n.add("cbp", 114, cbp_body(pad, group_key))
    return n


def pause_window():
    w = N("Pause/Window", "Window", R((0.5, 0.5), (0.5, 0.5), (0, 0), (WIN_W, WIN_H)))
    w.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", distance=14))
    H.framed(w, "Pause/Window", 14, "SurfaceBlack", "TextLight")
    H.label(w.child(N("Pause/Eyebrow", "Eyebrow", HM.TL(WIN_PAD, 36, 520, 20))), "Paused", "Label", "NeutralLightest")
    H.label(w.child(N("Pause/Title", "Title", HM.TL(WIN_PAD, 62, 520, 46))), "Skim Race", "H1", "TextLight")
    H.label(w.child(N("Pause/Detail", "Detail", HM.TL(WIN_PAD, 114, 520, 22))), "", "BodySmall", "NeutralLightest")
    for i in range(3):
        y = ROWS_Y + i * ROW_H
        row = w.child(N(f"Pause/Row{i}", f"Row{i}", HM.TL(WIN_PAD, y, 520, ROW_H)))
        row.add("cg", 225, H.canvas_group(True))
        A.image_node(row, f"Pause/Row{i}/Rule", "Rule", HM.TL(0, 0, 520, 1), "SurfaceDark")
        H.label(row.child(N(f"Pause/Row{i}/Label", "Label", HM.TL(0, 14, 300, 26))), "", "Body", "NeutralLightest")
        H.label(row.child(N(f"Pause/Row{i}/Value", "Value", HM.TL(240, 12, 280, 30))), "", "H3", "TextLight", align="right")
    for i, b in enumerate(PAUSE_BUTTONS):
        w.children.append(pause_button(i, *b))
    H.label(w.child(N("Pause/Note", "Note", HM.TL(WIN_W - WIN_PAD - BTN_W, 36 + 3 * (BTN_H + BTN_GAP) + 4, BTN_W, 22))),
            "B also resumes.", "BodySmall", "NeutralLightest")
    return w


def pause_scrim():
    n = N("Pause/Scrim", "Scrim", STRETCH)
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.78), raycast=True)) \
        .add("th", 114, H.themed("SurfaceBlack", 0.78)) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Fade", duration=0.2))
    return n


def pause_text(text_):
    p = Patch(text_, "PauseMenu/Item12")
    assert p.sc.script(PAUSE_COMP) == S["pause"], "PauseMenu moved"
    if p.sc.has("2798801317077596910"):
        p.remove_subtree("2798801317077596910")
    scrim = p.emit(pause_scrim(), PAUSE_ROOT_RT, PAUSE_COMP)
    win = p.emit(pause_window(), PAUSE_ROOT_RT, HM._last_doc(p, pause_scrim()))
    rest = [k for k in p.kids(PAUSE_ROOT_RT) if k not in (scrim, win) and k not in LEGACY_PAUSE_IDS]
    p.children(PAUSE_ROOT_RT, [scrim, win] + rest)
    c = p.ctx.c
    comp = p.doc(PAUSE_COMP)
    for f, v in (("replayButton", "{fileID: 0}"), ("mainMenuButton", f"{{fileID: {p.ctx.go('Pause/Leave')}}}"),
                 ("mainMenuButtonLabel", f"{{fileID: {c('Pause/Leave/Label', 'tmp')}}}"),
                 ("gameController", "{fileID: 0}"),
                 ("resumeButton", f"{{fileID: {c('Pause/Resume', 'button')}}}"),
                 ("settingsButton", f"{{fileID: {c('Pause/Settings', 'button')}}}"),
                 ("leaveButton", f"{{fileID: {c('Pause/Leave', 'button')}}}"),
                 ("windowGroup", f"{{fileID: {c('Pause/Window', 'cg')}}}"),
                 ("modeTitle", f"{{fileID: {c('Pause/Title', 'tmp')}}}"),
                 ("modeDetail", f"{{fileID: {c('Pause/Detail', 'tmp')}}}")):
        comp = set_field(comp, f, v)
    for f, v in (("hostLeaveLabel", "Leave match"), ("guestLeaveLabel", "Leave party")):
        comp = set_field(comp, f, v)
    for name, key, kind in (("rowLabels", "Label", "tmp"), ("rowValues", "Value", "tmp"), ("rowGroups", None, "cg")):
        comp = re.sub(r"^  %s:\n(?:  - .*\n)*|^  %s: \[\]\n" % (name, name), "", comp, flags=re.M)
        ids = [c(f"Pause/Row{i}/{key}" if key else f"Pause/Row{i}", kind) for i in range(3)]
        comp = comp.rstrip("\n") + "\n" + K.refs_list(name, ids)
    p.replace(PAUSE_COMP, comp)
    return p.text()


# --------------------------------------------------------------------------------------------
# 12c - the connecting panel (ConnectingPanel.prefab, nested in GameCanvas): Rev 91 F, THE TOUR
# (Docs/MENU_REDESIGN_B.md section 173, board F; the drawing is Docs/MenuRedesignB/rounds/r91_loading.py), on the
# Rev 33 cover (section 99): it COVERS the arena (no camera renders behind it, so the build runs at the cell-change
# screen's tempo). ENTERING and the mode's name sit top left; the parts of the game are shown one chapter at a time
# (a picture left of centre, a counter, a title and one line on the right, a five-word index top right), the chapter
# following the load (ConnectingTourView); one bar cut into the load's real steps runs along the bottom (KitStepBar).
# At the end the bar reads Ready in Lime and a hex opens from the centre into the arena (ConnectingGateView.Open).
#
# What F no longer draws is removed here, never by hand: the Omni crystal, the tagline and chips, the steps panel,
# the pilots panel and the footer. Three things are KEPT because something outside this prefab points at them:
#   - the old Slider's objects are the bar's track and fill (GameCanvas overrides the Fill's anchor); the Slider
#     component itself is gone, KitStepBar anchors the fill;
#   - the roster (ConnectingPlayerRoster) and its list and template row (GameCanvas overrides the row) stay under a
#     zero-alpha CanvasGroup: the load still waits on every human pilot through it, and their count is the bar's last
#     share, but F draws no pilot rows.
# --------------------------------------------------------------------------------------------

CONNECT = "Assets/_Prefabs/UI Elements/In Game/ConnectingPanel.prefab"
WHITRABT = K.meta_guid("Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/whitrabt SDF.asset")
CN = dict(comp="6370090134550638757", root_go="470661231859203382", root_rt="5402023128368193864",
          root_img="2388631188305556708", safe_go="668988761015778786", safe_rt="268860854097267997",
          status_go="917125772582838926", mode_go="5753793761074810139", mode_rt="7234322143272291273",
          mode_tmp="6280339008487199906", rank_rt="6369488707559300757", rank_tmp="1901962995261546283",
          camera_go="6872256739989315075", preview_go="6537181835104226913",
          slider_go="6508048406648284931", slider_rt="4629373923633661124", track_go="5177954019794184100",
          track_rt="7173161466604345517", track_img="8125581187658145292", fillarea_rt="8176116645415605081",
          fill_go="8684485316077403885", fill_rt="4280864247464791669", fill_img="6946419630781211910",
          icons_go="9124959156195794125", icons_rt="3593977903660026307", roster="4166936055535670324",
          row_go="1082109306432636569", row_rt="5898718248706178780", row_img="2786987675188890769",
          avatar_rt="2055132934474971526", avatar_img="2038959525692544740")
S.update(connect=script_guid("Assets/_Scripts/UI/ConnectingPanelController.cs"),
         gate=script_guid("Assets/_Scripts/UI/Elements/ConnectingGateView.cs"),
         tour=script_guid("Assets/_Scripts/UI/Elements/ConnectingTourView.cs"),
         drift=script_guid("Assets/_Scripts/UI/Screens/PosterDrift.cs"),
         stepbar=K.kit_script("KitStepBar"),
         aperture=script_guid("Assets/_Scripts/UI/Elements/HexApertureGraphic.cs"),
         crystal=script_guid("Assets/_Scripts/Controller/Environment/Spawning/LoadCrystalGraphic.cs"),
         roster=script_guid("Assets/_Scripts/UI/Elements/ConnectingPlayerRoster.cs"),
         hlg=K.HLAYOUT, vlg=K.VLAYOUT)
ARCADE_GAMES = "402ca7eefc2aeb64baaa140c43312bcd"   # ArcadeGames.asset (SO_GameList)
INTRO_LIBRARY = K.meta_guid("Assets/Resources/IntroCameraLibrary.asset")
VEIL_WORDS = K.meta_guid("Assets/Resources/UI/EnvironmentLoadVeilContent.asset")
RECT_MASK_2D = "3312d7739989d2b4e91e6319e9a96d76"
# Every node this section ever generated. All are deleted on the way in and the current ones re-emitted, so a re-run
# is a no-op and a retired look leaves nothing behind. (Rev 3 readout column, the section 28 backdrop, the Rev 33
# cell gate's crystal, brief, panels and footer, then Rev 91 F's own.)
RETIRED_CONNECT_KEYS = ("Connect/Diamond", "Connect/Progress", "Connect/Elapsed", "Connect/Backdrop", "Connect/Dim",
                        "Connect/BottomFade", "Connect/Phase", "Gate/Crystal", "Gate/Tagline", "Gate/Chips",
                        "Gate/Steps", "Gate/Pilots", "Gate/Footer", "Gate/Row/Rule", "Gate/Row/Stripe",
                        "Gate/Row/Frame", "Gate/Row/NameRow", "Gate/Row/Line", "Gate/Row/Pip", "Gate/Row/State")
GATE_RING_W = 3

# --- Rev 91 F: the board's geometry (1920 x 1080) and its words ------------------------------
EDGE = 96                         # the board's side margin
SHOW = (180, 250, 900, 500)       # the picture's box (board left, top, width, height)
WORDS = (1180, 400, 644, 220)     # the counter, title and line
BAR_BOTTOM, BAR_H, TRACK_H = 64, 132, 8
TOUR_FADE = 0.4
COUNTER_TEMPLATE = "{n} of {total}"
# The tour, in order: (title, line, picture). Every word here lands in an authored field on ConnectingTourView.
TOUR = (("Squad", "Three seats, each a vessel and a pilot."),
        ("The Ark", "Converter, Squads, Artifacts and Decor are aboard."),
        ("Pilots", "Eighteen pilots, each known by a callsign."),
        ("Vessels", "Four abilities each: Charge, Mass, Space and Time."),
        ("Toy Box", "Seven toys. Fly one to use it."))


def pilot_portrait(callsign):
    """The painted portrait a roster pilot wears (its PilotSO's own sprite, so the tour and the roster agree)."""
    text_ = open(os.path.join(REPO, f"Assets/_SO_Assets/Pilots/Roster/Pilot_{callsign}.asset"), encoding="utf-8").read()
    m = re.search(r"^  portrait: \{fileID: 21300000, guid: ([0-9a-f]{32}), type: 3\}", text_, re.M)
    assert m, f"Pilot_{callsign} has no portrait"
    return m.group(1)


SQUAD_PILOTS = ("Needle", "Galleon", "Anvil")
PORTRAIT = {c: pilot_portrait(c) for c in SQUAD_PILOTS}
HULL_CARD = {h: K.meta_guid(f"Assets/_Graphics/CardImages/{h}_large.png") for h in ("Sparrow", "Dolphin")}
# The Ark has no picture among the game's sprites (HomeScreen/Ark.png is the nav button's label), so the board's own
# cut of it (Docs/MenuRedesignB/build_refs/ark_chase.png, from Docs/VesselModels/ArkReverse_sheet.png) is imported.
ARK_ART = K.meta_guid("Assets/_Graphics/UI/Loading/loading_tour_ark.png")

# The bar's steps are ArenaLoadProgress's measured shares, read from the C# so the two can never disagree: laying
# ends at LayCeiling, growing at GrowCeiling, the pilots' share at 1. The board drew a fourth, "Connecting to shore",
# first; nothing measures it (the model starts at laying), so it would be a segment of no length - it is not built.
_ALP = open(os.path.join(REPO, "Assets/_Scripts/UI/Elements/ArenaLoadProgress.cs"), encoding="utf-8").read()


def _share(name):
    m = re.search(r"public const float %s = ([0-9.]+)f;" % name, _ALP)
    assert m, f"ArenaLoadProgress.{name} is not a literal share any more"
    return float(m.group(1))


STEP_ENDS = (_share("LayCeiling"), _share("GrowCeiling"), 1.0)
STEP_NAMES = ("Building the arena", "Settling the arena", "Waiting for pilots")
DONE_WORD = "Ready"
PERCENT_TEMPLATE = "{pct}"


def tmp_restyle(doc, face, size, upper, spacing, align, token):
    font = K.SEMIBOLD if face == "Chakra" else K.ALDRICH
    doc = C.field_set(doc, "m_fontAsset", K.ref(11400000, font[0], 2))
    doc = C.field_set(doc, "m_sharedMaterial", K.ref(font[1], font[0], 2))
    for f, v in (("m_fontSize", size), ("m_fontSizeBase", size), ("m_fontStyle", 16 if upper else 0),
                 ("m_characterSpacing", spacing), ("m_HorizontalAlignment", {"left": 1, "center": 2, "right": 4}[align]),
                 ("m_enableAutoSizing", 1), ("m_fontSizeMin", K.fit_min(size)), ("m_fontSizeMax", size)):
        doc = C.field_set(doc, f, str(v))
    c = tok(token)
    doc = C.field_set(doc, "m_Color", K.col(c))
    doc = C.field_set(doc, "m_fontColor", K.col(c))
    doc = re.sub(r"(  m_fontColor32:\n    serializedVersion: 2\n    rgba: )\d+", lambda m: m.group(1) + str(K.col32(c)), doc, count=1)
    return doc


def sub1(s, old, new):
    assert s.count(old) == 1, f"expected one {old.strip()!r}"
    return s.replace(old, new)


def ctext(t, token, size, face="Chakra", spacing=0, upper=True, align="left", alpha=1.0):
    """A TMP at any size in one of the two kit faces, without KitText (which would force its role's size)."""
    role = "Label" if face == "Chakra" else "Body"
    base = H.text(t, role, tok(token, alpha), align=align)
    _, _, rsize, rspacing, rupper, _ = K.ROLES[role]

    def body(ctx, go):
        b = base(ctx, go)
        b = sub1(b, f"  m_fontSize: {K.f32(rsize)}\n  m_fontSizeBase: {K.f32(rsize)}\n",
                 f"  m_fontSize: {K.f32(size)}\n  m_fontSizeBase: {K.f32(size)}\n")
        b = sub1(b, f"  m_fontSizeMin: {K.f32(K.fit_min(rsize))}\n  m_fontSizeMax: {K.f32(rsize)}\n",
                 f"  m_fontSizeMin: {K.f32(K.fit_min(size))}\n  m_fontSizeMax: {K.f32(size)}\n")
        b = sub1(b, f"  m_fontStyle: {16 if rupper else 0}\n", f"  m_fontStyle: {16 if upper else 0}\n")
        return sub1(b, f"  m_characterSpacing: {K.f32(rspacing)}\n", f"  m_characterSpacing: {K.f32(spacing)}\n")
    return body


def graphic_fields(color, material=None, raycast=False):
    mat = f"{{fileID: 2100000, guid: {material}, type: 2}}" if material else "{fileID: 0}"
    return (f"  m_Material: {mat}\n  m_Color: {K.col(color)}\n  m_RaycastTarget: {1 if raycast else 0}\n"
            "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n"
            "  m_OnCullStateChanged:\n    m_PersistentCalls:\n      m_Calls: []\n")


def aperture(color, material=None):
    return lambda ctx, go: K.mono(go, S["aperture"], graphic_fields(color, material))


def crystal_graphic(outline, glass=(0, 0, 0, 0), spokes=False, spoke_alpha=0.35, fill_alpha=0.6, width=2.0, enabled=True,
                    color=(1, 1, 1, 1)):
    return lambda ctx, go: K.mono(go, S["crystal"], graphic_fields(color)
                                  + f"  outline: {K.col(outline)}\n  glass: {K.col(glass)}\n  spokes: {1 if spokes else 0}\n"
                                  f"  spokeAlpha: {K.f32(spoke_alpha)}\n  fillAlpha: {K.f32(fill_alpha)}\n"
                                  f"  outlineWidth: {K.f32(width)}\n", enabled=enabled)


def off(body):
    """A component authored disabled."""
    return lambda ctx, go: sub1(body(ctx, go), "  m_Enabled: 1\n", "  m_Enabled: 0\n")


def gimg(node, key, name, r, color, sprite=None, preserve=False):
    n = node.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(color, sprite, preserve=preserve))
    return n


def gtext(node, key, name, r, body):
    n = node.child(N(key, name, r))
    n.add("cr", 222, K.canvas_renderer()).add("tmp", 114, body)
    return n


def arrives(node, mode="Rise", delay=0.0, distance=14):
    node.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance(mode, delay=delay, distance=distance))
    return node


def centred(box):
    """A board box (left, top, width, height on the 1920 x 1080 board) as a rect anchored on the screen's centre."""
    x, y, w, h = box
    return R((0.5, 0.5), (0.5, 0.5), (x + w / 2 - 960, 540 - (y + h / 2)), (w, h))


# --- the cover and the ring (root children) -------------------------------------------------

def motion(seconds, offset=((0, 0), (0, 0)), turn=(0, 0), scale=(1, 1), phase=0.0):
    """PosterDrift: between two poses and back, eased both ways, seconds one way (the board's CSS `alternate`).
    Offsets are canvas units with y UP; the board's CSS translates are y DOWN, so their y is negated here."""
    return lambda ctx, go: K.mono(go, S["drift"],
                                  f"  offsetFrom: {K.v2(*offset[0])}\n  turnFrom: {K.f32(turn[0])}\n  scaleFrom: {K.f32(scale[0])}\n"
                                  f"  offsetTo: {K.v2(*offset[1])}\n  turnTo: {K.f32(turn[1])}\n  scaleTo: {K.f32(scale[1])}\n"
                                  f"  seconds: {K.f32(seconds)}\n  phase: {K.f32(phase)}\n")


# The board's sea (r91_loading.py `sea()`): three soft fields drifting slowly under the sea lines. Each is (token, alpha,
# the board's box left/top/width/height, seconds one way, the CSS translate pair, the CSS scale pair). The board's two
# indigos are not theme colours, so the fields wear the theme's blue family (SurfaceLight, NeutralLightest) and follow
# the worn colour set like every other token.
SEA_FIELDS = (("SurfaceLight", 0.50, (-300, -260, 1500, 1100), 26, ((-260, -120), (340, 140)), (1.0, 1.25)),
              ("NeutralLightest", 0.30, (760, 120, 1500, 1100), 34, ((220, 90), (-320, -160)), (1.2, 1.0)),
              ("SurfaceLight", 0.46, (260, 380, 1500, 1100), 42, ((-120, 160), (260, -80)), (1.1, 1.3)))
# The board's chapter motions: the Ark drifts (16 s, a slight turn), the Vessels chapter's hull bobs (6 s).
ARK_DRIFT = dict(seconds=16, offset=((-40, -14), (110, 22)), turn=(1, -1.5))
HULL_BOB = dict(seconds=6, offset=((0, 10), (0, -12)))


def sea_fields_node():
    """The drifting fields, in their own group: the gate fades it with the content (it has no hole of its own)."""
    n = N("Gate/Cover/Fields", "CoverFields", STRETCH)
    n.add("cg", 225, H.canvas_group(True))
    for i, (token, alpha, box, seconds, moves, scale) in enumerate(SEA_FIELDS):
        f = gimg(n, f"Gate/Cover/Fields/Field{i}", f"Field{i + 1}", centred(box), tok(token, alpha), K.kit_sprite("kit_ring_glow"))
        f.add("th", 114, H.themed(token, alpha))
        f.add("drift", 114, motion(seconds, offset=tuple((x, -y) for x, y in moves), scale=scale))
    return n


def cover_nodes():
    base = N("Gate/Cover/Base", "CoverBase", STRETCH)
    base.add("cr", 222, K.canvas_renderer()).add("hole", 114, aperture(tok("SurfaceBlack")))
    sea = N("Gate/Cover/Sea", "CoverSea", STRETCH)
    sea.add("cr", 222, K.canvas_renderer()).add("hole", 114, aperture((1, 1, 1, 1), HM.M.SEA_WINDOW_MAT))
    return [base, sea_fields_node(), sea]


def ring_node():
    n = N("Gate/Ring", "GateRing", R((0.5, 0.5), (0.5, 0.5), (0, 0), (100, 100)))
    n.add("cr", 222, K.canvas_renderer()).add("hex", 114, crystal_graphic((1, 1, 1, 1), width=GATE_RING_W, enabled=False))
    return n


# --- the brief: ENTERING over the mode's name, top left (Safe Area) --------------------------

def entering_node():
    n = N("Gate/Entering", "Entering", R((0, 1), (0, 1), (EDGE, -64), (600, 18), (0, 1)))
    n.add("cr", 222, K.canvas_renderer()).add("tmp", 114, ctext("Entering", "NeutralLightest", 14, spacing=18))
    return arrives(n, "Fade", delay=0.05)


# --- the tour: a picture, its words and the index --------------------------------------------

def show_node(i):
    """Chapter i's picture, laid out in the board's 900 x 500 box."""
    n = N(f"Tour/Show{i}", f"Show{i + 1}", centred(SHOW))
    n.add("cg", 225, H.canvas_group(i == 0))
    white = (1, 1, 1, 1)
    if i == 0:      # Squad: three pilots side by side
        for j, callsign in enumerate(SQUAD_PILOTS):
            gimg(n, f"Tour/Show0/Pilot{j}", callsign, HM.TL(30 + j * (264 + 24), 60, 264, 336), white, PORTRAIT[callsign], preserve=True)
    elif i == 1:    # The Ark
        gimg(n, "Tour/Show1/Ark", "Ark", HM.TL(40, 60, 800, 375), white, ARK_ART, preserve=True) \
            .add("drift", 114, motion(**ARK_DRIFT))
    elif i == 2:    # Pilots: one, large
        gimg(n, "Tour/Show2/Pilot", "Galleon", HM.TL(260, 0, 380, 484), white, PORTRAIT["Galleon"], preserve=True)
    elif i == 3:    # Vessels: a hull's class card
        gimg(n, "Tour/Show3/Hull", "Sparrow", HM.TL(190, -20, 520, 520), white, HULL_CARD["Sparrow"], preserve=True) \
            .add("drift", 114, motion(**HULL_BOB))
    else:           # Toy Box: a hull in the Vessel Changer's hex
        gimg(n, "Tour/Show4/HexFill", "HexFill", HM.TL(190, 20, 520, 450), tok("SurfaceVeryDark", 0.88), K.kit_sprite("kit_hex_fill"))
        gimg(n, "Tour/Show4/HexLine", "HexLine", HM.TL(190, 20, 520, 450), tok("SurfaceLight"), K.kit_sprite("kit_hex_line2"))
        gimg(n, "Tour/Show4/Hull", "Dolphin", HM.TL(280, 75, 340, 340), white, HULL_CARD["Dolphin"], preserve=True)
    return n


def words_node(i):
    title, line = TOUR[i]
    w = WORDS[2]
    n = N(f"Tour/Words{i}", f"Words{i + 1}", centred(WORDS))
    n.add("cg", 225, H.canvas_group(i == 0))
    counter = COUNTER_TEMPLATE.replace("{n}", str(i + 1)).replace("{total}", str(len(TOUR)))
    gtext(n, f"Tour/Words{i}/Counter", "Counter", HM.TL(0, 0, w, 18), ctext(counter, "NeutralLightest", 14, spacing=18))
    gtext(n, f"Tour/Words{i}/Title", "Title", HM.TL(0, 30, w, 54), ctext(title, "TextLight", 48, face="Aldrich", upper=False))
    gtext(n, f"Tour/Words{i}/Line", "Line", HM.TL(0, 96, w, 28), ctext(line, "TextLight", 18, face="Aldrich", upper=False))
    return n


def index_node():
    n = N("Tour/Index", "Index", R((1, 1), (1, 1), (-EDGE, -72), (900, 34), (1, 1)))
    n.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 32, 5, (1, 0), (0, 0)))
    for i, (title, _line) in enumerate(TOUR):
        word = gtext(n, f"Tour/Index{i}", f"Index{i + 1}", R(size=(100, 34)),
                     ctext(title, "TextLight" if i == 0 else "NeutralLightest", 18, face="Aldrich", upper=False))
        rule = word.child(N(f"Tour/Index{i}/Rule", "Rule", R((0, 0), (1, 0), (0, 0), (0, 2), (0.5, 0))))
        img = K.image(tok("TextLight"))
        rule.add("cr", 222, K.canvas_renderer()).add("img", 114, img if i == 0 else off(img))
    return arrives(n, "Fade", delay=0.1)


def tour_view():
    def body(ctx, go):
        chapters = "".join(
            f"  - title: {K.yaml_scalar(title)}\n    line: {K.yaml_scalar(line)}\n"
            f"    show: {{fileID: {ctx.c(f'Tour/Show{i}', 'cg')}}}\n    words: {{fileID: {ctx.c(f'Tour/Words{i}', 'cg')}}}\n"
            f"    counterText: {{fileID: {ctx.c(f'Tour/Words{i}/Counter', 'tmp')}}}\n"
            f"    titleText: {{fileID: {ctx.c(f'Tour/Words{i}/Title', 'tmp')}}}\n"
            f"    lineText: {{fileID: {ctx.c(f'Tour/Words{i}/Line', 'tmp')}}}\n"
            f"    indexText: {{fileID: {ctx.c(f'Tour/Index{i}', 'tmp')}}}\n"
            f"    indexRule: {{fileID: {ctx.c(f'Tour/Index{i}/Rule', 'img')}}}\n"
            for i, (title, line) in enumerate(TOUR))
        return K.mono(go, S["tour"], "  chapters:\n" + chapters
                      + f"  counterTemplate: {K.yaml_scalar(COUNTER_TEMPLATE)}\n"
                      f"  indexOnToken: {K.TOKEN_INDEX['TextLight']}\n  indexOffToken: {K.TOKEN_INDEX['NeutralLightest']}\n"
                      f"  fadeSeconds: {K.f32(TOUR_FADE)}\n")
    return body


def tour_node():
    n = N("Tour", "Tour", STRETCH)
    gimg(n, "Tour/Glow", "Glow", centred((140, 420, 1000, 340)), tok("TextLight", 0.14), K.kit_sprite("kit_ring_glow"))
    for i in range(len(TOUR)):
        n.child(show_node(i))
    for i in range(len(TOUR)):
        n.child(words_node(i))
    n.child(index_node())
    n.add("tour", 114, tour_view())
    return n


# --- the bar: the step's name and the percentage over an 8 px track, the steps named under it --

def step_label(bar, i):
    start = STEP_ENDS[i - 1] if i else 0.0
    width = min(420, round((STEP_ENDS[i] - start) * (1920 - 2 * EDGE)) - 6)
    on = i == 0
    n = bar.child(N(f"Gate/Bar/Label{i}", f"Step{i + 1}", R((start, 1), (start, 1), (0, -104), (width, 20), (0, 1))))
    # The marker: a diamond (a square turned 45 degrees in mark_rotations). Lit once its step starts; the dark
    # centre makes it an outline until the step is done, when the fill covers it.
    mark = gimg(n, f"Gate/Bar/Label{i}/Mark", "Mark", R((0, 0.5), (0, 0.5), (6, 0), (8, 8)), tok("TextLight" if on else "NeutralLightest"))
    gimg(mark, f"Gate/Bar/Label{i}/Hole", "Hole", R(size=(6, 6)), tok("SurfaceVeryDark"))
    fill = mark.child(N(f"Gate/Bar/Label{i}/MarkFill", "Fill", STRETCH))
    fill.add("cr", 222, K.canvas_renderer()).add("img", 114, off(K.image(tok("TextLight"))))
    gtext(n, f"Gate/Bar/Label{i}/Text", "Text", R((0, 0), (1, 1), (11, 0), (-22, 0)),
          ctext(STEP_NAMES[i], "TextLight" if on else "NeutralLightest", 15, face="Aldrich", upper=False))
    return n


def step_bar():
    n = len(STEP_ENDS)
    return lambda ctx, go: (lambda c: K.mono(go, S["stepbar"],
        f"  fill: {{fileID: {CN['fill_rt']}}}\n  fillGraphic: {{fileID: {CN['fill_img']}}}\n"
        f"  glint: {{fileID: {ctx.rt('Gate/Bar/Glint')}}}\n  glintSeconds: 2.4\n"
        "  stepEnds:\n" + "".join(f"  - {K.f32(e)}\n" for e in STEP_ENDS)
        + "  stepNames:\n" + "".join(f"  - {K.yaml_scalar(s)}\n" for s in STEP_NAMES)
        + K.refs_list("stepLabels", [c(f"Gate/Bar/Label{i}/Text", "tmp") for i in range(n)])
        + K.refs_list("stepMarkers", [c(f"Gate/Bar/Label{i}/Mark", "img") for i in range(n)])
        + K.refs_list("stepMarkerFills", [c(f"Gate/Bar/Label{i}/MarkFill", "img") for i in range(n)])
        + f"  stepText: {{fileID: {c('Gate/Bar/Step', 'tmp')}}}\n  percentText: {{fileID: {c('Gate/Bar/Percent', 'tmp')}}}\n"
        f"  percentTemplate: {K.yaml_scalar(PERCENT_TEMPLATE)}\n  doneWord: {K.yaml_scalar(DONE_WORD)}\n"
        f"  fillToken: {K.TOKEN_INDEX['TextLight']}\n  doneToken: {K.TOKEN_INDEX['Cta']}\n"
        f"  stepOnToken: {K.TOKEN_INDEX['TextLight']}\n  stepOffToken: {K.TOKEN_INDEX['NeutralLightest']}\n"))(ctx.c)


def bar_node():
    n = N("Gate/Bar", "Bar", R((0, 0), (1, 0), (0, BAR_BOTTOM), (-2 * EDGE, BAR_H), (0.5, 0)))
    gtext(n, "Gate/Bar/Loading", "Loading", HM.TL(0, 0, 400, 18), ctext("Loading", "NeutralLightest", 14, spacing=18))
    gtext(n, "Gate/Bar/Step", "Step", HM.TL(0, 26, 900, 34), ctext(STEP_NAMES[0], "TextLight", 28, face="Aldrich", upper=False))
    gtext(n, "Gate/Bar/Percent", "Percent", HM.TR(26, 6, 200, 54), ctext("0", "TextLight", 44, face="Aldrich", upper=False, align="right"))
    gtext(n, "Gate/Bar/Unit", "Unit", HM.TR(0, 26, 22, 28), ctext("%", "NeutralLightest", 20, face="Aldrich", upper=False, align="right"))
    for i in range(len(STEP_ENDS)):
        step_label(n, i)
    n.add("steps", 114, step_bar())
    return arrives(n, delay=0.15, distance=16)


def glint_node():
    """A dark band that keeps crossing the fill (KitStepBar moves it), so a slow step never looks frozen."""
    n = N("Gate/Bar/Glint", "Glint", R((0, 0), (0.12, 1), (0, 0), (0, 0)))
    n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.55), K.kit_sprite("kit_spark")))
    return n


def cut_nodes():
    """The breaks between the steps: the ground's colour over the track, at each step's end but the last."""
    out = []
    for i, e in enumerate(STEP_ENDS[:-1]):
        n = N(f"Gate/Bar/Cut{i}", f"Cut{i + 1}", R((e, 0), (e, 1), (0, 0), (4, 0)))
        n.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark")))
        out.append(n)
    return out


GENERATED_CONNECT_KEYS = (("Gate/Cover/Base", "Gate/Cover/Fields", "Gate/Cover/Sea", "Gate/Ring", "Gate/Entering", "Tour", "Gate/Bar", "Gate/Bar/Glint")
                          + tuple(f"Gate/Bar/Cut{i}" for i in range(len(STEP_ENDS) - 1)))


def gate_view(p):
    c = p.ctx.c
    return lambda ctx, go: K.mono(go, S["gate"],
        f"  veilWords: {K.ref(11400000, VEIL_WORDS, 2)}\n"
        + K.refs_list("cover", [c("Gate/Cover/Base", "hole"), c("Gate/Cover/Sea", "hole")])
        + f"  gateRing: {{fileID: {c('Gate/Ring', 'hex')}}}\n"
        f"  enteringText: {{fileID: {c('Gate/Entering', 'tmp')}}}\n  loadingLabel: {{fileID: {c('Gate/Bar/Loading', 'tmp')}}}\n"
        f"  tour: {{fileID: {c('Tour', 'tour')}}}\n"
        f"  content: {{fileID: {p.ctx.fid(CN['safe_go'] + '#cg')}}}\n"
        f"  backdrop: {{fileID: {c('Gate/Cover/Fields', 'cg')}}}\n  gateSeconds: 1.7\n  gateSettleRadius: 452\n")


def controller_doc(p, gate_fid):
    old = p.doc(CN["comp"])
    head = old[:old.index("  m_EditorClassIdentifier:")]
    keep = {f: re.search(r"^  %s: (.*)$" % f, old, re.M).group(1)
            for f in ("gameData", "tournamentData", "canvasGroup", "playerRoster", "maelstromRankText", "rankHeader")}
    return (head + "  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.ConnectingPanelController\n"
            f"  gameData: {keep['gameData']}\n  tournamentData: {keep['tournamentData']}\n"
            f"  canvasGroup: {keep['canvasGroup']}\n  gate: {{fileID: {gate_fid}}}\n"
            f"  gameModeText: {{fileID: {CN['mode_tmp']}}}\n  games: {K.ref(11400000, ARCADE_GAMES, 2)}\n"
            f"  introWords: {K.ref(11400000, INTRO_LIBRARY, 2)}\n"
            f"  stepBar: {{fileID: {p.ctx.c('Gate/Bar', 'steps')}}}\n  barSmoothing: 0.25\n"
            "  coveredLayBudgetMs: 80\n  coveredCreationBudgetMs: 0\n  parkedBackdrop: {r: 0, g: 0, b: 0, a: 1}\n"
            f"  playerRoster: {keep['playerRoster']}\n  peerWaitTimeoutSeconds: 45\n"
            f"  maelstromRankText: {keep['maelstromRankText']}\n  rankHeader: {keep['rankHeader']}\n  dwellSeconds: 1\n")


ROSTER_ROW_FIELDS = (  # ConnectingPlayerRoster's row mode, in its declaration order (after builtEntrySize)
    "domainHalo", "showAI", "templateNameText", "templateBadgeText", "templateLineText", "templateStateText",
    "templatePip", "vessels", "youWord", "aiWord", "waitingWord", "readyWord", "lineTemplate", "countText",
    "countTemplate", "readyToken", "waitingToken", "youToken", "otherBadgeToken")


def roster_doc(p):
    """The roster counts the pilots the load waits on; F draws none of them, so its row's words are unwired (an
    unwired part is simply not drawn) and only the humans the wait is about are tracked."""
    none = "{fileID: 0}"
    vals = dict(domainHalo="0", showAI="0", templateNameText=none, templateBadgeText=none, templateLineText=none,
                templateStateText=none, templatePip=none, vessels=none,
                youWord="You", aiWord="AI", waitingWord="Loading", readyWord="Ready", lineTemplate="'{hull}'",
                countText=none, countTemplate="'{ready} / {total} Ready'",
                readyToken=str(K.TOKEN_INDEX["Cta"]), waitingToken=str(K.TOKEN_INDEX["NeutralLightest"]),
                youToken=str(K.TOKEN_INDEX["Selection"]), otherBadgeToken=str(K.TOKEN_INDEX["NeutralLightest"]))
    d = p.doc(CN["roster"])
    d = set_field(d, "templateDomainImage", none)
    after = "builtEntrySize"
    for f in ROSTER_ROW_FIELDS:
        d = insert_after_field(d, after, f, vals[f])
        after = f
    return d


def reparent(p, rt, parent_rt):
    old = re.search(r"^  m_Father: \{fileID: (-?\d+)\}", p.doc(rt), re.M).group(1)
    if old != parent_rt and p.sc.has(old):
        p.children(old, [k for k in p.kids(old) if k != rt])
    p.field(rt, "m_Father", f"{{fileID: {parent_rt}}}")


def emit_all(p, nodes, parent_rt, after):
    ids = []
    for n in nodes:
        ids.append(p.emit(n, parent_rt, after))
        after = HM._last_doc(p, n)
    return ids, after


def drop_slider(p):
    """The Slider component leaves its object (KitStepBar anchors the fill); the object, track and fill stay."""
    for fid in p.comp_ids(CN["slider_go"]):
        if p.sc.has(fid) and re.search(r"^  m_FillRect:", p.doc(fid), re.M):
            p.sc.remove(fid)
            p.replace(CN["slider_go"], p.doc(CN["slider_go"]).replace(f"  - component: {{fileID: {fid}}}\n", ""))


def mark_rotations(p):
    for i in range(len(STEP_ENDS)):
        rt = str(p.ctx.rt(f"Gate/Bar/Label{i}/Mark"))
        p.field(rt, "m_LocalRotation", K.rot_z(45))
        p.field(rt, "m_LocalEulerAnglesHint", "{x: 0, y: 0, z: 45}")


def connect_text(text_):
    p = Patch(text_, "ConnectingPanel/Item12")
    assert p.sc.script(CN["comp"]) == S["connect"], "ConnectingPanelController moved"
    # Out with the camera window and every generated node: they are re-emitted below, so a re-run is a no-op.
    if p.sc.has(CN["camera_go"]):           # a plain Transform (no RectTransform), with no children
        for fid in [CN["camera_go"]] + [c for c in p.comp_ids(CN["camera_go"]) if p.sc.has(c)]:
            p.sc.remove(fid)
    for go in (CN["preview_go"], CN["status_go"]):
        if p.sc.has(go):
            p.remove_subtree(go)
    # The bar's track and the roster's list live inside generated nodes: lift them out before those go.
    for rt in (CN["slider_rt"], CN["icons_rt"]):
        reparent(p, rt, CN["safe_rt"])
    for key in RETIRED_CONNECT_KEYS + GENERATED_CONNECT_KEYS:
        go = str(p.ctx.go(key))
        if p.sc.has(go):
            parent = re.search(r"^  m_Father: \{fileID: (-?\d+)\}", p.doc(str(p.ctx.rt(key))), re.M).group(1)
            p.remove_subtree(go)
            if p.sc.has(parent):
                p.children(parent, [k for k in p.kids(parent) if p.sc.has(k)])

    # The root: no backdrop of its own (the cover draws it, with the hole), still blocking input while it is up.
    p.body(CN["root_img"], HM.img_body((0, 0, 0, 0), raycast=True), CN["root_go"])
    root_ids, _ = emit_all(p, cover_nodes() + [ring_node()], CN["root_rt"], CN["comp"])
    p.children(CN["root_rt"], root_ids + [CN["safe_rt"]])

    # The Safe Area is the gate's content, faded as the gate opens: the brief, the tour and the bar.
    p.add_comp(CN["safe_go"], "cg", 225, H.canvas_group(True))
    p.rect(CN["mode_rt"], R((0, 1), (0, 1), (EDGE, -88), (900, 36), (0, 1)))
    m = tmp_restyle(p.doc(CN["mode_tmp"]), "Aldrich", 28, False, 0, "left", "TextLight")
    p.replace(CN["mode_tmp"], C.field_set(m, "m_TextWrappingMode", "0"))
    p.add_comp(CN["mode_go"], "cg", 225, H.canvas_group(True))
    p.add_comp(CN["mode_go"], "in", 114, HM.M.entrance("Rise", delay=0.08, distance=12))
    # The tournament's rank, top centre: clear of the brief on its left and the index on its right.
    p.rect(CN["rank_rt"], R((0.5, 1), (0.5, 1), (0, -56), (480, 120), (0.5, 1)))
    rank = p.doc(CN["rank_tmp"])
    rank = C.field_set(rank, "m_fontAsset", K.ref(11400000, K.ALDRICH[0], 2))
    rank = C.field_set(rank, "m_sharedMaterial", K.ref(K.ALDRICH[1], K.ALDRICH[0], 2))
    p.replace(CN["rank_tmp"], C.field_set(rank, "m_HorizontalAlignment", "2"))
    safe_ids, _ = emit_all(p, [entering_node(), tour_node(), bar_node()], CN["safe_rt"], CN["safe_go"])
    p.children(CN["safe_rt"], [safe_ids[0], CN["mode_rt"], safe_ids[1], safe_ids[2], CN["icons_rt"], CN["rank_rt"]])
    mark_rotations(p)

    # The bar's track: the old Slider's objects, under the bar. An 8 px track, a light fill that KitStepBar anchors,
    # a glint clipped to the fill, and a break at each step's end.
    bar_rt = str(p.ctx.rt("Gate/Bar"))
    drop_slider(p)
    reparent(p, CN["slider_rt"], bar_rt)
    p.field(CN["slider_go"], "m_Name", "Track")
    p.rect(CN["slider_rt"], R((0, 1), (1, 1), (0, -76), (0, TRACK_H), (0.5, 1)))
    p.children(bar_rt, p.kids(bar_rt) + [CN["slider_rt"]])
    p.rect(CN["track_rt"], STRETCH)
    p.rect(CN["fillarea_rt"], STRETCH)
    p.rect(CN["fill_rt"], R((0, 0), (0, 1), (0, 0), (0, 0)))
    p.body(CN["track_img"], HM.img_body(tok("SurfaceLight", 0.55)), CN["track_go"])
    p.body(CN["fill_img"], HM.img_body(tok("TextLight")), CN["fill_go"])
    clip = p.add_comp(CN["fill_go"], "clip", 114, lambda ctx, go: K.mono(go, RECT_MASK_2D, "  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n"))
    glint_ids, after = emit_all(p, [glint_node()], CN["fill_rt"], str(clip))
    p.children(CN["fill_rt"], glint_ids)
    cut_ids, _ = emit_all(p, cut_nodes(), CN["slider_rt"], after)
    p.children(CN["slider_rt"], [CN["track_rt"], CN["fillarea_rt"]] + cut_ids)

    # The pilots: the roster keeps counting who the load waits on, and draws nothing (a zero-alpha group).
    p.rect(CN["icons_rt"], R((0.5, 0), (0.5, 0), (0, 0), (400, 64), (0.5, 0)))
    p.add_comp(CN["icons_go"], "hide", 225, H.canvas_group(False))
    icons_img = next(f for f in p.comp_ids(CN["icons_go"]) if f"guid: {K.IMAGE}" in p.doc(f))
    p.body(icons_img, HM.img_body((1, 1, 1, 0), enabled=False), CN["icons_go"])
    layout = next(f for f in p.comp_ids(CN["icons_go"]) if f"guid: {S['hlg']}" in p.doc(f) or f"guid: {S['vlg']}" in p.doc(f))
    p.body(layout, K.hv_layout(True, (0, 0, 0, 0), 0, 0, (1, 0), (1, 0)), CN["icons_go"])
    p.field(CN["row_go"], "m_Name", "PilotRow")
    p.rect(CN["row_rt"], R((0, 0), (0, 0), (0, 0), (64, 64)))
    p.body(CN["row_img"], HM.img_body((1, 1, 1, 0), enabled=False), CN["row_go"])
    p.rect(CN["avatar_rt"], R((0.5, 0.5), (0.5, 0.5), (0, 0), (44, 44)))
    p.children(CN["row_rt"], [CN["avatar_rt"]])
    p.replace(CN["roster"], roster_doc(p))

    gate_fid = p.add_comp(CN["root_go"], "gate", 114, gate_view(p))
    p.replace(CN["comp"], controller_doc(p, gate_fid))
    return p.text()


# --------------------------------------------------------------------------------------------
# 12d - the end reveal (EndGameStatsPanel.prefab) and the scoreboard (new ScoreboardPanel.prefab)
# --------------------------------------------------------------------------------------------

ENDGAME = "Assets/_Prefabs/UI Elements/In Game/EndGameStatsPanel.prefab"
ENDGAME_GUID = K.meta_guid(ENDGAME)
EG = dict(seq="8928388148105690216", toast_rt="1666509602065227010", msg_go="6065125739201124439",
          msg_rt="2795126439267128504", msg_tmp="8549829074479965766")
# The reveal's two purple ornaments (Play_ActionText04/05), deleted with the canvas's overrides into them.
LEGACY_REVEAL_GOS = ("1922089438039815508", "3304012168629254948")
LEGACY_REVEAL_IDS = frozenset("""1922089438039815508 5888220749407968017 8127182187547510907 4122368631742369545
3304012168629254948 7632647083028235520 7159594451883480492 386883533307007208""".split())
S.update(seq=script_guid("Assets/_Scripts/Utility/DataContainers/EndGameSequencer.cs"),
         card=script_guid("Assets/_Scripts/UI/PlayerScoreCard.cs"), statrow=script_guid("Assets/_Scripts/UI/StatRowUI.cs"),
         scoreboard=script_guid("Assets/_Scripts/UI/Scoreboard.cs"))
MSG_SIZE = 96


def sized_text(t, role, token, size, spacing=None, align="center"):
    def body(ctx, go):
        b = H.text(t, role, tok(token), align=align)(ctx, go)
        _, face, rsize, rspacing, _, _ = K.ROLES[role]
        b = b.replace(f"  m_fontSize: {K.f32(rsize)}\n  m_fontSizeBase: {K.f32(rsize)}\n",
                      f"  m_fontSize: {size}\n  m_fontSizeBase: {size}\n")
        if spacing is not None:
            b = b.replace(f"  m_characterSpacing: {K.f32(rspacing)}\n", f"  m_characterSpacing: {spacing}\n")
        return b
    return body


def endgame_text(text_):
    p = Patch(text_, "EndGame/Item12")
    assert p.sc.script(EG["seq"]) == S["seq"], "EndGameSequencer moved"
    for go in LEGACY_REVEAL_GOS:
        if p.sc.has(go):
            p.remove_subtree(go)
    scrim = N("Reveal/Scrim", "Scrim", STRETCH)
    scrim.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.5))).add("th", 114, H.themed("SurfaceBlack", 0.5))
    win = N("Reveal/Winner", "Winner", R((0.5, 0.5), (0.5, 0.5), (0, 148), (1600, 28)))
    win.add("cr", 222, K.canvas_renderer()).add("tmp", 114, sized_text("Jade wins", "Label", "TextLight", 18, spacing=30)) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Drop", delay=0.1, distance=10, play_on_enable=False))
    rule = N("Reveal/Rule", "Rule", R((0.5, 0.5), (0.5, 0.5), (0, 0), (160, 3)))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"))) \
        .add("in", 114, HM.M.entrance("GrowX", delay=0.5, play_on_enable=False))
    det = N("Reveal/Detail", "Detail", R((0.5, 0.5), (0.5, 0.5), (0, -36), (1600, 30)))
    det.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "H3", tok("TextLight"), align="center")) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", delay=0.65, distance=8, play_on_enable=False))
    ids = [p.emit(n, EG["toast_rt"], EG["seq"]) for n in (scrim, win, rule, det)]
    p.children(EG["toast_rt"], [ids[0], ids[1], EG["msg_rt"], ids[2], ids[3]])
    # The crowd line: Light (never the old teal), 96 px Aldrich, auto-sized down to 48 so the longest
    # line in the set (NOT BEATING THE ALLEGATIONS) fits instead of running off the plate.
    p.rect(EG["msg_rt"], R((0.5, 0.5), (0.5, 0.5), (0, 68), (1600, 110)))
    m = tmp_restyle(p.doc(EG["msg_tmp"]), "Aldrich", MSG_SIZE, False, 0, "center", "TextLight")
    for f, v in (("m_enableAutoSizing", "1"), ("m_fontSizeMin", "48"), ("m_fontSizeMax", str(MSG_SIZE))):
        m = C.field_set(m, f, v)
    p.replace(EG["msg_tmp"], m)
    seq = p.doc(EG["seq"])
    seq = insert_after_field(seq, "gameOverCounterText", "winnerText", f"{{fileID: {p.ctx.c('Reveal/Winner', 'tmp')}}}")
    seq = insert_after_field(seq, "winnerText", "winnerRule", f"{{fileID: {p.ctx.c('Reveal/Rule', 'img')}}}")
    seq = insert_after_field(seq, "winnerRule", "detailText", f"{{fileID: {p.ctx.c('Reveal/Detail', 'tmp')}}}")
    seq = re.sub(r"^  revealEntrances:\n(?:  - .*\n)*|^  revealEntrances: \[\]\n", "", seq, flags=re.M)
    seq = seq.rstrip("\n") + "\n" + K.refs_list("revealEntrances", [p.ctx.c(k, "in") for k in ("Reveal/Winner", "Reveal/Rule", "Reveal/Detail")])
    p.replace(EG["seq"], seq)
    return p.text()


def prune_instance_mods(p, guid, dead):
    pis = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1001 ") and f"m_SourcePrefab: {{fileID: 100100000, guid: {guid}" in d]
    assert len(pis) == 1, f"GameCanvas should nest one instance of {guid}"
    entry = re.compile(r"    - target: \{fileID: (-?\d+), guid: %s,\s*type: 3\}\n      propertyPath: .*\n      value: .*\n      objectReference: .*\n(?:        .*\n)?" % guid)
    p.replace(pis[0], entry.sub(lambda m: "" if m.group(1) in dead else m.group(0), p.doc(pis[0])))
    for fid, doc in list(p.sc.docs.items()):
        mm = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: %s," % guid, doc)
        if mm and mm.group(1) in dead and doc.split("\n", 1)[0].endswith(" stripped"):
            p.sc.remove(fid)


# --- the scoreboard -------------------------------------------------------------------------

SB_DIR = "Assets/_Prefabs/UI Elements/In Game"
SB_PANEL, SB_ROW, SB_STAT = "ScoreboardPanel", "ScoreboardRow", "ScoreboardStat"
P.PREFABS = tuple(P.PREFABS) + (SB_PANEL, SB_ROW, SB_STAT)
GAMEOVER_GUID = K.meta_guid("Assets/_Prefabs/UI Elements/Panels/GameOverPanel.prefab")
ANIM_SETTINGS = "e1e99ce577ae9be408463160f475ed8d"
LIST_W, ROW_HT, LIST_H = 1400, 84, 500
HEAD_HT = 30
SB_BUTTONS = [  # key, caption, variant, keycap, pad (South 6 = A, East 5 = B), width
    ("PlayAgain", "Play again", "Play", "A", 6, 300),
    ("Continue", "Continue", "Play", "A", 6, 300),
    ("MainMenu", "Main menu", "Default", "B", 5, 260),
    ("LeaveLobby", "Leave lobby", "Default", "B", 5, 260),
]
SB_HEADER = "ScoreboardTeamHeader"
SB_VSROW = "ScoreboardVersusRow"           # Rev 23 D: a team's row, half the ladder's width
P.PREFABS = tuple(P.PREFABS) + (SB_HEADER, SB_VSROW)
VS_ROW_W, VS_COL_GAP = 660, 80             # two columns of 660 and the VS between them fill the ladder's 1400
VS_HEAD_H = 92                             # the team's head above its rows: name, status, score and its rule
S.update(header=script_guid("Assets/_Scripts/UI/ScoreboardTeamHeader.cs"),
         versus=script_guid("Assets/_Scripts/UI/ScoreboardVersusView.cs"),
         strip=script_guid("Assets/_Scripts/UI/ScoreboardRewardStrip.cs"),
         roster=script_guid("Assets/_Scripts/UI/Elements/RematchVoteRoster.cs"))
# Docs/UI_COLOR_RULES.md (Garrett, 2026-10-24): no element colours and no element rims anywhere in the UI, and the
# selection blue only on what is actually selected. A row's border is a still hairline (TextLight for you, SurfaceDark
# for the rest), PLAY AGAIN is the lime call to action, MAIN MENU / LEAVE LOBBY are neutral, the reward strip is plain.
NO_ROW_RIM = "{fileID: 0}"


def sb_path(name):
    return f"{SB_DIR}/{name}.prefab"


def mat_ref(guid):
    return f"{{fileID: 2100000, guid: {guid}, type: 2}}"


def build_row():
    """Rev 8 option B: a dark glass row with a thin team stripe, the pilot's share of the team's count along its foot,
    and a still hairline border (brighter for you)."""
    root = N("root", SB_ROW, R(size=(LIST_W, ROW_HT)))
    root.add("cg", 225, H.canvas_group(True)).add("le", 114, H.le(ph=ROW_HT, pw=LIST_W))
    glass = root.child(N("Glass", "Glass", STRETCH))
    glass.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.8), K.kit_sprite("kit_sliver14_fill"), True))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True))
    stripe = root.child(N("Stripe", "TeamStripe", HM.TL(0, 12, 4, ROW_HT - 24)))
    stripe.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    share = root.child(N("Share", "Share", R((0, 0), (0.5, 0), (0, 0), (0, 3), (0, 0))))
    share.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"))).add("th", 114, H.themed("NeutralLightest"))
    rank = root.child(N("Rank", "Rank", HM.TL(28, 21, 44, 42)))
    rank.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("1", "H2", tok("TextLight")))
    ring = root.child(N("Ring", "Ring", HM.TL(84, 16, 52, 52)))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_oct_fill"), True))
    clip = ring.child(N("Ring/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-6, -6))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_oct_fill"), True)).add("mask", 114, A.mask(False))
    av = clip.child(N("Ring/Clip/Avatar", "Avatar", STRETCH))
    av.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, A.AVATAR_PLACEHOLDER))
    name = root.child(N("Name", "Name", HM.TL(158, 12, 420, 34)))
    name.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(H.text("Pilot", "H2", tok("TextLight"))))
    dom = root.child(N("Domain", "Domain", HM.TL(158, 50, 300, 18)))
    dom.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "Label", tok("NeutralLightest")))
    # The pilot's own count ("12 Crystals") in the middle column; the team's shared score lives in its header.
    sec = root.child(N("Secondary", "Secondary", HM.TL(620, 21, 260, 42)))
    sec.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "Data", tok("TextLight")))
    score = root.child(N("Score", "Score", HM.TR(210, 21, 300, 42)))
    score.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("0", "H2", tok("TextLight"), align="right"))
    data = root.child(N("Data", "DataPanels", HM.TR(26, 21, 170, 42)))
    data.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 8, 5, (0, 0), (0, 0)))
    glyph = data.child(N("Data/Glyph", "Glyph", R(size=(20, 20))))
    glyph.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Purchase"), K.chrome_sprite("chrome_crystal"), preserve=True)) \
        .add("th", 114, H.themed("Purchase")).add("le", 114, H.le(pw=20, ph=20))
    amt = data.child(N("Data/Amount", "Amount", R(size=(110, 36))))
    amt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("+200", "H3", tok("TextLight"), align="right")).add("le", 114, H.le(pw=110, ph=36))
    root.add("card", 114, card_body)
    return root


def card_body(ctx, go):
    """PlayerScoreCard on a row (the ladder's and the versus row): the same parts under the same keys."""
    return (K.mono(go, S["card"],
        f"  playerAvatarImage: {{fileID: {ctx.c('Ring/Clip/Avatar', 'img')}}}\n"
        f"  playerNameText: {{fileID: {ctx.c('Name', 'tmp')}}}\n  playerScoreText: {{fileID: {ctx.c('Score', 'tmp')}}}\n"
        "  backgroundImage: {fileID: 0}\n"
        f"  domainIndicatorImage: {{fileID: {ctx.c('Ring', 'img')}}}\n"
        "  backgroundTintAlpha: 0\n"
        f"  dataPanelsRoot: {{fileID: {ctx.go('Data')}}}\n  secondaryStatText: {{fileID: {ctx.c('Secondary', 'tmp')}}}\n"
        f"  crystalRewardRoot: {{fileID: {ctx.go('Data')}}}\n  crystalRewardText: {{fileID: {ctx.c('Data/Amount', 'tmp')}}}\n"
        f"  rankText: {{fileID: {ctx.c('Rank', 'tmp')}}}\n  domainText: {{fileID: {ctx.c('Domain', 'tmp')}}}\n"
        f"  borderImage: {{fileID: {ctx.c('Border', 'img')}}}\n"
        f"  localBorderToken: {K.TOKEN_INDEX['TextLight']}\n  otherBorderToken: {K.TOKEN_INDEX['SurfaceDark']}\n"
        f"  leaderRankToken: {K.TOKEN_INDEX['TextLight']}\n  rankToken: {K.TOKEN_INDEX['NeutralLightest']}\n"
        f"  teamStripe: {{fileID: {ctx.c('Stripe', 'img')}}}\n  shareBar: {{fileID: {ctx.rt('Share')}}}\n"
        f"  glassFill: {{fileID: {ctx.c('Glass', 'img')}}}\n  glassToken: {K.TOKEN_INDEX['SurfaceVeryDark']}\n"
        "  glassAlpha: 0.8\n  localGlassTint: 0.35\n"
        f"  localGlassTintToken: {K.TOKEN_INDEX['NeutralLightest']}\n"
        f"  wonBorderMaterial: {NO_ROW_RIM}\n  lostBorderMaterial: {NO_ROW_RIM}\n  localBorderMaterial: {NO_ROW_RIM}\n"
        f"  animSettings: {K.ref(11400000, ANIM_SETTINGS, 2)}\n"))


def build_versus_row():
    """Rev 23 D: a team's row in the versus layout - the ladder row's parts at half its width: rank, face, the name with
    the pilot's line and counts stacked under it, the score, the reward."""
    w = VS_ROW_W
    root = N("root", SB_VSROW, R(size=(w, ROW_HT)))
    root.add("cg", 225, H.canvas_group(True)).add("le", 114, H.le(ph=ROW_HT, pw=w))
    glass = root.child(N("Glass", "Glass", STRETCH))
    glass.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark", 0.8), K.kit_sprite("kit_sliver14_fill"), True))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), K.kit_sprite("kit_sliver14_line1"), True))
    stripe = root.child(N("Stripe", "TeamStripe", HM.TL(0, 12, 4, ROW_HT - 24)))
    stripe.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    share = root.child(N("Share", "Share", R((0, 0), (0.5, 0), (0, 0), (0, 3), (0, 0))))
    share.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"))).add("th", 114, H.themed("NeutralLightest"))
    rank = root.child(N("Rank", "Rank", HM.TL(18, 21, 40, 42)))
    rank.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("1", "H2", tok("TextLight")))
    ring = root.child(N("Ring", "Ring", HM.TL(64, 16, 52, 52)))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_oct_fill"), True))
    clip = ring.child(N("Ring/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-6, -6))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_oct_fill"), True)).add("mask", 114, A.mask(False))
    av = clip.child(N("Ring/Clip/Avatar", "Avatar", STRETCH))
    av.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, A.AVATAR_PLACEHOLDER))
    name_w = w - 132 - 270
    name = root.child(N("Name", "Name", HM.TL(132, 8, name_w, 30)))
    name.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(H.text("Pilot", "H3", tok("TextLight"))))
    dom = root.child(N("Domain", "Domain", HM.TL(132, 40, name_w, 16)))
    dom.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(H.text("", "Label", tok("NeutralLightest"))))
    sec = root.child(N("Secondary", "Secondary", HM.TL(132, 58, name_w, 18)))
    sec.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(H.text("", "BodySmall", tok("TextLight"))))
    score = root.child(N("Score", "Score", HM.TR(118, 21, 150, 42)))
    score.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("0", "H2", tok("TextLight"), align="right"))
    data = root.child(N("Data", "DataPanels", HM.TR(16, 21, 96, 42)))
    data.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 6, 5, (0, 0), (0, 0)))
    glyph = data.child(N("Data/Glyph", "Glyph", R(size=(18, 18))))
    glyph.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Purchase"), K.chrome_sprite("chrome_crystal"), preserve=True)) \
        .add("th", 114, H.themed("Purchase")).add("le", 114, H.le(pw=18, ph=18))
    amt = data.child(N("Data/Amount", "Amount", R(size=(70, 36))))
    amt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("+200", "H3", tok("TextLight"), align="right")).add("le", 114, H.le(pw=70, ph=36))
    root.add("card", 114, card_body)
    return root


def build_header():
    """A team's header above its rows: the domain's name and a rule in its colour, then the team's line."""
    root = N("root", SB_HEADER, R(size=(LIST_W, HEAD_HT)))
    root.add("le", 114, H.le(ph=HEAD_HT, pw=LIST_W))
    dom = root.child(N("Domain", "Domain", HM.TL(12, 6, 72, 20)))
    dom.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("Jade", "Label", tok("TextLight")))
    rule = root.child(N("Rule", "Rule", HM.TL(92, 15, 120, 1)))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    summ = root.child(N("Summary", "Summary", HM.TL(228, 6, 700, 20)))
    summ.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "Label", tok("NeutralLightest")))
    root.add("header", 114, lambda ctx, go: K.mono(go, S["header"],
        f"  domainText: {{fileID: {ctx.c('Domain', 'tmp')}}}\n  rule: {{fileID: {ctx.c('Rule', 'img')}}}\n"
        f"  summaryText: {{fileID: {ctx.c('Summary', 'tmp')}}}\n"))
    return root


def build_stat():
    """Rev 8 option B: a slim inline stat - its icon, its label, its value - in a row under the ladder."""
    root = N("root", SB_STAT, R(size=(300, 40)))
    root.add("le", 114, H.le(pw=300, ph=40))
    # Rows are spawned into a layout group; each rises from where it lands (KitEntrance.ReconcileRest).
    root.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", by_sibling=True, distance=8))
    icon = root.child(N("Icon", "Icon", HM.TL(0, 9, 22, 22)))
    icon.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), preserve=True))
    H.label(root.child(N("Label", "Label", HM.TL(32, 11, 150, 18))), "Longest drift", "Label", "NeutralLightest")
    val = root.child(N("Value", "Value", HM.TL(186, 4, 110, 32)))
    val.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("0", "H3", tok("TextLight")))
    root.add("stat", 114, lambda ctx, go: K.mono(go, S["statrow"],
        f"  iconImage: {{fileID: {ctx.c('Icon', 'img')}}}\n  labelText: {{fileID: {ctx.c('Label', 'tmp')}}}\n"
        f"  valueText: {{fileID: {ctx.c('Value', 'tmp')}}}\n"))
    return root


def reward_strip(root):
    """What you earned and the wallet counting up, in a plain glass panel bottom right (the amount in the lime of a
    good outcome, the crystal in gold)."""
    w, h = 560, 92
    strip = root.child(N("Reward", "RewardStrip", R((0.5, 1), (0.5, 1), (LIST_W / 2 - w / 2, -770), (w, h), (0.5, 1))))
    strip.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", delay=0.7))
    _, bord = H.framed(strip, "Reward", 14, "SurfaceVeryDark", "SurfaceDark", fill_alpha=0.86)
    glyph = strip.child(N("Reward/Glyph", "Glyph", HM.TL(24, 30, 32, 32)))
    glyph.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Purchase"), K.chrome_sprite("chrome_crystal"), preserve=True)) \
        .add("th", 114, H.themed("Purchase"))
    H.label(strip.child(N("Reward/Earned", "Earned", HM.TL(72, 18, 200, 16))), "You earned", "Label", "NeutralLightest")
    amt = strip.child(N("Reward/Amount", "Amount", HM.TL(72, 36, 180, 42)))
    amt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("+200", "H1", tok("Cta")))
    H.label(strip.child(N("Reward/Balance", "Balance", HM.TR(24, 20, 200, 16))), "Balance", "Label", "NeutralLightest", align="right")
    to = strip.child(N("Reward/To", "To", HM.TR(24, 42, 120, 32)))
    to.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("0", "H3", tok("TextLight"), align="right"))
    chev = strip.child(N("Reward/Arrow", "Arrow", HM.TR(152, 50, 16, 16)))
    chev.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("NeutralLightest"), K.chrome_sprite("chrome_chevron"), preserve=True))
    frm = strip.child(N("Reward/From", "From", HM.TR(176, 42, 120, 32)))
    frm.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("0", "H3", tok("NeutralLightest"), align="right"))
    strip.add("strip", 114, lambda ctx, go: K.mono(go, S["strip"],
        f"  amountText: {{fileID: {ctx.c('Reward/Amount', 'tmp')}}}\n  fromText: {{fileID: {ctx.c('Reward/From', 'tmp')}}}\n"
        f"  toText: {{fileID: {ctx.c('Reward/To', 'tmp')}}}\n  amountTemplate: +{{amount}}\n  countSeconds: 0.8\n"))
    return strip


def versus_side(vs, key, right):
    """One team: its name in its colour and WINNER (or nothing) under it, its score on the right, a rule in its colour,
    then its rows."""
    col = vs.child(N(key, key.rsplit("/", 1)[-1], HM.TR(0, 56, VS_ROW_W, LIST_H - 56) if right else HM.TL(0, 56, VS_ROW_W, LIST_H - 56)))
    name = col.child(N(f"{key}/Name", "Name", HM.TL(0, 0, 340, 50)))
    name.add("cr", 222, K.canvas_renderer()).add("tmp", 114, sized_text("JADE", "H1", "TextLight", 40, align="left"))
    H.label(col.child(N(f"{key}/Status", "Status", HM.TL(2, 52, 300, 18))), "Winner", "Label", "NeutralLightest")
    score = col.child(N(f"{key}/Score", "Score", HM.TR(0, 4, 300, 60)))
    score.add("cr", 222, K.canvas_renderer()).add("tmp", 114, sized_text("", "H1", "TextLight", 48, align="right"))
    rule = col.child(N(f"{key}/Rule", "Rule", R((0, 1), (1, 1), (0, -(VS_HEAD_H - 14)), (0, 2), (0.5, 1))))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    rows = col.child(N(f"{key}/Rows", "Rows", R((0, 1), (1, 1), (0, -VS_HEAD_H), (0, 0), (0.5, 1))))
    rows.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 1, (1, 1), (0, 0))).add("csf", 114, K.fitter(0, 2))
    return col


def versus_layout(root):
    """Rev 23 D (Shombith, 2026-10-26: "For scoreboard I liked D"): where the ladder sits, the two teams face to face -
    the winner on the left - with the share bar across the top and VS between them. Hidden until a two-team match
    (ScoreboardVersusView); the ladder keeps every other match."""
    vs = root.child(N("Versus", "Versus", R((0.5, 1), (0.5, 1), (0, -196), (LIST_W, LIST_H), (0.5, 1))))
    vs.add("cg", 225, H.canvas_group(False))
    share = vs.child(N("Versus/Share", "Share", HM.TL(0, 0, LIST_W, 40)))
    share.add("cg", 225, H.canvas_group(True))
    track = share.child(N("Versus/Share/Bar", "Bar", HM.TL(0, 0, LIST_W, 10)))
    for side, r in (("Left", R((0, 0), (0.5, 1), (-1, 0), (-2, 0))), ("Right", R((0.5, 0), (1, 1), (1, 0), (-2, 0)))):
        track.child(N(f"Versus/Share/Bar/{side}", side, r)).add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    H.label(share.child(N("Versus/Share/LeftLine", "LeftLine", HM.TL(0, 18, 600, 18))), "Ruby 52%", "Label", "TextLight")
    H.label(share.child(N("Versus/Share/RightLine", "RightLine", HM.TR(0, 18, 600, 18))), "Jade 48%", "Label", "NeutralLightest",
            align="right")
    vs_word = vs.child(N("Versus/Vs", "Vs", HM.TL(VS_ROW_W, 56 + 14, VS_COL_GAP, 40)))
    vs_word.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("VS", "H2", tok("NeutralLightest"), align="center"))
    versus_side(vs, "Versus/Left", False)
    versus_side(vs, "Versus/Right", True)

    def body(ctx, go):
        sides = ""
        for side in ("Left", "Right"):
            k = f"Versus/{side}"
            sides += (f"  - nameText: {{fileID: {ctx.c(f'{k}/Name', 'tmp')}}}\n    statusText: {{fileID: {ctx.c(f'{k}/Status', 'tmp')}}}\n"
                      f"    scoreText: {{fileID: {ctx.c(f'{k}/Score', 'tmp')}}}\n    rule: {{fileID: {ctx.c(f'{k}/Rule', 'img')}}}\n"
                      f"    rows: {{fileID: {ctx.rt(f'{k}/Rows')}}}\n    shareFill: {{fileID: {ctx.c(f'Versus/Share/Bar/{side}', 'img')}}}\n"
                      f"    shareText: {{fileID: {ctx.c(f'Versus/Share/{side}Line', 'tmp')}}}\n")
        return K.mono(go, S["versus"],
                      f"  group: {{fileID: {ctx.c('Versus', 'cg')}}}\n  ladderGroup: {{fileID: {ctx.c('List', 'cg')}}}\n"
                      "  sides:\n" + sides
                      + f"  shareGroup: {{fileID: {ctx.c('Versus/Share', 'cg')}}}\n  shareGap: 4\n  loserShareAlpha: 0.6\n"
                      "  winnerWord: Winner\n  loserWord: \n  shareTemplate: '{domain} {percent}%'\n")
    vs.add("versus", 114, body)
    return vs


def build_panel():
    root = N("root", SB_PANEL, STRETCH)
    scrim = root.child(N("Scrim", "Scrim", STRETCH))
    scrim.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.86), raycast=True)).add("th", 114, H.themed("SurfaceBlack", 0.86))
    scrim.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Fade", duration=0.2))
    root.children.append(HM.M.window_sea_node("Lattice", name="Sea"))
    head = root.child(N("Header", "Header", R((0.5, 1), (0.5, 1), (0, -56), (LIST_W, 120), (0.5, 1))))
    head.add("in", 114, HM.M.entrance("Rise", distance=14))
    mode = head.child(N("Header/Mode", "Mode", HM.TL(0, 0, LIST_W, 22)))
    mode.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "Label", tok("NeutralLightest"), align="center"))
    ban = head.child(N("Header/Banner", "Banner", HM.TL(0, 28, LIST_W, 64)))
    ban.add("cr", 222, K.canvas_renderer()).add("tmp", 114, sized_text("Game over", "Display", "TextLight", 56)) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Pop", delay=0.08))
    rule = head.child(N("Header/Rule", "Rule", R((0.5, 1), (0.5, 1), (0, -102), (220, 3), (0.5, 1))))
    rule.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"))) \
        .add("in", 114, HM.M.entrance("GrowX", delay=0.3))
    view = root.child(N("List", "List", R((0.5, 1), (0.5, 1), (0, -196), (LIST_W, LIST_H), (0.5, 1))))
    view.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("rmask", 114, A.rect_mask()).add("scroll", 114, A.scroll_rect("List/Content", "List", 30)) \
        .add("cg", 225, H.canvas_group(True))
    content = view.child(N("List/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 1, (1, 1), (0, 0))).add("csf", 114, K.fitter(0, 2))
    versus_layout(root)
    stats = root.child(N("Stats", "Stats", R((0.5, 1), (0.5, 1), (0, -712), (LIST_W, 40), (0.5, 1))))
    stats.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 24, 3, (1, 1), (0, 0))) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", delay=0.5))
    row = root.child(N("Buttons", "Buttons", R((0.5, 1), (0.5, 1), (0, -784), (LIST_W, 52), (0.5, 1))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 24, 0, (1, 1), (0, 0))) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", delay=0.6))
    for key, cap, variant, keycap, pad, w in SB_BUTTONS:
        b = kit_button(f"Sb/{key}", f"{key}Button", R(size=(w, 52)), variant, False, cap, keycap, pad,
                       keycap_key=f"Sb{key}Keycap")
        b.add("le", 114, H.le(pw=w, ph=52))
        if key == "PlayAgain":
            strip = b.child(N("Sb/PlayAgain/Avatars", "PlayerAvatars", R((0, 0), (0, 0), (8, -12), (420, 30), (0, 1))))
            strip.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 3, (0, 0), (0, 0))).add("le", 114, H.le(ignore=True))
            cnt = strip.child(N("Sb/PlayAgain/Avatars/Count", "Count", R(size=(300, 30))))
            cnt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "Label", tok("NeutralLightest"))) \
                .add("le", 114, H.le(pw=300, ph=30))
            b.add("roster", 114, lambda ctx, go: K.mono(go, S["roster"],
                f"  container: {{fileID: {ctx.rt('Sb/PlayAgain/Avatars')}}}\n  builtEntrySize: 30\n"
                f"  countText: {{fileID: {ctx.c('Sb/PlayAgain/Avatars/Count', 'tmp')}}}\n"
                "  oneVoteLine: '{count} wants a rematch'\n  manyVotesLine: '{count} want a rematch'\n"))
        row.children.append(b)
    reward_strip(root)
    return root


def sb_prefabs():
    out = {}
    for name, build in ((SB_ROW, build_row), (SB_VSROW, build_versus_row), (SB_HEADER, build_header), (SB_STAT, build_stat),
                        (SB_PANEL, build_panel)):
        out[sb_path(name)] = P.prefab_text(build(), name)
        out[sb_path(name) + ".meta"] = K.PREFAB_META.format(g=P.asset_guid(name))
    return out


def sb_ref(name, key):
    """A reference INTO one of the new prefabs, as an asset reference (prefab fields)."""
    return K.ref(K.FileIds(name)(key), P.asset_guid(name), 3)


SCOREBOARD_HOST_RECT = (("m_AnchorMin", "{x: 0, y: 0}"), ("m_AnchorMax", "{x: 1, y: 1}"),
                        ("m_AnchoredPosition", "{x: 0, y: 0}"), ("m_SizeDelta", "{x: 0, y: 0}"),
                        ("m_Pivot", "{x: 0.5, y: 0.5}"))


def patch_scoreboard(p):
    """Swap the legacy GameOverPanel instance (800x450-era art, ~130 parked overrides and an added
    subtree carrying the real buttons) for one instance of ScoreboardPanel.prefab, and re-point the
    canvas's Scoreboard at it. Everything else in the canvas that touched the old instance is the
    ScoreboardController's child list, which is rebuilt here."""
    sbs = p.by_script(S["scoreboard"])
    assert len(sbs) == 1, "GameCanvas should carry one Scoreboard"
    sb = sbs[0]
    host_go = p.sc.go_of(sb)
    host_rt = p.sc.rect_of(host_go)
    # The host must cover the canvas: the style-B board stretches to it. It was a 0x0 point at the centre (the
    # legacy GameOverPanel carried its own 832 x 456 size at scale 2.4), so the stretched board collapsed to
    # nothing - no dim, rows clipped, the buttons off screen, in every mode (the owner, 2026-10-17: "after
    # finishing game no game over came with scorecard").
    hd = p.doc(host_rt)
    for key, val in SCOREBOARD_HOST_RECT:
        hd = re.sub(r"^  %s: \{[^}]*\}$" % key, f"  {key}: {val}", hd, count=1, flags=re.M)
    p.replace(host_rt, hd)
    olds = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1001 ") and f"m_SourcePrefab: {{fileID: 100100000, guid: {GAMEOVER_GUID}" in d]
    if olds:
        dead = set(C.instance_docs(p.sc, olds[0]))
        for fid in dead:
            if p.sc.has(fid):
                p.sc.remove(fid)
        p.children(host_rt, [k for k in p.kids(host_rt) if k not in dead])
    inst = str(p.ctx.fid("Score#inst"))
    fk = K.FileIds(SB_PANEL)
    guid = P.asset_guid(SB_PANEL)

    out = []
    K.emit_instance(p.ctx, K.Instance("Score", SB_PANEL, guid, "ScoreboardPanel", STRETCH, ()), host_rt, out)
    retired = set()
    C.STUB_KIND.setdefault("strip", (114, S["strip"]))
    C.STUB_KIND.setdefault("versus", (114, S["versus"]))

    def stub(key, kind):
        src = fk(key)
        fid = str(C.derived(src, inst))
        doc = C.stub_doc(fid, kind, src, guid, inst)
        if p.sc.has(fid):
            p.sc.remove(fid)
        # emit_instance already wrote the instance root's stand-in. A second stand-in for the same object binds to
        # nothing in Unity, so everything pointing at it reads as destroyed: the scoreboard panel did, and no end
        # screen ever showed (2026-10-17). Reuse the one that exists.
        same = T.stub_identity(doc)
        for prior in out:
            if T.stub_identity(prior) == same:
                retired.add(fid)
                return T.doc_fid(prior), prior
        return fid, doc
    refs = {}
    for field, key, kind in (("endGameObject", "root#go", "go"), ("scoreboardPanel", "root#rt", "rt"),
                             ("statsContainer", "Stats#rt", "rt"), ("BannerImage", "Header/Rule#img", "img"),
                             ("BannerText", "Header/Banner#tmp", "tmp"), ("playerCardContainer", "List/Content#rt", "rt"),
                             ("playAgainButton", "Sb/PlayAgain#go", "go"), ("playAgainLabel", "Sb/PlayAgain/Label#tmp", "tmp"),
                             ("mainMenuButton", "Sb/MainMenu#go", "go"), ("leaveLobbyButton", "Sb/LeaveLobby#go", "go"),
                             ("continueButton", "Sb/Continue#go", "go"), ("modeText", "Header/Mode#tmp", "tmp"),
                             ("rewardStrip", "Reward#strip", "strip"), ("versusView", "Versus#versus", "versus")):
        fid, doc = stub(key, kind)
        refs[field] = fid
        if doc not in out:
            out.append(doc)
    for fid in [T.doc_fid(d) for d in out]:
        if p.sc.has(fid):
            p.sc.remove(fid)
    anchor = host_rt
    for d in (K.unity_empty_scalars(d) for d in out):
        p.sc.insert_after(anchor, T.doc_fid(d), d)
        anchor = T.doc_fid(d)
    root_stub = refs["scoreboardPanel"]
    p.children(host_rt, [k for k in p.kids(host_rt) if k != root_stub and k not in retired] + [root_stub])
    d = p.doc(sb)
    for field, fid in refs.items():
        d = set_field(d, field, f"{{fileID: {fid}}}")
    d = set_field(d, "statRowPrefab", sb_ref(SB_STAT, "root#stat"))
    d = set_field(d, "playerCardPrefab", sb_ref(SB_ROW, "root#card"))
    d = set_field(d, "teamHeaderPrefab", sb_ref(SB_HEADER, "root#header"))
    d = set_field(d, "versusRowPrefab", sb_ref(SB_VSROW, "root#card"))
    d = set_field(d, "wireButtonsInCode", "1")
    p.replace(sb, d)


# --------------------------------------------------------------------------------------------
# 12e - the in-match feed (GameFeedText.prefab, NotificationUI.prefab, GameToastSettings.asset)
# --------------------------------------------------------------------------------------------

FEED = "Assets/_Prefabs/UI Elements/In Game/GameFeedText.prefab"
FEED_UI = "Assets/_Prefabs/UI Elements/In Game/NotificationUI.prefab"
TOAST_SETTINGS = "Assets/_SO_Assets/Game Toasts/GameToastSettings.asset"
FD = dict(go="5396139043551737779", rt="764802058328981548", plate="6895495377854868985", item="5570562861110427101",
          content_vlg="7355427487428714261")
FEED_H = 36
TOAST_FIELDS = {"slideInOffset": "120", "ageAfterSeconds": "6", "agedAlpha": "0.5"}


def feed_text(text_):
    p = Patch(text_, "Feed/Item12")
    # The root carried the TMP itself, so it could have no plate: the TMP becomes the plate (same
    # fileID) and the text moves to a child beside a domain accent bar.
    p.body(FD["plate"], HM.img_body(tok("SurfaceVeryDark", 0.5), K.kit_sprite("kit_sliver10_fill"), sliced=True), FD["go"])
    p.add_comp(FD["go"], "th", 114, K.themed("SurfaceVeryDark", 0.5))
    p.add_comp(FD["go"], "hlg", 114, K.hv_layout(False, (13, 14, 0, 0), 0, 3, (1, 1), (0, 0)))
    p.add_comp(FD["go"], "csf", 114, K.fitter(2, 0))
    p.rect(FD["rt"], R((0, 0), (0, 0), (0, 0), (300, FEED_H), (0, 0.5)))
    acc = N("Feed/Accent", "Accent", R((0, 0), (0, 1), (0, 0), (3, 0), (0, 0.5)))
    acc.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceLight"))).add("le", 114, H.le(ignore=True))
    txt = N("Feed/Text", "Text", R(size=(260, FEED_H)))
    txt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("Pilot collected a crystal", "Body", tok("TextLight")))
    ids = [p.emit(n, FD["rt"], FD["item"]) for n in (acc, txt)]
    p.children(FD["rt"], ids)
    item = p.doc(FD["item"])
    for f, v in (("messageText", f"{{fileID: {p.ctx.c('Feed/Text', 'tmp')}}}"),
                 ("accentImage", f"{{fileID: {p.ctx.c('Feed/Accent', 'img')}}}"),
                 ("background", f"{{fileID: {FD['plate']}}}")):
        item = set_field(item, f, v)
    p.replace(FD["item"], item)
    return p.text()


def feed_ui_text(text_):
    p = Patch(text_, "FeedUI/Item12")
    p.replace(FD["content_vlg"], C.field_set(p.doc(FD["content_vlg"]), "m_Spacing", "4"))
    return p.text()


def toast_settings_text(text_):
    for k, v in TOAST_FIELDS.items():
        text_ = C.field_set(text_, k, v)
    return text_


# --------------------------------------------------------------------------------------------
# 12e - the Menu_Main freestyle HUD: the exit glyph and the FTUE counter's face
# --------------------------------------------------------------------------------------------

MENU = "Assets/_Scenes/Menu_Main.unity"
FTUE = "Assets/_Prefabs/UI Elements/Quest/InGameInstructionSet.prefab"
LIBERATION = "8f586378b4e144a9851e7b34d9b748ee"
CLOSE_GLYPH = K.chrome_sprite("chrome_close")
S.update(menuhud=script_guid("Assets/_Scripts/UI/MenuMiniGameHUD.cs"))


def menu_text(text_):
    p = Patch(text_, "Menu_Main/Item12")
    huds = p.by_script(S["menuhud"])
    assert len(huds) == 1, "Menu_Main should carry one MenuMiniGameHUD"
    d = p.doc(huds[0])
    p.replace(huds[0], insert_after_field(d, "domainVolumeIndicator", "exitGlyph", K.sprite_ref(CLOSE_GLYPH)))
    return p.text()


def ftue_text(text_):
    # The counter ("0 / 10") was the only off-spec face in the bar - LiberationSans, TMP's default.
    return (text_.replace(f"m_fontAsset: {{fileID: 11400000, guid: {LIBERATION}, type: 2}}",
                          f"m_fontAsset: {K.ref(11400000, K.ALDRICH[0], 2)}")
                 .replace(f"m_sharedMaterial: {{fileID: 2180264, guid: {LIBERATION}, type: 2}}",
                          f"m_sharedMaterial: {K.ref(K.ALDRICH[1], K.ALDRICH[0], 2)}"))


def pause_instance(p):
    pis = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1001 ") and f"m_SourcePrefab: {{fileID: 100100000, guid: {PAUSE_GUID}" in d]
    assert len(pis) == 1, "GameCanvas should nest one R_Pause_Menu_Panel"
    return pis[0]


def patch_pause_instance(p):
    """Prune the canvas's overrides into the deleted legacy buttons, and the two PauseMenu fields
    the canvas pointed at them (replayButton - the Resume button, hidden for every guest - and
    mainMenuButton). The prefab now wires its own buttons, so the canvas overrides none of them."""
    pi = pause_instance(p)
    d = p.doc(pi)
    entry = re.compile(r"    - target: \{fileID: (-?\d+), guid: %s,\s*type: 3\}\n      propertyPath: (.*)\n      value: .*\n      objectReference: .*\n(?:        .*\n)?" % PAUSE_GUID)

    def keep(m):
        fid, prop = m.group(1), m.group(2)
        if fid in LEGACY_PAUSE_IDS:
            return ""
        if fid == PAUSE_COMP and prop in ("replayButton", "mainMenuButton", "mainMenuButtonLabel"):
            return ""
        return m.group(0)
    p.replace(pi, entry.sub(keep, d))
    for fid, doc in list(p.sc.docs.items()):
        m = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: %s," % PAUSE_GUID, doc)
        if m and m.group(1) in LEGACY_PAUSE_IDS and " stripped\n" in doc.split("\n", 1)[0] + "\n":
            p.sc.remove(fid)
    # The pause button opened the window twice (Show, then OnClickPauseGameButton -> Show): one
    # call now, and that call toggles.
    btn = next(c for c in p.comp_ids(pause_button_go(p)) if p.sc.has(c) and f"guid: {S['button']}" in p.doc(c))
    bd = p.doc(btn)
    head, calls = bd.split("      m_Calls:\n", 1) if "      m_Calls:\n" in bd else (bd, "")
    if calls:
        blocks = re.split(r"(?=^      - m_Target: )", calls, flags=re.M)
        tail = ""
        if blocks and not blocks[-1].startswith("      - m_Target: "):
            tail = blocks.pop()
        last = blocks[-1] if blocks else ""
        m = re.search(r"^(?!      [ -]).*", last, re.M) if last else None
        if m and m.start() > 0:          # whatever follows the last call (the rest of the doc)
            blocks[-1], tail = last[:m.start()], last[m.start():] + tail
        blocks = [b for b in blocks if "        m_MethodName: Show\n" not in b]
        p.replace(btn, head + "      m_Calls:\n" + "".join(blocks) + tail)


# --------------------------------------------------------------------------------------------
# Follow-ups - the in-game toast container (quest / intensity toasts at the end of a match)
# --------------------------------------------------------------------------------------------

TOAST_CONTAINER = "ToastNotificationContainer"   # ToastNotificationAPI finds it BY NAME
TOAST_H = 200
TOAST_RIGHT, TOAST_TOP, TOAST_W = 40, 204, 720
# The in-match feed (NotificationUI, nested in MiniGameHUD) joins it on the right, under the toasts.
FEED_INST, FEED_RT, FEED_VLG = "2353765311602242758", "6510982914127131662", "7355427487428714261"
FEED_POS = dict(amin="{x: 1, y: 1}", pos=(-40, -424), size=(520, 300))


def set_instance_mod(p, inst, target, path, value):
    """Rewrite one existing override value on a PrefabInstance (asserting it exists, so a renamed
    source fails loudly instead of silently adding nothing)."""
    pat = re.compile(r"(    - target: \{fileID: %s, guid: \w+,\s*type: 3\}\n      propertyPath: %s\n      value: ).*\n"
                     % (target, re.escape(path)))
    doc = p.doc(inst)
    assert pat.search(doc), f"override {target}.{path} not found on {inst}"
    p.replace(inst, pat.sub(lambda m: m.group(1) + str(value) + "\n", doc, count=1))


def patch_feed_position(p):
    for path, v in (("m_AnchorMin.x", 1), ("m_AnchorMin.y", 1), ("m_AnchorMax.x", 1), ("m_AnchorMax.y", 1),
                    ("m_Pivot.x", 1), ("m_Pivot.y", 1), ("m_AnchoredPosition.x", FEED_POS["pos"][0]),
                    ("m_AnchoredPosition.y", FEED_POS["pos"][1]), ("m_SizeDelta.x", FEED_POS["size"][0]),
                    ("m_SizeDelta.y", FEED_POS["size"][1])):
        set_instance_mod(p, FEED_INST, FEED_RT, path, v)
    set_instance_mod(p, FEED_INST, FEED_VLG, "m_ChildAlignment", 2)   # rows right-aligned


def canvas_root_rt(p):
    roots = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!224 ") and "  m_Father: {fileID: 0}\n" in d]
    assert len(roots) == 1, "GameCanvas should have one root RectTransform"
    return roots[0]


def patch_toast_container(p):
    """ToastNotificationAPI looks for a GameObject named ToastNotificationContainer and drops the
    toast when there is none - so every quest-complete and intensity-unlocked toast raised at the
    end of a match (EndGameSequencer) was dropped with a warning: only Menu_Main had one. The match
    gets its own, bottom centre (the top is the score columns, and at the end the scoreboard's
    header), as the canvas's LAST child so it draws over the scoreboard, and never taking a click."""
    root = canvas_root_rt(p)
    # Rev 3 (2026-10-16): every toast on the RIGHT, under the pause hex (which ends 185 down).
    n = N("Toasts", TOAST_CONTAINER, R((1, 1), None, (-TOAST_RIGHT, -TOAST_TOP), (TOAST_W, TOAST_H), (1, 1)))
    n.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 2, (1, 1), (0, 0)))
    n.add("cg", 225, lambda ctx, go: H.canvas_group(True)(ctx, go).replace("  m_Interactable: 1\n  m_BlocksRaycasts: 1\n",
                                                                           "  m_Interactable: 0\n  m_BlocksRaycasts: 0\n"))
    rt = p.emit(n, root, root)
    p.children(root, [k for k in p.kids(root) if k != rt] + [rt])


def retire_thumb_cursors(p):
    """ThumbCursors (two thumb cursors and two nested ThumbPerimeter instances) is inactive and
    nothing in the canvas, code or any scene references it - measured 2026-10-15. Of the objects
    the item 12 inventory called dead, it is the ONLY one that is: LeftDisplay's counters are written
    by Brood Rush's drone events, the inline Scoreboard text is written unguarded on every reset,
    and RoundTime, LifeFormCounter and Pip are wired into MultiplayerHUDView and kept on purpose."""
    gos = [f for f, d in p.sc.docs.items() if d.startswith("--- !u!1 ") and "  m_Name: ThumbCursors\n" in d]
    for go in gos:
        assert p.sc.field(go, "m_IsActive") == "0", "ThumbCursors is active again - it is no longer dead"
        rt = p.sc.rect_of(go)
        parent = re.search(r"m_Father: \{fileID: (-?\d+)\}", p.doc(rt)).group(1)
        p.remove_subtree(go)
        p.children(parent, [k for k in p.kids(parent) if k != rt])


def canvas_text(text_):
    p = Patch(text_, "GameCanvas/Item12")
    retire_thumb_cursors(p)
    patch_toast_container(p)
    patch_feed_position(p)
    patch_pause_instance(p)
    prune_instance_mods(p, ENDGAME_GUID, LEGACY_REVEAL_IDS)
    patch_scoreboard(p)
    patch_goal_rows(p)
    patch_pause(p)
    patch_ready(p)
    patch_waiting_and_countdown(p)
    return p.text()


# --------------------------------------------------------------------------------------------
# 12a - the domain score column (DomainScorePanel.prefab)
# --------------------------------------------------------------------------------------------

def panel_layer(key, sprite, token=None, alpha=1.0, color=(1, 1, 1, 1)):
    n = N(f"Col/{key}", key, STRETCH)
    n.add("cr", 222, K.canvas_renderer())
    n.add("img", 114, K.image(tok(token, alpha) if token else color, K.kit_sprite(sprite), True))
    if token:
        n.add("th", 114, K.themed(token, alpha))
    n.add("le", 114, K.layout_element(ignore=True))
    return n


def panel_text(text_):
    p = Patch(text_, "DomainScorePanel/Item12")
    I = PANEL
    assert p.sc.script(PANEL_COMP) == S["panel"], "DomainScorePanel's component moved"
    # Root: 168 x 96, the score over the pilot chips.
    d = p.doc(I["root_rt"])
    p.replace(I["root_rt"], C.field_set(d, "m_SizeDelta", K.v2(COL_W, COL_H)))
    vlg = p.doc(I["vlg"])
    vlg = re.sub(r"  m_Padding:\n    m_Left: .*\n    m_Right: .*\n    m_Top: .*\n    m_Bottom: .*\n",
                 "  m_Padding:\n    m_Left: 5\n    m_Right: 5\n    m_Top: 10\n    m_Bottom: 10\n", vlg)
    p.replace(I["vlg"], C.field_set(vlg, "m_ChildAlignment", "1"))
    p.replace(I["sum_rt"], C.field_set(p.doc(I["sum_rt"]), "m_SizeDelta", K.v2(0, 44)))
    tmp = p.doc(I["sum_tmp"])
    for f in ("m_fontSize", "m_fontSizeBase"):
        tmp = C.field_set(tmp, f, "44")
    p.replace(I["sum_tmp"], tmp)
    # Base, team tint and hairline: generated layers behind the number, outside the layout.
    # Emitted in reverse after one fixed anchor (the Glow's last document), so the file reads
    # Base, Tint, Border and a re-run lands every document in the same place.
    layers = {}
    for key, sprite, token, alpha in (("Border", "kit_sliver14_line1", None, 1.0),
                                      ("Tint", "kit_sliver14_fill", None, 1.0),
                                      ("Base", "kit_sliver14_fill", "SurfaceBlack", 0.35)):
        layers[key] = p.emit(panel_layer(key, sprite, token, alpha), I["root_rt"], I["glow_le"])
    # The cap: the old bottom accent strip, moved to the top edge and out of the layout.
    p.rect(I["accent_rt"], R((0.5, 1), (0.5, 1), (0, 0), (CAP_W, CAP_H), (0.5, 1)))
    p.add_comp(I["accent_go"], "le", 114, K.layout_element(ignore=True))
    # Motion: the columns pop in one after another (their own CanvasGroup; scale, so the bar's
    # HorizontalLayoutGroup is never fought).
    p.add_comp(PANEL_ROOT_GO, "in", 114, HM.M.entrance("Pop", by_sibling=True))
    p.children(I["root_rt"], [I["glow_rt"], layers["Base"], layers["Tint"], layers["Border"],
                              I["sum_rt"], I["avatars_rt"], I["accent_rt"]])
    comp = p.doc(PANEL_COMP)
    for f, v in (("domainIndicatorImage", f"{{fileID: {p.ctx.c('Col/Tint', 'img')}}}"),
                 ("indicatorAlpha", "0.12"), ("accentAlpha", "1")):
        comp = set_field(comp, f, v)
    comp = insert_after_field(comp, "accentImage", "borderImage", f"{{fileID: {p.ctx.c('Col/Border', 'img')}}}")
    comp = insert_after_field(comp, "borderImage", "borderAlpha", "0.9")
    for f, v in (("teammateChipAlpha", "0.45"), ("glowRestAlpha", "0"), ("glowBreathDepth", "0"),
                 ("glowBreathPeriod", "3.2"), ("glowScoreAlpha", "0.5")):
        comp = insert_after_field(comp, "avatarEntryPrefab", f, v) if f == "teammateChipAlpha" else set_field(comp, f, v)
    p.replace(PANEL_COMP, comp)
    return p.text()


# --------------------------------------------------------------------------------------------
# Outputs, validation
# --------------------------------------------------------------------------------------------

TRANSFORMS = {LOCKUP: lockup_text, GLYPHS: glyphs_text, CANVAS: canvas_text, PANEL["path"]: panel_text, PAUSE: pause_text,
              CONNECT: connect_text, ENDGAME: endgame_text, FEED: feed_text, FEED_UI: feed_ui_text,
              TOAST_SETTINGS: toast_settings_text, MENU: menu_text, FTUE: ftue_text}


def outputs():
    out = {path: fn(open(os.path.join(REPO, path), encoding="utf-8").read()) for path, fn in TRANSFORMS.items()}
    out.update(sb_prefabs())
    return out


def docs_of(text_):
    return {m.group(2): m.group(0) for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n.*?(?=^--- |\Z)", text_, re.S | re.M)}


def check_local_refs(path, text_):
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", text_, re.M)) | {"0"}
    for m in re.finditer(r"\{fileID: (-?\d+)\}", text_):
        assert m.group(1) in anchors, f"{path}: dangling local fileID {m.group(1)}"


def p_fid(key):
    """A deterministic fileID connect_text gives `key` (a node#comp or a go#comp key)."""
    return str(P.Ctx("ConnectingPanel/Item12").fid(key))


def validate(files):
    lk = files[LOCKUP]
    for k, v in LOCKUP_FIELDS.items():
        assert f"  {k}: {v}\n" in lk, f"AbilityLockupStyle: {k} is not the style-B value"
    for k, v in KEYCAP_FIELDS:
        assert f"  {k}: {v}\n" in lk, f"AbilityLockupStyle: {k} is not set"
    for k, v in NO_ELEMENT_COLOUR_FIELDS:
        assert f"  {k}: {v}\n" in lk, f"AbilityLockupStyle: {k} must be off - elements carry no colour (Docs/UI_COLOR_RULES.md)"
    # The LOCKED geometry is untouched (VESSEL_HUD_AND_ELEMENTS: changeable only within the asserted limits).
    for k, v in (("plateWidth", "104"), ("cardPitch", "128"), ("abilityCellHeight", "88"), ("petalCellHeight", "88"),
                 ("iconBoxSize", "60"), ("petalFlowerSize", "60"), ("rowMarginRight", "40"), ("rowMarginBottom", "44")):
        assert f"  {k}: {v}\n" in lk, f"AbilityLockupStyle: the LOCKED geometry moved ({k})"
    gl = files[GLYPHS]
    for b, label in PAD_LABELS.items():
        if re.search(rf"^  - binding: {b}\n", gl, re.M):
            assert re.search(rf"^  - binding: {b}\n(?:    .*\n)*?    padLabel: {label}\n", gl, re.M), f"ControlGlyphSet: binding {b} is not named {label}"

    for path_, text_ in files.items():
        if path_.endswith((".prefab", ".unity")):
            dups = T.duplicate_stubs(text_)
            assert not dups, f"{os.path.basename(path_)}: two stand-ins for one nested object {dups[:3]} - Unity binds one, every reference to the other reads as destroyed"
    cv = files[CANVAS]
    check_local_refs(CANVAS, cv)
    docs = docs_of(cv)
    rows = [f for f, d in docs.items() if f"guid: {S['goalrow']}" in d]
    assert len(rows) == 3, "GameCanvas lost a GoalRow"
    for row in rows:
        d = docs[row]
        for f in RETIRED_GOALROW_FIELDS:
            assert f"  {f}:" not in d, f"GoalRow {row} still carries the retired {f}"
        for f, v in GOALROW_FIELDS:
            assert f"  {f}: {v}\n" in d, f"GoalRow {row}: {f} is not {v}"
        plate = re.search(r"  plate: \{fileID: (-?\d+)\}", d).group(1)
        assert f"guid: {S['image']}" in docs[plate], f"GoalRow {row}: the plate is not the glass Image"
        assert f"guid: {K.kit_sprite('kit_sliver10_fill')}" in docs[plate], f"GoalRow {row}: the plate is not slivered"
        label = re.search(r"  label: \{fileID: (-?\d+)\}", d).group(1)
        assert f"guid: {K.SEMIBOLD[0]}" in docs[label], f"GoalRow {row}: the label is not Chakra Petch SemiBold"
    assert f"guid: {S['trapezoid']}" not in "".join(docs[re.search(r"  plate: \{fileID: (-?\d+)\}", docs[r]).group(1)] for r in rows), \
        "a goal plate is still a TrapezoidGraphic"
    vol = [f for f, d in docs.items() if f"guid: {S['volume']}" in d]
    assert len(vol) == 1, "the pause button must carry exactly one DomainVolumeIndicator"
    assert f"guid: {PAUSE_GLYPH}" in docs[vol[0]], "the pause hex has no centre glyph"
    assert "  padKeycap: Start\n" in docs[vol[0]] and "  keyboardKeycap: Esc\n" in docs[vol[0]], \
        "the pause hex does not name its control (Start / Esc)"
    hud = [d for d in docs.values() if f"guid: {S['hud']}" in d]
    assert len(hud) == 1 and f"  volumeIndicator: {{fileID: {vol[0]}}}" in hud[0], "MiniGameHUD.volumeIndicator is not the pause hex"

    # 12b
    view = next(f for f, d in docs.items() if f"guid: {S['hudview']}" in d)
    btn = re.search(r"  readyButton: \{fileID: (-?\d+)\}", docs[view]).group(1)
    assert f"guid: {S['kitbutton']}" in docs[btn], "the Ready button is not a KitButton"
    assert f"  variant: {K.VARIANTS['Play']}\n" in docs[btn], "the Ready button is not the Play variant"
    assert "propertyPath: m_text\n      value: A\n" in cv, "the Ready button has no A keycap"
    cbp = [d for d in docs.values() if f"guid: {S['cbp']}" in d and f"m_GameObject: {{fileID: {re.search(r'm_GameObject: {fileID: (-?[0-9]+)}', docs[btn]).group(1)}}}" in d]
    assert len(cbp) == 1 and "  activationButton: 6\n" in cbp[0], "the Ready button no longer answers the pad's A (South)"
    wait = re.search(r"  readyWaiting: \{fileID: (-?\d+)\}", docs[view])
    assert wait and wait.group(1) in docs, "MiniGameHUDView.readyWaiting is not wired"
    wd = docs[wait.group(1)]
    for f in ("canvasGroup", "countText", "roster", "chipTemplate"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, wd, re.M), f"ReadyWaitingPanel.{f} is not wired"
    assert f"guid: {TOAST_CHANNEL}" in wd, "ReadyWaitingPanel does not listen to the game toast channel"
    chip = re.search(r"  chipTemplate: \{fileID: (-?\d+)\}", wd).group(1)
    for f in ("ring", "avatar", "state", "group"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, docs[chip], re.M), f"ReadyWaitingChip.{f} is not wired"
    pi = next(d for d in docs.values() if d.startswith("--- !u!1001 ") and f"guid: {COUNTDOWN_PREFAB}" in d.split("m_SourcePrefab:")[1])
    for prop in ("countdownText", "waitingPanel", "countdownDisplay", "beatHex", "beatHexGroup", "goRing"):
        m = re.search(r"      propertyPath: %s\n      value: .*\n      objectReference: \{fileID: (-?\d+)\}" % prop, pi)
        assert m and m.group(1) in docs, f"CountdownTimer.{prop} is not wired"
        assert pi.count(f"propertyPath: {prop}\n") == 1, f"CountdownTimer.{prop} is overridden twice"
    beat = re.search(r"      propertyPath: countdownText\n      value: .*\n      objectReference: \{fileID: (-?\d+)\}", pi).group(1)
    assert f"  m_fontSize: {BEAT_SIZE}\n" in docs[beat], f"the countdown beat is not {BEAT_SIZE} px"
    assert "  m_enableAutoSizing: 0\n" in docs[beat], "the countdown beat auto-sizes - it would be capped at 48 px"
    hexd = docs[re.search(r"      propertyPath: beatHex\n      value: .*\n      objectReference: \{fileID: (-?\d+)\}", pi).group(1)]
    assert f"guid: {S['crystal']}, type: 3}}" in hexd, "the count's hex is not a LoadCrystalGraphic (it cannot fill)"

    # 12c
    pz = files[PAUSE]
    check_local_refs(PAUSE, pz)
    zd = docs_of(pz)
    assert not (LEGACY_PAUSE_IDS & set(zd)), "the legacy pause buttons survive"
    comp = zd[PAUSE_COMP]
    for f in ("resumeButton", "settingsButton", "leaveButton", "windowGroup", "modeTitle", "modeDetail",
              "mainMenuButton", "mainMenuButtonLabel", "settingsModalWindowManager"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, comp, re.M), f"PauseMenu.{f} is not wired"
    assert "  replayButton: {fileID: 0}\n" in comp, "PauseMenu.replayButton still hides a button from guests"
    for name in ("rowLabels", "rowValues", "rowGroups"):
        block = re.search(r"^  %s:\n((?:  - .*\n)*)" % name, comp, re.M)
        assert block and len(re.findall(r"\{fileID: [1-9-]", block.group(1))) == 3, f"PauseMenu.{name} needs three wired rows"
    pads = {}
    for f, d in zd.items():
        if f"guid: {S['cbp']}" in d:
            pads[re.search(r"  activationButton: (\d+)", d).group(1)] = d
    assert set(pads) == {"4", "5", "7"}, f"the pause window's pad buttons are {sorted(pads)}, not Y/B/X (4/5/7)"
    assert "6" not in pads, "a pause button answers A"
    for d in pads.values():
        assert re.search(r"  canvasGroup: \{fileID: [1-9-]", d), "a pause shortcut is not gated by the window (B would also resume under Settings)"
    pi = next(d for d in docs.values() if d.startswith("--- !u!1001 ") and f"guid: {PAUSE_GUID}" in d.split("m_SourcePrefab:")[1])
    for fid in re.findall(r"- target: \{fileID: (-?\d+), guid: %s" % PAUSE_GUID, pi):
        assert fid not in LEGACY_PAUSE_IDS, f"GameCanvas still overrides the deleted pause button object {fid}"
    assert not re.search(r"propertyPath: (replayButton|mainMenuButton)\n", pi), "GameCanvas still overrides PauseMenu's buttons"
    pb = next(d for d in docs.values() if f"guid: {S['button']}" in d and "m_MethodName: OnClickPauseGameButton" in d)
    assert "m_MethodName: Show\n" not in pb, "the pause button still opens the window twice"

    cn = files[CONNECT]
    check_local_refs(CONNECT, cn)
    assert f"guid: {WHITRABT}" not in cn, "the connecting panel still draws in the off-spec whitrabt face"
    cd = docs_of(cn)
    comp = cd[CN["comp"]]
    for f in ("gate", "gameModeText", "stepBar", "playerRoster"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, comp, re.M), f"ConnectingPanelController.{f} is not wired"
    for f, v in (("games", K.ref(11400000, ARCADE_GAMES, 2)), ("introWords", K.ref(11400000, INTRO_LIBRARY, 2)),
                 ("coveredLayBudgetMs", "80"), ("dwellSeconds", "1")):
        assert f"  {f}: {v}\n" in comp, f"ConnectingPanelController.{f} is not {v}"
    for f in ("connectingCamera", "arenaPreview", "backdropArt", "statusText", "progressText", "elapsedText", "watchedLayBudgetMs",
              "progressSlider", "phaseTitle", "phaseTitleEntrance", "connectingTitle", "buildingTitle", "settlingTitle", "waitingTitle"):
        assert not re.search(r"^  %s:" % f, comp, re.M), f"ConnectingPanelController still carries the retired {f}"
    # The screen COVERS the arena: nothing behind it renders the world (Docs/MENU_REDESIGN_B.md section 99.2).
    assert not re.search(r"^--- !u!20 ", cn, re.M), "a camera renders behind the connecting panel again"
    assert f"guid: {C.script('ConnectingArenaPreview')}" not in cn, "the connecting panel still renders an arena preview"
    assert "  m_Color: {r: 0, g: 0, b: 0, a: 0}\n" in cd[CN["root_img"]], \
        "the panel's own Image is opaque again - it would fill the gate's hole before the arena shows through it"
    gates = [f for f, d in cd.items() if f"guid: {S['gate']}, type: 3}}" in d]
    assert len(gates) == 1 and re.search(r"^  gate: \{fileID: %s\}" % gates[0], comp, re.M), "the gate view is not the controller's"
    gv = cd[gates[0]]

    def target(doc, name, script=None, owner="ConnectingGateView"):
        m = re.search(r"^ +%s: \{fileID: (-?\d+)\}" % name, doc, re.M)
        assert m and m.group(1) in cd, f"{owner}.{name} is not wired"
        if script:
            assert f"guid: {script}, type: 3}}" in cd[m.group(1)], f"{owner}.{name} is not a {script}"
        return cd[m.group(1)]

    def block(doc, name):
        m = re.search(r"^  %s:\n((?:  - .*\n)*)" % name, doc, re.M)
        return m.group(1) if m else ""
    cover = block(gv, "cover")
    assert len(re.findall(r"\{fileID: [1-9-]", cover)) == 2, "the gate's cover is not the base and the sea"
    for fid in re.findall(r"\{fileID: (-?\d+)\}", cover):
        assert f"guid: {S['aperture']}, type: 3}}" in cd[fid], "a cover layer is not a HexApertureGraphic (no hole to open)"
    target(gv, "gateRing", S["crystal"])
    for name in ("enteringText", "loadingLabel"):
        target(gv, name)
    assert re.search(r"^  content: \{fileID: %s\}" % p_fid(CN["safe_go"] + "#cg"), gv, re.M), "the gate fades nothing as it opens"
    assert f"  veilWords: {K.ref(11400000, VEIL_WORDS, 2)}\n" in gv, "the gate's words are not the cell-change screen's"

    # Rev 91 F: what the tour replaced is gone, through this generator.
    for key in RETIRED_CONNECT_KEYS:
        assert p_fid(key + "#go") not in cd, f"the retired {key} is still in the loading screen"
    assert f"guid: {K.meta_guid('Assets/_Graphics/Codex/ethirion_omni.png')}" not in cn, "the Omni crystal is still on the loading screen"
    assert not re.search(r"^  m_FillRect:", cn, re.M), "the Slider is back on the track - it and KitStepBar would both anchor the fill"

    # The board's sea and motion (section 186): the fields drift between the black base and the sea lines, in a group
    # the gate fades with its content (they have no hole), and the Ark drifts and the Vessels hull bobs.
    assert re.search(r"^  backdrop: \{fileID: %s\}" % p_fid("Gate/Cover/Fields#cg"), gv, re.M), \
        "the gate does not fade the sea's fields - they would cover the arena as the hole opens"
    order = re.findall(r"^  - \{fileID: (-?\d+)\}", cd[CN["root_rt"]].split("  m_Children:\n", 1)[1].split("  m_Father:", 1)[0], re.M)
    base_i, fields_i, sea_i = (order.index(p_fid(k + "#rt")) for k in ("Gate/Cover/Base", "Gate/Cover/Fields", "Gate/Cover/Sea"))
    assert base_i < fields_i < sea_i, "the sea's fields must draw over the black base and under the sea lines"
    def drift_on(key):
        go = p_fid(key + "#go")
        return [d for d in cd.values() if f"guid: {S['drift']}, type: 3}}" in d and f"  m_GameObject: {{fileID: {go}}}" in d]
    for i, (_, _, _, seconds, _, _) in enumerate(SEA_FIELDS):
        got = drift_on(f"Gate/Cover/Fields/Field{i}")
        assert len(got) == 1 and f"  seconds: {K.f32(seconds)}\n" in got[0], f"the sea's field {i + 1} does not drift"
    for key, what in (("Tour/Show1/Ark", ARK_DRIFT), ("Tour/Show3/Hull", HULL_BOB)):
        got = drift_on(key)
        assert len(got) == 1 and f"  seconds: {K.f32(what['seconds'])}\n" in got[0], f"{key} lost the board's motion"

    # The tour: five chapters, each a picture and its words, one on show at rest, every word an authored field.
    tours = [f for f, d in cd.items() if f"guid: {S['tour']}, type: 3}}" in d]
    assert len(tours) == 1 and re.search(r"^  tour: \{fileID: %s\}" % tours[0], gv, re.M), "the tour is not the gate view's"
    tv = cd[tours[0]]
    assert re.findall(r"^  - title: (.*)$", tv, re.M) == [K.yaml_scalar(t) for t, _l in TOUR], "the tour's chapters are not the board's five, in order"
    assert re.findall(r"^    line: (.*)$", tv, re.M) == [K.yaml_scalar(l) for _t, l in TOUR], "a chapter's line is not the board's"
    for f in ("show", "words", "counterText", "titleText", "lineText", "indexText", "indexRule"):
        ids = re.findall(r"^    %s: \{fileID: (-?\d+)\}" % f, tv, re.M)
        assert len(ids) == len(TOUR) and len(set(ids)) == len(TOUR) and all(i in cd and i != "0" for i in ids), \
            f"a chapter's {f} is not wired (or two chapters share one)"
    shown = [re.search(r"^  m_Alpha: (\S+)", cd[i], re.M).group(1) for i in re.findall(r"^    show: \{fileID: (-?\d+)\}", tv, re.M)]
    assert shown == ["1"] + ["0"] * (len(TOUR) - 1), "at rest the tour does not show exactly its first chapter"
    template = re.search(r"^  counterTemplate: (.*)$", tv, re.M).group(1)
    assert "{n}" in template and "{total}" in template, "the tour's counter template lost a token"
    for what, guid in [("Squad pilot " + c, g) for c, g in PORTRAIT.items()] + [("the Ark", ARK_ART)] + [(h + "'s card", g) for h, g in HULL_CARD.items()]:
        assert f"m_Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}" in cn, f"the tour has no picture of {what}"
    ark_meta = open(os.path.join(REPO, "Assets/_Graphics/UI/Loading/loading_tour_ark.png.meta"), encoding="utf-8").read()
    assert "  textureType: 8\n" in ark_meta and "  spriteMode: 1\n" in ark_meta, "the Ark's picture is not imported as a sprite"

    # The bar: one KitStepBar on the old slider's track, cut where ArenaLoadProgress's shares end.
    bars = [f for f, d in cd.items() if f"guid: {S['stepbar']}, type: 3}}" in d]
    assert len(bars) == 1 and re.search(r"^  stepBar: \{fileID: %s\}" % bars[0], comp, re.M), "the step bar is not the controller's"
    sb = cd[bars[0]]
    ends = [float(x) for x in re.findall(r"^  - (\S+)$", block(sb, "stepEnds"), re.M)]
    assert ends == [float(K.f32(e)) for e in STEP_ENDS], \
        f"the bar's steps end at {ends}, not at ArenaLoadProgress's measured shares {list(STEP_ENDS)}"
    assert all(a < b for a, b in zip(ends, ends[1:])) and ends[-1] == 1.0, "the bar's steps do not rise to 1"
    names = re.findall(r"^  - (.+)$", block(sb, "stepNames"), re.M)
    assert len(names) == len(ends), "the bar does not name every step"
    for f in ("stepLabels", "stepMarkers", "stepMarkerFills"):
        assert len(re.findall(r"\{fileID: [1-9-]", block(sb, f))) == len(ends), f"KitStepBar.{f} does not cover every step"
    assert f"  fill: {{fileID: {CN['fill_rt']}}}\n" in sb and f"  fillGraphic: {{fileID: {CN['fill_img']}}}\n" in sb, \
        "the bar does not fill the track GameCanvas overrides"
    glint = re.search(r"^  glint: \{fileID: (-?\d+)\}", sb, re.M)
    assert glint and f"  m_Father: {{fileID: {CN['fill_rt']}}}" in cd[glint.group(1)], "the glint does not travel along the fill"
    assert f"guid: {RECT_MASK_2D}" in cn, "the fill does not clip its glint"
    for name in ("stepText", "percentText"):
        target(sb, name, owner="KitStepBar")
    assert re.search(r"^  doneWord: \S", sb, re.M), "the bar has no done word"
    assert f"  doneToken: {K.TOKEN_INDEX['Cta']}\n" in sb, "the bar's done state is not Lime"
    assert f"  fillToken: {K.TOKEN_INDEX['Cta']}\n" not in sb, "the bar is Lime before the load is done"
    assert f"  m_Color: {K.col(tok('Cta'))}\n" not in cn, "Lime is drawn at rest - on this screen it means done and nothing else"
    for i, e in enumerate(STEP_ENDS[:-1]):
        assert f"  m_AnchorMin: {K.v2(e, 0)}\n" in cd[p_fid(f"Gate/Bar/Cut{i}#rt")], f"the break after step {i + 1} is not at its share"

    # The roster still counts who the load waits on; F draws no pilot rows.
    ro = target(comp, "playerRoster", script_guid("Assets/_Scripts/UI/Elements/ConnectingPlayerRoster.cs"), owner="ConnectingPanelController")
    assert ro == cd[CN["roster"]], "the controller's roster is not the kept one"
    assert "  m_Alpha: 0\n" in cd[p_fid(CN["icons_go"] + "#hide")], "the pilot rows are drawn again - Rev 91 F has none"
    for f in ("templateNameText", "templateStateText", "templatePip", "countText", "templateDomainImage"):
        assert f"  {f}: {{fileID: 0}}\n" in ro, f"the hidden pilots list's {f} points at a row part that no longer exists"
    assert "  showAI: 0\n" in ro, "the hidden pilots list builds a row per AI seat for nobody to see"
    # GameCanvas overrides four objects in this prefab: they must still exist, or the overrides dangle.
    guid = K.meta_guid(CONNECT)
    for fid in set(re.findall(r"- target: \{fileID: (-?\d+), guid: %s" % guid, files[CANVAS])):
        assert fid in cd, f"GameCanvas overrides {fid} in ConnectingPanel, which no longer exists"
    assert "m_Sprite: {fileID: 0}" in cd[CN["fill_img"]], "the connecting bar still wears the legacy loading sprite"

    # 12d - reveal
    eg = files[ENDGAME]
    check_local_refs(ENDGAME, eg)
    ed = docs_of(eg)
    assert not (LEGACY_REVEAL_IDS & set(ed)), "the reveal's purple ornaments survive"
    for f in ("winnerText", "winnerRule", "detailText", "gameOverCounterText", "toastCanvasGroup"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, ed[EG["seq"]], re.M), f"EndGameSequencer.{f} is not wired"
    msg = ed[EG["msg_tmp"]]
    assert "  m_enableAutoSizing: 1\n" in msg and f"  m_fontSizeMax: {MSG_SIZE}\n" in msg, "the reveal line does not auto-size (long lines overflow)"
    assert f"  m_Color: {K.col(tok('TextLight'))}\n" in msg, "the reveal line is not Light"
    egi = next(d for d in docs.values() if d.startswith("--- !u!1001 ") and f"guid: {ENDGAME_GUID}" in d.split("m_SourcePrefab:")[1])
    for fid in re.findall(r"- target: \{fileID: (-?\d+), guid: %s" % ENDGAME_GUID, egi):
        assert fid not in LEGACY_REVEAL_IDS, f"GameCanvas still overrides the deleted reveal ornament {fid}"
    # 12d - scoreboard
    assert not any(d.startswith("--- !u!1001 ") and f"guid: {GAMEOVER_GUID}" in d.split("m_SourcePrefab:")[1] for d in docs.values()), \
        "GameCanvas still nests the legacy GameOverPanel"
    sbd = next(d for d in docs.values() if f"guid: {S['scoreboard']}" in d)
    assert "  wireButtonsInCode: 1\n" in sbd, "the Scoreboard does not wire its buttons"
    for f in ("endGameObject", "scoreboardPanel", "statsContainer", "BannerImage", "BannerText", "playerCardContainer",
              "playAgainButton", "playAgainLabel", "mainMenuButton", "leaveLobbyButton", "continueButton", "modeText"):
        m = re.search(r"^  %s: \{fileID: (-?\d+)\}" % f, sbd, re.M)
        assert m and m.group(1) in docs, f"Scoreboard.{f} is not wired"
        assert f"guid: {P.asset_guid(SB_PANEL)}" in docs[m.group(1)], f"Scoreboard.{f} does not point into ScoreboardPanel"
    assert f"guid: {P.asset_guid(SB_ROW)}" in sbd and f"guid: {P.asset_guid(SB_STAT)}" in sbd, "the Scoreboard does not clone the style-B row and stat"
    host_go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", sbd).group(1)
    host_rt = next(d for d in docs.values() if d.startswith("--- !u!224 ") and f"m_GameObject: {{fileID: {host_go}}}" in d)
    for key, val in SCOREBOARD_HOST_RECT:
        assert re.search(r"^  %s: %s$" % (key, re.escape(val)), host_rt, re.M), \
            f"the Scoreboard's host is not stretched over the canvas ({key} is not {val}): the board would collapse"
    sp = files[sb_path(SB_PANEL)]
    check_local_refs(sb_path(SB_PANEL), sp)
    spd = docs_of(sp)
    fk = K.FileIds(SB_PANEL)
    for key, _, _, _, pad, _ in SB_BUTTONS:
        cbp = spd[str(fk(f"Sb/{key}#cbp"))]
        assert f"  activationButton: {pad}\n" in cbp, f"{key} answers the wrong pad button"
        bd = spd[str(fk(f"Sb/{key}/Border#img"))]
        assert not any(f"guid: {g}" in bd for g in HM.M.RETIRED_RIM_GUIDS), \
            f"{key}'s border carries an element rim again (Docs/UI_COLOR_RULES.md)"
    assert {b[4] for b in SB_BUTTONS if b[0] in ("PlayAgain", "Continue")} == {6} and \
           {b[4] for b in SB_BUTTONS if b[0] in ("MainMenu", "LeaveLobby")} == {5}, "A and B share an end-of-match button"
    row = docs_of(files[sb_path(SB_ROW)])[str(K.FileIds(SB_ROW)("root#card"))]
    for f in ("playerAvatarImage", "playerNameText", "playerScoreText", "rankText", "domainText", "borderImage",
              "crystalRewardRoot", "crystalRewardText", "dataPanelsRoot", "secondaryStatText", "teamStripe", "shareBar", "glassFill"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, row, re.M), f"ScoreboardRow: {f} is not wired"
    # Option B: dark glass and a thin stripe - never a slab of team colour again.
    assert "  backgroundImage: {fileID: 0}\n" in row, "the scoreboard row is a full team fill again (backgroundImage wired)"
    for f in ("wonBorderMaterial", "lostBorderMaterial", "localBorderMaterial"):
        assert f"  {f}: {NO_ROW_RIM}\n" in row, f"ScoreboardRow: {f} lights the border again (no rims, Docs/UI_COLOR_RULES.md)"
    assert f"  localBorderToken: {K.TOKEN_INDEX['TextLight']}\n" in row and \
           f"  localGlassTintToken: {K.TOKEN_INDEX['NeutralLightest']}\n" in row, "the local scoreboard row draws in the selection blue again"
    for name in (SB_ROW, SB_STAT, SB_HEADER):
        check_local_refs(sb_path(name), files[sb_path(name)])
    hd = docs_of(files[sb_path(SB_HEADER)])[str(K.FileIds(SB_HEADER)("root#header"))]
    for f in ("domainText", "rule", "summaryText"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, hd, re.M), f"ScoreboardTeamHeader: {f} is not wired"
    stripd = spd[str(fk("Reward#strip"))]
    for f in ("amountText", "fromText", "toText"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, stripd, re.M), f"ScoreboardRewardStrip: {f} is not wired"
    roster = spd[str(fk("Sb/PlayAgain#roster"))]
    assert re.search(r"^  countText: \{fileID: [1-9-]", roster, re.M), "the rematch count line is not wired"
    sbd = next(d for d in docs.values() if f"guid: {S['scoreboard']}" in d)
    assert f"  teamHeaderPrefab: {sb_ref(SB_HEADER, 'root#header')}" in sbd, "Scoreboard.teamHeaderPrefab is not the team header"
    assert re.search(r"^  rewardStrip: \{fileID: [1-9-]", sbd, re.M), "Scoreboard.rewardStrip is not wired"
    # Rev 23 D: the team-versus layout - the view reached from the Scoreboard, its two sides wired, the ladder's list it
    # steps aside for, and the half-width row it spawns.
    m = re.search(r"^  versusView: \{fileID: (-?\d+)\}", sbd, re.M)
    assert m and m.group(1) in docs and f"guid: {P.asset_guid(SB_PANEL)}" in docs[m.group(1)], \
        "Scoreboard.versusView is not wired into ScoreboardPanel (every match would stay the ladder)"
    assert f"  versusRowPrefab: {sb_ref(SB_VSROW, 'root#card')}" in sbd, "Scoreboard.versusRowPrefab is not the versus row"
    vsd = spd[str(fk("Versus#versus"))]
    assert f"  ladderGroup: {{fileID: {fk('List#cg')}}}\n" in vsd and f"  group: {{fileID: {fk('Versus#cg')}}}\n" in vsd, \
        "ScoreboardVersusView does not swap with the ladder's list"
    for side in ("Left", "Right"):
        for f, key in (("nameText", f"Versus/{side}/Name#tmp"), ("scoreText", f"Versus/{side}/Score#tmp"),
                       ("rows", f"Versus/{side}/Rows#rt"), ("shareFill", f"Versus/Share/Bar/{side}#img")):
            assert f"{f}: {{fileID: {fk(key)}}}" in vsd, f"ScoreboardVersusView's {side} side: {f} is not wired"
    vrow = files[sb_path(SB_VSROW)]
    check_local_refs(sb_path(SB_VSROW), vrow)
    vcard = docs_of(vrow)[str(K.FileIds(SB_VSROW)("root#card"))]
    for f in ("playerAvatarImage", "playerNameText", "playerScoreText", "rankText", "secondaryStatText", "shareBar"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, vcard, re.M), f"ScoreboardVersusRow: {f} is not wired"
    assert f"m_SizeDelta: {{x: {VS_ROW_W}, y: {ROW_HT}}}" in vrow and 2 * VS_ROW_W + VS_COL_GAP == LIST_W, \
        "the versus columns no longer fit the ladder's width"

    assert "  m_Name: ThumbCursors\n" not in cv, "the dead ThumbCursors subtree survives"
    # Follow-up - toast container
    names = re.findall(r"^  m_Name: %s$" % TOAST_CONTAINER, cv, re.M)
    assert len(names) == 1, "GameCanvas needs exactly one ToastNotificationContainer (the toast API finds it by name)"
    root_rt = next(f for f, d in docs.items() if d.startswith("--- !u!224 ") and "  m_Father: {fileID: 0}\n" in d)
    kids = re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", docs[root_rt], re.M).group(1))
    last_go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", docs[kids[-1]]).group(1)
    assert f"  m_Name: {TOAST_CONTAINER}\n" in docs[last_go], "the toast container is not drawn last (the scoreboard would cover it)"
    toast_rt = kids[-1]
    assert "  m_AnchorMin: {x: 1, y: 1}\n" in docs[toast_rt] and f"  m_AnchoredPosition: {{x: -{TOAST_RIGHT}, y: -{TOAST_TOP}}}\n" in docs[toast_rt], \
        "the in-game toasts are not on the right under the pause hex"
    fi = docs[FEED_INST]
    for path, v in (("m_AnchorMin.x", 1), ("m_AnchoredPosition.x", FEED_POS["pos"][0])):
        assert re.search(r"target: \{fileID: %s, guid: \w+,\s*type: 3\}\n      propertyPath: %s\n      value: %s\n" % (FEED_RT, re.escape(path), v), fi), \
            f"the match feed is not on the right ({path})"

    # 12e - feed
    fd = files[FEED]
    check_local_refs(FEED, fd)
    fdd = docs_of(fd)
    assert f"guid: {K.IMAGE}" in fdd[FD["plate"]], "the feed row has no plate"
    for f in ("messageText", "accentImage", "background"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, fdd[FD["item"]], re.M), f"GameToastItemView.{f} is not wired"
    ts = files[TOAST_SETTINGS]
    for k, v in TOAST_FIELDS.items():
        assert f"  {k}: {v}\n" in ts, f"GameToastSettings.{k} is not {v}"
    assert "  agedAlpha: 0\n" not in ts, "aged feed rows vanish (agedAlpha 0)"
    assert "  m_Spacing: 4\n" in docs_of(files[FEED_UI])[FD["content_vlg"]], "the feed rows have no gap"

    # 12e - freestyle
    mh = next(d for d in docs_of(files[MENU]).values() if f"guid: {S['menuhud']}" in d)
    assert f"  exitGlyph: {K.sprite_ref(CLOSE_GLYPH)}\n" in mh, "the freestyle exit hex has no glyph"
    assert f"guid: {LIBERATION}" not in files[FTUE], "the FTUE bar still draws in LiberationSans"

    # Motion (section 22.7): every surface arrives instead of appearing.
    kit_entrance = K.kit_script("KitEntrance")
    def entrances(text_):
        return text_.count(f"guid: {kit_entrance}, type: 3}}")
    for path, at_least, what in ((CANVAS, 3, "Ready button, caption and the waiting panel"),
                                 (PAUSE, 5, "pause scrim, window and the three buttons"),
                                 (CONNECT, 4, "the loading screen's brief, title, index and bar"), (ENDGAME, 3, "the reveal's three lines"),
                                 (sb_path(SB_PANEL), 6, "scoreboard scrim, header, banner, rule, stats and buttons"),
                                 (PANEL["path"], 1, "the score column"),
                                 (sb_path(SB_STAT), 1, "the scoreboard stat tiles")):
        assert entrances(files[path]) >= at_least, f"{path}: {what} have no entrance"
    sq = docs_of(files[ENDGAME])[EG["seq"]]
    blk = re.search(r"^  revealEntrances:\n((?:  - .*\n)*)", sq, re.M)
    assert blk and len(re.findall(r"\{fileID: [1-9-]", blk.group(1))) == 3, "the reveal's entrances are not started by the sequencer"
    assert re.search(r"^  entrance: \{fileID: [1-9-]", docs[next(f for f, d in docs.items() if f"guid: {S['waiting']}" in d)], re.M), \
        "the waiting panel has no entrance"

    pn = files[PANEL["path"]]
    check_local_refs(PANEL["path"], pn)
    pd = docs_of(pn)
    comp = pd[PANEL_COMP]
    for f in ("domainSumText", "accentImage", "borderImage", "domainIndicatorImage", "avatarContainer", "glowImage"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, comp, re.M), f"DomainScorePanel.{f} is not wired"
    assert "  indicatorAlpha: 0.12\n" in comp, "the score column is not Team Dark at 12%"
    assert "  glowBreathDepth: 0\n" in comp and "  glowRestAlpha: 0\n" in comp, "the score glow still breathes at rest"
    assert f"  m_SizeDelta: {K.v2(COL_W, COL_H)}\n" in pd[PANEL["root_rt"]], "the score column is not 168 x 96"
    assert f"  m_SizeDelta: {K.v2(CAP_W, CAP_H)}\n" in pd[PANEL["accent_rt"]], "the cap is not 44 x 3"
    assert "  m_AnchorMin: {x: 0.5, y: 1}\n" in pd[PANEL["accent_rt"]], "the cap is not on the top edge"
    border = re.search(r"  borderImage: \{fileID: (-?\d+)\}", comp).group(1)
    assert f"guid: {K.kit_sprite('kit_sliver14_line1')}" in pd[border], "the column's hairline is not the sliver line"
    for rt in re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", pd[PANEL["root_rt"]], re.M).group(1)):
        assert f"  m_Father: {{fileID: {PANEL['root_rt']}}}" in pd[rt], f"DomainScorePanel: child {rt} names another father"


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

    def trapezoid_plate(f):
        docs = docs_of(f[CANVAS])
        row = next(d for d in docs.values() if f"guid: {S['goalrow']}" in d)
        plate = re.search(r"  plate: \{fileID: (-?\d+)\}", row).group(1)
        f[CANVAS] = f[CANVAS].replace(docs[plate], docs[plate].replace(f"guid: {S['image']}", f"guid: {S['trapezoid']}"), 1)

    def sub(path, old, new):
        return lambda f: f.__setitem__(path, f[path].replace(old, new, 1))
    def point_host(f):
        docs = docs_of(f[CANVAS])
        sbd = next(d for d in docs.values() if f"guid: {S['scoreboard']}" in d)
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", sbd).group(1)
        rt = next(d for d in docs.values() if d.startswith("--- !u!224 ") and f"m_GameObject: {{fileID: {go}}}" in d)
        f[CANVAS] = f[CANVAS].replace(rt, rt.replace("  m_AnchorMin: {x: 0, y: 0}", "  m_AnchorMin: {x: 0.5, y: 0.5}", 1)
                                          .replace("  m_AnchorMax: {x: 1, y: 1}", "  m_AnchorMax: {x: 0.5, y: 0.5}", 1), 1)
    def second_root_stub(f):
        cv = f[CANVAS]
        sbd = next(d for d in docs_of(cv).values() if f"guid: {S['scoreboard']}" in d)
        fid = re.search(r"  scoreboardPanel: \{fileID: (-?\d+)\}", sbd).group(1)
        doc = docs_of(cv)[fid]
        twin = doc.replace(f"&{fid} stripped", "&777000111 stripped", 1)
        f[CANVAS] = cv.replace(doc, twin + doc, 1)
    cases = [
        ("two stand-ins for the scoreboard panel again", second_root_stub),
        ("the scoreboard host a 0x0 point again", point_host),
        ("the cell gate unwired", lambda f: f.__setitem__(CONNECT, re.sub(
            r"(  gate: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CONNECT], count=1))),
        ("a camera back behind the connecting panel", lambda f: f.__setitem__(CONNECT, f[CONNECT]
            + "--- !u!20 &7770001\nCamera:\n  m_ObjectHideFlags: 0\n")),
        ("the connecting panel's own backdrop opaque again", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            docs_of(f[CONNECT])[CN["root_img"]], docs_of(f[CONNECT])[CN["root_img"]].replace(
                "  m_Color: {r: 0, g: 0, b: 0, a: 0}\n", "  m_Color: {r: 0, g: 0.0039215684, b: 0.039215688, a: 1}\n")))),
        ("the tour unwired from the gate", lambda f: f.__setitem__(CONNECT, re.sub(
            r"(  tour: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CONNECT], count=1))),
        ("a chapter without its picture", lambda f: f.__setitem__(CONNECT, re.sub(
            r"(    show: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CONNECT], count=1))),
        ("a chapter's line rewritten off the board", sub(CONNECT, f"    line: {K.yaml_scalar(TOUR[4][1])}\n", "    line: Seven toys.\n")),
        ("two chapters on show at rest", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            docs_of(f[CONNECT])[p_fid("Tour/Show1#cg")], docs_of(f[CONNECT])[p_fid("Tour/Show1#cg")].replace(
                "  m_Alpha: 0\n", "  m_Alpha: 1\n")))),
        ("the Ark's picture missing", sub(CONNECT, f"guid: {ARK_ART}", f"guid: {HULL_CARD['Sparrow']}")),
        ("the bar cut off the measured shares", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            docs_of(f[CONNECT])[p_fid("Gate/Bar#steps")], docs_of(f[CONNECT])[p_fid("Gate/Bar#steps")].replace(
                f"  - {K.f32(STEP_ENDS[0])}\n", "  - 0.22\n", 1)))),
        ("the step bar unwired from the controller", lambda f: f.__setitem__(CONNECT, re.sub(
            r"(  stepBar: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CONNECT], count=1))),
        ("the bar Lime before the load is done", sub(CONNECT, f"  fillToken: {K.TOKEN_INDEX['TextLight']}\n", f"  fillToken: {K.TOKEN_INDEX['Cta']}\n")),
        ("the sea's fields left out of the gate's fade", lambda f: f.__setitem__(CONNECT, re.sub(
            r"(  backdrop: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CONNECT], count=1))),
        ("the hull no longer bobs", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            f"  seconds: {K.f32(HULL_BOB['seconds'])}\n", "  seconds: 9\n", 1))),
        ("the Slider back on the track", lambda f: f.__setitem__(CONNECT, f[CONNECT]
            + "--- !u!114 &7770002\nMonoBehaviour:\n  m_FillRect: {fileID: 0}\n")),
        ("the pilot rows drawn again", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            docs_of(f[CONNECT])[p_fid(CN["icons_go"] + "#hide")], docs_of(f[CONNECT])[p_fid(CN["icons_go"] + "#hide")].replace(
                "  m_Alpha: 0\n", "  m_Alpha: 1\n")))),
        ("the Omni crystal back on the loading screen", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            f"guid: {PORTRAIT['Anvil']}", f"guid: {K.meta_guid('Assets/_Graphics/Codex/ethirion_omni.png')}", 1))),
        ("the connecting panel waiting out 4 seconds again", sub(CONNECT, "  dwellSeconds: 1\n", "  dwellSeconds: 4\n")),
        ("the lava lamp's element colour back on", sub(LOCKUP, "  elementColour: 0\n", "  elementColour: 1\n")),
        ("the lockup plate back to the old near-black", sub(LOCKUP, LOCKUP_FIELDS["plateColor"], "{r: 0.024, g: 0.031, b: 0.063, a: 0.86}")),
        ("the LOCKED card pitch moved", sub(LOCKUP, "  cardPitch: 128\n", "  cardPitch: 140\n")),
        ("RT named wrong", sub(GLYPHS, "    padLabel: RT\n", "    padLabel: R2\n")),
        ("a goal plate left a trapezoid", trapezoid_plate),
        ("the pause hex without its glyph", sub(CANVAS, f"  centreGlyph: {K.sprite_ref(PAUSE_GLYPH)}", "  centreGlyph: {fileID: 0}")),
        ("the score column a full team fill", sub(PANEL["path"], "  indicatorAlpha: 0.12\n", "  indicatorAlpha: 1\n")),
        ("the Ready button left a plain Button", sub(CANVAS, f"guid: {S['kitbutton']}", f"guid: {S['button']}")),
        ("the waiting panel deaf to PlayerReady", sub(CANVAS, f"  toastChannel: {K.ref(11400000, TOAST_CHANNEL, 2)}", "  toastChannel: {fileID: 0}")),
        ("the count without its hex", lambda f: f.__setitem__(CANVAS, re.sub(
            r"(      propertyPath: beatHex\n      value: \n      objectReference: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CANVAS]))),
        ("the countdown still drawn in sprites", lambda f: f.__setitem__(CANVAS, re.sub(
            r"(      propertyPath: countdownText\n      value: \n      objectReference: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CANVAS]))),
        ("a pause button on A", sub(PAUSE, "  activationButton: 4\n", "  activationButton: 6\n")),
        ("the pause button still calls Show", lambda f: f.__setitem__(CANVAS, f[CANVAS].replace(
            "m_MethodName: OnClickPauseGameButton", "m_MethodName: Show\n        m_Mode: 1\n      - m_Target: {fileID: 0}\n        m_MethodName: OnClickPauseGameButton", 1))),
        ("guests lose Resume again", sub(PAUSE, "  replayButton: {fileID: 0}\n", f"  replayButton: {{fileID: {PAUSE_ROOT_GO}}}\n")),
        ("the connecting rank text back in whitrabt", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            f"guid: {K.ALDRICH[0]}", f"guid: {WHITRABT}", 1))),
        ("the reveal line overflowing again", lambda f: f.__setitem__(ENDGAME, f[ENDGAME].replace(
            docs_of(f[ENDGAME])[EG["msg_tmp"]], docs_of(f[ENDGAME])[EG["msg_tmp"]].replace("  m_enableAutoSizing: 1\n", "  m_enableAutoSizing: 0\n")))),
        ("Main menu back on A", lambda f: f.__setitem__(sb_path(SB_PANEL), f[sb_path(SB_PANEL)].replace(
            docs_of(f[sb_path(SB_PANEL)])[str(K.FileIds(SB_PANEL)("Sb/MainMenu#cbp"))],
            docs_of(f[sb_path(SB_PANEL)])[str(K.FileIds(SB_PANEL)("Sb/MainMenu#cbp"))].replace("  activationButton: 5\n", "  activationButton: 6\n")))),
        ("the scoreboard row a full team fill", lambda f: f.__setitem__(sb_path(SB_ROW), f[sb_path(SB_ROW)].replace(
            "  backgroundImage: {fileID: 0}\n", f"  backgroundImage: {{fileID: {K.FileIds(SB_ROW)('Glass#img')}}}\n", 1))),
        ("the versus view unwired", lambda f: f.__setitem__(CANVAS, re.sub(r"(  versusView: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CANVAS]))),
        ("the versus layout not stepping the ladder aside", lambda f: f.__setitem__(sb_path(SB_PANEL), re.sub(
            r"(  ladderGroup: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[sb_path(SB_PANEL)]))),
        ("a versus side with nowhere for its rows", lambda f: f.__setitem__(sb_path(SB_PANEL), re.sub(
            r"(    rows: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[sb_path(SB_PANEL)], count=1))),
        ("the reward strip unwired", lambda f: f.__setitem__(CANVAS, re.sub(r"(  rewardStrip: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CANVAS]))),
        ("the local scoreboard row lit with the selection rim", lambda f: f.__setitem__(sb_path(SB_ROW), f[sb_path(SB_ROW)].replace(
            f"  localBorderMaterial: {NO_ROW_RIM}\n", f"  localBorderMaterial: {mat_ref(HM.M._lattice_guid('selection_rim_mat'))}\n"))),
        ("the match feed back bottom-left", sub(CANVAS, "      propertyPath: m_AnchoredPosition.x\n      value: -40\n", "      propertyPath: m_AnchoredPosition.x\n      value: 316.80005\n")),
        ("feed rows vanishing again", sub(TOAST_SETTINGS, "  agedAlpha: 0.5\n", "  agedAlpha: 0\n")),
        ("the feed row without its accent bar", lambda f: f.__setitem__(FEED, re.sub(
            r"(  accentImage: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[FEED]))),
        ("the match without a toast container", sub(CANVAS, f"  m_Name: {TOAST_CONTAINER}\n", "  m_Name: Toasts\n")),
        ("the pause hex without its keycap", sub(CANVAS, "  padKeycap: Start\n", "  padKeycap: \n")),
        ("the reveal's lines no longer started", lambda f: f.__setitem__(ENDGAME, re.sub(
            r"^  revealEntrances:\n(?:  - .*\n)*", "  revealEntrances: []\n", f[ENDGAME], flags=re.M))),
        ("the scoreboard snapping in", lambda f: f.__setitem__(sb_path(SB_PANEL), f[sb_path(SB_PANEL)].replace(
            f"guid: {K.kit_script('KitEntrance')}, type: 3}}", f"guid: {K.kit_script('KitAmbient')}, type: 3}}"))),
        ("the column hairline unwired", lambda f: f.__setitem__(PANEL["path"], re.sub(r"(  borderImage: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[PANEL["path"]]))),
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
    print("OK: the in-game UI validates")
    if args.self_test:
        return 0 if self_test(files) else 1
    stale = drift(files)
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
