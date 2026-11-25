#!/usr/bin/env python3
"""
Authors every serialized asset the Larder game mode needs (GameModes.Larder = 102, hint toast 177 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 12 reserved for it).

Larder is the Shrike's CHARGE game. A shrike keeps a larder: prey hung on thorns. Here the thorns are the Dog Fight's
wreckage and the prey is a rival pilot. Every crystal becomes a missile plant where it was taken and every nest is
crowned with one (SHRIKE.md §3.5); paint a rival with the Targeting Array (Charge mode, RT held) and every plant with a
ready missile launches at them. A missile that reaches a rival hull is one point (a missile-class combat hit, already
counted platform-wide as MissileHitsLanded). The first DOMAIN to the target wins, golf-timed.

THE ARENA IS THE BONEYARD, referenced read-only (Dog Fight, Salvo and Breakwater fly it too): four cells of wreckage
inside r = 520 - hollow hulks, leaning spires, girder cages, broken overpasses, danger traps on the torn ends. Why not
a fork: the Shrike needs exactly what the Boneyard already authors - mass to dock on (a nest is left where you dock),
cover a missile blows up on, and sight lines between the cover for the paint. Its ladder is more wreckage per rung.

NOTHING ABOUT THE SHRIKE CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the wreckage (the
Boneyard's cells), the crystals per intensity (the scene's CrystalManager) and the AI hunters, which only steer: the
Shrike's own autopilot paints when a rival is in its cone (shrike_games_common.AUTOPILOT_NEEDLES).

The scene is cloned from Kelpwood's (a CombatPoints duel whose controller declares settings / rule / arenaCell as this
one does), with the cells, the AI hulls, the spawn ring and the crystals swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_larder_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import shrike_games_common as common  # noqa: E402

MODE_ID = 102
TOAST_HINT = 177
g = lib.Generator(MODE_ID, "Larder")
guid = lib.guid

AR = common.AR
SCRIPT_PATHS = {
    "LarderController":          f"{AR}/Larder/LarderController.cs",
    "LarderSettingsSO":          f"{AR}/Larder/LarderSettingsSO.cs",
    "LarderScoringRuleSO":       f"{AR}/Larder/LarderScoringRuleSO.cs",
    "LarderHunter":              f"{AR}/Larder/LarderHunter.cs",
    "LarderMissileTurnMonitor":  f"{AR}/TurnMonitors/LarderMissileTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameLarder":      guid("asset/ArcadeGameLarder"),
    "LarderScoringRule":     guid("asset/LarderScoringRule"),
    "LarderSettings":        guid("asset/LarderSettings"),
    "GameToastConfigLarder": guid("asset/GameToastConfigLarder"),
    "ModePreviewLarder":     guid("asset/ModePreviewLarder"),
    "MinigameLarder.unity":  guid("asset/MinigameLarder.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Shrike"] = common.SHRIKE_VESSEL
BONEYARD_DIR = "Assets/_SO_Assets/Cell Configs/Boneyard Cell"
CELLS = [lib.existing_guid(f"{BONEYARD_DIR}/Boneyard Cell Config {i}.asset") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"BoneyardCell{i}"] = gd
SHRIKE_PREFAB = "Assets/_Prefabs/Spacevessels/Shrike.prefab"
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
SHRIKE_ID = common.SHRIKE_ID

# ── The hunt ─────────────────────────────────────────────────────────────────
MISSILE_TARGET = 16      # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultLarderMissileTarget
MISSILE_POINTS = 1
MISSILE_BLAST_CLASS = 3  # CombatHitClass.MissileBlast - ShrikeConfig.asset's missileHitClass
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (4 hits) buys 1.2
# element levels; on the Shrike, Space widens the lock cone (5 -> 14 degrees), so the comeback reaches the paint.
COMEBACK_RATE = 0.3

BONEYARD_RADIUS = 520     # the wreckage's reach (the Boneyard cells' descriptions)
SPAWN_RINGS = [700, 700, 700, 700]                     # Dog Fight's ring round the same wreckage
CRYSTAL_BALL = 420                                     # Dog Fight's noNucleusSpawnRadius: crystals among the wreckage
CRYSTALS = [6, 5, 4, 3]                                # every one is a missile plant: fewer per rung

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameLarder.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = common.emit_shared(g)
g.folder_meta(f"{AR}/Larder")
g.text_meta(f"{AR}/LARDER.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/LarderScoringRule.asset", G_ASSET["LarderScoringRule"],
             lib.header_for(G_SCRIPT["LarderScoringRuleSO"], "LarderScoringRule") +
             f"""  metric: 8
  golfRules: 1
  missilePoints: {MISSILE_POINTS}
  otherHitPoints: 0
  hitsLeftTemplate: '{{count}} Hits Left'
  hitsLandedTemplate: '{{count}} missiles'
  victoryHeader: VICTORY
  victoryLabel: LARDER TIME
  defeatHeader: DEFEAT
  defeatLabel: HITS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":     ([2.5, 1.8, 1.2, 0.8], "down"),
    "aiLeadSeconds":         ([0, 0.2, 0.4, 0.6], "up"),
    "aiAimError":            ([70, 40, 18, 6], "down"),
    "aiReloadBelowMissiles": ([1, 2, 3, 4], "up"),
}
SHARED = {"aiCrystalRange": 700, "aiHumanFocus": 1.5}

g.emit_asset("Assets/_SO_Assets/Games/LarderSettings.asset", G_ASSET["LarderSettings"],
             lib.header_for(G_SCRIPT["LarderSettingsSO"], "LarderSettings")
             + "".join(common.ladder_yaml(LADDERS, n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a paint never takes a team-mate, so a solo lobby has nobody to hit.
DESCRIPTION = ("Shrikes only - a duel in the wreckage. Every crystal you take becomes a missile plant where you took it, "
               "and every nest you leave is crowned with one. Hold the Targeting Array on a rival pilot to paint them and "
               "every plant with a missile ready launches at them; every missile that reaches them is a point. The "
               "wreckage is cover - a missile that meets a block blows on it. Higher intensities pile up more wreckage "
               "and thin the crystals. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameLarder"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameLarder") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Larder
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameLarder
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Shrike']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Larder.asset", G_ASSET["GameToastConfigLarder"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Larder") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(81, "<b>{0}</b> hung one up - a missile hit a rival!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} hits", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} hits", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Take crystals to grow missile plants. Hold the Targeting Array on a rival - every "
                       "plant launches at them", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigLarder"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Larder.asset", G_ASSET["ModePreviewLarder"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Larder") + f"""  Mode: {MODE_ID}
  Notes: 'The Boneyard''s wreckage at each intensity, with fewer crystals each rung. OPEN-ENDED - it scores on
    missiles reaching rival pilots and a solo preview has none; what it teaches is the wreckage to dock on and
    hide behind and the crystals that become missile plants. Vessel pinned to Shrike({SHRIKE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SHRIKE_ID}
  ObjectiveText: Take crystals for missile plants, then paint a rival pilot
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in SPAWN_RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewLarder"])

# ── 7. Scene: Kelpwood's, with the identity, the cells, the AI hulls, the spawn ring and the crystals swapped ──
errors = []
SPAWN = common.spawn_block(SPAWN_RINGS)
DONOR_SPAWN = common.spawn_block([368, 400, 432, 464])      # Kelpwood's ring
OMNI = common.omni_block(CRYSTAL_BALL, CRYSTALS)
DONOR_OMNI = common.omni_block(200, [3, 2, 2, 1])           # Kelpwood's crystals


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["KelpwoodBiteTurnMonitor"], G_SCRIPT["LarderMissileTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["KelpwoodController"], G_SCRIPT["LarderController"], "controller")
    scene = lib.swap_guid(scene, DONOR["KelpwoodSettings"], G_ASSET["LarderSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["KelpwoodScoringRule"], G_ASSET["LarderScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + common.cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + common.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SHRIKE_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + common.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameLarder.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameLarder", G_ASSET["MinigameLarder.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameLarder"])
g.register_always_unlocked()
g.register_build_scene("MinigameKelpwood", "MinigameLarder", G_ASSET["MinigameLarder.unity"])
g.set_end_condition("larderMissileTarget", after="glasshouseRoundSeconds", value=MISSILE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * MISSILE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {MISSILE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * MISSILE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions: a missile reaching a rival hull is admitted as the config's missile class and raised
# on the scoring channel; every crystal becomes a plant.
executor = g.read(common.SHRIKE_EXECUTOR)
for needle in ("ScoreHit(victim, config.MissileHitClass, ElementalDebuffSources.Explosion",
               "if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, config.HitCooldown, source, out int superseded)) return;",
               "config.OnCombatHitLanded.Raise(new CombatHitStats", "public void CollectCrystal(Vector3 at)"):
    if needle not in executor:
        errors.append(f"ShrikeExecutor no longer has '{needle}' - Larder pays a missile reaching a rival hull and "
                      f"grows its ammunition from crystals; re-check how the Shrike's missiles hit")
cfg = g.read(common.SHRIKE_CONFIG)
if f"  missileHitClass: {MISSILE_BLAST_CLASS}\n" not in cfg:
    errors.append("ShrikeConfig.asset's missileHitClass is no longer MissileBlast - LarderScoringRuleSO pays every "
                  "missile class, so check the new class is a missile (CombatHitClasses.IsMissile)")
if "  onCombatHitLanded: {fileID: 11400000, guid: 45fb43a0cddb5cc3cc4dc448df994152" not in cfg:
    errors.append("ShrikeConfig.asset's onCombatHitLanded is no longer the game's combat-hit channel - missile hits "
                  "would score nothing")
if f"MissileBlast = {MISSILE_BLAST_CLASS}," not in g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs"):
    errors.append(f"CombatHitClass.MissileBlast is no longer {MISSILE_BLAST_CLASS}")
rule_cs = g.read(SCRIPT_PATHS["LarderScoringRuleSO"])
if "CombatHitClasses.IsMissile(hitClass) ? missilePoints : otherHitPoints" not in rule_cs:
    errors.append("LarderScoringRuleSO no longer pays every missile class and nothing else")
if f"LarderHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.LarderHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md row 12)")
if f"Larder = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Larder is not {MODE_ID} (GAME_KIT_ROSTER.md row 12)")
if f"DefaultLarderMissileTarget = {MISSILE_TARGET};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultLarderMissileTarget differs from MISSILE_TARGET")

errors += common.common_errors(g, SCRIPT_PATHS["LarderHunter"], "Larder")
errors += common.ladder_errors(g.read(SCRIPT_PATHS["LarderSettingsSO"]), LADDERS, SHARED, "LarderSettingsSO")

# The arena: crystals among the wreckage, pilots outside it.
for i, ring in enumerate(SPAWN_RINGS, 1):
    if not (BONEYARD_RADIUS + 100 <= ring <= lib.MEMBRANE_RADIUS - 300):
        errors.append(f"I{i}: spawn ring {ring} must sit outside the {BONEYARD_RADIUS} u wreckage and inside the membrane")
if CRYSTAL_BALL > BONEYARD_RADIUS:
    errors.append(f"the crystals' {CRYSTAL_BALL} u ball reaches out of the wreckage")
if any(b > a for a, b in zip(CRYSTALS, CRYSTALS[1:])) or min(CRYSTALS) < 1:
    errors.append(f"the crystal ladder must run down with intensity and never reach 0 ({CRYSTALS})")
for i in (1, 2, 3, 4):
    body = g.read(f"{BONEYARD_DIR}/Boneyard Cell Config {i}.asset")
    if "  EnvironmentPrefab: {fileID: 0}" in body:
        errors.append(f"Boneyard Cell Config {i} lays no wreckage any more - the Shrike would have nothing to dock on")
    if "  NucleusPrefab: {fileID: 0}" not in body:
        errors.append(f"Boneyard Cell Config {i} grew a nucleus - the crystals would move into it")

# The scene: every new identity present, every donor one gone, four Shrike AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("LarderController", "LarderMissileTurnMonitor")},
                     "LarderSettings": G_ASSET["LarderSettings"], "LarderScoringRule": G_ASSET["LarderScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Kelpwood cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {SHRIKE_ID}\n") != 4 or f"  - vesselClass: {ANGLER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Shrike AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/Larder"), guid(f"text/{AR}/LARDER.md")])
