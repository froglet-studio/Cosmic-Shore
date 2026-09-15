#!/usr/bin/env python3
"""
Authors every serialized asset the Wrecking Ball game mode needs (GameModes.WreckingBall = 54).

Wrecking Ball is the Scarab-only DEMOLITION RACE - Rampage's analog for the hull whose weapons
are a BALL and a PLATE. A sphere court (Scarab Scramble's arena: the nucleus resized, play
geometry rather than a claim) is grown full of Rampage's five breakable flora; every bright
crystal a Scarab flies through becomes its ball, every hostile prism that ball plows through or
the cavitation plate shreds is credited to the pilot, and the first DOMAIN to the prism target
wins. See Assets/_Scripts/Controller/Arcade/WRECKING_BALL.md.

WHAT IS FORKED AND WHY. The cell config, for the LADDER and the COURT: Scramble's ladder is
authored for Scarab trail volume and would be crossed at both gates the moment the forest grew,
and Rampage's cells put the forest OUTSIDE the nucleus, which is exactly where a ball dies
(SCARAB.md 4.1c - a ball outside its nucleus bleeds speed six times as fast). The five flora
species, for their PLANTING BAND: Rampage plants at 0.1-0.97 of the membrane; this court is 720u
of a 1200u membrane, so the same species are re-cut into 0.28-0.92 of the court with their
relative depths preserved. Everything else about a species - budget, breeding, element palette,
prefab - is Rampage's verbatim. The spawn profiles carry Rampage's fauna pair at Rampage's
per-intensity scale.

WHAT IS REFERENCED. The ball, the plate, the forge and their tuning belong to the VESSEL. The
scene is a clone of Scramble's (Scarab AI templates, the spawn ring, the crystal manager, the cell
network sync), with the mode identity, the cell list and the crystal economy swapped.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_wrecking_ball_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 54
g = lib.Generator(MODE_ID, "WreckingBall")
guid = lib.guid

# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
G_SCRIPT = {
    "WreckingBallController":       guid("script/WreckingBallController"),
    "WreckingBallSettingsSO":       guid("script/WreckingBallSettingsSO"),
    "WreckingBallScoringRuleSO":    guid("script/WreckingBallScoringRuleSO"),
    "WreckingBallPrismTurnMonitor": guid("script/WreckingBallPrismTurnMonitor"),
}
SCRIPT_PATHS = {
    "WreckingBallController":       "Assets/_Scripts/Controller/Arcade/WreckingBall/WreckingBallController.cs",
    "WreckingBallSettingsSO":       "Assets/_Scripts/Controller/Arcade/WreckingBall/WreckingBallSettingsSO.cs",
    "WreckingBallScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/WreckingBall/WreckingBallScoringRuleSO.cs",
    "WreckingBallPrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/WreckingBallPrismTurnMonitor.cs",
}

# ── The five species, Rampage's ───────────────────────────────────────────────
SPECIES = ["Cacti", "Spire", "Pine", "Rosette", "Coral"]
RAMPAGE_DIR = "Assets/_SO_Assets/Cell Configs/Rampage Cell"
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Wrecking Ball Cell"

# ── New asset GUIDs ──────────────────────────────────────────────────────────
G_ASSET = {
    "ArcadeGameWreckingBall":     guid("asset/ArcadeGameWreckingBall"),
    "WreckingBallScoringRule":    guid("asset/WreckingBallScoringRule"),
    "WreckingBallSettings":       guid("asset/WreckingBallSettings"),
    "GameToastConfigWreckingBall": guid("asset/GameToastConfigWreckingBall"),
    "ModePreviewWreckingBall":    guid("asset/ModePreviewWreckingBall"),
    "MinigameWreckingBall.unity": guid("asset/MinigameWreckingBall.unity"),
}
for _sp in SPECIES:
    G_ASSET[f"Flora{_sp}"] = guid(f"asset/WreckingBall Flora {_sp}")
for _i in range(1, 5):
    G_ASSET[f"CellConfig{_i}"] = guid(f"asset/WreckingBall Cell Config {_i}")
    G_ASSET[f"SpawnProfile{_i}"] = guid(f"asset/WreckingBall Spawn Profile {_i}")

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CELL_VISUALS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Scarab"] = lib.VESSELS["Scarab"]
# the donor's mode-specific wiring, swapped out of the cloned scene
EXISTING["ScarabScrambleController"] = lib.existing_guid(
    "Assets/_Scripts/Controller/Arcade/ScarabScramble/ScarabScrambleController.cs")
EXISTING["ScarabScrambleGoalTurnMonitor"] = lib.existing_guid(
    "Assets/_Scripts/Controller/Arcade/TurnMonitors/ScarabScrambleGoalTurnMonitor.cs")
EXISTING["ScarabScrambleScoringRule"] = lib.existing_guid(
    "Assets/_SO_Assets/Scoring Rules/ScarabScrambleScoringRule.asset")
EXISTING["ScarabScrambleSettings"] = lib.existing_guid(
    "Assets/_SO_Assets/Games/ScarabScrambleSettings.asset")
EXISTING["ScarabScrambleCellConfig"] = lib.existing_guid(
    "Assets/_SO_Assets/Cell Configs/Scarab Scramble Cell/Scarab Scramble Cell Config.asset")
# Rampage's species and fauna, carried across
for _sp in SPECIES:
    EXISTING[f"Rampage{_sp}Flora"] = lib.existing_guid(f"{RAMPAGE_DIR}/Rampage {_sp} Flora Config Data.asset")
EXISTING["BlobTadpoleFauna"] = lib.existing_guid(
    "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Tadpole Fauna Config Data.asset")
EXISTING["BlobSharkFauna"] = lib.existing_guid(
    "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset")

# ── The race ─────────────────────────────────────────────────────────────────
# Hostile prisms a domain must destroy. Rampage races to 2000 in a forest of 59-295 plants;
# this court holds 35-59, so the target is lower or a match would end on bare walls. Kept in
# sync with EndConditionOverridesSO.DefaultWreckingBallPrismTarget.
PRISM_TARGET = 1500

# Comeback strength - a FUNCTION OF THE TARGET (`bonusLevels = deficit x rate`): a quarter of
# the race behind (375 prisms) buys 2.25 element levels. The assert below is the gate this
# family keeps re-learning.
COMEBACK_RATE = 0.006

# ── The court, and the forest inside it ──────────────────────────────────────
# ONE radius at every intensity (WreckingBallSettingsSO.courtRadiusByIntensity says why): the
# forest is authored in MEMBRANE fractions against this court, and a court that grew would leave
# it outside the wall where a ball's drag ramp kills it.
COURT_RADIUS = 720.0
COURT_INNER, COURT_OUTER = 0.28, 0.92   # of the court: off the middle where balls are forged, off the wall

# Rampage's species author their own bands as membrane fractions spanning 0.1..0.97 between
# them; each is mapped linearly into the court band so the LAYERING (coral in, spire out)
# survives the move.
RAMPAGE_BAND_MIN, RAMPAGE_BAND_MAX = 0.1, 0.97


def court_band(orig_min: float, orig_max: float):
    def remap(x):
        t = (x - RAMPAGE_BAND_MIN) / (RAMPAGE_BAND_MAX - RAMPAGE_BAND_MIN)
        court_frac = COURT_INNER + t * (COURT_OUTER - COURT_INNER)
        return round(court_frac * COURT_RADIUS / lib.MEMBRANE_RADIUS, 4)
    return remap(orig_min), remap(orig_max)


# ── Intensity: DENSITY and SUPPLY ────────────────────────────────────────────
# Intensity 4 is a sparse court and a scarce ball; 1 is a full one with balls for everyone.
# The plant scale is flatter than Rampage's 5x/3.67x/2.33x/1x because the court band holds a
# fraction of Rampage's shell volume (4/3 pi (648^3 - 202^3) ~ 1.1e9 against ~6e9): Rampage's
# 295 plants in this court would be a wall a ball cannot enter. Prism SIZE is 1x at every level
# (a bigger prism is a bigger drag on the ball, which is the wrong direction for "easier").
FLORA_SCALE = [1.0, 0.85, 0.72, 0.6]
FAUNA_SCALE = [1.0, 2.0, 3.0, 4.0]          # Rampage's wildlife ladder, verbatim
CRYSTALS_BY_INTENSITY = [(2, 0), (1, 1), (1, 0), (1, -1)]   # (per player, extra): 4 pilots -> 8/5/4/3

# ── The volume ladder ────────────────────────────────────────────────────────
# Rampage's measured intensity-4 forest (59 plants, 1x leaves) is 396,178 volume and its
# play-tested ladder sits Restless at 0.285x and Frenzy at 4.11x of it (RAMPAGE.md); this cell's
# forest is that forest times FLORA_SCALE, so each intensity's ladder is Rampage's scaled by its
# own forest ratio - the same rule Rampage's own four cells follow. Counts are Rampage's
# backstops. ESTIMATE pending the in-editor baseline measure.
RAMPAGE_FOREST_VOLUME = 396_178
RESTLESS_ENTER_RATIO, RESTLESS_EXIT_RATIO = 113_000 / RAMPAGE_FOREST_VOLUME, 81_000 / RAMPAGE_FOREST_VOLUME
FRENZY_ENTER_RATIO, FRENZY_EXIT_RATIO = 1_630_000 / RAMPAGE_FOREST_VOLUME, 1_260_000 / RAMPAGE_FOREST_VOLUME


def ladder(scale: float):
    v = RAMPAGE_FOREST_VOLUME * scale
    r = lambda x: int(round(x / 1000.0)) * 1000
    return dict(re=r(v * RESTLESS_ENTER_RATIO), rx=r(v * RESTLESS_EXIT_RATIO),
                fe=r(v * FRENZY_ENTER_RATIO), fx=r(v * FRENZY_EXIT_RATIO), forest=int(round(v)))


# ── 1. .cs.meta for the scripts, and the folders this mode adds ─────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/WreckingBall")
g.text_meta("Assets/_Scripts/Controller/Arcade/WRECKING_BALL.md")
g.folder_meta(CELL_DIR)

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
# metric 5 = ScoringMetric.PrismsDestroyed - Rampage's; golf-timed like every race here.
g.emit_asset("Assets/_SO_Assets/Scoring Rules/WreckingBallScoringRule.asset",
             G_ASSET["WreckingBallScoringRule"],
             lib.header_for(G_SCRIPT["WreckingBallScoringRuleSO"], "WreckingBallScoringRule") +
             "  metric: 5\n  golfRules: 1\n")

# ── 3. Mode settings ─────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/WreckingBallSettings.asset", G_ASSET["WreckingBallSettings"],
             lib.header_for(G_SCRIPT["WreckingBallSettingsSO"], "WreckingBallSettings") + f"""  courtRadiusByIntensity:
  - {lib.num(COURT_RADIUS)}
  - {lib.num(COURT_RADIUS)}
  - {lib.num(COURT_RADIUS)}
  - {lib.num(COURT_RADIUS)}
  leadChangeAnnounceFraction: 0.25
  progressSampleSeconds: 0.5
  aiRetargetSeconds: 1
  aiApproachLead: 45
  aiInterceptLeadSeconds: 0.5
  aiDashRange: 70
  aiDashSampleSeconds: 0.5
""")

# ── 4. The forest: Rampage's five species, re-cut into the court ────────────
FLORA_BANDS = {}
for _sp in SPECIES:
    src = g.read(f"{RAMPAGE_DIR}/Rampage {_sp} Flora Config Data.asset")
    mmax = re.search(r"^  PlantRadiusCellFractionMaxOverride: ([\d.]+)\n", src, re.M)
    mmin = re.search(r"^  PlantRadiusCellFractionMinOverride: ([\d.]+)\n", src, re.M)
    assert mmax and mmin, f"Rampage {_sp} authors no planting band"
    lo, hi = court_band(float(mmin.group(1)), float(mmax.group(1)))
    FLORA_BANDS[_sp] = (lo, hi)
    out = src.replace(mmax.group(0), f"  PlantRadiusCellFractionMaxOverride: {lib.num(hi)}\n", 1)
    out = out.replace(mmin.group(0), f"  PlantRadiusCellFractionMinOverride: {lib.num(lo)}\n", 1)
    out, n = re.subn(r"^  m_Name: .*$", f"  m_Name: WreckingBall {_sp} Flora", out, count=1, flags=re.M)
    assert n == 1
    # Anything else the donor authors comes through verbatim - budget, breeding, palette, prefab.
    assert "NetworkSynced" not in out, "Rampage species are per-peer; this fork must stay so too"
    g.emit_asset(f"{CELL_DIR}/WreckingBall {_sp} Flora.asset", G_ASSET[f"Flora{_sp}"], out)

# ── 5. Spawn profiles + cell configs, one per intensity ─────────────────────
FLORA_LIST = "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[f'Flora{_sp}']}, type: 2}}\n" for _sp in SPECIES)
LADDERS = [ladder(s) for s in FLORA_SCALE]
for _i in range(1, 5):
    s = FLORA_SCALE[_i - 1]
    lad = LADDERS[_i - 1]
    g.emit_asset(f"{CELL_DIR}/WreckingBall Spawn Profile {_i}.asset", G_ASSET[f"SpawnProfile{_i}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"WreckingBall Spawn Profile {_i}") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 12000
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  FloraPopulationScale: {lib.num(s)}
  FloraPlantBudgetScale: 1
  FloraPrismScale: 1
  SupportedFloras:
{FLORA_LIST}  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 10
  FaunaSpawnVolumeThreshold: 1
  FaunaPopulationScale: {lib.num(FAUNA_SCALE[_i - 1])}
  BaseFaunaSpawnTime: 30
  FaunaFoodFloor: 5
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 1
  SupportedFaunas:
  - {{fileID: 11400000, guid: {EXISTING['BlobTadpoleFauna']}, type: 2}}
  - {{fileID: 11400000, guid: {EXISTING['BlobSharkFauna']}, type: 2}}
""")
    desc = (f"Demolition court cell for Wrecking Ball, intensity {_i} of 4 (CellTypeChoiceOptions."
            f"IntensityWise, list order = intensity). The nucleus IS the {int(COURT_RADIUS)}u sphere "
            f"court (play geometry, not a claim - the controller clears NucleusIsControlZone, which is "
            f"also what lets the forest plant INSIDE it) and the forest is Rampage's five breakable "
            f"species re-cut into the court's {COURT_INNER:.2f}-{COURT_OUTER:.2f} band at {s:g}x "
            f"Rampage's plant count, with Rampage's wildlife at {FAUNA_SCALE[_i - 1]:g}x. Every ball "
            f"reflects off the court wall back through the stands, and crystals respawn anywhere in "
            f"it. The mature forest is ~{lad['forest']:,} volume; this cell's VOLUME thresholds are "
            f"Rampage's play-tested intensity-4 ladder scaled by that, so Frenzy sits 4.11x above "
            f"the forest at every level. ESTIMATE pending the in-editor baseline measure; regenerate "
            f"with Tools/Build/author_wrecking_ball_assets.py rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/WreckingBall Cell Config {_i}.asset", G_ASSET[f"CellConfig{_i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"WreckingBall Cell Config {_i}") + f"""  CellName: Wrecking Ball
  Description: {desc}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {_i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: 346633111830028674, guid: {EXISTING['MembranePrefab']}, type: 3}}
  NucleusPrefab: {{fileID: 7555898194514117247, guid: {EXISTING['NucleusPrefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: 639495419069806261, guid: {EXISTING['CytoplasmPrefab']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'SpawnProfile{_i}']}, type: 2}}
  PhaseThresholds:
    RestlessEnter: 700
    RestlessExit: 500
    FrenzyEnter: 10000
    FrenzyExit: 8000
    RestlessEnterVolume: {lad['re']}
    RestlessExitVolume: {lad['rx']}
    FrenzyEnterVolume: {lad['fe']}
    FrenzyExitVolume: {lad['fx']}
""")

# ── 6. Arcade game config ────────────────────────────────────────────────────
# SCARAB ONLY: a single entry in Vessels drives all three enforcement layers. Solo is legal
# (the target is the forest, as in Rampage); two domains minimum so "hostile" means something.
g.emit_asset("Assets/_SO_Assets/Games/ArcadeGameWreckingBall.asset", G_ASSET["ArcadeGameWreckingBall"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameWreckingBall") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Wrecking Ball
  Description: Scarabs only, in a walled court grown full of cacti and coral. Fly through
    a bright crystal and it becomes your ball - bowl it into the thickest stand and every
    prism it plows through is yours; flick a dash beside the forest and the cavitation
    plate shreds the rest. Balls bounce off the wall and come back for more. First team
    to the wrecking target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameWreckingBall
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Scarab']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 7. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_WreckingBall.asset",
             G_ASSET["GameToastConfigWreckingBall"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_WreckingBall") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has wrecked {1} prisms ({1}/{3})", tint_domain=1, domain_names=0, every_n=250) +
             lib.toast(92, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(93, "Fly through a bright crystal - it becomes YOUR wrecking ball", idle=1, idle_seconds=25) +
             lib.toast(94, "Flick the right stick beside the forest - the plate shreds whatever it sweeps", idle=1, idle_seconds=45) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigWreckingBall"])

# ── 8. Mode preview ──────────────────────────────────────────────────────────
PREVIEW_CELLS = "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[f'CellConfig{i}']}, type: 2}}\n" for i in range(1, 5))
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_WreckingBall.asset", G_ASSET["ModePreviewWreckingBall"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_WreckingBall") + f"""  Mode: {MODE_ID}
  Notes: 'Every intensity is a different forest (plant count) in the same court, so the
    scale model rebuilds when the intensity row moves. The court sphere, the ball forge and
    the juke dash all work in the flight preview; bowling a ball through the stands is the
    whole mode and previews as itself.'
  PreviewCell: {{fileID: 11400000, guid: {G_ASSET['CellConfig1']}, type: 2}}
  PreviewCellsByIntensity:
{PREVIEW_CELLS}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: 12
  ObjectiveText: Bowl your ball through the forest
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 760
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewWreckingBall"])

# ── 9. Scene: clone MinigameScarabScramble, swap the mode-specific wiring ────
# The donor already IS the court arena (Scarab AI templates, the nucleus-as-court cell, the spawn
# ring, the crystal manager, the cell network sync). The controller's serialized field block is
# carried over UNCHANGED because WreckingBallController names its fields exactly as Scramble's
# does (settings / rule / arenaCell / cellData) - the swap is four guids, the cell list and the
# crystal economy.
scene = g.read(f"{lib.SCENES_DIR}/MinigameScarabScramble.unity")
for donor_key, new_guid, label in (
    ("ScarabScrambleController", G_SCRIPT["WreckingBallController"], "controller"),
    ("ScarabScrambleGoalTurnMonitor", G_SCRIPT["WreckingBallPrismTurnMonitor"], "turn monitor"),
    ("ScarabScrambleScoringRule", G_ASSET["WreckingBallScoringRule"], "scoring rule"),
    ("ScarabScrambleSettings", G_ASSET["WreckingBallSettings"], "settings"),
):
    scene = lib.swap_guid(scene, EXISTING[donor_key], new_guid, label)

# THE CELL becomes INTENSITY-WISE (list order = intensity, never sorted).
scene = lib.replace_block(scene,
    f"  CellConfigs:\n  - {{fileID: 11400000, guid: {EXISTING['ScarabScrambleCellConfig']}, type: 2}}\n  cellTypeChoiceOptions: 0\n",
    "  CellConfigs:\n" + "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[f'CellConfig{i}']}, type: 2}}\n" for i in range(1, 5))
    + "  cellTypeChoiceOptions: 1\n",
    "cell config")

# THE CRYSTAL ECONOMY: Scramble runs PlayerCountPlusExtra +2; here crystals are BALLS and the
# ball is the demolition tool, so the count is the supply half of the intensity axis.
scene = lib.replace_block(scene,
    "  crystalCountMode: 1\n  fixedCrystalCount: 1\n  extraCrystalsToSpawnBeyondPlayerCount: 2\n  crystalCountByIntensity: []\n",
    "  crystalCountMode: 2\n  fixedCrystalCount: 1\n  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
    + "".join(f"  - CrystalsPerPlayer: {p}\n    ExtraCrystals: {e}\n" for p, e in CRYSTALS_BY_INTENSITY),
    "crystal count")
g.emit_scene("MinigameWreckingBall", G_ASSET["MinigameWreckingBall.unity"], scene)

# ── 10-13. The shared registries ─────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameWreckingBall"])
g.register_always_unlocked()
g.register_build_scene("MinigameScarabScramble", "MinigameWreckingBall", G_ASSET["MinigameWreckingBall.unity"])
g.set_end_condition("wreckingBallPrismTarget", after="tollwayTollTarget", value=PRISM_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = []
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The forest must sit INSIDE the court, off the wall and off the middle, at every species.
for _sp, (lo, hi) in FLORA_BANDS.items():
    lo_u, hi_u = lo * lib.MEMBRANE_RADIUS, hi * lib.MEMBRANE_RADIUS
    if not (COURT_INNER * COURT_RADIUS - 1 <= lo_u < hi_u <= COURT_OUTER * COURT_RADIUS + 1):
        errors.append(f"{_sp} band {lo_u:.0f}-{hi_u:.0f}u is not inside the court band "
                      f"{COURT_INNER * COURT_RADIUS:.0f}-{COURT_OUTER * COURT_RADIUS:.0f}u")

# The ladder must be MONOTONE in intensity and Frenzy must clear the forest with room for the
# Scarab's own mass (a switch dais is 50,773 volume, SCARAB.md 5.1): at least four of them.
for i, lad in enumerate(LADDERS, start=1):
    if lad["fe"] - lad["forest"] < 4 * 50_773:
        errors.append(f"intensity {i}: Frenzy ({lad['fe']}) leaves under four daises above the forest ({lad['forest']})")
    if lad["re"] >= lad["fe"] or lad["rx"] >= lad["re"] or lad["fx"] >= lad["fe"]:
        errors.append(f"intensity {i}: ladder is not ordered")
for i in range(1, 4):
    if LADDERS[i]["fe"] > LADDERS[i - 1]["fe"]:
        errors.append("ladder is not monotone: a sparser court has a higher Frenzy gate")

# The scene must no longer mention the donor's mode-specific guids, and must mention ours.
sc = g.files[f"{lib.SCENES_DIR}/MinigameWreckingBall.unity"]
for name in ("ScarabScrambleController", "ScarabScrambleGoalTurnMonitor", "ScarabScrambleScoringRule",
             "ScarabScrambleSettings", "ScarabScrambleCellConfig"):
    if EXISTING[name] in sc:
        errors.append(f"cloned scene still references {name}")
for name in ("WreckingBallController", "WreckingBallPrismTurnMonitor"):
    if G_SCRIPT[name] not in sc:
        errors.append(f"cloned scene missing {name}")
if sc.count("  - vesselClass: 12\n") != 4:
    errors.append("cloned scene does not carry 4 Scarab AI templates")
if sc.count("  cellTypeChoiceOptions: 1\n") != 1:
    errors.append("cloned scene is not IntensityWise")

# Every intensity's profile must carry all five species and both fauna.
for i in range(1, 5):
    prof = g.files[f"{CELL_DIR}/WreckingBall Spawn Profile {i}.asset"]
    for _sp in SPECIES:
        if G_ASSET[f"Flora{_sp}"] not in prof:
            errors.append(f"profile {i} lost {_sp}")
    for f in ("BlobTadpoleFauna", "BlobSharkFauna"):
        if EXISTING[f] not in prof:
            errors.append(f"profile {i} lost {f}")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/WreckingBall"), guid("text/Assets/_Scripts/Controller/Arcade/WRECKING_BALL.md"), guid("folder/" + CELL_DIR)])
