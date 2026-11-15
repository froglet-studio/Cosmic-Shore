#!/usr/bin/env python3
"""
Authors every serialized asset the Shockfront game mode needs (GameModes.Shockfront = 119).

Shockfront is the Stoat's CHARGE game - a duel in Dog Fight's Boneyard. The Stoat's Charge ability is the RIPPLE
(StoatRippleExecutor, R_VesselActions/STOAT_DIPOLE.md section 1c): every annihilation of its pair - the orbit's pair
closing after a slingshot - throws a space-time ripple whose front strips one petal of every element from each rival it
sweeps over. Every rival stripped is a Debuff-class combat hit (since 2026-11-15) and one point; first DOMAIN to the
target wins. See Assets/_Scripts/Controller/Arcade/SHOCKFRONT.md.

THE IDS ARE THE ROSTER'S (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 29): GameModes 119, hint toast 194. Parallel
sessions build the other vessels' games at the same time; never take "the next free id" here.

THE ARENA IS DOG FIGHT'S BONEYARD, referenced read-only, as Dustup does: cover and canyons, so a duel between hulls
that sling round has somewhere to lose a pursuer and somewhere to swing back from. The scene is cloned from Undertow
(butterfly_games_common, with the Stoat's hull class on the AI templates) with Dog Fight's spawn ring and crystal
volume, because the Boneyard has no nucleus.

THE ONE VESSEL CHANGE IS NOT THIS SCRIPT'S. The Ripple raising its strip as a scored hit is the Stoat's (its config
asset is author_stoat_assets.py's); this script only ASSERTS it, because the whole mode rests on it.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_shockfront_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import butterfly_games_common as common    # noqa: E402

MODE_ID = 119          # GAME_KIT_ROSTER.md row 29
TOAST_HINT = 194       # GAME_KIT_ROSTER.md row 29
STOAT_ID = lib.VESSEL_CLASS_ID["Stoat"]
g = lib.Generator(MODE_ID, "Shockfront")
guid = lib.guid

SCRIPT_PATHS = {
    "ShockfrontController":        "Assets/_Scripts/Controller/Arcade/Shockfront/ShockfrontController.cs",
    "ShockfrontSettingsSO":        "Assets/_Scripts/Controller/Arcade/Shockfront/ShockfrontSettingsSO.cs",
    "ShockfrontScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Shockfront/ShockfrontScoringRuleSO.cs",
    "ShockfrontRippleTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/ShockfrontRippleTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameShockfront":      guid("asset/ArcadeGameShockfront"),
    "ShockfrontScoringRule":     guid("asset/ShockfrontScoringRule"),
    "ShockfrontSettings":        guid("asset/ShockfrontSettings"),
    "GameToastConfigShockfront": guid("asset/GameToastConfigShockfront"),
    "ModePreviewShockfront":     guid("asset/ModePreviewShockfront"),
    "MinigameShockfront.unity":  guid("asset/MinigameShockfront.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Stoat"] = lib.VESSELS["Stoat"]
BONEYARD_DIR = "Assets/_SO_Assets/Cell Configs/Boneyard Cell"
BONEYARD = [lib.existing_guid(f"{BONEYARD_DIR}/Boneyard Cell Config {i}.asset") for i in range(1, 5)]
for i, gd in enumerate(BONEYARD, 1):
    EXISTING[f"BoneyardCellConfig{i}"] = gd

# The hit this mode PAYS for - read, never edited here (the Stoat's generator owns the asset).
RIPPLE_CONFIG = "Assets/_SO_Assets/VesselActions/Stoat/StoatRippleConfig.asset"
RIPPLE_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/StoatRippleExecutor.cs"
COMBAT_HIT_CHANNEL = lib.existing_guid("Assets/_SO_Assets/Event Channels/Event_CombatHitStats.asset")
EXISTING["Event_CombatHitStats"] = COMBAT_HIT_CHANNEL

# ── The race ─────────────────────────────────────────────────────────────────
POINT_TARGET = 10     # kept in sync with EndConditionOverridesSO.DefaultShockfrontRippleTarget (a first cut: nobody has flown it)
RIPPLE_POINTS = 1
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter behind (2.5 ripple hits) buys 1.5
# element levels, and on this hull Charge is the ripple's reach (x1 -> x2) - the comeback widens the trailer's shockwave.
COMEBACK_RATE = 0.6

# The Boneyard is NUCLEUS-LESS: Dog Fight's own ring and crystal volume, read off its scene below (Dustup's numbers).
SPAWN = common.spawn_block(40, 700, 0)
NO_NUCLEUS_SPAWN_RADIUS = 420

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameShockfront.asset"

# ── 1. .cs.meta for the scripts, the folder and the doc ──────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Shockfront")
g.text_meta("Assets/_Scripts/Controller/Arcade/SHOCKFRONT.md")

# ── 2. Scoring rule: metric 8 = CombatPoints ─────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/ShockfrontScoringRule.asset", G_ASSET["ShockfrontScoringRule"],
             lib.header_for(G_SCRIPT["ShockfrontScoringRuleSO"], "ShockfrontScoringRule") +
             f"""  metric: 8
  golfRules: 1
  ripplePoints: {RIPPLE_POINTS}
  otherHitPoints: 0
  hitsLeftTemplate: '{{count}} Hits Left'
  hitsLandedTemplate: '{{count}} ripples'
  victoryHeader: VICTORY
  victoryLabel: SHOCKFRONT TIME
  defeatHeader: DEFEAT
  defeatLabel: HITS LEFT
""")

# ── 3. Mode settings ─────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/ShockfrontSettings.asset", G_ASSET["ShockfrontSettings"],
             lib.header_for(G_SCRIPT["ShockfrontSettingsSO"], "ShockfrontSettings") + """  aiRetargetSeconds: 1.5
  aiInterceptLeadSeconds: 0.8
  aiHumanFocus: 2
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: the ripple spares teammates, so a solo lobby has nobody it could score on.
# The description is plain YAML: no colon, no apostrophe (the Serpentine trap).
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameShockfront"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameShockfront") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Shockfront
  Description: Stoats only, loose in the Boneyard. Squeeze a trigger to orbit, let go to sling - and
    when your pair of holes closes, the meeting throws a ripple through space. Every rival its front
    sweeps over loses a petal of every element. Charge widens the ripple. One rival stripped is one
    hit; first team to the target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background("Shockfront")}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameShockfront
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Stoat']}, type: 2}}
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

# ── 5. Toasts: 82 a ripple hit (DebuffHitsLanded rose), 128-130 the shared race beats, the hint, 30 comeback ──
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Shockfront.asset", G_ASSET["GameToastConfigShockfront"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Shockfront") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> caught a rival in the ripple!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} hits", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} hits", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Squeeze a trigger to orbit, let go to sling. When your pair closes it throws a "
                       "ripple - sling in close to a rival", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigShockfront"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Shockfront.asset", G_ASSET["ModePreviewShockfront"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Shockfront") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Dog Fight''s Boneyard outright - referenced, not forked, exactly as the mode
    does. OPEN-ENDED: it scores on stripping rival pilots and a solo preview has none; what it
    teaches is the orbit, the sling and the ripple its pair throws. Vessel pinned to Stoat(15).'
  PreviewCell: {{fileID: 11400000, guid: {BONEYARD[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(BONEYARD)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {STOAT_ID}
  ObjectiveText: Sling in close to a rival
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 700
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewShockfront"])

# ── 7. Scene: clone Undertow, swap to Shockfront + the Boneyard + Stoat AIs ──
errors = []
scene = common.clone_or_stand_down(
    g, "MinigameShockfront", G_ASSET["MinigameShockfront.unity"], errors,
    lambda: common.clone_scene(G_SCRIPT["ShockfrontController"], G_SCRIPT["ShockfrontRippleTurnMonitor"],
                               G_ASSET["ShockfrontSettings"], G_ASSET["ShockfrontScoringRule"],
                               BONEYARD, 1, SPAWN, NO_NUCLEUS_SPAWN_RADIUS, vessel_id=STOAT_ID))

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameShockfront"])
g.register_always_unlocked()
g.register_build_scene("MinigameDustup", "MinigameShockfront", G_ASSET["MinigameShockfront.unity"])
g.set_end_condition("shockfrontRippleTarget", after="coronaBloomTarget", value=POINT_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * POINT_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {POINT_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * POINT_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
if RIPPLE_POINTS < 1:
    errors.append("a ripple hit is worth nothing - the mode would score nothing")

eoc = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultShockfrontRippleTarget = {POINT_TARGET};" not in eoc:
    errors.append("EndConditionOverridesSO.DefaultShockfrontRippleTarget disagrees with POINT_TARGET")

# The load-bearing assumption: the Ripple still raises its strip as a Debuff-class scored hit on the scoring channel.
ripple_cfg = g.read(RIPPLE_CONFIG)
if f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_CHANNEL}, type: 2}}\n" not in ripple_cfg:
    errors.append("StoatRippleConfig.asset no longer wires Event_CombatHitStats - no ripple would ever score")
if not re.search(r"^  petalsPerElement: [1-9]\d*\n", ripple_cfg, re.M):
    errors.append("the Stoat's ripple strips no petals (petalsPerElement 0) - nothing would score")
executor = g.read(RIPPLE_EXECUTOR)
if "CombatHitDrain.TryAdmitPetals(victim, _status, CombatHitClass.Debuff" not in executor:
    errors.append("StoatRippleExecutor no longer admits its strip as a Debuff hit - ShockfrontScoringRuleSO pays Debuff")
if "config.OnCombatHitLanded.Raise(" not in executor:
    errors.append("StoatRippleExecutor no longer raises its hit - StatsManager would count nothing")

# A mode never changes the vessel (LOCKED): the AI only steers; the slings are the hull's own autopilot's.
controller = g.read(SCRIPT_PATHS["ShockfrontController"])
for banned in ("PerformShipControllerActions", "InputStatus", "StoatSlingConfig", "StoatRippleConfig"):
    if banned in controller:
        errors.append(f"ShockfrontController mentions {banned} - the mode may only steer its AI")
if "SetExternalTargetProvider" not in controller:
    errors.append("ShockfrontController no longer steers its AI hunters")
if "GameToastSituation.ShockfrontHint" not in controller:
    errors.append("ShockfrontController does not post its hint")
toasts = g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
if f"ShockfrontHint = {TOAST_HINT}," not in toasts:
    errors.append(f"GameToastSituation.ShockfrontHint is not {TOAST_HINT} (the roster's id)")
modes = g.read("Assets/_Scripts/Data/Enums/GameModes.cs")
if f"Shockfront = {MODE_ID}," not in modes:
    errors.append(f"GameModes.Shockfront is not {MODE_ID} (the roster's id)")

# Dog Fight's own spawn ring and crystal volume, which this mode borrows with the Boneyard.
dogfight = g.read(f"{lib.SCENES_DIR}/MinigameDogFight.unity")
if SPAWN not in dogfight:
    errors.append("Dog Fight's spawn ring moved - re-read it and update SPAWN")
if f"  noNucleusSpawnRadius: {NO_NUCLEUS_SPAWN_RADIUS}\n" not in dogfight:
    errors.append("Dog Fight's noNucleusSpawnRadius moved - update NO_NUCLEUS_SPAWN_RADIUS")
if common.cell_list(BONEYARD) not in dogfight:
    errors.append("Dog Fight no longer runs the four Boneyard configs in this order")

common.assert_scene(errors, scene,
                    must_have={"ShockfrontController": G_SCRIPT["ShockfrontController"],
                               "ShockfrontRippleTurnMonitor": G_SCRIPT["ShockfrontRippleTurnMonitor"],
                               "ShockfrontSettings": G_ASSET["ShockfrontSettings"],
                               "ShockfrontScoringRule": G_ASSET["ShockfrontScoringRule"],
                               **{f"Boneyard {i}": gd for i, gd in enumerate(BONEYARD, 1)}},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}},
                    vessel_id=STOAT_ID)

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Shockfront"),
                   guid("text/Assets/_Scripts/Controller/Arcade/SHOCKFRONT.md")])
