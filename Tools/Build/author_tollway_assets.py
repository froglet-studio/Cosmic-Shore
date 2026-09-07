#!/usr/bin/env python3
"""
Authors every serialized asset the Tollway game mode needs (GameModes.Tollway = 48).

Tollway is the Scarab-only RING RACE, and the mode built on the one Scarab idea no shipped mode
had ever used: a switch pays its PLACER when ANY ball threads it, friend or enemy
(R_VesselActions/SCARAB.md 5, which calls it "the design's best idea"). Plant rings in the
court's own toll posts;
every ball that threads one pays the pilot who planted it and raises a 255-prism scarab-wing
monument on the spot. Rings are CONSUMED when they pay, so they must be replanted - which is
exactly why the switch's charge had to start recharging (SCARAB.md 5.2, the same branch).

What this script authors is only what is genuinely Tollway's. The ball, the switch, the dais and
their tuning belong to the VESSEL and are referenced, never forked. The fauna species and the
spawn profile are the Scramble arena's and are reused verbatim - the cell is per-arena, not
per-mode. The one thing that IS forked is the CELL CONFIG, and only for its volume ladder: a toll
IS a monument here, so a Tollway match raises three to five times the mass a Scramble match does
and Scramble's ladder would be crossed at both gates before the race was half run (the same
argument SCARABSCRAMBLE.md makes for why it could not inherit Astro League's).

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
    "TollwayTollPosts":        guid("script/TollwayTollPosts"),
}

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameTollway":      guid("asset/ArcadeGameTollway"),
    "TollwayScoringRule":     guid("asset/TollwayScoringRule"),
    "TollwaySettings":        guid("asset/TollwaySettings"),
    "TollwayCellConfig":      guid("asset/TollwayCellConfig"),
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
    # the donor's mode-specific wiring, swapped out of the cloned scene
    "ScarabScrambleController":      "360704a9d9bc44b8b122828125121f1e",
    "ScarabScrambleGoalTurnMonitor": "19433ae0bbcf410e96cec59d70d1f769",
    "ScarabScrambleScoringRule":     "5e656e2b47044242a4c2700561ebe788",
    "ScarabScrambleSettings":        "9151506d4c52d18a288fd54a99ef64b0",
    "ScarabScrambleCellConfig":      "4c79db5a1791ff3060644ec7d2aee0db",
    # reused arena content (the cell is per-ARENA, not per-mode)
    "ScarabScrambleSpawnProfile": "01b40762f7abf41e7e4b1eb70273090d",
    "CellIcon":        "6aa1c06e11b265744a5f9fa8858ac72a",
    "MembranePrefab":  "6e330f85972faf843b8a128e7166f7b5",
    "NucleusPrefab":   "b9cf1833fa2493d4b8724ccb6740fb3a",
    "CytoplasmPrefab": "9cacd903fcf4643459f5f14ac811bb20",
    # shared content
    "Vessel_Scarab":   "b136d82d275e0f8ea1feef29f0d416a4",
}

# ── The race ─────────────────────────────────────────────────────────────────
# TOLLS a domain needs to win. RE-DERIVED when rings stopped being placeable anywhere and became
# TOLL POSTS (TollwayTollPosts): a toll used to be "plant a ring in front of your ball and nudge",
# which is one move, and is now "fly to a socket, plant facing the line you want, then drive a ball
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
# ── The court, and the toll posts studding it ────────────────────────────────
# COURT_RADII and POST_COUNTS are authored ONCE and used twice: written into the settings asset
# below, and walked by the layout assertion further down. Two authors of a per-intensity ladder is
# how an asset and its own validation come to disagree.
COURT_RADII = [480, 560, 640, 720]
POST_COUNTS = [10, 12, 14, 16]
POST_INNER, POST_OUTER = 0.4, 0.85
POST_CLAIM_RADIUS = 70.0
POST_MARKER_RADIUS = 24.0

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


RESTLESS_ENTER_VOLUME = _round_to(TRAIL_BAND_RESTLESS + RESTLESS_DAISES * DAIS_VOLUME, 1000)
RESTLESS_EXIT_VOLUME = RESTLESS_ENTER_VOLUME - 4000
FRENZY_ENTER_VOLUME = _round_to(TRAIL_BAND_FRENZY + FRENZY_DAISES * DAIS_VOLUME, 1000)
FRENZY_EXIT_VOLUME = FRENZY_ENTER_VOLUME - 6000
RESTLESS_ENTER = _round_to(int(COUNT_BAND_RESTLESS + RESTLESS_DAISES * DAIS_PRISMS * COUNT_HEADROOM), 10)
RESTLESS_EXIT = RESTLESS_ENTER - 100
FRENZY_ENTER = _round_to(int(COUNT_BAND_FRENZY + FRENZY_DAISES * DAIS_PRISMS * COUNT_HEADROOM), 10)
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
    "TollwayTollPosts":         "Assets/_Scripts/Controller/Arcade/Tollway/TollwayTollPosts.cs",
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
# Scarab plants rings in freestyle and in Scramble too and a second author would drift.
emit("Assets/_SO_Assets/Games/TollwaySettings.asset",
     HEADER_FOR(G_SCRIPT["TollwaySettingsSO"], "TollwaySettings") + """  courtRadiusByIntensity: 0100000000000000
  postCountByIntensity: 0200000000000000
  postInnerCourtFraction: __POST_INNER__
  postOuterCourtFraction: __POST_OUTER__
  postClaimRadius: __POST_CLAIM__
  postMarkerRadius: __POST_MARKER__
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
   .replace("  postCountByIntensity: 0200000000000000\n",
            "  postCountByIntensity:\n" + "".join(f"  - {c}\n" for c in POST_COUNTS))
   .replace("__POST_INNER__", _num(POST_INNER)).replace("__POST_OUTER__", _num(POST_OUTER))
   .replace("__POST_CLAIM__", _num(POST_CLAIM_RADIUS))
   .replace("__POST_MARKER__", _num(POST_MARKER_RADIUS)))
emit("Assets/_SO_Assets/Games/TollwaySettings.asset.meta",
     asset_meta(G_ASSET["TollwaySettings"]))


# ── 4. Cell config (forked from Scramble's for its LADDER, nothing else) ─────
CELL_DESC = (
    "Ring-court cell for Tollway. Identical arena to Scarab Scramble - the nucleus IS the sphere "
    "court (play geometry, not a claim; the controller clears NucleusIsControlZone), the same "
    "spawn profile and the same three-species cleanup crew waiting outside the court until the "
    "volume ladder leaves Calm - and forked from it for exactly ONE reason: the LADDER. In "
    "Scramble a switch dais is a rare event; here a TOLL IS A DAIS, so a match raises three to "
    "five times the mass and Scramble's gates (Restless 164,000 / Frenzy 391,000) would both be "
    "crossed before the race was half run, after which the ladder conveys nothing. Restated in "
    "the currency this mode runs on: the trail band plus 6 monuments for Restless and 16 for "
    "Frenzy, at 50,773 volume and 255 prisms each (SCARAB.md 5.1). ESTIMATE pending the "
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
  SpawnProfile: {{fileID: 11400000, guid: {EXISTING['ScarabScrambleSpawnProfile']}, type: 2}}
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
  Description: The court is studded with toll posts. Fly to one, plant your ring in
    it, then drive a ball across the court and through the mouth - and every ball that
    threads it, yours or theirs or a stray off the wall, pays YOU and raises a monument
    on the spot, so the arena gets built out of the scoring. Rings are spent when they
    pay, so keep planting. Scarab only. First team to {TOLL_TARGET} tolls.
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
     toast(74, "Plant a ring - ANY ball through it pays YOU", idle=1, idle_seconds=25) +
     # Not an idle hint: this one fires ON A REFUSED PRESS, which is the moment the rule needs
     # stating. Before it, a press with no post on the line wrote one verbose log line and
     # nothing else - indistinguishable on screen from a dead button.
     toast(75, "No toll post on this line - fly at one and plant"))
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
if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 45\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)", r"\g<1>  - 45\n", prog, count=1)
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
# the spawn profile and the fauna species are REUSED, not forked - if the clone lost the profile
# the arena would have no cleanup crew at all and the ladder would describe nothing.
if EXISTING["ScarabScrambleSpawnProfile"] not in files[
        "Assets/_SO_Assets/Cell Configs/Tollway Cell/Tollway Cell Config.asset"]:
    errors.append("the Tollway cell config does not reuse the Scramble spawn profile")
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

# ── TOLL POSTS: the layout must actually be a layout ─────────────────────────
# The socket book is the rule the mode turns on (TOLLWAY.md "Toll posts"), and two of its
# properties are arithmetic rather than taste, so they are asserted here over the SHIPPED numbers
# rather than eyeballed in the editor:
#   * no two posts may be closer than the CLAIM RADIUS. Closer than that and one requested centre
#     sits inside two sockets at once, which makes TryResolve's "nearest free" a coin-flip near
#     the midpoint and lets one planted ring shadow its neighbour's aim.
#   * no post may sit in the MIDDLE. Sockets have to be places you FLY TO; one near the centre is
#     claimed incidentally by anyone crossing the court, which is the effortless placement the
#     whole mechanism exists to remove, back by the front door. (Note the crystals are no help
#     here: the nucleus IS the court in this mode, so they respawn across the whole volume rather
#     than in a core - the band's inner edge is the only thing holding the middle open.)
# This is a port of TollwayTollPosts.ComputeLayout, deliberately re-derived rather than imported:
# if the C# changes and this does not, the numbers disagree and the check fails, which is the
# point of writing it twice.
def _post_layout(seed, count, court_radius, inner, outer):
    m32 = 0xFFFFFFFF
    state = ((seed & m32) * 2654435761) & m32
    state ^= 0x9E3779B9
    state &= m32
    if state == 0:
        state = 0x6D2B79F5

    def nxt():
        nonlocal state
        state ^= (state << 13) & m32; state &= m32
        state ^= state >> 17
        state ^= (state << 5) & m32; state &= m32
        return (state >> 8) * (1.0 / 16777216.0)

    # Quaternion.Euler is ZXY; only the ROTATION matters here and a rotation preserves distances,
    # so the separation measured below is independent of it - it is applied for the radial band's
    # sake and for parity with the shipped walk.
    ax, ay, az = math.radians(nxt() * 360), math.radians(nxt() * 360), math.radians(nxt() * 360)
    cx, sx = math.cos(ax), math.sin(ax)
    cy, sy = math.cos(ay), math.sin(ay)
    cz, sz = math.cos(az), math.sin(az)

    def rot(v):
        x, y, z = v
        x, y = x * cz - y * sz, x * sz + y * cz          # Z
        y, z = y * cx - z * sx, y * sx + z * cx          # X
        x, z = x * cy + z * sy, -x * sy + z * cy         # Y
        return (x, y, z)

    golden = 2.399963229728653
    out = []
    for i in range(count):
        k = (i + 0.5) / count
        z = 1.0 - 2.0 * k
        r = math.sqrt(max(0.0, 1.0 - z * z))
        phi = i * golden
        d = rot((r * math.cos(phi), r * math.sin(phi), z))
        rad = (inner + (outer - inner) * nxt()) * court_radius
        out.append((d[0] * rad, d[1] * rad, d[2] * rad))
    return out


_worst_pair = None
_worst_core = None
for _count, _radius in zip(POST_COUNTS, COURT_RADII):
    for _seed in range(1, 401):
        _pts = _post_layout(_seed * 2 + 1, _count, _radius, POST_INNER, POST_OUTER)
        for _i in range(_count):
            _core = math.dist(_pts[_i], (0.0, 0.0, 0.0))
            if _worst_core is None or _core < _worst_core[0]:
                _worst_core = (_core, _count, _seed)
            for _j in range(_i + 1, _count):
                _d = math.dist(_pts[_i], _pts[_j])
                if _worst_pair is None or _d < _worst_pair[0]:
                    _worst_pair = (_d, _count, _seed)
if _worst_pair[0] <= POST_CLAIM_RADIUS:
    errors.append(f"toll posts can land {_worst_pair[0]:.1f}u apart (count {_worst_pair[1]}, seed "
                  f"{_worst_pair[2]}), inside the {POST_CLAIM_RADIUS:.0f}u claim radius - one "
                  f"requested centre would sit in two sockets at once")
# "You have to fly to it" in numbers: a post must sit further out than a pilot crossing the middle
# could claim without meaning to, i.e. beyond one claim radius plus one mouth. The shipped band
# clears this by ~2x; the check exists so a future narrowing of postInnerCourtFraction is loud.
_core_floor = POST_CLAIM_RADIUS + POST_MARKER_RADIUS
if _worst_core[0] <= _core_floor:
    errors.append(f"a toll post can land {_worst_core[0]:.1f}u from the court centre (count "
                  f"{_worst_core[1]}, seed {_worst_core[2]}), inside the {_core_floor:.0f}u a "
                  f"pilot crossing the middle would claim incidentally")

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
                    ("TollwayNoPost", 75)):
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
