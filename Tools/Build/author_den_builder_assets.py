#!/usr/bin/env python3
"""
Authors every serialized asset Den Builder needs (GameModes.DenBuilder = 115, hint toast 190 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 25 reserved for the Goby & Pistol's Mass game).

Den Builder is a timed build in Glasshouse's grazed cell: the Pistol earns energy jumping the trail and builds a den
from it at every perch (the Mass ability, Dens). The score is the mass standing in a domain's colour at the whistle
(VolumeRemaining: dens, perches, the islands' trail). Glasshouse's herds graze everything outside the nucleus, so where
to perch is the arena's question. The scene is Glasshouse's, cloned, its timed monitor swapped for Den Builder's.
No mode AI and nothing about the pair changes. See Assets/_Scripts/Controller/Arcade/DEN_BUILDER.md; the shared half
is goby_games_lib.py.

    python3 Tools/Build/author_den_builder_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import goby_games_lib as gg  # noqa: E402

MODE_ID = 115
TOAST_HINT = 190
ROUND_SECONDS = 150       # a den needs a ridden island first; kept in sync with DefaultDenBuilderRoundSeconds
DONOR_ROUND_SECONDS = 90  # Glasshouse's monitor block, as its generator authors it
# The reference stock a domain holds at the whistle, an estimate: three or four dens of ~40 prisms of 4 u (64 u^3 each,
# GobyPistolConfig.denBlockSize) plus a few islands of 1.8 x 1.4 x 0.5 u trail blocks.
DEN_BLOCK = 4.0
REFERENCE_DOMAIN_VOLUME = 3.5 * 40 * DEN_BLOCK ** 3 + 4 * 200 * 1.8 * 1.4 * 0.5
COMEBACK_RATE = 0.0005
LEAD = {"leadSampleSeconds": 1, "leadAnnounceAfterSeconds": 20, "leadAnnounceMinGapSeconds": 8}

errors = []
config = open(os.path.join(lib.ROOT, gg.GOBY_CONFIG), encoding="utf-8").read()
if f"  denBlockSize: {lib.num(DEN_BLOCK)}\n" not in config:
    errors.append(f"GobyPistolConfig.denBlockSize is no longer {DEN_BLOCK} - re-derive REFERENCE_DOMAIN_VOLUME")
ctrl = open(os.path.join(lib.ROOT, "Assets/_Scripts/Controller/Arcade/DenBuilder/DenBuilderController.cs")).read()
for k, v in LEAD.items():
    if f"{k} = {lib.num(float(v))}f;" not in ctrl:
        errors.append(f"DenBuilderController.{k} initializer differs from the authored {v}")

gg.author(gg.Spec(
    mode_id=MODE_ID, name="DenBuilder", display="Den Builder", doc="DEN_BUILDER.md", toast_hint=TOAST_HINT,
    element="Mass",
    donor="Glasshouse", donor_monitor="GlasshouseTimeTurnMonitor", donor_vessel_id=lib.VESSEL_CLASS_ID["Angler"],
    monitor="DenBuilderTimeTurnMonitor", metric=12, golf=0,
    rule_fields="""  volumeTemplate: '{count} Volume'
  gardenTemplate: '{count} prisms'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: MASS STANDING
""",
    end_key="denBuilderRoundSeconds", end_value=ROUND_SECONDS,
    comeback_rate=COMEBACK_RATE, comeback_reference=REFERENCE_DOMAIN_VOLUME,
    min_players=1, min_domains=1,   # the score is what you build: a solo run is a real game against the grazers
    description=("Goby and Pistol only - a timed build. From the Goby, dart and perch to lay the Pistol's islands. "
                 "From the Pistol, jump the hills - every trail block you fly over is energy, and a fall loses it - "
                 "and rest at each perch to build a den from it. Grazers eat whatever stands outside the core, and "
                 "rival rings smash it. Higher intensities bring more grazers and fewer crystals. The team with the "
                 "most mass standing at the whistle wins."),
    toasts=(lib.toast(130, "{0} takes the lead - {1} volume standing", tint_domain=1, domain_names=0)
            + lib.toast(TOAST_HINT, "Jump the trail from the Pistol for energy - rest at a perch to build a den from it. "
                        "Grazers eat dens outside the core", domain_names=0)),
    preview_notes=("Glasshouse's grazed cell at each intensity, flown by the Goby and Pistol. OPEN-ENDED - it scores "
                   "on mass standing in your colour; what a solo preview teaches is the loop - lay islands, jump them "
                   f"for energy, build a den at each perch. Vessel pinned to Goby({gg.GOBY_ID})."),
    objective_text="Jump the trail for energy, then rest at a perch to build a den",
    controller_fields="".join(f"  {k}: {v}\n" for k, v in LEAD.items()),
    monitor_lines=f"  duration: {ROUND_SECONDS}\n",
    donor_monitor_lines=f"  duration: {DONOR_ROUND_SECONDS}\n",
    kit_needles=[
        ("void LayDenPrism(Den den)", "the den is the Mass ability this game scores"),
        ("var prism = BoostRingBuilder.LayOne(channel, den.Pos + outward, rot, Vector3.one * Config.DenBlockSize, kind,\n"
         "                _status.Domain, _status.PlayerName, _status.PlayerName);",
         "a den prism must be laid in the pilot's name so StatsManager counts it as their VolumeRemaining"),
    ],
    extra_errors=errors,
))
