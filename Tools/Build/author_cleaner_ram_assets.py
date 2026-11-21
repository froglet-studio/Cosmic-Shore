#!/usr/bin/env python3
"""
Authors every serialized asset the Cleaner Ram game mode needs (GameModes.CleanerRam = 111, hint toast 186 - both
reserved by Docs/ElementalLoop/GAME_KIT_ROSTER.md row 21).

Cleaner Ram is the Wrasse and Grouper's CHARGE game (Assets/_Scripts/Controller/Arcade/WRASSEREEF.md). The Wrasse's
kite loops its tail round Charge motes to power its ram; a charged ram on a rival hull is a Strike-class combat hit
(KiteNavigator.Steal) that knocks petals loose. Every RAM is one point; the first DOMAIN to the target wins.

THE ARENA IS THE BARE BARREN CELL, referenced read-only (Tapestry's): open water, so the hunt is the Grouper putting a
rival under the kite's board. The game's lever is the REEF's crystals (WrasseReefCrystals): mostly Charge, so a pilot who
gathers them sees more Charge motes on the board (KiteNavigator.PickElement reads the hull's element levels). Intensity
is scarcity (24 -> 6 crystals). Omni crystals (the scene's CrystalManager) drop motes of every element (Kite Omni).

NOTHING ABOUT THE WRASSE CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The scene is cloned from Kelpwood's (a
CombatPoints race whose controller declares settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_cleaner_ram_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import wrasse_reef_common as reef  # noqa: E402

MODE_ID = 111
TOAST_HINT = 186
g = lib.Generator(MODE_ID, "CleanerRam")
guid = lib.guid

AR = reef.AR
SCRIPT_PATHS = {
    "CleanerRamController":        f"{reef.REEF_DIR}/CleanerRamController.cs",
    "CleanerRamScoringRuleSO":     f"{reef.REEF_DIR}/CleanerRamScoringRuleSO.cs",
    "CleanerRamStrikeTurnMonitor": f"{AR}/TurnMonitors/CleanerRamStrikeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {n: guid(f"asset/{n}") for n in ("ArcadeGameCleanerRam", "CleanerRamScoringRule", "CleanerRamSettings",
                                           "GameToastConfigCleanerRam", "ModePreviewCleanerRam",
                                           "MinigameCleanerRam.unity")}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Wrasse"] = reef.VESSEL_WRASSE
EXISTING["BarrenCellConfig"] = lib.existing_guid(reef.BARREN_CELL)

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

# ── The hunt ─────────────────────────────────────────────────────────────────
RAM_TARGET = 8            # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultCleanerRamStrikeTarget
RAM_POINTS = 1
# Comeback - a FUNCTION OF THE TARGET. A quarter of the race behind (2 rams) buys 1.2 element levels; on the Wrasse every
# level is more of its element's motes on the board, so a trailing pilot gets more Charge to loop.
COMEBACK_RATE = 0.6
CRYSTALS = [24, 16, 10, 6]
CHARGE_SHARE = 0.6
OMNI = [3, 2, 2, 1]
SCATTER_RADIUS = 900

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameCleanerRam.asset"

# ── 1. metas ─────────────────────────────────────────────────────────────────
reef.emit_shared(g)
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])

# ── 2. Scoring rule (Jackstraw's, inherited; Cleaner Ram's own words) ────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/CleanerRamScoringRule.asset", G_ASSET["CleanerRamScoringRule"],
             lib.header_for(G_SCRIPT["CleanerRamScoringRuleSO"], "CleanerRamScoringRule") +
             f"""  metric: 8
  golfRules: 1
  strikePoints: {RAM_POINTS}
  otherHitPoints: 0
  strikesLeftTemplate: '{{count}} Rams Left'
  strikesLandedTemplate: '{{count}} rams'
  victoryHeader: VICTORY
  victoryLabel: CLEANING TIME
  defeatHeader: DEFEAT
  defeatLabel: RAMS LEFT
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/CleanerRamSettings.asset", G_ASSET["CleanerRamSettings"],
             reef.settings_yaml(reef.SHARED_GUIDS["WrasseReefSettingsSO"], "CleanerRamSettings", "Charge",
                                CHARGE_SHARE, CRYSTALS, SCATTER_RADIUS, MODE_ID, 12))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a ram never takes a team-mate, so a solo lobby has nobody to ram.
DESCRIPTION = ("Wrasse and Grouper only - a reef of Charge crystals in open water. The Wrasse loops its tail round Charge "
               "motes to power its ram; the Grouper flies a rival under the kite's board, and a charged ram knocks their "
               "petals loose. Gather Charge crystals and more Charge motes come to the board. Every ram is a point. Higher "
               "intensities thin the crystals. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameCleanerRam"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameCleanerRam") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Cleaner Ram
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameCleanerRam
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Wrasse']}, type: 2}}
  MinPlayersAllowed: 2
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_CleanerRam.asset", G_ASSET["GameToastConfigCleanerRam"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_CleanerRam") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(86, "<b>{0}</b> rammed a rival - petals knocked loose!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} rams", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} rams", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Loop Charge motes to power the kite, then fly a rival under it and ram", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigCleanerRam"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_CleanerRam.asset", G_ASSET["ModePreviewCleanerRam"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_CleanerRam") + f"""  Mode: {MODE_ID}
  Notes: 'The bare Barren cell the mode runs in. OPEN-ENDED - it scores on ramming rival pilots and a solo
    preview has none; what it teaches is the Charge loop that powers the ram. Vessel pinned to Wrasse({reef.WRASSE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['BarrenCellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {reef.WRASSE_ID}
  ObjectiveText: Loop Charge motes, then ram a rival on the kite's board
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 150
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewCleanerRam"])

# ── 7. Scene: Kelpwood's, with the identity, the cell, the AI hulls, the spawn ring and the crystals swapped ──
errors = []


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


CELLS_BLOCK = "  CellConfigs:\n" + cell_list([EXISTING["BarrenCellConfig"]]) + "  cellTypeChoiceOptions: 0\n"
DONOR_CELLS_BLOCK = "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n"
# Barren HAS a nucleus: 150 outside it, symmetric (Tapestry's ring).
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 150\n"
         "  spawnRingRadiusFloor: 0\n  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 368\n  spawnRingRadiusFloorByIntensity:\n"
               "  - 368\n  - 400\n  - 432\n  - 464\n  spawnFormation: 0\n")
OMNI_BLOCK = ("  noNucleusSpawnRadius: 0\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
              "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
              + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {n}\n" for n in OMNI))
DONOR_OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
              "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 3\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 2\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 2\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 1\n")


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["KelpwoodBiteTurnMonitor"], G_SCRIPT["CleanerRamStrikeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["KelpwoodController"], G_SCRIPT["CleanerRamController"], "controller")
    scene = lib.swap_guid(scene, DONOR["KelpwoodSettings"], G_ASSET["CleanerRamSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["KelpwoodScoringRule"], G_ASSET["CleanerRamScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, DONOR_CELLS_BLOCK, CELLS_BLOCK, "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {reef.WRASSE_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI_BLOCK, "omni crystals")
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameCleanerRam.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [CELLS_BLOCK, SPAWN, OMNI_BLOCK])
errors += scene_errs
g.emit_scene("MinigameCleanerRam", G_ASSET["MinigameCleanerRam.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameCleanerRam"])
g.register_always_unlocked()
g.register_build_scene("MinigameKelpwood", "MinigameCleanerRam", G_ASSET["MinigameCleanerRam.unity"])
g.set_end_condition("cleanerRamStrikeTarget", after="glasshouseRoundSeconds", value=RAM_TARGET)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
errors += reef.comeback_error(RAM_TARGET, COMEBACK_RATE, "ram target")
errors += reef.shared_errors(g, "CleanerRam", MODE_ID, "CleanerRamHint", TOAST_HINT, CRYSTALS)
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultCleanerRamStrikeTarget = {RAM_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultCleanerRamStrikeTarget differs from RAM_TARGET")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count("Strike = 5,"):
    errors.append("CombatHitClass.Strike is no longer 5 - the rule's strike points would pay nothing")
if any(b > a for a, b in zip(OMNI, OMNI[1:])):
    errors.append(f"the omni ladder must not rise with intensity ({OMNI})")
if "Plan.Hunt" not in g.read(SCRIPT_PATHS["CleanerRamController"]):
    errors.append("CleanerRamController no longer arms the Hunt plan")
errors += reef.scene_identity_errors(
    scene,
    present={**{k: G_SCRIPT[k] for k in ("CleanerRamController", "CleanerRamStrikeTurnMonitor")},
             "CleanerRamSettings": G_ASSET["CleanerRamSettings"],
             "CleanerRamScoringRule": G_ASSET["CleanerRamScoringRule"], "Barren": EXISTING["BarrenCellConfig"]},
    absent={**DONOR, **{f"Kelpwood cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}})

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + reef.SHARED_MINTED)
