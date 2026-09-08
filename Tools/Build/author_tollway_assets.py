#!/usr/bin/env python3
"""
Authors every serialized asset the Tollway game mode needs (GameModes.Tollway = 48).

Tollway is the Scarab-only RING RACE, and the mode built on the one Scarab idea no shipped mode
had ever used: a switch pays its PLACER when ANY ball threads it, friend or enemy
(R_VesselActions/SCARAB.md 5, which calls it "the design's best idea"). Graft rings onto the
living plants growing in the court; every ball that threads one pays the pilot who planted it and
raises a 255-prism scarab-wing monument on the spot. Rings are CONSUMED when they pay, so they must be replanted - which is
exactly why the switch's charge had to start recharging (SCARAB.md 5.2, the same branch).

What this script authors is only what is genuinely Tollway's. The ball, the switch, the dais and
their tuning belong to the VESSEL and are referenced, never forked. The fauna species and the
cleanup crew are the Scramble arena's and are carried across verbatim - the cell is per-arena,
not per-mode. Two things ARE forked. The CELL CONFIG, for its volume ladder: a toll IS a monument
here, so a Tollway match raises three to five times the mass a Scramble match does and Scramble's
ladder would be crossed at both gates before the race was half run (the same argument
SCARABSCRAMBLE.md makes for why it could not inherit Astro League's). And the SPAWN PROFILE, for
the anchors: a Scarab's switch grafts onto a living plant's heart (ScarabSwitchAnchors, a VESSEL
rule that holds in every arena), so this cell has to grow plants - and Scramble's profile authors
no flora at all.

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output. Validates the whole result in memory and only then writes.

Run from the repo root:  python3 Tools/Build/author_tollway_assets.py [--check]

--check validates without writing (CI / pre-commit use).

See Assets/_Scripts/Controller/Arcade/TOLLWAY.md for what these numbers mean.
"""
import hashlib
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "TollwayController":       guid("script/TollwayController"),
    "TollwayTollTurnMonitor":  guid("script/TollwayTollTurnMonitor"),
    "TollwayScoringRuleSO":    guid("script/TollwayScoringRuleSO"),
    "TollwaySettingsSO":       guid("script/TollwaySettingsSO"),
    "TollwayObjectiveProvider": guid("script/TollwayObjectiveProvider"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameTollway":      guid("asset/ArcadeGameTollway"),
    "TollwayScoringRule":     guid("asset/TollwayScoringRule"),
    "TollwaySettings":        guid("asset/TollwaySettings"),
    "TollwayCellConfig":      guid("asset/TollwayCellConfig"),
    "TollwaySpawnProfile":    guid("asset/TollwaySpawnProfile"),
    "TollwayAnchorFlora":     guid("asset/TollwayAnchorFlora"),
    "GameToastConfigTollway": guid("asset/GameToastConfig_Tollway"),
    "ModePreviewTollway":     guid("asset/ModePreview_Tollway"),
    "MinigameTollway.unity":  guid("asset/MinigameTollway.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = {
    # script types
    "SO_ArcadeGame":          "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":       "01f934d50526431a9392a6ceca1dc33d",
    "GameToastConfigSO":      "86d1715b8f104fcc87cb60e015d4b563",
    "ModePreviewDefinitionSO": "9f1780f039eb88d45a9cd7a4a75f9e13",
    "FloraConfigurationSO":   "a32a297a7606432885f4d3e1f83bea9a",
    "SpawnProfileSO":         "e8d8aa5d835249798a256e18f2f7d912",
    # the donor's mode-specific wiring, swapped out of the cloned scene
    "ScarabScrambleController":      "360704a9d9bc44b8b122828125121f1e",
    "ScarabScrambleGoalTurnMonitor": "19433ae0bbcf410e96cec59d70d1f769",
    "ScarabScrambleScoringRule":     "5e656e2b47044242a4c2700561ebe788",
    "ScarabScrambleSettings":        "9151506d4c52d18a288fd54a99ef64b0",
    "ScarabScrambleCellConfig":      "4c79db5a1791ff3060644ec7d2aee0db",
    # reused arena content (the cell is per-ARENA, not per-mode)
    "ScarabScrambleSpawnProfile": "01b40762f7abf41e7e4b1eb70273090d",
    # the donor profile's three-species cleanup crew, carried across verbatim
    "ScrambleBrittlestarFauna": "2d82ef8cbefd205db7a329bb37d2a79c",
    "ScramblePiranhaFauna":     "eceaaeea8d613d2053f8d39ea6d56e50",
    "ScrambleTadpoleFauna":     "508c59d3c640345f9eda3929935fbf66",
    # the anchor species: Spire (a phyllotactic pillar - a marker that rises OUT of the ring
    # planted at its foot, because its heart sits at the root and it grows upward)
    "SpireFloraPrefab":  "becb04107104ecb6768ae2d6766e681e",
    "SpireFloraCharge":  "25e619766581c4807de09c87fddf877c",
    "SpireFloraMass":    "10859a22ceb9349219eabe30541f5341",
    "SpireFloraSpace":   "f46966ea66e278b0ad293ccecc672c1d",
    "SpireFloraTime":    "6771e101fe9fa81ae0161ecbffc4c901",
    "CellIcon":        "6aa1c06e11b265744a5f9fa8858ac72a",
    "MembranePrefab":  "6e330f85972faf843b8a128e7166f7b5",
    "NucleusPrefab":   "b9cf1833fa2493d4b8724ccb6740fb3a",
    "CytoplasmPrefab": "9cacd903fcf4643459f5f14ac811bb20",
    # shared content
    "Vessel_Scarab":   "b136d82d275e0f8ea1feef29f0d416a4",
}

# ── The race ─────────────────────────────────────────────────────────────────
# TOLLS a domain needs to win. RE-DERIVED when rings stopped being placeable anywhere and became
# ANCHORED (ScarabSwitchAnchors): a toll used to be "plant a ring in front of your ball and nudge",
# which is one move, and is now "fly to a plant, plant facing the line you want, then drive a ball
# across the court and through a mouth two dozen units wide". That is several times the work per
# point, so the race is shortened rather than left to become a grind - it now sits just under
# Scramble's 10 rather than above it. Kept in sync with
# EndConditionOverridesSO.DefaultTollwayTollTarget.
TOLL_TARGET = 8

# The comeback strength - a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`), which is
# exactly why it had to move with the target above: at the old 0.5 a quarter-of-target deficit
# (now 2 tolls) would buy 1.0 levels, sitting on the floor the generator asserts. At 0.75 it buys
# 1.5, the same felt comeback the 12-toll race had. The trap this guards has now bitten five modes.
COMEBACK_RATE = 0.75

# ── The volume ladder (the one thing the cell config is forked for) ──────────
# A spent switch raises a scarab-wing dais: 255 prisms, 50,773 box volume (SCARAB.md 5.1). In
# Scramble that is a rare event, so its ladder is "the trail band plus 3 and 7 spent switches".
# In Tollway a toll IS a dais, so the ladder has to be stated in the currency the match actually
# runs on. The trail band and the count headroom are Scramble's, unchanged - only the number of
# monuments differs.
# ── The court, and the ANCHOR FLORA studding it ──────────────────────────────
# A ring may only be grafted onto a LIVING PLANT'S HEART - a vessel rule now
# (ScarabSwitchAnchors), not a mode one - so this arena's job is to grow the anchors. The
# mode-owned toll-post system this replaces built, replicated and drew a set of sockets that
# existed in exactly one mode; a flora crystal is the same affordance the ecology already
# produces everywhere, and it is also food, a joustable heart and an element reward.
COURT_RADII = [480, 560, 640, 720]

# The membrane the Scramble/Tollway cell wears (CapsuleMembrane.prefab, radius 1200). Planting
# fractions are of the MEMBRANE, so this is what turns a court fraction into an authored one.
MEMBRANE_RADIUS = 1200.0

# The anchor field, expressed against the SMALLEST court (intensity 1) so the plants are inside
# the arena at every intensity. Same band the retired toll posts used - 0.4..0.85 of that court - held
# off the middle because the core is where balls are forged and crystals respawn, and off the
# wall so a ball can come at a ring from behind. The consequence at higher intensities is
# deliberate and worth stating: the court grows and the anchor field does not, so a bigger court
# is more open water around the same contested middle, which is what "traffic goes up" means here.
ANCHOR_COURT_INNER, ANCHOR_COURT_OUTER = 0.4, 0.85
ANCHOR_INNER = round(ANCHOR_COURT_INNER * COURT_RADII[0] / MEMBRANE_RADIUS, 4)   # 0.16
ANCHOR_OUTER = round(ANCHOR_COURT_OUTER * COURT_RADII[0] / MEMBRANE_RADIUS, 4)   # 0.34

# How many anchors the court offers. ONE number now rather than the old per-intensity ladder:
# intensity is court radius and crystal count, and a field that also thinned with intensity made
# two axes out of one. 14 sits in the middle of the 10..16 the posts used.
ANCHOR_PLANTS = 14

# Live-prism budget per plant, overriding the species' own 119..204. A Spire is a MARKER here,
# not scenery: it has to read from across the court and leave the ring mouth at its foot clear.
ANCHOR_PRISM_BUDGET = 40

# Seconds between re-seed ticks. A grazed anchor is a removed scoring surface, so it has to come
# back briskly - but by SEEDING, never by a respawn timer on a specific plant.
ANCHOR_RESEED_SECONDS = 20

# Mean leaf volume across the four Spire elements (2.89/4.59/1.70/3.40 square x 1.3 thick), which
# is what 14 plants rolling uniformly over the palette actually average. The spread is real
# (3.76 for Space, 27.39 for Mass) and is why this is a mean rather than a worst case: the ladder
# describes the arena a match is played in, not its extreme.
ANCHOR_LEAF_VOLUME = round(sum(e * e * 1.3 for e in (2.89, 4.59, 1.70, 3.40)) / 4.0, 2)
ANCHOR_PRISMS = ANCHOR_PLANTS * ANCHOR_PRISM_BUDGET
ANCHOR_VOLUME = int(round(ANCHOR_PRISMS * ANCHOR_LEAF_VOLUME))

# The AI's own aiming window - how close a free heart must be to its nose-line before it presses.
# Deliberately NOT the vessel's anchorReach: being wrong either way is free (see TollwaySettingsSO).
AI_ANCHOR_AIM_TOLERANCE = 70.0

DAIS_VOLUME = 50773          # measured, SCARAB.md 5.1
DAIS_PRISMS = 255
TRAIL_BAND_RESTLESS = 12000  # Scramble's pre-dais trail-only estimate
TRAIL_BAND_FRENZY = 36000
COUNT_BAND_RESTLESS = 900
COUNT_BAND_FRENZY = 3000
COUNT_HEADROOM = 1.6         # the ~1.6x Scramble's own count backstops carry
RESTLESS_DAISES = 6          # about a quarter of an 8-toll race across all domains: the crew arrives
FRENZY_DAISES = 16           # near full time in a close match: the court is a monument field


def _round_to(value: int, step: int) -> int:
    return int(round(value / step) * step)


def _num(value: float) -> str:
    """Unity serializes a whole float as `1`, not `1.0`; matching it keeps the next in-editor
    save from producing a spurious diff on a file this generator owns."""
    return str(int(value)) if float(value).is_integer() else str(value)


# The anchor forest is STANDING mass from the first seconds of the match, at every phase, so it
# is part of both bands rather than something the ladder discovers later.
RESTLESS_ENTER_VOLUME = _round_to(
    TRAIL_BAND_RESTLESS + ANCHOR_VOLUME + RESTLESS_DAISES * DAIS_VOLUME, 1000)
RESTLESS_EXIT_VOLUME = RESTLESS_ENTER_VOLUME - 4000
FRENZY_ENTER_VOLUME = _round_to(
    TRAIL_BAND_FRENZY + ANCHOR_VOLUME + FRENZY_DAISES * DAIS_VOLUME, 1000)
FRENZY_EXIT_VOLUME = FRENZY_ENTER_VOLUME - 6000
RESTLESS_ENTER = _round_to(
    int(COUNT_BAND_RESTLESS + ANCHOR_PRISMS + RESTLESS_DAISES * DAIS_PRISMS * COUNT_HEADROOM), 10)
RESTLESS_EXIT = RESTLESS_ENTER - 100
FRENZY_ENTER = _round_to(
    int(COUNT_BAND_FRENZY + ANCHOR_PRISMS + FRENZY_DAISES * DAIS_PRISMS * COUNT_HEADROOM), 10)
FRENZY_EXIT = FRENZY_ENTER - 210

# ── The crystal economy: INTENSITY IS TRAFFIC ────────────────────────────────
# Intensity here is how much is flying around, because traffic is what pays tolls. Low intensity
# is the party setting - a small court thick with balls, where a ring pays often and placement is
# forgiving. High intensity is the sweaty one - a big court and barely any balls, where every ring
# has to be aimed at a line somebody will actually fly. CrystalCountMode.IntensityScaled (2), the
# Rampage shape: max(1, round(players x CrystalsPerPlayer) + ExtraCrystals).
CRYSTALS_BY_INTENSITY = [
    (1.0, 3),   # I1 - 4 players -> 7
    (1.0, 2),   # I2 -> 6
    (0.75, 1),  # I3 -> 4
    (0.5, 0),   # I4 -> 2
]

_HEADER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def HEADER_FOR(script_guid: str, name: str) -> str:
    return _HEADER_TMPL.replace("__GUID__", script_guid).replace("__NAME__", name)


def meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


files: "dict[str, str]" = {}


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


# ── 1. .cs.meta for the scripts assets/scenes point at ───────────────────────
SCRIPT_PATHS = {
    "TollwayController":        "Assets/_Scripts/Controller/Arcade/Tollway/TollwayController.cs",
    "TollwayScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Tollway/TollwayScoringRuleSO.cs",
    "TollwaySettingsSO":        "Assets/_Scripts/Controller/Arcade/Tollway/TollwaySettingsSO.cs",
    "TollwayObjectiveProvider": "Assets/_Scripts/Controller/Arcade/Tollway/TollwayObjectiveProvider.cs",
    "TollwayTollTurnMonitor":   "Assets/_Scripts/Controller/Arcade/TurnMonitors/TollwayTollTurnMonitor.cs",
}
for k, p in SCRIPT_PATHS.items():
    emit(p + ".meta", meta(G_SCRIPT[k]))


# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 4 = ScoringMetric.Goals, points not golf - the Astro League / Scramble race shape.
# Tollway reuses the metric because the SHAPE of the race is the same; what differs is only what
# a goal IS (a ball through a ring a PILOT planted, which pays that pilot and is consumed).
emit("Assets/_SO_Assets/Scoring Rules/TollwayScoringRule.asset",
     HEADER_FOR(G_SCRIPT["TollwayScoringRuleSO"], "TollwayScoringRule") +
     "  metric: 4\n  golfRules: 0\n")
emit("Assets/_SO_Assets/Scoring Rules/TollwayScoringRule.asset.meta",
     asset_meta(G_ASSET["TollwayScoringRule"]))


# ── 3. Mode settings ─────────────────────────────────────────────────────────
# Court radius climbs with intensity (a bigger court is harder to cover, and covering lines is
# the skill). Nothing about the SWITCH is here - that is PlaceSwitchAction.asset's, because a
# Scarab plants rings in freestyle and in Scramble too and a second author would drift. Nor is
# the ANCHOR RULE: where a ring may go is the vessel's (ScarabSwitchAnchors), and the anchors
# themselves are the cell's spawn profile. What is left here is the AI's aiming window.
emit("Assets/_SO_Assets/Games/TollwaySettings.asset",
     HEADER_FOR(G_SCRIPT["TollwaySettingsSO"], "TollwaySettings") + """  courtRadiusByIntensity: 0100000000000000
  aiAnchorAimTolerance: __AI_AIM__
  chainWindowSeconds: 4
  faunaWaitOutsideCourt: 1
  faunaExclusionCourtFraction: 1
  faunaExclusionSweepSeconds: 3
  aiRetargetSeconds: 1
  aiApproachLead: 45
  aiInterceptLeadSeconds: 0.5
  aiSwitchIntervalSeconds: 22
  aiFirstSwitchDelaySeconds: 5
""".replace("  courtRadiusByIntensity: 0100000000000000\n",
            "  courtRadiusByIntensity:\n" + "".join(f"  - {_num(r)}\n" for r in COURT_RADII))
   .replace("__AI_AIM__", _num(AI_ANCHOR_AIM_TOLERANCE)))
emit("Assets/_SO_Assets/Games/TollwaySettings.asset.meta",
     asset_meta(G_ASSET["TollwaySettings"]))



# ── 3b. The ANCHOR SPECIES, and the profile that seeds it ────────────────────
# The mode needs points a ring can be planted on. It does NOT build them: a Scarab grafts its
# switch onto a living plant's heart in every arena (ScarabSwitchAnchors), so all this cell has
# to do is grow plants - which is the Cell's ordinary job, through the ordinary spawner, with
# the plants ordinary food-web citizens (grazeable, joustable, crystal-dropping) from the frame
# they exist. That is the whole reason the toll-post system was deleted rather than tuned.
#
# NetworkSynced is LOAD-BEARING here and is why the species is forked rather than referenced:
# a switch placement re-executes on every peer, so every peer has to agree about where the
# anchors are, and flora are per-peer by default (each machine runs its own spawner off its own
# UnityEngine.Random). FloraNetworkSync replicates the planting DECISION - species, root pose,
# domain and element - which is exactly the set the heart's world position is a function of.
# This is the first shipped user of that mechanism; the Tollway scene's cell already carries the
# component (it came across with the Scramble clone).
#
# SpreadElements over the four canonical Spire assets rather than one element, because a heart
# IS an element reward: jousting an anchor kills the plant, frees its crystal and removes a
# scoring surface, which is real counter-play, and a court of four different hearts pays four
# different amounts for it. The cell-level overrides win over the rolled palette sibling
# (FloraConfigurationSO.TryBuildCellOverrideTuning), which is what lets this cell pick the band
# and the plant size without forking four element assets.
emit("Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Anchor Flora.asset",
     HEADER_FOR(EXISTING["FloraConfigurationSO"], "Tollway Anchor Flora") + f"""  FloraPrefab: {{fileID: 7514956980722975813, guid: {EXISTING['SpireFloraPrefab']}, type: 3}}
  NetworkSynced: 1
  SpawnProbability: 1
  InitialSpawnCount: {ANCHOR_PLANTS}
  OverrideDefaultPlantPeriod: 1
  NewPlantPeriod: {ANCHOR_RESEED_SECONDS}
  PopulationSize: {ANCHOR_PLANTS}
  MaxLivePopulation: {ANCHOR_PLANTS}
  GrowthPerOffspring: 0
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 5
  MaturityFraction: 0
  OffspringSpread: 60
  PreferredSites: 17
  Element: 0
  Variant:
    Enabled: 0
  SpreadElements: 1
  ElementPalette:
  - {{fileID: 11400000, guid: {EXISTING['SpireFloraCharge']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['SpireFloraMass']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['SpireFloraSpace']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['SpireFloraTime']}, type: 2}}
  PlantRadiusCellFractionMaxOverride: {_num(ANCHOR_OUTER)}
  PlantRadiusCellFractionMinOverride: {_num(ANCHOR_INNER)}
  MaxTotalSpawnedObjectsOverride: {ANCHOR_PRISM_BUDGET}
""")
emit("Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Anchor Flora.asset.meta",
     asset_meta(G_ASSET["TollwayAnchorFlora"]))

# The profile is a fork of Scramble's for ONE reason - it grows the anchors - so every fauna
# number is carried across verbatim rather than re-derived. The cleanup crew is the same three
# species waiting outside the court until the volume ladder leaves Calm.
emit("Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Spawn Profile.asset",
     HEADER_FOR(EXISTING["SpawnProfileSO"], "Tollway Spawn Profile") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras:
  - {{fileID: 11400000, guid: {G_ASSET['TollwayAnchorFlora']}, type: 2}}
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 8
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 30
  FaunaFoodFloor: 5
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 2
  SupportedFaunas:
  - {{fileID: 11400000, guid: {EXISTING['ScrambleBrittlestarFauna']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['ScramblePiranhaFauna']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['ScrambleTadpoleFauna']}, type: 2}}
""")
emit("Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Spawn Profile.asset.meta",
     asset_meta(G_ASSET["TollwaySpawnProfile"]))


# ── 4. Cell config (forked from Scramble's for its LADDER, nothing else) ─────
CELL_DESC = (
    "Ring-court cell for Tollway. Scarab Scramble's arena - the nucleus IS the sphere court (play "
    "geometry, not a claim; the controller clears NucleusIsControlZone) and the same three-species "
    "cleanup crew waits outside it until the volume ladder leaves Calm - forked for exactly TWO "
    f"reasons. (1) ANCHORS: a Scarab's switch grafts onto a living plant's heart, so this cell "
    f"grows {ANCHOR_PLANTS} NetworkSynced Spire plants in a band inside the court and those hearts "
    "ARE the scoring sockets. Scramble authors no flora at all, so its profile could not be "
    "reused. (2) The LADDER. In Scramble a switch dais is a rare event; here a TOLL IS A DAIS, so "
    "a match raises three to five times the mass and Scramble's gates (Restless 164,000 / Frenzy "
    "391,000) would both be crossed before the race was half run, after which the ladder conveys "
    "nothing. Restated in the currency this mode runs on: the trail band plus the standing anchor "
    f"forest ({ANCHOR_VOLUME} volume, {ANCHOR_PRISMS} prisms) plus 6 monuments for Restless and 16 "
    "for Frenzy, at 50,773 volume and 255 prisms each (SCARAB.md 5.1). ESTIMATE pending the "
    "in-editor baseline measure; see TOLLWAY.md."
)
emit("Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Cell Config.asset",
     HEADER_FOR(EXISTING["CellConfigDataSO"], "Tollway Cell Config") + f"""  CellName: Tollway
  Description: {CELL_DESC}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: 2
  CellEndGameScore: 0
  MembranePrefab: {{fileID: 346633111830028674, guid: {EXISTING['MembranePrefab']}, type: 3}}
  NucleusPrefab: {{fileID: 7555898194514117247, guid: {EXISTING['NucleusPrefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: 639495419069806261, guid: {EXISTING['CytoplasmPrefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['TollwaySpawnProfile']}, type: 2}}
  SenseRadiusOverride: 1300
  PhaseThresholds:
    RestlessEnter: {RESTLESS_ENTER}
    RestlessExit: {RESTLESS_EXIT}
    FrenzyEnter: {FRENZY_ENTER}
    FrenzyExit: {FRENZY_EXIT}
    RestlessEnterVolume: {RESTLESS_ENTER_VOLUME}
    RestlessExitVolume: {RESTLESS_EXIT_VOLUME}
    FrenzyEnterVolume: {FRENZY_ENTER_VOLUME}
    FrenzyExitVolume: {FRENZY_EXIT_VOLUME}
""")
emit("Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Cell Config.asset.meta",
     asset_meta(G_ASSET["TollwayCellConfig"]))


# ── 5. Arcade game config ────────────────────────────────────────────────────
# SCARAB ONLY: a single entry in Vessels drives all three enforcement layers (the launcher clamp,
# the server-side spawn clamp, and the AI clamp). MinDomainsAllowed 2 because a toll race needs a
# rival - a one-domain lobby is a building exercise with nobody to lose to.
emit("Assets/_SO_Assets/Games/ArcadeGameTollway.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameTollway") + f"""  Mode: 48
  IsMultiplayer: 1
  DisplayName: Tollway
  Description: Plants grow in the court, and a ring can only be grafted onto one. Fly
    at a plant, plant your ring at its foot, then drive a ball across the court and
    through the mouth - and every ball that threads it, yours or theirs or a stray off
    the wall, pays YOU and raises a monument on the spot, so the arena gets built out of
    the scoring. Rings are spent when they pay, so keep planting. Scarab only. First
    team to {TOLL_TARGET} tolls.
  IconActive: {{fileID: 0}}
  IconInactive: {{fileID: 0}}
  CardBackground: {{fileID: 0}}
  PreviewClip: {{fileID: 0}}
  GolfScoring: 0
  SceneName: MinigameTollway
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Scarab']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  CallToActionTargetType: 0
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {COMEBACK_RATE}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameTollway.asset.meta",
     asset_meta(G_ASSET["ArcadeGameTollway"]))


# ── 6. Toasts ────────────────────────────────────────────────────────────────
def toast(situation: int, template: str, tint_domain: int = 0, domain_names: int = 1,
          idle: int = 0, idle_seconds: int = 60) -> str:
    return (f"  - situation: {situation}\n"
            f"    messageTemplate: '{template}'\n"
            f"    tintWithDomainColor: {tint_domain}\n"
            f"    useDomainColoredNames: {domain_names}\n"
            f"    alpha: 1\n"
            f"    isIdleHint: {idle}\n"
            f"    resetOnSituation: 0\n"
            f"    idleSeconds: {idle_seconds}\n"
            f"    repeatWhileIdle: 1\n")


emit("Assets/_SO_Assets/Game Toasts/GameToastConfig_Tollway.asset",
     HEADER_FOR(EXISTING["GameToastConfigSO"], "GameToastConfig_Tollway") +
     "  gameMode: 48\n  toasts:\n" +
     toast(70, "{0} collects a toll - {1}/{2}") +
     toast(71, "CHAIN x{3}! {0} collects again - {1}/{2}") +
     toast(72, "MATCH POINT - {0} needs one more toll", tint_domain=1, domain_names=0) +
     toast(73, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
     toast(74, "Plant a ring on a plant - ANY ball through it pays YOU", idle=1, idle_seconds=25) +
     # Not an idle hint: this one fires ON A REFUSED PRESS, which is the moment the rule needs
     # stating. Before it, a press with no anchor on the line wrote one verbose log line and
     # nothing else - indistinguishable on screen from a dead button.
     toast(75, "No plant on this line - fly at one and plant your ring"))
emit("Assets/_SO_Assets/Game Toasts/GameToastConfig_Tollway.asset.meta",
     asset_meta(G_ASSET["GameToastConfigTollway"]))

TOAST_LIB = "Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset"
lib = read(TOAST_LIB)
lib_entry = f"  - {{fileID: 11400000, guid: {G_ASSET['GameToastConfigTollway']}, type: 2}}\n"
if lib_entry not in lib:
    assert lib.endswith("\n")
    lib = lib + lib_entry
emit(TOAST_LIB, lib)


# ── 7. Mode preview ──────────────────────────────────────────────────────────
emit("Assets/_SO_Assets/Mode Previews/ModePreview_Tollway.asset",
     HEADER_FOR(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Tollway") + f"""  Mode: 48
  Notes: 'OPEN-ENDED: the rings are PLACED BY PILOTS at runtime, so a preview arena has
    nothing to thread until somebody plants one - which is the mode being honest rather
    than a gap. The court sphere, the ball forge and the juke dash all work. If a
    StructurePrefab is ever added it should be a couple of standing rings, not hoops.'
  PreviewCell: {{fileID: 11400000, guid: {G_ASSET['TollwayCellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: 12
  ObjectiveText: Plant rings and run the traffic
  ObjectiveMetric: 4
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 760
  SpawnFormation: 0
  SpawnPoints: []
""")
emit("Assets/_SO_Assets/Mode Previews/ModePreview_Tollway.asset.meta",
     asset_meta(G_ASSET["ModePreviewTollway"]))

PREVIEW_LIB = "Assets/Resources/ModePreviewLibrary.asset"
plib = read(PREVIEW_LIB)
pentry = f"  - {{fileID: 11400000, guid: {G_ASSET['ModePreviewTollway']}, type: 2}}\n"
if pentry not in plib:
    m = re.search(r"^  Definitions:\n((?:  - \{fileID[^\n]*\n)+)", plib, re.M)
    assert m, "ModePreviewLibrary Definitions block not found"
    plib = plib.replace(m.group(0), m.group(0) + pentry, 1)
emit(PREVIEW_LIB, plib)


# ── 8. Scene: clone MinigameScarabScramble, swap the mode-specific wiring ────
# The donor already IS the court arena this mode wants - Scarab AI templates, the nucleus-as-court
# cell, the spawn ring, the crystal manager - so the clone swaps the mode identity (controller,
# turn monitor, rule, settings), the cell config (for the ladder) and the crystal economy.
scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble.unity")

for donor_key, new_guid, label in (
    ("ScarabScrambleController", G_SCRIPT["TollwayController"], "controller"),
    ("ScarabScrambleGoalTurnMonitor", G_SCRIPT["TollwayTollTurnMonitor"], "turn monitor"),
    ("ScarabScrambleScoringRule", G_ASSET["TollwayScoringRule"], "scoring rule"),
    ("ScarabScrambleSettings", G_ASSET["TollwaySettings"], "settings"),
    ("ScarabScrambleCellConfig", G_ASSET["TollwayCellConfig"], "cell config"),
):
    scene, n = re.subn(EXISTING[donor_key], new_guid, scene)
    assert n == 1, f"{label} guid appeared {n} times in the donor scene (expected exactly 1)"

# THE CRYSTAL ECONOMY - the one gameplay dial this mode moves on the donor. Scramble runs
# PlayerCountPlusExtra +2; Tollway runs IntensityScaled, because here the crystal count IS the
# intensity axis: crystals become balls and balls are the traffic that pays tolls.
OLD_CRYSTALS = ("  crystalCountMode: 1\n"
                "  fixedCrystalCount: 1\n"
                "  extraCrystalsToSpawnBeyondPlayerCount: 2\n"
                "  crystalCountByIntensity: []\n")
NEW_CRYSTALS = ("  crystalCountMode: 2\n"
                "  fixedCrystalCount: 1\n"
                "  extraCrystalsToSpawnBeyondPlayerCount: 0\n"
                "  crystalCountByIntensity:\n"
                + "".join(f"  - CrystalsPerPlayer: {_num(p)}\n    ExtraCrystals: {e}\n"
                          for p, e in CRYSTALS_BY_INTENSITY))
assert OLD_CRYSTALS in scene, "donor crystal-count block not found"
scene = scene.replace(OLD_CRYSTALS, NEW_CRYSTALS, 1)

emit("Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity", scene)
emit("Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity.meta",
     scene_meta(G_ASSET["MinigameTollway.unity"]))


# ── 9. Register the card in the party-games list ────────────────────────────
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameTollway']}, type: 2}}\n"
if entry not in games:
    assert games.endswith("\n")
    games = games + entry
emit(LIST_PATH, games)


# ── 10. Always-unlocked so the card is clickable on a fresh account ─────────
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
# Mode 48 (see GameModes.Tollway). This said 45 until the renumber that moved Tollway off its
# collision with Hijack, and 45 was ALREADY in the list (Switchback) - so the check passed, the
# insert never ran, and Tollway shipped locked. A registration keyed on a number that another
# feature also holds cannot report its own failure.
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 48\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 48\n", prog, count=1)
    assert n == 1, "alwaysUnlockedModes block not found"
emit(PROG_PATH, prog)


# ── 11. Build settings ──────────────────────────────────────────────────────
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameTollway.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameScarabScramble\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    assert anchor, "Scarab Scramble scene entry not found in EditorBuildSettings"
    build = build.replace(anchor.group(1), anchor.group(1) +
                          "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity\n"
                          f"    guid: {G_ASSET['MinigameTollway.unity']}\n")
emit(BUILD_PATH, build)


# ── 12. End-game condition target ───────────────────────────────────────────
# SET semantics, not add-if-absent (the Dog Fight generator's lesson: an insert-only key left the
# asset on a stale number after a target retune).
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for live_key, new_key in (("scarabScrambleGoalTarget", "tollwayTollTarget"),
                          ("scarabScrambleGoalTargetBuild", "tollwayTollTargetBuild")):
    existing = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if existing:
        endcond = endcond.replace(existing.group(0), f"  {new_key}: {TOLL_TARGET}\n", 1)
        continue
    m = re.search(rf"^  {live_key}: (\d+)\n", endcond, re.M)
    assert m, f"{live_key} not found in {END_PATH}"
    endcond = endcond.replace(m.group(0), m.group(0) + f"  {new_key}: {TOLL_TARGET}\n", 1)
emit(END_PATH, endcond)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []

all_new = list(G_SCRIPT.values()) + list(G_ASSET.values())
if len(set(all_new)) != len(all_new):
    errors.append("minted GUID collision within this script")

# .meta files THIS script owns are excluded from the collision sweep - otherwise a second run
# flags its own (byte-identical) output as a collision and the script stops being idempotent.
owned_metas = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files if rel.endswith(".meta")}

existing_guids = set()
for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in filenames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.normpath(os.path.join(dirpath, fn))
        if full in owned_metas:
            continue
        try:
            with open(full, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
            if m:
                existing_guids.add(m.group(1))
        except OSError:
            pass
for g in all_new:
    if g in existing_guids:
        errors.append(f"minted GUID {g} collides with an asset this script does not own")

for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")

# the scene must no longer mention the donor's mode-specific guids
sc = files["Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity"]
for name in ("ScarabScrambleController", "ScarabScrambleGoalTurnMonitor",
             "ScarabScrambleScoringRule", "ScarabScrambleSettings", "ScarabScrambleCellConfig"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("TollwayController", "TollwayTollTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
for name in ("TollwayScoringRule", "TollwaySettings", "TollwayCellConfig"):
    if G_ASSET[name] not in sc:
        errors.append(f"cloned scene missing the {name} reference")
# The profile IS forked here (Scramble authors no flora and this mode's sockets are plants), so
# the check is that the cell points at the forked one and that the forked one still carries the
# donor's whole cleanup crew - a fork that quietly loses a species is how an arena ends up with a
# ladder describing fauna it does not have.
_cell_asset = files["Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Cell Config.asset"]
if G_ASSET["TollwaySpawnProfile"] not in _cell_asset:
    errors.append("the Tollway cell config does not point at the Tollway spawn profile")
if EXISTING["ScarabScrambleSpawnProfile"] in _cell_asset:
    errors.append("the Tollway cell config still points at Scramble's flora-less spawn profile")
_profile = files["Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Spawn Profile.asset"]
for _k in ("ScrambleBrittlestarFauna", "ScramblePiranhaFauna", "ScrambleTadpoleFauna"):
    if EXISTING[_k] not in _profile:
        errors.append(f"the forked spawn profile dropped {_k} from the cleanup crew")
if G_ASSET["TollwayAnchorFlora"] not in _profile:
    errors.append("the forked spawn profile seeds no anchor flora - nothing in the court could "
                  "carry a ring, so the mode would be unplayable")
_flora = files["Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Anchor Flora.asset"]
if "  NetworkSynced: 1\n" not in _flora:
    errors.append("the anchor species is not NetworkSynced - flora are per-peer by default, so "
                  "two machines would snap a ring onto different plants and disagree about where "
                  "it stands, permanently (nothing about a placed switch is replicated)")
if "  crystalCountMode: 2\n" not in sc:
    errors.append("scene is not on CrystalCountMode.IntensityScaled - the crystal count IS this "
                  "mode's intensity axis")
if sc.count("CrystalsPerPlayer:") != len(CRYSTALS_BY_INTENSITY):
    errors.append(f"scene must author exactly {len(CRYSTALS_BY_INTENSITY)} crystal intensity rows")
if sc.count("vesselClass: 12") != 4:
    errors.append("scene does not author 4 Scarab AI templates")
# Goals (2) is what ElementalComebackSystem.DefaultSourceFor returns for this mode; a scene-
# authored comeback instance is respected as-is and never reconfigured, so the two must agree.
if "  differenceSource: 2\n" not in sc:
    errors.append("scene's ElementalComebackSystem is not on ScoreDifferenceSource.Goals")

# Scarab-only must be a SINGLE entry, or the clamps let another hull through
arcade = files["Assets/_SO_Assets/Games/ArcadeGameTollway.asset"]
vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
if not vessels or vessels.group(1).count("- {fileID") != 1:
    errors.append("ArcadeGameTollway must author EXACTLY ONE vessel (Scarab)")
elif EXISTING["Vessel_Scarab"] not in vessels.group(1):
    errors.append("ArcadeGameTollway's single vessel is not Scarab")
if "MinDomainsAllowed: 2" not in arcade:
    errors.append("ArcadeGameTollway must require at least TWO domains - a one-domain lobby is a "
                  "toll race with nobody to lose to")

# ── ANCHORS: the band must actually be a band ────────────────────────────────
# The anchor field is the rule the mode turns on (TOLLWAY.md "Anchors"), and three of its
# properties are arithmetic rather than taste, so they are asserted here over the SHIPPED numbers
# rather than eyeballed in the editor. Note there is no layout to walk any more: the plants are
# placed by the CELL's ordinary volume-uniform band draw, which is why this replaced a 60-line
# re-derivation of a mode-owned Fibonacci walk with three inequalities.
_anchor_inner_world = ANCHOR_INNER * MEMBRANE_RADIUS
_anchor_outer_world = ANCHOR_OUTER * MEMBRANE_RADIUS

#   * every anchor must be INSIDE the smallest court, at every intensity. The band is authored in
#     membrane fractions while the court is resized per intensity, so this is the one place the
#     two coordinate systems are reconciled.
if _anchor_outer_world > min(COURT_RADII):
    errors.append(f"the anchor band reaches {_anchor_outer_world:.0f}u, outside the intensity-1 "
                  f"court ({min(COURT_RADII)}u) - plants would seed beyond the wall a ball can "
                  f"never cross, so a pilot could see anchors they can never reach")

#   * no anchor may sit in the MIDDLE. An anchor has to be a place you FLY TO; one near the centre
#     is claimed incidentally by anyone crossing the court, which is the effortless placement the
#     whole mechanism exists to remove, back by the front door. (The crystals are no help here:
#     the nucleus IS the court in this mode, so they respawn across the whole volume rather than
#     in a core - the band's inner edge is the only thing holding the middle open.)
_core_floor = AI_ANCHOR_AIM_TOLERANCE + 24.0     # one aim window plus one unupgraded ring mouth
if _anchor_inner_world <= _core_floor:
    errors.append(f"the anchor band starts {_anchor_inner_world:.0f}u from the court centre, "
                  f"inside the {_core_floor:.0f}u a pilot crossing the middle would claim "
                  f"incidentally")

#   * the band must have DEPTH. Collapsed to a shell, every anchor sits at one distance from the
#     middle and "which anchor" becomes a purely angular choice. This also fails loudly if the
#     nucleus-as-court clamp ever comes back: Flora.ResolvePlantRadius returns the OUTER edge when
#     inner >= outer, so a re-clamped band would degenerate to exactly that shell.
if _anchor_outer_world - _anchor_inner_world < 100.0:
    errors.append(f"the anchor band is only {_anchor_outer_world - _anchor_inner_world:.0f}u deep "
                  f"- with no depth, which anchor to claim is a purely angular choice")

# The comeback rate only means anything relative to the TARGET. A quarter-of-target deficit must
# buy at least one whole element level (the trap that has now bitten four modes).
_quarter_deficit_levels = (TOLL_TARGET * 0.25) * COMEBACK_RATE
if _quarter_deficit_levels < 1.0:
    errors.append(f"ComebackRatePerScoreDeficit {COMEBACK_RATE} is too small for a "
                  f"{TOLL_TARGET}-toll target: a quarter-of-target deficit buys only "
                  f"{_quarter_deficit_levels:.2f} element levels, which is invisible")

# The ladder must be ORDERED and must sit above the mass a match actually makes, or it stops
# carrying information the moment the monuments start going up.
cell = files["Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Cell Config.asset"]
if not (RESTLESS_EXIT_VOLUME < RESTLESS_ENTER_VOLUME < FRENZY_EXIT_VOLUME < FRENZY_ENTER_VOLUME):
    errors.append("volume ladder is not strictly ordered exit<enter<exit<enter")
if not (RESTLESS_EXIT < RESTLESS_ENTER < FRENZY_EXIT < FRENZY_ENTER):
    errors.append("count ladder is not strictly ordered exit<enter<exit<enter")
# The whole point of the fork: Frenzy must NOT be reachable by the first few monuments.
if FRENZY_ENTER_VOLUME <= TRAIL_BAND_FRENZY + 8 * DAIS_VOLUME:
    errors.append("FrenzyEnterVolume is reachable inside the Restless monument budget - the "
                  "ladder would stop conveying anything early in the race, which is exactly the "
                  "defect this fork exists to avoid")
# ...and it must be reachable at all inside a maximum-length match (target-1 tolls for each of
# two losing domains plus the winner's target).
_max_daises = TOLL_TARGET + 2 * (TOLL_TARGET - 1)
if FRENZY_ENTER_VOLUME > TRAIL_BAND_FRENZY + _max_daises * DAIS_VOLUME:
    errors.append(f"FrenzyEnterVolume is unreachable even in a maximum-length match "
                  f"({_max_daises} monuments) - the top of the ladder would be dead")


# serialized MonoBehaviour keys must exist on the C# class (asset-surgery)
def cs_fields(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as fh:
        src = fh.read()
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    MODS = (r"(?:(?:public|protected|private|internal|static|const|readonly|new|virtual|"
            r"override|abstract|sealed|partial)\s+)+")
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>|\()", src):
        out.add(m.group(1))
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    for m in re.finditer(r"^\s{4,}" + TYPE + r"\s+(\w+)\s*\{\s*get;", src, re.M):
        out.add(m.group(1))
    return out


SO_BASE = {"Mode", "IsMultiplayer", "DisplayName", "Description", "IconActive", "IconInactive",
           "CardBackground", "PreviewClip", "GolfScoring", "SceneName"}
CHECKS = [
    ("Assets/_SO_Assets/Games/ArcadeGameTollway.asset",
     "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs"),
    ("Assets/_SO_Assets/Scoring Rules/TollwayScoringRule.asset",
     "Assets/_Scripts/Controller/Arcade/Tollway/TollwayScoringRuleSO.cs"),
    ("Assets/_SO_Assets/Games/TollwaySettings.asset",
     "Assets/_Scripts/Controller/Arcade/Tollway/TollwaySettingsSO.cs"),
]
for asset_path, cs_path in CHECKS:
    keys = set(re.findall(r"^  (\w+):", files[asset_path], re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
        "m_EditorClassIdentifier"}
    known = cs_fields(cs_path) | SO_BASE
    for extra in ("Assets/_Scripts/ScriptableObjects/SO_Game.cs",
                  "Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs",
                  "Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueScoringRuleSO.cs"):
        # Deliberately NOT an if-exists skip: a base class that moved would silently weaken this
        # check into passing everything, which is worse than the missing key it exists to catch.
        if not os.path.exists(os.path.join(ROOT, extra)):
            errors.append(f"base class for the serialized-key check is missing: {extra}")
            continue
        known |= cs_fields(extra)
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on "
                      f"{os.path.basename(cs_path)}: {sorted(unknown)}")

# every serialized key the scene's controller block authors must exist on TollwayController.cs
CONTROLLER_CS = "Assets/_Scripts/Controller/Arcade/Tollway/TollwayController.cs"
controller_keys = {"settings", "rule", "arenaCell", "cellData"}
missing = controller_keys - cs_fields(CONTROLLER_CS)
if missing:
    errors.append(f"TollwayController.cs is missing serialized fields the scene authors: "
                  f"{sorted(missing)}")

# the C# default target and this script's must agree, or the tool window's "(default)" lies
endcond_cs = read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultTollwayTollTarget = (\d+);", endcond_cs)
if not m:
    errors.append("EndConditionOverridesSO.cs has no DefaultTollwayTollTarget")
elif int(m.group(1)) != TOLL_TARGET:
    errors.append(f"DefaultTollwayTollTarget ({m.group(1)}) != this script's "
                  f"TOLL_TARGET ({TOLL_TARGET}) - the two must move together")

# GameModes.Tollway must exist with the value this card authors
gamemodes_cs = read("Assets/_Scripts/Data/Enums/GameModes.cs")
if not re.search(r"^\s*Tollway = 48,", gamemodes_cs, re.M):
    errors.append("GameModes.cs has no 'Tollway = 48' - the card would launch nothing")

# the toast situations the controller posts must exist in the enum with these values
toast_cs = read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
for name, value in (("TollwayToll", 70), ("TollwayChain", 71), ("TollwayMatchPoint", 72),
                    ("TollwayLeadChanged", 73), ("TollwayRingHint", 74),
                    ("TollwayNoAnchor", 75)):
    if not re.search(rf"^\s*{name} = {value},", toast_cs, re.M):
        errors.append(f"GameToastSituation.{name} = {value} is missing")

if errors:
    print("VALIDATION FAILED - nothing written:")
    for e in errors:
        print("  x", e)
    sys.exit(1)

print(f"Validation passed ({len(files)} files).")
print(f"  toll target {TOLL_TARGET}, comeback {COMEBACK_RATE} "
      f"({_quarter_deficit_levels:.2f} levels at a quarter-target deficit)")
print(f"  ladder: Restless {RESTLESS_ENTER_VOLUME} vol / {RESTLESS_ENTER} prisms "
      f"(= trail band + {RESTLESS_DAISES} monuments)")
print(f"          Frenzy   {FRENZY_ENTER_VOLUME} vol / {FRENZY_ENTER} prisms "
      f"(= trail band + {FRENZY_DAISES} monuments); a maximum-length match raises "
      f"{_max_daises}")
for rel in sorted(files):
    print("  ", rel)

if CHECK_ONLY:
    print("\n--check: no files written.")
    sys.exit(0)

for rel, content in files.items():
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"\nWrote {len(files)} files.")
