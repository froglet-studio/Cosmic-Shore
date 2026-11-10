#!/usr/bin/env python3
"""
Authors every serialized asset the Glasshouse game mode needs (GameModes.Glasshouse = 89).

Glasshouse is the Angler's MASS game (Docs/MassAndSpaceGames/PLAN.md, wave 1): a timed garden. An omni crystal is a
creature and a SEED for an Angler (AnglerTameByCrystalEffectSO); letting go of the lure plants every seed it carries in
the pilot's colour (AnglerCommandExecutor.PlantSeeds), and holding the lure grows every plant in its light three times
as fast (HastenGrowth). The score is the mass standing in a domain's colour at the whistle (VolumeRemaining): its
Anglers' trail, counted by StatsManager as in every mode, plus the garden grown from its seeds outside the nucleus,
credited by GlasshouseController. Wild herds graze every garden outside the nucleus; a rival's paint throws their
school at yours.

THE LADDER (one cell config, spawn profile, herd and flora config per intensity; numbers in RUNGS):
  I1 Seed Tray   - four seeds out at once, six grazers, room for 36 plants.
  I2 Allotment   - three seeds, nine grazers, room for 30.
  I3 Meadow      - three seeds, twelve grazers, room for 24.
  I4 Overgrazed  - two seeds, fifteen grazers, room for 18.
The plant cap is the cell's, shared by every colour, so a full arena is a contested one.

NOTHING ABOUT THE ANGLER CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the cell (a
species planted ONLY from seeds - its flora configs have SpawnProbability 0, so the cell plants nothing wild and the
no-domain-asymmetry invariant is untouched - its herds, its cap and its prism scale), the scene's omni crystals per
intensity, and the AI gardeners. The scene is cloned from Kelpwood's (the Angler's Charge game: Angler AI templates, a
CrystalManager authored per intensity, a controller declaring settings / rule / arenaCell), with Kelpwood's combat
monitor swapped for a timed one.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_glasshouse_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 89
TOAST_HINT = 164
TOAST_LEAD = 130          # DomainRaceLeadChanged, posted by the controller's own lead sampler (a timed mode has no target)
ROUND_SECONDS = 90        # Docs/ARCADE_MATCH_LENGTH.md; kept in sync with EndConditionOverridesSO.DefaultGlasshouseRoundSeconds
g = lib.Generator(MODE_ID, "Glasshouse")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "GlasshouseController":      f"{AR}/Glasshouse/GlasshouseController.cs",
    "GlasshouseSettingsSO":      f"{AR}/Glasshouse/GlasshouseSettingsSO.cs",
    "GlasshouseScoringRuleSO":   f"{AR}/Glasshouse/GlasshouseScoringRuleSO.cs",
    "GlasshouseGardener":        f"{AR}/Glasshouse/GlasshouseGardener.cs",
    "GlasshouseGarden":          f"{AR}/Glasshouse/GlasshouseGarden.cs",
    "GlasshouseTimeTurnMonitor": f"{AR}/TurnMonitors/GlasshouseTimeTurnMonitor.cs",
    "GlasshouseScoringTests":    "Assets/_Scripts/Tests/Editor/GlasshouseScoringTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Glasshouse Cell"
G_ASSET = {
    "ArcadeGameGlasshouse":      guid("asset/ArcadeGameGlasshouse"),
    "GlasshouseScoringRule":     guid("asset/GlasshouseScoringRule"),
    "GlasshouseSettings":        guid("asset/GlasshouseSettings"),
    "GameToastConfigGlasshouse": guid("asset/GameToastConfigGlasshouse"),
    "ModePreviewGlasshouse":     guid("asset/ModePreviewGlasshouse"),
    "MinigameGlasshouse.unity":  guid("asset/MinigameGlasshouse.unity"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"GlasshouseCellConfig{i}"] = guid(f"asset/GlasshouseCellConfig{i}")
    G_ASSET[f"GlasshouseSpawnProfile{i}"] = guid(f"asset/GlasshouseSpawnProfile{i}")
    G_ASSET[f"GlasshouseTadpole{i}"] = guid(f"asset/GlasshouseTadpole{i}")
    G_ASSET[f"GlasshouseCactus{i}"] = guid(f"asset/GlasshouseCactus{i}")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Angler"] = "ea422bbe0d1aef61993692999cee50ea"           # SO_Class_Angler.asset
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell
EXISTING["Nucleus_prefab"] = "b9cf1833fa2493d4b8724ccb6740fb3a"          # Barren's nucleus (~392 u radius)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
EXISTING["FaunaConfigurationSO"] = "c778cfbe4dfc4c5c8401e40c17802311"
EXISTING["Tadpole_prefab"] = "c7fd418d426de8740ac888dcc23a5d24"
EXISTING["CactiFlora_prefab"] = "82e04cfd7f5b93f478b5fbfe40c2a2d7"       # CactiFlora.prefab (BranchingFlora)
EXISTING["AnglerLure"] = "c329dd299612fe08ed9840cd879c0734"              # AnglerLure.asset (AnglerControl.Lure)
EXISTING["BranchingFlora_script"] = "348a716a2db52944888716c19b4d6803"
# The herd: a Glasshouse-owned copy of the Thresher Time I1 tadpole (Kelpwood's donor too), so tuning the herds
# re-tunes no other mode. Tadpoles are herbivores (they graze the gardens) and in the Angler's omniFauna: tameable.
TADPOLE_DONOR = "Assets/_SO_Assets/Cell Configs/Thresher Time Cell/Thresher Time I1 Tadpole Fauna Config Data.asset"
# The garden: a Glasshouse-owned copy of Rampage's cactus (a branching flora - every prism laid at its leaf size, so a
# plant's volume is exactly prisms x leaf - spread over all four elements, 160 prisms a plant). Copied, never shared:
# the copy plants nothing wild and carries this arena's cap.
CACTUS_DONOR = "Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cacti Flora Config Data.asset"
ANGLER_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/AnglerCommandExecutor.cs"
ANGLER_PREFAB = "Assets/_Prefabs/Spacevessels/Angler.prefab"
ANGLER_LURE = "Assets/_SO_Assets/VesselActions/Angler/AnglerLure.asset"
FLORA_CS = "Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs"
SPAWNER_CS = "Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs"
CACTI_PREFAB = "Assets/_Prefabs/FloraAndFauna/CactiFlora.prefab"
# The donor scene's identity, minted by author_kelpwood_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameKelpwood.unity"
DONOR = {
    "KelpwoodController":      guid("script/KelpwoodController"),
    "KelpwoodBiteTurnMonitor": guid("script/KelpwoodBiteTurnMonitor"),
    "KelpwoodSettings":        guid("asset/KelpwoodSettings"),
    "KelpwoodScoringRule":     guid("asset/KelpwoodScoringRule"),
}
DONOR_CELLS = [guid(f"asset/KelpwoodCellConfig{i}") for i in (1, 2, 3, 4)]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]

MEMBRANE_FILEID = 346633111830028674
NUCLEUS_FILEID = 7555898194514117247
CYTOPLASM_FILEID = 639495419069806261
FAUNA_FILEID = 5945480239701989318
FLORA_FILEID = 7514956980722975813

# ── The model: what a garden and a trail are worth ───────────────────────────
MEMBRANE_RADIUS = 1200.0
NUCLEUS_RADIUS = 392.0           # Barren's nucleus at prefab scale (Docs/ECOSYSTEM.md §13)
PLANT_PRISMS = 160               # the donor's MaxTotalSpawnedObjectsOverride: a mature cactus
CACTUS_LEAF = 5 * 5 * 3          # CactiFlora.prefab leafSize (asserted below)
PRISM_SCALE = 1.4                # the cell's FloraPrismScale: Rampage I1's chunkier leaf (a branching leaf scales as s^3)
PLANT_VOLUME = PLANT_PRISMS * CACTUS_LEAF * PRISM_SCALE ** 3
# The Angler's trail (Angler.prefab's prism spawner): 20 x 0.75 x 5.5 every 7 u at 40 u/s cruise.
TRAIL_PRISM = 20 * 0.75 * 5.5
TRAIL_PRISMS_PER_SECOND = 40.0 / 7.0
PILOTS = 4

RUNGS = [
    dict(i=1, label="Seed Tray", omni=4, herdPoints=2, cap=36,
         blurb="four seeds out at once and few grazers - room to learn planting and tending"),
    dict(i=2, label="Allotment", omni=3, herdPoints=3, cap=30,
         blurb="three seeds and more grazers - a garden left alone starts to thin"),
    dict(i=3, label="Meadow", omni=3, herdPoints=4, cap=24,
         blurb="twelve grazers and less room - tending and raiding both matter"),
    dict(i=4, label="Overgrazed", omni=2, herdPoints=5, cap=18,
         blurb="two seeds, fifteen grazers and room for eighteen plants between every colour"),
]
HERDS_PER_POINT = 3
SPAWN_RING = 450                 # just outside the nucleus, inside the garden band

# Comeback - a FUNCTION OF THE REFERENCE MATCH (the mode is timed, so the "target" is a reference domain total):
# one pilot cruising the whole round plus five mature plants. A quarter of that behind buys >= 1 element level.
REFERENCE_DOMAIN_VOLUME = ROUND_SECONDS * TRAIL_PRISMS_PER_SECOND * TRAIL_PRISM + 5 * PLANT_VOLUME
COMEBACK_RATE = 0.00003


def ladder(r):
    """PhaseThresholds for a rung: Frenzy (which freezes planting AND growth) sits 25 % above the most the arena can
    hold - every plant the cap allows at maturity plus four pilots' whole-round trail - so the garden never freezes
    short of the cap; Restless at 7 % of it (the fauna start hunting a partly grown arena)."""
    trail_prisms = PILOTS * ROUND_SECONDS * TRAIL_PRISMS_PER_SECOND
    count = r["cap"] * PLANT_PRISMS + trail_prisms
    volume = r["cap"] * PLANT_VOLUME + trail_prisms * TRAIL_PRISM
    fe, fev = round(count * 1.25), round(volume * 1.25)
    return dict(RestlessEnter=round(fe * 0.07), RestlessExit=round(fe * 0.05), FrenzyEnter=fe,
                FrenzyExit=round(fe * 0.85), RestlessEnterVolume=round(fev * 0.07),
                RestlessExitVolume=round(fev * 0.05), FrenzyEnterVolume=fev, FrenzyExitVolume=round(fev * 0.85),
                maxCount=round(count), maxVolume=round(volume))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameGlasshouse.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Glasshouse")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/GLASSHOUSE.md")

# ── 2. Scoring rule (timed, highest wins, VolumeRemaining) ───────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/GlasshouseScoringRule.asset", G_ASSET["GlasshouseScoringRule"],
             lib.header_for(G_SCRIPT["GlasshouseScoringRuleSO"], "GlasshouseScoringRule") +
             """  metric: 12
  golfRules: 0
  volumeTemplate: '{count} Volume'
  gardenTemplate: '{count} grown'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME STANDING
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiLureHoldSeconds": ([2.5, 3.5, 4.5, 5.5], "up"),
    "aiRaidAtSchool":    ([99, 6, 4, 3], "down"),
    "aiRaidSeconds":     ([0, 6, 9, 12], "up"),
    "aiSeedReach":       ([900, 1400, 2000, 2600], "up"),
}
SHARED = {"gardenSampleSeconds": 1, "leadAnnounceAfterSeconds": 15, "leadAnnounceMinGapSeconds": 8,
          "aiGardenBandFraction": 0.35, "aiArriveDistance": 60, "aiTendIntervalSeconds": 12}
SHARED_ORDER_1 = ["gardenSampleSeconds", "leadAnnounceAfterSeconds", "leadAnnounceMinGapSeconds"]
SHARED_ORDER_2 = ["aiGardenBandFraction", "aiArriveDistance", "aiTendIntervalSeconds"]


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/GlasshouseSettings.asset", G_ASSET["GlasshouseSettings"],
             lib.header_for(G_SCRIPT["GlasshouseSettingsSO"], "GlasshouseSettings")
             + "".join(f"  {k}: {lib.num(float(SHARED[k]))}\n" for k in SHARED_ORDER_1)
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(SHARED[k]))}\n" for k in SHARED_ORDER_2))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 1: a garden race against the arena - the grazers take mass from a lone gardener too.
DESCRIPTION = ("Anglers only - a timed garden. Every omni crystal is a creature and a seed. Let go of the lure and "
               "the seed is planted in your colour, and the lure light grows every plant in reach three times as fast. "
               "Your score is the mass standing in your colour when the clock runs out - your trail and your garden. "
               "Wild herds graze every garden outside the core, and a rival paints your plants to throw a school at "
               "them. Higher intensities thin the seeds, bring more grazers and leave less room to grow.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameGlasshouse"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameGlasshouse") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Glasshouse
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameGlasshouse
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
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Glasshouse.asset", G_ASSET["GameToastConfigGlasshouse"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Glasshouse") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} volume standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Take an omni crystal for a seed, let go of the lure to plant it, and hold the lure "
                       "over your plants to grow them", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigGlasshouse"])

# ── 6. The arena: herd, garden species, spawn profile and cell config per rung ──
tadpole_donor = g.read(TADPOLE_DONOR)
cactus_donor = g.read(CACTUS_DONOR)
for r in RUNGS:
    i = r["i"]
    herd = r["herdPoints"] * HERDS_PER_POINT
    body = tadpole_donor
    for field, value in (("InitialSpawnCount", herd), ("PopulationSize", herd), ("MaxLivePopulation", herd + herd // 2)):
        body, n = re.subn(rf"^  {field}: \d+$", f"  {field}: {value}", body, flags=re.M)
        assert n == 1, f"{field} not found once in {TADPOLE_DONOR}"
    fields = body.split("  m_EditorClassIdentifier:\n", 1)[1]
    g.emit_asset(f"{CELL_DIR}/Glasshouse I{i} Tadpole Fauna Config Data.asset", G_ASSET[f"GlasshouseTadpole{i}"],
                 lib.header_for(EXISTING["FaunaConfigurationSO"], f"Glasshouse I{i} Tadpole Fauna Config Data")
                 + fields)

    # The garden species: planted ONLY from seeds (SpawnProbability 0, no initial batch, no seeder floor), and
    # reproducing in its parent's colour up to this rung's cap - the room every colour's garden shares.
    body = cactus_donor
    for field, value in (("SpawnProbability", 0), ("InitialSpawnCount", 0), ("PopulationSize", 0),
                         ("MaxLivePopulation", r["cap"])):
        body, n = re.subn(rf"^  {field}: [\d.]+$", f"  {field}: {value}", body, flags=re.M)
        assert n == 1, f"{field} not found once in {CACTUS_DONOR}"
    fields = body.split("  m_EditorClassIdentifier: \n", 1)[1] if "  m_EditorClassIdentifier: \n" in body \
        else body.split("  m_EditorClassIdentifier:\n", 1)[1]
    g.emit_asset(f"{CELL_DIR}/Glasshouse I{i} Cactus Flora Config Data.asset", G_ASSET[f"GlasshouseCactus{i}"],
                 lib.header_for(EXISTING["FloraConfigurationSO"], f"Glasshouse I{i} Cactus Flora Config Data")
                 + fields)

    g.emit_asset(f"{CELL_DIR}/Glasshouse Spawn Profile {i}.asset", G_ASSET[f"GlasshouseSpawnProfile{i}"],
                 lib.header_for(EXISTING["SpawnProfileSO"], f"Glasshouse Spawn Profile {i}") + f"""  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  FloraPopulationScale: 1
  FloraPlantBudgetScale: 1
  FloraPrismScale: {lib.num(PRISM_SCALE)}
  SupportedFloras:
  - {{fileID: 11400000, guid: {G_ASSET[f"GlasshouseCactus{i}"]}, type: 2}}
  FaunaExcludeLocalDomain: 1
  InitialFaunaSpawnWaitTime: 10
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
  - {{fileID: 11400000, guid: {G_ASSET[f"GlasshouseTadpole{i}"]}, type: 2}}
""")

    th = ladder(r)
    r["ladder"] = th
    desc = (f"{r['label']} - intensity {i} of Glasshouse: {r['blurb']}. The standard {int(MEMBRANE_RADIUS)} u cell "
            f"with its nucleus (~{int(NUCLEUS_RADIUS)} u). No environment and NOTHING planted wild - every plant is an "
            f"Angler's seed, cacti at {PRISM_SCALE}x leaf, up to {r['cap']} plants between every colour "
            f"({PLANT_PRISMS} prisms, ~{round(PLANT_VOLUME):,} volume each at maturity). "
            f"{r['herdPoints'] * HERDS_PER_POINT} tameable tadpoles graze every garden outside the nucleus; "
            f"{r['omni']} omni crystals respawn inside it (the scene's CrystalManager). Frenzy sits 25% above the "
            f"full arena ({th['maxCount']:,} prisms, {th['maxVolume']:,} volume). Authored by "
            f"Tools/Build/author_glasshouse_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Glasshouse Cell Config {i}.asset", G_ASSET[f"GlasshouseCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Glasshouse Cell Config {i}") + f"""  CellName: Glasshouse
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS_FILEID}, guid: {EXISTING['Nucleus_prefab']}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET[f'GlasshouseSpawnProfile{i}']}, type: 2}}
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

CELLS = [G_ASSET[f"GlasshouseCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnRingRadiusFloorByIntensity:\n"
         + "".join(f"  - {SPAWN_RING}\n" for _ in RUNGS) + "  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 368\n  spawnRingRadiusFloorByIntensity:\n"
               "  - 368\n  - 400\n  - 432\n  - 464\n  spawnFormation: 0\n")
# The omni crystals respawn INSIDE the nucleus (CrystalManager.GetAnchorlessSpawnRadius, LOCKED); noNucleusSpawnRadius
# is the no-nucleus fallback and stays the donor's.
OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {r['omni']}\n" for r in RUNGS))
DONOR_OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
              "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 3\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 2\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 2\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 1\n")
MONITOR_DONOR = (f"  m_Script: {{fileID: 11500000, guid: {DONOR['KelpwoodBiteTurnMonitor']}, type: 3}}\n"
                 "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n")
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['GlasshouseTimeTurnMonitor']}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
           f"  duration: {ROUND_SECONDS}\n")

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Glasshouse.asset", G_ASSET["ModePreviewGlasshouse"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Glasshouse") + f"""  Mode: {MODE_ID}
  Notes: 'The garden cell at each intensity - fewer seeds, more grazers, less room. OPEN-ENDED - it scores
    on mass standing in your colour, and what a solo preview teaches is the loop - an omni crystal in
    the core, out past the nucleus, let go of the lure to plant, hold it to grow. Vessel pinned to Angler({ANGLER_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {ANGLER_ID}
  ObjectiveText: Take an omni crystal, let go of the lure out past the core to plant it, hold the lure to grow it
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
g.register_preview(G_ASSET["ModePreviewGlasshouse"])

# ── 8. Scene: Kelpwood's, with the identity, the monitor, the cells, the spawn ring and the crystals swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.swap_guid(scene, DONOR["KelpwoodController"], G_SCRIPT["GlasshouseController"], "controller")
    scene = lib.swap_guid(scene, DONOR["KelpwoodSettings"], G_ASSET["GlasshouseSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["KelpwoodScoringRule"], G_ASSET["GlasshouseScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "omni crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI, MONITOR]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameGlasshouse.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameGlasshouse", G_ASSET["MinigameGlasshouse.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameGlasshouse"])
g.register_always_unlocked()
g.register_build_scene("MinigameKelpwood", "MinigameGlasshouse", G_ASSET["MinigameGlasshouse.unity"])
g.set_end_condition("glasshouseRoundSeconds", after="tapestryRoundSeconds", value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead: a quarter of the reference domain volume "
                  f"({REFERENCE_DOMAIN_VOLUME:,.0f}) behind buys "
                  f"{0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions about the Angler: an omni crystal leaves a seed from the cell's own species, letting go
# of the lure plants it in the PILOT's colour, and the lure's light hastens growth. A retune of the vessel that breaks
# any of these breaks the mode; this names it.
executor = g.read(ANGLER_EXECUTOR)
for needle in ("public void AddFloraSeed(Vector3 at)", "profile.SupportedFloras", "void PlantSeeds()",
               "domainOverride: _status.Domain", "flora.Hasten(config.LureGrowth, HasteWindow)",
               "public int FloraSeedCount", "public int TamedCount", "public void BeginLure()"):
    if needle not in executor:
        errors.append(f"AnglerCommandExecutor no longer has '{needle}' - Glasshouse's garden is the Angler's seed, "
                      f"planted in the pilot's colour when the lure is let go; re-check how the Angler plants")
prefab = g.read(ANGLER_PREFAB)
if ("  - InputEvent: 6\n    ShipActions:\n    - {fileID: 11400000, guid: " + EXISTING["AnglerLure"]) not in prefab:
    errors.append("Angler.prefab no longer binds Button1Action (6) to AnglerLure - the AI gardener presses "
                  "GlasshouseGardener.LureInput")
if "  control: 0\n" not in g.read(ANGLER_LURE) or "Lure = 0," not in g.read(
        "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/AnglerActionSO.cs"):
    errors.append("AnglerLure.asset is no longer AnglerControl.Lure")
if "spawnerEnabled: 1" not in prefab or "BaseScale: {x: 20, y: 0.75, z: 5.5}" not in prefab \
        or "initialWavelength: 7" not in prefab:
    errors.append("the Angler's trail changed (spawner, BaseScale or wavelength) - re-derive TRAIL_PRISM and the "
                  "comeback/phase model")

# The garden: a plant's children are its colour, the seeder is gated by SpawnProbability, the cactus is branching
# flora with the leaf the model assumes.
flora_cs = g.read(FLORA_CS)
if "inherit: _variantPick, preInitialize: ConfigureOffspring, domainOverride: domain);" not in flora_cs:
    errors.append("Flora.SpawnOffspring no longer plants a child in its parent's domain - a garden would spread "
                  "into other colours")
if "public float LeafVolume => Mathf.Abs(leafSize.x * leafSize.y * leafSize.z);" not in flora_cs:
    errors.append("Flora.LeafVolume changed - GlasshouseGarden.Measure prices a plant as prisms x leaf")
if "UnityEngine.Random.value < Mathf.Clamp01(spawnProbability)" not in g.read(SPAWNER_CS):
    errors.append("CellLifeSpawnerBase.AllowSpawn changed - SpawnProbability 0 may no longer mean 'never plant wild'")
cacti = g.read(CACTI_PREFAB)
if f"guid: {EXISTING['BranchingFlora_script']}" not in cacti or "leafSize: {x: 5, y: 5, z: 3}" not in cacti:
    errors.append("CactiFlora.prefab is no longer a BranchingFlora with a 5 x 5 x 3 leaf - re-derive PLANT_VOLUME")
if f"FloraPrefab: {{fileID: {FLORA_FILEID}, guid: {EXISTING['CactiFlora_prefab']}, type: 3}}" not in cactus_donor:
    errors.append("the donor cactus config no longer names CactiFlora.prefab")
if f"MaxTotalSpawnedObjectsOverride: {PLANT_PRISMS}" not in cactus_donor:
    errors.append(f"the donor cactus config's budget is no longer {PLANT_PRISMS} prisms - re-derive PLANT_VOLUME")
if not re.search(r"^  GrowthPerOffspring: [1-9]\d*$", cactus_donor, re.M):
    errors.append("the donor cactus no longer reproduces (GrowthPerOffspring 0) - a garden would never spread")
# The herd is tameable: a tadpole and a herbivore forager.
if f"FaunaPrefab: {{fileID: {FAUNA_FILEID}, guid: {EXISTING['Tadpole_prefab']}, type: 3}}" not in tadpole_donor:
    errors.append("the donor tadpole config no longer names the tadpole prefab")
if "    Forager: 1\n" not in tadpole_donor:
    errors.append("the donor tadpole is no longer a forager - it would not graze the gardens")

# Enum ids, the default round and the provider.
if f"GlasshouseHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.GlasshouseHint is not {TOAST_HINT}")
if f"DomainRaceLeadChanged = {TOAST_LEAD}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.DomainRaceLeadChanged is not {TOAST_LEAD}")
if f"Glasshouse = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Glasshouse is not {MODE_ID}")
if f"DefaultGlasshouseRoundSeconds = {ROUND_SECONDS};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultGlasshouseRoundSeconds differs from ROUND_SECONDS")
if "VolumeRemaining   => " not in g.read(f"{AR}/Scoring/ScoringMetrics.cs") \
        or "case ScoringMetric.VolumeRemaining:" not in g.read("Assets/_Scripts/Data/ModeGenre.cs"):
    errors.append("VolumeRemaining no longer reads as a metric with a Mass genre - the card would lose its petal")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["GlasshouseSettingsSO"])
for name, (values, harder) in LADDERS.items():
    pairs = list(zip(values, values[1:]))
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for field, value in SHARED.items():
    if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
        errors.append(f"GlasshouseSettingsSO.{field} initializer differs from the authored {value}")
# The gardener reads the seat's difficulty, never the intensity, and presses only the lure.
gardener = g.read(SCRIPT_PATHS["GlasshouseGardener"])
if "SelectedIntensity" in gardener or "AIDifficultyFor(" not in gardener:
    errors.append("GlasshouseGardener must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
if "TriggerAnalog" in gardener or "InputEvents.Button1Action" not in gardener \
        or gardener.count("ShipControllerActionsReplicated(LureInput)") != 2:
    errors.append("GlasshouseGardener presses something other than the lure - the hull's leapfrog policy owns the "
                  "paint and the throw")
# The controller credits the garden by deltas and never overwrites the trail's count.
controller = g.read(SCRIPT_PATHS["GlasshouseController"])
if "stats.VolumeRemaining += delta" not in controller or re.search(r"VolumeRemaining = (?!0f|volumes\[i\])", controller):
    errors.append("GlasshouseController must move VolumeRemaining by garden DELTAS only (StatsManager owns the trail)")

# The arena's own numbers.
for name, ladder_values, direction in (("omni crystals", [r["omni"] for r in RUNGS], "down"),
                                       ("grazers", [r["herdPoints"] for r in RUNGS], "up"),
                                       ("plant cap", [r["cap"] for r in RUNGS], "down")):
    pairs = list(zip(ladder_values, ladder_values[1:]))
    if direction == "up" and any(b < a for a, b in pairs) or direction == "down" and any(b > a for a, b in pairs):
        errors.append(f"the {name} ladder must run {direction} with intensity ({ladder_values})")
for r in RUNGS:
    th = r["ladder"]
    if th["FrenzyEnterVolume"] <= th["maxVolume"] or th["FrenzyEnter"] <= th["maxCount"]:
        errors.append(f"I{r['i']}: Frenzy would freeze the garden before the cap is reached")
    if not th["RestlessEnterVolume"] > th["RestlessExitVolume"] or not th["FrenzyEnterVolume"] > th["FrenzyExitVolume"]:
        errors.append(f"I{r['i']}: phase hysteresis inverted")
    if th["maxCount"] > 9000:
        errors.append(f"I{r['i']}: a full arena is {th['maxCount']} prisms - keep the garden under the collider budget")
if not (NUCLEUS_RADIUS < SPAWN_RING < MEMBRANE_RADIUS - 300):
    errors.append(f"spawn ring {SPAWN_RING} must sit just outside the nucleus")

# The scene: every new identity present, every donor one gone, four Angler AIs, the timed monitor.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("GlasshouseController", "GlasshouseTimeTurnMonitor")},
                     "GlasshouseSettings": G_ASSET["GlasshouseSettings"],
                     "GlasshouseScoringRule": G_ASSET["GlasshouseScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Kelpwood cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {ANGLER_ID}\n") != 4:
        errors.append("scene does not carry exactly 4 Angler AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Glasshouse"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/GLASSHOUSE.md")])
