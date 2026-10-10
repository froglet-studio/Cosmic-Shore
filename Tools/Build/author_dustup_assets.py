#!/usr/bin/env python3
"""
Authors every serialized asset the Dustup game mode needs (GameModes.Dustup = 59).

Dustup is the Butterfly's CHARGE game - a dust duel. The Butterfly carries no gun; its one weapon
is the Scale Dust capsule hanging BELOW the hull in Dust mode, and CHARGE is the dust's bite. Every
opposing pilot the dust passes through pays one DUSTING (1 point); first DOMAIN to the target
wins. See Assets/_Scripts/Controller/Arcade/DUSTUP.md.

NOTHING OUTSIDE THE MODE IS EDITED. The dust container has carried a Strike-class combat-hit
reporter (ButterflyCombatHitBySkimmerEffect) since the hull shipped - every dusting is already
COUNTED in every mode - so this mode only adds the rule that PAYS for it.

THE ARENA IS DOG FIGHT'S BONEYARD, referenced read-only (the cell is per-arena, not per-mode):
cover and canyons for the fleet's two slowest-turning hulls to lose a pursuer in. The scene is
cloned from Undertow (butterfly_games_common) with Dog Fight's own spawn ring and crystal volume,
because the Boneyard has no nucleus.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_dustup_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import butterfly_games_common as common    # noqa: E402

MODE_ID = 59
g = lib.Generator(MODE_ID, "Dustup")
guid = lib.guid

SCRIPT_PATHS = {
    "DustupController":       "Assets/_Scripts/Controller/Arcade/Dustup/DustupController.cs",
    "DustupSettingsSO":       "Assets/_Scripts/Controller/Arcade/Dustup/DustupSettingsSO.cs",
    "DustupScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Dustup/DustupScoringRuleSO.cs",
    "DustupPointTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/DustupPointTurnMonitor.cs",
    "ButterflyAutopilotModeDriver": "Assets/_Scripts/Controller/Arcade/ButterflyGames/ButterflyAutopilotModeDriver.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameDustup":       guid("asset/ArcadeGameDustup"),
    "DustupScoringRule":      guid("asset/DustupScoringRule"),
    "DustupSettings":         guid("asset/DustupSettings"),
    "GameToastConfigDustup":  guid("asset/GameToastConfigDustup"),
    "ModePreviewDustup":      guid("asset/ModePreviewDustup"),
    "MinigameDustup.unity":   guid("asset/MinigameDustup.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Butterfly"] = lib.VESSELS["Butterfly"]
BONEYARD_DIR = "Assets/_SO_Assets/Cell Configs/Boneyard Cell"
BONEYARD = [lib.existing_guid(f"{BONEYARD_DIR}/Boneyard Cell Config {i}.asset") for i in range(1, 5)]
for i, gd in enumerate(BONEYARD, 1):
    EXISTING[f"BoneyardCellConfig{i}"] = gd
# The reporter this mode PAYS for - read, never edited. Asserted below to still be Strike-class
# and to still sit in the dust container, because the whole mode rests on it.
REPORTER = "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/ButterflyCombatHitBySkimmerEffect.asset"
DUST_CONTAINER = ("Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/"
                  "ButterflyDustSkimmerImpactorDataContainer.asset")
EXISTING["ButterflyCombatHitBySkimmerEffect"] = lib.existing_guid(REPORTER)

# ── The race ─────────────────────────────────────────────────────────────────
POINT_TARGET = 10     # kept in sync with EndConditionOverridesSO.DefaultDustupPointTarget
DUST_POINTS = 1
STRIKE_CLASS = 5      # CombatHitClass.Strike
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind
# (2.5 dustings) buys 1.5 element levels - and on this hull an element level is the dust itself:
# Charge is the bite, Space the capsule's length.
COMEBACK_RATE = 0.6

# The Boneyard is NUCLEUS-LESS: Dog Fight's own ring and crystal volume, read off its scene below.
SPAWN = common.spawn_block(40, 700, 0)
NO_NUCLEUS_SPAWN_RADIUS = 420

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameDustup.asset"

# ── 1. .cs.meta for the scripts, and the folders this mode adds ──────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Dustup")
g.folder_meta("Assets/_Scripts/Controller/Arcade/ButterflyGames")
g.text_meta("Assets/_Scripts/Controller/Arcade/DUSTUP.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/DustupScoringRule.asset", G_ASSET["DustupScoringRule"],
             lib.header_for(G_SCRIPT["DustupScoringRuleSO"], "DustupScoringRule") +
             f"  metric: 8\n  golfRules: 1\n  dustPoints: {DUST_POINTS}\n  otherHitPoints: 0\n")

# ── 3. Mode settings ─────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/DustupSettings.asset", G_ASSET["DustupSettings"],
             lib.header_for(G_SCRIPT["DustupSettingsSO"], "DustupSettings") + """  firstMilestoneFraction: 0.25
  secondMilestoneFraction: 0.5
  progressSampleSeconds: 0.5
  aiRetargetSeconds: 1.5
  aiInterceptLeadSeconds: 0.5
  aiHumanFocus: 3
  aiDustHover: 30
  aiModeRetrySeconds: 1
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: the dust spares teammates, so a solo lobby has nobody
# it could ever score on.
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameDustup"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameDustup") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Dustup
  Description: Butterflies only, loose in the Boneyard, and not a gun between them. Flip to
    Dust mode and your scale dust hangs below you - fly OVER a rival and every element they
    have takes the bite. Charge makes it bite harder. One pass is one dusting; first team to
    the target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameDustup
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Butterfly']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Dustup.asset", G_ASSET["GameToastConfigDustup"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Dustup") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(119, "{0} is a quarter of the way to {2}", tint_domain=1, domain_names=0) +
             lib.toast(120, "{0} is halfway to {2} - {1} dustings", tint_domain=1, domain_names=0) +
             lib.toast(121, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(122, "Pull the right trigger for Dust mode - the dust hangs BELOW you, so fly over a rival", idle=1, idle_seconds=25) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigDustup"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Dustup.asset", G_ASSET["ModePreviewDustup"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Dustup") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Dog Fight''s Boneyard outright - referenced, not forked, exactly as the mode
    does. OPEN-ENDED: it scores on dusting rival pilots and a solo preview has none; what it
    teaches is the Butterfly, the mode switch and the dust hanging below the hull. Vessel pinned
    to Butterfly(13).'
  PreviewCell: {{fileID: 11400000, guid: {BONEYARD[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(BONEYARD)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {common.BUTTERFLY_ID}
  ObjectiveText: Fly over a rival in Dust mode
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 700
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewDustup"])

# ── 7. Scene: clone Undertow, swap to Dustup + the Boneyard ──────────────────
errors = []
scene = common.clone_or_stand_down(
    g, "MinigameDustup", G_ASSET["MinigameDustup.unity"], errors,
    lambda: common.clone_scene(G_SCRIPT["DustupController"], G_SCRIPT["DustupPointTurnMonitor"],
                               G_ASSET["DustupSettings"], G_ASSET["DustupScoringRule"],
                               BONEYARD, 1, SPAWN, NO_NUCLEUS_SPAWN_RADIUS))

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameDustup"])
g.register_always_unlocked()
g.register_build_scene("MinigameWaystation", "MinigameDustup", G_ASSET["MinigameDustup.unity"])
g.set_end_condition("dustupPointTarget", after="undertowPointTarget", value=POINT_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * POINT_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {POINT_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * POINT_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
if DUST_POINTS < 1:
    errors.append("a dusting is worth nothing - the mode would score nothing")

# The load-bearing assumption: the reporter the rule pays for is still Strike-class and still in
# the dust container. If either moves, the mode's scoreboard stays at zero with nothing to say why.
rep = g.read(REPORTER)
if f"  hitClass: {STRIKE_CLASS}\n" not in rep:
    errors.append("ButterflyCombatHitBySkimmerEffect is no longer Strike-class - DustupScoringRuleSO pays Strike")
if EXISTING["ButterflyCombatHitBySkimmerEffect"] not in g.read(DUST_CONTAINER):
    errors.append("the Butterfly dust container no longer carries its combat-hit reporter - nothing would score")

# Dog Fight's own spawn ring and crystal volume, which this mode borrows with the Boneyard.
dogfight = g.read(f"{lib.SCENES_DIR}/MinigameDogFight.unity")
if SPAWN not in dogfight:
    errors.append("Dog Fight's spawn ring moved - re-read it and update SPAWN")
if f"  noNucleusSpawnRadius: {NO_NUCLEUS_SPAWN_RADIUS}\n" not in dogfight:
    errors.append("Dog Fight's noNucleusSpawnRadius moved - update NO_NUCLEUS_SPAWN_RADIUS")
if common.cell_list(BONEYARD) not in dogfight:
    errors.append("Dog Fight no longer runs the four Boneyard configs in this order")

common.assert_scene(errors, scene,
                    must_have={"DustupController": G_SCRIPT["DustupController"],
                               "DustupPointTurnMonitor": G_SCRIPT["DustupPointTurnMonitor"],
                               "DustupSettings": G_ASSET["DustupSettings"],
                               "DustupScoringRule": G_ASSET["DustupScoringRule"],
                               **{f"Boneyard {i}": gd for i, gd in enumerate(BONEYARD, 1)}},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}})

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Dustup"),
                   guid("folder/Assets/_Scripts/Controller/Arcade/ButterflyGames"),
                   guid("text/Assets/_Scripts/Controller/Arcade/DUSTUP.md")])
