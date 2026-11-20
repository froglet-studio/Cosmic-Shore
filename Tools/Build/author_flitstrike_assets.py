#!/usr/bin/env python3
"""
Authors every serialized asset the Flitstrike game mode needs (GameModes.Flitstrike = 108, hint toast 183 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 18 reserved on 2026-11-15).

Flitstrike is the Honeyguide's CHARGE game: hold LT to see the Flit's lane, let go to dart 18 u along it, and every
rival hull within the strike radius takes a Strike. Every strike is one point; first DOMAIN to the target wins. See
Assets/_Scripts/Controller/Arcade/FLITSTRIKE.md.

NOTHING OUTSIDE THE MODE IS EDITED. HoneyguideCrew.StrikeVessel raises a Strike-class combat hit for every strike, so
every one is already COUNTED in every mode; this mode adds the rule that PAYS for it (Jackstraw's, Kelpwood's
precedent). Asserted below, because the whole mode rests on it.

THE ARENA IS DOG FIGHT'S BONEYARD, referenced read-only (Dustup's): cover and canyons to set a dart up round. The
scene is cloned from Undertow (honeyguide_games_common) with Dog Fight's own spawn ring and crystal volume, because
the Boneyard has no nucleus.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_flitstrike_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import honeyguide_games_common as common   # noqa: E402

MODE_ID = 108          # GAME_KIT_ROSTER.md row 18 - never "the next free id"
TOAST_HINT = 183
g = lib.Generator(MODE_ID, "Flitstrike")
guid = lib.guid

SCRIPT_PATHS = {
    "FlitstrikeController":        "Assets/_Scripts/Controller/Arcade/Flitstrike/FlitstrikeController.cs",
    "FlitstrikeSettingsSO":        "Assets/_Scripts/Controller/Arcade/Flitstrike/FlitstrikeSettingsSO.cs",
    "FlitstrikeScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Flitstrike/FlitstrikeScoringRuleSO.cs",
    "FlitstrikeStrikeTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/FlitstrikeStrikeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameFlitstrike":      guid("asset/ArcadeGameFlitstrike"),
    "FlitstrikeScoringRule":     guid("asset/FlitstrikeScoringRule"),
    "FlitstrikeSettings":        guid("asset/FlitstrikeSettings"),
    "GameToastConfigFlitstrike": guid("asset/GameToastConfigFlitstrike"),
    "ModePreviewFlitstrike":     guid("asset/ModePreviewFlitstrike"),
    "MinigameFlitstrike.unity":  guid("asset/MinigameFlitstrike.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Honeyguide"] = common.VESSEL_HONEYGUIDE
BONEYARD_DIR = "Assets/_SO_Assets/Cell Configs/Boneyard Cell"
BONEYARD = [lib.existing_guid(f"{BONEYARD_DIR}/Boneyard Cell Config {i}.asset") for i in range(1, 5)]
for i, gd in enumerate(BONEYARD, 1):
    EXISTING[f"BoneyardCellConfig{i}"] = gd

# ── The race ─────────────────────────────────────────────────────────────────
STRIKE_TARGET = 5     # kept in sync with EndConditionOverridesSO.DefaultFlitstrikeStrikeTarget
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (1.25 strikes) buys
# 1.25 element levels, and on this hull a Charge level is a wider strike (8 -> 16 u at Charge 10; Relentless at 5).
COMEBACK_RATE = 1.0
SPAWN = common.spawn_block(40, 700, 0)   # Dog Fight's own ring (the Boneyard has no nucleus)
NO_NUCLEUS_SPAWN_RADIUS = 420

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameFlitstrike.asset"

# ── 1. .cs.meta for the scripts, the folder this mode adds, the doc ──────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Flitstrike")
g.text_meta("Assets/_Scripts/Controller/Arcade/FLITSTRIKE.md")

# ── 2. Scoring rule (Jackstraw's, its words authored here) ───────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/FlitstrikeScoringRule.asset", G_ASSET["FlitstrikeScoringRule"],
             lib.header_for(G_SCRIPT["FlitstrikeScoringRuleSO"], "FlitstrikeScoringRule") + """  metric: 8
  golfRules: 1
  strikePoints: 1
  otherHitPoints: 0
  strikesLeftTemplate: '{count} Strikes Left'
  strikesLandedTemplate: '{count} strikes'
  victoryHeader: VICTORY
  victoryLabel: FLITSTRIKE TIME
  defeatHeader: DEFEAT
  defeatLabel: STRIKES LEFT
""")

# ── 3. Mode settings ─────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/FlitstrikeSettings.asset", G_ASSET["FlitstrikeSettings"],
             lib.header_for(G_SCRIPT["FlitstrikeSettingsSO"], "FlitstrikeSettings") + """  aiRetargetSeconds: 1.5
  aiInterceptLeadSeconds: 0.4
  aiHumanFocus: 2
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a Flit spares teammates, so a solo lobby has nobody it could score on.
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameFlitstrike"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameFlitstrike") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Flitstrike
  Description: Honeyguides only, loose in the Boneyard. Hold the left trigger to see the dart,
    let go to Flit through a rival - every strike is a point. Charge widens the strike, and the
    Ratel's Charge cards bank a lasso, a blink and more on the same trigger. First team to the
    target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background("Flitstrike")}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameFlitstrike
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Honeyguide']}, type: 2}}
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

# ── 5. Toasts: the shared race beats (128-130, DomainRaceToasts) and one hint ─
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Flitstrike.asset", G_ASSET["GameToastConfigFlitstrike"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Flitstrike") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(128, "{0} is a quarter of the way to {2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1} of {2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Hold the left trigger to see the dart, let go to Flit through a rival") +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigFlitstrike"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Flitstrike.asset", G_ASSET["ModePreviewFlitstrike"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Flitstrike") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Dog Fight''s Boneyard outright - referenced, not forked, exactly as the mode
    does. OPEN-ENDED: it scores on striking rival pilots and a solo preview has none; what it
    teaches is the Honeyguide and its Flit. Vessel pinned to Honeyguide({common.HONEYGUIDE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {BONEYARD[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(BONEYARD)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {common.HONEYGUIDE_ID}
  ObjectiveText: Flit through a rival
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 700
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewFlitstrike"])

# ── 7. Scene: clone Undertow, swap to Flitstrike + the Boneyard ──────────────
errors = []
scene = common.clone_or_stand_down(
    g, "MinigameFlitstrike", G_ASSET["MinigameFlitstrike.unity"], errors,
    lambda: common.clone_scene(G_SCRIPT["FlitstrikeController"], G_SCRIPT["FlitstrikeStrikeTurnMonitor"],
                               G_ASSET["FlitstrikeSettings"], G_ASSET["FlitstrikeScoringRule"],
                               BONEYARD, 1, SPAWN, NO_NUCLEUS_SPAWN_RADIUS))

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameFlitstrike"])
g.register_always_unlocked()
g.register_build_scene("MinigameSirocco", "MinigameFlitstrike", G_ASSET["MinigameFlitstrike.unity"])
g.set_end_condition("flitstrikeStrikeTarget", after="siroccoPrismTarget", value=STRIKE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * STRIKE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {STRIKE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * STRIKE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The C# default must be the number the generator writes (the asset is the authority, the default its fallback).
so = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultFlitstrikeStrikeTarget = {STRIKE_TARGET};" not in so:
    errors.append("EndConditionOverridesSO.DefaultFlitstrikeStrikeTarget differs from STRIKE_TARGET")

# The ids are the roster's; the enum and the toast enum must carry exactly them.
if f"Flitstrike = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Flitstrike is not {MODE_ID} (GAME_KIT_ROSTER.md row 18)")
if f"FlitstrikeHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.FlitstrikeHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md row 18)")

# The kit the mode rests on: a Flit's strike is Strike-class, and the crew's hunt yields to the match's steering.
errors += common.kit_errors(g, ("strike", "yield"))

# The AI only steers (the crew presses its own triggers); a second presser would fight it.
ctrl = g.read(SCRIPT_PATHS["FlitstrikeController"])
for banned in ("PerformShipControllerActions", "InputStatus", "MantaBombRules", "Honeyguide" + "ConfigSO"):
    if banned in ctrl:
        errors.append(f"FlitstrikeController uses {banned} - a mode AI only steers and a mode never touches the kit")

# Dog Fight's own spawn ring and crystal volume, which this mode borrows with the Boneyard.
dogfight = g.read(f"{lib.SCENES_DIR}/MinigameDogFight.unity")
if SPAWN not in dogfight:
    errors.append("Dog Fight's spawn ring moved - re-read it and update SPAWN")
if f"  noNucleusSpawnRadius: {NO_NUCLEUS_SPAWN_RADIUS}\n" not in dogfight:
    errors.append("Dog Fight's noNucleusSpawnRadius moved - update NO_NUCLEUS_SPAWN_RADIUS")
if common.cell_list(BONEYARD) not in dogfight:
    errors.append("Dog Fight no longer runs the four Boneyard configs in this order")

common.assert_scene(errors, scene,
                    must_have={"FlitstrikeController": G_SCRIPT["FlitstrikeController"],
                               "FlitstrikeStrikeTurnMonitor": G_SCRIPT["FlitstrikeStrikeTurnMonitor"],
                               "FlitstrikeSettings": G_ASSET["FlitstrikeSettings"],
                               "FlitstrikeScoringRule": G_ASSET["FlitstrikeScoringRule"],
                               **{f"Boneyard {i}": gd for i, gd in enumerate(BONEYARD, 1)}},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}})

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Flitstrike"),
                   guid("text/Assets/_Scripts/Controller/Arcade/FLITSTRIKE.md")])
