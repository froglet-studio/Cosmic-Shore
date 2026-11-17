"""The Plume: a celestial feather star, GROWN. Ten feathered arms that curl back round a small cup, beating in turn.

THE REAL ANIMAL (a feather star, Comatulida): a small cup with ten or more feathered arms. It swims by sweeping
alternate arms down while their neighbours rise, so it climbs through the water like a slow firework.
THE HYPERSEA'S: the cup trails the Etherion; every arm is a `rachis` spindle (a shaft and two rows of barbs); and
neighbouring arms sway half a beat apart, so five rise as five fall. Light runs out along each arm, one after another.

For a rider: it is all reach. RT eats on every side through the plumes, and its attack whips all ten arms.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 5
calyx = K.grown("Calyx", None, [[0, 0, -1.6], [0, 0, -3.5], [0, 0, -5.5]], girth=(1.1, 1.5), design="rib")
limbs, prisms = [], []
for k, a in enumerate(K.angles(10, phase=0.0)):
    P = lambda r, z, a=a: K.polar(r, a, z)
    arm = K.grown(f"Arm{k}", "Calyx", [P(1.0, -4.6), P(9.0, -3.2), P(18.0, -8.0), P(23.0, -18.0), P(21.0, -30.0), P(15.0, -38.0)],
                  girth=(0.45, 0.1), design="rachis", strands=2, section=2, step=1.4,
                  barbs={"count": 16, "length": [5.5, 1.2], "angle": 58.0, "sides": 2, "dir": "tangent", "flutter": 0.35},
                  sway={"dir": "radial", "amp": 5.0, "waves": 0.6, "phase": 3.1416 * (k % 2)},
                  glow={"bright": [1.0, 0.6], "waves": 1.5, "phase": 0.63 * k})
    limbs.append(arm)
    at, axis = K.along(arm, 0.42)
    prisms.append({"name": f"Plate{k}", "spindle": f"Arm{k}", "kind": "health", "at": at, "size": [0.5, 1.6, 3.0], "axis": axis})
    prisms.append({"name": f"Sting{k}", "spindle": f"Arm{k}", "kind": "danger", "role": "stinger",
                   "at": P(15.0 - 0.6 * 2.9, round(-38.0 - 0.8 * 2.9, 4)), "size": [0.5, 0.5, 5.0], "axis": P(-6.0, -8.0)})

# ---- THE FOUR ELEMENT FORMS (elements.py): the Plume's own anatomy for each element ----
# Every arm carries a sting, so whatever a form adds hangs from the Calyx (the root limb). Every form keeps all ten
# feathered arms; the additions are five copies (K.angles(N)) set between the arms.
arms = [K.find(limbs, f"Arm{k}") for k in range(10)]
GAP = 3.1416 / 10                                   # five copies sit half a step off the arms, in the gaps
between = lambda: [a + GAP for a in K.angles(N, phase=0.0)]


def comet():
    """TIME, the comet: joy. Five long streamers fly out between the plumes and whip behind it."""
    add = []
    for k, a in enumerate(between()):
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Stream{k}", "Calyx", [P(0.6, -3.5), P(14.0, -5.0), P(32.0, -9.0), P(46.0, -17.0), P(50.0, -27.0)],
                           girth=(0.2, 0.04), design="whip", sway={"dir": "tangent", "amp": 5.5, "waves": 2.2, "pow": 1.3, "phase": 0.8 * k}))
    return {"feeling": "the comet: joy. Streamers fly between the plumes and write where it has been.", "add": add}


def storm():
    """CHARGE, the storm: awe with fear in it. Every feather turns its barbs forward like thorns, and five hooked
    talons reach round the cup, curling forward past the arms."""
    add, change = [], {}
    for k in range(10):
        change[f"Arm{k}"] = {"barbs": {"count": 16, "length": [6.5, 1.5], "angle": 40.0, "sides": 2, "dir": [0, 0, 1], "flutter": 0.9}}
    for k, a in enumerate(between()):
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Talon{k}", "Calyx", [P(0.6, -3.5), P(12.0, -4.0), P(26.0, -9.0), P(32.0, -6.0), P(33.0, -0.8)],
                           girth=(0.4, 0.05), design="spar", barbs=False))
    return {"feeling": "the storm: awe with fear in it. Thorned forward, taloned; do not touch.", "add": add, "change": change}


def leviathan():
    """MASS, the leviathan: reverence. Braces vault from the cup to the arms like a nave, and the arms stand heavy and still."""
    add, change = [], {}
    for k, a in enumerate(between()):
        P = lambda r, z, a=a: K.polar(r, a, z)
        target = arms[2 * k]
        at = K.along(target, 0.45)[0]
        r, z = K.rz(at)
        brace = K.grown(f"Brace{k}", "Calyx", [P(0.8, -4.0), K.polar(r * 0.5, K.angles(N, phase=0.0)[k] + GAP * 0.5, z + 1.0), at],
                        girth=(0.4, 0.3), design="rib", step=2.0)
        brace["to_limb"] = target["name"]
        add.append(brace)
    for k in range(10):
        change[f"Arm{k}"] = {"barbs": {"count": 12, "length": [4.5, 1.2], "angle": 58.0, "sides": 2, "dir": "tangent", "flutter": 0.1}}
    return {"feeling": "the leviathan: reverence. Braced like a nave and unhurried.", "add": add, "change": change}


def ghost():
    """SPACE, the ghost: longing. Five thin lines drift far out and behind it, with gaps between."""
    add = []
    for k, a in enumerate(between()):
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Drift{k}", "Calyx", [P(0.6, -4.0), P(16.0, -6.0), P(36.0, -12.0), P(52.0, -22.0), P(58.0, -36.0)],
                           girth=(0.12, 0.02), design="vein", sway={"dir": "radial", "amp": 5.0, "waves": 1.3, "pow": 1.2, "phase": 0.9 * k}))
    return {"feeling": "the ghost: longing. Thin lines drifting far out; the one you watch leave.", "add": add}


SPEC = {
    "species": "Plume",
    "premise": "A celestial feather star grown round its Etherion: ten feathered arms curling back from a small cup, "
               "five rising as five fall, light running out along each in turn. It is for wonder: a slow firework "
               "that never finishes going off.",
    "role": "herbivore",
    "symmetry": N,
    "sway_hz": 0.5,
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "Comatulida (feather stars)", "kept": ["ten feathered arms round a cup", "alternate arms beating", "feeding through the plumes"],
              "changed": ["the cup trails the Etherion", "a sting at every arm's tip"], "vessel": "no hull is a feather star"},
    "spindles": [calyx] + limbs,
    "prisms": prisms,
    "elements": K.elements(prisms),
    "forms": {"Time": comet(), "Charge": storm(), "Mass": leviathan(), "Space": ghost()},
    "possession": {"top_speed": 22.0, "mouth_reach": 46.0, "attack": "whip", "turn_rate": 5.0, "body_radius": 22.0, "aim_cone": 180.0,
                   "suction": {"shape": "all"},
                   "feel": "It is all reach: RT eats on every side through its plumes, and its attack whips all ten arms."},
}
