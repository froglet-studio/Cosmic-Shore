"""The Teredo: a celestial borer. Three braided flutes wind round a long spine like the thread of a drill.

The animal's name means: the shipworm, the small patient thing that eats a hull from the inside. Its role is omnivore,
its behaviour tunnelling, the feeling of something getting in. The body is an auger: a long spine and three flutes of
four braids each winding round it, three-fold round the travel axis, a needle trailing behind.

For a rider: a quick swimmer whose strike LATCHES (its own attack trait, as the game's plate latch is the wild
creatures'): land one strike on a vessel and it rides the hull, striking again at twice the pace, for six seconds (then it is flung off and
rests five) or until you burst off (LT) or leave. It is the host for the vessel that comes at you: let it pass close, and go with it.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 3
spine = K.spindle("Spine", None, [0, 0, -1.5], [0, 0, -34.0])
sting = K.spindle("Sting", "Spine", [0, 0, -34.0], [0, 0, -37.5])
limbs, prisms = [], []
TWIST = math.radians(52.0)          # each flute segment winds this far round the axis
STEPS = [(0.0, -4.0), (4.2, -10.0), (5.0, -17.5), (4.6, -25.0), (3.2, -32.0)]   # (radius, z)
for k, a in enumerate(K.angles(N)):
    pts = [K.polar(r, a + TWIST * i, z) for i, (r, z) in enumerate(STEPS)]
    flute = K.chain(f"Flute{k}", "Spine", pts, girth=0.9)
    limbs += flute
    prisms.append(K.on(flute[1], f"Scale{k}a", "health", 0.5, [0.6, 1.8, 4.2], out=0.6))
    prisms.append(K.on(flute[2], f"Scale{k}b", "health", 0.5, [0.6, 1.8, 4.2], out=0.6))
    prisms.append(K.on(flute[3], f"Scale{k}c", "health", 0.5, [0.6, 1.5, 3.8], out=0.6))
prisms.append({"name": "Needle", "spindle": "Sting", "kind": "danger", "role": "stinger",
               "at": [0, 0, -42.5], "size": [0.6, 0.6, 9.0], "axis": [0, 0, 1]})

SPEC = {
    "species": "Teredo",
    "premise": "A celestial borer, grown round its Etherion: three braided flutes wind round a long spine like the "
               "thread of a drill. It is for patience and intrusion: the thing that gets inside a hull and eats.",
    "role": "omnivore",
    "symmetry": N,
    "steering_up": "carried",
    "mouth": "front",
    "spindles": [spine] + limbs + [sting],
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 30.0, "mouth_reach": 30.0, "attack": "whip", "turn_rate": 4.5, "for": "brawl",
                   "latch": {"seconds": 6.0, "rest": 5.0, "recharge_scale": 0.5},
                   "feel": "Its strike LATCHES: land one hit and it rides the hull for 6 s, striking twice as often. LT bursts off."},
}
