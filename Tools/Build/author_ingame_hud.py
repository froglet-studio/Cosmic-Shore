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
    return text_


def glyphs_text(text_):
    def entry(m):
        binding = int(m.group(1))
        body = re.sub(r"^    padLabel: .*\n", "", m.group(2), flags=re.M)
        label = PAD_LABELS.get(binding, "")
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
BEAT_SIZE = 260
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
    dia = A.image_node(n, "Wait/Diamond", "Diamond", HM.TL(26, 37, 12, 12), "Cta", K.chrome_sprite("chrome_diamond"), preserve=True)
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
        b = H.text("3", "Display", tok("TextLight"), align="center")(ctx, go)
        return b.replace(f"  m_fontSize: {K.f32(48)}\n  m_fontSizeBase: {K.f32(48)}\n",
                         f"  m_fontSize: {BEAT_SIZE}\n  m_fontSizeBase: {BEAT_SIZE}\n")
    n.add("cr", 222, K.canvas_renderer()).add("tmp", 114, body)
    return n


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
    beat_rt = p.emit(beat_node(), disp_rt, disp)
    p.children(disp_rt, [k for k in p.kids(disp_rt) if k != beat_rt] + [beat_rt])
    p.rect(disp_rt, R((0.5, 0.5), (0.5, 0.5), (0, 0), (600, 400)))
    set_instance_ref(p, pi, COUNTDOWN_COMP, "countdownText", p.ctx.c("Beat", "tmp"))
    set_instance_ref(p, pi, COUNTDOWN_COMP, "waitingPanel", p.ctx.c("Wait", "panel"))


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
# 12c - the connecting panel (ConnectingPanel.prefab, nested in GameCanvas)
# --------------------------------------------------------------------------------------------

CONNECT = "Assets/_Prefabs/UI Elements/In Game/ConnectingPanel.prefab"
WHITRABT = K.meta_guid("Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials/whitrabt SDF.asset")
CN = dict(comp="6370090134550638757", root_go="470661231859203382", root_rt="5402023128368193864",
          root_img="2388631188305556708", safe_rt="268860854097267997", status_rt="7647039353653187114",
          status_tmp="5105126948944046106", mode_rt="7234322143272291273", mode_tmp="6280339008487199906",
          rank_tmp="1901962995261546283", preview_rt="4036379287790713793", slider_rt="4629373923633661124",
          track_go="5177954019794184100", track_img="8125581187658145292", fill_go="8684485316077403885",
          fill_img="6946419630781211910")
S.update(connect=script_guid("Assets/_Scripts/UI/ConnectingPanelController.cs"))
COL_X = 1000           # the readout column: 1000 wide, centred, as the board draws it


def tmp_restyle(doc, face, size, upper, spacing, align, token):
    font = K.SEMIBOLD if face == "Chakra" else K.ALDRICH
    doc = C.field_set(doc, "m_fontAsset", K.ref(11400000, font[0], 2))
    doc = C.field_set(doc, "m_sharedMaterial", K.ref(font[1], font[0], 2))
    for f, v in (("m_fontSize", size), ("m_fontSizeBase", size), ("m_fontStyle", 16 if upper else 0),
                 ("m_characterSpacing", spacing), ("m_HorizontalAlignment", {"left": 1, "center": 2, "right": 4}[align]),
                 ("m_enableAutoSizing", 0)):
        doc = C.field_set(doc, f, str(v))
    c = tok(token)
    doc = C.field_set(doc, "m_Color", K.col(c))
    doc = C.field_set(doc, "m_fontColor", K.col(c))
    doc = re.sub(r"(  m_fontColor32:\n    serializedVersion: 2\n    rgba: )\d+", lambda m: m.group(1) + str(K.col32(c)), doc, count=1)
    return doc


def connect_text(text_):
    p = Patch(text_, "ConnectingPanel/Item12")
    assert p.sc.script(CN["comp"]) == S["connect"], "ConnectingPanelController moved"
    # Status: a quiet top-left line beside a breathing diamond (the board's "Connecting to Shore").
    p.rect(CN["status_rt"], R((0, 1), (0, 1), (64, -36), (600, 20), (0, 1)))
    p.replace(CN["status_tmp"], tmp_restyle(p.doc(CN["status_tmp"]), "Chakra", 14, True, 14, "left", "TextLight"))
    dia = N("Connect/Diamond", "Diamond", R((0, 1), (0, 1), (40, -40), (12, 12), (0, 1)))
    dia.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("ControlActive"), K.chrome_sprite("chrome_diamond"), preserve=True)) \
        .add("th", 114, H.themed("ControlActive")).add("breathe", 114, HM.M.ambient("Breathe", period=1.6, amplitude=0.6))
    # The readout column, bottom centre: mode, phase headline, bar, counts and seconds.
    p.rect(CN["mode_rt"], R((0.5, 0.5), (0.5, 0.5), (0, -190), (COL_X, 22)))
    p.replace(CN["mode_tmp"], tmp_restyle(p.doc(CN["mode_tmp"]), "Chakra", 15, True, 14, "left", "NeutralLightest"))
    title = N("Connect/Phase", "PhaseTitle", R((0.5, 0.5), (0.5, 0.5), (0, -232), (COL_X, 58)))
    H.label(title, "Connecting", "Display", "TextLight")
    title.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", distance=10))
    p.rect(CN["slider_rt"], R((0.5, 0.5), (0.5, 0.5), (0, -282), (COL_X, 6)))
    p.body(CN["track_img"], HM.img_body(tok("TextLight", 0.16)), CN["track_go"])
    p.body(CN["fill_img"], HM.img_body(tok("ControlActive")), CN["fill_go"])
    prog = N("Connect/Progress", "ProgressText", R((0.5, 0.5), (0.5, 0.5), (0, -308), (COL_X, 26)))
    H.label(prog, "", "Data", "TextLight")
    secs = N("Connect/Elapsed", "ElapsedText", R((0.5, 0.5), (0.5, 0.5), (0, -308), (COL_X, 26)))
    H.label(secs, "", "Data", "NeutralLightest", align="right")
    p.rect(CN["preview_rt"], R((0.5, 0.5), (0.5, 0.5), (0, 110), (960, 540)))
    rank = p.doc(CN["rank_tmp"])
    rank = C.field_set(rank, "m_fontAsset", K.ref(11400000, K.ALDRICH[0], 2))
    p.replace(CN["rank_tmp"], C.field_set(rank, "m_sharedMaterial", K.ref(K.ALDRICH[1], K.ALDRICH[0], 2)))
    added = []
    for n in (dia, title, prog, secs):
        added.append(p.emit(n, CN["safe_rt"], CN["comp"]))
    p.children(CN["safe_rt"], [k for k in p.kids(CN["safe_rt"]) if k not in added] + added)
    comp = p.doc(CN["comp"])
    comp = set_field(comp, "statusBaseText", "Connecting to Shore")
    comp = insert_after_field(comp, "gameModeText", "phaseTitle", f"{{fileID: {p.ctx.c('Connect/Phase', 'tmp')}}}")
    comp = insert_after_field(comp, "phaseTitle", "progressText", f"{{fileID: {p.ctx.c('Connect/Progress', 'tmp')}}}")
    comp = insert_after_field(comp, "progressText", "elapsedText", f"{{fileID: {p.ctx.c('Connect/Elapsed', 'tmp')}}}")
    comp = insert_after_field(comp, "elapsedText", "phaseTitleEntrance", f"{{fileID: {p.ctx.c('Connect/Phase', 'in')}}}")
    comp = insert_after_field(comp, "gameModeText", "backdropArt", f"{{fileID: {p.ctx.c('Connect/Backdrop/Drift/Art', 'img')}}}")
    comp = insert_after_field(comp, "backdropArt", "games", K.ref(11400000, ARCADE_GAMES, 2))
    p.replace(CN["comp"], comp)
    # The backdrop (section 28): the mode's card art drifting under a dim, over a solid base, in
    # place of the flat purple-blue sprite. It bleeds; the readout stays in the safe area.
    p.body(CN["root_img"], HM.img_body(tok("SurfaceBlack"), raycast=True), CN["root_go"])
    backs = backdrop_nodes()
    added = []
    after = CN["comp"]
    for n in backs:
        added.append(p.emit(n, CN["root_rt"], after))
        after = HM._last_doc(p, n)
    p.children(CN["root_rt"], added + [k for k in p.kids(CN["root_rt"]) if k not in added])
    return p.text()


ARCADE_GAMES = "402ca7eefc2aeb64baaa140c43312bcd"   # ArcadeGames.asset (SO_GameList)


def backdrop_nodes():
    clip = N("Connect/Backdrop", "Backdrop", STRETCH)
    clip.add("rmask", 114, A.rect_mask())
    drift = clip.child(N("Connect/Backdrop/Drift", "Drift", R((0, 0), (1, 1), (0, 0), (160, 90))))
    drift.add("cover", 114, lambda ctx, go: K.mono(go, K.kit_script("KitCoverImage"),
              f"  image: {{fileID: {ctx.c('Connect/Backdrop/Drift/Art', 'img')}}}\n")) \
        .add("pan", 114, HM.M.ambient("Pan", period=28, amplitude=0.06))
    art = drift.child(N("Connect/Backdrop/Drift/Art", "Art", STRETCH))
    art.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE))
    dim = N("Connect/Dim", "Dim", STRETCH)
    dim.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.55))).add("th", 114, H.themed("SurfaceBlack", 0.55))
    fade = N("Connect/BottomFade", "BottomFade", R((0, 0), (1, 0), (0, 240), (0, 480)))
    fade.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.92), K.kit_sprite("kit_fade_v"))) \
        .add("th", 114, H.themed("SurfaceBlack", 0.92))
    return [clip, dim, fade]


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
LIST_W, ROW_HT, LIST_H = 1260, 84, 396
SB_BUTTONS = [  # key, caption, variant, keycap, pad (South 6 = A, East 5 = B), width
    ("PlayAgain", "Play again", "Play", "A", 6, 340),
    ("Continue", "Continue", "Play", "A", 6, 300),
    ("MainMenu", "Main menu", "Cancel", "B", 5, 300),
    ("LeaveLobby", "Leave lobby", "Cancel", "B", 5, 300),
]


def sb_path(name):
    return f"{SB_DIR}/{name}.prefab"


def build_row():
    root = N("root", SB_ROW, R(size=(LIST_W, ROW_HT)))
    root.add("cg", 225, H.canvas_group(True)).add("le", 114, H.le(ph=ROW_HT, pw=LIST_W))
    base = root.child(N("Base", "Base", STRETCH))
    base.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceVeryDark"), K.kit_sprite("kit_sliver14_fill"), True)) \
        .add("th", 114, H.themed("SurfaceVeryDark"))
    tint = root.child(N("Tint", "Tint", STRETCH))
    tint.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_sliver14_fill"), True))
    bord = root.child(N("Border", "Border", STRETCH))
    bord.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_sliver14_line1"), True))
    rank = root.child(N("Rank", "Rank", HM.TL(28, 21, 44, 42)))
    rank.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("1", "H2", tok("TextLight")))
    ring = root.child(N("Ring", "Ring", HM.TL(84, 16, 52, 52)))
    ring.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceDark"), K.kit_sprite("kit_oct_fill"), True))
    clip = ring.child(N("Ring/Clip", "Clip", R((0, 0), (1, 1), (0, 0), (-6, -6))))
    clip.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, K.kit_sprite("kit_oct_fill"), True)).add("mask", 114, A.mask(False))
    av = clip.child(N("Ring/Clip/Avatar", "Avatar", STRETCH))
    av.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(K.WHITE, A.AVATAR_PLACEHOLDER))
    name = root.child(N("Name", "Name", HM.TL(158, 12, 560, 34)))
    name.add("cr", 222, K.canvas_renderer()).add("tmp", 114, A.ellipsis(H.text("Pilot", "H2", tok("TextLight"))))
    dom = root.child(N("Domain", "Domain", HM.TL(158, 50, 300, 18)))
    dom.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "Label", tok("NeutralLightest")))
    score = root.child(N("Score", "Score", HM.TR(170, 21, 360, 42)))
    score.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("0", "H2", tok("TextLight"), align="right"))
    data = root.child(N("Data", "DataPanels", HM.TR(24, 12, 130, 60)))
    data.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 2, 5, (1, 1), (0, 0)))
    rew = data.child(N("Data/Reward", "CrystalReward", R(size=(130, 30))))
    rew.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 6, 5, (0, 0), (0, 0))).add("le", 114, H.le(ph=30))
    glyph = rew.child(N("Data/Reward/Glyph", "Glyph", R(size=(18, 18))))
    glyph.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("Purchase"), K.chrome_sprite("chrome_crystal"), preserve=True)) \
        .add("th", 114, H.themed("Purchase")).add("le", 114, H.le(pw=18, ph=18))
    amt = rew.child(N("Data/Reward/Amount", "Amount", R(size=(90, 26))))
    amt.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("+200", "Data", tok("Purchase"), align="right")).add("le", 114, H.le(pw=90, ph=26))
    sec = data.child(N("Data/Secondary", "Secondary", R(size=(130, 22))))
    sec.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("", "DataSmall", tok("NeutralLightest"), align="right")).add("le", 114, H.le(ph=22))
    root.add("card", 114, lambda ctx, go: K.mono(go, S["card"],
        f"  playerAvatarImage: {{fileID: {ctx.c('Ring/Clip/Avatar', 'img')}}}\n"
        f"  playerNameText: {{fileID: {ctx.c('Name', 'tmp')}}}\n  playerScoreText: {{fileID: {ctx.c('Score', 'tmp')}}}\n"
        f"  backgroundImage: {{fileID: {ctx.c('Tint', 'img')}}}\n  domainIndicatorImage: {{fileID: {ctx.c('Ring', 'img')}}}\n"
        "  backgroundTintAlpha: 0.12\n"
        f"  dataPanelsRoot: {{fileID: {ctx.go('Data')}}}\n  secondaryStatText: {{fileID: {ctx.c('Data/Secondary', 'tmp')}}}\n"
        f"  crystalRewardRoot: {{fileID: {ctx.go('Data/Reward')}}}\n  crystalRewardText: {{fileID: {ctx.c('Data/Reward/Amount', 'tmp')}}}\n"
        f"  rankText: {{fileID: {ctx.c('Rank', 'tmp')}}}\n  domainText: {{fileID: {ctx.c('Domain', 'tmp')}}}\n"
        f"  borderImage: {{fileID: {ctx.c('Border', 'img')}}}\n"
        f"  localBorderToken: {K.TOKEN_INDEX['TextLight']}\n  otherBorderToken: {K.TOKEN_INDEX['SurfaceDark']}\n"
        f"  leaderRankToken: {K.TOKEN_INDEX['TextLight']}\n  rankToken: {K.TOKEN_INDEX['NeutralLightest']}\n"
        f"  animSettings: {K.ref(11400000, ANIM_SETTINGS, 2)}\n"))
    return root


def build_stat():
    root = N("root", SB_STAT, R(size=(300, 96)))
    root.add("le", 114, H.le(fw=1, ph=96))
    # Rows are spawned into a layout group; each rises from where it lands (KitEntrance.ReconcileRest).
    root.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", by_sibling=True, distance=8))
    H.framed(root, "Stat", 10, "SurfaceVeryDark", "SurfaceDark")
    H.label(root.child(N("Label", "Label", HM.TL(20, 16, 220, 18))), "Longest drift", "Label", "NeutralLightest")
    val = root.child(N("Value", "Value", HM.TL(20, 42, 240, 36)))
    val.add("cr", 222, K.canvas_renderer()).add("tmp", 114, H.text("0", "H2", tok("TextLight")))
    icon = root.child(N("Icon", "Icon", HM.TR(18, 16, 24, 24)))
    icon.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("TextLight"), preserve=True))
    root.add("stat", 114, lambda ctx, go: K.mono(go, S["statrow"],
        f"  iconImage: {{fileID: {ctx.c('Icon', 'img')}}}\n  labelText: {{fileID: {ctx.c('Label', 'tmp')}}}\n"
        f"  valueText: {{fileID: {ctx.c('Value', 'tmp')}}}\n"))
    return root


def build_panel():
    root = N("root", SB_PANEL, STRETCH)
    scrim = root.child(N("Scrim", "Scrim", STRETCH))
    scrim.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image(tok("SurfaceBlack", 0.86), raycast=True)).add("th", 114, H.themed("SurfaceBlack", 0.86))
    scrim.add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Fade", duration=0.2))
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
    view = root.child(N("List", "List", R((0.5, 1), (0.5, 1), (0, -230), (LIST_W, LIST_H), (0.5, 1))))
    view.add("cr", 222, K.canvas_renderer()).add("img", 114, K.image((1.0, 1.0, 1.0, 0.0), raycast=True)) \
        .add("rmask", 114, A.rect_mask()).add("scroll", 114, A.scroll_rect("List/Content", "List", 30))
    content = view.child(N("List/Content", "Content", R((0, 1), (1, 1), (0, 0), (0, 0), (0.5, 1))))
    content.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 12, 1, (1, 1), (0, 0))).add("csf", 114, K.fitter(0, 2))
    stats = root.child(N("Stats", "Stats", R((0.5, 1), (0.5, 1), (0, -650), (LIST_W, 96), (0.5, 1))))
    stats.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 14, 3, (1, 1), (1, 0))) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", delay=0.5))
    row = root.child(N("Buttons", "Buttons", R((0.5, 1), (0.5, 1), (0, -800), (LIST_W, 64), (0.5, 1))))
    row.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 20, 4, (1, 1), (0, 0))) \
        .add("cg", 225, H.canvas_group(True)).add("in", 114, HM.M.entrance("Rise", delay=0.6))
    for key, cap, variant, keycap, pad, w in SB_BUTTONS:
        b = kit_button(f"Sb/{key}", f"{key}Button", R(size=(w, 64)), variant, variant == "Play", cap, keycap, pad,
                       keycap_key=f"Sb{key}Keycap")
        b.add("le", 114, H.le(pw=w, ph=64))
        if key == "PlayAgain":
            strip = b.child(N("Sb/PlayAgain/Avatars", "PlayerAvatars", R((0.5, 0), (0.5, 0), (0, -16), (w, 40), (0.5, 1))))
            strip.add("hlg", 114, K.hv_layout(False, (0, 0, 0, 0), 10, 4, (0, 0), (0, 0))).add("le", 114, H.le(ignore=True))
        row.children.append(b)
    return root


def sb_prefabs():
    out = {}
    for name, build in ((SB_ROW, build_row), (SB_STAT, build_stat), (SB_PANEL, build_panel)):
        out[sb_path(name)] = P.prefab_text(build(), name)
        out[sb_path(name) + ".meta"] = K.PREFAB_META.format(g=P.asset_guid(name))
    return out


def sb_ref(name, key):
    """A reference INTO one of the new prefabs, as an asset reference (prefab fields)."""
    return K.ref(K.FileIds(name)(key), P.asset_guid(name), 3)


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

    def stub(key, kind):
        src = fk(key)
        fid = str(C.derived(src, inst))
        doc = C.stub_doc(fid, kind, src, guid, inst)
        if p.sc.has(fid):
            p.sc.remove(fid)
        return fid, doc
    out = []
    K.emit_instance(p.ctx, K.Instance("Score", SB_PANEL, guid, "ScoreboardPanel", STRETCH, ()), host_rt, out)
    refs = {}
    for field, key, kind in (("endGameObject", "root#go", "go"), ("scoreboardPanel", "root#rt", "rt"),
                             ("statsContainer", "Stats#rt", "rt"), ("BannerImage", "Header/Rule#img", "img"),
                             ("BannerText", "Header/Banner#tmp", "tmp"), ("playerCardContainer", "List/Content#rt", "rt"),
                             ("playAgainButton", "Sb/PlayAgain#go", "go"), ("playAgainLabel", "Sb/PlayAgain/Label#tmp", "tmp"),
                             ("mainMenuButton", "Sb/MainMenu#go", "go"), ("leaveLobbyButton", "Sb/LeaveLobby#go", "go"),
                             ("continueButton", "Sb/Continue#go", "go"), ("modeText", "Header/Mode#tmp", "tmp")):
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
    p.children(host_rt, [k for k in p.kids(host_rt) if k != root_stub] + [root_stub])
    d = p.doc(sb)
    for field, fid in refs.items():
        d = set_field(d, field, f"{{fileID: {fid}}}")
    d = set_field(d, "statRowPrefab", sb_ref(SB_STAT, "root#stat"))
    d = set_field(d, "playerCardPrefab", sb_ref(SB_ROW, "root#card"))
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
TOAST_FIELDS = {"slideInOffset": "-120", "ageAfterSeconds": "6", "agedAlpha": "0.5"}


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
TOAST_H = 132


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
    n = N("Toasts", TOAST_CONTAINER, R((0, 0), (1, 0), (0, 24), (0, TOAST_H), (0.5, 0)))
    n.add("vlg", 114, K.hv_layout(True, (0, 0, 0, 0), 10, 7, (1, 1), (0, 0)))
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


def validate(files):
    lk = files[LOCKUP]
    for k, v in LOCKUP_FIELDS.items():
        assert f"  {k}: {v}\n" in lk, f"AbilityLockupStyle: {k} is not the style-B value"
    for k, v in KEYCAP_FIELDS:
        assert f"  {k}: {v}\n" in lk, f"AbilityLockupStyle: {k} is not set"
    # The LOCKED geometry is untouched (VESSEL_HUD_AND_ELEMENTS: changeable only within the asserted limits).
    for k, v in (("plateWidth", "104"), ("cardPitch", "128"), ("abilityCellHeight", "88"), ("petalCellHeight", "88"),
                 ("iconBoxSize", "60"), ("petalFlowerSize", "60"), ("rowMarginRight", "40"), ("rowMarginBottom", "44")):
        assert f"  {k}: {v}\n" in lk, f"AbilityLockupStyle: the LOCKED geometry moved ({k})"
    gl = files[GLYPHS]
    for b, label in PAD_LABELS.items():
        if re.search(rf"^  - binding: {b}\n", gl, re.M):
            assert re.search(rf"^  - binding: {b}\n(?:    .*\n)*?    padLabel: {label}\n", gl, re.M), f"ControlGlyphSet: binding {b} is not named {label}"

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
    for prop in ("countdownText", "waitingPanel", "countdownDisplay"):
        m = re.search(r"      propertyPath: %s\n      value: .*\n      objectReference: \{fileID: (-?\d+)\}" % prop, pi)
        assert m and m.group(1) in docs, f"CountdownTimer.{prop} is not wired"
        assert pi.count(f"propertyPath: {prop}\n") == 1, f"CountdownTimer.{prop} is overridden twice"
    beat = re.search(r"      propertyPath: countdownText\n      value: .*\n      objectReference: \{fileID: (-?\d+)\}", pi).group(1)
    assert f"  m_fontSize: {BEAT_SIZE}\n" in docs[beat], "the countdown beat is not 260 px"

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
    for f in ("phaseTitle", "progressText", "elapsedText", "statusText", "gameModeText", "progressSlider", "backdropArt"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, cd[CN["comp"]], re.M), f"ConnectingPanelController.{f} is not wired"
    assert f"  games: {K.ref(11400000, ARCADE_GAMES, 2)}" in cd[CN["comp"]], "the connecting backdrop has no game list to find a mode's art in"
    for key in ("status_tmp", "mode_tmp"):
        assert f"guid: {K.SEMIBOLD[0]}" in cd[CN[key]], f"the connecting panel's {key} is not Chakra Petch SemiBold"
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
    sp = files[sb_path(SB_PANEL)]
    check_local_refs(sb_path(SB_PANEL), sp)
    spd = docs_of(sp)
    fk = K.FileIds(SB_PANEL)
    for key, _, _, _, pad, _ in SB_BUTTONS:
        cbp = spd[str(fk(f"Sb/{key}#cbp"))]
        assert f"  activationButton: {pad}\n" in cbp, f"{key} answers the wrong pad button"
    assert {b[4] for b in SB_BUTTONS if b[0] in ("PlayAgain", "Continue")} == {6} and \
           {b[4] for b in SB_BUTTONS if b[0] in ("MainMenu", "LeaveLobby")} == {5}, "A and B share an end-of-match button"
    row = docs_of(files[sb_path(SB_ROW)])[str(K.FileIds(SB_ROW)("root#card"))]
    for f in ("playerAvatarImage", "playerNameText", "playerScoreText", "backgroundImage", "rankText", "domainText", "borderImage",
              "crystalRewardRoot", "crystalRewardText", "dataPanelsRoot"):
        assert re.search(r"^  %s: \{fileID: [1-9-]" % f, row, re.M), f"ScoreboardRow: {f} is not wired"
    assert "  backgroundTintAlpha: 0.12\n" in row, "the scoreboard row is not Team at 12% (a full team fill)"
    for name in (SB_ROW, SB_STAT):
        check_local_refs(sb_path(name), files[sb_path(name)])

    assert "  m_Name: ThumbCursors\n" not in cv, "the dead ThumbCursors subtree survives"
    # Follow-up - toast container
    names = re.findall(r"^  m_Name: %s$" % TOAST_CONTAINER, cv, re.M)
    assert len(names) == 1, "GameCanvas needs exactly one ToastNotificationContainer (the toast API finds it by name)"
    root_rt = next(f for f, d in docs.items() if d.startswith("--- !u!224 ") and "  m_Father: {fileID: 0}\n" in d)
    kids = re.findall(r"\{fileID: (-?\d+)\}", re.search(r"^  m_Children:\n((?:  - .*\n)*)", docs[root_rt], re.M).group(1))
    last_go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", docs[kids[-1]]).group(1)
    assert f"  m_Name: {TOAST_CONTAINER}\n" in docs[last_go], "the toast container is not drawn last (the scoreboard would cover it)"

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
                                 (CONNECT, 1, "the connecting headline"), (ENDGAME, 3, "the reveal's three lines"),
                                 (sb_path(SB_PANEL), 6, "scoreboard scrim, header, banner, rule, stats and buttons"),
                                 (PANEL["path"], 1, "the score column"),
                                 (sb_path(SB_STAT), 1, "the scoreboard stat tiles")):
        assert entrances(files[path]) >= at_least, f"{path}: {what} have no entrance"
    sq = docs_of(files[ENDGAME])[EG["seq"]]
    blk = re.search(r"^  revealEntrances:\n((?:  - .*\n)*)", sq, re.M)
    assert blk and len(re.findall(r"\{fileID: [1-9-]", blk.group(1))) == 3, "the reveal's entrances are not started by the sequencer"
    assert re.search(r"^  phaseTitleEntrance: \{fileID: [1-9-]", docs_of(files[CONNECT])[CN["comp"]], re.M), "the connecting headline does not replay on a phase change"
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
    cases = [
        ("the connecting backdrop unwired", lambda f: f.__setitem__(CONNECT, re.sub(
            r"(  backdropArt: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CONNECT], count=1))),
        ("the lockup plate back to the old near-black", sub(LOCKUP, LOCKUP_FIELDS["plateColor"], "{r: 0.024, g: 0.031, b: 0.063, a: 0.86}")),
        ("the LOCKED card pitch moved", sub(LOCKUP, "  cardPitch: 128\n", "  cardPitch: 140\n")),
        ("RT named wrong", sub(GLYPHS, "    padLabel: RT\n", "    padLabel: R2\n")),
        ("a goal plate left a trapezoid", trapezoid_plate),
        ("the pause hex without its glyph", sub(CANVAS, f"  centreGlyph: {K.sprite_ref(PAUSE_GLYPH)}", "  centreGlyph: {fileID: 0}")),
        ("the score column a full team fill", sub(PANEL["path"], "  indicatorAlpha: 0.12\n", "  indicatorAlpha: 1\n")),
        ("the Ready button left a plain Button", sub(CANVAS, f"guid: {S['kitbutton']}", f"guid: {S['button']}")),
        ("the waiting panel deaf to PlayerReady", sub(CANVAS, f"  toastChannel: {K.ref(11400000, TOAST_CHANNEL, 2)}", "  toastChannel: {fileID: 0}")),
        ("the countdown still drawn in sprites", lambda f: f.__setitem__(CANVAS, re.sub(
            r"(      propertyPath: countdownText\n      value: \n      objectReference: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CANVAS]))),
        ("a pause button on A", sub(PAUSE, "  activationButton: 4\n", "  activationButton: 6\n")),
        ("the pause button still calls Show", lambda f: f.__setitem__(CANVAS, f[CANVAS].replace(
            "m_MethodName: OnClickPauseGameButton", "m_MethodName: Show\n        m_Mode: 1\n      - m_Target: {fileID: 0}\n        m_MethodName: OnClickPauseGameButton", 1))),
        ("guests lose Resume again", sub(PAUSE, "  replayButton: {fileID: 0}\n", f"  replayButton: {{fileID: {PAUSE_ROOT_GO}}}\n")),
        ("the connecting rank text back in whitrabt", lambda f: f.__setitem__(CONNECT, f[CONNECT].replace(
            f"guid: {K.ALDRICH[0]}", f"guid: {WHITRABT}", 1))),
        ("the connecting headline unwired", lambda f: f.__setitem__(CONNECT, re.sub(
            r"(  phaseTitle: )\{fileID: -?\d+\}", r"\1{fileID: 0}", f[CONNECT]))),
        ("the reveal line overflowing again", lambda f: f.__setitem__(ENDGAME, f[ENDGAME].replace(
            docs_of(f[ENDGAME])[EG["msg_tmp"]], docs_of(f[ENDGAME])[EG["msg_tmp"]].replace("  m_enableAutoSizing: 1\n", "  m_enableAutoSizing: 0\n")))),
        ("Main menu back on A", lambda f: f.__setitem__(sb_path(SB_PANEL), f[sb_path(SB_PANEL)].replace(
            docs_of(f[sb_path(SB_PANEL)])[str(K.FileIds(SB_PANEL)("Sb/MainMenu#cbp"))],
            docs_of(f[sb_path(SB_PANEL)])[str(K.FileIds(SB_PANEL)("Sb/MainMenu#cbp"))].replace("  activationButton: 5\n", "  activationButton: 6\n")))),
        ("the scoreboard row a full team fill", sub(sb_path(SB_ROW), "  backgroundTintAlpha: 0.12\n", "  backgroundTintAlpha: 1\n")),
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
