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

# ---- THE FOUR ELEMENT FORMS (elements.py): the Ammon's own anatomy for each element, on the one coiled body ----
shells = [(k, a, limbs[k]) for k, a in enumerate(K.angles(2, phase=0.0))]


def nautilus():
    """TIME, the jet: joy. A streamer flies from the siphon on each side and whips behind it, the way a jet's wake
    trails a swimmer. Nothing else is added: a Time body is the base body going faster."""
    add, webs = [], []
    for k, a, _shell in shells:
        P = lambda r, z, a=a: K.polar(r, a, z)
        at = K.along(_shell, 0.12)[0]
        r, z = (at[0] ** 2 + at[1] ** 2) ** 0.5, at[2]
        add.append(K.grown(f"Wake{k}", _shell["name"], [P(r, z), P(r + 6.0, z - 9.0), P(r + 13.0, z - 19.0), P(r + 21.0, z - 31.0), P(r + 25.0, z - 44.0)],
                           girth=(0.2, 0.03), design="whip", sway={"dir": "tangent", "amp": 5.5, "waves": 2.2, "pow": 1.3, "phase": 1.4 + 3.1416 * k}))
        at = K.along(_shell, 0.3)[0]
        r, z = (at[0] ** 2 + at[1] ** 2) ** 0.5, at[2]
        add.append(K.grown(f"Wake{k}b", _shell["name"], [P(r, z), P(r + 3.0, z - 9.0), P(r + 7.5, z - 19.0), P(r + 10.0, z - 31.0), P(r + 13.0, z - 44.0)],
                           girth=(0.2, 0.03), design="whip", sway={"dir": "tangent", "amp": 5.5, "waves": 2.2, "pow": 1.3, "phase": 2.0 + 3.1416 * k}))
        webs.append({"name": f"Ribbon{k}", "a": f"Wake{k}", "b": f"Wake{k}b", "across": 5})
    return {"feeling": "the jet: joy. It kicks and its wake writes where it has been.", "add": add, "membranes": webs}


def corona():
    """CHARGE, the corona: awe with fear. Thorns stand forward from each shell's outer whorl and a horn is hooked
    forward from the siphon on each side. The feelers keep their lines; the sparks are the element's own."""
    change, add = {}, []
    for k, a, shell in shells:
        P = lambda r, z, a=a: K.polar(r, a, z)
        change[shell["name"]] = {"barbs": {"count": 20, "length": [4.5, 1.6], "angle": 58.0, "sides": 1, "dir": [0, 0, 1], "flutter": 0.8, "from": 0.05, "to": 0.7}}
        add.append(K.grown(f"Horn{k}", "Siphon", [P(0.6, -2.4), P(6.0, -4.5), P(12.0, -3.5), P(15.5, -0.3)],
                           girth=(0.34, 0.05), design="spar", barbs=False))
    return {"feeling": "the corona: awe with fear in it. Thorned whorls, horns hooked forward; do not touch.", "change": change, "add": add}


def elder():
    """MASS, the elder: reverence. A hood of chambers spreads from the siphon like the nautilus's own hood, and a
    strut braces each shell to the siphon. It is old, armoured and not afraid of you."""
    add = []
    for k, a, shell in shells:
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Hood{k}", "Siphon", [P(0.8, -5.2), P(5.0, -9.0), P(10.0, -15.0), P(13.0, -23.0)],
                           girth=(0.6, 0.16), design="spar", sway={"dir": "tangent", "amp": 1.0, "waves": 0.5, "phase": 3.1416 * k}))
    return {"feeling": "the elder: reverence. Hooded like a nautilus, braced like a vault, older than the sea it swims.",
            "add": add, "membranes": [{"name": "HoodWeb", "a": "Hood0", "b": "Hood1", "across": 6}]}


def drift():
    """SPACE, the drift: longing. Long strands hang from each shell's outer whorl and trail a long way behind, joined
    by a thin sleeve. It is the shell you watch leave. (Hung only from the shells, never filling between them.)"""
    add, webs = [], []
    for k, a, shell in shells:
        P = lambda r, z, a=a: K.polar(r, a, z)
        hung = []
        for i, (t, drop, out) in enumerate(((0.1, 36.0, 4.0), (0.5, 28.0, 24.0))):
            at = K.along(shell, t)[0]
            r, z = (at[0] ** 2 + at[1] ** 2) ** 0.5, at[2]
            add.append(K.grown(f"Strand{k}{i}", shell["name"], [P(r, z), P(r + out * 0.3, z - drop * 0.33), P(r + out * 0.7, z - drop * 0.68), P(r + out, z - drop)],
                               girth=(0.12, 0.02), design="vein",
                               sway={"dir": "tangent", "amp": 4.5, "waves": 1.5, "pow": 1.2, "phase": 0.9 + 0.55 * i}))
            hung.append(f"Strand{k}{i}")
        for i in range(len(hung) - 1):
            webs.append({"name": f"Sleeve{k}{i}", "a": hung[i], "b": hung[i + 1], "across": 3})
    return {"feeling": "the drift: longing. Vast and thin, strands trailing from its shells; the one you watch leave.",
            "add": add, "membranes": webs}


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
    "forms": {"Time": nautilus(), "Charge": corona(), "Mass": elder(), "Space": drift()},
    "possession": {"top_speed": 24.0, "mouth_reach": 30.0, "attack": "whip", "turn_rate": 5.0,
                   "motion": {"kind": "pulse", "period": 1.2, "kick": 2.0, "glide": 0.5, "glide_turn": 10.0},
                   "feel": "It swims in jets: a kick, then a glide it turns in. Watch the light wind into its shells."},
}
