"""The Lantern: a celestial comb jelly, GROWN. A clear woven body the Etherion lights from inside, eight comb rows
with light running down them, two long fishing lines.

THE REAL ANIMAL (a comb jelly, Ctenophora): a glassy body with eight rows of beating plates that scatter light into
moving rainbows, and two long branching tentacles it fishes with. It does not sting; it glides on its combs.
THE HYPERSEA'S: the body is woven over the Etherion (the relaxed L1), each row is a `comb` spindle whose light pulses
run from front to back, each row a beat behind its neighbour, and the two lines trail far behind with a sting at
the end of each. Two-fold, as the animal is.

For a rider: the smoothest swimmer here, quick to turn. With two lines and no arms the game will lunge it, not whip it.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 2
gullet = K.grown("Gullet", None, [[0, 0, -1.6], [0, 0, -12.0], [0, 0, -24.0]], girth=(0.8, 0.3), design="rib")
limbs, membranes, prisms = [], [], []
for k, a in enumerate(K.angles(8, phase=22.5)):
    P = lambda r, z, a=a: K.polar(r, a, z)
    comb = K.grown(f"Comb{k}", None, [P(0.4, 2.6), P(5.0, 1.5), P(8.5, -5.0), P(9.0, -14.0), P(6.5, -23.0), P(1.2, -28.0)],
                   girth=(0.22, 0.18), design="comb",
                   barbs={"count": 14, "length": [1.5, 1.2], "angle": 88.0, "sides": 2, "dir": "tangent", "flutter": 0.6, "from": 0.12, "to": 0.9},
                   sway={"dir": "radial", "amp": 0.9, "waves": 0.5}, glow={"bright": [1.0, 0.9], "waves": 4.0, "phase": 0.8 * k})
    limbs.append(comb)
    membranes.append({"name": f"Skin{k}", "a": f"Comb{k}", "b": f"Comb{(k + 1) % 8}", "across": 5})
    at, axis = K.along(comb, 0.55)
    prisms.append({"name": f"Plate{k}", "spindle": f"Comb{k}", "kind": "health", "at": at, "size": [0.5, 1.4, 2.6], "axis": axis})
for k, a in enumerate(K.angles(2, phase=0.0)):
    P = lambda r, z, a=a: K.polar(r, a, z)
    limbs.append(K.grown(f"Line{k}", "Gullet", [P(0.8, -20.0), P(7.0, -27.0), P(13.0, -40.0), P(11.0, -58.0), P(15.0, -78.0)],
                         girth=(0.16, 0.04), design="tendril", sway={"dir": "tangent", "amp": 6.0, "waves": 2.2, "phase": 1.6 * k}))
    prisms.append({"name": f"Sting{k}", "spindle": f"Line{k}", "kind": "danger", "role": "stinger",
                   "at": P(15.0 + 4.0 / 20.396 * 3.3, round(-78.0 - 20.0 / 20.396 * 3.3, 4)), "size": [0.5, 0.5, 6.0], "axis": P(4.0, -20.0)})

SPEC = {
    "species": "Lantern",
    "premise": "A celestial comb jelly grown round its Etherion: a clear woven body lit from inside, eight rows of "
               "beating combs with light running down them, two long lines trailing behind. It is for quiet: the "
               "thing you stop to watch.",
    "role": "omnivore",
    "symmetry": N,
    "lead": 2.8,
    "sway_hz": 0.6,
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "Ctenophora (comb jellies)", "kept": ["eight comb rows with light running down them", "a clear body", "two fishing lines"],
              "changed": ["the body is woven over the Etherion, which lights it", "the lines end in stings"], "vessel": "no hull is a comb jelly"},
    "spindles": [gullet] + limbs,
    "membranes": membranes,
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 28.0, "mouth_reach": 32.0, "attack": "lunge", "turn_rate": 7.0,
                   "feel": "The smoothest swimmer here and quick to turn. Watch the light run down its combs."},
}
