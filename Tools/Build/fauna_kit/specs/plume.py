"""The Plume: a celestial feather star, GROWN. Ten feathered arms that curl back round a small cup, beating in turn.

THE REAL ANIMAL (a feather star, Comatulida): a small cup with ten or more feathered arms. It swims by sweeping
alternate arms down while their neighbours rise, so it climbs through the water like a slow firework.
THE HYPERSEA'S: the cup trails the Etherion; every arm is a `rachis` spindle (a shaft and two rows of barbs); and
neighbouring arms sway half a beat apart, so five rise as five fall. Light runs out along each arm, one after another.

For a rider: it is all reach. RT eats on every side through the plumes, and its attack whips all ten arms.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 5
calyx = K.grown("Calyx", None, [[0, 0, -1.6], [0, 0, -3.5], [0, 0, -5.5]], girth=(1.1, 1.5), design="rib")
limbs, prisms = [], []
for k, a in enumerate(K.angles(10, phase=0.0)):
    P = lambda r, z, a=a: K.polar(r, a, z)
    arm = K.grown(f"Arm{k}", "Calyx", [P(1.0, -4.6), P(9.0, -3.2), P(18.0, -8.0), P(23.0, -18.0), P(21.0, -30.0), P(15.0, -38.0)],
                  girth=(0.45, 0.1), design="rachis", strands=2, section=2, step=1.4,
                  barbs={"count": 16, "length": [5.5, 1.2], "angle": 58.0, "sides": 2, "dir": "tangent", "flutter": 0.35},
                  sway={"dir": "radial", "amp": 5.0, "waves": 0.6, "phase": 3.1416 * (k % 2)},
                  glow={"bright": [1.0, 0.6], "waves": 1.5, "phase": 0.63 * k})
    limbs.append(arm)
    at, axis = K.along(arm, 0.42)
    prisms.append({"name": f"Plate{k}", "spindle": f"Arm{k}", "kind": "health", "at": at, "size": [0.5, 1.6, 3.0], "axis": axis})
    prisms.append({"name": f"Sting{k}", "spindle": f"Arm{k}", "kind": "danger", "role": "stinger",
                   "at": P(15.0 - 0.6 * 2.9, round(-38.0 - 0.8 * 2.9, 4)), "size": [0.5, 0.5, 5.0], "axis": P(-6.0, -8.0)})

SPEC = {
    "species": "Plume",
    "premise": "A celestial feather star grown round its Etherion: ten feathered arms curling back from a small cup, "
               "five rising as five fall, light running out along each in turn. It is for wonder: a slow firework "
               "that never finishes going off.",
    "role": "herbivore",
    "symmetry": N,
    "sway_hz": 0.5,
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "Comatulida (feather stars)", "kept": ["ten feathered arms round a cup", "alternate arms beating", "feeding through the plumes"],
              "changed": ["the cup trails the Etherion", "a sting at every arm's tip"], "vessel": "no hull is a feather star"},
    "spindles": [calyx] + limbs,
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 22.0, "mouth_reach": 46.0, "attack": "whip", "turn_rate": 5.0, "body_radius": 22.0, "aim_cone": 180.0,
                   "suction": {"shape": "all"},
                   "feel": "It is all reach: RT eats on every side through its plumes, and its attack whips all ten arms."},
}
