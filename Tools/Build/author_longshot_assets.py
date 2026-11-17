#!/usr/bin/env python3
"""
Authors every serialized asset the Longshot game mode needs (GameModes.Longshot = 95, row 5 of
Docs/ElementalLoop/GAME_KIT_ROSTER.md, whose reserved ids it takes).

Longshot is the Serpent's SPACE game: a timed shoot. Lances of neutral (Blue, no-team) prisms - single-file lines of
cubes, bent into zig-zag runs at the top rungs - hang round the core of a bare nucleus cell, laid by LongshotController
on every peer from the intensity (LongshotField). The Serpent's sniper round is one hitscan cone that breaks EVERY
hostile prism inside it, neutral ones included (SERPENT_SNIPER_SCOPE.md), on a 12 s recovery: fired along a lance it
takes the whole run, fired across it a prism or two. A rival's straight trail is a lance of their colour. The score is
hostile prisms broken (PrismsDestroyed), credited by the platform, so the mode adds nothing to the score.

THE LADDER (LongshotField.ForIntensity; mirrored in RUNGS and checked against the C# below):
  I1 Range      - eight straight lances of 48 close to the core, four omni crystals.
  I2 Gallery    - twelve straight lances, farther out.
  I3 Dogleg     - twelve lances bent at the knee: two runs of 24, so two shots each.
  I4 Thornfield - sixteen lances zig-zagged into three runs of 16, out by the membrane, two omni crystals.

NOTHING ABOUT THE SERPENT CHANGES (the LOCKED vessel rule). The AI presses only the scope and the round
(LongshotRifleman): it lines up along a run, scopes, and fires when the sniper's own cone would take its rung's share.

The scene is cloned from Glasshouse's (a timed monitor, a controller declaring settings / rule / arenaCell, a nucleus
cell), with its identity, monitor, cells, the controller's field fields and the AI hulls swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_longshot_assets.py [--check]
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 95                 # GAME_KIT_ROSTER.md row 5
TOAST_HINT = 170             # GAME_KIT_ROSTER.md row 5
TOAST_LEAD = 130             # DomainRaceLeadChanged, posted by the controller's own lead sampler
ROUND_SECONDS = 90           # Docs/ARCADE_MATCH_LENGTH.md; EndConditionOverridesSO.DefaultLongshotRoundSeconds
g = lib.Generator(MODE_ID, "Longshot")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "LongshotController":      f"{AR}/Longshot/LongshotController.cs",
    "LongshotSettingsSO":      f"{AR}/Longshot/LongshotSettingsSO.cs",
    "LongshotScoringRuleSO":   f"{AR}/Longshot/LongshotScoringRuleSO.cs",
    "LongshotField":           f"{AR}/Longshot/LongshotField.cs",
    "LongshotRifleman":          f"{AR}/Longshot/LongshotRifleman.cs",
    "LongshotTimeTurnMonitor": f"{AR}/TurnMonitors/LongshotTimeTurnMonitor.cs",
    "LongshotTests":           "Assets/_Scripts/Tests/Editor/LongshotTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Longshot Cell"
G_ASSET = {
    "ArcadeGameLongshot":      guid("asset/ArcadeGameLongshot"),
    "LongshotScoringRule":     guid("asset/LongshotScoringRule"),
    "LongshotSettings":        guid("asset/LongshotSettings"),
    "GameToastConfigLongshot": guid("asset/GameToastConfigLongshot"),
    "ModePreviewLongshot":     guid("asset/ModePreviewLongshot"),
    "MinigameLongshot.unity":  guid("asset/MinigameLongshot.unity"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"LongshotCellConfig{i}"] = guid(f"asset/LongshotCellConfig{i}")
    G_ASSET[f"LongshotSpawnProfile{i}"] = guid(f"asset/LongshotSpawnProfile{i}")

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
RIFLE_CS = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/SniperShotActionExecutor.cs"
RIFLE_ASSET = "Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset"
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

PRISM_SIZE = 6.0                 # LongshotField.PrismSize (a cube)
SPACING = 12.0                   # LongshotField.Spacing
KNEE = 30.0                      # LongshotField.KneeDegrees
INNER_CLEARANCE = 150.0          # LongshotField.InnerClearance
OUTER_CLEARANCE = 150.0          # LongshotField.OuterClearance
CONE_HALF_ANGLE = 1.5            # SniperShotAction.coneHalfAngleDegrees
MIN_PATH_RADIUS = 6.0            # SniperShotAction.minPathRadius (LongshotRifleman restates it)
RUNGS = [
    dict(i=1, label="Range", lances=8, length=48, segments=1, band=0.35, omni=4,
         blurb="eight straight lances close to the core - room to learn to line up"),
    dict(i=2, label="Gallery", lances=12, length=48, segments=1, band=0.50, omni=3,
         blurb="twelve straight lances farther out, and every rival's rifle too"),
    dict(i=3, label="Dogleg", lances=12, length=48, segments=2, band=0.50, omni=3,
         blurb="every lance bent at the knee - two runs, so two shots to clear one"),
    dict(i=4, label="Thornfield", lances=16, length=48, segments=3, band=0.60, omni=2,
         blurb="sixteen lances zig-zagged into three short runs out by the membrane"),
]


def spiral(i, n):
    y = 1 - 2 * (i + 0.5) / max(1, n)
    ring = math.sqrt(max(0.0, 1 - y * y))
    th = i * 2.39996323
    return (ring * math.cos(th), y, ring * math.sin(th))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def unit(v):
    m = math.sqrt(dot(v, v))
    return tuple(c / m for c in v)


def rotate(v, axis, deg):
    """Rodrigues: v turned about unit axis by deg (Quaternion.AngleAxis's sense)."""
    a = math.radians(deg)
    c, sn = math.cos(a), math.sin(a)
    kxv = cross(axis, v)
    kdv = dot(axis, v)
    return tuple(v[k] * c + kxv[k] * sn + axis[k] * kdv * (1 - c) for k in range(3))


def lay(r):
    """The twin of LongshotField.Lay: every prism's position and its run, and every run's ends."""
    inner = NUCLEUS_RADIUS + INNER_CLEARANCE
    outer = max(inner, MEMBRANE_RADIUS - OUTER_CLEARANCE)
    rad = inner + (outer - inner) * r["band"]
    runs = max(1, r["segments"])
    per = r["length"] // runs
    pieces, segs = [], []
    for lance in range(r["lances"]):
        n = spiral(lance, r["lances"])
        axis = (1.0, 0.0, 0.0) if abs(n[1]) > 0.9 else (0.0, 1.0, 0.0)
        t = unit(cross(n, axis))
        b = cross(n, t)
        a = lance * 2.39996323
        base = unit(tuple(t[k] * math.cos(a) + b[k] * math.sin(a) for k in range(3)))
        mid = tuple(c * rad for c in n)
        lp, ls = [], []
        at = (0.0, 0.0, 0.0)
        for run in range(runs):
            count = r["length"] - per * (runs - 1) if run == runs - 1 else per
            turn = 0.0 if runs == 1 else (KNEE if run % 2 == 0 else -KNEE)
            d = rotate(base, n, turn)
            start = at if run == 0 else tuple(at[k] + d[k] * SPACING for k in range(3))
            for k in range(count):
                lp.append((tuple(start[c] + d[c] * k * SPACING for c in range(3)), len(segs) + len(ls)))
            end = tuple(start[c] + d[c] * (count - 1) * SPACING for c in range(3))
            ls.append([start, end, count])
            at = end
        cen = tuple(sum(p[c] for p, _ in lp) / len(lp) for c in range(3))
        shift = tuple(mid[c] - cen[c] for c in range(3))
        pieces += [(tuple(p[c] + shift[c] for c in range(3)), s_) for p, s_ in lp]
        segs += [(tuple(st[c] + shift[c] for c in range(3)), tuple(en[c] + shift[c] for c in range(3)), cnt, lance)
                 for st, en, cnt in ls]
    return rad, pieces, segs


def in_cone(p, apex, d, rng, half, min_r):
    rel = tuple(p[k] - apex[k] for k in range(3))
    tt = dot(rel, d)
    if tt < 0 or tt > rng:
        return False
    allowed = max(min_r, tt * math.tan(math.radians(half)))
    return max(0.0, dot(rel, rel) - tt * tt) <= allowed * allowed


for r in RUNGS:
    r["radius"], r["pieces"], r["segs"] = lay(r)
    r["prisms"] = len(r["pieces"])

CUBE_PRISM = PRISM_SIZE ** 3
# A reference domain breaks a quarter of the I2 field (three domains and the leftovers) - what a comeback is measured
# against, in PRISMS (the metric). A quarter of THAT behind must buy at least one element level; the rate buys 1.5.
REFERENCE_DOMAIN_PRISMS = RUNGS[1]["prisms"] / 4
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_PRISMS), 6)
TRAIL = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", g.read(SERPENT_PREFAB), re.M)
assert TRAIL, "Serpent.prefab BaseScale not found"
TRAIL_PRISM = float(TRAIL.group(1)) * float(TRAIL.group(2)) * float(TRAIL.group(3))


def ladder(r):
    """PhaseThresholds: the cell grows nothing; Frenzy sits 25 % above the whole field plus four Serpents' whole-round
    trail with every brick at Mass 10 (x2 on every axis) and never fires; Restless at 7 % of it."""
    trail = PILOTS * ROUND_SECONDS * 30          # a generous bound on the prisms four trails lay in a round
    count = r["prisms"] + trail
    volume = r["prisms"] * CUBE_PRISM + trail * TRAIL_PRISM * 8
    fe, fev = round(count * 1.25), round(volume * 1.25)
    return dict(RestlessEnter=round(fe * 0.07), RestlessExit=round(fe * 0.05), FrenzyEnter=fe,
                FrenzyExit=round(fe * 0.85), RestlessEnterVolume=round(fev * 0.07),
                RestlessExitVolume=round(fev * 0.05), FrenzyEnterVolume=fev, FrenzyExitVolume=round(fev * 0.85),
                maxCount=round(count), maxVolume=round(volume))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameLongshot.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Longshot")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/LONGSHOT.md")

# ── 2. Scoring rule (timed, highest wins, PrismsDestroyed = 5) ───────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/LongshotScoringRule.asset", G_ASSET["LongshotScoringRule"],
             lib.header_for(G_SCRIPT["LongshotScoringRuleSO"], "LongshotScoringRule") +
             """  metric: 5
  golfRules: 0
  prismsTemplate: '{count} Prisms'
  volumeTemplate: '{volume} Volume'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: PRISMS BROKEN
""")

# ── 3. Settings (AI ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiFireFraction":              ([0.25, 0.45, 0.65, 0.8], "up"),
    "aiScopeSettleSeconds":        ([1, 0.6, 0.35, 0.2], "down"),
    "aiRetargetSeconds":           ([14, 11, 9, 7], "down"),
    "aiRivalTrailDeficitFraction": ([1, 0.3, 0.15, 0.05], "down"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/LongshotSettings.asset", G_ASSET["LongshotSettings"],
             lib.header_for(G_SCRIPT["LongshotSettingsSO"], "LongshotSettings")
             + "  leadAnnounceAfterSeconds: 20\n  leadAnnounceMinGapSeconds: 10\n  progressSampleSeconds: 1\n"
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "  aiStandOff: 260\n  aiCarrot: 120\n  aiScopeLateral: 25\n  aiShotGiveUpSeconds: 4\n"
             + "  aiDecisionSeconds: 5\n")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 1: a shoot against the clock is still a game (the field is finite).
DESCRIPTION = ("Serpents only - a timed shoot. Lances of loose prisms hang round the core. Hold the scope and your "
               "sniper round breaks every prism on its line: line up along a lance and one shot takes the whole run, "
               "fire across it and you take a prism or two. Your rifle needs twelve seconds between shots, so make each "
               "one count - and a rival flying straight leaves a lance of their own colour behind them. The most prisms "
               "broken when the clock runs out wins. Higher intensities bend the lances into shorter runs, farther out.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameLongshot"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameLongshot") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Longshot
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameLongshot
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Serpent']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Longshot.asset", G_ASSET["GameToastConfigLongshot"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Longshot") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} broken", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Line up along a lance and hold the scope - one round breaks every prism on its line",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigLongshot"])

# ── 6. The arena: a bare nucleus cell per rung (no flora, no fauna: Tapestry's reasoning) ──
for r in RUNGS:
    i = r["i"]
    TH = ladder(r)
    g.emit_asset(f"{CELL_DIR}/Longshot Spawn Profile {i}.asset", G_ASSET[f"LongshotSpawnProfile{i}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"Longshot Spawn Profile {i}") + """  FloraExcludeLocalDomain: 0
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
    desc = (f"{r['label']} - intensity {i} of Longshot: {r['blurb']}. The standard {int(MEMBRANE_RADIUS)} u cell with "
            f"its nucleus (~{int(NUCLEUS_RADIUS)} u), no environment, no flora and no fauna. The field ({r['lances']} "
            f"lances of {r['length']} cubes in {len(r['segs'])} runs, {r['prisms']:,} prisms in all, {r['radius']:.0f} u out) is laid by "
            f"LongshotController. {r['omni']} omni crystals respawn inside the nucleus (the scene's CrystalManager). Frenzy "
            f"sits 25% above the full arena ({TH['maxCount']:,} prisms, {TH['maxVolume']:,} volume). Authored by "
            f"Tools/Build/author_longshot_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Longshot Cell Config {i}.asset", G_ASSET[f"LongshotCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Longshot Cell Config {i}") + f"""  CellName: Longshot
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS_FILEID}, guid: {EXISTING['Nucleus_prefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'LongshotSpawnProfile{i}']}, type: 2}}
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

CELLS = [G_ASSET[f"LongshotCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {r['omni']}\n" for r in RUNGS))
MONITOR_DONOR = (f"  m_Script: {{fileID: 11500000, guid: {DONOR['GlasshouseTimeTurnMonitor']}, type: 3}}\n"
                 "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
                 "  duration: 90\n")
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['LongshotTimeTurnMonitor']}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
           f"  duration: {ROUND_SECONDS}\n")
CONTROLLER_DONOR_TAIL = (f"  settings: {{fileID: 11400000, guid: {DONOR['GlasshouseSettings']}, type: 2}}\n"
                         f"  rule: {{fileID: 11400000, guid: {DONOR['GlasshouseScoringRule']}, type: 2}}\n"
                         "  arenaCell: {fileID: 1700000065}\n")
CONTROLLER_TAIL = (f"  settings: {{fileID: 11400000, guid: {G_ASSET['LongshotSettings']}, type: 2}}\n"
                   f"  rule: {{fileID: 11400000, guid: {G_ASSET['LongshotScoringRule']}, type: 2}}\n"
                   "  arenaCell: {fileID: 1700000065}\n"
                   f"  lancePrefab: {{fileID: {SPAWNABLE_PRISM_FILEID}, guid: {EXISTING['SpawnablePrism']}, type: 3}}\n"
                   f"  nucleusRadius: {int(NUCLEUS_RADIUS)}\n"
                   f"  membraneRadius: {int(MEMBRANE_RADIUS)}\n")
AI_DONOR = "".join(f"  - vesselClass: {ANGLER_ID}\n" for _ in range(1))
AI = f"  - vesselClass: {SERPENT_ID}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Longshot.asset", G_ASSET["ModePreviewLongshot"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Longshot") + f"""  Mode: {MODE_ID}
  Notes: 'The bare nucleus cell at each intensity. OPEN-ENDED - the field is laid by LongshotController at match start,
    so a preview has none - it teaches the stop. Vessel pinned to Serpent({SERPENT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SERPENT_ID}
  ObjectiveText: Line up along a lance and hold the scope - one round breaks every prism on its line
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
g.register_preview(G_ASSET["ModePreviewLongshot"])

# ── 8. Scene: Glasshouse's, with the identity, the monitor, the cells, the field fields and the AI hulls swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.replace_block(scene, CONTROLLER_DONOR_TAIL, CONTROLLER_TAIL, "controller fields")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["LongshotController"], "controller")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    n = scene.count(AI_DONOR)
    assert n == 4, f"donor scene has {n} Angler AI seats, expected 4"
    scene = scene.replace(AI_DONOR, AI)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", OMNI, MONITOR,
                   CONTROLLER_TAIL]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameLongshot.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameLongshot", G_ASSET["MinigameLongshot.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameLongshot"])
g.register_always_unlocked()
build = g.read_current(lib.BUILD_SETTINGS)
g.register_build_scene("MinigameHedgerow" if "MinigameHedgerow.unity" in build else "MinigameGlasshouse",
                       "MinigameLongshot", G_ASSET["MinigameLongshot.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("longshotRoundSeconds",
                    after="hedgerowRoundSeconds" if "hedgerowRoundSeconds:" in end else "glasshouseRoundSeconds",
                    value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
at_quarter = 0.25 * REFERENCE_DOMAIN_PRISMS * COMEBACK_RATE
if at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {at_quarter:.2f} levels a quarter of the reference "
                  f"({REFERENCE_DOMAIN_PRISMS:.0f}) behind (< 1)")
sizes = [(r["prisms"], len(r["segs"])) for r in RUNGS]
if any(b[0] < a[0] or b == a or (b[0] == a[0] and b[1] <= a[1]) for a, b in zip(sizes, sizes[1:])):
    errors.append(f"every rung must lay more prisms or cut them into more runs: {sizes}")
for r in RUNGS:
    if r["prisms"] > 3500:
        errors.append(f"I{r['i']}: {r['prisms']} lance prisms - keep the field under the collider budget")
    pts = r["pieces"]
    reach = max(math.dist(p, (0, 0, 0)) for p, _ in pts)
    near = min(math.dist(p, (0, 0, 0)) for p, _ in pts)
    if reach > MEMBRANE_RADIUS - 50:
        errors.append(f"I{r['i']}: a lance prism sits {reach:.0f} u out - too close to the membrane")
    if near < NUCLEUS_RADIUS + 50:
        errors.append(f"I{r['i']}: a lance prism sits {near:.0f} u out - too close to the nucleus")
    # Lances apart: a round down one run never grazes another lance on its way.
    by_lance = {}
    for p, seg in pts:
        by_lance.setdefault(r["segs"][seg][3], []).append(p)
    lances = list(by_lance.values())
    worst = min(math.dist(a, b) for x in range(len(lances)) for y in range(x + 1, len(lances))
                for a in lances[x][::3] for b in lances[y][::3])
    r["gap"] = worst
    if worst < 60:
        errors.append(f"I{r['i']}: two lances come {worst:.0f} u apart - closer than a lined-up round's cone")
    # THE PROMISE: from the AI's line-up point, a round down a run takes every prism of it; across it, a few.
    best_along, worst_across = 10 ** 9, 0
    for si, (st, en, cnt, _) in enumerate(r["segs"]):
        d = unit(tuple(en[k] - st[k] for k in range(3)))
        apex = tuple(st[k] - d[k] * 260.0 for k in range(3))
        mine = [p for p, s_ in pts if s_ == si]
        along = sum(in_cone(p, apex, d, 3000.0, CONE_HALF_ANGLE, MIN_PATH_RADIUS) for p in mine)
        best_along = min(best_along, along / cnt)
        side = unit(cross(d, unit(st)))
        midp = tuple((st[k] + en[k]) / 2 for k in range(3))
        apex2 = tuple(midp[k] - side[k] * 260.0 for k in range(3))
        worst_across = max(worst_across, sum(in_cone(p, apex2, side, 3000.0, CONE_HALF_ANGLE, MIN_PATH_RADIUS)
                                             for p in mine))
    r["along"], r["across"] = best_along, worst_across
    if best_along < 1.0:
        errors.append(f"I{r['i']}: a round down a run takes only {best_along:.0%} of it - a run is not straight")
    if worst_across > 3:
        errors.append(f"I{r['i']}: a round across a run takes {worst_across} prisms - lining up would not matter")
for name, (values, way) in LADDERS.items():
    pairs = list(zip(values, values[1:]))
    if any((b < a) if way == "up" else (b > a) for a, b in pairs):
        errors.append(f"AI ladder {name} must run {way} from Easy to Legendary")

# The twin and the C# agree: the ladder table and the geometry constants.
field_cs = g.read(SCRIPT_PATHS["LongshotField"])
for r in RUNGS:
    row = (f"{r['i'] if r['i'] < 4 else '_'} => new Rung({r['lances']}, {r['length']}, {r['segments']}, "
           f"{r['band']:.2f}f),")
    if row not in field_cs:
        errors.append(f"LongshotField.ForIntensity no longer has '{row}' - the generator's RUNGS and the C# disagree")
for needle in (f"PrismSize = {lib.num(PRISM_SIZE)}f;", f"Spacing = {lib.num(SPACING)}f;",
               f"KneeDegrees = {lib.num(KNEE)}f;", f"InnerClearance = {lib.num(INNER_CLEARANCE)}f;",
               f"OuterClearance = {lib.num(OUTER_CLEARANCE)}f;", "const float golden = 2.39996323f;",
               "Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up",
               "float turn = runs == 1 ? 0f : (r % 2 == 0 ? KneeDegrees : -KneeDegrees);"):
    if needle not in field_cs:
        errors.append(f"LongshotField no longer has '{needle}' - re-derive the generator's twin")
if "const float golden = 2.39996323f;" not in g.read(f"{AR}/Chum/ChumReef.cs"):
    errors.append("ChumReef.SpiralDirection changed - LongshotField places its lances on it; re-derive the twin")

# The settings asset and the C# agree on every field the generator writes.
settings_cs = g.read(SCRIPT_PATHS["LongshotSettingsSO"])
for field in ["leadAnnounceAfterSeconds", "leadAnnounceMinGapSeconds", "progressSampleSeconds", "aiDecisionSeconds",
              "aiStandOff", "aiCarrot", "aiScopeLateral", "aiShotGiveUpSeconds", *LADDERS]:
    if not re.search(rf"public (?:int|float)(?:\[\])? {field} = ", settings_cs):
        errors.append(f"LongshotSettingsSO has no public field '{field}' - the asset and the C# disagree")
for name, (values, _) in LADDERS.items():
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"public float[] {name} = {init};" not in settings_cs:
        errors.append(f"LongshotSettingsSO.{name}'s initializer is not {init} - the asset and the C# disagree")

# The load-bearing assumptions about the Serpent. A retune that breaks any of these breaks the mode.
rifle_cs = g.read(RIFLE_CS)
for needle in ("if (prism.Domain == _status.Domain) return false;",
               "int budget = so.PierceCount <= 0 ? int.MaxValue : so.PierceCount;",
               "Vector3 origin = ship.position;", "Vector3 direction = ship.forward;"):
    if needle not in rifle_cs:
        errors.append(f"SniperShotActionExecutor no longer has '{needle}' - Longshot's whole economy is one round down "
                      f"the nose breaking every non-own prism on its line; re-check SERPENT_SNIPER_SCOPE.md")
rifle = g.read(RIFLE_ASSET)
for field, value in (("pierceCount", "0"), ("coneHalfAngleDegrees", lib.num(CONE_HALF_ANGLE)),
                     ("minPathRadius", lib.num(MIN_PATH_RADIUS))):
    if f"  {field}: {value}\n" not in rifle:
        errors.append(f"SniperShotAction.{field} is no longer {value} - re-check the run geometry and the AI's cone")
if "ScoringMetric.PrismsDestroyed   => stats.HostilePrismsDestroyed," not in g.read(
        "Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs"):
    errors.append("PrismsDestroyed no longer reads HostilePrismsDestroyed - Longshot's score is every non-own prism broken")
controller = g.read(SCRIPT_PATHS["LongshotController"])
rifleman = g.read(SCRIPT_PATHS["LongshotRifleman"])
if "PerformShipControllerActionsReplicated" in controller:
    errors.append("LongshotController presses an input - only the rifleman presses, and only the scope and the round")
if re.search(r"PerformShipControllerActionsReplicated\((?!input\))", rifleman) or \
        re.search(r"Press\(r, InputEvents\.(?!LeftStickAction|RightStickAction)", rifleman):
    errors.append("LongshotRifleman presses something other than the scope (LT) and the round (RT)")
if f"const float minRadius = {lib.num(MIN_PATH_RADIUS)}f;" not in rifleman:
    errors.append(f"LongshotRifleman's cone no longer restates minPathRadius {MIN_PATH_RADIUS}")
for needle in ("LongshotField.Lay(", "GameToastSituation.LongshotHint", "GameToastSituation.DomainRaceLeadChanged",
               "Domains.Blue", "_riflemen.Arm(", "_riflemen.Disarm();"):
    if needle not in controller:
        errors.append(f"LongshotController no longer has '{needle}'")
for needle in ("SetExternalTargetProvider", "ClearExternalTargetProvider", "AIDifficultyFor", "SetCycledAbilityFilter",
               "LongshotField.InCone(", "CooldownRemaining"):
    if needle not in rifleman:
        errors.append(f"LongshotRifleman no longer has '{needle}'")
enum_src = g.read("Assets/_Scripts/Data/Enums/GameModes.cs")
if f"Longshot = {MODE_ID}," not in enum_src:
    errors.append(f"GameModes.Longshot is not {MODE_ID}")
if f"LongshotHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.LongshotHint is not {TOAST_HINT}")

# The scene: every new identity present, every donor one gone, four Serpent AIs.
if scene is not None:
    for name, gd in {"controller": G_SCRIPT["LongshotController"], "monitor": G_SCRIPT["LongshotTimeTurnMonitor"],
                     "settings": G_ASSET["LongshotSettings"], "rule": G_ASSET["LongshotScoringRule"],
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
    print(f"  I{r['i']} {r['label']}: {r['lances']} lances in {len(r['segs'])} runs, {r['prisms']} prisms, "
          f"{r['radius']:.0f} u out, lances {r.get('gap', 0):.0f} u apart; a round down a run takes "
          f"{r.get('along', 0):.0%}, across one {r.get('across', 0)}")
print(f"comeback {COMEBACK_RATE} against a reference domain of {REFERENCE_DOMAIN_PRISMS:.0f} prisms "
      f"({at_quarter:.2f} levels a quarter behind)")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Longshot"), guid(f"folder/{CELL_DIR}"), guid(f"text/{AR}/LONGSHOT.md")])
