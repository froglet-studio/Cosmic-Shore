"""The Manta: a celestial glider. Four great rib-fan wings round one axis, so it is a manta from every side.

THE REAL ANIMAL (Mobula): a diamond of two wings that beat in a slow wave running from the shoulder to the tip, two
cephalic lobes that frame a wide mouth and funnel food into it, a whip of a tail with no sting, and a habit of
rolling and looping through its food. It feeds by flying through it with its mouth open.
THE VESSEL (the Manta hull): a thin flat plane, wide and low: a bar from the side, a line from ahead.
THE HYPERSEA'S MANTA is neither. What a person would call a manta in it is the DIAMOND, the WAVE and the WHIP:
- A cell has no up, so one flat pair of wings would be a lie. It has FOUR, a quarter turn apart: from the side,
  from any quarter, the two wings in view draw the manta's diamond and the other two are a line through it. From
  ahead it is a cross, with a short lobe between each pair of wings.
- A wing is a fan: a swept leading spar of three braids, four ribs trailing from it, a hem joining their tips into the
  hind edge, and a thin plate on three of the ribs. The wire is the membrane.
- The lobes do not reach ahead of the mouth. The Etherion IS the mouth, and the lobes curl round it from behind.
- The whip carries the sting every celestial body carries.
- The wave: each wing swings about the travel axis at its shoulder, each joint further out a beat behind, and
  opposite wings mirror each other, so one pair rises as the other pair crosses. It never holds an up.

For a rider: it glides (slow to gain speed, slow to lose it) and feeds on the wing: holding RT eats whatever passes
between its lobes, with no wait between mouthfuls, so its meal is the line it flies.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _kit as K  # noqa: E402

N = 4
HZ = 0.42                              # the wingbeat: slow enough to watch
SPAR = [(0.0, -3.0), (13.0, -6.0), (24.0, -10.5), (32.0, -17.0)]            # (radius, z): the leading edge, swept back
RIBS = [(6.5, -4.5, 4.5, -24.0, 0), (13.0, -6.0, 11.0, -23.0, 1), (18.5, -8.2, 17.5, -21.0, 1), (24.0, -10.5, 24.5, -18.5, 2)]
# a rib: from (radius, z) on the spar back to (radius, z) on the hind edge, riding spar segment n

spine = K.spindle("Spine", None, [0, 0, -1.5], [0, 0, -20.0])
limbs, prisms = [], []
for k, a in enumerate(K.angles(N, phase=0.0)):
    # opposite wings mirror (both tips rise together), and the second pair runs a quarter beat behind the first
    sense = 1.0 if k in (0, 3) else -1.0
    lag = 0.0 if k % 2 == 0 else 1.5708
    spar = K.chain(f"Spar{k}", "Spine", [K.polar(r, a, z) for r, z in SPAR])
    for i, s in enumerate(spar):
        s["beat"] = {"axis": "travel", "deg": [15.0, 13.0, 13.0][i] * sense, "hz": HZ, "phase": lag + 0.9 * i}
    limbs += spar
    for j, (r0, z0, r1, z1, seg) in enumerate(RIBS):
        rib = K.spindle(f"Rib{k}_{j}", f"Spar{k}_{seg}", K.polar(r0, a, z0), K.polar(r1, a, z1), girth=0.8)
        rib["beat"] = {"axis": "travel", "deg": 6.0 * sense, "hz": HZ, "phase": lag + 0.9 * seg + 0.8}
        limbs.append(rib)
        if j < 3:
            prisms.append(K.on(rib, f"Vane{k}_{j}", "health", 0.72, [0.5, 2.2, 4.6]))
    # the hind edge: a hem from each rib's tip to the next, and on to the wingtip, so the fan closes into a wing
    tips = [(r1, z1) for _r0, _z0, r1, z1, _seg in RIBS] + [SPAR[-1]]
    for j in range(len(RIBS)):
        limbs.append(K.spindle(f"Hem{k}_{j}", f"Rib{k}_{j}", K.polar(tips[j][0], a, tips[j][1]), K.polar(tips[j + 1][0], a, tips[j + 1][1]), girth=0.6))
    # a cephalic lobe between this wing and the next: out from beside the Etherion, then curling back in
    b = a + 0.7854
    lobe = K.chain(f"Lobe{k}", None, [K.polar(2.2, b, -1.0), K.polar(6.2, b, -2.6), K.polar(5.0, b, -7.0)], girth=0.8)
    lobe[0]["beat"] = {"axis": "tangent", "deg": 9.0, "hz": HZ, "phase": lag}
    limbs += lobe
    prisms.append(K.on(lobe[1], f"LobePlate{k}", "health", 0.55, [0.6, 1.4, 3.0]))
tail = K.chain("Whip", "Spine", [[0, 0, -20.0], [0, 0, -30.0], [0, 0, -40.0], [0, 0, -48.0]], girth=0.6)
for i, s in enumerate(tail):
    s["beat"] = {"axis": [1.0, 0.0, 0.0], "deg": 5.0, "hz": 0.3, "phase": 0.8 * i}
prisms.append({"name": "Sting", "spindle": "Whip_2", "kind": "danger", "role": "stinger",
               "at": [0, 0, -53.0], "size": [0.5, 0.5, 9.0], "axis": [0, 0, 1]})

SPEC = {
    "species": "Manta",
    "premise": "A celestial glider, grown round its Etherion: four great rib-fan wings a quarter turn apart, a lobe "
               "curled between each pair, a long whip behind. From any side it draws a manta's diamond, and its wings "
               "beat in a slow wave that never settles on an up. It is for awe: something vast passing overhead.",
    "role": "herbivore",
    "symmetry": N,
    "steering_up": "carried",
    "mouth": "front",
    "clade": {"real": "Mobula (manta rays)", "kept": ["the diamond of two wings in view", "the wave from shoulder to tip",
                                                       "lobes framing the mouth", "the whip", "feeding on the wing"],
              "changed": ["four wings, not two: no up", "the Etherion is the mouth", "the whip carries a sting"],
              "vessel": "the Manta hull is a thin flat plane; this is a cross of fans, and silhouette.py holds them apart"},
    "spindles": [spine] + limbs + tail,
    "prisms": prisms,
    "elements": K.elements(prisms),
    "possession": {"top_speed": 32.0, "mouth_reach": 30.0, "attack": "whip", "turn_rate": 3.0, "speed_ease": 0.9,
                   "suction": {"shape": "ram", "radius": 20.0},
                   "feel": "It glides: slow to gain speed, slow to lose it. Hold RT to feed on the wing: no wait between mouthfuls, it eats the line you fly."},
}
