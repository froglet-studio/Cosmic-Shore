#!/usr/bin/env python3
"""
Give the in-game toast line the house plate.

The feed shipped as BARE TEXT: one GameObject with a TMP on it. GameToastItemView already
declares `accentImage` and `background` and both are {fileID: 0}, so the toast's DOMAIN
COLOUR - the one thing a "X jousted Y" line most needs to convey - had nowhere to go but a
text tint.

It now wears the same surface the ability lockup and the goal stack wear: a generated
TrapezoidGraphic plate behind the lockup's own LockupBloom, with the slant band on the
sloped sides. Not a third look - the third instance of one look.

THE MESSAGE HAS TO MOVE. UGUI draws a parent's own Graphic BEFORE its children, so a plate
added as a child of the text's GameObject would cover the text. The TMP component document
is REPARENTED onto a new last child rather than re-authored, so its font, material, size and
alignment survive byte-for-byte - and its fileID never changes, which is why
GameToastItemView.messageText needs no re-wiring.

The glow is tinted per-toast from the DOMAIN colour (white on the goal row, because that row
is the player's own objective; a toast is about who did the thing).

Idempotent; --check exits 1 if the plate is missing.
"""
import hashlib, re, sys, pathlib

PREFAB = pathlib.Path("Assets/_Prefabs/UI Elements/In Game/GameFeedText.prefab")

IMAGE     = "fe87c0e1cc204ed48ad3b37840f39efc"
TRAPEZOID = "571d3b9d08b3451f996795e5cc54291f"   # CosmicShore.UI.TrapezoidGraphic
BLOOM     = "006e134e05ff461bbd48ab3fd31629b4"   # HUD UI/AbilityLockup/LockupBloom.png
ITEMVIEW  = "34ad2d8864ab42bdbe5382d33916787f"   # GameToastItemView

# Read off Resources/AbilityLockupStyle.asset, same as the goal stack.
PLATE_COLOR = (0.024, 0.031, 0.063, 0.88)
EDGE_COLOR  = (0.51, 0.53, 0.61, 0.85)
GLOW_PAD    = 18

W, H = 576.0214, 39.6754
# The ANGLE carries over from the goal row (14 px over 48), never the fraction - the two
# rects have very different aspects. See Docs/GAME_MODE_TOPBAR.md section 2.0.
CHAMFER = round(H * 14.0 / 48.0, 4)
BOTTOM_FRAC = round(1 - 2 * CHAMFER / W, 4)

ACCENT_X0, ACCENT_W = 16.0, 3.0          # domain strip at the leading edge
ACCENT_INSET_Y = 8.0
TEXT_L, TEXT_R, TEXT_V = 30.0, 16.0, 4.0

# the accent must clear the chamfer at its LOWEST point, where the slant is widest
_slant = CHAMFER * (1 - ACCENT_INSET_Y / H)
assert ACCENT_X0 > _slant + 4, f"accent clips the chamfer (slant reaches {_slant:.2f})"
assert TEXT_L > ACCENT_X0 + ACCENT_W + 6, "text crowds the accent strip"

INT64_MAX = 9223372036854775807


def mint(key, taken):
    for salt in range(64):
        fid = int(hashlib.md5(f"CosmicShore/ToastPlate/{key}/{salt}".encode())
                  .hexdigest()[:16], 16) % (INT64_MAX // 2)
        if str(fid) not in taken:
            taken.add(str(fid)); return str(fid)
    raise SystemExit("no free fileID for " + key)


def split_docs(txt):
    marks = [(m.start(), m.group(0)) for m in
             re.finditer(r'^--- !u!\d+ &-?\d+(?: stripped)?$', txt, re.M)]
    out = []
    for i, (start, header) in enumerate(marks):
        end = marks[i + 1][0] if i + 1 < len(marks) else len(txt)
        out.append((header, txt[start + len(header):end]))
    return txt[:marks[0][0]], out


def gameobject(fid, name, comps):
    lines = "".join(f"\n  - component: {{fileID: {c}}}" for c in comps)
    return (f"--- !u!1 &{fid}", f"""
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
  m_IsActive: 1
""")


def rect(fid, go, parent, amin, amax, pivot, pos, size):
    return (f"--- !u!224 &{fid}", f"""
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
  m_Children: []
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {amin[0]}, y: {amin[1]}}}
  m_AnchorMax: {{x: {amax[0]}, y: {amax[1]}}}
  m_AnchoredPosition: {{x: {pos[0]}, y: {pos[1]}}}
  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}
  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}
""")


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


def image(fid, go, *, sprite=None, color=(1, 1, 1, 1), image_type=0, ppu=1):
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
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {spr}
  m_Type: {image_type}
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 0
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: {ppu}
""")


def trapezoid(fid, go):
    r, g, b, a = PLATE_COLOR
    er, eg, eb, ea = EDGE_COLOR
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
  topWidth: 1
  bottomWidth: {BOTTOM_FRAC}
  fillAmount: 1
  edgeThickness: 1.5
  edgeColor: {{r: {er}, g: {eg}, b: {eb}, a: {ea}}}
  edgeWrap: 22
  edgeAntialias: 1
""")


def author():
    txt = PREFAB.read_text()
    if f"guid: {TRAPEZOID}" in txt:
        print("already authored - nothing to do"); return

    preamble, docs = split_docs(txt)
    taken = set(re.findall(r'^--- !u!\d+ &(-?\d+)', txt, re.M))

    root_go = root_rect = tmp_comp = tmp_cr = None
    for header, body in docs:
        cls = header.split("!u!")[1].split(" ")[0]
        fid = header.split("&")[1].split()[0]
        if cls == "1": root_go = fid
        elif cls == "224": root_rect = fid
        elif cls == "222": tmp_cr = fid
        elif cls == "114" and "m_text:" in body: tmp_comp = fid
    assert root_go and root_rect and tmp_comp and tmp_cr, "unexpected prefab shape"

    ids = {k: mint(k, taken) for k in
           ("glow/go", "glow/rect", "glow/cr", "glow/gfx",
            "plate/go", "plate/rect", "plate/cr", "plate/gfx",
            "accent/go", "accent/rect", "accent/cr", "accent/gfx",
            "msg/go", "msg/rect")}

    new = [
        # --- glow: FIRST child, or it covers the plate it is meant to light -------------
        gameobject(ids["glow/go"], "Glow", [ids["glow/rect"], ids["glow/cr"], ids["glow/gfx"]]),
        rect(ids["glow/rect"], ids["glow/go"], root_rect, (0, 0), (1, 1), (0.5, 0.5),
             (0, 0), (GLOW_PAD * 2, GLOW_PAD * 2)),
        canvas_renderer(ids["glow/cr"], ids["glow/go"]),
        image(ids["glow/gfx"], ids["glow/go"], sprite=BLOOM,
              color=(0.96, 0.96, 1, 0.16), image_type=1, ppu=0.45),   # 1 = Sliced

        gameobject(ids["plate/go"], "Plate", [ids["plate/rect"], ids["plate/cr"], ids["plate/gfx"]]),
        rect(ids["plate/rect"], ids["plate/go"], root_rect, (0, 0), (1, 1), (0.5, 0.5), (0, 0), (0, 0)),
        canvas_renderer(ids["plate/cr"], ids["plate/go"]),
        trapezoid(ids["plate/gfx"], ids["plate/go"]),

        # --- the domain strip the component always wanted --------------------------------
        gameobject(ids["accent/go"], "Accent", [ids["accent/rect"], ids["accent/cr"], ids["accent/gfx"]]),
        rect(ids["accent/rect"], ids["accent/go"], root_rect, (0, 0), (0, 1), (0, 0.5),
             (ACCENT_X0, 0), (ACCENT_W, -2 * ACCENT_INSET_Y)),
        canvas_renderer(ids["accent/cr"], ids["accent/go"]),
        image(ids["accent/gfx"], ids["accent/go"], color=(1, 1, 1, 1)),

        # --- the message, MOVED (component doc reparented, fileID unchanged) -------------
        gameobject(ids["msg/go"], "Message", [ids["msg/rect"], tmp_cr, tmp_comp]),
        rect(ids["msg/rect"], ids["msg/go"], root_rect, (0, 0), (1, 1), (0.5, 0.5),
             ((TEXT_L - TEXT_R) / 2, 0), (-(TEXT_L + TEXT_R), -2 * TEXT_V)),
    ]

    kids = [ids["glow/rect"], ids["plate/rect"], ids["accent/rect"], ids["msg/rect"]]

    out, edits = [], {"root_go": 0, "root_rect": 0, "tmp": 0, "cr": 0, "view": 0}
    for header, body in docs:
        cls = header.split("!u!")[1].split(" ")[0]
        fid = header.split("&")[1].split()[0]
        if fid == root_go:
            # the TMP and its CanvasRenderer move to the Message child
            for c in (tmp_comp, tmp_cr):
                body = body.replace(f"\n  - component: {{fileID: {c}}}", "", 1)
            edits["root_go"] += 1
        elif fid == root_rect:
            assert "m_Children: []" in body
            body = body.replace("m_Children: []",
                                "m_Children:\n" + "\n".join(f"  - {{fileID: {k}}}" for k in kids), 1)
            edits["root_rect"] += 1
        elif fid in (tmp_comp, tmp_cr):
            body = re.sub(r'\n  m_GameObject: \{fileID: -?\d+\}',
                          f"\n  m_GameObject: {{fileID: {ids['msg/go']}}}", body, count=1)
            edits["tmp" if fid == tmp_comp else "cr"] += 1
        elif cls == "114" and f"guid: {ITEMVIEW}" in body:
            assert "accentImage: {fileID: 0}" in body and "background: {fileID: 0}" in body
            body = body.replace("accentImage: {fileID: 0}",
                                f"accentImage: {{fileID: {ids['accent/gfx']}}}", 1)
            body = body.replace("background: {fileID: 0}",
                                f"background: {{fileID: {ids['plate/gfx']}}}", 1)
            edits["view"] += 1
        out.append((header, body))
    assert edits == {"root_go": 1, "root_rect": 1, "tmp": 1, "cr": 1, "view": 1}, edits

    rebuilt = preamble + "".join(h + b for h, b in out + new)

    # ---- validate before writing ----------------------------------------------------
    anchors = re.findall(r'^--- !u!\d+ &(-?\d+)', rebuilt, re.M)
    assert len(anchors) == len(set(anchors)), "duplicate fileID"
    assert len(anchors) == len(docs) + len(new)
    defined, refs = set(anchors), set(re.findall(r'\{fileID: (-?\d+)\}', rebuilt))
    base_d = set(re.findall(r'^--- !u!\d+ &(-?\d+)', txt, re.M))
    base_r = set(re.findall(r'\{fileID: (-?\d+)\}', txt))
    assert not ((refs - defined - {"0"}) - (base_r - base_d - {"0"})), "new dangling refs"
    # the TMP component must be owned by exactly one GameObject, and it must be Message
    tmp_body = next(b for h, b in out + new if h.endswith(f"&{tmp_comp}"))
    assert f"m_GameObject: {{fileID: {ids['msg/go']}}}" in tmp_body
    assert rebuilt.count(f"- component: {{fileID: {tmp_comp}}}") == 1
    assert rebuilt.count(f"- component: {{fileID: {tmp_cr}}}") == 1
    assert f"guid: {BLOOM}" in rebuilt and f"guid: {TRAPEZOID}" in rebuilt

    PREFAB.write_text(rebuilt)
    print(f"{PREFAB.name}: +{len(new)} documents - Glow, Plate, Accent, Message "
          f"(chamfer {CHAMFER:.2f}px, bottomWidth {BOTTOM_FRAC})")


def main():
    if "--check" in sys.argv:
        ok = f"guid: {TRAPEZOID}" in PREFAB.read_text()
        print("toast plate present" if ok else "toast plate MISSING")
        sys.exit(0 if ok else 1)
    author()


if __name__ == "__main__":
    main()
