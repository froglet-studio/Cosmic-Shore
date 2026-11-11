#!/usr/bin/env python3
"""
Authors every serialized asset the Corona game mode needs (GameModes.Corona = 87).

Corona is the Manta's CHARGE game, picked by Shombith on 2026-11-10 from three drawn ideas (the others were
Tinderbox, a lattice of prism struts, and Tiltyard, braided rails ridden head-on): nested prism SHELLS round an open
crystal HEART in a 0G volume. Skim a shell to arm a bomb (the Manta's skimmer pays energy from any prism), graze a
rival to plant it (one bomb per target, no tell), then come in through the gates and touch an omni crystal in the
heart to set every bomb off at once (Kabloom, MANTA_STING_KABLOOM.md §0). Every rival a bloom catches - already a
Debuff-class combat hit - is one point. The first DOMAIN to the target wins.

THE LADDER (one SpawnableCorona prefab and cell config per intensity; numbers in corona_budget.RUNGS):
  I1 Halo          - one shell, eight wide gates, four omni crystals.
  I2 Corona        - two shells, six gates each, the inner turned away from the outer; three omni crystals.
  I3 Chromosphere  - three shells, five gates each with a danger rim; two omni crystals.
  I4 Flare         - four shells, four narrow rimmed gates each; one omni crystal.
The shells' census, volume and PhaseThresholds come from corona_budget.py, an exact mirror of the C#.

NOTHING ABOUT THE MANTA CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The fuse stays the Manta's own 20 s at
every rung (Bloomrush's per-intensity fuse override predates that rule and is not copied). The mode's levers are the
shells, the omni crystals in the heart (the scene's CrystalManager) and the AI stingers. The scene is cloned from
Kelpwood's (the newest CombatPoints duel, whose controller declares settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_corona_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import corona_budget as budget  # noqa: E402

MODE_ID = 87
TOAST_HINT = 162
g = lib.Generator(MODE_ID, "Corona")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "CoronaController":       f"{AR}/Corona/CoronaController.cs",
    "CoronaSettingsSO":       f"{AR}/Corona/CoronaSettingsSO.cs",
    "CoronaScoringRuleSO":    f"{AR}/Corona/CoronaScoringRuleSO.cs",
    "CoronaStinger":          f"{AR}/Corona/CoronaStinger.cs",
    "CoronaBloomTurnMonitor": f"{AR}/TurnMonitors/CoronaBloomTurnMonitor.cs",
    "SpawnableCorona":        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableCorona.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Corona Cell"
G_ASSET = {
    "ArcadeGameCorona":      guid("asset/ArcadeGameCorona"),
    "CoronaScoringRule":     guid("asset/CoronaScoringRule"),
    "CoronaSettings":        guid("asset/CoronaSettings"),
    "GameToastConfigCorona": guid("asset/GameToastConfigCorona"),
    "ModePreviewCorona":     guid("asset/ModePreviewCorona"),
    "MinigameCorona.unity":  guid("asset/MinigameCorona.unity"),
    "CoronaSpawnProfile":    guid("asset/CoronaSpawnProfile"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"CoronaCellConfig{i}"] = guid(f"asset/CoronaCellConfig{i}")
    G_ASSET[f"prefab/SpawnableCorona{i}"] = guid(f"asset/SpawnableCorona{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Manta"] = lib.VESSELS["Manta"]                          # SO_Class_Manta.asset
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"            # SpawnablePrism.prefab (window 0.5-100)
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"   # the standard 1,200 u cell (the Boneyard's)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
# The bloom: what makes a Manta's bomb a scored, petal-taking hit (read to prove the rule pays something real).
BLOOM_CONTAINER = ("Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/"
                   "MantaBloomExplosionImpactorDataContainer.asset")
BLOOM_PREFAB = "Assets/_Prefabs/Projectile/AOEMantaBloom.prefab"
COMBAT_HIT_EFFECT = "Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByCrystalBlast.asset"
COMBAT_HIT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"                    # Event_CombatHitStats.asset
MANTA_BAY = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/MantaStingActionExecutor.cs"
MANTA_SKIM = ("Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/"
              "MantaStingSkimPrismEffectSO.cs")
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
MANTA_ID = lib.VESSEL_CLASS_ID["Manta"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 8700000000000201, 8700000000000202, 8700000000000203

# ── The race ─────────────────────────────────────────────────────────────────
BLOOM_TARGET = 15         # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultCoronaBloomTarget
BLOOM_POINTS = 1
DEBUFF_CLASS = 2          # CombatHitClass.Debuff - what VesselCombatHitByCrystalBlast reports for a bloom
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (3.75 blooms) buys
# 1.1 element levels; on the Manta, Charge (petals per bloom), Space (skimmer reach) and Time (speed) all move a
# number in this fight.
COMEBACK_RATE = 0.3

MEMBRANE_RADIUS = 1200.0
HEART = budget.HEART_RADIUS   # the omni crystals spawn in a ball this wide round the heart (CrystalManager.noNucleusSpawnRadius)

RUNGS = budget.RUNGS
for r in RUNGS:
    r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableCorona{r['i']}.prefab"
    r["laid"] = budget.build(r)

# Cleave / Serpent Gulag / Boneyard / Jackstraw / Kelpwood ladder deltas: a cell's phase rides ITS arena's baseline.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameCorona.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Corona")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/CORONA.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/CoronaScoringRule.asset", G_ASSET["CoronaScoringRule"],
             lib.header_for(G_SCRIPT["CoronaScoringRuleSO"], "CoronaScoringRule") +
             f"""  metric: 8
  golfRules: 1
  bloomPoints: {BLOOM_POINTS}
  otherHitPoints: 0
  bloomsLeftTemplate: '{{count}} Blooms Left'
  bloomsLandedTemplate: '{{count}} blooms'
  victoryHeader: VICTORY
  victoryLabel: CORONA TIME
  defeatHeader: DEFEAT
  defeatLabel: BLOOMS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":            ([1.2, 0.9, 0.6, 0.4], "down"),
    "aiHuntRange":                  ([350, 500, 650, 850], "up"),
    "aiLeadSeconds":                ([0, 0.15, 0.3, 0.45], "up"),
    "aiGrazeOffset":                ([9, 7, 5, 4], "down"),
    "aiSkimStandoff":               ([10, 9, 8, 7], "down"),
    "aiGateError":                  ([0.5, 0.35, 0.2, 0.1], "down"),
    "aiCashAtBombs":                ([1, 2, 3, 3], "up"),
    "aiCashAtFuseSeconds":          ([11, 9, 7, 6], "down"),
    "aiBreakOffWhenSilencedChance": ([0, 0.5, 1, 1], "up"),
    "aiSilencedPreyFocus":          ([1, 1, 1.5, 2], "up"),
}
SHARED = {"aiHumanFocus": 2, "aiGateApproach": 45, "aiSkimLookahead": 60, "aiBandClearance": 25}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/CoronaSettings.asset", G_ASSET["CoronaSettings"],
             lib.header_for(G_SCRIPT["CoronaSettingsSO"], "CoronaSettings")
             + "".join(ladder_yaml(n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a bloom never takes a team-mate, so a solo lobby has nobody to catch.
# No colons in the description: a plain YAML scalar would start a new key at one (the Serpentine trap).
DESCRIPTION = ("Mantas only - shells of prisms round a heart of omni crystals, and the only way in is through the "
               "gates. Skim a shell to arm a bomb, graze a rival to plant it, then dive through the gates and touch a "
               "crystal to set every bomb you planted off at once. Every rival a bloom catches is a point that "
               "silences them. Never touch a shell with your hull - that sets your bombs off early. Higher "
               "intensities add shells, narrow the gates, rim them in danger and thin the crystals. First team to "
               "the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameCorona"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameCorona") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Corona
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameCorona
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Manta']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Corona.asset", G_ASSET["GameToastConfigCorona"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Corona") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> caught a rival in a bloom!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} blooms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} blooms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Skim a shell to arm a bomb, graze a rival to plant it, then touch a crystal in "
                       "the heart. Keep your hull off the shells", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigCorona"])

# ── 6. The arena: one environment prefab and cell config per rung, one empty spawn profile ──
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableCorona{r['i']}"], f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{SPAWNABLE_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SPAWNABLE_TR}}}
  - component: {{fileID: {SPAWNABLE_MB}}}
  m_Layer: 0
  m_Name: SpawnableCorona{r['i']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{SPAWNABLE_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{SPAWNABLE_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableCorona']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {r['seed']}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: {r['i']}
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  shellCount: {r['shellCount']}
  innerShellRadius: {r['innerShellRadius']}
  shellSpacing: {r['shellSpacing']}
  gatesPerShell: {r['gatesPerShell']}
  gateHalfAngle: {lib.num(float(r['gateHalfAngle']))}
  rimDegrees: {lib.num(float(r['rimDegrees']))}
""")

# One spawn profile, shared by all four configs: it spawns nothing (no flora, no fauna). The fight is pilot on pilot;
# a creature would be a free bomb carrier (MANTA_STING_KABLOOM.md §2) and a second target the AI would chase.
g.emit_asset(f"{CELL_DIR}/Corona Spawn Profile.asset", G_ASSET["CoronaSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Corona Spawn Profile") + """  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 0
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  SupportedFloras: []
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 0
  InitialFaunaReleaseTier: 0
  FaunaSpawnVolumeThreshold: 1
  BaseFaunaSpawnTime: 15
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 0
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0.5
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 0
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 0
  SupportedFaunas: []
""")

for r in RUNGS:
    i = r["i"]
    c = r["laid"]["census"]
    th = thresholds(c["prisms"], round(c["volume"]))
    env = f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableCorona{i}']}, type: 3}}"
    desc = (f"{r['label']} - intensity {i} of Corona: {r['blurb']}. {c['prisms']} prisms, all neutral "
            f"({c['danger']} danger), outer shell {round(c['outerRadius'])} u inside the standard "
            f"{int(MEMBRANE_RADIUS)} u cell, spawn ring {r['spawnRing']}. {r['omni']} omni crystals float in the "
            f"heart (the scene's CrystalManager). NO NUCLEUS, no flora, no fauna. Authored by "
            f"Tools/Build/author_corona_assets.py from corona_budget.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Corona Cell Config {i}.asset", G_ASSET[f"CoronaCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Corona Cell Config {i}") + f"""  CellName: Corona
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['CoronaSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {env}
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

CELLS = [G_ASSET[f"CoronaCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


def spawn_block(rings):
    return ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
            f"  spawnRingRadiusFloor: {rings[0]}\n  spawnRingRadiusFloorByIntensity:\n"
            + "".join(f"  - {v}\n" for v in rings) + "  spawnFormation: 0\n")


def omni_block(ball, counts):
    return (f"  noNucleusSpawnRadius: {ball}\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
            "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
            + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {n}\n" for n in counts))


RINGS = [r["spawnRing"] for r in RUNGS]
SPAWN = spawn_block(RINGS)
DONOR_SPAWN = spawn_block([368, 400, 432, 464])          # Kelpwood's ring
OMNI = omni_block(HEART, [r["omni"] for r in RUNGS])
DONOR_OMNI = omni_block(200, [3, 2, 2, 1])               # Kelpwood's crystals

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Corona.asset", G_ASSET["ModePreviewCorona"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Corona") + f"""  Mode: {MODE_ID}
  Notes: 'The shells at each intensity - more shells, narrower gates, then danger rims, and fewer crystals in
    the heart. OPEN-ENDED - it scores on blooms catching rival pilots and a solo preview has none; what it
    teaches is skimming a shell, the gates and the heart. Vessel pinned to Manta({MANTA_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {MANTA_ID}
  ObjectiveText: Skim a shell, graze a rival, then touch a crystal in the heart
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewCorona"])

# ── 8. Scene: Kelpwood's, with the identity, the cells, the AI hulls, the spawn ring and the crystals swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["KelpwoodBiteTurnMonitor"], G_SCRIPT["CoronaBloomTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["KelpwoodController"], G_SCRIPT["CoronaController"], "controller")
    scene = lib.swap_guid(scene, DONOR["KelpwoodSettings"], G_ASSET["CoronaSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["KelpwoodScoringRule"], G_ASSET["CoronaScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {MANTA_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "omni crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameCorona.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameCorona", G_ASSET["MinigameCorona.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameCorona"])
g.register_always_unlocked()
g.register_build_scene("MinigameKelpwood", "MinigameCorona", G_ASSET["MinigameCorona.unity"])
g.set_end_condition("coronaBloomTarget", after="kelpwoodBiteTarget", value=BLOOM_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * BLOOM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {BLOOM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * BLOOM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions about the Manta, read from the shipped kit (never edited here):
# a bloom is a scored Debuff hit on the game's combat channel...
container = g.read(BLOOM_CONTAINER)
hit_guid = lib.read_meta_guid(COMBAT_HIT_EFFECT) if hasattr(lib, "read_meta_guid") else \
    g.read(COMBAT_HIT_EFFECT + ".meta").split("guid: ")[1].split()[0]
if hit_guid not in container:
    errors.append("MantaBloomExplosionImpactorDataContainer no longer carries VesselCombatHitByCrystalBlast - a bloom "
                  "would take no petals and score nothing")
container_guid = g.read(BLOOM_CONTAINER + ".meta").split("guid: ")[1].split()[0]
if container_guid not in g.read(BLOOM_PREFAB):
    errors.append("AOEMantaBloom.prefab no longer uses MantaBloomExplosionImpactorDataContainer")
hit = g.read(COMBAT_HIT_EFFECT)
if f"hitClass: {DEBUFF_CLASS}\n" not in hit or f"onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_CHANNEL}" not in hit:
    errors.append("VesselCombatHitByCrystalBlast is no longer a Debuff-class hit on the game's combat-hit channel")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count(f"Debuff = {DEBUFF_CLASS},"):
    errors.append(f"CombatHitClass.Debuff is no longer {DEBUFF_CLASS}")
# ...a skim of ANY prism (the shells are neutral) pays energy, and a graze plants on a vessel...
skim = g.read(MANTA_SKIM)
if "bay.AddSkimCharge(prism)" not in skim or skim.index("bay.AddSkimCharge(prism)") > skim.index("domain == Domains.Blue"):
    errors.append("MantaStingSkimPrismEffectSO no longer pays energy before its domain check - neutral shells would "
                  "not arm a bomb")
bay = g.read(MANTA_BAY)
for needle in ("public bool TryPlantOnVessel(IVesselStatus target)", "public int ArmedBombs",
               "public IReadOnlyList<MantaBomb> PlantedBombs", "public float FuseRemaining",
               "public static bool TryGetFor(IVesselStatus status, out MantaStingActionExecutor executor)",
               "public void OnPrismImpact()"):
    if needle not in bay:
        errors.append(f"MantaStingActionExecutor no longer has '{needle}' - CoronaStinger reads the bay through it")
if f"CoronaHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.CoronaHint is not {TOAST_HINT}")
if f"Corona = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Corona is not {MODE_ID}")

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultCoronaBloomTarget = {BLOOM_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultCoronaBloomTarget differs from BLOOM_TARGET")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["CoronaSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for field, value in SHARED.items():
    if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
        errors.append(f"CoronaSettingsSO.{field} initializer differs from the authored {value}")
if max(LADDERS["aiSkimStandoff"][0]) > 10 or max(LADDERS["aiGrazeOffset"][0]) > 10:
    errors.append("an AI standoff or graze offset is past the Manta's 10 u skimmer reach (Skimmer.Scale 20 at Space 0)")

# The stinger reads the seat's difficulty, never the intensity, and presses NOTHING (every Charge verb is passive;
# the hull's own autopilot drive Soars). The controller never touches the fuse (the vessel rule).
stinger = g.read(SCRIPT_PATHS["CoronaStinger"])
if "SelectedIntensity" in stinger or "AIDifficultyFor(" not in stinger:
    errors.append("CoronaStinger must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
for banned in ("PerformShipControllerActions", "InputStatus", "TriggerAnalog"):
    if banned in stinger:
        errors.append(f"CoronaStinger uses {banned} - it steers only; the Manta's kit is passive")
if "SetExternalTargetProvider" not in stinger or "GateDirection" not in stinger:
    errors.append("CoronaStinger no longer steers through the arena's gates")
for path in (SCRIPT_PATHS["CoronaController"], SCRIPT_PATHS["CoronaStinger"]):
    if "FuseSecondsOverride" in g.read(path):
        errors.append(f"{path} overrides the Manta's fuse - a mode never changes the vessel")

# The shells the drawing promised, as the C# lays them.
spawnable = g.read(SCRIPT_PATHS["SpawnableCorona"])
for needle in (f"TileSpacing = {lib.num(budget.TILE_SPACING)}f;", f"TileWidth = {lib.num(budget.TILE_WIDTH)}f;",
               f"TileThickness = {lib.num(budget.TILE_THICKNESS)}f;",
               f"ShellTwistDegrees = {lib.num(budget.SHELL_TWIST_DEGREES)}f;",
               "TwistAxis = new Vector3(" + ", ".join(lib.num(v) + "f" for v in budget.TWIST_AXIS) + ");",
               f"DefaultSeed => {budget.DEFAULT_SEED};",
               "Mathf.RoundToInt(4f * Mathf.PI * radius * radius / (TileSpacing * TileSpacing))",
               "if (nearest > cosGate) continue;", "rimDegrees > 0f && nearest > cosRim",
               "Quaternion.AngleAxis(ShellTwistDegrees * shell, TwistAxis.normalized) * Fibonacci(gate, gatesPerShell)",
               "public float ShellRadius(int shell) => innerShellRadius + shellSpacing * shell;",
               "Mathf.Abs(p.y) < 0.9f ? Vector3.up : Vector3.right"):
    if needle not in spawnable:
        errors.append(f"SpawnableCorona no longer declares '{needle}' - update corona_budget.py with it")

counts = []
for r in RUNGS:
    c = r["laid"]["census"]
    counts.append(c["prisms"])
    if c["boundaryMargin"] < budget.MIN_MARGIN:
        errors.append(f"I{r['i']}: a tile sits {c['boundaryMargin']:.2e} (cosine) from a gate or rim edge - float32 "
                      f"could flip it; nudge gateHalfAngle / rimDegrees")
    if c["leastTileGap"] <= c["tileDiagonal"]:
        errors.append(f"I{r['i']}: two tiles' centres are {c['leastTileGap']:.2f} u apart, under a tile's diagonal "
                      f"({c['tileDiagonal']:.2f}) - they could interpenetrate")
    if r["shellCount"] > 1 and r["shellSpacing"] < 80:
        errors.append(f"I{r['i']}: shells {r['shellSpacing']} u apart leave no band to fly in")
    if r["innerShellRadius"] - HEART < budget.HEART_CLEAR:
        errors.append(f"I{r['i']}: the heart's {HEART} u crystal ball comes within {budget.HEART_CLEAR} u of the "
                      f"innermost shell")
    if c["gateWidthInner"] < 60:
        errors.append(f"I{r['i']}: the inner gates are {c['gateWidthInner']:.0f} u wide - too narrow for a Manta at speed")
    if not (c["outerRadius"] + 80 <= r["spawnRing"] <= MEMBRANE_RADIUS - 300):
        errors.append(f"I{r['i']}: spawn ring {r['spawnRing']} must sit outside the outer shell "
                      f"({c['outerRadius']:.0f}) and well inside the {int(MEMBRANE_RADIUS)} u membrane")
for name, ladder, direction in (("prisms", counts, "up"),
                                ("shells", [r["shellCount"] for r in RUNGS], "up"),
                                ("gate half-angles", [r["gateHalfAngle"] for r in RUNGS], "down"),
                                ("omni crystals", [r["omni"] for r in RUNGS], "down")):
    pairs = list(zip(ladder, ladder[1:]))
    if direction == "up" and any(b < a for a, b in pairs) or direction == "down" and any(b > a for a, b in pairs):
        errors.append(f"the {name} ladder must run {direction} with intensity ({ladder})")
for a, b in zip(counts, counts[1:]):
    if b < a * 1.25:
        errors.append(f"an intensity step adds under 25 % more shell ({a} -> {b}): intensity would not be felt")
if counts[-1] > 4000:
    errors.append(f"I4 lays {counts[-1]} prisms - keep the arena light")
if counts[-1] > 4096:
    errors.append("SpawnableCorona.LayCapacity (4096) is under the I4 census")

# The scene: every new identity present, every donor one gone, four Manta AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("CoronaController", "CoronaBloomTurnMonitor")},
                     "CoronaSettings": G_ASSET["CoronaSettings"],
                     "CoronaScoringRule": G_ASSET["CoronaScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Kelpwood cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {MANTA_ID}\n") != 4 or f"  - vesselClass: {ANGLER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Manta AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Corona"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/CORONA.md")])
