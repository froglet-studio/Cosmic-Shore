"""Quivera Pearlspark, OPAL - Human / Chameleon / Jellyfish, House of Seven Scales, a merchant: Gold. Round 2
(2026-11-07): two colour-changers for the Omni sight (the character-first cast's idea, with the jellyfish's one
trailing feature instead of the cuttlefish's whole-body bands the art judge rejected). The chameleon's green coat
and the jellyfish's blue-lilac glow rule out both the green and the blue key, so this spec paints over MAGENTA;
Gold trim is safe under it (matte.py's self-test). Name from PilotGenerator's tables (Qui + ve + ra; Pearl + spark).
"""
PORTRAIT = dict(
    name="Quivera",
    pilot="pilot.callsign.opal",
    person="Quivera Pearlspark",
    callsign="OPAL",
    heritage=("Chameleon", "Jellyfish"),
    faction="Merchant", faction_name="House of Seven Scales",
    pose="standing in a relaxed three-quarter pose, weight on one leg, turned slightly to the left, a sly knowing "
         "look at the viewer with one turret eye swivelled toward the opal pendant, one hand lifting the pendant on "
         "its chain",
    extra="The jellyfish tentacles trail from the back of the head like glowing hair in pale blue and lilac, and the "
          "travelling coat is a warm cream linen",
    source="Docs/Pilots/art/sources/quivera.png",
    key="#FF00FF",
    crop=None,
    exempt=(),
    before=None,
)
