"""SPECIESNAME: (one line: what it is in the HyperSea.)

THE REAL ANIMAL: (two or three shapes a child would draw, and how it moves.)
THE HYPERSEA'S: (what you kept, what you changed and why. /fauna-maker step 1.)

This file is a working creature: `python3 Tools/Build/fauna_kit/fauna.py check <name>` passes on it as it stands.
Change one thing at a time and run the check again.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 3                      # how many times the body repeats round its travel axis (2 = flat with two faces; 3 or more)

# THE CRYSTAL is never written here: it is always at [0, 0, 0], 3.55 across. The body travels along +z, so
# everything trails toward -z. (radius, z) below means: radius out from the travel axis, z along it.

# SPINDLES. A limb is a smooth curve through 3 to 6 points. The first limb grows from the crystal (parent None) and
# must start within 3.2 of it; every other limb names its parent and starts ON that parent (within 1.5 of its curve).
core = K.grown("Core", None, [[0, 0, -1.6], [0, 0, -8.0], [0, 0, -16.0]], girth=(1.0, 0.4), design="core")
tail = K.grown("Tail", "Core", [[0, 0, -16.0], [0, 0, -24.0], [0, 0, -32.0]], girth=(0.3, 0.08), design="whip",
               sway={"dir": [0, 1, 0], "amp": 3.0, "waves": 1.2})
limbs, prisms = [], []
for k, a in enumerate(K.angles(N)):                        # one limb, written once, turned N times
    P = lambda r, z, a=a: K.polar(r, a, z)                 # P(radius, z) at this copy's angle
    arm = K.grown(f"Arm{k}", "Core", [P(1.0, -4.0), P(7.0, -5.0), P(12.0, -10.0), P(13.0, -18.0)], girth=(0.5, 0.12),
                  design="spar", sway={"dir": "radial", "amp": 3.0, "waves": 0.7})
    limbs.append(arm)
    # PRISMS ride a limb. K.along(limb, t) gives a point t of the way down it and the direction it runs there.
    # Size is [thin, wide, long]: long lies along the limb. Keep every side between 0.5 and 10.
    at, axis = K.along(arm, 0.6)
    prisms.append({"name": f"Plate{k}", "spindle": f"Arm{k}", "kind": "health", "at": at, "size": [0.5, 1.6, 3.0], "axis": axis})
# THE STING: one long thin danger prism on a limb nothing grows from, and it must be the body's rearmost point.
prisms.append({"name": "Sting", "spindle": "Tail", "kind": "danger", "role": "stinger", "at": [0, 0, -36.0],
               "size": [0.5, 0.5, 6.0], "axis": [0, 0, 1]})

SPEC = {
    "species": "SPECIESNAME",
    "premise": "(What it is and the feeling it is for, in two sentences.)",
    "role": "herbivore",                 # herbivore / predator / pollinator / omnivore
    "symmetry": N,
    "sway_hz": 0.5,                      # the whole body's one tempo, beats a second
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "(the real animals)", "kept": ["(shape)", "(motion)"], "changed": ["(what the HyperSea changed)"],
              "vessel": "(what the hull of this name already took, or: no hull is this)"},
    "spindles": [core] + limbs + [tail],
    "membranes": [],                     # {"name", "a": limb, "b": limb, "across": 6}: a sheet woven between two limbs
    "prisms": prisms,
    "elements": K.elements(prisms),      # never typed in: Mass and Space are solved from the body
    "possession": {"top_speed": 24.0, "mouth_reach": 30.0, "attack": "whip", "turn_rate": 5.0,
                   "feel": "(one line the rider reads.)"},
}
