#!/usr/bin/env python3
"""Author the Urchin's HUD: icons, the HUD variant, and its wiring on Urchin.prefab.

The Urchin was the only hull in the fleet with a complete ability map (4/4 named, 4/4 level-5
upgrades) and NO HUD at all (URCHIN_BACKLOG U3): no `UrchinHUDVariant.prefab`, `vesselHUDController:
{fileID: 0}` on `Urchin.prefab`, and `UrchinVesselHUDController` / `UrchinVesselHUDView` referenced
by nothing. `VesselController.Initialize` logged "HUD will not function" on every spawn and the
pilot flew with no ability row, no element flowers and no control chips.

This writes three things, each the way the fleet's sibling hulls already carry them:

1. PLACEHOLDER icon sprites (128 px, white silhouettes, the house icon language of
   `author_manta_icon_placeholders.py` / `author_butterfly_icon_placeholders.py`, whose raster
   primitives this imports) into `Assets/_Graphics/Icons/AbilityIcons/Urchin/`, for the art pass
   to replace 1:1. Each names the ACT:

     Charge  Chain Spikes      a spiked ball - the volley the trigger releases
     Mass    Trail Rider       a hull riding a ribbon of prism blocks
     Space   Track Projector   a hull with a railed track thrown out ahead of it
     Time    Slip              a solid hull pulling out of two ghosted afterimages

2. `UrchinHUDVariant.prefab` - a Prefab VARIANT of `VesselHUDPrefab.prefab` (never a copy: a
   copy severs propagation, Docs/GAMECANVAS.md). It removes the base root's dead missing-script
   component (every variant in the fleet does), adds `UrchinVesselHUDView` on the root, and lays
   the minimum the ability lockup needs - per element a bare host RectTransform with one icon
   Image - plus two gauges, each under the card it reports on (the lockup re-homes gauges, but
   authoring one under the wrong button is the Squirrel/Scarab trap):

     Charge card  gauge = ammoFill         spike ammo: the volley spends it, the ride refills it
     Mass card    gauge = ridingIndicator  binary: on the ribbon or not (fill 0 or 1)

   Each gauge is bound TWICE and both are required - the view WRITES through its own field, the
   lockup ADOPTS through the binding's `gauge`; bind only the field and RetireLegacyChrome
   switches a correctly-driven meter off (the Butterfly's Mass-meter finding). The Space card's
   recharge is not an Image at all: the controller pushes `SetAbilityCooldown(Space, ...)` from
   the track executor, and the lockup draws the fleet's veil. Position, pitch and icon scale are
   all re-derived by the lockup from `Resources/AbilityLockupStyle`; icons are authored at
   exactly `iconBoxSize` so the derived scale is 1.

3. `Urchin.prefab` - a `ShipHUDContainer` (overlay Canvas + scaler + raycaster, verbatim the
   Butterfly's) holding a nested instance of the variant, an `UrchinVesselHUDController` on the
   vessel root bound to the instance's view and to the vessel's OWN `UrchinTrackActionExecutor`,
   and `VesselStatus.vesselHUDController` pointed at it. No modification on the nested instance
   touches the view or the icons, so the variant stays the single source of truth for the row
   (the Squirrel's Time-icon override trap).

Idempotent and deterministic: every fileID and guid is fixed, nested-object fileIDs are Unity's
own `(instance XOR source) & 0x7FFF...` rule, and the vessel-prefab edit strips its own previous
output before re-applying, so a re-run is byte-identical.

    python3 Tools/Build/author_urchin_hud.py           # write / repair
    python3 Tools/Build/author_urchin_hud.py --check   # exit 1 on any drift
"""
import hashlib
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_butterfly_icon_placeholders import (  # noqa: E402  (shared raster + meta schema)
    capsule, circle, coverage, folder_meta, render, ring, sprite_meta, tri)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is not the project root: {ROOT}"
CHECK = "--check" in sys.argv
MASK = 0x7FFFFFFFFFFFFFFF


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/UrchinHUD/{name}".encode()).hexdigest()


# ---------------------------------------------------------------------------------------------
# 1. Icons
# ---------------------------------------------------------------------------------------------

ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Urchin"


def chain_spikes(px, py):
    """A spiked ball: the volley the right trigger releases (and charges into a burst)."""
    if circle(px, py, 64, 64, 20.0):
        return True
    for k in range(10):
        a = math.radians(90.0 + 36.0 * k)
        ca, sa = math.cos(a), math.sin(a)
        tip = (64 + 58 * ca, 64 + 58 * sa)
        # base on the disc, 7 units either side of the spoke
        bx, by = 64 + 16 * ca, 64 + 16 * sa
        l = (bx - 7 * sa, by + 7 * ca)
        r = (bx + 7 * sa, by - 7 * ca)
        if tri(px, py, l[0], l[1], r[0], r[1], tip[0], tip[1]):
            return True
    return False


def trail_rider(px, py):
    """A hull riding a ribbon of prism blocks, with the speed of the ride behind it."""
    for cx in (14, 38, 62, 86, 110):
        if abs(px - cx) <= 10 and abs(py - 40) <= 7:
            return True                                # the ribbon's blocks
    if circle(px, py, 70, 68, 14.0):
        return True                                    # the rider, on the ribbon
    for y, x0 in ((60, 22), (70, 14), (80, 26)):
        if capsule(px, py, x0, y, 50, y, 3.0):
            return True                                # the ride's speed
    return False


def track_projector(px, py):
    """A railed track thrown out ahead of the hull - two converging rails and their ties."""
    if circle(px, py, 64, 16, 10.0):
        return True                                    # the hull
    if capsule(px, py, 34, 34, 56, 116, 4.0) or capsule(px, py, 94, 34, 72, 116, 4.0):
        return True                                    # the rails, into the distance
    for y in (50, 76, 100):
        t = (y - 34) / 82.0
        if capsule(px, py, 34 + 22 * t, y, 94 - 22 * t, y, 3.0):
            return True                                # the ties
    return False


def slip(px, py):
    """A solid hull pulling out of two ghosted afterimages of itself."""
    if circle(px, py, 92, 64, 20.0):
        return True                                    # the hull, here
    if ring(px, py, 56, 64, 17.0, 3.5):
        return True                                    # the ghost, a moment ago
    if ring(px, py, 26, 64, 13.0, 2.5):
        return True                                    # the ghost, before that
    return False


# element -> (slot name, sprite file, raster)
SLOTS = [
    (1, "Charge", "Urchin_ChainSpikes.png", chain_spikes),
    (2, "Mass", "Urchin_TrailRider.png", trail_rider),
    (3, "Space", "Urchin_TrackProjector.png", track_projector),
    (4, "Time", "Urchin_Slip.png", slip),
]


def icon_writes() -> dict:
    for _, _, png, fn in SLOTS:
        c = coverage(fn)
        assert 0.08 <= c <= 0.45, f"{png}: coverage {c:.3f} outside the readable band"
    out = {ICON_DIR + ".meta": folder_meta(guid_for("Urchin.folder"))}
    for _, _, png, fn in SLOTS:
        out[f"{ICON_DIR}/{png}"] = render(fn)
        out[f"{ICON_DIR}/{png}.meta"] = sprite_meta(guid_for(png))
    return out


# ---------------------------------------------------------------------------------------------
# 2. The HUD variant
# ---------------------------------------------------------------------------------------------

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/UrchinHUDVariant.prefab"
VARIANT_GUID = guid_for("UrchinHUDVariant.prefab")

BASE = "Assets/_Prefabs/UI Elements/VesselHUD/VesselHUDPrefab.prefab"
BASE_GUID = "bc09c07dcb0724842a327c887609f156"
BASE_ROOT_GO = 3323122711064239023
BASE_ROOT_RT = 6557045720525393826
BASE_MISSING_SCRIPT = 5582900316631961953   # the deleted ShipHUDView-era component
IMAGE_SCRIPT_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
VIEW_SCRIPT = "Assets/_Scripts/UI/View/UrchinVesselHUDView.cs"
CONTROLLER_SCRIPT = "Assets/_Scripts/UI/Controller/UrchinVesselHUDController.cs"

ICON_BOX = 60   # Resources/AbilityLockupStyle.iconBoxSize -> derived icon scale exactly 1

V_INSTANCE = 7781000000000000001
V_ROOT_GO = (V_INSTANCE ^ BASE_ROOT_GO) & MASK
V_ROOT_RT = (V_INSTANCE ^ BASE_ROOT_RT) & MASK
V_VIEW = 7781000000000000002
V_BASE = 7781000000000001000
AMMO = dict(go=V_BASE + 200, rt=V_BASE + 201, cr=V_BASE + 202, img=V_BASE + 203)
RIDE = dict(go=V_BASE + 210, rt=V_BASE + 211, cr=V_BASE + 212, img=V_BASE + 213)
GAUGE_ON = {"Charge": ("SpikeAmmoGauge", AMMO), "Mass": ("RidingIndicator", RIDE)}

# Colours are the lockup's own vocabulary (Resources/AbilityLockupStyle), not new hues:
# a gauge fills in gaugeFillColor; a FULL ammo meter brightens toward cooldownReadyFlashColor,
# which is what "ready" already means on every card; riding fades a gaugeFillColor plate in.
GAUGE_FILL = "{r: 0.22, g: 0.51, b: 1, a: 0.55}"
READY = "{r: 0.96, g: 0.96, b: 1, a: 0.5}"
GAUGE_CLEAR = "{r: 0.22, g: 0.51, b: 1, a: 0}"


def script_guid(cs: str) -> str:
    text = open(os.path.join(ROOT, cs + ".meta"), encoding="utf-8").read()
    return re.search(r"^guid: ([0-9a-f]{32})$", text, re.M).group(1)


def host_ids(i):
    b = V_BASE + 100 + i * 10
    return dict(host_go=b, host_rt=b + 1, icon_go=b + 2, icon_rt=b + 3, icon_cr=b + 4,
                icon_img=b + 5)


def rect(fid, go, parent, children, size):
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
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: {size}, y: {size}}}
  m_Pivot: {{x: 0.5, y: 0.5}}
"""


def game_object(fid, name, components, layer=5):
    comps = "".join(f"\n  - component: {{fileID: {c}}}" for c in components)
    return f"""--- !u!1 &{fid}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:{comps}
  m_Layer: {layer}
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


def image(fid, go, sprite_guid, fill=False):
    # A gauge is authored bare: the lockup's AdoptGauge re-homes it, gives it the plain sprite
    # its stencil needs and sets the fill mode itself.
    sprite = "{fileID: 0}" if sprite_guid is None else \
        f"{{fileID: 21300000, guid: {sprite_guid}, type: 3}}"
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
  m_FillAmount: {0 if fill else 1}
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""


def rt_modifications(target_fid, guid, name_target_fid, name):
    """The modification block Unity writes for a freshly instanced UI prefab: its name plus the
    root RectTransform's full pose. Identical to the Butterfly's, target ids aside."""
    def mod(fid, path, value):
        return (f"    - target: {{fileID: {fid}, guid: {guid},\n        type: 3}}\n"
                f"      propertyPath: {path}\n"
                f"      value: {value}\n"
                f"      objectReference: {{fileID: 0}}\n")
    out = mod(name_target_fid, "m_Name", name)
    for path, value in (("m_Pivot.x", 0.5), ("m_Pivot.y", 0.5), ("m_AnchorMax.x", 0.5),
                        ("m_AnchorMax.y", 0.5), ("m_AnchorMin.x", 0.5), ("m_AnchorMin.y", 0.5),
                        ("m_SizeDelta.x", 100), ("m_SizeDelta.y", 100),
                        ("m_LocalPosition.x", 0), ("m_LocalPosition.y", 0),
                        ("m_LocalPosition.z", 0), ("m_LocalRotation.w", 1),
                        ("m_LocalRotation.x", 0), ("m_LocalRotation.y", 0),
                        ("m_LocalRotation.z", 0), ("m_AnchoredPosition.x", 0),
                        ("m_AnchoredPosition.y", 0), ("m_LocalEulerAnglesHint.x", 0),
                        ("m_LocalEulerAnglesHint.y", 0), ("m_LocalEulerAnglesHint.z", 0)):
        out += mod(target_fid, path, value)
    return out


def variant_text() -> str:
    for fid in (BASE_ROOT_GO, BASE_ROOT_RT, BASE_MISSING_SCRIPT):
        assert re.search(rf"^--- !u!\d+ &{fid}$",
                         open(os.path.join(ROOT, BASE), encoding="utf-8").read(), re.M), \
            f"base HUD prefab no longer carries fileID {fid}"

    added_go, bindings, blocks = [], [], []
    for i, (element, name, png, _) in enumerate(SLOTS):
        f = host_ids(i)
        gauge = GAUGE_ON.get(name)
        children = [f["icon_rt"]] + ([gauge[1]["rt"]] if gauge else [])
        blocks.append(game_object(f["host_go"], f"{name}Button", [f["host_rt"]]))
        blocks.append(rect(f["host_rt"], f["host_go"], V_ROOT_RT, children, ICON_BOX))
        blocks.append(game_object(f["icon_go"], f"{name}Icon",
                                  [f["icon_rt"], f["icon_cr"], f["icon_img"]]))
        blocks.append(rect(f["icon_rt"], f["icon_go"], f["host_rt"], [], ICON_BOX))
        blocks.append(canvas_renderer(f["icon_cr"], f["icon_go"]))
        blocks.append(image(f["icon_img"], f["icon_go"], guid_for(png)))
        if gauge:
            gname, g = gauge
            blocks.append(game_object(g["go"], gname, [g["rt"], g["cr"], g["img"]]))
            blocks.append(rect(g["rt"], g["go"], f["host_rt"], [], ICON_BOX))
            blocks.append(canvas_renderer(g["cr"], g["go"]))
            blocks.append(image(g["img"], g["go"], None, fill=True))
        added_go.append(
            f"    - targetCorrespondingSourceObject: {{fileID: {BASE_ROOT_RT}, guid: {BASE_GUID},\n"
            f"        type: 3}}\n"
            f"      insertIndex: -1\n"
            f"      addedObject: {{fileID: {f['host_rt']}}}\n")
        bindings.append(f"  - element: {element}\n"
                        f"    icon: {{fileID: {f['icon_img']}}}\n"
                        f"    upgradedSprite: {{fileID: 0}}\n"
                        f"    gauge: {{fileID: {gauge[1]['img'] if gauge else 0}}}\n")

    head = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1001 &{V_INSTANCE}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
{rt_modifications(BASE_ROOT_RT, BASE_GUID, BASE_ROOT_GO, "UrchinHUDVariant")}    m_RemovedComponents:
    - {{fileID: {BASE_MISSING_SCRIPT}, guid: {BASE_GUID}, type: 3}}
    m_RemovedGameObjects: []
    m_AddedGameObjects:
{"".join(added_go)}    m_AddedComponents:
    - targetCorrespondingSourceObject: {{fileID: {BASE_ROOT_GO}, guid: {BASE_GUID},
        type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {V_VIEW}}}
  m_SourcePrefab: {{fileID: 100100000, guid: {BASE_GUID}, type: 3}}
--- !u!1 &{V_ROOT_GO} stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: {BASE_ROOT_GO}, guid: {BASE_GUID},
    type: 3}}
  m_PrefabInstance: {{fileID: {V_INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!224 &{V_ROOT_RT} stripped
RectTransform:
  m_CorrespondingSourceObject: {{fileID: {BASE_ROOT_RT}, guid: {BASE_GUID},
    type: 3}}
  m_PrefabInstance: {{fileID: {V_INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!114 &{V_VIEW}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {V_ROOT_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid(VIEW_SCRIPT)}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.UrchinVesselHUDView
  highlights: []
  abilityIcons:
{"".join(bindings)}  coreAbilities: []
  omniAbilitySprite: {{fileID: 0}}
  upgradeHighlightScale: 1.15
  upgradePunchScale: 1.35
  upgradePunchDuration: 0.35
  abilityLockups: {{fileID: 0}}
  animSettings: {{fileID: 0}}
  omniCollectPunchScale: 1.35
  omniCollectDuration: 0.4
  omniCollectWhiteMix: 0.75
  ammoFill: {{fileID: {AMMO['img']}}}
  ammoNormalColor: {GAUGE_FILL}
  ammoFullColor: {READY}
  ridingIndicator: {{fileID: {RIDE['img']}}}
  ridingOffColor: {GAUGE_CLEAR}
  ridingOnColor: {GAUGE_FILL}
  ridingBlendSeconds: 0.15
"""
    return head + "".join(blocks)


def variant_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {VARIANT_GUID}\n"
            "PrefabImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


# ---------------------------------------------------------------------------------------------
# 3. Urchin.prefab
# ---------------------------------------------------------------------------------------------

VESSEL = "Assets/_Prefabs/Spacevessels/Urchin.prefab"
ROOT_GO = 6417075533431866457
ROOT_TF = 5928382658856804067
TRACK_EXECUTOR = 7770000000000000052      # UrchinTrackActionExecutor on VesselActions/Track

U_BASE = 7791000000000000000
SHC_GO, SHC_RT, SHC_CANVAS, SHC_SCALER, SHC_RAYCASTER = (U_BASE + 1, U_BASE + 2, U_BASE + 3,
                                                         U_BASE + 4, U_BASE + 5)
U_INSTANCE = U_BASE + 10
U_CONTROLLER = U_BASE + 20
U_HUD_RT = (U_INSTANCE ^ V_ROOT_RT) & MASK       # the nested variant's root, in Urchin.prefab
U_HUD_VIEW = (U_INSTANCE ^ V_VIEW) & MASK        # the nested variant's view, in Urchin.prefab
OWNED_DOCS = {SHC_GO, SHC_RT, SHC_CANVAS, SHC_SCALER, SHC_RAYCASTER, U_INSTANCE, U_CONTROLLER,
              U_HUD_RT, U_HUD_VIEW}

COMPONENT_LINE = f"  - component: {{fileID: {U_CONTROLLER}}}\n"
CHILD_LINE = f"  - {{fileID: {SHC_RT}}}\n"
HUD_FIELD_OFF = "  vesselHUDController: {fileID: 0}\n"
HUD_FIELD_ON = f"  vesselHUDController: {{fileID: {U_CONTROLLER}}}\n"


def vessel_blocks() -> str:
    track = script_guid("Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/"
                        "UrchinTrackActionExecutor.cs")
    assert track == "377b57f5def70b3b58d3c6fe5393ae03", "track executor script guid moved"
    return f"""--- !u!1 &{SHC_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SHC_RT}}}
  - component: {{fileID: {SHC_CANVAS}}}
  - component: {{fileID: {SHC_SCALER}}}
  - component: {{fileID: {SHC_RAYCASTER}}}
  m_Layer: 0
  m_Name: ShipHUDContainer
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{SHC_RT}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHC_GO}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0, y: 0, z: 0}}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {{fileID: {U_HUD_RT}}}
  m_Father: {{fileID: {ROOT_TF}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 0}}
  m_AnchorMax: {{x: 0, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0, y: 0}}
--- !u!223 &{SHC_CANVAS}
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHC_GO}}}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {{fileID: 0}}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 0
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 0
  m_TargetDisplay: 0
--- !u!114 &{SHC_SCALER}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHC_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 0cd44c1031e13a943bb63640046fad76, type: 3}}
  m_Name:
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.CanvasScaler
  m_UiScaleMode: 0
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {{x: 800, y: 600}}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 0
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
--- !u!114 &{SHC_RAYCASTER}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHC_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: dc42784cf147c0c48a680349fa168899, type: 3}}
  m_Name:
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.GraphicRaycaster
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
  m_BlockingMask:
    serializedVersion: 2
    m_Bits: 4294967295
--- !u!1001 &{U_INSTANCE}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: {SHC_RT}}}
    m_Modifications:
{rt_modifications(V_ROOT_RT, VARIANT_GUID, V_ROOT_GO, "UrchinHUDVariant")}    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {VARIANT_GUID}, type: 3}}
--- !u!224 &{U_HUD_RT} stripped
RectTransform:
  m_CorrespondingSourceObject: {{fileID: {V_ROOT_RT}, guid: {VARIANT_GUID},
    type: 3}}
  m_PrefabInstance: {{fileID: {U_INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!114 &{U_HUD_VIEW} stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: {V_VIEW}, guid: {VARIANT_GUID},
    type: 3}}
  m_PrefabInstance: {{fileID: {U_INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid(VIEW_SCRIPT)}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.UrchinVesselHUDView
--- !u!114 &{U_CONTROLLER}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ROOT_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid(CONTROLLER_SCRIPT)}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.UrchinVesselHUDController
  baseView: {{fileID: {U_HUD_VIEW}}}
  _iconSetSwitcher: {{fileID: 0}}
  view: {{fileID: {U_HUD_VIEW}}}
  ammoIndex: 0
  trackExecutor: {{fileID: {TRACK_EXECUTOR}}}
"""


DOC_SPLIT = re.compile(r"(?m)^(?=--- !u!)")
DOC_HEAD = re.compile(r"^--- !u!\d+ &(\d+)")


def doc_ids(text):
    return [int(m.group(1)) for m in re.finditer(r"(?m)^--- !u!\d+ &(\d+)", text)]


def strip_vessel(text: str) -> str:
    """Remove every trace of this tool's previous output, leaving the prefab as it was."""
    parts = DOC_SPLIT.split(text)
    kept = [p for p in parts if not ((m := DOC_HEAD.match(p)) and int(m.group(1)) in OWNED_DOCS)]
    out = "".join(kept)
    out = out.replace(COMPONENT_LINE, "").replace(CHILD_LINE, "")
    out = out.replace(HUD_FIELD_ON, HUD_FIELD_OFF)
    return out


def apply_vessel(clean: str) -> str:
    collide = OWNED_DOCS & set(doc_ids(clean))
    assert not collide, f"Urchin.prefab already uses fileIDs this tool owns: {sorted(collide)}"

    # The root GameObject's component list, the root Transform's children, and the status field.
    go_doc = re.search(rf"(?ms)^--- !u!1 &{ROOT_GO}\n.*?^  m_Layer:", clean)
    assert go_doc, "Urchin root GameObject not found"
    comp_end = clean.index("  m_Layer:", go_doc.start())
    out = clean[:comp_end] + COMPONENT_LINE + clean[comp_end:]

    tf_doc = re.search(rf"(?ms)^--- !u!4 &{ROOT_TF}\n.*?^  m_Father: \{{fileID: 0\}}\n", out)
    assert tf_doc, "Urchin root Transform not found"
    father = out.index("  m_Father: {fileID: 0}\n", tf_doc.start())
    out = out[:father] + CHILD_LINE + out[father:]

    assert out.count(HUD_FIELD_OFF) == 1, "expected exactly one unset vesselHUDController"
    out = out.replace(HUD_FIELD_OFF, HUD_FIELD_ON)

    return out.rstrip("\n") + "\n" + vessel_blocks()


# ---------------------------------------------------------------------------------------------

def main() -> int:
    writes = icon_writes()
    writes[VARIANT] = variant_text()
    writes[VARIANT + ".meta"] = variant_meta()

    vessel_path = os.path.join(ROOT, VESSEL)
    have_vessel = open(vessel_path, encoding="utf-8", newline="").read()
    writes[VESSEL] = apply_vessel(strip_vessel(have_vessel))

    drift = []
    for rel, want in writes.items():
        path = os.path.join(ROOT, rel)
        binary = isinstance(want, bytes)
        have = None
        if os.path.exists(path):
            with open(path, "rb" if binary else "r", **({} if binary else
                                                        {"encoding": "utf-8", "newline": ""})) as f:
                have = f.read()
        if have != want:
            drift.append(rel)
            if not CHECK:
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "wb" if binary else "w", **({} if binary else
                                                            {"encoding": "utf-8",
                                                             "newline": ""})) as f:
                    f.write(want)

    if CHECK:
        if drift:
            print("DRIFT:\n  " + "\n  ".join(drift))
            return 1
        print("check clean")
        return 0

    print(f"wrote {len(drift)} file(s)" + "".join(f"\n  {d}" for d in drift))
    for element, name, png, fn in SLOTS:
        print(f"  {name:6} {png:28} guid {guid_for(png)}  coverage {coverage(fn):.3f}")
    print(f"  UrchinHUDVariant guid {VARIANT_GUID}; controller fileID {U_CONTROLLER}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
