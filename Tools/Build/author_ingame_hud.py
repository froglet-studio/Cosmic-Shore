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


def canvas_text(text_):
    p = Patch(text_, "GameCanvas/Item12")
    patch_goal_rows(p)
    patch_pause(p)
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
