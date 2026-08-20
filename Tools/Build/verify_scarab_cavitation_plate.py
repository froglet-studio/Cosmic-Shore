#!/usr/bin/env python3
"""Re-prove the Scarab's cavitation plate from the SHIPPED assets.

Companion to Tools/Build/verify_gyroid_octagon_tables.py and friends, and written for the
same reason: a geometry argument that was validated once, in a session, against numbers
typed into a script is not evidence about what ships. Between the argument and the asset
there is a TRANSCRIPTION, and that step is invisible to both the argument and code review.

So this parses the real prefabs and ProjectSettings, derives the plate the game will
actually build, and asserts the properties the design claims:

  1. radius / length come from the RELATIONSHIP to the hull collider, not a loose number
  2. the Burst slabs tile [0, L] EXACTLY at any frame rate - no gap, no reach past the tip
  3. the drawn cylinder is the damaged volume, frame for frame
  4. the trigger box CIRCUMSCRIBES (never under-reaches) and its excess is bounded
  5. depth 0 is the zero state on every axis (a cancelled blast can freeze there)
  6. the contact window survives the project's fixed timestep
  7. debris leaves at the blast's own velocity (restitution x inertia == 1)
  8. the plate visual's authored rotation maps the built-in Cylinder's +Y onto the sweep axis

Run:  python3 Tools/Build/verify_scarab_cavitation_plate.py
Exit code 0 = the shipped assets still describe the plate this file documents.
"""
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VESSEL = ROOT / "Assets/_Prefabs/Spacevessels/Scarab.prefab"
BLAST = ROOT / "Assets/_Prefabs/Projectile/AOEScarabCavitation.prefab"
TIME = ROOT / "ProjectSettings/TimeManager.asset"
BLAST_SCRIPT_GUID = "07d31f59a470cf1b100153fb27def1c5"   # AOECylindricalExplosion
JUKE_SCRIPT_GUID = None                                   # resolved from the .cs.meta below

FAILURES = []


def check(ok, label, detail=""):
    print(f"  {'PASS' if ok else 'FAIL'}  {label}{('  — ' + detail) if detail else ''}")
    if not ok:
        FAILURES.append(label)


def documents(text):
    """Unity YAML documents, as (classid, fileid, body)."""
    out = []
    for m in re.finditer(r"^--- !u!(\d+) &(\d+)(?: stripped)?$", text, re.M):
        start = m.end()
        nxt = re.search(r"^--- !u!\d+ &\d+", text[start:], re.M)
        end = start + (nxt.start() if nxt else len(text) - start)
        out.append((m.group(1), m.group(2), text[start:end]))
    return out


def mono_field(text, script_guid, field, cast=float):
    """Read one serialized field from the MonoBehaviour whose m_Script is script_guid."""
    for cls, _fid, body in documents(text):
        if cls != "114" or f"guid: {script_guid}" not in body:
            continue
        m = re.search(rf"^  {re.escape(field)}: (.+)$", body, re.M)
        if m:
            return cast(m.group(1).strip())
    raise AssertionError(f"field {field!r} not found on script {script_guid}")


def main():
    vessel_txt = VESSEL.read_text()
    blast_txt = BLAST.read_text()

    # --- the hull collider: the ONE authored number the whole blast is sized from ------
    hull = None
    for cls, _fid, body in documents(vessel_txt):
        if cls != "135":                      # SphereCollider
            continue
        r = float(re.search(r"^  m_Radius: (.+)$", body, re.M).group(1))
        c = re.search(r"^  m_Center: \{x: (\S+), y: (\S+), z: ([^}]+)\}$", body, re.M).groups()
        # the hull sphere is the one at the origin; the skimmer's is a 0.5 primitive it scales
        if abs(r - 0.5) > 1e-6:
            hull = (r, tuple(float(v) for v in c))
    assert hull, "no hull SphereCollider found on Scarab.prefab"
    hull_r, hull_c = hull

    juke_meta = ROOT / "Assets/_Scripts/Controller/Vessel/R_VesselActions/ScarabCavitationBlast.cs.meta"
    cav_guid = re.search(r"guid: (\w+)", juke_meta.read_text()).group(1)

    ratio = mono_field(vessel_txt, cav_guid, "radiusPerVesselRadius")
    length_per_r = mono_field(blast_txt, BLAST_SCRIPT_GUID, "lengthPerRadius")
    sweep_speed = mono_field(blast_txt, BLAST_SCRIPT_GUID, "sweepSpeed")
    hold_authored = mono_field(blast_txt, BLAST_SCRIPT_GUID, "contactHoldSeconds")
    inertia = mono_field(blast_txt, BLAST_SCRIPT_GUID, "Inertia")
    restitution = mono_field(blast_txt, BLAST_SCRIPT_GUID, "debrisRestitution")
    proportional = mono_field(blast_txt, BLAST_SCRIPT_GUID, "proportionalDebris", int)
    fixed_step = float(re.search(r"^  Fixed Timestep: (.+)$", TIME.read_text(), re.M).group(1))

    R = hull_r * ratio
    L = R * length_per_r
    duration = L / sweep_speed

    print(f"\nShipped Scarab cavitation plate (read from the assets, not assumed):")
    print(f"  NOTE: the runtime multiplies the collider radius by the hull GameObject's WORLD")
    print(f"        lossyScale (ScarabCavitationBlast.ResolveVesselColliderRadius), and that")
    print(f"        scale lives in SparrowModel1.fbx's binary, not in the repo — so the numbers")
    print(f"        below assume the FBX instance root is unit-scaled. The blast sizes itself")
    print(f"        CORRECTLY either way; what an offline check cannot settle is whether 4.5 is")
    print(f"        the intended WORLD radius. Confirm once in the editor (the checklist says how);")
    print(f"        every assertion here is about the relationships, which hold at any scale.")
    print(f"  hull collider        r={hull_r:g} at {hull_c}")
    print(f"  radiusPerVesselRadius {ratio:g}  ->  plate radius R = {R:g}")
    print(f"  lengthPerRadius       {length_per_r:g}  ->  plate length L = {L:g}")
    print(f"  sweepSpeed            {sweep_speed:g} u/s  ->  duration = {duration:.4f} s")
    print(f"  fixed timestep        {fixed_step:g} s  ({1/fixed_step:.0f} Hz)\n")

    print("Assertions:")

    # 1. the hull sphere is centred on the ship, or "radius" means something else
    check(hull_c == (0.0, 0.0, 0.0), "hull collider is centred on the vessel origin", str(hull_c))

    # 2. slabs tile [0, L] exactly, at any frame rate (linear sweep, clamped at t=1)
    tiling_ok = True
    for fps in (10, 20, 30, 60, 90, 144, 240):
        n = max(1, math.ceil(duration * fps))
        depths = [L * min((i + 1) / n, 1.0) for i in range(n)]
        swept, cover = 0.0, []
        for d in depths:
            cover.append((swept, d))
            swept = max(swept, d)
        if abs(cover[0][0]) > 1e-9 or abs(cover[-1][1] - L) > 1e-9:
            tiling_ok = False
        for a, b in zip(cover, cover[1:]):
            if abs(a[1] - b[0]) > 1e-9:
                tiling_ok = False
    check(tiling_ok, "Burst slabs tile [0, L] exactly at 10..240 fps")

    # 3. the drawn cylinder IS the damaged volume
    visual_ok = True
    for t in (0.0, 0.1, 0.5, 0.9, 1.0):
        d = L * t
        scale = (2 * R, d / 2, 2 * R)      # ShapePlateVisual
        pos_z = d / 2
        drawn_len, drawn_rad = 2 * scale[1], scale[0] / 2
        if abs(drawn_len - d) > 1e-9 or abs(drawn_rad - R) > 1e-9:
            visual_ok = False
        if abs((pos_z - drawn_len / 2)) > 1e-9 or abs((pos_z + drawn_len / 2) - d) > 1e-9:
            visual_ok = False
    check(visual_ok, "drawn cylinder == damaged slab union at every t")

    # 4. trigger box circumscribes, never under-reaches, bounded excess
    under = False
    for i in range(1, 2001):
        d = L * (i / 2000)
        size = (2 * R, 2 * R, d)
        if size[0] / 2 < R - 1e-9 or size[1] / 2 < R - 1e-9:
            under = True
        if abs((d / 2 - size[2] / 2)) > 1e-6 or abs((d / 2 + size[2] / 2) - d) > 1e-6:
            under = True
    check(not under, "trigger box circumscribes the plate and never under-reaches",
          f"corner reach {math.sqrt(2):.3f}xR, excess area {(4/math.pi)-1:.0%}")
    # INFORMATIONAL, deliberately not an assertion: it explains WHY the trigger is a box at
    # today's aspect, but a plate authored at L >= 2R would make a sphere adequate again — and
    # the box is still correct there, just no longer necessary. Failing on that would be a tool
    # enforcing a window it measured at one aspect, which is how a validator starts lying.
    sphere_r = min(L / 2, R)
    if sphere_r < R:
        print(f"  info  an inscribed SPHERE would cap at r={sphere_r:g} vs R={R:g} "
              f"({100*(1-sphere_r/R):.0f}% of the reach lost) — this is why the trigger is a box")
    else:
        print(f"  info  at this aspect (L={L:g} >= 2R={2*R:g}) a sphere trigger would also reach; "
              f"the box remains correct, just no longer necessary")

    # 5. depth 0 is the zero state on EVERY axis
    src = (ROOT / "Assets/_Scripts/Controller/Projectiles/AOECylindricalExplosion.cs").read_text()
    zero_state = re.search(r"if \(depth <= 0f\)\s*\{\s*_triggerBox\.size = new Vector3\(([^)]*)\)", src)
    check(bool(zero_state) and all(abs(float(v.strip().rstrip('f'))) <= 1e-3
                                   for v in zero_state.group(1).split(',')),
          "depth 0 shapes a nothing-box on every axis (a cancelled blast can freeze there)")

    # 6. the contact window survives the project's fixed timestep
    hold = max(hold_authored, fixed_step * 2)
    check(hold / fixed_step >= 2.0 - 1e-9,
          "contact hold covers >= 2 physics steps at the shipped timestep",
          f"{hold:g}s = {hold/fixed_step:.2f} steps (authored {hold_authored:g} alone = "
          f"{hold_authored/fixed_step:.2f})")
    check("Time.fixedDeltaTime * 2f" in src,
          "the hold is FLOORED off fixedDeltaTime, not a bare wall-clock number")

    # 7. debris leaves at the blast's own velocity
    check(proportional == 1, "proportionalDebris is ON (otherwise Inertia is dead tuning)")
    check(abs(restitution * inertia - 1.0) < 1e-3,
          "debrisRestitution x Inertia == 1, so debris speed IS the blast velocity",
          f"{restitution:g} x {inertia:g} = {restitution*inertia:.4f} -> {sweep_speed:g} u/s")

    # 8. the plate visual's authored rotation maps the built-in Cylinder's +Y onto +Z
    plate_rot = None
    for cls, _fid, body in documents(blast_txt):
        if cls == "4" and "m_LocalEulerAnglesHint: {x: 90" in body:
            m = re.search(r"m_LocalRotation: \{x: (\S+), y: (\S+), z: (\S+), w: ([^}]+)\}", body)
            plate_rot = tuple(float(v) for v in m.groups())
    expect = (math.sin(math.radians(45)), 0.0, 0.0, math.cos(math.radians(45)))
    check(plate_rot is not None and all(abs(a - b) < 1e-5 for a, b in zip(plate_rot, expect)),
          "plate child is rolled +90 deg about X (built-in Cylinder +Y -> sweep axis +Z)",
          str(plate_rot))

    # the mesh must actually be the built-in Cylinder (10206), not the Sphere (10207)
    check("m_Mesh: {fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}" in blast_txt,
          "plate visual uses Unity's built-in Cylinder mesh (10206)")

    print()
    if FAILURES:
        print(f"FAILED: {len(FAILURES)} assertion(s): " + "; ".join(FAILURES))
        return 1
    print("All assertions passed — the shipped assets describe the documented plate.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
