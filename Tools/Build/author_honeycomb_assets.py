#!/usr/bin/env python3
"""
Authors every serialized asset the Honeycomb game mode needs (GameModes.Honeycomb = 109, hint toast 184 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 19 reserved on 2026-11-15).

Honeycomb is the Honeyguide's MASS game - a TIMED building war scored on the prism volume a domain has STANDING in its
own colour at the whistle (ScoringMetric.VolumeRemaining, a live stock). An omni crystal is a HIVE: a comb of real,
super-shielded prisms behind the hull (your colour's cells are yours) while the Ratel plays the hive puzzle; a human's
hull also plants a Mass hive every 45 s. See Assets/_Scripts/Controller/Arcade/HONEYCOMB.md.

THE ARENA IS THE BARE BARREN CELL, referenced read-only (Tapestry's): no environment and no food web, on purpose.

THE COMEBACK RATE IS SIZED AGAINST A VOLUME, and a volume is a function of the hull. So the model below reads the
Honeyguide's trail off the SHIPPED prefab (BaseScale, XScaler, initialWavelength and its top speed) rather than typing
a number - Tapestry's wake model with the Honeyguide's numbers - so a vessel retune moves the comeback with it.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_honeycomb_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import honeyguide_games_common as common   # noqa: E402

MODE_ID = 109          # GAME_KIT_ROSTER.md row 19 - never "the next free id"
TOAST_HINT = 184
g = lib.Generator(MODE_ID, "Honeycomb")
guid = lib.guid

SCRIPT_PATHS = {
    "HoneycombController":      "Assets/_Scripts/Controller/Arcade/Honeycomb/HoneycombController.cs",
    "HoneycombSettingsSO":      "Assets/_Scripts/Controller/Arcade/Honeycomb/HoneycombSettingsSO.cs",
    "HoneycombScoringRuleSO":   "Assets/_Scripts/Controller/Arcade/Honeycomb/HoneycombScoringRuleSO.cs",
    "HoneycombTimeTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/HoneycombTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameHoneycomb":      guid("asset/ArcadeGameHoneycomb"),
    "HoneycombScoringRule":     guid("asset/HoneycombScoringRule"),
    "HoneycombSettings":        guid("asset/HoneycombSettings"),
    "GameToastConfigHoneycomb": guid("asset/GameToastConfigHoneycomb"),
    "ModePreviewHoneycomb":     guid("asset/ModePreviewHoneycomb"),
    "MinigameHoneycomb.unity":  guid("asset/MinigameHoneycomb.unity"),
}

BARREN_REL = "Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset"
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Honeyguide"] = common.VESSEL_HONEYGUIDE
EXISTING["BarrenCellConfig"] = lib.existing_guid(BARREN_REL)

# ── The round ────────────────────────────────────────────────────────────────
ROUND_SECONDS = 90            # kept in sync with EndConditionOverridesSO.DefaultHoneycombRoundSeconds
CRYSTALS_BY_INTENSITY = [24, 16, 10, 6]
SCATTER_RADIUS = 900
SCATTER_SEED = 109
SPAWN = common.spawn_block(150, 0, 0)   # Barren HAS a nucleus: 150 outside it, symmetric (Tapestry's ring)

# ── The trail model: what one Honeyguide lays per second, read off the shipped hull ──────────
PREFAB = g.read(common.HONEYGUIDE_PREFAB)


def one(src, pattern, what):
    m = re.search(pattern, src, re.M)
    assert m, f"{what} not found in Honeyguide.prefab"
    return float(m.group(1))


BASE = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", PREFAB, re.M)
assert BASE, "Honeyguide.prefab BaseScale not found"
base_x, base_y, base_z = (float(v) for v in BASE.groups())
x_scaler = one(PREFAB, r"^  XScaler: ([0-9.]+)$", "XScaler")
wavelength = one(PREFAB, r"^  initialWavelength: ([0-9.]+)$", "initialWavelength")
top_speed = one(PREFAB, r"^\s*DefaultMinimumSpeed:\s*([0-9.]+)$", "DefaultMinimumSpeed") \
    + one(PREFAB, r"^\s*DefaultThrottleScaler:\s*([0-9.]+)$", "DefaultThrottleScaler")

KEY_VOLUME = (base_x * x_scaler / 2.0) * base_y * base_z   # VesselPrismController.CreateBlock, width 1
KEYS_PER_SECOND = top_speed / wavelength
TRAIL_RATE = KEY_VOLUME * KEYS_PER_SECOND                    # volume / second, one pilot
# A domain of two pilots whose trail stands for about HALF the round (the rest is pecked off or laid on a slip) - the
# reference a comeback is measured against. The hive comb is extra, so a real lobby stands more and the rate below is
# worth a little less than modelled: still over one level a quarter behind.
REFERENCE_DOMAIN_VOLUME = 2 * 0.5 * ROUND_SECONDS * TRAIL_RATE
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameHoneycomb.asset"

# ── 1. .cs.meta, folder, doc ─────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Honeycomb")
g.text_meta("Assets/_Scripts/Controller/Arcade/HONEYCOMB.md")

# ── 2. Scoring rule: metric 12 = VolumeRemaining, points mode ────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/HoneycombScoringRule.asset", G_ASSET["HoneycombScoringRule"],
             lib.header_for(G_SCRIPT["HoneycombScoringRuleSO"], "HoneycombScoringRule") + """  metric: 12
  golfRules: 0
  volumeTemplate: '{count} Volume'
  breakdownTemplate: '{count} prisms standing'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME STANDING
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/HoneycombSettings.asset", G_ASSET["HoneycombSettings"],
             lib.header_for(G_SCRIPT["HoneycombSettingsSO"], "HoneycombSettings") +
             "  elementalCrystalCountByIntensity:\n" +
             "".join(f"  - {n}\n" for n in CRYSTALS_BY_INTENSITY) + f"""  crystalScatterRadius: {SCATTER_RADIUS}
  crystalScatterSeed: {SCATTER_SEED}
  leadAnnounceAfterSeconds: 20
  leadAnnounceMinGapSeconds: 10
  progressSampleSeconds: 1
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2: a building war with one side is a screensaver (Tapestry's rule).
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameHoneycomb"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameHoneycomb") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Honeycomb
  Description: Honeyguides only, in open water. Take an omni crystal and it becomes a hive - a
    comb of your prisms behind you, shielded while the Ratel plays the hive puzzle. Mass
    crystals bring the next hive sooner, and your trail counts too. Peck the others' mass
    down. When the clock runs out, the team with the most standing wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background("Honeycomb")}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameHoneycomb
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Honeyguide']}, type: 2}}
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

# ── 5. Toasts: the shared lead change (130, sampled by the controller), one hint ─
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Honeycomb.asset", G_ASSET["GameToastConfigHoneycomb"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Honeycomb") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(130, "{0} takes the lead - {1} volume standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Take an omni crystal and it becomes a hive - everything standing in your colour scores") +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigHoneycomb"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Honeycomb.asset", G_ASSET["ModePreviewHoneycomb"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Honeycomb") + f"""  Mode: {MODE_ID}
  Notes: 'The bare Barren cell the mode runs in, and nothing else - the arena IS what you build
    into it. What the preview teaches is the hive an omni crystal becomes. Vessel pinned to
    Honeyguide({common.HONEYGUIDE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {EXISTING['BarrenCellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {common.HONEYGUIDE_ID}
  ObjectiveText: Take an omni crystal for a hive
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 150
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewHoneycomb"])

# ── 7. Scene ─────────────────────────────────────────────────────────────────
errors = []
scene = common.clone_or_stand_down(
    g, "MinigameHoneycomb", G_ASSET["MinigameHoneycomb.unity"], errors,
    lambda: common.clone_scene(G_SCRIPT["HoneycombController"], G_SCRIPT["HoneycombTimeTurnMonitor"],
                               G_ASSET["HoneycombSettings"], G_ASSET["HoneycombScoringRule"],
                               [EXISTING["BarrenCellConfig"]], 0, SPAWN, 0,
                               monitor_extra=f"  duration: {ROUND_SECONDS}\n"))

# ── 8. Registries ────────────────────────────────────────────────────────────
build = g.read_current(lib.BUILD_SETTINGS)
after = "MinigameFlitstrike" if "MinigameFlitstrike.unity" in build else "MinigameSirocco"
g.register_arcade_card(G_ASSET["ArcadeGameHoneycomb"])
g.register_always_unlocked()
g.register_build_scene(after, "MinigameHoneycomb", G_ASSET["MinigameHoneycomb.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("honeycombRoundSeconds",
                    after="flitstrikeStrikeTarget" if "flitstrikeStrikeTarget:" in end else "siroccoPrismTarget",
                    value=ROUND_SECONDS)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
comeback_at_quarter = 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE
if comeback_at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {comeback_at_quarter:.2f} levels a quarter of the "
                  f"reference ({REFERENCE_DOMAIN_VOLUME:.0f}) behind (< 1)")
if len(CRYSTALS_BY_INTENSITY) != 4 or any(b > a for a, b in zip(CRYSTALS_BY_INTENSITY, CRYSTALS_BY_INTENSITY[1:])):
    errors.append("the crystal ladder must be four rungs, non-increasing (intensity is SCARCITY)")
barren = g.read(BARREN_REL)
if re.search(r"^  EnvironmentPrefab: \{fileID: [1-9]", barren, re.M):
    errors.append("Barren Cell Config grew an EnvironmentPrefab - Honeycomb's arena is meant to be bare")

so = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultHoneycombRoundSeconds = {ROUND_SECONDS};" not in so:
    errors.append("EndConditionOverridesSO.DefaultHoneycombRoundSeconds differs from ROUND_SECONDS")
settings_cs = g.read(SCRIPT_PATHS["HoneycombSettingsSO"])
if "{ " + ", ".join(str(n) for n in CRYSTALS_BY_INTENSITY) + " }" not in settings_cs \
        or f"crystalScatterSeed = {SCATTER_SEED};" not in settings_cs:
    errors.append("HoneycombSettingsSO's C# defaults differ from the authored scatter")
if f"Honeycomb = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Honeycomb is not {MODE_ID} (GAME_KIT_ROSTER.md row 19)")
if f"HoneycombHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.HoneycombHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md row 19)")

# The kit the mode rests on: an omni crystal opens a hive whose own-colour cells are the pilot's, super-shielded.
errors += common.kit_errors(g, ("hive",))

# The AI is the platform's (crystal seeking = hives); the mode adds no AI hook and never touches the kit.
ctrl = g.read(SCRIPT_PATHS["HoneycombController"])
for banned in ("SetExternalTargetProvider", "PerformShipControllerActions", "InputStatus", "Honeyguide" + "ConfigSO"):
    if banned in ctrl:
        errors.append(f"HoneycombController uses {banned} - the mode's AI is the platform's and it never touches the kit")

common.assert_scene(errors, scene,
                    must_have={"HoneycombController": G_SCRIPT["HoneycombController"],
                               "HoneycombTimeTurnMonitor": G_SCRIPT["HoneycombTimeTurnMonitor"],
                               "HoneycombSettings": G_ASSET["HoneycombSettings"],
                               "HoneycombScoringRule": G_ASSET["HoneycombScoringRule"],
                               "Barren": EXISTING["BarrenCellConfig"]},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}})
if scene is not None and f"  duration: {ROUND_SECONDS}\n" not in scene:
    errors.append("cloned scene's monitor carries no duration")

print(f"trail model: key {KEY_VOLUME:.1f} vol x {KEYS_PER_SECOND:.2f}/s = {TRAIL_RATE:.0f} vol/s per pilot; "
      f"reference domain {REFERENCE_DOMAIN_VOLUME:.0f}; comeback {COMEBACK_RATE} "
      f"({comeback_at_quarter:.2f} levels a quarter behind)")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Honeycomb"),
                   guid("text/Assets/_Scripts/Controller/Arcade/HONEYCOMB.md")])
