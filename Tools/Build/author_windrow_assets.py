#!/usr/bin/env python3
"""
Authors every serialized asset the Windrow game mode needs (GameModes.Windrow = 122, hint toast 197: the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 32 reserved, never "the next free one").

Windrow is the Rhino's MASS game - a TIMED war scored on the trail volume a domain has STANDING at the whistle
(ScoringMetric.VolumeRemaining, a live stock; Tapestry's shape). The only force that takes mass away is the plough: a
prism the Rhino throws that runs into another destroys both. MASS is the Heft (every throw 0 -> 60 u further, the
triggers steer it) and its level-5 Berm lands ploughed prisms shielded. See Assets/_Scripts/Controller/Arcade/WINDROW.md.

REUSED, NOT FORKED: the arena is the bare Barren cell (Tapestry's, referenced), the rule is a TapestryScoringRuleSO
asset and the settings a TapestrySettingsSO asset. The controller is WindrowController, Tapestry's copy without the
Butterfly's mode driver (an AI Rhino only steers). The scene is cloned from Tapestry's committed scene.

THE COMEBACK RATE IS SIZED AGAINST A VOLUME, read off the shipped Rhino (Tapestry's method): one trail block's volume
from Rhino.prefab's BaseScale and XScaler, its wavelength and its cruise speed. A vessel retune moves the comeback
with it, or fails the assert.

NOTHING ABOUT THE RHINO CHANGES (the LOCKED vessel rule, Shombith 2026-10-31).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_windrow_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 122          # GAME_KIT_ROSTER.md row 32
HINT_TOAST = 197       # GAME_KIT_ROSTER.md row 32
g = lib.Generator(MODE_ID, "Windrow")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "WindrowController":      f"{AR}/Windrow/WindrowController.cs",
    "WindrowTimeTurnMonitor": f"{AR}/TurnMonitors/WindrowTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameWindrow":      guid("asset/ArcadeGameWindrow"),
    "WindrowScoringRule":     guid("asset/WindrowScoringRule"),
    "WindrowSettings":        guid("asset/WindrowSettings"),
    "GameToastConfigWindrow": guid("asset/GameToastConfigWindrow"),
    "ModePreviewWindrow":     guid("asset/ModePreviewWindrow"),
    "MinigameWindrow.unity":  guid("asset/MinigameWindrow.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Rhino"] = lib.VESSELS["Rhino"]
EXISTING["TapestryScoringRuleSO"] = lib.existing_guid(f"{AR}/Tapestry/TapestryScoringRuleSO.cs")
EXISTING["TapestrySettingsSO"] = lib.existing_guid(f"{AR}/Tapestry/TapestrySettingsSO.cs")
BARREN = "Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset"
EXISTING["BarrenCellConfig"] = lib.existing_guid(BARREN)

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameTapestry.unity"
DONOR = {
    "TapestryController":      lib.existing_guid(f"{AR}/Tapestry/TapestryController.cs"),
    "TapestryTimeTurnMonitor": lib.existing_guid(f"{AR}/TurnMonitors/TapestryTimeTurnMonitor.cs"),
    "TapestrySettings":        lib.existing_guid("Assets/_SO_Assets/Games/TapestrySettings.asset"),
    "TapestryScoringRule":     lib.existing_guid("Assets/_SO_Assets/Scoring Rules/TapestryScoringRule.asset"),
}
BUTTERFLY_ID = lib.VESSEL_CLASS_ID["TigerMoth"]
RHINO_ID = lib.VESSEL_CLASS_ID["Rhino"]
PLOUGH_CS = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/RhinoPloughPrismEffectSO.cs"
PLOUGH = "Assets/_SO_Assets/Effects/Vessel Prism Effects/RhinoPloughPrismEffect.asset"

# ── The round ────────────────────────────────────────────────────────────────
ROUND_SECONDS = 90            # kept in sync with EndConditionOverridesSO.DefaultWindrowRoundSeconds
CRYSTALS_BY_INTENSITY = [24, 16, 10, 6]   # intensity is SCARCITY of Mass levels (Heft, then Berm)
SPAWN_OUTSIDE_NUCLEUS = 150   # the donor scene's: Barren HAS a nucleus

# ── The trail model: what one Rhino adds per second, read off the shipped hull ─────────
PREFAB = g.read("Assets/_Prefabs/Spacevessels/Rhino.prefab")


def one(src, pattern, what):
    m = re.search(pattern, src, re.M)
    assert m, f"{what} not found"
    return float(m.group(1))


BASE = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", PREFAB, re.M)
assert BASE, "Rhino.prefab BaseScale not found"
base_x, base_y, base_z = (float(v) for v in BASE.groups())
x_scaler = one(PREFAB, r"^  XScaler: ([0-9.]+)$", "XScaler")
wavelength = one(PREFAB, r"^  initialWavelength: ([0-9.]+)$", "initialWavelength")
cruise = one(PREFAB, r"^\s*DefaultMinimumSpeed:\s*([0-9.]+)$", "DefaultMinimumSpeed") \
    + one(PREFAB, r"^\s*DefaultThrottleScaler:\s*([0-9.]+)$", "DefaultThrottleScaler")

BLOCK_VOLUME = (base_x * x_scaler / 2.0) * base_y * base_z     # Tapestry's VesselPrismController.CreateBlock model
BLOCKS_PER_SECOND = cruise / wavelength                          # at CRUISE: a ramping Rhino lays faster
LAY_RATE = BLOCK_VOLUME * BLOCKS_PER_SECOND
# A domain of two pilots who lay about HALF the round at cruise (the other half raiding) - the reference a comeback is
# measured against. Conservative (a ramping Rhino lays more), which only makes the rate below worth MORE.
REFERENCE_DOMAIN_VOLUME = 2 * 0.5 * ROUND_SECONDS * LAY_RATE
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameWindrow.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Windrow")
g.text_meta(f"{AR}/WINDROW.md")

# ── 2. Scoring rule: a TapestryScoringRuleSO, metric 12 = VolumeRemaining, points mode ──
g.emit_asset("Assets/_SO_Assets/Scoring Rules/WindrowScoringRule.asset", G_ASSET["WindrowScoringRule"],
             lib.header_for(EXISTING["TapestryScoringRuleSO"], "WindrowScoringRule") +
             "  metric: 12\n  golfRules: 0\n")

# ── 3. Settings: a TapestrySettingsSO (aiDustHover and aiModeRetrySeconds are the Butterfly's, unread here) ──
g.emit_asset("Assets/_SO_Assets/Games/WindrowSettings.asset", G_ASSET["WindrowSettings"],
             lib.header_for(EXISTING["TapestrySettingsSO"], "WindrowSettings") +
             "  elementalCrystalCountByIntensity:\n" +
             "".join(f"  - {n}\n" for n in CRYSTALS_BY_INTENSITY) + """  crystalScatterRadius: 900
  crystalScatterSeed: 122
  leadAnnounceAfterSeconds: 20
  leadAnnounceMinGapSeconds: 10
  progressSampleSeconds: 1
  aiDecisionSeconds: 6
  aiRaidDeficitFraction: 0.1
  aiLateRaidSeconds: 25
  aiDustHover: 0
  aiPaintOrbitFraction: 0.6
  aiPaintOrbitDegreesPerSecond: 4
  aiCrystalReach: 450
  aiModeRetrySeconds: 1
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2: a war over standing mass with one side has nobody to raid.
DESCRIPTION = ("Rhinos only, and an empty cell to fill. Every row of trail you lay that is still standing when the "
               "clock runs out is your score. Your plough breaks nothing it hits, but a prism it throws into another "
               "destroys both - so fly along a rival's row and throw it into itself. Mass throws further, and the "
               "triggers steer the throw: hold one and everything goes that way. Mass crystals are scarcer at higher "
               "intensities; at Mass 5 every prism you plough lands shielded, so you can bank your own rows into "
               "armour. When the clock runs out, the team with the most standing wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameWindrow"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameWindrow") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Windrow
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameWindrow
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Rhino']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Windrow.asset", G_ASSET["GameToastConfigWindrow"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Windrow") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(130, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(HINT_TOAST, "Your standing trail is your score - plough through a rival row to break it",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigWindrow"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Windrow.asset", G_ASSET["ModePreviewWindrow"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Windrow") + f"""  Mode: {MODE_ID}
  Notes: 'The bare Barren cell the mode runs in, and nothing else - the arena IS the rows the
    Rhinos lay into it. What the preview teaches is the trail you leave standing. Vessel pinned
    to Rhino({RHINO_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['BarrenCellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {RHINO_ID}
  ObjectiveText: Lay rows of trail and keep them standing
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: {SPAWN_OUTSIDE_NUCLEUS}
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewWindrow"])

# ── 7. Scene: Tapestry's, with the identity and the AI hulls swapped ─────────
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["TapestryTimeTurnMonitor"], G_SCRIPT["WindrowTimeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["TapestryController"], G_SCRIPT["WindrowController"], "controller")
    scene = lib.swap_guid(scene, DONOR["TapestrySettings"], G_ASSET["WindrowSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["TapestryScoringRule"], G_ASSET["WindrowScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {BUTTERFLY_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {RHINO_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, "  duration: 90\n", f"  duration: {ROUND_SECONDS}\n", "round length")
    return scene


AUTHORED_BLOCKS = [f"  duration: {ROUND_SECONDS}\n"]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameWindrow.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameWindrow", G_ASSET["MinigameWindrow.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameWindrow"])
g.register_always_unlocked()
g.register_build_scene("MinigameTapestry", "MinigameWindrow", G_ASSET["MinigameWindrow.unity"])
g.set_end_condition("windrowRoundSeconds", after="bullrushGoreTarget"
                    if "bullrushGoreTarget:" in g.read_current(lib.END_CONDITIONS) else "jackstrawStrikeTarget",
                    value=ROUND_SECONDS)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
comeback_at_quarter = 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE
if comeback_at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {comeback_at_quarter:.2f} levels a quarter of the "
                  f"reference ({REFERENCE_DOMAIN_VOLUME:.0f}) behind (< 1)")
if len(CRYSTALS_BY_INTENSITY) != 4 or any(b > a for a, b in zip(CRYSTALS_BY_INTENSITY, CRYSTALS_BY_INTENSITY[1:])):
    errors.append("the crystal ladder must be four rungs, non-increasing (intensity is SCARCITY)")
if "spawnerEnabled: 1" not in PREFAB:
    errors.append("Rhino.prefab no longer lays trail (spawnerEnabled) - Windrow scores the trail a Rhino lays")
barren = g.read(BARREN)
if re.search(r"^  EnvironmentPrefab: \{fileID: [1-9]", barren, re.M):
    errors.append("Barren Cell Config grew an EnvironmentPrefab - Windrow's arena is meant to be bare")
plough_cs = g.read(PLOUGH_CS)
for needle in ("PrismMotion.Annihilate", "bermUpgradeElement", "furrowDistance"):
    if needle not in plough_cs:
        errors.append(f"RhinoPloughPrismEffectSO no longer has '{needle}' - Windrow is built on the plough's strikes, "
                      f"the Heft and the Berm")
plough = g.read(PLOUGH)
if not re.search(r"^  bermUpgradeElement: 2$", plough, re.M):
    errors.append("RhinoPloughPrismEffect.bermUpgradeElement is no longer Mass (2) - the card promises Berm at Mass 5")

if f"Windrow = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Windrow is not {MODE_ID} (the roster's reserved id)")
if f"WindrowHint = {HINT_TOAST}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.WindrowHint is not {HINT_TOAST} (the roster's reserved id)")
if f"DefaultWindrowRoundSeconds = {ROUND_SECONDS};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultWindrowRoundSeconds differs from ROUND_SECONDS")
controller = g.read(SCRIPT_PATHS["WindrowController"])
for bad in ("PerformShipControllerActions", "InputStatus", "ButterflyAutopilotModeDriver", "_modes"):
    if bad in controller:
        errors.append(f"WindrowController uses {bad} - an AI Rhino only steers")
settings_cs = g.read(f"{AR}/Tapestry/TapestrySettingsSO.cs")
for name in ("crystalScatterRadius", "crystalScatterSeed", "aiDecisionSeconds", "aiRaidDeficitFraction",
             "aiLateRaidSeconds", "aiPaintOrbitFraction", "aiPaintOrbitDegreesPerSecond", "aiCrystalReach",
             "leadAnnounceAfterSeconds", "leadAnnounceMinGapSeconds", "progressSampleSeconds"):
    if f" {name}" not in settings_cs or f"settings.{name}" not in controller:
        errors.append(f"'{name}' is not both a TapestrySettingsSO field and read by WindrowController")

if scene is not None:
    for name, gd in {**G_SCRIPT, "WindrowSettings": G_ASSET["WindrowSettings"],
                     "WindrowScoringRule": G_ASSET["WindrowScoringRule"],
                     "Barren": EXISTING["BarrenCellConfig"]}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {RHINO_ID}\n") != 4 or f"  - vesselClass: {BUTTERFLY_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Rhino AI templates")

print(f"trail model: block {BLOCK_VOLUME:.2f} vol x {BLOCKS_PER_SECOND:.1f}/s = {LAY_RATE:.1f} vol/s per Rhino at "
      f"cruise; reference domain {REFERENCE_DOMAIN_VOLUME:.0f}; comeback {COMEBACK_RATE} "
      f"({comeback_at_quarter:.2f} levels a quarter behind)")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Windrow"), guid(f"text/{AR}/WINDROW.md")])
