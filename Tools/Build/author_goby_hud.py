#!/usr/bin/env python3
"""Author the Goby & Pistol's own HUD: each seat's icons, the HUD variant, and its swap onto Goby.prefab.

Garrett, 2026-10-24: "give both of its seats separate UI ability icons, glyphs, etc." The Goby was cloned from the
Squirrel and still drew the Squirrel's HUD (SquirrelHUDVariant, SquirrelVesselHUDController: the drift card, the
boost fill, the steal ring, all reading a kit the Goby no longer has). This writes, the way the Urchin's HUD was
written (author_urchin_hud.py, whose YAML pieces this imports):

1. PLACEHOLDER icons (128 px white silhouettes, the house icon language) into
   `Assets/_Graphics/Icons/AbilityIcons/Goby/`, FOUR PER SEAT, for the art pass to replace 1:1. The motifs are the
   seats' phone buttons' (touch_glyph_art.py), so a card and its button read as one control:

     card     flying the Goby                         flying the Pistol
     Charge   Goby_Lock     brackets round a target    Pistol_Snap   a vortex ring leaving the claw
     Mass     Goby_Perch    the goby over its pad+den  Pistol_Den    the den, and the way back to it
     Space    Goby_Undertow the plane, mass pulled in  Pistol_Ghost  its outline, dashed
     Time     Goby_Dart     the dart and its speed     Pistol_Dive   the dive down a hill

2. `GobyHUDVariant.prefab`, a Prefab VARIANT of `VesselHUDPrefab.prefab` (never a copy): the base root's dead
   missing-script component removed, `GobyPistolHUDView` on the root with BOTH seats' icons per card, and per element
   a bare host + icon (authored at the lockup's icon box, so the derived scale is 1), plus two gauges, each under the
   card it reports on and bound twice (the view writes, the lockup adopts):

     Time card   gauge = timeGauge    the Goby's dart charge, or the Pistol's speed
     Space card  gauge = spaceGauge   how far the undertow reaches (both seats)

3. `Goby.prefab` (owned by author_goby_assets.py, which calls `swap_hud` from its retrofit): the Squirrel's HUD
   instance and everything it added are removed, a nested instance of the Goby's variant goes in the same
   ShipHUDContainer, and the hull's HUD controller (the same fileID, so `vesselHUDController` and the root's component
   list stay) becomes `GobyPistolHUDController`, bound to the instance's view and to the hull's GobyPistolDuo. The
   ElementalBarsController's reference into the old HUD is cleared (it finds the new HUD's bars itself).

Idempotent and deterministic (fixed fileIDs and guids; the swap strips its own previous output first).

    python3 Tools/Build/author_goby_hud.py             # write the icons and the variant (the prefab: author_goby_assets.py)
    python3 Tools/Build/author_goby_hud.py --check     # exit 1 on any drift
    python3 Tools/Build/author_goby_hud.py --self-test # the prefab checks fire on broken copies
"""
import hashlib
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_butterfly_icon_placeholders import (  # noqa: E402  (shared raster + meta schema)
    capsule, circle, coverage, ellipse, folder_meta, render, ring, sprite_meta, tri)
from author_urchin_hud import (  # noqa: E402  (the HUD variant's YAML pieces)
    BASE, BASE_GUID, BASE_MISSING_SCRIPT, BASE_ROOT_GO, BASE_ROOT_RT, ICON_BOX, MASK,
    canvas_renderer, game_object, image, rect, rt_modifications, script_guid)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT is not the project root: {ROOT}"


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/GobyHUD/{name}".encode()).hexdigest()


# ---------------------------------------------------------------------------------------------
# 1. Icons (math coords, y up, canvas 0..128)
# ---------------------------------------------------------------------------------------------

ICON_DIR = "Assets/_Graphics/Icons/AbilityIcons/Goby"


def box(px, py, cx, cy, hx, hy):
    return abs(px - cx) <= hx and abs(py - cy) <= hy


def goby_lock(px, py):
    """Four corner brackets round a target: the Goby's lock (its phone button's motif)."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = 64 + sx * 42, 64 + sy * 42
            if capsule(px, py, cx, cy, cx - sx * 20, cy, 5.0) or capsule(px, py, cx, cy, cx, cy - sy * 20, 5.0):
                return True
    return circle(px, py, 64, 64, 12.0)


def goby_body(px, py, cx, cy, s=1.0):
    """A goby in profile, nose right: a long body, a tail fan and a fin."""
    if ellipse(px, py, cx, cy, 26 * s, 10 * s):
        return True
    if tri(px, py, cx - 22 * s, cy, cx - 40 * s, cy + 12 * s, cx - 40 * s, cy - 12 * s):
        return True
    return tri(px, py, cx - 8 * s, cy + 8 * s, cx + 8 * s, cy + 8 * s, cx - 4 * s, cy + 20 * s)


def goby_perch(px, py):
    """The goby over its pad, the den's first blocks beside it: each perch the Goby makes is a den."""
    if capsule(px, py, 14, 24, 114, 24, 5.0):
        return True                                            # the pad
    if box(px, py, 30, 40, 11, 8) or box(px, py, 54, 40, 11, 8) or box(px, py, 42, 58, 11, 8):
        return True                                            # the den's blocks
    return goby_body(px, py, 82, 80, 1.15)                     # the goby, perched over it


def goby_undertow(px, py):
    """The Pistol's plane seen from above, and mass either side pulled into it."""
    if capsule(px, py, 64, 12, 64, 116, 4.0):
        return True                                            # the plane
    for y in (32, 64, 96):
        for side in (-1, 1):
            x0, x1 = 64 + side * 54, 64 + side * 22
            if capsule(px, py, x0, y, x1, y, 3.5):
                return True                                    # the pull
            toward = -side
            if tri(px, py, x1 - toward * 2, y + 9, x1 - toward * 2, y - 9, x1 + toward * 10, y):
                return True                                    # its head, toward the plane
            if box(px, py, x0, y, 6, 6):
                return True                                    # the block being pulled
    return False


def goby_dart(px, py):
    """The dart: a short, fast arrow with speed lines behind it (its phone button's motif)."""
    if tri(px, py, 118, 64, 84, 40, 84, 88):
        return True
    if capsule(px, py, 36, 64, 88, 64, 8.0):
        return True
    for y, x0 in ((40, 12), (88, 12), (64, 4)):
        if capsule(px, py, x0, y, x0 + 22, y, 3.5):
            return True
    return False


def pistol_snap(px, py):
    """The Snap: a vortex ring leaving the claw (its phone button's motif)."""
    d = math.hypot(px - 30, py - 64)
    ang = math.degrees(math.atan2(py - 64, px - 30))
    if d <= 26 and not (-32 <= ang <= 32):
        return True                                            # the claw, open toward the ring
    return ellipse(px, py, 92, 64, 16, 40) and not ellipse(px, py, 92, 64, 8, 31)   # the ring, edge on


def pistol_den(px, py):
    """The den the Pistol builds, and the arrow back to it (Back heads to the older dens)."""
    for cx, cy in ((34, 22), (64, 22), (94, 22), (49, 42), (79, 42), (64, 62)):
        if box(px, py, cx, cy, 12, 8):
            return True                                        # the den's blocks
    # the way back: an arc over the den, its head pointing left
    d = math.hypot(px - 64, py - 66)
    ang = math.degrees(math.atan2(py - 66, px - 64))
    if abs(d - 44) <= 4.5 and 20 <= ang <= 160:
        return True
    hx, hy = 64 - 44 * math.cos(math.radians(20)), 66 + 44 * math.sin(math.radians(20))
    return tri(px, py, hx - 12, hy + 4, hx + 10, hy + 4, hx - 1, hy - 14)


def pistol_ghost(px, py):
    """The ghost: the Pistol's outline dashed, passing through (its phone button's motif)."""
    d = math.hypot(px - 64, py - 64)
    ang = math.degrees(math.atan2(py - 64, px - 64)) % 360.0
    if abs(d - 46) <= 5.0 and (ang % 30.0) < 18.0:
        return True
    return ellipse(px, py, 64, 64, 18, 10)


def pistol_dive(px, py):
    """The dive: the body pressed onto a hill's downslope (hold to dive, let go on the upslope)."""
    hill = 36 + 20 * math.cos((px - 20) / 88 * 2 * math.pi)
    if abs(py - hill) <= 6.5 and 6 <= px <= 122:
        return True                                            # the hill
    if circle(px, py, 48, 88, 14):
        return True                                            # the Pistol
    if capsule(px, py, 58, 78, 72, 62, 5.5):
        return True                                            # pressed down the slope
    return tri(px, py, 66, 54, 82, 52, 76, 68)


GOBY, PISTOL = "goby", "pistol"
# element number -> name, the Goby's icon, the Pistol's icon
CARDS = [
    (1, "Charge", ("Goby_Lock.png", goby_lock), ("Pistol_Snap.png", pistol_snap)),
    (2, "Mass", ("Goby_Perch.png", goby_perch), ("Pistol_Den.png", pistol_den)),
    (3, "Space", ("Goby_Undertow.png", goby_undertow), ("Pistol_Ghost.png", pistol_ghost)),
    (4, "Time", ("Goby_Dart.png", goby_dart), ("Pistol_Dive.png", pistol_dive)),
]


def icons():
    for _, _, g, p in CARDS:
        yield g
        yield p


def icon_writes() -> dict:
    for png, fn in icons():
        c = coverage(fn)
        assert 0.08 <= c <= 0.45, f"{png}: coverage {c:.3f} outside the readable band"
    out = {ICON_DIR + ".meta": folder_meta(guid_for("Goby.folder"))}
    for png, fn in icons():
        out[f"{ICON_DIR}/{png}"] = render(fn)
        out[f"{ICON_DIR}/{png}.meta"] = sprite_meta(guid_for(png))
    return out


# ---------------------------------------------------------------------------------------------
# 2. The HUD variant
# ---------------------------------------------------------------------------------------------

VARIANT = "Assets/_Prefabs/UI Elements/VesselHUD/GobyHUDVariant.prefab"
VARIANT_GUID = guid_for("GobyHUDVariant.prefab")
VIEW_SCRIPT = "Assets/_Scripts/UI/View/GobyPistolHUDView.cs"
CONTROLLER_SCRIPT = "Assets/_Scripts/UI/Controller/GobyPistolHUDController.cs"

V_INSTANCE = 7881000000000000001
V_ROOT_GO = (V_INSTANCE ^ BASE_ROOT_GO) & MASK
V_ROOT_RT = (V_INSTANCE ^ BASE_ROOT_RT) & MASK
V_VIEW = 7881000000000000002
V_BASE = 7881000000000001000
TIME_GAUGE = dict(go=V_BASE + 200, rt=V_BASE + 201, cr=V_BASE + 202, img=V_BASE + 203)
SPACE_GAUGE = dict(go=V_BASE + 210, rt=V_BASE + 211, cr=V_BASE + 212, img=V_BASE + 213)
GAUGE_ON = {"Time": ("DartAndSpeedGauge", TIME_GAUGE), "Space": ("UndertowReachGauge", SPACE_GAUGE)}
GAUGE_FILL = "{r: 0.22, g: 0.51, b: 1, a: 0.55}"
READY = "{r: 0.96, g: 0.96, b: 1, a: 0.5}"


def host_ids(i):
    b = V_BASE + 100 + i * 10
    return dict(host_go=b, host_rt=b + 1, icon_go=b + 2, icon_rt=b + 3, icon_cr=b + 4, icon_img=b + 5)


def sprite_ref(png):
    return f"{{fileID: 21300000, guid: {guid_for(png)}, type: 3}}"


def variant_text() -> str:
    base = open(os.path.join(ROOT, BASE), encoding="utf-8").read()
    for fid in (BASE_ROOT_GO, BASE_ROOT_RT, BASE_MISSING_SCRIPT):
        assert re.search(rf"^--- !u!\d+ &{fid}$", base, re.M), f"base HUD prefab no longer carries fileID {fid}"

    added_go, bindings, seat_icons, blocks = [], [], [], []
    for i, (element, name, (gpng, _), (ppng, _)) in enumerate(CARDS):
        f = host_ids(i)
        gauge = GAUGE_ON.get(name)
        children = [f["icon_rt"]] + ([gauge[1]["rt"]] if gauge else [])
        blocks.append(game_object(f["host_go"], f"{name}Button", [f["host_rt"]]))
        blocks.append(rect(f["host_rt"], f["host_go"], V_ROOT_RT, children, ICON_BOX))
        blocks.append(game_object(f["icon_go"], f"{name}Icon", [f["icon_rt"], f["icon_cr"], f["icon_img"]]))
        blocks.append(rect(f["icon_rt"], f["icon_go"], f["host_rt"], [], ICON_BOX))
        blocks.append(canvas_renderer(f["icon_cr"], f["icon_go"]))
        blocks.append(image(f["icon_img"], f["icon_go"], guid_for(gpng)))   # the Goby's icon until the seat says
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
        seat_icons.append(f"  - element: {element}\n"
                          f"    goby: {sprite_ref(gpng)}\n"
                          f"    pistol: {sprite_ref(ppng)}\n")

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
{rt_modifications(BASE_ROOT_RT, BASE_GUID, BASE_ROOT_GO, "GobyHUDVariant")}    m_RemovedComponents:
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
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.GobyPistolHUDView
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
  seatIcons:
{"".join(seat_icons)}  timeGauge: {{fileID: {TIME_GAUGE['img']}}}
  spaceGauge: {{fileID: {SPACE_GAUGE['img']}}}
  gaugeColor: {GAUGE_FILL}
  gaugeFullColor: {READY}
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
# 3. Goby.prefab (called from author_goby_assets.retrofit)
# ---------------------------------------------------------------------------------------------

SQUIRREL_HUD_VARIANT_GUID = "c1572db06ad4244469ad3f25d86940b8"
SHIP_HUD_CONTAINER_RT = "3410670963481274219"   # ShipHUDContainer's RectTransform (the Squirrel's, kept)
HUD_CONTROLLER = "5649393235309590662"          # the hull's HUD controller: VesselStatus.vesselHUDController
ELEMENT_BARS_CONTROLLER = "6013884537839217096"  # ElementalBarsController on the root
DUO_ID = "5137264980016000001"                   # GobyPistolDuo on the root
ROOT_GO = "6417075533431866457"
G_INSTANCE = 7891000000000000010
G_HUD_RT = (G_INSTANCE ^ V_ROOT_RT) & MASK
G_HUD_VIEW = (G_INSTANCE ^ V_VIEW) & MASK
OWNED = {str(G_INSTANCE), str(G_HUD_RT), str(G_HUD_VIEW)}

DOC = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\n")


def split_docs(text: str):
    starts = [m.start() for m in DOC.finditer(text)]
    head = text[:starts[0]] if starts else text
    docs = []
    for i, a in enumerate(starts):
        b = starts[i + 1] if i + 1 < len(starts) else len(text)
        m = DOC.match(text, a)
        docs.append((m.group(1), m.group(2), text[a:b]))
    return head, docs


def yaml_quote(text: str) -> str:
    return "'" + text.replace("'", "''") + "'"


def controller_doc(view_script: str, controller_script: str) -> str:
    return f"""--- !u!114 &{HUD_CONTROLLER}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ROOT_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {controller_script}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.GobyPistolHUDController
  baseView: {{fileID: {G_HUD_VIEW}}}
  _iconSetSwitcher: {{fileID: 0}}
  duo: {{fileID: {DUO_ID}}}
  perchCountTemplate: {yaml_quote("{count}")}
  denCountTemplate: {yaml_quote("{count}")}
  countFontSize: 30
  countOffset: -0.62
  countLiveColor: {{r: 0.6156863, g: 1, b: 0.18039216, a: 1}}
  countEmptyColor: {{r: 0.59, g: 0.65, b: 0.76, a: 1}}
"""


def instance_docs(view_script: str) -> str:
    return f"""--- !u!1001 &{G_INSTANCE}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: {SHIP_HUD_CONTAINER_RT}}}
    m_Modifications:
{rt_modifications(V_ROOT_RT, VARIANT_GUID, V_ROOT_GO, "GobyHUDVariant")}    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {VARIANT_GUID}, type: 3}}
--- !u!224 &{G_HUD_RT} stripped
RectTransform:
  m_CorrespondingSourceObject: {{fileID: {V_ROOT_RT}, guid: {VARIANT_GUID},
    type: 3}}
  m_PrefabInstance: {{fileID: {G_INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!114 &{G_HUD_VIEW} stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {{fileID: {V_VIEW}, guid: {VARIANT_GUID},
    type: 3}}
  m_PrefabInstance: {{fileID: {G_INSTANCE}}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {view_script}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::CosmicShore.UI.GobyPistolHUDView
"""


def _instance_removals(docs, instance: str) -> set:
    """A nested prefab instance and everything that is its: its stripped stand-ins, the objects the outer prefab added
    under it (with their subtrees) and the components it added."""
    by = {a: d for _, a, d in docs}
    inst = by[instance]
    removed = {instance}
    removed |= {a for _, a, d in docs if f"  m_PrefabInstance: {{fileID: {instance}}}\n" in d}
    added = re.findall(r"addedObject: \{fileID: (-?\d+)\}", inst)
    transform_go, children = {}, {}
    for k, a, d in docs:
        if k in ("4", "224") and " stripped" not in d.split("\n", 1)[0]:
            go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", d, re.M)
            if go:
                transform_go[a] = go.group(1)
            ch = re.search(r"  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", d)
            children[a] = re.findall(r"fileID: (-?\d+)", ch.group(1)) if ch else []
    gos = set()

    def take(t):
        if t in transform_go:
            gos.add(transform_go[t])
        removed.add(t)
        for c in children.get(t, []):
            take(c)
    for a in added:
        if a in transform_go:
            take(a)
        else:
            removed.add(a)
    for _, a, d in docs:
        go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", d, re.M)
        if a in gos or (go and go.group(1) in gos):
            removed.add(a)
    return removed


def swap_hud(text: str) -> str:
    """The Squirrel's HUD off Goby.prefab and the Goby's own on. Idempotent."""
    view_script, controller_script = script_guid(VIEW_SCRIPT), script_guid(CONTROLLER_SCRIPT)
    head, docs = split_docs(text)
    squirrel = [a for k, a, d in docs if k == "1001"
                and f"m_SourcePrefab: {{fileID: 100100000, guid: {SQUIRREL_HUD_VARIANT_GUID}, type: 3}}" in d]
    removed = set(OWNED)
    for inst in squirrel:
        removed |= _instance_removals(docs, inst)
    out = []
    for _, a, d in docs:
        if a in removed:
            continue
        if a == HUD_CONTROLLER:
            d = controller_doc(view_script, controller_script)
        elif a == ELEMENT_BARS_CONTROLLER:
            d = re.sub(r"^  elementBars: \{fileID: -?\d+\}\n", "  elementBars: {fileID: 0}\n", d, flags=re.M)
        for r in removed:
            d = re.sub(r"^  - component: \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"^  - \{fileID: %s\}\n" % r, "", d, flags=re.M)
        if a == SHIP_HUD_CONTAINER_RT:
            d = re.sub(r"^  m_Children:(?: \[\])?\n", f"  m_Children:\n  - {{fileID: {G_HUD_RT}}}\n", d, count=1, flags=re.M)
            d = d.rstrip("\n") + "\n" + instance_docs(view_script)   # the instance sits beside its canvas, in place
        out.append(d)
    text = head + "".join(out)
    text = re.sub(r"^(  (?:m_Children|m_Component)):\n(?=  [a-zA-Z_<]|--- |\Z)", r"\1: []\n", text, flags=re.M)
    assert f"--- !u!114 &{HUD_CONTROLLER}\n" in text, "Goby.prefab: the HUD controller document is gone"
    assert f"--- !u!224 &{SHIP_HUD_CONTAINER_RT}\n" in text, "Goby.prefab: ShipHUDContainer is gone"
    return text


def check_hud(text: str):
    """What a Goby.prefab must be for each seat to have its own cards."""
    from author_wrasse_assets import dangling_local_refs
    errs = []

    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"guid: {SQUIRREL_HUD_VARIANT_GUID}" not in text, "Goby.prefab: still carries the Squirrel's HUD (SquirrelHUDVariant)")
    want(f"m_SourcePrefab: {{fileID: 100100000, guid: {VARIANT_GUID}, type: 3}}" in text,
         "Goby.prefab: no nested GobyHUDVariant (each seat's cards)")
    ctl = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % HUD_CONTROLLER, text)
    body = ctl.group(1) if ctl else ""
    want(f"guid: {script_guid(CONTROLLER_SCRIPT)}, type: 3" in body, "Goby.prefab: the HUD controller is not GobyPistolHUDController")
    want(f"  baseView: {{fileID: {G_HUD_VIEW}}}\n" in body, "Goby.prefab: the HUD controller's view is not the GobyHUDVariant's")
    want(f"  duo: {{fileID: {DUO_ID}}}\n" in body, "Goby.prefab: the HUD controller is not bound to the hull's GobyPistolDuo")
    want(f"  vesselHUDController: {{fileID: {HUD_CONTROLLER}}}\n" in text, "Goby.prefab: VesselStatus.vesselHUDController is not the HUD controller")
    container = re.search(r"--- !u!224 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % SHIP_HUD_CONTAINER_RT, text)
    want(container and f"  - {{fileID: {G_HUD_RT}}}\n" in container.group(1), "Goby.prefab: the Goby's HUD is not under ShipHUDContainer")
    want(not dangling_local_refs(text), f"Goby.prefab: local references that name no object: {dangling_local_refs(text)[:5]}")
    return errs


def check_variant(text: str):
    """Each card binds an icon, and both seats have their own sprite for every card."""
    errs = []
    for element, name, (gpng, _), (ppng, _) in CARDS:
        if f"  - element: {element}\n    goby: {sprite_ref(gpng)}\n    pistol: {sprite_ref(ppng)}\n" not in text:
            errs.append(f"GobyHUDVariant: the {name} card does not carry both seats' icons ({gpng}, {ppng})")
    if len(set(guid_for(p) for p, _ in icons())) != 8:
        errs.append("GobyHUDVariant: two cards share an icon")
    return errs


# ---------------------------------------------------------------------------------------------

def writes() -> dict:
    out = icon_writes()
    out[VARIANT] = variant_text()
    out[VARIANT + ".meta"] = variant_meta()
    return out


def sync(check: bool) -> list:
    drift = []
    for rel, want in writes().items():
        path = os.path.join(ROOT, rel)
        binary = isinstance(want, bytes)
        have = None
        if os.path.exists(path):
            with open(path, "rb" if binary else "r", **({} if binary else {"encoding": "utf-8", "newline": ""})) as f:
                have = f.read()
        if have != want:
            drift.append(rel)
            if not check:
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "wb" if binary else "w", **({} if binary else {"encoding": "utf-8", "newline": ""})) as f:
                    f.write(want)
    return drift


def self_test() -> int:
    prefab = open(os.path.join(ROOT, "Assets/_Prefabs/Spacevessels/Goby.prefab"), encoding="utf-8").read()
    fails = []
    good = swap_hud(prefab)
    if check_hud(good):
        fails.append(f"a swapped prefab fails: {check_hud(good)}")
    if swap_hud(good) != good:
        fails.append("swap_hud is not idempotent")
    for what, broken in (
            ("the Squirrel's HUD", prefab if f"guid: {SQUIRREL_HUD_VARIANT_GUID}" in prefab else good + f"# guid: {SQUIRREL_HUD_VARIANT_GUID}\n"),
            ("the controller left the Squirrel's", good.replace(script_guid(CONTROLLER_SCRIPT), "002a4e51f3b840e3b397dac7b84f1a9a")),
            ("the duo unbound", good.replace(f"  duo: {{fileID: {DUO_ID}}}\n", "  duo: {fileID: 0}\n")),
            ("the view unbound", good.replace(f"  baseView: {{fileID: {G_HUD_VIEW}}}\n", "  baseView: {fileID: 0}\n")),
            ("the HUD out of its canvas", good.replace(f"  - {{fileID: {G_HUD_RT}}}\n", "", 1)),
            ("a dangling reference", good.replace(f"  elementBars: {{fileID: 0}}\n", "  elementBars: {fileID: 8174941284802678170}\n"))):
        if not check_hud(broken):
            fails.append(f"check_hud did not fire on {what}")
    v = variant_text()
    if check_variant(v):
        fails.append(f"the variant fails: {check_variant(v)}")
    if not check_variant(v.replace(sprite_ref("Pistol_Dive.png"), sprite_ref("Goby_Dart.png"))):
        fails.append("check_variant did not fire on a card with one seat's icon")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_goby_hud self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv) -> int:
    if "--self-test" in argv:
        return self_test()
    check = "--check" in argv
    drift = sync(check)
    errs = check_variant(variant_text())
    for e in errs:
        print("FAIL:", e)
    if check:
        if drift:
            print("DRIFT:\n  " + "\n  ".join(drift))
        print("author_goby_hud:", "ok" if not drift and not errs else "problems")
        return 1 if drift or errs else 0
    print(f"wrote {len(drift)} file(s)" + "".join(f"\n  {d}" for d in drift))
    for png, fn in icons():
        print(f"  {png:20} guid {guid_for(png)}  coverage {coverage(fn):.3f}")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
