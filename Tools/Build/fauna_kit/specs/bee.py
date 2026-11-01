"""The Bee as a celestial, Etherion-based lifeform (the /fauna-creation skill's first species).

Garrett, 2026-11-01: "it isn't actually a bee. it is a celestial etherion based life form. it doesn't really have a sense
of up". It keeps the bee's ROLE (a pollinator: it quickens the flora round its hive, stings opposing creatures and carries
their mass home, Docs/ECOSYSTEM.md section 66) and drops the Earth insect's body (no thorax, legs, eyes, antennae or
belly; Bee.fbx is retired).

The body, in world units at root scale 1, the Etherion at the origin, travelling along +z:
- Spine: a braid spindle trailing from the Etherion.
- Three Vanes: braid spindles sweeping back from the Spine at 120 degrees apart (its flight: they beat), each carrying a
  thin vane plate.
- Three Ribs: health prisms set round the Spine, 120 degrees apart.
- Sting: a short terminal braid off the Spine's end, carrying the stinger: one long skinny authored danger prism.
Three-fold round its travel axis, so no side of it is up.

Schema (read by celestial.py; the builder reads the same dict):
  spindles: name, parent (None = grows from the Etherion), mesh ('braid'), from, to   (from = root, to = tip)
  prisms:   name, spindle (its DIRECT parent limb), kind ('health' | 'danger'), role ('stinger' for the stinger),
            at (centre), size (across, across, along), axis (the long axis's direction), seat_authored (bool)
  symmetry: n-fold round +z;  steering_up: 'carried';  mouth: 'none' | 'stinger' | 'front'
  elements: Charge / Mass / Space / Time forms
"""
import math

_VANE_R, _VANE_ROOT_Z, _VANE_TIP_Z = 7.0, -3.5, -6.5
_ANGLES = [math.radians(90 + 120 * k) for k in range(3)]


def _vane(k):
    a = _ANGLES[k]
    return {"name": f"Vane{k}", "parent": "Spine", "mesh": "braid",
            "from": [0.0, 0.0, _VANE_ROOT_Z],
            "to": [round(_VANE_R * math.cos(a), 6), round(_VANE_R * math.sin(a), 6), _VANE_TIP_Z]}


def _vane_plate(k, t=0.65):
    v = _vane(k)
    at = [v["from"][i] + (v["to"][i] - v["from"][i]) * t for i in range(3)]
    axis = [v["to"][i] - v["from"][i] for i in range(3)]
    return {"name": f"VanePlate{k}", "spindle": f"Vane{k}", "kind": "health",
            "at": [round(x, 6) for x in at], "size": [0.5, 2.0, 3.6], "axis": [round(x, 6) for x in axis]}


def _rib(k, r=1.4, z=-8.0):
    a = _ANGLES[k]
    return {"name": f"Rib{k}", "spindle": "Spine", "kind": "health",
            "at": [round(r * math.cos(a), 6), round(r * math.sin(a), 6), z], "size": [0.8, 0.8, 3.2], "axis": [0, 0, 1]}


SPEC = {
    "species": "Bee",
    "premise": "A celestial pollinator of the HyperSea, grown round its Etherion: three vanes beat round a braided spine, "
               "and a long needle trails behind. It is for warmth and busyness: a swarm that tends the flora and drives "
               "off what comes for its hive.",
    "role": "pollinator",
    "symmetry": 3,
    "steering_up": "carried",
    "mouth": "none",   # a pollinator does not eat: it stings with the stinger and carries the mass home
    "spindles": [
        {"name": "Spine", "parent": None, "mesh": "braid", "from": [0.0, 0.0, -1.5], "to": [0.0, 0.0, -11.0]},
        _vane(0), _vane(1), _vane(2),
        {"name": "Sting", "parent": "Spine", "mesh": "braid", "from": [0.0, 0.0, -11.0], "to": [0.0, 0.0, -14.0]},
    ],
    "prisms": [
        _rib(0), _rib(1), _rib(2),
        _vane_plate(0), _vane_plate(1), _vane_plate(2),
        {"name": "Stinger", "spindle": "Sting", "kind": "danger", "role": "stinger",
         "at": [0.0, 0.0, -17.0], "size": [0.5, 0.5, 6.0], "axis": [0, 0, 1]},
    ],
    "elements": {
        # Mass: every plain prism shielded; the body grown until the heart's 12.44 u swing clears every shield.
        "Mass": {"body_scale": 2.5, "shield_all": True},
        # Charge: every body prism danger; the spindles part hardest (SPINDLE_LOD.md).
        "Charge": {"body_scale": 1.0, "all_danger": True},
        # Space: 1.6477 x the Mass span, spindles eroded where thickest, prisms refit as needles. Whether the stinger
        # thins too is Garrett's call: Fauna.ErodeSpindle skips authored danger today.
        "Space": {"body_scale": round(2.5 * 1.6477, 4), "erode_spindle": True, "span_over_mass": 1.6477, "stinger_thins": False},
        # Time: as authored. No body expression for Time is written anywhere yet (an open decision, not a default).
        "Time": {"body_scale": 1.0},
    },
}
