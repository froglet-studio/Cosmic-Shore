"""The Ammon: a celestial twin-coiled shell, GROWN. Two spiral shells wind away on either side of the Etherion, six
feelers trail between them.

THE REAL ANIMAL (an ammonite, a nautilus): one coiled, chambered shell that widens as it grows, a crown of tentacles
at its mouth, and a jet: it swims in pulses, backwards.
THE HYPERSEA'S: one coil would give it a side, so it has TWO, mirrored across its travel axis like a ram's horns
(two-fold). Each is a `shell` spindle: nine ribs hooped as they wind inward and narrow, light spiralling down into
the centre. The Etherion sits where the two mouths meet, and the feelers trail from behind it with a sting on each.

For a rider: it swims in jets (a kick, then a glide it turns in), and its attack whips the feelers.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 2
siphon = K.grown("Siphon", None, [[0, 0, -1.6], [0, 0, -5.0], [0, 0, -8.0]], girth=(1.0, 0.8), design="rib")
limbs, prisms = [], []
for k, a in enumerate(K.angles(2, phase=0.0)):
    shell = K.grown(f"Shell{k}", "Siphon", K.spiral(a, (11.0, -6.0), 10.0, 2.4, 0.17), girth=(3.2, 0.5), design="shell",
                    sway={"dir": "tangent", "amp": 0.6, "waves": 0.3}, glow={"bright": [1.0, 0.55], "waves": 5.0, "phase": 3.1416 * k})
    limbs.append(shell)
    for tag, t in (("a", 0.2), ("b", 0.42)):
        at, axis = K.along(shell, t)
        prisms.append({"name": f"Plate{k}{tag}", "spindle": f"Shell{k}", "kind": "health", "at": at, "size": [0.6, 2.0, 3.4], "axis": axis})
for k, deg in enumerate((62.0, 90.0, 118.0, 242.0, 270.0, 298.0)):
    a = K.math.radians(deg)
    P = lambda r, z, a=a: K.polar(r, a, z)
    limbs.append(K.grown(f"Feeler{k}", "Siphon", [P(0.6, -7.6), P(5.0, -14.0), P(7.0, -24.0), P(5.0, -36.0), P(8.0, -48.0)],
                         girth=(0.3, 0.05), design="tendril", sway={"dir": "tangent", "amp": 4.0, "waves": 1.8, "phase": 0.9 * k}))
    prisms.append({"name": f"Sting{k}", "spindle": f"Feeler{k}", "kind": "danger", "role": "stinger",
                   "at": P(8.0 + 3.0 / 12.369 * 3.3, round(-48.0 - 12.0 / 12.369 * 3.3, 4)), "size": [0.5, 0.5, 6.0], "axis": P(3.0, -12.0)})

SPEC = {
    "species": "Ammon",
    "premise": "A celestial twin-coiled shell grown round its Etherion: two spirals winding away on either side like a "
               "ram's horns, light running down into their centres, six feelers trailing between. It is for age: "
               "something that has been here far longer than you.",
    "role": "omnivore",
    "symmetry": N,
    "sway_hz": 0.45,
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "Ammonoidea and Nautilus (coiled cephalopods)", "kept": ["a chambered coil that widens as it grows", "feelers at the mouth", "swimming in jets"],
              "changed": ["two coils, mirrored: no side", "the Etherion sits where the mouths meet", "the feelers trail and sting"],
              "vessel": "no hull is a coiled shell"},
    "spindles": [siphon] + limbs,
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 24.0, "mouth_reach": 30.0, "attack": "whip", "turn_rate": 5.0,
                   "motion": {"kind": "pulse", "period": 1.2, "kick": 2.0, "glide": 0.5, "glide_turn": 10.0},
                   "feel": "It swims in jets: a kick, then a glide it turns in. Watch the light wind into its shells."},
}
