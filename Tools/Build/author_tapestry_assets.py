#!/usr/bin/env python3
"""
Authors every serialized asset the Tapestry game mode needs (GameModes.Tapestry = 60).

Tapestry is the Butterfly's MASS game - a TIMED painting war scored on the prism volume a domain
has STANDING at the whistle (ScoringMetric.VolumeRemaining, a live stock). Mass mode paints a wake
5x-20x wide; Dust mode raids a rival's painting. See Assets/_Scripts/Controller/Arcade/TAPESTRY.md.

THE ARENA IS THE BARE BARREN CELL, referenced read-only (Switchback and Waystation run in it too):
no environment and no food web, on purpose - the Hijack reasoning, see the doc.

THE COMEBACK RATE IS SIZED AGAINST A VOLUME, and a volume is a function of the hull. So the model
below reads the Butterfly's wake off the SHIPPED assets (Butterfly.prefab's BaseScale, XScaler and
initialWavelength, its top speed, and ButterflySpreadWingsAction's resting Mass-mode width) rather
than typing a number: a vessel retune moves the comeback with it, or fails the assert.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_tapestry_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import butterfly_games_common as common    # noqa: E402

MODE_ID = 60
g = lib.Generator(MODE_ID, "Tapestry")
guid = lib.guid

SCRIPT_PATHS = {
    "TapestryController":      "Assets/_Scripts/Controller/Arcade/Tapestry/TapestryController.cs",
    "TapestrySettingsSO":      "Assets/_Scripts/Controller/Arcade/Tapestry/TapestrySettingsSO.cs",
    "TapestryScoringRuleSO":   "Assets/_Scripts/Controller/Arcade/Tapestry/TapestryScoringRuleSO.cs",
    "TapestryTimeTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/TapestryTimeTurnMonitor.cs",
    "ButterflyAutopilotModeDriver": "Assets/_Scripts/Controller/Arcade/ButterflyGames/ButterflyAutopilotModeDriver.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameTapestry":       guid("asset/ArcadeGameTapestry"),
    "TapestryScoringRule":      guid("asset/TapestryScoringRule"),
    "TapestrySettings":         guid("asset/TapestrySettings"),
    "GameToastConfigTapestry":  guid("asset/GameToastConfigTapestry"),
    "ModePreviewTapestry":      guid("asset/ModePreviewTapestry"),
    "MinigameTapestry.unity":   guid("asset/MinigameTapestry.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Butterfly"] = lib.VESSELS["Butterfly"]
EXISTING["BarrenCellConfig"] = lib.existing_guid("Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset") \
    if os.path.exists(os.path.join(lib.ROOT, "Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset.meta")) \
    else "52de45c2fab54ea0a35bc37a311d80e6"

# ── The round ────────────────────────────────────────────────────────────────
ROUND_SECONDS = 150           # kept in sync with EndConditionOverridesSO.DefaultTapestryRoundSeconds
CRYSTALS_BY_INTENSITY = [24, 16, 10, 6]
SPAWN = common.spawn_block(150, 0, 0)   # Barren HAS a nucleus: 150 outside it, symmetric

# ── The wake model: what one painting pilot adds per second, read off the shipped hull ────────
PREFAB = g.read("Assets/_Prefabs/Spacevessels/Butterfly.prefab")
WINGS = g.read("Assets/_SO_Assets/VesselActions/Butterfly/ButterflySpreadWingsAction.asset")


def one(src, pattern, what):
    m = re.search(pattern, src, re.M)
    assert m, f"{what} not found"
    return float(m.group(1))


BASE = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", PREFAB, re.M)
assert BASE, "Butterfly.prefab BaseScale not found"
base_x, base_y, base_z = (float(v) for v in BASE.groups())
x_scaler = one(PREFAB, r"^  XScaler: ([0-9.]+)$", "XScaler")
wavelength = one(PREFAB, r"^  initialWavelength: ([0-9.]+)$", "initialWavelength")
top_speed = one(PREFAB, r"^\s*DefaultMinimumSpeed:\s*([0-9.]+)$", "DefaultMinimumSpeed") \
    + one(PREFAB, r"^\s*DefaultThrottleScaler:\s*([0-9.]+)$", "DefaultThrottleScaler")
mass_width = one(WINGS, r"^    Min: ([0-9.]+)$", "massModeWidth Min")   # the width at Mass 0

KEY_VOLUME = (base_x * x_scaler / 2.0) * mass_width * base_y * base_z   # VesselPrismController.CreateBlock
KEYS_PER_SECOND = top_speed / wavelength
PAINT_RATE = KEY_VOLUME * KEYS_PER_SECOND                                # volume / second, one pilot
# A domain of two pilots who paint about HALF the round (the other half is raiding) - the
# reference a comeback is measured against. Deliberately conservative: a real lobby raids more
# and so stands less, which only makes the rate below worth MORE.
REFERENCE_DOMAIN_VOLUME = 2 * 0.5 * ROUND_SECONDS * PAINT_RATE

# Comeback - a FUNCTION OF THE SCORE'S SCALE (bonusLevels = deficit x rate). A quarter of the
# reference behind must buy at least one element level; the rate is chosen to buy 1.5.
# Rounded to the 6 decimals lib.num writes, so the assert below checks the number that SHIPS.
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameTapestry.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Tapestry")
g.folder_meta("Assets/_Scripts/Controller/Arcade/ButterflyGames")
g.text_meta("Assets/_Scripts/Controller/Arcade/TAPESTRY.md")

# ── 2. Scoring rule: metric 12 = VolumeRemaining, points mode ────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/TapestryScoringRule.asset", G_ASSET["TapestryScoringRule"],
             lib.header_for(G_SCRIPT["TapestryScoringRuleSO"], "TapestryScoringRule") +
             "  metric: 12\n  golfRules: 0\n")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/TapestrySettings.asset", G_ASSET["TapestrySettings"],
             lib.header_for(G_SCRIPT["TapestrySettingsSO"], "TapestrySettings") +
             "  elementalCrystalCountByIntensity:\n" +
             "".join(f"  - {n}\n" for n in CRYSTALS_BY_INTENSITY) + """  crystalScatterRadius: 900
  crystalScatterSeed: 60
  leadAnnounceAfterSeconds: 20
  leadAnnounceMinGapSeconds: 10
  progressSampleSeconds: 1
  aiDecisionSeconds: 6
  aiRaidDeficitFraction: 0.1
  aiLateRaidSeconds: 25
  aiDustHover: 30
  aiPaintOrbitFraction: 0.6
  aiPaintOrbitDegreesPerSecond: 6
  aiCrystalReach: 450
  aiModeRetrySeconds: 1
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2: a painting war with one side is a screensaver.
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameTapestry"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameTapestry") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Tapestry
  Description: Butterflies only, and an empty sky to fill. Mass mode paints a wake many keys
    wide - Mass crystals widen your brush. Flip to Dust mode and fly over a rival's painting
    to shred it, shrink it or steal it for your own. When the clock runs out, the team with
    the most standing wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameTapestry
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Tapestry.asset", G_ASSET["GameToastConfigTapestry"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Tapestry") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(123, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(124, "Mass mode paints a WIDE wake - everything you leave standing is score", idle=1, idle_seconds=20) +
             lib.toast(125, "Behind? Pull the right trigger for Dust mode and fly over their painting", idle=1, idle_seconds=45) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigTapestry"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Tapestry.asset", G_ASSET["ModePreviewTapestry"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Tapestry") + f"""  Mode: {MODE_ID}
  Notes: 'The bare Barren cell the mode runs in, and nothing else - the arena IS what you paint
    into it. What the preview teaches is Mass mode''s wide wake. Vessel pinned to Butterfly(13).'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['BarrenCellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {common.BUTTERFLY_ID}
  ObjectiveText: Paint the sky in Mass mode
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 150
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewTapestry"])

# ── 7. Scene ─────────────────────────────────────────────────────────────────
errors = []
scene = common.clone_or_stand_down(
    g, "MinigameTapestry", G_ASSET["MinigameTapestry.unity"], errors,
    lambda: common.clone_scene(G_SCRIPT["TapestryController"], G_SCRIPT["TapestryTimeTurnMonitor"],
                               G_ASSET["TapestrySettings"], G_ASSET["TapestryScoringRule"],
                               [EXISTING["BarrenCellConfig"]], 0, SPAWN, 0,
                               monitor_extra=f"  duration: {ROUND_SECONDS}\n"))

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameTapestry"])
g.register_always_unlocked()
g.register_build_scene("MinigameDustup" if "MinigameDustup.unity" in g.read_current(lib.BUILD_SETTINGS)
                       else "MinigameWaystation", "MinigameTapestry", G_ASSET["MinigameTapestry.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("tapestryRoundSeconds",
                    after="dustupPointTarget" if "dustupPointTarget:" in end else "undertowPointTarget",
                    value=ROUND_SECONDS)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
comeback_at_quarter = 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE
if comeback_at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {comeback_at_quarter:.2f} levels a quarter of the "
                  f"reference ({REFERENCE_DOMAIN_VOLUME:.0f}) behind (< 1)")
if len(CRYSTALS_BY_INTENSITY) != 4 or any(b > a for a, b in zip(CRYSTALS_BY_INTENSITY, CRYSTALS_BY_INTENSITY[1:])):
    errors.append("the crystal ladder must be four rungs, non-increasing (intensity is SCARCITY)")
if mass_width < 1.0:
    errors.append("Mass mode's resting width is under 1x - Mass mode would paint NARROWER than Dust mode")
barren = g.read("Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset") \
    if os.path.exists(os.path.join(lib.ROOT, "Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset")) else ""
if re.search(r"^  EnvironmentPrefab: \{fileID: [1-9]", barren, re.M):
    errors.append("Barren Cell Config grew an EnvironmentPrefab - Tapestry's arena is meant to be bare")

common.assert_scene(errors, scene,
                    must_have={"TapestryController": G_SCRIPT["TapestryController"],
                               "TapestryTimeTurnMonitor": G_SCRIPT["TapestryTimeTurnMonitor"],
                               "TapestrySettings": G_ASSET["TapestrySettings"],
                               "TapestryScoringRule": G_ASSET["TapestryScoringRule"],
                               "Barren": EXISTING["BarrenCellConfig"]},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}})
if scene is not None and f"  duration: {ROUND_SECONDS}\n" not in scene:
    errors.append("cloned scene's monitor carries no duration")

print(f"wake model: key {KEY_VOLUME:.1f} vol x {KEYS_PER_SECOND:.2f}/s = {PAINT_RATE:.0f} vol/s per painting pilot; "
      f"reference domain {REFERENCE_DOMAIN_VOLUME:.0f}; comeback {COMEBACK_RATE} "
      f"({comeback_at_quarter:.2f} levels a quarter behind)")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Tapestry"),
                   guid("folder/Assets/_Scripts/Controller/Arcade/ButterflyGames"),
                   guid("text/Assets/_Scripts/Controller/Arcade/TAPESTRY.md")])
