"""The Marlin: a celestial lancer. A long three-keeled dart that runs flat out and turns wide.

The animal's name means: a hunter of open water that wins by SPEED and commits to a line. Its role is predator, its
behaviour the strafing run, the feeling a thrown spear. It keeps none of the fish: the body is a braided spine with
three swept keels and a long lance trailing behind, three-fold round its travel axis.

For a rider (the Jewel Wasp): the fastest host and the widest turn. Its attack is its own CHARGE (a dash along the aim,
as the worm's lunge is the worm's own): fly the line, strike on the pass, come round again.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 3
spine = K.spindle("Spine", None, [0, 0, -1.5], [0, 0, -30.0])
sting = K.spindle("Sting", "Spine", [0, 0, -30.0], [0, 0, -34.0])
keels, plates, ribs = [], [], []
for k, a in enumerate(K.angles(N)):
    keel = K.spindle(f"Keel{k}", "Spine", K.polar(0, a, -5.0), K.polar(6.5, a, -24.0))
    keels.append(keel)
    plates.append(K.on(keel, f"KeelPlate{k}", "health", 0.72, [0.5, 2.2, 6.0]))
    ribs.append({"name": f"Rib{k}", "spindle": "Spine", "kind": "health", "at": K.polar(1.5, a, -11.0),
                 "size": [0.8, 0.8, 4.5], "axis": [0, 0, 1]})
    ribs.append({"name": f"Flank{k}", "spindle": "Spine", "kind": "health", "at": K.polar(1.5, a, -19.0),
                 "size": [0.7, 0.7, 4.5], "axis": [0, 0, 1]})
prisms = ribs + plates + [{"name": "Lance", "spindle": "Sting", "kind": "danger", "role": "stinger",
                           "at": [0, 0, -39.5], "size": [0.6, 0.6, 9.0], "axis": [0, 0, 1]}]

SPEC = {
    "species": "Marlin",
    "premise": "A celestial lancer of the open HyperSea, grown round its Etherion: three swept keels on a long braided "
               "spine, a lance trailing behind. It is for speed and commitment: a spear that has to be thrown well.",
    "role": "predator",
    "symmetry": N,
    "steering_up": "carried",
    "mouth": "front",
    "spindles": [spine] + keels + [sting],
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 46.0, "mouth_reach": 14.0, "attack": "charge", "turn_rate": 3.2, "for": "sprint",
                   "charge": {"speed_scale": 3.0, "seconds": 0.6},
                   "feel": "Fastest host, widest turn. RT at a vessel is a charge along your aim: line it up."},
}
