"""The Lantern: a celestial comb jelly, GROWN. A clear woven body the Etherion lights from inside, eight comb rows
with light running down them, two long fishing lines.

THE REAL ANIMAL (a comb jelly, Ctenophora): a glassy body with eight rows of beating plates that scatter light into
moving rainbows, and two long branching tentacles it fishes with. It does not sting; it glides on its combs.
THE HYPERSEA'S: the body is woven over the Etherion (the relaxed L1), each row is a `comb` spindle whose light pulses
run from front to back, each row a beat behind its neighbour, and the two lines trail far behind with a sting at
the end of each. Two-fold, as the animal is.

For a rider: the smoothest swimmer here, quick to turn. With two lines and no arms the game will lunge it, not whip it.
"""
import math
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

# ---- THE FOUR ELEMENT FORMS (elements.py): the Lantern's own anatomy for each element, on the one comb-jelly body ----
# The base body is already near the mesh bar, so each form adds little and, where it can, thins the combs' barbs.
combs = limbs[:8]
out_of = lambda a: [round(v, 6) for v in K.polar(1.0, a, 0.0)]


def tail_end(i):
    """Where comb i ends at the back: its radius, angle and z (a streamer or tine hangs from there)."""
    at = K.along(combs[i], 0.97)[0]
    return at, (at[0] ** 2 + at[1] ** 2) ** 0.5, math.atan2(at[1], at[0])


def drift():
    """TIME, the drift: joy. Two bright ribbons stream from the tails of two opposite combs and whip behind it; the
    body is the base."""
    add = []
    for k, i in enumerate((2, 6)):
        at, r, ang = tail_end(i)
        P = lambda rr, z, ang=ang: K.polar(rr, ang, z)
        add.append(K.grown(f"Stream{k}", combs[i]["name"], [at, P(r + 5.0, at[2] - 5.0), P(r + 12.0, at[2] - 10.0), P(r + 20.0, at[2] - 17.0), P(r + 26.0, at[2] - 27.0)],
                           girth=(0.14, 0.03), design="whip", barbs=False,
                           sway={"dir": "tangent", "amp": 4.5, "waves": 2.4, "pow": 1.3, "phase": 1.4 * k}))
    return {"feeling": "the drift: joy. It glides past and its streamers write where it has been.", "add": add}


def corona():
    """CHARGE, the corona: awe with fear in it. The combs' plates become forward-leaning thorns, and two barbed tines
    spread from the tails of two opposite combs. Every prism on it is danger (the game's law)."""
    add, change = [], {}
    for c in combs:
        change[c["name"]] = {"barbs": {"count": 8, "length": [2.6, 1.4], "angle": 58.0, "sides": 1, "dir": [0, 0, 1],
                                       "flutter": 0.8, "from": 0.12, "to": 0.9}}
    for k, i in enumerate((2, 6)):
        at, r, ang = tail_end(i)
        P = lambda rr, z, ang=ang: K.polar(rr, ang, z)
        add.append(K.grown(f"Tine{k}", combs[i]["name"], [at, P(r + 5.0, at[2] - 4.0), P(r + 11.0, at[2] - 8.0), P(r + 16.0, at[2] - 15.0)],
                           girth=(0.24, 0.04), design="whip", sway={"dir": out_of(ang), "amp": 1.8, "waves": 1.8, "pow": 1.2},
                           barbs={"count": 6, "length": [1.4, 0.5], "angle": 70.0, "sides": 1, "dir": out_of(ang), "flutter": 1.0}))
    return {"feeling": "the corona: awe with fear in it. Thorned combs, barbed tines behind; do not touch.",
            "change": change, "add": add}


def nave():
    """MASS, the nave: reverence. Hoops brace neighbouring combs where the body is widest, so it vaults like a
    cathedral, and the plates are fewer and still: heavy, calm and old."""
    add, change = [], {}
    for j in range(4):
        for i in (j, j + 4):
            n = (i + 1) % 8
            p, q = K.along(combs[i], 0.5)[0], K.along(combs[n], 0.5)[0]
            mid = [round((p[c] + q[c]) / 2 * 1.06, 6) if c < 2 else round((p[c] + q[c]) / 2, 6) for c in range(3)]
            hoop = K.grown(f"Hoop{i}", combs[i]["name"], [p, mid, q], girth=(0.3, 0.22), design="rib", step=2.0)
            hoop["to_limb"] = combs[n]["name"]
            add.append(hoop)
    for c in combs:
        change[c["name"]] = {"barbs": {"count": 7, "length": [1.6, 1.3], "angle": 88.0, "sides": 2, "dir": "tangent",
                                       "flutter": 0.1, "from": 0.12, "to": 0.9}}
    return {"feeling": "the nave: reverence. Hooped like a cathedral, still-plated and unhurried; it is not afraid of you.",
            "change": change, "add": add}


def veil():
    """SPACE, the veil: longing. Long threads hang from the combs' tail ends and drift a long way behind with a gauze
    between neighbours: vast, thin, the one you watch leave."""
    add, webs = [], []
    for g, (i0, i1) in enumerate(((0, 7), (3, 4))):
        hung = []
        for i in (i0, i1):
            at = K.along(combs[i], 0.9)[0]
            r = (at[0] ** 2 + at[1] ** 2) ** 0.5
            ang = math.atan2(at[1], at[0])
            P = lambda rr, z, ang=ang: K.polar(rr, ang, z)
            add.append(K.grown(f"Veil{i}", combs[i]["name"], [at, P(r + 7.0, at[2] - 6.0), P(r + 16.0, at[2] - 14.0), P(r + 22.0, at[2] - 26.0)],
                               girth=(0.12, 0.02), design="vein", barbs=False,
                               sway={"dir": "tangent", "amp": 4.5, "waves": 1.5, "pow": 1.2, "phase": 0.9 + 0.55 * i}))
            hung.append(f"Veil{i}")
        webs.append({"name": f"Gauze{g}", "a": hung[0], "b": hung[1], "across": 7})
    return {"feeling": "the veil: longing. Vast and thin, gauze drifting behind it; the one you watch leave.",
            "add": add, "membranes": webs}


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
    "forms": {"Time": drift(), "Charge": corona(), "Mass": nave(), "Space": veil()},
    "possession": {"top_speed": 28.0, "mouth_reach": 32.0, "attack": "lunge", "turn_rate": 7.0,
                   "feel": "The smoothest swimmer here and quick to turn. Watch the light run down its combs."},
}
