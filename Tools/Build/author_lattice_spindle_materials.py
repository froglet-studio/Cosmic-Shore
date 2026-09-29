#!/usr/bin/env python3
"""A LATTICE'S SWAY IS AUTHORED IN DEGREES, NOT IN A SHARED SLOPE.

Docs/ECOSYSTEM.md 47.7.

`SpindleSway.hlsl` shears a vertex by `Amplitude * PositionOS.z`, and its own header
calls `Amplitude` "a dimensionless SLOPE, so the same number means the same visual bend
on every mesh that shares the material". That is true of a UNIFORMLY scaled mesh and
false of every other one, because the shear is authored in OBJECT space: a renderer
carrying localScale (1, 1, sz) converts the slope into a world bend of

    tip deflection angle = atan(Amplitude * sx / sz)

so a mesh stretched along its own bend axis bends that much LESS. The sentence offered
in that header as reassurance -- "every shipped spindle prefab is scaled on z to match
(Branch 6.2, TadpoleSpindle 3.0)" -- names the two prefabs that DISAGREE: 3.0 is uniform
and 6.2 is a stretch, so at the shared 0.08 the tadpole bends 4.57 degrees and a lattice
branch bends 0.64-1.48.

That is why the lattice species read as dead. This script gives each lattice BRANCH mesh
its own material with the amplitude SOLVED for one authored world bend, which is the
right shape for two independent reasons:

  * it is Docs/ECOSYSTEM.md 46's rule applied one level down. "A shared material is a
    claim that everything wearing it moves alike" -- three meshes that disagree about
    their own z by 2.3x do not move alike under one slope, so they do not share.
  * equal ANGLE is equal FRACTION OF THE LIMB, so a prism riding at its fixed height up
    the limb sweeps the same fraction of its own bond in every lattice species. One
    authored number then means one thing across a family whose bonds span 3 to 24 world
    units, and it stays true under `FloraVariantTuning.LatticeScale` (which scales the
    branch and the bond together, so the ratio is scale-invariant by construction).

Run with --check in CI: it re-derives every amplitude from the SHIPPED prefabs and fails
on any hand-edit that drifts a material off what it would author.
"""

import argparse, hashlib, re, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SPINDLE_SCRIPT_GUID = "8ec45d1233573f9409107a25ab23b0c9"
DONOR = REPO / "Assets/_Graphics/Materials/SpindleMaterial.mat"
DONOR_GUID = "4f44fa5c7514a2c45b5af7f45bc51acd"
MAT_DIR = REPO / "Assets/_Graphics/Materials"
SPINDLE_SWAY = REPO / "Assets/_Graphics/Materials/Graphs/SpindleSway.hlsl"

# The one authored number. 3 degrees of tip deflection -- two thirds of the 4.57 the
# uniformly-scaled creature spindles get at the shared 0.08, so a crystal can never move
# more than a fish. It puts the prism riding at 55% of its limb ~3.2% of a bond away
# from rest: a breath. Two neighbouring prisms are on independent phases, so the most they
# ever move APART is twice that, ~6.4% of the bond -- inside the lattice's own 10% mate-snap
# tolerance (0.3 on 3, Docs/ECOSYSTEM.md 34.8), so it can never read as a broken bond.
#
# THIS IS THE ONE NUMBER TO MOVE after the first playtest. Everything else here is
# measured off the shipped prefabs.
TARGET_TIP_DEGREES = 3.0

# The band the derived amplitudes must land in, asserted rather than assumed: below the
# floor nothing moves, above the ceiling a lattice stops reading as crystalline.
AMPLITUDE_BAND = (0.05, 0.45)
# The PRISM's sweep as a fraction of the limb it rides -- the number this whole feature
# is about (Docs/ECOSYSTEM.md 47), and the one equal-angle makes near-identical across a
# family whose bonds span 3 to 24 world units. Asserted so a future branch prefab that
# seats its prism at a different height up the limb cannot silently leave the band.
PRISM_SWEEP_FRACTION_BAND = (0.015, 0.040)

# Which branch prefab dresses which material. Wall and Schwarz P share AssemblyBranch
# (Docs/ECOSYSTEM.md 34.12), so they share its material -- the amplitude is a property
# of the MESH, not of the species.
BRANCHES = [
    ("Assets/_Prefabs/FloraAndFauna/Spindles/GyroidBranch.prefab", "GyroidSpindleMaterial"),
    ("Assets/_Prefabs/FloraAndFauna/Spindles/AssemblyBranch.prefab", "AssemblySpindleMaterial"),
    ("Assets/_Prefabs/FloraAndFauna/Spindles/QuasicrystalBranch.prefab", "QuasicrystalSpindleMaterial"),
]


def guid_for(name):
    """Deterministic, so a re-run is a no-op and --check compares content, not identity."""
    return hashlib.md5(f"CosmicShore/LatticeSpindleMaterials/{name}".encode()).hexdigest()


def split_docs(text):
    return re.split(r"^--- ", text, flags=re.M)


def parse_local_poses(path):
    """-> {gameObject fileID: (localPosition, localRotation, localScale)}."""
    out = {}
    for d in split_docs(path.read_text()):
        if not d.startswith("!u!4 "):
            continue
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", d)
        pos = re.search(r"m_LocalPosition: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)\}", d)
        rot = re.search(r"m_LocalRotation: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+), w: ([-\d.e+]+)\}", d)
        sc = re.search(r"m_LocalScale: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)\}", d)
        if not (go and pos and rot and sc):
            continue
        out[go.group(1)] = (tuple(float(v) for v in pos.groups()),
                            tuple(float(v) for v in rot.groups()),
                            tuple(float(v) for v in sc.groups()))
    return out


def parse_prefab(path):
    """-> (transforms by fileID, renderers by fileID, spindle doc)."""
    text = path.read_text()
    docs = split_docs(text)
    transforms, renderers, spindles = {}, {}, []
    for d in docs:
        if d.startswith("!u!4 "):
            fid = re.search(r"&(\d+)", d).group(1)
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", d)
            sc = re.search(
                r"m_LocalScale: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)\}", d)
            if not (go and sc):
                continue
            transforms[go.group(1)] = tuple(float(v) for v in sc.groups())
        elif d.startswith("!u!23 "):
            fid = re.search(r"&(\d+)", d).group(1)
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", d)
            if go:
                renderers[fid] = go.group(1)
        elif d.startswith("!u!114 ") and SPINDLE_SCRIPT_GUID in d:
            spindles.append(d)
    assert len(spindles) == 1, f"{path.name}: expected exactly one Spindle, got {len(spindles)}"
    return text, transforms, renderers, spindles[0]


def spindle_renderer_ids(spindle_doc, path):
    ro = re.search(r"RenderedObject: \{fileID: (\d+)\}", spindle_doc)
    ids = [ro.group(1)] if ro and ro.group(1) != "0" else []
    if "additionalRenderedObjects:" in spindle_doc:
        tail = spindle_doc.split("additionalRenderedObjects:")[1].split("parentSpindle")[0]
        ids += re.findall(r"^  - \{fileID: (\d+)\}", tail, flags=re.M)
    assert ids, f"{path.name}: Spindle names no renderer"
    return ids


def rotate_inverse(q, v):
    """conj(q) * v * q -- a point taken back into the frame q rotates out of."""
    x, y, z, w = q
    x, y, z = -x, -y, -z                      # conjugate
    # v' = v + 2 * cross(qv, cross(qv, v) + w * v)
    qv = (x, y, z)
    def cross(a, b):
        return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
    t = cross(qv, v)
    t = (t[0] + w * v[0], t[1] + w * v[1], t[2] + w * v[2])
    t = cross(qv, t)
    return (v[0] + 2 * t[0], v[1] + 2 * t[1], v[2] + 2 * t[2])


def measure(path):
    """-> (sx/sz, |z0|, [renderer fileIDs]).

    `z0` is the height up the limb at which the PRISM sits, in the renderer's own object
    space -- the `_SwayTiming.z` PrismSway stamps. Every `AssembledFlora` seats its prism
    at the spindle ROOT (`localPosition = Vector3.zero`, identity rotation), so it is a
    pure function of where the branch mesh is posed UNDER that root, which is what makes
    it measurable from the prefab alone.

    Every renderer on one branch must agree about both numbers: the gyroid's and the
    quasicrystal's mirrored PAIR is one limb meeting at one prism (Docs/ECOSYSTEM.md
    34.12), so halves that disagreed would move the joint they share by different
    amounts and visibly separate."""
    text, transforms, renderers, spindle = parse_prefab(path)
    ids = spindle_renderer_ids(spindle, path)
    poses = parse_local_poses(path)
    ratios, heights = set(), set()
    for rid in ids:
        go = renderers.get(rid)
        assert go, f"{path.name}: Spindle names renderer {rid}, which is not in this prefab"
        pos, rot, (sx, _, sz) = poses[go]
        assert sz != 0, f"{path.name}: renderer {rid} has zero z scale"
        ratios.add(round(sx / sz, 9))
        # The prism sits at the spindle root, so its height in the renderer's frame is
        # the renderer's own offset taken back through the renderer's rotation and scale.
        back = rotate_inverse(rot, (-pos[0], -pos[1], -pos[2]))
        heights.add(round(abs(back[2] / sz), 6))
    assert len(ratios) == 1, f"{path.name}: renderers disagree about their stretch: {ratios}"
    assert len(heights) == 1, (
        f"{path.name}: mirrored halves seat the prism at different heights: {heights} -- "
        "they would move the joint they share by different amounts")
    return ratios.pop(), heights.pop(), ids


def sway_peak():
    """The peak of the shared two-wave pair, read off the SHIPPED HLSL rather than
    copied, so the number cannot drift from the shader it describes."""
    h = SPINDLE_SWAY.read_text()
    w = float(re.search(r"SPINDLE_SWAY_SECONDARY_WEIGHT\s+([\d.]+)", h).group(1))
    # The two waves ride PERPENDICULAR axes, so the displacement's magnitude is
    # sqrt(sin^2 + w^2 sin'^2) and its supremum is sqrt(1 + w^2) -- reached because the
    # frequency ratio is incommensurable, so the pair eventually gets arbitrarily close
    # to both peaks at once. sqrt(1 + 0.45^2) = 1.09659, which is the 1.0964 PrismSway.cs
    # prices its culling envelope with, measured there off the compiled shader. Derived
    # here rather than copied, so the two cannot drift.
    return __import__("math").sqrt(1.0 + w * w)


def render_material(name, amplitude):
    text = DONOR.read_text()
    assert "m_Name: SpindleMaterial" in text
    text = text.replace("m_Name: SpindleMaterial", f"m_Name: {name}")
    text, n = re.subn(r"- _SwayAmplitude: [-\d.e+]+", f"- _SwayAmplitude: {amplitude:g}", text)
    assert n == 1, f"{name}: expected exactly one _SwayAmplitude in the donor, got {n}"
    return text


def render_meta(name):
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid_for(name)}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 2100000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def repoint(text, path, renderer_ids, new_guid):
    """Point this prefab's spindle renderers at the new material.

    IDEMPOTENT, and it asserts the END STATE rather than counting swaps: a script that
    only counts what it changed reports a clean second run as a failure, and the thing
    worth being sure of is that every named renderer wears the new material, however it
    came to."""
    docs = split_docs(text)
    out, seen = [], set()
    for d in docs:
        if d.startswith("!u!23 "):
            fid = re.search(r"&(\d+)", d).group(1)
            if fid in renderer_ids:
                d = d.replace(f"guid: {DONOR_GUID}", f"guid: {new_guid}")
                assert f"guid: {new_guid}" in d, (
                    f"{path.name}: renderer {fid} wears neither the donor material nor "
                    f"{new_guid} -- it was re-pointed by hand at something else")
                seen.add(fid)
        out.append(d)
    missing = renderer_ids - seen
    assert not missing, f"{path.name}: no MeshRenderer document for {sorted(missing)}"
    return "--- ".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="re-derive everything and fail on drift; write nothing")
    args = ap.parse_args()

    peak = sway_peak()
    target_slope = __import__("math").tan(__import__("math").radians(TARGET_TIP_DEGREES))
    failures, rows = [], []
    writes = {}

    for rel, mat_name in BRANCHES:
        path = REPO / rel
        ratio, z0, ids = measure(path)
        amplitude = round(target_slope / ratio, 4)
        # Tip deflection as a fraction of the limb's own length, and the PRISM's -- the
        # prism rides at a fixed height z0 up the limb, so equal angle makes its sweep the
        # same fraction of that limb in every species, which is what the band asserts.
        tip_fraction = amplitude * ratio * peak
        prism_fraction = tip_fraction * z0
        rows.append((mat_name, path.name, 1.0 / ratio, amplitude, z0, tip_fraction, prism_fraction))

        if not (AMPLITUDE_BAND[0] <= amplitude <= AMPLITUDE_BAND[1]):
            failures.append(f"{mat_name}: amplitude {amplitude} outside {AMPLITUDE_BAND}")
        if not (PRISM_SWEEP_FRACTION_BAND[0] <= prism_fraction <= PRISM_SWEEP_FRACTION_BAND[1]):
            failures.append(
                f"{mat_name}: prism sweep {prism_fraction:.4f} of its limb outside "
                f"{PRISM_SWEEP_FRACTION_BAND}")

        mat_path = MAT_DIR / f"{mat_name}.mat"
        writes[mat_path] = render_material(mat_name, amplitude)
        writes[MAT_DIR / f"{mat_name}.mat.meta"] = render_meta(mat_name)
        writes[path] = repoint(path.read_text(), path, set(ids), guid_for(mat_name))

    # Every derived guid must be unique repo-wide, or Unity resolves one asset as another.
    for _, mat_name in BRANCHES:
        g = guid_for(mat_name)
        owners = [p for p in REPO.joinpath("Assets").rglob("*.meta")
                  if f"guid: {g}" in p.read_text(errors="ignore")]
        owners = [p for p in owners if p.name != f"{mat_name}.mat.meta"]
        if owners:
            failures.append(f"{mat_name}: guid {g} already owned by {owners[0]}")

    print(f"{'material':30s} {'branch':26s} {'z stretch':>9s} {'amplitude':>9s} "
          f"{'prism z0':>8s} {'tip/limb':>8s} {'prism/limb':>10s}")
    for name, branch, stretch, amp, z0, tip, prism in rows:
        print(f"{name:30s} {branch:26s} {stretch:9.4f} {amp:9.4f} {z0:8.4f} "
              f"{tip:7.2%} {prism:9.2%}")
    print(f"\ntarget tip deflection {TARGET_TIP_DEGREES} deg "
          f"(the shared SpindleMaterial's 0.08 on a uniform mesh is "
          f"{__import__('math').degrees(__import__('math').atan(0.08)):.2f} deg)")

    drift = [p for p, body in writes.items()
             if not p.exists() or p.read_text() != body]
    if args.check:
        for p in drift:
            failures.append(f"{p.relative_to(REPO)} differs from what this tool would author")
        if failures:
            print("\nFAIL:")
            for f in failures:
                print(" ", f)
            return 1
        print("\nOK: every lattice branch material matches its derivation.")
        return 0

    if failures:
        print("\nFAIL (nothing written):")
        for f in failures:
            print(" ", f)
        return 1
    for p, body in writes.items():
        p.write_text(body)
    print(f"\nwrote {len(writes)} files ({len(drift)} changed)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
