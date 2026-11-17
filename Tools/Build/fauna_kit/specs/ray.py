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
    "possession": {"top_speed": 34.0, "mouth_reach": 30.0, "attack": "whip", "turn_rate": 3.0, "speed_ease": 0.9,
                   "suction": {"shape": "ram", "radius": 20.0},
                   "feel": "It glides: slow to gain speed, slow to lose it. Hold RT to feed on the wing. Roll it (one stick up, one down) and watch."},
}
