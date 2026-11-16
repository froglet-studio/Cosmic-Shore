#!/usr/bin/env python3
"""
Authors every serialized asset the Chum game mode needs (GameModes.Chum = 91, row 1 of
Docs/ElementalLoop/GAME_KIT_ROSTER.md, whose reserved ids it takes).

Chum is the Angler's SPACE game: a timed feed on a reef. Shoals of neutral (Blue, no-team) prisms hang in the cell,
laid by ChumController on every peer from the intensity (ChumReef, pure). Paint a shoal with the brush (RT), let go,
and the group whose turn it is is thrown at it and eats it; the next throw sends the other half (ANGLER.md 1c, 1m). A
bigger school eats faster: an omni crystal is one creature, and a grazer held in the lure's light joins it too. The
score is prisms taken off the board (PrismsDestroyed): every prism the pilot's hull breaks, as every mode counts it,
plus every reef prism their school eats, which the controller credits (a creature's bite names no player). Timed, 90 s.

THE LADDER (ChumReef.ForIntensity; mirrored in RUNGS and checked against the C# below):
  I1 Shallows - eight small shoals close to the core, four omni crystals, six grazers.
  I2 Reef     - twelve shoals, farther out, three crystals, nine grazers.
  I3 Crusted  - twelve bigger shoals with a SHIELDED outer shell: no grazer eats shielded mass, so the wild herds cannot
                strip them; the card starts the Angler at Space 5, whose Paint Shields cracks the crust. Twelve grazers.
  I4 Barrier  - sixteen crusted shoals out by the membrane, two crystals, fifteen grazers racing you for the cores.

NOTHING ABOUT THE ANGLER CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the reef, the
cell's herds, the scene's omni crystals per intensity, the card's starting Space on I3-I4 (SO_ArcadeGame.StartingElements,
Brittle Star Time's precedent) and the AI foragers. The scene is cloned from Glasshouse's (the Angler's Mass game: Angler
AI templates, a nucleus cell, a timed monitor, a controller declaring settings / rule / arenaCell), with its identity,
monitor, cells, crystals and the controller's reef fields swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_chum_assets.py [--check]
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 91                 # GAME_KIT_ROSTER.md row 1
TOAST_HINT = 166             # GAME_KIT_ROSTER.md row 1
TOAST_LEAD = 130             # DomainRaceLeadChanged, posted by the controller's own lead sampler
ROUND_SECONDS = 90           # Docs/ARCADE_MATCH_LENGTH.md; EndConditionOverridesSO.DefaultChumRoundSeconds
g = lib.Generator(MODE_ID, "Chum")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "ChumController":      f"{AR}/Chum/ChumController.cs",
    "ChumSettingsSO":      f"{AR}/Chum/ChumSettingsSO.cs",
    "ChumScoringRuleSO":   f"{AR}/Chum/ChumScoringRuleSO.cs",
    "ChumForager":         f"{AR}/Chum/ChumForager.cs",
    "ChumReef":            f"{AR}/Chum/ChumReef.cs",
    "ChumTimeTurnMonitor": f"{AR}/TurnMonitors/ChumTimeTurnMonitor.cs",
    "ChumReefTests":       "Assets/_Scripts/Tests/Editor/ChumReefTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Chum Cell"
G_ASSET = {
    "ArcadeGameChum":      guid("asset/ArcadeGameChum"),
    "ChumScoringRule":     guid("asset/ChumScoringRule"),
    "ChumSettings":        guid("asset/ChumSettings"),
    "GameToastConfigChum": guid("asset/GameToastConfigChum"),
    "ModePreviewChum":     guid("asset/ModePreviewChum"),
    "MinigameChum.unity":  guid("asset/MinigameChum.unity"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"ChumCellConfig{i}"] = guid(f"asset/ChumCellConfig{i}")
    G_ASSET[f"ChumSpawnProfile{i}"] = guid(f"asset/ChumSpawnProfile{i}")
    G_ASSET[f"ChumTadpole{i}"] = guid(f"asset/ChumTadpole{i}")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Angler"] = "ea422bbe0d1aef61993692999cee50ea"           # SO_Class_Angler.asset
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell
EXISTING["Nucleus_prefab"] = "b9cf1833fa2493d4b8724ccb6740fb3a"          # Barren's nucleus (~392 u radius)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["Cytoplasm_prefab"] if "Cytoplasm_prefab" in lib.CELL_VISUALS \
    else lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
EXISTING["FaunaConfigurationSO"] = "c778cfbe4dfc4c5c8401e40c17802311"
EXISTING["Tadpole_prefab"] = "c7fd418d426de8740ac888dcc23a5d24"
EXISTING["AnglerLure"] = "c329dd299612fe08ed9840cd879c0734"              # AnglerLure.asset (AnglerControl.Lure)
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
EXISTING["SpawnablePrism"] = lib.existing_guid(SPAWNABLE_PRISM_PATH)
SPAWNABLE_PRISM_FILEID = 4563009547826722997
# The herd: a Chum-owned copy of the Thresher Time I1 tadpole (Glasshouse's donor too), so tuning the herds re-tunes no
# other mode. Tadpoles are herbivores (they graze the reef too) and in the Angler's omniFauna: tameable.
TADPOLE_DONOR = "Assets/_SO_Assets/Cell Configs/Thresher Time Cell/Thresher Time I1 Tadpole Fauna Config Data.asset"
ANGLER_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/AnglerCommandExecutor.cs"
ANGLER_PREFAB = "Assets/_Prefabs/Spacevessels/Angler.prefab"
ANGLER_LURE = "Assets/_SO_Assets/VesselActions/Angler/AnglerLure.asset"
LIGHT_FAUNA = "Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs"
FAUNA_CS = "Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs"
PRISM_CS = "Assets/_Scripts/Controller/Vessel/Prism.cs"
CELL_CS = "Assets/_Scripts/Controller/Environment/Cell.cs"
# The donor scene's identity, minted by author_glasshouse_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameGlasshouse.unity"
DONOR = {
    "GlasshouseController":      guid("script/GlasshouseController"),
    "GlasshouseTimeTurnMonitor": guid("script/GlasshouseTimeTurnMonitor"),
    "GlasshouseSettings":        guid("asset/GlasshouseSettings"),
    "GlasshouseScoringRule":     guid("asset/GlasshouseScoringRule"),
}
DONOR_CELLS = [guid(f"asset/GlasshouseCellConfig{i}") for i in (1, 2, 3, 4)]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]

MEMBRANE_FILEID = 346633111830028674
NUCLEUS_FILEID = 7555898194514117247
CYTOPLASM_FILEID = 639495419069806261
FAUNA_FILEID = 5945480239701989318

# ── The model: the reef (a twin of ChumReef, checked against the C# below) ──
MEMBRANE_RADIUS = 1200.0
NUCLEUS_RADIUS = 392.0           # Barren's nucleus at prefab scale (Docs/ECOSYSTEM.md 13)
PRISM_SIZE = 7.0
SPACING = 15.0
RUNGS = [
    dict(i=1, label="Shallows", shoals=8, shells=2, band=0.30, crusted=False, omni=4, herdPoints=2,
         blurb="eight small shoals close to the core - room to learn the paint and the throw"),
    dict(i=2, label="Reef", shoals=12, shells=2, band=0.45, crusted=False, omni=3, herdPoints=3,
         blurb="twelve shoals farther out, and the grazers eat them too"),
    dict(i=3, label="Crusted", shoals=12, shells=3, band=0.45, crusted=True, omni=3, herdPoints=4,
         blurb="bigger shoals under a shielded crust no grazer can eat - the Angler starts at Space 5, whose Paint "
               "Shields cracks it"),
    dict(i=4, label="Barrier", shoals=16, shells=3, band=0.60, crusted=True, omni=2, herdPoints=5,
         blurb="sixteen crusted shoals out by the membrane, two crystals and fifteen grazers racing you"),
]
HERDS_PER_POINT = 3
SPAWN_RING = 450
SPACE5 = 0.5                     # a StartingElements level is level / 10 (Brittle Star Time's Time: 0.5 = Time 5)


def shell_count(s):
    return 1 if s <= 0 else max(6, int(math.floor(4 * math.pi * s * s + 0.5)))


def spiral(i, n):
    golden = 2.39996323
    n = max(1, n)
    y = 1 - 2 * (i + 0.5) / n
    ring = math.sqrt(max(0.0, 1 - y * y))
    th = i * golden
    return (ring * math.cos(th), y, ring * math.sin(th))


def lay(r):
    """The twin of ChumReef.Lay: [(x, y, z, shoal, shielded)]."""
    out = []
    shoal_r = r["shells"] * SPACING + PRISM_SIZE
    inner = NUCLEUS_RADIUS + shoal_r
    outer = max(inner, MEMBRANE_RADIUS - shoal_r)
    rad = inner + (outer - inner) * r["band"]
    for s in range(r["shoals"]):
        c = tuple(v * rad for v in spiral(s, r["shoals"]))
        out.append((*c, s, False))
        for shell in range(1, r["shells"] + 1):
            n = shell_count(shell)
            crust = r["crusted"] and shell == r["shells"]
            for k in range(n):
                d = spiral(k, n)
                out.append((c[0] + d[0] * shell * SPACING, c[1] + d[1] * shell * SPACING,
                            c[2] + d[2] * shell * SPACING, s, crust))
    return out


for r in RUNGS:
    r["pieces"] = lay(r)
    r["prisms"] = len(r["pieces"])
    r["crust"] = sum(1 for p in r["pieces"] if p[4])

# The Angler's trail (Angler.prefab's prism spawner): 20 x 0.75 x 5.5 every 7 u at 40 u/s cruise.
TRAIL_PRISM = 20 * 0.75 * 5.5
TRAIL_PRISMS_PER_SECOND = 40.0 / 7.0
PILOTS = 4
REEF_PRISM = PRISM_SIZE ** 3

# Comeback - a FUNCTION OF THE REFERENCE MATCH (timed: the "target" is a reference domain total): a domain eating a
# quarter of the I2 reef in the round. A quarter of that behind buys >= 1 element level.
REFERENCE_DOMAIN_PRISMS = RUNGS[1]["prisms"] // 4
COMEBACK_RATE = 0.025


def ladder(r):
    """PhaseThresholds for a rung: the arena only LOSES mass after the start (the reef is eaten, nothing is planted),
    so Frenzy sits 25 % above the most it can hold (the whole reef plus four pilots' whole-round trail) and never
    fires; Restless at 7 % of it."""
    trail_prisms = PILOTS * ROUND_SECONDS * TRAIL_PRISMS_PER_SECOND
    count = r["prisms"] + trail_prisms
    volume = r["prisms"] * REEF_PRISM + trail_prisms * TRAIL_PRISM
    fe, fev = round(count * 1.25), round(volume * 1.25)
    return dict(RestlessEnter=round(fe * 0.07), RestlessExit=round(fe * 0.05), FrenzyEnter=fe,
                FrenzyExit=round(fe * 0.85), RestlessEnterVolume=round(fev * 0.07),
                RestlessExitVolume=round(fev * 0.05), FrenzyEnterVolume=fev, FrenzyExitVolume=round(fev * 0.85),
                maxCount=round(count), maxVolume=round(volume))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameChum.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Chum")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/CHUM.md")

# ── 2. Scoring rule (timed, highest wins, PrismsDestroyed) ───────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/ChumScoringRule.asset", G_ASSET["ChumScoringRule"],
             lib.header_for(G_SCRIPT["ChumScoringRuleSO"], "ChumScoringRule") +
             """  metric: 5
  golfRules: 0
  prismsTemplate: '{count} Prisms'
  eatenTemplate: '{count} eaten'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: PRISMS TAKEN
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiSchoolTarget":    ([2, 3, 4, 5], "up"),
    "aiLureHoldSeconds": ([2, 2.5, 3, 3.5], "up"),
    "aiCrystalReach":    ([700, 1100, 1500, 2000], "up"),
    "aiRetargetSeconds": ([9, 7, 5, 4], "down"),
}
SHARED_1 = {"attributeRadius": 90, "leadAnnounceAfterSeconds": 15, "leadAnnounceMinGapSeconds": 8,
            "leadSampleSeconds": 1}
SHARED_2 = {"aiLureDistance": 70, "aiStandOff": 220}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/ChumSettings.asset", G_ASSET["ChumSettings"],
             lib.header_for(G_SCRIPT["ChumSettingsSO"], "ChumSettings")
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED_1.items())
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED_2.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 1: a feed against the arena - the wild grazers eat the reef from a lone Angler too.
DESCRIPTION = ("Anglers only - a timed feed on a reef. Shoals of prisms hang round the core. Paint a shoal and let go, "
               "and half your school is thrown at it and eats it, then the other half. Every omni crystal is one more "
               "creature, and a grazer held in your lure light joins you too. The most prisms taken off the board when "
               "the clock runs out wins. Higher intensities put the shoals farther out, under a shielded crust only "
               "Paint Shields cracks, with more wild grazers racing you for them.")
STARTING = "".join(f"""  - Class: {ANGLER_ID}
    Intensity: {r['i']}
    Levels:
      Mass: 0
      Charge: 0
      Space: {lib.num(SPACE5)}
      Time: 0
""" for r in RUNGS if r["crusted"])
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameChum"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameChum") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Chum
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameChum
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Angler']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  StartingElements:
{STARTING}  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Chum.asset", G_ASSET["GameToastConfigChum"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Chum") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} prisms taken", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Hold RT to paint a shoal and let go to throw your school at it - omni crystals "
                       "and your lure tame more", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigChum"])

# ── 6. The arena: herd, spawn profile and cell config per rung ───────────────
tadpole_donor = g.read(TADPOLE_DONOR)
for r in RUNGS:
    i = r["i"]
    herd = r["herdPoints"] * HERDS_PER_POINT
    body = tadpole_donor
    for field, value in (("InitialSpawnCount", herd), ("PopulationSize", herd), ("MaxLivePopulation", herd + herd // 2)):
        body, n = re.subn(rf"^  {field}: \d+$", f"  {field}: {value}", body, flags=re.M)
        assert n == 1, f"{field} not found once in {TADPOLE_DONOR}"
    fields = body.split("  m_EditorClassIdentifier:\n", 1)[1]
    g.emit_asset(f"{CELL_DIR}/Chum I{i} Tadpole Fauna Config Data.asset", G_ASSET[f"ChumTadpole{i}"],
                 lib.header_for(EXISTING["FaunaConfigurationSO"], f"Chum I{i} Tadpole Fauna Config Data") + fields)

    g.emit_asset(f"{CELL_DIR}/Chum Spawn Profile {i}.asset", G_ASSET[f"ChumSpawnProfile{i}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"Chum Spawn Profile {i}") + f"""  FloraExcludeLocalDomain: 0
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
  HerbivoreSpawnPointCount: {r['herdPoints']}
  HerbivoreSpawnRadius: {round(NUCLEUS_RADIUS + 0.4 * (MEMBRANE_RADIUS - NUCLEUS_RADIUS))}
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas:
  - {{fileID: 11400000, guid: {G_ASSET[f"ChumTadpole{i}"]}, type: 2}}
""")

    th = ladder(r)
    r["ladder"] = th
    desc = (f"{r['label']} - intensity {i} of Chum: {r['blurb']}. The standard {int(MEMBRANE_RADIUS)} u cell with its "
            f"nucleus (~{int(NUCLEUS_RADIUS)} u). No environment prefab - the reef ({r['shoals']} shoals, "
            f"{r['prisms']:,} prisms of {int(PRISM_SIZE)} u, {r['crust']:,} of them shielded crust) is laid by "
            f"ChumController. {herd} tameable tadpoles graze it outside the nucleus; {r['omni']} omni crystals respawn "
            f"inside it (the scene's CrystalManager). Frenzy sits 25% above the full arena ({th['maxCount']:,} prisms, "
            f"{th['maxVolume']:,} volume). Authored by Tools/Build/author_chum_assets.py - re-run it rather than "
            f"hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Chum Cell Config {i}.asset", G_ASSET[f"ChumCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Chum Cell Config {i}") + f"""  CellName: Chum
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS_FILEID}, guid: {EXISTING['Nucleus_prefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'ChumSpawnProfile{i}']}, type: 2}}
  EnvironmentPrefab: {{fileID: 0}}
  EnvironmentIntensity: {i}
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {th['RestlessEnter']}
    RestlessExit: {th['RestlessExit']}
    FrenzyEnter: {th['FrenzyEnter']}
    FrenzyExit: {th['FrenzyExit']}
    RestlessEnterVolume: {th['RestlessEnterVolume']}
    RestlessExitVolume: {th['RestlessExitVolume']}
    FrenzyEnterVolume: {th['FrenzyEnterVolume']}
    FrenzyExitVolume: {th['FrenzyExitVolume']}
""")

CELLS = [G_ASSET[f"ChumCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {r['omni']}\n" for r in RUNGS))
DONOR_OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
              "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 4\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 3\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 3\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 2\n")
MONITOR_DONOR = (f"  m_Script: {{fileID: 11500000, guid: {DONOR['GlasshouseTimeTurnMonitor']}, type: 3}}\n"
                 "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
                 f"  duration: 90\n")
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['ChumTimeTurnMonitor']}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
           f"  duration: {ROUND_SECONDS}\n")
CONTROLLER_DONOR_TAIL = (f"  settings: {{fileID: 11400000, guid: {DONOR['GlasshouseSettings']}, type: 2}}\n"
                         f"  rule: {{fileID: 11400000, guid: {DONOR['GlasshouseScoringRule']}, type: 2}}\n"
                         "  arenaCell: {fileID: 1700000065}\n")
CONTROLLER_TAIL = (f"  settings: {{fileID: 11400000, guid: {G_ASSET['ChumSettings']}, type: 2}}\n"
                   f"  rule: {{fileID: 11400000, guid: {G_ASSET['ChumScoringRule']}, type: 2}}\n"
                   "  arenaCell: {fileID: 1700000065}\n"
                   f"  reefPrefab: {{fileID: {SPAWNABLE_PRISM_FILEID}, guid: {EXISTING['SpawnablePrism']}, type: 3}}\n"
                   f"  nucleusRadius: {int(NUCLEUS_RADIUS)}\n"
                   f"  membraneRadius: {int(MEMBRANE_RADIUS)}\n")

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Chum.asset", G_ASSET["ModePreviewChum"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Chum") + f"""  Mode: {MODE_ID}
  Notes: 'The reef cell at each intensity. OPEN-ENDED - the reef is laid by ChumController at match start,
    so a preview has none - it teaches the brush, the throw and the lure. Vessel pinned to Angler({ANGLER_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {ANGLER_ID}
  ObjectiveText: Paint a shoal and let go to throw your school at it - tame more with omni crystals and the lure
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {SPAWN_RING}\n" for _ in RUNGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewChum"])

# ── 8. Scene: Glasshouse's, with the identity, the monitor, the cells, the crystals and the reef fields swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.replace_block(scene, CONTROLLER_DONOR_TAIL, CONTROLLER_TAIL, "controller fields")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["ChumController"], "controller")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "omni crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", OMNI, MONITOR,
                   CONTROLLER_TAIL]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameChum.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameChum", G_ASSET["MinigameChum.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameChum"])
g.register_always_unlocked()
g.register_build_scene("MinigameGlasshouse", "MinigameChum", G_ASSET["MinigameChum.unity"])
g.set_end_condition("chumRoundSeconds", after="glasshouseRoundSeconds", value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * REFERENCE_DOMAIN_PRISMS * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead: a quarter of the reference domain ({REFERENCE_DOMAIN_PRISMS} "
                  f"prisms) behind buys {0.25 * REFERENCE_DOMAIN_PRISMS * COMEBACK_RATE:.2f} element levels (< 1)")

# The twin and the C# agree: the ladder table, the constants and the shell formula.
reef_cs = g.read(SCRIPT_PATHS["ChumReef"])
for r in RUNGS:
    row = (f"{r['i'] if r['i'] < 4 else '_'} => new Rung({r['shoals']}, {r['shells']}, {r['band']:.2f}f, "
           f"{'true' if r['crusted'] else 'false'}),")
    if row not in reef_cs:
        errors.append(f"ChumReef.ForIntensity no longer has '{row}' - the generator's RUNGS and the C# disagree")
for needle in (f"PrismSize = {lib.num(PRISM_SIZE)}f;", f"Spacing = {lib.num(SPACING)}f;",
               "Mathf.Max(6, Mathf.RoundToInt(4f * Mathf.PI * shell * shell))", "const float golden = 2.39996323f;"):
    if needle not in reef_cs:
        errors.append(f"ChumReef no longer has '{needle}' - re-derive the twin")

# No two reef prisms touch: axis-aligned cubes of PRISM_SIZE overlap only when EVERY axis is closer than the side.
# Shoals are far apart (checked below), so a pass per shoal is enough.
for r in RUNGS:
    first = [p for p in r["pieces"] if p[3] == 0]
    worst = float("inf")
    for a in range(len(first)):
        for b in range(a + 1, len(first)):
            cheb = max(abs(first[a][k] - first[b][k]) for k in range(3))
            worst = min(worst, cheb)
    if worst <= PRISM_SIZE + 0.5:
        errors.append(f"I{r['i']}: two reef prisms of a shoal come within {worst:.2f} u (Chebyshev) - they would touch")
    centres = [p for p in r["pieces"] if p[3] != -1 and p == [q for q in r["pieces"] if q[3] == p[3]][0]]
    shoal_r = r["shells"] * SPACING + PRISM_SIZE
    for a in range(len(centres)):
        for b in range(a + 1, len(centres)):
            d = math.dist(centres[a][:3], centres[b][:3])
            if d < 2 * shoal_r + 40:
                errors.append(f"I{r['i']}: shoals {a} and {b} are {d:.0f} u apart - closer than two shoals and a gap")
    for p in r["pieces"]:
        d = math.dist(p[:3], (0, 0, 0))
        if d < NUCLEUS_RADIUS + 1 or d > MEMBRANE_RADIUS - 1:
            errors.append(f"I{r['i']}: a reef prism sits {d:.0f} u from the centre, outside the band between the nucleus "
                          f"and the membrane")
            break
    if r["prisms"] > 3500:
        errors.append(f"I{r['i']}: {r['prisms']} reef prisms - keep the reef under the collider budget")

# The load-bearing assumptions about the Angler and the ecology. A retune that breaks any of these breaks the mode.
executor = g.read(ANGLER_EXECUTOR)
for needle in ("public sealed class AnglerCommandExecutor : ShipActionExecutorBase, IFaunaLeash",
               "public int TamedCount", "public void BeginLure()",
               "if (paint && config.SpareOwn && p.Domain == own) continue;"):
    if needle not in executor:
        errors.append(f"AnglerCommandExecutor no longer has '{needle}' - Chum credits a bite to the creature's leash "
                      f"(the Angler) and paints the Blue reef as an enemy; re-check the Angler")
prefab = g.read(ANGLER_PREFAB)
if ("  - InputEvent: 6\n    ShipActions:\n    - {fileID: 11400000, guid: " + EXISTING["AnglerLure"]) not in prefab:
    errors.append("Angler.prefab no longer binds Button1Action (6) to AnglerLure - the AI forager presses "
                  "ChumForager.LureInput")
if "  control: 0\n" not in g.read(ANGLER_LURE):
    errors.append("AnglerLure.asset is no longer AnglerControl.Lure")
if "BaseScale: {x: 20, y: 0.75, z: 5.5}" not in prefab or "initialWavelength: 7" not in prefab:
    errors.append("the Angler's trail changed (BaseScale or wavelength) - re-derive TRAIL_PRISM and the phase model")
if 'const string PLAYER_NAME = "light FaunaPrefab";' not in g.read(LIGHT_FAUNA):
    errors.append("LightFauna no longer eats as an unnamed creature - if a bite now names its keeper, StatsManager "
                  "credits it and ChumController would count it twice")
if "public bool DestroyedByCreature => _destroyedByCreature;" not in g.read(PRISM_CS):
    errors.append("Prism.DestroyedByCreature is gone - ChumController tells a bite from a ram by it")
if "public IFaunaLeash Leash { get; private set; }" not in g.read(FAUNA_CS):
    errors.append("Fauna.Leash is no longer public - ChumController credits a bite through the creature's leash")
if "public IReadOnlyList<Fauna> LiveFauna => liveFauna;" not in g.read(CELL_CS):
    errors.append("Cell.LiveFauna is gone - ChumController finds the biting creature among the cell's live fauna")
if f"FaunaPrefab: {{fileID: {FAUNA_FILEID}, guid: {EXISTING['Tadpole_prefab']}, type: 3}}" not in tadpole_donor:
    errors.append("the donor tadpole config no longer names the tadpole prefab")
if "    Forager: 1\n" not in tadpole_donor:
    errors.append("the donor tadpole is no longer a forager - it would not graze the reef")
if f"m_Script: {{fileID: 11500000, guid: " not in g.read(SPAWNABLE_PRISM_PATH) or \
        f"--- !u!114 &{SPAWNABLE_PRISM_FILEID}" not in g.read(SPAWNABLE_PRISM_PATH):
    errors.append(f"SpawnablePrism.prefab no longer carries its Prism component at fileID {SPAWNABLE_PRISM_FILEID}")

# Enum ids, the default round and the provider.
toasts_cs = g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
if f"ChumHint = {TOAST_HINT}," not in toasts_cs:
    errors.append(f"GameToastSituation.ChumHint is not {TOAST_HINT}")
if f"DomainRaceLeadChanged = {TOAST_LEAD}," not in toasts_cs:
    errors.append(f"GameToastSituation.DomainRaceLeadChanged is not {TOAST_LEAD}")
if f"Chum = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Chum is not {MODE_ID}")
if f"DefaultChumRoundSeconds = {ROUND_SECONDS};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultChumRoundSeconds differs from ROUND_SECONDS")
if "case GameModes.Chum:" not in g.read("Assets/_Scripts/UI/MiniGameHUD.cs"):
    errors.append("MiniGameHUD has no objective provider case for Chum")
if "case ScoringMetric.PrismsDestroyed:" not in g.read("Assets/_Scripts/Data/ModeGenre.cs"):
    errors.append("PrismsDestroyed no longer reads as a Space metric - the card would lose its petal")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["ChumSettingsSO"])
for name, (values, harder) in LADDERS.items():
    pairs = list(zip(values, values[1:]))
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for field, value in {**SHARED_1, **SHARED_2}.items():
    if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
        errors.append(f"ChumSettingsSO.{field} initializer differs from the authored {value}")
# The forager reads the seat's difficulty, never the intensity, and presses only the lure.
forager = g.read(SCRIPT_PATHS["ChumForager"])
if "SelectedIntensity" in forager or "AIDifficultyFor(" not in forager:
    errors.append("ChumForager must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
if "TriggerAnalog" in forager or "InputStatus" in forager \
        or forager.count("ShipControllerActionsReplicated(LureInput)") != 2:
    errors.append("ChumForager presses something other than the lure - the hull's leapfrog policy owns the paint and "
                  "the throw")
# The controller adds the school's meals ONLY; it never overwrites the platform's count during play.
controller = g.read(SCRIPT_PATHS["ChumController"])
if "stats.HostilePrismsDestroyed += 1;" not in controller \
        or re.search(r"HostilePrismsDestroyed = (?!0;|prisms\[i\];)", controller):
    errors.append("ChumController must add a school's meal to HostilePrismsDestroyed and never assign it mid-match "
                  "(StatsManager owns the hull's count)")
if "MantaBombRules" in controller or "R_VesselActions" in controller:
    errors.append("ChumController reaches into the vessel - a mode never changes the vessel (LOCKED)")

# The arena's own numbers.
for name, values, direction in (("omni crystals", [r["omni"] for r in RUNGS], "down"),
                                ("grazers", [r["herdPoints"] for r in RUNGS], "up"),
                                ("shoals", [r["shoals"] for r in RUNGS], "up"),
                                ("reef band", [r["band"] for r in RUNGS], "up")):
    pairs = list(zip(values, values[1:]))
    if direction == "up" and any(b < a for a, b in pairs) or direction == "down" and any(b > a for a, b in pairs):
        errors.append(f"the {name} ladder must run {direction} with intensity ({values})")
for r in RUNGS:
    th = r["ladder"]
    if th["FrenzyEnterVolume"] <= th["maxVolume"] or th["FrenzyEnter"] <= th["maxCount"]:
        errors.append(f"I{r['i']}: Frenzy would fire on the full arena")
    if not th["RestlessEnterVolume"] > th["RestlessExitVolume"] or not th["FrenzyEnterVolume"] > th["FrenzyExitVolume"]:
        errors.append(f"I{r['i']}: phase hysteresis inverted")
if not (NUCLEUS_RADIUS < SPAWN_RING < MEMBRANE_RADIUS - 300):
    errors.append(f"spawn ring {SPAWN_RING} must sit just outside the nucleus")
if any(r["crusted"] for r in RUNGS) and "Space: 0.5" not in STARTING:
    errors.append("a crusted rung must start the Angler at Space 5 (Paint Shields cracks the crust)")

# The scene: every new identity present, every donor one gone, four Angler AIs, the timed monitor.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("ChumController", "ChumTimeTurnMonitor")},
                     "ChumSettings": G_ASSET["ChumSettings"], "ChumScoringRule": G_ASSET["ChumScoringRule"],
                     "SpawnablePrism": EXISTING["SpawnablePrism"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Glasshouse cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {ANGLER_ID}\n") != 4:
        errors.append("scene does not carry exactly 4 Angler AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Chum"), guid(f"folder/{CELL_DIR}"), guid(f"text/{AR}/CHUM.md")])

if __name__ == "__main__" and "--check" not in sys.argv:
    for r in RUNGS:
        print(f"  I{r['i']} {r['label']}: {r['shoals']} shoals, {r['prisms']} prisms ({r['crust']} crust)")
