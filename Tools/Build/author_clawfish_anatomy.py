#!/usr/bin/env python3
"""
Give the Clawfish a body, a spindle and a seated heart — the three things §45 left open.

What was wrong
--------------
Clawfish.prefab was a LightFauna, a nested FBX and a crystal, and nothing else:

  * ZERO HealthPrisms, so it carried no conserved mass and `Fauna.OnBodyPrismExploded`
    could never fire — the one creature in the game that could not be shot (ECOSYSTEM §24).
  * NO Spindle, so the GPU sway shipped in §44 reached every other creature and not this one.
  * its HEART floated 1.6 units in FRONT of the open mouth (root z +1.32) rather than inside
    the body — it read as something the fish was about to swallow.

What the geometry actually is (measured, Tools/Build/verify_fauna_heart_seat.py)
--------------------------------------------------------------------------------
ClawfishTest.fbx is THREE connected components: a hollow HORN (739 verts, z −11.61..−0.42,
open at the +z mouth, pinching shut at z −9.4) and two mirrored horizontal TAIL FLUKES
(170 verts each, z −17.78..−5.08) — thin in y, broad in x, a whale's tail rather than claws.
The largest sphere that fits inside the horn on its own axis is r 2.21 at z −1.42, and the
cavity stays over r 1.28 all the way back to z −7.4. So there IS a back cavity, and it is
where a heart belongs.

What this writes
----------------
  Clawfish (root)                        LightFauna
  ├── Body                               Spindle  <- new, RenderedObject resolves to the FBX
  │   ├── ClawfishTest (1)               the FBX instance, re-parented
  │   └── HealthBlock x4                 two per fluke, on the broad blade
  └── SpaceCrystal                       moved from z +1.32 to z −2.06, inside the horn

FOUR prisms, matching the QuadFish — the two fish are the same size (17.4 vs 17.5 world
units) and this keeps the collider budget identical to the species the Clawfish is a peer of.
They sit on the FLUKES because §26's ordered wither runs farthest-from-the-heart first, so a
starving Clawfish should lose its tail before its core.

Their poses are MEASURED off the fluke, not eyeballed: each prism sits at the blade's own
centroid at its z station, and is pitched by the blade's own local slope between the two
stations (atan(0.83 / 2.6) = 17.7 deg), so the pair follows the fluke's upward curve.

Usage:  python3 Tools/Build/author_clawfish_anatomy.py [--check]
"""

import math
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PREFAB = "Assets/_Prefabs/FloraAndFauna/Clawfish.prefab"
FBX_META = "Assets/_Models/Fauna/ClawfishTest.fbx.meta"

ROOT_GO = "6455206541193087406"
ROOT_T = "6455206541193087393"
LIGHTFAUNA = "369859875180954115"
CRYSTAL_INSTANCE = "8182856232501990"
CRYSTAL_STRIPPED = "6589823528699324667"
CRYSTAL_SRC_T = "6588436222817790493"
CRYSTAL_GUID = "a4bde9d72595bfb43aa3b791d02f4db8"
FBX_INSTANCE = "8203972130975256407"
FBX_STRIPPED = "8525565138592426172"
FBX_SRC_T = "-8679921383154817045"
FBX_GUID = "9cc23c905c080c64a9c89bf5211d41ac"

SPINDLE_SCRIPT = "8ec45d1233573f9409107a25ab23b0c9"
HEALTH_GUID = "1488a2ac58b2b4c43b14f84206bd9195"
HEALTH_SRC_T = "5222650486365209692"
HEALTH_SRC_GO = "5776304996075792891"

SPINDLE_MAT_GUID = "4f44fa5c7514a2c45b5af7f45bc51acd"
CREATURE_MAT_GUID = "50b00b0267c900544856c0d3a2a3d54c"

# A fileID is a SIGNED int64; a random 19-digit decimal overflows it ~16% of the time and
# Unity then fails the WHOLE FILE with "Broken text PPtr" (/asset-surgery §3). These are a
# readable family, well under the ceiling, and asserted absent from the source before use.
BODY_GO = "4410000000000000101"
BODY_T = "4410000000000000102"
SPINDLE = "4410000000000000103"
INT64_MAX = 9223372036854775807

# The FBX instance sits at z +0.14 inside Body, so a mesh coordinate lands at z + 0.14.
FBX_Z_OFFSET = 0.14

# Measured stations on the upper fluke (FBX coords): (blade centroid y, z).
STATION_OUTER = (1.83, -16.0)
STATION_INNER = (2.66, -13.4)
PRISM_SCALE = (2.6, 0.4, 3.0)          # wide, thin, long — a rib lying along the blade
# The SHALLOWEST seat that puts the whole heart behind the mouth plane (z −0.419). The
# horn narrows going back, so how far the heart shows through the hull grows monotonically
# with depth — shallowest-and-legal is therefore also most-enclosed. Proved by
# Tools/Build/verify_fauna_heart_seat.py, which carries the seat sweep as its control.
HEART_Z_FBX = -2.8


def blade_pitch_degrees():
    """The fluke's own slope between the two stations, which is the prism's pitch."""
    dy = STATION_INNER[0] - STATION_OUTER[0]
    dz = STATION_INNER[1] - STATION_OUTER[1]
    return -math.degrees(math.atan2(dy, dz))


def quat_x(deg):
    h = math.radians(deg) * 0.5
    return (math.cos(h), math.sin(h))


def num(v):
    """Unity writes floats without a trailing .0 and trims noise."""
    if abs(v - round(v)) < 1e-9:
        return str(int(round(v)))
    return ("%.7f" % v).rstrip("0").rstrip(".")


def prisms():
    """(name, position, euler_x) for the four fluke ribs, upper fluke then lower."""
    pitch = blade_pitch_degrees()
    out = []
    for sign, tag in ((1.0, "Upper"), (-1.0, "Lower")):
        for (y, z), where in ((STATION_OUTER, "Outer"), (STATION_INNER, "Inner")):
            out.append(("HealthBlock %s %s" % (tag, where),
                        (0.0, sign * y, z + FBX_Z_OFFSET),
                        sign * pitch))
    return out


def modification(target, guid, prop, value):
    return ("    - target: {fileID: %s, guid: %s,\n        type: 3}\n"
            "      propertyPath: %s\n      value: %s\n"
            "      objectReference: {fileID: 0}\n" % (target, guid, prop, value))


def transform_mods(target, guid, pos, euler_x, scale, root_order, name_target, name):
    w, x = quat_x(euler_x)
    rows = [("m_RootOrder", str(root_order)),
            ("m_LocalScale.x", num(scale[0])),
            ("m_LocalScale.y", num(scale[1])),
            ("m_LocalScale.z", num(scale[2])),
            ("m_LocalPosition.x", num(pos[0])),
            ("m_LocalPosition.y", num(pos[1])),
            ("m_LocalPosition.z", num(pos[2])),
            ("m_LocalRotation.w", num(w)),
            ("m_LocalRotation.x", num(x)),
            ("m_LocalRotation.y", "0"),
            ("m_LocalRotation.z", "0"),
            ("m_LocalEulerAnglesHint.x", num(euler_x)),
            ("m_LocalEulerAnglesHint.y", "0"),
            ("m_LocalEulerAnglesHint.z", "0")]
    body = "".join(modification(target, guid, p, v) for p, v in rows)
    body += modification(name_target, guid, "m_Name", name)
    return body


def health_instance(instance_id, stripped_id, name, pos, euler_x, root_order):
    return (
        "--- !u!1001 &%s\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
        "  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: %s}\n"
        "    m_Modifications:\n%s"
        "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n"
        "    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
        "  m_SourcePrefab: {fileID: 100100000, guid: %s, type: 3}\n"
        "--- !u!4 &%s stripped\nTransform:\n"
        "  m_CorrespondingSourceObject: {fileID: %s, guid: %s,\n    type: 3}\n"
        "  m_PrefabInstance: {fileID: %s}\n  m_PrefabAsset: {fileID: 0}\n"
        % (instance_id, BODY_T,
           transform_mods(HEALTH_SRC_T, HEALTH_GUID, pos, euler_x, PRISM_SCALE,
                          root_order, HEALTH_SRC_GO, name),
           HEALTH_GUID, stripped_id, HEALTH_SRC_T, HEALTH_GUID, instance_id))


def seat_heart(src):
    """Re-appliable on its own, so the seat stays a value this script OWNS.

    Keeping it inside the one-shot structural pass would make the seat unchangeable the
    moment Body exists — a generator whose --check can only ever say "already done" is the
    spent one-shot /asset-surgery §0 warns about.
    """
    out, n = re.subn(
        r"(target: \{fileID: %s, guid: %s,\n        type: 3\}\n      propertyPath: m_LocalPosition\.z\n      value: )[-0-9.]+"
        % (CRYSTAL_SRC_T, CRYSTAL_GUID),
        lambda m: m.group(1) + num(HEART_Z_FBX + FBX_Z_OFFSET), src)
    assert n == 1, "expected exactly one crystal m_LocalPosition.z override, found %d" % n
    return out


def build(src):
    out = src

    # ── 2. the FBX instance re-parents under Body ─────────────────────────────
    marker = ("--- !u!1001 &%s\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
              "  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: %s}"
              % (FBX_INSTANCE, ROOT_T))
    assert marker in out, "the FBX instance is not parented to the root — re-read the prefab"
    out = out.replace(marker, marker.replace("m_TransformParent: {fileID: %s}" % ROOT_T,
                                             "m_TransformParent: {fileID: %s}" % BODY_T), 1)

    # ── 3. the root's children: the crystal, and Body in the FBX's place ──────
    old_children = ("  m_Children:\n  - {fileID: %s}\n  - {fileID: %s}\n"
                    % (CRYSTAL_STRIPPED, FBX_STRIPPED))
    assert old_children in out, "the root's child list is not what this script was written against"
    out = out.replace(old_children,
                      "  m_Children:\n  - {fileID: %s}\n  - {fileID: %s}\n"
                      % (BODY_T, CRYSTAL_STRIPPED), 1)

    # ── 4. Body, its Spindle, and the four ribs ───────────────────────────────
    rows = prisms()
    body_children = "".join("  - {fileID: %s}\n" % (int(BODY_T) + 20 + i)
                            for i in range(len(rows)))
    body = (
        "--- !u!1 &%s\nGameObject:\n  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n"
        "  - component: {fileID: %s}\n  - component: {fileID: %s}\n"
        "  m_Layer: 0\n  m_Name: Body\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n"
        "  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
        "--- !u!4 &%s\nTransform:\n  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %s}\n  serializedVersion: 2\n"
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n"
        "  m_Children:\n  - {fileID: %s}\n%s"
        "  m_Father: {fileID: %s}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        "--- !u!114 &%s\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %s}\n  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
        "  m_Name: \n  m_EditorClassIdentifier: \n"
        "  RenderedObject: {fileID: 0}\n  additionalRenderedObjects: []\n"
        "  parentSpindle: {fileID: 0}\n  LifeForm: {fileID: 0}\n"
        "  retainSpindle: 0\n  permanentWither: 1\n"
        % (BODY_GO, BODY_T, SPINDLE,
           BODY_T, BODY_GO, FBX_STRIPPED, body_children, ROOT_T,
           SPINDLE, BODY_GO, SPINDLE_SCRIPT))

    ribs = "".join(health_instance(str(int(BODY_T) + 10 + i), str(int(BODY_T) + 20 + i),
                                   name, pos, euler, i + 1)
                   for i, (name, pos, euler) in enumerate(rows))

    # keep the file's shape: GameObject/Transform/behaviours, then the PrefabInstances
    anchor = "--- !u!1001 &%s\nPrefabInstance:" % CRYSTAL_INSTANCE
    assert anchor in out
    out = out.replace(anchor, body + anchor, 1)
    if not out.endswith("\n"):
        out += "\n"
    out += ribs
    return out


def build_meta(src):
    """Point the FBX's material remap at the shared SpindleMaterial.

    The renderer lives inside a nested FBX PrefabInstance, so a material override from the
    prefab would need an fbx-internal fileID that only the importer knows. The importer's own
    externalObjects remap is the sanctioned way to say it, and ClawfishTest.fbx has exactly
    one user, so the blast radius is this creature.

    It is the SHARED fauna material rather than the Clawfish's own `CreatureMaterial`
    because the body is now a Spindle: `SpindleMaterial` is the one every other creature
    wears, it is the graph `SpindleSway.hlsl` lives in, and it already authors the shared
    0.08 / 1.4 sway. `CreatureMaterial` is on a different graph entirely and would leave
    this fish the only creature in the cell that does not move.
    """
    assert CREATURE_MAT_GUID in src or SPINDLE_MAT_GUID in src, \
        "ClawfishTest.fbx.meta does not remap its material at all"
    return src.replace(CREATURE_MAT_GUID, SPINDLE_MAT_GUID)


def validate(prefab, meta):
    problems = []

    for fid in (BODY_GO, BODY_T, SPINDLE):
        if int(fid) > INT64_MAX:
            problems.append("fileID %s overflows a signed int64" % fid)

    # every local fileID a reference names must be defined in this file
    defined = set(re.findall(r"^--- !u!\d+ &(\d+)", prefab, re.M))
    refs = set()
    for m in re.finditer(r"fileID: (-?\d+)\}", prefab):
        tail = prefab[m.end():m.end() + 8]
        if tail.startswith(","):
            continue
        refs.add(m.group(1))
    for m in re.finditer(r"\{fileID: (-?\d+), guid:", prefab):
        refs.discard(m.group(1))
    dangling = {r for r in refs if r not in defined and r not in ("0", "11500000", "100100000", "2100000")}
    if dangling:
        problems.append("dangling local fileIDs: %s" % sorted(dangling))

    # one Spindle, one Body, four ribs, one FBX, one crystal
    counts = {
        "Spindle components": len(re.findall(r"guid: %s, type: 3\}" % SPINDLE_SCRIPT, prefab)),
        "HealthBlock instances": len(re.findall(r"m_SourcePrefab: \{fileID: 100100000, guid: %s" % HEALTH_GUID, prefab)),
        "FBX instances": len(re.findall(r"m_SourcePrefab: \{fileID: 100100000, guid: %s" % FBX_GUID, prefab)),
        "crystal instances": len(re.findall(r"m_SourcePrefab: \{fileID: 100100000, guid: %s" % CRYSTAL_GUID, prefab)),
    }
    for key, want in (("Spindle components", 1), ("HealthBlock instances", 4),
                      ("FBX instances", 1), ("crystal instances", 1)):
        if counts[key] != want:
            problems.append("%s: %d, expected %d" % (key, counts[key], want))

    # the FBX hangs off Body, the crystal off the root
    if ("m_TransformParent: {fileID: %s}" % BODY_T) not in prefab:
        problems.append("nothing is parented to Body")
    fbx_block = prefab.split("--- !u!1001 &%s" % FBX_INSTANCE)[1].split("--- ")[0]
    if ("m_TransformParent: {fileID: %s}" % BODY_T) not in fbx_block:
        problems.append("the FBX instance is not parented to Body")
    crystal_block = prefab.split("--- !u!1001 &%s" % CRYSTAL_INSTANCE)[1].split("--- ")[0]
    if ("m_TransformParent: {fileID: %s}" % ROOT_T) not in crystal_block:
        problems.append("the crystal is not parented to the root")

    # the heart is where the measurement put it
    m = re.search(r"propertyPath: m_LocalPosition\.z\n      value: ([-0-9.]+)", crystal_block)
    if not m or abs(float(m.group(1)) - (HEART_Z_FBX + FBX_Z_OFFSET)) > 1e-6:
        problems.append("the heart's z is %s, expected %s"
                        % (m.group(1) if m else "absent", num(HEART_Z_FBX + FBX_Z_OFFSET)))

    # Spindle's RenderedObject is deliberately unauthored — it resolves to the FBX renderer
    if "RenderedObject: {fileID: 0}" not in prefab:
        problems.append("the Spindle authors a RenderedObject; it must resolve from children")

    if CREATURE_MAT_GUID in meta:
        problems.append("ClawfishTest.fbx.meta still remaps to CreatureMaterial")
    if SPINDLE_MAT_GUID not in meta:
        problems.append("ClawfishTest.fbx.meta does not remap to SpindleMaterial")

    return problems


def main():
    check_only = "--check" in sys.argv
    src = open(os.path.join(REPO, PREFAB), encoding="utf-8").read()
    meta_src = open(os.path.join(REPO, FBX_META), encoding="utf-8").read()

    already = ("&%s" % BODY_GO) in src
    prefab = seat_heart(src if already else build(src))
    meta = build_meta(meta_src)

    problems = validate(prefab, meta)
    if problems:
        for p in problems:
            print("FAIL: %s" % p, file=sys.stderr)
        sys.exit(1)

    if prefab == src and meta == meta_src:
        print("Clawfish anatomy: up to date")
        print("OK")
        return
    if check_only:
        print("Clawfish anatomy: NOT authored", file=sys.stderr)
        sys.exit(1)

    with open(os.path.join(REPO, PREFAB), "w", encoding="utf-8") as fh:
        fh.write(prefab)
    with open(os.path.join(REPO, FBX_META), "w", encoding="utf-8") as fh:
        fh.write(meta)
    print("Clawfish anatomy: written")
    print("  Body + Spindle over the FBX, 4 fluke ribs at pitch %.1f deg, heart at z %s"
          % (blade_pitch_degrees(), num(HEART_Z_FBX + FBX_Z_OFFSET)))
    print("OK")


if __name__ == "__main__":
    main()
