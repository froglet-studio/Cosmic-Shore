"""Wyvendo Waveline, TALON - the example pilot of Docs/Pilots/INVENTORY.md section 3.1 (Human / Lynx / Jackal,
Hollowfen Trackers, a hunter: Ruby). Round 1 (2026-11-07): the first portrait painted through the kit, and the
callsign portrait TALON shows in the Hangar until a person-by-person portrait exists. "They cannot resist a dare."
"""
PORTRAIT = dict(
    name="Wyvendo",                          # Assets/_Graphics/Pilots/Portrait_Wyvendo.png
    pilot="pilot.callsign.talon",            # the PilotSO whose portrait this is (None: written, not wired)
    person="Wyvendo Waveline",
    callsign="TALON",
    heritage=("Lynx", "Jackal"),             # the head and build, then the lent feature
    faction="Hunter", faction_name="Hollowfen Trackers",
    pose="standing in a relaxed three-quarter pose, weight on one leg, turned slightly to the left, chin up with a "
         "daring half-smile at the viewer, one gauntleted hand resting on the coiled rope at the hip",
    extra="",
    source="Docs/Pilots/art/sources/wyvendo.png",   # the captured painting, over the key colour
    key=None,                                # brief.key_for: green for this coat and accent
    crop=None,
    exempt=(),
    before=None,
)
