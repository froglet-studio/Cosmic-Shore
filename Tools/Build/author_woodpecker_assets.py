#!/usr/bin/env python3
"""
Authors every serialized asset the Woodpecker game mode needs (GameModes.Woodpecker = 110, hint toast 185 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 20 reserved on 2026-11-15).

Woodpecker is the Honeyguide's SPACE game: a pecking race through Rampage's cactus forest. RT's burst of Pecks (3 shots
at Space 0, 6 at Space 10, piercing at Space 5) chips hostile prisms; first DOMAIN to the hostile-prism target wins on
ScoringMetric.PrismsDestroyed (Rampage's metric and rule, through ShatterplaneScoringRuleSO's authored words). See
Assets/_Scripts/Controller/Arcade/WOODPECKER.md.

THE ARENA IS RAMPAGE'S, referenced read-only - the four cactus-forest configs The Bends, Bloomrush and Sirocco
reference (the cell is per-arena, not per-mode). Spawn ring is Rampage's own, read off The Bends' scene.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_woodpecker_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import honeyguide_games_common as common   # noqa: E402

MODE_ID = 110          # GAME_KIT_ROSTER.md row 20 - never "the next free id"
TOAST_HINT = 185
g = lib.Generator(MODE_ID, "Woodpecker")
guid = lib.guid

SCRIPT_PATHS = {
    "WoodpeckerController":       "Assets/_Scripts/Controller/Arcade/Woodpecker/WoodpeckerController.cs",
    "WoodpeckerSettingsSO":       "Assets/_Scripts/Controller/Arcade/Woodpecker/WoodpeckerSettingsSO.cs",
    "WoodpeckerScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Woodpecker/WoodpeckerScoringRuleSO.cs",
    "WoodpeckerPrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/WoodpeckerPrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameWoodpecker":      guid("asset/ArcadeGameWoodpecker"),
    "WoodpeckerScoringRule":     guid("asset/WoodpeckerScoringRule"),
    "WoodpeckerSettings":        guid("asset/WoodpeckerSettings"),
    "GameToastConfigWoodpecker": guid("asset/GameToastConfigWoodpecker"),
    "ModePreviewWoodpecker":     guid("asset/ModePreviewWoodpecker"),
    "MinigameWoodpecker.unity":  guid("asset/MinigameWoodpecker.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Honeyguide"] = common.VESSEL_HONEYGUIDE
RAMPAGE_DIR = "Assets/_SO_Assets/Cell Configs/Rampage Cell"
RAMPAGE = [lib.existing_guid(f"{RAMPAGE_DIR}/Rampage Cell Config {i}.asset") for i in range(1, 5)]
for i, gd in enumerate(RAMPAGE, 1):
    EXISTING[f"RampageCellConfig{i}"] = gd
PECK_ACTION = "Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguidePeckAction.asset"

# ── The race ─────────────────────────────────────────────────────────────────
PRISM_TARGET = 150    # kept in sync with EndConditionOverridesSO.DefaultWoodpeckerPrismTarget
# Comeback - a FUNCTION OF THE TARGET. A quarter behind (37.5 prisms) buys 1.125 element levels, and on this hull a
# Space level is more shots in a Peck (3 -> 6 at Space 10) and, at Space 5, rounds that pierce.
COMEBACK_RATE = 0.03
SPAWN = common.spawn_block(500, 0, 0)   # Rampage's own ring - 500 outside its nucleus

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameWoodpecker.asset"

# ── 1. .cs.meta, folder, doc ─────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Woodpecker")
g.text_meta("Assets/_Scripts/Controller/Arcade/WOODPECKER.md")

# ── 2. Scoring rule: metric 5 = PrismsDestroyed (Rampage's rule, Shatterplane's authored words) ──
g.emit_asset("Assets/_SO_Assets/Scoring Rules/WoodpeckerScoringRule.asset", G_ASSET["WoodpeckerScoringRule"],
             lib.header_for(G_SCRIPT["WoodpeckerScoringRuleSO"], "WoodpeckerScoringRule") + """  metric: 5
  golfRules: 1
  prismsLeftTemplate: '{count} Prisms Left'
  breakdownTemplate: '{count} pecked'
  victoryHeader: VICTORY
  victoryLabel: WOODPECKER TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/WoodpeckerSettings.asset", G_ASSET["WoodpeckerSettings"],
             lib.header_for(G_SCRIPT["WoodpeckerSettingsSO"], "WoodpeckerSettings") + """  aiRetargetSeconds: 4
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 2: a race against the ARENA - a solo pilot has the whole forest to peck against the AI
# (the Rampage shape).
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameWoodpecker"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameWoodpecker") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Woodpecker
  Description: Honeyguides only, over Rampage's cactus forest. Hold the right trigger to aim,
    let go to fire three quick pecks - each one chips a plant that is not your colour. Space
    adds shots to every peck, and the Ratel's Space cards bank a lance, a grenade and more.
    First team to peck the target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background("Woodpecker")}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameWoodpecker
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Honeyguide']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts: the shared race beats (128-130), a prism milestone (83), one hint ─
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Woodpecker.asset", G_ASSET["GameToastConfigWoodpecker"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Woodpecker") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has pecked {1} prisms", tint_domain=1, domain_names=0, every_n=50) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Hold the right trigger to aim, let go to peck - plants of any colour but yours score") +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigWoodpecker"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Woodpecker.asset", G_ASSET["ModePreviewWoodpecker"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Woodpecker") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Rampage''s cactus forest outright - referenced, not forked, exactly as the mode
    does; intensity is the forest''s density. What the preview teaches is the Peck over the
    forest. Vessel pinned to Honeyguide({common.HONEYGUIDE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {RAMPAGE[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(RAMPAGE)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {common.HONEYGUIDE_ID}
  ObjectiveText: Peck the forest
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 500
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewWoodpecker"])

# ── 7. Scene ─────────────────────────────────────────────────────────────────
errors = []
scene = common.clone_or_stand_down(
    g, "MinigameWoodpecker", G_ASSET["MinigameWoodpecker.unity"], errors,
    lambda: common.clone_scene(G_SCRIPT["WoodpeckerController"], G_SCRIPT["WoodpeckerPrismTurnMonitor"],
                               G_ASSET["WoodpeckerSettings"], G_ASSET["WoodpeckerScoringRule"],
                               RAMPAGE, 1, SPAWN, 0))

# ── 8. Registries ────────────────────────────────────────────────────────────
build = g.read_current(lib.BUILD_SETTINGS)
after = next((s for s in ("MinigameHoneycomb", "MinigameFlitstrike") if f"{s}.unity" in build), "MinigameSirocco")
g.register_arcade_card(G_ASSET["ArcadeGameWoodpecker"])
g.register_always_unlocked()
g.register_build_scene(after, "MinigameWoodpecker", G_ASSET["MinigameWoodpecker.unity"])
end = g.read_current(lib.END_CONDITIONS)
after_key = next((k for k in ("honeycombRoundSeconds", "flitstrikeStrikeTarget") if f"{k}:" in end), "siroccoPrismTarget")
g.set_end_condition("woodpeckerPrismTarget", after=after_key, value=PRISM_TARGET)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

so = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultWoodpeckerPrismTarget = {PRISM_TARGET};" not in so:
    errors.append("EndConditionOverridesSO.DefaultWoodpeckerPrismTarget differs from PRISM_TARGET")
if f"Woodpecker = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Woodpecker is not {MODE_ID} (GAME_KIT_ROSTER.md row 20)")
if f"WoodpeckerHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.WoodpeckerHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md row 20)")

# The kit the mode rests on: the AI pecks hostile prisms ahead, and its hunt yields to the match's steering.
errors += common.kit_errors(g, ("peck", "yield"))
# The Peck is the hull's own gun; it must still fire (a FullAutoActionSO) or the mode has no weapon.
if not os.path.exists(os.path.join(lib.ROOT, PECK_ACTION)):
    errors.append("HoneyguidePeckAction.asset is gone - Woodpecker's weapon moved")

ctrl = g.read(SCRIPT_PATHS["WoodpeckerController"])
for banned in ("PerformShipControllerActions", "InputStatus", "Honeyguide" + "ConfigSO"):
    if banned in ctrl:
        errors.append(f"WoodpeckerController uses {banned} - a mode AI only steers and a mode never touches the kit")

# Rampage's own spawn ring, borrowed with its cell.
bends = g.read(f"{lib.SCENES_DIR}/MinigameBends.unity")
if SPAWN not in bends:
    errors.append("the Rampage-arena spawn ring (read off The Bends) moved - update SPAWN")
if common.cell_list(RAMPAGE) not in bends:
    errors.append("The Bends no longer runs the four Rampage configs in this order")

# The forest must be big enough that a match ENDS with forest standing (intensity 4's forest is the smallest).
if PRISM_TARGET > 0.25 * 9830:
    errors.append("the target would strip most of intensity 4's forest - lower it")

common.assert_scene(errors, scene,
                    must_have={"WoodpeckerController": G_SCRIPT["WoodpeckerController"],
                               "WoodpeckerPrismTurnMonitor": G_SCRIPT["WoodpeckerPrismTurnMonitor"],
                               "WoodpeckerSettings": G_ASSET["WoodpeckerSettings"],
                               "WoodpeckerScoringRule": G_ASSET["WoodpeckerScoringRule"],
                               **{f"Rampage {i}": gd for i, gd in enumerate(RAMPAGE, 1)}},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}})

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Woodpecker"),
                   guid("text/Assets/_Scripts/Controller/Arcade/WOODPECKER.md")])
