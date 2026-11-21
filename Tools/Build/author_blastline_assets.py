#!/usr/bin/env python3
"""
Authors every serialized asset the Blastline game mode needs (GameModes.Blastline = 113, hint toast 188 - both reserved
by Docs/ElementalLoop/GAME_KIT_ROSTER.md row 23).

Blastline is the Wrasse and Grouper's SPACE game (Assets/_Scripts/Controller/Arcade/WRASSEREEF.md). The Wrasse's kite
closes its tail round Space motes and a blast goes off where the loop closed (a real AOEExplosion, not anonymous),
destroying hostile prisms and sparing yours; the more motes in the loop, the bigger the blast. Between blasts the kite's
head pops the hostile prisms it touches. Hostile prisms destroyed score (Rampage's rule, through Harvest's templates);
the first DOMAIN to the target wins.

THE ARENA IS RAMPAGE'S LIVING FOREST, referenced read-only (its four rungs, list order = intensity). The forest regrows,
so the field cannot run dry under three domains' targets (Harvest's trap does not apply). The lever is the REEF's
crystals, mostly Space (WrasseReefCrystals): more Space motes on the board, and every Space level also widens the hull's
skim reach. Intensity is Rampage's ladder plus crystal scarcity.

NOTHING ABOUT THE WRASSE CHANGES (the LOCKED vessel rule). The scene is cloned from Harvest's (a PrismsDestroyed race
whose controller declares settings / rule / arenaCell as this one does); the cells and the spawn ring are Rampage's.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_blastline_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import wrasse_reef_common as reef  # noqa: E402

MODE_ID = 113
TOAST_HINT = 188
g = lib.Generator(MODE_ID, "Blastline")
guid = lib.guid

AR = reef.AR
SCRIPT_PATHS = {
    "BlastlineController":        f"{reef.REEF_DIR}/BlastlineController.cs",
    "BlastlineScoringRuleSO":     f"{reef.REEF_DIR}/BlastlineScoringRuleSO.cs",
    "BlastlinePrismTurnMonitor":  f"{AR}/TurnMonitors/BlastlinePrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {n: guid(f"asset/{n}") for n in ("ArcadeGameBlastline", "BlastlineScoringRule", "BlastlineSettings",
                                           "GameToastConfigBlastline", "ModePreviewBlastline",
                                           "MinigameBlastline.unity")}

RAMPAGE_DIR = "Assets/_SO_Assets/Cell Configs/Rampage Cell"
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Wrasse"] = reef.VESSEL_WRASSE
CELLS = [lib.existing_guid(f"{RAMPAGE_DIR}/Rampage Cell Config {i}.asset") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"RampageCellConfig{i}"] = gd

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameHarvest.unity"
DONOR = {
    "HarvestController":       guid("script/HarvestController"),
    "HarvestPrismTurnMonitor": guid("script/HarvestPrismTurnMonitor"),
    "HarvestSettings":         guid("asset/HarvestSettings"),
    "HarvestScoringRule":      guid("asset/HarvestScoringRule"),
}
DONOR_CELLS = [guid(f"asset/HarvestCellConfig{i}") for i in (1, 2, 3, 4)]
THRESHER_ID = lib.VESSEL_CLASS_ID["Thresher"]

# ── The race ─────────────────────────────────────────────────────────────────
PRISM_TARGET = 600        # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultBlastlinePrismTarget
# Comeback - a FUNCTION OF THE TARGET. A quarter behind (150 prisms) buys 1.5 element levels.
COMEBACK_RATE = 0.01
CRYSTALS = [24, 16, 10, 6]
SPACE_SHARE = 0.6
SCATTER_RADIUS = 900

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameBlastline.asset"

# ── 1. metas ─────────────────────────────────────────────────────────────────
reef.emit_shared(g)
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])

# ── 2. Scoring rule (Harvest's, inherited; Blastline's own words) ────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/BlastlineScoringRule.asset", G_ASSET["BlastlineScoringRule"],
             lib.header_for(G_SCRIPT["BlastlineScoringRuleSO"], "BlastlineScoringRule") +
             """  metric: 5
  golfRules: 1
  prismsLeftTemplate: '{count} Prisms Left'
  prismsDestroyedTemplate: '{count} Prisms'
  victoryHeader: VICTORY
  victoryLabel: BLAST TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/BlastlineSettings.asset", G_ASSET["BlastlineSettings"],
             reef.settings_yaml(reef.SHARED_GUIDS["WrasseReefSettingsSO"], "BlastlineSettings", "Space",
                                SPACE_SHARE, CRYSTALS, SCATTER_RADIUS, MODE_ID, 12))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
DESCRIPTION = ("Wrasse and Grouper only - clean the overgrown reef. The Wrasse loops its tail round Space motes and a "
               "blast goes off where the loop closed, destroying hostile prisms and sparing yours; the more motes in the "
               "loop, the bigger the blast. Between blasts the kite's head pops what it touches. Gather Space crystals "
               "and more Space motes come to the board. Higher intensities grow a wilder forest and thin the crystals. "
               "First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameBlastline"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameBlastline") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Blastline
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameBlastline
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Wrasse']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 9
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Blastline.asset", G_ASSET["GameToastConfigBlastline"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Blastline") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has cleared {1} prisms ({1}/{3})", tint_domain=1, domain_names=0, every_n=100) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Loop Space motes - the blast clears every hostile prism where the loop closed",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigBlastline"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
RAMPAGE_RING = 500


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Blastline.asset", G_ASSET["ModePreviewBlastline"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Blastline") + f"""  Mode: {MODE_ID}
  Notes: 'Rampage''s forest at each intensity, the reef the Wrasse cleans. What it teaches is the Space loop and the
    blast where it closes. Vessel pinned to Wrasse({reef.WRASSE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {reef.WRASSE_ID}
  ObjectiveText: Loop Space motes and blast the hostile reef
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: {RAMPAGE_RING}
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewBlastline"])

# ── 7. Scene: Harvest's, with the identity, Rampage's cells and spawn ring and the AI hulls swapped ──
errors = []
CELLS_BLOCK = "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"
DONOR_CELLS_BLOCK = "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n"
SPAWN = (f"  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: {RAMPAGE_RING}\n"
         "  spawnRingRadiusFloor: 0\n  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 900\n  spawnRingRadiusFloorByIntensity:\n"
               "  - 900\n  - 960\n  - 1020\n  - 1080\n  spawnFormation: 0\n")
OMNI_BLOCK = "  noNucleusSpawnRadius: 0\n  crystalCountMode: 2\n"
DONOR_OMNI = "  noNucleusSpawnRadius: 150\n  crystalCountMode: 2\n"


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["HarvestPrismTurnMonitor"], G_SCRIPT["BlastlinePrismTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["HarvestController"], G_SCRIPT["BlastlineController"], "controller")
    scene = lib.swap_guid(scene, DONOR["HarvestSettings"], G_ASSET["BlastlineSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["HarvestScoringRule"], G_ASSET["BlastlineScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, DONOR_CELLS_BLOCK, CELLS_BLOCK, "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {THRESHER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {reef.WRASSE_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI_BLOCK, "omni crystals")
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameBlastline.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [CELLS_BLOCK, SPAWN, OMNI_BLOCK])
errors += scene_errs
g.emit_scene("MinigameBlastline", G_ASSET["MinigameBlastline.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameBlastline"])
g.register_always_unlocked()
g.register_build_scene("MinigameHarvest", "MinigameBlastline", G_ASSET["MinigameBlastline.unity"])
end = g.read_current(lib.END_CONDITIONS)
after = next(k for k in ("reefbuildRoundSeconds", "cleanerRamStrikeTarget", "glasshouseRoundSeconds") if f"{k}:" in end)
g.set_end_condition("blastlinePrismTarget", after=after, value=PRISM_TARGET)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
errors += reef.comeback_error(PRISM_TARGET, COMEBACK_RATE, "prism target")
errors += reef.shared_errors(g, "Blastline", MODE_ID, "BlastlineHint", TOAST_HINT, CRYSTALS)
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultBlastlinePrismTarget = {PRISM_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultBlastlinePrismTarget differs from PRISM_TARGET")
if "Plan.Clean" not in g.read(SCRIPT_PATHS["BlastlineController"]):
    errors.append("BlastlineController no longer arms the Clean plan")
for i in (1, 2, 3, 4):
    cfg = g.read(f"{RAMPAGE_DIR}/Rampage Cell Config {i}.asset")
    if "  NucleusPrefab: {fileID: 0}" in cfg:
        errors.append(f"Rampage Cell Config {i} lost its nucleus - the spawn ring is measured outside it")
errors += reef.scene_identity_errors(
    scene,
    present={**{k: G_SCRIPT[k] for k in ("BlastlineController", "BlastlinePrismTurnMonitor")},
             "BlastlineSettings": G_ASSET["BlastlineSettings"],
             "BlastlineScoringRule": G_ASSET["BlastlineScoringRule"],
             **{f"Rampage cell {i}": gd for i, gd in enumerate(CELLS, 1)}},
    absent={**DONOR, **{f"Harvest cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}})

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + reef.SHARED_MINTED)
