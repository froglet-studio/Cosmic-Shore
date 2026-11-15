#!/usr/bin/env python3
"""
Authors every serialized asset the Sentry game mode needs (GameModes.Sentry = 105).

Sentry is the Meerkat's CHARGE game (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 15, which reserved id 105 and hint
toast 180): a guard war round a crystal heart. Turn Sentinel Mode on (Y) and every soliton you form leaves a SENTINEL
standing at its way in - an afterimage of the hull - that zaps any rival hull inside its guard about five times a
second (MeerkatSolitonKit.ZapVessel, MEERKAT.md §10.3). Every ZAP, already a Bullet-class combat hit in every mode, is
one point. The first DOMAIN to the target wins.

THE ARENA IS CORONA'S, referenced read-only (the /arcadegame rule: reference a cell where the mode wants what it
authors). Nested prism shells round an open heart of omni crystals, cut by gates: every pilot comes in through the gates
for the heart's crystals, so the gates and the heart are the posts a sentinel pays at, and a soliton's tube (which
phases the hull) is the way through a shell where there is no gate. Its ladder is already the guard ladder Sentry wants:
  I1 Halo          - one shell, eight wide gates, four omni crystals.
  I2 Corona        - two shells, six gates each, turned; three omni crystals.
  I3 Chromosphere  - three shells, five danger-rimmed gates each; two omni crystals.
  I4 Flare         - four shells, four narrow rimmed gates each; one omni crystal.
More shells mean more gates to cover and fewer crystals mean every pilot converges on fewer points.

NOTHING ABOUT THE MEERKAT CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the arena and the
AI wardens (MeerkatArcadeSteer: steering only; the hull's own autopilot forms the solitons and turns Sentinel Mode on).
The scene is cloned from Corona's (the newest CombatPoints duel, whose controller declares settings / rule / arenaCell
as this one does), keeping its cells, spawn ring and heart crystals.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_sentry_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import meerkat_games_common as mk  # noqa: E402

MODE_ID = 105
TOAST_HINT = 180
g = lib.Generator(MODE_ID, "Sentry")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "SentryController":      f"{AR}/Sentry/SentryController.cs",
    "SentrySettingsSO":      f"{AR}/Sentry/SentrySettingsSO.cs",
    "SentryScoringRuleSO":   f"{AR}/Sentry/SentryScoringRuleSO.cs",
    "SentryZapTurnMonitor":  f"{AR}/TurnMonitors/SentryZapTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameSentry":      guid("asset/ArcadeGameSentry"),
    "SentryScoringRule":     guid("asset/SentryScoringRule"),
    "SentrySettings":        guid("asset/SentrySettings"),
    "GameToastConfigSentry": guid("asset/GameToastConfigSentry"),
    "ModePreviewSentry":     guid("asset/ModePreviewSentry"),
    "MinigameSentry.unity":  guid("asset/MinigameSentry.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Meerkat"] = lib.VESSELS["Meerkat"]                      # SO_Class_Meerkat.asset
# Corona's arena, minted by author_corona_assets.py with the same guid function.
CELLS = [guid(f"asset/CoronaCellConfig{i}") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"CoronaCellConfig{i}"] = gd
CELL_FILES = [f"Assets/_SO_Assets/Cell Configs/Corona Cell/Corona Cell Config {i}.asset" for i in (1, 2, 3, 4)]
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameCorona.unity"
DONOR = {
    "CoronaController":       guid("script/CoronaController"),
    "CoronaBloomTurnMonitor": guid("script/CoronaBloomTurnMonitor"),
    "CoronaSettings":         guid("asset/CoronaSettings"),
    "CoronaScoringRule":      guid("asset/CoronaScoringRule"),
}
MANTA_ID = lib.VESSEL_CLASS_ID["Manta"]
MEERKAT_ID = lib.VESSEL_CLASS_ID["Meerkat"]

# ── The race ─────────────────────────────────────────────────────────────────
ZAP_TARGET = 40           # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultSentryZapTarget
ZAP_POINTS = 1
BULLET_CLASS = 0          # CombatHitClass.Bullet - what MeerkatSolitonKit.ZapVessel reports
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (10 zaps, two seconds
# of one rival in one guard) buys 1.2 element levels; on the Meerkat, Charge is the one that moves a number in this
# fight (zapPetals, and Six Sentinels at 5), Space the size of the soliton, so the comeback reaches the guard.
COMEBACK_RATE = 0.12

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameSentry.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Sentry")
g.text_meta(f"{AR}/SENTRY.md")
mk.shared_metas(g)

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/SentryScoringRule.asset", G_ASSET["SentryScoringRule"],
             lib.header_for(G_SCRIPT["SentryScoringRuleSO"], "SentryScoringRule") +
             f"""  metric: 8
  golfRules: 1
  zapPoints: {ZAP_POINTS}
  otherHitPoints: 0
  zapsLeftTemplate: '{{count}} Zaps Left'
  zapsLandedTemplate: '{{count}} zaps'
  victoryHeader: VICTORY
  victoryLabel: SENTRY TIME
  defeatHeader: DEFEAT
  defeatLabel: ZAPS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":  ([2.5, 1.8, 1.2, 0.8], "down"),
    "aiHuntRange":        ([250, 400, 550, 700], "up"),
    "aiLeadSeconds":      ([0, 0.6, 1.2, 1.8], "up"),
    "aiAimError":         ([120, 70, 35, 10], "down"),
    "aiHoldHeartChance":  ([0.7, 0.5, 0.35, 0.25], "down"),
}
SHARED = {"aiHumanFocus": 1.5, "aiHeartAimRadius": 60}

g.emit_asset("Assets/_SO_Assets/Games/SentrySettings.asset", G_ASSET["SentrySettings"],
             lib.header_for(G_SCRIPT["SentrySettingsSO"], "SentrySettings")
             + "".join(mk.ladder_yaml(n, LADDERS) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a sentinel never zaps a team-mate, so a solo lobby has nobody to zap.
# No colons in the description: a plain YAML scalar would start a new key at one (the Serpentine trap).
DESCRIPTION = ("Meerkats only - shells of prisms round a heart of omni crystals, and every pilot comes in through the "
               "gates. Press Y for Sentinel Mode and every soliton you form leaves a sentinel standing at its way in, "
               "zapping any rival who flies into its guard. Every zap is a point that silences them and shakes their "
               "petals loose. Form your solitons where rivals pass - at a gate, on the way to the heart - and tunnel "
               "through a shell where there is no gate. Higher intensities add shells and gates, rim them in danger and "
               "thin the crystals. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameSentry"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameSentry") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Sentry
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameSentry
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Meerkat']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Sentry.asset", G_ASSET["GameToastConfigSentry"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Sentry") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(128, "{0} is a quarter of the way to {2} zaps", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} zaps", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Press Y for Sentinel Mode - every soliton leaves a sentinel at its way in. Form "
                       "them where rivals pass", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigSentry"])


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


CELL_BLOCK = "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"
RINGS = mk.scene_spawn_rings(g.read(DONOR_SCENE))

# ── 6. Mode preview (Corona's arena, Corona's ring) ──────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Sentry.asset", G_ASSET["ModePreviewSentry"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Sentry") + f"""  Mode: {MODE_ID}
  Notes: 'Corona''s shells at each intensity - more shells and gates, then danger rims, and fewer crystals in
    the heart. OPEN-ENDED - it scores on sentinels zapping rival pilots and a solo preview has none; what it
    teaches is the gates, the heart and a soliton through a shell. Vessel pinned to Meerkat({MEERKAT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {MEERKAT_ID}
  ObjectiveText: Press Y, then form solitons where rivals fly
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
g.register_preview(G_ASSET["ModePreviewSentry"])

# ── 7. Scene: Corona's, with the identity and the AI hulls swapped (its cells, ring and crystals kept) ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["CoronaBloomTurnMonitor"], G_SCRIPT["SentryZapTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["CoronaController"], G_SCRIPT["SentryController"], "controller")
    scene = lib.swap_guid(scene, DONOR["CoronaSettings"], G_ASSET["SentrySettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["CoronaScoringRule"], G_ASSET["SentryScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, CELL_BLOCK, CELL_BLOCK, "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {MANTA_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {MEERKAT_ID}\n    PlayerName: AI", "AI templates", expected=4)
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameSentry.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [CELL_BLOCK])
errors += scene_errs
g.emit_scene("MinigameSentry", G_ASSET["MinigameSentry.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameSentry"])
g.register_always_unlocked()
g.register_build_scene("MinigameCorona", "MinigameSentry", G_ASSET["MinigameSentry.unity"])
g.set_end_condition("sentryZapTarget", after="glasshouseRoundSeconds", value=ZAP_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * ZAP_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {ZAP_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * ZAP_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions about the Meerkat, read from the shipped kit (never edited here): a zap is a Bullet-class
# hit admitted through TryAdmit and raised on the game's combat channel, and an AI turns Sentinel Mode on by itself.
kit = g.read(mk.KIT)
for needle in ("CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Bullet, config.ZapCooldownSeconds",
               "HitClass = CombatHitClass.Bullet", "config.OnCombatHitLanded.Raise(",
               "if (IsAuthority && _status.AutoPilotEnabled) AutopilotSentinelMode();"):
    if needle not in kit:
        errors.append(f"MeerkatSolitonKit no longer has '{needle}' - Sentry's rule pays a sentinel's zaps and its AI "
                      f"relies on the hull turning Sentinel Mode on; re-check how the Meerkat zaps")
config = g.read(mk.CONFIG)
if f"onCombatHitLanded: {{fileID: 11400000, guid: {mk.COMBAT_HIT_CHANNEL}" not in config:
    errors.append("MeerkatSolitonConfig.onCombatHitLanded is no longer the game's combat-hit channel - zaps would score nothing")
if "  zapCooldownSeconds: 0\n" not in config:
    errors.append("MeerkatSolitonConfig.zapCooldownSeconds is no longer 0 - re-size ZAP_TARGET to the new zap rate")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count(f"Bullet = {BULLET_CLASS},"):
    errors.append(f"CombatHitClass.Bullet is no longer {BULLET_CLASS}")
errors += mk.enum_errors(g, "Sentry", MODE_ID, "SentryHint", TOAST_HINT)

# The C# default target matches the authored one.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
for needle in (f"DefaultSentryZapTarget = {ZAP_TARGET};", f"public int sentryZapTarget = {ZAP_TARGET};",
               f"public int sentryZapTargetBuild = {ZAP_TARGET};"):
    if needle not in eco:
        errors.append(f"EndConditionOverridesSO does not carry '{needle}' (ZAP_TARGET)")

# The ladders: each monotone toward harder; C# initializers agree; the AI only steers.
errors += mk.ladder_errors(g.read(SCRIPT_PATHS["SentrySettingsSO"]), "SentrySettingsSO", LADDERS, SHARED)
errors += mk.steer_errors(g, [SCRIPT_PATHS["SentryController"]])

# The arena: Corona's four rungs still exist and still have no nucleus (the heart is open, the crystals are the
# scene's CrystalManager's, as Corona authors them).
for rel, gd in zip(CELL_FILES, CELLS):
    if lib.existing_guid(rel) != gd:
        errors.append(f"{rel} is not Corona's cell any more (guid {gd}) - Sentry references it read-only")
    elif "  NucleusPrefab: {fileID: 0}\n" not in g.read(rel):
        errors.append(f"{rel} grew a nucleus - Sentry's AI holds the heart; re-check the arena")

# The scene: every new identity present, every donor one gone, four Meerkat AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("SentryController", "SentryZapTurnMonitor")},
                     "SentrySettings": G_ASSET["SentrySettings"],
                     "SentryScoringRule": G_ASSET["SentryScoringRule"]}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {MEERKAT_ID}\n") != 4 or f"  - vesselClass: {MANTA_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Meerkat AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Sentry"), guid(f"text/{AR}/SENTRY.md")] + mk.shared_minted())
