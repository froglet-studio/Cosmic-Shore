#!/usr/bin/env python3
"""
Authors every serialized asset the Starweave game mode needs (GameModes.Starweave = 92, row 2 of
Docs/ElementalLoop/GAME_KIT_ROSTER.md, whose reserved ids it takes).

Starweave is the Brittle Star's MASS game: a timed weaving war in a bare nucleus cell. Creation Mode (B) lays a trail
behind each of the ring's five corners at half cruise, and MASS fattens every block (BRITTLE_STAR.md 3.2). The score is
the volume a domain has standing in its colour at the whistle (VolumeRemaining, a live stock kept by StatsManager for
every mode), so the slowest mode is the one that scores, and every rival's guns, cords and ring are aimed at it.

THE LADDER (the crystal scatter is the dial, Tapestry's recipe; the card's StartingElements add reach on I3-I4):
  I1 Loom       - 24 elemental crystals: fat weaves early, room to learn Creation Mode.
  I2 Weave      - 16 crystals.
  I3 Crossfire  - 10 crystals, and every hull starts at Space 3: the guns reach further, a weave must be guarded.
  I4 Siege      - 6 crystals, every hull at Space 5.

NOTHING ABOUT THE BRITTLE STAR CHANGES (the LOCKED vessel rule). The AI only steers: the hull's own input-only autopilot
picks Creation when nothing rival lies ahead and its guns or cords when rival mass does (BRITTLE_STAR.md 6).

THE COMEBACK RATE IS SIZED AGAINST A VOLUME, read off the shipped hull (BrittleStar.prefab's BaseScale and speed,
BrittleStarConfig's creationSpacing and creationSpeedMultiplier), so a vessel retune moves it or fails the assert.

The scene is cloned from Glasshouse's (a timed monitor, a controller declaring settings / rule / arenaCell, a nucleus
cell), with its identity, monitor, cells and AI hulls swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_starweave_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 92                 # GAME_KIT_ROSTER.md row 2
TOAST_HINT = 167             # GAME_KIT_ROSTER.md row 2
TOAST_LEAD = 130             # DomainRaceLeadChanged, posted by the controller's own lead sampler
ROUND_SECONDS = 90           # Docs/ARCADE_MATCH_LENGTH.md; EndConditionOverridesSO.DefaultStarweaveRoundSeconds
g = lib.Generator(MODE_ID, "Starweave")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "StarweaveController":      f"{AR}/Starweave/StarweaveController.cs",
    "StarweaveSettingsSO":      f"{AR}/Starweave/StarweaveSettingsSO.cs",
    "StarweaveScoringRuleSO":   f"{AR}/Starweave/StarweaveScoringRuleSO.cs",
    "StarweaveTimeTurnMonitor": f"{AR}/TurnMonitors/StarweaveTimeTurnMonitor.cs",
    "StarweaveScoringTests":    "Assets/_Scripts/Tests/Editor/StarweaveScoringTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Starweave Cell"
G_ASSET = {
    "ArcadeGameStarweave":      guid("asset/ArcadeGameStarweave"),
    "StarweaveScoringRule":     guid("asset/StarweaveScoringRule"),
    "StarweaveSettings":        guid("asset/StarweaveSettings"),
    "GameToastConfigStarweave": guid("asset/GameToastConfigStarweave"),
    "ModePreviewStarweave":     guid("asset/ModePreviewStarweave"),
    "MinigameStarweave.unity":  guid("asset/MinigameStarweave.unity"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"StarweaveCellConfig{i}"] = guid(f"asset/StarweaveCellConfig{i}")
    G_ASSET[f"StarweaveSpawnProfile{i}"] = guid(f"asset/StarweaveSpawnProfile{i}")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_BrittleStar"] = lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_BrittleStar.asset")
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell
EXISTING["Nucleus_prefab"] = "b9cf1833fa2493d4b8724ccb6740fb3a"          # Barren's nucleus (~392 u radius)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["Cytoplasm_prefab"] if "Cytoplasm_prefab" in lib.CELL_VISUALS \
    else lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
BRITTLESTAR_ID = lib.VESSEL_CLASS_ID["BrittleStar"]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
HULL_PREFAB = "Assets/_Prefabs/Spacevessels/BrittleStar.prefab"
HULL_CONFIG = "Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset"
EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarExecutor.cs"

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameGlasshouse.unity"
DONOR = {
    "GlasshouseController":      guid("script/GlasshouseController"),
    "GlasshouseTimeTurnMonitor": guid("script/GlasshouseTimeTurnMonitor"),
    "GlasshouseSettings":        guid("asset/GlasshouseSettings"),
    "GlasshouseScoringRule":     guid("asset/GlasshouseScoringRule"),
}
DONOR_CELLS = [guid(f"asset/GlasshouseCellConfig{i}") for i in (1, 2, 3, 4)]

MEMBRANE_FILEID = 346633111830028674
NUCLEUS_FILEID = 7555898194514117247
CYTOPLASM_FILEID = 639495419069806261
MEMBRANE_RADIUS = 1200.0
NUCLEUS_RADIUS = 392.0
SPAWN_RING = 450
PILOTS = 4

RUNGS = [
    dict(i=1, label="Loom", crystals=24, space=0.0, omni=4,
         blurb="24 elemental crystals - fat weaves early, room to learn Creation Mode"),
    dict(i=2, label="Weave", crystals=16, space=0.0, omni=3,
         blurb="16 crystals, and every rival's guns on what you lay"),
    dict(i=3, label="Crossfire", crystals=10, space=0.3, omni=3,
         blurb="10 crystals and every hull at Space 3 - the guns reach further, a weave must be guarded"),
    dict(i=4, label="Siege", crystals=6, space=0.5, omni=2,
         blurb="6 crystals and every hull at Space 5 - the longest guns against the thinnest weaves"),
]

# ── The weave model: what one weaving pilot adds per second, read off the shipped hull ──
PREFAB = g.read(HULL_PREFAB)
CONFIG = g.read(HULL_CONFIG)


def one(src, pattern, what):
    m = re.search(pattern, src, re.M)
    assert m, f"{what} not found"
    return float(m.group(1))


BASE = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", PREFAB, re.M)
assert BASE, "BrittleStar.prefab BaseScale not found"
BLOCK_VOLUME = 1.0
for v in BASE.groups():
    BLOCK_VOLUME *= float(v)
CRUISE = one(PREFAB, r"^\s*DefaultMinimumSpeed:\s*([0-9.]+)$", "DefaultMinimumSpeed") \
    + one(PREFAB, r"^\s*DefaultThrottleScaler:\s*([0-9.]+)$", "DefaultThrottleScaler")
SPACING = one(CONFIG, r"^  creationSpacing: ([0-9.]+)$", "creationSpacing")
CREATION_SPEED = one(CONFIG, r"^  creationSpeedMultiplier: ([0-9.]+)$", "creationSpeedMultiplier")
CORNERS = 5
BLOCKS_PER_SECOND = CORNERS * CRUISE * CREATION_SPEED / SPACING
WEAVE_RATE = BLOCKS_PER_SECOND * BLOCK_VOLUME                     # volume / second, one weaving pilot at Mass 0
# Two pilots weaving half the round (the other half raiding or fetching) - the reference a comeback is measured against.
REFERENCE_DOMAIN_VOLUME = 2 * 0.5 * ROUND_SECONDS * WEAVE_RATE
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)


def ladder():
    """PhaseThresholds: the cell grows nothing, so Frenzy sits 25 % above the most four pilots can weave in a round at
    Mass 10 (every block x2 on x and y) and never fires; Restless at 7 % of it."""
    count = PILOTS * ROUND_SECONDS * BLOCKS_PER_SECOND
    volume = count * BLOCK_VOLUME * 4
    fe, fev = round(count * 1.25), round(volume * 1.25)
    return dict(RestlessEnter=round(fe * 0.07), RestlessExit=round(fe * 0.05), FrenzyEnter=fe,
                FrenzyExit=round(fe * 0.85), RestlessEnterVolume=round(fev * 0.07),
                RestlessExitVolume=round(fev * 0.05), FrenzyEnterVolume=fev, FrenzyExitVolume=round(fev * 0.85),
                maxCount=round(count), maxVolume=round(volume))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameStarweave.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Starweave")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/STARWEAVE.md")

# ── 2. Scoring rule (timed, highest wins, VolumeRemaining = 12) ──────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/StarweaveScoringRule.asset", G_ASSET["StarweaveScoringRule"],
             lib.header_for(G_SCRIPT["StarweaveScoringRuleSO"], "StarweaveScoringRule") +
             """  metric: 12
  golfRules: 0
  volumeTemplate: '{volume} Volume'
  prismsTemplate: '{count} prisms standing'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME WOVEN
""")

# ── 3. Settings (AI ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRaidDeficitFraction": ([1, 0.25, 0.1, 0.05], "down"),
    "aiLateRaidSeconds":     ([0, 15, 25, 35], "up"),
    "aiCrystalReach":        ([250, 400, 550, 750], "up"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/StarweaveSettings.asset", G_ASSET["StarweaveSettings"],
             lib.header_for(G_SCRIPT["StarweaveSettingsSO"], "StarweaveSettings")
             + "  elementalCrystalCountByIntensity:\n" + "".join(f"  - {r['crystals']}\n" for r in RUNGS)
             + "  crystalScatterRadius: 900\n  crystalScatterSeed: 92\n"
             + "  leadAnnounceAfterSeconds: 20\n  leadAnnounceMinGapSeconds: 10\n  progressSampleSeconds: 1\n"
             + "  aiDecisionSeconds: 6\n"
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "  aiWeaveOrbitFraction: 0.55\n  aiWeaveOrbitDegreesPerSecond: 4\n")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2: a weaving war with one side is a screensaver (Tapestry's reason).
DESCRIPTION = ("Brittle Stars only - a timed weaving war. Creation Mode lays a trail from every corner of your ring, "
               "five at once, at half speed. Everything you leave standing is score, and every rival's guns, cords and "
               "ring are aimed at it. Mass crystals fatten your weave; Space crystals lengthen your guns for a raid. "
               "The team with the most volume standing when the clock runs out wins. Higher intensities scatter fewer "
               "crystals and start every hull with longer guns.")
STARTING = "".join(f"""  - Class: {BRITTLESTAR_ID}
    Intensity: {r['i']}
    Levels:
      Mass: 0
      Charge: 0
      Space: {lib.num(r['space'])}
      Time: 0
""" for r in RUNGS if r["space"] > 0)
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameStarweave"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameStarweave") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Starweave
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameStarweave
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_BrittleStar']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  StartingElements:
{STARTING}  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Starweave.asset", G_ASSET["GameToastConfigStarweave"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Starweave") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Press B for Creation Mode - five trails at once, and everything standing at the "
                       "whistle is score", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigStarweave"])

# ── 6. The arena: a bare nucleus cell per rung (no flora, no fauna: Tapestry's reasoning) ──
TH = ladder()
for r in RUNGS:
    i = r["i"]
    g.emit_asset(f"{CELL_DIR}/Starweave Spawn Profile {i}.asset", G_ASSET[f"StarweaveSpawnProfile{i}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"Starweave Spawn Profile {i}") + """  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  FloraPopulationScale: 1
  FloraPlantBudgetScale: 1
  FloraPrismScale: 1
  SupportedFloras: []
  FaunaExcludeLocalDomain: 1
  InitialFaunaSpawnWaitTime: 6
  InitialFaunaReleaseTier: 0
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 10
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 1
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas: []
""")
    desc = (f"{r['label']} - intensity {i} of Starweave: {r['blurb']}. The standard {int(MEMBRANE_RADIUS)} u cell with "
            f"its nucleus (~{int(NUCLEUS_RADIUS)} u), no environment, no flora and no fauna - the arena is what the "
            f"Brittle Stars weave into it. StarweaveController scatters the elemental crystals; {r['omni']} omni crystals "
            f"respawn inside the nucleus (the scene's CrystalManager). Frenzy sits 25% above the most four pilots can "
            f"weave ({TH['maxCount']:,} blocks, {TH['maxVolume']:,} volume). Authored by "
            f"Tools/Build/author_starweave_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Starweave Cell Config {i}.asset", G_ASSET[f"StarweaveCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Starweave Cell Config {i}") + f"""  CellName: Starweave
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS_FILEID}, guid: {EXISTING['Nucleus_prefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'StarweaveSpawnProfile{i}']}, type: 2}}
  EnvironmentPrefab: {{fileID: 0}}
  EnvironmentIntensity: {i}
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {TH['RestlessEnter']}
    RestlessExit: {TH['RestlessExit']}
    FrenzyEnter: {TH['FrenzyEnter']}
    FrenzyExit: {TH['FrenzyExit']}
    RestlessEnterVolume: {TH['RestlessEnterVolume']}
    RestlessExitVolume: {TH['RestlessExitVolume']}
    FrenzyEnterVolume: {TH['FrenzyEnterVolume']}
    FrenzyExitVolume: {TH['FrenzyExitVolume']}
""")

CELLS = [G_ASSET[f"StarweaveCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {r['omni']}\n" for r in RUNGS))
MONITOR_DONOR = (f"  m_Script: {{fileID: 11500000, guid: {DONOR['GlasshouseTimeTurnMonitor']}, type: 3}}\n"
                 "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
                 "  duration: 90\n")
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['StarweaveTimeTurnMonitor']}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
           f"  duration: {ROUND_SECONDS}\n")
CONTROLLER_DONOR_TAIL = (f"  settings: {{fileID: 11400000, guid: {DONOR['GlasshouseSettings']}, type: 2}}\n"
                         f"  rule: {{fileID: 11400000, guid: {DONOR['GlasshouseScoringRule']}, type: 2}}\n"
                         "  arenaCell: {fileID: 1700000065}\n")
CONTROLLER_TAIL = (f"  settings: {{fileID: 11400000, guid: {G_ASSET['StarweaveSettings']}, type: 2}}\n"
                   f"  rule: {{fileID: 11400000, guid: {G_ASSET['StarweaveScoringRule']}, type: 2}}\n"
                   "  arenaCell: {fileID: 1700000065}\n")
AI_DONOR = "".join(f"  - vesselClass: {ANGLER_ID}\n" for _ in range(1))
AI = f"  - vesselClass: {BRITTLESTAR_ID}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Starweave.asset", G_ASSET["ModePreviewStarweave"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Starweave") + f"""  Mode: {MODE_ID}
  Notes: 'The bare nucleus cell at each intensity, and nothing else - the arena IS what the Brittle Stars weave into
    it. What the preview teaches is Creation Mode''s five trails. Vessel pinned to BrittleStar({BRITTLESTAR_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {BRITTLESTAR_ID}
  ObjectiveText: Press B for Creation Mode and weave - everything standing at the whistle is score
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {SPAWN_RING}\n" for _ in RUNGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewStarweave"])

# ── 8. Scene: Glasshouse's, with the identity, the monitor, the cells, the crystals and the AI hulls swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.replace_block(scene, CONTROLLER_DONOR_TAIL, CONTROLLER_TAIL, "controller fields")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["StarweaveController"], "controller")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    n = scene.count(AI_DONOR)
    assert n == 4, f"donor scene has {n} Angler AI seats, expected 4"
    scene = scene.replace(AI_DONOR, AI)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", OMNI, MONITOR,
                   CONTROLLER_TAIL]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameStarweave.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameStarweave", G_ASSET["MinigameStarweave.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameStarweave"])
g.register_always_unlocked()
build = g.read_current(lib.BUILD_SETTINGS)
g.register_build_scene("MinigameChum" if "MinigameChum.unity" in build else "MinigameGlasshouse",
                       "MinigameStarweave", G_ASSET["MinigameStarweave.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("starweaveRoundSeconds",
                    after="chumRoundSeconds" if "chumRoundSeconds:" in end else "glasshouseRoundSeconds",
                    value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
at_quarter = 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE
if at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {at_quarter:.2f} levels a quarter of the reference "
                  f"({REFERENCE_DOMAIN_VOLUME:.0f}) behind (< 1)")
counts = [r["crystals"] for r in RUNGS]
if any(b > a for a, b in zip(counts, counts[1:])):
    errors.append("the crystal ladder must be non-increasing (intensity is SCARCITY)")
spaces = [r["space"] for r in RUNGS]
if any(b < a for a, b in zip(spaces, spaces[1:])) or max(spaces) > 0.5:
    errors.append("the starting Space ladder must be non-decreasing and at most Space 5")
for name, (values, way) in LADDERS.items():
    pairs = list(zip(values, values[1:]))
    if any((b < a) if way == "up" else (b > a) for a, b in pairs):
        errors.append(f"AI ladder {name} must run {way} from Easy to Legendary")
if CREATION_SPEED >= 1.0:
    errors.append("Creation Mode is no longer slower than cruise - re-think the weave/raid trade (BRITTLE_STAR.md 3.2)")

# The settings asset and the C# agree on every field the generator writes.
settings_cs = g.read(SCRIPT_PATHS["StarweaveSettingsSO"])
for field in ["elementalCrystalCountByIntensity", "crystalScatterRadius", "crystalScatterSeed",
              "leadAnnounceAfterSeconds", "leadAnnounceMinGapSeconds", "progressSampleSeconds", "aiDecisionSeconds",
              "aiWeaveOrbitFraction", "aiWeaveOrbitDegreesPerSecond", *LADDERS]:
    if not re.search(rf"public (?:int|float)(?:\[\])? {field} = ", settings_cs):
        errors.append(f"StarweaveSettingsSO has no public field '{field}' - the asset and the C# disagree")
for name, (values, _) in LADDERS.items():
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"public float[] {name} = {init};" not in settings_cs:
        errors.append(f"StarweaveSettingsSO.{name}'s initializer is not {init} - the asset and the C# disagree")
if "public int[] elementalCrystalCountByIntensity = { " + ", ".join(str(c) for c in counts) + " };" not in settings_cs:
    errors.append("StarweaveSettingsSO's crystal ladder initializer disagrees with RUNGS")

# The load-bearing assumptions about the Brittle Star. A retune that breaks any of these breaks the mode.
executor = g.read(EXECUTOR)
for needle in (": BrittleStarControl.CreationMode;", "void Autopilot()",
               "handler.PerformShipControllerActionsReplicated(modeInput);"):
    if needle not in executor:
        errors.append(f"BrittleStarExecutor no longer has '{needle}' - Starweave's AI only steers and relies on the "
                      f"hull's own autopilot choosing Creation Mode when nothing rival lies ahead")
controller = g.read(SCRIPT_PATHS["StarweaveController"])
if "PerformShipControllerActionsReplicated" in controller:
    errors.append("StarweaveController presses an input - its AI only steers (the hull's autopilot presses the modes)")
for needle in ("SetExternalTargetProvider", "ClearExternalTargetProvider", "AIDifficultyFor",
               "GameToastSituation.StarweaveHint", "GameToastSituation.DomainRaceLeadChanged"):
    if needle not in controller:
        errors.append(f"StarweaveController no longer has '{needle}'")
enum_src = g.read("Assets/_Scripts/Data/Enums/GameModes.cs")
if f"Starweave = {MODE_ID}," not in enum_src:
    errors.append(f"GameModes.Starweave is not {MODE_ID}")
if f"StarweaveHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.StarweaveHint is not {TOAST_HINT}")

# The scene: every new identity present, every donor one gone, four Brittle Star AIs.
if scene is not None:
    for name, gd in {"controller": G_SCRIPT["StarweaveController"], "monitor": G_SCRIPT["StarweaveTimeTurnMonitor"],
                     "settings": G_ASSET["StarweaveSettings"], "rule": G_ASSET["StarweaveScoringRule"],
                     **{f"cell {i + 1}": c for i, c in enumerate(CELLS)}}.items():
        if gd not in scene:
            errors.append(f"scene is missing the {name} ({gd})")
    for name, gd in {**DONOR, **{f"Glasshouse cell {i}": c for i, c in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(AI) != 4 or AI_DONOR in scene:
        errors.append("scene does not seat exactly four Brittle Star AIs")
    if f"  duration: {ROUND_SECONDS}\n" not in scene:
        errors.append("scene's monitor carries no duration")

print(f"weave model: {BLOCKS_PER_SECOND:.2f} blocks/s x {BLOCK_VOLUME:.0f} vol = {WEAVE_RATE:.1f} vol/s per weaving "
      f"pilot; reference domain {REFERENCE_DOMAIN_VOLUME:.0f}; comeback {COMEBACK_RATE} ({at_quarter:.2f} levels a "
      f"quarter behind)")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Starweave"), guid(f"folder/{CELL_DIR}"), guid(f"text/{AR}/STARWEAVE.md")])
