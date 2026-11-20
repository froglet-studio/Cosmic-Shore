#!/usr/bin/env python3
"""
Authors every serialized asset the Palisade game mode needs (GameModes.Palisade = 98, hint toast 173: the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md reserved for row 8).

Palisade is the Swordfish's MASS game (Garrett, 2026-11-15: "make all the missing games you can"): a TIMED wall war
scored on the prism volume a domain has STANDING at the whistle (ScoringMetric.VolumeRemaining, a live stock). The
Swordfish's Mass ability is Afterimage - for 5 s after the blade leaves the energized state it leaves prism copies of
itself where it swings, Mass scales their volume x1 -> x2.5, and at Mass 5 they are laid as danger. Its blade cuts any
rival prism it reaches. So the sword builds and the sword raids: an energized run through a rival's wall cuts it down
and leaves your afterimages standing in its place. See Assets/_Scripts/Controller/Arcade/PALISADE.md.

REUSED, NOT COPIED: Tapestry's shape (the Butterfly's timed mass war) - its scoring rule is subclassed for authored
words, its scene is cloned (the bare Barren cell, the timed monitor, four AI seats) with the identity and the hulls
swapped, and the controller is Tapestry's without the Butterfly's mode press (the Swordfish's sword hand is the
vessel's own SwordfishSwordAutopilot). Nothing about the vessel changes (the LOCKED mode rule).

THE COMEBACK RATE IS SIZED AGAINST A VOLUME, read off the SHIPPED Swordfish (its trail block on Swordfish.prefab and
SwordfishSwordAfterimageConfig.asset), so a vessel retune moves it or fails the assert.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_palisade_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402  (project_network_hashes, plain_scalar_errors, yaml_parse_errors)

MODE_ID = 98
TOAST_HINT = 173
TOAST_LEAD = 130          # DomainRaceLeadChanged, posted by the controller's own lead sampler (a timed mode has no target)
g = lib.Generator(MODE_ID, "Palisade")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "PalisadeController":      f"{AR}/Palisade/PalisadeController.cs",
    "PalisadeSettingsSO":      f"{AR}/Palisade/PalisadeSettingsSO.cs",
    "PalisadeScoringRuleSO":   f"{AR}/Palisade/PalisadeScoringRuleSO.cs",
    "PalisadeTimeTurnMonitor": f"{AR}/TurnMonitors/PalisadeTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGamePalisade":      guid("asset/ArcadeGamePalisade"),
    "PalisadeScoringRule":     guid("asset/PalisadeScoringRule"),
    "PalisadeSettings":        guid("asset/PalisadeSettings"),
    "GameToastConfigPalisade": guid("asset/GameToastConfigPalisade"),
    "ModePreviewPalisade":     guid("asset/ModePreviewPalisade"),
    "MinigamePalisade.unity":  guid("asset/MinigamePalisade.unity"),
}
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Swordfish"] = lib.VESSELS["Swordfish"]

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameTapestry.unity"
DONOR_PREVIEW = "Assets/_SO_Assets/Mode Previews/ModePreview_Tapestry.asset"
DONOR = {
    "TapestryController":      guid("script/TapestryController"),
    "TapestryTimeTurnMonitor": guid("script/TapestryTimeTurnMonitor"),
    "TapestrySettings":        guid("asset/TapestrySettings"),
    "TapestryScoringRule":     guid("asset/TapestryScoringRule"),
}
BUTTERFLY_ID = lib.VESSEL_CLASS_ID["TigerMoth"]
SWORDFISH_ID = lib.VESSEL_CLASS_ID["Swordfish"]

# ── The round ────────────────────────────────────────────────────────────────
ROUND_SECONDS = 90        # kept in sync with EndConditionOverridesSO.DefaultPalisadeRoundSeconds
CRYSTALS_BY_INTENSITY = [24, 16, 10, 6]

# ── The mass model: what one Swordfish adds per second, read off the shipped hull ───────────────
PREFAB = g.read("Assets/_Prefabs/Spacevessels/Swordfish.prefab")
AFTER = g.read("Assets/_SO_Assets/VesselActions/Swordfish/SwordfishSwordAfterimageConfig.asset")


def one(src, pattern, what):
    m = re.search(pattern, src, re.M)
    assert m, f"{what} not found"
    return float(m.group(1))


BASE = re.search(r"^  BaseScale: \{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}", PREFAB, re.M)
assert BASE, "Swordfish.prefab BaseScale (its trail block) not found"
base_x, base_y, base_z = (float(v) for v in BASE.groups())
x_scaler = one(PREFAB, r"^  XScaler: ([0-9.]+)$", "XScaler")
wavelength = one(PREFAB, r"^  initialWavelength: ([0-9.]+)$", "initialWavelength")
trail_on = re.search(r"^  spawnerEnabled: 1$", PREFAB, re.M) is not None
RAMP_TOP = 840.0          # the Swordfish's ramp ceiling (RampBoostActionExecutor; SWORDFISH.md)
CRUISE = RAMP_TOP * 0.5   # a pilot who turns, cuts and raids averages about half of it
KEY_VOLUME = (base_x * x_scaler / 2.0) * base_y * base_z            # VesselPrismController.CreateBlock, no width scaler
TRAIL_RATE = KEY_VOLUME * CRUISE / wavelength if trail_on else 0.0  # volume / second, one pilot

seg_len = one(AFTER, r"^  segmentLength: ([0-9.]+)$", "segmentLength")
coverage = one(AFTER, r"^  lengthCoverage: ([0-9.]+)$", "lengthCoverage")
width = one(AFTER, r"^  width: ([0-9.]+)$", "width")
thick = one(AFTER, r"^  thickness: ([0-9.]+)$", "thickness")
per_window = one(AFTER, r"^  maxAfterimagesPerWindow: ([0-9]+)$", "maxAfterimagesPerWindow")
BLADE_AT_REST = 24.0      # Space 0 blade length (SWORDFISH.md, Blade Reach 24 -> 40)
SEGMENTS = max(1, round(BLADE_AT_REST / seg_len))
AFTERIMAGE_VOLUME = SEGMENTS * seg_len * coverage * width * thick   # one afterimage at Mass 0
WINDOW_VOLUME = per_window * AFTERIMAGE_VOLUME                      # one energize's worth at Mass 0
WINDOWS_PER_ROUND = ROUND_SECONDS / 15.0                            # an energize about every 15 s (crowds, cooldown)

# A domain of two pilots who build about half the round - the reference a comeback is measured against. Conservative:
# a real lobby cuts more and so stands less, which only makes the rate below worth MORE.
REFERENCE_DOMAIN_VOLUME = 2 * (0.5 * ROUND_SECONDS * TRAIL_RATE + WINDOWS_PER_ROUND * WINDOW_VOLUME)
# A quarter of the reference behind buys 1.5 element levels (rounded to what lib.num writes, so the assert checks the
# number that ships).
COMEBACK_RATE = round(1.5 / (0.25 * REFERENCE_DOMAIN_VOLUME), 6)

DISPLAY = "Palisade"
DESCRIPTION = """Swordfish only, and an empty sky to wall in. Light your sword and the blade
    leaves afterimages of itself as it cools - prism walls where you swing it. Mass crystals
    make them bigger. The same blade cuts a rival's walls down, so a lit run through their wall
    leaves yours standing in its place. When the clock runs out, the team with the most standing
    wins."""

# ── 1. metas ────────────────────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Palisade")
g.text_meta(f"{AR}/PALISADE.md")

# ── 2. Scoring rule: metric 12 = VolumeRemaining, points mode (Tapestry's, its own words) ──
g.emit_asset("Assets/_SO_Assets/Scoring Rules/PalisadeScoringRule.asset", G_ASSET["PalisadeScoringRule"],
             lib.header_for(G_SCRIPT["PalisadeScoringRuleSO"], "PalisadeScoringRule") + """  metric: 12
  golfRules: 0
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME STANDING
  volumeTemplate: '{volume} Volume'
  breakdownTemplate: '{prisms} prisms'
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/PalisadeSettings.asset", G_ASSET["PalisadeSettings"],
             lib.header_for(G_SCRIPT["PalisadeSettingsSO"], "PalisadeSettings") +
             "  elementalCrystalCountByIntensity:\n" +
             "".join(f"  - {n}\n" for n in CRYSTALS_BY_INTENSITY) + f"""  crystalScatterRadius: 900
  crystalScatterSeed: {MODE_ID}
  leadAnnounceAfterSeconds: 20
  leadAnnounceMinGapSeconds: 10
  progressSampleSeconds: 1
  aiDecisionSeconds: 6
  aiRaidDeficitFraction: 0.1
  aiLateRaidSeconds: 25
  aiBuildOrbitFraction: 0.6
  aiBuildOrbitDegreesPerSecond: 6
  aiCrystalReach: 450
""")

# ── 4. Card ─────────────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2: a wall war with one side has nobody to cut.
g.emit_asset("Assets/_SO_Assets/Games/ArcadeGamePalisade.asset", G_ASSET["ArcadeGamePalisade"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGamePalisade") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: {DISPLAY}
  Description: {DESCRIPTION}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background('Palisade')}, type: 3}}
  GolfScoring: 0
  SceneName: MinigamePalisade
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Swordfish']}, type: 2}}
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

# ── 5. Toasts ───────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Palisade.asset", G_ASSET["GameToastConfigPalisade"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Palisade") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Light your sword and swing - it leaves walls as it cools. A lit run through "
                       "their wall leaves yours in its place", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigPalisade"])

# ── 6. Preview: Tapestry's bare cell, the Swordfish flying it ───────────────
errors = []
donor_preview = g.read(DONOR_PREVIEW)
m = re.search(r"^  PreviewCell:.*?(?=^  Vessel: )", donor_preview, re.M | re.S)
spawn = re.search(r"^  ObjectiveMetric: 12\n(.*)\Z", donor_preview, re.M | re.S)
if not m or not spawn:
    errors.append("ModePreview_Tapestry no longer has the shape this generator copies (PreviewCell .. Vessel, ObjectiveMetric 12 ..)")
    cells_block, spawn_block = "", ""
else:
    cells_block, spawn_block = m.group(0), spawn.group(1)
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Palisade.asset", G_ASSET["ModePreviewPalisade"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Palisade") + f"""  Mode: {MODE_ID}
  Notes: 'The bare Barren cell the mode runs in (Tapestry''s), and nothing else - the arena IS the walls you swing
    into it. What the preview teaches is the afterimage: light the sword, swing, and it leaves walls as it cools.
    Vessel pinned to Swordfish({SWORDFISH_ID}).'
""" + cells_block + f"""  Vessel: {SWORDFISH_ID}
  ObjectiveText: Light your sword and swing walls into place
  ObjectiveMetric: 12
""" + spawn_block)
g.register_preview(G_ASSET["ModePreviewPalisade"])


# ── 7. Scene: Tapestry's, with the identity and the AI hulls swapped ────────
def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["TapestryTimeTurnMonitor"], G_SCRIPT["PalisadeTimeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["TapestryController"], G_SCRIPT["PalisadeController"], "controller")
    scene = lib.swap_guid(scene, DONOR["TapestrySettings"], G_ASSET["PalisadeSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["TapestryScoringRule"], G_ASSET["PalisadeScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {BUTTERFLY_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SWORDFISH_ID}\n    PlayerName: AI", "AI templates", expected=4)
    taken = htr.project_network_hashes(SCENE_REL)
    donor_hashes = sorted({int(h) for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene)} - {0})
    for i, old in enumerate(donor_hashes):
        n = int(guid(f"MinigamePalisade/hash/{i}")[:8], 16)
        while n == 0 or n in taken:
            n = (n * 1103515245 + 12345) & 0xFFFFFFFF
        taken.add(n)
        scene = lib.swap_guid(scene, f"GlobalObjectIdHash: {old}\n", f"GlobalObjectIdHash: {n}\n",
                              f"network hash {old}", expected=scene.count(f"GlobalObjectIdHash: {old}\n"))
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigamePalisade.unity"
AUTHORED_BLOCKS = [f"  - vesselClass: {SWORDFISH_ID}\n    PlayerName: AI", f"  duration: {ROUND_SECONDS}\n"]
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigamePalisade", G_ASSET["MinigamePalisade.unity"], scene)

# ── 8. Registries ───────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGamePalisade"])
g.register_always_unlocked()
g.register_build_scene("MinigameTapestry", "MinigamePalisade", G_ASSET["MinigamePalisade.unity"])
g.set_end_condition("palisadeRoundSeconds", after="tapestryRoundSeconds", value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
comeback_at_quarter = 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE
if comeback_at_quarter < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} buys {comeback_at_quarter:.2f} levels a quarter of the "
                  f"reference ({REFERENCE_DOMAIN_VOLUME:.0f}) behind (< 1)")
if len(CRYSTALS_BY_INTENSITY) != 4 or any(b > a for a, b in zip(CRYSTALS_BY_INTENSITY, CRYSTALS_BY_INTENSITY[1:])):
    errors.append("the crystal ladder must be four rungs, non-increasing (intensity is SCARCITY)")
if AFTERIMAGE_VOLUME <= 0:
    errors.append("the afterimage model lays no volume - SwordfishSwordAfterimageConfig changed shape")
if not re.search(r"^  massVolumeMultiplier:\n    Enabled: 1\n    Value: [0-9.]+\n    Min: 1\n    Max: 2\.5\n    element: 2\n",
                 AFTER, re.M):
    errors.append("SwordfishSwordAfterimageConfig no longer scales afterimage volume x1 -> x2.5 with Mass - the "
                  "card's Mass promise is gone")
if f"Palisade = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Palisade is not the roster's reserved {MODE_ID}")
if f"PalisadeHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.PalisadeHint is not the roster's reserved {TOAST_HINT}")
if f"DefaultPalisadeRoundSeconds = {ROUND_SECONDS};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultPalisadeRoundSeconds differs from ROUND_SECONDS")
if "case GameModes.Palisade:" not in g.read("Assets/_Scripts/UI/MiniGameHUD.cs"):
    errors.append("MiniGameHUD.CreateObjectiveProviderForGameMode has no Palisade case")
if f"{MODE_ID}: 'MinigamePalisade'" not in g.read("Tools/Build/author_preview_spawns.py"):
    errors.append("author_preview_spawns.py SCENE_FOR_MODE has no Palisade row")
ctl = g.read(SCRIPT_PATHS["PalisadeController"])
# The LOCKED mode rule: the mode steers the AI and scores what stands; it never touches the vessel's kit.
for banned in ("PerformShipControllerActions", "InputStatus", "ElementalScaling", "IsUpgradeActive",
               "ButterflyAutopilotModeDriver"):
    if banned in ctl:
        errors.append(f"PalisadeController mentions {banned} - a mode never changes the vessel")
for field in ("PalisadeSettingsSO settings;", "ScoringRuleSO rule;", "Cell arenaCell;"):
    if field not in ctl:
        errors.append(f"PalisadeController lost '{field}' - the cloned scene's controller block names it")
rule_cs = g.read(SCRIPT_PATHS["PalisadeScoringRuleSO"])
if "class PalisadeScoringRuleSO : TapestryScoringRuleSO" not in rule_cs:
    errors.append("PalisadeScoringRuleSO must inherit Tapestry's rule")
tap_rule = g.read(f"{AR}/Tapestry/TapestryScoringRuleSO.cs")
for f in ("victoryHeader", "defeatHeader", "revealLabel"):
    if f"string {f} =" not in tap_rule:
        errors.append(f"TapestryScoringRuleSO no longer has {f} - the Palisade rule asset names it")

if scene is not None:
    for name, gd in G_SCRIPT.items():
        if name in ("PalisadeController", "PalisadeTimeTurnMonitor") and gd not in scene:
            errors.append(f"scene missing {name}")
    for name in ("PalisadeSettings", "PalisadeScoringRule"):
        if G_ASSET[name] not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {SWORDFISH_ID}\n") != 4 or f"  - vesselClass: {BUTTERFLY_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Swordfish AI templates")
    taken = htr.project_network_hashes(SCENE_REL)
    for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene):
        if int(h) != 0 and int(h) in taken:
            errors.append(f"scene GlobalObjectIdHash {h} is also carried by another scene or prefab")

errors += htr.plain_scalar_errors("Description", DESCRIPTION)
errors += htr.yaml_parse_errors(g.files)

print(f"mass model: trail {TRAIL_RATE:.0f} vol/s at {CRUISE:.0f} u/s, afterimage {AFTERIMAGE_VOLUME:.1f} vol "
      f"(x{per_window:.0f} a window = {WINDOW_VOLUME:.0f}); reference domain {REFERENCE_DOMAIN_VOLUME:.0f}; "
      f"comeback {COMEBACK_RATE} ({comeback_at_quarter:.2f} levels a quarter behind)")

g.finish(errors, referenced={"Vessel": EXISTING["Vessel_Swordfish"]},
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()))
