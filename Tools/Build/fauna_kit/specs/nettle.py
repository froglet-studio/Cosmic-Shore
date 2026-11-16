"""The Nettle: a celestial jelly, GROWN. A woven bell over the Etherion, long tendrils and frilled arms trailing.

THE REAL ANIMAL (a sea nettle, Chrysaora): a bell that pulses, a ring of long stinging tentacles from its rim, frilled
oral arms from its centre, and no brain: it drifts and it stings what touches it.
THE HYPERSEA'S: the bell is a wide shallow parasol (wider than the animal's: a narrower bell with a train read as the
Clawfish's outline, 0.24 apart, and the silhouette gate refused it; this one stands 0.39 from the Rhino hull against a bar of 0.41, so
it still FAILS the gate by a little, and a longer train made it worse: 0.37), woven wire over the Etherion, so the light it is grown round shows through it (the relaxed
L1: the bell leads the heart). Eight ribs carry the bell; eight tendrils fall from its rim, each ending in a sting;
four frilled arms hang from the stalk beneath the heart. The bell's pulse and every tendril's wave are sway, not keys.

For a rider: it swims in pulses and it works all round. RT eats on every side of it and whips a vessel anywhere inside
its tendrils.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 4
stalk = K.grown("Stalk", None, [[0, 0, -1.6], [0, 0, -6.0], [0, 0, -11.0]], girth=(1.1, 0.55), design="core")
limbs, membranes, prisms = [], [], []
pulse = {"dir": "radial", "amp": 2.6, "waves": 0.3, "pow": 1.2}
A8 = K.angles(8, phase=0.0)
for k, a in enumerate(A8):
    P = lambda r, z, a=a: K.polar(r, a, z)
    rib = K.grown(f"Rib{k}", None, [P(0.35, 3.0), P(7.0, 2.8), P(14.0, 1.2), P(18.5, -2.5), P(19.5, -6.5), P(18.0, -9.5)],
                  girth=(0.3, 0.16), design="rib", section=2, sway=pulse)
    tendril = K.grown(f"Tendril{k}", f"Rib{k}", [P(18.0, -9.5), P(21.0, -19.0), P(19.0, -31.0), P(23.0, -44.0), P(21.0, -56.0)],
                      girth=(0.2, 0.05), design="tendril", barbs={"count": 10, "length": [1.6, 0.5], "angle": 95.0, "sides": 1, "flutter": 0.5},
                      sway={"dir": "tangent", "amp": 4.5, "waves": 2.0, "phase": 0.8 * k})
    limbs += [rib, tendril]
    membranes.append({"name": f"Bell{k}", "a": f"Rib{k}", "b": f"Rib{(k + 1) % 8}", "across": 6})
    at, axis = K.along(rib, 0.8)
    prisms.append({"name": f"RimPlate{k}", "spindle": f"Rib{k}", "kind": "health", "at": at, "size": [0.5, 1.6, 2.8], "axis": axis})
    prisms.append({"name": f"Sting{k}", "spindle": f"Tendril{k}", "kind": "danger", "role": "stinger",
                   "at": P(21.0 - 2.0 / 12.166 * 3.3, round(-56.0 - 12.0 / 12.166 * 3.3, 4)), "size": [0.5, 0.5, 6.0], "axis": P(-2.0, -12.0)})
for k, a in enumerate(K.angles(4, phase=22.5)):
    P = lambda r, z, a=a: K.polar(r, a, z)
    limbs.append(K.grown(f"Frill{k}", "Stalk", [P(0.5, -10.5), P(2.8, -17.0), P(1.6, -27.0), P(3.2, -38.0)], girth=(0.9, 0.2), design="frill", strands=3, sway={"dir": "radial", "amp": 2.6, "waves": 1.5, "phase": 1.1 * k}))

SPEC = {
    "species": "Nettle",
    "premise": "A celestial jelly grown round its Etherion: a woven bell the light shows through, eight tendrils falling "
               "from its rim and four frilled arms beneath. It pulses and it drifts, and everything it trails stings. "
               "It is for dread and beauty at once: you want to watch it, and you do not want it near.",
    "role": "predator",
    "symmetry": N,
    "lead": 3.2,
    "sway_hz": 0.55,
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "Chrysaora (sea nettles)", "kept": ["the pulsing bell", "tendrils from the rim that sting", "frilled arms from the centre"],
              "changed": ["the bell is woven over the Etherion, which shows through it", "eight ribs and four arms round one axis: no up"],
              "vessel": "no hull is a jelly"},
    "spindles": [stalk] + limbs,
    "membranes": membranes,
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 18.0, "mouth_reach": 42.0, "attack": "whip", "turn_rate": 4.0, "body_radius": 20.0, "aim_cone": 180.0,
                   "suction": {"shape": "all"}, "motion": {"kind": "pulse", "period": 1.8, "kick": 1.8, "glide": 0.55, "glide_turn": 9.0},
                   "feel": "It swims in pulses and works all round: RT eats on every side and whips a vessel anywhere inside its tendrils."},
}
