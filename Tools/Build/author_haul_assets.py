#!/usr/bin/env python3
"""
Authors every serialized asset the Haul game mode needs (GameModes.Haul = 93, row 3 of
Docs/ElementalLoop/GAME_KIT_ROSTER.md, whose reserved ids it takes).

Haul is the Thresher's MASS game: a timed steal. Bales of neutral (Blue, no-team) prisms hang in a bare nucleus cell,
laid by HaulController on every peer from the intensity (HaulField, on ChumReef's geometry). Every Thresher spawns in
Mass Mode, where the ball STEALS what it touches that is not its colour, SHIELDS what is and lays a trail (THRESHER.md
2b). The score is the volume standing in your colour at the whistle (VolumeRemaining): the platform credits every steal
to its thief, so the mode adds nothing to the score.

THE LADDER (HaulField.ForIntensity; mirrored in RUNGS and checked against the C# below):
  I1 Hayfield   - ten small bales close to the core, four omni crystals (each a 12 s Windmill).
  I2 Harvest    - fourteen bales, farther out.
  I3 Granary    - fourteen bigger bales under a SHIELDED crust: a steal only sheds a shield, so two passes.
  I4 Stronghold - eighteen crusted bales out by the membrane, two omni crystals.

NOTHING ABOUT THE THRESHER CHANGES (the LOCKED vessel rule). The AI only steers: an autopiloted Thresher never presses A,
so it stays in Mass Mode (THRESHER.md 2b), and circling a bale swings the ball through it.

The scene is cloned from Glasshouse's (a timed monitor, a controller declaring settings / rule / arenaCell, a nucleus
cell), with its identity, monitor, cells, the controller's field fields and the AI hulls swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_haul_assets.py [--check]
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 93                 # GAME_KIT_ROSTER.md row 3
TOAST_HINT = 168             # GAME_KIT_ROSTER.md row 3
TOAST_LEAD = 130             # DomainRaceLeadChanged, posted by the controller's own lead sampler
ROUND_SECONDS = 90           # Docs/ARCADE_MATCH_LENGTH.md; EndConditionOverridesSO.DefaultHaulRoundSeconds
g = lib.Generator(MODE_ID, "Haul")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "HaulController":      f"{AR}/Haul/HaulController.cs",
    "HaulSettingsSO":      f"{AR}/Haul/HaulSettingsSO.cs",
    "HaulScoringRuleSO":   f"{AR}/Haul/HaulScoringRuleSO.cs",
    "HaulField":           f"{AR}/Haul/HaulField.cs",
    "HaulTimeTurnMonitor": f"{AR}/TurnMonitors/HaulTimeTurnMonitor.cs",
    "HaulTests":           "Assets/_Scripts/Tests/Editor/HaulTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Haul Cell"
G_ASSET = {
    "ArcadeGameHaul":      guid("asset/ArcadeGameHaul"),
    "HaulScoringRule":     guid("asset/HaulScoringRule"),
    "HaulSettings":        guid("asset/HaulSettings"),
    "GameToastConfigHaul": guid("asset/GameToastConfigHaul"),
    "ModePreviewHaul":     guid("asset/ModePreviewHaul"),
    "MinigameHaul.unity":  guid("asset/MinigameHaul.unity"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"HaulCellConfig{i}"] = guid(f"asset/HaulCellConfig{i}")
    G_ASSET[f"HaulSpawnProfile{i}"] = guid(f"asset/HaulSpawnProfile{i}")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Thresher"] = lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_Thresher.asset")
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
EXISTING["SpawnablePrism"] = lib.existing_guid(SPAWNABLE_PRISM_PATH)
SPAWNABLE_PRISM_FILEID = 4563009547826722997
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell
EXISTING["Nucleus_prefab"] = "b9cf1833fa2493d4b8724ccb6740fb3a"          # Barren's nucleus (~392 u radius)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["Cytoplasm_prefab"] if "Cytoplasm_prefab" in lib.CELL_VISUALS \
    else lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
THRESHER_ID = lib.VESSEL_CLASS_ID["Thresher"]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ThresherExecutor.cs"
REEF_CS = f"{AR}/Chum/ChumReef.cs"

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

PRISM_SIZE = 7.0                 # ChumReef.PrismSize - the bales are laid on Chum's reef geometry
SPACING = 15.0                   # ChumReef.Spacing
RUNGS = [
    dict(i=1, label="Hayfield", bales=10, shells=2, band=0.30, crusted=False, omni=4,
         blurb="ten small bales close to the core - room to learn the swing"),
    dict(i=2, label="Harvest", bales=14, shells=2, band=0.45, crusted=False, omni=3,
         blurb="fourteen bales farther out, and every rival's ball stealing too"),
    dict(i=3, label="Granary", bales=14, shells=3, band=0.45, crusted=True, omni=3,
         blurb="bigger bales under a shielded crust - a steal only sheds a shield, so two passes"),
    dict(i=4, label="Stronghold", bales=18, shells=3, band=0.60, crusted=True, omni=2,
         blurb="eighteen crusted bales out by the membrane, and two Windmills"),
]


def shell_count(sh):
    return 1 if sh <= 0 else max(6, int(math.floor(4 * math.pi * sh * sh + 0.5)))


def spiral(i, n):
    y = 1 - 2 * (i + 0.5) / max(1, n)
    ring = math.sqrt(max(0.0, 1 - y * y))
    th = i * 2.39996323
    return (ring * math.cos(th), y, ring * math.sin(th))


for r in RUNGS:
    reach = r["shells"] * SPACING + PRISM_SIZE
    inner = NUCLEUS_RADIUS + reach
    outer = max(inner, MEMBRANE_RADIUS - reach)
    rad = inner + (outer - inner) * r["band"]
    r["centres"] = [tuple(c * rad for c in spiral(b, r["bales"])) for b in range(r["bales"])]
    r["per"] = 1 + sum(shell_count(sh) for sh in range(1, r["shells"] + 1))
    r["prisms"] = r["bales"] * r["per"]
    r["crust"] = r["bales"] * shell_count(r["shells"]) if r["crusted"] else 0
    r["reach"] = reach

BALE_PRISM = PRISM_SIZE ** 3
# A reference domain holds a quarter of the I2 field (three domains and the leftovers) - what a comeback is measured
# against. A quarter of THAT behind must buy at least one element level; the rate is chosen to buy 1.5.
REFERENCE_DOMAIN_VOLUME = RUNGS[1]["prisms"] * BALE_PRISM / 4
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)
TRAIL = re.search(r"^  massTrailPrismScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}",
                  g.read("Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset"), re.M)
assert TRAIL, "ThresherConfig massTrailPrismScale not found"
TRAIL_PRISM = float(TRAIL.group(1)) * float(TRAIL.group(2)) * float(TRAIL.group(3))


def ladder(r):
    """PhaseThresholds: the cell grows nothing; Frenzy sits 25 % above the whole field plus four balls' whole-round
    trail at Mass 10 (x2 on every axis) and never fires; Restless at 7 % of it."""
    trail = PILOTS * ROUND_SECONDS * 30          # a generous bound: a ball lays one prism per 6 u at up to ~180 u/s
    count = r["prisms"] + trail
    volume = r["prisms"] * BALE_PRISM + trail * TRAIL_PRISM * 8
    fe, fev = round(count * 1.25), round(volume * 1.25)
    return dict(RestlessEnter=round(fe * 0.07), RestlessExit=round(fe * 0.05), FrenzyEnter=fe,
                FrenzyExit=round(fe * 0.85), RestlessEnterVolume=round(fev * 0.07),
                RestlessExitVolume=round(fev * 0.05), FrenzyEnterVolume=fev, FrenzyExitVolume=round(fev * 0.85),
                maxCount=round(count), maxVolume=round(volume))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameHaul.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Haul")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/HAUL.md")

# ── 2. Scoring rule (timed, highest wins, VolumeRemaining = 12) ──────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/HaulScoringRule.asset", G_ASSET["HaulScoringRule"],
             lib.header_for(G_SCRIPT["HaulScoringRuleSO"], "HaulScoringRule") +
             """  metric: 12
  golfRules: 0
  volumeTemplate: '{volume} Volume'
  stolenTemplate: '{count} stolen'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME HAULED
""")

# ── 3. Settings (AI ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRaidDeficitFraction": ([1, 0.3, 0.15, 0.05], "down"),
    "aiLateRaidSeconds":     ([0, 15, 25, 35], "up"),
    "aiRetargetSeconds":     ([12, 9, 7, 5], "down"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/HaulSettings.asset", G_ASSET["HaulSettings"],
             lib.header_for(G_SCRIPT["HaulSettingsSO"], "HaulSettings")
             + "  leadAnnounceAfterSeconds: 20\n  leadAnnounceMinGapSeconds: 10\n  progressSampleSeconds: 1\n"
             + "  aiDecisionSeconds: 5\n"
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "  aiOrbitRadius: 160\n")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 1: a haul against the clock is still a game (the field is finite).
DESCRIPTION = ("Threshers only - a timed steal. Bales of loose prisms hang round the core. Your ball is in Mass Mode: "
               "swing it through a bale and every prism it touches turns your colour, sweep it through your own and "
               "they armour, and it lays your trail as it flies. Swing through a rival's hoard to steal it back. An omni "
               "crystal spins the ball round you for twelve seconds. The most volume standing in your colour when the "
               "clock runs out wins. Higher intensities put the bales farther out under a shielded crust.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameHaul"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameHaul") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Haul
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameHaul
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Thresher']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 9
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Haul.asset", G_ASSET["GameToastConfigHaul"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Haul") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Swing your ball through a bale - every prism it touches turns your colour, and "
                       "everything standing at the whistle is score", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigHaul"])

# ── 6. The arena: a bare nucleus cell per rung (no flora, no fauna: Tapestry's reasoning) ──
for r in RUNGS:
    i = r["i"]
    TH = ladder(r)
    g.emit_asset(f"{CELL_DIR}/Haul Spawn Profile {i}.asset", G_ASSET[f"HaulSpawnProfile{i}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"Haul Spawn Profile {i}") + """  FloraExcludeLocalDomain: 0
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
    desc = (f"{r['label']} - intensity {i} of Haul: {r['blurb']}. The standard {int(MEMBRANE_RADIUS)} u cell with "
            f"its nucleus (~{int(NUCLEUS_RADIUS)} u), no environment, no flora and no fauna. The field ({r['bales']} "
            f"bales, {r['prisms']:,} prisms of {int(PRISM_SIZE)} u, {r['crust']:,} of them shielded crust) is laid by "
            f"HaulController. {r['omni']} omni crystals respawn inside the nucleus (the scene's CrystalManager). Frenzy "
            f"sits 25% above the full arena ({TH['maxCount']:,} prisms, {TH['maxVolume']:,} volume). Authored by "
            f"Tools/Build/author_haul_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Haul Cell Config {i}.asset", G_ASSET[f"HaulCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Haul Cell Config {i}") + f"""  CellName: Haul
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS_FILEID}, guid: {EXISTING['Nucleus_prefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'HaulSpawnProfile{i}']}, type: 2}}
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

CELLS = [G_ASSET[f"HaulCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {r['omni']}\n" for r in RUNGS))
MONITOR_DONOR = (f"  m_Script: {{fileID: 11500000, guid: {DONOR['GlasshouseTimeTurnMonitor']}, type: 3}}\n"
                 "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
                 "  duration: 90\n")
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['HaulTimeTurnMonitor']}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
           f"  duration: {ROUND_SECONDS}\n")
CONTROLLER_DONOR_TAIL = (f"  settings: {{fileID: 11400000, guid: {DONOR['GlasshouseSettings']}, type: 2}}\n"
                         f"  rule: {{fileID: 11400000, guid: {DONOR['GlasshouseScoringRule']}, type: 2}}\n"
                         "  arenaCell: {fileID: 1700000065}\n")
CONTROLLER_TAIL = (f"  settings: {{fileID: 11400000, guid: {G_ASSET['HaulSettings']}, type: 2}}\n"
                   f"  rule: {{fileID: 11400000, guid: {G_ASSET['HaulScoringRule']}, type: 2}}\n"
                   "  arenaCell: {fileID: 1700000065}\n"
                   f"  balePrefab: {{fileID: {SPAWNABLE_PRISM_FILEID}, guid: {EXISTING['SpawnablePrism']}, type: 3}}\n"
                   f"  nucleusRadius: {int(NUCLEUS_RADIUS)}\n"
                   f"  membraneRadius: {int(MEMBRANE_RADIUS)}\n")
AI_DONOR = "".join(f"  - vesselClass: {ANGLER_ID}\n" for _ in range(1))
AI = f"  - vesselClass: {THRESHER_ID}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Haul.asset", G_ASSET["ModePreviewHaul"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Haul") + f"""  Mode: {MODE_ID}
  Notes: 'The bare nucleus cell at each intensity. OPEN-ENDED - the field is laid by HaulController at match start,
    so a preview has none - it teaches the Mass Mode ball. Vessel pinned to Thresher({THRESHER_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {THRESHER_ID}
  ObjectiveText: Swing your ball through the bales - everything standing in your colour at the whistle is score
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
g.register_preview(G_ASSET["ModePreviewHaul"])

# ── 8. Scene: Glasshouse's, with the identity, the monitor, the cells, the field fields and the AI hulls swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.replace_block(scene, CONTROLLER_DONOR_TAIL, CONTROLLER_TAIL, "controller fields")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["HaulController"], "controller")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    n = scene.count(AI_DONOR)
    assert n == 4, f"donor scene has {n} Angler AI seats, expected 4"
    scene = scene.replace(AI_DONOR, AI)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", OMNI, MONITOR,
                   CONTROLLER_TAIL]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameHaul.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameHaul", G_ASSET["MinigameHaul.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameHaul"])
g.register_always_unlocked()
build = g.read_current(lib.BUILD_SETTINGS)
g.register_build_scene("MinigameStarweave" if "MinigameStarweave.unity" in build else "MinigameGlasshouse",
                       "MinigameHaul", G_ASSET["MinigameHaul.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("haulRoundSeconds",
                    after="starweaveRoundSeconds" if "starweaveRoundSeconds:" in end else "glasshouseRoundSeconds",
                    value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
at_quarter = 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE
if at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {at_quarter:.2f} levels a quarter of the reference "
                  f"({REFERENCE_DOMAIN_VOLUME:.0f}) behind (< 1)")
prisms = [r["prisms"] for r in RUNGS]
if any(b <= a for a, b in zip(prisms, prisms[1:])):
    errors.append(f"the field must grow every rung: {prisms}")
for r in RUNGS:
    if r["prisms"] > 3500:
        errors.append(f"I{r['i']}: {r['prisms']} bale prisms - keep the field under the collider budget")
    worst = min(math.dist(a, b) for k, a in enumerate(r["centres"]) for b in r["centres"][k + 1:])
    if worst < 2 * r["reach"] + 40:
        errors.append(f"I{r['i']}: two bales sit {worst:.0f} u apart - closer than two bales and a ball's pass")
for name, (values, way) in LADDERS.items():
    pairs = list(zip(values, values[1:]))
    if any((b < a) if way == "up" else (b > a) for a, b in pairs):
        errors.append(f"AI ladder {name} must run {way} from Easy to Legendary")

# The twin and the C# agree: the ladder table, and the reef geometry it borrows.
field_cs = g.read(SCRIPT_PATHS["HaulField"])
for r in RUNGS:
    row = (f"{r['i'] if r['i'] < 4 else '_'} => new ChumReef.Rung({r['bales']}, {r['shells']}, {r['band']:.2f}f, "
           f"{'true' if r['crusted'] else 'false'}),")
    if row not in field_cs:
        errors.append(f"HaulField.ForIntensity no longer has '{row}' - the generator's RUNGS and the C# disagree")
reef_cs = g.read(REEF_CS)
for needle in (f"PrismSize = {lib.num(PRISM_SIZE)}f;", f"Spacing = {lib.num(SPACING)}f;",
               "Mathf.Max(6, Mathf.RoundToInt(4f * Mathf.PI * shell * shell))", "const float golden = 2.39996323f;"):
    if needle not in reef_cs:
        errors.append(f"ChumReef no longer has '{needle}' - Haul lays its bales on that geometry; re-derive the twin")

# The settings asset and the C# agree on every field the generator writes.
settings_cs = g.read(SCRIPT_PATHS["HaulSettingsSO"])
for field in ["leadAnnounceAfterSeconds", "leadAnnounceMinGapSeconds", "progressSampleSeconds", "aiDecisionSeconds",
              "aiOrbitRadius", *LADDERS]:
    if not re.search(rf"public (?:int|float)(?:\[\])? {field} = ", settings_cs):
        errors.append(f"HaulSettingsSO has no public field '{field}' - the asset and the C# disagree")
for name, (values, _) in LADDERS.items():
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"public float[] {name} = {init};" not in settings_cs:
        errors.append(f"HaulSettingsSO.{name}'s initializer is not {init} - the asset and the C# disagree")

# The load-bearing assumptions about the Thresher. A retune that breaks any of these breaks the mode.
executor = g.read(EXECUTOR)
for needle in ("prism.Steal(_status.PlayerName, _status.Domain);", "else prism.ActivateShield();",
               "void MassModeTouch(Prism prism, bool ownDomain, Vector3 at)"):
    if needle not in executor:
        errors.append(f"ThresherExecutor no longer has '{needle}' - Haul's whole economy is the Mass Mode ball stealing "
                      f"the neutral bales and shielding its own; re-check THRESHER.md 2b")
if "internal static void CreditPrismSteal(IRoundStats thief, float volume)" not in g.read(
        "Assets/_Scripts/Controller/Managers/StatsManager.cs"):
    errors.append("StatsManager.CreditPrismSteal is gone - Haul relies on the platform crediting every steal")
controller = g.read(SCRIPT_PATHS["HaulController"])
if "PerformShipControllerActionsReplicated" in controller:
    errors.append("HaulController presses an input - its AI only steers (an autopiloted Thresher stays in Mass Mode)")
for needle in ("SetExternalTargetProvider", "ClearExternalTargetProvider", "AIDifficultyFor", "HaulField.Lay(",
               "GameToastSituation.HaulHint", "GameToastSituation.DomainRaceLeadChanged", "Domains.Blue"):
    if needle not in controller:
        errors.append(f"HaulController no longer has '{needle}'")
enum_src = g.read("Assets/_Scripts/Data/Enums/GameModes.cs")
if f"Haul = {MODE_ID}," not in enum_src:
    errors.append(f"GameModes.Haul is not {MODE_ID}")
if f"HaulHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.HaulHint is not {TOAST_HINT}")

# The scene: every new identity present, every donor one gone, four Thresher AIs.
if scene is not None:
    for name, gd in {"controller": G_SCRIPT["HaulController"], "monitor": G_SCRIPT["HaulTimeTurnMonitor"],
                     "settings": G_ASSET["HaulSettings"], "rule": G_ASSET["HaulScoringRule"],
                     **{f"cell {i + 1}": c for i, c in enumerate(CELLS)}}.items():
        if gd not in scene:
            errors.append(f"scene is missing the {name} ({gd})")
    for name, gd in {**DONOR, **{f"Glasshouse cell {i}": c for i, c in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(AI) != 4 or AI_DONOR in scene:
        errors.append("scene does not seat exactly four Thresher AIs")
    if f"  duration: {ROUND_SECONDS}\n" not in scene:
        errors.append("scene's monitor carries no duration")

for r in RUNGS:
    print(f"  I{r['i']} {r['label']}: {r['bales']} bales, {r['prisms']} prisms ({r['crust']} crust)")
print(f"comeback {COMEBACK_RATE} against a reference domain of {REFERENCE_DOMAIN_VOLUME:.0f} volume "
      f"({at_quarter:.2f} levels a quarter behind); a bale prism is {BALE_PRISM:.0f} volume, a trail prism "
      f"{TRAIL_PRISM:.0f}")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Haul"), guid(f"folder/{CELL_DIR}"), guid(f"text/{AR}/HAUL.md")])
