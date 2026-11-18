#!/usr/bin/env python3
"""
Authors every serialized asset the Reefbuild game mode needs (GameModes.Reefbuild = 112, hint toast 187 - both reserved
by Docs/ElementalLoop/GAME_KIT_ROSTER.md row 22).

Reefbuild is the Wrasse and Grouper's MASS game (Assets/_Scripts/Controller/Arcade/WRASSEREEF.md): a TIMED reef war. The
Wrasse's kite closes its tail round Mass motes to lay a cluster of your prisms on its plane (the more motes in one loop,
the far bigger the cluster), and the Grouper's trail adds to it. Rival kites pop and blast what you built. The domain with
the most prism volume STANDING at the whistle wins (ScoringMetric.VolumeRemaining; StatsManager.PrismCreated credits a
cluster prism to its owner because KiteNavigator.LayMassQueue initializes it with the pilot's name).

THE ARENA IS THE BARE BARREN CELL, referenced read-only (Tapestry's reasoning: no food web, so the only forces that
remove mass are the pilots'). The lever is the REEF's crystals, mostly Mass (WrasseReefCrystals); intensity is scarcity.

NOTHING ABOUT THE WRASSE CHANGES (the LOCKED vessel rule). The scene is cloned from Tapestry's (a timed VolumeRemaining
war in the same cell, whose controller declares settings / rule / arenaCell as this one does).

THE COMEBACK RATE IS SIZED AGAINST A VOLUME, read off the shipped seat (KiteNavigatorConfig: massPrisms, growth,
massPrismScale) rather than typed, so a seat retune moves the comeback with it.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_reefbuild_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import wrasse_reef_common as reef  # noqa: E402

MODE_ID = 112
TOAST_HINT = 187
g = lib.Generator(MODE_ID, "Reefbuild")
guid = lib.guid

AR = reef.AR
SCRIPT_PATHS = {
    "ReefbuildController":      f"{reef.REEF_DIR}/ReefbuildController.cs",
    "ReefbuildScoringRuleSO":   f"{reef.REEF_DIR}/ReefbuildScoringRuleSO.cs",
    "ReefbuildTimeTurnMonitor": f"{AR}/TurnMonitors/ReefbuildTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {n: guid(f"asset/{n}") for n in ("ArcadeGameReefbuild", "ReefbuildScoringRule", "ReefbuildSettings",
                                           "GameToastConfigReefbuild", "ModePreviewReefbuild",
                                           "MinigameReefbuild.unity")}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Wrasse"] = reef.VESSEL_WRASSE
EXISTING["BarrenCellConfig"] = lib.existing_guid(reef.BARREN_CELL)

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameTapestry.unity"
DONOR = {
    "TapestryController":      guid("script/TapestryController"),
    "TapestryTimeTurnMonitor": guid("script/TapestryTimeTurnMonitor"),
    "TapestrySettings":        guid("asset/TapestrySettings"),
    "TapestryScoringRule":     guid("asset/TapestryScoringRule"),
}
TIGERMOTH_ID = lib.VESSEL_CLASS_ID["TigerMoth"]

# ── The round ────────────────────────────────────────────────────────────────
ROUND_SECONDS = 90        # kept in sync with EndConditionOverridesSO.DefaultReefbuildRoundSeconds
CRYSTALS = [24, 16, 10, 6]
MASS_SHARE = 0.6
SCATTER_RADIUS = 900

# ── The reef model: what one building pilot lays, read off the shipped seat ──
MASS_PRISMS = reef.kite("massPrisms")
GROWTH = reef.kite("growth")
MASS_CAP = reef.kite("massPrismCap")
SX, SY, SZ = reef.kite_vec("massPrismScale")
PRISM_VOLUME = SX * SY * SZ
REFERENCE_MOTES = 3          # a typical one-element loop
LOOP_SECONDS = 8.0           # a loop closed every this-many seconds (the lab's AI pace, rounded up)
MASS_LOOP_SHARE = 0.5        # half the loops are Mass on a Mass reef (the rest Space, Charge, Time)
CLUSTER_PRISMS = min(MASS_CAP, MASS_PRISMS * GROWTH ** (REFERENCE_MOTES - 1))
# A domain of two pilots, clusters only (the Grouper's trail is ignored, which only makes the rate worth MORE).
REFERENCE_DOMAIN_VOLUME = 2 * (ROUND_SECONDS / LOOP_SECONDS) * MASS_LOOP_SHARE * CLUSTER_PRISMS * PRISM_VOLUME
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameReefbuild.asset"

# ── 1. metas ─────────────────────────────────────────────────────────────────
reef.emit_shared(g)
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])

# ── 2. Scoring rule: metric 12 = VolumeRemaining, points mode ────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/ReefbuildScoringRule.asset", G_ASSET["ReefbuildScoringRule"],
             lib.header_for(G_SCRIPT["ReefbuildScoringRuleSO"], "ReefbuildScoringRule") +
             """  metric: 12
  golfRules: 0
  volumeTemplate: '{count} Volume'
  prismsTemplate: '{count} prisms'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: REEF STANDING
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/ReefbuildSettings.asset", G_ASSET["ReefbuildSettings"],
             reef.settings_yaml(reef.SHARED_GUIDS["WrasseReefSettingsSO"], "ReefbuildSettings", "Mass",
                                MASS_SHARE, CRYSTALS, SCATTER_RADIUS, MODE_ID, 12))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2: a reef war with one side is a screensaver (Tapestry's rule).
DESCRIPTION = ("Wrasse and Grouper only - build a reef in open water. The Wrasse loops its tail round Mass motes and a "
               "cluster of your prisms grows where the loop closed; the more motes in one loop, the far bigger the "
               "cluster. Rival kites pop and blast what you build, and yours can do the same to theirs. Gather Mass "
               "crystals and more Mass motes come to the board. Higher intensities thin the crystals. When the clock runs "
               "out, the team with the most reef standing wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameReefbuild"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameReefbuild") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Reefbuild
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameReefbuild
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Reefbuild.asset", G_ASSET["GameToastConfigReefbuild"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Reefbuild") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(130, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Loop Mass motes to grow your reef - everything standing at the whistle scores",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigReefbuild"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Reefbuild.asset", G_ASSET["ModePreviewReefbuild"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Reefbuild") + f"""  Mode: {MODE_ID}
  Notes: 'The bare Barren cell the mode runs in, and nothing else - the arena IS the reef you build in it.
    What the preview teaches is the Mass loop and the cluster it lays. Vessel pinned to Wrasse({reef.WRASSE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['BarrenCellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {reef.WRASSE_ID}
  ObjectiveText: Loop Mass motes to grow your reef
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 150
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewReefbuild"])

# ── 7. Scene: Tapestry's, with the identity and the AI hulls swapped ─────────
errors = []
DURATION = f"  duration: {ROUND_SECONDS}\n"


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["TapestryTimeTurnMonitor"], G_SCRIPT["ReefbuildTimeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["TapestryController"], G_SCRIPT["ReefbuildController"], "controller")
    scene = lib.swap_guid(scene, DONOR["TapestrySettings"], G_ASSET["ReefbuildSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["TapestryScoringRule"], G_ASSET["ReefbuildScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {TIGERMOTH_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {reef.WRASSE_ID}\n    PlayerName: AI", "AI templates", expected=4)
    assert scene.count("  duration: 90\n") == 1, "Tapestry's monitor duration not found once"
    return scene.replace("  duration: 90\n", DURATION, 1)


SCENE_REL = f"{lib.SCENES_DIR}/MinigameReefbuild.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [DURATION])
errors += scene_errs
g.emit_scene("MinigameReefbuild", G_ASSET["MinigameReefbuild.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameReefbuild"])
g.register_always_unlocked()
g.register_build_scene("MinigameTapestry", "MinigameReefbuild", G_ASSET["MinigameReefbuild.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("reefbuildRoundSeconds",
                    after="cleanerRamStrikeTarget" if "cleanerRamStrikeTarget:" in end else "glasshouseRoundSeconds",
                    value=ROUND_SECONDS)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
comeback_at_quarter = 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE
if comeback_at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {comeback_at_quarter:.2f} levels a quarter of the reference "
                  f"({REFERENCE_DOMAIN_VOLUME:.0f}) behind (< 1)")
errors += reef.shared_errors(g, "Reefbuild", MODE_ID, "ReefbuildHint", TOAST_HINT, CRYSTALS)
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultReefbuildRoundSeconds = {ROUND_SECONDS};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultReefbuildRoundSeconds differs from ROUND_SECONDS")
controller = g.read(SCRIPT_PATHS["ReefbuildController"])
if "Plan.Build" not in controller or "IsTimed => true" not in controller:
    errors.append("ReefbuildController no longer arms the Build plan on a timed round")
navigator = g.read(reef.KITE_NAVIGATOR)
if "prism.ownerID = _status.PlayerName;" not in navigator:
    errors.append("KiteNavigator.LayMassQueue no longer names the pilot as the cluster's owner - clusters would score nothing")
errors += reef.scene_identity_errors(
    scene,
    present={**{k: G_SCRIPT[k] for k in ("ReefbuildController", "ReefbuildTimeTurnMonitor")},
             "ReefbuildSettings": G_ASSET["ReefbuildSettings"],
             "ReefbuildScoringRule": G_ASSET["ReefbuildScoringRule"], "Barren": EXISTING["BarrenCellConfig"]},
    absent=DONOR)

print(f"reef model: cluster {CLUSTER_PRISMS:.0f} prisms x {PRISM_VOLUME:.0f} vol every {LOOP_SECONDS:.0f} s "
      f"(half Mass); reference domain {REFERENCE_DOMAIN_VOLUME:.0f}; comeback {COMEBACK_RATE} "
      f"({comeback_at_quarter:.2f} levels a quarter behind)")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + reef.SHARED_MINTED)
