"""The Nautilus: a celestial pulse-jet. Four broad sails open and close round a braided spine.

The animal's name means: a swimmer that moves in beats, not in a line. Its role is omnivore, its behaviour the dart
and the hang, the feeling of a heartbeat. The body is an umbrella: four sails flare from the spine and four tines
trail behind, four-fold round the travel axis.

For a rider: it swims in PULSES (its own motion: a kick, then a glide in which it can turn almost on the spot), and
LT's burst is one great kick. It is the host that out-turns a vessel.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 4
spine = K.spindle("Spine", None, [0, 0, -1.5], [0, 0, -15.0])
sting = K.spindle("Sting", "Spine", [0, 0, -15.0], [0, 0, -18.5])
limbs, prisms = [], []
for k, a in enumerate(K.angles(N, phase=45.0)):
    sail = K.spindle(f"Sail{k}", "Spine", K.polar(0, a, -3.5), K.polar(10.0, a, -9.5))
    tine = K.spindle(f"Tine{k}", "Spine", K.polar(0, a, -12.0), K.polar(4.5, a, -21.0), girth=0.8)
    limbs += [sail, tine]
    prisms.append(K.on(sail, f"SailPlate{k}", "health", 0.66, [0.5, 4.2, 5.2]))
    prisms.append(K.on(tine, f"TinePlate{k}", "health", 0.7, [0.6, 1.2, 3.6]))
prisms.append({"name": "Needle", "spindle": "Sting", "kind": "danger", "role": "stinger",
               "at": [0, 0, -23.5], "size": [0.5, 0.5, 7.0], "axis": [0, 0, 1]})

SPEC = {
    "species": "Nautilus",
    "premise": "A celestial pulse-jet, grown round its Etherion: four broad sails beat round a braided spine and four "
               "tines trail behind. It is for rhythm: a creature that moves on the beat and turns between beats.",
    "role": "omnivore",
    "symmetry": N,
    "steering_up": "carried",
    "mouth": "front",
    "spindles": [spine] + limbs + [sting],
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 34.0, "mouth_reach": 22.0, "attack": "whip", "turn_rate": 5.0, "for": "slalom",
                   "motion": {"kind": "pulse", "period": 0.9, "kick": 2.2, "glide": 0.6, "glide_turn": 20.0},
                   "feel": "Swims in beats: a kick, then a glide where it turns on the spot. Turn in the glide, strike on the kick."},
}
