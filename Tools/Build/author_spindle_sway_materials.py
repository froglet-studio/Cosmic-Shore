#!/usr/bin/env python3
"""
Author the spindle-sway tuning onto the materials that should sway, and ONLY those.

Tools/Shaders/wire_spindle_sway.py gives SpindleGraph the CAPABILITY; this decides who
uses it. The two are deliberately separate steps because the graph's `_SwayAmplitude`
defaults to 0, which makes the splice a provable no-op (verify_spindle_sway.py T2) —
so a material sways exactly when a human wrote a number into it, and the blast radius
of a LOCKED-area graph change is an authored list rather than a side effect.

SpindleGraph is worn by five materials. Three are not spindles and are left at the
default 0, unchanged:

    BranchingMembraneMaterial   BranchingMembrane.prefab
    FireProjectileMaterial      SparrowExhaustProjectile.prefab
    BlueProjectileMaterial      no users
    JadeSpindleMaterial         no users

WHY THE QUADFISH GETS ITS OWN MATERIAL.
SpindleMaterial is shared by twelve prefabs — every flora branch, the shark's wings,
the brittlestar's arms, the worm segments and the QuadFish. A fern and a fish do not
sway alike: a plant wants a slow gentle drift, a swimming fish wants a tail beat near
1 Hz, and one frequency cannot be both without the plant looking like it is buzzing.
FREQUENCY is the obvious thing that differs per creature, and a second material is the
cheapest honest way to say so. Spindle.cs caches its 8 phase variants PER BASE MATERIAL,
so a second base material costs 8 more shared materials and no batching.

AMPLITUDE IS NOT UNIT-FREE ACROSS A NON-UNIFORMLY SCALED MESH, and an earlier version of
this note said it was. The shear is authored in OBJECT space, so a renderer carrying
localScale (1, 1, sz) bends by atan(Amplitude * sx / sz) in WORLD terms — the same slope
on a mesh stretched along its own bend axis buys that much less visible bend. Measured
over every spindle renderer in the project at the 0.08 below: TadpoleSpindle and the worm
segments are uniform and lean 4.57 degrees, while Branch and AssemblyBranch (1, 1, 6.2)
lean 0.74 and QuasicrystalBranch (1, 1, 7.1288) leans 0.64. The three LATTICE branch
meshes are therefore re-dressed with their own materials, each solved for one authored
world bend, by Tools/Build/author_lattice_spindle_materials.py — see Docs/ECOSYSTEM.md
47.7. The ordinary flora Branch is deliberately left on this material's 0.08, stated
rather than silently changed: it is a look call nobody has asked for.

Numbers, and where they come from:

  SpindleMaterial            0.08 / 1.4 rad/s   ~4.6 deg of lean at 0.22 Hz — a plant
                                                 breathing, not a plant in a gale.
  QuadFishSpindleMaterial    0.13 / 5.2 rad/s   0.83 Hz tail beat. The body mesh spans
                                                 z -217.1 .. +132.0 (measured off
                                                 mediumfish.fbx), so the shear pivots
                                                 about mid-body and the tail travels
                                                 1.64x as far as the head — a fish, for
                                                 free, out of the geometry. Peak lateral
                                                 excursion 1.097 x 0.13 x 217 = 31 units
                                                 on a 349-long body, i.e. ~9% of body
                                                 length, which is about what a real fish
                                                 tail beat is worth.

Usage:  python3 Tools/Build/author_spindle_sway_materials.py [--check]
"""

import hashlib
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MATERIALS = "Assets/_Graphics/Materials"
SPINDLE_MAT = f"{MATERIALS}/SpindleMaterial.mat"
QUADFISH_MAT = f"{MATERIALS}/QuadFishSpindleMaterial.mat"
QUADFISH_PREFAB = "Assets/_Prefabs/FloraAndFauna/QuadFish.prefab"

SPINDLE_GUID = "4f44fa5c7514a2c45b5af7f45bc51acd"
QUADFISH_MAT_GUID = hashlib.md5(b"CosmicShore/SpindleSway/QuadFishSpindleMaterial.mat").hexdigest()

# material path -> (amplitude, frequency)
TUNING = {
    SPINDLE_MAT:  (0.08, 1.4),
    QUADFISH_MAT: (0.13, 5.2),
}

AMPLITUDE_KEY = "_SwayAmplitude"
FREQUENCY_KEY = "_SwayFrequency"


def read(path):
    with open(os.path.join(REPO, path), encoding="utf-8") as fh:
        return fh.read()


def write(path, text):
    with open(os.path.join(REPO, path), "w", encoding="utf-8") as fh:
        fh.write(text)


def fmt(v):
    """Unity writes a float with no trailing zeros ('0.08', not '0.080000')."""
    return repr(round(v, 6)).rstrip("0").rstrip(".") if isinstance(v, float) else str(v)


def set_float(text, key, value, label):
    """Insert or update one `- <key>: <value>` row in m_Floats, keeping Unity's sort.

    m_Floats is alphabetically ordered; Unity re-sorts on save either way, but writing
    it sorted keeps the next editor round-trip from showing a phantom diff.
    """
    existing = re.search(rf"^(    - {re.escape(key)}: ).*$", text, flags=re.M)
    if existing:
        return re.sub(rf"^    - {re.escape(key)}: .*$", f"    - {key}: {fmt(value)}",
                      text, count=1, flags=re.M)

    floats = re.search(r"^    m_Floats:\n((?:    - \w+: .*\n)+)", text, flags=re.M)
    assert floats, f"{label}: no m_Floats block"
    rows = floats.group(1).splitlines(keepends=True)
    row = f"    - {key}: {fmt(value)}\n"
    at = len(rows)
    for i, r in enumerate(rows):
        name = re.match(r"    - (\w+):", r).group(1)
        if name > key:
            at = i
            break
    rows.insert(at, row)
    return text[:floats.start(1)] + "".join(rows) + text[floats.end(1):]


def get_float(text, key):
    m = re.search(rf"^    - {re.escape(key)}: (.*)$", text, flags=re.M)
    return None if m is None else float(m.group(1))


def make_quadfish_material(spindle_text):
    """A flat COPY of SpindleMaterial, not a variant.

    The repo's own convention for 'the same thing with different numbers'
    (/asset-surgery §3). A copy keeps every render-state property and every shader
    KEYWORD that SpindleMaterial carries — _ALPHATEST_ON and _SURFACE_TYPE_TRANSPARENT
    are not derivable from the graph's property defaults, so minting a fresh material
    from the shader would silently lose them.
    """
    text = spindle_text.replace("m_Name: SpindleMaterial", "m_Name: QuadFishSpindleMaterial")
    assert "m_Name: QuadFishSpindleMaterial" in text, "rename anchor missed"
    return text


def guid_owner_count(guid):
    out = subprocess.run(["grep", "-rl", f"^guid: {guid}$", "Assets", "--include=*.meta"],
                         cwd=REPO, capture_output=True, text=True).stdout.split()
    return out


def check():
    problems = []

    for path, (amp, freq) in TUNING.items():
        if not os.path.exists(os.path.join(REPO, path)):
            problems.append(f"{path}: missing")
            continue
        text = read(path)
        got = (get_float(text, AMPLITUDE_KEY), get_float(text, FREQUENCY_KEY))
        if got != (amp, freq):
            problems.append(f"{path}: sway is {got}, expected {(amp, freq)}")

    # The three non-spindle SpindleGraph materials must NOT have opted in.
    for name in ("BranchingMembraneMaterial", "FireProjectileMaterial",
                 "BlueProjectileMaterial", "JadeSpindleMaterial"):
        p = f"{MATERIALS}/{name}.mat"
        if not os.path.exists(os.path.join(REPO, p)):
            continue
        text = read(p)
        if get_float(text, AMPLITUDE_KEY):
            problems.append(f"{p}: authored a non-zero {AMPLITUDE_KEY} — it is not a spindle")

    owners = guid_owner_count(QUADFISH_MAT_GUID)
    if len(owners) != 1:
        problems.append(f"{QUADFISH_MAT}: guid owned by {len(owners)} .meta files: {owners}")

    prefab = read(QUADFISH_PREFAB)
    if QUADFISH_MAT_GUID not in prefab:
        problems.append(f"{QUADFISH_PREFAB}: body renderer does not use QuadFishSpindleMaterial")
    if SPINDLE_GUID in prefab:
        problems.append(f"{QUADFISH_PREFAB}: still references SpindleMaterial")

    return problems


def main():
    check_only = "--check" in sys.argv

    if check_only:
        problems = check()
        for p in problems:
            print("FAIL " + p, file=sys.stderr)
        if problems:
            sys.exit(1)
        print("author_spindle_sway_materials: OK")
        return

    # 1. SpindleMaterial opts in.
    text = read(SPINDLE_MAT)
    amp, freq = TUNING[SPINDLE_MAT]
    text = set_float(text, AMPLITUDE_KEY, amp, "SpindleMaterial")
    text = set_float(text, FREQUENCY_KEY, freq, "SpindleMaterial")
    write(SPINDLE_MAT, text)
    print(f"  SpindleMaterial.mat: sway {amp} / {freq}")

    # 2. QuadFishSpindleMaterial — a flat copy at the fish's own tempo.
    owners = guid_owner_count(QUADFISH_MAT_GUID)
    assert owners in ([], [f"{QUADFISH_MAT}.meta"]), f"guid collision: {owners}"
    qf = make_quadfish_material(read(SPINDLE_MAT))
    amp, freq = TUNING[QUADFISH_MAT]
    qf = set_float(qf, AMPLITUDE_KEY, amp, "QuadFishSpindleMaterial")
    qf = set_float(qf, FREQUENCY_KEY, freq, "QuadFishSpindleMaterial")
    write(QUADFISH_MAT, qf)
    write(QUADFISH_MAT + ".meta",
          "fileFormatVersion: 2\n"
          f"guid: {QUADFISH_MAT_GUID}\n"
          "NativeFormatImporter:\n"
          "  externalObjects: {}\n"
          "  mainObjectFileID: 2100000\n"
          "  userData: \n"
          "  assetBundleName: \n"
          "  assetBundleVariant: \n")
    print(f"  QuadFishSpindleMaterial.mat: sway {amp} / {freq}  guid {QUADFISH_MAT_GUID}")

    # 3. Point the QuadFish body renderer at it.
    prefab = read(QUADFISH_PREFAB)
    before = prefab.count(SPINDLE_GUID)
    assert before == 1, f"{QUADFISH_PREFAB}: expected 1 SpindleMaterial reference, found {before}"
    prefab = prefab.replace(SPINDLE_GUID, QUADFISH_MAT_GUID)
    write(QUADFISH_PREFAB, prefab)
    print(f"  QuadFish.prefab: body renderer -> QuadFishSpindleMaterial")

    problems = check()
    for p in problems:
        print("FAIL " + p, file=sys.stderr)
    if problems:
        sys.exit(1)
    print("author_spindle_sway_materials: OK")


if __name__ == "__main__":
    main()
