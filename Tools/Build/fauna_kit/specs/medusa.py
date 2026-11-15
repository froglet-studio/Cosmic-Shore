"""The Medusa: a celestial lasher. Five long braided lashes trail a small crown, a sting on every tip.

The animal's name means: a drifting thing that is dangerous at arm's length, in every direction at once. Its role is
predator, its behaviour the ambush in a crowd, the feeling of being reached for. The body is all limb: a short crown
spindle and five lashes of four braids each, five-fold round the travel axis.

For a rider: a middling swimmer with the longest reach. Its attack is the WHIP every rigged body plays
(FaunaAttackDriver finds its five lashes as chains), its body radius is its lashes' span, and it needs no aim cone:
the strike lands on a vessel anywhere inside the lashes, ahead, beside or behind. Its suction is its own too: it pulls
from every side at once (what the wasp's Maw upgrade gives any host), so it eats a hull from the inside.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 5
crown = K.spindle("Crown", None, [0, 0, -1.5], [0, 0, -8.0])
limbs, prisms = [], []
PATH = [(0.0, -6.0), (6.0, -12.0), (9.5, -20.0), (10.5, -29.0), (9.0, -38.0)]   # (radius, z) down one lash
for k, a in enumerate(K.angles(N)):
    lash = K.chain(f"Lash{k}", "Crown", [K.polar(r, a, z) for r, z in PATH], girth=0.9)
    limbs += lash
    prisms.append(K.on(lash[1], f"Bead{k}a", "health", 0.5, [0.8, 1.6, 4.0]))
    prisms.append(K.on(lash[2], f"Bead{k}b", "health", 0.5, [0.7, 1.4, 4.0]))
    # the sting continues its lash's last braid: stated in (radius, z) and turned with the lash, so the five are exact copies
    (r0, z0), (r1, z1) = PATH[3], PATH[4]
    n = ((r1 - r0) ** 2 + (z1 - z0) ** 2) ** 0.5
    prisms.append({"name": f"Sting{k}", "spindle": lash[3]["name"], "kind": "danger", "role": "stinger",
                   "at": K.polar(r1 + (r1 - r0) / n * 4.2, a, round(z1 + (z1 - z0) / n * 4.2, 4)), "size": [0.5, 0.5, 7.0],
                   "axis": K.polar(r1 - r0, a, z1 - z0)})

SPEC = {
    "species": "Medusa",
    "premise": "A celestial lasher, grown round its Etherion: five long braided lashes trail a small crown, a sting on "
               "every tip. It is for menace at arm's length: nothing is safe inside the reach of its limbs.",
    "role": "predator",
    "symmetry": N,
    "steering_up": "carried",
    "mouth": "front",
    "spindles": [crown] + limbs,
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 24.0, "mouth_reach": 85.0, "attack": "whip", "turn_rate": 7.0, "for": "siege",
                   "suction": {"shape": "all"},
                   "body_radius": 20.0, "aim_cone": 180.0,
                   "feel": "It works all round: RT eats prisms on every side of it and whips a vessel anywhere inside its lashes. Get INSIDE things."},
}
