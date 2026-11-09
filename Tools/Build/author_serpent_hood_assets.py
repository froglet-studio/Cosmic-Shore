#!/usr/bin/env python3
"""
Authors every serialized asset Hood needs (GameModes.SerpentHood = 77; was Serpent Gulag, renamed and rebuilt in place on
2026-11-09 by Shombith: "change its names to a better one ... the shapes that we have around the rings in its time game
those can be covers not the weird bases of prisms. Remove the gulag idea entirely").

Hood is the Serpent's CHARGE game - a SNIPER DUEL across a crystal heart. Pilots spawn far apart on a 2,200 u sphere in a
3,600 u cell; a heart of elemental crystals floats in the middle; round it, on a sphere, stand the PERCHES - Serpentine's
ring shapes (SerpentineGate) bent into hoods, every mouth turned to the heart. A sniper sits in a hood's cup and fires out
through its exits; breaking a hood's core brings the whole hood down (Serpentine's rule). A snipe - a Sniper Shot that
strips a rival pilot, already a Debuff-class combat hit - is one point; first DOMAIN to the target wins.

THE LADDER (SerpentHoodLayout.RungFor; the Python mirror is serpent_hood_budget.py, parity-held against the C#):
  I1 Wide Hood   - 8 Coil hoods on 1,500 u: cross-shots near the rifle's 3,000 u reach.
  I2 Spread Hood - 12 Coil hoods on 1,350 u.
  I3 Fanged Hood - 18 Maw hoods on 1,250 u, and 6 shutters (hoods turned across) at 700 u breaking the lines through the heart.
  I4 Closed Hood - 20 Iris hoods on 1,120 u, and 6 shutters at 660 u; the deepest cup, the smallest core.
The hoods are laid by the controller on every peer (closed form), not by a cell environment: a baked environment's prisms
are anonymous, and the core rule needs to know which prisms are which hood's (SerpentTimeController's shapes, the same way).

NOTHING ABOUT THE SERPENT CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the arena,
the heart and the AI.

Every guid keeps its Serpent Gulag seed (guid("script/SerpentGulagController") and so on), so the rename broke no
reference: the scene, the rosters, the build list and the previews still point at the same assets.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_serpent_hood_assets.py [--check]
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import serpent_hood_budget as budget  # noqa: E402

MODE_ID = 77
g = lib.Generator(MODE_ID, "SerpentHood")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
# name -> (path, the seed its guid was minted from). The old seeds are kept on purpose: see the docstring.
SCRIPTS = {
    "SerpentHoodController":       (f"{AR}/SerpentHood/SerpentHoodController.cs", "SerpentGulagController"),
    "SerpentHoodSettingsSO":       (f"{AR}/SerpentHood/SerpentHoodSettingsSO.cs", "SerpentGulagSettingsSO"),
    "SerpentHoodScoringRuleSO":    (f"{AR}/SerpentHood/SerpentHoodScoringRuleSO.cs", "SerpentGulagScoringRuleSO"),
    "SerpentHoodMarksman":         (f"{AR}/SerpentHood/SerpentHoodMarksman.cs", "SerpentGulagMarksman"),
    "SerpentHoodLayout":           (f"{AR}/SerpentHood/SerpentHoodLayout.cs", "SerpentHoodLayout"),
    "SerpentHoodSnipeTurnMonitor": (f"{AR}/TurnMonitors/SerpentHoodSnipeTurnMonitor.cs", "SerpentGulagSnipeTurnMonitor"),
    "SerpentHoodLayoutTests":      ("Assets/_Scripts/Tests/Editor/SerpentHoodLayoutTests.cs", "SerpentHoodLayoutTests"),
}
SCRIPT_PATHS = {k: v[0] for k, v in SCRIPTS.items()}
G_SCRIPT = {k: guid(f"script/{v[1]}") for k, v in SCRIPTS.items()}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Serpent Hood Cell"
G_ASSET = {
    "ArcadeGameSerpentHood":      guid("asset/ArcadeGameSerpentGulag"),
    "SerpentHoodScoringRule":     guid("asset/SerpentGulagScoringRule"),
    "SerpentHoodSettings":        guid("asset/SerpentGulagSettings"),
    "GameToastConfigSerpentHood": guid("asset/GameToastConfigSerpentGulag"),
    "ModePreviewSerpentHood":     guid("asset/ModePreviewSerpentGulag"),
    "MinigameSerpentHood.unity":  guid("asset/MinigameSerpentGulag.unity"),
    "SerpentHoodSpawnProfile":    guid("asset/SerpentGulagSpawnProfile"),
}
for i in (1, 2, 3, 4):
    G_ASSET[f"SerpentHoodCellConfig{i}"] = guid(f"asset/SerpentGulagCellConfig{i}")
G_FOLDERS = {f"{AR}/SerpentHood": guid(f"folder/{AR}/SerpentGulag"),
             CELL_DIR: guid("folder/Assets/_SO_Assets/Cell Configs/Serpent Gulag Cell")}
G_DOC = guid(f"text/{AR}/SERPENTGULAG.md")

# What the rebuild retired: Serpent Gulag's nests, cell blocks and rubble (SpawnableSerpentGulag and its baked prefabs).
# On the stale list, so a stray copy brought back by a merge is removed by a run and named by --check.
STALE = (["Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableSerpentGulag.cs",
          "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableSerpentGulag.cs.meta"]
         + [f"Assets/_Prefabs/Spawnables/SpawnableSerpentGulag{i}.prefab{m}" for i in (2, 3, 4) for m in ("", ".meta")])
g.stale.extend(STALE)

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Serpent"] = lib.VESSELS["Serpent"]
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"           # SpawnablePrism.prefab (window 0.5-100)
EXISTING["CleaveMembrane_prefab"] = "6eb45784f9024cbb879f94c23ae46bd9"  # the x3 shell, radius 3,600
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
SNIPER_SHOT = "Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset"
EXISTING["SniperShotAction"] = lib.existing_guid(SNIPER_SHOT)
EXISTING["SerpentineGate"] = lib.existing_guid(f"{AR}/SerpentHood/SerpentineGate.cs")
# The donor scene's identity, minted by author_dustup_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameDustup.unity"
DONOR = {
    "DustupController":       guid("script/DustupController"),
    "DustupPointTurnMonitor": guid("script/DustupPointTurnMonitor"),
    "DustupSettings":         guid("asset/DustupSettings"),
    "DustupScoringRule":      guid("asset/DustupScoringRule"),
}
BONEYARD = [lib.existing_guid(f"Assets/_SO_Assets/Cell Configs/Boneyard Cell/Boneyard Cell Config {i}.asset")
            for i in range(1, 5)]
TIGER_MOTH_ID = lib.VESSEL_CLASS_ID["TigerMoth"]
SERPENT_ID = lib.VESSEL_CLASS_ID["Serpent"]

PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261

# ── The duel ─────────────────────────────────────────────────────────────────
SNIPE_TARGET = 3          # Shombith, 2026-10-31: "make the score to 3 shots not 6"; kept in sync with EndConditionOverridesSO.DefaultSerpentHoodSnipeTarget (checked)
SNIPE_POINTS = 1
DEBUFF_CLASS = 2          # CombatHitClass.Debuff - what SniperShotAction reports its pilot strip as
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (0.75 snipes) buys
# 1.125 element levels; on this hull a Charge level is a faster rifle.
COMEBACK_RATE = 1.5

# ── The arena (Cleave's x3 membrane at every rung) ───────────────────────────
MEMBRANE_RADIUS = 3600.0
CYTOPLASM_SHARD_DISTANCE = round(120 * MEMBRANE_RADIUS / 1200)   # keeps a standard cell's shard count
SPAWN_RING = 2200          # four pilots on a tetrahedron (spawnFormation 0): 3,593 u apart, past the rifle's 3,000
FIELD_RADIUS = 380.0       # the crystal heart; the omni crystal spawns inside it too
FIELD_COUNT = 24
RIFLE_RANGE = 3000

RUNG_BLURBS = {
    1: "eight Coil hoods on a 1,500 u sphere round the crystal heart, every mouth turned to it; shots across reach "
       "for the rifle's 3,000 u",
    2: "twelve Coil hoods on 1,350 u, flared wider, the cups deeper",
    3: "eighteen Maw hoods on 1,250 u, and six shutters (hoods turned across) at 700 u breaking the lines through "
       "the heart",
    4: "twenty Iris hoods on 1,120 u and six shutters at 660 u - the deepest cups, the smallest cores, every shot "
       "under 2,300 u",
}

# Cleave's ladder deltas (cleave_budget.BLOB_DELTAS): a cell's phase rides ITS arena's own baseline. This mode has
# no fauna, so the phase only matters as the cell's own state, but it must not start a match in the wrong one.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)


def thresholds(count, volume):
    return dict(RestlessEnter=count + DELTAS["re"], RestlessExit=count + DELTAS["rx"],
                FrenzyEnter=count + DELTAS["fe"], FrenzyExit=count + DELTAS["fx"],
                RestlessEnterVolume=round(volume + DELTAS["rev"]), RestlessExitVolume=round(volume + DELTAS["rxv"]),
                FrenzyEnterVolume=round(volume + DELTAS["fev"]), FrenzyExitVolume=round(volume + DELTAS["fxv"]))


CENSUS = {i: budget.census(i) for i in (1, 2, 3, 4)}
CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameSerpentHood.asset"

# ── 1. .meta for the scripts, the folders and the doc (each keeps its first guid) ──
for k, path in SCRIPT_PATHS.items():
    g.script_meta(path, G_SCRIPT[k])
for rel_dir, gd in G_FOLDERS.items():
    g.emit(rel_dir + ".meta", f"fileFormatVersion: 2\nguid: {gd}\nfolderAsset: yes\nDefaultImporter:\n"
                              f"  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
g.emit(f"{AR}/SERPENTHOOD.md.meta", f"fileFormatVersion: 2\nguid: {G_DOC}\nTextScriptImporter:\n"
                                     f"  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/SerpentHoodScoringRule.asset", G_ASSET["SerpentHoodScoringRule"],
             lib.header_for(G_SCRIPT["SerpentHoodScoringRuleSO"], "SerpentHoodScoringRule") +
             f"""  metric: 8
  golfRules: 1
  snipePoints: {SNIPE_POINTS}
  otherHitPoints: 0
  snipesLeftTemplate: '{{count}} Snipes Left'
  snipesLandedTemplate: '{{count}} snipes'
  victoryHeader: VICTORY
  victoryLabel: HOOD TIME
  defeatHeader: DEFEAT
  defeatLabel: SNIPES LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
# Difficulty is the OPPONENT, intensity the MAP (AIDifficulty.cs): the marksman reads each seat's own level.
LADDERS = {
    # name: (values, "up" = harder means larger, "down" = harder means smaller, "style" = the patient sniper rises
    # Easy -> Hard and Legendary (aggressive, Shombith 2026-10-31) drops below Easy)
    "aiRetargetSeconds":          ([1.2, 0.9, 0.6, 0.4], "down"),
    "aiAimErrorDegrees":          ([3.0, 2.0, 1.2, 0.5], "down"),
    "aiScopeSettleSeconds":       ([1.0, 0.6, 0.3, 0.15], "down"),
    "aiHuntRange":                ([1500, 2000, 2500, 3000], "up"),
    "aiForageMinCooldownSeconds": ([999, 7, 5, 3.5], "down"),
    # The sniper's discipline (Shombith, 2026-10-31: "keep distance as a sniper ... build its defenses ... not rush").
    # Legendary "can play aggressively": it pushes to 900 u, breaks off only inside 350 u, and digs in for 5 s.
    "aiPreferredRange":           ([1400, 1700, 2000, 900], "style"),
    "aiKeepAwayDistance":         ([700, 900, 1100, 350], "style"),
    "aiHoldSeconds":              ([8, 12, 16, 5], "style"),
    "aiCloakAfterShotChance":     ([0, 0.25, 0.5, 0.75], "up"),
}
HOOD_BURST_PER_FRAME, HOOD_BURST_SPEED, HOOD_REGROW_SECONDS, HOOD_REGROW_PER_FRAME = 48, 40, 25, 40   # Serpentine's
AI_CUP_DEPTH, AI_RIM_CLEARANCE = 45, 80


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/SerpentHoodSettings.asset", G_ASSET["SerpentHoodSettings"],
             lib.header_for(G_SCRIPT["SerpentHoodSettingsSO"], "SerpentHoodSettings") +
             f"""  progressSampleSeconds: 0.5
  fieldCrystalCount: {FIELD_COUNT}
  fieldRadius: {lib.num(FIELD_RADIUS)}
  fieldRegrowSeconds: 8
  fieldSeed: 77
  hoodBurstPerFrame: {HOOD_BURST_PER_FRAME}
  hoodBurstSpeed: {HOOD_BURST_SPEED}
  hoodRegrowSeconds: {HOOD_REGROW_SECONDS}
  hoodRegrowPerFrame: {HOOD_REGROW_PER_FRAME}
""" + "".join(ladder_yaml(n) for n in LADDERS) + f"""  aiPostArrivalRadius: 70
  aiCupDepth: {AI_CUP_DEPTH}
  aiRimClearance: {AI_RIM_CLEARANCE}
  aiRangeBand: 350
  aiStanceRetrySeconds: 1
  aiScopeOpenAngle: 15
  aiShotGiveUpSeconds: 3
  aiLeadSeconds: 0.25
  aiHumanFocus: 2
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: the rifle never strips a team-mate, so a solo lobby has nobody to score on.
DESCRIPTION = ("Serpents only - a sniper duel across a crystal heart. Hoods stand round the heart, every mouth "
               "turned to it - sit in a hood's cup and fire out through its gaps. Charge reloads your rifle faster, so "
               "the heart is where you load up, in every hood's sights. Hold LT to scope and RT to fire - a round that "
               "strips a rival pilot is one snipe. Break a hood's core and the whole hood falls, flushing whoever hid "
               "in it. Higher intensities pull the hoods in, deepen their cups and stand shutters across the heart. "
               "First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameSerpentHood"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameSerpentHood") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Hood
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameSerpentHood
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Serpent']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_SerpentHood.asset", G_ASSET["GameToastConfigSerpentHood"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_SerpentHood") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> sniped a rival!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} snipes", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} snipes", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(157, "Sit in a hood's cup - LT to scope, RT to fire. The crystal heart reloads you faster",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigSerpentHood"])

# ── 6. The arena: four cell configs (no environment prefab: the controller lays the hoods), one spawn profile ──
for i in (1, 2, 3, 4):
    count, danger, volume, covers = CENSUS[i]
    th = thresholds(count, round(volume))
    desc = (f"{budget.LEVEL_NAMES[i]} - intensity {i} of Hood: {RUNG_BLURBS[i]}. {count} prisms in {covers} hoods "
            f"({danger} of them the danger weave), all neutral, laid by SerpentHoodController on every peer "
            f"(SerpentHoodLayout), not by this cell. Membrane radius {int(MEMBRANE_RADIUS)} (Cleave's x3 shell), spawn "
            f"sphere {SPAWN_RING}, the crystal heart (radius {int(FIELD_RADIUS)}) at every rung. NO NUCLEUS, no flora, no "
            f"fauna. Authored by Tools/Build/author_serpent_hood_assets.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Serpent Hood Cell Config {i}.asset", G_ASSET[f"SerpentHoodCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Serpent Hood Cell Config {i}") + f"""  CellName: Hood
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CleaveMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: {CYTOPLASM_SHARD_DISTANCE}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['SerpentHoodSpawnProfile']}, type: 2}}
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

# One spawn profile, shared by all four configs: it spawns nothing (no flora, no fauna).
g.emit_asset(f"{CELL_DIR}/Serpent Hood Spawn Profile.asset", G_ASSET["SerpentHoodSpawnProfile"],
             lib.header_for(EXISTING["SpawnProfileSO"], "Serpent Hood Spawn Profile") + """  FloraExcludeLocalDomain: 0
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

CELLS = [G_ASSET[f"SerpentHoodCellConfig{i}"] for i in (1, 2, 3, 4)]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 700\n  spawnFormation: 0\n")
NO_NUCLEUS = f"  noNucleusSpawnRadius: {int(FIELD_RADIUS)}\n"

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_SerpentHood.asset", G_ASSET["ModePreviewSerpentHood"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_SerpentHood") + f"""  Mode: {MODE_ID}
  Notes: 'The Hood cell at each intensity - hoods round a crystal heart, pulled in and deepened up the ladder.
    OPEN-ENDED - it scores on sniping rival pilots and a solo preview has none; what it teaches is the scope, the
    rifle and the heart. The hoods are laid by the match controller, so a preview shows the cell alone.
    Vessel pinned to Serpent({SERPENT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SERPENT_ID}
  ObjectiveText: Scope with LT, fire with RT - snipe a rival pilot
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewSerpentHood"])

# ── 8. Scene: Dustup's, with the identity, the cells, the AI hulls and the spawn ring swapped ──
errors = []
ARENA_CELL = "  arenaCell: {fileID: 1700000065}\n"
HOOD_PRISM = f"  hoodPrismPrefab: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}\n"
CONTROLLER_TAIL = ARENA_CELL + HOOD_PRISM


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["DustupPointTurnMonitor"], G_SCRIPT["SerpentHoodSnipeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["DustupController"], G_SCRIPT["SerpentHoodController"], "controller")
    scene = lib.swap_guid(scene, DONOR["DustupSettings"], G_ASSET["SerpentHoodSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["DustupScoringRule"], G_ASSET["SerpentHoodScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + cell_list(BONEYARD) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {TIGER_MOTH_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SERPENT_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, "  noNucleusSpawnRadius: 420\n", NO_NUCLEUS, "crystal volume")
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameSerpentHood.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [
    "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, NO_NUCLEUS])
# One-shot migration (2026-11-09): the controller gained hoodPrismPrefab, the prism every hood is laid from. It enters
# the committed scene here, after the controller's arenaCell, and stands down once it is there.
controller_block = f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SerpentHoodController']}, type: 3}}\n"
if scene is not None and HOOD_PRISM not in scene:
    at = scene.find(controller_block)
    tail = scene.find(ARENA_CELL, at) if at >= 0 else -1
    if tail < 0:
        errors.append("the scene's controller block has no arenaCell line to put hoodPrismPrefab after")
    else:
        scene = scene[:tail] + CONTROLLER_TAIL + scene[tail + len(ARENA_CELL):]
if scene is not None and CONTROLLER_TAIL not in scene:
    errors.append("the scene's controller does not carry hoodPrismPrefab after arenaCell")
g.emit_scene("MinigameSerpentHood", G_ASSET["MinigameSerpentHood.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
# One-shot migrations for the rename (2026-11-09), standing down once applied: the build list's scene path and the
# end-condition keys moved from the Serpent Gulag names. The C# field carries [FormerlySerializedAs] for any copy that
# was not migrated.
build = g.read_current(lib.BUILD_SETTINGS)
if "MinigameSerpentGulag.unity" in build:
    g.emit(lib.BUILD_SETTINGS, build.replace("MinigameSerpentGulag.unity", "MinigameSerpentHood.unity"))
endcond = g.read_current(lib.END_CONDITIONS)
if "serpentGulagSnipeTarget" in endcond:
    g.emit(lib.END_CONDITIONS, endcond.replace("serpentGulagSnipeTarget", "serpentHoodSnipeTarget"))
g.register_arcade_card(G_ASSET["ArcadeGameSerpentHood"])
g.register_always_unlocked()
g.register_build_scene("MinigameDustup", "MinigameSerpentHood", G_ASSET["MinigameSerpentHood.unity"])
g.set_end_condition("serpentHoodSnipeTarget", after="dustupPointTarget", value=SNIPE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * SNIPE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {SNIPE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * SNIPE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumption: the rifle still reports its pilot strip as Debuff, the class the rule pays.
shot = g.read(SNIPER_SHOT)
if f"  hitClass: {DEBUFF_CLASS}\n" not in shot:
    errors.append("SniperShotAction no longer reports a Debuff-class hit - SerpentHoodScoringRuleSO pays Debuff")
if f"  rangeUnits: {RIFLE_RANGE}\n" not in shot:
    errors.append(f"the rifle's range moved off {RIFLE_RANGE} - re-check the perch ladder and the AI's hunt ladder")
if "  coneHalfAngleDegrees: 1.5\n" not in shot:
    errors.append("the rifle's cone moved off 1.5 deg - re-check the aim-error ladder")

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultSerpentHoodSnipeTarget = {SNIPE_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultSerpentHoodSnipeTarget differs from SNIPE_TARGET")

# The ladders: each monotone toward harder; Legendary's aim inside the 1.5 deg cone, Easy's outside; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["SerpentHoodSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    if harder == "style" and (any(b < a for a, b in pairs[:2]) or not values[3] < values[0]):
        errors.append(f"{name}: the patient sniper must rise Easy -> Hard and Legendary (aggressive) sit below Easy ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for field, value in (("hoodBurstPerFrame", HOOD_BURST_PER_FRAME), ("hoodRegrowPerFrame", HOOD_REGROW_PER_FRAME)):
    if f"{field} = {value};" not in settings_cs:
        errors.append(f"{field}: the C# initializer is not {value}")
for field, value in (("hoodBurstSpeed", HOOD_BURST_SPEED), ("hoodRegrowSeconds", HOOD_REGROW_SECONDS),
                     ("aiCupDepth", AI_CUP_DEPTH), ("aiRimClearance", AI_RIM_CLEARANCE)):
    if f"{field} = {value}f;" not in settings_cs:
        errors.append(f"{field}: the C# initializer is not {value}f")
aim = LADDERS["aiAimErrorDegrees"][0]
if not (aim[3] < 1.5 < aim[0]):
    errors.append("aim error ladder must put Legendary inside the 1.5 deg cone and Easy outside it")

# The arena the duel promises - read from the Python mirror, which serpent_hood_budget.py --parity holds to the C#.
layout_cs = g.read(SCRIPT_PATHS["SerpentHoodLayout"])
for i, (n, pr, m, sr, wide, narrow, cup) in budget.RUNGS.items():
    lead = "_" if i == 4 else str(i)
    line = (f"{lead} => new Rung({n}, {lib.num(pr)}f, {m}, {lib.num(sr)}f, {wide:.2f}f, {narrow:.2f}f, {cup:.2f}f),")
    if line not in layout_cs:
        errors.append(f"SerpentHoodLayout.RungFor's I{i} row is not the mirror's ({line}) - "
                      f"update serpent_hood_budget.RUNGS with it and run --parity")
if f"CoverRingRadius = {lib.num(budget.COVER_R)}f;" not in layout_cs:
    errors.append("SerpentHoodLayout.CoverRingRadius differs from the mirror's COVER_R")
counts = []
for i in (1, 2, 3, 4):
    count, _, _, _ = CENSUS[i]
    counts.append(count)
    n, pr, m, sr = budget.RUNGS[i][:4]
    reach = max(budget.build_cover(k, c, ax, i)[1] for k, c, ax, _ in budget.covers(i))
    if 2 * pr > RIFLE_RANGE:
        errors.append(f"I{i}: perches {2 * pr:.0f} u across are past the rifle's {RIFLE_RANGE} u")
    if pr + reach > SPAWN_RING - 300:
        errors.append(f"I{i}: a hood reaches within 300 u of the spawn sphere")
    if m and sr - reach < FIELD_RADIUS + 60:
        errors.append(f"I{i}: a shutter reaches into the crystal heart")
if counts != sorted(counts) or counts[-1] > 8000:
    errors.append(f"the prism ladder must rise and stay under 8,000 ({counts})")
if not (SPAWN_RING + 600 < MEMBRANE_RADIUS):
    errors.append("the spawn sphere is too close to the membrane")
tetra_edge = SPAWN_RING * math.sqrt(8 / 3)
if tetra_edge <= RIFLE_RANGE:
    errors.append(f"spawns ({tetra_edge:.0f} u apart) are inside the rifle's {RIFLE_RANGE} u - spawns must be FAR apart")
ctrl = g.read(SCRIPT_PATHS["SerpentHoodController"])
if f'HoodOwnerId = "SerpentHood"' not in ctrl or "if (!hood.Parts[k].Core) break;" not in ctrl:
    errors.append("SerpentHoodController no longer runs the core rule this generator documents")

# The sniper's discipline: a post inside the rifle's reach and the hunt range, and a keep-away that leaves room for
# the band the AI digs in within.
pref, keep, hunt = (LADDERS[k][0] for k in ("aiPreferredRange", "aiKeepAwayDistance", "aiHuntRange"))
LEVELS = ("Easy", "Medium", "Hard", "Legendary")
for i in range(4):
    if not (pref[i] <= hunt[i] and pref[i] <= RIFLE_RANGE):
        errors.append(f"{LEVELS[i]}: preferred range {pref[i]} is past the hunt range or the rifle's reach")
    if not (keep[i] + 350 <= pref[i]):
        errors.append(f"{LEVELS[i]}: keep-away {keep[i]} leaves no band below the preferred range {pref[i]}")
# The marksman reads the seat's difficulty, never the intensity (AIDifficulty.cs).
marksman = g.read(SCRIPT_PATHS["SerpentHoodMarksman"])
if "SelectedIntensity" in marksman or "AIDifficultyFor(" not in marksman:
    errors.append("SerpentHoodMarksman must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")

# The gulag is gone: no shipped file of this mode names it.
for k, path in SCRIPT_PATHS.items():
    if re.search(r"(?i)\bnests?\b|rubble|cell block", g.read(path)):
        errors.append(f"{path} still talks about Serpent Gulag's nests / rubble / cell blocks")

# The scene: every new identity present, every donor one gone, four Serpent AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("SerpentHoodController", "SerpentHoodSnipeTurnMonitor")},
                     "SerpentHoodSettings": G_ASSET["SerpentHoodSettings"],
                     "SerpentHoodScoringRule": G_ASSET["SerpentHoodScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Boneyard {i}": gd for i, gd in enumerate(BONEYARD, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {SERPENT_ID}\n") != 4 or f"  - vesselClass: {TIGER_MOTH_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Serpent AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + list(G_FOLDERS.values()) + [G_DOC])
