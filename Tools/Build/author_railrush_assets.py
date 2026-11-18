#!/usr/bin/env python3
"""
Authors every serialized asset the Railrush game mode needs (GameModes.Railrush = 124, hint toast 199: the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md reserved for row 34).

Railrush is the Urchin's CHARGE game: Hijack's Switchyard (three rings of open rails, each painted in three domain
thirds, a burr on every launch line), scored on CHAIN-SPIKE HITS on rival hulls instead of steals. A spike's velocity is
its muzzle speed PLUS the hull's (URCHIN_CHAIN_SPIKES.md, round 7), so a volley thrown off a friendly third at grind
speed (300 u/s, 360 off the open end) reaches about three times as far as one thrown at cruise: grind your colour,
launch, and lance a rival off the end of the rail. First DOMAIN to the hit target wins.

WHY NOT A JOUST. The roster first pitched this as a joust (the platform's skimmer overtake). The Urchin cannot joust:
its skimmer wears NoEffectsSkimmerImpactorDataContainer, which has no VesselExplosionBySkimmer effect, and giving it
one would change the vessel (the LOCKED rule, Shombith 2026-10-31). Its chain spikes, though, already land Bullet-class
combat hits on rival hulls in every mode (UrchinSpikeProjectileImpactContainer -> VesselCombatHitBySpike), so the
Charge game pays those. The validation below re-reads both facts every run.

WHAT IT REUSES. RailrushController INHERITS HijackController (the yard, its replicated prism ownership, the rail-riding
AI), through seams that default to Hijack's own behaviour. The arena is Hijack's four Switchyard cell configs,
referenced, never copied: intensity scales burr mass and nothing else, which in Railrush means more cover and more to
roll over, at the same rails and launch lines. The scene is Hijack's, cloned with the controller, rule and monitor
swapped and Railrush's AI ladders appended to the controller block.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_railrush_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 124
HINT_TOAST = 199
g = lib.Generator(MODE_ID, "Railrush")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "RailrushController":     f"{AR}/Railrush/RailrushController.cs",
    "RailrushScoringRuleSO":  f"{AR}/Railrush/RailrushScoringRuleSO.cs",
    "RailrushHitTurnMonitor": f"{AR}/TurnMonitors/RailrushHitTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameRailrush":      guid("asset/ArcadeGameRailrush"),
    "RailrushScoringRule":     guid("asset/RailrushScoringRule"),
    "GameToastConfigRailrush": guid("asset/GameToastConfigRailrush"),
    "ModePreviewRailrush":     guid("asset/ModePreviewRailrush"),
    "MinigameRailrush.unity":  guid("asset/MinigameRailrush.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Urchin"] = lib.VESSELS["Urchin"]
SWITCHYARD_CELLS = [lib.existing_guid(f"Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Cell Config {i}.asset")
                    for i in (1, 2, 3, 4)]
for i, gd in enumerate(SWITCHYARD_CELLS, 1):
    EXISTING[f"SwitchyardCellConfig{i}"] = gd

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameHijack.unity"
DONOR = {
    "HijackController":      lib.existing_guid(f"{AR}/HijackController.cs"),
    "HijackStealTurnMonitor": lib.existing_guid(f"{AR}/TurnMonitors/HijackStealTurnMonitor.cs"),
    "HijackScoringRule":     lib.existing_guid("Assets/_SO_Assets/Scoring Rules/HijackScoringRule.asset"),
}
URCHIN_ID = lib.VESSEL_CLASS_ID["Urchin"]

URCHIN_PREFAB = "Assets/_Prefabs/Spacevessels/Urchin.prefab"
SKIMMER_PREFAB = "Assets/_Prefabs/Spacevessels/Components/Skimmer.prefab"
NO_EFFECTS_SKIMMER = "Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/NoEffectsSkimmerImpactorDataContainer.asset"
JOUST_EFFECT = "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselExplosionBySkimmerEffect.asset"
SPIKE_PROJECTILES = [f"Assets/_Prefabs/Projectile/UrchinSpikeProjectile{t}.prefab" for t in ("", "Energized", "SuperEnergized")]
SPIKE_CONTAINER = "Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/UrchinSpikeProjectileImpactContainer.asset"
SPIKE_HIT_EFFECT = "Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitBySpike.asset"
SPIKE_ACTION = "Assets/_SO_Assets/VesselActions/Urchin/UrchinSpikeAction.asset"
COMBAT_HIT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"   # Event_CombatHitStats.asset (what StatsManager listens on)

# ── The race ─────────────────────────────────────────────────────────────────
HIT_TARGET = 20           # an ESTIMATE pending a timed match; kept in sync with DefaultRailrushHitTarget
SPIKE_HIT_POINTS = 1
BULLET_CLASS = 0          # CombatHitClass.Bullet - what VesselCombatHitBySpike reports
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (5 hits) buys 1.25
# element levels; on this hull a Charge level is a longer spike reach (chargeRangeMultiplier).
COMEBACK_RATE = 0.25

# The AI (RailrushController): index = AI difficulty - 1 (Easy, Medium, Hard, Legendary), never the intensity.
LADDERS = {
    "aiHuntRange":       ([500, 650, 800, 950], "up"),
    "aiLeadSeconds":     ([0, 0.15, 0.3, 0.45], "up"),
    "aiFireConeDegrees": ([30, 24, 18, 14], "down"),
}
SPIKE_MUZZLE_SPEED = 60   # UrchinSpikeAction.asset projectileSpeed
SPIKE_FLIGHT_SECONDS = 2  # UrchinSpikeAction.asset projectileTime
SCALARS = {"aiSpikeMuzzleSpeed": SPIKE_MUZZLE_SPEED, "aiSpikeFlightSeconds": SPIKE_FLIGHT_SECONDS,
           "aiReachFraction": 0.8, "aiEngageReachMultiple": 1.5, "aiHuntOvershoot": 120}
GRIND_SPEED, CRUISE_SPEED, LAUNCH_KICK = 300, 65, 1.2   # HIJACK.md section 2 (friendly third, cruise, end-of-ribbon)

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameRailrush.asset"

# ── 1. .cs.meta for the scripts, and the folder / doc this mode adds ────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Railrush")
g.text_meta(f"{AR}/RAILRUSH.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/RailrushScoringRule.asset", G_ASSET["RailrushScoringRule"],
             lib.header_for(G_SCRIPT["RailrushScoringRuleSO"], "RailrushScoringRule") +
             f"""  metric: 8
  golfRules: 1
  spikeHitPoints: {SPIKE_HIT_POINTS}
  otherHitPoints: 0
  hitsLeftTemplate: '{{count}} Hits Left'
  hitsLandedTemplate: '{{count}} hits'
  victoryHeader: VICTORY
  victoryLabel: RAIL TIME
  defeatHeader: DEFEAT
  defeatLabel: HITS LEFT
""")

# ── 3. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a spike never hits a team-mate, so a solo lobby has nobody to score on.
DESCRIPTION = ("Urchins only - a joust on rails. Grind a rail where it wears your colour and you fly fast; a spike "
               "flies at its own speed plus yours, so a volley thrown off the open end of a rail reaches three times "
               "as far as one thrown at cruise. Ride, launch, and lance a rival - every spike in a rival hull is a "
               "hit. Spike the road ahead to make a hostile third yours. Higher intensities grow the burrs on the "
               "launch lines into bigger cover. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameRailrush"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameRailrush") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Railrush
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameRailrush
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Urchin']}, type: 2}}
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

# ── 4. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Railrush.asset", G_ASSET["GameToastConfigRailrush"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Railrush") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(128, "{0} is a quarter of the way to {2} hits", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} hits", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(HINT_TOAST, "Grind your colour's rail, fly off the end and spike a rival - a spike thrown at "
                                   "speed flies three times as far", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigRailrush"])

# ── 5. Mode preview (Hijack's yard, so Hijack's cells and spawn ring) ────────
def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


SPAWN_RING = 1120          # the Switchyard's equatorial ring (HIJACK.md section 3, hijack_budget.prove_extent)
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Railrush.asset", G_ASSET["ModePreviewRailrush"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Railrush") + f"""  Mode: {MODE_ID}
  Notes: 'Hijack''s Switchyard, one EnvironmentPrefab per intensity (burr mass scales, the rails never
    move). OPEN-ENDED - it scores on spiking rival pilots and a solo preview has none; what it
    teaches is the grind, the launch and the reach a spike carries off it. Vessel pinned to
    Urchin({URCHIN_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {SWITCHYARD_CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(SWITCHYARD_CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  CourseStructurePrefab: {{fileID: 0}}
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {URCHIN_ID}
  ObjectiveText: Grind a rail, launch and spike a rival
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnFormation: 1
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewRailrush"])

# ── 6. Scene: Hijack's, with the controller, the rule and the monitor swapped, the AI ladders appended ──
errors = []
DONOR_TAIL = "  aiSlippedRailCooldown: 10\n"


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


RAILRUSH_FIELDS = "".join(ladder_yaml(n) for n in LADDERS) + "".join(
    f"  {k}: {lib.num(float(v))}\n" for k, v in SCALARS.items())


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["HijackStealTurnMonitor"], G_SCRIPT["RailrushHitTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["HijackController"], G_SCRIPT["RailrushController"], "controller")
    scene = lib.swap_guid(scene, DONOR["HijackScoringRule"], G_ASSET["RailrushScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, DONOR_TAIL, DONOR_TAIL + RAILRUSH_FIELDS, "controller AI fields")
    return scene


AUTHORED_BLOCKS = [DONOR_TAIL + RAILRUSH_FIELDS, "  CellConfigs:\n" + cell_list(SWITCHYARD_CELLS)]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameRailrush.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameRailrush", G_ASSET["MinigameRailrush.unity"], scene)

# ── 7. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameRailrush"])
g.register_always_unlocked()
g.register_build_scene("MinigameHijack", "MinigameRailrush", G_ASSET["MinigameRailrush.unity"])
g.set_end_condition("railrushHitTarget", after="coronaBloomTarget", value=HIT_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * HIT_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {HIT_TARGET}: a quarter-of-target deficit "
                  f"buys {0.25 * HIT_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing facts about the Urchin, re-read every run.
# (a) It cannot joust: its skimmer's container has no joust effect. If that ever changes, this game could pay jousts.
urchin = g.read(URCHIN_PREFAB)
skimmer = g.read(SKIMMER_PREFAB)
no_fx = lib.existing_guid(NO_EFFECTS_SKIMMER)
joust_fx = lib.existing_guid(JOUST_EFFECT)
if "skimmerImpactorDataContainer" in urchin or f"guid: {no_fx}" not in skimmer:
    errors.append("the Urchin's skimmer no longer wears NoEffectsSkimmerImpactorDataContainer - re-check whether it "
                  "jousts now (RAILRUSH.md 'Why not a joust')")
if joust_fx in g.read(NO_EFFECTS_SKIMMER):
    errors.append("NoEffectsSkimmerImpactorDataContainer now holds the joust effect - re-read RAILRUSH.md")
# (b) Its spikes land Bullet-class combat hits on rival hulls, on the channel StatsManager listens on.
container = lib.existing_guid(SPIKE_CONTAINER)
for p in SPIKE_PROJECTILES:
    if f"projectileImpactorDataContainer: {{fileID: 11400000, guid: {container}" not in g.read(p):
        errors.append(f"{os.path.basename(p)} no longer uses UrchinSpikeProjectileImpactContainer - spike hits may "
                      f"no longer score")
if f"guid: {lib.existing_guid(SPIKE_HIT_EFFECT)}" not in g.read(SPIKE_CONTAINER).split("projectilePrismEffects:")[0]:
    errors.append("UrchinSpikeProjectileImpactContainer no longer lists VesselCombatHitBySpike as a ship effect")
hit_fx = g.read(SPIKE_HIT_EFFECT)
if f"hitClass: {BULLET_CLASS}\n" not in hit_fx or f"onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_CHANNEL}" not in hit_fx:
    errors.append("VesselCombatHitBySpike is no longer a Bullet-class hit on Event_CombatHitStats - RailrushScoringRuleSO "
                  "pays Bullet hits")
if f"Bullet = {BULLET_CLASS}," not in g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs"):
    errors.append(f"CombatHitClass.Bullet is no longer {BULLET_CLASS}")
rule_cs = g.read(SCRIPT_PATHS["RailrushScoringRuleSO"])
if "hitClass == CombatHitClass.Bullet ? spikeHitPoints : otherHitPoints" not in rule_cs:
    errors.append("RailrushScoringRuleSO no longer pays Bullet hits as spike hits")
# (c) The AI's reach model reads the spike's own numbers.
spike = g.read(SPIKE_ACTION)
for field, value in (("projectileSpeed", SPIKE_MUZZLE_SPEED), ("projectileTime", SPIKE_FLIGHT_SECONDS)):
    if not re.search(rf"^  {field}: {value}\n", spike, re.M):
        errors.append(f"UrchinSpikeAction.{field} is no longer {value} - re-tune the AI's reach (SCALARS)")
# The lance: off the rail's end a spike must reach clearly further than one thrown at cruise.
reach_cruise = (CRUISE_SPEED + SPIKE_MUZZLE_SPEED) * SPIKE_FLIGHT_SECONDS
reach_launch = (GRIND_SPEED * LAUNCH_KICK + SPIKE_MUZZLE_SPEED) * SPIKE_FLIGHT_SECONDS
if reach_launch < 2.5 * reach_cruise:
    errors.append(f"a launched spike reaches {reach_launch} u against {reach_cruise} u at cruise - the rail is no "
                  f"longer the lance this mode is built on")

# (d) The mode does not change the vessel: nothing under the Urchin's files is in this generator's output.
for rel in g.files:
    if "Urchin" in os.path.basename(rel) or "/R_VesselActions/" in rel or "/Spacevessels/" in rel:
        errors.append(f"{rel}: a mode never edits a vessel file (the LOCKED vessel rule)")

# The C# default target matches the authored one, and the controller's initializers match the ladders.
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultRailrushHitTarget = {HIT_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultRailrushHitTarget differs from HIT_TARGET")
ctl = g.read(SCRIPT_PATHS["RailrushController"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in ctl:
        errors.append(f"{name}: the C# initializer does not match {init}")
for name, value in SCALARS.items():
    if f"{name} = {lib.num(float(value))}f;" not in ctl:
        errors.append(f"RailrushController.{name} initializer differs from the authored {value}")
if "SelectedIntensity" in ctl or "AIDifficultyFor(" not in ctl:
    errors.append("RailrushController must key its AI ladders on gameData.AIDifficultyFor(seat), not the intensity")
if "class RailrushController : HijackController" not in ctl:
    errors.append("RailrushController no longer inherits HijackController - the yard's ownership sync would be lost")
hijack_cs = g.read(f"{AR}/HijackController.cs")
for seam in ("protected virtual void ZeroScoredStats(IRoundStats stats) => stats.PrismStolen = 0;",
             "protected virtual int ReadScoredStat(IRoundStats stats) => stats.PrismStolen;",
             "protected virtual bool TryOverrideAITarget(", "protected virtual int ChooseRail(",
             "protected void SpikeVolley(", "protected virtual void Update()"):
    if seam not in hijack_cs:
        errors.append(f"HijackController no longer has the seam '{seam}' Railrush overrides")
if "GameToastSituation.RailrushHint" not in ctl:
    errors.append("RailrushController never posts its hint toast")
if f"RailrushHint = {HINT_TOAST}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.RailrushHint is not {HINT_TOAST}")
if f"Railrush = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Railrush is not {MODE_ID}")

# The scene: every new identity present, every donor one gone, four Urchin AIs, the Switchyard cells.
if scene is not None:
    for name, gd in {**G_SCRIPT, "RailrushScoringRule": G_ASSET["RailrushScoringRule"]}.items():
        if name == "RailrushScoringRuleSO":
            continue
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {URCHIN_ID}\n") != 4:
        errors.append("scene does not carry exactly 4 Urchin AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Railrush"), guid(f"text/{AR}/RAILRUSH.md")])
