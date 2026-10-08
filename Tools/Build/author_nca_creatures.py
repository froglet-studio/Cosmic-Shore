#!/usr/bin/env python3
"""Author the NCA CREATURES - the lab's trained 3D neural-cellular-automaton animals, as game fauna.   (Docs/NCA_CREATURES.md)

    python3 Tools/Build/author_nca_creatures.py            # write
    python3 Tools/Build/author_nca_creatures.py --check    # FAIL on any drift (reads the disk)

What it owns, per species in SPECIES (one row per animal; the lizard today, the whale and the jellyfish once their
trainings finish - add a row, run this, and the same runtime grows them):
  * <Species>NcaWeights.json - the trained network re-packed for NcaVoxelWeights.Parse (base64 float32, row-major
    [out, in]) with the body frame (forward, offset), the render tint and the harness-measured grown size;
  * <Species>NcaConfig.asset - the NcaCreatureConfigSO every individual reads (every gameplay number);
  * Nca<Species>Fauna.prefab - the creature: one GameObject with NcaCreatureFauna (its heart is provisioned from the
    element at spawn, its body is the NCA's voxels drawn as prism entities - no authored mesh, no collider);
  * Nca<Species> <Element> Fauna Config Data.asset x4 - the FaunaConfigurationSOs (one per element) a spawn profile
    or the Spawn Matrix lists;
  * the .meta of every script, asset and prefab (stable guids, one owner each).
And for the Swarm demo cell (author_swarm_fauna.py composes these through cell_profile_entries / colliders):
  * Swarm <Band> Nca<Species> Fauna Config Data.asset - the cell's population of the species, penned in a band
    over the flora it eats.

The weights come from the research branch (RESEARCH_REF) when it is reachable; otherwise the committed weights are
kept and checked for shape only - exactly how swarm_plans.py treats the swarm's body plans.

The grown size (GrownVoxels) and the steps to grow it (GrowSteps) are MEASURED by Tools/Build/nca_creature_harness
(`run.sh measure`); the harness asserts the numbers written here still hold.
"""
import argparse
import base64
import hashlib
import json
import math
import os
import struct
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
assert os.path.isdir(os.path.join(REPO, "Assets")), REPO

A = lambda *p: os.path.join(REPO, "Assets", *p)
RESEARCH_REF = "origin/cece/gifted-curie-x2cpd0"
RESEARCH_DIR = "Tools/NCA/results"
SO_DIR = A("_SO_Assets", "NCA Creatures")
PREFAB_DIR = A("_Prefabs", "FloraAndFauna")
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "NcaCreature")
SCRIPTS = ["NcaVoxelCore", "NcaCreatureConfigSO", "NcaCreatureFauna"]

ELEMENTS = [("Charge", 1), ("Mass", 2), ("Space", 3), ("Time", 4)]

# One row per animal. `run` is the research export (Tools/NCA/results/<run>/weights.json); `forward` / `offset` are the
# body frame the lab runtime uses for it (nca_creature.src.js; export_nca3d_creature.py); `grown` / `grow_steps` are
# what Tools/Build/nca_creature_harness measured (`run.sh measure`), and it re-asserts them.
SPECIES = [
    dict(name="Lizard", run="lizard3d_swim", forward=(-0.6148, -0.7887), offset=(-1.39, -0.72), tint=0.65,
         grown=1143, grow_steps=50),
    # PENDING - the whale (16x26x39) and the jellyfish (22x22x30) share this runtime unchanged; their trainings are
    # being resumed (whale 4850/6000, jelly 3250/5600 steps). When they finish: export (Tools/NCA/export_nca3d_
    # creature.py), measure their body frame (forward/offset - the lab reuses the lizard's offset for both, which is
    # wrong), add a row here, run `nca_creature_harness/run.sh measure`, then this script.
]


def rel(p):
    return os.path.relpath(p, REPO).replace(os.sep, "/")


def guid(path_rel):
    """Stable guid from the asset's repo path (one owner each, the author_swarm_fauna.py convention)."""
    return hashlib.md5(("nca-creatures:" + path_rel).encode()).hexdigest()


def read(p):
    with open(p, encoding="utf-8") as fh:
        return fh.read()


def research_weights(run):
    """The research export, or None when the research ref is not reachable here."""
    try:
        text = subprocess.run(["git", "-C", REPO, "show", f"{RESEARCH_REF}:{RESEARCH_DIR}/{run}/weights.json"],
                              capture_output=True, text=True, check=True).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return None
    return json.loads(text)


def b64(values):
    return base64.b64encode(struct.pack("<%df" % len(values), *values)).decode("ascii")


def flat(m):
    return [float(x) for row in m for x in row] if m and isinstance(m[0], list) else [float(x) for x in m]


def weights_path(sp):
    return os.path.join(SO_DIR, f"{sp['name']}NcaWeights.json")


def weights_json(sp, w):
    C, HID = int(w["channel_n"]), int(w["hidden"])
    w1, b1, w2, b2 = flat(w["w1"]), flat(w["b1"]), flat(w["w2"]), flat(w["b2"])
    assert len(w1) == HID * 4 * C and len(b1) == HID and len(w2) == C * HID and len(b2) == C, sp["name"]
    out = [
        "{",
        f'  "species": "{sp["name"]}",',
        f'  "source": "{RESEARCH_REF}:{RESEARCH_DIR}/{sp["run"]}/weights.json",',
        f'  "channel_n": {C},',
        f'  "hidden": {HID},',
        f'  "fire_rate": {float(w["fire_rate"])},',
        f'  "D": {int(w["D"])},',
        f'  "H": {int(w["H"])},',
        f'  "W": {int(w["W"])},',
        f'  "forward_x": {sp["forward"][0]},',
        f'  "forward_y": {sp["forward"][1]},',
        f'  "offset_x": {sp["offset"][0]},',
        f'  "offset_y": {sp["offset"][1]},',
        f'  "tint": {sp["tint"]},',
        f'  "grown_voxels": {sp["grown"]},',
        f'  "grow_steps": {sp["grow_steps"]},',
        f'  "w1": "{b64(w1)}",',
        f'  "b1": "{b64(b1)}",',
        f'  "w2": "{b64(w2)}",',
        f'  "b2": "{b64(b2)}"',
        "}",
    ]
    return "\n".join(out) + "\n"


def committed_weights_ok(path):
    """Shape check of a committed weights asset (the research ref is not reachable)."""
    if not os.path.exists(path):
        return [f"{rel(path)} is missing and the research ref {RESEARCH_REF} is not reachable to author it"]
    w = json.loads(read(path))
    C, HID = w["channel_n"], w["hidden"]
    n = lambda k: len(base64.b64decode(w[k])) // 4
    want = {"w1": HID * 4 * C, "b1": HID, "w2": C * HID, "b2": C}
    return [f"{rel(path)}: {k} holds {n(k)} floats, expected {v}" for k, v in want.items() if n(k) != v]


TEXT_META = """fileFormatVersion: 2
guid: %s
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def emit():
    """Every file this script owns, as {path: text}, plus problems found while building them."""
    out, problems, baked = {}, [], []
    for sp in SPECIES:
        wp = weights_path(sp)
        w = research_weights(sp["run"])
        if w is not None:
            out[wp] = weights_json(sp, w)
            baked.append(sp["name"])
        else:
            problems += committed_weights_ok(wp)
            if os.path.exists(wp):
                out[wp] = read(wp)
        out[wp + ".meta"] = TEXT_META % guid(rel(wp))
    return out, problems, baked


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    out, problems, baked = emit()
    print(f"NCA creatures: {', '.join(s['name'] for s in SPECIES)}; weights "
          f"{'re-baked from ' + RESEARCH_REF + ' for ' + ', '.join(baked) if baked else 'kept (research ref not reachable)'}")
    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {d}" for d in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every NCA creature asset matches what this script authors.")
        return 0
    if problems:
        print("\nFAIL - refusing to write:")
        for p in problems:
            print("  - " + p)
        return 1
    for p, t in out.items():
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    print(f"\nwrote {len(out)} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
