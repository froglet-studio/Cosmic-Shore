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

# ---- THE FOUR ELEMENT FORMS (elements.py): the Nettle's own anatomy for each element, on the one bell above ----
# Light on purpose: the base body is already near the mesh bar. Each form adds one or two things; where the form's
# style would overrun the bar, a form also thins a limb's wire ("change"), which never touches a limb's path or prisms.
frills = [(k, a, limbs[16 + k]) for k, a in enumerate(K.angles(4, phase=22.5))]
ribs = [(k, a, limbs[2 * k]) for k, a in enumerate(A8)]
outward = lambda a: [round(v, 6) for v in K.polar(1.0, a, 0.0)]


def sprite():
    """TIME, the sprite: joy. Four bright ribbons fly off the bell's rim, flung wide and trailing back past the tendrils,
    so the whole jelly seems to be streaming. Nothing on the bell changes: Time is the base body."""
    add = []
    for k, a, rib in ribs:
        if k % 2:
            continue
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Pennant{k}", rib["name"], [P(18.0, -9.5), P(26.0, -16.0), P(32.0, -26.0), P(31.0, -37.0), P(34.0, -47.0)],
                           girth=(0.3, 0.04), design="whip", section=2, barbs=False, step=2.0,
                           sway={"dir": "tangent", "amp": 5.0, "waves": 2.2, "pow": 1.3, "phase": 0.6 * k}))
    return {"feeling": "the sprite: joy. Ribbons fly off its rim and stream behind; it is all speed and ribbon.",
            "add": add}


def thornbell():
    """CHARGE, the thornbell: beauty with a warning in it. A crown of eight spears leans forward from the rim of the
    bell, each one hooked a little, and the frilled arms are bare so the points are all you see. Every prism on it is
    danger (the game's law)."""
    add, change = [], {}
    for k, a, rib in ribs:
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Spear{k}", rib["name"], [P(17.6, -7.5), P(19.6, -3.6), P(20.8, 0.4), P(20.2, 2.9)],
                           girth=(0.26, 0.04), design="spar", section=2, barbs=False))
    for k, a, fr in frills:
        change[fr["name"]] = {"barbs": False}
    for k, a, rib in ribs:
        change[f"Tendril{k}"] = {"barbs": {"count": 7, "length": [1.6, 0.5], "angle": 70.0, "sides": 1, "dir": [0, 0, 1], "flutter": 0.8}}
    return {"feeling": "the thornbell: beauty with a warning in it. A crown of spears leans out of the bell; do not touch.",
            "add": add, "change": change}


def abbey():
    """MASS, the abbey: reverence. A girdle of struts ties the ribs together round the rim like the hoop of a bell
    or a cloister, and a curtain hangs between the oral arms. It is armoured (the game's law) and it does not hurry."""
    add = []
    for k, a, rib in ribs:
        nxt = ribs[(k + 1) % 8][2]
        p, q = K.along(rib, 0.9)[0], K.along(nxt, 0.9)[0]
        mid = [(p[c] + q[c]) / 2 * 1.04 for c in range(3)]
        hoop = K.grown(f"Hoop{k}", rib["name"], [p, mid, q], girth=(0.3, 0.26), design="rib", step=2.0)
        hoop["to_limb"] = nxt["name"]
        add.append(hoop)
    webs = [{"name": f"Curtain{k}", "a": f"Frill{k}", "b": f"Frill{(k + 1) % 4}", "across": 5} for k in range(4)]
    thin = {f"Tendril{k}": {"barbs": {"count": 5, "length": [1.6, 0.5], "angle": 95.0, "sides": 1, "flutter": 0.5}, "step": 2.0} for k in range(8)}
    for k in range(8):
        thin[f"Rib{k}"] = {"step": 1.6}
    return {"feeling": "the abbey: reverence. A girdle hoops the bell and a curtain hangs between its arms; heavy and unhurried.",
            "add": add, "membranes": webs, "change": thin}


def veil():
    """SPACE, the veil: longing. Four very long strands hang from the tips of the oral arms, further behind than
    anything else on the body, with a thin web between neighbours; the bell is the law's eroded wire, vast and thin.
    It is the one you watch drift away."""
    add, hung = [], []
    for k, a, fr in frills:
        P = lambda r, z, a=a: K.polar(r, a, z)
        at = K.along(fr, 1.0)[0]
        r, z = (at[0] ** 2 + at[1] ** 2) ** 0.5, at[2]
        add.append(K.grown(f"Drift{k}", fr["name"], [P(r, z), P(r + 1.0, z - 10.0), P(r + 0.2, z - 22.0), P(r + 1.6, z - 34.0), P(r + 0.6, z - 46.0)],
                           girth=(0.14, 0.02), design="vein", section=2,
                           sway={"dir": "tangent", "amp": 6.0, "waves": 1.6, "pow": 1.2, "phase": 0.9 * k}))
        hung.append(f"Drift{k}")
    webs = [{"name": f"Gossamer{k}", "a": hung[k], "b": hung[(k + 1) % 4], "across": 5} for k in range(4)]
    return {"feeling": "the veil: longing. Long strands trail from its arms into the dark; the one you watch drift away.",
            "add": add, "membranes": webs}


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
    "forms": {"Time": sprite(), "Charge": thornbell(), "Mass": abbey(), "Space": veil()},
    "possession": {"top_speed": 18.0, "mouth_reach": 42.0, "attack": "whip", "turn_rate": 4.0, "body_radius": 20.0, "aim_cone": 180.0,
                   "suction": {"shape": "all"}, "motion": {"kind": "pulse", "period": 1.8, "kick": 1.8, "glide": 0.55, "glide_turn": 9.0},
                   "feel": "It swims in pulses and works all round: RT eats on every side and whips a vessel anywhere inside its tendrils."},
}
