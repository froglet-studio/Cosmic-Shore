#!/usr/bin/env python3
"""
Authors every serialized asset the Marionette game mode needs (GameModes.Marionette = 126, hint toast 201 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 36-37 reserved for the Jewel Wasp).

Marionette is the Jewel Wasp's CHARGE game. A jewel wasp stings a cockroach and walks it home by its antenna; this one
stings a creature's heart and flies it into a rival pilot. In a host, RT at a rival vessel plays the creature's attack
and lands a Strike (CombatHitClass.Strike, one point; Charge shortens its recharge, 3 s to 0.8 s). With Charge 5,
leaving the creature bursts it on every rival vessel within reach (the wasp's burstHitClass, MissileBlast, one point a
hull). The first DOMAIN to the target wins, golf-timed.

THE ARENA IS KELPWOOD'S, referenced read-only (the Angler's Charge game flies it): four cells of kelp strands (28, 44,
64, 88; danger tips and collars on the upper rungs) with herds grazing among them (8, 6, 5, 4) and omni crystals (3,
2, 2, 1). Why not a fork: the wasp needs exactly what Kelpwood already authors - creatures to take, cover between the
pilots, and an omni crystal that hatches a predator it rides at once. The ladder is more kelp and fewer creatures.

NOTHING ABOUT THE WASP CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the kelp, the herds
and the crystals (Kelpwood's cells, the scene's CrystalManager) and the AI puppeteers, which only steer the FREE wasp:
its own AI stings the creature ahead of its nose and flies a host at the nearest rival with the attack held
(wasp_games_common.AUTOPILOT_NEEDLES), so the puppeteer only chooses which creature is ahead.

The scene is cloned from Kelpwood's (a CombatPoints duel whose controller declares settings / rule / arenaCell as this
one does), with the identity and the AI hulls swapped and everything else kept.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_marionette_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import wasp_games_common as common  # noqa: E402

MODE_ID = 126
TOAST_HINT = 201
g = lib.Generator(MODE_ID, "Marionette")
guid = lib.guid

AR = common.AR
SCRIPT_PATHS = {
    "MarionetteController":        f"{AR}/Marionette/MarionetteController.cs",
    "MarionetteSettingsSO":        f"{AR}/Marionette/MarionetteSettingsSO.cs",
    "MarionetteScoringRuleSO":     f"{AR}/Marionette/MarionetteScoringRuleSO.cs",
    "MarionettePuppeteer":         f"{AR}/Marionette/MarionettePuppeteer.cs",
    "MarionetteStrikeTurnMonitor": f"{AR}/TurnMonitors/MarionetteStrikeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameMarionette":      guid("asset/ArcadeGameMarionette"),
    "MarionetteScoringRule":     guid("asset/MarionetteScoringRule"),
    "MarionetteSettings":        guid("asset/MarionetteSettings"),
    "GameToastConfigMarionette": guid("asset/GameToastConfigMarionette"),
    "ModePreviewMarionette":     guid("asset/ModePreviewMarionette"),
    "MinigameMarionette.unity":  guid("asset/MinigameMarionette.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_JewelWasp"] = common.WASP_VESSEL
# The donor scene's identity, minted by author_kelpwood_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameKelpwood.unity"
DONOR = {
    "KelpwoodController":      guid("script/KelpwoodController"),
    "KelpwoodBiteTurnMonitor": guid("script/KelpwoodBiteTurnMonitor"),
    "KelpwoodSettings":        guid("asset/KelpwoodSettings"),
    "KelpwoodScoringRule":     guid("asset/KelpwoodScoringRule"),
}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Kelpwood Cell"
CELLS = [guid(f"asset/KelpwoodCellConfig{i}") for i in (1, 2, 3, 4)]   # Kelpwood's own, referenced read-only
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"KelpwoodCell{i}"] = gd
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
WASP_ID = common.WASP_ID

# ── The duel ─────────────────────────────────────────────────────────────────
STRIKE_TARGET = 12       # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultMarionetteStrikeTarget
STRIKE_POINTS = 1
BURST_POINTS = 1
STRIKE_CLASS = 5         # CombatHitClass.Strike - JewelWaspConfig.asset's attackHitClass
BURST_CLASS = 3          # CombatHitClass.MissileBlast - JewelWaspConfig.asset's burstHitClass
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (3 strikes) buys 1.2
# element levels; on the wasp, Charge is the attack's recharge (3 s to 0.8 s), so the comeback reaches the strike.
COMEBACK_RATE = 0.4
KELPWOOD_SPAWN = [368, 400, 432, 464]     # Kelpwood's ring, kept

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameMarionette.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = common.emit_shared(g)
g.folder_meta(f"{AR}/Marionette")
g.text_meta(f"{AR}/MARIONETTE.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/MarionetteScoringRule.asset", G_ASSET["MarionetteScoringRule"],
             lib.header_for(G_SCRIPT["MarionetteScoringRuleSO"], "MarionetteScoringRule") +
             f"""  metric: 8
  golfRules: 1
  strikePoints: {STRIKE_POINTS}
  burstPoints: {BURST_POINTS}
  otherHitPoints: 0
  hitsLeftTemplate: '{{count}} Hits Left'
  hitsLandedTemplate: '{{count}} strikes'
  victoryHeader: VICTORY
  victoryLabel: PUPPET TIME
  defeatHeader: DEFEAT
  defeatLabel: HITS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds": ([3, 2.2, 1.5, 1], "down"),
    "aiHostChoosiness":  ([0.25, 0.5, 0.75, 0.95], "up"),
    "aiAimError":        ([60, 35, 15, 5], "down"),
}
SHARED = {"aiHumanFocus": 1.5}

g.emit_asset("Assets/_SO_Assets/Games/MarionetteSettings.asset", G_ASSET["MarionetteSettings"],
             lib.header_for(G_SCRIPT["MarionetteSettingsSO"], "MarionetteSettings")
             + "".join(common.ladder_yaml(LADDERS, n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a strike never lands on a team-mate, so a solo lobby has nobody to hit.
DESCRIPTION = ("Jewel Wasps only - a duel in the kelp. Sting a creature with a pull of RT and ride it: point it at a "
               "rival pilot and RT plays its attack, and every attack that lands is a point. Charge makes the attack "
               "ready sooner, and at Charge 5 leaving a creature bursts it on every rival near it. An omni crystal "
               "hatches a predator you ride at once. Higher intensities grow more kelp and thin the herds. First team "
               "to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameMarionette"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameMarionette") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Marionette
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameMarionette
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_JewelWasp']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Marionette.asset", G_ASSET["GameToastConfigMarionette"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Marionette") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(81, "<b>{0}</b> pulled the strings - a strike on a rival!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Pull RT to sting a creature and ride it. Point it at a rival and pull RT to strike",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigMarionette"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Marionette.asset", G_ASSET["ModePreviewMarionette"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Marionette") + f"""  Mode: {MODE_ID}
  Notes: 'Kelpwood''s kelp and herds at each intensity. OPEN-ENDED - it scores on strikes landing on rival
    pilots and a solo preview has none; what it teaches is the sting, the ride and the herds to choose from.
    Vessel pinned to JewelWasp({WASP_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {WASP_ID}
  ObjectiveText: Sting a creature, ride it to a rival pilot and strike
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {KELPWOOD_SPAWN[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in KELPWOOD_SPAWN) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewMarionette"])

# ── 7. Scene: Kelpwood's, with the identity and the AI hulls swapped ──────────
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["KelpwoodBiteTurnMonitor"], G_SCRIPT["MarionetteStrikeTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["KelpwoodController"], G_SCRIPT["MarionetteController"], "controller")
    scene = lib.swap_guid(scene, DONOR["KelpwoodSettings"], G_ASSET["MarionetteSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["KelpwoodScoringRule"], G_ASSET["MarionetteScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {WASP_ID}\n    PlayerName: AI", "AI templates", expected=4)
    return scene


CONTROLLER_TAIL = (f"  settings: {{fileID: 11400000, guid: {G_ASSET['MarionetteSettings']}, type: 2}}\n"
                   f"  rule: {{fileID: 11400000, guid: {G_ASSET['MarionetteScoringRule']}, type: 2}}\n"
                   "  arenaCell: {fileID: 1700000065}\n")
AUTHORED_BLOCKS = ["  CellConfigs:\n" + common.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", CONTROLLER_TAIL]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameMarionette.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameMarionette", G_ASSET["MinigameMarionette.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameMarionette"])
g.register_always_unlocked()
g.register_build_scene("MinigameKelpwood", "MinigameMarionette", G_ASSET["MinigameMarionette.unity"])
g.set_end_condition("marionetteStrikeTarget", after="longshotRoundSeconds", value=STRIKE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * STRIKE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {STRIKE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * STRIKE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions: the attack and the burst are combat hits of the config's classes, raised on the
# scoring channel.
executor = g.read(common.WASP_EXECUTOR)
for needle in ("TryStrikeAround(host, config.AttackReach, config.AttackHitClass,",
               "TryStrikeAround(host, config.BurstRadius, config.BurstHitClass, 0.5f, allInRange: true);",
               "if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, cooldown, ElementalDebuffSources.VesselContact,",
               "var channel = config.OnCombatHitLanded;"):
    if needle not in executor:
        errors.append(f"JewelWaspExecutor no longer has '{needle}' - Marionette pays a ridden creature's attack and "
                      f"burst on rival hulls; re-check how the wasp strikes")
cfg = g.read(common.WASP_CONFIG)
if f"  attackHitClass: {STRIKE_CLASS}\n" not in cfg:
    errors.append("JewelWaspConfig.asset's attackHitClass is no longer Strike - MarionetteScoringRuleSO pays Strike")
if f"  burstHitClass: {BURST_CLASS}\n" not in cfg:
    errors.append("JewelWaspConfig.asset's burstHitClass is no longer MissileBlast - MarionetteScoringRuleSO pays it")
if f"  onCombatHitLanded: {{fileID: 11400000, guid: {common.COMBAT_CHANNEL}" not in cfg:
    errors.append("JewelWaspConfig.asset's onCombatHitLanded is no longer the game's combat-hit channel - strikes "
                  "would score nothing")
hit_cs = g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs")
if f"Strike = {STRIKE_CLASS}," not in hit_cs or f"MissileBlast = {BURST_CLASS}," not in hit_cs:
    errors.append("CombatHitClass.Strike / MissileBlast moved")
rule_cs = g.read(SCRIPT_PATHS["MarionetteScoringRuleSO"])
for needle in ("CombatHitClass.Strike => strikePoints,", "CombatHitClass.MissileBlast => burstPoints,",
               "_ => otherHitPoints,"):
    if needle not in rule_cs:
        errors.append(f"MarionetteScoringRuleSO no longer has '{needle}'")
if f"MarionetteHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.MarionetteHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md rows 36-37)")
if f"Marionette = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Marionette is not {MODE_ID} (GAME_KIT_ROSTER.md rows 36-37)")
if f"DefaultMarionetteStrikeTarget = {STRIKE_TARGET};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultMarionetteStrikeTarget differs from STRIKE_TARGET")

errors += common.common_errors(g, SCRIPT_PATHS["MarionettePuppeteer"], "Marionette")
errors += common.ladder_errors(g.read(SCRIPT_PATHS["MarionetteSettingsSO"]), LADDERS, SHARED, "MarionetteSettingsSO")

# The arena: every rung has creatures to take.
for i in (1, 2, 3, 4):
    body = g.read(f"{CELL_DIR}/Kelpwood Cell Config {i}.asset")
    if "  SpawnProfile: {fileID: 0}" in body:
        errors.append(f"Kelpwood Cell Config {i} spawns nothing - the wasp would have no creature to take")

# The scene: every new identity present, every donor one gone, four wasp AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("MarionetteController", "MarionetteStrikeTurnMonitor")},
                     "MarionetteSettings": G_ASSET["MarionetteSettings"],
                     "MarionetteScoringRule": G_ASSET["MarionetteScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {WASP_ID}\n") != 4 or f"  - vesselClass: {ANGLER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Jewel Wasp AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/Marionette"), guid(f"text/{AR}/MARIONETTE.md")])
