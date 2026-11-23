"""The Basker: a celestial filter feeder. A wide six-fold basket that drifts through mass and swallows it.

The animal's name means: a slow giant that feeds by moving through its food with its mouth open. Its role is
herbivore, its behaviour grazing, the feeling a tide coming in. The body is an open basket: six hoops flare from a
short braided spine and six combs trail back from them, the Etherion at the mouth of the funnel.

For a rider: the slowest host and the biggest mouthful. Its suction is its own FUNNEL (everything inside the basket's
cone ahead, twice the bites), so it clears a cluster by flying through it.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 6
spine = K.spindle("Spine", None, [0, 0, -1.5], [0, 0, -26.0])
sting = K.spindle("Sting", "Spine", [0, 0, -26.0], [0, 0, -30.0])
limbs, prisms = [], []
for k, a in enumerate(K.angles(N)):
    hoop = K.spindle(f"Hoop{k}", "Spine", K.polar(0, a, -3.0), K.polar(15.0, a, -13.0))
    comb = K.spindle(f"Comb{k}", f"Hoop{k}", K.polar(15.0, a, -13.0), K.polar(13.0, a, -30.0))
    limbs += [hoop, comb]
    prisms.append(K.on(hoop, f"HoopPlate{k}", "health", 0.62, [0.6, 3.0, 5.0]))
    prisms.append(K.on(comb, f"CombPlate{k}", "health", 0.55, [0.6, 2.4, 7.0]))
prisms.append({"name": "Barb", "spindle": "Sting", "kind": "danger", "role": "stinger",
               "at": [0, 0, -35.0], "size": [0.7, 0.7, 9.0], "axis": [0, 0, 1]})

SPEC = {
    "species": "Basker",
    "premise": "A celestial filter feeder, grown round its Etherion: six hoops flare into an open basket and six combs "
               "trail from them. It is for calm appetite: a tide that comes in over a field of mass and leaves nothing.",
    "role": "herbivore",
    "symmetry": N,
    "steering_up": "carried",
    "mouth": "front",
    "spindles": [spine] + limbs + [sting],
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 17.0, "mouth_reach": 95.0, "attack": "whip", "turn_rate": 2.6, "for": "graze",
                   "suction": {"shape": "funnel", "half_angle": 55.0, "bites_scale": 2.0},
                   "feel": "Slowest host, biggest mouthful. RT at prisms swallows the whole cone ahead: fly THROUGH the mass."},
}
