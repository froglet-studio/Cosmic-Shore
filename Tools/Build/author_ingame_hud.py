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


def pause_button(p):
    vui = p.by_script(S["vui"])
    assert len(vui) == 1, "GameCanvas should carry exactly one VolumeUI (the pause button)"
    return p.sc.go_of(vui[0])


def patch_pause(p):
    go = pause_button(p)
    assert p.sc.field(go, "m_Name") == "Volume / Pause Button", "the pause button was renamed"
    fid = p.add_comp(go, "volume", 114, lambda ctx, g: K.mono(g, S["volume"],
        "  hexGraphic: {fileID: 0}\n"
        f"  centreGlyph: {K.sprite_ref(PAUSE_GLYPH)}\n"
        "  centreGlyphSize: 44\n"
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
        .add("kt", 114, K.kit_text("BodySmall", "TextLight")).add("le", 114, H.le(ignore=True))
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
        f"  state: {{fileID: {ctx.c('Wait/Chip/State', 'tmp')}}}\n  group: {{fileID: {ctx.c('Wait/ChipTemplate', 'cg')}}}\n"))
    return n


def waiting_node():
    n = N("Wait", "ReadyWaiting", R((0.5, 0.5), (0.5, 0.5), WAIT_POS, (WAIT_W, WAIT_H)))
    n.add("cg", 225, H.canvas_group(False))
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


def canvas_text(text_):
    p = Patch(text_, "GameCanvas/Item12")
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

TRANSFORMS = {LOCKUP: lockup_text, GLYPHS: glyphs_text, CANVAS: canvas_text, PANEL["path"]: panel_text}


def outputs():
    return {path: fn(open(os.path.join(REPO, path), encoding="utf-8").read()) for path, fn in TRANSFORMS.items()}


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
