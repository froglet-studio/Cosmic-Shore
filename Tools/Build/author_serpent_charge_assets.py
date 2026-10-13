#!/usr/bin/env python3
"""
Authors every serialized asset the Serpent Charge game mode needs (GameModes.SerpentCharge = 66).

Serpent Charge is the Serpent's CHARGE game - a sniper race through Rampage's cactus forest. On the
Serpent, Charge is the Sniper Shot's RECOVERY (12 s at rest -> 5.4 s at Charge 10); the round is a
1.5 deg, 3,000 u hitscan cone that destroys everything hostile it contains and strips any rival
pilot inside its reach. First DOMAIN to the target wins on ScoringMetric.PrismsDestroyed with every
sniped pilot folded in at PILOT_SNIPE_POINTS (SerpentChargeScoringRuleSO). The arena is seeded
with waves of CHARGE crystals, and the AI is a marksman (SerpentMarksmanAI) whose difficulty is a
per-intensity ladder. See Assets/_Scripts/Controller/Arcade/SERPENTCHARGE.md.

THE ARENA IS RAMPAGE'S, referenced read-only - the four cactus-forest configs The Bends, Bloomrush
and Sirocco reference. THE SCENE is a clone of MinigameSirocco.unity (Rampage's forest, Rampage's
spawn ring, a controller block of settings / rule / arenaCell - the field names this controller
declares), with the identity swapped and the four AI hulls Butterfly(13) -> Serpent(7). Once the
scene is committed the clone STANDS DOWN (arcade_mode_lib.committed_scene).

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_serpent_charge_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import rampage_intensity as rampage        # noqa: E402

MODE_ID = 66
g = lib.Generator(MODE_ID, "SerpentCharge")
guid = lib.guid

SCRIPT_PATHS = {
    "SerpentChargeController":       "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentChargeController.cs",
    "SerpentChargeSettingsSO":       "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentChargeSettingsSO.cs",
    "SerpentChargeScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentChargeScoringRuleSO.cs",
    "SerpentMarksmanAI":             "Assets/_Scripts/Controller/Arcade/SerpentCharge/SerpentMarksmanAI.cs",
    "SerpentChargePrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/SerpentChargePrismTurnMonitor.cs",
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

# ── The race ─────────────────────────────────────────────────────────────────
# Hostile prisms a domain must destroy (sniped pilots folded in). Kept in sync with
# EndConditionOverridesSO.DefaultSerpentChargePrismTarget. REASONED, NOT MEASURED: a good line down
# a stand takes ~60 prisms (a plant averages ~178), the rifle fires every 12 s at rest, so a solo
# pilot finishes in ~2.5 min and a Charge-fed pair in well under two.
PRISM_TARGET = 800
# One sniped rival pilot, in prisms - about one good line through a stand (the rule's own doc).
PILOT_SNIPE_POINTS = 60
# Comeback - a FUNCTION OF THE TARGET. A quarter behind (200) buys 1.6 element levels, and the
# element that matters here is Charge: a trailing domain fires sooner.
COMEBACK_RATE = 0.008

SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 500\n"
         "  spawnRingRadiusFloor: 0\n  spawnFormation: 0\n")   # Rampage's own ring

# ── The marksman ladders (index = intensity - 1), and which way is HARDER ─────
# Each tuple: (settings key, values, +1 if a bigger number is harder, -1 if smaller is harder).
AI_LADDERS = [
    ("aiSolveSecondsByIntensity",       [1.6, 1.1, 0.75, 0.5],     -1),
    ("aiCandidateLinesByIntensity",     [6, 10, 16, 24],           +1),
    ("aiAimJitterDegreesByIntensity",   [2.5, 1.5, 0.75, 0.25],    -1),
    ("aiReactionSecondsByIntensity",    [1.0, 0.6, 0.3, 0.12],     -1),
    ("aiFireFractionByIntensity",       [0.5, 0.6, 0.7, 0.8],      +1),
    ("aiPatienceSecondsByIntensity",    [2.5, 3.0, 3.5, 4.0],      +1),
    ("aiPilotHuntRangeByIntensity",     [0, 1200, 2000, 2800],     +1),
    ("aiChargeDetourRangeByIntensity",  [0, 450, 700, 1000],       +1),
]
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

# ── 2. Scoring rule: metric 5 = PrismsDestroyed, pilots folded in ────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/SerpentChargeScoringRule.asset", G_ASSET["SerpentChargeScoringRule"],
             lib.header_for(G_SCRIPT["SerpentChargeScoringRuleSO"], "SerpentChargeScoringRule") +
             f"  metric: 5\n  golfRules: 1\n  pilotSnipePoints: {PILOT_SNIPE_POINTS}\n  otherHitPoints: 0\n")


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
  aiMinShotValue: 8
  aiMinAimDistance: 250
  aiStandSampleRadius: 260
  aiAheadSampleDistance: 900
  aiAheadSampleRadius: 450
  aiTurnPenaltyFloor: 0.35
  aiDetourMinCooldownSeconds: 2.5
  aiCloakMinCooldownSeconds: 5
  aiScopeRetrySeconds: 1
"""
g.emit_asset("Assets/_SO_Assets/Games/SerpentChargeSettings.asset", G_ASSET["SerpentChargeSettings"], settings)

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 2: a race against the ARENA - a solo pilot has the whole forest and an
# AI marksman to beat (the Rampage / Sirocco shape).
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameSerpentCharge"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameSerpentCharge") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Serpent Charge
  Description: Serpents only, over Rampage's cactus forest. Hold the scope, lay the reticle
    down the length of a stand and fire - one round tears through everything hostile in its
    line, and a rival pilot caught in it is worth a whole stand. Charge is your reload, so
    grab every Charge crystal. The AI marksmen hunt you back. First team to the target wins.
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
# 82 (DebuffHitsLanded rose) IS a pilot snipe here - the sniper's hit class is Debuff. The race
# beats (128-131) come from DomainRaceToasts in the controller; 137 is the mode's idle hint.
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_SerpentCharge.asset", G_ASSET["GameToastConfigSerpentCharge"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_SerpentCharge") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(82, "<b>{0}</b> sniped a rival pilot!", tint_domain=1, domain_names=0) +
             lib.toast(83, "<b>{0}</b> has shot down {1} prisms", tint_domain=1, domain_names=0, every_n=200) +
             lib.toast(128, "{0} is a quarter of the way to {2}", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway to {2} - {1} down", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(131, "{0} is closing in - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(137, "Hold the left trigger to scope, line the reticle down a whole stand, fire with the right",
                       idle=1, idle_seconds=25) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigSerpentCharge"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
PREVIEW_CELLS = "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in RAMPAGE)
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_SerpentCharge.asset", G_ASSET["ModePreviewSerpentCharge"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_SerpentCharge") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Rampage''s cactus forest outright - referenced, not forked, exactly as the mode
    does; intensity is the forest''s density. What the preview teaches is the scope and the
    rifle: a round down the length of a stand. Vessel pinned to Serpent({SERPENT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {RAMPAGE[0]}, type: 2}}
  PreviewCellsByIntensity:
{PREVIEW_CELLS}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SERPENT_ID}
  ObjectiveText: Scope in and snipe down the length of a stand
  ObjectiveMetric: 5
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


def build_clone() -> str:
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["SiroccoController"], G_SCRIPT["SerpentChargeController"], "controller")
    scene = lib.swap_guid(scene, DONOR["SiroccoPrismTurnMonitor"], G_SCRIPT["SerpentChargePrismTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["SiroccoSettings"], G_ASSET["SerpentChargeSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["SiroccoScoringRule"], G_ASSET["SerpentChargeScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {BUTTERFLY_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SERPENT_ID}\n    PlayerName: AI", "AI templates", expected=4)
    assert SPAWN in scene, "the Sirocco donor no longer carries Rampage's spawn ring"
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameSerpentCharge.unity"
scene, scene_errors = lib.committed_scene(SCENE_REL, build_clone, authored_blocks=[CONTROLLER_BLOCK, SPAWN])
g.emit_scene("MinigameSerpentCharge", G_ASSET["MinigameSerpentCharge.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameSerpentCharge"])
g.register_always_unlocked()
g.register_build_scene("MinigameSirocco", "MinigameSerpentCharge", G_ASSET["MinigameSerpentCharge.unity"])
g.set_end_condition("serpentChargePrismTarget", after="siroccoPrismTarget", value=PRISM_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
errors = list(scene_errors)

if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The C# defaults and the authored numbers must agree (EndConditionOverridesSO's default target and
# the rule's field initializer) - a fresh asset must not play a different game.
endcond_cs = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultSerpentChargePrismTarget = (\d+);", endcond_cs)
if not m or int(m.group(1)) != PRISM_TARGET:
    errors.append("EndConditionOverridesSO.DefaultSerpentChargePrismTarget is not PRISM_TARGET")
rule_cs = g.read(SCRIPT_PATHS["SerpentChargeScoringRuleSO"])
m = re.search(r"int pilotSnipePoints = (\d+);", rule_cs)
if not m or int(m.group(1)) != PILOT_SNIPE_POINTS:
    errors.append("SerpentChargeScoringRuleSO.pilotSnipePoints' initializer is not PILOT_SNIPE_POINTS")

# A pilot must be worth more than a thin line and never a match on its own.
if not (10 <= PILOT_SNIPE_POINTS <= PRISM_TARGET // 8):
    errors.append(f"PILOT_SNIPE_POINTS {PILOT_SNIPE_POINTS} is outside [10, target/8]: either a pilot is "
                  f"cheaper than a stray prism line or a handful of snipes decides the match")

# The forest must be big enough that a match ENDS with forest standing: the target sits well under
# a quarter of the sparsest rung's forest (a domain's hostile share is about two thirds of it).
smallest_forest = min(rampage.forest(i)[2] for i in range(1, 5))
if PRISM_TARGET > 0.25 * smallest_forest:
    errors.append(f"the target would strip most of the sparsest forest ({smallest_forest} prisms) - lower it")

# The weapon the mode is built on: a piercing round whose pilot strip is a Debuff-class hit on a
# same-victim window far under its cooldown - the rule prices exactly that class.
shot = g.read(SNIPER_SHOT)


def field(text, name):
    mm = re.search(rf"^  {name}: ([-0-9.]+)$", text, re.M)
    return float(mm.group(1)) if mm else None


if field(shot, "hitClass") != 2:
    errors.append("SniperShotAction's hitClass is no longer Debuff (2) - the rule's pilot price would never pay")
if field(shot, "pierceCount") != 0:
    errors.append("SniperShotAction no longer pierces everything (pierceCount != 0) - the target assumes it does")
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
# Intensity 4's aim error must sit inside the cone (1.5 deg) or the marksman cannot hit a pilot.
cone = field(shot, "coneHalfAngleDegrees") or 0
if AI_LADDERS[2][1][3] >= cone:
    errors.append("intensity-4 aim jitter is not inside the rifle's cone - the hardest bot could not snipe")
if max(AI_LADDERS[6][1]) > (field(shot, "rangeUnits") or 0):
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

# The scene: our identity in, the donor's out, four Serpent AI templates, Rampage's four cells.
if scene is not None:
    for name, gd in {"SerpentChargeController": G_SCRIPT["SerpentChargeController"],
                     "SerpentChargePrismTurnMonitor": G_SCRIPT["SerpentChargePrismTurnMonitor"],
                     "SerpentChargeSettings": G_ASSET["SerpentChargeSettings"],
                     "SerpentChargeScoringRule": G_ASSET["SerpentChargeScoringRule"],
                     **{f"Rampage {i}": gd for i, gd in enumerate(RAMPAGE, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references {name}")
    if scene.count(f"  - vesselClass: {SERPENT_ID}\n") != 4 or re.search(r"  - vesselClass: (?!7\n)\d+\n", scene):
        errors.append("scene does not carry exactly 4 Serpent AI templates")
    if "  cellTypeChoiceOptions: 1\n" not in scene:
        errors.append("scene is not IntensityWise over the Rampage ladder")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/" + MODE_DIR), guid("text/" + DOC_REL)])
