#!/usr/bin/env python3
"""
Authors every serialized asset the Depth Charge game mode needs (GameModes.DepthCharge = 101, hint toast 176).

Depth Charge is the Orca's SPACE game (the game kit roster, Docs/ElementalLoop/GAME_KIT_ROSTER.md row 11): a raid on
the rival forest. Rampage's arena is a grown forest in all three colours; every HOSTILE prism your domain breaks -
missiles and the bomb burst on contact, the guns pick at the rest - counts. The first DOMAIN to the target wins,
golf-timed.

NOTHING ABOUT THE ORCA CHANGES (the LOCKED vessel rule). The arena is Rampage's four cells, referenced (the forest
regrows on its own spawner). The gunners shoot on their own (OrcaCrewAI); the mode's AI is the helm alone
(OrcaArcadeHelm, goal Raid), which flies each AI hull to the nearest plant not in its colour. The scene is cloned
from Harvest's (the newest PrismsDestroyed race, whose controller declares settings / rule / arenaCell as this one
does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_depth_charge_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import orca_games_common as orca  # noqa: E402

MODE_ID = 101
TOAST_HINT = 176
g = lib.Generator(MODE_ID, "DepthCharge")
guid = lib.guid
AR = orca.AR

SCRIPT_PATHS = {
    "DepthChargeController":       f"{AR}/DepthCharge/DepthChargeController.cs",
    "DepthChargeScoringRuleSO":    f"{AR}/DepthCharge/DepthChargeScoringRuleSO.cs",
    "DepthChargePrismTurnMonitor": f"{AR}/TurnMonitors/DepthChargePrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_SCRIPT.update(orca.script_guids())
G_ASSET = {
    "ArcadeGameDepthCharge":      guid("asset/ArcadeGameDepthCharge"),
    "DepthChargeScoringRule":     guid("asset/DepthChargeScoringRule"),
    "DepthChargeSettings":        guid("asset/DepthChargeSettings"),
    "GameToastConfigDepthCharge": guid("asset/GameToastConfigDepthCharge"),
    "ModePreviewDepthCharge":     guid("asset/ModePreviewDepthCharge"),
    "MinigameDepthCharge.unity":  guid("asset/MinigameDepthCharge.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Orca"] = orca.VESSEL_ORCA
# Rampage's four cells (the shipped demolition forest; intensity 4 is the play-tested arena exactly).
CELLS = ["fc20698b2b983f9e1e9c20733ea92760", "398abbfc433510307e154b76aa4191b4",
         "789e13b3381f614d997ba4b7a830ff22", "c6959b0e548d4f26bdde820ca48ac26e"]
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

# ── The raid ─────────────────────────────────────────────────────────────────
PRISM_TARGET = 1500       # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultDepthChargePrismTarget
# Comeback - a FUNCTION OF THE TARGET. A quarter of the race behind (375 prisms) buys 2.25 element levels, Harvest's
# scale (0.012 against 750) carried to this target.
COMEBACK_RATE = 0.006
TOAST_EVERY = 250

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameDepthCharge.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = orca.emit_shared_metas(g)
g.folder_meta(f"{AR}/DepthCharge")
g.text_meta(f"{AR}/DEPTHCHARGE.md")

# ── 2. Scoring rule (Harvest's, inherited; Depth Charge's own words) ─────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/DepthChargeScoringRule.asset", G_ASSET["DepthChargeScoringRule"],
             lib.header_for(G_SCRIPT["DepthChargeScoringRuleSO"], "DepthChargeScoringRule") +
             """  metric: 5
  golfRules: 1
  prismsLeftTemplate: '{count} Prisms Left'
  prismsDestroyedTemplate: '{count} Prisms'
  victoryHeader: VICTORY
  victoryLabel: RAID TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/DepthChargeSettings.asset", G_ASSET["DepthChargeSettings"],
             orca.settings_yaml("DepthChargeSettings"))

# ── 4. Arcade card (1/2: a race against the arena; another colour must be in the forest to raid) ──
DESCRIPTION = ("Orcas only - a raid on the rival forest. The arena is a grown forest in every colour, and every rival "
               "prism your team breaks counts: fly a missile into a plant, throw the bomb into a cluster, and let the "
               "guns pick at what is left. Your own colour is not your score but it is your cover. Omni crystals "
               "reload every gunner seat. Higher intensities thin the forest. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameDepthCharge"],
             orca.card_yaml(EXISTING, "ArcadeGameDepthCharge", MODE_ID, "Depth Charge", DESCRIPTION, CARD_REL,
                            "MinigameDepthCharge", golf=True, min_players=1, min_domains=2, comeback=COMEBACK_RATE))

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_DepthCharge.asset", G_ASSET["GameToastConfigDepthCharge"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_DepthCharge") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has broken {1} rival prisms ({1}/{3})", tint_domain=1, domain_names=0,
                       every_n=TOAST_EVERY) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Only rival colours count - fly a missile into their plants and throw the bomb "
                       "into a cluster", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigDepthCharge"])

# ── 6. The scene's spawn ring: Rampage's (500 u outside the nucleus, no floor) ──
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 500\n"
         "  spawnRingRadiusFloor: 0\n  spawnRingRadiusFloorByIntensity: []\n  spawnFormation: 0\n")
DONOR_RINGS = [900, 960, 1020, 1080]
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               f"  spawnRingRadiusFloor: {DONOR_RINGS[0]}\n  spawnRingRadiusFloorByIntensity:\n"
               + "".join(f"  - {v}\n" for v in DONOR_RINGS) + "  spawnFormation: 0\n")

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_DepthCharge.asset", G_ASSET["ModePreviewDepthCharge"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_DepthCharge") + f"""  Mode: {MODE_ID}
  Notes: 'Rampage''s forest at each intensity, thinning from I1 to I4, grown by the cell''s own spawner.
    What it teaches is the raid - a missile into a plant of another colour, the bomb into a cluster.
    Vessel pinned to Orca({orca.ORCA_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{orca.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {orca.ORCA_ID}
  ObjectiveText: Fly a missile into a rival plant, and throw the bomb into a cluster
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 500
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewDepthCharge"])

# ── 8. Scene: Harvest's, with the identity, the cells, the AI hulls and the spawn ring swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["HarvestPrismTurnMonitor"], G_SCRIPT["DepthChargePrismTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["HarvestController"], G_SCRIPT["DepthChargeController"], "controller")
    scene = lib.swap_guid(scene, DONOR["HarvestSettings"], G_ASSET["DepthChargeSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["HarvestScoringRule"], G_ASSET["DepthChargeScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + orca.cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + orca.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = orca.swap_ai_templates(scene, THRESHER_ID)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + orca.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameDepthCharge.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameDepthCharge", G_ASSET["MinigameDepthCharge.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameDepthCharge"])
g.register_always_unlocked()
g.register_build_scene("MinigameHarvest", "MinigameDepthCharge", G_ASSET["MinigameDepthCharge.unity"])
g.set_end_condition("depthChargePrismTarget", after="glasshouseRoundSeconds", value=PRISM_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
orca.validate_shared(g, errors)
orca.validate_enums(g, errors, "DepthCharge", MODE_ID, "DepthChargeHint", TOAST_HINT)
if f"DefaultDepthChargePrismTarget = {PRISM_TARGET};" not in g.read(
        "Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultDepthChargePrismTarget differs from PRISM_TARGET")
harvest_rule = g.read(f"{AR}/Harvest/HarvestScoringRuleSO.cs")
for field in ("prismsLeftTemplate", "prismsDestroyedTemplate", "victoryHeader", "victoryLabel", "defeatHeader",
              "defeatLabel"):
    if f"string {field}" not in harvest_rule:
        errors.append(f"HarvestScoringRuleSO no longer serializes '{field}' - DepthChargeScoringRule.asset fills it")
monitor = g.read(SCRIPT_PATHS["DepthChargePrismTurnMonitor"])
if "GetDepthChargePrismTarget()" not in monitor or "IsObjectiveReached(gameData" not in monitor:
    errors.append("DepthChargePrismTurnMonitor no longer resolves its own target and ends through the rule")
# The forest is what the race spends: it must be in more than one colour (only rival prisms count) and must regrow.
for i in (1, 2, 3, 4):
    cfg = g.read(f"Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config {i}.asset")
    if "across all three\n    domains" not in cfg and "across all three domains" not in cfg.replace("\n    ", " "):
        errors.append(f"Rampage Cell Config {i} no longer grows a forest in all three domains - nothing to raid")

orca.validate_scene(errors, scene,
                    present={**{k: G_SCRIPT[k] for k in ("DepthChargeController", "DepthChargePrismTurnMonitor")},
                             "DepthChargeSettings": G_ASSET["DepthChargeSettings"],
                             "DepthChargeScoringRule": G_ASSET["DepthChargeScoringRule"],
                             **{f"Rampage cell {i}": gd for i, gd in enumerate(CELLS, 1)}},
                    gone={**DONOR, **{f"Harvest cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}},
                    donor_class=THRESHER_ID)

g.finish(errors, referenced=EXISTING,
         minted=[G_SCRIPT[k] for k in SCRIPT_PATHS] + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/DepthCharge"), guid(f"text/{AR}/DEPTHCHARGE.md")])
