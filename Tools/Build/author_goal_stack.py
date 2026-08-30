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
PLATE      = "f0c7acaaca5402cb82b3699ce453de18"   # Assets/_Graphics/UI/Goals/goal_plate.png
# A TMP font asset and its MATERIAL travel together: the material carries the atlas the
# glyph rects index into, so setting m_fontAsset without m_sharedMaterial renders the wrong
# atlas - wrong glyphs or none. Material fileIDs measured from the dominant shipped pairing
# (Aldrich 861 uses, ChakraPetch 93), not recalled.
FONT_LABEL = ("137312175295ff84fbe0b8c18ec605aa", "-1508558789793029919")  # ChakraPetch-Regular SDF
FONT_VALUE = ("6ab8eca0e6e2b7c4a8a495d9afae2053", "3995749905058258831")   # ALDRICH-REGULAR SDF

ROWS = 3
INT64_MAX = 9223372036854775807


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
          fill_method=0, fill_amount=1, raycast=0):
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
  m_PixelsPerUnitMultiplier: 1
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
    view = None
    for fid, (cls, body) in docs.items():
        if cls != '114': continue
        g = re.search(r'm_Script: \{fileID: 11500000, guid: (\w+)', body)
        if g and g.group(1) in VIEW_GUIDS:
            assert view is None, "two HUD views in one canvas"
            view = fid
    assert view, "no MiniGameHUDView/MultiplayerHUDView in this canvas"
    return dict(hud_rect=rect_of_go[hud_go], view=view, roundtime_go=go_named("RoundTime"))


def author(PREFAB):
    txt = PREFAB.read_text()
    already = GOALSTACK in txt
    if already:
        print(f"{PREFAB.name}: already authored - nothing to do")
        return

    a_ = resolve(txt)
    HUD_RECT, VIEW_COMP, ROUNDTIME_GO = a_["hud_rect"], a_["view"], a_["roundtime_go"]

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
        for kind in ("plate", "icon", "label", "value", "fill"):
            kids[kind] = dict(go=mint(f"row{i}/{kind}/go", taken),
                              rect=mint(f"row{i}/{kind}/rect", taken),
                              cr=mint(f"row{i}/{kind}/cr", taken),
                              gfx=mint(f"row{i}/{kind}/gfx", taken))
        row_rects.append(rc); row_comps.append(comp)

        child_rects = [kids[k]["rect"] for k in ("plate", "icon", "label", "value", "fill")]
        row_docs.append(gameobject(go, f"GoalRow{i}", [rc, cg, le, comp],
                                   active=1 if i == 0 else 0))
        row_docs.append(rect(rc, go, stack_rect, child_rects,
                             (0, 1), (0, 1), (0, 1), (0, 0), (312, 48)))
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
  m_MinWidth: 312
  m_MinHeight: 48
  m_PreferredWidth: 312
  m_PreferredHeight: 48
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
  icon: {{fileID: {kids['icon']['gfx']}}}
  label: {{fileID: {kids['label']['gfx']}}}
  value: {{fileID: {kids['value']['gfx']}}}
  progressFill: {{fileID: {kids['fill']['gfx']}}}
  canvasGroup: {{fileID: {cg}}}
  layoutElement: {{fileID: {le}}}
  primaryHeight: 48
  primaryLabelSize: 16
  primaryValueSize: 22
  primaryIconSize: 19
  primaryFillColor: {{r: 0.224, g: 0.843, b: 0.627, a: 1}}
  secondaryHeight: 37
  secondaryLabelSize: 13
  secondaryValueSize: 16
  secondaryIconSize: 14
  secondaryFillColor: {{r: 0.247, g: 0.498, b: 0.847, a: 1}}
  secondaryAlpha: 0.6
  chromeTint: {{r: 0.902, g: 0.914, b: 1, a: 1}}
  targetHexColor: FFFFFF5C
"""))

        # ---- the row's five children --------------------------------------------------
        k = kids["plate"]
        row_docs += [gameobject(k["go"], "Plate", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0), (1, 1), (0.5, 0.5), (0, 0), (0, 0)),
                     canvas_renderer(k["cr"], k["go"]),
                     image(k["gfx"], k["go"], sprite=PLATE, image_type=1)]  # 1 = Sliced

        k = kids["icon"]
        row_docs += [gameobject(k["go"], "Icon", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0.5), (0, 0.5), (0, 0.5), (17, 1), (19, 19)),
                     canvas_renderer(k["cr"], k["go"]),
                     image(k["gfx"], k["go"], color=(0.902, 0.914, 1, 1))]

        k = kids["label"]
        row_docs += [gameobject(k["go"], "Label", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0), (1, 1), (0.5, 0.5), (-48, 1), (-184, -14)),
                     canvas_renderer(k["cr"], k["go"]),
                     tmp(k["gfx"], k["go"], donor, text="COLLECT CRYSTALS",
                         font=FONT_LABEL, size=16, align=513)]   # left + middle

        k = kids["value"]
        row_docs += [gameobject(k["go"], "Value", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (1, 0.5), (1, 0.5), (1, 0.5), (-15, 1), (120, 28)),
                     canvas_renderer(k["cr"], k["go"]),
                     tmp(k["gfx"], k["go"], donor, text="0",
                         font=FONT_VALUE, size=22, align=516, color=(1, 1, 1, 1))]  # right + middle

        k = kids["fill"]
        row_docs += [gameobject(k["go"], "Fill", [k["rect"], k["cr"], k["gfx"]]),
                     rect(k["rect"], k["go"], rc, [], (0, 0), (1, 0), (0.5, 0.5), (3.5, 7), (-31, 2)),
                     canvas_renderer(k["cr"], k["go"]),
                     image(k["gfx"], k["go"], color=(0.224, 0.843, 0.627, 1),
                           image_type=3, fill_method=0, fill_amount=0)]  # 3 = Filled, horizontal

    rows_field = "".join(f"\n  - {{fileID: {c}}}" for c in row_comps)
    new += [
        gameobject(stack_go, "GoalStack", [stack_rect, stack_vlg, stack_fit, stack_comp]),
        rect(stack_rect, stack_go, HUD_RECT, row_rects, (0, 1), (0, 1), (0, 1), (16, -13), (312, 0)),
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
    edits = {"child": 0, "roundtime": 0, "view": 0}
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
        elif fid == VIEW_COMP:
            assert "\n  goalStack:" not in body
            m = re.search(r'\n  lifeFormCounter: \{fileID: -?\d+\}', body)
            assert m, "lifeFormCounter anchor missing in the view component"
            body = body[:m.end()] + f"\n  goalStack: {{fileID: {stack_comp}}}" + body[m.end():]
            edits["view"] += 1
        out.append((header, body))
    assert edits == {"child": 1, "roundtime": 1, "view": 1}, edits

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
    assert f"guid: {PLATE}" in rebuilt, "plate sprite not referenced"

    PREFAB.write_text(rebuilt)
    print(f"{PREFAB.name}: +{len(new)} documents, {ROWS} rows, RoundTime off, view wired")


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
