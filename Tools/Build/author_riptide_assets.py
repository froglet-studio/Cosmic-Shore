#!/usr/bin/env python3
"""
Authors every serialized asset Riptide needs (GameModes.Riptide = 116, hint toast 191 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 26 reserved for the Goby & Pistol's Space game).

Riptide is Shatterplane's field of neutral plates for the Goby & Pistol: the undertow (the Space ability) pulls loose
tiles into the Pistol's plane and its vortex rings smash them as they collapse; first domain to the hostile-prism
target wins (Rampage's metric, Shatterplane's rule inherited). The arena is Shatterplane's four cells, referenced; the
scene is Shatterplane's, cloned. No mode AI and nothing about the pair changes.
See Assets/_Scripts/Controller/Arcade/RIPTIDE.md; the shared half is goby_games_lib.py.

    python3 Tools/Build/author_riptide_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import goby_games_lib as gg  # noqa: E402
import shatterplane_budget as sb  # noqa: E402

MODE_ID = 116
TOAST_HINT = 191
PRISM_TARGET = 600        # first cut, unplayed: a ring carries at most 30 (pVortexMax) and reloads in 4 s
COMEBACK_RATE = 0.01      # a quarter of the target (150 prisms) behind buys 1.5 element levels

errors = []
# The plates never regrow: three domains' worth of target must fit in the smallest breakable field.
smallest = min(sb.census(r)["breakable"] for r in sb.RUNGS)
if 3 * PRISM_TARGET > smallest:
    errors.append(f"three domains' target ({3 * PRISM_TARGET}) does not fit the smallest plate field ({smallest} "
                  f"breakable tiles): the field could run dry with nobody at the target")
config = open(os.path.join(lib.ROOT, gg.GOBY_CONFIG), encoding="utf-8").read()
if "  pVortexCollapse: 1\n" not in config:
    errors.append("GobyPistolConfig.pVortexCollapse is no longer 1 - a collapsing ring would not smash what it "
                  "carried, and Riptide would have only the Pistol's body to score with")

gg.author(gg.Spec(
    mode_id=MODE_ID, name="Riptide", display="Riptide", doc="RIPTIDE.md", toast_hint=TOAST_HINT, element="Space",
    donor="Shatterplane", donor_monitor="ShatterplanePrismTurnMonitor",
    donor_vessel_id=lib.VESSEL_CLASS_ID["BrittleStar"],
    monitor="RiptidePrismTurnMonitor", metric=5, golf=1,
    rule_fields="""  prismsLeftTemplate: '{count} Prisms Left'
  breakdownTemplate: '{count} Prisms'
  victoryHeader: VICTORY
  victoryLabel: RIPTIDE TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""",
    end_key="riptidePrismTarget", end_value=PRISM_TARGET,
    comeback_rate=COMEBACK_RATE, comeback_reference=PRISM_TARGET,
    min_players=1, min_domains=2,   # a race against the plates (Shatterplane's)
    description=("Goby and Pistol only - wide plates of prisms hang in the cell. From the Goby, dart your islands "
                 "past the plates. From the Pistol, the undertow pulls their tiles into your plane - fire vortex rings "
                 "to gather them and smash them, and break what you touch. Rival trails and dens count too. Higher "
                 "intensities turn the plates, stack them, and brace them with shielded ribs. First team to the target "
                 "wins."),
    toasts=(lib.toast(83, "<b>{0}</b> has broken {1} prisms", tint_domain=1, domain_names=0, every_n=100)
            + lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0)
            + lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0)
            + lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0)
            + lib.toast(TOAST_HINT, "Ride the Pistol past the plates - the undertow pulls their tiles in, and your rings "
                        "smash them", domain_names=0)),
    preview_notes=("Shatterplane's plates at each intensity, flown by the Goby and Pistol. A race to a prism target; "
                   "a solo preview teaches laying islands past the plates and the undertow pulling their tiles in. "
                   f"Vessel pinned to Goby({gg.GOBY_ID})."),
    objective_text="Ride past the plates - the undertow pulls their tiles in, your rings smash them",
    kit_needles=[
        ("prism.Damage((prism.transform.position - ring.C).normalized * 40f, _status.Domain, _status.PlayerName);",
         "a collapsing ring must break what it carried in the pilot's name, or nothing scores"),
        ("bool Pullable(Prism prism, bool shieldedToo)", "the undertow is what brings the plates' tiles into reach"),
        ("prism.Damage(vel, _status.Domain, _status.PlayerName);", "the Pistol's body breaks what it touches"),
    ],
    extra_errors=errors,
))
