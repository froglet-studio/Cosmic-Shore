#!/usr/bin/env python3
"""
Re-author the Maelstrom launch panel's POOL LIST so it reads like the Toy Box.

    python3 Tools/Build/author_maelstrom_pool_cards.py            # write prefab + scene
    python3 Tools/Build/author_maelstrom_pool_cards.py --check    # fail if either drifted

WHAT IT FIXES (measured on the authored assets, 2026-09-16):

  The pool row was a 228x170 chamfered PNG (ARCADE/Game_Option_Border_Active) drawn SIMPLE -
  stretched - into a 260x80 grid cell, carrying ONE centred label and nothing else. That is the
  same trap Docs/HomeHub/ARCHITECTURE.md section 4.1.8 records for the toy cards and
  Docs/GAME_MODE_TOPBAR.md for the goal stack: a low-resolution plate upscaled on every display
  (the pixelated bent corners), here squashed to 3.25:1 on top of it. It also said only the mode's
  name, so sixteen rows of a ladder whose entire subject is WHICH RUNG A MODE ENTERS ON told the
  player nothing about the ladder.

  The row is now the Toy Box's VARIANT ROW: the same two chamfered sprites drawn SLICED (so the
  chamfer is a chamfer at any rect), the name bottom-left, and one detail line above it -
  "TIER 2  ·  SPARROW". Cell 310x88 against the variant row's 275x88.

ASCII ONLY in every string it writes: the fleet's UI font (ALDRICH-REGULAR SDF) carries 32..126
plus nbsp and an ellipsis and has an EMPTY fallback table, so a middle dot or a check mark
renders as tofu. A separator here shipped as an empty box once already.

WHAT IT DELIBERATELY DOES NOT DO: put the mode's IconActive on the row. That field is the ARCADE
GRID's card art and is legacy - Salvo carries Rampage's picture and Joust carries Duel for the
Cell's - so filling the Toy Box card's PORTRAIT slot from it would be inventing art out of a field
that does not mean what the slot wants. The Toy Box's own answer to "this thing has no portrait"
is the accent fill, and a mode has no authored accent either, so the row states facts instead.
"""
import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PREFAB = os.path.join(ROOT, "Assets", "_Prefabs", "UI Elements", "ArcadeLaunch",
                      "MaelstromPoolRow.prefab")
SCENE = os.path.join(ROOT, "Assets", "_Scenes", "Menu_Main.unity")

# script guids (stable for this project's Unity version)
TMP_GUID = "f4688fdb7df04437aeb418b961361dc5"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
FITTER_GUID = "3245ec927659c4140ac4f8d17403cc18"
ENTRY_GUID = "f35e7419ae174139b689e0522d892ed9"
CANVASGROUP_CLASS = "225"
FONT_GUID = "6ab8eca0e6e2b7c4a8a495d9afae2053"
FONT_MAT = "3995749905058258831"

PLATE_GUID = "54ad1e72496cc12498291c5a57d81f8b"   # Group 1585.png        - the card body
RIM_GUID = "a0f080f06c102c7469074c50e7457822"     # Rectangle 1127 (2).png - the card rim

# UGUI divides a sprite's PPU by the CANVAS's referencePixelsPerUnit before slicing, so a border
# authored at design scale x4 needs referencePixelsPerUnit/100 to come back to design scale.
# Menu_Main's canvas is 240. Read off the scene rather than written down (see resolve_multiplier).
SLICE_MULTIPLIER_REF = 100.0

# Fixed so a re-run is idempotent: minting one per run would append a new fitter every time.
FITTER_ID = 515927811

# ── identities that must survive: Menu_Main points at the ENTRY component by fileID ────────────
GO_ROOT = 3269776104706994919
TR_ROOT = 7487735886153168846
CR_ROOT = 5436873114323381513
IMG_ROOT = 404604575313429005
ENTRY = 7246761228144965327          # <- MaelstromPoolListView.rowPrefab points here
GO_NAME = 5321472749888460313        # the old "Game Description Text", re-cast as the NAME line
TR_NAME = 7073505379906695214
CR_NAME = 8800103131690077039
TMP_NAME = 7797072703496990648

# new
GO_RIM, TR_RIM, CR_RIM, IMG_RIM = 3269776104706994920, 3269776104706994921, 3269776104706994922, 3269776104706994923
GO_DET, TR_DET, CR_DET, TMP_DET = 3269776104706994924, 3269776104706994925, 3269776104706994926, 3269776104706994927
GROUP_ROOT = 3269776104706994928

# ── the numbers ────────────────────────────────────────────────────────────────────────────────
# Two columns inside a ~713-wide viewport: 32 + 310 + 24 + 310 = 676.
CELL = (310.0, 88.0)
SPACING = (24.0, 18.0)
PADDING = dict(left=32, right=32, top=20, bottom=0)
# The Toy Box variant row's own anchors (author_toybox_layout.py VARIANT_TITLE / VARIANT_DETAIL):
# name bottom-left, detail above it, both inset past the chamfer.
NAME_ANCHORS = ((0.06, 0.08), (0.80, 0.52))
DETAIL_ANCHORS = ((0.06, 0.52), (0.94, 0.90))
NAME_BAND = (16.0, 22.0)
DETAIL_BAND = (11.0, 13.0)


def tmp_doc(fid, go, text, band, align_h, color_a=1.0):
    lo, hi = band
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {TMP_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: {color_a}}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: {text}
  m_isRightToLeft: 0
  m_fontAsset: {{fileID: 11400000, guid: {FONT_GUID}, type: 2}}
  m_sharedMaterial: {{fileID: {FONT_MAT}, guid: {FONT_GUID}, type: 2}}
  m_fontSharedMaterials: []
  m_fontMaterial: {{fileID: 0}}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4294967295
  m_fontColor: {{r: 1, g: 1, b: 1, a: 1}}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {{r: 1, g: 1, b: 1, a: 1}}
    topRight: {{r: 1, g: 1, b: 1, a: 1}}
    bottomLeft: {{r: 1, g: 1, b: 1, a: 1}}
    bottomRight: {{r: 1, g: 1, b: 1, a: 1}}
  m_fontColorGradientPreset: {{fileID: 0}}
  m_spriteAsset: {{fileID: 0}}
  m_tintAllSprites: 0
  m_StyleSheet: {{fileID: 0}}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: {hi}
  m_fontSizeBase: {hi}
  m_fontWeight: 400
  m_enableAutoSizing: 1
  m_fontSizeMin: {lo}
  m_fontSizeMax: {hi}
  m_fontStyle: 0
  m_HorizontalAlignment: {align_h}
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_characterHorizontalScale: 1
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 1
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {{fileID: 0}}
  parentLinkedComponent: {{fileID: 0}}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {{x: 0, y: 0, z: 0, w: 0}}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {{fileID: 0}}
  m_maskOffset: {{x: 0, y: 0, z: 0, w: 0}}
"""


def go_doc(fid, name, components):
    lines = "\n".join(f"  - component: {{fileID: {c}}}" for c in components)
    return f"""--- !u!1 &{fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{lines}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""


def rect_doc(fid, go, father, children, amin, amax, pivot=(0.5, 0.5), size=(0.0, 0.0), pos=(0.0, 0.0)):
    kids = "".join(f"\n  - {{fileID: {c}}}" for c in children) or " []"
    return f"""--- !u!224 &{fid}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: -0, y: -0, z: -0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{kids}
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: {amin[0]}, y: {amin[1]}}}
  m_AnchorMax: {{x: {amax[0]}, y: {amax[1]}}}
  m_AnchoredPosition: {{x: {pos[0]}, y: {pos[1]}}}
  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}
  m_Pivot: {{x: {pivot[0]}, y: {pivot[1]}}}
"""


def cr_doc(fid, go):
    return f"""--- !u!222 &{fid}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
"""


def image_doc(fid, go, sprite_guid, mult, raycast):
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: {raycast}
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: {mult}
"""


def build_prefab(mult):
    parts = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"]
    parts.append(go_doc(GO_ROOT, "MaelstromPoolRow", [TR_ROOT, CR_ROOT, IMG_ROOT, GROUP_ROOT, ENTRY]))
    parts.append(rect_doc(TR_ROOT, GO_ROOT, 0, [TR_RIM, TR_DET, TR_NAME], (0, 0), (0, 0)))
    parts.append(cr_doc(CR_ROOT, GO_ROOT))
    parts.append(image_doc(IMG_ROOT, GO_ROOT, PLATE_GUID, mult, 0))
    parts.append(f"""--- !u!{CANVASGROUP_CLASS} &{GROUP_ROOT}
CanvasGroup:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO_ROOT}}}
  m_Enabled: 1
  m_Alpha: 1
  m_Interactable: 0
  m_BlocksRaycasts: 0
  m_IgnoreParentGroups: 0
""")
    parts.append(f"""--- !u!114 &{ENTRY}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {GO_ROOT}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {ENTRY_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.MaelstromPoolEntry
  icon: {{fileID: 0}}
  nameText: {{fileID: {TMP_NAME}}}
  detailText: {{fileID: {TMP_DET}}}
  lockedText: {{fileID: 0}}
  lockedFormat: Intensity {{0}}
  neverUnlockedText: Not in pool
  detailFormat: TIER {{0}}  -  {{1}}
  lockedAlpha: 0.4
""")
    # rim
    parts.append(go_doc(GO_RIM, "Border", [TR_RIM, CR_RIM, IMG_RIM]))
    parts.append(rect_doc(TR_RIM, GO_RIM, TR_ROOT, [], (0, 0), (1, 1)))
    parts.append(cr_doc(CR_RIM, GO_RIM))
    parts.append(image_doc(IMG_RIM, GO_RIM, RIM_GUID, mult, 0))
    # detail line
    parts.append(go_doc(GO_DET, "Detail", [TR_DET, CR_DET, TMP_DET]))
    parts.append(rect_doc(TR_DET, GO_DET, TR_ROOT, [], DETAIL_ANCHORS[0], DETAIL_ANCHORS[1]))
    parts.append(cr_doc(CR_DET, GO_DET))
    parts.append(tmp_doc(TMP_DET, GO_DET, "TIER 1  -  DOLPHIN", DETAIL_BAND, align_h=1, color_a=0.72))
    # name line
    parts.append(go_doc(GO_NAME, "Name", [TR_NAME, CR_NAME, TMP_NAME]))
    parts.append(rect_doc(TR_NAME, GO_NAME, TR_ROOT, [], NAME_ANCHORS[0], NAME_ANCHORS[1]))
    parts.append(cr_doc(CR_NAME, GO_NAME))
    parts.append(tmp_doc(TMP_NAME, GO_NAME, "THE BENDS", NAME_BAND, align_h=1))
    return "".join(parts)


# ── scene: the grid the rows are laid into ─────────────────────────────────────────────────────
def resolve_multiplier(scene_text):
    refs = re.findall(r"^  m_ReferencePixelsPerUnit: ([\d.]+)$", scene_text, re.M)
    if not refs:
        raise SystemExit("Menu_Main declares no CanvasScaler referencePixelsPerUnit")
    return float(refs[0]) / SLICE_MULTIPLIER_REF


def patch_grid(text):
    """The GridLayoutGroup on the pool list's Content object (found via the list view's own
    rowContainer reference, so a scene re-layout cannot point this at the wrong grid)."""
    m = re.search(r"m_EditorClassIdentifier: Assembly-CSharp::CosmicShore\.UI\.MaelstromPoolListView\n"
                  r"  rowContainer: \{fileID: (\d+)\}", text)
    if not m:
        raise SystemExit("MaelstromPoolListView / rowContainer not found in Menu_Main")
    rect_id = m.group(1)

    rm = re.search(rf"^--- !u!224 &{rect_id}$.*?m_GameObject: \{{fileID: (\d+)\}}", text, re.S | re.M)
    go = rm.group(1)
    gm = re.search(rf"^--- !u!1 &{go}$(.*?)(?=^--- !u!)", text, re.S | re.M)
    comps = re.findall(r"component: \{fileID: (-?\d+)\}", gm.group(1))

    # The scroll content's height was AUTHORED (1351.7 for a grid whose rows add up to 903), so
    # the list has always ended in a screenful of nothing and any change to the cell size makes
    # that worse. A fitter is the fix rather than a re-measured literal: the pool is sixteen modes
    # today and the whole point of the asset is that adding a seventeenth is one edit.
    # Idempotency is keyed on the fitter's OWN fileID, not on its script guid: the guid is on
    # dozens of other objects in this scene, and re-deriving the component list after the insert
    # is what a second run would have to do to see its own work.
    if not re.search(rf"^--- !u!114 &{FITTER_ID}$", text, re.M):
        fid = FITTER_ID
        # Inside the GameObject's OWN body, never "the next m_Layer: after this doc" - gm.end(1)
        # is the END of that body, so searching forward from there lands the component entry on
        # whatever object happens to be serialized next (it landed on AvatarSpace first try).
        body = gm.group(1)
        patched = body.replace("  m_Layer:", f"  - component: {{fileID: {fid}}}\n  m_Layer:", 1)
        if patched == body:
            raise SystemExit("the pool list's Content GameObject has no m_Layer line to anchor on")
        text = text[:gm.start(1)] + patched + text[gm.end(1):]
        doc = f'''--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FITTER_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_HorizontalFit: 0
  m_VerticalFit: 2
'''
        marker = "--- !u!1660057539 &"
        at = text.index(marker)
        text = text[:at] + doc + text[at:]

    for c in comps:
        dm = re.search(rf"^(--- !u!114 &{c}$)(.*?)(?=^--- !u!)", text, re.S | re.M)
        if not dm or "GridLayoutGroup" not in dm.group(2):
            continue
        body = dm.group(2)
        body = re.sub(r"(m_Padding:\n    m_Left: )\d+", rf"\g<1>{PADDING['left']}", body)
        body = re.sub(r"(m_Padding:\n    m_Left: \d+\n    m_Right: )\d+", rf"\g<1>{PADDING['right']}", body)
        body = re.sub(r"(m_Top: )\d+", rf"\g<1>{PADDING['top']}", body, count=1)
        body = re.sub(r"(m_Bottom: )\d+", rf"\g<1>{PADDING['bottom']}", body, count=1)
        body = re.sub(r"m_CellSize: \{x: [\d.-]+, y: [\d.-]+\}",
                      f"m_CellSize: {{x: {CELL[0]}, y: {CELL[1]}}}", body)
        body = re.sub(r"m_Spacing: \{x: [\d.-]+, y: [\d.-]+\}",
                      f"m_Spacing: {{x: {SPACING[0]}, y: {SPACING[1]}}}", body)
        return text[:dm.start(2)] + body + text[dm.end(2):]

    raise SystemExit("the pool list's Content object carries no GridLayoutGroup")


def verify_scene(text):
    """The fitter has to be on the pool list's OWN Content object. Asserted rather than assumed:
    the first cut of the insert anchored on the m_Layer AFTER the matched body and put the
    component on the next object serialized (AvatarSpace), which Unity accepts silently."""
    m = re.search(r"m_EditorClassIdentifier: Assembly-CSharp::CosmicShore\.UI\.MaelstromPoolListView\n"
                  r"  rowContainer: \{fileID: (\d+)\}", text)
    rect_id = m.group(1)
    go = re.search(rf"^--- !u!224 &{rect_id}$.*?m_GameObject: \{{fileID: (\d+)\}}", text, re.S | re.M).group(1)
    body = re.search(rf"^--- !u!1 &{go}$(.*?)(?=^--- !u!)", text, re.S | re.M).group(1)
    if f"component: {{fileID: {FITTER_ID}}}" not in body:
        raise SystemExit(f"the fitter {FITTER_ID} is not on the pool list's Content object")
    owner = re.search(rf"^--- !u!114 &{FITTER_ID}$(.*?)(?=^--- !u!)", text, re.S | re.M).group(1)
    if f"m_GameObject: {{fileID: {go}}}" not in owner:
        raise SystemExit("the fitter document points at a different GameObject than the list uses")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    scene = open(SCENE, errors="ignore").read()
    mult = resolve_multiplier(scene)
    want_prefab = build_prefab(mult)
    want_scene = patch_grid(scene)

    verify_scene(want_scene)

    have_prefab = open(PREFAB, errors="ignore").read()
    drift = []
    if have_prefab != want_prefab:
        drift.append(os.path.relpath(PREFAB, ROOT))
    if scene != want_scene:
        drift.append(os.path.relpath(SCENE, ROOT))

    if args.check:
        if drift:
            print("DRIFT: " + ", ".join(drift))
            return 1
        print("ok: pool row + grid match what this script authors")
        return 0

    if not drift:
        print("already done")
        return 0

    open(PREFAB, "w").write(want_prefab)
    open(SCENE, "w").write(want_scene)
    print(f"wrote {', '.join(drift)} (slice multiplier {mult})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
