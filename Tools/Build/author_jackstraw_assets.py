#!/usr/bin/env python3
"""
Authors every serialized asset the Jackstraw game mode needs (GameModes.Jackstraw = 85; it first took 84, which Shombith gave to the Brittle Star's Flatline on
2026-11-09).

Jackstraw is the Thresher's CHARGE game, picked by Shombith on 2026-11-09 from three drawn ideas: a 0G heap of
pick-up sticks with no table, each stick about a chain long (120 u against the Thresher's 22-140 u chain), so a
swinging ball is always about to clip one. A STRIKE - the ball or any chain link reaching a rival hull, already a
Strike-class combat hit (ThresherExecutor.TryHitVessel) - is one point; the first DOMAIN to the target wins.

THE LADDER (one SpawnableJackstraw prefab and cell config per intensity; numbers in jackstraw_budget.RUNGS):
  I1 Loose Sticks - 34 sticks round one clearing.
  I2 Woodpile     - 78 sticks, 6 jacks, five clearings, red tips, ironwood hearts.
  I3 Bramble      - 140 sticks, 14 jacks, seven clearings, more of both.
  I4 Thicket      - 230 sticks, 26 jacks, nine small clearings, red tips on one stick in three.
The heap's census, volume and PhaseThresholds come from jackstraw_budget.py, an exact mirror of the C#.

NOTHING ABOUT THE THRESHER CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the heap, the
crystals in its clearings and the AI hunters. The scene is cloned from Serpent Gulag's (the newest CombatPoints duel,
whose controller declares settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_jackstraw_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import jackstraw_budget as budget  # noqa: E402

MODE_ID = 85
g = lib.Generator(MODE_ID, "Jackstraw")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "JackstrawController":        f"{AR}/Jackstraw/JackstrawController.cs",
    "JackstrawSettingsSO":        f"{AR}/Jackstraw/JackstrawSettingsSO.cs",
    "JackstrawScoringRuleSO":     f"{AR}/Jackstraw/JackstrawScoringRuleSO.cs",
    "JackstrawHunter":            f"{AR}/Jackstraw/JackstrawHunter.cs",
    "JackstrawStrikeTurnMonitor": f"{AR}/TurnMonitors/JackstrawStrikeTurnMonitor.cs",
    "SpawnableJackstraw":         "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableJackstraw.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Jackstraw Cell"
G_ASSET = {
    "ArcadeGameJackstraw":      guid("asset/ArcadeGameJackstraw"),
    "JackstrawScoringRule":     guid("asset/JackstrawScoringRule"),
    "JackstrawSettings":        guid("asset/JackstrawSettings"),
    "GameToastConfigJackstraw": guid("asset/GameToastConfigJackstraw"),
    "ModePreviewJackstraw":     guid("asset/ModePreviewJackstraw"),
    "MinigameJackstraw.unity":  guid("asset/MinigameJackstraw.unity"),
    "JackstrawSpawnProfile":    guid("asset/JackstrawSpawnProfile"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"JackstrawCellConfig{i}"] = guid(f"asset/JackstrawCellConfig{i}")
    G_ASSET[f"prefab/SpawnableJackstraw{i}"] = guid(f"asset/SpawnableJackstraw{i}.prefab")

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Thresher"] = lib.VESSELS["Thresher"]
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"           # SpawnablePrism.prefab (window 0.5-100)
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"  # the standard 1,200 u cell (the Boneyard's)
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
THRESHER_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ThresherExecutor.cs"
THRESHER_CONFIG = "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset"
# The donor scene's identity, minted by author_serpent_gulag_assets.py with the same guid function.
# Serpent Gulag was rebuilt in place as Hood on 2026-11-09 (same scene, same guids: Hood keeps the Gulag seeds below).
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameSerpentHood.unity"
DONOR = {
    "SerpentGulagController":       guid("script/SerpentGulagController"),
    "SerpentGulagSnipeTurnMonitor": guid("script/SerpentGulagSnipeTurnMonitor"),
    "SerpentGulagSettings":         guid("asset/SerpentGulagSettings"),
    "SerpentGulagScoringRule":      guid("asset/SerpentGulagScoringRule"),
}
DONOR_CELLS = [guid(f"asset/SerpentGulagCellConfig{i}") for i in (1, 2, 3, 4)]
SERPENT_ID = lib.VESSEL_CLASS_ID["Serpent"]
THRESHER_ID = lib.VESSEL_CLASS_ID["Thresher"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 8400000000000201, 8400000000000202, 8400000000000203

# ── The brawl ────────────────────────────────────────────────────────────────
STRIKE_TARGET = 8         # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultJackstrawStrikeTarget
STRIKE_POINTS = 1
STRIKE_CLASS = 5          # CombatHitClass.Strike - what ThresherExecutor.TryHitVessel reports
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (2 strikes) buys
# 1.2 element levels; on this hull a Charge level is more petals per strike.
COMEBACK_RATE = 0.6

MEMBRANE_RADIUS = 1200.0
CHAIN_MAX = 140.0          # the Thresher's chain let out (THRESHER.md §4, maxLen)
CRYSTALS_PER_CLEARING = 3
OMNI_RADIUS = 50           # the omni crystal spawns inside the centre clearing (every rung has one)

RUNGS = budget.RUNGS
for r in RUNGS:
    r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableJackstraw{r['i']}.prefab"
    r["laid"] = budget.build(r)

# Cleave / Serpent Gulag / Boneyard ladder deltas: a cell's phase rides ITS arena's own baseline. No fauna here, so the
# phase only matters as the cell's own state, but it must not start a match in the wrong one.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameJackstraw.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Jackstraw")
g.folder_meta(CELL_DIR)
g.text_meta(f"{AR}/JACKSTRAW.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/JackstrawScoringRule.asset", G_ASSET["JackstrawScoringRule"],
             lib.header_for(G_SCRIPT["JackstrawScoringRuleSO"], "JackstrawScoringRule") +
             f"""  metric: 8
  golfRules: 1
  strikePoints: {STRIKE_POINTS}
  otherHitPoints: 0
  strikesLeftTemplate: '{{count}} Strikes Left'
  strikesLandedTemplate: '{{count}} strikes'
  victoryHeader: VICTORY
  victoryLabel: HEAP TIME
  defeatHeader: DEFEAT
  defeatLabel: STRIKES LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":          ([1.2, 0.9, 0.6, 0.4], "down"),
    "aiHuntRange":                ([600, 800, 1000, 1400], "up"),
    "aiLeadSeconds":              ([0, 0.15, 0.3, 0.45], "up"),
    "aiMissRadius":               ([45, 30, 15, 5], "down"),
    "aiRefuelWhenSilencedChance": ([0, 0.5, 1, 1], "up"),
    "aiSilencedPreyFocus":        ([1, 1, 1.5, 2], "up"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/JackstrawSettings.asset", G_ASSET["JackstrawSettings"],
             lib.header_for(G_SCRIPT["JackstrawSettingsSO"], "JackstrawSettings") +
             f"""  progressSampleSeconds: 0.5
  crystalsPerClearing: {CRYSTALS_PER_CLEARING}
  clearingFill: 0.5
  crystalRegrowSeconds: 8
  crystalSeed: 84
""" + "".join(ladder_yaml(n) for n in LADDERS) + """  aiOvershoot: 60
  aiHumanFocus: 2
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: the flail never strikes a team-mate, so a solo lobby has nobody to score on.
DESCRIPTION = ("Threshers only - a brawl in a heap of pick-up sticks floating in every direction. Each stick is about "
               "a chain long, so your swinging ball is always about to clip one, and every stick it breaks costs it "
               "speed. Run your ball or chain through a rival pilot to strike them - each strike is one point and "
               "silences them while they refuel. Crystals float in the clearings, out of the sticks' cover. Higher "
               "intensities grow the heap, cross the sticks into jacks and add red-tipped sticks. First team to the "
               "target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameJackstraw"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameJackstraw") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Jackstraw
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameJackstraw
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Thresher']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Jackstraw.asset", G_ASSET["GameToastConfigJackstraw"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Jackstraw") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(86, "<b>{0}</b> struck a rival!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(160, "Run your ball or chain through a rival to strike - crystals float in the clearings",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigJackstraw"])

# ── 6. The arena: one environment prefab per rung, four cell configs, one spawn profile ──
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableJackstraw{r['i']}"], f"""%YAML 1.1
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
  m_Name: SpawnableJackstraw{r['i']}
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
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableJackstraw']}, type: 3}}
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
  heapRadius: {r['heapRadius']}
  stickCount: {r['stickCount']}
  jackCount: {r['jackCount']}
  centreClearingRadius: {r['centreClearingRadius']}
  ringClearingCount: {r['ringClearingCount']}
  ringClearingDistance: {r['ringClearingDistance']}
  ringClearingRadius: {r['ringClearingRadius']}
  dangerTipEveryNth: {r['dangerTipEveryNth']}
  ironwoodEveryNth: {r['ironwoodEveryNth']}
""")

for r in RUNGS:
    i = r["i"]
    c = r["laid"]["census"]
    th = thresholds(c["prisms"], round(c["volume"]))
    env = f"{{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableJackstraw{i}']}, type: 3}}"
    desc = (f"{r['label']} - intensity {i} of Jackstraw: {r['blurb']}. {c['prisms']} prisms, all neutral "
            f"({c['shielded']} shielded, {c['danger']} danger), heap radius {r['heapRadius']} inside the standard "
            f"{int(MEMBRANE_RADIUS)} u cell, spawn ring {r['spawnRing']}. Crystals float in the clearings, laid by the "
            f"controller. NO NUCLEUS, no flora, no fauna. Authored by Tools/Build/author_jackstraw_assets.py from "
            f"jackstraw_budget.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Jackstraw Cell Config {i}.asset", G_ASSET[f"JackstrawCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Jackstraw Cell Config {i}") + f"""  CellName: Jackstraw
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['JackstrawSpawnProfile']}, type: 2}}
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

# One spawn profile, shared by all four configs: it spawns nothing (no flora, no fauna).
g.emit_asset(f"{CELL_DIR}/Jackstraw Spawn Profile.asset", G_ASSET["JackstrawSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Jackstraw Spawn Profile") + """  FloraExcludeLocalDomain: 0
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

CELLS = [G_ASSET[f"JackstrawCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


RINGS = [r["spawnRing"] for r in RUNGS]
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {RINGS[0]}\n  spawnRingRadiusFloorByIntensity:\n"
         + "".join(f"  - {v}\n" for v in RINGS) + "  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 2200\n  spawnFormation: 0\n")
NO_NUCLEUS = f"  noNucleusSpawnRadius: {OMNI_RADIUS}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Jackstraw.asset", G_ASSET["ModePreviewJackstraw"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Jackstraw") + f"""  Mode: {MODE_ID}
  Notes: 'The heap at each intensity - loose sticks, then jacks, red tips and ironwood. OPEN-ENDED -
    it scores on striking rival pilots and a solo preview has none; what it teaches is the whip
    through the sticks and the clearings. Vessel pinned to Thresher({THRESHER_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {THRESHER_ID}
  ObjectiveText: Swing your ball through a rival pilot to strike them
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
g.register_preview(G_ASSET["ModePreviewJackstraw"])

# ── 8. Scene: Serpent Gulag's, with the identity, the cells, the AI hulls and the spawn ring swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["SerpentGulagSnipeTurnMonitor"], G_SCRIPT["JackstrawStrikeTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["SerpentGulagController"], G_SCRIPT["JackstrawController"], "controller")
    scene = lib.swap_guid(scene, DONOR["SerpentGulagSettings"], G_ASSET["JackstrawSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["SerpentGulagScoringRule"], G_ASSET["JackstrawScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {SERPENT_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {THRESHER_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, "  noNucleusSpawnRadius: 380\n", NO_NUCLEUS, "crystal volume")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, NO_NUCLEUS]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameJackstraw.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameJackstraw", G_ASSET["MinigameJackstraw.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameJackstraw"])
g.register_always_unlocked()
g.register_build_scene("MinigameSerpentHood", "MinigameJackstraw", G_ASSET["MinigameJackstraw.unity"])
g.set_end_condition("jackstrawStrikeTarget", after="serpentHoodSnipeTarget", value=STRIKE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * STRIKE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {STRIKE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * STRIKE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumption: the Thresher still reports a hull contact as a Strike, through TryAdmit, on the
# scoring channel, from both the ball and the chain.
executor = g.read(THRESHER_EXECUTOR)
for needle in ("CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Strike",
               "HitClass = CombatHitClass.Strike", "HitVesselsAlong(result.BallFrom",
               "HitVesselsAlong(pts[s], pts[s + 1], config.ChainCutRadius"):
    if needle not in executor:
        errors.append(f"ThresherExecutor no longer has '{needle}' - JackstrawScoringRuleSO pays Strike hits from the "
                      f"ball and the chain; re-check how the Thresher hits a hull")
if "onCombatHitLanded: {fileID: 11400000, guid: 45fb43a0cddb5cc3cc4dc448df994152" not in g.read(THRESHER_CONFIG):
    errors.append("ThresherConfig.onCombatHitLanded is no longer the game's combat-hit channel - strikes would score nothing")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count(f"Strike = {STRIKE_CLASS},"):
    errors.append(f"CombatHitClass.Strike is no longer {STRIKE_CLASS}")

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultJackstrawStrikeTarget = {STRIKE_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultJackstrawStrikeTarget differs from STRIKE_TARGET")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["JackstrawSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for field, value in (("crystalsPerClearing", CRYSTALS_PER_CLEARING), ("aiOvershoot", "60f"), ("aiHumanFocus", "2f")):
    if f"{field} = {value};" not in settings_cs:
        errors.append(f"JackstrawSettingsSO.{field} initializer differs from the authored {value}")
# The hunter reads the seat's difficulty, never the intensity (AIDifficulty.cs), and presses nothing of the vessel's.
hunter = g.read(SCRIPT_PATHS["JackstrawHunter"])
if "SelectedIntensity" in hunter or "AIDifficultyFor(" not in hunter:
    errors.append("JackstrawHunter must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
if "PerformShipControllerActions" in hunter:
    errors.append("JackstrawHunter presses a vessel input - it only steers; the hull's own sling policy plants")

# The heap the drawing promised, as the C# lays it.
spawnable = g.read(SCRIPT_PATHS["SpawnableJackstraw"])
for needle in (f"StickPrisms = {budget.STICK_PRISMS};",
               f"Segment = {lib.num(budget.SEGMENT)}f, Section = {lib.num(budget.SECTION)}f;",
               f"IronwoodFrom = {budget.IRONWOOD_FROM}, IronwoodTo = {budget.IRONWOOD_TO};",
               f"StickGap = {budget.STICK_GAP}f;", f"StickTries = {budget.STICK_TRIES};",
               f"JackArmPrisms = {budget.JACK_ARM_PRISMS};", f"Knot = {lib.num(budget.KNOT)}f;",
               f"Flush = {budget.FLUSH}f;", "JackKeyBase = 1_000_000, StickKeyBase = 2_000_000;",
               f"DefaultSeed => {budget.DEFAULT_SEED};",
               "unchecked(LayoutSeed * 7919 + family + (slot * StickTries + attempt) * 4 + draw)"):
    if needle not in spawnable:
        errors.append(f"SpawnableJackstraw no longer declares '{needle}' - update jackstraw_budget.py with it")

counts = []
for r in RUNGS:
    c = r["laid"]["census"]
    counts.append(c["prisms"])
    if c["skippedSticks"] or c["skippedJacks"]:
        errors.append(f"I{r['i']}: {c['skippedSticks']} sticks and {c['skippedJacks']} jacks found no room - the "
                      f"heap is fuller than its radius allows; the drawing promised all of them")
    if c["minMargin"] < budget.MIN_MARGIN:
        errors.append(f"I{r['i']}: a piece sits {c['minMargin']:.5f} u from a rejection boundary - float32 could lay "
                      f"a different heap than this census (re-seed the rung)")
    if r["ironwoodEveryNth"] == 1:
        errors.append(f"I{r['i']}: ironwoodEveryNth 1 marks nothing (slot % 1 is never 1)")
    if not (r["heapRadius"] + 60 <= r["spawnRing"] <= MEMBRANE_RADIUS - 300):
        errors.append(f"I{r['i']}: spawn ring {r['spawnRing']} must sit just outside the heap ({r['heapRadius']}) and "
                      f"well inside the {int(MEMBRANE_RADIUS)} u membrane")
    for centre, radius in r["laid"]["clearings"]:
        d = budget.math.sqrt(budget.dot(centre, centre))
        if d + radius > r["heapRadius"]:
            errors.append(f"I{r['i']}: a clearing pokes out of the heap")
        if radius < 2 * 9:
            errors.append(f"I{r['i']}: a clearing of radius {radius} cannot hold a crystal clear of the sticks")
    if r["centreClearingRadius"] < OMNI_RADIUS + 20:
        errors.append(f"I{r['i']}: the omni crystal's {OMNI_RADIUS} u spawn ball does not fit the centre clearing")
    if budget.STICK_HALF * 2 < CHAIN_MAX * 0.6:
        errors.append("a stick is much shorter than the chain - the heap stops being in the ball's way")
if counts != sorted(counts):
    errors.append(f"the prism ladder must rise ({counts})")
for a, b in zip(counts, counts[1:]):
    if b < a * 1.25:
        errors.append(f"an intensity step adds under 25 % more cover ({a} -> {b}): intensity would not be felt")
if counts[-1] > 8000:
    errors.append(f"I4 lays {counts[-1]} prisms - keep the brawl light")

# The scene: every new identity present, every donor one gone, four Thresher AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("JackstrawController", "JackstrawStrikeTurnMonitor")},
                     "JackstrawSettings": G_ASSET["JackstrawSettings"],
                     "JackstrawScoringRule": G_ASSET["JackstrawScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Serpent Gulag cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {THRESHER_ID}\n") != 4 or f"  - vesselClass: {SERPENT_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Thresher AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Jackstraw"), guid(f"folder/{CELL_DIR}"),
                   guid(f"text/{AR}/JACKSTRAW.md")])
