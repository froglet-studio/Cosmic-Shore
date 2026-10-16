#!/usr/bin/env python3
"""
Authors every serialized asset the Serpent Charge game mode needs (GameModes.SerpentCharge = 67).

Serpent Charge is the Serpent's CHARGE game - a sniper DUEL. A Charge game is about hitting other
pilots with your abilities (the Space games are the ones about destroying prisms - Garrett,
2026-10-16), so the only thing that scores is a SNIPE: a Sniper Shot (1.5 deg, 3,000 u hitscan
cone) that strips a rival pilot - a Debuff-class combat hit, credited into CombatPoints at
SNIPE_POINTS (SerpentChargeScoringRuleSO). First DOMAIN to SNIPE_TARGET wins, golf-timed (the
Dustup shape, ScoringMetric.CombatPoints). On the Serpent, Charge is the rifle's RECOVERY (12 s at
rest -> 5.4 s at Charge 10), and the arena is seeded with waves of CHARGE crystals. The AI is a
pilot hunter (SerpentMarksmanAI) whose difficulty is a per-intensity ladder. See
Assets/_Scripts/Controller/Arcade/SERPENTCHARGE.md.

THE ARENA IS RAMPAGE'S, referenced read-only - ONE config, Rampage Cell Config 4 (the forest is
COVER, not the score, and config 4 is the rung measured at 58.7 FPS in a build). THE SCENE is a
clone of MinigameSirocco.unity (Rampage's spawn ring, a controller block of settings / rule /
arenaCell - the field names this controller declares), with the identity swapped, the four AI
hulls Butterfly(13) -> Serpent(7) and the cell narrowed to config 4. Once the scene is committed
the clone STANDS DOWN (arcade_mode_lib.committed_scene); the snipe redesign's two scene edits (the
turn monitor and the cell block) are applied to the committed scene as an idempotent PATCH.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_serpent_charge_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402

MODE_ID = 67
g = lib.Generator(MODE_ID, "SerpentCharge")
guid = lib.guid

SCRIPT_PATHS = {
    "SerpentChargeController":       "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentChargeController.cs",
    "SerpentChargeSettingsSO":       "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentChargeSettingsSO.cs",
    "SerpentChargeScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentChargeScoringRuleSO.cs",
    "SerpentMarksmanAI":             "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentMarksmanAI.cs",
    "SerpentChargeSnipeTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/SerpentChargeSnipeTurnMonitor.cs",
    "SerpentChargeScoringRuleTests": "Assets/_Scripts/Tests/Editor/SerpentChargeScoringRuleTests.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameSerpentCharge":      guid("asset/ArcadeGameSerpentCharge"),
    "SerpentChargeScoringRule":     guid("asset/SerpentChargeScoringRule"),
    "SerpentChargeSettings":        guid("asset/SerpentChargeSettings"),
    "GameToastConfigSerpentCharge": guid("asset/GameToastConfigSerpentCharge"),
    "ModePreviewSerpentCharge":     guid("asset/ModePreviewSerpentCharge"),
    "MinigameSerpentCharge.unity":  guid("asset/MinigameSerpentCharge.unity"),
}

# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Serpent"] = lib.VESSELS["Serpent"]
RAMPAGE_DIR = "Assets/_SO_Assets/Cell Configs/Rampage Cell"
RAMPAGE = [lib.existing_guid(f"{RAMPAGE_DIR}/Rampage Cell Config {i}.asset") for i in range(1, 5)]
for i, gd in enumerate(RAMPAGE, 1):
    EXISTING[f"RampageCellConfig{i}"] = gd
ARENA_CONFIG = RAMPAGE[3]   # Rampage Cell Config 4 - the only arena
# The prism-race design's turn monitor, retired by the snipe redesign. Its guid is still minted by
# the same function, so the scene patch can find it and the validation can prove it is gone.
RETIRED_MONITOR = guid("script/SerpentChargePrismTurnMonitor")
RETIRED_MONITOR_PATH = "Assets/_Scripts/Controller/Arcade/TurnMonitors/SerpentChargePrismTurnMonitor.cs"
g.stale += [RETIRED_MONITOR_PATH, RETIRED_MONITOR_PATH + ".meta"]
# The donor's mode-specific wiring - minted by author_sirocco_assets.py, so the same guid function
# reproduces it; asserted to resolve like any other reference.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameSirocco.unity"
DONOR = {
    "SiroccoController":       guid("script/SiroccoController"),
    "SiroccoPrismTurnMonitor": guid("script/SiroccoPrismTurnMonitor"),
    "SiroccoSettings":         guid("asset/SiroccoSettings"),
    "SiroccoScoringRule":      guid("asset/SiroccoScoringRule"),
}
EXISTING.update(DONOR)
# The weapon the whole mode rests on - read, never edited.
SNIPER_SHOT = "Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset"
SNIPER_SCOPE = "Assets/_SO_Assets/VesselActions/Serpent/SniperScopeAction.asset"
SERPENT_PREFAB = "Assets/_Prefabs/Spacevessels/Serpent.prefab"
EXISTING["SniperShotAction"] = lib.existing_guid(SNIPER_SHOT)
EXISTING["SniperScopeAction"] = lib.existing_guid(SNIPER_SCOPE)

SERPENT_ID = lib.VESSEL_CLASS_ID["Serpent"]
BUTTERFLY_ID = lib.VESSEL_CLASS_ID["Butterfly"]

# ── The duel ─────────────────────────────────────────────────────────────────
# Snipes a domain must land. Kept in sync with EndConditionOverridesSO.DefaultSerpentChargeSnipeTarget.
# REASONED, NOT MEASURED: the rifle fires every 12 s at rest (5.4 s fully Charged); a pilot who
# connects with one round in three lands a snipe every ~25 s, so a lone pilot finishes in ~3.5 min
# and a Charge-fed pair in about two - Dustup's (10 points) neighbourhood.
SNIPE_TARGET = 8
# One snipe = one point; every other combat-hit class pays zero (the rule's own doc).
SNIPE_POINTS = 1
# Comeback - a FUNCTION OF THE TARGET (Dustup's rate on Dustup's scale). A quarter behind (2 snipes)
# buys 1.2 element levels, and the element that matters here is Charge: a trailing domain fires sooner.
COMEBACK_RATE = 0.6

SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 500\n"
         "  spawnRingRadiusFloor: 0\n  spawnFormation: 0\n")   # Rampage's own ring

# ── The marksman ladders (index = intensity - 1), and which way is HARDER ─────
# Each tuple: (settings key, values, +1 if a bigger number is harder, -1 if smaller is harder).
# The marksman no longer searches the forest for lines (the 2026-10-13 frame-time ladder is gone);
# its one physics query is a reach check that runs only when a rival is already inside the cone.
AI_LADDERS = [
    ("aiSolveSecondsByIntensity",       [1.2, 0.9, 0.6, 0.4],      -1),
    ("aiAimJitterDegreesByIntensity",   [3.0, 2.0, 1.2, 0.5],      -1),
    ("aiReactionSecondsByIntensity",    [1.0, 0.6, 0.3, 0.12],     -1),
    ("aiPilotHuntRangeByIntensity",     [1500, 2000, 2500, 3000],  +1),
    ("aiChargeDetourRangeByIntensity",  [0, 450, 700, 1000],       +1),
]
LADDER = {key: values for key, values, _ in AI_LADDERS}
CHARGE_CRYSTALS_PER_WAVE = 10
CHARGE_WAVE_SECONDS = 40
SCATTER_INNER, SCATTER_OUTER = 320, 1000

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameSerpentCharge.asset"
MODE_DIR = "Assets/_Scripts/Controller/Arcade/SerpentCharge"
DOC_REL = "Assets/_Scripts/Controller/Arcade/SERPENTCHARGE.md"

# ── 1. .cs.meta, folder, doc ─────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(MODE_DIR)
g.text_meta(DOC_REL)

# ── 2. Scoring rule: metric 8 = CombatPoints, snipes only ────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/SerpentChargeScoringRule.asset", G_ASSET["SerpentChargeScoringRule"],
             lib.header_for(G_SCRIPT["SerpentChargeScoringRuleSO"], "SerpentChargeScoringRule") +
             f"  metric: 8\n  golfRules: 1\n  snipePoints: {SNIPE_POINTS}\n  otherHitPoints: 0\n")


# ── 3. Settings ──────────────────────────────────────────────────────────────
def yaml_list(values) -> str:
    return "".join(f"  - {lib.num(v)}\n" for v in values)


settings = (lib.header_for(G_SCRIPT["SerpentChargeSettingsSO"], "SerpentChargeSettings") +
            f"  chargeCrystalsPerWave: {CHARGE_CRYSTALS_PER_WAVE}\n"
            f"  chargeCrystalWaveSeconds: {CHARGE_WAVE_SECONDS}\n"
            f"  crystalScatterInnerRadius: {SCATTER_INNER}\n"
            f"  crystalScatterOuterRadius: {SCATTER_OUTER}\n"
            f"  crystalScatterSeed: {MODE_ID}\n")
for key, values, _ in AI_LADDERS:
    settings += f"  {key}:\n" + yaml_list(values)
settings += """  aiTickSeconds: 0.1
  aiMinAimDistance: 300
  aiStandoffDistance: 700
  aiTurnPenaltyFloor: 0.35
  aiDetourMinCooldownSeconds: 2.5
  aiCloakMinCooldownSeconds: 5
  aiScopeRetrySeconds: 1
"""
g.emit_asset("Assets/_SO_Assets/Games/SerpentChargeSettings.asset", G_ASSET["SerpentChargeSettings"], settings)

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 2: a solo pilot duels the AI marksmen (the Dustup shape).
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameSerpentCharge"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameSerpentCharge") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Serpent Charge
  Description: A sniper duel between Serpents in Rampage's cactus forest. Hold the scope,
    put the reticle on a rival pilot and fire - only a hit on a pilot scores. Lay a seed to
    vanish from their sights, and use the forest as cover. Charge is your reload, so grab
    every Charge crystal. First team to the snipe target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameSerpentCharge
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Serpent']}, type: 2}}
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

# ── 5. Toasts ────────────────────────────────────────────────────────────────
# 82 (DebuffHitsLanded rose) IS a snipe here - the sniper's hit class is Debuff. The race beats
# (128-131) come from DomainRaceToasts in the controller; 137 is the mode's idle hint. The prism
# milestone (83) is gone: prisms pay nothing in a Charge game.
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_SerpentCharge.asset", G_ASSET["GameToastConfigSerpentCharge"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_SerpentCharge") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> sniped a rival pilot!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} has landed a quarter of {2} snipes", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1} of {2} snipes", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2} snipes", tint_domain=1, domain_names=0) +
             lib.toast(131, "{0} is one shot from winning - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(137, "Hold the left trigger to scope, put the reticle on a rival pilot, fire with the right",
                       idle=1, idle_seconds=25) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigSerpentCharge"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_SerpentCharge.asset", G_ASSET["ModePreviewSerpentCharge"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_SerpentCharge") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Rampage Cell Config 4 outright - referenced, not forked, exactly as the mode
    does; the forest is cover, not the score. What the preview teaches is the scope and the
    rifle: a round laid on a rival pilot. Vessel pinned to Serpent({SERPENT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {ARENA_CONFIG}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SERPENT_ID}
  ObjectiveText: Scope in and snipe the rival pilots
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 500
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewSerpentCharge"])


# ── 7. Scene: clone MinigameSirocco, swap the identity and the AI hulls ──────
CONTROLLER_BLOCK = (f"  settings: {{fileID: 11400000, guid: {G_ASSET['SerpentChargeSettings']}, type: 2}}\n"
                    f"  rule: {{fileID: 11400000, guid: {G_ASSET['SerpentChargeScoringRule']}, type: 2}}\n")
RAMPAGE_LADDER_BLOCK = ("  CellConfigs:\n" +
                        "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in RAMPAGE) +
                        "  cellTypeChoiceOptions: 1\n")
ARENA_BLOCK = (f"  CellConfigs:\n  - {{fileID: 11400000, guid: {ARENA_CONFIG}, type: 2}}\n"
               "  cellTypeChoiceOptions: 0\n")


def patch_snipe_redesign(scene: str) -> str:
    """The snipe redesign's two scene edits, IDEMPOTENT so they apply to the committed scene as
    well as to a fresh clone: the retired prism monitor's script guid becomes the snipe monitor's
    (same component, same fields - neither declares a serialized field beyond TurnMonitor's own),
    and the Rampage ladder becomes config 4."""
    scene = scene.replace(RETIRED_MONITOR, G_SCRIPT["SerpentChargeSnipeTurnMonitor"])
    if RAMPAGE_LADDER_BLOCK in scene:
        scene = lib.replace_block(scene, RAMPAGE_LADDER_BLOCK, ARENA_BLOCK, "Rampage cell ladder")
    return scene


def build_clone() -> str:
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["SiroccoController"], G_SCRIPT["SerpentChargeController"], "controller")
    scene = lib.swap_guid(scene, DONOR["SiroccoPrismTurnMonitor"], G_SCRIPT["SerpentChargeSnipeTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["SiroccoSettings"], G_ASSET["SerpentChargeSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["SiroccoScoringRule"], G_ASSET["SerpentChargeScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {BUTTERFLY_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SERPENT_ID}\n    PlayerName: AI", "AI templates", expected=4)
    assert SPAWN in scene, "the Sirocco donor no longer carries Rampage's spawn ring"
    assert RAMPAGE_LADDER_BLOCK in scene, "the Sirocco donor no longer carries Rampage's cell ladder"
    return patch_snipe_redesign(scene)


SCENE_REL = f"{lib.SCENES_DIR}/MinigameSerpentCharge.unity"
scene, scene_errors = lib.committed_scene(SCENE_REL, build_clone, authored_blocks=[CONTROLLER_BLOCK, SPAWN])
scene = patch_snipe_redesign(scene)
g.emit_scene("MinigameSerpentCharge", G_ASSET["MinigameSerpentCharge.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameSerpentCharge"])
g.register_always_unlocked()
g.register_build_scene("MinigameSirocco", "MinigameSerpentCharge", G_ASSET["MinigameSerpentCharge.unity"])
# The prism race's target is retired with it (EndConditionOverridesSO no longer declares it, and
# Unity never prunes a serialized key the class lost).
_endcond = g.read_current(lib.END_CONDITIONS)
g.emit(lib.END_CONDITIONS, re.sub(r"^  serpentChargePrismTarget(Build)?: \d+\n", "", _endcond, flags=re.M))
g.set_end_condition("serpentChargeSnipeTarget", after="siroccoPrismTarget", value=SNIPE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = list(scene_errors)

if 0.25 * SNIPE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {SNIPE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * SNIPE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The C# defaults and the authored numbers must agree (EndConditionOverridesSO's default target and
# the rule's field initializer) - a fresh asset must not play a different game.
endcond_cs = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultSerpentChargeSnipeTarget = (\d+);", endcond_cs)
if not m or int(m.group(1)) != SNIPE_TARGET:
    errors.append("EndConditionOverridesSO.DefaultSerpentChargeSnipeTarget is not SNIPE_TARGET")
if "serpentChargePrismTarget" in endcond_cs or "serpentChargePrismTarget" in g.files[lib.END_CONDITIONS]:
    errors.append("the retired serpentChargePrismTarget is still declared or serialized")
rule_cs = g.read(SCRIPT_PATHS["SerpentChargeScoringRuleSO"])
m = re.search(r"int snipePoints = (\d+);", rule_cs)
if not m or int(m.group(1)) != SNIPE_POINTS:
    errors.append("SerpentChargeScoringRuleSO.snipePoints' initializer is not SNIPE_POINTS")
# The settings SO's ladder initializers must match what is authored here.
settings_cs = g.read(SCRIPT_PATHS["SerpentChargeSettingsSO"])
for key, values, _ in AI_LADDERS:
    mm = re.search(rf"float\[\] {key} = \{{ ([^}}]*) \}};", settings_cs)
    cs_values = [float(v.strip().rstrip("f")) for v in mm.group(1).split(",")] if mm else None
    if cs_values != [float(v) for v in values]:
        errors.append(f"SerpentChargeSettingsSO.{key}'s initializer {cs_values} is not the authored {values}")

# A match must take several snipes: one lucky round never decides it.
if not (4 <= SNIPE_TARGET <= 20):
    errors.append(f"SNIPE_TARGET {SNIPE_TARGET} is outside [4, 20]")

# The weapon the mode is built on: a piercing round whose pilot strip is a Debuff-class hit on a
# same-victim window far under its cooldown - the rule prices exactly that class.
shot = g.read(SNIPER_SHOT)


def field(text, name):
    mm = re.search(rf"^  {name}: ([-0-9.]+)$", text, re.M)
    return float(mm.group(1)) if mm else None


if field(shot, "hitClass") != 2:
    errors.append("SniperShotAction's hitClass is no longer Debuff (2) - the rule's pilot price would never pay")
if field(shot, "pierceCount") != 0:
    errors.append("SniperShotAction no longer pierces everything (pierceCount != 0) - the AI's reach check assumes it does")
if (field(shot, "vesselStripPerElement") or 0) <= 0:
    errors.append("SniperShotAction no longer strips pilots (vesselStripPerElement <= 0) - snipes would never land")
cooldown = field(shot, "cooldownSeconds") or 0
floor = cooldown * (field(shot, "cooldownMultiplierAtFullCharge") or 1)
if (field(shot, "sameVictimCooldownSeconds") or 0) >= floor:
    errors.append("the sniper's same-victim window outlasts its fastest cooldown - a connecting round would not pay")

# The Serpent must still carry the scope and the rifle the marksman AI presses.
prefab = g.read(SERPENT_PREFAB)
for name in ("SniperShotAction", "SniperScopeAction"):
    if EXISTING[name] not in prefab:
        errors.append(f"Serpent.prefab no longer references {name} - the AI has nothing to press")

# The marksman ladders: four rungs each, monotone in the HARD direction.
for key, values, harder in AI_LADDERS:
    if len(values) != 4:
        errors.append(f"{key} has {len(values)} rungs, not 4")
    for a, b in zip(values, values[1:]):
        if (b - a) * harder < 0:
            errors.append(f"{key} makes a higher intensity EASIER ({values})")
# Intensity 4's aim error must sit inside the cone (1.5 deg) or the marksman cannot hit a pilot -
# and intensity 1's must sit OUTSIDE it, or the easiest bot is as accurate as the hardest.
cone = field(shot, "coneHalfAngleDegrees") or 0
jitter = LADDER["aiAimJitterDegreesByIntensity"]
if not (jitter[3] < cone < jitter[0]):
    errors.append(f"aim jitter {jitter} does not straddle the rifle's {cone} deg cone - intensity 4 must "
                  f"be inside it and intensity 1 outside it")
if max(LADDER["aiPilotHuntRangeByIntensity"]) > (field(shot, "rangeUnits") or 0):
    errors.append("a pilot-hunt range exceeds the rifle's reach")

# The marksman lowers its scope to cloak and re-raises it aiScopeRetrySeconds later, so the cloak
# window must comfortably exceed that or a bot would trade a ready rifle for a cloak; and it must
# fit inside the RESTING reload or the bot never cloaks at all.
if not (2 * 1.0 <= 5 < cooldown):
    errors.append("aiCloakMinCooldownSeconds (5) must be >= 2x aiScopeRetrySeconds and under the resting reload")
# The cloak only reaches other players if the AI's cycler presses it through the input - the
# prefab's AIPilot entry for CloakSeedWallAction must carry ReplicateThroughInput: 1.
CLOAK_ACTION = lib.existing_guid("Assets/_SO_Assets/VesselActions/Serpent/CloakSeedWallAction.asset")
if not re.search(rf"  - Ability: {{fileID: 11400000, guid: {CLOAK_ACTION}, type: 2}}\n    Duration: [^\n]*\n"
                 rf"    Cooldown: [^\n]*\n    ReplicateThroughInput: 1\n", prefab):
    errors.append("Serpent.prefab's AIPilot cloak entry is not ReplicateThroughInput - an AI cloak "
                  "would hide the hull and trail on the server only")

# The scene: our identity in, the donor's and the prism race's out, four Serpent AI templates, and
# exactly one cell config - Rampage's fourth.
if scene is not None:
    for name, gd in {"SerpentChargeController": G_SCRIPT["SerpentChargeController"],
                     "SerpentChargeSnipeTurnMonitor": G_SCRIPT["SerpentChargeSnipeTurnMonitor"],
                     "SerpentChargeSettings": G_ASSET["SerpentChargeSettings"],
                     "SerpentChargeScoringRule": G_ASSET["SerpentChargeScoringRule"]}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, "SerpentChargePrismTurnMonitor": RETIRED_MONITOR,
                     **{f"Rampage {i}": gd for i, gd in enumerate(RAMPAGE[:3], 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references {name}")
    if scene.count(f"  - vesselClass: {SERPENT_ID}\n") != 4 or re.search(r"  - vesselClass: (?!7\n)\d+\n", scene):
        errors.append("scene does not carry exactly 4 Serpent AI templates")
    if ARENA_BLOCK not in scene:
        errors.append("scene's cell is not Rampage Cell Config 4 alone")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/" + MODE_DIR), guid("text/" + DOC_REL)])
