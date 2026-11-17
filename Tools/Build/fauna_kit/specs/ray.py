"""The Ray: a celestial glider, GROWN. The first body under the relaxed laws (Garrett, 2026-11-16: "more lifelike ...
hyper realistic ethereal lifeforms"), and the lifelike answer to the four-winged Manta beside it.

What changed from the Manta, law by law:
- grown wire, not braids (L2): a tapering core, a leading edge that sweeps out and back in one curve, a hind edge, and
  a MEMBRANE woven between them. Nothing on it is straight.
- two-fold (L6): one pair of wings. Its two faces are identical, so it still has no up; it rolls freely.
- wire leads the heart (L1, `lead`): the two lobes curl forward round the Etherion, as the animal's do round its mouth.
The wave is not keyed: every vertex carries its limb's sway (largest at the wingtip, the hind edge a beat behind the
leading edge), so the wing ripples from shoulder to tip and from front to back.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 2
core = K.grown("Core", None, [[0, 0, -1.5], [0, 0, -10.0], [0, 0, -22.0], [0, 0, -30.0]], girth=(1.7, 0.45), design="core")
whip = K.grown("Whip", "Core", [[0, 0, -30.0], [0, 0, -45.0], [0, 0, -60.0]], girth=(0.4, 0.07), design="whip",
               sway={"dir": [0, 1, 0], "amp": 4.0, "waves": 1.3, "pow": 1.6})
limbs, membranes, prisms = [], [], []
for k, a in enumerate(K.angles(N, phase=0.0)):
    P = lambda r, z, a=a: K.polar(r, a, z)
    beat = {"dir": [0, 1, 0], "amp": 7.5, "waves": 0.75, "pow": 1.5}
    lead = K.grown(f"Lead{k}", "Core", [P(1.2, -2.2), P(12.0, -1.0), P(24.0, -5.0), P(32.0, -13.0), P(34.0, -17.0)],
                   girth=(0.8, 0.18), design="spar", sway=beat)
    trail = K.grown(f"Trail{k}", "Core", [P(1.0, -21.0), P(10.0, -22.5), P(22.0, -20.5), P(30.0, -18.0), P(34.0, -17.0)],
                    girth=(0.45, 0.12), design="vein", sway=dict(beat, phase=0.9))
    lobe = K.grown(f"Lobe{k}", "Core", [P(1.4, -1.7), P(3.8, 1.5), P(5.2, 5.0), P(4.2, 8.0)], girth=(0.6, 0.12), design="spar",
                   sway={"dir": "radial", "amp": 1.3, "waves": 0.5})
    limbs += [lead, trail, lobe]
    membranes.append({"name": f"Web{k}", "a": f"Lead{k}", "b": f"Trail{k}", "across": 9})
    for tag, limb, t in (("a", lead, 0.66), ("b", lead, 0.88), ("c", trail, 0.45), ("d", trail, 0.72)):
        at, axis = K.along(limb, t)
        prisms.append({"name": f"Plate{k}{tag}", "spindle": limb["name"], "kind": "health", "at": at, "size": [0.5, 1.8, 3.6], "axis": axis})
prisms.append({"name": "Sting", "spindle": "Whip", "kind": "danger", "role": "stinger", "at": [0, 0, -64.6],
               "size": [0.5, 0.5, 9.0], "axis": [0, 0, 1]})

# ---- THE FOUR ELEMENT FORMS (elements.py): the Ray's own anatomy for each element, on the one body plan above ----
# Each is one feeling. The element's style (strands, wire, tempo) and the game's law (size, armour, erosion) are
# applied on top by elements.form; what is here is what only THIS creature grows.
wings = [(k, a, limbs[3 * k], limbs[3 * k + 1]) for k, a in enumerate(K.angles(N, phase=0.0))]
out_of = lambda a: [round(v, 6) for v in K.polar(1.0, a, 0.0)]      # the way out along a wing


def comet():
    """TIME, the comet: joy. Two streamers fly from the wingtips and whip behind it; nothing else is added, because a
    Time body is the base body going faster."""
    add, jointed = [], []
    for k, a, lead, _trail in wings:
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Stream{k}", lead["name"], [P(34.0, -17.0), P(35.4, -27.0), P(34.0, -38.0), P(35.2, -50.0), P(34.2, -61.0)],
                           girth=(0.2, 0.03), design="whip", sway={"dir": [0, 1, 0], "amp": 5.5, "waves": 2.4, "pow": 1.3, "phase": 1.4}))
        jointed.append({"name": "Stream" + "AB"[k], "wires": [f"Stream{k}"], "cut": "z", "hinges": [-19.0, -32.0, -46.0],
                        "origin": P(34.5, 0.0), "parent": "Vane" + "AB"[k] + "2", "axis": [1, 0, 0], "degrees": [6.0, 9.0, 13.0], "lag": 1.1})
    return {"feeling": "the comet: joy. It streaks past and its streamers write where it has been.",
            "add": add, "unity": {"limbs": jointed}}


def storm():
    """CHARGE, the storm: awe with fear in it. Thorns stand forward along the leading edges, the hind edges are
    serrated, each wingtip runs on into a talon hooked forward, and the lash forks into a trident. Every prism on it
    is danger (the game's law), and it looks it."""
    vanes = {"VaneA": ["Talon0"], "VaneB": ["Talon1"]}
    change, add = {"Core": {"barbs": False}, "Whip": {"barbs": {"count": 10, "length": [1.5, 0.5], "angle": 72.0, "sides": 2, "dir": [1, 0, 0], "flutter": 1.0}}}, []
    for k, a, lead, trail in wings:
        P = lambda r, z, a=a: K.polar(r, a, z)
        change[lead["name"]] = {"barbs": {"count": 12, "length": [3.0, 1.0], "angle": 62.0, "sides": 1, "dir": [0, 0, 1], "flutter": 0.9, "from": 0.14, "to": 0.96}}
        change[trail["name"]] = {"barbs": {"count": 10, "length": [1.7, 0.6], "angle": 68.0, "sides": 1, "dir": [0, 0, -1], "flutter": 0.9, "from": 0.1, "to": 0.92}}
        change[f"Lobe{k}"] = {"barbs": False}
        add.append(K.grown(f"Talon{k}", lead["name"], [P(34.0, -17.0), P(37.2, -14.0), P(39.6, -8.5), P(40.2, -2.0), P(38.6, 3.0)],
                           girth=(0.3, 0.05), design="spar", barbs=False))
        # the side tines leave the CORE's end beside the whip (a stinger rides a limb with no children: law L5)
        add.append(K.grown(f"Tine{k}", "Core", [[0, 0, -30.0], P(2.0, -37.0), P(4.8, -47.0), P(5.6, -58.0)], girth=(0.26, 0.05), design="whip",
                           sway={"dir": out_of(a), "amp": 1.8, "waves": 1.8, "pow": 1.2},
                           barbs={"count": 6, "length": [1.2, 0.4], "angle": 70.0, "sides": 1, "dir": out_of(a), "flutter": 1.0}))
    return {"feeling": "the storm: awe with fear in it. Thorned, taloned, its lash a trident; do not touch.",
            "limbs": change, "add": add, "unity": {"wires": dict(vanes, Lash=["Tine0", "Tine1"])}}


def leviathan():
    """MASS, the leviathan: reverence. Ribs vault each wing from the leading edge to the hind edge like a nave, and
    the tail spreads into a fluke. It is armoured (the game's law), it is slow, and it is not afraid of you."""
    add, vanes = [], {"VaneA": [], "VaneB": []}
    for k, a, lead, trail in wings:
        for i, t in enumerate((0.2, 0.4, 0.6, 0.8)):
            p, q = K.along(lead, t)[0], K.along(trail, t)[0]
            mid = [(p[c] + q[c]) / 2 + out_of(a)[c] * 1.1 for c in range(3)]      # bowed a little toward the tip
            rib = K.grown(f"Rib{k}{i}", lead["name"], [p, mid, q], girth=(0.34, 0.24), design="rib", step=2.0)      # a strut needs few rings
            rib["to_limb"] = trail["name"]
            add.append(rib)
            vanes["Vane" + "AB"[k]].append(rib["name"])
        P = lambda r, z, a=a: K.polar(r, a, z)
        add.append(K.grown(f"Fluke{k}", "Core", [[0, 0, -26.5], P(3.6, -30.5), P(8.2, -36.0), P(11.0, -42.0)], girth=(0.6, 0.14), design="spar",
                           sway={"dir": [0, 1, 0], "amp": 2.4, "waves": 0.6, "pow": 1.5, "phase": 1.6}))
    return {"feeling": "the leviathan: reverence. Vaulted like a nave, fluked like a whale, armoured and unhurried.",
            "add": add, "membranes": [{"name": "FlukeWeb", "a": "Fluke0", "b": "Fluke1", "across": 7}],
            "unity": {"wires": dict(vanes, Lash=["Fluke0", "Fluke1", "FlukeWeb"])}}


def ghost():
    """SPACE, the ghost: longing. A torn sleeve hangs from the outer half of each hind edge, longest at the wingtip,
    and drifts a long way behind; the body is the law's eroded spindle, vast and thin. It is the one you watch leave.
    (Hung along the WHOLE hind edge the veil filled the outline in and read as the Sparrow hull, 0.35 under the 0.41
    bar; as sleeves it is 0.44 from its nearest neighbour. elements.py --silhouette measures it.)"""
    add, webs, vanes = [], [], {"VaneA": [], "VaneB": []}
    for k, a, _lead, trail in wings:
        P = lambda r, z, a=a: K.polar(r, a, z)
        hung = []
        for i, (t, drop) in enumerate(((0.58, 14.0), (0.7, 24.0), (0.82, 34.0), (0.94, 42.0))):
            at = K.along(trail, t)[0]
            r, z = (at[0] ** 2 + at[1] ** 2) ** 0.5, at[2]
            add.append(K.grown(f"Veil{k}{i}", trail["name"], [P(r, z), P(r + 0.5, z - drop * 0.33), P(r - 0.4, z - drop * 0.68), P(r + 0.3, z - drop)],
                               girth=(0.12, 0.02), design="vein",
                               sway={"dir": [0, 1, 0], "amp": 4.5, "waves": 1.5, "pow": 1.2, "phase": 0.9 + 0.55 * i}))
            hung.append(f"Veil{k}{i}")
        for i in range(len(hung) - 1):
            webs.append({"name": f"Shroud{k}{i}", "a": hung[i], "b": hung[i + 1], "across": 7})
        vanes["Vane" + "AB"[k]] = hung + [f"Shroud{k}{i}" for i in range(len(hung) - 1)]
    return {"feeling": "the ghost: longing. Vast and thin, torn sleeves drifting behind it; the one you watch leave.",
            "add": add, "membranes": webs, "unity": {"wires": vanes}}


SPEC = {
    "species": "Ray",
    "premise": "A celestial glider grown round its Etherion: one pair of great woven wings, two lobes curling forward "
               "round the light it carries, a whip trailing far behind. Its wings ripple from shoulder to tip and it "
               "has no up, so it rolls as it pleases. It is for awe: something vast and unhurried passing overhead.",
    "role": "herbivore",
    "symmetry": N,
    "lead": 8.0,
    "sway_hz": 0.38,
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "Mobula (manta rays)", "kept": ["two wings and their diamond", "the ripple from shoulder to tip",
                                                       "lobes curling forward round the mouth", "the whip"],
              "changed": ["two identical faces: no up", "the Etherion is the mouth", "the whip carries a sting"],
              "vessel": "the Manta hull is a thin hard plane; this is woven and it moves"},
    "spindles": [core] + limbs + [whip],
    "membranes": membranes,
    "prisms": prisms,
    "elements": K.elements(prisms),
    # How the body is jointed in Unity (unity_port.py): a limb is cut where it crosses each hinge, each piece hangs
    # from a joint, and GrownLimbDriver turns the joints. Vanes hinge about the travel axis (the tips rise and fall
    # together: the second vane's sweep is negative), each joint a beat behind the one before: the wave.
    "unity": {"klass": "RayFauna", "ram_radius": 20.0, "idle_hz": 0.38, "fast_hz": 0.9, "limbs": [
        {"name": "VaneA", "wires": ["Lead0", "Trail0", "Web0"], "cut": "x", "hinges": [3.0, 13.0, 24.0], "axis": [0, 0, 1], "degrees": [9.0, 11.0, 13.0], "lag": 0.9},
        {"name": "VaneB", "wires": ["Lead1", "Trail1", "Web1"], "cut": "x", "hinges": [-3.0, -13.0, -24.0], "axis": [0, 0, 1], "degrees": [-9.0, -11.0, -13.0], "lag": 0.9},
        {"name": "Lash", "wires": ["Whip"], "cut": "z", "hinges": [-32.0, -42.0, -52.0], "axis": [0, 1, 0], "degrees": [3.0, 4.0, 5.0], "lag": 0.8}]},
    "forms": {"Time": comet(), "Charge": storm(), "Mass": leviathan(), "Space": ghost()},
    "possession": {"top_speed": 34.0, "mouth_reach": 30.0, "attack": "whip", "turn_rate": 3.0, "speed_ease": 0.9,
                   "suction": {"shape": "ram", "radius": 20.0},
                   "feel": "It glides: slow to gain speed, slow to lose it. Hold RT to feed on the wing. Roll it (one stick up, one down) and watch."},
}
