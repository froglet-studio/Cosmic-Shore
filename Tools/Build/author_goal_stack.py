#!/usr/bin/env python3
"""
Author the top-left GOAL STACK into Assets/_Prefabs/CORE/GameCanvas.prefab.

The stack replaces the RoundTime ring cluster. That ring was never a clock: every turn
monitor raises onUpdateTurnMonitorDisplay with the metric REMAINING, so a timer face was
drawn over an unlabelled objective count. The stack shows the same number with the two
things the ring could not - what you are counting, and how many it takes.

Idempotent: re-running prints "already authored" and exits 0.
Validate-before-write: every document is rebuilt in memory and asserted before the file
is touched, so a failed assert costs nothing.

    python3 Tools/Build/author_goal_stack.py [--check]

--check exits 1 if the prefab does not already carry the stack (for CI).
"""
import hashlib, re, sys, pathlib

# Both canvases. GameCanvas-HexRace is a hard COPY rather than a variant (Docs/GAMECANVAS.md
# records that as a known defect), so propagation is severed and it has to be authored too -
# and it is the one 12 of the domain modes actually instance.
PREFABS = [pathlib.Path("Assets/_Prefabs/CORE/GameCanvas.prefab"),
           pathlib.Path("Assets/_Prefabs/GameCanvas-HexRace.prefab")]

VIEW_GUIDS = ("726155bbf4139474dbf25b978b006c3a",   # MiniGameHUDView
              "3d38e324226a48149a22fa95f9d10448")   # MultiplayerHUDView (subclass, same field)

# --- guids harvested from shipped assets, never recalled -------------------------------
IMAGE      = "fe87c0e1cc204ed48ad3b37840f39efc"
TMP_UGUI   = "f4688fdb7df04437aeb418b961361dc5"
VLAYOUT    = "59f8146938fff824cb5fd77236b75775"   # measured: children vary in Y
LAYOUTELEM = "306cc8c2b49d7114eaa3623786fc2126"
FITTER     = "3245ec927659c4140ac4f8d17403cc18"
GOALROW    = "7b8e1255a96efb8b109ddf11ddf2fdf8"
GOALSTACK  = "f2fb3346fa3bea72ff65cd3bdb67758e"
# The plate is GENERATED, never sprited - the ability lockup's own law (Docs/ABILITY_LOCKUP.md):
# a trapezoid has no 9-slice, so a sprited one freezes the slant into the art and is crisp only
# at the size it was exported at. The first cut of this stack shipped a 112x36 PNG stretched to
# 312x48 and read exactly as blurry as that arithmetic predicts.
TRAPEZOID  = "571d3b9d08b3451f996795e5cc54291f"   # CosmicShore.UI.TrapezoidGraphic
BLOOM      = "006e134e05ff461bbd48ab3fd31629b4"   # HUD UI/AbilityLockup/LockupBloom.png (9-slice 48)
# A TMP font asset and its MATERIAL travel together: the material carries the atlas the
# glyph rects index into, so setting m_fontAsset without m_sharedMaterial renders the wrong
# atlas - wrong glyphs or none. Material fileIDs measured from the dominant shipped pairing
# (Aldrich 861 uses, ChakraPetch 93), not recalled.
FONT_LABEL = ("137312175295ff84fbe0b8c18ec605aa", "-1508558789793029919")  # ChakraPetch-Regular SDF
FONT_VALUE = ("6ab8eca0e6e2b7c4a8a495d9afae2053", "3995749905058258831")   # ALDRICH-REGULAR SDF

ROWS = 3
INT64_MAX = 9223372036854775807

# Read off Resources/AbilityLockupStyle.asset, so the goal stack and the ability row are visibly
# the same product rather than two people's idea of a dark plate.
PLATE_COLOR = (0.024, 0.031, 0.063, 0.9)
EDGE_COLOR  = (0.51, 0.53, 0.61, 0.85)
GLOW_COLOR  = (0.96, 0.96, 1, 0.3)   # alpha = the PRIMARY row's lit strength
GLOW_PAD    = 28          # px of soft falloff outside the row, per side
# The chamfer is authored in PIXELS and converted, because TrapezoidGraphic takes FRACTIONS of
# the rect: 14px over a 48px height is a ~16 degree slant, which reads as a HUD wedge. The
# lockup's own 9px-on-104 would be invisible on a bar four times as wide.
# The row is sized to the WIDEST LABEL IT CAN BE ASKED TO SHOW, not to a guess. At 312 wide
# the 128-unit label box wrapped 6 of the 10 authored objectives onto two lines - measured, see
# assert_content_fits() - which is what "COLLECT / CRYSTALS" under the FPS readout was.
ROW_W, ROW_H = 400, 48
CHAMFER_PX = 14                       # px of slant per side; the ANGLE is what carries over
BOTTOM_FRAC = round(1 - 2 * CHAMFER_PX / ROW_W, 4)

# Every box in the row, in ROW SPACE (x from the left edge, y up from the bottom). Authoring
# them here and deriving the RectTransform arithmetic once is what lets the clearance checks
# below be arithmetic rather than eyeballing.
TEXT_MID   = 28                       # text centre, LIFTED off the row's middle (24) so the bar
                                      # gets a band of its own instead of crowding the numerals
ICON_X, ICON_SIZE = 17, 19
LABEL_X0, LABEL_X1 = 44, 240          # 196 units - fits the widest authored label at font 16
VALUE_W, VALUE_PAD = 132, 15          # 132 fits "1997/2000" at font 22; 120 did not
LABEL_H = 34

VALUE_X1 = ROW_W - VALUE_PAD
VALUE_X0 = VALUE_X1 - VALUE_W
assert VALUE_X0 - LABEL_X1 >= 10, "label and value columns collide"

# Sibling order IS draw order, so this list is the stacking order bottom-up: the bloom under
# the plate it lights, the track under the bar that fills over it.
CHILDREN = ("glow", "plate", "icon", "label", "value", "track", "fill")

# The whole top bar drops by one amount, so the left and the centre keep their relationship and
# both stop hugging the screen edge. The size is set by the LEFT: DiagnosticsHUD builds its own
# ConstantPixelSize canvas and parks the FPS panel at (8, -8) with height TopY 8 + ~18 + Pad 10,
# so it owns roughly the first 44 screen px. The stack's old top margin of 13 sat inside that,
# which is why the readout was drawn over "COLLECT". 52 clears it with 8 units of air.
TOP_BAR_DROP = 39
GOAL_STACK_TOP = 13 + TOP_BAR_DROP    # 52

# The slider bed, in row coordinates. It must clear the chamfer AT ITS OWN TOP EDGE - the
# slant is widest at the bottom of the plate, which is exactly where the bar lives, so the
# clearance has to be measured there rather than at the plate's waist.
BAR_L, BAR_R, BAR_Y, BAR_H = 26, ROW_W - 26, 7, 3
_slant_at_bar = CHAMFER_PX * (1 - (BAR_Y + BAR_H) / ROW_H)
assert BAR_L > _slant_at_bar + 6 and BAR_R < ROW_W - _slant_at_bar - 6, \
    f"the progress bar clips the chamfer (slant reaches x={_slant_at_bar:.1f})"
# ... and clear of the text above it, which is the other half of "more spacing".
_value_bottom = TEXT_MID - 11         # half of the 22pt value's em box
assert _value_bottom - (BAR_Y + BAR_H) >= 6, "the bar crowds the numerals"
# RectTransform form of the same rect, anchored to the row's bottom edge.
BAR_POS  = ((BAR_L + BAR_R) / 2 - ROW_W / 2, BAR_Y + BAR_H / 2)
BAR_SIZE = (BAR_R - BAR_L - ROW_W, BAR_H)


def assert_content_fits():
    """A label box is a promise the row can keep. Measure it against the SHIPPED TTFs and the
    SHIPPED catalogue rather than trusting the layout to look right - word wrapping is off, so
    an overflowing label would run under the numerals instead of quietly stacking, and either
    way the failure belongs here rather than on screen."""
    try:
        from PIL import ImageFont
    except ImportError:
        print("  NOTE: PIL missing - label/value fit NOT verified this run")
        return
    fonts = pathlib.Path("Assets/Unity Assests/TextMesh Pro/Resources/Fonts & Materials")
    lab = ImageFont.truetype(str(fonts / "ChakraPetch-Regular.ttf"), 160)   # 10x for precision
    val = ImageFont.truetype(str(fonts / "ALDRICH-REGULAR.TTF"), 220)

    catalogue = pathlib.Path("Assets/Resources/ObjectiveIconSet.asset").read_text()
    labels = [m.group(1).strip() for m in re.finditer(r'^\s+label: (.+)$', catalogue, re.M)]
    labels.append("Time remaining")      # the clock row's label, authored in GoalStack
    assert labels, "no labels found in ObjectiveIconSet.asset"

    box = LABEL_X1 - LABEL_X0
    worst = max(labels, key=lambda t: lab.getlength(t.upper()))
    worst_w = lab.getlength(worst.upper()) / 10.0
    assert worst_w <= box, (
        f"label '{worst.upper()}' needs {worst_w:.1f} units and the box is {box} - it would "
        f"overflow into the numerals (widen LABEL_X1, or shorten the label in the catalogue)")

    # The counted objectives run to 2000 (Rampage/Ribcage/Salvo), so the widest value the row
    # can be asked to draw is four digits over four.
    widest_value = val.getlength("1997/2000") / 10.0
    assert widest_value <= VALUE_W, (
        f"the value column is {VALUE_W} and '1997/2000' needs {widest_value:.1f}")
    print(f"  content fits: widest label '{worst.upper()}' {worst_w:.1f}/{box}, "
          f"widest value {widest_value:.1f}/{VALUE_W}")


def mint(key, taken):
    """Deterministic, in-range, collision-checked fileID."""
    for salt in range(64):
        h = hashlib.md5(f"CosmicShore/GoalStack/{key}/{salt}".encode()).hexdigest()
        fid = int(h[:16], 16) % (INT64_MAX // 2)      # comfortably inside a signed int64
        s = str(fid)
        if s not in taken:
            taken.add(s)
            return s
    raise SystemExit("could not mint a free fileID for " + key)


def split_docs(txt):
    """(header, body) per document. body already starts with its own newline."""
    marks = [(m.start(), m.group(0)) for m in
             re.finditer(r'^--- !u!\d+ &-?\d+(?: stripped)?$', txt, re.M)]
    out, preamble = [], txt[:marks[0][0]] if marks else txt
    for i, (start, header) in enumerate(marks):
        end = marks[i + 1][0] if i + 1 < len(marks) else len(txt)
        out.append((header, txt[start + len(header):end]))
    return preamble, out


def rect(fid, go, parent, children, amin, amax, pivot, pos, size, offmin=None, offmax=None):
    kids = "".join(f"\n  - {{fileID: {c}}}" for c in children) or " []"
    if children:
        kids = "\n" + "\n".join(f"  - {{fileID: {c}}}" for c in children)
    body = f"""
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{kids if children else ' []'}
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {amin[0]}, y: {amin[1]}}}
  m_AnchorMax: {{x: {amax[0]}, y: {amax[1]}}}
  m_AnchoredPosition: {{x: {pos[0]}, y: {pos[1]}}}
  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}
  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}
"""
    return (f"--- !u!224 &{fid}", body)


def gameobject(fid, name, comps, active=1):
    lines = "".join(f"\n  - component: {{fileID: {c}}}" for c in comps)
    body = f"""
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:{lines}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: {active}
"""
    return (f"--- !u!1 &{fid}", body)


def canvas_renderer(fid, go):
    return (f"--- !u!222 &{fid}", f"""
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
""")


def canvas_group(fid, go):
    return (f"--- !u!225 &{fid}", f"""
CanvasGroup:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_Alpha: 1
  m_Interactable: 0
  m_BlocksRaycasts: 0
  m_IgnoreParentGroups: 0
""")


def image(fid, go, *, sprite=None, color=(1, 1, 1, 1), image_type=0,
          fill_method=0, fill_amount=1, raycast=0, ppu=1):
    spr = (f"{{fileID: 21300000, guid: {sprite}, type: 3}}" if sprite else "{fileID: 0}")
    r, g, b, a = color
    return (f"--- !u!114 &{fid}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: {r}, g: {g}, b: {b}, a: {a}}}
  m_RaycastTarget: {raycast}
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {spr}
  m_Type: {image_type}
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: {fill_method}
  m_FillAmount: {fill_amount}
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: {ppu}
""")


def trapezoid(fid, go, *, color, bottom_frac, top_frac=1.0,
              edge_thickness=2, edge_color=(1, 1, 1, 1), edge_wrap=28, edge_aa=1):
    """A TrapezoidGraphic document. Field order follows the class - MaskableGraphic's block
    first, exactly as a shipped BlastProfileGraphic serializes it."""
    r, g, b, a = color
    er, eg, eb, ea = edge_color
    return (f"--- !u!114 &{fid}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TRAPEZOID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: {r}, g: {g}, b: {b}, a: {a}}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  topWidth: {top_frac}
  bottomWidth: {bottom_frac}
  fillAmount: 1
  edgeThickness: {edge_thickness}
  edgeColor: {{r: {er}, g: {eg}, b: {eb}, a: {ea}}}
  edgeWrap: {edge_wrap}
  edgeAntialias: {edge_aa}
""")


def tmp(fid, go, tmp_donor_body, *, text, font, size, align, color=(0.902, 0.914, 1, 1)):
    """Clone a shipped TextMeshProUGUI document; rewrite only identity + style fields."""
    b = tmp_donor_body
    b = re.sub(r'\n  m_GameObject: \{fileID: -?\d+\}', f'\n  m_GameObject: {{fileID: {go}}}', b, count=1)
    r, g, bl, a = color
    b = re.sub(r'\n  m_Color: \{[^}]*\}', f'\n  m_Color: {{r: {r}, g: {g}, b: {bl}, a: {a}}}', b, count=1)
    b = re.sub(r'\n  m_text: .*', f'\n  m_text: {text}', b, count=1)
    font_guid, mat_id = font
    b = re.sub(r'\n  m_fontAsset: \{[^}]*\}',
               f'\n  m_fontAsset: {{fileID: 11400000, guid: {font_guid}, type: 2}}', b, count=1)
    # the material must follow the font asset - see the FONT_* note above
    b = re.sub(r'\n  m_sharedMaterial: \{[^}]*\}',
               f'\n  m_sharedMaterial: {{fileID: {mat_id}, guid: {font_guid}, type: 2}}', b, count=1)
    b = re.sub(r'\n  m_Materials:\n  - \{[^}]*\}',
               f'\n  m_Materials:\n  - {{fileID: {mat_id}, guid: {font_guid}, type: 2}}', b, count=1)
    b = re.sub(r'\n  m_fontSize: [-\d.]+', f'\n  m_fontSize: {size}', b, count=1)
    b = re.sub(r'\n  m_fontSizeBase: [-\d.]+', f'\n  m_fontSizeBase: {size}', b, count=1)
    b = re.sub(r'\n  m_textAlignment: \d+', f'\n  m_textAlignment: {align}', b, count=1)
    b = re.sub(r'\n  m_enableAutoSizing: \d+', '\n  m_enableAutoSizing: 0', b, count=1)
    b = re.sub(r'\n  m_RaycastTarget: \d+', '\n  m_RaycastTarget: 0', b, count=1)
    b = re.sub(r'\n  m_isRightToLeft: \d+', '\n  m_isRightToLeft: 0', b, count=1)
    # One line, always. A wrapped label silently changes what the row IS - "COLLECT / CRYSTALS"
    # reads as two goals - where an overflow is loud and gets fixed. assert_content_fits() is
    # what makes turning wrapping off safe.
    b = re.sub(r'\n  m_TextWrappingMode: \d+', '\n  m_TextWrappingMode: 0', b, count=1)
    return (f"--- !u!114 &{fid}", b)


def resolve(txt):
    """Anchor by NAME and script guid, never by a literal fileID - the two canvases number
    everything differently."""
    docs = {m.group(2): (m.group(1), m.group(3)) for m in
            re.finditer(r'--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=\n--- !u!|\Z)', txt, re.S)}
    names, rect_of_go = {}, {}
    for fid, (cls, body) in docs.items():
        if cls == '1':
            n = re.search(r'^  m_Name: (.*)$', body, re.M)
            if n: names[fid] = n.group(1).strip()
        elif cls == '224':
            g = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
            if g: rect_of_go[g.group(1)] = fid

    def go_named(n):
        hits = [f for f, nm in names.items() if nm == n]
        assert len(hits) == 1, f"expected exactly one GameObject named {n}, found {len(hits)}"
        return hits[0]

    hud_go = go_named("MiniGameHUD")
    hud_rect_id = rect_of_go[hud_go]

    # TWO objects are named "Scoreboard" in each canvas: the top-bar score block and the
    # end-game panel. Tell them apart by PARENT, never by name.
    scoreboard = None
    for fid, (cls, body) in docs.items():
        if cls != '224': continue
        g = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
        if not g or names.get(g.group(1)) != "Scoreboard": continue
        if re.search(rf'm_Father: \{{fileID: {hud_rect_id}\}}', body):
            assert scoreboard is None, "two top-bar Scoreboards under MiniGameHUD"
            scoreboard = fid
    assert scoreboard, "no Scoreboard parented to MiniGameHUD"
    view = None
    for fid, (cls, body) in docs.items():
        if cls != '114': continue
        g = re.search(r'm_Script: \{fileID: 11500000, guid: (\w+)', body)
        if g and g.group(1) in VIEW_GUIDS:
            assert view is None, "two HUD views in one canvas"
            view = fid
    assert view, "no MiniGameHUDView/MultiplayerHUDView in this canvas"
    return dict(hud_rect=hud_rect_id, view=view, roundtime_go=go_named("RoundTime"),
                scoreboard_rect=scoreboard)


def author(PREFAB):
    txt = PREFAB.read_text()
    already = GOALSTACK in txt
    if already:
        print(f"{PREFAB.name}: already authored - nothing to do")
        return

    assert_content_fits()
    a_ = resolve(txt)
    HUD_RECT, VIEW_COMP, ROUNDTIME_GO = a_["hud_rect"], a_["view"], a_["roundtime_go"]
    SCOREBOARD_RECT = a_["scoreboard_rect"]

    preamble, docs = split_docs(txt)
    taken = set(re.findall(r'^--- !u!\d+ &(-?\d+)', txt, re.M))

    # donor: a shipped TextMeshProUGUI document, so the serializer version is right by
    # construction rather than by transcription.
    donor = None
    for header, body in docs:
        if header.startswith("--- !u!114") and f"guid: {TMP_UGUI}" in body and "m_text:" in body:
            donor = body
            break
    assert donor is not None, "no TextMeshProUGUI donor in the prefab"

    new = []
    # ---- the stack root -------------------------------------------------------------
    stack_go   = mint("stack/go", taken)
    stack_rect = mint("stack/rect", taken)
    stack_vlg  = mint("stack/vlg", taken)
    stack_fit  = mint("stack/fit", taken)
    stack_comp = mint("stack/comp", taken)

    row_rects, row_comps = [], []
    row_docs = []
    for i in range(ROWS):
        go    = mint(f"row{i}/go", taken)
        rc    = mint(f"row{i}/rect", taken)
        cg    = mint(f"row{i}/cg", taken)
        le    = mint(f"row{i}/le", taken)
        comp  = mint(f"row{i}/comp", taken)
        kids = {}
        for kind in CHILDREN:
            kids[kind] = dict(go=mint(f"row{i}/{kind}/go", taken),
                              rect=mint(f"row{i}/{kind}/rect", taken),
                              cr=mint(f"row{i}/{kind}/cr", taken),
                              gfx=mint(f"row{i}/{kind}/gfx", taken))
        row_rects.append(rc); row_comps.append(comp)

        child_rects = [kids[k]["rect"] for k in CHILDREN]
        row_docs.append(gameobject(go, f"GoalRow{i}", [rc, cg, le, comp],
                                   active=1 if i == 0 else 0))
        row_docs.append(rect(rc, go, stack_rect, child_rects,
                             (0, 1), (0, 1), (0, 1), (0, 0), (ROW_W, ROW_H)))
        row_docs.append(canvas_group(cg, go))
        row_docs.append((f"--- !u!114 &{le}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {LAYOUTELEM}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_IgnoreLayout: 0
  m_MinWidth: {ROW_W}
  m_MinHeight: {ROW_H}
  m_PreferredWidth: {ROW_W}
  m_PreferredHeight: {ROW_H}
  m_FlexibleWidth: -1
  m_FlexibleHeight: -1
  m_LayoutPriority: 1
"""))
        # the GoalRow component
        row_docs.append((f"--- !u!114 &{comp}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GOALROW}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  plate: {{fileID: {kids['plate']['gfx']}}}
  glow: {{fileID: {kids['glow']['gfx']}}}
  icon: {{fileID: {kids['icon']['gfx']}}}
  label: {{fileID: {kids['label']['gfx']}}}
  value: {{fileID: {kids['value']['gfx']}}}
  progressTrack: {{fileID: {kids['track']['gfx']}}}
  progressFill: {{fileID: {kids['fill']['gfx']}}}
  canvasGroup: {{fileID: {cg}}}
  layoutElement: {{fileID: {le}}}
  primaryHeight: {ROW_H}
  primaryLabelSize: 16
  primaryValueSize: 22
  primaryIconSize: 19
  primaryGlowAlpha: {GLOW_COLOR[3]}
  primaryFillColor: {{r: 0.224, g: 0.843, b: 0.627, a: 1}}
  secondaryHeight: 37
  secondaryLabelSize: 13
  secondaryValueSize: 16
  secondaryIconSize: 14
  secondaryGlowAlpha: 0.12
  secondaryFillColor: {{r: 0.247, g: 0.498, b: 0.847, a: 1}}
  secondaryAlpha: 0.6
  chromeTint: {{r: 0.902, g: 0.914, b: 1, a: 1}}
  targetHexColor: FFFFFF5C
  glowColor: {{r: {GLOW_COLOR[0]}, g: {GLOW_COLOR[1]}, b: {GLOW_COLOR[2]}, a: 1}}
  glowPunchAlpha: 1
  glowPunchSeconds: 0.45
  trackColor: {{r: 0.902, g: 0.914, b: 1, a: 0.16}}
"""))

        # ---- the row's children, in the CHILDREN draw order ----------------------------
        # The bloom overhangs the row on every side; it is a soft falloff, so its job is to
        # make the plate look lit rather than to draw an edge of its own. ppu 0.6 shrinks the
        # sprite's 48px 9-slice border to ~29px, which a 48px-tall row can actually carry -
        # at 1 the two 48px borders would leave a 4px middle and the glow would read as two
        # blobs with a seam.
        k = kids["glow"]
        row_docs += [gameobject(k["go"], "Glow", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0), (1, 1), (0.5, 0.5), (0, 0),
                          (GLOW_PAD * 2, GLOW_PAD * 2)),
                     canvas_renderer(k["cr"], k["go"]),
                     image(k["gfx"], k["go"], sprite=BLOOM, color=GLOW_COLOR,
                           image_type=1, ppu=0.6)]  # 1 = Sliced

        k = kids["plate"]
        row_docs += [gameobject(k["go"], "Plate", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0), (1, 1), (0.5, 0.5), (0, 0), (0, 0)),
                     canvas_renderer(k["cr"], k["go"]),
                     trapezoid(k["gfx"], k["go"], color=PLATE_COLOR, bottom_frac=BOTTOM_FRAC,
                               edge_color=EDGE_COLOR)]

        k = kids["icon"]
        row_docs += [gameobject(k["go"], "Icon", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0.5), (0, 0.5), (0, 0.5),
                          (ICON_X, TEXT_MID - ROW_H / 2), (ICON_SIZE, ICON_SIZE)),
                     canvas_renderer(k["cr"], k["go"]),
                     image(k["gfx"], k["go"], color=(0.902, 0.914, 1, 1))]

        k = kids["label"]
        row_docs += [gameobject(k["go"], "Label", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0), (1, 1), (0.5, 0.5),
                          ((LABEL_X0 + LABEL_X1) / 2 - ROW_W / 2, TEXT_MID - ROW_H / 2),
                          (LABEL_X1 - LABEL_X0 - ROW_W, LABEL_H - ROW_H)),
                     canvas_renderer(k["cr"], k["go"]),
                     tmp(k["gfx"], k["go"], donor, text="COLLECT CRYSTALS",
                         font=FONT_LABEL, size=16, align=513)]   # left + middle

        k = kids["value"]
        row_docs += [gameobject(k["go"], "Value", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (1, 0.5), (1, 0.5), (1, 0.5),
                          (-VALUE_PAD, TEXT_MID - ROW_H / 2), (VALUE_W, 28)),
                     canvas_renderer(k["cr"], k["go"]),
                     tmp(k["gfx"], k["go"], donor, text="0",
                         font=FONT_VALUE, size=22, align=516, color=(1, 1, 1, 1))]  # right + middle

        # Track and bar share one rect, so the bar can only ever sit inside its bed. Inset to
        # 22..294 rather than the plate's full width: the chamfer eats 14px at the bottom
        # corners, and a bar that ran to the plate's edge would be clipped by the slant.
        for kind, colour, itype, amount in (
                ("track", (0.902, 0.914, 1, 0.16), 0, 1),
                ("fill",  (0.224, 0.843, 0.627, 1), 3, 0)):   # 3 = Filled, horizontal
            k = kids[kind]
            row_docs += [gameobject(k["go"], kind.capitalize(), [k["rect"], k["cr"], k["gfx"]]),
                         rect(k["rect"], k["go"], rc, [], (0, 0), (1, 0), (0.5, 0.5),
                              BAR_POS, BAR_SIZE),
                         canvas_renderer(k["cr"], k["go"]),
                         image(k["gfx"], k["go"], color=colour, image_type=itype,
                               fill_method=0, fill_amount=amount)]

    rows_field = "".join(f"\n  - {{fileID: {c}}}" for c in row_comps)
    new += [
        gameobject(stack_go, "GoalStack", [stack_rect, stack_vlg, stack_fit, stack_comp]),
        rect(stack_rect, stack_go, HUD_RECT, row_rects, (0, 1), (0, 1), (0, 1),
             (16, -GOAL_STACK_TOP), (ROW_W, 0)),
        (f"--- !u!114 &{stack_vlg}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {stack_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {VLAYOUT}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Padding:
    m_Left: 0
    m_Right: 0
    m_Top: 0
    m_Bottom: 0
  m_ChildAlignment: 0
  m_Spacing: 6
  m_ChildForceExpandWidth: 0
  m_ChildForceExpandHeight: 0
  m_ChildControlWidth: 1
  m_ChildControlHeight: 1
  m_ChildScaleWidth: 0
  m_ChildScaleHeight: 0
  m_ReverseArrangement: 0
"""),
        (f"--- !u!114 &{stack_fit}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {stack_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FITTER}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_HorizontalFit: 0
  m_VerticalFit: 2
"""),
        (f"--- !u!114 &{stack_comp}", f"""
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {stack_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {GOALSTACK}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  rows:{rows_field}
  iconSet: {{fileID: 0}}
  clockLabel: Time remaining
"""),
    ] + row_docs

    # ---- edits to existing documents -------------------------------------------------
    out = []
    edits = {"child": 0, "roundtime": 0, "view": 0, "scoreboard": 0}
    for header, body in docs:
        fid = header.split("&")[1].split()[0]
        if fid == HUD_RECT:
            assert "m_Children:" in body
            body = body.replace("\n  m_Father:", f"\n  - {{fileID: {stack_rect}}}\n  m_Father:", 1)
            edits["child"] += 1
        elif fid == ROUNDTIME_GO:
            assert "\n  m_IsActive: 1\n" in body, "RoundTime already inactive?"
            body = body.replace("\n  m_IsActive: 1\n", "\n  m_IsActive: 0\n", 1)
            edits["roundtime"] += 1
        elif fid == SCOREBOARD_RECT:
            m = re.search(r'\n  m_AnchoredPosition: \{x: ([-\d.]+), y: ([-\d.]+)\}', body)
            assert m, "scoreboard has no anchored position"
            y = float(m.group(2)) - TOP_BAR_DROP
            body = (body[:m.start()] +
                    f"\n  m_AnchoredPosition: {{x: {m.group(1)}, y: {y:g}}}" + body[m.end():])
            edits["scoreboard"] += 1
        elif fid == VIEW_COMP:
            assert "\n  goalStack:" not in body
            m = re.search(r'\n  lifeFormCounter: \{fileID: -?\d+\}', body)
            assert m, "lifeFormCounter anchor missing in the view component"
            body = body[:m.end()] + f"\n  goalStack: {{fileID: {stack_comp}}}" + body[m.end():]
            edits["view"] += 1
        out.append((header, body))
    assert edits == {"child": 1, "roundtime": 1, "view": 1, "scoreboard": 1}, edits

    # insert the new documents before the trailing PrefabInstance blocks, if any
    idx = next((i for i, (h, _) in enumerate(out) if h.startswith("--- !u!1001")), len(out))
    out = out[:idx] + new + out[idx:]

    rebuilt = preamble + "".join(h + b for h, b in out)

    # ---- validate before writing -----------------------------------------------------
    anchors = re.findall(r'^--- !u!\d+ &(-?\d+)', rebuilt, re.M)
    assert len(anchors) == len(set(anchors)), "duplicate fileID minted"
    assert len(anchors) == len(docs) + len(new), (len(anchors), len(docs), len(new))
    for a in anchors:
        assert abs(int(a)) <= INT64_MAX, f"fileID overflows int64: {a}"

    defined = set(anchors)
    refs = set(re.findall(r'\{fileID: (-?\d+)\}', rebuilt))
    base_refs = set(re.findall(r'\{fileID: (-?\d+)\}', txt))
    base_defined = set(re.findall(r'^--- !u!\d+ &(-?\d+)', txt, re.M))
    new_dangling = (refs - defined - {"0"}) - (base_refs - base_defined - {"0"})
    assert not new_dangling, f"new dangling references: {sorted(new_dangling)[:10]}"

    for fid in [stack_comp] + row_comps:
        assert rebuilt.count(f"&{fid}\n") == 1, f"component {fid} not defined exactly once"
    assert rebuilt.count(f"guid: {TRAPEZOID}") == ROWS, "one generated plate per row expected"
    assert rebuilt.count(f"guid: {BLOOM}") == ROWS, "one bloom per row expected"

    PREFAB.write_text(rebuilt)
    print(f"{PREFAB.name}: +{len(new)} documents, {ROWS} rows of {ROW_W}x{ROW_H}, "
          f"top bar dropped {TOP_BAR_DROP}, RoundTime off, view wired")


def main():
    if "--check" in sys.argv:
        missing = [p.name for p in PREFABS if GOALSTACK not in p.read_text()]
        print("goal stack MISSING in: " + ", ".join(missing) if missing
              else "goal stack present in both canvases")
        sys.exit(1 if missing else 0)
    for p in PREFABS:
        author(p)


if __name__ == "__main__":
    main()
