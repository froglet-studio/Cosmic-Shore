#!/usr/bin/env python3
"""Author the Butterfly's four-icon ability row into ButterflyHUDVariant.prefab.

The Butterfly shipped with `abilityIcons: []` and `wingEnergyGauge: {fileID: 0}` -- a
completely written view and controller with nothing bound to them. So all four lockup cards
rendered LOCKED and the Fold's recharge veil swept over a bare plate: correct, driven every
frame, and unreadable. That is the Serpent's report one vessel over, and the rule it left
behind is that an indicator whose only rendering is its VALUE has no rendering at its
extremes.

This lays the minimum the lockup needs and nothing more, because the lockup OWNS the row:
per element a bare host RectTransform with one Image child, plus one Image for the wing
meter. Position, pitch, cell size, host scale and icon scale are all re-derived at build
time from Resources/AbilityLockupStyle, so nothing authored here is a layout decision --
the icons are authored at exactly `iconBoxSize` so the derived scale is 1, and would still
be correct at any other size.

It writes the HUD VARIANT rather than Butterfly.prefab: the ButterflyHUDView component is an
added component on the variant, so the bindings and the objects they point at live together
and the vessel prefab needs no instance override. An override there is the Squirrel's
Time-icon trap -- a value on the variant that has been dead for as long as the override has
existed.

Idempotent, deterministic fileIDs, --check.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is not the project root: {ROOT}"
CHECK = "--check" in sys.argv

PREFAB = "Assets/_Prefabs/UI Elements/VesselHUD/ButterflyHUDVariant.prefab"
ICON_META_DIR = "Assets/_Graphics/Icons/AbilityIcons/Butterfly"

# The base HUD prefab this variant derives from, and the two fileIDs in it the variant
# already addresses (its root GameObject and that root's RectTransform).
BASE_GUID = "bc09c07dcb0724842a327c887609f156"
BASE_ROOT_GO = "3323122711064239023"
BASE_ROOT_RT = "6557045720525393826"
IMAGE_SCRIPT_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"

# Resources/AbilityLockupStyle.iconBoxSize. Authoring the icon at exactly this makes the
# lockup's derived scale (iconBoxSize / authored size) exactly 1; it is not a layout
# decision, and any other size would be re-derived to the same drawn result.
ICON_BOX = 60

# element value -> (slot name, icon sprite file). Charge/Mass/Space/Time is
# VesselHUDView.AbilityDisplayOrder and the order the row is read in; the list is sorted
# into it by OnValidate anyway, but authoring it right means never relying on that.
SLOTS = [
    (1, "Charge", "Butterfly_ScaleDust.png"),
    (2, "Mass",   "Butterfly_SpreadWings.png"),
    (3, "Space",  "Butterfly_Wingreach.png"),
    (4, "Time",   "Butterfly_Fold.png"),
]

BASE_ID = 7700000000000001000
STRIPPED_ROOT_RT = BASE_ID
GAUGE_GO, GAUGE_RT, GAUGE_CR, GAUGE_IMG = (BASE_ID + 200, BASE_ID + 201,
                                           BASE_ID + 202, BASE_ID + 203)


def ids(i):
    b = BASE_ID + 100 + i * 10
    return dict(host_go=b, host_rt=b + 1, icon_go=b + 2,
                icon_rt=b + 3, icon_cr=b + 4, icon_img=b + 5)


def sprite_guid(png: str) -> str:
    path = os.path.join(ROOT, ICON_META_DIR, png + ".meta")
    assert os.path.exists(path), (
        f"missing {path} - run Tools/Build/author_butterfly_icon_placeholders.py first")
    m = re.search(r"^guid: ([0-9a-f]{32})$", open(path, encoding="utf-8").read(), re.M)
    assert m, f"no guid in {path}"
    return m.group(1)


def rect(fid, go, parent, children, size, pos=(0.0, 0.0)):
    kids = "".join(f"\n  - {{fileID: {c}}}" for c in children) or " []"
    return f"""--- !u!224 &{fid}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{kids}
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0.5, y: 0.5}}
  m_AnchorMax: {{x: 0.5, y: 0.5}}
  m_AnchoredPosition: {{x: {pos[0]}, y: {pos[1]}}}
  m_SizeDelta: {{x: {size[0]}, y: {size[1]}}}
  m_Pivot: {{x: 0.5, y: 0.5}}
"""


def game_object(fid, name, components):
    comps = "".join(f"\n  - component: {{fileID: {c}}}" for c in components)
    return f"""--- !u!1 &{fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:{comps}
  m_Layer: 5
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""


def canvas_renderer(fid, go):
    return f"""--- !u!222 &{fid}
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_CullTransparentMesh: 1
"""


def image(fid, go, guid, fill=False):
    # A gauge is authored bare: AdoptGauge re-homes it, gives it the plain sprite the stencil
    # needs, and sets the fill mode itself. Authoring a sprite on it would be a second
    # opinion about a shape the trapezoid clip already owns.
    sprite = "{fileID: 0}" if guid is None else f"{{fileID: 21300000, guid: {guid}, type: 3}}"
    return f"""--- !u!114 &{fid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {IMAGE_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {sprite}
  m_Type: {3 if fill else 0}
  m_PreserveAspect: {0 if fill else 1}
  m_FillCenter: 1
  m_FillMethod: {1 if fill else 4}
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""


def stripped_root_rt(prefab_instance):
    return f"""--- !u!224 &{STRIPPED_ROOT_RT} stripped
RectTransform:
  m_CorrespondingSourceObject: {{fileID: {BASE_ROOT_RT}, guid: {BASE_GUID}, type: 3}}
  m_PrefabInstance: {{fileID: {prefab_instance}}}
  m_PrefabAsset: {{fileID: 0}}
"""


def build(src: str) -> str:
    m = re.search(r"^--- !u!1001 &(\d+)$", src, re.M)
    assert m, "no PrefabInstance in the variant"
    prefab_instance = m.group(1)

    blocks = [stripped_root_rt(prefab_instance)]
    added, bindings = [], []

    for i, (element, name, png) in enumerate(SLOTS):
        f = ids(i)
        gauge_child = [GAUGE_RT] if name == "Mass" else []
        blocks.append(game_object(f["host_go"], f"{name}Button", [f["host_rt"]]))
        blocks.append(rect(f["host_rt"], f["host_go"], STRIPPED_ROOT_RT,
                           [f["icon_rt"]] + gauge_child, (ICON_BOX, ICON_BOX)))
        blocks.append(game_object(f["icon_go"], f"{name}Icon",
                                  [f["icon_rt"], f["icon_cr"], f["icon_img"]]))
        blocks.append(rect(f["icon_rt"], f["icon_go"], f["host_rt"], [], (ICON_BOX, ICON_BOX)))
        blocks.append(canvas_renderer(f["icon_cr"], f["icon_go"]))
        blocks.append(image(f["icon_img"], f["icon_go"], sprite_guid(png)))

        if name == "Mass":
            blocks.append(game_object(GAUGE_GO, "WingEnergyGauge",
                                      [GAUGE_RT, GAUGE_CR, GAUGE_IMG]))
            blocks.append(rect(GAUGE_RT, GAUGE_GO, f["host_rt"], [], (ICON_BOX, ICON_BOX)))
            blocks.append(canvas_renderer(GAUGE_CR, GAUGE_GO))
            blocks.append(image(GAUGE_IMG, GAUGE_GO, None, fill=True))

        # Only the HOSTS are added GameObjects at root level; their children travel with them.
        added.append(f"""    - targetCorrespondingSourceObject: {{fileID: {BASE_ROOT_RT}, guid: {BASE_GUID},
        type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {f["host_rt"]}}}""")
        # The BINDING's gauge and the view's own wingEnergyGauge are the same Image and both
        # are required: the view WRITES fillAmount through its field, and the lockup ADOPTS
        # the meter through the binding. Bind only the field and the lockup never claims it -
        # so RetireLegacyChrome switches it off as an unrecognised child of the host, and a
        # correctly-driven meter draws nothing.
        gauge_id = GAUGE_IMG if name == "Mass" else 0
        bindings.append(f"""  - element: {element}
    icon: {{fileID: {f["icon_img"]}}}
    upgradedSprite: {{fileID: 0}}
    gauge: {{fileID: {gauge_id}}}""")

    out = src
    assert "    m_AddedGameObjects: []\n" in out, "variant already carries added GameObjects"
    out = out.replace("    m_AddedGameObjects: []\n",
                      "    m_AddedGameObjects:\n" + "\n".join(added) + "\n", 1)

    assert "  abilityIcons: []\n" in out, "view already binds ability icons"
    out = out.replace("  abilityIcons: []\n",
                      "  abilityIcons:\n" + "\n".join(bindings) + "\n", 1)

    assert "  wingEnergyGauge: {fileID: 0}\n" in out, "view already binds a wing gauge"
    out = out.replace("  wingEnergyGauge: {fileID: 0}\n",
                      f"  wingEnergyGauge: {{fileID: {GAUGE_IMG}}}\n", 1)

    return out.rstrip("\n") + "\n" + "".join(blocks)


def main() -> int:
    path = os.path.join(ROOT, PREFAB)
    have = open(path, encoding="utf-8").read()

    # Idempotent by construction: once the row is in, the three anchors are gone and there is
    # nothing left to author. A re-run then has to confirm the shipped file rather than
    # rebuild it, so the anchors' absence IS the clean answer.
    if "  abilityIcons: []\n" not in have:
        need = [f'&{ids(i)["icon_img"]}' for i in range(len(SLOTS))] + [f'&{GAUGE_IMG}']
        missing = [n for n in need if n not in have]
        if missing:
            print("DRIFT: the row is partly authored - missing " + ", ".join(missing))
            return 1
        print("check clean" if CHECK else "already authored - nothing to write")
        return 0

    want = build(have)
    if CHECK:
        print(f"DRIFT: {PREFAB}")
        return 1
    open(path, "w", encoding="utf-8").write(want)
    print(f"wrote {PREFAB}")
    for i, (_, name, png) in enumerate(SLOTS):
        print(f"  {name}Icon -> {png} (Image {ids(i)['icon_img']})")
    print(f"  WingEnergyGauge -> Image {GAUGE_IMG}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
