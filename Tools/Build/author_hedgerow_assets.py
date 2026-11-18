#!/usr/bin/env python3
"""
Authors every serialized asset the Hedgerow game mode needs (GameModes.Hedgerow = 94, row 4 of
Docs/ElementalLoop/GAME_KIT_ROSTER.md, whose reserved ids it takes).

Hedgerow is the Serpent's MASS game: a timed steal. Hedges of neutral (Blue, no-team) prisms - straight rows of the
Serpent trail's own bricks - stand round the core of a bare nucleus cell, laid by HedgerowController on every peer from
the intensity (HedgerowField). Stop dead (the stance) just short of a hedge, flying across it, and the Serpent's latest
trail prism becomes a held seed: a wall grows in the plane across its heading and steals loose mass from its edge, so
it creeps along the hedge, and every brick it takes turns your colour (SERPENT_SEED_WALL.md). It eats a rival's trail
and bricks the same way. The score is the volume standing in your colour at the whistle (VolumeRemaining): the platform
credits every steal to its thief, so the mode adds nothing to the score.

THE LADDER (HedgerowField.ForIntensity; mirrored in RUNGS and checked against the C# below):
  I1 Hedgerow  - eight low hedges close to the core, four omni crystals (each re-shields your walls).
  I2 Bocage    - twelve hedges, farther out.
  I3 Thicket   - twelve longer hedges three bricks high: a wall needs a longer hold to eat one.
  I4 Labyrinth - sixteen thick hedges out by the membrane, two omni crystals.

NOTHING ABOUT THE SERPENT CHANGES (the LOCKED vessel rule). The AI presses only the stop toggle (HedgerowWarden): it
runs in across a hedge, stops short of it and holds, and the autopilot's ability cycler is kept off the stance and the
cloak.

The scene is cloned from Glasshouse's (a timed monitor, a controller declaring settings / rule / arenaCell, a nucleus
cell), with its identity, monitor, cells, the controller's field fields and the AI hulls swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_hedgerow_assets.py [--check]
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 94                 # GAME_KIT_ROSTER.md row 4
TOAST_HINT = 169             # GAME_KIT_ROSTER.md row 4
TOAST_LEAD = 130             # DomainRaceLeadChanged, posted by the controller's own lead sampler
ROUND_SECONDS = 90           # Docs/ARCADE_MATCH_LENGTH.md; EndConditionOverridesSO.DefaultHedgerowRoundSeconds
g = lib.Generator(MODE_ID, "Hedgerow")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "HedgerowController":      f"{AR}/Hedgerow/HedgerowController.cs",
    "HedgerowSettingsSO":      f"{AR}/Hedgerow/HedgerowSettingsSO.cs",
    "HedgerowScoringRuleSO":   f"{AR}/Hedgerow/HedgerowScoringRuleSO.cs",
    "HedgerowField":           f"{AR}/Hedgerow/HedgerowField.cs",
    "HedgerowWarden":          f"{AR}/Hedgerow/HedgerowWarden.cs",
    "HedgerowTimeTurnMonitor": f"{AR}/TurnMonitors/HedgerowTimeTurnMonitor.cs",
    "HedgerowTests":           "Assets/_Scripts/Tests/Editor/HedgerowTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Hedgerow Cell"
G_ASSET = {
    "ArcadeGameHedgerow":      guid("asset/ArcadeGameHedgerow"),
    "HedgerowScoringRule":     guid("asset/HedgerowScoringRule"),
    "HedgerowSettings":        guid("asset/HedgerowSettings"),
    "GameToastConfigHedgerow": guid("asset/GameToastConfigHedgerow"),
    "ModePreviewHedgerow":     guid("asset/ModePreviewHedgerow"),
    "MinigameHedgerow.unity":  guid("asset/MinigameHedgerow.unity"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"HedgerowCellConfig{i}"] = guid(f"asset/HedgerowCellConfig{i}")
    G_ASSET[f"HedgerowSpawnProfile{i}"] = guid(f"asset/HedgerowSpawnProfile{i}")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Serpent"] = lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_Serpent.asset")
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
EXISTING["SpawnablePrism"] = lib.existing_guid(SPAWNABLE_PRISM_PATH)
SPAWNABLE_PRISM_FILEID = 4563009547826722997
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell
EXISTING["Nucleus_prefab"] = "b9cf1833fa2493d4b8724ccb6740fb3a"          # Barren's nucleus (~392 u radius)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["Cytoplasm_prefab"] if "Cytoplasm_prefab" in lib.CELL_VISUALS \
    else lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
SERPENT_ID = lib.VESSEL_CLASS_ID["Serpent"]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
ASSEMBLER = "Assets/_Scripts/Controller/Assemblers/SerpentWallAssembler.cs"
SEED_WALL = "Assets/_SO_Assets/VesselActions/Serpent/SeedWallAction.asset"
SERPENT_PREFAB = "Assets/_Prefabs/Spacevessels/Serpent.prefab"

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

BRICK = (3.0, 6.0, 0.5)          # HedgerowField.BrickScale - the Serpent trail's own BaseScale
SPACING = 10.0                   # HedgerowField.Spacing
INNER_CLEARANCE = 200.0          # HedgerowField.InnerClearance
OUTER_CLEARANCE = 100.0          # HedgerowField.OuterClearance
STEAL_RADIUS = 40.0              # SeedWallAction.stealRadius at rest
RUNGS = [
    dict(i=1, label="Hedgerow", rows=8, length=32, ranks=2, band=0.30, omni=4,
         blurb="eight low hedges close to the core - room to learn the stop"),
    dict(i=2, label="Bocage", rows=12, length=32, ranks=2, band=0.45, omni=3,
         blurb="twelve hedges farther out, and every rival's wall eating too"),
    dict(i=3, label="Thicket", rows=12, length=40, ranks=3, band=0.45, omni=3,
         blurb="longer hedges three bricks high - a wall needs a longer hold to eat one"),
    dict(i=4, label="Labyrinth", rows=16, length=40, ranks=3, band=0.60, omni=2,
         blurb="sixteen thick hedges out by the membrane, and two crystals to armour your walls"),
]


def spiral(i, n):
    y = 1 - 2 * (i + 0.5) / max(1, n)
    ring = math.sqrt(max(0.0, 1 - y * y))
    th = i * 2.39996323
    return (ring * math.cos(th), y, ring * math.sin(th))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def unit(v):
    m = math.sqrt(sum(c * c for c in v))
    return tuple(c / m for c in v)


def lay(r):
    """The twin of HedgerowField.Lay: every prism's position and its hedge."""
    inner = NUCLEUS_RADIUS + INNER_CLEARANCE
    outer = max(inner, MEMBRANE_RADIUS - OUTER_CLEARANCE)
    rad = inner + (outer - inner) * r["band"]
    out = []
    for row in range(r["rows"]):
        n = spiral(row, r["rows"])
        axis = (1.0, 0.0, 0.0) if abs(n[1]) > 0.9 else (0.0, 1.0, 0.0)
        t = unit(cross(n, axis))
        b = cross(n, t)
        a = row * 2.39996323
        along = unit(tuple(t[k] * math.cos(a) + b[k] * math.sin(a) for k in range(3)))
        up = cross(n, along)
        mid = tuple(c * rad for c in n)
        hl, hr = (r["length"] - 1) * 0.5, (r["ranks"] - 1) * 0.5
        for k in range(r["length"]):
            for h in range(r["ranks"]):
                out.append((tuple(mid[c] + along[c] * (k - hl) * SPACING + up[c] * (h - hr) * SPACING
                                  for c in range(3)), row))
    return rad, out


for r in RUNGS:
    r["radius"], r["pieces"] = lay(r)
    r["prisms"] = len(r["pieces"])

BRICK_PRISM = BRICK[0] * BRICK[1] * BRICK[2]
# A reference domain holds a quarter of the I2 field (three domains and the leftovers) - what a comeback is measured
# against. A quarter of THAT behind must buy at least one element level; the rate is chosen to buy 1.5.
REFERENCE_DOMAIN_VOLUME = RUNGS[1]["prisms"] * BRICK_PRISM / 4
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)
TRAIL = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", g.read(SERPENT_PREFAB), re.M)
assert TRAIL, "Serpent.prefab BaseScale not found"
TRAIL_PRISM = float(TRAIL.group(1)) * float(TRAIL.group(2)) * float(TRAIL.group(3))


def ladder(r):
    """PhaseThresholds: the cell grows nothing; Frenzy sits 25 % above the whole field plus four Serpents' whole-round
    trail with every brick at Mass 10 (x2 on every axis) and never fires; Restless at 7 % of it."""
    trail = PILOTS * ROUND_SECONDS * 30          # a generous bound on the prisms four trails lay in a round
    count = r["prisms"] + trail
    volume = (r["prisms"] + trail) * TRAIL_PRISM * 8
    fe, fev = round(count * 1.25), round(volume * 1.25)
    return dict(RestlessEnter=round(fe * 0.07), RestlessExit=round(fe * 0.05), FrenzyEnter=fe,
                FrenzyExit=round(fe * 0.85), RestlessEnterVolume=round(fev * 0.07),
                RestlessExitVolume=round(fev * 0.05), FrenzyEnterVolume=fev, FrenzyExitVolume=round(fev * 0.85),
                maxCount=round(count), maxVolume=round(volume))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameHedgerow.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Hedgerow")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/HEDGEROW.md")

# ── 2. Scoring rule (timed, highest wins, VolumeRemaining = 12) ──────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/HedgerowScoringRule.asset", G_ASSET["HedgerowScoringRule"],
             lib.header_for(G_SCRIPT["HedgerowScoringRuleSO"], "HedgerowScoringRule") +
             """  metric: 12
  golfRules: 0
  volumeTemplate: '{volume} Volume'
  stolenTemplate: '{count} stolen'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME WALLED
""")

# ── 3. Settings (AI ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRaidDeficitFraction": ([1, 0.3, 0.15, 0.05], "down"),
    "aiLateRaidSeconds":     ([0, 15, 25, 35], "up"),
    "aiRetargetSeconds":     ([12, 9, 7, 5], "down"),
    "aiHoldSeconds":         ([8, 12, 15, 18], "up"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/HedgerowSettings.asset", G_ASSET["HedgerowSettings"],
             lib.header_for(G_SCRIPT["HedgerowSettingsSO"], "HedgerowSettings")
             + "  leadAnnounceAfterSeconds: 20\n  leadAnnounceMinGapSeconds: 10\n  progressSampleSeconds: 1\n"
             + "  aiDecisionSeconds: 5\n"
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "  aiStopShort: 22\n  aiRunUp: 140\n  aiStopLateral: 60\n  aiRaidStopShort: 25\n"
             + "  aiStanceRetrySeconds: 1.5\n")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 1: a hedgerow against the clock is still a game (the field is finite).
DESCRIPTION = ("Serpents only - a timed steal. Hedges of loose bricks stand round the core. Fly across one and stop "
               "dead just short of it: your seed wall grows across your path and eats its way along the hedge, and "
               "every brick it takes turns your colour. Fly on and the wall stands; stop again to grow another. Walls "
               "eat a rival's trail just the same, and an omni crystal re-shields every wall you are growing. The most "
               "volume standing in your colour when the clock runs out wins. Higher intensities put longer, thicker "
               "hedges farther out.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameHedgerow"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameHedgerow") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Hedgerow
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameHedgerow
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Serpent']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Hedgerow.asset", G_ASSET["GameToastConfigHedgerow"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Hedgerow") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Fly across a hedge and stop dead just short of it - your wall eats along it, and "
                       "everything standing in your colour at the whistle is score", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigHedgerow"])

# ── 6. The arena: a bare nucleus cell per rung (no flora, no fauna: Tapestry's reasoning) ──
for r in RUNGS:
    i = r["i"]
    TH = ladder(r)
    g.emit_asset(f"{CELL_DIR}/Hedgerow Spawn Profile {i}.asset", G_ASSET[f"HedgerowSpawnProfile{i}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"Hedgerow Spawn Profile {i}") + """  FloraExcludeLocalDomain: 0
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
    desc = (f"{r['label']} - intensity {i} of Hedgerow: {r['blurb']}. The standard {int(MEMBRANE_RADIUS)} u cell with "
            f"its nucleus (~{int(NUCLEUS_RADIUS)} u), no environment, no flora and no fauna. The field ({r['rows']} "
            f"hedges of {r['length']} x {r['ranks']} bricks, {r['prisms']:,} prisms in all, {r['radius']:.0f} u out) is laid by "
            f"HedgerowController. {r['omni']} omni crystals respawn inside the nucleus (the scene's CrystalManager). Frenzy "
            f"sits 25% above the full arena ({TH['maxCount']:,} prisms, {TH['maxVolume']:,} volume). Authored by "
            f"Tools/Build/author_hedgerow_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Hedgerow Cell Config {i}.asset", G_ASSET[f"HedgerowCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Hedgerow Cell Config {i}") + f"""  CellName: Hedgerow
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS_FILEID}, guid: {EXISTING['Nucleus_prefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'HedgerowSpawnProfile{i}']}, type: 2}}
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

CELLS = [G_ASSET[f"HedgerowCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {r['omni']}\n" for r in RUNGS))
MONITOR_DONOR = (f"  m_Script: {{fileID: 11500000, guid: {DONOR['GlasshouseTimeTurnMonitor']}, type: 3}}\n"
                 "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
                 "  duration: 90\n")
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['HedgerowTimeTurnMonitor']}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
           f"  duration: {ROUND_SECONDS}\n")
CONTROLLER_DONOR_TAIL = (f"  settings: {{fileID: 11400000, guid: {DONOR['GlasshouseSettings']}, type: 2}}\n"
                         f"  rule: {{fileID: 11400000, guid: {DONOR['GlasshouseScoringRule']}, type: 2}}\n"
                         "  arenaCell: {fileID: 1700000065}\n")
CONTROLLER_TAIL = (f"  settings: {{fileID: 11400000, guid: {G_ASSET['HedgerowSettings']}, type: 2}}\n"
                   f"  rule: {{fileID: 11400000, guid: {G_ASSET['HedgerowScoringRule']}, type: 2}}\n"
                   "  arenaCell: {fileID: 1700000065}\n"
                   f"  hedgePrefab: {{fileID: {SPAWNABLE_PRISM_FILEID}, guid: {EXISTING['SpawnablePrism']}, type: 3}}\n"
                   f"  nucleusRadius: {int(NUCLEUS_RADIUS)}\n"
                   f"  membraneRadius: {int(MEMBRANE_RADIUS)}\n")
AI_DONOR = "".join(f"  - vesselClass: {ANGLER_ID}\n" for _ in range(1))
AI = f"  - vesselClass: {SERPENT_ID}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Hedgerow.asset", G_ASSET["ModePreviewHedgerow"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Hedgerow") + f"""  Mode: {MODE_ID}
  Notes: 'The bare nucleus cell at each intensity. OPEN-ENDED - the field is laid by HedgerowController at match start,
    so a preview has none - it teaches the stop. Vessel pinned to Serpent({SERPENT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SERPENT_ID}
  ObjectiveText: Stop dead short of a hedge and your wall eats along it - everything standing in your colour at the whistle is score
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
g.register_preview(G_ASSET["ModePreviewHedgerow"])

# ── 8. Scene: Glasshouse's, with the identity, the monitor, the cells, the field fields and the AI hulls swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.replace_block(scene, CONTROLLER_DONOR_TAIL, CONTROLLER_TAIL, "controller fields")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["HedgerowController"], "controller")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    n = scene.count(AI_DONOR)
    assert n == 4, f"donor scene has {n} Angler AI seats, expected 4"
    scene = scene.replace(AI_DONOR, AI)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", OMNI, MONITOR,
                   CONTROLLER_TAIL]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameHedgerow.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameHedgerow", G_ASSET["MinigameHedgerow.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameHedgerow"])
g.register_always_unlocked()
build = g.read_current(lib.BUILD_SETTINGS)
g.register_build_scene("MinigameHaul" if "MinigameHaul.unity" in build else "MinigameGlasshouse",
                       "MinigameHedgerow", G_ASSET["MinigameHedgerow.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("hedgerowRoundSeconds",
                    after="haulRoundSeconds" if "haulRoundSeconds:" in end else "glasshouseRoundSeconds",
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
        errors.append(f"I{r['i']}: {r['prisms']} hedge prisms - keep the field under the collider budget")
    pts = r["pieces"]
    reach = max(math.dist(p, (0, 0, 0)) for p, _ in pts)
    near = min(math.dist(p, (0, 0, 0)) for p, _ in pts)
    if reach > MEMBRANE_RADIUS - 50:
        errors.append(f"I{r['i']}: a hedge prism sits {reach:.0f} u out - too close to the membrane")
    if near < NUCLEUS_RADIUS + INNER_CLEARANCE - 1:
        errors.append(f"I{r['i']}: a hedge prism sits {near:.0f} u out - inside the run-up's clearance of the nucleus")
    # One hedge per wall: a stop at one hedge must not reach the next (the warden counts one hedge at a time).
    by_row = {}
    for p, row in pts:
        by_row.setdefault(row, []).append(p)
    rows = list(by_row.values())
    worst = min(math.dist(a, b) for x in range(len(rows)) for y in range(x + 1, len(rows))
                for a in rows[x][::4] for b in rows[y][::4])
    r["gap"] = worst
    if worst < 3 * STEAL_RADIUS:
        errors.append(f"I{r['i']}: two hedges come {worst:.0f} u apart - a wall at one would eat the other")
    if (r["ranks"] - 1) * SPACING > STEAL_RADIUS:
        errors.append(f"I{r['i']}: a hedge is taller than one wall's steal reach")
for name, (values, way) in LADDERS.items():
    pairs = list(zip(values, values[1:]))
    if any((b < a) if way == "up" else (b > a) for a, b in pairs):
        errors.append(f"AI ladder {name} must run {way} from Easy to Legendary")

# The twin and the C# agree: the ladder table and the geometry constants.
field_cs = g.read(SCRIPT_PATHS["HedgerowField"])
for r in RUNGS:
    row = (f"{r['i'] if r['i'] < 4 else '_'} => new Rung({r['rows']}, {r['length']}, {r['ranks']}, {r['band']:.2f}f),")
    if row not in field_cs:
        errors.append(f"HedgerowField.ForIntensity no longer has '{row}' - the generator's RUNGS and the C# disagree")
for needle in (f"BrickScale = new({lib.num(BRICK[0])}f, {lib.num(BRICK[1])}f, {lib.num(BRICK[2])}f);",
               f"Spacing = {lib.num(SPACING)}f;", f"InnerClearance = {lib.num(INNER_CLEARANCE)}f;",
               f"OuterClearance = {lib.num(OUTER_CLEARANCE)}f;", "const float golden = 2.39996323f;",
               "Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up"):
    if needle not in field_cs:
        errors.append(f"HedgerowField no longer has '{needle}' - re-derive the generator's twin")
if "const float golden = 2.39996323f;" not in g.read(f"{AR}/Chum/ChumReef.cs"):
    errors.append("ChumReef.SpiralDirection changed - HedgerowField places its hedges on it; re-derive the twin")

# The settings asset and the C# agree on every field the generator writes.
settings_cs = g.read(SCRIPT_PATHS["HedgerowSettingsSO"])
for field in ["leadAnnounceAfterSeconds", "leadAnnounceMinGapSeconds", "progressSampleSeconds", "aiDecisionSeconds",
              "aiStopShort", "aiRunUp", "aiStopLateral", "aiRaidStopShort", "aiStanceRetrySeconds", *LADDERS]:
    if not re.search(rf"public (?:int|float)(?:\[\])? {field} = ", settings_cs):
        errors.append(f"HedgerowSettingsSO has no public field '{field}' - the asset and the C# disagree")
for name, (values, _) in LADDERS.items():
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"public float[] {name} = {init};" not in settings_cs:
        errors.append(f"HedgerowSettingsSO.{name}'s initializer is not {init} - the asset and the C# disagree")

# The load-bearing assumptions about the Serpent. A retune that breaks any of these breaks the mode.
assembler = g.read(ASSEMBLER)
for needle in ("if (!own) m.Prism.Steal(_shape.PlayerName, _shape.Domain, true);",
               "if (props.IsShielded && !takeShielded && p.Domain != _shape.Domain) return false;"):
    if needle not in assembler:
        errors.append(f"SerpentWallAssembler no longer has '{needle}' - Hedgerow's whole economy is the wall stealing "
                      f"the unshielded neutral hedges; re-check SERPENT_SEED_WALL.md")
seed_wall = g.read(SEED_WALL)
if f"  stealRadius: {int(STEAL_RADIUS)}\n" not in seed_wall:
    errors.append(f"SeedWallAction.stealRadius is no longer {int(STEAL_RADIUS)} - re-fit aiStopShort and the hedge gap")
prefab = g.read(SERPENT_PREFAB)
if "  BaseScale: {x: 3, y: 6, z: 0.5}\n" not in prefab:
    errors.append("Serpent.prefab's trail BaseScale is no longer 3 x 6 x 0.5 - HedgerowField.BrickScale copies it")
if "internal static void CreditPrismSteal(IRoundStats thief, float volume" not in g.read(
        "Assets/_Scripts/Controller/Managers/StatsManager.cs"):
    errors.append("StatsManager.CreditPrismSteal is gone - Hedgerow relies on the platform crediting every steal")
controller = g.read(SCRIPT_PATHS["HedgerowController"])
warden = g.read(SCRIPT_PATHS["HedgerowWarden"])
if "PerformShipControllerActionsReplicated" in controller:
    errors.append("HedgerowController presses an input - only the warden presses, and only the stop toggle")
if re.search(r"PerformShipControllerActionsReplicated\((?!_stanceInput\))", warden):
    errors.append("HedgerowWarden presses something other than the stop toggle")
for needle in ("HedgerowField.Lay(", "GameToastSituation.HedgerowHint", "GameToastSituation.DomainRaceLeadChanged",
               "Domains.Blue", "_wardens.Arm(", "_wardens.Disarm();"):
    if needle not in controller:
        errors.append(f"HedgerowController no longer has '{needle}'")
for needle in ("SetExternalTargetProvider", "ClearExternalTargetProvider", "AIDifficultyFor",
               "TryGetBoundAction<ToggleTranslationModeActionSO>", "SetCycledAbilityFilter"):
    if needle not in warden:
        errors.append(f"HedgerowWarden no longer has '{needle}'")
enum_src = g.read("Assets/_Scripts/Data/Enums/GameModes.cs")
if f"Hedgerow = {MODE_ID}," not in enum_src:
    errors.append(f"GameModes.Hedgerow is not {MODE_ID}")
if f"HedgerowHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.HedgerowHint is not {TOAST_HINT}")

# The scene: every new identity present, every donor one gone, four Serpent AIs.
if scene is not None:
    for name, gd in {"controller": G_SCRIPT["HedgerowController"], "monitor": G_SCRIPT["HedgerowTimeTurnMonitor"],
                     "settings": G_ASSET["HedgerowSettings"], "rule": G_ASSET["HedgerowScoringRule"],
                     **{f"cell {i + 1}": c for i, c in enumerate(CELLS)}}.items():
        if gd not in scene:
            errors.append(f"scene is missing the {name} ({gd})")
    for name, gd in {**DONOR, **{f"Glasshouse cell {i}": c for i, c in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(AI) != 4 or AI_DONOR in scene:
        errors.append("scene does not seat exactly four Serpent AIs")
    if f"  duration: {ROUND_SECONDS}\n" not in scene:
        errors.append("scene's monitor carries no duration")

for r in RUNGS:
    print(f"  I{r['i']} {r['label']}: {r['rows']} hedges, {r['prisms']} prisms, {r['radius']:.0f} u out, "
          f"hedges {r.get('gap', 0):.0f} u apart at the closest")
print(f"comeback {COMEBACK_RATE} against a reference domain of {REFERENCE_DOMAIN_VOLUME:.0f} volume "
      f"({at_quarter:.2f} levels a quarter behind); a hedge prism is {BRICK_PRISM:.0f} volume, a trail prism "
      f"{TRAIL_PRISM:.0f}")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Hedgerow"), guid(f"folder/{CELL_DIR}"), guid(f"text/{AR}/HEDGEROW.md")])
